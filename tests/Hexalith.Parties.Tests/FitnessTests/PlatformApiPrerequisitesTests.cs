using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

using Hexalith.Parties.Contracts.Authorization;

using Shouldly;

namespace Hexalith.Parties.Tests.FitnessTests;

public sealed class PlatformApiPrerequisitesTests
{
    private const string EventStorePayloadProtectionSpecRelativePath = "references/Hexalith.EventStore/_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md";
    private const string EventStorePackageVersion = "3.113.0";
    private const string EventStoreRelativePath = "references/Hexalith.EventStore";
    private const string EventStoreSprintStatusRelativePath = "references/Hexalith.EventStore/_bmad-output/implementation-artifacts/sprint-status.yaml";
    private const string EventStoreStoryMigrationRelativePath = "references/Hexalith.EventStore/_bmad-output/planning-artifacts/story-id-migration-2026-08-01.md";
    private const string MatrixRelativePath = "_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md";

    // Every child process here is a git query or an MSBuild evaluation. None should approach this;
    // the bound exists so a hung child fails the lane with a diagnosable message instead of blocking.
    private const int ProcessTimeoutMilliseconds = 300_000;
    private const string AiToolsSha = "3f194e17174994d308ec84af9ee2b5aa68674d0d";
    private const string BuildsSha = "360a2b9c4e96809365a7de785be9a68152d5ac28";
    private const string CommonsSha = "116d26815eb81e35b3c161e1799e5ee12805fc0a";

    // Consumed by MainLayout for the shell landmarks and skip links (G4 work package F, delivered
    // under sprint-change-proposal-2026-08-19-story-8-10-frontcomposer-shell-slice-backfill.md).
    // Recorded separately from the packaged 4.5.0 identity that CI and the released container use.
    /// <summary>Gets the FrontComposer source identity approved by the 2026-10-04 working-tree decision.</summary>
    internal const string FrontComposerSha = "2cc8dd3a3ac76c03f5ea6f6f92e65829306db470";
    private const string MemoriesSha = "5b43fe2f8a0f04dc021921a077dff1a573c2ce5e";
    private const string PolymorphicSerializationsSha = "98de6e013840ece9f0fa7c68ab7dcdf2bba3b375";
    private const string TenantsSha = "72b8e4f508176b69826549e87b7b2a286f607fd1";
    private const string PayloadProtectionEventStoreDescribe = "v3.113.0";
    private const string PayloadProtectionEventStoreSha = "865cd9e49273dffbb1cdae85efeaf1aac322e09e";
    private const string PayloadProtectionPartiesReceiptSha = "75b4fa1fe2cfd2167c186e94faf52e18be4b0ff6";
    private const string PayloadProtectionRetentionAction = "Keep Parties crypto/key-management implementation until an approved shared provider proves payload compatibility, typed unreadable outcomes, no-leak diagnostics, exports, processing records, certificates, and rollback.";
    private const string PayloadProtectionSurface = "Payload protection engine package";
    private const string SpecRelativePath = "_bmad-output/implementation-artifacts/spec-8-3-platform-api-prerequisites.md";
    private const string StartMarker = "<!-- platform-api-prerequisite-matrix:start -->";
    private const string EndMarker = "<!-- platform-api-prerequisite-matrix:end -->";

