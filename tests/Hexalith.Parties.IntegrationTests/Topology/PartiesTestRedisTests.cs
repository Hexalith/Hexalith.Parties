extern alias apphost;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using Aspire.Hosting.Testing;

using CommunityToolkit.Aspire.Hosting.Dapr;

using Hexalith.Parties.IntegrationTests.HealthChecks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using NSubstitute;

using Shouldly;

using YamlDotNet.RepresentationModel;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Checks private-store routing before any sidecar can start.</summary>
[Collection("Non-parallel")]
public sealed class PartiesTestRedisTests
{
    [Fact]
    public async Task Configure_RoutesEveryStateAndPubSubComponentAndSidecarToFreshRedis()
    {
        IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
            .CreateAsync<apphost::Projects.Hexalith_Parties_AppHost>().ConfigureAwait(true);
        await using (builder.ConfigureAwait(true))
        {
            DaprSidecarAnnotation[] sidecars = builder.Resources
                .SelectMany(resource => resource.Annotations.OfType<DaprSidecarAnnotation>()).ToArray();
            string[] statelessSidecars = sidecars
                .Where(sidecar => !sidecar.Sidecar.Annotations.OfType<DaprComponentReferenceAnnotation>().Any())
                .Select(sidecar => sidecar.Sidecar.Name).ToArray();
            statelessSidecars.ShouldContain("eventstore-admin-ui-dapr");
            IResourceBuilder<ContainerResource> redis = PartiesTestRedis.Configure(builder);
            ResourceExtensions.TryGetContainerImageName(redis.Resource, out string? image).ShouldBeTrue();
            image.ShouldBe("docker.io/library/redis@sha256:ff02b58f971e7d7d156a1267e283fcbbeee91773b6aa36c49dac28ecfe28eadf");
            EndpointAnnotation endpoint = redis.Resource.Annotations.OfType<EndpointAnnotation>().Single();
            endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "127.0.0.1", 46379);
            redis.Resource.Annotations.OfType<ContainerMountAnnotation>().ShouldBeEmpty();
            redis.Resource.Annotations.OfType<ContainerLifetimeAnnotation>().ShouldBeEmpty();

            DaprComponentResource[] components = builder.Resources.OfType<DaprComponentResource>()
                .Where(component => component.Type is "state.redis" or "pubsub.redis" or "pubsub").ToArray();
            components.Length.ShouldBe(2);
            sidecars.Length.ShouldBeGreaterThanOrEqualTo(5);
            foreach (DaprSidecarAnnotation sidecar in sidecars)
            {
                var context = new EnvironmentCallbackContext(
                    new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run),
                    sidecar.Sidecar, new Dictionary<string, object>(), TestContext.Current.CancellationToken);
                foreach (EnvironmentCallbackAnnotation callback in sidecar.Sidecar.Annotations.OfType<EnvironmentCallbackAnnotation>())
                {
                    await callback.Callback(context);
                }

                var address = (IValueProvider)context.EnvironmentVariables["REDIS_HOST"];
                (await address.GetValueAsync(TestContext.Current.CancellationToken)).ShouldBe("127.0.0.1:46379");
                context.EnvironmentVariables["REDIS_PASSWORD"].ShouldBe(string.Empty);
            }

            // Run the SDK's rendering hook without starting containers or sidecars. Inspect
            // the actual files and environment passed to every generated Dapr executable.
            IDistributedApplicationBuilder renderer = DistributedApplication.CreateBuilder(
                new DistributedApplicationOptions { DisableDashboard = true });
            renderer.Services.RemoveAll<IDistributedApplicationEventingSubscriber>();
            renderer.AddDapr();
            foreach (IResource resource in builder.Resources)
            {
                renderer.Resources.Add(resource);
            }

