using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.UI.Components.Shared;

/// <summary>Maps Parties freshness and copy onto an explicitly registered shared candidate.</summary>
public partial class DataFreshnessIndicator
{
    // Unsupported dependency selections omit the shared capability branch. Keep the
    // fallback compilation valid without changing component state or service access.
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "The shared-capability compilation reads component state.")]
    private Type? SharedFreshnessType
    {
        get
        {
#if HFC_SHARED_UI
            if (Services.GetService(typeof(Hexalith.FrontComposer.Contracts.Downloads.IFcBrowserDownloadService)) is not null)
            {
                return typeof(Hexalith.FrontComposer.Shell.Components.Feedback.FcFreshnessIndicator);
            }
#endif
            return null;
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "The shared-capability compilation reads component state.")]
    private IDictionary<string, object> SharedFreshnessParameters
    {
        get
        {
#if HFC_SHARED_UI
            var status = FreshnessStatus switch
            {
                ProjectionFreshnessStatus.Current => Hexalith.FrontComposer.Contracts.Freshness.FcFreshnessStatus.Current,
                ProjectionFreshnessStatus.Stale => Hexalith.FrontComposer.Contracts.Freshness.FcFreshnessStatus.Stale,
                _ => Hexalith.FrontComposer.Contracts.Freshness.FcFreshnessStatus.Degraded,
            };
            return new Dictionary<string, object>
            {
                ["Freshness"] = new Hexalith.FrontComposer.Contracts.Freshness.FcRecordFreshness(status, AsOf),
                ["CurrentLabel"] = "Up to date",
                ["StaleLabel"] = StaleMessage,
                ["DegradedLabel"] = "Showing last known",
            };
#else
            return new Dictionary<string, object>();
#endif
        }
    }

}
