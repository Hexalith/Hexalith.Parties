using CommunityToolkit.Aspire.Hosting.Dapr;

using Hexalith.EventStore.Aspire;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

string eventStoreAccessControlConfigPath = ResolveDaprConfigPath("accesscontrol.yaml");
string adminServerAccessControlConfigPath = ResolveDaprConfigPath("accesscontrol.eventstore-admin.yaml");
string tenantsAccessControlConfigPath = ResolveDaprConfigPath("accesscontrol.tenants.yaml");
string partiesAccessControlConfigPath = ResolveDaprConfigPath("accesscontrol.parties.yaml");
string memoriesAccessControlConfigPath = ResolveDaprConfigPath("accesscontrol.memories.yaml");
string resiliencyConfigPath = ResolveDaprConfigPath("resiliency.yaml");
// PUBLISH-MODE-DNS-ANCHOR - Story 9.2 publish-mode wiring; matches Story 9.3 keycloak Service shape.
// Service name `keycloak`, namespace `hexalith-parties`, port 8080, realm `hexalith`.
// If Story 9.3 changes the keycloak Service name, port, namespace, or realm, this constant must update.
// Story 9.3 Definition of Done must cross-verify this value against the committed keycloak Service.
const string KeycloakRealmUrlInCluster = "http://keycloak.hexalith-parties.svc.cluster.local:8080/realms/hexalith";

// Registration dictionary key uses the Kubernetes-valid sanitized wildcard form `wildcard_party_v1`
// (story 9.3 AC1 / ADR 9.3-1). ConfigMap data keys must match ^[A-Za-z0-9_.-]+$ and Pod container
// env names must match ^[A-Za-z_][A-Za-z0-9_]*$ — both reject '*' and '|'. The sanitized form
// (alphanumeric + underscore only) binds correctly via .NET configuration's __-separator strategy
// AND survives aspirate emission into a ConfigMap and consumer Pod env vars. The EventStore
// DomainServiceResolver was extended in parallel to recognize the "wildcard_<domain>_<version>"
// shape in addition to the legacy "*|domain|version" pipe form (see
// Hexalith.EventStore/src/Hexalith.EventStore.Server/DomainServices/DomainServiceResolver.cs).
// The dictionary VALUE's TenantId field stays "*" — that is a value, not a key, and is valid in
// env-var values.
IResourceBuilder<ProjectResource> eventStore = builder.AddProject<Projects.Hexalith_EventStore>("eventstore")
    .WithEnvironment("Authentication__DaprInternal__AllowedCallers__0", "tenants")
    .WithEnvironment("EventStore__DomainServices__Registrations__wildcard_party_v1__AppId", "parties")
    .WithEnvironment("EventStore__DomainServices__Registrations__wildcard_party_v1__MethodName", "process")
    .WithEnvironment("EventStore__DomainServices__Registrations__wildcard_party_v1__TenantId", "*")
    .WithEnvironment("EventStore__DomainServices__Registrations__wildcard_party_v1__Domain", "party")
    .WithEnvironment("EventStore__DomainServices__Registrations__wildcard_party_v1__Version", "v1");
IResourceBuilder<ProjectResource> adminServer = builder.AddProject<Projects.Hexalith_EventStore_Admin_Server_Host>("eventstore-admin");
IResourceBuilder<ProjectResource> adminUI = builder.AddProject<Projects.Hexalith_EventStore_Admin_UI>("eventstore-admin-ui");

// Redis is composed in run mode only. Local Aspire dev needs the actual Redis resource wired into
// Dapr state/pubsub metadata; publish mode gets the hand-authored carve-out under deploy/k8s/redis/
// from Story 9.3 and must not let aspirate emit a Redis workload.
IResourceBuilder<RedisResource>? redis = null;
if (builder.ExecutionContext.IsRunMode)
{
    redis = builder.AddRedis("redis");
}

HexalithEventStoreResources eventStoreResources = builder.AddHexalithEventStore(
    eventStore,
    adminServer,
    adminUI,
    eventStoreAccessControlConfigPath,
    adminServerAccessControlConfigPath,
    resiliencyConfigPath,
    redis: redis);

