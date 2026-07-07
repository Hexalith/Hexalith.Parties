using System.Diagnostics;
using System.Text.RegularExpressions;
using Shouldly;

namespace Hexalith.Parties.Tests.FitnessTests;

public sealed class PlatformApiPrerequisitesTests
{
    private const string MatrixRelativePath = "_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md";
    private const string SpecRelativePath = "_bmad-output/implementation-artifacts/spec-8-3-platform-api-prerequisites.md";
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

    private static readonly IReadOnlyDictionary<string, string[]> RequiredGapTokensBySurface = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["EventStore degraded response and DAPR health checks"] = ["G1", "G2"],
        ["EventStore projection/query SDK"] = ["G3", "G6", "G10"],
        ["FrontComposer UI primitives"] = ["G4"],
        ["Payload protection engine package"] = ["G5"],
        ["EventStore client envelopes/freshness/error codes"] = ["G6"],
        ["Tenant claims transformation"] = ["G7", "G9"],
        ["Aspire publish helpers"] = ["G8"],
        ["MCP, deep-link, and search probes"] = ["G11"],
        ["Package publishing/source-mode CI"] = ["G12"],
    };

    private static readonly IReadOnlyDictionary<string, string[]> RequiredEvidenceTokensBySurface = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["EventStore domain-service host"] = ["AddEventStoreDomainService", "UseEventStoreDomainService"],
        ["EventStore projection/query SDK"] = ["IDomainProjectionHandler", "IDomainQueryHandler", "IReadModelStore", "IQueryCursorCodec"],
        ["EventStore DataProtection"] = ["AddEventStoreDataProtection", "DaprXmlRepository", "AddEventStoreQueryCursorCodec"],
        ["Payload protection engine package"] = ["IEventPayloadProtectionService", "CryptoShreddingWorkflowState", "PartyPayloadProtectionService", "pdenc-v2", "v1 read support", "IPersonalDataPolicy", "IErasureStateProvider"],
        ["EventStore client envelopes/freshness/error codes"] = ["IEventStoreGatewayClient", "QueryResponseMetadata", "QueryProblemReasonCodes", "GatewayProblemDetailsExtensions"],
        ["Tenant claims transformation"] = ["eventstore:tenant", "AggregateIdentity.IsValid(string)", "UniqueIdHelper.IsValidUlid(string)"],
        ["Aspire publish helpers"] = ["AddEventStoreDomainModule", "WithJwtBearerSecurity", "WithEventStoreJwtAuthentication(audience)", "AddEventStoreGatewayClient"],
        ["FrontComposer UI primitives"] = ["FcAggregateListPage", "FcDestructiveConfirmationDialog", "ProjectionSubscriptionService"],
        ["Commons HTTP helpers"] = ["HttpClientRegistration", "BoundedProblemDetailsReader", "HttpCorrelation"],
        ["MCP, deep-link, and search probes"] = ["FrontComposerMcpDescriptorRegistry", "FrontComposerMcpTool", "tenant header relay"],
        ["Builds shared props/targets"] = ["TreatWarningsAsErrors", "Directory.Packages.props"],
        ["Package publishing/source-mode CI"] = ["Hexalith.Commons.Http", "Hexalith.Commons.ServiceDefaults", "Hexalith.Tenants.Client", "Hexalith.Tenants.Testing"],
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
        ["Hexalith.EventStore.Aspire"] = ["references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/"],
        ["Hexalith.Commons"] = ["references/Hexalith.Commons/"],
        ["Hexalith.FrontComposer"] = ["references/Hexalith.FrontComposer/"],
        ["Hexalith.Builds"] = ["references/Hexalith.Builds/"],
        ["Hexalith.Tenants"] = ["references/Hexalith.Tenants/"],
        ["platform AppHost owners"] = ["references/Hexalith.FrontComposer/"],
    };

    private static readonly IReadOnlyDictionary<string, string[]> ExcludedOwnerPrefixes = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["Hexalith.EventStore"] = ["references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/"],
    };

    private static readonly string[] ForbiddenStoryMigrationPathPrefixes =
    [
        "src/Hexalith.Parties/",
        "src/Hexalith.Parties.",
        "src/Directory.",
    ];

    private static readonly string[] ForbiddenStoryMigrationFiles =
    [
        "Directory.Build.props",
        "Directory.Build.targets",
        "Directory.Packages.props",
        "Directory.Solution.props",
        "Directory.Solution.targets",
        "Hexalith.Parties.slnx",
        "MSBuild.rsp",
        "NuGet.config",
        "global.json",
        "package.json",
        "package-lock.json",
    ];

    private static readonly string[] AllowedContextEvidencePrefixes =
    [
        "_bmad-output/",
        "src/Hexalith.Parties/",
        "src/Hexalith.Parties.Authentication/",
        "src/Hexalith.Parties.Mcp/",
        "src/Hexalith.Parties.Security/",
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
        IReadOnlyDictionary<string, MatrixRow> rows = ReadRows();
        HashSet<string> actualGapIds = [];

        foreach (KeyValuePair<string, string[]> expected in RequiredGapTokensBySurface)
        {
            rows.ContainsKey(expected.Key).ShouldBeTrue(expected.Key);
            string rowText = ToSearchableText(rows[expected.Key]);

            foreach (string gap in expected.Value)
            {
                ContainsExactToken(rowText, gap).ShouldBeTrue($"{expected.Key}: {gap}");
                actualGapIds.Add(gap);
            }
        }

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

            foreach (string ownerSegment in OwnerSegments(row.Owner))
            {
                string[] ownerPrefixes = OwnerPrefixes[ownerSegment];
                string[] excludedPrefixes = ExcludedPrefixes(ownerSegment).ToArray();

                evidencePaths.Any(path =>
                        ownerPrefixes.Any(prefix => IsUnderPathPrefix(path, prefix)) &&
                        !excludedPrefixes.Any(prefix => IsUnderPathPrefix(path, prefix)))
                    .ShouldBeTrue($"{row.Surface} should cite owner evidence for {ownerSegment}.");
            }

            foreach (string evidencePath in evidencePaths)
            {
                bool isOwnerOrContextEvidence =
                    expectedPrefixes.Any(path => IsUnderPathPrefix(evidencePath, path)) ||
                    AllowedContextEvidencePrefixes.Any(path => IsUnderPathPrefix(evidencePath, path));

                isOwnerOrContextEvidence.ShouldBeTrue($"{row.Surface}: {evidencePath}");
            }
        }
    }

    [Fact]
    public void Matrix_AvailableRowsStillRequireReleaseOrSubmoduleProof()
    {
        foreach (MatrixRow row in ReadRows().Values.Where(static row => row.Status == "available"))
        {
            bool explicitlyRequiresReleaseOrPin = Regex.IsMatch(
                row.ProofRequired,
                @"\bmust validate\b.*\b(release|submodule-pin)\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            explicitlyRequiresReleaseOrPin.ShouldBeTrue(row.Surface);
            row.ProofRequired.Contains("no release", StringComparison.OrdinalIgnoreCase).ShouldBeFalse(row.Surface);
            row.ProofRequired.Contains("without release", StringComparison.OrdinalIgnoreCase).ShouldBeFalse(row.Surface);
            row.ProofRequired.Contains("without submodule-pin", StringComparison.OrdinalIgnoreCase).ShouldBeFalse(row.Surface);
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
            Regex.IsMatch(row.Decision, @"\bKeep\b.*\brollback path\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                .ShouldBeTrue(row.Surface);
            row.Decision.Contains("remove rollback", StringComparison.OrdinalIgnoreCase).ShouldBeFalse(row.Surface);
            row.Decision.Contains("without rollback", StringComparison.OrdinalIgnoreCase).ShouldBeFalse(row.Surface);
        }
    }

    [Fact]
    public void Matrix_AllRowsNameProofAndRollbackPath()
    {
        foreach (MatrixRow row in ReadRows().Values)
        {
            row.ProofRequired.ShouldContain("Proof required:");
            Regex.IsMatch(row.Decision, @"\bKeep\b.*\brollback path\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                .ShouldBeTrue(row.Surface);
        }
    }

    [Fact]
    public void Matrix_ValidationEvidenceNamesExpectedSymbols()
    {
        IReadOnlyDictionary<string, MatrixRow> rows = ReadRows();

        foreach (KeyValuePair<string, string[]> expected in RequiredEvidenceTokensBySurface)
        {
            rows.ContainsKey(expected.Key).ShouldBeTrue(expected.Key);
            string validationEvidence = rows[expected.Key].ValidationEvidence;

            foreach (string token in expected.Value)
            {
                validationEvidence.Contains(token, StringComparison.Ordinal).ShouldBeTrue($"{expected.Key}: {token}");
            }

            validationEvidence.Contains("Inspected", StringComparison.OrdinalIgnoreCase).ShouldBeFalse(expected.Key);
        }
    }

    [Fact]
    public void Matrix_ValidationEvidenceCommandsAreReproducible()
    {
        string root = RepositoryRoot.Locate();

        foreach (MatrixRow row in ReadRows().Values)
        {
            (string Pattern, string[] Paths)[] commands = ExtractRgCommands(row.ValidationEvidence).ToArray();
            commands.ShouldNotBeEmpty(row.Surface);

            foreach ((string pattern, string[] paths) in commands)
            {
                RunRg(root, pattern, paths);
            }
        }
    }

    [Fact]
    public void CurrentStoryDiff_DoesNotModifyProductionMigrationPaths()
    {
        string root = RepositoryRoot.Locate();
        string spec = File.ReadAllText(Path.Combine(root, SpecRelativePath));
        string baselineRevision = ReadFrontmatterValue(spec, "baseline_revision");

        if (string.Equals(baselineRevision, "NO_VCS", StringComparison.Ordinal))
        {
            return;
        }

        GitObjectExists(root, baselineRevision).ShouldBeTrue(baselineRevision);

        string diffNames = RunGit(root, "diff", "--name-only", baselineRevision, "--");
        string untrackedNames = RunGit(root, "ls-files", "--others", "--exclude-standard");
        string[] forbiddenChanges = string.Concat(diffNames, "\n", untrackedNames)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(IsForbiddenStoryMigrationPath)
            .ToArray();

        forbiddenChanges.ShouldBeEmpty();
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
        Regex.Matches(text, Regex.Escape(StartMarker), RegexOptions.CultureInvariant)
            .Count
            .ShouldBe(1, StartMarker);
        Regex.Matches(text, Regex.Escape(EndMarker), RegexOptions.CultureInvariant)
            .Count
            .ShouldBe(1, EndMarker);

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
        dependentStories.SetEquals(required.Value).ShouldBeTrue(row.Surface);
        foreach (string expectedStory in required.Value)
        {
            dependentStories.Contains(expectedStory).ShouldBeTrue(row.Surface);
        }
    }

    private static HashSet<string> ParseStoryIds(string dependentStories)
        => Regex.Matches(
                dependentStories,
                @"(?<![A-Za-z0-9])8\.(?:[4-9]|10)(?![A-Za-z0-9])",
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
        => OwnerSegments(owner).SelectMany(segment => OwnerPrefixes[segment]);

    private static IEnumerable<string> OwnerSegments(string owner)
    {
        foreach (string segment in owner.Split(" and ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            OwnerPrefixes.ContainsKey(segment).ShouldBeTrue($"Unknown owner segment: {segment}");
            yield return segment;
        }
    }

    private static IEnumerable<string> ExcludedPrefixes(string ownerSegment)
        => ExcludedOwnerPrefixes.TryGetValue(ownerSegment, out string[]? prefixes)
            ? prefixes
            : [];

    private static bool IsUnderPathPrefix(string path, string prefix)
    {
        string normalizedPrefix = prefix.TrimEnd('/');
        return string.Equals(path, normalizedPrefix, StringComparison.Ordinal) ||
            path.StartsWith(normalizedPrefix + "/", StringComparison.Ordinal);
    }

    private static bool IsForbiddenStoryMigrationPath(string path)
        => ForbiddenStoryMigrationFiles.Contains(path, StringComparer.Ordinal) ||
            ForbiddenStoryMigrationPathPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.Ordinal));

    private static bool ContainsExactToken(string value, string token)
        => Regex.IsMatch(
            value,
            $@"(?<![A-Za-z0-9]){Regex.Escape(token)}(?![A-Za-z0-9])",
            RegexOptions.CultureInvariant);

    private static IEnumerable<(string Pattern, string[] Paths)> ExtractRgCommands(string validationEvidence)
    {
        foreach (Match match in Regex.Matches(
            validationEvidence,
            @"`rg\s+-n\s+-F\s+'(?<pattern>[^']+)'\s+(?<paths>[^`]+)`",
            RegexOptions.CultureInvariant))
        {
            string[] paths = match.Groups["paths"].Value
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            yield return (match.Groups["pattern"].Value, paths);
        }
    }

    private static void RunRg(string root, string pattern, string[] paths)
    {
        using var process = CreateProcess(root, "rg");
        process.StartInfo.ArgumentList.Add("-n");
        process.StartInfo.ArgumentList.Add("-F");
        process.StartInfo.ArgumentList.Add(pattern);

        foreach (string path in paths)
        {
            process.StartInfo.ArgumentList.Add(path);
        }

        process.Start().ShouldBeTrue();
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        process.ExitCode.ShouldBe(0, $"rg -n -F '{pattern}' {string.Join(' ', paths)}{Environment.NewLine}{output}{error}");
    }

    private static string ToSearchableText(MatrixRow row)
        => string.Join(
            " ",
            row.Surface,
            row.Owner,
            row.Status,
            row.EvidencePaths,
            row.ProofRequired,
            row.DependentStories,
            row.Decision,
            row.ValidationEvidence);

    private static string ReadFrontmatterValue(string markdown, string key)
    {
        Match match = Regex.Match(
            markdown,
            $@"(?m)^{Regex.Escape(key)}:\s*['""]?(?<value>[^'""\r\n]+)['""]?\s*$",
            RegexOptions.CultureInvariant);

        match.Success.ShouldBeTrue(key);
        return match.Groups["value"].Value.Trim();
    }

    private static bool GitObjectExists(string root, string revision)
    {
        using var process = CreateGitProcess(root, "cat-file", "-e", $"{revision}^{{commit}}");

        process.Start().ShouldBeTrue();
        process.WaitForExit();

        return process.ExitCode == 0;
    }

    private static string RunGit(string root, params string[] arguments)
    {
        using var process = CreateGitProcess(root, arguments);

        process.Start().ShouldBeTrue();
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        process.ExitCode.ShouldBe(0, error);
        return output;
    }

    private static Process CreateGitProcess(string root, params string[] arguments)
    {
        Process process = CreateProcess(root, "git");

        foreach (string argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        return process;
    }

    private static Process CreateProcess(string root, string fileName)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo(fileName)
            {
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };

        return process;
    }
}
