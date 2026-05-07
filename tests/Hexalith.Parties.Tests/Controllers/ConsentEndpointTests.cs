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
using Hexalith.Parties.Contracts.Commands;
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

namespace Hexalith.Parties.Tests.Controllers;

public sealed class ConsentEndpointTests : IClassFixture<ConsentEndpointTests.ConsentTestFactory> {
    private const string ConsentEndpoint = "/api/v1/admin/parties/{0}/consent";
    private const string RevokeConsentEndpoint = "/api/v1/admin/parties/{0}/consent/{1}";

    private readonly ConsentTestFactory _factory;

    public ConsentEndpointTests(ConsentTestFactory factory) {
        _factory = factory;
    }

    // === Task 8.1: Auth tests for POST consent ===

    [Fact]
    public async Task RecordConsent_WithoutToken_Returns401Async() {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            string.Format(ConsentEndpoint, "party-1"),
            new { channelId = "ch-1", purpose = "marketing", lawfulBasis = 0 });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RecordConsent_WithRegularUserToken_Returns403Async() {
        using HttpClient client = CreateRegularUserClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            string.Format(ConsentEndpoint, "party-1"),
            new { channelId = "ch-1", purpose = "marketing", lawfulBasis = 0 });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RecordConsent_WithAdminToken_Returns200Async() {
        using HttpClient client = CreateAdminClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            string.Format(ConsentEndpoint, "party-1"),
            new { channelId = "ch-1", purpose = "marketing", lawfulBasis = 0 });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonDocument payload = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        payload.RootElement.TryGetProperty("correlationId", out JsonElement cid).ShouldBeTrue();
        cid.GetString().ShouldNotBeNullOrWhiteSpace();
    }

    // === Task 8.2: DELETE consent/{id} revokes consent ===

    [Fact]
    public async Task RevokeConsent_WithAdminToken_Returns200Async() {
        using HttpClient client = CreateAdminClient();

        HttpResponseMessage response = await client.DeleteAsync(
            string.Format(RevokeConsentEndpoint, "party-1", "ch-1:marketing"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonDocument payload = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        payload.RootElement.TryGetProperty("correlationId", out JsonElement cid).ShouldBeTrue();
        cid.GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RevokeConsent_WithoutToken_Returns401Async() {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.DeleteAsync(
            string.Format(RevokeConsentEndpoint, "party-1", "ch-1:marketing"));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // === Task 8.3: GET consent returns all consent records ===

    [Fact]
    public async Task GetConsent_WithAdminToken_ReturnsConsentRecordsAsync() {
        // Create client first to ensure ConfigureWebHost has run (lazy init)
        using HttpClient client = CreateAdminClient();

        // Configure detail proxy AFTER factory initialization
        PartyDetail detailWithConsent = new() {
            Id = "party-consent",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Test User",
            SortName = "User, Test",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-10),
            LastModifiedAt = DateTimeOffset.UtcNow,
            ConsentRecords = [
                new ConsentRecord {
                    ConsentId = "ch-1:marketing",
                    ChannelId = "ch-1",
                    Purpose = "marketing",
                    LawfulBasis = LawfulBasis.Consent,
                    GrantedAt = DateTimeOffset.UtcNow.AddDays(-5),
                    GrantedBy = "admin",
                },
                new ConsentRecord {
                    ConsentId = "ch-1:billing",
                    ChannelId = "ch-1",
                    Purpose = "billing",
                    LawfulBasis = LawfulBasis.ContractualNecessity,
                    GrantedAt = DateTimeOffset.UtcNow.AddDays(-3),
                    GrantedBy = "admin",
                    RevokedAt = DateTimeOffset.UtcNow.AddDays(-1),
                    RevokedBy = "admin",
                },
            ],
        };
        _factory.DetailProxy.GetDetailAsync().Returns(Task.FromResult<PartyDetail?>(detailWithConsent));

        try {
            HttpResponseMessage response = await client.GetAsync(
                string.Format(ConsentEndpoint, "party-consent"));

            response.StatusCode.ShouldBe(HttpStatusCode.OK);

            JsonDocument payload = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync());
            JsonElement records = payload.RootElement;
            records.GetArrayLength().ShouldBe(2);

            // First record: active consent
            records[0].GetProperty("consentId").GetString().ShouldBe("ch-1:marketing");
            records[0].GetProperty("purpose").GetString().ShouldBe("marketing");

            // Second record: revoked consent
            records[1].GetProperty("consentId").GetString().ShouldBe("ch-1:billing");
            records[1].TryGetProperty("revokedAt", out _).ShouldBeTrue();
        }
        finally {
            // Reset to default
            _factory.DetailProxy.GetDetailAsync().Returns(Task.FromResult<PartyDetail?>(null));
        }
    }

    [Fact]
    public async Task GetConsent_PartyNotFound_Returns404Async() {
        _factory.DetailProxy.GetDetailAsync().Returns(Task.FromResult<PartyDetail?>(null));

        using HttpClient client = CreateAdminClient();

        HttpResponseMessage response = await client.GetAsync(
            string.Format(ConsentEndpoint, "nonexistent-party"));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RecordConsent_CommandRejected_Returns422Async() {
        // Configure command router to reject the consent recording
        _factory.CommandRouter.RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CommandProcessingResult(false) {
                ErrorMessage = "Domain rejection: Hexalith.Parties.Contracts.Events.ContactChannelNotFound",
            }));

        try {
            using HttpClient client = CreateAdminClient();

            HttpResponseMessage response = await client.PostAsJsonAsync(
                string.Format(ConsentEndpoint, "party-1"),
                new { channelId = "nonexistent", purpose = "marketing", lawfulBasis = 0 });

            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        }
        finally {
            // Reset to default behavior
            _factory.CommandRouter.RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(new CommandProcessingResult(true)));
        }
    }

    [Fact]
    public async Task RecordConsent_WithAdminToken_PropagatesJwtSubjectIntoCommandPayloadAsync() {
        using HttpClient client = CreateAdminClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            string.Format(ConsentEndpoint, "party-1"),
            new { channelId = "ch-1", purpose = "marketing", lawfulBasis = 0 });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        SubmitCommand? submit = _factory.CommandRouter.ReceivedCalls()
            .Select(call => call.GetArguments().FirstOrDefault())
            .OfType<SubmitCommand>()
            .LastOrDefault(c => c.CommandType == nameof(RecordConsent));

        submit.ShouldNotBeNull();

        Hexalith.Parties.Contracts.Commands.RecordConsent? command = JsonSerializer.Deserialize<Hexalith.Parties.Contracts.Commands.RecordConsent>(submit.Payload);
        command.ShouldNotBeNull();
        command.ActorUserId.ShouldBe("test-admin");
    }

    // --- Helper methods ---

    private HttpClient CreateAdminClient() {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken(includeAdminRole: true));
        return client;
    }

    private HttpClient CreateRegularUserClient() {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken(includeAdminRole: false));
        return client;
    }

    private static string CreateToken(bool includeAdminRole) {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ConsentTestFactory.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new("sub", "test-admin"),
            new("eventstore:tenant", "tenant-a"),
        };

        if (includeAdminRole) {
            claims.Add(new Claim(ClaimTypes.Role, "admin"));
        }

        var token = new JwtSecurityToken(
            issuer: ConsentTestFactory.Issuer,
            audience: ConsentTestFactory.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public sealed class ConsentTestFactory : WebApplicationFactory<Program> {
        internal const string Issuer = "hexalith-dev";
        internal const string Audience = "hexalith-parties";
        internal const string SigningKey = "DevOnlySigningKey-AtLeast32Chars-MustBeSecure!";

        internal ICommandRouter CommandRouter { get; } = Substitute.For<ICommandRouter>();
        internal IProjectionRebuildService ProjectionRebuildService { get; } = Substitute.For<IProjectionRebuildService>();
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
                services.AddSingleton(ProjectionRebuildService);
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
                services.RemoveAll<Hexalith.Parties.Authorization.ITenantAccessService>();
                services.AddSingleton<Hexalith.Parties.Authorization.ITenantAccessService, Hexalith.Parties.Tests.Authorization.TestTenantAccessService>();
            });
        }
    }
}
