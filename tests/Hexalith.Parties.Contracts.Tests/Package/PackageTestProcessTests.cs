using System.Diagnostics;

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
}
