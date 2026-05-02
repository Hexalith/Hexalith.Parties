using System.Diagnostics;

using Hexalith.Parties.CommandApi.Search;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;

using Shouldly;

using Xunit.Abstractions;

namespace Hexalith.Parties.CommandApi.Tests.Search;

/// <summary>
/// Performance benchmark tests for local fuzzy fallback search.
/// Verifies NFR2/NFR4 compliance: search latency &lt; 500ms at scale.
/// </summary>
public class LocalFuzzySearchPerformanceBenchmarkTests(ITestOutputHelper output)
{
    private readonly LocalFuzzyPartySearchProvider _provider = new();

    // ─── Task 10.1: Benchmark at 10K entries — verify < 500ms ───

    [Fact]
    public void Search_10KEntries_CompletesWithin500ms()
    {
        List<PartyIndexEntry> entries = GenerateEntries(10_000);

        // Warm up (JIT)
        _ = _provider.Search(entries, "John", null, null, 1, 20);

        Stopwatch sw = Stopwatch.StartNew();
        PagedResult<PartySearchResult> result = _provider.Search(entries, "Jean Dupont", null, null, 1, 20);
        sw.Stop();

        output.WriteLine($"10K entries — search for 'Jean Dupont': {sw.ElapsedMilliseconds}ms, {result.TotalCount} results");

        sw.ElapsedMilliseconds.ShouldBeLessThan(500, $"Search at 10K entries took {sw.ElapsedMilliseconds}ms (NFR2 limit: 500ms)");
        result.TotalCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Search_10KEntries_FuzzyQuery_CompletesWithin500ms()
    {
        List<PartyIndexEntry> entries = GenerateEntries(10_000);

        // Warm up
        _ = _provider.Search(entries, "Dupnt", null, null, 1, 20);

        Stopwatch sw = Stopwatch.StartNew();
        PagedResult<PartySearchResult> result = _provider.Search(entries, "Dupnt", null, null, 1, 20);
        sw.Stop();

        output.WriteLine($"10K entries — fuzzy search for 'Dupnt': {sw.ElapsedMilliseconds}ms, {result.TotalCount} results");

        sw.ElapsedMilliseconds.ShouldBeLessThan(500, $"Fuzzy search at 10K entries took {sw.ElapsedMilliseconds}ms (NFR2 limit: 500ms)");
    }

    // ─── Task 10.2: Benchmark at 100K entries — fuzzy-only worst case ───

    [Fact]
    public void Search_100KEntries_FuzzyOnly_MeasureLatency()
    {
        List<PartyIndexEntry> entries = GenerateEntries(100_000);

        // Warm up
        _ = _provider.Search(entries, "Xyzzq", null, null, 1, 20);

        // Fuzzy-only query: a misspelled name that won't match exact/prefix/contains,
        // forcing Jaro-Winkler computation on every entry
        Stopwatch sw = Stopwatch.StartNew();
        PagedResult<PartySearchResult> result = _provider.Search(entries, "Dupnt", null, null, 1, 20);
        sw.Stop();

        long elapsed = sw.ElapsedMilliseconds;
        output.WriteLine($"100K entries — fuzzy-only search for 'Dupnt': {elapsed}ms, {result.TotalCount} results");

        // Document the measured P95 latency. If exceeds 500ms, subtask 10.3 documents mitigations.
        if (elapsed > 500)
        {
            output.WriteLine("WARNING: 100K fuzzy-only search exceeded 500ms NFR2 target.");
            output.WriteLine("Mitigation options documented in Task 10.3:");
            output.WriteLine("  1. Partition strategy (D5) — reduce candidate set per tenant shard");
            output.WriteLine("  2. Pre-filtered candidate set — first-character pre-filter reduces candidates ~96%");
            output.WriteLine("  3. Elasticsearch/OpenSearch backend (v2) — offload fuzzy matching to dedicated engine");
            output.WriteLine("  4. Parallel partition search — split entries across CPU cores");
        }
        else
        {
            output.WriteLine($"100K fuzzy-only search within NFR2 target ({elapsed}ms < 500ms)");
        }

        // This test always passes — it's a measurement benchmark, not a hard gate.
        // The 10K test is the hard NFR2 gate; 100K is informational.
        result.ShouldNotBeNull();
    }

    [Fact]
    public void Search_100KEntries_ExactMatch_CompletesWithin500ms()
    {
        List<PartyIndexEntry> entries = GenerateEntries(100_000);

        // Warm up
        _ = _provider.Search(entries, "Entry-50000", null, null, 1, 20);

        Stopwatch sw = Stopwatch.StartNew();
        PagedResult<PartySearchResult> result = _provider.Search(entries, "Entry-50000", null, null, 1, 20);
        sw.Stop();

        output.WriteLine($"100K entries — exact search for 'Entry-50000': {sw.ElapsedMilliseconds}ms, {result.TotalCount} results");

        // Exact/prefix/contains should short-circuit before fuzzy — expect fast
        sw.ElapsedMilliseconds.ShouldBeLessThan(500, $"Exact match at 100K took {sw.ElapsedMilliseconds}ms");
    }

    private static List<PartyIndexEntry> GenerateEntries(int count)
    {
        List<PartyIndexEntry> entries = new(count);

        // Mix of persons and organizations with varied names
        string[] firstNames = ["Jean", "Marie", "Pierre", "Sophie", "François", "Claire", "Laurent", "Isabelle", "Nicolas", "Camille"];
        string[] lastNames = ["Dupont", "Martin", "Bernard", "Petit", "Robert", "Moreau", "Laurent", "Simon", "Michel", "Garcia"];
        string[] orgNames = ["Acme", "TechCorp", "DataSoft", "CloudNet", "InnoSys", "WebPro", "DevLab", "NetBase", "CodeHub", "AppForge"];
        string[] orgSuffixes = ["Corporation", "Inc", "SA", "SAS", "SARL", "GmbH", "Ltd", "Group", "International", "Systems"];

        for (int i = 0; i < count; i++)
        {
            bool isOrg = i % 5 == 0; // 20% organizations
            string displayName;
            PartyType type;

            if (isOrg)
            {
                displayName = $"{orgNames[i % orgNames.Length]} {orgSuffixes[(i / orgNames.Length) % orgSuffixes.Length]}";
                type = PartyType.Organization;
            }
            else
            {
                displayName = $"{firstNames[i % firstNames.Length]} {lastNames[(i / firstNames.Length) % lastNames.Length]}";
                type = PartyType.Person;
            }

            // Make names unique by appending index for large datasets
            if (i >= firstNames.Length * lastNames.Length)
            {
                displayName = $"Entry-{i} {displayName}";
            }

            entries.Add(new PartyIndexEntry
            {
                Id = $"bench-{i}",
                Type = type,
                IsActive = i % 7 != 0, // ~14% inactive
                DisplayName = displayName,
                SearchableContactChannels = i % 3 == 0
                    ? [new ContactChannel { Id = $"ch-{i}", Type = ContactChannelType.Email, Value = $"user{i}@example.com" }]
                    : [],
                SearchableIdentifiers = i % 4 == 0
                    ? [new PartyIdentifier { Id = $"id-{i}", Type = IdentifierType.VAT, Value = $"FR{i:D11}" }]
                    : [],
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-i),
                LastModifiedAt = DateTimeOffset.UtcNow,
            });
        }

        return entries;
    }
}
