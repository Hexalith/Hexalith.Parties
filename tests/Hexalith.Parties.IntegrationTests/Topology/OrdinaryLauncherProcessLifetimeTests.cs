using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;

using Hexalith.Parties.IntegrationTests.HealthChecks;

using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Exercises actual PID replacement only in a verified owned Linux PID/user namespace.</summary>
public sealed class OrdinaryLauncherProcessLifetimeTests
{
    private const string ChildMarker = "PARTIES_PROCESS_PID_REUSE_CHILD";
    private const string ParentPidNamespace = "PARTIES_PROCESS_PARENT_PID_NAMESPACE";
    private const string ParentUserNamespace = "PARTIES_PROCESS_PARENT_USER_NAMESPACE";
    private const string ParentUid = "PARTIES_PROCESS_PARENT_UID";

    /// <summary>Reuses a numeric PID during role observation and reaps every owned process with bounded cleanup.</summary>
    [Fact(Timeout = 30000)]
    public async Task IsolatedPidReuseDuringRoleReadRefusesQualificationAsync()
    {
        if (!OperatingSystem.IsLinux()) { Assert.Skip("Requires Linux user/PID namespaces."); }
        if (Environment.GetEnvironmentVariable(ChildMarker) != "1")
        {
            var start = new ProcessStartInfo("unshare") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (string argument in new[] { "--user", "--map-root-user", "--pid", "--fork", "--mount-proc", "dotnet",
                typeof(OrdinaryLauncherProcessLifetimeTests).Assembly.Location, "-method", "*IsolatedPidReuseDuringRoleReadRefusesQualificationAsync" })
            { start.ArgumentList.Add(argument); }
            start.Environment[ChildMarker] = "1";
            start.Environment[ParentPidNamespace] = Namespace("self", "pid");
            start.Environment[ParentUserNamespace] = Namespace("self", "user");
            start.Environment[ParentUid] = File.ReadLines("/proc/self/status").Single(line => line.StartsWith("Uid:", StringComparison.Ordinal))
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[1];
            (int exit, string retained) = await RunOwnedRunnerAsync(start, TestContext.Current.CancellationToken);
            TestContext.Current.TestOutputHelper?.WriteLine(retained);
            exit.ShouldBe(0, retained);
            return;
        }

        // An inherited marker is not namespace authority. Verify the kernel namespaces and
        // exact PID-one/root mapping before starting children or writing ns_last_pid.
        RequireOwnedNamespace();
        var owned = new List<Process>();
        Process original = Process.Start(new ProcessStartInfo("sleep", "120") { UseShellExecute = false })!;
        owned.Add(original);
        Exception? primary = null;
        try
        {
            int reusedPid = original.Id;
            long birth = PartiesAspireTopologyFixture.ReadWorkerStartIdentity(reusedPid)!.Value;
            bool replaced = false;
            var workers = new ConcurrentDictionary<int, (long Started, string Role, string Resource)>();
            var launchers = new ConcurrentDictionary<string, int>(StringComparer.Ordinal); launchers["owned"] = Environment.ProcessId;
            var capture = new OrdinaryLauncherProcessCapture(
                enumerateTasks: id => id == Environment.ProcessId ? ["owned-parent"] : Directory.GetDirectories($"/proc/{id}/task"),
                readText: path =>
                {
                    if (path == "owned-parent/children") { return reusedPid.ToString(CultureInfo.InvariantCulture); }
                    if (path == $"/proc/{reusedPid}/comm" && !replaced)
                    {
                        original.Kill();
                        if (!original.WaitForExit(5000)) { throw new TimeoutException("The owned original PID did not exit."); }
                        // Kernel start identities have clock-tick granularity.
                        Thread.Sleep(30);
                        RequireOwnedNamespace();
                        File.WriteAllText("/proc/sys/kernel/ns_last_pid", (reusedPid - 1).ToString(CultureInfo.InvariantCulture));
                        Process replacement = Process.Start(new ProcessStartInfo("sleep", "120") { UseShellExecute = false })!;
                        owned.Add(replacement);
                        replacement.Id.ShouldBe(reusedPid);
                        replaced = true;
                    }
                    return File.ReadAllText(path);
                });
            capture.CaptureDescendants(Environment.ProcessId, "owned", workers);
            replaced.ShouldBeTrue();
            PartiesAspireTopologyFixture.ReadWorkerStartIdentity(reusedPid).ShouldNotBe(birth);
            workers.ShouldNotContainKey(reusedPid);
            capture.Failures.ShouldContain(item => item.Contains("lifetime changed", StringComparison.Ordinal));
            capture.QualificationFailures(launchers, workers).ShouldNotBeEmpty();
            Console.WriteLine($"PID reuse observed: pid={reusedPid}, before={birth}, after={PartiesAspireTopologyFixture.ReadWorkerStartIdentity(reusedPid)}, refused=true");
        }
        catch (Exception failure) { primary = failure; }
        finally { await CleanupOwnedAsync(owned, primary).ConfigureAwait(true); }
        if (primary is not null) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(primary).Throw(); }
    }

    /// <summary>Direct and inherited child markers cannot authorize mutation in the caller's existing namespace.</summary>
    [Theory(Timeout = 25000)]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChildMarkerOutsideOwnedNamespaceRefusesBeforeMutationAsync(bool inheritedCoordinates)
    {
        if (!OperatingSystem.IsLinux()) { Assert.Skip("Requires Linux namespaces."); }
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in new[] { typeof(OrdinaryLauncherProcessLifetimeTests).Assembly.Location, "-method", "*IsolatedPidReuseDuringRoleReadRefusesQualificationAsync" })
        { start.ArgumentList.Add(argument); }
        start.Environment[ChildMarker] = "1";
        foreach (string key in new[] { ParentPidNamespace, ParentUserNamespace, ParentUid }) { start.Environment.Remove(key); }
        if (inheritedCoordinates)
        {
            start.Environment[ParentPidNamespace] = Namespace("self", "pid");
            start.Environment[ParentUserNamespace] = Namespace("self", "user");
            start.Environment[ParentUid] = "0";
        }
        (int exit, string retained) = await RunOwnedRunnerAsync(start, TestContext.Current.CancellationToken);
        exit.ShouldNotBe(0); retained.ShouldContain("Requires a new owned PID/user namespace");
        retained.ShouldNotContain("PID reuse observed");
    }

    private static string Namespace(string process, string name)
        => new FileInfo($"/proc/{process}/ns/{name}").LinkTarget ?? throw new InvalidOperationException("Kernel namespace identity is unavailable.");

    private static void RequireOwnedNamespace()
    {
        string? priorPid = Environment.GetEnvironmentVariable(ParentPidNamespace), priorUser = Environment.GetEnvironmentVariable(ParentUserNamespace), uid = Environment.GetEnvironmentVariable(ParentUid);
        string[] mapping = File.ReadAllText("/proc/self/uid_map").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (Environment.ProcessId != 1 || string.IsNullOrWhiteSpace(priorPid) || string.IsNullOrWhiteSpace(priorUser)
            || Namespace("self", "pid") == priorPid || Namespace("self", "user") == priorUser
            || Namespace("self", "pid") != Namespace("1", "pid") || Namespace("self", "user") != Namespace("1", "user")
            || mapping.Length != 3 || mapping[0] != "0" || mapping[1] != uid || mapping[2] != "1")
        { throw new InvalidOperationException("Requires a new owned PID/user namespace with PID one and the exact root user mapping before ns_last_pid mutation."); }
    }

    private static async Task<(int Exit, string Output)> RunOwnedRunnerAsync(ProcessStartInfo start, CancellationToken token)
    {
        var owned = new List<Process>();
        Process runner = Process.Start(start)!;
        owned.Add(runner);
        Exception? primary = null;
        (int, string) result = default;
        try
        {
            using var execution = CancellationTokenSource.CreateLinkedTokenSource(token);
            execution.CancelAfter(TimeSpan.FromSeconds(15));
            Task<string> output = runner.StandardOutput.ReadToEndAsync(execution.Token), errors = runner.StandardError.ReadToEndAsync(execution.Token);
            await runner.WaitForExitAsync(execution.Token).ConfigureAwait(false);
            result = (runner.ExitCode, (await output.ConfigureAwait(false)) + (await errors.ConfigureAwait(false)));
        }
        catch (Exception failure) { primary = failure; }
        finally { await CleanupOwnedAsync(owned, primary).ConfigureAwait(true); }
        if (primary is not null) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(primary).Throw(); }
        return result;
    }

    private static async Task CleanupOwnedAsync(List<Process> owned, Exception? primary)
    {
        var failures = new List<Exception>();
        foreach (Process process in owned.AsEnumerable().Reverse())
        {
            try
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                if (!process.HasExited) { process.Kill(entireProcessTree: true); }
                await process.WaitForExitAsync(cleanup.Token).ConfigureAwait(false);
            }
            catch (Exception failure) { failures.Add(failure); }
            finally { process.Dispose(); }
        }
        if (failures.Count != 0)
        {
            if (primary is not null) { failures.Insert(0, primary); }
            throw new AggregateException("Owned namespace cleanup failed.", failures);
        }
    }
}