    // Release selection is separate from the historical migration/parity receipt constants.
    private const string CurrentReleaseEventStoreVersion = "3.117.1";
    private const string CurrentCatalogEventStoreVersion = "3.115.0";
    private const string CurrentReleaseEventStoreSha = "b830d9829af70536d2a3fd21c5e2a23b2ca2f256";
    private const string CurrentReleaseEventStoreDescribe = "v3.117.0";
    private static readonly IReadOnlyDictionary<string, string> CurrentReleaseGitlinks = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["references/Hexalith.AI.Tools"] = AiToolsSha,
        ["references/Hexalith.Builds"] = "ad52c5bdd4361c59eedf12a16620150006403584",
        ["references/Hexalith.Commons"] = CommonsSha,
        [EventStoreRelativePath] = CurrentReleaseEventStoreSha,
        ["references/Hexalith.FrontComposer"] = "c561b3210f15206a90c39c82c58f2e5b1005cd60",
        ["references/Hexalith.Memories"] = "aac6d9054cb138881e6e49c8e48233553123ffce",
        ["references/Hexalith.PolymorphicSerializations"] = PolymorphicSerializationsSha,
        ["references/Hexalith.Tenants"] = "86aa888301b40cda12b000f68d9292d53cd93efe",
    };

    private static readonly IReadOnlyDictionary<string, string> CurrentSourceDescribes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["references/Hexalith.Builds"] = "v4.30.0-5-gad52c5bd",
        [EventStoreRelativePath] = CurrentReleaseEventStoreDescribe,
        ["references/Hexalith.FrontComposer"] = "v4.6.0-5-gc561b321",
        ["references/Hexalith.Memories"] = "v2.28.1-34-gaac6d905",
        ["references/Hexalith.Tenants"] = "v5.7.0-171-g86aa8883",
    };

    private static readonly string[] RequiredAbsentPayloadProtectionPaths =
    [
        "references/Hexalith.EventStore/_bmad-output/implementation-artifacts/8-11-g5-evidence-and-approval-closure.md",
        "references/Hexalith.EventStore/src/Hexalith.EventStore.PayloadProtection.AzureKeyVault/Hexalith.EventStore.PayloadProtection.AzureKeyVault.csproj",
    ];

    private static readonly string[] RequiredPayloadProtectionRollbackPaths =
    [
        "src/Hexalith.Parties.Security/PartyPayloadProtectionService.cs",
        "src/Hexalith.Parties.Security/PartyKeyManagementService.cs",
        "src/Hexalith.Parties.Security/CachedPartyKeyManagementService.cs",
        "src/Hexalith.Parties.Security/PartyKeyLifecycleService.cs",
        "src/Hexalith.Parties.Security/IPartyKeyRetryScheduler.cs",
        "src/Hexalith.Parties.Security/ActorBackedPartyKeyRetryScheduler.cs",
        "src/Hexalith.Parties.Security/PartyKeyRetryActor.cs",
        "src/Hexalith.Parties.Security/IPartyKeyRetryActor.cs",
        "src/Hexalith.Parties.Security/DecryptionCircuitBreaker.cs",
        "src/Hexalith.Parties.Security/DecryptionCircuitOpenException.cs",
        "src/Hexalith.Parties.Security/KeyOperationAuditService.cs",
        "src/Hexalith.Parties.Security/TenantKeyRotationService.cs",
        "src/Hexalith.Parties.Security/TenantKeyRotationProgress.cs",
        "src/Hexalith.Parties.Security/TenantKeyRotationProgressConflictException.cs",
        "src/Hexalith.Parties.Security/ITenantKeyRotationCacheInvalidator.cs",
        "src/Hexalith.Parties.Security/LocalDevKeyStorageBackend.cs",
        "src/Hexalith.Parties.Security/PartyEncryptionKeyDestroyedException.cs",
        "src/Hexalith.Parties.Security/CryptoPendingRecord.cs",
        "src/Hexalith.Parties.Security/PartyErasureOrchestrator.cs",
        "src/Hexalith.Parties.Security/ErasureVerificationService.cs",
        "src/Hexalith.Parties.Security/PartyErasureRecordStore.cs",
        "src/Hexalith.Parties.Security/PartyPersonalDataCommandGuard.cs",
        "src/Hexalith.Parties.Security/PersonalDataGraphInspector.cs",
        "src/Hexalith.Parties.Security/EventStorePartyPayloadProtectionAdapter.cs",
        "tests/Hexalith.Parties.Security.Tests/CryptoKeyManagementCompatibilityHarnessTests.cs",
    ];

    private static readonly string[] RequiredPayloadProtectionStoryStatuses =
    [
        "8-1-shared-payload-protection-security-spec-and-adr: done",
        "8-2-payload-protection-contracts-and-golden-vectors: done",
        "8-3-pdenc-v2-core-cryptographic-engine: in-progress",
        "8-4-compatibility-readers-and-mixed-history-routing: backlog",
        "8-5-policy-and-key-lifecycle-mechanics: backlog",
        "8-6-azure-key-vault-production-adapter-conformance: backlog",
        "8-7-server-persistence-and-snapshot-integration: backlog",
        "8-8-package-and-release-integration: backlog",
        "8-9-parties-dual-provider-parity: backlog",
        "8-10-post-v2-write-rollback-rehearsal: backlog",
        "8-11-g5-evidence-and-approval-closure: backlog",
    ];

    private static readonly string[] RequiredPositivePayloadProtectionSpecTokens =
    [
        "status: approved-authorized",
        "decision: adopted-amendment",
        "story_8_2_authorized: true",
        "Hexalith.EventStore.PayloadProtection",
        "Hexalith.EventStore.PayloadProtection.AzureKeyVault",
        "pdenc-v2",
        "json+pdenc-v1",
        "IPersonalDataPolicy",
        "IErasureStateProvider",
    ];

    private static readonly IReadOnlyDictionary<string, string[]> RequiredRows = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["EventStore domain-service host"] = ["8.4", "8.5", "8.10"],
        ["EventStore projection/query SDK"] = ["8.6", "8.10"],
        ["EventStore DataProtection"] = ["8.6", "8.10"],
        ["EventStore client envelopes/freshness/error codes"] = ["8.6", "8.8", "8.9", "8.10"],
        ["Tenant claims transformation"] = ["8.4", "8.8", "8.10"],
        ["Aspire publish helpers"] = ["8.5", "8.8", "8.10"],
        ["FrontComposer UI primitives"] = ["8.9", "8.10"],
        ["Commons HTTP helpers"] = ["8.8", "8.10"],
        ["Builds shared props/targets"] = ["8.8", "8.10"],
    };

    private static readonly IReadOnlyDictionary<string, string[]> RequiredGapRows = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["EventStore degraded response and DAPR health checks"] = ["8.5", "8.8", "8.10"],
        ["Payload protection engine package"] = ["8.7", "8.10"],
        ["MCP, deep-link, and search probes"] = ["8.8", "8.10"],
        ["Package publishing/source-mode CI"] = ["8.8", "8.10"],
    };

    private static readonly IReadOnlyDictionary<string, string[]> RequiredGapTokensBySurface = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["EventStore degraded response and DAPR health checks"] = ["G1", "G2"],
        ["EventStore projection/query SDK"] = ["G3", "G6", "G10"],
        ["FrontComposer UI primitives"] = ["G4"],
        ["Payload protection engine package"] = ["G5"],
        ["EventStore client envelopes/freshness/error codes"] = ["G6"],
        ["Tenant claims transformation"] = ["G7", "G9"],
        ["Aspire publish helpers"] = ["G8"],
        ["MCP, deep-link, and search probes"] = ["G11"],
        ["Package publishing/source-mode CI"] = ["G12"],
    };

    private static readonly IReadOnlyDictionary<string, string[]> RequiredEvidenceTokensBySurface = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["EventStore domain-service host"] = ["AddEventStoreDomainService", "UseEventStoreDomainService"],
        ["EventStore degraded response and DAPR health checks"] = ["AddEventStoreDaprHealthChecks", "DegradedResponseMiddleware", "DaprStateStoreHealthCheck", "DaprPubSubHealthCheck"],
        ["EventStore projection/query SDK"] = ["IDomainProjectionHandler", "IDomainQueryHandler", "IReadModelStore", "IQueryCursorCodec"],
        ["EventStore DataProtection"] = ["AddEventStoreDataProtection", "DaprXmlRepository", "AddEventStoreQueryCursorCodec"],
        ["Payload protection engine package"] =
        [
            "IEventPayloadProtectionService",
            "NoOpEventPayloadProtectionService",
            "TryAddSingleton<IEventPayloadProtectionService, NoOpEventPayloadProtectionService>",
            "Hexalith.EventStore.PayloadProtection",
            "Hexalith.EventStore.PayloadProtection.AzureKeyVault",
            "PartyPayloadProtectionService",
            "EventStorePartyPayloadProtectionAdapter",
            "CryptoKeyManagementCompatibilityHarnessTests",
            "pdenc-v2",
            "json+pdenc-v1",
            "IPersonalDataPolicy",
            "IErasureStateProvider",
            "8-2-payload-protection-contracts-and-golden-vectors: done",
            "8-11-g5-evidence-and-approval-closure: backlog",
            "only this story can record",
        ],
        ["EventStore client envelopes/freshness/error codes"] = ["IEventStoreGatewayClient", "QueryResponseMetadata", "QueryProblemReasonCodes", "GatewayProblemDetailsExtensions"],
        ["Tenant claims transformation"] = [PartiesClaimTypes.EventStoreTenant, "AggregateIdentity.IsValid(string)", "UniqueIdHelper.IsValidUlid(string)"],
        ["Aspire publish helpers"] = ["AddEventStoreDomainModule", "WithJwtBearerSecurity", "WithEventStoreJwtAuthentication", "HexalithEventStoreJwtAuthenticationOptions", "PrimaryAudience", "ValidAudiences", "AddEventStoreGatewayClient"],
        ["FrontComposer UI primitives"] = ["FcEntityPicker", "FcDestructiveConfirmationDialog", "FileDownload", "JsonDownload"],
        ["Commons HTTP helpers"] = ["HttpClientRegistration", "BoundedProblemDetailsReader", "HttpCorrelation"],
        ["MCP, deep-link, and search probes"] = ["GetContext()", "IFrontComposerMcpTenantToolGate", "TryAddWithoutValidation", "BuildCorrelationLink", "GetRichSearchCapabilityAsync"],
        ["Builds shared props/targets"] = ["TreatWarningsAsErrors", "Directory.Packages.props"],
        ["Package publishing/source-mode CI"] = ["Hexalith.Commons.Http", "Hexalith.Commons.ServiceDefaults", "Hexalith.Tenants.Client", "Hexalith.Tenants.Testing"],
    };

    private static readonly IReadOnlyDictionary<string, (string Pattern, string Path)[]> RequiredDeliveredApiCommands =
        new Dictionary<string, (string Pattern, string Path)[]>(StringComparer.Ordinal)
        {
            ["EventStore degraded response and DAPR health checks"] =
            [
                ("AddEventStoreDaprHealthChecks", "references/Hexalith.EventStore/src/Hexalith.EventStore/HealthChecks/HealthCheckBuilderExtensions.cs"),
            ],
            ["Aspire publish helpers"] =
            [
                ("WithEventStoreJwtAuthentication", "references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreSecurityExtensions.cs"),
                ("HexalithEventStoreJwtAuthenticationOptions", "references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreJwtAuthenticationOptions.cs"),
                ("PrimaryAudience", "references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreJwtAuthenticationOptions.cs"),
                ("ValidAudiences", "references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreJwtAuthenticationOptions.cs"),
            ],
        };

    private static readonly IReadOnlyDictionary<string, string[]> RequiredFinalConsumptionRows = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["EventStore domain-service host and DataProtection/query SDK|Package (default Release graph)"] =
        [
            "Hexalith.Parties",
            "PackageReference",
            $"Released package `{EventStorePackageVersion}`",
        ],
        ["EventStore domain-service host and DataProtection/query SDK|Source (explicit project-reference graph)"] =
        [
            "HexalithEventStoreFromSource",
            PayloadProtectionEventStoreSha,
            PayloadProtectionEventStoreDescribe,
        ],
        ["Commons HTTP helpers|Source"] =
        [
            "Hexalith.Parties.Client",
            CommonsSha,
            "`v2.30.1-15-g116d268`",
        ],
        ["Builds central catalog|Source import"] =
        [
            "Directory.Packages.props",
            "does not import `Hexalith.Build.props` or `Hexalith.Package.props`",
            BuildsSha,
            $"EventStore `{EventStorePackageVersion}`",
            "Commons `2.30.1`",
            "Memories `2.27.1`",
            "Tenants `5.7.0`",
            "Parties `1.1.1`",
        ],
        ["Memories submodule|Source (optional rich search)"] =
        [
            MemoriesSha,
            "`v2.28.0`",
            "HexalithMemoriesVersion=2.27.1",
        ],
        ["Tenants AppHost topology|Source (diagnostic) and package `5.7.0` (default graph)"] =
        [
            TenantsSha,
            "`v5.7.0-143-g72b8e4f5`",
            "5.7.0",
        ],
    };

    private static readonly HashSet<string> ApprovedStatuses = new(StringComparer.Ordinal)
    {
        "available",
        "needs-additive-api",
        "blocked",
    };

    private static readonly IReadOnlyDictionary<string, string[]> OwnerPrefixes = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["Hexalith.EventStore"] = ["references/Hexalith.EventStore/"],
        ["Hexalith.EventStore.Aspire"] = ["references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/"],
        ["Hexalith.Commons"] = ["references/Hexalith.Commons/"],
        ["Hexalith.Commons.Http"] = ["references/Hexalith.Commons/"],
        ["Hexalith.FrontComposer"] = ["references/Hexalith.FrontComposer/"],
        ["Hexalith.FrontComposer.Contracts.UI"] = ["references/Hexalith.FrontComposer/"],
        ["Hexalith.FrontComposer.Mcp"] = ["references/Hexalith.FrontComposer/"],
        ["Hexalith.FrontComposer.Shell"] = ["references/Hexalith.FrontComposer/"],
        ["Hexalith.Builds"] = ["references/Hexalith.Builds/"],
        ["Hexalith.Tenants"] = ["references/Hexalith.Tenants/"],
        ["platform AppHost owners"] = ["references/Hexalith.FrontComposer/"],
    };

    private static readonly IReadOnlyDictionary<string, string[]> ExcludedOwnerPrefixes = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["Hexalith.EventStore"] = ["references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/"],
    };

    private static readonly string[] ForbiddenStoryMigrationPathPrefixes =
    [
        "src/Hexalith.Parties/",
        "src/Hexalith.Parties.",
        "src/Directory.",
    ];

    private static readonly string[] ForbiddenStoryMigrationFiles =
    [
        "Directory.Build.props",
        "Directory.Build.targets",
        "Directory.Packages.props",
        "Directory.Solution.props",
        "Directory.Solution.targets",
        "Hexalith.Parties.slnx",
        "MSBuild.rsp",
        "NuGet.config",
        "global.json",
        "package.json",
        "package-lock.json",
    ];

    private static readonly string[] ApprovedStory84LeafRetirementPaths =
    [
        "Hexalith.Parties.slnx",
        "src/Hexalith.Parties.Mcp/Hexalith.Parties.Mcp.csproj",
        "src/Hexalith.Parties.Mcp/Program.cs",
        "src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs",
        "src/Hexalith.Parties.Server/Hexalith.Parties.Server.csproj",
        "src/Hexalith.Parties.ServiceDefaults/Extensions.cs",
        "src/Hexalith.Parties.ServiceDefaults/Hexalith.Parties.ServiceDefaults.csproj",
        "src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj",
        "src/Hexalith.Parties.UI/Program.cs",
        "src/Hexalith.Parties/Domain/PartyAggregate.cs",
        "src/Hexalith.Parties/Domain/PartyDomainServiceInvoker.cs",
        "src/Hexalith.Parties/Hexalith.Parties.csproj",
        "src/Hexalith.Parties/Program.cs",
        "src/Hexalith.Parties/Validation/CreatePartyCompositeValidator.cs",
        "src/Hexalith.Parties/Validation/UpdatePartyCompositeValidator.cs",
    ];

    private static readonly string[] ApprovedStory85SdkHostCutoverPaths =
    [
        "src/Hexalith.Parties/Domain/PartyAggregate.cs",
        "src/Hexalith.Parties/Domain/PartyDomainProcessor.cs",
        "src/Hexalith.Parties/Domain/PartyDomainServiceInvoker.cs",
        "src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs",
        "src/Hexalith.Parties/Hexalith.Parties.csproj",
        "src/Hexalith.Parties/Program.cs",
    ];

    private static readonly string[] ApprovedEpic8MigrationPaths =
    [
        .. ApprovedStory84LeafRetirementPaths,
        .. ApprovedStory85SdkHostCutoverPaths,
    ];

    private static readonly IReadOnlyDictionary<string, string[]> ApprovedStory84ChangedLines = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["Hexalith.Parties.slnx"] =
        [
            "    <Project Path=\"src/Hexalith.Parties.Server/Hexalith.Parties.Server.csproj\" />",
            "    <Project Path=\"src/Hexalith.Parties.ServiceDefaults/Hexalith.Parties.ServiceDefaults.csproj\" />",
        ],
        ["src/Hexalith.Parties.Mcp/Hexalith.Parties.Mcp.csproj"] =
        [
            "    <ProjectReference Include=\"$(HexalithCommonsRoot)\\src\\libraries\\Hexalith.Commons.ServiceDefaults\\Hexalith.Commons.ServiceDefaults.csproj\" Condition=\"'$(HexalithCommonsFromSource)' == 'true'\" />",
            "    <PackageReference Include=\"Hexalith.Commons.ServiceDefaults\" Condition=\"'$(HexalithCommonsFromSource)' != 'true'\" />",
            "    <ProjectReference Include=\"..\\Hexalith.Parties.ServiceDefaults\\Hexalith.Parties.ServiceDefaults.csproj\" />",
        ],
        ["src/Hexalith.Parties.Mcp/Program.cs"] =
        [
            "using Hexalith.Commons.ServiceDefaults;",
            "using Hexalith.Parties.Mcp;",
            "using Hexalith.Parties.ServiceDefaults;",
            "_ = builder.AddServiceDefaults();",
            "_ = builder.AddHexalithServiceDefaults(ConfigurePartiesServiceDefaults);",
            "_ = app.MapDefaultEndpoints();",
            "_ = app.MapHexalithDefaultEndpoints(ConfigurePartiesServiceDefaults);",
            string.Empty,
            "static void ConfigurePartiesServiceDefaults(HexalithServiceDefaultsOptions options)",
            "{",
            "    options.HealthEndpointPath = \"/health\";",
            "    options.LivenessEndpointPath = \"/alive\";",
            "    options.ReadinessEndpointPath = \"/ready\";",
            "    options.RegisterDefaultSelfCheck = false;",
            "    options.ActivitySourceNames.Add(\"Hexalith.Parties\");",
            "}",
        ],
        ["src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj"] =
        [
            "    <ProjectReference Include=\"..\\Hexalith.Parties.ServiceDefaults\\Hexalith.Parties.ServiceDefaults.csproj\" />",
            "    <!-- Story 1.7 (AR-D6) — the low-level EventStore SignalR transport (EventStoreSignalRClient) the",
            "    <ProjectReference Include=\"$(HexalithCommonsRoot)\\src\\libraries\\Hexalith.Commons.ServiceDefaults\\Hexalith.Commons.ServiceDefaults.csproj\" Condition=\"'$(HexalithCommonsFromSource)' == 'true'\" />",
            "    <PackageReference Include=\"Hexalith.Commons.ServiceDefaults\" Condition=\"'$(HexalithCommonsFromSource)' != 'true'\" />",
        ],
        ["src/Hexalith.Parties.UI/Program.cs"] =
        [
            "using Hexalith.Commons.ServiceDefaults;",
            "using Hexalith.Parties.ServiceDefaults;",
            "WebApplicationBuilder builder = WebApplication.CreateBuilder(args);",
            "builder.AddServiceDefaults();",
            "builder.AddHexalithServiceDefaults(ConfigurePartiesServiceDefaults);",
            "app.MapDefaultEndpoints();",
            "app.MapHexalithDefaultEndpoints(ConfigurePartiesServiceDefaults);",
            string.Empty,
            "static void ConfigurePartiesServiceDefaults(HexalithServiceDefaultsOptions options)",
            "{",
            "    options.HealthEndpointPath = \"/health\";",
            "    options.LivenessEndpointPath = \"/alive\";",
            "    options.ReadinessEndpointPath = \"/ready\";",
            "    options.RegisterDefaultSelfCheck = false;",
            "    options.ActivitySourceNames.Add(\"Hexalith.Parties\");",
            "}",
        ],
        ["src/Hexalith.Parties/Domain/PartyDomainServiceInvoker.cs"] =
        [
            "using Hexalith.Parties.Security;",
            "using Hexalith.Parties.Server.Aggregates;",
        ],
        ["src/Hexalith.Parties/Hexalith.Parties.csproj"] =
        [
            "    <ProjectReference Include=\"..\\Hexalith.Parties.Server\\Hexalith.Parties.Server.csproj\" />",
            "    <ProjectReference Include=\"..\\Hexalith.Parties.ServiceDefaults\\Hexalith.Parties.ServiceDefaults.csproj\" />",
            "    <ProjectReference Include=\"$(HexalithCommonsRoot)\\src\\libraries\\Hexalith.Commons.ServiceDefaults\\Hexalith.Commons.ServiceDefaults.csproj\" Condition=\"'$(HexalithCommonsFromSource)' == 'true'\" />",
            "    <PackageReference Include=\"Hexalith.Commons.ServiceDefaults\" Condition=\"'$(HexalithCommonsFromSource)' != 'true'\" />",
            "    <ProjectReference Include=\"$(HexalithEventStoreRoot)\\src\\Hexalith.EventStore.Client\\Hexalith.EventStore.Client.csproj\" Condition=\"'$(HexalithEventStoreFromSource)' == 'true'\" />",
            "    <PackageReference Include=\"Hexalith.EventStore.Client\" Condition=\"'$(HexalithEventStoreFromSource)' != 'true'\" />",
        ],
        ["src/Hexalith.Parties/Program.cs"] =
        [
            "using Hexalith.Commons.ServiceDefaults;",
            "using Hexalith.Parties.ServiceDefaults;",
            "builder.AddServiceDefaults();",
            "builder.AddHexalithServiceDefaults(ConfigurePartiesServiceDefaults);",
            "app.MapDefaultEndpoints();                    // Health checks: /health, /alive, /ready",
            "app.MapHexalithDefaultEndpoints(ConfigurePartiesServiceDefaults); // Health checks: /health, /alive, /ready",
            string.Empty,
            "static void ConfigurePartiesServiceDefaults(HexalithServiceDefaultsOptions options)",
            "{",
            "    options.HealthEndpointPath = \"/health\";",
            "    options.LivenessEndpointPath = \"/alive\";",
            "    options.ReadinessEndpointPath = \"/ready\";",
            "    options.RegisterDefaultSelfCheck = false;",
            "    options.ActivitySourceNames.Add(\"Hexalith.Parties\");",
            "}",
        ],
        ["src/Hexalith.Parties/Validation/CreatePartyCompositeValidator.cs"] =
        [
            "using Hexalith.Parties.Server.Aggregates;",
            "using Hexalith.Parties.Domain;",
        ],
        ["src/Hexalith.Parties/Validation/UpdatePartyCompositeValidator.cs"] =
        [
            "using Hexalith.Parties.Server.Aggregates;",
            "using Hexalith.Parties.Domain;",
        ],
    };

    private static readonly string[] AllowedContextEvidencePrefixes =
    [
        "_bmad-output/",
        "src/Hexalith.Parties/",
        "src/Hexalith.Parties.Authentication/",
        "src/Hexalith.Parties.Mcp/",
        "src/Hexalith.Parties.Security/",
        "references/Hexalith.Tenants/",
    ];

    private static readonly IReadOnlyDictionary<string, string[]> AdditionalContextEvidencePrefixes =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Aspire publish helpers"] =
            [
                "src/Hexalith.Parties.AppHost/",
                "src/Hexalith.Parties.Client/",
            ],
            ["FrontComposer UI primitives"] =
            [
                "src/Hexalith.Parties.AdminPortal/",
                "src/Hexalith.Parties.ConsumerPortal/",
                "src/Hexalith.Parties.Picker/",
            ],
            ["MCP, deep-link, and search probes"] =
            [
                "src/Hexalith.Parties.AdminPortal/",
            ],
        };

    [Fact]
    public void Matrix_ContainsRequiredRowsAndRequiredGapRows()
    {
        IReadOnlyDictionary<string, MatrixRow> rows = ReadRows();

        rows.Count.ShouldBeGreaterThanOrEqualTo(RequiredRows.Count + RequiredGapRows.Count);
        foreach (KeyValuePair<string, string[]> required in RequiredRows)
        {
            AssertRequiredRow(rows, required);
        }

        foreach (KeyValuePair<string, string[]> required in RequiredGapRows)
        {
            AssertRequiredRow(rows, required);
        }

        foreach (MatrixRow row in rows.Values)
        {
            ParseStoryIds(row.DependentStories).ShouldNotBeEmpty(row.Surface);
        }
    }

    [Fact]
    public void Matrix_CoversKnownFablePlatformGaps()
    {
        IReadOnlyDictionary<string, MatrixRow> rows = ReadRows();
        HashSet<string> actualGapIds = [];

        foreach (KeyValuePair<string, string[]> expected in RequiredGapTokensBySurface)
        {
            rows.ContainsKey(expected.Key).ShouldBeTrue(expected.Key);
            string rowText = ToSearchableText(rows[expected.Key]);

            foreach (string gap in expected.Value)
            {
                ContainsExactToken(rowText, gap).ShouldBeTrue($"{expected.Key}: {gap}");
                actualGapIds.Add(gap);
            }
        }

        foreach (string gap in Enumerable.Range(1, 12).Select(static number => $"G{number}"))
        {
            actualGapIds.Contains(gap).ShouldBeTrue(gap);
        }
    }

    [Fact]
    public void Matrix_StatusesUseApprovedVocabulary()
    {
        foreach (MatrixRow row in ReadRows().Values)
        {
            ApprovedStatuses.Contains(row.Status).ShouldBeTrue($"{row.Surface}: {row.Status}");
        }
    }

    [Fact]
    public void Matrix_EvidencePathsExistAndMatchDeclaredOwner()
    {
        string root = RepositoryRoot.Locate();
        string normalizedRoot = Path.GetFullPath(root);

        foreach (MatrixRow row in ReadRows().Values)
        {
            string[] evidencePaths = SplitEvidencePaths(row.EvidencePaths)
                .Select(path => NormalizeEvidencePath(normalizedRoot, path))
                .ToArray();
            evidencePaths.ShouldNotBeEmpty(row.Surface);

            foreach (string evidencePath in evidencePaths)
            {
                string fullPath = Path.Combine(normalizedRoot, evidencePath);
                (File.Exists(fullPath) || Directory.Exists(fullPath)).ShouldBeTrue(evidencePath);
            }

            string[] expectedPrefixes = ExpectedOwnerPrefixes(row.Owner).ToArray();
            expectedPrefixes.ShouldNotBeEmpty($"{row.Surface}: {row.Owner}");

            foreach (string ownerSegment in OwnerSegments(row.Owner))
            {
                string[] ownerPrefixes = OwnerPrefixes[ownerSegment];
                string[] excludedPrefixes = ExcludedPrefixes(ownerSegment).ToArray();

                evidencePaths.Any(path =>
                        ownerPrefixes.Any(prefix => IsUnderPathPrefix(path, prefix)) &&
                        !excludedPrefixes.Any(prefix => IsUnderPathPrefix(path, prefix)))
                    .ShouldBeTrue($"{row.Surface} should cite owner evidence for {ownerSegment}.");
            }

            foreach (string evidencePath in evidencePaths)
            {
                bool isOwnerOrContextEvidence =
                    expectedPrefixes.Any(path => IsUnderPathPrefix(evidencePath, path)) ||
                    AllowedContextEvidencePrefixes.Any(path => IsUnderPathPrefix(evidencePath, path)) ||
                    AdditionalContextPrefixes(row.Surface).Any(path => IsUnderPathPrefix(evidencePath, path));

                isOwnerOrContextEvidence.ShouldBeTrue($"{row.Surface}: {evidencePath}");
            }
        }
    }

    [Fact]
    public void Matrix_AvailableRowsStillRequireReleaseOrSubmoduleProof()
    {
        foreach (MatrixRow row in ReadRows().Values.Where(static row => row.Status == "available"))
        {
            bool explicitlyRequiresReleaseOrPin = Regex.IsMatch(
                row.ProofRequired,
                @"\bmust validate\b.*\b(release|submodule-pin)\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            explicitlyRequiresReleaseOrPin.ShouldBeTrue(row.Surface);
            row.ProofRequired.Contains("no release", StringComparison.OrdinalIgnoreCase).ShouldBeFalse(row.Surface);
            row.ProofRequired.Contains("without release", StringComparison.OrdinalIgnoreCase).ShouldBeFalse(row.Surface);
            row.ProofRequired.Contains("without submodule-pin", StringComparison.OrdinalIgnoreCase).ShouldBeFalse(row.Surface);
        }
    }

    [Fact]
    public void Matrix_NamedAvailableRowsRecordImmutableConsumptionIdentities()
    {
        IReadOnlyDictionary<string, MatrixRow> rows = ReadRows();
        string matrix = ReadMatrix();
        IReadOnlyDictionary<string, string> finalRows = ParseFinalConsumptionRows(matrix);

        foreach (string surface in new[]
                 {
                     "EventStore domain-service host",
                     "EventStore DataProtection",
                     "Commons HTTP helpers",
                     "Builds shared props/targets",
                 })
        {
            rows.ContainsKey(surface).ShouldBeTrue(surface);
            rows[surface].Status.ShouldBe("available", surface);
        }

        foreach (KeyValuePair<string, string[]> expected in RequiredFinalConsumptionRows)
        {
            finalRows.ContainsKey(expected.Key).ShouldBeTrue(expected.Key);
            DescribeRowTokenGaps(finalRows[expected.Key], expected.Value).ShouldBeEmpty(expected.Key);
        }

        matrix.ShouldContain("Story 8.10 final retained-identity reconciliation");
    }

    [Fact]
    public void FinalConsumptionRowIdentityValidationFailsClosedOnSwappedOrMissingTokens()
    {
        DescribeRowTokenGaps("Commons row with EventStore 3.95.0 only", [CommonsSha])
            .ShouldBe([$"missing:{CommonsSha}"]);
        DescribeRowTokenGaps($"Builds row {BuildsSha}", [BuildsSha, "Commons `2.30.1`"])
            .ShouldBe(["missing:Commons `2.30.1`"]);
    }

    [Fact]
    public void AvailableRowConsumersFailClosedOnMissingOrMismatchedIdentity()
    {
        string root = RepositoryRoot.Locate();
        string matrix = ReadMatrix();

        foreach (string surface in RequiredFinalConsumptionRows.Keys.Select(static key => key.Split('|')[0]).Distinct(StringComparer.Ordinal))
        {
            matrix.ShouldContain(surface);
        }

        // Directional gate retention. A downstream story that is still blocked must keep the
        // available-row identity gate holding it there; a story that has since completed may
        // legitimately retire its gate. Pinning both unconditionally is what pushed the previous
        // version of this test into grepping the Story 8.10 spec's own frozen Boundaries prose --
        // a spec asserting its own wording proves nothing about consumer fail-closed behaviour.
        // (DescribeIdentityGap's missing/mismatched outcomes are exercised directly by the
        // identity-gap theory below.)
        string sprintStatus = File.ReadAllText(
            Path.Combine(root, "_bmad-output/implementation-artifacts/sprint-status.yaml"));
        (string StoryKey, string SpecPath)[] downstreamConsumers =
        [
            ("8-8-client-mcp-apphost-build-and-deploy-cleanup",
                "_bmad-output/implementation-artifacts/spec-8-8-client-mcp-apphost-build-and-deploy-cleanup.md"),
        ];

        foreach ((string storyKey, string specPath) in downstreamConsumers)
        {
            if (!Regex.IsMatch(
                    sprintStatus,
                    $@"(?m)^\s*{Regex.Escape(storyKey)}:\s*blocked\s*$",
                    RegexOptions.CultureInvariant))
            {
                continue;
            }

            File.ReadAllText(Path.Combine(root, specPath)).ShouldContain(
                "Block If — available-row identities",
                Case.Sensitive,
                $"{storyKey} is still blocked, so its available-row identity gate must remain declared.");
        }
    }

    [Fact]
    public void FinalDependencyReceiptsMatchTheSelectedPackageAndSourceGraph()
    {
        string root = RepositoryRoot.Locate();
        string matrix = ReadMatrix();

        const string currentHeading = "### Current source observation — 2026-10-08";
        int currentStart = matrix.IndexOf(currentHeading, StringComparison.Ordinal);
        currentStart.ShouldBeGreaterThanOrEqualTo(0, currentHeading);
        int historicalStart = matrix.IndexOf("### Story 8.10 final retained-identity reconciliation", currentStart, StringComparison.Ordinal);
        historicalStart.ShouldBeGreaterThan(currentStart);
        string currentSelection = matrix[currentStart..historicalStart];
        currentSelection.ShouldContain($"EventStore package `{CurrentReleaseEventStoreVersion}`");
        currentSelection.ShouldContain("Selection does not revalidate historical parity receipts");
        foreach ((string path, string identity) in CurrentReleaseGitlinks)
        {
            string dependency = Path.GetFileName(path).Replace("Hexalith.", string.Empty, StringComparison.Ordinal);
            string row = currentSelection.Split('\n', StringSplitOptions.TrimEntries)
                .Single(line => line.StartsWith($"| {dependency} |", StringComparison.Ordinal));
            row.Split('|', StringSplitOptions.TrimEntries)[2].Split(' ')[0].ShouldBe($"`{identity}`", path);
            AssertGitlinkAndCheckout(root, path, identity);
            if (CurrentSourceDescribes.TryGetValue(path, out string? describe))
            {
                row.ShouldContain($"`{describe}`", Case.Sensitive, path);
                RunGit(root, "-C", path, "describe", "--tags", "--always", "--abbrev=8", "HEAD")
                    .Trim()
                    .ShouldBe(describe, path);
            }
        }

        RunGit(root, "-C", EventStoreRelativePath, "describe", "--tags", "--match", CurrentReleaseEventStoreDescribe, "--abbrev=8", "HEAD")
            .Trim()
            .ShouldBe(CurrentReleaseEventStoreDescribe);

        // Keep historical shell/a11y receipts bound to the identity at which they ran.
        matrix.ShouldContain(FrontComposerSha);
        matrix.ShouldContain("4.5.0");

        // Any present-tense HEAD receipt must match current release selection. Historical
        // receipts remain attached to their dated Parties revision and original evidence.
        foreach (Match receipt in Regex.Matches(
                     matrix,
                     @"git ls-tree HEAD (?<path>references/[A-Za-z.]+)`\s*->\s*`160000 commit (?<sha>[0-9a-f]{40})",
                     RegexOptions.CultureInvariant))
        {
            if (CurrentReleaseGitlinks.TryGetValue(receipt.Groups["path"].Value, out string? expectedSha))
            {
                receipt.Groups["sha"].Value.ShouldBe(
                    expectedSha,
                    $"Stale present-tense gitlink receipt for {receipt.Groups["path"].Value}.");
            }
        }

        string rootBuildProps = File.ReadAllText(Path.Combine(root, "Directory.Build.props"));
        rootBuildProps.ShouldContain("<UseHexalithProjectReferences Condition=\"'$(UseHexalithProjectReferences)' == ''\">false</UseHexalithProjectReferences>");
        rootBuildProps.ShouldContain("<HexalithEventStoreFromSource Condition=\"'$(UseHexalithProjectReferences)' == 'true'");
        rootBuildProps.ShouldContain("<HexalithCommonsHttpFromSource Condition=\"'$(HexalithCommonsHttpFromSource)' == '' and Exists('$(HexalithCommonsRoot)\\src\\libraries\\Hexalith.Commons.Http\\Hexalith.Commons.Http.csproj')\">true</HexalithCommonsHttpFromSource>");

        string rootPackages = File.ReadAllText(Path.Combine(root, "Directory.Packages.props"));
        rootPackages.ShouldContain("references/Hexalith.Builds/Props/Directory.Packages.props");
        string rootBuildTargets = string.Join(
            '\n',
            File.ReadAllText(Path.Combine(root, "Directory.Build.props")),
            File.ReadAllText(Path.Combine(root, "Directory.Build.targets")));
        rootBuildTargets.ShouldNotContain("Hexalith.Build.props");
        rootBuildTargets.ShouldNotContain("Hexalith.Package.props");
        string catalog = File.ReadAllText(Path.Combine(root, "references/Hexalith.Builds/Props/Directory.Packages.props"));
        rootPackages.ShouldContain($"<HexalithEventStoreVersion Condition=\"'$(HexalithEventStoreVersion)' == ''\">{CurrentReleaseEventStoreVersion}</HexalithEventStoreVersion>");
        catalog.ShouldContain($"<HexalithEventStoreVersion Condition=\"'$(HexalithEventStoreVersion)' == ''\">{CurrentCatalogEventStoreVersion}</HexalithEventStoreVersion>");
        catalog.ShouldContain("<PackageVersion Include=\"Hexalith.EventStore.Contracts\" Version=\"$(HexalithEventStoreVersion)\" />");
        catalog.ShouldContain("<HexalithCommonsVersion Condition=\"'$(HexalithCommonsVersion)' == ''\">2.30.1</HexalithCommonsVersion>");
        catalog.ShouldContain("<HexalithFrontComposerVersion Condition=\"'$(HexalithFrontComposerVersion)' == ''\">4.6.0</HexalithFrontComposerVersion>");
        catalog.ShouldContain("<HexalithMemoriesVersion Condition=\"'$(HexalithMemoriesVersion)' == ''\">2.27.1</HexalithMemoriesVersion>");
        catalog.ShouldContain("<HexalithTenantsVersion Condition=\"'$(HexalithTenantsVersion)' == ''\">5.7.0</HexalithTenantsVersion>");
        catalog.ShouldContain("<HexalithPartiesVersion Condition=\"'$(HexalithPartiesVersion)' == ''\">1.1.1</HexalithPartiesVersion>");
        // The selected Builds catalog routes this version through a property whose unconditional value applies to
        // every Parties project (only Hexalith.Folders.Aspire overrides it), so pin both halves exactly.
        catalog.ShouldContain("<HexalithAspireHostingDaprVersion>13.6.0-preview.1.261001-0243</HexalithAspireHostingDaprVersion>");
        catalog.ShouldContain("<PackageVersion Include=\"CommunityToolkit.Aspire.Hosting.Dapr\" Version=\"$(HexalithAspireHostingDaprVersion)\" />");
        catalog.ShouldContain("<PackageVersion Include=\"xunit.v3\" Version=\"4.0.1\" />");
        catalog.ShouldContain("<PackageVersion Include=\"xunit.v3.assert\" Version=\"4.0.1\" />");
        catalog.ShouldContain("<PackageVersion Include=\"xunit.v3.extensibility.core\" Version=\"4.0.1\" />");
        catalog.ShouldContain("<PackageVersion Include=\"xunit.runner.visualstudio\" Version=\"4.0.0\" />");
        catalog.ShouldContain("<PackageVersion Include=\"bunit\" Version=\"2.11.3\" />");

        matrix.ShouldContain("Package (default Release graph)");
        matrix.ShouldContain("Source (explicit project-reference graph)");
        matrix.ShouldContain("The catalog's `2.30.1` package is a fallback, not current consumption proof.");

        string[] projectFiles =
        [
            .. Directory.GetFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories).Where(path => IsNotBuildOutput(root, path)),
            .. Directory.GetFiles(Path.Combine(root, "samples"), "*.csproj", SearchOption.AllDirectories).Where(path => IsNotBuildOutput(root, path)),
            .. Directory.GetFiles(Path.Combine(root, "tests"), "*.csproj", SearchOption.AllDirectories).Where(path => IsNotBuildOutput(root, path)),
        ];
        string[] eventStoreConsumers = projectFiles
            .Where(path => File.ReadAllText(path).Contains("HexalithEventStoreFromSource", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
        eventStoreConsumers.ShouldNotBeEmpty();
        foreach (string projectFile in eventStoreConsumers)
        {
            EvaluatedProjectGraph packageGraph = EvaluateProjectGraph(root, projectFile, useSource: false);
            packageGraph.Properties["HexalithEventStoreVersion"].ShouldBe(CurrentReleaseEventStoreVersion, projectFile);
            packageGraph.PackageReferences.Any(static reference => reference.StartsWith("Hexalith.EventStore.", StringComparison.Ordinal))
                .ShouldBeTrue($"{projectFile} package graph");
            packageGraph.ProjectReferences.Any(static reference => reference.Contains("/references/Hexalith.EventStore/", StringComparison.Ordinal))
                .ShouldBeFalse($"{projectFile} package graph");

            EvaluatedProjectGraph sourceGraph = EvaluateProjectGraph(root, projectFile, useSource: true);
            sourceGraph.ProjectReferences.Any(static reference => reference.Contains("/references/Hexalith.EventStore/", StringComparison.Ordinal))
                .ShouldBeTrue($"{projectFile} source graph");
            sourceGraph.PackageReferences.Any(static reference => reference.StartsWith("Hexalith.EventStore.", StringComparison.Ordinal))
                .ShouldBeFalse($"{projectFile} source graph");
        }

        string[] commonsConsumers = projectFiles
            .Where(path => File.ReadAllText(path).Contains("HexalithCommonsHttpFromSource", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
        commonsConsumers.ShouldNotBeEmpty();
        foreach (string projectFile in commonsConsumers)
        {
            EvaluatedProjectGraph selectedGraph = EvaluateProjectGraph(root, projectFile, useSource: false);
            selectedGraph.Properties["HexalithCommonsVersion"].ShouldBe("2.30.1", projectFile);
            selectedGraph.Properties["HexalithCommonsHttpFromSource"].ShouldBe("true", projectFile);
            selectedGraph.ProjectReferences.Any(static reference => reference.EndsWith("/Hexalith.Commons.Http.csproj", StringComparison.Ordinal))
                .ShouldBeTrue($"{projectFile} selected Commons HTTP graph");
            selectedGraph.PackageReferences.Contains("Hexalith.Commons.Http", StringComparer.Ordinal)
                .ShouldBeFalse($"{projectFile} selected Commons HTTP graph");
        }

        string[] memoriesConsumers = projectFiles
            .Where(path => File.ReadAllText(path).Contains("HexalithMemoriesFromSource", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
        memoriesConsumers.ShouldNotBeEmpty();
        foreach (string projectFile in memoriesConsumers)
        {
            EvaluatedProjectGraph packageGraph = EvaluateProjectGraph(root, projectFile, useSource: false);
            packageGraph.Properties["HexalithMemoriesVersion"].ShouldBe("2.27.1", projectFile);
            packageGraph.PackageReferences.Any(static reference => reference.StartsWith("Hexalith.Memories.", StringComparison.Ordinal))
                .ShouldBeTrue($"{projectFile} package graph");
            packageGraph.ProjectReferences.Any(static reference => reference.Contains("/references/Hexalith.Memories/", StringComparison.Ordinal))
                .ShouldBeFalse($"{projectFile} package graph");
        }
    }

    [Theory]
    [InlineData("", PayloadProtectionEventStoreSha, "missing")]
    [InlineData("0000000000000000000000000000000000000000", PayloadProtectionEventStoreSha, "mismatched")]
    public void DependencyReceiptValidationFailsClosedForMissingOrMismatchedIdentity(
        string actualIdentity,
        string expectedIdentity,
        string expectedGap)
    {
        DescribeIdentityGap(actualIdentity, expectedIdentity).ShouldContain(expectedGap);
    }

    [Fact]
    public void Matrix_KeepsNoMigrationGateAndResidualBlockers()
    {
        string text = ReadMatrix();

        text.ShouldContain("No Parties source migration starts in Story 8.3.");
        text.ShouldContain("No production source migration was performed.");
        text.ShouldContain("Do not edit submodules or production source for Story 8.3.");
        text.ShouldContain("A checked-out submodule source file is not sufficient by itself.");
        text.ShouldContain("A row status of `available` means source evidence exists; it is not enough by itself.");
        text.ShouldContain("Release builds must use NuGet package references for external Hexalith libraries. Remove -p:UseHexalithProjectReferences=true or build Debug for source-debugging.");
        text.ShouldContain("five pre-existing tenant-event failures");

        foreach (MatrixRow row in ReadRows().Values)
        {
            row.Decision.ShouldContain("No Parties source migration starts in Story 8.3.");
            Regex.IsMatch(row.Decision, @"\bKeep\b.*\brollback path\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                .ShouldBeTrue(row.Surface);
            row.Decision.Contains("remove rollback", StringComparison.OrdinalIgnoreCase).ShouldBeFalse(row.Surface);
            row.Decision.Contains("without rollback", StringComparison.OrdinalIgnoreCase).ShouldBeFalse(row.Surface);
        }
    }

    [Fact]
    public void Matrix_AllRowsNameProofAndRollbackPath()
    {
        foreach (MatrixRow row in ReadRows().Values)
        {
            row.ProofRequired.ShouldContain("Proof required:");
            Regex.IsMatch(row.Decision, @"\bKeep\b.*\brollback path\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                .ShouldBeTrue(row.Surface);
        }
    }

    [Fact]
    public void Matrix_ValidationEvidenceNamesExpectedSymbols()
    {
        IReadOnlyDictionary<string, MatrixRow> rows = ReadRows();

        foreach (KeyValuePair<string, string[]> expected in RequiredEvidenceTokensBySurface)
        {
            rows.ContainsKey(expected.Key).ShouldBeTrue(expected.Key);
            string validationEvidence = rows[expected.Key].ValidationEvidence;

            foreach (string token in expected.Value)
            {
                validationEvidence.Contains(token, StringComparison.Ordinal).ShouldBeTrue($"{expected.Key}: {token}");
            }

            validationEvidence.Contains("Inspected", StringComparison.OrdinalIgnoreCase).ShouldBeFalse(expected.Key);
        }
    }

    [Fact]
    public void Matrix_ValidationEvidenceCommandsAreReproducible()
    {
        string root = RepositoryRoot.Locate();

        foreach (MatrixRow row in ReadRows().Values)
        {
            (string Pattern, string[] Paths, bool ExpectMatch)[] commands = ExtractRgCommands(row.ValidationEvidence).ToArray();
            commands.ShouldNotBeEmpty(row.Surface);
            AssertDeliveredApiCommands(row.Surface, commands);

            foreach ((string pattern, string[] paths, bool expectMatch) in commands)
            {
                AssertFixedStringSearch(root, pattern, paths, expectMatch);
            }

            if (string.Equals(row.Surface, PayloadProtectionSurface, StringComparison.Ordinal))
            {
                AssertPayloadProtectionEvidence(root, row, commands);
            }
        }
    }

    [Theory]
    [InlineData("EventStore degraded response and DAPR health checks", "AddEventStoreDaprHealthChecks")]
    [InlineData("Aspire publish helpers", "WithEventStoreJwtAuthentication")]
    [InlineData("Aspire publish helpers", "HexalithEventStoreJwtAuthenticationOptions")]
    [InlineData("Aspire publish helpers", "PrimaryAudience")]
    [InlineData("Aspire publish helpers", "ValidAudiences")]
    public void DeliveredApiCommandEvidenceCannotBeReplacedByProse(string surface, string pattern)
    {
        string evidence = ReadRows()[surface].ValidationEvidence;
        string path = RequiredDeliveredApiCommands[surface].Single(command => command.Pattern == pattern).Path;
        string command = $"`rg -n -F '{pattern}' {path}`";
        evidence.ShouldContain(command);
        string withoutCommand = evidence.Replace(command, string.Empty, StringComparison.Ordinal);

        Should.Throw<ShouldAssertException>(() => AssertDeliveredApiCommands(surface, ExtractRgCommands(withoutCommand)))
            .Message.ShouldContain(pattern);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DeliveredApiCommandEvidenceRequiresPositiveExactSourcePaths(bool expectMatch)
    {
        const string surface = "EventStore degraded response and DAPR health checks";
        (string pattern, string path) = RequiredDeliveredApiCommands[surface].Single();
        (string Pattern, string[] Paths, bool ExpectMatch)[] commands =
        [
            (pattern, expectMatch ? [path, "references/Hexalith.EventStore/src"] : [path], expectMatch),
        ];

        Should.Throw<ShouldAssertException>(() => AssertDeliveredApiCommands(surface, commands))
            .Message.ShouldContain(pattern);
    }

    private static void AssertDeliveredApiCommands(
        string surface,
        IEnumerable<(string Pattern, string[] Paths, bool ExpectMatch)> commands)
    {
        if (!RequiredDeliveredApiCommands.TryGetValue(surface, out (string Pattern, string Path)[]? required))
        {
            return;
        }

        foreach ((string pattern, string path) in required)
        {
            commands.Any(command => command.ExpectMatch
                    && string.Equals(command.Pattern, pattern, StringComparison.Ordinal)
                    && command.Paths.Length == 1
                    && string.Equals(command.Paths[0], path, StringComparison.Ordinal))
                .ShouldBeTrue($"{surface}: positive rg -n -F '{pattern}' command must name the exact source path {path}.");
        }
    }

    [Fact]
    public void CurrentStoryDiff_DoesNotModifyUnapprovedProductionMigrationPaths()
    {
        string root = RepositoryRoot.Locate();
        string spec = File.ReadAllText(Path.Combine(root, SpecRelativePath));
        string status = ReadFrontmatterValue(spec, "status");
        string baselineRevision = ReadFrontmatterValue(spec, "baseline_revision");
        string? finalRevision = string.Equals(status, "done", StringComparison.OrdinalIgnoreCase)
            ? ReadFrontmatterValue(spec, "final_revision")
            : null;

        AssertStoryMigrationScope(root, status, baselineRevision, finalRevision);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CompletedStoryMigrationScopeFailsClosedForMissingRevisions(bool missingBaseline)
    {
        string root = RepositoryRoot.Locate();
        string spec = File.ReadAllText(Path.Combine(root, SpecRelativePath));
        string baseline = ReadFrontmatterValue(spec, "baseline_revision");
        string final = ReadFrontmatterValue(spec, "final_revision");
        const string missingRevision = "0000000000000000000000000000000000000000";

        Should.Throw<ShouldAssertException>(() => AssertStoryMigrationScope(
                root,
                "done",
                missingBaseline ? missingRevision : baseline,
                missingBaseline ? final : missingRevision))
            .Message.ShouldContain(missingBaseline ? "baseline revision" : "final revision");
    }

    [Fact]
    public void CompletedStoryMigrationScopeRejectsForbiddenProductionPaths()
    {
        string root = RepositoryRoot.Locate();
        string spec = File.ReadAllText(Path.Combine(root, SpecRelativePath));
        string baseline = ReadFrontmatterValue(spec, "baseline_revision");
        const string laterRevision = "c095134f6ec648560954d6f0fc76858fd1d7cd0e";

        // Later Epic 8 migrations are authorized separately, but would be forbidden in the
        // recorded Story 8.3 range. This negative control proves that completed scope is checked.
        Should.Throw<ShouldAssertException>(() => AssertStoryMigrationScope(root, "done", baseline, laterRevision))
            .Message.ShouldContain("src/Hexalith.Parties");
    }

    [Theory]
    [InlineData("src/Hexalith.Parties/Domain/Épreuve.cs")]
    [InlineData("src/Hexalith.Parties/Domain/Line\nBreak.cs")]
    [InlineData("src/Hexalith.Parties/Domain/PartyAggregate.cs")]
    [InlineData(".gitmodules")]
    [InlineData("references/Hexalith.EventStore")]
    [InlineData("references/Hexalith.Builds")]
    [InlineData("references/Directory.Build.targets")]
    public void CompletedStoryMigrationScopeRejectsRawForbiddenPathsAndRenamedDeletions(string forbiddenPath)
    {
        ArgumentNullException.ThrowIfNull(forbiddenPath);

        string root = RepositoryRoot.Locate();
        string spec = File.ReadAllText(Path.Combine(root, SpecRelativePath));
        string baseline = ReadFrontmatterValue(spec, "baseline_revision");
        string final = ReadFrontmatterValue(spec, "final_revision");

        string ReadFixtureDiff(string[] arguments)
        {
            arguments.ShouldBe(["diff", "--no-renames", "--name-only", "-z", $"{baseline}..{final}", "--"]);
            // A production-to-test rename must report the deleted source as well as its
            // destination. NUL separators keep Unicode and newline-containing paths raw.
            return $"{forbiddenPath}\0tests/Hexalith.Parties.Tests/Renamed.cs\0";
        }

        Should.Throw<ShouldAssertException>(() => AssertStoryMigrationScope(root, "done", baseline, final, ReadFixtureDiff))
            .Message.ShouldContain(forbiddenPath.Split('\n')[0]);
    }

    private static void AssertStoryMigrationScope(
        string root,
        string status,
        string baselineRevision,
        string? finalRevision,
        Func<string[], string>? readCompletedDiff = null)
    {
        if (string.Equals(status, "done", StringComparison.OrdinalIgnoreCase))
        {
            GitObjectExists(root, baselineRevision).ShouldBeTrue($"Story 8.3 baseline revision is missing: {baselineRevision}");
            finalRevision.ShouldNotBeNullOrWhiteSpace("Story 8.3 final revision is missing.");
            GitObjectExists(root, finalRevision!).ShouldBeTrue($"Story 8.3 final revision is missing: {finalRevision}");

            string[] arguments = ["diff", "--no-renames", "--name-only", "-z", $"{baselineRevision}..{finalRevision}", "--"];
            // The fixture reader exercises the same completed branch without creating commits
            // or modifying the repository. The ordinary gate always reads the actual git diff.
            string diffNames = readCompletedDiff is null ? RunGit(root, arguments) : readCompletedDiff(arguments);
            string[] forbiddenChanges = diffNames
                .Split('\0', StringSplitOptions.RemoveEmptyEntries)
                .Where(IsForbiddenCompletedStoryMigrationPath)
                .ToArray();
            forbiddenChanges.ShouldBeEmpty($"Story 8.3 historical scope {baselineRevision}..{finalRevision}");
            return;
        }

        if (string.Equals(baselineRevision, "NO_VCS", StringComparison.Ordinal))
        {
            return;
        }

        GitObjectExists(root, baselineRevision).ShouldBeTrue(baselineRevision);

        string currentDiffNames = RunGit(root, "diff", "--name-only", baselineRevision, "--");
        string untrackedNames = RunGit(root, "ls-files", "--others", "--exclude-standard");
        string[] changedPaths = string.Concat(currentDiffNames, "\n", untrackedNames)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();

        string[] currentForbiddenChanges = changedPaths
            .Where(IsForbiddenStoryMigrationPath)
            .Where(static path => !ApprovedEpic8MigrationPaths.Contains(path, StringComparer.Ordinal))
            .ToArray();

        currentForbiddenChanges.ShouldBeEmpty();

        foreach (string approvedPath in changedPaths.Where(static path => ApprovedEpic8MigrationPaths.Contains(path, StringComparer.Ordinal)))
        {
            AssertApprovedEpic8DiffIsNarrow(root, baselineRevision, approvedPath);
        }
    }

    private static bool IsForbiddenCompletedStoryMigrationPath(string path)
        => IsForbiddenStoryMigrationPath(path)
            || string.Equals(path, ".gitmodules", StringComparison.Ordinal)
            || path.StartsWith("references/", StringComparison.Ordinal);

    private static IReadOnlyDictionary<string, string> ParseFinalConsumptionRows(string matrix)
    {
        const string heading = "### Story 8.10 final retained-identity reconciliation";
        int start = matrix.IndexOf(heading, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0, heading);
        int end = matrix.IndexOf(StartMarker, start, StringComparison.Ordinal);
        end.ShouldBeGreaterThan(start, StartMarker);

        Dictionary<string, string> rows = new(StringComparer.Ordinal);
        foreach (string line in matrix[start..end].Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!line.StartsWith('|') || line.StartsWith("| ---", StringComparison.Ordinal))
            {
                continue;
            }

            string[] cells = SplitMarkdownTableRow(line);
            if (string.Equals(cells.Length > 0 ? cells[0] : string.Empty, "Surface", StringComparison.Ordinal))
            {
                continue;
            }

            cells.Length.ShouldBe(5, $"malformed reconciliation-table row (expected 5 cells): {line}");

            string key = $"{cells[0]}|{cells[1]}";
            rows.TryAdd(key, string.Join(" | ", cells)).ShouldBeTrue(key);
        }

        rows.ShouldNotBeEmpty(heading);
        return rows;
    }

    private static string[] DescribeRowTokenGaps(string row, IEnumerable<string> expectedTokens)
        => expectedTokens
            .Where(token => !row.Contains(token, StringComparison.Ordinal))
            .Select(static token => $"missing:{token}")
            .ToArray();

    private static EvaluatedProjectGraph EvaluateProjectGraph(string root, string projectFile, bool useSource)
    {
        string relativeProject = Path.GetRelativePath(root, projectFile);
        string output = RunProcess(
            root,
            "dotnet",
            "msbuild",
            relativeProject,
            "-nologo",
            "-getProperty:HexalithEventStoreVersion,HexalithCommonsVersion,HexalithMemoriesVersion,UseHexalithProjectReferences,HexalithEventStoreFromSource,HexalithCommonsHttpFromSource,HexalithMemoriesFromSource",
            "-getItem:PackageReference,ProjectReference",
            $"-p:UseHexalithProjectReferences={useSource.ToString().ToLowerInvariant()}",
            "-p:NuGetAudit=false",
            "-p:MinVerVersionOverride=1.0.0");
        // MSBuild can emit restore or NuGet preamble on stdout ahead of the JSON payload; parsing
        // the raw stream throws JsonException instead of reporting a graph difference.
        int jsonStart = output.IndexOf('{', StringComparison.Ordinal);
        jsonStart.ShouldBeGreaterThanOrEqualTo(0, $"MSBuild produced no JSON payload for {relativeProject}: {output}");

        using JsonDocument document = JsonDocument.Parse(output[jsonStart..]);
        Dictionary<string, string> properties = document.RootElement.GetProperty("Properties")
            .EnumerateObject()
            .ToDictionary(static property => property.Name, static property => property.Value.GetString() ?? string.Empty, StringComparer.Ordinal);
        JsonElement items = document.RootElement.GetProperty("Items");
        string[] packages = ReadEvaluatedItems(items, "PackageReference", "Identity");
        string[] projects = ReadEvaluatedItems(items, "ProjectReference", "FullPath")
            .Select(static path => path.Replace('\\', '/'))
            .ToArray();
        return new EvaluatedProjectGraph(properties, packages, projects);
    }

    private static string[] ReadEvaluatedItems(JsonElement items, string itemName, string metadataName)
        => items.TryGetProperty(itemName, out JsonElement values)
            ? values.EnumerateArray()
                .Select(item => item.TryGetProperty(metadataName, out JsonElement value) ? value.GetString() ?? string.Empty : string.Empty)
                .ToArray()
            : [];

    private static string ReadMatrix()
        => File.ReadAllText(Path.Combine(RepositoryRoot.Locate(), MatrixRelativePath));

    private static IReadOnlyDictionary<string, MatrixRow> ReadRows()
    {
        string section = ReadMarkedSection(ReadMatrix());
        Dictionary<string, MatrixRow> rows = new(StringComparer.Ordinal);

        foreach (string line in section.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = line.Trim();
            if (!trimmed.StartsWith('|'))
            {
                continue;
            }

            string[] cells = SplitMarkdownTableRow(trimmed);
            if (IsMarkdownSeparatorRow(cells))
            {
                continue;
            }

            if (cells.Length == 8 && string.Equals(cells[0], "Surface", StringComparison.Ordinal))
            {
                continue;
            }

            cells.Length.ShouldBe(8, trimmed);

            MatrixRow row = new(
                cells[0],
                cells[1],
                cells[2],
                cells[3],
                cells[4],
                cells[5],
                cells[6],
                cells[7]);

            rows.TryAdd(row.Surface, row).ShouldBeTrue(row.Surface);
        }

        return rows;
    }

    private static string ReadMarkedSection(string text)
    {
        Regex.Matches(text, Regex.Escape(StartMarker), RegexOptions.CultureInvariant)
            .Count
            .ShouldBe(1, StartMarker);
        Regex.Matches(text, Regex.Escape(EndMarker), RegexOptions.CultureInvariant)
            .Count
            .ShouldBe(1, EndMarker);

        int start = text.IndexOf(StartMarker, StringComparison.Ordinal);
        if (start < 0)
        {
            throw new InvalidOperationException($"Matrix start marker '{StartMarker}' was not found.");
        }

        int end = text.IndexOf(EndMarker, start, StringComparison.Ordinal);
        if (end < 0)
        {
            throw new InvalidOperationException($"Matrix end marker '{EndMarker}' was not found.");
        }

        return text[(start + StartMarker.Length)..end];
    }

    private static string[] SplitMarkdownTableRow(string row)
    {
        List<string> cells = [];
        int startIndex = row.StartsWith('|') ? 1 : 0;
        int endIndex = row.EndsWith('|') ? row.Length - 1 : row.Length;
        var current = new System.Text.StringBuilder();
        bool escaped = false;
        bool inCodeSpan = false;

        for (int i = startIndex; i < endIndex; i++)
        {
            char currentChar = row[i];
            if (escaped)
            {
                _ = current.Append(currentChar);
                escaped = false;
                continue;
            }

            if (currentChar == '\\')
            {
                escaped = true;
                continue;
            }

            if (currentChar == '`')
            {
                inCodeSpan = !inCodeSpan;
                _ = current.Append(currentChar);
                continue;
            }

            if (currentChar == '|' && !inCodeSpan)
            {
                cells.Add(current.ToString().Trim());
                _ = current.Clear();
                continue;
            }

            _ = current.Append(currentChar);
        }

        if (escaped)
        {
            _ = current.Append('\\');
        }

        cells.Add(current.ToString().Trim());
        return [.. cells];
    }

    private static string[] SplitEvidencePaths(string evidencePaths)
        => evidencePaths
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(static path => path.Trim())
            .Where(static path => path.Length > 0)
            .ToArray();

    private static void AssertRequiredRow(
        IReadOnlyDictionary<string, MatrixRow> rows,
        KeyValuePair<string, string[]> required)
    {
        rows.ContainsKey(required.Key).ShouldBeTrue(required.Key);

        MatrixRow row = rows[required.Key];
        row.Owner.ShouldNotBeNullOrWhiteSpace();
        row.ProofRequired.ShouldNotBeNullOrWhiteSpace();
        row.ValidationEvidence.ShouldNotBeNullOrWhiteSpace();
        row.Decision.ShouldContain("No Parties source migration starts in Story 8.3.");

        HashSet<string> dependentStories = ParseStoryIds(row.DependentStories);
        dependentStories.SetEquals(required.Value).ShouldBeTrue(row.Surface);
        foreach (string expectedStory in required.Value)
        {
            dependentStories.Contains(expectedStory).ShouldBeTrue(row.Surface);
        }
    }

    private static HashSet<string> ParseStoryIds(string dependentStories)
        => Regex.Matches(
                dependentStories,
                @"(?<![A-Za-z0-9])8\.(?:[4-9]|10)(?![A-Za-z0-9])",
                RegexOptions.CultureInvariant)
            .Select(static match => match.Value)
            .ToHashSet(StringComparer.Ordinal);

    private static bool IsMarkdownSeparatorRow(string[] cells)
        => cells.All(static cell =>
            cell.Length > 0 &&
            cell.All(static character => character is '-' or ':' or ' '));

    private static string NormalizeEvidencePath(string repositoryRoot, string evidencePath)
    {
        Path.IsPathFullyQualified(evidencePath).ShouldBeFalse(evidencePath);
        evidencePath.Contains("..", StringComparison.Ordinal).ShouldBeFalse(evidencePath);

        string fullPath = Path.GetFullPath(Path.Combine(repositoryRoot, evidencePath));
        string rootWithSeparator = repositoryRoot.EndsWith(Path.DirectorySeparatorChar)
            ? repositoryRoot
            : repositoryRoot + Path.DirectorySeparatorChar;

        fullPath.StartsWith(rootWithSeparator, StringComparison.Ordinal).ShouldBeTrue(evidencePath);

        return Path.GetRelativePath(repositoryRoot, fullPath)
            .Replace(Path.DirectorySeparatorChar, '/');
    }

    private static IEnumerable<string> ExpectedOwnerPrefixes(string owner)
        => OwnerSegments(owner).SelectMany(segment => OwnerPrefixes[segment]);

    private static IEnumerable<string> OwnerSegments(string owner)
    {
        foreach (string rawGroup in owner.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string group = Regex.Replace(rawGroup, @"\s*\([^)]*\)\s*$", string.Empty, RegexOptions.CultureInvariant).Trim();
            foreach (string segment in group.Split(" and ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                OwnerPrefixes.ContainsKey(segment).ShouldBeTrue($"Unknown owner segment: {segment}");
                yield return segment;
            }
        }
    }

    private static IEnumerable<string> ExcludedPrefixes(string ownerSegment)
        => ExcludedOwnerPrefixes.TryGetValue(ownerSegment, out string[]? prefixes)
            ? prefixes
            : [];

    private static IEnumerable<string> AdditionalContextPrefixes(string surface)
        => AdditionalContextEvidencePrefixes.TryGetValue(surface, out string[]? prefixes)
            ? prefixes
            : [];

    private static bool IsUnderPathPrefix(string path, string prefix)
    {
        string normalizedPrefix = prefix.TrimEnd('/');
        return string.Equals(path, normalizedPrefix, StringComparison.Ordinal) ||
            path.StartsWith(normalizedPrefix + "/", StringComparison.Ordinal);
    }

    private static bool IsForbiddenStoryMigrationPath(string path)
        => ForbiddenStoryMigrationFiles.Contains(path, StringComparer.Ordinal) ||
            ForbiddenStoryMigrationPathPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.Ordinal));

    private static void AssertApprovedEpic8DiffIsNarrow(string root, string baselineRevision, string path)
    {
        if (ApprovedStory85SdkHostCutoverPaths.Contains(path, StringComparer.Ordinal))
        {
            AssertApprovedStory85DiffIsNarrow(root, baselineRevision, path);
            return;
        }

        AssertApprovedStory84DiffIsNarrow(root, baselineRevision, path);
    }

    private static void AssertApprovedStory85DiffIsNarrow(string root, string baselineRevision, string path)
    {
        if (string.Equals(path, "src/Hexalith.Parties/Domain/PartyAggregate.cs", StringComparison.Ordinal))
        {
            string oldAggregate = RunGit(root, "show", $"{baselineRevision}:src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs")
                .Replace("using Hexalith.EventStore.Client.Aggregates;\n", "using Hexalith.EventStore.Client.Aggregates;\nusing Hexalith.EventStore.Client.Attributes;\n", StringComparison.Ordinal)
                .Replace("namespace Hexalith.Parties.Server.Aggregates;", "namespace Hexalith.Parties.Domain;", StringComparison.Ordinal)
                .Replace("public sealed class PartyAggregate", "[EventStoreDomain(\"party\")]\npublic sealed class PartyAggregate", StringComparison.Ordinal);
            string newAggregate = File.ReadAllText(Path.Combine(root, path));
            newAggregate.ShouldBe(oldAggregate);
            return;
        }

        if (string.Equals(path, "src/Hexalith.Parties/Domain/PartyDomainServiceInvoker.cs", StringComparison.Ordinal))
        {
            File.Exists(Path.Combine(root, path)).ShouldBeFalse(path);
            return;
        }

        if (string.Equals(path, "src/Hexalith.Parties/Domain/PartyDomainProcessor.cs", StringComparison.Ordinal))
        {
            string source = File.ReadAllText(Path.Combine(root, path));
            source.ShouldContain(": IDomainProcessor");
            source.ShouldContain("IAggregateReplay");
            source.ShouldContain("AggregateReconstructionResult Replay");
            source.ShouldContain("TryRejectInvalidPayloadAsync");
            source.ShouldContain("UnprotectCurrentStateAsync");
            source.ShouldContain("SaveErasureStatusUpdatesAsync");
            source.ShouldContain("InvokeRetryErasureVerificationAsync");
            source.ShouldContain("PartyCommandValidationRejected");
            source.ShouldContain("PartyPayloadProtectionService.RedactProtectedPayload");
            source.ShouldNotContain("IDomainServiceInvoker");
            source.ShouldNotContain("DomainServiceNotFoundException");
            return;
        }

        if (string.Equals(path, "src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs", StringComparison.Ordinal))
        {
            string source = File.ReadAllText(Path.Combine(root, path));
            source.ShouldContain("AddKeyedScoped<IDomainProcessor, PartyDomainProcessor>(PartyDomain)");
            source.ShouldContain("PartyDomainCaseVariants()");
            source.ShouldContain("AddEventStoreReadModelStore()");
            source.ShouldContain("AddEventStoreDataProtection(configuration, \"Hexalith.Parties\")");
            source.ShouldContain("AddEventStoreQueryCursorCodec(\"Hexalith.Parties.QueryCursor.v1\")");
            source.ShouldContain("AddScoped<PartySdkQueryService>()");
            source.ShouldContain("TryAddSingleton(TimeProvider.System)");
            source.ShouldContain("RegisterActor<PartyKeyRetryActor>");
            source.ShouldNotContain("RegisterActor<AggregateActor>");
            source.ShouldNotContain("AddHostedService<ProjectionDiscoveryHostedService>");
            source.ShouldNotContain("AddHostedService<ActiveRebuildIndexCleanupService>");
            source.ShouldNotContain("AddHostedService<ProjectionPollerService>");
            source.ShouldNotContain("TryAddTransient<IDomainServiceInvoker, DaprDomainServiceInvoker>");
            source.ShouldNotContain("AddEventStoreServer(configuration)");
            source.ShouldNotContain("PartyDomainServiceInvoker");
            return;
        }

        if (string.Equals(path, "src/Hexalith.Parties/Program.cs", StringComparison.Ordinal))
        {
            string source = File.ReadAllText(Path.Combine(root, path));
            source.ShouldContain("AddEventStoreDomainService(");
            source.ShouldContain("typeof(PartyAggregate).Assembly");
            source.ShouldContain("typeof(PartyDetailProjectionHandler).Assembly");
            source.ShouldContain("UseEventStoreDomainService()");
            source.ShouldContain("ConfigureOpenTelemetryTracerProvider");
            source.ShouldContain("ConfigureOpenTelemetryMeterProvider");
            source.ShouldContain("AddPartiesDaprHealthChecks");
            source.ShouldContain("UseMiddleware<DegradedResponseMiddleware>");
            source.ShouldContain("MapSubscribeHandler()");
            source.ShouldContain("MapEventStoreDomainEvents()");
            source.ShouldContain("MapActorsHandlers()");
            source.ShouldNotContain("MapPost(\"/process\"");
            source.ShouldNotContain("MapHexalithDefaultEndpoints(ConfigurePartiesServiceDefaults)");
            return;
        }

        if (string.Equals(path, "src/Hexalith.Parties/Hexalith.Parties.csproj", StringComparison.Ordinal))
        {
            string source = File.ReadAllText(Path.Combine(root, path));
            source.ShouldContain("Hexalith.EventStore.DomainService");
            source.ShouldContain("Hexalith.EventStore.Server");
            source.ShouldNotContain("Hexalith.Parties.Server");
            source.ShouldNotContain("Hexalith.Parties.ServiceDefaults");
            source.ShouldNotContain("Hexalith.Commons.ServiceDefaults");
            source.ShouldNotContain("Version=");
            return;
        }

        throw new InvalidOperationException($"No Story 8.5 diff guard is defined for '{path}'.");
    }

    private static void AssertApprovedStory84DiffIsNarrow(string root, string baselineRevision, string path)
    {
        if (string.Equals(path, "src/Hexalith.Parties/Domain/PartyAggregate.cs", StringComparison.Ordinal))
        {
            string oldAggregate = RunGit(root, "show", $"{baselineRevision}:src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs")
                .Replace("namespace Hexalith.Parties.Server.Aggregates;", "namespace Hexalith.Parties.Domain;", StringComparison.Ordinal);
            string newAggregate = File.ReadAllText(Path.Combine(root, path));
            newAggregate.ShouldBe(oldAggregate);
            return;
        }

        string[] deletedRetiredProjectPaths =
        [
            "src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs",
            "src/Hexalith.Parties.Server/Hexalith.Parties.Server.csproj",
            "src/Hexalith.Parties.ServiceDefaults/Extensions.cs",
            "src/Hexalith.Parties.ServiceDefaults/Hexalith.Parties.ServiceDefaults.csproj",
        ];
        if (deletedRetiredProjectPaths.Contains(path, StringComparer.Ordinal))
        {
            File.Exists(Path.Combine(root, path)).ShouldBeFalse(path);
            return;
        }

        string diff = RunGit(root, "diff", "--unified=0", baselineRevision, "--", path);
        foreach (string line in diff.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!IsChangedContentLine(line))
            {
                continue;
            }

            string content = line[1..].TrimEnd('\r');
            IsAllowedStory84ChangedLine(path, content).ShouldBeTrue($"{path}: {line}");
        }
    }

    private static bool IsChangedContentLine(string line)
        => (line.StartsWith('+') || line.StartsWith('-'))
            && !line.StartsWith("+++", StringComparison.Ordinal)
            && !line.StartsWith("---", StringComparison.Ordinal);

    private static bool IsAllowedStory84ChangedLine(string path, string line)
        => ApprovedStory84ChangedLines.TryGetValue(path, out string[]? allowedLines) &&
            allowedLines.Contains(line, StringComparer.Ordinal);

    private static bool ContainsExactToken(string value, string token)
        => Regex.IsMatch(
            value,
            $@"(?<![A-Za-z0-9]){Regex.Escape(token)}(?![A-Za-z0-9])",
            RegexOptions.CultureInvariant);

    private static IEnumerable<(string Pattern, string[] Paths, bool ExpectMatch)> ExtractRgCommands(string validationEvidence)
    {
        foreach (Match match in Regex.Matches(
            validationEvidence,
            @"`rg\s+-n\s+-F\s+'(?<pattern>[^']+)'\s+(?<paths>[^`]+)`(?<noMatch>\s*\(expected no matches\))?",
            RegexOptions.CultureInvariant))
        {
            string[] paths = match.Groups["paths"].Value
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            yield return (match.Groups["pattern"].Value, paths, !match.Groups["noMatch"].Success);
        }
    }

    private static void AssertFixedStringSearch(string root, string pattern, string[] paths, bool expectMatch)
    {
        string normalizedRoot = Path.GetFullPath(root);
        List<string> matches = [];

        foreach (string path in paths)
        {
            string normalizedPath = NormalizeEvidencePath(normalizedRoot, path);
            string fullPath = Path.Combine(normalizedRoot, normalizedPath);
            (File.Exists(fullPath) || Directory.Exists(fullPath)).ShouldBeTrue(path);

            IEnumerable<string> files = File.Exists(fullPath)
                ? [fullPath]
                : EnumerateSearchFiles(fullPath);

            foreach (string file in files)
            {
                if (File.ReadAllText(file).Contains(pattern, StringComparison.Ordinal))
                {
                    matches.Add(Path.GetRelativePath(normalizedRoot, file).Replace(Path.DirectorySeparatorChar, '/'));
                }
            }
        }

        bool hasMatches = matches.Count > 0;
        hasMatches.ShouldBe(
            expectMatch,
            $"rg -n -F '{pattern}' {string.Join(' ', paths)}{Environment.NewLine}{string.Join(Environment.NewLine, matches)}");
    }

    private static void AssertPayloadProtectionEvidence(
        string root,
        MatrixRow row,
        (string Pattern, string[] Paths, bool ExpectMatch)[] commands)
    {
        string finalLedger = ReadMatrix();
        finalLedger.ShouldContain("Story 8.10 final retained-identity reconciliation");
        finalLedger.ShouldContain(PayloadProtectionEventStoreSha);
        finalLedger.ShouldContain($"Released package `{EventStorePackageVersion}`");
        row.Status.ShouldBe("needs-additive-api");

        foreach (string path in RequiredAbsentPayloadProtectionPaths)
        {
            row.ValidationEvidence.ShouldContain($"`test ! -f {path}`");
            File.Exists(Path.Combine(root, path)).ShouldBeFalse(path);
        }

        File.ReadAllText(Path.Combine(root, "references/Hexalith.Builds/Props/Directory.Packages.props"))
            .ShouldNotContain("Hexalith.EventStore.PayloadProtection", Case.Sensitive,
                "G5 is not enrolled in the selected package catalog.");
        File.ReadAllText(Path.Combine(root, "references/Hexalith.EventStore/tools/release-packages.json"))
            .ShouldNotContain("Hexalith.EventStore.PayloadProtection", Case.Sensitive,
                "G5 is not enrolled in the owner release inventory.");

        foreach (string status in RequiredPayloadProtectionStoryStatuses)
        {
            HasPositiveFixedStringCommand(commands, status, EventStoreSprintStatusRelativePath)
                .ShouldBeTrue(status);
        }

        foreach (string token in RequiredPositivePayloadProtectionSpecTokens)
        {
            HasPositiveFixedStringCommand(commands, token, EventStorePayloadProtectionSpecRelativePath)
                .ShouldBeTrue(token);
        }

        string ownerSpec = File.ReadAllText(Path.Combine(root, EventStorePayloadProtectionSpecRelativePath));
        ExtractFrontMatterValue(ownerSpec, "status").ShouldBe("approved-authorized");
        ExtractFrontMatterValue(ownerSpec, "decision").ShouldBe("adopted-amendment");
        ExtractFrontMatterValue(ownerSpec, "story_8_2_authorized").ShouldBe("true");
        HashSet<string> exactPackageCodeSpans = Regex.Matches(
                ownerSpec,
                @"`(?<package>Hexalith\.EventStore\.PayloadProtection(?:\.AzureKeyVault)?)`",
                RegexOptions.CultureInvariant)
            .Select(static match => match.Groups["package"].Value)
            .ToHashSet(StringComparer.Ordinal);
        exactPackageCodeSpans.Contains("Hexalith.EventStore.PayloadProtection").ShouldBeTrue();
        exactPackageCodeSpans.Contains("Hexalith.EventStore.PayloadProtection.AzureKeyVault").ShouldBeTrue();

        const string authorityLine = "| Content- and identity-bound G5 decision | 8.11 G5 Evidence And Approval Closure | `backlog` | 8.10 complete plus all named approvals; only this story can record `available` and unblock Parties Story 8.7. |";
        File.ReadAllLines(Path.Combine(root, EventStoreStoryMigrationRelativePath))
            .Contains(authorityLine, StringComparer.Ordinal)
            .ShouldBeTrue(authorityLine);

        HasPositiveFixedStringCommand(
                commands,
                "TryAddSingleton<IEventPayloadProtectionService, NoOpEventPayloadProtectionService>",
                "references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs")
            .ShouldBeTrue("The recorded no-op provider must remain the default server registration.");

        foreach (string rollbackPath in RequiredPayloadProtectionRollbackPaths)
        {
            File.Exists(Path.Combine(root, rollbackPath)).ShouldBeTrue(rollbackPath);
        }

        string ledger = File.ReadAllText(Path.Combine(root, "_bmad-output/implementation-artifacts/sprint-status.yaml"));
        string retentionItem = ExtractYamlListItemContaining(ledger, PayloadProtectionRetentionAction);
        string normalizedRetentionItem = Regex.Replace(
            retentionItem.Replace("#", string.Empty, StringComparison.Ordinal),
            @"\s+",
            " ",
            RegexOptions.CultureInvariant);
        normalizedRetentionItem.ShouldContain("owner contracts/core are partial delivery (8.2 done, 8.3 in-progress)");
        normalizedRetentionItem.ShouldContain("G5 closure/parity/rollback remain absent");
        normalizedRetentionItem.ShouldContain("Story 8.11 alone may record G5 `available` and unblock Parties Story 8.7");
        normalizedRetentionItem.ShouldContain("Story 8.7 remains blocked");
        normalizedRetentionItem.ShouldContain("retention action stays open");
        normalizedRetentionItem.ShouldContain("status: open");

        // Current structural G5 gates remain independent of the dated identity receipt.
        // Verify that receipt at its original Parties commit without refreshing runtime/parity proof.
        string expectedGitlink = $"160000 commit {PayloadProtectionEventStoreSha} {EventStoreRelativePath}";
        RunGit(root, "ls-tree", PayloadProtectionPartiesReceiptSha, EventStoreRelativePath)
            .Trim()
            .Replace('\t', ' ')
            .ShouldBe(expectedGitlink);

        RunGit(root, "-C", EventStoreRelativePath, "rev-parse", "--verify", $"{PayloadProtectionEventStoreSha}^{{commit}}")
            .Trim()
            .ShouldBe(PayloadProtectionEventStoreSha);

        RunGit(root, "-C", EventStoreRelativePath, "describe", "--tags", "--exact-match", "--match", PayloadProtectionEventStoreDescribe, PayloadProtectionEventStoreSha)
            .Trim()
            .ShouldBe(PayloadProtectionEventStoreDescribe);
    }

    private static bool HasPositiveFixedStringCommand(
        IEnumerable<(string Pattern, string[] Paths, bool ExpectMatch)> commands,
        string pattern,
        string expectedPath)
        => commands.Any(command =>
            command.ExpectMatch &&
            string.Equals(command.Pattern, pattern, StringComparison.Ordinal) &&
            command.Paths.SequenceEqual([expectedPath], StringComparer.Ordinal));

    private static string ExtractFrontMatterValue(string markdown, string key)
    {
        string[] lines = markdown.Split('\n');
        lines[0].TrimEnd('\r').ShouldBe("---");

        int closingDelimiter = Array.FindIndex(
            lines,
            1,
            static line => string.Equals(line.TrimEnd('\r'), "---", StringComparison.Ordinal));
        closingDelimiter.ShouldBeGreaterThan(0);

        string prefix = $"{key}:";
        string[] matches = lines[1..closingDelimiter]
            .Select(static line => line.TrimEnd('\r'))
            .Where(line => line.StartsWith(prefix, StringComparison.Ordinal))
            .ToArray();

        return matches.ShouldHaveSingleItem()[prefix.Length..].Trim().Trim('\'', '"');
    }

    private static string ExtractYamlListItemContaining(string yaml, string value)
    {
        int valueIndex = yaml.IndexOf(value, StringComparison.Ordinal);
        valueIndex.ShouldBeGreaterThanOrEqualTo(0, value);

        int itemStart = yaml.LastIndexOf("\n  - ", valueIndex, StringComparison.Ordinal);
        itemStart.ShouldBeGreaterThanOrEqualTo(0, value);

        int nextItemStart = yaml.IndexOf("\n  - ", valueIndex, StringComparison.Ordinal);
        return nextItemStart < 0
            ? yaml[itemStart..]
            : yaml[itemStart..nextItemStart];
    }

    private static IEnumerable<string> EnumerateSearchFiles(string directory)
    {
        var pending = new Stack<string>();
        pending.Push(directory);

        while (pending.TryPop(out string? current))
        {
            foreach (string childDirectory in Directory.EnumerateDirectories(current))
            {
                string name = Path.GetFileName(childDirectory);
                if (name is not ".git" and not "bin" and not "obj" and not "node_modules" and not "TestResults")
                {
                    pending.Push(childDirectory);
                }
            }

            foreach (string file in Directory.EnumerateFiles(current))
            {
                yield return file;
            }
        }
    }

    private static string ToSearchableText(MatrixRow row)
        => string.Join(
            " ",
            row.Surface,
            row.Owner,
            row.Status,
            row.EvidencePaths,
            row.ProofRequired,
            row.DependentStories,
            row.Decision,
            row.ValidationEvidence);

    private static string ReadFrontmatterValue(string markdown, string key)
    {
        Match match = Regex.Match(
            markdown,
            $@"(?m)^{Regex.Escape(key)}:\s*['""]?(?<value>[^'""\r\n]+)['""]?\s*$",
            RegexOptions.CultureInvariant);

        match.Success.ShouldBeTrue(key);
        return match.Groups["value"].Value.Trim();
    }

    private static bool GitObjectExists(string root, string revision)
    {
        using var process = CreateGitProcess(root, "cat-file", "-e", $"{revision}^{{commit}}");

        process.Start().ShouldBeTrue();
        process.WaitForExit();

        return process.ExitCode == 0;
    }

    private static bool IsNotBuildOutput(string root, string path)
        => !Path.GetRelativePath(root, path).Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            && !Path.GetRelativePath(root, path).Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

    private static void AssertGitlinkAndCheckout(string root, string relativePath, string expectedIdentity)
    {
        string gitlink = RunGit(root, "ls-tree", "HEAD", relativePath).Trim();
        string checkout = RunGit(root, "-C", relativePath, "rev-parse", "HEAD").Trim();

        // Only a committed root gitlink proves the selected source identity. Staging a
        // different checkout must not satisfy the gate before it is committed.
        gitlink.Replace('\t', ' ').ShouldBe(
            $"160000 commit {expectedIdentity} {relativePath}",
            $"{relativePath} committed gitlink must match selected {expectedIdentity}; HEAD entry was '{gitlink}'.");

        DescribeIdentityGap(checkout, expectedIdentity).ShouldBeEmpty(relativePath);
        RunGit(root, "-C", relativePath, "status", "--porcelain")
            .ShouldBeNullOrWhiteSpace($"{relativePath} must be clean for immutable consumption proof.");

        string[] initializedNestedSubmodules = RunGit(root, "-C", relativePath, "submodule", "status")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static status => !status.StartsWith("-", StringComparison.Ordinal))
            .ToArray();
        initializedNestedSubmodules.ShouldBeEmpty($"{relativePath} must not contain initialized nested submodules.");
    }

    private static string DescribeIdentityGap(string actualIdentity, string expectedIdentity)
    {
        if (string.IsNullOrWhiteSpace(actualIdentity))
        {
            return "missing dependency identity";
        }

        return string.Equals(actualIdentity, expectedIdentity, StringComparison.Ordinal)
            ? string.Empty
            : $"mismatched dependency identity: expected {expectedIdentity}, actual {actualIdentity}";
    }

    private static string RunGit(string root, params string[] arguments)
        => RunProcess(root, "git", arguments);

    private static string RunProcess(string root, string fileName, params string[] arguments)
    {
        using Process process = CreateProcess(root, fileName);
        foreach (string argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start().ShouldBeTrue();

        // Drain stdout and stderr concurrently and under one shared deadline. Reading either
        // stream to completion synchronously deadlocks if the child fills the other pipe's
        // buffer, or blocks forever if the child hangs while keeping a stream open -- a verbose
        // or stalled MSBuild evaluation can do either. A single Stopwatch-tracked deadline (rather
        // than two independent full timeouts) bounds total detection time at ProcessTimeoutMilliseconds
        // instead of up to double that; the catch-all kills the child on a stream fault too, not only
        // on timeout, so a faulted read task cannot leak the process tree.
        Stopwatch stopwatch = Stopwatch.StartNew();
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        try
        {
            if (!Task.WaitAll([outputTask, errorTask], ProcessTimeoutMilliseconds))
            {
                throw new TimeoutException(
                    $"{fileName} {string.Join(' ', arguments)} did not exit within {ProcessTimeoutMilliseconds / 1000} seconds.");
            }

            int remainingMilliseconds = (int)Math.Max(0, ProcessTimeoutMilliseconds - stopwatch.ElapsedMilliseconds);
            if (!process.WaitForExit(remainingMilliseconds))
            {
                throw new TimeoutException(
                    $"{fileName} {string.Join(' ', arguments)} did not exit within {ProcessTimeoutMilliseconds / 1000} seconds.");
            }
        }
        catch
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        string output = outputTask.GetAwaiter().GetResult();
        string error = errorTask.GetAwaiter().GetResult();
        process.ExitCode.ShouldBe(0, error);
        return output;
    }

    private static Process CreateGitProcess(string root, params string[] arguments)
    {
        Process process = CreateProcess(root, "git");

        foreach (string argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        return process;
    }

    private static Process CreateProcess(string root, string fileName)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo(fileName)
            {
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };

        return process;
    }

    private sealed record EvaluatedProjectGraph(
        IReadOnlyDictionary<string, string> Properties,
        string[] PackageReferences,
        string[] ProjectReferences);
}
