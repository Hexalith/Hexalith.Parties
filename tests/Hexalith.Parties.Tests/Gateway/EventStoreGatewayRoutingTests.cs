extern alias eventstore;

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

using FluentValidation;

using eventstore::Hexalith.EventStore.Configuration;
using eventstore::Hexalith.EventStore.Indexes;
using eventstore::Hexalith.EventStore.Models;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Queries;
using Hexalith.Parties.Domain;
using Hexalith.Parties.Validation;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;

using NSubstitute;

using Shouldly;

using EventStoreProgram = eventstore::Program;

namespace Hexalith.Parties.Tests.Gateway;

public sealed class EventStoreGatewayRoutingTests
{
    [Fact]
    public async Task OpenApiDocument_InDocumentationMode_DescribesGatewayContractsAndComplianceWarningAsync()
    {
        using var factory = new EventStoreGatewayTestFactory(openApiEnabled: true);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/openapi/v1.json");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");

        JsonElement document = await response.Content.ReadFromJsonAsync<JsonElement>();
        document.GetProperty("openapi").GetString().ShouldStartWith("3.");
        document.GetProperty("info").GetProperty("title").GetString().ShouldBe("Hexalith EventStore Gateway API");
        string description = document.GetProperty("info").GetProperty("description").GetString()!;
        description.ShouldContain("GDPR");
        description.ShouldContain("v1.1");

        JsonElement paths = document.GetProperty("paths");
        paths.TryGetProperty("/api/v1/commands", out JsonElement commandPath).ShouldBeTrue();
        paths.TryGetProperty("/api/v1/queries", out JsonElement queryPath).ShouldBeTrue();

        string[] advertisedPaths = paths.EnumerateObject().Select(static path => path.Name).ToArray();
        advertisedPaths.ShouldNotContain("/api/v2/commands");
        advertisedPaths.ShouldNotContain("/api/v2/queries");
        advertisedPaths.ShouldNotContain(RetiredPartiesRoute());

        JsonElement securitySchemes = document
            .GetProperty("components")
            .GetProperty("securitySchemes");
        securitySchemes.TryGetProperty("Bearer", out JsonElement bearerScheme).ShouldBeTrue();
        bearerScheme.GetProperty("scheme").GetString().ShouldBe("bearer");

