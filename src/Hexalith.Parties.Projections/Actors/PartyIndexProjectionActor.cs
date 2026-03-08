using System.Collections.Concurrent;
using System.Runtime.Serialization;
using System.Text.Json;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Projections.Abstractions;
using Hexalith.Parties.Projections.Configuration;
using Hexalith.Parties.Projections.Handlers;
using Hexalith.Parties.Projections.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.Parties.Projections.Actors;

public sealed partial class PartyIndexProjectionActor : Actor, IPartyIndexProjectionActor, IRemindable
{
    private const string ProjectionName = "party-index";
    private const string FlushReminderName = "flush-batch";
    private const string RebuildReminderName = "auto-rebuild";
    private const string ManifestStateKeySuffix = "manifest";
    private static readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, PartyIndexEntry>> s_lastKnownEntries = new(StringComparer.Ordinal);
    private readonly IIndexPartitionStrategy _partitionStrategy;
    private readonly ProjectionOptions _options;
    private readonly ILogger<PartyIndexProjectionActor> _logger;
    private readonly IProjectionRebuildService _rebuildService;
    private Dictionary<string, PartyIndexEntry>? _entries;
    private string? _activeStateKey;
    private int _pendingChanges;
    private volatile bool _isRebuilding;

    public PartyIndexProjectionActor(
        ActorHost host,
        IIndexPartitionStrategy partitionStrategy,
        IOptions<ProjectionOptions> options,
        IProjectionRebuildService rebuildService,
        ILogger<PartyIndexProjectionActor> logger)
        : base(host)
    {
        ArgumentNullException.ThrowIfNull(options);
        _partitionStrategy = partitionStrategy;
        _options = options.Value;
        _rebuildService = rebuildService;
        _logger = logger;
    }

