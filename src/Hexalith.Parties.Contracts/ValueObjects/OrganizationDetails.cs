using System.ComponentModel;

namespace Hexalith.Parties.Contracts.ValueObjects;

/// <summary>Supplied organization details for a typed party command.</summary>
public sealed record OrganizationDetails
{
    /// <summary>The organization's complete legal name.</summary>
    [Description("The organization's complete legal name.")]
    public required string LegalName { get; init; }

    /// <summary>The optional name the organization trades under.</summary>
    [Description("The optional name the organization trades under.")]
    public string? TradingName { get; init; }

    /// <summary>The optional legal form of the organization.</summary>
    [Description("The optional legal form of the organization.")]
    public string? LegalForm { get; init; }

    /// <summary>The optional organization registration number.</summary>
    [Description("The optional organization registration number.")]
    public string? RegistrationNumber { get; init; }

    /// <summary>Natural-person classification (an individual rather than a legal entity), default false. Create initializes the party classification. Detail replacement changes this nested value but does not change the established party classification.</summary>
    [Description("Natural-person classification (an individual rather than a legal entity), default false. Create initializes the party classification. Detail replacement changes this nested value but does not change the established party classification.")]
    public bool IsNaturalPerson { get; init; }
}
