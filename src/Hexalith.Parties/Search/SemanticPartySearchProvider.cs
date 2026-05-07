using System.Globalization;
using System.Text;

using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Search;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Search;

/// <summary>
/// Enhanced search provider with fuzzy matching, diacritic normalization,
/// type-text mapping, and multi-field relevance scoring.
/// </summary>
internal sealed class SemanticPartySearchProvider : IPartySearchProvider
{
    private static readonly Dictionary<string, PartyType> s_typeAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["person"] = PartyType.Person,
        ["individual"] = PartyType.Person,
        ["people"] = PartyType.Person,
        ["company"] = PartyType.Organization,
        ["corporation"] = PartyType.Organization,
        ["organization"] = PartyType.Organization,
        ["organisation"] = PartyType.Organization,
        ["org"] = PartyType.Organization,
        ["enterprise"] = PartyType.Organization,
        ["firm"] = PartyType.Organization,
        ["business"] = PartyType.Organization,
    };

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

        if (string.IsNullOrWhiteSpace(query))
        {
            return new PagedResult<PartySearchResult>
            {
                Items = [],
                Page = page,
                PageSize = pageSize,
                TotalCount = 0,
                TotalPages = 1,
            };
        }

        string normalizedQuery = NormalizeDiacritics(query.Trim());
        string[] tokens = normalizedQuery
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        List<PartySearchResult> results = ApplyFilters(entries, typeFilter, activeFilter)
            .Where(e => !e.IsErased)
            .Select(entry => EvaluateEntry(entry, tokens))
            .Where(r => r is not null)
            .Select(r => r!)
            .OrderByDescending(r => r.RelevanceScore)
            .ThenBy(r => r.Party.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return CreatePagedResult(results, page, pageSize);
    }

    internal static double JaroWinklerSimilarity(string s1, string s2)
    {
        if (string.Equals(s1, s2, StringComparison.Ordinal))
        {
            return 1.0;
        }

        if (s1.Length == 0 || s2.Length == 0)
        {
            return 0.0;
        }

        int matchWindow = Math.Max(0, (Math.Max(s1.Length, s2.Length) / 2) - 1);

        bool[] s1Matched = new bool[s1.Length];
        bool[] s2Matched = new bool[s2.Length];

        int matches = 0;
        int transpositions = 0;

        // Find matching characters
        for (int i = 0; i < s1.Length; i++)
        {
            int start = Math.Max(0, i - matchWindow);
            int end = Math.Min(i + matchWindow + 1, s2.Length);

            for (int j = start; j < end; j++)
            {
                if (s2Matched[j] || !char.Equals(char.ToLowerInvariant(s1[i]), char.ToLowerInvariant(s2[j])))
                {
                    continue;
                }

                s1Matched[i] = true;
                s2Matched[j] = true;
                matches++;
                break;
            }
        }

        if (matches == 0)
        {
            return 0.0;
        }

        // Count transpositions
        int k = 0;
        for (int i = 0; i < s1.Length; i++)
        {
            if (!s1Matched[i])
            {
                continue;
            }

            while (!s2Matched[k])
            {
                k++;
            }

            if (!char.Equals(char.ToLowerInvariant(s1[i]), char.ToLowerInvariant(s2[k])))
            {
                transpositions++;
            }

            k++;
        }

        double jaro = ((double)matches / s1.Length
            + (double)matches / s2.Length
            + (double)(matches - (transpositions / 2)) / matches)
            / 3.0;

        // Winkler prefix bonus (up to 4 characters)
        int prefixLen = 0;
        for (int i = 0; i < Math.Min(4, Math.Min(s1.Length, s2.Length)); i++)
        {
            if (char.Equals(char.ToLowerInvariant(s1[i]), char.ToLowerInvariant(s2[i])))
            {
                prefixLen++;
            }
            else
            {
                break;
            }
        }

        return jaro + (prefixLen * 0.1 * (1.0 - jaro));
    }

    internal static string NormalizeDiacritics(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        string normalized = input.Normalize(NormalizationForm.FormD);
        StringBuilder sb = new(normalized.Length);

        foreach (char c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                _ = sb.Append(c);
            }
        }

        return sb.ToString();
    }

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
        int totalCount = items.Count;
        int totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling((double)totalCount / Math.Max(1, pageSize));
        // P19: Use long arithmetic and clamp to int.MaxValue so `Page=int.MaxValue` cannot
        // overflow `(page-1)*pageSize` to a negative value. The sister provider
        // `LocalFuzzyPartySearchProvider` already applies the same fix.
        long skipLong = (long)Math.Max(0, page - 1) * Math.Max(0, pageSize);
        int skip = skipLong > int.MaxValue ? int.MaxValue : (int)skipLong;
        List<PartySearchResult> pagedItems = [.. items.Skip(skip).Take(pageSize)];

        return new PagedResult<PartySearchResult>
        {
            Items = pagedItems,
            Page = page,
            PageSize = pageSize,
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
            // Match against DisplayName
            if (TryMatch(normalizedDisplayName, token, out double matchScore, out string matchType))
            {
                UpsertFieldMatch(fieldMatches, "displayName", 1.0, matchScore, matchType);
                _ = matchedTokens.Add(token);
            }

            // Match against ALL contact channels (not just Email)
            // Structured fields use exact/prefix/contains only (no fuzzy — prevents false positives on email domains)
            foreach (ContactChannel channel in entry.SearchableContactChannels)
            {
                string normalizedValue = NormalizeDiacritics(channel.Value);
                if (TryMatchExact(normalizedValue, token, out double channelScore, out string channelMatchType))
                {
                    string fieldName = channel.Type == ContactChannelType.Email ? "email" : "contactChannel";
                    double fieldWeight = channel.Type == ContactChannelType.Email ? 0.9 : 0.7;
                    UpsertFieldMatch(fieldMatches, fieldName, fieldWeight, channelScore, channelMatchType);
                    _ = matchedTokens.Add(token);
                }
            }

            // Match against identifiers (exact/prefix/contains only — structured data)
            foreach (PartyIdentifier identifier in entry.SearchableIdentifiers)
            {
                string normalizedValue = NormalizeDiacritics(identifier.Value);
                if (TryMatchExact(normalizedValue, token, out double idScore, out string idMatchType))
                {
                    UpsertFieldMatch(fieldMatches, "identifier", 0.8, idScore, idMatchType);
                    _ = matchedTokens.Add(token);
                }
            }

            // Type-text matching (scoring boost, NOT a filter)
            if (s_typeAliases.TryGetValue(token, out PartyType mappedType) && entry.Type == mappedType)
            {
                UpsertFieldMatch(fieldMatches, "type", 0.5, 1.0, "exact");
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

    private static bool TryMatchExact(string candidate, string token, out double score, out string matchType)
    {
        score = 0.0;
        matchType = string.Empty;

        if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        if (string.Equals(candidate, token, StringComparison.OrdinalIgnoreCase))
        {
            score = 1.0;
            matchType = "exact";
            return true;
        }

        if (candidate.StartsWith(token, StringComparison.OrdinalIgnoreCase))
        {
            score = 0.8;
            matchType = "prefix";
            return true;
        }

        if (candidate.Contains(token, StringComparison.OrdinalIgnoreCase))
        {
            score = 0.6;
            matchType = "contains";
            return true;
        }

        return false;
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
