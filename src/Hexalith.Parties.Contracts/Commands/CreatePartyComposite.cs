using System.ComponentModel;
using System.Text.Json.Serialization;

using Hexalith.McpCli.Abstractions;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Contracts.Commands;

/// <summary>Create a person or organization atomically with explicit IDs, typed details, optional contact channels and business identifiers. No IDs are generated. Raw strings are limited to 512 UTF-16 units each and 4096 total, including whitespace and repeated IDs. PartyId, ContactChannelId, IdentifierId and removal ID entries have a tighter 128-character limit; business identifier Value strings use the 512 limit. Null collections or items reject; omitted lists default to empty. Sub-operation count is 1 plus every ContactChannels and Identifiers item; the configured cap defaults to 100.</summary>
[HexalithCommand("Create a person or organization atomically with explicit IDs, typed details, optional contact channels and business identifiers. No IDs are generated. Raw strings are limited to 512 UTF-16 units each and 4096 total, including whitespace and repeated IDs. PartyId, ContactChannelId, IdentifierId and removal ID entries have a tighter 128-character limit; business identifier Value strings use the 512 limit. Null collections or items reject; omitted lists default to empty. Sub-operation count is 1 plus every ContactChannels and Identifiers item; the configured cap defaults to 100.",
    Name = "create-party",
    Domain = "party", AggregateIdProperty = nameof(PartyId),
    Example = """{"PartyId":"party-49","Type":"Person","PersonDetails":{"FirstName":"Ada","LastName":"Lovelace"}}""")]
public sealed record CreatePartyComposite
{
    /// <summary>The supplied PartyId, at most 128 characters, shared by the root and every child operation; tenant comes from the gateway envelope. For new IDs use ASCII letters or digits plus '.', '_' or '-'; start and end with an ASCII letter or digit. Parties accepts legacy braced, parenthesized and X-format GUID IDs, but their McpCli/Gateway reachability remains unproven.</summary>
    [Description("The supplied PartyId, at most 128 characters, shared by the root and every child operation; tenant comes from the gateway envelope. For new IDs use ASCII letters or digits plus '.', '_' or '-'; start and end with an ASCII letter or digit. Parties accepts legacy braced, parenthesized and X-format GUID IDs, but their McpCli/Gateway reachability remains unproven.")]
    [HexalithIdentifier]
    public required string PartyId { get; init; }

    /// <summary>The explicit party kind: Person or Organization; Unknown is rejected.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<PartyType>))]
    [Description("The explicit party kind: Person or Organization; Unknown is rejected.")]
    public required PartyType Type { get; init; }

    /// <summary>Complete person details, required when Type is Person; optional otherwise and stored if supplied. These details do not override the declared Type.</summary>
    [Description("Complete person details, required when Type is Person; optional otherwise and stored if supplied. These details do not override the declared Type.")]
    public PersonDetails? PersonDetails { get; init; }

    /// <summary>Complete organization details, required when Type is Organization; optional otherwise and stored if supplied. These details do not override the declared Type.</summary>
    [Description("Complete organization details, required when Type is Organization; optional otherwise and stored if supplied. These details do not override the declared Type.")]
    public OrganizationDetails? OrganizationDetails { get; init; }

    /// <summary>Contact channels to add atomically with the party; supply each child ID and the same PartyId as the root.</summary>
    [Description("Contact channels to add atomically with the party; supply each child ID and the same PartyId as the root.")]
    public IReadOnlyList<AddContactChannel> ContactChannels { get; init; } = [];

    /// <summary>Business identifiers to add atomically with the party; supply each child ID and the same PartyId as the root.</summary>
    [Description("Business identifiers to add atomically with the party; supply each child ID and the same PartyId as the root.")]
    public IReadOnlyList<AddIdentifier> Identifiers { get; init; } = [];
}
