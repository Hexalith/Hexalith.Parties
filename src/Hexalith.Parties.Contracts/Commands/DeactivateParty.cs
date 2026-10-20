using System.ComponentModel;

using Hexalith.McpCli.Abstractions;

namespace Hexalith.Parties.Contracts.Commands;

/// <summary>Soft-deactivate an existing party without erasing personal data. Processing must be unrestricted and the current erasure status must be Active. An already inactive party is unchanged only after these checks.</summary>
[HexalithCommand("Soft-deactivate an existing party without erasing personal data. Processing must be unrestricted and the current erasure status must be Active. An already inactive party is unchanged only after these checks.",
    Domain = "party", AggregateIdProperty = nameof(PartyId),
    Example = """{"PartyId":"party-49"}""")]
public sealed record DeactivateParty
{
    /// <summary>The supplied PartyId, at most 128 characters, shared by the root and every child operation; tenant comes from the gateway envelope. For new IDs use ASCII letters or digits plus '.', '_' or '-'; start and end with an ASCII letter or digit. Parties accepts legacy braced, parenthesized and X-format GUID IDs, but their McpCli/Gateway reachability remains unproven.</summary>
    [Description("The supplied PartyId, at most 128 characters, shared by the root and every child operation; tenant comes from the gateway envelope. For new IDs use ASCII letters or digits plus '.', '_' or '-'; start and end with an ASCII letter or digit. Parties accepts legacy braced, parenthesized and X-format GUID IDs, but their McpCli/Gateway reachability remains unproven.")]
    [HexalithIdentifier]
    public required string PartyId { get; init; }
}
