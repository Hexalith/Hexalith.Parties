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
    public Task IsolatedPidReuseDuringRoleReadRefusesQualificationAsync()
        => RunPidReuseControlAsync(false, TestContext.Current.CancellationToken);

    /// <summary>Reuses a listed PID under another live parent before its first identity read.</summary>
    [Fact(Timeout = 30000)]
    public Task IsolatedForeignParentPidReuseBeforeIdentityReadRefusesQualificationAsync()
        => RunPidReuseControlAsync(true, TestContext.Current.CancellationToken);

    private static async Task RunPidReuseControlAsync(bool foreignParent, CancellationToken token)
    {
        if (!OperatingSystem.IsLinux())
        {
            if (Environment.GetEnvironmentVariable("PARTIES_PROCESS_NAMESPACE_REQUIRED") == "true")
            { throw new InvalidOperationException("Namespace qualification is unsupported: requires Linux."); }
            Assert.Skip("Namespace qualification unsupported: requires Linux user/PID namespaces.");
        }
        if (Environment.GetEnvironmentVariable(ChildMarker) != "1")
        {
            var probe = new ProcessStartInfo("unshare") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            probe.Environment["LC_ALL"] = "C";
            foreach (string argument in new[] { "--user", "--map-root-user", "--pid", "--fork", "--mount-proc", "sh", "-c", "echo PARTIES_NAMESPACE_ENTERED" })
            { probe.ArgumentList.Add(argument); }
            int probeExit;
            string probeOutput;
            try { (probeExit, probeOutput) = await RunOwnedRunnerAsync(probe, token).ConfigureAwait(true); }
            catch (System.ComponentModel.Win32Exception exception) when (exception.NativeErrorCode == 2)
            {
                NamespaceUnavailable("unshare executable is unavailable");
                throw;
            }
            TestContext.Current.TestOutputHelper?.WriteLine($"Namespace entry probe: exit={probeExit} {probeOutput}");
            if (probeExit != 0)
            {
                if (!probeOutput.StartsWith("unshare: unshare failed:", StringComparison.Ordinal)
                    && !probeOutput.StartsWith("unshare: mount", StringComparison.Ordinal))
                { throw new InvalidOperationException($"The namespace probe child failed: {probeOutput}"); }
                NamespaceUnavailable(probeOutput);
            }
            probeOutput.ShouldContain("PARTIES_NAMESPACE_ENTERED");
            var start = new ProcessStartInfo("unshare") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (string argument in new[] { "--user", "--map-root-user", "--pid", "--fork", "--mount-proc", "dotnet",
                typeof(OrdinaryLauncherProcessLifetimeTests).Assembly.Location, "-method", foreignParent
                    ? "*IsolatedForeignParentPidReuseBeforeIdentityReadRefusesQualificationAsync" : "*IsolatedPidReuseDuringRoleReadRefusesQualificationAsync" })
            { start.ArgumentList.Add(argument); }
            start.Environment[ChildMarker] = "1";
            start.Environment[ParentPidNamespace] = Namespace("self", "pid");
            start.Environment[ParentUserNamespace] = Namespace("self", "user");
            start.Environment[ParentUid] = File.ReadLines("/proc/self/status").Single(line => line.StartsWith("Uid:", StringComparison.Ordinal))
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[1];
            (int exit, string retained) = await RunOwnedRunnerAsync(start, TestContext.Current.CancellationToken).ConfigureAwait(true);
            TestContext.Current.TestOutputHelper?.WriteLine(retained);
            RequireSuccessfulPidReuseControl(exit, retained);
            return;
        }

        // An inherited marker is not namespace authority. Verify the kernel namespaces and
        // exact PID-one/root mapping before starting children or writing ns_last_pid.
        RequireOwnedNamespace();
        if (Environment.GetEnvironmentVariable("PARTIES_PROCESS_TEST_CHILD_FAILURE") == "1")
        { throw new InvalidOperationException("Owned namespace child failure control."); }
        var owned = new List<Process>();
        Process original = Process.Start(new ProcessStartInfo("sleep", "120") { UseShellExecute = false })!;
        owned.Add(original);
        Exception? primary = null;
        try
        {
            int reusedPid = original.Id;
            Process sibling = Process.Start(new ProcessStartInfo("sleep", "120") { UseShellExecute = false })!;
            owned.Add(sibling);
            Process? otherParent = null;
            if (foreignParent)
            {
                var otherStart = new ProcessStartInfo("python3") { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true };
                otherStart.ArgumentList.Add("-u"); otherStart.ArgumentList.Add("-c");
                otherStart.ArgumentList.Add("import os,sys\nvalue=sys.stdin.readline().strip()\nwith open('/proc/sys/kernel/ns_last_pid','w') as target: target.write(value)\nchild=os.fork()\nif child==0: os.execlp('sleep','sleep','120')\nprint(child,flush=True)\nos.waitpid(child,0)");
                otherParent = Process.Start(otherStart)!;
                owned.Add(otherParent);
            }
            long birth = PartiesAspireTopologyFixture.ReadWorkerStartIdentity(reusedPid)!.Value;
            bool replaced = false;
            var workers = new ConcurrentDictionary<int, (long Started, string Role, string Resource)>();
            var launchers = new ConcurrentDictionary<string, int>(StringComparer.Ordinal); launchers["owned"] = Environment.ProcessId;
            void Replace()
            {
                original.Kill();
                if (!original.WaitForExit(5000)) { throw new TimeoutException("The owned original PID did not exit."); }
                original.Dispose(); owned.Remove(original);
                // Kernel start identities have clock-tick granularity.
                Thread.Sleep(30);
                RequireOwnedNamespace();
                Process replacement;
                if (otherParent is null)
                {
                    File.WriteAllText("/proc/sys/kernel/ns_last_pid", (reusedPid - 1).ToString(CultureInfo.InvariantCulture));
                    replacement = Process.Start(new ProcessStartInfo("sleep", "120") { UseShellExecute = false })!;
                }
                else
                {
                    Task<string?> child = otherParent.StandardOutput.ReadLineAsync();
                    otherParent.StandardInput.WriteLine((reusedPid - 1).ToString(CultureInfo.InvariantCulture)); otherParent.StandardInput.Flush();
                    if (!child.Wait(TimeSpan.FromSeconds(5))) { throw new TimeoutException("Foreign parent did not start replacement."); }
                    replacement = Process.GetProcessById(int.Parse(child.Result!, CultureInfo.InvariantCulture));
                }
                owned.Add(replacement); replacement.Id.ShouldBe(reusedPid); replaced = true;
            }
            var capture = new OrdinaryLauncherProcessCapture(
                enumerateTasks: id => id == Environment.ProcessId ? ["owned-parent"] : Directory.GetDirectories($"/proc/{id}/task"),
                readText: path =>
                {
                    if (path == "owned-parent/children") { return $"{reusedPid} {sibling.Id}"; }
                    if (path == $"/proc/{reusedPid}/comm" && !replaced)
                    {
                        if (!foreignParent) { Replace(); }
                    }
                    return File.ReadAllText(path);
                }, readIdentity: id =>
                {
                    if (foreignParent && id == reusedPid && !replaced) { Replace(); }
                    return PartiesAspireTopologyFixture.ReadWorkerStartIdentity(id);
                });
            capture.CaptureDescendants(Environment.ProcessId, "owned", workers);
            replaced.ShouldBeTrue();
            PartiesAspireTopologyFixture.ReadWorkerStartIdentity(reusedPid).ShouldNotBe(birth);
            workers.ShouldNotContainKey(reusedPid);
            capture.Failures.ShouldContain(item => item.Contains(foreignParent ? "parent membership" : "lifetime changed", StringComparison.Ordinal));
            workers.ShouldContainKey(sibling.Id);
            sibling.HasExited.ShouldBeFalse();
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
        (int exit, string retained) = await RunOwnedRunnerAsync(start, TestContext.Current.CancellationToken).ConfigureAwait(true);
        exit.ShouldNotBe(0); retained.ShouldContain("Requires a new owned PID/user namespace");
        retained.ShouldNotContain("PID reuse observed");
    }

    /// <summary>A failure after verified namespace entry remains a failure in ordinary and strict runs.</summary>
    /// <param name="strict">Whether namespace qualification is required.</param>
    [Theory(Timeout = 25000)]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EnteredNamespaceChildFailureCannotBecomeUnsupportedAsync(bool strict)
    {
        if (!OperatingSystem.IsLinux()) { Assert.Skip("Requires Linux namespace controls."); }
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in new[] { typeof(OrdinaryLauncherProcessLifetimeTests).Assembly.Location, "-method", "*IsolatedPidReuseDuringRoleReadRefusesQualificationAsync" })
        { start.ArgumentList.Add(argument); }
        start.Environment.Remove(ChildMarker);
        start.Environment["PARTIES_PROCESS_NAMESPACE_REQUIRED"] = strict ? "true" : "false";
        start.Environment["PARTIES_PROCESS_TEST_CHILD_FAILURE"] = "1";
        (int exit, string output) = await RunOwnedRunnerAsync(start, TestContext.Current.CancellationToken).ConfigureAwait(true);
        if (output.Contains("Namespace qualification unsupported", StringComparison.Ordinal))
        { NamespaceUnavailable("The post-entry failure control could not enter an owned namespace."); }
        exit.ShouldBe(1); output.ShouldContain("Owned namespace child failure control.");
        output.ShouldContain("Failed: 1"); output.ShouldNotContain("PID reuse observed");
    }

    private static void RequireSuccessfulPidReuseControl(int exit, string output)
    {
        exit.ShouldBe(0, output);
        var summaries = System.Text.RegularExpressions.Regex.Matches(output, @"Total:\s+\d+, Errors:\s+\d+, Failed:\s+\d+, Skipped:\s+\d+, Not Run:\s+\d+");
        summaries.Count.ShouldBe(1, output);
        summaries[0].Value.ShouldMatch(@"Total:\s+1, Errors:\s+0, Failed:\s+0, Skipped:\s+0, Not Run:\s+0$");
        var observation = System.Text.RegularExpressions.Regex.Match(output, @"PID reuse observed: pid=(\d+), before=(\d+), after=(\d+), refused=true");
        observation.Success.ShouldBeTrue(output);
        observation.Groups[2].Value.ShouldNotBe(observation.Groups[3].Value);
    }

    /// <summary>An unmatched selector's successful zero-test exit cannot qualify PID reuse.</summary>
    [Fact(Timeout = 25000)]
    public async Task UnmatchedNestedSelectorCannotQualifyPidReuseAsync()
    {
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in new[] { typeof(OrdinaryLauncherProcessLifetimeTests).Assembly.Location, "-method", "*ThisPidReuseControlDoesNotExist" })
        { start.ArgumentList.Add(argument); }
        (int exit, string output) = await RunOwnedRunnerAsync(start, TestContext.Current.CancellationToken).ConfigureAwait(true);
        exit.ShouldBe(0); output.ShouldContain("Total: 0");
        Should.Throw<Shouldly.ShouldAssertException>(() => RequireSuccessfulPidReuseControl(exit, output));
        TestContext.Current.TestOutputHelper?.WriteLine(output);
    }

    private static void NamespaceUnavailable(string reason)
    {
        if (Environment.GetEnvironmentVariable("PARTIES_PROCESS_NAMESPACE_REQUIRED") == "true")
        { throw new InvalidOperationException($"Namespace qualification unsupported: {reason}"); }
        Assert.Skip($"Namespace qualification unsupported: {reason}");
    }

    /// <summary>An absent real namespace executable is an explicit ordinary skip and a strict qualification failure.</summary>
    /// <param name="strict">Whether namespace qualification is required.</param>
    /// <param name="denied">Whether to retain real EPERM from a child-scoped unshare syscall denial.</param>
    [Theory(Timeout = 25000)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task UnavailableNamespaceExecutableCannotQualifyAsync(bool strict, bool denied)
    {
        if (!OperatingSystem.IsLinux()) { Assert.Skip("Requires Linux process controls."); }
        if (denied && System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture != System.Runtime.InteropServices.Architecture.X64)
        { Assert.Skip("The owned seccomp unshare-denial control uses the Linux x64 syscall number."); }
        string emptyPath = Path.Combine(Path.GetTempPath(), "parties-empty-tools-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(emptyPath);
        try
        {
            var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            if (denied)
            {
                start = new ProcessStartInfo("python3") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
                start.ArgumentList.Add("-c");
                // This filter affects only this owned child and its descendants; the real
                // unshare syscall returns EPERM before namespace entry or PID mutation.
                start.ArgumentList.Add("import ctypes,os,sys\nclass F(ctypes.Structure): _fields_=[('code',ctypes.c_ushort),('jt',ctypes.c_ubyte),('jf',ctypes.c_ubyte),('k',ctypes.c_uint)]\nclass P(ctypes.Structure): _fields_=[('length',ctypes.c_ushort),('filter',ctypes.POINTER(F))]\nf=(F*4)(F(0x20,0,0,0),F(0x15,0,1,272),F(0x06,0,0,0x50001),F(0x06,0,0,0x7fff0000))\np=P(4,f)\nlibc=ctypes.CDLL(None,use_errno=True)\nassert libc.prctl(38,1,0,0,0)==0\nassert libc.prctl(22,2,ctypes.byref(p),0,0)==0\nos.execv(sys.argv[1],sys.argv[1:])");
                start.ArgumentList.Add(Environment.ProcessPath!);
            }
            foreach (string argument in new[] { typeof(OrdinaryLauncherProcessLifetimeTests).Assembly.Location, "-method", "*IsolatedPidReuseDuringRoleReadRefusesQualificationAsync" })
            { start.ArgumentList.Add(argument); }
            if (!denied) { start.Environment["PATH"] = emptyPath; }
            start.Environment["PARTIES_PROCESS_NAMESPACE_REQUIRED"] = strict ? "true" : "false";
            start.Environment.Remove(ChildMarker);
            (int exit, string output) = await RunOwnedRunnerAsync(start, TestContext.Current.CancellationToken).ConfigureAwait(true);
            exit.ShouldBe(strict ? 1 : 0);
            TestContext.Current.TestOutputHelper?.WriteLine(output);
            output.ShouldContain("Namespace qualification unsupported");
            if (denied) { output.ShouldContain("Operation not permitted"); }
            output.ShouldContain(strict ? "Failed: 1" : "Skipped: 1");
            output.ShouldNotContain("PID reuse observed");
        }
        finally { Directory.Delete(emptyPath); }
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
