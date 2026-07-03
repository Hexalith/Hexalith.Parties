using Hexalith.FrontComposer.Shell.Extensions;
using Hexalith.Parties.AdminPortal.Extensions;
using Hexalith.Parties.Client.Abstractions;
using Hexalith.Parties.Client.AdminPortal;
using Hexalith.Parties.Client.Extensions;
using Hexalith.Parties.ConsumerPortal.Services;
using Hexalith.Parties.UI.Authentication;
using Hexalith.Parties.UI.IdentityBinding;
using Hexalith.Parties.UI.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Parties.UI.Extensions;

/// <summary>
/// Service registration helpers for hosts that embed the Parties UI module.
/// </summary>
public static class PartiesUiServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Parties UI BFF services used by the standalone Parties UI host.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">Host configuration.</param>
    /// <param name="enableGatewayAuthorization">Whether gateway HTTP clients should relay the signed-in user's bearer token.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddHexalithPartiesUiModule(
        this IServiceCollection services,
        IConfiguration configuration,
        bool enableGatewayAuthorization)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddHexalithPartiesAdminPortal();
        services.AddPartiesUiAuthorization();
        services.AddPartiesUiClaimsResolution();
        services.AddSelfScopedPartiesClient();
        services.AddScoped<IConsumerProfileDataClient, ConsumerProfileDataClient>();
        services.AddScoped<IConsumerProfileEditClient, ConsumerProfileEditClient>();
        services.AddScoped<IConsumerConsentClient, ConsumerConsentClient>();
        services.AddScoped<IConsumerPrivacyExportClient, ConsumerPrivacyExportClient>();
        services.AddScoped<IConsumerPrivacyErasureClient, ConsumerPrivacyErasureClient>();
        services.AddScoped<IConsumerPrivacyProcessingClient, ConsumerPrivacyProcessingClient>();
        services.AddIdentityBindingProvisioning();
        services.AddPartiesProjectionFreshness(configuration);

        if (!string.IsNullOrWhiteSpace(configuration["Parties:BaseUrl"]))
        {
            services.AddPartiesClient(configuration);
            services.AddTransient<DegradedResponseHeaderHandler>();

            IHttpClientBuilder queryClient = services.AddHttpClient(nameof(IPartiesQueryClient))
                .AddHttpMessageHandler<DegradedResponseHeaderHandler>();
            IHttpClientBuilder gdprClient = services.AddHttpClient(nameof(IAdminPortalGdprClient))
                .AddHttpMessageHandler<DegradedResponseHeaderHandler>();
            IHttpClientBuilder commandClient = services.AddHttpClient(nameof(IPartiesCommandClient));

            if (enableGatewayAuthorization)
            {
                _ = queryClient.AddFrontComposerGatewayAuthorization();
                _ = gdprClient.AddFrontComposerGatewayAuthorization();
                _ = commandClient.AddFrontComposerGatewayAuthorization();
            }
        }

        return services;
    }
}
