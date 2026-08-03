using System.Xml.Linq;

namespace Hexalith.Parties.Ci.Tests;

public sealed class StandaloneSolutionTests
{
    private const string GatewayTestHostProject =
        "tests/Hexalith.Parties.EventStoreGateway.TestHost/Hexalith.Parties.EventStoreGateway.TestHost.csproj";

    private static readonly string[] ExpectedOwnedProjects =
    [
        "samples/Hexalith.Parties.Sample/Hexalith.Parties.Sample.csproj",
        "src/Hexalith.Parties.AdminPortal/Hexalith.Parties.AdminPortal.csproj",
        "src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj",
        "src/Hexalith.Parties.Authentication/Hexalith.Parties.Authentication.csproj",
        "src/Hexalith.Parties.Client/Hexalith.Parties.Client.csproj",
        "src/Hexalith.Parties.ConsumerPortal/Hexalith.Parties.ConsumerPortal.csproj",
        "src/Hexalith.Parties.Contracts/Hexalith.Parties.Contracts.csproj",
        "src/Hexalith.Parties.Mcp/Hexalith.Parties.Mcp.csproj",
        "src/Hexalith.Parties.Picker/Hexalith.Parties.Picker.csproj",
        "src/Hexalith.Parties.Projections/Hexalith.Parties.Projections.csproj",
        "src/Hexalith.Parties.Security/Hexalith.Parties.Security.csproj",
        "src/Hexalith.Parties.Testing/Hexalith.Parties.Testing.csproj",
        "src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj",
        "src/Hexalith.Parties/Hexalith.Parties.csproj",
        "tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj",
        "tests/Hexalith.Parties.Authentication.Tests/Hexalith.Parties.Authentication.Tests.csproj",
        "tests/Hexalith.Parties.Ci.Tests/Hexalith.Parties.Ci.Tests.csproj",
        "tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj",
        "tests/Hexalith.Parties.ConsumerPortal.Tests/Hexalith.Parties.ConsumerPortal.Tests.csproj",
        "tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj",
        GatewayTestHostProject,
        "tests/Hexalith.Parties.IntegrationTests/Hexalith.Parties.IntegrationTests.csproj",
        "tests/Hexalith.Parties.Mcp.Tests/Hexalith.Parties.Mcp.Tests.csproj",
        "tests/Hexalith.Parties.Picker.Tests/Hexalith.Parties.Picker.Tests.csproj",
        "tests/Hexalith.Parties.Projections.Tests/Hexalith.Parties.Projections.Tests.csproj",
        "tests/Hexalith.Parties.Sample.Tests/Hexalith.Parties.Sample.Tests.csproj",
        "tests/Hexalith.Parties.Security.Tests/Hexalith.Parties.Security.Tests.csproj",
        "tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj",
        "tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj",
        "tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj",
    ];

    [Fact]
    public void StandaloneSolutionContainsEveryOwnedProjectAndNoReferenceEntries()
    {
        XDocument solution = XDocument.Load(CiTestPaths.RepoFile("Hexalith.Parties.Standalone.slnx"));
        string[] projects = solution.Descendants("Project")
            .Select(static project => NormalizePath(project.Attribute("Path")?.Value))
            .Order(StringComparer.Ordinal)
            .ToArray();

        projects.ShouldBe(ExpectedOwnedProjects.Order(StringComparer.Ordinal));
        solution.Descendants()
            .Where(static entry => entry.Name.LocalName is "Project" or "File")
            .Select(static entry => NormalizePath(entry.Attribute("Path")?.Value))
            .ShouldAllBe(static path => !path.StartsWith("references/", StringComparison.Ordinal));
    }

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

    [Fact]
    public void AppHostUsesPathsForExternalTopologyProjects()
    {
        XDocument appHost = XDocument.Load(CiTestPaths.RepoFile("src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj"));
        string[] externalProjectReferences = appHost.Descendants("ProjectReference")
            .Select(static reference => (string?)reference.Attribute("Include") ?? string.Empty)
            .Where(static include => include.Contains("HexalithEventStoreRoot", StringComparison.Ordinal)
                || include.Contains("HexalithTenantsRoot", StringComparison.Ordinal))
            .Where(static include => !include.EndsWith(
                @"\Hexalith.EventStore.Aspire\Hexalith.EventStore.Aspire.csproj",
                StringComparison.Ordinal))
            .ToArray();
        string program = CiTestPaths.ReadRepoFile("src/Hexalith.Parties.AppHost/Program.cs");

        externalProjectReferences.ShouldBeEmpty();
        program.ShouldContain("ResolveRequiredReferenceProjectPath");
        program.ShouldContain("builder.AddProject(\"eventstore\", eventStoreProjectPath)");
        program.ShouldContain("builder.AddProject(\"eventstore-admin\", adminServerProjectPath)");
        program.ShouldContain("builder.AddProject(\"eventstore-admin-ui\", adminUiProjectPath)");
        program.ShouldContain("builder.AddProject(\"tenants\", tenantsProjectPath)");
    }

    private static string NormalizePath(string? path) =>
        (path ?? throw new InvalidDataException("A solution entry is missing its Path attribute."))
            .Replace('\\', '/');
}
