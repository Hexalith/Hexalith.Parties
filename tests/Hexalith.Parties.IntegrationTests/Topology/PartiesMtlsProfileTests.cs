using System.Net;
using System.Net.Sockets;
using System.Text;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

using Hexalith.Parties.IntegrationTests.HealthChecks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Checks optional database readiness through the actual registered Redis PING checks.</summary>
[Collection("Non-parallel")]
public sealed class PartiesMtlsProfileTests
{
    [Fact]
    public void InternalGrpcListeners_AreDeclaredProxylessBeforeDynamicEndpointAllocation()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        PartiesTopologySession session = PartiesTopologySession.Create(isolatedIdentities: true);
        foreach (string name in session.InternalGrpcPorts.Keys.Append("memories-domain-service"))
        {
            builder.Resources.Add(new ProjectResource(name));
        }
        builder.Resources.Add(new ProjectResource("unrelated-project"));
        PartiesMtlsProfile.ConfigureInternalGrpcEndpoints(builder, session);
        foreach (ProjectResource project in builder.Resources.OfType<ProjectResource>())
        {
            EndpointAnnotation[] endpoints = project.Annotations.OfType<EndpointAnnotation>().ToArray();
            if (project.Name == "unrelated-project") { endpoints.ShouldBeEmpty(); continue; }
            int expected = project.Name == "memories-domain-service"
                ? session.MemoriesDomainInternalGrpcPort : session.InternalGrpcPorts[project.Name];
            EndpointAnnotation endpoint = endpoints.ShouldHaveSingleItem();
            endpoint.Name.ShouldBe("fixture-dapr-internal");
            endpoint.Port.ShouldBe(expected);
            endpoint.TargetPort.ShouldBe(expected);
            endpoint.IsProxied.ShouldBeFalse();
            endpoint.UriScheme.ShouldBe("tcp");
        }
    }

    [Theory]
    [InlineData("+PONG\r\n", HealthStatus.Healthy)]
    [InlineData("-LOADING Redis is loading data\r\n", HealthStatus.Unhealthy)]
    public async Task OptionalDatabases_RequirePongBeforeMemoriesCanStart(string response, HealthStatus expected)
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        var memories = new ProjectResource("memories");
        builder.Resources.Add(memories);
        PartiesMtlsProfile.ConfigureOptionalDependencies(builder);
        WaitAnnotation[] waits = memories.Annotations.OfType<WaitAnnotation>().ToArray();
        waits.Select(wait => wait.Resource.Name).Order(StringComparer.Ordinal)
            .ShouldBe(["fixture-memories-graph", "fixture-memories-search"]);
        waits.ShouldAllBe(wait => wait.WaitType == WaitType.WaitUntilHealthy);
        using ServiceProvider services = builder.Services.BuildServiceProvider();
        HealthCheckService health = services.GetRequiredService<HealthCheckService>();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        foreach (WaitAnnotation wait in waits)
        {
            string check = wait.Resource.Annotations.OfType<HealthCheckAnnotation>().Single().Key;
            EndpointAnnotation endpoint = wait.Resource.Annotations.OfType<EndpointAnnotation>().Single();
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port);
            Task server = RespondToPingAsync(listener, response, timeout.Token);
            HealthReport report = await health.CheckHealthAsync(registration => registration.Name == check, timeout.Token).ConfigureAwait(true);
            await server.ConfigureAwait(true);
            report.Entries.ShouldContainKey(check);
            report.Status.ShouldBe(expected);
        }
    }

    private static async Task RespondToPingAsync(TcpListener listener, string response, CancellationToken cancellationToken)
    {
        using TcpClient connection = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
        NetworkStream stream = connection.GetStream();
        var request = new byte[6];
        await stream.ReadExactlyAsync(request, cancellationToken).ConfigureAwait(false);
        Encoding.UTF8.GetString(request).ShouldBe("PING\r\n");
        await stream.WriteAsync(Encoding.UTF8.GetBytes(response), cancellationToken).ConfigureAwait(false);
    }
}
