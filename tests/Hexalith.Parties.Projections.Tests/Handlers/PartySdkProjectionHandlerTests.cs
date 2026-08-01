using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Configuration;
using Hexalith.Parties.Projections.Handlers;
using Hexalith.Parties.Projections.Models;
using Hexalith.Parties.Projections.Services;

using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.Projections.Tests.Handlers;

public sealed class PartySdkProjectionHandlerTests
{
    private static readonly JsonSerializerOptions s_canonicalJsonOptions = new(PartiesJsonOptions.Default)
    {
        PropertyNameCaseInsensitive = true,
    };
    private static readonly IOptions<PartySdkReadModelOptions> s_options =
        Options.Create(new PartySdkReadModelOptions { ReadModelStateStoreName = "statestore" });

    [Fact]
    public async Task DetailHandler_WritesCanonicalBatchAndMatchesRetainedFoldAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        IReadModelBatchStore batchStore = Substitute.For<IReadModelBatchStore>();
        store.GetAsync<PartyDetailSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyDetailSdkReadModel>(null, null));
        store.GetAsync<PartyProcessingSdkReadModel>("statestore", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyProcessingSdkReadModel>(null, null));
        ReadModelBatch? captured = null;
        batchStore.ExecuteAsync(Arg.Do<ReadModelBatch>(value => captured = value), Arg.Any<CancellationToken>())
            .Returns(ReadModelBatchResult.Completed("fingerprint"));
        var handler = new PartyDetailSdkProjectionHandler(store, batchStore, s_options);
        ProjectionRequest request = CreateRequest();

        DomainProjectionHandlerResult result = await handler.ProjectAsync(
            request,
            "dispatch-1",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        captured.ShouldNotBeNull();
        captured.Scope.ShouldBe(new ReadModelBatchScope(
            "statestore", "tenant-a", "party", "party-1", PartyProjectionNames.Detail, "dispatch-1"));
        captured.Operations.Count.ShouldBe(2);
        ReadModelBatchOperation detailOperation = captured.Operations.Single(static operation =>
            operation.Key.EndsWith(":detail", StringComparison.Ordinal));
        detailOperation.Key.ShouldBe("readmodel:tenant-a:party:party-detail:party-1:detail");
        PartyDetailSdkReadModel persisted = JsonSerializer.Deserialize<PartyDetailSdkReadModel>(
            detailOperation.CanonicalValue.Span,
            s_canonicalJsonOptions)!;
        PartyDetail? retained = PartyDetailProjectionHandler.Apply(
            "party-1",
            new PartyCreated { Type = PartyType.Person },
            null);
        retained = PartyDetailProjectionHandler.Apply("party-1", new PartyDeactivated(), retained) ?? retained;
        persisted.Detail.ShouldNotBeNull();
        persisted.Detail.Id.ShouldBe(retained!.Id);
        persisted.Detail.Type.ShouldBe(retained.Type);
        persisted.Detail.IsActive.ShouldBe(retained.IsActive);
        persisted.LastSequenceNumber.ShouldBe(2);
        persisted.ProjectedAt.ShouldBe(DateTimeOffset.UnixEpoch.AddSeconds(1));
    }

    [Fact]
    public async Task DetailRebuildPlan_MatchesNormalReplayAfterTimestampNormalizationAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        IReadModelBatchStore batchStore = Substitute.For<IReadModelBatchStore>();
        var handler = new PartyDetailSdkProjectionHandler(store, batchStore, s_options);
        ProjectionRequest request = CreateRequest();

        DomainProjectionRebuildPlan rebuild = await handler.PrepareRebuildAsync(
            request,
            "rebuild-1",
            TestContext.Current.CancellationToken);
        PartyDetailSdkReadModel candidate = JsonSerializer.Deserialize<PartyDetailSdkReadModel>(
            rebuild.Operations.Single(static operation => operation.Key.EndsWith(":detail", StringComparison.Ordinal)).CanonicalValue.Span,
            s_canonicalJsonOptions)!;
        PartyDetailSdkReadModel replay = PartyDetailSdkProjectionHandler.Fold(request, current: null);

        Normalize(candidate).ShouldBe(Normalize(replay));
        rebuild.StoreName.ShouldBe("statestore");
        rebuild.Operations.Count.ShouldBe(2);
        handler.RebuildSemantics.ShouldBe(DomainProjectionRebuildSemantics.FullReplay);
    }

    [Fact]
    public async Task IndexHandler_ErasureRemovesOnlyTargetAndPreservesUnrelatedEntryAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        PartyIndexSdkReadModel existing = CreateIndex("party-1", "party-2");
        store.GetAsync<PartyIndexSdkReadModel>("statestore", PartySdkReadModelAddresses.Index("tenant-a"), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyIndexSdkReadModel>(existing, "etag-1"));
        PartyIndexSdkReadModel? persisted = null;
        store.TrySaveAsync(
                "statestore",
                PartySdkReadModelAddresses.Index("tenant-a"),
                Arg.Do<PartyIndexSdkReadModel>(value => persisted = value),
                "etag-1",
                Arg.Any<CancellationToken>())
            .Returns(true);
        var handler = new PartyIndexSdkProjectionHandler(store, s_options);

        DomainProjectionHandlerResult result = await handler.ProjectAsync(
            CreateErasureRequest(),
            "dispatch-erase",
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        persisted.ShouldNotBeNull();
        persisted.Entries.ContainsKey("party-1").ShouldBeFalse();
        persisted.Entries["party-2"].DisplayName.ShouldBe("Party party-2");
        persisted.LastSequenceNumbers["party-1"].ShouldBe(3);
        persisted.LastSequenceNumbers["party-2"].ShouldBe(1);
    }

    [Fact]
    public void SharedIndex_IsDeclaredSharedAndAdvertisesOnlyDomainSharedRebuild()
    {
        ProjectionReadModelSlotDeclaration slot = PartyIndexSdkProjectionHandler.ProjectionReadModelSlots.Single();

        slot.Domain.ShouldBe("party");
        slot.Kind.ShouldBe(ProjectionReadModelSlotKind.Shared);
        slot.DeclaresCanonicalWriter.ShouldBeTrue();
        typeof(IAsyncDomainProjectionRebuildHandler).IsAssignableFrom(typeof(PartyIndexSdkProjectionHandler)).ShouldBeFalse();
        typeof(IAsyncDomainSharedProjectionRebuildHandler).IsAssignableFrom(typeof(PartyIndexSdkProjectionHandler)).ShouldBeTrue();
    }

    [Fact]
    public async Task SharedIndexRebuild_AccumulatesCompleteHistoriesIntoOneReplacementAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        var handler = new PartyIndexSdkProjectionHandler(store, s_options);
        DomainSharedProjectionRebuildIdentity identity = CreateSharedRebuildIdentity();
        ProjectionRequest first = CreateRequest();
        ProjectionRequest second = CreateRequest() with
        {
            AggregateId = "party-2",
            Events =
            [
                Event(new PartyCreated { Type = PartyType.Organization }, 1, DateTimeOffset.UnixEpoch.AddSeconds(2)),
            ],
        };

        DomainSharedProjectionRebuildCandidate candidate = await handler.CreateEmptyCandidateAsync(
            identity,
            TestContext.Current.CancellationToken);
        candidate = await handler.AccumulateAsync(identity, candidate, first, TestContext.Current.CancellationToken);
        candidate = await handler.AccumulateAsync(identity, candidate, second, TestContext.Current.CancellationToken);
        DomainProjectionRebuildPlan plan = await handler.FinalizeAsync(
            identity,
            candidate,
            TestContext.Current.CancellationToken);

        plan.StoreName.ShouldBe("statestore");
        plan.Operations.Count.ShouldBe(1);
        plan.Operations[0].Key.ShouldBe(PartySdkReadModelAddresses.Index("tenant-a"));
        PartyIndexSdkReadModel rebuilt = JsonSerializer.Deserialize<PartyIndexSdkReadModel>(
            plan.Operations[0].CanonicalValue.Span,
            s_canonicalJsonOptions)!;
        PartyIndexSdkReadModel replayed = PartyIndexSdkProjectionHandler.Fold(
            second,
            PartyIndexSdkProjectionHandler.Fold(first, current: null));
        JsonSerializer.Serialize(rebuilt, s_canonicalJsonOptions)
            .ShouldBe(JsonSerializer.Serialize(replayed, s_canonicalJsonOptions));
        rebuilt.Entries.Keys.Order(StringComparer.Ordinal).ShouldBe(["party-1", "party-2"]);
        rebuilt.ProjectedAt.ShouldBe(DateTimeOffset.UnixEpoch.AddSeconds(2));
    }

    [Fact]
    public async Task SharedIndexRebuild_EmptyInventoryProducesEmptyReplacementThatPrunesStaleEntriesAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        var handler = new PartyIndexSdkProjectionHandler(store, s_options);
        DomainSharedProjectionRebuildIdentity identity = CreateSharedRebuildIdentity();
        DomainSharedProjectionRebuildCandidate candidate = await handler.CreateEmptyCandidateAsync(
            identity,
            TestContext.Current.CancellationToken);

        DomainProjectionRebuildPlan plan = await handler.FinalizeAsync(
            identity,
            candidate,
            TestContext.Current.CancellationToken);

        PartyIndexSdkReadModel rebuilt = JsonSerializer.Deserialize<PartyIndexSdkReadModel>(
            plan.Operations.Single().CanonicalValue.Span,
            s_canonicalJsonOptions)!;
        rebuilt.Entries.ShouldBeEmpty();
        rebuilt.LastSequenceNumbers.ShouldBeEmpty();
    }

    [Fact]
    public async Task Eraser_ConditionallyDeletesDetailButNeverWholeSharedIndexAsync()
    {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        IReadModelConditionalEraser conditional = Substitute.For<IReadModelConditionalEraser>();
        string detailKey = PartySdkReadModelAddresses.Detail("tenant-a", "party-1");
        string indexKey = PartySdkReadModelAddresses.Index("tenant-a");
        conditional.TryReadEtagAsync("statestore", detailKey, Arg.Any<CancellationToken>())
            .Returns((true, "detail-etag"));
        conditional.TryEraseAsync("statestore", detailKey, "detail-etag", Arg.Any<CancellationToken>())
            .Returns(true);
        store.GetAsync<PartyIndexSdkReadModel>("statestore", indexKey, Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<PartyIndexSdkReadModel>(CreateIndex("party-1", "party-2"), "index-etag"));
        PartyIndexSdkReadModel? persisted = null;
        store.TrySaveAsync(
                "statestore",
                indexKey,
                Arg.Do<PartyIndexSdkReadModel>(value => persisted = value),
                "index-etag",
                Arg.Any<CancellationToken>())
            .Returns(true);
        var eraser = new PartySdkReadModelEraser(store, conditional, s_options);

        await eraser.EraseAsync("tenant-a", "party-1", TestContext.Current.CancellationToken);

        await conditional.Received(1).TryEraseAsync(
            "statestore", detailKey, "detail-etag", Arg.Any<CancellationToken>());
        await conditional.DidNotReceive().TryEraseAsync(
            "statestore", indexKey, Arg.Any<string>(), Arg.Any<CancellationToken>());
        persisted.ShouldNotBeNull();
        persisted.Entries.Keys.ShouldBe(["party-2"]);
    }

    [Fact]
    public async Task DetailHandler_PartyErasedRedactsPersonalDataUsingEventTimestampAsync()
    {
        ProjectionRequest request = CreateRequest() with
        {
            Events =
            [
                .. CreateRequest().Events,
                Event(new PartyErased
                {
                    PartyId = "party-1",
                    TenantId = "tenant-a",
                    ErasedAt = DateTimeOffset.UnixEpoch.AddMinutes(5),
                }, 3, DateTimeOffset.UnixEpoch.AddMinutes(5)),
            ],
        };

        PartyDetailSdkReadModel result = PartyDetailSdkProjectionHandler.Fold(request, current: null);

        result.Detail.ShouldNotBeNull();
        result.Detail.IsErased.ShouldBeTrue();
        result.Detail.DisplayName.ShouldBeEmpty();
        result.Detail.ErasedAt.ShouldBe(DateTimeOffset.UnixEpoch.AddMinutes(5));
        result.Detail.LastModifiedAt.ShouldBe(DateTimeOffset.UnixEpoch.AddMinutes(5));
    }

    [Fact]
    public void DetailFold_DuplicateAndOutOfOrderBatchMatchesOrderedReplayWithoutFreshnessDrift()
    {
        ProjectionRequest ordered = CreateRequest();
        ProjectionRequest duplicateAndOutOfOrder = ordered with
        {
            Events =
            [
                ordered.Events[1],
                ordered.Events[0],
                ordered.Events[0] with { Timestamp = DateTimeOffset.UnixEpoch.AddHours(1) },
            ],
        };

        PartyDetailSdkReadModel expected = PartyDetailSdkProjectionHandler.Fold(ordered, current: null);
        PartyDetailSdkReadModel actual = PartyDetailSdkProjectionHandler.Fold(duplicateAndOutOfOrder, current: null);

        JsonSerializer.Serialize(actual, s_canonicalJsonOptions)
            .ShouldBe(JsonSerializer.Serialize(expected, s_canonicalJsonOptions));
        actual.ProjectedAt.ShouldBe(DateTimeOffset.UnixEpoch.AddSeconds(1));
    }

    [Fact]
    public void IndexFold_DuplicateAndOutOfOrderBatchMatchesOrderedReplayWithoutFreshnessDrift()
    {
        ProjectionRequest ordered = CreateRequest();
        ProjectionRequest duplicateAndOutOfOrder = ordered with
        {
            Events =
            [
                ordered.Events[1],
                ordered.Events[0],
                ordered.Events[0] with { Timestamp = DateTimeOffset.UnixEpoch.AddHours(1) },
            ],
        };

        PartyIndexSdkReadModel expected = PartyIndexSdkProjectionHandler.Fold(ordered, current: null);
        PartyIndexSdkReadModel actual = PartyIndexSdkProjectionHandler.Fold(duplicateAndOutOfOrder, current: null);

        JsonSerializer.Serialize(actual, s_canonicalJsonOptions)
            .ShouldBe(JsonSerializer.Serialize(expected, s_canonicalJsonOptions));
        actual.ProjectedAt.ShouldBe(DateTimeOffset.UnixEpoch.AddSeconds(1));
    }

    [Fact]
    public void ProcessingActivityFold_ProjectsBoundedMetadataWithoutPersonalPayloadText()
    {
        const string firstName = "PersonalFirstName-should-not-leak";
        const string lastName = "PersonalLastName-should-not-leak";
        ProjectionRequest request = new(
            "tenant-a",
            "party",
            "party-1",
            [
                Event(new PersonDetailsUpdated
                {
                    PersonDetails = new PersonDetails
                    {
                        FirstName = firstName,
                        LastName = lastName,
                    },
                }, 1, DateTimeOffset.UnixEpoch),
            ]);

        PartyProcessingSdkReadModel result = PartyProcessingActivityFold.Fold(request, current: null);

        ProcessingActivityRecord record = result.Records.ShouldHaveSingleItem();
        record.SequenceNumber.ShouldBe(1);
        record.OperationCategory.ShouldBe("PartyCommand");
        record.EventType.ShouldBe(nameof(PersonDetailsUpdated));
        record.Summary.ShouldBe("Person details updated.");
        string json = JsonSerializer.Serialize(result, s_canonicalJsonOptions);
        json.ShouldNotContain(firstName);
        json.ShouldNotContain(lastName);
    }

    private static ProjectionRequest CreateRequest()
        => new(
            "tenant-a",
            "party",
            "party-1",
            [
                Event(new PartyCreated { Type = PartyType.Person }, 1, DateTimeOffset.UnixEpoch),
                Event(new PartyDeactivated(), 2, DateTimeOffset.UnixEpoch.AddSeconds(1)),
            ]);

    private static ProjectionRequest CreateErasureRequest()
        => new(
            "tenant-a",
            "party",
            "party-1",
            [
                Event(new PartyErased
                {
                    PartyId = "party-1",
                    TenantId = "tenant-a",
                    ErasedAt = DateTimeOffset.UnixEpoch.AddMinutes(5),
                }, 3, DateTimeOffset.UnixEpoch.AddMinutes(5)),
            ]);

    private static DomainSharedProjectionRebuildIdentity CreateSharedRebuildIdentity()
        => new("tenant-a", "party", PartyProjectionNames.Index, "rebuild-1", "catalog-fingerprint");

    private static ProjectionEventDto Event(object payload, long sequence, DateTimeOffset timestamp)
        => new(
            payload.GetType().Name,
            JsonSerializer.SerializeToUtf8Bytes(payload, payload.GetType(), PartiesJsonOptions.Default),
            "json",
            sequence,
            timestamp,
            "correlation-1");

    private static PartyIndexSdkReadModel CreateIndex(params string[] partyIds)
        => new()
        {
            Entries = partyIds.ToDictionary(
                static id => id,
                static id => new PartyIndexEntry
                {
                    Id = id,
                    Type = PartyType.Person,
                    IsActive = true,
                    DisplayName = $"Party {id}",
                    SortName = id,
                },
                StringComparer.Ordinal),
            LastSequenceNumbers = partyIds.ToDictionary(static id => id, static _ => 1L, StringComparer.Ordinal),
            ProjectedAt = DateTimeOffset.UnixEpoch,
            ProjectionVersion = "1",
        };

    private static object Normalize(PartyDetailSdkReadModel model)
        => new
        {
            model.Detail?.Id,
            model.Detail?.Type,
            model.Detail?.IsActive,
            model.Detail?.DisplayName,
            model.Detail?.SortName,
            model.LastSequenceNumber,
            model.ProjectedAt,
            model.ProjectionVersion,
        };
}
