using System.Collections.Concurrent;
using System.Diagnostics;

using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Fault-injected observation controls for the live ordinary launcher timing gate.</summary>
public sealed class OrdinaryLauncherProcessCaptureTests
{
    /// <summary>Refuses partial trees even when another sibling supplies both required roles.</summary>
    /// <param name="failure">Process observation boundary to fail.</param>
    [Theory]
    [InlineData("tasks")]
    [InlineData("empty-tasks")]
    [InlineData("children")]
    [InlineData("identity")]
    [InlineData("missing-identity")]
    [InlineData("comm")]
    [InlineData("empty-comm")]
    [InlineData("access")]
    [InlineData("invalid-pid")]
    public void FailedProcessReadRetainsDiagnosticsContinuesSiblingsAndRefusesQualification(string failure)
    {
        ConcurrentDictionary<int, (long Started, string Role, string Resource)> workers = new();
        ConcurrentDictionary<string, int> launchers = new(StringComparer.Ordinal);
        launchers["resource"] = 1;
        workers[1] = (1, "launcher", "resource");
        OrdinaryLauncherProcessCapture capture = new(
            enumerateTasks: id => failure == "tasks" && id == 2
                ? throw new IOException("injected task enumeration failure")
                : id == 2 && failure == "empty-tasks" ? [] : id == 1 && failure == "children" ? ["9", "1"] : [id.ToString(System.Globalization.CultureInfo.InvariantCulture)],
            readText: path => {
                if (path == "9/children" && failure == "children") { throw new IOException("injected children failure"); }
                if (path == "/proc/2/comm" && failure == "empty-comm") { return " \t\r\n"; }
                if (path == "/proc/2/comm" && failure == "comm") { throw new IOException("injected comm failure"); }
                if (path == "/proc/2/comm" && failure == "access") { throw new UnauthorizedAccessException("injected access failure"); }
                return path switch {
                    "1/children" => failure == "invalid-pid" ? "invalid 2 3 4" : "2 3 4",
                    "9/children" => "",
                    "2/children" or "3/children" or "4/children" => "",
                    "/proc/2/status" or "/proc/3/status" or "/proc/4/status" => "PPid: 1",
                    "/proc/2/comm" or "/proc/3/comm" => "dapr",
                    "/proc/4/comm" => "daprd",
                    _ => throw new InvalidOperationException(path),
                };
            },
            readIdentity: id => id == 2 ? failure switch {
                "identity" => throw new IOException("injected identity failure"),
                "missing-identity" => null,
                _ => id,
            } : id);
        // A failed task in the first subtree must not prevent the root's remaining siblings being captured.
        capture.CaptureDescendants(1, "resource", workers);
        workers[3].Role.ShouldBe("cli");
        workers[4].Role.ShouldBe("daprd");
        capture.Failures.ShouldNotBeEmpty();
        capture.QualificationFailures(launchers, workers).ShouldNotBeEmpty();
        string.Join(";", capture.Failures).ShouldContain("resource");
    }

    /// <summary>Refuses stale child listings while retaining true siblings and exact ownership.</summary>
    /// <param name="status">The observed child parent relationship.</param>
    [Theory]
    [InlineData("PPid: 9")]
    [InlineData("Name: dapr")]
    [InlineData("PPid: invalid")]
    [InlineData("PPid: 1\nPPid: 1")]
    public void ForeignMissingOrMalformedParentRefusesAndRetainsIndependentSiblings(string status)
    {
        var workers = new ConcurrentDictionary<int, (long Started, string Role, string Resource)>();
        var capture = new OrdinaryLauncherProcessCapture(id => [id.ToString(System.Globalization.CultureInfo.InvariantCulture)],
            path => path switch {
                "1/children" => "2 3 4", "2/children" or "3/children" or "4/children" => "",
                "/proc/2/comm" => "dapr",
                "/proc/2/status" => status, "/proc/3/status" or "/proc/4/status" => "PPid: 1",
                "/proc/3/comm" => "dapr", "/proc/4/comm" => "daprd",
                _ => throw new InvalidOperationException(path),
            }, id => id);
        capture.CaptureDescendants(1, "owned", workers);
        workers.ShouldNotContainKey(2);
        workers[3].Role.ShouldBe("cli"); workers[4].Role.ShouldBe("daprd");
        capture.Failures.ShouldContain(item => item.Contains("parent membership", StringComparison.Ordinal));
    }

