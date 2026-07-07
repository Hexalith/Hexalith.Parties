using FluentValidation.Results;

using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Validation;

using Shouldly;

namespace Hexalith.Parties.Tests.Validation;

public sealed class ContactChannelValidatorTests
{
    private const string UlidPartyId = "01HYX7QS3NP8M4KQJR5A7CVWKM";
    private const string LegacyGuidContactChannelId = "a1b2c3d4-e5f6-7890-abcd-ef1234567890";

    [Theory]
    [InlineData("ch-email-1")]
    [InlineData(LegacyGuidContactChannelId)]
    public void AddContactChannel_SupportSafeContactChannelId_PassesValidation(string contactChannelId)
    {
        AddContactChannel command = new()
        {
            PartyId = UlidPartyId,
            ContactChannelId = contactChannelId,
            Type = ContactChannelType.Email,
            Value = "person@example.com",
        };

        ValidationResult result = new AddContactChannelValidator().Validate(command);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void AddContactChannel_UnsafeContactChannelId_ReturnsSingleSupportSafeFailure()
    {
        AddContactChannel command = new()
        {
            PartyId = UlidPartyId,
            ContactChannelId = "contact/unsafe",
            Type = ContactChannelType.Email,
            Value = "person@example.com",
        };

        ValidationResult result = new AddContactChannelValidator().Validate(command);

        result.IsValid.ShouldBeFalse();
        ValidationFailure[] channelErrors = result.Errors
            .Where(e => e.PropertyName == nameof(AddContactChannel.ContactChannelId))
            .ToArray();
        channelErrors.Length.ShouldBe(1);
        channelErrors[0].ErrorMessage.ShouldBe("ContactChannelId must be a support-safe identifier.");
        channelErrors[0].ErrorMessage.ShouldNotContain("contact/unsafe");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void AddContactChannel_EmptyContactChannelId_ReturnsSingleRequiredFailure(string? id)
    {
        AddContactChannel command = new()
        {
            PartyId = UlidPartyId,
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
    [InlineData("ch-email-1")]
    [InlineData(LegacyGuidContactChannelId)]
    public void UpdateContactChannel_SupportSafeContactChannelId_PassesValidation(string id)
    {
        UpdateContactChannel command = new()
        {
            PartyId = UlidPartyId,
            ContactChannelId = id,
            Value = "person@example.com",
        };

        ValidationResult result = new UpdateContactChannelValidator().Validate(command);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void UpdateContactChannel_UnsafeContactChannelId_ReturnsSingleSupportSafeFailure()
    {
        UpdateContactChannel command = new()
        {
            PartyId = UlidPartyId,
            ContactChannelId = "contact:unsafe",
            Value = "person@example.com",
        };

        ValidationResult result = new UpdateContactChannelValidator().Validate(command);

        result.IsValid.ShouldBeFalse();
        ValidationFailure[] channelErrors = result.Errors
            .Where(e => e.PropertyName == nameof(UpdateContactChannel.ContactChannelId))
            .ToArray();
        channelErrors.Length.ShouldBe(1);
        channelErrors[0].ErrorMessage.ShouldBe("ContactChannelId must be a support-safe identifier.");
        channelErrors[0].ErrorMessage.ShouldNotContain("contact:unsafe");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void UpdateContactChannel_EmptyContactChannelId_ReturnsSingleRequiredFailure(string? id)
    {
        UpdateContactChannel command = new()
        {
            PartyId = UlidPartyId,
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
    [InlineData("ch-email-1")]
    [InlineData(LegacyGuidContactChannelId)]
    public void RemoveContactChannel_SupportSafeContactChannelId_PassesValidation(string id)
    {
        RemoveContactChannel command = new()
        {
            PartyId = UlidPartyId,
            ContactChannelId = id,
        };

        ValidationResult result = new RemoveContactChannelValidator().Validate(command);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void RemoveContactChannel_UnsafeContactChannelId_ReturnsSingleSupportSafeFailure()
    {
        RemoveContactChannel command = new()
        {
            PartyId = UlidPartyId,
            ContactChannelId = "contact unsafe",
        };

        ValidationResult result = new RemoveContactChannelValidator().Validate(command);

        result.IsValid.ShouldBeFalse();
        ValidationFailure[] channelErrors = result.Errors
            .Where(e => e.PropertyName == nameof(RemoveContactChannel.ContactChannelId))
            .ToArray();
        channelErrors.Length.ShouldBe(1);
        channelErrors[0].ErrorMessage.ShouldBe("ContactChannelId must be a support-safe identifier.");
        channelErrors[0].ErrorMessage.ShouldNotContain("contact unsafe");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void RemoveContactChannel_EmptyContactChannelId_ReturnsSingleRequiredFailure(string? id)
    {
        RemoveContactChannel command = new()
        {
            PartyId = UlidPartyId,
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
