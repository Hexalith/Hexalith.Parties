using System.Diagnostics;
using System.Text.RegularExpressions;

using Shouldly;

namespace Hexalith.Parties.Tests.FitnessTests;

public sealed class EpicEightClosureFitnessTests
{
    private const string BaselineCommit = "37f4ec826c6f4aea4651cfbad94fb6ab7fc4f0a0";
    private const string DeferredWorkPath = "_bmad-output/implementation-artifacts/deferred-work.md";
    private const string EpicsPath = "_bmad-output/planning-artifacts/epics.md";
    private const string PrdPath = "_bmad-output/planning-artifacts/parties-ui-prd.md";
    private const string SpecPath = "_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md";
    private const string SpinePath = "_bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md";
    private const string SprintStatusPath = "_bmad-output/implementation-artifacts/sprint-status.yaml";
    private const string Story86Path = "_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md";
    private const string TestSummaryPath = "_bmad-output/implementation-artifacts/tests/test-summary.md";

    private static readonly string[] ExpectedDeferrals =
    [
        "8.6-residual-review-debt",
        "8.7-data-protection-extraction",
        "8.8-runtime-boundary-cleanup",
        "8.9-frontcomposer-ui-consolidation",
        "external-runtime-deployment",
    ];

    [Fact]
    public void AcceptedDeferralsAreCompleteAndKeepIncompleteStoriesHonest()
    {
        string root = RepositoryRoot.Locate();
        ClosureDeferral[] deferrals = ParseDeferrals(Read(root, DeferredWorkPath));

        deferrals.Select(static item => item.Id).ShouldBe(ExpectedDeferrals);
        foreach (ClosureDeferral deferral in deferrals)
        {
            DescribeDeferralGaps(deferral).ShouldBeEmpty(deferral.Id);
            DescribeEvidenceAnchorGaps(root, deferral.Evidence).ShouldBeEmpty(deferral.Id);
            deferral.Status.ShouldBe("accepted");
        }

        string sprintStatus = Read(root, SprintStatusPath);
        ReadYamlStatus(sprintStatus, "8-6-projection-and-query-sdk-migration").ShouldBe("done");
        Regex.Matches(
                Read(root, Story86Path),
                @"(?m)^\s*- \[ \] \[Review\]\[Defer\]",
                RegexOptions.CultureInvariant)
            .ShouldNotBeEmpty("8.6 has explicit residual review debt, so its accepted umbrella deferral must remain present.");
        ReadYamlStatus(sprintStatus, "8-7-data-protection-extraction").ShouldBe("blocked");
        ReadYamlStatus(sprintStatus, "8-8-client-mcp-apphost-build-and-deploy-cleanup").ShouldBe("blocked");
        ReadYamlStatus(sprintStatus, "8-9-ui-frontcomposer-and-fluent-consolidation").ShouldBe("backlog");
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("exit_proof")]
    [InlineData("rollback")]
    [InlineData("evidence")]
    public void IncompleteDeferralFailsClosed(string missingField)
    {
        var complete = new ClosureDeferral("example", "accepted", "owner", "proof", "rollback", "evidence");
        ClosureDeferral incomplete = missingField switch
        {
            "owner" => complete with { Owner = string.Empty },
            "exit_proof" => complete with { ExitProof = string.Empty },
            "rollback" => complete with { Rollback = string.Empty },
            "evidence" => complete with { Evidence = string.Empty },
            _ => throw new InvalidOperationException(missingField),
        };

        DescribeDeferralGaps(incomplete).ShouldContain(missingField);
    }

    [Fact]
    public void DeferralParserPreservesReorderedAndMissingFieldsForSpecificDiagnostics()
    {
        const string markdown = """
            ## Story 8.10 accepted Epic 8 closure deferrals

            - deferral_id: `example`
              evidence: `_bmad-output/implementation-artifacts/deferred-work.md`
              rollback: `rollback`
              status: accepted
              source_spec: `spec.md`
              exit_proof: `proof`
            """;

        ClosureDeferral[] deferrals = ParseDeferrals(markdown);
        deferrals.ShouldHaveSingleItem();
        ClosureDeferral deferral = deferrals[0];
        deferral.Evidence.ShouldBe("_bmad-output/implementation-artifacts/deferred-work.md");
        DescribeDeferralGaps(deferral).ShouldBe(["owner"]);
    }

    [Fact]
    public void DeferralEvidenceWithoutAnExistingRepositoryAnchorFailsClosed()
    {
        string root = RepositoryRoot.Locate();

        DescribeEvidenceAnchorGaps(root, "unsupported prose only")
            .ShouldContain("evidence_anchor");
        DescribeEvidenceAnchorGaps(root, "docs/does-not-exist.md")
            .ShouldContain("evidence_path:docs/does-not-exist.md");
    }

