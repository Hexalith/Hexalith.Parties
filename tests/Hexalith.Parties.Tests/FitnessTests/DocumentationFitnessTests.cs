using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

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
            .Where(path => IsNotBuildOutput(root, path))
            .Select(path => Path.GetFileNameWithoutExtension(path)!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        sourceProjects.ShouldBe(ExpectedSourceProjects);
        File.Exists(Path.Combine(root, "samples/Hexalith.Parties.Sample/Hexalith.Parties.Sample.csproj")).ShouldBeTrue();
        Read(root, "Hexalith.Parties.slnx").ShouldContain("samples/Hexalith.Parties.Sample/Hexalith.Parties.Sample.csproj");

        string[] testProjects = Directory.GetFiles(Path.Combine(root, "tests"), "*.csproj", SearchOption.AllDirectories)
            .Where(path => IsNotBuildOutput(root, path))
            .Select(path => Path.GetFileNameWithoutExtension(path)!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        // Assert the exact set, not the count. A bare Length check lets a rename, or an add paired
        // with a removal, pass while the documented inventory silently drifts.
        string[] expectedTestProjects = [.. ExpectedRunnableProjects, "Hexalith.Parties.EventStoreGateway.TestHost"];
        testProjects.ShouldBe([.. expectedTestProjects.Order(StringComparer.Ordinal)]);

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
    public void CodeMapDocumentsThePinnedSdkVersion()
    {
        string root = RepositoryRoot.Locate();
        using JsonDocument globalJson = JsonDocument.Parse(Read(root, "global.json"));
        string pinnedSdk = globalJson.RootElement.GetProperty("sdk").GetProperty("version").GetString()!;
        string[] codeMapDocuments =
        [
            "README.md",
            "docs/architecture.md",
            "docs/development-guide.md",
            "docs/index.md",
            "docs/project-overview.md",
            "docs/source-tree-analysis.md",
            "docs/getting-started.md",
        ];

        foreach (string relativePath in codeMapDocuments)
        {
            string documentation = Read(root, relativePath);
            documentation.ShouldContain(pinnedSdk, Case.Sensitive, relativePath);

            // Containing the pinned SDK is not enough: a stale SDK listed beside it must fail too.
            FindStaleSdkTokens(documentation, pinnedSdk)
                .ShouldBeEmpty($"{relativePath} must list only the pinned SDK {pinnedSdk}.");
        }
    }

    [Theory]
    [InlineData("10.0.401", "SDK `10.0.401`.", "")]
    [InlineData("10.0.401", "SDK `10.0.401`; formerly 10.0.302 and 10.0.400.", "10.0.302,10.0.400")]
    [InlineData("10.0.401", "Hosting.Abstractions 10.0.8 and Aspire 13.6.0 are not SDK tokens; 110.0.302 is not either.", "")]
    [InlineData("11.0.100", "SDK `11.0.100`; formerly 11.0.099 beside an unrelated 10.0.401.", "11.0.099")]
    public void StaleSdkTokensAreReportedBesideThePinnedSdk(string pinnedSdk, string documentation, string expectedStaleTokens)
    {
        ArgumentNullException.ThrowIfNull(pinnedSdk);
        ArgumentNullException.ThrowIfNull(expectedStaleTokens);
        FindStaleSdkTokens(documentation, pinnedSdk)
            .ShouldBe(expectedStaleTokens.Split(',', StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void MaintainedTechnologyTablesMatchCentralPackageCatalog()
    {
        string root = RepositoryRoot.Locate();
        XDocument catalog = XDocument.Parse(Read(root, "references/Hexalith.Builds/Props/Directory.Packages.props"));
        Dictionary<string, string> versions = catalog.Descendants("PackageVersion")
            .ToDictionary(element => element.Attribute("Include")!.Value, element => element.Attribute("Version")!.Value, StringComparer.Ordinal);
        IReadOnlyDictionary<string, string> catalogProperties = ReadDefaultCatalogProperties(catalog);
        string Version(string packageId) => Regex.Replace(
            versions[packageId],
            @"\$\((?<name>[A-Za-z0-9_.]+)\)",
            match => catalogProperties[match.Groups["name"].Value],
            RegexOptions.CultureInvariant);
        string aspire = Version("Aspire.Hosting");
        XDocument appHost = XDocument.Parse(Read(root, "src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj"));
        appHost.Root!.Attribute("Sdk")!.Value.ShouldBe($"Aspire.AppHost.Sdk/{aspire}");
        foreach (string package in new[] { "Aspire.Hosting.Azure.AppContainers", "Aspire.Hosting.Docker", "Aspire.Hosting.Redis", "Aspire.Hosting.Testing" })
        {
            Version(package).ShouldBe(aspire, package);
        }

        string dapr = Version("Dapr.Client");
        foreach (string package in new[] { "Dapr.AspNetCore", "Dapr.Actors.AspNetCore", "Dapr.Actors.Generators", "Dapr.Actors", "Dapr.AI", "Dapr.AI.Microsoft.Extensions", "Dapr.Workflow" })
        {
            Version(package).ShouldBe(dapr, package);
        }

        string architecture = Read(root, "docs/architecture.md");
        string[] expectedArchitectureRows =
        [
            $"| Orchestration | .NET Aspire (`Aspire.Hosting` + hosting integrations) | `{aspire}` |",
            $"| Actors & pub/sub | DAPR client/actors/AspNetCore | `{dapr}` |",
            $"| | `CommunityToolkit.Aspire.Hosting.Dapr` | `{Version("CommunityToolkit.Aspire.Hosting.Dapr")}` |",
            $"| AuthN | Microsoft.AspNetCore.Authentication.JwtBearer | `{Version("Microsoft.AspNetCore.Authentication.JwtBearer")}` |",
            $"| | Microsoft.AspNetCore.Components.CustomElements | `{Version("Microsoft.AspNetCore.Components.CustomElements")}` |",
            $"| UI | Microsoft.FluentUI.AspNetCore.Components | `{Version("Microsoft.FluentUI.AspNetCore.Components")}` |",
            $"| Resilience/discovery | Microsoft.Extensions.Http.Resilience / ServiceDiscovery | `{Version("Microsoft.Extensions.Http.Resilience")}` |",
            $"| Versioning | MinVer (git-tag SemVer, prefix `v`) | `{Version("MinVer")}` |",
            $"| Testing | xUnit v3 / Shouldly / NSubstitute / bunit / Testcontainers / YamlDotNet | `{Version("xunit.v3")}` / `{Version("Shouldly")}` / `{Version("NSubstitute")}` / `{Version("bunit")}` / `{Version("Testcontainers")}`† / `{Version("YamlDotNet")}` |",
        ];
        foreach (string expectedRow in expectedArchitectureRows)
        {
            architecture.ShouldContain(expectedRow);
        }

        Version("Microsoft.Extensions.ServiceDiscovery").ShouldBe(Version("Microsoft.Extensions.Http.Resilience"));
        architecture.ShouldContain($"aligned at `{aspire}`");
        string overview = Read(root, "docs/project-overview.md");
        overview.ShouldContain($"| Actors / pub-sub | DAPR | {dapr} |");
        overview.ShouldContain($"| AuthN | JWT Bearer | {Version("Microsoft.AspNetCore.Authentication.JwtBearer")} |");
        overview.ShouldContain($"| UI | FluentUI Blazor + CustomElements | {Version("Microsoft.FluentUI.AspNetCore.Components")} / {Version("Microsoft.AspNetCore.Components.CustomElements")} |");
        overview.ShouldContain($"| Testing | xUnit v3 / Shouldly / NSubstitute / bunit / Testcontainers | {Version("xunit.v3")} / {Version("Shouldly")} / {Version("NSubstitute")} / {Version("bunit")} / {Version("Testcontainers")} |");
        foreach (string document in new[] { "docs/architecture.md", "docs/index.md", "docs/project-overview.md" })
        {
            Read(root, document).ShouldContain($"Aspire {aspire}", Case.Sensitive, document);
        }

        XDocument rootPackages = XDocument.Parse(Read(root, "Directory.Packages.props"));
        string eventStoreVersion = rootPackages.Descendants("HexalithEventStoreVersion").SingleOrDefault()?.Value
            ?? catalog.Descendants("HexalithEventStoreVersion").Single().Value;
        Read(root, "docs/ci.md").ShouldContain($"package graph selects EventStore {eventStoreVersion}");
    }

    [Fact]
    public void ComponentInventoryDocumentsEveryRootSubmodule()
    {
        string root = RepositoryRoot.Locate();
        string gitmodules = Read(root, ".gitmodules");
        string inventory = Read(root, "docs/component-inventory.md");
        string[] rootSubmodulePaths = Regex.Matches(
                gitmodules,
                @"(?m)^\s*path\s*=\s*(?<path>references/\S+)\s*$",
                RegexOptions.CultureInvariant)
            .Select(static match => match.Groups["path"].Value)
            .ToArray();

        rootSubmodulePaths.ShouldNotBeEmpty();
        rootSubmodulePaths.Distinct(StringComparer.Ordinal).Count().ShouldBe(rootSubmodulePaths.Length);
        foreach (string path in rootSubmodulePaths)
        {
            inventory.ShouldContain($"`{path}`", Case.Sensitive, path);
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

        // Pin the documentation to the ACL files as they actually are, not to a slogan. Asserting the
        // doc merely omits "defaultAction: allow" made the real allow-by-default eventstore-admin
        // policy undocumentable, so the doc read as a blanket deny-by-default claim that was false.
        // Every component that IS deny-by-default must be named as such, and any component that is
        // not must be named as an explicit exception.
        string componentDirectory = Path.Combine(root, "src/Hexalith.Parties.AppHost/DaprComponents");
        foreach (string aclPath in Directory.GetFiles(componentDirectory, "accesscontrol*.yaml"))
        {
            string fileName = Path.GetFileName(aclPath);
            string yaml = File.ReadAllText(aclPath);

            // Scope the allow-by-default check to the top-level accessControl fields only (before
            // the nested "policies:" list), not any indentation depth -- otherwise a per-app
            // policy's own "defaultAction: allow" would be misread as the file's overall default.
            int accessControlIndex = yaml.IndexOf("accessControl:", StringComparison.Ordinal);
            accessControlIndex.ShouldBeGreaterThanOrEqualTo(0, $"{fileName} must declare accessControl.");
            Match policiesMatch = Regex.Match(
                yaml[accessControlIndex..],
                @"(?m)^\s*policies:\s*$",
                RegexOptions.CultureInvariant);
            string topLevelAccessControl = policiesMatch.Success
                ? yaml[accessControlIndex..(accessControlIndex + policiesMatch.Index)]
                : yaml[accessControlIndex..];
            bool allowsByDefault = Regex.IsMatch(
                topLevelAccessControl,
                @"(?m)^\s*defaultAction:\s*allow\s*$",
                RegexOptions.CultureInvariant);

            eventPublishing.ShouldContain(fileName, Case.Sensitive, $"{fileName} must be documented.");
            if (allowsByDefault)
            {
                eventPublishing.ShouldContain(
                    "defaultAction: allow",
                    Case.Sensitive,
                    $"{fileName} is allow-by-default and must be documented as an explicit exception.");
            }
        }
        foreach (string appId in new[] { "eventstore", "parties", "tenants", "memories", "sample" })
        {
            eventPublishing.ShouldContain(appId);
        }

        eventPublishing.ShouldContain("eventstore=sample.parties.events");
        eventPublishing.ShouldContain("tenants=system.tenants.events");
        eventPublishing.ShouldContain("parties=system.tenants.events");
        eventPublishing.ShouldContain("sample=sample.parties.events");
    }

    /// <summary>
    /// Excludes generated or copied project files under <c>obj</c> and <c>bin</c> so a stale build
    /// output cannot fail the exact-inventory assertions as if it were real drift.
    /// </summary>
    private static bool IsNotBuildOutput(string root, string path)
        => !Path.GetRelativePath(root, path).Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            && !Path.GetRelativePath(root, path).Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

    private static string[] FindStaleSdkTokens(string documentation, string pinnedSdk)
    {
        // Derive the feature band from the pinned SDK so the guard follows the next major/minor too.
        string band = pinnedSdk[..pinnedSdk.LastIndexOf('.')];
        return Regex.Matches(
                documentation,
                $@"(?<![0-9.]){Regex.Escape(band)}\.[0-9]{{3}}(?![0-9])",
                RegexOptions.CultureInvariant)
            .Select(static match => match.Value)
            .Where(token => !string.Equals(token, pinnedSdk, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string Read(string root, string relativePath)
        => File.ReadAllText(Path.Combine(root, relativePath));

    /// <summary>
    /// Evaluates the catalog properties that apply to a Parties project in document order:
    /// unconditional assignments overwrite, <c>'$(Name)' == ''</c> defaults apply only when unset,
    /// and any other condition (for example a different <c>MSBuildProjectName</c>) is skipped.
    /// </summary>
    private static IReadOnlyDictionary<string, string> ReadDefaultCatalogProperties(XDocument catalog)
    {
        Dictionary<string, string> properties = new(StringComparer.Ordinal);
        foreach (XElement property in catalog.Descendants("PropertyGroup").Elements())
        {
            string name = property.Name.LocalName;
            string? condition = property.Attribute("Condition")?.Value;
            if (condition is null)
            {
                properties[name] = property.Value.Trim();
            }
            else if (string.Equals(condition.Replace(" ", string.Empty, StringComparison.Ordinal), $"'$({name})'==''", StringComparison.Ordinal))
            {
                properties.TryAdd(name, property.Value.Trim());
            }
        }

        return properties;
    }
}
