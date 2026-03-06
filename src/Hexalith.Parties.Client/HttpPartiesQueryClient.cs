using System.Net.Http.Json;

using Hexalith.Parties.Client.Abstractions;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Client;

public sealed class HttpPartiesQueryClient(HttpClient httpClient) : IPartiesQueryClient
{
    public async Task<PartyDetail> GetPartyAsync(string partyId, CancellationToken ct)
    {
        using HttpResponseMessage response = await httpClient
            .GetAsync($"api/v1/parties/{Uri.EscapeDataString(partyId)}", ct)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            await HttpPartiesCommandClient.ThrowOnErrorAsync(response, ct).ConfigureAwait(false);
        }

        return await response.Content
            .ReadFromJsonAsync<PartyDetail>(HttpPartiesCommandClient.JsonOptions, ct)
            .ConfigureAwait(false)
            ?? throw new PartiesClientException(200, "OK", null, "Response body was null.", null);
    }

    public async Task<PagedResult<PartyIndexEntry>> ListPartiesAsync(
        int page,
        int pageSize,
        PartyType? type,
        bool? active,
        DateTimeOffset? createdAfter,
        DateTimeOffset? createdBefore,
        DateTimeOffset? modifiedAfter,
        DateTimeOffset? modifiedBefore,
        CancellationToken ct)
    {
        var queryParams = new List<string>
        {
            $"page={page}",
            $"pageSize={pageSize}",
        };

        if (type is not null)
        {
            queryParams.Add($"type={type.Value}");
        }

        if (active is not null)
        {
            queryParams.Add($"active={active.Value.ToString().ToLowerInvariant()}");
        }

        if (createdAfter is not null)
        {
            queryParams.Add($"createdAfter={Uri.EscapeDataString(createdAfter.Value.ToString("o"))}");
        }

        if (createdBefore is not null)
        {
            queryParams.Add($"createdBefore={Uri.EscapeDataString(createdBefore.Value.ToString("o"))}");
        }

        if (modifiedAfter is not null)
        {
            queryParams.Add($"modifiedAfter={Uri.EscapeDataString(modifiedAfter.Value.ToString("o"))}");
        }

        if (modifiedBefore is not null)
        {
            queryParams.Add($"modifiedBefore={Uri.EscapeDataString(modifiedBefore.Value.ToString("o"))}");
        }

        string url = $"api/v1/parties?{string.Join('&', queryParams)}";

        using HttpResponseMessage response = await httpClient
            .GetAsync(url, ct)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            await HttpPartiesCommandClient.ThrowOnErrorAsync(response, ct).ConfigureAwait(false);
        }

        return await response.Content
            .ReadFromJsonAsync<PagedResult<PartyIndexEntry>>(HttpPartiesCommandClient.JsonOptions, ct)
            .ConfigureAwait(false)
            ?? throw new PartiesClientException(200, "OK", null, "Response body was null.", null);
    }

    public async Task<PagedResult<PartySearchResult>> SearchPartiesAsync(string query, int page, int pageSize, CancellationToken ct)
    {
        string url = $"api/v1/parties/search?q={Uri.EscapeDataString(query)}&page={page}&pageSize={pageSize}";

        using HttpResponseMessage response = await httpClient
            .GetAsync(url, ct)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            await HttpPartiesCommandClient.ThrowOnErrorAsync(response, ct).ConfigureAwait(false);
        }

        return await response.Content
            .ReadFromJsonAsync<PagedResult<PartySearchResult>>(HttpPartiesCommandClient.JsonOptions, ct)
            .ConfigureAwait(false)
            ?? throw new PartiesClientException(200, "OK", null, "Response body was null.", null);
    }
}
