namespace Hexalith.Parties.Authorization;

public interface IDataSubjectAccessService {
    DataSubjectAccessDecision CheckSelfAccess(string? boundPartyId, string? aggregateId);
}
