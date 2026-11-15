using System.Text.Json;

using Shouldly;

namespace Hexalith.Parties.Tests.Search;

/// <summary>Qualifies private custody refusal and cancellation-safe authority fault restoration.</summary>
public sealed class PartyMemoryUnitAuthorityRuntimeTests
{
    [Fact]
    public async Task CancellationAfterAppliedMutationWaitsForAcknowledgementAndRestoresOriginal()
    {
        using var caller = new CancellationTokenSource();
        var applied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var acknowledge = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string state = "original";
        bool checkedFault = false;
        Task operation = PartyMemoryUnitAuthorityRuntime.WithMutationAsync(async () =>
        {
            state = "altered";
            applied.SetResult();
            await acknowledge.Task.ConfigureAwait(false);
        }, restore =>
        {
            restore.IsCancellationRequested.ShouldBeFalse();
            state.ShouldBe("altered");
            state = "original";
            return Task.CompletedTask;
        }, () => { checkedFault = true; return Task.CompletedTask; }, caller.Token);
        await applied.Task.ConfigureAwait(true);
        caller.Cancel();
        operation.IsCompleted.ShouldBeFalse("An applied command must be settled before reconciliation and caller return.");
        acknowledge.SetResult();
        await Should.ThrowAsync<OperationCanceledException>(() => operation);
        state.ShouldBe("original");
        checkedFault.ShouldBeFalse();
    }

    [Fact]
    public async Task UncertainAppliedFailureRestoresOriginalAndPreservesPrimaryFailure()
    {
        var primary = new IOException("synthetic acknowledgement loss");
        string state = "original";
        var observed = await Should.ThrowAsync<IOException>(() => PartyMemoryUnitAuthorityRuntime.WithMutationAsync(() =>
        {
            state = "altered";
            return Task.FromException(primary);
        }, _ => { state = "original"; return Task.CompletedTask; }, () => Task.CompletedTask, TestContext.Current.CancellationToken));
        observed.ShouldBeSameAs(primary);
        state.ShouldBe("original");
    }

    [Fact]
    public async Task RestorationFailureRetainsTheOriginalQualificationFailure()
    {
        var primary = new IOException("synthetic mutation failure");
        var cleanup = new IOException("synthetic competing authority");
        var observed = await Should.ThrowAsync<AggregateException>(() => PartyMemoryUnitAuthorityRuntime.WithMutationAsync(
            () => Task.FromException(primary), _ => Task.FromException(cleanup), () => Task.CompletedTask, TestContext.Current.CancellationToken));
        observed.InnerExceptions.ShouldBe([primary, cleanup]);
    }

    [Theory]
    [InlineData("matching")]
    [InlineData("endpoint")]
    [InlineData("duplicate")]
    [InlineData("foreign-name")]
    [InlineData("stopped")]
    [InlineData("missing")]
    public async Task CustodyRequiresExactRunningResourcesAndBoundSearchEndpoint(string variation)
    {
        string directory = Path.Combine(Path.GetTempPath(), "owned-authority-custody-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string[] names = ["fixture-redis", "fixture-memories-search", "fixture-memories-graph"];
            if (variation == "duplicate") { names[2] = names[1]; }
            if (variation == "foreign-name") { names[2] = "foreign-resource"; }
            if (variation == "missing") { names = names[..2]; }
            string generation = new('a', 32);
            string exclusion = "owned-disposable-session:synthetic:" + generation;
            string path = Path.Combine(directory, "custody.json");
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new
            {
                Session = "synthetic", Generation = generation, WriterExclusionEvidenceId = exclusion,
                Resources = names.Select(name => new { Name = name, State = variation == "stopped" ? "Stopped" : "Running", Endpoint = "tcp://127.0.0.1:23001/" }),
            }), TestContext.Current.CancellationToken);
            var config = new PartyMemoryRuntimeConfiguration
            {
                OwnershipGeneration = generation, OwnershipSessionId = "synthetic", OwnershipWriterExclusionEvidenceId = exclusion,
                OwnershipCustodyPath = path, OwnershipRedisEndpoint = variation == "endpoint" ? "127.0.0.1:23002" : "127.0.0.1:23001",
            };
            if (variation == "matching") { await PartyMemoryUnitAuthorityRuntime.ValidateAsync(config, TestContext.Current.CancellationToken); }
            else { await Should.ThrowAsync<InvalidOperationException>(() => PartyMemoryUnitAuthorityRuntime.ValidateAsync(config, TestContext.Current.CancellationToken)); }
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
