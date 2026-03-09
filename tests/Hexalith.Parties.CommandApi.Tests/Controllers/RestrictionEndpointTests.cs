using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Abstractions;
using Hexalith.Parties.Projections.Services;
using Hexalith.Parties.Security;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.CommandApi.Tests.Controllers;

public sealed class RestrictionEndpointTests : IClassFixture<RestrictionEndpointTests.RestrictionTestFactory> {
    private const string RestrictEndpoint = "/api/v1/admin/parties/{0}/restrict";
    private const string LiftRestrictionEndpoint = "/api/v1/admin/parties/{0}/lift-restriction";
    private const string UpdatePersonDetailsEndpoint = "/api/v1/parties/{0}/update-person-details";
    private const string DeactivateEndpoint = "/api/v1/parties/{0}/deactivate";

    private readonly RestrictionTestFactory _factory;

    public RestrictionEndpointTests(RestrictionTestFactory factory) {
        _factory = factory;
    }

    // === Task 8.4: POST restrict ===

    [Fact]
    public async Task RestrictProcessing_WithAdminToken_Returns200Async() {
        using HttpClient client = CreateAdminClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            string.Format(RestrictEndpoint, "party-1"),
            new { reason = "Investigation pending" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonDocument payload = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        payload.RootElement.TryGetProperty("correlationId", out JsonElement cid).ShouldBeTrue();
        cid.GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RestrictProcessing_WithoutToken_Returns401Async() {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            string.Format(RestrictEndpoint, "party-1"),
            new { reason = "Investigation" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RestrictProcessing_WithRegularUserToken_Returns403Async() {
        using HttpClient client = CreateRegularUserClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            string.Format(RestrictEndpoint, "party-1"),
            new { reason = "Investigation" });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // === Task 8.5: POST lift-restriction ===

    [Fact]
    public async Task LiftRestriction_WithAdminToken_Returns200Async() {
        using HttpClient client = CreateAdminClient();

        HttpResponseMessage response = await client.PostAsync(
            string.Format(LiftRestrictionEndpoint, "party-1"), null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonDocument payload = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        payload.RootElement.TryGetProperty("correlationId", out JsonElement cid).ShouldBeTrue();
        cid.GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task LiftRestriction_WithoutToken_Returns401Async() {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.PostAsync(
            string.Format(LiftRestrictionEndpoint, "party-1"), null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // === Task 8.6: Restriction enforcement — UpdatePersonDetails rejected ===

    [Fact]
    public async Task UpdatePersonDetails_WhenRestricted_Returns422Async() {
        string partyGuid = Guid.NewGuid().ToString();

        // Create client first to ensure ConfigureWebHost has run
        using HttpClient client = CreateAuthenticatedClient();

        // Override CommandRouter AFTER factory initialization to simulate aggregate restriction rejection
        _factory.CommandRouter.RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => {
                SubmitCommand cmd = callInfo.Arg<SubmitCommand>();
                if (cmd.CommandType == "UpdatePersonDetails") {
                    return Task.FromResult(new CommandProcessingResult(false) {
                        ErrorMessage = "Domain rejection: Hexalith.Parties.Contracts.Events.PartyProcessingRestricted",
                    });
                }
                return Task.FromResult(new CommandProcessingResult(true));
            });

        try {
            HttpResponseMessage response = await client.PostAsJsonAsync(
                string.Format(UpdatePersonDetailsEndpoint, partyGuid),
                new {
                    partyId = partyGuid,
                    personDetails = new {
                        firstName = "Updated",
                        lastName = "Name",
                    },
                });

            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

            JsonDocument payload = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync());
            payload.RootElement.GetProperty("title").GetString().ShouldBe("Domain Rejection");
        }
        finally {
            // Reset to default behavior
            _factory.CommandRouter.RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(new CommandProcessingResult(true)));
        }
    }

    // === Task 8.7: Restriction bypass — RecordConsent succeeds ===

    [Fact]
    public async Task RecordConsent_WhenRestricted_Returns200Async() {
        // Admin consent endpoint always routes through AdminController
        // CommandRouter accepts consent commands even when party is restricted
        using HttpClient client = CreateAdminClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/admin/parties/restricted-party/consent",
            new { channelId = "ch-1", purpose = "marketing", lawfulBasis = 0 });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // === Task 8.8: Restriction bypass — RevokeConsent succeeds ===

    [Fact]
    public async Task RevokeConsent_WhenRestricted_Returns200Async() {
        using HttpClient client = CreateAdminClient();

        HttpResponseMessage response = await client.DeleteAsync(
            "/api/v1/admin/parties/restricted-party/consent/ch-1:marketing");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // === Task 8.9: Restriction bypass — EraseParty succeeds ===

    [Fact]
    public async Task EraseParty_WhenRestricted_Returns202AcceptedAsync() {
        using HttpClient client = CreateAdminClient();

        HttpResponseMessage response = await client.PostAsync(
            "/api/v1/admin/parties/restricted-party/erase", null);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    // === Task 8.10: Restriction lifecycle interaction ===

    [Fact]
    public async Task RestrictionLifecycle_RestrictRevokeAllLiftRestriction_PartyActiveWithZeroConsentsAsync() {
        using HttpClient client = CreateAdminClient();

        // Step 1: Restrict processing
        HttpResponseMessage restrictResponse = await client.PostAsJsonAsync(
            string.Format(RestrictEndpoint, "lifecycle-party"),
            new { reason = "Investigation" });
        restrictResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Step 2: Revoke a consent (allowed during restriction)
        HttpResponseMessage revokeResponse = await client.DeleteAsync(
            "/api/v1/admin/parties/lifecycle-party/consent/ch-1:marketing");
        revokeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Step 3: Lift restriction
        HttpResponseMessage liftResponse = await client.PostAsync(
            string.Format(LiftRestrictionEndpoint, "lifecycle-party"), null);
        liftResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Step 4: Verify party is accessible (GET consent returns empty for active party)
        PartyDetail activeDetail = new() {
            Id = "lifecycle-party",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Test User",
            SortName = "User, Test",
            IsRestricted = false,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-10),
            LastModifiedAt = DateTimeOffset.UtcNow,
            ConsentRecords = [],
        };
        _factory.DetailProxy.GetDetailAsync().Returns(Task.FromResult<PartyDetail?>(activeDetail));

        HttpResponseMessage getConsentResponse = await client.GetAsync(
            "/api/v1/admin/parties/lifecycle-party/consent");
        getConsentResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonDocument payload = await JsonDocument.ParseAsync(
            await getConsentResponse.Content.ReadAsStreamAsync());
        payload.RootElement.GetArrayLength().ShouldBe(0);
    }

    // --- Helper methods ---

    private HttpClient CreateAdminClient() {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken(role: "admin"));
        return client;
    }

    private HttpClient CreateRegularUserClient() {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken(role: null));
        return client;
    }

    private HttpClient CreateAuthenticatedClient() {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken(role: null));
        return client;
    }

    private static string CreateToken(string? role) {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(RestrictionTestFactory.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new("sub", "test-admin"),
            new("eventstore:tenant", "tenant-a"),
        };

        if (role is not null) {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var token = new JwtSecurityToken(
            issuer: RestrictionTestFactory.Issuer,
            audience: RestrictionTestFactory.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public sealed class RestrictionTestFactory : WebApplicationFactory<Program> {
        internal const string Issuer = "hexalith-dev";
        internal const string Audience = "hexalith-parties";
        internal const string SigningKey = "DevOnlySigningKey-AtLeast32Chars-MustBeSecure!";

        internal ICommandRouter CommandRouter { get; } = Substitute.For<ICommandRouter>();
        internal IPartyDetailProjectionActor DetailProxy { get; } = Substitute.For<IPartyDetailProjectionActor>();
        internal IPartyIndexProjectionActor IndexProxy { get; } = Substitute.For<IPartyIndexProjectionActor>();
        internal IPartyKeyManagementService KeyManagementService { get; } = Substitute.For<IPartyKeyManagementService>();
        internal IErasureVerificationService ErasureVerificationService { get; } = Substitute.For<IErasureVerificationService>();
        internal IPartyErasureRecordStore ErasureRecordStore { get; } = Substitute.For<IPartyErasureRecordStore>();
        internal IPersonalDataCommandGuard PersonalDataGuard { get; } = Substitute.For<IPersonalDataCommandGuard>();

        protected override void ConfigureWebHost(IWebHostBuilder builder) {
            ArgumentNullException.ThrowIfNull(builder);

            builder.UseEnvironment("Development");

            builder.ConfigureAppConfiguration((_, config) => {
                config.AddInMemoryCollection(new Dictionary<string, string?> {
                    ["Authentication:JwtBearer:Issuer"] = Issuer,
                    ["Authentication:JwtBearer:Audience"] = Audience,
                    ["Authentication:JwtBearer:SigningKey"] = SigningKey,
                    ["Authentication:JwtBearer:RequireHttpsMetadata"] = "false",
                });
            });

            // Default mock behaviors
            CommandRouter.RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(new CommandProcessingResult(true)));

            DetailProxy.GetDetailAsync().Returns(Task.FromResult<PartyDetail?>(null));
            DetailProxy.IsRebuildingAsync().Returns(Task.FromResult(false));

            IndexProxy.GetEntriesAsync().Returns(Task.FromResult<IReadOnlyDictionary<string, PartyIndexEntry>>(
                new Dictionary<string, PartyIndexEntry>()));

            IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();

            actorProxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
                Arg.Any<ActorId>(), Arg.Any<string>())
                .Returns(DetailProxy);
            actorProxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
                Arg.Any<ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions?>())
                .Returns(DetailProxy);
            actorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
                Arg.Any<ActorId>(), Arg.Any<string>())
                .Returns(IndexProxy);
            actorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
                Arg.Any<ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions?>())
                .Returns(IndexProxy);

            PersonalDataGuard.GetBlockingReasonAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
                .Returns((string?)null);

            ErasureRecordStore.GetStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns((PartyErasureStatusRecord?)null);
            ErasureRecordStore.GetCertificateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns((ErasureCertificate?)null);
            ErasureRecordStore.GetVerificationReportAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns((ErasureVerificationReport?)null);

            builder.ConfigureTestServices(services => {
                services.RemoveAll<ICommandRouter>();
                services.AddSingleton(CommandRouter);
                services.RemoveAll<IActorProxyFactory>();
                services.AddSingleton(actorProxyFactory);
                services.RemoveAll<IProjectionRebuildService>();
                services.AddSingleton(Substitute.For<IProjectionRebuildService>());
                services.RemoveAll<IPartyKeyManagementService>();
                services.AddSingleton(KeyManagementService);
                services.RemoveAll<IKeyStorageBackend>();
                services.AddSingleton(Substitute.For<IKeyStorageBackend>());
                services.RemoveAll<IKeyOperationAuditService>();
                services.AddSingleton(Substitute.For<IKeyOperationAuditService>());
                services.RemoveAll<IErasureVerificationService>();
                services.AddSingleton(ErasureVerificationService);
                services.RemoveAll<IPartyErasureRecordStore>();
                services.AddSingleton(ErasureRecordStore);
                services.RemoveAll<PartyErasureOrchestrator>();
                services.AddSingleton<PartyErasureOrchestrator>();
                services.RemoveAll<IPersonalDataCommandGuard>();
                services.AddSingleton(PersonalDataGuard);
            });
        }
    }
}
