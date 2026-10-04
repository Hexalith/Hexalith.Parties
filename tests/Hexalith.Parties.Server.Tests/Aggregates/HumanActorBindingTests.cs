using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.State;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Domain;
using Shouldly;

namespace Hexalith.Parties.Server.Tests.Aggregates;

public sealed class HumanActorBindingTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
    private const string FirstActor = "01HX0000000000000000000001";
    private const string SecondActor = "01HX0000000000000000000002";
    private static IdentityCommandAuthorization Authorization(string actor, long head, DateTimeOffset time)
    {
        var policy = new IdentityHistoryPolicy("synthetic", TimeSpan.FromDays(10), "binding-effective-at");
        return new(new(new("tenant-a", "party", "party-1", "Bind", "message", "logical", "digest"), "writer", FirstActor,
            actor, 1, true, time, time.AddMinutes(1), 1), head, policy,
            new("synthetic", "party-actor-history-v1", time.AddDays(10), 1, "custody", true, true, true));
    }
    private static PartyState Human()
    {
        var state = new PartyState();
        state.Apply(new PartyCreated { Type = PartyType.Person, CreatedAt = Start, PersonDetails = new PersonDetails { FirstName = "Private", LastName = "Name" } });
        return state;
    }
    private static EstablishHumanActorBinding Establish() => new("tenant-a", "party-1", FirstActor, 1, 1, 0, Start, "establish", "synthetic")
        { Authorization = Authorization(FirstActor, 1, Start), OperatorProof = "first-transient-proof" };

    [Fact]
    public void SharedBoundary_PreservesActorVersionAndClosesPredecessor()
    {
        PartyState state = Human();
        state.Apply(PartyAggregate.Handle(Establish(), state).Events.Single().ShouldBeOfType<HumanActorBindingEstablished>());
        DateTimeOffset boundary = Start.AddHours(1);
        var command = new RebindHumanActorBinding("tenant-a", "party-1", SecondActor, 1, 2, 1, boundary, "rebind", "synthetic")
            { Authorization = Authorization(SecondActor, 2, boundary) };
        state.Apply(PartyAggregate.Handle(command, state).Events.Single().ShouldBeOfType<HumanActorBindingRebound>());
        state.HumanActorBindings[0].Evidence.ValidUntil.ShouldBe(boundary);
        state.HumanActorBindings[1].Evidence.ValidFrom.ShouldBe(boundary);
        state.HumanActorBindings[0].Evidence.ActorId.ShouldBe(FirstActor);
        state.HumanActorBindings[1].Evidence.ActorId.ShouldBe(SecondActor);
        state.HumanBindingVersion.ShouldBe(2);
    }

    [Fact]
    public void RefreshedOperatorProof_RetriesImmutableOriginalAfterStateBecomesInactive()
    {
        PartyState state = Human();
        EstablishHumanActorBinding command = Establish();
        var result = PartyAggregate.Handle(command, state);
        state.Apply(result.Events.Single().ShouldBeOfType<HumanActorBindingEstablished>());
        state.Apply(new PartyDeactivated());
        var retry = PartyAggregate.Handle(command with { OperatorProof = "refreshed-transient-proof" }, state);
        retry.Events.ShouldBeEmpty();
        retry.ResultPayload.ShouldBe(result.ResultPayload);
    }

    [Fact]
    public void MissingPolicyWrongActorOrOrganization_Denies()
    {
        PartyState state = Human();
        EstablishHumanActorBinding command = Establish();
        PartyAggregate.Handle(command with { Authorization = command.Authorization! with { Policy = null } }, state).IsRejection.ShouldBeTrue();
        PartyAggregate.Handle(command with { ActorId = SecondActor }, state).IsRejection.ShouldBeTrue();
        var organization = new PartyState();
        organization.Apply(new PartyCreated { Type = PartyType.Organization, OrganizationDetails = new OrganizationDetails { LegalName = "Natural organization", IsNaturalPerson = true } });
        PartyAggregate.Handle(command, organization).IsRejection.ShouldBeTrue();
        JsonSerializer.Serialize(command, PartiesJsonOptions.Default).ShouldNotContain("authorization");
    }

    [Fact]
    public void ValidRevocation_ClosesOriginalAndRefreshedProofKeepsOriginalRetryResult()
    {
        PartyState state = Human();
        state.Apply(PartyAggregate.Handle(Establish(), state).Events.Single().ShouldBeOfType<HumanActorBindingEstablished>());
        DateTimeOffset boundary = Start.AddHours(1);
        var command = new RevokeHumanActorBinding("tenant-a", "party-1", FirstActor, 1, 2, 1, boundary, "revoke", "synthetic")
            { Authorization = Authorization(FirstActor, 2, boundary), OperatorProof = "first-revoke-proof" };
        var result = PartyAggregate.Handle(command, state);
        result.IsRejection.ShouldBeFalse();
        state.Apply(result.Events.Single().ShouldBeOfType<HumanActorBindingRevoked>());
        state.HumanActorBindings.Single().Evidence.ValidUntil.ShouldBe(boundary);
        state.HumanBindingVersion.ShouldBe(2);
        state.Apply(new PartyDeactivated());
        var retry = PartyAggregate.Handle(command with { OperatorProof = "refreshed-revoke-proof" }, state);
        retry.Events.ShouldBeEmpty();
        retry.ResultPayload.ShouldBe(result.ResultPayload);
    }
}
