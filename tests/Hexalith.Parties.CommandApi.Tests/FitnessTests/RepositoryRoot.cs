namespace Hexalith.Parties.CommandApi.Tests.FitnessTests;

internal static class RepositoryRoot
{
    private const string SolutionFileName = "Hexalith.Parties.slnx";

    public static string Locate()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate repository root: '{SolutionFileName}' was not found above '{AppContext.BaseDirectory}'.");
    }

    public static string ProjectFile(string projectName)
        => Path.Combine(Locate(), "src", projectName, projectName + ".csproj");
}
