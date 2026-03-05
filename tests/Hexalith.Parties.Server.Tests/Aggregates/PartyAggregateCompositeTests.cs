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
}
