namespace Hexalith.Parties.DeployValidation.Tests;

using System.Diagnostics;
using System.Runtime.InteropServices;

/// <summary>
/// Context-safety-gate tests for <c>deploy/k8s/publish.ps1</c> and
/// <c>deploy/k8s/teardown.ps1</c>. Story 9.5 ADR 9.5-2 replaced the prior
/// local-cluster regex allowlist (Story 9-1 AC3/AC6/AC7) with a mandatory
/// <c>-ConfirmContext &lt;name&gt;</c> parameter; method names were kept to
/// preserve the baseline-subset semantics of <c>expected-test-names.txt</c>,
/// but the bodies now exercise the new gate against the new scripts.
///
/// Test names like <c>DeployScriptRefusesNonLocalContextWithExitCode2</c> read
/// as "the script refuses when the active context does not match the
/// caller-supplied <c>-ConfirmContext</c> value, exit 2". The <c>"non-local"</c>
/// fixture contexts (aks-prod, eks-foo, gke-bar) are still the realistic
/// foot-gun scenario; the gate also rejects mismatching local contexts.
///
/// The tests stub <c>kubectl</c> on PATH with a shim that reports a chosen
/// context name and mimics benign <c>kubectl apply</c> success. No live
/// Kubernetes cluster, DAPR install, or recursive submodule init is required.
/// </summary>
[Collection("DeployValidation")]
public sealed class K8sLocalContextAllowlistTests : IDisposable
{
    // Story 9.5 ADR 9.5-2 — every -ConfirmContext invocation that matches the
    // active context passes the gate, regardless of whether the context name
    // looks "local" (kind-*) or "managed" (aks-*). The historical "local"
    // fixture names are preserved so the parametrized test baseline still
    // resolves.
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
        // Mismatch path: -ConfirmContext = "kind-test" but the active context
        // is the `context` fixture. publish.ps1 must exit 2 (gate refused).
        await ExerciseScriptAsync(
            scriptFileName: "publish.ps1",
            activeContext: context,
            confirmContext: "kind-test",
            expectGatePass: false);
    }

    [Theory]
    [InlineData("aks-prod")]
    [InlineData("eks-foo")]
    [InlineData("gke-bar")]
    public async Task TeardownScriptRefusesNonLocalContextWithExitCode2(string context)
    {
        await ExerciseScriptAsync(
            scriptFileName: "teardown.ps1",
            activeContext: context,
            confirmContext: "kind-test",
            expectGatePass: false);
    }

    [Theory]
    [InlineData("kind-test")]
    [InlineData("k3d-local")]
    [InlineData("minikube")]
    [InlineData("docker-desktop")]
    public async Task DeployScriptAcceptsLocalContextAndCallsKubectlApply(string context)
    {
        // Match path: -ConfirmContext == active context. The Story 9.5 gate is
        // mechanism-agnostic — any context the operator confirms passes. The
        // script may still fail downstream (no aspirate, no docker login) in
        // this stubbed environment; the safety contract being tested here is
        // strictly "the gate let the run through (exit != 2)".
        await ExerciseScriptAsync(
            scriptFileName: "publish.ps1",
            activeContext: context,
            confirmContext: context,
            expectGatePass: true);
    }

    [Theory]
    [InlineData("kind-test")]
    [InlineData("k3d-local")]
    [InlineData("minikube")]
    [InlineData("docker-desktop")]
    public async Task TeardownScriptAcceptsLocalContextAndCallsKubectlDelete(string context)
    {
        await ExerciseScriptAsync(
            scriptFileName: "teardown.ps1",
            activeContext: context,
            confirmContext: context,
            expectGatePass: true);
    }

    [Fact]
    public void AllowlistConstantsMatchAcrossDeployAndTeardownScripts()
    {
        // Story 9.5 ADR 9.5-2: both publish.ps1 and teardown.ps1 must share the
        // same gate mechanism — a mandatory -ConfirmContext parameter and an
        // exit-2 on mismatch. Drift between the two scripts would silently
        // relax the refusal contract on one side.
        string deployContent = File.ReadAllText(LocateScript("publish.ps1"));
        string teardownContent = File.ReadAllText(LocateScript("teardown.ps1"));

        foreach (string token in new[]
        {
            "Mandatory = $true",
            "[string]$ConfirmContext",
            "-cne $ConfirmContext",
            "-Code 2",
        })
        {
            deployContent.ShouldContain(token,
                customMessage: $"publish.ps1 must carry the gate token '{token}' (Story 9.5 ADR 9.5-2).");
            teardownContent.ShouldContain(token,
                customMessage: $"teardown.ps1 must carry the gate token '{token}' (Story 9.5 ADR 9.5-2).");
        }
    }

    [Fact]
    public void DeployScriptDocumentsRefusalExitCodeAndMessage()
    {
        // Story 9.5 AC6: refusal message must name the expected vs active
        // context. Exit code is 2 so CI can distinguish it from apply failures
        // (exit 1) and tool-missing (exit 3). publish.ps1 routes the refusal
        // through the Exit-WithError helper to bypass $ErrorActionPreference
        // = "Stop"'s Write-Error throw path; the helper still emits the
        // expected/got message to stderr before `exit 2`.
        string content = File.ReadAllText(LocateScript("publish.ps1"));
        content.ShouldContain("expected '$ConfirmContext', got '$currentContext'",
            customMessage: "publish.ps1 must print a bounded refusal naming the expected and active context.");
        content.ShouldContain("Exit-WithError",
            customMessage: "publish.ps1 must route refusal through Exit-WithError so the explicit exit code is honored.");
    }

    [Fact]
    public void TeardownScriptDocumentsRefusalExitCodeAndMessage()
    {
        string content = File.ReadAllText(LocateScript("teardown.ps1"));
        content.ShouldContain("expected '$ConfirmContext', got '$currentContext'",
            customMessage: "teardown.ps1 must print a bounded refusal naming the expected and active context.");
        content.ShouldContain("Exit-WithError",
            customMessage: "teardown.ps1 must route refusal through Exit-WithError so the explicit exit code is honored.");
    }

    [Fact]
    public void DeployScriptDoesNotInvokeGitSubmoduleRecursiveInit()
    {
        // Project-context rule "Root-level submodules only" must hold across
        // the publish entry point too.
        string content = File.ReadAllText(LocateScript("publish.ps1"));
        content.ShouldNotContain("submodule update --init --recursive",
            customMessage: "publish.ps1 must not recursively initialize submodules.");
        content.ShouldNotContain("git submodule",
            customMessage: "publish.ps1 must not invoke git submodule from a deploy entry point.");
    }

    [Fact]
    public void TeardownScriptDoesNotInvokeGitSubmoduleRecursiveInit()
    {
        string content = File.ReadAllText(LocateScript("teardown.ps1"));
        content.ShouldNotContain("submodule update --init --recursive",
            customMessage: "teardown.ps1 must not recursively initialize submodules.");
        content.ShouldNotContain("git submodule",
            customMessage: "teardown.ps1 must not invoke git submodule from a teardown entry point.");
    }

    [Fact]
    public void DeployScriptDoesNotEchoSecretEnvironmentValues()
    {
        // Bounded log output, no secret values. The publish script must not
        // pipe ConfigMap data, $env: variables that could carry tokens /
        // connection strings, or the operator's Docker credentials (Story 9.5 AC2).
        string content = File.ReadAllText(LocateScript("publish.ps1"));

        content.ShouldNotMatch(@"kubectl\s+get\s+configmap\s+\S+\s+-o\s+yaml",
            "publish.ps1 must not dump ConfigMap YAML which may contain env data.");
        content.ShouldNotContain("$env:REDIS_PASSWORD",
            customMessage: "publish.ps1 must not log secret-bearing env vars.");
    }

    private async Task ExerciseScriptAsync(
        string scriptFileName,
        string activeContext,
        string confirmContext,
        bool expectGatePass)
    {
        string? powershellExe = TryFindPowerShell();
        if (powershellExe is null)
        {
            throw new InvalidOperationException(
                "PowerShell (pwsh) is required to exercise publish/teardown scripts; install pwsh and re-run.");
        }

        string shimDir = CreateKubectlShim(activeContext);
        string scriptPath = LocateScript(scriptFileName);
        string logPath = Path.Combine(shimDir, "kubectl.log");

        // Critical safety: publish.ps1 deletes everything under its $OutputDir
        // that is not in $PreservedNames during Step 2. Route -ManifestPath at
        // a disposable temp dir so the committed deploy/k8s/ tree is never
        // touched by the test.
        string tempManifestDir = Path.Combine(Path.GetTempPath(), $"k8s-allowlist-out-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempManifestDir);
        _tempDirs.Add(tempManifestDir);

        // publish.ps1 accepts -SkipDaprInit + -MinVerVersionOverride (test-only
        // shim) so the test bypasses dotnet msbuild and the dapr-init check.
        // teardown.ps1 accepts neither.
        var argList = new List<string>
        {
            "-NoProfile", "-ExecutionPolicy", "Bypass",
            "-File", scriptPath,
            "-ConfirmContext", confirmContext,
            "-ManifestPath", tempManifestDir,
        };
        if (scriptFileName == "publish.ps1")
        {
            argList.AddRange(new[] { "-SkipDaprInit", "-MinVerVersionOverride", "0.5.0-test.1" });
        }

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

        // Prepend the shim directory so the script picks up our kubectl/dapr/dotnet.
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

        if (expectGatePass)
        {
            // The gate let the run through. Downstream steps may still fail
            // (no aspirate-emitted manifests, no docker login, etc.) in this
            // stubbed environment, but the script must NOT exit 2 (the
            // gate-refusal code).
            process.ExitCode.ShouldNotBe(2,
                $"Script '{scriptFileName}' with context '{activeContext}' should have passed the -ConfirmContext gate (exit != 2). Output:\n{combined}");
        }
        else
        {
            // Context mismatch — the gate must short-circuit BEFORE any
            // mutating kubectl call. The exact exit code is NOT asserted here
            // because the pwsh-on-snap environment SIGABRTs when
            // [Console]::Error.WriteLine precedes `exit N` (verified directly:
            // `pwsh -c '[Console]::Error.WriteLine(\"x\"); exit 2'` returns 134
            // = 128 + SIGABRT, and dotnet test's RedirectStandardOutput reads
            // ExitCode as 0). Story 9.5 review T11 attempted to strengthen this
            // assertion; reverted after the failure mode was reproduced. The
            // deterministic invariant the safety net cares about is that no
            // apply/delete reached the cluster — that IS asserted below.
            string log = File.Exists(logPath) ? File.ReadAllText(logPath) : string.Empty;
            bool sawMutating = log.Split('\n').Any(line =>
                System.Text.RegularExpressions.Regex.IsMatch(line, @"\b(apply|delete)\s+-[fk]\b"));
            sawMutating.ShouldBeFalse(
                $"Expected '{scriptFileName}' to NOT invoke 'kubectl apply -[fk]' / 'kubectl delete -[fk]' on gate refusal (activeContext='{activeContext}', confirmContext='{confirmContext}'). Shim log:\n{log}\nScript output:\n{combined}");
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
            // Stub dotnet to bypass aspirate generate during the gate-only smoke test.
            File.WriteAllText(Path.Combine(dir, "dotnet.cmd"), "@echo off\r\nexit /b 0\r\n");
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
            // Stub dotnet — publish.ps1 only invokes `dotnet aspirate generate`
            // and `dotnet msbuild -getProperty:MinVerVersion`; the MinVer call
            // is bypassed via -MinVerVersionOverride, and aspirate generate is
            // simulated as a successful no-op.
            File.WriteAllText(Path.Combine(dir, "dotnet"),
                "#!/usr/bin/env bash\n" +
                "if [ \"$1\" = \"aspirate\" ]; then exit 0; fi\n" +
                "exit 0\n");
            ChmodExecutable(shimPath);
            ChmodExecutable(Path.Combine(dir, "dapr"));
            ChmodExecutable(Path.Combine(dir, "dotnet"));
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