    /// <summary>Requires CLI and runtime observations for every launcher rather than once globally.</summary>
    /// <param name="missingRole">Required role missing from one resource.</param>
    [Theory]
    [InlineData("cli")]
    [InlineData("daprd")]
    public void MissingRoleOnOneLauncherRefusesEvenWhenAnotherLauncherHasIt(string missingRole)
    {
        ConcurrentDictionary<string, int> launchers = new(StringComparer.Ordinal);
        launchers["complete"] = 1;
        launchers["incomplete"] = 4;
        ConcurrentDictionary<int, (long Started, string Role, string Resource)> workers = new();
        workers[1] = (1, "launcher", "complete");
        workers[2] = (2, "cli", "complete");
        workers[3] = (3, "daprd", "complete");
        workers[4] = (4, "launcher", "incomplete");
        workers[5] = (5, missingRole == "cli" ? "daprd" : "cli", "incomplete");
        OrdinaryLauncherProcessCapture capture = new();
        string[] refusals = capture.QualificationFailures(launchers, workers);
        refusals.Length.ShouldBe(1);
        refusals[0].ShouldContain("incomplete");
        refusals[0].ShouldContain(missingRole);
    }

    /// <summary>A complete process tree remains eligible for the live exit-status and budget checks.</summary>
    [Fact]
    public void CompleteTreeRetainsBothRolesAndAllowsTimingQualification()
    {
        ConcurrentDictionary<string, int> launchers = new(StringComparer.Ordinal);
        launchers["complete"] = 1;
        ConcurrentDictionary<int, (long Started, string Role, string Resource)> workers = new();
        workers[1] = (1, "launcher", "complete");
        OrdinaryLauncherProcessCapture capture = new(id => [id.ToString(System.Globalization.CultureInfo.InvariantCulture)],
            path => path switch {
                "1/children" => "2",
                "2/children" => "3",
                "3/children" => "",
                "/proc/2/status" => "PPid: 1",
                "/proc/3/status" => "PPid: 2",
                "/proc/2/comm" => "dapr",
                "/proc/3/comm" => "daprd",
                _ => throw new InvalidOperationException(path),
            }, id => id);
        capture.CaptureDescendants(1, "complete", workers);
        workers.Count.ShouldBe(3);
        capture.Failures.ShouldBeEmpty();
        capture.QualificationFailures(launchers, workers).ShouldBeEmpty();
    }

    /// <summary>A stale retained launcher identity cannot qualify a complete stable replacement tree.</summary>
    [Fact]
    public void RetainedLauncherLifetimeMismatchRefusesQualification()
    {
        var launchers = new ConcurrentDictionary<string, int>(StringComparer.Ordinal); launchers["owned"] = 1;
        var workers = new ConcurrentDictionary<int, (long Started, string Role, string Resource)>();
        workers[1] = (99, "launcher", "owned");
        bool enumerated = false;
        var capture = new OrdinaryLauncherProcessCapture(id => { enumerated = true; return [id.ToString(System.Globalization.CultureInfo.InvariantCulture)]; },
            path => path switch { "1/children" => "2 3", "2/children" or "3/children" => "", "/proc/2/comm" => "dapr", "/proc/3/comm" => "daprd", _ => throw new InvalidOperationException(path) },
            id => id);
        capture.CaptureDescendants(1, "owned", workers);
        enumerated.ShouldBeFalse(); workers.Count.ShouldBe(1);
        capture.Failures.ShouldContain(item => item.Contains("lifetime changed before descendant enumeration", StringComparison.Ordinal));
        capture.QualificationFailures(launchers, workers).ShouldNotBeEmpty();
    }

