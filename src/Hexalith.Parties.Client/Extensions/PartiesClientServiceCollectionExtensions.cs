using Hexalith.Parties.Client.Abstractions;
using Hexalith.Parties.Client.AdminPortal;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Parties.Client.Extensions;

public static class PartiesClientServiceCollectionExtensions
{
    public static IServiceCollection AddPartiesClient(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        IConfigurationSection partiesSection = configuration.GetSection("Parties");
        Uri validatedBaseAddress = GetValidatedBaseAddress(partiesSection);
        _ = GetValidatedTenant(partiesSection);

        services
            .AddOptions<PartiesClientOptions>()
            .Bind(partiesSection)
            .Validate(
                options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _),
                "Parties:BaseUrl must be an absolute URI.")
            .Validate(
                options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out Uri? baseAddress)
                    && IsHttpOrHttps(baseAddress),
                "Parties:BaseUrl must use http or https.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Tenant),
                "Parties:Tenant configuration is required.");

        services.AddHttpClient<IPartiesCommandClient, HttpPartiesCommandClient>(client =>
        {
            client.BaseAddress = validatedBaseAddress;
        });

        services.AddHttpClient<IPartiesQueryClient, HttpPartiesQueryClient>(client =>
        {
            client.BaseAddress = validatedBaseAddress;
        });

        services.AddHttpClient<IAdminPortalGdprClient, HttpAdminPortalGdprClient>(client =>
        {
            client.BaseAddress = validatedBaseAddress;
        });

        return services;
    }

    private static Uri GetValidatedBaseAddress(IConfigurationSection partiesSection)
    {
        string? baseUrl = partiesSection[nameof(PartiesClientOptions.BaseUrl)];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("Parties:BaseUrl configuration is required.");
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? baseAddress))
        {
            throw new InvalidOperationException("Parties:BaseUrl must be an absolute URI.");
        }

        return IsHttpOrHttps(baseAddress)
            ? baseAddress
            : throw new InvalidOperationException("Parties:BaseUrl must use http or https.");
    }

    private static string GetValidatedTenant(IConfigurationSection partiesSection)
    {
        string? tenant = partiesSection[nameof(PartiesClientOptions.Tenant)];
        return string.IsNullOrWhiteSpace(tenant)
            ? throw new InvalidOperationException("Parties:Tenant configuration is required.")
            : tenant;
    }

    private static bool IsHttpOrHttps(Uri baseAddress)
        => string.Equals(baseAddress.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(baseAddress.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
}
