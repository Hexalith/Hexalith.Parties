using System.Collections.Concurrent;
using System.Runtime.Serialization;
using System.Text.Json;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.Parties.Contracts.Events;
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
    private const string LastSequenceStateKeySuffix = "last-sequence";
    private const string RedactedFormat = "json-redacted";
    private const long UnloadedSequenceSentinel = long.MinValue;
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, PartyIndexEntry>> s_lastKnownEntries = new(StringComparer.Ordinal);
    private readonly IIndexPartitionStrategy _partitionStrategy;
    private readonly ProjectionOptions _options;
    private readonly ILogger<PartyIndexProjectionActor> _logger;
    private readonly IProjectionRebuildService _rebuildService;
    private Dictionary<string, PartyIndexEntry>? _entries;
    private Dictionary<string, long>? _lastProcessedSequencePerParty;
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

        // PartyCreated arriving for existing index entry is normally benign on replay, but a
        // non-replay duplicate (or a re-create with diverging payload after erasure) is a
        // warning condition worth surfacing. The Apply method silently returns the existing
        // entry in this case; logging here gives observability without changing semantics.
        if (@event is PartyCreated && existingEntry is not null)
        {
            Log.PartyCreatedReceivedForExistingState(_logger);
        }

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

        // Skip already-applied events to make replay-from-zero idempotent. The sequence map is
        // keyed per-party because the index actor is shared across all parties of a tenant.
        await EnsureLastSequenceMapLoadedAsync(partyId, cancellationToken).ConfigureAwait(false);
        if (_lastProcessedSequencePerParty is not null
            && _lastProcessedSequencePerParty.TryGetValue(partyId, out long last)
            && last != UnloadedSequenceSentinel
            && sequenceNumber <= last)
        {
            return;
        }

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
            // Resolved decision 1: skip-and-log redacted events (post-erasure tail-replay)
            // distinct from live deserialization failures so dashboards can separate signals.
            if (isRedacted)
            {
                Log.RedactedEventDropped(_logger, eventTypeName, sequenceNumber, ex.GetType().Name);
            }
            else
            {
                Log.PayloadDeserializationFailed(_logger, eventTypeName, ex.GetType().Name);
            }

            // Advance the checkpoint past the un-deserializable event so it is not retried.
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
            Log.RedactedEventDropped(_logger, eventTypeName, sequenceNumber, "Unknown");
            await PersistLastSequenceAsync(partyId, sequenceNumber, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task EnsureLastSequenceMapLoadedAsync(string partyId, CancellationToken cancellationToken)
    {
        // Lazy-load the in-memory cache once per actor activation. Individual party sequence
        // entries are persisted as separate state keys (see PersistLastSequenceAsync) so we
        // don't pay an O(N) re-write of the whole dictionary on every event delivery. The
        // first time a party is seen after activation we read its dedicated state key on
        // demand inside this method.
        _lastProcessedSequencePerParty ??= new Dictionary<string, long>(StringComparer.Ordinal);
        if (_lastProcessedSequencePerParty.ContainsKey(partyId))
        {
            return;
        }

        string sequenceKey = ResolveSequenceKey(partyId);
        try
        {
            ConditionalValue<long> result =
                await StateManager.TryGetStateAsync<long>(sequenceKey, cancellationToken).ConfigureAwait(false);
            // Always record an entry — the sentinel distinguishes "checked, no checkpoint"
            // from "not yet checked", preventing repeated state-store reads for the same
            // party on every event.
            _lastProcessedSequencePerParty[partyId] = result.HasValue ? result.Value : UnloadedSequenceSentinel;
        }
        catch (Exception ex) when (IsDeserializationFailure(ex) || ex is KeyNotFoundException)
        {
            // Persisted checkpoint corrupted or missing — treat as "no prior progress"
            // and let replay rebuild state. Infrastructure failures (state-store outage)
            // must propagate so the orchestrator surfaces them rather than silently
            // degrading to replay-from-zero on every command.
            Log.SequenceCheckpointReset(_logger, ex.GetType().Name);
            _lastProcessedSequencePerParty[partyId] = UnloadedSequenceSentinel;
        }
    }

    private async Task PersistLastSequenceAsync(string partyId, long sequenceNumber, CancellationToken cancellationToken)
    {
        _lastProcessedSequencePerParty ??= new Dictionary<string, long>(StringComparer.Ordinal);
        _lastProcessedSequencePerParty[partyId] = sequenceNumber;
        // Persist only the changed party's sequence, not the entire map. The previous shape
        // wrote O(parties_in_tenant) bytes on every event for every party — quadratic state
        // store traffic at scale.
        string sequenceKey = ResolveSequenceKey(partyId);
        await StateManager.SetStateAsync(sequenceKey, sequenceNumber, cancellationToken).ConfigureAwait(false);
    }

    private string ResolveSequenceKey(string partyId)
    {
        string actorId = Host.Id.GetId();
        string[] segments = actorId.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 1 || string.IsNullOrEmpty(segments[0]))
        {
            // Throwing here is safer than the previous "unknown" fallback: a malformed actor
            // id with that fallback would silently route every malformed-id activation across
            // tenants into the same `unknown:party-index:{partyId}:last-sequence` state key,
            // cross-contaminating tenant projection state.
            throw new InvalidOperationException(
                $"Unable to derive tenant from actor id '{actorId}'. Expected '{{tenant}}:{ProjectionName}'.");
        }

        string tenant = segments[0];
        // Per-party state key so each party's checkpoint is independently versioned. The
        // partition strategy is no longer part of the key — checkpoints follow the party id,
        // which is the only stable durable identity for the sequence number anyway.
        return $"{tenant}:{ProjectionName}:{partyId}:{LastSequenceStateKeySuffix}";
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

        // Always remove the companion checkpoint key on erasure. Without this, recreating a
        // party with the same id leaves a stale `:last-sequence` marker; events with sequence
        // numbers ≤ the stale value would be silently dropped on replay until the new stream
        // advanced past the high-water mark.
        string sequenceKey = ResolveSequenceKey(partyId);
        await StateManager.TryRemoveStateAsync(sequenceKey, default).ConfigureAwait(false);
        if (_lastProcessedSequencePerParty is not null)
        {
            _lastProcessedSequencePerParty.Remove(partyId);
        }
    }

    public async Task<IReadOnlyDictionary<string, PartyIndexEntry>> GetEntriesAsync()
    {
        if (_isRebuilding)
        {
            string actorId = Host.Id.GetId();
            string[] segs = actorId.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (segs.Length != 2)
            {
                Log.MalformedActorId(_logger, ProjectionName);
            }
            else
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
            Log.MalformedActorId(_logger, ProjectionName);
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

    public async Task<string?> GetEntriesJsonAsync()
    {
        IReadOnlyDictionary<string, PartyIndexEntry> entries = await GetEntriesAsync().ConfigureAwait(false);
        // Snapshot the dictionary before serialization so a concurrent FlushAsync mutation in
        // the same actor turn cannot throw `InvalidOperationException: Collection was modified`
        // mid-serialize. We materialize via ToDictionary rather than serialize the live view.
        Dictionary<string, PartyIndexEntry> snapshot = new(entries, StringComparer.Ordinal);
        return JsonSerializer.Serialize(snapshot, s_jsonOptions);
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
                Log.CorruptionDetected(_logger, ex.GetType().Name);
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
            Log.MalformedActorId(_logger, ProjectionName);
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
            Log.RebuildCompleted(_logger);
        }
        catch (Exception ex)
        {
            Log.RebuildFailed(_logger, ex.GetType().Name);
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
            Message = "PartyIndex projection state corruption detected. ExceptionType={ExceptionType}, Stage=ProjectionCorruptionDetected")]
        public static partial void CorruptionDetected(ILogger logger, string exceptionType);

        [LoggerMessage(
            EventId = 8311,
            Level = LogLevel.Information,
            Message = "PartyIndex projection rebuild completed. Stage=ProjectionRebuildCompleted")]
        public static partial void RebuildCompleted(ILogger logger);

        [LoggerMessage(
            EventId = 8312,
            Level = LogLevel.Error,
            Message = "PartyIndex projection rebuild failed. ExceptionType={ExceptionType}, Stage=ProjectionRebuildFailed")]
        public static partial void RebuildFailed(ILogger logger, string exceptionType);

        [LoggerMessage(
            EventId = 8313,
            Level = LogLevel.Warning,
            Message = "PartyIndex projection received event {EventTypeName} with non-JSON serialization format '{SerializationFormat}'. Event dropped.")]
        public static partial void NonJsonEventDropped(ILogger logger, string eventTypeName, string serializationFormat);

        [LoggerMessage(
            EventId = 8314,
            Level = LogLevel.Warning,
            Message = "PartyIndex projection could not resolve event type '{EventTypeName}'. Event dropped.")]
        public static partial void UnknownEventTypeDropped(ILogger logger, string eventTypeName);

        [LoggerMessage(
            EventId = 8315,
            Level = LogLevel.Warning,
            Message = "PartyIndex projection failed to deserialize event {EventTypeName}. Event dropped. ExceptionType={ExceptionType}")]
        public static partial void PayloadDeserializationFailed(ILogger logger, string eventTypeName, string exceptionType);

        [LoggerMessage(
            EventId = 8316,
            Level = LogLevel.Error,
            Message = "PartyIndex projection found an ambiguous short event-type name '{EventTypeName}' (multiple types share this short name). Event dropped.")]
        public static partial void AmbiguousEventTypeDropped(ILogger logger, string eventTypeName);

        [LoggerMessage(
            EventId = 8317,
            Level = LogLevel.Warning,
            Message = "PartyIndex projection dropped redacted event {EventTypeName} at sequence {SequenceNumber}. Checkpoint advanced. ExceptionType={ExceptionType}")]
        public static partial void RedactedEventDropped(ILogger logger, string eventTypeName, long sequenceNumber, string exceptionType);

        [LoggerMessage(
            EventId = 8318,
            Level = LogLevel.Warning,
            Message = "PartyIndex projection sequence checkpoint reset. Reason={Reason}. Replay-from-zero will rebuild state.")]
        public static partial void SequenceCheckpointReset(ILogger logger, string reason);

        [LoggerMessage(
            EventId = 8319,
            Level = LogLevel.Warning,
            Message = "PartyIndex projection actor id does not match expected projection shape. ProjectionName={ProjectionName}, Operation aborted.")]
        public static partial void MalformedActorId(ILogger logger, string projectionName);

        [LoggerMessage(
            EventId = 8321,
            Level = LogLevel.Warning,
            Message = "PartyCreated event received but index entry already exists. Existing entry preserved (replay-safe).")]
        public static partial void PartyCreatedReceivedForExistingState(ILogger logger);
    }
}
