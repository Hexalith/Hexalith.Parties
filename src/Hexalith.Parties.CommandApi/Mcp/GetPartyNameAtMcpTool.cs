using System.ComponentModel;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.Parties.CommandApi.Authorization;
using Hexalith.Parties.CommandApi.Extensions;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Abstractions;
using Hexalith.Parties.Projections.Actors;

using ModelContextProtocol.Server;

namespace Hexalith.Parties.CommandApi.Mcp;

[McpServerToolType]
public static class GetPartyNameAtMcpTool
{
    [McpServerTool(Name = "get_party_name_at")]
    [Description("Returns the display name and sort name a party had at a given point in time. Useful for auditing historical name changes.")]
    public static async Task<string> GetPartyNameAtAsync(
        [Description("The unique identifier (UUID) of the party")] string partyId,
        [Description("ISO 8601 timestamp to query the name at (e.g., '2025-06-15T10:30:00Z')")] string asOf,
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        McpTenantAccessContext access = await McpTenantAuthorization
            .RequireAccessAsync(services, TenantAccessRequirement.Read, cancellationToken)
            .ConfigureAwait(false);
        string tenant = access.TenantId;

        if (!Guid.TryParse(partyId, out _))
        {
            throw new InvalidOperationException(
                "Invalid party ID format. Expected a UUID like '550e8400-e29b-41d4-a716-446655440000'.");
        }

        if (!DateTimeOffset.TryParse(asOf, out DateTimeOffset asOfTimestamp))
        {
            throw new InvalidOperationException(
                "Invalid timestamp format. Expected ISO 8601 like '2025-06-15T10:30:00Z'.");
        }

        IActorProxyFactory actorProxyFactory = services.GetRequiredService<IActorProxyFactory>();
        var actorId = new ActorId($"{tenant}:party-detail:{partyId}");
        IPartyDetailProjectionActor proxy = actorProxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
            actorId, nameof(PartyDetailProjectionActor));

        PartyDetail? detail = await proxy.ReadDetailAsync().ConfigureAwait(false);
        if (detail is null)
        {
            throw new InvalidOperationException($"Party not found. No party exists with ID '{partyId}'.");
        }

        if (detail.IsErased)
        {
            throw new InvalidOperationException(
                $"Party '{partyId}' has been erased under GDPR Article 17. No personal data is available.");
        }

        if (detail.NameHistory.Count == 0)
        {
            throw new InvalidOperationException(
                "Name history not available for this party. Trigger projection rebuild.");
        }

        NameHistoryEntry? entry = detail.NameHistory
            .Where(e => e.ChangedAt <= asOfTimestamp)
            .OrderBy(e => e.ChangedAt)
            .LastOrDefault();

        if (entry is null)
        {
            throw new InvalidOperationException(
                "Party did not exist at the requested timestamp.");
        }

        return JsonSerializer.Serialize(
            new TemporalNameResult
            {
                PartyId = partyId,
                AsOf = asOfTimestamp,
                DisplayName = entry.DisplayName,
                SortName = entry.SortName,
            },
            McpSessionContext.JsonOptions);
    }
}
