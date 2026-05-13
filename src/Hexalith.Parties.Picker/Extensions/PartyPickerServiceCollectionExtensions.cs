using Hexalith.Parties.Client.Abstractions;
using Hexalith.Parties.Client.Extensions;
using Hexalith.Parties.Picker.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Parties.Picker.Extensions;

public static class PartyPickerServiceCollectionExtensions
{
    public static IServiceCollection AddHexalithPartyPicker(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        if (!services.Any(d => d.ServiceType == typeof(IPartiesQueryClient)))
        {
            services.AddPartiesClient(configuration);
        }

        return services.AddHexalithPartyPicker();
    }

    public static IServiceCollection AddHexalithPartyPicker(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<PartyPickerApiClient>();
        return services;
    }
}
