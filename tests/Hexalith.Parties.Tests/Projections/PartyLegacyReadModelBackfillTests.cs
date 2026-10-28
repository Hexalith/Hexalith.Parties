using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.Parties.Projections.Models;

using Shouldly;

namespace Hexalith.Parties.Tests.Projections;

/// <summary>Rehearses actor-only to SDK backfill through real SDK dispatch and persisted rebuild sessions.</summary>
public sealed class PartyLegacyReadModelBackfillTests
{
    [Fact]
    public async Task ActorOnlyHistoryBackfillSurvivesRestartAndPreservesRollbackAndForeignTenantBytes()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var fixture = new PartyLegacyBackfillFixture();
        await fixture.SeedAsync(token);
        fixture.Survivors.Count.ShouldBe(9);
        foreach (var history in fixture.Histories)
        {
            (await fixture.Store.GetAsync<PartyDetailSdkReadModel>(PartyLegacyBackfillFixture.StoreName,
                PartySdkReadModelAddresses.Detail(history.TenantId, history.AggregateId), token)).Value.ShouldBeNull();
            (await fixture.Store.GetAsync<PartyProcessingSdkReadModel>(PartyLegacyBackfillFixture.StoreName,
                PartySdkReadModelAddresses.Processing(history.TenantId, history.AggregateId), token)).Value.ShouldBeNull();
            (await fixture.DetailAsync(history, "backfill-" + history.AggregateId, token))
                .Outcomes.ShouldHaveSingleItem().Status.ShouldBe(ProjectionDispatchStatus.Completed);
            fixture = fixture.Restart();
        }
        const string operation = "tenant-backfill";
        var current = await fixture.AccumulateAsync(operation, token);
        current.Status.ShouldBe(ProjectionDispatchStatus.Completed, current.ReasonCode);
        current.AcceptedAggregateCount.ShouldBe(2);
        (await fixture.FinalizeAsync(operation, current, token)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await fixture.TransitionAsync(operation, DomainSharedProjectionRebuildAction.Stage, token)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await fixture.Store.GetAsync<PartyIndexSdkReadModel>(PartyLegacyBackfillFixture.StoreName,
            PartySdkReadModelAddresses.Index("tenant-a"), token)).Value.ShouldBeNull();
        fixture = fixture.Restart();
        (await fixture.TransitionAsync(operation, DomainSharedProjectionRebuildAction.Commit, token)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        fixture = fixture.Restart();
        var verified = await fixture.TransitionAsync(operation, DomainSharedProjectionRebuildAction.Verify, token);
        verified.Status.ShouldBe(ProjectionDispatchStatus.Completed, verified.ReasonCode);
        var detail = (await fixture.Store.GetAsync<PartyDetailSdkReadModel>(PartyLegacyBackfillFixture.StoreName,
            PartySdkReadModelAddresses.Detail("tenant-a", "party-live"), token)).Value.ShouldNotBeNull();
        detail.Detail.ShouldNotBeNull().DisplayName.ShouldBe("Synthetic Updated");
        detail.Detail.IsActive.ShouldBeFalse();
        detail.LastSequenceNumber.ShouldBe(3);
        var erased = (await fixture.Store.GetAsync<PartyDetailSdkReadModel>(PartyLegacyBackfillFixture.StoreName,
            PartySdkReadModelAddresses.Detail("tenant-a", "party-erased"), token)).Value.ShouldNotBeNull();
        erased.Detail.ShouldNotBeNull().IsErased.ShouldBeTrue();
        erased.Detail.PersonDetails.ShouldBeNull();
        erased.Detail.DisplayName.ShouldBeEmpty();
        erased.ErasureSequenceNumber.ShouldBe(2);
        foreach (var history in fixture.Histories)
        {
            var processing = (await fixture.Store.GetAsync<PartyProcessingSdkReadModel>(PartyLegacyBackfillFixture.StoreName,
                PartySdkReadModelAddresses.Processing("tenant-a", history.AggregateId), token)).Value.ShouldNotBeNull();
            processing.LastSequenceNumber.ShouldBe(3);
            if (history.AggregateId == "party-erased")
            {
                processing.Records.Select(record => record.SequenceNumber).ShouldBe([1, 2]);
                processing.ErasureSequenceNumber.ShouldBe(2);
            }
            else
            {
                processing.Records.Select(record => record.SequenceNumber).ShouldBe([1, 2, 3]);
            }
        }
        var index = (await fixture.Store.GetAsync<PartyIndexSdkReadModel>(PartyLegacyBackfillFixture.StoreName,
            PartySdkReadModelAddresses.Index("tenant-a"), token)).Value.ShouldNotBeNull();
        index.Entries.Keys.ShouldBe(["party-live"]);
        index.Entries["party-live"].DisplayName.ShouldBe(detail.Detail.DisplayName);
        index.Entries["party-live"].IsActive.ShouldBe(detail.Detail.IsActive);
        index.LastSequenceNumbers["party-live"].ShouldBe(3);
        index.ErasureSequenceNumbers["party-erased"].ShouldBe(2);
        AssertSurvivors(fixture);
    }

