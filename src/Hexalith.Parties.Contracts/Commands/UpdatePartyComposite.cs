using System.ComponentModel;

using Hexalith.McpCli.Abstractions;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Contracts.Commands;

/// <summary>Atomically replace details for the existing party type; add, update or remove contact channels; add or remove business identifiers. Party type cannot change. Supply at least one supported change; activation is separate. Raw strings are limited to 512 UTF-16 units each and 4096 total, including whitespace and repeated IDs. PartyId, ContactChannelId, IdentifierId and removal ID entries have a tighter 128-character limit; business identifier Value strings use the 512 limit. Null collections or items reject; omitted lists default to empty. Sub-operation count is each non-null PersonDetails or OrganizationDetails object plus every item across the five operation lists; the configured cap defaults to 100.</summary>
[HexalithCommand("Atomically replace details for the existing party type; add, update or remove contact channels; add or remove business identifiers. Party type cannot change. Supply at least one supported change; activation is separate. Raw strings are limited to 512 UTF-16 units each and 4096 total, including whitespace and repeated IDs. PartyId, ContactChannelId, IdentifierId and removal ID entries have a tighter 128-character limit; business identifier Value strings use the 512 limit. Null collections or items reject; omitted lists default to empty. Sub-operation count is each non-null PersonDetails or OrganizationDetails object plus every item across the five operation lists; the configured cap defaults to 100.",
    Name = "update-party",
    Domain = "party", AggregateIdProperty = nameof(PartyId),
    Example = """{"PartyId":"party-49","PersonDetails":{"FirstName":"Augusta Ada","LastName":"Lovelace"}}""")]
public sealed record UpdatePartyComposite
{
    /// <summary>The supplied PartyId, at most 128 characters, shared by the root and every child operation; tenant comes from the gateway envelope. Use ASCII letters or digits plus '.', '_' or '-'; start and end with an ASCII letter or digit. McpCli refuses legacy braced, parenthesized and X-format GUID PartyId values before Gateway submission, although Parties accepts them.</summary>
    [Description("The supplied PartyId, at most 128 characters, shared by the root and every child operation; tenant comes from the gateway envelope. Use ASCII letters or digits plus '.', '_' or '-'; start and end with an ASCII letter or digit. McpCli refuses legacy braced, parenthesized and X-format GUID PartyId values before Gateway submission, although Parties accepts them.")]
    [HexalithIdentifier]
    public required string PartyId { get; init; }

    /// <summary>Complete replacement for an existing Person party only. Omitted or null leaves details unchanged; supplying this for another party type rejects the whole update. Party type cannot change.</summary>
    [Description("Complete replacement for an existing Person party only. Omitted or null leaves details unchanged; supplying this for another party type rejects the whole update. Party type cannot change.")]
    public PersonDetails? PersonDetails { get; init; }

    /// <summary>Complete replacement for an existing Organization party only. Omitted or null leaves details unchanged; supplying this for another party type rejects the whole update. Party type cannot change.</summary>
    [Description("Complete replacement for an existing Organization party only. Omitted or null leaves details unchanged; supplying this for another party type rejects the whole update. Party type cannot change.")]
    public OrganizationDetails? OrganizationDetails { get; init; }

    /// <summary>Contact channels to add with explicit child IDs and matching PartyId. An existing exact ID is skipped even when also listed in UpdateContactChannels; that update still applies.</summary>
    [Description("Contact channels to add with explicit child IDs and matching PartyId. An existing exact ID is skipped even when also listed in UpdateContactChannels; that update still applies.")]
    public IReadOnlyList<AddContactChannel> AddContactChannels { get; init; } = [];

    /// <summary>Existing contact channels to change; each entry must supply at least one non-null Type, Value or IsPreferred. False is a supported preference change; omitted or null members preserve existing values.</summary>
    [Description("Existing contact channels to change; each entry must supply at least one non-null Type, Value or IsPreferred. False is a supported preference change; omitted or null members preserve existing values.")]
    public IReadOnlyList<UpdateContactChannel> UpdateContactChannels { get; init; } = [];

    /// <summary>Exact existing ContactChannelId values to remove, at most 128 characters each; case is significant. An ID also in AddContactChannels or UpdateContactChannels rejects the whole update. Null entries reject.</summary>
    [Description("Exact existing ContactChannelId values to remove, at most 128 characters each; case is significant. An ID also in AddContactChannels or UpdateContactChannels rejects the whole update. Null entries reject.")]
    public IReadOnlyList<string> RemoveContactChannelIds { get; init; } = [];

    /// <summary>Business identifiers to add in this atomic update, with explicit child IDs and matching PartyId.</summary>
    [Description("Business identifiers to add in this atomic update, with explicit child IDs and matching PartyId.")]
    public IReadOnlyList<AddIdentifier> AddIdentifiers { get; init; } = [];

    /// <summary>Exact existing IdentifierId values to remove, at most 128 characters each; case is significant. An ID also in AddIdentifiers rejects the whole update. Null entries reject.</summary>
    [Description("Exact existing IdentifierId values to remove, at most 128 characters each; case is significant. An ID also in AddIdentifiers rejects the whole update. Null entries reject.")]
    public IReadOnlyList<string> RemoveIdentifierIds { get; init; } = [];
}
