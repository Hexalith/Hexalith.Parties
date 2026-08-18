using System.Text.RegularExpressions;

using Shouldly;

namespace Hexalith.Parties.Tests.FitnessTests;

public sealed class DocumentationFitnessTests
{
    private static readonly string[] ExpectedSourceProjects =
    [
        "Hexalith.Parties",
        "Hexalith.Parties.AdminPortal",
        "Hexalith.Parties.AppHost",
        "Hexalith.Parties.Authentication",
        "Hexalith.Parties.Client",
        "Hexalith.Parties.ConsumerPortal",
        "Hexalith.Parties.Contracts",
        "Hexalith.Parties.Mcp",
        "Hexalith.Parties.Picker",
        "Hexalith.Parties.Projections",
        "Hexalith.Parties.Security",
        "Hexalith.Parties.Testing",
        "Hexalith.Parties.UI",
    ];

    private static readonly string[] ExpectedSdkRoutes =
    [
        "/process",
        "/query",
        "/admin/operational-index-metadata",
        "/project",
        "/project/v2",
        "/project/v2/reconcile",
        "/replay-state",
        "/project/rebuild/v1",
        "/project/rebuild/shared/v1",
        "/project/rebuild/stage/v1",
        "/project/rebuild/commit/v1",
        "/project/rebuild/abort/v1",
        "/project/rebuild/verify/v1",
    ];

    private static readonly string[] ExpectedRunnableProjects =
    [
        "Hexalith.Parties.AdminPortal.Tests",
        "Hexalith.Parties.Authentication.Tests",
        "Hexalith.Parties.Ci.Tests",
        "Hexalith.Parties.Client.Tests",
        "Hexalith.Parties.ConsumerPortal.Tests",
        "Hexalith.Parties.Contracts.Tests",
        "Hexalith.Parties.IntegrationTests",
        "Hexalith.Parties.Mcp.Tests",
        "Hexalith.Parties.Picker.Tests",
        "Hexalith.Parties.Projections.Tests",
        "Hexalith.Parties.Sample.Tests",
        "Hexalith.Parties.Security.Tests",
        "Hexalith.Parties.Server.Tests",
        "Hexalith.Parties.Tests",
        "Hexalith.Parties.UI.Tests",
    ];

    private static readonly string[] InventoryDocumentation =
    [
        "README.md",
        "docs/architecture.md",
        "docs/component-inventory.md",
        "docs/index.md",
        "docs/project-overview.md",
        "docs/source-tree-analysis.md",
    ];

    [Fact]
    public void SourceAndTestProjectInventoryIsDocumentedExactly()
    {
        string root = RepositoryRoot.Locate();
        string[] sourceProjects = Directory.GetFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension)
            .Order(StringComparer.Ordinal)
            .ToArray()!;
        sourceProjects.ShouldBe(ExpectedSourceProjects);
        File.Exists(Path.Combine(root, "samples/Hexalith.Parties.Sample/Hexalith.Parties.Sample.csproj")).ShouldBeTrue();
        Read(root, "Hexalith.Parties.slnx").ShouldContain("samples/Hexalith.Parties.Sample/Hexalith.Parties.Sample.csproj");

