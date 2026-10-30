using System.Text.Json;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Testing.Integration;

using StackExchange.Redis;

namespace Hexalith.Parties.Platform.IntegrationTests;

/// <summary>Consumes only the runner-owned Parties dependency closure and private credential channel.</summary>
public sealed class PlatformEnvironmentFixture : AspireTopologyFixtureBase
{
    /// <inheritdoc/>
    protected override IReadOnlyList<TestEnvironmentEndpoint> Resources =>
    [
        new("eventstore", "eventstore", "http", TimeSpan.FromSeconds(30)),
        new("parties", "parties", "http", TimeSpan.FromSeconds(30)),
        new("tenants-api", "tenants", "http", TimeSpan.FromSeconds(30)),
        new("memories", "memories", "dapr-invocation", TimeSpan.FromSeconds(30)) { DaprInvocation = true },
    ];

    /// <summary>Gets the authenticated EventStore gateway client.</summary>
    public HttpClient Gateway => Client("eventstore");

    /// <summary>Gets the declared Tenants REST interface, which owns its system-scope query routing.</summary>
    public HttpClient Tenants => Client("tenants-api");

    /// <summary>Gets the authenticated Memories declared REST interface.</summary>
    public HttpClient Memories => Client("memories");

    /// <summary>Reads the exact persisted synthetic stream from the supplied run-owned state store.</summary>
    /// <param name="identity">The synthetic aggregate identity written through EventStore.</param>
    /// <param name="cancellationToken">The bounded test token.</param>
    /// <returns>Persisted metadata and complete event envelopes.</returns>
    public async Task<(AggregateMetadata Metadata, EventEnvelope[] Events)> ReadStoredStreamAsync(AggregateIdentity identity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (identity.TenantId != Environment.SyntheticTenantId)
        {
            throw new InvalidOperationException("Persistence observations require this run's synthetic tenant.");
        }
        Uri endpoint = Environment.GetEndpoint("redis", "parties", "tcp");
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, Environment.RequestCancellation);
        bound.CancelAfter(TimeSpan.FromSeconds(30));
        bound.Token.ThrowIfCancellationRequested();
        Task<ConnectionMultiplexer> connecting = ConnectionMultiplexer.ConnectAsync(new ConfigurationOptions
        {
            EndPoints = { { endpoint.Host, endpoint.Port } },
            ConnectTimeout = 5000, SyncTimeout = 5000, AsyncTimeout = 5000, AbortOnConnectFail = true, AllowAdmin = false,
        });
        ConnectionMultiplexer connection;
        try
        {
            connection = await connecting.WaitAsync(bound.Token).ConfigureAwait(false);
        }
        catch
        {
            _ = DisposeLateConnectionAsync(connecting);
            throw;
        }
        using IConnectionMultiplexer redis = connection;
        IDatabase database = redis.GetDatabase();
        // The descriptor does not invent an app-id binding. Observe the one exact actor suffix in this owned store.
        string suffix = $"||AggregateActor||{identity.ActorId}||{identity.MetadataKey}";
        var matches = new List<string>();
        await foreach (RedisKey key in redis.GetServer(endpoint.Host, endpoint.Port).KeysAsync(pattern: "*" + suffix, pageSize: 100).WithCancellation(bound.Token).ConfigureAwait(false))
        {
            string candidate = key.ToString();
            if (candidate.EndsWith(suffix, StringComparison.Ordinal)) { matches.Add(candidate); }
            if (matches.Count > 1) { throw new InvalidOperationException("The exact synthetic stream has ambiguous persisted ownership."); }
        }
        string metadataKey = matches.SingleOrDefault() ?? throw new InvalidOperationException("The exact aggregate metadata has no persisted bytes.");
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        RedisValue metadataValue = await database.HashGetAsync(metadataKey, "data").WaitAsync(bound.Token).ConfigureAwait(false);
        AggregateMetadata metadata = JsonSerializer.Deserialize<AggregateMetadata>(metadataValue.ToString(), options)
            ?? throw new InvalidOperationException("Persisted aggregate metadata is unreadable.");
        if (metadata.CurrentSequence < 1 || metadata.CurrentSequence > 100 || metadata.RetainedFloor != 1)
        {
            throw new InvalidOperationException("A fresh synthetic create requires a complete bounded stream.");
        }
        string prefix = metadataKey[..^identity.MetadataKey.Length];
        var events = new List<EventEnvelope>();
        for (long sequence = 1; sequence <= metadata.CurrentSequence; sequence++)
        {
            RedisValue value = await database.HashGetAsync(prefix + identity.EventStreamKeyPrefix + sequence, "data").WaitAsync(bound.Token).ConfigureAwait(false);
            if (value.IsNull) { throw new InvalidOperationException("An exact aggregate event key has no persisted bytes."); }
            events.Add(JsonSerializer.Deserialize<EventEnvelope>(value.ToString(), options)
                ?? throw new InvalidOperationException("A persisted event envelope is unreadable."));
        }
        return (metadata, events.ToArray());
    }

    private static async Task DisposeLateConnectionAsync(Task<ConnectionMultiplexer> connecting)
    {
        try
        {
            ConnectionMultiplexer connection = await connecting.ConfigureAwait(false);
            await connection.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // The caller retains its original cancellation/failure; abandoned connection cleanup owns no credentials or CTS.
        }
    }
}