IResourceBuilder<ProjectResource> parties = builder.AddProject<Projects.Hexalith_Parties>("parties")
    .WithDaprSidecar(sidecar => sidecar
        .WithOptions(new DaprSidecarOptions
        {
            AppId = "parties",
            Config = partiesAccessControlConfigPath,
        })
        .WithReference(eventStoreResources.StateStore)
        .WithReference(eventStoreResources.PubSub))
    .WaitFor(eventStoreResources.StateStore)
    .WaitFor(eventStoreResources.PubSub);

IResourceBuilder<ProjectResource> partiesMcp = builder.AddProject<Projects.Hexalith_Parties_Mcp>("parties-mcp")
    .WithReference(eventStore)
    .WaitFor(eventStore)
    .WithReference(parties)
    .WaitFor(parties);

_ = partiesMcp.WithEnvironment("Parties__Mcp__EventStoreGatewayBaseUrl", ReferenceExpression.Create($"{eventStore.GetEndpoint("http")}"));

IResourceBuilder<ProjectResource> tenants = builder.AddProject<Projects.Hexalith_Tenants>("tenants")
    .WithDaprSidecar(sidecar => sidecar
        .WithOptions(new DaprSidecarOptions
        {
            AppId = "tenants",
            Config = tenantsAccessControlConfigPath,
        })
        .WithReference(eventStoreResources.StateStore)
        .WithReference(eventStoreResources.PubSub))
    .WaitFor(eventStoreResources.StateStore)
    .WaitFor(eventStoreResources.PubSub);

string? bootstrapGlobalAdminUserId = builder.Configuration["Tenants:BootstrapGlobalAdminUserId"];
if (!string.IsNullOrWhiteSpace(bootstrapGlobalAdminUserId))
{
    _ = tenants.WithEnvironment("Tenants__BootstrapGlobalAdminUserId", bootstrapGlobalAdminUserId);
}

_ = parties
    .WithReference(eventStore)
    .WaitFor(eventStore)
    .WithReference(tenants)
    .WaitFor(tenants)
    .WithEnvironment("Tenants__Enabled", "true")
    .WithEnvironment("Tenants__ServiceName", "tenants")
    .WithEnvironment("Tenants__CommandApiAppId", "eventstore")
    .WithEnvironment("Tenants__PubSubName", "pubsub")
    .WithEnvironment("Tenants__TopicName", "system.tenants.events");

// Tenants takes a direct WithReference(eventStore) here intentionally, diverging
// from the canonical Hexalith.EventStore.AppHost sample where Tenants is a peer
// with no eventstore reference. Under the EventStore-fronted topology, Tenants
// commands flow through EventStore (Tenants__CommandApiAppId=eventstore on the
// caller side), so Tenants needs eventstore visibility to coordinate startup.
// The Hexalith.Tenants.Aspire helper was removed in this story; if a future
// story (12.2 or beyond) reintroduces a wrapping helper, this block becomes the
// inlined equivalent of what AddHexalithTenants would have produced.
_ = tenants
    .WithReference(eventStore)
    .WaitFor(eventStore);

// Memories.Server: composed in-cluster as a first-class topology participant (story 9.3 AC2 /
// ADR 9.3-2). The configurable external HTTP `MemoriesEndpoint` escape hatch was removed —
// FR31a now enumerates Memories as part of the single-source-of-truth service graph. The
// in-cluster Service URL (`http://memories:8080/`) is the only endpoint Parties uses for the
// `MemoriesSearch` feature; `EnableMemoriesSearch` still controls feature-on/off, never the
// endpoint location.
IResourceBuilder<ProjectResource> memories = builder.AddProject<Projects.Hexalith_Memories_Server>("memories")
    .WithDaprSidecar(sidecar => sidecar
        .WithOptions(new DaprSidecarOptions
        {
            AppId = "memories",
            Config = memoriesAccessControlConfigPath,
        })
        .WithReference(eventStoreResources.StateStore)
        .WithReference(eventStoreResources.PubSub))
    .WaitFor(eventStoreResources.StateStore)
    .WaitFor(eventStoreResources.PubSub);

bool enableMemoriesSearch = builder.ExecutionContext.IsPublishMode
    || string.Equals(builder.Configuration["EnableMemoriesSearch"], "true", StringComparison.OrdinalIgnoreCase);
if (enableMemoriesSearch)
{
    _ = parties
        .WithReference(memories)
        .WaitFor(memories)
        .WithEnvironment("Parties__MemoriesSearch__Enabled", "true")
        .WithEnvironment("Parties__MemoriesSearch__Endpoint", "http://memories:8080/")
        .WithEnvironment("Parties__MemoriesSearch__RequireApiToken", "false")
        .WithEnvironment("Parties__MemoriesSearch__TenantId", "hexalith-dev")
        .WithEnvironment("Parties__MemoriesSearch__CaseId", "parties");
}

