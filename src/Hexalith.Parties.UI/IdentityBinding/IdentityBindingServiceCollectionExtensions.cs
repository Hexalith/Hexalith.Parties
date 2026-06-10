using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.Parties.UI.IdentityBinding;

public static class IdentityBindingServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityBindingProvisioning(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IIdentityBindingStore, InMemoryIdentityBindingStore>();
        services.TryAddSingleton<IIdentityProviderPartyAttributeClient, InMemoryIdentityProviderPartyAttributeClient>();
        services.AddScoped<IIdentityBindingProvisioningService, IdentityBindingProvisioningService>();
        return services;
    }
}
