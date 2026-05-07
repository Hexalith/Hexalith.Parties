using System.Reflection;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.Parties.Mcp;
using Hexalith.Parties.Search;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Search;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Abstractions;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.Tests.Mcp;

public sealed class FindPartiesMcpToolTests
{
    [Fact]
    public async Task FindPartiesAsync_WithQuery_ReturnsMatchingResultsWithMetadataAsync()
    {
        using TenantScope _ = TenantScope.Create("tenant-a");

        IPartyIndexProjectionActor indexActor = Substitute.For<IPartyIndexProjectionActor>();
        indexActor.GetEntriesAsync().Returns(new Dictionary<string, PartyIndexEntry>
        {
            ["id-1"] = new PartyIndexEntry
            {
                Id = "id-1",
                Type = PartyType.Person,
                IsActive = true,
                DisplayName = "Jean Dupont",
                SearchableContactChannels =
                [
                    new ContactChannel
                    {
                        Id = "ch1",
                        Type = ContactChannelType.Email,
                        Value = "jean@example.com",
                        IsPreferred = true,
                    },
                ],
                SearchableIdentifiers = [],
                CreatedAt = DateTimeOffset.UtcNow,
                LastModifiedAt = DateTimeOffset.UtcNow,
            },
            ["id-2"] = new PartyIndexEntry
            {
                Id = "id-2",
                Type = PartyType.Person,
                IsActive = true,
                DisplayName = "Marie Martin",
                SearchableContactChannels = [],
                SearchableIdentifiers = [],
                CreatedAt = DateTimeOffset.UtcNow,
                LastModifiedAt = DateTimeOffset.UtcNow,
            },
        });

        IActorProxyFactory factory = CreateActorProxyFactory(indexActor);
        ServiceProvider services = new ServiceCollection()
            .AddSingleton(factory)
            .AddSingleton<IPartySearchProvider, LocalFuzzyPartySearchProvider>()
            .AddSingleton<IPartySearchService, LocalPartySearchService>()
            .AddSingleton<Hexalith.Parties.Authorization.ITenantAccessService, Hexalith.Parties.Tests.Authorization.TestTenantAccessService>()
            .BuildServiceProvider();

        string json = await FindPartiesMcpTool.FindPartiesAsync(services, query: "Dupont");

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        // MCP now returns the full PartySearchResponse envelope; the paged items live under .results.items
        JsonElement results = root.GetProperty("results");
        results.GetProperty("items").GetArrayLength().ShouldBe(1);
        JsonElement item = results.GetProperty("items")[0];
        item.GetProperty("party").GetProperty("displayName").GetString().ShouldBe("Jean Dupont");
        item.TryGetProperty("matches", out JsonElement matches).ShouldBeTrue();
        matches.GetArrayLength().ShouldBeGreaterThan(0);
        matches[0].GetProperty("matchedField").GetString().ShouldNotBeNullOrWhiteSpace();
        matches[0].GetProperty("matchType").GetString().ShouldNotBeNullOrWhiteSpace();

        // Confirm the envelope-level fields are exposed too — ScoreMetadata + Status
        root.TryGetProperty("status", out JsonElement statusEl).ShouldBeTrue();
        root.TryGetProperty("scoreMetadata", out JsonElement scoresEl).ShouldBeTrue();
        root.TryGetProperty("sourceMetadata", out JsonElement sourcesEl).ShouldBeTrue();
        statusEl.ValueKind.ShouldNotBe(JsonValueKind.Undefined);
        scoresEl.ValueKind.ShouldNotBe(JsonValueKind.Undefined);
        sourcesEl.ValueKind.ShouldNotBe(JsonValueKind.Undefined);
    }

