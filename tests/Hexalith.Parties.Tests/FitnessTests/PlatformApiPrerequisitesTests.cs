using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using Hexalith.Parties.Contracts.Authorization;
using Hexalith.Parties.PackageTests;

using Shouldly;

using static Hexalith.Parties.Tests.FitnessTests.RepositoryTestProcess;

namespace Hexalith.Parties.Tests.FitnessTests;

public sealed class PlatformApiPrerequisitesTests
{
    private const string EventStorePayloadProtectionSpecRelativePath = "references/Hexalith.EventStore/_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md";
    private const string HistoricalEventStorePackageVersion = "3.113.0";
    private const string EventStoreRelativePath = "references/Hexalith.EventStore";
    private const string EventStoreSprintStatusRelativePath = "references/Hexalith.EventStore/_bmad-output/implementation-artifacts/sprint-status.yaml";
    private const string EventStoreStoryMigrationRelativePath = "references/Hexalith.EventStore/_bmad-output/planning-artifacts/story-id-migration-2026-08-01.md";
    private const string MatrixRelativePath = "_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md";

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
    private static readonly string[] RuntimeSourceSelectors =
    [
        "HexalithEventStoreFromSource", "HexalithCommonsFromSource", "HexalithCommonsHttpFromSource",
        "HexalithCommonsServiceDefaultsFromSource", "HexalithFrontComposerFromSource", "HexalithTenantsFromSource",
        "HexalithMemoriesFromSource", "HexalithMcpCliFromSource", "HexalithPolymorphicSerializationsFromSource",
    ];
    private static readonly string[] RuntimeDependencyModules =
    [
        "Hexalith.EventStore", "Hexalith.Commons", "Hexalith.FrontComposer", "Hexalith.Tenants",
        "Hexalith.Memories", "Hexalith.McpCli", "Hexalith.PolymorphicSerializations",
    ];
    private static readonly string[] RuntimeReferenceContextMetadata =
    [
        "AdditionalProperties", "Properties", "GlobalPropertiesToRemove", "SetConfiguration", "SetTargetFramework", "SetPlatform",
    ];

