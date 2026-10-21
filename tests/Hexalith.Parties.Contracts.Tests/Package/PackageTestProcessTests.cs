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
    [InlineData(false)]
    [InlineData(true)]
    public void RunReapsTimedOutProcessWithOpenOrClosedStreams(bool closeStreams)
    {
        string script = "import os, sys, threading; print('pid=' + str(os.getpid()), file=sys.stderr, flush=True); "
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

    [Fact]
    public void RunKeepsPostExitCaptureBoundForInheritedPipes()
    {
        string pidFile = Path.GetTempFileName();
        const string script = "import pathlib, subprocess, sys; "
            + "child = subprocess.Popen([sys.executable, '-c', 'import threading; threading.Event().wait()']); "
            + "pathlib.Path(sys.argv[1]).write_text(str(child.pid))";
        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            TimeoutException exception = Should.Throw<TimeoutException>(() =>
                PackageTestProcess.Run("python3", ["-c", script, pidFile], Directory.GetCurrentDirectory(),
                    processTimeoutMilliseconds: 10_000,
                    postKillExitTimeoutMilliseconds: 500,
                    outputCaptureTimeoutMilliseconds: 100));

            exception.Message.ShouldContain("output capture did not complete within 100 ms");
            stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
        }
        finally
        {
            // The parent has already exited: the fixture owns cleanup of its orphaned child.
            if (int.TryParse(File.ReadAllText(pidFile), out int processId))
            {
                try
                {
                    using Process child = Process.GetProcessById(processId);
                    if (!child.HasExited)
                    {
                        child.Kill(entireProcessTree: true);
                        child.WaitForExit(2_000).ShouldBeTrue();
                    }
                }
                catch (ArgumentException)
                {
                    // The child was already reaped.
                }
            }

            File.Delete(pidFile);
        }
    }

}
