// ATDD red-phase scaffolds for Story 11.4 — deployment validation must include
// distinct, secret-safe, CI-readable checks for Hexalith.Tenants integration.

namespace Hexalith.Parties.DeployValidation.Tests;

using System.Diagnostics;
using System.Text.Json;

/// <summary>
/// Story 11.4 — AC3: <c>deploy/validate-deployment.ps1</c> must detect missing or
/// misconfigured Hexalith.Tenants integration with stable, distinct categories so CI
/// logs and operators can react deterministically. Each test below is skipped until
/// the script learns the new check; assertions describe the exact contract.
/// </summary>
[Collection("DeployValidation")]
public sealed class TenantsDeploymentValidationTests : IDisposable
{
    private const string SkipReason =
        "TDD red phase — Story 11.4 must extend deploy/validate-deployment.ps1 with " +
        "Tenants subscription/configuration checks and surface them in JSON output.";

    private readonly string _scriptPath;
    private readonly string _tempDir;
    private bool _disposed;

    public TenantsDeploymentValidationTests()
    {
        string? solutionDir = FindSolutionDirectory();
        solutionDir.ShouldNotBeNull("Could not find solution directory");
        _scriptPath = Path.Combine(solutionDir, "deploy", "validate-deployment.ps1");
        File.Exists(_scriptPath).ShouldBeTrue($"Validation script not found at {_scriptPath}");

        _tempDir = Path.Combine(Path.GetTempPath(), $"deploy-validation-tenants-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    [Fact(Skip = SkipReason)]
    public async Task TenantsSubscription_Missing_FailsWithSpecificErrorAsync()
    {
        WriteValidProductionConfig(_tempDir);
        WritePartiesPubSub(_tempDir);
        // Intentionally omit the Tenants subscription file.

        (int exitCode, string output) = await RunValidationAsync(_tempDir);

        exitCode.ShouldBe(1);
        output.ShouldContain("system.tenants.events");
        output.ShouldContain("FAIL");
    }

    [Fact(Skip = SkipReason)]
    public async Task TenantsSubscriptionScopes_MissingCommandApi_FailsWithRecommendationAsync()
    {
        WriteValidProductionConfig(_tempDir);
        WritePartiesPubSub(_tempDir);
        WriteTenantsSubscription(_tempDir, includeCommandApiInScopes: false);

        (int exitCode, string output) = await RunValidationAsync(_tempDir);

        exitCode.ShouldBe(1);
        output.ShouldContain("commandapi");
        output.ShouldContain("subscriptionScopes");
    }

    [Fact(Skip = SkipReason)]
    public async Task TenantsConfiguration_Missing_FailsWithRecommendationAsync()
    {
        WriteValidProductionConfig(_tempDir);
        WritePartiesPubSub(_tempDir);
        WriteTenantsSubscription(_tempDir, includeCommandApiInScopes: true);
        // Tenants configuration block intentionally omitted from appsettings/yaml.

        (int exitCode, string output) = await RunValidationAsync(_tempDir);

        exitCode.ShouldBe(1);
        output.ShouldContain("Tenants");
        output.ShouldContain("FAIL");
    }

    [Fact(Skip = SkipReason)]
    public async Task TenantsConfiguration_Malformed_FailsWithDistinctCategoryAsync()
    {
        WriteValidProductionConfig(_tempDir);
        WritePartiesPubSub(_tempDir);
        WriteTenantsSubscription(_tempDir, includeCommandApiInScopes: true);
        WriteMalformedTenantsConfig(_tempDir);

        (int exitCode, string output) = await RunValidationAsync(_tempDir);

        exitCode.ShouldBe(1);
        // Distinct from "missing" so operators can tell them apart in CI logs.
        output.ShouldContain("Tenants configuration");
        output.ShouldNotContain("not found", Case.Insensitive);
    }

    [Fact(Skip = SkipReason)]
    public async Task TenantsValidation_JsonOutput_IncludesTenantsChecksInChecksArrayAsync()
    {
        WriteValidProductionConfig(_tempDir);
        WritePartiesPubSub(_tempDir);
        WriteTenantsSubscription(_tempDir, includeCommandApiInScopes: true);
        WriteValidTenantsConfig(_tempDir);

        (int exitCode, string output) = await RunValidationAsync(_tempDir, jsonOutput: true);

        exitCode.ShouldBe(0);
        using JsonDocument doc = JsonDocument.Parse(output);
        JsonElement checks = doc.RootElement.GetProperty("checks");
        checks.ValueKind.ShouldBe(JsonValueKind.Array);
        checks.EnumerateArray()
            .Any(c => (c.GetProperty("name").GetString() ?? string.Empty).Contains("Tenants", StringComparison.Ordinal))
            .ShouldBeTrue("Expected at least one Tenants-named entry in checks[]");
    }

    [Fact(Skip = SkipReason)]
    public async Task TenantsValidation_LocalDev_PassesWithWarningsAsync()
    {
        WriteValidProductionConfig(_tempDir);
        WritePartiesPubSub(_tempDir);
        WriteLocalDevTenantsSubscription(_tempDir);
        WriteValidTenantsConfig(_tempDir);

        (int exitCode, string output) = await RunValidationAsync(_tempDir);

        exitCode.ShouldBe(0);
        output.ShouldContain("WARN");
        output.ShouldContain("system.tenants.events");
    }

    [Fact(Skip = SkipReason)]
    public async Task TenantsValidation_Output_DoesNotLeakSecretsOrPiiAsync()
    {
        WriteValidProductionConfig(_tempDir);
        WritePartiesPubSub(_tempDir);
        WriteTenantsSubscriptionWithSensitiveValues(_tempDir);
        WriteTenantsConfigWithSensitiveValues(_tempDir);

        (int exitCode, string output) = await RunValidationAsync(_tempDir);

        // Whatever the exit code, output must never echo tokens, claims, membership, or PII.
        output.ShouldNotContain("Bearer ", Case.Insensitive);
        output.ShouldNotContain("eventstore:tenant=");
        output.ShouldNotContain("user-1@example.com");
        output.ShouldNotContain("ConnectionString=");
    }

    private async Task<(int exitCode, string output)> RunValidationAsync(string configDir, bool jsonOutput = false)
    {
        ProcessStartInfo psi = new()
        {
            FileName = ResolvePowerShellExecutable(),
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{_scriptPath}\" " +
                        $"-ConfigDir \"{configDir}\"" + (jsonOutput ? " -Json" : string.Empty),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using Process? process = Process.Start(psi);
        process.ShouldNotBeNull("Unable to start PowerShell to run validation script");
        string stdout = await process.StandardOutput.ReadToEndAsync();
        string stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, stdout + stderr);
    }

    private static string ResolvePowerShellExecutable()
        => OperatingSystem.IsWindows() ? "pwsh" : "pwsh";

    private static string? FindSolutionDirectory()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Hexalith.Parties.slnx")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName;
    }

    // The fixture writers below are intentionally not implemented. Story 11.4 must
    // ship them alongside the new validation logic so the tests share a single
    // scaffolding pattern with the existing DeploymentValidationTests fixtures.
    private static void WriteValidProductionConfig(string dir)
        => throw new NotImplementedException("Reuse DeploymentValidationTests' WriteValidProductionConfig.");

    private static void WritePartiesPubSub(string dir)
        => throw new NotImplementedException("Story 11.4: emit a parties-events pubsub fixture.");

    private static void WriteTenantsSubscription(string dir, bool includeCommandApiInScopes)
        => throw new NotImplementedException("Story 11.4: emit a system.tenants.events subscription fixture.");

    private static void WriteLocalDevTenantsSubscription(string dir)
        => throw new NotImplementedException("Story 11.4: emit a local-dev variant warning on missing scopes.");

    private static void WriteValidTenantsConfig(string dir)
        => throw new NotImplementedException("Story 11.4: emit a valid Tenants config block (PubSubName, TopicName, CommandApiAppId).");

    private static void WriteMalformedTenantsConfig(string dir)
        => throw new NotImplementedException("Story 11.4: emit a malformed Tenants config block (empty/invalid values).");

    private static void WriteTenantsSubscriptionWithSensitiveValues(string dir)
        => throw new NotImplementedException("Story 11.4: emit a Tenants subscription containing token-shaped values to verify redaction.");

    private static void WriteTenantsConfigWithSensitiveValues(string dir)
        => throw new NotImplementedException("Story 11.4: emit a Tenants config block containing PII-shaped values to verify redaction.");

    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing && Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, recursive: true); }
            catch { /* best-effort cleanup */ }
        }
        _disposed = true;
    }
}
