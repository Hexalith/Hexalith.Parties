namespace Hexalith.Parties.Projections.Models;

/// <summary>Validates retained processing metadata and bounded buckets before reading or changing authority.</summary>
public static class PartyProcessingBucketValidation
{
    /// <summary>Requires complete contiguous retained sequence boundaries.</summary>
    /// <param name="model">The selected canonical processing metadata.</param>
    public static void ValidateCoverage(PartyProcessingSdkReadModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (model.BucketGeneration is null) return;
        bool empty = model.RecordCount == 0 && model.FirstBucket is null && model.LastBucket is null
            && model.FirstRecordSequenceNumber is null && model.LastRecordSequenceNumber is null;
        bool complete = model.RecordCount > 0 && model.FirstRecordSequenceNumber is >= 0
            && model.LastRecordSequenceNumber >= model.FirstRecordSequenceNumber
            && model.LastRecordSequenceNumber - model.FirstRecordSequenceNumber == model.RecordCount - 1
            && model.FirstBucket == PartyProcessingBucket.Number(model.FirstRecordSequenceNumber.Value)
            && model.LastBucket == PartyProcessingBucket.Number(model.LastRecordSequenceNumber!.Value);
        if (string.IsNullOrWhiteSpace(model.BucketGeneration) || model.Records is not { Count: 0 } || !empty && !complete)
            throw new InvalidOperationException("processing-history-incomplete");
    }

    /// <summary>Requires ordered, unique, scoped facts covering exactly the selected interval in one bucket.</summary>
    /// <param name="value">The stored bucket.</param>
    /// <param name="model">The selected canonical metadata.</param>
    /// <param name="tenant">The owning tenant.</param>
    /// <param name="party">The owning party.</param>
    /// <param name="bucket">The sequence partition.</param>
    public static void ValidateBucket(PartyProcessingBucket value, PartyProcessingSdkReadModel model, string tenant, string party, long bucket)
    {
        ArgumentNullException.ThrowIfNull(value);
        ValidateCoverage(model);
        if (value.Records is null || value.Records.Count > PartyProcessingBucket.Capacity
            || value.Records.Any(record => record is null || record.TenantId != tenant || record.PartyId != party
                || record.SequenceNumber < 0 || PartyProcessingBucket.Number(record.SequenceNumber) != bucket)
            || value.Records.Select(record => record.SequenceNumber).Distinct().Count() != value.Records.Count
            || !value.Records.Select(record => record.SequenceNumber).SequenceEqual(value.Records.Select(record => record.SequenceNumber).Order()))
            throw new InvalidOperationException("processing-bucket-invalid");
        if (model.FirstBucket is null || bucket < model.FirstBucket || bucket > model.LastBucket)
            throw new InvalidOperationException("processing-history-incomplete");
        long baseSequence = checked(bucket * PartyProcessingBucket.Capacity);
        long first = Math.Max(model.FirstRecordSequenceNumber!.Value, baseSequence);
        long last = Math.Min(model.LastRecordSequenceNumber!.Value, baseSequence + Math.Min(PartyProcessingBucket.Capacity - 1, long.MaxValue - baseSequence));
        if (value.Records.Count != last - first + 1 || value.Records.Count == 0
            || !value.Records.Select(record => record.SequenceNumber).SequenceEqual(Enumerable.Range(0, value.Records.Count).Select(offset => first + offset)))
            throw new InvalidOperationException("processing-history-incomplete");
    }
}
