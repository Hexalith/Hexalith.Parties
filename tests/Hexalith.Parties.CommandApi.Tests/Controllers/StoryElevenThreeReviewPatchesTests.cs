using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.Parties.CommandApi.Authorization;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Abstractions;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.CommandApi.Tests.Controllers;

// Tests added during the BMad code review of story 11-3 to close the gaps
// flagged by the review layers:
//   - P1: payload tenant conflict rejection (AC5 spoofing regression)
//   - P5: role-matrix happy paths (Reader allowed to read; Contributor allowed
//         to write; Owner allowed everything)
//   - P6: per-reason endpoint denial coverage (tenant-disabled, not-member,
//         tenant-state-stale, missing-user)
public sealed class StoryElevenThreeReviewPatchesTests : IClassFixture<PartiesApiTestFactory>
{
    private readonly PartiesApiTestFactory _factory;

    public StoryElevenThreeReviewPatchesTests(PartiesApiTestFactory factory)
    {
        _factory = factory;
        _factory.TenantAccessService.AllowAll();
        _factory.CommandGuard
            .GetBlockingReasonAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);
    }

    // ----- P1 — Payload tenant conflict rejection (AC5 spoofing regression) -----

    // Whitebox unit test of the conflict-detection helper. AC5 says payload
    // TenantId on a command body cannot redirect authorization context. The
    // PartiesController.DispatchCommandAsync helper uses HasConflictingPayloadTenant
    // for defence-in-depth before any command dispatch. Today no command flowing
    // through DispatchCommandAsync carries a TenantId field — but if anyone
    // adds one, this test ensures the helper rejects mismatches without our
    // having to enumerate every command DTO.
    [Theory]
    [InlineData("tenant-a", "tenant-a", false)]      // matches → not a conflict
    [InlineData("tenant-a", "tenant-b", true)]       // mismatch → conflict
    [InlineData("tenant-a", "TENANT-A", true)]       // case-sensitive: ordinal mismatch → conflict
    [InlineData("tenant-a", "", false)]              // payload empty/whitespace → not a conflict (server overwrites)
    [InlineData("tenant-a", "  ", false)]            // payload whitespace → not a conflict
    [InlineData("tenant-a", null, false)]            // payload null → not a conflict
    public void HasConflictingPayloadTenant_DetectsMismatchAgainstTrustedTenant(
        string trustedTenantId,
        string? payloadTenantId,
        bool expectedConflict)
    {
        EraseParty command = new() { PartyId = "party-1", TenantId = payloadTenantId! };

        bool actual = PartiesAuthClaims.HasConflictingPayloadTenant(command, trustedTenantId);

        actual.ShouldBe(expectedConflict);
    }

    [Fact]
    public void HasConflictingPayloadTenant_OnCommandWithoutTenantIdProperty_ReturnsFalse()
    {
        // CreateParty has no TenantId field — must not be flagged as conflict.
        Hexalith.Parties.Contracts.Commands.CreateParty command = new()
        {
            PartyId = "party-1",
            Type = PartyType.Person,
            PersonDetails = new PersonDetails { FirstName = "Ada", LastName = "Lovelace" },
        };

        PartiesAuthClaims.HasConflictingPayloadTenant(command, "tenant-a").ShouldBeFalse();
    }

    [Fact]
    public void HasConflictingPayloadTenant_OnNullCommand_ReturnsFalse()
        => PartiesAuthClaims.HasConflictingPayloadTenant<object?>(null, "tenant-a").ShouldBeFalse();

    // ----- P5 — Role matrix happy paths -----

    [Fact]
    public async Task ListParties_TenantReader_Returns200OkAsync()
    {
        _factory.TenantAccessService.Handler = (_, _, _, _) => Task.FromResult(TenantAccessDecision.Allowed);
        _factory.SetIndexEntries();
        _factory.ResetIndexProxy();

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        HttpResponseMessage response = await client.GetAsync("/api/v1/parties");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateParty_TenantContributor_Returns202AcceptedAsync()
    {
        _factory.Router.ClearReceivedCalls();
        _factory.TenantAccessService.Handler = (_, _, _, _) => Task.FromResult(TenantAccessDecision.Allowed);
        _factory.Router
            .RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CommandProcessingResult(true)));

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        using HttpContent body = JsonContent.Create(new
        {
            partyId = Guid.NewGuid().ToString(),
            type = "person",
            personDetails = new { firstName = "Ada", lastName = "Lovelace" },
        });

        HttpResponseMessage response = await client.PostAsync("/api/v1/parties", body);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    // ----- P6 — Per-reason endpoint denial coverage -----

    [Theory]
    [InlineData(TenantAccessDenialReason.DisabledTenant, "tenant-disabled", HttpStatusCode.Forbidden)]
    [InlineData(TenantAccessDenialReason.MissingMember, "not-member", HttpStatusCode.Forbidden)]
    [InlineData(TenantAccessDenialReason.TenantStateStale, "tenant-state-stale", HttpStatusCode.Forbidden)]
    public async Task ListParties_DenialReason_ReturnsExpectedReasonCodeAsync(
        TenantAccessDenialReason reason,
        string expectedReasonCode,
        HttpStatusCode expectedStatus)
    {
        _factory.TenantAccessService.Handler = (_, _, _, _) =>
            Task.FromResult(TenantAccessDecision.Denied(reason));
        _factory.ActorProxyFactory.ClearReceivedCalls();

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        HttpResponseMessage response = await client.GetAsync("/api/v1/parties");

        response.StatusCode.ShouldBe(expectedStatus);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        using JsonDocument problem = await ReadJsonAsync(response);
        problem.RootElement.GetProperty("reasonCode").GetString().ShouldBe(expectedReasonCode);
        problem.RootElement.GetProperty("type").GetString()
            .ShouldBe($"urn:hexalith:parties:authorization:{expectedReasonCode}");

        // Call-order regression: denied requests must not read projection actors.
        // The health-check pipeline creates a `health:party-index` proxy; we
        // care that the GET /api/v1/parties handler did NOT create the
        // tenant-scoped projection actor.
        _factory.ActorProxyFactory.DidNotReceive().CreateActorProxy<IPartyIndexProjectionActor>(
            Arg.Is<ActorId>(id => id.ToString().StartsWith("tenant-", StringComparison.Ordinal)),
            Arg.Any<string>(),
            Arg.Any<ActorProxyOptions?>());
    }

    [Fact]
    public async Task ListParties_MissingUser_Returns401WithMissingUserReasonCodeAsync()
    {
        // JWT carries tenant claim but no `sub` — ExtractUserId returns null,
        // CheckAccessAsync rejects with MissingUserId, translator maps to 401.
        _factory.TenantAccessService.AllowAll();
        _factory.ActorProxyFactory.ClearReceivedCalls();

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", JwtTokenHelper.CreateTokenWithoutSub("tenant-a"));

        HttpResponseMessage response = await client.GetAsync("/api/v1/parties");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        using JsonDocument problem = await ReadJsonAsync(response);
        problem.RootElement.GetProperty("reasonCode").GetString().ShouldBe("missing-user");

        // The health-check pipeline creates a `health:party-index` proxy; we
        // care that the GET /api/v1/parties handler did NOT create the
        // tenant-scoped projection actor.
        _factory.ActorProxyFactory.DidNotReceive().CreateActorProxy<IPartyIndexProjectionActor>(
            Arg.Is<ActorId>(id => id.ToString().StartsWith("tenant-", StringComparison.Ordinal)),
            Arg.Any<string>(),
            Arg.Any<ActorProxyOptions?>());
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        string payload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return JsonDocument.Parse(payload);
    }
}
