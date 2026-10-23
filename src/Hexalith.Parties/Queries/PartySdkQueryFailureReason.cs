using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.Parties.Queries;

/// <summary>Semantic SDK query vocabulary across the qualified source graph and retained package baseline.</summary>
internal static class PartySdkQueryFailureReason
{
#if HEXALITH_EVENTSTORE_SEMANTIC_QUERY_FAILURES
    /// <summary>Read-model or query infrastructure is unavailable; the legacy wire value is preserved.</summary>
    internal const string ReadModelUnavailable = QueryAdapterFailureReason.ReadModelUnavailable;

    /// <summary>Query execution failed; the legacy wire value is preserved.</summary>
    internal const string QueryExecutionFailure = QueryAdapterFailureReason.QueryExecutionFailure;
#else
    // EventStore 3.127.0 predates the semantic aliases. Keep this package lane buildable
    // until a separately published owner package qualifies removal of this bridge.
    /// <summary>Compatible package binding for an unavailable SDK read model.</summary>
    internal const string ReadModelUnavailable = QueryAdapterFailureReason.ActorNotFoundInfrastructure;

    /// <summary>Compatible package binding for an SDK query execution failure.</summary>
    internal const string QueryExecutionFailure = QueryAdapterFailureReason.ActorException;
#endif
}
