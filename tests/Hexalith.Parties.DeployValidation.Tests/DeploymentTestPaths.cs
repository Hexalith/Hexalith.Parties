namespace Hexalith.Parties.DeployValidation.Tests;

internal static class DeploymentTestPaths
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

    public static string DeployDirectory => Path.Combine(RepositoryRoot, "deploy");

    public static string DaprDirectory => Path.Combine(DeployDirectory, "dapr");

    public static string K8sDirectory => Path.Combine(DeployDirectory, "k8s");

    public static string FixturesDirectory => Path.Combine(RepositoryRoot, "tests", "Hexalith.Parties.DeployValidation.Tests", "Fixtures");

    public static string RepoFile(string relativePath) => Path.Combine(RepositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

    public static string ReadRepoFile(string relativePath) => File.ReadAllText(RepoFile(relativePath));
}
