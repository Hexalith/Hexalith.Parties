using System.Text.Json;

using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Queries;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Queries;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.Tests.Queries;

public sealed class ChatBotPartyAuthorityQueryHandlerTests
{
    [Fact]
    public async Task CurrentPersonAllowsOnlyBoundRequesterAndDeactivationDenies()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var identity = new AggregateIdentity("tenant-alpha", "party", "actor-alpha");
        var created = new PartyCreated
        {
            Type = PartyType.Person,
            PersonDetails = new PersonDetails { FirstName = "Ada", LastName = "Lovelace" },
            CreatedAt = now.AddMinutes(-1),
        };
        StreamReadEvent[] active = [Event(1, created, now)];
        IAuthoritativeEventStreamReader reader = Substitute.For<IAuthoritativeEventStreamReader>();
        reader.ReadAsync(identity, Arg.Any<CancellationToken>()).Returns(_ => new AuthoritativeStreamReadResult(
            new AuthoritativeEventStream(identity, 1, now, active, "party-observation-1"), null));
        var handler = new ChatBotPartyAuthorityQueryHandler(reader, TimeProvider.System);
        QueryEnvelope exact = Envelope("actor-alpha");

        QueryResult allowed = await handler.ExecuteAsync(exact, CancellationToken.None);
        allowed.Success.ShouldBeTrue();
        JsonSerializer.Deserialize<ChatBotPartyAuthorityResult>(allowed.PayloadBytes!,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!.Allowed.ShouldBeTrue();
        (await handler.ExecuteAsync(Envelope("actor-beta") with { Payload = exact.Payload }, CancellationToken.None))
            .Success.ShouldBeFalse();

        StreamReadEvent[] inactive = [.. active, Event(2, new PartyDeactivated(), now)];
        reader.ReadAsync(identity, Arg.Any<CancellationToken>()).Returns(_ => new AuthoritativeStreamReadResult(
            new AuthoritativeEventStream(identity, 2, now, inactive, "party-observation-2"), null));
        QueryResult denied = await handler.ExecuteAsync(exact, CancellationToken.None);
        denied.Success.ShouldBeTrue();
        JsonSerializer.Deserialize<ChatBotPartyAuthorityResult>(denied.PayloadBytes!,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!.Allowed.ShouldBeFalse();
    }

    private static QueryEnvelope Envelope(string requester)
    {
        var payload = new ResolveChatBotPartyAuthority("tenant-alpha", "actor-alpha", "actor-alpha",
            "RecordGovernedNote", "human", "Ui");
        return new QueryEnvelope("tenant-alpha", "party", "actor-alpha", ResolveChatBotPartyAuthority.QueryType,
            JsonSerializer.SerializeToUtf8Bytes(payload, PartiesJsonOptions.Default), "corr-1", requester)
        {
            AuthenticatedWorkloadId = "chatbot", OriginalActorId = requester, IsDelegated = true,
        };
    }

    private static StreamReadEvent Event(long sequence, object payload, DateTimeOffset now)
        => new(sequence, payload.GetType().FullName!, JsonSerializer.SerializeToUtf8Bytes(payload,
            PartiesJsonOptions.Default), "json", 1, $"message-{sequence}", null, null, now, "issuer");
}
