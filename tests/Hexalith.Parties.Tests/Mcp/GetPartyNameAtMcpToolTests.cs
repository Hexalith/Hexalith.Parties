using System.Reflection;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.Parties.Mcp;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Abstractions;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.Tests.Mcp;

public sealed class GetPartyNameAtMcpToolTests
{
    private static readonly DateTimeOffset CreatedAt = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset NameChangedAt = new(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset BetweenChanges = new(2025, 3, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset BeforeCreation = new(2024, 12, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetPartyNameAtAsync_TimestampBetweenChanges_ReturnsHistoricalNameAsync()
    {
        using TenantScope _ = TenantScope.Create("tenant-a");

        string partyId = Guid.NewGuid().ToString();
        PartyDetail detail = CreateDetailWithHistory(partyId);

        IServiceProvider services = BuildServicesFor(detail);

        string json = await GetPartyNameAtMcpTool.GetPartyNameAtAsync(
            partyId,
            BetweenChanges.ToString("O"),
            services);

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        root.GetProperty("partyId").GetString().ShouldBe(partyId);
        root.GetProperty("displayName").GetString().ShouldBe("John Doe");
        root.GetProperty("sortName").GetString().ShouldBe("Doe, John");
        root.GetProperty("asOf").GetDateTimeOffset().ShouldBe(BetweenChanges);
    }

    [Fact]
    public async Task GetPartyNameAtAsync_TimestampAfterAllChanges_ReturnsLatestNameAsync()
    {
        using TenantScope _ = TenantScope.Create("tenant-a");

        string partyId = Guid.NewGuid().ToString();
        PartyDetail detail = CreateDetailWithHistory(partyId);

        IServiceProvider services = BuildServicesFor(detail);

        string json = await GetPartyNameAtMcpTool.GetPartyNameAtAsync(
            partyId,
            NameChangedAt.AddDays(1).ToString("O"),
            services);

        using JsonDocument document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("displayName").GetString().ShouldBe("Jane Smith");
    }

    [Fact]
    public async Task GetPartyNameAtAsync_TimestampBeforeCreation_ThrowsAsync()
    {
        using TenantScope _ = TenantScope.Create("tenant-a");

        string partyId = Guid.NewGuid().ToString();
        PartyDetail detail = CreateDetailWithHistory(partyId);

        IServiceProvider services = BuildServicesFor(detail);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => GetPartyNameAtMcpTool.GetPartyNameAtAsync(
                partyId,
                BeforeCreation.ToString("O"),
                services));

        exception.Message.ShouldBe("Party did not exist at the requested timestamp.");
    }

    [Fact]
    public async Task GetPartyNameAtAsync_NonExistentParty_ThrowsNotFoundAsync()
    {
        using TenantScope _ = TenantScope.Create("tenant-a");

        string partyId = Guid.NewGuid().ToString();

        IServiceProvider services = BuildServicesFor(detail: null);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => GetPartyNameAtMcpTool.GetPartyNameAtAsync(
                partyId,
                NameChangedAt.ToString("O"),
                services));

        exception.Message.ShouldBe($"Party not found. No party exists with ID '{partyId}'.");
    }

    [Fact]
    public async Task GetPartyNameAtAsync_ErasedParty_ThrowsErasedAsync()
    {
        using TenantScope _ = TenantScope.Create("tenant-a");

        string partyId = Guid.NewGuid().ToString();
        PartyDetail erased = new()
        {
            Id = partyId,
            Type = PartyType.Person,
            IsActive = false,
            DisplayName = string.Empty,
            SortName = string.Empty,
            IsErased = true,
            ErasedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            CreatedAt = CreatedAt,
            LastModifiedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            NameHistory = [],
        };

        IServiceProvider services = BuildServicesFor(erased);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => GetPartyNameAtMcpTool.GetPartyNameAtAsync(
                partyId,
                NameChangedAt.ToString("O"),
                services));

        exception.Message.ShouldContain("erased");
        exception.Message.ShouldContain("GDPR Article 17");
    }

    [Fact]
    public async Task GetPartyNameAtAsync_PartyWithoutHistory_ThrowsHistoryUnavailableAsync()
    {
        using TenantScope _ = TenantScope.Create("tenant-a");

        string partyId = Guid.NewGuid().ToString();
        PartyDetail noHistory = new()
        {
            Id = partyId,
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "John Doe",
            SortName = "Doe, John",
            CreatedAt = CreatedAt,
            LastModifiedAt = CreatedAt,
            NameHistory = [],
        };

        IServiceProvider services = BuildServicesFor(noHistory);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => GetPartyNameAtMcpTool.GetPartyNameAtAsync(
                partyId,
                NameChangedAt.ToString("O"),
                services));

