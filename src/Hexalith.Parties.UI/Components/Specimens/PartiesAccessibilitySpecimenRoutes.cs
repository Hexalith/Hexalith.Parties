using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Hexalith.Parties.UI.Components.Specimens;

/// <summary>
/// Route contract for Parties UI browser-level accessibility and visual specimens.
/// </summary>
public static class PartiesAccessibilitySpecimenRoutes
{
    /// <summary>Configuration key that explicitly enables accessibility specimen routes.</summary>
    public const string EnabledConfigurationKey = "Hexalith:Parties:AccessibilitySpecimens:Enabled";

    /// <summary>Route for the deterministic shell accessibility specimen.</summary>
    public const string ShellSpecimen = "/__parties/specimens/accessibility";

    /// <summary>
    /// Returns whether the host may expose specimen content.
    /// </summary>
    public static bool IsEnabled(IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        return string.Equals(configuration[EnabledConfigurationKey], "true", StringComparison.OrdinalIgnoreCase)
            && (environment.IsDevelopment() || environment.IsEnvironment("Test"));
    }
}
