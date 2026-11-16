using System.Text.Json;

namespace Hexalith.Parties.Testing;

/// <summary>Coordinates bounded private producer restarts without exposing an HTTP control surface.</summary>
internal static class PartyMemoryProducerRestartProtocol
{
    /// <summary>The observed phases required by the five recovery boundaries, in execution order.</summary>
    internal static IReadOnlyList<string> Phases { get; } = ["cross-source-authority", "same-uri-refresh", "admitted-empty-mappings", "output-known", "accepted-deletion", "bounded-legacy-discovery"];

    /// <summary>Publishes evidence atomically and requires an exact authenticated acknowledgement.</summary>
    internal static async Task RequestAsync(string directory, string session, string secret, string phase, object evidence, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(phase is "admitted-empty-mappings" or "output-known" ? TimeSpan.FromMinutes(7) : TimeSpan.FromMinutes(1));
        string nonce = Guid.NewGuid().ToString("N");
        await WriteAsync(Path.Combine(directory, phase + ".request.json"), new { session, secret, phase, nonce, evidence }, deadline.Token).ConfigureAwait(false);
        string acknowledgement = Path.Combine(directory, phase + ".ack.json");
        while (!File.Exists(acknowledgement)) { await Task.Delay(25, deadline.Token).ConfigureAwait(false); }
        using JsonDocument reply = await ReadAsync(acknowledgement, deadline.Token).ConfigureAwait(false);
        Validate(reply.RootElement, session, secret, phase);
        if (reply.RootElement.GetProperty("nonce").GetString() != nonce || !reply.RootElement.GetProperty("replaced").GetBoolean())
        { throw new InvalidDataException("The producer replacement acknowledgement does not match this request."); }
    }

    /// <summary>Processes each phase exactly once; failed restarts never receive a success acknowledgement.</summary>
    internal static async Task ServeAsync(string directory, string session, string secret,
        Func<string, JsonElement, CancellationToken, Task> restart, CancellationToken token)
    {
        var nonces = new HashSet<string>(StringComparer.Ordinal);
        foreach (string phase in Phases)
        {
            string request = Path.Combine(directory, phase + ".request.json");
            while (!File.Exists(request)) { await Task.Delay(25, token).ConfigureAwait(false); }
            using JsonDocument document = await ReadAsync(request, token).ConfigureAwait(false);
            JsonElement message = document.RootElement;
            Validate(message, session, secret, phase);
            string nonce = message.GetProperty("nonce").GetString()!;
            if (!nonces.Add(nonce)) { throw new InvalidDataException("A producer restart request was replayed."); }
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(phase is "admitted-empty-mappings" or "output-known" ? TimeSpan.FromMinutes(6) : TimeSpan.FromSeconds(45));
            await restart(phase, message.GetProperty("evidence").Clone(), deadline.Token).ConfigureAwait(false);
            await WriteAsync(Path.Combine(directory, phase + ".ack.json"), new { session, secret, phase, nonce, replaced = true }, deadline.Token).ConfigureAwait(false);
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
