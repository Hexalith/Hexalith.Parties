using Dapr.Client;

using Hexalith.Parties.Search;
using Hexalith.Parties.Testing;

using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.Parties.Tests.Search;

public sealed class PartyMemoryMappingConcurrencyTests
{
    private static readonly PartyMemoryUnitMappingEntry _cleaned = new("unit-old", "urn:old", "case-old");
    private static readonly PartyMemoryUnitMappingEntry _retained = new("unit-retained", "urn:retained", "case-old");
    private static readonly PartyMemoryUnitMappingEntry _new = new("unit-new", "urn:new", "case-new");

    [Fact]
    public async Task RemovePreservesEntriesAddedAfterCleanupSnapshot()
    {
        var state = new PartyMemoryMappingState(_cleaned, _new);
        PartyMemoryUnitMappingStore store = CreateStore(state);

        int retained = await store.RemoveMappingsAsync("tenant", "party", [_cleaned], TestContext.Current.CancellationToken);

        retained.ShouldBe(1);
        state.Entries.ShouldBe([_new]);
        state.LastConcurrency.ShouldBe(ConcurrencyMode.FirstWrite);
    }

    [Fact]
    public async Task RemoveRecomputesSubtractionAfterConcurrentIndexingSave()
    {
        var state = new PartyMemoryMappingState(_cleaned, _retained);
        PartyMemoryUnitMappingStore store = CreateStore(state);
        state.BeforeSave = () => store.RecordMappingAsync(
            "tenant", "party", _new.MemoryUnitId, _new.SourceUri, "case-new", TestContext.Current.CancellationToken);

        int retained = await store.RemoveMappingsAsync("tenant", "party", [_cleaned], TestContext.Current.CancellationToken);

        retained.ShouldBe(2);
        state.Entries.ShouldBe([_retained, _new]);
        state.SaveAttempts.ShouldBe(3);
        state.LastConcurrency.ShouldBe(ConcurrencyMode.FirstWrite);
    }

    [Fact]
    public async Task RemovePreservesConcurrentIndexingWhenEmptyInventorySaveConflicts()
    {
        var state = new PartyMemoryMappingState(_cleaned);
        PartyMemoryUnitMappingStore store = CreateStore(state);
        state.BeforeSave = () => store.RecordMappingAsync(
            "tenant", "party", _new.MemoryUnitId, _new.SourceUri, "case-new", TestContext.Current.CancellationToken);

        int retained = await store.RemoveMappingsAsync("tenant", "party", [_cleaned], TestContext.Current.CancellationToken);

        retained.ShouldBe(1);
        state.KeyExists.ShouldBeTrue();
        state.Entries.ShouldBe([_new]);
        state.DeleteAttempts.ShouldBe(0);
        state.SaveAttempts.ShouldBe(3);
    }

    [Theory]
    [InlineData("unit-new", "urn:old", "case-old")]
    [InlineData("unit-old", "urn:new", "case-old")]
    [InlineData("unit-old", "urn:old", "case-new")]
    public async Task RemovePreservesChangedMappingIdentity(string unitId, string source, string caseId)
    {
        var replacement = new PartyMemoryUnitMappingEntry(unitId, source, caseId);
        var state = new PartyMemoryMappingState(_cleaned);
        state.BeforeSave = () =>
        {
            state.SetEntries([replacement]);
            return Task.CompletedTask;
        };

        int retained = await CreateStore(state).RemoveMappingsAsync(
            "tenant", "party", [_cleaned], TestContext.Current.CancellationToken);

        retained.ShouldBe(1);
        state.Entries.ShouldBe([replacement]);
        state.DeleteAttempts.ShouldBe(0);
        state.SaveAttempts.ShouldBe(1);
    }

    [Fact]
    public async Task OverlappingCleanupAndIndexingPreserveTheNewLedgerGeneration()
    {
        var state = new PartyMemoryMappingState(_cleaned);
        PartyMemoryUnitMappingStore store = CreateStore(state);
        state.BeforeMutation = async () =>
        {
            _ = await store.RemoveMappingsAsync("tenant", "party", [_cleaned], TestContext.Current.CancellationToken).ConfigureAwait(false);
            await store.RecordMappingAsync(
                "tenant", "party", _new.MemoryUnitId, _new.SourceUri, "case-new", TestContext.Current.CancellationToken).ConfigureAwait(false);
        };

        int retained = await store.RemoveMappingsAsync("tenant", "party", [_cleaned], TestContext.Current.CancellationToken);

        retained.ShouldBe(1);
        state.KeyExists.ShouldBeTrue();
        state.Entries.ShouldBe([_new]);
        state.DeleteAttempts.ShouldBe(0);
        state.SaveAttempts.ShouldBe(3);
    }

