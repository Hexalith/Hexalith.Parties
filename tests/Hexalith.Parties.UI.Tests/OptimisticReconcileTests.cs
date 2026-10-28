using Hexalith.Parties.Client;
using Hexalith.Parties.Client.AdminPortal;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.UI.Services;
using Hexalith.Parties.UI.Status;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;
using NSubstitute;

namespace Hexalith.Parties.UI.Tests;

/// <summary>
/// Story 1.7 AC1/AC3 — the shared optimistic-then-reconcile primitive: optimistic + polite "Saving…" →
/// confirm-reconcile (via <c>Freshness=Current</c> AND via a SignalR signal); rejection → revert + alert;
/// user-cancel → silent drop; duplicate/late confirm → no double-apply; announce-without-focus-steal.
/// </summary>
public sealed class OptimisticReconcileTests
{
    private const string ProjectionType = PartyProjectionNames.Detail;
    private const string Tenant = "tenant-a";

    [Fact]
    public async Task HappyPath_ViaFreshnessCurrent_AppliesOptimistic_AnnouncesSaving_ThenReconcilesPolitely()
    {
        typeof(OptimisticReconcile).GetConstructor([
            typeof(PartiesProjectionSubscription), typeof(ProjectionFreshnessFallback), typeof(ILogger<OptimisticReconcile>),
        ]).ShouldNotBeNull();
        var recorder = new Recorder();
        // Disconnected → the polling fallback owns the re-read; its immediate tick returns Current at once.
        FakeProjectionStream stream = NewStream(connected: false);
        OptimisticReconcile primitive = NewPrimitive(stream);

        OptimisticReconcileRequest request = recorder.BuildRequest(
            issueCommand: _ => Task.FromResult(CommandAcceptance.Accepted),
            reconcile: _ => Task.FromResult(ProjectionFreshnessStatus.Current));

        await primitive.ExecuteAsync(request, CancellationToken.None);

        recorder.OptimisticApplied.ShouldBeTrue();
        recorder.Announcements[0].ShouldBe((StatusKind.AcceptedProcessing, "Saving…"));
        recorder.IssueCommandCalls.ShouldBe(1);
        recorder.ReconcileCalls.ShouldBe(1);
        recorder.Reverted.ShouldBeFalse();

        // Final announce is polite (a status kind, never an alert).
        (StatusKind finalKind, _) = recorder.Announcements[^1];
        StatusPresentation.PolitenessFor(finalKind).ShouldBe(LiveRegionPoliteness.Polite);
    }

    [Fact]
    public async Task HappyPath_ViaSignalRConfirm_RunsReconcileOnceOnTheConfirm()
    {
        var recorder = new Recorder();
        FakeProjectionStream stream = NewStream(connected: true); // connected → await the SignalR confirm
        OptimisticReconcile primitive = NewPrimitive(stream);

        OptimisticReconcileRequest request = recorder.BuildRequest(
            issueCommand: _ => Task.FromResult(CommandAcceptance.Accepted),
            reconcile: _ => Task.FromResult(ProjectionFreshnessStatus.Current));

        Task run = primitive.ExecuteAsync(request, CancellationToken.None);

        // Connected: no immediate re-read — reconcile fires only when the projection confirms.
        recorder.ReconcileCalls.ShouldBe(0);
        run.IsCompleted.ShouldBeFalse();

        stream.RaiseChanged(ProjectionType, Tenant);
        await run.ConfigureAwait(true);

        recorder.ReconcileCalls.ShouldBe(1);
        recorder.Reverted.ShouldBeFalse();
        (StatusKind finalKind, _) = recorder.Announcements[^1];
        StatusPresentation.PolitenessFor(finalKind).ShouldBe(LiveRegionPoliteness.Polite);
    }

