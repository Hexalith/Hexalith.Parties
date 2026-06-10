namespace Hexalith.Parties.UI.Services;

/// <summary>
/// Bound configuration for the Story 1.7 live-freshness mechanism (AR-D6). Both keys are
/// <c>__</c>-overridable (<c>EventStore__SignalR__HubUrl</c>, <c>Parties__Freshness__PollingIntervalSeconds</c>)
/// and ship with inert defaults so a test / degraded boot composes without connecting.
/// </summary>
public sealed class ProjectionFreshnessOptions
{
    /// <summary>
    /// Gets or sets the absolute URL of the EventStore <c>/hubs/projection-changes</c> SignalR hub.
    /// <strong>Empty / whitespace = inert</strong>: nothing connects and <see cref="IProjectionStream.IsConnected"/>
    /// stays <see langword="false"/> (driving the polling fallback). The AppHost injects the run-mode value;
    /// the committed <c>appsettings.json</c> default is empty (no secrets committed).
    /// </summary>
    public string? HubUrl { get; set; }

    /// <summary>
    /// Gets or sets the polling-fallback interval in seconds used while the stream is disconnected.
    /// Default <c>30</c> — mirrors the EventStore Admin UI <c>DashboardRefreshService</c> 30&#160;s loop.
    /// </summary>
    public int PollingIntervalSeconds { get; set; } = 30;
}
