using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Client.Security;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Serialization;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Events.Rejections;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Queries;
using Hexalith.Parties.Contracts.State;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Extensions;
using Hexalith.Parties.Queries;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Identity;

/// <summary>
/// Authenticated P-01–P-10 proof against a disposable durable directory.
/// Custody is a test double registered by this host, never by production DI.
/// </summary>
public sealed class PartyIdentityPersistedVerifierTests
{
    private const string AgentId = "01HX0000000000000000000001";
    private const string ActorId = "01HX0000000000000000000002";
    private const string SuccessorId = "01HX0000000000000000000003";
    private const string OperatorId = "01HX0000000000000000000004";
    private const string AliasCaller = "01HX0000000000000000000005";
    private const string AliasEditor = "01HX0000000000000000000006";
    private const string AliasRegenerator = "01HX0000000000000000000007";
    private const string PolicyId = "party-actor-retention-v1";
    private const string Purpose = "party-actor-history-v1";
    private static readonly DateTimeOffset Started = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Missing host custody and history registrations fail closed before protected work.</summary>
    [Fact]
    public async Task MissingHostRegistrations_FailClosed()
    {
        using Lane lane = Lane.Open(registerHostSeams: false);
        using IServiceScope scope = lane.Services.CreateScope();
        scope.ServiceProvider.GetService<IIdentityHistoryCustody>().ShouldBeNull();
        scope.ServiceProvider.GetService<IRetainedIdentityHistoryReader>().ShouldBeNull();
        string partyId = "human-unbound";
        var body = new EstablishHumanActorBinding("tenant-a", partyId, ActorId, 1, 0, 0, lane.Clock.UtcNow, "bind-unhosted", PolicyId);
        CommandEnvelope command = lane.Signed(body, "bind-unhosted", ActorId, "trusted-writer");

        DomainResult denied = await lane.Dispatch(command);

        Reason(denied).ShouldBe("CustodyUnavailable");
        lane.Directory.Read("tenant-a", partyId).ShouldBeEmpty();
        await lane.Protection.DidNotReceiveWithAnyArgs().UnprotectEventPayloadAsync(default!, default!, default!, default!, default);
        HumanActorBindingResult history = await lane.History("tenant-a", partyId, lane.Clock.UtcNow, ActorId, 1);
        history.Outcome.ShouldBe(HumanActorBindingOutcome.Unavailable);
        history.Evidence.ShouldBeNull();
    }

    /// <summary>Lost acknowledgement and restart keep one party and one binding.</summary>
    [Fact]
    public async Task P01_LostAcknowledgementThenRestartKeepsOnePartyAndBinding()
    {
        using Lane lane = Lane.Open(registerHostSeams: true);
        (string partyId, CommandEnvelope provision, DomainResult created) = await lane.Provision("tenant-a", "provision-original");
        created.Events.OfType<PartyCreated>().Single().Type.ShouldBe(PartyType.Organization);
        await lane.Commit("tenant-a", partyId, created);
        string originalPayload = created.ResultPayload!;
        int createdCount = lane.Directory.Read("tenant-a", partyId).Count;
        string createdHash = lane.Directory.Fingerprint("tenant-a", partyId);
        lane.Restart();

        DomainResult retried = await lane.Retry(provision, "provision-original", AgentId, "trusted-provisioner");

        retried.IsRejection.ShouldBeFalse();
        retried.Events.ShouldBeEmpty();
        retried.ResultPayload.ShouldBe(originalPayload);
        lane.Directory.Read("tenant-a", partyId).Count.ShouldBe(createdCount);
        lane.Directory.Fingerprint("tenant-a", partyId).ShouldBe(createdHash);
        PartyIdentityResult identity = await lane.Current("tenant-a", partyId, expectedActor: null);
        identity.Outcome.ShouldBe(PartyIdentityOutcome.Resolved);
        identity.Evidence!.Classification.ShouldBe(PartyIdentityClassification.Organization);
        identity.Evidence.PartyId.ShouldBe(partyId);
        identity.Evidence.TenantId.ShouldBe("tenant-a");
        identity.Evidence.HumanBinding.ShouldBeNull();
        AssertNoProfile(identity);

        DomainResult secondCreate = await lane.Dispatch(lane.Signed(
            new ProvisionAgentParty(new AgentPartyIdentity("tenant-a", AgentId, partyId, 1, "provision-today", new string('b', 64), Started)),
            "provision-today", AgentId, "trusted-provisioner"));
        Reason(secondCreate).ShouldBe("intent-conflict");
        lane.Directory.Fingerprint("tenant-a", partyId).ShouldBe(createdHash);

        string humanId = "human-recovered";
        await lane.CreatePerson(humanId, "Ada", "Lovelace");
        (CommandEnvelope binding, DomainResult bound) = await lane.Establish(humanId, ActorId, "bind-original");
        await lane.Commit("tenant-a", humanId, bound, ActorId);
        string bindingPayload = bound.ResultPayload!;
        int bindingCount = lane.Directory.Read("tenant-a", humanId).Count;
        string bindingHash = lane.Directory.Fingerprint("tenant-a", humanId);
        lane.Restart();

        DomainResult boundAgain = await lane.Retry(binding, "bind-original", ActorId, "trusted-writer");

        boundAgain.IsRejection.ShouldBeFalse();
        boundAgain.Events.ShouldBeEmpty();
        boundAgain.ResultPayload.ShouldBe(bindingPayload);
        lane.Directory.Read("tenant-a", humanId).Count.ShouldBe(bindingCount);
        lane.Directory.Fingerprint("tenant-a", humanId).ShouldBe(bindingHash);
        DomainResult today = await lane.Dispatch(lane.Signed(
            new EstablishHumanActorBinding("tenant-a", humanId, ActorId, 1, bindingCount, 0, lane.Clock.UtcNow, "bind-today", PolicyId),
            "bind-today", ActorId, "trusted-writer"));
        Reason(today).ShouldBe("binding-unavailable");
        lane.Directory.Fingerprint("tenant-a", humanId).ShouldBe(bindingHash);
        (await lane.Current("tenant-a", humanId, ActorId)).Evidence!.HumanBinding!.ActorId.ShouldBe(ActorId);
    }

