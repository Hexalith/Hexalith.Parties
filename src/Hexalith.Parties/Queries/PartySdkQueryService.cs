using System.Buffers;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Queries;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Search;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Projections.Configuration;
using Hexalith.Parties.Projections.Models;
using Hexalith.Parties.Search;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.Parties.Queries;

/// <summary>Reads the canonical SDK read models without routing through rollback actors.</summary>
public sealed partial class PartySdkQueryService(
    IReadModelStore readModelStore,
    IOptions<PartySdkReadModelOptions> options,
    TimeProvider timeProvider,
    IPartySearchProvider searchProvider,
    IPartyErasureRecordStore erasureRecordStore,
    IQueryCursorCodec cursorCodec,
    PartySdkLastKnownReadModelCache lastKnownCache,
    ILogger<PartySdkQueryService> logger)
{
    private static readonly JsonSerializerOptions s_jsonOptions = PartiesJsonOptions.Default;
    private static readonly PartyDetailSdkReadModel s_unknownFreshness = new();
    private static readonly SearchValues<char> s_reservedPartyIdChars = SearchValues.Create(":\0|\r\n");

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
            (ReadModelEntry<PartyDetailSdkReadModel> read, bool degraded) = await ReadDetailModelAsync(
                query.TenantId,
                partyId,
                cancellationToken)
                .ConfigureAwait(false);
            if (read.Value?.Detail is not { } stored)
            {
                return QueryResult.Failure(QueryAdapterFailureReason.ActorNotFoundInfrastructure);
            }

            (ReadModelEntry<PartyProcessingSdkReadModel> processing, bool processingDegraded) = await ReadProcessingModelAsync(
                query.TenantId,
                partyId,
                cancellationToken).ConfigureAwait(false);
            degraded = degraded || processingDegraded;
            DateTimeOffset now = timeProvider.GetUtcNow();
            ProjectionFreshnessMetadata freshness = ToPartiesFreshness(read.Value, now, degraded);
            PartyDetail detail = stored with { Freshness = freshness };
            IReadOnlyList<ProcessingActivityRecord> records = processing.Value?.Records ?? [];
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
            return Success(
                package,
                PartyDetailProjectionQueryActor.DataPortabilityProjectionType,
                read.Value,
                read.ETag,
                now,
                degraded);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogQueryFailed(query.QueryType, exception.GetType().Name);
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

        try
        {
            (ReadModelEntry<PartyDetailSdkReadModel> detail, bool detailDegraded) = await ReadDetailModelAsync(
                query.TenantId,
                partyId,
                cancellationToken).ConfigureAwait(false);
            if (detail.Value?.Detail is null)
            {
                return QueryResult.Failure(QueryAdapterFailureReason.ActorNotFoundInfrastructure);
            }

            (ReadModelEntry<PartyProcessingSdkReadModel> read, bool degraded) = await ReadProcessingModelAsync(
                query.TenantId,
                partyId,
                cancellationToken).ConfigureAwait(false);
            degraded |= detailDegraded;
            PartyProcessingSdkReadModel model = read.Value ?? new PartyProcessingSdkReadModel();
            return Success(
                model.Records,
                "party-processing-records",
                model,
                read.ETag,
                timeProvider.GetUtcNow(),
                degraded);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogQueryFailed(query.QueryType, exception.GetType().Name);
            return QueryResult.Failure(QueryAdapterFailureReason.ActorException);
        }
    }

    public async Task<QueryResult> GetErasureStatusAsync(QueryEnvelope query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!TryValidateDetailEnvelope(query, out string partyId))
        {
            return QueryResult.Failure(QueryAdapterFailureReason.InvalidEnvelope);
        }

        try
        {
            PartyErasureStatusRecord? status = await erasureRecordStore
                .GetStatusAsync(query.TenantId, partyId, cancellationToken)
                .ConfigureAwait(false);
            IReadModelFreshness freshness = s_unknownFreshness;
            string? etag = null;
            bool degraded = false;
            if (status is null)
            {
                (ReadModelEntry<PartyDetailSdkReadModel> read, bool detailDegraded) = await ReadDetailModelAsync(
                    query.TenantId,
                    partyId,
                    cancellationToken)
                    .ConfigureAwait(false);
                degraded = detailDegraded;
                if (read.Value is not null)
                {
                    freshness = read.Value;
                    etag = read.ETag;
                }

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

            return Success(
                status,
                "party-erasure-status",
                freshness,
                etag,
                timeProvider.GetUtcNow(),
                degraded);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogQueryFailed(query.QueryType, exception.GetType().Name);
            return QueryResult.Failure(QueryAdapterFailureReason.ActorException);
        }
    }

    public async Task<QueryResult> GetErasureCertificateAsync(QueryEnvelope query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!TryValidateDetailEnvelope(query, out string partyId))
        {
            return QueryResult.Failure(QueryAdapterFailureReason.InvalidEnvelope);
        }

        try
        {
            ErasureCertificate? certificate = await erasureRecordStore
                .GetCertificateAsync(query.TenantId, partyId, cancellationToken)
                .ConfigureAwait(false);
            return Success(
                certificate,
                "party-erasure-certificate",
                s_unknownFreshness,
                etag: null,
                timeProvider.GetUtcNow());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogQueryFailed(query.QueryType, exception.GetType().Name);
            return QueryResult.Failure(QueryAdapterFailureReason.ActorException);
        }
    }

    public async Task<QueryResult> GetPartyIndexAsync(QueryEnvelope query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!TryValidateIndexEnvelope(query)
            || !PartyIndexProjectionQueryActor.TryParseListPayload(query.Payload, out PartyIndexProjectionQueryActor.ListPartiesQueryPayload payload))
        {
            return QueryResult.Failure(QueryAdapterFailureReason.InvalidEnvelope);
        }

        string cursorScope = CreateListCursorScope(query, payload, ScopePageSize(query, payload.PageSize));
        if (!TryResolvePaging(query, payload.Page, payload.PageSize, cursorScope, out int pageNumber, out int pageSize, out int offset))
        {
            return QueryResult.Failure(QueryAdapterFailureReason.InvalidCursor);
        }

        return await ReadIndexAsync(query, cancellationToken, (model, freshness) =>
        {
            IEnumerable<PartyIndexEntry> entries = (model.Entries ?? new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal)).Values;
            PagedResult<PartyIndexEntry> page = PartySearchResultsBuilder.BuildPagedList(
                entries,
                payload.Type,
                payload.Active,
                payload.CreatedAfter,
                payload.CreatedBefore,
                payload.ModifiedAfter,
                payload.ModifiedBefore,
                pageNumber,
                pageSize) with
            {
                Freshness = freshness,
            };
            return (page, CreatePagingMetadata(query, cursorScope, offset, page.PageSize, page.Items.Count, page.TotalCount));
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

        string cursorScope = CreateSearchCursorScope(query, payload, ScopePageSize(query, payload.PageSize));
        if (!TryResolvePaging(query, payload.Page, payload.PageSize, cursorScope, out int pageNumber, out int pageSize, out int offset))
        {
            return QueryResult.Failure(QueryAdapterFailureReason.InvalidCursor);
        }

        return await ReadIndexAsync(query, cancellationToken, (model, freshness) =>
        {
            IEnumerable<PartyIndexEntry> entries = (model.Entries ?? new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal)).Values;
            PagedResult<PartySearchResult> page = searchProvider.Search(
                entries,
                payload.Query,
                payload.Type,
                payload.Active,
                pageNumber,
                pageSize,
                cancellationToken) with
            {
                Freshness = freshness,
            };
            return (page, CreatePagingMetadata(query, cursorScope, offset, page.PageSize, page.Items.Count, page.TotalCount));
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
            (ReadModelEntry<PartyDetailSdkReadModel> read, bool degraded) = await ReadDetailModelAsync(
                query.TenantId,
                partyId,
                cancellationToken)
                .ConfigureAwait(false);
            if (read.Value?.Detail is not { } stored)
            {
                return QueryResult.Failure(QueryAdapterFailureReason.ActorNotFoundInfrastructure);
            }

            DateTimeOffset now = timeProvider.GetUtcNow();
            PartyDetail detail = stored with { Freshness = ToPartiesFreshness(read.Value, now, degraded) };
            return Success(detail, PartyProjectionNames.Detail, read.Value, read.ETag, now, degraded);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogQueryFailed(query.QueryType, exception.GetType().Name);
            return QueryResult.Failure(QueryAdapterFailureReason.ActorException);
        }
    }

    private async Task<QueryResult> ReadIndexAsync<TPayload>(
        QueryEnvelope query,
        CancellationToken cancellationToken,
        Func<PartyIndexSdkReadModel, ProjectionFreshnessMetadata, (TPayload Payload, QueryPagingMetadata? Paging)> createPayload)
    {
        try
        {
            string storeName = StoreName;
            ReadModelEntry<PartyIndexSdkReadModel> read;
            bool degraded = false;
            long cacheGeneration = lastKnownCache.BeginRead(PartySdkReadModelAddresses.Index(query.TenantId));
            try
            {
                read = await readModelStore
                    .GetAsync<PartyIndexSdkReadModel>(storeName, PartySdkReadModelAddresses.Index(query.TenantId), cancellationToken)
                    .ConfigureAwait(false);
                if (read.Value is null)
                {
                    lastKnownCache.EvictIndex(query.TenantId);
                }
                else if (!lastKnownCache.StoreIndexIfCurrent(query.TenantId, cacheGeneration, read.Value))
                {
                    throw new InvalidOperationException("read-model-invalidated");
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception) when (lastKnownCache.TryGetIndex(query.TenantId, out PartyIndexSdkReadModel? cached))
            {
                read = new ReadModelEntry<PartyIndexSdkReadModel>(cached, null);
                degraded = true;
            }

            if (read.Value is not { } model)
            {
                return QueryResult.Failure(QueryAdapterFailureReason.ActorNotFoundInfrastructure);
            }

            DateTimeOffset now = timeProvider.GetUtcNow();
            (TPayload payload, QueryPagingMetadata? paging) = createPayload(model, ToPartiesFreshness(model, now, degraded));
            return Success(payload, PartyProjectionNames.Index, model, read.ETag, now, degraded, paging);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogQueryFailed(query.QueryType, exception.GetType().Name);
            return QueryResult.Failure(QueryAdapterFailureReason.ActorException);
        }
    }

    private async Task<(ReadModelEntry<PartyDetailSdkReadModel> Read, bool Degraded)> ReadDetailModelAsync(
        string tenantId,
        string partyId,
        CancellationToken cancellationToken)
    {
        long cacheGeneration = lastKnownCache.BeginRead(PartySdkReadModelAddresses.Detail(tenantId, partyId));
        try
        {
            ReadModelEntry<PartyDetailSdkReadModel> read = await readModelStore.GetAsync<PartyDetailSdkReadModel>(
                StoreName,
                PartySdkReadModelAddresses.Detail(tenantId, partyId),
                cancellationToken).ConfigureAwait(false);
            if (read.Value is null)
            {
                lastKnownCache.EvictDetail(tenantId, partyId);
            }
            else if (!lastKnownCache.StoreDetailIfCurrent(tenantId, partyId, cacheGeneration, read.Value))
            {
                throw new InvalidOperationException("read-model-invalidated");
            }

            return (read, false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception) when (lastKnownCache.TryGetDetail(tenantId, partyId, out PartyDetailSdkReadModel? cached))
        {
            return (new ReadModelEntry<PartyDetailSdkReadModel>(cached, null), true);
        }
    }

    private async Task<(ReadModelEntry<PartyProcessingSdkReadModel> Read, bool Degraded)> ReadProcessingModelAsync(
        string tenantId,
        string partyId,
        CancellationToken cancellationToken)
    {
        long cacheGeneration = lastKnownCache.BeginRead(PartySdkReadModelAddresses.Processing(tenantId, partyId));
        try
        {
            ReadModelEntry<PartyProcessingSdkReadModel> read = await readModelStore.GetAsync<PartyProcessingSdkReadModel>(
                StoreName,
                PartySdkReadModelAddresses.Processing(tenantId, partyId),
                cancellationToken).ConfigureAwait(false);
            if (read.Value is null)
            {
                lastKnownCache.EvictProcessing(tenantId, partyId);
            }
            else if (!lastKnownCache.StoreProcessingIfCurrent(tenantId, partyId, cacheGeneration, read.Value))
            {
                throw new InvalidOperationException("read-model-invalidated");
            }

            return read.Value is null
                ? (new ReadModelEntry<PartyProcessingSdkReadModel>(new PartyProcessingSdkReadModel(), null), true)
                : (read, false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception) when (lastKnownCache.TryGetProcessing(tenantId, partyId, out PartyProcessingSdkReadModel? cached))
        {
            return (new ReadModelEntry<PartyProcessingSdkReadModel>(cached, null), true);
        }
        catch (Exception exception)
        {
            // Soft-fail: export and Art.30 reads stay bounded when the processing slot is unreachable
            // and no last-known entry exists (retired actor degraded to an empty record list).
            LogProcessingUnavailable(exception.GetType().Name);
            return (new ReadModelEntry<PartyProcessingSdkReadModel>(new PartyProcessingSdkReadModel(), null), true);
        }
    }

    private QueryResult Success<TPayload>(
        TPayload payload,
        string projectionType,
        IReadModelFreshness freshness,
        string? etag,
        DateTimeOffset now,
        bool degraded = false,
        QueryPagingMetadata? paging = null)
    {
        QueryResponseMetadata baseMetadata = freshness.ToQueryResponseMetadata(Thresholds, now, etag);
        QueryResponseMetadata metadata = baseMetadata with
        {
            IsStale = degraded ? true : baseMetadata.IsStale,
            IsDegraded = degraded,
            Paging = paging,
            WarningCodes = degraded
                ? [ProjectionFreshnessMetadata.WarningProjectionStateStoreUnavailable]
                : baseMetadata.WarningCodes,
            Provenance = QueryResponseProvenance.ProjectionBacked,
            Lifecycle = degraded ? ProjectionLifecycleState.Stale : baseMetadata.Lifecycle,
        };
        return QueryResult.FromPayload(JsonSerializer.SerializeToElement(payload, s_jsonOptions), projectionType, metadata);
    }

    private ProjectionFreshnessMetadata ToPartiesFreshness(IReadModelFreshness model, DateTimeOffset now, bool degraded = false)
        => degraded
            ? ProjectionFreshnessMetadata.Create(
                ProjectionFreshnessStatus.Stale,
                ProjectionFreshnessMetadata.WarningProjectionStateStoreUnavailable)
            : ReadModelFreshness.Classify(model, Thresholds, now) switch
            {
                ReadModelFreshnessState.Current or ReadModelFreshnessState.Aging =>
                    ProjectionFreshnessMetadata.Create(ProjectionFreshnessStatus.Current),
                ReadModelFreshnessState.Stale => ProjectionFreshnessMetadata.Create(ProjectionFreshnessStatus.Stale),
                _ => ProjectionFreshnessMetadata.Create(
                    ProjectionFreshnessStatus.Unavailable,
                    ProjectionFreshnessMetadata.WarningProjectionStateUnavailable),
            };

    private QueryPagingMetadata? CreatePagingMetadata(
        QueryEnvelope query,
        string cursorScope,
        int offset,
        int pageSize,
        int itemCount,
        int totalCount)
    {
        if (query.Paging is null)
        {
            return null;
        }

        int nextOffset = checked(offset + itemCount);
        bool hasMore = nextOffset < totalCount;
        string? nextCursor = hasMore
            ? cursorCodec.Encode(query.QueryType, cursorScope, nextOffset.ToString(CultureInfo.InvariantCulture))
            : null;
        return new QueryPagingMetadata(pageSize, offset, nextCursor, totalCount, hasMore);
    }

    private bool TryResolvePaging(
        QueryEnvelope query,
        int payloadPage,
        int payloadPageSize,
        string cursorScope,
        out int page,
        out int pageSize,
        out int offset)
    {
        page = payloadPage;
        pageSize = Math.Clamp(payloadPageSize, 1, 100);
        long payloadOffset = (long)(page - 1) * pageSize;
        offset = payloadOffset > int.MaxValue ? int.MaxValue : (int)payloadOffset;
        if (query.Paging is null)
        {
            return true;
        }

        pageSize = query.Paging.PageSize ?? pageSize;
        string? failureReason = null;
        if (pageSize is < 1 or > 100
            || query.Paging.Offset is < 0
            || (!string.IsNullOrWhiteSpace(query.Paging.Cursor) && query.Paging.Offset is not null)
            || !cursorCodec.TryDecode(
                query.Paging.Cursor,
                query.QueryType,
                cursorScope,
                out string? position,
                out failureReason))
        {
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                LogCursorRejected(query.QueryType, failureReason);
            }

            return false;
        }

        if (!string.IsNullOrWhiteSpace(position))
        {
            if (!int.TryParse(position, NumberStyles.None, CultureInfo.InvariantCulture, out offset) || offset < 0)
            {
                return false;
            }
        }
        else if (query.Paging.Offset is { } requestedOffset)
        {
            offset = requestedOffset;
        }

        if (offset % pageSize != 0)
        {
            return false;
        }

        long resolvedPage = ((long)offset / pageSize) + 1;
        if (resolvedPage > int.MaxValue)
        {
            return false;
        }

        page = (int)resolvedPage;
        return true;
    }

    private static int ScopePageSize(QueryEnvelope query, int payloadPageSize)
        => query.Paging?.PageSize ?? Math.Clamp(payloadPageSize, 1, 100);

    private static string CreateListCursorScope(
        QueryEnvelope query,
        PartyIndexProjectionQueryActor.ListPartiesQueryPayload payload,
        int pageSize)
        => QueryCursorScope.Create()
            .Add("tenant", query.TenantId)
            .Add("user", query.UserId)
            .Add("type", payload.Type?.ToString())
            .Add("active", payload.Active?.ToString(CultureInfo.InvariantCulture))
            .Add("created-after", payload.CreatedAfter)
            .Add("created-before", payload.CreatedBefore)
            .Add("modified-after", payload.ModifiedAfter)
            .Add("modified-before", payload.ModifiedBefore)
            .Add("page-size", pageSize.ToString(CultureInfo.InvariantCulture))
            .Build();

    private static string CreateSearchCursorScope(
        QueryEnvelope query,
        PartyIndexProjectionQueryActor.SearchPartiesQueryPayload payload,
        int pageSize)
        => QueryCursorScope.Create()
            .Add("tenant", query.TenantId)
            .Add("user", query.UserId)
            .Add("query", payload.Query.Trim())
            .Add("type", payload.Type?.ToString())
            .Add("active", payload.Active?.ToString(CultureInfo.InvariantCulture))
            .Add("mode", payload.Mode)
            .Add("case", payload.CaseId)
            .Add("page-size", pageSize.ToString(CultureInfo.InvariantCulture))
            .Build();

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
            && partyId.AsSpan().IndexOfAny(s_reservedPartyIdChars) < 0;
    }

    private static bool TryValidateIndexEnvelope(QueryEnvelope query)
    {
        string entityId = string.IsNullOrWhiteSpace(query.EntityId) ? query.AggregateId : query.EntityId;
        return ValidTenantIdRegex().IsMatch(query.TenantId ?? string.Empty)
            && string.Equals(query.AggregateId, PartyIndexProjectionQueryActor.ListAggregateId, StringComparison.Ordinal)
            && string.Equals(entityId, PartyIndexProjectionQueryActor.ListAggregateId, StringComparison.Ordinal);
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Party SDK query {QueryType} failed with {ExceptionType}.")]
    private partial void LogQueryFailed(string? queryType, string exceptionType);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Party processing read model unavailable with {ExceptionType}; returning empty degraded records.")]
    private partial void LogProcessingUnavailable(string exceptionType);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Party query cursor rejected for queryType {QueryType}: {FailureReason}.")]
    private partial void LogCursorRejected(string? queryType, string failureReason);
}
