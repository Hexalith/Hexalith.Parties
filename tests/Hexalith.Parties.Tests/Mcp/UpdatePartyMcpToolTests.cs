using System.Reflection;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using FluentValidation;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.Parties.Mcp;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Abstractions;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.Tests.Mcp;

public sealed class UpdatePartyMcpToolTests
{
    [Fact]
    public async Task UpdatePartyAsync_AddEmailOnly_ConstructsPatchWithOnlyAddChannelsAsync()
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
        ServiceProvider services = BuildServices(router, actorProxyFactory, new InlineValidator<UpdatePartyComposite>());

        await UpdatePartyMcpTool.UpdatePartyAsync(
            partyId: partyId,
            services: services,
            addEmail: "new@example.com");

        routedCommand.ShouldNotBeNull();
        UpdatePartyComposite? command = JsonSerializer.Deserialize<UpdatePartyComposite>(routedCommand!.Payload);
        command.ShouldNotBeNull();
        command!.AddContactChannels.Count.ShouldBe(1);
        command.AddContactChannels[0].Type.ShouldBe(ContactChannelType.Email);
        command.AddContactChannels[0].Value.ShouldBe("new@example.com");
        command.RemoveContactChannelIds.Count.ShouldBe(0);
        command.UpdateContactChannels.Count.ShouldBe(0);
        command.PersonDetails.ShouldBeNull();
        command.OrganizationDetails.ShouldBeNull();
    }

    [Fact]
    public async Task UpdatePartyAsync_RemoveChannelOnly_ConstructsPatchWithOnlyRemoveIdsAsync()
    {
        using TenantScope _ = TenantScope.Create("tenant-a");

        string partyId = Guid.NewGuid().ToString();
        string channelId = Guid.NewGuid().ToString();
        PartyDetail currentParty = CreatePartyDetail(partyId, isActive: true, channelId);

        SubmitCommand? routedCommand = null;
        ICommandRouter router = Substitute.For<ICommandRouter>();
        router
            .RouteCommandAsync(Arg.Do<SubmitCommand>(cmd => routedCommand = cmd), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CommandProcessingResult(true)));

        IPartyDetailProjectionActor projectionActor = Substitute.For<IPartyDetailProjectionActor>();
        projectionActor.GetDetailAsync().Returns(currentParty, currentParty);

        IActorProxyFactory actorProxyFactory = CreateActorProxyFactory(projectionActor);
        ServiceProvider services = BuildServices(router, actorProxyFactory, new InlineValidator<UpdatePartyComposite>());

        await UpdatePartyMcpTool.UpdatePartyAsync(
            partyId: partyId,
            services: services,
            removeContactChannelIds: channelId);

        routedCommand.ShouldNotBeNull();
        UpdatePartyComposite? command = JsonSerializer.Deserialize<UpdatePartyComposite>(routedCommand!.Payload);
        command.ShouldNotBeNull();
        command!.RemoveContactChannelIds.Count.ShouldBe(1);
        command.RemoveContactChannelIds[0].ShouldBe(channelId);
        command.AddContactChannels.Count.ShouldBe(0);
        command.UpdateContactChannels.Count.ShouldBe(0);
        command.PersonDetails.ShouldBeNull();
        command.OrganizationDetails.ShouldBeNull();
    }

    [Fact]
    public async Task UpdatePartyAsync_MixedOperations_AllListsPopulatedAsync()
    {
        using TenantScope _ = TenantScope.Create("tenant-a");

        string partyId = Guid.NewGuid().ToString();
        string removeChannelId = Guid.NewGuid().ToString();
        PartyDetail currentParty = CreatePartyDetail(partyId, isActive: true, removeChannelId);

        SubmitCommand? routedCommand = null;
        ICommandRouter router = Substitute.For<ICommandRouter>();
        router
            .RouteCommandAsync(Arg.Do<SubmitCommand>(cmd => routedCommand = cmd), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CommandProcessingResult(true)));

        IPartyDetailProjectionActor projectionActor = Substitute.For<IPartyDetailProjectionActor>();
        projectionActor.GetDetailAsync().Returns(currentParty, currentParty);

        IActorProxyFactory actorProxyFactory = CreateActorProxyFactory(projectionActor);
        ServiceProvider services = BuildServices(router, actorProxyFactory, new InlineValidator<UpdatePartyComposite>());

        await UpdatePartyMcpTool.UpdatePartyAsync(
            partyId: partyId,
            services: services,
            firstName: "Pierre",
            addEmail: "new@example.com",
            removeContactChannelIds: removeChannelId);

        routedCommand.ShouldNotBeNull();
        UpdatePartyComposite? command = JsonSerializer.Deserialize<UpdatePartyComposite>(routedCommand!.Payload);
        command.ShouldNotBeNull();
        command!.PersonDetails.ShouldNotBeNull();
        command.PersonDetails!.FirstName.ShouldBe("Pierre");
        command.AddContactChannels.Count.ShouldBe(1);
        command.RemoveContactChannelIds.Count.ShouldBe(1);
    }

    [Fact]
    public async Task UpdatePartyAsync_PersonDetailsPatch_MergesWithCurrentStateAsync()
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
        ServiceProvider services = BuildServices(router, actorProxyFactory, new InlineValidator<UpdatePartyComposite>());

        await UpdatePartyMcpTool.UpdatePartyAsync(
            partyId: partyId,
            services: services,
            firstName: "Pierre");

        routedCommand.ShouldNotBeNull();
        UpdatePartyComposite? command = JsonSerializer.Deserialize<UpdatePartyComposite>(routedCommand!.Payload);
        command.ShouldNotBeNull();
        command!.PersonDetails.ShouldNotBeNull();
        command.PersonDetails!.FirstName.ShouldBe("Pierre");
        command.PersonDetails.LastName.ShouldBe("Dupont");
    }

    [Fact]
    public async Task UpdatePartyAsync_GeneratesUuidForNewChannelAsync()
    {
        using TenantScope tenantScope = TenantScope.Create("tenant-a");

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
        ServiceProvider services = BuildServices(router, actorProxyFactory, new InlineValidator<UpdatePartyComposite>());

        await UpdatePartyMcpTool.UpdatePartyAsync(
            partyId: partyId,
            services: services,
            addEmail: "new@example.com");

        routedCommand.ShouldNotBeNull();
        UpdatePartyComposite? command = JsonSerializer.Deserialize<UpdatePartyComposite>(routedCommand!.Payload);
        command.ShouldNotBeNull();
        command!.AddContactChannels.Count.ShouldBe(1);
        Guid.TryParse(command.AddContactChannels[0].ContactChannelId, out _).ShouldBeTrue();
    }

    [Fact]
    public async Task UpdatePartyAsync_NonExistentParty_ThrowsNotFoundErrorAsync()
    {
        using TenantScope _ = TenantScope.Create("tenant-a");

        string partyId = Guid.NewGuid().ToString();

        IPartyDetailProjectionActor projectionActor = Substitute.For<IPartyDetailProjectionActor>();
        projectionActor.GetDetailAsync().Returns((PartyDetail?)null);

        IActorProxyFactory actorProxyFactory = CreateActorProxyFactory(projectionActor);
        ServiceProvider services = BuildServices(
            Substitute.For<ICommandRouter>(),
            actorProxyFactory,
            new InlineValidator<UpdatePartyComposite>());

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => UpdatePartyMcpTool.UpdatePartyAsync(
                partyId: partyId,
                services: services,
                firstName: "Pierre"));

        exception.Message.ShouldBe($"Party not found. No party exists with ID '{partyId}'.");
    }

    [Fact]
    public async Task UpdatePartyAsync_NoChangesSpecified_ThrowsValidationErrorAsync()
    {
        using TenantScope _ = TenantScope.Create("tenant-a");

        string partyId = Guid.NewGuid().ToString();

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => UpdatePartyMcpTool.UpdatePartyAsync(
                partyId: partyId,
                services: new ServiceCollection().AddSingleton<Hexalith.Parties.Authorization.ITenantAccessService, Hexalith.Parties.Tests.Authorization.TestTenantAccessService>().BuildServiceProvider()));

        exception.Message.ShouldBe("No changes specified. Provide at least one field to update.");
    }

    [Fact]
    public async Task UpdatePartyAsync_MissingTenant_ThrowsAuthenticationErrorAsync()
    {
        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => UpdatePartyMcpTool.UpdatePartyAsync(
                partyId: Guid.NewGuid().ToString(),
                services: new ServiceCollection().AddSingleton<Hexalith.Parties.Authorization.ITenantAccessService, Hexalith.Parties.Tests.Authorization.TestTenantAccessService>().BuildServiceProvider(),
                firstName: "Pierre"));

        exception.Message.ShouldContain("missing-tenant");
    }

    [Fact]
    public async Task UpdatePartyAsync_InvalidRemoveChannelId_ThrowsValidationErrorAsync()
    {
        using TenantScope _ = TenantScope.Create("tenant-a");

        string partyId = Guid.NewGuid().ToString();
        PartyDetail currentParty = CreatePartyDetail(partyId, isActive: true);

        IPartyDetailProjectionActor projectionActor = Substitute.For<IPartyDetailProjectionActor>();
        projectionActor.GetDetailAsync().Returns(currentParty);

        IActorProxyFactory actorProxyFactory = CreateActorProxyFactory(projectionActor);
        ServiceProvider services = BuildServices(
            Substitute.For<ICommandRouter>(),
            actorProxyFactory,
            new InlineValidator<UpdatePartyComposite>());

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => UpdatePartyMcpTool.UpdatePartyAsync(
                partyId: partyId,
                services: services,
                removeContactChannelIds: "not-a-guid"));

        exception.Message.ShouldContain("not-a-guid");
        exception.Message.ShouldContain("UUID");
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
            .AddSingleton<Hexalith.Parties.Authorization.ITenantAccessService, Hexalith.Parties.Tests.Authorization.TestTenantAccessService>()
            .BuildServiceProvider();

    private static IActorProxyFactory CreateActorProxyFactory(IPartyDetailProjectionActor projectionActor)
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        actorProxyFactory
            .CreateActorProxy<IPartyDetailProjectionActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(projectionActor);

        return actorProxyFactory;
    }

    private static PartyDetail CreatePartyDetail(string partyId, bool isActive, string? channelId = null)
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
            ContactChannels = channelId is null
                ? []
                :
                [
                    new ContactChannel
                    {
                        Id = channelId,
                        Type = ContactChannelType.Email,
                        Value = "old@example.com",
                        IsPreferred = false,
                    },
                ],
            Identifiers = [],
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            LastModifiedAt = DateTimeOffset.UtcNow,
        };

    private sealed class TenantScope : IDisposable
    {
        private static readonly FieldInfo _tenantField = typeof(UpdatePartyMcpTool)
            .Assembly
            .GetType("Hexalith.Parties.Mcp.McpSessionContext", throwOnError: true)!
            .GetField("Tenant", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;

        private static readonly FieldInfo _userIdField = typeof(UpdatePartyMcpTool)
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