    /// <summary>Changed intent, an occupied unmarked id, and generic creates leave the original bytes in place.</summary>
    [Fact]
    public async Task P02_ChangedIntentOccupiedIdentityAndGenericBypassConflict()
    {
        using Lane lane = Lane.Open(registerHostSeams: true);
        (string partyId, CommandEnvelope provision, DomainResult created) = await lane.Provision("tenant-a", "provision-original");
        await lane.Commit("tenant-a", partyId, created);
        string payload = created.ResultPayload!;
        string hash = lane.Directory.Fingerprint("tenant-a", partyId);
        int count = lane.Directory.Read("tenant-a", partyId).Count;

        DomainResult changed = await lane.Dispatch(lane.Signed(
            new ProvisionAgentParty(new AgentPartyIdentity("tenant-a", AgentId, partyId, 1, "provision-changed", new string('c', 64), Started)),
            "provision-changed", AgentId, "trusted-provisioner"));
        Reason(changed).ShouldBe("intent-conflict");
        lane.AssertUnchanged("tenant-a", partyId, count, hash, changed);
        (await lane.Retry(provision, "provision-original", AgentId, "trusted-provisioner")).ResultPayload.ShouldBe(payload);

        string occupiedId = AgentPartyIdMapping.Create("tenant-b", AgentId);
        byte[] unmarked = EventBytes(new PartyCreated
        {
            Type = PartyType.Organization,
            CreatedAt = Started,
            OrganizationDetails = new OrganizationDetails { LegalName = "Occupied", IsNaturalPerson = false },
        });
        lane.Directory.AppendPlain("tenant-b", occupiedId, typeof(PartyCreated).FullName!, unmarked, Started);
        string occupiedHash = lane.Directory.Fingerprint("tenant-b", occupiedId);
        DomainResult occupied = await lane.Dispatch(lane.Signed(
            new ProvisionAgentParty(new AgentPartyIdentity("tenant-b", AgentId, occupiedId, 1, "provision-occupied", new string('d', 64), Started)),
            "provision-occupied", AgentId, "trusted-provisioner"));
        Reason(occupied).ShouldBe("occupied-unmarked-identity");
        lane.AssertUnchanged("tenant-b", occupiedId, 1, occupiedHash, occupied);

        DomainResult generic = await lane.Dispatch(lane.Plain(new CreateParty
        {
            PartyId = partyId,
            Type = PartyType.Person,
            PersonDetails = new PersonDetails { FirstName = "Ada", LastName = "Lovelace" },
        }, "tenant-a", partyId));
        lane.AssertUnchanged("tenant-a", partyId, count, hash, generic);
        DomainResult composite = await lane.Dispatch(lane.Plain(new CreatePartyComposite
        {
            PartyId = occupiedId,
            Type = PartyType.Organization,
            OrganizationDetails = new OrganizationDetails { LegalName = "Bypass", IsNaturalPerson = false },
        }, "tenant-b", occupiedId));
        lane.AssertUnchanged("tenant-b", occupiedId, 1, occupiedHash, composite);
    }

    /// <summary>A rejection-only replay is not a creation marker. One later provision is kept.</summary>
    [Fact]
    public async Task P03_RejectionOnlyReplayThenOneValidProvision()
    {
        using Lane lane = Lane.Open(registerHostSeams: true);
        string partyId = AgentPartyIdMapping.Create("tenant-a", AgentId);
        CommandEnvelope unsigned = lane.Plain(
            new ProvisionAgentParty(new AgentPartyIdentity("tenant-a", AgentId, partyId, 1, "provision-valid", new string('a', 64), Started)),
            "tenant-a", partyId);
        DomainResult rejected = await lane.Dispatch(unsigned);
        Reason(rejected).ShouldBe("AuthorityUnavailable");
        lane.CommitRejection("tenant-a", partyId, rejected);
        (await lane.Current("tenant-a", partyId, expectedActor: null)).Outcome.ShouldBe(PartyIdentityOutcome.Unavailable);

        CommandEnvelope provision = lane.Signed(
            new ProvisionAgentParty(new AgentPartyIdentity("tenant-a", AgentId, partyId, 1, "provision-valid", new string('a', 64), Started)),
            "provision-valid", AgentId, "trusted-provisioner");
        DomainResult created = await lane.Dispatch(provision);
        created.IsRejection.ShouldBeFalse();
        await lane.Commit("tenant-a", partyId, created);
        int count = lane.Directory.Read("tenant-a", partyId).Count;
        created.Events.OfType<AgentPartyProvisioned>().ShouldHaveSingleItem();
        (await lane.Current("tenant-a", partyId, expectedActor: null)).Evidence!.Classification.ShouldBe(PartyIdentityClassification.Organization);

        DomainResult replay = await lane.Retry(provision, "provision-valid", AgentId, "trusted-provisioner");
        replay.Events.ShouldBeEmpty();
        replay.ResultPayload.ShouldBe(created.ResultPayload);
        lane.Directory.Read("tenant-a", partyId).Count.ShouldBe(count);
    }

    /// <summary>Other tenants, forged admission, and foreign ids are denied before lookup or effect.</summary>
    [Fact]
    public async Task P04_CrossTenantForgedAndForeignDenialLeavesForeignState()
    {
        using Lane lane = Lane.Open(registerHostSeams: true);
        (string partyA, _, DomainResult createdA) = await lane.Provision("tenant-a", "provision-a");
        await lane.Commit("tenant-a", partyA, createdA);
        (string partyB, _, DomainResult createdB) = await lane.Provision("tenant-b", "provision-b");
        await lane.Commit("tenant-b", partyB, createdB);
        string hashA = lane.Directory.Fingerprint("tenant-a", partyA);
        string hashB = lane.Directory.Fingerprint("tenant-b", partyB);
        int authoritative = lane.Directory.AuthoritativeReads;
        int retained = lane.Directory.RetainedReads;

        DomainResult foreignPayload = await lane.Dispatch(lane.Signed(
            new ProvisionAgentParty(new AgentPartyIdentity("tenant-b", AgentId, partyB, 1, "provision-b", new string('a', 64), Started)),
            "provision-b", AgentId, "trusted-provisioner", "tenant-a", partyA));
        Reason(foreignPayload).ShouldBe("AuthorityUnavailable");
        string forged = Forge(lane.Sign(Lane.Scope(
            lane.Plain(new ProvisionAgentParty(new AgentPartyIdentity("tenant-b", AgentId, partyB, 1, "provision-forged", new string('e', 64), Started)), "tenant-b", partyB),
            "provision-forged"), AgentId, "trusted-provisioner"));
        CommandEnvelope forgedCommand = lane.Plain(
            new ProvisionAgentParty(new AgentPartyIdentity("tenant-b", AgentId, partyB, 1, "provision-forged", new string('e', 64), Started)),
            "tenant-b", partyB);
        forgedCommand.Extensions![IdentityAdmissionProof.ExtensionKey] = forged;
        DomainResult forgedResult = await lane.Dispatch(forgedCommand);
        Reason(forgedResult).ShouldBe("AuthorityUnavailable");

        Task<DomainResult>[] concurrent =
        [
            lane.Dispatch(lane.Signed(
                new ProvisionAgentParty(new AgentPartyIdentity("tenant-b", AgentId, partyB, 1, "provision-left", new string('f', 64), Started)),
                "provision-left", AgentId, "trusted-provisioner", "tenant-a", partyA)),
            lane.Dispatch(lane.Signed(
                new ProvisionAgentParty(new AgentPartyIdentity("tenant-a", AgentId, partyA, 1, "provision-right", new string('1', 64), Started)),
                "provision-right", AgentId, "trusted-provisioner", "tenant-b", partyB)),
        ];
        DomainResult[] denied = await Task.WhenAll(concurrent);
        denied.ShouldAllBe(item => Reason(item) == "AuthorityUnavailable");

        PartyIdentityResult mismatched = await lane.Current("tenant-b", partyB, expectedActor: null, proofTenant: "tenant-a", proofParty: partyA);
        mismatched.Outcome.ShouldBe(PartyIdentityOutcome.Unavailable);
        mismatched.Evidence.ShouldBeNull();
        HumanActorBindingResult forgedHistory = await lane.History("tenant-b", partyB, Started, ActorId, 1, forge: true);
        forgedHistory.Outcome.ShouldBe(HumanActorBindingOutcome.Unavailable);
        forgedHistory.Evidence.ShouldBeNull();

        lane.Directory.Fingerprint("tenant-a", partyA).ShouldBe(hashA);
        lane.Directory.Fingerprint("tenant-b", partyB).ShouldBe(hashB);
        lane.Directory.AuthoritativeReads.ShouldBe(authoritative);
        lane.Directory.RetainedReads.ShouldBe(retained);
    }

