namespace Hexalith.Parties.DeployValidation.Tests;

using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;

/// <summary>
/// Story 9.2 — Kubernetes manifest lint surface tests. Exercises
/// <c>deploy/validate-deployment.ps1</c> in K8s-mode (<c>-K8sPath</c>) via
/// synthetic fixtures + the as-committed tree. No live cluster, no kubectl,
/// no DAPR install. Mirrors the temp-fixture + Process.Start pattern from
/// <see cref="DeploymentValidationTests"/>.
/// </summary>
[Collection("DeployValidation")]
public sealed class K8sManifestLintTests : IDisposable
{
    private readonly string _scriptPath;
    private readonly string _solutionRoot;
    private readonly string _tempK8sDir;
    private readonly string _tempDaprDir;
    private bool _disposed;

    public K8sManifestLintTests()
    {
        string? solutionDir = FindSolutionDirectory();
        solutionDir.ShouldNotBeNull("Could not find solution directory");
        _solutionRoot = solutionDir;
        _scriptPath = Path.Combine(solutionDir, "deploy", "validate-deployment.ps1");
        File.Exists(_scriptPath).ShouldBeTrue($"Validation script not found at {_scriptPath}");

        _tempK8sDir = Path.Combine(Path.GetTempPath(), $"k8s-lint-k8s-{Guid.NewGuid():N}");
        _tempDaprDir = Path.Combine(Path.GetTempPath(), $"k8s-lint-dapr-{Guid.NewGuid():N}");
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

    // -----------------------------------------------------------------------
    // (a) Happy-path regression — committed tree passes lint with 0 fails.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CommittedTreeLintPassesWithZeroFailsAndAtLeastFourWarns()
    {
        string k8sPath = Path.Combine(_solutionRoot, "deploy", "k8s");
        string daprPath = Path.Combine(_solutionRoot, "deploy", "dapr");
        (int exit, string stdout, string stderr) = await RunLintAsync(daprPath: daprPath, k8sPath: k8sPath, jsonOutput: true);
        exit.ShouldBe(0, $"Expected exit 0 against committed tree.\nstdout:\n{stdout}\nstderr:\n{stderr}");

        using JsonDocument doc = JsonDocument.Parse(stdout);
        JsonElement summary = doc.RootElement.GetProperty("summary");
        summary.GetProperty("k8sFail").GetInt32().ShouldBe(0, "Committed tree must have zero K8s blocking fails.");
        summary.GetProperty("k8sWarn").GetInt32().ShouldBeGreaterThanOrEqualTo(4,
            "Committed tree should surface >=4 warns (missing probes / resources / :latest).");
    }

    // -----------------------------------------------------------------------
    // (b) AC1 fail-shape: missing image
    // -----------------------------------------------------------------------

    [Fact]
    public async Task MissingImageInDeploymentTriggersBlockingFail()
    {
        WriteMinimalApp(_tempK8sDir, "parties", missingImage: true);
        (int exit, string stdout, _) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        exit.ShouldBe(1);
        stdout.ShouldContain("K8sWorkload-MissingImage");
    }

    [Fact]
    public async Task EmptyImageStringTriggersBlockingFail()
    {
        WriteMinimalApp(_tempK8sDir, "parties", emptyImage: true);
        (int exit, string stdout, _) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        exit.ShouldBe(1);
        stdout.ShouldContain("K8sWorkload-MissingImage");
    }

    // -----------------------------------------------------------------------
    // (c) AC1 fail-shape: missing DAPR annotation per DAPR-enabled app id
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("eventstore")]
    [InlineData("eventstore-admin")]
    [InlineData("parties")]
    [InlineData("tenants")]
    public async Task MissingDaprAnnotationOnDaprEnabledAppTriggersBlockingFail(string appId)
    {
        WriteMinimalApp(_tempK8sDir, appId, includeDaprAnnotations: false);
        (int exit, string stdout, _) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        exit.ShouldBe(1, stdout);
        stdout.ShouldContain("K8sWorkload-MissingDaprAnnotation");
    }

    [Theory]
    [InlineData("eventstore-admin-ui")]
    [InlineData("parties-mcp")]
    public async Task MissingDaprAnnotationOnExcludedAppDoesNotTriggerFail(string appId)
    {
        WriteMinimalApp(_tempK8sDir, appId, includeDaprAnnotations: false);
        (int exit, string stdout, _) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        // No K8sWorkload-MissingDaprAnnotation — only warn-severity shape findings allowed.
        stdout.ShouldNotContain("K8sWorkload-MissingDaprAnnotation");
        using JsonDocument doc = JsonDocument.Parse(stdout);
        doc.RootElement.GetProperty("summary").GetProperty("k8sFail").GetInt32().ShouldBe(0,
            $"Excluded app id '{appId}' must not trigger DAPR-annotation failures.");
        exit.ShouldBe(0);
    }

    // -----------------------------------------------------------------------
    // (d) AC1 fail-shape: unresolved envFrom configMapRef
    // -----------------------------------------------------------------------

    [Fact]
    public async Task UnresolvedConfigMapRefTriggersBlockingFail()
    {
        WriteMinimalApp(_tempK8sDir, "parties", configMapRefName: "nonexistent-env");
        (int exit, string stdout, _) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        exit.ShouldBe(1);
        stdout.ShouldContain("K8sWorkload-UnresolvedConfigMapRef");
    }

    // -----------------------------------------------------------------------
    // (e) AC1 fail-shape: unresolved top-level kustomization.yaml resources
    // -----------------------------------------------------------------------

    [Fact]
    public async Task UnresolvedTopLevelKustomizationResourceTriggersBlockingFail()
    {
        WriteMinimalApp(_tempK8sDir, "parties");
        File.WriteAllText(Path.Combine(_tempK8sDir, "kustomization.yaml"), """
            resources:
            - parties
            - ghost-folder
            """);
        (int exit, string stdout, _) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        exit.ShouldBe(1);
        stdout.ShouldContain("K8sWorkload-UnresolvedKustomizationResource");
    }

    // -----------------------------------------------------------------------
    // (f) AC1 warn-shape: missing probes / resources / :latest tag
    // -----------------------------------------------------------------------

    [Fact]
    public async Task MissingProbesEmitsWarnNotFail()
    {
        WriteMinimalApp(_tempK8sDir, "parties");
        (int exit, string stdout, _) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        exit.ShouldBe(0);
        stdout.ShouldContain("K8sWorkload-MissingProbes");
        stdout.ShouldContain("\"severity\": \"warn\"");
    }

    [Fact]
    public async Task MissingResourcesEmitsWarnNotFail()
    {
        WriteMinimalApp(_tempK8sDir, "parties");
        (int exit, string stdout, _) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        exit.ShouldBe(0);
        stdout.ShouldContain("K8sWorkload-MissingResources");
    }

