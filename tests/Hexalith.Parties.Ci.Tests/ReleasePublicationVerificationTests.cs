using System.Diagnostics;
using System.Text.Json;

namespace Hexalith.Parties.Ci.Tests;

/// <summary>Executes the publication verifier with isolated GitHub API responses.</summary>
public sealed class ReleasePublicationVerificationTests
{
    private const string SourceSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string OtherSha = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string TagSha = "cccccccccccccccccccccccccccccccccccccccc";
    private const string ApiPrefix = "repos/Hexalith/Hexalith.Parties";

    /// <summary>Verifies deleted refs cannot hide a later exact-source publication.</summary>
    /// <param name="scenario">The release, tag and HTTP framing fixture.</param>
    [Theory]
    [InlineData("lightweight")]
    [InlineData("annotated")]
    [InlineData("deleted-lightweight")]
    [InlineData("deleted-annotated")]
    [InlineData("deleted-lf")]
    [InlineData("deleted-http11")]
    [InlineData("paginated")]
    [InlineData("unrelated-before-match")]
    public void PublicationRequiresAnExactSourceMatchDespiteDeletedHistoricalTags(string scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        (int exitCode, string output, string error, string[] requests, string githubOutput) = RunGate(scenario);

        exitCode.ShouldBe(0, error);
        output.ShouldContain($"resolves to dispatched SHA {SourceSha}.");
        const string tag = "v1.2.3";
        githubOutput.ShouldBe($"matching-tag={tag}\n");
        requests.ShouldContain($"{ApiPrefix}/git/ref/tags/{Uri.EscapeDataString(tag)}");
        if (scenario is not ("lightweight" or "annotated"))
        {
            output.ShouldContain("Skipping published release tag 'refs/tags/v1.0.0'");
            Array.IndexOf(requests, $"{ApiPrefix}/git/ref/tags/v1.0.0")
                .ShouldBeLessThan(Array.IndexOf(requests, $"{ApiPrefix}/git/ref/tags/{Uri.EscapeDataString(tag)}"));
        }

        requests.ShouldNotContain($"{ApiPrefix}/git/ref/tags/v9.0.0");
        if (scenario.Contains("annotated", StringComparison.Ordinal))
        {
            requests.ShouldContain($"{ApiPrefix}/git/tags/{TagSha}");
        }
    }

    /// <summary>Verifies missing tags never substitute for exact publication evidence.</summary>
    /// <param name="scenario">The absent or unrelated publication fixture.</param>
    [Theory]
    [InlineData("missing-only")]
    [InlineData("all-missing")]
    [InlineData("unrelated")]
    [InlineData("draft-only")]
    [InlineData("empty")]
    public void PublicationWithoutAResolvableExactSourceReleaseFails(string scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        (int exitCode, string output, string error, string[] requests, _) = RunGate(scenario);

        exitCode.ShouldNotBe(0);
        error.ShouldContain("no published GitHub Release tag resolves to dispatched SHA");
        output.ShouldNotContain("resolves to dispatched SHA");
        requests.ShouldNotContain($"{ApiPrefix}/git/ref/tags/v9.0.0");
    }

    /// <summary>Verifies only an explicit missing-ref HTTP status may be skipped.</summary>
    /// <param name="scenario">The failed historical lookup fixture.</param>
    [Theory]
    [InlineData("historical-401")]
    [InlineData("historical-403")]
    [InlineData("historical-429")]
    [InlineData("historical-500")]
    [InlineData("historical-503")]
    [InlineData("transport")]
    [InlineData("spoofed-404")]
    [InlineData("status-4040")]
    public void OtherLookupFailuresRemainFatalEvenWithALaterValidRelease(string scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        (int exitCode, string output, string error, string[] requests, _) = RunGate(scenario);

        exitCode.ShouldNotBe(0);
        error.ShouldContain("could not be resolved");
        output.ShouldNotContain("Skipping published release tag");
        requests.ShouldNotContain($"{ApiPrefix}/git/ref/tags/v1.2.3");
    }

