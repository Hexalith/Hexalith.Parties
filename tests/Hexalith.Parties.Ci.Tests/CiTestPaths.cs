using System.Reflection;

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

    public static string RepoFile(string relativePath) => Hexalith.Parties.PackageTests.RepositoryDependencyPaths.Resolve(RepositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

    public static string ReadRepoFile(string relativePath) => File.ReadAllText(RepoFile(relativePath));
    /// <summary>Gets the explicit SDK artifact root selected for this test build, if any.</summary>
    public static string? ArtifactsPath => typeof(CiTestPaths).Assembly
        .GetCustomAttributes<System.Reflection.AssemblyMetadataAttribute>()
        .Single(attribute => attribute.Key == "HexalithTestArtifactsPath").Value is { Length: > 0 } path ? path : null;

    /// <summary>Resolves the actual restore output in either supported SDK output layout.</summary>
    /// <param name="relativeProjectDirectory">The explicitly declared owner project directory.</param>
    public static string ProjectAssetsFile(string relativeProjectDirectory)
        => ArtifactsPath is { } artifacts
            ? Path.Combine(artifacts, "obj", Path.GetFileName(relativeProjectDirectory), "project.assets.json")
            : RepoFile(relativeProjectDirectory + "/obj/project.assets.json");

    /// <summary>Resolves a built output in either supported SDK output layout.</summary>
    /// <param name="relativeProjectDirectory">The explicitly declared owner project directory.</param>
    /// <param name="configuration">The actual test assembly build configuration.</param>
    /// <param name="fileName">The expected generated file name.</param>
    public static string ProjectOutputFile(string relativeProjectDirectory, string configuration, string fileName)
        => ArtifactsPath is { } artifacts
            ? Path.Combine(artifacts, "bin", Path.GetFileName(relativeProjectDirectory), configuration.ToLowerInvariant(), fileName)
            : RepoFile(relativeProjectDirectory + "/bin/" + configuration + "/net10.0/" + fileName);
}

