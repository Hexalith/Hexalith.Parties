namespace Hexalith.Parties.IntegrationTests.HealthChecks;

/// <summary>Identifies the writer protocol persisted by the authenticated activation API.</summary>
/// <param name="SchemaVersion">The marker schema version.</param>
/// <param name="WriterProtocolVersion">The activated writer protocol.</param>
/// <param name="CutoverCommit">The exact operator-selected source identity.</param>
internal sealed record WriterProtocolMarker(
    int SchemaVersion,
    int WriterProtocolVersion,
    string CutoverCommit);
