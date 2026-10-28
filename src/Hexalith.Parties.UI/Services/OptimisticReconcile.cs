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
    /// <summary>Preserves the existing three-argument adapter constructor for compiled callers.</summary>
    /// <param name="subscription">The caller-owned projection transport adapter.</param>
    /// <param name="fallback">The caller-owned polling fallback.</param>
    /// <param name="logger">The existing service-category logger.</param>
    public OptimisticReconcile(
        PartiesProjectionSubscription subscription,
        ProjectionFreshnessFallback fallback,
        ILogger<OptimisticReconcile> logger)
        : this(subscription, fallback, logger, null)
    {
    }

    private const string SavingMessage = "Saving…";

    /// <summary>Runs the optimistic-then-reconcile flow for <paramref name="request"/>.</summary>
    public async Task ExecuteAsync(OptimisticReconcileRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }
#if HFC_SHARED_RECONCILE_READINESS
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
                WaitForSubscriptionAsync = PartiesProjectionSubscription.WaitUntilReadyAsync,
                IsConnected = () => subscription.IsConnected,
                RunFallback = fallback.RunAsync,
                ReadFailed = () => OptimisticReconcileLog.ReadFailed(logger),
            }, cancellationToken).ConfigureAwait(false);
            return;
        }
#endif

        _ = services;
        request.ApplyOptimistic();
        request.Announce(StatusKind.AcceptedProcessing, SavingMessage);
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CancellationToken token = lifetime.Token;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        // Serializing asynchronous reads fences simultaneous confirmations and late responses.
        // The gate remains reachable by a captured late callback, so it is intentionally not disposed.
        var gate = new SemaphoreSlim(1, 1);
        bool done = false;
        bool accepted = false;
        bool commandStarted = false;
        int pendingConfirmation = 0;
        async Task TryReconcileAsync(CancellationToken ignored)
        {
            if (!Volatile.Read(ref commandStarted))
            {
                return;
            }
            if (!Volatile.Read(ref accepted))
            {
                Interlocked.Exchange(ref pendingConfirmation, 1);
                if (!Volatile.Read(ref accepted))
                {
                    return;
                }
            }
            try
            {
                await gate.WaitAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }

            try
            {
                if (done || token.IsCancellationRequested)
                {
                    return;
                }

                ProjectionFreshnessStatus freshness;
                try
                {
                    freshness = await request.Reconcile(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception)
                {
                    if (!token.IsCancellationRequested)
                    {
                        OptimisticReconcileLog.ReadFailed(logger);
                    }
                    return;
                }

                if (token.IsCancellationRequested)
                {
                    return;
                }

                if (freshness == ProjectionFreshnessStatus.Current)
                {
                    done = true;
                    request.Announce(StatusKind.AcceptedProcessing, null);
                    completion.TrySetResult();
                }
                else
                {
                    request.Announce(StatusKind.Degraded, null);
                }
            }
            catch (Exception exception)
            {
                // Subscription callbacks are fire-and-forget; deliver callback faults to
                // the operation owner so it can terminate and dispose the subscription.
                done = true;
                completion.TrySetException(exception);
            }
            finally
            {
                gate.Release();
            }
        }

        IDisposable handle;
        try
        {
            handle = subscription.Subscribe(request.ProjectionKey.ProjectionType, request.ProjectionKey.Tenant,
                () => _ = TryReconcileAsync(token));
        }
        catch (Exception exception)
        {
            if (!token.IsCancellationRequested)
            {
                request.Revert();
                if (exception is PartiesClientException clientException)
                {
                    request.Announce(StatusPresentation.FromClientException(clientException), clientException.Detail ?? clientException.Title);
                }
                else
                {
                    request.Announce(StatusPresentation.FromException(exception), exception.Message);
                }
            }
            return;
        }
        using CancellationTokenRegistration registration = token.Register(() => completion.TrySetCanceled(token));
        Task polling = Task.CompletedTask;
        try
        {
            CommandAcceptance commandAcceptance;
            try
            {
                await PartiesProjectionSubscription.WaitUntilReadyAsync(handle, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                Volatile.Write(ref commandStarted, true);
                commandAcceptance = await request.IssueCommand(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }
                request.Revert();
                if (exception is PartiesClientException clientException)
                {
                    request.Announce(StatusPresentation.FromClientException(clientException), clientException.Detail ?? clientException.Title);
                }
                else
                {
                    request.Announce(StatusPresentation.FromException(exception), exception.Message);
                }
                return;
            }
            if (token.IsCancellationRequested)
            {
                return;
            }
            if (!commandAcceptance.IsAccepted)
            {
                request.Revert();
                request.Announce(commandAcceptance.FailureStatus, commandAcceptance.FailureReason);
                return;
            }
            Volatile.Write(ref accepted, true);
            if (Interlocked.Exchange(ref pendingConfirmation, 0) == 1)
            {
                await TryReconcileAsync(token).ConfigureAwait(false);
            }
            polling = ObserveFallbackAsync();
            await completion.Task.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Caller abandonment has no failure announcement or focus effect.
        }
        finally
        {
            try
            {
                try
                {
                    await lifetime.CancelAsync().ConfigureAwait(false);
                }
                finally
                {
                    if (handle is IAsyncDisposable asynchronous)
                    {
                        await asynchronous.DisposeAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        handle.Dispose();
                    }
                }
            }
            finally
            {
                try
                {
                    await polling.ConfigureAwait(false);
                }
                finally
                {
                    // Even a cancellation or disposal fault must drain every serialized read.
                    await gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
                    gate.Release();
                }
            }
        }

        async Task ObserveFallbackAsync()
        {
            try
            {
                // The caller's driver observes connectivity for the whole operation, including reconnects.
                await fallback.RunAsync(TryReconcileAsync, () => Volatile.Read(ref done), token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }
    }
}
