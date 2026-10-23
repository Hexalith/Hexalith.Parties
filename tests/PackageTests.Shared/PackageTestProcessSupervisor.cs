using System.Diagnostics;
using System.Text;

namespace Hexalith.Parties.PackageTests;

/// <summary>Contains Linux test commands in an exclusive session and descendant reaper.</summary>
internal sealed class PackageTestProcessSupervisor : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"parties-process-{Guid.NewGuid():N}");

    /// <summary>Creates a dedicated supervisor without changing the test runner's child ownership.</summary>
    internal PackageTestProcessSupervisor(ProcessStartInfo command, int cleanupMilliseconds)
    {
        if (!OperatingSystem.IsLinux())
        {
            StartInfo = command;
            return;
        }

        Directory.CreateDirectory(_directory);
        StartInfo = new ProcessStartInfo("python3")
        {
            WorkingDirectory = command.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        StartInfo.ArgumentList.Add("-c");
        StartInfo.ArgumentList.Add(Script);
        StartInfo.ArgumentList.Add(_directory);
        StartInfo.ArgumentList.Add(Math.Max(1, cleanupMilliseconds - 100).ToString(System.Globalization.CultureInfo.InvariantCulture));
        StartInfo.ArgumentList.Add(command.FileName);
        if (command.ArgumentList.Count > 0)
        {
            foreach (string argument in command.ArgumentList)
            {
                StartInfo.ArgumentList.Add(argument);
            }
        }
        else
        {
            // Let ProcessStartInfo itself parse the original Arguments string.
            // Only the fixed supervisor prefix needs quoting for the same runtime.
            StartInfo.Arguments = string.Join(' ', StartInfo.ArgumentList.Select(QuoteArgument)) + " " + command.Arguments;
            StartInfo.ArgumentList.Clear();
        }
    }

    /// <summary>Gets the executable supervisor start information.</summary>
    internal ProcessStartInfo StartInfo { get; }

    /// <summary>Gets whether dedicated descendant containment is available.</summary>
    internal bool IsContained { get; } = OperatingSystem.IsLinux();

    /// <summary>Requests bounded cleanup, including after the command's direct parent exited.</summary>
    internal void RequestTermination() => File.WriteAllText(Path.Combine(_directory, "stop"), string.Empty);

    /// <summary>Reports command launch or containment failures independently of its exit code.</summary>
    internal void EnsureCleanupSucceeded(string fileName)
    {
        string failurePath = Path.Combine(_directory, "failure");
        if (File.Exists(failurePath))
        {
            string failure = File.ReadAllText(failurePath);
            throw new InvalidOperationException($"Could not start {fileName} or contain its descendants: {failure}", new System.ComponentModel.Win32Exception(failure));
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    /// <summary>Quotes one supervisor prefix argument using ProcessStartInfo's backslash rules.</summary>
    private static string QuoteArgument(string value)
    {
        var quoted = new StringBuilder("\"");
        int backslashes = 0;
        foreach (char character in value)
        {
            if (character == '\\')
            {
                backslashes++;
                continue;
            }

            quoted.Append('\\', character == '"' ? (backslashes * 2) + 1 : backslashes);
            quoted.Append(character);
            backslashes = 0;
        }

        quoted.Append('\\', backslashes * 2);
        quoted.Append('"');
        return quoted.ToString();
    }

    // The supervisor is the sole subreaper for this command. No runner-wide prctl,
    // process-name matching, or global waitpid can affect another worker's children.
    private const string Script = """
        import ctypes, os, pathlib, signal, subprocess, sys, time

        directory = pathlib.Path(sys.argv[1])
        cleanup_seconds = int(sys.argv[2]) / 1000
        child = None
        exit_code = 125

        def cleanup():
            deadline = time.monotonic() + cleanup_seconds
            try:
                os.killpg(child.pid, signal.SIGKILL)
            except ProcessLookupError:
                pass
            while True:
                if time.monotonic() >= deadline:
                    raise TimeoutError('owned descendants exceeded cleanup deadline')
                # Once parents die, even descendants that created a new session
                # are adopted by this exclusive subreaper and remain owned.
                children = pathlib.Path('/proc/self/task/' + str(os.getpid()) + '/children').read_text().split()
                for pid in children:
                    try:
                        os.kill(int(pid), signal.SIGKILL)
                    except ProcessLookupError:
                        pass
                try:
                    pid, status = os.waitpid(-1, os.WNOHANG)
                except ChildProcessError:
                    return
                if pid == 0:
                    time.sleep(0.005)

        try:
            libc = ctypes.CDLL(None, use_errno=True)
            if libc.prctl(36, 1, 0, 0, 0) != 0:
                raise OSError(ctypes.get_errno(), 'subreaper initialization failed')
            child = subprocess.Popen(sys.argv[3:], start_new_session=True)
            while child.poll() is None and not (directory / 'stop').exists():
                time.sleep(0.005)
            exit_code = child.returncode if child.returncode is not None else 124
        except Exception as error:
            (directory / 'failure').write_text(str(error))
        finally:
            if child is not None:
                try:
                    cleanup()
                except Exception as error:
                    (directory / 'failure').write_text(str(error))
                    exit_code = 125
        sys.exit(exit_code if exit_code >= 0 else 128 - exit_code)
        """;
}
