using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Testing.Integration;

namespace Hexalith.Parties.Platform.IntegrationTests;

/// <summary>Consumes only the runner-owned Parties dependency closure and private credential channel.</summary>
public sealed class PlatformEnvironmentFixture : AspireTopologyFixtureBase
{
    /// <inheritdoc/>
    protected override IReadOnlyList<TestEnvironmentEndpoint> Resources =>
    [
        new("eventstore", "eventstore", "http", TimeSpan.FromSeconds(30)),
        new("parties", "parties", "http", TimeSpan.FromSeconds(30)),
        new("redis", "parties", "tcp", TimeSpan.FromSeconds(30), Authenticated: false),
        new("tenants-api", "tenants", "http", TimeSpan.FromSeconds(30)),
        new("memories", "memories", "dapr-invocation", TimeSpan.FromSeconds(30)) { DaprInvocation = true },
    ];

    /// <summary>Gets the authenticated EventStore gateway client.</summary>
    public HttpClient Gateway => Client("eventstore");

    /// <summary>Gets the declared Tenants REST interface, which owns its system-scope query routing.</summary>
    public HttpClient Tenants => Client("tenants-api");

    /// <summary>Gets the authenticated Memories declared REST interface.</summary>
    public HttpClient Memories => Client("memories");

    /// <summary>Reads the exact synthetic stream using the EventStore-owned persistence observer.</summary>
    /// <param name="identity">The synthetic aggregate identity written through EventStore.</param>
    /// <param name="cancellationToken">The bounded test token.</param>
    /// <returns>Persisted metadata and complete event envelopes.</returns>
    public Task<(AggregateMetadata Metadata, EventEnvelope[] Events)> ReadStoredStreamAsync(AggregateIdentity identity, CancellationToken cancellationToken) =>
        TestEnvironmentStreamReader.ReadStoredStreamAsync(Descriptor, "redis", "parties", identity, cancellationToken);
}
