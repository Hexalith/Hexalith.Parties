using System.Diagnostics;

namespace Hexalith.Parties.Ci.Tests;

/// <summary>Exercises history preparation without changing root-selected sources or initializing nested dependencies.</summary>
public sealed class CiGitHistoryPreparationTests
{
    /// <summary>Uses actual local bare origins and shallow direct clones to preserve historical prerequisites and dirty work.</summary>
    /// <param name="scenario">The historical fetch or unavailable direct-checkout boundary.</param>
    [Theory]
    [InlineData("shallow")]
    [InlineData("full-no-tags")]
    [InlineData("missing-direct")]
    [InlineData("mismatched-head")]
    [InlineData("fetch-transient")]
    [InlineData("fetch-exhausted")]
    public async Task DirectHistoryPreparationPreservesSourceSelections(string scenario)
    {
        if (OperatingSystem.IsWindows()) { Assert.Skip("History preparation requires bash and timeout."); }
        ProcessStartInfo start = new("python3")
        {
            WorkingDirectory = CiTestPaths.RepositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(CiTestPaths.RepoFile("tests/Hexalith.Parties.Ci.Tests/ci_git_history_cases.py"));
        start.ArgumentList.Add(CiTestPaths.RepositoryRoot);
        start.ArgumentList.Add(scenario);
        using Process process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        Task<string> error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(entireProcessTree: true); throw; }
        process.ExitCode.ShouldBe(0, await output.ConfigureAwait(true) + await error.ConfigureAwait(true));
    }

    /// <summary>Both reusable callers prepare root-specific history before the unchanged Aspire package restore.</summary>
    [Fact]
    public void BothIsolatedTestCallersUseTheHistoricalPreparationHook()
    {
        foreach (string file in new[] { ".github/workflows/ci.yml", ".github/workflows/ci-lifecycle-qualification.yml" })
        {
            CiTestPaths.ReadRepoFile(file).ShouldContain("package-prepare-script: scripts/prepare-ci-package-dependencies.sh");
        }
        string prepare = CiTestPaths.ReadRepoFile("scripts/prepare-ci-package-dependencies.sh");
        prepare.ShouldContain("prepare-ci-git-history.sh");
        prepare.IndexOf("prepare-ci-git-history.sh", StringComparison.Ordinal)
            .ShouldBeLessThan(prepare.IndexOf("dotnet restore", StringComparison.Ordinal));
    }
}
