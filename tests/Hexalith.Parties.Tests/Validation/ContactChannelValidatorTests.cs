using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Validation;

using Shouldly;

namespace Hexalith.Parties.Tests.Validation;

public sealed class ContactChannelValidatorTests
{
    [Fact]
    public void AddContactChannel_InvalidContactChannelId_ReturnsValidationFailure()
    {
        AddContactChannel command = new()
        {
            PartyId = Guid.NewGuid().ToString("D"),
            ContactChannelId = "not-a-guid",
            Type = ContactChannelType.Email,
            Value = "person@example.com",
        };

        var result = new AddContactChannelValidator().Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.PropertyName).ShouldContain(nameof(AddContactChannel.ContactChannelId));
        result.Errors.Select(e => e.ErrorMessage).ShouldContain("ContactChannelId must be a valid GUID.");
    }

    [Fact]
    public void UpdateContactChannel_InvalidContactChannelId_ReturnsValidationFailure()
    {
        UpdateContactChannel command = new()
        {
            PartyId = Guid.NewGuid().ToString("D"),
            ContactChannelId = "not-a-guid",
            Value = "person@example.com",
        };

        var result = new UpdateContactChannelValidator().Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.PropertyName).ShouldContain(nameof(UpdateContactChannel.ContactChannelId));
        result.Errors.Select(e => e.ErrorMessage).ShouldContain("ContactChannelId must be a valid GUID.");
    }

    [Fact]
    public void RemoveContactChannel_InvalidContactChannelId_ReturnsValidationFailure()
    {
        RemoveContactChannel command = new()
        {
            PartyId = Guid.NewGuid().ToString("D"),
            ContactChannelId = "not-a-guid",
        };

        var result = new RemoveContactChannelValidator().Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.PropertyName).ShouldContain(nameof(RemoveContactChannel.ContactChannelId));
        result.Errors.Select(e => e.ErrorMessage).ShouldContain("ContactChannelId must be a valid GUID.");
    }
}
