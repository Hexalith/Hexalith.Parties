using Hexalith.Parties.Search;

using Shouldly;

namespace Hexalith.Parties.Tests.Search;

/// <summary>Mapping recovery preserves remote producers for canonical redelivery.</summary>
public sealed class PartyMemoryCompensationTests
{
    [Fact]
    public async Task MappingFailurePreservesProducerForSuccessfulRedeliveryAfterRestart()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var state = new PartyMemoryTestReadModelStore();
        var handler = new PartyMemoryRecoveryHandler();
        handler.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed();
        handler.Units.Add("canonical");
        using var http = PartyMemoryRecoveryTests.Http(handler);
        var client = new PartyMemoryRecoveryClient(http);
        IPartyMemoryUnitMappingStore unavailable = new IndexingCancellationMappingStore(
            (_, _, _, _, _, _) => Task.FromException(new InvalidOperationException("mapping unavailable")));

        (await PartyMemoryRecoveryTests.Indexer(state, client, unavailable).IndexAsync(
            PartyMemoryRecoveryTests.Entry(), PartyMemoryRecoveryTests.Context(), token)).ShouldNotBeNull().Indexed.ShouldBeFalse();
        handler.Requests.ShouldAllBe(request => request.Method != HttpMethod.Delete);
        handler.Units.ShouldContain("canonical");

        (await PartyMemoryRecoveryTests.Indexer(state, client).IndexAsync(
            PartyMemoryRecoveryTests.Entry(), PartyMemoryRecoveryTests.Context(), token)).ShouldNotBeNull().Indexed.ShouldBeTrue();

        client.Submissions.ShouldBe(1);
        handler.Units.ShouldContain("canonical");
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", token))
            .ShouldHaveSingleItem().MemoryUnitId.ShouldBe("canonical");
        (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", token))
            .ShouldHaveSingleItem().MemoryUnitId.ShouldBe("canonical");
    }
}
