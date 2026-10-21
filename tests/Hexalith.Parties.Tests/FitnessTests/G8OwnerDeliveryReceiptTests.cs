using System.Text.RegularExpressions;

using Shouldly;

using static Hexalith.Parties.Tests.FitnessTests.RepositoryTestProcess;

namespace Hexalith.Parties.Tests.FitnessTests;

/// <summary>Binds the historical G8-A receipt to immutable producer and consumer Git objects.</summary>
public sealed class G8OwnerDeliveryReceiptTests
{
    private const string ProducerBase = "9b9c776791c149cab26c795a476d23d3d11f7796";
    private const string Delivery = "e7cf91fa714b780d60eb129722f4ab82fc7b0b26";
    private const string Integration = "a7af77ecf141278d891b3ef64b03c0da7d9dab74";
    private const string Consumer = "8c7b4e6ddb2bce9a0d3041cc2e8f3d6153e45c3c";
    private const string ConsumerGitlink = "fa2d1c9910f8976553adb33dcdb1c9ff2ea75594";
    private const string ProducerPath = "references/Hexalith.EventStore";
    private const string TestClass = "HexalithEventStoreJwtAuthenticationTests";
    private const string TestPath = "tests/Hexalith.EventStore.AppHost.Tests/Configuration/" + TestClass + ".cs";
    private const string Lane = "dotnet tests/Hexalith.EventStore.AppHost.Tests/bin/Debug/net10.0/Hexalith.EventStore.AppHost.Tests.dll -class Hexalith.EventStore.AppHost.Tests.Configuration." + TestClass;
    private const string WorktreeCommand = "git worktree add --detach /home/administrator/projects/hexalith/parties/references/.g8a-eventstore-review-e7cf91fa " + Delivery;
    private const string DiffCommand = "git diff-tree --no-commit-id --name-status -r " + Delivery;
    private static readonly string[] Inventory =
    [
        "docs/reference/nuget-packages.md",
        "src/Hexalith.EventStore.Aspire/HexalithEventStoreJwtAuthenticationOptions.cs",
        "src/Hexalith.EventStore.Aspire/HexalithEventStoreSecurityExtensions.cs",
        TestPath,
    ];

    /// <summary>Verifies provenance without claiming a fresh producer run or consumer adoption.</summary>
    [Fact]
    public void HistoricalOwnerReceipt_MatchesTheReferencedGitObjectsAndFocusedLane()
    {
        string root = RepositoryRoot.Locate();
        string receipt = ReadReceipt(root);
        ReceiptGaps(receipt).ShouldBeEmpty();
        RunGit(root, "-C", ProducerPath, "rev-parse", $"{Delivery}^").Trim().ShouldBe(ProducerBase);
        RunGit(root, "-C", ProducerPath, "merge-base", "--is-ancestor", Delivery, Integration).ShouldBeEmpty();
        RunGit(root, "ls-tree", Consumer, ProducerPath).Trim()
            .ShouldBe($"160000 commit {ConsumerGitlink}\t{ProducerPath}");
        RunGit(root, "-C", ProducerPath, "diff-tree", "--no-commit-id", "--name-only", "-r", Delivery)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ShouldBe(Inventory);

        // Read the exact delivery blobs, never a later producer checkout's source.
        string source = RunGit(root, "-C", ProducerPath, "show", $"{Delivery}:{TestPath}");
        source.ShouldContain($"public sealed class {TestClass}");
        source.ShouldContain("namespace Hexalith.EventStore.AppHost.Tests.Configuration;");
        source.ShouldNotContain("[MemberData");
        source.ShouldNotContain("[ClassData");
        int cases = Regex.Matches(source, @"(?m)^\s*\[(?:Fact|InlineData)(?:\]|\()").Count;
        cases.ShouldBe(26, "The recorded 26-case lane must exist at the recorded delivery.");
        string options = RunGit(root, "-C", ProducerPath, "show", $"{Delivery}:{Inventory[1]}");
        foreach (string property in new[] { "PrimaryAudience", "ValidAudiences", "ExternalAuthority", "ExternalIssuer" })
        {
            options.ShouldContain(property);
        }
        RunGit(root, "-C", ProducerPath, "show", $"{Delivery}:{Inventory[2]}")
            .ShouldContain("WithEventStoreJwtAuthentication(");
    }

