using Hexalith.Parties.Authorization;

namespace Hexalith.Parties.Mcp;

public sealed class McpTenantAuthorizationException : InvalidOperationException
{
    private McpTenantAuthorizationException(TenantAccessDenialReason reason, string reasonCode, string message)
        : base(message)
    {
        Reason = reason;
        ReasonCode = reasonCode;
    }

    public TenantAccessDenialReason Reason { get; }

    public string ReasonCode { get; }

    internal static McpTenantAuthorizationException FromDecision(TenantAccessDecision decision)
        => new(
            decision.Reason,
            TenantAccessDenialTranslator.ToReasonCode(decision.Reason),
            TenantAccessDenialTranslator.ToMcpMessage(decision));
}
