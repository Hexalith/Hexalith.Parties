using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
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

public sealed class PortabilityEndpointTests : IClassFixture<PortabilityEndpointTests.PortabilityTestFactory> {
    private const string ExportEndpoint = "/api/v1/admin/parties/{0}/export";

    private readonly PortabilityTestFactory _factory;

    public PortabilityEndpointTests(PortabilityTestFactory factory) {
        _factory = factory;
    }

    // === Task 8.12: GET export returns complete party JSON ===

    [Fact]
    public async Task Export_WithAdminToken_ReturnsCompletePartyJsonAsync() {
        PartyDetail fullDetail = new() {
            Id = "export-party",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Jane Doe",
            SortName = "Doe, Jane",
            PersonDetails = new PersonDetails {
                FirstName = "Jane",
                LastName = "Doe",
            },
            ContactChannels = [
                new ContactChannel {
                    Id = "ch-email-1",
                    Type = ContactChannelType.Email,
                    Value = "jane@example.com",
                    IsPreferred = true,
                },
            ],
            Identifiers = [
                new PartyIdentifier {
                    Id = "id-vat-1",
                    Type = IdentifierType.VAT,
                    Value = "FR12345678901",
                },
            ],
            ConsentRecords = [
                new ConsentRecord {
                    ConsentId = "ch-email-1:marketing",
                    ChannelId = "ch-email-1",
                    Purpose = "marketing",
                    LawfulBasis = LawfulBasis.Consent,
                    GrantedAt = DateTimeOffset.UtcNow.AddDays(-5),
                    GrantedBy = "admin",
                },
            ],
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-30),
            LastModifiedAt = DateTimeOffset.UtcNow,
        };
        _factory.DetailProxy.GetDetailAsync().Returns(Task.FromResult<PartyDetail?>(fullDetail));

        using HttpClient client = CreateAdminClient();

