using System.Reflection;
using System.Text.Json.Serialization;

using Hexalith.EventStore.Contracts.Security;

using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Events.Rejections;
using Hexalith.Parties.Contracts.Queries;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.State;
using Hexalith.Parties.Contracts.Results;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.Parties.Contracts.ValueObjects;

using Shouldly;

namespace Hexalith.Parties.Contracts.Tests.Privacy;

public sealed class PersonalDataInventoryTests
{
    private static readonly IReadOnlyDictionary<string, Classification> s_expectedClassifications =
        new Dictionary<string, Classification>(StringComparer.Ordinal)
        {
            [Key(typeof(PersonDetails), nameof(PersonDetails.FirstName))] = Classification.PersonalData,
            [Key(typeof(PersonDetails), nameof(PersonDetails.LastName))] = Classification.PersonalData,
            [Key(typeof(PersonDetails), nameof(PersonDetails.DateOfBirth))] = Classification.PersonalData,
            [Key(typeof(PersonDetails), nameof(PersonDetails.Prefix))] = Classification.PersonalData,
            [Key(typeof(PersonDetails), nameof(PersonDetails.Suffix))] = Classification.PersonalData,

            [Key(typeof(OrganizationDetails), nameof(OrganizationDetails.LegalName))] = Classification.NonPersonalByDefault,
            [Key(typeof(OrganizationDetails), nameof(OrganizationDetails.TradingName))] = Classification.NonPersonalByDefault,
            [Key(typeof(OrganizationDetails), nameof(OrganizationDetails.LegalForm))] = Classification.NonPersonalByDefault,
            [Key(typeof(OrganizationDetails), nameof(OrganizationDetails.RegistrationNumber))] = Classification.NonPersonalByDefault,
            [Key(typeof(OrganizationDetails), nameof(OrganizationDetails.IsNaturalPerson))] = Classification.NonPersonalByDefault,

            [Key(typeof(ContactChannel), nameof(ContactChannel.Id))] = Classification.NonPersonalMetadata,
            [Key(typeof(ContactChannel), nameof(ContactChannel.Type))] = Classification.NonPersonalMetadata,
            [Key(typeof(ContactChannel), nameof(ContactChannel.Value))] = Classification.PersonalData,
            [Key(typeof(ContactChannel), nameof(ContactChannel.IsPreferred))] = Classification.NonPersonalMetadata,
            [Key(typeof(EmailAddress), nameof(EmailAddress.Address))] = Classification.PersonalData,
            [Key(typeof(PostalAddress), nameof(PostalAddress.Street))] = Classification.PersonalData,
            [Key(typeof(PostalAddress), nameof(PostalAddress.City))] = Classification.PersonalData,
            [Key(typeof(PostalAddress), nameof(PostalAddress.Region))] = Classification.PersonalData,
            [Key(typeof(PostalAddress), nameof(PostalAddress.PostalCode))] = Classification.PersonalData,
            [Key(typeof(PostalAddress), nameof(PostalAddress.Country))] = Classification.PersonalData,
            [Key(typeof(PhoneNumber), nameof(PhoneNumber.Number))] = Classification.PersonalData,
            [Key(typeof(PhoneNumber), nameof(PhoneNumber.CountryCode))] = Classification.NonPersonalMetadata,
            [Key(typeof(SocialMediaHandle), nameof(SocialMediaHandle.Platform))] = Classification.NonPersonalMetadata,
            [Key(typeof(SocialMediaHandle), nameof(SocialMediaHandle.Handle))] = Classification.PersonalData,

            [Key(typeof(PartyIdentifier), nameof(PartyIdentifier.Id))] = Classification.NonPersonalMetadata,
            [Key(typeof(PartyIdentifier), nameof(PartyIdentifier.Type))] = Classification.NonPersonalMetadata,
            [Key(typeof(PartyIdentifier), nameof(PartyIdentifier.Value))] = Classification.PersonalData,
            [Key(typeof(PartyIdentifier), nameof(PartyIdentifier.Jurisdiction))] = Classification.NonPersonalMetadata,

            [Key(typeof(NameHistoryEntry), nameof(NameHistoryEntry.DisplayName))] = Classification.PersonalData,
            [Key(typeof(NameHistoryEntry), nameof(NameHistoryEntry.SortName))] = Classification.PersonalData,
            [Key(typeof(NameHistoryEntry), nameof(NameHistoryEntry.ChangedAt))] = Classification.NonPersonalMetadata,
            [Key(typeof(NameHistoryEntry), nameof(NameHistoryEntry.TriggeredBy))] = Classification.NonPersonalMetadata,

            [Key(typeof(TemporalNameResult), nameof(TemporalNameResult.PartyId))] = Classification.NonPersonalMetadata,
            [Key(typeof(TemporalNameResult), nameof(TemporalNameResult.AsOf))] = Classification.NonPersonalMetadata,
            [Key(typeof(TemporalNameResult), nameof(TemporalNameResult.DisplayName))] = Classification.PersonalData,
            [Key(typeof(TemporalNameResult), nameof(TemporalNameResult.SortName))] = Classification.PersonalData,

            [Key(typeof(CreateParty), nameof(CreateParty.PartyId))] = Classification.NonPersonalMetadata,
            [Key(typeof(CreateParty), nameof(CreateParty.Type))] = Classification.NonPersonalMetadata,
            [Key(typeof(CreateParty), nameof(CreateParty.PersonDetails))] = Classification.PersonalDataContainer,
            [Key(typeof(CreateParty), nameof(CreateParty.OrganizationDetails))] = Classification.TypeDependentContainer,
            [Key(typeof(UpdatePersonDetails), nameof(UpdatePersonDetails.PartyId))] = Classification.NonPersonalMetadata,
            [Key(typeof(UpdatePersonDetails), nameof(UpdatePersonDetails.PersonDetails))] = Classification.PersonalDataContainer,
            [Key(typeof(UpdateOrganizationDetails), nameof(UpdateOrganizationDetails.PartyId))] = Classification.NonPersonalMetadata,
            [Key(typeof(UpdateOrganizationDetails), nameof(UpdateOrganizationDetails.OrganizationDetails))] = Classification.TypeDependentContainer,
            [Key(typeof(AddContactChannel), nameof(AddContactChannel.PartyId))] = Classification.NonPersonalMetadata,
            [Key(typeof(AddContactChannel), nameof(AddContactChannel.ContactChannelId))] = Classification.NonPersonalMetadata,
            [Key(typeof(AddContactChannel), nameof(AddContactChannel.Type))] = Classification.NonPersonalMetadata,
            [Key(typeof(AddContactChannel), nameof(AddContactChannel.Value))] = Classification.PersonalData,
            [Key(typeof(AddContactChannel), nameof(AddContactChannel.IsPreferred))] = Classification.NonPersonalMetadata,
            [Key(typeof(UpdateContactChannel), nameof(UpdateContactChannel.PartyId))] = Classification.NonPersonalMetadata,
            [Key(typeof(UpdateContactChannel), nameof(UpdateContactChannel.ContactChannelId))] = Classification.NonPersonalMetadata,
            [Key(typeof(UpdateContactChannel), nameof(UpdateContactChannel.Type))] = Classification.NonPersonalMetadata,
            [Key(typeof(UpdateContactChannel), nameof(UpdateContactChannel.Value))] = Classification.PersonalData,
            [Key(typeof(UpdateContactChannel), nameof(UpdateContactChannel.IsPreferred))] = Classification.NonPersonalMetadata,
            [Key(typeof(AddIdentifier), nameof(AddIdentifier.PartyId))] = Classification.NonPersonalMetadata,
            [Key(typeof(AddIdentifier), nameof(AddIdentifier.IdentifierId))] = Classification.NonPersonalMetadata,
            [Key(typeof(AddIdentifier), nameof(AddIdentifier.Type))] = Classification.NonPersonalMetadata,
            [Key(typeof(AddIdentifier), nameof(AddIdentifier.Value))] = Classification.PersonalData,

            [Key(typeof(PartyCreated), nameof(PartyCreated.Type))] = Classification.NonPersonalMetadata,
            [Key(typeof(PartyCreated), nameof(PartyCreated.PersonDetails))] = Classification.PersonalDataContainer,
            [Key(typeof(PartyCreated), nameof(PartyCreated.OrganizationDetails))] = Classification.TypeDependentContainer,
            [Key(typeof(PartyCreated), nameof(PartyCreated.CreatedAt))] = Classification.NonPersonalMetadata,
            [Key(typeof(PersonDetailsUpdated), nameof(PersonDetailsUpdated.PersonDetails))] = Classification.PersonalDataContainer,
            [Key(typeof(OrganizationDetailsUpdated), nameof(OrganizationDetailsUpdated.OrganizationDetails))] = Classification.TypeDependentContainer,
            [Key(typeof(PartyDisplayNameDerived), nameof(PartyDisplayNameDerived.DisplayName))] = Classification.PersonalData,
            [Key(typeof(PartyDisplayNameDerived), nameof(PartyDisplayNameDerived.SortName))] = Classification.PersonalData,
            [Key(typeof(ContactChannelAdded), nameof(ContactChannelAdded.ContactChannelId))] = Classification.NonPersonalMetadata,
            [Key(typeof(ContactChannelAdded), nameof(ContactChannelAdded.Type))] = Classification.NonPersonalMetadata,
            [Key(typeof(ContactChannelAdded), nameof(ContactChannelAdded.Value))] = Classification.PersonalData,
            [Key(typeof(ContactChannelAdded), nameof(ContactChannelAdded.IsPreferred))] = Classification.NonPersonalMetadata,
            [Key(typeof(ContactChannelUpdated), nameof(ContactChannelUpdated.ContactChannelId))] = Classification.NonPersonalMetadata,
            [Key(typeof(ContactChannelUpdated), nameof(ContactChannelUpdated.Type))] = Classification.NonPersonalMetadata,
            [Key(typeof(ContactChannelUpdated), nameof(ContactChannelUpdated.Value))] = Classification.PersonalData,
            [Key(typeof(ContactChannelUpdated), nameof(ContactChannelUpdated.IsPreferred))] = Classification.NonPersonalMetadata,
            [Key(typeof(IdentifierAdded), nameof(IdentifierAdded.IdentifierId))] = Classification.NonPersonalMetadata,
            [Key(typeof(IdentifierAdded), nameof(IdentifierAdded.Type))] = Classification.NonPersonalMetadata,
            [Key(typeof(IdentifierAdded), nameof(IdentifierAdded.Value))] = Classification.PersonalData,

            [Key(typeof(PartyState), nameof(PartyState.Type))] = Classification.NonPersonalMetadata,
            [Key(typeof(PartyState), nameof(PartyState.IsActive))] = Classification.NonPersonalMetadata,
            [Key(typeof(PartyState), nameof(PartyState.IsNaturalPerson))] = Classification.NonPersonalMetadata,
            [Key(typeof(PartyState), nameof(PartyState.DisplayName))] = Classification.PersonalData,
            [Key(typeof(PartyState), nameof(PartyState.SortName))] = Classification.PersonalData,
            [Key(typeof(PartyState), nameof(PartyState.Person))] = Classification.PersonalDataContainer,
            [Key(typeof(PartyState), nameof(PartyState.Organization))] = Classification.TypeDependentContainer,
            [Key(typeof(PartyState), nameof(PartyState.ContactChannels))] = Classification.PersonalDataContainer,
            [Key(typeof(PartyState), nameof(PartyState.Identifiers))] = Classification.PersonalDataContainer,
            [Key(typeof(PartyState), nameof(PartyState.ConsentRecords))] = Classification.NonPersonalMetadata,
            [Key(typeof(PartyState), nameof(PartyState.CreatedAt))] = Classification.NonPersonalMetadata,
            [Key(typeof(PartyState), nameof(PartyState.IsRestricted))] = Classification.NonPersonalMetadata,
            [Key(typeof(PartyState), nameof(PartyState.RestrictedAt))] = Classification.NonPersonalMetadata,
            [Key(typeof(PartyState), nameof(PartyState.RestrictionReason))] = Classification.DeferredPrivacyDesign,
            [Key(typeof(PartyState), nameof(PartyState.ErasureStatus))] = Classification.NonPersonalMetadata,
            [Key(typeof(PartyState), nameof(PartyState.ErasedAt))] = Classification.NonPersonalMetadata,
            [Key(typeof(PartyState), nameof(PartyState.HasBeenCreated))] = Classification.NonPersonalMetadata,
            [Key(typeof(PartyState), nameof(PartyState.HumanBindingVersion))] = Classification.NonPersonalMetadata,
            [Key(typeof(PartyState), nameof(PartyState.AgentProvisioning))] = Classification.PersonalData,

            // Actor and provenance history is personal data under independent finite custody.
            // Profile-key annotations would incorrectly couple this history to profile erasure.
            [Key(typeof(PartyState), nameof(PartyState.HumanActorBindings))] = Classification.IdentityHistoryPersonalData,
            [Key(typeof(PartyState), nameof(PartyState.HumanActorTransitions))] = Classification.IdentityHistoryPersonalData,

            [Key(typeof(PartyDetail), nameof(PartyDetail.Id))] = Classification.NonPersonalMetadata,
            [Key(typeof(PartyDetail), nameof(PartyDetail.Type))] = Classification.NonPersonalMetadata,
            [Key(typeof(PartyDetail), nameof(PartyDetail.IsActive))] = Classification.NonPersonalMetadata,
            [Key(typeof(PartyDetail), nameof(PartyDetail.DisplayName))] = Classification.PersonalData,
            [Key(typeof(PartyDetail), nameof(PartyDetail.SortName))] = Classification.PersonalData,
            [Key(typeof(PartyDetail), nameof(PartyDetail.PersonDetails))] = Classification.PersonalDataContainer,
            [Key(typeof(PartyDetail), nameof(PartyDetail.OrganizationDetails))] = Classification.TypeDependentContainer,
            [Key(typeof(PartyDetail), nameof(PartyDetail.ContactChannels))] = Classification.PersonalDataContainer,
            [Key(typeof(PartyDetail), nameof(PartyDetail.Identifiers))] = Classification.PersonalDataContainer,
            [Key(typeof(PartyDetail), nameof(PartyDetail.ConsentRecords))] = Classification.NonPersonalMetadata,
            [Key(typeof(PartyDetail), nameof(PartyDetail.NameHistory))] = Classification.PersonalData,
            [Key(typeof(PartyDetail), nameof(PartyDetail.CreatedAt))] = Classification.NonPersonalMetadata,
            [Key(typeof(PartyDetail), nameof(PartyDetail.LastModifiedAt))] = Classification.NonPersonalMetadata,
            [Key(typeof(PartyDetail), nameof(PartyDetail.IsRestricted))] = Classification.NonPersonalMetadata,
            [Key(typeof(PartyDetail), nameof(PartyDetail.RestrictedAt))] = Classification.NonPersonalMetadata,
            [Key(typeof(PartyDetail), nameof(PartyDetail.IsErased))] = Classification.NonPersonalMetadata,
            [Key(typeof(PartyDetail), nameof(PartyDetail.ErasedAt))] = Classification.NonPersonalMetadata,
            [Key(typeof(PartyDetail), nameof(PartyDetail.Freshness))] = Classification.NonPersonalMetadata,

            [Key(typeof(PartyIndexEntry), nameof(PartyIndexEntry.Id))] = Classification.NonPersonalMetadata,
            [Key(typeof(PartyIndexEntry), nameof(PartyIndexEntry.Type))] = Classification.NonPersonalMetadata,
            [Key(typeof(PartyIndexEntry), nameof(PartyIndexEntry.IsActive))] = Classification.NonPersonalMetadata,
            [Key(typeof(PartyIndexEntry), nameof(PartyIndexEntry.DisplayName))] = Classification.PersonalData,
            [Key(typeof(PartyIndexEntry), nameof(PartyIndexEntry.SortName))] = Classification.PersonalData,
            [Key(typeof(PartyIndexEntry), nameof(PartyIndexEntry.SearchableContactChannels))] = Classification.PersonalDataContainer,
            [Key(typeof(PartyIndexEntry), nameof(PartyIndexEntry.SearchableIdentifiers))] = Classification.PersonalDataContainer,
            [Key(typeof(PartyIndexEntry), nameof(PartyIndexEntry.CreatedAt))] = Classification.NonPersonalMetadata,
            [Key(typeof(PartyIndexEntry), nameof(PartyIndexEntry.LastModifiedAt))] = Classification.NonPersonalMetadata,
            [Key(typeof(PartyIndexEntry), nameof(PartyIndexEntry.IsErased))] = Classification.NonPersonalMetadata,
        };

