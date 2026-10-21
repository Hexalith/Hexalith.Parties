using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Hexalith.Parties.Ci.Tests;

/// <summary>Executes the tracked release registry floor with isolated GitHub and NuGet fixtures.</summary>
public sealed class ReleaseRegistryFloorTests
{
    private const string SourceSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string PackageId = "Hexalith.Parties.Contracts";
    private const string OtherPackageId = "Hexalith.Parties.Client";
    private const string ApiPrefix = "repos/Hexalith/Hexalith.Parties";
    private const string IndexUrl = "https://api.nuget.org/v3-flatcontainer/hexalith.parties.contracts/index.json";
    private const string OtherIndexUrl = "https://api.nuget.org/v3-flatcontainer/hexalith.parties.client/index.json";

    /// <summary>Verifies numerical equality, lower versions and prereleases do not exceed the floor.</summary>
    /// <param name="floor">The reachable stable release version.</param>
    /// <param name="index">The registry version-index response.</param>
    [Theory]
    [InlineData("3.15.1", "{\"versions\":[\"3.2.18\"]}")]
    [InlineData("3.15.1", "{\"versions\":[\"3.15.1\",\"3.15.0\"]}")]
    [InlineData("3.15.1", "{\"versions\":[\"99.0.0-rc.1\",\"4.0.0-preview.2\"]}")]
    [InlineData("3.15.1", "{\"versions\":[\"3.15.1\",\"99.0.0-rc.1\"]}")]
    [InlineData("3.15.1", "{\"versions\":[\"99.0.0-a-b.9\",\"99.0.0--\"]}")]
    [InlineData("3.16.0", "{\"versions\":[\"3.16.0.0\"]}")]
    [InlineData("3.16.0", "{\"versions\":[\"3.16.0.7\",\"3.15.9.8\"]}")]
    [InlineData("3.16.0", "{\"versions\":[\"3.16\"]}")]
    [InlineData("3.16.0", "{\"versions\":[]}")]
    public void RegistryFloorAcceptsOnlyStableVersionCoresAtOrBelowTheFloor(string floor, string index)
    {
        (int exitCode, string output, string error, string outputs, string[] requests) = RunGate(floor, index);

        exitCode.ShouldBe(0, error);
        output.ShouldContain($"Release tag floor v{floor} is at or above every published version");
        outputs.ShouldBe($"release-floor-tag=v{floor}\n");
        requests.ShouldContain($"curl {IndexUrl}");
    }

    /// <summary>Verifies higher stable cores fail, using numerical rather than lexical ordering.</summary>
    /// <param name="floor">The reachable stable release version.</param>
    /// <param name="index">The registry version-index response.</param>
    /// <param name="highest">The highest offending published version.</param>
    [Theory]
    [InlineData("3.2.18", "{\"versions\":[\"3.15.1\"]}", "3.15.1")]
    [InlineData("3.15.1", "{\"versions\":[\"4.0.0\"]}", "4.0.0")]
    [InlineData("3.15.1", "{\"versions\":[\"3.15.2\"]}", "3.15.2")]
    [InlineData("3.15.1", "{\"versions\":[\"3.16.0.0\"]}", "3.16.0.0")]
    [InlineData("3.15.1", "{\"versions\":[\"3.16.0.7\"]}", "3.16.0.7")]
    [InlineData("3.15.1", "{\"versions\":[\"3.16\"]}", "3.16")]
    [InlineData("3.0.0", "{\"versions\":[\"3.2.18\",\"3.15.1\",\"3.9.99\",\"99.0.0-rc.1\"]}", "3.15.1")]
    public void RegistryFloorRejectsHigherStableVersionsWithoutEmittingSuccess(string floor, string index, string highest)
    {
        (int exitCode, string output, string error, string outputs, _) = RunGate(floor, index);

        exitCode.ShouldNotBe(0);
        error.ShouldContain($"Published package versions exceed the release tag floor v{floor}:");
        error.ShouldContain($"  {PackageId} {highest}\n");
        output.ShouldNotContain("is at or above every published version");
        outputs.ShouldBeEmpty();
    }

