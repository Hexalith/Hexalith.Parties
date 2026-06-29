using System.Text.RegularExpressions;

using Shouldly;

namespace Hexalith.Parties.Contracts.Tests.Authorization;

public sealed class PartiesClaimLiteralConsolidationTests
{
    private static readonly Regex RawClaimLiteralPattern = new("\"(?:eventstore:tenant|party_id|sub|oid)\"", RegexOptions.Compiled);

    [Fact]
    public void RawClaimLiterals_AreOnlyDeclaredInSharedAnchorOrIntentionalTests()
    {
        string root = FindRepositoryRoot();
        string[] allowedSuffixes =
        [
            Normalize("src/Hexalith.Parties.Contracts/Authorization/PartiesClaimTypes.cs"),
            Normalize("tests/Hexalith.Parties.Contracts.Tests/Authorization/PartiesClaimLiteralConsolidationTests.cs"),
            Normalize("tests/Hexalith.Parties.IntegrationTests/Topology/ConsumerPartyIdBindingRealmTests.cs"),
        ];

        string[] offenders = new[] { "src", "tests" }
            .SelectMany(directory => Directory.EnumerateFiles(Path.Combine(root, directory), "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(path => RawClaimLiteralPattern.Matches(File.ReadAllText(path))
                .Select(match => Normalize(Path.GetRelativePath(root, path))))
            .Where(relativePath => !allowedSuffixes.Contains(relativePath, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        offenders.ShouldBeEmpty("Claim wire literals must flow through PartiesClaimTypes outside intentional wire-value tests.");
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Hexalith.Parties.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }

    private static string Normalize(string path)
        => path.Replace('\\', '/');
}
