using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Configuration;
using Hexalith.Parties.Projections.Handlers;
using Hexalith.Parties.Projections.Models;

using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.Projections.Tests.Handlers;

/// <summary>
/// Verifies overlapping snapshot reads without depending on state-store timing.
/// </summary>
public sealed class PartyDetailRebuildReadTests
{
    private static readonly IOptions<PartySdkReadModelOptions> s_options =
        Options.Create(new PartySdkReadModelOptions { ReadModelStateStoreName = "statestore" });

    [Theory]
    [InlineData(true, "detail-etag", "processing-etag")]
    [InlineData(false, "detail-etag", "processing-etag")]
    [InlineData(true, null, null)]
    [InlineData(false, null, null)]
    [InlineData(true, "detail-etag", null)]
    [InlineData(false, null, "processing-etag")]
    public async Task PrepareRebuildAsync_OverlapsReadsAndPreservesSlotConcurrencyAsync(
        bool detailCompletesFirst,
        string? detailEtag,
        string? processingEtag)
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        IReadModelBatchStore batches = Substitute.For<IReadModelBatchStore>();
        var detailRead = new TaskCompletionSource<ReadModelEntry<PartyDetailSdkReadModel>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var processingRead = new TaskCompletionSource<ReadModelEntry<PartyProcessingSdkReadModel>>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken token = TestContext.Current.CancellationToken;
        ConfigureReads(store, detailRead.Task, processingRead.Task, token);
        var handler = new PartyDetailSdkProjectionHandler(store, batches, s_options);
        var detail = new ReadModelEntry<PartyDetailSdkReadModel>(detailEtag is null ? null : new(), detailEtag);
        var processing = new ReadModelEntry<PartyProcessingSdkReadModel>(processingEtag is null ? null : new(), processingEtag);

        Task<DomainProjectionRebuildPlan> pending = handler.PrepareRebuildAsync(Request(), "rebuild", token);
        try
        {
            AssertBothReads(store, token);
            pending.IsCompleted.ShouldBeFalse();
            if (detailCompletesFirst)
            {
                detailRead.SetResult(detail);
            }
            else
            {
                processingRead.SetResult(processing);
            }

            pending.IsCompleted.ShouldBeFalse();
        }
        finally
        {
            detailRead.TrySetResult(detail);
            processingRead.TrySetResult(processing);
        }

        DomainProjectionRebuildPlan plan = await pending.WaitAsync(TimeSpan.FromSeconds(5), token);
        plan.StoreName.ShouldBe("statestore");
        plan.Operations.Count.ShouldBe(2);
        plan.Operations[0].Key.ShouldBe(PartySdkReadModelAddresses.Detail("tenant-a", "party-1"));
        plan.Operations[0].Concurrency.ShouldBe(detailEtag is null
            ? ReadModelBatchConcurrency.CreateOnly
            : ReadModelBatchConcurrency.Match(detailEtag));
        plan.Operations[1].Key.ShouldBe(PartySdkReadModelAddresses.Processing("tenant-a", "party-1"));
        plan.Operations[1].Concurrency.ShouldBe(processingEtag is null
            ? ReadModelBatchConcurrency.CreateOnly
            : ReadModelBatchConcurrency.Match(processingEtag));
        batches.ReceivedCalls().ShouldBeEmpty();
        store.ReceivedCalls().Count().ShouldBe(2);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PrepareRebuildAsync_ReadFailureWaitsForSiblingAndPropagatesAsync(bool detailFails)
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        IReadModelBatchStore batches = Substitute.For<IReadModelBatchStore>();
        var detailRead = new TaskCompletionSource<ReadModelEntry<PartyDetailSdkReadModel>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var processingRead = new TaskCompletionSource<ReadModelEntry<PartyProcessingSdkReadModel>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var failure = new InvalidOperationException("read failed");
        if (detailFails)
        {
            detailRead.SetException(failure);
        }
        else
        {
            processingRead.SetException(failure);
        }

        CancellationToken token = TestContext.Current.CancellationToken;
        ConfigureReads(store, detailRead.Task, processingRead.Task, token);
        var handler = new PartyDetailSdkProjectionHandler(store, batches, s_options);
        Task<DomainProjectionRebuildPlan> pending = handler.PrepareRebuildAsync(Request(), "rebuild", token);
        try
        {
            AssertBothReads(store, token);
            pending.IsCompleted.ShouldBeFalse();
        }
        finally
        {
            detailRead.TrySetResult(new(null, null));
            processingRead.TrySetResult(new(null, null));
        }

