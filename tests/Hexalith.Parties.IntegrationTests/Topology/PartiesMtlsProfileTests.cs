using System.Net;
using System.Net.Sockets;
using System.Text;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;

using CommunityToolkit.Aspire.Hosting.Dapr;

using Hexalith.Parties.IntegrationTests.HealthChecks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using NSubstitute;

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
    [InlineData("eventstore")]
    [InlineData("eventstore-admin-ui")]
    [InlineData("memories")]
    public async Task AppChannelEndpoint_RendersTheActualListenerAndKeepsOtherEndpointsAsync(string name)
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(
            new DistributedApplicationOptions { DisableDashboard = true });
        builder.Services.RemoveAll<IDistributedApplicationEventingSubscriber>();
        builder.AddDapr();
        IResourceBuilder<ProjectResource> app = builder.AddResource(new ProjectResource(name))
            .WithHttpEndpoint(port: 44209, targetPort: 18695)
            .WithHttpsEndpoint(port: 44309, targetPort: 18795)
            .WithDaprSidecar();
        if (name == "eventstore-admin-ui") { _ = app.WithExplicitStart(); }
        IResourceBuilder<ProjectResource> unrelated = builder.AddResource(new ProjectResource("unrelated"))
            .WithHttpEndpoint(port: 44409, targetPort: 18895);
        PartiesMtlsProfile.ConfigureAppChannelEndpoint(builder, name);
        EndpointAnnotation http = app.Resource.Annotations.OfType<EndpointAnnotation>().Single(endpoint => endpoint.Name == "http");
        http.IsProxied.ShouldBeFalse();
        http.Port.ShouldBeNull();
        http.TargetPort.ShouldBeNull();
        EndpointAnnotation https = app.Resource.Annotations.OfType<EndpointAnnotation>().Single(endpoint => endpoint.Name == "https");
        https.IsProxied.ShouldBeTrue();
        https.Port.ShouldBe(44309);
        https.TargetPort.ShouldBe(18795);
        EndpointAnnotation other = unrelated.Resource.Annotations.OfType<EndpointAnnotation>().ShouldHaveSingleItem();
        other.IsProxied.ShouldBeTrue();
        other.Port.ShouldBe(44409);
        other.TargetPort.ShouldBe(18895);

        // Render the installed toolkit lifecycle without starting Dapr or the project.
        // The injected listener models the allocated Kestrel socket after a cold startup.
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        http.AllocatedEndpoint = new AllocatedEndpoint(http, "localhost", port,
            targetPortExpression: port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        DistributedApplication application = builder.Build();
        await using var applicationLifetime = application.ConfigureAwait(true);
        DistributedApplicationModel model = application.Services.GetRequiredService<DistributedApplicationModel>();
        IDistributedApplicationEventingSubscriber dapr = application.Services
            .GetServices<IDistributedApplicationEventingSubscriber>()
            .Single(subscriber => subscriber.GetType().Name == "DaprDistributedApplicationLifecycleHook");
        var eventing = Substitute.For<IDistributedApplicationEventing>();
        await dapr.SubscribeAsync(eventing, builder.ExecutionContext, TestContext.Current.CancellationToken);
        var render = (Func<BeforeStartEvent, CancellationToken, Task>)eventing.ReceivedCalls().Single().GetArguments()[0]!;
        await render(new BeforeStartEvent(application.Services, model), TestContext.Current.CancellationToken);
        string cliName = app.Resource.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar.Name + "-cli";
        ExecutableResource cli = model.Resources.OfType<ExecutableResource>().Single(resource => resource.Name == cliName);
        var arguments = new List<object>();
        var commandLine = new CommandLineArgsCallbackContext(arguments, cli, TestContext.Current.CancellationToken);
        foreach (CommandLineArgsCallbackAnnotation callback in cli.Annotations.OfType<CommandLineArgsCallbackAnnotation>())
        {
            await callback.Callback(commandLine);
        }
        arguments.Count(argument => argument is "--app-port").ShouldBe(1);
        Convert.ToString(arguments[arguments.IndexOf("--app-port") + 1], System.Globalization.CultureInfo.InvariantCulture)
            .ShouldBe(port.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task AdminServerSidecarEndpoint_FollowsDeferredConsumerUriWithoutProducerPortTemplates()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        IResourceBuilder<ProjectResource> gateway = builder.AddResource(new ProjectResource("eventstore")).WithDaprSidecar();
        IResourceBuilder<ProjectResource> admin = builder.AddResource(new ProjectResource("eventstore-admin"))
            .WithEnvironment("AdminServer__EventStoreDaprHttpEndpoint", "http://localhost:44209");
        PartiesMtlsProfile.ConfigureAdminServerSidecarEndpoint(builder);
        // The generated CLI does not exist at configuration time, matching toolkit lifecycle order.
        string cliName = gateway.Resource.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar.Name + "-cli";
        IResourceBuilder<ExecutableResource> cli = builder.AddExecutable(cliName, "unused", ".").WithHttpEndpoint(port: 44209, targetPort: 18695);
        EndpointAnnotation endpoint = cli.Resource.Annotations.OfType<EndpointAnnotation>().Single();
        endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "localhost", 44825, targetPortExpression: "{{- portForServing \"eventstore-dapr-cli-http\" -}}");
        var context = new EnvironmentCallbackContext(builder.ExecutionContext, admin.Resource,
            new Dictionary<string, object>(), TestContext.Current.CancellationToken);
        foreach (EnvironmentCallbackAnnotation callback in admin.Resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            await callback.Callback(context).ConfigureAwait(true);
        }
        var selected = context.EnvironmentVariables["AdminServer__EventStoreDaprHttpEndpoint"].ShouldBeAssignableTo<EndpointReference>();
        (await selected.GetValueAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBe("http://localhost:44825");
        endpoint.TargetPort = 18795;
        endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "localhost", 44925, targetPortExpression: "{{- portForServing \"eventstore-dapr-cli-http\" -}}");
        (await selected.GetValueAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBe("http://localhost:44925");
        endpoint.IsProxied.ShouldBeTrue();
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
