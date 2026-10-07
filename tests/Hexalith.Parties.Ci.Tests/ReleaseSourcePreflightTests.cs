using System.Diagnostics;
using System.Text.Json;

namespace Hexalith.Parties.Ci.Tests;

/// <summary>
/// Executes the unprotected release source gate with isolated GitHub API fixtures.
/// </summary>
public sealed class ReleaseSourcePreflightTests
{
    private const string SourceSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string OtherSha = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    /// <summary>
    /// Verifies invalid dispatches and incomplete push evidence fail before the protected job.
    /// </summary>
    /// <param name="scenario">The invalid source or proof fixture.</param>
    [Theory]
    [InlineData("non-main")]
    [InlineData("invalid-dispatch")]
    [InlineData("invalid-live-main")]
    [InlineData("stale-main")]
    [InlineData("missing-ci")]
    [InlineData("wrong-ci-sha")]
    [InlineData("wrong-ci-branch")]
    [InlineData("wrong-ci-event")]
    [InlineData("failed-ci")]
    [InlineData("running-ci")]
    [InlineData("malformed-ci")]
    [InlineData("array-ci-response")]
    [InlineData("object-ci-runs")]
    [InlineData("missing-ci-id")]
    [InlineData("null-ci-id")]
    [InlineData("zero-ci-id")]
    [InlineData("negative-ci-id")]
    [InlineData("fractional-ci-id")]
    [InlineData("string-ci-id")]
    [InlineData("boolean-ci-id")]
    [InlineData("api-failure")]
    [InlineData("ci-api-failure")]
    public void SourceGateRejectsInvalidDispatchOrIncompletePushEvidence(string scenario)
    {
        (int exitCode, string error, string[] requests, string outputs) = RunSourceGate(scenario);

        exitCode.ShouldNotBe(0, scenario);
        error.ShouldNotBeNullOrWhiteSpace();
        string expectedFailure = scenario switch
        {
            "non-main" => "Release must be dispatched from refs/heads/main.",
            "invalid-dispatch" => "The dispatch source must be an exact lowercase commit SHA.",
            "invalid-live-main" => "The live main SHA could not be resolved safely.",
            "stale-main" => "The dispatched source is no longer the live main tip.",
            "api-failure" => "Fixture GitHub API failure.",
            "ci-api-failure" => "Fixture CI-runs API failure.",
            _ => "No successful push ci.yml run exists for the exact current main SHA.",
        };
        error.ShouldContain(expectedFailure);
        outputs.ShouldBeEmpty();
        if (scenario is "non-main" or "invalid-dispatch")
        {
            requests.ShouldBeEmpty("Invalid dispatch identity must fail before any GitHub API request.");
        }

        if (scenario is "invalid-live-main" or "stale-main")
        {
            requests.ShouldNotContain("repos/Hexalith/Hexalith.Parties/actions/workflows/ci.yml/runs");
        }

        if (scenario == "ci-api-failure")
        {
            int mainLookup = Array.IndexOf(requests, "repos/Hexalith/Hexalith.Parties/git/ref/heads/main");
            int ciLookup = Array.IndexOf(requests, "repos/Hexalith/Hexalith.Parties/actions/workflows/ci.yml/runs");
            mainLookup.ShouldBeGreaterThanOrEqualTo(0);
            ciLookup.ShouldBeGreaterThan(mainLookup);
        }
    }

    /// <summary>
    /// Verifies full CI proof must bind the exact current main SHA and a completed push.
    /// </summary>
    [Fact]
    public void SourceGateAcceptsOnlySuccessfulExactCurrentMainPush()
    {
        (int exitCode, string error, string[] requests, string outputs) = RunSourceGate("green");

        exitCode.ShouldBe(0, error);
        requests.ShouldContain("repos/Hexalith/Hexalith.Parties/git/ref/heads/main");
        requests.ShouldContain("repos/Hexalith/Hexalith.Parties/actions/workflows/ci.yml/runs");
        requests.ShouldContain($"head_sha={SourceSha}");
        requests.ShouldContain("branch=main");
        requests.ShouldContain("event=push");
        requests.ShouldContain("status=success");
        outputs.ShouldBeEmpty("The source-proof workflow is fixed and must not be selected through an output.");
    }

