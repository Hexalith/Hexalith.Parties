using Hexalith.FrontComposer.Contracts.Registration;
using Hexalith.Parties.AdminPortal.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hexalith.Parties.AdminPortal.Extensions;

public static class PartiesAdminPortalServiceCollectionExtensions
{
    public static IServiceCollection AddHexalithPartiesAdminPortal(this IServiceCollection services)
    {
        services
            .AddOptions<PartiesAdminPortalOptions>()
            .Validate(
                static options => options.ApiBaseAddress is not null,
                "PartiesAdminPortalOptions.ApiBaseAddress must be configured (call AddHexalithPartiesAdminPortal(...).Configure<PartiesAdminPortalOptions>(o => o.ApiBaseAddress = ...) at host wire-up).")
            .ValidateOnStart();
        services.AddHttpClient<IPartiesAdminPortalApiClient, PartiesAdminPortalApiClient>();
        services.AddScoped<PartiesAdminListCoordinator>();
        services.AddScoped<AdminPortalPartyQueryService>();
        return services;
    }

    public static IServiceProvider RegisterHexalithPartiesAdminPortal(this IServiceProvider serviceProvider)
    {
        IFrontComposerRegistry? registry = serviceProvider.GetService<IFrontComposerRegistry>();
        registry?.RegisterDomain(PartiesAdminPortalManifest.Manifest);
        return serviceProvider;
    }
}
