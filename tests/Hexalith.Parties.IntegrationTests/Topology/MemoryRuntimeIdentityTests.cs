using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Refuses stale child configuration or producer selection before any topology is started.</summary>
public sealed class MemoryRuntimeIdentityTests
{
    [Fact]
    public void MatchingChildUsesTheExecutingWrapperBuildMetadata()
    {
        var wrapper = typeof(PartyMemoryOwnedRuntimeTests).Assembly;
        string child = MemoryRuntimeIdentity.SelectChild(wrapper);
        child.ShouldBe(MemoryRuntimeIdentity.Metadata(wrapper.Location)["HexalithRuntimeChildPath"]);
        MemoryRuntimeIdentity.Files(wrapper, child).Values.ShouldAllBe(hash => hash.Length == 64);
    }

    [Theory]
    [InlineData("Configuration")]
    [InlineData("TargetFramework")]
    [InlineData("RuntimeIdentifier")]
    [InlineData("MappingMode")]
    [InlineData("MappingHash")]
    [InlineData("EventStoreVersion")]
    [InlineData("MemoriesVersion")]
    [InlineData("FrontComposerVersion")]
    public void MismatchedChildSelectionRefusesBeforeRuntimeMutation(string field)
    {
        var wrapper = typeof(PartyMemoryOwnedRuntimeTests).Assembly;
        Dictionary<string, string> expected = MemoryRuntimeIdentity.Metadata(wrapper.Location);
        expected["HexalithRuntime" + field] = "foreign-selection";
        Should.Throw<InvalidDataException>(() => MemoryRuntimeIdentity.SelectChild(wrapper, expected)).Message.ShouldContain(field);
    }

    [Fact]
    public void MismatchedExecutedProducerBytesRefuseBeforeRuntimeMutation()
    {
        var wrapper = typeof(PartyMemoryOwnedRuntimeTests).Assembly;
        string child = MemoryRuntimeIdentity.SelectChild(wrapper);
        string temporary = Path.Combine(Path.GetTempPath(), "memory-runtime-identity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            string copy = Path.Combine(temporary, Path.GetFileName(child));
            File.Copy(child, copy);
            File.Copy(Path.Combine(Path.GetDirectoryName(child)!, "Hexalith.Parties.dll"), Path.Combine(temporary, "Hexalith.Parties.dll"));
            File.AppendAllText(Path.Combine(temporary, "Hexalith.Parties.dll"), "changed producer bytes");
            Should.Throw<InvalidDataException>(() => MemoryRuntimeIdentity.Validate(copy,
                MemoryRuntimeIdentity.Metadata(wrapper.Location), Path.GetDirectoryName(wrapper.Location)!)).Message.ShouldContain("Hexalith.Parties.dll");
        }
        finally { Directory.Delete(temporary, true); }
    }
}
