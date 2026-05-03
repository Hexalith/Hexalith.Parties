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
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly ConcurrentDictionary<string, PartyDetail> s_lastKnownDetails = new(StringComparer.Ordinal);
    private readonly ILogger<PartyDetailProjectionActor> _logger;
    private readonly IProjectionRebuildService _rebuildService;
    private PartyDetail? _cachedDetail;
    private long _lastProcessedSequence = -1;
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
        PartyDetail? newState = PartyDetailProjectionHandler.Apply(actorPartyId, @event, currentState.HasValue ? currentState.Value : null);
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
        long sequenceNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventTypeName);
        ArgumentNullException.ThrowIfNull(payload);

        // Skip already-applied events to make replay-from-zero idempotent. The orchestrator
        // calls GetEventsAsync(0) on every command, so without this guard every prior event
        // would re-apply on every successful command.
        await EnsureLastSequenceLoadedAsync(partyId).ConfigureAwait(false);
        if (sequenceNumber <= _lastProcessedSequence)
        {
            return;
        }

        // Accept "json" and the new "json-redacted" marker (the latter is used after a party's
        // encryption key has been destroyed; payloads have personal-data fields replaced with
        // JSON null but otherwise round-trip through the same deserializer).
        bool isJson = string.Equals(serializationFormat, "json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(serializationFormat, "json-redacted", StringComparison.OrdinalIgnoreCase);
        if (!isJson)
        {
            Log.NonJsonEventDropped(_logger, partyId, eventTypeName, serializationFormat);
            return;
        }

        Type? eventType = PartyEventTypeResolver.Resolve(eventTypeName);
        if (eventType is null)
        {
            Log.UnknownEventTypeDropped(_logger, partyId, eventTypeName);
            return;
        }

        object? deserialized;
        try
        {
            deserialized = JsonSerializer.Deserialize(payload, eventType, s_jsonOptions);
        }
        catch (JsonException ex)
        {
            Log.PayloadDeserializationFailed(_logger, partyId, eventTypeName, ex);
            return;
        }

        if (deserialized is IEventPayload eventPayload)
        {
            await HandleEventAsync(partyId, eventPayload).ConfigureAwait(false);
            await PersistLastSequenceAsync(partyId, sequenceNumber).ConfigureAwait(false);
        }
    }

    private async Task EnsureLastSequenceLoadedAsync(string partyId)
    {
        if (_lastSequenceLoaded)
        {
            return;
        }

        (string _, string stateKey) = ResolveStateContext(partyId);
        string sequenceKey = $"{stateKey}:{LastSequenceStateKeySuffix}";
        try
        {
            ConditionalValue<long> result = await StateManager.TryGetStateAsync<long>(sequenceKey, default).ConfigureAwait(false);
            _lastProcessedSequence = result.HasValue ? result.Value : -1;
        }
        catch
        {
            // Best-effort: on read failure, assume nothing processed and let replay rebuild state.
            _lastProcessedSequence = -1;
        }

        _lastSequenceLoaded = true;
    }

    private async Task PersistLastSequenceAsync(string partyId, long sequenceNumber)
    {
        if (sequenceNumber <= _lastProcessedSequence)
        {
            return;
        }

        (string _, string stateKey) = ResolveStateContext(partyId);
        string sequenceKey = $"{stateKey}:{LastSequenceStateKeySuffix}";
        await StateManager.SetStateAsync(sequenceKey, sequenceNumber, default).ConfigureAwait(false);
        _lastProcessedSequence = sequenceNumber;
    }

    public Task<bool> PingAsync() => Task.FromResult(true);

    public Task<bool> IsRebuildingAsync() => Task.FromResult(_isRebuilding);

    public async Task EraseAsync(string partyId)
    {
        (string actorPartyId, string stateKey) = ResolveStateContext(partyId);
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
    }

    public async Task<PartyDetail?> GetDetailAsync()
    {
        string actorId = Host.Id.GetId();
        string[] segments = actorId.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 3)
        {
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
    }
}