bool enableKeycloak = !bool.TryParse(builder.Configuration["EnableKeycloak"], out bool parsed) || parsed;
IResourceBuilder<KeycloakResource>? keycloak = null;
ReferenceExpression? realmUrl = null;
if (enableKeycloak)
{
    if (builder.ExecutionContext.IsRunMode)
    {
        keycloak = builder.AddKeycloak("keycloak", 8180)
            .WithRealmImport("./KeycloakRealms");

        EndpointReference keycloakEndpoint = keycloak.GetEndpoint("http");
        realmUrl = ReferenceExpression.Create($"{keycloakEndpoint}/realms/hexalith");
    }
}

if (keycloak is not null)
{
    _ = eventStore.WithReference(keycloak).WaitFor(keycloak);
    _ = adminServer.WithReference(keycloak).WaitFor(keycloak);
    _ = parties.WithReference(keycloak).WaitFor(keycloak);
    _ = partiesMcp.WithReference(keycloak).WaitFor(keycloak);
    _ = tenants.WithReference(keycloak).WaitFor(keycloak);
    _ = adminUI.WithReference(keycloak).WaitFor(keycloak);
}

_ = WithJwtAuthority(eventStore, realmUrl, builder.ExecutionContext.IsPublishMode ? KeycloakRealmUrlInCluster : null)
    .WithEnvironment("Authentication__JwtBearer__Audience", "hexalith-eventstore")
    .WithEnvironment("Authentication__JwtBearer__RequireHttpsMetadata", "false")
    .WithEnvironment("Authentication__JwtBearer__SigningKey", "");

_ = WithJwtAuthority(adminServer, realmUrl, builder.ExecutionContext.IsPublishMode ? KeycloakRealmUrlInCluster : null)
    .WithEnvironment("Authentication__JwtBearer__Audience", "hexalith-eventstore")
    .WithEnvironment("Authentication__JwtBearer__RequireHttpsMetadata", "false")
    .WithEnvironment("Authentication__JwtBearer__SigningKey", "");

// Multi-audience tolerance: Parties accepts its own audience plus the
// EventStore audience, so tokens minted for cross-service DAPR invocations
// (eventstore -> parties /process) validate without per-call token exchange.
_ = WithJwtAuthority(parties, realmUrl, builder.ExecutionContext.IsPublishMode ? KeycloakRealmUrlInCluster : null)
    .WithEnvironment("Authentication__JwtBearer__Audience", "hexalith-parties")
    .WithEnvironment("Authentication__JwtBearer__TokenValidationParameters__ValidAudiences__0", "hexalith-parties")
    .WithEnvironment("Authentication__JwtBearer__TokenValidationParameters__ValidAudiences__1", "hexalith-eventstore")
    .WithEnvironment("Authentication__JwtBearer__RequireHttpsMetadata", "false")
    .WithEnvironment("Authentication__JwtBearer__SigningKey", "");

_ = WithJwtAuthority(partiesMcp, realmUrl, builder.ExecutionContext.IsPublishMode ? KeycloakRealmUrlInCluster : null)
    .WithEnvironment("Authentication__JwtBearer__Audience", "hexalith-parties-mcp")
    .WithEnvironment("Authentication__JwtBearer__TokenValidationParameters__ValidAudiences__0", "hexalith-parties-mcp")
    .WithEnvironment("Authentication__JwtBearer__TokenValidationParameters__ValidAudiences__1", "hexalith-eventstore")
    .WithEnvironment("Authentication__JwtBearer__RequireHttpsMetadata", "false")
    .WithEnvironment("Authentication__JwtBearer__SigningKey", "");

// Multi-audience tolerance: Tenants validates its own audience and accepts
// EventStore-issued tokens for command-gateway invocations.
_ = WithJwtAuthority(tenants, realmUrl, builder.ExecutionContext.IsPublishMode ? KeycloakRealmUrlInCluster : null)
    .WithEnvironment("Authentication__JwtBearer__Audience", "hexalith-tenants")
    .WithEnvironment("Authentication__JwtBearer__TokenValidationParameters__ValidAudiences__0", "hexalith-tenants")
    .WithEnvironment("Authentication__JwtBearer__TokenValidationParameters__ValidAudiences__1", "hexalith-eventstore")
    .WithEnvironment("Authentication__JwtBearer__RequireHttpsMetadata", "false")
    .WithEnvironment("Authentication__JwtBearer__SigningKey", "");

