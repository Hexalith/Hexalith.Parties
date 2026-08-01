using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.Parties.Queries;

public sealed class PartyIndexQueryHandler(PartySdkQueryService queryService) : PartySdkQueryHandler(queryService)
{
    public override string QueryType => PartyIndexProjectionQueryActor.PartyIndexQueryType;

    protected override Task<QueryResult> ExecuteCoreAsync(QueryEnvelope query, CancellationToken cancellationToken)
        => QueryService.GetPartyIndexAsync(query, cancellationToken);
}