            DistributedApplication application = renderer.Build();
            await using var applicationLifetime = application.ConfigureAwait(true);
            DistributedApplicationModel model = application.Services.GetRequiredService<DistributedApplicationModel>();
            IDistributedApplicationEventingSubscriber dapr = application.Services
                .GetServices<IDistributedApplicationEventingSubscriber>()
                .Single(subscriber => subscriber.GetType().Name == "DaprDistributedApplicationLifecycleHook");
            var eventing = Substitute.For<IDistributedApplicationEventing>();
            await dapr.SubscribeAsync(eventing, renderer.ExecutionContext, TestContext.Current.CancellationToken);
            var render = (Func<BeforeStartEvent, CancellationToken, Task>)eventing.ReceivedCalls().Single().GetArguments()[0]!;
            await render(new BeforeStartEvent(application.Services, model), TestContext.Current.CancellationToken);
            ExecutableResource[] executables = model.Resources.OfType<ExecutableResource>()
                .Where(resource => resource.Name.EndsWith("-dapr-cli", StringComparison.Ordinal)).ToArray();
            executables.Length.ShouldBe(sidecars.Length);
            foreach (ExecutableResource executable in executables)
            {
                var environment = new EnvironmentCallbackContext(
                    builder.ExecutionContext, executable, new Dictionary<string, object>(), TestContext.Current.CancellationToken);
                foreach (EnvironmentCallbackAnnotation callback in executable.Annotations.OfType<EnvironmentCallbackAnnotation>())
                {
                    await callback.Callback(environment);
                }

                var arguments = new List<object>();
                var commandLine = new CommandLineArgsCallbackContext(arguments, executable, TestContext.Current.CancellationToken);
                foreach (CommandLineArgsCallbackAnnotation callback in executable.Annotations.OfType<CommandLineArgsCallbackAnnotation>())
                {
                    await callback.Callback(commandLine);
                }

                string[] paths = arguments.Select((argument, index) => (argument, index))
                    .Where(item => item.argument is "--resources-path")
                    .Select(item => (string)arguments[item.index + 1]).ToArray();
                paths.ShouldNotBeEmpty();
                DaprSidecarAnnotation sidecar = sidecars
                    .Single(sidecar => executable.Name == $"{sidecar.Sidecar.Name}-cli");
                DaprComponentReferenceAnnotation[] references = sidecar.Sidecar.Annotations
                    .OfType<DaprComponentReferenceAnnotation>().ToArray();
                YamlMappingNode[] rendered = paths.SelectMany(path => Directory.GetFiles(path, "*.yaml"))
                    .Select(ReadComponent).ToArray();
                if (statelessSidecars.Contains(sidecar.Sidecar.Name, StringComparer.Ordinal))
                {
                    references.Length.ShouldBe(1);
                    rendered.Length.ShouldBe(1);
                    rendered.Single()["metadata"]["name"].ToString().ShouldBe("fixture-stateless-secretstore");
                    rendered.Single()["spec"]["type"].ToString().ShouldBe("secretstores.local.env");
                    continue;
                }

                YamlMappingNode secretStore = rendered.Single(document =>
                    document["metadata"]["name"].ToString() == "secretstore");
                secretStore["spec"]["type"].ToString().ShouldBe("secretstores.local.env");
                YamlMappingNode[] stores = rendered.Where(document =>
                    document["spec"]["type"].ToString() is "state.redis" or "pubsub.redis").ToArray();
                stores.Length.ShouldBe(references.Length);
                foreach (YamlMappingNode store in stores)
                {
                    store["auth"]["secretStore"].ToString().ShouldBe("secretstore");
                    var metadata = (YamlSequenceNode)store["spec"]["metadata"];
                    YamlNode host = metadata.Children.Single(item => item["name"].ToString() == "redisHost");
                    string hostVariable = host["secretKeyRef"]["name"].ToString();
                    host["secretKeyRef"]["key"].ToString().ShouldBe(hostVariable);
                    environment.EnvironmentVariables[hostVariable].ShouldBe("127.0.0.1:46379");
                    YamlNode password = metadata.Children.Single(item => item["name"].ToString() == "redisPassword");
                    string passwordVariable = password["secretKeyRef"]["name"].ToString();
                    password["secretKeyRef"]["key"].ToString().ShouldBe(passwordVariable);
                    environment.EnvironmentVariables[passwordVariable].ShouldBe(string.Empty);
                    if (store["spec"]["type"].ToString() == "state.redis")
                    {
                        metadata.Children.Single(item => item["name"].ToString() == "keyPrefix")["value"]
                            .ToString().ShouldBe("none");
                    }
                }
            }
        }
    }

    private static YamlMappingNode ReadComponent(string path)
    {
        var yaml = new YamlStream();
        using var reader = new StringReader(File.ReadAllText(path));
        yaml.Load(reader);
        return (YamlMappingNode)yaml.Documents.Single().RootNode;
    }

    [Fact]
    public void Configure_MissingComponents_FailsBeforeCreatingInfrastructure()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        Should.Throw<InvalidOperationException>(() => PartiesTestRedis.Configure(builder))
            .Message.ShouldContain("modelled Redis");
        builder.Resources.OfType<ContainerResource>().ShouldBeEmpty();
    }

    [Fact]
    public void Configure_UnmodelledSidecarComponents_FailsClosed()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        _ = builder.AddContainer("application", "example/image")
            .WithDaprSidecar(new DaprSidecarOptions { ResourcesPaths = ["/unrelated/components"] });
        Should.Throw<InvalidOperationException>(() => PartiesTestRedis.Configure(builder))
            .Message.ShouldContain("private Redis routing cannot be proved");
        builder.Resources.OfType<ContainerResource>().Select(resource => resource.Name).ShouldBe(["application"]);
    }
    [Theory]
    [InlineData("statestore", "state.redis")]
    [InlineData("pubsub", "pubsub.redis")]
    public void Configure_FileBackedReferencedComponent_FailsBeforeCreatingInfrastructure(string name, string type)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"parties-file-backed-component-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "component.yaml");
        string source = $"apiVersion: dapr.io/v1alpha1\nkind: Component\nmetadata:\n  name: {name}\nspec:\n  type: {type}\n  version: v1\n  metadata:\n    - name: redisHost\n      value: localhost:6379\n";
        File.WriteAllText(path, source);
        try
        {
            IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
            IResourceBuilder<IDaprComponentResource> component = builder.AddDaprComponent(
                name, type, new DaprComponentOptions { LocalPath = path });
            _ = builder.AddContainer("unrelated", "example/image")
                .WithDaprSidecar(sidecar => sidecar.WithReference(component));

            Should.Throw<InvalidOperationException>(() => PartiesTestRedis.Configure(builder))
                .Message.ShouldContain("File-backed Dapr components");
            builder.Resources.OfType<ContainerResource>().Select(resource => resource.Name).ShouldBe(["unrelated"]);
            File.ReadAllText(path).ShouldBe(source);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
