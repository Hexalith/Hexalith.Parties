using CommunityToolkit.Aspire.Hosting.Dapr;

using Hexalith.EventStore.Aspire;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

string eventStoreAccessControlConfigPath = ResolveDaprConfigPath("accesscontrol.yaml");
string adminServerAccessControlConfigPath = ResolveDaprConfigPath("accesscontrol.eventstore-admin.yaml");
string sampleAccessControlConfigPath = ResolveDaprConfigPath("accesscontrol.sample.yaml");
string tenantsAccessControlConfigPath = ResolveDaprConfigPath("accesscontrol.tenants.yaml");
string partiesAccessControlConfigPath = ResolveDaprConfigPath("accesscontrol.parties.yaml");
string memoriesAccessControlConfigPath = ResolveDaprConfigPath("accesscontrol.memories.yaml");
string resiliencyConfigPath = ResolveDaprConfigPath("resiliency.yaml");
const string PublishModeJwtIssuer = "https://auth.tache.ai/realms/tache";
const string PublishModeJwtAuthority = PublishModeJwtIssuer;

// Registration dictionary key uses the Kubernetes-valid sanitized wildcard form `wildcard_party_v1`
// (story 9.3 AC1 / ADR 9.3-1). ConfigMap data keys must match ^[A-Za-z0-9_.-]+$ and Pod container
// env names must match ^[A-Za-z_][A-Za-z0-9_]*$ — both reject '*' and '|'. The sanitized form
// (alphanumeric + underscore only) binds correctly via .NET configuration's __-separator strategy
// AND survives aspirate emission into a ConfigMap and consumer Pod env vars. The EventStore
// DomainServiceResolver was extended in parallel to recognize the "wildcard_<domain>_<version>"
// shape in addition to the legacy "*|domain|version" pipe form (see
// references/Hexalith.EventStore/src/Hexalith.EventStore.Server/DomainServices/DomainServiceResolver.cs).
// The dictionary VALUE's TenantId field stays "*" — that is a value, not a key, and is valid in
// env-var values.
IResourceBuilder<ProjectResource> eventStore = builder.AddProject<Projects.Hexalith_EventStore>("eventstore")
    .WithEnvironment("Authentication__DaprInternal__AllowedCallers__0", "tenants")
    .WithEnvironment("EventStore__DomainServices__Registrations__wildcard_party_v1__AppId", "parties")
    .WithEnvironment("EventStore__DomainServices__Registrations__wildcard_party_v1__MethodName", "process")
    .WithEnvironment("EventStore__DomainServices__Registrations__wildcard_party_v1__TenantId", "*")
    .WithEnvironment("EventStore__DomainServices__Registrations__wildcard_party_v1__Domain", "party")
    .WithEnvironment("EventStore__DomainServices__Registrations__wildcard_party_v1__Version", "v1");

// Story 1.7 (AR-D6) — enable the EventStore projection-changes SignalR hub UNCONDITIONALLY (previously set
// only inside the EventStore-sample block) so parties-ui's live-freshness subscription has a hub whenever
// the topology runs. The hub path is /hubs/projection-changes (ProjectionChangedHub.HubPath); server-side
// enablement is the EventStore host's EventStore:SignalR:Enabled (AddEventStoreSignalR).
_ = eventStore.WithEnvironment("EventStore__SignalR__Enabled", "true");
IResourceBuilder<ProjectResource> adminServer = builder.AddProject<Projects.Hexalith_EventStore_Admin_Server_Host>("eventstore-admin");
IResourceBuilder<ProjectResource> adminUI = builder.AddProject<Projects.Hexalith_EventStore_Admin_UI>("eventstore-admin-ui")
    .WithExplicitStart();

// Persistence is the DAPR state store / pub-sub layer. Redis is provided by `dapr init` at
// 127.0.0.1:6379 and wired into the statestore/pubsub component metadata by AddHexalithEventStore —
// it is not managed by Aspire. Runtime deployment backing stores are owned by the external
// deployment orchestrator, not by this AppHost.
HexalithEventStoreResources eventStoreResources = builder.AddHexalithEventStore(
    eventStore,
    adminServer,
    adminUI,
    eventStoreAccessControlConfigPath,
    adminServerAccessControlConfigPath,
    resiliencyConfigPath);

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
    .WithExplicitStart()
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

