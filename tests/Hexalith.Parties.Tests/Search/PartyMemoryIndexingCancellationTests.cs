using Hexalith.Parties.Search;

using Shouldly;

namespace Hexalith.Parties.Tests.Search;

/// <summary>Accepted ingestion remains durably recoverable independently of caller cancellation.</summary>
public sealed class PartyMemoryIndexingCancellationTests
{
    [Fact]
    public async Task LostMappingAcknowledgementAfterCommitRetainsCanonicalRecoveryAndCancellation()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(token);
        var state = new PartyMemoryTestReadModelStore();
        var handler = new PartyMemoryRecoveryHandler();
        handler.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed();
        using var http = PartyMemoryRecoveryTests.Http(handler);
        var observed = new IndexingCancellationMappingStore(async (tenant, party, unit, source, caseId, recoveryToken) =>
        {
            await PartyMemoryRecoveryTests.Mappings(state).RecordMappingAsync(tenant, party, unit, source, caseId, recoveryToken).ConfigureAwait(false);
            caller.Cancel();
            throw new OperationCanceledException("lost-acknowledgement", caller.Token);
        });

        (await Should.ThrowAsync<OperationCanceledException>(() => PartyMemoryRecoveryTests.Indexer(state,
            new PartyMemoryRecoveryClient(http), observed).IndexAsync(PartyMemoryRecoveryTests.Entry(), PartyMemoryRecoveryTests.Context(), caller.Token)))
            .CancellationToken.ShouldBe(caller.Token);

        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", token)).ShouldHaveSingleItem().MemoryUnitId.ShouldBe("canonical");
        (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", token)).ShouldHaveSingleItem().MemoryUnitId.ShouldBe("canonical");
        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", token)).Cleaned.ShouldBeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationAfterAcceptanceRecoversWorkflowAndRethrowsCallerCancellation(bool persistenceFails)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(token);
        var state = new PartyMemoryTestReadModelStore();
        var handler = new PartyMemoryRecoveryHandler();
        handler.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed();
        using var http = PartyMemoryRecoveryTests.Http(handler);
        var client = new PartyMemoryRecoveryClient(http)
        {
            Submit = _ =>
            {
                caller.Cancel();
                if (persistenceFails)
                {
                    state.SaveFailure = new InvalidOperationException("store unavailable");
                }

                return Task.FromResult("workflow");
            },
        };

        OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(() => PartyMemoryRecoveryTests.Indexer(state, client)
            .IndexAsync(PartyMemoryRecoveryTests.Entry(), PartyMemoryRecoveryTests.Context(), caller.Token));

        error.CancellationToken.ShouldBe(caller.Token);
        state.SaveFailure = null;
        PartyMemoryIngestionIntent intent = (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", token)).ShouldHaveSingleItem();
        intent.WorkflowInstanceId.ShouldBe(persistenceFails ? null : "workflow");
        intent.MemoryUnitId.ShouldBe(persistenceFails ? null : "canonical");
        PartyMemoryCleanupResult cleaned = await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", token);
        cleaned.Cleaned.ShouldBe(!persistenceFails);
    }

    [Fact]
    public async Task CancellationDuringUnacknowledgedSubmissionRetainsUnresolvedIntent()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(token);
        var state = new PartyMemoryTestReadModelStore();
        using var http = PartyMemoryRecoveryTests.Http(new PartyMemoryRecoveryHandler());
        var original = new OperationCanceledException("caller cancelled", caller.Token);
        var client = new PartyMemoryRecoveryClient(http)
        {
            Submit = _ =>
            {
                caller.Cancel();
                return Task.FromException<string>(original);
            },
        };

        OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(() => PartyMemoryRecoveryTests.Indexer(state, client)
            .IndexAsync(PartyMemoryRecoveryTests.Entry(), PartyMemoryRecoveryTests.Context(), caller.Token));

        error.CancellationToken.ShouldBe(caller.Token);
        caller.IsCancellationRequested.ShouldBeTrue();
        (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", token)).ShouldHaveSingleItem().WorkflowInstanceId.ShouldBeNull();
        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", token)).Cleaned.ShouldBeFalse();
    }

    [Fact]
    public async Task CancellationDuringMappingRetainsCanonicalEvidenceEvenWhenRecoveryAlsoFails()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(token);
        var state = new PartyMemoryTestReadModelStore();
        var handler = new PartyMemoryRecoveryHandler();
        handler.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed();
        using var http = PartyMemoryRecoveryTests.Http(handler);
        var original = new OperationCanceledException("mapping cancelled", caller.Token);
        var mapping = new IndexingCancellationMappingStore((_, _, _, _, _, _) =>
        {
            caller.Cancel();
            state.SaveFailure = new InvalidOperationException("recovery unavailable");
            return Task.FromException(original);
        });

        OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(() => PartyMemoryRecoveryTests.Indexer(state,
            new PartyMemoryRecoveryClient(http), mapping).IndexAsync(PartyMemoryRecoveryTests.Entry(), PartyMemoryRecoveryTests.Context(), caller.Token));

        error.CancellationToken.ShouldBe(caller.Token);
        caller.IsCancellationRequested.ShouldBeTrue();
        state.SaveFailure = null;
        (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", token)).ShouldHaveSingleItem().MemoryUnitId.ShouldBe("canonical");
        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", token)).Cleaned.ShouldBeTrue();
    }
}
