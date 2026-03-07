using System.Collections.Concurrent;
using System.Runtime.Serialization;
using System.Text.Json;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Events;
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
    private static readonly ConcurrentDictionary<string, PartyDetail> s_lastKnownDetails = new(StringComparer.Ordinal);
    private readonly ILogger<PartyDetailProjectionActor> _logger;
    private readonly IProjectionRebuildService _rebuildService;
    private PartyDetail? _cachedDetail;
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

    public Task<bool> PingAsync() => Task.FromResult(true);

    public Task<bool> IsRebuildingAsync() => Task.FromResult(_isRebuilding);

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
    }
}
