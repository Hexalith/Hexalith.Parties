using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Security;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Events.Rejections;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Results;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.State;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Domain;

public sealed partial class PartyAggregate
{
    /// <summary>Provisions creation and immutable marker in one domain result.</summary>
    public static DomainResult Handle(ProvisionAgentParty command, PartyState? state)
    {
        ArgumentNullException.ThrowIfNull(command);
        AgentPartyIdentity intent = command.Identity;
        IdentityCommandAuthorization? authorization = command.Authorization;
        if (authorization is null || intent is null || intent.ContractVersion != 1
            || string.IsNullOrWhiteSpace(intent.LogicalId) || intent.CreationFingerprint is not { Length: 64 } || !intent.CreationFingerprint.All(Uri.IsHexDigit)
            || authorization.Admission.Scope.TenantId != intent.TenantId
            || authorization.Admission.Scope.AggregateId != intent.PartyId
            || authorization.Admission.Scope.LogicalId != intent.LogicalId)
        {
            return ProvisionRejected("authority-unavailable");
        }

        try
        {
            if (AgentPartyIdMapping.Create(intent.TenantId, intent.AgentId) != intent.PartyId)
            {
                return ProvisionRejected("intent-conflict");
            }
        }
        catch (ArgumentException)
        {
            return ProvisionRejected("intent-conflict");
        }

        if (state?.AgentProvisioning is { } original)
        {
            return original.Identity == intent
                ? new PartyIdentityCommandResult([], original)
                : ProvisionRejected("intent-conflict");
        }

        if (state is { HasBeenCreated: true } || state is not null && state.Type != default)
        {
            return ProvisionRejected("occupied-unmarked-identity");
        }

        var result = new AgentPartyProvisioningResult(intent, checked(authorization.SourcePosition + 3), authorization.Admission.SourceId);
        IEventPayload[] events =
        [
            new PartyCreated { Type = PartyType.Organization, CreatedAt = intent.CreatedAt,
                OrganizationDetails = new OrganizationDetails { LegalName = "Agent", IsNaturalPerson = false } },
            new PartyDisplayNameDerived { DisplayName = "Agent", SortName = "Agent" },
            new AgentPartyProvisioned(result),
        ];
        return new PartyIdentityCommandResult(events, result);
    }

    /// <summary>Establishes a finite active human attribution interval.</summary>
    public static DomainResult Handle(EstablishHumanActorBinding command, PartyState? state)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (state is null)
        {
            return BindingRejected("binding-unavailable");
        }