    /// <summary>Changed or incomplete receipt claims fail independently of the matrix's general rows.</summary>
    /// <param name="scenario">The tampered provenance claim.</param>
    [Theory]
    [InlineData("base")]
    [InlineData("delivery")]
    [InlineData("integration")]
    [InlineData("consumer")]
    [InlineData("gitlink")]
    [InlineData("abbreviated")]
    [InlineData("missing-inventory")]
    [InlineData("extra-inventory")]
    [InlineData("duplicate-inventory")]
    [InlineData("lane")]
    [InlineData("count")]
    [InlineData("conflicting-base")]
    [InlineData("conflicting-delivery")]
    [InlineData("conflicting-consumer")]
    [InlineData("conflicting-integration")]
    [InlineData("conflicting-gitlink")]
    [InlineData("worktree-command")]
    [InlineData("diff-command")]
    [InlineData("remote-command")]
    [InlineData("remote-result")]
    [InlineData("success-in-command")]
    public void HistoricalOwnerReceipt_RejectsTamperedClaims(string scenario)
    {
        string receipt = ReadReceipt(RepositoryRoot.Locate());
        string altered = scenario switch
        {
            "base" => receipt.Replace(ProducerBase, new string('a', 40), StringComparison.Ordinal),
            "delivery" => receipt.Replace(Delivery, new string('a', 40), StringComparison.Ordinal),
            "integration" => receipt.Replace(Integration, new string('a', 40), StringComparison.Ordinal),
            "consumer" => receipt.Replace(Consumer, new string('a', 40), StringComparison.Ordinal),
            "gitlink" => receipt.Replace(ConsumerGitlink, new string('a', 40), StringComparison.Ordinal),
            "abbreviated" => receipt.Replace(Delivery, Delivery[..8], StringComparison.Ordinal),
            "missing-inventory" => receipt.Replace(Inventory[0] + "\n", string.Empty, StringComparison.Ordinal),
            "extra-inventory" => receipt.Replace(Inventory[0], Inventory[0] + "\nunrelated.cs", StringComparison.Ordinal),
            "duplicate-inventory" => receipt.Replace(Inventory[0], Inventory[0] + "\n" + Inventory[0], StringComparison.Ordinal),
            "lane" => receipt.Replace(Lane, Lane.Replace(TestClass, "UnrelatedTests", StringComparison.Ordinal), StringComparison.Ordinal),
            "count" => receipt.Replace("Passed 26/26", "Passed 25/25", StringComparison.Ordinal),
            "conflicting-base" => receipt + "\n- Producer base: Hexalith.EventStore\n  `" + new string('a', 40) + "`.\n",
            "conflicting-delivery" => receipt + "\n- Producer delivery: Hexalith.EventStore\n  `" + new string('a', 40) + "`\n",
            "conflicting-consumer" => receipt + "\n- Consumer/rollback identity: Parties\n  `" + new string('a', 40) + "`\n",
            "conflicting-integration" => receipt + "\nAdditional integration commit `" + new string('a', 40) + "`.\n",
            "conflicting-gitlink" => receipt + "\nAdditional EventStore gitlink `" + new string('a', 40) + "`.\n",
            "worktree-command" => receipt.Replace(WorktreeCommand, WorktreeCommand.Replace(Delivery, new string('a', 40), StringComparison.Ordinal), StringComparison.Ordinal),
            "diff-command" => receipt.Replace(DiffCommand, DiffCommand.Replace(Delivery, new string('a', 40), StringComparison.Ordinal), StringComparison.Ordinal),
            "remote-command" => receipt.Replace($"with owner commit `{Delivery}`", $"with owner commit `{new string('a', 40)}`", StringComparison.Ordinal),
            "remote-result" => receipt.Replace($"| Merge commit `{Integration}`", $"| Merge commit `{new string('a', 40)}`", StringComparison.Ordinal),
            "success-in-command" => receipt.Replace($"`{Lane}` | Passed 26/26; 0 errors, failures, skips, or not-run tests.",
                $"`{Lane}` Passed 26/26; 0 errors, failures, skips, or not-run tests. | FAILED.", StringComparison.Ordinal),
            _ => throw new ArgumentException("Unknown receipt fixture.", nameof(scenario)),
        };
        ReceiptGaps(altered).ShouldNotBeEmpty(scenario);
    }

