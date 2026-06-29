using System.Collections.Concurrent;
using System.Runtime.Serialization;
using System.Text.Json;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Projections.Abstractions;
using Hexalith.Parties.Projections.Handlers;
using Hexalith.Parties.Projections.Services;

using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.Projections.Actors;

public sealed partial class PartyDetailProjectionActor : Actor, IPartyDetailProjectionActor, IRemindable
{
    private const string ProjectionName = "party-detail";
    private const string RebuildReminderName = "auto-rebuild";
    private const string LastSequenceStateKeySuffix = "last-sequence";
    private const string RedactedFormat = "json-redacted";
    private const long UnloadedSequenceSentinel = long.MinValue;
    private static readonly JsonSerializerOptions s_jsonOptions = PartiesJsonOptions.Default;
    private static readonly ConcurrentDictionary<string, PartyDetail> s_lastKnownDetails = new(StringComparer.Ordinal);
    private readonly ILogger<PartyDetailProjectionActor> _logger;
    private readonly IProjectionRebuildService _rebuildService;
    private readonly SemaphoreSlim _rebuildGate = new(1, 1);
    private PartyDetail? _cachedDetail;

    // Sentinel for "checkpoint not yet loaded from state store"; long.MinValue is used so that a
    // legitimate persisted sequence of 0 (some event stores number from zero) does not collide
    // with the not-loaded state.
    private long _lastProcessedSequence = UnloadedSequenceSentinel;
    private bool _lastSequenceLoaded;
    private volatile bool _isRebuilding;

    public PartyDetailProjectionActor(
        ActorHost host,
        IProjectionRebuildService rebuildService,
        ILogger<PartyDetailProjectionActor> logger)
        : base(host)
    {
        _rebuildService = rebuildService;
        _logger = logger;
    }

