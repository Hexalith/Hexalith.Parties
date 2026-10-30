using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

using CommunityToolkit.Aspire.Hosting.Dapr;

using Hexalith.EventStore.Aspire;

namespace Hexalith.Parties.Aspire.Tests;

/// <summary>Protects the published helper's platform composition and dependency boundary.</summary>
public class PartiesServerCompositionTests
{
    /// <summary>The helper launches root workspace source without building or referencing the server assembly.</summary>
    [Fact]
    public void MetadataLocatesOwnRootSourceAndSuppressesBuild()
    {
        var metadata = new PartiesServerProjectMetadata();
        metadata.ProjectPath.ShouldBe(Path.Combine(RepositoryProjectPaths.GetRepositoryRoot(),
            "src", "Hexalith.Parties", "Hexalith.Parties.csproj"));
        metadata.SuppressBuild.ShouldBeTrue();
        typeof(HexalithPartiesServerExtensions).Assembly.GetReferencedAssemblies()
            .ShouldNotContain(assembly => assembly.Name == "Hexalith.Parties"
                || assembly.Name!.StartsWith("Hexalith.Parties.", StringComparison.Ordinal));
    }

    /// <summary>The service shares the platform components and forwards caller-supplied sidecar settings.</summary>
    [Fact]
    public void CompositionSharesPlatformComponentsAndForwardsSidecarSettings()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        IResourceBuilder<ProjectResource> gateway = builder.AddProject("eventstore", Hexalith.Parties.PackageTests.RepositoryDependencyPaths.Resolve(
                RepositoryProjectPaths.GetRepositoryRoot(), "references/Hexalith.EventStore/src/Hexalith.EventStore/Hexalith.EventStore.csproj"));
        HexalithEventStoreResources topology = builder.AddHexalithEventStore(gateway, null);
        IResourceBuilder<ProjectResource> parties = builder.AddHexalithPartiesServer(topology,
            "/test/parties-access.yaml", "parties-smoke", "localhost:6050", "localhost:6060");

        parties.Resource.Name.ShouldBe("parties-smoke");
        IDaprSidecarResource sidecar = parties.Resource.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar;
        DaprSidecarOptions options = sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options;
        options.AppId.ShouldBe("parties-smoke");
        options.Config.ShouldBe("/test/parties-access.yaml");
        options.PlacementHostAddress.ShouldBe("localhost:6050");
        options.SchedulerHostAddress.ShouldBe("localhost:6060");
        options.EnableAppHealthCheck.ShouldBe(true);
        options.AppHealthCheckPath.ShouldBe("/alive");
        sidecar.Annotations.OfType<DaprComponentReferenceAnnotation>().Select(item => item.Component)
            .ShouldContain(topology.StateStore.Resource);
        sidecar.Annotations.OfType<DaprComponentReferenceAnnotation>().Select(item => item.Component)
            .ShouldContain(topology.PubSub.Resource);
        parties.Resource.Annotations.OfType<WaitAnnotation>().Select(item => item.Resource)
            .ShouldContain(gateway.Resource);
    }

    /// <summary>Invalid required arguments fail before adding a Parties resource.</summary>
    [Theory]
    [InlineData(null, "parties")]
    [InlineData("", "parties")]
    [InlineData(" ", "parties")]
    [InlineData("access.yaml", null)]
    [InlineData("access.yaml", "")]
    [InlineData("access.yaml", " ")]
    public void InvalidConfigurationDoesNotMutateTopology(string? config, string? appId)
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        HexalithEventStoreResources topology = builder.AddHexalithEventStore(
            builder.AddProject("eventstore", Hexalith.Parties.PackageTests.RepositoryDependencyPaths.Resolve(
                RepositoryProjectPaths.GetRepositoryRoot(), "references/Hexalith.EventStore/src/Hexalith.EventStore/Hexalith.EventStore.csproj")), null);
        IResource[] before = builder.Resources.ToArray();
        Should.Throw<ArgumentException>(() => builder.AddHexalithPartiesServer(topology, config!, appId!));
        builder.Resources.ShouldBe(before);
    }

    /// <summary>Null platform arguments fail explicitly.</summary>
    [Fact]
    public void NullPlatformArgumentsFailExplicitly()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        Should.Throw<ArgumentNullException>(() => builder.AddHexalithPartiesServer(null!, "access.yaml"));
        Should.Throw<ArgumentNullException>(() => HexalithPartiesServerExtensions.AddHexalithPartiesServer(null!, null!, "access.yaml"));
    }
}
