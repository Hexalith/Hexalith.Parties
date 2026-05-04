using System.Reflection;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using FluentValidation;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.Parties.CommandApi.Mcp;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Abstractions;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.CommandApi.Tests.Mcp;

public sealed class DeletePartyMcpToolTests
{
    [Fact]
    public async Task DeletePartyAsync_ActiveParty_DispatchesDeactivateCommandAsync()
    {
        using TenantScope _ = TenantScope.Create("tenant-a");

        string partyId = Guid.NewGuid().ToString();
        PartyDetail currentParty = CreatePartyDetail(partyId, isActive: true);

        SubmitCommand? routedCommand = null;
        ICommandRouter router = Substitute.For<ICommandRouter>();
        router
            .RouteCommandAsync(Arg.Do<SubmitCommand>(cmd => routedCommand = cmd), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CommandProcessingResult(true)));

        IPartyDetailProjectionActor projectionActor = Substitute.For<IPartyDetailProjectionActor>();
        projectionActor.GetDetailAsync().Returns(currentParty, currentParty);

        IActorProxyFactory actorProxyFactory = CreateActorProxyFactory(projectionActor);
        ServiceProvider services = BuildServices(router, actorProxyFactory, new InlineValidator<DeactivateParty>());

        await DeletePartyMcpTool.DeletePartyAsync(partyId, services);

        routedCommand.ShouldNotBeNull();
        routedCommand!.CommandType.ShouldBe(nameof(DeactivateParty));
        routedCommand.Domain.ShouldBe("party");
        routedCommand.AggregateId.ShouldBe(partyId);
    }

    [Fact]
    public async Task DeletePartyAsync_AlreadyDeactivated_ReturnsImmediatelyWithoutCommandAsync()
    {
        using TenantScope _ = TenantScope.Create("tenant-a");

        string partyId = Guid.NewGuid().ToString();
        PartyDetail inactiveParty = CreatePartyDetail(partyId, isActive: false);

        ICommandRouter router = Substitute.For<ICommandRouter>();

        IPartyDetailProjectionActor projectionActor = Substitute.For<IPartyDetailProjectionActor>();
        projectionActor.GetDetailAsync().Returns(inactiveParty);

        IActorProxyFactory actorProxyFactory = CreateActorProxyFactory(projectionActor);
        ServiceProvider services = BuildServices(router, actorProxyFactory, new InlineValidator<DeactivateParty>());

        string json = await DeletePartyMcpTool.DeletePartyAsync(partyId, services);

        await router.DidNotReceive()
            .RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>());

        using JsonDocument document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("isActive").GetBoolean().ShouldBeFalse();
        document.RootElement.GetProperty("id").GetString().ShouldBe(partyId);
    }

    [Fact]
    public async Task DeletePartyAsync_NonExistentParty_ThrowsNotFoundErrorAsync()
    {
        using TenantScope _ = TenantScope.Create("tenant-a");

        string partyId = Guid.NewGuid().ToString();

        IPartyDetailProjectionActor projectionActor = Substitute.For<IPartyDetailProjectionActor>();
        projectionActor.GetDetailAsync().Returns((PartyDetail?)null);

        IActorProxyFactory actorProxyFactory = CreateActorProxyFactory(projectionActor);
        ServiceProvider services = BuildServices(
            Substitute.For<ICommandRouter>(),
            actorProxyFactory,
            new InlineValidator<DeactivateParty>());

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => DeletePartyMcpTool.DeletePartyAsync(partyId, services));

        exception.Message.ShouldBe($"Party not found. No party exists with ID '{partyId}'.");
    }

    [Fact]
    public async Task DeletePartyAsync_InvalidPartyId_ThrowsValidationErrorAsync()
    {
        using TenantScope _ = TenantScope.Create("tenant-a");

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => DeletePartyMcpTool.DeletePartyAsync("not-a-guid", new ServiceCollection().AddSingleton<Hexalith.Parties.CommandApi.Authorization.ITenantAccessService, Hexalith.Parties.CommandApi.Tests.Authorization.TestTenantAccessService>().BuildServiceProvider()));

        exception.Message.ShouldBe("Party ID is required and must be a valid UUID.");
    }

    [Fact]
    public async Task DeletePartyAsync_MissingTenant_ThrowsAuthenticationErrorAsync()
    {
        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => DeletePartyMcpTool.DeletePartyAsync(Guid.NewGuid().ToString(), new ServiceCollection().AddSingleton<Hexalith.Parties.CommandApi.Authorization.ITenantAccessService, Hexalith.Parties.CommandApi.Tests.Authorization.TestTenantAccessService>().BuildServiceProvider()));

        exception.Message.ShouldContain("missing-tenant");
    }

    private static ServiceProvider BuildServices<TCommand>(
        ICommandRouter router,
        IActorProxyFactory actorProxyFactory,
        IValidator<TCommand> validator)
        where TCommand : class
        => new ServiceCollection()
            .AddSingleton(router)
            .AddSingleton(actorProxyFactory)
            .AddSingleton(validator)
            .AddSingleton<Hexalith.Parties.CommandApi.Authorization.ITenantAccessService, Hexalith.Parties.CommandApi.Tests.Authorization.TestTenantAccessService>()
            .BuildServiceProvider();

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
            ContactChannels = [],
            Identifiers = [],
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            LastModifiedAt = DateTimeOffset.UtcNow,
        };

    private sealed class TenantScope : IDisposable
    {
        private static readonly FieldInfo _tenantField = typeof(DeletePartyMcpTool)
            .Assembly
            .GetType("Hexalith.Parties.CommandApi.Mcp.McpSessionContext", throwOnError: true)!
            .GetField("Tenant", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;

        private static readonly FieldInfo _userIdField = typeof(DeletePartyMcpTool)
            .Assembly
            .GetType("Hexalith.Parties.CommandApi.Mcp.McpSessionContext", throwOnError: true)!
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
