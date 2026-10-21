using System.Diagnostics;

namespace Hexalith.Parties.PackageTests;

internal static class PackageTestProcess
{
    private const int DefaultOutputCaptureTimeoutMilliseconds = 5_000;
    private const int DefaultProcessTimeoutMilliseconds = 120_000;

    internal static string ResolveMsbuildProperty(
        string repositoryRoot,
        string projectRelativePath,
        string propertyName)
        => EffectivePackageVersions.ReadProperty(repositoryRoot, propertyName, projectRelativePath);

    internal static PackageTestProcessResult RunDotnet(string arguments, string workingDirectory)
    {
        PackageTestProcessResult result = Run("dotnet", arguments, workingDirectory);
        EnsureSuccess(result, $"dotnet {arguments}");
        return result;
    }

    private static void EnsureSuccess(PackageTestProcessResult result, string operation)
    {
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"{operation} failed with exit code {result.ExitCode}.{Environment.NewLine}{result.Diagnostic}");
        }
    }

    internal static PackageTestProcessResult Run(
        string fileName,
        string arguments,
        string workingDirectory,
        int processTimeoutMilliseconds = DefaultProcessTimeoutMilliseconds,
        int postKillExitTimeoutMilliseconds = DefaultOutputCaptureTimeoutMilliseconds,
        int outputCaptureTimeoutMilliseconds = DefaultOutputCaptureTimeoutMilliseconds)
        => Run(
            new ProcessStartInfo(fileName) { Arguments = arguments },
            workingDirectory,
            processTimeoutMilliseconds,
            postKillExitTimeoutMilliseconds,
            outputCaptureTimeoutMilliseconds);

    /// <summary>Runs a child process with literal arguments and finite execution and cleanup bounds.</summary>
    internal static PackageTestProcessResult Run(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        int processTimeoutMilliseconds = DefaultProcessTimeoutMilliseconds,
        int postKillExitTimeoutMilliseconds = DefaultOutputCaptureTimeoutMilliseconds,
        int outputCaptureTimeoutMilliseconds = DefaultOutputCaptureTimeoutMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var startInfo = new ProcessStartInfo(fileName);
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return Run(startInfo, workingDirectory, processTimeoutMilliseconds,
            postKillExitTimeoutMilliseconds, outputCaptureTimeoutMilliseconds);
    }

    /// <summary>Captures both streams under one deadline and reaps a stalled process tree.</summary>
    private static PackageTestProcessResult Run(
        ProcessStartInfo startInfo,
        string workingDirectory,
        int processTimeoutMilliseconds,
        int postKillExitTimeoutMilliseconds,
        int outputCaptureTimeoutMilliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(processTimeoutMilliseconds);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(postKillExitTimeoutMilliseconds);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(outputCaptureTimeoutMilliseconds);

        startInfo.WorkingDirectory = workingDirectory;
        startInfo.RedirectStandardError = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        string fileName = startInfo.FileName;
        using Process process = new() { StartInfo = startInfo };

        bool started;
        try
        {
            started = process.Start();
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            throw new InvalidOperationException($"Could not start {fileName}: {exception.Message}", exception);
        }

        if (!started)
        {
            throw new InvalidOperationException($"Could not start {fileName}.");
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        bool exited;
        try
        {
            // Both streams drain concurrently while exit and capture share one deadline.
            // Once the direct child exits, preserve the shorter output-capture allowance.
            exited = process.WaitForExit(processTimeoutMilliseconds);
        }
        catch
        {
            KillAndWaitForExit(process, postKillExitTimeoutMilliseconds);
            throw;
        }

        if (!exited)
        {
            KillAndWaitForExit(process, postKillExitTimeoutMilliseconds);
            WaitForOutput(standardOutput, standardError, outputCaptureTimeoutMilliseconds, fileName);
            string diagnostic = string.IsNullOrWhiteSpace(standardError.Result)
                ? standardOutput.Result
                : standardError.Result;
            string arguments = startInfo.ArgumentList.Count == 0
                ? startInfo.Arguments
                : string.Join(' ', startInfo.ArgumentList);
            throw new TimeoutException(
                $"{fileName} exceeded the {processTimeoutMilliseconds} ms timeout.{Environment.NewLine}"
                    + $"Arguments: {arguments}{Environment.NewLine}{diagnostic}");
        }

        int remainingMilliseconds = (int)Math.Min(
            outputCaptureTimeoutMilliseconds,
            Math.Max(0, processTimeoutMilliseconds - stopwatch.ElapsedMilliseconds));
        WaitForOutput(standardOutput, standardError, remainingMilliseconds, fileName);
        return new PackageTestProcessResult(process.ExitCode, standardOutput.Result, standardError.Result);
    }

    /// <summary>Requests termination and waits only for the configured cleanup interval.</summary>
    private static void KillAndWaitForExit(Process process, int timeoutMilliseconds)
    {
        TryKill(process);
        if (!process.WaitForExit(timeoutMilliseconds))
        {
            throw new TimeoutException(
                $"{process.StartInfo.FileName} did not exit within {timeoutMilliseconds} ms after termination was requested.");
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // The process exited between the timeout and the termination request.
        }
        catch (System.ComponentModel.Win32Exception) when (process.HasExited)
        {
            // The process exited while the operating system was handling the termination request.
        }
    }

    private static void WaitForOutput(
        Task<string> standardOutput,
        Task<string> standardError,
        int timeoutMilliseconds,
        string fileName)
    {
        if (!Task.WaitAll([standardOutput, standardError], timeoutMilliseconds))
        {
            throw new TimeoutException(
                $"{fileName} output capture did not complete within {timeoutMilliseconds} ms.");
        }
    }
}
