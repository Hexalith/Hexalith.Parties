using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Hexalith.Parties.DeployValidation.Tests;

[Collection("DeployValidation")]
public sealed class LiveClusterDeploymentTests
{
    private const string ExpectedSandboxContext = "hexalith-parties-livecluster";
    private static readonly TimeSpan s_processTimeout = TimeSpan.FromMinutes(20);

    [Fact]
    [Trait("Category", "LiveCluster")]
    public void LiveCluster_PublishHappyPath()
    {
        string kubeconfig = ResolveLiveClusterKubeconfig();
        string context = ReadCurrentContext(kubeconfig);

        context.ShouldBe(ExpectedSandboxContext, "LiveCluster publish must fail before mutation unless the sandbox context is active.");
        ProcessResult publish = RunK8sScript("publish.ps1", kubeconfig, "-ConfirmContext", ExpectedSandboxContext);

        publish.ExitCode.ShouldBe(0, publish.CombinedOutput);
        ProcessResult wait = RunKubectl(kubeconfig, "wait", "--for=condition=Ready", "pod", "-n", "hexalith-parties", "--all", "--timeout=600s");
        wait.ExitCode.ShouldBe(0, wait.CombinedOutput);
        AssertTenPodsReady(kubeconfig);
    }

    [Fact]
    [Trait("Category", "LiveCluster")]
    public void LiveCluster_TeardownClean()
    {
        string kubeconfig = ResolveLiveClusterKubeconfig();
        string context = ReadCurrentContext(kubeconfig);

        context.ShouldBe(ExpectedSandboxContext, "LiveCluster teardown must fail before mutation unless the sandbox context is active.");
        ProcessResult teardown = RunK8sScript("teardown.ps1", kubeconfig, "-ConfirmContext", ExpectedSandboxContext);

        teardown.ExitCode.ShouldBe(0, teardown.CombinedOutput);
        AssertNoResidualStoryResources(kubeconfig);
    }

    [Fact]
    [Trait("Category", "LiveCluster")]
    public void LiveCluster_IdempotentRepublish()
    {
        string kubeconfig = ResolveLiveClusterKubeconfig();
        string context = ReadCurrentContext(kubeconfig);

        context.ShouldBe(ExpectedSandboxContext, "LiveCluster republish must fail before mutation unless the sandbox context is active.");
        ProcessResult first = RunK8sScript("publish.ps1", kubeconfig, "-ConfirmContext", ExpectedSandboxContext);
        first.ExitCode.ShouldBe(0, first.CombinedOutput);

        ProcessResult second = RunK8sScript("publish.ps1", kubeconfig, "-ConfirmContext", ExpectedSandboxContext);
        second.ExitCode.ShouldBe(0, second.CombinedOutput);

        ProcessResult diff = RunKubectl(kubeconfig, "diff", "-k", DeploymentTestPaths.K8sDirectory);
        diff.ExitCode.ShouldBe(0, diff.CombinedOutput);
    }

    private static string ResolveLiveClusterKubeconfig()
    {
        string? kubeconfig = Environment.GetEnvironmentVariable("KUBECONFIG_TEST_PATH");
        if (string.IsNullOrWhiteSpace(kubeconfig))
        {
            Assert.Skip("LiveCluster tests require KUBECONFIG_TEST_PATH pointing to a sandbox kubeconfig.");
        }

        File.Exists(kubeconfig).ShouldBeTrue("KUBECONFIG_TEST_PATH must point to an existing sandbox kubeconfig.");
        return kubeconfig;
    }

    private static string ReadCurrentContext(string kubeconfig)
    {
        ProcessResult result = RunKubectl(kubeconfig, "config", "current-context");
        result.ExitCode.ShouldBe(0, result.CombinedOutput);
        return result.Stdout.Trim();
    }

    private static ProcessResult RunK8sScript(string scriptName, string kubeconfig, params string[] arguments)
    {
        ProcessStartInfo start = new()
        {
            FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "pwsh.exe" : "pwsh",
            WorkingDirectory = DeploymentTestPaths.K8sDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(Path.Combine(DeploymentTestPaths.K8sDirectory, scriptName));
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment["KUBECONFIG"] = kubeconfig;
        return Run(start);
    }

    private static ProcessResult RunKubectl(string kubeconfig, params string[] arguments)
    {
        ProcessStartInfo start = new()
        {
            FileName = "kubectl",
            WorkingDirectory = DeploymentTestPaths.RepositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment["KUBECONFIG"] = kubeconfig;
        return Run(start);
    }

    private static void AssertTenPodsReady(string kubeconfig)
    {
        ProcessResult result = RunKubectl(kubeconfig, "get", "pods", "-n", "hexalith-parties", "--no-headers");
        result.ExitCode.ShouldBe(0, result.CombinedOutput);

        string[] podLines = result.Stdout.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        podLines.Length.ShouldBe(10, $"Expected the Story 9.8 topology to have 10 Ready pods, but got {podLines.Length}:{Environment.NewLine}{result.Stdout}");
        podLines.ShouldContain(line => line.StartsWith("falkordb-", StringComparison.Ordinal), result.Stdout);
    }

    private static void AssertNoResidualStoryResources(string kubeconfig)
    {
        ProcessResult result = RunKubectl(
            kubeconfig,
            "get",
            "all,configmap,secret,serviceaccount,role,rolebinding,component.dapr.io,configuration.dapr.io,subscription.dapr.io,resiliency.dapr.io",
            "-n",
            "hexalith-parties",
            "-o",
            "name",
            "--ignore-not-found");

        result.ExitCode.ShouldBe(0, result.CombinedOutput);
        result.Stdout.Trim().ShouldBeEmpty(result.CombinedOutput);
    }

    private static ProcessResult Run(ProcessStartInfo start)
    {
        using Process process = Process.Start(start) ?? throw new InvalidOperationException($"Failed to start {start.FileName}.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit((int)s_processTimeout.TotalMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"{start.FileName} exceeded the LiveCluster command timeout of {s_processTimeout.TotalMinutes:N0} minutes.");
        }

        return new ProcessResult(process.ExitCode, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult());
    }

    private sealed record ProcessResult(int ExitCode, string Stdout, string Stderr)
    {
        public string CombinedOutput => Stdout + Stderr;
    }
}
