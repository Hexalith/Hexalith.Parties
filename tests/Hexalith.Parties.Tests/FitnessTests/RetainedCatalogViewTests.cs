using System.Xml.Linq;

using Hexalith.Parties.PackageTests;

using Shouldly;

namespace Hexalith.Parties.Tests.FitnessTests;

/// <summary>Separates the measured Parties package view from the producer's current SDK default.</summary>
public sealed class RetainedCatalogViewTests
{
    /// <summary>Every current Parties project independently evaluates the retained SDK and OpenAPI ABI.</summary>
    [Fact]
    public void CurrentPartiesProjectsUseTheMeasuredRetainedCatalogView()
    {
        string root = RepositoryRoot.Locate();
        string[] projects = new[] { "src", "samples", "tests" }
            .SelectMany(directory => Directory.GetFiles(Path.Combine(root, directory), "*.csproj", SearchOption.AllDirectories))
            .Where(path => !Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar).Any(segment => segment is "bin" or "obj"))
            .Select(path => Path.GetRelativePath(root, path))
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] names = projects.Select(path => Path.GetFileNameWithoutExtension(path)!).ToArray();
        names.Length.ShouldBe(40, "The measured project cohort must be reconsidered when an owning project is added or removed.");
        names.Distinct(StringComparer.Ordinal).Count().ShouldBe(names.Length);
        IReadOnlyDictionary<string, string> packageMode = new Dictionary<string, string>
        {
            ["UseHexalithProjectReferences"] = "false",
            ["UseNuGetDeps"] = "true",
        };
        foreach (string project in projects)
        {
            string name = Path.GetFileNameWithoutExtension(project);
            AssertCatalog(root, name, "4.2.0", "10.0.15", "2.13.0");
            IReadOnlyDictionary<string, string> packages = EffectivePackageVersions.ReadPackages(root, project,
                configuration: "Release", isolateEnvironment: true, globalProperties: packageMode);
            AssertPackages(packages, project, "4.2.0", "10.0.15", "2.13.0");
        }
    }

    /// <summary>Related spelling or a future project name does not acquire retained compatibility.</summary>
    /// <param name="name">An unqualified project name.</param>
    [Theory]
    [InlineData("Hexalith.Parties.Future")]
    [InlineData("Hexalith.PartiesClient")]
    [InlineData("Unrelated")]
    public void UnknownProjectsKeepTheCurrentCatalogDefault(string name)
        => AssertCatalog(RepositoryRoot.Locate(), name, "5.0.0", "11.0.0", "3.11.0");

    private static void AssertPackages(IReadOnlyDictionary<string, string> packages, string project, string sdk, string aspNetOpenApi, string openApi)
    {
        packages["Hexalith.EventStore.Contracts"].ShouldBe(sdk, project);
        packages["Microsoft.AspNetCore.OpenApi"].ShouldBe(aspNetOpenApi, project);
        packages["Microsoft.OpenApi"].ShouldBe(openApi, project);
    }

    private static void AssertCatalog(string root, string name, string sdk, string aspNetOpenApi, string openApi)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"parties-retained-catalog-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string project = Path.Combine(directory, name + ".proj");
            string catalog = RepositoryDependencyPaths.Resolve(root, "references/Hexalith.Builds/Props/Directory.Packages.props");
            new XDocument(new XElement("Project", new XElement("Import", new XAttribute("Project", catalog)))).Save(project);
            IReadOnlyDictionary<string, string> packages = EffectivePackageVersions.ReadPackages(root, project, isolateEnvironment: true);
            AssertPackages(packages, name, sdk, aspNetOpenApi, openApi);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