// parties-ui: Blazor Server BFF over HTTP/SignalR — NO DAPR sidecar (like parties-mcp /
// eventstore-admin-ui). Auto-starts (no WithExplicitStart) so AC2 — "healthy once eventstore/
// tenants are healthy" — is observable on `aspire run`. OIDC/Keycloak wiring is Story 1.2;
// ServiceDefaults health-check endpoints are Story 1.10.
IResourceBuilder<ProjectResource> partiesUi = builder.AddProject<Projects.Hexalith_Parties_UI>("parties-ui")
    .WithReference(eventStore)
    .WaitFor(eventStore)
    .WithReference(tenants)
    .WaitFor(tenants)
    // Story 1.7 (AR-D6) — inject the EventStore projection-changes hub URL so the host-side live-freshness
    // subscription connects server-side (the browser still talks only to the UI host). Mirrors the
    // parties-mcp EventStoreGatewayBaseUrl pattern + the sample-blazor-ui hub-URL composition.
    .WithEnvironment(
        "EventStore__SignalR__HubUrl",
        ReferenceExpression.Create($"{eventStore.GetEndpoint("http")}/hubs/projection-changes"));

// Memories.Server is an optional reference submodule. It is composed as a first-class DAPR resource
// in publish mode (FR31a single-source-of-truth service graph) and on demand in run mode when
// `EnableMemoriesSearch=true`; the default one-command local Parties run does not require the
// Hexalith.Memories submodule to be initialized. The project is resolved by path (not a compile-time
// Projects.* reference) so the AppHost csproj carries no hard Memories dependency.
if (builder.ExecutionContext.IsPublishMode
    || string.Equals(builder.Configuration["EnableMemoriesSearch"], "true", StringComparison.OrdinalIgnoreCase))
{
    string memoriesProjectPath = ResolveOptionalReferenceProjectPath(
        Path.Combine("references", "Hexalith.Memories"),
        Path.Combine("src", "Hexalith.Memories.Server", "Hexalith.Memories.Server.csproj"),
        "EnableMemoriesSearch");

    // Memories.Server is optional for the default one-command local Parties run, but publish
    // mode always composes it as a first-class topology participant. The in-cluster Service URL
    // is the only rich-search endpoint Parties uses.
    IResourceBuilder<ProjectResource> memories = builder.AddProject("memories", memoriesProjectPath)
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

    if (builder.ExecutionContext.IsPublishMode)
    {
        _ = memories
            .WithEnvironment("ConnectionStrings__redis", "redis:6379")
            .WithEnvironment("ConnectionStrings__falkordb", "falkordb:6379");
    }
    _ = parties
        .WithReference(memories)
        .WaitFor(memories)
        .WithEnvironment("Parties__MemoriesSearch__Enabled", "true")
        .WithEnvironment("Parties__MemoriesSearch__Endpoint", "http://memories:8080/")
        .WithEnvironment("Parties__MemoriesSearch__RequireApiToken", "false")
        .WithEnvironment("Parties__MemoriesSearch__TenantId", "tenant-a")
        .WithEnvironment("Parties__MemoriesSearch__CaseId", "parties");
}

