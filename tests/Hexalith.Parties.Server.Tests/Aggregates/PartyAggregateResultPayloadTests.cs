using Hexalith.EventStore.Contracts.Results;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Results;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Server.Aggregates;
using Hexalith.Parties.Testing;

using Shouldly;

namespace Hexalith.Parties.Server.Tests.Aggregates;

public sealed class PartyAggregateResultPayloadTests
{
    [Fact]
    public void Handle_UpdatePersonDetails_ReturnsFinalUpdatedPartyDetail()
    {
        DomainResult result = PartyAggregate.Handle(
            PartyTestData.ValidUpdatePersonDetails(),
            PartyTestData.CreatePersonState());

        PartyDetail detail = result.ShouldBeOfType<PartyCommandResult>().UpdatedPartyDetail;
        detail.PersonDetails.ShouldNotBeNull();
        detail.PersonDetails.FirstName.ShouldBe("Jane");
        detail.PersonDetails.LastName.ShouldBe("Smith");
        detail.DisplayName.ShouldBe("Jane Smith");
        detail.SortName.ShouldBe("Smith, Jane");
    }

    [Fact]
    public void Handle_AddContactChannel_ReturnsDetailWithPreferredChannelApplied()
    {
        var command = new AddContactChannel
        {
            PartyId = PartyTestData.DefaultPartyId,
            ContactChannelId = "ch-phone-1",
            Type = ContactChannelType.Phone,
            Value = "+33123456789",
            IsPreferred = true,
        };

        DomainResult result = PartyAggregate.Handle(command, PartyTestData.CreatePersonStateWithChannelsAndIdentifiers());

        PartyDetail detail = result.ShouldBeOfType<PartyCommandResult>().UpdatedPartyDetail;
        ContactChannel channel = detail.ContactChannels.Single(c => c.Id == "ch-phone-1");
        channel.Value.ShouldBe("+33123456789");
        channel.IsPreferred.ShouldBeTrue();
    }

    [Fact]
    public void Handle_RemoveIdentifier_ReturnsDetailWithoutRemovedIdentifier()
    {
        DomainResult result = PartyAggregate.Handle(
            PartyTestData.ValidRemoveIdentifier(),
            PartyTestData.CreatePersonStateWithIdentifier());

        PartyDetail detail = result.ShouldBeOfType<PartyCommandResult>().UpdatedPartyDetail;
        detail.Identifiers.ShouldNotContain(identifier => identifier.Id == "id-vat-1");
    }

    [Fact]
    public void Handle_DeactivateParty_ReturnsInactivePartyDetail()
    {
        DomainResult result = PartyAggregate.Handle(
            PartyTestData.ValidDeactivateParty(),
            PartyTestData.CreatePersonState());

        PartyDetail detail = result.ShouldBeOfType<PartyCommandResult>().UpdatedPartyDetail;
        detail.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Handle_SetIsNaturalPerson_ReturnsOrganizationDetailWithNaturalPersonFlag()
    {
        DomainResult result = PartyAggregate.Handle(
            PartyTestData.ValidSetIsNaturalPerson(),
            PartyTestData.CreateOrganizationState());

        PartyDetail detail = result.ShouldBeOfType<PartyCommandResult>().UpdatedPartyDetail;
        detail.OrganizationDetails.ShouldNotBeNull();
        detail.OrganizationDetails.IsNaturalPerson.ShouldBeTrue();
    }

    [Fact]
    public void Handle_AddDuplicateIdentifierNoOp_DoesNotReturnResultPayload()
    {
        DomainResult result = PartyAggregate.Handle(
            PartyTestData.ValidAddVatIdentifier(),
            PartyTestData.CreatePersonStateWithIdentifier());

        result.IsNoOp.ShouldBeTrue();
        result.ResultPayload.ShouldBeNull();
    }
}
