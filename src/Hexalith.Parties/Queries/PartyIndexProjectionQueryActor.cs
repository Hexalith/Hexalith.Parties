using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Abstractions;
using Hexalith.Parties.Projections.Actors;
using Hexalith.Parties.Search;

using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.Queries;

public sealed partial class PartyIndexProjectionQueryActor(
    ActorHost host,
    IActorProxyFactory actorProxyFactory,
    ILogger<PartyIndexProjectionQueryActor> logger) : Actor(host), IProjectionActor
{
    public const string ActorTypeName = nameof(PartyIndexProjectionQueryActor);
    public const string ListAggregateId = "parties";
    public const string PartyDomain = "party";
    public const string PartyIndexQueryType = "PartyIndex";
    public const string ProjectionType = "party-index";

    // P13: Unknown fields fail closed so a future contributor cannot bypass tenant authority
    // by adding a payload field whose name is read by the actor.
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

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

        if (!TryParsePayload(envelope.Payload, out ListPartiesQueryPayload payload))
        {
            return QueryResult.Failure(QueryAdapterFailureReason.InvalidEnvelope);
        }

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

            IReadOnlyDictionary<string, PartyIndexEntry> entries = await indexProxy.GetEntriesAsync().ConfigureAwait(false);
            PagedResult<PartyIndexEntry> page = PartySearchResultsBuilder.BuildPagedList(
                entries.Values,
                payload.Type,
                payload.Active,
                payload.CreatedAfter,
                payload.CreatedBefore,
                payload.ModifiedAfter,
                payload.ModifiedBefore,
                payload.Page,
                payload.PageSize);

            return QueryResult.FromPayload(JsonSerializer.SerializeToElement(page, s_jsonOptions), ProjectionType);
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
            && string.Equals(envelope.QueryType, PartyIndexQueryType, StringComparison.Ordinal);

    private static bool TryResolveActorRoute(string actorId, QueryEnvelope envelope)
    {
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

    private static bool TryParsePayload(byte[] payloadBytes, out ListPartiesQueryPayload payload)
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

        if (wire is null)
        {
            return false;
        }

        // P3: Reject negative or zero Page/PageSize at the boundary so client bugs surface
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

        // P12: Accept any well-formed ISO-8601 representation (matches System.Text.Json default
        // round-trip semantics). Internal clients emit the strict "O" form via HttpPartiesQueryClient,
        // but third-party adopters commonly use Z-suffixed or fraction-less forms.
        if (!DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind | DateTimeStyles.AssumeUniversal,
            out DateTimeOffset parsed))
        {
            return false;
        }

        // P4: Defensive guard. ToUniversalTime on a successfully-parsed DateTimeOffset cannot
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
