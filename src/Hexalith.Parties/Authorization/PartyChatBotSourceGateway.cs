using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.ServiceDefaults.Authentication;

namespace Hexalith.Parties.Authorization;

/// <summary>Reads only Parties' own source streams through the bound EventStore workload route.</summary>
internal sealed class PartyChatBotSourceGateway(
    HttpClient http, IWorkloadAssertionIssuer issuer, IConfiguration configuration, TimeProvider clock) : IEventStoreGatewayClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<StreamReadPage> ReadStreamAsync(StreamReadRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Domain != "party" || string.IsNullOrWhiteSpace(request.Tenant) ||
            configuration["Authentication:BoundStreamRead:PrivateKey"] is not { Length: > 0 } key)
        {
            throw new InvalidOperationException("Party source scope is unavailable.");
        }

        string proof;
        try
        {
            using RSA rsa = RSA.Create();
            rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(key), out _);
            if (rsa.KeySize < 2048) { throw new InvalidOperationException("Party source key is unavailable."); }
            DateTimeOffset now = clock.GetUtcNow();
            var payload = new ProofPayload(1, "parties", request.Tenant, request.Domain, request.AggregateId!,
                now.ToUnixTimeSeconds(), now.AddSeconds(30).ToUnixTimeSeconds(),
                Convert.ToHexString(RandomNumberGenerator.GetBytes(16)));
            string body = Base64Url(JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions));
            proof = $"{body}.{Base64Url(rsa.SignData(Encoding.ASCII.GetBytes(body), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))}";
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or CryptographicException)
        {
            throw new InvalidOperationException("Party source key is unavailable.", exception);
        }

        string? assertion = await issuer.IssueAsync(new WorkloadAssertionRequest("eventstore", "eventstore:stream-read"),
            cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(assertion)) { throw new InvalidOperationException("Party source assertion is unavailable."); }
        using HttpRequestMessage message = new(HttpMethod.Post, "api/v1/streams/read/workload")
        {
            Content = JsonContent.Create(request, options: JsonOptions),
        };
        message.Headers.TryAddWithoutValidation(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, assertion);
        message.Headers.TryAddWithoutValidation("X-Hexalith-Bound-Stream-Read-Proof", proof);
        message.Headers.TryAddWithoutValidation("dapr-app-id", "eventstore");
        if (Environment.GetEnvironmentVariable("DAPR_API_TOKEN") is { Length: > 0 } apiToken)
        {
            message.Headers.TryAddWithoutValidation("dapr-api-token", apiToken);
        }
        using HttpResponseMessage response = await http.SendAsync(message, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        StreamReadPage? page = await response.Content.ReadFromJsonAsync<StreamReadPage>(JsonOptions, cancellationToken).ConfigureAwait(false);
        if (page is null || page.Tenant != request.Tenant || page.Domain != request.Domain || page.AggregateId != request.AggregateId)
        {
            throw new InvalidOperationException("Party source page is mismatched.");
        }

        return page;
    }

    public Task<SubmitCommandResponse> SubmitCommandAsync(SubmitCommandRequest request, CancellationToken cancellationToken = default)
        => Task.FromException<SubmitCommandResponse>(new NotSupportedException());

    public Task<CommandStatusQueryResponse?> GetCommandStatusAsync(string messageId, CancellationToken cancellationToken = default)
        => Task.FromException<CommandStatusQueryResponse?>(new NotSupportedException());

    public Task<EventStoreQueryResult> SubmitQueryAsync(SubmitQueryRequest request, string? ifNoneMatch = null, CancellationToken cancellationToken = default)
        => Task.FromException<EventStoreQueryResult>(new NotSupportedException());

    public Task<EventStoreQueryResult<T>> SubmitQueryAsync<T>(SubmitQueryRequest request, string? ifNoneMatch = null, CancellationToken cancellationToken = default)
        => Task.FromException<EventStoreQueryResult<T>>(new NotSupportedException());

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed record ProofPayload(int Version, string Caller, string Tenant, string Domain, string AggregateId,
        long IssuedAt, long ExpiresAt, string Nonce);
}
