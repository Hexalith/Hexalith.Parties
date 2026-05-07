namespace Hexalith.Parties.AdminPortal.Services;

public sealed record AdminPortalRichSearchCapability(bool IsAvailable, bool IsDegraded, string? Reason)
{
    public static AdminPortalRichSearchCapability Available()
        => new(IsAvailable: true, IsDegraded: false, Reason: null);

    public static AdminPortalRichSearchCapability LocalOnly(string? reason = null)
        => new(IsAvailable: false, IsDegraded: false, Reason: BoundReason(reason));

    public static AdminPortalRichSearchCapability Degraded(string? reason = null)
        => new(IsAvailable: false, IsDegraded: true, Reason: BoundReason(reason));

    private static string? BoundReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return null;
        }

        string bounded = new(reason
            .Trim()
            .Where(static c => !char.IsControl(c))
            .Take(128)
            .ToArray());

        return string.IsNullOrWhiteSpace(bounded) ? null : bounded;
    }
}
