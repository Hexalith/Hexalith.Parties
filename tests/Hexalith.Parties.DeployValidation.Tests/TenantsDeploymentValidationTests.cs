namespace Hexalith.Parties.DeployValidation.Tests;

using System.Diagnostics;
using System.Text.Json;

[Collection("DeployValidation")]
public sealed class TenantsDeploymentValidationTests : IDisposable
{
    private readonly string _scriptPath;
    private readonly string _tempDir;
    private bool _disposed;

    public TenantsDeploymentValidationTests()
    {
        string? solutionDir = FindSolutionDirectory();
        solutionDir.ShouldNotBeNull("Could not find solution directory");
        _scriptPath = Path.Combine(solutionDir, "deploy", "validate-deployment.ps1");
        File.Exists(_scriptPath).ShouldBeTrue($"Validation script not found at {_scriptPath}");

        _tempDir = Path.Combine(Path.GetTempPath(), $"deploy-validation-tenants-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task TenantsSubscription_Missing_FailsWithSpecificErrorAsync()
    {
        WriteBaseProductionConfig(_tempDir, includeTenantsSubscription: false, includeTenantsConfig: true);

        (int exitCode, string output) = await RunValidationAsync(_tempDir);

        exitCode.ShouldBe(1);
        output.ShouldContain("No Tenants subscription");
        output.ShouldContain("system.tenants.events");
    }

    [Fact]
    public async Task TenantsSubscriptionScopes_MissingCommandApi_FailsWithRecommendationAsync()
    {
        WriteBaseProductionConfig(_tempDir, includeTenantsSubscription: true, includeTenantsConfig: true, includeCommandApiScope: false);

        (int exitCode, string output) = await RunValidationAsync(_tempDir);

        exitCode.ShouldBe(1);
        output.ShouldContain("Missing commandapi subscription permission");
        output.ShouldContain("subscriptionScopes");
    }

    [Fact]
    public async Task TenantsConfiguration_Missing_FailsWithRecommendationAsync()
    {
        WriteBaseProductionConfig(_tempDir, includeTenantsSubscription: true, includeTenantsConfig: false);

        (int exitCode, string output) = await RunValidationAsync(_tempDir);

        exitCode.ShouldBe(1);
        output.ShouldContain("No Tenants integration config found");
    }

    [Fact]
    public async Task TenantsConfiguration_Malformed_FailsWithDistinctCategoryAsync()
    {
        WriteBaseProductionConfig(_tempDir, includeTenantsSubscription: true, includeTenantsConfig: true, malformedTenantsConfig: true);

        (int exitCode, string output) = await RunValidationAsync(_tempDir);

        exitCode.ShouldBe(1);
        output.ShouldContain("Tenants configuration values");
        output.ShouldContain("Missing or malformed Tenants config values");
    }

    [Fact]
    public async Task TenantsValidation_JsonOutput_IncludesTenantsChecksInChecksArrayAsync()
    {
        WriteBaseProductionConfig(_tempDir, includeTenantsSubscription: true, includeTenantsConfig: true);

        (int exitCode, string output) = await RunValidationAsync(_tempDir, jsonOutput: true);

        exitCode.ShouldBe(0);
        using JsonDocument doc = JsonDocument.Parse(output);
        JsonElement checks = doc.RootElement.GetProperty("checks");
        checks.EnumerateArray()
            .Any(c => c.GetProperty("category").GetString() == "Tenants Integration")
            .ShouldBeTrue("Expected at least one Tenants Integration entry in checks[]");
    }

    [Fact]
    public async Task TenantsValidation_LocalDev_PassesWithWarningsAsync()
    {
        WriteLocalDevConfig(_tempDir);

        (int exitCode, string output) = await RunValidationAsync(_tempDir);

        exitCode.ShouldBe(0);
        output.ShouldContain("WARN");
        output.ShouldContain("system.tenants.events");
    }

    [Fact]
    public async Task TenantsValidation_Output_DoesNotLeakSecretsOrPiiAsync()
    {
        WriteBaseProductionConfig(_tempDir, includeTenantsSubscription: true, includeTenantsConfig: true, includeSensitiveValues: true);

        (int _, string output) = await RunValidationAsync(_tempDir);

        output.ShouldNotContain("Bearer ", Case.Insensitive);
        output.ShouldNotContain("eventstore:tenant=");
        output.ShouldNotContain("user-1@example.com");
        output.ShouldNotContain("ConnectionString=");
    }

    private async Task<(int exitCode, string output)> RunValidationAsync(string configDir, bool jsonOutput = false)
    {
        ProcessStartInfo psi = new()
        {
            FileName = ResolvePowerShellExecutable(),
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{_scriptPath}\" " +
                        $"-ConfigPath \"{configDir}\"" + (jsonOutput ? " -Output json" : string.Empty),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using Process? process = Process.Start(psi);
        process.ShouldNotBeNull("Unable to start PowerShell to run validation script");
        string stdout = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        string stderr = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);
        return (process.ExitCode, stdout + stderr);
    }

    private static string ResolvePowerShellExecutable() => "pwsh";

    private static string? FindSolutionDirectory()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Hexalith.Parties.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName;
    }

    private static void WriteBaseProductionConfig(
        string dir,
        bool includeTenantsSubscription,
        bool includeTenantsConfig,
        bool includeCommandApiScope = true,
        bool malformedTenantsConfig = false,
        bool includeSensitiveValues = false)
    {
        WriteCommonProductionFiles(dir, includeCommandApiScope);
        if (includeTenantsSubscription)
        {
            WriteTenantsSubscription(dir);
        }

        if (includeTenantsConfig)
        {
            WriteTenantsConfig(dir, malformedTenantsConfig, includeSensitiveValues);
        }
    }

    private static void WriteCommonProductionFiles(string dir, bool includeCommandApiScope)
    {
        File.WriteAllText(Path.Combine(dir, "accesscontrol.yaml"), """
            apiVersion: dapr.io/v1alpha1
            kind: Configuration
            spec:
              accessControl:
                defaultAction: deny
                trustDomain: "hexalith.io"
                policies:
                  - appId: commandapi
                    defaultAction: deny
            """);

        File.WriteAllText(Path.Combine(dir, "statestore.yaml"), """
            apiVersion: dapr.io/v1alpha1
            kind: Component
            metadata:
              name: statestore
            spec:
              type: state.azure.cosmosdb
              metadata:
                - name: url
                  value: "{env:COSMOSDB_URL}"
                - name: masterKey
                  value: "{env:COSMOSDB_MASTER_KEY}"
                - name: actorStateStore
                  value: "true"
            scopes:
              - commandapi
            """);

        string scopes = includeCommandApiScope
            ? "commandapi=system.tenants.events;{env:SUBSCRIBER_APP_ID}=acme.parties.events"
            : "{env:SUBSCRIBER_APP_ID}=acme.parties.events";
        File.WriteAllText(Path.Combine(dir, "pubsub.yaml"), $$"""
            apiVersion: dapr.io/v1alpha1
            kind: Component
            metadata:
              name: pubsub
            spec:
              type: pubsub.kafka
              metadata:
                - name: brokers
                  value: "{env:KAFKA_BROKERS}"
                - name: enableDeadLetter
                  value: "true"
                - name: publishingScopes
                  value: "{env:SUBSCRIBER_APP_ID}="
                - name: subscriptionScopes
                  value: "{{scopes}}"
            scopes:
              - commandapi
              - "{env:SUBSCRIBER_APP_ID}"
            """);

        File.WriteAllText(Path.Combine(dir, "subscription-parties.yaml"), """
            apiVersion: dapr.io/v2alpha1
            kind: Subscription
            spec:
              pubsubname: pubsub
              topic: "sample.parties.events"
              routes:
                default: /events/parties
              deadLetterTopic: "deadletter.sample.parties.events"
            scopes:
              - "{env:SUBSCRIBER_APP_ID}"
            """);

        File.WriteAllText(Path.Combine(dir, "resiliency.yaml"), """
            apiVersion: dapr.io/v1alpha1
            kind: Resiliency
            spec:
              policies:
                retries:
                  defaultRetry:
                    policy: exponential
                circuitBreakers:
                  defaultBreaker:
                    maxRequests: 1
              targets:
                components:
                  pubsub:
                    outbound:
                      retry: defaultRetry
                      circuitBreaker: defaultBreaker
                    inbound:
                      retry: defaultRetry
                  statestore:
                    retry: defaultRetry
                    circuitBreaker: defaultBreaker
            """);
    }

    private static void WriteTenantsSubscription(string dir)
        => File.WriteAllText(Path.Combine(dir, "subscription-tenants.yaml"), """
            apiVersion: dapr.io/v2alpha1
            kind: Subscription
            spec:
              pubsubname: pubsub
              topic: "system.tenants.events"
              routes:
                default: /events/tenants
              deadLetterTopic: "deadletter.system.tenants.events"
            scopes:
              - commandapi
            """);

    private static void WriteTenantsConfig(string dir, bool malformed, bool includeSensitiveValues)
        => File.WriteAllText(Path.Combine(dir, "tenants-integration.yaml"), malformed
            ? """
                apiVersion: hexalith.io/v1
                kind: TenantsIntegration
                spec:
                  pubsubName: ""
                  topicName: ""
                  commandApiAppId: ""
                """
            : $$"""
                apiVersion: hexalith.io/v1
                kind: TenantsIntegration
                spec:
                  pubsubName: pubsub
                  topicName: system.tenants.events
                  commandApiAppId: commandapi
                  tenantsDependencyHealth: healthy
                  ignoredDiagnostic: "{{(includeSensitiveValues ? "Bearer secret eventstore:tenant=tenant-a user-1@example.com ConnectionString=secret" : "none")}}"
                """);

    private static void WriteLocalDevConfig(string dir)
    {
        WriteCommonProductionFiles(dir, includeCommandApiScope: false);
        File.WriteAllText(Path.Combine(dir, "accesscontrol.yaml"), """
            apiVersion: dapr.io/v1alpha1
            kind: Configuration
            spec:
              accessControl:
                defaultAction: allow
                trustDomain: "public"
                policies:
                  - appId: commandapi
                    defaultAction: deny
            """);
        File.WriteAllText(Path.Combine(dir, "pubsub.yaml"), """
            apiVersion: dapr.io/v1alpha1
            kind: Component
            metadata:
              name: pubsub
            spec:
              type: pubsub.redis
              metadata:
                - name: redisHost
                  value: "localhost:6379"
            scopes:
              - commandapi
            """);
        WriteTenantsSubscription(dir);
        WriteTenantsConfig(dir, malformed: false, includeSensitiveValues: false);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing && Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, recursive: true); }
            catch { }
        }

        _disposed = true;
    }
}
