using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Hexalith.Parties.DeployValidation.Tests;

[Collection("DeployValidation")]
public sealed class ValidateDeploymentLintToolingTests : IDisposable
{
    private const string Poison = "DO-NOT-PRINT-THIS-SECRET-eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJ0ZXN0In0.signature";

    private static readonly string[] s_expectedCategories =
    [
        "DaprACL-WildcardAppId",
        "DaprACL-WildcardOperation",
        "K8sIngress-InvalidPublicRoute",
        "K8sWorkload-DirtyTagOnConsumerImage",
        "K8sWorkload-MissingDaprAnnotations",
        "K8sWorkload-MissingImagePullSecret",
        "K8sWorkload-MissingProbes",
        "K8sWorkload-NonSemVerTag",
        "Secret-Plaintext",
    ];

    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "hexalith-parties-validate-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public void DocumentedInvocationPassesFromRepositoryRootAndNonRootDirectory()
    {
        using CommandShim shim = CreateCommandShim();

        ProcessResult rootResult = RunValidator(
            RepositoryRoot,
            shim.BinDirectory,
            "--config-path",
            "deploy/dapr",
            "-K8sPath",
            "deploy/k8s/");

        rootResult.ExitCode.ShouldBe(0, rootResult.CombinedOutput);
        rootResult.Stdout.ShouldContain("[validate] 0 findings (0 blocking, 0 warnings) - PASS");
        rootResult.CombinedOutput.ShouldNotContain(Path.GetFullPath(RepositoryRoot), Case.Sensitive);

        string nonRoot = Path.Combine(RepositoryRoot, "tests", "Hexalith.Parties.DeployValidation.Tests");
        ProcessResult nonRootResult = RunValidator(
            nonRoot,
            shim.BinDirectory,
            "--config-path",
            "deploy/dapr",
            "-K8sPath",
            "deploy/k8s/");

        nonRootResult.ExitCode.ShouldBe(0, nonRootResult.CombinedOutput);
        shim.LogLines.ShouldBeEmpty("validate-deployment.ps1 is context-free and must not shell to cluster or mutation tools.");
    }

    [Fact]
    public void JsonOutputIsStableCleanAndRepoRelative()
    {
        ProcessResult result = RunValidator(
            RepositoryRoot,
            null,
            "-ConfigPath",
            "deploy/dapr",
            "-K8sPath",
            "deploy/k8s",
            "-Format",
            "json");

        result.ExitCode.ShouldBe(0, result.CombinedOutput);
        result.Stderr.ShouldBeEmpty();
        result.Stdout.TrimStart().ShouldStartWith("{");
        result.Stdout.ShouldNotContain("[validate]");
        result.Stdout.ShouldNotContain(Path.GetFullPath(RepositoryRoot), Case.Sensitive);

        using JsonDocument document = JsonDocument.Parse(result.Stdout);
        document.RootElement.GetProperty("version").GetString().ShouldBe("1");
        document.RootElement.GetProperty("findings").GetArrayLength().ShouldBe(0);
        JsonElement summary = document.RootElement.GetProperty("summary");
        summary.GetProperty("findings").GetInt32().ShouldBe(0);
        summary.GetProperty("blocking").GetInt32().ShouldBe(0);
        summary.GetProperty("warnings").GetInt32().ShouldBe(0);
        summary.GetProperty("status").GetString().ShouldBe("PASS");
    }

    [Fact]
    public void ExitCodePrecedenceMatchesOperatorContract()
    {
        ProcessResult invalid = RunValidator(RepositoryRoot, null, "-ConfigPath", "deploy/dapr", "-K8sPath", "deploy/k8s", "-Format", "xml");
        invalid.ExitCode.ShouldBe(2, invalid.CombinedOutput);

        ProcessResult missing = RunValidator(RepositoryRoot, null, "-ConfigPath", "deploy/missing-dapr", "-K8sPath", "deploy/k8s");
        missing.ExitCode.ShouldBe(3, missing.CombinedOutput);

        using FixtureTree tree = CreateFixtureTree();
        File.WriteAllText(Path.Combine(tree.K8sPath, "eventstore", "deployment.yaml"), DeploymentYaml("eventstore", "registry.hexalith.com/eventstore:latest", includePullSecret: false, includeDapr: false, includeProbes: false));

        ProcessResult blocking = RunValidator(RepositoryRoot, null, "-ConfigPath", tree.ConfigPath, "-K8sPath", tree.K8sPath, "-Format", "json");
        blocking.ExitCode.ShouldBe(1, blocking.CombinedOutput);
    }