    /// <summary>The provisioned organization is verified by tenant and party id, and type is not a substitute.</summary>
    [Fact]
    public async Task P05_ExactOrganizationIdentityRejectsSubstitutionAndTypeChange()
    {
        using Lane lane = Lane.Open(registerHostSeams: true);
        (string partyId, _, DomainResult created) = await lane.Provision("tenant-a", "provision-exact");
        await lane.Commit("tenant-a", partyId, created);
        PartyIdentityResult resolved = await lane.Current("tenant-a", partyId, expectedActor: null);
        resolved.Outcome.ShouldBe(PartyIdentityOutcome.Resolved);
        resolved.Evidence!.PartyId.ShouldBe(partyId);
        resolved.Evidence.TenantId.ShouldBe("tenant-a");
        resolved.Evidence.Classification.ShouldBe(PartyIdentityClassification.Organization);
        AssertNoProfile(resolved);

        const string otherId = "other-organization";
        DomainResult otherCreated = await lane.Dispatch(lane.Plain(new CreateParty
        {
            PartyId = otherId,
            Type = PartyType.Organization,
            OrganizationDetails = new OrganizationDetails { LegalName = "Other Org", IsNaturalPerson = false },
        }, "tenant-a", otherId));
        await lane.Commit("tenant-a", otherId, otherCreated);
        PartyIdentityResult other = await lane.Current("tenant-a", otherId, expectedActor: null);
        other.Evidence!.PartyId.ShouldBe(otherId);
        other.Evidence.PartyId.ShouldNotBe(partyId);
        other.Evidence.Classification.ShouldBe(PartyIdentityClassification.Organization);

        int reads = lane.Directory.AuthoritativeReads;
        PartyIdentityResult crossed = await lane.Current("tenant-a", otherId, expectedActor: null, envelopeParty: partyId);
        crossed.Outcome.ShouldBe(PartyIdentityOutcome.Unavailable);
        crossed.Evidence.ShouldBeNull();
        lane.Directory.AuthoritativeReads.ShouldBe(reads);

        int count = lane.Directory.Read("tenant-a", partyId).Count;
        string hash = lane.Directory.Fingerprint("tenant-a", partyId);
        DomainResult retyped = await lane.Dispatch(lane.Plain(new CreateParty
        {
            PartyId = partyId,
            Type = PartyType.Person,
            PersonDetails = new PersonDetails { FirstName = "Ada", LastName = "Lovelace" },
        }, "tenant-a", partyId));
        lane.AssertUnchanged("tenant-a", partyId, count, hash, retyped);
        (await lane.Current("tenant-a", partyId, expectedActor: null)).Evidence!.Classification.ShouldBe(PartyIdentityClassification.Organization);
    }

    /// <summary>Missing, inactive, restricted, erased, and unreadable sources stay fail-closed.</summary>
    [Fact]
    public async Task P06_MissingInactiveRestrictedErasedAndUnreadableSourcesFailClosed()
    {
        using Lane lane = Lane.Open(registerHostSeams: true);
        (await lane.Current("tenant-a", "missing-party", expectedActor: null)).Outcome.ShouldBe(PartyIdentityOutcome.Unavailable);

        const string inactiveId = "human-inactive";
        await lane.CreatePerson(inactiveId, "Grace", "Hopper");
        DomainResult deactivated = await lane.Dispatch(lane.Plain(new DeactivateParty { PartyId = inactiveId }, "tenant-a", inactiveId));
        await lane.Commit("tenant-a", inactiveId, deactivated);
        PartyIdentityResult inactive = await lane.Current("tenant-a", inactiveId, expectedActor: null);
        inactive.Outcome.ShouldBe(PartyIdentityOutcome.Ineligible);
        inactive.Evidence!.IsActive.ShouldBeFalse();
        (await lane.Current("tenant-a", inactiveId, expectedActor: null)).Outcome.ShouldBe(PartyIdentityOutcome.Ineligible);

        const string boundId = "human-source";
        await lane.CreatePerson(boundId, "Katherine", "Johnson");
        (_, DomainResult bound) = await lane.Establish(boundId, ActorId, "bind-source");
        await lane.Commit("tenant-a", boundId, bound, ActorId);
        await AssertFault(lane, boundId, "stale");
        await AssertFault(lane, boundId, "gap");
        await AssertFault(lane, boundId, "future");
        await AssertFault(lane, boundId, "outage");
        (await lane.Current("tenant-a", boundId, ActorId)).Outcome.ShouldBe(PartyIdentityOutcome.Resolved);

        const string restrictedId = "human-restricted";
        await lane.CreatePerson(restrictedId, "Margaret", "Hamilton");
        lane.Directory.AppendPlain("tenant-a", restrictedId, typeof(ProcessingRestricted).FullName!, EventBytes(
            new ProcessingRestricted { PartyId = restrictedId, TenantId = "tenant-a", RestrictedAt = lane.Clock.UtcNow, Reason = "hold" }), lane.Clock.UtcNow);
        PartyIdentityResult restricted = await lane.Current("tenant-a", restrictedId, expectedActor: null);
        restricted.Outcome.ShouldBe(PartyIdentityOutcome.Ineligible);
        restricted.Evidence!.IsRestricted.ShouldBeTrue();
        (await lane.Current("tenant-a", restrictedId, expectedActor: null)).Outcome.ShouldBe(PartyIdentityOutcome.Ineligible);

        const string erasedId = "human-erased";
        await lane.CreatePerson(erasedId, "Radia", "Perlman");
        lane.Directory.AppendPlain("tenant-a", erasedId, typeof(ErasePartyRequested).FullName!, EventBytes(
            new ErasePartyRequested { PartyId = erasedId, TenantId = "tenant-a", RequestedAt = lane.Clock.UtcNow, RequestedBy = OperatorId }), lane.Clock.UtcNow);
        PartyIdentityResult erased = await lane.Current("tenant-a", erasedId, expectedActor: null);
        erased.Outcome.ShouldBe(PartyIdentityOutcome.Ineligible);
        erased.Evidence!.IsErasingOrErased.ShouldBeTrue();
        AssertNoProfile(erased);
    }