    /// <summary>An unreadable tree still returns control to fresh bounded owned cleanup before refusing qualification.</summary>
    [Fact(Timeout = 15000)]
    public async Task IncompleteObservationStillReachesOwnedProcessCleanupAsync()
    {
        if (!OperatingSystem.IsLinux()) { Assert.Skip("The ordinary process observer is Linux-only."); }
        var owned = new List<Process>();
        Process process = Process.Start(new ProcessStartInfo("sleep", "120") { UseShellExecute = false })!;
        owned.Add(process);
        var launchers = new ConcurrentDictionary<string, int>(StringComparer.Ordinal); launchers["owned"] = process.Id;
        var workers = new ConcurrentDictionary<int, (long Started, string Role, string Resource)>();
        var capture = new OrdinaryLauncherProcessCapture(enumerateTasks: _ => throw new IOException("injected read failure"));
        Exception? primary = null;
        try { TestContext.Current.CancellationToken.ThrowIfCancellationRequested(); capture.CaptureDescendants(process.Id, "owned", workers); }
        catch (Exception failure) { primary = failure; }
        finally { await CleanupOwnedProcessesAsync(owned, primary).ConfigureAwait(true); }
        if (primary is not null) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(primary).Throw(); }
        capture.Failures.ShouldNotBeEmpty();
        capture.QualificationFailures(launchers, workers).ShouldNotBeEmpty();
    }

    /// <summary>Refuses real procfs enumeration opened before parent exit while an owned child survives.</summary>
    [Fact(Timeout = 25000)]
    public Task ParentExitCanYieldEmptyTaskEnumerationWhileChildSurvivesAsync()
        => RunParentExitControlAsync(TestContext.Current.CancellationToken);

    /// <summary>Acquisition failures retain cleanup ownership before parsing or cancellation can fail.</summary>
    [Theory(Timeout = 20000)]
    [InlineData("cancellation")]
    [InlineData("malformed")]
    public async Task FailedChildAcquisitionCleansEveryOwnedProcess(string failure)
    {
        if (!OperatingSystem.IsLinux()) { Assert.Skip("Requires Linux procfs."); }
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        Process? observedChild = null;
        Exception? primary = null;
        try
        {
            Task Control() => RunParentExitControlAsync(operation.Token, async (parent, token) =>
            {
                string? line = await parent.StandardOutput.ReadLineAsync(token).ConfigureAwait(true);
                observedChild = Process.GetProcessById(int.Parse(line!, System.Globalization.CultureInfo.InvariantCulture));
                if (failure == "cancellation") { await operation.CancelAsync().ConfigureAwait(true); operation.Token.ThrowIfCancellationRequested(); }
                return "malformed-child-id";
            });
            if (failure == "cancellation") { await Should.ThrowAsync<OperationCanceledException>(Control); }
            else { await Should.ThrowAsync<FormatException>(Control); }
            using var exit = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await observedChild.ShouldNotBeNull().WaitForExitAsync(exit.Token);
            observedChild.HasExited.ShouldBeTrue();
        }
        catch (Exception exception) { primary = exception; }
        finally
        {
            if (observedChild is not null) { await CleanupOwnedProcessesAsync([observedChild], primary); }
        }
        if (primary is not null) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(primary).Throw(); }
    }

    private static async Task RunParentExitControlAsync(CancellationToken token, Func<Process, CancellationToken, Task<string?>>? readChild = null)
    {
        if (!OperatingSystem.IsLinux()) { Assert.Skip("Requires Linux procfs."); }
        var start = new ProcessStartInfo("sh") { RedirectStandardInput = true, RedirectStandardOutput = true, UseShellExecute = false };
        start.ArgumentList.Add("-c"); start.ArgumentList.Add("sleep 120 & echo $!; read done");
        var owned = new List<Process>();
        Process parent = Process.Start(start)!;
        owned.Add(parent);
        Exception? primary = null;
        try
        {
            using var acquisition = CancellationTokenSource.CreateLinkedTokenSource(token);
            acquisition.CancelAfter(TimeSpan.FromSeconds(5));
            string? line = readChild is null ? await parent.StandardOutput.ReadLineAsync(acquisition.Token).ConfigureAwait(false) : await readChild(parent, acquisition.Token).ConfigureAwait(false);
            int childId = int.Parse(line!, System.Globalization.CultureInfo.InvariantCulture);
            Process child = Process.GetProcessById(childId); owned.Add(child);
            string[]? tasks = null;
            var capture = new OrdinaryLauncherProcessCapture(enumerateTasks: id =>
            {
                // Open procfs now; enumerate only after the bounded parent exit.
                IEnumerable<string> opened = Directory.EnumerateDirectories($"/proc/{id}/task");
                parent.StandardInput.WriteLine("done"); parent.StandardInput.Flush();
                if (!parent.WaitForExit(5000)) { throw new TimeoutException("The owned procfs parent did not exit."); }
                return tasks = opened.ToArray();
            });
            var workers = new ConcurrentDictionary<int, (long Started, string Role, string Resource)>();
            var launchers = new ConcurrentDictionary<string, int>(StringComparer.Ordinal); launchers["owned"] = parent.Id;
            capture.CaptureDescendants(parent.Id, "owned", workers);
            tasks.ShouldNotBeNull().ShouldBeEmpty(); child.HasExited.ShouldBeFalse();
            capture.Failures.ShouldContain(item => item.Contains("empty task enumeration", StringComparison.Ordinal));
            capture.QualificationFailures(launchers, workers).ShouldNotBeEmpty();
        }
        catch (Exception failure) { primary = failure; }
        finally { await CleanupOwnedProcessesAsync(owned, primary).ConfigureAwait(true); }
        if (primary is not null) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(primary).Throw(); }
    }

    private static async Task CleanupOwnedProcessesAsync(List<Process> owned, Exception? primary)
    {
        var failures = new List<Exception>();
        foreach (Process process in owned.AsEnumerable().Reverse())
        {
            try
            {
                // A canceled acquisition or a failed child's cleanup cannot cancel the parent's cleanup.
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                if (!process.HasExited) { process.Kill(entireProcessTree: true); }
                await process.WaitForExitAsync(cleanup.Token).ConfigureAwait(false);
                process.HasExited.ShouldBeTrue();
            }
            catch (Exception failure) { failures.Add(failure); }
            finally { process.Dispose(); }
        }
        if (failures.Count != 0)
        {
            if (primary is not null) { failures.Insert(0, primary); }
            throw new AggregateException("Owned process cleanup failed.", failures);
        }
    }

    /// <summary>Changes during role, task or children reads cannot combine observations from different lifetimes.</summary>
    [Theory]
    [InlineData("comm", false)]
    [InlineData("comm", true)]
    [InlineData("tasks", false)]
    [InlineData("status", false)]
    [InlineData("status", true)]
    [InlineData("children", false)]
    public void LifetimeChangesRefuseAndContinueIndependentSiblings(string boundary, bool disappeared)
    {
        bool changed = false;
        ConcurrentDictionary<int, (long Started, string Role, string Resource)> workers = new();
        ConcurrentDictionary<string, int> launchers = new(StringComparer.Ordinal); launchers["owned"] = 1;
        OrdinaryLauncherProcessCapture capture = new(id =>
        {
            if (id == 2 && boundary == "tasks") { changed = true; }
            return [id.ToString(System.Globalization.CultureInfo.InvariantCulture)];
        }, path =>
        {
            if (path == "/proc/2/comm" && boundary == "comm" || path == "2/children" && boundary == "children" || path == "/proc/2/status" && boundary == "status") { changed = true; }
            return path switch { "1/children" => "2 3 4", "2/children" or "3/children" or "4/children" => "",
                "/proc/2/status" or "/proc/3/status" or "/proc/4/status" => "PPid: 1",
                "/proc/2/comm" or "/proc/3/comm" => "dapr", "/proc/4/comm" => "daprd", _ => throw new InvalidOperationException(path) };
        }, id => id == 2 && changed ? disappeared ? null : 200 : id);
        capture.CaptureDescendants(1, "owned", workers);
        workers[3].Role.ShouldBe("cli"); workers[4].Role.ShouldBe("daprd");
        capture.Failures.ShouldContain(item => item.Contains("lifetime changed", StringComparison.Ordinal));
        capture.QualificationFailures(launchers, workers).ShouldNotBeEmpty();
    }
}
