using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Commands;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Search;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Abstractions;
using Hexalith.Parties.Projections.Actors;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.CommandApi.Tests.Controllers;

public sealed class TemporalNameEndpointTests : IClassFixture<TemporalNameEndpointTests.TemporalNameTestFactory>
{
    private readonly TemporalNameTestFactory _factory;

    public TemporalNameEndpointTests(TemporalNameTestFactory factory)
    {
        _factory = factory;
    }

    // 8.3: GET /api/v1/parties/{id}/name?at={timestamp} returns historical name
    [Fact]
    public async Task GetPartyNameAt_WithValidTimestamp_ReturnsHistoricalName()
    {
        DateTimeOffset createdTime = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset nameChangeTime = new(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset queryTime = new(2025, 3, 1, 0, 0, 0, TimeSpan.Zero);

        _factory.SetDetail(new PartyDetail
        {
            Id = "test-id",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Jane Smith",
            SortName = "Smith, Jane",
            CreatedAt = createdTime,
            LastModifiedAt = nameChangeTime,
            NameHistory =
            [
                new NameHistoryEntry { DisplayName = "John Doe", SortName = "Doe, John", ChangedAt = createdTime, TriggeredBy = "PartyCreated" },
                new NameHistoryEntry { DisplayName = "Jane Smith", SortName = "Smith, Jane", ChangedAt = nameChangeTime, TriggeredBy = "PartyDisplayNameDerived" },
            ],
        });

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        HttpResponseMessage response = await client.GetAsync($"/api/v1/parties/test-id/name?at={Uri.EscapeDataString(queryTime.ToString("O"))}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using JsonDocument payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        payload.RootElement.GetProperty("partyId").GetString().ShouldBe("test-id");
        payload.RootElement.GetProperty("displayName").GetString().ShouldBe("John Doe");
        payload.RootElement.GetProperty("sortName").GetString().ShouldBe("Doe, John");
    }

    // 8.4: GET /api/v1/parties/{id}/name?at={beforeCreation} returns 404
    [Fact]
    public async Task GetPartyNameAt_BeforeCreation_Returns404()
    {
        DateTimeOffset createdTime = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset queryTime = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

        _factory.SetDetail(new PartyDetail
        {
            Id = "test-id",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "John Doe",
            SortName = "Doe, John",
            CreatedAt = createdTime,
            LastModifiedAt = createdTime,
            NameHistory =
            [
                new NameHistoryEntry { DisplayName = "John Doe", SortName = "Doe, John", ChangedAt = createdTime, TriggeredBy = "PartyCreated" },
            ],
        });

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        HttpResponseMessage response = await client.GetAsync($"/api/v1/parties/test-id/name?at={Uri.EscapeDataString(queryTime.ToString("O"))}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // 8.5: GET /api/v1/parties/{id}/name-history returns full timeline
    [Fact]
    public async Task GetPartyNameHistory_ReturnsFullTimeline()
    {
        DateTimeOffset t1 = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset t2 = new(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);

        _factory.SetDetail(new PartyDetail
        {
            Id = "test-id",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Jane Smith",
            SortName = "Smith, Jane",
            CreatedAt = t1,
            LastModifiedAt = t2,
            NameHistory =
            [
                new NameHistoryEntry { DisplayName = "John Doe", SortName = "Doe, John", ChangedAt = t1, TriggeredBy = "PartyCreated" },
                new NameHistoryEntry { DisplayName = "Jane Smith", SortName = "Smith, Jane", ChangedAt = t2, TriggeredBy = "PartyDisplayNameDerived" },
            ],
        });

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        HttpResponseMessage response = await client.GetAsync("/api/v1/parties/test-id/name-history");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using JsonDocument payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        payload.RootElement.GetArrayLength().ShouldBe(2);
        payload.RootElement[0].GetProperty("displayName").GetString().ShouldBe("John Doe");
        payload.RootElement[1].GetProperty("displayName").GetString().ShouldBe("Jane Smith");
    }

    // 8.6: GET /api/v1/parties/{id}/name for erased party returns 410 Gone
    [Fact]
    public async Task GetPartyNameAt_ErasedParty_Returns410Gone()
    {
        _factory.SetDetail(new PartyDetail
        {
            Id = "test-id",
            Type = PartyType.Person,
            IsActive = false,
            DisplayName = string.Empty,
            SortName = string.Empty,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-30),
            LastModifiedAt = DateTimeOffset.UtcNow,
            IsErased = true,
            ErasedAt = DateTimeOffset.UtcNow,
        });

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenHelper.CreateToken(includeTenantClaim: true));

        HttpResponseMessage response = await client.GetAsync($"/api/v1/parties/test-id/name?at={Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"))}");

        response.StatusCode.ShouldBe(HttpStatusCode.Gone);
    }

    // 8.9: Auth tests: 401 for name endpoints without token
    [Fact]
    public async Task GetPartyNameAt_WithoutToken_Returns401()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/v1/parties/test-id/name?at=2025-01-01T00:00:00Z");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPartyNameHistory_WithoutToken_Returns401()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/v1/parties/test-id/name-history");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // 8.10: DI resolution test
    [Fact]
    public void DIResolution_IPartySearchProvider_ResolvesToLocalFuzzyPartySearchProvider()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IPartySearchProvider provider = scope.ServiceProvider.GetRequiredService<IPartySearchProvider>();

        provider.ShouldBeOfType<CommandApi.Search.LocalFuzzyPartySearchProvider>();
    }

    public sealed class TemporalNameTestFactory : WebApplicationFactory<Program>
    {
        private readonly IPartyDetailProjectionActor _detailProxy = Substitute.For<IPartyDetailProjectionActor>();

        internal void SetDetail(PartyDetail? detail)
        {
            _detailProxy.GetDetailAsync().Returns(Task.FromResult(detail));
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.UseEnvironment("Development");

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Authentication:JwtBearer:Issuer"] = JwtTokenHelper.Issuer,
                    ["Authentication:JwtBearer:Audience"] = JwtTokenHelper.Audience,
                    ["Authentication:JwtBearer:SigningKey"] = JwtTokenHelper.SigningKey,
                    ["Authentication:JwtBearer:RequireHttpsMetadata"] = "false",
                });
            });

            _detailProxy.GetDetailAsync().Returns(Task.FromResult<PartyDetail?>(null));
            _detailProxy.IsRebuildingAsync().Returns(Task.FromResult(false));

            IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
            actorProxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
                Arg.Any<ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions?>())
                .Returns(_detailProxy);
            actorProxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
                Arg.Any<ActorId>(), Arg.Any<string>())
                .Returns(_detailProxy);

            IPartyIndexProjectionActor indexProxy = Substitute.For<IPartyIndexProjectionActor>();
            indexProxy.GetEntriesAsync().Returns(Task.FromResult<IReadOnlyDictionary<string, PartyIndexEntry>>(
                new Dictionary<string, PartyIndexEntry>()));
            actorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
                Arg.Any<ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions?>())
                .Returns(indexProxy);
            actorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
                Arg.Any<ActorId>(), Arg.Any<string>())
                .Returns(indexProxy);

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICommandRouter>();
                services.AddSingleton(Substitute.For<ICommandRouter>());
                services.RemoveAll<IActorProxyFactory>();
                services.AddSingleton(actorProxyFactory);
                services.RemoveAll<IPersonalDataCommandGuard>();
                services.AddSingleton(Substitute.For<IPersonalDataCommandGuard>());
            });
        }
    }
}
