using Hexalith.Parties.Client;
using Hexalith.Parties.Client.AdminPortal;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.UI.Services;
using Hexalith.Parties.UI.Status;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.Parties.UI.Tests;

/// <summary>
/// Story 1.7 AC1/AC3 — the shared optimistic-then-reconcile primitive: optimistic + polite "Saving…" →
/// confirm-reconcile (via <c>Freshness=Current</c> AND via a SignalR signal); rejection → revert + alert;
/// user-cancel → silent drop; duplicate/late confirm → no double-apply; announce-without-focus-steal.
/// </summary>
public sealed class OptimisticReconcileTests
{
    private const string ProjectionType = "party-detail";
    private const string Tenant = "tenant-a";

    [Fact]
    public async Task HappyPath_ViaFreshnessCurrent_AppliesOptimistic_AnnouncesSaving_ThenReconcilesPolitely()
    {
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
        OptimisticReconcile primitive = NewPrimitive(stream);

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
    }

    private static CommandAcceptance MapGdpr(AdminPortalGdprCommandResult result)
        => result.Outcome == AdminPortalGdprOutcome.Accepted
            ? CommandAcceptance.Accepted
            : CommandAcceptance.Rejected(StatusKind.Validation, result.Detail);

    private static FakeProjectionStream NewStream(bool connected) => new() { IsConnected = connected };

    private static OptimisticReconcile NewPrimitive(FakeProjectionStream stream)
    {
        var subscription = new PartiesProjectionSubscription(stream, NullLogger<PartiesProjectionSubscription>.Instance);
        var options = Options.Create(new ProjectionFreshnessOptions { PollingIntervalSeconds = 30 });
        var fallback = new ProjectionFreshnessFallback(stream, new ManualTimeProvider(), options);
        return new OptimisticReconcile(subscription, fallback, NullLogger<OptimisticReconcile>.Instance);
    }

    private sealed class Recorder
    {
        public bool OptimisticApplied { get; private set; }

        public bool Reverted { get; private set; }

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
                Revert = () => Reverted = true,
                ProjectionKey = (ProjectionType, Tenant),
                Announce = (kind, message) => Announcements.Add((kind, message)),
            };
    }
}
