using CommunityToolkit.Aspire.Hosting.Dapr;

using Hexalith.Parties.Aspire;
using Hexalith.Tenants.Aspire;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// Resolve DAPR access control configuration path.
// Both parties and any domain-service sidecars load this Configuration CRD.
string accessControlConfigPath = Path.Combine(Directory.GetCurrentDirectory(), "DaprComponents", "accesscontrol.yaml");
if (!File.Exists(accessControlConfigPath))
{
    accessControlConfigPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "DaprComponents", "accesscontrol.yaml"));
}

if (!File.Exists(accessControlConfigPath))
{
    throw new FileNotFoundException(
        "DAPR access control configuration not found. "
        + "Ensure accesscontrol.yaml exists in the DaprComponents directory.",
        accessControlConfigPath);
}

// Add Parties service project.
IResourceBuilder<ProjectResource> parties = builder.AddProject<Projects.Hexalith_Parties>("parties");
IResourceBuilder<ProjectResource> tenants = builder.AddProject<Projects.Hexalith_Tenants>("tenants");

HexalithTenantsResources tenantsResources = builder.AddHexalithTenants(tenants, accessControlConfigPath);

string? bootstrapGlobalAdminUserId = builder.Configuration["Tenants:BootstrapGlobalAdminUserId"];
if (!string.IsNullOrWhiteSpace(bootstrapGlobalAdminUserId))
{
    _ = tenants.WithEnvironment("Tenants__BootstrapGlobalAdminUserId", bootstrapGlobalAdminUserId);
}

// Wire Parties topology (delegates to EventStore + Parties Aspire extensions)
HexalithPartiesResources partiesResources = builder.AddHexalithParties(
    parties,
    accessControlConfigPath,
    tenantsResources.StateStore,
    tenantsResources.PubSub);

_ = parties
    .WithReference(tenantsResources.CommandApi)
    .WaitFor(tenantsResources.CommandApi)
    .WithEnvironment("Tenants__Enabled", "true")
    .WithEnvironment("Tenants__ServiceName", "tenants")
    .WithEnvironment("Tenants__CommandApiAppId", "parties")
    .WithEnvironment("Tenants__PubSubName", "pubsub")
    .WithEnvironment("Tenants__TopicName", "system.tenants.events");

if (string.Equals(builder.Configuration["EnableMemoriesSearch"], "true", StringComparison.OrdinalIgnoreCase))
{
    string memoriesEndpoint = builder.Configuration["MemoriesEndpoint"] ?? "http://localhost:5010/";
    if (!memoriesEndpoint.EndsWith('/'))
    {
        // P4: PartyMemorySearchOptionsValidator requires the endpoint base address to end
        // with a trailing slash so HttpClient relative-path resolution preserves the base
        // path component. Normalize here so operators supplying a custom MemoriesEndpoint
        // without a slash do not see the validator fail at startup.
        memoriesEndpoint += "/";
    }

    _ = parties
        .WithEnvironment("Parties__MemoriesSearch__Enabled", "true")
        .WithEnvironment("Parties__MemoriesSearch__Endpoint", memoriesEndpoint)
        .WithEnvironment("Parties__MemoriesSearch__RequireApiToken", "false")
        .WithEnvironment("Parties__MemoriesSearch__TenantId", "hexalith-dev")
        .WithEnvironment("Parties__MemoriesSearch__CaseId", "parties");
}

// Optional Keycloak OIDC integration (follow EventStore pattern).
// Set EnableKeycloak=false in environment or appsettings to run without Keycloak
// (falls back to symmetric key auth via Authentication:JwtBearer:SigningKey).
if (!string.Equals(builder.Configuration["EnableKeycloak"], "false", StringComparison.OrdinalIgnoreCase))
{
    IResourceBuilder<KeycloakResource> keycloak = builder.AddKeycloak("keycloak", 8180)
        .WithRealmImport("./KeycloakRealms");

    EndpointReference keycloakEndpoint = keycloak.GetEndpoint("http");
    var realmUrl = ReferenceExpression.Create($"{keycloakEndpoint}/realms/hexalith");
    _ = parties
        .WithReference(keycloak)
        .WaitFor(keycloak)
        .WithEnvironment("Authentication__JwtBearer__Authority", realmUrl)
        .WithEnvironment("Authentication__JwtBearer__Issuer", realmUrl)
        .WithEnvironment("Authentication__JwtBearer__Audience", "hexalith-parties")
        .WithEnvironment("Authentication__JwtBearer__RequireHttpsMetadata", "false")
        // Explicitly clear SigningKey to prevent dual-mode auth conflict.
        .WithEnvironment("Authentication__JwtBearer__SigningKey", "");
}

// --- Publisher environments (only activate during `aspire publish`) ---
string? publishTarget = builder.Configuration["PUBLISH_TARGET"];
if (string.Equals(publishTarget, "docker", StringComparison.OrdinalIgnoreCase))
{
    builder.AddDockerComposeEnvironment("docker");
}
else if (string.Equals(publishTarget, "k8s", StringComparison.OrdinalIgnoreCase))
{
    builder.AddKubernetesEnvironment("k8s");
}
else if (string.Equals(publishTarget, "aca", StringComparison.OrdinalIgnoreCase))
{
    builder.AddAzureContainerAppEnvironment("aca");
}

builder.Build().Run();
