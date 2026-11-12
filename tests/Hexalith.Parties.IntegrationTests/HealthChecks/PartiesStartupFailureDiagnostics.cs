using System.Text.Json;
using System.Text.RegularExpressions;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Parties.IntegrationTests.HealthChecks;

/// <summary>Derives bounded startup facts without rendering resource logs, credentials or environment values.</summary>
internal static class PartiesStartupFailureDiagnostics
{
    private static readonly Regex ExceptionType = new(
        @"(?<![A-Za-z0-9_.])(?:System|Microsoft|Aspire|Hexalith)\.[A-Za-z0-9_.+`]{1,200}Exception\b",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex FrameMethod = new(
        @"\bat (?<method>(?:System|Microsoft|Aspire|Hexalith)\.[A-Za-z0-9_.+`<>|]{1,250})\(",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    /// <summary>Reads actual failed project resources before the fixture tears down their owned lifetime.</summary>
    internal static async Task<string> DescribeAsync(DistributedApplication? application)
    {
        if (application is null) { return string.Empty; }
        var facts = new List<object>();
        try
        {
            var model = application.Services.GetRequiredService<DistributedApplicationModel>();
            var logger = application.Services.GetRequiredService<ResourceLoggerService>();
            foreach (ProjectResource project in model.Resources.OfType<ProjectResource>().OrderBy(item => item.Name, StringComparer.Ordinal).Take(32))
            {
                if (!application.ResourceNotifications.TryGetCurrentState(project.Name, out ResourceEvent? observation)
                    || (observation.Snapshot.State?.Text != KnownResourceStates.FailedToStart
                        && observation.Snapshot.State?.Text != KnownResourceStates.Exited)) { continue; }
                var lines = new List<string>();
                string? captureError = null;
                using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                try
                {
                    await foreach (IReadOnlyList<LogLine> batch in logger.GetAllAsync(project).WithCancellation(budget.Token).ConfigureAwait(false))
                    {
                        foreach (LogLine line in batch)
                        {
                            lines.Add(line.Content.Length <= 4096 ? line.Content : line.Content[..4096]);
                            if (lines.Count >= 512) { break; }
                        }
                        if (lines.Count >= 512) { break; }
                    }
                }
                catch (OperationCanceledException) when (budget.IsCancellationRequested) { captureError = "bounded-timeout"; }
                catch (Exception error) { captureError = error.GetType().FullName; }
                facts.Add(new { resource = project.Name, state = observation.Snapshot.State?.Text, exitCode = observation.Snapshot.ExitCode,
                    exceptionTypes = ExceptionTypes(lines), frameMethods = FrameMethods(lines), errorCodes = ErrorCodes(lines), capturedLines = lines.Count, captureError });
            }
            return "; startupFacts=" + JsonSerializer.Serialize(new { parentRuntime = Environment.Version.ToString(),
                jsonAssemblyVersion = typeof(JsonElement).Assembly.GetName().Version?.ToString(), resources = facts });
        }
        catch (Exception error) { return "; startupFactsCaptureError=" + error.GetType().FullName; }
    }

    /// <summary>Retains only qualified exception type identifiers, never the surrounding message or payload.</summary>
    internal static string[] ExceptionTypes(IEnumerable<string> lines) => lines
        .SelectMany(line => ExceptionType.Matches(line).Select(match => match.Value))
        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Take(16).ToArray();

    /// <summary>Retains source method identifiers without arguments, paths or captured payload values.</summary>
    internal static string[] FrameMethods(IEnumerable<string> lines) => lines
        .SelectMany(line => FrameMethod.Matches(line).Select(match => match.Groups["method"].Value))
        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Take(16).ToArray();

    /// <summary>Reports static code labels for known infrastructure and SDK refusals without copying their values.</summary>
    internal static string[] ErrorCodes(IEnumerable<string> lines)
    {
        var codes = new HashSet<string>(StringComparer.Ordinal);
        foreach (string line in lines)
        {
            if (line.Contains("operation", StringComparison.OrdinalIgnoreCase)
                && (line.Contains("differ", StringComparison.OrdinalIgnoreCase) || line.Contains("digest", StringComparison.OrdinalIgnoreCase)))
                codes.Add("operation-inventory-mismatch");
            if (line.Contains("Selected child dependency", StringComparison.Ordinal)) codes.Add("selected-child-dependency-unavailable");
            if (line.Contains("manifest definition does not match", StringComparison.OrdinalIgnoreCase)) codes.Add("assembly-binding-failure");
            if (line.Contains("Unable to resolve service for type", StringComparison.Ordinal)
                || line.Contains("No service for type", StringComparison.Ordinal)) codes.Add("dependency-injection-resolution-failure");
            if (line.Contains("No complete catalog generation is serving", StringComparison.Ordinal)) codes.Add("catalog-generation-unavailable");
            if (line.Contains("An independently bound local catalog host identity and profile are required", StringComparison.Ordinal))
                codes.Add("catalog-local-identity-unavailable");
        }
        return codes.Order(StringComparer.Ordinal).ToArray();
    }
}
