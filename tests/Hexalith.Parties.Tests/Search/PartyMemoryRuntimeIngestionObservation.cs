namespace Hexalith.Parties.Tests.Search;

/// <summary>Content-free wire identity for actual unchanged-URI revision refresh.</summary>
/// <param name="RefreshExistingSource">Whether explicit refresh was requested.</param>
/// <param name="SourceRevisionNumber">The canonical revision number.</param>
/// <param name="SourceUri">The unchanged owned source identity.</param>
internal sealed record PartyMemoryRuntimeIngestionObservation(bool RefreshExistingSource, long SourceRevisionNumber, string? SourceUri);
