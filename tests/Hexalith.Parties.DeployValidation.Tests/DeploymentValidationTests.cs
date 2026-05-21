namespace Hexalith.Parties.DeployValidation.Tests;

using System.Diagnostics;
using System.Text.Json;

/// <summary>
/// Tests for the deployment validation PowerShell script (deploy/validate-deployment.ps1).
/// Creates known-good and known-bad DAPR config YAML files in temp directories,
/// invokes the script, and asserts on exit code and structured output.
/// </summary>
[Collection("DeployValidation")]
public class DeploymentValidationTests : IDisposable
{
    private readonly string _scriptPath;
    private readonly string _tempDir;
    private bool _disposed;

    public DeploymentValidationTests()
    {
        // Find the script relative to the test project
        string? solutionDir = FindSolutionDirectory();
        solutionDir.ShouldNotBeNull("Could not find solution directory");
        _scriptPath = Path.Combine(solutionDir, "deploy", "validate-deployment.ps1");
        File.Exists(_scriptPath).ShouldBeTrue($"Validation script not found at {_scriptPath}");

        _tempDir = Path.Combine(Path.GetTempPath(), $"deploy-validation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ValidProductionConfig_PassesAllChecks()
    {
        WriteValidProductionConfig(_tempDir);

        (int exitCode, string output) = await RunValidationAsync(_tempDir);

        exitCode.ShouldBe(0, $"Expected exit code 0 but got {exitCode}. Output:\n{output}");
        output.ShouldContain("PASS");
    }

    [Fact]
    public async Task MissingAccessControl_FailsWithSpecificError()
    {
        WriteValidProductionConfig(_tempDir);
        File.Delete(Path.Combine(_tempDir, "accesscontrol.yaml"));

        (int exitCode, string output) = await RunValidationAsync(_tempDir);

        exitCode.ShouldBe(1);
        output.ShouldContain("accesscontrol.yaml not found");
    }

    [Fact]
    public async Task MissingEventStoreTopologyResource_FailsWithGatewayRecommendation()
    {
        WriteValidProductionConfig(_tempDir);
        WriteTopologyConfig(_tempDir, includeEventStore: false);

        (int exitCode, string output) = await RunValidationAsync(_tempDir);

        exitCode.ShouldBe(1);
        output.ShouldContain("EventStore gateway");
        output.ShouldContain("eventstore");
        output.ShouldContain("topology.yaml");
    }

    [Fact]
    public async Task PartiesMcpEnabledButMissingResource_FailsWithOptionalMcpRecommendation()
    {
        WriteValidProductionConfig(_tempDir);
        WriteTopologyConfig(_tempDir, includePartiesMcp: false, mcpEnabled: true);

        (int exitCode, string output) = await RunValidationAsync(_tempDir);

        exitCode.ShouldBe(1);
        output.ShouldContain("Optional MCP");
        output.ShouldContain("parties-mcp");
        output.ShouldContain("mcpEnabled");
    }

    [Fact]
    public async Task DefaultActionAllow_FailsWithRecommendation()
    {
        WriteValidProductionConfig(_tempDir);
        WriteInsecureAccessControl(_tempDir);

        (int exitCode, string output) = await RunValidationAsync(_tempDir);

        exitCode.ShouldBe(1);
        output.ShouldContain("allow");
        output.ShouldContain("deny");
    }

    [Fact]
    public async Task HardcodedConnectionString_FailsWithRecommendation()
    {
        WriteValidProductionConfig(_tempDir);
        WriteHardcodedStateStore(_tempDir);

        (int exitCode, string output) = await RunValidationAsync(_tempDir);

        exitCode.ShouldBe(1);
        output.ShouldContain("hardcoded");
    }

    [Fact]
    public async Task MissingStateStoreScopes_FailsWithRecommendation()
    {
        WriteValidProductionConfig(_tempDir);
        WriteUnscopedStateStore(_tempDir);

        (int exitCode, string output) = await RunValidationAsync(_tempDir);

        exitCode.ShouldBe(1);
        output.ShouldContain("required shared topology scopes");
    }

    [Fact]
    public async Task MissingDeadLetterConfig_FailsWithRecommendation()
    {
        WriteValidProductionConfig(_tempDir);
        WritePubSubWithoutDeadLetter(_tempDir);

        (int exitCode, string output) = await RunValidationAsync(_tempDir);

        exitCode.ShouldBe(1);
        output.ShouldContain("enableDeadLetter");
    }

    [Fact]
    public async Task SubscriberInPublishingScopes_WithTopics_FailsWithRecommendation()
    {
        WriteValidProductionConfig(_tempDir);
        WritePubSubWithSubscriberPublishing(_tempDir);

        (int exitCode, string output) = await RunValidationAsync(_tempDir);

        exitCode.ShouldBe(1);
        output.ShouldContain("publish");
    }

    [Fact]
    public async Task MissingResiliencyConfig_FailsWithRecommendation()
    {
        WriteValidProductionConfig(_tempDir);
        File.Delete(Path.Combine(_tempDir, "resiliency.yaml"));

        (int exitCode, string output) = await RunValidationAsync(_tempDir);

        exitCode.ShouldBe(1);
        output.ShouldContain("resiliency.yaml not found");
    }

    [Fact]
    public async Task JsonOutputMode_ProducesValidJson()
    {
        WriteValidProductionConfig(_tempDir);

        (int exitCode, string output) = await RunValidationAsync(_tempDir, jsonOutput: true);

        exitCode.ShouldBe(0);

        // Verify it's valid JSON
        JsonDocument? doc = null;
        Should.NotThrow(() => doc = JsonDocument.Parse(output));
        doc.ShouldNotBeNull();

        JsonElement root = doc.RootElement;
        root.TryGetProperty("summary", out JsonElement summary).ShouldBeTrue();
        summary.GetProperty("result").GetString().ShouldBe("PASS");
        summary.GetProperty("total").GetInt32().ShouldBeGreaterThan(0);
        summary.GetProperty("passed").GetInt32().ShouldBeGreaterThan(0);
        summary.GetProperty("failed").GetInt32().ShouldBe(0);

        root.TryGetProperty("checks", out JsonElement checks).ShouldBeTrue();
        checks.GetArrayLength().ShouldBeGreaterThan(0);
        checks.EnumerateArray().Any(check =>
            check.GetProperty("category").GetString() == "Tenants Integration").ShouldBeTrue();
    }

    [Fact]
    public async Task MissingTenantsSubscription_FailsWithSpecificRecommendation()
    {
        WriteValidProductionConfig(_tempDir);
        File.Delete(Path.Combine(_tempDir, "subscription-tenants.yaml"));

        (int exitCode, string output) = await RunValidationAsync(_tempDir);

        exitCode.ShouldBe(1);
        output.ShouldContain("No Tenants subscription");
        output.ShouldContain("system.tenants.events");
        output.ShouldContain("local tenant projection");
    }

    [Fact]
    public async Task MissingAuthenticationSettings_FailClosedValidationBlocksProduction()
    {
        WriteValidProductionConfig(_tempDir);
        WriteTopologyConfig(_tempDir, includeAuthentication: false);

        (int exitCode, string output) = await RunValidationAsync(_tempDir);

        exitCode.ShouldBe(1);
        output.ShouldContain("Authentication");
        output.ShouldContain("jwtIssuer");
        output.ShouldContain("failClosed");
    }

    [Fact]
    public async Task MissingTenantIdentitySettings_BlockProductionWithAuthenticatedCredentialGuidance()
    {
        WriteValidProductionConfig(_tempDir);
        WriteTenantsIntegrationConfig(_tempDir, includeTenantSecurity: false);

        (int exitCode, string output) = await RunValidationAsync(_tempDir);

        exitCode.ShouldBe(1);
        output.ShouldContain("Tenant identity configuration is missing or unsafe");
        output.ShouldContain("authenticated credentials");
        output.ShouldContain("not request payloads");
    }

    [Fact]
    public async Task UnsafeProductionTransportSettings_BlockProductionWithTlsGuidance()
    {
        WriteValidProductionConfig(_tempDir);
        WriteTopologyConfig(_tempDir, httpsRequired: false, daprMtlsRequired: false, localDevelopmentHttpAllowed: true);

        (int exitCode, string output) = await RunValidationAsync(_tempDir);

        exitCode.ShouldBe(1);
        output.ShouldContain("Transport");
        output.ShouldContain("HTTPS/TLS");
        output.ShouldContain("DAPR sidecar mTLS");
    }

    [Fact]
    public async Task MalformedTenantsConfig_FailsWithoutSecretData()
    {
        WriteValidProductionConfig(_tempDir);
        File.WriteAllText(Path.Combine(_tempDir, "tenants-integration.yaml"), """
            apiVersion: hexalith.io/v1
            kind: TenantsIntegration
            metadata:
              name: parties-tenants
            spec:
              pubsubName: ""
              topicName: ""
              commandApiAppId: ""
              tenantIdentitySource: requestPayload
              allowTenantFromPayload: true
              metadataRequired: false
            """);

        (int exitCode, string output) = await RunValidationAsync(_tempDir);

        exitCode.ShouldBe(1);
        output.ShouldContain("Missing or malformed Tenants config values");
        output.ShouldContain("commandApiAppId=eventstore");
        output.ShouldNotContain("token");
        output.ShouldNotContain("membership");
    }

    [Fact]
    public async Task MissingPartiesTenantsSubscriptionScope_FailsWhenProductionScopesArePresent()
    {
        WriteValidProductionConfig(_tempDir);
        WritePubSubWithoutTenantsPartiesScope(_tempDir);

        (int exitCode, string output) = await RunValidationAsync(_tempDir);

        exitCode.ShouldBe(1);
        output.ShouldContain("Missing parties subscription permission");
        output.ShouldContain("system.tenants.events");
    }

    [Fact]
    public async Task UnhealthyTenantsDependencySignal_FailsWithActionableImpact()
    {
        WriteValidProductionConfig(_tempDir);
        File.WriteAllText(Path.Combine(_tempDir, "tenants-integration.yaml"), """
            apiVersion: hexalith.io/v1
            kind: TenantsIntegration
            metadata:
              name: parties-tenants
            spec:
              pubsubName: pubsub
              topicName: system.tenants.events
              commandApiAppId: eventstore
              tenantsDependencyHealth: unhealthy
            """);

        (int exitCode, string output) = await RunValidationAsync(_tempDir);

        exitCode.ShouldBe(1);
        output.ShouldContain("Tenants dependency signal is unhealthy");
        output.ShouldContain("fails closed");
    }

    [Fact]
      public async Task GnuStyleArguments_AreAccepted()
      {
        WriteValidProductionConfig(_tempDir);

        (int exitCode, string output) = await RunValidationAsync(_tempDir, jsonOutput: true, useGnuStyleArguments: true);

        exitCode.ShouldBe(0, $"Expected GNU-style arguments to work. Output:\n{output}");
        output.ShouldContain("\"result\": \"PASS\"");
      }

      [Fact]
      public async Task LocalDevConfig_PassesWithWarnings()
    {
        WriteLocalDevConfig(_tempDir);

        (int exitCode, string output) = await RunValidationAsync(_tempDir);

        exitCode.ShouldBe(0, $"Expected local dev profile to pass with warnings. Output:\n{output}");
        output.ShouldContain("WARN");
        output.ShouldContain("allow");
        output.ShouldContain("public");
    }

      [Fact]
      public async Task WildcardAppId_FailsWithRecommendation()
      {
        WriteValidProductionConfig(_tempDir);
        WriteWildcardAccessControl(_tempDir);

        (int exitCode, string output) = await RunValidationAsync(_tempDir);

        exitCode.ShouldBe(1);
        output.ShouldContain("Wildcard");
        output.ShouldContain("explicit known callers");
      }

      [Fact]
      public async Task MissingComponentResiliencyTargets_FailsWithRecommendation()
      {
        WriteValidProductionConfig(_tempDir);
        WriteResiliencyWithoutComponentTargets(_tempDir);

        (int exitCode, string output) = await RunValidationAsync(_tempDir);

        exitCode.ShouldBe(1);
        output.ShouldContain("Pub/sub inbound/outbound resiliency targets are missing");
        output.ShouldContain("State store resiliency target is missing");
      }

    [Fact]
    public async Task SecretStoreWarning_IsAdvisoryNotBlocking()
    {
        WriteValidProductionConfig(_tempDir);
        // No secretstore file — should warn but still pass

        (int exitCode, string output) = await RunValidationAsync(_tempDir);

        exitCode.ShouldBe(0, $"Secret store warning should not cause failure. Output:\n{output}");
        output.ShouldContain("secretstore");
    }

    [Fact]
    public async Task TopologyValidation_Output_DoesNotLeakSecretLookingManifestValues()
    {
        string sentinel = $"hexalith-secret-sentinel-{Guid.NewGuid():N}";
        WriteValidProductionConfig(_tempDir);
        WriteTopologyConfig(_tempDir, includeSecretAnnotation: true, secretSentinel: sentinel);

        (int exitCode, string consoleOutput) = await RunValidationAsync(_tempDir);
        (int jsonExitCode, string jsonOutput) = await RunValidationAsync(_tempDir, jsonOutput: true);

        exitCode.ShouldBe(0, consoleOutput);
        jsonExitCode.ShouldBe(0, jsonOutput);
        consoleOutput.ShouldNotContain(sentinel, Case.Insensitive);
        jsonOutput.ShouldNotContain(sentinel, Case.Insensitive);
        consoleOutput.ShouldNotContain("Bearer ", Case.Insensitive);
        jsonOutput.ShouldNotContain("Bearer ", Case.Insensitive);
        consoleOutput.ShouldNotContain("ConnectionString=", Case.Insensitive);
        jsonOutput.ShouldNotContain("ConnectionString=", Case.Insensitive);
    }

    [Fact]
    public async Task TopologyValidationFailure_JsonOutput_DoesNotLeakSecretLookingManifestValues()
    {
        // Pair the passing-case sanitized-output coverage above with a failure-case variant:
        // remove the eventstore resource from topology.yaml so the topology check FAILS, and
        // bake a sentinel into the (still-present) annotations to prove the failure-path
        // formatter also sanitizes operator-supplied secrets in JSON output.
        string sentinel = $"hexalith-failure-sentinel-{Guid.NewGuid():N}";
        WriteValidProductionConfig(_tempDir);
        WriteTopologyConfig(_tempDir, includeEventStore: false, includeSecretAnnotation: true, secretSentinel: sentinel);

        (int consoleExitCode, string consoleOutput) = await RunValidationAsync(_tempDir);
        (int jsonExitCode, string jsonOutput) = await RunValidationAsync(_tempDir, jsonOutput: true);

        consoleExitCode.ShouldNotBe(0, $"Missing eventstore in topology should fail validation. Output:\n{consoleOutput}");
        jsonExitCode.ShouldNotBe(0, $"Missing eventstore in topology should fail JSON validation. Output:\n{jsonOutput}");
        consoleOutput.ShouldContain("EventStore gateway", Case.Insensitive);
        jsonOutput.ShouldContain("EventStore gateway", Case.Insensitive);
        consoleOutput.ShouldNotContain(sentinel, Case.Insensitive);
        jsonOutput.ShouldNotContain(sentinel, Case.Insensitive);
        consoleOutput.ShouldNotContain("Bearer ", Case.Insensitive);
        jsonOutput.ShouldNotContain("Bearer ", Case.Insensitive);
        consoleOutput.ShouldNotContain("ConnectionString=", Case.Insensitive);
        jsonOutput.ShouldNotContain("ConnectionString=", Case.Insensitive);
    }

    [Fact]
    public async Task DeploymentSecurityValidation_RedactsAuthTenantAndPersonalDataValues()
    {
        string sentinel = $"hexalith-claims-membership-personal-{Guid.NewGuid():N}";
        WriteValidProductionConfig(_tempDir);
        WriteTopologyConfig(
            _tempDir,
            failClosed: false,
            jwtIssuer: $"Bearer {sentinel}",
            jwtAudience: $"claims:{sentinel}",
            signingKeySecretName: sentinel,
            signingKeySecretKey: sentinel);
        WriteTenantsIntegrationConfig(
            _tempDir,
            includeTenantSecurity: false,
            unsafeTenantDiagnosticValue: sentinel);

        (int exitCode, string consoleOutput) = await RunValidationAsync(_tempDir);
        (int jsonExitCode, string jsonOutput) = await RunValidationAsync(_tempDir, jsonOutput: true);

        exitCode.ShouldBe(1);
        jsonExitCode.ShouldBe(1);
        consoleOutput.ShouldContain("Authentication");
        consoleOutput.ShouldContain("Tenant identity configuration is missing or unsafe");
        consoleOutput.ShouldNotContain(sentinel, Case.Insensitive);
        jsonOutput.ShouldNotContain(sentinel, Case.Insensitive);
        consoleOutput.ShouldNotContain("Bearer ", Case.Insensitive);
        jsonOutput.ShouldNotContain("Bearer ", Case.Insensitive);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing && Directory.Exists(_tempDir))
            {
                try
                {
                    Directory.Delete(_tempDir, recursive: true);
                }
                catch (IOException)
                {
                    // Best effort cleanup
                }
            }

            _disposed = true;
        }
    }

