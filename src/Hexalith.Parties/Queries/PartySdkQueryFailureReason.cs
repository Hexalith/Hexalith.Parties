using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.Parties.Queries;

/// <summary>Semantic SDK query vocabulary across the published SDK and qualified source graph.</summary>
internal static class PartySdkQueryFailureReason
{
    /// <summary>Read-model or query infrastructure is unavailable; the legacy wire value is preserved.</summary>
    internal const string ReadModelUnavailable = QueryAdapterFailureReason.ReadModelUnavailable;

    /// <summary>Query execution failed; the legacy wire value is preserved.</summary>
    internal const string QueryExecutionFailure = QueryAdapterFailureReason.QueryExecutionFailure;
}