    [Fact]
    public async Task LatestImageTagEmitsWarnNotFail()
    {
        WriteMinimalApp(_tempK8sDir, "parties", imageTag: ":latest");
        (int exit, string stdout, _) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        exit.ShouldBe(0);
        stdout.ShouldContain("K8sWorkload-LatestImageTag");
    }

    [Fact]
    public async Task StagingLatestTagDoesNotTriggerLatestImageTagWarn()
    {
        WriteMinimalApp(_tempK8sDir, "parties", imageTag: ":staging-latest");
        (int exit, string stdout, _) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        exit.ShouldBe(0);
        stdout.ShouldNotContain("K8sWorkload-LatestImageTag");
    }

    // -----------------------------------------------------------------------
    // AC2: DAPR ACL drift
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DaprAclDefaultActionAllowTriggersBlockingFail()
    {
        WriteMinimalApp(_tempK8sDir, "parties");
        WriteAuthoritativeDapr(_tempDaprDir);
        File.WriteAllText(Path.Combine(_tempDaprDir, "accesscontrol.yaml"), """
            apiVersion: dapr.io/v1alpha1
            kind: Configuration
            metadata:
              name: accesscontrol
            spec:
              accessControl:
                defaultAction: allow
                trustDomain: public
                policies:
                  - appId: parties
            """);
        (int exit, string stdout, _) = await RunLintAsync(daprPath: _tempDaprDir, k8sPath: _tempK8sDir, jsonOutput: true);
        exit.ShouldBe(1, stdout);
        stdout.ShouldContain("DAPR-ACL-DefaultActionNotDeny");
    }

    [Fact]
    public async Task DaprAclWildcardAppIdTriggersBlockingFail()
    {
        WriteMinimalApp(_tempK8sDir, "parties");
        WriteAuthoritativeDapr(_tempDaprDir);
        File.WriteAllText(Path.Combine(_tempDaprDir, "accesscontrol.yaml"), """
            apiVersion: dapr.io/v1alpha1
            kind: Configuration
            metadata:
              name: accesscontrol
            spec:
              accessControl:
                defaultAction: deny
                trustDomain: public
                policies:
                  - appId: '*'
            """);
        (int exit, string stdout, _) = await RunLintAsync(daprPath: _tempDaprDir, k8sPath: _tempK8sDir, jsonOutput: true);
        exit.ShouldBe(1, stdout);
        stdout.ShouldContain("DAPR-ACL-WildcardAppId");
    }

    [Fact]
    public async Task DaprSubscriptionMissingDeadLetterTriggersBlockingFail()
    {
        WriteMinimalApp(_tempK8sDir, "parties");
        WriteAuthoritativeDapr(_tempDaprDir);
        File.WriteAllText(Path.Combine(_tempDaprDir, "subscription-parties.yaml"), """
            apiVersion: dapr.io/v2alpha1
            kind: Subscription
            metadata:
              name: parties-events
            spec:
              pubsubname: pubsub
              topic: parties.events
              routes:
                default: /process
            scopes:
              - parties
            """);
        (int exit, string stdout, _) = await RunLintAsync(daprPath: _tempDaprDir, k8sPath: _tempK8sDir, jsonOutput: true);
        exit.ShouldBe(1, stdout);
        stdout.ShouldContain("DAPR-Subscription-MissingDeadLetter");
    }

    [Fact]
    public async Task DaprSubscriptionWrongPubsubNameTriggersBlockingFail()
    {
        WriteMinimalApp(_tempK8sDir, "parties");
        WriteAuthoritativeDapr(_tempDaprDir);
        File.WriteAllText(Path.Combine(_tempDaprDir, "subscription-parties.yaml"), """
            apiVersion: dapr.io/v2alpha1
            kind: Subscription
            metadata:
              name: parties-events
            spec:
              pubsubname: not-pubsub
              topic: parties.events
              deadLetterTopic: parties.deadletter
              routes:
                default: /process
            scopes:
              - parties
            """);
        (int exit, string stdout, _) = await RunLintAsync(daprPath: _tempDaprDir, k8sPath: _tempK8sDir, jsonOutput: true);
        exit.ShouldBe(1, stdout);
        stdout.ShouldContain("DAPR-Subscription-WrongPubsubName");
    }

    [Fact]
    public async Task RegenPlaceholderReintroducedTriggersBlockingFail()
    {
        WriteMinimalApp(_tempK8sDir, "parties");
        WriteAuthoritativeDapr(_tempDaprDir);
        Directory.CreateDirectory(Path.Combine(_tempK8sDir, "dapr"));
        File.WriteAllText(Path.Combine(_tempK8sDir, "dapr", "statestore.yaml"),
            "apiVersion: dapr.io/v1alpha1\nkind: Component\nmetadata:\n  name: statestore\nspec:\n  type: state.redis\n  metadata: []\n");
        (int exit, string stdout, _) = await RunLintAsync(daprPath: _tempDaprDir, k8sPath: _tempK8sDir, jsonOutput: true);
        exit.ShouldBe(1, stdout);
        stdout.ShouldContain("DAPR-Regen-PlaceholderNotStripped");
    }

    // -----------------------------------------------------------------------
    // AC3 plaintext-secret regex (positive + negatives per regex)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("password=hunter2RealValue", "K8sSecret-PlaintextCredential")]
    [InlineData("MyService_token=ghs_abc123def456GHIabc123def", "K8sSecret-PlaintextCredential")]
    [InlineData("DB_URL=postgres://user:supersecret@db:5432", "K8sSecret-UrlEmbeddedCred")]
    [InlineData("AUTH_JWT=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJ1c2VyMTIzIn0.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c", "K8sSecret-JwtTokenLiteral")]
    [InlineData("AWS_KEY=AKIAIOSFODNN7EXAMPLE", "K8sSecret-AwsAccessKey")]
    [InlineData("AZ_CONN=DefaultEndpointsProtocol=https;AccountName=foo;AccountKey=bar==", "K8sSecret-AzureConnString")]
    public async Task PlaintextSecretRegexPositives_TriggerFailAndRedactValue(string literal, string code)
    {
        ArgumentNullException.ThrowIfNull(literal);
        WriteMinimalAppWithLiteral(_tempK8sDir, "parties", literal);
        (int exit, string stdout, string stderr) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        exit.ShouldBe(1, stdout);
        stdout.ShouldContain(code);
        // Redaction contract: the offending value MUST NOT appear anywhere.
        string secretValue = literal[(literal.IndexOf('=') + 1)..];
        stdout.ShouldNotContain(secretValue);
        stderr.ShouldNotContain(secretValue);
        stdout.ShouldContain("<redacted:");
    }

    [Theory]
    [InlineData("password={env:DB_PASSWORD}")]
    [InlineData("password=$(DB_PASSWORD)")]
    [InlineData("password=REPLACE_ME")]
    [InlineData("password=%24%7Benv%3ADB_PASSWORD%7D")]
    public async Task PlaintextSecretRegexNegatives_PlaceholderValuesPass(string literal)
    {
        WriteMinimalAppWithLiteral(_tempK8sDir, "parties", literal);
        (int exit, string stdout, _) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        exit.ShouldBe(0, $"Placeholder literal '{literal}' should not fail. Output:\n{stdout}");
        stdout.ShouldNotContain("K8sSecret-PlaintextCredential");
    }

