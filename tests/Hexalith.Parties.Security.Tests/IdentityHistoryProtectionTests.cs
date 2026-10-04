using System.Text.Json;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.State;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Hexalith.Parties.Security.Tests;

public sealed class IdentityHistoryProtectionTests
{
    [Fact]
    public async Task SerializedHistorySnapshot_IsDeniedInsteadOfProfileOnlyProtection()
    {
        PartyState state = State();
        var custody = Substitute.For<IIdentityHistoryCustody>();
        var service = Service(Substitute.For<IPartyKeyManagementService>(), Substitute.For<IKeyStorageBackend>(), custody);
        JsonElement serialized = JsonSerializer.SerializeToElement(state, PartiesJsonOptions.Default);
        await Should.ThrowAsync<InvalidOperationException>(() => service.ProtectSnapshotStateAsync(new("tenant-a", "party", "party-1"), serialized, TestContext.Current.CancellationToken));
        await custody.DidNotReceiveWithAnyArgs().ProtectSnapshotAsync(default!, default!, default);
    }

    [Fact]
    public async Task TypedSnapshot_RetainedHistoryRequiresCustodyAndSurvivesProfileKeyErasure()
    {
        IPartyKeyManagementService keys = Substitute.For<IPartyKeyManagementService>();
        IKeyStorageBackend backend = Substitute.For<IKeyStorageBackend>();
        backend.ListKeyVersionsAsync("tenant-a", "party-1", Arg.Any<CancellationToken>()).Returns([1]);
        keys.GetKeyVersionAsync("tenant-a", "party-1", 1, Arg.Any<CancellationToken>()).Returns(_ => Enumerable.Repeat((byte)7, 32).ToArray());
        IIdentityHistoryCustody custody = Substitute.For<IIdentityHistoryCustody>();
        // Synthetic provider only verifies purpose dispatch and profile separation; this is not live custody qualification.
        custody.ProtectSnapshotAsync(Arg.Any<AggregateIdentity>(), Arg.Any<object>(), Arg.Any<CancellationToken>()).Returns(call => call.Arg<object>());
        custody.UnprotectSnapshotAsync(Arg.Any<AggregateIdentity>(), Arg.Any<object>(), Arg.Any<CancellationToken>()).Returns(call => call.Arg<object>());
        var service = Service(keys, backend, custody);
        object protectedState = await service.ProtectSnapshotStateAsync(new("tenant-a", "party", "party-1"), State(), TestContext.Current.CancellationToken);
        protectedState.ShouldBeOfType<IdentityHistorySnapshot>();
        keys.GetKeyVersionAsync("tenant-a", "party-1", 1, Arg.Any<CancellationToken>()).Returns<byte[]>(_ => throw new PartyEncryptionKeyDestroyedException("tenant-a", "party-1"));
        object restored = await service.UnprotectSnapshotStateAsync(new("tenant-a", "party", "party-1"), protectedState, TestContext.Current.CancellationToken);
        JsonElement json = restored.ShouldBeOfType<JsonElement>();
        json.GetProperty("humanActorBindings").GetArrayLength().ShouldBe(1);
        json.GetProperty("person").GetProperty("firstName").ValueKind.ShouldBe(JsonValueKind.Null);
        await custody.Received(1).UnprotectSnapshotAsync(Arg.Any<AggregateIdentity>(), Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProtectedHistoryFormat_UsesCustodyWithoutProfileKeysAndRejectsNoOpProtection()
    {
        IIdentityHistoryCustody custody = Substitute.For<IIdentityHistoryCustody>();
        var inner = Service(Substitute.For<IPartyKeyManagementService>(), Substitute.For<IKeyStorageBackend>(), custody);
        var adapter = new EventStorePartyPayloadProtectionAdapter(inner, Substitute.For<IPartyErasureRecordStore>());
        var metadata = new EventStorePayloadProtectionMetadata(PayloadProtectionState.Protected, 1, "party-actor-history-v1", null, "application/json", null);
        custody.UnprotectEventAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PayloadProtectionResult("{}"u8.ToArray(), "json"));
        PayloadUnprotectionOutcome read = await adapter.TryUnprotectEventPayloadAsync(new("tenant-a", "party", "party-1"), typeof(HumanActorBindingEstablished).FullName!,
            "opaque"u8.ToArray(), "json+identity-history-v1", metadata, TestContext.Current.CancellationToken);
        read.IsUnreadable.ShouldBeFalse();
        HumanActorBindingEstablished history = new(State().HumanActorBindings[0], DateTimeOffset.UnixEpoch, 0);
        custody.ProtectEventAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<IdentityHistoryCustodyEvidence>(), Arg.Any<CancellationToken>())
            .Returns(new PayloadProtectionResult("{}"u8.ToArray(), "json"));
        await Should.ThrowAsync<InvalidOperationException>(() => inner.ProtectEventPayloadAsync(new("tenant-a", "party", "party-1"), history,
            typeof(HumanActorBindingEstablished).FullName!, "{}"u8.ToArray(), "json", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("[]", false)]
    [InlineData("5", false)]
    [InlineData("{\"marker\":17}", true)]
    [InlineData("{\"marker\":\"unknown-history\"}", true)]
    public async Task LegacyNonObjectAndMalformedMarker_AreHandledSafely(string json, bool denied)
    {
        var service = Service(Substitute.For<IPartyKeyManagementService>(), Substitute.For<IKeyStorageBackend>(), null);
        JsonElement state = JsonSerializer.Deserialize<JsonElement>(json);
        if (denied)
        {
            await Should.ThrowAsync<InvalidOperationException>(() => service.UnprotectSnapshotStateAsync(new("tenant-a", "party", "party-1"), state, TestContext.Current.CancellationToken));
        }
        else
        {
            await service.UnprotectSnapshotStateAsync(new("tenant-a", "party", "party-1"), state, TestContext.Current.CancellationToken);
        }
    }

    private static PartyState State()
    {
        var state = new PartyState();
        DateTimeOffset start = DateTimeOffset.UnixEpoch;
        state.Apply(new PartyCreated { Type = PartyType.Person, CreatedAt = start, PersonDetails = new PersonDetails { FirstName = "Private", LastName = "Name" } });
        state.Apply(new HumanActorBindingEstablished(new(new("tenant-a", "party-1", "01HX0000000000000000000001", 1, 1, start, start.AddDays(1), "writer", "operator",
            new("synthetic", "party-actor-history-v1", start.AddDays(1), 1, "custody", true, true, true)), "logical", "digest"), start, 0));
        return state;
    }
    private static PartyPayloadProtectionService Service(IPartyKeyManagementService keys, IKeyStorageBackend backend, IIdentityHistoryCustody? custody)
    {
        IOptionsMonitor<CryptoShreddingOptions> options = Substitute.For<IOptionsMonitor<CryptoShreddingOptions>>();
        options.CurrentValue.Returns(new CryptoShreddingOptions());
        return new(keys, backend, new PartyKeyLifecycleService(keys, Substitute.For<IPartyKeyRetryScheduler>(), NullLogger<PartyKeyLifecycleService>.Instance),
            new DecryptionCircuitBreaker(NullLogger<DecryptionCircuitBreaker>.Instance), options, NullLogger<PartyPayloadProtectionService>.Instance, custody);
    }
}
