using FluentValidation.Results;

using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Validation;

using Shouldly;

namespace Hexalith.Parties.Tests.Validation;

public sealed class RevokeConsentValidatorTests
{
    [Theory]
    [InlineData("consent-1")]
    [InlineData("ch-email-1:marketing")]
    [InlineData("a1b2c3d4-e5f6-7890-abcd-ef1234567890:billing")]
    [InlineData("{a1b2c3d4-e5f6-7890-abcd-ef1234567890}:billing")]
    public void Validate_CompatibleConsentId_Passes(string consentId)
    {
        ValidationResult result = new RevokeConsentValidator().Validate(CreateCommand(consentId));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("consent/unsafe")]
    [InlineData("channel:purpose:extra")]
    [InlineData("channel:pur pose")]
    public void Validate_UnsafeConsentId_ReturnsBoundedFailure(string consentId)
    {
        ArgumentNullException.ThrowIfNull(consentId);
        ValidationResult result = new RevokeConsentValidator().Validate(CreateCommand(consentId));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(error => error.PropertyName == nameof(RevokeConsent.ConsentId));
        result.Errors.ShouldAllBe(error => error.ErrorMessage.Length <= 128);
        if (consentId.Length > 0)
        {
            result.Errors.ShouldAllBe(error => !error.ErrorMessage.Contains(consentId, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Validate_MissingPartyAndTenant_ReturnsRequiredFailures()
    {
        RevokeConsent command = CreateCommand("consent-1") with
        {
            PartyId = string.Empty,
            TenantId = string.Empty,
        };

        ValidationResult result = new RevokeConsentValidator().Validate(command);

        result.Errors.Select(error => error.PropertyName).ShouldContain(nameof(RevokeConsent.PartyId));
        result.Errors.Select(error => error.PropertyName).ShouldContain(nameof(RevokeConsent.TenantId));
    }

    private static RevokeConsent CreateCommand(string consentId)
        => new()
        {
            PartyId = "party-1",
            TenantId = "tenant-a",
            ConsentId = consentId,
        };
}
