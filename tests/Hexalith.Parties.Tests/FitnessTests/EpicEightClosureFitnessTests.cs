using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

using Hexalith.Parties.PackageTests;

using Shouldly;

using static Hexalith.Parties.Tests.FitnessTests.RepositoryRoot;
using static Hexalith.Parties.Tests.FitnessTests.RepositoryTestProcess;

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
        "8.7-data-protection-extraction",
        "8.8-runtime-boundary-cleanup",
        "8.9-frontcomposer-ui-consolidation",
        "external-runtime-deployment",
    ];

    private static readonly string[] RequiredValidationReceiptChecks =
    [
        "Warning and nested-submodule policy",
        "Release solution build",
        "All .NET test projects",
        "Package/API and package-only consumers",
        "npm install and typecheck",
        "Playwright accessibility",
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
            // Only open ledger work can satisfy the accepted-wait expectation. Completing one
            // requires reconciling ExpectedDeferrals and its dependent invariants explicitly.
        }

        string sprintStatus = Read(root, SprintStatusPath);
        ReadYamlStatus(sprintStatus, "8-6-projection-and-query-sdk-migration").ShouldBe("done");

        // Directional, not mandatory. Unresolved Story 8.6 review debt REQUIRES the umbrella
        // deferral to exist; it must not require the debt itself to persist. The previous assertion
        // was inverted, so resolving Story 8.6's residual debt would have broken Epic 8 closure.
        bool hasResidualStory86Debt = Regex.IsMatch(
            Read(root, Story86Path),
            @"(?m)^\s*- \[ \] \[Review\]\[Defer\]",
            RegexOptions.CultureInvariant);
        if (hasResidualStory86Debt)
        {
            deferrals.Select(static item => item.Id)
                .ShouldContain("8.6-residual-review-debt", "Story 8.6 still has unchecked review debt.");
        }
        ReadYamlStatus(sprintStatus, "8-7-data-protection-extraction").ShouldBe("blocked");
        ReadYamlStatus(sprintStatus, "8-8-client-mcp-apphost-build-and-deploy-cleanup").ShouldBe("blocked");
        ReadYamlStatus(sprintStatus, "8-9-ui-frontcomposer-and-fluent-consolidation").ShouldBe("blocked");
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

    /// <summary>
    /// Verifies that retained reason fields cannot keep completed or unclassified work accepted.
    /// </summary>
    [Theory]
    [InlineData("done")]
    [InlineData("done 2026-10-21")]
    [InlineData("resolved")]
    [InlineData("archived")]
    [InlineData("unknown")]
    [InlineData("")]
    public void CurrentDeferralRequiresAnExplicitOpenLedgerStatus(string status)
    {
        string markdown = $"""
            ### DW-999: Example closure deferral

            reason: deferral_id: `example` status: accepted owner: `owner` exit_proof: `proof` rollback: `rollback` evidence: `docs/architecture.md`
            {(string.IsNullOrEmpty(status) ? string.Empty : $"status: {status}")}
            """;

        ParseDeferrals(markdown).ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies first-class status wins over packed history without leaking across entries.
    /// </summary>
    [Fact]
    public void CurrentDeferralStatusIsAuthoritativeAndBoundedByItsOwnEntry()
    {
        const string markdown = """
            ### DW-998: Completed deferral

            status: done 2026-10-21
            reason: deferral_id: `completed` status: accepted owner: `old owner` exit_proof: `old proof` rollback: `old rollback` evidence: `old.md`

            ### DW-999: Open deferral

            reason: deferral_id: `example` status: done owner: `owner` exit_proof: `proof` rollback: `rollback` evidence: `docs/architecture.md`
            status: open

            ### DW-1000: Unrelated work

            status: done
            """;

        ClosureDeferral deferral = ParseDeferrals(markdown).ShouldHaveSingleItem();
        deferral.Id.ShouldBe("example");
        deferral.Status.ShouldBe("open");
        deferral.Owner.ShouldBe("owner");
        DescribeDeferralGaps(deferral).ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies legacy bullet status still governs eligibility before ledger migration.
    /// </summary>
    [Theory]
    [InlineData("accepted", true)]
    [InlineData("open", true)]
    [InlineData("done", false)]
    [InlineData("done 2026-10-21", false)]
    [InlineData("unknown", false)]
    [InlineData("", false)]
    public void LegacyDeferralRequiresAnAcceptedOrOpenStatus(string status, bool expected)
    {
        string markdown = $"""
            - deferral_id: `example`
              owner: `owner`
              exit_proof: `proof`
              rollback: `rollback`
              evidence: `docs/architecture.md`
              status: {status}
            """;

        ParseDeferrals(markdown).Length.ShouldBe(expected ? 1 : 0);
    }

    /// <summary>
    /// Verifies legacy formatting inside completed ledger work cannot bypass its status.
    /// </summary>
    [Fact]
    public void CompletedLedgerEntryCannotContributeANestedLegacyDeferral()
    {
        const string markdown = """
            ### DW-999: Completed migrated work

            status: done 2026-10-21
            - deferral_id: `example`
              status: accepted
              owner: `owner`
              exit_proof: `proof`
              rollback: `rollback`
              evidence: `docs/architecture.md`
            """;

        ParseDeferrals(markdown).ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies unrelated provenance cannot supply a required packed reason field.
    /// </summary>
    [Fact]
    public void CurrentDeferralCannotBorrowRequiredFieldsFromMetadata()
    {
        const string markdown = """
            ### DW-999: Open migrated work

            status: open
            origin: Earlier owner: `historical owner`
            reason: deferral_id: `example` exit_proof: `proof` rollback: `rollback` evidence: `docs/architecture.md`
            resolution: Historical outcome retained for context
            """;

        ClosureDeferral deferral = ParseDeferrals(markdown).ShouldHaveSingleItem();
        deferral.Id.ShouldBe("example");
        DescribeDeferralGaps(deferral).ShouldBe(["owner"]);
    }

    /// <summary>
    /// Verifies duplicated or conflicting status fields cannot establish acceptance.
    /// </summary>
    [Theory]
    [InlineData(true, "open", "open")]
    [InlineData(true, "open", "done")]
    [InlineData(true, "done", "open")]
    [InlineData(false, "accepted", "accepted")]
    [InlineData(false, "accepted", "done")]
    [InlineData(false, "done", "accepted")]
    public void DuplicateDeferralStatusesCannotEstablishAcceptance(bool current, string first, string second)
    {
        string markdown = current
            ? $"""
                ### DW-999: Ambiguous status

                status: {first}
                reason: deferral_id: `example` owner: `owner` exit_proof: `proof` rollback: `rollback` evidence: `docs/architecture.md`
                status: {second}
                """
            : $"""
                - deferral_id: `example`
                  status: {first}
                  owner: `owner`
                  exit_proof: `proof`
                  rollback: `rollback`
                  evidence: `docs/architecture.md`
                  status: {second}
                """;

        ParseDeferrals(markdown).ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies a following bullet or heading cannot fill an incomplete legacy deferral.
    /// </summary>
    [Theory]
    [InlineData("- Unrelated work")]
    [InlineData("### Unrelated work")]
    public void LegacyDeferralFieldsAreBoundedByTheNextItem(string boundary)
    {
        string markdown = $"""
            - deferral_id: `example`
              status: accepted
              exit_proof: `proof`
              rollback: `rollback`
              evidence: `docs/architecture.md`

            {boundary}
              owner: `unrelated owner`
            """;

        ClosureDeferral deferral = ParseDeferrals(markdown).ShouldHaveSingleItem();
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
        HashSet<string> testClasses = CollectExecutableTestClasses(root);

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
        SplitLines(RunGit(root, ["ls-files", "--others", "--exclude-standard", "--", .. scopeArtifacts]))
            .ShouldBeEmpty("Untracked canonical scope artifacts also fail the zero-PRD gate.");

        // Skip loudly rather than pass silently. A shallow clone cannot reach the baseline commit,
        // and the previous conditional turned the whole PRD/epic freeze into a no-op that still
        // reported pass -- exactly the invisible skip AC3 forbids.
        Assert.SkipUnless(
            TryRunGit(root, out _, "cat-file", "-e", $"{BaselineCommit}^{{commit}}"),
            $"Baseline commit {BaselineCommit} is unreachable; check out with fetch-depth 0 to run the zero-PRD freeze.");

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

        string? closureCommit = ReadClosureCommit(root);
        if (closureCommit is not null)
        {
            spec = RunGit(root, "show", $"{closureCommit}:{SpecPath}");
        }
        Regex.Matches(spec, @"(?m)^\s*- \[ \]", RegexOptions.CultureInvariant).ShouldBeEmpty();
        epicStatus.ShouldBe("done");
        string summary = ReadClosureSummary(root, closureCommit);
        summary.ShouldContain("Story 8.10 Final Readiness, Documentation, and Retirement Gate");
        Dictionary<string, string> receipts = ParseValidationReceipts(summary);
        DescribeReceiptGaps(receipts).ShouldBeEmpty();
        DescribeCurrentReceiptIdentityGaps(summary, receipts, () => ReadClosureIdentity(root, closureCommit)).ShouldBeEmpty();
        DescribeAccessibilityStampGaps(summary, receipts, ReadSelectedRootGitlinkSha(root, "references/Hexalith.FrontComposer", closureCommit ?? "HEAD"))
            .ShouldBeEmpty();
    }

    [Fact]
    public void ValidationReceiptSectionResolvesToTheCurrentTableAndIsWellFormed()
    {
        string root = RepositoryRoot.Locate();
        string? closureCommit = ReadClosureCommit(root);
        string summary = ReadClosureSummary(root, closureCommit);
        Dictionary<string, string> receipts = ParseValidationReceipts(summary);

        // Deliberately exercised regardless of story status. The closure gate reads these receipts
        // only once the story is already marked done, so a superseded or unparsable table would stay
        // invisible until the moment someone attempts closure and the gate proves unpassable.
        DescribeMissingReceiptGaps(receipts).ShouldBeEmpty(
            "Every spec Verification lane requires its canonical receipt row.");

        // Structural only -- deliberately NOT a greenness check. Greenness belongs to the closure
        // gate above. Asserting Pass here would mean a genuinely red gate has to be written up as
        // Pass to keep the normal test lane green, which is precisely the pressure that manufactures
        // false receipts. A red receipt must be recordable as red without breaking the build.
        foreach (KeyValuePair<string, string> receipt in receipts)
        {
            receipt.Value.ShouldNotBeNullOrWhiteSpace(receipt.Key);
        }

        DescribeCurrentReceiptIdentityGaps(summary, receipts, () => ReadClosureIdentity(root, closureCommit)).ShouldBeEmpty(
            "Every clean Pass receipt must bind its qualifying Parties snapshot and dependency set.");
        if (IsCleanPass(receipts["Playwright accessibility"]))
        {
            DescribeAccessibilityStampGaps(summary, receipts, ReadSelectedRootGitlinkSha(root, "references/Hexalith.FrontComposer", closureCommit ?? "HEAD"))
                .ShouldBeEmpty("A clean Pass accessibility receipt must prove the selected FrontComposer source identity.");
        }
    }

    /// <summary>
    /// Verifies that only a clean Pass accessibility receipt must carry the selected FrontComposer
    /// stamp, so an honest red receipt without it never breaks the structural check.
    /// </summary>
    [Theory]
    [InlineData("**Blocked**", false, false)]
    [InlineData("Failed", false, false)]
    [InlineData("**Unvalidated**", false, false)]
    [InlineData("Pass", true, false)]
    [InlineData("Pass", false, true)]
    [InlineData("**Pass**", false, true)]
    public void AccessibilityStampIsRequiredOnlyForACleanPass(string result, bool stamped, bool expectGap)
    {
        string evidence = stamped ? $"6/6 at FrontComposer {PlatformApiPrerequisitesTests.FrontComposerSha}" : "6/6 at an unstamped tree";
        string summary = $"""
            ### Validation receipts

            | Check | Result | Evidence |
            | --- | --- | --- |
            | Playwright accessibility | {result} | {evidence} |
            """;

        string[] expected = expectGap ? ["Playwright accessibility:frontcomposer-stamp"] : [];
        DescribeAccessibilityStampGaps(summary, ParseValidationReceipts(summary), PlatformApiPrerequisitesTests.FrontComposerSha)
            .ShouldBe(expected);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("ready-ish")]
    public void UnknownStoryStatusFailsClosed(string status)
        => Should.Throw<ShouldAssertException>(() => NormalizeStoryStatus(status));

    [Fact]
    public void BlockedOrFailedValidationReceiptFailsClosed()
    {
        Dictionary<string, string> receipts = CreatePassingValidationReceipts();
        receipts["Release solution build"] = "**Blocked**";
        receipts["Playwright accessibility"] = "Failed";

        DescribeReceiptGaps(receipts).ShouldBe(["Release solution build:**Blocked**", "Playwright accessibility:Failed"]);
    }

    [Theory]
    [InlineData("Pass (unvalidated)")]
    [InlineData("Pass with skipped checks")]
    [InlineData("Pass with failed checks")]
    [InlineData("Pass with errors")]
    [InlineData("Pass (not run)")]
    [InlineData("Pass (partial)")]
    public void QualifiedPassWithMissingValidationFailsClosed(string status)
    {
        Dictionary<string, string> receipts = CreatePassingValidationReceipts();
        receipts["Playwright accessibility"] = status;

        DescribeReceiptGaps(receipts).ShouldBe([$"Playwright accessibility:{status}"]);
    }

    /// <summary>
    /// Verifies that the closure guard rejects omission of each required validation lane.
    /// </summary>
    [Theory]
    [InlineData("Warning and nested-submodule policy")]
    [InlineData("Release solution build")]
    [InlineData("All .NET test projects")]
    [InlineData("Package/API and package-only consumers")]
    [InlineData("npm install and typecheck")]
    [InlineData("Playwright accessibility")]
    public void MissingRequiredValidationReceiptFailsClosed(string missingCheck)
    {
        Dictionary<string, string> receipts = CreatePassingValidationReceipts();
        receipts.Remove(missingCheck).ShouldBeTrue();

        DescribeReceiptGaps(receipts).ShouldBe([$"{missingCheck}:missing"]);
    }

    /// <summary>
    /// Verifies that a later Markdown section cannot supply rows or evidence to the current table.
    /// </summary>
    [Theory]
    [InlineData("# Later receipts")]
    [InlineData("## Later receipts")]
    [InlineData("### Later receipts")]
    public void ValidationReceiptSectionEndsAtTheNextHeading(string heading)
    {
        string summary = $"""
            ### Validation receipts

            | Check | Result | Evidence |
            | --- | --- | --- |
            | Release solution build | Blocked | current build evidence |

            {heading}

            | Check | Result | Evidence |
            | --- | --- | --- |
            | Playwright accessibility | Pass | later unrelated evidence |
            """;

        Dictionary<string, string> receipts = ParseValidationReceipts(summary);

        receipts.Keys.ShouldBe(["Release solution build"]);
        ReadValidationReceiptEvidence(summary, "Release solution build").ShouldBe("current build evidence");
        Should.Throw<ShouldAssertException>(() => ReadValidationReceiptEvidence(summary, "Playwright accessibility"));
    }

    /// <summary>Rejects stale, missing, malformed, duplicated, and borrowed identities in every lane.</summary>
    [Theory]
    [InlineData("Warning and nested-submodule policy")]
    [InlineData("Release solution build")]
    [InlineData("All .NET test projects")]
    [InlineData("Package/API and package-only consumers")]
    [InlineData("npm install and typecheck")]
    [InlineData("Playwright accessibility")]
    public void EveryPassingReceiptRequiresItsOwnExactIdentity(string check)
    {
        string snapshot = new('a', 64);
        string dependencies = new('b', 64);
        string validEvidence = $"parties_snapshot={snapshot} dependency_set={dependencies}";
        string[] invalidEvidence =
        [
            $"dependency_set={dependencies}",
            $"parties_snapshot={snapshot}",
            $"parties_snapshot={new string('c', 64)} dependency_set={dependencies}",
            $"parties_snapshot={snapshot} dependency_set={new string('c', 64)}",
            $"parties_snapshot={new string('a', 63)} dependency_set={dependencies}",
            $"parties_snapshot={snapshot} dependency_set={new string('B', 64)}",
            $"parties_snapshot={snapshot}x dependency_set={dependencies}",
            $"parties_snapshot= dependency_set={dependencies}",
            $"{validEvidence} parties_snapshot={snapshot}",
            $"{validEvidence} dependency_set={new string('c', 64)}",
            "See the valid identity in the next row",
        ];

        foreach (string evidence in invalidEvidence)
        {
            string summary = CreateStampedValidationSummary(check, "Pass", evidence, validEvidence);
            DescribeReceiptIdentityGaps(summary, ParseValidationReceipts(summary), snapshot, dependencies)
                .ShouldNotBeEmpty($"{check}: {evidence}");
        }

        string validSummary = CreateStampedValidationSummary(check, "**Pass**", validEvidence, validEvidence);
        DescribeReceiptIdentityGaps(validSummary, ParseValidationReceipts(validSummary), snapshot, dependencies)
            .ShouldBeEmpty();
    }

    /// <summary>Preserves honest unsuccessful receipts without requiring evidence they do not have.</summary>
    [Theory]
    [InlineData("Blocked")]
    [InlineData("**Failed**")]
    [InlineData("Unvalidated")]
    public void NonPassingReceiptsDoNotRequireIdentityStamps(string status)
    {
        string summary = "### Validation receipts\n\n| Check | Result | Evidence |\n| --- | --- | --- |\n"
            + string.Join('\n', RequiredValidationReceiptChecks.Select(check => $"| {check} | {status} | Not qualified |"));

        DescribeReceiptIdentityGaps(summary, ParseValidationReceipts(summary), new string('a', 64), new string('b', 64))
            .ShouldBeEmpty();
    }

    /// <summary>Derives the live identity for even one successful lane, never for unsuccessful rows.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CurrentIdentityReaderRunsOnlyWhenAnyRequiredReceiptPasses(bool hasPass)
    {
        string snapshot = new('a', 64);
        string dependencies = new('b', 64);
        string summary = "### Validation receipts\n\n| Check | Result | Evidence |\n| --- | --- | --- |\n"
            + string.Join('\n', RequiredValidationReceiptChecks.Select((check, index) =>
                $"| {check} | {(hasPass && index == 0 ? "Pass" : "Unvalidated")} | parties_snapshot={new string('c', 64)} dependency_set={dependencies} |"));
        int calls = 0;

        string[] gaps = DescribeCurrentReceiptIdentityGaps(summary, ParseValidationReceipts(summary), () =>
        {
            calls++;
            return (snapshot, dependencies);
        });

        calls.ShouldBe(hasPass ? 1 : 0);
        gaps.ShouldBe(hasPass ? [$"{RequiredValidationReceiptChecks[0]}:parties_snapshot:expected={snapshot}"] : []);
    }

    /// <summary>Proves that receipt normalization exempts only the current receipt section.</summary>
    [Fact]
    public void SnapshotBindsFileBytesModesPathsAndGitlinksButNotReceiptPayload()
    {
        const string summary = "# Retained evidence\nHistorical result\n\n### Validation receipts\n\n| Check | Result | Evidence |\n| --- | --- | --- |\n| Release solution build | Unvalidated | Pending |\n\n### Stamp instructions\nRetained instructions\n";
        (string Path, string Mode, string Digest)[] entries =
        [
            CreateManifestEntry("src/example.cs", "100644", Encoding.UTF8.GetBytes("source")),
            CreateManifestEntry(TestSummaryPath, "100644", Encoding.UTF8.GetBytes(summary)),
            CreateManifestEntry("references/Example", "160000", Encoding.UTF8.GetBytes(new string('a', 40))),
        ];
        string baseline = HashManifest("parties-snapshot-v2", entries);
        (string Path, string Mode, string Digest)[] receiptEdit = [.. entries];
        receiptEdit[1] = CreateManifestEntry(TestSummaryPath, "100644", Encoding.UTF8.GetBytes(
            summary.Replace("Unvalidated | Pending", "Pass | parties_snapshot=anything dependency_set=anything", StringComparison.Ordinal)));
        HashManifest("parties-snapshot-v2", receiptEdit).ShouldBe(baseline);

        (string Path, string Mode, string Digest)[] changes =
        [
            CreateManifestEntry("src/example.cs", "100644", Encoding.UTF8.GetBytes("changed source")),
            (entries[0].Path, "100755", entries[0].Digest),
            ("src/renamed.cs", entries[0].Mode, entries[0].Digest),
        ];
        foreach ((string Path, string Mode, string Digest) change in changes)
        {
            HashManifest("parties-snapshot-v2", [change, .. entries.Skip(1)]).ShouldNotBe(baseline);
        }
        HashManifest("parties-snapshot-v2", entries.Skip(1)).ShouldNotBe(baseline, "Deleted paths are absent from the final-content manifest.");

        HashManifest("parties-snapshot-v2", [.. entries.Take(2), CreateManifestEntry("references/Example", "160000", Encoding.UTF8.GetBytes(new string('b', 40)))])
            .ShouldNotBe(baseline);
        HashManifest("parties-snapshot-v2", [.. entries, CreateManifestEntry("untracked.txt", "100644", [1])])
            .ShouldNotBe(baseline);
        HashManifest("parties-snapshot-v2", [entries[0], CreateManifestEntry(TestSummaryPath, "100644", Encoding.UTF8.GetBytes(summary.Replace("Historical result", "Changed history", StringComparison.Ordinal))), entries[2]])
            .ShouldNotBe(baseline);
        HashManifest("parties-snapshot-v2", entries.Reverse()).ShouldBe(baseline);
    }

    /// <summary>Exempts only the three closure bookkeeping values needed after gates are rerun.</summary>
    [Fact]
    public void ClosureBookkeepingTransitionsPreserveSnapshotButOtherStateRemainsBound()
    {
        const string spec = "---\ntitle: Closure\nstatus: 'in-review'\n---\n\nRetained requirements\nstatus: 'in-review'\n";
        const string sprint = "development_status:\n  epic-8: in-progress\n  8-10-final-readiness-documentation-and-retirement-gate: review\n  8-9-ui-frontcomposer-and-fluent-consolidation: blocked\n";
        (string Path, string Mode, string Digest)[] entries =
        [
            CreateManifestEntry(SpecPath, "100644", Encoding.UTF8.GetBytes(spec)),
            CreateManifestEntry(SprintStatusPath, "100644", Encoding.UTF8.GetBytes(sprint)),
        ];
        string baseline = HashManifest("parties-snapshot-v2", entries);
        string doneSpec = spec.Replace("status: 'in-review'\n---", "status: 'done'\n---", StringComparison.Ordinal);
        string doneSprint = sprint.Replace("epic-8: in-progress", "epic-8: done", StringComparison.Ordinal)
            .Replace("retirement-gate: review", "retirement-gate: done", StringComparison.Ordinal);
        HashManifest("parties-snapshot-v2",
        [
            CreateManifestEntry(SpecPath, "100644", Encoding.UTF8.GetBytes(doneSpec)),
            CreateManifestEntry(SprintStatusPath, "100644", Encoding.UTF8.GetBytes(doneSprint)),
        ]).ShouldBe(baseline);

        foreach (string changedSpec in new[]
                 {
                     spec.Replace("Retained requirements", "Changed requirements", StringComparison.Ordinal),
                     spec.Replace("status: 'in-review'\n", "status: 'done'\n", StringComparison.Ordinal),
                     spec.Replace("status: 'in-review'\n---", "status: 'blocked'\n---", StringComparison.Ordinal),
                 })
        {
            HashManifest("parties-snapshot-v2", [CreateManifestEntry(SpecPath, "100644", Encoding.UTF8.GetBytes(changedSpec)), entries[1]])
                .ShouldNotBe(baseline);
        }

        HashManifest("parties-snapshot-v2", [entries[0], CreateManifestEntry(SprintStatusPath, "100644", Encoding.UTF8.GetBytes(
                sprint.Replace("consolidation: blocked", "consolidation: done", StringComparison.Ordinal)))])
            .ShouldNotBe(baseline);
    }

    /// <summary>Exercises root Git enumeration and real modified, untracked, deleted, mode and link inputs.</summary>
    [Fact]
    public void SnapshotReaderBindsActualWorktreeFilesAndOwnerExecutableMode()
    {
        DirectoryInfo fixture = Directory.CreateTempSubdirectory("parties-closure-files-");
        try
        {
            string root = Path.Combine(fixture.FullName, "root");
            RunGit(fixture.FullName, "clone", "--shared", "--no-checkout", "--", RepositoryRoot.Locate(), root);
            RunGit(root, "restore", "--source=HEAD", "--staged", "--worktree", "--", ".gitignore", "README.md", "global.json");
            string tracked = Path.Combine(root, "README.md");
            byte[] original = File.ReadAllBytes(tracked);
            string baseline = ReadFixtureSnapshot(root);

            File.AppendAllText(tracked, "\nFixture-only modification\n");
            ReadFixtureSnapshot(root).ShouldNotBe(baseline);
            File.WriteAllBytes(tracked, original);
            ReadFixtureSnapshot(root).ShouldBe(baseline);

            string untracked = Path.Combine(root, "closure-untracked.txt");
            File.WriteAllText(untracked, "Untracked fixture input");
            ReadFixtureSnapshot(root).ShouldNotBe(baseline);
            File.Delete(untracked);
            string deleted = Path.Combine(root, "global.json");
            byte[] deletedBytes = File.ReadAllBytes(deleted);
            File.Delete(deleted);
            ReadFixtureSnapshot(root).ShouldNotBe(baseline);
            File.WriteAllBytes(deleted, deletedBytes);
            ReadFixtureSnapshot(root).ShouldBe(baseline);

            if (!OperatingSystem.IsWindows())
            {
                UnixFileMode originalMode = File.GetUnixFileMode(tracked);
                File.SetUnixFileMode(tracked, (originalMode & ~UnixFileMode.UserExecute) | UnixFileMode.GroupExecute);
                ReadFixtureSnapshot(root).ShouldBe(baseline, "Group-only execute does not change Git's owner-executable mode.");
                File.SetUnixFileMode(tracked, originalMode | UnixFileMode.UserExecute);
                ReadFixtureSnapshot(root).ShouldNotBe(baseline);
                File.SetUnixFileMode(tracked, originalMode);

                string link = Path.Combine(root, "closure-link.md");
                File.CreateSymbolicLink(link, "README.md");
                string linkedSnapshot = ReadFixtureSnapshot(root);
                linkedSnapshot.ShouldNotBe(baseline);
                (string Path, string Mode, string Digest) linkEntry = ReadWorktreeManifestEntry(root, "closure-link.md", "100644");
                linkEntry.Mode.ShouldBe("120000");
                linkEntry.Digest.ShouldBe(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("README.md"))));
                File.Delete(link);
                File.CreateSymbolicLink(link, "global.json");
                ReadFixtureSnapshot(root).ShouldNotBe(linkedSnapshot);
            }
        }
        finally
        {
            fixture.Delete(recursive: true);
        }
    }

    /// <summary>Git clean conversion and index modes make the same content portable across checkouts.</summary>
    [Fact]
    public void SnapshotUsesGitNormalizedContentAndHonorsDisabledFileMode()
    {
        DirectoryInfo fixture = Directory.CreateTempSubdirectory("parties-closure-normalization-");
        try
        {
            string root = Path.Combine(fixture.FullName, "root");
            RunGit(fixture.FullName, "clone", "--shared", "--no-checkout", "--", RepositoryRoot.Locate(), root);
            RunGit(root, "restore", "--source=HEAD", "--staged", "--worktree", "--", "README.md");
            File.WriteAllText(Path.Combine(root, ".gitattributes"), "*.md text eol=lf\n*.yaml text eol=lf\n");
            foreach ((string path, string text) in new[]
                     {
                         (SpecPath, "---\nstatus: 'in-review'\n---\nRequirements\n"),
                         (SprintStatusPath, "  epic-8: in-progress\n  8-10-final-readiness-documentation-and-retirement-gate: review\n"),
                         (TestSummaryPath, "Historical evidence\n\n### Validation receipts\n\nPending\n\n### Instructions\nRetained\n"),
                     })
            {
                string fullPath = Path.Combine(root, path);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                File.WriteAllText(fullPath, text);
            }
            RunGit(root, "config", "core.autocrlf", "false");
            RunGit(root, "config", "core.fileMode", "false");
            string baseline = ReadFixtureSnapshot(root);
            foreach (string path in new[] { "README.md", SpecPath, SprintStatusPath, TestSummaryPath })
            {
                string fullPath = Path.Combine(root, path);
                File.WriteAllText(fullPath, File.ReadAllText(fullPath).Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal));
            }
            RunGit(root, "config", "core.autocrlf", "true");
            ReadFixtureSnapshot(root).ShouldBe(baseline, "Git-normalized LF and CRLF checkouts must identify the same content, including bookkeeping files.");
            if (!OperatingSystem.IsWindows())
            {
                string tracked = Path.Combine(root, "README.md");
                UnixFileMode original = File.GetUnixFileMode(tracked);
                File.SetUnixFileMode(tracked, original ^ UnixFileMode.UserExecute);
                ReadFixtureSnapshot(root).ShouldBe(baseline, "core.fileMode=false preserves the tracked index mode.");
            }
            File.AppendAllText(Path.Combine(root, "README.md"), "Changed content\r\n");
            ReadFixtureSnapshot(root).ShouldNotBe(baseline);
        }
        finally
        {
            fixture.Delete(recursive: true);
        }
    }

    /// <summary>Preserves legacy mixed blobs that Git does not renormalize during ordinary checkout/add.</summary>
    [Fact]
    public void SnapshotPreservesTrackedMixedLineEndingsWithoutEolAttributes()
    {
        DirectoryInfo fixture = Directory.CreateTempSubdirectory("parties-closure-mixed-eol-");
        try
        {
            string root = Path.Combine(fixture.FullName, "root");
            RunGit(fixture.FullName, "clone", "--shared", "--no-checkout", "--", RepositoryRoot.Locate(), root);
            RunGit(root, "restore", "--source=HEAD", "--staged", "--worktree", "--", "README.md");
            RunGit(root, "config", "core.autocrlf", "false");
            const string mixed = "Historical CRLF\r\nHistorical LF\n\n### Validation receipts\n\nPending\r\n\n### Instructions\nRetained\r\n";
            string summary = Path.Combine(root, TestSummaryPath);
            Directory.CreateDirectory(Path.GetDirectoryName(summary)!);
            File.WriteAllText(summary, mixed);
            RunGit(root, "add", "--", TestSummaryPath);
            RunGit(root, "ls-files", "--eol", "--", TestSummaryPath).ShouldContain("i/mixed");
            string baseline = ReadFixtureSnapshot(root);
            RunGit(root, "config", "core.autocrlf", "true");
            ReadFixtureSnapshot(root).ShouldBe(baseline, "Changing checkout configuration cannot reinterpret a legacy mixed-EOL blob.");
            File.WriteAllText(summary, mixed.Replace("Pending", "Pass with recorded tokens", StringComparison.Ordinal));
            ReadFixtureSnapshot(root).ShouldBe(baseline, "Only the canonical receipt payload changed.");
            File.WriteAllText(summary, mixed.Replace("Historical LF", "Changed historical LF", StringComparison.Ordinal));
            ReadFixtureSnapshot(root).ShouldNotBe(baseline);
        }
        finally
        {
            fixture.Delete(recursive: true);
        }
    }

    /// <summary>Exercises exact closed inputs, later changes, rejected receipts, reopening and shallow history.</summary>
    [Fact]
    public void ClosedIdentityUsesTheClosureCommitThroughLaterChangesAndReopening()
    {
        DirectoryInfo fixture = Directory.CreateTempSubdirectory("parties-closure-history-");
        const string versionProperty = "HexalithClosureFixtureVersion";
        string? originalVersion = Environment.GetEnvironmentVariable(versionProperty);
        try
        {
            Environment.SetEnvironmentVariable(versionProperty, null);
            string root = Path.Combine(fixture.FullName, "root");
            Directory.CreateDirectory(root);
            RunGit(root, "init");
            RunGit(root, "config", "user.name", "Closure fixture");
            RunGit(root, "config", "user.email", "closure-fixture@example.invalid");
            RunGit(root, "config", "core.autocrlf", "false");
            RunGit(root, "config", "core.fileMode", "false");
            string ownerRoot = RepositoryRoot.Locate();
            const string buildsPath = "references/Hexalith.Builds";
            const string composerPath = "references/Hexalith.FrontComposer";
            string buildsRoot = Path.Combine(root, buildsPath);
            Directory.CreateDirectory(buildsRoot);
            RunGit(buildsRoot, "init");
            RunGit(buildsRoot, "config", "user.name", "Closure fixture");
            RunGit(buildsRoot, "config", "user.email", "closure-fixture@example.invalid");
            RunGit(buildsRoot, "config", "core.autocrlf", "false");
            void WriteFixture(string path, string content)
            {
                string fullPath = Path.Combine(root, path);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                File.WriteAllText(fullPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: IsClosureBookkeepingPath(path)));
            }
            void CommitFixture(string repository)
                => RunGit(repository, "commit", "--allow-empty", "-m", "test: exercise closure identity");
            WriteFixture(buildsPath + "/.gitattributes", "Props/hidden.props export-ignore\nProps/substituted.props export-subst\nProps/Directory.Packages.props text eol=crlf\n");
            WriteFixture(buildsPath + "/Props/hidden.props", "<Project><PropertyGroup><HexalithClosureFixtureVersion Condition=\"'$(HexalithClosureFixtureVersion)' == ''\">1.0.0</HexalithClosureFixtureVersion></PropertyGroup></Project>");
            WriteFixture(buildsPath + "/Props/substituted.props", "<Project><PropertyGroup><Marker>$Format:%H$</Marker><HexalithClosureFixtureVersion Condition=\"'$(Marker)' != '&#36;Format:%H&#36;'\">8.8.8</HexalithClosureFixtureVersion></PropertyGroup></Project>");
            WriteFixture(buildsPath + "/Props/Directory.Packages.props", "<Project><Import Project=\"hidden.props\" /><Import Project=\"substituted.props\" /><ItemGroup><PackageVersion Include=\"Example\" Version=\"$(HexalithClosureFixtureVersion)\" /></ItemGroup></Project>");
            RunGit(buildsRoot, "add", "--", ".");
            CommitFixture(buildsRoot);
            string builds = RunGit(buildsRoot, "rev-parse", "HEAD").Trim();
            string composer = ReadSelectedRootGitlinkSha(ownerRoot, composerPath);
            RunGit(fixture.FullName, "clone", "--shared", "--no-checkout", "--", RepositoryDependencyPaths.Resolve(ownerRoot, composerPath), Path.Combine(root, composerPath));
            RunGit(Path.Combine(root, composerPath), "sparse-checkout", "set", "--no-cone", "/README.md");
            RunGit(Path.Combine(root, composerPath), "checkout", "--detach", composer);
            WriteFixture(".gitmodules", $"[submodule \"Builds\"]\n path = {buildsPath}\n[submodule \"FrontComposer\"]\n path = {composerPath}\n");
            WriteFixture("global.json", "{}\n");
            WriteFixture("Directory.Packages.props", "<Project><Import Project=\"references/Hexalith.Builds/Props/Directory.Packages.props\" /></Project>");
            WriteFixture("src/Hexalith.Parties.Client/Hexalith.Parties.Client.csproj", "<Project><Import Project=\"../../Directory.Packages.props\" /></Project>");
            WriteFixture(SpecPath, "---\nstatus: 'in-review'\n---\nRequirements\n");
            WriteFixture(SprintStatusPath, "  epic-8: in-progress\n  8-10-final-readiness-documentation-and-retirement-gate: review\n");
            WriteFixture(TestSummaryPath, "### Validation receipts\n\nOriginal evidence\n");
            WriteFixture("retired.txt", "Removed before qualification\n");
            RunGit(root, "add", "--", ".gitmodules", "global.json", "Directory.Packages.props", "src", "_bmad-output", "retired.txt");
            RunGit(root, "update-index", "--add", "--cacheinfo", $"160000,{builds},{buildsPath}");
            RunGit(root, "update-index", "--add", "--cacheinfo", $"160000,{composer},{composerPath}");
            CommitFixture(root);
            ReadClosureCommit(root).ShouldBeNull();
            EffectivePackageVersions.ReadPackages(root)["Example"].ShouldBe("1.0.0");

            File.Delete(Path.Combine(root, "retired.txt"));
            (string Snapshot, string Dependencies) deleted = ReadCurrentClosureIdentity(root);
            RunGit(root, "add", "--", "retired.txt");
            ReadCurrentClosureIdentity(root).ShouldBe(deleted, "Staging a deletion cannot change final-content identity.");
            CommitFixture(root);
            ReadCommittedClosureIdentity(root, "HEAD").ShouldBe(deleted, "Deleted paths are absent both before staging and after commit.");
            (string Snapshot, string Dependencies) qualifying = ReadCommittedClosureIdentity(root, "HEAD");
            List<string> emitted = [];
            EmitCurrentClosureIdentity(root, qualify: true, emitted.Add);
            string qualifyingOutput = emitted.ShouldHaveSingleItem();
            qualifyingOutput.ShouldContain("immutable_consumption=true");
            string emittedSummary = CreateStampedValidationSummary("Release solution build", "Pass", qualifyingOutput, qualifyingOutput);
            DescribeReceiptIdentityGaps(emittedSummary, ParseValidationReceipts(emittedSummary), qualifying.Snapshot, qualifying.Dependencies)
                .ShouldBeEmpty("Actual qualifying output must match independently reconstructed immutable Git-object identities.");
            emitted.Clear();
            EmitCurrentClosureIdentity(root, qualify: false, emitted.Add);
            string diagnosticOutput = emitted.ShouldHaveSingleItem();
            diagnosticOutput.ShouldContain("diagnostic_parties_snapshot=");
            diagnosticOutput.ShouldContain("immutable_consumption=false");
            string diagnosticSummary = CreateStampedValidationSummary("Release solution build", "Pass", diagnosticOutput, diagnosticOutput);
            DescribeReceiptIdentityGaps(diagnosticSummary, ParseValidationReceipts(diagnosticSummary), qualifying.Snapshot, qualifying.Dependencies).ShouldNotBeEmpty();

            string evidence = $"Original evidence parties_snapshot={qualifying.Snapshot} dependency_set={qualifying.Dependencies} FrontComposer {composer}";
            WriteFixture(TestSummaryPath, CreateStampedValidationSummary("Release solution build", "Pass", evidence, evidence));
            WriteFixture(SpecPath, "---\nstatus: 'done'\n---\nRequirements\n");
            WriteFixture(SprintStatusPath, "  epic-8: done\n  8-10-final-readiness-documentation-and-retirement-gate: done\n");
            ReadClosureCommit(root).ShouldBeNull("An uncommitted done transition still qualifies the live tree.");
            RunGit(root, "add", "--", SpecPath, SprintStatusPath, TestSummaryPath);
            CommitFixture(root);
            string closure = RunGit(root, "rev-parse", "HEAD").Trim();
            ReadClosureCommit(root).ShouldBe(closure);
            (string Snapshot, string Dependencies) identity = ReadCommittedClosureIdentity(root, closure);
            ReadCurrentClosureIdentity(root).ShouldBe(identity, "BOM-bearing bookkeeping must preserve live/committed equality.");
            ReadCommittedClosureIdentity(root, $"{closure}^").ShouldBe(identity, "Only normalized statuses and receipts changed, with BOMs retained.");
            foreach (string path in new[] { SpecPath, SprintStatusPath, TestSummaryPath })
            {
                byte[] bytes = ReadGitBlobBytes(root, RunGit(root, "rev-parse", $"{closure}:{path}").Trim());
                bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble).ShouldBeTrue(path);
            }
            Environment.SetEnvironmentVariable(versionProperty, "9.9.9");
            EffectivePackageVersions.ReadPackages(root)["Example"].ShouldBe("9.9.9", "Live evaluation retains ambient overrides.");
            ReadCurrentClosureIdentity(root).Dependencies.ShouldNotBe(identity.Dependencies);
            ReadCommittedClosureIdentity(root, closure).ShouldBe(identity, "An ambient MSBuild override cannot rewrite historical qualification.");
            Environment.SetEnvironmentVariable(versionProperty, null);

            string movedBuilds = Path.Combine(fixture.FullName, "temporarily-unavailable-builds");
            Directory.Move(buildsRoot, movedBuilds);
            try
            {
                Should.Throw<Exception>(() => ReadCommittedClosureIdentity(root, closure)).Message.ShouldContain("Closure Builds repository is unavailable");
            }
            finally
            {
                Directory.Move(movedBuilds, buildsRoot);
            }

            WriteFixture(buildsPath + "/Props/hidden.props", "<Project><PropertyGroup><HexalithClosureFixtureVersion>2.0.0</HexalithClosureFixtureVersion></PropertyGroup></Project>");
            RunGit(buildsRoot, "add", "--", "Props/hidden.props");
            CommitFixture(buildsRoot);
            string laterBuilds = RunGit(buildsRoot, "rev-parse", "HEAD").Trim();
            EffectivePackageVersions.ReadPackages(root)["Example"].ShouldBe("2.0.0");
            WriteFixture("later.txt", "Later unrelated work\n");
            WriteFixture(TestSummaryPath, "### Validation receipts\n\nLater evidence\n");
            string laterComposer = RunGit(ownerRoot, "-C", composerPath, "rev-parse", $"{composer}^").Trim();
            RunGit(root, "add", "--", "later.txt", TestSummaryPath);
            RunGit(root, "update-index", "--cacheinfo", $"160000,{laterComposer},{composerPath}");
            RunGit(root, "update-index", "--cacheinfo", $"160000,{laterBuilds},{buildsPath}");
            CommitFixture(root);
            ReadClosureCommit(root).ShouldBe(closure);
            ReadClosureIdentity(root, ReadClosureCommit(root)).ShouldBe(identity, "The original Builds import remains 1.0.0 despite the live 2.0.0 catalog and gitlink.");
            string closedSummary = ReadClosureSummary(root, ReadClosureCommit(root));
            Dictionary<string, string> closedReceipts = ParseValidationReceipts(closedSummary);
            DescribeReceiptGaps(closedReceipts).ShouldBeEmpty();
            DescribeCurrentReceiptIdentityGaps(closedSummary, closedReceipts, () => ReadClosureIdentity(root, ReadClosureCommit(root))).ShouldBeEmpty();
            DescribeAccessibilityStampGaps(closedSummary, closedReceipts, ReadSelectedRootGitlinkSha(root, composerPath, ReadClosureCommit(root)!)).ShouldBeEmpty();
            ReadSelectedRootGitlinkSha(root, composerPath, closure).ShouldBe(composer);
            ReadSelectedRootGitlinkSha(root, composerPath).ShouldBe(laterComposer);
            ReadCommittedClosureIdentity(root, "HEAD").Snapshot.ShouldNotBe(identity.Snapshot);
            ReadCommittedClosureIdentity(root, "HEAD").Dependencies.ShouldNotBe(identity.Dependencies);

            foreach (bool corruptSnapshot in new[] { true, false })
            {
                WriteFixture(SpecPath, "---\nstatus: 'in-review'\n---\nRequirements\n");
                ReadClosureCommit(root).ShouldBeNull("Reopening returns to live qualification.");
                RunGit(root, "add", "--", SpecPath);
                CommitFixture(root);
                (string Snapshot, string Dependencies) nextIdentity = ReadCommittedClosureIdentity(root, "HEAD");
                string badSnapshot = corruptSnapshot ? new string('0', 64) : nextIdentity.Snapshot;
                string stamp = corruptSnapshot ? laterComposer : composer;
                string badEvidence = $"parties_snapshot={badSnapshot} dependency_set={nextIdentity.Dependencies} FrontComposer {stamp}";
                WriteFixture(TestSummaryPath, CreateStampedValidationSummary("Release solution build", "Pass", badEvidence, badEvidence));
                WriteFixture(SpecPath, "---\nstatus: 'done'\n---\nRequirements\n");
                ReadClosureCommit(root).ShouldBeNull("A new uncommitted closure cannot reuse an earlier done epoch.");
                RunGit(root, "add", "--", SpecPath, TestSummaryPath);
                CommitFixture(root);
                string rejectedClosure = ReadClosureCommit(root)!;
                rejectedClosure.ShouldBe(RunGit(root, "rev-parse", "HEAD").Trim());
                string goodEvidence = $"parties_snapshot={nextIdentity.Snapshot} dependency_set={nextIdentity.Dependencies} FrontComposer {laterComposer}";
                WriteFixture(TestSummaryPath, CreateStampedValidationSummary("Release solution build", "Pass", goodEvidence, goodEvidence));
                string rejectedSummary = ReadClosureSummary(root, rejectedClosure);
                Dictionary<string, string> rejectedReceipts = ParseValidationReceipts(rejectedSummary);
                string[] identityGaps = DescribeCurrentReceiptIdentityGaps(rejectedSummary, rejectedReceipts, () => ReadClosureIdentity(root, rejectedClosure));
                string[] accessibilityGaps = DescribeAccessibilityStampGaps(rejectedSummary, rejectedReceipts, ReadSelectedRootGitlinkSha(root, composerPath, rejectedClosure));
                if (corruptSnapshot)
                {
                    identityGaps.ShouldNotBeEmpty("Corrupted archived snapshot tokens must fail despite valid live receipt edits.");
                    accessibilityGaps.ShouldBeEmpty();
                }
                else
                {
                    identityGaps.ShouldBeEmpty();
                    accessibilityGaps.ShouldBe(["Playwright accessibility:frontcomposer-stamp"], "A foreign historical FrontComposer stamp must fail through the closed route.");
                }
            }

            string shallow = Path.Combine(fixture.FullName, "shallow");
            RunGit(fixture.FullName, "clone", "--depth=1", "--no-checkout", "--", new Uri(root + Path.DirectorySeparatorChar).AbsoluteUri, shallow);
            RunGit(shallow, "restore", "--source=HEAD", "--worktree", "--", SpecPath);
            Should.Throw<Exception>(() => ReadClosureCommit(shallow)).Message.ShouldContain("fetch-depth 0");
        }
        finally
        {
            Environment.SetEnvironmentVariable(versionProperty, originalVersion);
            fixture.Delete(recursive: true);
        }
    }

    /// <summary>Rejects actual dirty and mismatched root checkouts without changing any user repository.</summary>
    [Fact]
    public void RootGitlinkReaderRejectsDirtyAndMismatchedIsolatedCheckouts()
    {
        DirectoryInfo fixture = Directory.CreateTempSubdirectory("parties-closure-gitlink-");
        try
        {
            string sourceRoot = RepositoryRoot.Locate();
            string root = Path.Combine(fixture.FullName, "root");
            const string path = "references/Hexalith.EventStore";
            string checkout = Path.Combine(root, path);
            RunGit(fixture.FullName, "clone", "--shared", "--no-checkout", "--", sourceRoot, root);
            RunGit(fixture.FullName, "clone", "--shared", "--no-checkout", "--", RepositoryDependencyPaths.Resolve(sourceRoot, path), checkout);
            RunGit(checkout, "sparse-checkout", "set", "--no-cone", "/README.md");
            string selected = ReadSelectedRootGitlinkSha(root, path);
            RunGit(checkout, "checkout", "--detach", selected);
            (string Path, string Mode, string Digest) entry = ReadValidatedRootGitlinkEntry(root, path);
            entry.Mode.ShouldBe("160000");
            entry.Digest.ShouldBe(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"selected={selected}\ncheckout={selected}"))));
            string otherSha = RunGit(checkout, "rev-parse", $"{selected}^").Trim();
            (string Path, string Mode, string Digest) other = CreateManifestEntry(path, "160000", Encoding.UTF8.GetBytes($"selected={otherSha}\ncheckout={otherSha}"));
            foreach (string domain in new[] { "parties-snapshot-v2", "parties-dependency-set-v2" })
            {
                HashManifest(domain, [entry]).ShouldNotBe(HashManifest(domain, [other]));
            }

            string file = Path.Combine(checkout, "README.md");
            byte[] original = File.ReadAllBytes(file);
            File.AppendAllText(file, "\nFixture-only dirty checkout\n");
            Should.Throw<ShouldAssertException>(() => ReadValidatedRootGitlinkEntry(root, path));
            File.WriteAllBytes(file, original);
            ReadValidatedRootGitlinkEntry(root, path).Mode.ShouldBe("160000");

            RunGit(checkout, "checkout", "--detach", $"{selected}^");
            Should.Throw<ShouldAssertException>(() => ReadValidatedRootGitlinkEntry(root, path));
        }
        finally
        {
            fixture.Delete(recursive: true);
        }
    }

    /// <summary>Exercises actual Git, worktree modes, root gitlinks, and evaluated catalog inputs.</summary>
    [Fact]
    public void CurrentClosureIdentityCanBeDerivedWithoutClaimingQualification()
    {
        bool qualify = string.Equals(Environment.GetEnvironmentVariable("HEXALITH_EMIT_CLOSURE_IDENTITY"), "true", StringComparison.Ordinal);
        EmitCurrentClosureIdentity(RepositoryRoot.Locate(), qualify, static output => TestContext.Current.TestOutputHelper?.WriteLine(output));
    }

    private static void EmitCurrentClosureIdentity(string root, bool qualify, Action<string> output)
    {
        (string snapshot, string dependencies) = ReadCurrentClosureIdentity(root, requireImmutableConsumption: qualify);

        Regex.IsMatch(snapshot, @"\A[0-9a-f]{64}\z", RegexOptions.CultureInvariant).ShouldBeTrue();
        Regex.IsMatch(dependencies, @"\A[0-9a-f]{64}\z", RegexOptions.CultureInvariant).ShouldBeTrue();
        string prefix = qualify ? string.Empty : "diagnostic_";
        output($"{prefix}parties_snapshot={snapshot} {prefix}dependency_set={dependencies} immutable_consumption={qualify.ToString().ToLowerInvariant()}");
    }

    private static string CreateStampedValidationSummary(string changedCheck, string status, string evidence, string validEvidence)
        => "### Validation receipts\n\n| Check | Result | Evidence |\n| --- | --- | --- |\n"
            + string.Join('\n', RequiredValidationReceiptChecks.Select(check =>
                $"| {check} | {(check == changedCheck ? status : "Pass")} | {(check == changedCheck ? evidence : validEvidence)} |"));

    private static Dictionary<string, string> CreatePassingValidationReceipts()
        => new(StringComparer.Ordinal)
        {
            ["Warning and nested-submodule policy"] = "Pass",
            ["Release solution build"] = "Pass",
            ["All .NET test projects"] = "Pass",
            ["Package/API and package-only consumers"] = "Pass",
            ["npm install and typecheck"] = "Pass",
            ["Playwright accessibility"] = "Pass",
        };

    /// <summary>
    /// Collects test classes that can actually discharge an invariant: the class must be declared in
    /// a project that <c>scripts/test.ps1</c> runs, and that file must declare at least one test
    /// method. A bare name match anywhere under <c>tests/</c> is not evidence — a comment, a support
    /// host, or an abstract helper would all have satisfied it.
    /// </summary>
    private static HashSet<string> CollectExecutableTestClasses(string root)
    {
        HashSet<string> runnableProjects = Regex.Matches(
                Read(root, "scripts/test.ps1"),
                "tests/(?<project>[^/]+Tests)/[^\"\\r\\n]+\\.csproj",
                RegexOptions.CultureInvariant)
            .Select(static match => match.Groups["project"].Value)
            .ToHashSet(StringComparer.Ordinal);
        runnableProjects.ShouldNotBeEmpty("scripts/test.ps1 must enumerate the runnable test projects.");

        string testsRoot = Path.Combine(root, "tests");
        HashSet<string> classes = new(StringComparer.Ordinal);
        foreach (string path in Directory.GetFiles(testsRoot, "*.cs", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(testsRoot, path);
            string[] segments = relativePath.Split(Path.DirectorySeparatorChar);
            if (segments.Length < 2 || !runnableProjects.Contains(segments[0]))
            {
                continue;
            }

            if (relativePath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                || relativePath.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string text = File.ReadAllText(path);
            if (!text.Contains("[Fact]", StringComparison.Ordinal) && !text.Contains("[Theory]", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (Match match in Regex.Matches(
                         text,
                         @"\bclass\s+(?<name>[A-Za-z][A-Za-z0-9_]*Tests)\b",
                         RegexOptions.CultureInvariant))
            {
                classes.Add(match.Groups["name"].Value);
            }
        }

        return classes;
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
                // csproj precedes cs: with cs first, "Foo.csproj" matches only ".cs" and the
                // truncated path is then reported as a missing evidence anchor.
                @"(?<path>(?:_bmad-output|docs|src|tests|scripts|references)/[^\s;,`]+\.(?:csproj|md|ya?ml|cs|ps1|py|sh|ts|json|razor)(?![A-Za-z0-9]))",
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
        MatchCollection ledgerEntries = Regex.Matches(
            markdown,
            @"(?ms)^### DW-\d+:[^\r\n]*\r?\n.*?(?=^#{1,3} |\z)",
            RegexOptions.CultureInvariant);

        // Legacy multi-line bulleted form (still exercised by
        // DeferralParserPreservesReorderedAndMissingFieldsForSpecificDiagnostics): a top-level
        // "- deferral_id:" line followed by indented sub-field lines. It cannot bypass a current
        // ledger entry's status merely because migrated evidence retains the old bullet format.
        IEnumerable<string> bulletedBlocks = Regex.Matches(
                markdown,
                @"(?ms)^- deferral_id:.*?(?=^- |^#{1,6} |\z)",
                RegexOptions.CultureInvariant)
            .Where(match => !ledgerEntries.Any(entry => match.Index >= entry.Index && match.Index < entry.Index + entry.Length))
            .Select(static match => match.Value);

        // Current ledger entries keep the authoritative status outside the packed reason.
        // Read the whole entry, bounded by the next heading, so completed history cannot borrow
        // an open status from adjacent work or stay accepted through a stale packed token.
        IEnumerable<string> ledgerBlocks = ledgerEntries
            .Select(static match => match.Value)
            .Where(static block => Regex.IsMatch(block, @"(?m)^reason:.*\bdeferral_id:", RegexOptions.CultureInvariant));

        IEnumerable<ClosureDeferral> legacy = bulletedBlocks
            .Select(static block => ParseDeferral(block, ReadDeferralField(block, "status")))
            .Where(static deferral => deferral.Status is "accepted" or "open");
        IEnumerable<ClosureDeferral> current = ledgerBlocks
            .Select(static block =>
            {
                MatchCollection statuses = Regex.Matches(block, @"(?m)^status:[ \t]*(?<value>[^\r\n]*)", RegexOptions.CultureInvariant);
                string status = statuses.Count == 1 ? statuses[0].Groups["value"].Value.Trim() : string.Empty;
                MatchCollection reasons = Regex.Matches(block, @"(?m)^reason:[ \t]*(?<value>[^\r\n]*)", RegexOptions.CultureInvariant);
                string reason = reasons.Count == 1 ? reasons[0].Groups["value"].Value : string.Empty;
                return ParseDeferral(reason, status);
            })
            .Where(static deferral => string.Equals(deferral.Status, "open", StringComparison.Ordinal));

        return legacy.Concat(current)
            .OrderBy(static deferral => Array.IndexOf(ExpectedDeferrals, deferral.Id))
            .ToArray();
    }

    private static ClosureDeferral ParseDeferral(string block, string status)
        => new(
            ReadDeferralField(block, "deferral_id"),
            status,
            ReadDeferralField(block, "owner"),
            ReadDeferralField(block, "exit_proof"),
            ReadDeferralField(block, "rollback"),
            ReadDeferralField(block, "evidence"));

    private static string ReadDeferralField(string block, string field)
    {
        // Field markers appear either at the start of an indented bullet line (legacy form) or
        // preceded by whitespace mid-line among several fields packed onto one line (current
        // form). An unquoted value's end is bounded by the next "field: " marker or end of line/
        // block, so a value on a shared line does not swallow the fields that follow it.
        MatchCollection matches = Regex.Matches(
            block,
            $@"(?:^|(?<=\s))(?:-\s*)?{Regex.Escape(field)}:\s*(?:`(?<quoted>[^`]*)`|(?<plain>\S.*?))(?=\s+[A-Za-z][A-Za-z_]*:\s|\s*$)",
            RegexOptions.CultureInvariant | RegexOptions.Multiline);
        return matches.Count == 1
            ? (matches[0].Groups["quoted"].Success ? matches[0].Groups["quoted"].Value : matches[0].Groups["plain"].Value.Trim())
            : string.Empty;
    }

    private static Dictionary<string, string> ParseValidationReceipts(string summary)
    {
        string section = ReadLatestValidationReceiptSection(summary);

        Dictionary<string, string> receipts = new(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(
                     section,
                     @"(?m)^\|\s*(?<check>[^|]+?)\s*\|\s*(?<result>[^|]+?)\s*\|",
                     RegexOptions.CultureInvariant))
        {
            string check = match.Groups["check"].Value.Trim();
            if (string.Equals(check, "Check", StringComparison.Ordinal)
                || Regex.IsMatch(check, @"^[:\-\s]+$", RegexOptions.CultureInvariant))
            {
                continue;
            }

            // Fail loudly on a duplicated check name: silent last-wins lets a later Pass row
            // overwrite an earlier Blocked one.
            receipts.ContainsKey(check).ShouldBeFalse($"Duplicate validation receipt row: {check}");
            receipts[check] = match.Groups["result"].Value.Trim();
        }

        receipts.ShouldNotBeEmpty("Validation receipt table is missing.");
        return receipts;
    }

    private static string ReadValidationReceiptEvidence(string summary, string check)
    {
        string section = ReadLatestValidationReceiptSection(summary);
        Match match = Regex.Match(
            section,
            $@"(?m)^\|\s*{Regex.Escape(check)}\s*\|[^|]*\|\s*(?<evidence>[^|\r\n]+?)\s*\|",
            RegexOptions.CultureInvariant);
        match.Success.ShouldBeTrue($"Validation receipt evidence is missing: {check}");
        return match.Groups["evidence"].Value.Trim();
    }

    private static string ReadLatestValidationReceiptSection(string summary)
    {
        // Match a real heading LINE, not a substring. A plain LastIndexOf also matches any prose
        // that merely quotes the heading text -- including this file's own narrative describing the
        // parser -- which silently reselects the section and makes the gate read the wrong table.
        MatchCollection headings = Regex.Matches(
            summary,
            @"(?m)^### Validation receipts[ \t]*\r?$",
            RegexOptions.CultureInvariant);
        headings.Count.ShouldBeGreaterThan(0, "Validation receipt heading is missing.");
        Match heading = headings[^1];
        int headingIndex = heading.Index;

        // Bound the section at the next heading. Slicing to end of file swallows every later table,
        // so superseded blocker and remediation rows would decide the closure gate.
        int contentIndex = headingIndex + heading.Length;
        Match nextHeading = Regex.Match(
            summary[contentIndex..],
            @"(?m)^ {0,3}#{1,3}(?:[ \t]+|\r?$)",
            RegexOptions.CultureInvariant);
        return nextHeading.Success
            ? summary[headingIndex..(contentIndex + nextHeading.Index)]
            : summary[headingIndex..];
    }

    private static string[] DescribeMissingReceiptGaps(IReadOnlyDictionary<string, string> receipts)
        => RequiredValidationReceiptChecks
            .Where(check => !receipts.ContainsKey(check))
            .Select(static check => $"{check}:missing")
            .ToArray();

    private static string[] DescribeReceiptGaps(IReadOnlyDictionary<string, string> receipts)
        => DescribeMissingReceiptGaps(receipts).Concat(receipts
            .Where(static receipt => !IsCleanPass(receipt.Value))
            .Select(static receipt => $"{receipt.Key}:{receipt.Value}"))
            .ToArray();

    /// <summary>Uses live evidence until closure is committed, then locates that done epoch's first commit.</summary>
    private static string? ReadClosureCommit(string root)
    {
        if (NormalizeStoryStatus(ReadFrontmatterValue(Read(root, SpecPath), "status")) != "done"
            || ReadFrontmatterValue(RunGit(root, "show", $"HEAD:{SpecPath}"), "status") != "done")
        {
            return null;
        }

        string? closure = null;
        foreach (string revision in SplitLines(RunGit(root, "log", "--first-parent", "--format=%H", "HEAD", "--", SpecPath)))
        {
            if (ReadFrontmatterValue(RunGit(root, "show", $"{revision}:{SpecPath}"), "status") != "done")
            {
                closure.ShouldNotBeNull("A committed done transition must have a closure commit.");
                return closure;
            }
            closure = revision;
        }

        Assert.SkipUnless(RunGit(root, "rev-parse", "--is-shallow-repository").Trim() == "false",
            "Story 8.10 closure history is shallow; check out with fetch-depth 0 to verify its closure commit.");
        closure.ShouldNotBeNull("No Story 8.10 closure commit was found.");
        return closure;
    }

    private static string ReadClosureSummary(string root, string? revision)
        => revision is null ? Read(root, TestSummaryPath) : RunGit(root, "show", $"{revision}:{TestSummaryPath}");

    private static (string Snapshot, string Dependencies) ReadClosureIdentity(string root, string? revision)
        => revision is null ? ReadCurrentClosureIdentity(root) : ReadCommittedClosureIdentity(root, revision);

    /// <summary>Reads closed evidence from Git objects; subsequent worktree and dependency changes are irrelevant.</summary>
    private static (string Snapshot, string Dependencies) ReadCommittedClosureIdentity(string root, string revision)
    {
        Dictionary<string, (string Path, string Mode, string Digest)> files = new(StringComparer.Ordinal);
        foreach (string entry in RunGit(root, "ls-tree", "-r", "-z", revision).Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = entry.Split('\t', 2);
            string[] metadata = parts[0].Split(' ');
            string path = parts[1];
            if (metadata[0] == "160000")
            {
                files.Add(path, CreateManifestEntry(path, "160000", Encoding.UTF8.GetBytes($"selected={metadata[2]}\ncheckout={metadata[2]}")));
            }
            else if (IsClosureBookkeepingPath(path))
            {
                files.Add(path, CreateNormalizedBlobEntry(root, path, metadata[0], ReadGitBlobBytes(root, metadata[2]), applyFilters: false));
            }
            else if (metadata[0] == "120000")
            {
                files.Add(path, CreateManifestEntry(path, "120000", ReadGitBlobBytes(root, metadata[2])));
            }
            else
            {
                files.Add(path, (path, metadata[0], metadata[2]));
            }
        }

        List<(string Path, string Mode, string Digest)> dependencies = files.Values.Where(static entry => entry.Mode == "160000").ToList();
        dependencies.AddRange(files.Values.Where(static entry => entry.Path is ".gitmodules" or "global.json"
            || (!entry.Path.Contains('/') && (entry.Path.EndsWith(".props", StringComparison.Ordinal) || entry.Path.EndsWith(".targets", StringComparison.Ordinal)))));
        const string buildsPath = "references/Hexalith.Builds";
        string buildsRevision = ReadSelectedRootGitlinkSha(root, buildsPath, revision);
        string buildsRoot = RepositoryDependencyPaths.Resolve(root, buildsPath);
        Assert.SkipUnless(Directory.Exists(buildsRoot)
            && (File.Exists(Path.Combine(buildsRoot, ".git")) || Directory.Exists(Path.Combine(buildsRoot, ".git"))),
            $"Closure Builds repository is unavailable at {buildsRoot}; initialize the declared root dependency and fetch object {buildsRevision} before verifying closed evidence.");
        Assert.SkipUnless(TryRunGit(buildsRoot, out _, "cat-file", "-e", $"{buildsRevision}^{{commit}}"),
            $"Closure Builds object {buildsRevision} is unreachable; fetch its full history before verifying closed evidence.");
        dependencies.Add(($"{buildsPath}/Props/Directory.Packages.props", "100644",
            RunGit(buildsRoot, "rev-parse", $"{buildsRevision}:Props/Directory.Packages.props").Trim()));

        DirectoryInfo fixture = Directory.CreateTempSubdirectory("parties-closed-identity-");
        try
        {
            string snapshotRoot = Path.Combine(fixture.FullName, "snapshot");
            MaterializeGitTree(root, revision, snapshotRoot);
            MaterializeGitTree(buildsRoot, buildsRevision, Path.Combine(snapshotRoot, buildsPath));
            foreach ((string package, string version) in EffectivePackageVersions.ReadPackages(snapshotRoot, isolateEnvironment: true))
            {
                dependencies.Add(CreateManifestEntry($"evaluated-package/{package}", "version", Encoding.UTF8.GetBytes(version)));
            }
        }
        finally
        {
            fixture.Delete(recursive: true);
        }
        return (HashManifest("parties-snapshot-v2", files.Values), HashManifest("parties-dependency-set-v2", dependencies));
    }

    /// <summary>Reads raw object bytes without a text decoder consuming a UTF-8 BOM.</summary>
    private static byte[] ReadGitBlobBytes(string root, string objectId)
    {
        string temporary = Path.GetTempFileName();
        try
        {
            PackageTestProcessResult result = PackageTestProcess.Run("git", ["cat-file", "blob", objectId], root, 30_000, standardOutputFile: temporary);
            result.ExitCode.ShouldBe(0, result.Diagnostic);
            return File.ReadAllBytes(temporary);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    /// <summary>Materializes exact blobs; archive attributes and ambient clean/smudge filters never apply.</summary>
    private static void MaterializeGitTree(string root, string revision, string destination)
    {
        var entries = RunGit(root, "ls-tree", "-r", "-z", revision).Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Select(static entry => entry.Split('\t', 2))
            .Select(static entry => (Metadata: entry[0].Split(' '), Path: entry[1]))
            .Where(static entry => entry.Metadata[1] == "blob")
            .GroupBy(static entry => entry.Metadata[2], StringComparer.Ordinal).ToArray();
        string temporary = Path.GetTempFileName();
        try
        {
            PackageTestProcessResult result = PackageTestProcess.Run("git", ["cat-file", "--batch"], root, 30_000,
                standardInput: string.Join('\n', entries.Select(static entry => entry.Key)) + "\n", standardOutputFile: temporary);
            result.ExitCode.ShouldBe(0, result.Diagnostic);
            using FileStream blobs = File.OpenRead(temporary);
            foreach (var group in entries)
            {
                StringBuilder header = new();
                int next;
                while ((next = blobs.ReadByte()) != '\n' && next >= 0)
                {
                    header.Append((char)next);
                }
                string[] fields = header.ToString().Split(' ');
                fields.Length.ShouldBe(3);
                fields[0].ShouldBe(group.Key);
                fields[1].ShouldBe("blob");
                byte[] content = new byte[int.Parse(fields[2], CultureInfo.InvariantCulture)];
                blobs.ReadExactly(content);
                blobs.ReadByte().ShouldBe((int)'\n');
                foreach (var entry in group)
                {
                    string path = Path.Combine(destination, entry.Path);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    if (entry.Metadata[0] == "120000")
                    {
                        File.CreateSymbolicLink(path, Encoding.UTF8.GetString(content));
                    }
                    else
                    {
                        File.WriteAllBytes(path, content);
                    }
                }
            }
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    /// <summary>Derives identities only when a required row claims successful current qualification.</summary>
    private static string[] DescribeCurrentReceiptIdentityGaps(string summary, IReadOnlyDictionary<string, string> receipts, Func<(string Snapshot, string Dependencies)> readIdentity)
    {
        if (!RequiredValidationReceiptChecks.Any(check => receipts.TryGetValue(check, out string? result) && IsCleanPass(result)))
        {
            return [];
        }

        (string snapshot, string dependencies) = readIdentity();
        return DescribeReceiptIdentityGaps(summary, receipts, snapshot, dependencies);
    }

    private static string[] DescribeReceiptIdentityGaps(
        string summary,
        IReadOnlyDictionary<string, string> receipts,
        string snapshot,
        string dependencies)
    {
        List<string> gaps = [];
        foreach (string check in RequiredValidationReceiptChecks)
        {
            if (!receipts.TryGetValue(check, out string? result) || !IsCleanPass(result))
            {
                continue;
            }

            string evidence = ReadValidationReceiptEvidence(summary, check);
            foreach ((string name, string expected) in new[] { ("parties_snapshot", snapshot), ("dependency_set", dependencies) })
            {
                MatchCollection names = Regex.Matches(evidence, $@"(?<![A-Za-z0-9_]){name}(?![A-Za-z0-9_])", RegexOptions.CultureInvariant);
                MatchCollection stamps = Regex.Matches(evidence, $@"(?<![A-Za-z0-9_]){name}=(?<value>[0-9a-f]{{64}})(?=$|[\s`])", RegexOptions.CultureInvariant);
                if (names.Count != 1 || stamps.Count != 1 || stamps[0].Groups["value"].Value != expected)
                {
                    gaps.Add($"{check}:{name}:expected={expected}");
                }
            }
        }

        return gaps.ToArray();
    }

    /// <summary>
    /// Hashes all root worktree files, including unignored untracked files, plus selected/checked-out
    /// root gitlinks. Receipt payload and three closure status values are normalized as bookkeeping.
    /// No index or repository object is written; these SHA256 digests are not Git tree object IDs.
    /// </summary>
    private static (string Snapshot, string Dependencies) ReadCurrentClosureIdentity(string root, bool requireImmutableConsumption = true)
    {
        Dictionary<string, string> trackedModes = ReadTrackedModes(root);
        List<(string Path, string Mode, string Digest)> gitlinks = [];
        HashSet<string> rootGitlinkPaths = new(StringComparer.Ordinal);
        foreach (string declaration in SplitLines(RunGit(root, "config", "--file", ".gitmodules", "--get-regexp", @"^submodule\..*\.path$")))
        {
            string path = declaration[(declaration.IndexOf(' ') + 1)..];
            rootGitlinkPaths.Add(path).ShouldBeTrue("Root submodule paths must be unique.");
            gitlinks.Add(requireImmutableConsumption
                ? ReadValidatedRootGitlinkEntry(root, path)
                : ReadObservedRootGitlinkEntry(root, path));
        }

        List<(string Path, string Mode, string Digest)> files = ReadRootWorktreeManifest(root, trackedModes, rootGitlinkPaths);

        List<(string Path, string Mode, string Digest)> dependencyInputs = [.. gitlinks];
        foreach (string path in Directory.EnumerateFiles(root, "*.props").Concat(Directory.EnumerateFiles(root, "*.targets"))
                     .Select(Path.GetFileName).OfType<string>()
                     .Concat([".gitmodules", "global.json", "references/Hexalith.Builds/Props/Directory.Packages.props"]))
        {
            if (!requireImmutableConsumption && path.StartsWith("references/", StringComparison.Ordinal))
            {
                string observedPath = RepositoryDependencyPaths.Resolve(root, path);
                dependencyInputs.Add((path, File.Exists(observedPath) ? "observed-file" : "missing",
                    File.Exists(observedPath) ? Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(observedPath))) : "missing"));
            }
            else
            {
                if (path.StartsWith("references/Hexalith.Builds/", StringComparison.Ordinal))
                {
                    const string buildsPath = "references/Hexalith.Builds";
                    (string Path, string Mode, string Digest) catalog = ReadWorktreeManifestEntry(
                        RepositoryDependencyPaths.Resolve(root, buildsPath), path[(buildsPath.Length + 1)..], "100644");
                    dependencyInputs.Add((path, catalog.Mode, catalog.Digest));
                }
                else
                {
                    if (File.Exists(Path.Combine(root, path)) || new FileInfo(Path.Combine(root, path)).LinkTarget is not null)
                    {
                        dependencyInputs.Add(ReadWorktreeManifestEntry(root, path, trackedModes.GetValueOrDefault(path, "100644")));
                    }
                }
            }
        }

        foreach ((string package, string version) in EffectivePackageVersions.ReadPackages(root))
        {
            dependencyInputs.Add(CreateManifestEntry($"evaluated-package/{package}", "version", Encoding.UTF8.GetBytes(version)));
        }

        return (HashManifest(requireImmutableConsumption ? "parties-snapshot-v2" : "parties-diagnostic-snapshot-v2", [.. files, .. gitlinks]),
            HashManifest(requireImmutableConsumption ? "parties-dependency-set-v2" : "parties-diagnostic-dependency-set-v2", dependencyInputs));
    }

    private static Dictionary<string, string> ReadTrackedModes(string root)
    {
        Dictionary<string, string> trackedModes = new(StringComparer.Ordinal);
        foreach (string entry in RunGit(root, "ls-files", "--stage", "-z").Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] fields = entry.Split('\t', 2);
            string[] metadata = fields[0].Split(' ');
            metadata[2].ShouldBe("0", "Conflicted files cannot qualify closure.");
            trackedModes.Add(fields[1], metadata[0]);
        }

        return trackedModes;
    }

    /// <summary>Matches Git's index-aware preservation of legacy CRLF/mixed blobs without text attributes.</summary>
    private static HashSet<string> ReadPreservedLineEndingPaths(string root)
        => RunGit(root, "ls-files", "--eol", "-z").Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Where(static entry => Regex.IsMatch(entry, @"\Ai/(?:crlf|mixed)\s+\S+\s+attr/[ \t]*\t", RegexOptions.CultureInvariant))
            .Select(static entry => entry[(entry.IndexOf('\t') + 1)..])
            .ToHashSet(StringComparer.Ordinal);

    private static List<(string Path, string Mode, string Digest)> ReadRootWorktreeManifest(
        string root,
        IReadOnlyDictionary<string, string> trackedModes,
        IReadOnlySet<string> rootGitlinkPaths)
    {
        List<(string Path, string Mode, string Digest)> files = [];
        bool trustMode = !TryRunGit(root, out string fileMode, "config", "--bool", "--get", "core.fileMode") || fileMode.Trim() != "false";
        HashSet<string> preserved = ReadPreservedLineEndingPaths(root);
        List<(string Path, string Mode, bool Preserve)> ordinary = [];
        foreach (string path in RunGit(root, "ls-files", "--cached", "--others", "--exclude-standard", "-z")
                     .Split('\0', StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal))
        {
            if (!rootGitlinkPaths.Contains(path))
            {
                string mode = trackedModes.GetValueOrDefault(path, "100644");
                string fullPath = Path.Combine(root, path);
                string? target = new FileInfo(fullPath).LinkTarget;
                if (!File.Exists(fullPath) && target is null)
                {
                    continue;
                }
                if (IsClosureBookkeepingPath(path) || target is not null)
                {
                    files.Add(ReadWorktreeManifestEntry(root, path, mode, trustMode, preserved.Contains(path)));
                }
                else
                {
                    ordinary.Add((path, ReadWorktreeMode(fullPath, mode, trustMode), preserved.Contains(path)));
                }
            }
        }

        foreach (IGrouping<bool, (string Path, string Mode, bool Preserve)> group in ordinary.GroupBy(static entry => entry.Preserve))
        {
            foreach ((string Path, string Mode, bool Preserve)[] batch in group.Chunk(128))
            {
                string[] arguments = group.Key
                    ? ["hash-object", "--no-filters", "--", .. batch.Select(static entry => entry.Path)]
                    : ["hash-object", "--", .. batch.Select(static entry => entry.Path)];
                string[] digests = SplitLines(RunGit(root, arguments));
                digests.Length.ShouldBe(batch.Length);
                for (int index = 0; index < batch.Length; index++)
                {
                    files.Add((batch[index].Path, batch[index].Mode, digests[index]));
                }
            }
        }

        return files;
    }

    private static string ReadFixtureSnapshot(string root)
        => HashManifest("parties-snapshot-v2", ReadRootWorktreeManifest(root, ReadTrackedModes(root), new HashSet<string>(StringComparer.Ordinal)));

    /// <summary>Hashes diagnostic source observations without producing immutable-consumption evidence.</summary>
    private static (string Path, string Mode, string Digest) ReadObservedRootGitlinkEntry(string root, string path)
    {
        string selected = ReadSelectedRootGitlinkSha(root, path);
        string actualRoot = RepositoryDependencyPaths.Resolve(root, path);
        bool mapped = RepositoryDependencyPaths.IsMappedDependency(root, path);
        bool present = File.Exists(Path.Combine(actualRoot, ".git")) || Directory.Exists(Path.Combine(actualRoot, ".git"));
        string checkout = present ? RunGit(root, "-C", actualRoot, "rev-parse", "HEAD").Trim() : "missing";
        string status = present ? RunGit(root, "-C", actualRoot, "status", "--porcelain", "--ignore-submodules=all") : "missing";
        string state = !present ? "missing" : mapped ? "mapped-source" : "checkout";
        string changes = present ? RunGit(root, "-C", actualRoot, "diff", "--binary", "HEAD", "--") : "missing";
        List<string> untracked = [];
        if (present)
        {
            foreach (string file in RunGit(root, "-C", actualRoot, "ls-files", "--others", "--exclude-standard", "-z")
                         .Split('\0', StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal))
            {
                string fullPath = Path.Combine(actualRoot, file);
                if (File.Exists(fullPath)) untracked.Add(file + "=" + Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(fullPath))));
            }
        }
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"{path}: selected={selected} actual={checkout} state={state} dirty={!string.IsNullOrWhiteSpace(status)} immutable_consumption=false");
        return CreateManifestEntry(path, "observed-gitlink",
            Encoding.UTF8.GetBytes($"selected={selected}\nactual={checkout}\nstate={state}\nstatus={status}\nchanges={changes}\nuntracked={string.Join('\n', untracked)}"));
    }

    private static (string Path, string Mode, string Digest) ReadValidatedRootGitlinkEntry(string root, string path)
    {
        string fullPath = RepositoryDependencyPaths.Resolve(root, path);
        (fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || RepositoryDependencyPaths.IsMappedDependency(root, path)).ShouldBeTrue(path);
        (File.Exists(Path.Combine(fullPath, ".git")) || Directory.Exists(Path.Combine(fullPath, ".git")))
            .ShouldBeTrue($"Initialize only the declared root submodule before qualification: {path}");
        string selected = ReadSelectedRootGitlinkSha(root, path);
        string checkout = RunGit(root, "-C", fullPath, "rev-parse", "--verify", "HEAD").Trim();
        checkout.ShouldBe(selected, $"Root checkout must match the selected gitlink: {path}");
        RunGit(root, "-C", fullPath, "status", "--porcelain", "--untracked-files=normal", "--ignore-submodules=all")
            .ShouldBeEmpty($"Root checkout must be clean before qualification: {path}");
        return CreateManifestEntry(path, "160000", Encoding.UTF8.GetBytes($"selected={selected}\ncheckout={checkout}"));
    }

    private static (string Path, string Mode, string Digest) ReadWorktreeManifestEntry(string root, string path, string indexedMode, bool? trustFileMode = null, bool? preserveLineEndings = null)
    {
        string fullPath = Path.Combine(root, path);
        string? target = new FileInfo(fullPath).LinkTarget;
        if (target is not null)
        {
            return CreateManifestEntry(path, "120000", Encoding.UTF8.GetBytes(target));
        }

        if (!File.Exists(fullPath))
        {
            return CreateManifestEntry(path, "missing", []);
        }

        bool trustMode = trustFileMode ?? (!TryRunGit(root, out string configured, "config", "--bool", "--get", "core.fileMode") || configured.Trim() != "false");
        string mode = ReadWorktreeMode(fullPath, indexedMode, trustMode);
        bool preserve = preserveLineEndings ?? ReadPreservedLineEndingPaths(root).Contains(path);

        return IsClosureBookkeepingPath(path)
            ? CreateNormalizedBlobEntry(root, path, mode, File.ReadAllBytes(fullPath), applyFilters: !preserve)
            : (path, mode, (preserve
                ? RunGit(root, "hash-object", "--no-filters", "--", fullPath)
                : RunGit(root, "hash-object", $"--path={path}", "--", fullPath)).Trim());
    }

    private static string ReadWorktreeMode(string fullPath, string indexedMode, bool trustFileMode)
        => !OperatingSystem.IsWindows() && trustFileMode
            ? (File.GetUnixFileMode(fullPath) & UnixFileMode.UserExecute) != 0 ? "100755" : "100644"
            : indexedMode;

    private static string ReadSelectedRootGitlinkSha(string root, string path, string revision = "HEAD")
    {
        Match selected = Regex.Match(RunGit(root, "ls-tree", revision, "--", path), @"\A160000 commit (?<sha>[0-9a-f]{40})\t", RegexOptions.CultureInvariant);
        selected.Success.ShouldBeTrue($"No committed root gitlink: {path}");
        return selected.Groups["sha"].Value;
    }

    private static bool IsClosureBookkeepingPath(string path)
        => path is TestSummaryPath or SpecPath or SprintStatusPath;

    private static (string Path, string Mode, string Digest) CreateNormalizedBlobEntry(string root, string path, string mode, byte[] content, bool applyFilters)
    {
        string temporary = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(temporary, NormalizeManifestContent(path, mode, content));
            string digest = applyFilters
                ? RunGit(root, "hash-object", $"--path={path}", "--", temporary).Trim()
                : RunGit(root, "hash-object", "--no-filters", "--", temporary).Trim();
            return (path, mode, digest);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private static (string Path, string Mode, string Digest) CreateManifestEntry(string path, string mode, byte[] content)
        => (path, mode, Convert.ToHexStringLower(SHA256.HashData(NormalizeManifestContent(path, mode, content))));

    private static byte[] NormalizeManifestContent(string path, string mode, byte[] content)
    {
        bool hasBom = content.AsSpan().StartsWith(Encoding.UTF8.Preamble);
        if (hasBom)
        {
            content = content[Encoding.UTF8.Preamble.Length..];
        }
        if (string.Equals(path, TestSummaryPath, StringComparison.Ordinal) && mode != "missing")
        {
            string summary = Encoding.UTF8.GetString(content);
            string section = ReadLatestValidationReceiptSection(summary);
            int start = summary.LastIndexOf(section, StringComparison.Ordinal);
            content = Encoding.UTF8.GetBytes(summary[..start] + "<!-- closure-receipt-payload:v1 -->\n" + summary[(start + section.Length)..]);
        }
        else if (string.Equals(path, SpecPath, StringComparison.Ordinal))
        {
            string spec = Encoding.UTF8.GetString(content);
            Match frontmatter = Regex.Match(spec, @"\A---\r?\n(?<header>[\s\S]*?)\r?\n---(?:\r?\n|\z)", RegexOptions.CultureInvariant);
            if (frontmatter.Success)
            {
                Group header = frontmatter.Groups["header"];
                content = Encoding.UTF8.GetBytes(spec[..header.Index]
                    + NormalizeClosureStatusValue(header.Value, "status:", "in-review|review|done")
                    + spec[(header.Index + header.Length)..]);
            }
        }
        else if (string.Equals(path, SprintStatusPath, StringComparison.Ordinal))
        {
            string sprint = NormalizeClosureStatusValue(Encoding.UTF8.GetString(content), "  epic-8:", "in-progress|done");
            sprint = NormalizeClosureStatusValue(sprint, "  8-10-final-readiness-documentation-and-retirement-gate:", "review|done");
            content = Encoding.UTF8.GetBytes(sprint);
        }

        return hasBom ? [.. Encoding.UTF8.Preamble, .. content] : content;
    }

    private static string NormalizeClosureStatusValue(string content, string key, string allowedValues)
    {
        MatchCollection matches = Regex.Matches(content,
            $@"(?m)^{Regex.Escape(key)}[ \t]*(?:'(?<value>{allowedValues})'|""(?<value>{allowedValues})""|(?<value>{allowedValues}))[ \t]*\r?$",
            RegexOptions.CultureInvariant);
        if (matches.Count != 1)
        {
            return content;
        }

        Group value = matches[0].Groups["value"];
        return content[..value.Index] + "<closure-status:v1>" + content[(value.Index + value.Length)..];
    }

    private static string HashManifest(string kind, IEnumerable<(string Path, string Mode, string Digest)> entries)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(kind + "\0"));
        foreach ((string path, string mode, string digest) in entries.OrderBy(static entry => entry.Path, StringComparer.Ordinal))
        {
            hash.AppendData(Encoding.UTF8.GetBytes($"{path}\0{mode}\0{digest}\0"));
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    /// <summary>
    /// Accepts only an exact <c>Pass</c> Result after trimming bold markup and spaces. Any qualified
    /// pass ("Pass with errors", "Pass (not run)", "Pass (partial)", "Pass (unvalidated)", ...) is a gap:
    /// a deny-list of blocker words would admit every qualifier it does not happen to name.
    /// </summary>
    private static bool IsCleanPass(string result)
        => string.Equals(result.Trim('*', ' '), "Pass", StringComparison.Ordinal);

    /// <summary>
    /// Requires the selected FrontComposer source identity in the Playwright accessibility evidence,
    /// but only when that Result is a clean <c>Pass</c>. An honest red or unvalidated receipt need not
    /// carry the stamp, and must stay recordable without breaking the structural check.
    /// </summary>
    private static string[] DescribeAccessibilityStampGaps(string summary, IReadOnlyDictionary<string, string> receipts, string expectedFrontComposerSha)
        => IsCleanPass(receipts["Playwright accessibility"])
            && !ReadValidationReceiptEvidence(summary, "Playwright accessibility")
                .Contains(expectedFrontComposerSha, StringComparison.Ordinal)
            ? ["Playwright accessibility:frontcomposer-stamp"]
            : [];

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
}
