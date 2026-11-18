using System.Text.Json;

using Shouldly;

namespace Hexalith.Parties.Platform.IntegrationTests;

/// <summary>Local control/marker contracts without a real-platform fixture or hosted qualification credit.</summary>
public sealed class CiLifecycleHoldTests
{
    /// <summary>Ordinary native execution must not wait on a controller that is not present.</summary>
    /// <returns>A completed no-op when the hold flag is unset.</returns>
    [Fact]
    public async Task AfterPersistenceIsNoOpUnlessExplicitlyRequestedAsync()
    {
        if (CiLifecycleHold.IsRequested)
        {
            return;
        }

        await CiLifecycleHold.AfterPersistenceAsync(null!, null!, CancellationToken.None).ConfigureAwait(true);
    }

    /// <summary>Published Builds 6.3.0 does not forward the hold flag; the controller wait file is sufficient.</summary>
    [Fact]
    public void IsRequestedWhenControllerWaitControlExists()
    {
        string runId = Guid.NewGuid().ToString("N");
        string? previousRun = Environment.GetEnvironmentVariable("HEXALITH_G4_RUN_ID");
        string? previousHold = Environment.GetEnvironmentVariable(CiLifecycleHold.VariableName);
        string directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "hexalith-builds",
            "ci-qualification",
            runId);
        try
        {
            Environment.SetEnvironmentVariable(CiLifecycleHold.VariableName, null);
            Environment.SetEnvironmentVariable("HEXALITH_G4_RUN_ID", runId);
            CiLifecycleHold.IsRequested.ShouldBeFalse();
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "control.json"), JsonSerializer.Serialize(new { runId, action = "wait" }));
            CiLifecycleHold.IsRequested.ShouldBeTrue();
        }
        finally
        {
            Environment.SetEnvironmentVariable("HEXALITH_G4_RUN_ID", previousRun);
            Environment.SetEnvironmentVariable(CiLifecycleHold.VariableName, previousHold);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    /// <summary>The controller's wait/pass contract is observed from local files only.</summary>
    /// <returns>The published marker and pass recheck without starting services.</returns>
    [Fact]
    public async Task ObservePublishesMarkerThenCompletesOnPassAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), "parties-ci-hold-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string runId = Guid.NewGuid().ToString("N");
            string descriptor = Path.Combine(directory, "test-environment.json");
            await File.WriteAllTextAsync(descriptor, "{\"kind\":\"local-control\"}", TestContext.Current.CancellationToken).ConfigureAwait(true);
            await File.WriteAllTextAsync(Path.Combine(directory, "control.json"), JsonSerializer.Serialize(new { runId, action = "wait" }), TestContext.Current.CancellationToken).ConfigureAwait(true);
            using var bound = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            bound.CancelAfter(TimeSpan.FromSeconds(10));
            int rechecks = 0;
            Task observing = CiLifecycleHold.ObserveAsync(runId, directory, descriptor, _ =>
            {
                rechecks++;
                return Task.CompletedTask;
            }, bound.Token);
            string marker = Path.Combine(directory, "execution-ready.json");
            while (!File.Exists(marker))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(20), bound.Token).ConfigureAwait(true);
            }

            using (JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(marker, bound.Token).ConfigureAwait(true)))
            {
                document.RootElement.GetProperty("runId").GetString().ShouldBe(runId);
                document.RootElement.GetProperty("phase").GetString().ShouldBe("execution");
                document.RootElement.GetProperty("realPersistenceAssertionsPassed").GetBoolean().ShouldBeTrue();
            }

            await File.WriteAllTextAsync(Path.Combine(directory, "control.json"), JsonSerializer.Serialize(new { runId, action = "pass" }), bound.Token).ConfigureAwait(true);
            await observing.WaitAsync(bound.Token).ConfigureAwait(true);
            rechecks.ShouldBe(1);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
