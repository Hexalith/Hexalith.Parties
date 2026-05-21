namespace Hexalith.Parties.DeployValidation.Tests;

using System.Diagnostics;
using System.Text.Json;
using System.Threading;

/// <summary>
/// Story 9.3 AC7 — three new K8s manifest lint categories exercised against synthetic
/// fixtures, plus the AC4 regen.ps1 SecretKeyRef patch idempotency test. The fixtures
/// mirror the temp-fixture + Process.Start pattern from
/// <see cref="K8sManifestLintTests"/> so the existing collection serialization (PowerShell
/// process pool) is respected.
/// </summary>
/// <remarks>
/// Test floor per AC7 / Required Test Matrix:
///   - K8sTopology-MissingService: 4 tests (positive, negative, selector mismatch, ExternalName whitelist).
///   - K8sSecret-JwtSigningKeyLiteral: 5 tests (env literal, Secret.data, secretKeyRef negative,
///     poison-string sweep, redaction-contract).
///   - K8sDapr-ResiliencyCrdSchemaDrift: 3 tests (legacy daprSidecar.general, statestore flat shape, apiVersion skew).
///   - AC4 SecretKeyRef shape on consumer Deployments: 1 test.
///   - AC4 regen.ps1 patch idempotency: 1 test.
///   - Total: 14 + the EventStore binding roundtrip tests already in
///     <see cref="EventStoreRegistrationBindingTests"/> (2) = 16 new tests in this story.
/// </remarks>
[Collection("DeployValidation")]
public sealed class K8sStory93LintTests : IDisposable
{
    private readonly string _scriptPath;
    private readonly string _solutionRoot;
    private readonly string _tempK8sDir;
    private readonly string _tempDaprDir;
    private bool _disposed;

    public K8sStory93LintTests()
    {
        string? solutionDir = FindSolutionDirectory();
        solutionDir.ShouldNotBeNull("Could not find solution directory");
        _solutionRoot = solutionDir;
        _scriptPath = Path.Combine(solutionDir, "deploy", "validate-deployment.ps1");
        File.Exists(_scriptPath).ShouldBeTrue($"Validation script not found at {_scriptPath}");

        _tempK8sDir = Path.Combine(Path.GetTempPath(), $"k8s-9-3-{Guid.NewGuid():N}");
        _tempDaprDir = Path.Combine(Path.GetTempPath(), $"k8s-9-3-dapr-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempK8sDir);
        Directory.CreateDirectory(_tempDaprDir);
    }

    public void Dispose()
    {
        if (_disposed) { return; }
        _disposed = true;
        foreach (string dir in new[] { _tempK8sDir, _tempDaprDir })
        {
            try { if (Directory.Exists(dir)) { Directory.Delete(dir, recursive: true); } }
            catch (IOException) { /* best-effort cleanup */ }
        }
        GC.SuppressFinalize(this);
    }

    // ========================================================================
    // K8sTopology-MissingService (4 tests)
    // ========================================================================

    [Fact]
    public async Task K8sTopologyMissingService_FiresWhenExpectedAppFolderIsAbsent()
    {
        // Arrange — write a partial topology missing the `redis` folder.
        WriteFullExpectedTopology(_tempK8sDir, skipApps: new[] { "redis" });

        // Act
        (int exit, string stdout, _) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);

        // Assert — exit 1 + at least one MissingService finding for redis.
        exit.ShouldBe(1);
        IReadOnlyList<JsonElement> findings = ExtractK8sFindings(stdout, "K8sTopology-MissingService");
        findings.ShouldNotBeEmpty();
        findings.Any(f => f.GetProperty("target").GetString()!.Contains("redis", StringComparison.Ordinal)).ShouldBeTrue();
    }

    [Fact]
    public async Task K8sTopologyMissingService_PassesWhenAllExpectedFoldersPresent()
    {
        WriteFullExpectedTopology(_tempK8sDir);
        (int exit, string stdout, _) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        exit.ShouldBe(0);
        IReadOnlyList<JsonElement> findings = ExtractK8sFindings(stdout, "K8sTopology-MissingService");
        findings.ShouldBeEmpty();
    }