    [Fact]
    public void InvariantMapCoversI1ThroughI15WithExecutableOrDeferredEvidence()
    {
        string root = RepositoryRoot.Locate();
        string spine = Read(root, SpinePath);
        string marked = ReadMarkedSection(spine, "epic-8-invariant-map");
        Dictionary<string, (string Disposition, string Evidence)> rows = ParseInvariantRows(marked);
        string[] expected = ["I1", "I1a", .. Enumerable.Range(2, 14).Select(static number => $"I{number}")];
        HashSet<string> testClasses = Directory.GetFiles(Path.Combine(root, "tests"), "*.cs", SearchOption.AllDirectories)
            .SelectMany(static path => Regex.Matches(
                File.ReadAllText(path),
                @"\bclass\s+(?<name>[A-Za-z][A-Za-z0-9_]*Tests)\b",
                RegexOptions.CultureInvariant))
            .Select(static match => match.Groups["name"].Value)
            .ToHashSet(StringComparer.Ordinal);

        rows.Keys.Order(StringComparer.Ordinal).ShouldBe(expected.Order(StringComparer.Ordinal));
        foreach (KeyValuePair<string, (string Disposition, string Evidence)> row in rows)
        {
            row.Value.Disposition.ShouldNotBeNullOrWhiteSpace(row.Key);
            row.Value.Evidence.ShouldNotBeNullOrWhiteSpace(row.Key);
            Regex.IsMatch(row.Value.Disposition, @"\b(Executable|Deferred)\b", RegexOptions.CultureInvariant)
                .ShouldBeTrue(row.Key);

            if (row.Value.Disposition.Contains("Executable", StringComparison.Ordinal))
            {
                string[] namedTests = Regex.Matches(
                        row.Value.Evidence,
                        @"`(?<name>[A-Za-z][A-Za-z0-9_]*Tests)`",
                        RegexOptions.CultureInvariant)
                    .Select(static match => match.Groups["name"].Value)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                namedTests.ShouldNotBeEmpty($"{row.Key} must name executable test classes.");
                namedTests.Where(test => !testClasses.Contains(test)).ShouldBeEmpty(row.Key);
            }

            if (row.Value.Disposition.Contains("Deferred", StringComparison.Ordinal))
            {
                ExpectedDeferrals.Where(deferral => row.Value.Evidence.Contains(deferral, StringComparison.Ordinal))
                    .ShouldNotBeEmpty($"{row.Key} must name an accepted deferral.");
            }
        }

        string combinedEvidence = string.Join('\n', rows.Values.Select(static value => value.Evidence));
        ExpectedDeferrals.Where(deferral => !combinedEvidence.Contains(deferral, StringComparison.Ordinal))
            .ShouldBeEmpty();
    }

    [Fact]
    public void EpicEightAddsNoPrdFunctionalRequirement()
    {
        string root = RepositoryRoot.Locate();
        string spec = Read(root, SpecPath);
        string spine = Read(root, SpinePath);

        spec.ShouldContain("Add no PRD functional requirement.");
        spine.ShouldContain("zero new PRD FRs");
        spine.ShouldContain("Epic 8 adds **zero** PRD functional requirements");

        string[] scopeArtifacts = [PrdPath, EpicsPath];
        if (TryRunGit(root, out _, "cat-file", "-e", $"{BaselineCommit}^{{commit}}"))
        {
            SplitLines(RunGit(root, ["diff", "--name-only", BaselineCommit, "--", EpicsPath]))
                .ShouldBeEmpty("Epic 8 cannot change the epic/FR inventory.");

            // The PRD accepts governed non-functional corrections (currency metadata,
            // traceability, wording — e.g. the 2026-08-18 validation-driven correction),
            // but its FR/NFR requirement inventory is frozen at the baseline: Epic 8
            // adds zero PRD functional requirements.
            ExtractRequirementIds(Read(root, PrdPath)).ShouldBe(
                ExtractRequirementIds(RunGit(root, ["show", $"{BaselineCommit}:{PrdPath}"])),
                "Epic 8 cannot add, remove, or reorder canonical PRD requirements.");
        }

        SplitLines(RunGit(root, ["ls-files", "--others", "--exclude-standard", "--", .. scopeArtifacts]))
            .ShouldBeEmpty("Untracked canonical scope artifacts also fail the zero-PRD gate.");
    }

