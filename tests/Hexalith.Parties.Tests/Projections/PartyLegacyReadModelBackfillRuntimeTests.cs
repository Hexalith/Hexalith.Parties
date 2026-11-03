using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.Parties.Projections.Models;

using Shouldly;

namespace Hexalith.Parties.Tests.Projections;

/// <summary>Qualifies synthetic backfill with the real SDK store and an explicitly disposable Redis/Dapr provider.</summary>
public sealed class PartyLegacyReadModelBackfillRuntimeTests
{
    [Theory(Timeout = 120000)]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualProviderRebuildPersistsAcrossFreshClientsAndPreservesErasureAndRollbackBytes(bool existing)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var fixture = new PartyLegacyBackfillRuntimeFixture(await PartyLegacyBackfillRuntimeFixture.ConfigurationAsync(token));
        await fixture.SeedAsync(token);
        fixture.Survivors.Count.ShouldBe(9);
        foreach (ProjectionRequest history in fixture.Histories)
        {
            string detailKey = PartySdkReadModelAddresses.Detail(fixture.Tenant, history.AggregateId);
            string processingKey = PartySdkReadModelAddresses.Processing(fixture.Tenant, history.AggregateId);
            if (existing)
            {
                await fixture.SaveAsync(detailKey, new PartyDetailSdkReadModel { ProjectionVersion = "previous-canonical" }, token);
                await fixture.SaveAsync(processingKey, new PartyProcessingSdkReadModel { ProjectionVersion = "previous-canonical" }, token);
            }
            var previousDetail = await fixture.ReadAsync<PartyDetailSdkReadModel>(detailKey, token);
            var previousProcessing = await fixture.ReadAsync<PartyProcessingSdkReadModel>(processingKey, token);
            if (existing)
            {
                previousDetail.Value.ShouldNotBeNull().ProjectionVersion.ShouldBe("previous-canonical");
                previousProcessing.Value.ShouldNotBeNull().ProjectionVersion.ShouldBe("previous-canonical");
                previousDetail.ETag.ShouldNotBeNullOrWhiteSpace();
                previousProcessing.ETag.ShouldNotBeNullOrWhiteSpace();
            }
            else
            {
                previousDetail.Value.ShouldBeNull();
                previousProcessing.Value.ShouldBeNull();
            }
            string operation = "backfill-" + history.AggregateId;
            var rebuilt = (await fixture.DetailAsync(history, operation, "rebuild", token)).Outcomes.ShouldHaveSingleItem();
            rebuilt.Status.ShouldBe(ProjectionDispatchStatus.Completed, rebuilt.ReasonCode);
            var persistedDetail = await fixture.ReadAsync<PartyDetailSdkReadModel>(detailKey, token);
            var persistedProcessing = await fixture.ReadAsync<PartyProcessingSdkReadModel>(processingKey, token);
            persistedDetail.ETag.ShouldNotBeNullOrWhiteSpace();
            persistedProcessing.ETag.ShouldNotBeNullOrWhiteSpace();
            if (existing)
            {
                persistedDetail.ETag.ShouldNotBe(previousDetail.ETag);
                persistedProcessing.ETag.ShouldNotBe(previousProcessing.ETag);
            }
            var detail = persistedDetail.Value.ShouldNotBeNull();
            detail.LastSequenceNumber.ShouldBe(3);
            var processing = persistedProcessing.Value.ShouldNotBeNull();
            processing.LastSequenceNumber.ShouldBe(3);
            if (history.AggregateId == "party-erased")
            {
                detail.Detail.ShouldNotBeNull().IsErased.ShouldBeTrue();
                detail.Detail.DisplayName.ShouldBeEmpty();
                detail.Detail.PersonDetails.ShouldBeNull();
                detail.ErasureSequenceNumber.ShouldBe(2);
                processing.Records.Select(record => record.SequenceNumber).ShouldBe([1, 2]);
                processing.ErasureSequenceNumber.ShouldBe(2);
            }
            else
            {
                detail.Detail.ShouldNotBeNull().DisplayName.ShouldBe("Synthetic Updated");
                detail.Detail.IsActive.ShouldBeFalse();
                processing.Records.Select(record => record.SequenceNumber).ShouldBe([1, 2, 3]);
            }
            (await fixture.RawAsync(detailKey, token)).ShouldNotBeEmpty();
            (await fixture.RawAsync(processingKey, token)).ShouldNotBeEmpty();
        }

