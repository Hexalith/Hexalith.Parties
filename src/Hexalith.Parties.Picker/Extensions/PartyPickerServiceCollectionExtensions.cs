using Hexalith.Parties.Picker.Services;

using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Parties.Picker.Extensions;

public static class PartyPickerServiceCollectionExtensions
{
    public static IServiceCollection AddHexalithPartyPicker(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<PartyPickerApiClient>();
        return services;
    }
}
