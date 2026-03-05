using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Projections.Abstractions;
using Hexalith.Parties.Projections.Configuration;
using Hexalith.Parties.Projections.Handlers;

using Microsoft.Extensions.Options;

namespace Hexalith.Parties.Projections.Actors;

public sealed class PartyIndexProjectionActor : Actor, IPartyIndexProjectionActor, IRemindable
{
    private const string ProjectionName = "party-index";
    private const string FlushReminderName = "flush-batch";
    private readonly IIndexPartitionStrategy _partitionStrategy;
    private readonly ProjectionOptions _options;
    private Dictionary<string, PartyIndexEntry>? _entries;
    private string? _activeStateKey;
    private int _pendingChanges;

    public PartyIndexProjectionActor(
        ActorHost host,
        IIndexPartitionStrategy partitionStrategy,
        IOptions<ProjectionOptions> options)
        : base(host)
    {
        ArgumentNullException.ThrowIfNull(options);
        _partitionStrategy = partitionStrategy;
        _options = options.Value;
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

    public async Task<IReadOnlyDictionary<string, PartyIndexEntry>> GetEntriesAsync()
    {
        await FlushAsync().ConfigureAwait(false);

        if (_entries is not null)
        {
            return _entries;
        }

        string actorId = Host.Id.GetId();
        string[] segments = actorId.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length != 2)
        {
            return new Dictionary<string, PartyIndexEntry>();
        }

        string tenant = segments[0];
        string partitionKey = _partitionStrategy.GetPartitionKey(string.Empty);
        string stateKey = $"{tenant}:{ProjectionName}:{partitionKey}";

        _entries = await LoadStateAsync(stateKey).ConfigureAwait(false);
        _activeStateKey = stateKey;
        return _entries;
    }

    public async Task ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)
    {
        if (reminderName == FlushReminderName)
        {
            await FlushAsync().ConfigureAwait(false);
        }
    }

    protected override async Task OnDeactivateAsync()
    {
        await FlushAsync().ConfigureAwait(false);

        await base.OnDeactivateAsync().ConfigureAwait(false);
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
        return result.HasValue ? result.Value : [];
    }

    private async Task PersistStateAsync(string stateKey)
    {
        if (_entries is not null)
        {
            await StateManager.SetStateAsync(stateKey, _entries, default).ConfigureAwait(false);
            _pendingChanges = 0;
        }
    }
}
