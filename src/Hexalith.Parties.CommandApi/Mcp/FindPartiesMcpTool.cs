using System.ComponentModel;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.Parties.CommandApi.Authorization;
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
        [Description("Search mode: 'hybrid' (default), 'lexical', 'semantic', or 'graph'.")] string? mode = null,
        [Description("Optional case id to scope Memories-backed rich search.")] string? caseId = null,
        [Description("Filter by party type: 'Person' or 'Organization'")] string? type = null,
        [Description("When true, only returns active parties")] bool activeOnly = true,
        [Description("Page number for pagination (starts at 1)")] int page = 1,
        [Description("Number of results per page (max 100)")] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        McpTenantAccessContext access = await McpTenantAuthorization
            .RequireAccessAsync(services, TenantAccessRequirement.Read, cancellationToken)
            .ConfigureAwait(false);
        string tenant = access.TenantId;

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

        IReadOnlyDictionary<string, PartyIndexEntry> entries = await GetPartyIndexEntriesAsync(proxy).ConfigureAwait(false);

        // Exclude erased parties from all MCP search/list results
        IReadOnlyList<PartyIndexEntry> activeEntries = [.. entries.Values.Where(e => !e.IsErased)];
        HashSet<string> authorizedPartyIds = activeEntries.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);

        bool? activeFilter = activeOnly ? true : null;

        if (string.IsNullOrWhiteSpace(query))
        {
            return JsonSerializer.Serialize(
                PartySearchResultsBuilder.BuildPagedList(activeEntries, typeFilter, activeFilter, page, pageSize),
                McpSessionContext.JsonOptions);
        }

        IPartySearchService searchService = services.GetRequiredService<IPartySearchService>();
        PartySearchResponse search = await searchService.SearchAsync(
            new PartySearchRequest(
                tenant,
                query,
                ParseSearchMode(mode),
                typeFilter,
                activeFilter,
                page,
                pageSize,
                CaseId: caseId,
                AuthorizedPartyIds: authorizedPartyIds),
            activeEntries,
            cancellationToken).ConfigureAwait(false);

        // Serialise the entire response so MCP clients receive the same metadata REST does:
        // execution status, degraded reason, score channels (lexical/semantic/graph/composite),
        // and source metadata. Without this, AI agents cannot reason about why a result ranked
        // where it did or whether the search was rich, degraded, or local-only.
        return JsonSerializer.Serialize(
            search,
            McpSessionContext.JsonOptions);
    }

    private static PartySearchMode ParseSearchMode(string? mode)
        => mode?.Trim().ToLowerInvariant() switch
        {
            "lexical" or "syntactic" => PartySearchMode.Lexical,
            "semantic" => PartySearchMode.Semantic,
            "graph" => PartySearchMode.Graph,
            _ => PartySearchMode.Hybrid,
        };

    private static async Task<IReadOnlyDictionary<string, PartyIndexEntry>> GetPartyIndexEntriesAsync(IPartyIndexProjectionActor proxy)
    {
        Task<string?>? jsonTask = null;
        try
        {
            jsonTask = proxy.GetEntriesJsonAsync();
        }
        catch (NotImplementedException)
        {
            // Older test doubles and actor implementations can still use the typed actor method.
        }

        if (jsonTask is not null)
        {
            try
            {
                string? json = await jsonTask.ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    Dictionary<string, PartyIndexEntry>? entries =
                        JsonSerializer.Deserialize<Dictionary<string, PartyIndexEntry>>(json, McpSessionContext.JsonOptions);
                    if (entries is not null)
                    {
                        return entries;
                    }
                }
            }
            catch (NotImplementedException)
            {
                // Dapr proxies typically surface "method not implemented" at await time.
            }
        }

        return await proxy.GetEntriesAsync().ConfigureAwait(false);
    }
}
