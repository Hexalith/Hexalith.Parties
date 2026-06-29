using Hexalith.Parties.Contracts;

using Shouldly;

namespace Hexalith.Parties.Contracts.Tests;

public sealed class PartyExportFileNameTests
{
    [Fact]
    public void Build_UsesCanonicalUtcTimestampFormat()
    {
        string actual = PartyExportFileName.Build(
            "party-token",
            new DateTimeOffset(2026, 05, 21, 22, 45, 00, TimeSpan.FromHours(2)));

        actual.ShouldBe("party-party-token-20260521T204500Z.json");
        actual.ShouldNotContain("-export-20260521");
    }

    [Fact]
    public void Build_SanitizesInvalidCharactersAndBoundsToken()
    {
        string actual = PartyExportFileName.Build(
            "party/with:unsafe*characters?and-a-very-long-token-that-must-be-truncated-before-download",
            DateTimeOffset.Parse("2026-06-10T00:00:00Z"));

        actual.ShouldBe("party-party-with-unsafe-characters-and-a-very-long-token-that-must-be-20260610T000000Z.json");
        actual.ShouldNotContain('/');
        actual.ShouldNotContain(':');
        actual.ShouldNotContain('*');
        actual.ShouldNotContain('?');
    }

    [Fact]
    public void Build_DoesNotAcceptBlankTokens()
    {
        Should.Throw<ArgumentException>(() => PartyExportFileName.Build(" ", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Build_WhenTokenContainsOnlyInvalidCharacters_UsesSafeFallback()
    {
        string actual = PartyExportFileName.Build("///", DateTimeOffset.Parse("2026-06-10T00:00:00Z"));

        actual.ShouldBe("party-party-20260610T000000Z.json");
    }
}