        InvalidOperationException actual = await Should.ThrowAsync<InvalidOperationException>(
            () => pending.WaitAsync(TimeSpan.FromSeconds(5), token));
        actual.ShouldBeSameAs(failure);
        batches.ReceivedCalls().ShouldBeEmpty();
        store.ReceivedCalls().Count().ShouldBe(2);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PrepareRebuildAsync_CancelledReadWaitsForSiblingAndPropagatesAsync(bool detailCancels)
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        IReadModelBatchStore batches = Substitute.For<IReadModelBatchStore>();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        CancellationToken token = cancellation.Token;
        var detailRead = new TaskCompletionSource<ReadModelEntry<PartyDetailSdkReadModel>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var processingRead = new TaskCompletionSource<ReadModelEntry<PartyProcessingSdkReadModel>>(TaskCreationOptions.RunContinuationsAsynchronously);
        ConfigureReads(store, detailRead.Task, processingRead.Task, token);
        store.GetAsync<PartyProcessingSdkReadModel>("statestore", PartySdkReadModelAddresses.Processing("tenant-a", "party-1"), token)
            .Returns(_ =>
            {
                // Cancel during the second read invocation, before preparation reaches its await.
                // An early cancellation return is then synchronous and cannot evade IsCompleted.
                cancellation.Cancel();
                if (detailCancels)
                {
                    detailRead.SetCanceled(token);
                }
                else
                {
                    processingRead.SetCanceled(token);
                }

                return processingRead.Task;
            });
        var handler = new PartyDetailSdkProjectionHandler(store, batches, s_options);
        Task<DomainProjectionRebuildPlan> pending = handler.PrepareRebuildAsync(Request(), "rebuild", token);
        try
        {
            AssertBothReads(store, token);
            pending.IsCompleted.ShouldBeFalse();
        }
        finally
        {
            detailRead.TrySetResult(new(null, null));
            processingRead.TrySetResult(new(null, null));
        }

        OperationCanceledException actual = await Should.ThrowAsync<OperationCanceledException>(
            () => pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        actual.CancellationToken.ShouldBe(token);
        pending.IsCanceled.ShouldBeTrue();
        batches.ReceivedCalls().ShouldBeEmpty();
        store.ReceivedCalls().Count().ShouldBe(2);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task PrepareRebuildAsync_ConcurrentFailuresPropagateAReadFaultAsync(bool detailFails, bool siblingCancels)
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        IReadModelBatchStore batches = Substitute.For<IReadModelBatchStore>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var detailFailure = new InvalidOperationException("detail read failed");
        var processingFailure = new InvalidOperationException("processing read failed");
        Task<ReadModelEntry<PartyDetailSdkReadModel>> detailRead = !detailFails && siblingCancels
            ? Task.FromCanceled<ReadModelEntry<PartyDetailSdkReadModel>>(cancellation.Token)
            : Task.FromException<ReadModelEntry<PartyDetailSdkReadModel>>(detailFailure);
        Task<ReadModelEntry<PartyProcessingSdkReadModel>> processingRead = detailFails && siblingCancels
            ? Task.FromCanceled<ReadModelEntry<PartyProcessingSdkReadModel>>(cancellation.Token)
            : Task.FromException<ReadModelEntry<PartyProcessingSdkReadModel>>(processingFailure);
        CancellationToken token = TestContext.Current.CancellationToken;
        ConfigureReads(store, detailRead, processingRead, token);
        var handler = new PartyDetailSdkProjectionHandler(store, batches, s_options);

        InvalidOperationException actual = await Should.ThrowAsync<InvalidOperationException>(() =>
            handler.PrepareRebuildAsync(Request(), "rebuild", token));

        AssertBothReads(store, token);
        if (siblingCancels)
        {
            actual.ShouldBeSameAs(detailFails ? detailFailure : processingFailure);
        }
        else
        {
            new[] { detailFailure, processingFailure }.ShouldContain(actual);
        }

        batches.ReceivedCalls().ShouldBeEmpty();
        store.ReceivedCalls().Count().ShouldBe(2);
    }

    [Fact]
    public async Task PrepareRebuildAsync_PreCancelledRequestDoesNotReadAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        IReadModelBatchStore batches = Substitute.For<IReadModelBatchStore>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var handler = new PartyDetailSdkProjectionHandler(store, batches, s_options);

        OperationCanceledException actual = await Should.ThrowAsync<OperationCanceledException>(() =>
            handler.PrepareRebuildAsync(Request(), "rebuild", cancellation.Token));

        actual.CancellationToken.ShouldBe(cancellation.Token);
        store.ReceivedCalls().ShouldBeEmpty();
        batches.ReceivedCalls().ShouldBeEmpty();
    }

    private static void ConfigureReads(
        IReadModelStore store,
        Task<ReadModelEntry<PartyDetailSdkReadModel>> detail,
        Task<ReadModelEntry<PartyProcessingSdkReadModel>> processing,
        CancellationToken token)
    {
        store.GetAsync<PartyDetailSdkReadModel>("statestore", PartySdkReadModelAddresses.Detail("tenant-a", "party-1"), token)
            .Returns(detail);
        store.GetAsync<PartyProcessingSdkReadModel>("statestore", PartySdkReadModelAddresses.Processing("tenant-a", "party-1"), token)
            .Returns(processing);
    }

    private static void AssertBothReads(IReadModelStore store, CancellationToken token)
    {
        _ = store.Received(1).GetAsync<PartyDetailSdkReadModel>(
            "statestore", PartySdkReadModelAddresses.Detail("tenant-a", "party-1"), token);
        _ = store.Received(1).GetAsync<PartyProcessingSdkReadModel>(
            "statestore", PartySdkReadModelAddresses.Processing("tenant-a", "party-1"), token);
    }

    private static ProjectionRequest Request()
        => new("tenant-a", "party", "party-1",
        [
            new ProjectionEventDto(
                nameof(PartyCreated),
                JsonSerializer.SerializeToUtf8Bytes(new PartyCreated { Type = PartyType.Person }, PartiesJsonOptions.Default),
                "json",
                1,
                DateTimeOffset.UnixEpoch,
                "correlation-1"),
        ]);
}
