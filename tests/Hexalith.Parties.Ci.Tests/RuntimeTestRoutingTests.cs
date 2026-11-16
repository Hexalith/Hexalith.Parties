using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

using Hexalith.Parties.PackageTests;

namespace Hexalith.Parties.Ci.Tests;

/// <summary>Checks the compiled boundary between ordinary tests and live provider workloads.</summary>
public sealed class RuntimeTestRoutingTests
{
    /// <summary>Retains every live workload in the integration assembly without duplicate unit execution.</summary>
    [Fact]
    public void LiveWorkloadsExecuteOnlyFromTheRuntimeIntegrationAssembly()
    {
        string configuration = PackageTestProcess.ProbeConfiguration;
        HashSet<string> unit = ReadTypes(CiTestPaths.ProjectOutputFile("tests/Hexalith.Parties.Tests", configuration, "Hexalith.Parties.Tests.dll"));
        HashSet<string> runtime = ReadTypes(CiTestPaths.ProjectOutputFile("tests/Hexalith.Parties.Runtime.IntegrationTests", configuration, "Hexalith.Parties.Runtime.IntegrationTests.dll"));
        foreach (string name in new[]
        {
            "Hexalith.Parties.Tests.Search.PartyMemoryRuntimeTests",
            "Hexalith.Parties.Tests.Search.PartyMemoryPersistenceRuntimeTests",
            "Hexalith.Parties.Tests.Search.PartyMemoryOwnerDependencyTests",
            "Hexalith.Parties.Tests.Projections.PartyLegacyBackfillProcessRuntimeTests",
            "Hexalith.Parties.Tests.Projections.PartyLegacyReadModelBackfillRuntimeTests",
        })
        {
            unit.ShouldNotContain(name);
            runtime.ShouldContain(name);
        }
        unit.ShouldContain("Hexalith.Parties.Tests.Search.PartyMemoryUnitAuthorityRuntimeTests");
        runtime.ShouldNotContain("Hexalith.Parties.Tests.Search.PartyMemoryUnitAuthorityRuntimeTests");
    }

    private static HashSet<string> ReadTypes(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        MetadataReader metadata = pe.GetMetadataReader();
        return metadata.TypeDefinitions.Select(handle => metadata.GetTypeDefinition(handle))
            .Select(type => metadata.GetString(type.Namespace) + "." + metadata.GetString(type.Name))
            .ToHashSet(StringComparer.Ordinal);
    }
}
