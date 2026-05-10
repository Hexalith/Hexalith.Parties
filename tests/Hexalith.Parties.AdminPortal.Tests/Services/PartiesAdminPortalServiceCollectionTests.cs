using Hexalith.FrontComposer.Contracts.Communication;
using Hexalith.FrontComposer.Contracts.Registration;
using Hexalith.FrontComposer.Contracts.Storage;
using Hexalith.FrontComposer.Shell.Services.Feedback;
using Hexalith.FrontComposer.Shell.State.DataGridNavigation;
using Hexalith.FrontComposer.Shell.State.ETagCache;
using Hexalith.Parties.AdminPortal.Components;
using Hexalith.Parties.AdminPortal.Extensions;
using Hexalith.Parties.AdminPortal.Services;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.Parties.AdminPortal.Tests.Services;

public sealed class PartiesAdminPortalServiceCollectionTests
{
    [Fact]
    public void AddHexalithPartiesAdminPortal_ComposesFrontComposerShellServices()
    {
        var services = new ServiceCollection();

        services.AddHexalithPartiesAdminPortal();

        services.ShouldContain(static descriptor => descriptor.ServiceType == typeof(IStorageService));
        services.ShouldContain(static descriptor => descriptor.ServiceType == typeof(IETagCache));
        services.ShouldContain(static descriptor => descriptor.ServiceType == typeof(IAuthRedirector));
        services.ShouldContain(static descriptor => descriptor.ServiceType == typeof(ICommandFeedbackPublisher));
        services.ShouldContain(static descriptor => descriptor.ServiceType == typeof(DataGridNavigationEffects));
        services.ShouldContain(static descriptor => descriptor.ServiceType == typeof(IPartiesAdminPortalApiClient));
        services.ShouldContain(static descriptor => descriptor.ServiceType == typeof(AdminPortalEventStoreAdminLinks));
    }

    [Fact]
    public void RegisterHexalithPartiesAdminPortal_RegistersPartiesManifestForFrontComposerRouteDiscovery()
    {
        var registry = new RecordingFrontComposerRegistry();
        using ServiceProvider provider = new ServiceCollection()
            .AddSingleton<IFrontComposerRegistry>(registry)
            .BuildServiceProvider();

        provider.RegisterHexalithPartiesAdminPortal();

        DomainManifest manifest = registry.Manifests.Single();
        manifest.Name.ShouldBe("Parties");
        manifest.BoundedContext.ShouldBe("Parties");
        manifest.Projections.ShouldContain(typeof(PartiesAdminPortal).FullName!);
        PartiesAdminPortalManifest.Route.ShouldBe("/admin/parties");
    }

    [Fact]
    public void EventStoreAdminLinks_BuildSafeGenericUrlsWhenBaseAddressIsConfigured()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new PartiesAdminPortalOptions
        {
            EventStoreAdminUiBaseAddress = new Uri("https://admin.example/base/"),
        });
        var links = new AdminPortalEventStoreAdminLinks(options);

        Uri? stream = links.BuildStreamLink("tenant-a:party:party-123");
        Uri? correlation = links.BuildCorrelationLink("corr-123");

        stream!.ToString().ShouldBe("https://admin.example/base/streams?aggregateId=tenant-a%3Aparty%3Aparty-123");
        correlation!.ToString().ShouldBe("https://admin.example/base/correlations?correlationId=corr-123");
        stream.ToString().ShouldNotContain("Ada");
        stream.ToString().ShouldNotContain("ada@example.test");
    }

    [Fact]
    public void EventStoreAdminLinks_ReturnNullWhenBaseAddressIsUnavailable()
    {
        var links = new AdminPortalEventStoreAdminLinks(
            Microsoft.Extensions.Options.Options.Create(new PartiesAdminPortalOptions()));

        links.BuildStreamLink("party-1").ShouldBeNull();
        links.BuildCorrelationLink("corr-1").ShouldBeNull();
    }

    private sealed class RecordingFrontComposerRegistry : IFrontComposerRegistry
    {
        public List<DomainManifest> Manifests { get; } = [];

        public void AddNavGroup(string name, string boundedContext)
        {
        }

        public IReadOnlyList<DomainManifest> GetManifests() => Manifests;

        public void RegisterDomain(DomainManifest manifest) => Manifests.Add(manifest);
    }
}
