using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.Parties.Client.Abstractions;
using Hexalith.Parties.Contracts.Commands;

namespace Hexalith.Parties.Client;

public sealed class HttpPartiesCommandClient(HttpClient httpClient) : IPartiesCommandClient
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public Task<string> CreatePartyAsync(CreateParty command, CancellationToken ct)
        => PostCommandAsync("api/v1/parties", command, ct);

    public Task<string> UpdatePersonDetailsAsync(string partyId, UpdatePersonDetails command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        return PostCommandAsync(
            $"api/v1/parties/{Uri.EscapeDataString(partyId)}/update-person-details",
            command with { PartyId = partyId },
            ct);
    }

    public Task<string> UpdateOrganizationDetailsAsync(string partyId, UpdateOrganizationDetails command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        return PostCommandAsync(
            $"api/v1/parties/{Uri.EscapeDataString(partyId)}/update-organization-details",
            command with { PartyId = partyId },
            ct);
    }

    public Task<string> AddContactChannelAsync(string partyId, AddContactChannel command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        return PostCommandAsync(
            $"api/v1/parties/{Uri.EscapeDataString(partyId)}/add-contact-channel",
            command with { PartyId = partyId },
            ct);
    }

    public Task<string> UpdateContactChannelAsync(string partyId, UpdateContactChannel command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        return PostCommandAsync(
            $"api/v1/parties/{Uri.EscapeDataString(partyId)}/update-contact-channel",
            command with { PartyId = partyId },
            ct);
    }

    public Task<string> RemoveContactChannelAsync(string partyId, RemoveContactChannel command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        return PostCommandAsync(
            $"api/v1/parties/{Uri.EscapeDataString(partyId)}/remove-contact-channel",
            command with { PartyId = partyId },
            ct);
    }

    public Task<string> AddIdentifierAsync(string partyId, AddIdentifier command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        return PostCommandAsync(
            $"api/v1/parties/{Uri.EscapeDataString(partyId)}/add-identifier",
            command with { PartyId = partyId },
            ct);
    }

    public Task<string> RemoveIdentifierAsync(string partyId, RemoveIdentifier command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        return PostCommandAsync(
            $"api/v1/parties/{Uri.EscapeDataString(partyId)}/remove-identifier",
            command with { PartyId = partyId },
            ct);
    }

    public Task<string> DeactivatePartyAsync(string partyId, CancellationToken ct)
        => PostCommandAsync($"api/v1/parties/{Uri.EscapeDataString(partyId)}/deactivate", (object?)null, ct);

    public Task<string> ReactivatePartyAsync(string partyId, CancellationToken ct)
        => PostCommandAsync($"api/v1/parties/{Uri.EscapeDataString(partyId)}/reactivate", (object?)null, ct);

    public Task<string> CreatePartyCompositeAsync(CreatePartyComposite command, CancellationToken ct)
        => PostCommandAsync("api/v1/parties/create-composite", command, ct);

    public Task<string> UpdatePartyCompositeAsync(string partyId, UpdatePartyComposite command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        return PostCommandAsync(
            $"api/v1/parties/{Uri.EscapeDataString(partyId)}/update-composite",
            command with { PartyId = partyId },
            ct);
    }

    public Task<string> SetIsNaturalPersonAsync(string partyId, SetIsNaturalPerson command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        return PostCommandAsync(
            $"api/v1/parties/{Uri.EscapeDataString(partyId)}/set-natural-person",
            command with { PartyId = partyId },
            ct);
    }

    private async Task<string> PostCommandAsync<TCommand>(string requestUri, TCommand command, CancellationToken ct)
    {
        using HttpResponseMessage response = command is null
            ? await httpClient.PostAsync(requestUri, null, ct).ConfigureAwait(false)
            : await httpClient.PostAsJsonAsync(requestUri, command, JsonOptions, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            await ThrowOnErrorAsync(response, ct).ConfigureAwait(false);
        }

        try
        {
            using JsonDocument doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false),
                cancellationToken: ct).ConfigureAwait(false);

            if (doc.RootElement.TryGetProperty("correlationId", out JsonElement correlationIdElement)
                && correlationIdElement.ValueKind == JsonValueKind.String)
            {
                string? correlationId = correlationIdElement.GetString();
                if (!string.IsNullOrWhiteSpace(correlationId))
                {
                    return correlationId;
                }
            }
        }
        catch (JsonException)
        {
            // Convert malformed success bodies into a typed client exception.
        }

        throw new PartiesClientException(
            (int)response.StatusCode,
            response.ReasonPhrase ?? "Accepted",
            null,
            "Response did not contain a valid correlationId.",
            null);
    }

    internal static async Task ThrowOnErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        string? correlationId = null;
        string? title = null;
        string? type = null;
        string? detail = null;
        int status = (int)response.StatusCode;

        string contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        if (contentType.Contains("problem+json", StringComparison.OrdinalIgnoreCase)
            || contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using JsonDocument doc = await JsonDocument.ParseAsync(
                    await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false),
                    cancellationToken: ct).ConfigureAwait(false);

                JsonElement root = doc.RootElement;

                if (root.TryGetProperty("status", out JsonElement statusElement))
                {
                    status = statusElement.GetInt32();
                }

                if (root.TryGetProperty("title", out JsonElement titleElement))
                {
                    title = titleElement.GetString();
                }

                if (root.TryGetProperty("type", out JsonElement typeElement))
                {
                    type = typeElement.GetString();
                }

                if (root.TryGetProperty("detail", out JsonElement detailElement))
                {
                    detail = detailElement.GetString();
                }

                if (root.TryGetProperty("correlationId", out JsonElement corrElement))
                {
                    correlationId = corrElement.GetString();
                }
            }
            catch (JsonException)
            {
                // If we can't parse the body, use HTTP status info only
            }
        }

        throw new PartiesClientException(
            status,
            title ?? response.ReasonPhrase ?? "Error",
            type,
            detail,
            correlationId);
    }
}
