using Hexalith.Commons.Strings;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Search;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Search;

/// <summary>
/// Local fallback search provider for MVP display-name search.
/// </summary>
internal sealed class LocalFuzzyPartySearchProvider : IPartySearchProvider
{
    // P1: Upper-bound clamp matches PartySearchResultsBuilder.BuildPagedList policy.
    // Defense-in-depth against payload-size DoS — Take(int.MaxValue) over large tenants can OOM.
    internal const int MaxPageSize = 100;

    public PagedResult<PartySearchResult> Search(
        IEnumerable<PartyIndexEntry> entries,
        string query,
        PartyType? typeFilter,
        bool? activeFilter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // P27: Reject `page < 1` so callers that bypass `LocalPartySearchService.NormalizeRequest`
        // do not silently get page 1 contents while the response advertises page 0.
        if (page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(page), page, "Page must be >= 1.");
        }

        if (pageSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, "PageSize must be >= 1.");
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            // P15: Echo normalized values so paging math stays consistent across all paths.
            return new PagedResult<PartySearchResult>
            {
                Items = [],
                Page = Math.Max(1, page),
                PageSize = Math.Clamp(pageSize, 1, MaxPageSize),
                TotalCount = 0,
                TotalPages = 1,
            };
        }

        string normalizedQuery = NormalizeDiacritics(query.Trim());
        string[] tokens = normalizedQuery
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Materialize once: callers may pass a yield-iterator over Dapr actor state, and the
        // pipeline below enumerates `entries` multiple times via Where/Select/OrderBy.
        IReadOnlyList<PartyIndexEntry> materialized = entries as IReadOnlyList<PartyIndexEntry> ?? [.. entries];

        List<PartySearchResult> results = [];
        foreach (PartyIndexEntry entry in ApplyFilters(materialized, typeFilter, activeFilter))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.IsErased)
            {
                continue;
            }

            PartySearchResult? evaluated = EvaluateEntry(entry, tokens);
            if (evaluated is not null)
            {
                results.Add(evaluated);
            }
        }

        // Precompute the diacritic-normalized display name once per result. The previous comparator
        // re-ran NormalizeDiacritics (a FormD normalization that allocates a StringBuilder) on both
        // operands of every comparison — O(n log n) normalizations on a hot path for large tenants.
        List<(PartySearchResult Result, string SortKey)> decorated =
            [.. results.Select(r => (r, NormalizeDiacritics(r.Party.DisplayName)))];

        decorated.Sort((left, right) =>
        {
            int byScore = right.Result.RelevanceScore.CompareTo(left.Result.RelevanceScore);
            if (byScore != 0)
            {
                return byScore;
            }

            int byDisplayName = string.Compare(left.SortKey, right.SortKey, StringComparison.OrdinalIgnoreCase);
            if (byDisplayName != 0)
            {
                return byDisplayName;
            }

            return string.Compare(left.Result.Party.Id, right.Result.Party.Id, StringComparison.Ordinal);
        });

        return CreatePagedResult([.. decorated.Select(d => d.Result)], page, pageSize);
    }

    internal static double JaroWinklerSimilarity(string s1, string s2)
        => StringHelper.JaroWinklerSimilarity(s1, s2);

    internal static string NormalizeDiacritics(string? input)
        => StringHelper.StripDiacritics(input);

    private static IEnumerable<PartyIndexEntry> ApplyFilters(
        IEnumerable<PartyIndexEntry> entries,
        PartyType? typeFilter,
        bool? activeFilter)
    {
        IEnumerable<PartyIndexEntry> filtered = entries;

        if (typeFilter is not null)
        {
            filtered = filtered.Where(e => e.Type == typeFilter.Value);
        }

        if (activeFilter is not null)
        {
            filtered = filtered.Where(e => e.IsActive == activeFilter.Value);
        }

        return filtered;
    }

    private static double ComputeRelevanceScore(
        List<(string Field, double FieldWeight, double MatchScore)> fieldScores,
        int matchedTokenCount,
        int totalTokenCount)
    {
        if (fieldScores.Count == 0)
        {
            return 0.0;
        }

        double maxScore = fieldScores.Max(f => f.FieldWeight * f.MatchScore);
        double avgScore = fieldScores.Average(f => f.FieldWeight * f.MatchScore);
        double coverage = totalTokenCount > 0 ? (double)matchedTokenCount / totalTokenCount : 0.0;

        double score = (maxScore * 0.7) + (avgScore * 0.2) + (coverage * 0.1);
        return Math.Clamp(score, 0.0, 1.0);
    }

    private static PagedResult<PartySearchResult> CreatePagedResult(IReadOnlyList<PartySearchResult> items, int page, int pageSize)
    {
        // P1: Clamp pageSize to [1, MaxPageSize]. Take(int.MaxValue) over a populated list
        // would force the LINQ pipeline to materialize the entire result set into a single
        // List<T>, risking OOM on large tenants.
        int safePageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        // P15: Math.Max(1, page) ensures the response's Page index is always valid even if
        // the caller passed 0 or negative. Upper bound (Page > TotalPages) is left as-is so
        // clients see exactly which out-of-range page they requested.
        int safePage = Math.Max(1, page);
        int totalCount = items.Count;
        int totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling((double)totalCount / safePageSize);
        // Use long arithmetic and clamp to int.MaxValue so a malicious or buggy caller
        // passing Page=int.MaxValue cannot overflow `(page-1)*pageSize` to a negative value
        // (Enumerable.Skip with negative returns the full sequence, producing the wrong page
        // with the advertised page index).
        long skipLong = (long)(safePage - 1) * safePageSize;
        int skip = skipLong > int.MaxValue ? int.MaxValue : (int)skipLong;
        List<PartySearchResult> pagedItems = [.. items.Skip(skip).Take(safePageSize)];

        return new PagedResult<PartySearchResult>
        {
            Items = pagedItems,
            // P15: Echo the safe values so `TotalPages = ceil(TotalCount / PageSize)` is consistent
            // with the data the client receives.
            Page = safePage,
            PageSize = safePageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
        };
    }

    private static PartySearchResult? EvaluateEntry(PartyIndexEntry entry, string[] tokens)
    {
        Dictionary<string, (double FieldWeight, double BestMatchScore, string MatchType)> fieldMatches = [];
        HashSet<string> matchedTokens = [];

        string normalizedDisplayName = NormalizeDiacritics(entry.DisplayName);

        // Include full query as a candidate when multi-token (same as v1.0)
        string fullQuery = string.Join(' ', tokens);
        IEnumerable<string> candidates = tokens.Length > 1 ? [fullQuery, .. tokens] : tokens;

        foreach (string token in candidates)
        {
            if (TryMatch(normalizedDisplayName, token, out double matchScore, out string matchType))
            {
                UpsertFieldMatch(fieldMatches, "displayName", 1.0, matchScore, matchType);
                _ = matchedTokens.Add(token);
            }
        }

        if (fieldMatches.Count == 0)
        {
            return null;
        }

        List<(string Field, double FieldWeight, double MatchScore)> scores = fieldMatches
            .Select(kv => (kv.Key, kv.Value.FieldWeight, kv.Value.BestMatchScore))
            .ToList();

        double relevance = ComputeRelevanceScore(scores, matchedTokens.Count, tokens.Length);

        List<MatchMetadata> metadata = [.. fieldMatches
            .OrderByDescending(m => m.Value.FieldWeight * m.Value.BestMatchScore)
            .ThenBy(m => m.Key, StringComparer.Ordinal)
            .Select(m => new MatchMetadata
            {
                MatchedField = m.Key,
                MatchType = m.Value.MatchType,
                Score = m.Value.FieldWeight * m.Value.BestMatchScore,
            })];

        return new PartySearchResult
        {
            Party = entry,
            Matches = metadata,
            RelevanceScore = relevance,
        };
    }

    private static bool TryMatch(string candidate, string token, out double score, out string matchType)
    {
        score = 0.0;
        matchType = string.Empty;

        if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        // Exact match
        if (string.Equals(candidate, token, StringComparison.OrdinalIgnoreCase))
        {
            score = 1.0;
            matchType = "exact";
            return true;
        }

        // Prefix match
        if (candidate.StartsWith(token, StringComparison.OrdinalIgnoreCase))
        {
            score = 0.8;
            matchType = "prefix";
            return true;
        }

        // Contains match
        if (candidate.Contains(token, StringComparison.OrdinalIgnoreCase))
        {
            score = 0.6;
            matchType = "contains";
            return true;
        }

        // Identifier-like tokens with digits create excessive false positives under Jaro-Winkler
        // (for example Entry-50000 vs Entry-10000). Keep them to deterministic matching.
        if (token.Any(char.IsDigit))
        {
            return false;
        }

        // Fuzzy match (Jaro-Winkler) — only when exact/prefix/contains fail
        double similarity = JaroWinklerSimilarity(candidate, token);
        if (similarity >= 0.85)
        {
            score = 0.4;
            matchType = "fuzzy";
            return true;
        }

        // For multi-word candidates, check individual words
        string[] candidateWords = candidate.Split([' ', '-', '.'], StringSplitOptions.RemoveEmptyEntries);
        if (candidateWords.Length > 1)
        {
            foreach (string word in candidateWords)
            {
                double wordSimilarity = JaroWinklerSimilarity(word, token);
                if (wordSimilarity >= 0.85)
                {
                    score = 0.4;
                    matchType = "fuzzy";
                    return true;
                }
            }
        }

        return false;
    }

    private static void UpsertFieldMatch(
        Dictionary<string, (double FieldWeight, double BestMatchScore, string MatchType)> fieldMatches,
        string field,
        double fieldWeight,
        double matchScore,
        string matchType)
    {
        if (!fieldMatches.TryGetValue(field, out (double FieldWeight, double BestMatchScore, string MatchType) existing) ||
            matchScore > existing.BestMatchScore)
        {
            fieldMatches[field] = (fieldWeight, matchScore, matchType);
        }
    }
}
