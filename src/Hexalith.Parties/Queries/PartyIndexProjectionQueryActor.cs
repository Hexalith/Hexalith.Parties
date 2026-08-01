using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Queries;

/// <summary>Stable index query discriminators and strict payload parsing shared by SDK handlers.</summary>
public static class PartyIndexProjectionQueryActor
{
    public const string ActorTypeName = nameof(PartyIndexProjectionQueryActor);
    public const string ListAggregateId = "parties";
    public const string PartyDomain = "party";
    public const string PartyIndexQueryType = "PartyIndex";
    public const string PartySearchQueryType = "PartySearch";
    public const string ProjectionType = PartyProjectionNames.Index;

    private static readonly JsonSerializerOptions s_jsonOptions = new(PartiesJsonOptions.Default)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private static readonly Regex s_iso8601WithOffset = new(
        @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(?::\d{2}(?:\.\d+)?)?(?:Z|[+-]\d{2}:?\d{2})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    internal static bool TryParseListPayload(byte[] payloadBytes, out ListPartiesQueryPayload payload)
    {
        payload = new ListPartiesQueryPayload(1, 20, null, null, null, null, null, null);
        if (payloadBytes.Length == 0)
        {
            return false;
        }

        ListPartiesQueryPayloadWire? wire;
        try
        {
            wire = JsonSerializer.Deserialize<ListPartiesQueryPayloadWire>(payloadBytes, s_jsonOptions);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or NotSupportedException)
        {
            return false;
        }

        if (wire is null
            || (wire.Page is { } pageValue && pageValue < 1)
            || (wire.PageSize is { } pageSizeValue && pageSizeValue < 1)
            || !TryParsePartyType(wire.Type, out PartyType? type)
            || !TryParseInstant(wire.CreatedAfter, out DateTimeOffset? createdAfter)
            || !TryParseInstant(wire.CreatedBefore, out DateTimeOffset? createdBefore)
            || !TryParseInstant(wire.ModifiedAfter, out DateTimeOffset? modifiedAfter)
            || !TryParseInstant(wire.ModifiedBefore, out DateTimeOffset? modifiedBefore)
            || (createdAfter is not null && createdBefore is not null && createdAfter.Value > createdBefore.Value)
            || (modifiedAfter is not null && modifiedBefore is not null && modifiedAfter.Value > modifiedBefore.Value))
        {
            return false;
        }

        payload = new ListPartiesQueryPayload(
            wire.Page ?? 1,
            wire.PageSize ?? 20,
            type,
            wire.Active,
            createdAfter,
            createdBefore,
            modifiedAfter,
            modifiedBefore);
        return true;
    }

    internal static bool TryParseSearchPayload(byte[] payloadBytes, out SearchPartiesQueryPayload payload)
    {
        payload = new SearchPartiesQueryPayload(string.Empty, 1, 20, null, null, null, null);
        if (payloadBytes.Length == 0)
        {
            return false;
        }

        SearchPartiesQueryPayloadWire? wire;
        try
        {
            wire = JsonSerializer.Deserialize<SearchPartiesQueryPayloadWire>(payloadBytes, s_jsonOptions);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or NotSupportedException)
        {
            return false;
        }

        if (wire is null
            || (wire.Page is { } pageValue && pageValue < 1)
            || (wire.PageSize is { } pageSizeValue && pageSizeValue < 1)
            || !TryParsePartyType(wire.Type, out PartyType? type))
        {
            return false;
        }

        payload = new SearchPartiesQueryPayload(
            wire.Query ?? string.Empty,
            wire.Page ?? 1,
            wire.PageSize ?? 20,
            type,
            wire.Active,
            wire.Mode,
            wire.CaseId);
        return true;
    }

    internal static bool IsUnsupportedSearchMode(string? mode)
        => !string.IsNullOrWhiteSpace(mode)
            && !string.Equals(mode, "Lexical", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(mode, "DisplayName", StringComparison.OrdinalIgnoreCase);

    private static bool TryParsePartyType(string? value, out PartyType? type)
    {
        type = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (string.Equals(value, nameof(PartyType.Person), StringComparison.OrdinalIgnoreCase))
        {
            type = PartyType.Person;
            return true;
        }

        if (string.Equals(value, nameof(PartyType.Organization), StringComparison.OrdinalIgnoreCase))
        {
            type = PartyType.Organization;
            return true;
        }

        return false;
    }

    private static bool TryParseInstant(string? value, out DateTimeOffset? instant)
    {
        instant = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!s_iso8601WithOffset.IsMatch(value)
            || !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset parsed))
        {
            return false;
        }

        try
        {
            instant = parsed.ToUniversalTime();
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    internal sealed record ListPartiesQueryPayload(
        int Page,
        int PageSize,
        PartyType? Type,
        bool? Active,
        DateTimeOffset? CreatedAfter,
        DateTimeOffset? CreatedBefore,
        DateTimeOffset? ModifiedAfter,
        DateTimeOffset? ModifiedBefore);

    private sealed record ListPartiesQueryPayloadWire(
        int? Page = null,
        int? PageSize = null,
        string? Type = null,
        bool? Active = null,
        string? CreatedAfter = null,
        string? CreatedBefore = null,
        string? ModifiedAfter = null,
        string? ModifiedBefore = null);

    internal sealed record SearchPartiesQueryPayload(
        string Query,
        int Page,
        int PageSize,
        PartyType? Type,
        bool? Active,
        string? Mode,
        string? CaseId);

    private sealed record SearchPartiesQueryPayloadWire(
        string? Query = null,
        int? Page = null,
        int? PageSize = null,
        string? Type = null,
        bool? Active = null,
        string? Mode = null,
        string? CaseId = null);
}
