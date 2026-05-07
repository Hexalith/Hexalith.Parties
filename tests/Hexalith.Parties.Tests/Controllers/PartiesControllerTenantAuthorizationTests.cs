// ATDD red-phase scaffolds for Story 11.4 — REST authorization-before-projection
// proof. Each test asserts that tenant access denial happens BEFORE projection
// reads or command routing, using NSubstitute Received(0) on the existing tracking
// doubles in PartiesApiTestFactory (ICommandRouter, IActorProxyFactory).

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Server.Commands;
using Hexalith.Parties.Authorization;
using Hexalith.Parties.Tests.Authorization;
using Hexalith.Parties.Projections.Abstractions;
using Hexalith.EventStore.Server.Pipeline.Commands;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.Tests.Controllers;

/// <summary>
/// Story 11.4 — AC2, AC5: REST denial responses must use Tenants-backed authorization
/// state and must not read projections or route commands when access is denied.
/// </summary>
[Collection(PartiesApiTestCollection.Name)]
public sealed class PartiesControllerTenantAuthorizationTests : IClassFixture<PartiesApiTestFactory>, IDisposable
{
    private readonly PartiesApiTestFactory _factory;

    public PartiesControllerTenantAuthorizationTests(PartiesApiTestFactory factory)
    {
        _factory = factory;
        _factory.Router.ClearReceivedCalls();
        _factory.ActorProxyFactory.ClearReceivedCalls();
        // Reset Handler at the start of every test — IClassFixture shares the factory
        // (and therefore the TenantAccessService instance) across all tests in this class.
        _factory.TenantAccessService.AllowAll();
    }

    public void Dispose()
    {
        // Restore the default allow-all handler so unrelated tests in other classes that
        // share the factory do not inherit a deny-state from this class's tests.
        _factory.TenantAccessService.AllowAll();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetParty_GivenDisabledTenant_Returns403_BeforeReadingProjectionAsync()
    {
        // Arrange — TestTenantAccessService configured to reflect a disabled tenant.
        _factory.TenantAccessService.Handler = (_, _, _, _) =>
            Task.FromResult(TenantAccessDecision.Denied(TenantAccessDenialReason.DisabledTenant));

        using HttpClient client = CreateClient(tenantId: "tenant-a");
        string partyId = Guid.NewGuid().ToString();

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/v1/parties/" + partyId);

        // Assert — denial via ProblemDetails with stable reasonCode.
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("status").GetInt32().ShouldBe(403);
        problem.RootElement.GetProperty("reasonCode").GetString().ShouldBe("tenant-disabled");

        // Critical: no projection actor was created for this denied request.
        _factory.ActorProxyFactory
            .DidNotReceive()
            .CreateActorProxy<IPartyDetailProjectionActor>(
                Arg.Is<ActorId>(id => string.Equals(id.GetId(), $"tenant-a:party-detail:{partyId}", StringComparison.Ordinal)),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>());

        // Critical: command router was never invoked for this denied request — proves
        // authorization fired before any command routing path could fire.
        await _factory.Router
            .DidNotReceive()
            .RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetParty_GivenContributorOnReadEndpoint_Returns404OrOkButNotForbiddenAsync()
    {
        // Positive control to prove auth enforcement does NOT block legitimate reads.
        // The projection is empty, so the controller will return 404. We pin to NotFound here
        // (rather than a generic ShouldNotBe Forbidden, which would also accept 200, 500, etc.)
        // so a regression that swallows the 404 path or returns a different status is detected.
        _factory.TenantAccessService.AllowAll();

        using HttpClient client = CreateClient(tenantId: "tenant-a");
        string partyId = Guid.NewGuid().ToString();
        _factory.SetIndexEntries(); // empty by default
        _factory.ResetIndexProxy();

        HttpResponseMessage response = await client.GetAsync("/api/v1/parties/" + partyId);

        // Authorized contributor + empty projection ⇒ 404 (not 403, not 500).
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateParty_GivenReader_Returns403_InsufficientRole_BeforeRoutingCommandAsync()
    {
        // Arrange — reader role must not be allowed to write.
        _factory.TenantAccessService.Handler = (_, _, requirement, _) =>
            Task.FromResult(requirement == TenantAccessRequirement.Read
                ? TenantAccessDecision.Allowed
                : TenantAccessDecision.Denied(TenantAccessDenialReason.InsufficientRole));

        using HttpClient client = CreateClient(tenantId: "tenant-a");
        using HttpContent body = JsonContent.Create(new
        {
            partyId = Guid.NewGuid().ToString(),
            type = "person",
            personDetails = new { firstName = "Ada", lastName = "Lovelace" },
        });

        // Act
        HttpResponseMessage response = await client.PostAsync("/api/v1/parties", body);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("reasonCode").GetString().ShouldBe("insufficient-role");

        // Critical: command router was never invoked.
        await _factory.Router
            .DidNotReceive()
            .RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetParty_GivenStaleProjection_Returns403_TenantStateStaleAsync()
    {
        _factory.TenantAccessService.Handler = (_, _, _, _) =>
            Task.FromResult(TenantAccessDecision.Denied(
                TenantAccessDenialReason.TenantStateStale,
                diagnosticText: "Tenant access state is unavailable."));

        using HttpClient client = CreateClient(tenantId: "tenant-a");

        HttpResponseMessage response = await client.GetAsync("/api/v1/parties/" + Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("reasonCode").GetString().ShouldBe("tenant-state-stale");
    }

    [Fact]
    public async Task GetParty_DenialResponse_HasProblemJsonAndCorrelationIdAsync()
    {
        _factory.TenantAccessService.Handler = (_, _, _, _) =>
            Task.FromResult(TenantAccessDecision.Denied(TenantAccessDenialReason.MissingMember));

        using HttpClient client = CreateClient(tenantId: "tenant-a");
        const string correlationId = "corr-tenant-auth-001";
        client.DefaultRequestHeaders.Add("X-Correlation-Id", correlationId);

        HttpResponseMessage response = await client.GetAsync("/api/v1/parties/" + Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("type").GetString().ShouldStartWith("urn:hexalith:parties:authorization:");
        problem.RootElement.GetProperty("correlationId").GetString().ShouldNotBeNullOrWhiteSpace();
        problem.RootElement.GetProperty("reasonCode").GetString().ShouldBe("not-member");
    }

    private HttpClient CreateClient(string tenantId)
    {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(tenantId));
        return client;
    }
}
