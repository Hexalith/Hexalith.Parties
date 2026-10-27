using Hexalith.Parties.Client;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.UI.Status;

using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.UI.Services;

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
    ILogger<OptimisticReconcile> logger,
    IServiceProvider? services = null)
{
    private const string SavingMessage = "Saving…";

    /// <summary>Runs the optimistic-then-reconcile flow for <paramref name="request"/>.</summary>
    public async Task ExecuteAsync(OptimisticReconcileRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
#if HFC_SHARED_UI
        if (services?.GetService(typeof(Hexalith.FrontComposer.Shell.Services.Feedback.FcOptimisticReconciler))
            is Hexalith.FrontComposer.Shell.Services.Feedback.FcOptimisticReconciler shared)
        {
            CommandAcceptance sharedAcceptance = default;
            await shared.ExecuteAsync(new Hexalith.FrontComposer.Contracts.Freshness.FcOptimisticReconcileRequest
            {
                ApplyOptimistic = request.ApplyOptimistic,
                IssueCommand = async token =>
                {
                    sharedAcceptance = await request.IssueCommand(token).ConfigureAwait(false);
                    return sharedAcceptance.IsAccepted;
                },
                Reconcile = async token => (await request.Reconcile(token).ConfigureAwait(false)) switch
                {
                    ProjectionFreshnessStatus.Current => Hexalith.FrontComposer.Contracts.Freshness.FcFreshnessStatus.Current,
                    ProjectionFreshnessStatus.Stale => Hexalith.FrontComposer.Contracts.Freshness.FcFreshnessStatus.Stale,
                    _ => Hexalith.FrontComposer.Contracts.Freshness.FcFreshnessStatus.Degraded,
                },
                Revert = request.Revert,
                Rejected = () => request.Announce(sharedAcceptance.FailureStatus, sharedAcceptance.FailureReason),
                Failed = exception =>
                {
                    if (exception is PartiesClientException clientException)
                    {
                        request.Announce(StatusPresentation.FromClientException(clientException), clientException.Detail ?? clientException.Title);
                    }
                    else
                    {
                        request.Announce(StatusPresentation.FromException(exception), exception.Message);
                    }
                },
                Announce = state => request.Announce(
                    state == Hexalith.FrontComposer.Contracts.Freshness.FcReconcileState.Degraded ? StatusKind.Degraded : StatusKind.AcceptedProcessing,
                    state == Hexalith.FrontComposer.Contracts.Freshness.FcReconcileState.Saving ? SavingMessage : null),
                Subscribe = callback => subscription.Subscribe(request.ProjectionKey.ProjectionType, request.ProjectionKey.Tenant, callback),
                IsConnected = () => subscription.IsConnected,
                RunFallback = fallback.RunAsync,
                ReadFailed = () => OptimisticReconcileLog.ReadFailed(logger),
            }, cancellationToken).ConfigureAwait(false);
            return;
        }
#endif

        _ = services;
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