    /// <summary>Only a verified matching active human is eligible. An organization is not that approver.</summary>
    [Fact]
    public async Task P07_OnlyMatchingActiveHumanIsEligible()
    {
        using Lane lane = Lane.Open(registerHostSeams: true);
        (string partyId, _, DomainResult created) = await lane.Provision("tenant-a", "provision-org");
        await lane.Commit("tenant-a", partyId, created);
        PartyIdentityResult organization = await lane.Current("tenant-a", partyId, ActorId);
        organization.Outcome.ShouldBe(PartyIdentityOutcome.Resolved);
        organization.Evidence!.Classification.ShouldBe(PartyIdentityClassification.Organization);
        organization.Evidence.HumanBinding.ShouldBeNull();

        const string humanId = "human-approver";
        await lane.CreatePerson(humanId, "Ada", "Lovelace");
        (_, DomainResult bound) = await lane.Establish(humanId, ActorId, "bind-approver");
        await lane.Commit("tenant-a", humanId, bound, ActorId);
        PartyIdentityResult matched = await lane.Current("tenant-a", humanId, ActorId);
        matched.Outcome.ShouldBe(PartyIdentityOutcome.Resolved);
        matched.Evidence!.Classification.ShouldBe(PartyIdentityClassification.Human);
        matched.Evidence.HumanBinding!.ActorId.ShouldBe(ActorId);
        AssertNoProfile(matched);

        PartyIdentityResult mismatched = await lane.Current("tenant-a", humanId, SuccessorId);
        mismatched.Outcome.ShouldNotBe(PartyIdentityOutcome.Resolved);
        mismatched.Evidence?.HumanBinding.ShouldBeNull();
        int reads = lane.Directory.AuthoritativeReads;
        PartyIdentityResult forgedActor = await lane.Current("tenant-a", humanId, ActorId, admittedActor: SuccessorId);
        forgedActor.Outcome.ShouldBe(PartyIdentityOutcome.Unavailable);
        lane.Directory.AuthoritativeReads.ShouldBe(reads);
        (await lane.Current("tenant-a", humanId, expectedActor: null)).Outcome.ShouldBe(PartyIdentityOutcome.Ineligible);
    }

    /// <summary>Exclusive expiry, pending destruction, and actor-free successor continuity.</summary>
    [Fact]
    public async Task P08_BoundaryExpiryDestructionAndSuccessorContinuity()
    {
        using Lane lane = Lane.Open(registerHostSeams: true);
        const string partyId = "human-successor";
        await lane.CreatePerson(partyId, "Ada", "Lovelace");
        (_, DomainResult establishedResult) = await lane.Establish(partyId, ActorId, "bind-predecessor");
        HumanActorBindingEstablished established = establishedResult.Events.OfType<HumanActorBindingEstablished>().Single();
        await lane.Commit("tenant-a", partyId, establishedResult, ActorId);
        DateTimeOffset predecessorFrom = established.EffectiveAt;
        IdentityHistoryCustodyEvidence predecessorCustody = established.Custody;
        lane.Clock.UtcNow = predecessorFrom.AddDays(30);
        (_, DomainResult reboundResult) = await lane.Rebind(partyId, SuccessorId, "bind-successor", expectedBindingVersion: 1);
        HumanActorBindingRebound rebound = reboundResult.Events.OfType<HumanActorBindingRebound>().Single();
        await lane.Commit("tenant-a", partyId, reboundResult, SuccessorId);
        DateTimeOffset successorFrom = rebound.EffectiveAt;
        AssertCiphertextOmitsActor(lane, predecessorCustody.EvidenceId, ActorId);
        AssertCiphertextOmitsActor(lane, rebound.Custody.EvidenceId, SuccessorId);

        HumanActorBindingResult before = await lane.History("tenant-a", partyId, successorFrom.AddTicks(-1), ActorId, 1);
        before.Outcome.ShouldBe(HumanActorBindingOutcome.Resolved);
        before.Evidence!.ActorId.ShouldBe(ActorId);
        HumanActorBindingResult atBoundary = await lane.History("tenant-a", partyId, successorFrom, SuccessorId, 2);
        atBoundary.Outcome.ShouldBe(HumanActorBindingOutcome.Resolved);
        atBoundary.Evidence!.ActorId.ShouldBe(SuccessorId);
        (await lane.History("tenant-a", partyId, successorFrom, ActorId, 1)).Outcome.ShouldNotBe(HumanActorBindingOutcome.Resolved);

        lane.Clock.UtcNow = predecessorCustody.ExpiresAt.AddTicks(-1);
        (await lane.Custody.DestroyExpiredAsync(new AggregateIdentity("tenant-a", "party", partyId), predecessorCustody, CancellationToken.None)).ShouldBeFalse();
        lane.Custody.EveryCopyIsUnreadable(predecessorCustody.EvidenceId).ShouldBeFalse();
        (await lane.History("tenant-a", partyId, predecessorFrom, ActorId, 1)).Outcome.ShouldBe(HumanActorBindingOutcome.Resolved);

        lane.Clock.UtcNow = predecessorCustody.ExpiresAt;
        HumanActorBindingResult expired = await lane.History("tenant-a", partyId, predecessorFrom, ActorId, 1);
        expired.Outcome.ShouldBe(HumanActorBindingOutcome.Expired);
        expired.Evidence.ShouldBeNull();
        (await lane.Current("tenant-a", partyId, ActorId)).Outcome.ShouldNotBe(PartyIdentityOutcome.Resolved);

        lane.Custody.HoldAcknowledgement(predecessorCustody.EvidenceId);
        (await lane.Custody.DestroyExpiredAsync(new AggregateIdentity("tenant-a", "party", partyId), predecessorCustody, CancellationToken.None)).ShouldBeFalse();
        lane.Custody.IsPending(predecessorCustody.EvidenceId).ShouldBeTrue();
        lane.Custody.EveryCopyIsUnreadable(predecessorCustody.EvidenceId).ShouldBeTrue();
        AssertCiphertextOmitsActor(lane, predecessorCustody.EvidenceId, ActorId);
        lane.Restart();
        lane.Custody.IsPending(predecessorCustody.EvidenceId).ShouldBeTrue();
        (await lane.Custody.DestroyExpiredAsync(new AggregateIdentity("tenant-a", "party", partyId), predecessorCustody, CancellationToken.None)).ShouldBeFalse();
        HumanActorBindingResult pendingSuccessor = await lane.History("tenant-a", partyId, successorFrom, SuccessorId, 2);
        pendingSuccessor.Outcome.ShouldNotBe(HumanActorBindingOutcome.Resolved);
        pendingSuccessor.Evidence.ShouldBeNull();

        lane.Custody.Acknowledge(predecessorCustody.EvidenceId);
        (await lane.Custody.DestroyExpiredAsync(new AggregateIdentity("tenant-a", "party", partyId), predecessorCustody, CancellationToken.None)).ShouldBeTrue();
        lane.Custody.IsPending(predecessorCustody.EvidenceId).ShouldBeFalse();
        lane.Restart();
        HumanActorBindingResult successor = await lane.History("tenant-a", partyId, successorFrom, SuccessorId, 2);
        successor.Outcome.ShouldBe(HumanActorBindingOutcome.Resolved);
        successor.Evidence!.ActorId.ShouldBe(SuccessorId);
        HumanActorBindingResult predecessor = await lane.History("tenant-a", partyId, predecessorFrom, ActorId, 1);
        predecessor.Outcome.ShouldNotBe(HumanActorBindingOutcome.Resolved);
        predecessor.Evidence.ShouldBeNull();
        JsonSerializer.Serialize(predecessor, PartiesJsonOptions.Default).ShouldNotContain(ActorId);

        IReadOnlyList<PersistedPartyEvent> stored = lane.Directory.Read("tenant-a", partyId);
        long establishedSequence = stored.Single(item => item.EventTypeName == typeof(HumanActorBindingEstablished).FullName).Sequence;
        long reboundSequence = stored.Single(item => item.EventTypeName == typeof(HumanActorBindingRebound).FullName).Sequence;
        long[] profiles = stored.Where(item => !item.Protected).Select(item => item.Sequence).ToArray();
        SaveOverride(lane, partyId, stored.Count, [Readable(reboundSequence, rebound, successorFrom)], [.. profiles, establishedSequence]);
        HumanActorBindingResult relabeled = await lane.History("tenant-a", partyId, successorFrom, SuccessorId, 2);
        relabeled.Outcome.ShouldBe(HumanActorBindingOutcome.Unavailable);
        relabeled.Evidence.ShouldBeNull();

        SaveOverride(lane, partyId, stored.Count, [Readable(establishedSequence, established, predecessorFrom), Readable(reboundSequence, rebound, successorFrom)], profiles);
        HumanActorBindingResult revived = await lane.History("tenant-a", partyId, predecessorFrom, ActorId, 1);
        revived.Outcome.ShouldNotBe(HumanActorBindingOutcome.Resolved);
        revived.Evidence.ShouldBeNull();
        JsonSerializer.Serialize(revived, PartiesJsonOptions.Default).ShouldNotContain(ActorId);
    }