if (builder.ExecutionContext.IsPublishMode
    || string.Equals(builder.Configuration["EnableEventStoreSampleUi"], "true", StringComparison.OrdinalIgnoreCase))
{
    string sampleProjectPath = ResolveOptionalReferenceProjectPath(
        Path.Combine("references", "Hexalith.EventStore"),
        Path.Combine("samples", "Hexalith.EventStore.Sample", "Hexalith.EventStore.Sample.csproj"),
        "EnableEventStoreSampleUi");
    string sampleBlazorUiProjectPath = ResolveOptionalReferenceProjectPath(
        Path.Combine("references", "Hexalith.EventStore"),
        Path.Combine("samples", "Hexalith.EventStore.Sample.BlazorUI", "Hexalith.EventStore.Sample.BlazorUI.csproj"),
        "EnableEventStoreSampleUi");

    // EventStore__SignalR__Enabled is now set unconditionally near the eventstore definition (Story 1.7).

    IResourceBuilder<ProjectResource> sample = builder.AddProject("sample", sampleProjectPath)
        .WithDaprSidecar(sidecar => sidecar
            .WithOptions(new DaprSidecarOptions
            {
                AppId = "sample",
                Config = sampleAccessControlConfigPath,
            }));

    _ = builder.AddProject("sample-blazor-ui", sampleBlazorUiProjectPath)
        .WithReference(eventStore)
        .WaitFor(eventStore)
        .WithReference(sample)
        .WaitFor(sample)
        .WithDaprSidecar(sidecar => sidecar
            .WithOptions(new DaprSidecarOptions
            {
                AppId = "sample-blazor-ui",
            }))
        .WithEnvironment("EventStore__EventStoreUrl", ReferenceExpression.Create($"{eventStore.GetEndpoint("http")}"))
        .WithEnvironment("EventStore__SignalR__HubUrl", ReferenceExpression.Create($"{eventStore.GetEndpoint("http")}/hubs/projection-changes"))
        .WithEnvironment("EventStore__Authentication__Authority", PublishModeJwtAuthority)
        .WithEnvironment("EventStore__Authentication__ClientId", "hexalith-eventstore")
        .WithEnvironment("EventStore__Authentication__ClientCredentialsClientId", "hexalith-eventstore-ui")
        .WithEnvironment("EventStore__Authentication__Subject", "sample-blazor-ui")
        .WithEnvironment("EventStore__Authentication__Issuer", PublishModeJwtIssuer)
        .WithEnvironment("EventStore__Authentication__Audience", "hexalith-eventstore")
        .WithEnvironment("EventStore__Authentication__SigningKey", "")
        .WithEnvironment("EventStore__Authentication__Tenants__0", "tenant-a")
        .WithEnvironment("EventStore__Authentication__Domains__0", "counter")
        .WithEnvironment("EventStore__Authentication__Permissions__0", "command:submit")
        .WithEnvironment("EventStore__Authentication__Permissions__1", "query:read");
}

HexalithEventStoreSecurityResources? security = builder.ExecutionContext.IsRunMode
    ? builder.AddHexalithEventStoreSecurity()
    : null;
ReferenceExpression? realmUrl = security?.RealmUrl;

if (security is not null)
{
    _ = eventStore.WithSecurityDependency(security);
    _ = adminServer.WithSecurityDependency(security);
    _ = parties.WithSecurityDependency(security);
    _ = partiesMcp.WithSecurityDependency(security);
    _ = tenants.WithSecurityDependency(security);
    _ = adminUI.WithSecurityDependency(security);
    _ = partiesUi.WithSecurityDependency(security);
}

string? publishModeAuthority = builder.ExecutionContext.IsPublishMode
    ? PublishModeJwtAuthority
    : null;
string requireHttpsMetadata = builder.ExecutionContext.IsPublishMode ? "true" : "false";

_ = WithJwtAuthentication(eventStore, realmUrl, publishModeAuthority, builder.ExecutionContext.IsPublishMode ? PublishModeJwtIssuer : null)
    .WithEnvironment("Authentication__JwtBearer__Audience", "hexalith-eventstore")
    .WithEnvironment("Authentication__JwtBearer__RequireHttpsMetadata", requireHttpsMetadata)
    .WithEnvironment("Authentication__JwtBearer__SigningKey", "");

_ = WithJwtAuthentication(adminServer, realmUrl, publishModeAuthority, builder.ExecutionContext.IsPublishMode ? PublishModeJwtIssuer : null)
    .WithEnvironment("Authentication__JwtBearer__Audience", "hexalith-eventstore")
    .WithEnvironment("Authentication__JwtBearer__RequireHttpsMetadata", requireHttpsMetadata)
    .WithEnvironment("Authentication__JwtBearer__SigningKey", "");