        const string indexOperation = "backfill-index";
        string indexKey = PartySdkReadModelAddresses.Index(fixture.Tenant);
        if (existing) await fixture.SaveAsync(indexKey, new PartyIndexSdkReadModel { ProjectionVersion = "previous-canonical" }, token);
        var previousIndex = await fixture.ReadAsync<PartyIndexSdkReadModel>(indexKey, token);
        if (existing) previousIndex.ETag.ShouldNotBeNullOrWhiteSpace();
        (await fixture.PrepareIndexAsync(indexOperation, token)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await fixture.IndexAsync(indexOperation, DomainSharedProjectionRebuildAction.Stage, token)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        var stagedIndex = await fixture.ReadAsync<PartyIndexSdkReadModel>(indexKey, token);
        if (existing) stagedIndex.Value.ShouldNotBeNull().ProjectionVersion.ShouldBe("previous-canonical");
        else stagedIndex.Value.ShouldBeNull();
        (await fixture.IndexAsync(indexOperation, DomainSharedProjectionRebuildAction.Commit, token)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await fixture.IndexAsync(indexOperation, DomainSharedProjectionRebuildAction.Verify, token)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        var persistedIndex = await fixture.ReadAsync<PartyIndexSdkReadModel>(indexKey, token);
        persistedIndex.ETag.ShouldNotBeNullOrWhiteSpace();
        if (existing) persistedIndex.ETag.ShouldNotBe(previousIndex.ETag);
        var index = persistedIndex.Value.ShouldNotBeNull();
        index.Entries.Keys.ShouldBe(["party-live"]);
        index.Entries["party-live"].DisplayName.ShouldBe("Synthetic Updated");
        index.Entries["party-live"].IsActive.ShouldBeFalse();
        index.LastSequenceNumbers["party-live"].ShouldBe(3);
        index.ErasureSequenceNumbers["party-erased"].ShouldBe(2);
        (await fixture.RawAsync(indexKey, token)).ShouldNotBeEmpty();
        foreach ((string key, byte[] bytes) in fixture.Survivors)
        {
            (await fixture.RawAsync(key, token)).ShouldBe(bytes);
        }
    }

    [Theory(Timeout = 120000)]
    [InlineData("detail", false)]
    [InlineData("detail", true)]
    [InlineData("processing", false)]
    [InlineData("processing", true)]
    [InlineData("index", false)]
    [InlineData("index", true)]
    public async Task ActualProviderCompetingWriteAfterPreparationRefusesPublicationAndPreservesEveryCanonicalKey(string target, bool existing)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var fixture = new PartyLegacyBackfillRuntimeFixture(await PartyLegacyBackfillRuntimeFixture.ConfigurationAsync(token));
        await fixture.SeedAsync(token);
        const string operation = "conflicting-backfill";
        ProjectionRequest history = fixture.Histories[0];
        string detailKey = PartySdkReadModelAddresses.Detail(fixture.Tenant, history.AggregateId);
        string processingKey = PartySdkReadModelAddresses.Processing(fixture.Tenant, history.AggregateId);
        string indexKey = PartySdkReadModelAddresses.Index(fixture.Tenant);
        if (existing)
        {
            await fixture.SaveAsync(detailKey, new PartyDetailSdkReadModel { ProjectionVersion = "previous-canonical" }, token);
            await fixture.SaveAsync(processingKey, new PartyProcessingSdkReadModel { ProjectionVersion = "previous-canonical" }, token);
            await fixture.SaveAsync(indexKey, new PartyIndexSdkReadModel { ProjectionVersion = "previous-canonical" }, token);
        }
        ReadModelEntry<PartyDetailSdkReadModel> previousDetail = await fixture.ReadAsync<PartyDetailSdkReadModel>(detailKey, token);
        ReadModelEntry<PartyProcessingSdkReadModel> previousProcessing = await fixture.ReadAsync<PartyProcessingSdkReadModel>(processingKey, token);
        ReadModelEntry<PartyIndexSdkReadModel> previousIndex = await fixture.ReadAsync<PartyIndexSdkReadModel>(indexKey, token);
        string key = target switch { "detail" => detailKey, "processing" => processingKey, _ => indexKey };
        string? previousEtag = target switch { "detail" => previousDetail.ETag, "processing" => previousProcessing.ETag, _ => previousIndex.ETag };
        if (existing) previousEtag.ShouldNotBeNullOrWhiteSpace();
        else previousEtag.ShouldBeNullOrEmpty();
        var expected = new Dictionary<string, byte[]?>
        {
            [detailKey] = existing ? await fixture.RawAsync(detailKey, token) : null,
            [processingKey] = existing ? await fixture.RawAsync(processingKey, token) : null,
            [indexKey] = existing ? await fixture.RawAsync(indexKey, token) : null,
        };
        bool competed = false;
        async Task Compete(ReadModelBatch batch, CancellationToken writeToken)
        {
            batch.Operations.ShouldContain(item => item.Key == key && item.Concurrency.ExpectedETag == (previousEtag ?? string.Empty));
            bool saved = target switch
            {
                "detail" => await fixture.TrySaveAsync(key, new PartyDetailSdkReadModel { ProjectionVersion = "competing-canonical-winner" }, previousEtag ?? string.Empty, writeToken).ConfigureAwait(false),
                "processing" => await fixture.TrySaveAsync(key, new PartyProcessingSdkReadModel { ProjectionVersion = "competing-canonical-winner" }, previousEtag ?? string.Empty, writeToken).ConfigureAwait(false),
                _ => await fixture.TrySaveAsync(key, new PartyIndexSdkReadModel { ProjectionVersion = "competing-canonical-winner" }, previousEtag ?? string.Empty, writeToken).ConfigureAwait(false),
            };
            saved.ShouldBeTrue();
            expected[key] = await fixture.RawAsync(key, writeToken).ConfigureAwait(false);
            competed = true;
        }
        if (target == "index")
        {
            (await fixture.PrepareIndexAsync(operation, token)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
            var refused = await fixture.IndexAsync(operation, DomainSharedProjectionRebuildAction.Stage, token, Compete);
            refused.Status.ShouldBe(ProjectionDispatchStatus.Failed);
            refused.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        }
        else
        {
            var refused = (await fixture.DetailAsync(history, operation, "stage", token, Compete)).Outcomes.ShouldHaveSingleItem();
            refused.Status.ShouldBe(ProjectionDispatchStatus.Failed);
            refused.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.MalformedOutcome);
        }
        competed.ShouldBeTrue();
        foreach ((string canonicalKey, byte[]? bytes) in expected)
        {
            if (bytes is null) (await fixture.ReadAsync<object>(canonicalKey, token)).Value.ShouldBeNull();
            else (await fixture.RawAsync(canonicalKey, token)).ShouldBe(bytes);
        }
        foreach ((string survivorKey, byte[] bytes) in fixture.Survivors)
        {
            (await fixture.RawAsync(survivorKey, token)).ShouldBe(bytes);
        }
    }
}
