using System.Xml.Linq;

namespace Hexalith.Parties.Ci.Tests;

/// <summary>Verifies the complete package-mode solution and its external dependency boundaries.</summary>
public sealed class StandaloneSolutionTests
{
    private const string GatewayTestHostProject =
        "tests/Hexalith.Parties.EventStoreGateway.TestHost/Hexalith.Parties.EventStoreGateway.TestHost.csproj";

    /// <summary>Verifies that every owned project on disk is represented exactly once.</summary>
    [Fact]
    public void StandaloneSolutionContainsEveryOwnedProjectAndNoReferenceEntries()
    {
        XDocument solution = XDocument.Load(CiTestPaths.RepoFile("Hexalith.Parties.Standalone.slnx"));
        string[] projects = solution.Descendants("Project")
            .Select(static project => NormalizePath(project.Attribute("Path")?.Value))
            .Order(StringComparer.Ordinal)
            .ToArray();

        projects.ShouldBe(EnumerateOwnedProjectFiles());
        solution.Descendants()
            .Where(static entry => entry.Name.LocalName is "Project" or "File")
            .Select(static entry => NormalizePath(entry.Attribute("Path")?.Value))
            .ShouldAllBe(static path => !path.StartsWith("references/", StringComparison.Ordinal));
    }

    private static string[] EnumerateOwnedProjectFiles() =>
        [
            .. new[] { "samples", "src", "tests" }
                .SelectMany(root => Directory.EnumerateFiles(
                    CiTestPaths.RepoFile(root),
                    "*.csproj",
                    SearchOption.AllDirectories))
                .Where(path => !NormalizePath(Path.GetRelativePath(CiTestPaths.RepositoryRoot, path))
                    .Split('/')
                    .Any(segment => segment is "bin" or "obj"))
                .Select(path => NormalizePath(Path.GetRelativePath(CiTestPaths.RepositoryRoot, path)))
                .Order(StringComparer.Ordinal),
        ];

    /// <summary>Verifies that the canonical solution owns a package-built gateway host.</summary>
    [Fact]
    public void CanonicalSolutionIncludesOwnedPackageBuiltGatewayTestHost()
    {
        XDocument canonicalSolution = XDocument.Load(CiTestPaths.RepoFile("Hexalith.Parties.slnx"));
        canonicalSolution.Descendants("Project")
            .Select(static project => NormalizePath(project.Attribute("Path")?.Value))
            .ShouldContain(GatewayTestHostProject);

        XDocument gatewayHost = XDocument.Load(CiTestPaths.RepoFile(GatewayTestHostProject));
        gatewayHost.Descendants("ProjectReference").ShouldBeEmpty();
        gatewayHost.Descendants("PackageReference")
            .Single(reference => string.Equals(
                (string?)reference.Attribute("Include"),
                "Hexalith.EventStore.Gateway",
                StringComparison.Ordinal))
            .Attribute("Version")
            .ShouldBeNull();
    }

    /// <summary>Verifies that gateway tests use the production host in source mode and the owned host in package mode.</summary>
    [Fact]
    public void GatewayTestsSelectHostByDependencyMode()
    {
        XDocument testsProject = XDocument.Load(CiTestPaths.RepoFile(
            "tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj"));
        XElement[] aliasedHosts = testsProject.Descendants("ProjectReference")
            .Where(reference => reference.Elements("Aliases").Any(alias => alias.Value == "eventstore"))
            .ToArray();

        aliasedHosts.Length.ShouldBe(2);
        aliasedHosts.Any(reference =>
            ((string?)reference.Attribute("Include"))?.EndsWith(
                @"\Hexalith.EventStore\Hexalith.EventStore.csproj",
                StringComparison.Ordinal) == true
            && string.Equals(
                (string?)reference.Attribute("Condition"),
                "'$(HexalithEventStoreFromSource)' == 'true'",
                StringComparison.Ordinal)).ShouldBeTrue();
        aliasedHosts.Any(reference =>
            ((string?)reference.Attribute("Include"))?.EndsWith(
                @"\Hexalith.Parties.EventStoreGateway.TestHost.csproj",
                StringComparison.Ordinal) == true
            && string.Equals(
                (string?)reference.Attribute("Condition"),
                "'$(HexalithEventStoreFromSource)' != 'true'",
                StringComparison.Ordinal)).ShouldBeTrue();
    }

    /// <summary>Verifies that AppHost has no compile-time topology-host dependency except Aspire integration.</summary>
    [Fact]
    public void AppHostUsesPathsForExternalTopologyProjects()
    {
        XDocument appHost = XDocument.Load(CiTestPaths.RepoFile("src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj"));
        XElement[] projectReferences = appHost.Descendants("ProjectReference").ToArray();
        string[] includes = projectReferences
            .Select(static reference => (string?)reference.Attribute("Include") ?? string.Empty)
            .ToArray();
        XElement eventStoreAspire = projectReferences.Single(reference =>
            ((string?)reference.Attribute("Include"))?.Contains("Hexalith.EventStore.Aspire", StringComparison.Ordinal) == true);
        XElement eventStoreAspirePackage = appHost.Descendants("PackageReference").Single(reference =>
            string.Equals((string?)reference.Attribute("Include"), "Hexalith.EventStore.Aspire", StringComparison.Ordinal));
        string program = CiTestPaths.ReadRepoFile("src/Hexalith.Parties.AppHost/Program.cs");

        includes.ShouldBe(
        [
            @"$(HexalithEventStoreRoot)\src\Hexalith.EventStore.Aspire\Hexalith.EventStore.Aspire.csproj",
            @"..\Hexalith.Parties\Hexalith.Parties.csproj",
            @"..\Hexalith.Parties.Mcp\Hexalith.Parties.Mcp.csproj",
            @"..\Hexalith.Parties.UI\Hexalith.Parties.UI.csproj",
        ]);
        ((string?)eventStoreAspire.Attribute("Condition")).ShouldBe("'$(HexalithEventStoreFromSource)' == 'true'");
        ((string?)eventStoreAspirePackage.Attribute("Condition")).ShouldBe("'$(HexalithEventStoreFromSource)' != 'true'");
        eventStoreAspirePackage.Attribute("Version").ShouldBeNull();
        program.ShouldContain("ReferenceProjectResolver.ResolveRequired");
        program.ShouldContain("builder.AddProject(\"eventstore\", eventStoreProjectPath)");
        program.ShouldContain("builder.AddProject(\"eventstore-admin\", adminServerProjectPath)");
        program.ShouldContain("builder.AddProject(\"eventstore-admin-ui\", adminUiProjectPath)");
        program.ShouldContain("builder.AddProject(\"tenants\", tenantsProjectPath)");
    }

    private static string NormalizePath(string? path) =>
        (path ?? throw new InvalidDataException("A solution entry is missing its Path attribute."))
            .Replace('\\', '/');
}
