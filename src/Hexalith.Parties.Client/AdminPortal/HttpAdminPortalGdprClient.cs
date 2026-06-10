using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.ValueObjects;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hexalith.Parties.Client.AdminPortal;

public sealed class HttpAdminPortalGdprClient : IAdminPortalGdprClient
{
    private const string CommandGatewayPath = "api/v1/commands";
    private const string QueryGatewayPath = "api/v1/queries";
    private const string PartyDomain = "party";

    private readonly HttpClient _httpClient;
    private readonly PartiesClientOptions _options;

    public HttpAdminPortalGdprClient(HttpClient httpClient)
        : this(httpClient, Options.Create(new PartiesClientOptions()))
    {
    }

    [ActivatorUtilitiesConstructor]
    public HttpAdminPortalGdprClient(HttpClient httpClient, IOptions<PartiesClientOptions> options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public Task<AdminPortalGdprCommandResult> RequestErasureAsync(string partyId, CancellationToken cancellationToken)
        => PostCommandAsync(
            partyId,
            new EraseParty { PartyId = partyId, TenantId = _options.Tenant },
            AdminPortalGdprRoutes.EraseParty,
            cancellationToken);

    public Task<PartyErasureStatusRecord?> GetErasureStatusAsync(string partyId, CancellationToken cancellationToken)
        => GetErasureStatusFromPartyDetailAsync(partyId, cancellationToken);

    public Task<ErasureCertificate?> GetErasureCertificateAsync(string partyId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);
        return PostNullableQueryAsync<ErasureCertificate>(
            partyId,
            queryType: "GetErasureCertificate",
            projectionType: "PartyDetail",
            projectionActorType: "PartyDetailProjectionQueryActor",
            payload: new PartyQueryPayload(partyId),
            cancellationToken);
    }

    public Task<AdminPortalGdprCommandResult> RetryErasureVerificationAsync(string partyId, CancellationToken cancellationToken)
        => PostCommandAsync(
            partyId,
            new RetryErasureVerification { PartyId = partyId, TenantId = _options.Tenant },
            AdminPortalGdprRoutes.RetryVerification,
            cancellationToken);

    public Task<AdminPortalGdprCommandResult> RestrictProcessingAsync(
        string partyId,
        string? reason,
        CancellationToken cancellationToken)
        => PostCommandAsync(
            partyId,
            new RestrictProcessing { PartyId = partyId, TenantId = _options.Tenant, Reason = reason },
            AdminPortalGdprRoutes.RestrictProcessing,
            cancellationToken);

    public Task<AdminPortalGdprCommandResult> LiftRestrictionAsync(string partyId, CancellationToken cancellationToken)
        => PostCommandAsync(
            partyId,
            new LiftRestriction { PartyId = partyId, TenantId = _options.Tenant },
            AdminPortalGdprRoutes.LiftRestriction,
            cancellationToken);

    public Task<AdminPortalGdprCommandResult> AddConsentAsync(
        string partyId,
        string channelId,
        string purpose,
        LawfulBasis lawfulBasis,
        CancellationToken cancellationToken)
        => PostCommandAsync(
            partyId,
            new RecordConsent
            {
                PartyId = partyId,
                TenantId = _options.Tenant,
                ChannelId = channelId,
                Purpose = purpose,
                LawfulBasis = lawfulBasis,
            },
            AdminPortalGdprRoutes.Consent,
            cancellationToken);

    public Task<AdminPortalGdprCommandResult> RevokeConsentAsync(string partyId, string consentId, CancellationToken cancellationToken)
        => PostCommandAsync(
            partyId,
            new RevokeConsent { PartyId = partyId, TenantId = _options.Tenant, ConsentId = consentId },
            AdminPortalGdprRoutes.ConsentById,
            cancellationToken);

    public Task<IReadOnlyList<ConsentRecord>> GetConsentAsync(string partyId, CancellationToken cancellationToken)
        => GetConsentFromPartyDetailAsync(partyId, cancellationToken);

