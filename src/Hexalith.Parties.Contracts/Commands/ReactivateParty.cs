using System.ComponentModel;

using Hexalith.McpCli.Abstractions;

namespace Hexalith.Parties.Contracts.Commands;

/// <summary>Reactivate an existing inactive party. Processing must be unrestricted and the current erasure status must be Active. An already active party is unchanged only after these checks.</summary>
[HexalithCommand("Reactivate an existing inactive party. Processing must be unrestricted and the current erasure status must be Active. An already active party is unchanged only after these checks.",
    Domain = "party", AggregateIdProperty = nameof(PartyId),
    Example = """{"PartyId":"party-49"}""")]
public sealed record ReactivateParty
{
    /// <summary>The supplied PartyId, at most 128 characters, shared by the root and every child operation; tenant comes from the gateway envelope.</summary>
    [Description("The supplied PartyId, at most 128 characters, shared by the root and every child operation; tenant comes from the gateway envelope.")]
    [HexalithIdentifier]
    public required string PartyId { get; init; }
}
