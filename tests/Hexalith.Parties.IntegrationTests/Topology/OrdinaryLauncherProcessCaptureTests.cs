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
                : id == 1 && failure == "children" ? ["9", "1"] : [id.ToString(System.Globalization.CultureInfo.InvariantCulture)],
            readText: path => {
                if (path == "9/children" && failure == "children") { throw new IOException("injected children failure"); }
                if (path == "/proc/2/comm" && failure == "empty-comm") { return " \t\r\n"; }
                if (path == "/proc/2/comm" && failure == "comm") { throw new IOException("injected comm failure"); }
                if (path == "/proc/2/comm" && failure == "access") { throw new UnauthorizedAccessException("injected access failure"); }
                return path switch {
                    "1/children" => failure == "invalid-pid" ? "invalid 2 3 4" : "2 3 4",
                    "9/children" => "",
                    "2/children" or "3/children" or "4/children" => "",
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
                "/proc/2/comm" => "dapr",
                "/proc/3/comm" => "daprd",
                _ => throw new InvalidOperationException(path),
            }, id => id);
        capture.CaptureDescendants(1, "complete", workers);
        workers.Count.ShouldBe(3);
        capture.Failures.ShouldBeEmpty();
        capture.QualificationFailures(launchers, workers).ShouldBeEmpty();
    }

    /// <summary>An unreadable tree still returns control to the owner's cleanup before refusing qualification.</summary>
    /// <returns>A task completing after the owned real process is reaped.</returns>
    [Fact]
    public async Task IncompleteObservationStillReachesOwnedProcessCleanupAsync()
    {
        if (!OperatingSystem.IsLinux()) { Assert.Skip("The ordinary process observer is Linux-only."); }
        using Process owned = Process.Start(new ProcessStartInfo("sleep", "120") { UseShellExecute = false })!;
        ConcurrentDictionary<string, int> launchers = new(StringComparer.Ordinal);
        launchers["owned"] = owned.Id;
        ConcurrentDictionary<int, (long Started, string Role, string Resource)> workers = new();
        OrdinaryLauncherProcessCapture capture = new(enumerateTasks: _ => throw new IOException("injected read failure"));
        try { capture.CaptureDescendants(owned.Id, "owned", workers); }
        finally {
            owned.Kill(entireProcessTree: true);
            await owned.WaitForExitAsync(TestContext.Current.CancellationToken);
        }
        owned.HasExited.ShouldBeTrue();
        capture.Failures.ShouldNotBeEmpty();
        capture.QualificationFailures(launchers, workers).ShouldNotBeEmpty();
    }
}
