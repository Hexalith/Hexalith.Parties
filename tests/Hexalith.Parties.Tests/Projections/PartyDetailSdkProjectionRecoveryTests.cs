using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Configuration;
using Hexalith.Parties.Projections.Handlers;
using Hexalith.Parties.Projections.Models;
using Hexalith.Parties.Projections.Services;

using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.Tests.Projections;

/// <summary>Exercises coordinated initialization against the SDK's serialized batch protocol.</summary>
public sealed class PartyDetailSdkProjectionRecoveryTests
{
    private const string StoreName = "statestore";
    private static readonly string s_detailKey = PartySdkReadModelAddresses.Detail("tenant-a", "party-1");
    private static readonly string s_processingKey = PartySdkReadModelAddresses.Processing("tenant-a", "party-1");
    private static readonly IOptions<PartySdkReadModelOptions> s_options =
        Options.Create(new PartySdkReadModelOptions { ReadModelStateStoreName = StoreName });

    /// <summary>Verifies repeated unresolved deliveries retain coordinated slots and recover when supported.</summary>
    /// <param name="dispatchId">The supported redelivery identity.</param>
    [Theory]
    [InlineData("original")]
    [InlineData("changed")]
    public async Task RepeatedUnresolvedDeliveryRetainsBothSlotsAndLaterSupportedDeliveryRecoversAsync(string dispatchId)
    {
        var store = new InMemoryReadModelStore();
        var batches = new List<ReadModelBatch>();
        PartyDetailSdkProjectionHandler handler = Handler(store, batches);
        for (int retry = 0; retry < 2; retry++)
        {
            DomainProjectionHandlerResult failed = await handler.ProjectAsync(Request(supported: false), "original", TestContext.Current.CancellationToken);
            failed.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
            failed.ReasonCode.ShouldBe("unresolved-or-unsupported-event");
            await AssertUnadvancedAuditAsync(store);
        }

        ReadModelBatch initialization = batches.ShouldHaveSingleItem();
        initialization.RetainManifestForRecovery.ShouldBeTrue();
        initialization.Scope.BatchId.ShouldNotBe("original");
        initialization.Scope.BatchId.Length.ShouldBeLessThan(ReadModelBatchScope.MaxComponentByteLength);
        initialization.Operations.ShouldAllBe(operation => operation.Concurrency == ReadModelBatchConcurrency.CreateOnly);
        Deserialize<PartyDetailSdkReadModel>(initialization, s_detailKey).LastSequenceNumber.ShouldBe(long.MinValue);
        Deserialize<PartyProcessingSdkReadModel>(initialization, s_processingKey).Records.ShouldBeEmpty();
        string oldDetailEtag = (await store.GetAsync<PartyDetailSdkReadModel>(StoreName, s_detailKey, TestContext.Current.CancellationToken)).ETag!;
        string oldProcessingEtag = (await store.GetAsync<PartyProcessingSdkReadModel>(StoreName, s_processingKey, TestContext.Current.CancellationToken)).ETag!;

        (await handler.ProjectAsync(Request(supported: true), dispatchId, TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        await AssertSupportedAsync(store);
        (await store.GetAsync<PartyDetailSdkReadModel>(StoreName, s_detailKey, TestContext.Current.CancellationToken)).ETag.ShouldNotBe(oldDetailEtag);
        (await store.GetAsync<PartyProcessingSdkReadModel>(StoreName, s_processingKey, TestContext.Current.CancellationToken)).ETag.ShouldNotBe(oldProcessingEtag);
    }

    /// <summary>Verifies interrupted empty initialization permits safe supported redelivery.</summary>
    /// <param name="interruption">The SDK phase at which the operation is interrupted.</param>
    /// <param name="operationOrdinal">The operation to interrupt, or a negative value for a batch phase.</param>
    /// <param name="changedDispatch">Whether redelivery uses a different identity.</param>
    [Theory]
    [InlineData(ReadModelBatchPhase.MarkerPrepared, -1, false)]
    [InlineData(ReadModelBatchPhase.MarkerPrepared, -1, true)]
    [InlineData(ReadModelBatchPhase.AfterInstallOperation, 0, false)]
    [InlineData(ReadModelBatchPhase.AfterInstallOperation, 0, true)]
    [InlineData(ReadModelBatchPhase.AfterInstallOperation, 1, true)]
    [InlineData(ReadModelBatchPhase.BeforeCommit, -1, true)]
    [InlineData(ReadModelBatchPhase.AfterCommit, -1, true)]
    [InlineData(ReadModelBatchPhase.BeforeCompaction, 0, true)]
    [InlineData(ReadModelBatchPhase.AfterCompaction, 0, true)]
    [InlineData(ReadModelBatchPhase.BeforeCompaction, 1, true)]
    [InlineData(ReadModelBatchPhase.RecoveryReceiptWritten, -1, true)]
    [InlineData(ReadModelBatchPhase.RecoveryManifestReclaimed, -1, true)]
    public async Task InterruptedInitializationResumesOriginalManifestBeforeSupportedFoldAsync(
        ReadModelBatchPhase interruption, int operationOrdinal, bool changedDispatch)
    {
        var store = new InMemoryReadModelStore();
        var batches = new List<ReadModelBatch>();
        PartyDetailSdkProjectionHandler handler = Handler(store, batches);
        bool interrupted = false;
        store.BatchFaultHook = (phase, ordinal, _) =>
        {
            if (phase == interruption && (operationOrdinal < 0 || ordinal == operationOrdinal))
            {
                interrupted = true;
                throw new IOException("initialization interrupted");
            }

            if (phase == ReadModelBatchPhase.Reconcile && interrupted)
            {
                throw new IOException("reconciliation unavailable");
            }

            return Task.CompletedTask;
        };
        (await handler.ProjectAsync(Request(supported: false), "original", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Indeterminate);
        interrupted.ShouldBeTrue();
        ReadModelBatch original = batches.ShouldHaveSingleItem();
        JsonObject? preparedMarker = store.Snapshot<JsonObject>(StoreName, original.Scope.ComputeMarkerKey());
        // These committed envelopes expose empty ETags until SDK recovery compacts them.
        if (interruption is ReadModelBatchPhase.AfterCommit or ReadModelBatchPhase.BeforeCompaction)
        {
            (await store.GetAsync<PartyProcessingSdkReadModel>(StoreName, s_processingKey, TestContext.Current.CancellationToken)).ETag.ShouldBeNullOrEmpty();
        }

        store.BatchFaultHook = null;
        (await handler.ProjectAsync(Request(supported: true), changedDispatch ? "changed" : "original", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        await AssertSupportedAsync(store);
        if (interruption == ReadModelBatchPhase.MarkerPrepared)
        {
            // With no participating envelope, support can change without completing the empty
            // initializer. Its immutable empty marker is safe to leave behind.
            store.Snapshot<JsonObject>(StoreName, original.Scope.ComputeMarkerKey())!.ToJsonString()
                .ShouldBe(preparedMarker!.ToJsonString());
        }
        else
        {
            (await store.ExecuteAsync(original, TestContext.Current.CancellationToken)).Status.ShouldBe(ReadModelBatchStatus.AlreadyCompleted);
        }
        await AssertSupportedAsync(store);
    }

    /// <summary>Verifies legacy first-create retries preserve their original batch contract.</summary>
    /// <param name="interruption">The SDK phase at which the operation is interrupted.</param>
    /// <param name="ordinal">The operation to interrupt, or a negative value for a batch phase.</param>
    [Theory]
    [InlineData(ReadModelBatchPhase.MarkerPrepared, -1)]
    [InlineData(ReadModelBatchPhase.AfterInstallOperation, 0)]
    [InlineData(ReadModelBatchPhase.AfterInstallOperation, 1)]
    [InlineData(ReadModelBatchPhase.BeforeCommit, -1)]
    [InlineData(ReadModelBatchPhase.AfterCommit, -1)]
    [InlineData(ReadModelBatchPhase.BeforeCompaction, 0)]
    public async Task LegacyFirstCreateRetryKeepsOriginalNonRetainedManifestAsync(ReadModelBatchPhase interruption, int ordinal)
    {
        var store = new InMemoryReadModelStore();
        var batches = new List<ReadModelBatch>();
        PartyDetailSdkProjectionHandler handler = Handler(store, batches);
        store.BatchFaultHook = (phase, operation, _) => (phase == interruption && (ordinal < 0 || operation == ordinal))
            || phase == ReadModelBatchPhase.Reconcile
                ? throw new IOException("supported dispatch interrupted")
                : Task.CompletedTask;
        (await handler.ProjectAsync(Request(supported: true), "original", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Indeterminate);
        ReadModelBatch original = batches.ShouldHaveSingleItem();
        original.Scope.ShouldBe(new ReadModelBatchScope(StoreName, "tenant-a", "party", "party-1", PartyProjectionNames.Detail, "original"));
        original.RetainManifestForRecovery.ShouldBeFalse();
        store.BatchFaultHook = null;
        DomainProjectionHandlerResult result = await handler.ProjectAsync(Request(supported: true), "original", TestContext.Current.CancellationToken);
        result.Status.ShouldBeOneOf(ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
        if (result.Status == ProjectionDispatchStatus.AlreadyCompleted)
        {
            (await store.GetAsync<PartyDetailSdkReadModel>(StoreName, s_detailKey, TestContext.Current.CancellationToken)).Value!.LastSequenceNumber.ShouldBe(1);
#if HEXALITH_EVENTSTORE_NONEXPIRING_RECOVERY
            // Source recovery has already compacted the original decision before checkpoint reads.
            await AssertCompactedAsync(store);
            (await store.ExecuteAsync(original, TestContext.Current.CancellationToken)).Status.ShouldBe(ReadModelBatchStatus.AlreadyCompleted);
#else
            // Published SDKs still require replay of the exact original batch to compact.
            (await store.ExecuteAsync(original, TestContext.Current.CancellationToken)).Status.ShouldBe(ReadModelBatchStatus.Completed);
#endif
        }
        batches.ShouldAllBe(batch => !batch.RetainManifestForRecovery);
        if (batches.Count > 1)
        {
            batches.Count.ShouldBe(2);
            batches[1].Scope.ShouldBe(original.Scope);
            for (int index = 0; index < original.Operations.Count; index++)
            {
                batches[1].Operations[index].CanonicalValue.ToArray().ShouldBe(original.Operations[index].CanonicalValue.ToArray());
                batches[1].Operations[index].Concurrency.ShouldBe(original.Operations[index].Concurrency);
            }
        }
        await AssertSupportedAsync(store);
    }

    /// <summary>Verifies initialization receipts cannot collide with a caller delivery identity.</summary>
    [Fact]
    public async Task InitializationNamespaceCannotCollideWithCallerDispatchIdentityAsync()
    {
        var store = new InMemoryReadModelStore();
        var batches = new List<ReadModelBatch>();
        PartyDetailSdkProjectionHandler handler = Handler(store, batches);
        (await handler.ProjectAsync(Request(supported: false), "original", TestContext.Current.CancellationToken))
            .ReasonCode.ShouldBe("unresolved-or-unsupported-event");
        ReadModelBatch initialization = batches.ShouldHaveSingleItem();
        string initializationMarkerKey = initialization.Scope.ComputeMarkerKey();
        JsonObject receipt = store.Snapshot<JsonObject>(StoreName, initializationMarkerKey).ShouldNotBeNull();
        (await handler.ProjectAsync(Request(supported: true), initialization.Scope.BatchId, TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        batches.Count.ShouldBe(2);
        ReadModelBatch normal = batches[1];
        normal.Scope.BatchId.ShouldBe(initialization.Scope.BatchId);
        normal.Scope.ProjectionType.ShouldNotBe(initialization.Scope.ProjectionType);
        normal.Scope.ComputeMarkerKey().ShouldNotBe(initializationMarkerKey);
        store.Snapshot<JsonObject>(StoreName, normal.Scope.ComputeMarkerKey()).ShouldNotBeNull();
        store.Snapshot<JsonObject>(StoreName, initializationMarkerKey)!.ToJsonString().ShouldBe(receipt.ToJsonString());
        await AssertSupportedAsync(store);
    }

    /// <summary>Recover a committed normal v1 delivery before reading checkpoints for the next event tail.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task CommittedLegacyDeliveryRecoversBeforeNextTailAndPreservesNewerErasureBytes(bool erased, bool interruptedRecovery)
    {
        Assert.SkipUnless(typeof(PartyDetailSdkProjectionRecoveryTests).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
            .OfType<System.Reflection.AssemblyMetadataAttribute>()
            .Any(attribute => attribute.Key == "HexalithSourceMappingMode" && attribute.Value == "source"),
            "Requires pinned-tool source qualification; EventStore 3.127.0 has no nonexpiring legacy recovery capability.");
        var store = new InMemoryReadModelStore();
        var batches = new List<ReadModelBatch>();
        PartyDetailSdkProjectionHandler handler = Handler(store, batches);
        store.BatchFaultHook = (phase, _, _) => phase is ReadModelBatchPhase.AfterCommit or ReadModelBatchPhase.Reconcile
            ? throw new IOException("legacy normal dispatch interrupted") : Task.CompletedTask;
        (await handler.ProjectAsync(Request(supported: true), "original", TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Indeterminate);
        ReadModelBatch original = batches.ShouldHaveSingleItem();
        original.RetainManifestForRecovery.ShouldBeFalse();
        store.BatchFaultHook = null;
        byte[]? erasedBytes = null;
        if (erased)
        {
            PartyDetailSdkReadModel current = (await store.GetAsync<PartyDetailSdkReadModel>(StoreName, s_detailKey, TestContext.Current.CancellationToken)).Value!;
            store.SeedRaw(StoreName, s_detailKey, PartySdkReadModelEraser.RedactDetail(current, "party-1", DateTimeOffset.UnixEpoch.AddDays(1)));
            erasedBytes = store.SnapshotRaw(StoreName, s_detailKey);
        }
        if (interruptedRecovery)
        {
            store.BatchFaultHook = (phase, _, _) => phase == ReadModelBatchPhase.RecoveryReceiptWritten
                ? throw new IOException("legacy recovery interrupted after content-free receipt") : Task.CompletedTask;
            (await Handler(store).ProjectAsync(Request(supported: true), "changed", TestContext.Current.CancellationToken))
                .ReasonCode.ShouldBe("projection-recovery-incomplete");
            store.BatchFaultHook = null;
        }
        // Lose all handler state while retaining the serialized protocol rows.
        DomainProjectionHandlerResult result = await Handler(store).ProjectAsync(Request(supported: true), "changed", TestContext.Current.CancellationToken);
        result.Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        store.HasPendingEnvelope(StoreName, s_detailKey).ShouldBeFalse();
        store.HasPendingEnvelope(StoreName, s_processingKey).ShouldBeFalse();
        (await store.GetAsync<PartyDetailSdkReadModel>(StoreName, s_detailKey, TestContext.Current.CancellationToken)).ETag.ShouldNotBeNullOrEmpty();
        if (erased) store.SnapshotRaw(StoreName, s_detailKey).ShouldBe(erasedBytes);
        else
        {
            await AssertSupportedAsync(store);
            ProjectionRequest tail = new("tenant-a", "party", "party-1", [Event(new PartyDeactivated(), 2)]);
            (await Handler(store).ProjectAsync(tail, "tail", TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
            store.Snapshot<PartyDetailSdkReadModel>(StoreName, s_detailKey)!.LastSequenceNumber.ShouldBe(2);
            store.Snapshot<PartyProcessingSdkReadModel>(StoreName, s_processingKey)!.LastSequenceNumber.ShouldBe(2);
            await AssertCompactedAsync(store);
        }
    }

    /// <summary>Pins the persisted empty-v1 payload format across future model changes.</summary>
    [Fact]
    public async Task EmptyV1InitializationKeepsPersistedCanonicalFormatAsync()
    {
        var store = new InMemoryReadModelStore();
        var batches = new List<ReadModelBatch>();
        (await Handler(store, batches).ProjectAsync(Request(supported: false), "original", TestContext.Current.CancellationToken))
            .ReasonCode.ShouldBe("unresolved-or-unsupported-event");
        ReadModelBatch initialization = batches.ShouldHaveSingleItem();
        // These independent literal bytes pin the persisted empty-v1 format. Changing either
        // serialized format requires an explicit initialization identity-version decision;
        // updating the fixtures alone could strand an older prepared manifest at this identity.
        initialization.Operations.Single(operation => operation.Key == s_detailKey).CanonicalValue.ToArray().ShouldBe(
            """{"detail":null,"erasedAt":null,"erasureSequenceNumber":null,"lastSequenceNumber":-9223372036854775808,"projectedAt":null,"projectionVersion":null}"""u8.ToArray());
        initialization.Operations.Single(operation => operation.Key == s_processingKey).CanonicalValue.ToArray().ShouldBe(
            """{"erasedAt":null,"erasureSequenceNumber":null,"lastSequenceNumber":-9223372036854775808,"projectedAt":null,"projectionVersion":null,"records":[]}"""u8.ToArray());
    }

    /// <summary>Verifies interrupted normal delivery adds no retained payload outside the erasure keys.</summary>
    [Fact]
    public async Task NormalPreInstallInterruptionLeavesNoRetainedPersonalDataAfterRealErasureAsync()
    {
        var store = new InMemoryReadModelStore();
        var batches = new List<ReadModelBatch>();
        PartyDetailSdkProjectionHandler handler = Handler(store, batches);
        const string privateName = "PrivateRecoveryCanary";
        ProjectionRequest request = Request(supported: true) with
        {
            Events = [Event(new PartyCreated { Type = PartyType.Person }, 1), Event(new PersonDetailsUpdated
            {
                PersonDetails = new PersonDetails { FirstName = privateName, LastName = "PrivateSurname" },
            }, 2)],
        };
        store.BatchFaultHook = (phase, _, _) => phase is ReadModelBatchPhase.MarkerPrepared or ReadModelBatchPhase.Reconcile
            ? throw new IOException("before any payload envelope") : Task.CompletedTask;
        (await handler.ProjectAsync(request, "original", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Indeterminate);
        ReadModelBatch original = batches.ShouldHaveSingleItem();
        original.RetainManifestForRecovery.ShouldBeFalse();
        JsonObject marker = store.Snapshot<JsonObject>(StoreName, original.Scope.ComputeMarkerKey()).ShouldNotBeNull();
        marker.ContainsKey("manifest").ShouldBeFalse();
        marker.ContainsKey("manifestKey").ShouldBeFalse();
        store.Count.ShouldBe(1); // The only persisted key is the payload-free prepared marker.

        store.BatchFaultHook = null;
        (await handler.ProjectAsync(request, "changed", TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        store.Snapshot<PartyDetailSdkReadModel>(StoreName, s_detailKey)!.Detail!.PersonDetails!.FirstName.ShouldBe(privateName);
        await new PartySdkReadModelEraser(store, store, s_options).EraseAsync("tenant-a", "party-1", TestContext.Current.CancellationToken);
        PartyDetailSdkReadModel erased = store.Snapshot<PartyDetailSdkReadModel>(StoreName, s_detailKey).ShouldNotBeNull();
        erased.ErasureSequenceNumber.ShouldBe(2);
        JsonSerializer.Serialize(erased).ShouldNotContain(privateName);
        JsonSerializer.Serialize(store.Snapshot<PartyProcessingSdkReadModel>(StoreName, s_processingKey)).ShouldNotContain(privateName);
        store.Snapshot<JsonObject>(StoreName, original.Scope.ComputeMarkerKey())!.ToJsonString().ShouldBe(marker.ToJsonString());
        batches.Count.ShouldBe(2); // Supported first deliveries do not initialize empty slots.
        batches.ShouldAllBe(batch => !batch.RetainManifestForRecovery);
    }

    /// <summary>Verifies a normal namespace terminal anchor can pass initialization preflight.</summary>
    /// <param name="interruption">The SDK phase at which the operation is interrupted.</param>
    [Theory]
    [InlineData(ReadModelBatchPhase.RecoveryReceiptWritten)]
    [InlineData(ReadModelBatchPhase.RecoveryManifestReclaimed)]
    public async Task NormalNamespaceTerminalAnchorDoesNotBlockInitializationPreflightAsync(ReadModelBatchPhase interruption)
    {
        var store = new InMemoryReadModelStore();
        var batches = new List<ReadModelBatch>();
        PartyDetailSdkProjectionHandler handler = Handler(store, batches);
        store.BatchFaultHook = (_, _, _) => throw new IOException("capture the unchanged normal manifest");
        await Should.ThrowAsync<IOException>(() => handler.ProjectAsync(Request(supported: true), "original", TestContext.Current.CancellationToken));
        ReadModelBatch normal = batches.ShouldHaveSingleItem();
        normal.RetainManifestForRecovery.ShouldBeFalse();
        // Fixture only: reproduce the SDK normal-namespace anchor from the rejected candidate.
        // Production normal deliveries must continue to submit the non-retained manifest above.
        var retainedFixture = new ReadModelBatch(normal.Scope, normal.Operations) { RetainManifestForRecovery = true };
        store.BatchFaultHook = (phase, _, _) => phase == interruption || phase == ReadModelBatchPhase.Reconcile
            ? throw new IOException("terminal cleanup interrupted") : Task.CompletedTask;
        (await store.ExecuteAsync(retainedFixture, TestContext.Current.CancellationToken)).Status.ShouldBe(ReadModelBatchStatus.Indeterminate);
        store.HasPendingEnvelope(StoreName, s_processingKey).ShouldBeTrue();
        store.BatchFaultHook = null;
        (await handler.ProjectAsync(Request(supported: true), "changed", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        batches.ShouldHaveSingleItem().ShouldBeSameAs(normal);
        await AssertSupportedAsync(store);
    }

    /// <summary>Verifies supported-prefix auditing preserves recovery of the unresolved suffix.</summary>
    [Fact]
    public async Task MixedSupportedPrefixAndUnresolvedRedeliveryRepairsBothCheckpointsAsync()
    {
        var store = new InMemoryReadModelStore();
        var batches = new List<ReadModelBatch>();
        PartyDetailSdkProjectionHandler handler = Handler(store, batches);
        ProjectionRequest request = Request(supported: false) with
        {
            Events = [Event(new PartyCreated { Type = PartyType.Person }, 1), Request(supported: false).Events[0] with { SequenceNumber = 2 }],
        };
        for (int attempt = 0; attempt < 2; attempt++)
        {
            (await handler.ProjectAsync(request, "mixed", TestContext.Current.CancellationToken)).ReasonCode.ShouldBe("unresolved-or-unsupported-event");
            store.Snapshot<PartyDetailSdkReadModel>(StoreName, s_detailKey)!.LastSequenceNumber.ShouldBe(long.MinValue);
            PartyProcessingSdkReadModel audit = store.Snapshot<PartyProcessingSdkReadModel>(StoreName, s_processingKey).ShouldNotBeNull();
            audit.LastSequenceNumber.ShouldBe(1);
            audit.Records.Select(record => record.Outcome).ShouldBe(["Succeeded", "Failed"]);
            await AssertCompactedAsync(store);
        }

        ReadModelBatch initialization = batches.ShouldHaveSingleItem();
        Deserialize<PartyDetailSdkReadModel>(initialization, s_detailKey).Detail.ShouldBeNull();
        Deserialize<PartyProcessingSdkReadModel>(initialization, s_processingKey).Records.ShouldBeEmpty();
        request = request with { Events = [request.Events[0], Event(new PartyDeactivated(), 2)] };
        (await handler.ProjectAsync(request, "changed", TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        PartyDetailSdkReadModel detail = store.Snapshot<PartyDetailSdkReadModel>(StoreName, s_detailKey).ShouldNotBeNull();
        detail.LastSequenceNumber.ShouldBe(2);
        detail.Detail!.IsActive.ShouldBeFalse();
        PartyProcessingSdkReadModel repaired = store.Snapshot<PartyProcessingSdkReadModel>(StoreName, s_processingKey).ShouldNotBeNull();
        repaired.LastSequenceNumber.ShouldBe(2);
        repaired.Records.Select(record => record.Outcome).ShouldBe(["Succeeded", "Succeeded"]);
        batches.Count.ShouldBe(2);
        batches[1].RetainManifestForRecovery.ShouldBeFalse();
        batches[1].Operations[1].Concurrency.Mode.ShouldBe(ReadModelBatchConcurrencyMode.ExpectedETag);
        await AssertCompactedAsync(store);
    }

    /// <summary>Verifies unresolved-prefix auditing preserves recovery of both event checkpoints.</summary>
    [Fact]
    public async Task UnresolvedFirstAndSupportedSecondRedeliveryRepairsBothCheckpointsAsync()
    {
        var store = new InMemoryReadModelStore();
        var batches = new List<ReadModelBatch>();
        PartyDetailSdkProjectionHandler handler = Handler(store, batches);
        ProjectionRequest request = Request(supported: false) with
        {
            Events = [Request(supported: false).Events[0], Event(new PersonDetailsUpdated
            {
                PersonDetails = new PersonDetails { FirstName = "Resolved", LastName = "Person" },
            }, 2)],
        };
        for (int attempt = 0; attempt < 2; attempt++)
        {
            (await handler.ProjectAsync(request, "unresolved-first", TestContext.Current.CancellationToken))
                .ReasonCode.ShouldBe("unresolved-or-unsupported-event");
            PartyDetailSdkReadModel detail = store.Snapshot<PartyDetailSdkReadModel>(StoreName, s_detailKey).ShouldNotBeNull();
            detail.Detail.ShouldBeNull();
            detail.LastSequenceNumber.ShouldBe(long.MinValue);
            PartyProcessingSdkReadModel audit = store.Snapshot<PartyProcessingSdkReadModel>(StoreName, s_processingKey).ShouldNotBeNull();
            audit.LastSequenceNumber.ShouldBe(long.MinValue);
            audit.Records.Select(record => record.SequenceNumber).ShouldBe([1L, 2L]);
            audit.Records.Select(record => record.Outcome).ShouldBe(["Failed", "Succeeded"]);
            audit.Records[0].EventType.ShouldBe("UnknownPartyEvent");
            await AssertCompactedAsync(store);
        }

        batches.ShouldHaveSingleItem();
        request = request with { Events = [Event(new PartyCreated { Type = PartyType.Person }, 1), request.Events[1]] };
        (await handler.ProjectAsync(request, "resolved-first", TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        PartyDetailSdkReadModel repairedDetail = store.Snapshot<PartyDetailSdkReadModel>(StoreName, s_detailKey).ShouldNotBeNull();
        repairedDetail.LastSequenceNumber.ShouldBe(2);
        repairedDetail.Detail.ShouldNotBeNull().Type.ShouldBe(PartyType.Person);
        repairedDetail.Detail.PersonDetails.ShouldNotBeNull().FirstName.ShouldBe("Resolved");
        repairedDetail.Detail.PersonDetails.LastName.ShouldBe("Person");
        PartyProcessingSdkReadModel repairedAudit = store.Snapshot<PartyProcessingSdkReadModel>(StoreName, s_processingKey).ShouldNotBeNull();
        repairedAudit.LastSequenceNumber.ShouldBe(2);
        repairedAudit.Records.Select(record => record.SequenceNumber).ShouldBe([1L, 2L]);
        repairedAudit.Records.Select(record => record.Outcome).ShouldBe(["Succeeded", "Succeeded"]);
        repairedAudit.Records[0].EventType.ShouldBe(nameof(PartyCreated));
        repairedAudit.Records[0].OperationCategory.ShouldBe("PartyCommand");
        batches.Count.ShouldBe(2);
        batches[1].RetainManifestForRecovery.ShouldBeFalse();
        await AssertCompactedAsync(store);
    }

    /// <summary>Verifies interrupted compensation finishes before coordinated-slot validation.</summary>
    /// <param name="interruption">The SDK phase at which the operation is interrupted.</param>
    [Theory]
    [InlineData(ReadModelBatchPhase.RecoveryReceiptWritten)]
    [InlineData(ReadModelBatchPhase.RecoveryManifestReclaimed)]
    public async Task InterruptedCompensationRecoversBeforeMissingSlotGuardAsync(ReadModelBatchPhase interruption)
    {
        var store = new InMemoryReadModelStore();
        var batches = new List<ReadModelBatch>();
        PartyDetailSdkProjectionHandler handler = Handler(store, batches);
        bool contended = false;
        bool interrupted = false;
        store.BatchFaultHook = (phase, ordinal, _) =>
        {
            if (phase == ReadModelBatchPhase.BeforeInstallOperation && ordinal == 1 && !contended)
            {
                contended = true;
                store.SeedRaw(StoreName, s_processingKey, new PartyProcessingSdkReadModel { LastSequenceNumber = 5 });
            }
            if (phase == interruption)
            {
                interrupted = true;
                throw new IOException("compensation interrupted");
            }
            if (phase == ReadModelBatchPhase.Reconcile && interrupted)
            {
                throw new IOException("reconcile interrupted");
            }
            return Task.CompletedTask;
        };
        (await handler.ProjectAsync(Request(supported: false), "original", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Indeterminate);
        contended.ShouldBeTrue();
        interrupted.ShouldBeTrue();
        store.HasPendingEnvelope(StoreName, s_detailKey).ShouldBeTrue();
        string processingEtag = (await store.GetAsync<PartyProcessingSdkReadModel>(StoreName, s_processingKey, TestContext.Current.CancellationToken)).ETag!;
        store.BatchFaultHook = null;
        (await handler.ProjectAsync(Request(supported: true), "changed", TestContext.Current.CancellationToken))
            .ReasonCode.ShouldBe("projection-rebuild-required");
        store.HasPendingEnvelope(StoreName, s_detailKey).ShouldBeFalse();
        store.Snapshot<PartyDetailSdkReadModel>(StoreName, s_detailKey).ShouldBeNull();
        store.Snapshot<PartyProcessingSdkReadModel>(StoreName, s_processingKey)!.LastSequenceNumber.ShouldBe(5);
        (await store.GetAsync<PartyProcessingSdkReadModel>(StoreName, s_processingKey, TestContext.Current.CancellationToken)).ETag.ShouldBe(processingEtag);
        (await store.ExecuteAsync(batches.ShouldHaveSingleItem(), TestContext.Current.CancellationToken)).ConflictKind.ShouldBe(ReadModelBatchConflictKind.Optimistic);
    }

    /// <summary>Verifies overlapping handlers converge without replacing a supported delivery.</summary>
    [Fact]
    public async Task OverlappingHandlersRecoverInitializerThenPreserveSupportedDeliveryAsync()
    {
        var store = new InMemoryReadModelStore();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int paused = 0;
        store.BatchFaultHook = async (phase, ordinal, token) =>
        {
            if (phase == ReadModelBatchPhase.AfterInstallOperation && ordinal == 0 && Interlocked.CompareExchange(ref paused, 1, 0) == 0)
            {
                entered.SetResult();
                await release.Task.WaitAsync(token).ConfigureAwait(false);
            }
        };
        Task<DomainProjectionHandlerResult> unresolved = Handler(store).ProjectAsync(Request(supported: false), "original", TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        string? detailEtag = null;
        string? processingEtag = null;
        try
        {
            (await Handler(store).ProjectAsync(Request(supported: true), "changed", TestContext.Current.CancellationToken))
                .Status.ShouldBe(ProjectionDispatchStatus.Completed);
            await AssertSupportedAsync(store);
            detailEtag = (await store.GetAsync<PartyDetailSdkReadModel>(StoreName, s_detailKey, TestContext.Current.CancellationToken)).ETag;
            processingEtag = (await store.GetAsync<PartyProcessingSdkReadModel>(StoreName, s_processingKey, TestContext.Current.CancellationToken)).ETag;
        }
        finally
        {
            release.TrySetResult();
        }
        (await unresolved.ConfigureAwait(true)).Status.ShouldBe(ProjectionDispatchStatus.Indeterminate);
        (await Handler(store).ProjectAsync(Request(supported: false), "original", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        await AssertSupportedAsync(store);
        (await store.GetAsync<PartyDetailSdkReadModel>(StoreName, s_detailKey, TestContext.Current.CancellationToken)).ETag.ShouldBe(detailEtag);
        (await store.GetAsync<PartyProcessingSdkReadModel>(StoreName, s_processingKey, TestContext.Current.CancellationToken)).ETag.ShouldBe(processingEtag);
    }

    /// <summary>Verifies failed recovery remains retryable without fabricating historical state.</summary>
    [Fact]
    public async Task RecoveryFailureStaysRetryableAndDoesNotCreateMissingHistoricalSlotsAsync()
    {
        var store = new InMemoryReadModelStore();
        PartyDetailSdkProjectionHandler handler = Handler(store);
        store.BatchFaultHook = (phase, _, _) => phase is ReadModelBatchPhase.AfterInstallOperation or ReadModelBatchPhase.Reconcile
            ? throw new IOException("store unavailable")
            : Task.CompletedTask;
        (await handler.ProjectAsync(Request(supported: false), "original", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Indeterminate);
        DomainProjectionHandlerResult failed = await handler.ProjectAsync(Request(supported: true), "changed", TestContext.Current.CancellationToken);
        failed.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        failed.ReasonCode.ShouldBe("projection-recovery-incomplete");
        store.Snapshot<PartyProcessingSdkReadModel>(StoreName, s_processingKey).ShouldBeNull();
        store.BatchFaultHook = null;
        (await handler.ProjectAsync(Request(supported: true), "changed", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        await AssertSupportedAsync(store);
    }

    /// <summary>Verifies second-operation contention compensates initialization without restarting it.</summary>
    [Fact]
    public async Task SecondOperationContentionCompensatesFirstAndKeepsInitializerTerminalAsync()
    {
        var store = new InMemoryReadModelStore();
        var batches = new List<ReadModelBatch>();
        PartyDetailSdkProjectionHandler handler = Handler(store, batches);
        bool contended = false;
        store.BatchFaultHook = (phase, ordinal, _) =>
        {
            if (phase == ReadModelBatchPhase.BeforeInstallOperation && ordinal == 1 && !contended)
            {
                store.HasPendingEnvelope(StoreName, s_detailKey).ShouldBeTrue();
                contended = true;
                store.SeedRaw(StoreName, s_processingKey, new PartyProcessingSdkReadModel { LastSequenceNumber = 5 });
            }

            return Task.CompletedTask;
        };
        (await handler.ProjectAsync(Request(supported: false), "original", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        contended.ShouldBeTrue();
        store.BatchFaultHook = null;
        ReadModelBatch original = batches.ShouldHaveSingleItem();
        (await store.ExecuteAsync(original, TestContext.Current.CancellationToken)).ConflictKind.ShouldBe(ReadModelBatchConflictKind.Optimistic);
        (await store.GetAsync<PartyDetailSdkReadModel>(StoreName, s_detailKey, TestContext.Current.CancellationToken)).Value.ShouldBeNull();
        store.Snapshot<PartyProcessingSdkReadModel>(StoreName, s_processingKey)!.LastSequenceNumber.ShouldBe(5);
        store.HasPendingEnvelope(StoreName, s_detailKey).ShouldBeFalse();
        store.HasPendingEnvelope(StoreName, s_processingKey).ShouldBeFalse();
        DomainProjectionHandlerResult retry = await handler.ProjectAsync(Request(supported: true), "changed", TestContext.Current.CancellationToken);
        retry.ReasonCode.ShouldBe("projection-rebuild-required");
        batches.Count.ShouldBe(1);
    }

    /// <summary>Verifies historical loss of either coordinated slot still requires a rebuild.</summary>
    /// <param name="detailIsMissing">Whether the Detail slot is the missing historical slot.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GenuineMissingHistoricalSlotStillRequiresRebuildAsync(bool detailIsMissing)
    {
        var store = new InMemoryReadModelStore();
        if (detailIsMissing)
        {
            store.SeedRaw(StoreName, s_processingKey, new PartyProcessingSdkReadModel { LastSequenceNumber = 4 });
        }
        else
        {
            store.SeedRaw(StoreName, s_detailKey, new PartyDetailSdkReadModel { LastSequenceNumber = 4 });
        }

        DomainProjectionHandlerResult result = await Handler(store).ProjectAsync(Request(supported: true) with
        {
            Events = [Event(new PartyDeactivated(), 5)],
        }, "tail", TestContext.Current.CancellationToken);
        result.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        result.ReasonCode.ShouldBe("projection-rebuild-required");
        ((await store.GetAsync<PartyDetailSdkReadModel>(StoreName, s_detailKey, TestContext.Current.CancellationToken)).Value is null).ShouldBe(detailIsMissing);
        ((await store.GetAsync<PartyProcessingSdkReadModel>(StoreName, s_processingKey, TestContext.Current.CancellationToken)).Value is null).ShouldBe(!detailIsMissing);
    }

    /// <summary>Verifies incoming scope validation occurs before initialization identity hashing.</summary>
    /// <param name="oversized">Whether the incoming identity exceeds the SDK byte limit.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IncomingDispatchByteLimitAppliesBeforeInitializationHashingAsync(bool oversized)
    {
        var store = new InMemoryReadModelStore();
        string dispatch = new('é', (ReadModelBatchScope.MaxComponentByteLength / 2) + (oversized ? 1 : 0));
        if (oversized)
        {
            await Should.ThrowAsync<ArgumentException>(() => Handler(store).ProjectAsync(Request(supported: false), dispatch,
                TestContext.Current.CancellationToken));
            store.Count.ShouldBe(0);
        }
        else
        {
            (await Handler(store).ProjectAsync(Request(supported: false), dispatch, TestContext.Current.CancellationToken))
                .ReasonCode.ShouldBe("unresolved-or-unsupported-event");
            await AssertUnadvancedAuditAsync(store);
        }
    }

    /// <summary>Verifies initialization reconciliation preserves newer erasure tombstones.</summary>
    [Fact]
    public async Task NewerErasureBeforeReceiptSurvivesInitializationReconciliationAsync()
    {
        var store = new InMemoryReadModelStore();
        DateTimeOffset erasedAt = DateTimeOffset.UnixEpoch.AddSeconds(5);
        var detail = new PartyDetailSdkReadModel
        {
            LastSequenceNumber = 5, ErasureSequenceNumber = 5, ErasedAt = erasedAt,
            ProjectedAt = erasedAt, ProjectionVersion = "5",
        };
        var processing = new PartyProcessingSdkReadModel
        {
            LastSequenceNumber = 5, ErasureSequenceNumber = 5, ErasedAt = erasedAt,
            ProjectedAt = erasedAt, ProjectionVersion = "5",
        };
        bool injected = false;
        string? detailEtag = null;
        string? processingEtag = null;
        store.BatchFaultHook = async (phase, _, _) =>
        {
            if (phase == ReadModelBatchPhase.BeforeReceipt && !injected)
            {
                injected = true;
                store.SeedRaw(StoreName, s_detailKey, detail);
                store.SeedRaw(StoreName, s_processingKey, processing);
                detailEtag = (await store.GetAsync<PartyDetailSdkReadModel>(StoreName, s_detailKey, TestContext.Current.CancellationToken).ConfigureAwait(false)).ETag;
                processingEtag = (await store.GetAsync<PartyProcessingSdkReadModel>(StoreName, s_processingKey, TestContext.Current.CancellationToken).ConfigureAwait(false)).ETag;
                throw new IOException("receipt interrupted after erasure");
            }
        };
        PartyDetailSdkProjectionHandler handler = Handler(store);
        _ = await handler.ProjectAsync(Request(supported: false), "original", TestContext.Current.CancellationToken);
        injected.ShouldBeTrue();
        store.BatchFaultHook = null;
        (await handler.ProjectAsync(Request(supported: true), "changed", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        JsonSerializer.Serialize(store.Snapshot<PartyDetailSdkReadModel>(StoreName, s_detailKey)).ShouldBe(JsonSerializer.Serialize(detail));
        JsonSerializer.Serialize(store.Snapshot<PartyProcessingSdkReadModel>(StoreName, s_processingKey)).ShouldBe(JsonSerializer.Serialize(processing));
        (await store.GetAsync<PartyDetailSdkReadModel>(StoreName, s_detailKey, TestContext.Current.CancellationToken)).ETag.ShouldBe(detailEtag);
        (await store.GetAsync<PartyProcessingSdkReadModel>(StoreName, s_processingKey, TestContext.Current.CancellationToken)).ETag.ShouldBe(processingEtag);
        store.HasPendingEnvelope(StoreName, s_detailKey).ShouldBeFalse();
        store.HasPendingEnvelope(StoreName, s_processingKey).ShouldBeFalse();
    }

    [Theory]
    [InlineData(ReadModelBatchPhase.AfterCommit)]
    [InlineData(ReadModelBatchPhase.BeforeCompaction)]
    [InlineData(ReadModelBatchPhase.AfterCompaction)]
    [InlineData(ReadModelBatchPhase.BeforeReceipt)]
    public async Task NormalCommittedBatchRetryPreservesNewerErasureTombstoneBytes(ReadModelBatchPhase interruption)
    {
        Assert.SkipUnless(typeof(PartyDetailSdkProjectionRecoveryTests).Assembly
                .GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
                .OfType<System.Reflection.AssemblyMetadataAttribute>()
                .Any(attribute => attribute.Key == "HexalithSourceMappingMode" && attribute.Value == "source"),
            "Requires a qualified pinned-tool-generated source-mode build. Published EventStore 3.127.0 lacks this recovery fix; its package gate remains open.");
        var store = new InMemoryReadModelStore();
        var batches = new List<ReadModelBatch>();
        PartyDetailSdkProjectionHandler handler = Handler(store, batches);
        ProjectionRequest request = Request(supported: true);
        store.BatchFaultHook = (phase, _, _) => phase == interruption || phase == ReadModelBatchPhase.Reconcile
            ? throw new IOException("projection interrupted after commit") : Task.CompletedTask;
        (await handler.ProjectAsync(request, "original", TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Indeterminate);
        ReadModelBatch original = batches.ShouldHaveSingleItem();
        original.RetainManifestForRecovery.ShouldBeFalse();
        store.BatchFaultHook = null;
        // Produce canonical tombstones through the actual eraser on a fully compacted
        // projection, then model the later ordinary SDK writer on the interrupted store.
        // A separate batch correctly refuses to replace another batch's pending envelopes.
        var newerStore = new InMemoryReadModelStore();
        (await Handler(newerStore).ProjectAsync(request, "newer", TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        await new PartySdkReadModelEraser(newerStore, newerStore, s_options).EraseAsync("tenant-a", "party-1", TestContext.Current.CancellationToken);
        await store.SaveAsync(StoreName, s_detailKey, newerStore.Snapshot<PartyDetailSdkReadModel>(StoreName, s_detailKey)!, TestContext.Current.CancellationToken);
        await store.SaveAsync(StoreName, s_processingKey, newerStore.Snapshot<PartyProcessingSdkReadModel>(StoreName, s_processingKey)!, TestContext.Current.CancellationToken);
        byte[] detail = store.SnapshotRaw(StoreName, s_detailKey)!;
        byte[] processing = store.SnapshotRaw(StoreName, s_processingKey)!;
        store.Snapshot<PartyDetailSdkReadModel>(StoreName, s_detailKey)!.ErasureSequenceNumber.ShouldBe(1);
        (await store.ExecuteAsync(original, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        (await store.ExecuteAsync(original, TestContext.Current.CancellationToken)).Status.ShouldBe(ReadModelBatchStatus.AlreadyCompleted);
        store.SnapshotRaw(StoreName, s_detailKey).ShouldBe(detail);
        store.SnapshotRaw(StoreName, s_processingKey).ShouldBe(processing);
        JsonObject receipt = store.Snapshot<JsonObject>(StoreName, original.Scope.ComputeMarkerKey())!;
        receipt.ContainsKey("manifest").ShouldBeFalse(); receipt.ContainsKey("manifestKey").ShouldBeFalse();
    }

    private static PartyDetailSdkProjectionHandler Handler(InMemoryReadModelStore store, List<ReadModelBatch>? batches = null)
    {
        if (batches is null)
        {
            return new PartyDetailSdkProjectionHandler(store, store, s_options);
        }

        // Forward unchanged to the real SDK protocol; only record the caller's immutable manifests.
        IReadModelBatchStore recorder = Substitute.For<IReadModelBatchStore, IReadModelPendingBatchRecoveryStore>();
        recorder.ExecuteAsync(Arg.Any<ReadModelBatch>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            ReadModelBatch batch = call.Arg<ReadModelBatch>();
            batches.Add(batch);
            return store.ExecuteAsync(batch, call.Arg<CancellationToken>());
        });
        ((IReadModelPendingBatchRecoveryStore)recorder).RecoverPendingAsync(Arg.Any<ReadModelBatchScope>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => store.RecoverPendingAsync(call.Arg<ReadModelBatchScope>(), call.Arg<string>(), call.Arg<CancellationToken>()));
#if HEXALITH_EVENTSTORE_NONEXPIRING_RECOVERY
        ((IReadModelPendingBatchRecoveryStore)recorder).RecoverNonExpiringPendingAsync(Arg.Any<ReadModelBatchScope>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => store.RecoverNonExpiringPendingAsync(call.Arg<ReadModelBatchScope>(), call.Arg<string>(), call.Arg<CancellationToken>()));
#endif
        return new PartyDetailSdkProjectionHandler(store, recorder, s_options);
    }

    private static ProjectionRequest Request(bool supported)
        => new("tenant-a", "party", "party-1", [supported
            ? Event(new PartyCreated { Type = PartyType.Person }, 1)
            : new ProjectionEventDto("UnknownPartyEvent", "{}"u8.ToArray(), "json", 1, DateTimeOffset.UnixEpoch, "correlation")]);

    private static ProjectionEventDto Event(object payload, long sequence)
        => new(payload.GetType().Name, JsonSerializer.SerializeToUtf8Bytes(payload, payload.GetType(), PartiesJsonOptions.Default),
            "json", sequence, DateTimeOffset.UnixEpoch.AddSeconds(sequence), "correlation");

    private static TValue Deserialize<TValue>(ReadModelBatch batch, string key)
        => JsonSerializer.Deserialize<TValue>(batch.Operations.Single(operation => operation.Key == key).CanonicalValue.Span,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    private static async Task AssertUnadvancedAuditAsync(InMemoryReadModelStore store)
    {
        PartyDetailSdkReadModel detail = store.Snapshot<PartyDetailSdkReadModel>(StoreName, s_detailKey).ShouldNotBeNull();
        PartyProcessingSdkReadModel processing = store.Snapshot<PartyProcessingSdkReadModel>(StoreName, s_processingKey).ShouldNotBeNull();
        detail.Detail.ShouldBeNull();
        detail.LastSequenceNumber.ShouldBe(long.MinValue);
        detail.ProjectionVersion.ShouldBeNull();
        detail.ErasureSequenceNumber.ShouldBeNull();
        processing.LastSequenceNumber.ShouldBe(long.MinValue);
        processing.Records.ShouldHaveSingleItem().Outcome.ShouldBe("Failed");
        await AssertCompactedAsync(store).ConfigureAwait(false);
    }

    private static async Task AssertSupportedAsync(InMemoryReadModelStore store)
    {
        PartyDetailSdkReadModel detail = store.Snapshot<PartyDetailSdkReadModel>(StoreName, s_detailKey).ShouldNotBeNull();
        detail.LastSequenceNumber.ShouldBe(1);
        detail.ProjectionVersion.ShouldBe("1");
        detail.Detail.ShouldNotBeNull().Type.ShouldBe(PartyType.Person);
        PartyProcessingSdkReadModel processing = store.Snapshot<PartyProcessingSdkReadModel>(StoreName, s_processingKey).ShouldNotBeNull();
        processing.LastSequenceNumber.ShouldBe(1);
        processing.Records.ShouldHaveSingleItem().Outcome.ShouldBe("Succeeded");
        await AssertCompactedAsync(store).ConfigureAwait(false);
    }

    private static async Task AssertCompactedAsync(InMemoryReadModelStore store)
    {
        (await store.GetAsync<PartyDetailSdkReadModel>(StoreName, s_detailKey, TestContext.Current.CancellationToken).ConfigureAwait(false)).ETag.ShouldNotBeNullOrEmpty();
        (await store.GetAsync<PartyProcessingSdkReadModel>(StoreName, s_processingKey, TestContext.Current.CancellationToken).ConfigureAwait(false)).ETag.ShouldNotBeNullOrEmpty();
        store.HasPendingEnvelope(StoreName, s_detailKey).ShouldBeFalse();
        store.HasPendingEnvelope(StoreName, s_processingKey).ShouldBeFalse();
    }
}
