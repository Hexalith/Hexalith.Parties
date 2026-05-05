using System.Reflection;
using System.Security.Claims;

namespace Hexalith.Parties.CommandApi.Authorization;

internal static class PartiesAuthClaims {
    internal const string TenantClaimType = "eventstore:tenant";

    private const string SubClaimType = "sub";

    private const string ObjectIdClaimType = "oid";

    // Strict ordered claim list: OIDC `sub` is the canonical stable subject identifier;
    // Microsoft AAD `oid` is the standard normalized fallback. `User.Identity.Name` is
    // intentionally NOT a fallback because it is mapped to whichever claim is configured
    // as NameClaimType (typically `name`/`preferred_username`/`upn`) which are display
    // strings, not stable subject identifiers — using them as the Tenants membership key
    // would silently mismatch keys that are stored as `sub`.
    internal static string? ExtractUserId(ClaimsPrincipal principal) {
        ArgumentNullException.ThrowIfNull(principal);

        string? userId = principal.FindFirst(SubClaimType)?.Value
            ?? principal.FindFirst(ObjectIdClaimType)?.Value;

        return string.IsNullOrWhiteSpace(userId) ? null : userId;
    }

    internal static string? ExtractTenant(ClaimsPrincipal principal) {
        ArgumentNullException.ThrowIfNull(principal);

        return principal.FindAll(TenantClaimType)
            .Select(c => c.Value)
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
    }

    // AC5: payload tenant values are non-authoritative; a conflicting payload
    // TenantId must be rejected before any side-effecting work. Several command
    // DTOs carry a TenantId field (RecordConsent, EraseParty, RestrictProcessing,
    // etc.); detection is by reflection so we don't need to enumerate them and
    // future commands that add a TenantId field are protected automatically.
    internal static bool HasConflictingPayloadTenant<TCommand>(TCommand command, string trustedTenantId) {
        if (command is null) {
            return false;
        }

        PropertyInfo? property = command.GetType().GetProperty(
            "TenantId",
            BindingFlags.Instance | BindingFlags.Public);
        if (property is null || property.PropertyType != typeof(string)) {
            return false;
        }

        if (property.GetValue(command) is not string payloadTenant) {
            return false;
        }

        if (string.IsNullOrWhiteSpace(payloadTenant)) {
            return false;
        }

        return !string.Equals(payloadTenant, trustedTenantId, StringComparison.Ordinal);
    }
}
