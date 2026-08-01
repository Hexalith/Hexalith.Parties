using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.Parties.Queries;

public sealed class GetPartyQueryHandler(PartySdkQueryService queryService) : PartySdkQueryHandler(queryService)
{
    public override string QueryType => PartyDetailProjectionQueryActor.GetPartyQueryType;

    protected override Task<QueryResult> ExecuteCoreAsync(QueryEnvelope query, CancellationToken cancellationToken)
        => QueryService.GetPartyAsync(query, cancellationToken);
}
