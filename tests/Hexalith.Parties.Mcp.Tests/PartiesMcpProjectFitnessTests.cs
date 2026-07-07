using System.Xml.Linq;

using Shouldly;

namespace Hexalith.Parties.Mcp.Tests;

public sealed class PartiesMcpProjectFitnessTests
{
    [Fact]
    public void McpProjectReferencesOnlyAcceptedProductionBoundaries()
    {
        string project = File.ReadAllText(Path.Combine(
            RepositoryRoot.Locate(),
            "src",
            "Hexalith.Parties.Mcp",
            "Hexalith.Parties.Mcp.csproj"));
        XDocument document = XDocument.Parse(project);
        string[] projectReferences =
        [
            .. document
                .Descendants("ProjectReference")
                .Select(reference => reference.Attribute("Include")?.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .OrderBy(value => value),
        ];
        string[] packageReferences =
        [
            .. document
                .Descendants("PackageReference")
                .Select(reference => reference.Attribute("Include")?.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .OrderBy(value => value),
        ];

        projectReferences.ShouldBe(
            [
                "..\\Hexalith.Parties.Client\\Hexalith.Parties.Client.csproj",
                "..\\Hexalith.Parties.Contracts\\Hexalith.Parties.Contracts.csproj",
                "$(HexalithCommonsRoot)\\src\\libraries\\Hexalith.Commons.ServiceDefaults\\Hexalith.Commons.ServiceDefaults.csproj",
                "$(HexalithCommonsRoot)\\src\\libraries\\Hexalith.Commons.UniqueIds\\Hexalith.Commons.UniqueIds.csproj",
            ],
            ignoreOrder: true);
        packageReferences.ShouldBe(["Hexalith.Commons.ServiceDefaults", "Hexalith.Commons.UniqueIds", "ModelContextProtocol.AspNetCore"], ignoreOrder: true);

        string[] forbidden =
        [
            "Hexalith.Parties\\Hexalith.Parties.csproj",
            "Hexalith.Parties.Server",
            "Hexalith.Parties.Projections",
            "Hexalith.Parties.Security",
            "Dapr.",
            "MediatR",
            "FluentValidation",
            "Microsoft.AspNetCore.Mvc",
            "Swashbuckle",
        ];

        IEnumerable<string> violations = forbidden.Where(project.Contains);
        violations.ShouldBeEmpty("The MCP host must remain a thin consumer over the typed client boundary.");
    }

    [Fact]
    public void McpStartupUsesStatelessHttpTransportAndSeparateMapMcp()
    {
        string program = File.ReadAllText(Path.Combine(
            RepositoryRoot.Locate(),
            "src",
            "Hexalith.Parties.Mcp",
            "Program.cs"));

        program.ShouldContain("AddHexalithServiceDefaults(ConfigurePartiesServiceDefaults)");
        program.ShouldContain("AddMcpServer()");
        program.ShouldContain("WithHttpTransport(options => options.Stateless = true)");
        program.ShouldContain("WithToolsFromAssembly()");
        program.ShouldContain("app.MapMcp()");
        program.ShouldContain("app.MapHexalithDefaultEndpoints(ConfigurePartiesServiceDefaults)");
        program.ShouldContain("RegisterDefaultSelfCheck = false");
        program.ShouldContain("ActivitySourceNames.Add(\"Hexalith.Parties\")");
    }

    [Fact]
    public void AppHostWiresPartiesMcpAsSeparateResource()
    {
        string program = File.ReadAllText(Path.Combine(
            RepositoryRoot.Locate(),
            "src",
            "Hexalith.Parties.AppHost",
            "Program.cs"));

        program.ShouldContain("AddProject<Projects.Hexalith_Parties_Mcp>(\"parties-mcp\")");
        program.ShouldContain("partiesMcp");
        program.ShouldContain("WithReference(eventStore)");
        program.ShouldContain("WaitFor(eventStore)");
        program.ShouldContain("WithReference(parties)");
        program.ShouldContain("WaitFor(parties)");
    }

    [Fact]
    public void McpSourceDoesNotReferenceForbiddenActorHostOrServerInternals()
    {
        string sourceRoot = Path.Combine(RepositoryRoot.Locate(), "src", "Hexalith.Parties.Mcp");
        string combinedSource = string.Join(
            Environment.NewLine,
            Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));

        string[] forbidden =
        [
            "ICommandRouter",
            "IActorProxyFactory",
            "IPartySearchService",
            "ITenantAccessService",
            "TenantAccessDenialTranslator",
            "Hexalith.Parties.Projections",
            "Hexalith.Parties.Server",
            "Hexalith.Parties.Security",
            "Dapr.",
            "MediatR",
            "FluentValidation",
            "ControllerBase",
        ];

        IEnumerable<string> violations = forbidden.Where(combinedSource.Contains);
        violations.ShouldBeEmpty("The MCP host scaffold must not depend on retired Parties REST/MCP internals.");
    }

    [Fact]
    public void McpToolSourceDoesNotSurfaceRawErrorsSecretsOrClaimPayloads()
    {
        string source = File.ReadAllText(Path.Combine(
            RepositoryRoot.Locate(),
            "src",
            "Hexalith.Parties.Mcp",
            "Tools",
            "PartiesMcpTools.cs"));

        string[] forbidden =
        [
            "ProblemDetails",
            "ex.Detail",
            "ex.Message",
            "ClaimsPrincipal",
            "ClaimsIdentity",
            "FindAll(",
            "Request.Headers",
            "Authorization\"",
            "Bearer",
        ];

        IEnumerable<string> violations = forbidden.Where(source.Contains);
        violations.ShouldBeEmpty("MCP tool responses must stay bounded and avoid raw errors, secrets, or claims payloads.");
    }
}
