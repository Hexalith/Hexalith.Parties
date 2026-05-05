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
using Hexalith.Parties.CommandApi.Authorization;
using Hexalith.Parties.CommandApi.Tests.Authorization;
using Hexalith.Parties.Projections.Abstractions;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.CommandApi.Tests.Controllers;

/// <summary>
/// Story 11.4 — AC2, AC5: REST denial responses must use Tenants-backed authorization
/// state and must not read projections or route commands when access is denied.
/// </summary>
public sealed class PartiesControllerTenantAuthorizationTests : IClassFixture<PartiesApiTestFactory>
{
    private const string SkipReason =
        "TDD red phase — Story 11.4 must surface Tenants-backed denial reasons (tenant-disabled, " +
        "insufficient-role, tenant-state-stale) through ProblemDetails AND prove that no projection " +
        "actor or command-router invocation occurs on a denied request.";

    private readonly PartiesApiTestFactory _factory;

    public PartiesControllerTenantAuthorizationTests(PartiesApiTestFactory factory)
    {
        _factory = factory;
        _factory.Router.ClearReceivedCalls();
        _factory.ActorProxyFactory.ClearReceivedCalls();
    }

    [Fact(Skip = SkipReason)]
    public async Task GetParty_GivenDisabledTenant_Returns403_BeforeReadingProjectionAsync()
    {
        // Arrange — TestTenantAccessService configured to reflect a disabled tenant.
        _factory.TenantAccessService.Handler = (_, _, _, _) =>
            Task.FromResult(TenantAccessDecision.Denied(TenantAccessDenialReason.DisabledTenant));

        using HttpClient client = CreateClient(tenantId: "tenant-a");

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/v1/parties/" + Guid.NewGuid());

        // Assert — denial via ProblemDetails with stable reasonCode.
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("status").GetInt32().ShouldBe(403);
        problem.RootElement.GetProperty("extensions").GetProperty("reasonCode").GetString().ShouldBe("tenant-disabled");

        // Critical: no projection actor was created for this denied request.
        _factory.ActorProxyFactory
            .DidNotReceive()
            .CreateActorProxy<IPartyDetailProjectionActor>(Arg.Any<ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions?>());
    }

    [Fact(Skip = SkipReason)]
    public async Task GetParty_GivenContributorOnReadEndpoint_Returns200Async()
    {
        // Positive control to prove auth enforcement does NOT block legitimate reads.
        _factory.TenantAccessService.AllowAll();

        using HttpClient client = CreateClient(tenantId: "tenant-a");
        string partyId = Guid.NewGuid().ToString();
        _factory.SetIndexEntries(); // empty by default
        _factory.ResetIndexProxy();

        HttpResponseMessage response = await client.GetAsync("/api/v1/parties/" + partyId);

        // Either 200 with body or 404 when projection has no record — but never 403 for an authorized contributor.
        response.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden);
    }

    [Fact(Skip = SkipReason)]
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
        problem.RootElement.GetProperty("extensions").GetProperty("reasonCode").GetString().ShouldBe("insufficient-role");

        // Critical: command router was never invoked.
        await _factory.Router
            .DidNotReceive()
            .RouteAsync(Arg.Any<CommandEnvelope>(), Arg.Any<CancellationToken>());
    }

    [Fact(Skip = SkipReason)]
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
        problem.RootElement.GetProperty("extensions").GetProperty("reasonCode").GetString().ShouldBe("tenant-state-stale");
    }

    [Fact(Skip = SkipReason)]
    public async Task AdminEndpoint_GivenContributor_Returns403_InsufficientRoleAsync()
    {
        // Admin endpoints require TenantOwner; contributors must be denied.
        _factory.TenantAccessService.Handler = (_, _, requirement, _) =>
            Task.FromResult(requirement == TenantAccessRequirement.Admin
                ? TenantAccessDecision.Denied(TenantAccessDenialReason.InsufficientRole)
                : TenantAccessDecision.Allowed);

        using HttpClient client = CreateClient(tenantId: "tenant-a");

        HttpResponseMessage response = await client.GetAsync("/api/v1/admin/keys/status");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("extensions").GetProperty("reasonCode").GetString().ShouldBe("insufficient-role");
    }

    [Fact(Skip = SkipReason)]
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
        problem.RootElement.GetProperty("extensions").GetProperty("correlationId").GetString().ShouldBe(correlationId);
        problem.RootElement.GetProperty("extensions").GetProperty("reasonCode").GetString().ShouldBe("not-member");
    }

    private HttpClient CreateClient(string tenantId)
    {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(tenantId));
        return client;
    }
}
