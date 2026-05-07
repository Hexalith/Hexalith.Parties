using Shouldly;

namespace Hexalith.Parties.Tests.FitnessTests;

public class ContractsArchitectureFitnessTests
{
    [Fact]
    public void ContractsProjectDoesNotReferenceMemoriesAssemblies()
        => ReadProject("Hexalith.Parties.Contracts").ShouldNotContain("Hexalith.Memories", Case.Insensitive);

    [Fact]
    public void ContractsProjectDoesNotReferenceTenantsAssemblies()
        => ReadProject("Hexalith.Parties.Contracts").ShouldNotContain("Hexalith.Tenants", Case.Insensitive);

    [Fact]
    public void ClientProjectDoesNotReferenceTenantsAssemblies()
        => ReadProject("Hexalith.Parties.Client").ShouldNotContain("Hexalith.Tenants", Case.Insensitive);

    private static string ReadProject(string projectName)
    {
        string path = RepositoryRoot.ProjectFile(projectName);
        File.Exists(path).ShouldBeTrue($"Project file not found at '{path}'.");
        return File.ReadAllText(path);
    }
}
