using CommunityToolkit.Aspire.Hosting.Dapr;

using Hexalith.EventStore.Aspire;

const string FalseLiteral = "false";

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

string eventStoreAccessControlConfigPath = ResolveDaprConfigPath("accesscontrol.yaml");
string adminServerAccessControlConfigPath = ResolveDaprConfigPath("accesscontrol.eventstore-admin.yaml");
string tenantsAccessControlConfigPath = ResolveDaprConfigPath("accesscontrol.tenants.yaml");
string partiesAccessControlConfigPath = ResolveDaprConfigPath("accesscontrol.parties.yaml");
string resiliencyConfigPath = ResolveDaprConfigPath("resiliency.yaml");

IResourceBuilder<ProjectResource> eventStore = builder.AddProject<Projects.Hexalith_EventStore>("eventstore")
    .WithEnvironment("Authentication__DaprInternal__AllowedCallers__0", "tenants")
    .WithEnvironment("EventStore__DomainServices__Registrations__*|party|v1__AppId", "parties")
    .WithEnvironment("EventStore__DomainServices__Registrations__*|party|v1__MethodName", "process")
    .WithEnvironment("EventStore__DomainServices__Registrations__*|party|v1__TenantId", "*")
    .WithEnvironment("EventStore__DomainServices__Registrations__*|party|v1__Domain", "party")
    .WithEnvironment("EventStore__DomainServices__Registrations__*|party|v1__Version", "v1");
IResourceBuilder<ProjectResource> adminServer = builder.AddProject<Projects.Hexalith_EventStore_Admin_Server_Host>("eventstore-admin");
IResourceBuilder<ProjectResource> adminUI = builder.AddProject<Projects.Hexalith_EventStore_Admin_UI>("eventstore-admin-ui");
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
        .WithReference(eventStoreResources.PubSub));

IResourceBuilder<ProjectResource> tenants = builder.AddProject<Projects.Hexalith_Tenants>("tenants")
    .WithDaprSidecar(sidecar => sidecar
        .WithOptions(new DaprSidecarOptions
        {
            AppId = "tenants",
            Config = tenantsAccessControlConfigPath,
        })
        .WithReference(eventStoreResources.StateStore)
        .WithReference(eventStoreResources.PubSub));

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

_ = tenants
    .WithReference(eventStore)
    .WaitFor(eventStore);

if (string.Equals(builder.Configuration["EnableMemoriesSearch"], "true", StringComparison.OrdinalIgnoreCase))
{
    string memoriesEndpoint = builder.Configuration["MemoriesEndpoint"] ?? "http://localhost:5010/";
    if (!memoriesEndpoint.EndsWith('/'))
    {
        memoriesEndpoint += "/";
    }

    _ = parties
        .WithEnvironment("Parties__MemoriesSearch__Enabled", "true")
        .WithEnvironment("Parties__MemoriesSearch__Endpoint", memoriesEndpoint)
        .WithEnvironment("Parties__MemoriesSearch__RequireApiToken", "false")
        .WithEnvironment("Parties__MemoriesSearch__TenantId", "hexalith-dev")
        .WithEnvironment("Parties__MemoriesSearch__CaseId", "parties");
}

IResourceBuilder<KeycloakResource>? keycloak = null;
ReferenceExpression? realmUrl = null;
if (!string.Equals(builder.Configuration["EnableKeycloak"], FalseLiteral, StringComparison.OrdinalIgnoreCase))
{
    keycloak = builder.AddKeycloak("keycloak", 8180)
        .WithRealmImport("./KeycloakRealms");

    EndpointReference keycloakEndpoint = keycloak.GetEndpoint("http");
    realmUrl = ReferenceExpression.Create($"{keycloakEndpoint}/realms/hexalith");

    _ = eventStore.WithReference(keycloak)
        .WaitFor(keycloak)
        .WithEnvironment("Authentication__JwtBearer__Authority", realmUrl)
        .WithEnvironment("Authentication__JwtBearer__Issuer", realmUrl)
        .WithEnvironment("Authentication__JwtBearer__Audience", "hexalith-eventstore")
        .WithEnvironment("Authentication__JwtBearer__RequireHttpsMetadata", FalseLiteral)
        .WithEnvironment("Authentication__JwtBearer__SigningKey", "");

    _ = adminServer.WithReference(keycloak)
        .WaitFor(keycloak)
        .WithEnvironment("Authentication__JwtBearer__Authority", realmUrl)
        .WithEnvironment("Authentication__JwtBearer__Issuer", realmUrl)
        .WithEnvironment("Authentication__JwtBearer__Audience", "hexalith-eventstore")
        .WithEnvironment("Authentication__JwtBearer__RequireHttpsMetadata", FalseLiteral)
        .WithEnvironment("Authentication__JwtBearer__SigningKey", "");

    _ = parties.WithReference(keycloak)
        .WaitFor(keycloak)
        .WithEnvironment("Authentication__JwtBearer__Authority", realmUrl)
        .WithEnvironment("Authentication__JwtBearer__Issuer", realmUrl)
        .WithEnvironment("Authentication__JwtBearer__Audience", "hexalith-parties")
        .WithEnvironment("Authentication__JwtBearer__RequireHttpsMetadata", FalseLiteral)
        .WithEnvironment("Authentication__JwtBearer__SigningKey", "");

    _ = tenants.WithReference(keycloak)
        .WaitFor(keycloak)
        .WithEnvironment("Authentication__JwtBearer__Authority", realmUrl)
        .WithEnvironment("Authentication__JwtBearer__Issuer", realmUrl)
        .WithEnvironment("Authentication__JwtBearer__Audience", "hexalith-eventstore")
        .WithEnvironment("Authentication__JwtBearer__RequireHttpsMetadata", FalseLiteral)
        .WithEnvironment("Authentication__JwtBearer__SigningKey", "");

    _ = adminUI.WithReference(keycloak)
        .WaitFor(keycloak)
        .WithEnvironment("EventStore__AdminServer__SwaggerUrl", ReferenceExpression.Create($"{adminServer.GetEndpoint("https")}/swagger/index.html"))
        .WithEnvironment("EventStore__Authentication__Authority", realmUrl)
        .WithEnvironment("EventStore__Authentication__ClientId", "hexalith-eventstore");
}
else
{
    _ = adminUI.WithEnvironment("EventStore__AdminServer__SwaggerUrl", ReferenceExpression.Create($"{adminServer.GetEndpoint("https")}/swagger/index.html"));
}

string? publishTarget = builder.Configuration["PUBLISH_TARGET"];
if (string.Equals(publishTarget, "docker", StringComparison.OrdinalIgnoreCase))
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

builder.Build().Run();

static string ResolveDaprConfigPath(string fileName)
{
    string configPath = Path.Combine(Directory.GetCurrentDirectory(), "DaprComponents", fileName);
    if (!File.Exists(configPath))
    {
        configPath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "DaprComponents", fileName));
    }

    if (!File.Exists(configPath))
    {
        throw new FileNotFoundException(
            "DAPR configuration not found. "
            + $"Ensure {fileName} exists in the DaprComponents directory.",
            configPath);
    }

    return configPath;
}