    [Fact]
    public void AllCategoriesAreReportedWithStableJsonContractAndRedactedReasons()
    {
        using FixtureTree tree = CreateFixtureTree();
        File.WriteAllText(Path.Combine(tree.K8sPath, "missing-pull-secret.yaml"), DeploymentYaml("missing-pull-secret", "registry.hexalith.com/missing-pull-secret:1.2.3", includePullSecret: false, includeDapr: false, includeProbes: true));
        File.WriteAllText(Path.Combine(tree.K8sPath, "missing-dapr.yaml"), DeploymentYaml("eventstore", "registry.hexalith.com/eventstore:1.2.3", includePullSecret: true, includeDapr: false, includeProbes: true));
        File.WriteAllText(Path.Combine(tree.K8sPath, "missing-probes.yaml"), DeploymentYaml("missing-probes", "registry.hexalith.com/missing-probes:1.2.3", includePullSecret: true, includeDapr: false, includeProbes: false));
        File.WriteAllText(Path.Combine(tree.K8sPath, "latest-tag.yaml"), DeploymentYaml("latest-tag", "registry.hexalith.com/latest-tag:latest", includePullSecret: true, includeDapr: false, includeProbes: true));
        File.WriteAllText(Path.Combine(tree.K8sPath, "dirty-tag.yaml"), DeploymentYaml("dirty-tag", "registry.hexalith.com/dirty-tag:1.2.3+dirty", includePullSecret: true, includeDapr: false, includeProbes: true));
        File.WriteAllText(Path.Combine(tree.K8sPath, "unsafe-ingress.yaml"), UnsafeIngressYaml());
        File.WriteAllText(Path.Combine(tree.K8sPath, "secret-configmap.yaml"), SecretConfigMapYaml(Poison));
        File.WriteAllText(Path.Combine(tree.ConfigPath, "accesscontrol-wildcard.yaml"), DaprAccessControlYaml("*", "/api/v1/commands"));
        File.WriteAllText(Path.Combine(tree.ConfigPath, "accesscontrol-operation.yaml"), DaprAccessControlYaml("parties", "/**"));

        ProcessResult result = RunValidator(RepositoryRoot, null, "-ConfigPath", tree.ConfigPath, "-K8sPath", tree.K8sPath, "-Format", "json");

        result.ExitCode.ShouldBe(1, result.CombinedOutput);
        result.CombinedOutput.ShouldNotContain(Poison);
        using JsonDocument document = JsonDocument.Parse(result.Stdout);
        document.RootElement.GetProperty("version").GetString().ShouldBe("1");

        JsonElement findings = document.RootElement.GetProperty("findings");
        findings.GetArrayLength().ShouldBeGreaterThanOrEqualTo(8);
        string[] categories = findings.EnumerateArray()
            .Select(static f => f.GetProperty("category").GetString()!)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        categories.ShouldBe(s_expectedCategories);
        foreach (JsonElement finding in findings.EnumerateArray())
        {
            finding.GetProperty("severity").GetString().ShouldBe("BLOCKING");
            finding.GetProperty("file").GetString().ShouldNotBeNullOrWhiteSpace();
            finding.GetProperty("file").GetString()!.ShouldNotContain("\\");
            finding.GetProperty("jsonpath").GetString().ShouldStartWith("$.");
            finding.GetProperty("reason").GetString().ShouldNotBeNullOrWhiteSpace();
        }

        JsonElement summary = document.RootElement.GetProperty("summary");
        summary.GetProperty("blocking").GetInt32().ShouldBe(findings.GetArrayLength());
        summary.GetProperty("warnings").GetInt32().ShouldBe(0);
        summary.GetProperty("status").GetString().ShouldBe("FAIL");
    }

