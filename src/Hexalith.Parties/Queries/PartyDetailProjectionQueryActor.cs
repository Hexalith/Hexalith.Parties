using System.Text.Json;
using System.Text.RegularExpressions;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Extensions;
using Hexalith.Parties.Projections.Abstractions;
using Hexalith.Parties.Projections.Actors;
using Hexalith.Parties.Projections.Services;

using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.Queries;

public sealed partial class PartyDetailProjectionQueryActor(
    ActorHost host,
    IActorProxyFactory actorProxyFactory,
    ILogger<PartyDetailProjectionQueryActor> logger,
    IProjectionRebuildService? projectionRebuildService = null,
    IPartyErasureRecordStore? erasureRecordStore = null) : Actor(host), IPartyProjectionQueryActor, IProjectionActor
{
    public const string ActorTypeName = nameof(PartyDetailProjectionQueryActor);
    public const string ProjectionType = "party-detail";
    public const string DataPortabilityProjectionType = "party-data-portability";
    public const string GetPartyQueryType = "GetParty";
    public const string PartyDetailQueryType = "PartyDetail";
    public const string ExportPartyDataQueryType = "ExportPartyData";
    public const string GetProcessingRecordsQueryType = "GetProcessingRecords";
    public const string GetErasureCertificateQueryType = "GetErasureCertificate";
    public const string PartyDomain = "party";
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);

    // P3 (Story 2.6, parity with PartyIndexProjectionQueryActor): tenant identifiers must
    // contain only letters, digits, underscore, hyphen, and dot. Defense-in-depth — reject
    // anything else (including ':' which composes the actor key separator, control chars,
    // whitespace, or oversized values) before the value flows into actor proxy construction
    // or log templates. The detail and index adapters share the same allowlist so an upstream
    // gateway change can't relax one path while the other remains strict.
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

        if (!TryResolveActorRoute(Host.Id.GetId(), envelope, out string partyId))
        {
            return QueryResult.Failure(QueryAdapterFailureReason.InvalidEnvelope);
        }

        if (string.Equals(envelope.QueryType, GetProcessingRecordsQueryType, StringComparison.Ordinal))
        {
            IReadOnlyList<ProcessingActivityRecord> records = projectionRebuildService is null
                ? []
                : await projectionRebuildService
                    .GetProcessingRecordsAsync(envelope.TenantId, partyId, CancellationToken.None)
                    .ConfigureAwait(false);

            return QueryResult.FromPayload(
                JsonSerializer.SerializeToElement(records, s_jsonOptions),
                "party-processing-records");
        }

        if (string.Equals(envelope.QueryType, GetErasureCertificateQueryType, StringComparison.Ordinal))
        {
            if (erasureRecordStore is null)
            {
                return QueryResult.Failure(QueryAdapterFailureReason.UnsupportedQueryType);
            }

            ErasureCertificate? certificate = await erasureRecordStore
                .GetCertificateAsync(envelope.TenantId, partyId, CancellationToken.None)
                .ConfigureAwait(false);

            return QueryResult.FromPayload(
                JsonSerializer.SerializeToElement(certificate, s_jsonOptions),
                "party-erasure-certificate");
        }

        string detailActorId = $"{envelope.TenantId}:party-detail:{partyId}";
        Log.PartyDetailQueryRouting(logger, envelope.CorrelationId, envelope.TenantId, envelope.QueryType);

        try
        {
            IPartyDetailProjectionActor detailProxy = actorProxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
                new ActorId(detailActorId),
                nameof(PartyDetailProjectionActor));

            PartyDetailProjectionReadResult readResult = await detailProxy.ReadDetailWithFreshnessAsync().ConfigureAwait(false);
            if (readResult.Detail is null || readResult.Freshness.Status == ProjectionFreshnessStatus.Unavailable)
            {
                return QueryResult.Failure(QueryAdapterFailureReason.ActorNotFoundInfrastructure);
            }

            PartyDetail detail = readResult.Detail with { Freshness = readResult.Freshness };
            if (string.Equals(envelope.QueryType, ExportPartyDataQueryType, StringComparison.Ordinal))
            {
                PartyDataPortabilityPackage package = await BuildPortabilityPackageAsync(
                    envelope,
                    detail,
                    readResult.Freshness).ConfigureAwait(false);

                return QueryResult.FromPayload(
                    JsonSerializer.SerializeToElement(package, s_jsonOptions),
                    DataPortabilityProjectionType);
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
            Log.PartyDetailProjectionNotFound(logger, envelope.CorrelationId, envelope.TenantId, envelope.QueryType);
            return QueryResult.Failure(QueryAdapterFailureReason.ActorNotFoundInfrastructure);
        }
        catch (Exception ex)
        {
            Log.PartyDetailProjectionReadFailed(logger, ex, envelope.CorrelationId, envelope.TenantId, envelope.QueryType);
            return QueryResult.Failure(QueryAdapterFailureReason.ActorException);
        }
    }

    private static bool CanHandle(QueryEnvelope envelope)
        => string.Equals(envelope.Domain, PartyDomain, StringComparison.Ordinal)
            && (string.Equals(envelope.QueryType, PartyDetailQueryType, StringComparison.Ordinal)
                || string.Equals(envelope.QueryType, GetPartyQueryType, StringComparison.Ordinal)
                || string.Equals(envelope.QueryType, ExportPartyDataQueryType, StringComparison.Ordinal)
                || string.Equals(envelope.QueryType, GetProcessingRecordsQueryType, StringComparison.Ordinal)
                || string.Equals(envelope.QueryType, GetErasureCertificateQueryType, StringComparison.Ordinal));

    private async Task<PartyDataPortabilityPackage> BuildPortabilityPackageAsync(
        QueryEnvelope envelope,
        PartyDetail detail,
        ProjectionFreshnessMetadata freshness)
    {
        IReadOnlyList<ProcessingActivityRecord> records = projectionRebuildService is null
            ? []
            : await projectionRebuildService
                .GetProcessingRecordsAsync(envelope.TenantId, detail.Id, CancellationToken.None)
                .ConfigureAwait(false);

        bool unavailable = string.IsNullOrWhiteSpace(detail.DisplayName) || string.IsNullOrWhiteSpace(detail.SortName);
        bool erased = detail.IsErased;
        string status = erased
            ? "Erased"
            : unavailable
                ? "PersonalDataUnavailable"
                : detail.IsRestricted ? "RestrictedExported" : "Exported";

        return new PartyDataPortabilityPackage
        {
            PartyId = detail.Id,
            TenantId = envelope.TenantId,
            Status = status,
            ExportedAt = DateTimeOffset.UtcNow,
            ExportedBy = string.IsNullOrWhiteSpace(envelope.UserId) ? "unknown" : envelope.UserId.Trim(),
            CorrelationId = string.IsNullOrWhiteSpace(envelope.CorrelationId) ? "unspecified" : envelope.CorrelationId.Trim(),
            Party = erased || unavailable ? null : detail,
            ProcessingRecords = records,
            Freshness = freshness,
        };
    }

    private static bool TryResolveActorRoute(string actorId, QueryEnvelope envelope, out string partyId)
    {
        partyId = string.Empty;

        // P3: Reject malformed tenant identifiers at the boundary. envelope.TenantId is concatenated
        // into the downstream actor id; a stray colon, control character, or oversized value from
        // an unsanitized upstream gateway would corrupt routing or escape into log templates.
        if (!s_validTenantId.IsMatch(envelope.TenantId ?? string.Empty))
        {
            return false;
        }

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
            Message = "Routing party detail query: CorrelationId={CorrelationId}, TenantId={TenantId}, QueryType={QueryType}, Stage=PartyDetailQueryRouting")]
        public static partial void PartyDetailQueryRouting(
            ILogger logger,
            string correlationId,
            string tenantId,
            string queryType);

        [LoggerMessage(
            EventId = 8601,
            Level = LogLevel.Warning,
            Message = "Party detail projection actor not found: CorrelationId={CorrelationId}, TenantId={TenantId}, QueryType={QueryType}, Stage=PartyDetailProjectionNotFound")]
        public static partial void PartyDetailProjectionNotFound(
            ILogger logger,
            string correlationId,
            string tenantId,
            string queryType);

        [LoggerMessage(
            EventId = 8602,
            Level = LogLevel.Warning,
            Message = "Party detail projection read failed: CorrelationId={CorrelationId}, TenantId={TenantId}, QueryType={QueryType}, Stage=PartyDetailProjectionReadFailed")]
        public static partial void PartyDetailProjectionReadFailed(
            ILogger logger,
            Exception exception,
            string correlationId,
            string tenantId,
            string queryType);
    }
}
