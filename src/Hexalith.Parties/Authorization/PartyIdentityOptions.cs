using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Parties.Authorization;

/// <summary>Required attribution policy configuration; unset settings deny binding writes, binding reads and live qualification.</summary>
public sealed class PartyIdentityOptions
{
    /// <summary>Gets or sets the approved versioned retention policy.</summary>
    public string? PolicyId { get; set; }

    /// <summary>Gets or sets the explicitly approved finite duration.</summary>
    public TimeSpan? Retention { get; set; }

    /// <summary>Gets or sets the explicitly approved supported expiry trigger.</summary>
    public string? ExpiryTrigger { get; set; }

    /// <summary>Gets a valid supported policy, or null when required configuration is missing.</summary>
    public IdentityHistoryPolicy? Policy
    {
        get
        {
            var policy = new IdentityHistoryPolicy(PolicyId ?? string.Empty, Retention ?? TimeSpan.Zero, ExpiryTrigger ?? string.Empty);
            return policy.IsValid ? policy : null;
        }
    }
}
