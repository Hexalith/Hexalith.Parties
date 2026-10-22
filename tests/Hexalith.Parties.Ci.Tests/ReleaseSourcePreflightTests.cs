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
    [InlineData("wrong-ci-workflow")]
    [InlineData("api-failure")]
    [InlineData("ci-api-failure")]
    [InlineData("split-proof-ci")]
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
        error.ShouldContain(expectedFailure, customMessage: error);
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
    /// <param name="scenario">A matching run alone or among unrelated successful runs.</param>
    [Theory]
    [InlineData("green")]
    [InlineData("mixed-valid-ci")]
    [InlineData("mixed-valid-rc")]
    public void SourceGateAcceptsOnlySuccessfulExactCurrentMainPush(string scenario)
    {
        (int exitCode, string error, string[] requests, string outputs) = RunSourceGate(scenario);

        exitCode.ShouldBe(0, error);
        requests.ShouldContain("repos/Hexalith/Hexalith.Parties/git/ref/heads/main");
        requests.ShouldContain("repos/Hexalith/Hexalith.Parties/actions/workflows/ci.yml/runs");
        requests.ShouldContain("repos/Hexalith/Hexalith.Parties/actions/workflows/ci.yml");
        requests.ShouldContain("repos/Hexalith/Hexalith.Parties/actions/workflows/rc-gate.yml");
        requests.ShouldContain("repos/Hexalith/Hexalith.Parties/actions/workflows/rc-gate.yml/runs");
        requests.ShouldContain($"head_sha={SourceSha}");
        requests.ShouldContain("branch=main");
        requests.ShouldContain("event=push");
        requests.ShouldContain("status=success");
        outputs.ShouldBeEmpty("The source-proof workflow is fixed and must not be selected through an output.");
    }

    /// <summary>Successful CI cannot replace independent completed exact-source main-push range proof.</summary>
    /// <param name="scenario">The invalid range-proof response.</param>
    [Theory]
    [InlineData("missing-rc")]
    [InlineData("wrong-rc-sha")]
    [InlineData("wrong-rc-branch")]
    [InlineData("wrong-rc-event")]
    [InlineData("failed-rc")]
    [InlineData("cancelled-rc")]
    [InlineData("running-rc")]
    [InlineData("pending-rc")]
    [InlineData("malformed-rc")]
    [InlineData("array-rc-response")]
    [InlineData("object-rc-runs")]
    [InlineData("missing-rc-id")]
    [InlineData("null-rc-id")]
    [InlineData("zero-rc-id")]
    [InlineData("negative-rc-id")]
    [InlineData("fractional-rc-id")]
    [InlineData("string-rc-id")]
    [InlineData("boolean-rc-id")]
    [InlineData("wrong-rc-workflow")]
    [InlineData("split-proof-rc")]
    [InlineData("rc-api-failure")]
    public void SourceGateRequiresIndependentRangeProofDespiteGreenCi(string scenario)
    {
        (int exitCode, string error, string[] requests, string outputs) = RunSourceGate(scenario);

        exitCode.ShouldNotBe(0, scenario);
        error.ShouldContain(scenario == "rc-api-failure" ? "Fixture range-runs API failure."
            : "No successful push rc-gate.yml range proof exists for the exact current main SHA.", customMessage: error);
        requests.ShouldContain("repos/Hexalith/Hexalith.Parties/actions/workflows/ci.yml/runs");
        requests.ShouldContain("repos/Hexalith/Hexalith.Parties/actions/workflows/rc-gate.yml/runs");
        outputs.ShouldBeEmpty();
    }

    /// <summary>Workflow metadata binds the two independent run proofs to their fixed owners.</summary>
    /// <param name="scenario">The malformed, disabled, duplicated or unrelated workflow identity.</param>
    [Theory]
    [InlineData("malformed-ci-workflow")]
    [InlineData("wrong-ci-path")]
    [InlineData("disabled-ci-workflow")]
    [InlineData("invalid-ci-workflow-id")]
    [InlineData("malformed-rc-workflow")]
    [InlineData("wrong-rc-path")]
    [InlineData("disabled-rc-workflow")]
    [InlineData("invalid-rc-workflow-id")]
    [InlineData("duplicate-workflow-identities")]
    public void SourceGateRejectsUnvalidatedWorkflowIdentities(string scenario)
    {
        (int exitCode, string error, string[] requests, string outputs) = RunSourceGate(scenario);

        exitCode.ShouldNotBe(0, scenario);
        error.ShouldContain("independent CI and range-proof workflow identities could not be validated safely");
        requests.ShouldNotContain("repos/Hexalith/Hexalith.Parties/actions/workflows/ci.yml/runs");
        requests.ShouldNotContain("repos/Hexalith/Hexalith.Parties/actions/workflows/rc-gate.yml/runs");
        outputs.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies legacy bypass values and successful Commitlint cannot replace missing full CI proof.
    /// </summary>
    /// <param name="legacyBypassValidation">A value supplied through the retired bypass environment.</param>
    [Theory]
    [InlineData("")]
    [InlineData("false")]
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
                    */actions/workflows/ci.yml)
                      printf '%s\n' "$FAKE_CI_WORKFLOW"
                      exit 0
                      ;;
                    */actions/workflows/rc-gate.yml)
                      printf '%s\n' "$FAKE_RC_WORKFLOW"
                      exit 0
                      ;;
                    */actions/workflows/rc-gate.yml/runs)
                      if [ "$FAKE_SCENARIO" = rc-api-failure ]; then
                        echo 'Fixture range-runs API failure.' >&2
                        exit 1
                      fi
                      printf '%s\n' "$FAKE_RC_RUNS"
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

            string Runs(string proof, int workflowId)
            {
                string selected = scenario.Replace($"-{proof}", "-proof", StringComparison.Ordinal);
                Dictionary<string, object> run = new()
                {
                    ["id"] = proof == "ci" ? 123456789 : 234567890,
                    ["workflow_id"] = selected == "wrong-proof-workflow" ? 999 : workflowId,
                    ["head_sha"] = selected == "wrong-proof-sha" ? OtherSha : SourceSha,
                    ["head_branch"] = selected == "wrong-proof-branch" ? "other" : "main",
                    ["event"] = selected == "wrong-proof-event" ? "workflow_dispatch" : "push",
                    ["status"] = selected == "pending-proof" ? "queued" : selected == "running-proof" ? "in_progress" : "completed",
                    ["conclusion"] = selected == "failed-proof" ? "failure" : selected == "cancelled-proof" ? "cancelled"
                        : selected == "pending-proof" ? null! : "success",
                };
                if (selected == "missing-proof-id") { run.Remove("id"); }
                else
                {
                    run["id"] = selected switch
                    {
                        "null-proof-id" => null!,
                        "zero-proof-id" => 0,
                        "negative-proof-id" => -1,
                        "fractional-proof-id" => 1.5,
                        "string-proof-id" => "123456789",
                        "boolean-proof-id" => true,
                        _ => run["id"],
                    };
                }
                Dictionary<string, object> wrongShaRun = new(run) { ["head_sha"] = OtherSha };
                Dictionary<string, object> incompleteRun = new(run) { ["status"] = "in_progress" };
                Dictionary<string, object> failedRun = new(run) { ["conclusion"] = "failure" };
                return selected switch
                {
                    "malformed-proof" => "{}",
                    "mixed-valid-proof" => JsonSerializer.Serialize(new { workflow_runs = new[] { wrongShaRun, failedRun, run, incompleteRun } }),
                    "split-proof-proof" => JsonSerializer.Serialize(new { workflow_runs = new[] { wrongShaRun, incompleteRun } }),
                    "array-proof-response" => JsonSerializer.Serialize(new[] { new { workflow_runs = new[] { run } } }),
                    "object-proof-runs" => JsonSerializer.Serialize(new { workflow_runs = new { matching = run } }),
                    _ => JsonSerializer.Serialize(new
                    {
                        workflow_runs = selected == "missing-proof" ? Array.Empty<Dictionary<string, object>>() : [run],
                    }),
                };
            }
            string Workflow(string proof, int workflowId)
            {
                if (scenario == $"malformed-{proof}-workflow") { return "{}"; }
                return JsonSerializer.Serialize(new
                {
                    id = scenario == $"invalid-{proof}-workflow-id" ? 0
                        : scenario == "duplicate-workflow-identities" ? 101 : workflowId,
                    path = scenario == $"wrong-{proof}-path" ? ".github/workflows/other.yml"
                        : $".github/workflows/{(proof == "ci" ? "ci.yml" : "rc-gate.yml")}",
                    state = scenario == $"disabled-{proof}-workflow" ? "disabled_manually" : "active",
                });
            }
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
            start.Environment["FAKE_CI_RUNS"] = Runs("ci", 101);
            start.Environment["FAKE_RC_RUNS"] = Runs("rc", 202);
            start.Environment["FAKE_CI_WORKFLOW"] = Workflow("ci", 101);
            start.Environment["FAKE_RC_WORKFLOW"] = Workflow("rc", 202);
            start.Environment["FAKE_COMMITLINT_RUNS"] = Runs("commitlint", 303);

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
