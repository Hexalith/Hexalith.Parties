using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.UI.Status;

namespace Hexalith.Parties.UI.Services;

/// <summary>
/// The delegate-driven request the call site hands <see cref="OptimisticReconcile.ExecuteAsync"/>. Carrying
/// only delegates keeps the primitive slice-agnostic, so every screen reuses it verbatim.
/// </summary>
public sealed record OptimisticReconcileRequest
{
    /// <summary>Dispatches/applies the optimistic slice state (caller's job).</summary>
    public required Action ApplyOptimistic { get; init; }

    /// <summary>Issues the command via the self-scoped / tenant-scoped client; returns acceptance.</summary>
    public required Func<CancellationToken, Task<CommandAcceptance>> IssueCommand { get; init; }

    /// <summary>The caller's <strong>idempotent</strong> re-read; returns the freshness so the primitive knows when <c>Current</c>.</summary>
    public required Func<CancellationToken, Task<ProjectionFreshnessStatus>> Reconcile { get; init; }

    /// <summary>Gets the operation-specific witness evaluated after each Current read.</summary>
    /// <remarks>Capture the accepted command/checkpoint and inspect the model just read. Missing or false witnesses keep the operation pending until cancellation; Current and unrelated signals alone never complete it.</remarks>
    public Func<CancellationToken, Task<bool>>? OperationWitness { get; init; }

    /// <summary>Reverts the optimistic state (caller's job) on rejection.</summary>
    public required Action Revert { get; init; }

    /// <summary>The <c>(projectionType, tenant)</c> key to await a SignalR projection-confirm on.</summary>
    public required (string ProjectionType, string Tenant) ProjectionKey { get; init; }

    /// <summary>
    /// Announces a <see cref="StatusKind"/> + non-PII reason; the caller renders it via
    /// <c>StatusLiveRegion</c> (the primitive never touches the DOM/focus). Politeness follows the kind via
    /// <c>StatusPresentation.PolitenessFor</c> — never a second mapping.
    /// </summary>
    public required Action<StatusKind, string?> Announce { get; init; }
}
