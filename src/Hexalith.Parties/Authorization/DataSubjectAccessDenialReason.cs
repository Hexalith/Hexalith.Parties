namespace Hexalith.Parties.Authorization;

public enum DataSubjectAccessDenialReason {
    None,
    MissingPartyBinding,
    MissingAggregateId,
    AggregateMismatch,
}
