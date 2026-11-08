namespace Hexalith.Parties.Platform.IntegrationTests;

using System.Diagnostics;
using System.Formats.Tar;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>Measures five fixed files in the actual running owned public Parties image without executing container commands.</summary>
internal static class PublicRuntimeImageObservations
{
    /// <summary>The already authenticated immutable public image index; tags confer no authority.</summary>
    internal const string ImageReference = "registry.hexalith.com/parties@sha256:48d0e8db506e4ce574e8c7bd97e528a302d3fbf97754b007176904cbc1f3abfb";
    private const int MaximumFileBytes = 4 * 1024 * 1024;
    private const int MaximumTarBytes = 5 * 1024 * 1024;
    private const string InspectTemplate = "{\"containerId\":{{json .Id}},\"runId\":{{json (index .Config.Labels \"hexalith.g4.run\")}},\"imageReference\":{{json .Config.Image}},\"imageContentId\":{{json .Image}},\"running\":{{json .State.Running}},\"startedAt\":{{json .State.StartedAt}}}";
    private static readonly string[] _files = ["Hexalith.Parties.Contracts.dll", "Hexalith.EventStore.Contracts.dll", "Hexalith.EventStore.Client.dll", "Hexalith.EventStore.DomainService.dll", "Hexalith.EventStore.ServiceDefaults.dll"];

    /// <summary>Retains only ordinary public hosted observations, never substituting for private current-owner proof.</summary>
    /// <param name="binding">The descriptor, plan, mapping and issued owner already cross-validated by the native handoff.</param>
    /// <param name="token">The unchanged real functional-test bound.</param>
    /// <returns>The bounded measured record for the existing content-addressed functional diagnostic, or no hosted claim.</returns>
    internal static async Task<JsonElement?> ReadAsync(NativeDiagnosticBinding binding, CancellationToken token)
    {
        if (binding.Profile is "platform-current-package" or "platform-source") return null;
        Require(binding.Profile is "platform-package" or "lifecycle-package", "Unknown public observation purpose.");
        if (binding.ExecutionContext.GetProperty("kind").GetString() == "local") return null;
        Require(OperatingSystem.IsLinux() && binding.ExecutionContext.GetProperty("kind").GetString() == "ci", "Public runtime observations require the actual Linux CI owner.");
        binding.VerifyCallerArtifact();
        JsonElement platform = binding.PlatformIdentity;
        foreach (string name in new[] { "Commit", "CompositionCommit", "CheckoutCommit" })
            Require(platform.GetProperty(name).GetString() == "0f4e27f1f78310bd731c10c19dc51bb1e0345d44", "Public runtime Platform source differs from the authenticated release.");
        Require(platform.GetProperty("CatalogCommit").GetString() == "3c8096180ecf2aa539ac454815efbfceb8a92822"
            && platform.GetProperty("ReleaseTag").GetString() == "v1.7.1", "Public runtime catalog or release tag differs.");
        using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(token);
        bound.CancelAfter(TimeSpan.FromSeconds(20));
        JsonElement actual = await ObserveImageAsync(binding.RunId, RunDockerAsync, bound.Token).ConfigureAwait(false);
        return JsonSerializer.SerializeToElement(new
        {
            kind = "running-owned-public-parties-image", profile = binding.Profile,
            ownerRunId = binding.RunId, ownerAttemptId = binding.AttemptId, environmentId = binding.EnvironmentId,
            revision = binding.Revision, mappingHash = binding.MappingHash, executionContext = binding.ExecutionContext,
            platform = new { commit = platform.GetProperty("Commit"), compositionCommit = platform.GetProperty("CompositionCommit"),
                checkoutCommit = platform.GetProperty("CheckoutCommit"), catalogCommit = platform.GetProperty("CatalogCommit"), releaseTag = platform.GetProperty("ReleaseTag") },
            image = actual.GetProperty("image"), bodies = actual.GetProperty("bodies"), measuredAtUtc = DateTimeOffset.UtcNow,
            qualificationCredit = false,
        });
    }

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

    private static async Task<byte[]> RunDockerAsync(IReadOnlyList<string> arguments, int maximumOutput, CancellationToken token)
    {
        ProcessStartInfo start = new("docker") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using Process process = Process.Start(start) ?? throw new IOException("The owned image reader could not start.");
        Task<byte[]> output = ReadBoundedAsync(process.StandardOutput.BaseStream, maximumOutput, token);
        Task<byte[]> error = ReadBoundedAsync(process.StandardError.BaseStream, 16384, token);
        try
        {
            await Task.WhenAll(output, error, process.WaitForExitAsync(token)).ConfigureAwait(false);
            Require(process.ExitCode == 0, "The bounded owned image read failed.");
            return await output.ConfigureAwait(false);
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream input, int maximum, CancellationToken token)
    {
        using MemoryStream result = new();
        byte[] buffer = new byte[65536];
        while (true)
        {
            int read = await input.ReadAsync(buffer, token).ConfigureAwait(false);
            if (read == 0) return result.ToArray();
            Require(result.Length + read <= maximum, "The owned image client exceeded its output bound.");
            result.Write(buffer, 0, read);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