    /// <summary>Verifies existing response validation and annotated peeling remain strict.</summary>
    /// <param name="scenario">The invalid dispatch or API response fixture.</param>
    /// <param name="expectedError">The diagnostic from the rejected validation boundary.</param>
    [Theory]
    [InlineData("invalid-source", "exact lowercase commit SHA")]
    [InlineData("releases-failure", "Releases API could not be queried")]
    [InlineData("malformed-releases", "Releases response could not be validated")]
    [InlineData("malformed-ref", "response could not be validated")]
    [InlineData("truncated-headers", "The exact tag ref")]
    [InlineData("missing-separator", "The exact tag ref")]
    [InlineData("empty-response", "The exact tag ref")]
    [InlineData("wrong-ref", "response could not be validated")]
    [InlineData("invalid-sha", "object must have an exact lowercase SHA")]
    [InlineData("invalid-type", "unsupported object type")]
    [InlineData("malformed-tag", "could not be validated")]
    [InlineData("tag-failure", "could not be peeled")]
    [InlineData("invalid-peeled-sha", "did not peel to an exact lowercase commit SHA")]
    [InlineData("peeled-type", "unsupported peeled object type")]
    [InlineData("tag-line-break", "could not be validated safely")]
    public void InvalidPublicationEvidenceStillFails(string scenario, string expectedError)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        (int exitCode, string output, string error, string[] requests, string githubOutput) = RunGate(scenario);

        exitCode.ShouldNotBe(0);
        error.ShouldContain(expectedError);
        output.ShouldNotContain("resolves to dispatched SHA");
        if (scenario == "invalid-source")
        {
            requests.ShouldBeEmpty();
        }

