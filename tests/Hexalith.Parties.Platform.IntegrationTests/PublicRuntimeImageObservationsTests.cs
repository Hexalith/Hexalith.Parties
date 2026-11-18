namespace Hexalith.Parties.Platform.IntegrationTests;

using System.Text.Json;

using Shouldly;

/// <summary>Local released-image receipts only; no Docker, registry or native owner is contacted.</summary>
public sealed class PublicRuntimeImageObservationsTests
{
    /// <summary>Accepts both launched reference and image-ID forms and refuses foreign or substituted receipts.</summary>
    /// <param name="mutation">The receipt change to exercise.</param>
    [Theory]
    [InlineData("accepted-immutable-config")]
    [InlineData("accepted-tagged-manifest")]
    [InlineData("foreign-run")]
    [InlineData("foreign-attempt")]
    [InlineData("foreign-environment")]
    [InlineData("foreign-mapping")]
    [InlineData("qualification-credit")]
    [InlineData("duplicate-resource")]
    [InlineData("foreign-repository")]
    [InlineData("substituted-reference")]
    [InlineData("substituted-image-id")]
    [InlineData("empty-images")]
    [InlineData("platform-source")]
    public void ReadBindsResolvedDependencyImagesToThisOwner(string mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        string workspace = Directory.CreateTempSubdirectory("parties-local-released-images-").FullName;
        try
        {
            using JsonDocument context = JsonDocument.Parse("{\"kind\":\"local\"}");
            var binding = new NativeDiagnosticBinding(new string('1', 32), new string('2', 32), new string('3', 32), workspace, workspace,
                new string('a', 40), new string('b', 64), context.RootElement.Clone(), workspace, "local-model",
                mutation == "platform-source" ? "platform-source" : "platform-package");
            if (mutation == "platform-source")
            {
                PublicRuntimeImageObservations.Read(binding).ShouldBeNull();
                return;
            }

            const string catalog = "registry.hexalith.com/tenants:1.0.0";
            string manifest = "sha256:" + new string('c', 64), config = "sha256:" + new string('d', 64), substitute = "sha256:" + new string('e', 64);
            string expected = (mutation == "foreign-repository" ? "registry.hexalith.com/memories" : "registry.hexalith.com/tenants") + "@" + manifest;
            Dictionary<string, string> image = new(StringComparer.Ordinal)
            {
                ["resourceId"] = "tenants",
                ["catalogImage"] = catalog,
                ["expectedReference"] = expected,
                ["expectedManifestDigest"] = manifest,
                ["expectedConfigDigest"] = config,
                ["observedReference"] = mutation switch
                {
                    "accepted-tagged-manifest" => catalog + "@" + manifest,
                    "substituted-reference" => "registry.hexalith.com/tenants@" + substitute,
                    _ => expected,
                },
                ["observedImageId"] = mutation switch
                {
                    "accepted-tagged-manifest" => manifest,
                    "substituted-image-id" => substitute,
                    _ => config,
                },
                ["containerId"] = new string('f', 64),
            };
            Dictionary<string, string>[] images = mutation switch
            {
                "empty-images" => [],
                "duplicate-resource" => [image, new(image) { ["containerId"] = new string('9', 64) }],
                _ => [image],
            };
            string directory = Directory.CreateDirectory(Path.Combine(workspace, "platform-components", binding.EnvironmentId + "-catalog")).FullName;
            File.WriteAllBytes(Path.Combine(directory, "released-images.json"), JsonSerializer.SerializeToUtf8Bytes(new
            {
                runId = mutation == "foreign-run" ? new string('4', 32) : binding.RunId,
                attemptId = mutation == "foreign-attempt" ? new string('4', 32) : binding.AttemptId,
                environmentId = mutation == "foreign-environment" ? new string('4', 32) : binding.EnvironmentId,
                mappingHash = mutation == "foreign-mapping" ? new string('4', 64) : binding.MappingHash,
                qualificationCredit = mutation == "qualification-credit",
                images,
            }));
            if (!mutation.StartsWith("accepted-", StringComparison.Ordinal))
            {
                Should.Throw<InvalidDataException>(() => PublicRuntimeImageObservations.Read(binding));
                return;
            }

            JsonElement? observed = PublicRuntimeImageObservations.Read(binding);
            observed.HasValue.ShouldBeTrue();
            JsonElement.DeepEquals(observed.GetValueOrDefault(), JsonSerializer.SerializeToElement(images)).ShouldBeTrue();
        }
        finally { Directory.Delete(workspace, recursive: true); }
    }
}
