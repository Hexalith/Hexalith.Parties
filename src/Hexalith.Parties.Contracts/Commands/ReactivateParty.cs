using System.ComponentModel;

using Hexalith.McpCli.Abstractions;

namespace Hexalith.Parties.Contracts.Commands;

/// <summary>Reactivate an existing inactive party. Processing must be unrestricted and the current erasure status must be Active. An already active party is unchanged only after these checks.</summary>
[HexalithCommand("Reactivate an existing inactive party. Processing must be unrestricted and the current erasure status must be Active. An already active party is unchanged only after these checks.",
    Domain = "party", AggregateIdProperty = nameof(PartyId),
    Example = """{"PartyId":"party-49"}""")]
public sealed record ReactivateParty
{
    /// <summary>The supplied PartyId, at most 128 characters of the party to reactivate; tenant comes from the gateway envelope. Use ASCII letters or digits plus '.', '_' or '-'; start and end with an ASCII letter or digit. McpCli refuses legacy braced, parenthesized and X-format GUID PartyId values before Gateway submission, although Parties accepts them.</summary>
    [Description("The supplied PartyId, at most 128 characters of the party to reactivate; tenant comes from the gateway envelope. Use ASCII letters or digits plus '.', '_' or '-'; start and end with an ASCII letter or digit. McpCli refuses legacy braced, parenthesized and X-format GUID PartyId values before Gateway submission, although Parties accepts them.")]
    [HexalithIdentifier]
    public required string PartyId { get; init; }
}
