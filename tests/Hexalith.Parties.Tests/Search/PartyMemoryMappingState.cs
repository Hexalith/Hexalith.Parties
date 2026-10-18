using System.Text.Json;

using Dapr.Client;

using Hexalith.Parties.Search;

using NSubstitute;

namespace Hexalith.Parties.Tests.Search;

/// <summary>
/// Serialized state and ETag semantics for deterministic mapping interleavings.
/// Hooks run immediately before the conditional mutation checks its ETag.
/// </summary>
internal sealed class PartyMemoryMappingState
{
    private readonly Dictionary<string, (string Json, int Version)> _states = new(StringComparer.Ordinal);
    private (string Json, int Version)? _initial;
    private string? _primaryKey;

    public PartyMemoryMappingState(params PartyMemoryUnitMappingEntry[] entries)
    {
        SetEntries(entries);
        Client = Substitute.For<DaprClient>();
        Client.GetStateAndETagAsync<List<PartyMemoryUnitMappingEntry>>(
                "statestore", Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                ReadCount++;
                if (ReadFailure is not null)
                {
                    throw ReadFailure;
                }

                (string Json, int Version)? state = GetState(call.ArgAt<string>(1));
                return (Decode(state).ToList(), MissingETag ? string.Empty : ETag(state));
            });
        _ = Client.TrySaveStateAsync(
                "statestore", Arg.Any<string>(), Arg.Any<List<PartyMemoryUnitMappingEntry>>(),
                Arg.Any<string>(), Arg.Any<StateOptions?>(),
                Arg.Any<IReadOnlyDictionary<string, string>?>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                SaveAttempts++;
                await BeforeMutationAsync().ConfigureAwait(false);
                Func<Task>? hook = BeforeSave;
                BeforeSave = null;
                if (hook is not null)
                {
                    await hook().ConfigureAwait(false);
                }

                LastConcurrency = call.ArgAt<StateOptions?>(4)?.Concurrency;
                string key = call.ArgAt<string>(1);
                (string Json, int Version)? state = GetState(key);
                if (RejectWrites || (state is not null && call.ArgAt<string>(3) != ETag(state)))
                {
                    return false;
                }

                SetEntries(key, call.ArgAt<List<PartyMemoryUnitMappingEntry>>(2));
                return true;
            });
        _ = Client.TryDeleteStateAsync(
                "statestore", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<StateOptions?>(),
                Arg.Any<IReadOnlyDictionary<string, string>?>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                DeleteAttempts++;
                await BeforeMutationAsync().ConfigureAwait(false);
                Func<Task>? hook = BeforeDelete;
                BeforeDelete = null;
                if (hook is not null)
                {
                    await hook().ConfigureAwait(false);
                }

                LastConcurrency = call.ArgAt<StateOptions?>(3)?.Concurrency;
                string key = call.ArgAt<string>(1);
                (string Json, int Version)? state = GetState(key);
                if (RejectWrites || (state is not null && call.ArgAt<string>(2) != ETag(state)))
                {
                    return false;
                }

                _ = _states.Remove(key);
                return true;
            });
    }

    public DaprClient Client { get; }

    public IReadOnlyList<PartyMemoryUnitMappingEntry> Entries => Decode(PrimaryState);

    public bool KeyExists => PrimaryState is not null;

    public Exception? ReadFailure { get; set; }

    public bool MissingETag { get; set; }

    public bool RejectWrites { get; set; }

    public int ReadCount { get; private set; }

    public int SaveAttempts { get; private set; }

    public int DeleteAttempts { get; private set; }

    public ConcurrencyMode? LastConcurrency { get; private set; }

    public Func<Task>? BeforeSave { get; set; }

    public Func<Task>? BeforeDelete { get; set; }

    public Func<Task>? BeforeMutation { get; set; }

    private (string Json, int Version)? PrimaryState => _primaryKey is null ? _initial : GetState(_primaryKey);

    public void SetEntries(IEnumerable<PartyMemoryUnitMappingEntry> entries)
    {
        if (_primaryKey is null)
        {
            _initial = (JsonSerializer.Serialize(entries.ToList()), (_initial?.Version ?? 0) + 1);
        }
        else
        {
            SetEntries(_primaryKey, entries);
        }
    }

    public void DeleteKey()
    {
        if (_primaryKey is null)
        {
            _initial = null;
        }
        else
        {
            _ = _states.Remove(_primaryKey);
        }
    }

    private static string ETag((string Json, int Version)? state) => state is null ? string.Empty : $"etag-{state.Value.Version}";

    private static IReadOnlyList<PartyMemoryUnitMappingEntry> Decode((string Json, int Version)? state) => state is null
        ? []
        : JsonSerializer.Deserialize<List<PartyMemoryUnitMappingEntry>>(state.Value.Json) ?? [];

    private (string Json, int Version)? GetState(string key)
    {
        if (_primaryKey is null)
        {
            _primaryKey = key;
            if (_initial is { } initial)
            {
                _states[key] = initial;
                _initial = null;
            }
        }

        return _states.TryGetValue(key, out (string Json, int Version) state) ? state : null;
    }

    private void SetEntries(string key, IEnumerable<PartyMemoryUnitMappingEntry> entries)
    {
        int version = (GetState(key)?.Version ?? 0) + 1;
        _states[key] = (JsonSerializer.Serialize(entries.ToList()), version);
    }

    private async Task BeforeMutationAsync()
    {
        Func<Task>? hook = BeforeMutation;
        BeforeMutation = null;
        if (hook is not null)
        {
            await hook().ConfigureAwait(false);
        }
    }
}
