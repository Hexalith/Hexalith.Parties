using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.Parties.Queries;

public sealed class PartyDetailQueryHandler(PartySdkQueryService queryService) : PartySdkQueryHandler(queryService)
{
    public override string QueryType => PartyDetailProjectionQueryActor.PartyDetailQueryType;

    protected override Task<QueryResult> ExecuteCoreAsync(QueryEnvelope query, CancellationToken cancellationToken)
        => QueryService.GetPartyDetailAsync(query, cancellationToken);
}