    [Fact]
    public async Task FindPartiesAsync_EmptyQuery_ReturnsPaginatedListAsync()
    {
        using TenantScope _ = TenantScope.Create("tenant-a");

        IPartyIndexProjectionActor indexActor = Substitute.For<IPartyIndexProjectionActor>();
        indexActor.GetEntriesAsync().Returns(new Dictionary<string, PartyIndexEntry>
        {
            ["id-1"] = CreateIndexEntry("id-1", "Jean Dupont"),
            ["id-2"] = CreateIndexEntry("id-2", "Marie Martin"),
        });

        IActorProxyFactory factory = CreateActorProxyFactory(indexActor);
        ServiceProvider services = new ServiceCollection()
            .AddSingleton(factory)
            .AddSingleton<Hexalith.Parties.Authorization.ITenantAccessService, Hexalith.Parties.Tests.Authorization.TestTenantAccessService>()
            .BuildServiceProvider();

        string json = await FindPartiesMcpTool.FindPartiesAsync(services, query: null);

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        root.GetProperty("items").GetArrayLength().ShouldBe(2);
        root.GetProperty("page").GetInt32().ShouldBe(1);
        root.GetProperty("pageSize").GetInt32().ShouldBe(20);
        root.GetProperty("totalCount").GetInt32().ShouldBe(2);
        root.GetProperty("totalPages").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task FindPartiesAsync_Pagination_RespectsPageAndPageSizeAsync()
    {
        using TenantScope _ = TenantScope.Create("tenant-a");

        IPartyIndexProjectionActor indexActor = Substitute.For<IPartyIndexProjectionActor>();
        indexActor.GetEntriesAsync().Returns(new Dictionary<string, PartyIndexEntry>
        {
            ["id-1"] = CreateIndexEntry("id-1", "Alpha"),
            ["id-2"] = CreateIndexEntry("id-2", "Beta"),
            ["id-3"] = CreateIndexEntry("id-3", "Gamma"),
        });

        IActorProxyFactory factory = CreateActorProxyFactory(indexActor);
        ServiceProvider services = new ServiceCollection()
            .AddSingleton(factory)
            .AddSingleton<Hexalith.Parties.Authorization.ITenantAccessService, Hexalith.Parties.Tests.Authorization.TestTenantAccessService>()
            .BuildServiceProvider();

        string json = await FindPartiesMcpTool.FindPartiesAsync(services, query: null, page: 2, pageSize: 1);

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        root.GetProperty("items").GetArrayLength().ShouldBe(1);
        root.GetProperty("page").GetInt32().ShouldBe(2);
        root.GetProperty("pageSize").GetInt32().ShouldBe(1);
        root.GetProperty("totalCount").GetInt32().ShouldBe(3);
        root.GetProperty("totalPages").GetInt32().ShouldBe(3);
        root.GetProperty("items")[0].GetProperty("displayName").GetString().ShouldBe("Beta");
    }

    [Fact]
    public async Task FindPartiesAsync_PageSizeClamped_MaxIs100Async()
    {
        using TenantScope _ = TenantScope.Create("tenant-a");

        IPartyIndexProjectionActor indexActor = Substitute.For<IPartyIndexProjectionActor>();
        indexActor.GetEntriesAsync().Returns(new Dictionary<string, PartyIndexEntry>());

        IActorProxyFactory factory = CreateActorProxyFactory(indexActor);
        ServiceProvider services = new ServiceCollection()
            .AddSingleton(factory)
            .AddSingleton<Hexalith.Parties.Authorization.ITenantAccessService, Hexalith.Parties.Tests.Authorization.TestTenantAccessService>()
            .BuildServiceProvider();

        string json = await FindPartiesMcpTool.FindPartiesAsync(services, query: null, pageSize: 200);

        using JsonDocument document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("pageSize").GetInt32().ShouldBe(100);
    }

    [Fact]
    public async Task FindPartiesAsync_ErasedPartyInIndex_ExcludedFromResultsAsync()
    {
        using TenantScope _ = TenantScope.Create("tenant-a");

        IPartyIndexProjectionActor indexActor = Substitute.For<IPartyIndexProjectionActor>();
        indexActor.GetEntriesAsync().Returns(new Dictionary<string, PartyIndexEntry>
        {
            ["id-active"] = CreateIndexEntry("id-active", "Active User"),
            ["id-erased"] = new PartyIndexEntry
            {
                Id = "id-erased",
                Type = PartyType.Person,
                IsActive = false,
                DisplayName = string.Empty,
                IsErased = true,
                SearchableContactChannels = [],
                SearchableIdentifiers = [],
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-30),
                LastModifiedAt = DateTimeOffset.UtcNow,
            },
        });

        IActorProxyFactory factory = CreateActorProxyFactory(indexActor);
        ServiceProvider services = new ServiceCollection()
            .AddSingleton(factory)
            .AddSingleton<Hexalith.Parties.Authorization.ITenantAccessService, Hexalith.Parties.Tests.Authorization.TestTenantAccessService>()
            .BuildServiceProvider();

        string json = await FindPartiesMcpTool.FindPartiesAsync(services, query: null);

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        root.GetProperty("totalCount").GetInt32().ShouldBe(1);
        root.GetProperty("items").GetArrayLength().ShouldBe(1);
        root.GetProperty("items")[0].GetProperty("displayName").GetString().ShouldBe("Active User");
    }

    [Fact]
    public async Task FindPartiesAsync_MissingTenant_ThrowsAuthenticationErrorAsync()
    {
        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => FindPartiesMcpTool.FindPartiesAsync(new ServiceCollection().AddSingleton<Hexalith.Parties.Authorization.ITenantAccessService, Hexalith.Parties.Tests.Authorization.TestTenantAccessService>().BuildServiceProvider()));

        exception.Message.ShouldContain("missing-tenant");
    }

    private static IActorProxyFactory CreateActorProxyFactory(IPartyIndexProjectionActor indexActor)
    {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        factory
            .CreateActorProxy<IPartyIndexProjectionActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(indexActor);

        return factory;
    }

    private static PartyIndexEntry CreateIndexEntry(string id, string displayName)
        => new()
        {
            Id = id,
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = displayName,
            SearchableContactChannels = [],
            SearchableIdentifiers = [],
            CreatedAt = DateTimeOffset.UtcNow,
            LastModifiedAt = DateTimeOffset.UtcNow,
        };

    private sealed class TenantScope : IDisposable
    {
        private static readonly FieldInfo _tenantField = typeof(FindPartiesMcpTool)
            .Assembly
            .GetType("Hexalith.Parties.Mcp.McpSessionContext", throwOnError: true)!
            .GetField("Tenant", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;

        private static readonly FieldInfo _userIdField = typeof(FindPartiesMcpTool)
            .Assembly
            .GetType("Hexalith.Parties.Mcp.McpSessionContext", throwOnError: true)!
            .GetField("UserId", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;

        private readonly AsyncLocal<string?> _tenant;
        private readonly AsyncLocal<string?> _userId;
        private readonly string? _previousTenant;
        private readonly string? _previousUserId;

        private TenantScope(string value)
        {
            _tenant = (AsyncLocal<string?>)_tenantField.GetValue(null)!;
            _userId = (AsyncLocal<string?>)_userIdField.GetValue(null)!;
            _previousTenant = _tenant.Value;
            _previousUserId = _userId.Value;
            _tenant.Value = value;
            _userId.Value = "test-user";
        }

        public static TenantScope Create(string value) => new(value);

        public void Dispose()
        {
            _tenant.Value = _previousTenant;
            _userId.Value = _previousUserId;
        }
    }
}
