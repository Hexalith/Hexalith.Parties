using System.Text.Json;

using Hexalith.Parties.Testing;
using Shouldly;

namespace Hexalith.Parties.Tests.Search;

/// <summary>Verifies private phase authentication, replay refusal and bounded failure/cancellation coordination.</summary>
public sealed class PartyMemoryProducerRestartProtocolTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ObservationOnlyNeverAttestsAReplacementAndRefusesModeMismatch(bool mismatch)
    {
        string directory = CreateDirectory();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        bool entered = false;
        Task server = PartyMemoryProducerRestartProtocol.ServeAsync(directory, "owned", "private", (_, _, _) =>
        { entered = true; return Task.CompletedTask; }, deadline.Token, observationOnly: !mismatch);
        Task client = PartyMemoryProducerRestartProtocol.RequestAsync(directory, "owned", "private", "cross-source-authority",
            new { persisted = true }, deadline.Token, observationOnly: true);
        try
        {
            if (mismatch)
            {
                await Should.ThrowAsync<InvalidDataException>(server);
                entered.ShouldBeFalse();
                File.Exists(Path.Combine(directory, "cross-source-authority.ack.json")).ShouldBeFalse();
            }
            else
            {
                await client.ConfigureAwait(true);
                using JsonDocument acknowledgement = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "cross-source-authority.ack.json"), deadline.Token));
                acknowledgement.RootElement.GetProperty("observed").GetBoolean().ShouldBeTrue();
                acknowledgement.RootElement.TryGetProperty("replaced", out _).ShouldBeFalse();
                entered.ShouldBeTrue();
            }
        }
        finally { deadline.Cancel(); await SettleAsync(server); await SettleAsync(client); Directory.Delete(directory, recursive: true); }
    }

    [Theory]
    [InlineData("session")]
    [InlineData("secret")]
    [InlineData("phase")]
    [InlineData("nonce")]
    public void ForeignOrMalformedRequestsRefuseBeforeRestart(string field)
    {
        var request = new Dictionary<string, string> { ["session"] = "owned", ["secret"] = "private", ["phase"] = "cross-source-authority", ["nonce"] = new('a', 32) };
        request[field] = "foreign";
        Should.Throw<InvalidDataException>(() => PartyMemoryProducerRestartProtocol.Validate(JsonSerializer.SerializeToElement(request), "owned", "private", "cross-source-authority"));
    }

    [Fact]
    public async Task EveryPhaseRequiresItsOwnCompletedRestartAcknowledgement()
    {
        string directory = CreateDirectory();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        var observed = new List<string>();
        Task server = PartyMemoryProducerRestartProtocol.ServeAsync(directory, "owned", "private", (phase, _, _) =>
        { observed.Add(phase); return Task.CompletedTask; }, deadline.Token);
        try
        {
            foreach (string phase in PartyMemoryProducerRestartProtocol.Phases)
            { await PartyMemoryProducerRestartProtocol.RequestAsync(directory, "owned", "private", phase, new { persisted = true }, deadline.Token); }
            await server.ConfigureAwait(true);
            observed.ShouldBe(PartyMemoryProducerRestartProtocol.Phases);
        }
        finally { deadline.Cancel(); await SettleAsync(server); Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task ReplayedNonceCannotReplaceAnotherPhase()
    {
        string directory = CreateDirectory();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        int restarts = 0;
        try
        {
            foreach (string phase in PartyMemoryProducerRestartProtocol.Phases.Take(2))
            {
                await File.WriteAllTextAsync(Path.Combine(directory, phase + ".request.json"), JsonSerializer.Serialize(new
                { session = "owned", secret = "private", phase, nonce = new string('a', 32), evidence = new { persisted = true } }), deadline.Token);
            }
            await Should.ThrowAsync<InvalidDataException>(() => PartyMemoryProducerRestartProtocol.ServeAsync(directory, "owned", "private",
                (_, _, _) => { restarts++; return Task.CompletedTask; }, deadline.Token));
            restarts.ShouldBe(1);
            File.Exists(Path.Combine(directory, "same-uri-refresh.ack.json")).ShouldBeFalse();
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Theory]
    [InlineData("restart")]
    [InlineData("evidence")]
    [InlineData("cancellation")]
    public async Task FailedOrCanceledRestartSettlesWorkAndNeverAcknowledgesSuccess(string failure)
    {
        string directory = CreateDirectory();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        bool ownedWorkDisposed = false;
        Task server = Task.CompletedTask;
        Task client = Task.CompletedTask;
        try
        {
            string phase = PartyMemoryProducerRestartProtocol.Phases[0];
            server = PartyMemoryProducerRestartProtocol.ServeAsync(directory, "owned", "private", async (_, _, token) =>
            {
                try
                {
                    if (failure == "cancellation") { deadline.Cancel(); token.ThrowIfCancellationRequested(); }
                    if (failure == "evidence") { await File.WriteAllTextAsync(directory, "cannot replace an evidence directory", token).ConfigureAwait(true); }
                    throw new IOException("synthetic failed replacement");
                }
                finally { ownedWorkDisposed = true; }
            }, deadline.Token);
            client = PartyMemoryProducerRestartProtocol.RequestAsync(directory, "owned", "private", phase, new { persisted = true }, deadline.Token);
            await Should.ThrowAsync<Exception>(server);
            deadline.Cancel();
            await Should.ThrowAsync<OperationCanceledException>(client);
            ownedWorkDisposed.ShouldBeTrue();
            File.Exists(Path.Combine(directory, phase + ".ack.json")).ShouldBeFalse();
        }
        finally { deadline.Cancel(); await SettleAsync(server); await SettleAsync(client); Directory.Delete(directory, recursive: true); }
    }

    private static string CreateDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "producer-restart-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static async Task SettleAsync(Task task)
    {
        try { await task.ConfigureAwait(true); }
        catch (Exception) { /* The owning test asserts the primary failure before final settlement. */ }
    }
}