        HttpResponseMessage response = await client.GetAsync(
            string.Format(ExportEndpoint, "export-party"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonDocument payload = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());

        payload.RootElement.GetProperty("partyId").GetString().ShouldBe("export-party");
        payload.RootElement.GetProperty("partyType").GetString().ShouldBe("Person");
        payload.RootElement.GetProperty("displayName").GetString().ShouldBe("Jane Doe");
        payload.RootElement.TryGetProperty("exportedAt", out _).ShouldBeTrue();

        // Verify all data sections are present
        payload.RootElement.GetProperty("personDetails").GetProperty("firstName").GetString().ShouldBe("Jane");
        payload.RootElement.GetProperty("contactChannels").GetArrayLength().ShouldBe(1);
        payload.RootElement.GetProperty("identifiers").GetArrayLength().ShouldBe(1);
        payload.RootElement.GetProperty("consentRecords").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task Export_WithoutToken_Returns401Async() {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            string.Format(ExportEndpoint, "party-1"));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Export_WithRegularUserToken_Returns403Async() {
        using HttpClient client = CreateRegularUserClient();

        HttpResponseMessage response = await client.GetAsync(
            string.Format(ExportEndpoint, "party-1"));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Export_PartyNotFound_Returns404Async() {
        _factory.DetailProxy.GetDetailAsync().Returns(Task.FromResult<PartyDetail?>(null));

        using HttpClient client = CreateAdminClient();

        HttpResponseMessage response = await client.GetAsync(
            string.Format(ExportEndpoint, "nonexistent-party"));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // === Task 8.13: GET export for restricted party succeeds (portability is a separate right) ===

    [Fact]
    public async Task Export_RestrictedParty_Returns200Async() {
        PartyDetail restrictedDetail = new() {
            Id = "restricted-party",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Restricted User",
            SortName = "User, Restricted",
            IsRestricted = true,
            RestrictedAt = DateTimeOffset.UtcNow.AddHours(-2),
            PersonDetails = new PersonDetails {
                FirstName = "Restricted",
                LastName = "User",
            },
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-30),
            LastModifiedAt = DateTimeOffset.UtcNow,
        };
        _factory.DetailProxy.GetDetailAsync().Returns(Task.FromResult<PartyDetail?>(restrictedDetail));

        using HttpClient client = CreateAdminClient();

        HttpResponseMessage response = await client.GetAsync(
            string.Format(ExportEndpoint, "restricted-party"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonDocument payload = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        payload.RootElement.GetProperty("partyId").GetString().ShouldBe("restricted-party");
    }

    // === Task 8.14: GET export for erasure-pending party returns 409 Conflict ===

    [Fact]
    public async Task Export_ErasurePendingParty_Returns409ConflictAsync() {
        PartyDetail pendingDetail = new() {
            Id = "pending-party",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Pending User",
            SortName = "User, Pending",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-30),
            LastModifiedAt = DateTimeOffset.UtcNow,
        };
        _factory.DetailProxy.GetDetailAsync().Returns(Task.FromResult<PartyDetail?>(pendingDetail));

        _factory.ErasureRecordStore.GetStatusAsync("tenant-a", "pending-party", Arg.Any<CancellationToken>())
            .Returns(new PartyErasureStatusRecord {
                PartyId = "pending-party",
                TenantId = "tenant-a",
                Status = ErasureStatus.ErasurePending.ToString(),
                UpdatedAt = DateTimeOffset.UtcNow,
            });

        using HttpClient client = CreateAdminClient();

        HttpResponseMessage response = await client.GetAsync(
            string.Format(ExportEndpoint, "pending-party"));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Export_ErasureStatusErased_Returns410GoneEvenWhenProjectionLagsAsync() {
        PartyDetail laggingDetail = new() {
            Id = "erased-lagging-party",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Lagging Projection",
            SortName = "Projection, Lagging",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-30),
            LastModifiedAt = DateTimeOffset.UtcNow,
        };
        _factory.DetailProxy.GetDetailAsync().Returns(Task.FromResult<PartyDetail?>(laggingDetail));

        _factory.ErasureRecordStore.GetStatusAsync("tenant-a", "erased-lagging-party", Arg.Any<CancellationToken>())
            .Returns(new PartyErasureStatusRecord {
                PartyId = "erased-lagging-party",
                TenantId = "tenant-a",
                Status = ErasureStatus.Erased.ToString(),
                UpdatedAt = DateTimeOffset.UtcNow,
                ErasedAt = DateTimeOffset.UtcNow,
            });

        using HttpClient client = CreateAdminClient();

        HttpResponseMessage response = await client.GetAsync(
            string.Format(ExportEndpoint, "erased-lagging-party"));

        response.StatusCode.ShouldBe(HttpStatusCode.Gone);
    }

    // === Task 8.15: GET export for erased party returns 410 Gone ===

    [Fact]
    public async Task Export_ErasedParty_Returns410GoneAsync() {
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

        using HttpClient client = CreateAdminClient();

        HttpResponseMessage response = await client.GetAsync(
            string.Format(ExportEndpoint, "erased-party"));

        response.StatusCode.ShouldBe(HttpStatusCode.Gone);
    }

    [Fact]
    public async Task GetProcessingRecords_WithAdminToken_ReturnsEventSummariesAsync() {
        _factory.ProjectionRebuildService.GetProcessingRecordsAsync("tenant-a", "processing-party", Arg.Any<CancellationToken>())
            .Returns([
                new ProcessingActivityRecord {
                    SequenceNumber = 1,
                    EventType = "PartyCreated",
                    Timestamp = DateTimeOffset.UtcNow.AddDays(-2),
                    Summary = "Party record created.",
                },
                new ProcessingActivityRecord {
                    SequenceNumber = 2,
                    EventType = "ConsentRecorded",
                    Timestamp = DateTimeOffset.UtcNow.AddDays(-1),
                    Summary = "Consent recorded for 'marketing' via channel 'ch-1'.",
                },
            ]);

        using HttpClient client = CreateAdminClient();

        HttpResponseMessage response = await client.GetAsync("/api/v1/admin/parties/processing-party/processing-records");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonDocument payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        payload.RootElement.GetProperty("partyId").GetString().ShouldBe("processing-party");
        payload.RootElement.GetProperty("records").GetArrayLength().ShouldBe(2);
        payload.RootElement.GetProperty("records")[1].GetProperty("eventType").GetString().ShouldBe("ConsentRecorded");
    }

    [Fact]
    public async Task GetProcessingRecords_PartyNotFound_Returns404Async() {
        _factory.ProjectionRebuildService.GetProcessingRecordsAsync("tenant-a", "missing-party", Arg.Any<CancellationToken>())
            .Returns([]);
        _factory.DetailProxy.GetDetailAsync().Returns(Task.FromResult<PartyDetail?>(null));

        using HttpClient client = CreateAdminClient();

        HttpResponseMessage response = await client.GetAsync("/api/v1/admin/parties/missing-party/processing-records");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
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
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(PortabilityTestFactory.SigningKey));
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
            issuer: PortabilityTestFactory.Issuer,
            audience: PortabilityTestFactory.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public sealed class PortabilityTestFactory : WebApplicationFactory<Program> {
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
            });
        }
    }
}
