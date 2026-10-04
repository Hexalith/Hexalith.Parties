using System.Text.Json;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.DomainService;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Queries;

namespace Hexalith.Parties.Queries;

/// <summary>Routes the strict authoritative identity contract through the existing gateway.</summary>
public sealed class PartyIdentityQueryHandler(PartyIdentityQueryService service) : IDomainQueryHandler
{
    /// <inheritdoc/>
    public string Domain => "party";

    /// <inheritdoc/>
    public string QueryType => typeof(ResolvePartyIdentity).FullName!;

    /// <inheritdoc/>
    public async Task<QueryResult> ExecuteAsync(QueryEnvelope query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Domain != Domain || query.QueryType != QueryType)
        {
            return QueryResult.Failure(QueryAdapterFailureReason.UnsupportedQueryType);
        }

        var result = await service.ResolveAsync(query, cancellationToken).ConfigureAwait(false);
        return QueryResult.FromPayload(JsonSerializer.SerializeToElement(result, PartiesJsonOptions.Default),
            projectionType: null, metadata: new QueryResponseMetadata { Provenance = QueryResponseProvenance.Unknown });
    }
}