    // Identity classification is contextual. Opaque IDs, fingerprints and timestamps can
    // identify relationships; this inventory never changes wire or encryption annotations.
    private static readonly IReadOnlyDictionary<string, (Classification Classification, string Rationale)> s_identityClassifications =
        new Dictionary<string, (Classification, string)>(StringComparer.Ordinal)
        {
            [Key(typeof(PartyIdentityCommandResult), nameof(PartyIdentityCommandResult.ResultPayload))] = (Classification.SensitiveIdentityResponse, "Serialized binding or provisioning results contain identifying opaque relationships and original retry evidence; this response is not a profile-key annotation boundary."),
            [Key(typeof(PartyIdentityCommandResult), nameof(PartyIdentityCommandResult.PostCommandStateJson))] = (Classification.SensitiveIdentityResponse, "Inherited optional folded-state JSON can contain profile, actor linkage and provenance in this identity command result; absence today does not make the slot non-personal."),
            [Key(typeof(PartyIdentityCommandResult), nameof(PartyIdentityCommandResult.Events))] = (Classification.SensitiveIdentityResponse, "Inherited event payloads in this concrete result carry purpose-custodied binding history or profile-protected provisioning markers; individual payload protection remains unchanged."),
            [Key(typeof(PartyIdentityCommandResult), nameof(PartyIdentityCommandResult.IsSuccess))] = (Classification.IdentityProtocolMetadata, "Inherited success flag classifies the concrete command result without carrying an identifier by itself."),
            [Key(typeof(PartyIdentityCommandResult), nameof(PartyIdentityCommandResult.IsRejection))] = (Classification.IdentityProtocolMetadata, "Inherited rejection flag contains no independent actor or Party identity."),
            [Key(typeof(PartyIdentityCommandResult), nameof(PartyIdentityCommandResult.IsNoOp))] = (Classification.IdentityProtocolMetadata, "Inherited no-op flag describes result processing without independent identifying content."),
            [Key(typeof(EstablishHumanActorBinding), nameof(EstablishHumanActorBinding.TenantId))] = (Classification.SensitiveIdentityInput, "Tenant scope links this request to the target identity."),
            [Key(typeof(EstablishHumanActorBinding), nameof(EstablishHumanActorBinding.PartyId))] = (Classification.SensitiveIdentityInput, "Opaque Party target can identify a person through its actor relationship."),
            [Key(typeof(EstablishHumanActorBinding), nameof(EstablishHumanActorBinding.ActorId))] = (Classification.SensitiveIdentityInput, "Opaque actor identity is personal linkage even without a login name."),
            [Key(typeof(EstablishHumanActorBinding), nameof(EstablishHumanActorBinding.ActorRevision))] = (Classification.SensitiveIdentityInput, "Registry revision is associated with the target actor."),
            [Key(typeof(EstablishHumanActorBinding), nameof(EstablishHumanActorBinding.ExpectedPartyRevision))] = (Classification.SensitiveIdentityInput, "Expected checkpoint links the intent to a particular Party history."),
            [Key(typeof(EstablishHumanActorBinding), nameof(EstablishHumanActorBinding.ExpectedBindingVersion))] = (Classification.SensitiveIdentityInput, "Expected version identifies a particular attribution relationship."),
            [Key(typeof(EstablishHumanActorBinding), nameof(EstablishHumanActorBinding.EffectiveAt))] = (Classification.SensitiveIdentityInput, "Action boundary is personal attribution timing in this request."),
            [Key(typeof(EstablishHumanActorBinding), nameof(EstablishHumanActorBinding.LogicalId))] = (Classification.SensitiveIdentityInput, "Stable retry identity links repeated attribution operations."),
            [Key(typeof(EstablishHumanActorBinding), nameof(EstablishHumanActorBinding.PolicyId))] = (Classification.SensitiveIdentityInput, "Policy reference describes custody requested for this attribution."),
            [Key(typeof(EstablishHumanActorBinding), nameof(EstablishHumanActorBinding.OperatorProof))] = (Classification.TransientAuthorization, "Opaque operator proof is confidential transient authorization, never a profile field."),
            [Key(typeof(EstablishHumanActorBinding), nameof(EstablishHumanActorBinding.Authorization))] = (Classification.TransientAuthorization, "Processor-only verified authorization is excluded from public command JSON."),
            [Key(typeof(RebindHumanActorBinding), nameof(RebindHumanActorBinding.TenantId))] = (Classification.SensitiveIdentityInput, "Tenant scope links this request to the target identity."),
            [Key(typeof(RebindHumanActorBinding), nameof(RebindHumanActorBinding.PartyId))] = (Classification.SensitiveIdentityInput, "Opaque Party target can identify a person through its actor relationship."),
            [Key(typeof(RebindHumanActorBinding), nameof(RebindHumanActorBinding.ActorId))] = (Classification.SensitiveIdentityInput, "Opaque actor identity is personal linkage even without a login name."),
            [Key(typeof(RebindHumanActorBinding), nameof(RebindHumanActorBinding.ActorRevision))] = (Classification.SensitiveIdentityInput, "Registry revision is associated with the target actor."),
            [Key(typeof(RebindHumanActorBinding), nameof(RebindHumanActorBinding.ExpectedPartyRevision))] = (Classification.SensitiveIdentityInput, "Expected checkpoint links the intent to a particular Party history."),
            [Key(typeof(RebindHumanActorBinding), nameof(RebindHumanActorBinding.ExpectedBindingVersion))] = (Classification.SensitiveIdentityInput, "Expected version identifies a particular attribution relationship."),
            [Key(typeof(RebindHumanActorBinding), nameof(RebindHumanActorBinding.EffectiveAt))] = (Classification.SensitiveIdentityInput, "Action boundary is personal attribution timing in this request."),
            [Key(typeof(RebindHumanActorBinding), nameof(RebindHumanActorBinding.LogicalId))] = (Classification.SensitiveIdentityInput, "Stable retry identity links repeated attribution operations."),
            [Key(typeof(RebindHumanActorBinding), nameof(RebindHumanActorBinding.PolicyId))] = (Classification.SensitiveIdentityInput, "Policy reference describes custody requested for this attribution."),
            [Key(typeof(RebindHumanActorBinding), nameof(RebindHumanActorBinding.OperatorProof))] = (Classification.TransientAuthorization, "Opaque operator proof is confidential transient authorization, never a profile field."),
            [Key(typeof(RebindHumanActorBinding), nameof(RebindHumanActorBinding.Authorization))] = (Classification.TransientAuthorization, "Processor-only verified authorization is excluded from public command JSON."),
            [Key(typeof(RevokeHumanActorBinding), nameof(RevokeHumanActorBinding.TenantId))] = (Classification.SensitiveIdentityInput, "Tenant scope links this request to the target identity."),
            [Key(typeof(RevokeHumanActorBinding), nameof(RevokeHumanActorBinding.PartyId))] = (Classification.SensitiveIdentityInput, "Opaque Party target can identify a person through its actor relationship."),
            [Key(typeof(RevokeHumanActorBinding), nameof(RevokeHumanActorBinding.ActorId))] = (Classification.SensitiveIdentityInput, "Opaque actor identity is personal linkage even without a login name."),
            [Key(typeof(RevokeHumanActorBinding), nameof(RevokeHumanActorBinding.ActorRevision))] = (Classification.SensitiveIdentityInput, "Registry revision is associated with the target actor."),
            [Key(typeof(RevokeHumanActorBinding), nameof(RevokeHumanActorBinding.ExpectedPartyRevision))] = (Classification.SensitiveIdentityInput, "Expected checkpoint links the intent to a particular Party history."),
            [Key(typeof(RevokeHumanActorBinding), nameof(RevokeHumanActorBinding.ExpectedBindingVersion))] = (Classification.SensitiveIdentityInput, "Expected version identifies a particular attribution relationship."),
            [Key(typeof(RevokeHumanActorBinding), nameof(RevokeHumanActorBinding.EffectiveAt))] = (Classification.SensitiveIdentityInput, "Action boundary is personal attribution timing in this request."),
            [Key(typeof(RevokeHumanActorBinding), nameof(RevokeHumanActorBinding.LogicalId))] = (Classification.SensitiveIdentityInput, "Stable retry identity links repeated attribution operations."),
            [Key(typeof(RevokeHumanActorBinding), nameof(RevokeHumanActorBinding.PolicyId))] = (Classification.SensitiveIdentityInput, "Policy reference describes custody requested for this attribution."),
            [Key(typeof(RevokeHumanActorBinding), nameof(RevokeHumanActorBinding.OperatorProof))] = (Classification.TransientAuthorization, "Opaque operator proof is confidential transient authorization, never a profile field."),
            [Key(typeof(RevokeHumanActorBinding), nameof(RevokeHumanActorBinding.Authorization))] = (Classification.TransientAuthorization, "Processor-only verified authorization is excluded from public command JSON."),
            [Key(typeof(ProvisionAgentParty), nameof(ProvisionAgentParty.Identity))] = (Classification.ProfileProtectedProvisioning, "Nested agent-to-Party mapping is sensitive provenance; its persisted marker uses profile protection."),
            [Key(typeof(ProvisionAgentParty), nameof(ProvisionAgentParty.LogicalId))] = (Classification.ProfileProtectedProvisioning, "Computed retry identifier links the provisioning intent and resulting Party."),
            [Key(typeof(ProvisionAgentParty), nameof(ProvisionAgentParty.Authorization))] = (Classification.TransientAuthorization, "Processor-only authorization is transient and excluded from public command JSON."),
            [Key(typeof(ResolvePartyIdentity), nameof(ResolvePartyIdentity.TenantId))] = (Classification.SensitiveIdentityInput, "Tenant scope links a transient lookup to the target identity."),
            [Key(typeof(ResolvePartyIdentity), nameof(ResolvePartyIdentity.PartyId))] = (Classification.SensitiveIdentityInput, "Opaque lookup target can identify a human through Party linkage."),
            [Key(typeof(ResolvePartyIdentity), nameof(ResolvePartyIdentity.ExpectedActorId))] = (Classification.SensitiveIdentityInput, "Expected opaque actor is sensitive linkage in an authorization lookup."),
            [Key(typeof(ResolveHumanActorBindingAt), nameof(ResolveHumanActorBindingAt.TenantId))] = (Classification.SensitiveIdentityInput, "Tenant scope links a historical lookup to the target identity."),
            [Key(typeof(ResolveHumanActorBindingAt), nameof(ResolveHumanActorBindingAt.PartyId))] = (Classification.SensitiveIdentityInput, "Opaque lookup target identifies a recorded attribution relationship."),
            [Key(typeof(ResolveHumanActorBindingAt), nameof(ResolveHumanActorBindingAt.ActionAt))] = (Classification.SensitiveIdentityInput, "Exact action timing is personal attribution context in this lookup."),
            [Key(typeof(ResolveHumanActorBindingAt), nameof(ResolveHumanActorBindingAt.ExpectedActorId))] = (Classification.SensitiveIdentityInput, "Expected opaque actor is sensitive historical linkage."),
            [Key(typeof(ResolveHumanActorBindingAt), nameof(ResolveHumanActorBindingAt.ExpectedBindingVersion))] = (Classification.SensitiveIdentityInput, "Version identifies the exact recorded attribution interval."),
            [Key(typeof(HumanActorBindingEstablished), nameof(HumanActorBindingEstablished.Binding))] = (Classification.IdentityHistoryPersonalData, "Nested actor relationship and provenance remain under independent finite history custody."),
            [Key(typeof(HumanActorBindingEstablished), nameof(HumanActorBindingEstablished.EffectiveAt))] = (Classification.IdentityHistoryPersonalData, "Persisted interval timing belongs to purpose-custodied actor history."),
            [Key(typeof(HumanActorBindingEstablished), nameof(HumanActorBindingEstablished.ExpectedBindingVersion))] = (Classification.IdentityHistoryPersonalData, "Predecessor version links purpose-custodied actor transitions."),
            [Key(typeof(HumanActorBindingEstablished), nameof(HumanActorBindingEstablished.Custody))] = (Classification.IdentityHistoryPersonalData, "Derived custody evidence controls this history independently of profile erasure."),
            [Key(typeof(HumanActorBindingRebound), nameof(HumanActorBindingRebound.Binding))] = (Classification.IdentityHistoryPersonalData, "Nested actor relationship and provenance remain under independent finite history custody."),
            [Key(typeof(HumanActorBindingRebound), nameof(HumanActorBindingRebound.EffectiveAt))] = (Classification.IdentityHistoryPersonalData, "Persisted interval timing belongs to purpose-custodied actor history."),
            [Key(typeof(HumanActorBindingRebound), nameof(HumanActorBindingRebound.ExpectedBindingVersion))] = (Classification.IdentityHistoryPersonalData, "Predecessor version links purpose-custodied actor transitions."),
            [Key(typeof(HumanActorBindingRebound), nameof(HumanActorBindingRebound.Custody))] = (Classification.IdentityHistoryPersonalData, "Derived custody evidence controls this history independently of profile erasure."),
            [Key(typeof(HumanActorBindingRevoked), nameof(HumanActorBindingRevoked.LogicalId))] = (Classification.IdentityHistoryPersonalData, "Stable revocation identity links purpose-custodied actor transitions."),
            [Key(typeof(HumanActorBindingRevoked), nameof(HumanActorBindingRevoked.IntentDigest))] = (Classification.IdentityHistoryPersonalData, "Digest is linkable intent provenance, not anonymous merely because it is hashed."),
            [Key(typeof(HumanActorBindingRevoked), nameof(HumanActorBindingRevoked.ExpectedBindingVersion))] = (Classification.IdentityHistoryPersonalData, "Version identifies the purpose-custodied relationship being revoked."),
            [Key(typeof(HumanActorBindingRevoked), nameof(HumanActorBindingRevoked.EffectiveAt))] = (Classification.IdentityHistoryPersonalData, "Revocation timing is part of personal actor history."),
            [Key(typeof(HumanActorBindingRevoked), nameof(HumanActorBindingRevoked.Custody))] = (Classification.IdentityHistoryPersonalData, "Custody controls independent finite retention of the revocation."),
            [Key(typeof(HumanActorBindingEvidence), nameof(HumanActorBindingEvidence.TenantId))] = (Classification.IdentityHistoryPersonalData, "Tenant scope links a recorded actor-to-Party relationship."),
            [Key(typeof(HumanActorBindingEvidence), nameof(HumanActorBindingEvidence.PartyId))] = (Classification.IdentityHistoryPersonalData, "Opaque Party identity is personal linkage in recorded attribution."),
            [Key(typeof(HumanActorBindingEvidence), nameof(HumanActorBindingEvidence.ActorId))] = (Classification.IdentityHistoryPersonalData, "Opaque actor ULID is personal linkage in recorded attribution."),
            [Key(typeof(HumanActorBindingEvidence), nameof(HumanActorBindingEvidence.BindingVersion))] = (Classification.IdentityHistoryPersonalData, "Version identifies this retained personal relationship."),
            [Key(typeof(HumanActorBindingEvidence), nameof(HumanActorBindingEvidence.ActorRevision))] = (Classification.IdentityHistoryPersonalData, "Registry capability revision is associated with the recorded actor."),
            [Key(typeof(HumanActorBindingEvidence), nameof(HumanActorBindingEvidence.ValidFrom))] = (Classification.IdentityHistoryPersonalData, "Interval start records personal attribution timing."),
            [Key(typeof(HumanActorBindingEvidence), nameof(HumanActorBindingEvidence.ValidUntil))] = (Classification.IdentityHistoryPersonalData, "Interval end records personal attribution timing."),
            [Key(typeof(HumanActorBindingEvidence), nameof(HumanActorBindingEvidence.SourceId))] = (Classification.IdentityHistoryPersonalData, "Trusted writer reference is linkable attribution provenance."),
            [Key(typeof(HumanActorBindingEvidence), nameof(HumanActorBindingEvidence.ProvenanceId))] = (Classification.IdentityHistoryPersonalData, "Opaque operator reference can identify the actor behind the transition."),
            [Key(typeof(HumanActorBindingEvidence), nameof(HumanActorBindingEvidence.Custody))] = (Classification.IdentityHistoryPersonalData, "Nested provider evidence controls independent finite attribution custody."),
            [Key(typeof(HumanActorBinding), nameof(HumanActorBinding.Evidence))] = (Classification.IdentityHistoryPersonalData, "Nested interval is purpose-custodied personal actor history."),
            [Key(typeof(HumanActorBinding), nameof(HumanActorBinding.LogicalId))] = (Classification.IdentityHistoryPersonalData, "Retry identity links repeated processing of the same actor transition."),
            [Key(typeof(HumanActorBinding), nameof(HumanActorBinding.IntentDigest))] = (Classification.IdentityHistoryPersonalData, "Linkable intent digest remains sensitive in actor history."),
            [Key(typeof(HumanActorBindingTransition), nameof(HumanActorBindingTransition.LogicalId))] = (Classification.IdentityHistoryPersonalData, "Logical identity links the original actor transition and retries."),
            [Key(typeof(HumanActorBindingTransition), nameof(HumanActorBindingTransition.IntentDigest))] = (Classification.IdentityHistoryPersonalData, "Immutable digest links the actor intent to the original result."),
            [Key(typeof(HumanActorBindingTransition), nameof(HumanActorBindingTransition.Version))] = (Classification.IdentityHistoryPersonalData, "Revision identifies the personal actor transition."),
            [Key(typeof(HumanActorBindingTransition), nameof(HumanActorBindingTransition.OriginalEvidence))] = (Classification.IdentityHistoryPersonalData, "Original interval remains purpose-custodied even after a later rebind."),
            [Key(typeof(AgentPartyProvisioned), nameof(AgentPartyProvisioned.Result))] = (Classification.ProfileProtectedContainer, "Persisted provenance marker is already profile-key protected at this root property."),
            [Key(typeof(AgentPartyProvisioningResult), nameof(AgentPartyProvisioningResult.Identity))] = (Classification.ProfileProtectedProvisioning, "Nested agent-to-Party identifiers are protected by the containing persisted marker."),
            [Key(typeof(AgentPartyProvisioningResult), nameof(AgentPartyProvisioningResult.ProvisioningRevision))] = (Classification.ProfileProtectedProvisioning, "Source checkpoint links the protected original provisioning marker."),
            [Key(typeof(AgentPartyProvisioningResult), nameof(AgentPartyProvisioningResult.SourceId))] = (Classification.ProfileProtectedProvisioning, "Original writer reference is linkable provisioning provenance."),
            [Key(typeof(AgentPartyIdentity), nameof(AgentPartyIdentity.TenantId))] = (Classification.ProfileProtectedProvisioning, "Tenant scopes the protected agent-to-Party mapping."),
            [Key(typeof(AgentPartyIdentity), nameof(AgentPartyIdentity.AgentId))] = (Classification.ProfileProtectedProvisioning, "Opaque Agent ULID is sensitive linkage in provisioning context."),
            [Key(typeof(AgentPartyIdentity), nameof(AgentPartyIdentity.PartyId))] = (Classification.ProfileProtectedProvisioning, "Mapped opaque Party ULID links agent and Party records."),
            [Key(typeof(AgentPartyIdentity), nameof(AgentPartyIdentity.LogicalId))] = (Classification.ProfileProtectedProvisioning, "Stable retry identity links provisioning requests and results."),
            [Key(typeof(AgentPartyIdentity), nameof(AgentPartyIdentity.CreationFingerprint))] = (Classification.ProfileProtectedProvisioning, "Intent digest is linkable provenance, not anonymous by hashing."),
            [Key(typeof(AgentPartyIdentity), nameof(AgentPartyIdentity.CreatedAt))] = (Classification.ProfileProtectedProvisioning, "Creation timing is associated with the protected provisioning identity."),
            [Key(typeof(AgentPartyIdentity), nameof(AgentPartyIdentity.ContractVersion))] = (Classification.IdentityProtocolMetadata, "Mapping schema version has no independent identifying content."),
            [Key(typeof(IdentityCommandAuthorization), nameof(IdentityCommandAuthorization.Admission))] = (Classification.TransientAuthorization, "Verified scope and actor evidence authorize one operation; never profile state."),
            [Key(typeof(IdentityCommandAuthorization), nameof(IdentityCommandAuthorization.SourcePosition))] = (Classification.TransientAuthorization, "Processing checkpoint binds transient authorization to source state."),
            [Key(typeof(IdentityCommandAuthorization), nameof(IdentityCommandAuthorization.Policy))] = (Classification.TransientAuthorization, "Approved finite policy is consulted transiently before a binding write."),
            [Key(typeof(IdentityCommandAuthorization), nameof(IdentityCommandAuthorization.Custody))] = (Classification.TransientAuthorization, "Provider admission is transient authorization for independent history retention."),
            [Key(typeof(IdentityAdmissionEvidence), nameof(IdentityAdmissionEvidence.Scope))] = (Classification.TransientAuthorization, "Authenticated exact-operation scope is confidential transient authorization."),
            [Key(typeof(IdentityAdmissionEvidence), nameof(IdentityAdmissionEvidence.SourceId))] = (Classification.TransientAuthorization, "Verified workload reference belongs to transient authorization provenance."),
            [Key(typeof(IdentityAdmissionEvidence), nameof(IdentityAdmissionEvidence.OperatorActorId))] = (Classification.TransientAuthorization, "Opaque operator is sensitive authorization linkage, never a profile field."),
            [Key(typeof(IdentityAdmissionEvidence), nameof(IdentityAdmissionEvidence.TargetActorId))] = (Classification.TransientAuthorization, "Opaque target actor is sensitive authorization linkage."),
            [Key(typeof(IdentityAdmissionEvidence), nameof(IdentityAdmissionEvidence.ActorRevision))] = (Classification.TransientAuthorization, "Capability revision is associated with the admitted target actor."),
            [Key(typeof(IdentityAdmissionEvidence), nameof(IdentityAdmissionEvidence.ActorActive))] = (Classification.TransientAuthorization, "Actor activity is transient authorization state for this observation."),
            [Key(typeof(IdentityAdmissionEvidence), nameof(IdentityAdmissionEvidence.IssuedAt))] = (Classification.TransientAuthorization, "Proof issuance timing is confidential authorization context."),
            [Key(typeof(IdentityAdmissionEvidence), nameof(IdentityAdmissionEvidence.ExpiresAt))] = (Classification.TransientAuthorization, "Proof expiry bounds transient authorization, not history retention."),
            [Key(typeof(IdentityAdmissionEvidence), nameof(IdentityAdmissionEvidence.AuthorityRevision))] = (Classification.TransientAuthorization, "Trust-policy revision describes this transient admission."),
            [Key(typeof(IdentityAdmissionScope), nameof(IdentityAdmissionScope.TenantId))] = (Classification.TransientAuthorization, "Tenant identity scopes the authenticated operation."),
            [Key(typeof(IdentityAdmissionScope), nameof(IdentityAdmissionScope.Domain))] = (Classification.TransientAuthorization, "Domain constrains transient authorization scope."),
            [Key(typeof(IdentityAdmissionScope), nameof(IdentityAdmissionScope.AggregateId))] = (Classification.TransientAuthorization, "Exact opaque target is sensitive linkage in authenticated scope."),
            [Key(typeof(IdentityAdmissionScope), nameof(IdentityAdmissionScope.Operation))] = (Classification.TransientAuthorization, "Operation name constrains the authorized action."),
            [Key(typeof(IdentityAdmissionScope), nameof(IdentityAdmissionScope.MessageId))] = (Classification.TransientAuthorization, "Delivery or correlation identity links the authorization and request."),
            [Key(typeof(IdentityAdmissionScope), nameof(IdentityAdmissionScope.LogicalId))] = (Classification.TransientAuthorization, "Logical intent identity links authorized retries."),
            [Key(typeof(IdentityAdmissionScope), nameof(IdentityAdmissionScope.PayloadDigest))] = (Classification.TransientAuthorization, "Digest binds the exact payload and remains linkable authorization evidence."),
            [Key(typeof(IdentityHistoryCustodyEvidence), nameof(IdentityHistoryCustodyEvidence.PolicyId))] = (Classification.IdentityCustodyMetadata, "Approved policy reference controls finite purpose custody."),
            [Key(typeof(IdentityHistoryCustodyEvidence), nameof(IdentityHistoryCustodyEvidence.Purpose))] = (Classification.IdentityCustodyMetadata, "Purpose separates identity history from profile encryption."),
            [Key(typeof(IdentityHistoryCustodyEvidence), nameof(IdentityHistoryCustodyEvidence.ExpiresAt))] = (Classification.IdentityCustodyMetadata, "Irreversible expiry is associated with the retained identity interval."),
            [Key(typeof(IdentityHistoryCustodyEvidence), nameof(IdentityHistoryCustodyEvidence.LifecycleRevision))] = (Classification.IdentityCustodyMetadata, "Anti-resurrection revision authenticates custody lifecycle."),
            [Key(typeof(IdentityHistoryCustodyEvidence), nameof(IdentityHistoryCustodyEvidence.EvidenceId))] = (Classification.IdentityCustodyMetadata, "Opaque provider receipt links retained history and is sensitive in context."),
            [Key(typeof(IdentityHistoryCustodyEvidence), nameof(IdentityHistoryCustodyEvidence.SourceExpiryEnforced))] = (Classification.IdentityCustodyMetadata, "Lifecycle assurance covers events, snapshots and backups."),
            [Key(typeof(IdentityHistoryCustodyEvidence), nameof(IdentityHistoryCustodyEvidence.RestoreSafe))] = (Classification.IdentityCustodyMetadata, "Lifecycle assurance forbids resurrection through rollback or restore."),
            [Key(typeof(IdentityHistoryCustodyEvidence), nameof(IdentityHistoryCustodyEvidence.DerivedCopiesCovered))] = (Classification.IdentityCustodyMetadata, "Lifecycle assurance covers derived identity copies."),
            [Key(typeof(IdentityHistoryPolicy), nameof(IdentityHistoryPolicy.PolicyId))] = (Classification.IdentityCustodyMetadata, "Configured policy reference defines independent history custody."),
            [Key(typeof(IdentityHistoryPolicy), nameof(IdentityHistoryPolicy.Retention))] = (Classification.IdentityCustodyMetadata, "Finite duration is policy configuration, not an individual profile field."),
            [Key(typeof(IdentityHistoryPolicy), nameof(IdentityHistoryPolicy.ExpiryTrigger))] = (Classification.IdentityCustodyMetadata, "Configured trigger derives irreversible history expiry."),
            [Key(typeof(IdentityHistoryPolicy), nameof(IdentityHistoryPolicy.IsValid))] = (Classification.IdentityCustodyMetadata, "Computed policy-validity flag contains no profile content."),
            [Key(typeof(PartyIdentityEvidence), nameof(PartyIdentityEvidence.TenantId))] = (Classification.SensitiveIdentityResponse, "Tenant scope identifies the authorized Party observation."),
            [Key(typeof(PartyIdentityEvidence), nameof(PartyIdentityEvidence.PartyId))] = (Classification.SensitiveIdentityResponse, "Opaque Party target is linkable personal identity context."),
            [Key(typeof(PartyIdentityEvidence), nameof(PartyIdentityEvidence.Classification))] = (Classification.SensitiveIdentityResponse, "Human or organization classification is sensitive in an identified response."),
            [Key(typeof(PartyIdentityEvidence), nameof(PartyIdentityEvidence.IsActive))] = (Classification.SensitiveIdentityResponse, "Current active state is associated with the identified Party."),
            [Key(typeof(PartyIdentityEvidence), nameof(PartyIdentityEvidence.IsRestricted))] = (Classification.SensitiveIdentityResponse, "Processing restriction is associated with the identified Party."),
            [Key(typeof(PartyIdentityEvidence), nameof(PartyIdentityEvidence.IsErasingOrErased))] = (Classification.SensitiveIdentityResponse, "Erasure state is associated with the identified Party."),
            [Key(typeof(PartyIdentityEvidence), nameof(PartyIdentityEvidence.SourcePosition))] = (Classification.SensitiveIdentityResponse, "Exact checkpoint links the response to Party history."),
            [Key(typeof(PartyIdentityEvidence), nameof(PartyIdentityEvidence.ObservedAt))] = (Classification.SensitiveIdentityResponse, "Observation timing links identity evidence to its source state."),
            [Key(typeof(PartyIdentityEvidence), nameof(PartyIdentityEvidence.ObservationId))] = (Classification.SensitiveIdentityResponse, "Opaque observation reference links source and identity evidence."),
            [Key(typeof(PartyIdentityEvidence), nameof(PartyIdentityEvidence.HumanBinding))] = (Classification.SensitiveIdentityResponse, "Nested personal actor history keeps independent finite custody."),
            [Key(typeof(PartyIdentityEvidence), nameof(PartyIdentityEvidence.ContractVersion))] = (Classification.IdentityProtocolMetadata, "Evidence schema version has no independent identifying content."),
            [Key(typeof(PartyIdentityResult), nameof(PartyIdentityResult.Outcome))] = (Classification.IdentityProtocolMetadata, "Bounded outcome classification contains no identifier by itself."),
            [Key(typeof(PartyIdentityResult), nameof(PartyIdentityResult.Evidence))] = (Classification.SensitiveIdentityResponse, "Authorized response contains linkable source and actor evidence, without profile details."),
            [Key(typeof(HumanActorBindingResult), nameof(HumanActorBindingResult.Outcome))] = (Classification.IdentityProtocolMetadata, "Bounded historical outcome contains no independent identifying content."),
            [Key(typeof(HumanActorBindingResult), nameof(HumanActorBindingResult.ContractVersion))] = (Classification.IdentityProtocolMetadata, "Result schema version contains no independent identifying content."),
            [Key(typeof(HumanActorBindingResult), nameof(HumanActorBindingResult.TenantId))] = (Classification.SensitiveIdentityResponse, "Tenant scope links the historical response to the target identity."),
            [Key(typeof(HumanActorBindingResult), nameof(HumanActorBindingResult.PartyId))] = (Classification.SensitiveIdentityResponse, "Opaque Party target identifies the recorded relationship."),
            [Key(typeof(HumanActorBindingResult), nameof(HumanActorBindingResult.ActionAt))] = (Classification.SensitiveIdentityResponse, "Exact action timing is personal attribution context."),
            [Key(typeof(HumanActorBindingResult), nameof(HumanActorBindingResult.SourcePosition))] = (Classification.SensitiveIdentityResponse, "Checkpoint links the result to authoritative identity history."),
            [Key(typeof(HumanActorBindingResult), nameof(HumanActorBindingResult.ObservationId))] = (Classification.SensitiveIdentityResponse, "Opaque observation links the source and historical response."),
            [Key(typeof(HumanActorBindingResult), nameof(HumanActorBindingResult.Evidence))] = (Classification.SensitiveIdentityResponse, "Returned interval remains independently purpose-custodied personal history."),
            [Key(typeof(HumanActorBindingResult), nameof(HumanActorBindingResult.BindingSourcePosition))] = (Classification.SensitiveIdentityResponse, "Original event position identifies the exact recorded personal interval."),
            [Key(typeof(HumanActorBindingRejected), nameof(HumanActorBindingRejected.ReasonCode))] = (Classification.IdentityProtocolMetadata, "Bounded value-free rejection code exposes neither foreign existence nor profile content."),
            [Key(typeof(AgentPartyProvisioningRejected), nameof(AgentPartyProvisioningRejected.ReasonCode))] = (Classification.IdentityProtocolMetadata, "Bounded value-free rejection code exposes neither foreign existence nor profile content."),
        };

