using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.CommandApi.Models;

/// <summary>
/// Internal DTO for deserializing PartyState from DAPR actor snapshot.
/// Required because PartyState uses private setters which System.Text.Json cannot populate.
/// Temporary mechanism until read-model projections are available (Epic 3).
/// </summary>
internal sealed record PartyStateSnapshot
{
    public PartyType Type { get; init; }

    public bool IsActive { get; init; }

    public bool IsNaturalPerson { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string SortName { get; init; } = string.Empty;

    public PersonDetails? Person { get; init; }

    public OrganizationDetails? Organization { get; init; }

    public IReadOnlyList<ContactChannel> ContactChannels { get; init; } = [];

    public IReadOnlyList<PartyIdentifier> Identifiers { get; init; } = [];
}
