using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Search;

internal static class PartySearchResultsBuilder
{
    public static PagedResult<PartyIndexEntry> BuildPagedList(
        IEnumerable<PartyIndexEntry> entries,
        PartyType? typeFilter,
        bool? activeFilter,
        int page,
        int pageSize)
        => BuildPagedList(
            entries,
            typeFilter,
            activeFilter,
            createdAfter: null,
            createdBefore: null,
            modifiedAfter: null,
            modifiedBefore: null,
            page,
            pageSize);

    public static PagedResult<PartyIndexEntry> BuildPagedList(
        IEnumerable<PartyIndexEntry> entries,
        PartyType? typeFilter,
        bool? activeFilter,
        DateTimeOffset? createdAfter,
        DateTimeOffset? createdBefore,
        DateTimeOffset? modifiedAfter,
        DateTimeOffset? modifiedBefore,
        int page,
        int pageSize)
    {
        ArgumentNullException.ThrowIfNull(entries);

        int normalizedPage = Math.Max(1, page);
        int normalizedPageSize = Math.Clamp(pageSize, 1, 100);

        List<PartyIndexEntry> sorted = [.. ApplyListFilters(
                entries,
                typeFilter,
                activeFilter,
                createdAfter,
                createdBefore,
                modifiedAfter,
                modifiedBefore)
            // P12: Sort by normalized display name, then party id — matches the spec's
            // "normalized display name, then party id" tie-break and the LocalFuzzyPartySearchProvider
            // sort. Avoids the prior SortName-vs-DisplayName divergence between list and search paths.
            .OrderBy(e => LocalFuzzyPartySearchProvider.NormalizeDiacritics(e.DisplayName), StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Id, StringComparer.Ordinal)];

        return CreatePagedResult(sorted, normalizedPage, normalizedPageSize);
    }

    public static PagedResult<PartySearchResult> BuildSearchResults(
        IEnumerable<PartyIndexEntry> entries,
        string query,
        PartyType? typeFilter,
        bool? activeFilter,
        int page,
        int pageSize)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        string normalizedQuery = query.Trim();
        string[] tokens = normalizedQuery
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        IEnumerable<(PartySearchResult Result, int Priority, int FieldCount, int TokenCount)> matches = ApplyFilters(entries, typeFilter, activeFilter)
            .Select(entry => EvaluateEntry(entry, normalizedQuery, tokens))
            .Where(result => result.HasValue)
            .Select(result => result!.Value);

        List<PartySearchResult> sorted = [.. matches
            .OrderBy(m => m.Priority)
            .ThenByDescending(m => m.FieldCount)
            .ThenByDescending(m => m.TokenCount)
            // P12: Tie-break by normalized display name then party id, matching the search provider.
            .ThenBy(m => LocalFuzzyPartySearchProvider.NormalizeDiacritics(m.Result.Party.DisplayName), StringComparer.OrdinalIgnoreCase)
            .ThenBy(m => m.Result.Party.Id, StringComparer.Ordinal)
            .Select(m => m.Result)];

        return CreatePagedResult(sorted, page, pageSize);
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

    private static IEnumerable<PartyIndexEntry> ApplyListFilters(
        IEnumerable<PartyIndexEntry> entries,
        PartyType? typeFilter,
        bool? activeFilter,
        DateTimeOffset? createdAfter,
        DateTimeOffset? createdBefore,
        DateTimeOffset? modifiedAfter,
        DateTimeOffset? modifiedBefore)
    {
        IEnumerable<PartyIndexEntry> filtered = ApplyFilters(entries, typeFilter, activeFilter)
            .Where(static e => !e.IsErased);

        if (createdAfter is not null)
        {
            filtered = filtered.Where(e => e.CreatedAt >= createdAfter.Value);
        }

        if (createdBefore is not null)
        {
            filtered = filtered.Where(e => e.CreatedAt <= createdBefore.Value);
        }

        if (modifiedAfter is not null)
        {
            filtered = filtered.Where(e => e.LastModifiedAt >= modifiedAfter.Value);
        }

        if (modifiedBefore is not null)
        {
            filtered = filtered.Where(e => e.LastModifiedAt <= modifiedBefore.Value);
        }

        return filtered;
    }

    private static (PartySearchResult Result, int Priority, int FieldCount, int TokenCount)? EvaluateEntry(
        PartyIndexEntry entry,
        string normalizedQuery,
        IReadOnlyList<string> tokens)
    {
        Dictionary<string, int> fieldMatches = [];
        HashSet<string> matchedTokens = [];
        IEnumerable<string> candidates = tokens.Count > 1 ? [normalizedQuery, .. tokens] : [normalizedQuery];

        foreach (string candidate in candidates)
        {
            if (TryMatchValue(entry.DisplayName, candidate, out int displayPriority))
            {
                UpsertMatch(fieldMatches, "displayName", displayPriority);
                matchedTokens.Add(candidate);
            }

        }

        if (fieldMatches.Count == 0)
        {
            return null;
        }

        List<MatchMetadata> metadata = [.. fieldMatches
            .OrderBy(m => m.Value)
            .ThenBy(m => m.Key, StringComparer.Ordinal)
            .Select(m => new MatchMetadata
            {
                MatchedField = m.Key,
                MatchType = ToMatchType(m.Value),
            })];

        return (
            new PartySearchResult
            {
                Party = entry,
                Matches = metadata,
            },
            fieldMatches.Values.Min(),
            fieldMatches.Count,
            matchedTokens.Count);
    }

    private static bool TryMatchValue(string? source, string candidate, out int priority)
    {
        priority = int.MaxValue;

        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        if (string.Equals(source, candidate, StringComparison.OrdinalIgnoreCase))
        {
            priority = 0;
            return true;
        }

        if (source.StartsWith(candidate, StringComparison.OrdinalIgnoreCase))
        {
            priority = 1;
            return true;
        }

        if (source.Contains(candidate, StringComparison.OrdinalIgnoreCase))
        {
            priority = 2;
            return true;
        }

        return false;
    }

    private static void UpsertMatch(IDictionary<string, int> fieldMatches, string field, int priority)
    {
        if (!fieldMatches.TryGetValue(field, out int existingPriority) || priority < existingPriority)
        {
            fieldMatches[field] = priority;
        }
    }

    private static string ToMatchType(int priority)
        => priority switch
        {
            0 => "exact",
            1 => "prefix",
            _ => "contains",
        };

    private static PagedResult<T> CreatePagedResult<T>(IReadOnlyList<T> items, int page, int pageSize)
    {
        // P1/P15: Clamp pageSize to [1, 100] and page to [1, ...]. Returning the safe values
        // ensures `TotalPages = ceil(TotalCount / PageSize)` is consistent with the data the
        // client receives — the previous "echo caller-supplied" behavior produced inconsistent
        // metadata where Items.Count would not match the advertised PageSize for pageSize > 100.
        int safePageSize = Math.Clamp(pageSize, 1, 100);
        int safePage = Math.Max(1, page);
        int totalCount = items.Count;
        int totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling((double)totalCount / safePageSize);
        long skipLong = (long)(safePage - 1) * safePageSize;
        int skip = skipLong > int.MaxValue ? int.MaxValue : (int)skipLong;
        List<T> pagedItems = [.. items.Skip(skip).Take(safePageSize)];

        return new PagedResult<T>
        {
            Items = pagedItems,
            Page = safePage,
            PageSize = safePageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
        };
    }
}