    [Fact]
    public async Task RemoveDeletesOnlyTheExactCaseAndSource()
    {
        var otherCase = _cleaned with { CaseId = "case-other" };
        var otherSource = _cleaned with { SourceUri = "urn:other" };
        var legacy = _cleaned with { CaseId = null };
        var state = new PartyMemoryMappingState(_cleaned, otherCase, otherSource, legacy);

        int retained = await CreateStore(state).RemoveMappingsAsync(
            "tenant", "party", [_cleaned], TestContext.Current.CancellationToken);

        retained.ShouldBe(3);
        state.Entries.ShouldBe([otherCase, otherSource, legacy]);
    }

    [Fact]
    public async Task RemoveRetainsAnEmptyLedgerGenerationAndRepeatedRemovalIsANoop()
    {
        var state = new PartyMemoryMappingState(_cleaned);
        PartyMemoryUnitMappingStore store = CreateStore(state);

        int first = await store.RemoveMappingsAsync("tenant", "party", [_cleaned], TestContext.Current.CancellationToken);
        int second = await store.RemoveMappingsAsync("tenant", "party", [_cleaned], TestContext.Current.CancellationToken);

        first.ShouldBe(0);
        second.ShouldBe(0);
        state.KeyExists.ShouldBeTrue();
        state.Entries.ShouldBeEmpty();
        state.DeleteAttempts.ShouldBe(0);
        state.SaveAttempts.ShouldBe(1);
        state.LastConcurrency.ShouldBe(ConcurrencyMode.FirstWrite);
    }

    [Fact]
    public async Task ClearPreservesEntriesAddedAfterItsInitialSnapshot()
    {
        var state = new PartyMemoryMappingState(_cleaned);
        state.BeforeDelete = () =>
        {
            state.SetEntries([_cleaned, _new]);
            return Task.CompletedTask;
        };

        await CreateStore(state).ClearMappingsAsync("_health-probe", "_health-probe-00000000000000000000000000000001", TestContext.Current.CancellationToken);

        state.Entries.ShouldBe([_new]);
        state.DeleteAttempts.ShouldBe(1);
        state.SaveAttempts.ShouldBe(1);
    }

    [Fact]
    public async Task ReservedProbeClearReclaimsItsUniqueKey()
    {
        var state = new PartyMemoryMappingState(_cleaned);

        await CreateStore(state).ClearMappingsAsync(
            "_health-probe", "_health-probe-00000000000000000000000000000001", TestContext.Current.CancellationToken);

        state.KeyExists.ShouldBeFalse();
        state.DeleteAttempts.ShouldBe(1);
        state.LastConcurrency.ShouldBe(ConcurrencyMode.FirstWrite);
    }

    [Theory]
    [InlineData("tenant", "party")]
    [InlineData("tenant", "_health-probe-00000000000000000000000000000001")]
    [InlineData("_health-probe", "party")]
    [InlineData("_health-probe", "_health-probe-not-a-guid")]
    public async Task PhysicalClearRejectsDomainOrReusableProbeKeys(string tenant, string party)
    {
        var state = new PartyMemoryMappingState(_cleaned);

        await Should.ThrowAsync<ArgumentException>(() =>
            CreateStore(state).ClearMappingsAsync(tenant, party, TestContext.Current.CancellationToken));

        state.Entries.ShouldBe([_cleaned]);
        state.ReadCount.ShouldBe(0);
        (state.SaveAttempts + state.DeleteAttempts).ShouldBe(0);
    }

    [Fact]
    public async Task OverlappingCleanupOfTheSameSnapshotIsIdempotent()
    {
        var state = new PartyMemoryMappingState(_cleaned);
        PartyMemoryUnitMappingStore store = CreateStore(state);
        int innerRetained = -1;
        state.BeforeMutation = async () => innerRetained = await store.RemoveMappingsAsync(
            "tenant", "party", [_cleaned], TestContext.Current.CancellationToken).ConfigureAwait(false);

        int retained = await store.RemoveMappingsAsync("tenant", "party", [_cleaned], TestContext.Current.CancellationToken);

        retained.ShouldBe(0);
        innerRetained.ShouldBe(0);
        state.KeyExists.ShouldBeTrue();
        state.Entries.ShouldBeEmpty();
        state.DeleteAttempts.ShouldBe(0);
        state.SaveAttempts.ShouldBe(2);
    }

