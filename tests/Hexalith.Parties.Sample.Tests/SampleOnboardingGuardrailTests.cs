using System.Xml.Linq;

using Shouldly;

namespace Hexalith.Parties.Sample.Tests;

public sealed class SampleOnboardingGuardrailTests
{
    [Fact]
    public void CurrentAdopterDocsAndSampleProduction_DoNotAdvertiseRetiredPartiesSurfaces()
    {
        string[] scannedFiles =
        [
            "README.md",
            "docs/getting-started.md",
            "samples/Hexalith.Parties.Sample/Program.cs",
            "samples/Hexalith.Parties.Sample/appsettings.json",
            "samples/Hexalith.Parties.Sample/Properties/launchSettings.json",
        ];

        string[] forbiddenLiterals =
        [
            "api/v1/parties",
            "api/v1/admin",
            "openapi/v1.json",
            "Swagger",
            "X-GDPR-Warning",
            "https://localhost:5001/mcp",
            "http://localhost:5000/mcp",
        ];

        foreach (string relativePath in scannedFiles)
        {
            string text = File.ReadAllText(GetRepositoryFilePath(relativePath));

            foreach (string forbiddenLiteral in forbiddenLiterals)
            {
                text.ShouldNotContain(forbiddenLiteral);
            }
        }
    }

    [Fact]
    public void SampleTests_DoNotAssertRetiredPartiesRoutes()
    {
        string testsRoot = GetRepositoryFilePath("tests/Hexalith.Parties.Sample.Tests");
        string[] forbiddenLiterals =
        [
            "api/v1/parties",
            "api/v1/admin",
            "openapi/v1.json",
            "X-GDPR-Warning",
            "https://localhost:5001/mcp",
            "http://localhost:5000/mcp",
        ];

        foreach (string file in Directory.EnumerateFiles(testsRoot, "*.cs", SearchOption.AllDirectories)
                     .Where(static file => !file.EndsWith(nameof(SampleOnboardingGuardrailTests) + ".cs", StringComparison.Ordinal)))
        {
            string text = File.ReadAllText(file);

            foreach (string forbiddenLiteral in forbiddenLiterals)
            {
                text.ShouldNotContain(forbiddenLiteral);
            }
        }
    }

    [Fact]
    public void SampleProjectReferences_StayWithinApprovedConsumerBoundary()
    {
        string projectPath = GetRepositoryFilePath("samples/Hexalith.Parties.Sample/Hexalith.Parties.Sample.csproj");
        XDocument project = XDocument.Load(projectPath);

        string[] projectReferences = project
            .Descendants("ProjectReference")
            .Select(static reference => reference.Attribute("Include")?.Value.Replace('\\', '/'))
            .Where(static include => !string.IsNullOrWhiteSpace(include))
            .Cast<string>()
            .ToArray();

        projectReferences.ShouldAllBe(
            static include =>
                include.EndsWith("src/Hexalith.Parties.Client/Hexalith.Parties.Client.csproj", StringComparison.Ordinal)
                || include.EndsWith("src/Hexalith.Parties.Contracts/Hexalith.Parties.Contracts.csproj", StringComparison.Ordinal)
                || include.EndsWith("src/Hexalith.Parties.ServiceDefaults/Hexalith.Parties.ServiceDefaults.csproj", StringComparison.Ordinal),
            "Sample production project references must stay limited to approved consumer packages.");

        string[] packageReferences = project
            .Descendants("PackageReference")
            .Select(static reference => reference.Attribute("Include")?.Value)
            .Where(static include => !string.IsNullOrWhiteSpace(include))
            .Cast<string>()
            .ToArray();

        packageReferences.ShouldAllBe(
            static include => include == "Dapr.AspNetCore",
            "Sample production packages should only include subscriber-owned DAPR ASP.NET Core support.");
    }

    [Fact]
    public void Docs_NameEventStoreGatewayPartiesActorHostAndSeparateMcpHost()
    {
        string readme = File.ReadAllText(GetRepositoryFilePath("README.md"));
        string gettingStarted = File.ReadAllText(GetRepositoryFilePath("docs/getting-started.md"));
        string combined = readme + Environment.NewLine + gettingStarted;

        combined.ShouldContain("EventStore");
        combined.ShouldContain("POST /api/v1/commands");
        combined.ShouldContain("POST /api/v1/queries");
        combined.ShouldContain("Domain=\"party\"");
        combined.ShouldContain("`eventstore`");
        combined.ShouldContain("`eventstore-admin`");
        combined.ShouldContain("`eventstore-admin-ui`");
        combined.ShouldContain("`parties`");
        combined.ShouldContain("`tenants`");
        combined.ShouldContain("parties-mcp");
        combined.ShouldContain("EventStore owns public authentication, tenant validation, RBAC, command/query routing, and generic response mapping");
        combined.ShouldContain("Parties owns domain execution");
    }

    [Fact]
    public void GettingStartedRunnableSnippets_UseAcceptedGatewayShape()
    {
        string gettingStarted = File.ReadAllText(GetRepositoryFilePath("docs/getting-started.md"));

        gettingStarted.ShouldContain("\"domain\": \"party\"");
        gettingStarted.ShouldContain("\"commandType\": \"Hexalith.Parties.Contracts.Commands.CreateParty\"");
        gettingStarted.ShouldContain("\"queryType\": \"PartyDetail\"");
        gettingStarted.ShouldContain("\"queryType\": \"PartySearch\"");
        gettingStarted.ShouldContain("\"queryType\": \"PartyIndex\"");
        gettingStarted.ShouldContain("\"payload\"");
        gettingStarted.ShouldNotContain("contract_unavailable");
        gettingStarted.ShouldNotContain("Story 12.5 is blocked");
        gettingStarted.ShouldNotContain("Story 12.6 is blocked");
    }

    [Fact]
    public void SampleConfiguration_PointsClientAtEventStoreGatewayAndTenant()
    {
        string program = File.ReadAllText(GetRepositoryFilePath("samples/Hexalith.Parties.Sample/Program.cs"));
        string appSettings = File.ReadAllText(GetRepositoryFilePath("samples/Hexalith.Parties.Sample/appsettings.json"));

        program.ShouldContain("EventStore gateway URL");
        program.ShouldContain("Parties:Tenant");
        program.ShouldContain("parties-mcp host");
        appSettings.ShouldContain("\"BaseUrl\"");
        appSettings.ShouldContain("\"Tenant\": \"tenant-a\"");
    }

    private static string GetRepositoryFilePath(string relativePath)
        => Path.Combine(GetRepositoryRoot(), relativePath);

    private static string GetRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Hexalith.Parties.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root.");
    }
}
