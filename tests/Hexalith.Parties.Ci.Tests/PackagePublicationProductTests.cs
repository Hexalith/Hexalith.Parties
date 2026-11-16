using System.Diagnostics;

namespace Hexalith.Parties.Ci.Tests;

/// <summary>Preserves the declared package and image inventory at the legacy publisher boundary.</summary>
public sealed class PackagePublicationProductTests
{
    private const int ExpectedPackageCount = 11;
    private static readonly TimeSpan PublicationPreflightTimeout = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData("verify")]
    [InlineData("publish")]
    public void PublicationPreflightWrapperForwardsExactPackageAndContainerSet(string phase)
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Publication preflight wrapper tests require bash.");
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
        ArgumentValues(arguments, "--expected-package-count").ShouldBe(["11"]);
        ArgumentValues(arguments, "--phase").ShouldBe([phase]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("9")]
    [InlineData("10")]
    [InlineData("12")]
    public void PublicationPreflightWrapperRejectsPackageCountDriftBeforeSharedPreflight(string? packageCount)
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Publication preflight wrapper tests require bash.");
        }

        (int exitCode, string error, bool preflightInvoked, string[] _) =
            RunPublicationPreflightWrapper("verify", packageCount);

        exitCode.ShouldNotBe(0);
        error.ShouldContain("expected-package-count input must be exactly 11");
        preflightInvoked.ShouldBeFalse();
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
        RunPublicationPreflightWrapper(
            string phase,
            string? workflowPackageCount,
            string sourceCiWorkflow = "ci.yml")
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
            start.Environment["HEXALITH_RELEASE_SOURCE_CI_WORKFLOW"] = sourceCiWorkflow;
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