    public async Task HandleEventAsync(string partyId, IEventPayload @event)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);

        (string actorPartyId, string stateKey) = ResolveStateContext(partyId);
        ConditionalValue<PartyDetail> currentState = await StateManager.TryGetStateAsync<PartyDetail>(stateKey, default).ConfigureAwait(false);
        PartyDetail? existing = currentState.HasValue ? currentState.Value : null;

        // PartyCreated arriving for existing state is normally benign on replay, but a non-replay
        // duplicate (or a re-create with diverging payload after erasure) is a warning condition
        // worth surfacing. The Apply method silently returns the existing state in this case;
        // logging here gives observability without changing semantics.
        if (@event is PartyCreated && existing is not null)
        {
            Log.PartyCreatedReceivedForExistingState(_logger);
        }

        PartyDetail? newState = PartyDetailProjectionHandler.Apply(actorPartyId, @event, existing);
        if (newState is not null)
        {
            await StateManager.SetStateAsync(stateKey, newState, default).ConfigureAwait(false);
            _cachedDetail = newState;
            s_lastKnownDetails[stateKey] = newState;
        }
    }

    public async Task HandleSerializedEventAsync(
        string partyId,
        string eventTypeName,
        byte[] payload,
        string serializationFormat,
        long sequenceNumber,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventTypeName);
        ArgumentNullException.ThrowIfNull(payload);
        cancellationToken.ThrowIfCancellationRequested();

        // Skip already-applied events to make replay-from-zero idempotent. The orchestrator
        // calls GetEventsAsync(0) on every command, so without this guard every prior event
        // would re-apply on every successful command.
        await EnsureLastSequenceLoadedAsync(partyId, cancellationToken).ConfigureAwait(false);
        if (sequenceNumber <= _lastProcessedSequence)
        {
            return;
        }

        // Accept "json" and the new "json-redacted" marker (the latter is used after a party's
        // encryption key has been destroyed; payloads have personal-data fields replaced with
        // JSON null but otherwise round-trip through the same deserializer).
        bool isJson = string.Equals(serializationFormat, "json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(serializationFormat, RedactedFormat, StringComparison.OrdinalIgnoreCase);
        bool isRedacted = string.Equals(serializationFormat, RedactedFormat, StringComparison.OrdinalIgnoreCase);
        if (!isJson)
        {
            Log.NonJsonEventDropped(_logger, eventTypeName, serializationFormat);
            return;
        }

        Type? eventType = PartyEventTypeResolver.Resolve(eventTypeName);
        if (eventType is null)
        {
            if (PartyEventTypeResolver.IsAmbiguousShortName(eventTypeName))
            {
                Log.AmbiguousEventTypeDropped(_logger, eventTypeName);
            }
            else
            {
                Log.UnknownEventTypeDropped(_logger, eventTypeName);
            }

            return;
        }

        object? deserialized;
        try
        {
            deserialized = JsonSerializer.Deserialize(payload, eventType, s_jsonOptions);
        }
        catch (JsonException ex)
        {
            // Resolved decision 1: skip-and-log redacted events that fail to deserialize so
            // operations can see the drop, while letting projection delivery advance past the
            // event. Non-redacted deserialization failures take the same path but emit a
            // distinct event id so dashboards can separate "post-erasure tolerated drop" from
            // "live event corruption".
            if (isRedacted)
            {
                Log.RedactedEventDropped(_logger, ex, eventTypeName, sequenceNumber);
            }
            else
            {
                Log.PayloadDeserializationFailed(_logger, ex, eventTypeName);
            }

            // Advance the checkpoint so the same un-deserializable event is not retried on the
            // next replay-from-zero. Without this, the orchestrator's foreach would replay the
            // dropped event forever, masking forward progress.
            await PersistLastSequenceAsync(partyId, sequenceNumber, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (deserialized is IEventPayload eventPayload)
        {
            await HandleEventAsync(partyId, eventPayload).ConfigureAwait(false);
            await PersistLastSequenceAsync(partyId, sequenceNumber, cancellationToken).ConfigureAwait(false);
        }
        else if (isRedacted)
        {
            // Whole-payload redaction (root-level $enc collapsed to {}) yields a default-valued
            // instance that may not implement IEventPayload. Advance the checkpoint and log so
            // the lifecycle can move forward. Distinct event id from the deserialization-failure
            // path so dashboards can separate "whole-payload redaction" from "redacted-but-broken".
            Log.WholePayloadRedactedEventDropped(_logger, eventTypeName, sequenceNumber);
            await PersistLastSequenceAsync(partyId, sequenceNumber, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task EnsureLastSequenceLoadedAsync(string partyId, CancellationToken cancellationToken)
    {
        if (_lastSequenceLoaded)
        {
            return;
        }

        (string _, string stateKey) = ResolveStateContext(partyId);
        string sequenceKey = $"{stateKey}:{LastSequenceStateKeySuffix}";
        try
        {
            ConditionalValue<long> result = await StateManager.TryGetStateAsync<long>(sequenceKey, cancellationToken).ConfigureAwait(false);
            _lastProcessedSequence = result.HasValue ? result.Value : UnloadedSequenceSentinel;
        }
        catch (Exception ex) when (IsDeserializationFailure(ex) || ex is KeyNotFoundException)
        {
            // Persisted checkpoint is missing or unreadable — treat as "no prior progress" and
            // let replay rebuild state. Infrastructure failures (state-store outage, OOM, etc.)
            // must propagate so the orchestrator surfaces them rather than silently degrading
            // to replay-from-zero on every command.
            Log.SequenceCheckpointReset(_logger, ex);
            _lastProcessedSequence = UnloadedSequenceSentinel;
        }

        _lastSequenceLoaded = true;
    }

    private async Task PersistLastSequenceAsync(string partyId, long sequenceNumber, CancellationToken cancellationToken)
    {
        if (sequenceNumber <= _lastProcessedSequence)
        {
            return;
        }

        (string _, string stateKey) = ResolveStateContext(partyId);
        string sequenceKey = $"{stateKey}:{LastSequenceStateKeySuffix}";

        // Dapr Actor framework batches all SetStateAsync writes from a single actor turn into
        // one commit at turn end (via SaveStateAsync). The state-key write in HandleEventAsync
        // and this checkpoint write are therefore committed atomically when the state store
        // supports transactional writes (Redis, Cosmos, etc.). For non-transactional state
        // stores, a host crash mid-commit could still leave state and checkpoint divergent —
        // mitigation: the idempotent collection-mutation handlers dedup-by-id on replay.
        await StateManager.SetStateAsync(sequenceKey, sequenceNumber, cancellationToken).ConfigureAwait(false);
        _lastProcessedSequence = sequenceNumber;
    }

    public Task<bool> PingAsync() => Task.FromResult(true);

    public Task<bool> IsRebuildingAsync() => Task.FromResult(_isRebuilding);

    public async Task EraseAsync(string partyId)
    {
        (string actorPartyId, string stateKey) = ResolveStateContext(partyId);
        string sequenceKey = $"{stateKey}:{LastSequenceStateKeySuffix}";
        ConditionalValue<PartyDetail> currentState = await StateManager.TryGetStateAsync<PartyDetail>(stateKey, default).ConfigureAwait(false);
        PartyDetail? erased = PartyDetailProjectionHandler.ApplyErasure(actorPartyId, currentState.HasValue ? currentState.Value : null);
        if (erased is not null)
        {
            await StateManager.SetStateAsync(stateKey, erased, default).ConfigureAwait(false);
            _cachedDetail = erased;
            s_lastKnownDetails[stateKey] = erased;
        }
        else
        {
            // No state existed — nothing to erase
            await StateManager.TryRemoveStateAsync(stateKey, default).ConfigureAwait(false);
            _cachedDetail = null;
            s_lastKnownDetails.TryRemove(stateKey, out _);
        }

        // Always remove the companion checkpoint key on erasure. Without this, recreating a
        // party with the same id leaves a stale `:last-sequence` marker; events with sequence
        // numbers ≤ the stale value would be silently dropped on replay until the new stream
        // advances past the high-water mark.
        await StateManager.TryRemoveStateAsync(sequenceKey, default).ConfigureAwait(false);
        _lastProcessedSequence = UnloadedSequenceSentinel;
        _lastSequenceLoaded = false;
    }

    public async Task<PartyDetailProjectionReadResult> GetDetailReadAsync()
    {
        string actorId = Host.Id.GetId();
        if (!TryResolveActorStateContext(actorId, out string tenant, out string actorPartyId, out string stateKey))
        {
            return new PartyDetailProjectionReadResult
            {
                Detail = null,
                Freshness = ProjectionFreshnessMetadata.Create(ProjectionFreshnessStatus.Unavailable, ProjectionFreshnessMetadata.WarningProjectionContextUnavailable),
            };
        }

        if (_isRebuilding)
        {
            return new PartyDetailProjectionReadResult
            {
                Detail = _cachedDetail ?? s_lastKnownDetails.GetValueOrDefault(stateKey),
                Freshness = ProjectionFreshnessMetadata.Create(ProjectionFreshnessStatus.Rebuilding, ProjectionFreshnessMetadata.WarningProjectionRebuilding),
            };
        }

        try
        {
            ConditionalValue<PartyDetail> result =
                await StateManager.TryGetStateAsync<PartyDetail>(stateKey, default).ConfigureAwait(false);
            _cachedDetail = result.HasValue ? result.Value : null;
            if (_cachedDetail is not null)
            {
                s_lastKnownDetails[stateKey] = _cachedDetail;
            }

            return new PartyDetailProjectionReadResult
            {
                Detail = _cachedDetail,
                Freshness = ProjectionFreshnessMetadata.Create(ProjectionFreshnessStatus.Current),
            };
        }
        // Cancellation is terminal per story 2.7 advanced elicitation: do not coerce a canceled
        // read into a Stale success with cached data — propagate so the caller can honor it.
        catch (Exception ex) when (ex is not OperationCanceledException
            && (_cachedDetail is not null || s_lastKnownDetails.TryGetValue(stateKey, out _)))
        {
            return new PartyDetailProjectionReadResult
            {
                Detail = _cachedDetail ?? s_lastKnownDetails.GetValueOrDefault(stateKey),
                Freshness = ProjectionFreshnessMetadata.Create(ProjectionFreshnessStatus.Stale, ProjectionFreshnessMetadata.WarningProjectionStateStoreUnavailable),
            };
        }
    }

    public async Task<PartyDetail?> GetDetailAsync()
        => (await GetDetailReadAsync().ConfigureAwait(false)).Detail;

    public async Task<byte[]?> GetSerializedDetailAsync()
    {
        PartyDetail? detail = await GetDetailAsync().ConfigureAwait(false);
        return detail is null
            ? null
            : JsonSerializer.SerializeToUtf8Bytes(detail, s_jsonOptions);
    }

    public async Task<string?> GetDetailJsonAsync()
    {
        PartyDetail? detail = await GetDetailAsync().ConfigureAwait(false);
        return detail is null
            ? null
            : JsonSerializer.Serialize(detail, s_jsonOptions);
    }

    public async Task ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)
    {
        if (!string.Equals(reminderName, RebuildReminderName, StringComparison.Ordinal))
        {
            return;
        }

        string actorId = Host.Id.GetId();
        if (!TryResolveActorStateContext(actorId, out string tenant, out string actorPartyId, out string stateKey))
        {
            return;
        }

        await _rebuildGate.WaitAsync().ConfigureAwait(false);
        try
        {
            try
            {
                await _rebuildService.RebuildDetailProjectionAsync(tenant, actorPartyId, default).ConfigureAwait(false);

                // Reload the rebuilt state
                ConditionalValue<PartyDetail> result = await StateManager.TryGetStateAsync<PartyDetail>(stateKey, default).ConfigureAwait(false);
                if (result.HasValue)
                {
                    _cachedDetail = result.Value;
                    s_lastKnownDetails[stateKey] = result.Value;
                }

                _isRebuilding = false;
                Log.RebuildCompleted(_logger);
            }
            catch (Exception ex)
            {
                Log.RebuildFailed(_logger, ex);
            }

            try
            {
                await UnregisterReminderAsync(RebuildReminderName).ConfigureAwait(false);
            }
            catch
            {
                // Best-effort unregister; reminder has no repeat so it won't fire again
            }
        }
        finally
        {
            _rebuildGate.Release();
        }
    }

    protected override async Task OnActivateAsync()
    {
        string actorId = Host.Id.GetId();
        if (!TryResolveActorStateContext(actorId, out string tenant, out _, out string stateKey))
        {
            await base.OnActivateAsync().ConfigureAwait(false);
            return;
        }

        try
        {
            ConditionalValue<PartyDetail> result = await StateManager.TryGetStateAsync<PartyDetail>(stateKey, default).ConfigureAwait(false);
            if (result.HasValue)
            {
                _cachedDetail = result.Value;
                s_lastKnownDetails[stateKey] = result.Value;
            }
        }
        catch (Exception ex) when (IsDeserializationFailure(ex))
        {
            Log.CorruptionDetected(_logger, ex);
            _isRebuilding = true;

            // Schedule rebuild via reminder (non-blocking, runs in next actor turn)
            await RegisterReminderAsync(
                RebuildReminderName,
                null,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromMilliseconds(-1)).ConfigureAwait(false);
        }

        await base.OnActivateAsync().ConfigureAwait(false);
    }

    internal void SetRebuilding(bool value) => _isRebuilding = value;

    private static bool IsDeserializationFailure(Exception ex)
    {
        return ex is SerializationException
            or InvalidCastException
            or FormatException
            or JsonException
            || (ex.InnerException is not null && IsDeserializationFailure(ex.InnerException));
    }

    private (string PartyId, string StateKey) ResolveStateContext(string incomingPartyId)
    {
        string actorId = Host.Id.GetId();
        if (!TryResolveActorStateContext(actorId, out _, out string actorPartyId, out string stateKey))
        {
            // AC7: exception messages must remain metadata-only. The actor id is excluded because
            // it carries tenant, projection name, and party id segments — the Log.MalformedActorId
            // call inside TryResolveActorStateContext already captured the coarse segment count.
            throw new InvalidOperationException($"Invalid {ProjectionName} actor id shape. Expected three colon-separated segments.");
        }

        if (!string.Equals(actorPartyId, incomingPartyId, StringComparison.Ordinal))
        {
            // AC7: do not echo either party id. The mismatch fact is the only diagnostic we need;
            // the values themselves are projection-identifiers covered by the no-actor-key rule.
            throw new InvalidOperationException($"Party id mismatch between {ProjectionName} actor id and incoming command/event party id.");
        }

        return (actorPartyId, stateKey);
    }

    private bool TryResolveActorStateContext(
        string actorId,
        out string tenant,
        out string actorPartyId,
        out string stateKey)
    {
        string[] segments = actorId.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length != 3 || !string.Equals(segments[1], ProjectionName, StringComparison.Ordinal))
        {
            // AC7: log only the projection name and the coarse segment count so triage can
            // distinguish "too few segments" from "wrong projection segment" without ever
            // capturing the malformed identifier itself.
            Log.MalformedActorId(_logger, ProjectionName, segments.Length);
            tenant = string.Empty;
            actorPartyId = string.Empty;
            stateKey = string.Empty;
            return false;
        }

        tenant = segments[0];
        actorPartyId = segments[2];
        stateKey = $"{tenant}:{ProjectionName}:{actorPartyId}";
        return true;
    }

    private static partial class Log
    {
        // AC7 — structured logging contract:
        // The message template never embeds actor ids, party ids, state keys, partition keys, or
        // tenant identifiers. The Exception argument is passed positionally so the logging
        // framework attaches it as a top-level structured field (stack trace + inner exceptions
        // for triage); the formatted message itself does NOT render the exception. Exception
        // .Message strings are scrubbed at throw sites (ResolveStateContext / handlers) so the
        // structured exception payload also stays metadata-only.

        [LoggerMessage(
            EventId = 8300,
            Level = LogLevel.Error,
            Message = "PartyDetail projection state corruption detected. Stage=ProjectionCorruptionDetected")]
        public static partial void CorruptionDetected(ILogger logger, Exception exception);

        [LoggerMessage(
            EventId = 8301,
            Level = LogLevel.Information,
            Message = "PartyDetail projection rebuild completed. Stage=ProjectionRebuildCompleted")]
        public static partial void RebuildCompleted(ILogger logger);

        [LoggerMessage(
            EventId = 8302,
            Level = LogLevel.Error,
            Message = "PartyDetail projection rebuild failed. Stage=ProjectionRebuildFailed")]
        public static partial void RebuildFailed(ILogger logger, Exception exception);

        [LoggerMessage(
            EventId = 8303,
            Level = LogLevel.Warning,
            Message = "PartyDetail projection received event {EventTypeName} with non-JSON serialization format '{SerializationFormat}'. Event dropped.")]
        public static partial void NonJsonEventDropped(ILogger logger, string eventTypeName, string serializationFormat);

        [LoggerMessage(
            EventId = 8304,
            Level = LogLevel.Warning,
            Message = "PartyDetail projection could not resolve event type '{EventTypeName}'. Event dropped.")]
        public static partial void UnknownEventTypeDropped(ILogger logger, string eventTypeName);

        [LoggerMessage(
            EventId = 8305,
            Level = LogLevel.Warning,
            Message = "PartyDetail projection failed to deserialize event {EventTypeName}. Event dropped.")]
        public static partial void PayloadDeserializationFailed(ILogger logger, Exception exception, string eventTypeName);

        [LoggerMessage(
            EventId = 8306,
            Level = LogLevel.Error,
            Message = "PartyDetail projection found an ambiguous short event-type name '{EventTypeName}' (multiple types share this short name). Event dropped.")]
        public static partial void AmbiguousEventTypeDropped(ILogger logger, string eventTypeName);

        [LoggerMessage(
            EventId = 8307,
            Level = LogLevel.Warning,
            Message = "PartyDetail projection dropped redacted event {EventTypeName} at sequence {SequenceNumber} (post-erasure deserialization failure). Checkpoint advanced.")]
        public static partial void RedactedEventDropped(ILogger logger, Exception exception, string eventTypeName, long sequenceNumber);

        [LoggerMessage(
            EventId = 8322,
            Level = LogLevel.Warning,
            Message = "PartyDetail projection dropped whole-payload-redacted event {EventTypeName} at sequence {SequenceNumber} (no deserialization error). Checkpoint advanced.")]
        public static partial void WholePayloadRedactedEventDropped(ILogger logger, string eventTypeName, long sequenceNumber);

        [LoggerMessage(
            EventId = 8308,
            Level = LogLevel.Warning,
            Message = "PartyDetail projection sequence checkpoint reset. Replay-from-zero will rebuild state.")]
        public static partial void SequenceCheckpointReset(ILogger logger, Exception exception);

        [LoggerMessage(
            EventId = 8309,
            Level = LogLevel.Warning,
            Message = "PartyDetail projection actor id does not match expected projection shape. ProjectionName={ProjectionName}, SegmentCount={SegmentCount}. Operation aborted.")]
        public static partial void MalformedActorId(ILogger logger, string projectionName, int segmentCount);

        [LoggerMessage(
            EventId = 8320,
            Level = LogLevel.Warning,
            Message = "PartyCreated event received but state already exists. Existing state preserved (replay-safe).")]
        public static partial void PartyCreatedReceivedForExistingState(ILogger logger);
    }
}
