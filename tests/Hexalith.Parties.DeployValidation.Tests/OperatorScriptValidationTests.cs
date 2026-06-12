using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Hexalith.Parties.DeployValidation.Tests;

[Collection("DeployValidation")]
public sealed class OperatorScriptValidationTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "hexalith-parties-script-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public void PublishAndTeardownUseSharedConfirmContextHelper()
    {
        string helper = ReadRepoFile("deploy/k8s/_lib/Confirm-KubeContext.ps1");
        string publish = ReadRepoFile("deploy/k8s/publish.ps1");
        string teardown = ReadRepoFile("deploy/k8s/teardown.ps1");

        helper.ShouldContain("#Requires -Version 7");
        helper.ShouldContain("Set-StrictMode -Version Latest");
        helper.ShouldContain("function Assert-KubeContext");
        helper.ShouldContain("$FormatKubeContextForOutput");
        helper.ShouldContain("kubectl config current-context");
        helper.ShouldContain("-cne $Expected");

        foreach (string script in new[] { publish, teardown })
        {
            script.ShouldContain("#Requires -Version 7");
            script.ShouldContain("Set-StrictMode -Version Latest");
            script.ShouldContain("_lib/Confirm-KubeContext.ps1");
            script.ShouldContain("Assert-KubeContext -Expected $ConfirmContext");
            script.ShouldContain("exit $ExitContext");
        }
    }

    [Fact]
    public void PublishConfirmContextMismatchStopsBeforeMutation()
    {
        using ShimWorkspace workspace = CreateShimWorkspace(currentContext: "actual-context");

        ProcessResult result = RunPwshScript(
            "deploy/k8s/publish.ps1",
            "-ConfirmContext",
            "expected-context",
            workspace.BinDirectory);

        result.ExitCode.ShouldBe(2);
        result.Output.ShouldContain("expected 'expected-context', got 'actual-context'");
        workspace.LogLines.ShouldBe(["kubectl config current-context"]);
    }

    [Fact]
    public void TeardownConfirmContextMismatchStopsBeforeMutation()
    {
        using ShimWorkspace workspace = CreateShimWorkspace(currentContext: "actual-context");

        ProcessResult result = RunPwshScript(
            "deploy/k8s/teardown.ps1",
            "-ConfirmContext",
            "expected-context",
            workspace.BinDirectory);

        result.ExitCode.ShouldBe(2);
        result.Output.ShouldContain("expected 'expected-context', got 'actual-context'");
        workspace.LogLines.ShouldBe(["kubectl config current-context"]);
    }

    [Theory]
    [InlineData("deploy/k8s/publish.ps1")]
    [InlineData("deploy/k8s/teardown.ps1")]
    public void EmptyCurrentContextStopsBeforeMutation(string scriptPath)
    {
        using ShimWorkspace workspace = CreateShimWorkspace(currentContext: "");

        ProcessResult result = RunPwshScript(scriptPath, "-ConfirmContext", "expected-context", workspace.BinDirectory);

        result.ExitCode.ShouldBe(2);
        result.Output.ShouldContain("expected 'expected-context', got '<empty>'");
        workspace.LogLines.ShouldBe(["kubectl config current-context"]);
    }

    [Fact]
    public void ConfirmContextFailureDoesNotPrintLeakShapedContextData()
    {
        using ShimWorkspace workspace = CreateShimWorkspace(currentContext: "https://cluster.example token eyJabcdef certificate-authority-data");

        ProcessResult result = RunPwshScript(
            "deploy/k8s/publish.ps1",
            "-ConfirmContext",
            "expected-context",
            workspace.BinDirectory);

        result.ExitCode.ShouldBe(2);
        result.Output.ShouldContain("<redacted-context>");
        result.Output.ShouldNotContain("https://cluster.example");
        result.Output.ShouldNotContain("eyJabcdef");
        result.Output.ShouldNotContain("certificate-authority-data");
        workspace.LogLines.ShouldBe(["kubectl config current-context"]);
    }

    [Fact]
    public void TeardownAbsentNamespaceIsSuccessfulNoOp()
    {
        using ShimWorkspace workspace = CreateShimWorkspace(currentContext: "safe-context", namespaceExists: false);

        ProcessResult result = RunPwshScript(
            "deploy/k8s/teardown.ps1",
            "-ConfirmContext",
            "safe-context",
            workspace.BinDirectory);

        result.ExitCode.ShouldBe(0);
        result.Output.ShouldContain("namespace hexalith-parties not present - nothing to delete");
        workspace.LogLines.ShouldBe([
            "kubectl config current-context",
            "kubectl get namespace hexalith-parties"
        ]);
    }

    [Fact]
    public void PublishScriptCentralizesExitCodesAndPhaseMarkers()
    {
        string publish = ReadRepoFile("deploy/k8s/publish.ps1");

        publish.ShouldContain("$ExitGeneral = 1");
        publish.ShouldContain("$ExitContext = 2");
        publish.ShouldContain("$ExitCliMissing = 3");
        publish.ShouldContain("$ExitFolderDrift = 4");
        publish.ShouldContain("$ExitMinVer = 5");
        publish.ShouldContain("$ExitZotAuth = 6");
        publish.ShouldContain("$ExitResidual = 7");

        string[] orderedMarkers =
        [
            "Confirm Kubernetes context",
            "Ensure namespace and preflight external auth",
            "Resolve MinVer image tag",
            "Clean generated deploy/k8s entries",
            "Run dotnet aspirate generate",
            "Strip Aspirate placeholder files",
            "Patch Dapr annotations",
            "Patch Keycloak host alias",
            "Patch UI credential secretKeyRefs",
            "Patch health probes",
            "Patch imagePullSecrets",
            "Verify expected service folders",
            "Verify Zot image manifests",
            "Run static deployment validator",
            "Install or verify Dapr control plane",
            "Dry-run resiliency CR",
            "Ensure Zot pull Secret",
            "Apply Dapr CRs",
            "Reconcile legacy local Keycloak resources",
            "Apply Kubernetes workloads",
            "Wait for workloads to become Ready",
        ];

        int previous = -1;
        foreach (string marker in orderedMarkers)
        {
            int index = publish.IndexOf(marker, StringComparison.Ordinal);
            index.ShouldBeGreaterThan(previous, marker);
            previous = index;
        }
    }

    [Fact]
    public void PublishScriptGuardsCleanupPatchTargetsAndSecretInputs()
    {
        string publish = ReadRepoFile("deploy/k8s/publish.ps1");

        foreach (string preserved in new[] { "redis", "falkordb", "kustomization.yaml", "namespace.yaml", "README.md", "publish.ps1", "teardown.ps1", "_lib" })
        {
            publish.ShouldContain($"'{preserved}'");
        }

        foreach (string generated in new[] { "eventstore", "eventstore-admin", "eventstore-admin-ui", "sample", "sample-blazor-ui", "parties", "parties-mcp", "parties-ui", "tenants", "memories" })
        {
            publish.ShouldContain($"'{generated}'");
        }

        publish.ShouldContain("'eventstore' = 'accesscontrol'");
        publish.ShouldContain("'eventstore-admin' = 'accesscontrol-eventstore-admin'");
        publish.ShouldContain("'parties' = 'accesscontrol-parties'");
        publish.ShouldContain("'tenants' = 'accesscontrol-tenants'");
        publish.ShouldContain("'memories' = 'accesscontrol-memories'");
        publish.ShouldContain("$DaprClientOnlyTargets = @('eventstore-admin-ui', 'sample-blazor-ui')");
        publish.ShouldContain("$ForbiddenDaprTargets = @('parties-mcp', 'parties-ui', 'redis', 'falkordb')");
        publish.ShouldContain("http://auth.tache.ai:8080/realms/tache");
        publish.ShouldContain("https://auth.tache.ai/realms/tache");
        publish.ShouldContain("EventStore__Authentication__Username");
        publish.ShouldContain("EventStore__Authentication__Password");
        publish.ShouldContain("Authentication__OpenIdConnect__ClientSecret");
        publish.ShouldContain("hexalith-parties-ui-oidc-client");
        publish.ShouldContain("imagePullSecrets");
        publish.ShouldContain("zot-pull-secret");
        publish.ShouldContain("Assert-ZotImageManifests");
        publish.ShouldContain("Invoke-DeploymentValidator");
        publish.ShouldContain("credsStore");
        publish.ShouldContain("credHelpers");
        publish.ShouldContain("Docker config uses credsStore");
        publish.ShouldContain("helper-backed auth is not supported");
    }

    [Fact]
    public void PublishScriptKeepsDaprApplyOrderAndSkipInitCrdCheck()
    {
        string publish = ReadRepoFile("deploy/k8s/publish.ps1");

        publish.ShouldContain("if ($SkipDaprInit)");
        publish.ShouldContain("components.dapr.io");
        publish.ShouldContain("configurations.dapr.io");
        publish.ShouldContain("subscriptions.dapr.io");
        publish.ShouldContain("resiliencies.dapr.io");
        publish.ShouldContain("dapr init -k --wait --timeout 300");
        publish.ShouldNotContain("deploy/dapr-alternatives");

        string[] orderedFiles =
        [
            "statestore.yaml",
            "pubsub.yaml",
            "resiliency.yaml",
            "accesscontrol.yaml",
            "accesscontrol-eventstore-admin.yaml",
            "accesscontrol-sample.yaml",
            "accesscontrol-parties.yaml",
            "accesscontrol-tenants.yaml",
            "accesscontrol-memories.yaml",
            "subscription-parties.yaml",
            "subscription-tenants.yaml",
        ];

        int orderedSectionStart = publish.IndexOf("$orderedFiles = @(", StringComparison.Ordinal);
        orderedSectionStart.ShouldBeGreaterThan(0);
        string orderedSection = publish[orderedSectionStart..];

        int previous = -1;
        foreach (string file in orderedFiles)
        {
            int index = orderedSection.IndexOf($"'{file}'", StringComparison.Ordinal);
            index.ShouldBeGreaterThan(previous, file);
            previous = index;
        }
    }

    [Fact]
    public void TeardownScriptHasResidualAndExplicitPurgeContracts()
    {
        string teardown = ReadRepoFile("deploy/k8s/teardown.ps1");

        teardown.ShouldContain("namespace $Namespace not present - nothing to delete");
        teardown.ShouldContain("kubectl' @('delete', '-k'");
        teardown.ShouldContain("kubectl' @('delete', '-f'");
        teardown.ShouldContain("Residual state detected - manual intervention required before next publish");
        teardown.ShouldContain("hexalith-keycloak-admin");
        teardown.ShouldContain("deployment.apps/keycloak");
        teardown.ShouldContain("service/keycloak");
        teardown.ShouldContain("configmap/keycloak-realm");
        teardown.ShouldContain("$PurgeNamespace");
        teardown.ShouldContain("$PurgeDapr");
        teardown.ShouldContain("'dapr' @('uninstall', '-k', '--all')");
    }

    [Fact]
    public void TeardownDefaultDeletesOwnedKustomizationsWithoutDeletingNamespace()
    {
        using ScriptWorkspace workspace = CreateScriptWorkspace();

        ProcessResult result = RunPwshScriptPath(
            workspace.TeardownScript,
            workspace.BinDirectory,
            workspace.Environment,
            "-ConfirmContext",
            "safe-context");

        result.ExitCode.ShouldBe(0, result.Output);
        workspace.LogLines.ShouldContain(line => line.Contains("delete -k", StringComparison.Ordinal) && line.Contains("/eventstore", StringComparison.Ordinal));
        workspace.LogLines.ShouldContain(line => line.Contains("delete -f", StringComparison.Ordinal) && line.Contains("ingress.yaml", StringComparison.Ordinal));
        workspace.LogLines.ShouldNotContain(line => line == $"kubectl delete -k {workspace.K8sRoot} --ignore-not-found=true");
        workspace.LogLines.ShouldNotContain(line => line.Contains("delete namespace hexalith-parties", StringComparison.Ordinal));
    }

    [Fact]
    public void TeardownPurgeNamespaceFailsClosedWhenResourceEnumerationFails()
    {
        using ScriptWorkspace workspace = CreateScriptWorkspace(failNamespaceResourceEnumeration: true);

        ProcessResult result = RunPwshScriptPath(
            workspace.TeardownScript,
            workspace.BinDirectory,
            workspace.Environment,
            "-ConfirmContext",
            "safe-context",
            "-PurgeNamespace");

        result.ExitCode.ShouldBe(7, result.Output);
        result.Output.ShouldContain("unable to list namespaced resource");
        workspace.LogLines.ShouldNotContain(line => line.Contains("delete namespace hexalith-parties", StringComparison.Ordinal));
    }

    [Fact]
    public void PublishPatchesGeneratedManifestsAndKeepsDaprDryRunSingle()
    {
        using ScriptWorkspace workspace = CreateScriptWorkspace(includeLiteralJwt: true);

        ProcessResult result = RunPwshScriptPath(
            workspace.PublishScript,
            workspace.BinDirectory,
            workspace.Environment,
            "-ConfirmContext",
            "safe-context",
            "-SkipDaprInit");

        result.ExitCode.ShouldBe(0, result.Output);

        string eventstore = File.ReadAllText(Path.Combine(workspace.K8sRoot, "eventstore", "deployment.yaml"));
        eventstore.ShouldContain("dapr.io/enabled: \"true\"");
        eventstore.ShouldContain("dapr.io/app-id: eventstore");
        eventstore.ShouldContain("dapr.io/app-port: '8080'");
        eventstore.ShouldContain("dapr.io/config: accesscontrol");
        eventstore.ShouldContain("imagePullSecrets:");
        eventstore.ShouldContain("- name: zot-pull-secret");
        eventstore.ShouldContain("readinessProbe:");
        eventstore.ShouldContain("path: /health");
        eventstore.ShouldContain("port: http");
        CountOccurrences(eventstore, "timeoutSeconds: 10").ShouldBe(2);
        eventstore.ShouldContain("livenessProbe:");
        eventstore.ShouldContain("hostAliases:");
        eventstore.ShouldContain("ip: 10.96.42.17");
        eventstore.ShouldContain("auth.tache.ai");
        eventstore.ShouldContain("internal.example");
        eventstore.ShouldContain("envFrom:");
        eventstore.ShouldContain("terminationGracePeriodSeconds: 180");
        eventstore.ShouldNotContain("Authentication__JwtBearer__SigningKey");
        eventstore.ShouldNotContain("hexalith-jwt-signing");
        eventstore.ShouldNotContain("value: literal-secret");

        string adminUi = File.ReadAllText(Path.Combine(workspace.K8sRoot, "eventstore-admin-ui", "deployment.yaml"));
        adminUi.ShouldContain("dapr.io/enabled: \"true\"");
        adminUi.ShouldContain("dapr.io/app-id: eventstore-admin-ui");
        adminUi.ShouldNotContain("dapr.io/app-port");
        adminUi.ShouldNotContain("dapr.io/config");
        adminUi.ShouldContain("imagePullSecrets:");
        adminUi.ShouldNotContain("hostAliases:");
        adminUi.ShouldNotContain("auth.tache.ai");
        adminUi.ShouldContain("EventStore__Authentication__Username");
        adminUi.ShouldContain("EventStore__Authentication__Password");
        adminUi.ShouldContain("secretKeyRef:");
        adminUi.ShouldContain("name: hexalith-tache-ui-credentials");
        string adminUiKustomization = File.ReadAllText(Path.Combine(workspace.K8sRoot, "eventstore-admin-ui", "kustomization.yaml"));
        adminUiKustomization.ShouldContain("EventStore__Authentication__Authority=https://auth.tache.ai/realms/tache");
        adminUiKustomization.ShouldContain("EventStore__Authentication__Issuer=https://auth.tache.ai/realms/tache");
        adminUiKustomization.ShouldNotContain("EventStore__Authentication__Authority=http://auth.tache.ai:8080/realms/tache");
        adminUiKustomization.ShouldNotContain("EventStore__Authentication__Issuer=http://auth.tache.ai:8080/realms/tache");

        string partiesUi = File.ReadAllText(Path.Combine(workspace.K8sRoot, "parties-ui", "deployment.yaml"));
        partiesUi.ShouldNotContain("dapr.io/enabled");
        partiesUi.ShouldNotContain("dapr.io/app-id");
        partiesUi.ShouldNotContain("dapr.io/app-port");
        partiesUi.ShouldNotContain("dapr.io/config");
        partiesUi.ShouldContain("Authentication__OpenIdConnect__ClientSecret");
        partiesUi.ShouldContain("secretKeyRef:");
        partiesUi.ShouldContain("name: hexalith-parties-ui-oidc-client");
        partiesUi.ShouldContain("key: client-secret");

        workspace.LogLines.ShouldContain("zot manifest eventstore 0.1.1-preview.0.7");
        workspace.LogLines.ShouldContain("zot manifest memories 0.1.1-preview.0.7");
        workspace.LogLines.ShouldContain("zot manifest parties-ui 0.1.1-preview.0.7");
        workspace.LogLines.ShouldContain(line => line.Contains("run keycloak-tache-preflight", StringComparison.Ordinal));
        workspace.LogLines.ShouldContain(line => line.Contains("delete deployment/keycloak service/keycloak configmap/keycloak-realm secret/hexalith-keycloak-admin", StringComparison.Ordinal));
        workspace.LogLines.ShouldContain(line => line.StartsWith("validator --config-path ", StringComparison.Ordinal));
        workspace.LogLines.Count(line => line.Contains("resiliency.yaml --dry-run=server", StringComparison.Ordinal)).ShouldBe(1);
        int statestore = Array.FindIndex(workspace.LogLines, line => line.Contains("statestore.yaml", StringComparison.Ordinal));
        int pubsub = Array.FindIndex(workspace.LogLines, line => line.Contains("pubsub.yaml", StringComparison.Ordinal));
        int resiliency = Array.FindIndex(workspace.LogLines, line => line.Contains("resiliency.yaml", StringComparison.Ordinal) && !line.Contains("--dry-run=server", StringComparison.Ordinal));
        int workloads = Array.FindIndex(workspace.LogLines, line => line == $"kubectl apply -k {workspace.K8sRoot}");
        int restart = Array.FindIndex(workspace.LogLines, line => line.Contains("rollout restart deployment/eventstore", StringComparison.Ordinal));
        int ready = Array.FindIndex(workspace.LogLines, line => line.Contains("rollout status deployment/eventstore", StringComparison.Ordinal));
        statestore.ShouldBeGreaterThan(0);
        pubsub.ShouldBeGreaterThan(statestore);
        resiliency.ShouldBeGreaterThan(pubsub);
        workloads.ShouldBeGreaterThan(resiliency);
        restart.ShouldBeGreaterThan(workloads);
        ready.ShouldBeGreaterThan(restart);
    }

    [Fact]
    public void PublishCanRunTwiceWithoutDuplicatingPatchArtifactsOrChangingCarveOuts()
    {
        using ScriptWorkspace workspace = CreateScriptWorkspace(includeLiteralJwt: true);
        Dictionary<string, string> carveOutBaselines = new(StringComparer.Ordinal)
        {
            ["redis"] = File.ReadAllText(Path.Combine(workspace.K8sRoot, "redis", "deployment.yaml")),
            ["falkordb"] = File.ReadAllText(Path.Combine(workspace.K8sRoot, "falkordb", "deployment.yaml")),
        };

        for (int i = 0; i < 2; i++)
        {
            ProcessResult result = RunPwshScriptPath(
                workspace.PublishScript,
                workspace.BinDirectory,
                workspace.Environment,
                "-ConfirmContext",
                "safe-context",
                "-SkipDaprInit");

            result.ExitCode.ShouldBe(0, result.Output);
        }

        string eventstore = File.ReadAllText(Path.Combine(workspace.K8sRoot, "eventstore", "deployment.yaml"));
        CountOccurrences(eventstore, "dapr.io/enabled: \"true\"").ShouldBe(1);
        CountOccurrences(eventstore, "dapr.io/app-id: eventstore").ShouldBe(1);
        CountOccurrences(eventstore, "dapr.io/app-port: '8080'").ShouldBe(1);
        CountOccurrences(eventstore, "dapr.io/config: accesscontrol").ShouldBe(1);
        CountOccurrences(eventstore, "imagePullSecrets:").ShouldBe(1);
        CountOccurrences(eventstore, "- name: zot-pull-secret").ShouldBe(1);
        CountOccurrences(eventstore, "hostAliases:").ShouldBe(1);
        CountOccurrences(eventstore, "auth.tache.ai").ShouldBe(1);
        CountOccurrences(eventstore, "internal.example").ShouldBe(1);
        eventstore.ShouldContain("envFrom:");
        eventstore.ShouldContain("terminationGracePeriodSeconds: 180");
        eventstore.ShouldNotContain("Authentication__JwtBearer__SigningKey");
        eventstore.ShouldNotContain("hexalith-jwt-signing");
        CountOccurrences(eventstore, "secretKeyRef:").ShouldBe(0);
        CountOccurrences(eventstore, "readinessProbe:").ShouldBe(1);
        CountOccurrences(eventstore, "livenessProbe:").ShouldBe(1);
        CountOccurrences(eventstore, "timeoutSeconds: 10").ShouldBe(2);
        eventstore.ShouldContain("path: /health");
        eventstore.ShouldContain("port: http");
        eventstore.ShouldNotContain("value: literal-secret");

        string adminUi = File.ReadAllText(Path.Combine(workspace.K8sRoot, "eventstore-admin-ui", "deployment.yaml"));
        CountOccurrences(adminUi, "dapr.io/enabled: \"true\"").ShouldBe(1);
        CountOccurrences(adminUi, "dapr.io/app-id: eventstore-admin-ui").ShouldBe(1);
        adminUi.ShouldNotContain("dapr.io/app-port");
        adminUi.ShouldNotContain("dapr.io/config");
        CountOccurrences(adminUi, "hostAliases:").ShouldBe(0);
        CountOccurrences(adminUi, "EventStore__Authentication__Username").ShouldBe(1);
        CountOccurrences(adminUi, "EventStore__Authentication__Password").ShouldBe(1);
        CountOccurrences(adminUi, "secretKeyRef:").ShouldBe(2);
        CountOccurrences(adminUi, "name: hexalith-tache-ui-credentials").ShouldBe(2);
        string adminUiKustomization = File.ReadAllText(Path.Combine(workspace.K8sRoot, "eventstore-admin-ui", "kustomization.yaml"));
        CountOccurrences(adminUiKustomization, "https://auth.tache.ai/realms/tache").ShouldBe(2);
        adminUiKustomization.ShouldNotContain("http://auth.tache.ai:8080/realms/tache");

        string partiesUi = File.ReadAllText(Path.Combine(workspace.K8sRoot, "parties-ui", "deployment.yaml"));
        CountOccurrences(partiesUi, "Authentication__OpenIdConnect__ClientSecret").ShouldBe(1);
        CountOccurrences(partiesUi, "secretKeyRef:").ShouldBe(1);
        CountOccurrences(partiesUi, "name: hexalith-parties-ui-oidc-client").ShouldBe(1);
        partiesUi.ShouldNotContain("dapr.io/");

        foreach ((string folder, string baseline) in carveOutBaselines)
        {
            string deployment = File.ReadAllText(Path.Combine(workspace.K8sRoot, folder, "deployment.yaml"));
            deployment.ShouldBe(baseline, $"{folder} must remain byte-identical after repeated publish cleanup and patch cycles.");
            deployment.ShouldNotContain("dapr.io/");
            deployment.ShouldNotContain("Authentication__JwtBearer__SigningKey");
            deployment.ShouldNotContain("zot-pull-secret");
            deployment.ShouldNotContain("imagePullSecrets:");
        }
    }

    [Fact]
    public void PublishClearsInheritedPluralContainerImageTagsBeforeAspirate()
    {
        using ScriptWorkspace workspace = CreateScriptWorkspace(inheritedPluralContainerImageTags: "staging-latest");

        ProcessResult result = RunPwshScriptPath(
            workspace.PublishScript,
            workspace.BinDirectory,
            workspace.Environment,
            "-ConfirmContext",
            "safe-context",
            "-SkipDaprInit");

        result.ExitCode.ShouldBe(0, result.Output);
        workspace.LogLines.ShouldNotContain("plural ContainerImageTags leaked into aspirate");
    }

    [Theory]
    [InlineData("username")]
    [InlineData("password")]
    public void PublishFailsBeforeGenerateWhenUiCredentialSecretKeyIsMissing(string missingKey)
    {
        using ScriptWorkspace workspace = CreateScriptWorkspace(missingUiCredentialKey: missingKey);

        ProcessResult result = RunPwshScriptPath(
            workspace.PublishScript,
            workspace.BinDirectory,
            workspace.Environment,
            "-ConfirmContext",
            "safe-context",
            "-SkipDaprInit");

        result.ExitCode.ShouldBe(1, result.Output);
        result.Output.ShouldContain($"required UI credential Secret hexalith-tache-ui-credentials key '{missingKey}' is missing");
        workspace.LogLines.ShouldNotContain(line => line.StartsWith("dotnet ", StringComparison.Ordinal));
    }

    [Fact]
    public void PublishFailsBeforeGenerateWhenPartiesUiOidcClientSecretKeyIsMissing()
    {
        using ScriptWorkspace workspace = CreateScriptWorkspace(missingPartiesUiOidcSecret: true);

        ProcessResult result = RunPwshScriptPath(
            workspace.PublishScript,
            workspace.BinDirectory,
            workspace.Environment,
            "-ConfirmContext",
            "safe-context",
            "-SkipDaprInit");

        result.ExitCode.ShouldBe(1, result.Output);
        result.Output.ShouldContain("required parties-ui OIDC Secret hexalith-parties-ui-oidc-client key 'client-secret' is missing");
        workspace.LogLines.ShouldNotContain(line => line.StartsWith("dotnet ", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null, "read failed")]
    [InlineData("None", "has no clusterIP")]
    [InlineData("not-an-ip", "invalid clusterIP")]
    public void PublishFailsBeforeGenerateWhenKeycloakServicePreflightFails(string? clusterIp, string expectedMessage)
    {
        using ScriptWorkspace workspace = CreateScriptWorkspace(keycloakClusterIp: clusterIp);

        ProcessResult result = RunPwshScriptPath(
            workspace.PublishScript,
            workspace.BinDirectory,
            workspace.Environment,
            "-ConfirmContext",
            "safe-context",
            "-SkipDaprInit");

        result.ExitCode.ShouldBe(1, result.Output);
        result.Output.ShouldContain(expectedMessage);
        workspace.LogLines.ShouldNotContain(line => line.StartsWith("dotnet ", StringComparison.Ordinal));
    }

    [Fact]
    public void PublishFailsBeforeGenerateWhenKeycloakTokenContractIsInvalid()
    {
        using ScriptWorkspace workspace = CreateScriptWorkspace(invalidKeycloakToken: true);

        ProcessResult result = RunPwshScriptPath(
            workspace.PublishScript,
            workspace.BinDirectory,
            workspace.Environment,
            "-ConfirmContext",
            "safe-context",
            "-SkipDaprInit");

        result.ExitCode.ShouldBe(1, result.Output);
        result.Output.ShouldContain("Keycloak token claim 'aud' does not include required value 'hexalith-eventstore'");
        workspace.LogLines.ShouldNotContain(line => line.StartsWith("dotnet ", StringComparison.Ordinal));
    }

    [Fact]
    public void PublishReplacesReadinessOnlyGeneratedProbeWithoutDuplicatingKeys()
    {
        using ScriptWorkspace workspace = CreateScriptWorkspace(generatedReadinessOnlyProbe: true);

        ProcessResult result = RunPwshScriptPath(
            workspace.PublishScript,
            workspace.BinDirectory,
            workspace.Environment,
            "-ConfirmContext",
            "safe-context",
            "-SkipDaprInit");

        result.ExitCode.ShouldBe(0, result.Output);

        string eventstore = File.ReadAllText(Path.Combine(workspace.K8sRoot, "eventstore", "deployment.yaml"));
        CountOccurrences(eventstore, "readinessProbe:").ShouldBe(1);
        CountOccurrences(eventstore, "livenessProbe:").ShouldBe(1);
        CountOccurrences(eventstore, "timeoutSeconds: 10").ShouldBe(2);
        eventstore.ShouldContain("path: /health");
        eventstore.ShouldContain("port: http");
        eventstore.ShouldNotContain("tcpSocket:");
    }

    [Fact]
    public void PublishFailsWhenExistingDaprNamespaceIsUnhealthy()
    {
        using ScriptWorkspace workspace = CreateScriptWorkspace(daprStatusFailsExistingInstall: true);

        ProcessResult result = RunPwshScriptPath(
            workspace.PublishScript,
            workspace.BinDirectory,
            workspace.Environment,
            "-ConfirmContext",
            "safe-context");

        result.ExitCode.ShouldBe(1, result.Output);
        result.Output.ShouldContain("existing Dapr control plane is unhealthy");
        workspace.LogLines.ShouldContain("dapr status -k");
        workspace.LogLines.ShouldContain("kubectl get namespace dapr-system --ignore-not-found=true -o name");
        workspace.LogLines.ShouldNotContain("dapr init -k --wait --timeout 300");
    }

    [Fact]
    public void PublishFailsOnUnexpectedTopLevelFileEmittedByAspirate()
    {
        using ScriptWorkspace workspace = CreateScriptWorkspace(emitUnexpectedTopLevelFile: true);

        ProcessResult result = RunPwshScriptPath(
            workspace.PublishScript,
            workspace.BinDirectory,
            workspace.Environment,
            "-ConfirmContext",
            "safe-context",
            "-SkipDaprInit");

        result.ExitCode.ShouldBe(4, result.Output);
        result.Output.ShouldContain("unknown deploy/k8s top-level entries refused during post-generation validation");
    }

    [Fact]
    public void PublishRoutesNullDockerAuthsThroughExitSix()
    {
        using ScriptWorkspace workspace = CreateScriptWorkspace(dockerConfigJson: """{"auths":null}""");

        ProcessResult result = RunPwshScriptPath(
            workspace.PublishScript,
            workspace.BinDirectory,
            workspace.Environment,
            "-ConfirmContext",
            "safe-context",
            "-SkipDaprInit");

        result.ExitCode.ShouldBe(6, result.Output);
        result.Output.ShouldContain("Docker config missing auths");
    }

    private static string RepositoryRoot
    {
        get
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
        }
    }

    private static string ReadRepoFile(string relativePath)
        => File.ReadAllText(Path.Combine(RepositoryRoot, relativePath));

    private static string PwshPath => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "pwsh.exe" : "pwsh";

    private static string DotnetPath =>
        Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } hostPath
            ? hostPath
            : ResolveCommandPath(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dotnet.exe" : "dotnet");

    private static string ResolveCommandPath(string command)
    {
        if (Path.IsPathFullyQualified(command) && File.Exists(command))
        {
            return command;
        }

        string[] names = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && Path.GetExtension(command).Length == 0
            ? [command, $"{command}.exe", $"{command}.cmd", $"{command}.bat"]
            : [command];
        foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            foreach (string name in names)
            {
                string candidate = Path.Combine(directory, name);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return command;
    }

    private static ProcessResult RunPwshScript(string relativeScriptPath, string argumentName, string argumentValue, string shimBinDirectory)
    {
        ProcessStartInfo start = new()
        {
            FileName = PwshPath,
            WorkingDirectory = RepositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(Path.Combine(RepositoryRoot, relativeScriptPath));
        start.ArgumentList.Add(argumentName);
        start.ArgumentList.Add(argumentValue);
        start.Environment["PATH"] = shimBinDirectory + Path.PathSeparator + start.Environment["PATH"];

        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Failed to start pwsh.");
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new ProcessResult(process.ExitCode, stdout + stderr);
    }

    private static ProcessResult RunPwshScriptPath(
        string scriptPath,
        string shimBinDirectory,
        IReadOnlyDictionary<string, string> environment,
        params string[] arguments)
    {
        ProcessStartInfo start = new()
        {
            FileName = PwshPath,
            WorkingDirectory = Path.GetDirectoryName(scriptPath)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(scriptPath);
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment["PATH"] = shimBinDirectory + Path.PathSeparator + start.Environment["PATH"];
        foreach (KeyValuePair<string, string> item in environment)
        {
            start.Environment[item.Key] = item.Value;
        }

        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Failed to start pwsh.");
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new ProcessResult(process.ExitCode, stdout + stderr);
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private ScriptWorkspace CreateScriptWorkspace(
        bool failNamespaceResourceEnumeration = false,
        bool includeLiteralJwt = false,
        bool emitUnexpectedTopLevelFile = false,
        bool generatedReadinessOnlyProbe = false,
        bool daprStatusFailsExistingInstall = false,
        string? keycloakClusterIp = "10.96.42.17",
        string? missingUiCredentialKey = null,
        bool invalidKeycloakToken = false,
        string? inheritedPluralContainerImageTags = null,
        bool missingPartiesUiOidcSecret = false,
        string dockerConfigJson = """{"auths":{"registry.hexalith.com":{"auth":"dXNlcjpwYXNz"}}}""")
    {
        string root = Path.Combine(_tempRoot, Guid.NewGuid().ToString("N"));
        string k8sRoot = Path.Combine(root, "deploy", "k8s");
        string daprRoot = Path.Combine(root, "deploy", "dapr");
        string appHostRoot = Path.Combine(root, "src", "Hexalith.Parties.AppHost");
        string bin = Path.Combine(root, "bin");
        string docker = Path.Combine(root, "docker");
        Directory.CreateDirectory(Path.Combine(k8sRoot, "_lib"));
        Directory.CreateDirectory(daprRoot);
        Directory.CreateDirectory(appHostRoot);
        Directory.CreateDirectory(bin);
        Directory.CreateDirectory(docker);

        File.Copy(Path.Combine(RepositoryRoot, "global.json"), Path.Combine(root, "global.json"));
        File.WriteAllText(Path.Combine(root, "deploy", "validate-deployment.ps1"), """
#Requires -Version 7
Add-Content -LiteralPath $env:SCRIPT_TEST_LOG -Value "validator $($args -join ' ')"
exit 0
""");
        File.Copy(Path.Combine(RepositoryRoot, "deploy", "k8s", "publish.ps1"), Path.Combine(k8sRoot, "publish.ps1"));
        File.Copy(Path.Combine(RepositoryRoot, "deploy", "k8s", "teardown.ps1"), Path.Combine(k8sRoot, "teardown.ps1"));
        File.Copy(Path.Combine(RepositoryRoot, "deploy", "k8s", "_lib", "Confirm-KubeContext.ps1"), Path.Combine(k8sRoot, "_lib", "Confirm-KubeContext.ps1"));
        File.WriteAllText(Path.Combine(k8sRoot, "kustomization.yaml"), """
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
namespace: hexalith-parties
resources:
  - namespace.yaml
  - eventstore
  - eventstore-admin
  - eventstore-admin-ui
  - memories
  - parties
  - parties-mcp
  - parties-ui
  - tenants
  - redis
  - falkordb
""");
        File.WriteAllText(Path.Combine(k8sRoot, "namespace.yaml"), "apiVersion: v1\nkind: Namespace\nmetadata:\n  name: hexalith-parties\n");
        File.WriteAllText(Path.Combine(k8sRoot, "README.md"), "test\n");

        foreach (string folder in new[] { "redis", "falkordb" })
        {
            Directory.CreateDirectory(Path.Combine(k8sRoot, folder));
            File.WriteAllText(Path.Combine(k8sRoot, folder, "deployment.yaml"), DeploymentYaml(folder, $"quay.io/{folder}/{folder}:test", includeDapr: false, includeLiteralJwt: false));
            File.WriteAllText(Path.Combine(k8sRoot, folder, "kustomization.yaml"), $"resources:\n- deployment.yaml\n");
        }

        foreach (string file in new[]
        {
            "statestore.yaml",
            "pubsub.yaml",
            "resiliency.yaml",
            "accesscontrol.yaml",
            "accesscontrol-eventstore-admin.yaml",
            "accesscontrol-parties.yaml",
            "accesscontrol-tenants.yaml",
            "accesscontrol-memories.yaml",
            "subscription-parties.yaml",
            "subscription-tenants.yaml",
        })
        {
            File.WriteAllText(Path.Combine(daprRoot, file), "apiVersion: dapr.io/v1alpha1\nkind: Test\nmetadata:\n  name: test\n");
        }

        File.WriteAllText(Path.Combine(docker, "config.json"), dockerConfigJson);

        string logPath = Path.Combine(root, "commands.log");
        WriteKubectlShim(bin, logPath, failNamespaceResourceEnumeration, daprStatusFailsExistingInstall, keycloakClusterIp, missingUiCredentialKey, invalidKeycloakToken, missingPartiesUiOidcSecret);
        WriteDotnetShim(bin, logPath, includeLiteralJwt, emitUnexpectedTopLevelFile, generatedReadinessOnlyProbe);
        WriteDaprShim(bin, logPath, daprStatusFailsExistingInstall);

        Dictionary<string, string> environment = new(StringComparer.Ordinal)
        {
            ["SCRIPT_TEST_LOG"] = logPath,
            ["DOCKER_CONFIG"] = docker,
        };
        if (!string.IsNullOrEmpty(inheritedPluralContainerImageTags))
        {
            environment["ContainerImageTags"] = inheritedPluralContainerImageTags;
        }

        return new ScriptWorkspace(root, k8sRoot, bin, logPath, environment);
    }

    private ShimWorkspace CreateShimWorkspace(string currentContext, bool namespaceExists = true)
    {
        string root = Path.Combine(_tempRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string bin = Path.Combine(root, "bin");
        Directory.CreateDirectory(bin);
        string logPath = Path.Combine(root, "commands.log");

        string kubectlPath = Path.Combine(bin, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "kubectl.cmd" : "kubectl");
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            File.WriteAllText(kubectlPath, $"""
@echo off
echo kubectl %*>> "{logPath}"
if "%1 %2 %3"=="config current-context" (
  echo {currentContext}
  exit /b 0
)
if "%1 %2 %3"=="get namespace hexalith-parties" (
  exit /b {(namespaceExists ? 0 : 1)}
)
exit /b 1
""");
        }
        else
        {
            File.WriteAllText(kubectlPath, $"""
#!/usr/bin/env bash
printf 'kubectl %s\n' "$*" >> "{logPath}"
if [ "$1 $2" = "config current-context" ]; then
  printf '%s\n' "{currentContext}"
  exit 0
fi
if [ "$1 $2 $3" = "get namespace hexalith-parties" ]; then
  exit {(namespaceExists ? 0 : 1)}
fi
exit 1
""");
            File.SetUnixFileMode(kubectlPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return new ShimWorkspace(root, bin, logPath);
    }

    private static void WriteKubectlShim(
        string binDirectory,
        string logPath,
        bool failNamespaceResourceEnumeration,
        bool daprStatusFailsExistingInstall,
        string? keycloakClusterIp,
        string? missingUiCredentialKey,
        bool invalidKeycloakToken,
        bool missingPartiesUiOidcSecret)
    {
        string kubectlPath = Path.Combine(binDirectory, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "kubectl.cmd" : "kubectl");
        string validPayload = "eyJpc3MiOiJodHRwOi8vYXV0aC50YWNoZS5haTo4MDgwL3JlYWxtcy90YWNoZSIsImF1ZCI6WyJoZXhhbGl0aC1ldmVudHN0b3JlIl0sImV2ZW50c3RvcmU6dGVuYW50IjpbInRlbmFudC1hIl0sImV2ZW50c3RvcmU6ZG9tYWluIjpbImNvdW50ZXIiXSwiZXZlbnRzdG9yZTpwZXJtaXNzaW9uIjpbImNvbW1hbmRzOioiXX0";
        string invalidPayload = "eyJpc3MiOiJodHRwOi8vYXV0aC50YWNoZS5haTo4MDgwL3JlYWxtcy90YWNoZSIsImF1ZCI6WyJvdGhlciJdLCJldmVudHN0b3JlOnRlbmFudCI6WyJ0ZW5hbnQtYSJdLCJldmVudHN0b3JlOmRvbWFpbiI6WyJwYXJ0eSJdLCJldmVudHN0b3JlOnBlcm1pc3Npb24iOlsicXVlcnk6cmVhZCJdfQ";
        string tokenPayload = invalidKeycloakToken ? invalidPayload : validPayload;
        string keycloakServiceExit = keycloakClusterIp is null ? "1" : "0";
        string keycloakServiceOutput = keycloakClusterIp ?? string.Empty;
        string usernameSecretExit = string.Equals(missingUiCredentialKey, "username", StringComparison.Ordinal) ? "1" : "0";
        string passwordSecretExit = string.Equals(missingUiCredentialKey, "password", StringComparison.Ordinal) ? "1" : "0";
        string partiesUiSecretExit = missingPartiesUiOidcSecret ? "1" : "0";
        string tokenJson = "{\"access_token\":\"eyJhbGciOiJub25lIn0." + tokenPayload + ".signature\"}";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            File.WriteAllText(kubectlPath, $"""
@echo off
echo kubectl %*>> "{logPath}"
if "%1 %2"=="config current-context" (
  echo safe-context
  exit /b 0
)
if "%1 %2 %3"=="get namespace hexalith-parties" exit /b 0
if "%1 %2 %3"=="get namespace dapr-system" (
  echo namespace/dapr-system
  exit /b {(daprStatusFailsExistingInstall ? 0 : 1)}
)
if "%1 %2 %3"=="get service keycloak" (
  echo {keycloakServiceOutput}
  exit /b {keycloakServiceExit}
)
if "%1 %2 %3"=="get secret hexalith-tache-ui-credentials" (
  echo %* | findstr username > nul
  if not errorlevel 1 (
    if "{usernameSecretExit}"=="1" exit /b 1
    echo dXNlcg==
    exit /b 0
  )
  echo %* | findstr password > nul
  if not errorlevel 1 (
    if "{passwordSecretExit}"=="1" exit /b 1
    echo dXNlcg==
    exit /b 0
  )
  echo dXNlcg==
  exit /b 0
)
if "%1 %2 %3"=="get secret hexalith-parties-ui-oidc-client" (
  if "{partiesUiSecretExit}"=="1" exit /b 1
  echo present
  exit /b 0
)
if "%1"=="run" (
  echo {tokenJson}
  exit /b 0
)
if "%1 %2"=="api-resources --verbs=list" (
  echo pods
  echo services
  echo configmaps
  exit /b 0
)
if "%1 %2"=="get crd" exit /b 0
if "%1 %2 %3"=="get pods -n" exit /b {(failNamespaceResourceEnumeration ? 1 : 0)}
if "%1"=="get" exit /b 1
if "%1"=="apply" (
  if "%2 %3"=="-f -" more > nul
  exit /b 0
)
if "%1"=="delete" exit /b 0
exit /b 0
""");
        }
        else
        {
            File.WriteAllText(kubectlPath, $"""
#!/usr/bin/env bash
printf 'kubectl %s\n' "$*" >> "{logPath}"
if [ "$1 $2" = "config current-context" ]; then
  printf '%s\n' "safe-context"
  exit 0
fi
if [ "$1 $2 $3" = "get namespace hexalith-parties" ]; then
  exit 0
fi
if [ "$1 $2 $3" = "get namespace dapr-system" ]; then
  printf '%s\n' namespace/dapr-system
  exit {(daprStatusFailsExistingInstall ? 0 : 1)}
fi
if [ "$1 $2 $3" = "get service keycloak" ]; then
  printf '%s\n' "{keycloakServiceOutput}"
  exit {keycloakServiceExit}
fi
if [ "$1 $2 $3" = "get secret hexalith-tache-ui-credentials" ]; then
  case "$*" in
    *username*)
      if [ "{usernameSecretExit}" = "1" ]; then exit 1; fi
      printf '%s\n' "dXNlcg=="
      exit 0
      ;;
    *password*)
      if [ "{passwordSecretExit}" = "1" ]; then exit 1; fi
      printf '%s\n' "dXNlcg=="
      exit 0
      ;;
  esac
  printf '%s\n' "dXNlcg=="
  exit 0
fi
if [ "$1 $2 $3" = "get secret hexalith-parties-ui-oidc-client" ]; then
  if [ "{partiesUiSecretExit}" = "1" ]; then exit 1; fi
  printf '%s\n' "present"
  exit 0
fi
if [ "$1" = "run" ]; then
  printf '%s\n' '{tokenJson}'
  exit 0
fi
if [ "$1" = "api-resources" ]; then
  printf '%s\n' pods services configmaps
  exit 0
fi
if [ "$1 $2" = "get crd" ]; then
  exit 0
fi
if [ "$1 $2 $3" = "get pods -n" ]; then
  exit {(failNamespaceResourceEnumeration ? 1 : 0)}
fi
if [ "$1" = "get" ]; then
  exit 1
fi
if [ "$1" = "apply" ]; then
  if [ "$2 $3" = "-f -" ]; then
    cat >/dev/null
  fi
  exit 0
fi
if [ "$1" = "delete" ]; then
  exit 0
fi
exit 0
""");
            File.SetUnixFileMode(kubectlPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private static void WriteDotnetShim(string binDirectory, string logPath, bool includeLiteralJwt, bool emitUnexpectedTopLevelFile, bool generatedReadinessOnlyProbe)
    {
        string dotnetPath = Path.Combine(binDirectory, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dotnet.cmd" : "dotnet");
        string realDotnetPath = DotnetPath;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            File.WriteAllText(dotnetPath, $"""
@echo off
if /I "%~nx1"=="pwsh.dll" (
  "{realDotnetPath}" %*
  exit /b %ERRORLEVEL%
)
echo dotnet %*>> "{logPath}"
if "%1"=="msbuild" (
  echo 0.1.1-preview.0.7
  exit /b 0
)
exit /b 1
""");
            return;
        }

        string shellDotnetPath = realDotnetPath.Replace("'", "'\"'\"'");
        File.WriteAllText(dotnetPath, $$"""
#!/usr/bin/env bash
real_dotnet='{{shellDotnetPath}}'
if [ "${1:-}" != "" ] && [ "$(basename "$1")" = "pwsh.dll" ]; then
  exec "$real_dotnet" "$@"
fi
printf 'dotnet %s\n' "$*" >> "{{logPath}}"
if [ "$1" = "msbuild" ]; then
  printf '%s\n' "0.1.1-preview.0.7"
  exit 0
fi
if [ "$1 $2 $3 $4 $5 $6" = "tool run --allow-roll-forward aspirate -- generate" ]; then
  if [ -n "${ContainerImageTags:-}" ]; then
    printf '%s\n' "plural ContainerImageTags leaked into aspirate" >> "{{logPath}}"
    exit 42
  fi
  out="$PWD/../../deploy/k8s"
  for name in eventstore eventstore-admin eventstore-admin-ui sample sample-blazor-ui parties parties-mcp parties-ui tenants memories; do
    mkdir -p "$out/$name"
    include_dapr=0
    case "$name" in
      eventstore|eventstore-admin|eventstore-admin-ui|sample|sample-blazor-ui|parties|tenants|memories) include_dapr=1 ;;
    esac
    literal_jwt=0
    if [ "$name" = "eventstore" ] && [ "{{(includeLiteralJwt ? "1" : "0")}}" = "1" ]; then literal_jwt=1; fi
    python3 - "$out/$name/deployment.yaml" "$name" "$include_dapr" "$literal_jwt" <<'PY'
import sys
path, name, include_dapr, literal_jwt = sys.argv[1], sys.argv[2], sys.argv[3] == "1", sys.argv[4] == "1"
image = f"registry.hexalith.com/{name}:0.1.1-preview.0.7"
annotations = "        dapr.io/enabled: 'true'\n        dapr.io/app-id: " + name + "\n" if include_dapr else "        example.com/placeholder: none\n"
jwt = "        env:\n        - name: Authentication__JwtBearer__SigningKey\n          value: literal-secret\n" if literal_jwt else ""
probe = "        readinessProbe:\n          tcpSocket:\n            port: 8080\n" if "{{(generatedReadinessOnlyProbe ? "1" : "0")}}" == "1" else ""
host_aliases = "" if name == "eventstore-admin-ui" else "      hostAliases:\n      - ip: 127.0.0.1\n        hostnames:\n        - internal.example\n"
open(path, "w", encoding="utf-8").write(f'''---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: {name}
spec:
  template:
    metadata:
      labels:
        app: {name}
      annotations:
{annotations}    spec:
{host_aliases}
      containers:
      - name: {name}
        image: {image}
        imagePullPolicy: IfNotPresent
{probe}{jwt}        envFrom:
        - configMapRef:
            name: {name}-env
      terminationGracePeriodSeconds: 180
''')
PY
    if [ "$name" = "eventstore-admin-ui" ]; then
      printf 'resources:\n- deployment.yaml\nconfigMapGenerator:\n- name: eventstore-admin-ui-env\n  literals:\n    - EventStore__Authentication__Authority=http://auth.tache.ai:8080/realms/tache\n    - EventStore__Authentication__Issuer=http://auth.tache.ai:8080/realms/tache\n' > "$out/$name/kustomization.yaml"
    else
      printf 'resources:\n- deployment.yaml\n' > "$out/$name/kustomization.yaml"
    fi
  done
  if [ "{{(emitUnexpectedTopLevelFile ? "1" : "0")}}" = "1" ]; then
    printf 'unexpected\n' > "$out/surprise.txt"
  fi
  exit 0
fi
exit 1
""");
        File.SetUnixFileMode(dotnetPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private static void WriteDaprShim(string binDirectory, string logPath, bool failStatus)
    {
        string daprPath = Path.Combine(binDirectory, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dapr.cmd" : "dapr");
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            File.WriteAllText(daprPath, $"""
@echo off
echo dapr %*>> "{logPath}"
if "%1 %2"=="status -k" exit /b {(failStatus ? 1 : 0)}
exit /b 0
""");
        }
        else
        {
            File.WriteAllText(daprPath, $"""
#!/usr/bin/env bash
printf 'dapr %s\n' "$*" >> "{logPath}"
if [ "$1 $2" = "status -k" ]; then
  printf '%s\n' "dapr status unhealthy"
  exit {(failStatus ? 1 : 0)}
fi
exit 0
""");
            File.SetUnixFileMode(daprPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private static string DeploymentYaml(string name, string image, bool includeDapr, bool includeLiteralJwt)
    {
        string annotations = includeDapr
            ? $"        dapr.io/enabled: 'true'\n        dapr.io/app-id: {name}\n"
            : "        example.com/placeholder: none\n";
        string jwt = includeLiteralJwt
            ? "        env:\n        - name: Authentication__JwtBearer__SigningKey\n          value: literal-secret\n"
            : string.Empty;

        return $$"""
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: {{name}}
spec:
  template:
    metadata:
      labels:
        app: {{name}}
      annotations:
{{annotations}}    spec:
      containers:
      - name: {{name}}
        image: {{image}}
        imagePullPolicy: IfNotPresent
        readinessProbe:
          tcpSocket:
            port: 8080
        livenessProbe:
          tcpSocket:
            port: 8080
{{jwt}}        envFrom:
        - configMapRef:
            name: {{name}}-env
      terminationGracePeriodSeconds: 180
""";
    }

    private sealed record ProcessResult(int ExitCode, string Output);

    private sealed class ShimWorkspace(string rootDirectory, string binDirectory, string logPath) : IDisposable
    {
        public string BinDirectory { get; } = binDirectory;

        public string[] LogLines => File.Exists(logPath)
            ? File.ReadAllLines(logPath)
            : [];

        public void Dispose()
        {
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, recursive: true);
            }
        }
    }

    private sealed class ScriptWorkspace(
        string rootDirectory,
        string k8sRoot,
        string binDirectory,
        string logPath,
        IReadOnlyDictionary<string, string> environment) : IDisposable
    {
        public string K8sRoot { get; } = k8sRoot;

        public string PublishScript => Path.Combine(K8sRoot, "publish.ps1");

        public string TeardownScript => Path.Combine(K8sRoot, "teardown.ps1");

        public string BinDirectory { get; } = binDirectory;

        public IReadOnlyDictionary<string, string> Environment { get; } = environment;

        public string[] LogLines => File.Exists(logPath)
            ? File.ReadAllLines(logPath)
            : [];

        public void Dispose()
        {
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, recursive: true);
            }
        }
    }
}
