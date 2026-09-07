using FluentValidation.Results;

using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Validation;

using Shouldly;

namespace Hexalith.Parties.Tests.Validation;

public sealed class RecordConsentValidatorTests
{
    [Theory]
    [InlineData("ch-email-1", "marketing")]
    [InlineData("a1b2c3d4-e5f6-7890-abcd-ef1234567890", " billing_1 ")]
    [InlineData("{a1b2c3d4-e5f6-7890-abcd-ef1234567890}", "PURPOSE-1")]
    public void Validate_CompatibleIdentifiersAndPurpose_Passes(string channelId, string purpose)
    {
        ValidationResult result = new RecordConsentValidator().Validate(CreateCommand(channelId, purpose));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("channel/unsafe")]
    [InlineData("channel:purpose")]
    [InlineData("channel unsafe")]
    public void Validate_UnsafeChannelId_ReturnsBoundedFailure(string channelId)
    {
        ArgumentNullException.ThrowIfNull(channelId);
        ValidationResult result = new RecordConsentValidator().Validate(CreateCommand(channelId, "marketing"));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(error => error.PropertyName == nameof(RecordConsent.ChannelId));
        result.Errors.ShouldAllBe(error => error.ErrorMessage.Length <= 128);
        if (channelId.Length > 0)
        {
            result.Errors.ShouldAllBe(error => !error.ErrorMessage.Contains(channelId, StringComparison.Ordinal));
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("purpose with spaces")]
    [InlineData("purpose/path")]
    public void Validate_UnsafePurpose_ReturnsBoundedFailure(string purpose)
    {
        ArgumentNullException.ThrowIfNull(purpose);
        ValidationResult result = new RecordConsentValidator().Validate(CreateCommand("ch-email-1", purpose));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(error => error.PropertyName == nameof(RecordConsent.Purpose));
        result.Errors.ShouldAllBe(error => error.ErrorMessage.Length <= 128);
        if (purpose.Length > 0)
        {
            result.Errors.ShouldAllBe(error => !error.ErrorMessage.Contains(purpose, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Validate_MissingPartyAndTenant_ReturnsRequiredFailures()
    {
        RecordConsent command = CreateCommand("ch-email-1", "marketing") with
        {
            PartyId = string.Empty,
            TenantId = string.Empty,
        };

        ValidationResult result = new RecordConsentValidator().Validate(command);

        result.Errors.Select(error => error.PropertyName).ShouldContain(nameof(RecordConsent.PartyId));
        result.Errors.Select(error => error.PropertyName).ShouldContain(nameof(RecordConsent.TenantId));
    }

    private static RecordConsent CreateCommand(string channelId, string purpose)
        => new()
        {
            PartyId = "party-1",
            TenantId = "tenant-a",
            ChannelId = channelId,
            Purpose = purpose,
            LawfulBasis = LawfulBasis.Consent,
        };
}
