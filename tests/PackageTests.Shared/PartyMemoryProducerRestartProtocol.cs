using System.Text.Json;

namespace Hexalith.Parties.Testing;

/// <summary>Coordinates bounded private producer restarts without exposing an HTTP control surface.</summary>
internal static class PartyMemoryProducerRestartProtocol
{
    /// <summary>Names the private phase-observation mode used only by the external runner interruption supervisor.</summary>
    internal const string ObservationOnlyVariable = "PARTIES_MEMORY_PRODUCER_CHECKPOINT_ONLY";
    /// <summary>The observed phases required by the five recovery boundaries, in execution order.</summary>
    internal static IReadOnlyList<string> Phases { get; } = ["cross-source-authority", "same-uri-refresh", "admitted-empty-mappings", "output-known", "accepted-deletion", "bounded-legacy-discovery"];

    /// <summary>Publishes evidence atomically and requires an exact authenticated acknowledgement.</summary>
    internal static async Task RequestAsync(string directory, string session, string secret, string phase, object evidence, CancellationToken token, bool? observationOnly = null)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(phase is "admitted-empty-mappings" or "output-known" ? TimeSpan.FromMinutes(7) : TimeSpan.FromMinutes(1));
        string nonce = Guid.NewGuid().ToString("N");
        bool observeOnly = observationOnly ?? Environment.GetEnvironmentVariable(ObservationOnlyVariable) == "1";
        await WriteAsync(Path.Combine(directory, phase + ".request.json"), new { session, secret, phase, nonce, evidence, observationOnly = observeOnly }, deadline.Token).ConfigureAwait(false);
        string acknowledgement = Path.Combine(directory, phase + ".ack.json");
        while (!File.Exists(acknowledgement)) { await Task.Delay(25, deadline.Token).ConfigureAwait(false); }
        using JsonDocument reply = await ReadAsync(acknowledgement, deadline.Token).ConfigureAwait(false);
        Validate(reply.RootElement, session, secret, phase);
        string outcome = observeOnly ? "observed" : "replaced";
        if (reply.RootElement.GetProperty("nonce").GetString() != nonce || !reply.RootElement.GetProperty(outcome).GetBoolean()
            || reply.RootElement.TryGetProperty(observeOnly ? "replaced" : "observed", out _))
        { throw new InvalidDataException("The producer replacement acknowledgement does not match this request."); }
    }

    /// <summary>Processes each phase exactly once; failed restarts never receive a success acknowledgement.</summary>
    internal static async Task ServeAsync(string directory, string session, string secret,
        Func<string, JsonElement, CancellationToken, Task> restart, CancellationToken token, bool observationOnly = false)
    {
        var nonces = new HashSet<string>(StringComparer.Ordinal);
        foreach (string phase in Phases)
        {
            string request = Path.Combine(directory, phase + ".request.json");
            while (!File.Exists(request)) { await Task.Delay(25, token).ConfigureAwait(false); }
            using JsonDocument document = await ReadAsync(request, token).ConfigureAwait(false);
            JsonElement message = document.RootElement;
            Validate(message, session, secret, phase);
            bool requestedObservation = message.TryGetProperty("observationOnly", out JsonElement mode) && mode.GetBoolean();
            if (requestedObservation != observationOnly) { throw new InvalidDataException("The private producer checkpoint mode does not match its supervisor."); }
            string nonce = message.GetProperty("nonce").GetString()!;
            if (!nonces.Add(nonce)) { throw new InvalidDataException("A producer restart request was replayed."); }
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            TimeSpan phaseBudget = phase is "admitted-empty-mappings" or "output-known" ? TimeSpan.FromMinutes(6) : TimeSpan.FromSeconds(45);
            deadline.CancelAfter(phaseBudget);
            Dictionary<string, JsonElement> phaseEvidence = message.GetProperty("evidence").EnumerateObject().ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal);
            phaseEvidence["_PhaseExpiresUtc"] = JsonSerializer.SerializeToElement(DateTimeOffset.UtcNow + phaseBudget);
            await restart(phase, JsonSerializer.SerializeToElement(phaseEvidence), deadline.Token).ConfigureAwait(false);
            await WriteAsync(Path.Combine(directory, phase + ".ack.json"), new Dictionary<string, object>
            { ["session"] = session, ["secret"] = secret, ["phase"] = phase, ["nonce"] = nonce, [observationOnly ? "observed" : "replaced"] = true }, deadline.Token).ConfigureAwait(false);
        }
    }

    /// <summary>Rejects foreign scope, phase and malformed request identities before lifecycle operations.</summary>
    internal static void Validate(JsonElement message, string session, string secret, string phase)
    {
        if (message.GetProperty("session").GetString() != session || message.GetProperty("secret").GetString() != secret
            || message.GetProperty("phase").GetString() != phase || !Phases.Contains(phase)
            || message.GetProperty("nonce").GetString() is not { Length: 32 } nonce || !nonce.All(char.IsAsciiHexDigit))
        { throw new InvalidDataException("Foreign or malformed producer restart request."); }
    }

    private static async Task<JsonDocument> ReadAsync(string path, CancellationToken token)
    {
        if (new FileInfo(path).Length > 65536) { throw new InvalidDataException("Producer restart evidence exceeds its private bound."); }
        return JsonDocument.Parse(await File.ReadAllBytesAsync(path, token).ConfigureAwait(false));
    }

    private static async Task WriteAsync(string path, object value, CancellationToken token)
    {
        string staging = path + "." + Guid.NewGuid().ToString("N");
        try
        {
            await File.WriteAllTextAsync(staging, JsonSerializer.Serialize(value), token).ConfigureAwait(false);
            File.Move(staging, path, overwrite: false);
        }
        finally { File.Delete(staging); }
    }
}