    [Fact]
    public async Task ExistingIndexConditionalPolicySurvivesCommitAndFreshVerification()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var fixture = new PartyLegacyBackfillFixture();
        await fixture.SeedAsync(token);
        await fixture.Store.SaveAsync(PartyLegacyBackfillFixture.StoreName, PartySdkReadModelAddresses.Index("tenant-a"),
            new PartyIndexSdkReadModel { ProjectionVersion = "legacy-sdk-index" }, token);
        const string operation = "existing-index-backfill";
        var current = await fixture.AccumulateAsync(operation, token);
        current.Status.ShouldBe(ProjectionDispatchStatus.Completed, current.ReasonCode);
        current.AcceptedAggregateCount.ShouldBe(2);
        (await fixture.FinalizeAsync(operation, current, token)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await fixture.TransitionAsync(operation, DomainSharedProjectionRebuildAction.Stage, token)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        fixture = fixture.Restart();
        (await fixture.TransitionAsync(operation, DomainSharedProjectionRebuildAction.Commit, token)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        fixture = fixture.Restart();
        var verified = await fixture.TransitionAsync(operation, DomainSharedProjectionRebuildAction.Verify, token);
        verified.Status.ShouldBe(ProjectionDispatchStatus.Completed, verified.ReasonCode);
        var persisted = (await fixture.Store.GetAsync<PartyIndexSdkReadModel>(PartyLegacyBackfillFixture.StoreName,
            PartySdkReadModelAddresses.Index("tenant-a"), token)).Value.ShouldNotBeNull();
        persisted.Entries.Keys.ShouldBe(["party-live"]);
        persisted.ErasureSequenceNumbers["party-erased"].ShouldBe(2);
        AssertSurvivors(fixture);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IncompleteOrUnreadableHistoryRefusesPublicationAndPreservesActorAuthority(bool unreadable)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var fixture = new PartyLegacyBackfillFixture();
        await fixture.SeedAsync(token);
        var history = fixture.Histories[0];
        var unsafeHistory = history with
        {
            Events = unreadable
                ? [history.Events[0] with { SerializationFormat = "json+pdenc-v1" }, .. history.Events[1..]]
                : [history.Events[0], history.Events[2]],
        };
        var failure = await Record.ExceptionAsync(async () =>
        {
            (await fixture.DetailAsync(unsafeHistory, "unsafe", token).ConfigureAwait(false)).Outcomes
                .ShouldAllBe(outcome => outcome.Status != ProjectionDispatchStatus.Completed && outcome.Status != ProjectionDispatchStatus.AlreadyCompleted);
        });
        if (failure is not null) failure.GetType().Name.ShouldBe("ProjectionDispatchValidationException");
        (await fixture.Store.GetAsync<PartyDetailSdkReadModel>(PartyLegacyBackfillFixture.StoreName,
            PartySdkReadModelAddresses.Detail("tenant-a", "party-live"), token)).Value.ShouldBeNull();
        (await fixture.Store.GetAsync<PartyProcessingSdkReadModel>(PartyLegacyBackfillFixture.StoreName,
            PartySdkReadModelAddresses.Processing("tenant-a", "party-live"), token)).Value.ShouldBeNull();
        (await fixture.Store.GetAsync<PartyIndexSdkReadModel>(PartyLegacyBackfillFixture.StoreName,
            PartySdkReadModelAddresses.Index("tenant-a"), token)).Value.ShouldBeNull();
        AssertSurvivors(fixture);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompetingCanonicalWriterAfterPreparationRefusesBackfillWithoutOverwritingAuthority(bool shared)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var fixture = new PartyLegacyBackfillFixture();
        await fixture.SeedAsync(token);
        const string operation = "competing-backfill";
        if (shared)
        {
            var current = await fixture.AccumulateAsync(operation, token);
            current.Status.ShouldBe(ProjectionDispatchStatus.Completed, current.ReasonCode);
            current.AcceptedAggregateCount.ShouldBe(2);
            (await fixture.FinalizeAsync(operation, current, token)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
            (await fixture.TransitionAsync(operation, DomainSharedProjectionRebuildAction.Stage, token)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
            string key = PartySdkReadModelAddresses.Index("tenant-a");
            await fixture.Store.SaveAsync(PartyLegacyBackfillFixture.StoreName, key,
                new PartyIndexSdkReadModel { ProjectionVersion = "competing-live-authority" }, token);
            byte[] authority = fixture.Store.SnapshotRaw(PartyLegacyBackfillFixture.StoreName, key)!;
            fixture = fixture.Restart();
            var refused = await fixture.TransitionAsync(operation, DomainSharedProjectionRebuildAction.Commit, token);
            refused.Status.ShouldNotBe(ProjectionDispatchStatus.Completed);
            refused.Status.ShouldNotBe(ProjectionDispatchStatus.AlreadyCompleted);
            fixture.Store.SnapshotRaw(PartyLegacyBackfillFixture.StoreName, key).ShouldBe(authority);
        }
        else
        {
            var history = fixture.Histories[0];
            (await fixture.DetailTransitionAsync(history, operation, "stage", token)).Outcomes
                .ShouldHaveSingleItem().Status.ShouldBe(ProjectionDispatchStatus.Completed);
            string detailKey = PartySdkReadModelAddresses.Detail("tenant-a", "party-live");
            string processingKey = PartySdkReadModelAddresses.Processing("tenant-a", "party-live");
            await fixture.Store.SaveAsync(PartyLegacyBackfillFixture.StoreName, detailKey,
                new PartyDetailSdkReadModel { LastSequenceNumber = 99, ProjectionVersion = "competing-live-authority" }, token);
            await fixture.Store.SaveAsync(PartyLegacyBackfillFixture.StoreName, processingKey,
                new PartyProcessingSdkReadModel { LastSequenceNumber = 99, ProjectionVersion = "competing-live-authority" }, token);
            byte[] detail = fixture.Store.SnapshotRaw(PartyLegacyBackfillFixture.StoreName, detailKey)!;
            byte[] processing = fixture.Store.SnapshotRaw(PartyLegacyBackfillFixture.StoreName, processingKey)!;
            fixture = fixture.Restart();
            (await fixture.DetailTransitionAsync(history, operation, "commit", token)).Outcomes.ShouldAllBe(outcome =>
                outcome.Status != ProjectionDispatchStatus.Completed && outcome.Status != ProjectionDispatchStatus.AlreadyCompleted);
            fixture.Store.SnapshotRaw(PartyLegacyBackfillFixture.StoreName, detailKey).ShouldBe(detail);
            fixture.Store.SnapshotRaw(PartyLegacyBackfillFixture.StoreName, processingKey).ShouldBe(processing);
        }
        AssertSurvivors(fixture);
    }

    private static void AssertSurvivors(PartyLegacyBackfillFixture fixture)
    {
        foreach ((string key, byte[] value) in fixture.Survivors)
        {
            fixture.Store.SnapshotRaw(PartyLegacyBackfillFixture.StoreName, key).ShouldBe(value);
        }
    }
}