    [Fact]
    public void ClosureStatusCannotBeDoneBeforeEveryTaskAndValidationReceiptExists()
    {
        string root = RepositoryRoot.Locate();
        string spec = Read(root, SpecPath);
        string sprintStatus = Read(root, SprintStatusPath);
        string storyStatus = ReadFrontmatterValue(spec, "status");
        string trackedStoryStatus = ReadYamlStatus(sprintStatus, "8-10-final-readiness-documentation-and-retirement-gate");
        string epicStatus = ReadYamlStatus(sprintStatus, "epic-8");

        string normalizedStoryStatus = NormalizeStoryStatus(storyStatus);
        string normalizedTrackedStatus = NormalizeStoryStatus(trackedStoryStatus);
        normalizedStoryStatus.ShouldBe(normalizedTrackedStatus);
        if (!string.Equals(normalizedStoryStatus, "done", StringComparison.Ordinal))
        {
            epicStatus.ShouldBe("in-progress");
            return;
        }

        Regex.Matches(spec, @"(?m)^\s*- \[ \]", RegexOptions.CultureInvariant).ShouldBeEmpty();
        epicStatus.ShouldBe("done");
        string summary = Read(root, TestSummaryPath);
        summary.ShouldContain("Story 8.10 Final Readiness, Documentation, and Retirement Gate");
        Dictionary<string, string> receipts = ParseValidationReceipts(summary);
        receipts.ContainsKey("Release solution build").ShouldBeTrue();
        receipts.ContainsKey("Playwright accessibility").ShouldBeTrue();
        DescribeReceiptGaps(receipts).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("ready-ish")]
    public void UnknownStoryStatusFailsClosed(string status)
        => Should.Throw<ShouldAssertException>(() => NormalizeStoryStatus(status));

    [Fact]
    public void BlockedOrFailedValidationReceiptFailsClosed()
    {
        var receipts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Release solution build"] = "**Blocked**",
            ["Playwright accessibility"] = "Failed",
        };

