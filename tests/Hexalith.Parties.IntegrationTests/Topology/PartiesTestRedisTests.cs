extern alias apphost;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

using CommunityToolkit.Aspire.Hosting.Dapr;

using Hexalith.Parties.IntegrationTests.HealthChecks;

using Shouldly;

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
            foreach (DaprComponentResource component in components)
            {
                // These annotations are SDK-owned implementation types; use the public value-provider
                // contract to verify the endpoint the SDK will resolve when generating component YAML.
                object providerAnnotation = component.Annotations.Single(annotation =>
                    annotation.GetType().Name == "DaprComponentValueProviderAnnotation");
                var valueProvider = (IValueProvider)providerAnnotation.GetType().GetProperty("ValueProvider")!
                    .GetValue(providerAnnotation)!;
                (await valueProvider.GetValueAsync(TestContext.Current.CancellationToken)).ShouldBe("127.0.0.1:46379");
            }

            DaprSidecarAnnotation[] sidecars = builder.Resources
                .SelectMany(resource => resource.Annotations.OfType<DaprSidecarAnnotation>()).ToArray();
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
        }
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