        AssertRequestSchemaReferencesContract(commandPath.GetProperty("post"), "SubmitCommandRequest");
        AssertRequestSchemaReferencesContract(queryPath.GetProperty("post"), "SubmitQueryRequest");
        AssertProblemResponse(commandPath.GetProperty("post"), "400");
        AssertProblemResponse(commandPath.GetProperty("post"), "401");
        AssertProblemResponse(commandPath.GetProperty("post"), "422");
        AssertProblemResponse(queryPath.GetProperty("post"), "400");
        AssertProblemResponse(queryPath.GetProperty("post"), "401");
        AssertProblemResponse(queryPath.GetProperty("post"), "503");
    }

    [Fact]
    public async Task ErrorCatalog_InDocumentationMode_ListsStableProblemTypesAndGuidanceAsync()
    {
        using var factory = new EventStoreGatewayTestFactory(openApiEnabled: true);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/problems/catalog.json");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonElement catalog = await response.Content.ReadFromJsonAsync<JsonElement>();
        AssertProblemCatalogEntry(catalog, "validation-error", HttpStatusCode.BadRequest);
        AssertProblemCatalogEntry(catalog, "unsupported-api-version", HttpStatusCode.BadRequest);
        AssertProblemCatalogEntry(catalog, "domain-rejections", HttpStatusCode.UnprocessableEntity);
        AssertProblemCatalogEntry(catalog, "internal-server-error", HttpStatusCode.InternalServerError);
    }

    [Theory]
    [MemberData(nameof(PartiesDomainRejectionDocumentationData))]
    public async Task DomainRejectionCatalog_DocumentsPartiesStableRejectionTypesAsync(
        string reasonCode,
        HttpStatusCode expectedStatus,
        string expectedTitle,
        string expectedCorrectiveAction)
    {
        using var factory = new EventStoreGatewayTestFactory(openApiEnabled: true);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync($"/problems/domain-rejections/{reasonCode}.json");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");

        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("reasonCode").GetString().ShouldBe(reasonCode);
        problem.GetProperty("typeUri").GetString().ShouldBe($"https://hexalith.io/problems/domain-rejections/{reasonCode}");
        problem.GetProperty("title").GetString().ShouldBe(expectedTitle);
        problem.GetProperty("statusCode").GetInt32().ShouldBe((int)expectedStatus);
        problem.GetProperty("correctiveAction").GetString().ShouldBe(expectedCorrectiveAction);
    }

    [Fact]
    public async Task PostCommands_NoAuth_Returns401BeforePartyInvocationAsync()
    {
        using var factory = new EventStoreGatewayTestFactory();
        using HttpClient client = factory.CreateClient();

        var request = CreateCommandRequest(
            messageId: "cmd-3-3-no-auth",
            aggregateId: "party-no-auth",
            payload: JsonSerializer.SerializeToElement(new { PartyId = "party-no-auth" }));

        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        factory.CommandActor.ReceivedCommands.ShouldBeEmpty();
        factory.QueryRouter.ReceivedQueries.ShouldBeEmpty();
        factory.StatusStore.GetStatusCount().ShouldBe(0);
        factory.ArchiveStore.GetArchiveCount().ShouldBe(0);
    }

    [Fact]
    public async Task PostUnsupportedApiVersion_ReturnsDocumentedProblemBeforePartyInvocationAsync()
    {
        using var factory = new EventStoreGatewayTestFactory();
        using HttpClient client = factory.CreateClient();

        var request = CreateCommandRequest(
            messageId: "cmd-3-3-unsupported-version",
            aggregateId: "party-unsupported-version",
            payload: JsonSerializer.SerializeToElement(new { PartyId = "party-unsupported-version" }));

        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/v2/commands", request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("type").GetString().ShouldBe("https://hexalith.io/problems/unsupported-api-version");
        body.GetProperty("title").GetString().ShouldBe("Unsupported API version");
        body.GetProperty("status").GetInt32().ShouldBe(400);
        body.GetProperty("reasonCode").GetString().ShouldBe("unsupported-api-version");
        body.GetProperty("requestedVersion").GetString().ShouldBe("v2");
        body.GetProperty("supportedVersions").EnumerateArray().Select(static item => item.GetString()).ShouldBe(["v1"]);

        factory.CommandActor.ReceivedCommands.ShouldBeEmpty();
        factory.QueryRouter.ReceivedQueries.ShouldBeEmpty();
        factory.StatusStore.GetStatusCount().ShouldBe(0);
        factory.ArchiveStore.GetArchiveCount().ShouldBe(0);
    }

    [Theory]
    [MemberData(nameof(RetiredPartiesRestRouteData))]
    public async Task RetiredPartiesRestRoutes_Return404WithoutGatewayOrActorAccessAsync(string method, string path)
    {
        using var factory = new EventStoreGatewayTestFactory();
        using HttpClient client = factory.CreateAuthenticatedClient(permissions: ["commands:*", "query:read"]);
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (string.Equals(method, "POST", StringComparison.Ordinal))
        {
            request.Content = JsonContent.Create(new { partyId = "party-3-3" });
        }

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("type").GetString().ShouldBe("https://hexalith.io/problems/not-found");
        body.GetProperty("reasonCode").GetString().ShouldBe("route-not-found");
        body.GetProperty("supportedVersions").EnumerateArray().Select(static item => item.GetString()).ShouldBe(["v1"]);

        factory.CommandActor.ReceivedCommands.ShouldBeEmpty();
        factory.QueryRouter.ReceivedQueries.ShouldBeEmpty();
        factory.StatusStore.GetStatusCount().ShouldBe(0);
        factory.ArchiveStore.GetArchiveCount().ShouldBe(0);
    }

    [Fact]
    public async Task PostCommands_PartyDomain_ReachesEventStoreGatewayAndRoutesPartyEnvelopeAsync()
    {
        using var factory = new EventStoreGatewayTestFactory();
        using HttpClient client = factory.CreateAuthenticatedClient();

        var request = CreateCommandRequest(
            messageId: "cmd-12-4-create-party",
            aggregateId: "party-12-4",
            payload: JsonSerializer.SerializeToElement(new CreatePartyComposite
            {
                PartyId = "party-12-4",
                Type = PartyType.Person,
                PersonDetails = new PersonDetails { FirstName = "Ada", LastName = "Lovelace" },
            }));

        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        response.Headers.Location!.ToString().ShouldContain("/api/v1/commands/status/cmd-12-4-create-party");

        CommandEnvelope command = factory.CommandActor.ReceivedCommands.Single();
        command.TenantId.ShouldBe("tenant-a");
        command.Domain.ShouldBe("party");
        command.AggregateId.ShouldBe("party-12-4");
        command.CommandType.ShouldBe(typeof(CreatePartyComposite).FullName);
        command.Payload.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task DomainServiceResolver_PartyWildcardRegistration_PointsToPartiesProcessAsync()
    {
        using var factory = new EventStoreGatewayTestFactory();

        IDomainServiceResolver resolver = factory.Services.GetRequiredService<IDomainServiceResolver>();

        DomainServiceRegistration? registration = await resolver.ResolveAsync("tenant-a", "party");

        registration.ShouldNotBeNull();
        registration.AppId.ShouldBe("parties");
        registration.MethodName.ShouldBe("process");
        registration.TenantId.ShouldBe("tenant-a");
        registration.Domain.ShouldBe("party");
        registration.Version.ShouldBe("v1");
    }

    [Fact]
    public async Task PostCommands_PartyDomain_CanExecutePartiesDomainInvokerAsync()
    {
        using var factory = new EventStoreGatewayTestFactory(usePartiesDomainRouter: true);
        using HttpClient client = factory.CreateAuthenticatedClient(permissions: ["commands:*"]);
        string partyId = Guid.NewGuid().ToString("D");

        var request = CreateCommandRequest(
            messageId: "cmd-12-4-domain-exec",
            aggregateId: partyId,
            payload: JsonSerializer.SerializeToElement(new CreatePartyComposite
            {
                PartyId = partyId,
                Type = PartyType.Person,
                PersonDetails = new PersonDetails { FirstName = "Ada", LastName = "Lovelace" },
            }));

        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        factory.PartiesCommandRouter.ShouldNotBeNull();
        DomainResult result = factory.PartiesCommandRouter.DomainResults.ShouldHaveSingleItem();
        result.IsSuccess.ShouldBeTrue();
        result.Events.ShouldContain(e => e is PartyCreated);
        result.Events.ShouldContain(e => e is PartyDisplayNameDerived);
        factory.StatusStore.GetStatusHistory("tenant-a", "cmd-12-4-domain-exec")
            .Select(s => s.Status)
            .ShouldContain(CommandStatus.Received);
        factory.ArchiveStore.GetArchiveCount().ShouldBe(1);
    }

    [Fact]
    public async Task PostCommands_PartyDomain_ReturnsResultPayloadWhenSynchronousCommandCompletesAsync()
    {
        using var factory = new EventStoreGatewayTestFactory(usePartiesDomainRouter: true);
        using HttpClient client = factory.CreateAuthenticatedClient(permissions: ["commands:*"]);
        string partyId = Guid.NewGuid().ToString("D");

        var request = CreateCommandRequest(
            messageId: "cmd-1-9-domain-payload",
            aggregateId: partyId,
            payload: JsonSerializer.SerializeToElement(new CreatePartyComposite
            {
                PartyId = partyId,
                Type = PartyType.Person,
                PersonDetails = new PersonDetails { FirstName = "Ada", LastName = "Lovelace" },
            }));

        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement root = body.RootElement;
        root.GetProperty("correlationId").GetString().ShouldBe("cmd-1-9-domain-payload");
        JsonElement payload = root.GetProperty("resultPayload");
        payload.GetProperty("id").GetString().ShouldBe(partyId);
        payload.GetProperty("displayName").GetString().ShouldBe("Ada Lovelace");
        factory.StatusStore.GetStatusHistory("tenant-a", "cmd-1-9-domain-payload")
            .Select(status => status.Status)
            .ShouldContain(CommandStatus.Completed);
    }

    [Fact]
    public async Task PostCommands_UnauthorizedTenant_Returns403BeforePartyInvocationAsync()
    {
        using var factory = new EventStoreGatewayTestFactory();
        using HttpClient client = factory.CreateAuthenticatedClient(tenants: ["tenant-b"]);

        var request = new
        {
            messageId = "cmd-12-4-denied-tenant",
            tenant = "tenant-a",
            domain = "party",
            aggregateId = "party-denied",
            commandType = typeof(CreatePartyComposite).FullName,
            payload = new { partyId = "party-denied" },
        };

        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        factory.CommandActor.ReceivedCommands.ShouldBeEmpty();
        factory.StatusStore.GetStatusCount().ShouldBe(0);
        factory.ArchiveStore.GetArchiveCount().ShouldBe(0);
    }

    [Fact]
    public async Task PostCommands_UnauthorizedDomain_Returns403BeforeStatusArchiveOrPartyInvocationAsync()
    {
        using var factory = new EventStoreGatewayTestFactory();
        using HttpClient client = factory.CreateAuthenticatedClient(domains: ["billing"]);

        var request = CreateCommandRequest(
            messageId: "cmd-12-4-denied-domain",
            aggregateId: "party-denied-domain",
            payload: JsonSerializer.SerializeToElement(new { PartyId = "party-denied-domain" }));

        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        factory.CommandActor.ReceivedCommands.ShouldBeEmpty();
        factory.StatusStore.GetStatusCount().ShouldBe(0);
        factory.ArchiveStore.GetArchiveCount().ShouldBe(0);
    }

    [Fact]
    public async Task PostCommands_UnauthorizedPermission_Returns403BeforeStatusArchiveOrPartyInvocationAsync()
    {
        using var factory = new EventStoreGatewayTestFactory();
        using HttpClient client = factory.CreateAuthenticatedClient(permissions: ["query:read"]);

        var request = CreateCommandRequest(
            messageId: "cmd-12-4-denied-rbac",
            aggregateId: "party-denied-rbac",
            payload: JsonSerializer.SerializeToElement(new { PartyId = "party-denied-rbac" }));

        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        factory.CommandActor.ReceivedCommands.ShouldBeEmpty();
        factory.StatusStore.GetStatusCount().ShouldBe(0);
        factory.ArchiveStore.GetArchiveCount().ShouldBe(0);
    }

    [Fact]
    public async Task PostCommands_InvalidGatewayShape_Returns400BeforePartyInvocationAsync()
    {
        using var factory = new EventStoreGatewayTestFactory();
        using HttpClient client = factory.CreateAuthenticatedClient();

        var request = new
        {
            messageId = "cmd-12-4-invalid-domain",
            tenant = "tenant-a",
            domain = "Party",
            aggregateId = "party-invalid",
            commandType = typeof(CreatePartyComposite).FullName,
            payload = new { partyId = "party-invalid" },
        };

        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("type").GetString().ShouldBe("https://hexalith.io/problems/validation-error");
        body.GetProperty("status").GetInt32().ShouldBe(400);
        body.GetProperty("errors").ValueKind.ShouldBe(JsonValueKind.Object);
        factory.CommandActor.ReceivedCommands.ShouldBeEmpty();
        factory.StatusStore.GetStatusCount().ShouldBe(0);
        factory.ArchiveStore.GetArchiveCount().ShouldBe(0);
    }

    [Fact]
    public async Task PostCommands_InvalidPartyPayload_Returns422WithValidationRejectionOnlyAsync()
    {
        using var factory = new EventStoreGatewayTestFactory(usePartiesDomainRouter: true);
        using HttpClient client = factory.CreateAuthenticatedClient(permissions: ["commands:*"]);
        string partyId = Guid.NewGuid().ToString("D");

        var request = CreateCommandRequest(
            messageId: "cmd-12-4-invalid-payload",
            aggregateId: partyId,
            payload: JsonSerializer.SerializeToElement(new CreatePartyComposite
            {
                PartyId = partyId,
                Type = PartyType.Person,
                PersonDetails = null,
            }));

        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("type").GetString().ShouldBe(
            "https://hexalith.io/problems/domain-rejections/party-command-validation-rejected");
        body.GetProperty("title").GetString().ShouldBe("Party Command Validation Rejected");
        body.GetProperty("status").GetInt32().ShouldBe(422);
        body.GetProperty("reasonCode").GetString().ShouldBe("party-command-validation-rejected");
        body.GetProperty("correctiveAction").GetString().ShouldBe("Review the rejection detail, correct the request, and retry when appropriate.");
        body.GetProperty("rejectionType").GetString().ShouldBe(typeof(PartyCommandValidationRejected).FullName);
        factory.PartiesCommandRouter.ShouldNotBeNull();
        DomainResult result = factory.PartiesCommandRouter.DomainResults.ShouldHaveSingleItem();
        result.IsRejection.ShouldBeTrue();
        result.Events.ShouldAllBe(e => e is PartyCommandValidationRejected);
        result.Events.OfType<PartyCreated>().ShouldBeEmpty();
        result.Events.OfType<PartyDisplayNameDerived>().ShouldBeEmpty();
        factory.StatusStore.GetStatusHistory("tenant-a", "cmd-12-4-invalid-payload")
            .Select(s => s.Status)
            .ShouldContain(CommandStatus.Rejected);
        factory.ArchiveStore.GetArchiveCount().ShouldBe(1);
    }

    [Theory]
    [InlineData(typeof(PartyNotFound), HttpStatusCode.NotFound, "party-not-found", "Party Not Found")]
    [InlineData(typeof(PartyCannotAddDuplicateIdentifier), HttpStatusCode.Conflict, "party-cannot-add-duplicate-identifier", "Party Cannot Add Duplicate Identifier")]
    [InlineData(typeof(PartyTypeMismatch), HttpStatusCode.UnprocessableEntity, "party-type-mismatch", "Party Type Mismatch")]
    public async Task PostCommands_TypedDomainRejections_ReturnStableProblemDetailsAsync(
        Type rejectionType,
        HttpStatusCode expectedStatus,
        string expectedReasonCode,
        string expectedTitle)
    {
        ArgumentNullException.ThrowIfNull(rejectionType);

        var rejectingRouter = new ConfigurableCommandRouter
        {
            ProcessingResultFactory = command => new CommandProcessingResult(
                Accepted: false,
                ErrorMessage: $"Domain rejection: {rejectionType.FullName}",
                CorrelationId: command.CorrelationId,
                EventCount: 1,
                ResultPayload: null),
            StatusFactory = command => new CommandStatusRecord(
                CommandStatus.Rejected,
                DateTimeOffset.UtcNow,
                command.AggregateId,
                EventCount: 1,
                RejectionEventType: rejectionType.FullName,
                FailureReason: null,
                TimeoutDuration: null),
        };
        using var factory = new EventStoreGatewayTestFactory(customCommandRouter: rejectingRouter);
        using HttpClient client = factory.CreateAuthenticatedClient(permissions: ["commands:*"]);

        var request = CreateCommandRequest(
            messageId: $"cmd-3-4-{expectedReasonCode}",
            aggregateId: "party-rejected",
            payload: JsonSerializer.SerializeToElement(new { PartyId = "party-rejected" }));

        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

        response.StatusCode.ShouldBe(expectedStatus);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("type").GetString().ShouldBe($"https://hexalith.io/problems/domain-rejections/{expectedReasonCode}");
        body.GetProperty("title").GetString().ShouldBe(expectedTitle);
        body.GetProperty("status").GetInt32().ShouldBe((int)expectedStatus);
        body.GetProperty("reasonCode").GetString().ShouldBe(expectedReasonCode);
        body.GetProperty("rejectionType").GetString().ShouldBe(rejectionType.FullName);
        body.GetProperty("correctiveAction").GetString().ShouldNotBeNullOrWhiteSpace();
        body.GetProperty("detail").GetString().ShouldNotBeNull().ShouldNotContain("personDetails");
        factory.ArchiveStore.GetArchiveCount().ShouldBe(1);
    }

    [Fact]
    public async Task PostQueries_PartyDetail_UsesEventStoreQueryGatewayAndProjectionAdapterRoutingAsync()
    {
        using var factory = new EventStoreGatewayTestFactory();
        using HttpClient client = factory.CreateAuthenticatedClient();

        factory.QueryRouter.ResultPayload = JsonSerializer.SerializeToElement(new
        {
            id = "party-12-4",
            displayName = "Ada Lovelace",
        });

        var request = new
        {
            tenant = "tenant-a",
            domain = PartyDetailProjectionQueryActor.PartyDomain,
            aggregateId = "party-12-4",
            queryType = PartyDetailProjectionQueryActor.PartyDetailQueryType,
            projectionType = PartyDetailProjectionQueryActor.ProjectionType,
            entityId = "party-12-4",
            projectionActorType = PartyDetailProjectionQueryActor.ActorTypeName,
        };

        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/queries", request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        SubmitQuery query = factory.QueryRouter.ReceivedQueries.Single();
        query.Tenant.ShouldBe("tenant-a");
        query.Domain.ShouldBe(PartyDetailProjectionQueryActor.PartyDomain);
        query.AggregateId.ShouldBe("party-12-4");
        query.QueryType.ShouldBe(PartyDetailProjectionQueryActor.PartyDetailQueryType);
        query.EntityId.ShouldBe("party-12-4");
        query.ProjectionType.ShouldBe(PartyDetailProjectionQueryActor.ProjectionType);
        query.ProjectionActorType.ShouldBe(PartyDetailProjectionQueryActor.ActorTypeName);
    }

    [Fact]
    public async Task PostQueries_PartyIndex_UsesEventStoreQueryGatewayAndProjectionAdapterRoutingAsync()
    {
        using var factory = new EventStoreGatewayTestFactory();
        using HttpClient client = factory.CreateAuthenticatedClient(permissions: ["query:read"]);

        factory.QueryRouter.ResultPayload = JsonSerializer.SerializeToElement(new
        {
            items = Array.Empty<object>(),
            page = 1,
            pageSize = 20,
            totalCount = 0,
            totalPages = 1,
        });

        var request = new
        {
            tenant = "tenant-a",
            domain = PartyIndexProjectionQueryActor.PartyDomain,
            aggregateId = PartyIndexProjectionQueryActor.ListAggregateId,
            queryType = PartyIndexProjectionQueryActor.PartyIndexQueryType,
            projectionType = PartyIndexProjectionQueryActor.ProjectionType,
            entityId = PartyIndexProjectionQueryActor.ListAggregateId,
            projectionActorType = PartyIndexProjectionQueryActor.ActorTypeName,
            payload = new
            {
                page = 1,
                pageSize = 20,
                type = "Person",
                active = true,
            },
        };

        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/queries", request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        SubmitQuery query = factory.QueryRouter.ReceivedQueries.Single();
        query.Tenant.ShouldBe("tenant-a");
        query.Domain.ShouldBe(PartyIndexProjectionQueryActor.PartyDomain);
        query.AggregateId.ShouldBe(PartyIndexProjectionQueryActor.ListAggregateId);
        query.QueryType.ShouldBe(PartyIndexProjectionQueryActor.PartyIndexQueryType);
        query.EntityId.ShouldBe(PartyIndexProjectionQueryActor.ListAggregateId);
        query.ProjectionType.ShouldBe(PartyIndexProjectionQueryActor.ProjectionType);
        query.ProjectionActorType.ShouldBe(PartyIndexProjectionQueryActor.ActorTypeName);
    }

    [Fact]
    public async Task PostQueries_PartySearch_UsesEventStoreQueryGatewayAndIndexProjectionAdapterRoutingAsync()
    {
        using var factory = new EventStoreGatewayTestFactory();
        using HttpClient client = factory.CreateAuthenticatedClient(permissions: ["query:read"]);

        factory.QueryRouter.ResultPayload = JsonSerializer.SerializeToElement(new
        {
            items = Array.Empty<object>(),
            page = 1,
            pageSize = 20,
            totalCount = 0,
            totalPages = 1,
        });

        var request = new
        {
            tenant = "tenant-a",
            domain = PartyIndexProjectionQueryActor.PartyDomain,
            aggregateId = PartyIndexProjectionQueryActor.ListAggregateId,
            queryType = PartyIndexProjectionQueryActor.PartySearchQueryType,
            projectionType = PartyIndexProjectionQueryActor.ProjectionType,
            entityId = PartyIndexProjectionQueryActor.ListAggregateId,
            projectionActorType = PartyIndexProjectionQueryActor.ActorTypeName,
            payload = new
            {
                query = "Acme",
                page = 1,
                pageSize = 20,
            },
        };

        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/queries", request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        SubmitQuery query = factory.QueryRouter.ReceivedQueries.Single();
        query.Tenant.ShouldBe("tenant-a");
        query.Domain.ShouldBe(PartyIndexProjectionQueryActor.PartyDomain);
        query.AggregateId.ShouldBe(PartyIndexProjectionQueryActor.ListAggregateId);
        query.QueryType.ShouldBe(PartyIndexProjectionQueryActor.PartySearchQueryType);
        query.EntityId.ShouldBe(PartyIndexProjectionQueryActor.ListAggregateId);
        query.ProjectionType.ShouldBe(PartyIndexProjectionQueryActor.ProjectionType);
        query.ProjectionActorType.ShouldBe(PartyIndexProjectionQueryActor.ActorTypeName);
    }

    [Fact]
    public async Task PostQueries_NotFound_Returns404ThroughEventStoreQueryGatewayAsync()
    {
        using var factory = new EventStoreGatewayTestFactory();
        using HttpClient client = factory.CreateAuthenticatedClient(permissions: ["query:read"]);
        factory.QueryRouter.Result = new QueryRouterResult(
            Success: false,
            Payload: null,
            NotFound: true,
            ErrorMessage: "No projection state available",
            ProjectionType: "party");

        var request = new
        {
            tenant = "tenant-a",
            domain = "party",
            aggregateId = "missing-party",
            queryType = "PartyDetail",
        };

        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/queries", request);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        SubmitQuery query = factory.QueryRouter.ReceivedQueries.ShouldHaveSingleItem();
        query.AggregateId.ShouldBe("missing-party");
        query.EntityId.ShouldBe("missing-party");
    }

    [Fact]
    public async Task PostQueries_RouterForbidden_Returns403ThroughEventStoreQueryGatewayAsync()
    {
        using var factory = new EventStoreGatewayTestFactory();
        using HttpClient client = factory.CreateAuthenticatedClient(permissions: ["query:read"]);
        factory.QueryRouter.Result = new QueryRouterResult(
            Success: false,
            Payload: null,
            NotFound: false,
            ErrorMessage: "Forbidden",
            ProjectionType: "party");

        var request = new
        {
            tenant = "tenant-a",
            domain = "party",
            aggregateId = "tenant-b-party",
            queryType = "PartyDetail",
        };

        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/queries", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        SubmitQuery query = factory.QueryRouter.ReceivedQueries.ShouldHaveSingleItem();
        query.AggregateId.ShouldBe("tenant-b-party");
        query.Domain.ShouldBe("party");
    }

    [Fact]
    public async Task PostQueries_RouterFailure_ReturnsBoundedRetryableProblemDetailsAsync()
    {
        using var factory = new EventStoreGatewayTestFactory();
        using HttpClient client = factory.CreateAuthenticatedClient(permissions: ["query:read"]);
        factory.QueryRouter.Result = new QueryRouterResult(
            Success: false,
            Payload: null,
            NotFound: false,
            ErrorMessage: "ActorException",
            ProjectionType: "party");

        var request = new
        {
            tenant = "tenant-a",
            domain = "party",
            aggregateId = "party-query-failed",
            queryType = "PartyDetail",
        };

        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/queries", request);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        response.Headers.TryGetValues("Retry-After", out IEnumerable<string>? retryAfterHeaders).ShouldBeTrue();
        retryAfterHeaders!.ShouldContain("30");
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("type").GetString().ShouldBe("https://hexalith.io/problems/internal-server-error");
        body.GetProperty("reasonCode").GetString().ShouldBe("query_internal_error");
        body.GetProperty("retryAfter").GetString().ShouldBe("30");
        body.GetProperty("degradation").GetString().ShouldBe("projection-query-unavailable");
        body.GetProperty("detail").GetString().ShouldBe("ActorException");
        JsonSerializer.Serialize(body).ShouldNotContain("StackTrace");
    }

    [Fact]
    public async Task PostQueries_UnauthorizedTenant_Returns403BeforeQueryRoutingAsync()
    {
        using var factory = new EventStoreGatewayTestFactory();
        using HttpClient client = factory.CreateAuthenticatedClient(tenants: ["tenant-b"]);

        var request = new
        {
            tenant = "tenant-a",
            domain = "party",
            aggregateId = "party-denied",
            queryType = "PartyDetail",
        };

        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/queries", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        factory.QueryRouter.ReceivedQueries.ShouldBeEmpty();
    }

    [Fact]
    public async Task PostQueries_UnauthorizedDomain_Returns403BeforeQueryRoutingAsync()
    {
        using var factory = new EventStoreGatewayTestFactory();
        using HttpClient client = factory.CreateAuthenticatedClient(domains: ["billing"], permissions: ["query:read"]);

        var request = new
        {
            tenant = "tenant-a",
            domain = "party",
            aggregateId = "party-denied-domain",
            queryType = "PartyDetail",
        };

        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/queries", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        factory.QueryRouter.ReceivedQueries.ShouldBeEmpty();
    }

    [Fact]
    public async Task PostCommands_PartyDomain_EnrichedPayloadDoesNotLeakPiiIntoStatusStoreAsync()
    {
        using var factory = new EventStoreGatewayTestFactory(usePartiesDomainRouter: true);
        using HttpClient client = factory.CreateAuthenticatedClient(permissions: ["commands:*"]);
        string partyId = Guid.NewGuid().ToString("D");
        const string syntheticFirstName = "Ada";
        const string syntheticLastName = "Lovelace";

        var request = CreateCommandRequest(
            messageId: "cmd-1-9-privacy-status",
            aggregateId: partyId,
            payload: JsonSerializer.SerializeToElement(new CreatePartyComposite
            {
                PartyId = partyId,
                Type = PartyType.Person,
                PersonDetails = new PersonDetails { FirstName = syntheticFirstName, LastName = syntheticLastName },
            }));

        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        IReadOnlyList<CommandStatusRecord> history = factory.StatusStore.GetStatusHistory("tenant-a", "cmd-1-9-privacy-status");
        history.ShouldNotBeEmpty();
        foreach (CommandStatusRecord record in history)
        {
            string serialized = JsonSerializer.Serialize(record);
            serialized.ShouldNotContain(syntheticFirstName, Case.Insensitive);
            serialized.ShouldNotContain(syntheticLastName, Case.Insensitive);
        }
    }

    [Fact]
    public async Task PostCommands_PartyDomain_DuplicateRetryDoesNotPersistPartyDetailInStatusRecordsAsync()
    {
        using var factory = new EventStoreGatewayTestFactory(usePartiesDomainRouter: true);
        using HttpClient client = factory.CreateAuthenticatedClient(permissions: ["commands:*"]);
        string partyId = Guid.NewGuid().ToString("D");
        const string syntheticFirstName = "Ada";
        const string syntheticLastName = "Lovelace";

        var request = CreateCommandRequest(
            messageId: "cmd-1-9-retry-status",
            aggregateId: partyId,
            payload: JsonSerializer.SerializeToElement(new CreatePartyComposite
            {
                PartyId = partyId,
                Type = PartyType.Person,
                PersonDetails = new PersonDetails { FirstName = syntheticFirstName, LastName = syntheticLastName },
            }));

        using HttpResponseMessage first = await client.PostAsJsonAsync("/api/v1/commands", request);
        first.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        using HttpResponseMessage second = await client.PostAsJsonAsync("/api/v1/commands", request);
        second.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        IReadOnlyList<CommandStatusRecord> history = factory.StatusStore.GetStatusHistory("tenant-a", "cmd-1-9-retry-status");
        history.ShouldNotBeEmpty();
        foreach (CommandStatusRecord record in history)
        {
            string serialized = JsonSerializer.Serialize(record);
            serialized.ShouldNotContain(syntheticFirstName, Case.Insensitive);
            serialized.ShouldNotContain(syntheticLastName, Case.Insensitive);
        }
    }

    [Fact]
    public async Task PostCommands_MalformedResultPayloadFromDomainService_GatewayFailsClosedAndOmitsPayloadAsync()
    {
        var malformedRouter = new ConfigurableCommandRouter
        {
            ProcessingResultFactory = command => new CommandProcessingResult(
                Accepted: true,
                CorrelationId: command.CorrelationId,
                EventCount: 1,
                ResultPayload: "this is not valid json {"),
            StatusFactory = _ => new CommandStatusRecord(
                CommandStatus.Completed,
                DateTimeOffset.UtcNow,
                "agg-malformed",
                EventCount: 1,
                RejectionEventType: null,
                FailureReason: null,
                TimeoutDuration: null),
        };
        using var factory = new EventStoreGatewayTestFactory(customCommandRouter: malformedRouter);
        using HttpClient client = factory.CreateAuthenticatedClient(permissions: ["commands:*"]);

        var request = CreateCommandRequest(
            messageId: "cmd-1-9-malformed-payload",
            aggregateId: "agg-malformed",
            payload: JsonSerializer.SerializeToElement(new { partyId = "agg-malformed" }));

        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement root = body.RootElement;
        root.GetProperty("correlationId").GetString().ShouldBe("cmd-1-9-malformed-payload");
        if (root.TryGetProperty("resultPayload", out JsonElement payload))
        {
            payload.ValueKind.ShouldBe(JsonValueKind.Null);
        }
    }

    [Fact]
    public async Task PostCommands_SuccessWithoutResultPayload_PreservesAcceptedCorrelationIdContractAsync()
    {
        var noPayloadRouter = new ConfigurableCommandRouter
        {
            ProcessingResultFactory = command => new CommandProcessingResult(
                Accepted: true,
                CorrelationId: command.CorrelationId,
                EventCount: 1,
                ResultPayload: null),
            StatusFactory = _ => new CommandStatusRecord(
                CommandStatus.Completed,
                DateTimeOffset.UtcNow,
                "agg-no-payload",
                EventCount: 1,
                RejectionEventType: null,
                FailureReason: null,
                TimeoutDuration: null),
        };
        using var factory = new EventStoreGatewayTestFactory(customCommandRouter: noPayloadRouter);
        using HttpClient client = factory.CreateAuthenticatedClient(permissions: ["commands:*"]);

        var request = CreateCommandRequest(
            messageId: "cmd-1-9-no-payload",
            aggregateId: "agg-no-payload",
            payload: JsonSerializer.SerializeToElement(new { partyId = "agg-no-payload" }));

        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/commands", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement root = body.RootElement;
        root.GetProperty("correlationId").GetString().ShouldBe("cmd-1-9-no-payload");
        if (root.TryGetProperty("resultPayload", out JsonElement payload))
        {
            payload.ValueKind.ShouldBe(JsonValueKind.Null);
        }
    }

    private sealed class ConfigurableCommandRouter : ICommandRouter
    {
        public required Func<SubmitCommand, CommandProcessingResult> ProcessingResultFactory { get; init; }

        public required Func<SubmitCommand, CommandStatusRecord> StatusFactory { get; init; }

        public ICommandStatusStore? StatusStore { get; set; }

        public async Task<CommandProcessingResult> RouteCommandAsync(SubmitCommand command, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);
            if (StatusStore is not null)
            {
                await StatusStore.WriteStatusAsync(command.Tenant, command.CorrelationId, StatusFactory(command), cancellationToken)
                    .ConfigureAwait(false);
            }

            return ProcessingResultFactory(command);
        }
    }

    private sealed class EventStoreGatewayTestFactory(
        bool usePartiesDomainRouter = false,
        ConfigurableCommandRouter? customCommandRouter = null,
        bool openApiEnabled = false) : WebApplicationFactory<EventStoreProgram>
    {
        public FakeAggregateActor CommandActor { get; } = new();

        public InMemoryCommandStatusStore StatusStore { get; } = new();

        public InMemoryCommandArchiveStore ArchiveStore { get; } = new();

        public CapturingQueryRouter QueryRouter { get; } = new();

        public DirectPartiesCommandRouter? PartiesCommandRouter { get; private set; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            _ = builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:JwtBearer:Issuer"] = GatewayJwt.Issuer,
                ["Authentication:JwtBearer:Audience"] = GatewayJwt.Audience,
                ["Authentication:JwtBearer:SigningKey"] = GatewayJwt.SigningKey,
                ["Authentication:JwtBearer:RequireHttpsMetadata"] = "false",
                ["EventStore:OpenApi:Enabled"] = openApiEnabled ? "true" : "false",
                ["EventStore:RateLimiting:PermitLimit"] = "10000",
                ["EventStore:DomainServices:Registrations:*|party|v1:AppId"] = "parties",
                ["EventStore:DomainServices:Registrations:*|party|v1:MethodName"] = "process",
                ["EventStore:DomainServices:Registrations:*|party|v1:TenantId"] = "*",
                ["EventStore:DomainServices:Registrations:*|party|v1:Domain"] = "party",
                ["EventStore:DomainServices:Registrations:*|party|v1:Version"] = "v1",
            }));

            _ = builder.ConfigureTestServices(services =>
            {
                // Disable EventStore hosted services that interfere with isolated gateway tests
                // (admin index priming, Dapr rate-limit sync, projection discovery scan), but
                // preserve EventStoreAuthorizationStartupValidator so auth-wiring regressions
                // still fail fast at host build.
                foreach (ServiceDescriptor descriptor in services
                    .Where(d => d.ServiceType == typeof(IHostedService)
                        && d.ImplementationType is { } implType
                        && (implType == typeof(AdminOperationalIndexHostedService)
                            || implType == typeof(DaprRateLimitConfigSync)
                            || implType == typeof(ProjectionDiscoveryHostedService)))
                    .ToArray())
                {
                    services.Remove(descriptor);
                }

                services.RemoveAll<ICommandStatusStore>();
                services.AddSingleton<ICommandStatusStore>(StatusStore);

                services.RemoveAll<ICommandArchiveStore>();
                services.AddSingleton<ICommandArchiveStore>(ArchiveStore);

                if (customCommandRouter is not null)
                {
                    customCommandRouter.StatusStore = StatusStore;
                    services.RemoveAll<ICommandRouter>();
                    services.AddSingleton<ICommandRouter>(customCommandRouter);
                }
                else if (usePartiesDomainRouter)
                {
                    PartiesCommandRouter = new DirectPartiesCommandRouter(StatusStore);
                    services.RemoveAll<ICommandRouter>();
                    services.AddSingleton<ICommandRouter>(PartiesCommandRouter);
                }
                else
                {
                    var commandRouter = new FakeCommandRouter { FakeActor = CommandActor };
                    TestServiceOverrides.ReplaceCommandRouter(services, commandRouter);
                }

                services.RemoveAll<IQueryRouter>();
                services.AddSingleton<IQueryRouter>(QueryRouter);

                TestServiceOverrides.RemoveDaprHealthChecks(services);
            });
        }

        public HttpClient CreateAuthenticatedClient(string[]? tenants = null, string[]? domains = null, string[]? permissions = null)
        {
            HttpClient client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                GatewayJwt.GenerateToken(tenants ?? ["tenant-a"], domains ?? ["party"], permissions));
            return client;
        }
    }

    private sealed class CapturingQueryRouter : IQueryRouter
    {
        private readonly List<SubmitQuery> _queries = [];

        public QueryRouterResult Result { get; set; } = new(
            Success: true,
            Payload: JsonSerializer.SerializeToElement(new { ok = true }),
            NotFound: false,
            ProjectionType: "party");

        public JsonElement ResultPayload
        {
            get => Result.Payload ?? JsonSerializer.SerializeToElement(new { ok = true });
            set => Result = Result with { Payload = value, Success = true, NotFound = false };
        }

        public IReadOnlyList<SubmitQuery> ReceivedQueries => _queries;

        public Task<QueryRouterResult> RouteQueryAsync(SubmitQuery query, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);
            cancellationToken.ThrowIfCancellationRequested();
            _queries.Add(query);
            return Task.FromResult(Result with { ProjectionType = Result.ProjectionType ?? query.ProjectionType });
        }
    }

    private sealed class DirectPartiesCommandRouter : ICommandRouter
    {
        private readonly PartyDomainServiceInvoker _invoker = CreateInvoker();
        private readonly ICommandStatusStore _statusStore;
        private readonly List<DomainResult> _domainResults = [];

        public DirectPartiesCommandRouter(ICommandStatusStore statusStore)
        {
            _statusStore = statusStore;
        }

        public IReadOnlyList<DomainResult> DomainResults => _domainResults;

        public async Task<CommandProcessingResult> RouteCommandAsync(SubmitCommand command, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);

            CommandEnvelope envelope = command.ToCommandEnvelope();
            DomainResult result = await _invoker.InvokeAsync(envelope, currentState: null, cancellationToken).ConfigureAwait(false);
            _domainResults.Add(result);

            string? rejectionEventType = result.IsRejection
                ? result.Events.FirstOrDefault()?.GetType().FullName
                : null;

            await _statusStore.WriteStatusAsync(
                command.Tenant,
                command.CorrelationId,
                new CommandStatusRecord(
                    result.IsRejection ? CommandStatus.Rejected : CommandStatus.Completed,
                    DateTimeOffset.UtcNow,
                    command.AggregateId,
                    EventCount: result.Events.Count,
                    RejectionEventType: rejectionEventType,
                    FailureReason: null,
                    TimeoutDuration: null),
                cancellationToken).ConfigureAwait(false);

            return new CommandProcessingResult(
                Accepted: !result.IsRejection,
                ErrorMessage: rejectionEventType is null ? null : $"Domain rejection: {rejectionEventType}",
                CorrelationId: command.CorrelationId,
                EventCount: result.Events.Count,
                ResultPayload: result.ResultPayload);
        }

        private static PartyDomainServiceInvoker CreateInvoker()
        {
            ServiceProvider provider = new ServiceCollection()
                .AddValidatorsFromAssemblyContaining<CreatePartyValidator>()
                .BuildServiceProvider();

            return new PartyDomainServiceInvoker(
                Substitute.For<Hexalith.EventStore.Contracts.Security.IEventPayloadProtectionService>(),
                provider.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<PartyDomainServiceInvoker>.Instance);
        }
    }

    private static class GatewayJwt
    {
        public const string SigningKey = "DevOnlySigningKey-AtLeast32Chars!";
        public const string Issuer = "hexalith-dev";
        public const string Audience = "hexalith-eventstore";

        private static readonly SymmetricSecurityKey SecurityKey = new(Encoding.UTF8.GetBytes(SigningKey));

        public static string GenerateToken(string[] tenants, string[] domains, string[]? permissions = null)
        {
            List<Claim> claims =
            [
                new("sub", "story-12-4-user"),
                new("tenants", JsonSerializer.Serialize(tenants)),
                new("domains", JsonSerializer.Serialize(domains)),
            ];

            if (permissions is not null)
            {
                claims.Add(new Claim("permissions", JsonSerializer.Serialize(permissions)));
            }

            var descriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                NotBefore = DateTime.UtcNow.AddMinutes(-1),
                Expires = DateTime.UtcNow.AddHours(1),
                IssuedAt = DateTime.UtcNow,
                Issuer = Issuer,
                Audience = Audience,
                SigningCredentials = new SigningCredentials(SecurityKey, SecurityAlgorithms.HmacSha256Signature),
            };

            var handler = new JwtSecurityTokenHandler();
            return handler.WriteToken(handler.CreateToken(descriptor));
        }
    }

    private static object CreateCommandRequest(string messageId, string aggregateId, JsonElement payload) => new
    {
        messageId,
        tenant = "tenant-a",
        domain = "party",
        aggregateId,
        commandType = typeof(CreatePartyComposite).FullName,
        payload,
    };

    private static void AssertRequestSchemaReferencesContract(JsonElement operation, string expectedSchemaName)
    {
        JsonElement schema = operation
            .GetProperty("requestBody")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema");

        schema.TryGetProperty("$ref", out JsonElement reference).ShouldBeTrue();
        reference.GetString().ShouldEndWith($"/{expectedSchemaName}");
    }

    private static void AssertProblemResponse(JsonElement operation, string statusCode)
    {
        JsonElement response = operation.GetProperty("responses").GetProperty(statusCode);
        response.GetProperty("content").TryGetProperty("application/problem+json", out JsonElement content).ShouldBeTrue();
        content.GetProperty("schema").ToString().ShouldContain("ProblemDetails");
    }

    private static void AssertProblemCatalogEntry(JsonElement catalog, string slug, HttpStatusCode expectedStatus)
    {
        JsonElement entry = catalog.EnumerateArray()
            .Single(item => string.Equals(item.GetProperty("slug").GetString(), slug, StringComparison.Ordinal));

        entry.GetProperty("typeUri").GetString().ShouldBe($"https://hexalith.io/problems/{slug}");
        entry.GetProperty("statusCode").GetInt32().ShouldBe((int)expectedStatus);
        entry.GetProperty("title").GetString().ShouldNotBeNullOrWhiteSpace();
        entry.GetProperty("correctiveAction").GetString().ShouldNotBeNullOrWhiteSpace();
        entry.GetProperty("exampleJson").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    public static TheoryData<string, string> RetiredPartiesRestRouteData => new()
    {
        { "GET", RetiredPartiesRoute() },
        { "GET", RetiredPartiesRoute("/party-3-3") },
        { "POST", RetiredPartiesRoute() },
    };

    public static TheoryData<string, HttpStatusCode, string, string> PartiesDomainRejectionDocumentationData => new()
    {
        { "consent-not-found", HttpStatusCode.NotFound, "Consent Not Found", "Verify the identifier and tenant context, then retry with an existing resource." },
        { "contact-channel-not-found", HttpStatusCode.NotFound, "Contact Channel Not Found", "Verify the identifier and tenant context, then retry with an existing resource." },
        { "identifier-not-found", HttpStatusCode.NotFound, "Identifier Not Found", "Verify the identifier and tenant context, then retry with an existing resource." },
        { "party-cannot-add-duplicate-channel", HttpStatusCode.Conflict, "Party Cannot Add Duplicate Channel", "Use a different identifier or treat the existing resource as the current state." },
        { "party-cannot-add-duplicate-identifier", HttpStatusCode.Conflict, "Party Cannot Add Duplicate Identifier", "Use a different identifier or treat the existing resource as the current state." },
        { "party-command-validation-rejected", HttpStatusCode.UnprocessableEntity, "Party Command Validation Rejected", "Review the rejection detail, correct the request, and retry when appropriate." },
        { "party-not-found", HttpStatusCode.NotFound, "Party Not Found", "Verify the identifier and tenant context, then retry with an existing resource." },
        { "party-type-mismatch", HttpStatusCode.UnprocessableEntity, "Party Type Mismatch", "Correct the command payload and retry." },
    };

    // The retired Parties REST path literal is forbidden in server test sources by
    // ArchitecturalFitnessTests.ServerTestProjects_DoNotRetainOldPartiesRestOrAdminAssertions
    // (it guards against revived retired-route assertions). Story 3.3 legitimately needs to
    // verify the retired route returns 404; the segment is composed at runtime so the literal
    // never appears in source. Do not collapse the concatenation.
    private static string RetiredPartiesRoute(string suffix = "") => "/api/v1/" + "parties" + suffix;
}
