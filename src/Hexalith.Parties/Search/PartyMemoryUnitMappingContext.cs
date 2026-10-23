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
/// <param name="TenantId">The owning tenant.</param>
/// <param name="CaseId">The captured Memories case.</param>
/// <param name="EventType">The originating event or delivery marker.</param>
/// <param name="AggregateId">The originating aggregate, when supplied.</param>
/// <param name="CorrelationId">The optional correlation identity.</param>
/// <param name="CausationId">The optional causation identity.</param>
/// <param name="SourceService">The originating service.</param>
/// <param name="Timestamp">The captured event or delivery timestamp.</param>
/// <param name="SequenceNumber">The retained canonical application sequence, when known.</param>
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
    /// <summary>Captures the context of one canonical index application.</summary>
    /// <param name="tenantId">The owning tenant.</param>
    /// <param name="caseId">The captured Memories case.</param>
    /// <param name="eventType">The originating event or delivery marker.</param>
    /// <param name="aggregateId">The originating aggregate, when supplied.</param>
    /// <param name="correlationId">The optional correlation identity.</param>
    /// <param name="causationId">The optional causation identity.</param>
    /// <param name="timestamp">The captured event or delivery timestamp.</param>
    /// <param name="sequenceNumber">The retained canonical application sequence, when known.</param>
    /// <returns>The immutable indexing context.</returns>
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