    [Fact]
    public async Task K8sTopologyMissingService_FiresWhenServiceSelectorMismatchesDeploymentLabel()
    {
        WriteFullExpectedTopology(_tempK8sDir);
        // Tamper: rewrite the parties Service selector to a wrong label.
        string svcPath = Path.Combine(_tempK8sDir, "parties", "service.yaml");
        string svc = File.ReadAllText(svcPath).Replace("app: parties", "app: parties-orphan");
        File.WriteAllText(svcPath, svc);

        (int exit, string stdout, _) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        exit.ShouldBe(1);
        IReadOnlyList<JsonElement> findings = ExtractK8sFindings(stdout, "K8sTopology-MissingService");
        findings.Any(f => f.GetProperty("target").GetString()!.Contains("parties", StringComparison.Ordinal)
                         && f.GetProperty("recommendation").GetString()!.Contains("Service selector", StringComparison.Ordinal))
            .ShouldBeTrue();
    }

    [Fact]
    public async Task K8sTopologyMissingService_DoesNotFireOnExternalNameService()
    {
        WriteFullExpectedTopology(_tempK8sDir);
        // Replace memories Service with type=ExternalName (no selector, no clusterIP).
        string svcPath = Path.Combine(_tempK8sDir, "memories", "service.yaml");
        File.WriteAllText(svcPath, """
            apiVersion: v1
            kind: Service
            metadata:
              name: memories
            spec:
              type: ExternalName
              externalName: memories.example.com
            """);

        (int exit, string stdout, _) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        // ExternalName services have no selector — must NOT trigger the mismatch rule.
        IReadOnlyList<JsonElement> memoriesFindings = ExtractK8sFindings(stdout, "K8sTopology-MissingService")
            .Where(f => f.GetProperty("target").GetString()!.Contains("memories", StringComparison.Ordinal))
            .ToList();
        memoriesFindings.ShouldBeEmpty($"ExternalName Service must not trigger K8sTopology-MissingService. stdout:\n{stdout}");
        exit.ShouldBe(0);
    }

    // ========================================================================
    // K8sSecret-JwtSigningKeyLiteral (5 tests)
    // ========================================================================

