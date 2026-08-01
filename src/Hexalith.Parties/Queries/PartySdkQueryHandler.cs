using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.DomainService;

namespace Hexalith.Parties.Queries;

/// <summary>Routes one query discriminator to a direct canonical read-model operation.</summary>
public abstract class PartySdkQueryHandler(PartySdkQueryService queryService) : IDomainQueryHandler
{
    public string Domain => PartyDetailProjectionQueryActor.PartyDomain;

    public abstract string QueryType { get; }

    public Task<QueryResult> ExecuteAsync(QueryEnvelope query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        return !string.Equals(query.Domain, Domain, StringComparison.Ordinal)
            || !string.Equals(query.QueryType, QueryType, StringComparison.Ordinal)
                ? Task.FromResult(QueryResult.Failure(QueryAdapterFailureReason.UnsupportedQueryType))
                : ExecuteCoreAsync(query, cancellationToken);
    }

    protected PartySdkQueryService QueryService { get; } = queryService;

    protected abstract Task<QueryResult> ExecuteCoreAsync(QueryEnvelope query, CancellationToken cancellationToken);
}
