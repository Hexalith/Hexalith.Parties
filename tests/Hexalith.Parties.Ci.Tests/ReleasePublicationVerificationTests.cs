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
    [InlineData("encoded")]
    [InlineData("unrelated-before-match")]
    public void PublicationRequiresAnExactSourceMatchDespiteDeletedHistoricalTags(string scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        (int exitCode, string output, string error, string[] requests) = RunGate(scenario);

        exitCode.ShouldBe(0, error);
        output.ShouldContain($"resolves to dispatched SHA {SourceSha}.");
        string tag = scenario == "encoded" ? "release/with space+#?" : "vcurrent";
        requests.ShouldContain($"{ApiPrefix}/git/ref/tags/{Uri.EscapeDataString(tag)}");
        if (scenario is not ("lightweight" or "annotated"))
        {
            output.ShouldContain("Skipping published release tag 'refs/tags/vdeleted'");
            Array.IndexOf(requests, $"{ApiPrefix}/git/ref/tags/vdeleted")
                .ShouldBeLessThan(Array.IndexOf(requests, $"{ApiPrefix}/git/ref/tags/{Uri.EscapeDataString(tag)}"));
        }

        requests.ShouldNotContain($"{ApiPrefix}/git/ref/tags/vdraft");
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
        (int exitCode, string output, string error, string[] requests) = RunGate(scenario);

        exitCode.ShouldNotBe(0);
        error.ShouldContain("no published GitHub Release tag resolves to dispatched SHA");
        output.ShouldNotContain("resolves to dispatched SHA");
        requests.ShouldNotContain($"{ApiPrefix}/git/ref/tags/vdraft");
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
        (int exitCode, string output, string error, string[] requests) = RunGate(scenario);

        exitCode.ShouldNotBe(0);
        error.ShouldContain("could not be resolved");
        output.ShouldNotContain("Skipping published release tag");
        requests.ShouldNotContain($"{ApiPrefix}/git/ref/tags/vcurrent");
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
    public void InvalidPublicationEvidenceStillFails(string scenario, string expectedError)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        (int exitCode, string output, string error, string[] requests) = RunGate(scenario);

        exitCode.ShouldNotBe(0);
        error.ShouldContain(expectedError);
        output.ShouldNotContain("resolves to dispatched SHA");
        if (scenario == "invalid-source")
        {
            requests.ShouldBeEmpty();
        }
    }

    /// <summary>Runs the tracked shell with local responses and bounded process lifetime.</summary>
    /// <param name="scenario">The API fixture scenario.</param>
    /// <returns>The exit code, stdout, stderr and GitHub API arguments.</returns>
    private static (int ExitCode, string Output, string Error, string[] Requests) RunGate(string scenario)
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
                  "$FAKE_API_PREFIX/git/ref/tags/vdeleted")
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
                  "$FAKE_API_PREFIX/git/ref/tags/vother")
                    body="{\"ref\":\"refs/tags/vother\",\"object\":{\"type\":\"commit\",\"sha\":\"$FAKE_OTHER_SHA\"}}"
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

            string tag = scenario == "encoded" ? "release/with space+#?" : "vcurrent";
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
            string draft = JsonSerializer.Serialize(new { draft = true, tag_name = "vdraft", target_commitish = SourceSha });
            string current = JsonSerializer.Serialize(new { draft = false, tag_name = tag, target_commitish = SourceSha });
            string deleted = JsonSerializer.Serialize(new { draft = false, tag_name = "vdeleted", target_commitish = SourceSha });
            string unrelated = JsonSerializer.Serialize(new { draft = false, tag_name = "vother", target_commitish = SourceSha });
            bool historical = scenario.StartsWith("deleted-", StringComparison.Ordinal)
                || scenario.StartsWith("historical-", StringComparison.Ordinal)
                || scenario is "paginated" or "encoded" or "missing-only" or "all-missing" or "transport" or "spoofed-404" or "status-4040";
            string releases = scenario switch
            {
                "empty" => "[]",
                "draft-only" => $"[{draft}]",
                "malformed-releases" => "{}",
                "missing-only" => $"[{deleted}]",
                "paginated" => $"[{draft},{deleted}]\n[{current}]",
                "unrelated-before-match" => $"[{deleted},{unrelated},{current}]",
                _ => historical ? $"[{draft},{deleted},{current}]" : $"[{draft},{current}]",
            };

            string requestLog = Path.Combine(temporary, "requests");
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
            start.Environment["GITHUB_OUTPUT"] = Path.Combine(temporary, "github-output");
            start.Environment["GH_TOKEN"] = "fixture-token";
            start.Environment["REPOSITORY"] = "Hexalith/Hexalith.Parties";
            start.Environment["DISPATCH_SHA"] = scenario == "invalid-source" ? "main" : SourceSha;
            start.Environment["FAKE_SCENARIO"] = scenario;
            start.Environment["FAKE_REQUEST_LOG"] = requestLog;
            start.Environment["FAKE_API_PREFIX"] = ApiPrefix;
            start.Environment["FAKE_TAG_URI"] = Uri.EscapeDataString(tag);
            start.Environment["FAKE_TAG_SHA"] = TagSha;
            start.Environment["FAKE_OTHER_SHA"] = OtherSha;
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
            return (process.ExitCode, stdout, stderr, File.Exists(requestLog) ? File.ReadAllLines(requestLog) : []);
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }
}
