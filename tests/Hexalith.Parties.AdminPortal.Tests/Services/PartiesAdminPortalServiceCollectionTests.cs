using Hexalith.FrontComposer.Contracts.Communication;
using Hexalith.FrontComposer.Contracts.Storage;
using Hexalith.FrontComposer.Shell.Services.Feedback;
using Hexalith.FrontComposer.Shell.State.DataGridNavigation;
using Hexalith.FrontComposer.Shell.State.ETagCache;
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
    }
}
