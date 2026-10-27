using Hexalith.Parties.Client;
using Hexalith.Parties.UI.Status;

namespace Hexalith.Parties.UI.Services;

/// <summary>
/// Whether the command the call site issued was <strong>accepted</strong> (enters the reconcile path) or
/// <strong>rejected</strong> by a non-throwing "validation outcome" (e.g. a GDPR
/// <c>AdminPortalGdprCommandResult</c> whose <c>Outcome</c> is not <c>Accepted</c>) — which the primitive
/// treats exactly like a thrown rejection: revert + an assertive inline reason.
/// </summary>
/// <remarks>
/// This keeps <see cref="OptimisticReconcile"/> <strong>slice-agnostic</strong>: the call site maps its own
/// result type (a thrown <see cref="PartiesClientException"/>, or a returned domain result) into this small
/// primitive-owned shape, so the primitive never references a screen's command/GDPR result types.
/// </remarks>
public readonly record struct CommandAcceptance
{
    private CommandAcceptance(bool isAccepted, StatusKind failureStatus, string? failureReason)
    {
        IsAccepted = isAccepted;
        FailureStatus = failureStatus;
        FailureReason = failureReason;
    }

    /// <summary>Gets whether the command was accepted (200/202 / <c>Outcome == Accepted</c>).</summary>
    public bool IsAccepted { get; }

    /// <summary>Gets the canonical UI state for a non-throwing rejection (ignored when accepted).</summary>
    public StatusKind FailureStatus { get; }

    /// <summary>Gets the non-PII inline reason for a non-throwing rejection (ignored when accepted).</summary>
    public string? FailureReason { get; }

    /// <summary>An accepted command — proceed to reconcile.</summary>
    public static CommandAcceptance Accepted { get; } = new(true, StatusKind.AcceptedProcessing, null);

    /// <summary>A rejected "validation outcome" — revert + announce the mapped state assertively.</summary>
    public static CommandAcceptance Rejected(StatusKind failureStatus, string? failureReason)
        => new(false, failureStatus, failureReason);
}