    [Fact]
    public async Task DuplicateOrLateConfirm_RunsReconcileExactlyOnce()
    {
        var recorder = new Recorder();
        FakeProjectionStream stream = NewStream(connected: true);
        OptimisticReconcile primitive = NewPrimitive(stream);

        OptimisticReconcileRequest request = recorder.BuildRequest(
            issueCommand: _ => Task.FromResult(CommandAcceptance.Accepted),
            reconcile: _ => Task.FromResult(ProjectionFreshnessStatus.Current));

        Task run = primitive.ExecuteAsync(request, CancellationToken.None);

        // Grab the registered confirm callback and fire it repeatedly — including after completion/dispose.
        Action confirm = stream.Subscriptions.ShouldHaveSingleItem().OnChanged;
        confirm(); // first confirm → reconciles (guard set)
        confirm(); // duplicate confirm → one-shot guard makes it a no-op
        await run.ConfigureAwait(true);
        confirm(); // late confirm after the subscription disposed → still a no-op

        recorder.ReconcileCalls.ShouldBe(1);
    }

    [Fact]
    public async Task Rejection_ClientValidationException_RevertsAndAnnouncesAssertively()
    {
        var recorder = new Recorder();
        OptimisticReconcile primitive = NewPrimitive(NewStream(connected: true));

        OptimisticReconcileRequest request = recorder.BuildRequest(
            issueCommand: _ => throw new PartiesClientException(422, "Validation", null, "Name is required", "corr-1"),
            reconcile: _ => Task.FromResult(ProjectionFreshnessStatus.Current));

        await primitive.ExecuteAsync(request, CancellationToken.None);

        recorder.Reverted.ShouldBeTrue();
        recorder.ReconcileCalls.ShouldBe(0);
        (StatusKind kind, _) = recorder.Announcements[^1];
        kind.ShouldBe(StatusKind.Validation);
        StatusPresentation.PolitenessFor(kind).ShouldBe(LiveRegionPoliteness.Assertive);
    }

    [Fact]
    public async Task Rejection_TenantWarmingUp403_RevertsAndAnnouncesTenantUnavailablePolitely()
    {
        var recorder = new Recorder();
        OptimisticReconcile primitive = NewPrimitive(NewStream(connected: true));

        OptimisticReconcileRequest request = recorder.BuildRequest(
            issueCommand: _ => throw new PartiesClientException(403, "Tenant warming up", null, "tenant is initializing", "corr-2"),
            reconcile: _ => Task.FromResult(ProjectionFreshnessStatus.Current));

        await primitive.ExecuteAsync(request, CancellationToken.None);

        recorder.Reverted.ShouldBeTrue();
        (StatusKind kind, _) = recorder.Announcements[^1];
        kind.ShouldBe(StatusKind.TenantUnavailable);
        StatusPresentation.PolitenessFor(kind).ShouldBe(LiveRegionPoliteness.Polite);
    }

    [Fact]
    public async Task Rejection_GdprNonAcceptedOutcome_RevertsAndAnnouncesAssertively()
    {
        var recorder = new Recorder();
        OptimisticReconcile primitive = NewPrimitive(NewStream(connected: true));

        // The call site maps its own (non-throwing) GDPR result into a CommandAcceptance — the primitive
        // stays slice-agnostic. A non-Accepted Outcome → revert + alert.
        var gdpr = new AdminPortalGdprCommandResult(AdminPortalGdprOutcome.ValidationRejected, "corr-3", "consent already revoked");
        OptimisticReconcileRequest request = recorder.BuildRequest(
            issueCommand: _ => Task.FromResult(MapGdpr(gdpr)),
            reconcile: _ => Task.FromResult(ProjectionFreshnessStatus.Current));

        await primitive.ExecuteAsync(request, CancellationToken.None);

        recorder.Reverted.ShouldBeTrue();
        recorder.ReconcileCalls.ShouldBe(0);
        (StatusKind kind, _) = recorder.Announcements[^1];
        kind.ShouldBe(StatusKind.Validation);
        StatusPresentation.PolitenessFor(kind).ShouldBe(LiveRegionPoliteness.Assertive);
    }

    [Fact]
    public async Task UserInitiatedCancellation_IsDroppedSilently_NoRevertNoFailure()
    {
        var recorder = new Recorder();
        OptimisticReconcile primitive = NewPrimitive(NewStream(connected: true));

        using var cts = new CancellationTokenSource();
        OptimisticReconcileRequest request = recorder.BuildRequest(
            issueCommand: ct =>
            {
                cts.Cancel();                  // the user navigated away / the component disposed
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(CommandAcceptance.Accepted);
            },
            reconcile: _ => Task.FromResult(ProjectionFreshnessStatus.Current));

        await primitive.ExecuteAsync(request, cts.Token);

        recorder.Reverted.ShouldBeFalse();
        // Only the initial polite "Saving…" was announced — no failure state.
        recorder.Announcements.ShouldHaveSingleItem();
        recorder.Announcements[0].ShouldBe((StatusKind.AcceptedProcessing, "Saving…"));
    }

