using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

using Hexalith.Commons.Http;
using Hexalith.Commons.UniqueIds;
using Hexalith.Parties.Client.Abstractions;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Models;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using SemanticId = Hexalith.Parties.Contracts.ValueObjects.PartyIdentifier;

namespace Hexalith.Parties.Client;

public sealed class HttpPartiesCommandClient : IPartiesCommandClient
{
    private const string PartyDomain = "party";
    private const string CommandGatewayPath = "api/v1/commands";

    private readonly HttpClient _httpClient;
    private readonly PartiesClientOptions _options;
    private static readonly Regex SensitiveDetailPattern = new(
        "(?i)(payload|token|authorization|bearer|secret|password|sidecar|dapr|redis|connection\\s*string|connectionstring|api[-_\\s]*key|client[-_\\s]*secret)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

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

    internal static readonly JsonSerializerOptions JsonOptions = PartiesJsonOptions.Default;

    public async Task<string> CreatePartyAsync(CreateParty command, CancellationToken ct)
        => (await CreatePartyWithResultAsync(command, ct).ConfigureAwait(false)).CorrelationId;

    public Task<PartiesCommandResult<PartyDetail>> CreatePartyWithResultAsync(CreateParty command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        return PostCommandForResultAsync(command.PartyId, command, ct);
    }

    public async Task<string> UpdatePersonDetailsAsync(string partyId, UpdatePersonDetails command, CancellationToken ct)
        => (await UpdatePersonDetailsWithResultAsync(partyId, command, ct).ConfigureAwait(false)).CorrelationId;

    public Task<PartiesCommandResult<PartyDetail>> UpdatePersonDetailsWithResultAsync(string partyId, UpdatePersonDetails command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        return PostCommandForResultAsync(partyId, command with { PartyId = partyId }, ct);
    }

    public async Task<string> UpdateOrganizationDetailsAsync(string partyId, UpdateOrganizationDetails command, CancellationToken ct)
        => (await UpdateOrganizationDetailsWithResultAsync(partyId, command, ct).ConfigureAwait(false)).CorrelationId;

    public Task<PartiesCommandResult<PartyDetail>> UpdateOrganizationDetailsWithResultAsync(string partyId, UpdateOrganizationDetails command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        return PostCommandForResultAsync(partyId, command with { PartyId = partyId }, ct);
    }

    public async Task<string> AddContactChannelAsync(string partyId, AddContactChannel command, CancellationToken ct)
        => (await AddContactChannelWithResultAsync(partyId, command, ct).ConfigureAwait(false)).CorrelationId;

    public Task<PartiesCommandResult<PartyDetail>> AddContactChannelWithResultAsync(string partyId, AddContactChannel command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        return PostCommandForResultAsync(partyId, command with { PartyId = partyId }, ct);
    }

    public async Task<string> UpdateContactChannelAsync(string partyId, UpdateContactChannel command, CancellationToken ct)
        => (await UpdateContactChannelWithResultAsync(partyId, command, ct).ConfigureAwait(false)).CorrelationId;

    public Task<PartiesCommandResult<PartyDetail>> UpdateContactChannelWithResultAsync(string partyId, UpdateContactChannel command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        return PostCommandForResultAsync(partyId, command with { PartyId = partyId }, ct);
    }

    public async Task<string> RemoveContactChannelAsync(string partyId, RemoveContactChannel command, CancellationToken ct)
        => (await RemoveContactChannelWithResultAsync(partyId, command, ct).ConfigureAwait(false)).CorrelationId;

    public Task<PartiesCommandResult<PartyDetail>> RemoveContactChannelWithResultAsync(string partyId, RemoveContactChannel command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        return PostCommandForResultAsync(partyId, command with { PartyId = partyId }, ct);
    }

    public async Task<string> AddIdentifierAsync(string partyId, AddIdentifier command, CancellationToken ct)
        => (await AddIdentifierWithResultAsync(partyId, command, ct).ConfigureAwait(false)).CorrelationId;

    public Task<PartiesCommandResult<PartyDetail>> AddIdentifierWithResultAsync(string partyId, AddIdentifier command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        return PostCommandForResultAsync(partyId, command with { PartyId = partyId }, ct);
    }

    public async Task<string> RemoveIdentifierAsync(string partyId, RemoveIdentifier command, CancellationToken ct)
        => (await RemoveIdentifierWithResultAsync(partyId, command, ct).ConfigureAwait(false)).CorrelationId;

    public Task<PartiesCommandResult<PartyDetail>> RemoveIdentifierWithResultAsync(string partyId, RemoveIdentifier command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        return PostCommandForResultAsync(partyId, command with { PartyId = partyId }, ct);
    }

    public async Task<string> DeactivatePartyAsync(string partyId, CancellationToken ct)
        => (await DeactivatePartyWithResultAsync(partyId, ct).ConfigureAwait(false)).CorrelationId;

    public Task<PartiesCommandResult<PartyDetail>> DeactivatePartyWithResultAsync(string partyId, CancellationToken ct)
        => PostCommandForResultAsync(partyId, new DeactivateParty { PartyId = partyId }, ct);

    public async Task<string> ReactivatePartyAsync(string partyId, CancellationToken ct)
        => (await ReactivatePartyWithResultAsync(partyId, ct).ConfigureAwait(false)).CorrelationId;

    public Task<PartiesCommandResult<PartyDetail>> ReactivatePartyWithResultAsync(string partyId, CancellationToken ct)
        => PostCommandForResultAsync(partyId, new ReactivateParty { PartyId = partyId }, ct);

    public async Task<string> CreatePartyCompositeAsync(CreatePartyComposite command, CancellationToken ct)
        => (await CreatePartyCompositeWithResultAsync(command, ct).ConfigureAwait(false)).CorrelationId;

    public Task<PartiesCommandResult<PartyDetail>> CreatePartyCompositeWithResultAsync(CreatePartyComposite command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        return PostCommandForResultAsync(command.PartyId, command, ct);
    }

    public async Task<string> UpdatePartyCompositeAsync(string partyId, UpdatePartyComposite command, CancellationToken ct)
        => (await UpdatePartyCompositeWithResultAsync(partyId, command, ct).ConfigureAwait(false)).CorrelationId;

    public Task<PartiesCommandResult<PartyDetail>> UpdatePartyCompositeWithResultAsync(string partyId, UpdatePartyComposite command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        return PostCommandForResultAsync(partyId, NormalizeUpdatePartyCompositePartyIds(partyId, command), ct);
    }

    public async Task<string> SetIsNaturalPersonAsync(string partyId, SetIsNaturalPerson command, CancellationToken ct)
        => (await SetIsNaturalPersonWithResultAsync(partyId, command, ct).ConfigureAwait(false)).CorrelationId;

    public Task<PartiesCommandResult<PartyDetail>> SetIsNaturalPersonWithResultAsync(string partyId, SetIsNaturalPerson command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        return PostCommandForResultAsync(partyId, command with { PartyId = partyId }, ct);
    }

    private async Task<PartiesCommandResult<PartyDetail>> PostCommandForResultAsync<TCommand>(string aggregateId, TCommand command, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
        ArgumentNullException.ThrowIfNull(command);
        ct.ThrowIfCancellationRequested();

        if (!SemanticId.IsValid(aggregateId))
        {
            throw new ArgumentException("AggregateId must be a support-safe identifier.", nameof(aggregateId));
        }

        ValidateCommandSemanticIds(aggregateId, command);

        string messageId = UniqueIdHelper.GenerateSortableUniqueStringId();
        var request = new EventStoreCommandRequest(
            MessageId: messageId,
            Tenant: GetValidatedTenant(_options),
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
                    return new PartiesCommandResult<PartyDetail>(
                        correlationId,
                        TryDeserializePartyDetail(doc.RootElement, aggregateId));
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

    private static PartyDetail? TryDeserializePartyDetail(JsonElement root, string aggregateId)
    {
        if (!root.TryGetProperty("resultPayload", out JsonElement payload)
            || payload.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        try
        {
            PartyDetail? detail = payload.Deserialize<PartyDetail>(JsonOptions);

            // Defense-in-depth: a buggy or compromised gateway could echo a payload for a different
            // party. Only trust an enriched result whose id matches the aggregate we submitted;
            // otherwise fail closed to the correlationId-only contract.
            return detail is not null && string.Equals(detail.Id, aggregateId, StringComparison.Ordinal)
                ? detail
                : null;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            // Fail closed on malformed, unsupported, or non-Parties payloads — caller keeps
            // the existing correlationId-only contract rather than throwing.
            return null;
        }
    }

    private static UpdatePartyComposite NormalizeUpdatePartyCompositePartyIds(string partyId, UpdatePartyComposite command)
    {
        ValidateCompositeList(command.AddContactChannels, nameof(UpdatePartyComposite.AddContactChannels));
        ValidateCompositeList(command.UpdateContactChannels, nameof(UpdatePartyComposite.UpdateContactChannels));
        ValidateCompositeList(command.AddIdentifiers, nameof(UpdatePartyComposite.AddIdentifiers));

        return command with
        {
            PartyId = partyId,
            AddContactChannels = command.AddContactChannels
                .Select(channel => channel is null ? null! : channel with { PartyId = partyId })
                .ToArray(),
            UpdateContactChannels = command.UpdateContactChannels
                .Select(channel => channel is null ? null! : channel with { PartyId = partyId })
                .ToArray(),
            AddIdentifiers = command.AddIdentifiers
                .Select(identifier => identifier is null ? null! : identifier with { PartyId = partyId })
                .ToArray(),
        };
    }

    private static IReadOnlyList<TItem> ValidateCompositeList<TItem>(IReadOnlyList<TItem>? items, string propertyName)
    {
        if (items is null)
        {
            throw new ArgumentException($"{propertyName} is required.", propertyName);
        }

        return items;
    }

    private static void ValidateCommandSemanticIds<TCommand>(string aggregateId, TCommand command)
    {
        switch (command)
        {
            case AddContactChannel addContact:
                ValidateChildPartyId(addContact.PartyId, aggregateId);
                ValidateSemanticId(addContact.ContactChannelId, nameof(AddContactChannel.ContactChannelId));
                break;
            case UpdateContactChannel updateContact:
                ValidateChildPartyId(updateContact.PartyId, aggregateId);
                ValidateSemanticId(updateContact.ContactChannelId, nameof(UpdateContactChannel.ContactChannelId));
                break;
            case RemoveContactChannel removeContact:
                ValidateChildPartyId(removeContact.PartyId, aggregateId);
                ValidateSemanticId(removeContact.ContactChannelId, nameof(RemoveContactChannel.ContactChannelId));
                break;
            case AddIdentifier addIdentifier:
                ValidateChildPartyId(addIdentifier.PartyId, aggregateId);
                ValidateSemanticId(addIdentifier.IdentifierId, nameof(AddIdentifier.IdentifierId));
                break;
            case RemoveIdentifier removeIdentifier:
                ValidateChildPartyId(removeIdentifier.PartyId, aggregateId);
                ValidateSemanticId(removeIdentifier.IdentifierId, nameof(RemoveIdentifier.IdentifierId));
                break;
            case CreatePartyComposite createComposite:
                ValidateCompositeContactChannels(createComposite.PartyId, createComposite.ContactChannels);
                ValidateCompositeIdentifiers(createComposite.PartyId, createComposite.Identifiers);
                break;
            case UpdatePartyComposite updateComposite:
                ValidateCompositeContactChannels(aggregateId, updateComposite.AddContactChannels);
                ValidateUpdateContactChannels(aggregateId, updateComposite.UpdateContactChannels);
                ValidateSemanticIds(updateComposite.RemoveContactChannelIds, nameof(UpdatePartyComposite.RemoveContactChannelIds));
                ValidateCompositeIdentifiers(aggregateId, updateComposite.AddIdentifiers);
                ValidateSemanticIds(updateComposite.RemoveIdentifierIds, nameof(UpdatePartyComposite.RemoveIdentifierIds));
                break;
        }
    }

    private static void ValidateCompositeContactChannels(
        string aggregateId,
        IReadOnlyList<AddContactChannel>? contactChannels)
    {
        contactChannels = ValidateCompositeList(contactChannels, nameof(CreatePartyComposite.ContactChannels));

        for (int i = 0; i < contactChannels.Count; i++)
        {
            AddContactChannel? channel = contactChannels[i];
            if (channel is null)
            {
                throw new ArgumentException("Contact channel operations are required.", nameof(contactChannels));
            }

            ValidateChildPartyId(channel.PartyId, aggregateId);
            ValidateSemanticId(channel.ContactChannelId, nameof(AddContactChannel.ContactChannelId));
        }
    }

    private static void ValidateUpdateContactChannels(
        string aggregateId,
        IReadOnlyList<UpdateContactChannel>? contactChannels)
    {
        contactChannels = ValidateCompositeList(contactChannels, nameof(UpdatePartyComposite.UpdateContactChannels));

        for (int i = 0; i < contactChannels.Count; i++)
        {
            UpdateContactChannel? channel = contactChannels[i];
            if (channel is null)
            {
                throw new ArgumentException("Contact channel operations are required.", nameof(contactChannels));
            }

            ValidateChildPartyId(channel.PartyId, aggregateId);
            ValidateSemanticId(channel.ContactChannelId, nameof(UpdateContactChannel.ContactChannelId));
        }
    }

    private static void ValidateCompositeIdentifiers(
        string aggregateId,
        IReadOnlyList<AddIdentifier>? identifiers)
    {
        identifiers = ValidateCompositeList(identifiers, nameof(CreatePartyComposite.Identifiers));

        for (int i = 0; i < identifiers.Count; i++)
        {
            AddIdentifier? identifier = identifiers[i];
            if (identifier is null)
            {
                throw new ArgumentException("Identifier operations are required.", nameof(identifiers));
            }

            ValidateChildPartyId(identifier.PartyId, aggregateId);
            ValidateSemanticId(identifier.IdentifierId, nameof(AddIdentifier.IdentifierId));
        }
    }

    private static void ValidateSemanticIds(IReadOnlyList<string>? values, string propertyName)
    {
        values = ValidateCompositeList(values, propertyName);

        for (int i = 0; i < values.Count; i++)
        {
            ValidateSemanticId(values[i], propertyName);
        }
    }

    private static void ValidateChildPartyId(string? childPartyId, string aggregateId)
    {
        ValidateSemanticId(childPartyId, "PartyId");
        if (!string.Equals(childPartyId, aggregateId, StringComparison.Ordinal))
        {
            throw new ArgumentException("Child PartyId must match AggregateId.", "PartyId");
        }
    }

    private static void ValidateSemanticId(string? value, string propertyName)
    {
        if (!SemanticId.IsValid(value))
        {
            throw new ArgumentException($"{propertyName} must be a support-safe identifier.", propertyName);
        }
    }

    internal static async Task ThrowOnErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        BoundedProblemDetails problem = await BoundedProblemDetailsReader
            .ReadAsync(response, ct)
            .ConfigureAwait(false);

        throw new PartiesClientException(
            problem.Status,
            problem.Title ?? response.ReasonPhrase ?? "Error",
            problem.Type,
            SanitizeDetail(problem.Detail),
            problem.CorrelationId);
    }

    private static string? SanitizeDetail(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
        {
            return detail;
        }

        return SensitiveDetailPattern.IsMatch(detail)
            ? "Details withheld by Parties client."
            : detail;
    }

    internal static string GetValidatedTenant(PartiesClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return string.IsNullOrWhiteSpace(options.Tenant)
            ? throw new InvalidOperationException("Parties:Tenant configuration is required.")
            : options.Tenant;
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