    /// <summary>Gap, overlap, and a conflicting revision fail closed with no current-binding fallback.</summary>
    [Fact]
    public async Task P09_GapOverlapAndConflictingRevisionDoNotFallBack()
    {
        using Lane lane = Lane.Open(registerHostSeams: true);
        const string partyId = "human-history";
        await lane.CreatePerson(partyId, "Ada", "Lovelace");
        (_, DomainResult bound) = await lane.Establish(partyId, ActorId, "bind-history");
        await lane.Commit("tenant-a", partyId, bound, ActorId);
        DateTimeOffset effective = bound.Events.OfType<HumanActorBindingEstablished>().Single().EffectiveAt;
        PartyIdentityResult current = await lane.Current("tenant-a", partyId, ActorId);
        current.Outcome.ShouldBe(PartyIdentityOutcome.Resolved);
        current.Evidence!.HumanBinding!.ActorId.ShouldBe(ActorId);

        HumanActorBindingResult gap = await lane.History("tenant-a", partyId, effective.AddTicks(-1), ActorId, 1);
        gap.Outcome.ShouldBe(HumanActorBindingOutcome.Gap);
        gap.Evidence.ShouldBeNull();
        HumanActorBindingResult mismatch = await lane.History("tenant-a", partyId, effective, ActorId, 99);
        mismatch.Outcome.ShouldBe(HumanActorBindingOutcome.Mismatch);
        mismatch.Evidence.ShouldBeNull();
        (await lane.Current("tenant-a", partyId, ActorId)).Evidence!.HumanBinding!.ActorId.ShouldBe(ActorId);

        DateTimeOffset start = effective;
        var firstCustody = new IdentityHistoryCustodyEvidence(PolicyId, Purpose, start.AddDays(365), 1, "overlap-a", true, true, true);
        var secondCustody = new IdentityHistoryCustodyEvidence(PolicyId, Purpose, start.AddDays(30).AddDays(365), 1, "overlap-b", true, true, true);
        var first = new HumanActorBindingEstablished(new HumanActorBinding(new HumanActorBindingEvidence(
            "tenant-a", partyId, ActorId, 1, 1, start, firstCustody.ExpiresAt, "trusted-writer", OperatorId, firstCustody), "overlap-a", new string('a', 64)), start, 0);
        var second = new HumanActorBindingEstablished(new HumanActorBinding(new HumanActorBindingEvidence(
            "tenant-a", partyId, SuccessorId, 1, 1, start.AddDays(30), secondCustody.ExpiresAt, "trusted-writer", OperatorId, secondCustody), "overlap-b", new string('b', 64)), start.AddDays(30), 0);
        SaveOverride(lane, partyId, 2, [Readable(1, first, start), Readable(2, second, start.AddDays(30))], []);
        HumanActorBindingResult overlap = await lane.History("tenant-a", partyId, start, ActorId, 1);
        overlap.Outcome.ShouldBe(HumanActorBindingOutcome.Unavailable);
        overlap.Evidence.ShouldBeNull();
        (await lane.Current("tenant-a", partyId, ActorId)).Evidence!.HumanBinding!.ActorId.ShouldBe(ActorId);

        int retained = lane.Directory.RetainedReads;
        string hash = lane.Directory.Fingerprint("tenant-a", partyId);
        HumanActorBindingResult foreign = await lane.History("tenant-b", partyId, effective, ActorId, 1, proofTenant: "tenant-a", proofParty: partyId);
        foreign.Outcome.ShouldBe(HumanActorBindingOutcome.Unavailable);
        foreign.Evidence.ShouldBeNull();
        lane.Directory.RetainedReads.ShouldBe(retained);
        lane.Directory.Fingerprint("tenant-a", partyId).ShouldBe(hash);
    }

