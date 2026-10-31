extern alias apphost;

using Shouldly;

using CatalogTool = apphost::Hexalith.Parties.AppHost.PartiesDevelopmentCatalogTool;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Exercises the actual preparation ownership boundary on success, failure and foreign directories.</summary>
public sealed class DevelopmentCatalogPreparationCleanupTests
{
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
