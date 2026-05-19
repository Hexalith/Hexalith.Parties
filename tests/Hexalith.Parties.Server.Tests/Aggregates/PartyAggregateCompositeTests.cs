using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Results;
using Hexalith.Parties.Contracts.State;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Testing;
using Hexalith.Parties.Server.Aggregates;

using Shouldly;

namespace Hexalith.Parties.Server.Tests.Aggregates;

public class PartyAggregateCompositeTests
{
    [Fact]
    public void Handle_CreatePartyComposite_PersonWithTwoChannelsAndOneIdentifier_EmitsExpectedEvents()
    {
        CreatePartyComposite command = PartyTestData.ValidCreatePersonComposite();

        CompositeCommandResult result = PartyAggregate.Handle(command, null);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(5);
        result.Events[0].ShouldBeOfType<PartyCreated>();
        result.Events[1].ShouldBeOfType<PartyDisplayNameDerived>();
        result.Events[2].ShouldBeOfType<ContactChannelAdded>();
        result.Events[3].ShouldBeOfType<ContactChannelAdded>();
        result.Events[4].ShouldBeOfType<IdentifierAdded>();
        result.Applied.Count.ShouldBe(5);
        result.Skipped.ShouldBeEmpty();
        result.Rejected.ShouldBeEmpty();
    }

    [Fact]
    public void Handle_CreatePartyComposite_Success_ReturnsUpdatedPartyDetailReflectingAllEmittedEvents()
    {
        CreatePartyComposite command = PartyTestData.ValidCreatePersonComposite();

        CompositeCommandResult result = PartyAggregate.Handle(command, null);

        result.IsSuccess.ShouldBeTrue();
        result.UpdatedPartyDetail.ShouldNotBeNull();
        result.UpdatedPartyDetail.Id.ShouldBe(command.PartyId);
        result.UpdatedPartyDetail.Type.ShouldBe(PartyType.Person);
        result.UpdatedPartyDetail.IsActive.ShouldBeTrue();
        result.UpdatedPartyDetail.PersonDetails.ShouldNotBeNull();
        PartyDisplayNameDerived derived = result.Events.OfType<PartyDisplayNameDerived>().Single();
        result.UpdatedPartyDetail.DisplayName.ShouldBe(derived.DisplayName);
        result.UpdatedPartyDetail.SortName.ShouldBe(derived.SortName);
        result.UpdatedPartyDetail.ContactChannels.Count.ShouldBe(2);
        result.UpdatedPartyDetail.Identifiers.Count.ShouldBe(1);
        result.UpdatedPartyDetail.CreatedAt.ShouldNotBe(default);
        result.UpdatedPartyDetail.LastModifiedAt.ShouldNotBe(default);
    }

    [Fact]
    public void Handle_CreatePartyComposite_DuplicateContactChannelIds_SkipsDuplicate()
    {
        CreatePartyComposite command = PartyTestData.ValidCreatePersonComposite() with
        {
            ContactChannels =
            [
                new AddContactChannel
                {
                    PartyId = PartyTestData.DefaultPartyId,
                    ContactChannelId = "ch-1",
                    Type = ContactChannelType.Email,
                    Value = "john@example.com",
                },
                new AddContactChannel
                {
                    PartyId = PartyTestData.DefaultPartyId,
                    ContactChannelId = "ch-1",
                    Type = ContactChannelType.Email,
                    Value = "john.dup@example.com",
                },
            ],
            Identifiers = [],
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, null);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(3);
        result.Skipped.Count.ShouldBe(1);
        result.Skipped[0].ShouldBe("Duplicate contact channel: ch-1");
    }

    [Fact]
    public void Handle_CreatePartyComposite_DerivedDisplayNameApplied_DoesNotContainPiiName()
    {
        CreatePartyComposite command = PartyTestData.ValidCreatePersonComposite();

        CompositeCommandResult result = PartyAggregate.Handle(command, null);

        result.Applied.ShouldContain("Derived display name");
        result.Applied.Any(x => x.Contains("John") || x.Contains("Doe")).ShouldBeFalse();
    }

    [Fact]
    public void Handle_CreatePartyComposite_InvalidContactChannelId_ReturnsRejectionWithoutSuccessEvents()
    {
        CreatePartyComposite command = PartyTestData.ValidCreatePersonComposite() with
        {
            ContactChannels =
            [
                new AddContactChannel
                {
                    PartyId = PartyTestData.DefaultPartyId,
                    ContactChannelId = " ",
                    Type = ContactChannelType.Email,
                    Value = "john@example.com",
                },
            ],
            Identifiers = [],
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, null);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<CompositeOperationConflict>();
        result.Applied.ShouldBeEmpty();
        result.Skipped.ShouldBeEmpty();
        result.Rejected.ShouldContain("Contact channel ID is required.");
    }

    [Fact]
    public void Handle_CreatePartyComposite_InvalidIdentifierId_ReturnsRejectionWithoutSuccessEvents()
    {
        CreatePartyComposite command = PartyTestData.ValidCreatePersonComposite() with
        {
            ContactChannels = [],
            Identifiers =
            [
                new AddIdentifier
                {
                    PartyId = PartyTestData.DefaultPartyId,
                    IdentifierId = string.Empty,
                    Type = IdentifierType.VAT,
                    Value = "synthetic-vat-value",
                },
            ],
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, null);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<CompositeOperationConflict>();
        result.Applied.ShouldBeEmpty();
        result.Skipped.ShouldBeEmpty();
        result.Rejected.ShouldContain("Identifier ID is required.");
    }

    [Fact]
    public void Handle_CreatePartyComposite_PayloadExceedsConfiguredMax_ReturnsRejection()
    {
        int previous = PartyAggregate.MaxSubOperations;
        PartyAggregate.MaxSubOperations = 3;

        try
        {
            CreatePartyComposite command = PartyTestData.ValidCreatePersonComposite() with
            {
                ContactChannels =
                [
                    new AddContactChannel
                    {
                        PartyId = PartyTestData.DefaultPartyId,
                        ContactChannelId = "ch-1",
                        Type = ContactChannelType.Email,
                        Value = "john@example.com",
                    },
                    new AddContactChannel
                    {
                        PartyId = PartyTestData.DefaultPartyId,
                        ContactChannelId = "ch-2",
                        Type = ContactChannelType.Phone,
                        Value = "+33111111111",
                    },
                    new AddContactChannel
                    {
                        PartyId = PartyTestData.DefaultPartyId,
                        ContactChannelId = "ch-3",
                        Type = ContactChannelType.SocialMedia,
                        Value = "@john",
                    },
                ],
                Identifiers = [],
            };

            CompositeCommandResult result = PartyAggregate.Handle(command, null);

            result.IsRejection.ShouldBeTrue();
            result.Events.Count.ShouldBe(1);
            result.Events[0].ShouldBeOfType<CompositeOperationConflict>();
            result.Rejected.ShouldContain(x => x.Contains("Payload size exceeded:"));
        }
        finally
        {
            PartyAggregate.MaxSubOperations = previous;
        }
    }

