using System.Text.Json;

using Hexalith.EventStore.Client.Projections;

namespace Hexalith.Parties.Tests.Search;

/// <summary>Serialized SDK state shared by fresh service instances in recovery tests.</summary>
internal sealed class PartyMemoryTestReadModelStore : IReadModelStore, IReadModelConditionalEraser, IReadModelExpiringStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<(string StoreName, string Key), (string Json, int Version)> _state = [];
    private readonly Dictionary<(string StoreName, string Key), (DateTimeOffset Deadline, int Version)> _expirations = [];
    internal DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UnixEpoch;
    private Func<Task>? _beforeSave;
    private int _saveAttempts;

    public Func<Task>? BeforeSave
    {
        get { lock (_gate) { return _beforeSave; } }
        set { lock (_gate) { _beforeSave = value; } }
    }

    public bool RejectWrites { get; set; }

    public Exception? SaveFailure { get; set; }

    public int SaveAttempts
    {
        get { lock (_gate) { return _saveAttempts; } }
    }

    public IReadOnlyList<string> SerializedValues
    {
        get { lock (_gate) { return [.. _state.Values.Select(value => value.Json)]; } }
    }

    public Task<ReadModelEntry<TValue>> GetAsync<TValue>(string storeName, string key, CancellationToken cancellationToken = default) where TValue : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_expirations.TryGetValue((storeName, key), out var expiry) && expiry.Deadline <= UtcNow)
            {
                if (_state.TryGetValue((storeName, key), out var expired) && expired.Version == expiry.Version) _state.Remove((storeName, key));
                _expirations.Remove((storeName, key));
            }
            return Task.FromResult(_state.TryGetValue((storeName, key), out (string Json, int Version) value)
                ? new ReadModelEntry<TValue>(JsonSerializer.Deserialize<TValue>(value.Json), value.Version.ToString(System.Globalization.CultureInfo.InvariantCulture))
                : new ReadModelEntry<TValue>(null, null));
        }
    }

    public Task SaveAsync<TValue>(string storeName, string key, TValue value, CancellationToken cancellationToken = default) where TValue : class
        => throw new NotSupportedException("Recovery inventories require conditional writes.");

    public async Task<bool> TrySaveAsync<TValue>(string storeName, string key, TValue value, string etag, CancellationToken cancellationToken = default) where TValue : class
    {
        Func<Task>? hook;
        lock (_gate)
        {
            _saveAttempts++;
            hook = _beforeSave;
            _beforeSave = null;
        }

        // Interceptors may await competing writes and must never hold the state lock.
        if (hook is not null)
        {
            await hook().ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (SaveFailure is not null)
            {
                throw SaveFailure;
            }

            int version = _state.TryGetValue((storeName, key), out (string Json, int Version) current) ? current.Version : 0;
            if (RejectWrites || etag != (version == 0 ? string.Empty : version.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            {
                return false;
            }

            _expirations.Remove((storeName, key));
            _state[(storeName, key)] = (JsonSerializer.Serialize(value), version + 1);
            return true;
        }
    }

    public async Task<bool> TrySaveWithTimeToLiveAsync<TValue>(string storeName, string key, TValue value, string etag,
        TimeSpan timeToLive, CancellationToken cancellationToken = default) where TValue : class
    {
        bool saved = await TrySaveAsync(storeName, key, value, etag, cancellationToken).ConfigureAwait(false);
        if (saved)
        {
            lock (_gate) { _expirations[(storeName, key)] = (UtcNow + timeToLive, _state[(storeName, key)].Version); }
        }
        return saved;
    }

    public Task<bool> TryEraseAsync(string storeName, string key, string etag, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_state.TryGetValue((storeName, key), out (string Json, int Version) value))
            {
                return Task.FromResult(true);
            }

            return Task.FromResult(etag == value.Version.ToString(System.Globalization.CultureInfo.InvariantCulture) && _state.Remove((storeName, key)));
        }
    }

    public Task<(bool Present, string Etag)> TryReadEtagAsync(string storeName, string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult(_state.TryGetValue((storeName, key), out (string Json, int Version) value)
                ? (true, value.Version.ToString(System.Globalization.CultureInfo.InvariantCulture)) : (false, string.Empty));
        }
    }
}