    [Fact]
    public void NearMissesAndAlternativeFoldersDoNotTriggerDefaultScan()
    {
        using FixtureTree tree = CreateFixtureTree();
        File.WriteAllText(Path.Combine(tree.K8sPath, "vendor.yaml"), DeploymentYaml("vendor", "redis:latest", includePullSecret: false, includeDapr: false, includeProbes: true));
        File.WriteAllText(Path.Combine(tree.K8sPath, "literal-asterisk.yaml"), """
apiVersion: v1
kind: ConfigMap
metadata:
  name: literal-asterisk
data:
  route: "*"
  passwordHint: "<placeholder>"
""");
        string alternatives = Path.Combine(Path.GetDirectoryName(tree.ConfigPath)!, "dapr-alternatives");
        Directory.CreateDirectory(alternatives);
        File.WriteAllText(Path.Combine(alternatives, "accesscontrol.yaml"), DaprAccessControlYaml("*", "/**"));

        ProcessResult result = RunValidator(RepositoryRoot, null, "-ConfigPath", tree.ConfigPath, "-K8sPath", tree.K8sPath);

        result.ExitCode.ShouldBe(0, result.CombinedOutput);
        result.Stdout.ShouldContain("[validate] 0 findings (0 blocking, 0 warnings) - PASS");
    }

    [Fact]
    public void ScopedWorkloadChecksDoNotAcceptTopLevelAnnotationsOrSidecarOnlyProbes()
    {
        using FixtureTree tree = CreateFixtureTree();
        File.WriteAllText(Path.Combine(tree.K8sPath, "scoped-workloads.yaml"), """
apiVersion: apps/v1
kind: Deployment
metadata:
  name: eventstore
  annotations:
    dapr.io/enabled: 'true'
    dapr.io/app-id: eventstore
    dapr.io/app-port: '8080'
    dapr.io/config: tracing
spec:
  template:
    metadata:
      labels:
        app: eventstore
    spec:
      containers:
      - name: eventstore
        image: registry.hexalith.com/eventstore:1.2.3
      - name: helper
        image: busybox:1.36
        readinessProbe:
          exec:
            command: ['true']
        livenessProbe:
          exec:
            command: ['true']
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: pull-secret-carrier
spec:
  template:
    metadata:
      labels:
        app: pull-secret-carrier
    spec:
      imagePullSecrets:
      - name: zot-pull-secret
      containers:
      - name: pull-secret-carrier
        image: registry.hexalith.com/pull-secret-carrier:1.2.3
        readinessProbe:
          httpGet:
            path: /health
            port: 8080
        livenessProbe:
          httpGet:
            path: /health
            port: 8080
""");

        ProcessResult result = RunValidator(RepositoryRoot, null, "-ConfigPath", tree.ConfigPath, "-K8sPath", tree.K8sPath, "-Format", "json");

        result.ExitCode.ShouldBe(1, result.CombinedOutput);
        string[] categories = CategoriesFrom(result.Stdout);
        categories.ShouldContain("K8sWorkload-MissingDaprAnnotations");
        categories.ShouldContain("K8sWorkload-MissingImagePullSecret");
        categories.ShouldContain("K8sWorkload-MissingProbes");
    }

    [Fact]
    public void DaprAclChecksCatchMissingAppIdButAllowDocumentedPrefixWildcards()
    {
        using FixtureTree cleanTree = CreateFixtureTree();
        File.WriteAllText(Path.Combine(cleanTree.ConfigPath, "accesscontrol-prefix.yaml"), DaprAccessControlYaml("eventstore-admin", "/api/v1/commands/**"));

        ProcessResult clean = RunValidator(RepositoryRoot, null, "-ConfigPath", cleanTree.ConfigPath, "-K8sPath", cleanTree.K8sPath);

        clean.ExitCode.ShouldBe(0, clean.CombinedOutput);

        using FixtureTree invalidTree = CreateFixtureTree();
        File.WriteAllText(Path.Combine(invalidTree.ConfigPath, "accesscontrol-missing-appid.yaml"), DaprAccessControlWithoutAppIdYaml("/process"));
        File.WriteAllText(Path.Combine(invalidTree.ConfigPath, "accesscontrol-loose-wildcard.yaml"), DaprAccessControlYaml("parties", "/api/v1/*"));

        ProcessResult invalid = RunValidator(RepositoryRoot, null, "-ConfigPath", invalidTree.ConfigPath, "-K8sPath", invalidTree.K8sPath, "-Format", "json");

        invalid.ExitCode.ShouldBe(1, invalid.CombinedOutput);
        string[] categories = CategoriesFrom(invalid.Stdout);
        categories.ShouldContain("DaprACL-WildcardAppId");
        categories.ShouldContain("DaprACL-WildcardOperation");
    }

