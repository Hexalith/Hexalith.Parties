namespace Hexalith.Parties.Client;

public sealed record PartiesClientOptions
{
    /// <summary>
    /// Gets the EventStore gateway base URL used for Parties command and query submission.
    /// </summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>
    /// Gets the tenant identifier placed in EventStore command and query envelopes.
    /// </summary>
    public string Tenant { get; init; } = string.Empty;
}