    private static readonly Type[] s_identityTypes =
    [
        typeof(EstablishHumanActorBinding),
        typeof(RebindHumanActorBinding),
        typeof(RevokeHumanActorBinding),
        typeof(ProvisionAgentParty),
        typeof(ResolvePartyIdentity),
        typeof(ResolveHumanActorBindingAt),
        typeof(HumanActorBindingEstablished),
        typeof(HumanActorBindingRebound),
        typeof(HumanActorBindingRevoked),
        typeof(HumanActorBindingEvidence),
        typeof(HumanActorBinding),
        typeof(HumanActorBindingTransition),
        typeof(AgentPartyProvisioned),
        typeof(AgentPartyProvisioningResult),
        typeof(AgentPartyIdentity),
        typeof(IdentityCommandAuthorization),
        typeof(IdentityAdmissionEvidence),
        typeof(IdentityAdmissionScope),
        typeof(IdentityHistoryCustodyEvidence),
        typeof(IdentityHistoryPolicy),
        typeof(PartyIdentityEvidence),
        typeof(PartyIdentityResult),
        typeof(HumanActorBindingResult),
        typeof(HumanActorBindingRejected),
        typeof(AgentPartyProvisioningRejected),
        typeof(PartyIdentityCommandResult),
    ];

    private static readonly Type[] s_inspectedTypes =
    [
        typeof(PersonDetails),
        typeof(OrganizationDetails),
        typeof(ContactChannel),
        typeof(EmailAddress),
        typeof(PostalAddress),
        typeof(PhoneNumber),
        typeof(SocialMediaHandle),
        typeof(PartyIdentifier),
        typeof(NameHistoryEntry),
        typeof(CreateParty),
        typeof(UpdatePersonDetails),
        typeof(UpdateOrganizationDetails),
        typeof(AddContactChannel),
        typeof(UpdateContactChannel),
        typeof(AddIdentifier),
        typeof(PartyCreated),
        typeof(PersonDetailsUpdated),
        typeof(OrganizationDetailsUpdated),
        typeof(PartyDisplayNameDerived),
        typeof(ContactChannelAdded),
        typeof(ContactChannelUpdated),
        typeof(IdentifierAdded),
        typeof(PartyState),
        typeof(PartyDetail),
        typeof(TemporalNameResult),
        typeof(PartyIndexEntry),
        .. s_identityTypes,
    ];

