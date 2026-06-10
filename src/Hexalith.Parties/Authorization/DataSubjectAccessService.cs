namespace Hexalith.Parties.Authorization;

public sealed class DataSubjectAccessService : IDataSubjectAccessService {
    public DataSubjectAccessDecision CheckSelfAccess(string? boundPartyId, string? aggregateId) {
        if (string.IsNullOrWhiteSpace(boundPartyId)) {
            return DataSubjectAccessDecision.Denied(DataSubjectAccessDenialReason.MissingPartyBinding);
        }

        if (string.IsNullOrWhiteSpace(aggregateId)) {
            return DataSubjectAccessDecision.Denied(DataSubjectAccessDenialReason.MissingAggregateId);
        }

        // Ordinal — ids are opaque tokens, never culture-aware.
        return string.Equals(boundPartyId, aggregateId, StringComparison.Ordinal)
            ? DataSubjectAccessDecision.Allowed
            : DataSubjectAccessDecision.Denied(DataSubjectAccessDenialReason.AggregateMismatch);
    }
}
