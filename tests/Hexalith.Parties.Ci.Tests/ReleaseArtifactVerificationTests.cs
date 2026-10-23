using System.Diagnostics;

namespace Hexalith.Parties.Ci.Tests;

/// <summary>Validates actual registry bytes with isolated HTTP archives and the real pinned Builds OCI validator.</summary>
public sealed class ReleaseArtifactVerificationTests
{
    /// <summary>Rejects incomplete or mismatched publication and proves bounded propagation recovery.</summary>
    /// <param name="scenario">The local package, HTTP or OCI graph mutation.</param>
    [Theory]
    [InlineData("valid")]
    [InlineData("propagation-package")]
    [InlineData("propagation-image")]
    [InlineData("transient-recovery")]
    [InlineData("transport-recovery")]
    [InlineData("missing-package")]
    [InlineData("missing-image-parties")]
    [InlineData("missing-image-parties-mcp")]
    [InlineData("missing-image-parties-ui")]
    [InlineData("wrong-id")]
    [InlineData("wrong-version")]
    [InlineData("wrong-repository")]
    [InlineData("wrong-repository-type")]
    [InlineData("wrong-commit")]
    [InlineData("missing-license")]
    [InlineData("missing-readme")]
    [InlineData("forbidden-dependency")]
    [InlineData("malformed-package")]
    [InlineData("corrupt-package-crc")]
    [InlineData("duplicate-nuspec")]
    [InlineData("nested-nuspec")]
    [InlineData("member-count-bound")]
    [InlineData("archive-size-bound")]
    [InlineData("nuspec-size-bound")]
    [InlineData("duplicate-repository")]
    [InlineData("non-package-root")]
    [InlineData("uppercase-file-license")]
    [InlineData("nuspec-doctype")]
    [InlineData("duplicate-member")]
    [InlineData("ambiguous-metadata")]
    [InlineData("partial-inventory")]
    [InlineData("duplicate-inventory")]
    [InlineData("ambiguous-inventory")]
    [InlineData("duplicate-json-inventory")]
    [InlineData("malformed-index")]
    [InlineData("oversized-index")]
    [InlineData("wrong-index-digest")]
    [InlineData("immutable-mismatch")]
    [InlineData("missing-platform")]
    [InlineData("duplicate-platform")]
    [InlineData("wrong-config-platform")]
    [InlineData("wrong-child-digest")]
    [InlineData("wrong-config-digest")]
    [InlineData("wrong-provenance")]
    [InlineData("authentication")]
    [InlineData("forbidden")]
    [InlineData("oci-authentication")]
    [InlineData("retry-exhaustion")]
    [InlineData("request-timeout")]
    [InlineData("total-budget")]
    [InlineData("unpinned-validator")]
    [InlineData("nested-metadata")]
    [InlineData("nested-id")]
    [InlineData("valid-file-license")]
    [InlineData("missing-file-license")]
    [InlineData("valid-legacy-file-license")]
    [InlineData("missing-legacy-file-license")]
    [InlineData("no-dependencies")]
    [InlineData("missing-eventstore-dependency")]
    [InlineData("wrong-eventstore-version")]
    [InlineData("wrong-commons-version")]
    [InlineData("missing-required-commons-http")]
    [InlineData("wrong-extra-eventstore-version")]
    [InlineData("download-slow-body")]
    [InlineData("download-slow-headers")]
    [InlineData("download-redirect-body")]
    [InlineData("download-redirect-deadline")]
    [InlineData("download-same-origin-auth")]
    [InlineData("download-two-origin-auth")]
    [InlineData("download-default-ports")]
    [InlineData("download-https-downgrade")]
    [InlineData("download-uppercase-https")]
    [InlineData("download-unsafe-redirect")]
    [InlineData("download-resolver-stall")]
    [InlineData("download-late-socket")]
    [InlineData("download-candidate-remaining-budget")]
    [InlineData("download-candidate-exhausted-budget")]
    public async Task PublishedArtifactsRequireTheCompleteExactSourceSet(string scenario)
    {
        if (OperatingSystem.IsWindows()) { Assert.Skip("Publication fixtures require python3 and Git."); }
        ProcessStartInfo start = new("python3")
        {
            WorkingDirectory = CiTestPaths.RepositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(Path.Combine(CiTestPaths.RepositoryRoot, "tests/Hexalith.Parties.Ci.Tests/release_artifact_cases.py"));
        start.ArgumentList.Add(CiTestPaths.RepositoryRoot);
        start.ArgumentList.Add(scenario);
        using Process process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        Task<string> error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(25));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(entireProcessTree: true); throw; }
        process.ExitCode.ShouldBe(0, await output.ConfigureAwait(true) + await error.ConfigureAwait(true));
    }

    /// <summary>The read-only verifier pins both sources and always retains evidence, without publishing.</summary>
    [Fact]
    public void WorkflowVerifiesBothRegistriesWithThePinnedSharedValidator()
    {
        string workflow = CiTestPaths.ReadRepoFile(".github/workflows/release.yml");
        string verification = workflow.Split("  verify-publication:", StringSplitOptions.None)[1];
        verification.ShouldContain("matching-tag: ${{ steps.published.outputs.matching-tag }}");
        verification.ShouldContain("ref: ${{ github.sha }}");
        verification.ShouldContain("ref: d9070aa0d43feb9e3359286bc797d2be65960727");
        verification.ShouldContain("submodules: false");
        verification.ShouldContain("contents: read");
        verification.ShouldContain("run: git -c submodule.recurse=false submodule update --init references/Hexalith.Builds");
        verification.ShouldContain("global-json-file: global.json");
        verification.ShouldNotContain("contents: write");
        verification.ShouldNotContain("NUGET_API_KEY");
        verification.ShouldContain("python3 scripts/verify-release-publication.py");
        verification.ShouldContain("--package-manifest tools/release-packages.json");
        verification.ShouldContain("--oci-validator .hexalith/builds-verification/Github/publish-containers/oci_registry_validator.py");
        verification.ShouldContain("if: ${{ always() }}");
        verification.ShouldContain("path: .hexalith/publication-verification/**");
    }
}
