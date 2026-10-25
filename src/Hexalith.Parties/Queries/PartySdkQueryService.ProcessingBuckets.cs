using System.Globalization;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Client.Queries;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Projections.Models;

namespace Hexalith.Parties.Queries;

public sealed partial class PartySdkQueryService
{
    private async Task<QueryResult> ReadProcessingPageAsync(QueryEnvelope query, string party, PartyProcessingSdkReadModel model,
        string? etag, bool degraded, CancellationToken token)
    {
        PartyProcessingBucketValidation.ValidateCoverage(model);
        int size = query.Paging!.PageSize ?? 20;
        if (size is < 1 or > 100 || query.Paging.Offset is not null and not 0)
        {
            return QueryResult.Failure(QueryAdapterFailureReason.InvalidPage);
        }
        string scope = QueryCursorScope.Create().Add("tenant", query.TenantId).Add("party", party).Add("user", query.UserId)
                    .Add("page-size", size.ToString(CultureInfo.InvariantCulture)).Add("revision", etag)
                    .Add("generation", model.BucketGeneration).Build();
        if (string.IsNullOrEmpty(etag) || !TryDecodeCursor(query, scope, out string? position, out _))
        {
            return QueryResult.Failure(QueryAdapterFailureReason.InvalidPage);
        }
        long after = -1;
        if (!string.IsNullOrEmpty(position) && (!long.TryParse(position, NumberStyles.None, CultureInfo.InvariantCulture, out after) || after < 0))
        {
            return QueryResult.Failure(QueryAdapterFailureReason.InvalidPage);
        }
        var records = new List<ProcessingActivityRecord>(size + 1);
        if (model.BucketGeneration is null)
        {
            records.AddRange(model.Records.OrderBy(record => record.SequenceNumber).Where(record => record.SequenceNumber > after).Take(size + 1));
        }
        else if (model.FirstBucket is { } first && model.LastBucket is { } last)
        {
            long start = Math.Max(first, PartyProcessingBucket.Number(Math.Max(0, after)));
            for (long bucket = start; bucket <= last && records.Count <= size; bucket++)
            {
                var value = await ReadProcessingBucketAsync(query.TenantId, party, model, bucket, token).ConfigureAwait(false);
                records.AddRange(value.Records.Where(record => record.SequenceNumber > after).Take(size + 1 - records.Count));
            }
        }
        await VerifyProcessingRevisionAsync(query.TenantId, party, model, etag, token).ConfigureAwait(false);
        bool more = records.Count > size;
        var items = records.Take(size).ToArray();
        string? next = more ? cursorCodec.Encode(query.QueryType, CompactCursorScope(scope), items[^1].SequenceNumber.ToString(CultureInfo.InvariantCulture)) : null;
        return Success(items, "party-processing-records", model, etag, timeProvider.GetUtcNow(), degraded,
            new QueryPagingMetadata(size, NextCursor: next, TotalCount: model.BucketGeneration is null ? model.Records.Count : model.RecordCount, HasMore: more));
    }

    private async Task<IReadOnlyList<ProcessingActivityRecord>> ReadAllProcessingRecordsAsync(string tenant, string party,
        PartyProcessingSdkReadModel model, string? etag, CancellationToken token)
    {
        if (model.BucketGeneration is null)
        {
            return model.Records;
        }
        PartyProcessingBucketValidation.ValidateCoverage(model);
        var records = new List<ProcessingActivityRecord>();
        if (model.FirstBucket is { } first && model.LastBucket is { } last)
        {
            for (long bucket = first; bucket <= last; bucket++)
            {
                records.AddRange((await ReadProcessingBucketAsync(tenant, party, model, bucket, token).ConfigureAwait(false)).Records);
            }
        }
        if (records.Count != model.RecordCount)
        {
            throw new InvalidOperationException("processing-history-incomplete");
        }
        await VerifyProcessingRevisionAsync(tenant, party, model, etag, token).ConfigureAwait(false);
        return records;
    }

    private async Task<PartyProcessingBucket> ReadProcessingBucketAsync(string tenant, string party, PartyProcessingSdkReadModel model, long bucket, CancellationToken token)
    {
        var read = await readModelStore.GetAsync<PartyProcessingBucket>(StoreName,
            PartySdkReadModelAddresses.ProcessingBucket(tenant, party, model.BucketGeneration!, bucket), token).ConfigureAwait(false);
        PartyProcessingBucket value = read.Value ?? throw new InvalidOperationException("processing-bucket-unavailable");
        PartyProcessingBucketValidation.ValidateBucket(value, model, tenant, party, bucket);
        return value;
    }

    private async Task VerifyProcessingRevisionAsync(string tenant, string party, PartyProcessingSdkReadModel model, string? etag, CancellationToken token)
    {
        if (model.BucketGeneration is null && etag is null)
        {
            return;
        }
        var latest = await readModelStore.GetAsync<PartyProcessingSdkReadModel>(StoreName,
                    PartySdkReadModelAddresses.Processing(tenant, party), token).ConfigureAwait(false);
        if (etag is null || latest.ETag != etag)
        {
            throw new InvalidOperationException("processing-history-revision-changed");
        }
    }
}
