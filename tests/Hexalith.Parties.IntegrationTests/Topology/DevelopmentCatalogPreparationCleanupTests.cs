extern alias apphost;

using System.Reflection;

using Shouldly;

using CatalogTool = apphost::Hexalith.Parties.AppHost.PartiesDevelopmentCatalogTool;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Exercises the actual preparation ownership boundary on success, failure and foreign directories.</summary>
public sealed class DevelopmentCatalogPreparationCleanupTests
{
    /// <summary>Competing preparations never replace the successful run's marker or delete its files.</summary>
    [Fact]
    public async Task ConcurrentPreparationsPreserveTheWinningOwner()
    {
        string path = Path.Combine(Path.GetTempPath(), "parties-catalog-cleanup-" + Guid.NewGuid().ToString("N"));
        string? winner = null;
        using var barrier = new Barrier(8);
        try
        {
            Task[] attempts = Enumerable.Range(0, 8).Select(index => Task.Run(async () =>
            {
                barrier.SignalAndWait(TestContext.Current.CancellationToken);
                string owner = "run-" + index;
                try
                {
                    await CatalogTool.RunOwnedPreparationAsync(path, owner, () =>
                    {
                        Interlocked.CompareExchange(ref winner, owner, null).ShouldBeNull();
                        File.WriteAllText(Path.Combine(path, "retained"), owner);
                        return Task.CompletedTask;
                    }).ConfigureAwait(false);
                }
                catch (Exception failure) when (failure is IOException or InvalidDataException) { }
            }, TestContext.Current.CancellationToken)).ToArray();
            await Task.WhenAll(attempts).ConfigureAwait(true);
            winner.ShouldNotBeNull();
            File.ReadAllText(Path.Combine(path, ".owner")).ShouldBe(winner);
            File.ReadAllText(Path.Combine(path, "retained")).ShouldBe(winner);
        }
        finally
        {
            if (winner is not null) { CatalogTool.DeleteOwnedDirectory(path, winner); }
        }
    }

    /// <summary>Actual failed tool output remains actionable after its private working directory is removed.</summary>
    [Fact]
    public async Task FailedToolRetainsBoundedDiagnosticsAfterOwnedCleanup()
    {
        string path = Path.Combine(Path.GetTempPath(), "parties-catalog-cleanup-" + Guid.NewGuid().ToString("N"));
        MethodInfo run = typeof(CatalogTool).GetMethod("RunAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        InvalidDataException failure = await Should.ThrowAsync<InvalidDataException>(() => CatalogTool.RunOwnedPreparationAsync(path, "this-run", () =>
            (Task)run.Invoke(null, [path, "tool.log", new[] { "parties-catalog-no-such-command" }, TestContext.Current.CancellationToken])!)).ConfigureAwait(true);
        Directory.Exists(path).ShouldBeFalse();
        failure.Message.ShouldContain("parties-catalog-no-such-command");
        failure.Message.Length.ShouldBeLessThan(4200);
    }

    /// <summary>Cancellation remains cancellation when process exit races with termination.</summary>
    [Fact]
    public async Task CanceledToolConfirmsTerminationAndPreservesCancellation()
    {
        string path = Path.Combine(Path.GetTempPath(), "parties-catalog-cleanup-" + Guid.NewGuid().ToString("N"));
        MethodInfo run = typeof(CatalogTool).GetMethod("RunAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        using var canceled = new CancellationTokenSource();
        await canceled.CancelAsync().ConfigureAwait(true);
        for (int attempt = 0; attempt < 8; attempt++)
        {
            OperationCanceledException failure = await Should.ThrowAsync<OperationCanceledException>(() => CatalogTool.RunOwnedPreparationAsync(path, "this-run", () =>
                (Task)run.Invoke(null, [path, "tool.log", new[] { "--info" }, canceled.Token])!)).ConfigureAwait(true);
            failure.Data.Contains("CatalogOwnedProcessStillRunning").ShouldBeFalse();
            Directory.Exists(path).ShouldBeFalse();
        }
    }

    /// <summary>Ordinary failed preparation removes partial owned output while a running owned child prevents deletion.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreparationFailurePreservesOnlyUnreapedOwnedProcesses(bool unreaped)
    {
        string path = Path.Combine(Path.GetTempPath(), "parties-catalog-cleanup-" + Guid.NewGuid().ToString("N"));
        const string Owner = "this-run";
        try
        {
            InvalidOperationException failure = await Should.ThrowAsync<InvalidOperationException>(() => CatalogTool.RunOwnedPreparationAsync(path, Owner, () =>
            {
                File.WriteAllText(Path.Combine(path, "partial-secret"), "run-owned-value");
                InvalidOperationException error = new("actual preparation failed");
                if (unreaped) { error.Data["CatalogOwnedProcessStillRunning"] = true; }
                throw error;
            })).ConfigureAwait(true);
            failure.Message.ShouldBe("actual preparation failed");
            Directory.Exists(path).ShouldBe(unreaped);
            if (unreaped) { File.Exists(Path.Combine(path, "partial-secret")).ShouldBeTrue(); }
        }
        finally { CatalogTool.DeleteOwnedDirectory(path, Owner); }
    }

    /// <summary>Successful run preparation retains its private files until disposal and refuses foreign ownership.</summary>
    [Fact]
    public async Task SuccessRetainsOwnedFilesUntilCleanupAndForeignFilesSurvive()
    {
        string path = Path.Combine(Path.GetTempPath(), "parties-catalog-cleanup-" + Guid.NewGuid().ToString("N"));
        const string Owner = "this-run";
        try
        {
            await CatalogTool.RunOwnedPreparationAsync(path, Owner, () => Task.CompletedTask).ConfigureAwait(true);
            Directory.Exists(path).ShouldBeTrue();
            Should.Throw<InvalidDataException>(() => CatalogTool.DeleteOwnedDirectory(path, "foreign-run"));
            File.ReadAllText(Path.Combine(path, ".owner")).ShouldBe(Owner);
            await Should.ThrowAsync<InvalidDataException>(() => CatalogTool.RunOwnedPreparationAsync(path, "foreign-run", () => Task.CompletedTask)).ConfigureAwait(true);
            CatalogTool.DeleteOwnedDirectory(path, Owner);
            CatalogTool.DeleteOwnedDirectory(path, Owner);
            Directory.Exists(path).ShouldBeFalse();
        }
        finally { CatalogTool.DeleteOwnedDirectory(path, Owner); }
    }
}
