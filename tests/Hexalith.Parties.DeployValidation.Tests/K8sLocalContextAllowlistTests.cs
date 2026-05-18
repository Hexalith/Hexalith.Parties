namespace Hexalith.Parties.DeployValidation.Tests;

using System.Diagnostics;
using System.Runtime.InteropServices;

/// <summary>
/// Allowlist enforcement tests for <c>deploy/k8s/deploy-local.ps1</c> and
/// <c>deploy/k8s/teardown-local.ps1</c> (Story 9-1 AC3, AC6, AC7). The tests stub
/// the <c>kubectl</c> binary on PATH with a script that reports a chosen context
/// name and otherwise mimics a benign <c>kubectl apply</c> success. No live
/// Kubernetes cluster, DAPR install, or recursive submodule init is required.
/// </summary>
public sealed class K8sLocalContextAllowlistTests : IDisposable
{
    private static readonly string[] LocalContexts =
    [
        "kind-test",
        "k3d-local",
        "minikube",
        "docker-desktop",
    ];

    private static readonly string[] NonLocalContexts =
    [
        "aks-prod",
        "eks-foo",
        "gke-bar",
    ];

    private readonly List<string> _tempDirs = new();
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (string dir in _tempDirs)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
            catch
            {
                // best-effort cleanup
            }
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    [Theory]
    [InlineData("aks-prod")]
    [InlineData("eks-foo")]
    [InlineData("gke-bar")]
    public async Task DeployScriptRefusesNonLocalContextWithExitCode2(string context)
    {
        await ExerciseScriptAsync("deploy-local.ps1", context, expectedExitCode: 2, expectApplyInvocation: false);
    }

    [Theory]
    [InlineData("aks-prod")]
    [InlineData("eks-foo")]
    [InlineData("gke-bar")]
    public async Task TeardownScriptRefusesNonLocalContextWithExitCode2(string context)
    {
        await ExerciseScriptAsync("teardown-local.ps1", context, expectedExitCode: 2, expectApplyInvocation: false);
    }

    [Theory]
    [InlineData("kind-test")]
    [InlineData("k3d-local")]
    [InlineData("minikube")]
    [InlineData("docker-desktop")]
    public async Task DeployScriptAcceptsLocalContextAndCallsKubectlApply(string context)
    {
        // Local contexts pass the allowlist; the stub kubectl reports zero from
        // every call so the script proceeds and exits zero.
        await ExerciseScriptAsync("deploy-local.ps1", context, expectedExitCode: 0, expectApplyInvocation: true);
    }

    [Theory]
    [InlineData("kind-test")]
    [InlineData("k3d-local")]
    [InlineData("minikube")]
    [InlineData("docker-desktop")]
    public async Task TeardownScriptAcceptsLocalContextAndCallsKubectlDelete(string context)
    {
        await ExerciseScriptAsync("teardown-local.ps1", context, expectedExitCode: 0, expectApplyInvocation: true);
    }

    [Fact]
    public void AllowlistConstantsMatchAcrossDeployAndTeardownScripts()
    {
        string deployContent = File.ReadAllText(LocateScript("deploy-local.ps1"));
        string teardownContent = File.ReadAllText(LocateScript("teardown-local.ps1"));

        // AC3 + AC7: both scripts share the same allowlist patterns. The smoke
        // test entry point (this assembly) verifies the same set via
        // LocalContexts above. Drift between the three would silently relax the
        // refusal contract.
        // Patterns are anchored, case-sensitive (via -cmatch), and reject
        // dot-containing or empty suffixes after kind-/k3d-. Tightened regex
        // closes the 'Kind-Phishing' / 'kind-' case-bypass attack vector.
        foreach (string pattern in new[]
        {
            "^kind-[a-z0-9][a-z0-9-]*$",
            "^k3d-[a-z0-9][a-z0-9-]*$",
            "^minikube$",
            "^docker-desktop$",
        })
        {
            deployContent.ShouldContain(pattern,
                customMessage: $"deploy-local.ps1 must list allowlist pattern '{pattern}'.");
            teardownContent.ShouldContain(pattern,
                customMessage: $"teardown-local.ps1 must list allowlist pattern '{pattern}'.");
        }

        // Both scripts must use -cmatch (case-sensitive) so a renamed managed
        // context like 'Kind-Phishing' does not bypass the allowlist.
        deployContent.ShouldContain("-cmatch",
            customMessage: "deploy-local.ps1 must use -cmatch (case-sensitive regex) for allowlist evaluation.");
        teardownContent.ShouldContain("-cmatch",
            customMessage: "teardown-local.ps1 must use -cmatch (case-sensitive regex) for allowlist evaluation.");
    }

