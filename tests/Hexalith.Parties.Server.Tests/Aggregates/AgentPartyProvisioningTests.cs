using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Events.Rejections;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.State;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Domain;
using Shouldly;

namespace Hexalith.Parties.Server.Tests.Aggregates;

public sealed class AgentPartyProvisioningTests
{
    private static ProvisionAgentParty Command(string tenant = "tenant-a")
    {
        var intent = new AgentPartyIdentity(tenant, "01HX0000000000000000000001", AgentPartyIdMapping.Create(tenant, "01HX0000000000000000000001"), 1, "logical", new string('a', 64), DateTimeOffset.UnixEpoch);
        var scope = new IdentityAdmissionScope(tenant, "party", intent.PartyId, "ProvisionAgentParty", "message", intent.LogicalId, "digest");
        return new(intent) { Authorization = new(new(scope, "provisioner", null, null, 0, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(1), 1), 0, null, null) };
    }

    [Fact]
    public void DeterministicMapping_IsTenantSpecificAndReserved()
    {
        ProvisionAgentParty first = Command();
        AgentPartyIdMapping.Create("tenant-a", first.Identity.AgentId).ShouldBe(first.Identity.PartyId);
        Command("tenant-b").Identity.PartyId.ShouldNotBe(first.Identity.PartyId);
        AgentPartyIdMapping.IsReserved(first.Identity.PartyId).ShouldBeTrue();
        first.Identity.PartyId.Length.ShouldBe(26);
        first.Identity.PartyId.ShouldBe("0000000000TXYDY097JGTSDVEX");
        Command("tenant-b").Identity.PartyId.ShouldBe("0000000000T5XCJN74D4TBDG0Y");
    }

    [Fact]
    public void ExactLostAckRetry_ReturnsOriginalWithoutAnotherCreationAndRejectsChangedIntent()
    {
        ProvisionAgentParty command = Command();
        DomainResult initial = PartyAggregate.Handle(command, null);
        initial.IsRejection.ShouldBeFalse();
        var state = new PartyState();
        Apply(state, initial.Events);
        AgentPartyProvisioningResult original = state.AgentProvisioning!;
        state.Apply(new PartyDeactivated());
        DomainResult retry = PartyAggregate.Handle(command, state);
        retry.Events.ShouldBeEmpty();
        retry.ResultPayload.ShouldBe(initial.ResultPayload);
        state.AgentProvisioning.ShouldBe(original);
        PartyAggregate.Handle(command with { Identity = command.Identity with { LogicalId = "changed" } }, state).IsRejection.ShouldBeTrue();
    }

    [Fact]
    public void RejectionOnlyStream_AllowsCreationWhileUnmarkedOrganizationConflicts()
    {
        var rejected = new PartyState();
        rejected.Apply(new AgentPartyProvisioningRejected("authority-unavailable"));
        rejected.HasBeenCreated.ShouldBeFalse();
        PartyAggregate.Handle(Command(), rejected).IsRejection.ShouldBeFalse();
        rejected.Apply(new PartyCreated { Type = PartyType.Organization, CreatedAt = DateTimeOffset.UnixEpoch,
            OrganizationDetails = new OrganizationDetails { LegalName = "Existing" } });
        PartyAggregate.Handle(Command(), rejected).IsRejection.ShouldBeTrue();
    }

    private static void Apply(PartyState state, IReadOnlyList<IEventPayload> events)
    {
        foreach (IEventPayload item in events)
        {
            typeof(PartyState).GetMethod("Apply", [item.GetType()])!.Invoke(state, [item]);
        }
    }
}
