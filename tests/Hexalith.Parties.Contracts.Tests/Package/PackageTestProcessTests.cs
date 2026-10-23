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

    /// <summary>Cooperative interruption cannot abandon inherited pipes or adopted descendants.</summary>
    /// <param name="signal">The cooperative Unix signal.</param>
    /// <param name="duringCleanup">Whether interruption is delivered after cleanup begins.</param>
    [Theory]
    [InlineData("TERM", false)]
    [InlineData("INT", false)]
    [InlineData("TERM", true)]
    [InlineData("INT", true)]
    public async Task SupervisorSignals_ReapOwnedFamilyAndLeaveUnrelatedWorker(string signal, bool duringCleanup)
    {
        if (!OperatingSystem.IsLinux()) { Assert.Skip("Linux exclusive subreaper controls."); }
        string directory = Path.Combine(Path.GetTempPath(), $"parties-signal-control-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        const string script = "import os,pathlib,subprocess,sys,threading; child=subprocess.Popen([sys.executable,'-c','import threading; threading.Event().wait()'],start_new_session=True); pathlib.Path(sys.argv[1]).write_text(str(os.getpid())+' '+str(child.pid)); threading.Event().wait()";
        var command = new ProcessStartInfo("python3") { WorkingDirectory = Directory.GetCurrentDirectory(), ArgumentList = { "-c", script, Path.Combine(directory, "ready") } };
        using var supervisor = new PackageTestProcessSupervisor(command, 2_000);
        if (duringCleanup)
        {
            // Test-only instrumentation delivers the signal synchronously after
            // the real cleanup killed its first owned group, before reaping.
            // The shipped supervisor has no handshake or cleanup wait state.
            const string injection = "os.killpg(child.pid, signal.SIGKILL)";
            supervisor.StartInfo.ArgumentList[1].ShouldContain(injection);
            supervisor.StartInfo.ArgumentList[1] = supervisor.StartInfo.ArgumentList[1].Replace(injection,
                injection + "\n        os.kill(os.getpid(), signal.SIG" + signal + ")\n        (directory / 'cleanup-signal-delivered').touch()", StringComparison.Ordinal);
        }
        using Process process = Process.Start(supervisor.StartInfo).ShouldNotBeNull();
        Task<string> output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        Task<string> error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        using Process unrelated = Process.Start(new ProcessStartInfo("sleep") { ArgumentList = { "3600" }, UseShellExecute = false }).ShouldNotBeNull();
        string stateDirectory = supervisor.StartInfo.ArgumentList[2];
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        Process? signaler = null;
        await WithFixtureCleanupAsync(async () =>
        {
            while (!File.Exists(Path.Combine(directory, "ready"))) { await Task.Delay(5, deadline.Token).ConfigureAwait(true); }
            int[] ids = File.ReadAllText(Path.Combine(directory, "ready")).Split(' ').Select(value => int.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            if (duringCleanup) { supervisor.RequestTermination(); }
            else
            {
                var send = new ProcessStartInfo("python3") { UseShellExecute = false };
                foreach (string argument in new[] { "-c", "import os,signal,sys; os.kill(int(sys.argv[1]),getattr(signal,'SIG'+sys.argv[2]))", process.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), signal }) { send.ArgumentList.Add(argument); }
                signaler = Process.Start(send).ShouldNotBeNull();
                await signaler.WaitForExitAsync(deadline.Token).ConfigureAwait(true);
                signaler.ExitCode.ShouldBe(0);
            }
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(true);
            await Task.WhenAll(output, error).WaitAsync(deadline.Token).ConfigureAwait(true);
            supervisor.EnsureCleanupSucceeded("python3");
            if (duringCleanup) { File.Exists(Path.Combine(stateDirectory, "cleanup-signal-delivered")).ShouldBeTrue(); }
            process.ExitCode.ShouldBe(signal == "TERM" ? 143 : 130);
            foreach (int id in ids) { Should.Throw<ArgumentException>(() => Process.GetProcessById(id)); }
            unrelated.HasExited.ShouldBeFalse();
        },
        async () =>
        {
            if (signaler is not null)
            {
                using (signaler) { await ReapFixtureProcessAsync(signaler).ConfigureAwait(true); }
            }
        },
        () => ReapFixtureProcessAsync(process, supervisor.RequestTermination),
        () => ObserveFixtureOutputAsync(output, error),
        () => ReapFixtureProcessAsync(unrelated),
        () => { Directory.Delete(directory, recursive: true); return Task.CompletedTask; }).ConfigureAwait(true);
    }

    /// <summary>Missing bookkeeping storage cannot bypass cleanup, including when recording a launch-loop failure.</summary>
    [Fact]
    public async Task SupervisorMissingStateDirectory_StillReapsOwnedFamily()
    {
        if (!OperatingSystem.IsLinux()) { Assert.Skip("Linux exclusive subreaper control."); }
        string directory = Path.Combine(Path.GetTempPath(), $"parties-missing-state-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        const string script = "import os,pathlib,subprocess,sys,threading; child=subprocess.Popen(['sleep','3600'],start_new_session=True); pathlib.Path(sys.argv[1]).write_text(str(os.getpid())+' '+str(child.pid)); threading.Event().wait()";
        var command = new ProcessStartInfo("python3") { WorkingDirectory = Directory.GetCurrentDirectory(), ArgumentList = { "-c", script, Path.Combine(directory, "ready") } };
        using var supervisor = new PackageTestProcessSupervisor(command, 2_000);
        using Process process = Process.Start(supervisor.StartInfo).ShouldNotBeNull();
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        await WithFixtureCleanupAsync(async () =>
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (!File.Exists(Path.Combine(directory, "ready"))) { await Task.Delay(5, deadline.Token).ConfigureAwait(true); }
            int[] ids = File.ReadAllText(Path.Combine(directory, "ready")).Split(' ').Select(value => int.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            Directory.Delete(supervisor.StartInfo.ArgumentList[2], recursive: true);
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(true);
            await Task.WhenAll(output, error).WaitAsync(deadline.Token).ConfigureAwait(true);
            process.ExitCode.ShouldBe(125);
            (await error.ConfigureAwait(true)).ShouldBeEmpty("A failed diagnostic write must not escape cleanup.");
            foreach (int id in ids) { Should.Throw<ArgumentException>(() => Process.GetProcessById(id)); }
        },
        () => ReapFixtureProcessAsync(process),
        () => ObserveFixtureOutputAsync(output, error),
        () => { Directory.Delete(directory, recursive: true); return Task.CompletedTask; }).ConfigureAwait(true);
    }

    /// <summary>The monitor survives loss of an isolated caller without touching another worker.</summary>
    [Fact]
    public async Task SupervisorCallerLoss_UsesExplicitStartIdentityAndReapsInheritedPipes()
    {
        if (!OperatingSystem.IsLinux()) { Assert.Skip("Linux caller identity and exclusive subreaper control."); }
        string directory = Path.Combine(Path.GetTempPath(), $"parties-caller-control-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        const string work = "import os,pathlib,subprocess,sys,threading; child=subprocess.Popen([sys.executable,'-c','import threading; threading.Event().wait()'],start_new_session=True); pathlib.Path(sys.argv[1]).write_text(str(os.getpid())+' '+str(child.pid)); threading.Event().wait()";
        var command = new ProcessStartInfo("python3") { WorkingDirectory = Directory.GetCurrentDirectory(), ArgumentList = { "-c", work, Path.Combine(directory, "ready") } };
        using var supervisor = new PackageTestProcessSupervisor(command, 2_000);
        const string caller = "import json,os,pathlib,subprocess,sys,threading; args=json.loads(sys.argv[1]); stat=pathlib.Path('/proc/self/stat').read_text(); args[4]=str(os.getpid()); args[5]=stat[stat.rfind(')')+2:].split()[19]; child=subprocess.Popen(['python3']+args); pathlib.Path(sys.argv[2]).write_text(str(os.getpid())+' '+str(child.pid)); threading.Event().wait()";
        const string controller = """
            import ctypes, os, subprocess, sys, threading
            ctypes.CDLL(None).prctl(36, 1, 0, 0, 0)
            child_ready = threading.Event()
            def stop_caller():
                sys.stdin.readline()
                child_ready.wait()
                if child.poll() is None:
                    try:
                        child.kill()
                    except ProcessLookupError:
                        pass
            threading.Thread(target=stop_caller, daemon=True).start()
            child = subprocess.Popen([sys.executable, '-c', sys.argv[1], sys.argv[2], sys.argv[3]], stdin=subprocess.DEVNULL)
            child_ready.set()
            child.wait()
            while True:
                try:
                    os.waitpid(-1, 0)
                except ChildProcessError:
                    break
            """;
        var start = new ProcessStartInfo("python3") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (string argument in new[] { "-c", controller, caller, JsonSerializer.Serialize(supervisor.StartInfo.ArgumentList), Path.Combine(directory, "caller") }) { start.ArgumentList.Add(argument); }
        using Process process = Process.Start(start).ShouldNotBeNull();
        Task<string> output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        Task<string> error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        using Process unrelated = Process.Start(new ProcessStartInfo("sleep") { ArgumentList = { "3600" }, UseShellExecute = false }).ShouldNotBeNull();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        await WithFixtureCleanupAsync(async () =>
        {
            while (!File.Exists(Path.Combine(directory, "ready"))) { await Task.Delay(5, deadline.Token).ConfigureAwait(true); }
            int[] family = File.ReadAllText(Path.Combine(directory, "ready")).Split(' ').Select(value => int.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            int[] callers = File.ReadAllText(Path.Combine(directory, "caller")).Split(' ').Select(value => int.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            using Process callerProcess = Process.GetProcessById(callers[0]);
            callerProcess.Kill();
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(true);
            await Task.WhenAll(output, error).WaitAsync(deadline.Token).ConfigureAwait(true);
            process.ExitCode.ShouldBe(0);
            supervisor.EnsureCleanupSucceeded("python3");
            foreach (int id in family.Concat(callers)) { Should.Throw<ArgumentException>(() => Process.GetProcessById(id)); }
            unrelated.HasExited.ShouldBeFalse();
        },
        () => ReapFixtureProcessAsync(process, () => process.StandardInput.Close()),
        () => ObserveFixtureOutputAsync(output, error),
        () => ReapFixtureProcessAsync(unrelated),
        () => { Directory.Delete(directory, recursive: true); return Task.CompletedTask; }).ConfigureAwait(true);
    }

    /// <summary>An expired observation and a cleanup failure cannot abandon later owned resources.</summary>
    [Fact]
    public async Task SupervisorFixtureCleanup_AfterExpiredObservationPreservesFailureAndReapsEveryOwnedProcess()
    {
        if (!OperatingSystem.IsLinux()) { Assert.Skip("Linux exclusive subreaper control."); }
        string directory = Path.Combine(Path.GetTempPath(), $"parties-expired-control-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string ready = Path.Combine(directory, "ready");
        var command = new ProcessStartInfo("python3")
        {
            WorkingDirectory = Directory.GetCurrentDirectory(),
            ArgumentList = { "-c", "import os,pathlib,sys,threading; pathlib.Path(sys.argv[1]).write_text(str(os.getpid())); threading.Event().wait()", ready },
        };
        using var supervisor = new PackageTestProcessSupervisor(command, 2_000);
        using Process process = Process.Start(supervisor.StartInfo).ShouldNotBeNull();
        Task<string> output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        Task<string> error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        using Process unrelated = Process.Start(new ProcessStartInfo("sleep") { ArgumentList = { "3600" }, UseShellExecute = false }).ShouldNotBeNull();
        using var observation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        InvalidOperationException? original = null;
        int child = 0;
        var cleanupFailure = new IOException("controlled cleanup failure");
        InvalidOperationException failure = await Should.ThrowAsync<InvalidOperationException>(() => WithFixtureCleanupAsync(async () =>
        {
            while (!File.Exists(ready)) { await Task.Delay(5, observation.Token).ConfigureAwait(true); }
            child = int.Parse(File.ReadAllText(ready), System.Globalization.CultureInfo.InvariantCulture);
            observation.Cancel();
            try { await process.WaitForExitAsync(observation.Token).ConfigureAwait(true); }
            catch (OperationCanceledException exception)
            {
                original = new InvalidOperationException("The controlled observation deadline expired.", exception);
                throw original;
            }
        },
        () => ReapFixtureProcessAsync(process, supervisor.RequestTermination),
        () => Task.FromException(cleanupFailure),
        () => ObserveFixtureOutputAsync(output, error),
        () => ReapFixtureProcessAsync(unrelated),
        () => { Directory.Delete(directory, recursive: true); return Task.CompletedTask; })).ConfigureAwait(true);
        failure.ShouldBeSameAs(original);
        failure.InnerException.ShouldBeAssignableTo<OperationCanceledException>();
        ((OperationCanceledException)failure.InnerException!).CancellationToken.ShouldBe(observation.Token);
        failure.Data["FixtureCleanupFailures"].ShouldBeOfType<AggregateException>().InnerExceptions.ShouldBe([cleanupFailure]);
        process.HasExited.ShouldBeTrue();
        unrelated.HasExited.ShouldBeTrue();
        child.ShouldBeGreaterThan(0);
        Should.Throw<ArgumentException>(() => Process.GetProcessById(child));
        Directory.Exists(directory).ShouldBeFalse();
        supervisor.EnsureCleanupSucceeded("python3");
    }

    private static async Task WithFixtureCleanupAsync(Func<Task> body, params Func<Task>[] cleanupSteps)
    {
        Exception? original = null;
        try { await body().ConfigureAwait(false); }
        catch (Exception exception) { original = exception; throw; }
        finally
        {
            var failures = new List<Exception>();
            foreach (Func<Task> step in cleanupSteps)
            {
                try { await step().ConfigureAwait(false); }
                catch (Exception exception) { failures.Add(exception); }
            }
            if (failures.Count > 0)
            {
                var aggregate = new AggregateException("Fixture cleanup failed after independently attempting every owned resource.", failures);
                if (original is null) { throw aggregate; }
                original.Data["FixtureCleanupFailures"] = aggregate;
            }
        }
    }

    private static async Task ReapFixtureProcessAsync(Process process, Action? requestTermination = null)
    {
        if (process.HasExited) { return; }
        Exception? cooperativeFailure = null;
        if (requestTermination is not null)
        {
            using var graceful = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            try
            {
                requestTermination();
                await process.WaitForExitAsync(graceful.Token).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException) when (graceful.IsCancellationRequested) { }
            catch (Exception exception) { cooperativeFailure = exception; }
        }
        try { if (!process.HasExited) { process.Kill(entireProcessTree: true); } }
        catch (InvalidOperationException) when (process.HasExited) { }
        using var forced = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await process.WaitForExitAsync(forced.Token).ConfigureAwait(false);
        if (cooperativeFailure is not null)
        {
            throw new InvalidOperationException("Cooperative fixture cleanup failed; the owned process was forcibly reaped.", cooperativeFailure);
        }
    }

    private static async Task ObserveFixtureOutputAsync(Task<string> output, Task<string> error)
    {
        using var capture = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await Task.WhenAll(output, error).WaitAsync(capture.Token).ConfigureAwait(false);
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
