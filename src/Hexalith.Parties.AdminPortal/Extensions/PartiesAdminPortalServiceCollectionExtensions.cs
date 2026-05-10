using System.Diagnostics.CodeAnalysis;

using Hexalith.FrontComposer.Shell.Extensions;
using Hexalith.FrontComposer.Contracts.Registration;
using Hexalith.Parties.AdminPortal.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.Parties.AdminPortal.Extensions;

public static class PartiesAdminPortalServiceCollectionExtensions
{
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:RequiresUnreferencedCode",
        Justification = "The admin portal composes the FrontComposer shell exactly as a host would; domain discovery remains host-owned.")]
    public static IServiceCollection AddHexalithPartiesAdminPortal(this IServiceCollection services)
    {
        services.AddHexalithFrontComposerQuickstart();
        services.AddOptions<PartiesAdminPortalOptions>();
        services.AddScoped<IPartiesAdminPortalApiClient, PartiesAdminPortalApiClient>();
        services.AddScoped<PartiesAdminListCoordinator>();
        services.AddScoped<AdminPortalGdprStateCoordinator>();
        services.AddScoped<AdminPortalEventStoreAdminLinks>();
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