    [Fact]
    public async Task UnrequestedCancellationOrTimeout_MapsToTransientFailureAssertively()
    {
        var recorder = new Recorder();
        OptimisticReconcile primitive = NewPrimitive(NewStream(connected: true));

        // Token NOT requested — a transport timeout surfacing as OperationCanceledException.
        OptimisticReconcileRequest request = recorder.BuildRequest(
            issueCommand: _ => throw new OperationCanceledException(),
            reconcile: _ => Task.FromResult(ProjectionFreshnessStatus.Current));

        await primitive.ExecuteAsync(request, CancellationToken.None);

        recorder.Reverted.ShouldBeTrue();
        (StatusKind kind, _) = recorder.Announcements[^1];
        kind.ShouldBe(StatusKind.TransientFailure);
        StatusPresentation.PolitenessFor(kind).ShouldBe(LiveRegionPoliteness.Assertive);
    }

    [Fact]
    public async Task AnnounceNotSteal_OptimisticSignalIsPolite_AndOnlyTheAnnounceDelegateIsUsed()
    {
        var recorder = new Recorder();
        OptimisticReconcile primitive = NewPrimitive(NewStream(connected: false));

        OptimisticReconcileRequest request = recorder.BuildRequest(
            issueCommand: _ => Task.FromResult(CommandAcceptance.Accepted),
            reconcile: _ => Task.FromResult(ProjectionFreshnessStatus.Current));

        await primitive.ExecuteAsync(request, CancellationToken.None);

        // The primitive's only UI interaction is the announce delegate; the optimistic "Saving…" signal is
        // POLITE (never assertive → never steals focus). There is no focus API to call.
        recorder.Announcements.Count.ShouldBeGreaterThanOrEqualTo(1);
        StatusPresentation.PolitenessFor(recorder.Announcements[0].Kind).ShouldBe(LiveRegionPoliteness.Polite);
    }

    [Fact]
    public async Task ReconcileReturningNonCurrent_AnnouncesDegradedPolitely_KeepsWaiting_ThenReconcilesOnceWhenCurrent()
    {
        var recorder = new Recorder();
        FakeProjectionStream stream = NewStream(connected: true); // connected → reconcile fires on each confirm
        OptimisticReconcile primitive = NewPrimitive(stream);

        // The projection is still catching up: the first re-read is Stale, the next is Current.
        var statuses = new Queue<ProjectionFreshnessStatus>(
            [ProjectionFreshnessStatus.Stale, ProjectionFreshnessStatus.Current]);
        OptimisticReconcileRequest request = recorder.BuildRequest(
            issueCommand: _ => Task.FromResult(CommandAcceptance.Accepted),
            reconcile: _ => Task.FromResult(statuses.Dequeue()));

        Task run = primitive.ExecuteAsync(request, CancellationToken.None);

        // First confirm: re-read is non-Current → polite Degraded indicator, NOT yet reconciled (keep waiting).
        stream.RaiseChanged(ProjectionType, Tenant);
        run.IsCompleted.ShouldBeFalse();
        recorder.ReconcileCalls.ShouldBe(1);
        (StatusKind degradedKind, _) = recorder.Announcements[^1];
        degradedKind.ShouldBe(StatusKind.Degraded);
        StatusPresentation.PolitenessFor(degradedKind).ShouldBe(LiveRegionPoliteness.Polite);

        // Second confirm: re-read now Current → reconciles exactly once, polite reconciled announce, no revert.
        stream.RaiseChanged(ProjectionType, Tenant);
        await run.ConfigureAwait(true);

        recorder.ReconcileCalls.ShouldBe(2);
        recorder.Reverted.ShouldBeFalse();
        (StatusKind finalKind, _) = recorder.Announcements[^1];
        StatusPresentation.PolitenessFor(finalKind).ShouldBe(LiveRegionPoliteness.Polite);
    }

