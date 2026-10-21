using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hexalith.Parties.Ci.Tests;

/// <summary>Protects the accessibility lane's blocking CI and release-proof contract.</summary>
public sealed class AccessibilityWorkflowTests
{
    /// <summary>Requires every CI event to run the entire accessibility suite without a bypass.</summary>
    [Fact]
    public void AccessibilityIsAnUnconditionalBlockingCiJob()
    {
        string workflow = CiTestPaths.ReadRepoFile(".github/workflows/ci.yml").Replace("\r\n", "\n", StringComparison.Ordinal);
        string triggers = workflow[..workflow.IndexOf("\njobs:", StringComparison.Ordinal)];
        triggers.ShouldContain("on:\n  push:\n    branches: [main]\n  pull_request:\n    branches: [main]\n  schedule:");
        triggers.ShouldContain("  repository_dispatch:\n    types: [contract_requiring_verification_published]");
        Regex.IsMatch(triggers, @"^\s+paths(?:-ignore)?:", RegexOptions.Multiline).ShouldBeFalse();

        string job = ReadAccessibilityJob();

        job.ShouldNotContain("continue-on-error:");
        job.ShouldNotContain("needs:");
        Regex.IsMatch(job, @"^    if:", RegexOptions.Multiline).ShouldBeFalse();
        job.ShouldContain("timeout-minutes: 20");
        job.ShouldContain("working-directory: tests/e2e");
        job.ShouldContain("CI: 'true'");
        job.ShouldContain("BASE_URL: http://127.0.0.1:5072");
        job.ShouldContain("PLAYWRIGHT_SKIP_WEBSERVER: ''");

        ReadAccessibilityStep(job, "Run accessibility gate").Trim().ShouldBe("run: npm run test:a11y");

        using JsonDocument package = JsonDocument.Parse(CiTestPaths.ReadRepoFile("tests/e2e/package.json"));
        package.RootElement.GetProperty("scripts").GetProperty("test:a11y").GetString()
            .ShouldBe("playwright test specs/parties-accessibility.spec.ts --project=chromium");
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

    /// <summary>Requires locked tools, browser installation and the published Release UI before testing.</summary>
    [Fact]
    public void AccessibilityBuildsThePackageModeUiBeforeRunningTheBrowser()
    {
        string job = ReadAccessibilityJob();

        job.ShouldContain("submodules: false");
        job.ShouldContain("git -c submodule.recurse=false submodule update --init references/Hexalith.Builds references/Hexalith.Commons");
        job.ShouldNotContain("--recursive");
        job.ShouldContain("global-json-file: global.json");
        job.ShouldContain("node-version: '24'");
        job.ShouldContain("cache-dependency-path: tests/e2e/package-lock.json");
        job.ShouldNotContain("NuGetAudit=false");
        job.ShouldNotContain("TreatWarningsAsErrors=false");

        string[] commands =
        [
            "run: npm ci\n",
            "run: npm run test:config\n",
            "run: npm run typecheck\n",
            "run: python3 ../Hexalith.Parties.UI.Tests/verify-restored-assets.py\n",
            "run: npm run install:browsers\n",
            "run: dotnet build ../../src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj --configuration Release -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true\n",
            "run: npm run test:a11y\n",
        ];
        int previous = -1;
        foreach (string command in commands)
        {
            int position = job.IndexOf(command, StringComparison.Ordinal);
            position.ShouldBeGreaterThan(previous, $"Missing or out-of-order command: {command}");
            previous = position;
        }
    }

    /// <summary>Retains reports, traces and failure screenshots regardless of the test outcome.</summary>
    [Fact]
    public void AccessibilityEvidenceUploadsAfterFailure()
    {
        string job = ReadAccessibilityJob();
        string step = ReadAccessibilityStep(job, "Upload accessibility evidence");
        Regex.IsMatch(step, @"^        if: always\(\)$", RegexOptions.Multiline).ShouldBeTrue();
        step.ShouldContain("uses: actions/upload-artifact@");
        step.ShouldContain("name: ui-a11y-results");
        step.ShouldContain("tests/e2e/playwright-report");
        step.ShouldContain("tests/e2e/test-results");
        step.ShouldContain("retention-days: 14");
    }

    private static string ReadAccessibilityStep(string job, string name)
    {
        Match match = Regex.Match(job, $@"^      - name: {Regex.Escape(name)}\n(?<step>.*?)(?=^      - |\z)", RegexOptions.Multiline | RegexOptions.Singleline);
        match.Success.ShouldBeTrue($"Missing accessibility step: {name}");
        return match.Groups["step"].Value;
    }

    private static string ReadAccessibilityJob()
    {
        string workflow = CiTestPaths.ReadRepoFile(".github/workflows/ci.yml").Replace("\r\n", "\n", StringComparison.Ordinal);
        Match match = Regex.Match(workflow, @"^  ui-a11y:\n(?<job>.*?)(?=^  [\w-]+:\n|\z)", RegexOptions.Multiline | RegexOptions.Singleline);
        match.Success.ShouldBeTrue("The accessibility gate must remain in the exact-source CI workflow.");
        return match.Groups["job"].Value;
    }
}
