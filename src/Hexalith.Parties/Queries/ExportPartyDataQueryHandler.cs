using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.Parties.Queries;

public sealed class ExportPartyDataQueryHandler(PartySdkQueryService queryService) : PartySdkQueryHandler(queryService)
{
    public override string QueryType => PartyDetailProjectionQueryActor.ExportPartyDataQueryType;

    protected override Task<QueryResult> ExecuteCoreAsync(QueryEnvelope query, CancellationToken cancellationToken)
        => QueryService.ExportPartyDataAsync(query, cancellationToken);
}
