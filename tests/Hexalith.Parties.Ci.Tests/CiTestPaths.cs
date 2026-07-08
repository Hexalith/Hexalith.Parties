namespace Hexalith.Parties.Ci.Tests;

internal static class CiTestPaths
{
    public static string RepositoryRoot
    {
        get
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
        }
    }

    public static string RepoFile(string relativePath) => Path.Combine(RepositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

    public static string ReadRepoFile(string relativePath) => File.ReadAllText(RepoFile(relativePath));
}