    /// <summary>
    /// Verifies legacy bypass values and successful Commitlint cannot replace missing full CI proof.
    /// </summary>
    /// <param name="legacyBypassValidation">A value supplied through the retired bypass environment.</param>
    [Theory]
    [InlineData("")]
    [InlineData("true")]
    [InlineData("TRUE")]
    [InlineData("unknown")]
    public void LegacyBypassValuesCannotSubstituteCommitlintForFullCi(string legacyBypassValidation)
    {
        (int exitCode, string error, string[] requests, string outputs) = RunSourceGate("missing-ci", legacyBypassValidation);

        exitCode.ShouldNotBe(0);
        error.ShouldContain("No successful push ci.yml run exists for the exact current main SHA.");
        requests.ShouldContain("repos/Hexalith/Hexalith.Parties/actions/workflows/ci.yml/runs");
        requests.ShouldNotContain("repos/Hexalith/Hexalith.Parties/actions/workflows/commitlint.yml/runs");
        outputs.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies the source proof runs without protected credentials and precedes approval.
    /// </summary>
    [Fact]
    public void SourceGateRunsOutsideTheProtectedReleaseEnvironment()
    {
        string workflow = CiTestPaths.ReadRepoFile(".github/workflows/release.yml").Replace("\r\n", "\n");
        int sourceStart = workflow.IndexOf("  verify-source:\n", StringComparison.Ordinal);
        int releaseStart = workflow.IndexOf("  release:\n", StringComparison.Ordinal);
        sourceStart.ShouldBeGreaterThan(0);
        releaseStart.ShouldBeGreaterThan(sourceStart);
        string source = workflow[sourceStart..releaseStart];
        string release = workflow[releaseStart..];

        source.ShouldContain("      actions: read\n      contents: read\n");
        source.ShouldNotContain("environment:");
        source.ShouldNotContain("${{ secrets.");
        release.ShouldContain("    needs: verify-source\n");
        release.ShouldContain("    environment: production\n");
    }

    /// <summary>
    /// Extracts and runs the tracked caller's actual source proof, without publication commands.
    /// </summary>
    /// <param name="scenario">The GitHub API fixture scenario.</param>
    /// <param name="legacyBypassValidation">Optional retired bypass environment fixture.</param>
    /// <returns>The gate exit code, stderr, API arguments, and workflow outputs.</returns>
    private static (int ExitCode, string Error, string[] Requests, string Outputs) RunSourceGate(
        string scenario,
        string? legacyBypassValidation = null)
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("The release source gate requires bash and jq.");
        }

        string workflow = CiTestPaths.ReadRepoFile(".github/workflows/release.yml").Replace("\r\n", "\n");
        string step = workflow.Split("      - name: ", StringSplitOptions.None)
            .Single(candidate => candidate.StartsWith("Require current main with ", StringComparison.Ordinal));
        string script = step.Split("        run: |\n", StringSplitOptions.None)[1];
        script = string.Join('\n', script.Split('\n').Select(line =>
            line.StartsWith("          ", StringComparison.Ordinal) ? line[10..] : line));