        return Bind(command, state, command.Authorization, command.TenantId, command.PartyId, command.ActorId,
            command.ActorRevision, command.ExpectedPartyRevision, command.ExpectedBindingVersion,
            command.EffectiveAt, command.LogicalId, command.PolicyId, establish: true);
    }

    /// <summary>Rebinds at one shared immutable predecessor and successor boundary.</summary>
    public static DomainResult Handle(RebindHumanActorBinding command, PartyState? state)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (state is null)
        {
            return BindingRejected("binding-unavailable");
        }

        return Bind(command, state, command.Authorization, command.TenantId, command.PartyId, command.ActorId,
            command.ActorRevision, command.ExpectedPartyRevision, command.ExpectedBindingVersion,
            command.EffectiveAt, command.LogicalId, command.PolicyId, establish: false);
    }

    /// <summary>Revokes current attribution without replacing its recorded actor identity.</summary>
    public static DomainResult Handle(RevokeHumanActorBinding command, PartyState? state)
    {
        ArgumentNullException.ThrowIfNull(command);
        string digest = Digest(command);
        DomainResult? retry = RetryBinding(state, command.LogicalId, digest);
        if (retry is not null)
        {
            return retry;
        }

        IdentityCommandAuthorization? authorization = command.Authorization;
        if (!CanBind(state, authorization, command.TenantId, command.PartyId, command.ActorId,
                command.ActorRevision, command.ExpectedPartyRevision, command.ExpectedBindingVersion,
                command.EffectiveAt, command.PolicyId)
            || state!.HumanActorBindings.LastOrDefault()?.Evidence is not { } previous
            || previous.ActorId != command.ActorId || previous.ValidFrom >= command.EffectiveAt
            || previous.ValidUntil <= command.EffectiveAt)
        {
            return BindingRejected("binding-unavailable");
        }

        return new PartyIdentityCommandResult(
            [new HumanActorBindingRevoked(command.LogicalId, digest, command.ExpectedBindingVersion, command.EffectiveAt, authorization!.Custody!)],
            new HumanActorBindingTransition(command.LogicalId, digest, command.ExpectedBindingVersion + 1, null));
    }

    private static DomainResult Bind(object command, PartyState? state, IdentityCommandAuthorization? authorization,
        string tenantId, string partyId, string actorId, long actorRevision, long partyRevision, long bindingVersion,
        DateTimeOffset effectiveAt, string logicalId, string policyId, bool establish)
    {
        string digest = Digest(command);
        DomainResult? retry = RetryBinding(state, logicalId, digest);
        if (retry is not null)
        {
            return retry;
        }

        if (!CanBind(state, authorization, tenantId, partyId, actorId, actorRevision, partyRevision,
                bindingVersion, effectiveAt, policyId)
            || establish != (bindingVersion == 0)
            || state!.HumanActorBindings.Any(binding => binding.Evidence.ValidFrom >= effectiveAt))
        {
            return BindingRejected("binding-unavailable");
        }

        var evidence = new HumanActorBindingEvidence(tenantId, partyId, actorId, bindingVersion + 1,
            actorRevision, effectiveAt, authorization!.Custody!.ExpiresAt,
            authorization.Admission.SourceId, authorization.Admission.OperatorActorId!, authorization.Custody);
        var binding = new HumanActorBinding(evidence, logicalId, digest);
        IEventPayload transition = establish
            ? new HumanActorBindingEstablished(binding, effectiveAt, bindingVersion)
            : new HumanActorBindingRebound(binding, effectiveAt, bindingVersion);
        return new PartyIdentityCommandResult([transition], evidence);
    }

    private static bool CanBind(PartyState? state, IdentityCommandAuthorization? authorization, string tenantId,
        string partyId, string actorId, long actorRevision, long partyRevision, long bindingVersion,
        DateTimeOffset effectiveAt, string policyId)
        => state is { HasBeenCreated: true, Type: PartyType.Person, IsActive: true, IsRestricted: false, ErasureStatus: ErasureStatus.Active }
            && authorization?.Policy is { IsValid: true } policy && policy.PolicyId == policyId
            && authorization.Custody?.Satisfies(policy, effectiveAt) == true
            && authorization.SourcePosition == partyRevision && state.HumanBindingVersion == bindingVersion
            && authorization.Admission.Scope.TenantId == tenantId && authorization.Admission.Scope.AggregateId == partyId
            && authorization.Admission.TargetActorId == actorId && actorRevision > 0
            && authorization.Admission.ActorRevision == actorRevision && authorization.Admission.ActorActive
            && state.CreatedAt <= effectiveAt && effectiveAt <= authorization.Admission.IssuedAt
            && !string.IsNullOrWhiteSpace(authorization.Admission.OperatorActorId);

    private static DomainResult? RetryBinding(PartyState? state, string logicalId, string digest)
    {
        HumanActorBindingTransition? original = state?.HumanActorTransitions.SingleOrDefault(transition => transition.LogicalId == logicalId);
        return original is null ? null : original.IntentDigest == digest
            ? new PartyIdentityCommandResult([], original.OriginalEvidence is null ? original : original.OriginalEvidence)
            : BindingRejected("intent-conflict");
    }

    private static string Digest(object command)
    {
        JsonNode? node = JsonSerializer.SerializeToNode(command, command.GetType(), PartiesJsonOptions.Default);
        if (node is JsonObject root)
        {
            _ = root.Remove("operatorProof");
        }

        return IdentityAdmissionProof.Digest(JsonSerializer.SerializeToUtf8Bytes(node, PartiesJsonOptions.Default));
    }

    private static DomainResult ProvisionRejected(string reason)
        => DomainResult.Rejection([new AgentPartyProvisioningRejected(reason)]);

    private static DomainResult BindingRejected(string reason)
        => DomainResult.Rejection([new HumanActorBindingRejected(reason)]);
}