    [Fact]
    public void IngressChecksRejectBackendRoutesAndMissingTls()
    {
        using FixtureTree invalidTree = CreateFixtureTree();
        File.WriteAllText(Path.Combine(invalidTree.K8sPath, "ingress.yaml"), UnsafeIngressYaml());

        ProcessResult invalid = RunValidator(RepositoryRoot, null, "-ConfigPath", invalidTree.ConfigPath, "-K8sPath", invalidTree.K8sPath, "-Format", "json");

        invalid.ExitCode.ShouldBe(1, invalid.CombinedOutput);
        CategoriesFrom(invalid.Stdout).ShouldContain("K8sIngress-InvalidPublicRoute");
    }

    [Theory]
    [InlineData("eventstore.hexalith.com", "sample-blazor-ui", "/", "Prefix", "8080")]
    [InlineData("preview.hexalith.com", "eventstore-admin-ui", "/", "Prefix", "8080")]
    [InlineData("eventstore.hexalith.com", "eventstore-admin-ui", "/ui", "Prefix", "8080")]
    [InlineData("eventstore.hexalith.com", "eventstore-admin-ui", "/", "Exact", "8080")]
    [InlineData("eventstore.hexalith.com", "eventstore-admin-ui", "/", "Prefix", "8443")]
    public void IngressChecksRejectUiRoutesThatDoNotMatchTheExactPublicContract(string host, string service, string path, string pathType, string port)
    {
        using FixtureTree invalidTree = CreateFixtureTree();
        File.WriteAllText(Path.Combine(invalidTree.K8sPath, "ingress.yaml"), IngressYaml(host, service, path, pathType, port));

        ProcessResult invalid = RunValidator(RepositoryRoot, null, "-ConfigPath", invalidTree.ConfigPath, "-K8sPath", invalidTree.K8sPath, "-Format", "json");

        invalid.ExitCode.ShouldBe(1, invalid.CombinedOutput);
        CategoriesFrom(invalid.Stdout).ShouldContain("K8sIngress-InvalidPublicRoute");
    }

