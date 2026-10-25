using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Queries;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Search;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Configuration;
using Hexalith.Parties.Projections.Handlers;
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

    [GeneratedRegex(@"\A[A-Za-z0-9_\-\.]{1,128}\z", RegexOptions.CultureInvariant)]
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
                return QueryResult.Failure(PartySdkQueryFailureReason.ReadModelUnavailable);
            }

            (ReadModelEntry<PartyProcessingSdkReadModel> processing, bool processingDegraded) = await ReadProcessingModelAsync(
                query.TenantId,
                partyId,
                cancellationToken).ConfigureAwait(false);
            degraded = degraded || processingDegraded;
            DateTimeOffset now = timeProvider.GetUtcNow();
            ProjectionFreshnessMetadata freshness = ToPartiesFreshness(read.Value, now, degraded);
            PartyDetail detail = stored with { Freshness = freshness };
            IReadOnlyList<ProcessingActivityRecord> records = await ReadAllProcessingRecordsAsync(query.TenantId, partyId,
                processing.Value ?? new PartyProcessingSdkReadModel(), processing.ETag, cancellationToken).ConfigureAwait(false);
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
                PartyDetailQueryTypes.DataPortabilityProjectionType,
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
            return QueryResult.Failure(PartySdkQueryFailureReason.QueryExecutionFailure);
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
                return QueryResult.Failure(PartySdkQueryFailureReason.ReadModelUnavailable);
            }

            (ReadModelEntry<PartyProcessingSdkReadModel> read, bool degraded) = await ReadProcessingModelAsync(
                query.TenantId,
                partyId,
                cancellationToken).ConfigureAwait(false);
            degraded |= detailDegraded;
            PartyProcessingSdkReadModel model = read.Value ?? new PartyProcessingSdkReadModel();
            if (query.Paging is not null)
                return await ReadProcessingPageAsync(query, partyId, model, read.ETag, degraded, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<ProcessingActivityRecord> records = await ReadAllProcessingRecordsAsync(query.TenantId, partyId, model, read.ETag, cancellationToken).ConfigureAwait(false);
            return Success(
                records,
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
            return QueryResult.Failure(PartySdkQueryFailureReason.QueryExecutionFailure);
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
            // Legacy side records were written before append and cannot certify a lifecycle.
            // The aggregate-owned SDK projection folds only committed events.
            (ReadModelEntry<PartyDetailSdkReadModel> read, bool degraded) = await ReadDetailModelAsync(
                query.TenantId, partyId, cancellationToken).ConfigureAwait(false);
            PartyDetailSdkReadModel? model = read.Value;
            // Privacy tombstones may be installed by cleanup before append. Legacy rows
            // without committed lifecycle evidence remain unknown until event replay backfills it.
            PartyErasureStatusRecord? status = model?.ErasureLifecycle;

            return Success(
                status,
                "party-erasure-status",
                model ?? s_unknownFreshness,
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
            return QueryResult.Failure(PartySdkQueryFailureReason.QueryExecutionFailure);
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
            return QueryResult.Failure(PartySdkQueryFailureReason.QueryExecutionFailure);
        }
    }

    public async Task<QueryResult> GetPartyIndexAsync(QueryEnvelope query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!TryValidateIndexEnvelope(query)
            || !PartyIndexQueryTypes.TryParseListPayload(query.Payload, out ListPartiesQueryPayload payload, query.Paging is not null))
        {
            return QueryResult.Failure(QueryAdapterFailureReason.InvalidEnvelope);
        }

        string cursorScope = CreateListCursorScope(query, payload, ScopePageSize(query, payload.PageSize));
        if (!TryResolvePaging(query, payload.Page, payload.PageSize, cursorScope, out int pageNumber, out int pageSize, out int offset, out string failureReason))
        {
            return QueryResult.Failure(failureReason);
        }

        return await ReadIndexAsync(query, cancellationToken, (model, freshness) =>
        {
            IEnumerable<PartyIndexEntry> entries = model.Entries.Values;
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
            || !PartyIndexQueryTypes.TryParseSearchPayload(query.Payload, out SearchPartiesQueryPayload payload, query.Paging is not null))
        {
            return QueryResult.Failure(QueryAdapterFailureReason.InvalidEnvelope);
        }

        if (PartyIndexQueryTypes.IsUnsupportedSearchMode(payload.Mode) || payload.CaseId is not null)
        {
            return QueryResult.Failure(QueryAdapterFailureReason.UnsupportedQueryType);
        }

        string cursorScope = CreateSearchCursorScope(query, payload, ScopePageSize(query, payload.PageSize));
        if (!TryResolvePaging(query, payload.Page, payload.PageSize, cursorScope, out int pageNumber, out int pageSize, out int offset, out string failureReason))
        {
            return QueryResult.Failure(failureReason);
        }

        return await ReadIndexAsync(query, cancellationToken, (model, freshness) =>
        {
            IEnumerable<PartyIndexEntry> entries = model.Entries.Values
                .Where(static entry => !entry.IsErased);
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
            if (read.Value?.Detail is not { IsErased: false } stored)
            {
                return QueryResult.Failure(PartySdkQueryFailureReason.ReadModelUnavailable);
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
            return QueryResult.Failure(PartySdkQueryFailureReason.QueryExecutionFailure);
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
                if (read.Value is not null
                    && read.Value is not { Entries: not null, LastIndexedEvents: not null, LastSequenceNumbers: not null, ErasureSequenceNumbers: not null })
                {
                    // A successful read of corrupt canonical data cannot use an older empty
                    // cache to conceal the need for a rebuild or cache the malformed row.
                    lastKnownCache.EvictIndex(query.TenantId);
                    return QueryResult.Failure(PartySdkQueryFailureReason.QueryExecutionFailure);
                }

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
            catch (Exception)
            {
                if (!lastKnownCache.TryGetIndex(query.TenantId, out PartyIndexSdkReadModel? cached))
                {
                    throw;
                }

                // Another instance may have erased any indexed party while this host was offline.
                // Only an already-empty index is safe without a successful canonical read.
                if (cached is not { Entries.Count: 0, LastIndexedEvents: not null, LastSequenceNumbers: not null, ErasureSequenceNumbers: not null })
                {
                    lastKnownCache.EvictIndex(query.TenantId);
                    throw;
                }

                read = new ReadModelEntry<PartyIndexSdkReadModel>(cached, null);
                degraded = true;
            }
            finally
            {
                lastKnownCache.EndRead(cacheGeneration);
            }

            if (read.Value is not { } model)
            {
                return QueryResult.Failure(PartySdkQueryFailureReason.ReadModelUnavailable);
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
            return QueryResult.Failure(PartySdkQueryFailureReason.QueryExecutionFailure);
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
            if (read.Value?.Detail is { IsErased: true } erasedDetail)
            {
                read = new ReadModelEntry<PartyDetailSdkReadModel>(read.Value with
                {
                    Detail = PartyDetailProjectionHandler.ApplyErasure(partyId, erasedDetail)! with
                    {
                        ErasedAt = erasedDetail.ErasedAt,
                        LastModifiedAt = erasedDetail.LastModifiedAt,
                    },
                }, read.ETag);
            }

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
        catch (Exception)
        {
            if (!lastKnownCache.TryGetDetail(tenantId, partyId, out PartyDetailSdkReadModel? cached))
            {
                throw;
            }

            // A host-local cache cannot know whether another instance has erased this party.
            // An outage must not authorize personal data or manufacture an erased response.
            if (cached!.Detail is { IsErased: false })
            {
                lastKnownCache.EvictDetail(tenantId, partyId);
                throw;
            }

            if (cached.Detail is { } erased)
            {
                cached = cached with
                {
                    Detail = PartyDetailProjectionHandler.ApplyErasure(partyId, erased)! with
                    {
                        ErasedAt = erased.ErasedAt,
                        LastModifiedAt = erased.LastModifiedAt,
                    },
                };
            }

            return (new ReadModelEntry<PartyDetailSdkReadModel>(cached, null), true);
        }
        finally
        {
            lastKnownCache.EndRead(cacheGeneration);
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
        finally
        {
            lastKnownCache.EndRead(cacheGeneration);
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
        // Keep bounded cursors readable by older instances during rolling upgrades. Compact
        // only scopes whose protected JSON exceeds the SDK's accepted cursor size.
        if (nextCursor?.Length > QueryPolicyLimits.MaxCursorLength)
        {
            nextCursor = cursorCodec.Encode(query.QueryType, CompactCursorScope(cursorScope), nextOffset.ToString(CultureInfo.InvariantCulture));
        }

        return new QueryPagingMetadata(pageSize, offset, nextCursor, totalCount, hasMore);
    }

    private bool TryResolvePaging(
        QueryEnvelope query,
        int payloadPage,
        int payloadPageSize,
        string cursorScope,
        out int page,
        out int pageSize,
        out int offset,
        out string failureReason)
    {
        failureReason = QueryAdapterFailureReason.InvalidCursor;
        page = payloadPage;
        pageSize = Math.Clamp(payloadPageSize, 1, 100);
        long payloadOffset = (long)(page - 1) * pageSize;
        offset = payloadOffset > int.MaxValue ? int.MaxValue : (int)payloadOffset;
        if (query.Paging is null)
        {
            return true;
        }

        pageSize = query.Paging.PageSize ?? 20;
        offset = 0;
        if (pageSize is < 1 or > 100)
        {
            failureReason = QueryAdapterFailureReason.InvalidPage;
            return false;
        }

        string? cursorFailureReason = null;
        if (query.Paging.Offset is < 0
            || (!string.IsNullOrWhiteSpace(query.Paging.Cursor) && query.Paging.Offset is not null)
            || !TryDecodeCursor(query, cursorScope, out string? position, out cursorFailureReason))
        {
            if (!string.IsNullOrWhiteSpace(cursorFailureReason))
            {
                LogCursorRejected(query.QueryType, cursorFailureReason);
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

    private bool TryDecodeCursor(
        QueryEnvelope query,
        string cursorScope,
        out string? position,
        out string? failureReason)
    {
        if (cursorCodec.TryDecode(query.Paging!.Cursor, query.QueryType, cursorScope, out position, out failureReason))
        {
            return true;
        }

        return string.Equals(failureReason, "wrong-scope", StringComparison.Ordinal)
            && cursorCodec.TryDecode(query.Paging.Cursor, query.QueryType, CompactCursorScope(cursorScope), out position, out failureReason);
    }

    private static string CompactCursorScope(string scope)
        => "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(scope)));

    private static int ScopePageSize(QueryEnvelope query, int payloadPageSize)
        => query.Paging is null ? Math.Clamp(payloadPageSize, 1, 100) : query.Paging.PageSize ?? 20;

    private static string CreateListCursorScope(
        QueryEnvelope query,
        ListPartiesQueryPayload payload,
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
        SearchPartiesQueryPayload payload,
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
            && PartyIdentifier.IsValid(partyId)
            && string.Equals(partyId, query.AggregateId, StringComparison.Ordinal);
    }

    private static bool TryValidateIndexEnvelope(QueryEnvelope query)
    {
        string entityId = string.IsNullOrWhiteSpace(query.EntityId) ? query.AggregateId : query.EntityId;
        return ValidTenantIdRegex().IsMatch(query.TenantId ?? string.Empty)
            && string.Equals(query.AggregateId, PartyIndexQueryTypes.ListAggregateId, StringComparison.Ordinal)
            && string.Equals(entityId, PartyIndexQueryTypes.ListAggregateId, StringComparison.Ordinal);
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
