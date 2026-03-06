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
        CreatePartyComposite command = BuildValidPersonComposite();

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
    public void Handle_CreatePartyComposite_DuplicateContactChannelIds_SkipsDuplicate()
    {
        CreatePartyComposite command = BuildValidPersonComposite() with
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
        CreatePartyComposite command = BuildValidPersonComposite();

        CompositeCommandResult result = PartyAggregate.Handle(command, null);

        result.Applied.ShouldContain("Derived display name");
        result.Applied.Any(x => x.Contains("John") || x.Contains("Doe")).ShouldBeFalse();
    }

    [Fact]
    public void Handle_CreatePartyComposite_InvalidContactChannelId_ReturnsRejectionWithoutSuccessEvents()
    {
        CreatePartyComposite command = BuildValidPersonComposite() with
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
        CreatePartyComposite command = BuildValidPersonComposite() with
        {
            ContactChannels = [],
            Identifiers =
            [
                new AddIdentifier
                {
                    PartyId = PartyTestData.DefaultPartyId,
                    IdentifierId = string.Empty,
                    Type = IdentifierType.VAT,
                    Value = "FR12345678901",
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
            CreatePartyComposite command = BuildValidPersonComposite() with
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
    public void Handle_UpdatePartyComposite_PersonDetailsChannelsAndIdentifiers_EmitsExpectedEvents()
    {
        PartyState state = BuildExistingPersonState();
        UpdatePartyComposite command = BuildValidUpdatePersonComposite();

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
        result.Applied.ShouldContain("Added contact channel: phone-1 (Phone)");
        result.Applied.ShouldContain("Updated contact channel: email-1");
        result.Applied.ShouldContain("Removed contact channel: email-2");
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
                    Value = "12345678901234",
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
        result.Events[0].ShouldBeOfType<ContactChannelNotFound>();
        result.Rejected.ShouldContain("Contact channel 'missing-channel' not found.");
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
        result.Events[0].ShouldBeOfType<CompositeOperationConflict>();
        result.Rejected.ShouldContain("Conflicting operations on same channel ID: email-3.");
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
                    Value = "12345678901234",
                },
            ],
            RemoveIdentifierIds = ["id-siret-1"],
        };

        CompositeCommandResult result = PartyAggregate.Handle(command, state);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<CompositeOperationConflict>();
        result.Rejected.ShouldContain("Conflicting operations on same identifier ID: id-siret-1.");
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
            UpdatePartyComposite command = BuildValidUpdatePersonComposite();

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
        CompositeCommandResult result = PartyAggregate.Handle(BuildValidUpdatePersonComposite(), null);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<PartyNotFound>();
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

    private static CreatePartyComposite BuildValidPersonComposite() => new()
    {
        PartyId = PartyTestData.DefaultPartyId,
        Type = PartyType.Person,
        PersonDetails = PartyTestData.ValidPersonDetails(),
        ContactChannels =
        [
            new AddContactChannel
            {
                PartyId = PartyTestData.DefaultPartyId,
                ContactChannelId = "ch-email-1",
                Type = ContactChannelType.Email,
                Value = "john@example.com",
            },
            new AddContactChannel
            {
                PartyId = PartyTestData.DefaultPartyId,
                ContactChannelId = "ch-email-2",
                Type = ContactChannelType.Email,
                Value = "john.alt@example.com",
            },
        ],
        Identifiers =
        [
            new AddIdentifier
            {
                PartyId = PartyTestData.DefaultPartyId,
                IdentifierId = "id-vat-1",
                Type = IdentifierType.VAT,
                Value = "FR12345678901",
            },
        ],
    };

    private static UpdatePartyComposite BuildValidUpdatePersonComposite() => new()
    {
        PartyId = PartyTestData.DefaultPartyId,
        PersonDetails = new PersonDetails
        {
            FirstName = "Jane",
            LastName = "Smith",
        },
        AddContactChannels =
        [
            new AddContactChannel
            {
                PartyId = PartyTestData.DefaultPartyId,
                ContactChannelId = "phone-1",
                Type = ContactChannelType.Phone,
                Value = "+33123456789",
            },
        ],
        UpdateContactChannels =
        [
            new UpdateContactChannel
            {
                PartyId = PartyTestData.DefaultPartyId,
                ContactChannelId = "email-1",
                Value = "jane@example.com",
            },
        ],
        RemoveContactChannelIds = ["email-2"],
        AddIdentifiers =
        [
            new AddIdentifier
            {
                PartyId = PartyTestData.DefaultPartyId,
                IdentifierId = "id-siret-1",
                Type = IdentifierType.SIRET,
                Value = "12345678901234",
            },
        ],
    };

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
            Value = "FR12345678901",
        });

        return state;
    }
}