    // Committed source observations are separate from historical migration/parity approvals.
    private const string CurrentSelectedEventStoreSha = "be4aba873d09736abb4caacf1fae8ad870f0b3b6";
    private const string CurrentSelectedEventStoreDescribe = "v3.128.4-5-gbe4aba87";
    private const string CurrentSelectedEventStoreTag = "v3.128.4";
    // Current committed selections are observations, separate from historical owner approvals.
    private static readonly IReadOnlyDictionary<string, string> CurrentSelectedGitlinks = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["references/Hexalith.AI.Tools"] = "70dbf73e80787299c832d30d911b16b105ab194d",
        ["references/Hexalith.Builds"] = "1884a88ab80d3d0ae80c09a5ed9dfe23867d9fb9",
        ["references/Hexalith.Commons"] = "3edfbdafc7d4fa915286dce8a37dc4cc7c06085a",
        ["references/Hexalith.EventStore"] = "28ccd32c95ab521755d12deee5d7ad16927e7c92",
        ["references/Hexalith.FrontComposer"] = "e7e892b6ed584501f6dd18e11acd299948c0113f",
        ["references/Hexalith.McpCli"] = "c05335fc2ed90901bcaeef82a67263fb2d1c8703",
        ["references/Hexalith.Memories"] = "fc8b46f07f77b73c67cab6687feb07e0e600d585",
        ["references/Hexalith.Platform"] = "aa8c66edc782b1735539e60b2b949f7f4015e3e3",
        ["references/Hexalith.PolymorphicSerializations"] = "221ac4592ba908ae7661ba1fa9e2d1fcc23baee0",
        ["references/Hexalith.Tenants"] = "4fa78baa94f939a2b752bce52d0bac77214ddd83",
    };

    private static readonly IReadOnlyDictionary<string, string> CurrentSelectedSourceDescribes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["references/Hexalith.AI.Tools"] = "70dbf73e",
        ["references/Hexalith.Builds"] = "v4.38.3-39-g1884a88a",
        ["references/Hexalith.Commons"] = "v2.30.3-5-g3edfbdaf",
        ["references/Hexalith.EventStore"] = "v3.128.4-30-g28ccd32c",
        ["references/Hexalith.FrontComposer"] = "v4.7.1-32-ge7e892b6",
        ["references/Hexalith.McpCli"] = "v1.1.0-25-gc05335fc",
        ["references/Hexalith.Memories"] = "v3.0.10-10-gfc8b46f0",
        ["references/Hexalith.Platform"] = "v1.0.1-85-gaa8c66ed",
        ["references/Hexalith.PolymorphicSerializations"] = "v1.19.5",
        ["references/Hexalith.Tenants"] = "v5.11.1-16-g4fa78baa",
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
        "8-3-pdenc-v2-core-cryptographic-engine: done",
        "8-4-compatibility-readers-and-mixed-history-routing: done",
        "8-5-policy-and-key-lifecycle-mechanics: review",
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
        ["FrontComposer UI primitives"] = ["FcEntityPicker", "FcDestructiveConfirmationDialog", "FileDownload", "JsonDownload", "FcSurfaceStatus", "aria-live=\"@(_suppressSpeech ? \"off\" : \"polite\")\""],
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
            ["FrontComposer UI primitives"] =
            [
                ("aria-live=\"@(_suppressSpeech ? \"off\" : \"polite\")\"", "references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Components/Rendering/FcSurfaceStatus.razor"),
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
            $"Released package `{HistoricalEventStorePackageVersion}`",
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
            $"EventStore `{HistoricalEventStorePackageVersion}`",
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
                string fullPath = RepositoryDependencyPaths.Resolve(normalizedRoot, evidencePath);
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

    /// <summary>Qualifies non-consumed source selections independently of package-consumption rows.</summary>
    /// <param name="dependency">The root source selection being qualified.</param>
    /// <param name="scenario">The malformed receipt to reject, or the valid control.</param>
    [Theory]
    [InlineData("McpCli", "valid")]
    [InlineData("Platform", "valid")]
    [InlineData("McpCli", "missing")]
    [InlineData("Platform", "missing")]
    [InlineData("McpCli", "duplicate")]
    [InlineData("Platform", "duplicate")]
    [InlineData("McpCli", "duplicate-spaced")]
    [InlineData("Platform", "duplicate-spaced")]
    [InlineData("McpCli", "compact")]
    [InlineData("Platform", "compact")]
    [InlineData("McpCli", "wrong-sha")]
    [InlineData("Platform", "wrong-sha")]
    [InlineData("McpCli", "wrong-describe")]
    [InlineData("Platform", "wrong-describe")]
    [InlineData("McpCli", "describe-in-prose")]
    [InlineData("Platform", "describe-in-prose")]
    [InlineData("McpCli", "missing-column")]
    [InlineData("Platform", "missing-column")]
    public void NonConsumedSourceRowsRequireUniqueExactIdentityReceipts(string dependency, string scenario)
    {
        string path = $"references/Hexalith.{dependency}";
        string identity = CurrentSelectedGitlinks[path];
        string describe = CurrentSelectedSourceDescribes[path];
        string valid = $"| {dependency} | `{identity}` (`{describe}`) | Source selection only. |";
        string selection = scenario switch
        {
            "valid" => valid,
            "missing" => "| Unrelated | Other identity | Source selection only. |",
            "duplicate" => $"{valid}\n{valid}",
            "duplicate-spaced" => $"{valid}\n| {dependency}| `stale-identity` (`stale-describe`) | Source selection only. |",
            "compact" => $"|{dependency}|`{identity}` (`{describe}`)|Source selection only.|",
            "wrong-sha" => valid.Replace(identity, new string('a', 40), StringComparison.Ordinal),
            "wrong-describe" => valid.Replace($"`{describe}`", "`stale-describe`", StringComparison.Ordinal),
            "describe-in-prose" => $"| {dependency} | `{identity}` (`stale-describe`) | Historical `{describe}`. |",
            "missing-column" => $"| {dependency} | `{identity}` (`{describe}`) |",
            _ => throw new ArgumentException("Unknown source-selection fixture.", nameof(scenario)),
        };

        if (scenario is "valid" or "compact")
        {
            AssertCurrentSourceRow(selection, path, identity, describe);
        }
        else
        {
            Should.Throw<ShouldAssertException>(() => AssertCurrentSourceRow(selection, path, identity, describe));
        }
    }

    /// <summary>Runs real source-selection proof without requiring consumed-package qualification.</summary>
    /// <param name="dependency">The non-consumed root dependency to inspect.</param>
    [Theory]
    [InlineData("McpCli")]
    [InlineData("Platform")]
    public void CommittedSourceSelections_RecordOverridesAndRefuseInvalidImmutableConsumption(string dependency)
    {
        string root = RepositoryRoot.Locate();
        string path = $"references/Hexalith.{dependency}";
        string identity = CurrentSelectedGitlinks[path];
        string describe = CurrentSelectedSourceDescribes[path];

        AssertCurrentSourceRow(ReadCurrentSourceSelection(ReadMatrix()), path, identity, describe);
        AssertSelectedSourceObservation(root, path, identity);
        DescribeObservedSource(root, path, describe, identity).ShouldBe(describe, path);
    }

    [Fact]
    public void CommittedSelectionObservations_PreservePackageGraphAndImmutableConsumptionRefusals()
    {
        string root = RepositoryRoot.Locate();
        string matrix = ReadMatrix();

        string currentEventStoreVersion = EffectivePackageVersions.ReadProperty(root, "HexalithEventStoreVersion");
        IReadOnlyDictionary<string, string> effectivePackages = EffectivePackageVersions.ReadPackages(root);
        string currentSelection = ReadCurrentSourceSelection(matrix);
        currentSelection.ShouldContain($"EventStore package `{currentEventStoreVersion}`");
        currentSelection.ShouldContain("Selection does not revalidate historical parity receipts");
        foreach ((string path, string identity) in CurrentSelectedGitlinks)
        {
            string describe = CurrentSelectedSourceDescribes[path];
            AssertCurrentSourceRow(currentSelection, path, identity, describe);
            AssertSelectedSourceObservation(root, path, identity);
            DescribeObservedSource(root, path, describe, identity).ShouldBe(describe, path);
        }

        RunGit(root, "-C", EventStoreRelativePath, "describe", "--tags", "--match", CurrentSelectedEventStoreTag, "--abbrev=8", CurrentSelectedEventStoreSha)
            .Trim()
            .ShouldBe(CurrentSelectedEventStoreDescribe);

        // Keep historical shell/a11y receipts bound to the identity at which they ran.
        matrix.ShouldContain(FrontComposerSha);
        matrix.ShouldContain("4.5.0");

        // Any present-tense HEAD observation must match current committed selection. Historical
        // receipts remain attached to their dated Parties revision and original evidence.
        foreach (Match receipt in Regex.Matches(
                     matrix,
                     @"git ls-tree HEAD (?<path>references/[A-Za-z.]+)`\s*->\s*`160000 commit (?<sha>[0-9a-f]{40})",
                     RegexOptions.CultureInvariant))
        {
            if (CurrentSelectedGitlinks.TryGetValue(receipt.Groups["path"].Value, out string? expectedSha))
            {
                receipt.Groups["sha"].Value.ShouldBe(
                    expectedSha,
                    $"Stale present-tense gitlink receipt for {receipt.Groups["path"].Value}.");
            }
        }

        string rootBuildProps = File.ReadAllText(Path.Combine(root, "Directory.Build.props"));
        rootBuildProps.ShouldNotContain("<UseHexalithProjectReferences");
        rootBuildProps.ShouldNotContain("<HexalithEventStoreFromSource");
        rootBuildProps.ShouldNotContain("<HexalithCommonsHttpFromSource");

        string rootPackages = File.ReadAllText(Path.Combine(root, "Directory.Packages.props"));
        rootPackages.ShouldContain("$(MSBuildThisFileDirectory)references/Hexalith.Builds/Props/Directory.Packages.props");
        string rootBuildTargets = string.Join(
            '\n',
            File.ReadAllText(Path.Combine(root, "Directory.Build.props")),
            File.ReadAllText(Path.Combine(root, "Directory.Build.targets")));
        rootBuildTargets.ShouldNotContain("Hexalith.Build.props");
        rootBuildTargets.ShouldNotContain("Hexalith.Package.props");
        string catalog = RunGit(root, "-C", "references/Hexalith.Builds", "show",
            CurrentSelectedGitlinks["references/Hexalith.Builds"] + ":Props/Directory.Packages.props");
        effectivePackages["Hexalith.EventStore.Contracts"].ShouldBe(currentEventStoreVersion);
        catalog.ShouldContain("<PackageVersion Include=\"Hexalith.EventStore.Contracts\" Version=\"$(HexalithEventStoreVersion)\" Condition=\"'$(UseHexalithProjectReferences)' != 'true' or !$(MSBuildProjectName.StartsWith('Hexalith.Works'))\" />");
        catalog.ShouldContain("<HexalithCommonsVersion Condition=\"'$(HexalithCommonsVersion)' == ''\">2.30.3</HexalithCommonsVersion>");
        catalog.ShouldContain("<HexalithFrontComposerVersion Condition=\"'$(HexalithFrontComposerVersion)' == ''\">4.7.1</HexalithFrontComposerVersion>");
        catalog.ShouldContain("<HexalithMemoriesVersion Condition=\"'$(HexalithMemoriesVersion)' == ''\">3.0.10</HexalithMemoriesVersion>");
        catalog.ShouldContain("<HexalithTenantsVersion Condition=\"'$(HexalithTenantsVersion)' == ''\">5.11.1</HexalithTenantsVersion>");
        catalog.ShouldContain("<HexalithPartiesVersion Condition=\"'$(HexalithPartiesVersion)' == ''\">2.0.0</HexalithPartiesVersion>");
        // The selected Builds catalog routes this version through a property whose unconditional value applies to
        // every Parties project (only Hexalith.Folders.Aspire overrides it), so pin both halves exactly.
        catalog.ShouldContain("<HexalithAspireHostingDaprVersion>13.6.2-beta.959</HexalithAspireHostingDaprVersion>");
        catalog.ShouldContain("<PackageVersion Include=\"CommunityToolkit.Aspire.Hosting.Dapr\" Version=\"$(HexalithAspireHostingDaprVersion)\" />");
        catalog.ShouldContain("<PackageVersion Include=\"xunit.v3\" Version=\"4.0.2\" />");
        catalog.ShouldContain("<PackageVersion Include=\"xunit.v3.assert\" Version=\"4.0.2\" />");
        catalog.ShouldContain("<PackageVersion Include=\"xunit.v3.extensibility.core\" Version=\"4.0.2\" />");
        catalog.ShouldContain("<PackageVersion Include=\"xunit.runner.visualstudio\" Version=\"4.0.1\" />");
        catalog.ShouldContain("<PackageVersion Include=\"bunit\" Version=\"2.13.3\" />");

        matrix.ShouldContain("Package (default Release graph)");
        matrix.ShouldContain("Source (explicit project-reference graph)");
        matrix.ShouldContain("The catalog's `2.30.1` package is a fallback, not current consumption proof.");

        string[] projectFiles =
        [
            .. Directory.GetFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories).Where(path => IsNotBuildOutput(root, path)),
            .. Directory.GetFiles(Path.Combine(root, "samples"), "*.csproj", SearchOption.AllDirectories).Where(path => IsNotBuildOutput(root, path)),
            .. Directory.GetFiles(Path.Combine(root, "tests"), "*.csproj", SearchOption.AllDirectories).Where(path => IsNotBuildOutput(root, path)),
        ];
        foreach ((string dependency, string version, string property, string relativeRoot, string selector) in new[]
                 {
                     ("Hexalith.EventStore", currentEventStoreVersion, "HexalithEventStoreVersion", EventStoreRelativePath, "HexalithEventStoreFromSource"),
                     ("Hexalith.Commons.Http", effectivePackages["Hexalith.Commons.Http"], "HexalithCommonsVersion", "references/Hexalith.Commons", "HexalithCommonsHttpFromSource"),
                     ("Hexalith.Memories", effectivePackages["Hexalith.Memories.Client.Rest"], "HexalithMemoriesVersion", "references/Hexalith.Memories", "HexalithMemoriesFromSource"),
                 })
        {
            string[] consumers = projectFiles
                .Where(path => DeclaredDependencyReferences(path, dependency, null).Length > 0)
                .Order(StringComparer.Ordinal)
                .ToArray();
            consumers.ShouldNotBeEmpty(dependency);
            foreach (string projectFile in consumers)
            {
                // No mode override means ordinary Release defaults; true alone is bare Debug.
                foreach (bool? mode in new bool?[] { null, false, true })
                {
                    EvaluatedProjectGraph packageGraph = EvaluateProjectGraph(root, projectFile, useSource: mode);
                    packageGraph.Properties[property].ShouldBe(version, projectFile);
                    packageGraph.Properties[selector].ShouldNotBe("true", projectFile);
                    AssertPackageConsumer(packageGraph, projectFile, dependency, version);
                }

                string rootProperty = dependency == "Hexalith.Commons.Http" ? "HexalithCommonsRoot"
                    : dependency == "Hexalith.EventStore" ? "HexalithEventStoreRoot" : "HexalithMemoriesRoot";
                EvaluatedProjectGraph sourceGraph = EvaluateProjectGraph(
                    root,
                    projectFile,
                    useSource: true,
                    $"-p:{rootProperty}={RepositoryDependencyPaths.Resolve(root, relativeRoot)}",
                    $"-p:{selector}=true");
                sourceGraph.Properties[selector].ShouldBe("true", projectFile);
                AssertSourceConsumer(sourceGraph, projectFile, dependency, version, RepositoryDependencyPaths.Resolve(root, relativeRoot));
            }
        }
    }

    /// <summary>Verifies the runtime projects that Aspire launches by path instead of solution references.</summary>
    [Fact]
    public void ExternalRuntimeInventoryMatchesAppHostDeclarations()
    {
        string root = RepositoryRoot.Locate();
        string[] actual = ReadExternalRuntimeProjects(root);
        string[] expected =
        [
            "references/Hexalith.EventStore/src/Hexalith.EventStore/Hexalith.EventStore.csproj",
            "references/Hexalith.EventStore/src/Hexalith.EventStore.Admin.Server.Host/Hexalith.EventStore.Admin.Server.Host.csproj",
            "references/Hexalith.EventStore/src/Hexalith.EventStore.Admin.UI/Hexalith.EventStore.Admin.UI.csproj",
            "references/Hexalith.Tenants/src/Hexalith.Tenants/Hexalith.Tenants.csproj",
            "references/Hexalith.Memories/src/Hexalith.Memories.Server/Hexalith.Memories.Server.csproj",
            "references/Hexalith.Memories/src/Hexalith.Memories.DomainService/Hexalith.Memories.DomainService.csproj",
            "references/Hexalith.EventStore/samples/Hexalith.EventStore.Sample/Hexalith.EventStore.Sample.csproj",
            "references/Hexalith.EventStore/samples/Hexalith.EventStore.Sample.BlazorUI/Hexalith.EventStore.Sample.BlazorUI.csproj",
        ];
        actual.Order(StringComparer.Ordinal).ShouldBe(expected.Order(StringComparer.Ordinal));
        string preparation = File.ReadAllText(Path.Combine(root, "scripts", "prepare-aspire-runtime-hosts.sh"));
        foreach (string project in actual) { preparation.ShouldContain(project); }
    }

    /// <summary>Checks every selected external runtime dependency before starting a topology.</summary>
    [Theory]
    [InlineData("Debug")]
    [InlineData("Release")]
    public void ExternalRuntimeGraphsMatchTheIndependentlyEvaluatedPartiesPackageSelection(string configuration)
    {
        string root = RepositoryRoot.Locate();
        string expectedVersion = EffectivePackageVersions.ReadProperty(root, "HexalithEventStoreVersion", configuration: configuration);
        IReadOnlyDictionary<string, EvaluatedProjectGraph> graphs = ReadExternalRuntimeGraphs(root, ReadExternalRuntimeProjects(root), expectedVersion, configuration);

        graphs.Count.ShouldBeGreaterThan(7, "The runtime preflight must inspect transitive projects as well as the eight roots.");
        graphs.Keys.ShouldContain(path => path.EndsWith("Hexalith.Tenants.csproj", StringComparison.Ordinal));
        graphs.Keys.ShouldContain(path => path.EndsWith("Hexalith.Memories.Server.csproj", StringComparison.Ordinal));
        graphs.Values.SelectMany(static graph => graph.PackageReferences)
            .ShouldContain("Hexalith.EventStore.DomainService");
    }

    /// <summary>Rejects package skew, hidden source selection, and missing transitive runtime projects.</summary>
    [Theory]
    [InlineData("version")]
    [InlineData("override")]
    [InlineData("source")]
    [InlineData("source-uppercase")]
    [InlineData("project")]
    [InlineData("missing")]
    public void ExternalRuntimePreflightRejectsIncompatibleSelectedDependencies(string failure)
    {
        string root = Path.Combine(Path.GetTempPath(), $"parties-runtime-graph-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            new XDocument(new XElement("Project", new XElement("ItemGroup",
                new XElement("ProjectReference", new XAttribute("Include", "RuntimeConsumer.csproj")))))
                .Save(Path.Combine(root, "Runtime.csproj"));
            XElement package = new("PackageReference", new XAttribute("Include", "Hexalith.EventStore.DomainService"));
            if (failure is "version" or "override")
            {
                package.Add(new XAttribute(failure == "override" ? "VersionOverride" : "Version", "8.0.0"));
            }

            XElement items = new("ItemGroup", package,
                new XElement("PackageVersion", new XAttribute("Include", "Hexalith.EventStore.DomainService"), new XAttribute("Version", "9.0.0")));
            if (failure == "missing")
            {
                items.Add(new XElement("ProjectReference", new XAttribute("Include", "Missing.csproj")));
            }
            else if (failure == "project")
            {
                File.WriteAllText(Path.Combine(root, "Hexalith.EventStore.Contracts.csproj"), "<Project />");
                items.Add(new XElement("ProjectReference", new XAttribute("Include", "Hexalith.EventStore.Contracts.csproj")));
            }

            new XDocument(new XElement("Project", new XElement("PropertyGroup",
                new XElement("HexalithEventStoreVersion", "9.0.0"),
                new XElement("HexalithEventStoreFromSource", failure == "source" ? "true" : failure == "source-uppercase" ? "True" : string.Empty)), items))
                .Save(Path.Combine(root, "RuntimeConsumer.csproj"));

            Should.Throw<ShouldAssertException>(() => ReadExternalRuntimeGraphs(root, ["Runtime.csproj"], "9.0.0"))
                .Message.ShouldContain(failure == "missing" ? "Missing.csproj" : "RuntimeConsumer.csproj");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Unsupported edge contexts cannot hide the child's effective version selection.</summary>
    [Theory]
    [InlineData("AdditionalProperties", "DependencyVersion=8.0.0", "")]
    [InlineData("Properties", "DependencyVersion=8.0.0", "")]
    [InlineData("GlobalPropertiesToRemove", "DependencyVersion", "")]
    [InlineData("GlobalPropertiesToRemove", "WebPublishProfileFile", "selected-profile")]
    [InlineData("SetConfiguration", "Configuration=Debug", "")]
    [InlineData("SetTargetFramework", "TargetFramework=net9.0", "")]
    [InlineData("SetPlatform", "Platform=x86", "")]
    public void ExternalRuntimePreflightRejectsUnevaluatedReferenceContexts(string metadata, string value, string publishProfile)
    {
        string root = Path.Combine(Path.GetTempPath(), $"parties-runtime-context-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            new XDocument(new XElement("Project", new XElement("PropertyGroup",
                new XElement("DependencyVersion", "9.0.0"), new XElement("WebPublishProfileFile", publishProfile)),
                new XElement("ItemGroup", new XElement("ProjectReference", new XAttribute("Include", "Consumer.csproj"),
                    new XAttribute(metadata, value)))))
                .Save(Path.Combine(root, "Runtime.csproj"));
            File.WriteAllText(Path.Combine(root, "Consumer.csproj"), """
                <Project><PropertyGroup><DependencyVersion Condition="'$(DependencyVersion)' == ''">9.0.0</DependencyVersion></PropertyGroup>
                <ItemGroup><PackageVersion Include="Hexalith.EventStore.Contracts" Version="$(DependencyVersion)" />
                <PackageReference Include="Hexalith.EventStore.Contracts" /></ItemGroup></Project>
                """);

            Should.Throw<ShouldAssertException>(() => ReadExternalRuntimeGraphs(root, ["Runtime.csproj"], "9.0.0"))
                .Message.ShouldContain($"unsupported ProjectReference context metadata {metadata}");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>The package and runtime readers must both honor the startup/build configuration.</summary>
    [Theory]
    [InlineData("Debug", "8.0.0")]
    [InlineData("Release", "9.0.0")]
    public void ExternalRuntimePreflightEvaluatesConfigurationSpecificSelections(string configuration, string expected)
    {
        string root = Path.Combine(Path.GetTempPath(), $"parties-runtime-configuration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            const string versionProperties = """
                <HexalithEventStoreVersion Condition="'$(Configuration)' == 'Debug'">8.0.0</HexalithEventStoreVersion>
                <HexalithEventStoreVersion Condition="'$(Configuration)' == 'Release'">9.0.0</HexalithEventStoreVersion>
                """;
            File.WriteAllText(Path.Combine(root, "Expected.csproj"), $"<Project><PropertyGroup>{versionProperties}</PropertyGroup></Project>");
            XElement package = new("PackageReference", new XAttribute("Include", "Hexalith.EventStore.Contracts"));
            XDocument runtime = XDocument.Parse($"<Project><PropertyGroup>{versionProperties}</PropertyGroup><ItemGroup><PackageVersion Include=\"Hexalith.EventStore.Contracts\" Version=\"$(HexalithEventStoreVersion)\" /></ItemGroup></Project>");
            runtime.Root!.Element("ItemGroup")!.Add(package);
            string project = Path.Combine(root, "Runtime.csproj");
            runtime.Save(project);
            string independentlyEvaluatedVersion = EffectivePackageVersions.ReadProperty(root, "HexalithEventStoreVersion", "Expected.csproj", configuration);
            independentlyEvaluatedVersion.ShouldBe(expected);
            ReadExternalRuntimeGraphs(root, [project], independentlyEvaluatedVersion, configuration).Count.ShouldBe(1);

            package.Add(new XAttribute("VersionOverride", "7.0.0"));
            package.Add(new XAttribute("Condition", $"'$(Configuration)' == '{configuration}'"));
            runtime.Save(project);
            Should.Throw<ShouldAssertException>(() => ReadExternalRuntimeGraphs(root, [project], independentlyEvaluatedVersion, configuration))
                .Message.ShouldContain("independently evaluated Parties EventStore package selection");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>An outer multitarget evaluation cannot certify packages selected only by inner builds.</summary>
    [Fact]
    public void ExternalRuntimePreflightRejectsUnexpandedTargetFrameworks()
    {
        string root = Path.Combine(Path.GetTempPath(), $"parties-runtime-frameworks-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "Runtime.csproj"), """
                <Project><PropertyGroup><TargetFrameworks>net9.0;net10.0</TargetFrameworks></PropertyGroup>
                <ItemGroup Condition="'$(TargetFramework)' == 'net9.0'">
                <PackageReference Include="Hexalith.EventStore.Contracts" Version="8.0.0" /></ItemGroup></Project>
                """);
            Should.Throw<ShouldAssertException>(() => ReadExternalRuntimeGraphs(root, ["Runtime.csproj"], "9.0.0"))
                .Message.ShouldContain("unexpanded TargetFrameworks");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Every known external source selector participates in the runtime package boundary.</summary>
    [Theory]
    [InlineData("HexalithCommonsFromSource")]
    [InlineData("HexalithCommonsHttpFromSource")]
    [InlineData("HexalithCommonsServiceDefaultsFromSource")]
    [InlineData("HexalithFrontComposerFromSource")]
    [InlineData("HexalithTenantsFromSource")]
    [InlineData("HexalithMemoriesFromSource")]
    [InlineData("HexalithMcpCliFromSource")]
    [InlineData("HexalithPolymorphicSerializationsFromSource")]
    public void ExternalRuntimePreflightRejectsOtherSourceSelectors(string selector)
    {
        string root = Path.Combine(Path.GetTempPath(), $"parties-runtime-selector-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            new XDocument(new XElement("Project", new XElement("PropertyGroup", new XElement(selector, "True"))))
                .Save(Path.Combine(root, "Runtime.csproj"));
            Should.Throw<ShouldAssertException>(() => ReadExternalRuntimeGraphs(root, ["Runtime.csproj"], "9.0.0"))
                .Message.ShouldContain(selector);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Missing selector metadata cannot conceal cross-module source references.</summary>
    [Theory]
    [InlineData("Hexalith.Commons.Http")]
    [InlineData("Hexalith.FrontComposer.Contracts")]
    [InlineData("Hexalith.Tenants.Contracts")]
    [InlineData("Hexalith.Memories.Contracts")]
    [InlineData("Hexalith.McpCli.Abstractions")]
    [InlineData("Hexalith.PolymorphicSerializations")]
    public void ExternalRuntimePreflightRejectsOtherHiddenSourceProjects(string dependency)
    {
        string root = Path.Combine(Path.GetTempPath(), $"parties-runtime-hidden-source-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, dependency + ".csproj"), "<Project />");
            new XDocument(new XElement("Project", new XElement("ItemGroup", new XElement("ProjectReference",
                new XAttribute("Include", dependency + ".csproj")))))
                .Save(Path.Combine(root, "Runtime.csproj"));
            Should.Throw<ShouldAssertException>(() => ReadExternalRuntimeGraphs(root, ["Runtime.csproj"], "9.0.0"))
                .Message.ShouldContain("source projects");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Shared dependencies and cycles are evaluated once without hiding their packages.</summary>
    [Fact]
    public void ExternalRuntimePreflightDeduplicatesSelectedReferencesAndCycles()
    {
        string root = Path.Combine(Path.GetTempPath(), $"parties-runtime-cycle-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            foreach ((string project, string referencedProject) in new[] { ("Runtime", "Consumer"), ("Consumer", "Runtime") })
            {
                new XDocument(new XElement("Project", new XElement("ItemGroup",
                    new XElement("ProjectReference", new XAttribute("Include", referencedProject + ".csproj")),
                    new XElement("PackageVersion", new XAttribute("Include", "Hexalith.EventStore.Contracts"), new XAttribute("Version", "9.0.0")),
                    new XElement("PackageReference", new XAttribute("Include", "Hexalith.EventStore.Contracts")))))
                    .Save(Path.Combine(root, project + ".csproj"));
            }

            ReadExternalRuntimeGraphs(root, ["Runtime.csproj", "Runtime.csproj"], "9.0.0")
                .Count.ShouldBe(2);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Unrecognized runtime project expressions cannot silently escape the inventory.</summary>
    [Fact]
    public void ExternalRuntimeInventoryRejectsUnsupportedAddProjectArguments()
    {
        string root = RepositoryRoot.Locate();
        string appHost = File.ReadAllText(Path.Combine(root, "src/Hexalith.Parties.AppHost/Program.cs"));
        Should.Throw<ShouldAssertException>(() => ReadExternalRuntimeProjects(root, appHost + "\nbuilder.AddProject(\"extra\", \"literal/path.csproj\");"))
            .Message.ShouldContain("unsupported AddProject");
    }

    /// <summary>Variable path segments cannot be ignored while harvesting surrounding string literals.</summary>
    [Fact]
    public void ExternalRuntimeInventoryRejectsNonliteralPathCombineSegments()
    {
        string root = RepositoryRoot.Locate();
        string appHost = File.ReadAllText(Path.Combine(root, "src/Hexalith.Parties.AppHost/Program.cs"));
        appHost = appHost.Replace("Path.Combine(\"src\", \"Hexalith.EventStore\",", "Path.Combine(\"src\", extraSegment, \"Hexalith.EventStore\",", StringComparison.Ordinal);
        Should.Throw<ShouldAssertException>(() => ReadExternalRuntimeProjects(root, appHost))
            .Message.ShouldContain("nonliteral Path.Combine segments");
    }

    /// <summary>Runtime declarations cannot introduce dependencies outside the root submodule manifest.</summary>
    [Fact]
    public void ExternalRuntimeInventoryRejectsUndeclaredRootSubmodules()
    {
        string root = Path.Combine(Path.GetTempPath(), $"parties-runtime-inventory-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, ".gitmodules"), string.Empty);
            const string appHost = """
                string runtimePath = ReferenceProjectResolver.ResolveRequired(repositoryRoot,
                    "Hexalith.Unlisted", Path.Combine("src", "Runtime.csproj"));
                builder.AddProject("runtime", runtimePath);
                """;
            Should.Throw<ShouldAssertException>(() => ReadExternalRuntimeProjects(root, appHost))
                .Message.ShouldContain("declared root submodule");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string[] ReadExternalRuntimeProjects(string root, string? appHost = null)
    {
        appHost ??= File.ReadAllText(Path.Combine(root, "src/Hexalith.Parties.AppHost/Program.cs"));
        HashSet<string> rootSubmodules = Regex.Matches(File.ReadAllText(Path.Combine(root, ".gitmodules")), @"(?m)^\s*path\s*=\s*(?<path>[^\r\n]+)")
            .Select(static match => match.Groups["path"].Value.Trim()).ToHashSet(StringComparer.Ordinal);
        Dictionary<string, string> declarations = new(StringComparer.Ordinal);
        foreach (Match declaration in Regex.Matches(appHost,
                     "(?<variable>[A-Za-z][A-Za-z0-9]*)\\s*=\\s*(?:ReferenceProjectResolver\\.ResolveRequired|ResolveOptionalReferenceProjectPath)\\s*\\(\\s*(?:repositoryRoot,\\s*)?\"(?<module>Hexalith\\.[A-Za-z.]+)\"\\s*,\\s*Path\\.Combine\\((?<segments>[^)]+)\\)",
                     RegexOptions.CultureInvariant))
        {
            string moduleRoot = "references/" + declaration.Groups["module"].Value;
            rootSubmodules.ShouldContain(moduleRoot, "Runtime projects must belong to a declared root submodule.");
            Regex.IsMatch(declaration.Groups["segments"].Value, "\\A\\s*\"[^\"]+\"\\s*(?:,\\s*\"[^\"]+\"\\s*)*\\z", RegexOptions.CultureInvariant)
                .ShouldBeTrue($"{declaration.Groups["variable"].Value}: nonliteral Path.Combine segments are unsupported by the external runtime inventory");
            string[] segments = Regex.Matches(declaration.Groups["segments"].Value, "\"(?<segment>[^\"]+)\"")
                .Select(static match => match.Groups["segment"].Value).ToArray();
            string project = moduleRoot + "/" + string.Join('/', segments);
            File.Exists(RepositoryDependencyPaths.Resolve(root, project)).ShouldBeTrue($"AppHost external runtime project is missing: {project}");
            declarations.Add(declaration.Groups["variable"].Value, project);
        }

        MatchCollection runtimeCalls = Regex.Matches(appHost, @"\bbuilder\s*\.\s*AddProject\s*\(", RegexOptions.CultureInvariant);
        MatchCollection recognizedCalls = Regex.Matches(appHost, "builder\\s*\\.\\s*AddProject\\s*\\(\\s*\"[^\"]+\"\\s*,\\s*(?<variable>[A-Za-z][A-Za-z0-9]*)\\s*\\)", RegexOptions.CultureInvariant);
        recognizedCalls.Count.ShouldBe(runtimeCalls.Count, "AppHost contains unsupported AddProject arguments; update the external runtime inventory parser.");
        return recognizedCalls
            .Select(match => declarations.TryGetValue(match.Groups["variable"].Value, out string? project)
                ? project : throw new InvalidOperationException($"No external runtime project declaration for {match.Groups["variable"].Value}."))
            .Distinct(StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyDictionary<string, EvaluatedProjectGraph> ReadExternalRuntimeGraphs(
        string root, IEnumerable<string> runtimeProjects, string expectedEventStoreVersion, string configuration = "Release")
    {
        Dictionary<string, EvaluatedProjectGraph> graphs = new(StringComparer.Ordinal);
        Queue<string> pending = new(runtimeProjects.Select(path => RepositoryDependencyPaths.Resolve(root, path)));
        while (pending.TryDequeue(out string? project))
        {
            if (graphs.ContainsKey(project))
            {
                continue;
            }

            File.Exists(project).ShouldBeTrue($"External runtime project does not exist: {project}");
            EvaluatedProjectGraph graph = EvaluateProjectGraphForConfiguration(root, project, configuration, useSource: null);
            string.IsNullOrWhiteSpace(graph.Properties["TargetFrameworks"])
                .ShouldBeTrue($"{project}: unexpanded TargetFrameworks are unsupported by the external runtime preflight; evaluate an explicit inner framework context first");
            foreach (string selector in RuntimeSourceSelectors)
            {
                string.Equals(graph.Properties[selector], "true", StringComparison.OrdinalIgnoreCase)
                    .ShouldBeFalse($"{project}: unexpected {selector} source selection in the runtime package graph ({configuration})");
            }
            foreach (IReadOnlyDictionary<string, string> reference in graph.ProjectMetadata)
            {
                foreach (string metadata in RuntimeReferenceContextMetadata)
                {
                    string value = reference.GetValueOrDefault(metadata, string.Empty);
                    // The Web SDK removes this unset publishing-only property on every web edge.
                    bool removesUnsetPublishProfile = metadata == "GlobalPropertiesToRemove"
                        && value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .All(static name => name == "WebPublishProfileFile")
                        && string.IsNullOrEmpty(graph.Properties["WebPublishProfileFile"]);
                    (string.IsNullOrWhiteSpace(value) || removesUnsetPublishProfile).ShouldBeTrue(
                        $"{project}: {reference["FullPath"]} has unsupported ProjectReference context metadata {metadata}; refusing to evaluate it under the parent's context");
                }
            }
            foreach (string package in graph.PackageReferences.Where(static identity => IsDependencyIdentity(identity, "Hexalith.EventStore")))
            {
                graph.PackageMetadata[package]["EffectiveVersion"].ShouldBe(expectedEventStoreVersion,
                    $"{project}: {package} must match the independently evaluated Parties EventStore package selection");
            }

            foreach (string module in RuntimeDependencyModules)
            {
                string moduleRoot = RepositoryDependencyPaths.Resolve(root, "references/" + module) + Path.DirectorySeparatorChar;
                if (!project.StartsWith(moduleRoot, StringComparison.Ordinal))
                {
                    graph.ProjectReferences.Any(reference => IsDependencyIdentity(Path.GetFileNameWithoutExtension(reference), module))
                        .ShouldBeFalse($"{project}: external package consumers must not select {module} source projects");
                }
            }

            graphs.Add(project, graph);
            foreach (string reference in graph.ProjectReferences)
            {
                pending.Enqueue(Path.GetFullPath(reference));
            }
        }

        return graphs;
    }

    private static bool IsDependencyIdentity(string identity, string dependency)
        => string.Equals(identity, dependency, StringComparison.Ordinal)
            || identity.StartsWith(dependency + ".", StringComparison.Ordinal);

    private static string[] DeclaredDependencyReferences(string projectFile, string dependency, string? itemType)
        => XDocument.Load(projectFile).Descendants()
            .Where(item => (item.Name.LocalName is "PackageReference" or "ProjectReference")
                && (itemType is null || string.Equals(item.Name.LocalName, itemType, StringComparison.Ordinal)))
            .Select(item => item.Attribute("Include")?.Value ?? item.Attribute("Update")?.Value ?? string.Empty)
            .Select(static identity => identity.EndsWith(".csproj", StringComparison.Ordinal)
                ? Path.GetFileNameWithoutExtension(identity.Replace('\\', '/'))
                : identity)
            .Where(identity => IsDependencyIdentity(identity, dependency))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static void AssertPackageConsumer(
        EvaluatedProjectGraph graph,
        string projectFile,
        string dependency,
        string expectedVersion)
    {
        string[] expectedPackages = DeclaredDependencyReferences(projectFile, dependency, "PackageReference");
        expectedPackages.ShouldNotBeEmpty(projectFile);
        graph.PackageReferences.Where(identity => IsDependencyIdentity(identity, dependency))
            .Order(StringComparer.Ordinal).ShouldBe(expectedPackages, Case.Sensitive, projectFile);
        foreach (string identity in expectedPackages)
        {
            graph.PackageMetadata[identity]["EffectiveVersion"].ShouldBe(expectedVersion,
                $"{projectFile}: {identity} effective version (VersionOverride, Version, then central PackageVersion)");
        }

        graph.ProjectReferences.Any(path => IsDependencyIdentity(Path.GetFileNameWithoutExtension(path), dependency))
            .ShouldBeFalse($"{projectFile} package graph");
    }

    private static void AssertSourceConsumer(
        EvaluatedProjectGraph graph,
        string projectFile,
        string dependency,
        string expectedVersion,
        string expectedRoot)
    {
        string[] expectedProjects = DeclaredDependencyReferences(projectFile, dependency, "ProjectReference");
        if (expectedProjects.Length == 0)
        {
            // The package-only Gateway test host deliberately has no source branch.
            AssertPackageConsumer(graph, projectFile, dependency, expectedVersion);
            return;
        }

        string[] selectedProjects = graph.ProjectReferences
            .Where(path => IsDependencyIdentity(Path.GetFileNameWithoutExtension(path), dependency))
            .ToArray();
        selectedProjects.Select(static path => Path.GetFileNameWithoutExtension(path))
            .Order(StringComparer.Ordinal).ShouldBe(expectedProjects, Case.Sensitive, projectFile);
        selectedProjects.ShouldAllBe(path => path.StartsWith(
            Path.GetFullPath(expectedRoot).Replace('\\', '/') + "/", StringComparison.Ordinal),
            $"{projectFile}: selected projects must use the supplied consumer root.");
        graph.PackageReferences.Any(identity => IsDependencyIdentity(identity, dependency))
            .ShouldBeFalse($"{projectFile} source graph");
    }

    [Theory]
    [InlineData("Hexalith.McpCli")]
    [InlineData("references/Hexalith.McpCli")]
    public void McpCliSiblingLayoutsDoNotSelectSourceWithoutToolMapping(string relativeMcpCliRoot)
    {
        string root = RepositoryRoot.Locate();
        string temporaryRoot = Path.Combine(Path.GetTempPath(), $"parties-mcpcli-layout-{Guid.NewGuid():N}");
        string partiesRoot = Path.Combine(temporaryRoot, "Hexalith.Parties");
        string contractsRelativePath = "src/Hexalith.Parties.Contracts/Hexalith.Parties.Contracts.csproj";
        string contractsProject = Path.Combine(partiesRoot, contractsRelativePath);
        string abstractionsProject = Path.Combine(temporaryRoot, relativeMcpCliRoot,
            "src/Hexalith.McpCli.Abstractions/Hexalith.McpCli.Abstractions.csproj");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(contractsProject)!);
            Directory.CreateDirectory(Path.GetDirectoryName(abstractionsProject)!);
            File.Copy(Path.Combine(root, "Directory.Build.props"), Path.Combine(partiesRoot, "Directory.Build.props"));
            File.Copy(Path.Combine(root, contractsRelativePath), contractsProject);
            File.WriteAllText(abstractionsProject, "<Project />");
            Directory.Exists(Path.Combine(partiesRoot, "references/Hexalith.McpCli")).ShouldBeFalse();

            EvaluatedProjectGraph graph = EvaluateProjectGraph(partiesRoot, contractsProject, useSource: true);

            graph.Properties["HexalithMcpCliFromSource"].ShouldNotBe("true");
            graph.ProjectReferences.ShouldNotContain(Path.GetFullPath(abstractionsProject).Replace('\\', '/'));
            graph.PackageReferences.ShouldContain("Hexalith.McpCli.Abstractions");
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("Hexalith.McpCli")]
    [InlineData("references/Hexalith.McpCli")]
    public void ExplicitlyMappedMcpCliSiblingLayoutsSelectOnlyAbstractionsProject(string relativeMcpCliRoot)
    {
        string root = RepositoryRoot.Locate();
        string temporaryRoot = Path.Combine(Path.GetTempPath(), $"parties-mcpcli-mapped-layout-{Guid.NewGuid():N}");
        string partiesRoot = Path.Combine(temporaryRoot, "Hexalith.Parties");
        string contractsRelativePath = "src/Hexalith.Parties.Contracts/Hexalith.Parties.Contracts.csproj";
        string contractsProject = Path.Combine(partiesRoot, contractsRelativePath);
        string mcpCliRoot = Path.Combine(temporaryRoot, relativeMcpCliRoot);
        string abstractionsProject = Path.Combine(mcpCliRoot,
            "src/Hexalith.McpCli.Abstractions/Hexalith.McpCli.Abstractions.csproj");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(contractsProject)!);
            Directory.CreateDirectory(Path.GetDirectoryName(abstractionsProject)!);
            File.Copy(Path.Combine(root, "Directory.Build.props"), Path.Combine(partiesRoot, "Directory.Build.props"));
            File.Copy(Path.Combine(root, contractsRelativePath), contractsProject);
            File.WriteAllText(abstractionsProject, "<Project />");
            Directory.Exists(Path.Combine(partiesRoot, "references/Hexalith.McpCli")).ShouldBeFalse();

            EvaluatedProjectGraph graph = EvaluateProjectGraph(
                partiesRoot,
                contractsProject,
                useSource: true,
                $"-p:HexalithMcpCliRoot={mcpCliRoot}",
                "-p:HexalithMcpCliFromSource=true");

            graph.Properties["HexalithMcpCliRoot"].ShouldBe(mcpCliRoot);
            graph.Properties["HexalithMcpCliFromSource"].ShouldBe("true");
            graph.ProjectReferences.ShouldBe([Path.GetFullPath(abstractionsProject).Replace('\\', '/')]);
            graph.PackageReferences.ShouldNotContain("Hexalith.McpCli.Abstractions");
            graph.PackageReferences.ShouldContain("Hexalith.EventStore.Contracts");
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, recursive: true);
            }
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
    [InlineData("FrontComposer UI primitives", "aria-live=\"@(_suppressSpeech ? \"off\" : \"polite\")\"")]
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
    [InlineData("EventStore degraded response and DAPR health checks", true)]
    [InlineData("EventStore degraded response and DAPR health checks", false)]
    [InlineData("FrontComposer UI primitives", true)]
    [InlineData("FrontComposer UI primitives", false)]
    public void DeliveredApiCommandEvidenceRequiresPositiveExactSourcePaths(string surface, bool expectMatch)
    {
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
        // The path-filter fixture must work in a clean clone without the historical final object.
        const string final = "0000000000000000000000000000000000000000";

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
            if (readCompletedDiff is null)
            {
                GitObjectExists(root, finalRevision!).ShouldBeTrue($"Story 8.3 final revision is missing: {finalRevision}");
            }

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

    [Theory]
    [InlineData("")]
    [InlineData("'$(HexalithEventStoreFromSorce)' == 'true'")]
    public void ConsumerDiscoveryRetainsIdentitiesWithRemovedOrMisspelledSelectors(string condition)
    {
        string temporaryRoot = Path.Combine(Path.GetTempPath(), $"parties-consumer-discovery-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryRoot);
        try
        {
            string projectFile = Path.Combine(temporaryRoot, "Consumer.csproj");
            new XDocument(new XElement("Project", new XElement("ItemGroup",
                new XElement("PackageReference", new XAttribute("Include", "Hexalith.EventStore.Contracts"), new XAttribute("Condition", condition)),
                new XElement("ProjectReference", new XAttribute("Include", "source/Hexalith.EventStore.Contracts.csproj"), new XAttribute("Condition", condition)))))
                .Save(projectFile);
            DeclaredDependencyReferences(projectFile, "Hexalith.EventStore", null)
                .ShouldBe(["Hexalith.EventStore.Contracts"]);
        }
        finally
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData("3.125.0", "", false)]
    [InlineData("", "3.125.0", false)]
    [InlineData("", "", true)]
    public void ConsumerPropertyFixturesRejectEffectiveVersionDriftAndMissingProjects(string version, string versionOverride, bool missingProject)
    {
        string temporaryRoot = Path.Combine(Path.GetTempPath(), $"parties-consumer-property-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryRoot);
        try
        {
            string projectFile = Path.Combine(temporaryRoot, "Consumer.csproj");
            XElement items = new("ItemGroup",
                new XElement("PackageReference", new XAttribute("Include", "Hexalith.EventStore.Contracts"),
                    new XAttribute("Version", version), new XAttribute("VersionOverride", versionOverride)),
                new XElement("PackageVersion", new XAttribute("Include", "Hexalith.EventStore.Contracts"), new XAttribute("Version", "3.126.0")));
            if (missingProject)
            {
                items.Add(new XElement("ProjectReference", new XAttribute("Include", "missing.csproj")));
            }

            new XDocument(new XElement("Project", items)).Save(projectFile);
            Should.Throw<ShouldAssertException>(() =>
                {
                    EvaluatedProjectGraph graph = EvaluateProjectGraph(temporaryRoot, projectFile, useSource: null);
                    AssertPackageConsumer(graph, projectFile, "Hexalith.EventStore", "3.126.0");
                })
                .Message.ShouldContain(missingProject ? "selected source project does not exist" : "effective version");
        }
        finally
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    private static EvaluatedProjectGraph EvaluateProjectGraph(
        string root,
        string projectFile,
        bool? useSource,
        params string[] mappedSourceProperties)
        => EvaluateProjectGraphForConfiguration(root, projectFile, useSource == true ? "Debug" : "Release", useSource, mappedSourceProperties);

    private static EvaluatedProjectGraph EvaluateProjectGraphForConfiguration(
        string root,
        string projectFile,
        string configuration,
        bool? useSource,
        params string[] mappedSourceProperties)
    {
        string relativeProject = Path.GetRelativePath(root, projectFile);
        string output = RunProcess(
            root,
            "dotnet",
            [
                "msbuild",
                relativeProject,
                "-nologo",
                "-getProperty:HexalithEventStoreVersion,HexalithCommonsVersion,HexalithMemoriesVersion,UseHexalithProjectReferences,HexalithMcpCliRoot,TargetFramework,TargetFrameworks,WebPublishProfileFile," + string.Join(',', RuntimeSourceSelectors),
                "-getItem:PackageReference,PackageVersion,ProjectReference",
                .. (useSource.HasValue ? new[] { $"-p:UseHexalithProjectReferences={useSource.Value.ToString().ToLowerInvariant()}" } : []),
                $"-p:Configuration={configuration}",
                "-p:NuGetAudit=false",
                "-p:MinVerVersionOverride=1.0.0",
                .. mappedSourceProperties
            ]);
        using JsonDocument document = ParseMsbuildJson(output, relativeProject);
        Dictionary<string, string> properties = document.RootElement.GetProperty("Properties")
            .EnumerateObject()
            .ToDictionary(static property => property.Name, static property => property.Value.GetString() ?? string.Empty, StringComparer.Ordinal);
        JsonElement items = document.RootElement.GetProperty("Items");
        string[] packages = ReadEvaluatedItems(items, "PackageReference", "Identity");
        string[] projects = ReadEvaluatedItems(items, "ProjectReference", "FullPath")
            .Select(static path => path.Replace('\\', '/'))
            .ToArray();
        foreach (string project in projects)
        {
            File.Exists(project).ShouldBeTrue($"{relativeProject}: selected source project does not exist: {project}");
        }

        IReadOnlyDictionary<string, string>[] projectMetadata = items.GetProperty("ProjectReference").EnumerateArray()
            .Select(static item => (IReadOnlyDictionary<string, string>)item.EnumerateObject()
                .ToDictionary(static metadata => metadata.Name, static metadata => metadata.Value.GetString() ?? string.Empty, StringComparer.Ordinal))
            .ToArray();
        return new EvaluatedProjectGraph(properties, packages, projects, ReadPackageMetadata(items), projectMetadata);
    }

    /// <summary>Consumes one structured MSBuild payload while leaving surrounding diagnostics alone.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("Restore complete.\n")]
    [InlineData("warning: unresolved {Property}\n")]
    [InlineData("warning: {\"level\":\"diagnostic\"}\n")]
    [InlineData("{\"level\":\"diagnostic\"}\n")]
    public void MsbuildJsonParsingIgnoresSurroundingDiagnostics(string prefix)
    {
        const string payload = """{"Properties":{"Value":"brace } and { and quote \""},"Items":{"PackageReference":[{"Identity":"example"}]}}""";
        using JsonDocument document = ParseMsbuildJson(prefix + payload + "\nBuild succeeded. {not JSON}", "fixture.csproj");

        document.RootElement.GetProperty("Properties").GetProperty("Value").GetString().ShouldBe("brace } and { and quote \"");
        document.RootElement.GetProperty("Items").GetProperty("PackageReference")[0].GetProperty("Identity").GetString().ShouldBe("example");
    }

    /// <summary>Absent or incomplete payloads must not become successful graph evaluations.</summary>
    [Fact]
    public void MsbuildJsonParsingRejectsMissingPayload()
        => Should.Throw<JsonException>(() => ParseMsbuildJson("diagnostics without a payload", "fixture.csproj"));

    /// <summary>Malformed or incomplete JSON remains an explicit parse failure.</summary>
    [Theory]
    [InlineData("preamble\n{\"Properties\":{")]
    [InlineData("preamble\n{invalid}")]
    [InlineData("{\"Properties\":{\"Value\":\"outer\"},\"Items\":{\"Properties\":{},\"Items\":{}}")]
    public void MsbuildJsonParsingRejectsInvalidPayload(string output)
        => Should.Throw<JsonException>(() => ParseMsbuildJson(output, "fixture.csproj"));

    /// <summary>Reports the owning project and bounded stdout when the graph payload is unavailable.</summary>
    [Fact]
    public void MsbuildJsonParsingFailureRetainsBoundedProjectDiagnostics()
    {
        JsonException failure = Should.Throw<JsonException>(() => ParseMsbuildJson("restore failed " + new string('x', 2_000), "fixture.csproj"));
        failure.Message.ShouldContain("fixture.csproj");
        failure.Message.ShouldContain("restore failed");
        failure.Message.Length.ShouldBeLessThan(1_200);
    }

    private static JsonDocument ParseMsbuildJson(string output, string projectFile)
    {
        for (int start = output.IndexOf('{', StringComparison.Ordinal); start >= 0; start = output.IndexOf('{', start + 1))
        {
            // MSBuild emits its root object at the start of a line. Braces inside diagnostics
            // or a malformed payload must never become nested replacement graph roots.
            if (start > 0 && output[start - 1] != '\n')
            {
                continue;
            }

            try
            {
                byte[] payload = Encoding.UTF8.GetBytes(output[start..]);
                var reader = new Utf8JsonReader(payload);
                JsonDocument document = JsonDocument.ParseValue(ref reader);
                if (document.RootElement.TryGetProperty("Properties", out JsonElement properties) && properties.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty("Items", out JsonElement items) && items.ValueKind == JsonValueKind.Object)
                {
                    return document;
                }

                document.Dispose();
                start += Encoding.UTF8.GetCharCount(payload.AsSpan(0, (int)reader.BytesConsumed)) - 1;
            }
            catch (JsonException)
            {
                // A malformed independent object cannot be repaired by accepting one of its children.
                break;
            }
        }

        string diagnostic = output[..Math.Min(output.Length, 1_000)].Replace('\r', ' ').Replace('\n', ' ');
        throw new JsonException($"MSBuild produced no complete project graph JSON for {projectFile}: {diagnostic}");
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ReadPackageMetadata(JsonElement items)
    {
        Dictionary<string, string> centralVersions = items.TryGetProperty("PackageVersion", out JsonElement versions)
            ? versions.EnumerateArray().ToDictionary(
                static item => item.GetProperty("Identity").GetString()!,
                static item => ReadItemMetadata(item, "Version"),
                StringComparer.Ordinal)
            : new(StringComparer.Ordinal);
        return items.GetProperty("PackageReference").EnumerateArray().ToDictionary(
            static item => item.GetProperty("Identity").GetString()!,
            item =>
            {
                string identity = item.GetProperty("Identity").GetString()!;
                string version = ReadItemMetadata(item, "Version");
                string versionOverride = ReadItemMetadata(item, "VersionOverride");
                string centralVersion = centralVersions.GetValueOrDefault(identity, string.Empty);
                return (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Version"] = version,
                    ["VersionOverride"] = versionOverride,
                    ["CentralVersion"] = centralVersion,
                    ["EffectiveVersion"] = !string.IsNullOrWhiteSpace(versionOverride) ? versionOverride
                        : !string.IsNullOrWhiteSpace(version) ? version : centralVersion,
                };
            },
            StringComparer.Ordinal);
    }

    private static string ReadItemMetadata(JsonElement item, string name)
        => item.TryGetProperty(name, out JsonElement value) ? value.GetString() ?? string.Empty : string.Empty;

    [Theory]
    [InlineData("", "", "3.126.0")]
    [InlineData("3.125.0", "", "3.125.0")]
    [InlineData("3.125.0", "3.124.0", "3.124.0")]
    public void EvaluatedPackageMetadataRetainsVersionAndOverridePrecedence(string version, string versionOverride, string expected)
    {
        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            PackageReference = new[] { new { Identity = "Hexalith.EventStore.Contracts", Version = version, VersionOverride = versionOverride } },
            PackageVersion = new[] { new { Identity = "Hexalith.EventStore.Contracts", Version = "3.126.0" } },
        }));
        IReadOnlyDictionary<string, string> metadata = ReadPackageMetadata(document.RootElement)["Hexalith.EventStore.Contracts"];
        metadata["Version"].ShouldBe(version);
        metadata["VersionOverride"].ShouldBe(versionOverride);
        metadata["EffectiveVersion"].ShouldBe(expected);
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
            string fullPath = RepositoryDependencyPaths.Resolve(normalizedRoot, normalizedPath);
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
        finalLedger.ShouldContain($"Released package `{HistoricalEventStorePackageVersion}`");
        row.Status.ShouldBe("needs-additive-api");

        foreach (string path in RequiredAbsentPayloadProtectionPaths)
        {
            row.ValidationEvidence.ShouldContain($"`test ! -f {path}`");
            File.Exists(Path.Combine(root, path)).ShouldBeFalse(path);
        }

        File.ReadAllText(RepositoryDependencyPaths.Resolve(root, "references/Hexalith.Builds/Props/Directory.Packages.props"))
            .ShouldNotContain("Hexalith.EventStore.PayloadProtection", Case.Sensitive,
                "G5 is not enrolled in the selected package catalog.");
        File.ReadAllText(RepositoryDependencyPaths.Resolve(root, "references/Hexalith.EventStore/tools/release-packages.json"))
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

        string ownerSpec = File.ReadAllText(RepositoryDependencyPaths.Resolve(root, EventStorePayloadProtectionSpecRelativePath));
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
        File.ReadAllLines(RepositoryDependencyPaths.Resolve(root, EventStoreStoryMigrationRelativePath))
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
        => TryRunGit(root, out _, "cat-file", "-e", $"{revision}^{{commit}}");

    private static bool IsNotBuildOutput(string root, string path)
        => !Path.GetRelativePath(root, path).Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            && !Path.GetRelativePath(root, path).Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

    /// <summary>Reproduces an observed descriptor without adopting tags published after the receipt.</summary>
    private static string DescribeObservedSource(string root, string path, string observedDescribe, string revision = "HEAD")
    {
        if (Regex.IsMatch(observedDescribe, "^[0-9a-f]{8}$", RegexOptions.CultureInvariant))
        {
            return RunGit(root, "-C", path, "rev-parse", "--short=8", revision).Trim();
        }

        string observedTag = Regex.Replace(observedDescribe, @"-\d+-g[0-9a-f]+$", string.Empty, RegexOptions.CultureInvariant);
        return RunGit(root, "-C", path, "describe", "--tags", "--match", observedTag, "--abbrev=8", revision).Trim();
    }

    private static string ReadCurrentSourceSelection(string matrix)
    {
        const string currentHeading = "### Current source observation — 2026-10-30";
        int currentStart = matrix.IndexOf(currentHeading, StringComparison.Ordinal);
        currentStart.ShouldBeGreaterThanOrEqualTo(0, currentHeading);
        int historicalStart = matrix.IndexOf("### Historical source observation", currentStart, StringComparison.Ordinal);
        historicalStart.ShouldBeGreaterThan(currentStart);
        return matrix[currentStart..historicalStart];
    }

    // Keep source-selection evidence separate from the consumed-package matrix and historical receipts.
    private static void AssertCurrentSourceRow(string currentSelection, string path, string identity, string describe)
    {
        string dependency = Path.GetFileName(path).Replace("Hexalith.", string.Empty, StringComparison.Ordinal);
        string[][] rows = currentSelection.Split('\n', StringSplitOptions.TrimEntries)
            .Where(static line => line.StartsWith('|'))
            .Select(static line => line.Split('|', StringSplitOptions.TrimEntries))
            .Where(cells => cells.Length > 1 && string.Equals(cells[1], dependency, StringComparison.Ordinal))
            .ToArray();
        rows.Length.ShouldBe(1, $"{path} must have exactly one current source-selection row.");
        string[] cells = rows[0];
        cells.Length.ShouldBe(5, $"{path} must have dependency, identity and release-use columns.");
        cells[2].ShouldBe($"`{identity}` (`{describe}`)", path);
    }

    /// <summary>Separates the committed selection from a tool-selected source override.</summary>
    private static void AssertSelectedSourceObservation(string root, string relativePath, string expectedIdentity)
    {
        string selected = RunGit(root, "ls-tree", "HEAD", relativePath).Trim();
        selected.Replace('\t', ' ').ShouldBe($"160000 commit {expectedIdentity} {relativePath}");
        if (!RepositoryDependencyPaths.IsMappedDependency(root, relativePath))
        {
            AssertGitlinkAndCheckout(root, relativePath, expectedIdentity);
            return;
        }

        string actual = RunGit(root, "-C", relativePath, "rev-parse", "HEAD").Trim();
        Regex.IsMatch(actual, @"\A[0-9a-f]{40}\z", RegexOptions.CultureInvariant).ShouldBeTrue(relativePath);
        bool dirty = !string.IsNullOrWhiteSpace(RunGit(root, "-C", relativePath, "status", "--porcelain", "--ignore-submodules=all"));
        // Observe source context without granting consumption proof. The unchanged strict
        // gate must reject these actual mismatched or dirty mapped checkouts.
        if (actual != expectedIdentity || dirty)
        {
            Should.Throw<ShouldAssertException>(() => AssertGitlinkAndCheckout(root, relativePath, expectedIdentity));
        }
        string state = actual == expectedIdentity ? "mapped-source-match" : "mapped-source-override";
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"{relativePath}: selected={expectedIdentity} actual={actual} state={state} dirty={dirty} immutable_consumption=false");
    }

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

    private sealed record EvaluatedProjectGraph(
        IReadOnlyDictionary<string, string> Properties,
        string[] PackageReferences,
        string[] ProjectReferences,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> PackageMetadata,
        IReadOnlyDictionary<string, string>[] ProjectMetadata);
}
