using Hexalith.FrontComposer.Contracts.Registration;
using Hexalith.Parties.AdminPortal.Services;

using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Parties.AdminPortal.Extensions;

public static class PartiesAdminPortalServiceCollectionExtensions
{
    public static IServiceCollection AddHexalithPartiesAdminPortal(this IServiceCollection services)
    {
        services.AddOptions<PartiesAdminPortalOptions>();
        services.AddHttpClient<IPartiesAdminPortalApiClient, PartiesAdminPortalApiClient>();
        return services;
    }

    public static IServiceProvider RegisterHexalithPartiesAdminPortal(this IServiceProvider serviceProvider)
    {
        IFrontComposerRegistry? registry = serviceProvider.GetService<IFrontComposerRegistry>();
        registry?.RegisterDomain(PartiesAdminPortalManifest.Manifest);
        return serviceProvider;
    }
}
