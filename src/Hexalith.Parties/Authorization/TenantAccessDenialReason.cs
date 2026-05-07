namespace Hexalith.Parties.Authorization;

public enum TenantAccessDenialReason {
    None,
    MissingTenantId,
    MissingUserId,
    UnknownTenant,
    DisabledTenant,
    MissingMember,
    InsufficientRole,
    TenantStateStale,
}
