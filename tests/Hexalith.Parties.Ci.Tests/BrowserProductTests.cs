using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hexalith.Parties.Ci.Tests;

/// <summary>Checks the required browser scenarios and their product fixture.</summary>
public sealed class BrowserProductTests
{
    /// <summary>Preserves the complete accessibility, portal and picker browser suite.</summary>
    [Fact]
    public void BrowserScriptsIncludeEveryRequiredProductScenario()
    {
        using JsonDocument package = JsonDocument.Parse(CiTestPaths.ReadRepoFile("tests/e2e/package.json"));
        JsonElement scripts = package.RootElement.GetProperty("scripts");
        scripts.GetProperty("test:a11y").GetString()
            .ShouldBe("playwright test specs/parties-accessibility.spec.ts --project=chromium");
        const string portalSpecs = "specs/admin-parties-list.spec.ts specs/consumer-portal-routes.spec.ts specs/party-picker.spec.ts specs/shared-portal-display-formatters.spec.ts";
        scripts.GetProperty("test:portals").GetString()
            .ShouldBe($"playwright test {portalSpecs} --project=chromium");
        scripts.GetProperty("test:ui").GetString()
            .ShouldBe($"playwright test specs/parties-accessibility.spec.ts {portalSpecs} --project=chromium");
    }

    /// <summary>Keeps every required portal and picker check independent of optional environment flags.</summary>
    [Theory]
    [InlineData("admin-parties-list.spec.ts")]
    [InlineData("consumer-portal-routes.spec.ts")]
    [InlineData("party-picker.spec.ts")]
    [InlineData("shared-portal-display-formatters.spec.ts")]
    public void PortalBrowserRegressionsRemainRequired(string spec)
    {
        string source = CiTestPaths.ReadRepoFile($"tests/e2e/specs/{spec}");
        source.ShouldNotContain("test.skip");
        source.ShouldNotContain("test.fixme");
        source.ShouldNotContain("test.describe.skip");
        source.ShouldNotContain("test.describe.fixme");
        source.ShouldNotContain("PARTIES_E2E_");
    }

    /// <summary>Requires the isolated CI fixture to launch prebuilt Release assets with its Test-only opt-ins.</summary>
    [Fact]
    public void AccessibilityCiHostUsesThePrebuiltReleaseAndTestFixture()
    {
        string config = CiTestPaths.ReadRepoFile("tests/e2e/playwright.config.ts").Replace("\r\n", "\n", StringComparison.Ordinal);
        Match command = Regex.Match(config, @"command: IS_CI\s+\? '(?<command>[^']+)'");
        command.Success.ShouldBeTrue();
        string ciCommand = command.Groups["command"].Value;
        ciCommand.ShouldContain("--configuration Release --no-build --no-restore --no-launch-profile");
        ciCommand.ShouldContain("--urls http://127.0.0.1:5072");
        ciCommand.ShouldNotContain("UseHexalithProjectReferences=true");
        ciCommand.ShouldNotContain("NuGetAudit=false");

        config.ShouldContain("reuseExistingServer: false");
        config.ShouldContain("ASPNETCORE_ENVIRONMENT: 'Test'");
        config.ShouldContain("Hexalith__Parties__AccessibilitySpecimens__Enabled: 'true'");
        config.ShouldContain("Hexalith__Parties__AdminPortalE2E__Enabled: 'true'");
    }
}