        DescribeReceiptGaps(receipts).ShouldBe(["Release solution build:**Blocked**", "Playwright accessibility:Failed"]);
    }

    private static string[] DescribeDeferralGaps(ClosureDeferral deferral)
    {
        List<string> gaps = [];
        if (string.IsNullOrWhiteSpace(deferral.Owner))
        {
            gaps.Add("owner");
        }

        if (string.IsNullOrWhiteSpace(deferral.ExitProof))
        {
            gaps.Add("exit_proof");
        }

        if (string.IsNullOrWhiteSpace(deferral.Rollback))
        {
            gaps.Add("rollback");
        }

        if (string.IsNullOrWhiteSpace(deferral.Evidence))
        {
            gaps.Add("evidence");
        }

        return [.. gaps];
    }

    private static string[] DescribeEvidenceAnchorGaps(string root, string evidence)
    {
        string[] anchors = Regex.Matches(
                evidence,
                @"(?<path>(?:_bmad-output|docs|src|tests|scripts|references)/[^\s;,`]+\.(?:md|ya?ml|cs|csproj|ps1|py|sh))",
                RegexOptions.CultureInvariant)
            .Select(static match => match.Groups["path"].Value.TrimEnd('.', ':'))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (anchors.Length == 0)
        {
            return ["evidence_anchor"];
        }

        return anchors
            .Where(path => !File.Exists(Path.Combine(root, path)) && !Directory.Exists(Path.Combine(root, path)))
            .Select(static path => $"evidence_path:{path}")
            .ToArray();
    }

    private static ClosureDeferral[] ParseDeferrals(string markdown)
    {
        const string heading = "## Story 8.10 accepted Epic 8 closure deferrals";
        int headingIndex = markdown.IndexOf(heading, StringComparison.Ordinal);
        headingIndex.ShouldBeGreaterThanOrEqualTo(0, $"Missing {heading}");
        string closureSection = markdown[headingIndex..];
        return Regex.Matches(
                closureSection,
                @"(?ms)^- deferral_id:.*?(?=^- deferral_id:|\z)",
                RegexOptions.CultureInvariant)
            .Select(static match => match.Value)
            .Select(static block => new ClosureDeferral(
                ReadDeferralField(block, "deferral_id"),
                ReadDeferralField(block, "status"),
                ReadDeferralField(block, "owner"),
                ReadDeferralField(block, "exit_proof"),
                ReadDeferralField(block, "rollback"),
                ReadDeferralField(block, "evidence")))
            .ToArray();
    }

    private static string ReadDeferralField(string block, string field)
    {
        Match match = Regex.Match(
            block,
            $@"(?m)^\s*(?:-\s*)?{Regex.Escape(field)}:\s*(?:`(?<quoted>[^`]*)`|(?<plain>\S.*?))\s*$",
            RegexOptions.CultureInvariant);
        return match.Success
            ? (match.Groups["quoted"].Success ? match.Groups["quoted"].Value : match.Groups["plain"].Value.Trim())
            : string.Empty;
    }

    private static Dictionary<string, string> ParseValidationReceipts(string summary)
    {
        int headingIndex = summary.LastIndexOf("### Validation receipts", StringComparison.Ordinal);
        headingIndex.ShouldBeGreaterThanOrEqualTo(0, "Validation receipt heading is missing.");
        string section = summary[headingIndex..];
        Dictionary<string, string> receipts = new(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(
                     section,
                     @"(?m)^\|\s*(?<check>[^|]+?)\s*\|\s*(?<result>[^|]+?)\s*\|",
                     RegexOptions.CultureInvariant))
        {
            string check = match.Groups["check"].Value.Trim();
            if (!string.Equals(check, "Check", StringComparison.Ordinal) && !check.StartsWith("---", StringComparison.Ordinal))
            {
                receipts[check] = match.Groups["result"].Value.Trim();
            }
        }

        receipts.ShouldNotBeEmpty("Validation receipt table is missing.");
        return receipts;
    }

    private static string[] DescribeReceiptGaps(IReadOnlyDictionary<string, string> receipts)
        => receipts
            .Where(static receipt => !receipt.Value.StartsWith("Pass", StringComparison.OrdinalIgnoreCase))
            .Select(static receipt => $"{receipt.Key}:{receipt.Value}")
            .ToArray();

    private static string NormalizeStoryStatus(string status)
    {
        string normalized = string.Equals(status, "in-review", StringComparison.Ordinal) ? "review" : status;
        new[] { "in-progress", "review", "done" }.ShouldContain(normalized, $"Unknown Story 8.10 status: {status}");
        return normalized;
    }

    private static string[] SplitLines(string value)
        => value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string[] ExtractRequirementIds(string prd)
        => Regex.Matches(
                prd,
                @"(?m)^###\s+(?<id>FR-[A-Za-z]+(?:-\d+)?|NFR\d+)\b",
                RegexOptions.CultureInvariant)
            .Select(static match => match.Groups["id"].Value)
            .ToArray();

    private static Dictionary<string, (string Disposition, string Evidence)> ParseInvariantRows(string section)
    {
        Dictionary<string, (string Disposition, string Evidence)> rows = new(StringComparer.Ordinal);
        foreach (string line in section.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!line.StartsWith("| I", StringComparison.Ordinal))
            {
                continue;
            }

            string[] cells = line.Trim('|').Split('|', StringSplitOptions.TrimEntries);
            cells.Length.ShouldBe(3, line);
            if (string.Equals(cells[0], "Invariant", StringComparison.Ordinal))
            {
                continue;
            }

            rows.Add(cells[0], (cells[1], cells[2]));
        }

        return rows;
    }

    private static string ReadMarkedSection(string markdown, string markerName)
    {
        string start = $"<!-- {markerName}:start -->";
        string end = $"<!-- {markerName}:end -->";
        int startIndex = markdown.IndexOf(start, StringComparison.Ordinal);
        int endIndex = markdown.IndexOf(end, StringComparison.Ordinal);
        startIndex.ShouldBeGreaterThanOrEqualTo(0);
        endIndex.ShouldBeGreaterThan(startIndex);
        return markdown[(startIndex + start.Length)..endIndex];
    }

    private static string ReadFrontmatterValue(string markdown, string key)
    {
        Match match = Regex.Match(
            markdown,
            $@"(?m)^{Regex.Escape(key)}:\s*['""]?(?<value>[^'""\r\n]+)['""]?\s*$",
            RegexOptions.CultureInvariant);
        match.Success.ShouldBeTrue(key);
        return match.Groups["value"].Value.Trim();
    }

    private static string ReadYamlStatus(string yaml, string key)
    {
        Match match = Regex.Match(
            yaml,
            $@"(?m)^\s*{Regex.Escape(key)}:\s*(?<value>\S+)\s*$",
            RegexOptions.CultureInvariant);
        match.Success.ShouldBeTrue(key);
        return match.Groups["value"].Value;
    }

    private static string Read(string root, string relativePath)
        => File.ReadAllText(Path.Combine(root, relativePath));

    private static string RunGit(string root, params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("git")
            {
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };
        foreach (string argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start().ShouldBeTrue();
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        process.ExitCode.ShouldBe(0, error);
        return output;
    }

    private static bool TryRunGit(string root, out string output, params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("git")
            {
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };
        foreach (string argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        if (!process.Start())
        {
            output = string.Empty;
            return false;
        }

        output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode == 0;
    }
}
