using Hexalith.Parties.Client;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.UI.Status;

using Microsoft.Extensions.Logging;

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

/// <summary>
/// The single shared optimistic-then-reconcile orchestration (Story 1.7, AC1/AC3;
/// <c>architecture.md:479</c> "one shared effect pattern, not per-screen"; <c>656-659</c> command data
/// flow). Exactly one implementation, reused verbatim by every screen and by the generated <c>[Command]</c>
/// Fluxor lifecycle later — there is no per-screen re-implementation.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Flow.</strong> apply optimistic → announce polite "Saving…" (<see cref="StatusKind.AcceptedProcessing"/>,
/// no focus steal) → issue command. On acceptance: reconcile on the <em>first</em> of {a SignalR
/// projection-confirm, a re-read returning <see cref="ProjectionFreshnessStatus.Current"/>} (polling owns the
/// re-read while disconnected), announce the reconciled state politely, then tear the subscription / polling
/// down. A <strong>one-shot guard</strong> makes a duplicate or late confirm a no-op (AC3 "no duplicate
/// application").
/// </para>
/// <para>
/// <strong>Rejection / cancel.</strong> A non-accepted <see cref="CommandAcceptance"/> or a thrown
/// <see cref="PartiesClientException"/> → revert + an inline reason mapped through
/// <see cref="StatusPresentation"/> (the kind drives <c>role=alert</c>). A <strong>user-initiated
/// cancellation</strong> (the supplied token fired) is dropped silently — this is the call-site contract
/// Story 1.6 deferred (<see cref="StatusPresentation.FromException"/> only ever sees an <em>un-requested</em>
/// cancellation = a timeout → <see cref="StatusKind.TransientFailure"/>).
/// </para>
/// <para><strong>Lifetime.</strong> Scoped (per circuit — ADR-030).</para>
/// </remarks>
public sealed class OptimisticReconcile(
    PartiesProjectionSubscription subscription,
    ProjectionFreshnessFallback fallback,
    ILogger<OptimisticReconcile> logger)
{
    private const string SavingMessage = "Saving…";

    /// <summary>Runs the optimistic-then-reconcile flow for <paramref name="request"/>.</summary>
    public async Task ExecuteAsync(OptimisticReconcileRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        request.ApplyOptimistic();
        request.Announce(StatusKind.AcceptedProcessing, SavingMessage); // polite, no focus steal

        CommandAcceptance acceptance;
        try
        {
            acceptance = await request.IssueCommand(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // User-initiated cancellation (navigated away / component disposed) — abandoned. Drop silently:
            // no revert-announce, no UI failure state. (The Story 1.6 FromException user-cancel contract.)
            return;
        }
        catch (PartiesClientException pce)
        {
            request.Revert();
            request.Announce(StatusPresentation.FromClientException(pce), pce.Detail ?? pce.Title);
            return;
        }
        catch (Exception ex)
        {
            // An un-requested cancellation/timeout reaches here → TransientFailure (assertive).
            request.Revert();
            request.Announce(StatusPresentation.FromException(ex), ex.Message);
            return;
        }

        if (!acceptance.IsAccepted)
        {
            request.Revert();
            request.Announce(acceptance.FailureStatus, acceptance.FailureReason);
            return;
        }

        await ReconcileAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task ReconcileAsync(OptimisticReconcileRequest request, CancellationToken cancellationToken)
    {
        int reconciled = 0; // one-shot guard
        var confirmed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task TryReconcileAsync(CancellationToken ct)
        {
            if (Volatile.Read(ref reconciled) == 1)
            {
                return; // already reconciled — a duplicate/late confirm is a no-op (AC3)
            }

            ProjectionFreshnessStatus status;
            try
            {
                status = await request.Reconcile(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Reconcile re-read failed; will retry on the next confirm/poll.");
                return;
            }

            if (status == ProjectionFreshnessStatus.Current)
            {
                if (Interlocked.CompareExchange(ref reconciled, 1, 0) == 0)
                {
                    request.Announce(StatusKind.AcceptedProcessing, null); // reconciled — polite
                    _ = confirmed.TrySetResult();
                }
            }
            else
            {
                request.Announce(StatusKind.Degraded, null); // freshness not Current — polite degraded indicator
            }
        }

        using IDisposable handle = subscription.Subscribe(
            request.ProjectionKey.ProjectionType,
            request.ProjectionKey.Tenant,
            () => _ = TryReconcileAsync(cancellationToken));

        using CancellationTokenRegistration registration = cancellationToken.Register(
            static state => ((TaskCompletionSource)state!).TrySetCanceled(),
            confirmed);

        try
        {
            if (!subscription.IsConnected)
            {
                // Disconnected: polling owns the re-read (immediate tick + interval ticks) until Current or reconnect.
                await fallback.RunAsync(TryReconcileAsync, () => Volatile.Read(ref reconciled) == 1, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (Volatile.Read(ref reconciled) == 0)
            {
                // Connected (await the SignalR confirm), or the fallback stopped on reconnect (now await the confirm).
                await confirmed.Task.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Circuit teardown / abandonment — stop awaiting reconcile silently.
        }
    }
}