        exception.Message.ShouldContain("Name history not available");
    }

    [Fact]
    public async Task GetPartyNameAtAsync_InvalidPartyIdFormat_ThrowsValidationAsync()
    {
        using TenantScope _ = TenantScope.Create("tenant-a");

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => GetPartyNameAtMcpTool.GetPartyNameAtAsync(
                "not-a-guid",
                NameChangedAt.ToString("O"),
                new ServiceCollection().AddSingleton<Hexalith.Parties.Authorization.ITenantAccessService, Hexalith.Parties.Tests.Authorization.TestTenantAccessService>().BuildServiceProvider()));

        exception.Message.ShouldStartWith("Invalid party ID format");
    }

    [Fact]
    public async Task GetPartyNameAtAsync_InvalidTimestampFormat_ThrowsValidationAsync()
    {
        using TenantScope _ = TenantScope.Create("tenant-a");

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => GetPartyNameAtMcpTool.GetPartyNameAtAsync(
                Guid.NewGuid().ToString(),
                "not-a-timestamp",
                new ServiceCollection().AddSingleton<Hexalith.Parties.Authorization.ITenantAccessService, Hexalith.Parties.Tests.Authorization.TestTenantAccessService>().BuildServiceProvider()));

        exception.Message.ShouldStartWith("Invalid timestamp format");
    }

    [Fact]
    public async Task GetPartyNameAtAsync_MissingTenant_ThrowsAuthenticationErrorAsync()
    {
        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => GetPartyNameAtMcpTool.GetPartyNameAtAsync(
                Guid.NewGuid().ToString(),
                NameChangedAt.ToString("O"),
                new ServiceCollection().AddSingleton<Hexalith.Parties.Authorization.ITenantAccessService, Hexalith.Parties.Tests.Authorization.TestTenantAccessService>().BuildServiceProvider()));

        exception.Message.ShouldContain("missing-tenant");
    }

    [Fact]
    public async Task GetPartyNameAtAsync_OutOfOrderHistory_ResolvesByChronologicalOrderAsync()
    {
        using TenantScope _ = TenantScope.Create("tenant-a");

        string partyId = Guid.NewGuid().ToString();
        PartyDetail detail = new()
        {
            Id = partyId,
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Jane Smith",
            SortName = "Smith, Jane",
            CreatedAt = CreatedAt,
            LastModifiedAt = NameChangedAt,
            // Entries deliberately appended out of chronological order to assert the
            // controller/MCP defense-in-depth OrderBy(ChangedAt) holds.
            NameHistory =
            [
                new NameHistoryEntry { DisplayName = "Jane Smith", SortName = "Smith, Jane", ChangedAt = NameChangedAt, TriggeredBy = "PartyDisplayNameDerived" },
                new NameHistoryEntry { DisplayName = "John Doe", SortName = "Doe, John", ChangedAt = CreatedAt, TriggeredBy = "PartyCreated" },
            ],
        };

        IServiceProvider services = BuildServicesFor(detail);

        string json = await GetPartyNameAtMcpTool.GetPartyNameAtAsync(
            partyId,
            BetweenChanges.ToString("O"),
            services);

        using JsonDocument document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("displayName").GetString().ShouldBe("John Doe");
    }

    private static PartyDetail CreateDetailWithHistory(string partyId)
        => new()
        {
            Id = partyId,
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Jane Smith",
            SortName = "Smith, Jane",
            CreatedAt = CreatedAt,
            LastModifiedAt = NameChangedAt,
            NameHistory =
            [
                new NameHistoryEntry { DisplayName = "John Doe", SortName = "Doe, John", ChangedAt = CreatedAt, TriggeredBy = "PartyCreated" },
                new NameHistoryEntry { DisplayName = "Jane Smith", SortName = "Smith, Jane", ChangedAt = NameChangedAt, TriggeredBy = "PartyDisplayNameDerived" },
            ],
        };

    private static IServiceProvider BuildServicesFor(PartyDetail? detail)
    {
        IPartyDetailProjectionActor projectionActor = Substitute.For<IPartyDetailProjectionActor>();
        projectionActor.GetDetailJsonAsync().Returns((string?)null);
        projectionActor.GetSerializedDetailAsync().Returns((byte[]?)null);
        projectionActor.GetDetailAsync().Returns(detail);

        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        actorProxyFactory
            .CreateActorProxy<IPartyDetailProjectionActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(projectionActor);

        return new ServiceCollection()
            .AddSingleton(actorProxyFactory)
            .AddSingleton<Hexalith.Parties.Authorization.ITenantAccessService, Hexalith.Parties.Tests.Authorization.TestTenantAccessService>()
            .BuildServiceProvider();
    }

    private sealed class TenantScope : IDisposable
    {
        private static readonly FieldInfo _tenantField = typeof(GetPartyNameAtMcpTool)
            .Assembly
            .GetType("Hexalith.Parties.Mcp.McpSessionContext", throwOnError: true)!
            .GetField("Tenant", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;

        private static readonly FieldInfo _userIdField = typeof(GetPartyNameAtMcpTool)
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