        if (scenario == "tag-line-break")
        {
            githubOutput.ShouldBeEmpty();
        }
    }

    /// <summary>Canonical release selection ignores non-version tags and deduplicates releases independently of list order.</summary>
    /// <param name="scenario">The eligible/non-version ordering or repeated tag.</param>
    [Theory]
    [InlineData("nonversion-first")]
    [InlineData("nonversion-last")]
    [InlineData("duplicate-tag")]
    [InlineData("duplicate-annotated")]
    public void PublicationSelectsOneCanonicalStableVersion(string scenario)
    {
        (int exitCode, string output, string error, string[] requests, string githubOutput) = RunGate(scenario);

        exitCode.ShouldBe(0, error);
        output.ShouldContain($"resolves to dispatched SHA {SourceSha}.");
        githubOutput.ShouldBe("matching-tag=v1.2.3\n");
        requests.Count(request => request == $"{ApiPrefix}/git/ref/tags/v1.2.3").ShouldBe(1);
        requests.ShouldNotContain($"{ApiPrefix}/git/ref/tags/release%2Fwith%20space%2B%23%3F");
    }

    /// <summary>Distinct eligible versions at the same source are ambiguous, including annotated tags in either list order.</summary>
    /// <param name="scenario">The order and tag-object fixture.</param>
    [Theory]
    [InlineData("ambiguous-first")]
    [InlineData("ambiguous-last")]
    [InlineData("ambiguous-annotated")]
    public void PublicationRejectsMultipleExactSourceStableVersions(string scenario)
    {
        (int exitCode, string output, string error, string[] requests, string githubOutput) = RunGate(scenario);

        exitCode.ShouldNotBe(0);
        error.ShouldContain("Multiple canonical stable release versions");
        output.ShouldNotContain("resolves to dispatched SHA");
        githubOutput.ShouldBeEmpty();
        requests.ShouldContain($"{ApiPrefix}/git/ref/tags/v1.2.3");
        requests.ShouldContain($"{ApiPrefix}/git/ref/tags/v2.0.0");
    }

    /// <summary>Ineligible tag spellings never become artifact versions or workflow output.</summary>
    /// <param name="tag">A noncanonical or unsafe release tag.</param>
    [Theory]
    [InlineData("release/with space+#?")]
    [InlineData("1.2.3")]
    [InlineData("vv1.2.3")]
    [InlineData("v01.2.3")]
    [InlineData("v1.02.3")]
    [InlineData("v1.2.03")]
    [InlineData("v1.2.3.0")]
    [InlineData("v1.2.3-rc.1")]
    [InlineData("v1.2.3+build")]
    [InlineData("v1.2.3\ninjected=value")]
    [InlineData("v1.2.3\rinjected=value")]
    public void PublicationRejectsIneligibleVersionsWithoutOutput(string tag)
    {
        (int exitCode, _, string error, string[] requests, string githubOutput) = RunGate("ineligible", tag);

        exitCode.ShouldNotBe(0);
        error.ShouldNotBeNullOrWhiteSpace();
        githubOutput.ShouldBeEmpty();
        requests.ShouldNotContain(request => request.Contains("/git/ref/tags/", StringComparison.Ordinal));
    }

    /// <summary>The artifact step removes one leading v and leaves a second v in place.</summary>
    [Fact]
    public void ArtifactStepStripsOneLeadingVersionPrefix()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("The release publication verifier requires bash.");
        }

        string workflow = CiTestPaths.ReadRepoFile(".github/workflows/release.yml").Replace("\r\n", "\n");
        string step = workflow.Split("      - name: ", StringSplitOptions.None)
            .Single(candidate => candidate.StartsWith("Verify every declared registry artifact\n", StringComparison.Ordinal));
        const string marker = "--version \"";
        int start = step.IndexOf(marker, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0);
        start += marker.Length;
        int end = step.IndexOf('"', start);
        string expansion = step[start..end];
        expansion.ShouldBe("${RELEASE_TAG#v}");

        ProcessStartInfo processStart = new("bash")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        processStart.ArgumentList.Add("-c");
        processStart.ArgumentList.Add(
            "set -euo pipefail\n"
            + "RELEASE_TAG='v1.2.3'\n"
            + "[ \"$(printf '%s' " + expansion + ")\" = '1.2.3' ]\n"
            + "RELEASE_TAG='vv1.2.3'\n"
            + "[ \"$(printf '%s' " + expansion + ")\" = 'v1.2.3' ]\n"
            + "RELEASE_TAG='1.2.3'\n"
            + "[ \"$(printf '%s' " + expansion + ")\" = '1.2.3' ]\n");
        using Process process = Process.Start(processStart)!;
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(5_000).ShouldBeTrue(stderr);
        process.ExitCode.ShouldBe(0, stdout + stderr);
    }

    /// <summary>Runs the tracked shell with local responses and bounded process lifetime.</summary>
    /// <param name="scenario">The API fixture scenario.</param>
    /// <returns>The exit code, stdout, stderr, GitHub API arguments and github-output file.</returns>
    private static (int ExitCode, string Output, string Error, string[] Requests, string GithubOutput) RunGate(string scenario, string? releaseTag = null)
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("The release publication verifier requires bash and jq.");
        }

        string workflow = CiTestPaths.ReadRepoFile(".github/workflows/release.yml").Replace("\r\n", "\n");
        string step = workflow.Split("      - name: ", StringSplitOptions.None)
            .Single(candidate => candidate.StartsWith("Require published release for exact dispatched source\n", StringComparison.Ordinal));
        string script = step.Split("        run: |\n", StringSplitOptions.None)[1];
        script = string.Join('\n', script.Split('\n').Select(line =>
            line.StartsWith("          ", StringComparison.Ordinal) ? line[10..] : line));

        string temporary = Path.Combine(Path.GetTempPath(), $"hexalith-release-publication-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
            string fakeGh = Path.Combine(temporary, "gh");
            File.WriteAllText(fakeGh, """
                #!/usr/bin/env bash
                set -euo pipefail
                printf '%s\n' "$@" >> "$FAKE_REQUEST_LOG"
                include=false
                paginate=false
                endpoint=''
                for argument in "$@"; do
                  case "$argument" in
                    api) ;;
                    --paginate) paginate=true ;;
                    --include) include=true ;;
                    *) endpoint="$argument" ;;
                  esac
                done
                status=200
                body=''
                case "$endpoint" in
                  "$FAKE_API_PREFIX/releases?per_page=100")
                    if [ "$FAKE_SCENARIO" = releases-failure ]; then exit 1; fi
                    if [ "$FAKE_SCENARIO" = paginated ] && [ "$paginate" = false ]; then
                      printf '%s\n' "${FAKE_RELEASES%%$'\n'*}"
                    else
                      printf '%s\n' "$FAKE_RELEASES"
                    fi
                    exit 0
                    ;;
                  "$FAKE_API_PREFIX/git/ref/tags/v1.0.0")
                    status=404
                    body='{"message":"Not Found"}'
                    case "$FAKE_SCENARIO" in
                      historical-*) status="${FAKE_SCENARIO#historical-}" ;;
                      status-4040) status=4040 ;;
                      transport) echo 'Transport failed.' >&2; exit 1 ;;
                      spoofed-404)
                        echo 'gh: Not Found (HTTP 404)' >&2
                        echo '{"message":"HTTP/2.0 404 Not Found"}'
                        exit 1
                        ;;
                    esac
                    ;;
                  "$FAKE_API_PREFIX/git/ref/tags/$FAKE_TAG_URI")
                    body="$FAKE_REF"
                    if [ "$FAKE_SCENARIO" = all-missing ]; then status=404; fi
                    ;;
                  "$FAKE_API_PREFIX/git/ref/tags/v2.0.0")
                    body="{\"ref\":\"refs/tags/v2.0.0\",\"object\":{\"type\":\"commit\",\"sha\":\"$FAKE_SOURCE_SHA\"}}"
                    ;;
                  "$FAKE_API_PREFIX/git/ref/tags/v1.1.0")
                    body="{\"ref\":\"refs/tags/v1.1.0\",\"object\":{\"type\":\"commit\",\"sha\":\"$FAKE_OTHER_SHA\"}}"
                    ;;
                  "$FAKE_API_PREFIX/git/tags/$FAKE_TAG_SHA")
                    body="$FAKE_TAG_OBJECT"
                    if [ "$FAKE_SCENARIO" = tag-failure ]; then status=404; fi
                    ;;
                  *) echo "Unexpected fixture request: $endpoint" >&2; exit 1 ;;
                esac
                if [ "$include" = true ]; then
                  case "$FAKE_SCENARIO" in
                    empty-response) exit 0 ;;
                    truncated-headers) printf 'HTTP/2.0 200 OK\nX-Fixture: true\r\n'; exit 0 ;;
                    missing-separator) printf 'HTTP/2.0 200 OK\nX-Fixture: true\r\n%s\n' "$body"; exit 0 ;;
                  esac
                  protocol='2.0'
                  newline=$'\r\n'
                  if [ "$FAKE_SCENARIO" = deleted-lf ]; then newline=$'\n'; fi
                  if [ "$FAKE_SCENARIO" = deleted-http11 ]; then protocol='1.1'; fi
                  printf 'HTTP/%s %s Fixture\nX-Fixture: true%s%s' "$protocol" "$status" "$newline" "$newline"
                fi
                printf '%s\n' "$body"
                if [ "$status" != 200 ]; then
                  echo "Fixture HTTP failure ($status)." >&2
                  exit 1
                fi
                """.Replace("\r\n", "\n") + "\n");
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(fakeGh, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            string tag = releaseTag ?? (scenario switch
            {
                "tag-line-break" => "v1.2.3\ninjected",
                _ => "v1.2.3",
            });
            bool annotated = scenario.Contains("annotated", StringComparison.Ordinal)
                || scenario is "malformed-tag" or "tag-failure" or "invalid-peeled-sha" or "peeled-type";
            string tagRef = JsonSerializer.Serialize(new
            {
                @ref = scenario == "wrong-ref" ? "refs/tags/other" : $"refs/tags/{tag}",
                @object = new
                {
                    type = scenario == "invalid-type" ? "tree" : annotated ? "tag" : "commit",
                    sha = scenario == "invalid-sha" ? "main" : annotated ? TagSha : scenario == "unrelated" ? OtherSha : SourceSha,
                },
            });
            string tagObject = JsonSerializer.Serialize(new
            {
                sha = TagSha,
                @object = new
                {
                    type = scenario == "peeled-type" ? "tag" : "commit",
                    sha = scenario == "invalid-peeled-sha" ? "main" : SourceSha,
                },
            });
            // Misleading target_commitish must never substitute for the resolved tag SHA.
            string draft = JsonSerializer.Serialize(new { draft = true, tag_name = "v9.0.0", target_commitish = SourceSha });
            string current = JsonSerializer.Serialize(new { draft = false, tag_name = tag, target_commitish = SourceSha });
            string nonversion = JsonSerializer.Serialize(new { draft = false, tag_name = "release/with space+#?", target_commitish = SourceSha });
            string second = JsonSerializer.Serialize(new { draft = false, tag_name = "v2.0.0", target_commitish = SourceSha });
            string deleted = JsonSerializer.Serialize(new { draft = false, tag_name = "v1.0.0", target_commitish = SourceSha });
            string unrelated = JsonSerializer.Serialize(new { draft = false, tag_name = "v1.1.0", target_commitish = SourceSha });
            bool historical = scenario.StartsWith("deleted-", StringComparison.Ordinal)
                || scenario.StartsWith("historical-", StringComparison.Ordinal)
                || scenario is "paginated" or "missing-only" or "all-missing" or "transport" or "spoofed-404" or "status-4040";
            string releases = scenario switch
            {
                "nonversion-first" => $"[{nonversion},{current}]",
                "nonversion-last" => $"[{current},{nonversion}]",
                "duplicate-tag" or "duplicate-annotated" => $"[{current},{current}]",
                "ambiguous-first" => $"[{second},{current}]",
                "ambiguous-last" or "ambiguous-annotated" => $"[{current},{second}]",
                "empty" => "[]",
                "draft-only" => $"[{draft}]",
                "malformed-releases" => "{}",
                "missing-only" => $"[{deleted}]",
                "paginated" => $"[{draft},{deleted}]\n[{current}]",
                "unrelated-before-match" => $"[{deleted},{unrelated},{current}]",
                _ => historical ? $"[{draft},{deleted},{current}]" : $"[{draft},{current}]",
            };

            string requestLog = Path.Combine(temporary, "requests");
            string githubOutputPath = Path.Combine(temporary, "github-output");
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
            start.Environment["GITHUB_OUTPUT"] = githubOutputPath;
            start.Environment["GH_TOKEN"] = "fixture-token";
            start.Environment["REPOSITORY"] = "Hexalith/Hexalith.Parties";
            start.Environment["DISPATCH_SHA"] = scenario == "invalid-source" ? "main" : SourceSha;
            start.Environment["FAKE_SCENARIO"] = scenario;
            start.Environment["FAKE_REQUEST_LOG"] = requestLog;
            start.Environment["FAKE_API_PREFIX"] = ApiPrefix;
            start.Environment["FAKE_TAG_URI"] = Uri.EscapeDataString(tag);
            start.Environment["FAKE_TAG_SHA"] = TagSha;
            start.Environment["FAKE_OTHER_SHA"] = OtherSha;
            start.Environment["FAKE_SOURCE_SHA"] = SourceSha;
            start.Environment["FAKE_RELEASES"] = releases;
            start.Environment["FAKE_REF"] = scenario == "malformed-ref" ? "{}" : tagRef;
            start.Environment["FAKE_TAG_OBJECT"] = scenario == "malformed-tag" ? "{}" : tagObject;

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

            string stdout = output.GetAwaiter().GetResult();
            string stderr = error.GetAwaiter().GetResult();
            exited.ShouldBeTrue($"Publication verifier timed out: {stderr}");
            string githubOutput = File.Exists(githubOutputPath) ? File.ReadAllText(githubOutputPath) : string.Empty;
            return (process.ExitCode, stdout, stderr, File.Exists(requestLog) ? File.ReadAllLines(requestLog) : [], githubOutput);
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }
}
