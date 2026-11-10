using System.Text.Json;

using Shouldly;

namespace Hexalith.Parties.Platform.LifecycleTests;

/// <summary>Local publication controls without a real-platform fixture or hosted qualification credit.</summary>
public sealed class QualificationExecutionMarkerTests
{
    /// <summary>A partial private write cannot replace absence or an already admitted complete marker.</summary>
    /// <param name="replace">Whether an earlier complete marker has already been published.</param>
    /// <returns>A task completing after the controlled partial write and atomic publication.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PartialTemporaryWriteCannotBecomeAnAdmittedMarkerAsync(bool replace)
    {
        string directory = Path.Combine(Path.GetTempPath(), "parties-marker-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string final = Path.Combine(directory, "execution-ready.json");
        const string previous = "{\"runId\":\"previous\"}";
        const string ready = "{\"runId\":\"current\",\"realPersistenceAssertionsPassed\":true}";
        var partial = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        bound.CancelAfter(TimeSpan.FromSeconds(10));
        Task? publishing = null;
        try
        {
            if (replace) { await File.WriteAllTextAsync(final, previous, bound.Token).ConfigureAwait(true); }
            publishing = QualificationExecutionMarker.PublishAsync(directory, ready, bound.Token, async (path, body, token) =>
            {
                await File.WriteAllTextAsync(path, "{\"runId\":", token).ConfigureAwait(false);
                partial.SetResult(path);
                await release.Task.WaitAsync(token).ConfigureAwait(false);
                await File.WriteAllTextAsync(path, body, token).ConfigureAwait(false);
            });
            string temporary = await partial.Task.WaitAsync(bound.Token).ConfigureAwait(true);
            Path.GetDirectoryName(temporary).ShouldBe(directory);
            Should.Throw<JsonException>(() => JsonDocument.Parse(File.ReadAllText(temporary)));
            if (replace) { (await File.ReadAllTextAsync(final, bound.Token).ConfigureAwait(true)).ShouldBe(previous); }
            else { File.Exists(final).ShouldBeFalse(); }
            release.SetResult();
            await publishing.WaitAsync(bound.Token).ConfigureAwait(true);
            (await File.ReadAllTextAsync(final, bound.Token).ConfigureAwait(true)).ShouldBe(ready);
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(final));
            document.RootElement.GetProperty("realPersistenceAssertionsPassed").GetBoolean().ShouldBeTrue();
            Directory.GetFiles(directory).ShouldBe(new[] { final });
        }
        finally
        {
            release.TrySetResult();
            if (publishing is not null) { await publishing.ConfigureAwait(true); }
            Directory.Delete(directory, recursive: true);
        }
    }
}
