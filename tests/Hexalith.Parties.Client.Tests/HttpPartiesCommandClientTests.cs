using System.Net;
using System.Text;
using System.Text.Json;

using Hexalith.Parties.Client.Abstractions;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;

using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.Parties.Client.Tests;

public sealed class HttpPartiesCommandClientTests
{
    [Fact]
    public async Task CreatePartyAsync_SubmitsEventStoreCommandAndReturnsCorrelationIdAsync()
    {
        const string expectedCorrelationId = "corr-123";
        (HttpPartiesCommandClient client, MockHandler handler) = CreateClient(expectedCorrelationId);

        var command = new CreateParty
        {
            PartyId = "p-1",
            Type = PartyType.Person,
            PersonDetails = new PersonDetails { FirstName = "Ada", LastName = "Lovelace" },
        };

        string result = await client.CreatePartyAsync(command, CancellationToken.None);

        result.ShouldBe(expectedCorrelationId);
        handler.LastRequest!.Method.ShouldBe(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.ShouldBe("/api/v1/commands");

        using JsonDocument body = JsonDocument.Parse(handler.LastRequestBody!);
        JsonElement root = body.RootElement;
        root.GetProperty("tenant").GetString().ShouldBe("tenant-a");
        root.GetProperty("domain").GetString().ShouldBe("party");
        root.GetProperty("aggregateId").GetString().ShouldBe("p-1");
        root.GetProperty("commandType").GetString().ShouldBe(typeof(CreateParty).FullName);
        root.GetProperty("messageId").GetString().ShouldNotBeNullOrWhiteSpace();
        root.GetProperty("correlationId").GetString().ShouldBe(root.GetProperty("messageId").GetString());

        JsonElement payload = root.GetProperty("payload");
        payload.GetProperty("partyId").GetString().ShouldBe("p-1");
        payload.GetProperty("type").GetString().ShouldBe("Person");
        payload.GetProperty("personDetails").GetProperty("firstName").GetString().ShouldBe("Ada");
    }

    [Fact]
    public async Task CreatePartyWithResultAsync_WhenPayloadIsPresent_ReturnsUpdatedPartyDetailAsync()
    {
        string responseBody = JsonSerializer.Serialize(new
        {
            correlationId = "corr-with-payload",
            resultPayload = new
            {
                id = "p-1",
                type = "Person",
                isActive = true,
                displayName = "Ada Lovelace",
                sortName = "Lovelace, Ada",
            },
        });
        var handler = new MockHandler(HttpStatusCode.Accepted, responseBody, "application/json");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };
        var client = new HttpPartiesCommandClient(httpClient, Options.Create(ClientOptions()));

        PartiesCommandResult<PartyDetail> result = await client.CreatePartyWithResultAsync(
            new CreateParty
            {
                PartyId = "p-1",
                Type = PartyType.Person,
                PersonDetails = new PersonDetails { FirstName = "Ada", LastName = "Lovelace" },
            },
            CancellationToken.None);

        result.CorrelationId.ShouldBe("corr-with-payload");
        result.Payload.ShouldNotBeNull();
        result.Payload.Id.ShouldBe("p-1");
        result.Payload.Type.ShouldBe(PartyType.Person);
        result.Payload.DisplayName.ShouldBe("Ada Lovelace");
    }

    [Fact]
    public async Task CreatePartyWithResultAsync_WhenPayloadIsMalformed_FailsClosedAndKeepsCorrelationAsync()
    {
        string responseBody = JsonSerializer.Serialize(new
        {
            correlationId = "corr-bad-payload",
            resultPayload = "not-a-party-detail",
        });
        var handler = new MockHandler(HttpStatusCode.Accepted, responseBody, "application/json");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };
        var client = new HttpPartiesCommandClient(httpClient, Options.Create(ClientOptions()));

        PartiesCommandResult<PartyDetail> result = await client.CreatePartyWithResultAsync(
            new CreateParty
            {
                PartyId = "p-1",
                Type = PartyType.Person,
                PersonDetails = new PersonDetails { FirstName = "Ada", LastName = "Lovelace" },
            },
            CancellationToken.None);