    [Fact]
    public async Task RemovalLeavesOtherTenantsAndPartiesUntouched()
    {
        var state = new PartyMemoryMappingState(_cleaned);
        PartyMemoryUnitMappingStore store = CreateStore(state);
        CancellationToken token = TestContext.Current.CancellationToken;
        _ = await store.GetMappingsAsync("tenant", "party", token);
        await store.RecordMappingAsync("tenant-other", "party", _new.MemoryUnitId, _new.SourceUri, "case-new", token);
        await store.RecordMappingAsync("tenant", "party-other", _retained.MemoryUnitId, _retained.SourceUri, "case-old", token);

        int retained = await store.RemoveMappingsAsync("tenant", "party", [_cleaned], token);

        retained.ShouldBe(0);
        IReadOnlyList<PartyMemoryUnitMappingEntry> otherTenant = await store.GetMappingsAsync("tenant-other", "party", token);
        otherTenant.ShouldBe([_new]);
        IReadOnlyList<PartyMemoryUnitMappingEntry> otherParty = await store.GetMappingsAsync("tenant", "party-other", token);
        otherParty.ShouldBe([_retained]);
        state.Entries.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RemoveConflictsAreBoundedAndLeaveInventoryIntact(bool retainOtherEntry)
    {
        var state = new PartyMemoryMappingState(retainOtherEntry ? [_cleaned, _retained] : [_cleaned]) { RejectWrites = true };
        var logger = new RecordingLogger<PartyMemoryUnitMappingStore>();

        InvalidOperationException error = await Should.ThrowAsync<InvalidOperationException>(() =>
            CreateStore(state, logger).RemoveMappingsAsync("tenant", "party", [_cleaned], TestContext.Current.CancellationToken));

        error.Message.ShouldBe("memory-unit-mapping-remove-conflict");
        error.InnerException.ShouldBeNull();
        state.Entries.ShouldBe(retainOtherEntry ? [_cleaned, _retained] : [_cleaned]);
        (state.SaveAttempts + state.DeleteAttempts).ShouldBe(5);
        state.ReadCount.ShouldBe(5);
        logger.Records.Count.ShouldBe(5);
        logger.Records[^1].Message.ShouldContain("5 attempts");
        logger.Records[^1].Message.ShouldNotContain("retrying");
        logger.Records.ShouldAllBe(record => record.Exception == null
            && !record.Message.Contains("unit-old", StringComparison.Ordinal)
            && !record.Message.Contains("urn:old", StringComparison.Ordinal)
            && !record.Message.Contains("case-old", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RemoveFailsClosedWithoutAnETag(bool retainOtherEntry)
    {
        var state = new PartyMemoryMappingState(retainOtherEntry ? [_cleaned, _retained] : [_cleaned]) { MissingETag = true };

        InvalidOperationException error = await Should.ThrowAsync<InvalidOperationException>(() =>
            CreateStore(state).RemoveMappingsAsync("tenant", "party", [_cleaned], TestContext.Current.CancellationToken));

        error.Message.ShouldBe("memory-unit-mapping-remove-missing-etag");
        state.Entries.ShouldBe(retainOtherEntry ? [_cleaned, _retained] : [_cleaned]);
        (state.SaveAttempts + state.DeleteAttempts).ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RemovePropagatesCallerCancellationDuringAConditionalWrite(bool retainOtherEntry)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var state = new PartyMemoryMappingState(retainOtherEntry ? [_cleaned, _retained] : [_cleaned]);
        Func<Task> cancelDuringWrite = () =>
        {
            cancellation.Cancel();
            return Task.FromCanceled(cancellation.Token);
        };
        state.BeforeSave = cancelDuringWrite;

        var logger = new RecordingLogger<PartyMemoryUnitMappingStore>();

        await Should.ThrowAsync<OperationCanceledException>(() =>
            CreateStore(state, logger).RemoveMappingsAsync("tenant", "party", [_cleaned], cancellation.Token));

        state.Entries.ShouldBe(retainOtherEntry ? [_cleaned, _retained] : [_cleaned]);
        (state.SaveAttempts + state.DeleteAttempts).ShouldBe(1);
        logger.Records.ShouldBeEmpty();
    }

    private static PartyMemoryUnitMappingStore CreateStore(
        PartyMemoryMappingState state,
        RecordingLogger<PartyMemoryUnitMappingStore>? logger = null) => new(
            state.Client,
            Options.Create(new PartyMemoryUnitMappingStoreOptions { StateStoreName = "statestore" }),
            logger ?? new RecordingLogger<PartyMemoryUnitMappingStore>());
}
