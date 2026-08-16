using Dapr.Client;

using Hexalith.Parties.Search;
using Hexalith.Parties.Testing;

using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.Tests.Search;

public sealed class PartyMemoryUnitMappingStoreTests
{
    [Fact]
    public async Task GetMappingsAsync_StateReadFailurePropagatesSanitizedAndDoesNotLogIdentifiers()
    {
        DaprClient client = Substitute.For<DaprClient>();
        client.GetStateAndETagAsync<List<PartyMemoryUnitMappingEntry>>(
                "statestore",
                Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Task.FromException<(List<PartyMemoryUnitMappingEntry> Value, string ETag)>(
                new InvalidOperationException("Ada Lovelace tenant-secret party-secret")));
        var logger = new RecordingLogger<PartyMemoryUnitMappingStore>();
        var store = new PartyMemoryUnitMappingStore(
            client,
            Options.Create(new PartyMemoryUnitMappingStoreOptions { StateStoreName = "statestore" }),
            logger);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            store.GetMappingsAsync(
                "tenant-secret",
                "party-secret",
                TestContext.Current.CancellationToken));

        exception.Message.ShouldBe("memory-unit-mapping-read-failed");
        string message = logger.Records.ShouldHaveSingleItem().Message;
        message.ShouldContain(nameof(InvalidOperationException));
        message.ShouldNotContain("Ada Lovelace");
        message.ShouldNotContain("tenant-secret");
        message.ShouldNotContain("party-secret");
    }

    [Fact]
    public async Task GetMappingsAsync_StateReadCancellationPropagates()
    {
        DaprClient client = Substitute.For<DaprClient>();
        client.GetStateAndETagAsync<List<PartyMemoryUnitMappingEntry>>(
                "statestore",
                Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Task.FromException<(List<PartyMemoryUnitMappingEntry> Value, string ETag)>(
                new OperationCanceledException()));
        var store = new PartyMemoryUnitMappingStore(
            client,
            Options.Create(new PartyMemoryUnitMappingStoreOptions { StateStoreName = "statestore" }),
            new RecordingLogger<PartyMemoryUnitMappingStore>());

        await Should.ThrowAsync<OperationCanceledException>(() => store.GetMappingsAsync(
            "tenant-a",
            "party-1",
            TestContext.Current.CancellationToken));
    }
}
