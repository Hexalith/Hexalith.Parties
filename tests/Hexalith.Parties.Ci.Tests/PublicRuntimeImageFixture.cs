namespace Hexalith.Parties.Ci.Tests;

using System.Formats.Tar;
using System.Text;
using System.Text.Json;
using Hexalith.Parties.Platform.IntegrationTests;

/// <summary>Supplies controlled inspect and tar bytes; it never invokes Docker or supplies native qualification.</summary>
/// <param name="mutation">The ownership, identity or archive boundary to alter.</param>
internal sealed class PublicRuntimeImageFixture(string mutation = "valid")
{
    /// <summary>The explicitly synthetic owner used only by this byte fixture.</summary>
    internal const string RunId = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string ContainerId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private int _inspections;

    /// <summary>Gets all requested read-only operations for exact protocol assertions.</summary>
    internal List<string[]> Commands { get; } = [];

    /// <summary>Creates the fixed fake file bytes, rather than copying static expected runtime hashes.</summary>
    /// <param name="name">The requested fixed file name.</param>
    /// <returns>Only synthetic test bytes.</returns>
    internal static byte[] Body(string name) => Encoding.UTF8.GetBytes("synthetic-byte-fixture-no-native-credit:" + name);

    /// <summary>Implements only the exact bounded inspect/copy protocol.</summary>
    /// <param name="arguments">The fixed client operation.</param>
    /// <param name="maximum">The reader's explicit output bound.</param>
    /// <param name="token">The observation cancellation.</param>
    /// <returns>Controlled bytes or an assertion failure for an unexpected operation.</returns>
    internal Task<byte[]> ReadAsync(IReadOnlyList<string> arguments, int maximum, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Commands.Add(arguments.ToArray());
        if (arguments[0] == "ps")
        {
            arguments.ShouldBe(["ps", "--all", "--quiet", "--no-trunc", "--filter", "label=hexalith.g4.run=" + RunId]);
            maximum.ShouldBe(8192);
            string result = mutation switch
            {
                "missing" => string.Empty,
                "duplicate-inventory" => ContainerId + "\n" + ContainerId,
                "malformed-id" => "--foreign-option",
                "ambiguous" => ContainerId + "\n" + new string('d', 64),
                _ => ContainerId,
            };
            return Task.FromResult(Encoding.UTF8.GetBytes(result + "\n"));
        }
        if (arguments[0] == "inspect")
        {
            maximum.ShouldBe(16384);
            arguments.Take(4).ShouldBe(["inspect", "--type", "container", "--format"]);
            arguments[4].ShouldNotContain(".Config.Env");
            _inspections++;
            var value = new Dictionary<string, object>
            {
                ["containerId"] = mutation == "foreign-id" ? new string('e', 64) : arguments[^1],
                ["runId"] = mutation == "foreign-owner" ? new string('f', 32) : RunId,
                ["imageReference"] = mutation == "tag-only" ? "registry.hexalith.com/parties:3.1.0" : mutation == "foreign-image" ? "foreign.invalid/owner@sha256:" + new string('c', 64) : PublicRuntimeImageObservations.ImageReference,
                ["imageContentId"] = mutation == "bad-content-id" ? "latest" : "sha256:" + new string(mutation == "changed-image" && _inspections > 1 ? 'e' : 'c', 64),
                ["running"] = mutation != "stopped",
                ["startedAt"] = mutation == "nanoseconds" ? "2026-01-01T00:00:00.123456789Z" : mutation == "changed-start" && _inspections > 1 ? "2026-01-02T00:00:00Z" : "2026-01-01T00:00:00Z",
            };
            if (mutation == "unknown-field") value.Add("unreviewed", true);
            return Task.FromResult(JsonSerializer.SerializeToUtf8Bytes(value));
        }
        arguments[0].ShouldBe("cp");
        arguments.Count.ShouldBe(3);
        arguments[1].ShouldStartWith(ContainerId + ":/app/");
        arguments[2].ShouldBe("-");
        maximum.ShouldBe(5 * 1024 * 1024);
        string name = arguments[1].Split('/')[^1];
        new[] { "Hexalith.Parties.Contracts.dll", "Hexalith.EventStore.Contracts.dll", "Hexalith.EventStore.Client.dll", "Hexalith.EventStore.DomainService.dll", "Hexalith.EventStore.ServiceDefaults.dll" }.ShouldContain(name);
        if (mutation == "oversized") return Task.FromResult(new byte[maximum + 1]);
        using MemoryStream output = new();
        using (TarWriter writer = new(output, leaveOpen: true))
        {
            var entry = new PaxTarEntry(mutation == "linked-file" ? TarEntryType.SymbolicLink : TarEntryType.RegularFile,
                mutation == "unsafe-path" ? "../" + name : name);
            if (mutation == "linked-file") entry.LinkName = "/foreign/file";
            else entry.DataStream = new MemoryStream(mutation == "empty-file" ? [] : mutation == "changed-bytes" ? Encoding.UTF8.GetBytes("different actual bytes") : Body(name));
            writer.WriteEntry(entry);
            if (mutation == "extra-file") writer.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, "extra") { DataStream = new MemoryStream([1]) });
        }
        return Task.FromResult(output.ToArray());
    }
}
