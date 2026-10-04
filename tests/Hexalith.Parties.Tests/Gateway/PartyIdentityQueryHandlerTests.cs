using System.Text.Json;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Parties.Authorization;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Queries;
using Hexalith.Parties.Contracts.State;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Queries;
using NSubstitute;
using Shouldly;

namespace Hexalith.Parties.Tests.Gateway;

public sealed class PartyIdentityQueryHandlerTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.UtcNow.AddDays(-1);
    private const string Actor = "01HX0000000000000000000001";
    private static HumanActorBindingEvidence Binding() => new("tenant-a", "party-1", Actor, 1, 1, Start, Start.AddDays(10), "writer", Actor,
        new("synthetic", "party-actor-history-v1", Start.AddDays(10), 1, "custody", true, true, true));
    private static IEventPayload[] Events() => [new PartyCreated { Type = PartyType.Person, CreatedAt = Start, PersonDetails = new PersonDetails { FirstName = "Private", LastName = "Name" } },
        new HumanActorBindingEstablished(new(Binding(), "logical", "digest"), Start, 0)];
    private static QueryEnvelope Envelope<T>(T query) => new("tenant-a", "party", "party-1", typeof(T).FullName!, JsonSerializer.SerializeToUtf8Bytes(query, PartiesJsonOptions.Default), "correlation", "reader", "party-1");
    private static (PartyIdentityQueryService Service, IPartyIdentityAuthority Authority, IAuthoritativeEventStreamReader Reader, IIdentityHistoryCustody Custody) Service(params IEventPayload[] events)
    {
        IPartyIdentityAuthority authority = Substitute.For<IPartyIdentityAuthority>();
        authority.Admit(Arg.Any<QueryEnvelope>()).Returns(call => new PartyIdentityAdmissionResult(new(new("tenant-a", "party", "party-1", "Read", "correlation", "correlation", "digest"),
            "reader", null, Actor, 1, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(1), 1), null));
        IAuthoritativeEventStreamReader reader = Substitute.For<IAuthoritativeEventStreamReader>();
        StreamReadEvent[] source = [.. events.Select((item, index) => new StreamReadEvent(index + 1, item.GetType().FullName!, JsonSerializer.SerializeToUtf8Bytes(item, item.GetType(), PartiesJsonOptions.Default), "json", 1, "message", null, null, Start, "writer"))];
        reader.ReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<CancellationToken>()).Returns(new AuthoritativeStreamReadResult(new(new("tenant-a", "party", "party-1"), source.Length, DateTimeOffset.UtcNow, source, "observation"), null));
        IIdentityHistoryCustody custody = Substitute.For<IIdentityHistoryCustody>();
        custody.CanReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<IdentityHistoryCustodyEvidence>(), Arg.Any<CancellationToken>()).Returns(true);
        return (new(authority, TimeProvider.System, reader, custody), authority, reader, custody);
    }

    [Fact]
    public async Task CurrentHuman_RequiresCompleteSourceAndCurrentExactActor()
    {
        var fixture = Service(Events());
        PartyIdentityResult result = await fixture.Service.ResolveAsync(Envelope(new ResolvePartyIdentity("tenant-a", "party-1", Actor)), TestContext.Current.CancellationToken);
        result.Outcome.ShouldBe(PartyIdentityOutcome.Resolved);
        result.Evidence!.Classification.ShouldBe(PartyIdentityClassification.Human);
        result.Evidence.HumanBinding!.ActorId.ShouldBe(Actor);
        JsonSerializer.Serialize(result, PartiesJsonOptions.Default).ShouldNotContain("Private");
        (await fixture.Service.ResolveAsync(Envelope(new ResolvePartyIdentity("tenant-a", "party-1", "01HX0000000000000000000002")), TestContext.Current.CancellationToken)).Outcome.ShouldBe(PartyIdentityOutcome.Ineligible);
    }

    [Fact]
    public async Task HistoricalRead_DoesNotRequireCurrentActorActivityAndHonorsHalfOpenBoundary()
    {
        var fixture = Service(Events());
        fixture.Authority.Admit(Arg.Any<QueryEnvelope>()).Returns(new PartyIdentityAdmissionResult(new(new("tenant-a", "party", "party-1", "Read", "c", "c", "d"), "reader", null, Actor, 2, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(1), 1), null));
        var query = new ResolveHumanActorBindingAt("tenant-a", "party-1", Start, Actor, 1);
        (await fixture.Service.ResolveAtAsync(Envelope(query), TestContext.Current.CancellationToken)).Outcome.ShouldBe(HumanActorBindingOutcome.Resolved);
        (await fixture.Service.ResolveAtAsync(Envelope(query with { ActionAt = Start.AddTicks(-1) }), TestContext.Current.CancellationToken)).Outcome.ShouldBe(HumanActorBindingOutcome.Gap);
        (await fixture.Service.ResolveAtAsync(Envelope(query with { ExpectedBindingVersion = 2 }), TestContext.Current.CancellationToken)).Outcome.ShouldBe(HumanActorBindingOutcome.Mismatch);
    }

    [Fact]
    public async Task HistoricalReplay_BeforeBoundaryReturnsOriginalAndAtBoundaryReturnsSuccessor()
    {
        DateTimeOffset boundary = Start.AddHours(1);
        string successor = "01HX0000000000000000000002";
        HumanActorBindingEvidence second = Binding() with { ActorId = successor, BindingVersion = 2, ValidFrom = boundary };
        var fixture = Service(Events().Append(new HumanActorBindingRebound(new(second, "rebind", "digest-two"), boundary, 1)).ToArray());
        fixture.Authority.Admit(Arg.Any<QueryEnvelope>()).Returns(call =>
        {
            string target = JsonSerializer.Deserialize<ResolveHumanActorBindingAt>(call.Arg<QueryEnvelope>().Payload, PartiesJsonOptions.Default)!.ExpectedActorId;
            return new PartyIdentityAdmissionResult(new(new("tenant-a", "party", "party-1", "Read", "c", "c", "d"), "reader", null, target, 3, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(1), 1), null);
        });
        HumanActorBindingResult before = await fixture.Service.ResolveAtAsync(Envelope(new ResolveHumanActorBindingAt("tenant-a", "party-1", boundary.AddTicks(-1), Actor, 1)), TestContext.Current.CancellationToken);
        HumanActorBindingResult at = await fixture.Service.ResolveAtAsync(Envelope(new ResolveHumanActorBindingAt("tenant-a", "party-1", boundary, successor, 2)), TestContext.Current.CancellationToken);
        before.Outcome.ShouldBe(HumanActorBindingOutcome.Resolved);
        at.Outcome.ShouldBe(HumanActorBindingOutcome.Resolved);
        before.Evidence!.ActorId.ShouldBe(Actor);
        at.Evidence!.ActorId.ShouldBe(successor);
        before.Evidence.ValidUntil.ShouldBe(boundary);
    }

    [Fact]
    public async Task ForeignAuthoritativeSourceAndFutureAction_DenyWithNoEvidence()
    {
        var fixture = Service(new PartyCreated { Type = PartyType.Organization, OrganizationDetails = new OrganizationDetails { LegalName = "Foreign" } });
        fixture.Reader.ReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<CancellationToken>())
            .Returns(new AuthoritativeStreamReadResult(new(new("tenant-b", "party", "foreign-party"), 1, DateTimeOffset.UtcNow,
                [new(1, typeof(PartyCreated).FullName!, JsonSerializer.SerializeToUtf8Bytes(new PartyCreated { Type = PartyType.Organization }, PartiesJsonOptions.Default), "json", 1, "m", null, null, Start, "writer")], "observation"), null));
        PartyIdentityResult current = await fixture.Service.ResolveAsync(Envelope(new ResolvePartyIdentity("tenant-a", "party-1")), TestContext.Current.CancellationToken);
        current.Outcome.ShouldBe(PartyIdentityOutcome.Unavailable);
        current.Evidence.ShouldBeNull();
        HumanActorBindingResult history = await fixture.Service.ResolveAtAsync(Envelope(new ResolveHumanActorBindingAt("tenant-a", "party-1", Start, Actor, 1)), TestContext.Current.CancellationToken);
        history.Outcome.ShouldBe(HumanActorBindingOutcome.Unavailable);
        history.Evidence.ShouldBeNull();
        var futureFixture = Service(Events());
        HumanActorBindingResult future = await futureFixture.Service.ResolveAtAsync(Envelope(new ResolveHumanActorBindingAt("tenant-a", "party-1", DateTimeOffset.UtcNow.AddDays(1), Actor, 1)), TestContext.Current.CancellationToken);
        future.Outcome.ShouldBe(HumanActorBindingOutcome.Unavailable);
        future.Evidence.ShouldBeNull();
    }

    [Fact]
    public async Task ValidRevocationReplay_PreservesBeforeBoundaryAndReturnsGapAtBoundary()
    {
        DateTimeOffset boundary = Start.AddHours(1);
        var fixture = Service([.. Events(), new HumanActorBindingRevoked("revoke", "digest-two", 1, boundary, Binding().Custody)]);
        HumanActorBindingResult before = await fixture.Service.ResolveAtAsync(Envelope(new ResolveHumanActorBindingAt("tenant-a", "party-1", boundary.AddTicks(-1), Actor, 1)), TestContext.Current.CancellationToken);
        before.Outcome.ShouldBe(HumanActorBindingOutcome.Resolved);
        before.Evidence!.ActorId.ShouldBe(Actor);
        before.Evidence.BindingVersion.ShouldBe(1);
        before.Evidence.ValidUntil.ShouldBe(boundary);
        HumanActorBindingResult at = await fixture.Service.ResolveAtAsync(Envelope(new ResolveHumanActorBindingAt("tenant-a", "party-1", boundary, Actor, 1)), TestContext.Current.CancellationToken);
        at.Outcome.ShouldBe(HumanActorBindingOutcome.Gap);
        at.Evidence.ShouldBeNull();
    }

    [Fact]
    public async Task WrongAuthorizedActor_DeniesBeforeSourceRead()
    {
        var fixture = Service(Events());
        var query = new ResolveHumanActorBindingAt("tenant-a", "party-1", Start, "01HX0000000000000000000002", 1);
        (await fixture.Service.ResolveAtAsync(Envelope(query), TestContext.Current.CancellationToken)).Outcome.ShouldBe(HumanActorBindingOutcome.Unavailable);
        await fixture.Reader.DidNotReceiveWithAnyArgs().ReadAsync(default!, default);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RevocationWithoutLivePredecessor_DeniesWithNoEvidence(bool missingPredecessor)
    {
        IEventPayload[] events = missingPredecessor ? [Events()[0]] : Events();
        var fixture = Service([.. events, new HumanActorBindingRevoked("revoke", "digest-two", missingPredecessor ? 0 : 1, Start, Binding().Custody)]);
        PartyIdentityResult current = await fixture.Service.ResolveAsync(Envelope(new ResolvePartyIdentity("tenant-a", "party-1", Actor)), TestContext.Current.CancellationToken);
        current.Outcome.ShouldBe(PartyIdentityOutcome.Unavailable);
        current.Evidence.ShouldBeNull();
        HumanActorBindingResult history = await fixture.Service.ResolveAtAsync(Envelope(new ResolveHumanActorBindingAt("tenant-a", "party-1", Start, Actor, 1)), TestContext.Current.CancellationToken);
        history.Outcome.ShouldBe(HumanActorBindingOutcome.Unavailable);
        history.Evidence.ShouldBeNull();
        await fixture.Custody.DidNotReceiveWithAnyArgs().CanReadAsync(default!, default!, default);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NonFiniteOrBeyondCustodyHistory_DeniesWithNoEvidence(bool missingEnd)
    {
        HumanActorBindingEvidence malformed = Binding() with
        {
            ValidUntil = missingEnd ? null : Binding().Custody.ExpiresAt.AddTicks(1),
        };
        var fixture = Service(Events()[0], new HumanActorBindingEstablished(new(malformed, "logical", "digest"), Start, 0));
        PartyIdentityResult current = await fixture.Service.ResolveAsync(Envelope(new ResolvePartyIdentity("tenant-a", "party-1", Actor)), TestContext.Current.CancellationToken);
        current.Outcome.ShouldBe(PartyIdentityOutcome.Unavailable);
        current.Evidence.ShouldBeNull();
        HumanActorBindingResult history = await fixture.Service.ResolveAtAsync(Envelope(new ResolveHumanActorBindingAt("tenant-a", "party-1", Start, Actor, 1)), TestContext.Current.CancellationToken);
        history.Outcome.ShouldBe(HumanActorBindingOutcome.Unavailable);
        history.Evidence.ShouldBeNull();
        await fixture.Custody.DidNotReceiveWithAnyArgs().CanReadAsync(default!, default!, default);
    }

    [Fact]
    public async Task CustodyFailureOrNetworkFailure_IsUnavailableAndCancellationPropagates()
    {
        var fixture = Service(Events());
        fixture.Custody.CanReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<IdentityHistoryCustodyEvidence>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(new HttpRequestException("unavailable")));
        var query = new ResolveHumanActorBindingAt("tenant-a", "party-1", Start, Actor, 1);
        (await fixture.Service.ResolveAtAsync(Envelope(query), TestContext.Current.CancellationToken)).Outcome.ShouldBe(HumanActorBindingOutcome.Unavailable);
        using var canceled = new CancellationTokenSource();
        await canceled.CancelAsync();
        fixture.Reader.ReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<CancellationToken>()).Returns(call => Task.FromCanceled<AuthoritativeStreamReadResult>(call.Arg<CancellationToken>()));
        await Should.ThrowAsync<OperationCanceledException>(() => fixture.Service.ResolveAtAsync(Envelope(query), canceled.Token));
    }

    [Fact]
    public async Task UncreatedInactiveRestrictedUnknownAndErased_AreNeverResolved()
    {
        foreach (IEventPayload[] events in new[] { Array.Empty<IEventPayload>(), [new PartyCreated { Type = PartyType.Unknown }],
            Events().Append(new PartyDeactivated()).ToArray(), Events().Append(new ProcessingRestricted { TenantId = "tenant-a", PartyId = "party-1", RestrictedAt = Start }).ToArray(),
            Events().Append(new PartyErased { TenantId = "tenant-a", PartyId = "party-1", ErasedAt = Start }).ToArray() })
        {
            (await Service(events).Service.ResolveAsync(Envelope(new ResolvePartyIdentity("tenant-a", "party-1", Actor)), TestContext.Current.CancellationToken)).Outcome.ShouldNotBe(PartyIdentityOutcome.Resolved);
        }
    }
}
