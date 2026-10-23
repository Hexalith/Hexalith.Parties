using System.Diagnostics;
using System.Text.Json;

using Hexalith.Parties.PackageTests;

using Shouldly;

namespace Hexalith.Parties.Contracts.Tests.Package;

public sealed class PackageTestProcessTests
{
    [Fact]
    public void RunReportsStartFailure()
    {
        string executable = $"hexalith-parties-missing-{Guid.NewGuid():N}";

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() =>
            PackageTestProcess.Run(executable, string.Empty, Directory.GetCurrentDirectory()));

        exception.Message.ShouldContain($"Could not start {executable}");
        exception.InnerException.ShouldNotBeNull();
    }

    [Fact]
    public void RunKillsTimedOutProcessWithinPostKillBound()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        TimeoutException exception = Should.Throw<TimeoutException>(() =>
            PackageTestProcess.Run(
                "python3",
                "-c \"import time; time.sleep(30)\"",
                Directory.GetCurrentDirectory(),
                processTimeoutMilliseconds: 100,
                postKillExitTimeoutMilliseconds: 2_000,
                outputCaptureTimeoutMilliseconds: 2_000));

        stopwatch.Stop();
        exception.Message.ShouldContain("python3 exceeded the 100 ms timeout");
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(6));
    }

    [Fact]
    public void RunDrainsLargeStandardOutputAndErrorStreams()
    {
        PackageTestProcessResult result = PackageTestProcess.Run(
            "python3",
            "-c \"import sys; sys.stdout.write('o' * 200000); sys.stderr.write('e' * 200000)\"",
            Directory.GetCurrentDirectory(),
            processTimeoutMilliseconds: 10_000);

        result.ExitCode.ShouldBe(0);
        result.StandardOutput.Length.ShouldBe(200_000);
        result.StandardError.Length.ShouldBe(200_000);
    }

    [Fact]
    public void DiagnosticFallsBackToStandardOutput()
    {
        PackageTestProcessResult result = PackageTestProcess.Run(
            "python3",
            "-c \"import sys; print('stdout diagnostic'); sys.exit(7)\"",
            Directory.GetCurrentDirectory(),
            processTimeoutMilliseconds: 10_000);

        result.ExitCode.ShouldBe(7);
        result.StandardError.ShouldBeEmpty();
        result.Diagnostic.Trim().ShouldBe("stdout diagnostic");
    }

    [Fact]
    public void RunPreservesLiteralArgumentBoundariesAndWhitespace()
    {
        const string argument = "  value with spaces, \"quotes\", \\slashes and café  ";
        PackageTestProcessResult result = PackageTestProcess.Run(
            "python3",
            ["-c", "import json, sys; print(json.dumps(sys.argv[1:]))", argument, string.Empty, "-option"],
            Directory.GetCurrentDirectory());

        result.ExitCode.ShouldBe(0);
        JsonSerializer.Deserialize<string[]>(result.StandardOutput).ShouldBe([argument, string.Empty, "-option"]);
        result.StandardError.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("plain \"with spaces\" \"\" -option")]
    [InlineData("\"with \\\"quotes\\\"\" C:\\path\\file \"C:\\path with spaces\\\\\"")]
    [InlineData("\"two\\\\\\\\\\\"slashes and quote\" tail\\")]
    public void RunStringArgumentsMatchRuntimeParsing(string arguments)
    {
        const string prefix = "-c \"import json, sys; print(json.dumps(sys.argv[1:]))\" ";
        using var direct = new Process
        {
            StartInfo = new ProcessStartInfo("python3")
            {
                Arguments = prefix + arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };
        direct.Start().ShouldBeTrue();
        string expected = direct.StandardOutput.ReadToEnd();
        direct.WaitForExit(5_000).ShouldBeTrue();
        direct.ExitCode.ShouldBe(0);

        PackageTestProcessResult result = PackageTestProcess.Run("python3", prefix + arguments, Directory.GetCurrentDirectory());
        result.ExitCode.ShouldBe(0);
        result.StandardOutput.ShouldBe(expected);
        result.StandardError.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RunReapsTimedOutProcessWithOpenOrClosedStreams(bool closeStreams)
    {
        string script = "import os, subprocess, sys, threading; "
            + "child = subprocess.Popen([sys.executable, '-c', 'import threading; threading.Event().wait()']); "
            + "print('childpid=' + str(child.pid), file=sys.stderr, flush=True); "
            + "print('pid=' + str(os.getpid()), file=sys.stderr, flush=True); "
            + (closeStreams ? "os.close(1); os.close(2); " : string.Empty)
            + "threading.Event().wait()";
        Stopwatch stopwatch = Stopwatch.StartNew();

        TimeoutException exception = Should.Throw<TimeoutException>(() =>
            PackageTestProcess.Run(
                "python3", ["-c", script], Directory.GetCurrentDirectory(),
                processTimeoutMilliseconds: 1_000,
                postKillExitTimeoutMilliseconds: 2_000,
                outputCaptureTimeoutMilliseconds: 2_000));

        exception.Message.ShouldContain("python3 exceeded the 1000 ms timeout");
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(6));
        string pidText = exception.Message.Split('\n')
            .Single(static line => line.StartsWith("pid=", StringComparison.Ordinal))[4..].Trim();
        int processId = int.Parse(pidText, System.Globalization.CultureInfo.InvariantCulture);
        Should.Throw<ArgumentException>(() => Process.GetProcessById(processId));
        if (OperatingSystem.IsLinux())
        {
            string childText = exception.Message.Split('\n')
                .Single(static line => line.StartsWith("childpid=", StringComparison.Ordinal))[9..].Trim();
            int childId = int.Parse(childText, System.Globalization.CultureInfo.InvariantCulture);
            Should.Throw<ArgumentException>(() => Process.GetProcessById(childId));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RunRejectsUnboundedExecutionBeforeStarting(int timeoutMilliseconds)
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            PackageTestProcess.Run("missing-executable", Array.Empty<string>(), Directory.GetCurrentDirectory(),
                processTimeoutMilliseconds: timeoutMilliseconds));
    }

    [Fact]
    public void RunUsesOneDeadlineWhenStreamsCloseBeforeExit()
    {
        const string script = "import os, threading; "
            + "threading.Timer(2, lambda: (os.close(1), os.close(2))).start(); threading.Event().wait()";
        Stopwatch stopwatch = Stopwatch.StartNew();

        TimeoutException exception = Should.Throw<TimeoutException>(() =>
            PackageTestProcess.Run("python3", ["-c", script], Directory.GetCurrentDirectory(),
                processTimeoutMilliseconds: 3_000,
                postKillExitTimeoutMilliseconds: 500,
                outputCaptureTimeoutMilliseconds: 500));

        exception.Message.ShouldContain("python3 exceeded the 3000 ms timeout");
        exception.Message.ShouldContain("Arguments: -c " + script);
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromMilliseconds(4_500));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void RunReapsOwnedDescendantsAfterParentExit(bool closeStreams, bool separateSession)
    {
        string pidFile = Path.GetTempFileName();
        string childScript = (closeStreams ? "import os; os.close(1); os.close(2); " : string.Empty)
            + "import threading; threading.Event().wait()";
        string script = "import pathlib, subprocess, sys; "
            + "child = subprocess.Popen([sys.executable, '-c', sys.argv[2]], start_new_session=sys.argv[3] == 'True'); "
            + "pathlib.Path(sys.argv[1]).write_text(str(child.pid))";
        using Process unrelated = Process.Start(new ProcessStartInfo("python3")
        {
            ArgumentList = { "-c", "import threading; threading.Event().wait()" },
            UseShellExecute = false,
        })!;
        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            if (OperatingSystem.IsLinux() || closeStreams)
            {
                PackageTestProcessResult result = PackageTestProcess.Run(
                    "python3", ["-c", script, pidFile, childScript, separateSession.ToString()], Directory.GetCurrentDirectory(),
                    processTimeoutMilliseconds: 10_000,
                    postKillExitTimeoutMilliseconds: 1_000,
                    outputCaptureTimeoutMilliseconds: 100);
                result.ExitCode.ShouldBe(0);
            }
            else
            {
                // Other operating systems preserve their previous bounded capture behavior.
                Should.Throw<TimeoutException>(() => PackageTestProcess.Run(
                    "python3", ["-c", script, pidFile, childScript, separateSession.ToString()], Directory.GetCurrentDirectory(),
                    processTimeoutMilliseconds: 10_000,
                    postKillExitTimeoutMilliseconds: 1_000,
                    outputCaptureTimeoutMilliseconds: 100));
            }

            stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
            int processId = int.Parse(File.ReadAllText(pidFile), System.Globalization.CultureInfo.InvariantCulture);
            if (OperatingSystem.IsLinux())
            {
                // No fixture cleanup precedes this assertion: the production supervisor reaped it.
                Should.Throw<ArgumentException>(() => Process.GetProcessById(processId));
            }

            unrelated.HasExited.ShouldBeFalse();
        }
        finally
        {
            if (int.TryParse(File.ReadAllText(pidFile), out int processId))
            {
                StopFixtureProcess(processId);
            }

            StopFixtureProcess(unrelated.Id);
            File.Delete(pidFile);
        }
    }

    [Fact]
    public void RunPreservesEarlyFailureAndReapsItsOwnedDescendants()
    {
        string pidFile = Path.GetTempFileName();
        const string script = "import pathlib, subprocess, sys; "
            + "child = subprocess.Popen([sys.executable, '-c', 'import time; time.sleep(30)'], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL); "
            + "pathlib.Path(sys.argv[1]).write_text(str(child.pid)); print('early failure', file=sys.stderr); sys.exit(7)";
        try
        {
            PackageTestProcessResult result = PackageTestProcess.Run("python3", ["-c", script, pidFile], Directory.GetCurrentDirectory());
            result.ExitCode.ShouldBe(7);
            result.Diagnostic.Trim().ShouldBe("early failure");
            int processId = int.Parse(File.ReadAllText(pidFile), System.Globalization.CultureInfo.InvariantCulture);
            if (OperatingSystem.IsLinux())
            {
                Should.Throw<ArgumentException>(() => Process.GetProcessById(processId));
            }
        }
        finally
        {
            if (int.TryParse(File.ReadAllText(pidFile), out int processId))
            {
                StopFixtureProcess(processId);
            }

            File.Delete(pidFile);
        }
    }

    [Fact]
    public void RunReapsMultipleGenerationsThatCreateSeparateSessions()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"parties-process-family-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        const string script = """
            import os, pathlib, subprocess, sys, threading, time
            directory = pathlib.Path(sys.argv[1])
            depth = int(sys.argv[2])
            (directory / str(os.getpid())).write_text(str(depth))
            if depth:
                for _ in range(2):
                    subprocess.Popen([sys.executable, '-c', sys.argv[3], str(directory), str(depth - 1), sys.argv[3]],
                        start_new_session=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            if depth == 3:
                while len(list(directory.iterdir())) < 15:
                    time.sleep(0.005)
            else:
                threading.Event().wait()
            """;
        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            PackageTestProcessResult result = PackageTestProcess.Run(
                "python3", ["-c", script, directory, "3", script], Directory.GetCurrentDirectory(),
                processTimeoutMilliseconds: 10_000,
                postKillExitTimeoutMilliseconds: 2_000);

            result.ExitCode.ShouldBe(0);
            string[] ownedProcessIds = Directory.GetFiles(directory);
            ownedProcessIds.Length.ShouldBe(15);
            stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(6));
            if (OperatingSystem.IsLinux())
            {
                foreach (string file in ownedProcessIds)
                {
                    int processId = int.Parse(Path.GetFileName(file), System.Globalization.CultureInfo.InvariantCulture);
                    Should.Throw<ArgumentException>(() => Process.GetProcessById(processId));
                }
            }
        }
        finally
        {
            foreach (string file in Directory.GetFiles(directory))
            {
                StopFixtureProcess(int.Parse(Path.GetFileName(file), System.Globalization.CultureInfo.InvariantCulture));
            }

            Directory.Delete(directory, recursive: true);
        }
    }

    private static void StopFixtureProcess(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(2_000).ShouldBeTrue();
            }
        }
        catch (ArgumentException)
        {
            // The production helper already reaped this owned child.
        }
    }
}
