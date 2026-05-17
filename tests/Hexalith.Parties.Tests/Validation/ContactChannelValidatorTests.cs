using FluentValidation.Results;

using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Validation;

using Shouldly;

namespace Hexalith.Parties.Tests.Validation;

public sealed class ContactChannelValidatorTests
{
    [Fact]
    public void AddContactChannel_ValidGuidContactChannelId_PassesValidation()
    {
        AddContactChannel command = new()
        {
            PartyId = Guid.NewGuid().ToString("D"),
            ContactChannelId = Guid.NewGuid().ToString("D"),
            Type = ContactChannelType.Email,
            Value = "person@example.com",
        };

        ValidationResult result = new AddContactChannelValidator().Validate(command);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void AddContactChannel_InvalidContactChannelId_ReturnsSingleGuidFailure()
    {
        AddContactChannel command = new()
        {
            PartyId = Guid.NewGuid().ToString("D"),
            ContactChannelId = "not-a-guid",
            Type = ContactChannelType.Email,
            Value = "person@example.com",
        };

        ValidationResult result = new AddContactChannelValidator().Validate(command);

        result.IsValid.ShouldBeFalse();
        ValidationFailure[] channelErrors = result.Errors
            .Where(e => e.PropertyName == nameof(AddContactChannel.ContactChannelId))
            .ToArray();
        channelErrors.Length.ShouldBe(1);
        channelErrors[0].ErrorMessage.ShouldBe("ContactChannelId must be a valid GUID.");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void AddContactChannel_EmptyContactChannelId_ReturnsSingleRequiredFailure(string? id)
    {
        AddContactChannel command = new()
        {
            PartyId = Guid.NewGuid().ToString("D"),
            ContactChannelId = id!,
            Type = ContactChannelType.Email,
            Value = "person@example.com",
        };

        ValidationResult result = new AddContactChannelValidator().Validate(command);

        result.IsValid.ShouldBeFalse();
        ValidationFailure[] channelErrors = result.Errors
            .Where(e => e.PropertyName == nameof(AddContactChannel.ContactChannelId))
            .ToArray();
        channelErrors.Length.ShouldBe(1);
        channelErrors[0].ErrorMessage.ShouldBe("ContactChannelId is required.");
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("ch-email-1")]
    public void UpdateContactChannel_NonGuidContactChannelId_PassesValidation(string id)
    {
        UpdateContactChannel command = new()
        {
            PartyId = Guid.NewGuid().ToString("D"),
            ContactChannelId = id,
            Value = "person@example.com",
        };

        ValidationResult result = new UpdateContactChannelValidator().Validate(command);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void UpdateContactChannel_EmptyContactChannelId_ReturnsSingleRequiredFailure(string? id)
    {
        UpdateContactChannel command = new()
        {
            PartyId = Guid.NewGuid().ToString("D"),
            ContactChannelId = id!,
            Value = "person@example.com",
        };

        ValidationResult result = new UpdateContactChannelValidator().Validate(command);

        result.IsValid.ShouldBeFalse();
        ValidationFailure[] channelErrors = result.Errors
            .Where(e => e.PropertyName == nameof(UpdateContactChannel.ContactChannelId))
            .ToArray();
        channelErrors.Length.ShouldBe(1);
        channelErrors[0].ErrorMessage.ShouldBe("ContactChannelId is required.");
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("ch-email-1")]
    public void RemoveContactChannel_NonGuidContactChannelId_PassesValidation(string id)
    {
        RemoveContactChannel command = new()
        {
            PartyId = Guid.NewGuid().ToString("D"),
            ContactChannelId = id,
        };

        ValidationResult result = new RemoveContactChannelValidator().Validate(command);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void RemoveContactChannel_EmptyContactChannelId_ReturnsSingleRequiredFailure(string? id)
    {
        RemoveContactChannel command = new()
        {
            PartyId = Guid.NewGuid().ToString("D"),
            ContactChannelId = id!,
        };

        ValidationResult result = new RemoveContactChannelValidator().Validate(command);

        result.IsValid.ShouldBeFalse();
        ValidationFailure[] channelErrors = result.Errors
            .Where(e => e.PropertyName == nameof(RemoveContactChannel.ContactChannelId))
            .ToArray();
        channelErrors.Length.ShouldBe(1);
        channelErrors[0].ErrorMessage.ShouldBe("ContactChannelId is required.");
    }
}
