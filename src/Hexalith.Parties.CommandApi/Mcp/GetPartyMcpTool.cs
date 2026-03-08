using System.ComponentModel;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Projections.Abstractions;
using Hexalith.Parties.Projections.Actors;

using ModelContextProtocol.Server;

namespace Hexalith.Parties.CommandApi.Mcp;

[McpServerToolType]
public static class GetPartyMcpTool
{
    [McpServerTool(Name = "get_party")]
    [Description("Retrieves the complete details of a party by its ID, including person/organization details, contact channels, identifiers, and active status.")]
    public static async Task<string> GetPartyAsync(
        [Description("The unique identifier (UUID) of the party to retrieve")] string partyId,
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        string? tenant = McpSessionContext.Tenant.Value;
        if (string.IsNullOrWhiteSpace(tenant))
        {
            throw new InvalidOperationException("Authentication required. No tenant context found in the request.");
        }

        if (!Guid.TryParse(partyId, out _))
        {
            throw new InvalidOperationException(
                "Invalid party ID format. Expected a UUID like '550e8400-e29b-41d4-a716-446655440000'.");
        }

        IActorProxyFactory actorProxyFactory = services.GetRequiredService<IActorProxyFactory>();
        var actorId = new ActorId($"{tenant}:party-detail:{partyId}");
        IPartyDetailProjectionActor proxy = actorProxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
            actorId, nameof(PartyDetailProjectionActor));

        PartyDetail? detail = await proxy.GetDetailAsync().ConfigureAwait(false);
        if (detail is null)
        {
            throw new InvalidOperationException($"Party not found. No party exists with ID '{partyId}'.");
        }

        if (detail.IsErased)
        {
            throw new InvalidOperationException(
                $"Party '{partyId}' has been erased under GDPR Article 17. No personal data is available.");
        }

        return JsonSerializer.Serialize(detail, McpSessionContext.JsonOptions);
    }
}