    [Fact]
    public async Task Cancellation_WhileAwaitingTheSignalRConfirm_DropsSilently_NoRevertNoFailure()
    {
        var recorder = new Recorder();
        OptimisticReconcile primitive = NewPrimitive(NewStream(connected: true));

        using var cts = new CancellationTokenSource();
        OptimisticReconcileRequest request = recorder.BuildRequest(
            issueCommand: _ => Task.FromResult(CommandAcceptance.Accepted),
            reconcile: _ => Task.FromResult(ProjectionFreshnessStatus.Current));

        // Command accepted; the primitive is now parked awaiting the projection-confirm.
        Task run = primitive.ExecuteAsync(request, cts.Token);
        run.IsCompleted.ShouldBeFalse();

        // The circuit tears down / the user navigates away mid-reconcile → drop silently (no confirm ever arrives).
        await cts.CancelAsync().ConfigureAwait(true);
        await run.ConfigureAwait(true);

        recorder.Reverted.ShouldBeFalse();
        recorder.ReconcileCalls.ShouldBe(0);
        recorder.Announcements.ShouldHaveSingleItem(); // only the initial polite "Saving…" — no failure state
        recorder.Announcements[0].ShouldBe((StatusKind.AcceptedProcessing, "Saving…"));
    }

    [Fact]
    public async Task ReconcileReadThrowsTransiently_IsRetriedOnTheNextConfirm_NoRevert()
    {
        var recorder = new Recorder();
        FakeProjectionStream stream = NewStream(connected: true);
        var logger = Substitute.For<ILogger<OptimisticReconcile>>();
        logger.IsEnabled(LogLevel.Warning).Returns(true);
        OptimisticReconcile primitive = NewPrimitive(stream, logger);

        int reads = 0;
        OptimisticReconcileRequest request = recorder.BuildRequest(
            issueCommand: _ => Task.FromResult(CommandAcceptance.Accepted),
            reconcile: _ =>
            {
                reads++;
                return reads == 1
                    ? throw new InvalidOperationException("transient re-read failure")
                    : Task.FromResult(ProjectionFreshnessStatus.Current);
            });

        Task run = primitive.ExecuteAsync(request, CancellationToken.None);

        // First confirm: the re-read throws → swallowed (logged), NOT reconciled, NOT reverted; keep waiting.
        stream.RaiseChanged(ProjectionType, Tenant);
        run.IsCompleted.ShouldBeFalse();
        recorder.Reverted.ShouldBeFalse();

        // Second confirm: the re-read succeeds → reconciles once.
        stream.RaiseChanged(ProjectionType, Tenant);
        await run.ConfigureAwait(true);

        recorder.ReconcileCalls.ShouldBe(2);
        recorder.Reverted.ShouldBeFalse();
        (StatusKind finalKind, _) = recorder.Announcements[^1];
        StatusPresentation.PolitenessFor(finalKind).ShouldBe(LiveRegionPoliteness.Polite);
#if HFC_SHARED_UI
        // Exercise the generated warning on the actual adapter path, with no exception payload.
        var warning = logger.ReceivedCalls().Where(call => call.GetMethodInfo().Name == "Log").ShouldHaveSingleItem();
        warning.GetArguments()[0].ShouldBe(LogLevel.Warning);
        warning.GetArguments()[2]!.ToString().ShouldBe("Reconcile re-read failed; will retry on the next confirm/poll.");
        warning.GetArguments()[3].ShouldBeNull();
#endif
    }

