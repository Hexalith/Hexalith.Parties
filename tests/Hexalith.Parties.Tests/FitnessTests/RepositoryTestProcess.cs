using Hexalith.Parties.PackageTests;

using Shouldly;

namespace Hexalith.Parties.Tests.FitnessTests;

/// <summary>Provides bounded Git queries and MSBuild evaluations for repository fitness tests.</summary>
internal static class RepositoryTestProcess
{
    // Preserve the existing platform prerequisite evaluation allowance.
    private const int ProcessTimeoutMilliseconds = 300_000;

    /// <summary>Returns unmodified stdout and fails with captured diagnostics on a nonzero exit.</summary>
    internal static string RunProcess(string root, string fileName, params string[] arguments)
    {
        PackageTestProcessResult result = Run(root, fileName, arguments);
        result.ExitCode.ShouldBe(0, result.Diagnostic);
        return result.StandardOutput;
    }

    /// <summary>Runs a Git query that must succeed.</summary>
    internal static string RunGit(string root, params string[] arguments)
        => RunProcess(root, "git", arguments);

    /// <summary>Returns false for a nonzero Git exit; startup and timeout failures still throw.</summary>
    internal static bool TryRunGit(string root, out string output, params string[] arguments)
    {
        PackageTestProcessResult result = Run(root, "git", arguments);
        output = result.StandardOutput;
        return result.ExitCode == 0;
    }

    /// <summary>Uses the shared runner with a literal argument vector and the fitness-test deadline.</summary>
    private static PackageTestProcessResult Run(string root, string fileName, string[] arguments)
    {
        string[] selected = arguments.ToArray();
        if (fileName == "git")
        {
            for (int index = 0; index < selected.Length - 1; index++)
            {
                if (selected[index] == "-C")
                {
                    selected[index + 1] = RepositoryDependencyPaths.Resolve(root, selected[index + 1]);
                }
            }
        }
        return PackageTestProcess.Run(fileName, selected, root, ProcessTimeoutMilliseconds);
    }
}
