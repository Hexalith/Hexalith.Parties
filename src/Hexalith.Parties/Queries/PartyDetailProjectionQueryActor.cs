using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Extensions;
using Hexalith.Parties.Projections.Abstractions;
using Hexalith.Parties.Projections.Actors;

using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.Queries;

public sealed partial class PartyDetailProjectionQueryActor(
    ActorHost host,
    IActorProxyFactory actorProxyFactory,
    ILogger<PartyDetailProjectionQueryActor> logger) : Actor(host), IProjectionActor
{
    public const string ActorTypeName = nameof(PartyDetailProjectionQueryActor);
    public const string ProjectionType = "party-detail";
    public const string GetPartyQueryType = "GetParty";
    public const string PartyDetailQueryType = "PartyDetail";
    public const string PartyDomain = "party";
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<QueryResult> QueryAsync(QueryEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (!CanHandle(envelope))
        {
            return QueryResult.Failure(QueryAdapterFailureReason.UnsupportedQueryType);
        }

        if (!TryResolveActorRoute(Host.Id.GetId(), envelope, out string partyId))
        {
            return QueryResult.Failure(QueryAdapterFailureReason.InvalidEnvelope);
        }

        string detailActorId = $"{envelope.TenantId}:party-detail:{partyId}";
        Log.PartyDetailQueryRouting(logger, envelope.CorrelationId, envelope.TenantId, envelope.QueryType, detailActorId);

        try
        {
            IPartyDetailProjectionActor detailProxy = actorProxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
                new ActorId(detailActorId),
                nameof(PartyDetailProjectionActor));

            PartyDetail? detail = await detailProxy.ReadDetailAsync().ConfigureAwait(false);
            if (detail is null)
            {
                return QueryResult.Failure(QueryAdapterFailureReason.ActorNotFoundInfrastructure);
            }

            return QueryResult.FromPayload(JsonSerializer.SerializeToElement(detail, s_jsonOptions), ProjectionType);
        }
        catch (OperationCanceledException)
        {
            // Cancellation must propagate so callers (and the EventStore router) can honor terminal cancellation
            // without retry; converting it to a Failure result would mask the cancel signal.
            throw;
        }
        catch (Exception ex) when (IsProjectionActorNotFound(ex))
        {
            Log.PartyDetailProjectionNotFound(logger, envelope.CorrelationId, envelope.TenantId, envelope.QueryType, detailActorId);
            return QueryResult.Failure(QueryAdapterFailureReason.ActorNotFoundInfrastructure);
        }
        catch (Exception ex)
        {
            Log.PartyDetailProjectionReadFailed(logger, ex, envelope.CorrelationId, envelope.TenantId, envelope.QueryType, detailActorId);
            return QueryResult.Failure(QueryAdapterFailureReason.ActorException);
        }
    }

    private static bool CanHandle(QueryEnvelope envelope)
        => string.Equals(envelope.Domain, PartyDomain, StringComparison.Ordinal)
            && (string.Equals(envelope.QueryType, PartyDetailQueryType, StringComparison.Ordinal)
                || string.Equals(envelope.QueryType, GetPartyQueryType, StringComparison.Ordinal));

    private static bool TryResolveActorRoute(string actorId, QueryEnvelope envelope, out string partyId)
    {
        partyId = string.Empty;
        string[] segments = actorId.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length != 3
            || !string.Equals(segments[0], ProjectionType, StringComparison.Ordinal)
            || !string.Equals(segments[1], envelope.TenantId, StringComparison.Ordinal))
        {
            return false;
        }

        string requestedPartyId = string.IsNullOrWhiteSpace(envelope.EntityId)
            ? envelope.AggregateId
            : envelope.EntityId;
        if (!string.Equals(segments[2], requestedPartyId, StringComparison.Ordinal))
        {
            return false;
        }

        partyId = requestedPartyId;
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

    private static partial class Log
    {
        [LoggerMessage(
            EventId = 8600,
            Level = LogLevel.Debug,
            Message = "Routing party detail query: CorrelationId={CorrelationId}, TenantId={TenantId}, QueryType={QueryType}, ActorId={ActorId}, Stage=PartyDetailQueryRouting")]
        public static partial void PartyDetailQueryRouting(
            ILogger logger,
            string correlationId,
            string tenantId,
            string queryType,
            string actorId);

        [LoggerMessage(
            EventId = 8601,
            Level = LogLevel.Warning,
            Message = "Party detail projection actor not found: CorrelationId={CorrelationId}, TenantId={TenantId}, QueryType={QueryType}, ActorId={ActorId}, Stage=PartyDetailProjectionNotFound")]
        public static partial void PartyDetailProjectionNotFound(
            ILogger logger,
            string correlationId,
            string tenantId,
            string queryType,
            string actorId);

        [LoggerMessage(
            EventId = 8602,
            Level = LogLevel.Warning,
            Message = "Party detail projection read failed: CorrelationId={CorrelationId}, TenantId={TenantId}, QueryType={QueryType}, ActorId={ActorId}, Stage=PartyDetailProjectionReadFailed")]
        public static partial void PartyDetailProjectionReadFailed(
            ILogger logger,
            Exception exception,
            string correlationId,
            string tenantId,
            string queryType,
            string actorId);
    }
}
