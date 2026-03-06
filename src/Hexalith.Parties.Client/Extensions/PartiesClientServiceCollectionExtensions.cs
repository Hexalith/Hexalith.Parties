using Hexalith.Parties.Client.Abstractions;

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

        services
            .AddOptions<PartiesClientOptions>()
            .Bind(partiesSection)
            .Validate(
                options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _),
                "Parties:BaseUrl must be an absolute URI.");

        services.AddHttpClient<IPartiesCommandClient, HttpPartiesCommandClient>(client =>
        {
            client.BaseAddress = validatedBaseAddress;
        });

        services.AddHttpClient<IPartiesQueryClient, HttpPartiesQueryClient>(client =>
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

        return Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? baseAddress)
            ? baseAddress
            : throw new InvalidOperationException("Parties:BaseUrl must be an absolute URI.");
    }
}
