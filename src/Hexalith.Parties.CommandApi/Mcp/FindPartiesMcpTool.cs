using System.ComponentModel;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.Parties.CommandApi.Search;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Abstractions;
using Hexalith.Parties.Projections.Actors;

using ModelContextProtocol.Server;

namespace Hexalith.Parties.CommandApi.Mcp;

[McpServerToolType]
public static class FindPartiesMcpTool
{
    [McpServerTool(Name = "find_parties")]
    [Description("Searches for parties by name, organization, or other criteria. Returns matching parties with match metadata for disambiguation. When called with no query, returns a paginated list of all parties.")]
    public static async Task<string> FindPartiesAsync(
        IServiceProvider services,
        [Description("Search text to match against party names, organization names, and identifiers. Leave empty to list all parties.")] string? query = null,
        [Description("Filter by party type: 'Person' or 'Organization'")] string? type = null,
        [Description("When true, only returns active parties")] bool activeOnly = true,
        [Description("Page number for pagination (starts at 1)")] int page = 1,
        [Description("Number of results per page (max 100)")] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        string? tenant = McpSessionContext.Tenant.Value;
        if (string.IsNullOrWhiteSpace(tenant))
        {
            throw new InvalidOperationException("Authentication required. No tenant context found in the request.");
        }

        if (page < 1)
        {
            page = 1;
        }

        if (pageSize < 1)
        {
            pageSize = 1;
        }
        else if (pageSize > 100)
        {
            pageSize = 100;
        }

        PartyType? typeFilter = null;
        if (!string.IsNullOrWhiteSpace(type) && Enum.TryParse<PartyType>(type, ignoreCase: true, out PartyType parsed))
        {
            typeFilter = parsed;
        }

        IActorProxyFactory actorProxyFactory = services.GetRequiredService<IActorProxyFactory>();
        var actorId = new ActorId($"{tenant}:party-index");
        IPartyIndexProjectionActor proxy = actorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
            actorId, nameof(PartyIndexProjectionActor));

        IReadOnlyDictionary<string, PartyIndexEntry> entries = await proxy.GetEntriesAsync().ConfigureAwait(false);

        // Exclude erased parties from all MCP search/list results
        IEnumerable<PartyIndexEntry> activeEntries = entries.Values.Where(e => !e.IsErased);

        bool? activeFilter = activeOnly ? true : null;

        if (string.IsNullOrWhiteSpace(query))
        {
            return JsonSerializer.Serialize(
                PartySearchResultsBuilder.BuildPagedList(activeEntries, typeFilter, activeFilter, page, pageSize),
                McpSessionContext.JsonOptions);
        }

        return JsonSerializer.Serialize(
            PartySearchResultsBuilder.BuildSearchResults(activeEntries, query, typeFilter, activeFilter, page, pageSize),
            McpSessionContext.JsonOptions);
    }
}