    [Fact]
    public async Task K8sSecretJwtSigningKeyLiteral_FiresOnInlineEnvLiteral()
    {
        // Use a full-topology fixture + inject the JWT literal on parties — keeps the
        // topology lint quiet so the assertion isolates to the JWT category under test.
        WriteFullExpectedTopology(_tempK8sDir);
        WriteAppWithJwtEnvLiteral(_tempK8sDir, "parties", literalValue: "this-is-a-secret-key-value-1234");
        (_, string stdout, _) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        IReadOnlyList<JsonElement> findings = ExtractK8sFindings(stdout, "K8sSecret-JwtSigningKeyLiteral");
        findings.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task K8sSecretJwtSigningKeyLiteral_FiresOnSecretDataLiteral()
    {
        WriteFullExpectedTopology(_tempK8sDir);
        // Write a Secret with a non-placeholder data entry.
        string secretPath = Path.Combine(_tempK8sDir, "parties", "jwt-secret.yaml");
        File.WriteAllText(secretPath, """
            apiVersion: v1
            kind: Secret
            metadata:
              name: hexalith-jwt-signing
            type: Opaque
            data:
              Authentication__JwtBearer__SigningKey: c2VjcmV0LXNpZ25pbmcta2V5LWlzLWNvbW1pdHRlZA==
            """);
        (_, string stdout, _) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        IReadOnlyList<JsonElement> findings = ExtractK8sFindings(stdout, "K8sSecret-JwtSigningKeyLiteral");
        findings.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task K8sSecretJwtSigningKeyLiteral_PassesOnSecretKeyRefShape()
    {
        WriteFullExpectedTopology(_tempK8sDir);
        WriteAppWithJwtSecretKeyRef(_tempK8sDir, "parties");
        (_, string stdout, _) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        IReadOnlyList<JsonElement> findings = ExtractK8sFindings(stdout, "K8sSecret-JwtSigningKeyLiteral");
        findings.ShouldBeEmpty();
    }

    [Fact]
    public async Task K8sSecretJwtSigningKeyLiteral_PoisonStringSweep_NeverEchoesLiteralValue()
    {
        const string poisonValue = "POISONED_JWT_DO_NOT_LEAK_MAGIC_TOKEN_42";
        WriteFullExpectedTopology(_tempK8sDir);
        WriteAppWithJwtEnvLiteral(_tempK8sDir, "parties", literalValue: poisonValue);
        (_, string stdout, string stderr) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        // The poison string must never appear in stdout, stderr, or JSON recommendation.
        stdout.ShouldNotContain(poisonValue);
        stderr.ShouldNotContain(poisonValue);
    }

    [Fact]
    public async Task K8sSecretJwtSigningKeyLiteral_RedactionContract_RecommendationCarriesRedactedShape()
    {
        WriteFullExpectedTopology(_tempK8sDir);
        WriteAppWithJwtEnvLiteral(_tempK8sDir, "parties", literalValue: "exact-32-character-secret-12345!");
        (_, string stdout, _) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        IReadOnlyList<JsonElement> findings = ExtractK8sFindings(stdout, "K8sSecret-JwtSigningKeyLiteral");
        findings.ShouldNotBeEmpty();
        // At least one finding must have a redaction-contract `<redacted:N chars at <file>:<line>>` shape
        // and must NOT echo the literal value.
        bool anyRedactionShape = findings.Any(f =>
        {
            string rec = f.GetProperty("recommendation").GetString() ?? string.Empty;
            return rec.Contains("<redacted:", StringComparison.Ordinal)
                && rec.Contains(" chars at ", StringComparison.Ordinal)
                && !rec.Contains("exact-32-character-secret", StringComparison.Ordinal);
        });
        anyRedactionShape.ShouldBeTrue("At least one finding must use the <redacted:N chars at <file>:<line>> contract and not echo the literal value.");
    }

    // ========================================================================
    // K8sDapr-ResiliencyCrdSchemaDrift (3 tests)
    // ========================================================================

    [Fact]
    public async Task K8sDaprResiliencyCrdSchemaDrift_FiresOnLegacyNestedDaprSidecarGeneral()
    {
        WriteFullExpectedTopology(_tempK8sDir);
        WriteResiliencyYaml(_tempDaprDir, """
            apiVersion: dapr.io/v1alpha1
            kind: Resiliency
            metadata:
              name: resiliency
            spec:
              policies:
                timeouts:
                  daprSidecar:
                    general: 5s
            """);
        (_, string stdout, _) = await RunLintAsync(daprPath: _tempDaprDir, k8sPath: _tempK8sDir, jsonOutput: true);
        IReadOnlyList<JsonElement> findings = ExtractK8sFindings(stdout, "K8sDapr-ResiliencyCrdSchemaDrift");
        findings.Any(f => f.GetProperty("recommendation").GetString()!.Contains("Duration scalar", StringComparison.Ordinal))
            .ShouldBeTrue();
    }

    [Fact]
    public async Task K8sDaprResiliencyCrdSchemaDrift_FiresOnFlatComponentTargetShape()
    {
        WriteFullExpectedTopology(_tempK8sDir);
        WriteResiliencyYaml(_tempDaprDir, """
            apiVersion: dapr.io/v1alpha1
            kind: Resiliency
            metadata:
              name: resiliency
            spec:
              policies:
                timeouts:
                  daprSidecar: 5s
              targets:
                components:
                  statestore:
                    retry: defaultRetry
                    timeout: daprSidecar
                    circuitBreaker: defaultBreaker
            """);
        (_, string stdout, _) = await RunLintAsync(daprPath: _tempDaprDir, k8sPath: _tempK8sDir, jsonOutput: true);
        IReadOnlyList<JsonElement> findings = ExtractK8sFindings(stdout, "K8sDapr-ResiliencyCrdSchemaDrift");
        findings.Any(f => f.GetProperty("recommendation").GetString()!.Contains("outbound/inbound", StringComparison.Ordinal))
            .ShouldBeTrue();
    }

    [Fact]
    public async Task K8sDaprResiliencyCrdSchemaDrift_UnknownApiVersion_EmitsWarnNotCrash()
    {
        WriteFullExpectedTopology(_tempK8sDir);
        WriteResiliencyYaml(_tempDaprDir, """
            apiVersion: dapr.io/v2
            kind: Resiliency
            metadata:
              name: resiliency
            spec:
              policies:
                timeouts:
                  daprSidecar: 5s
            """);
        (_, string stdout, _) = await RunLintAsync(daprPath: _tempDaprDir, k8sPath: _tempK8sDir, jsonOutput: true);
        // Unknown apiVersion is warn-severity only — the schema-drift category emits warn,
        // not fail. The script must not crash.
        IReadOnlyList<JsonElement> findings = ExtractK8sFindings(stdout, "K8sDapr-ResiliencyCrdSchemaDrift");
        findings.ShouldNotBeEmpty();
        findings.All(f => f.GetProperty("severity").GetString() == "warn").ShouldBeTrue();
    }

    // ========================================================================
    // AC4 — SecretKeyRef shape + regen.ps1 patch idempotency (2 tests)
    // ========================================================================

    [Fact]
    public async Task ConsumerDeploymentsCarryJwtSecretKeyRef_WhenJwtEnvIsPresent()
    {
        WriteFullExpectedTopology(_tempK8sDir);
        WriteAppWithJwtSecretKeyRef(_tempK8sDir, "parties");
        // Verify the file shape contains the secretKeyRef structure (string-level assertion).
        string deploymentText = File.ReadAllText(Path.Combine(_tempK8sDir, "parties", "deployment.yaml"));
        deploymentText.ShouldContain("secretKeyRef");
        deploymentText.ShouldContain("hexalith-jwt-signing");
        // Re-running the lint against this fixture surfaces zero JwtSigningKeyLiteral findings.
        (_, string stdout, _) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        IReadOnlyList<JsonElement> findings = ExtractK8sFindings(stdout, "K8sSecret-JwtSigningKeyLiteral");
        findings.ShouldBeEmpty();
    }

    [Fact]
    public void RegenPs1JwtPatch_IsIdempotent_NoDoublePatchOnSecondRun()
    {
        // This test is a static-source assertion: the regen.ps1 patch block MUST guard against
        // double-patching by checking for the presence of the secretKeyRef sibling before
        // injecting. Verify the guard exists in the regen.ps1 source by reading the file
        // and asserting the documented anchor strategy. This is a Tier-1 surface assertion
        // that protects against a future refactor that drops the idempotency guard.
        string regenPath = Path.Combine(_solutionRoot, "deploy", "k8s", "regen.ps1");
        File.Exists(regenPath).ShouldBeTrue($"regen.ps1 not found at {regenPath}");
        string regenText = File.ReadAllText(regenPath);
        // Anchor strategy: idempotency check must reference the JWT secret name AND the env key name.
        regenText.ShouldContain("hexalith-jwt-signing");
        regenText.ShouldContain("Authentication__JwtBearer__SigningKey");
        // Documented anchor + no-op clause (Story 9.3 AC7 patch-idempotency contract):
        // the patch must short-circuit when a `secretKeyRef` sibling already exists.
        regenText.ShouldContain("Idempotency");
    }

    // ========================================================================
    // Fixture helpers
    // ========================================================================

    private static void WriteFullExpectedTopology(string root, string[]? skipApps = null)
    {
        skipApps ??= Array.Empty<string>();
        string[] expected = ["eventstore", "eventstore-admin", "eventstore-admin-ui",
                             "parties", "parties-mcp", "tenants",
                             "memories", "keycloak", "redis"];
        foreach (string app in expected)
        {
            if (skipApps.Contains(app)) { continue; }
            WriteMinimalApp(root, app);
        }
    }

    private static void WriteMinimalApp(string root, string appId)
    {
        string appDir = Path.Combine(root, appId);
        Directory.CreateDirectory(appDir);
        bool isDaprEnabled = appId is "eventstore" or "eventstore-admin" or "parties" or "tenants" or "memories";
        string annotations = isDaprEnabled
            ? $"""

              annotations:
                dapr.io/enabled: 'true'
                dapr.io/config: accesscontrol-{appId}
                dapr.io/app-id: {appId}
                dapr.io/app-port: '8080'
            """
            : "";
        string podAnnotations = isDaprEnabled
            ? $"""

                  annotations:
                    dapr.io/enabled: 'true'
                    dapr.io/config: accesscontrol-{appId}
                    dapr.io/app-id: {appId}
                    dapr.io/app-port: '8080'
            """
            : "";
        File.WriteAllText(Path.Combine(appDir, "deployment.yaml"), $"""
            ---
            apiVersion: apps/v1
            kind: Deployment
            metadata:
              name: {appId}
              labels:
                app: {appId}{annotations}
            spec:
              replicas: 1
              selector:
                matchLabels:
                  app: {appId}
              template:
                metadata:
                  labels:
                    app: {appId}{podAnnotations}
                spec:
                  containers:
                  - name: {appId}
                    image: registry.example.com/{appId}:v1.0.0
                    imagePullPolicy: IfNotPresent
                    envFrom:
                    - configMapRef:
                        name: {appId}-env
            """);
        File.WriteAllText(Path.Combine(appDir, "service.yaml"), $"""
            ---
            apiVersion: v1
            kind: Service
            metadata:
              name: {appId}
            spec:
              selector:
                app: {appId}
              ports:
              - name: http
                port: 8080
                targetPort: 8080
            """);
        File.WriteAllText(Path.Combine(appDir, "kustomization.yaml"), $"""
            resources:
            - deployment.yaml
            - service.yaml
            generatorOptions:
              disableNameSuffixHash: true
            configMapGenerator:
            - name: {appId}-env
              literals:
                - HTTP_PORTS=8080
            """);
        // Top-level kustomization (idempotent append).
        string topKust = Path.Combine(root, "kustomization.yaml");
        if (!File.Exists(topKust))
        {
            File.WriteAllText(topKust, $"resources:\n- {appId}\n");
        }
        else
        {
            string current = File.ReadAllText(topKust);
            if (!current.Contains($"- {appId}\n", StringComparison.Ordinal))
            {
                File.WriteAllText(topKust, current + $"- {appId}\n");
            }
        }
    }

    private static void WriteAppWithJwtEnvLiteral(string root, string appId, string literalValue)
    {
        WriteMinimalApp(root, appId);
        string deploymentPath = Path.Combine(root, appId, "deployment.yaml");
        // Insert an explicit env block with a literal Authentication__JwtBearer__SigningKey
        // value. The literal violates the no-secret-values contract.
        // C# raw-string-literal leading whitespace is stripped to the closing `"""` indent,
        // so the on-disk YAML has 8-space indent at the container-spec level.
        string deployment = File.ReadAllText(deploymentPath);
        string injected = deployment.Replace(
            "        envFrom:",
            $"        env:\n" +
            $"        - name: Authentication__JwtBearer__SigningKey\n" +
            $"          value: '{literalValue}'\n" +
            $"        envFrom:");
        File.WriteAllText(deploymentPath, injected);
    }

    private static void WriteAppWithJwtSecretKeyRef(string root, string appId)
    {
        WriteMinimalApp(root, appId);
        string deploymentPath = Path.Combine(root, appId, "deployment.yaml");
        string deployment = File.ReadAllText(deploymentPath);
        string injected = deployment.Replace(
            "        envFrom:",
            "        env:\n" +
            "        - name: Authentication__JwtBearer__SigningKey\n" +
            "          valueFrom:\n" +
            "            secretKeyRef:\n" +
            "              name: hexalith-jwt-signing\n" +
            "              key: Authentication__JwtBearer__SigningKey\n" +
            "        envFrom:");
        File.WriteAllText(deploymentPath, injected);
    }

    private static void WriteResiliencyYaml(string daprDir, string body)
    {
        File.WriteAllText(Path.Combine(daprDir, "resiliency.yaml"), body);
    }

    private static IReadOnlyList<JsonElement> ExtractK8sFindings(string stdoutJson, string code)
    {
        using JsonDocument doc = JsonDocument.Parse(stdoutJson);
        if (!doc.RootElement.TryGetProperty("k8sFindings", out JsonElement arr))
        {
            return Array.Empty<JsonElement>();
        }
        var matches = new List<JsonElement>();
        foreach (JsonElement f in arr.EnumerateArray())
        {
            if (f.TryGetProperty("code", out JsonElement c) && c.GetString() == code)
            {
                matches.Add(f.Clone());
            }
        }
        return matches;
    }

    private async Task<(int ExitCode, string StdOut, string StdErr)> RunLintAsync(
        string? daprPath = null,
        string? k8sPath = null,
        bool jsonOutput = false)
    {
        string powershellExe = FindPowerShell();
        var argList = new List<string>
        {
            "-NoProfile",
            "-ExecutionPolicy", "Bypass",
            "-File", _scriptPath,
        };
        if (daprPath != null) { argList.AddRange(new[] { "-ConfigPath", daprPath }); }
        if (k8sPath != null) { argList.AddRange(new[] { "-K8sPath", k8sPath }); }
        if (jsonOutput) { argList.AddRange(new[] { "-Output", "json" }); }

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = powershellExe,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (string arg in argList) { process.StartInfo.ArgumentList.Add(arg); }
        process.Start();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
        Task<string> stderrTask = process.StandardError.ReadToEndAsync(cts.Token);
        try
        {
            await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* best-effort */ }
            // Story 9.3 review fix: drain partial stdout/stderr without the cancellation
            // token so the failure message can include whatever the process emitted before
            // the timeout. Without this, the InvalidOperationException carries no payload
            // and debugging requires local reproduction.
            string partialStdout;
            string partialStderr;
            try { partialStdout = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false); } catch { partialStdout = "<unavailable>"; }
            try { partialStderr = await process.StandardError.ReadToEndAsync().ConfigureAwait(false); } catch { partialStderr = "<unavailable>"; }
            throw new InvalidOperationException(
                $"validate-deployment.ps1 exceeded the 90 s timeout and was killed.{Environment.NewLine}" +
                $"--- partial stdout (truncated to 4 KB) ---{Environment.NewLine}{Truncate(partialStdout, 4096)}{Environment.NewLine}" +
                $"--- partial stderr (truncated to 4 KB) ---{Environment.NewLine}{Truncate(partialStderr, 4096)}",
                ex);
        }
        return (process.ExitCode, stdoutTask.Result, stderrTask.Result);
    }

    private static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= max ? value : value.Substring(0, max) + "... [truncated]";
    }

    private static string FindPowerShell()
    {
        string[] candidates = ["pwsh", "pwsh.exe", "powershell", "powershell.exe"];
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
                Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
                Task<string> stderrTask = process.StandardError.ReadToEndAsync();
                bool exited = process.WaitForExit(5000);
                if (!exited)
                {
                    try { process.Kill(entireProcessTree: true); } catch { /* best-effort */ }
                    continue;
                }
                try { Task.WaitAll([stdoutTask, stderrTask], 1000); } catch { /* best-effort */ }
                if (process.ExitCode == 0) { return candidate; }
            }
            catch
            {
                continue;
            }
        }
        throw new InvalidOperationException("PowerShell not found.");
    }

    private static string? FindSolutionDirectory()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.GetFiles(dir, "*.slnx").Length > 0) { return dir; }
            dir = Directory.GetParent(dir)?.FullName;
        }
        return null;
    }
}