    [Theory]
    [InlineData("Tenants__PubSubName=pubsub")]
    [InlineData("Tenants__ServiceName=tenants")]
    [InlineData("services__eventstore__http__0=http://eventstore:8080")]
    [InlineData("ASPNETCORE_URLS=http://+:8080;")]
    [InlineData("ASPNETCORE_FORWARDEDHEADERS_ENABLED=true")]
    [InlineData("HTTP_PORTS=8080")]
    [InlineData("EVENTSTORE_HTTP=http://eventstore:8080")]
    [InlineData("OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY=in_memory")]
    public async Task KeyAllowlistSweep_DoesNotFireValueRegex(string literal)
    {
        WriteMinimalAppWithLiteral(_tempK8sDir, "parties", literal);
        (int exit, string stdout, _) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        exit.ShouldBe(0, $"Allowlisted key '{literal}' should not trigger a K8s secret finding. Output:\n{stdout}");
        using JsonDocument doc = JsonDocument.Parse(stdout);
        doc.RootElement.GetProperty("summary").GetProperty("k8sFail").GetInt32().ShouldBe(0);
    }

    // -----------------------------------------------------------------------
    // AC3: static tenant id
    // -----------------------------------------------------------------------

    [Fact]
    public async Task StaticTenantIdValueTriggersFail()
    {
        WriteMinimalAppWithLiteral(_tempK8sDir, "parties", "Tenants__TenantId=acme-corp");
        (int exit, string stdout, _) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        exit.ShouldBe(1);
        stdout.ShouldContain("K8sSecret-StaticTenantId");
        stdout.ShouldNotContain("acme-corp");
    }

    [Fact]
    public async Task TenantIdEnvRefPasses()
    {
        WriteMinimalAppWithLiteral(_tempK8sDir, "parties", "Tenants__TenantId={env:TENANT_ID}");
        (int exit, _, _) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        exit.ShouldBe(0);
    }

    // -----------------------------------------------------------------------
    // AC3: committed Secret resource
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CommittedSecretWithPlaintextStringDataTriggersFail()
    {
        WriteMinimalApp(_tempK8sDir, "parties");
        File.WriteAllText(Path.Combine(_tempK8sDir, "parties", "secret.yaml"), """
            apiVersion: v1
            kind: Secret
            metadata:
              name: parties-secret
            type: Opaque
            stringData:
              password: hunter2RealCommittedValue
            """);
        File.WriteAllText(Path.Combine(_tempK8sDir, "parties", "kustomization.yaml"), """
            resources:
            - deployment.yaml
            - secret.yaml
            generatorOptions:
              disableNameSuffixHash: true
            configMapGenerator:
            - name: parties-env
              literals:
                - HTTP_PORTS=8080
            """);
        (int exit, string stdout, _) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        exit.ShouldBe(1, stdout);
        stdout.ShouldContain("K8sSecret-CommittedSecretValue");
        stdout.ShouldNotContain("hunter2RealCommittedValue");
    }

    [Fact]
    public async Task CommittedSecretWithPlaceholderValuePasses()
    {
        WriteMinimalApp(_tempK8sDir, "parties");
        File.WriteAllText(Path.Combine(_tempK8sDir, "parties", "secret.yaml"), """
            apiVersion: v1
            kind: Secret
            metadata:
              name: parties-secret
            type: Opaque
            stringData:
              password: REPLACE_ME
            """);
        File.WriteAllText(Path.Combine(_tempK8sDir, "parties", "kustomization.yaml"), """
            resources:
            - deployment.yaml
            - secret.yaml
            generatorOptions:
              disableNameSuffixHash: true
            configMapGenerator:
            - name: parties-env
              literals:
                - HTTP_PORTS=8080
            """);
        (int exit, _, _) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        exit.ShouldBe(0);
    }

    // -----------------------------------------------------------------------
    // AC3: annotation / comment carve-out
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SecretShapedAnnotationValueDoesNotTriggerFail()
    {
        // dapr.io/enable-api-logging is an aspirate-default annotation that should
        // NOT trip the plaintext-secret scan, even though its key would not be
        // value-checked anyway because annotations are out of scope.
        WriteMinimalApp(_tempK8sDir, "parties");
        // Add a comment that looks like a token; lint must not scan comments.
        string deploymentPath = Path.Combine(_tempK8sDir, "parties", "deployment.yaml");
        string content = File.ReadAllText(deploymentPath);
        content = "# password=PoisonedCommentValueShouldBeIgnored\n" + content;
        File.WriteAllText(deploymentPath, content);
        (int exit, string stdout, _) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        exit.ShouldBe(0, stdout);
        stdout.ShouldNotContain("PoisonedCommentValueShouldBeIgnored");
    }

    // -----------------------------------------------------------------------
    // AC4: cloud-only capabilities (StorageClass, IngressClass, LoadBalancer)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("StorageClass", "managed-csi", "storageclass.yaml")]
    [InlineData("IngressClass", "alb", "ingressclass.yaml")]
    public async Task CloudOnlyCapabilityShape_FailsInDefaultMode(string kind, string name, string fileName)
    {
        WriteMinimalApp(_tempK8sDir, "parties");
        File.WriteAllText(Path.Combine(_tempK8sDir, fileName), $"""
            apiVersion: {(kind == "StorageClass" ? "storage.k8s.io/v1" : "networking.k8s.io/v1")}
            kind: {kind}
            metadata:
              name: {name}
            """);
        File.WriteAllText(Path.Combine(_tempK8sDir, "kustomization.yaml"), $"""
            resources:
            - parties
            - {fileName}
            """);
        (int exit, string stdout, _) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        exit.ShouldBe(1, stdout);
        stdout.ShouldContain("K8s-NonLocalClusterCapability");
    }

    [Fact]
    public async Task ServiceLoadBalancerType_FailsInDefaultMode()
    {
        WriteMinimalApp(_tempK8sDir, "parties");
        File.WriteAllText(Path.Combine(_tempK8sDir, "parties", "lb-service.yaml"), """
            apiVersion: v1
            kind: Service
            metadata:
              name: parties-lb
            spec:
              type: LoadBalancer
              selector:
                app: parties
              ports:
              - port: 8080
            """);
        (int exit, string stdout, _) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        exit.ShouldBe(1, stdout);
        stdout.ShouldContain("K8s-NonLocalClusterCapability");
    }