    /// <summary>Reads only the historical owner-delivery section, excluding other receipts.</summary>
    private static string ReadReceipt(string root)
    {
        string matrix = RepositoryRoot.Read(root, "_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md")
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        string[] sections = matrix.Split("## G8-A EventStore.Aspire Owner Delivery Receipt — 2026-08-01\n", StringSplitOptions.None);
        sections.Length.ShouldBe(2, "The historical owner receipt must be unique.");
        return sections[1].Split("\n## ", StringSplitOptions.None)[0];
    }

    /// <summary>Checks each structured historical claim in its own evidence location.</summary>
    private static List<string> ReceiptGaps(string receipt)
    {
        var gaps = new List<string>();
        foreach ((string label, string field, string pattern) in new[]
        {
            ("base", @"(?m)^- Producer base:", $@"- Producer base: Hexalith\.EventStore\s+`{ProducerBase}`\."),
            ("delivery", @"(?m)^- Producer delivery:", $@"- Producer delivery: Hexalith\.EventStore\s+`{Delivery}`"),
            ("integration", @"integration commit `", $@"integration commit `{Integration}`"),
            ("consumer", @"(?m)^- Consumer/rollback identity:", $@"- Consumer/rollback identity: Parties\s+`{Consumer}`"),
            ("gitlink", @"EventStore gitlink `", $@"EventStore gitlink `{ConsumerGitlink}`"),
        })
        {
            if (Regex.Matches(receipt, field).Count != 1 || Regex.Matches(receipt, pattern).Count != 1)
            {
                gaps.Add(label);
            }
        }
        MatchCollection inventories = Regex.Matches(receipt, @"### Verified delivery file inventory[^\n]*\n(?:(?!\n### ).)*?```text\n(?<files>.*?)\n```", RegexOptions.Singleline);
        if (inventories.Count != 1 || !inventories[0].Groups["files"].Value.Split('\n').SequenceEqual(Inventory, StringComparer.Ordinal))
        {
            gaps.Add("inventory");
        }
        string[] lane = ReadEvidenceCells(receipt, "JWT application-model tests");
        if (lane.Length != 3 || lane[1] != $"From the clean worktree: `{Lane}`"
            || !lane[2].StartsWith("Passed 26/26; 0 errors, failures, skips, or not-run tests.", StringComparison.Ordinal))
        {
            gaps.Add("lane");
        }
        foreach ((string name, string command) in new[]
        {
            ("Clean producer identity", WorktreeCommand),
            ("Diff and commit policy", DiffCommand),
        })
        {
            string[] cells = ReadEvidenceCells(receipt, name);
            if (cells.Length != 3 || !cells[1].Contains($"`{command}`", StringComparison.Ordinal)
                || Regex.Matches(cells[1], Regex.Escape(command.Split(' ')[0] + " " + command.Split(' ')[1])).Count != 1)
            {
                gaps.Add(name);
            }
        }
        string[] publication = ReadEvidenceCells(receipt, "Remote publication");
        if (publication.Length != 3 || !publication[1].Contains($"with owner commit `{Delivery}`", StringComparison.Ordinal)
            || !publication[2].StartsWith($"Merge commit `{Integration}`", StringComparison.Ordinal))
        {
            gaps.Add("publication");
        }
        return gaps;
    }

    /// <summary>Separates evidence cells while preserving escaped command pipes.</summary>
    private static string[] ReadEvidenceCells(string receipt, string name)
    {
        string[] rows = receipt.Split('\n').Where(line => line.StartsWith($"| {name} |", StringComparison.Ordinal)).ToArray();
        return rows.Length == 1
            ? Regex.Split(rows[0], @"(?<!\\)\|").Skip(1).SkipLast(1).Select(static cell => cell.Trim()).ToArray()
            : [];
    }
}
