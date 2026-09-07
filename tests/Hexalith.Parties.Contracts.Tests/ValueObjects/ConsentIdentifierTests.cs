using Hexalith.Parties.Contracts.ValueObjects;

using Shouldly;

namespace Hexalith.Parties.Contracts.Tests.ValueObjects;

public sealed class ConsentIdentifierTests
{
    [Theory]
    [InlineData("ch-email-1")]
    [InlineData("a1b2c3d4-e5f6-7890-abcd-ef1234567890")]
    [InlineData("{a1b2c3d4-e5f6-7890-abcd-ef1234567890}")]
    [InlineData("{0xa1b2c3d4,0xe5f6,0x7890,{0xab,0xcd,0xef,0x12,0x34,0x56,0x78,0x90}}")]
    public void IsValidChannelId_CompatiblePartyIdentifier_ReturnsTrue(string channelId)
    {
        ConsentIdentifier.IsValidChannelId(channelId).ShouldBeTrue();
        ConsentIdentifier.IsValidChannelId(channelId).ShouldBe(PartyIdentifier.IsValid(channelId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("channel/unsafe")]
    [InlineData("channel unsafe")]
    [InlineData("ch-email-1:marketing")]
    [InlineData("-channel")]
    public void IsValidChannelId_UnsafeIdentifier_ReturnsFalse(string? channelId)
    {
        ConsentIdentifier.IsValidChannelId(channelId).ShouldBeFalse();
        ConsentIdentifier.IsValidChannelId(channelId).ShouldBe(PartyIdentifier.IsValid(channelId));
    }

    [Theory]
    [InlineData("consent-1")]
    [InlineData("legacy.consent_1")]
    [InlineData("01HYX7QS3NP8M4KQJR5A7CVWKM")]
    [InlineData("ch-email-1:marketing")]
    [InlineData("a1b2c3d4-e5f6-7890-abcd-ef1234567890:billing_1")]
    [InlineData("{a1b2c3d4-e5f6-7890-abcd-ef1234567890}:PURPOSE-1")]
    [InlineData("{0xa1b2c3d4,0xe5f6,0x7890,{0xab,0xcd,0xef,0x12,0x34,0x56,0x78,0x90}}:legal")]
    public void IsValidConsentId_CompatibleOpaqueOrCompositeIdentifier_ReturnsTrue(string consentId)
    {
        ConsentIdentifier.IsValidConsentId(consentId).ShouldBeTrue();
    }

    [Fact]
    public void IsValidConsentId_ExactOpaqueAndCompositeBounds_ReturnExpectedResults()
    {
        string maximumOpaqueId = $"a{new string('b', PartyIdentifier.MaximumSemanticIdLength - 2)}c";
        string oversizedOpaqueId = $"a{new string('b', PartyIdentifier.MaximumSemanticIdLength - 1)}c";
        string maximumChannelId = maximumOpaqueId;
        string maximumPurpose = new('p', 100);
        string oversizedPurpose = new('p', 101);

        ConsentIdentifier.IsValidConsentId(maximumOpaqueId).ShouldBeTrue();
        ConsentIdentifier.IsValidConsentId(oversizedOpaqueId).ShouldBeFalse();
        ConsentIdentifier.IsValidPurpose(maximumPurpose).ShouldBeTrue();
        ConsentIdentifier.IsValidPurpose(oversizedPurpose).ShouldBeFalse();
        ConsentIdentifier.IsValidConsentId($"{maximumChannelId}:{maximumPurpose}").ShouldBeTrue();
        ConsentIdentifier.IsValidConsentId($"{oversizedOpaqueId}:purpose").ShouldBeFalse();
        ConsentIdentifier.IsValidConsentId($"channel:{oversizedPurpose}").ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("consent/unsafe")]
    [InlineData("consent\\unsafe")]
    [InlineData("consent unsafe")]
    [InlineData("consent\nunsafe")]
    [InlineData(":purpose")]
    [InlineData("channel:")]
    [InlineData("channel:purpose:extra")]
    [InlineData("channel:pur pose")]
    public void IsValidConsentId_UnsafeIdentifier_ReturnsFalse(string? consentId)
    {
        ConsentIdentifier.IsValidConsentId(consentId).ShouldBeFalse();
    }

    [Theory]
    [InlineData("marketing")]
    [InlineData("PURPOSE-1")]
    [InlineData("billing_2")]
    public void IsValidPurpose_CompatiblePurpose_ReturnsTrue(string purpose)
    {
        ConsentIdentifier.IsValidPurpose(purpose).ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("purpose with spaces")]
    [InlineData("purpose/path")]
    [InlineData("purpose:extra")]
    [InlineData("é")]
    public void IsValidPurpose_UnsafePurpose_ReturnsFalse(string? purpose)
    {
        ConsentIdentifier.IsValidPurpose(purpose).ShouldBeFalse();
    }
}
