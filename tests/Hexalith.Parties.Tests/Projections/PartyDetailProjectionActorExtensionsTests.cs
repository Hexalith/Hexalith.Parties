using System.Text.Json;

using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Extensions;
using Hexalith.Parties.Projections.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.Parties.Tests.Projections;

public sealed class PartyDetailProjectionActorExtensionsTests
{
    [Fact]
    public async Task ReadDetailAsync_JsonStringSuccess_ReturnsDeserializedDetailAsync()
    {
        IPartyDetailProjectionActor actor = Substitute.For<IPartyDetailProjectionActor>();
        actor.GetDetailJsonAsync().Returns(JsonSerializer.Serialize(CreateDetail("p-json"), JsonOptions));

        PartyDetail? detail = await actor.ReadDetailAsync();

        detail.ShouldNotBeNull();
        detail.Id.ShouldBe("p-json");
        await actor.DidNotReceive().GetSerializedDetailAsync();
        await actor.DidNotReceive().GetDetailAsync();
    }

    [Fact]
    public async Task ReadDetailAsync_EmptyJsonFallsThroughToSerializedBytesAsync()
    {
        IPartyDetailProjectionActor actor = Substitute.For<IPartyDetailProjectionActor>();
        actor.GetDetailJsonAsync().Returns("{}");
        actor.GetSerializedDetailAsync().Returns(JsonSerializer.SerializeToUtf8Bytes(CreateDetail("p-bytes"), JsonOptions));

        PartyDetail? detail = await actor.ReadDetailAsync();

        detail.ShouldNotBeNull();
        detail.Id.ShouldBe("p-bytes");
        await actor.DidNotReceive().GetDetailAsync();
    }

    [Fact]
    public async Task ReadDetailAsync_MalformedJsonFallsThroughToSerializedBytesAsync()
    {
        IPartyDetailProjectionActor actor = Substitute.For<IPartyDetailProjectionActor>();
        actor.GetDetailJsonAsync().Returns("{");
        actor.GetSerializedDetailAsync().Returns(JsonSerializer.SerializeToUtf8Bytes(CreateDetail("p-bytes"), JsonOptions));

        PartyDetail? detail = await actor.ReadDetailAsync();

        detail.ShouldNotBeNull();
        detail.Id.ShouldBe("p-bytes");
        await actor.DidNotReceive().GetDetailAsync();
    }

    [Fact]
    public async Task ReadDetailAsync_CorruptSerializedBytesFallsThroughToTypedActorAsync()
    {
        IPartyDetailProjectionActor actor = Substitute.For<IPartyDetailProjectionActor>();
        actor.GetDetailJsonAsync().Returns((string?)null);
        actor.GetSerializedDetailAsync().Returns([0x7B]);
        actor.GetDetailAsync().Returns(Task.FromResult<PartyDetail?>(CreateDetail("p-typed")));

        PartyDetail? detail = await actor.ReadDetailAsync();

        detail.ShouldNotBeNull();
        detail.Id.ShouldBe("p-typed");
    }

    [Fact]
    public async Task ReadDetailAsync_RemotingNotImplementedFallsThroughToTypedActorAsync()
    {
        IPartyDetailProjectionActor actor = Substitute.For<IPartyDetailProjectionActor>();
        actor.GetDetailJsonAsync().Throws<NotImplementedException>();
        actor.GetSerializedDetailAsync().Throws<NotImplementedException>();
        actor.GetDetailAsync().Returns(Task.FromResult<PartyDetail?>(CreateDetail("p-typed")));

        PartyDetail? detail = await actor.ReadDetailAsync();

        detail.ShouldNotBeNull();
        detail.Id.ShouldBe("p-typed");
    }

    [Fact]
    public async Task ReadDetailAsync_AllStrategiesEmpty_ReturnsNullAsync()
    {
        IPartyDetailProjectionActor actor = Substitute.For<IPartyDetailProjectionActor>();
        actor.GetDetailJsonAsync().Returns((string?)null);
        actor.GetSerializedDetailAsync().Returns((byte[]?)null);
        actor.GetDetailAsync().Returns(Task.FromResult<PartyDetail?>(null));

        PartyDetail? detail = await actor.ReadDetailAsync();

        detail.ShouldBeNull();
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static PartyDetail CreateDetail(string id)
        => new()
        {
            Id = id,
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Ada Lovelace",
            SortName = "Lovelace, Ada",
            CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            LastModifiedAt = DateTimeOffset.Parse("2026-05-02T00:00:00Z"),
        };
}
