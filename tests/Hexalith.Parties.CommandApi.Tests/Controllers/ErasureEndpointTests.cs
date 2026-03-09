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
using Hexalith.Parties.Contracts.Commands;
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

public sealed class ErasureEndpointTests : IClassFixture<ErasureEndpointTests.ErasureTestFactory> {
    private const string EraseEndpoint = "/api/v1/admin/parties/{0}/erase";
    private const string ErasureStatusEndpoint = "/api/v1/admin/parties/{0}/erasure-status";
    private const string ErasureCertificateEndpoint = "/api/v1/admin/parties/{0}/erasure-certificate";
    private const string RetryVerificationEndpoint = "/api/v1/admin/parties/{0}/retry-verification";
    private const string GetPartyEndpoint = "/api/v1/parties/{0}";
    private const string SearchEndpoint = "/api/v1/parties/search?q={0}";
    private const string ListEndpoint = "/api/v1/parties";

    private readonly ErasureTestFactory _factory;

    public ErasureEndpointTests(ErasureTestFactory factory) {
        _factory = factory;
    }

    // --- 9.1: Admin endpoint auth tests ---

    [Fact]
    public async Task Erase_WithoutToken_Returns401Async() {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.PostAsync(
            string.Format(EraseEndpoint, "party-1"), null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Erase_WithRegularUserToken_Returns403Async() {
        using HttpClient client = CreateRegularUserClient();

        HttpResponseMessage response = await client.PostAsync(
            string.Format(EraseEndpoint, "party-1"), null);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Erase_WithAdminToken_Returns202AcceptedAsync() {
        using HttpClient client = CreateAdminClient();

        HttpResponseMessage response = await client.PostAsync(
            string.Format(EraseEndpoint, "party-1"), null);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        JsonDocument payload = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        payload.RootElement.TryGetProperty("correlationId", out JsonElement cid).ShouldBeTrue();
        cid.GetString().ShouldNotBeNullOrWhiteSpace();
    }

    // --- 9.2: Erasure status endpoint ---

    [Fact]
    public async Task ErasureStatus_WithAdminToken_ReturnsStatusResponseAsync() {
        _factory.ErasureRecordStore.GetStatusAsync("tenant-a", "party-1", Arg.Any<CancellationToken>())
            .Returns(new PartyErasureStatusRecord {
                PartyId = "party-1",
                TenantId = "tenant-a",
                Status = ErasureStatus.KeyDestroyed.ToString(),
                UpdatedAt = DateTimeOffset.UtcNow,
            });

        using HttpClient client = CreateAdminClient();

        HttpResponseMessage response = await client.GetAsync(
            string.Format(ErasureStatusEndpoint, "party-1"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonDocument payload = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        payload.RootElement.GetProperty("partyId").GetString().ShouldBe("party-1");
        payload.RootElement.GetProperty("tenantId").GetString().ShouldBe("tenant-a");
        payload.RootElement.GetProperty("status").GetString().ShouldBe(ErasureStatus.KeyDestroyed.ToString());
    }

    // --- 9.3: Erasure certificate endpoint ---

    [Fact]
    public async Task ErasureCertificate_WithStoredArtifacts_Returns200Async() {
        _factory.ErasureRecordStore.GetCertificateAsync("tenant-a", "party-1", Arg.Any<CancellationToken>())
            .Returns(new ErasureCertificate {
                PartyId = "party-1",
                TenantId = "tenant-a",
                Timestamp = DateTimeOffset.UtcNow,
                KeyVersionsDestroyed = [1, 2],
                VerificationStatus = ErasureVerificationStatus.Verified,
            });
        _factory.ErasureRecordStore.GetVerificationReportAsync("tenant-a", "party-1", Arg.Any<CancellationToken>())
            .Returns(new ErasureVerificationReport {
                PartyId = "party-1",
                TenantId = "tenant-a",
                Timestamp = DateTimeOffset.UtcNow,
                OverallStatus = ErasureVerificationOverallStatus.Complete,
                StoreResults =
                [
                    new ErasureVerificationStoreResult
                    {
                        StoreName = "detail-projection",
                        Status = ErasureStoreCleanupStatus.Cleaned,
                        Timestamp = DateTimeOffset.UtcNow,
                    },
                ],
            });

        using HttpClient client = CreateAdminClient();

        HttpResponseMessage response = await client.GetAsync(
            string.Format(ErasureCertificateEndpoint, "party-1"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonDocument payload = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        payload.RootElement.GetProperty("certificate").GetProperty("partyId").GetString().ShouldBe("party-1");
        payload.RootElement.GetProperty("verificationReport").GetProperty("overallStatus").GetString().ShouldBe("Complete");
    }

    // --- 9.4: Read sweep — GET /party/{id} returns erased status ---

    [Fact]
    public async Task GetParty_ErasedParty_Returns410GoneAsync() {
        // Configure the detail proxy to return an erased party
        PartyDetail erasedDetail = new() {
            Id = "erased-party",
            Type = PartyType.Person,
            IsActive = false,
            DisplayName = string.Empty,
            SortName = string.Empty,
            IsErased = true,
            ErasedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-30),
            LastModifiedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
        };
        _factory.DetailProxy.GetDetailAsync().Returns(Task.FromResult<PartyDetail?>(erasedDetail));
        _factory.DetailProxy.IsRebuildingAsync().Returns(Task.FromResult(false));

        using HttpClient client = CreateAdminClient();

        HttpResponseMessage response = await client.GetAsync(
            string.Format(GetPartyEndpoint, "erased-party"));

        response.StatusCode.ShouldBe(HttpStatusCode.Gone);

        JsonDocument payload = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        payload.RootElement.GetProperty("title").GetString().ShouldBe("Party Erased");
    }

    // --- 9.5: Read sweep — search excludes erased party ---

    [Fact]
    public async Task SearchParties_ErasedPartyInIndex_ExcludedFromResultsAsync() {
        // Configure index proxy to include both active and erased entries
        _factory.IndexProxy.GetEntriesAsync().Returns(Task.FromResult<IReadOnlyDictionary<string, PartyIndexEntry>>(
            new Dictionary<string, PartyIndexEntry> {
                ["active-party"] = new PartyIndexEntry {
                    Id = "active-party",
                    Type = PartyType.Person,
                    IsActive = true,
                    DisplayName = "Active User",
                    SearchableContactChannels = [],
                    SearchableIdentifiers = [],
                    CreatedAt = DateTimeOffset.UtcNow,
                    LastModifiedAt = DateTimeOffset.UtcNow,
                },
                ["erased-party"] = new PartyIndexEntry {
                    Id = "erased-party",
                    Type = PartyType.Person,
                    IsActive = false,
                    DisplayName = string.Empty,
                    IsErased = true,
                    SearchableContactChannels = [],
                    SearchableIdentifiers = [],
                    CreatedAt = DateTimeOffset.UtcNow.AddDays(-30),
                    LastModifiedAt = DateTimeOffset.UtcNow,
                },
            }));

        using HttpClient client = CreateAdminClient();

        HttpResponseMessage response = await client.GetAsync(ListEndpoint);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonDocument payload = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        JsonElement items = payload.RootElement.GetProperty("items");
        items.GetArrayLength().ShouldBe(1);
        items[0].GetProperty("id").GetString().ShouldBe("active-party");
    }

    // --- 9.8: Concurrent erasure — two requests, both return 202 (idempotent) ---

    [Fact]
    public async Task Erase_TwoConcurrentRequests_BothReturn202Async() {
        using HttpClient client = CreateAdminClient();
        string endpoint = string.Format(EraseEndpoint, "concurrent-party");

        Task<HttpResponseMessage> request1 = client.PostAsync(endpoint, null);
        Task<HttpResponseMessage> request2 = client.PostAsync(endpoint, null);

        HttpResponseMessage[] responses = await Task.WhenAll(request1, request2);

        responses[0].StatusCode.ShouldBe(HttpStatusCode.Accepted);
        responses[1].StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    // --- 9.9: Command rejection for erased/pending party ---

    [Fact]
    public async Task DeactivateCommand_WhenErasurePending_ReturnsUnprocessableEntityAsync() {
        // Set guard to block commands (simulates erasure-pending state
        // where the guard prevents modifications before they reach the command router)
        _factory.GuardBlockingReason = "Party erasure in progress. No modifications allowed.";

        try {
            using HttpClient client = CreateAdminClient();

            string partyId = Guid.NewGuid().ToString();
            HttpResponseMessage response = await client.PostAsync(
                $"/api/v1/parties/{partyId}/deactivate", null);

            // The guard blocks → endpoint returns 422 with personal data write blocked
            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        }
        finally {
            _factory.GuardBlockingReason = null;
        }
    }

    // --- 9.10: Verification report persistence (erasure-status returns correct state) ---

    [Fact]
    public async Task ErasureStatus_AfterErasure_ReturnsPartyAndTenantAsync() {
        _factory.ErasureRecordStore.GetStatusAsync("tenant-a", "verified-party", Arg.Any<CancellationToken>())
            .Returns(new PartyErasureStatusRecord {
                PartyId = "verified-party",
                TenantId = "tenant-a",
                Status = ErasureStatus.Erased.ToString(),
                UpdatedAt = DateTimeOffset.UtcNow,
                ErasedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            });
        _factory.ErasureRecordStore.GetVerificationReportAsync("tenant-a", "verified-party", Arg.Any<CancellationToken>())
            .Returns(new ErasureVerificationReport {
                PartyId = "verified-party",
                TenantId = "tenant-a",
                Timestamp = DateTimeOffset.UtcNow,
                OverallStatus = ErasureVerificationOverallStatus.Complete,
                StoreResults =
                [
                    new ErasureVerificationStoreResult
                    {
                        StoreName = "detail-projection",
                        Status = ErasureStoreCleanupStatus.Cleaned,
                        Timestamp = DateTimeOffset.UtcNow,
                    },
                    new ErasureVerificationStoreResult
                    {
                        StoreName = "index-projection",
                        Status = ErasureStoreCleanupStatus.Cleaned,
                        Timestamp = DateTimeOffset.UtcNow,
                    },
                ],
            });

        using HttpClient client = CreateAdminClient();

        HttpResponseMessage response = await client.GetAsync(
            string.Format(ErasureStatusEndpoint, "verified-party"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonDocument payload = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());

        // Verify the response structure includes required fields
        payload.RootElement.GetProperty("partyId").GetString().ShouldBe("verified-party");
        payload.RootElement.GetProperty("tenantId").GetString().ShouldBe("tenant-a");
        payload.RootElement.TryGetProperty("status", out JsonElement status).ShouldBeTrue();
        status.GetString().ShouldBe(ErasureStatus.Erased.ToString());
        payload.RootElement.GetProperty("storeResults").GetArrayLength().ShouldBe(2);
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
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ErasureTestFactory.SigningKey));
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
            issuer: ErasureTestFactory.Issuer,
            audience: ErasureTestFactory.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public sealed class ErasureTestFactory : WebApplicationFactory<Program> {
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
        internal string? GuardBlockingReason { get; set; }

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

            // Mock both 2-arg and 3-arg overloads (controller uses 2-arg)
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

            // Mock personal data command guard — reads GuardBlockingReason on each call
            PersonalDataGuard.GetBlockingReasonAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
                .Returns(_ => GuardBlockingReason);

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
