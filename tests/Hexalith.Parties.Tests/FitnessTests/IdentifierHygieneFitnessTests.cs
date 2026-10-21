using Shouldly;

using static Hexalith.Parties.Tests.FitnessTests.RepositoryTestProcess;

namespace Hexalith.Parties.Tests.FitnessTests;

public sealed class IdentifierHygieneFitnessTests
{
    [Fact]
    public void SemanticIdentifierValidationSources_DoNotUseGuidParsers()
    {
        string root = RepositoryRoot.Locate();
        string validationRoot = Path.Combine(root, "src", "Hexalith.Parties", "Validation");
        string aggregatePath = Path.Combine(root, "src", "Hexalith.Parties", "Domain", "PartyAggregate.cs");

        string[] sourceFiles =
        [
            .. Directory.GetFiles(validationRoot, "*.cs", SearchOption.TopDirectoryOnly),
            aggregatePath,
        ];

        foreach (string sourceFile in sourceFiles)
        {
            string source = File.ReadAllText(sourceFile);
            source.Contains("Guid.TryParse", StringComparison.Ordinal).ShouldBeFalse(Path.GetRelativePath(root, sourceFile));
            source.Contains("Guid.Parse", StringComparison.Ordinal).ShouldBeFalse(Path.GetRelativePath(root, sourceFile));
            source.Contains("new Guid(", StringComparison.Ordinal).ShouldBeFalse(Path.GetRelativePath(root, sourceFile));
        }
    }

    [Fact]
    public void GatewayCommandIdSources_DoNotUseGuidNewGuidForSemanticIds()
    {
        string root = RepositoryRoot.Locate();
        string[] sourceFiles =
        [
            Path.Combine(root, "src", "Hexalith.Parties.Client", "HttpPartiesCommandClient.cs"),
            Path.Combine(root, "src", "Hexalith.Parties.Client", "AdminPortal", "HttpAdminPortalGdprClient.cs"),
            Path.Combine(root, "src", "Hexalith.Parties.Mcp", "Tools", "PartiesMcpTools.cs"),
            Path.Combine(root, "src", "Hexalith.Parties.Security", "PartyKeyManagementService.cs"),
            Path.Combine(root, "src", "Hexalith.Parties.Security", "TenantKeyRotationService.cs"),
        ];

        foreach (string sourceFile in sourceFiles)
        {
            string source = File.ReadAllText(sourceFile);
            source
                .Contains("UniqueIdHelper.GenerateSortableUniqueStringId()", StringComparison.Ordinal)
                .ShouldBeTrue(Path.GetRelativePath(root, sourceFile));
            source.Contains("Guid.NewGuid", StringComparison.Ordinal).ShouldBeFalse(Path.GetRelativePath(root, sourceFile));
        }
    }

    [Fact]
    public void Repository_DoesNotTrackLanguageServerCacheArtifacts()
    {
        string root = RepositoryRoot.Locate();
        string output = RunGit(root, "ls-files", "*.csproj.lscache", "*.lscache");

        output.Trim().ShouldBeEmpty();
    }
}
