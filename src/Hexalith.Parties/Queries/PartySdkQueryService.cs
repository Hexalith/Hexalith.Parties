using System.Text.Json;
using System.Text.RegularExpressions;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Search;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Projections.Configuration;
using Hexalith.Parties.Projections.Models;
using Hexalith.Parties.Projections.Services;
using Hexalith.Parties.Search;

using Microsoft.Extensions.Options;

namespace Hexalith.Parties.Queries;

/// <summary>Reads the canonical SDK read models without routing through rollback actors.</summary>
public sealed partial class PartySdkQueryService(
    IReadModelStore readModelStore,
    IOptions<PartySdkReadModelOptions> options,
    TimeProvider timeProvider,
    IPartySearchProvider searchProvider,
    IProjectionRebuildService projectionRebuildService,
    IPartyErasureRecordStore erasureRecordStore)
{
    private static readonly JsonSerializerOptions s_jsonOptions = PartiesJsonOptions.Default;

    [GeneratedRegex(@"^[A-Za-z0-9_\-\.]{1,128}$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidTenantIdRegex();

    public Task<QueryResult> GetPartyAsync(QueryEnvelope query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return ReadDetailAsync(query, cancellationToken);
    }

    public Task<QueryResult> GetPartyDetailAsync(QueryEnvelope query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return ReadDetailAsync(query, cancellationToken);
    }

    public async Task<QueryResult> ExportPartyDataAsync(QueryEnvelope query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!TryValidateDetailEnvelope(query, out string partyId))
        {
            return QueryResult.Failure(QueryAdapterFailureReason.InvalidEnvelope);
        }

        try
        {
            ReadModelEntry<PartyDetailSdkReadModel> read = await ReadDetailModelAsync(query.TenantId, partyId, cancellationToken)
                .ConfigureAwait(false);
            if (read.Value?.Detail is not { } stored)
            {
                return QueryResult.Failure(QueryAdapterFailureReason.ActorNotFoundInfrastructure);
            }

            DateTimeOffset now = timeProvider.GetUtcNow();
            ProjectionFreshnessMetadata freshness = ToPartiesFreshness(read.Value, now);
            PartyDetail detail = stored with { Freshness = freshness };
            IReadOnlyList<ProcessingActivityRecord> records = await projectionRebuildService
                .GetProcessingRecordsAsync(query.TenantId, partyId, cancellationToken)
                .ConfigureAwait(false);
            bool unavailable = string.IsNullOrWhiteSpace(detail.DisplayName) || string.IsNullOrWhiteSpace(detail.SortName);
            var package = new PartyDataPortabilityPackage
            {
                PartyId = detail.Id,
                TenantId = query.TenantId,
                Status = detail.IsErased
                    ? "Erased"
                    : unavailable
                        ? "PersonalDataUnavailable"
                        : detail.IsRestricted ? "RestrictedExported" : "Exported",
                ExportedAt = now,
                ExportedBy = string.IsNullOrWhiteSpace(query.UserId) ? "unknown" : query.UserId.Trim(),
                CorrelationId = string.IsNullOrWhiteSpace(query.CorrelationId) ? "unspecified" : query.CorrelationId.Trim(),
                Party = detail.IsErased || unavailable ? null : detail,
                ProcessingRecords = records,
                Freshness = freshness,
            };
            return Success(package, PartyDetailProjectionQueryActor.DataPortabilityProjectionType, read.Value, read.ETag, now);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return QueryResult.Failure(QueryAdapterFailureReason.ActorException);
        }
    }

    public async Task<QueryResult> GetProcessingRecordsAsync(QueryEnvelope query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!TryValidateDetailEnvelope(query, out string partyId))
        {
            return QueryResult.Failure(QueryAdapterFailureReason.InvalidEnvelope);
        }

        IReadOnlyList<ProcessingActivityRecord> records = await projectionRebuildService
            .GetProcessingRecordsAsync(query.TenantId, partyId, cancellationToken)
            .ConfigureAwait(false);
        return QueryResult.FromPayload(JsonSerializer.SerializeToElement(records, s_jsonOptions), "party-processing-records");
    }

    public async Task<QueryResult> GetErasureStatusAsync(QueryEnvelope query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!TryValidateDetailEnvelope(query, out string partyId))
        {
            return QueryResult.Failure(QueryAdapterFailureReason.InvalidEnvelope);
        }

        PartyErasureStatusRecord? status = await erasureRecordStore
            .GetStatusAsync(query.TenantId, partyId, cancellationToken)
            .ConfigureAwait(false);
        if (status is null)
        {
            ReadModelEntry<PartyDetailSdkReadModel> read = await ReadDetailModelAsync(query.TenantId, partyId, cancellationToken)
                .ConfigureAwait(false);
            PartyDetail? detail = read.Value?.Detail;
            status = detail?.IsErased == true
                ? new PartyErasureStatusRecord
                {
                    PartyId = detail.Id,
                    TenantId = query.TenantId,
                    Status = ErasureStatus.Erased.ToString(),
                    UpdatedAt = detail.ErasedAt ?? detail.LastModifiedAt,
                    ErasedAt = detail.ErasedAt,
                }
                : null;
        }

        return QueryResult.FromPayload(JsonSerializer.SerializeToElement(status, s_jsonOptions), "party-erasure-status");
    }

    public async Task<QueryResult> GetErasureCertificateAsync(QueryEnvelope query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!TryValidateDetailEnvelope(query, out string partyId))
        {
            return QueryResult.Failure(QueryAdapterFailureReason.InvalidEnvelope);
        }

        ErasureCertificate? certificate = await erasureRecordStore
            .GetCertificateAsync(query.TenantId, partyId, cancellationToken)
            .ConfigureAwait(false);
        return QueryResult.FromPayload(JsonSerializer.SerializeToElement(certificate, s_jsonOptions), "party-erasure-certificate");
    }

    public async Task<QueryResult> GetPartyIndexAsync(QueryEnvelope query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!TryValidateIndexEnvelope(query)
            || !PartyIndexProjectionQueryActor.TryParseListPayload(query.Payload, out PartyIndexProjectionQueryActor.ListPartiesQueryPayload payload))
        {
            return QueryResult.Failure(QueryAdapterFailureReason.InvalidEnvelope);
        }

        return await ReadIndexAsync(query, cancellationToken, (model, freshness) =>
        {
            PagedResult<PartyIndexEntry> page = PartySearchResultsBuilder.BuildPagedList(
                model.Entries.Values,
                payload.Type,
                payload.Active,
                payload.CreatedAfter,
                payload.CreatedBefore,
                payload.ModifiedAfter,
                payload.ModifiedBefore,
                payload.Page,
                payload.PageSize) with
            {
                Freshness = freshness,
            };
            return page;
        }).ConfigureAwait(false);
    }

    public async Task<QueryResult> SearchPartiesAsync(QueryEnvelope query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!TryValidateIndexEnvelope(query)
            || !PartyIndexProjectionQueryActor.TryParseSearchPayload(query.Payload, out PartyIndexProjectionQueryActor.SearchPartiesQueryPayload payload))
        {
            return QueryResult.Failure(QueryAdapterFailureReason.InvalidEnvelope);
        }

        if (PartyIndexProjectionQueryActor.IsUnsupportedSearchMode(payload.Mode))
        {
            return QueryResult.Failure(QueryAdapterFailureReason.UnsupportedQueryType);
        }

        return await ReadIndexAsync(query, cancellationToken, (model, freshness) =>
        {
            PagedResult<PartySearchResult> page = searchProvider.Search(
                model.Entries.Values,
                payload.Query,
                payload.Type,
                payload.Active,
                payload.Page,
                payload.PageSize,
                cancellationToken) with
            {
                Freshness = freshness,
            };
            return page;
        }).ConfigureAwait(false);
    }

    private async Task<QueryResult> ReadDetailAsync(QueryEnvelope query, CancellationToken cancellationToken)
    {
        if (!TryValidateDetailEnvelope(query, out string partyId))
        {
            return QueryResult.Failure(QueryAdapterFailureReason.InvalidEnvelope);
        }

        try
        {
            ReadModelEntry<PartyDetailSdkReadModel> read = await ReadDetailModelAsync(query.TenantId, partyId, cancellationToken)
                .ConfigureAwait(false);
            if (read.Value?.Detail is not { } stored)
            {
                return QueryResult.Failure(QueryAdapterFailureReason.ActorNotFoundInfrastructure);
            }

            DateTimeOffset now = timeProvider.GetUtcNow();
            PartyDetail detail = stored with { Freshness = ToPartiesFreshness(read.Value, now) };
            return Success(detail, PartyProjectionNames.Detail, read.Value, read.ETag, now);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return QueryResult.Failure(QueryAdapterFailureReason.ActorException);
        }
    }

    private async Task<QueryResult> ReadIndexAsync<TPayload>(
        QueryEnvelope query,
        CancellationToken cancellationToken,
        Func<PartyIndexSdkReadModel, ProjectionFreshnessMetadata, TPayload> createPayload)
    {
        try
        {
            string storeName = StoreName;
            ReadModelEntry<PartyIndexSdkReadModel> read = await readModelStore
                .GetAsync<PartyIndexSdkReadModel>(storeName, PartySdkReadModelAddresses.Index(query.TenantId), cancellationToken)
                .ConfigureAwait(false);
            if (read.Value is not { } model)
            {
                return QueryResult.Failure(QueryAdapterFailureReason.ActorNotFoundInfrastructure);
            }

            DateTimeOffset now = timeProvider.GetUtcNow();
            TPayload payload = createPayload(model, ToPartiesFreshness(model, now));
            return Success(payload, PartyProjectionNames.Index, model, read.ETag, now);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return QueryResult.Failure(QueryAdapterFailureReason.ActorException);
        }
    }

    private Task<ReadModelEntry<PartyDetailSdkReadModel>> ReadDetailModelAsync(
        string tenantId,
        string partyId,
        CancellationToken cancellationToken)
        => readModelStore.GetAsync<PartyDetailSdkReadModel>(
            StoreName,
            PartySdkReadModelAddresses.Detail(tenantId, partyId),
            cancellationToken);

    private QueryResult Success<TPayload>(
        TPayload payload,
        string projectionType,
        IReadModelFreshness freshness,
        string? etag,
        DateTimeOffset now)
    {
        QueryResponseMetadata metadata = freshness.ToQueryResponseMetadata(Thresholds, now, etag) with
        {
            Provenance = QueryResponseProvenance.ProjectionBacked,
        };
        return QueryResult.FromPayload(JsonSerializer.SerializeToElement(payload, s_jsonOptions), projectionType, metadata);
    }

    private ProjectionFreshnessMetadata ToPartiesFreshness(IReadModelFreshness model, DateTimeOffset now)
        => ReadModelFreshness.Classify(model, Thresholds, now) switch
        {
            ReadModelFreshnessState.Current or ReadModelFreshnessState.Aging =>
                ProjectionFreshnessMetadata.Create(ProjectionFreshnessStatus.Current),
            ReadModelFreshnessState.Stale => ProjectionFreshnessMetadata.Create(ProjectionFreshnessStatus.Stale),
            _ => ProjectionFreshnessMetadata.Create(
                ProjectionFreshnessStatus.Unavailable,
                ProjectionFreshnessMetadata.WarningProjectionStateUnavailable),
        };

    private ReadModelFreshnessThresholds Thresholds => ReadModelFreshnessThresholds.Create(
        TimeSpan.FromSeconds(options.Value.FreshnessAgingSeconds),
        TimeSpan.FromSeconds(options.Value.FreshnessStaleSeconds));

    private string StoreName
    {
        get
        {
            string value = options.Value.ReadModelStateStoreName;
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            return value;
        }
    }

    private static bool TryValidateDetailEnvelope(QueryEnvelope query, out string partyId)
    {
        partyId = string.IsNullOrWhiteSpace(query.EntityId) ? query.AggregateId : query.EntityId;
        return ValidTenantIdRegex().IsMatch(query.TenantId ?? string.Empty)
            && !string.IsNullOrWhiteSpace(query.AggregateId)
            && string.Equals(partyId, query.AggregateId, StringComparison.Ordinal)
            && !partyId.Contains(':', StringComparison.Ordinal);
    }

    private static bool TryValidateIndexEnvelope(QueryEnvelope query)
    {
        string entityId = string.IsNullOrWhiteSpace(query.EntityId) ? query.AggregateId : query.EntityId;
        return ValidTenantIdRegex().IsMatch(query.TenantId ?? string.Empty)
            && string.Equals(query.AggregateId, PartyIndexProjectionQueryActor.ListAggregateId, StringComparison.Ordinal)
            && string.Equals(entityId, PartyIndexProjectionQueryActor.ListAggregateId, StringComparison.Ordinal);
    }
}