        string temporary = Path.Combine(Path.GetTempPath(), $"hexalith-release-source-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
            string requestLog = Path.Combine(temporary, "requests");
            string outputPath = Path.Combine(temporary, "outputs");
            string fakeGh = Path.Combine(temporary, "gh");
            File.WriteAllText(fakeGh, """
                #!/usr/bin/env bash
                set -euo pipefail
                printf '%s\n' "$@" >> "$FAKE_REQUEST_LOG"
                if [ "$FAKE_SCENARIO" = api-failure ]; then
                  echo 'Fixture GitHub API failure.' >&2
                  exit 1
                fi
                for argument in "$@"; do
                  case "$argument" in
                    */git/ref/heads/main)
                      printf '%s\n' "$FAKE_LIVE_MAIN_SHA"
                      exit 0
                      ;;
                    */actions/workflows/ci.yml/runs)
                      if [ "$FAKE_SCENARIO" = ci-api-failure ]; then
                        echo 'Fixture CI-runs API failure.' >&2
                        exit 1
                      fi
                      printf '%s\n' "$FAKE_CI_RUNS"
                      exit 0
                      ;;
                    */actions/workflows/commitlint.yml/runs)
                      printf '%s\n' "$FAKE_COMMITLINT_RUNS"
                      exit 0
                      ;;
                  esac
                done
                echo 'Unexpected fixture GitHub API request.' >&2
                exit 1
                """.Replace("\r\n", "\n") + "\n");
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(fakeGh, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            Dictionary<string, object> run = new()
            {
                ["id"] = 123456789,
                ["head_sha"] = scenario == "wrong-ci-sha" ? OtherSha : SourceSha,
                ["head_branch"] = scenario == "wrong-ci-branch" ? "other" : "main",
                ["event"] = scenario == "wrong-ci-event" ? "pull_request" : "push",
                ["status"] = scenario == "running-ci" ? "in_progress" : "completed",
                ["conclusion"] = scenario == "failed-ci" ? "failure" : "success",
            };
            if (scenario == "missing-ci-id")
            {
                run.Remove("id");
            }
            else
            {
                run["id"] = scenario switch
                {
                    "null-ci-id" => null!,
                    "zero-ci-id" => 0,
                    "negative-ci-id" => -1,
                    "fractional-ci-id" => 1.5,
                    "string-ci-id" => "123456789",
                    "boolean-ci-id" => true,
                    _ => 123456789,
                };
            }

            string ciRuns = scenario switch
            {
                "malformed-ci" => "{}",
                "array-ci-response" => JsonSerializer.Serialize(new[] { new { workflow_runs = new[] { run } } }),
                "object-ci-runs" => JsonSerializer.Serialize(new { workflow_runs = new { matching = run } }),
                _ => JsonSerializer.Serialize(new
                {
                    workflow_runs = scenario == "missing-ci" ? Array.Empty<Dictionary<string, object>>() : [run],
                }),
            };
            ProcessStartInfo start = new("bash")
            {
                WorkingDirectory = CiTestPaths.RepositoryRoot,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add(script);
            start.Environment["PATH"] = $"{temporary}{Path.PathSeparator}{start.Environment["PATH"]}";
            start.Environment.Remove("BYPASS_VALIDATION");
            if (legacyBypassValidation is not null)
            {
                start.Environment["BYPASS_VALIDATION"] = legacyBypassValidation;
            }
            start.Environment["GH_TOKEN"] = "fixture-token";
            start.Environment["REPOSITORY"] = "Hexalith/Hexalith.Parties";
            start.Environment["DISPATCH_REF"] = scenario == "non-main" ? "refs/heads/other" : "refs/heads/main";
            start.Environment["DISPATCH_SHA"] = scenario == "invalid-dispatch" ? "main" : SourceSha;
            start.Environment["GITHUB_OUTPUT"] = outputPath;
            start.Environment["FAKE_SCENARIO"] = scenario;
            start.Environment["FAKE_REQUEST_LOG"] = requestLog;
            start.Environment["FAKE_LIVE_MAIN_SHA"] = scenario switch
            {
                "invalid-live-main" => "main",
                "stale-main" => OtherSha,
                _ => SourceSha,
            };
            start.Environment["FAKE_CI_RUNS"] = ciRuns;
            start.Environment["FAKE_COMMITLINT_RUNS"] = JsonSerializer.Serialize(new { workflow_runs = new[] { run } });

            using Process process = new() { StartInfo = start };
            process.Start().ShouldBeTrue();
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> error = process.StandardError.ReadToEndAsync();
            bool exited = process.WaitForExit(10_000);
            if (!exited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }

            string stderr = error.GetAwaiter().GetResult();
            output.GetAwaiter().GetResult();
            exited.ShouldBeTrue($"Release source gate timed out: {stderr}");
            return (
                process.ExitCode,
                stderr,
                File.Exists(requestLog) ? File.ReadAllLines(requestLog) : [],
                File.Exists(outputPath) ? File.ReadAllText(outputPath) : string.Empty);
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }
}
