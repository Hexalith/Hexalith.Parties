using Hexalith.FrontComposer.Contracts.Registration;
using Hexalith.Parties.AdminPortal.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.Parties.AdminPortal.Extensions;

public static class PartiesAdminPortalServiceCollectionExtensions
{
    public static IServiceCollection AddHexalithPartiesAdminPortal(this IServiceCollection services)
    {
        services.AddOptions<PartiesAdminPortalOptions>();
        services.AddScoped<IPartiesAdminPortalApiClient, PartiesAdminPortalApiClient>();
        services.AddScoped<PartiesAdminListCoordinator>();
        services.AddScoped<AdminPortalPartyQueryService>();
        services.TryAddScoped<IAdminPortalAuthorizationService, AdminPortalAuthorizationService>();
        return services;
    }

    public static IServiceProvider RegisterHexalithPartiesAdminPortal(this IServiceProvider serviceProvider)
    {
        IFrontComposerRegistry? registry = serviceProvider.GetService<IFrontComposerRegistry>();
        registry?.RegisterDomain(PartiesAdminPortalManifest.Manifest);
        return serviceProvider;
    }
}
