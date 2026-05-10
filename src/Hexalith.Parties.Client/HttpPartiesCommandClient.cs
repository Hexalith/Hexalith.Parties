using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.Parties.Client.Abstractions;
using Hexalith.Parties.Contracts.Commands;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hexalith.Parties.Client;

public sealed class HttpPartiesCommandClient : IPartiesCommandClient
{
    private const string PartyDomain = "party";
    private const string CommandGatewayPath = "api/v1/commands";

    private readonly HttpClient _httpClient;
    private readonly PartiesClientOptions _options;

    public HttpPartiesCommandClient(HttpClient httpClient)
        : this(httpClient, Options.Create(new PartiesClientOptions()))
    {
    }

    [ActivatorUtilitiesConstructor]
    public HttpPartiesCommandClient(HttpClient httpClient, IOptions<PartiesClientOptions> options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public Task<string> CreatePartyAsync(CreateParty command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        return PostCommandAsync(command.PartyId, command, ct);
    }

    public Task<string> UpdatePersonDetailsAsync(string partyId, UpdatePersonDetails command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        return PostCommandAsync(partyId, command with { PartyId = partyId }, ct);
    }

    public Task<string> UpdateOrganizationDetailsAsync(string partyId, UpdateOrganizationDetails command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        return PostCommandAsync(partyId, command with { PartyId = partyId }, ct);
    }

    public Task<string> AddContactChannelAsync(string partyId, AddContactChannel command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        return PostCommandAsync(partyId, command with { PartyId = partyId }, ct);
    }

    public Task<string> UpdateContactChannelAsync(string partyId, UpdateContactChannel command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        return PostCommandAsync(partyId, command with { PartyId = partyId }, ct);
    }

    public Task<string> RemoveContactChannelAsync(string partyId, RemoveContactChannel command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        return PostCommandAsync(partyId, command with { PartyId = partyId }, ct);
    }

    public Task<string> AddIdentifierAsync(string partyId, AddIdentifier command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        return PostCommandAsync(partyId, command with { PartyId = partyId }, ct);
    }

    public Task<string> RemoveIdentifierAsync(string partyId, RemoveIdentifier command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        return PostCommandAsync(partyId, command with { PartyId = partyId }, ct);
    }

    public Task<string> DeactivatePartyAsync(string partyId, CancellationToken ct)
        => PostCommandAsync(partyId, new DeactivateParty { PartyId = partyId }, ct);

    public Task<string> ReactivatePartyAsync(string partyId, CancellationToken ct)
        => PostCommandAsync(partyId, new ReactivateParty { PartyId = partyId }, ct);

    public Task<string> CreatePartyCompositeAsync(CreatePartyComposite command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        return PostCommandAsync(command.PartyId, command, ct);
    }

    public Task<string> UpdatePartyCompositeAsync(string partyId, UpdatePartyComposite command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        return PostCommandAsync(partyId, command with { PartyId = partyId }, ct);
    }

    public Task<string> SetIsNaturalPersonAsync(string partyId, SetIsNaturalPerson command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        return PostCommandAsync(partyId, command with { PartyId = partyId }, ct);
    }

    private async Task<string> PostCommandAsync<TCommand>(string aggregateId, TCommand command, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
        ArgumentNullException.ThrowIfNull(command);

        string messageId = Guid.NewGuid().ToString("N");
        var request = new EventStoreCommandRequest(
            MessageId: messageId,
            Tenant: _options.Tenant,
            Domain: PartyDomain,
            AggregateId: aggregateId,
            CommandType: typeof(TCommand).FullName ?? typeof(TCommand).Name,
            Payload: JsonSerializer.SerializeToElement(command, JsonOptions),
            CorrelationId: messageId,
            Extensions: null);

        using HttpResponseMessage response = await _httpClient
            .PostAsJsonAsync(CommandGatewayPath, request, JsonOptions, ct)
            .ConfigureAwait(false);

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

                if (root.TryGetProperty("status", out JsonElement statusElement)
                    && statusElement.TryGetInt32(out int problemStatus))
                {
                    status = problemStatus;
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
                    detail = SanitizeDetail(detailElement.GetString());
                }

                if (root.TryGetProperty("correlationId", out JsonElement corrElement))
                {
                    correlationId = corrElement.GetString();
                }
            }
            catch (JsonException)
            {
                // If we can't parse the body, use HTTP status info only.
            }
        }

        throw new PartiesClientException(
            status,
            title ?? response.ReasonPhrase ?? "Error",
            type,
            detail,
            correlationId);
    }

    private static string? SanitizeDetail(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
        {
            return detail;
        }

        string[] sensitiveMarkers =
        [
            "payload",
            "token",
            "authorization",
            "bearer",
            "secret",
            "password",
            "sidecar",
            "dapr",
            "redis",
            "connection string",
        ];

        return sensitiveMarkers.Any(marker => detail.Contains(marker, StringComparison.OrdinalIgnoreCase))
            ? "Details withheld by Parties client."
            : detail;
    }

    private sealed record EventStoreCommandRequest(
        string MessageId,
        string Tenant,
        string Domain,
        string AggregateId,
        string CommandType,
        JsonElement Payload,
        string? CorrelationId,
        Dictionary<string, string>? Extensions);
}