    /// <summary>Verifies invalid index payloads and unsupported version spellings fail closed.</summary>
    /// <param name="index">The malformed registry version-index response.</param>
    [Theory]
    [InlineData("not-json")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"versions\":null}")]
    [InlineData("{\"versions\":\"3.15.1\"}")]
    [InlineData("{\"versions\":[null]}")]
    [InlineData("{\"versions\":[42]}")]
    [InlineData("{\"versions\":[\"3.15.1\",\"broken\"]}")]
    [InlineData("{\"versions\":[\"3.15.1.0.0\"]}")]
    [InlineData("{\"versions\":[\"3.15.1+build.9\"]}")]
    [InlineData("{\"versions\":[\"3.16.0-a..b\"]}")]
    [InlineData("{\"versions\":[\"3.16.0-.\"]}")]
    [InlineData("{\"versions\":[\"3.16.0-.a\"]}")]
    [InlineData("{\"versions\":[\"3.16.0-a.\"]}")]
    [InlineData("{\"versions\":[\"3.16.0-\"]}")]
    [InlineData("{\"versions\":[\"3.16.0-a...b\",\"3.15.1\"]}")]
    public void RegistryFloorRejectsUnprovableVersionIndexes(string index)
    {
        (int exitCode, string output, string error, string outputs, _) = RunGate("3.15.1", index);

        exitCode.ShouldNotBe(0);
        error.ShouldContain($"The published version index for {PackageId} could not be validated safely.");
        output.ShouldNotContain("is at or above every published version");
        outputs.ShouldBeEmpty();
    }

    /// <summary>Verifies registry absence is accepted only for HTTP 404 and other failures stay fatal.</summary>
    /// <param name="scenario">The GitHub, registry or transport failure fixture.</param>
    /// <param name="expectedError">The diagnostic identifying the failed proof boundary.</param>
    [Theory]
    [InlineData("tag-api-failure", "The repository tag refs could not be queried safely.")]
    [InlineData("malformed-tags", "The repository tag refs response could not be validated safely.")]
    [InlineData("compare-api-failure", "could not be compared with the dispatched source.")]
    [InlineData("malformed-comparison", "could not be validated safely.")]
    [InlineData("unreachable-tag", "No release tag is reachable from the dispatched source")]
    [InlineData("manifest-api-failure", "The release package manifest could not be read at the dispatched source.")]
    [InlineData("malformed-manifest", "The release package manifest could not be validated safely.")]
    [InlineData("manifest-wrong-path", "The release package manifest response could not be validated safely.")]
    [InlineData("manifest-wrong-encoding", "The release package manifest response could not be validated safely.")]
    [InlineData("manifest-missing-content", "The release package manifest response could not be validated safely.")]
    [InlineData("manifest-invalid-base64", "The release package manifest could not be decoded.")]
    [InlineData("registry-401", "returned unexpected status 401.")]
    [InlineData("registry-403", "returned unexpected status 403.")]
    [InlineData("registry-429", "returned unexpected status 429.")]
    [InlineData("registry-500", "returned unexpected status 500.")]
    [InlineData("registry-503", "returned unexpected status 503.")]
    [InlineData("registry-transport", "could not be retrieved.")]
    public void RegistryFloorRejectsApiAndTransportFailures(string scenario, string expectedError)
    {
        (int exitCode, string output, string error, string outputs, _) = RunGate("3.15.1", "{\"versions\":[]}", scenario);

        exitCode.ShouldNotBe(0);
        error.ShouldContain(expectedError);
        output.ShouldNotContain("is at or above every published version");
        outputs.ShouldBeEmpty();
    }

    /// <summary>Verifies a never-published package index does not require a parseable response body.</summary>
    [Fact]
    public void RegistryFloorAcceptsAnExplicitlyMissingPackageIndex()
    {
        (int exitCode, string output, string error, string outputs, string[] requests) = RunGate("3.15.1", "not-json", "registry-404");

        exitCode.ShouldBe(0, error);
        output.ShouldContain("Release tag floor v3.15.1 is at or above every published version");
        outputs.ShouldBe("release-floor-tag=v3.15.1\n");
        requests.ShouldContain($"curl {IndexUrl}");
    }

    /// <summary>Verifies each manifest package is checked before successful floor output is allowed.</summary>
    [Fact]
    public void RegistryFloorChecksLaterManifestPackagesForCollisions()
    {
        (int exitCode, _, string error, string outputs, string[] requests) = RunGate(
            "3.15.1", "{\"versions\":[\"3.15.1\"]}", "two-packages");

        exitCode.ShouldNotBe(0);
        error.ShouldContain($"  {OtherPackageId} 3.16.0\n");
        error.ShouldNotContain($"  {PackageId} ");
        outputs.ShouldBeEmpty();
        requests.ShouldContain($"curl {IndexUrl}");
        requests.ShouldContain($"curl {OtherIndexUrl}");
    }

    /// <summary>Verifies the highest reachable tag is selected numerically across every API page.</summary>
    /// <param name="scenario">The tag ordering, reachability or pagination fixture.</param>
    [Theory]
    [InlineData("highest-reachable-tag")]
    [InlineData("higher-unreachable-tags")]
    [InlineData("paginated-tags")]
    public void RegistryFloorSelectsTheHighestReachableNumericTagAcrossPages(string scenario)
    {
        (int exitCode, _, string error, string outputs, string[] requests) = RunGate(
            "3.15.1", "{\"versions\":[\"3.15.1\"]}", scenario);

        exitCode.ShouldBe(0, error);
        outputs.ShouldBe("release-floor-tag=v3.15.1\n");
        string[] expectedTags = scenario switch
        {
            "higher-unreachable-tags" => ["v99.0.0", "v88.0.0", "v3.15.1"],
            "paginated-tags" => ["v99.0.0", "v3.15.1"],
            _ => ["v3.15.1"],
        };
        requests.Where(request => request.StartsWith($"gh {ApiPrefix}/compare/", StringComparison.Ordinal))
            .ShouldBe(expectedTags.Select(tag => $"gh {ApiPrefix}/compare/{tag}...{SourceSha}?per_page=1"));
    }

    /// <summary>Runs the exact workflow shell with fixture executables that cannot access a service.</summary>
    /// <param name="floor">The version of the reachable tag.</param>
    /// <param name="index">The first declared package's published-version response.</param>
    /// <param name="scenario">An optional failure or inventory fixture.</param>
    /// <returns>The exit code, captured streams, workflow outputs and recorded requests.</returns>
    private static (int ExitCode, string Output, string Error, string Outputs, string[] Requests) RunGate(
        string floor,
        string index,
        string scenario = "valid")
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("The release registry-floor fixture requires bash and jq.");
        }

        string temporary = Path.Combine(Path.GetTempPath(), $"hexalith-registry-floor-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
            WriteExecutable(Path.Combine(temporary, "gh"), """
                #!/usr/bin/env bash
                set -euo pipefail
                endpoint=''
                paginate=false
                for argument in "$@"; do
                  case "$argument" in
                    repos/*) endpoint="$argument" ;;
                    --paginate) paginate=true ;;
                  esac
                done
                printf 'gh %s\n' "$endpoint" >> "$FAKE_REQUEST_LOG"
                case "$endpoint" in
                  "$FAKE_API_PREFIX/git/refs/tags?per_page=100")
                    if [ "$paginate" != true ]; then echo 'Fixture tag lookup requires --paginate.' >&2; exit 98; fi
                    if [ "$FAKE_SCENARIO" = tag-api-failure ]; then exit 1; fi
                    if [ "$FAKE_SCENARIO" = malformed-tags ]; then printf '{}\n'; else printf '%s\n' "$FAKE_TAG_REFS"; fi
                    ;;
                  "$FAKE_API_PREFIX"/compare/v*"...$DISPATCH_SHA?per_page=1")
                    if [ "$FAKE_SCENARIO" = compare-api-failure ]; then exit 1; fi
                    case "$FAKE_SCENARIO" in
                      malformed-comparison) printf '{}\n' ;;
                      unreachable-tag) printf '{"status":"behind"}\n' ;;
                      higher-unreachable-tags | paginated-tags)
                        candidate="${endpoint#"$FAKE_API_PREFIX/compare/"}"
                        case "$candidate" in
                          v99.0.0...*) printf '{"status":"behind"}\n' ;;
                          v88.0.0...*) printf '{"status":"diverged"}\n' ;;
                          *) printf '{"status":"ahead"}\n' ;;
                        esac
                        ;;
                      *) printf '{"status":"ahead"}\n' ;;
                    esac
                    ;;
                  "$FAKE_API_PREFIX/contents/$PACKAGE_MANIFEST?ref=$DISPATCH_SHA")
                    if [ "$FAKE_SCENARIO" = manifest-api-failure ]; then exit 1; fi
                    printf '%s\n' "$FAKE_MANIFEST_RESPONSE"
                    ;;
                  *) echo "Unexpected fixture GitHub request: $endpoint" >&2; exit 98 ;;
                esac
                """);
            WriteExecutable(Path.Combine(temporary, "curl"), """
                #!/usr/bin/env bash
                set -euo pipefail
                destination=''
                url=''
                write_format=''
                while [ "$#" -gt 0 ]; do
                  case "$1" in
                    --output) destination="$2"; shift 2 ;;
                    --write-out) write_format="$2"; shift 2 ;;
                    --max-time | --retry) shift 2 ;;
                    --silent | --show-error | --location | --retry-connrefused | --retry-all-errors) shift ;;
                    https://*) url="$1"; shift ;;
                    *) echo "Unexpected fixture curl argument: $1" >&2; exit 98 ;;
                  esac
                done
                if [ "$write_format" != '%{http_code}' ]; then echo 'Fixture curl requires the HTTP status write-out format.' >&2; exit 98; fi
                printf 'curl %s\n' "$url" >> "$FAKE_REQUEST_LOG"
                case "$url" in
                  "$FAKE_INDEX_URL") body="$FAKE_INDEX" ;;
                  "$FAKE_OTHER_INDEX_URL") body='{"versions":["3.16.0"]}' ;;
                  *) echo "Unexpected fixture NuGet request: $url" >&2; exit 98 ;;
                esac
                if [ -z "$destination" ]; then echo 'Missing fixture output path.' >&2; exit 98; fi
                if [ "$FAKE_SCENARIO" = registry-transport ]; then echo 'Fixture transport failure.' >&2; exit 7; fi
                printf '%s\n' "$body" > "$destination"
                case "$FAKE_SCENARIO" in
                  registry-*) printf '%s' "${FAKE_SCENARIO#registry-}" ;;
                  *) printf '200' ;;
                esac
                """);

            string manifest = scenario == "malformed-manifest" ? "{\"packages\":[]}"
                : JsonSerializer.Serialize(new
                {
                    packages = (scenario == "two-packages" ? new[] { PackageId, OtherPackageId } : [PackageId])
                        .Select(id => new { id }).ToArray(),
                });
            Dictionary<string, object> manifestResponse = new()
            {
                ["type"] = "file",
                ["path"] = scenario == "manifest-wrong-path" ? "tools/other-packages.json" : "tools/release-packages.json",
                ["encoding"] = scenario == "manifest-wrong-encoding" ? "utf-8" : "base64",
                ["content"] = scenario == "manifest-invalid-base64" ? "!invalid!" : Convert.ToBase64String(Encoding.UTF8.GetBytes(manifest)),
            };
            if (scenario == "manifest-missing-content")
            {
                manifestResponse.Remove("content");
            }

            string[] tags = scenario switch
            {
                "highest-reachable-tag" => ["3.2.18", "3.15.1", "3.9.99"],
                "higher-unreachable-tags" => ["3.2.18", "99.0.0", "3.15.1", "88.0.0"],
                "paginated-tags" => ["3.2.18", "99.0.0"],
                _ => [floor],
            };
            string tagRefs = JsonSerializer.Serialize(tags.Select(version => new { @ref = $"refs/tags/v{version}" }));
            if (scenario == "paginated-tags")
            {
                tagRefs += "\n" + JsonSerializer.Serialize(new[] { new { @ref = $"refs/tags/v{floor}" } });
            }

            string outputPath = Path.Combine(temporary, "outputs");
            string requestLog = Path.Combine(temporary, "requests");
            ProcessStartInfo start = new("bash")
            {
                WorkingDirectory = temporary,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add(ReadRegistryFloorScript());
            start.Environment["PATH"] = $"{temporary}{Path.PathSeparator}{start.Environment["PATH"]}";
            start.Environment["GH_TOKEN"] = "fixture-token";
            start.Environment["REPOSITORY"] = "Hexalith/Hexalith.Parties";
            start.Environment["DISPATCH_SHA"] = SourceSha;
            start.Environment["PACKAGE_MANIFEST"] = "tools/release-packages.json";
            start.Environment["GITHUB_OUTPUT"] = outputPath;
            start.Environment["FAKE_API_PREFIX"] = ApiPrefix;
            start.Environment["FAKE_SCENARIO"] = scenario;
            start.Environment["FAKE_FLOOR"] = floor;
            start.Environment["FAKE_TAG_REFS"] = tagRefs;
            start.Environment["FAKE_MANIFEST_RESPONSE"] = JsonSerializer.Serialize(manifestResponse);
            start.Environment["FAKE_INDEX_URL"] = IndexUrl;
            start.Environment["FAKE_OTHER_INDEX_URL"] = OtherIndexUrl;
            start.Environment["FAKE_INDEX"] = index;
            start.Environment["FAKE_REQUEST_LOG"] = requestLog;

            using Process process = new() { StartInfo = start };
            process.Start().ShouldBeTrue();
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(10_000))
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5_000).ShouldBeTrue("The timed-out fixture must exit after termination.");
                throw new TimeoutException("The release registry-floor fixture exceeded 10000 ms.");
            }

            Task.WaitAll([output, error], 5_000).ShouldBeTrue("Both fixture output streams must close within 5000 ms.");
            return (process.ExitCode, output.Result, error.Result,
                File.Exists(outputPath) ? File.ReadAllText(outputPath) : string.Empty,
                File.Exists(requestLog) ? File.ReadAllLines(requestLog) : []);
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>Extracts only the tracked registry-floor run block, excluding subsequent jobs.</summary>
    /// <returns>The Bash body after removing its YAML block indentation.</returns>
    private static string ReadRegistryFloorScript()
    {
        string workflow = CiTestPaths.ReadRepoFile(".github/workflows/release.yml").Replace("\r\n", "\n");
        string step = workflow.Split("      - name: ", StringSplitOptions.None)
            .Single(candidate => candidate.StartsWith("Require registry versions at or below the release tag floor\n", StringComparison.Ordinal));
        string body = step.Split("        run: |\n", StringSplitOptions.None)[1];
        return string.Join('\n', body.Split('\n')
            .TakeWhile(line => string.IsNullOrWhiteSpace(line) || line.StartsWith("          ", StringComparison.Ordinal))
            .Select(line => line.StartsWith("          ", StringComparison.Ordinal) ? line[10..] : line));
    }

    /// <summary>Writes an executable fixture without platform-dependent line endings.</summary>
    /// <param name="path">The isolated executable path.</param>
    /// <param name="script">The fixture's Bash implementation.</param>
    private static void WriteExecutable(string path, string script)
    {
        File.WriteAllText(path, script.Replace("\r\n", "\n") + "\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }
}
