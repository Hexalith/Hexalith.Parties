using Hexalith.FrontComposer.Contracts.Rendering;
using Hexalith.Parties.UI.Components.Specimens;

using Microsoft.AspNetCore.Components;

namespace Hexalith.Parties.UI.Services;

/// <summary>
/// Supplies a deterministic shell scope only for the explicitly enabled accessibility specimen.
/// </summary>
internal sealed class PartiesAccessibilitySpecimenUserContextAccessor(
    IUserContextAccessor authenticatedContext,
    NavigationManager navigation,
    IConfiguration configuration,
    IHostEnvironment environment) : IUserContextAccessor
{
    /// <inheritdoc />
    public string? TenantId => IsSpecimen ? "parties-accessibility-tenant" : authenticatedContext.TenantId;

    /// <inheritdoc />
    public string? UserId => IsSpecimen ? "parties-accessibility-user" : authenticatedContext.UserId;

    private bool IsSpecimen => PartiesAccessibilitySpecimenRoutes.IsEnabled(configuration, environment)
        && string.Equals(
            new Uri(navigation.Uri).AbsolutePath,
            PartiesAccessibilitySpecimenRoutes.ShellSpecimen,
            StringComparison.Ordinal);

    /// <summary>
    /// Wraps the selected authentication context without replacing its ordinary-route behavior.
    /// </summary>
    internal static void Register(IServiceCollection services)
    {
        ServiceDescriptor original = services.Last(descriptor => descriptor.ServiceType == typeof(IUserContextAccessor));
        services.AddScoped<IUserContextAccessor>(provider =>
        {
            IUserContextAccessor authenticatedContext = (IUserContextAccessor)(original.ImplementationInstance
                ?? original.ImplementationFactory?.Invoke(provider)
                ?? ActivatorUtilities.CreateInstance(provider, original.ImplementationType!));
            return new PartiesAccessibilitySpecimenUserContextAccessor(
                authenticatedContext,
                provider.GetRequiredService<NavigationManager>(),
                provider.GetRequiredService<IConfiguration>(),
                provider.GetRequiredService<IHostEnvironment>());
        });
    }
}