        string[] testProjects = Directory.GetFiles(Path.Combine(root, "tests"), "*.csproj", SearchOption.AllDirectories)
            .Select(path => Path.GetFileNameWithoutExtension(path)!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        testProjects.Length.ShouldBe(16);
        testProjects.ShouldContain("Hexalith.Parties.EventStoreGateway.TestHost");

        string testScript = Read(root, "scripts/test.ps1");
        string[] runnableProjects = Regex.Matches(
                testScript,
                "tests/(?<project>[^/]+Tests)/[^\"\\r\\n]+\\.csproj",
                RegexOptions.CultureInvariant)
            .Select(static match => match.Groups["project"].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        runnableProjects.ShouldBe(ExpectedRunnableProjects);
        testScript.ShouldContain("$testSupportProjects");
        testScript.ShouldContain("Hexalith.Parties.EventStoreGateway.TestHost.csproj");

        foreach (string relativePath in InventoryDocumentation)
        {
            string documentation = Read(root, relativePath);
            string normalizedDocumentation = Regex.Replace(documentation, @"\s+", " ", RegexOptions.CultureInvariant);
            normalizedDocumentation.ShouldContain("13 projects under `src`", Case.Insensitive, relativePath);
            normalizedDocumentation.ShouldContain("one sample project", Case.Insensitive, relativePath);
            documentation.ShouldContain("15 runnable", Case.Insensitive, relativePath);
            documentation.ShouldContain("support host", Case.Insensitive, relativePath);
        }
    }

    [Fact]
    public void MaintainedDocumentationDescribesSdkRoutesUnderEventStoreOnlyDenyAcl()
    {
        string root = RepositoryRoot.Locate();
        string acl = Read(root, "src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.parties.yaml");

        Regex.Matches(acl, @"(?m)^\s*defaultAction:\s*deny\s*$", RegexOptions.CultureInvariant).Count.ShouldBe(2);
        Regex.Matches(acl, @"(?m)^\s*- appId:\s*(?<id>\S+)\s*$", RegexOptions.CultureInvariant)
            .Select(static match => match.Groups["id"].Value)
            .ShouldBe(["eventstore"]);
        Regex.Matches(acl, @"(?m)^\s*- name:\s*(?<route>/\S+)\s*$", RegexOptions.CultureInvariant)
            .Select(static match => match.Groups["route"].Value)
            .ShouldBe(ExpectedSdkRoutes);
        Regex.Matches(acl, @"httpVerb:\s*\['POST'\]", RegexOptions.CultureInvariant).Count.ShouldBe(ExpectedSdkRoutes.Length);
        acl.ShouldNotContain("/**");

        foreach (string relativePath in new[] { "README.md", "docs/architecture.md", "docs/api-contracts.md", "docs/getting-started.md" })
        {
            string documentation = Read(root, relativePath);
            foreach (string route in ExpectedSdkRoutes)
            {
                documentation.Contains(route, StringComparison.Ordinal).ShouldBeTrue($"{relativePath}: {route}");
            }

            documentation.ShouldContain("deny-by-default", Case.Insensitive);
            documentation.ShouldContain("eventstore", Case.Insensitive);
            documentation.ShouldContain("SDK", Case.Insensitive);
        }
    }

    [Fact]
    public void RuntimeDeploymentIsExternallyOwnedAndRetiredAssetsRemainAbsent()
    {
        string root = RepositoryRoot.Locate();
        Directory.Exists(Path.Combine(root, "deploy")).ShouldBeFalse();
        Directory.GetFiles(Path.Combine(root, "tests"), "*DeployValidation*", SearchOption.AllDirectories).ShouldBeEmpty();

        foreach (string relativePath in new[] { "README.md", "docs/architecture.md", "docs/deployment-guide.md", "docs/event-publishing.md" })
        {
            string documentation = Read(root, relativePath);
            Regex.Replace(documentation, @"\s+", " ", RegexOptions.CultureInvariant)
                .ShouldContain("runtime deployment orchestration is externally owned", Case.Insensitive, relativePath);
            documentation.ShouldContain("immutable", Case.Insensitive, relativePath);
        }

        string eventPublishing = Read(root, "docs/event-publishing.md");
        eventPublishing.ShouldNotContain("defaultAction: allow");
        foreach (string appId in new[] { "eventstore", "parties", "tenants", "memories", "sample" })
        {
            eventPublishing.ShouldContain(appId);
        }

        eventPublishing.ShouldContain("eventstore=sample.parties.events");
        eventPublishing.ShouldContain("tenants=system.tenants.events");
        eventPublishing.ShouldContain("parties=system.tenants.events");
        eventPublishing.ShouldContain("sample=sample.parties.events");
    }

    private static string Read(string root, string relativePath)
        => File.ReadAllText(Path.Combine(root, relativePath));
}
