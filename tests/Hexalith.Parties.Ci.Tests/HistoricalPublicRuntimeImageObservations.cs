namespace Hexalith.Parties.Ci.Tests;

using System.Formats.Tar;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>Measures five fixed files in the actual running owned public Parties image without executing container commands.</summary>
internal static class HistoricalPublicRuntimeImageObservations
{
    /// <summary>The already authenticated immutable public image index; tags confer no authority.</summary>
    internal const string ImageReference = "registry.hexalith.com/parties@sha256:48d0e8db506e4ce574e8c7bd97e528a302d3fbf97754b007176904cbc1f3abfb";
    private const int MaximumFileBytes = 4 * 1024 * 1024;
    private const int MaximumTarBytes = 5 * 1024 * 1024;
    private const string InspectTemplate = "{\"containerId\":{{json .Id}},\"runId\":{{json (index .Config.Labels \"hexalith.g4.run\")}},\"imageReference\":{{json .Config.Image}},\"imageContentId\":{{json .Image}},\"running\":{{json .State.Running}},\"startedAt\":{{json .State.StartedAt}}}";
    private static readonly string[] _files = ["Hexalith.Parties.Contracts.dll", "Hexalith.EventStore.Contracts.dll", "Hexalith.EventStore.Client.dll", "Hexalith.EventStore.DomainService.dll", "Hexalith.EventStore.ServiceDefaults.dll"];

    /// <summary>Exercises the real fixed inspect/read protocol through a bounded process seam; fixture bytes grant no native credit.</summary>
    /// <param name="runId">The exact validated owner run.</param>
    /// <param name="read">The fixed Docker client or a resource-free byte fixture.</param>
    /// <param name="token">The overall observation deadline.</param>
    /// <returns>Actual container identity and actual hashes of its five file bodies.</returns>
    internal static async Task<JsonElement> ObserveImageAsync(string runId, Func<IReadOnlyList<string>, int, CancellationToken, Task<byte[]>> read, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Require(Regex.IsMatch(runId, "\\A[a-f0-9]{32}\\z", RegexOptions.CultureInvariant), "Malformed public image owner.");
        byte[] listing = await read(["ps", "--all", "--quiet", "--no-trunc", "--filter", "label=hexalith.g4.run=" + runId], 8192, token).ConfigureAwait(false);
        Require(listing.Length <= 8192, "Owned image inventory exceeds its bound.");
        string[] ids = Encoding.UTF8.GetString(listing).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Require(ids.Length is > 0 and <= 32 && ids.Distinct(StringComparer.Ordinal).Count() == ids.Length
            && ids.All(id => Regex.IsMatch(id, "\\A[a-f0-9]{64}\\z", RegexOptions.CultureInvariant)), "Owned container discovery is absent, duplicate or malformed.");
        JsonElement? selected = null;
        foreach (string id in ids)
        {
            JsonElement current = await InspectAsync(id, runId, read, token).ConfigureAwait(false);
            string image = current.GetProperty("imageReference").GetString()!;
            if (image.StartsWith("registry.hexalith.com/parties:", StringComparison.Ordinal)
                || image.StartsWith("registry.hexalith.com/parties@", StringComparison.Ordinal))
            {
                Require(image == ImageReference && selected is null, "Public Parties container is unpinned or ambiguous.");
                selected = current;
            }
        }
        Require(selected is not null, "The running public Parties container was not observed.");
        JsonElement before = selected!.Value;
        string container = before.GetProperty("containerId").GetString()!;
        Dictionary<string, object> bodies = new(StringComparer.Ordinal);
        foreach (string name in _files)
        {
            byte[] archive = await read(["cp", container + ":/app/" + name, "-"], MaximumTarBytes, token).ConfigureAwait(false);
            bodies.Add(name[..^4], ReadFileTar(archive, name));
        }
        JsonElement after = await InspectAsync(container, runId, read, token).ConfigureAwait(false);
        Require(JsonElement.DeepEquals(before, after), "The owned running image changed during measurement.");
        token.ThrowIfCancellationRequested();
        return JsonSerializer.SerializeToElement(new { image = before, bodies });
    }

    private static async Task<JsonElement> InspectAsync(string id, string runId, Func<IReadOnlyList<string>, int, CancellationToken, Task<byte[]>> read, CancellationToken token)
    {
        byte[] raw = await read(["inspect", "--type", "container", "--format", InspectTemplate, id], 16384, token).ConfigureAwait(false);
        Require(raw.Length <= 16384, "Owned image inspection exceeds its bound.");
        using JsonDocument document = JsonDocument.Parse(raw, new JsonDocumentOptions { MaxDepth = 8 });
        JsonElement value = document.RootElement;
        string[] expected = ["containerId", "runId", "imageReference", "imageContentId", "running", "startedAt"];
        Require(value.ValueKind == JsonValueKind.Object && value.EnumerateObject().Select(item => item.Name).Order(StringComparer.Ordinal)
            .SequenceEqual(expected.Order(StringComparer.Ordinal)), "Owned image inspection has unknown or duplicate fields.");
        Require(value.GetProperty("containerId").GetString() == id && value.GetProperty("runId").GetString() == runId
            && value.GetProperty("running").ValueKind == JsonValueKind.True, "Image ownership, identity or running state differs.");
        Require(Regex.IsMatch(value.GetProperty("imageContentId").GetString() ?? string.Empty, "\\Asha256:[a-f0-9]{64}\\z", RegexOptions.CultureInvariant)
            && DateTimeOffset.TryParse(value.GetProperty("startedAt").GetString(), out DateTimeOffset started)
            && started > DateTimeOffset.UnixEpoch && started <= DateTimeOffset.UtcNow, "Running image content or start identity is absent.");
        return value.Clone();
    }

    private static object ReadFileTar(byte[] archive, string name)
    {
        Require(archive.Length is > 0 and <= MaximumTarBytes, "Image file archive exceeds its bound.");
        using MemoryStream input = new(archive, writable: false);
        using TarReader reader = new(input, leaveOpen: false);
        TarEntry? entry = reader.GetNextEntry();
        Require(entry is not null && entry.Name == name && entry.EntryType is TarEntryType.RegularFile or TarEntryType.V7RegularFile
            && entry.Length is > 0 and <= MaximumFileBytes && entry.DataStream is not null, "Image file archive has an unexpected path, link, type or size.");
        using MemoryStream bytes = new();
        entry!.DataStream!.CopyTo(bytes);
        Require(bytes.Length == entry.Length && reader.GetNextEntry() is null, "Image file archive has extra or incomplete bodies.");
        return new { role = "running-public-image-file", path = "/app/" + name, bytes = bytes.Length,
            sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes.GetBuffer().AsSpan(0, checked((int)bytes.Length)))) };
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
