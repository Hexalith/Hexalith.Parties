using System.Net.Sockets;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Hexalith.Parties.IntegrationTests.HealthChecks;

/// <summary>Supplies disposable backing services needed only by the complete mTLS qualification.</summary>
internal static class PartiesMtlsProfile
{
    /// <summary>Declares sidecar-owned fixed listeners before DCP allocates dynamic proxy ports.</summary>
    /// <param name="builder">The owned application model.</param>
    /// <param name="session">The exact session whose sidecars bind these listeners.</param>
    internal static void ConfigureInternalGrpcEndpoints(IDistributedApplicationBuilder builder, PartiesTopologySession session)
    {
        foreach (ProjectResource project in builder.Resources.OfType<ProjectResource>())
        {
            int port;
            if (!session.InternalGrpcPorts.TryGetValue(project.Name, out port))
            {
                if (project.Name != "memories-domain-service") { continue; }
                port = session.MemoriesDomainInternalGrpcPort;
            }
            // The process that binds this socket is the project's daprd sidecar. A
            // proxyless declaration keeps DCP from taking it for another resource.
            _ = builder.CreateResourceBuilder(project).WithEndpoint(
                port: port, targetPort: port, scheme: "tcp", name: "fixture-dapr-internal", isProxied: false);
        }
    }

    /// <summary>Routes Memories' direct connections to session-owned databases without persistent volumes.</summary>
    internal static void ConfigureOptionalDependencies(IDistributedApplicationBuilder builder)
    {
        IResourceBuilder<ContainerResource> search = builder.AddContainer("fixture-memories-search", "redis/redis-stack-server")
            .WithImageSHA256("798ab84d9f266936b034ab11c4d04a2b8e4b441884c5aa7d17ac951eefdf742a")
            .WithEnvironment("REDIS_ARGS", "--save \"\" --appendonly no")
            .WithEndpoint(name: "tcp", targetPort: 6379, scheme: "tcp");
        IResourceBuilder<ContainerResource> graph = builder.AddContainer("fixture-memories-graph", "falkordb/falkordb")
            .WithImageSHA256("7927eb194df0fadf70e2deb6c20c8178743ec92aacd739c790f8a35ce5dee613")
            .WithEnvironment("REDIS_ARGS", "--save \"\" --appendonly no")
            .WithEndpoint(name: "tcp", targetPort: 6379, scheme: "tcp");
        RegisterRedisPing(builder, search);
        RegisterRedisPing(builder, graph);
        _ = builder.CreateResourceBuilder<ProjectResource>("memories")
            .WithEnvironment("ConnectionStrings__redis", ReferenceExpression.Create($"{search.GetEndpoint("tcp").Property(EndpointProperty.HostAndPort)}"))
            .WithEnvironment("ConnectionStrings__falkordb", ReferenceExpression.Create($"{graph.GetEndpoint("tcp").Property(EndpointProperty.HostAndPort)}"))
            .WaitFor(search)
            .WaitFor(graph);
    }
    private static void RegisterRedisPing(IDistributedApplicationBuilder builder, IResourceBuilder<ContainerResource> database)
    {
        string healthCheck = database.Resource.Name + "-ping";
        EndpointReference endpoint = database.GetEndpoint("tcp");
        builder.Services.AddHealthChecks().AddAsyncCheck(healthCheck,
            cancellationToken => PingRedisAsync(endpoint, cancellationToken), tags: null, timeout: TimeSpan.FromSeconds(3));
        _ = database.WithHealthCheck(healthCheck);
    }

    private static async Task<HealthCheckResult> PingRedisAsync(EndpointReference endpoint, CancellationToken cancellationToken)
    {
        try
        {
            using var connection = new TcpClient();
            await connection.ConnectAsync(endpoint.Host, endpoint.Port, cancellationToken).ConfigureAwait(false);
            NetworkStream stream = connection.GetStream();
            await stream.WriteAsync("PING\r\n"u8.ToArray(), cancellationToken).ConfigureAwait(false);
            var response = new byte[7];
            await stream.ReadExactlyAsync(response, cancellationToken).ConfigureAwait(false);
            return response.AsSpan().SequenceEqual("+PONG\r\n"u8)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Redis PING did not return PONG.");
        }
        catch (Exception exception) when (exception is SocketException or IOException or OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Redis PING did not complete.");
        }
    }

}
