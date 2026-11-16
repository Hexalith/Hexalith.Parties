using Hexalith.Parties.PackageTests;

using Shouldly;

namespace Hexalith.Parties.Tests.FitnessTests;

/// <summary>Checks the actual product graph's qualified default and explicit compatibility selection.</summary>
public sealed class RetainedCatalogViewTests
{
    /// <summary>Every actual project keeps package mode and honors the selected SDK family.</summary>
    [Fact]
    public void ActualProjectsUseTheQualifiedDefaultAndPreserveExplicitCompatibilitySelection()
    {
        string root = RepositoryRoot.Locate();
        string[] projects = new[] { "src", "samples", "tests" }
            .SelectMany(directory => Directory.GetFiles(Path.Combine(root, directory), "*.csproj", SearchOption.AllDirectories))
            .Where(path => !Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar).Any(segment => segment is "bin" or "obj"))
            .Select(path => Path.GetRelativePath(root, path))
            .Order(StringComparer.Ordinal)
            .ToArray();
        projects.ShouldNotBeEmpty();
        projects.Select(path => Path.GetFileNameWithoutExtension(path)!).Distinct(StringComparer.Ordinal).Count().ShouldBe(projects.Length);
        var properties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["UseHexalithProjectReferences"] = "false",
            ["UseNuGetDeps"] = "true",
        };
        foreach (string project in projects)
        {
            IReadOnlyDictionary<string, string> current = EffectivePackageVersions.ReadPackages(root, project,
                configuration: "Release", isolateEnvironment: true, globalProperties: properties);
            current["Hexalith.EventStore.Contracts"].ShouldBe("5.1.1", project);
            properties["HexalithEventStoreVersion"] = "4.2.0";
            IReadOnlyDictionary<string, string> compatibility = EffectivePackageVersions.ReadPackages(root, project,
                configuration: "Release", isolateEnvironment: true, globalProperties: properties);
            compatibility["Hexalith.EventStore.Contracts"].ShouldBe("4.2.0", project);
            properties.Remove("HexalithEventStoreVersion");
        }
    }
}