        result.CorrelationId.ShouldBe("corr-bad-payload");
        result.Payload.ShouldBeNull();
    }

    [Fact]
    public async Task UpdatePersonDetailsAsync_ReplacesBodyPartyIdWithRoutePartyIdInEventStorePayloadAsync()
    {
        (HttpPartiesCommandClient client, MockHandler handler) = CreateClient("corr-456");

        var command = new UpdatePersonDetails
        {
            PartyId = "stale-id",
            PersonDetails = new PersonDetails { FirstName = "Jane", LastName = "Doe" },
        };

        await client.UpdatePersonDetailsAsync("p-1", command, CancellationToken.None);

        using JsonDocument body = JsonDocument.Parse(handler.LastRequestBody!);
        JsonElement root = body.RootElement;
        root.GetProperty("aggregateId").GetString().ShouldBe("p-1");
        root.GetProperty("commandType").GetString().ShouldBe(typeof(UpdatePersonDetails).FullName);
        root.GetProperty("payload").GetProperty("partyId").GetString().ShouldBe("p-1");
    }

    [Fact]
    public async Task DeactivatePartyAsync_UsesTypedDeactivateCommandPayloadAsync()
    {
        (HttpPartiesCommandClient client, MockHandler handler) = CreateClient("corr-789");

        string result = await client.DeactivatePartyAsync("p-2", CancellationToken.None);

        result.ShouldBe("corr-789");
        using JsonDocument body = JsonDocument.Parse(handler.LastRequestBody!);
        JsonElement root = body.RootElement;
        root.GetProperty("aggregateId").GetString().ShouldBe("p-2");
        root.GetProperty("commandType").GetString().ShouldBe(typeof(DeactivateParty).FullName);
        root.GetProperty("payload").GetProperty("partyId").GetString().ShouldBe("p-2");
    }

    [Theory]
    [InlineData(nameof(IPartiesCommandClient.UpdateOrganizationDetailsAsync), typeof(UpdateOrganizationDetails))]
    [InlineData(nameof(IPartiesCommandClient.AddContactChannelAsync), typeof(AddContactChannel))]
    [InlineData(nameof(IPartiesCommandClient.UpdateContactChannelAsync), typeof(UpdateContactChannel))]
    [InlineData(nameof(IPartiesCommandClient.RemoveContactChannelAsync), typeof(RemoveContactChannel))]
    [InlineData(nameof(IPartiesCommandClient.AddIdentifierAsync), typeof(AddIdentifier))]
    [InlineData(nameof(IPartiesCommandClient.RemoveIdentifierAsync), typeof(RemoveIdentifier))]
    [InlineData(nameof(IPartiesCommandClient.UpdatePartyCompositeAsync), typeof(UpdatePartyComposite))]
    [InlineData(nameof(IPartiesCommandClient.SetIsNaturalPersonAsync), typeof(SetIsNaturalPerson))]
    public async Task RoutePartyIdMethods_OverrideStalePayloadPartyIdAsync(string methodName, Type expectedCommandType)
    {
        ArgumentNullException.ThrowIfNull(expectedCommandType);

        (HttpPartiesCommandClient client, MockHandler handler) = CreateClient("corr-route");

        await InvokeRouteMethodAsync(client, methodName);

        using JsonDocument body = JsonDocument.Parse(handler.LastRequestBody!);
        JsonElement root = body.RootElement;
        root.GetProperty("aggregateId").GetString().ShouldBe("p-route");
        root.GetProperty("commandType").GetString().ShouldBe(expectedCommandType.FullName);
        root.GetProperty("payload").GetProperty("partyId").GetString().ShouldBe("p-route");
    }

    [Fact]
    public async Task CreatePartyCompositeAsync_UsesConcreteCommandTypeAsync()
    {
        (HttpPartiesCommandClient client, MockHandler handler) = CreateClient("corr-comp");

        var command = new CreatePartyComposite
        {
            PartyId = "p-3",
            Type = PartyType.Organization,
        };

        await client.CreatePartyCompositeAsync(command, CancellationToken.None);

        using JsonDocument body = JsonDocument.Parse(handler.LastRequestBody!);
        body.RootElement.GetProperty("aggregateId").GetString().ShouldBe("p-3");
        body.RootElement.GetProperty("commandType").GetString().ShouldBe(typeof(CreatePartyComposite).FullName);
    }

    [Fact]
    public async Task PostCommand_OnErrorResponse_ThrowsPartiesClientExceptionAsync()
    {
        string problemJson = JsonSerializer.Serialize(new
        {
            status = 404,
            title = "Party Not Found",
            type = "urn:hexalith:eventstore:error:not-found",
            detail = "No party found with ID 'p-missing'.",
            correlationId = "corr-err",
        });

        var handler = new MockHandler(
            HttpStatusCode.NotFound,
            problemJson,
            "application/problem+json");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };
        var client = new HttpPartiesCommandClient(httpClient, Options.Create(ClientOptions()));

        PartiesClientException exception = await Should.ThrowAsync<PartiesClientException>(
            () => client.DeactivatePartyAsync("p-missing", CancellationToken.None));

        exception.Status.ShouldBe(404);
        exception.Title.ShouldBe("Party Not Found");
        exception.Type.ShouldBe("urn:hexalith:eventstore:error:not-found");
        exception.Detail.ShouldBe("No party found with ID 'p-missing'.");
        exception.CorrelationId.ShouldBe("corr-err");
    }

    [Fact]
    public async Task PostCommand_OnSensitiveProblemDetail_DoesNotLeakPayloadValuesAsync()
    {
        string problemJson = JsonSerializer.Serialize(new
        {
            status = 422,
            title = "Validation failed",
            type = "urn:hexalith:eventstore:error:validation",
            detail = "payload.personDetails.email=ada@example.test apiKey=abc connectionString=redis://host client_secret=secret",
            correlationId = "corr-sensitive",
        });

        var handler = new MockHandler(
            HttpStatusCode.UnprocessableEntity,
            problemJson,
            "application/problem+json");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };
        var client = new HttpPartiesCommandClient(httpClient, Options.Create(ClientOptions()));

        PartiesClientException exception = await Should.ThrowAsync<PartiesClientException>(
            () => client.CreatePartyAsync(
                new CreateParty { PartyId = "p-sensitive", Type = PartyType.Person },
                CancellationToken.None));

        exception.Detail.ShouldNotBeNull().ShouldNotContain("ada@example.test");
        exception.Detail.ShouldNotBeNull().ShouldNotContain("abc");
        exception.Detail.ShouldNotBeNull().ShouldNotContain("redis://host");
        exception.Detail.ShouldNotBeNull().ShouldNotContain("secret");
        exception.CorrelationId.ShouldBe("corr-sensitive");
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, 400, "urn:hexalith:eventstore:error:validation")]
    [InlineData(HttpStatusCode.Unauthorized, 401, "urn:hexalith:eventstore:error:unauthorized")]
    [InlineData(HttpStatusCode.Forbidden, 403, "urn:hexalith:eventstore:error:forbidden")]
    [InlineData(HttpStatusCode.Conflict, 409, "urn:hexalith:eventstore:error:conflict")]
    [InlineData(HttpStatusCode.ServiceUnavailable, 503, "urn:hexalith:eventstore:error:degraded")]
    public async Task CommandErrorResponses_MapToPartiesClientExceptionAsync(
        HttpStatusCode httpStatusCode,
        int expectedStatus,
        string expectedType)
    {
        string problemJson = JsonSerializer.Serialize(new
        {
            status = expectedStatus,
            title = "EventStore command failed",
            type = expectedType,
            detail = "Safe command failure detail.",
            correlationId = "corr-command-error",
        });

        var handler = new MockHandler(httpStatusCode, problemJson, "application/problem+json");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };
        var client = new HttpPartiesCommandClient(httpClient, Options.Create(ClientOptions()));

        PartiesClientException exception = await Should.ThrowAsync<PartiesClientException>(
            () => client.DeactivatePartyAsync("p-error", CancellationToken.None));

        exception.Status.ShouldBe(expectedStatus);
        exception.Type.ShouldBe(expectedType);
        exception.Detail.ShouldBe("Safe command failure detail.");
        exception.CorrelationId.ShouldBe("corr-command-error");
    }

    [Fact]
    public async Task PostCommand_OnProblemDetailsWithNonStringFields_DoesNotThrowInvalidOperationAsync()
    {
        // Defends against JsonElement.GetString() InvalidOperationException when problem-detail
        // fields arrive with unexpected JSON types; surfaces a typed PartiesClientException.
        string problemJson = "{\"status\": 503, \"title\": 1, \"type\": false, \"detail\": null, \"correlationId\": 99}";
        var handler = new MockHandler(HttpStatusCode.ServiceUnavailable, problemJson, "application/problem+json");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };
        var client = new HttpPartiesCommandClient(httpClient, Options.Create(ClientOptions()));

        PartiesClientException exception = await Should.ThrowAsync<PartiesClientException>(
            () => client.DeactivatePartyAsync("p-bad-fields", CancellationToken.None));

        exception.Status.ShouldBe(503);
        exception.Title.ShouldNotBeNull();
        exception.CorrelationId.ShouldBeNull();
    }

    [Fact]
    public async Task PostCommand_OnMalformedAcceptedResponse_ThrowsPartiesClientExceptionAsync()
    {
        var handler = new MockHandler(
            HttpStatusCode.Accepted,
            "{}",
            "application/json");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };
        var client = new HttpPartiesCommandClient(httpClient, Options.Create(ClientOptions()));

        PartiesClientException exception = await Should.ThrowAsync<PartiesClientException>(
            () => client.DeactivatePartyAsync("p-malformed", CancellationToken.None));

        exception.Status.ShouldBe(202);
        exception.Detail.ShouldBe("Response did not contain a valid correlationId.");
    }

    [Fact]
    public async Task DeactivatePartyAsync_WhenCancelled_PropagatesCancellationAsync()
    {
        var httpClient = new HttpClient(new CancellationHandler()) { BaseAddress = new Uri("https://localhost") };
        var client = new HttpPartiesCommandClient(httpClient, Options.Create(ClientOptions()));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => client.DeactivatePartyAsync("p-cancel", cts.Token));
    }

    [Fact]
    public async Task DeactivatePartyAsync_WhenTenantIsMissing_DoesNotSendRequestAsync()
    {
        var handler = new CountingHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };
        var client = new HttpPartiesCommandClient(
            httpClient,
            Options.Create(new PartiesClientOptions { BaseUrl = "https://localhost", Tenant = " " }));

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => client.DeactivatePartyAsync("p-no-tenant", CancellationToken.None));

        exception.Message.ShouldBe("Parties:Tenant configuration is required.");
        handler.SendCount.ShouldBe(0);
    }

    [Fact]
    public async Task DeactivatePartyAsync_WhenTokenIsPreCanceled_DoesNotSendRequestAsync()
    {
        var handler = new CountingHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };
        var client = new HttpPartiesCommandClient(httpClient, Options.Create(ClientOptions()));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => client.DeactivatePartyAsync("p-cancel", cts.Token));

        handler.SendCount.ShouldBe(0);
    }

    private static async Task InvokeRouteMethodAsync(HttpPartiesCommandClient client, string methodName)
    {
        switch (methodName)
        {
            case nameof(IPartiesCommandClient.UpdateOrganizationDetailsAsync):
                await client.UpdateOrganizationDetailsAsync(
                    "p-route",
                    new UpdateOrganizationDetails
                    {
                        PartyId = "stale-id",
                        OrganizationDetails = new OrganizationDetails { LegalName = "Acme Corp" },
                    },
                    CancellationToken.None).ConfigureAwait(false);
                break;
            case nameof(IPartiesCommandClient.AddContactChannelAsync):
                await client.AddContactChannelAsync(
                    "p-route",
                    new AddContactChannel
                    {
                        PartyId = "stale-id",
                        ContactChannelId = "cc-1",
                        Type = ContactChannelType.Email,
                        Value = "test@example.com",
                    },
                    CancellationToken.None).ConfigureAwait(false);
                break;
            case nameof(IPartiesCommandClient.UpdateContactChannelAsync):
                await client.UpdateContactChannelAsync(
                    "p-route",
                    new UpdateContactChannel { PartyId = "stale-id", ContactChannelId = "cc-1" },
                    CancellationToken.None).ConfigureAwait(false);
                break;
            case nameof(IPartiesCommandClient.RemoveContactChannelAsync):
                await client.RemoveContactChannelAsync(
                    "p-route",
                    new RemoveContactChannel { PartyId = "stale-id", ContactChannelId = "cc-1" },
                    CancellationToken.None).ConfigureAwait(false);
                break;
            case nameof(IPartiesCommandClient.AddIdentifierAsync):
                await client.AddIdentifierAsync(
                    "p-route",
                    new AddIdentifier
                    {
                        PartyId = "stale-id",
                        IdentifierId = "id-1",
                        Type = IdentifierType.VAT,
                        Value = "FR123456789",
                    },
                    CancellationToken.None).ConfigureAwait(false);
                break;
            case nameof(IPartiesCommandClient.RemoveIdentifierAsync):
                await client.RemoveIdentifierAsync(
                    "p-route",
                    new RemoveIdentifier { PartyId = "stale-id", IdentifierId = "id-1" },
                    CancellationToken.None).ConfigureAwait(false);
                break;
            case nameof(IPartiesCommandClient.UpdatePartyCompositeAsync):
                await client.UpdatePartyCompositeAsync(
                    "p-route",
                    new UpdatePartyComposite { PartyId = "stale-id" },
                    CancellationToken.None).ConfigureAwait(false);
                break;
            case nameof(IPartiesCommandClient.SetIsNaturalPersonAsync):
                await client.SetIsNaturalPersonAsync(
                    "p-route",
                    new SetIsNaturalPerson { PartyId = "stale-id", IsNaturalPerson = true },
                    CancellationToken.None).ConfigureAwait(false);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(methodName), methodName, null);
        }
    }

    private static (HttpPartiesCommandClient Client, MockHandler Handler) CreateClient(string correlationId)
    {
        var handler = new MockHandler(
            HttpStatusCode.Accepted,
            JsonSerializer.Serialize(new { correlationId }),
            "application/json");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };
        return (new HttpPartiesCommandClient(httpClient, Options.Create(ClientOptions())), handler);
    }

    private static PartiesClientOptions ClientOptions()
        => new()
        {
            BaseUrl = "https://localhost",
            Tenant = "tenant-a",
        };

    internal sealed class MockHandler(
        HttpStatusCode statusCode,
        string responseBody,
        string contentType) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, contentType),
            };
        }
    }

    private sealed class CancellationHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));
        }
    }

    internal sealed class CountingHandler : HttpMessageHandler
    {
        public int SendCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            SendCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { correlationId = "corr-count" }), Encoding.UTF8, "application/json"),
            });
        }
    }
}
