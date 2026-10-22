using System.Diagnostics;

namespace Hexalith.Parties.Ci.Tests;

/// <summary>Exercises main-push root pointer enforcement against real temporary Git histories.</summary>
public sealed class GitlinkGateWorkflowTests
{
    /// <summary>Checks each pointer in the complete event range and fails closed on unusable bases.</summary>
    /// <param name="scenario">The isolated pointer, ledger and base fixture.</param>
    [Theory]
    [InlineData("unchanged")]
    [InlineData("unsigned")]
    [InlineData("signed")]
    [InlineData("stale")]
    [InlineData("placeholder")]
    [InlineData("reset")]
    [InlineData("multi-unsigned")]
    [InlineData("temporary-unsigned")]
    [InlineData("multi-signed")]
    [InlineData("missing-base")]
    [InlineData("zero-base")]
    [InlineData("invalid-base")]
    [InlineData("nonancestor")]
    [InlineData("undeclared-bump")]
    [InlineData("malformed-declarations")]
    [InlineData("removed-gitlink")]
    [InlineData("gitlink-mode-change")]
    [InlineData("empty-base")]
    [InlineData("diff-signed")]
    [InlineData("diff-unsigned")]
    [InlineData("delayed-declaration-unsigned")]
    [InlineData("delayed-declaration-signed")]
    [InlineData("merge-inherited")]
    [InlineData("merge-branch-unsigned")]
    [InlineData("merge-branch-signed")]
    [InlineData("merge-resolution-unsigned")]
    [InlineData("merge-resolution-signed")]
    [InlineData("workflow-main-signed")]
    [InlineData("workflow-main-unchanged")]
    [InlineData("workflow-main-unsigned")]
    [InlineData("workflow-main-multi-unsigned")]
    [InlineData("workflow-main-missing-base")]
    [InlineData("workflow-main-zero-base")]
    [InlineData("workflow-main-invalid-base")]
    [InlineData("workflow-main-nonancestor")]
    [InlineData("workflow-main-empty-base")]
    [InlineData("workflow-rc-signed")]
    [InlineData("workflow-rc-unsigned")]
    [InlineData("workflow-tag-signed")]
    [InlineData("workflow-manual-signed")]
    [InlineData("workflow-manual-unsigned")]
    [InlineData("workflow-pr-signed")]
    public async Task GateEnforcesEveryRootPointerInTheEventRange(string scenario)
    {
        if (OperatingSystem.IsWindows()) { Assert.Skip("The gitlink gate requires bash."); }
        ProcessStartInfo start = new("python3")
        {
            WorkingDirectory = CiTestPaths.RepositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(Path.Combine(CiTestPaths.RepositoryRoot, "tests/Hexalith.Parties.Ci.Tests/gitlink_gate_cases.py"));
        start.ArgumentList.Add(CiTestPaths.RepositoryRoot);
        start.ArgumentList.Add(scenario);
        using Process process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        Task<string> error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(entireProcessTree: true); throw; }
        process.ExitCode.ShouldBe(0, await output.ConfigureAwait(true) + await error.ConfigureAwait(true));
    }

    /// <summary>Ordinary main pushes use event.before while existing RC, tag and manual routes stay available.</summary>
    [Fact]
    public void MainPushUsesValidatedEventBeforeInsteadOfTheNewMainTip()
    {
        string workflow = CiTestPaths.ReadRepoFile(".github/workflows/rc-gate.yml");
        workflow.ShouldContain("- main");
        workflow.ShouldContain("group: ${{ github.workflow }}-${{ github.head_ref || github.ref }}-${{ github.event_name == 'push' && github.ref == 'refs/heads/main' && github.sha || 'latest' }}");
        workflow.ShouldContain("- 'rc/**'");
        workflow.ShouldContain("- 'release/**'");
        workflow.ShouldContain("- 'v*'");
        workflow.ShouldContain("EVENT_BEFORE: ${{ github.event.before }}");
        workflow.ShouldContain("git merge-base --is-ancestor \"$base\" HEAD");
        workflow.ShouldContain("mode=push-range");
        workflow.ShouldContain("submodules: false");
        workflow.ShouldContain("run: bash scripts/gitlink-rc-gate.sh \"--$GATE_MODE\" \"$GATE_BASE\"");
    }
}
