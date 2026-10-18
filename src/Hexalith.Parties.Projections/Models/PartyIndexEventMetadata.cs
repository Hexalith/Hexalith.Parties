namespace Hexalith.Parties.Projections.Models;

/// <summary>Identifies the last nonnull canonical index application without retaining its payload.</summary>
/// <param name="SequenceNumber">The aggregate sequence of the selected application.</param>
/// <param name="EventTypeName">The source event type name supplied by the delivery.</param>
/// <param name="Timestamp">The source timestamp supplied by the delivery.</param>
public sealed record PartyIndexEventMetadata(
    long SequenceNumber,
    string EventTypeName,
    DateTimeOffset Timestamp);
