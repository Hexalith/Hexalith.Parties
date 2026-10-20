using System.ComponentModel;

using Hexalith.McpCli.Abstractions;

namespace Hexalith.Parties.Contracts.Queries;

/// <summary>Read an existing party's detail in the envelope tenant. Missing and erased parties return the same safe not-found result; inactive parties remain visible.</summary>
[HexalithQuery("Read an existing party's detail in the envelope tenant. Missing and erased parties return the same safe not-found result; inactive parties remain visible.",
    Domain = "party", WireType = "PartyDetail", AggregateIdProperty = nameof(PartyId),
    ProjectionType = "party-detail", ProjectionActorType = "PartyDetailProjectionQueryActor",
    Example = """{"PartyId":"party-49"}""")]
public sealed record GetParty
{
    /// <summary>The supplied PartyId, at most 128 characters; tenant comes from the envelope. Use ASCII letters or digits plus '.', '_' or '-'; start and end with an ASCII letter or digit. McpCli refuses legacy braced, parenthesized and X-format GUID IDs before submission, although Parties accepts them.</summary>
    [Description("The supplied PartyId, at most 128 characters; tenant comes from the envelope. Use ASCII letters or digits plus '.', '_' or '-'; start and end with an ASCII letter or digit. McpCli refuses legacy braced, parenthesized and X-format GUID IDs before submission, although Parties accepts them.")]
    [HexalithIdentifier]
    public required string PartyId { get; init; }
}
