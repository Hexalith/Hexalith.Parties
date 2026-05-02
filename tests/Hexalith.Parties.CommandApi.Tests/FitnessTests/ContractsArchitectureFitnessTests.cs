using Shouldly;

namespace Hexalith.Parties.CommandApi.Tests.FitnessTests;

public class ContractsArchitectureFitnessTests
{
    [Fact]
    public void ContractsProjectDoesNotReferenceMemoriesAssemblies()
    {
        string projectPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "Hexalith.Parties.Contracts",
            "Hexalith.Parties.Contracts.csproj"));

        string projectXml = File.ReadAllText(projectPath);

        projectXml.ShouldNotContain("Hexalith.Memories", Case.Insensitive);
    }
}
