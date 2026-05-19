using System.Globalization;
using System.Text.Json;

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
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);

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
        Log.PartyIndexQueryRouting(logger, envelope.CorrelationId, envelope.TenantId, envelope.QueryType, indexActorId);

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
            Log.PartyIndexProjectionNotFound(logger, envelope.CorrelationId, envelope.TenantId, envelope.QueryType, indexActorId);
            return QueryResult.Failure(QueryAdapterFailureReason.ActorNotFoundInfrastructure);
        }
        catch (Exception ex)
        {
            Log.PartyIndexProjectionReadFailed(
                logger,
                envelope.CorrelationId,
                envelope.TenantId,
                envelope.QueryType,
                indexActorId,
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

        ListPartiesQueryPayloadWire? wire;
        try
        {
            wire = payloadBytes.Length == 0
                ? new ListPartiesQueryPayloadWire()
                : JsonSerializer.Deserialize<ListPartiesQueryPayloadWire>(payloadBytes, s_jsonOptions);
        }
        catch (JsonException)
        {
            return false;
        }

        if (wire is null)
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

        if (Enum.TryParse(value, ignoreCase: true, out PartyType parsed) && Enum.IsDefined(parsed))
        {
            type = parsed;
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

        if (!DateTimeOffset.TryParseExact(
            value,
            "O",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out DateTimeOffset parsed))
        {
            return false;
        }

        instant = parsed.ToUniversalTime();
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
        [LoggerMessage(
            EventId = 8610,
            Level = LogLevel.Debug,
            Message = "Routing party index query: CorrelationId={CorrelationId}, TenantId={TenantId}, QueryType={QueryType}, ActorId={ActorId}, Stage=PartyIndexQueryRouting")]
        public static partial void PartyIndexQueryRouting(
            ILogger logger,
            string correlationId,
            string tenantId,
            string queryType,
            string actorId);

        [LoggerMessage(
            EventId = 8611,
            Level = LogLevel.Warning,
            Message = "Party index projection actor not found: CorrelationId={CorrelationId}, TenantId={TenantId}, QueryType={QueryType}, ActorId={ActorId}, Stage=PartyIndexProjectionNotFound")]
        public static partial void PartyIndexProjectionNotFound(
            ILogger logger,
            string correlationId,
            string tenantId,
            string queryType,
            string actorId);

        [LoggerMessage(
            EventId = 8612,
            Level = LogLevel.Warning,
            Message = "Party index projection read failed: CorrelationId={CorrelationId}, TenantId={TenantId}, QueryType={QueryType}, ActorId={ActorId}, ExceptionType={ExceptionType}, Stage=PartyIndexProjectionReadFailed")]
        public static partial void PartyIndexProjectionReadFailed(
            ILogger logger,
            string correlationId,
            string tenantId,
            string queryType,
            string actorId,
            string exceptionType);
    }
}
