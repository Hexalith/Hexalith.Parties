using System.Net;
using System.Text;
using System.Text.Json;

using Hexalith.Parties.Client.Abstractions;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.ValueObjects;

using Shouldly;

namespace Hexalith.Parties.Client.Tests;

public sealed class HttpPartiesCommandClientTests
{
    [Fact]
    public async Task CreatePartyAsync_SendsPostToCorrectEndpoint_ReturnsCorrelationIdAsync()
    {
        const string expectedCorrelationId = "corr-123";
        (HttpPartiesCommandClient client, MockHandler handler) = CreateClient(expectedCorrelationId);

        var command = new CreateParty
        {
            PartyId = "p-1",
            Type = PartyType.Person,
        };

        string result = await client.CreatePartyAsync(command, CancellationToken.None);

        result.ShouldBe(expectedCorrelationId);
        handler.LastRequest!.Method.ShouldBe(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.ShouldBe("/api/v1/parties");
    }

    [Fact]
    public async Task UpdatePersonDetailsAsync_SendsPostWithPartyIdInRoute_ReturnsCorrelationIdAsync()
    {
        const string expectedCorrelationId = "corr-456";
        (HttpPartiesCommandClient client, MockHandler handler) = CreateClient(expectedCorrelationId);

        var command = new UpdatePersonDetails
        {
            PartyId = "p-1",
            PersonDetails = new PersonDetails { FirstName = "Jane", LastName = "Doe" },
        };

        string result = await client.UpdatePersonDetailsAsync("p-1", command, CancellationToken.None);

        result.ShouldBe(expectedCorrelationId);
        handler.LastRequest!.RequestUri!.PathAndQuery.ShouldBe("/api/v1/parties/p-1/update-person-details");

        using JsonDocument body = JsonDocument.Parse(handler.LastRequestBody!);
        body.RootElement.GetProperty("partyId").GetString().ShouldBe("p-1");
    }

    [Fact]
    public async Task UpdatePersonDetailsAsync_ReplacesBodyPartyIdWithRoutePartyIdAsync()
    {
        (HttpPartiesCommandClient client, MockHandler handler) = CreateClient("corr-456");

        var command = new UpdatePersonDetails
        {
            PartyId = "stale-id",
            PersonDetails = new PersonDetails { FirstName = "Jane", LastName = "Doe" },
        };

        await client.UpdatePersonDetailsAsync("p-1", command, CancellationToken.None);

        using JsonDocument body = JsonDocument.Parse(handler.LastRequestBody!);
        body.RootElement.GetProperty("partyId").GetString().ShouldBe("p-1");
    }

    [Fact]
    public async Task DeactivatePartyAsync_SendsPostWithEmptyBody_ReturnsCorrelationIdAsync()
    {
        const string expectedCorrelationId = "corr-789";
        (HttpPartiesCommandClient client, MockHandler handler) = CreateClient(expectedCorrelationId);

        string result = await client.DeactivatePartyAsync("p-1", CancellationToken.None);

        result.ShouldBe(expectedCorrelationId);
        handler.LastRequest!.RequestUri!.PathAndQuery.ShouldBe("/api/v1/parties/p-1/deactivate");
    }

    [Fact]
    public async Task ReactivatePartyAsync_SendsPostToCorrectEndpointAsync()
    {
        (HttpPartiesCommandClient client, MockHandler handler) = CreateClient("corr-x");

        await client.ReactivatePartyAsync("p-2", CancellationToken.None);

        handler.LastRequest!.RequestUri!.PathAndQuery.ShouldBe("/api/v1/parties/p-2/reactivate");
    }

    [Fact]
    public async Task CreatePartyCompositeAsync_SendsPostToCorrectEndpointAsync()
    {
        (HttpPartiesCommandClient client, MockHandler handler) = CreateClient("corr-comp");

        var command = new CreatePartyComposite
        {
            PartyId = "p-3",
            Type = PartyType.Organization,
        };

        await client.CreatePartyCompositeAsync(command, CancellationToken.None);

        handler.LastRequest!.RequestUri!.PathAndQuery.ShouldBe("/api/v1/parties/create-composite");
    }

    [Fact]
    public async Task UpdatePartyCompositeAsync_SendsPostWithPartyIdInRouteAsync()
    {
        (HttpPartiesCommandClient client, MockHandler handler) = CreateClient("corr-upd");

        var command = new UpdatePartyComposite { PartyId = "p-4" };

        await client.UpdatePartyCompositeAsync("p-4", command, CancellationToken.None);

        handler.LastRequest!.RequestUri!.PathAndQuery.ShouldBe("/api/v1/parties/p-4/update-composite");
    }

    [Fact]
    public async Task UpdatePartyCompositeAsync_ReplacesBodyPartyIdWithRoutePartyIdAsync()
    {
        (HttpPartiesCommandClient client, MockHandler handler) = CreateClient("corr-upd");

        var command = new UpdatePartyComposite { PartyId = "stale-id" };

        await client.UpdatePartyCompositeAsync("p-4", command, CancellationToken.None);

        using JsonDocument body = JsonDocument.Parse(handler.LastRequestBody!);
        body.RootElement.GetProperty("partyId").GetString().ShouldBe("p-4");
    }

    [Fact]
    public async Task SetIsNaturalPersonAsync_SendsPostToCorrectEndpointAsync()
    {
        (HttpPartiesCommandClient client, MockHandler handler) = CreateClient("corr-nat");

        var command = new SetIsNaturalPerson { PartyId = "p-5", IsNaturalPerson = true };

        await client.SetIsNaturalPersonAsync("p-5", command, CancellationToken.None);

        handler.LastRequest!.RequestUri!.PathAndQuery.ShouldBe("/api/v1/parties/p-5/set-natural-person");
    }

    [Fact]
    public async Task SetIsNaturalPersonAsync_ReplacesBodyPartyIdWithRoutePartyIdAsync()
    {
        (HttpPartiesCommandClient client, MockHandler handler) = CreateClient("corr-nat");

        var command = new SetIsNaturalPerson { PartyId = "stale-id", IsNaturalPerson = true };

        await client.SetIsNaturalPersonAsync("p-5", command, CancellationToken.None);

        using JsonDocument body = JsonDocument.Parse(handler.LastRequestBody!);
        body.RootElement.GetProperty("partyId").GetString().ShouldBe("p-5");
    }

    [Fact]
    public async Task AddContactChannelAsync_SendsPostToCorrectEndpointAsync()
    {
        (HttpPartiesCommandClient client, MockHandler handler) = CreateClient("corr-acc");

        var command = new AddContactChannel
        {
            PartyId = "p-6",
            ContactChannelId = "cc-1",
            Type = ContactChannelType.Email,
            Value = "test@example.com",
        };

        await client.AddContactChannelAsync("p-6", command, CancellationToken.None);

        handler.LastRequest!.RequestUri!.PathAndQuery.ShouldBe("/api/v1/parties/p-6/add-contact-channel");
    }

    [Fact]
    public async Task AddContactChannelAsync_ReplacesBodyPartyIdWithRoutePartyIdAsync()
    {
        (HttpPartiesCommandClient client, MockHandler handler) = CreateClient("corr-acc");

        var command = new AddContactChannel
        {
            PartyId = "stale-id",
            ContactChannelId = "cc-1",
            Type = ContactChannelType.Email,
            Value = "test@example.com",
        };

        await client.AddContactChannelAsync("p-6", command, CancellationToken.None);

        using JsonDocument body = JsonDocument.Parse(handler.LastRequestBody!);
        body.RootElement.GetProperty("partyId").GetString().ShouldBe("p-6");
    }

    [Fact]
    public async Task RemoveContactChannelAsync_SendsPostToCorrectEndpointAsync()
    {
        (HttpPartiesCommandClient client, MockHandler handler) = CreateClient("corr-rcc");

        var command = new RemoveContactChannel { PartyId = "p-7", ContactChannelId = "cc-1" };

        await client.RemoveContactChannelAsync("p-7", command, CancellationToken.None);

        handler.LastRequest!.RequestUri!.PathAndQuery.ShouldBe("/api/v1/parties/p-7/remove-contact-channel");
    }

    [Fact]
    public async Task RemoveContactChannelAsync_ReplacesBodyPartyIdWithRoutePartyIdAsync()
    {
        (HttpPartiesCommandClient client, MockHandler handler) = CreateClient("corr-rcc");

        var command = new RemoveContactChannel { PartyId = "stale-id", ContactChannelId = "cc-1" };

        await client.RemoveContactChannelAsync("p-7", command, CancellationToken.None);

        using JsonDocument body = JsonDocument.Parse(handler.LastRequestBody!);
        body.RootElement.GetProperty("partyId").GetString().ShouldBe("p-7");
    }

    [Fact]
    public async Task UpdateContactChannelAsync_SendsPostToCorrectEndpointAsync()
    {
        (HttpPartiesCommandClient client, MockHandler handler) = CreateClient("corr-ucc");

        var command = new UpdateContactChannel { PartyId = "p-8", ContactChannelId = "cc-1" };

        await client.UpdateContactChannelAsync("p-8", command, CancellationToken.None);

        handler.LastRequest!.RequestUri!.PathAndQuery.ShouldBe("/api/v1/parties/p-8/update-contact-channel");
    }

    [Fact]
    public async Task UpdateContactChannelAsync_ReplacesBodyPartyIdWithRoutePartyIdAsync()
    {
        (HttpPartiesCommandClient client, MockHandler handler) = CreateClient("corr-ucc");

        var command = new UpdateContactChannel { PartyId = "stale-id", ContactChannelId = "cc-1" };

        await client.UpdateContactChannelAsync("p-8", command, CancellationToken.None);

        using JsonDocument body = JsonDocument.Parse(handler.LastRequestBody!);
        body.RootElement.GetProperty("partyId").GetString().ShouldBe("p-8");
    }

    [Fact]
    public async Task AddIdentifierAsync_SendsPostToCorrectEndpointAsync()
    {
        (HttpPartiesCommandClient client, MockHandler handler) = CreateClient("corr-aid");

        var command = new AddIdentifier
        {
            PartyId = "p-9",
            IdentifierId = "id-1",
            Type = IdentifierType.VAT,
            Value = "FR123456789",
        };

        await client.AddIdentifierAsync("p-9", command, CancellationToken.None);

        handler.LastRequest!.RequestUri!.PathAndQuery.ShouldBe("/api/v1/parties/p-9/add-identifier");
    }

    [Fact]
    public async Task AddIdentifierAsync_ReplacesBodyPartyIdWithRoutePartyIdAsync()
    {
        (HttpPartiesCommandClient client, MockHandler handler) = CreateClient("corr-aid");

        var command = new AddIdentifier
        {
            PartyId = "stale-id",
            IdentifierId = "id-1",
            Type = IdentifierType.VAT,
            Value = "FR123456789",
        };

        await client.AddIdentifierAsync("p-9", command, CancellationToken.None);

        using JsonDocument body = JsonDocument.Parse(handler.LastRequestBody!);
        body.RootElement.GetProperty("partyId").GetString().ShouldBe("p-9");
    }

    [Fact]
    public async Task RemoveIdentifierAsync_SendsPostToCorrectEndpointAsync()
    {
        (HttpPartiesCommandClient client, MockHandler handler) = CreateClient("corr-rid");

        var command = new RemoveIdentifier { PartyId = "p-10", IdentifierId = "id-1" };

        await client.RemoveIdentifierAsync("p-10", command, CancellationToken.None);

        handler.LastRequest!.RequestUri!.PathAndQuery.ShouldBe("/api/v1/parties/p-10/remove-identifier");
    }

    [Fact]
    public async Task RemoveIdentifierAsync_ReplacesBodyPartyIdWithRoutePartyIdAsync()
    {
        (HttpPartiesCommandClient client, MockHandler handler) = CreateClient("corr-rid");

        var command = new RemoveIdentifier { PartyId = "stale-id", IdentifierId = "id-1" };

        await client.RemoveIdentifierAsync("p-10", command, CancellationToken.None);

        using JsonDocument body = JsonDocument.Parse(handler.LastRequestBody!);
        body.RootElement.GetProperty("partyId").GetString().ShouldBe("p-10");
    }

    [Fact]
    public async Task UpdateOrganizationDetailsAsync_SendsPostToCorrectEndpointAsync()
    {
        (HttpPartiesCommandClient client, MockHandler handler) = CreateClient("corr-org");

        var command = new UpdateOrganizationDetails
        {
            PartyId = "p-11",
            OrganizationDetails = new OrganizationDetails { LegalName = "Acme Corp" },
        };

        await client.UpdateOrganizationDetailsAsync("p-11", command, CancellationToken.None);

        handler.LastRequest!.RequestUri!.PathAndQuery.ShouldBe("/api/v1/parties/p-11/update-organization-details");
    }

    [Fact]
    public async Task UpdateOrganizationDetailsAsync_ReplacesBodyPartyIdWithRoutePartyIdAsync()
    {
        (HttpPartiesCommandClient client, MockHandler handler) = CreateClient("corr-org");

        var command = new UpdateOrganizationDetails
        {
            PartyId = "stale-id",
            OrganizationDetails = new OrganizationDetails { LegalName = "Acme Corp" },
        };

        await client.UpdateOrganizationDetailsAsync("p-11", command, CancellationToken.None);

        using JsonDocument body = JsonDocument.Parse(handler.LastRequestBody!);
        body.RootElement.GetProperty("partyId").GetString().ShouldBe("p-11");
    }

    [Fact]
    public async Task CreatePartyAsync_SerializesWithCamelCaseAndStringEnumsAsync()
    {
        (HttpPartiesCommandClient client, MockHandler handler) = CreateClient("corr-ser");

        var command = new CreateParty
        {
            PartyId = "p-ser",
            Type = PartyType.Person,
            PersonDetails = new PersonDetails { FirstName = "John", LastName = "Doe" },
        };

        await client.CreatePartyAsync(command, CancellationToken.None);

        string body = handler.LastRequestBody!;
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        root.GetProperty("partyId").GetString().ShouldBe("p-ser");
        root.GetProperty("type").GetString().ShouldBe("Person");
        root.GetProperty("personDetails").GetProperty("firstName").GetString().ShouldBe("John");
    }

    [Fact]
    public async Task PostCommand_OnErrorResponse_ThrowsPartiesClientExceptionAsync()
    {
        string problemJson = JsonSerializer.Serialize(new
        {
            status = 404,
            title = "Party Not Found",
            type = "urn:hexalith:parties:error:PartyNotFound",
            detail = "No party found with ID 'p-missing'.",
            correlationId = "corr-err",
        });

        var handler = new MockHandler(
            HttpStatusCode.NotFound,
            problemJson,
            "application/problem+json");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };
        var client = new HttpPartiesCommandClient(httpClient);

        PartiesClientException exception = await Should.ThrowAsync<PartiesClientException>(
            () => client.DeactivatePartyAsync("p-missing", CancellationToken.None));

        exception.Status.ShouldBe(404);
        exception.Title.ShouldBe("Party Not Found");
        exception.Type.ShouldBe("urn:hexalith:parties:error:PartyNotFound");
        exception.Detail.ShouldBe("No party found with ID 'p-missing'.");
        exception.CorrelationId.ShouldBe("corr-err");
    }

    [Fact]
    public async Task PostCommand_OnMalformedAcceptedResponse_ThrowsPartiesClientExceptionAsync()
    {
        var handler = new MockHandler(
            HttpStatusCode.Accepted,
            "{}",
            "application/json");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };
        var client = new HttpPartiesCommandClient(httpClient);

        PartiesClientException exception = await Should.ThrowAsync<PartiesClientException>(
            () => client.DeactivatePartyAsync("p-malformed", CancellationToken.None));

        exception.Status.ShouldBe(202);
        exception.Detail.ShouldBe("Response did not contain a valid correlationId.");
    }

    private static (HttpPartiesCommandClient Client, MockHandler Handler) CreateClient(string correlationId)
    {
        var handler = new MockHandler(
            HttpStatusCode.Accepted,
            JsonSerializer.Serialize(new { correlationId }),
            "application/json");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };
        return (new HttpPartiesCommandClient(httpClient), handler);
    }

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
}