// Multi-audience tolerance: Parties accepts its own audience plus the
// EventStore audience, so tokens minted for cross-service DAPR invocations
// (eventstore -> parties /process) validate without per-call token exchange.
_ = WithJwtAuthentication(parties, realmUrl, publishModeAuthority, builder.ExecutionContext.IsPublishMode ? PublishModeJwtIssuer : null)
    .WithEnvironment("Authentication__JwtBearer__Audience", "hexalith-parties")
    .WithEnvironment("Authentication__JwtBearer__TokenValidationParameters__ValidAudiences__0", "hexalith-parties")
    .WithEnvironment("Authentication__JwtBearer__TokenValidationParameters__ValidAudiences__1", "hexalith-eventstore")
    .WithEnvironment("Authentication__JwtBearer__RequireHttpsMetadata", requireHttpsMetadata)
    .WithEnvironment("Authentication__JwtBearer__SigningKey", "");

_ = WithJwtAuthentication(partiesMcp, realmUrl, publishModeAuthority, builder.ExecutionContext.IsPublishMode ? PublishModeJwtIssuer : null)
    .WithEnvironment("Authentication__JwtBearer__Audience", "hexalith-parties-mcp")
    .WithEnvironment("Authentication__JwtBearer__TokenValidationParameters__ValidAudiences__0", "hexalith-parties-mcp")
    .WithEnvironment("Authentication__JwtBearer__TokenValidationParameters__ValidAudiences__1", "hexalith-eventstore")
    .WithEnvironment("Authentication__JwtBearer__RequireHttpsMetadata", requireHttpsMetadata)
    .WithEnvironment("Authentication__JwtBearer__SigningKey", "");

// Multi-audience tolerance: Tenants validates its own audience and accepts
// EventStore-issued tokens for command-gateway invocations.
_ = WithJwtAuthentication(tenants, realmUrl, publishModeAuthority, builder.ExecutionContext.IsPublishMode ? PublishModeJwtIssuer : null)
    .WithEnvironment("Authentication__JwtBearer__Audience", "hexalith-tenants")
    .WithEnvironment("Authentication__JwtBearer__TokenValidationParameters__ValidAudiences__0", "hexalith-tenants")
    .WithEnvironment("Authentication__JwtBearer__TokenValidationParameters__ValidAudiences__1", "hexalith-eventstore")
    .WithEnvironment("Authentication__JwtBearer__RequireHttpsMetadata", requireHttpsMetadata)
    .WithEnvironment("Authentication__JwtBearer__SigningKey", "");

_ = adminUI
    .WithEnvironment("EventStore__AdminServer__BaseUrl", ReferenceExpression.Create($"{adminServer.GetEndpoint("http")}"))
    .WithEnvironment("EventStore__AdminServer__SwaggerUrl", ReferenceExpression.Create($"{adminServer.GetEndpoint("http")}/swagger/index.html"))
    .WithEnvironment("EventStore__SignalR__HubUrl", ReferenceExpression.Create($"{eventStore.GetEndpoint("http")}/hubs/projection-changes"));
if (realmUrl is not null)
{
    _ = adminUI
        .WithEnvironment("EventStore__Authentication__Authority", realmUrl)
        .WithEnvironment("EventStore__Authentication__ClientId", "hexalith-eventstore");
}
else if (builder.ExecutionContext.IsPublishMode)
{
    _ = adminUI
        .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
        .WithEnvironment("DOTNET_ENVIRONMENT", "Development")
        .WithEnvironment("EventStore__Authentication__Authority", PublishModeJwtAuthority)
        .WithEnvironment("EventStore__Authentication__Issuer", PublishModeJwtIssuer)
        .WithEnvironment("EventStore__Authentication__Audience", "hexalith-eventstore")
        .WithEnvironment("EventStore__Authentication__ClientId", "hexalith-eventstore")
        .WithEnvironment("EventStore__Authentication__ClientCredentialsClientId", "hexalith-eventstore-ui");
}
else
{
    // Keycloak disabled: explicitly clear OIDC env vars on adminUI to prevent
    // stale Authority/ClientId values from a previous launch leaking into the UI.
    _ = adminUI
        .WithEnvironment("EventStore__Authentication__Authority", "")
        .WithEnvironment("EventStore__Authentication__ClientId", "");
}

