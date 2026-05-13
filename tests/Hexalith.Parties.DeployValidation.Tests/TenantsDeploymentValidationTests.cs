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
    public async Task TenantsSubscriptionScopes_MissingParties_FailsWithRecommendationAsync()
    {
        WriteBaseProductionConfig(_tempDir, includeTenantsSubscription: true, includeTenantsConfig: true, includePartiesScope: false);

        (int exitCode, string output) = await RunValidationAsync(_tempDir);

        exitCode.ShouldBe(1);
        output.ShouldContain("Missing parties subscription permission");
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
        output.ShouldContain("No Tenants integration config found");
        output.ShouldContain("No declarative subscription for system.tenants.events found");
        output.ShouldContain("system.tenants.events");
    }

    [Fact]
    public async Task TenantsValidation_QuotedTenantsIntegrationKind_IsRecognizedAsync()
    {
        WriteBaseProductionConfig(_tempDir, includeTenantsSubscription: true, includeTenantsConfig: true);
        string tenantsConfig = Path.Combine(_tempDir, "tenants-integration.yaml");
        File.WriteAllText(
            tenantsConfig,
            File.ReadAllText(tenantsConfig).Replace("kind: TenantsIntegration", "kind: \"TenantsIntegration\"", StringComparison.Ordinal));

        (int exitCode, string output) = await RunValidationAsync(_tempDir);

        exitCode.ShouldBe(0, output);
        output.ShouldNotContain("No Tenants integration config found");
    }

    [Fact]
    public async Task TenantsValidation_SubscriptionScopesWithQuotesAndWhitespace_AllowsPartiesAsync()
    {
        WriteBaseProductionConfig(_tempDir, includeTenantsSubscription: true, includeTenantsConfig: true);
        string pubsub = Path.Combine(_tempDir, "pubsub.yaml");
        File.WriteAllText(
            pubsub,
            File.ReadAllText(pubsub).Replace(
                "value: \"parties=system.tenants.events;{env:SUBSCRIBER_APP_ID}=acme.parties.events\"",
                "value: ' \"parties\" = \"system.tenants.events\" ; {env:SUBSCRIBER_APP_ID}=acme.parties.events '",
                StringComparison.Ordinal));

        (int exitCode, string output) = await RunValidationAsync(_tempDir);

        exitCode.ShouldBe(0, output);
        output.ShouldContain("parties is allowed to subscribe to system.tenants.events");
    }

    [Fact]
    public async Task TenantsValidation_MultiDocumentPubSub_InspectsAllComponentDocumentsAsync()
    {
        WriteBaseProductionConfig(_tempDir, includeTenantsSubscription: true, includeTenantsConfig: true, includePartiesScope: false);
        File.WriteAllText(Path.Combine(_tempDir, "pubsub.yaml"), """
            apiVersion: dapr.io/v1alpha1
            kind: Configuration
            metadata:
              name: unrelated-config
            ---
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
                  value: "parties=system.tenants.events;{env:SUBSCRIBER_APP_ID}=acme.parties.events"
            scopes:
              - parties
              - "{env:SUBSCRIBER_APP_ID}"
            """);

        (int exitCode, string output) = await RunValidationAsync(_tempDir);

        exitCode.ShouldBe(0, output);
        output.ShouldContain("parties is allowed to subscribe to system.tenants.events");
    }

    [Fact]
    public async Task TenantsValidation_Output_DoesNotLeakSecretsOrPiiAsync()
    {
        // Inject a single random GUID sentinel into every operator-supplied field that the
        // validator parses (pubsubName/topicName/commandApiAppId/tenantsDependencyHealth/
        // dependency annotations). If the validator echoes ANY raw operator-supplied value,
        // the sentinel will appear somewhere in the output — proving the secret-safe contract
        // independently of which field a real operator might paste a secret into. Wins over
        // the previous test that only matched 4 hand-picked tokens against a deliberately
        // ignored YAML field.
        string sentinel = $"hexalith-pii-sentinel-{Guid.NewGuid():N}";
        WriteBaseProductionConfigWithSentinel(_tempDir, sentinel);

        (int _, string output) = await RunValidationAsync(_tempDir);

        output.ShouldNotContain(sentinel, Case.Insensitive,
            "validate-deployment.ps1 must not echo operator-supplied YAML values into its output. Output:\n" + output);
        // Keep the legacy literal-token assertions as defence in depth — they exercise the
        // narrower "common secret patterns" contract for completeness.
        output.ShouldNotContain("Bearer ", Case.Insensitive);
        output.ShouldNotContain("eventstore:tenant=");
        output.ShouldNotContain("user-1@example.com");
        output.ShouldNotContain("ConnectionString=");
    }

    /// <summary>
    /// Writes a production config where every operator-supplied tenants field carries the
    /// supplied sentinel (so the validator's "no leak" contract is proved by sentinel absence,
    /// not by hand-picked token strings).
    /// </summary>
    private static void WriteBaseProductionConfigWithSentinel(string dir, string sentinel)
    {
        WriteCommonProductionFiles(dir, includePartiesScope: true);
        File.WriteAllText(Path.Combine(dir, "subscription-tenants.yaml"), $$"""
            apiVersion: dapr.io/v2alpha1
            kind: Subscription
            metadata:
              annotations:
                diagnostic: "Bearer {{sentinel}}"
            spec:
              pubsubname: pubsub
              topic: "system.tenants.events"
              routes:
                default: /events/tenants
              deadLetterTopic: "deadletter.system.tenants.events"
            scopes:
              - parties
            """);
        // Inject the sentinel into multiple fields. The valid-shape spec keys
        // (pubsubName/topicName/commandApiAppId) are kept correct so the validator does
        // not Fail on shape — instead it succeeds, and we assert nothing in its output
        // echoes the sentinel from the annotations or the dependency-health field.
        File.WriteAllText(Path.Combine(dir, "tenants-integration.yaml"), $$"""
            apiVersion: hexalith.io/v1
            kind: TenantsIntegration
            metadata:
              name: parties-tenants
              annotations:
                diagnostic: "Bearer {{sentinel}} eventstore:tenant=tenant-a"
            spec:
              pubsubName: pubsub
              topicName: system.tenants.events
              commandApiAppId: eventstore
              tenantsDependencyHealth: "healthy {{sentinel}}"
            """);
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
        // 2-minute hard cap covers BOTH the stdout/stderr drain AND WaitForExitAsync — a
        // hung pwsh that produces output but never exits would otherwise block ReadToEndAsync
        // before the timeout could fire.
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
        Task<string> stderrTask = process.StandardError.ReadToEndAsync(cts.Token);
        try
        {
            await Task.WhenAll(stdoutTask, stderrTask, process.WaitForExitAsync(cts.Token)).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { /* already exited */ }
            }
            throw new TimeoutException(
                $"PowerShell validation did not complete within {cts.Token}. ConfigPath: {configDir}.");
        }

        return (process.ExitCode, stdoutTask.Result + stderrTask.Result);
    }

    /// <summary>
    /// Prefer PowerShell 7+ ("pwsh") which is cross-platform and matches the script's #requires.
    /// Falls back to Windows PowerShell ("powershell.exe") for environments without PS7 installed.
    /// </summary>
    private static string ResolvePowerShellExecutable()
    {
        if (IsExecutableAvailable("pwsh"))
        {
            return "pwsh";
        }

        if (OperatingSystem.IsWindows() && IsExecutableAvailable("powershell.exe"))
        {
            return "powershell.exe";
        }

        // Fall back to "pwsh" so the test fails with a clear "executable not found" diagnostic
        // rather than silently picking the wrong shell.
        return "pwsh";
    }

    private static bool IsExecutableAvailable(string fileName)
    {
        try
        {
            using Process? process = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = "-NoProfile -Command \"exit 0\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (process is null)
            {
                return false;
            }

            process.WaitForExit(TimeSpan.FromSeconds(5));
            return process.HasExited && process.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

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
        bool includePartiesScope = true,
        bool malformedTenantsConfig = false,
        bool includeSensitiveValues = false)
    {
        WriteCommonProductionFiles(dir, includePartiesScope);
        if (includeTenantsSubscription)
        {
            WriteTenantsSubscription(dir);
        }

        if (includeTenantsConfig)
        {
            WriteTenantsConfig(dir, malformedTenantsConfig, includeSensitiveValues);
        }
    }

    private static void WriteCommonProductionFiles(string dir, bool includePartiesScope)
    {
        File.WriteAllText(Path.Combine(dir, "accesscontrol.yaml"), """
            apiVersion: dapr.io/v1alpha1
            kind: Configuration
            spec:
              accessControl:
                defaultAction: deny
                trustDomain: "hexalith.io"
                policies:
                  - appId: eventstore-admin
                    defaultAction: deny
                  - appId: tenants
                    defaultAction: deny
                  - appId: parties
                    defaultAction: deny
            """);

        File.WriteAllText(Path.Combine(dir, "accesscontrol.parties.yaml"), """
            apiVersion: dapr.io/v1alpha1
            kind: Configuration
            spec:
              accessControl:
                defaultAction: deny
                trustDomain: "hexalith.io"
                policies:
                  - appId: eventstore
                    defaultAction: deny
                    operations:
                      - name: /process
                        httpVerb: ['POST']
                        action: allow
            """);

        File.WriteAllText(Path.Combine(dir, "accesscontrol.tenants.yaml"), """
            apiVersion: dapr.io/v1alpha1
            kind: Configuration
            spec:
              accessControl:
                defaultAction: deny
                trustDomain: "hexalith.io"
                policies:
                  - appId: parties
                    defaultAction: deny
                    operations:
                      - name: /ready
                        httpVerb: ['GET']
                        action: allow
            """);

        File.WriteAllText(Path.Combine(dir, "accesscontrol.eventstore-admin.yaml"), """
            apiVersion: dapr.io/v1alpha1
            kind: Configuration
            spec:
              accessControl:
                defaultAction: deny
                trustDomain: "hexalith.io"
                policies: []
            """);

        File.WriteAllText(Path.Combine(dir, "topology.yaml"), """
            apiVersion: hexalith.io/v1
            kind: PartiesTopology
            spec:
              appIds:
                - eventstore
                - eventstore-admin
                - eventstore-admin-ui
                - parties
                - tenants
                - parties-mcp
              mcpEnabled: false
              eventStoreAdminUi:
                adminServerAppId: eventstore-admin
              domainServices:
                - key: "*|party|v1"
                  appId: parties
                  methodName: process
                  domain: party
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
                - name: keyPrefix
                  value: "none"
            scopes:
              - eventstore
              - eventstore-admin
              - parties
              - tenants
            """);

        string scopes = includePartiesScope
            ? "parties=system.tenants.events;{env:SUBSCRIBER_APP_ID}=acme.parties.events"
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
                  value: "tenants=system.tenants.events;{env:SUBSCRIBER_APP_ID}="
                - name: subscriptionScopes
                  value: "{{scopes}}"
            scopes:
              - eventstore
              - parties
              - tenants
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
              - parties
            """);

    private static void WriteTenantsConfig(string dir, bool malformed, bool includeSensitiveValues)
    {
        // Inject PII into a metadata.annotations field that the validator does not parse.
        // The test then asserts the validator never echoes arbitrary YAML content into its output —
        // proving it only emits values it has explicitly chosen to emit, not raw config payloads.
        string sensitiveAnnotation = includeSensitiveValues
            ? "Bearer secret eventstore:tenant=tenant-a user-1@example.com ConnectionString=secret"
            : "none";

        File.WriteAllText(Path.Combine(dir, "tenants-integration.yaml"), malformed
            ? $$"""
                apiVersion: hexalith.io/v1
                kind: TenantsIntegration
                metadata:
                  name: parties-tenants
                  annotations:
                    diagnostic: "{{sensitiveAnnotation}}"
                spec:
                  pubsubName: ""
                  topicName: ""
                  commandApiAppId: ""
                """
            : $$"""
                apiVersion: hexalith.io/v1
                kind: TenantsIntegration
                metadata:
                  name: parties-tenants
                  annotations:
                    diagnostic: "{{sensitiveAnnotation}}"
                spec:
                  pubsubName: pubsub
                  topicName: system.tenants.events
                  commandApiAppId: eventstore
                  tenantsDependencyHealth: healthy
                """);
    }

    private static void WriteLocalDevConfig(string dir)
    {
        WriteCommonProductionFiles(dir, includePartiesScope: false);
        File.WriteAllText(Path.Combine(dir, "accesscontrol.yaml"), """
            apiVersion: dapr.io/v1alpha1
            kind: Configuration
            spec:
              accessControl:
                defaultAction: allow
                trustDomain: "public"
                policies:
                  - appId: parties
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
              - parties
            """);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing && Directory.Exists(_tempDir))
        {
            try
            {
                Directory.Delete(_tempDir, recursive: true);
            }
            catch (IOException)
            {
                // Locked file or in-use handle; safe to leave for OS temp cleanup.
            }
            catch (UnauthorizedAccessException)
            {
                // ACL issue; safe to leave for OS temp cleanup.
            }
        }

        _disposed = true;
    }
}
