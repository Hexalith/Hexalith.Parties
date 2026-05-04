namespace Hexalith.Parties.CommandApi.Authorization;

public enum TenantAccessDenialReason {
    None,
    MissingTenantId,
    MissingUserId,
    UnknownTenant,
    DisabledTenant,
    MissingMember,
    InsufficientRole,
}