_ = adminUI.WithEnvironment("EventStore__AdminServer__SwaggerUrl", ReferenceExpression.Create($"{adminServer.GetEndpoint("http")}/swagger/index.html"));
if (realmUrl is not null)
{
    _ = adminUI
        .WithEnvironment("EventStore__Authentication__Authority", realmUrl)
        .WithEnvironment("EventStore__Authentication__ClientId", "hexalith-eventstore");
}
else if (builder.ExecutionContext.IsPublishMode)
{
    _ = adminUI
        .WithEnvironment("EventStore__Authentication__Authority", KeycloakRealmUrlInCluster)
        .WithEnvironment("EventStore__Authentication__ClientId", "hexalith-eventstore");
}
else
{
    // Keycloak disabled: explicitly clear OIDC env vars on adminUI to prevent
    // stale Authority/ClientId values from a previous launch leaking into the UI.
    _ = adminUI
        .WithEnvironment("EventStore__Authentication__Authority", "")
        .WithEnvironment("EventStore__Authentication__ClientId", "");
}

// PUBLISH_TARGET registers an Aspire-native publish environment (`dotnet aspire publish`).
// This is orthogonal to the aspirate-based Kubernetes deploy path documented in
// `deploy/k8s/` (story 9-1). Aspirate reads the AppHost composition directly and
// produces its own manifests under `deploy/k8s/`; the `k8s` branch below only
// affects Aspire's own publish pipeline if a future story opts in.
string? publishTarget = builder.Configuration["PUBLISH_TARGET"];
if (string.IsNullOrWhiteSpace(publishTarget))
{
    // Default: no publish environment registered; AppHost runs in dev mode only.
}
else if (string.Equals(publishTarget, "docker", StringComparison.OrdinalIgnoreCase))
{
    _ = builder.AddDockerComposeEnvironment("docker");
}
else if (string.Equals(publishTarget, "k8s", StringComparison.OrdinalIgnoreCase))
{
    _ = builder.AddKubernetesEnvironment("k8s");
}
else if (string.Equals(publishTarget, "aca", StringComparison.OrdinalIgnoreCase))
{
    _ = builder.AddAzureContainerAppEnvironment("aca");
}
else
{
    throw new InvalidOperationException(
        $"Unknown PUBLISH_TARGET '{publishTarget}'. Supported values: docker, k8s, aca, or unset for dev mode.");
}

builder.Build().Run();

static string ResolveDaprConfigPath(string fileName)
{
    string cwdPath = Path.Combine(Directory.GetCurrentDirectory(), "DaprComponents", fileName);
    if (File.Exists(cwdPath))
    {
        return cwdPath;
    }

    string baseDirPath = Path.Combine(AppContext.BaseDirectory, "DaprComponents", fileName);
    if (File.Exists(baseDirPath))
    {
        return baseDirPath;
    }

    string sourceTreePath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "DaprComponents", fileName));
    if (File.Exists(sourceTreePath))
    {
        return sourceTreePath;
    }

    throw new FileNotFoundException(
        $"DAPR configuration not found. Probed: '{cwdPath}', '{baseDirPath}', '{sourceTreePath}'. "
        + $"Ensure {fileName} exists in the DaprComponents directory of the running AppHost.",
        cwdPath);
}

// PUBLISH-MODE-JWT-HELPER - single-place contract for JWT authority/issuer wiring.
static IResourceBuilder<ProjectResource> WithJwtAuthority(
    IResourceBuilder<ProjectResource> service,
    ReferenceExpression? runModeAuthority,
    string? publishModeAuthority)
{
    if (runModeAuthority is not null)
    {
        return service
            .WithEnvironment("Authentication__JwtBearer__Authority", runModeAuthority)
            .WithEnvironment("Authentication__JwtBearer__Issuer", runModeAuthority);
    }

    if (publishModeAuthority is not null)
    {
        return service
            .WithEnvironment("Authentication__JwtBearer__Authority", publishModeAuthority)
            .WithEnvironment("Authentication__JwtBearer__Issuer", publishModeAuthority);
    }

    return service;
}