    [Fact]
    public async Task DelayedRegistration_RecoversOnlyEarlyConfirmation_AndDrainsTeardown()
    {
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeProjectionStream stream = NewStream(connected: true);
        stream.RegistrationDelay = ready.Task;
        var time = new ManualTimeProvider();
        var recorder = new Recorder();

        OptimisticReconcileRequest request = recorder.BuildRequest(
            issueCommand: _ =>
            {
                stream.RaiseChanged(ProjectionType, Tenant); // only signal, while the command is pending
                return Task.FromResult(CommandAcceptance.Accepted);
            },
            reconcile: _ => Task.FromResult(ProjectionFreshnessStatus.Current));
        Task run = NewPrimitive(stream, time: time).ExecuteAsync(request, TestContext.Current.CancellationToken);
        recorder.IssueCommandCalls.ShouldBe(0);
        recorder.ReconcileCalls.ShouldBe(0);
        ready.SetResult();
        await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        recorder.ReconcileCalls.ShouldBe(1);
        recorder.Announcements.Count(item => item == (StatusKind.AcceptedProcessing, (string?)null)).ShouldBe(1);
        stream.Subscriptions.ShouldBeEmpty();
        time.ActiveTimers.ShouldBe(0);
    }

    [Fact]
    public async Task RepeatedDisconnects_ResumePolling_AndCompletionReleasesAllWork()
    {
        var time = new ManualTimeProvider();
        FakeProjectionStream stream = NewStream(connected: true);
        var recorder = new Recorder();
        bool current = false;
        Task run = NewPrimitive(stream, time: time).ExecuteAsync(recorder.BuildRequest(
            _ => Task.FromResult(CommandAcceptance.Accepted),
            _ => Task.FromResult(current ? ProjectionFreshnessStatus.Current : ProjectionFreshnessStatus.Stale)),
            TestContext.Current.CancellationToken);
        recorder.ReconcileCalls.ShouldBe(0);
        stream.IsConnected = false;
        time.Advance(TimeSpan.FromSeconds(30));
        recorder.ReconcileCalls.ShouldBe(1);
        stream.IsConnected = true;
        time.Advance(TimeSpan.FromSeconds(30));
        recorder.ReconcileCalls.ShouldBe(1);
        stream.IsConnected = false;
        current = true;
        time.Advance(TimeSpan.FromSeconds(30));
        await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        recorder.ReconcileCalls.ShouldBe(2);
        stream.Subscriptions.ShouldBeEmpty();
        time.ActiveTimers.ShouldBe(0);
        time.Advance(TimeSpan.FromMinutes(1));
        recorder.ReconcileCalls.ShouldBe(2);
    }

    [Fact]
    public async Task CancellationDuringDelayedRegistration_DrainsLateRegistrationWithoutFeedback()
    {
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeProjectionStream stream = NewStream(connected: true);
        stream.RegistrationDelay = ready.Task;
        var time = new ManualTimeProvider();
        var recorder = new Recorder();
        using var cancellation = new CancellationTokenSource();
        Task run = NewPrimitive(stream, time: time).ExecuteAsync(recorder.BuildRequest(
            _ => Task.FromResult(CommandAcceptance.Accepted),
            _ => Task.FromResult(ProjectionFreshnessStatus.Stale)), cancellation.Token);
        await cancellation.CancelAsync();
        ready.SetResult();
        await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        recorder.IssueCommandCalls.ShouldBe(0);
        recorder.ReconcileCalls.ShouldBe(0);
        recorder.Reverted.ShouldBeFalse();
        stream.Subscriptions.ShouldBeEmpty();
        time.ActiveTimers.ShouldBe(0);
    }

    [Fact]
    public async Task ConnectedOldCurrentWithoutConfirmation_DoesNotReconcileQueuedCommand()
    {
        var stream = NewStream(connected: true);
        var time = new ManualTimeProvider();
        var recorder = new Recorder();
        using var cancellation = new CancellationTokenSource();
        Task run = NewPrimitive(stream, time: time).ExecuteAsync(recorder.BuildRequest(
            _ => Task.FromResult(CommandAcceptance.Accepted),
            _ => Task.FromResult(ProjectionFreshnessStatus.Current)), cancellation.Token);
        time.Advance(TimeSpan.FromMinutes(2));
        run.IsCompleted.ShouldBeFalse();
        recorder.ReconcileCalls.ShouldBe(0);
        recorder.Announcements.ShouldHaveSingleItem();
        stream.RaiseChanged(ProjectionType, Tenant);
        await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        recorder.ReconcileCalls.ShouldBe(1);
        stream.Subscriptions.ShouldBeEmpty();
        time.ActiveTimers.ShouldBe(0);
    }

