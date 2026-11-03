using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Binds the executed runtime child to the wrapper configuration and actual shared producer bytes.</summary>
internal static class MemoryRuntimeIdentity
{
    /// <summary>Reads assembly identity attributes without loading the child or its dependencies.</summary>
    internal static Dictionary<string, string> Metadata(string path)
    {
        using FileStream file = File.OpenRead(path);
        using var pe = new PEReader(file);
        MetadataReader reader = pe.GetMetadataReader();
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (CustomAttributeHandle handle in reader.GetAssemblyDefinition().GetCustomAttributes())
        {
            CustomAttribute attribute = reader.GetCustomAttribute(handle);
            if (attribute.Constructor.Kind != HandleKind.MemberReference) { continue; }
            MemberReference constructor = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
            if (constructor.Parent.Kind != HandleKind.TypeReference) { continue; }
            TypeReference type = reader.GetTypeReference((TypeReferenceHandle)constructor.Parent);
            if (reader.GetString(type.Namespace) != "System.Reflection" || reader.GetString(type.Name) != nameof(AssemblyMetadataAttribute)) { continue; }
            BlobReader value = reader.GetBlobReader(attribute.Value);
            if (value.ReadUInt16() != 1) { throw new InvalidDataException("Invalid runtime identity attribute."); }
            string key = value.ReadSerializedString() ?? throw new InvalidDataException("Missing runtime identity key.");
            values.Add(key, value.ReadSerializedString() ?? string.Empty);
        }
        return values;
    }

    /// <summary>Selects and validates the child recorded by the executing wrapper build.</summary>
    internal static string SelectChild(Assembly wrapper, IReadOnlyDictionary<string, string>? expected = null)
    {
        ArgumentNullException.ThrowIfNull(wrapper);
        Dictionary<string, string> wrapperIdentity = Metadata(wrapper.Location);
        string child = wrapperIdentity["HexalithRuntimeChildPath"];
        Validate(child, expected ?? wrapperIdentity, Path.GetDirectoryName(wrapper.Location)!);
        return child;
    }

    /// <summary>Refuses differing configuration, graph selection, producer bytes or refresh capability.</summary>
    internal static void Validate(string child, IReadOnlyDictionary<string, string> expected, string wrapperDirectory)
    {
        ArgumentNullException.ThrowIfNull(expected);
        Dictionary<string, string> actual = Metadata(child);
        foreach (string key in new[] { "Configuration", "TargetFramework", "RuntimeIdentifier", "MappingMode", "MappingHash", "EventStoreVersion", "MemoriesVersion", "FrontComposerVersion" })
        {
            string name = "HexalithRuntime" + key;
            if (!expected.TryGetValue(name, out string? value) || !actual.TryGetValue(name, out string? childValue) || value != childValue)
            { throw new InvalidDataException("Runtime child identity differs from the executing wrapper: " + key); }
        }
        string directory = Path.GetDirectoryName(child)!;
        foreach (string filename in new[] { "Hexalith.Parties.dll", "Hexalith.EventStore.Contracts.dll", "Hexalith.EventStore.Client.dll",
            "Hexalith.Memories.Contracts.dll", "Hexalith.Memories.Client.Rest.dll" })
        {
            if (Hash(Path.Combine(directory, filename)) != Hash(Path.Combine(wrapperDirectory, filename)))
            { throw new InvalidDataException("Runtime child producer bytes differ from the executing wrapper: " + filename); }
        }
        if (!Metadata(Path.Combine(directory, "Hexalith.Parties.dll")).TryGetValue("HexalithMemoriesSourceRefresh", out string? refresh) || refresh != "true")
        { throw new InvalidDataException("The executed runtime child does not support explicit source refresh."); }
    }

    /// <summary>Computes the exact executed file digest.</summary>
    internal static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    /// <summary>Captures wrapper, child, runtime manifests and shared producer bytes.</summary>
    internal static Dictionary<string, string> Files(Assembly wrapper, string child)
    {
        string directory = Path.GetDirectoryName(child)!;
        return new[] { wrapper.Location, child, Path.ChangeExtension(child, ".deps.json"), Path.ChangeExtension(child, ".runtimeconfig.json"),
            Path.Combine(directory, "Hexalith.Parties.dll"), Path.Combine(directory, "Hexalith.EventStore.Contracts.dll"),
            Path.Combine(directory, "Hexalith.EventStore.Client.dll"), Path.Combine(directory, "Hexalith.Memories.Contracts.dll"),
            Path.Combine(directory, "Hexalith.Memories.Client.Rest.dll") }.ToDictionary(path => path, Hash, StringComparer.Ordinal);
    }
}
