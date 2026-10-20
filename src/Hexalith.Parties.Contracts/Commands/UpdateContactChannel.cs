using System.ComponentModel;
using System.Text.Json.Serialization;

using Hexalith.McpCli.Abstractions;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Contracts.Commands;

/// <summary>Supplied update contact channel for a typed party command.</summary>
public sealed record UpdateContactChannel
{
    /// <summary>The supplied PartyId, at most 128 characters, shared by the root and every child operation; tenant comes from the gateway envelope. Use ASCII letters or digits plus '.', '_' or '-'; start and end with an ASCII letter or digit. McpCli refuses legacy braced, parenthesized and X-format GUID PartyId values before Gateway submission, although Parties accepts them.</summary>
    [Description("The supplied PartyId, at most 128 characters, shared by the root and every child operation; tenant comes from the gateway envelope. Use ASCII letters or digits plus '.', '_' or '-'; start and end with an ASCII letter or digit. McpCli refuses legacy braced, parenthesized and X-format GUID PartyId values before Gateway submission, although Parties accepts them.")]
    [HexalithIdentifier]
    public required string PartyId { get; init; }

    /// <summary>The supplied ContactChannelId, at most 128 characters; additions must be unique ignoring case, while updates address exact case. For new IDs use ASCII letters or digits plus '.', '_' or '-'; start and end with an ASCII letter or digit. Parties accepts legacy braced, parenthesized and X-format GUID IDs, but their McpCli/Gateway reachability remains unproven.</summary>
    [Description("The supplied ContactChannelId, at most 128 characters; additions must be unique ignoring case, while updates address exact case. For new IDs use ASCII letters or digits plus '.', '_' or '-'; start and end with an ASCII letter or digit. Parties accepts legacy braced, parenthesized and X-format GUID IDs, but their McpCli/Gateway reachability remains unproven.")]
    [HexalithIdentifier]
    public required string ContactChannelId { get; init; }

    /// <summary>The replacement kind: Email, Phone, PostalAddress or SocialMedia. Omitted or null preserves the kind. When moving a preferred channel to another kind, also supply IsPreferred=true to reconcile preferences.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<ContactChannelType>))]
    [Description("The replacement kind: Email, Phone, PostalAddress or SocialMedia. Omitted or null preserves the kind. When moving a preferred channel to another kind, also supply IsPreferred=true to reconcile preferences.")]
    public ContactChannelType? Type { get; init; }

    /// <summary>Omitted or null preserves the value; a supplied string replaces it verbatim, including an empty string to clear it. All kinds, including PostalAddress and SocialMedia, use raw strings without a defined structured format.</summary>
    [PersonalData]
    [Description("Omitted or null preserves the value; a supplied string replaces it verbatim, including an empty string to clear it. All kinds, including PostalAddress and SocialMedia, use raw strings without a defined structured format.")]
    public string? Value { get; init; }

    /// <summary>Omitted or null preserves preference; false clears only this channel. True selects this channel within its target type and clears other preferences when becoming preferred or changing type. Supply true with a type change to reconcile preferences.</summary>
    [Description("Omitted or null preserves preference; false clears only this channel. True selects this channel within its target type and clears other preferences when becoming preferred or changing type. Supply true with a type change to reconcile preferences.")]
    public bool? IsPreferred { get; init; }
}
