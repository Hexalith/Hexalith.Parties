using Shouldly;

namespace Hexalith.Parties.Tests.FitnessTests;

/// <summary>Checks the repository adapter's observable Git behavior.</summary>
public sealed class RepositoryTestProcessTests
{
    [Fact]
    public void RunGitAndTryRunGitPreserveSuccessfulOutput()
    {
        string root = RepositoryRoot.Locate();
        string output = RepositoryTestProcess.RunGit(root, "rev-parse", "--show-toplevel");

        RepositoryTestProcess.TryRunGit(root, out string optionalOutput, "rev-parse", "--show-toplevel")
            .ShouldBeTrue();
        optionalOutput.ShouldBe(output);
        output.Trim().ShouldBe(root.Replace('\\', '/'));
        output.ShouldEndWith("\n");
    }

    [Fact]
    public void TryRunGitReturnsFalseForMissingHistory()
    {
        RepositoryTestProcess.TryRunGit(
            RepositoryRoot.Locate(), out string output, "cat-file", "-e", "DW90-missing-revision^{commit}")
            .ShouldBeFalse();
        output.ShouldBeEmpty();
    }

    [Fact]
    public void TryRunGitPreservesOutputFromFailedCommands()
    {
        string root = RepositoryRoot.Locate();
        string head = RepositoryTestProcess.RunGit(root, "rev-parse", "HEAD");
        RepositoryTestProcess.TryRunGit(root, out string output, "rev-parse", "HEAD", "DW90-missing-revision")
            .ShouldBeFalse();
        output.ShouldBe(head + "DW90-missing-revision\n");
    }

    [Fact]
    public void RunGitFailsWithCapturedDiagnosticsForMissingHistory()
    {
        ShouldAssertException exception = Should.Throw<ShouldAssertException>(() =>
            RepositoryTestProcess.RunGit(
                RepositoryRoot.Locate(), "cat-file", "-e", "DW90-missing-revision^{commit}"));

        exception.Message.ShouldContain("DW90-missing-revision");
    }

    [Fact]
    public void RunProcessPreservesArgumentBoundariesAndOutput()
    {
        const string argument = "  value with spaces and \"quotes\"  ";
        RepositoryTestProcess.RunProcess(
            RepositoryRoot.Locate(), "git", "-c", $"dw90.literal={argument}", "config", "--get", "dw90.literal")
            .ShouldBe(argument + "\n");
    }

    [Fact]
    public void RunProcessFailsOnStartRatherThanReturningAnEmptyResult()
    {
        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() =>
            RepositoryTestProcess.RunProcess(RepositoryRoot.Locate(), "DW90-missing-executable"));

        exception.Message.ShouldContain("Could not start DW90-missing-executable");
    }
}
