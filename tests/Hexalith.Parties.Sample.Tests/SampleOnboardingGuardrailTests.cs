using System.Text.RegularExpressions;
using System.Xml.Linq;

using Shouldly;

namespace Hexalith.Parties.Sample.Tests;

public sealed class SampleOnboardingGuardrailTests
{
    private static readonly string[] DocAndSampleScannedFiles =
    [
        "README.md",
        "docs/getting-started.md",
        "samples/Hexalith.Parties.Sample/Program.cs",
        "samples/Hexalith.Parties.Sample/appsettings.json",
        "samples/Hexalith.Parties.Sample/Properties/launchSettings.json",
    ];

    private static readonly string[] CurrentSurfaceForbiddenLiterals =
    [
        "api/v1/parties",
        "api/v1/admin",
        "openapi/v1.json",
        "/openapi",
        "Swagger",
        "OpenAPI",
        "X-GDPR-Warning",
        "https://localhost:5001/mcp",
        "http://localhost:5000/mcp",
        "PartyActor",
        "PartyDetailProjectionActor",
        "PartyIndexProjectionActor",
        "IPartySearchService",
    ];

    [Fact]
    public void CurrentAdopterDocsAndSampleProduction_DoNotAdvertiseRetiredPartiesSurfaces()
    {
        foreach (string relativePath in DocAndSampleScannedFiles)
        {
            string path = GetRepositoryFilePath(relativePath);
            File.Exists(path).ShouldBeTrue($"Required onboarding artifact is missing: {relativePath}");
            string text = File.ReadAllText(path);

            foreach (string forbiddenLiteral in CurrentSurfaceForbiddenLiterals)
            {
                text.ShouldNotContain(forbiddenLiteral, Case.Insensitive, $"{relativePath} must not advertise retired surface '{forbiddenLiteral}'.");
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
            "/openapi",
            "X-GDPR-Warning",
            "https://localhost:5001/mcp",
            "http://localhost:5000/mcp",
        ];

        foreach (string file in Directory.EnumerateFiles(testsRoot, "*.cs", SearchOption.AllDirectories)
                     .Where(static file => !file.EndsWith(nameof(SampleOnboardingGuardrailTests) + ".cs", StringComparison.Ordinal))
                     .Where(static file => !IsBuildArtifact(file)))
        {
            string text = File.ReadAllText(file);

            foreach (string forbiddenLiteral in forbiddenLiterals)
            {
                text.ShouldNotContain(forbiddenLiteral, Case.Insensitive, $"{file} must not assert retired surface '{forbiddenLiteral}'.");
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

        projectReferences.ShouldNotBeEmpty("Sample must reference at least Hexalith.Parties.Client to compile against the typed gateway boundary.");
        projectReferences.ShouldAllBe(
            static include =>
                include.EndsWith("src/Hexalith.Parties.Client/Hexalith.Parties.Client.csproj", StringComparison.Ordinal)
                || include.EndsWith("src/Hexalith.Parties.Contracts/Hexalith.Parties.Contracts.csproj", StringComparison.Ordinal),
            "Sample production project references must stay limited to approved consumer packages.");

        string[] packageReferences = project
            .Descendants("PackageReference")
            .Select(static reference => reference.Attribute("Include")?.Value)
            .Where(static include => !string.IsNullOrWhiteSpace(include))
            .Cast<string>()
            .ToArray();

        packageReferences.ShouldNotBeEmpty("Sample must reference at least Dapr.AspNetCore for the subscriber endpoint.");
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

        gettingStarted.ShouldContain("dotnet tool restore");
        gettingStarted.ShouldContain("dotnet tool run hexalith-module -- debug --manifest module/hexalith.module-manifest.v2.json");
        gettingStarted.ShouldContain("dotnet tool run hexalith-module -- down --manifest module/hexalith.module-manifest.v2.json --run-id <run-id>");
        gettingStarted.ShouldContain("aspire run --apphost src/Hexalith.Parties.AppHost");
        gettingStarted.ShouldContain("Legacy hosting scope");

        string[] directReferences = ReadSubmoduleUpdateReferences(gettingStarted).ShouldHaveSingleItem();
        string[] requiredReferences =
        [
            "references/Hexalith.Platform",
            "references/Hexalith.Builds",
            "references/Hexalith.Commons",
            "references/Hexalith.EventStore",
            "references/Hexalith.FrontComposer",
            "references/Hexalith.PolymorphicSerializations",
            "references/Hexalith.Tenants",
            "references/Hexalith.Memories",
            "references/Hexalith.McpCli",
        ];
        directReferences.Order(StringComparer.Ordinal).ShouldBe(requiredReferences.Order(StringComparer.Ordinal));
        HashSet<string> declaredReferences = Regex.Matches(
                File.ReadAllText(GetRepositoryFilePath(".gitmodules")),
                @"(?m)^\s*path\s*=\s*(?<path>\S+)\s*$",
                RegexOptions.CultureInvariant)
            .Select(static match => match.Groups["path"].Value)
            .ToHashSet(StringComparer.Ordinal);
        directReferences.ShouldAllBe(path => declaredReferences.Contains(path),
            "Only root-declared direct references may be initialized.");
        gettingStarted.ShouldMatch(@"(?i)\b(?:do\s+not|never|avoid)\b[^\r\n.!]*--recursive\b");
        gettingStarted.ShouldMatch(@"(?i)\b(?:do\s+not|never|avoid)\s+(?:initialize|update)\b[^.]*\b(?:nested|inside)\b");
        gettingStarted.ShouldContain("Aspire");
        gettingStarted.ShouldContain("DAPR");
        gettingStarted.ShouldContain("/ready");
        gettingStarted.ShouldContain("Docker Desktop");
        gettingStarted.ShouldContain("\"domain\": \"party\"");
        gettingStarted.ShouldContain("\"commandType\": \"Hexalith.Parties.Contracts.Commands.CreateParty\"");
        gettingStarted.ShouldContain("\"queryType\": \"PartyDetail\"");
        gettingStarted.ShouldContain("\"queryType\": \"PartySearch\"");
        gettingStarted.ShouldContain("\"queryType\": \"PartyIndex\"");
        gettingStarted.ShouldContain("\"payload\"");
        gettingStarted.ShouldContain("command -> event -> projection flow");
        gettingStarted.ShouldContain("Freshness and Eventual Consistency");
        gettingStarted.ShouldContain("AddPartiesClient(builder.Configuration)");
        gettingStarted.ShouldContain("\"Parties\"");
        gettingStarted.ShouldContain("\"BaseUrl\"");
        gettingStarted.ShouldContain("\"Tenant\"");
        gettingStarted.ShouldContain("not approved for regulated EU personal data until v1.1 GDPR features are active");
        gettingStarted.ShouldNotContain("submodule update --init --recursive");
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
        program.ShouldContain("AddPartiesClient(builder.Configuration)");
        program.ShouldContain("CreatePartyWithResultAsync");
        program.ShouldContain("AddContactChannelWithResultAsync");
        program.ShouldContain("AddIdentifierWithResultAsync");
        program.ShouldContain("PartiesClientException");
        program.ShouldContain("ListPartiesAsync");
        program.ShouldContain("PartyType.Person");
        program.ShouldContain("SearchPartiesAsync");
        program.ShouldContain("GetPartyAsync");
        program.ShouldContain("PrintFreshness");
        appSettings.ShouldContain("\"BaseUrl\"");
        appSettings.ShouldContain("\"Tenant\": \"tenant-a\"");
    }

    [Fact]
    public void SampleEventHandler_DemonstratesSubscriberOwnedLifecycleAndFutureErasureCleanup()
    {
        string handler = File.ReadAllText(GetRepositoryFilePath("samples/Hexalith.Parties.Sample/PartyEventHandler.cs"));
        string subscription = File.ReadAllText(GetRepositoryFilePath("samples/Hexalith.Parties.Sample/DaprComponents/subscription-sample.yaml"));

        handler.ShouldContain("PartyCreated");
        handler.ShouldContain("ContactChannelAdded");
        handler.ShouldContain("IdentifierAdded");
        handler.ShouldContain("PartyDeactivated");
        handler.ShouldContain("PartyReactivated");
        handler.ShouldContain("PartyErased");
        handler.ShouldContain("TryAcceptSequence");
        handler.ShouldContain("_lastSequenceByAggregate");
        handler.ShouldContain("CustomerSummaryStore.Customers.TryRemove");
        handler.ShouldContain("Unknown event type");
        subscription.ShouldContain("tenant-a.parties.events");
        subscription.ShouldContain("/events/parties");
        subscription.ShouldContain("sample");
    }

    [Theory]
    [InlineData("git -c submodule.recurse=true submodule update --recursive --init --remote references/Hexalith.Builds")]
    [InlineData("git --no-pager -C . submodule update references/Hexalith.Builds --init --remote")]
    [InlineData("git --git-dir=.git submodule update --recursive references/Hexalith.Builds --init")]
    [InlineData("echo ready && git -c color.ui=never submodule update --init references/Hexalith.Builds --remote")]
    [InlineData("git -c submodule.recurse=true submodule update --init references/Hexalith.Builds")]
    [InlineData("git -C . submodule --quiet update references/Hexalith.Builds --remote --init")]
    public void OnboardingSubmoduleCommandsRejectRecursiveOrRemoteUpdatesWithGlobalOptions(string command)
    {
        Should.Throw<ShouldAssertException>(() => ReadSubmoduleUpdateReferences(command))
            .Message.ShouldContain("recursive/remote");
    }

    [Theory]
    [InlineData("git -C . -c color.ui=never submodule update references/Hexalith.Builds --init")]
    [InlineData("git --no-pager submodule update --init references/Hexalith.Builds")]
    public void OnboardingSubmoduleCommandsAcceptDirectInitializationWithGlobalOptions(string command)
    {
        ReadSubmoduleUpdateReferences(command).ShouldHaveSingleItem().ShouldBe(["references/Hexalith.Builds"]);
    }

    private static string[][] ReadSubmoduleUpdateReferences(string text)
        => Regex.Matches(
                text,
                @"(?m)(?:^|[;&|])\s*(?:\$\s*)?git\s+[^;|&\r\n`]*?\bsubmodule\s+(?:--?[^\s;|&`]+\s+)*update\b(?<arguments>[^;|&\r\n`#]*)",
                RegexOptions.CultureInvariant)
            .Select(match =>
            {
                string command = match.Value.Trim();
                Regex.IsMatch(command, @"(?:^|\s)--(?:recursive|remote|recurse-submodules)(?:[=\s]|$)", RegexOptions.CultureInvariant)
                    .ShouldBeFalse($"Unsafe recursive/remote submodule update: {command}");
                Regex.IsMatch(command, @"\bsubmodule\.recurse\s*=\s*(?:true|1|yes|on)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                    .ShouldBeFalse($"Unsafe recursive/remote Git configuration: {command}");
                string[] arguments = match.Groups["arguments"].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                arguments.ShouldContain("--init", "Onboarding initializes explicitly listed direct references.");
                return arguments.Where(static argument => argument != "--init").ToArray();
            })
            .ToArray();

    private static bool IsBuildArtifact(string path)
        => path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

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
