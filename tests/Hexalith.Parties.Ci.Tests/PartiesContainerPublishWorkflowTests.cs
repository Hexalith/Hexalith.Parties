using System.Diagnostics;
using System.Text.Json;

namespace Hexalith.Parties.Ci.Tests;

public sealed class PartiesContainerPublishWorkflowTests
{
    private const int ExpectedPackageCount = 9;
    private static readonly TimeSpan PublicationPreflightTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public void CiWorkflowDelegatesToSharedDomainCiWithPartiesTestLanes()
    {
        string workflow = CiTestPaths.ReadRepoFile(".github/workflows/ci.yml");

        workflow.ShouldContain("Hexalith/Hexalith.Builds/.github/workflows/domain-ci.yml@main");
        workflow.ShouldContain("solution: Hexalith.Parties.slnx");
        workflow.ShouldContain("test-platform: microsoft-testing-platform");
        workflow.ShouldContain("run-consumer-validation: true");
        workflow.ShouldContain("run-coverage-gate: false");
        workflow.ShouldContain("tests/Hexalith.Parties.Contracts.Tests");
        workflow.ShouldContain("tests/Hexalith.Parties.Authentication.Tests");
        workflow.ShouldContain("tests/Hexalith.Parties.Client.Tests");
        workflow.ShouldContain("tests/Hexalith.Parties.Server.Tests");
        workflow.ShouldContain("tests/Hexalith.Parties.Projections.Tests");
        workflow.ShouldContain("tests/Hexalith.Parties.Security.Tests");
        workflow.ShouldContain("tests/Hexalith.Parties.AdminPortal.Tests");
        workflow.ShouldContain("tests/Hexalith.Parties.ConsumerPortal.Tests");
        workflow.ShouldContain("tests/Hexalith.Parties.UI.Tests");
        workflow.ShouldContain("tests/Hexalith.Parties.Picker.Tests");
        workflow.ShouldContain("tests/Hexalith.Parties.Mcp.Tests");
        workflow.ShouldContain("tests/Hexalith.Parties.Tests");
        workflow.ShouldContain("tests/Hexalith.Parties.Sample.Tests");
        workflow.ShouldContain("tests/Hexalith.Parties.Ci.Tests");
        workflow.ShouldContain("aspire-test-project: tests/Hexalith.Parties.IntegrationTests");
        workflow.ShouldNotContain("submodules: recursive");

        string sharedWorkflow = CiTestPaths.ReadRepoFile("references/Hexalith.Builds/.github/workflows/domain-ci.yml");
        sharedWorkflow.ShouldContain("default: 'vstest'");
        sharedWorkflow.ShouldContain("inputs.test-platform == 'microsoft-testing-platform'");
        sharedWorkflow.ShouldContain("--report-xunit-trx");
        sharedWorkflow.ShouldContain("--filter-not-trait");
        sharedWorkflow.ShouldContain("--filter-trait");
    }

    [Fact]
    public void ReleaseWorkflowPublishesOnlyPartiesContainersThroughSharedDomainRelease()
    {
        string workflow = CiTestPaths.ReadRepoFile(".github/workflows/release.yml");

        workflow.ShouldContain("Hexalith/Hexalith.Builds/.github/workflows/domain-release.yml@main");
        workflow.ShouldContain("test-platform: microsoft-testing-platform");
        workflow.ShouldContain("publish-containers: true");
        workflow.ShouldContain("src/Hexalith.Parties/Hexalith.Parties.csproj|parties");
        workflow.ShouldContain("src/Hexalith.Parties.Mcp/Hexalith.Parties.Mcp.csproj|parties-mcp");
        workflow.ShouldContain("src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj|parties-ui");
        workflow.ShouldContain("secrets: inherit");
        workflow.ShouldContain("tests/Hexalith.Parties.Ci.Tests");
        workflow.ShouldNotContain("eventstore-admin");
        workflow.ShouldNotContain("sample-blazor-ui");
        workflow.ShouldNotContain("|tenants");
        workflow.ShouldNotContain("|memories");
        workflow.ShouldNotContain(":latest");

        string sharedWorkflow = CiTestPaths.ReadRepoFile("references/Hexalith.Builds/.github/workflows/domain-release.yml");
        sharedWorkflow.ShouldContain("default: 'vstest'");
        sharedWorkflow.ShouldContain("inputs.test-platform == 'microsoft-testing-platform'");
        sharedWorkflow.ShouldContain("--report-xunit-trx");
    }

