using System.Reflection;

using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.State;
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

            [Key(typeof(PartyIndexEntry), nameof(PartyIndexEntry.Id))] = Classification.NonPersonalMetadata,
            [Key(typeof(PartyIndexEntry), nameof(PartyIndexEntry.Type))] = Classification.NonPersonalMetadata,
            [Key(typeof(PartyIndexEntry), nameof(PartyIndexEntry.IsActive))] = Classification.NonPersonalMetadata,
            [Key(typeof(PartyIndexEntry), nameof(PartyIndexEntry.DisplayName))] = Classification.PersonalData,
            [Key(typeof(PartyIndexEntry), nameof(PartyIndexEntry.SearchableContactChannels))] = Classification.PersonalDataContainer,
            [Key(typeof(PartyIndexEntry), nameof(PartyIndexEntry.SearchableIdentifiers))] = Classification.PersonalDataContainer,
            [Key(typeof(PartyIndexEntry), nameof(PartyIndexEntry.CreatedAt))] = Classification.NonPersonalMetadata,
            [Key(typeof(PartyIndexEntry), nameof(PartyIndexEntry.LastModifiedAt))] = Classification.NonPersonalMetadata,
            [Key(typeof(PartyIndexEntry), nameof(PartyIndexEntry.IsErased))] = Classification.NonPersonalMetadata,
        };

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
        typeof(PartyIndexEntry),
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
    public void InspectedContractProperties_AreClassified()
    {
        string[] unclassified = s_inspectedTypes
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Select(property => Key(property.DeclaringType!, property.Name))
            .Where(key => !s_expectedClassifications.ContainsKey(key))
            .Order(StringComparer.Ordinal)
            .ToArray();

        unclassified.ShouldBeEmpty("Each inspected contract property must be classified as personal, non-personal, or deferred.");
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
        PersonalData,
        PersonalDataContainer,
        TypeDependentContainer,
        NonPersonalMetadata,
        NonPersonalByDefault,
        DeferredPrivacyDesign,
    }
}