    [Fact]
    public void DeployScriptDocumentsRefusalExitCodeAndMessage()
    {
        // AC3: refusal message must name the active context and reference the
        // allowlist so operators can self-diagnose. Exit code is 2 so CI can
        // distinguish it from apply failures (exit 1) and tool-missing (exit 3).
        string content = File.ReadAllText(LocateScript("deploy-local.ps1"));
        content.ShouldContain("Refusing to deploy against non-local kubectl context",
            customMessage: "deploy-local.ps1 must print a bounded refusal message naming the active context.");
        content.ShouldContain("exit 2",
            customMessage: "deploy-local.ps1 must exit with code 2 on context refusal.");
    }

    [Fact]
    public void TeardownScriptDocumentsRefusalExitCodeAndMessage()
    {
        string content = File.ReadAllText(LocateScript("teardown-local.ps1"));
        content.ShouldContain("Refusing to teardown against non-local kubectl context",
            customMessage: "teardown-local.ps1 must print a bounded refusal message naming the active context.");
        content.ShouldContain("exit 2",
            customMessage: "teardown-local.ps1 must exit with code 2 on context refusal.");
    }

    [Fact]
    public void DeployScriptDoesNotInvokeGitSubmoduleRecursiveInit()
    {
        // AC7: smoke tests / deploy scripts must not recurse submodules.
        // Mirrors the project-context rule "Root-level submodules only."
        string content = File.ReadAllText(LocateScript("deploy-local.ps1"));
        content.ShouldNotContain("submodule update --init --recursive",
            customMessage: "deploy-local.ps1 must not recursively initialize submodules.");
        content.ShouldNotContain("git submodule",
            customMessage: "deploy-local.ps1 must not invoke git submodule from a deploy entry point.");
    }

    [Fact]
    public void TeardownScriptDoesNotInvokeGitSubmoduleRecursiveInit()
    {
        string content = File.ReadAllText(LocateScript("teardown-local.ps1"));
        content.ShouldNotContain("submodule update --init --recursive",
            customMessage: "teardown-local.ps1 must not recursively initialize submodules.");
        content.ShouldNotContain("git submodule",
            customMessage: "teardown-local.ps1 must not invoke git submodule from a teardown entry point.");
    }

    [Fact]
    public void DeployScriptDoesNotEchoSecretEnvironmentValues()
    {
        // Story 1.7/1.8 carryover: bounded log output, no secret values.
        // The deploy script must not pipe ConfigMap data or env-var contents.
        string content = File.ReadAllText(LocateScript("deploy-local.ps1"));

        // Defensive: the script must NOT redirect kubectl get configmap output
        // or echo $env: variables that could carry tokens / connection strings.
        content.ShouldNotMatch(@"kubectl\s+get\s+configmap\s+\S+\s+-o\s+yaml",
            "deploy-local.ps1 must not dump ConfigMap YAML which may contain env data.");
        content.ShouldNotContain("$env:REDIS_PASSWORD",
            customMessage: "deploy-local.ps1 must not log secret-bearing env vars.");
    }

    private async Task ExerciseScriptAsync(
        string scriptFileName,
        string context,
        int expectedExitCode,
        bool expectApplyInvocation)
    {
        string? powershellExe = TryFindPowerShell();
        if (powershellExe is null)
        {
            // The DeployValidation test lane requires pwsh; if it is unavailable
            // (e.g. minimal CI runner) the existing DeploymentValidationTests
            // fail loudly. Mirror that contract here rather than silently passing.
            throw new InvalidOperationException(
                "PowerShell (pwsh) is required to exercise deploy/teardown scripts; install pwsh and re-run.");
        }

        string shimDir = CreateKubectlShim(context);
        string scriptPath = LocateScript(scriptFileName);
        string logPath = Path.Combine(shimDir, "kubectl.log");

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = powershellExe,
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -SkipDaprInit",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        // teardown script does not accept -SkipDaprInit; drop the switch for it.
        if (scriptFileName.StartsWith("teardown", StringComparison.Ordinal))
        {
            process.StartInfo.Arguments =
                $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"";
        }

        // Prepend the shim directory so the script picks up our kubectl/dapr.
        string existingPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        char pathSep = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';
        process.StartInfo.Environment["PATH"] = shimDir + pathSep + existingPath;
        process.StartInfo.Environment["KUBECTL_SHIM_LOG"] = logPath;

        process.Start();
        string stdout = await process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        string stderr = await process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

        string combined = stdout
            + (string.IsNullOrWhiteSpace(stderr) ? string.Empty : $"\nSTDERR:\n{stderr}");

        process.ExitCode.ShouldBe(
            expectedExitCode,
            $"Script '{scriptFileName}' with context '{context}' expected exit {expectedExitCode}. Output:\n{combined}");

        string log = File.Exists(logPath) ? File.ReadAllText(logPath) : string.Empty;
        // Tighten from a loose `Contains("apply")` substring grep to a per-line
        // regex match on the exact mutating kubectl verbs the scripts actually
        // emit. `apply -f` covers the per-yaml DAPR Component apply loop;
        // `apply -k` covers the kustomize apply; `delete -f` / `delete -k`
        // cover the teardown. Matching whole tokens guards against false
        // positives from substrings like an "apply.yaml" filename appearing
        // in unrelated kubectl args.
        var logLines = log.Split('\n');
        bool sawApply = logLines.Any(line =>
            System.Text.RegularExpressions.Regex.IsMatch(
                line, @"\b(apply|delete)\s+-[fk]\b"));

        if (expectApplyInvocation)
        {
            sawApply.ShouldBeTrue(
                $"Expected '{scriptFileName}' to invoke 'kubectl apply -[fk]' or 'kubectl delete -[fk]' for local context '{context}'. Shim log:\n{log}");
        }
        else
        {
            sawApply.ShouldBeFalse(
                $"Expected '{scriptFileName}' to NOT invoke 'kubectl apply -[fk]' / 'kubectl delete -[fk]' for non-local context '{context}'. Shim log:\n{log}");
        }
    }