    public async Task HandleEventAsync(string partyId, IEventPayload @event)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);

        string stateKey = ResolveStateKey(partyId);

        _entries ??= await LoadStateAsync(stateKey).ConfigureAwait(false);

        _entries.TryGetValue(partyId, out PartyIndexEntry? existingEntry);
        PartyIndexEntry? newEntry = PartyIndexProjectionHandler.Apply(partyId, @event, existingEntry);

        if (newEntry is not null)
        {
            _entries[partyId] = newEntry;
            _pendingChanges++;

            if (_pendingChanges >= _options.BatchSize)
            {
                await PersistStateAsync(stateKey).ConfigureAwait(false);
            }
            else if (_pendingChanges == 1)
            {
                await RegisterReminderAsync(
                    FlushReminderName,
                    null,
                    TimeSpan.FromMilliseconds(_options.BatchTimeWindowMs),
                    TimeSpan.FromMilliseconds(-1)).ConfigureAwait(false);
            }
        }
    }

    public async Task FlushAsync()
    {
        if (_pendingChanges > 0 && _activeStateKey is not null)
        {
            await PersistStateAsync(_activeStateKey).ConfigureAwait(false);
        }
    }

    public Task<bool> PingAsync() => Task.FromResult(true);

    public Task<bool> IsRebuildingAsync() => Task.FromResult(_isRebuilding);

    public async Task EraseAsync(string partyId)
    {
        string stateKey = ResolveStateKey(partyId);
        _entries ??= await LoadStateAsync(stateKey).ConfigureAwait(false);

        if (_entries.Remove(partyId))
        {
            await PersistStateAsync(stateKey).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyDictionary<string, PartyIndexEntry>> GetEntriesAsync()
    {
        if (_isRebuilding)
        {
            string actorId = Host.Id.GetId();
            string[] segs = actorId.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (segs.Length == 2)
            {
                string partitionKey = _partitionStrategy.GetPartitionKey(string.Empty);
                string cacheKey = $"{segs[0]}:{ProjectionName}:{partitionKey}";
                if (_entries is not null)
                {
                    return _entries;
                }

                if (s_lastKnownEntries.TryGetValue(cacheKey, out IReadOnlyDictionary<string, PartyIndexEntry>? cached))
                {
                    return cached;
                }
            }

            return new Dictionary<string, PartyIndexEntry>();
        }

        try
        {
            await FlushAsync().ConfigureAwait(false);
        }
        catch when (_entries is not null)
        {
            // If the backing state store is unavailable, continue serving the
            // in-memory snapshot already loaded in this actor activation.
        }

        if (_entries is not null)
        {
            return _entries;
        }

        string id = Host.Id.GetId();
        string[] segments = id.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length != 2)
        {
            return new Dictionary<string, PartyIndexEntry>();
        }

        string tenant = segments[0];
        string pk = _partitionStrategy.GetPartitionKey(string.Empty);
        string stateKey = $"{tenant}:{ProjectionName}:{pk}";

        try
        {
            _entries = await LoadStateAsync(stateKey).ConfigureAwait(false);
            _activeStateKey = stateKey;
        }
        catch when (TryGetCachedState(stateKey, out Dictionary<string, PartyIndexEntry>? cachedEntries))
        {
            // If the state store is temporarily unavailable during a cold read,
            // serve the last successfully persisted snapshot from this process.
            _entries = cachedEntries;
            _activeStateKey = stateKey;
        }

        return _entries ?? new Dictionary<string, PartyIndexEntry>();
    }

    public async Task ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)
    {
        if (string.Equals(reminderName, FlushReminderName, StringComparison.Ordinal))
        {
            await FlushAsync().ConfigureAwait(false);
            return;
        }

        if (string.Equals(reminderName, RebuildReminderName, StringComparison.Ordinal))
        {
            await HandleRebuildReminderAsync().ConfigureAwait(false);
        }
    }

    protected override async Task OnActivateAsync()
    {
        string actorId = Host.Id.GetId();
        string[] segments = actorId.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 2)
        {
            string tenant = segments[0];
            string partitionKey = _partitionStrategy.GetPartitionKey(string.Empty);
            string stateKey = $"{tenant}:{ProjectionName}:{partitionKey}";

            try
            {
                _entries = await LoadStateAsync(stateKey).ConfigureAwait(false);
                _activeStateKey = stateKey;
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
        }

        await base.OnActivateAsync().ConfigureAwait(false);
    }

    private async Task HandleRebuildReminderAsync()
    {
        string actorId = Host.Id.GetId();
        string[] segments = actorId.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length != 2)
        {
            return;
        }

        string tenant = segments[0];

        try
        {
            await _rebuildService.RebuildIndexProjectionAsync(tenant, default).ConfigureAwait(false);

            // Reload the rebuilt state
            string partitionKey = _partitionStrategy.GetPartitionKey(string.Empty);
            string stateKey = $"{tenant}:{ProjectionName}:{partitionKey}";
            _entries = await LoadStateAsync(stateKey).ConfigureAwait(false);
            _activeStateKey = stateKey;

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
            // Best-effort unregister
        }
    }

    protected override async Task OnDeactivateAsync()
    {
        await FlushAsync().ConfigureAwait(false);

        await base.OnDeactivateAsync().ConfigureAwait(false);
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

    private string ResolveStateKey(string partyId)
    {
        string actorId = Host.Id.GetId();
        string[] segments = actorId.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length != 2)
        {
            throw new InvalidOperationException($"Invalid actor id format '{actorId}'. Expected '{{tenant}}:{ProjectionName}'.");
        }

        string tenant = segments[0];
        string projection = segments[1];

        if (!string.Equals(projection, ProjectionName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Invalid actor projection segment '{projection}'. Expected '{ProjectionName}'.");
        }

        string partitionKey = _partitionStrategy.GetPartitionKey(partyId);
        string stateKey = $"{tenant}:{ProjectionName}:{partitionKey}";

        if (_activeStateKey is null)
        {
            _activeStateKey = stateKey;
            return _activeStateKey;
        }

        if (!string.Equals(_activeStateKey, stateKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "PartyIndexProjectionActor currently supports one partition key per actor activation. "
                + "Use SingleKeyPartitionStrategy for v1.0 or update actor state management before enabling multi-key partitioning.");
        }

        return _activeStateKey;
    }

    private async Task<Dictionary<string, PartyIndexEntry>> LoadStateAsync(string stateKey)
    {
        ConditionalValue<Dictionary<string, PartyIndexEntry>> result =
            await StateManager.TryGetStateAsync<Dictionary<string, PartyIndexEntry>>(stateKey, default).ConfigureAwait(false);
        Dictionary<string, PartyIndexEntry> entries = result.HasValue ? result.Value : [];
        s_lastKnownEntries[stateKey] = new Dictionary<string, PartyIndexEntry>(entries);
        return entries;
    }

    private async Task PersistStateAsync(string stateKey)
    {
        if (_entries is not null)
        {
            await StateManager.SetStateAsync(stateKey, _entries, default).ConfigureAwait(false);
            await StateManager.SetStateAsync(
                GetManifestStateKey(stateKey),
                _entries.Keys.OrderBy(static x => x, StringComparer.Ordinal).ToList(),
                default).ConfigureAwait(false);
            s_lastKnownEntries[stateKey] = new Dictionary<string, PartyIndexEntry>(_entries);
            _pendingChanges = 0;
        }
    }

    private static string GetManifestStateKey(string stateKey)
    {
        string[] segments = stateKey.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 2)
        {
            throw new InvalidOperationException($"Invalid index state key '{stateKey}'.");
        }

        return $"{segments[0]}:{ProjectionName}:{ManifestStateKeySuffix}";
    }

    private static bool TryGetCachedState(string stateKey, out Dictionary<string, PartyIndexEntry>? entries)
    {
        if (s_lastKnownEntries.TryGetValue(stateKey, out IReadOnlyDictionary<string, PartyIndexEntry>? cached))
        {
            entries = new Dictionary<string, PartyIndexEntry>(cached);
            return true;
        }

        entries = null;
        return false;
    }

    private static partial class Log
    {
        [LoggerMessage(
            EventId = 8310,
            Level = LogLevel.Error,
            Message = "Projection state corruption detected for actor {ActorKey} in tenant {TenantId}. Triggering auto-rebuild.")]
        public static partial void CorruptionDetected(ILogger logger, string actorKey, string tenantId, Exception exception);

        [LoggerMessage(
            EventId = 8311,
            Level = LogLevel.Information,
            Message = "Projection rebuild completed for actor {ActorKey}.")]
        public static partial void RebuildCompleted(ILogger logger, string actorKey);

        [LoggerMessage(
            EventId = 8312,
            Level = LogLevel.Error,
            Message = "Projection rebuild failed for actor {ActorKey}.")]
        public static partial void RebuildFailed(ILogger logger, string actorKey, Exception exception);
    }
}
