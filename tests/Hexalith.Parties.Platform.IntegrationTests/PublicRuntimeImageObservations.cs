namespace Hexalith.Parties.Platform.IntegrationTests;

using System.Text.Json;

/// <summary>Reads the release and loaded image identities measured by the Platform package run.</summary>
internal static class PublicRuntimeImageObservations
{
    /// <summary>Requires the image observations produced for this exact native owner invocation.</summary>
    internal static JsonElement? Read(NativeDiagnosticBinding binding)
    {
        if (binding.Profile == "platform-source") return null;
        string directory = Path.Combine(binding.OwnerWorkspace, "platform-components", binding.EnvironmentId + "-catalog");
        using JsonDocument document = NativeDiagnosticBinding.ReadDocument(Path.Combine(directory, "released-images.json"));
        JsonElement receipt = document.RootElement;
        if (receipt.GetProperty("runId").GetString() != binding.RunId
            || receipt.GetProperty("attemptId").GetString() != binding.AttemptId
            || receipt.GetProperty("environmentId").GetString() != binding.EnvironmentId
            || receipt.GetProperty("mappingHash").GetString() != binding.MappingHash
            || receipt.GetProperty("qualificationCredit").GetBoolean())
            throw new InvalidDataException("Released image observations do not bind this baseline owner.");

        JsonElement images = receipt.GetProperty("images");
        var resources = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement image in images.EnumerateArray())
        {
            string resource = image.GetProperty("resourceId").GetString()!;
            string catalog = image.GetProperty("catalogImage").GetString()!;
            string expected = image.GetProperty("expectedReference").GetString()!;
            string observed = image.GetProperty("observedReference").GetString()!;
            string manifestDigest = image.GetProperty("expectedManifestDigest").GetString()!;
            string configurationDigest = image.GetProperty("expectedConfigDigest").GetString()!;
            string observedId = image.GetProperty("observedImageId").GetString()!;
            if (string.IsNullOrWhiteSpace(resource) || !resources.Add(resource)
                || catalog.LastIndexOf(':') <= catalog.IndexOf('/') || expected.LastIndexOf('@') <= 0
                || expected[..expected.LastIndexOf('@')] != catalog[..catalog.LastIndexOf(':')]
                || !IsDigest(manifestDigest) || expected[(expected.LastIndexOf('@') + 1)..] != manifestDigest
                || (observed != expected && observed != catalog + expected[expected.LastIndexOf('@')..])
                || !IsDigest(configurationDigest) || (observedId != manifestDigest && observedId != configurationDigest))
                throw new InvalidDataException($"Released image '{resource}' has an invalid or substituted runtime identity.");
        }
        if (resources.Count == 0) throw new InvalidDataException("The baseline owner observed no released dependency images.");
        return images.Clone();
    }

    private static bool IsDigest(string value) => value.Length == 71 && value.StartsWith("sha256:", StringComparison.Ordinal)
        && value.Skip("sha256:".Length).All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