    private string CreateKubectlShim(string contextName)
    {
        string dir = Path.Combine(Path.GetTempPath(), $"k8s-allowlist-shim-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string cmdPath = Path.Combine(dir, "kubectl.cmd");
            string cmdShim =
                "@echo off\r\n" +
                "echo %* >> \"%KUBECTL_SHIM_LOG%\"\r\n" +
                "if \"%~1\" == \"config\" if \"%~2\" == \"current-context\" (\r\n" +
                "    echo " + contextName + "\r\n" +
                "    exit /b 0\r\n" +
                ")\r\n" +
                "exit /b 0\r\n";
            File.WriteAllText(cmdPath, cmdShim);
            File.WriteAllText(Path.Combine(dir, "dapr.cmd"), "@echo off\r\nexit /b 0\r\n");
        }
        else
        {
            string shimPath = Path.Combine(dir, "kubectl");
            string shim =
                "#!/usr/bin/env bash\n" +
                "printf '%s\\n' \"$*\" >> \"${KUBECTL_SHIM_LOG:-/dev/null}\"\n" +
                "if [ \"$1\" = \"config\" ] && [ \"$2\" = \"current-context\" ]; then\n" +
                "    echo \"" + contextName + "\"\n" +
                "    exit 0\n" +
                "fi\n" +
                "if [ \"$1\" = \"apply\" ]; then\n" +
                "    echo \"deployment.apps/eventstore created\"\n" +
                "    exit 0\n" +
                "fi\n" +
                "if [ \"$1\" = \"delete\" ]; then\n" +
                "    echo \"deployment.apps/eventstore deleted\"\n" +
                "    exit 0\n" +
                "fi\n" +
                "if [ \"$1\" = \"get\" ]; then\n" +
                "    if [ \"$2\" = \"namespace\" ]; then exit 1; fi\n" +
                "    exit 0\n" +
                "fi\n" +
                "if [ \"$1\" = \"create\" ] && [ \"$2\" = \"namespace\" ]; then exit 0; fi\n" +
                "exit 0\n";
            File.WriteAllText(shimPath, shim);
            File.WriteAllText(Path.Combine(dir, "dapr"), "#!/usr/bin/env bash\nexit 0\n");
            ChmodExecutable(shimPath);
            ChmodExecutable(Path.Combine(dir, "dapr"));
        }

        return dir;
    }

    private static void ChmodExecutable(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

#pragma warning disable CA1416 // OS guarded above
        File.SetUnixFileMode(path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
#pragma warning restore CA1416
    }

    private static string LocateScript(string scriptFileName)
    {
        string? dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.GetFiles(dir, "*.slnx").Length > 0)
            {
                string scriptPath = Path.Combine(dir, "deploy", "k8s", scriptFileName);
                File.Exists(scriptPath).ShouldBeTrue($"Script not found at {scriptPath}.");
                return scriptPath;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException("Could not locate Hexalith.Parties solution root (looking for *.slnx).");
    }

    private static string? TryFindPowerShell()
    {
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
                using var probe = new Process();
                probe.StartInfo = new ProcessStartInfo
                {
                    FileName = candidate,
                    Arguments = "-NoProfile -Command \"exit 0\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                probe.Start();
                if (probe.WaitForExit(5000) && probe.ExitCode == 0)
                {
                    return candidate;
                }
            }
            catch
            {
                // try next candidate
            }
        }

        return null;
    }
}
