using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.Parties.Queries;

public sealed class GetErasureCertificateQueryHandler(PartySdkQueryService queryService) : PartySdkQueryHandler(queryService)
{
    public override string QueryType => PartyDetailProjectionQueryActor.GetErasureCertificateQueryType;

    protected override Task<QueryResult> ExecuteCoreAsync(QueryEnvelope query, CancellationToken cancellationToken)
        => QueryService.GetErasureCertificateAsync(query, cancellationToken);
}
