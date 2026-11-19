using System.Security.Cryptography;
using System.Text.Json;

using Shouldly;

namespace Hexalith.Parties.Platform.IntegrationTests;

/// <summary>Optional persist-then-hold for current-owner CI; ordinary native execution leaves this path inert.</summary>
internal static class CiLifecycleHold
{
    /// <summary>The exact native handoff flag forwarded only when the owner process requested a hold.</summary>
    internal const string VariableName = CiLifecycleHoldSignal.VariableName;

    /// <summary>True when the owner process forwarded the hold flag, or when the controller already published wait control for this run.</summary>
    internal static bool IsRequested => CiLifecycleHoldSignal.IsRequested;

    /// <summary>No-ops unless the owner requested a hold; then it reuses the controller control/marker contract.</summary>
    /// <param name="fixture">The real runner-owned dependency fixture.</param>
    /// <param name="output">The native test output.</param>
    /// <param name="cancellationToken">The caller's remaining qualification lifetime.</param>
    /// <returns>A completed task when hold is inactive, otherwise the controlled terminal outcome.</returns>
    internal static Task AfterPersistenceAsync(PlatformEnvironmentFixture fixture, ITestOutputHelper output, CancellationToken cancellationToken)
        => IsRequested ? HoldAsync(fixture, output, cancellationToken) : Task.CompletedTask;

    /// <summary>Publishes the execution marker and waits for the controller without starting services.</summary>
    /// <param name="runId">The issued owner run identity.</param>
    /// <param name="directory">The controller-owned marker directory.</param>
    /// <param name="descriptorPath">The real runner descriptor used for the marker digest.</param>
    /// <param name="onPass">The persistence recheck invoked on an explicit pass.</param>
    /// <param name="cancellationToken">The bounded execution token.</param>
    /// <returns>A task completing after the controlled terminal outcome.</returns>
    internal static async Task ObserveAsync(
        string runId,
        string directory,
        string descriptorPath,
        Func<CancellationToken, Task> onPass,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(runId);
        ArgumentException.ThrowIfNullOrEmpty(directory);
        ArgumentException.ThrowIfNullOrEmpty(descriptorPath);
        ArgumentNullException.ThrowIfNull(onPass);
        string control = Path.Combine(directory, "control.json");
        _ = await ReadActionAsync(control, runId, cancellationToken).ConfigureAwait(false);
        byte[] descriptorBytes = await File.ReadAllBytesAsync(descriptorPath, cancellationToken).ConfigureAwait(false);
        string ready = JsonSerializer.Serialize(new
        {
            runId,
            phase = "execution",
            observedAtUtc = DateTimeOffset.UtcNow,
            processId = Environment.ProcessId,
            descriptorSha256 = Convert.ToHexStringLower(SHA256.HashData(descriptorBytes)),
            realPersistenceAssertionsPassed = true,
        });
        await PublishAsync(directory, ready, cancellationToken).ConfigureAwait(false);
        while (true)
        {
            string action = await ReadActionAsync(control, runId, cancellationToken).ConfigureAwait(false);
            if (action == "pass")
            {
                cancellationToken.ThrowIfCancellationRequested();
                await onPass(cancellationToken).ConfigureAwait(false);
                return;
            }

            action.ShouldNotBe("fail", "Intentional qualification failing-suite outcome.");
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task HoldAsync(PlatformEnvironmentFixture fixture, ITestOutputHelper output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        ArgumentNullException.ThrowIfNull(output);
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bound.CancelAfter(TimeSpan.FromSeconds(240));
        string runId = Environment.GetEnvironmentVariable("HEXALITH_G4_RUN_ID") ?? string.Empty;
        (runId.Length == 32 && runId.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f')).ShouldBeTrue();
        string directory = Path.GetDirectoryName(ControlPath())
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "hexalith-builds", "ci-qualification", runId);
        string descriptor = Environment.GetEnvironmentVariable("HEXALITH_TEST_ENVIRONMENT_PATH")
            ?? throw new InvalidOperationException("The real runner descriptor is required.");
        await ObserveAsync(
            runId,
            directory,
            descriptor,
            token => new PartyPersistenceAssertions(fixture, output).RecheckAsync(token),
            bound.Token).ConfigureAwait(false);
    }

    internal static string? ControlPath() => CiLifecycleHoldSignal.ControlPath();

    private static async Task PublishAsync(string directory, string ready, CancellationToken token)
    {
        string temporary = Path.Combine(directory, ".execution-ready-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await File.WriteAllTextAsync(temporary, ready, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            File.Move(temporary, Path.Combine(directory, "execution-ready.json"), overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private static async Task<string> ReadActionAsync(string path, string runId, CancellationToken token)
    {
        FileInfo file = new(path);
        (file.Exists && file.LinkTarget is null && file.Length is > 0 and <= 4096).ShouldBeTrue("Missing or unsafe explicit qualification control.");
        using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(path, token).ConfigureAwait(true));
        document.RootElement.GetProperty("runId").GetString().ShouldBe(runId);
        string action = document.RootElement.GetProperty("action").GetString() ?? string.Empty;
        new[] { "wait", "pass", "fail" }.ShouldContain(action);
        return action;
    }
}