    /// <summary>Caller, editor, and regenerator aliases and a revoked actor are excluded. Consumer identity-by-id remains.</summary>
    [Fact]
    public async Task P10_AliasAndRevocationExclusionsPreserveConsumerIdentity()
    {
        using Lane lane = Lane.Open(registerHostSeams: true);
        const string consumerId = "consumer-organization";
        DomainResult consumerCreated = await lane.Dispatch(lane.Plain(new CreateParty
        {
            PartyId = consumerId,
            Type = PartyType.Organization,
            OrganizationDetails = new OrganizationDetails { LegalName = "Consumer Org", IsNaturalPerson = false },
        }, "tenant-a", consumerId));
        await lane.Commit("tenant-a", consumerId, consumerCreated);
        PartyIdentityResult consumer = await lane.Current("tenant-a", consumerId, expectedActor: null);
        consumer.Outcome.ShouldBe(PartyIdentityOutcome.Resolved);
        consumer.Evidence!.Classification.ShouldBe(PartyIdentityClassification.Organization);
        consumer.Evidence.PartyId.ShouldBe(consumerId);

        const string humanId = "human-revoked";
        await lane.CreatePerson(humanId, "Ada", "Lovelace");
        (CommandEnvelope binding, DomainResult bound) = await lane.Establish(humanId, ActorId, "bind-stable");
        await lane.Commit("tenant-a", humanId, bound, ActorId);
        DateTimeOffset effective = bound.Events.OfType<HumanActorBindingEstablished>().Single().EffectiveAt;
        foreach (string alias in new[] { AliasCaller, AliasEditor, AliasRegenerator })
        {
            PartyIdentityResult excluded = await lane.Current("tenant-a", humanId, alias);
            excluded.Outcome.ShouldNotBe(PartyIdentityOutcome.Resolved);
            excluded.Evidence?.HumanBinding.ShouldBeNull();
        }

        lane.Directory.ReadFault = "outage";
        PartyIdentityResult unavailable = await lane.Current("tenant-a", humanId, ActorId);
        unavailable.Outcome.ShouldBe(PartyIdentityOutcome.Unavailable);
        unavailable.Evidence.ShouldBeNull();
        lane.Directory.ReadFault = null;
        (await lane.Current("tenant-a", humanId, ActorId)).Outcome.ShouldBe(PartyIdentityOutcome.Resolved);

        int count = lane.Directory.Read("tenant-a", humanId).Count;
        lane.Clock.UtcNow = effective.AddDays(1);
        DomainResult revoked = await lane.Dispatch(lane.Signed(
            new RevokeHumanActorBinding("tenant-a", humanId, ActorId, 1, count, 1, lane.Clock.UtcNow, "bind-revoke", PolicyId),
            "bind-revoke", ActorId, "trusted-writer"));
        await lane.Commit("tenant-a", humanId, revoked, ActorId);
        PartyIdentityResult current = await lane.Current("tenant-a", humanId, ActorId);
        current.Outcome.ShouldBe(PartyIdentityOutcome.Ineligible);
        current.Evidence!.HumanBinding.ShouldBeNull();
        HumanActorBindingResult historical = await lane.History("tenant-a", humanId, effective, ActorId, 1);
        historical.Outcome.ShouldBe(HumanActorBindingOutcome.Resolved);
        historical.Evidence!.ActorId.ShouldBe(ActorId);
        (await lane.Retry(binding, "bind-stable", ActorId, "trusted-writer")).Events.ShouldBeEmpty();
    }

    private static async Task AssertFault(Lane lane, string partyId, string fault)
    {
        lane.Directory.ReadFault = fault;
        PartyIdentityResult first = await lane.Current("tenant-a", partyId, ActorId).ConfigureAwait(false);
        PartyIdentityResult second = await lane.Current("tenant-a", partyId, ActorId).ConfigureAwait(false);
        first.Outcome.ShouldBe(PartyIdentityOutcome.Unavailable);
        second.Outcome.ShouldBe(PartyIdentityOutcome.Unavailable);
        first.Evidence.ShouldBeNull();
        lane.Directory.ReadFault = null;
    }

    private static void SaveOverride(Lane lane, string partyId, long head, StreamReadEvent[] events, long[] excluded)
    {
        var stream = new RetainedIdentityHistoryStream(
            new AggregateIdentity("tenant-a", "party", partyId), Purpose, head, lane.Clock.UtcNow, events, excluded, "override-observation")
        {
            AuthorityRevision = "persisted-source-r1",
            ValidUntil = lane.Clock.UtcNow.AddMinutes(1),
            ExpiredEvents = [],
        };
        lane.Directory.SaveRetainedOverride("tenant-a", partyId, stream);
    }

    private static StreamReadEvent Readable(long sequence, IEventPayload evt, DateTimeOffset timestamp)
        => new(sequence, evt.GetType().FullName!, EventBytes(evt),
            "json", 1, string.Empty, null, null, timestamp, null, EventStorePayloadProtectionMetadata.Unprotected());

    private static byte[] EventBytes(IEventPayload evt)
        => JsonSerializer.SerializeToUtf8Bytes(evt, evt.GetType(), EventStorePayloadSerialization.Options);

    private static void AssertCiphertextOmitsActor(Lane lane, string evidenceId, string actorId)
    {
        foreach (string copy in new[] { "source", "snapshot", "cache", "replica", "backup" })
        {
            string path = Path.Combine(lane.Directory.Root, "custody", evidenceId, "copies", copy + ".bin");
            Omits(File.ReadAllBytes(path), actorId);
        }
    }

    private static void Omits(byte[] haystack, string needle)
    {
        byte[] encoded = Encoding.UTF8.GetBytes(needle);
        for (int index = 0; index <= haystack.Length - encoded.Length; index++)
        {
            haystack.AsSpan(index, encoded.Length).SequenceEqual(encoded).ShouldBeFalse();
        }
    }

    private static void AssertNoProfile(PartyIdentityResult result)
    {
        string json = JsonSerializer.Serialize(result, PartiesJsonOptions.Default);
        json.ShouldNotContain("PartyDetail");
        json.ShouldNotContain("displayName");
        json.ShouldNotContain("firstName");
        json.ShouldNotContain("LegalName");
        json.ShouldNotContain("Ada");
    }

    private static string Reason(DomainResult result)
        => result.Events.Single() switch
        {
            PartyCommandValidationRejected rejected => rejected.Failures.Single().ErrorCode,
            AgentPartyProvisioningRejected rejected => rejected.ReasonCode,
            HumanActorBindingRejected rejected => rejected.ReasonCode,
            IEventPayload other => throw new InvalidOperationException(other.GetType().FullName),
        };

    private static string Forge(string proof)
    {
        char tail = proof[^1] == 'A' ? 'B' : 'A';
        return proof[..^1] + tail;
    }

    private sealed class Lane : IDisposable
    {
        private readonly string _root;
        private readonly bool _registerHostSeams;
        private ServiceProvider _services;
        private int _messages = 0x100;

