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
    {
        PackageTestProcessResult result = Run(
            "dotnet",
            $"msbuild \"{Path.Combine(repositoryRoot, projectRelativePath)}\" -nologo "
                + $"-p:Configuration=Release -getProperty:{propertyName}",
            repositoryRoot);
        EnsureSuccess(result, $"resolve MSBuild property {propertyName}");

        string value = result.StandardOutput.Trim();
        if (string.IsNullOrWhiteSpace(value) || value.Contains("$(", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"MSBuild property {propertyName} did not resolve to a concrete value: {result.Diagnostic}");
        }

        return value;
    }

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
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

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

        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(processTimeoutMilliseconds))
        {
            TryKill(process);
            if (!process.WaitForExit(postKillExitTimeoutMilliseconds))
            {
                throw new TimeoutException(
                    $"{fileName} did not exit within {postKillExitTimeoutMilliseconds} ms after termination was requested.");
            }

            WaitForOutput(standardOutput, standardError, outputCaptureTimeoutMilliseconds, fileName);
            string diagnostic = string.IsNullOrWhiteSpace(standardError.Result)
                ? standardOutput.Result
                : standardError.Result;
            throw new TimeoutException(
                $"{fileName} exceeded the {processTimeoutMilliseconds} ms timeout.{Environment.NewLine}{diagnostic}");
        }

        WaitForOutput(standardOutput, standardError, outputCaptureTimeoutMilliseconds, fileName);
        return new PackageTestProcessResult(process.ExitCode, standardOutput.Result, standardError.Result);
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