    public async Task<AdminPortalExportDownload> ExportPartyDataAsync(string partyId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);
        PartyDataPortabilityPackage package = await PostQueryAsync<PartyDataPortabilityPackage>(
            partyId,
            queryType: "ExportPartyData",
            projectionType: "PartyDetail",
            projectionActorType: "PartyDetailProjectionQueryActor",
            payload: new PartyQueryPayload(partyId),
            cancellationToken).ConfigureAwait(false);

        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(package, HttpPartiesCommandClient.JsonOptions);
        return new AdminPortalExportDownload(
            BuildSafeExportFileName(package.PartyId, package.ExportedAt),
            "application/json",
            payload);
    }

    public async Task<IReadOnlyList<ProcessingActivityRecord>> GetProcessingRecordsAsync(string partyId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);
        ProcessingActivityRecord[] records = await PostQueryAsync<ProcessingActivityRecord[]>(
            partyId,
            queryType: "GetProcessingRecords",
            projectionType: "PartyDetail",
            projectionActorType: "PartyDetailProjectionQueryActor",
            payload: new PartyQueryPayload(partyId),
            cancellationToken).ConfigureAwait(false);

        return records;
    }

    private async Task<AdminPortalGdprCommandResult> PostCommandAsync<TCommand>(
        string aggregateId,
        TCommand command,
        string route,
        CancellationToken cancellationToken)
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
            Payload: JsonSerializer.SerializeToElement(command, HttpPartiesCommandClient.JsonOptions),
            CorrelationId: messageId,
            Extensions: null);

        using HttpResponseMessage response = await _httpClient
            .PostAsJsonAsync(CommandGatewayPath, request, HttpPartiesCommandClient.JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return await OutcomeFromErrorAsync(response, cancellationToken).ConfigureAwait(false);
        }

        string? correlationId = await ReadCorrelationIdAsync(response, cancellationToken).ConfigureAwait(false);
        return new AdminPortalGdprCommandResult(AdminPortalGdprOutcome.Accepted, correlationId ?? messageId);
    }

    private async Task<T> PostQueryAsync<T>(
        string aggregateId,
        string queryType,
        string projectionType,
        string? projectionActorType,
        object? payload,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);

        var request = new SubmitQueryRequest(
            Tenant: _options.Tenant,
            Domain: PartyDomain,
            AggregateId: aggregateId,
            QueryType: queryType,
            ProjectionType: projectionType,
            Payload: payload is null ? null : JsonSerializer.SerializeToElement(payload, HttpPartiesCommandClient.JsonOptions),
            EntityId: aggregateId,
            ProjectionActorType: projectionActorType);

        using HttpResponseMessage response = await _httpClient
            .PostAsJsonAsync(QueryGatewayPath, request, HttpPartiesCommandClient.JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            await ThrowQueryErrorAsync(response, cancellationToken).ConfigureAwait(false);
        }

        using JsonDocument doc = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (doc.RootElement.TryGetProperty("payload", out JsonElement payloadElement)
            && payloadElement.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            return payloadElement.Deserialize<T>(HttpPartiesCommandClient.JsonOptions)
                ?? throw new PartiesClientException((int)response.StatusCode, "OK", null, "Query payload was null.", null);
        }

        throw new PartiesClientException((int)response.StatusCode, "OK", null, "Response did not contain a query payload.", null);
    }

    private async Task<T?> PostNullableQueryAsync<T>(
        string aggregateId,
        string queryType,
        string projectionType,
        string? projectionActorType,
        object? payload,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);

        var request = new SubmitQueryRequest(
            Tenant: _options.Tenant,
            Domain: PartyDomain,
            AggregateId: aggregateId,
            QueryType: queryType,
            ProjectionType: projectionType,
            Payload: payload is null ? null : JsonSerializer.SerializeToElement(payload, HttpPartiesCommandClient.JsonOptions),
            EntityId: aggregateId,
            ProjectionActorType: projectionActorType);

        using HttpResponseMessage response = await _httpClient
            .PostAsJsonAsync(QueryGatewayPath, request, HttpPartiesCommandClient.JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            await ThrowQueryErrorAsync(response, cancellationToken).ConfigureAwait(false);
        }

        using JsonDocument doc = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (doc.RootElement.TryGetProperty("payload", out JsonElement payloadElement))
        {
            return payloadElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
                ? default
                : payloadElement.Deserialize<T>(HttpPartiesCommandClient.JsonOptions);
        }

        return default;
    }

    private async Task<PartyDetail> GetPartyDetailAsync(string partyId, CancellationToken cancellationToken)
        => await PostQueryAsync<PartyDetail>(
            partyId,
            queryType: "GetParty",
            projectionType: "PartyDetail",
            projectionActorType: "PartyDetailProjectionActor",
            payload: null,
            cancellationToken).ConfigureAwait(false);

    private async Task<PartyErasureStatusRecord?> GetErasureStatusFromPartyDetailAsync(
        string partyId,
        CancellationToken cancellationToken)
    {
        PartyDetail detail = await GetPartyDetailAsync(partyId, cancellationToken).ConfigureAwait(false);
        return detail.IsErased
            ? new PartyErasureStatusRecord
            {
                PartyId = detail.Id,
                TenantId = _options.Tenant,
                Status = "Erased",
                UpdatedAt = detail.ErasedAt ?? detail.LastModifiedAt,
                ErasedAt = detail.ErasedAt,
            }
            : null;
    }

    private async Task<IReadOnlyList<ConsentRecord>> GetConsentFromPartyDetailAsync(
        string partyId,
        CancellationToken cancellationToken)
    {
        PartyDetail detail = await GetPartyDetailAsync(partyId, cancellationToken).ConfigureAwait(false);
        return detail.ConsentRecords;
    }

    private static async Task<string?> ReadCorrelationIdAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength == 0)
        {
            return null;
        }

        try
        {
            using JsonDocument doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return doc.RootElement.TryGetProperty("correlationId", out JsonElement element)
                && element.ValueKind == JsonValueKind.String
                ? element.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<AdminPortalGdprCommandResult> OutcomeFromErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        string? rawDetail = await TryReadDetailAsync(response, cancellationToken).ConfigureAwait(false);
        AdminPortalGdprOutcome outcome = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => AdminPortalGdprOutcome.AuthenticationRequired,
            HttpStatusCode.Forbidden => ContainsTenant(rawDetail)
                ? AdminPortalGdprOutcome.MissingTenant
                : AdminPortalGdprOutcome.Forbidden,
            HttpStatusCode.NotFound => AdminPortalGdprOutcome.NotFound,
            HttpStatusCode.Conflict => AdminPortalGdprOutcome.ErasureInProgress,
            HttpStatusCode.Gone => AdminPortalGdprOutcome.Erased,
            HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => AdminPortalGdprOutcome.ValidationRejected,
            HttpStatusCode.NotImplemented => AdminPortalGdprOutcome.ContractUnavailable,
            HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests => AdminPortalGdprOutcome.TransientFailure,
            >= HttpStatusCode.InternalServerError => AdminPortalGdprOutcome.TransientFailure,
            _ => AdminPortalGdprOutcome.Unknown,
        };

        return new AdminPortalGdprCommandResult(outcome, null, SanitizeDetail(rawDetail));
    }

    private static async Task ThrowQueryErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        AdminPortalGdprCommandResult result = await OutcomeFromErrorAsync(response, cancellationToken).ConfigureAwait(false);
        throw new PartiesClientException(
            (int)response.StatusCode,
            result.Outcome.ToString(),
            null,
            result.Detail,
            result.CorrelationId);
    }

    private static async Task<string?> TryReadDetailAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using JsonDocument doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (doc.RootElement.TryGetProperty("detail", out JsonElement detail)
                && detail.ValueKind == JsonValueKind.String)
            {
                return detail.GetString();
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private static bool ContainsTenant(string? value)
        => value?.Contains("tenant", StringComparison.OrdinalIgnoreCase) == true;

    private static string? SanitizeDetail(string? detail)
        => string.IsNullOrWhiteSpace(detail)
            ? detail
            : "Operation details are available from the server audit trail.";

    private static string SanitizeFileToken(string value)
    {
        char[] chars = value
            .Select(static c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-')
            .ToArray();
        string sanitized = new(chars);
        return string.IsNullOrWhiteSpace(sanitized) ? "party" : sanitized;
    }

    private static string BuildSafeExportFileName(string partyId, DateTimeOffset exportedAt)
        => $"party-{SanitizeFileToken(partyId)}-{exportedAt.UtcDateTime.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture)}.json";

    private sealed record PartyQueryPayload(string PartyId);

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
