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
            "Resolve MinVer image tag",
            "Clean generated deploy/k8s entries",
            "Run dotnet aspirate generate",
            "Strip Aspirate placeholder files",
            "Patch Dapr annotations",
            "Patch JWT secretKeyRef",
            "Patch imagePullSecrets",
            "Verify expected service folders",
            "Install or verify Dapr control plane",
            "Ensure namespace and dry-run resiliency CR",
            "Bootstrap operator-managed Secrets",
            "Apply Dapr CRs",
            "Apply Kubernetes workloads",
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

        foreach (string preserved in new[] { "redis", "keycloak", "kustomization.yaml", "namespace.yaml", "README.md", "publish.ps1", "teardown.ps1", "_lib" })
        {
            publish.ShouldContain($"'{preserved}'");
        }

        foreach (string generated in new[] { "eventstore", "eventstore-admin", "eventstore-admin-ui", "parties", "parties-mcp", "tenants", "memories" })
        {
            publish.ShouldContain($"'{generated}'");
        }

        publish.ShouldContain("'eventstore' = 'accesscontrol'");
        publish.ShouldContain("'eventstore-admin' = 'accesscontrol-eventstore-admin'");
        publish.ShouldContain("'parties' = 'accesscontrol-parties'");
        publish.ShouldContain("'tenants' = 'accesscontrol-tenants'");
        publish.ShouldContain("'memories' = 'accesscontrol-memories'");
        publish.ShouldContain("$ForbiddenDaprTargets = @('eventstore-admin-ui', 'parties-mcp', 'redis', 'keycloak')");
        publish.ShouldContain("Authentication__JwtBearer__SigningKey");
        publish.ShouldContain("imagePullSecrets");
        publish.ShouldContain("zot-pull-secret");
        publish.ShouldContain("credsStore");
        publish.ShouldContain("credHelpers");
        publish.ShouldNotContain("FromBase64String");
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
        eventstore.ShouldContain("Authentication__JwtBearer__SigningKey");
        eventstore.ShouldContain("secretKeyRef:");
        eventstore.ShouldContain("name: hexalith-jwt-signing");
        eventstore.ShouldNotContain("value: literal-secret");

        string adminUi = File.ReadAllText(Path.Combine(workspace.K8sRoot, "eventstore-admin-ui", "deployment.yaml"));
        adminUi.ShouldNotContain("dapr.io/app-id");
        adminUi.ShouldContain("imagePullSecrets:");

        workspace.LogLines.Count(line => line.Contains("resiliency.yaml --dry-run=server", StringComparison.Ordinal)).ShouldBe(1);
        int statestore = Array.FindIndex(workspace.LogLines, line => line.Contains("statestore.yaml", StringComparison.Ordinal));
        int pubsub = Array.FindIndex(workspace.LogLines, line => line.Contains("pubsub.yaml", StringComparison.Ordinal));
        int resiliency = Array.FindIndex(workspace.LogLines, line => line.Contains("resiliency.yaml", StringComparison.Ordinal) && !line.Contains("--dry-run=server", StringComparison.Ordinal));
        int workloads = Array.FindIndex(workspace.LogLines, line => line == $"kubectl apply -k {workspace.K8sRoot}");
        statestore.ShouldBeGreaterThan(0);
        pubsub.ShouldBeGreaterThan(statestore);
        resiliency.ShouldBeGreaterThan(pubsub);
        workloads.ShouldBeGreaterThan(resiliency);
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

    private ScriptWorkspace CreateScriptWorkspace(
        bool failNamespaceResourceEnumeration = false,
        bool includeLiteralJwt = false,
        bool emitUnexpectedTopLevelFile = false,
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
  - tenants
  - redis
  - keycloak
""");
        File.WriteAllText(Path.Combine(k8sRoot, "namespace.yaml"), "apiVersion: v1\nkind: Namespace\nmetadata:\n  name: hexalith-parties\n");
        File.WriteAllText(Path.Combine(k8sRoot, "README.md"), "test\n");

        foreach (string folder in new[] { "redis", "keycloak" })
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
        WriteKubectlShim(bin, logPath, failNamespaceResourceEnumeration);
        WriteDotnetShim(bin, logPath, includeLiteralJwt, emitUnexpectedTopLevelFile);
        WriteDaprShim(bin, logPath);

        Dictionary<string, string> environment = new(StringComparer.Ordinal)
        {
            ["SCRIPT_TEST_LOG"] = logPath,
            ["DOCKER_CONFIG"] = docker,
        };

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

    private static void WriteKubectlShim(string binDirectory, string logPath, bool failNamespaceResourceEnumeration)
    {
        string kubectlPath = Path.Combine(binDirectory, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "kubectl.cmd" : "kubectl");
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

    private static void WriteDotnetShim(string binDirectory, string logPath, bool includeLiteralJwt, bool emitUnexpectedTopLevelFile)
    {
        string dotnetPath = Path.Combine(binDirectory, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dotnet.cmd" : "dotnet");
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            File.WriteAllText(dotnetPath, $"""
@echo off
echo dotnet %*>> "{logPath}"
if "%1"=="msbuild" (
  echo 0.1.1-preview.0.7
  exit /b 0
)
exit /b 1
""");
            return;
        }

        File.WriteAllText(dotnetPath, $$"""
#!/usr/bin/env bash
printf 'dotnet %s\n' "$*" >> "{{logPath}}"
if [ "$1" = "msbuild" ]; then
  printf '%s\n' "0.1.1-preview.0.7"
  exit 0
fi
if [ "$1 $2 $3 $4 $5 $6" = "tool run --allow-roll-forward aspirate -- generate" ]; then
  out="$PWD/../../deploy/k8s"
  for name in eventstore eventstore-admin eventstore-admin-ui parties parties-mcp tenants memories; do
    mkdir -p "$out/$name"
    include_dapr=0
    case "$name" in
      eventstore|eventstore-admin|parties|tenants|memories) include_dapr=1 ;;
    esac
    literal_jwt=0
    if [ "$name" = "eventstore" ] && [ "{{(includeLiteralJwt ? "1" : "0")}}" = "1" ]; then literal_jwt=1; fi
    python3 - "$out/$name/deployment.yaml" "$name" "$include_dapr" "$literal_jwt" <<'PY'
import sys
path, name, include_dapr, literal_jwt = sys.argv[1], sys.argv[2], sys.argv[3] == "1", sys.argv[4] == "1"
image = f"registry.hexalith.com/{name}:0.1.1-preview.0.7"
annotations = "        dapr.io/enabled: 'true'\n        dapr.io/app-id: " + name + "\n" if include_dapr else "        example.com/placeholder: none\n"
jwt = "        env:\n        - name: Authentication__JwtBearer__SigningKey\n          value: literal-secret\n" if literal_jwt else ""
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
      containers:
      - name: {name}
        image: {image}
        imagePullPolicy: IfNotPresent
{jwt}        envFrom:
        - configMapRef:
            name: {name}-env
      terminationGracePeriodSeconds: 180
''')
PY
    printf 'resources:\n- deployment.yaml\n' > "$out/$name/kustomization.yaml"
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

    private static void WriteDaprShim(string binDirectory, string logPath)
    {
        string daprPath = Path.Combine(binDirectory, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dapr.cmd" : "dapr");
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            File.WriteAllText(daprPath, $"""
@echo off
echo dapr %*>> "{logPath}"
exit /b 0
""");
        }
        else
        {
            File.WriteAllText(daprPath, $"""
#!/usr/bin/env bash
printf 'dapr %s\n' "$*" >> "{logPath}"
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