    private static string? FindSolutionDirectory()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.GetFiles(dir, "*.slnx").Length > 0)
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        return null;
    }

    private static void WriteValidProductionConfig(string dir)
    {
        File.WriteAllText(Path.Combine(dir, "accesscontrol.yaml"), """
            apiVersion: dapr.io/v1alpha1
            kind: Configuration
            metadata:
              name: accesscontrol
            spec:
              accessControl:
                defaultAction: deny
                trustDomain: "{env:DAPR_TRUST_DOMAIN|hexalith.io}"
                policies:
                  - appId: eventstore-admin
                    defaultAction: deny
                    trustDomain: "{env:DAPR_TRUST_DOMAIN|hexalith.io}"
                    namespace: "{env:DAPR_NAMESPACE|hexalith}"
                    operations:
                      - name: /**
                        httpVerb: ['GET', 'POST', 'PUT']
                        action: allow
                  - appId: tenants
                    defaultAction: deny
                    trustDomain: "{env:DAPR_TRUST_DOMAIN|hexalith.io}"
                    namespace: "{env:DAPR_NAMESPACE|hexalith}"
                    operations:
                      - name: /**
                        httpVerb: ['POST']
                        action: allow
                  - appId: parties
                    defaultAction: deny
                    trustDomain: "{env:DAPR_TRUST_DOMAIN|hexalith.io}"
                    namespace: "{env:DAPR_NAMESPACE|hexalith}"
                    operations:
                      - name: /**
                        httpVerb: ['POST']
                        action: allow
            """);

        WriteSplitAccessControlFiles(dir);
        WriteTopologyConfig(dir);

        File.WriteAllText(Path.Combine(dir, "statestore-cosmosdb.yaml"), """
            apiVersion: dapr.io/v1alpha1
            kind: Component
            metadata:
              name: statestore
            spec:
              type: state.azure.cosmosdb
              version: v1
              metadata:
                - name: url
                  value: "{env:COSMOSDB_URL}"
                - name: masterKey
                  value: "{env:COSMOSDB_MASTER_KEY}"
                - name: database
                  value: "{env:COSMOSDB_DATABASE|dapr}"
                - name: collection
                  value: "{env:COSMOSDB_COLLECTION|state}"
                - name: actorStateStore
                  value: "true"
                - name: keyPrefix
                  value: "none"
            scopes:
              - eventstore
              - eventstore-admin
              - parties
              - tenants
            """);

        File.WriteAllText(Path.Combine(dir, "pubsub-kafka.yaml"), """
            apiVersion: dapr.io/v1alpha1
            kind: Component
            metadata:
              name: pubsub
            spec:
              type: pubsub.kafka
              version: v1
              metadata:
                - name: brokers
                  value: "{env:KAFKA_BROKERS}"
                - name: authType
                  value: "{env:KAFKA_AUTH_TYPE}"
                - name: enableDeadLetter
                  value: "true"
                - name: publishingScopes
                  value: "tenants=system.tenants.events;{env:SUBSCRIBER_APP_ID}="
                - name: subscriptionScopes
                  value: "parties=system.tenants.events;{env:SUBSCRIBER_APP_ID}=acme.parties.events"
            scopes:
              - eventstore
              - parties
              - tenants
              - "{env:SUBSCRIBER_APP_ID}"
            """);

        File.WriteAllText(Path.Combine(dir, "subscription-parties.yaml"), """
            apiVersion: dapr.io/v2alpha1
            kind: Subscription
            metadata:
              name: parties-events-sample-tenant
            spec:
              pubsubname: pubsub
              topic: "sample-tenant.parties.events"
              routes:
                default: /events/parties
              deadLetterTopic: "deadletter.sample-tenant.parties.events"
            scopes:
              - "{env:SUBSCRIBER_APP_ID}"
            """);

        File.WriteAllText(Path.Combine(dir, "subscription-tenants.yaml"), """
            apiVersion: dapr.io/v2alpha1
            kind: Subscription
            metadata:
              name: tenants-events-parties
            spec:
              pubsubname: pubsub
              topic: "system.tenants.events"
              routes:
                default: /events/tenants
              deadLetterTopic: "deadletter.system.tenants.events"
            scopes:
              - parties
            """);

        WriteTenantsIntegrationConfig(dir);

        File.WriteAllText(Path.Combine(dir, "resiliency.yaml"), """
            apiVersion: dapr.io/v1alpha1
            kind: Resiliency
            metadata:
              name: resiliency
            spec:
              policies:
                retries:
                  defaultRetry:
                    policy: exponential
                    maxInterval: 15s
                    maxRetries: 10
                circuitBreakers:
                  defaultBreaker:
                    maxRequests: 1
                    interval: 60s
                    timeout: 60s
                    trip: consecutiveFailures > 5
              targets:
                components:
                  pubsub:
                    outbound:
                      retry: defaultRetry
                      circuitBreaker: defaultBreaker
                    inbound:
                      retry: defaultRetry
                      circuitBreaker: defaultBreaker
                  statestore:
                    retry: defaultRetry
                    circuitBreaker: defaultBreaker
            """);
    }

    private static void WriteSplitAccessControlFiles(string dir)
    {
        File.WriteAllText(Path.Combine(dir, "accesscontrol.parties.yaml"), """
            apiVersion: dapr.io/v1alpha1
            kind: Configuration
            metadata:
              name: accesscontrol-parties
            spec:
              accessControl:
                defaultAction: deny
                trustDomain: "{env:DAPR_TRUST_DOMAIN|hexalith.io}"
                policies:
                  - appId: eventstore
                    defaultAction: deny
                    trustDomain: "{env:DAPR_TRUST_DOMAIN|hexalith.io}"
                    namespace: "{env:DAPR_NAMESPACE|hexalith}"
                    operations:
                      - name: /process
                        httpVerb: ['POST']
                        action: allow
            """);

        File.WriteAllText(Path.Combine(dir, "accesscontrol.tenants.yaml"), """
            apiVersion: dapr.io/v1alpha1
            kind: Configuration
            metadata:
              name: accesscontrol-tenants
            spec:
              accessControl:
                defaultAction: deny
                trustDomain: "{env:DAPR_TRUST_DOMAIN|hexalith.io}"
                policies:
                  - appId: parties
                    defaultAction: deny
                    trustDomain: "{env:DAPR_TRUST_DOMAIN|hexalith.io}"
                    namespace: "{env:DAPR_NAMESPACE|hexalith}"
                    operations:
                      - name: /ready
                        httpVerb: ['GET']
                        action: allow
            """);

        File.WriteAllText(Path.Combine(dir, "accesscontrol.eventstore-admin.yaml"), """
            apiVersion: dapr.io/v1alpha1
            kind: Configuration
            metadata:
              name: accesscontrol-eventstore-admin
            spec:
              accessControl:
                defaultAction: deny
                trustDomain: "{env:DAPR_TRUST_DOMAIN|hexalith.io}"
                policies: []
            """);
    }

    private static void WriteTopologyConfig(
        string dir,
        bool includeEventStore = true,
        bool includeEventStoreAdmin = true,
        bool includeEventStoreAdminUi = true,
        bool includeParties = true,
        bool includeTenants = true,
        bool includePartiesMcp = true,
        bool mcpEnabled = false,
        bool includeSecretAnnotation = false,
        string? secretSentinel = null,
        bool includeAuthentication = true,
        bool failClosed = true,
        string jwtIssuer = "{env:AUTHENTICATION_JWTBEARER_ISSUER}",
        string jwtAudience = "{env:AUTHENTICATION_JWTBEARER_AUDIENCE}",
        string signingKeySecretName = "hexalith-jwt-signing",
        string signingKeySecretKey = "Authentication__JwtBearer__SigningKey",
        bool httpsRequired = true,
        bool daprMtlsRequired = true,
        bool localDevelopmentHttpAllowed = false)
    {
        List<string> appIds = [];
        if (includeEventStore) appIds.Add("eventstore");
        if (includeEventStoreAdmin) appIds.Add("eventstore-admin");
        if (includeEventStoreAdminUi) appIds.Add("eventstore-admin-ui");
        if (includeParties) appIds.Add("parties");
        if (includeTenants) appIds.Add("tenants");
        if (includePartiesMcp) appIds.Add("parties-mcp");

        string appIdLines = string.Join(Environment.NewLine, appIds.Select(appId => $"    - {appId}"));
        string annotation = includeSecretAnnotation
            ? $"""
              annotations:
                diagnostic: "Bearer {secretSentinel} ConnectionString=Server=example;Password={secretSentinel}"
            """
            : string.Empty;
        string authentication = includeAuthentication
            ? $$"""
                  authentication:
                    jwtIssuer: "{{jwtIssuer}}"
                    jwtAudience: "{{jwtAudience}}"
                    signingKeySecretName: {{signingKeySecretName}}
                    signingKeySecretKey: {{signingKeySecretKey}}
                    failClosed: {{failClosed.ToString().ToLowerInvariant()}}
            """
            : string.Empty;

        File.WriteAllText(Path.Combine(dir, "topology.yaml"), $$"""
            apiVersion: hexalith.io/v1
            kind: PartiesTopology
            metadata:
              name: parties-eventstore-fronted
            {{annotation}}
            spec:
              appIds:
            {{appIdLines}}
              mcpEnabled: {{mcpEnabled.ToString().ToLowerInvariant()}}
              eventStoreAdminUi:
                adminServerAppId: eventstore-admin
              domainServices:
                - key: "*|party|v1"
                  appId: parties
                  methodName: process
                  domain: party
              deploymentSecurity:
            {{authentication}}
                transport:
                  httpsRequired: {{httpsRequired.ToString().ToLowerInvariant()}}
                  daprMtlsRequired: {{daprMtlsRequired.ToString().ToLowerInvariant()}}
                  localDevelopmentHttpAllowed: {{localDevelopmentHttpAllowed.ToString().ToLowerInvariant()}}
            """);
    }

    private static void WriteTenantsIntegrationConfig(
        string dir,
        bool includeTenantSecurity = true,
        string? unsafeTenantDiagnosticValue = null)
    {
        string tenantSecurity = includeTenantSecurity
            ? """
              tenantIdentitySource: authenticatedCredentials
              allowTenantFromPayload: false
              metadataRequired: true
            """
            : """
              tenantIdentitySource: requestPayload
              allowTenantFromPayload: true
              metadataRequired: false
            """;
        string diagnostic = unsafeTenantDiagnosticValue is null
            ? string.Empty
            : $"""
              tenantMembershipPayload: "{unsafeTenantDiagnosticValue}"
            """;

        File.WriteAllText(Path.Combine(dir, "tenants-integration.yaml"), $$"""
            apiVersion: hexalith.io/v1
            kind: TenantsIntegration
            metadata:
              name: parties-tenants
            spec:
              pubsubName: pubsub
              topicName: system.tenants.events
              commandApiAppId: eventstore
              tenantsDependencyHealth: healthy
            {{tenantSecurity}}
            {{diagnostic}}
            """);
    }

    private static void WriteInsecureAccessControl(string dir)
    {
        File.WriteAllText(Path.Combine(dir, "accesscontrol.yaml"), """
            apiVersion: dapr.io/v1alpha1
            kind: Configuration
            metadata:
              name: accesscontrol
            spec:
              accessControl:
                defaultAction: allow
                trustDomain: "hexalith.io"
                policies:
                  - appId: parties
                    defaultAction: deny
                    trustDomain: "hexalith.io"
                    namespace: "hexalith"
                    operations:
                      - name: /**
                        httpVerb: ['POST']
                        action: allow
            """);
    }

    private static void WriteWildcardAccessControl(string dir)
    {
        File.WriteAllText(Path.Combine(dir, "accesscontrol.yaml"), """
            apiVersion: dapr.io/v1alpha1
            kind: Configuration
            metadata:
              name: accesscontrol
            spec:
              accessControl:
                defaultAction: deny
                trustDomain: "{env:DAPR_TRUST_DOMAIN|hexalith.io}"
                policies:
                  - appId: "*"
                    defaultAction: deny
                    trustDomain: "{env:DAPR_TRUST_DOMAIN|hexalith.io}"
                    namespace: "{env:DAPR_NAMESPACE|hexalith}"
                    operations:
                      - name: /**
                        httpVerb: ['POST']
                        action: allow
            """);
    }

    private static void WriteHardcodedStateStore(string dir)
    {
        File.WriteAllText(Path.Combine(dir, "statestore-cosmosdb.yaml"), """
            apiVersion: dapr.io/v1alpha1
            kind: Component
            metadata:
              name: statestore
            spec:
              type: state.azure.cosmosdb
              version: v1
              metadata:
                - name: url
                  value: "https://myaccount.documents.azure.com:443/"
                - name: masterKey
                  value: "supersecretkey123"
                - name: actorStateStore
                  value: "true"
            scopes:
              - parties
            """);
    }

    private static void WriteUnscopedStateStore(string dir)
    {
        File.WriteAllText(Path.Combine(dir, "statestore-cosmosdb.yaml"), """
            apiVersion: dapr.io/v1alpha1
            kind: Component
            metadata:
              name: statestore
            spec:
              type: state.azure.cosmosdb
              version: v1
              metadata:
                - name: url
                  value: "{env:COSMOSDB_URL}"
                - name: masterKey
                  value: "{env:COSMOSDB_MASTER_KEY}"
                - name: actorStateStore
                  value: "true"
            """);
    }

    private static void WritePubSubWithoutDeadLetter(string dir)
    {
        File.WriteAllText(Path.Combine(dir, "pubsub-kafka.yaml"), """
            apiVersion: dapr.io/v1alpha1
            kind: Component
            metadata:
              name: pubsub
            spec:
              type: pubsub.kafka
              version: v1
              metadata:
                - name: brokers
                  value: "{env:KAFKA_BROKERS}"
                - name: publishingScopes
                  value: "{env:SUBSCRIBER_APP_ID}="
                - name: subscriptionScopes
                  value: "{env:SUBSCRIBER_APP_ID}=acme.parties.events"
            scopes:
              - parties
              - "{env:SUBSCRIBER_APP_ID}"
            """);
    }

    private static void WritePubSubWithSubscriberPublishing(string dir)
    {
        File.WriteAllText(Path.Combine(dir, "pubsub-kafka.yaml"), """
            apiVersion: dapr.io/v1alpha1
            kind: Component
            metadata:
              name: pubsub
            spec:
              type: pubsub.kafka
              version: v1
              metadata:
                - name: brokers
                  value: "{env:KAFKA_BROKERS}"
                - name: enableDeadLetter
                  value: "true"
                - name: publishingScopes
                  value: "{env:SUBSCRIBER_APP_ID}=acme.parties.events"
                - name: subscriptionScopes
                  value: "{env:SUBSCRIBER_APP_ID}=acme.parties.events"
            scopes:
              - parties
              - "{env:SUBSCRIBER_APP_ID}"
            """);
    }

    private static void WritePubSubWithoutTenantsPartiesScope(string dir)
    {
        File.WriteAllText(Path.Combine(dir, "pubsub-kafka.yaml"), """
            apiVersion: dapr.io/v1alpha1
            kind: Component
            metadata:
              name: pubsub
            spec:
              type: pubsub.kafka
              version: v1
              metadata:
                - name: brokers
                  value: "{env:KAFKA_BROKERS}"
                - name: enableDeadLetter
                  value: "true"
                - name: publishingScopes
                  value: "{env:SUBSCRIBER_APP_ID}="
                - name: subscriptionScopes
                  value: "{env:SUBSCRIBER_APP_ID}=acme.parties.events"
            scopes:
              - parties
              - "{env:SUBSCRIBER_APP_ID}"
            """);
    }

    private static void WriteLocalDevConfig(string dir)
    {
        File.WriteAllText(Path.Combine(dir, "accesscontrol.yaml"), """
            apiVersion: dapr.io/v1alpha1
            kind: Configuration
            metadata:
              name: accesscontrol
            spec:
              accessControl:
                defaultAction: allow
                trustDomain: "public"
                policies:
                  - appId: parties
                    defaultAction: deny
                    trustDomain: "public"
                    namespace: "default"
                    operations:
                      - name: /**
                        httpVerb: ['POST']
                        action: allow
            """);

        File.WriteAllText(Path.Combine(dir, "statestore.yaml"), """
            apiVersion: dapr.io/v1alpha1
            kind: Component
            metadata:
              name: statestore
            spec:
              type: state.redis
              version: v1
              metadata:
                - name: redisHost
                  value: "localhost:6379"
                - name: actorStateStore
                  value: "true"
            scopes:
              - parties
            """);

        File.WriteAllText(Path.Combine(dir, "pubsub.yaml"), """
            apiVersion: dapr.io/v1alpha1
            kind: Component
            metadata:
              name: pubsub
            spec:
              type: pubsub.redis
              version: v1
              metadata:
                - name: redisHost
                  value: "localhost:6379"
            scopes:
              - parties
              - sample
            """);

        File.WriteAllText(Path.Combine(dir, "subscription-parties.yaml"), """
            apiVersion: dapr.io/v2alpha1
            kind: Subscription
            metadata:
              name: parties-events-sample
            spec:
              pubsubname: pubsub
              topic: "sample.parties.events"
              routes:
                default: /events/parties
              deadLetterTopic: "deadletter.sample.parties.events"
            scopes:
              - sample
            """);

        File.WriteAllText(Path.Combine(dir, "resiliency.yaml"), """
            apiVersion: dapr.io/v1alpha1
            kind: Resiliency
            metadata:
              name: resiliency
            spec:
              policies:
                retries:
                  defaultRetry:
                    policy: exponential
                    maxRetries: 10
                circuitBreakers:
                  defaultBreaker:
                    maxRequests: 1
                    interval: 60s
                    timeout: 60s
                    trip: consecutiveFailures > 5
            """);
    }

    private static void WriteResiliencyWithoutComponentTargets(string dir)
    {
        File.WriteAllText(Path.Combine(dir, "resiliency.yaml"), """
            apiVersion: dapr.io/v1alpha1
            kind: Resiliency
            metadata:
              name: resiliency
            spec:
              policies:
                retries:
                  defaultRetry:
                    policy: exponential
                    maxRetries: 10
                circuitBreakers:
                  defaultBreaker:
                    maxRequests: 1
                    interval: 60s
                    timeout: 60s
                    trip: consecutiveFailures > 5
              targets:
                apps:
                  parties:
                    retry: defaultRetry
            """);
    }

    private async Task<(int ExitCode, string Output)> RunValidationAsync(
        string configPath,
        bool jsonOutput = false,
        bool useGnuStyleArguments = false)
    {
        string powershellExe = FindPowerShell();
        string args = useGnuStyleArguments
            ? $"-NoProfile -ExecutionPolicy Bypass -File \"{_scriptPath}\" --config-path \"{configPath}\""
            : $"-NoProfile -ExecutionPolicy Bypass -File \"{_scriptPath}\" -ConfigPath \"{configPath}\"";
        if (jsonOutput)
        {
            args += useGnuStyleArguments ? " --output json" : " -Output json";
        }

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = powershellExe,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        process.Start();
        string stdout = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        string stderr = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);

        string combinedOutput = stdout;
        if (!string.IsNullOrWhiteSpace(stderr))
        {
            combinedOutput += "\nSTDERR:\n" + stderr;
        }

        return (process.ExitCode, combinedOutput);
    }

    private static string FindPowerShell()
    {
        // Prefer pwsh (PowerShell 7+), fall back to powershell.exe (Windows 5.1)
        string[] candidates =
        [
            "pwsh",
            "pwsh.exe",
            "powershell",
            "powershell.exe",
        ];

        foreach (string candidate in candidates)
        {
            try
            {
                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = candidate,
                    Arguments = "-NoProfile -Command \"exit 0\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                process.Start();
                process.WaitForExit(5000);
                if (process.ExitCode == 0)
                {
                    return candidate;
                }
            }
            catch
            {
                continue;
            }
        }

        throw new InvalidOperationException(
            "PowerShell not found. Install PowerShell 7+ (pwsh) or ensure Windows PowerShell (powershell.exe) is available.");
    }
}