    [Fact]
    public void ReleaseSupportFilesDeclareSemanticReleaseAndSecretContracts()
    {
        string packageJson = CiTestPaths.ReadRepoFile("package.json");
        string releaseConfig = CiTestPaths.ReadRepoFile("release.config.cjs");
        string secretCheck = CiTestPaths.ReadRepoFile("scripts/validate-release-secrets.sh");
        string publicationPreflight = CiTestPaths.ReadRepoFile("scripts/validate-publication-preflight.sh");

        packageJson.ShouldContain("\"semantic-release\"");
        packageJson.ShouldContain("\"@commitlint/cli\"");
        releaseConfig.ShouldContain("verifyReleaseCmd");
        releaseConfig.ShouldContain("scripts/pack-release-packages.py");
        releaseConfig.ShouldContain("scripts/validate-nuget-packages.py");
        releaseConfig.ShouldContain("scripts/validate-consumer-package-references.py");
        releaseConfig.ShouldContain("scripts/validate-publication-preflight.sh ${nextRelease.version} verify");
        releaseConfig.ShouldContain("scripts/validate-publication-preflight.sh ${nextRelease.version} publish");
        releaseConfig.ShouldContain("dotnet nuget push \"./nupkgs/Hexalith.Parties.*.nupkg\"");
        releaseConfig.ShouldContain("./.hexalith/release/publish-containers.sh");
        releaseConfig.ShouldNotContain("--skip-duplicate");
        publicationPreflight.ShouldContain("readonly expected_package_count=9");
        publicationPreflight.ShouldContain("--container-repository \"registry.hexalith.com/parties\"");
        publicationPreflight.ShouldContain("--container-repository \"registry.hexalith.com/parties-mcp\"");
        publicationPreflight.ShouldContain("--container-repository \"registry.hexalith.com/parties-ui\"");
        secretCheck.ShouldContain("NUGET_API_KEY");
        secretCheck.ShouldContain("HEXALITH_ZOT_USERNAME");
        secretCheck.ShouldContain("HEXALITH_ZOT_API_KEY");
        secretCheck.ShouldNotContain("ZOT_REGISTRY_PASSWORD");
    }

    [Fact]
    public void ReleasePackageManifestDeclaresExactNinePackageInventory()
    {
        using JsonDocument manifest = JsonDocument.Parse(CiTestPaths.ReadRepoFile("tools/release-packages.json"));
        JsonElement[] packages = manifest.RootElement.GetProperty("packages").EnumerateArray().ToArray();

        packages.Length.ShouldBe(ExpectedPackageCount);
        packages.Select(package => package.GetProperty("id").GetString()).ShouldBe(
        [
            "Hexalith.Parties.Contracts",
            "Hexalith.Parties.Client",
            "Hexalith.Parties.AdminPortal",
            "Hexalith.Parties.ConsumerPortal",
            "Hexalith.Parties.Picker",
            "Hexalith.Parties.Authentication",
            "Hexalith.Parties.Projections",
            "Hexalith.Parties.Security",
            "Hexalith.Parties.Testing",
        ]);
        packages.Select(package => package.GetProperty("project").GetString()).ShouldBe(
        [
            "src/Hexalith.Parties.Contracts/Hexalith.Parties.Contracts.csproj",
            "src/Hexalith.Parties.Client/Hexalith.Parties.Client.csproj",
            "src/Hexalith.Parties.AdminPortal/Hexalith.Parties.AdminPortal.csproj",
            "src/Hexalith.Parties.ConsumerPortal/Hexalith.Parties.ConsumerPortal.csproj",
            "src/Hexalith.Parties.Picker/Hexalith.Parties.Picker.csproj",
            "src/Hexalith.Parties.Authentication/Hexalith.Parties.Authentication.csproj",
            "src/Hexalith.Parties.Projections/Hexalith.Parties.Projections.csproj",
            "src/Hexalith.Parties.Security/Hexalith.Parties.Security.csproj",
            "src/Hexalith.Parties.Testing/Hexalith.Parties.Testing.csproj",
        ]);
    }

