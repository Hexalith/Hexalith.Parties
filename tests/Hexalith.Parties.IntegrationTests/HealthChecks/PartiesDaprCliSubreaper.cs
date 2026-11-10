using System.Globalization;
using System.Text.Json;

namespace Hexalith.Parties.IntegrationTests.HealthChecks;

/// <summary>Writes the test-owned launcher that runs the ordinary Dapr CLI under an exclusive child subreaper.</summary>
/// <remarks>The orchestrator observes the launcher's PID. Every descendant orphaned by the CLI, including a runtime
/// orphaned before the fixture's first observation, is adopted by that PID, so polling still finds it. SIGTERM and
/// SIGINT are forwarded to every child; children that remain after the grace period are killed. The launcher exits
/// only once it has reaped every child, so its exit never leaves an escaped descendant.</remarks>
internal static class PartiesDaprCliSubreaper
{
    /// <summary>The launcher file name the topology fixture recognizes.</summary>
    internal const string LauncherName = "dapr-subreaper";

    // PR_SET_CHILD_SUBREAPER is 36. Exit 125 reports a launcher failure before the CLI started.
    private const string Script = """
        import ctypes, os, signal, sys, time

        requested = []

        def request(signum, frame):
            if not requested:
                requested.append(signum)

        signal.signal(signal.SIGTERM, request)
        signal.signal(signal.SIGINT, request)
        if ctypes.CDLL(None, use_errno=True).prctl(36, 1, 0, 0, 0) != 0:
            sys.exit(125)
        restore = [signal.SIGPIPE, signal.SIGXFSZ]
        try:
            cli = os.posix_spawn(CLI, [CLI, *sys.argv[1:]], os.environ, setsigdef=restore)
        except OSError:
            sys.exit(125)
        exit_code, deadline, signalled = 125, None, set()

        def children():
            found = set()
            for task in os.listdir('/proc/self/task'):
                try:
                    with open('/proc/self/task/' + task + '/children') as handle:
                        found.update(int(pid) for pid in handle.read().split())
                except FileNotFoundError:
                    pass
            return found

        while True:
            if requested:
                deadline = deadline or time.monotonic() + GRACE
                escalate = time.monotonic() >= deadline
                for pid in children():
                    if escalate or pid not in signalled:
                        signalled.add(pid)
                        try:
                            os.kill(pid, signal.SIGKILL if escalate else requested[0])
                        except ProcessLookupError:
                            pass
            try:
                pid, status = os.waitpid(-1, os.WNOHANG)
            except ChildProcessError:
                break
            if pid == 0:
                time.sleep(0.01)
            elif pid == cli:
                code = os.waitstatus_to_exitcode(status)
                exit_code = code if code >= 0 else 128 - code
        sys.exit(128 + requested[0] if requested else exit_code)
        """;

    /// <summary>Gets whether a configured Dapr path names this launcher.</summary>
    /// <param name="daprPath">The configured Dapr CLI path.</param>
    /// <returns>Whether the path's file name is the launcher name.</returns>
    internal static bool IsLauncher(string? daprPath) => Path.GetFileName(daprPath) == LauncherName;

    /// <summary>Writes an owner-only launcher for one CLI into a private directory.</summary>
    /// <param name="directory">The private directory that receives the launcher.</param>
    /// <param name="cliPath">The absolute path of the CLI the launcher runs.</param>
    /// <param name="grace">How long forwarded termination may take before remaining children are killed.</param>
    /// <returns>The absolute launcher path to configure as the Dapr path.</returns>
    internal static string Create(string directory, string cliPath, TimeSpan grace)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(cliPath);
        if (!OperatingSystem.IsLinux() || !Path.IsPathFullyQualified(cliPath) || grace <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("The Dapr CLI subreaper requires Linux, an absolute CLI path and a positive grace period.");
        }

        _ = Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        string launcher = Path.Combine(Path.GetFullPath(directory), LauncherName);
        // A JSON string is also a valid Python string literal; isolated mode ignores PYTHON* variables and user sites.
        string body = "#!/usr/bin/env -S python3 -I\n"
            + "CLI = " + JsonSerializer.Serialize(cliPath) + "\n"
            + "GRACE = " + grace.TotalSeconds.ToString("R", CultureInfo.InvariantCulture) + "\n"
            + Script.ReplaceLineEndings("\n") + "\n";
        File.WriteAllText(launcher, body);
        File.SetUnixFileMode(launcher, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return launcher;
    }

    /// <summary>Resolves the CLI the Dapr hosting integration selects on Linux when no path is configured.</summary>
    /// <returns>The first existing candidate, or null when no CLI is installed.</returns>
    internal static string? ResolveDefaultCli()
    {
        IEnumerable<string> candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "dapr", "dapr"),
            "/usr/local/bin/dapr",
            "/usr/bin/dapr",
        }.Concat((Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.Combine(directory, "dapr")));
        return candidates.Where(Path.IsPathFullyQualified).FirstOrDefault(File.Exists);
    }

    /// <summary>Gets whether a live process is this launcher, as started from its exact path.</summary>
    /// <param name="processId">The observed wrapper process.</param>
    /// <param name="launcher">The configured launcher path.</param>
    /// <returns>Whether the process command line runs exactly that launcher.</returns>
    internal static bool IsRunning(int processId, string launcher)
    {
        try
        {
            string[] arguments = File.ReadAllText($"/proc/{processId}/cmdline").Split('\0');
            // The kernel runs the interpreter named by the shebang; the launcher path follows its options.
            int index = 1;
            while (index < arguments.Length && arguments[index].StartsWith('-'))
            {
                index++;
            }

            return index < arguments.Length && arguments[index] == launcher;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
}
