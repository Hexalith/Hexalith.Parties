#if HEXALITH_EVENTSTORE_NONEXPIRING_RECOVERY
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Configuration;
using Hexalith.Parties.Projections.Handlers;
using Hexalith.Parties.Projections.Models;
using Hexalith.Parties.Projections.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.Tests.Projections;

/// <summary>Executes large Parties generation cutovers through the SDK's persisted marker/envelope protocol.</summary>
public sealed class PartyProcessingGenerationRecoveryTests
{
    private const string StoreName = "statestore";
    private const int LegacyCount = 4097;
    private static readonly string s_detailKey = PartySdkReadModelAddresses.Detail("tenant-a", "party-1");
    private static readonly string s_processingKey = PartySdkReadModelAddresses.Processing("tenant-a", "party-1");
    // This fixture owns the complete synthetic store, including any private SDK recovery state.
    private static readonly IOptions<PartySdkReadModelOptions> s_options =
        Options.Create(new PartySdkReadModelOptions { ReadModelStateStoreName = StoreName, HistoricalPrivateRecoveryReconciliationAttested = true });

    [Fact]
    public async Task UnqualifiedHistoricalRecoveryRefusesErasureWithEveryPersistedByteUnchangedAsync()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new InMemoryReadModelStore();
        var history = History() with { Events = History().Events.Take(40).ToArray() };
        await SeedLegacyAsync(store, history, token);
        const string historical = "unregistered-historical-private-manifest";
        const string survivor = "unrelated-synthetic-private-state";
        store.SeedRaw(StoreName, historical, new { PrivateContent = "unreconciled historical content" });
        store.SeedRaw(StoreName, survivor, new { PrivateContent = "unrelated content" });
        var before = new[] { s_detailKey, s_processingKey, historical, survivor }
            .ToDictionary(key => key, key => store.SnapshotRaw(StoreName, key)!);
        int count = store.Count;
        int batches = 0;
        store.BatchFaultHook = (_, _, _) => { batches++; return Task.CompletedTask; };
        var defaults = new PartySdkReadModelOptions { ReadModelStateStoreName = StoreName };
        defaults.HistoricalPrivateRecoveryReconciliationAttested.ShouldBeFalse();
        var failure = await Should.ThrowAsync<InvalidOperationException>(() => new PartySdkReadModelEraser(store, store, Options.Create(defaults))
            .EraseAsync("tenant-a", "party-1", token));
        failure.Message.ShouldBe("sdk-historical-private-recovery-reconciliation-required");
        batches.ShouldBe(0);
        store.Count.ShouldBe(count);
        foreach (var entry in before) store.SnapshotRaw(StoreName, entry.Key).ShouldBe(entry.Value);
    }

    [Fact]
    public async Task ErasureRetiresSdkPrivateCanonicalManifestAfterCrashBeforeFirstParticipantAsync()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new InMemoryReadModelStore();
        ProjectionRequest history = History() with { Events = History().Events.Take(128).ToArray() };
        await SeedLegacyAsync(store, history, token);
        var current = await store.GetAsync<PartyDetailSdkReadModel>(StoreName, s_detailKey, token);
        var processing = await store.GetAsync<PartyProcessingSdkReadModel>(StoreName, s_processingKey, token);
        var scope = new ReadModelBatchScope(StoreName, "tenant-a", "party", "party-1", "party-detail", "private-preparation-crash");
        var batch = new ReadModelBatch(scope,
            [ReadModelBatchOperation.Write(s_detailKey, current.Value!, ReadModelBatchConcurrency.Match(current.ETag!)),
             ReadModelBatchOperation.Write(s_processingKey, processing.Value!, ReadModelBatchConcurrency.Match(processing.ETag!))])
             { RetainManifestForRecovery = true, DiscoverableRecovery = true };
        store.BatchFaultHook = (phase, _, _) => phase == ReadModelBatchPhase.ManifestPrepared
            ? Task.FromException(new IOException("crash before marker/participant")) : Task.CompletedTask;
        (await store.ExecuteAsync(batch, token)).IsSuccess.ShouldBeFalse();
        string manifest = scope.ComputeMarkerKey() + ":manifest";
        store.SnapshotRaw(StoreName, manifest).ShouldNotBeNull();
        store.HasPendingEnvelope(StoreName, s_detailKey).ShouldBeFalse();
        store.BatchFaultHook = null;
        await new PartySdkReadModelEraser(store, store, s_options).EraseAsync("tenant-a", "party-1", token);
        System.Text.Encoding.UTF8.GetString(store.SnapshotRaw(StoreName, manifest)!).ShouldBe("{\"retiredRecoveryManifest\":true}");
        System.Text.Encoding.UTF8.GetString(store.SnapshotRaw(StoreName, scope.ComputeMarkerKey())!).ShouldNotContain("Legacy");
        (await store.ExecuteAsync(batch, token)).IsSuccess.ShouldBeFalse();
        (await store.GetAsync<PartyDetailSdkReadModel>(StoreName, s_detailKey, token)).Value!.Detail!.PersonDetails.ShouldBeNull();
        (await store.GetAsync<PartyProcessingSdkReadModel>(StoreName, s_processingKey, token)).Value!.RecordCount.ShouldBe(128);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InterruptedLargeMigrationOrDispatcherRebuildResumesPersistedBucketsBeforeAuthorityCutoverAsync(bool rebuild)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var store = new InMemoryReadModelStore();
        ProjectionRequest legacy = History();
        await SeedLegacyAsync(store, legacy, token);
        byte[] detailBefore = store.SnapshotRaw(StoreName, s_detailKey)!;
        byte[] processingBefore = store.SnapshotRaw(StoreName, s_processingKey)!;
        ProjectionRequest expected = rebuild ? legacy : Append(legacy, new PartyReactivated());
        ProjectionRequest delivery = rebuild ? expected : expected with
        {
            Events = [expected.Events[^1]]
        };
        var firstBatches = new List<ReadModelBatch>();
        using var interrupted = CancellationTokenSource.CreateLinkedTokenSource(token);
        int groups = 0;
        IReadModelBatchStore firstRecorder = Recorder(store, firstBatches, (batch, cancellation) =>
        {
            if (IsGeneration(batch) && ++groups == 3)
            {
                interrupted.Cancel();
                cancellation.ThrowIfCancellationRequested();
            }
            return Task.CompletedTask;
        });
        var firstHandler = new PartyDetailSdkProjectionHandler(store, firstRecorder, s_options);

        await Should.ThrowAsync<OperationCanceledException>(() => ExecuteAsync(
            firstHandler, firstRecorder, delivery, "interrupted-large", rebuild, interrupted.Token));

        firstBatches.Count(IsGeneration).ShouldBe(3);
        string generation = Generation(firstBatches.First(IsGeneration));
        string firstKey = BucketKey(generation, 0);
        byte[] firstBucket = store.SnapshotRaw(StoreName, firstKey).ShouldNotBeNull();
        (await store.GetAsync<PartyProcessingBucket>(StoreName, firstKey, token)).Value.ShouldNotBeNull();
        (await store.GetAsync<PartyProcessingBucket>(StoreName,
            BucketKey(generation, PartyProcessingBucket.Number(expected.Events[^1].SequenceNumber)), token)).Value.ShouldBeNull();
        store.SnapshotRaw(StoreName, s_detailKey).ShouldBe(detailBefore);
        store.SnapshotRaw(StoreName, s_processingKey).ShouldBe(processingBefore);
        (await store.GetAsync<PartyProcessingSdkReadModel>(StoreName, s_processingKey, token)).Value!
            .Records.Count.ShouldBe(LegacyCount);

        // Recreate the handler, recorder and dispatcher service provider over the retained byte store.
        // The first two immutable group manifests and their already-written buckets survive this restart.
        var resumedBatches = new List<ReadModelBatch>();
        IReadModelBatchStore resumedRecorder = Recorder(store, resumedBatches);
        var restarted = new PartyDetailSdkProjectionHandler(store, resumedRecorder, s_options);
        (await ExecuteAsync(restarted, resumedRecorder, delivery, "interrupted-large", rebuild, token))
            .ShouldBe(ProjectionDispatchStatus.Completed);

        store.SnapshotRaw(StoreName, firstKey).ShouldBe(firstBucket);
        resumedBatches.Where(IsGeneration).Select(batch => batch.Scope.BatchId)
            .Take(2).ShouldBe(firstBatches.Where(IsGeneration).Take(2).Select(batch => batch.Scope.BatchId));
        resumedBatches.Where(IsGeneration).ShouldAllBe(batch => batch.Operations.Count <= 32);
        resumedBatches.Last().Operations.Select(operation => operation.Key).Order(StringComparer.Ordinal)
            .ShouldBe(new[] { s_detailKey, s_processingKey }.Order(StringComparer.Ordinal));
        var selected = (await store.GetAsync<PartyProcessingSdkReadModel>(StoreName, s_processingKey, token)).Value!;
        selected.BucketGeneration.ShouldBe(generation);
        selected.Records.ShouldBeEmpty();
        selected.RecordCount.ShouldBe(expected.Events.Length);
        selected.LastSequenceNumber.ShouldBe(expected.Events[^1].SequenceNumber);
        await AssertCompleteGenerationAsync(store, generation, expected, token);
        var detail = (await store.GetAsync<PartyDetailSdkReadModel>(StoreName, s_detailKey, token)).Value!;
        JsonSerializer.Serialize(detail, PartiesJsonOptions.Default)
            .ShouldBe(JsonSerializer.Serialize(PartyDetailSdkProjectionHandler.Fold(expected, null), PartiesJsonOptions.Default));
        store.BatchOptions.MaxOperations.ShouldBe(100);
        store.BatchOptions.MaxCanonicalManifestBytes.ShouldBe(1_048_576);
    }

    [Fact]
    public async Task InterruptedLegacyAuditErasureResumesItsPersistedGenerationAcrossEraserRecreationAsync()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var store = new InMemoryReadModelStore();
        ProjectionRequest legacy = History();
        await SeedLegacyAsync(store, legacy, token);
        byte[] detailBefore = store.SnapshotRaw(StoreName, s_detailKey)!;
        byte[] processingBefore = store.SnapshotRaw(StoreName, s_processingKey)!;
        var firstBatches = new List<ReadModelBatch>();
        using var interrupted = CancellationTokenSource.CreateLinkedTokenSource(token);
        int groups = 0;
        IReadModelBatchStore recorder = Recorder(store, firstBatches, (batch, cancellation) =>
        {
            if (IsGeneration(batch) && ++groups == 3)
            {
                interrupted.Cancel();
                cancellation.ThrowIfCancellationRequested();
            }
            return Task.CompletedTask;
        });
        await Should.ThrowAsync<OperationCanceledException>(() =>
            new PartySdkReadModelEraser(store, recorder, s_options).EraseAsync("tenant-a", "party-1", interrupted.Token));
        store.SnapshotRaw(StoreName, s_detailKey).ShouldBe(detailBefore);
        store.SnapshotRaw(StoreName, s_processingKey).ShouldBe(processingBefore);
        string generation = Generation(firstBatches.First(IsGeneration));
        byte[] firstBucket = store.SnapshotRaw(StoreName, BucketKey(generation, 0)).ShouldNotBeNull();
        (await store.GetAsync<PartyProcessingBucket>(StoreName,
            BucketKey(generation, PartyProcessingBucket.Number(LegacyCount)), token)).Value.ShouldBeNull();

        var resumedBatches = new List<ReadModelBatch>();
        IReadModelBatchStore resumedRecorder = Recorder(store, resumedBatches);
        await new PartySdkReadModelEraser(store, resumedRecorder, s_options).EraseAsync("tenant-a", "party-1", token);

        resumedBatches.Where(IsGeneration).Take(2).Select(batch => batch.Scope.BatchId)
            .ShouldBe(firstBatches.Where(IsGeneration).Take(2).Select(batch => batch.Scope.BatchId));
        store.SnapshotRaw(StoreName, BucketKey(generation, 0)).ShouldBe(firstBucket);
        var processing = (await store.GetAsync<PartyProcessingSdkReadModel>(StoreName, s_processingKey, token)).Value!;
        processing.BucketGeneration.ShouldBe(generation);
        processing.ErasureSequenceNumber.ShouldBe(LegacyCount);
        processing.RecordCount.ShouldBe(LegacyCount);
        processing.Records.ShouldBeEmpty();
        await AssertCompleteGenerationAsync(store, generation, legacy, token);
        var detail = (await store.GetAsync<PartyDetailSdkReadModel>(StoreName, s_detailKey, token)).Value!;
        detail.ErasureSequenceNumber.ShouldBe(LegacyCount);
        detail.Detail!.PersonDetails.ShouldBeNull();
        resumedBatches.Last().Operations.Count.ShouldBe(3);
        resumedBatches.Where(IsGeneration).ShouldAllBe(batch => batch.Operations.Count <= 32);
        store.BatchOptions.MaxOperations.ShouldBe(100);
        store.BatchOptions.MaxCanonicalManifestBytes.ShouldBe(1_048_576);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CompetingAppendOrErasureInvalidatesLargeMigrationOrDispatcherRebuildPromotionAsync(bool rebuild, bool erase)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var store = new InMemoryReadModelStore();
        ProjectionRequest legacy = History();
        await SeedLegacyAsync(store, legacy, token);
        ProjectionRequest candidate = rebuild ? legacy : Append(legacy, new PartyDeactivated());
        ProjectionRequest delivery = rebuild ? candidate : candidate with
        {
            Events = [candidate.Events[^1]]
        };
        ProjectionRequest winner = Append(legacy, new PartyDisplayNameDerived
        {
            DisplayName = "Concurrent winner",
            SortName = "Winner, Concurrent",
        });
        var batches = new List<ReadModelBatch>();
        byte[]? winningDetail = null;
        byte[]? winningProcessing = null;
        bool competed = false;
        IReadModelBatchStore recorder = Recorder(store, batches, async (batch, cancellation) =>
        {
            if (competed || IsGeneration(batch))
            {
                return;
            }
            competed = true;
            batch.Operations.Count.ShouldBe(2);
            batch.Operations.ShouldAllBe(operation => operation.Concurrency.Mode == ReadModelBatchConcurrencyMode.ExpectedETag);
            string stagedGeneration = Generation(batches.First(IsGeneration));
            await AssertCompleteGenerationAsync(store, stagedGeneration, candidate, cancellation).ConfigureAwait(false);
            (await store.GetAsync<PartyProcessingSdkReadModel>(StoreName, s_processingKey, cancellation).ConfigureAwait(false)).Value!
                .BucketGeneration.ShouldBeNull();
            if (erase)
            {
                await new PartySdkReadModelEraser(store, store, s_options).EraseAsync("tenant-a", "party-1", cancellation).ConfigureAwait(false);
            }
            else
            {
                var competing = new PartyDetailSdkProjectionHandler(store, store, s_options);
                (await competing.ProjectAsync(winner with
                {
                    Events = [winner.Events[^1]]
                }, "concurrent-append", cancellation).ConfigureAwait(false))
                    .Status.ShouldBe(ProjectionDispatchStatus.Completed);
            }
            winningDetail = store.SnapshotRaw(StoreName, s_detailKey).ShouldNotBeNull();
            winningProcessing = store.SnapshotRaw(StoreName, s_processingKey).ShouldNotBeNull();
        });
        using (var interrupted = CancellationTokenSource.CreateLinkedTokenSource(token))
        {
            int groups = 0;
            IReadModelBatchStore firstRecorder = Recorder(store, [], (batch, cancellation) =>
            {
                if (IsGeneration(batch) && ++groups == 3)
                {
                    interrupted.Cancel();
                    cancellation.ThrowIfCancellationRequested();
                }
                return Task.CompletedTask;
            });
            var firstHandler = new PartyDetailSdkProjectionHandler(store, firstRecorder, s_options);
            await Should.ThrowAsync<OperationCanceledException>(() => ExecuteAsync(
                firstHandler, firstRecorder, delivery, "losing-large", rebuild, interrupted.Token));
            (await store.GetAsync<PartyProcessingSdkReadModel>(StoreName, s_processingKey, token)).Value!
                .BucketGeneration.ShouldBeNull();
        }
        var handler = new PartyDetailSdkProjectionHandler(store, recorder, s_options);

        (await ExecuteAsync(handler, recorder, delivery, "losing-large", rebuild, token))
            .ShouldBe(ProjectionDispatchStatus.Retryable);

        competed.ShouldBeTrue();
        store.SnapshotRaw(StoreName, s_detailKey).ShouldBe(winningDetail);
        store.SnapshotRaw(StoreName, s_processingKey).ShouldBe(winningProcessing);
        string rejectedGeneration = Generation(batches.First(IsGeneration));
        await AssertCompleteGenerationAsync(store, rejectedGeneration, candidate, token);
        var selected = (await store.GetAsync<PartyProcessingSdkReadModel>(StoreName, s_processingKey, token)).Value!;
        selected.BucketGeneration.ShouldNotBe(rejectedGeneration);
        if (erase)
        {
            var detail = (await store.GetAsync<PartyDetailSdkReadModel>(StoreName, s_detailKey, token)).Value!;
            detail.ErasureSequenceNumber.ShouldBe(LegacyCount);
            detail.Detail!.PersonDetails.ShouldBeNull();
            selected.ErasureSequenceNumber.ShouldBe(LegacyCount);
            selected.Records.ShouldBeEmpty();
            selected.RecordCount.ShouldBe(LegacyCount);
            await AssertCompleteGenerationAsync(store, selected.BucketGeneration!, legacy, token);
        }
        else
        {
            (await store.GetAsync<PartyDetailSdkReadModel>(StoreName, s_detailKey, token)).Value!
                .Detail!.DisplayName.ShouldBe("Concurrent winner");
            await AssertCompleteGenerationAsync(store, selected.BucketGeneration!, winner, token);
        }

        // Snapshot-bound retry may admit safe checkpoint advancement after erasure, while it must
        // never select the abandoned generation, alter retained facts, or restore private values.
        var retainedBuckets = new Dictionary<string, byte[]>();
        if (erase && !rebuild)
        {
            for (long number = selected.FirstBucket!.Value; number <= selected.LastBucket; number++)
            {
                string bucketKey = BucketKey(selected.BucketGeneration!, number);
                retainedBuckets[bucketKey] = store.SnapshotRaw(StoreName, bucketKey)!;
            }
        }
        var restarted = new PartyDetailSdkProjectionHandler(store, store, s_options);
        if (rebuild)
        {
            (await ExecuteAsync(restarted, store, delivery, "losing-large", true, token))
                .ShouldNotBe(ProjectionDispatchStatus.Completed);
        }
        else
        {
            _ = await restarted.ProjectAsync(delivery, "losing-large", token);
        }
        if (erase && !rebuild)
        {
            long head = delivery.Events.Max(item => item.SequenceNumber);
            string version = head.ToString(System.Globalization.CultureInfo.InvariantCulture);
            DateTimeOffset sourceTime = delivery.Events.Max(item => item.Timestamp).ToUniversalTime();
            var expectedDetail = JsonSerializer.Deserialize<PartyDetailSdkReadModel>(winningDetail!, JsonSerializerOptions.Web)!;
            var expectedProcessing = JsonSerializer.Deserialize<PartyProcessingSdkReadModel>(winningProcessing!, JsonSerializerOptions.Web)!;
            expectedDetail = expectedDetail with
            {
                LastSequenceNumber = head, ProjectionVersion = version,
                ProjectedAt = expectedDetail.ProjectedAt > sourceTime ? expectedDetail.ProjectedAt : sourceTime
            };
            expectedProcessing = expectedProcessing with
            {
                LastSequenceNumber = head, ProjectionVersion = version,
                ProjectedAt = expectedProcessing.ProjectedAt > sourceTime ? expectedProcessing.ProjectedAt : sourceTime
            };
            var actualDetail = (await store.GetAsync<PartyDetailSdkReadModel>(StoreName, s_detailKey, token)).Value!;
            var actualProcessing = (await store.GetAsync<PartyProcessingSdkReadModel>(StoreName, s_processingKey, token)).Value!;
            JsonSerializer.Serialize(actualDetail, JsonSerializerOptions.Web).ShouldBe(JsonSerializer.Serialize(expectedDetail, JsonSerializerOptions.Web));
            JsonSerializer.Serialize(actualProcessing, JsonSerializerOptions.Web).ShouldBe(JsonSerializer.Serialize(expectedProcessing, JsonSerializerOptions.Web));
            actualDetail.Detail!.PersonDetails.ShouldBeNull();
            JsonSerializer.Serialize(actualDetail).ShouldNotContain("Legacy");
            foreach (var bucket in retainedBuckets) store.SnapshotRaw(StoreName, bucket.Key).ShouldBe(bucket.Value);
            await AssertCompleteGenerationAsync(store, selected.BucketGeneration!, legacy, token);
        }
        else
        {
            store.SnapshotRaw(StoreName, s_detailKey).ShouldBe(winningDetail);
            store.SnapshotRaw(StoreName, s_processingKey).ShouldBe(winningProcessing);
        }
        if (!erase)
        {
            // A newly planned rebuild including the winner remains possible after the rejected cutover.
            (await ExecuteAsync(restarted, store, winner, "rebuild-current-authority", true, token))
                .ShouldBe(ProjectionDispatchStatus.Completed);
            var recovered = (await store.GetAsync<PartyProcessingSdkReadModel>(StoreName, s_processingKey, token)).Value!;
            await AssertCompleteGenerationAsync(store, recovered.BucketGeneration!, winner, token);
            (await store.GetAsync<PartyDetailSdkReadModel>(StoreName, s_detailKey, token)).Value!
                .Detail!.DisplayName.ShouldBe("Concurrent winner");
        }
    }

    [Theory]
    [InlineData(40)]
    [InlineData(96)]
    public async Task UnresolvedAuditRetriesSameDispatchAndResolvesWithoutCheckpointDriftAsync(int count)
    {
        var token = TestContext.Current.CancellationToken;
        var store = new InMemoryReadModelStore();
        var original = History() with { Events = History().Events.Take(count).ToArray() };
        var unresolved = original with { Events = original.Events.Select(item => item.SequenceNumber == count / 2
            ? item with { EventTypeName = "FutureUnresolvedType" } : item).ToArray() };
        string id = new('d', 512);
        var handler = new PartyDetailSdkProjectionHandler(store, store, s_options);
        (await handler.ProjectAsync(unresolved, id, token)).Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        var before = (await store.GetAsync<PartyProcessingSdkReadModel>(StoreName, s_processingKey, token)).Value!;
        before.RecordCount.ShouldBe(count);
        before.LastSequenceNumber.ShouldBe(long.MinValue);
        (await store.GetAsync<PartyDetailSdkReadModel>(StoreName, s_detailKey, token)).Value!.LastSequenceNumber.ShouldBe(long.MinValue);
        byte[] canonical = store.SnapshotRaw(StoreName, s_processingKey)!;
        handler = new PartyDetailSdkProjectionHandler(store, store, s_options);
        (await handler.ProjectAsync(unresolved, id, token)).Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        store.SnapshotRaw(StoreName, s_processingKey).ShouldBe(canonical);
        (await handler.ProjectAsync(original, id, token)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        var resolved = (await store.GetAsync<PartyProcessingSdkReadModel>(StoreName, s_processingKey, token)).Value!;
        resolved.LastSequenceNumber.ShouldBe(count);
        (await store.GetAsync<PartyDetailSdkReadModel>(StoreName, s_detailKey, token)).Value!.LastSequenceNumber.ShouldBe(count);
        await AssertCompleteGenerationAsync(store, resolved.BucketGeneration!, original, token);
        var conflicting = original with { Events = [original.Events[0], original.Events[0] with { EventTypeName = "Conflicting" }] };
        (await handler.ProjectAsync(conflicting, "conflicting-replay", token)).ReasonCode.ShouldBe("conflicting-duplicate-event");
    }

    [Theory]
    [InlineData(ReadModelBatchPhase.MarkerPrepared)]
    [InlineData(ReadModelBatchPhase.AfterInstallOperation)]
    [InlineData(ReadModelBatchPhase.BeforeCommit)]
    [InlineData(ReadModelBatchPhase.AfterCommit)]
    [InlineData(ReadModelBatchPhase.BeforeCompaction)]
    public async Task LargeAuditRecoversPendingCanonicalEnvelopesAcrossHandlerRestartAsync(ReadModelBatchPhase failurePhase)
    {
        var token = TestContext.Current.CancellationToken;
        var store = new InMemoryReadModelStore();
        var complete = History() with { Events = History().Events.Take(136).ToArray() };
        var initial = complete with { Events = complete.Events.Take(40).ToArray() };
        var handler = new PartyDetailSdkProjectionHandler(store, store, s_options);
        (await handler.ProjectAsync(initial, "initial-buckets", token)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        var tail = complete with { Events = complete.Events.Skip(40).ToArray() };
        var unresolved = tail with { Events = tail.Events.Select(item => item.SequenceNumber == 70
            ? item with { EventTypeName = "FutureUnresolvedType" } : item).ToArray() };
        store.BatchFaultHook = (phase, _, _) => phase == failurePhase || phase == ReadModelBatchPhase.Reconcile
            ? throw new IOException("audit persistence interrupted") : Task.CompletedTask;
        (await handler.ProjectAsync(unresolved, "same-audit", token)).Status.ShouldBe(ProjectionDispatchStatus.Indeterminate);
        if (failurePhase is ReadModelBatchPhase.AfterCommit or ReadModelBatchPhase.BeforeCompaction)
            store.HasPendingEnvelope(StoreName, s_processingKey).ShouldBeTrue();
        store.BatchFaultHook = null;
        handler = new PartyDetailSdkProjectionHandler(store, store, s_options);
        (await handler.ProjectAsync(unresolved, "same-audit", token)).Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        store.HasPendingEnvelope(StoreName, s_processingKey).ShouldBeFalse();
        (await store.GetAsync<PartyProcessingSdkReadModel>(StoreName, s_processingKey, token)).Value!.LastSequenceNumber.ShouldBe(40);
        (await store.GetAsync<PartyDetailSdkReadModel>(StoreName, s_detailKey, token)).Value!.LastSequenceNumber.ShouldBe(40);
        (await handler.ProjectAsync(tail, "same-audit", token)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        var model = (await store.GetAsync<PartyProcessingSdkReadModel>(StoreName, s_processingKey, token)).Value!;
        await AssertCompleteGenerationAsync(store, model.BucketGeneration!, complete, token);
    }

    [Fact]
    public async Task RedeliveredValidPrefixRepairsLaggingDetailBeforeAcknowledgementAsync()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new InMemoryReadModelStore();
        var history = History() with { Events = History().Events.Take(40).ToArray() };
        var handler = new PartyDetailSdkProjectionHandler(store, store, s_options);
        (await handler.ProjectAsync(history, "initial", token)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        await store.SaveAsync(StoreName, s_detailKey, PartyDetailSdkProjectionHandler.Fold(history with { Events = history.Events.Take(10).ToArray() }, null), token);
        var prefix = history with { Events = history.Events.Take(20).ToArray() };
        (await handler.ProjectAsync(prefix, "repair-detail", token)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await store.GetAsync<PartyDetailSdkReadModel>(StoreName, s_detailKey, token)).Value!.LastSequenceNumber.ShouldBe(20);
        (await store.GetAsync<PartyProcessingSdkReadModel>(StoreName, s_processingKey, token)).Value!.LastSequenceNumber.ShouldBe(40);
    }

    [Fact]
    public async Task CompetingInlineAppendAtCutoverUsesNewSnapshotForStagingAndPromotionAsync()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new InMemoryReadModelStore();
        var history = History() with { Events = History().Events.Take(33).ToArray() };
        await SeedLegacyAsync(store, history with { Events = history.Events.Take(30).ToArray() }, token);
        var batches = new List<ReadModelBatch>();
        bool competed = false;
        var recorder = Recorder(store, batches, async (batch, cancellation) =>
        {
            if (IsGeneration(batch) || competed) return;
            competed = true;
            var inline = new PartyDetailSdkProjectionHandler(store, store, s_options);
            (await inline.ProjectAsync(history with { Events = [history.Events[30]] }, "competing-inline", cancellation).ConfigureAwait(false))
                .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        });
        var delivery = history with { Events = history.Events.Skip(30).ToArray() };
        var handler = new PartyDetailSdkProjectionHandler(store, recorder, s_options);
        (await handler.ProjectAsync(delivery, "same-cutover", token)).Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        string abandoned = Generation(batches.First(IsGeneration));
        batches.Clear();
        handler = new PartyDetailSdkProjectionHandler(store, recorder, s_options);
        (await handler.ProjectAsync(delivery, "same-cutover", token)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        string selected = Generation(batches.First(IsGeneration));
        selected.ShouldNotBe(abandoned);
        (await store.GetAsync<PartyProcessingSdkReadModel>(StoreName, s_processingKey, token)).Value!.BucketGeneration.ShouldBe(selected);
        await AssertCompleteGenerationAsync(store, selected, history, token);
    }

    [Fact]
    public async Task RebuildCannotReplaceProcessingCheckpointWhenDetailLagsAsync()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new InMemoryReadModelStore();
        var history = History() with { Events = History().Events.Take(40).ToArray() };
        await SeedLegacyAsync(store, history, token);
        await store.SaveAsync(StoreName, s_detailKey, PartyDetailSdkProjectionHandler.Fold(history with { Events = history.Events.Take(10).ToArray() }, null), token);
        byte[] before = store.SnapshotRaw(StoreName, s_processingKey)!;
        var handler = new PartyDetailSdkProjectionHandler(store, store, s_options);
        var error = await Should.ThrowAsync<InvalidOperationException>(() => handler.PrepareRebuildAsync(
            history with { Events = history.Events.Take(20).ToArray() }, "stale-rebuild", token));
        error.Message.ShouldBe("processing-rebuild-would-replace-newer-authority");
        store.SnapshotRaw(StoreName, s_processingKey).ShouldBe(before);
    }

    [Theory]
    [InlineData("scope")]
    [InlineData("capacity")]
    [InlineData("order")]
    [InlineData("duplicate")]
    [InlineData("coverage")]
    [InlineData("metadata")]
    public async Task CorruptTouchedBucketCannotAdvanceEitherCanonicalAuthorityAsync(string defect)
    {
        var token = TestContext.Current.CancellationToken;
        var store = new InMemoryReadModelStore();
        var history = History() with { Events = History().Events.Take(40).ToArray() };
        var handler = new PartyDetailSdkProjectionHandler(store, store, s_options);
        (await handler.ProjectAsync(history, "seed-buckets", token)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        var metadata = (await store.GetAsync<PartyProcessingSdkReadModel>(StoreName, s_processingKey, token)).Value!;
        string key = BucketKey(metadata.BucketGeneration!, 1);
        var bucket = (await store.GetAsync<PartyProcessingBucket>(StoreName, key, token)).Value!;
        var records = bucket.Records.ToArray();
        records = defect switch
        {
            "scope" => records.Select(item => item with { TenantId = "foreign" }).ToArray(),
            "capacity" => Enumerable.Repeat(records[0], 33).ToArray(),
            "order" => records.Reverse().ToArray(),
            "duplicate" => [records[0], .. records.Skip(1).Select(_ => records[0])],
            "coverage" => records.Skip(1).ToArray(),
            _ => records
        };
        await store.SaveAsync(StoreName, key, bucket with { Records = records }, token);
        if (defect == "metadata") await store.SaveAsync(StoreName, s_processingKey, metadata with { LastRecordSequenceNumber = null }, token);
        byte[] beforeDetail = store.SnapshotRaw(StoreName, s_detailKey)!;
        byte[] beforeProcessing = store.SnapshotRaw(StoreName, s_processingKey)!;
        byte[] beforeBucket = store.SnapshotRaw(StoreName, key)!;
        var result = await handler.ProjectAsync(Append(history, new PartyReactivated()) with { Events = [Event(new PartyReactivated(), 41)] }, "corrupt-append", token);
        result.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        store.SnapshotRaw(StoreName, s_detailKey).ShouldBe(beforeDetail);
        store.SnapshotRaw(StoreName, s_processingKey).ShouldBe(beforeProcessing);
        store.SnapshotRaw(StoreName, key).ShouldBe(beforeBucket);
    }

    private static async Task SeedLegacyAsync(InMemoryReadModelStore store, ProjectionRequest history, CancellationToken token)
    {
        await store.SaveAsync(StoreName, s_detailKey, PartyDetailSdkProjectionHandler.Fold(history, null), token).ConfigureAwait(false);
        await store.SaveAsync(StoreName, s_processingKey, PartyProcessingActivityFold.Fold(history, null), token).ConfigureAwait(false);
    }

    private static async Task AssertCompleteGenerationAsync(
        InMemoryReadModelStore store, string generation, ProjectionRequest history, CancellationToken token)
    {
        var records = new List<ProcessingActivityRecord>();
        for (long number = 0; number <= PartyProcessingBucket.Number(history.Events[^1].SequenceNumber); number++)
        {
            PartyProcessingBucket bucket = (await store.GetAsync<PartyProcessingBucket>(StoreName, BucketKey(generation, number), token).ConfigureAwait(false))
                .Value.ShouldNotBeNull();
            bucket.Records.Count.ShouldBeLessThanOrEqualTo(PartyProcessingBucket.Capacity);
            records.AddRange(bucket.Records);
        }
        records.Select(record => record.SequenceNumber).ShouldBe(history.Events.Select(item => item.SequenceNumber));
        JsonSerializer.Serialize(records, PartiesJsonOptions.Default)
            .ShouldBe(JsonSerializer.Serialize(PartyProcessingActivityFold.Fold(history, null).Records, PartiesJsonOptions.Default));
    }

    private static IReadModelBatchStore Recorder(InMemoryReadModelStore store, List<ReadModelBatch> batches,
        Func<ReadModelBatch, CancellationToken, Task>? beforeExecute = null)
    {
        IReadModelBatchStore recorder = Substitute.For<IReadModelBatchStore, IReadModelPendingBatchRecoveryStore>();
        recorder.ExecuteAsync(Arg.Any<ReadModelBatch>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            ReadModelBatch batch = call.Arg<ReadModelBatch>();
            CancellationToken token = call.Arg<CancellationToken>();
            batches.Add(batch);
            if (beforeExecute is not null)
            {
                await beforeExecute(batch, token).ConfigureAwait(false);
            }
            return await store.ExecuteAsync(batch, token).ConfigureAwait(false);
        });
        ((IReadModelPendingBatchRecoveryStore)recorder).RecoverPendingAsync(
            Arg.Any<ReadModelBatchScope>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
                store.RecoverPendingAsync(call.Arg<ReadModelBatchScope>(), call.Arg<string>(), call.Arg<CancellationToken>()));
        ((IReadModelPendingBatchRecoveryStore)recorder).RecoverNonExpiringPendingAsync(
            Arg.Any<ReadModelBatchScope>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
                store.RecoverNonExpiringPendingAsync(call.Arg<ReadModelBatchScope>(), call.Arg<string>(), call.Arg<CancellationToken>()));
        return recorder;
    }

    private static async Task<ProjectionDispatchStatus> ExecuteAsync(PartyDetailSdkProjectionHandler handler,
        IReadModelBatchStore batches, ProjectionRequest request, string operationId, bool rebuild, CancellationToken token)
    {
        if (!rebuild)
        {
            return (await handler.ProjectAsync(request, operationId, token).ConfigureAwait(false)).Status;
        }
        using ServiceProvider services = new ServiceCollection()
            .AddSingleton<IAsyncDomainProjectionHandler>(handler)
            .AddSingleton(batches).BuildServiceProvider();
        string fingerprint = ProjectionRouteCatalogFingerprint.Compute("parties-generation-tests", "v1",
            [new ProjectionDispatchRoute(handler.Domain, handler.ProjectionType)]);
        ProjectionDispatchResponse response = await DomainProjectionDispatcher.RebuildAsync(services,
            new ProjectionDispatchRequest(request, [handler.ProjectionType], operationId, fingerprint),
            new ProjectionDispatchOptions(),
            new DomainProjectionIdentityOptions { AppId = "parties-generation-tests", ServiceVersion = "v1" }, token).ConfigureAwait(false);
        return response.Outcomes.ShouldHaveSingleItem().Status;
    }

    private static bool IsGeneration(ReadModelBatch batch)
        => batch.Scope.ProjectionType.EndsWith(".generation", StringComparison.Ordinal);

    private static string Generation(ReadModelBatch batch)
        => batch.Operations[0].Key[(s_processingKey.Length + 1)..batch.Operations[0].Key.LastIndexOf('.')];

    private static string BucketKey(string generation, long number)
        => PartySdkReadModelAddresses.ProcessingBucket("tenant-a", "party-1", generation, number);

    private static ProjectionRequest Append(ProjectionRequest history, object payload)
        => history with
        {
            Events = [.. history.Events, Event(payload, history.Events[^1].SequenceNumber + 1)]
        };

    private static ProjectionRequest History()
        => new("tenant-a", "party", "party-1",
            [Event(new PartyCreated { Type = PartyType.Person, PersonDetails = new PersonDetails { FirstName = "Legacy", LastName = "Owner" } }, 1),
                .. Enumerable.Range(2, LegacyCount - 1).Select(sequence => Event(new PartyDeactivated(), sequence))]);

    private static ProjectionEventDto Event(object payload, long sequence)
        => new(payload.GetType().Name, JsonSerializer.SerializeToUtf8Bytes(payload, payload.GetType(), PartiesJsonOptions.Default),
            "json", sequence, DateTimeOffset.UnixEpoch.AddSeconds(sequence), "correlation");
}
#endif
