using System.Text.RegularExpressions;
using Shouldly;

namespace Hexalith.Parties.Tests.FitnessTests;

public sealed class PlatformApiPrerequisitesTests
{
    private const string MatrixRelativePath = "_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md";
    private const string StartMarker = "<!-- platform-api-prerequisite-matrix:start -->";
    private const string EndMarker = "<!-- platform-api-prerequisite-matrix:end -->";

    private static readonly IReadOnlyDictionary<string, string[]> RequiredRows = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["EventStore domain-service host"] = ["8.4", "8.5", "8.10"],
        ["EventStore projection/query SDK"] = ["8.6", "8.10"],
        ["EventStore DataProtection"] = ["8.6", "8.10"],
        ["EventStore client envelopes/freshness/error codes"] = ["8.6", "8.8", "8.9", "8.10"],
        ["Tenant claims transformation"] = ["8.4", "8.8", "8.10"],
        ["Aspire publish helpers"] = ["8.5", "8.8", "8.10"],
        ["FrontComposer UI primitives"] = ["8.8", "8.9", "8.10"],
        ["Commons HTTP helpers"] = ["8.8", "8.10"],
        ["Builds shared props/targets"] = ["8.8", "8.10"],
    };

    private static readonly IReadOnlyDictionary<string, string[]> RequiredGapRows = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["EventStore degraded response and DAPR health checks"] = ["8.5", "8.8", "8.10"],
        ["Payload protection engine package"] = ["8.7", "8.10"],
        ["MCP, deep-link, and search probes"] = ["8.8", "8.10"],
        ["Package publishing/source-mode CI"] = ["8.8", "8.10"],
    };

    private static readonly HashSet<string> ApprovedStatuses = new(StringComparer.Ordinal)
    {
        "available",
        "needs-additive-api",
        "blocked",
    };

    private static readonly IReadOnlyDictionary<string, string[]> OwnerPrefixes = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["Hexalith.EventStore"] = ["references/Hexalith.EventStore/"],
        ["Hexalith.EventStore.Aspire"] = ["references/Hexalith.EventStore/"],
        ["Hexalith.Commons"] = ["references/Hexalith.Commons/"],
        ["Hexalith.FrontComposer"] = ["references/Hexalith.FrontComposer/"],
        ["Hexalith.Builds"] = ["references/Hexalith.Builds/"],
        ["Hexalith.Tenants"] = ["references/Hexalith.Tenants/"],
        ["platform AppHost owners"] = ["references/Hexalith.FrontComposer/"],
        ["Platform release owners"] = ["_bmad-output/", "references/Hexalith.Builds/"],
    };

    private static readonly string[] AllowedContextEvidencePrefixes =
    [
        "_bmad-output/",
        "src/Hexalith.Parties",
        "references/Hexalith.Tenants/",
    ];

    [Fact]
    public void Matrix_ContainsRequiredRowsAndRequiredGapRows()
    {
        IReadOnlyDictionary<string, MatrixRow> rows = ReadRows();

        rows.Count.ShouldBeGreaterThanOrEqualTo(RequiredRows.Count + RequiredGapRows.Count);
        foreach (KeyValuePair<string, string[]> required in RequiredRows)
        {
            AssertRequiredRow(rows, required);
        }

        foreach (KeyValuePair<string, string[]> required in RequiredGapRows)
        {
            AssertRequiredRow(rows, required);
        }

        foreach (MatrixRow row in rows.Values)
        {
            ParseStoryIds(row.DependentStories).ShouldNotBeEmpty(row.Surface);
        }
    }

    [Fact]
    public void Matrix_CoversKnownFablePlatformGaps()
    {
        string text = ReadMatrix();
        HashSet<string> actualGapIds = Regex.Matches(
                text,
                @"(?<![A-Za-z0-9])G(?:[1-9]|1[0-2])(?![A-Za-z0-9])",
                RegexOptions.CultureInvariant)
            .Select(static match => match.Value)
            .ToHashSet(StringComparer.Ordinal);

        foreach (string gap in Enumerable.Range(1, 12).Select(static number => $"G{number}"))
        {
            actualGapIds.Contains(gap).ShouldBeTrue(gap);
        }
    }

    [Fact]
    public void Matrix_StatusesUseApprovedVocabulary()
    {
        foreach (MatrixRow row in ReadRows().Values)
        {
            ApprovedStatuses.Contains(row.Status).ShouldBeTrue($"{row.Surface}: {row.Status}");
        }
    }

    [Fact]
    public void Matrix_EvidencePathsExistAndMatchDeclaredOwner()
    {
        string root = RepositoryRoot.Locate();
        string normalizedRoot = Path.GetFullPath(root);

        foreach (MatrixRow row in ReadRows().Values)
        {
            string[] evidencePaths = SplitEvidencePaths(row.EvidencePaths)
                .Select(path => NormalizeEvidencePath(normalizedRoot, path))
                .ToArray();
            evidencePaths.ShouldNotBeEmpty(row.Surface);

            foreach (string evidencePath in evidencePaths)
            {
                string fullPath = Path.Combine(normalizedRoot, evidencePath);
                (File.Exists(fullPath) || Directory.Exists(fullPath)).ShouldBeTrue(evidencePath);
            }

            string[] expectedPrefixes = ExpectedOwnerPrefixes(row.Owner).ToArray();
            expectedPrefixes.ShouldNotBeEmpty($"{row.Surface}: {row.Owner}");

            foreach (string expectedPrefix in expectedPrefixes)
            {
                evidencePaths.Any(path => path.StartsWith(expectedPrefix, StringComparison.Ordinal))
                    .ShouldBeTrue($"{row.Surface} should cite owner evidence under {expectedPrefix}.");
            }

            foreach (string evidencePath in evidencePaths)
            {
                bool isOwnerOrContextEvidence =
                    expectedPrefixes.Any(path => evidencePath.StartsWith(path, StringComparison.Ordinal)) ||
                    AllowedContextEvidencePrefixes.Any(path => evidencePath.StartsWith(path, StringComparison.Ordinal));

                isOwnerOrContextEvidence.ShouldBeTrue($"{row.Surface}: {evidencePath}");
            }
        }
    }

    [Fact]
    public void Matrix_AvailableRowsStillRequireReleaseOrSubmoduleProof()
    {
        foreach (MatrixRow row in ReadRows().Values.Where(static row => row.Status == "available"))
        {
            bool mentionsReleaseOrPin =
                row.ProofRequired.Contains("release", StringComparison.OrdinalIgnoreCase) ||
                row.ProofRequired.Contains("submodule-pin", StringComparison.OrdinalIgnoreCase);

            mentionsReleaseOrPin.ShouldBeTrue(row.Surface);
        }
    }

    [Fact]
    public void Matrix_KeepsNoMigrationGateAndResidualBlockers()
    {
        string text = ReadMatrix();

        text.ShouldContain("No Parties source migration starts in Story 8.3.");
        text.ShouldContain("No production source migration was performed.");
        text.ShouldContain("Do not edit submodules or production source for Story 8.3.");
        text.ShouldContain("A checked-out submodule source file is not sufficient by itself.");
        text.ShouldContain("A row status of `available` means source evidence exists; it is not enough by itself.");
        text.ShouldContain("Release builds must use NuGet package references for external Hexalith libraries. Remove -p:UseHexalithProjectReferences=true or build Debug for source-debugging.");
        text.ShouldContain("five pre-existing tenant-event failures");

        foreach (MatrixRow row in ReadRows().Values)
        {
            row.Decision.ShouldContain("No Parties source migration starts in Story 8.3.");
        }
    }

    [Fact]
    public void Matrix_AllRowsNameProofAndRollbackPath()
    {
        foreach (MatrixRow row in ReadRows().Values)
        {
            row.ProofRequired.ShouldContain("Proof required:");
            row.Decision.Contains("rollback", StringComparison.OrdinalIgnoreCase).ShouldBeTrue(row.Surface);
        }
    }

    private static string ReadMatrix()
        => File.ReadAllText(Path.Combine(RepositoryRoot.Locate(), MatrixRelativePath));

    private static IReadOnlyDictionary<string, MatrixRow> ReadRows()
    {
        string section = ReadMarkedSection(ReadMatrix());
        Dictionary<string, MatrixRow> rows = new(StringComparer.Ordinal);

        foreach (string line in section.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = line.Trim();
            if (!trimmed.StartsWith('|'))
            {
                continue;
            }

            string[] cells = SplitMarkdownTableRow(trimmed);
            if (IsMarkdownSeparatorRow(cells))
            {
                continue;
            }

            if (cells.Length == 8 && string.Equals(cells[0], "Surface", StringComparison.Ordinal))
            {
                continue;
            }

            cells.Length.ShouldBe(8, trimmed);

            MatrixRow row = new(
                cells[0],
                cells[1],
                cells[2],
                cells[3],
                cells[4],
                cells[5],
                cells[6],
                cells[7]);

            rows.TryAdd(row.Surface, row).ShouldBeTrue(row.Surface);
        }

        return rows;
    }

    private static string ReadMarkedSection(string text)
    {
        int start = text.IndexOf(StartMarker, StringComparison.Ordinal);
        if (start < 0)
        {
            throw new InvalidOperationException($"Matrix start marker '{StartMarker}' was not found.");
        }

        int end = text.IndexOf(EndMarker, start, StringComparison.Ordinal);
        if (end < 0)
        {
            throw new InvalidOperationException($"Matrix end marker '{EndMarker}' was not found.");
        }

        return text[(start + StartMarker.Length)..end];
    }

    private static string[] SplitMarkdownTableRow(string row)
    {
        List<string> cells = [];
        int startIndex = row.StartsWith('|') ? 1 : 0;
        int endIndex = row.EndsWith('|') ? row.Length - 1 : row.Length;
        var current = new System.Text.StringBuilder();
        bool escaped = false;

        for (int i = startIndex; i < endIndex; i++)
        {
            char currentChar = row[i];
            if (escaped)
            {
                _ = current.Append(currentChar);
                escaped = false;
                continue;
            }

            if (currentChar == '\\')
            {
                escaped = true;
                continue;
            }

            if (currentChar == '|')
            {
                cells.Add(current.ToString().Trim());
                _ = current.Clear();
                continue;
            }

            _ = current.Append(currentChar);
        }

        if (escaped)
        {
            _ = current.Append('\\');
        }

        cells.Add(current.ToString().Trim());
        return [.. cells];
    }

    private static string[] SplitEvidencePaths(string evidencePaths)
        => evidencePaths
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(static path => path.Trim())
            .Where(static path => path.Length > 0)
            .ToArray();

    private static void AssertRequiredRow(
        IReadOnlyDictionary<string, MatrixRow> rows,
        KeyValuePair<string, string[]> required)
    {
        rows.ContainsKey(required.Key).ShouldBeTrue(required.Key);

        MatrixRow row = rows[required.Key];
        row.Owner.ShouldNotBeNullOrWhiteSpace();
        row.ProofRequired.ShouldNotBeNullOrWhiteSpace();
        row.ValidationEvidence.ShouldNotBeNullOrWhiteSpace();
        row.Decision.ShouldContain("No Parties source migration starts in Story 8.3.");

        HashSet<string> dependentStories = ParseStoryIds(row.DependentStories);
        foreach (string expectedStory in required.Value)
        {
            dependentStories.Contains(expectedStory).ShouldBeTrue(row.Surface);
        }
    }

    private static HashSet<string> ParseStoryIds(string dependentStories)
        => Regex.Matches(
                dependentStories,
                @"(?<![0-9])8\.(?:[4-9]|10)(?![0-9])",
                RegexOptions.CultureInvariant)
            .Select(static match => match.Value)
            .ToHashSet(StringComparer.Ordinal);

    private static bool IsMarkdownSeparatorRow(string[] cells)
        => cells.All(static cell =>
            cell.Length > 0 &&
            cell.All(static character => character is '-' or ':' or ' '));

    private static string NormalizeEvidencePath(string repositoryRoot, string evidencePath)
    {
        Path.IsPathFullyQualified(evidencePath).ShouldBeFalse(evidencePath);
        evidencePath.Contains("..", StringComparison.Ordinal).ShouldBeFalse(evidencePath);

        string fullPath = Path.GetFullPath(Path.Combine(repositoryRoot, evidencePath));
        string rootWithSeparator = repositoryRoot.EndsWith(Path.DirectorySeparatorChar)
            ? repositoryRoot
            : repositoryRoot + Path.DirectorySeparatorChar;

        fullPath.StartsWith(rootWithSeparator, StringComparison.Ordinal).ShouldBeTrue(evidencePath);

        return Path.GetRelativePath(repositoryRoot, fullPath)
            .Replace(Path.DirectorySeparatorChar, '/');
    }

    private static IEnumerable<string> ExpectedOwnerPrefixes(string owner)
    {
        foreach (string segment in owner.Split(" and ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            OwnerPrefixes.TryGetValue(segment, out string[]? prefixes).ShouldBeTrue($"Unknown owner segment: {segment}");

            foreach (string prefix in prefixes)
            {
                yield return prefix;
            }
        }
    }
}
