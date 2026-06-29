namespace Hexalith.Parties.Projections.Services;

public sealed record PartyProjectionRebuildCheckpoint(string PartyId, long SequenceNumber);
