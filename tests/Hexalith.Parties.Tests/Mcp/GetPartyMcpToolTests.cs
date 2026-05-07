using System.Reflection;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.Parties.Authorization;
using Hexalith.Parties.Mcp;
using Hexalith.Parties.Tests.Authorization;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Abstractions;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.Tests.Mcp;

public sealed class GetPartyMcpToolTests
{
    [Fact]
    public async Task GetPartyAsync_ValidPartyId_ReturnsCompletePartyDetailJsonAsync()
    {
        using TenantScope tenantScope = TenantScope.Create("tenant-a");

        string partyId = Guid.NewGuid().ToString();
        PartyDetail detail = CreatePartyDetail(partyId, isActive: true);

        IPartyDetailProjectionActor projectionActor = Substitute.For<IPartyDetailProjectionActor>();
        projectionActor.GetDetailAsync().Returns(detail);

        IActorProxyFactory actorProxyFactory = CreateActorProxyFactory(projectionActor);
        ServiceProvider services = new ServiceCollection()
            .AddSingleton(actorProxyFactory)
            .AddSingleton<Hexalith.Parties.Authorization.ITenantAccessService, Hexalith.Parties.Tests.Authorization.TestTenantAccessService>()
            .BuildServiceProvider();

        string json = await GetPartyMcpTool.GetPartyAsync(partyId, services);

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        root.GetProperty("id").GetString().ShouldBe(partyId);
        root.GetProperty("type").GetString().ShouldBe("Person");
        root.GetProperty("isActive").GetBoolean().ShouldBeTrue();
        root.GetProperty("displayName").GetString().ShouldBe("Jean Dupont");
        root.GetProperty("sortName").GetString().ShouldBe("Dupont, Jean");
        root.TryGetProperty("personDetails", out JsonElement personDetails).ShouldBeTrue();
        personDetails.GetProperty("firstName").GetString().ShouldBe("Jean");
        personDetails.GetProperty("lastName").GetString().ShouldBe("Dupont");
        root.TryGetProperty("contactChannels", out _).ShouldBeTrue();
        root.TryGetProperty("identifiers", out _).ShouldBeTrue();
        root.TryGetProperty("createdAt", out _).ShouldBeTrue();
        root.TryGetProperty("lastModifiedAt", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task GetPartyAsync_NonExistentParty_ThrowsNotFoundErrorAsync()
    {
        using TenantScope _ = TenantScope.Create("tenant-a");

        string partyId = Guid.NewGuid().ToString();

        IPartyDetailProjectionActor projectionActor = Substitute.For<IPartyDetailProjectionActor>();
        projectionActor.GetDetailAsync().Returns((PartyDetail?)null);

        IActorProxyFactory actorProxyFactory = CreateActorProxyFactory(projectionActor);
        ServiceProvider services = new ServiceCollection()
            .AddSingleton(actorProxyFactory)
            .AddSingleton<Hexalith.Parties.Authorization.ITenantAccessService, Hexalith.Parties.Tests.Authorization.TestTenantAccessService>()
            .BuildServiceProvider();

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => GetPartyMcpTool.GetPartyAsync(partyId, services));

        exception.Message.ShouldBe($"Party not found. No party exists with ID '{partyId}'.");
    }

    [Fact]
    public async Task GetPartyAsync_ErasedParty_ThrowsErasedErrorAsync()
    {
        using TenantScope tenantScope = TenantScope.Create("tenant-a");

        string partyId = Guid.NewGuid().ToString();
        PartyDetail erasedDetail = new()
        {
            Id = partyId,
            Type = PartyType.Person,
            IsActive = false,
            DisplayName = string.Empty,
            SortName = string.Empty,
            IsErased = true,
            ErasedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-30),
            LastModifiedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
        };

        IPartyDetailProjectionActor projectionActor = Substitute.For<IPartyDetailProjectionActor>();
        projectionActor.GetDetailAsync().Returns(erasedDetail);

        IActorProxyFactory actorProxyFactory = CreateActorProxyFactory(projectionActor);
        ServiceProvider services = new ServiceCollection()
            .AddSingleton(actorProxyFactory)
            .AddSingleton<Hexalith.Parties.Authorization.ITenantAccessService, Hexalith.Parties.Tests.Authorization.TestTenantAccessService>()
            .BuildServiceProvider();

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => GetPartyMcpTool.GetPartyAsync(partyId, services));

