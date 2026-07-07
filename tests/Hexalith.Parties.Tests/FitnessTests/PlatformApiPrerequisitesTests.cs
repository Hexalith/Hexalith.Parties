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

    private static readonly HashSet<string> ApprovedStatuses = new(StringComparer.Ordinal)
    {
        "available",
        "needs-additive-api",
        "blocked",
    };

    [Fact]
    public void Matrix_ContainsRequiredRowsAndAllowsAdditionalGapRows()
    {
        IReadOnlyDictionary<string, MatrixRow> rows = ReadRows();

        rows.Count.ShouldBeGreaterThanOrEqualTo(RequiredRows.Count);
        foreach (KeyValuePair<string, string[]> required in RequiredRows)
        {
            rows.ContainsKey(required.Key).ShouldBeTrue(required.Key);

            MatrixRow row = rows[required.Key];
            row.Owner.ShouldNotBeNullOrWhiteSpace();
            row.ProofRequired.ShouldNotBeNullOrWhiteSpace();
            row.ValidationEvidence.ShouldNotBeNullOrWhiteSpace();
            row.Decision.ShouldContain("No Parties source migration starts in Story 8.3.");

            foreach (string expectedStory in required.Value)
            {
                row.DependentStories.Contains(expectedStory, StringComparison.Ordinal).ShouldBeTrue(row.Surface);
            }
        }
    }

    [Fact]
    public void Matrix_CoversKnownFablePlatformGaps()
    {
        string text = ReadMatrix();

        foreach (string gap in Enumerable.Range(1, 12).Select(static number => $"G{number}"))
        {
            text.ShouldContain(gap);
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

        foreach (MatrixRow row in ReadRows().Values)
        {
            string[] evidencePaths = SplitEvidencePaths(row.EvidencePaths);
            evidencePaths.ShouldNotBeEmpty(row.Surface);

            foreach (string evidencePath in evidencePaths)
            {
                string fullPath = Path.Combine(root, evidencePath);
                (File.Exists(fullPath) || Directory.Exists(fullPath)).ShouldBeTrue(evidencePath);
            }

            foreach (string expectedPrefix in ExpectedOwnerPrefixes(row.Owner))
            {
                evidencePaths.Any(path => path.StartsWith(expectedPrefix, StringComparison.Ordinal))
                    .ShouldBeTrue($"{row.Surface} should cite owner evidence under {expectedPrefix}.");
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
                row.ProofRequired.Contains("submodule-pin", StringComparison.OrdinalIgnoreCase) ||
                row.ProofRequired.Contains("package/source", StringComparison.OrdinalIgnoreCase);

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
    public void Matrix_NonAvailableRowsNameProofAndRollbackPath()
    {
        foreach (MatrixRow row in ReadRows().Values.Where(static row => row.Status != "available"))
        {
            row.ProofRequired.ShouldContain("Proof required:");
            row.Decision.Contains("Keep local rollback path:", StringComparison.Ordinal).ShouldBeTrue(row.Surface);
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
            if (!trimmed.StartsWith('|') || trimmed.Contains("---", StringComparison.Ordinal))
            {
                continue;
            }

            string[] cells = SplitMarkdownTableRow(trimmed);
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

    private static IEnumerable<string> ExpectedOwnerPrefixes(string owner)
    {
        if (owner.Contains("Hexalith.EventStore", StringComparison.Ordinal))
        {
            yield return "references/Hexalith.EventStore/";
        }

        if (owner.Contains("Hexalith.Commons", StringComparison.Ordinal))
        {
            yield return "references/Hexalith.Commons/";
        }

        if (owner.Contains("Hexalith.FrontComposer", StringComparison.Ordinal))
        {
            yield return "references/Hexalith.FrontComposer/";
        }

        if (owner.Contains("Hexalith.Builds", StringComparison.Ordinal))
        {
            yield return "references/Hexalith.Builds/";
        }

        if (owner.Contains("Hexalith.Tenants", StringComparison.Ordinal))
        {
            yield return "references/Hexalith.Tenants/";
        }

        if (owner.Contains("Platform release owners", StringComparison.Ordinal))
        {
            yield return "_bmad-output/";
        }
    }

    private sealed record MatrixRow(
        string Surface,
        string Owner,
        string Status,
        string EvidencePaths,
        string ProofRequired,
        string DependentStories,
        string Decision,
        string ValidationEvidence);
}
