using System.ComponentModel;

namespace Hexalith.Parties.Contracts.ValueObjects;

/// <summary>Supplied person details for a typed party command.</summary>
public sealed record PersonDetails
{
    /// <summary>The person's supplied first name.</summary>
    [PersonalData]
    [Description("The person's supplied first name.")]
    public required string FirstName { get; init; }

    /// <summary>The person's supplied last name.</summary>
    [PersonalData]
    [Description("The person's supplied last name.")]
    public required string LastName { get; init; }

    /// <summary>The optional date of birth with an explicit UTC offset.</summary>
    [PersonalData]
    [Description("The optional date of birth with an explicit UTC offset.")]
    public DateTimeOffset? DateOfBirth { get; init; }

    /// <summary>The optional name prefix, such as a title.</summary>
    [PersonalData]
    [Description("The optional name prefix, such as a title.")]
    public string? Prefix { get; init; }

    /// <summary>The optional name suffix.</summary>
    [PersonalData]
    [Description("The optional name suffix.")]
    public string? Suffix { get; init; }
}
