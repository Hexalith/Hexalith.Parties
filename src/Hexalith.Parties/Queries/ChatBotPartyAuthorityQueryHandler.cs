using System.Text.Json;

using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.DomainService;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Queries;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.State;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Queries;

/// <summary>Checks current Party source eligibility for a requester-bound ChatBot authority query.</summary>
public sealed class ChatBotPartyAuthorityQueryHandler(
    IAuthoritativeEventStreamReader reader, TimeProvider clock) : IDomainQueryHandler
{
    public string Domain => "party";

    public string QueryType => ResolveChatBotPartyAuthority.QueryType;

    public async Task<QueryResult> ExecuteAsync(QueryEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ResolveChatBotPartyAuthority? request;
        try
        {
            request = JsonSerializer.Deserialize<ResolveChatBotPartyAuthority>(envelope.Payload, PartiesJsonOptions.Default);
        }
        catch (JsonException)
        {
            return QueryResult.Failure("Forbidden");
        }

        if (request is null || envelope.Domain != Domain || envelope.QueryType != QueryType ||
            envelope.AuthenticatedWorkloadId is not ("chatbot" or "projects") || !envelope.IsDelegated ||
            envelope.OriginalActorId != envelope.UserId || request.TenantId != envelope.TenantId ||
            request.PartyId != envelope.AggregateId || request.PrincipalId != envelope.UserId ||
            request.ActorClass is not ("human" or "service" or "ai") ||
            string.IsNullOrWhiteSpace(request.Operation) || string.IsNullOrWhiteSpace(request.Origin))
        {
            return QueryResult.Failure("Forbidden");
        }

        AuthoritativeStreamReadResult read = await reader.ReadAsync(
            new(envelope.TenantId, Domain, envelope.AggregateId), cancellationToken).ConfigureAwait(false);
        if (!read.IsAuthoritative || read.Stream is not { } source || source.Head <= 0 ||
            source.ObservedAt > clock.GetUtcNow())
        {
            return QueryResult.Failure("Forbidden");
        }

        PartyState state;
        try
        {
            state = PartyIdentitySourceFold.Fold(source);
        }
        catch (Exception exception) when (exception is InvalidOperationException or JsonException or ArgumentException)
        {
            return QueryResult.Failure("Forbidden");
        }

        bool eligible = state.HasBeenCreated && state.IsActive && !state.IsRestricted &&
            state.ErasureStatus == ErasureStatus.Active &&
            (state.Type == PartyType.Person && state.Person is not null ||
                state.Type == PartyType.Organization && state.Organization is not null);
        // The requester's own human Party must be a current Person. Referenced Parties may be organizations.
        if (request.ActorClass == "human" && request.PartyId == request.PrincipalId)
        {
            eligible &= state.Type == PartyType.Person;
        }

        var result = new ChatBotPartyAuthorityResult(request.TenantId, request.PrincipalId,
            request.PartyId, request.Operation, request.ActorClass, request.Origin, eligible,
            source.Head, source.ObservationId, source.ObservedAt);
        return QueryResult.FromPayload(JsonSerializer.SerializeToElement(result, PartiesJsonOptions.Default),
            projectionType: null, metadata: new QueryResponseMetadata { Provenance = QueryResponseProvenance.HandlerComputed });
    }
}
