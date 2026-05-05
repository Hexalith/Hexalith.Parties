using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.Parties.CommandApi.Authorization;

internal static class TenantAccessDenialTranslator {
    internal const string PayloadTenantConflictReasonCode = "payload-tenant-conflict";

    internal static string ToReasonCode(TenantAccessDenialReason reason)
        => reason switch {
            TenantAccessDenialReason.MissingTenantId => "missing-tenant",
            TenantAccessDenialReason.MissingUserId => "missing-user",
            TenantAccessDenialReason.UnknownTenant => "unknown-tenant",
            TenantAccessDenialReason.DisabledTenant => "tenant-disabled",
            TenantAccessDenialReason.MissingMember => "not-member",
            TenantAccessDenialReason.InsufficientRole => "insufficient-role",
            TenantAccessDenialReason.TenantStateStale => "tenant-state-stale",
            TenantAccessDenialReason.None => throw new InvalidOperationException(
                "TenantAccessDenialReason.None has no client-facing reason code; the decision was Allowed."),
            _ => throw new InvalidOperationException(
                $"Unmapped TenantAccessDenialReason '{reason}'. Add it to ToReasonCode/ToHttpStatus/SafeDetail."),
        };

    internal static int ToHttpStatus(TenantAccessDenialReason reason)
        => reason switch {
            TenantAccessDenialReason.MissingTenantId or TenantAccessDenialReason.MissingUserId
                => StatusCodes.Status401Unauthorized,
            TenantAccessDenialReason.UnknownTenant
                or TenantAccessDenialReason.DisabledTenant
                or TenantAccessDenialReason.MissingMember
                or TenantAccessDenialReason.InsufficientRole
                or TenantAccessDenialReason.TenantStateStale
                => StatusCodes.Status403Forbidden,
            TenantAccessDenialReason.None => throw new InvalidOperationException(
                "TenantAccessDenialReason.None has no HTTP status; the decision was Allowed."),
            _ => throw new InvalidOperationException(
                $"Unmapped TenantAccessDenialReason '{reason}'. Add it to ToReasonCode/ToHttpStatus/SafeDetail."),
        };

    internal static ObjectResult ToProblemDetails(
        TenantAccessDecision decision,
        PathString path,
        string correlationId) {
        string reasonCode = ToReasonCode(decision.Reason);
        int statusCode = ToHttpStatus(decision.Reason);

        var problem = new ProblemDetails {
            Status = statusCode,
            Title = statusCode == StatusCodes.Status401Unauthorized
                ? "Authentication context missing"
                : "Tenant access denied",
            Detail = decision.DiagnosticText ?? SafeDetail(decision.Reason),
            Type = $"urn:hexalith:parties:authorization:{reasonCode}",
            Instance = path,
        };
        problem.Extensions["correlationId"] = correlationId;
        problem.Extensions["reasonCode"] = reasonCode;

        var result = new ObjectResult(problem) { StatusCode = statusCode };
        result.ContentTypes.Add("application/problem+json");
        return result;
    }

    internal static ObjectResult ToPayloadTenantConflictProblemDetails(
        PathString path,
        string correlationId) {
        var problem = new ProblemDetails {
            Status = StatusCodes.Status400BadRequest,
            Title = "Payload tenant conflict",
            Detail = "The tenant identifier in the request body does not match the trusted tenant context. Payload tenant values cannot override the authenticated tenant.",
            Type = $"urn:hexalith:parties:authorization:{PayloadTenantConflictReasonCode}",
            Instance = path,
        };
        problem.Extensions["correlationId"] = correlationId;
        problem.Extensions["reasonCode"] = PayloadTenantConflictReasonCode;

        var result = new ObjectResult(problem) { StatusCode = StatusCodes.Status400BadRequest };
        result.ContentTypes.Add("application/problem+json");
        return result;
    }

    internal static string ToMcpMessage(TenantAccessDecision decision) {
        string reasonCode = ToReasonCode(decision.Reason);
        return $"Tenant authorization failed ({reasonCode}): {decision.DiagnosticText ?? SafeDetail(decision.Reason)}";
    }

    private static string SafeDetail(TenantAccessDenialReason reason)
        => reason switch {
            TenantAccessDenialReason.MissingTenantId => "A trusted tenant context is required.",
            TenantAccessDenialReason.MissingUserId => "An authenticated user context is required.",
            TenantAccessDenialReason.UnknownTenant => "The tenant is not available in the local tenant access state.",
            TenantAccessDenialReason.DisabledTenant => "The tenant is not active.",
            TenantAccessDenialReason.MissingMember => "The authenticated user is not an active member of the tenant.",
            TenantAccessDenialReason.InsufficientRole => "The authenticated user does not have the required tenant role.",
            TenantAccessDenialReason.TenantStateStale => "Tenant access state is unavailable or not current enough to authorize the request.",
            TenantAccessDenialReason.None => throw new InvalidOperationException(
                "TenantAccessDenialReason.None has no detail; the decision was Allowed."),
            _ => throw new InvalidOperationException(
                $"Unmapped TenantAccessDenialReason '{reason}'. Add it to ToReasonCode/ToHttpStatus/SafeDetail."),
        };
}
