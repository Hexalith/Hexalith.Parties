using System.ComponentModel;
using System.Text.Json.Serialization;

using Hexalith.McpCli.Abstractions;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Contracts.Commands;

/// <summary>Supplied add contact channel for a typed party command.</summary>
public sealed record AddContactChannel
{
    /// <summary>The supplied PartyId, at most 128 characters, shared by the root and every child operation; tenant comes from the gateway envelope. Use ASCII letters or digits plus '.', '_' or '-'; start and end with an ASCII letter or digit. McpCli refuses legacy braced, parenthesized and X-format GUID PartyId values before Gateway submission, although Parties accepts them.</summary>
    [Description("The supplied PartyId, at most 128 characters, shared by the root and every child operation; tenant comes from the gateway envelope. Use ASCII letters or digits plus '.', '_' or '-'; start and end with an ASCII letter or digit. McpCli refuses legacy braced, parenthesized and X-format GUID PartyId values before Gateway submission, although Parties accepts them.")]
    [HexalithIdentifier]
    public required string PartyId { get; init; }

    /// <summary>The supplied ContactChannelId, at most 128 characters; additions must be unique ignoring case, while updates address exact case. For new IDs use ASCII letters or digits plus '.', '_' or '-'; start and end with an ASCII letter or digit. Parties accepts legacy braced, parenthesized and X-format GUID IDs, but their McpCli/Gateway reachability remains unproven.</summary>
    [Description("The supplied ContactChannelId, at most 128 characters; additions must be unique ignoring case, while updates address exact case. For new IDs use ASCII letters or digits plus '.', '_' or '-'; start and end with an ASCII letter or digit. Parties accepts legacy braced, parenthesized and X-format GUID IDs, but their McpCli/Gateway reachability remains unproven.")]
    [HexalithIdentifier]
    public required string ContactChannelId { get; init; }

    /// <summary>The contact-channel kind: Email, Phone, PostalAddress or SocialMedia.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<ContactChannelType>))]
    [Description("The contact-channel kind: Email, Phone, PostalAddress or SocialMedia.")]
    public required ContactChannelType Type { get; init; }

    /// <summary>A raw string with at least one non-whitespace character for every kind, including PostalAddress and SocialMedia; no structured address or social object format is defined. Stored without trimming.</summary>
    [PersonalData]
    [Description("A raw string with at least one non-whitespace character for every kind, including PostalAddress and SocialMedia; no structured address or social object format is defined. Stored without trimming.")]
    public required string Value { get; init; }

    /// <summary>Defaults to false. True makes this channel preferred and clears preference on other channels of the same type; the last true addition for that type wins.</summary>
    [Description("Defaults to false. True makes this channel preferred and clears preference on other channels of the same type; the last true addition for that type wins.")]
    public bool IsPreferred { get; init; }
}