// parties-ui is an OIDC relying party (Story 1.2 / AR-D5): authorization-code sign-in into a
// server-side cookie session — NOT a JWT bearer resource server, so it uses
// Authentication__OpenIdConnect__* and is deliberately NOT routed through WithJwtAuthentication.
// Mirrors the adminUI realm/publish conditional above; parties-ui references and waits for the
// local security resource when AddHexalithEventStoreSecurity returns one.
if (realmUrl is not null)
{
    // Run mode: interactive sign-in against the local dev Keycloak. The dev-only client secret
    // matches the Hexalith.Tenants.UI precedent — a throwaway local-realm secret, NOT under deploy/
    // and NOT a production credential.
    _ = partiesUi
        .WithEnvironment("Authentication__OpenIdConnect__Authority", realmUrl)
        .WithEnvironment("Authentication__OpenIdConnect__ClientId", "hexalith-parties-ui")
        .WithEnvironment("Authentication__OpenIdConnect__ClientSecret", "parties-ui-dev-secret")
        .WithEnvironment("Authentication__OpenIdConnect__Audience", "hexalith-eventstore");
}
else if (builder.ExecutionContext.IsPublishMode)
{
    // Publish: tache realm. The client secret MUST come from configuration / a secret store, never
    // a committed literal — source it from builder.Configuration (env / user-secrets).
    _ = partiesUi
        .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
        .WithEnvironment("Authentication__OpenIdConnect__Authority", PublishModeJwtAuthority)
        .WithEnvironment("Authentication__OpenIdConnect__ClientId", "hexalith-parties-ui")
        .WithEnvironment("Authentication__OpenIdConnect__ClientSecret", builder.Configuration["PartiesUi:OidcClientSecret"] ?? "")
        .WithEnvironment("Authentication__OpenIdConnect__Audience", "hexalith-eventstore");
}

// PUBLISH_TARGET registers an Aspire-native publish environment (`dotnet aspire publish`).
// Runtime deployment orchestration is external to this repository; the `k8s` branch below
// only affects Aspire's own publish pipeline if a future story opts in.
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

// Resolves an optional reference submodule project by path so the AppHost carries no compile-time
// ProjectReference to it. Used for Memories.Server, which is only needed when rich search is enabled.
static string ResolveOptionalReferenceProjectPath(string submodulePath, string projectRelativePath, string enablingSettingName)
{
    string repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    string projectPath = Path.Combine(repositoryRoot, submodulePath, projectRelativePath);
    if (File.Exists(projectPath))
    {
        return projectPath;
    }

    string normalizedSubmodulePath = submodulePath.Replace('\\', '/');
    throw new FileNotFoundException(
        $"Optional project '{projectPath}' was not found. Run 'git submodule update --init {normalizedSubmodulePath}' before enabling '{enablingSettingName}' or publishing the Kubernetes topology. Do not use recursive submodule initialization for the default local run.",
        projectPath);
}

// PUBLISH-MODE-JWT-HELPER - single-place contract for JWT authority/issuer wiring.
static IResourceBuilder<ProjectResource> WithJwtAuthentication(
    IResourceBuilder<ProjectResource> service,
    ReferenceExpression? runModeAuthority,
    string? publishModeAuthority,
    string? publishModeIssuer)
{
    if (runModeAuthority is not null)
    {
        return service
            .WithEnvironment("Authentication__JwtBearer__Authority", runModeAuthority)
            .WithEnvironment("Authentication__JwtBearer__Issuer", runModeAuthority);
    }

    if (publishModeIssuer is not null)
    {
        return service
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
            .WithEnvironment("DOTNET_ENVIRONMENT", "Development")
            .WithEnvironment("Authentication__JwtBearer__Authority", publishModeAuthority ?? publishModeIssuer)
            .WithEnvironment("Authentication__JwtBearer__Issuer", publishModeIssuer);
    }

    return service;
}