        private Lane(string root, bool registerHostSeams)
        {
            _root = root;
            _registerHostSeams = registerHostSeams;
            Clock = new MutableUtcClock { UtcNow = Started };
            Directory = new PersistedPartyIdentityDirectory(root);
            Key = RSA.Create(2048);
            (_services, Protection) = Build();
        }

        public MutableUtcClock Clock { get; }

        public PersistedPartyIdentityDirectory Directory { get; }

        public RSA Key { get; }

        public ServiceProvider Services => _services;

        public IEventPayloadProtectionService Protection { get; private set; }

        public TestOnlyIdentityHistoryCustody Custody { get; private set; } = null!;

        public PersistedPartyIdentitySource? Source { get; private set; }

        public static Lane Open(bool registerHostSeams)
            => new(Path.Combine(Path.GetTempPath(), "ext-parties-1-" + Guid.NewGuid().ToString("N")), registerHostSeams);

        public void Restart()
        {
            _services.Dispose();
            (_services, Protection) = Build();
        }

        public async Task<(string PartyId, CommandEnvelope Command, DomainResult Result)> Provision(string tenant, string logicalId)
        {
            string partyId = AgentPartyIdMapping.Create(tenant, AgentId);
            var body = new ProvisionAgentParty(new AgentPartyIdentity(tenant, AgentId, partyId, 1, logicalId, new string('a', 64), Started));
            CommandEnvelope command = Signed(body, logicalId, AgentId, "trusted-provisioner");
            return (partyId, command, await Dispatch(command).ConfigureAwait(false));
        }

        public async Task CreatePerson(string partyId, string firstName, string lastName)
        {
            DomainResult result = await Dispatch(Plain(new CreateParty
            {
                PartyId = partyId,
                Type = PartyType.Person,
                PersonDetails = new PersonDetails { FirstName = firstName, LastName = lastName },
            }, "tenant-a", partyId)).ConfigureAwait(false);
            await Commit("tenant-a", partyId, result).ConfigureAwait(false);
            DateTimeOffset createdAt = result.Events.OfType<PartyCreated>().Single().CreatedAt;
            if (createdAt > Clock.UtcNow)
            {
                Clock.UtcNow = createdAt;
            }
        }

        public async Task<(CommandEnvelope Command, DomainResult Result)> Establish(string partyId, string actorId, string logicalId)
        {
            long revision = Directory.Read("tenant-a", partyId).Count;
            var body = new EstablishHumanActorBinding("tenant-a", partyId, actorId, 1, revision, 0, Clock.UtcNow, logicalId, PolicyId);
            CommandEnvelope command = Signed(body, logicalId, actorId, "trusted-writer");
            return (command, await Dispatch(command).ConfigureAwait(false));
        }

        public async Task<(CommandEnvelope Command, DomainResult Result)> Rebind(string partyId, string actorId, string logicalId, long expectedBindingVersion)
        {
            long revision = Directory.Read("tenant-a", partyId).Count;
            var body = new RebindHumanActorBinding("tenant-a", partyId, actorId, 1, revision, expectedBindingVersion, Clock.UtcNow, logicalId, PolicyId);
            CommandEnvelope command = Signed(body, logicalId, actorId, "trusted-writer");
            return (command, await Dispatch(command).ConfigureAwait(false));
        }

        public CommandEnvelope Signed<T>(T body, string logicalId, string? targetActor, string sourceId, string? envelopeTenant = null, string? envelopeParty = null)
        {
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(body, PartiesJsonOptions.Default);
            string tenant = envelopeTenant ?? TenantOf(body);
            string party = envelopeParty ?? PartyOf(body);
            string messageId = NextMessage();
            var scope = new IdentityAdmissionScope(tenant, "party", party, typeof(T).FullName!, messageId, logicalId, IdentityAdmissionProof.Digest(payload));
            return new CommandEnvelope(messageId, tenant, "party", party, typeof(T).FullName!, payload, "correlation", null, "writer",
                new Dictionary<string, string> { [IdentityAdmissionProof.ExtensionKey] = Sign(scope, targetActor, sourceId) });
        }

        public CommandEnvelope Plain<T>(T body, string tenant, string party)
        {
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(body, PartiesJsonOptions.Default);
            return new CommandEnvelope(NextMessage(), tenant, "party", party, typeof(T).FullName!, payload, "correlation", null, "writer", []);
        }

        public static IdentityAdmissionScope Scope(CommandEnvelope command, string logicalId)
            => new(command.TenantId, command.Domain, command.AggregateId, command.CommandType, command.MessageId, logicalId, IdentityAdmissionProof.Digest(command.Payload));