    [Fact]
    public async Task ConcurrentConfirmations_SerializeReads_AndIgnoreLateCurrentAfterCancellation()
    {
        var stream = NewStream(connected: true);
        var recorder = new Recorder();
        var read = new TaskCompletionSource<ProjectionFreshnessStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        Task run = NewPrimitive(stream).ExecuteAsync(recorder.BuildRequest(
            _ => Task.FromResult(CommandAcceptance.Accepted), _ => read.Task), cancellation.Token);
        Action confirm = stream.Subscriptions.ShouldHaveSingleItem().OnChanged;
        confirm();
        confirm();
        recorder.ReconcileCalls.ShouldBe(1);
        await cancellation.CancelAsync();
        read.SetResult(ProjectionFreshnessStatus.Current);
        await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        confirm();
        recorder.ReconcileCalls.ShouldBe(1);
        recorder.Announcements.ShouldHaveSingleItem();
        stream.Subscriptions.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EarlyConfirmationCannotReconcileRejectedOrCancelledCommand(bool cancel)
    {
        var stream = NewStream(connected: true);
        var recorder = new Recorder();
        using var cancellation = new CancellationTokenSource();
        await NewPrimitive(stream).ExecuteAsync(recorder.BuildRequest(
            token =>
            {
                stream.RaiseChanged(ProjectionType, Tenant);
                if (cancel)
                {
                    cancellation.Cancel();
                    token.ThrowIfCancellationRequested();
                }
                return Task.FromResult(CommandAcceptance.Rejected(StatusKind.Validation, "synthetic rejection"));
            }, _ => Task.FromResult(ProjectionFreshnessStatus.Current)), cancellation.Token)
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        recorder.ReconcileCalls.ShouldBe(0);
        recorder.Reverted.ShouldBe(!cancel);
        recorder.Announcements.Count.ShouldBe(cancel ? 1 : 2);
        stream.Subscriptions.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OfflineConnectionFailure_RetainsAcceptedCommandPollingFallback(bool transportTimeout)
    {
        var stream = NewStream(connected: false);
        stream.StartFailure = transportTimeout
            ? new OperationCanceledException("synthetic transport timeout")
            : new IOException("synthetic offline connection");
        var recorder = new Recorder();
        await NewPrimitive(stream).ExecuteAsync(recorder.BuildRequest(
            _ => Task.FromResult(CommandAcceptance.Accepted),
            _ => Task.FromResult(ProjectionFreshnessStatus.Current)), TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        recorder.IssueCommandCalls.ShouldBe(1);
        recorder.ReconcileCalls.ShouldBe(1);
        recorder.Reverted.ShouldBeFalse();
        stream.Subscriptions.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SubscriptionAcquisitionFailure_RevertsOnceOrSilentlyAbandonsBeforeDispatch(bool cancel)
    {
        var stream = NewStream(connected: true);
        var wrapper = new PartiesProjectionSubscription(stream, NullLogger<PartiesProjectionSubscription>.Instance);
        await wrapper.DisposeAsync();
        var recorder = new Recorder();
        using var cancellation = new CancellationTokenSource();
        OptimisticReconcileRequest request = recorder.BuildRequest(
            _ => Task.FromResult(CommandAcceptance.Accepted), _ => Task.FromResult(ProjectionFreshnessStatus.Current));
        if (cancel)
        {
            request = request with { ApplyOptimistic = cancellation.Cancel };
        }
        await NewPrimitive(stream, subscriptionOverride: wrapper).ExecuteAsync(request, cancellation.Token)
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        recorder.IssueCommandCalls.ShouldBe(0);
        recorder.ReconcileCalls.ShouldBe(0);
        recorder.RevertCalls.ShouldBe(cancel ? 0 : 1);
        recorder.Announcements.Count.ShouldBe(cancel ? 1 : 2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteWaitsForDelayedUnsubscription(bool cancel)
    {
        var stream = NewStream(connected: true);
        var removal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        stream.UnsubscriptionDelay = removal.Task;
        var recorder = new Recorder();
        using var cancellation = new CancellationTokenSource();
        Task run = NewPrimitive(stream).ExecuteAsync(recorder.BuildRequest(
            _ => Task.FromResult(CommandAcceptance.Accepted), _ => Task.FromResult(ProjectionFreshnessStatus.Current)), cancellation.Token);
        if (cancel)
        {
            await cancellation.CancelAsync();
        }
        else
        {
            stream.RaiseChanged(ProjectionType, Tenant);
        }
        await stream.UnsubscriptionStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        run.IsCompleted.ShouldBeFalse();
        stream.Subscriptions.ShouldHaveSingleItem();
        removal.SetResult();
        await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        stream.Unsubscribed.ShouldHaveSingleItem();
        stream.Subscriptions.ShouldBeEmpty();
    }

    [Fact]
    public async Task CleanupCancellationFailureStillAwaitsUnsubscription()
    {
        var stream = NewStream(connected: true);
        var removal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        stream.UnsubscriptionDelay = removal.Task;
        var recorder = new Recorder();
        CancellationTokenRegistration faulting = default;
        Task run = NewPrimitive(stream).ExecuteAsync(recorder.BuildRequest(
            _ => Task.FromResult(CommandAcceptance.Accepted), token =>
            {
                faulting = token.Register(() => throw new InvalidOperationException("synthetic cancellation callback"));
                return Task.FromResult(ProjectionFreshnessStatus.Current);
            }), TestContext.Current.CancellationToken);
        stream.RaiseChanged(ProjectionType, Tenant);
        await stream.UnsubscriptionStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        run.IsCompleted.ShouldBeFalse();
        removal.SetResult();
        await Should.ThrowAsync<AggregateException>(() => run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        faulting.Dispose();
        stream.Subscriptions.ShouldBeEmpty();
    }

    private static CommandAcceptance MapGdpr(AdminPortalGdprCommandResult result)
        => result.Outcome == AdminPortalGdprOutcome.Accepted
            ? CommandAcceptance.Accepted
            : CommandAcceptance.Rejected(StatusKind.Validation, result.Detail);

    private static FakeProjectionStream NewStream(bool connected) => new() { IsConnected = connected };

    private static OptimisticReconcile NewPrimitive(FakeProjectionStream stream, ILogger<OptimisticReconcile>? logger = null, ManualTimeProvider? time = null, PartiesProjectionSubscription? subscriptionOverride = null)
    {
        var subscription = subscriptionOverride ?? new PartiesProjectionSubscription(stream, NullLogger<PartiesProjectionSubscription>.Instance);
        var options = Options.Create(new ProjectionFreshnessOptions { PollingIntervalSeconds = 30 });
        var fallback = new ProjectionFreshnessFallback(stream, time ?? new ManualTimeProvider(), options);
#if HFC_SHARED_UI
        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(Hexalith.FrontComposer.Shell.Services.Feedback.FcOptimisticReconciler))
            .Returns(new Hexalith.FrontComposer.Shell.Services.Feedback.FcOptimisticReconciler());
        return new OptimisticReconcile(subscription, fallback, logger ?? NullLogger<OptimisticReconcile>.Instance, provider);
#else
        return new OptimisticReconcile(subscription, fallback, logger ?? NullLogger<OptimisticReconcile>.Instance);
#endif
    }

    private sealed class Recorder
    {
        public bool OptimisticApplied { get; private set; }

        public bool Reverted { get; private set; }

        public int RevertCalls { get; private set; }

        public int IssueCommandCalls { get; private set; }

        public int ReconcileCalls { get; private set; }

        public List<(StatusKind Kind, string? Message)> Announcements { get; } = [];

        public OptimisticReconcileRequest BuildRequest(
            Func<CancellationToken, Task<CommandAcceptance>> issueCommand,
            Func<CancellationToken, Task<ProjectionFreshnessStatus>> reconcile)
            => new()
            {
                ApplyOptimistic = () => OptimisticApplied = true,
                IssueCommand = ct =>
                {
                    IssueCommandCalls++;
                    return issueCommand(ct);
                },
                Reconcile = ct =>
                {
                    ReconcileCalls++;
                    return reconcile(ct);
                },
                Revert = () => { Reverted = true; RevertCalls++; },
                ProjectionKey = (ProjectionType, Tenant),
                Announce = (kind, message) => Announcements.Add((kind, message)),
            };
    }
}