    [Fact]
    public void Handle_CreatePartyComposite_OrganizationWithChannelsAndIdentifiers_EmitsExpectedEvents()
    {
        CreatePartyComposite command = PartyTestData.ValidCreateOrganizationComposite();

        CompositeCommandResult result = PartyAggregate.Handle(command, null);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(4);
        result.Events[0].ShouldBeOfType<PartyCreated>();
        result.Events[1].ShouldBeOfType<PartyDisplayNameDerived>();
        result.Events[2].ShouldBeOfType<ContactChannelAdded>();
        result.Events[3].ShouldBeOfType<IdentifierAdded>();
        result.Applied.Count.ShouldBe(4);
        result.Applied.ShouldContain("Created organization party");
        result.Applied.ShouldContain("Derived display name");
        result.Applied.ShouldContain("Added contact channel: ch-email-1 (Email)");
        result.Applied.ShouldContain("Added identifier: id-vat-1 (VAT)");
        result.Skipped.ShouldBeEmpty();
        result.Rejected.ShouldBeEmpty();
    }

    [Fact]
    public void Handle_CreatePartyComposite_PartyOnlyNoChannelsNoIdentifiers_EmitsOnlyCreateAndDisplayName()
    {
        CreatePartyComposite command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            Type = PartyType.Person,
            PersonDetails = PartyTestData.ValidPersonDetails(),
            ContactChannels = [],
            Identifiers = [],
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, null);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(2);
        result.Events[0].ShouldBeOfType<PartyCreated>();
        result.Events[1].ShouldBeOfType<PartyDisplayNameDerived>();
        result.Applied.Count.ShouldBe(2);
        result.Applied.ShouldContain("Created person party");
        result.Applied.ShouldContain("Derived display name");
        result.Skipped.ShouldBeEmpty();
        result.Rejected.ShouldBeEmpty();
    }

    [Fact]
    public void Handle_CreatePartyComposite_PartyAlreadyExists_ReturnsNoOp()
    {
        PartyState existingState = PartyTestData.CreatePersonState();
        CreatePartyComposite command = PartyTestData.ValidCreatePersonComposite();

        CompositeCommandResult result = PartyAggregate.Handle(command, existingState);

        result.IsNoOp.ShouldBeTrue();
        result.Events.ShouldBeEmpty();
        result.Applied.ShouldBeEmpty();
        result.Skipped.ShouldBeEmpty();
        result.Rejected.ShouldBeEmpty();
    }

    [Fact]
    public void Handle_CreatePartyComposite_MissingPartyType_ReturnsRejection()
    {
        CreatePartyComposite command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            Type = default,
            ContactChannels = [],
            Identifiers = [],
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, null);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<PartyCannotBeCreatedWithoutType>();
        result.Applied.ShouldBeEmpty();
        result.Rejected.ShouldContain("Party type is required.");
    }

    [Fact]
    public void Handle_CreatePartyComposite_DuplicateIdentifierIds_SkipsDuplicate()
    {
        CreatePartyComposite command = PartyTestData.ValidCreatePersonComposite() with
        {
            ContactChannels = [],
            Identifiers =
            [
                new AddIdentifier
                {
                    PartyId = PartyTestData.DefaultPartyId,
                    IdentifierId = "id-vat-1",
                    Type = IdentifierType.VAT,
                    Value = "synthetic-vat-value",
                },
                new AddIdentifier
                {
                    PartyId = PartyTestData.DefaultPartyId,
                    IdentifierId = "id-vat-1",
                    Type = IdentifierType.VAT,
                    Value = "synthetic-other-vat-value",
                },
            ],
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, null);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(3);
        result.Events[2].ShouldBeOfType<IdentifierAdded>();
        result.Skipped.Count.ShouldBe(1);
        result.Skipped[0].ShouldBe("Duplicate identifier: id-vat-1");
    }

    [Fact]
    public void Handle_CreatePartyComposite_PreferredChannel_EmitsPreferredContactChannelChanged()
    {
        CreatePartyComposite command = PartyTestData.ValidCreatePersonComposite() with
        {
            ContactChannels =
            [
                new AddContactChannel
                {
                    PartyId = PartyTestData.DefaultPartyId,
                    ContactChannelId = "ch-email-pref",
                    Type = ContactChannelType.Email,
                    Value = "preferred@example.com",
                    IsPreferred = true,
                },
            ],
            Identifiers = [],
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, null);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(4);
        result.Events[2].ShouldBeOfType<ContactChannelAdded>();
        result.Events[3].ShouldBeOfType<PreferredContactChannelChanged>();
        result.Applied.ShouldContain("Set preferred contact channel: ch-email-pref");
    }

    [Fact]
    public void Handle_CreatePartyComposite_MaxChannels50InSingleCreate_AllApplied()
    {
        List<AddContactChannel> channels = [];
        for (int i = 0; i < 50; i++)
        {
            channels.Add(new AddContactChannel
            {
                PartyId = PartyTestData.DefaultPartyId,
                ContactChannelId = $"ch-{i}",
                Type = ContactChannelType.Email,
                Value = $"user{i}@example.com",
            });
        }

        CreatePartyComposite command = PartyTestData.ValidCreatePersonComposite() with
        {
            ContactChannels = channels,
            Identifiers = [],
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, null);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(52);
        result.Applied.Count.ShouldBe(52);
        List<ContactChannelAdded> addedChannels = result.Events.OfType<ContactChannelAdded>().ToList();
        addedChannels.Count.ShouldBe(50);
        addedChannels
            .Select(x => x.ContactChannelId)
            .ShouldBe(channels.Select(x => x.ContactChannelId), ignoreOrder: true);
        result.Skipped.ShouldBeEmpty();
        result.Rejected.ShouldBeEmpty();
    }

    [Fact]
    public void Handle_UpdatePartyComposite_PersonDetailsOnly_EmitsPersonDetailsUpdatedAndDisplayName()
    {
        PartyState state = PartyTestData.CreatePersonStateWithChannelsAndIdentifiers();
        UpdatePartyComposite command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            PersonDetails = new PersonDetails { FirstName = "Jane", LastName = "Smith" },
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, state);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(2);
        result.Events[0].ShouldBeOfType<PersonDetailsUpdated>();
        result.Events[1].ShouldBeOfType<PartyDisplayNameDerived>();
        PartyDisplayNameDerived derived = (PartyDisplayNameDerived)result.Events[1];
        derived.DisplayName.ShouldBe("Jane Smith");
        derived.SortName.ShouldBe("Smith, Jane");
        result.UpdatedPartyDetail.ShouldNotBeNull();
        result.UpdatedPartyDetail.CreatedAt.ShouldBe(state.CreatedAt);
        result.UpdatedPartyDetail.LastModifiedAt.ShouldNotBe(default);
        result.UpdatedPartyDetail.PersonDetails.ShouldNotBeNull();
        result.UpdatedPartyDetail.PersonDetails.FirstName.ShouldBe("Jane");
        result.UpdatedPartyDetail.PersonDetails.LastName.ShouldBe("Smith");
        result.UpdatedPartyDetail.OrganizationDetails.ShouldBeNull();
        result.UpdatedPartyDetail.DisplayName.ShouldBe("Jane Smith");
        result.UpdatedPartyDetail.SortName.ShouldBe("Smith, Jane");
        result.Applied.ShouldContain("Updated person details");
        result.Applied.ShouldContain("Derived display name");
        result.Skipped.ShouldBeEmpty();
        result.Rejected.ShouldBeEmpty();
    }

    [Fact]
    public void Handle_UpdatePartyComposite_OrganizationDetailsOnly_EmitsOrganizationDetailsUpdatedAndDisplayName()
    {
        PartyState state = PartyTestData.CreateOrganizationStateWithChannelsAndIdentifiers();
        OrganizationDetails orgDetails = new() { LegalName = "New Corp", TradingName = "New Trading" };
        UpdatePartyComposite command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            OrganizationDetails = orgDetails,
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, state);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(2);
        result.Events[0].ShouldBeOfType<OrganizationDetailsUpdated>();
        result.Events[1].ShouldBeOfType<PartyDisplayNameDerived>();
        PartyDisplayNameDerived derived = (PartyDisplayNameDerived)result.Events[1];
        derived.DisplayName.ShouldBe(orgDetails.LegalName);
        derived.SortName.ShouldBe(orgDetails.LegalName);
        result.UpdatedPartyDetail.ShouldNotBeNull();
        result.UpdatedPartyDetail.OrganizationDetails.ShouldNotBeNull();
        result.UpdatedPartyDetail.OrganizationDetails.LegalName.ShouldBe(orgDetails.LegalName);
        result.UpdatedPartyDetail.OrganizationDetails.TradingName.ShouldBe(orgDetails.TradingName);
        result.UpdatedPartyDetail.PersonDetails.ShouldBeNull();
        result.UpdatedPartyDetail.DisplayName.ShouldBe(orgDetails.LegalName);
        result.UpdatedPartyDetail.SortName.ShouldBe(orgDetails.LegalName);
        result.Applied.ShouldContain("Updated organization details");
        result.Applied.ShouldContain("Derived display name");
        result.Skipped.ShouldBeEmpty();
        result.Rejected.ShouldBeEmpty();
    }

    [Fact]
    public void Handle_UpdatePartyComposite_AddChannelsOnly_EmitsContactChannelAddedEvents()
    {
        PartyState state = PartyTestData.CreatePersonStateWithChannelsAndIdentifiers();
        UpdatePartyComposite command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            AddContactChannels =
            [
                new AddContactChannel
                {
                    PartyId = PartyTestData.DefaultPartyId,
                    ContactChannelId = "ch-phone-1",
                    Type = ContactChannelType.Phone,
                    Value = "+33111111111",
                },
            ],
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, state);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<ContactChannelAdded>();
        result.Applied.ShouldContain("Added contact channel: ch-phone-1 (Phone)");
        result.Skipped.ShouldBeEmpty();
        result.Rejected.ShouldBeEmpty();

        result.UpdatedPartyDetail.ShouldNotBeNull();
        ContactChannel? added = result.UpdatedPartyDetail.ContactChannels.SingleOrDefault(c => c.Id == "ch-phone-1");
        added.ShouldNotBeNull();
        added.Type.ShouldBe(ContactChannelType.Phone);
        added.Value.ShouldBe("+33111111111");
    }

    [Fact]
    public void Handle_UpdatePartyComposite_UpdateChannelsOnly_EmitsContactChannelUpdatedEvents()
    {
        PartyState state = PartyTestData.CreatePersonStateWithChannelsAndIdentifiers();
        UpdatePartyComposite command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            UpdateContactChannels =
            [
                new UpdateContactChannel
                {
                    PartyId = PartyTestData.DefaultPartyId,
                    ContactChannelId = "ch-email-1",
                    Value = "updated@example.com",
                },
            ],
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, state);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<ContactChannelUpdated>();
        result.Applied.ShouldContain("Updated contact channel: ch-email-1");
        result.Skipped.ShouldBeEmpty();
        result.Rejected.ShouldBeEmpty();

        result.UpdatedPartyDetail.ShouldNotBeNull();
        ContactChannel? updated = result.UpdatedPartyDetail.ContactChannels.SingleOrDefault(c => c.Id == "ch-email-1");
        updated.ShouldNotBeNull();
        updated.Value.ShouldBe("updated@example.com");
    }

    [Fact]
    public void Handle_UpdatePartyComposite_RemoveChannelsOnly_EmitsContactChannelRemovedEvents()
    {
        PartyState state = PartyTestData.CreatePersonStateWithChannelsAndIdentifiers();
        UpdatePartyComposite command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            RemoveContactChannelIds = ["ch-email-2"],
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, state);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<ContactChannelRemoved>();
        result.Applied.ShouldContain("Removed contact channel: ch-email-2");
        result.Skipped.ShouldBeEmpty();
        result.Rejected.ShouldBeEmpty();

        result.UpdatedPartyDetail.ShouldNotBeNull();
        result.UpdatedPartyDetail.ContactChannels.ShouldNotContain(c => c.Id == "ch-email-2");
    }

    [Fact]
    public void Handle_UpdatePartyComposite_ChangePreferredChannel_TypeScopedReflectedInUpdatedPartyDetail()
    {
        PartyState state = PartyTestData.CreatePersonStateWithChannelsAndIdentifiers();
        UpdatePartyComposite command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            UpdateContactChannels =
            [
                new UpdateContactChannel
                {
                    PartyId = PartyTestData.DefaultPartyId,
                    ContactChannelId = "ch-email-2",
                    IsPreferred = true,
                },
            ],
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, state);

        result.IsSuccess.ShouldBeTrue();
        result.UpdatedPartyDetail.ShouldNotBeNull();
        ContactChannel preferredEmail = result.UpdatedPartyDetail.ContactChannels.Single(c => c.Id == "ch-email-2");
        preferredEmail.IsPreferred.ShouldBeTrue();
        ContactChannel previousPreferred = result.UpdatedPartyDetail.ContactChannels.Single(c => c.Id == "ch-email-1");
        previousPreferred.IsPreferred.ShouldBeFalse();
    }

    [Fact]
    public void Handle_UpdatePartyComposite_AddIdentifiersOnly_EmitsIdentifierAddedEvents()
    {
        PartyState state = PartyTestData.CreatePersonStateWithChannelsAndIdentifiers();
        UpdatePartyComposite command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            AddIdentifiers =
            [
                new AddIdentifier
                {
                    PartyId = PartyTestData.DefaultPartyId,
                    IdentifierId = "id-siret-1",
                    Type = IdentifierType.SIRET,
                    Value = "synthetic-siret-value",
                },
            ],
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, state);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<IdentifierAdded>();
        result.Applied.ShouldContain("Added identifier: id-siret-1 (SIRET)");
        result.Skipped.ShouldBeEmpty();
        result.Rejected.ShouldBeEmpty();
        result.UpdatedPartyDetail.ShouldNotBeNull();
        PartyIdentifier added = result.UpdatedPartyDetail.Identifiers.Single(x => x.Id == "id-siret-1");
        added.Type.ShouldBe(IdentifierType.SIRET);
        added.Value.ShouldBe("synthetic-siret-value");
    }

    [Fact]
    public void Handle_UpdatePartyComposite_RemoveIdentifiersOnly_EmitsIdentifierRemovedEvents()
    {
        PartyState state = PartyTestData.CreatePersonStateWithChannelsAndIdentifiers();
        UpdatePartyComposite command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            RemoveIdentifierIds = ["id-vat-1"],
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, state);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<IdentifierRemoved>();
        result.Applied.ShouldContain("Removed identifier: id-vat-1");
        result.Skipped.ShouldBeEmpty();
        result.Rejected.ShouldBeEmpty();
        result.UpdatedPartyDetail.ShouldNotBeNull();
        result.UpdatedPartyDetail.Identifiers.Any(x => x.Id == "id-vat-1").ShouldBeFalse();
    }

    [Fact]
    public void Handle_UpdatePartyComposite_PersonDetailsChannelsAndIdentifiers_EmitsExpectedEvents()
    {
        PartyState state = PartyTestData.CreatePersonStateWithChannelsAndIdentifiers();
        UpdatePartyComposite command = PartyTestData.ValidUpdatePersonComposite();

        CompositeCommandResult result = PartyAggregate.Handle(command, state);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(6);
        result.Events[0].ShouldBeOfType<PersonDetailsUpdated>();
        result.Events[1].ShouldBeOfType<PartyDisplayNameDerived>();
        result.Events[2].ShouldBeOfType<ContactChannelAdded>();
        result.Events[3].ShouldBeOfType<ContactChannelUpdated>();
        result.Events[4].ShouldBeOfType<ContactChannelRemoved>();
        result.Events[5].ShouldBeOfType<IdentifierAdded>();
        result.Applied.ShouldContain("Updated person details");
        result.Applied.ShouldContain("Derived display name");
        result.Applied.ShouldContain("Added contact channel: ch-phone-1 (Phone)");
        result.Applied.ShouldContain("Updated contact channel: ch-email-1");
        result.Applied.ShouldContain("Removed contact channel: ch-email-2");
        result.Applied.ShouldContain("Added identifier: id-siret-1 (SIRET)");
        result.Skipped.ShouldBeEmpty();
        result.Rejected.ShouldBeEmpty();
    }

    [Fact]
    public void Handle_UpdatePartyComposite_NullPersonDetails_DoesNotUpdatePersonDetails()
    {
        PartyState state = BuildExistingPersonState();
        UpdatePartyComposite command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            AddIdentifiers =
            [
                new AddIdentifier
                {
                    PartyId = PartyTestData.DefaultPartyId,
                    IdentifierId = "id-siret-1",
                    Type = IdentifierType.SIRET,
                    Value = "synthetic-siret-value",
                },
            ],
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, state);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<IdentifierAdded>();
        result.Events.Any(e => e is PersonDetailsUpdated).ShouldBeFalse();
        result.Events.Any(e => e is PartyDisplayNameDerived).ShouldBeFalse();
    }

    [Fact]
    public void Handle_UpdatePartyComposite_DuplicateAddContactChannel_SkipsDuplicate()
    {
        PartyState state = BuildExistingPersonState();
        UpdatePartyComposite command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            AddContactChannels =
            [
                new AddContactChannel
                {
                    PartyId = PartyTestData.DefaultPartyId,
                    ContactChannelId = "email-1",
                    Type = ContactChannelType.Email,
                    Value = "already@exists.example",
                },
            ],
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, state);

        result.IsNoOp.ShouldBeTrue();
        result.Events.ShouldBeEmpty();
        result.Skipped.ShouldContain("Duplicate contact channel: email-1");
    }

    [Fact]
    public void Handle_UpdatePartyComposite_UpdateMissingChannel_ReturnsRejection()
    {
        PartyState state = BuildExistingPersonState();
        UpdatePartyComposite command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            UpdateContactChannels =
            [
                new UpdateContactChannel
                {
                    PartyId = PartyTestData.DefaultPartyId,
                    ContactChannelId = "missing-channel",
                    Value = "missing@example.com",
                },
            ],
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, state);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        ContactChannelNotFound notFound = result.Events[0].ShouldBeOfType<ContactChannelNotFound>();
        result.Rejected.ShouldContain("Contact channel 'missing-channel' not found.");
        string rawValue = command.UpdateContactChannels[0].Value!;
        rawValue.ShouldNotBeNullOrEmpty("Privacy absence assertion requires a non-empty sentinel — empty string makes Contains() trivially true.");
        result.Rejected.Any(x => x.Contains(rawValue)).ShouldBeFalse();
        (notFound.Message ?? string.Empty).Contains(rawValue).ShouldBeFalse("Persisted event Message must not carry the raw contact value.");
        result.Events.OfType<ContactChannelUpdated>().ShouldBeEmpty();
    }

    [Fact]
    public void Handle_UpdatePartyComposite_RemoveMissingChannel_ReturnsRejection()
    {
        PartyState state = BuildExistingPersonState();
        UpdatePartyComposite command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            RemoveContactChannelIds = ["missing-channel"],
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, state);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<ContactChannelNotFound>();
        result.Rejected.ShouldContain("Contact channel 'missing-channel' not found.");
    }

    [Fact]
    public void Handle_UpdatePartyComposite_ConflictingAddAndRemoveChannel_ReturnsRejection()
    {
        PartyState state = BuildExistingPersonState();
        UpdatePartyComposite command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            AddContactChannels =
            [
                new AddContactChannel
                {
                    PartyId = PartyTestData.DefaultPartyId,
                    ContactChannelId = "email-3",
                    Type = ContactChannelType.Email,
                    Value = "new@example.com",
                },
            ],
            RemoveContactChannelIds = ["email-3"],
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, state);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        CompositeOperationConflict conflict = result.Events[0].ShouldBeOfType<CompositeOperationConflict>();
        result.Rejected.ShouldContain("Conflicting operations on same channel ID: email-3.");
        string rawValue = command.AddContactChannels[0].Value;
        rawValue.ShouldNotBeNullOrEmpty("Privacy absence assertion requires a non-empty sentinel — empty string makes Contains() trivially true.");
        result.Rejected.Any(x => x.Contains(rawValue)).ShouldBeFalse();
        (conflict.Message ?? string.Empty).Contains(rawValue).ShouldBeFalse("Persisted event Message must not carry the raw contact value.");
        result.Events.OfType<ContactChannelAdded>().ShouldBeEmpty();
        result.Events.OfType<ContactChannelRemoved>().ShouldBeEmpty();
    }

    [Fact]
    public void Handle_UpdatePartyComposite_ConflictingAddAndRemoveIdentifier_ReturnsRejection()
    {
        PartyState state = BuildExistingPersonState();
        UpdatePartyComposite command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            AddIdentifiers =
            [
                new AddIdentifier
                {
                    PartyId = PartyTestData.DefaultPartyId,
                    IdentifierId = "id-siret-1",
                    Type = IdentifierType.SIRET,
                    Value = "synthetic-siret-value",
                },
            ],
            RemoveIdentifierIds = ["id-siret-1"],
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, state);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        CompositeOperationConflict conflict = result.Events[0].ShouldBeOfType<CompositeOperationConflict>();
        result.Rejected.ShouldContain("Conflicting operations on same identifier ID: id-siret-1.");
        string rawValue = command.AddIdentifiers[0].Value;
        rawValue.ShouldNotBeNullOrEmpty("Privacy absence assertion requires a non-empty sentinel — empty string makes Contains() trivially true.");
        result.Rejected.Any(x => x.Contains(rawValue)).ShouldBeFalse();
        (conflict.Message ?? string.Empty).Contains(rawValue).ShouldBeFalse("Persisted event Message must not carry the raw identifier value.");
        result.Events.OfType<IdentifierAdded>().ShouldBeEmpty();
        result.Events.OfType<IdentifierRemoved>().ShouldBeEmpty();
    }

    [Fact]
    public void Handle_UpdatePartyComposite_NoOperations_ReturnsNoOp()
    {
        PartyState state = BuildExistingPersonState();
        UpdatePartyComposite command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, state);

        result.IsNoOp.ShouldBeTrue();
        result.Events.ShouldBeEmpty();
        result.Applied.ShouldBeEmpty();
        result.Skipped.ShouldBeEmpty();
        result.Rejected.ShouldBeEmpty();
    }

    [Fact]
    public void Handle_UpdatePartyComposite_PayloadExceedsConfiguredMax_ReturnsRejection()
    {
        int previous = PartyAggregate.MaxSubOperations;
        PartyAggregate.MaxSubOperations = 2;

        try
        {
            PartyState state = BuildExistingPersonState();
            UpdatePartyComposite command = PartyTestData.ValidUpdatePersonComposite();

            CompositeCommandResult result = PartyAggregate.Handle(command, state);

            result.IsRejection.ShouldBeTrue();
            result.Events.Count.ShouldBe(1);
            result.Events[0].ShouldBeOfType<CompositeOperationConflict>();
            result.Rejected.ShouldContain(x => x.Contains("Payload size exceeded:"));
        }
        finally
        {
            PartyAggregate.MaxSubOperations = previous;
        }
    }

    [Fact]
    public void Handle_UpdatePartyComposite_NullState_ReturnsPartyNotFound()
    {
        CompositeCommandResult result = PartyAggregate.Handle(PartyTestData.ValidUpdatePersonComposite(), null);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<PartyNotFound>();
        result.Events.OfType<PersonDetailsUpdated>().ShouldBeEmpty();
        result.Events.OfType<OrganizationDetailsUpdated>().ShouldBeEmpty();
        result.Events.OfType<PartyDisplayNameDerived>().ShouldBeEmpty();
        result.UpdatedPartyDetail.ShouldBeNull();
        result.Applied.ShouldBeEmpty();
        result.Skipped.ShouldBeEmpty();
        result.Rejected.ShouldContain("Party does not exist.");
    }

    [Fact]
    public void Handle_UpdatePartyComposite_DuplicateUpdateAndRemoveOperations_ReportSkippedAndPreserveRemovalOrder()
    {
        PartyState state = BuildExistingPersonState();
        state.Apply(new ContactChannelAdded
        {
            ContactChannelId = "email-3",
            Type = ContactChannelType.Email,
            Value = "third@example.com",
        });

        UpdatePartyComposite command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            UpdateContactChannels =
            [
                new UpdateContactChannel
                {
                    PartyId = PartyTestData.DefaultPartyId,
                    ContactChannelId = "email-3",
                    Value = "third-updated@example.com",
                },
                new UpdateContactChannel
                {
                    PartyId = PartyTestData.DefaultPartyId,
                    ContactChannelId = "email-3",
                    Value = "third-duplicate@example.com",
                },
            ],
            RemoveContactChannelIds = ["email-2", "email-1", "email-2"],
            RemoveIdentifierIds = ["id-vat-1", "id-vat-1"],
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, state);

        result.IsSuccess.ShouldBeTrue();
        result.Skipped.ShouldContain("Duplicate contact channel update: email-3");
        result.Skipped.ShouldContain("Duplicate contact channel removal: email-2");
        result.Skipped.ShouldContain("Duplicate identifier removal: id-vat-1");

        List<string> removedChannels = result.Events
            .OfType<ContactChannelRemoved>()
            .Select(x => x.ContactChannelId)
            .ToList();
        removedChannels.ShouldBe(["email-2", "email-1"]);

        List<string> removedIdentifiers = result.Events
            .OfType<IdentifierRemoved>()
            .Select(x => x.IdentifierId)
            .ToList();
        removedIdentifiers.ShouldBe(["id-vat-1"]);
    }

    [Fact]
    public void Handle_UpdatePartyComposite_AddChannelDuplicateInPayload_SkippedAsDuplicate()
    {
        PartyState state = PartyTestData.CreatePersonStateWithChannelsAndIdentifiers();
        UpdatePartyComposite command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            AddContactChannels =
            [
                new AddContactChannel
                {
                    PartyId = PartyTestData.DefaultPartyId,
                    ContactChannelId = "ch-new-1",
                    Type = ContactChannelType.Phone,
                    Value = "+33111111111",
                },
                new AddContactChannel
                {
                    PartyId = PartyTestData.DefaultPartyId,
                    ContactChannelId = "ch-new-1",
                    Type = ContactChannelType.Phone,
                    Value = "+33222222222",
                },
            ],
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, state);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<ContactChannelAdded>();
        result.Skipped.Count.ShouldBe(1);
        result.Skipped.ShouldContain("Duplicate contact channel: ch-new-1");
    }

    [Fact]
    public void Handle_UpdatePartyComposite_DuplicateAddIdentifier_SkipsDuplicate()
    {
        PartyState state = PartyTestData.CreatePersonStateWithChannelsAndIdentifiers();
        UpdatePartyComposite command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            AddIdentifiers =
            [
                new AddIdentifier
                {
                    PartyId = PartyTestData.DefaultPartyId,
                    IdentifierId = "id-vat-1",
                    Type = IdentifierType.VAT,
                    Value = "synthetic-vat-value",
                },
            ],
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, state);

        result.IsNoOp.ShouldBeTrue();
        result.Events.ShouldBeEmpty();
        result.Events.OfType<IdentifierAdded>().ShouldBeEmpty();
        result.Skipped.ShouldContain("Duplicate identifier: id-vat-1");
        result.Skipped.Any(x => x.Contains(command.AddIdentifiers[0].Value)).ShouldBeFalse();
    }

    [Fact]
    public void Handle_UpdatePartyComposite_DuplicateAddIdentifierInPayload_SkipsDuplicateWithoutValue()
    {
        PartyState state = PartyTestData.CreatePersonStateWithChannelsAndIdentifiers();
        UpdatePartyComposite command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            AddIdentifiers =
            [
                new AddIdentifier
                {
                    PartyId = PartyTestData.DefaultPartyId,
                    IdentifierId = "id-siret-1",
                    Type = IdentifierType.SIRET,
                    Value = "synthetic-siret-value",
                },
                new AddIdentifier
                {
                    PartyId = PartyTestData.DefaultPartyId,
                    IdentifierId = "id-siret-1",
                    Type = IdentifierType.SIRET,
                    Value = "synthetic-other-siret-value",
                },
            ],
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, state);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        IdentifierAdded added = result.Events.OfType<IdentifierAdded>().Single();
        added.IdentifierId.ShouldBe("id-siret-1");
        added.Type.ShouldBe(IdentifierType.SIRET);
        added.Value.ShouldBe(command.AddIdentifiers[0].Value);
        result.Skipped.ShouldContain("Duplicate identifier: id-siret-1");
        result.Skipped.Any(x => x.Contains(command.AddIdentifiers[0].Value)).ShouldBeFalse();
        result.Skipped.Any(x => x.Contains(command.AddIdentifiers[1].Value)).ShouldBeFalse();
        result.Applied.Any(x => x.Contains(command.AddIdentifiers[0].Value)).ShouldBeFalse();
        result.Applied.Any(x => x.Contains(command.AddIdentifiers[1].Value)).ShouldBeFalse();
        result.UpdatedPartyDetail.ShouldNotBeNull();
        result.UpdatedPartyDetail.Identifiers.Count(x => x.Id == "id-siret-1").ShouldBe(1);
        result.UpdatedPartyDetail.Identifiers.ShouldContain(x => x.Id == "id-vat-1");
    }

    [Fact]
    public void Handle_UpdatePartyComposite_InvalidRemoveIdentifierId_ReturnsRejection()
    {
        PartyState state = PartyTestData.CreatePersonStateWithChannelsAndIdentifiers();
        UpdatePartyComposite command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            RemoveIdentifierIds = ["id-missing-1"],
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, state);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<IdentifierNotFound>();
        result.Events.OfType<IdentifierRemoved>().ShouldBeEmpty();
        result.UpdatedPartyDetail.ShouldBeNull();
        result.Rejected.ShouldContain("Identifier 'id-missing-1' not found.");
    }

    [Fact]
    public void Handle_UpdatePartyComposite_ConflictingIdentifierAddAndRemove_ReturnsRejectionOnly()
    {
        PartyState state = PartyTestData.CreatePersonStateWithChannelsAndIdentifiers();
        UpdatePartyComposite command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            AddIdentifiers =
            [
                new AddIdentifier
                {
                    PartyId = PartyTestData.DefaultPartyId,
                    IdentifierId = "id-vat-1",
                    Type = IdentifierType.VAT,
                    Value = "synthetic-vat-value",
                },
            ],
            RemoveIdentifierIds = ["id-vat-1"],
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, state);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        CompositeOperationConflict conflict = result.Events[0].ShouldBeOfType<CompositeOperationConflict>();
        result.Events.OfType<IdentifierAdded>().ShouldBeEmpty();
        result.Events.OfType<IdentifierRemoved>().ShouldBeEmpty();
        result.UpdatedPartyDetail.ShouldBeNull();
        result.Applied.ShouldBeEmpty();
        result.Skipped.ShouldBeEmpty();
        result.Rejected.ShouldContain("Conflicting operations on same identifier ID: id-vat-1.");
        string rawValue = command.AddIdentifiers[0].Value;
        rawValue.ShouldNotBeNullOrEmpty("Privacy absence assertion requires a non-empty sentinel — empty string makes Contains() trivially true.");
        result.Rejected.Any(x => x.Contains(rawValue)).ShouldBeFalse();
        (conflict.Message ?? string.Empty).Contains(rawValue).ShouldBeFalse("Persisted event Message must not carry the raw identifier value.");
    }

    [Fact]
    public void Handle_UpdatePartyComposite_ConflictingChannelUpdateAndRemove_ReturnsRejection()
    {
        PartyState state = PartyTestData.CreatePersonStateWithChannelsAndIdentifiers();
        UpdatePartyComposite command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            UpdateContactChannels =
            [
                new UpdateContactChannel
                {
                    PartyId = PartyTestData.DefaultPartyId,
                    ContactChannelId = "ch-email-1",
                    Value = "updated@example.com",
                },
            ],
            RemoveContactChannelIds = ["ch-email-1"],
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, state);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        CompositeOperationConflict conflict = result.Events[0].ShouldBeOfType<CompositeOperationConflict>();
        result.Rejected.ShouldContain("Conflicting operations on same channel ID: ch-email-1.");
        string rawValue = command.UpdateContactChannels[0].Value!;
        rawValue.ShouldNotBeNullOrEmpty("Privacy absence assertion requires a non-empty sentinel — empty string makes Contains() trivially true.");
        result.Rejected.Any(x => x.Contains(rawValue)).ShouldBeFalse();
        (conflict.Message ?? string.Empty).Contains(rawValue).ShouldBeFalse("Persisted event Message must not carry the raw contact value.");
        result.Events.OfType<ContactChannelUpdated>().ShouldBeEmpty();
        result.Events.OfType<ContactChannelRemoved>().ShouldBeEmpty();
    }

    [Fact]
    public void Handle_UpdatePartyComposite_PersonDetailsOnOrganization_ReturnsTypeMismatch()
    {
        PartyState state = PartyTestData.CreateOrganizationStateWithChannelsAndIdentifiers();
        OrganizationDetails? originalOrganization = state.Organization;
        string originalDisplayName = state.DisplayName;
        string originalSortName = state.SortName;
        UpdatePartyComposite command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            PersonDetails = new PersonDetails { FirstName = "John", LastName = "Doe" },
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, state);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<PartyTypeMismatch>();
        result.Events.OfType<PersonDetailsUpdated>().ShouldBeEmpty();
        result.Events.OfType<PartyDisplayNameDerived>().ShouldBeEmpty();
        result.UpdatedPartyDetail.ShouldBeNull();
        result.Applied.ShouldBeEmpty();
        result.Skipped.ShouldBeEmpty();
        result.Rejected.ShouldContain("Cannot update person details on a Organization party.");
        state.Organization.ShouldBe(originalOrganization);
        state.Person.ShouldBeNull();
        state.DisplayName.ShouldBe(originalDisplayName);
        state.SortName.ShouldBe(originalSortName);
    }

    [Fact]
    public void Handle_UpdatePartyComposite_OrganizationDetailsOnPerson_ReturnsTypeMismatch()
    {
        PartyState state = PartyTestData.CreatePersonStateWithChannelsAndIdentifiers();
        PersonDetails? originalPerson = state.Person;
        string originalDisplayName = state.DisplayName;
        string originalSortName = state.SortName;
        UpdatePartyComposite command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            OrganizationDetails = new OrganizationDetails { LegalName = "Acme", TradingName = "Acme" },
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, state);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<PartyTypeMismatch>();
        result.Events.OfType<OrganizationDetailsUpdated>().ShouldBeEmpty();
        result.Events.OfType<PartyDisplayNameDerived>().ShouldBeEmpty();
        result.UpdatedPartyDetail.ShouldBeNull();
        result.Applied.ShouldBeEmpty();
        result.Skipped.ShouldBeEmpty();
        result.Rejected.ShouldContain("Cannot update organization details on a Person party.");
        state.Person.ShouldBe(originalPerson);
        state.Organization.ShouldBeNull();
        state.DisplayName.ShouldBe(originalDisplayName);
        state.SortName.ShouldBe(originalSortName);
    }

    [Theory]
    [InlineData("AddContactChannels")]
    [InlineData("UpdateContactChannels")]
    [InlineData("RemoveContactChannelIds")]
    [InlineData("AddIdentifiers")]
    [InlineData("RemoveIdentifierIds")]
    public void Handle_UpdatePartyComposite_BlankIdInList_ReturnsRejection(string listType)
    {
        PartyState state = PartyTestData.CreatePersonStateWithChannelsAndIdentifiers();
        UpdatePartyComposite command = listType switch
        {
            "AddContactChannels" => new()
            {
                PartyId = PartyTestData.DefaultPartyId,
                AddContactChannels =
                [
                    new AddContactChannel
                    {
                        PartyId = PartyTestData.DefaultPartyId,
                        ContactChannelId = " ",
                        Type = ContactChannelType.Email,
                        Value = "blank@example.com",
                    },
                ],
            },
            "UpdateContactChannels" => new()
            {
                PartyId = PartyTestData.DefaultPartyId,
                UpdateContactChannels =
                [
                    new UpdateContactChannel
                    {
                        PartyId = PartyTestData.DefaultPartyId,
                        ContactChannelId = " ",
                        Value = "blank@example.com",
                    },
                ],
            },
            "RemoveContactChannelIds" => new()
            {
                PartyId = PartyTestData.DefaultPartyId,
                RemoveContactChannelIds = [" "],
            },
            "AddIdentifiers" => new()
            {
                PartyId = PartyTestData.DefaultPartyId,
                AddIdentifiers =
                [
                    new AddIdentifier
                    {
                        PartyId = PartyTestData.DefaultPartyId,
                        IdentifierId = " ",
                        Type = IdentifierType.VAT,
                        Value = "synthetic-vat-value",
                    },
                ],
            },
            "RemoveIdentifierIds" => new()
            {
                PartyId = PartyTestData.DefaultPartyId,
                RemoveIdentifierIds = [" "],
            },
            _ => throw new InvalidOperationException(),
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, state);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<CompositeOperationConflict>();
        result.Applied.ShouldBeEmpty();

        string expectedMessage = listType.Contains("Identifier")
            ? "Identifier ID is required."
            : "Contact channel ID is required.";
        result.Rejected.ShouldContain(expectedMessage);
    }

    [Fact]
    public void Handle_UpdatePartyComposite_PayloadExactlyAtLimit_Succeeds()
    {
        int previous = PartyAggregate.MaxSubOperations;
        PartyAggregate.MaxSubOperations = 1;

        try
        {
            PartyState state = PartyTestData.CreatePersonStateWithChannelsAndIdentifiers();
            UpdatePartyComposite command = new()
            {
                PartyId = PartyTestData.DefaultPartyId,
                RemoveContactChannelIds = ["ch-email-2"],
            };

            CompositeCommandResult result = PartyAggregate.Handle(command, state);

            result.IsSuccess.ShouldBeTrue();
            result.Events.Count.ShouldBe(1);
            result.Events[0].ShouldBeOfType<ContactChannelRemoved>();
        }
        finally
        {
            PartyAggregate.MaxSubOperations = previous;
        }
    }

    [Fact]
    public void Handle_UpdatePartyComposite_MaxSubOperationsResetFromZero_UsesDefault()
    {
        int previous = PartyAggregate.MaxSubOperations;
        PartyAggregate.MaxSubOperations = 0;

        try
        {
            PartyState state = PartyTestData.CreatePersonStateWithChannelsAndIdentifiers();
            UpdatePartyComposite command = new()
            {
                PartyId = PartyTestData.DefaultPartyId,
                RemoveContactChannelIds = ["ch-email-2"],
            };

            CompositeCommandResult result = PartyAggregate.Handle(command, state);

            result.IsSuccess.ShouldBeTrue();
            result.Events.Count.ShouldBe(1);
        }
        finally
        {
            PartyAggregate.MaxSubOperations = previous;
        }
    }

    [Fact]
    public void Handle_UpdatePartyComposite_UpdateChannelToPreferred_EmitsPreferredChanged()
    {
        PartyState state = PartyTestData.CreatePersonStateWithChannelsAndIdentifiers();
        UpdatePartyComposite command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            UpdateContactChannels =
            [
                new UpdateContactChannel
                {
                    PartyId = PartyTestData.DefaultPartyId,
                    ContactChannelId = "ch-email-2",
                    IsPreferred = true,
                },
            ],
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, state);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(2);
        result.Events[0].ShouldBeOfType<ContactChannelUpdated>();
        result.Events[1].ShouldBeOfType<PreferredContactChannelChanged>();
        result.Applied.ShouldContain("Updated contact channel: ch-email-2");
        result.Applied.ShouldContain("Set preferred contact channel: ch-email-2");
    }

    [Fact]
    public void Handle_UpdatePartyComposite_UpdateChannelAlreadyPreferred_NoPreferredChangedEvent()
    {
        PartyState state = PartyTestData.CreatePersonStateWithChannelsAndIdentifiers();
        UpdatePartyComposite command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            UpdateContactChannels =
            [
                new UpdateContactChannel
                {
                    PartyId = PartyTestData.DefaultPartyId,
                    ContactChannelId = "ch-email-1",
                    IsPreferred = true,
                },
            ],
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, state);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<ContactChannelUpdated>();
        result.Applied.ShouldContain("Updated contact channel: ch-email-1");
        result.Applied.Any(x => x.Contains("Set preferred")).ShouldBeFalse();
    }

    [Fact]
    public void Handle_UpdatePartyComposite_AddChannelPreferred_EmitsPreferredChanged()
    {
        PartyState state = PartyTestData.CreatePersonStateWithChannelsAndIdentifiers();
        UpdatePartyComposite command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            AddContactChannels =
            [
                new AddContactChannel
                {
                    PartyId = PartyTestData.DefaultPartyId,
                    ContactChannelId = "ch-phone-pref",
                    Type = ContactChannelType.Phone,
                    Value = "+33111111111",
                    IsPreferred = true,
                },
            ],
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, state);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(2);
        result.Events[0].ShouldBeOfType<ContactChannelAdded>();
        result.Events[1].ShouldBeOfType<PreferredContactChannelChanged>();
        result.Applied.ShouldContain("Added contact channel: ch-phone-pref (Phone)");
        result.Applied.ShouldContain("Set preferred contact channel: ch-phone-pref");
    }

    [Fact]
    public void Handle_UpdatePartyComposite_UpdateChannelTypeChangeToPreferred_EmitsPreferredChanged()
    {
        PartyState state = PartyTestData.CreatePersonStateWithChannelsAndIdentifiers();
        UpdatePartyComposite command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            UpdateContactChannels =
            [
                new UpdateContactChannel
                {
                    PartyId = PartyTestData.DefaultPartyId,
                    ContactChannelId = "ch-email-1",
                    Type = ContactChannelType.Phone,
                    IsPreferred = true,
                },
            ],
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, state);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(2);
        result.Events[0].ShouldBeOfType<ContactChannelUpdated>();
        result.Events[1].ShouldBeOfType<PreferredContactChannelChanged>();
        result.Applied.ShouldContain("Set preferred contact channel: ch-email-1");
    }

    [Fact]
    public void Handle_UpdatePartyComposite_UpdateChannelNullableFieldsPreserved_EventHasNullValues()
    {
        PartyState state = PartyTestData.CreatePersonStateWithChannelsAndIdentifiers();
        UpdatePartyComposite command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            UpdateContactChannels =
            [
                new UpdateContactChannel
                {
                    PartyId = PartyTestData.DefaultPartyId,
                    ContactChannelId = "ch-email-1",
                    Type = null,
                    Value = null,
                    IsPreferred = null,
                },
            ],
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, state);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        ContactChannelUpdated updated = result.Events[0].ShouldBeOfType<ContactChannelUpdated>();
        updated.ContactChannelId.ShouldBe("ch-email-1");
        updated.Type.ShouldBeNull();
        updated.Value.ShouldBeNull();
        updated.IsPreferred.ShouldBeNull();
    }

    [Fact]
    public void Handle_UpdatePartyComposite_BothPersonAndOrgDetails_RejectsTypeMismatch()
    {
        PartyState state = PartyTestData.CreatePersonStateWithChannelsAndIdentifiers();
        UpdatePartyComposite command = new()
        {
            PartyId = PartyTestData.DefaultPartyId,
            PersonDetails = new PersonDetails { FirstName = "Jane", LastName = "Smith" },
            OrganizationDetails = new OrganizationDetails { LegalName = "Acme", TradingName = "Acme" },
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, state);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<PartyTypeMismatch>();
        result.Rejected.ShouldContain("Cannot update organization details on a Person party.");
        result.Applied.ShouldBeEmpty();
    }

    private static PartyState BuildExistingPersonState()
    {
        PartyState state = PartyTestData.CreatePersonState();
        state.Apply(new ContactChannelAdded
        {
            ContactChannelId = "email-1",
            Type = ContactChannelType.Email,
            Value = "john@example.com",
            IsPreferred = true,
        });
        state.Apply(new ContactChannelAdded
        {
            ContactChannelId = "email-2",
            Type = ContactChannelType.Email,
            Value = "john.alt@example.com",
        });
        state.Apply(new IdentifierAdded
        {
            IdentifierId = "id-vat-1",
            Type = IdentifierType.VAT,
            Value = "synthetic-vat-value",
        });

        return state;
    }
}
