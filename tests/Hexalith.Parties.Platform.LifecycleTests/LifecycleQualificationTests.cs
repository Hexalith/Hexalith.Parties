using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.Parties.Platform.IntegrationTests;

namespace Hexalith.Parties.Platform.LifecycleTests;

/// <summary>Explicit real-service qualification fixture; no absent control or environment can skip it.</summary>
public sealed class LifecycleQualificationTests(PlatformEnvironmentFixture fixture, ITestOutputHelper output) : IClassFixture<PlatformEnvironmentFixture>
{
    /// <summary>Proves the real persistence path before exposing a bounded controller-owned execution phase.</summary>
    /// <returns>The real-service assertion and finite controlled terminal outcome.</returns>
    [Fact]
    public async Task PersistRealPartyThenObserveExplicitQualificationControl()
    {
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        bound.CancelAfter(TimeSpan.FromSeconds(240));
        string runId = Environment.GetEnvironmentVariable("HEXALITH_G4_RUN_ID") ?? string.Empty;
        Assert.True(runId.Length == 32 && runId.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f'));
        string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "hexalith-builds", "ci-qualification", runId);
        string control = Path.Combine(directory, "control.json");
        _ = await ReadActionAsync(control, runId, bound.Token).ConfigureAwait(true);

        await new PartyEnvironmentTests(fixture, output).RealDependencies_CreateQueryAndPersistMatchingPartyEvents().ConfigureAwait(true);

        string descriptor = Environment.GetEnvironmentVariable("HEXALITH_TEST_ENVIRONMENT_PATH") ?? throw new InvalidOperationException("The real runner descriptor is required.");
        byte[] descriptorBytes = await File.ReadAllBytesAsync(descriptor, bound.Token).ConfigureAwait(true);
        string ready = JsonSerializer.Serialize(new
        {
            runId,
            phase = "execution",
            observedAtUtc = DateTimeOffset.UtcNow,
            processId = Environment.ProcessId,
            descriptorSha256 = Convert.ToHexStringLower(SHA256.HashData(descriptorBytes)),
            realPersistenceAssertionsPassed = true,
        });
        await File.WriteAllTextAsync(Path.Combine(directory, "execution-ready.json"), ready, bound.Token).ConfigureAwait(true);
        while (true)
        {
            string action = await ReadActionAsync(control, runId, bound.Token).ConfigureAwait(true);
            if (action == "pass")
            {
                bound.Token.ThrowIfCancellationRequested();
                await new PartyEnvironmentTests(fixture, output).RealDependencies_CreateQueryAndPersistMatchingPartyEvents().ConfigureAwait(true);
                return;
            }

            Assert.True(action != "fail", "Intentional qualification failing-suite outcome.");
            await Task.Delay(TimeSpan.FromMilliseconds(100), bound.Token).ConfigureAwait(true);
        }
    }

    private static async Task<string> ReadActionAsync(string path, string runId, CancellationToken token)
    {
        FileInfo file = new(path);
        Assert.True(file.Exists && file.LinkTarget is null && file.Length is > 0 and <= 4096, "Missing or unsafe explicit qualification control.");
        using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(path, token).ConfigureAwait(true));
        Assert.Equal(runId, document.RootElement.GetProperty("runId").GetString());
        string action = document.RootElement.GetProperty("action").GetString() ?? string.Empty;
        Assert.Contains(action, new[] { "wait", "pass", "fail" });
        return action;
    }
}
