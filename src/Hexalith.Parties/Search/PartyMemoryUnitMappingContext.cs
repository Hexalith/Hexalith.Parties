namespace Hexalith.Parties.Search;

/// <summary>
/// Context attached to every memory unit indexed for a party. <see cref="EventType"/> is
/// required (P15) — callers must thread the originating envelope's <c>EventTypeName</c>
/// through so AC1's "useful event context" metadata is never replaced by a generic marker.
/// <see cref="SequenceNumber"/> is the retained canonical application checkpoint, so distinct
/// events sharing a timestamp still have distinct durable ingestion identities.
/// Parameter order on the record matches <see cref="ForProjection"/> so positional argument
/// usage cannot silently miswire fields between the two signatures (P38).
/// </summary>
internal sealed record PartyMemoryUnitMappingContext(
    string TenantId,
    string CaseId,
    string EventType,
    string? AggregateId = null,
    string? CorrelationId = null,
    string? CausationId = null,
    string SourceService = "Hexalith.Parties",
    DateTimeOffset? Timestamp = null,
    long? SequenceNumber = null)
{
    public static PartyMemoryUnitMappingContext ForProjection(
        string tenantId,
        string caseId,
        string eventType,
        string? aggregateId = null,
        string? correlationId = null,
        string? causationId = null,
        DateTimeOffset? timestamp = null,
        long? sequenceNumber = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        return new(
            tenantId,
            caseId,
            eventType,
            aggregateId,
            correlationId,
            causationId,
            Timestamp: timestamp,
            SequenceNumber: sequenceNumber);
    }
}
