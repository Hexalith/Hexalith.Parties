namespace Hexalith.Parties.Contracts.Queries;

/// <summary>Requests current eligibility for an exact Party and bound ChatBot requester.</summary>
public sealed record ResolveChatBotPartyAuthority(
    string TenantId, string PrincipalId, string PartyId, string Operation, string ActorClass, string Origin)
{
    public static string QueryType => typeof(ResolveChatBotPartyAuthority).FullName!;
}

/// <summary>Metadata-only current Party source evidence; no profile data is returned.</summary>
public sealed record ChatBotPartyAuthorityResult(
    string TenantId, string PrincipalId, string PartyId, string Operation, string ActorClass, string Origin,
    bool Allowed, long SourceVersion, string ObservationId, DateTimeOffset ObservedAt);