    [Theory]
    [InlineData("verify")]
    [InlineData("publish")]
    public void PublicationPreflightWrapperForwardsExactPackageAndContainerSet(string phase)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        (int exitCode, string error, bool preflightInvoked, string[] arguments) =
            RunPublicationPreflightWrapper(phase, ExpectedPackageCount.ToString());

        exitCode.ShouldBe(0, error);
        preflightInvoked.ShouldBeTrue();
        arguments.Where(argument => argument == "--container-repository").Count().ShouldBe(3);
        ArgumentValues(arguments, "--container-repository").ShouldBe(
        [
            "registry.hexalith.com/parties",
            "registry.hexalith.com/parties-mcp",
            "registry.hexalith.com/parties-ui",
        ]);
        ArgumentValues(arguments, "--expected-package-count").ShouldBe(["9"]);
        ArgumentValues(arguments, "--phase").ShouldBe([phase]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("8")]
    [InlineData("10")]
    public void PublicationPreflightWrapperRejectsPackageCountDriftBeforeSharedPreflight(string? packageCount)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        (int exitCode, string error, bool preflightInvoked, string[] _) =
            RunPublicationPreflightWrapper("verify", packageCount);

        exitCode.ShouldNotBe(0);
        error.ShouldContain("expected-package-count input must be exactly 9");
        preflightInvoked.ShouldBeFalse();
    }

    [Fact]
    public void CommitlintAndDependabotUseReleaseCompatibleCommitContracts()
    {
        string commitlintConfig = CiTestPaths.ReadRepoFile("commitlint.config.mjs");
        string commitlint = CiTestPaths.ReadRepoFile(".github/workflows/commitlint.yml");
        string dependabot = CiTestPaths.ReadRepoFile(".github/dependabot.yml");

        commitlintConfig.ShouldContain("'body-max-line-length': [2, 'always', 200]");
        commitlintConfig.ShouldContain("'header-max-length': [2, 'always', 200]");
        commitlint.ShouldContain("types: [opened, synchronize, reopened, edited]");
        commitlint.ShouldContain("push:");
        commitlint.ShouldContain("pull-request-title: ${{ github.event.pull_request.title || '' }}");
        dependabot.ShouldContain("prefix: \"build(deps)\"");
        dependabot.ShouldNotContain("prefix: \"chore(deps)\"");
    }

    [Fact]
    public void CiDocsDescribeSharedCiReleaseAndZotApiKeyPublishContract()
    {
        string ci = CiTestPaths.ReadRepoFile("docs/ci.md");
        string secrets = CiTestPaths.ReadRepoFile("docs/ci-secrets-checklist.md");

        ci.ShouldContain("Hexalith/Hexalith.Builds/.github/workflows/domain-ci.yml@main");
        ci.ShouldContain("Hexalith/Hexalith.Builds/.github/workflows/domain-release.yml@");
        ci.ShouldContain("workflow_dispatch");
        ci.ShouldContain("EventStore 3.88.0");
        ci.ShouldContain("registry.hexalith.com/parties");
        ci.ShouldContain("registry.hexalith.com/parties-mcp");
        ci.ShouldContain("registry.hexalith.com/parties-ui");
        ci.ShouldContain("does not apply runtime deployment manifests");
        secrets.ShouldContain("NUGET_API_KEY");
        secrets.ShouldContain("HEXALITH_ZOT_USERNAME");
        secrets.ShouldContain("HEXALITH_ZOT_API_KEY");
        secrets.ShouldContain("Zot API key");
        secrets.ShouldNotContain("ZOT_REGISTRY_PASSWORD");
    }

    private static string[] ArgumentValues(string[] arguments, string option)
    {
        List<string> values = [];
        for (int index = 0; index < arguments.Length; index++)
        {
            if (arguments[index] == option)
            {
                (index + 1).ShouldBeLessThan(arguments.Length);
                values.Add(arguments[++index]);
            }
        }

        return [.. values];
    }

    private static (int ExitCode, string Error, bool PreflightInvoked, string[] Arguments)
        RunPublicationPreflightWrapper(string phase, string? workflowPackageCount)
    {
        string temporary = Path.Combine(Path.GetTempPath(), $"hexalith-parties-preflight-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
            string invocationMarker = Path.Combine(temporary, "preflight-invoked");
            string argumentsPath = Path.Combine(temporary, "preflight-arguments");
            string recordingPreflight = Path.Combine(temporary, "record-preflight.sh");
            File.WriteAllText(
                recordingPreflight,
                "#!/usr/bin/env bash\n" +
                "set -euo pipefail\n" +
                ": > \"$PREFLIGHT_INVOCATION_MARKER\"\n" +
                "printf '%s\\n' \"$@\" > \"$PREFLIGHT_ARGUMENTS\"\n");
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(
                    recordingPreflight,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            ProcessStartInfo start = new("bash")
            {
                WorkingDirectory = CiTestPaths.RepositoryRoot,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            start.ArgumentList.Add(CiTestPaths.RepoFile("scripts/validate-publication-preflight.sh"));
            start.ArgumentList.Add("99.0.0");
            start.ArgumentList.Add(phase);
            start.Environment["HEXALITH_BUILDS_EXECUTION_SHA"] = new string('a', 40);
            start.Environment["HEXALITH_RELEASE_ENVIRONMENT"] = "production";
            start.Environment["HEXALITH_RELEASE_SOURCE_BRANCH"] = "main";
            start.Environment["HEXALITH_RELEASE_SOURCE_CI_WORKFLOW"] = "ci.yml";
            start.Environment["HEXALITH_RELEASE_PACKAGE_MANIFEST"] = "tools/release-packages.json";
            start.Environment["GITHUB_SHA"] = new string('b', 40);
            start.Environment["HEXALITH_PUBLICATION_PREFLIGHT"] = recordingPreflight;
            start.Environment["HEXALITH_ZOT_REGISTRY"] = "registry.hexalith.com";
            start.Environment["PREFLIGHT_INVOCATION_MARKER"] = invocationMarker;
            start.Environment["PREFLIGHT_ARGUMENTS"] = argumentsPath;
            start.Environment.Remove("HEXALITH_RELEASE_EXPECTED_PACKAGE_COUNT");
            if (workflowPackageCount is not null)
            {
                start.Environment["HEXALITH_RELEASE_EXPECTED_PACKAGE_COUNT"] = workflowPackageCount;
            }

            using Process process = new() { StartInfo = start };
            process.Start().ShouldBeTrue("Could not start the publication preflight wrapper.");
            process.StandardOutput.ReadToEnd();
            Task<string> errorTask = process.StandardError.ReadToEndAsync();
            bool exited = process.WaitForExit((int)PublicationPreflightTimeout.TotalMilliseconds);
            if (!exited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }

            string error = errorTask.GetAwaiter().GetResult();
            exited.ShouldBeTrue($"Publication preflight wrapper timed out: {error}");
            string[] arguments = File.Exists(argumentsPath) ? File.ReadAllLines(argumentsPath) : [];
            return (process.ExitCode, error, File.Exists(invocationMarker), arguments);
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }
}