    [Fact]
    public void PersonalDataProperties_AreMarked()
    {
        string[] missing = s_expectedClassifications
            .Where(kvp => kvp.Value is Classification.PersonalData)
            .Select(kvp => ResolveProperty(kvp.Key))
            .Where(property => !property.IsDefined(typeof(PersonalDataAttribute), inherit: true))
            .Select(property => $"{property.DeclaringType!.Name}.{property.Name}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        missing.ShouldBeEmpty("Required personal-data properties must be marked by type and property name.");
    }

    [Fact]
    public void PersonalDataContainers_AreDiscoverableThroughNestedTypes()
    {
        string[] missing = s_expectedClassifications
            .Where(kvp => kvp.Value is Classification.PersonalDataContainer)
            .Select(kvp => ResolveProperty(kvp.Key))
            .Where(property => !PropertyTypeContainsPersonalData(property.PropertyType))
            .Select(property => $"{property.DeclaringType!.Name}.{property.Name}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        missing.ShouldBeEmpty("Container properties must remain discoverable through nested [PersonalData] metadata.");
    }

    [Fact]
    public void IdentityHistoryPersonalData_RemainsIndependentOfProfileKeyAnnotations()
    {
        PropertyInfo[] history = s_expectedClassifications
            .Where(kvp => kvp.Value is Classification.IdentityHistoryPersonalData)
            .Select(kvp => ResolveProperty(kvp.Key))
            .ToArray();

        history.Select(property => property.Name).Order(StringComparer.Ordinal).ShouldBe(
            new[] { nameof(PartyState.HumanActorBindings), nameof(PartyState.HumanActorTransitions) });
        foreach (PropertyInfo property in history)
        {
            property.IsDefined(typeof(PersonalDataAttribute), inherit: true).ShouldBeFalse();
            PropertyTypeContainsPersonalData(property.PropertyType).ShouldBeFalse(
                "Identity history must remain under purpose custody, separate from profile-key encryption.");
        }
    }

    [Fact]
    public void OrganizationEntityFields_RemainUnmarkedByDefault()
    {
        string[] unexpectedlyMarked = s_expectedClassifications
            .Where(kvp => kvp.Value is Classification.NonPersonalByDefault)
            .Select(kvp => ResolveProperty(kvp.Key))
            .Where(property => property.IsDefined(typeof(PersonalDataAttribute), inherit: true))
            .Select(property => $"{property.DeclaringType!.Name}.{property.Name}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        unexpectedlyMarked.ShouldBeEmpty("D6 keeps organization entity fields unmarked until type-dependent v1.1 handling is accepted.");
    }

    [Fact]
    public void NonPersonalMetadataProperties_RemainUnmarked()
    {
        string[] unexpectedlyMarked = s_expectedClassifications
            .Where(kvp => kvp.Value is Classification.NonPersonalMetadata)
            .Select(kvp => ResolveProperty(kvp.Key))
            .Where(property => property.IsDefined(typeof(PersonalDataAttribute), inherit: true))
            .Select(property => $"{property.DeclaringType!.Name}.{property.Name}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        unexpectedlyMarked.ShouldBeEmpty("A [PersonalData]-marked property cannot be classified as non-personal metadata.");
    }

    [Fact]
    public void InspectedContractProperties_AreClassified()
    {
        string[] unclassified = s_inspectedTypes
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => Key(type, property.Name)))
            .Where(key => !s_expectedClassifications.ContainsKey(key) && !s_identityClassifications.ContainsKey(key))
            .Order(StringComparer.Ordinal)
            .ToArray();

        unclassified.ShouldBeEmpty("Each inspected contract property must be classified as personal, non-personal, or deferred.");
    }

    [Fact]
    public void IdentityInventory_CoversEveryIdentityContractAndNestedRecord()
    {
        Type[] roots = [.. typeof(CreateParty).Assembly.GetTypes().Where(type => !type.IsInterface && !type.IsEnum
            && type.Namespace is "Hexalith.Parties.Contracts.Commands" or "Hexalith.Parties.Contracts.Queries"
                or "Hexalith.Parties.Contracts.Events" or "Hexalith.Parties.Contracts.Events.Rejections"
                or "Hexalith.Parties.Contracts.Results")
            .Where(type => type.Name.Contains("HumanActor", StringComparison.Ordinal)
                || type.Name.Contains("PartyIdentity", StringComparison.Ordinal)
                || type.Name.Contains("AgentParty", StringComparison.Ordinal))];
        foreach (Type type in IdentityRecordGraph(roots.Concat(s_identityTypes)))
        {
            s_identityTypes.ShouldContain(type, $"Identity contract {type.Name} needs an explicit contextual inventory.");
        }
        s_identityTypes.SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => Key(type, property.Name))).Order(StringComparer.Ordinal)
            .ShouldBe(s_identityClassifications.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void IdentityInventory_TraversesNestedContainersAndValueRecords()
    {
        Type[] records = [.. IdentityRecordGraph([typeof(Dictionary<string, IReadOnlyList<IdentityInventoryValueRecord?[]>>),
            typeof(HumanActorBindingEvidence[]), typeof(IEventPayload), typeof(int), typeof(string)])];

        records.ShouldContain(typeof(IdentityInventoryValueRecord));
        records.ShouldContain(typeof(HumanActorBindingEvidence));
        records.ShouldContain(typeof(IdentityHistoryCustodyEvidence));
        records.ShouldNotContain(typeof(HumanActorBindingEvidence[]));
        records.ShouldNotContain(typeof(IEventPayload));
        records.ShouldNotContain(typeof(int));
        records.ShouldNotContain(typeof(string));
        records.Distinct().Count().ShouldBe(records.Length);
    }

    private static IEnumerable<Type> IdentityRecordGraph(IEnumerable<Type> roots)
    {
        var visited = new HashSet<Type>();
        var pending = new Stack<Type>(roots);
        while (pending.TryPop(out Type? type))
        {
            if (!visited.Add(type)) { continue; }
            if (type.HasElementType)
            {
                pending.Push(type.GetElementType()!);
                continue;
            }
            foreach (Type argument in type.GetGenericArguments()) { pending.Push(argument); }
            // Traverse framework containers, but inventory only concrete Hexalith
            // records/classes, including value records. Interfaces are not payloads.
            if (type.IsInterface || type.IsEnum || type.Namespace?.StartsWith("Hexalith.", StringComparison.Ordinal) != true)
            {
                continue;
            }
            yield return type;
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                pending.Push(property.PropertyType);
            }
        }
    }

    [Fact]
    public void IdentityInventory_RecordsRationaleWithoutCouplingHistoryOrProofsToProfileKeys()
    {
        foreach (var (key, entry) in s_identityClassifications)
        {
            entry.Rationale.ShouldNotBeNullOrWhiteSpace($"{key} requires a context-specific rationale.");
            PropertyInfo property = ResolveProperty(key);
            property.IsDefined(typeof(PersonalDataAttribute), inherit: true).ShouldBe(
                entry.Classification == Classification.ProfileProtectedContainer,
                $"{key}: {entry.Rationale} Preserve the existing profile/history protection boundary.");
        }
        foreach (Type command in new[] { typeof(EstablishHumanActorBinding), typeof(RebindHumanActorBinding),
            typeof(RevokeHumanActorBinding), typeof(ProvisionAgentParty) })
        {
            command.GetProperty("Authorization")!.GetCustomAttribute<JsonIgnoreAttribute>()!.Condition
                .ShouldBe(JsonIgnoreCondition.Always);
        }
    }

    private static PropertyInfo ResolveProperty(string key)
    {
        string[] parts = key.Split('.');
        Type type = s_inspectedTypes.Single(t => t.Name == parts[0]);
        return type.GetProperty(parts[1], BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Expected inspected property '{key}'.");
    }

    private static bool PropertyTypeContainsPersonalData(Type type)
    {
        Type actualType = Nullable.GetUnderlyingType(type) ?? type;
        if (actualType == typeof(string) || actualType.IsValueType)
        {
            return false;
        }

        if (actualType.IsGenericType && actualType.GetGenericArguments().Length == 1)
        {
            actualType = actualType.GetGenericArguments()[0];
        }

        return actualType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Any(property => property.IsDefined(typeof(PersonalDataAttribute), inherit: true));
    }

    private static string Key(Type type, string propertyName) => $"{type.Name}.{propertyName}";

    private enum Classification
    {
        SensitiveIdentityInput,
        TransientAuthorization,
        ProfileProtectedContainer,
        ProfileProtectedProvisioning,
        SensitiveIdentityResponse,
        IdentityProtocolMetadata,
        IdentityCustodyMetadata,
        PersonalData,
        PersonalDataContainer,
        IdentityHistoryPersonalData,
        TypeDependentContainer,
        NonPersonalMetadata,
        NonPersonalByDefault,
        DeferredPrivacyDesign,
    }
}
