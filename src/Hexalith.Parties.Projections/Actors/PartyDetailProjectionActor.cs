using System.Collections.Concurrent;
using System.Runtime.Serialization;
using System.Text.Json;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Events;
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
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly ConcurrentDictionary<string, PartyDetail> s_lastKnownDetails = new(StringComparer.Ordinal);
    private readonly ILogger<PartyDetailProjectionActor> _logger;
    private readonly IProjectionRebuildService _rebuildService;
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
            Log.PartyCreatedReceivedForExistingState(_logger, actorPartyId);
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
            Log.NonJsonEventDropped(_logger, partyId, eventTypeName, serializationFormat);
            return;
        }

        Type? eventType = PartyEventTypeResolver.Resolve(eventTypeName);
        if (eventType is null)
        {
            if (PartyEventTypeResolver.IsAmbiguousShortName(eventTypeName))
            {
                Log.AmbiguousEventTypeDropped(_logger, partyId, eventTypeName);
            }
            else
            {
                Log.UnknownEventTypeDropped(_logger, partyId, eventTypeName);
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
                Log.RedactedEventDropped(_logger, partyId, eventTypeName, sequenceNumber, ex);
            }
            else
            {
                Log.PayloadDeserializationFailed(_logger, partyId, eventTypeName, ex);
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
            // the lifecycle can move forward.
            Log.RedactedEventDropped(_logger, partyId, eventTypeName, sequenceNumber, null);
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
            Log.SequenceCheckpointReset(_logger, partyId, ex.Message);
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

    public async Task<PartyDetail?> GetDetailAsync()
    {
        string actorId = Host.Id.GetId();
        string[] segments = actorId.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 3)
        {
            Log.MalformedActorId(_logger, actorId, ProjectionName);
            return null;
        }

        if (_isRebuilding)
        {
            string rebuildStateKey = $"{segments[0]}:{ProjectionName}:{segments[^1]}";
            return _cachedDetail ?? s_lastKnownDetails.GetValueOrDefault(rebuildStateKey);
        }

        string tenant = segments[0];
        string actorPartyId = segments[^1];
        string stateKey = $"{tenant}:{ProjectionName}:{actorPartyId}";

        try
        {
            ConditionalValue<PartyDetail> result =
                await StateManager.TryGetStateAsync<PartyDetail>(stateKey, default).ConfigureAwait(false);
            _cachedDetail = result.HasValue ? result.Value : null;
            if (_cachedDetail is not null)
            {
                s_lastKnownDetails[stateKey] = _cachedDetail;
            }

            return _cachedDetail;
        }
        catch when (_cachedDetail is not null || s_lastKnownDetails.TryGetValue(stateKey, out _))
        {
            return _cachedDetail ?? s_lastKnownDetails.GetValueOrDefault(stateKey);
        }
    }

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
        string[] segments = actorId.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 3)
        {
            Log.MalformedActorId(_logger, actorId, ProjectionName);
            return;
        }

        string tenant = segments[0];
        string actorPartyId = segments[^1];

        try
        {
            await _rebuildService.RebuildDetailProjectionAsync(tenant, actorPartyId, default).ConfigureAwait(false);

            // Reload the rebuilt state
            string stateKey = $"{tenant}:{ProjectionName}:{actorPartyId}";
            ConditionalValue<PartyDetail> result = await StateManager.TryGetStateAsync<PartyDetail>(stateKey, default).ConfigureAwait(false);
            if (result.HasValue)
            {
                _cachedDetail = result.Value;
                s_lastKnownDetails[stateKey] = result.Value;
            }

            _isRebuilding = false;
            Log.RebuildCompleted(_logger, actorId);
        }
        catch (Exception ex)
        {
            Log.RebuildFailed(_logger, actorId, ex);
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

    protected override async Task OnActivateAsync()
    {
        string actorId = Host.Id.GetId();
        string[] segments = actorId.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 3)
        {
            Log.MalformedActorId(_logger, actorId, ProjectionName);
            await base.OnActivateAsync().ConfigureAwait(false);
            return;
        }

        string tenant = segments[0];
        string actorPartyId = segments[^1];
        string stateKey = $"{tenant}:{ProjectionName}:{actorPartyId}";

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
            Log.CorruptionDetected(_logger, actorId, tenant, ex);
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
        string[] segments = actorId.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 3)
        {
            throw new InvalidOperationException($"Invalid actor id format '{actorId}'. Expected '{{tenant}}:{ProjectionName}:{{partyId}}'.");
        }

        string tenant = segments[0];
        string projection = segments[1];
        string actorPartyId = segments[^1];

        if (!string.Equals(projection, ProjectionName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Invalid actor projection segment '{projection}'. Expected '{ProjectionName}'.");
        }

        if (!string.Equals(actorPartyId, incomingPartyId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Party id mismatch. Actor id party '{actorPartyId}' does not match incoming '{incomingPartyId}'.");
        }

        return (actorPartyId, $"{tenant}:{ProjectionName}:{actorPartyId}");
    }

    private static partial class Log
    {
        [LoggerMessage(
            EventId = 8300,
            Level = LogLevel.Error,
            Message = "Projection state corruption detected for actor {ActorKey} in tenant {TenantId}. Triggering auto-rebuild.")]
        public static partial void CorruptionDetected(ILogger logger, string actorKey, string tenantId, Exception exception);

        [LoggerMessage(
            EventId = 8301,
            Level = LogLevel.Information,
            Message = "Projection rebuild completed for actor {ActorKey}.")]
        public static partial void RebuildCompleted(ILogger logger, string actorKey);

        [LoggerMessage(
            EventId = 8302,
            Level = LogLevel.Error,
            Message = "Projection rebuild failed for actor {ActorKey}.")]
        public static partial void RebuildFailed(ILogger logger, string actorKey, Exception exception);

        [LoggerMessage(
            EventId = 8303,
            Level = LogLevel.Warning,
            Message = "PartyDetail projection received event {EventTypeName} for {PartyId} with non-JSON serialization format '{SerializationFormat}'. Event dropped.")]
        public static partial void NonJsonEventDropped(ILogger logger, string partyId, string eventTypeName, string serializationFormat);

        [LoggerMessage(
            EventId = 8304,
            Level = LogLevel.Warning,
            Message = "PartyDetail projection could not resolve event type '{EventTypeName}' for {PartyId}. Event dropped.")]
        public static partial void UnknownEventTypeDropped(ILogger logger, string partyId, string eventTypeName);

        [LoggerMessage(
            EventId = 8305,
            Level = LogLevel.Warning,
            Message = "PartyDetail projection failed to deserialize event {EventTypeName} for {PartyId}. Event dropped.")]
        public static partial void PayloadDeserializationFailed(ILogger logger, string partyId, string eventTypeName, Exception exception);

        [LoggerMessage(
            EventId = 8306,
            Level = LogLevel.Error,
            Message = "PartyDetail projection found an ambiguous short event-type name '{EventTypeName}' for {PartyId} (multiple types share this short name). Event dropped — promote the emitter to a full-name event type to dispatch safely.")]
        public static partial void AmbiguousEventTypeDropped(ILogger logger, string partyId, string eventTypeName);

        [LoggerMessage(
            EventId = 8307,
            Level = LogLevel.Warning,
            Message = "PartyDetail projection dropped redacted event {EventTypeName} for {PartyId} at sequence {SequenceNumber} (post-erasure deserialization failure). Checkpoint advanced.")]
        public static partial void RedactedEventDropped(ILogger logger, string partyId, string eventTypeName, long sequenceNumber, Exception? exception);

        [LoggerMessage(
            EventId = 8308,
            Level = LogLevel.Warning,
            Message = "PartyDetail projection sequence checkpoint reset for {PartyId} (state-store read failed: {Reason}). Replay-from-zero will rebuild state.")]
        public static partial void SequenceCheckpointReset(ILogger logger, string partyId, string reason);

        [LoggerMessage(
            EventId = 8309,
            Level = LogLevel.Warning,
            Message = "PartyDetail projection actor id '{ActorId}' does not match expected '{{tenant}}:{ProjectionName}:{{partyId}}' shape. Operation aborted.")]
        public static partial void MalformedActorId(ILogger logger, string actorId, string projectionName);

        [LoggerMessage(
            EventId = 8320,
            Level = LogLevel.Warning,
            Message = "PartyCreated event received for {PartyId} but state already exists. Existing state preserved (replay-safe), but a non-replay duplicate or post-erasure re-create may indicate a bug.")]
        public static partial void PartyCreatedReceivedForExistingState(ILogger logger, string partyId);
    }
}