        public string Sign(IdentityAdmissionScope scope, string? targetActor, string sourceId)
        {
            DateTimeOffset issued = Clock.UtcNow;
            var evidence = new IdentityAdmissionEvidence(scope, sourceId, OperatorId, targetActor, 1, true, issued, issued.AddMinutes(1), 1);
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(evidence);
            byte[] signature = Key.SignData(IdentityAdmissionProof.Encode(payload), HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
            return Convert.ToBase64String(payload) + "." + Convert.ToBase64String(signature);
        }

        public async Task<DomainResult> Retry(CommandEnvelope original, string logicalId, string? targetActor, string sourceId)
        {
            string messageId = NextMessage();
            var scope = new IdentityAdmissionScope(original.TenantId, original.Domain, original.AggregateId, original.CommandType, messageId, logicalId, IdentityAdmissionProof.Digest(original.Payload));
            var retry = original with
            {
                MessageId = messageId,
                Extensions = new Dictionary<string, string> { [IdentityAdmissionProof.ExtensionKey] = Sign(scope, targetActor, sourceId) },
            };
            return await Dispatch(retry).ConfigureAwait(false);
        }

        public async Task<DomainResult> Dispatch(CommandEnvelope command)
        {
            using IServiceScope scope = Services.CreateScope();
            IAsyncDomainProcessor processor = scope.ServiceProvider.GetRequiredKeyedService<IAsyncDomainProcessor>("party");
            return await processor.ProcessAsync(command, Source?.CommandState(command.TenantId, command.AggregateId), TestContext.Current.CancellationToken).ConfigureAwait(false);
        }

        public async Task Commit(string tenant, string party, DomainResult result, string? protectedActorId = null)
        {
            result.IsRejection.ShouldBeFalse();
            foreach (IEventPayload evt in result.Events)
            {
                byte[] plain = EventBytes(evt);
                DateTimeOffset timestamp = evt switch
                {
                    PartyCreated created => created.CreatedAt,
                    HumanActorBindingEstablished established => established.EffectiveAt,
                    HumanActorBindingRebound rebound => rebound.EffectiveAt,
                    HumanActorBindingRevoked revoked => revoked.EffectiveAt,
                    _ => Clock.UtcNow,
                };
                if (evt is IIdentityHistoryEvent history)
                {
                    PayloadProtectionResult protectedPayload = await Custody.ProtectEventAsync(
                        new AggregateIdentity(tenant, "party", party), evt.GetType().FullName!, plain, "json", history.Custody, CancellationToken.None).ConfigureAwait(false);
                    if (protectedActorId is not null)
                    {
                        Omits(protectedPayload.PayloadBytes, protectedActorId);
                    }

                    string digest = Convert.ToHexString(SHA256.HashData(protectedPayload.PayloadBytes));
                    Directory.AppendProtected(tenant, party, evt.GetType().FullName!, protectedPayload.PayloadBytes, timestamp, history.Custody.EvidenceId, digest);
                }
                else
                {
                    Directory.AppendPlain(tenant, party, evt.GetType().FullName!, plain, timestamp);
                }
            }
        }

        public void CommitRejection(string tenant, string party, DomainResult result)
        {
            result.IsRejection.ShouldBeTrue();
            IEventPayload evt = result.Events.Single();
            Directory.AppendPlain(tenant, party, evt.GetType().FullName!, EventBytes(evt), Clock.UtcNow);
        }

        public void AssertUnchanged(string tenant, string party, int count, string fingerprint, DomainResult result)
        {
            (result.IsRejection || result.Events.Count == 0).ShouldBeTrue();
            Directory.Read(tenant, party).Count.ShouldBe(count);
            Directory.Fingerprint(tenant, party).ShouldBe(fingerprint);
        }

        public async Task<PartyIdentityResult> Current(string tenant, string party, string? expectedActor, string? proofTenant = null, string? proofParty = null, string? envelopeParty = null, string? admittedActor = null)
        {
            var query = new ResolvePartyIdentity(tenant, party, expectedActor);
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(query, PartiesJsonOptions.Default);
            string correlation = NextMessage();
            string proofTenantId = proofTenant ?? tenant;
            string proofPartyId = proofParty ?? party;
            var proofScope = new IdentityAdmissionScope(proofTenantId, "party", proofPartyId, typeof(ResolvePartyIdentity).FullName!, correlation, correlation, IdentityAdmissionProof.Digest(payload));
            var envelope = new QueryEnvelope(tenant, "party", envelopeParty ?? party, typeof(ResolvePartyIdentity).FullName!, payload, correlation, "reader", envelopeParty ?? party)
            {
                IdentityAdmissionProof = Sign(proofScope, admittedActor ?? expectedActor, "trusted-reader"),
            };
            using IServiceScope scope = Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<PartyIdentityQueryService>().ResolveAsync(envelope, TestContext.Current.CancellationToken).ConfigureAwait(false);
        }

        public async Task<HumanActorBindingResult> History(string tenant, string party, DateTimeOffset actionAt, string actorId, long version, bool forge = false, string? proofTenant = null, string? proofParty = null)
        {
            var query = new ResolveHumanActorBindingAt(tenant, party, actionAt, actorId, version);
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(query, PartiesJsonOptions.Default);
            string correlation = NextMessage();
            var proofScope = new IdentityAdmissionScope(proofTenant ?? tenant, "party", proofParty ?? party, typeof(ResolveHumanActorBindingAt).FullName!, correlation, correlation, IdentityAdmissionProof.Digest(payload));
            string proof = Sign(proofScope, actorId, "trusted-reader");
            var envelope = new QueryEnvelope(tenant, "party", party, typeof(ResolveHumanActorBindingAt).FullName!, payload, correlation, "reader", party)
            {
                IdentityAdmissionProof = forge ? Forge(proof) : proof,
            };
            using IServiceScope scope = Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<PartyIdentityQueryService>().ResolveAtAsync(envelope, TestContext.Current.CancellationToken).ConfigureAwait(false);
        }

        public void Dispose()
        {
            _services.Dispose();
            Key.Dispose();
            try
            {
                System.IO.Directory.Delete(_root, recursive: true);
            }
            catch (IOException)
            {
            }
        }

        private (ServiceProvider Services, IEventPayloadProtectionService Protection) Build()
        {
            IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["IdentityAdmission:VerificationKeyPem"] = Key.ExportSubjectPublicKeyInfoPem(),
                ["IdentityAdmission:AuthorityRevision"] = "1",
                ["Parties:Identity:PolicyId"] = PolicyId,
                ["Parties:Identity:Retention"] = "365.00:00:00",
                ["Parties:Identity:ExpiryTrigger"] = "binding-effective-at",
                ["Parties:Identity:QueryTimeout"] = "00:00:30",
                ["Tenants:PubSubName"] = "pubsub",
                ["Tenants:TopicName"] = "system.tenants.events",
            }).Build();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<TimeProvider>(Clock);
            services.AddSingleton(Substitute.For<DaprClient>());
            services.AddParties(configuration);
            services.Any(descriptor => descriptor.ServiceType == typeof(IIdentityHistoryCustody)).ShouldBeFalse();
            services.Any(descriptor => descriptor.ServiceType == typeof(IRetainedIdentityHistoryReader)).ShouldBeFalse();
            if (_registerHostSeams)
            {
                Custody = new TestOnlyIdentityHistoryCustody(Directory, Clock);
                Source = new PersistedPartyIdentitySource(Directory, Custody, Clock);
                services.AddSingleton<IIdentityHistoryCustody>(Custody);
                services.AddSingleton<IAuthoritativeEventStreamReader>(Source);
                services.AddSingleton<IRetainedIdentityHistoryReader>(Source);
            }

            IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
            if (_registerHostSeams)
            {
                protection.UnprotectEventPayloadAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                    .Returns(call =>
                    {
                        byte[] payload = call.ArgAt<byte[]>(2);
                        string format = call.ArgAt<string>(3);
                        if (format == "json+identity-history-v1")
                        {
                            return Custody.UnprotectEventAsync(call.ArgAt<AggregateIdentity>(0), call.ArgAt<string>(1), payload, format, call.ArgAt<CancellationToken>(4)).GetAwaiter().GetResult();
                        }

                        return new PayloadProtectionResult(payload.ToArray(), "json");
                    });
            }

            services.AddSingleton(protection);
            return (services.BuildServiceProvider(), protection);
        }

        private string NextMessage() => "01HX000000000000000000" + _messages++.ToString("X4");

        private static string TenantOf<T>(T body)
            => body switch
            {
                ProvisionAgentParty provision => provision.Identity.TenantId,
                EstablishHumanActorBinding establish => establish.TenantId,
                RebindHumanActorBinding rebind => rebind.TenantId,
                RevokeHumanActorBinding revoke => revoke.TenantId,
                _ => "tenant-a",
            };

        private static string PartyOf<T>(T body)
            => body switch
            {
                ProvisionAgentParty provision => provision.Identity.PartyId,
                EstablishHumanActorBinding establish => establish.PartyId,
                RebindHumanActorBinding rebind => rebind.PartyId,
                RevokeHumanActorBinding revoke => revoke.PartyId,
                _ => throw new ArgumentException("The command has no party id.", nameof(body)),
            };
    }
}
