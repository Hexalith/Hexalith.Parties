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

    // Count decoded UTF-16 units before trimming or building the cursor scope.
    private const int MaximumSearchStringLength = 512;

    private static readonly JsonSerializerOptions s_jsonOptions = new(PartiesJsonOptions.Default)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private static readonly Regex s_iso8601WithOffset = new(
        @"\A\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(?::\d{2}(?:\.\d+)?)?(?:Z|[+-]\d{2}:?\d{2})\z",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    internal static bool TryParseListPayload(byte[]? payloadBytes, out ListPartiesQueryPayload payload, bool ignorePayloadPaging = false)
    {
        payload = new ListPartiesQueryPayload(1, 20, null, null, null, null, null, null);
        if (payloadBytes is null || payloadBytes.Length == 0)
        {
            return false;
        }

        ListPartiesQueryPayloadWire? wire;
        try
        {
            wire = DeserializePayload<ListPartiesQueryPayloadWire>(payloadBytes, ignorePayloadPaging);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or NotSupportedException or InvalidOperationException)
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

    internal static bool TryParseSearchPayload(byte[]? payloadBytes, out SearchPartiesQueryPayload payload, bool ignorePayloadPaging = false)
    {
        payload = new SearchPartiesQueryPayload(string.Empty, 1, 20, null, null, null, null);
        if (payloadBytes is null || payloadBytes.Length == 0)
        {
            return false;
        }

        SearchPartiesQueryPayloadWire? wire;
        try
        {
            wire = DeserializePayload<SearchPartiesQueryPayloadWire>(payloadBytes, ignorePayloadPaging);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or NotSupportedException or InvalidOperationException)
        {
            return false;
        }

        if (wire is null
            || wire.Query is { Length: > MaximumSearchStringLength }
            || wire.Mode is { Length: > MaximumSearchStringLength }
            || wire.CaseId is { Length: > MaximumSearchStringLength }
            || wire.Type is { Length: > MaximumSearchStringLength }
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
        if (value is null)
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
        if (value is null)
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

    private static TPayload? DeserializePayload<TPayload>(byte[] bytes, bool ignorePayloadPaging)
    {
        using JsonDocument document = JsonDocument.Parse(bytes);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return default;
        }

        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        using MemoryStream normalized = new();
        using (Utf8JsonWriter writer = new(normalized))
        {
            writer.WriteStartObject();
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    return default;
                }

                if (ignorePayloadPaging && (property.Name.Equals("Page", StringComparison.OrdinalIgnoreCase)
                    || property.Name.Equals("PageSize", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                property.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        return JsonSerializer.Deserialize<TPayload>(normalized.ToArray(), s_jsonOptions);
    }
}