    [Fact]
    public void SecretScannerCoversStringDataEnvValuesAndComponentMetadata()
    {
        using FixtureTree tree = CreateFixtureTree();
        File.WriteAllText(Path.Combine(tree.K8sPath, "secret-stringdata.yaml"), """
apiVersion: v1
kind: Secret
metadata:
  name: jwt-secret
stringData:
  signingKey: "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
""");
        File.WriteAllText(Path.Combine(tree.K8sPath, "deployment-env-secret.yaml"), DeploymentYaml("parties", "registry.hexalith.com/parties:1.2.3", includePullSecret: true, includeDapr: true, includeProbes: true) + """
        env:
        - name: Authentication__JwtBearer__SigningKey
          value: changeme
""");
        File.WriteAllText(Path.Combine(tree.ConfigPath, "component-secret.yaml"), """
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: statestore
spec:
  type: state.redis
  metadata:
  - name: redisPassword
    value: changeme
""");

        ProcessResult result = RunValidator(RepositoryRoot, null, "-ConfigPath", tree.ConfigPath, "-K8sPath", tree.K8sPath, "-Format", "json");

        result.ExitCode.ShouldBe(1, result.CombinedOutput);
        result.CombinedOutput.ShouldNotContain("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");
        result.CombinedOutput.ShouldNotContain("changeme");
        using JsonDocument document = JsonDocument.Parse(result.Stdout);
        JsonElement[] secretFindings = document.RootElement.GetProperty("findings").EnumerateArray()
            .Where(static f => f.GetProperty("category").GetString() == "Secret-Plaintext")
            .ToArray();
        secretFindings.Length.ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void HumanAndJsonOutputsNeverLeakPoisonValues()
    {
        using FixtureTree tree = CreateFixtureTree();
        File.WriteAllText(Path.Combine(tree.K8sPath, "secret-configmap.yaml"), SecretConfigMapYaml(Poison));

        ProcessResult human = RunValidator(RepositoryRoot, null, "-ConfigPath", tree.ConfigPath, "-K8sPath", tree.K8sPath);
        human.ExitCode.ShouldBe(1, human.CombinedOutput);
        human.CombinedOutput.ShouldNotContain(Poison);
        human.CombinedOutput.ShouldContain("jwt-shaped");
        human.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(static line => line.StartsWith("[BLOCKING]", StringComparison.Ordinal))
            .ShouldAllBe(static line => line.Length <= 200);

        ProcessResult json = RunValidator(RepositoryRoot, null, "-ConfigPath", tree.ConfigPath, "-K8sPath", tree.K8sPath, "-Output", "json");
        json.ExitCode.ShouldBe(1, json.CombinedOutput);
        json.CombinedOutput.ShouldNotContain(Poison);
        JsonDocument.Parse(json.Stdout).Dispose();

        ProcessResult parserError = RunValidator(RepositoryRoot, null, "--config-path");
        parserError.ExitCode.ShouldBe(2, parserError.CombinedOutput);
        parserError.CombinedOutput.ShouldNotContain(Poison);

        ProcessResult base64ParserError = RunValidator(RepositoryRoot, null, "-ConfigPath", "deploy/dapr", "-K8sPath", "deploy/k8s", "-Format", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");
        base64ParserError.ExitCode.ShouldBe(2, base64ParserError.CombinedOutput);
        base64ParserError.CombinedOutput.ShouldNotContain("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");
    }

    [Fact]
    public void ScriptSourceDoesNotImportClusterContextOrMutationCommands()
    {
        string script = File.ReadAllText(ValidatorPath);

        script.ShouldNotContain("_lib/Confirm-KubeContext.ps1");
        script.ShouldNotContain("-ConfirmContext");
        script.ShouldNotContain("kubectl config current-context");
        script.ShouldNotContain("kubectl apply");
        script.ShouldNotContain("kubectl delete");
        script.ShouldNotContain("helm install");
        script.ShouldNotContain("dapr init");
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

    private static string ValidatorPath => Path.Combine(RepositoryRoot, "deploy", "validate-deployment.ps1");

    private static string PwshPath => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "pwsh.exe" : "pwsh";

    private static ProcessResult RunValidator(string workingDirectory, string? shimBinDirectory, params string[] arguments)
    {
        ProcessStartInfo start = new()
        {
            FileName = PwshPath,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(ValidatorPath);
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        if (!string.IsNullOrEmpty(shimBinDirectory))
        {
            start.Environment["PATH"] = shimBinDirectory + Path.PathSeparator + start.Environment["PATH"];
        }

        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Failed to start pwsh.");
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new ProcessResult(process.ExitCode, stdout, stderr);
    }

    private static string[] CategoriesFrom(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("findings").EnumerateArray()
            .Select(static f => f.GetProperty("category").GetString()!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private FixtureTree CreateFixtureTree()
    {
        string root = Path.Combine(_tempRoot, Guid.NewGuid().ToString("N"));
        string configPath = Path.Combine(root, "deploy", "dapr");
        string k8sPath = Path.Combine(root, "deploy", "k8s");
        Directory.CreateDirectory(configPath);
        Directory.CreateDirectory(k8sPath);
        Directory.CreateDirectory(Path.Combine(k8sPath, "eventstore"));
        Directory.CreateDirectory(Path.Combine(k8sPath, "redis"));

        File.WriteAllText(Path.Combine(configPath, "accesscontrol.yaml"), DaprAccessControlYaml("parties", "/api/v1/commands"));
        File.WriteAllText(Path.Combine(configPath, "statestore.yaml"), """
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: statestore
spec:
  type: state.redis
  metadata:
  - name: redisHost
    value: redis:6379
""");
        File.WriteAllText(Path.Combine(k8sPath, "eventstore", "deployment.yaml"), DeploymentYaml("eventstore", "registry.hexalith.com/eventstore:1.2.3-preview.4", includePullSecret: true, includeDapr: true, includeProbes: true));
        File.WriteAllText(Path.Combine(k8sPath, "redis", "deployment.yaml"), DeploymentYaml("redis", "redis:8.6.3", includePullSecret: false, includeDapr: false, includeProbes: true));
        File.WriteAllText(Path.Combine(k8sPath, "ingress.yaml"), SafeIngressYaml());

        return new FixtureTree(root, configPath, k8sPath);
    }

    private CommandShim CreateCommandShim()
    {
        string root = Path.Combine(_tempRoot, Guid.NewGuid().ToString("N"));
        string bin = Path.Combine(root, "bin");
        Directory.CreateDirectory(bin);
        string logPath = Path.Combine(root, "commands.log");
        foreach (string command in new[] { "kubectl", "helm", "dapr" })
        {
            WriteCommandShim(bin, command, logPath);
        }

        return new CommandShim(root, bin, logPath);
    }

    private static void WriteCommandShim(string binDirectory, string command, string logPath)
    {
        string path = Path.Combine(binDirectory, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? command + ".cmd" : command);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            File.WriteAllText(path, $"""
@echo off
echo {command} %*>> "{logPath}"
exit /b 99
""");
            return;
        }

        File.WriteAllText(path, $"""
#!/usr/bin/env bash
printf '{command} %s\n' "$*" >> "{logPath}"
exit 99
""");
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private static string DeploymentYaml(string name, string image, bool includePullSecret, bool includeDapr, bool includeProbes)
    {
        List<string> lines =
        [
            "---",
            "apiVersion: apps/v1",
            "kind: Deployment",
            "metadata:",
            $"  name: {name}",
            "spec:",
            "  template:",
            "    metadata:",
            "      labels:",
            $"        app: {name}",
            "      annotations:",
        ];

        if (includeDapr)
        {
            lines.Add("        dapr.io/enabled: 'true'");
            lines.Add($"        dapr.io/app-id: {name}");
            lines.Add("        dapr.io/app-port: '8080'");
            lines.Add("        dapr.io/config: tracing");
        }
        else
        {
            lines.Add("        example.com/note: no-dapr");
        }

        lines.Add("    spec:");
        if (includePullSecret)
        {
            lines.Add("      imagePullSecrets:");
            lines.Add("      - name: zot-pull-secret");
        }

        lines.Add("      containers:");
        lines.Add($"      - name: {name}");
        lines.Add($"        image: {image}");
        lines.Add("        imagePullPolicy: IfNotPresent");
        if (includeProbes)
        {
            lines.Add("        readinessProbe:");
            lines.Add("          httpGet:");
            lines.Add("            path: /health");
            lines.Add("            port: http");
            lines.Add("        livenessProbe:");
            lines.Add("          httpGet:");
            lines.Add("            path: /health");
            lines.Add("            port: http");
        }

        return string.Join("\n", lines) + "\n";
    }

    private static string DaprAccessControlYaml(string appId, string operation)
        => $$"""
apiVersion: dapr.io/v1alpha1
kind: Configuration
metadata:
  name: accesscontrol
spec:
  accessControl:
    defaultAction: deny
    policies:
    - appId: "{{appId}}"
      defaultAction: deny
      operations:
      - name: "{{operation}}"
        action: allow
""";

    private static string UnsafeIngressYaml()
        => """
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: hexalith-pages-ingress
spec:
  ingressClassName: nginx
  rules:
    - host: eventstore.hexalith.com
      http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service:
                name: eventstore
                port:
                  number: 8080
""";

    private static string SafeIngressYaml()
        => """
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: hexalith-pages-ingress
spec:
  ingressClassName: nginx
  rules:
    - host: eventstore.hexalith.com
      http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service:
                name: eventstore-admin-ui
                port:
                  number: 8080
    - host: sample.hexalith.com
      http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service:
                name: sample-blazor-ui
                port:
                  number: 8080
  tls:
    - hosts:
        - eventstore.hexalith.com
        - sample.hexalith.com
      secretName: hexalith-pages-tls
""";

    private static string IngressYaml(string host, string service, string path, string pathType, string port)
        => $$"""
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: hexalith-pages-ingress
spec:
  ingressClassName: nginx
  rules:
    - host: {{host}}
      http:
        paths:
          - path: {{path}}
            pathType: {{pathType}}
            backend:
              service:
                name: {{service}}
                port:
                  number: {{port}}
  tls:
    - hosts:
        - eventstore.hexalith.com
        - sample.hexalith.com
      secretName: hexalith-pages-tls
""";

    private static string DaprAccessControlWithoutAppIdYaml(string operation)
        => $$"""
apiVersion: dapr.io/v1alpha1
kind: Configuration
metadata:
  name: accesscontrol
spec:
  accessControl:
    defaultAction: deny
    policies:
    - defaultAction: deny
      operations:
      - name: "{{operation}}"
        action: allow
""";

    private static string SecretConfigMapYaml(string secret)
        => $$"""
apiVersion: v1
kind: ConfigMap
metadata:
  name: secret-config
data:
  token: "{{secret}}"
""";

    private sealed record ProcessResult(int ExitCode, string Stdout, string Stderr)
    {
        public string CombinedOutput => Stdout + Stderr;
    }

    private sealed class FixtureTree(string rootDirectory, string configPath, string k8sPath) : IDisposable
    {
        public string ConfigPath { get; } = configPath;

        public string K8sPath { get; } = k8sPath;

        public void Dispose()
        {
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, recursive: true);
            }
        }
    }

    private sealed class CommandShim(string rootDirectory, string binDirectory, string logPath) : IDisposable
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
}