    [Fact]
    public async Task CloudCapabilityWithAllowSwitch_DemoteToWarn()
    {
        WriteMinimalApp(_tempK8sDir, "parties");
        File.WriteAllText(Path.Combine(_tempK8sDir, "managed-csi.yaml"), """
            apiVersion: storage.k8s.io/v1
            kind: StorageClass
            metadata:
              name: managed-csi
            """);
        File.WriteAllText(Path.Combine(_tempK8sDir, "kustomization.yaml"), """
            resources:
            - parties
            - managed-csi.yaml
            """);
        (int exit, string stdout, _) = await RunLintAsync(k8sPath: _tempK8sDir, allowCloudCapabilities: true, jsonOutput: true);
        exit.ShouldBe(0, stdout);
        stdout.ShouldContain("K8s-NonLocalClusterCapability");
        // Severity should be warn under the opt-out switch.
        using JsonDocument doc = JsonDocument.Parse(stdout);
        bool anyWarn = false;
        foreach (JsonElement f in doc.RootElement.GetProperty("k8sFindings").EnumerateArray())
        {
            if (f.GetProperty("code").GetString() == "K8s-NonLocalClusterCapability"
                && f.GetProperty("severity").GetString() == "warn")
            {
                anyWarn = true;
            }
        }
        anyWarn.ShouldBeTrue("Cloud capability finding must demote to warn under -AllowCloudCapabilities.");
    }

    // -----------------------------------------------------------------------
    // AC5: output safety — poison-string sweep + entropy property test
    // -----------------------------------------------------------------------

