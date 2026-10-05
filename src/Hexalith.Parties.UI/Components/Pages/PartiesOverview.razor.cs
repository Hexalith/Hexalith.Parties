namespace Hexalith.Parties.UI.Components.Pages;

/// <summary>
/// Parties overview page: the module landing tabs at <c>/parties</c> and <c>/parties/overview</c>.
/// </summary>
public partial class PartiesOverview
{
    /// <summary>
    /// Gets the source-only <c>FcPageTabs</c> parameters passed through <c>@attributes</c>.
    /// </summary>
    /// <remarks>
    /// Packaged <c>Hexalith.FrontComposer.Shell</c> 4.5.0 (the CI, bUnit, and released-container
    /// graph) has neither <c>FcPageTabs.ModuleRoute</c> nor <c>FcPageTabs.DefaultTabId</c>, and
    /// rendering an unknown component parameter throws. The parameters are therefore supplied only
    /// when <c>HFC_ROUTE_OPTIONS</c> is defined (FrontComposer source mode), matching the route-option
    /// gate in <c>Program.cs</c>; in package mode the dictionary is empty. DW-139 removes this gate
    /// once a FrontComposer Shell release carries both parameters.
    /// </remarks>
    private static IReadOnlyDictionary<string, object> ModuleRouteTabAttributes { get; } =
        new Dictionary<string, object>(StringComparer.Ordinal)
        {
#if HFC_ROUTE_OPTIONS
            [nameof(Hexalith.FrontComposer.Shell.Components.Layout.FcPageTabs.ModuleRoute)] = "/parties",
            [nameof(Hexalith.FrontComposer.Shell.Components.Layout.FcPageTabs.DefaultTabId)] = "overview",
#endif
        };
}
