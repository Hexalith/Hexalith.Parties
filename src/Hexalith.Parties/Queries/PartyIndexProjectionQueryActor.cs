using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Search;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Abstractions;
using Hexalith.Parties.Projections.Actors;
using Hexalith.Parties.Search;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.Queries;

public sealed partial class PartyIndexProjectionQueryActor(
    ActorHost host,
    IActorProxyFactory actorProxyFactory,
    IPartySearchProvider searchProvider,
    IHostApplicationLifetime hostLifetime,
    ILogger<PartyIndexProjectionQueryActor> logger) : Actor(host), IProjectionActor
{
    public const string ActorTypeName = nameof(PartyIndexProjectionQueryActor);
    public const string ListAggregateId = "parties";
    public const string PartyDomain = "party";
    public const string PartyIndexQueryType = "PartyIndex";
    public const string PartySearchQueryType = "PartySearch";
    public const string ProjectionType = "party-index";

    // Unknown fields fail closed so a future contributor cannot bypass tenant authority
    // by adding a payload field whose name is read by the actor.
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    // P17: ISO-8601 timestamps must carry an explicit timezone designator (Z, +HH:MM, -HH:MM, +HHMM, -HHMM).
    // Naive timestamps like "2026-05-10T08:00:00" are rejected to prevent silent UTC assumption
    // that skews the filter by the client's local UTC offset.
    private static readonly Regex s_iso8601WithOffset = new(
        @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(?::\d{2}(?:\.\d+)?)?(?:Z|[+-]\d{2}:?\d{2})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // P3: Tenant identifiers must contain only letters, digits, underscore, hyphen, and dot.
    // Reject anything else (including ':' which composes the actor key separator, control chars,
    // whitespace, or oversized values) before the value flows into actor proxy construction or
    // log templates.
    private static readonly Regex s_validTenantId = new(
        @"^[A-Za-z0-9_\-\.]{1,128}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<QueryResult> QueryAsync(QueryEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (!CanHandle(envelope))
        {
            return QueryResult.Failure(QueryAdapterFailureReason.UnsupportedQueryType);
        }

        if (!TryResolveActorRoute(Host.Id.GetId(), envelope))
        {
            return QueryResult.Failure(QueryAdapterFailureReason.InvalidEnvelope);
        }

        if (IsPartySearch(envelope))
        {
            if (!TryParseSearchPayload(envelope.Payload, out SearchPartiesQueryPayload payload))
            {
                return QueryResult.Failure(QueryAdapterFailureReason.InvalidEnvelope);
            }

            if (IsUnsupportedSearchMode(payload.Mode))
            {
                return QueryResult.Failure(QueryAdapterFailureReason.UnsupportedQueryType);
            }

            return await QueryEntriesAsync(envelope, readResult =>
            {
                // P13: Forward the host's ApplicationStopping token so the in-actor matching and
                // sort loops abort during graceful shutdown. The per-request CT path
                // (IProjectionActor.QueryAsync gaining a CancellationToken) is tracked as a
                // cross-submodule follow-up in deferred-work.md.
                PagedResult<PartySearchResult> page = searchProvider.Search(
                    readResult.Entries.Values,
                    payload.Query,
                    payload.Type,
                    payload.Active,
                    payload.Page,
                    payload.PageSize,
                    hostLifetime.ApplicationStopping) with
                {
                    Freshness = readResult.Freshness,
                };

                return QueryResult.FromPayload(JsonSerializer.SerializeToElement(page, s_jsonOptions), ProjectionType);
            }).ConfigureAwait(false);
        }

        if (!TryParseListPayload(envelope.Payload, out ListPartiesQueryPayload listPayload))
        {
            return QueryResult.Failure(QueryAdapterFailureReason.InvalidEnvelope);
        }

        return await QueryEntriesAsync(envelope, readResult =>
        {
            PagedResult<PartyIndexEntry> page = PartySearchResultsBuilder.BuildPagedList(
                readResult.Entries.Values,
                listPayload.Type,
                listPayload.Active,
                listPayload.CreatedAfter,
                listPayload.CreatedBefore,
                listPayload.ModifiedAfter,
                listPayload.ModifiedBefore,
                listPayload.Page,
                listPayload.PageSize) with
            {
                Freshness = readResult.Freshness,
            };

            return QueryResult.FromPayload(JsonSerializer.SerializeToElement(page, s_jsonOptions), ProjectionType);
        }).ConfigureAwait(false);
    }

    private async Task<QueryResult> QueryEntriesAsync(
        QueryEnvelope envelope,
        Func<PartyIndexProjectionReadResult, QueryResult> createResult)
    {
        string indexActorId = $"{envelope.TenantId}:party-index";

        // P14: Log templates intentionally omit the constructed actor id. The tenant id and
        // query type are sufficient diagnostics; the actor key is a derivable storage identifier
        // that the Party-Mode clarifications list as forbidden in diagnostics.
        Log.PartyIndexQueryRouting(logger, envelope.CorrelationId, envelope.TenantId, envelope.QueryType);

        try
        {
            IPartyIndexProjectionActor indexProxy = actorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
                new ActorId(indexActorId),
                nameof(PartyIndexProjectionActor));

            PartyIndexProjectionReadResult readResult = await ReadEntriesWithFreshnessAsync(indexProxy).ConfigureAwait(false);
            if (readResult.Freshness.Status == ProjectionFreshnessStatus.Unavailable)
            {
                return QueryResult.Failure(QueryAdapterFailureReason.ActorNotFoundInfrastructure);
            }

            return createResult(readResult);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (IsProjectionActorNotFound(ex))
        {
            Log.PartyIndexProjectionNotFound(logger, envelope.CorrelationId, envelope.TenantId, envelope.QueryType);
            return QueryResult.Failure(QueryAdapterFailureReason.ActorNotFoundInfrastructure);
        }
        catch (Exception ex)
        {
            Log.PartyIndexProjectionReadFailed(
                logger,
                envelope.CorrelationId,
                envelope.TenantId,
                envelope.QueryType,
                ex.GetType().Name);
            return QueryResult.Failure(QueryAdapterFailureReason.ActorException);
        }
    }

    private static bool CanHandle(QueryEnvelope envelope)
        => string.Equals(envelope.Domain, PartyDomain, StringComparison.Ordinal)
            && (string.Equals(envelope.QueryType, PartyIndexQueryType, StringComparison.Ordinal)
                || IsPartySearch(envelope));

    private static bool IsPartySearch(QueryEnvelope envelope)
        => string.Equals(envelope.QueryType, PartySearchQueryType, StringComparison.Ordinal);

    private static bool TryResolveActorRoute(string actorId, QueryEnvelope envelope)
    {
        // P3: Reject malformed tenant identifiers at the boundary. Downstream proxy calls
        // concatenate envelope.TenantId into the actor id; a stray colon, control character,
        // or whitespace from an unsanitized upstream gateway would corrupt routing.
        if (!s_validTenantId.IsMatch(envelope.TenantId ?? string.Empty))
        {
            return false;
        }

        string[] segments = actorId.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length != 3
            || !string.Equals(segments[0], ProjectionType, StringComparison.Ordinal)
            || !string.Equals(segments[1], envelope.TenantId, StringComparison.Ordinal)
            || !string.Equals(segments[2], ListAggregateId, StringComparison.Ordinal))
        {
            return false;
        }

        string requestedEntityId = string.IsNullOrWhiteSpace(envelope.EntityId)
            ? envelope.AggregateId
            : envelope.EntityId;
        return string.Equals(envelope.AggregateId, ListAggregateId, StringComparison.Ordinal)
            && string.Equals(requestedEntityId, ListAggregateId, StringComparison.Ordinal);
    }

    private static async Task<PartyIndexProjectionReadResult> ReadEntriesWithFreshnessAsync(IPartyIndexProjectionActor indexProxy)
    {
        Task<PartyIndexProjectionReadResult>? readTask = null;
        try
        {
            readTask = indexProxy.GetEntriesReadAsync();
        }
        catch (NotImplementedException)
        {
        }

        if (readTask is not null)
        {
            try
            {
                PartyIndexProjectionReadResult? result = await readTask.ConfigureAwait(false);
                if (result is not null)
                {
                    return result;
                }
            }
            catch (NotImplementedException)
            {
            }
        }

        IReadOnlyDictionary<string, PartyIndexEntry> entries = await indexProxy.GetEntriesAsync().ConfigureAwait(false);
        ProjectionFreshnessStatus status = await IsRebuildingAsync(indexProxy).ConfigureAwait(false)
            ? ProjectionFreshnessStatus.Rebuilding
            : ProjectionFreshnessStatus.Current;
        return new PartyIndexProjectionReadResult
        {
            Entries = entries,
            Freshness = status == ProjectionFreshnessStatus.Rebuilding
                ? ProjectionFreshnessMetadata.Create(status, ProjectionFreshnessMetadata.WarningProjectionRebuilding)
                : ProjectionFreshnessMetadata.Create(status),
        };
    }

    private static async Task<bool> IsRebuildingAsync(IPartyIndexProjectionActor indexProxy)
    {
        try
        {
            Task<bool>? task = indexProxy.IsRebuildingAsync();
            return task is not null && await task.ConfigureAwait(false);
        }
        catch (NotImplementedException)
        {
            return false;
        }
    }

    private static bool TryParseListPayload(byte[] payloadBytes, out ListPartiesQueryPayload payload)
    {
        payload = new ListPartiesQueryPayload(
            Page: 1,
            PageSize: 20,
            Type: null,
            Active: null,
            CreatedAfter: null,
            CreatedBefore: null,
            ModifiedAfter: null,
            ModifiedBefore: null);

        // P2: List queries are Tier 1 (entityId-scoped) and always carry payload.
        // An empty payload is a malformed envelope, not a "use defaults" signal.
        if (payloadBytes.Length == 0)
        {
            return false;
        }

        ListPartiesQueryPayloadWire? wire;
        try
        {
            wire = JsonSerializer.Deserialize<ListPartiesQueryPayloadWire>(payloadBytes, s_jsonOptions);
        }
        catch (JsonException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            // P9: Malformed UTF-8, encoding errors, and constructor-binding failures from
            // System.Text.Json surface as ArgumentException variants. Treat as InvalidEnvelope;
            // the outer catch (Exception) in QueryEntriesAsync only protects the projection-read
            // scope, not the payload-parse scope.
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }

        if (wire is null)
        {
            return false;
        }

        // Reject negative or zero Page/PageSize at the boundary so client bugs surface
        // rather than being silently coerced to defaults inside the builder.
        if ((wire.Page is { } pageValue && pageValue < 1)
            || (wire.PageSize is { } pageSizeValue && pageSizeValue < 1))
        {
            return false;
        }

        if (!TryParsePartyType(wire.Type, out PartyType? type)
            || !TryParseInstant(wire.CreatedAfter, out DateTimeOffset? createdAfter)
            || !TryParseInstant(wire.CreatedBefore, out DateTimeOffset? createdBefore)
            || !TryParseInstant(wire.ModifiedAfter, out DateTimeOffset? modifiedAfter)
            || !TryParseInstant(wire.ModifiedBefore, out DateTimeOffset? modifiedBefore))
        {
            return false;
        }

        if ((createdAfter is not null && createdBefore is not null && createdAfter.Value > createdBefore.Value)
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

    private static bool TryParseSearchPayload(byte[] payloadBytes, out SearchPartiesQueryPayload payload)
    {
        payload = new SearchPartiesQueryPayload(
            Query: string.Empty,
            Page: 1,
            PageSize: 20,
            Type: null,
            Active: null,
            Mode: null,
            CaseId: null);

        if (payloadBytes.Length == 0)
        {
            return false;
        }

        SearchPartiesQueryPayloadWire? wire;
        try
        {
            wire = JsonSerializer.Deserialize<SearchPartiesQueryPayloadWire>(payloadBytes, s_jsonOptions);
        }
        catch (JsonException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            // P9: See TryParseListPayload for the broadened-catch rationale.
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }

        if (wire is null)
        {
            return false;
        }

        if ((wire.Page is { } pageValue && pageValue < 1)
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

    private static bool IsUnsupportedSearchMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return false;
        }

        return !string.Equals(mode, "Lexical", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(mode, "DisplayName", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParsePartyType(string? value, out PartyType? type)
    {
        type = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        // P1: Strict allowlist of public party-type names. Enum.TryParse accepts numeric
        // strings ("0", "1") and the internal `Unknown` sentinel; AC2 restricts the wire
        // to `Person | Organization` only.
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

        // P17: Require an explicit timezone designator. AssumeUniversal would silently treat
        // a naive timestamp ("2026-05-10T08:00:00") as UTC, skewing the filter by the client's
        // local offset. Reject naive timestamps as InvalidEnvelope so callers are unambiguous.
        if (!s_iso8601WithOffset.IsMatch(value))
        {
            return false;
        }

        if (!DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out DateTimeOffset parsed))
        {
            return false;
        }

        // Defensive guard. ToUniversalTime on a successfully-parsed DateTimeOffset cannot
        // throw in current .NET (the parser already rejects out-of-range UTC instants), but
        // wrapping is cheap insurance against a future runtime change classifying overflow as
        // an unhandled ActorException instead of a bounded InvalidEnvelope.
        try
        {
            instant = parsed.ToUniversalTime();
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }

        return true;
    }

    private static bool IsProjectionActorNotFound(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return ContainsNotFoundMarker(exception.Message)
            || ContainsNotFoundMarker(exception.InnerException?.Message);

        static bool ContainsNotFoundMarker(string? message)
            => !string.IsNullOrWhiteSpace(message)
                && (message.Contains("actor type not registered", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("did not find address for actor", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("could not find address for actor", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("no address found for actor", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("actor not found", StringComparison.OrdinalIgnoreCase));
    }

    private sealed record ListPartiesQueryPayload(
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

    private sealed record SearchPartiesQueryPayload(
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

    private static partial class Log
    {
        // P14: Templates omit ActorId. The actor key is a structured storage identifier that
        // Party-Mode clarifications classify as forbidden in diagnostics. Tenant + correlation
        // + query type are sufficient for ops; the actor key is deterministic from the tenant.
        [LoggerMessage(
            EventId = 8610,
            Level = LogLevel.Debug,
            Message = "Routing party index query: CorrelationId={CorrelationId}, TenantId={TenantId}, QueryType={QueryType}, Stage=PartyIndexQueryRouting")]
        public static partial void PartyIndexQueryRouting(
            ILogger logger,
            string correlationId,
            string tenantId,
            string queryType);

        [LoggerMessage(
            EventId = 8611,
            Level = LogLevel.Warning,
            Message = "Party index projection actor not found: CorrelationId={CorrelationId}, TenantId={TenantId}, QueryType={QueryType}, Stage=PartyIndexProjectionNotFound")]
        public static partial void PartyIndexProjectionNotFound(
            ILogger logger,
            string correlationId,
            string tenantId,
            string queryType);

        [LoggerMessage(
            EventId = 8612,
            Level = LogLevel.Warning,
            Message = "Party index projection read failed: CorrelationId={CorrelationId}, TenantId={TenantId}, QueryType={QueryType}, ExceptionType={ExceptionType}, Stage=PartyIndexProjectionReadFailed")]
        public static partial void PartyIndexProjectionReadFailed(
            ILogger logger,
            string correlationId,
            string tenantId,
            string queryType,
            string exceptionType);
    }
}