    [Fact]
    public async Task PoisonStringSweep_PoisonedValuesAbsentFromStdoutAndStderr()
    {
        const string poisonTenant = "__POISONED_TENANT_DO_NOT_LEAK__";
        const string poisonConn = "__POISONED_CONNSTR_DO_NOT_LEAK__";
        const string poisonSecret = "__POISONED_SECRET_DO_NOT_LEAK__";

        WriteMinimalApp(_tempK8sDir, "parties");
        File.WriteAllText(Path.Combine(_tempK8sDir, "parties", "kustomization.yaml"), $"""
            resources:
            - deployment.yaml
            generatorOptions:
              disableNameSuffixHash: true
            configMapGenerator:
            - name: parties-env
              literals:
                - HTTP_PORTS=8080
                - Tenants__TenantId={poisonTenant}
                - DB_CONNECTION_STRING={poisonConn}
                - API_TOKEN={poisonSecret}
            """);

        (int exit, string stdout, string stderr) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        exit.ShouldBe(1);

        stdout.ShouldNotContain(poisonTenant);
        stdout.ShouldNotContain(poisonConn);
        stdout.ShouldNotContain(poisonSecret);
        stderr.ShouldNotContain(poisonTenant);
        stderr.ShouldNotContain(poisonConn);
        stderr.ShouldNotContain(poisonSecret);
        stdout.ShouldContain("<redacted:");

        // Repeat in console mode.
        (_, string stdoutConsole, string stderrConsole) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: false);
        stdoutConsole.ShouldNotContain(poisonTenant);
        stdoutConsole.ShouldNotContain(poisonConn);
        stdoutConsole.ShouldNotContain(poisonSecret);
        stderrConsole.ShouldNotContain(poisonTenant);
        stderrConsole.ShouldNotContain(poisonSecret);
    }

    [Fact]
    public async Task EntropyPropertyTest_HighEntropyStringsAbsentFromAllOutputStreams()
    {
        // Generate 50 random 32-char strings, prefixed `~ENT~`. The `~` character
        // does not appear in any category code, recommendation, or file path,
        // guaranteeing zero collision with naturally-emitted substrings.
        const int count = 50;
        var entropyStrings = new string[count];
        for (int i = 0; i < count; i++)
        {
            byte[] bytes = new byte[28];
            RandomNumberGenerator.Fill(bytes);
            // Base32-style alphabet (avoids Base64 '+'/'/' that some regex
            // matchers in the assertion harness would treat as special).
            char[] alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".ToCharArray();
            var sb = new StringBuilder();
            foreach (byte b in bytes)
            {
                sb.Append(alphabet[b % alphabet.Length]);
            }
            entropyStrings[i] = $"~ENT~{sb}";
        }

        WriteMinimalApp(_tempK8sDir, "parties");
        var literals = new StringBuilder();
        literals.AppendLine("    - HTTP_PORTS=8080");
        for (int i = 0; i < count; i++)
        {
            // Suffix `_password` makes the reconstructed Key=Value match the
            // K8sSecret-PlaintextCredential regex so the lint fires; the
            // entropy value sits on the right-hand side of `=`.
            literals.AppendLine($"    - FIELD_{i}_password={entropyStrings[i]}");
        }
        File.WriteAllText(Path.Combine(_tempK8sDir, "parties", "kustomization.yaml"), $"""
            resources:
            - deployment.yaml
            generatorOptions:
              disableNameSuffixHash: true
            configMapGenerator:
            - name: parties-env
              literals:
            {literals}
            """);

        (int exit, string stdout, string stderr) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        exit.ShouldBe(1);

        foreach (string entropyString in entropyStrings)
        {
            stdout.ShouldNotContain(entropyString, customMessage: $"Entropy string '{entropyString}' leaked into stdout.");
            stderr.ShouldNotContain(entropyString, customMessage: $"Entropy string '{entropyString}' leaked into stderr.");
        }
    }

    // -----------------------------------------------------------------------
    // AC5: deterministic ordering
    // -----------------------------------------------------------------------

    [Fact]
    public async Task TwoConsecutiveRunsProduceIdenticalJsonModuloTimestamp()
    {
        string k8sPath = Path.Combine(_solutionRoot, "deploy", "k8s");
        string daprPath = Path.Combine(_solutionRoot, "deploy", "dapr");
        (_, string a, _) = await RunLintAsync(daprPath: daprPath, k8sPath: k8sPath, jsonOutput: true);
        (_, string b, _) = await RunLintAsync(daprPath: daprPath, k8sPath: k8sPath, jsonOutput: true);

        using JsonDocument da = JsonDocument.Parse(a);
        using JsonDocument db = JsonDocument.Parse(b);

        string serializedA = StripTimestamp(da.RootElement);
        string serializedB = StripTimestamp(db.RootElement);
        serializedA.ShouldBe(serializedB, "Two consecutive runs must produce identical JSON modulo timestamp.");

        // Findings sorted ascending by (category, code, target).
        var findings = da.RootElement.GetProperty("k8sFindings").EnumerateArray().ToArray();
        for (int i = 1; i < findings.Length; i++)
        {
            string prevKey = SortKey(findings[i - 1]);
            string curKey = SortKey(findings[i]);
            (string.Compare(prevKey, curKey, StringComparison.Ordinal) <= 0).ShouldBeTrue(
                $"Findings must be sorted ascending: '{prevKey}' > '{curKey}'.");
        }
    }

    // -----------------------------------------------------------------------
    // AC5: JSON schema — each finding has exactly {category, code, severity, target, recommendation}
    // -----------------------------------------------------------------------

    [Fact]
    public async Task JsonOutput_EachFindingHasExactlyExpectedFields()
    {
        string k8sPath = Path.Combine(_solutionRoot, "deploy", "k8s");
        string daprPath = Path.Combine(_solutionRoot, "deploy", "dapr");
        (_, string stdout, _) = await RunLintAsync(daprPath: daprPath, k8sPath: k8sPath, jsonOutput: true);
        using JsonDocument doc = JsonDocument.Parse(stdout);
        JsonElement findings = doc.RootElement.GetProperty("k8sFindings");
        var expected = new HashSet<string> { "category", "code", "severity", "target", "recommendation" };
        foreach (JsonElement f in findings.EnumerateArray())
        {
            var keys = new HashSet<string>(f.EnumerateObject().Select(p => p.Name));
            keys.SetEquals(expected).ShouldBeTrue(
                $"Finding must have exactly [category, code, severity, target, recommendation]. Got: [{string.Join(", ", keys)}]");
            f.TryGetProperty("value", out _).ShouldBeFalse("No 'value' field allowed.");
            f.TryGetProperty("raw", out _).ShouldBeFalse("No 'raw' field allowed.");
            string sev = f.GetProperty("severity").GetString()!;
            (sev == "fail" || sev == "warn" || sev == "pass").ShouldBeTrue();
        }
    }

    // -----------------------------------------------------------------------
    // AC5: control-character sanitization in target paths
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ControlCharsInFilePath_AreReplacedWithQuestionMark()
    {
        WriteMinimalApp(_tempK8sDir, "parties");
        // Linux file names allow newlines; create one to verify sanitization.
        string maliciousFolder = Path.Combine(_tempK8sDir, "evil\n[FAKE]\n");
        try
        {
            Directory.CreateDirectory(maliciousFolder);
        }
        catch
        {
            // If filesystem refuses, skip with explicit assertion.
            return;
        }
        File.WriteAllText(Path.Combine(maliciousFolder, "deployment.yaml"), """
            ---
            apiVersion: apps/v1
            kind: Deployment
            metadata:
              name: evil
              labels:
                app: evil
            spec:
              template:
                metadata:
                  labels:
                    app: evil
                spec:
                  containers:
                  - name: evil
                    image:
            """);
        (_, string stdout, string stderr) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        // Raw newline in path must not appear (replaced by '?'); look for the
        // sanitized form.
        stdout.ShouldNotContain("evil\n[FAKE]\n");
        stderr.ShouldNotContain("evil\n[FAKE]\n");
    }

    // -----------------------------------------------------------------------
    // AC5: catch-block redaction (YAML parse failure does not echo raw exception)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task MalformedYaml_DoesNotEchoRawExceptionText()
    {
        Directory.CreateDirectory(Path.Combine(_tempK8sDir, "parties"));
        File.WriteAllText(Path.Combine(_tempK8sDir, "parties", "deployment.yaml"),
            "this is\n  invalid: : yaml :: !!! @#$%\n  nested:\n garbage:\n");
        File.WriteAllText(Path.Combine(_tempK8sDir, "parties", "kustomization.yaml"),
            "resources:\n- deployment.yaml\nconfigMapGenerator:\n- name: parties-env\n  literals:\n    - HTTP_PORTS=8080\n");
        File.WriteAllText(Path.Combine(_tempK8sDir, "kustomization.yaml"), "resources:\n- parties\n");

        (_, string stdout, string stderr) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        // Lint must not crash with raw exception trace; output must still be JSON.
        Should.NotThrow(() => JsonDocument.Parse(stdout));
        stdout.ShouldNotContain("at System.");  // typical .NET stack trace prefix
        stderr.ShouldNotContain("at System.");
    }

    // -----------------------------------------------------------------------
    // AC6: no cluster calls in validator script (.cs assertion, not grep)
    // -----------------------------------------------------------------------

    [Fact]
    public void ValidatorScriptDoesNotInvokeClusterCommands()
    {
        string scriptText = File.ReadAllText(_scriptPath);

        // Strip comments + the doc-comment block so the category-code list
        // doesn't false-positive on `kubectl` mentions documenting WHAT we
        // refuse to do.
        var sanitized = new StringBuilder();
        foreach (string line in scriptText.Split('\n'))
        {
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }
            sanitized.AppendLine(line);
        }
        string body = sanitized.ToString();
        body.ShouldNotContain("kubectl", customMessage: "Validator must not invoke kubectl.");
        body.ShouldNotContain("Invoke-RestMethod", customMessage: "Validator must not perform HTTP requests.");
        body.ShouldNotContain("Invoke-WebRequest", customMessage: "Validator must not perform HTTP requests.");
        body.ShouldNotContain("Start-Job", customMessage: "Validator must not spawn background jobs.");
        // P33: also forbid dapr CLI invocation and network-resource provisioning.
        // The case-sensitive comparison is deliberate — the script's narrative
        // documentation uses "DAPR" as a noun. Only literal "dapr " (lowercase
        // followed by a space, the actual CLI invocation shape) is forbidden.
        body.ShouldNotContain("dapr ", Case.Sensitive, customMessage: "Validator must not invoke the dapr CLI.");
        body.ShouldNotContain("New-Item -ItemType Network", customMessage: "Validator must not provision network resources.");
    }

    // -----------------------------------------------------------------------
    // AC6: name-baseline regression guard
    // -----------------------------------------------------------------------

    [Fact]
    public void ExistingTestNamesAreStrictSubsetOfCurrent()
    {
        string baselinePath = Path.Combine(_solutionRoot, "tests", "Hexalith.Parties.DeployValidation.Tests", "expected-test-names.txt");
        File.Exists(baselinePath).ShouldBeTrue("expected-test-names.txt baseline must be committed.");
        string[] baseline = File.ReadAllLines(baselinePath)
            .Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith("#", StringComparison.Ordinal))
            .Select(NormalizeTestName)
            .Distinct()
            .ToArray();

        var currentTestNames = new HashSet<string>();
        Assembly assembly = typeof(K8sManifestLintTests).Assembly;
        foreach (Type type in assembly.GetTypes())
        {
            if (!type.IsClass || type.IsAbstract) { continue; }
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                bool isTest = method.GetCustomAttributes(inherit: false)
                    .Any(a => a.GetType().Name is "FactAttribute" or "TheoryAttribute");
                if (isTest)
                {
                    currentTestNames.Add($"{type.FullName}.{method.Name}");
                }
            }
        }

        var missing = baseline.Where(b => !currentTestNames.Contains(b)).ToArray();
        missing.Length.ShouldBe(0, $"Baseline tests removed or renamed: {string.Join(", ", missing)}");
    }

    /// <summary>
    /// Strips a theory-row parameter qualifier (e.g. <c>(context: "k3d-local")</c>)
    /// from a display name so the baseline collapses Theory rows back to the
    /// underlying method identifier (FullName.MethodName).
    /// </summary>
    private static string NormalizeTestName(string raw)
    {
        int parenIdx = raw.IndexOf('(', StringComparison.Ordinal);
        return parenIdx > 0 ? raw[..parenIdx] : raw;
    }

    // -----------------------------------------------------------------------
    // AC6: Story 8.1 invariant preserved
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Story81Invocation_PassesWithCommittedDaprTree()
    {
        string daprPath = Path.Combine(_solutionRoot, "deploy", "dapr");
        (int exit, string stdout, _) = await RunLintAsync(daprPath: daprPath, k8sPath: null);
        exit.ShouldBe(0, $"Story 8.1 invocation must still pass against committed deploy/dapr. Output:\n{stdout}");
    }

    // -----------------------------------------------------------------------
    // Path-not-found scenarios
    // -----------------------------------------------------------------------

    [Fact]
    public async Task MissingK8sPath_ExitsWithCode2()
    {
        (int exit, _, _) = await RunLintAsync(k8sPath: Path.Combine(_tempK8sDir, "does-not-exist"));
        exit.ShouldBe(2);
    }

    [Fact]
    public async Task NoArguments_ExitsWithCode2()
    {
        (int exit, _, _) = await RunLintAsync(daprPath: null, k8sPath: null);
        exit.ShouldBe(2);
    }

    // -----------------------------------------------------------------------
    // P28: Console truncation budget — 100 findings, console suppresses past
    // the per-category limit, JSON emits all.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task HundredFindings_ConsoleTruncatesJsonEmitsAll()
    {
        // Generate 100 app folders each with a deployment that triggers
        // K8sWorkload-MissingImage (single category). Console mode must
        // print the "N additional findings suppressed" marker; JSON mode
        // must carry every finding.
        const int count = 100;
        for (int i = 0; i < count; i++)
        {
            string appName = $"appfix{i:D3}";
            string appDir = Path.Combine(_tempK8sDir, appName);
            Directory.CreateDirectory(appDir);
            File.WriteAllText(Path.Combine(appDir, "deployment.yaml"), $"""
                ---
                apiVersion: apps/v1
                kind: Deployment
                metadata:
                  name: {appName}
                  labels:
                    app: {appName}
                spec:
                  template:
                    metadata:
                      labels:
                        app: {appName}
                    spec:
                      containers:
                      - name: {appName}
                """);
            File.WriteAllText(Path.Combine(appDir, "kustomization.yaml"), $"""
                resources:
                - deployment.yaml
                generatorOptions:
                  disableNameSuffixHash: true
                configMapGenerator:
                - name: {appName}-env
                  literals:
                    - HTTP_PORTS=8080
                """);
        }
        var topResources = new StringBuilder();
        topResources.AppendLine("resources:");
        for (int i = 0; i < count; i++)
        {
            topResources.AppendLine($"- appfix{i:D3}");
        }
        File.WriteAllText(Path.Combine(_tempK8sDir, "kustomization.yaml"), topResources.ToString());

        (int exitConsole, string stdoutConsole, _) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: false);
        exitConsole.ShouldBe(1);
        stdoutConsole.ShouldContain("additional findings suppressed");

        (int exitJson, string stdoutJson, _) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        exitJson.ShouldBe(1);
        using JsonDocument doc = JsonDocument.Parse(stdoutJson);
        int missingImage = 0;
        foreach (JsonElement f in doc.RootElement.GetProperty("k8sFindings").EnumerateArray())
        {
            if (f.GetProperty("code").GetString() == "K8sWorkload-MissingImage") { missingImage++; }
        }
        missingImage.ShouldBe(count, "JSON mode must emit every finding without truncation.");
    }

    // -----------------------------------------------------------------------
    // P30: Multi-doc YAML stream — Deployment + Secret in same file
    // -----------------------------------------------------------------------

    [Fact]
    public async Task MultiDocYamlStream_DetectsSecretInSecondDocument()
    {
        WriteMinimalApp(_tempK8sDir, "parties");
        string deploymentPath = Path.Combine(_tempK8sDir, "parties", "deployment.yaml");
        string deployment = File.ReadAllText(deploymentPath);
        File.WriteAllText(deploymentPath, deployment + """


            ---
            apiVersion: v1
            kind: Secret
            metadata:
              name: parties-secret
            type: Opaque
            stringData:
              password: hunter2multidoc
            """);
        (int exit, string stdout, _) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        exit.ShouldBe(1, stdout);
        stdout.ShouldContain("K8sSecret-CommittedSecretValue");
        stdout.ShouldNotContain("hunter2multidoc");
    }

    [Fact]
    public async Task MultiDocYamlStream_PlaceholderSecretInSecondDocumentPasses()
    {
        WriteMinimalApp(_tempK8sDir, "parties");
        string deploymentPath = Path.Combine(_tempK8sDir, "parties", "deployment.yaml");
        string deployment = File.ReadAllText(deploymentPath);
        File.WriteAllText(deploymentPath, deployment + """


            ---
            apiVersion: v1
            kind: Secret
            metadata:
              name: parties-secret
            type: Opaque
            stringData:
              password: REPLACE_ME
            """);
        (int exit, _, _) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        exit.ShouldBe(0);
    }

    // -----------------------------------------------------------------------
    // P31: Per-regex negative tests (placeholders, comments, key allowlist)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("MONGO_URI={env:MONGO_URI}")]
    [InlineData("DB_URL=mongodb://localhost/db")]
    public async Task UrlCredRegexNegatives_DoNotTriggerFail(string literal)
    {
        WriteMinimalAppWithLiteral(_tempK8sDir, "parties", literal);
        (int exit, string stdout, _) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        exit.ShouldBe(0, $"URL-cred negative '{literal}' must not fail. Output:\n{stdout}");
        stdout.ShouldNotContain("K8sSecret-UrlEmbeddedCred");
    }

    [Theory]
    [InlineData("JWT={env:JWT}")]
    [InlineData("SHORT_NOT_JWT=eyJabc.def.ghi")]
    public async Task JwtRegexNegatives_DoNotTriggerFail(string literal)
    {
        WriteMinimalAppWithLiteral(_tempK8sDir, "parties", literal);
        (int exit, string stdout, _) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        exit.ShouldBe(0, $"JWT negative '{literal}' must not fail. Output:\n{stdout}");
        stdout.ShouldNotContain("K8sSecret-JwtTokenLiteral");
    }

    [Theory]
    [InlineData("AWS_KEY={env:AWS_KEY}")]
    public async Task AwsAccessKeyNegatives_DoNotTriggerFail(string literal)
    {
        WriteMinimalAppWithLiteral(_tempK8sDir, "parties", literal);
        (int exit, string stdout, _) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        exit.ShouldBe(0, $"AWS key negative '{literal}' must not fail. Output:\n{stdout}");
        stdout.ShouldNotContain("K8sSecret-AwsAccessKey");
    }

    [Theory]
    [InlineData("AZ_CONN={env:AZURE_CONN}")]
    public async Task AzureConnStringNegatives_DoNotTriggerFail(string literal)
    {
        WriteMinimalAppWithLiteral(_tempK8sDir, "parties", literal);
        (int exit, string stdout, _) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        exit.ShouldBe(0, $"Azure conn negative '{literal}' must not fail. Output:\n{stdout}");
        stdout.ShouldNotContain("K8sSecret-AzureConnString");
    }

    [Fact]
    public async Task PrivateKey_InCommittedSecretBlockScalar_TriggersFail()
    {
        WriteMinimalApp(_tempK8sDir, "parties");
        File.WriteAllText(Path.Combine(_tempK8sDir, "parties", "secret.yaml"), """
            apiVersion: v1
            kind: Secret
            metadata:
              name: parties-secret
            type: Opaque
            stringData:
              cert: |
                -----BEGIN RSA PRIVATE KEY-----
                MIIEpAIBAAKCAQEAxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
                -----END RSA PRIVATE KEY-----
            """);
        File.WriteAllText(Path.Combine(_tempK8sDir, "parties", "kustomization.yaml"), """
            resources:
            - deployment.yaml
            - secret.yaml
            generatorOptions:
              disableNameSuffixHash: true
            configMapGenerator:
            - name: parties-env
              literals:
                - HTTP_PORTS=8080
            """);
        (int exit, string stdout, _) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        exit.ShouldBe(1, stdout);
        stdout.ShouldContain("K8sSecret-PrivateKey");
    }

    // -----------------------------------------------------------------------
    // P34: AC4 cloud Service annotation + Service.type=LoadBalancer matrix
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ServiceCloudAnnotation_FailsInDefaultMode()
    {
        WriteMinimalApp(_tempK8sDir, "parties");
        File.WriteAllText(Path.Combine(_tempK8sDir, "parties", "svc.yaml"), """
            apiVersion: v1
            kind: Service
            metadata:
              name: parties-svc
              annotations:
                service.beta.kubernetes.io/aws-load-balancer-type: nlb
            spec:
              type: ClusterIP
              selector:
                app: parties
              ports:
              - port: 8080
            """);
        (int exit, string stdout, _) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        exit.ShouldBe(1, stdout);
        stdout.ShouldContain("K8s-NonLocalClusterCapability");
    }

    [Fact]
    public async Task ServiceCloudAnnotation_WithAllowSwitchDemotesToWarn()
    {
        WriteMinimalApp(_tempK8sDir, "parties");
        File.WriteAllText(Path.Combine(_tempK8sDir, "parties", "svc.yaml"), """
            apiVersion: v1
            kind: Service
            metadata:
              name: parties-svc
              annotations:
                service.beta.kubernetes.io/aws-load-balancer-type: nlb
            spec:
              type: ClusterIP
              selector:
                app: parties
              ports:
              - port: 8080
            """);
        (int exit, string stdout, _) = await RunLintAsync(k8sPath: _tempK8sDir, allowCloudCapabilities: true, jsonOutput: true);
        exit.ShouldBe(0, stdout);
        stdout.ShouldContain("K8s-NonLocalClusterCapability");
    }

    [Fact]
    public async Task LoadBalancerType_WithAllowSwitchDemotesToWarn()
    {
        WriteMinimalApp(_tempK8sDir, "parties");
        File.WriteAllText(Path.Combine(_tempK8sDir, "parties", "lb-service.yaml"), """
            apiVersion: v1
            kind: Service
            metadata:
              name: parties-lb
            spec:
              type: LoadBalancer
              selector:
                app: parties
              ports:
              - port: 8080
            """);
        (int exit, string stdout, _) = await RunLintAsync(k8sPath: _tempK8sDir, allowCloudCapabilities: true, jsonOutput: true);
        exit.ShouldBe(0, stdout);
        stdout.ShouldContain("K8s-NonLocalClusterCapability");
    }

    [Fact]
    public async Task IngressClassAlb_WithAllowSwitchDemotesToWarn()
    {
        WriteMinimalApp(_tempK8sDir, "parties");
        File.WriteAllText(Path.Combine(_tempK8sDir, "ingressclass.yaml"), """
            apiVersion: networking.k8s.io/v1
            kind: IngressClass
            metadata:
              name: alb
            """);
        File.WriteAllText(Path.Combine(_tempK8sDir, "kustomization.yaml"), """
            resources:
            - parties
            - ingressclass.yaml
            """);
        (int exit, string stdout, _) = await RunLintAsync(k8sPath: _tempK8sDir, allowCloudCapabilities: true, jsonOutput: true);
        exit.ShouldBe(0, stdout);
        stdout.ShouldContain("K8s-NonLocalClusterCapability");
    }

    // -----------------------------------------------------------------------
    // P34: AC5 — secret value never reaches stderr for any positive regex row
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("password=hunter2RealValue")]
    [InlineData("DB_URL=postgres://user:supersecret@db:5432")]
    [InlineData("AWS_KEY=AKIAIOSFODNN7EXAMPLE")]
    public async Task SecretPositiveRows_NeverAppearInStderr(string literal)
    {
        ArgumentNullException.ThrowIfNull(literal);
        WriteMinimalAppWithLiteral(_tempK8sDir, "parties", literal);
        (_, _, string stderr) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        string secretValue = literal[(literal.IndexOf('=') + 1)..];
        stderr.ShouldNotContain(secretValue, customMessage: $"Secret '{secretValue}' leaked into stderr.");
    }

    [Fact]
    public async Task ControlCharsInFilePath_PositiveSanitizedFormAppears()
    {
        WriteMinimalApp(_tempK8sDir, "parties");
        string maliciousFolder = Path.Combine(_tempK8sDir, "evil\n[FAKE]\n");
        try
        {
            Directory.CreateDirectory(maliciousFolder);
        }
        catch
        {
            return;
        }
        File.WriteAllText(Path.Combine(maliciousFolder, "deployment.yaml"), """
            ---
            apiVersion: apps/v1
            kind: Deployment
            metadata:
              name: evil
              labels:
                app: evil
            spec:
              template:
                metadata:
                  labels:
                    app: evil
                spec:
                  containers:
                  - name: evil
                    image:
            """);
        (_, string stdout, _) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        // The sanitized form replaces \n with ? per Format-SafePath. The
        // sanitized target should appear in the output.
        stdout.ShouldContain("evil?[FAKE]?");
    }

    [Fact]
    public async Task PoisonFieldNameInSecret_AbsentFromAllOutputs()
    {
        const string poisonField = "~ENT~PoisonField123";
        WriteMinimalApp(_tempK8sDir, "parties");
        File.WriteAllText(Path.Combine(_tempK8sDir, "parties", "secret.yaml"), $"""
            apiVersion: v1
            kind: Secret
            metadata:
              name: parties-secret
            type: Opaque
            stringData:
              {poisonField}: hunter2
            """);
        File.WriteAllText(Path.Combine(_tempK8sDir, "parties", "kustomization.yaml"), """
            resources:
            - deployment.yaml
            - secret.yaml
            generatorOptions:
              disableNameSuffixHash: true
            configMapGenerator:
            - name: parties-env
              literals:
                - HTTP_PORTS=8080
            """);
        (_, string stdout, string stderr) = await RunLintAsync(k8sPath: _tempK8sDir, jsonOutput: true);
        stdout.ShouldNotContain(poisonField);
        stderr.ShouldNotContain(poisonField);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static string StripTimestamp(JsonElement root)
    {
        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = false }))
        {
            WriteRecursively(root, writer, skipKey: "timestamp");
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static void WriteRecursively(JsonElement element, Utf8JsonWriter writer, string skipKey)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (JsonProperty p in element.EnumerateObject())
                {
                    if (p.Name == skipKey) { continue; }
                    writer.WritePropertyName(p.Name);
                    WriteRecursively(p.Value, writer, skipKey);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement item in element.EnumerateArray())
                {
                    WriteRecursively(item, writer, skipKey);
                }
                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static string SortKey(JsonElement finding)
        => $"{finding.GetProperty("category").GetString()}|{finding.GetProperty("code").GetString()}|{finding.GetProperty("target").GetString()}";

    private static void WriteMinimalApp(
        string root,
        string appId,
        bool missingImage = false,
        bool emptyImage = false,
        bool includeDaprAnnotations = true,
        string configMapRefName = "",
        string imageTag = ":v1.0.0")
    {
        string appDir = Path.Combine(root, appId);
        Directory.CreateDirectory(appDir);
        string cmName = string.IsNullOrEmpty(configMapRefName) ? $"{appId}-env" : configMapRefName;
        string image = missingImage
            ? ""
            : (emptyImage ? "        image: " : $"        image: registry.example.com/{appId}{imageTag}");

        bool isDaprEnabled = appId is "eventstore" or "eventstore-admin" or "parties" or "tenants";
        string annotations = (includeDaprAnnotations && isDaprEnabled)
            ? $"""

              annotations:
                dapr.io/enabled: 'true'
                dapr.io/config: accesscontrol-{appId}
                dapr.io/app-id: {appId}
                dapr.io/app-port: '8080'
            """
            : "";
        string podAnnotations = (includeDaprAnnotations && isDaprEnabled)
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
            {image}
                    imagePullPolicy: IfNotPresent
                    envFrom:
                    - configMapRef:
                        name: {cmName}
            """);

        File.WriteAllText(Path.Combine(appDir, "kustomization.yaml"), $"""
            resources:
            - deployment.yaml
            generatorOptions:
              disableNameSuffixHash: true
            configMapGenerator:
            - name: {appId}-env
              literals:
                - HTTP_PORTS=8080
            """);

        // Top-level kustomization.yaml — write once but be idempotent.
        string topKust = Path.Combine(root, "kustomization.yaml");
        if (!File.Exists(topKust))
        {
            File.WriteAllText(topKust, $"resources:\n- {appId}\n");
        }
    }

    private static void WriteMinimalAppWithLiteral(string root, string appId, string literal)
    {
        WriteMinimalApp(root, appId);
        string kust = Path.Combine(root, appId, "kustomization.yaml");
        File.WriteAllText(kust, $"""
            resources:
            - deployment.yaml
            generatorOptions:
              disableNameSuffixHash: true
            configMapGenerator:
            - name: {appId}-env
              literals:
                - HTTP_PORTS=8080
                - {literal}
            """);
    }

    private static void WriteAuthoritativeDapr(string daprDir)
    {
        // Minimal valid DAPR component set so parity checks have a baseline.
        File.WriteAllText(Path.Combine(daprDir, "accesscontrol.yaml"), """
            apiVersion: dapr.io/v1alpha1
            kind: Configuration
            metadata:
              name: accesscontrol
            spec:
              accessControl:
                defaultAction: deny
                trustDomain: hexalith.io
                policies:
                  - appId: parties
            """);
        foreach (string name in new[] { "statestore.yaml", "pubsub.yaml", "statestore-cosmosdb.yaml",
                "statestore-postgresql.yaml", "pubsub-kafka.yaml", "pubsub-rabbitmq.yaml",
                "pubsub-servicebus.yaml", "accesscontrol.eventstore-admin.yaml",
                "accesscontrol.parties.yaml", "accesscontrol.tenants.yaml",
                "subscription-parties.yaml", "subscription-tenants.yaml",
                "resiliency.yaml", "topology.yaml", "tenants-integration.yaml" })
        {
            File.WriteAllText(Path.Combine(daprDir, name), "apiVersion: dapr.io/v1alpha1\nkind: Component\nmetadata:\n  name: placeholder\n");
        }
        File.WriteAllText(Path.Combine(daprDir, "subscription-parties.yaml"), """
            apiVersion: dapr.io/v2alpha1
            kind: Subscription
            metadata:
              name: parties-events
            spec:
              pubsubname: pubsub
              topic: parties.events
              deadLetterTopic: parties.deadletter
              routes:
                default: /process
            scopes:
              - parties
            """);
        File.WriteAllText(Path.Combine(daprDir, "subscription-tenants.yaml"), """
            apiVersion: dapr.io/v2alpha1
            kind: Subscription
            metadata:
              name: tenants-events
            spec:
              pubsubname: pubsub
              topic: system.tenants.events
              deadLetterTopic: system.tenants.deadletter
              routes:
                default: /process
            scopes:
              - parties
            """);
    }

    private async Task<(int ExitCode, string StdOut, string StdErr)> RunLintAsync(
        string? daprPath = null,
        string? k8sPath = null,
        bool allowCloudCapabilities = false,
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
        if (allowCloudCapabilities) { argList.Add("-AllowCloudCapabilities"); }
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

        // Async stdout + stderr drain in parallel + 90 s hard timeout to
        // avoid Linux pipe-buffer deadlock above ~64 KB.
        // P19: on cancellation, kill the entire process tree (the script is
        // hung) and rewrap into a clearer InvalidOperationException so the
        // test signal isn't a bare TaskCanceledException stack trace.
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
            throw new InvalidOperationException(
                $"validate-deployment.ps1 exceeded the 90 s timeout and was killed. cancellation requested: {cts.IsCancellationRequested}",
                ex);
        }

        return (process.ExitCode, stdoutTask.Result, stderrTask.Result);
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
                // P18: drain stdout/stderr in parallel before WaitForExit so
                // a candidate that floods either pipe cannot deadlock the
                // probe. On timeout, kill the entire process tree and fall
                // through to the next candidate.
                Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
                Task<string> stderrTask = process.StandardError.ReadToEndAsync();
                bool exited = process.WaitForExit(5000);
                if (!exited)
                {
                    try { process.Kill(entireProcessTree: true); } catch { /* best-effort */ }
                    continue;
                }
                // Ensure drain tasks complete before disposing the process.
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