        exception.Message.ShouldContain("erased");
        exception.Message.ShouldContain("GDPR Article 17");
    }

    [Fact]
    public async Task GetPartyAsync_InvalidPartyIdFormat_ThrowsValidationErrorAsync()
    {
        using TenantScope _ = TenantScope.Create("tenant-a");
        ServiceProvider services = new ServiceCollection()
            .AddSingleton(Substitute.For<IActorProxyFactory>())
            .AddSingleton<ITenantAccessService, TestTenantAccessService>()
            .BuildServiceProvider();

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => GetPartyMcpTool.GetPartyAsync("not-a-guid", services));

        exception.Message.ShouldBe(
            "Invalid party ID format. Expected a UUID like '550e8400-e29b-41d4-a716-446655440000'.");
    }

    [Fact]
    public async Task GetPartyAsync_AccessDenied_DoesNotReadProjectionAsync()
    {
        using TenantScope _ = TenantScope.Create("tenant-a");

        IPartyDetailProjectionActor projectionActor = Substitute.For<IPartyDetailProjectionActor>();
        IActorProxyFactory actorProxyFactory = CreateActorProxyFactory(projectionActor);
        ServiceProvider services = new ServiceCollection()
            .AddSingleton(actorProxyFactory)
            .AddSingleton<ITenantAccessService>(new TestTenantAccessService(
                (_, _, _, _) => Task.FromResult(TenantAccessDecision.Denied(TenantAccessDenialReason.MissingMember))))
            .BuildServiceProvider();

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            GetPartyMcpTool.GetPartyAsync(Guid.NewGuid().ToString(), services));

        exception.Message.ShouldContain("not-member");
        await projectionActor.DidNotReceive().GetDetailAsync();
    }

    [Fact]
    public async Task GetPartyAsync_MissingTenant_ThrowsAuthenticationErrorAsync()
    {
        ServiceProvider services = new ServiceCollection()
            .AddSingleton(Substitute.For<IActorProxyFactory>())
            .AddSingleton<ITenantAccessService>(new TestTenantAccessService(
                (_, _, _, _) => Task.FromResult(TenantAccessDecision.Denied(TenantAccessDenialReason.MissingTenantId))))
            .BuildServiceProvider();

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => GetPartyMcpTool.GetPartyAsync(Guid.NewGuid().ToString(), services));

        exception.Message.ShouldContain("missing-tenant");
    }

    private static IActorProxyFactory CreateActorProxyFactory(IPartyDetailProjectionActor projectionActor)
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        actorProxyFactory
            .CreateActorProxy<IPartyDetailProjectionActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(projectionActor);

        return actorProxyFactory;
    }

    private static PartyDetail CreatePartyDetail(string partyId, bool isActive)
        => new()
        {
            Id = partyId,
            Type = PartyType.Person,
            IsActive = isActive,
            DisplayName = "Jean Dupont",
            SortName = "Dupont, Jean",
            PersonDetails = new PersonDetails
            {
                FirstName = "Jean",
                LastName = "Dupont",
                DateOfBirth = new DateTimeOffset(1990, 1, 15, 0, 0, 0, TimeSpan.Zero),
                Prefix = null,
                Suffix = null,
            },
            OrganizationDetails = null,
            ContactChannels =
            [
                new ContactChannel
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = ContactChannelType.Email,
                    Value = "jean@example.com",
                    IsPreferred = true,
                },
            ],
            Identifiers =
            [
                new PartyIdentifier
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = IdentifierType.VAT,
                    Value = "FR12345678901",
                },
            ],
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            LastModifiedAt = DateTimeOffset.UtcNow,
        };

    private sealed class TenantScope : IDisposable
    {
        private static readonly FieldInfo _tenantField = typeof(GetPartyMcpTool)
            .Assembly
            .GetType("Hexalith.Parties.Mcp.McpSessionContext", throwOnError: true)!
            .GetField("Tenant", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;

        private static readonly FieldInfo _userIdField = typeof(GetPartyMcpTool)
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
