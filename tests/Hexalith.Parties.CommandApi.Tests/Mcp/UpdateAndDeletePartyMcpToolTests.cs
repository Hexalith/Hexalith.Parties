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

public sealed class UpdateAndDeletePartyMcpToolTests
{
    [Fact]
    public async Task UpdatePartyAsync_ExistingContactChannelUpdate_MapsToCompositeUpdateListAsync()
    {
        using TenantScope tenantScope = TenantScope.Create("tenant-a");

        string partyId = Guid.NewGuid().ToString();
        string channelId = Guid.NewGuid().ToString();
        PartyDetail currentParty = CreatePartyDetail(partyId, isActive: true, channelId);

        SubmitCommand? routedCommand = null;
        ICommandRouter router = Substitute.For<ICommandRouter>();
        router
            .RouteCommandAsync(Arg.Do<SubmitCommand>(command => routedCommand = command), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CommandProcessingResult(true)));

        IPartyDetailProjectionActor projectionActor = Substitute.For<IPartyDetailProjectionActor>();
        projectionActor.GetDetailAsync().Returns(currentParty, currentParty);

        IActorProxyFactory actorProxyFactory = CreateActorProxyFactory(projectionActor);
        ServiceProvider services = BuildServices(
            router,
            actorProxyFactory,
            new InlineValidator<UpdatePartyComposite>());

        string json = await UpdatePartyMcpTool.UpdatePartyAsync(
            partyId: partyId,
            services: services,
            updateContactChannelId: channelId,
            updateContactChannelValue: "new@example.com",
            updateContactChannelIsPreferred: true);

        json.ShouldNotBeNullOrWhiteSpace();

        routedCommand.ShouldNotBeNull();
        routedCommand!.CommandType.ShouldBe(nameof(UpdatePartyComposite));

        UpdatePartyComposite? command = JsonSerializer.Deserialize<UpdatePartyComposite>(routedCommand.Payload);
        command.ShouldNotBeNull();
        command!.UpdateContactChannels.Count.ShouldBe(1);
        command.UpdateContactChannels[0].PartyId.ShouldBe(partyId);
        command.UpdateContactChannels[0].ContactChannelId.ShouldBe(channelId);
        command.UpdateContactChannels[0].Value.ShouldBe("new@example.com");
        command.UpdateContactChannels[0].IsPreferred.ShouldBe(true);
        command.AddContactChannels.Count.ShouldBe(0);
        command.RemoveContactChannelIds.Count.ShouldBe(0);
        command.PersonDetails.ShouldBeNull();
        command.OrganizationDetails.ShouldBeNull();
    }

    [Fact]
    public async Task DeletePartyAsync_AcceptedWithStaleProjection_ReturnsInactivePartyAsync()
    {
        using TenantScope tenantScope = TenantScope.Create("tenant-a");

        string partyId = Guid.NewGuid().ToString();
        PartyDetail currentParty = CreatePartyDetail(partyId, isActive: true);

        ICommandRouter router = Substitute.For<ICommandRouter>();
        router
            .RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CommandProcessingResult(true)));

        IPartyDetailProjectionActor projectionActor = Substitute.For<IPartyDetailProjectionActor>();
        projectionActor.GetDetailAsync().Returns(currentParty, currentParty);

        IActorProxyFactory actorProxyFactory = CreateActorProxyFactory(projectionActor);
        ServiceProvider services = BuildServices(
            router,
            actorProxyFactory,
            new InlineValidator<DeactivateParty>());

        string json = await DeletePartyMcpTool.DeletePartyAsync(partyId, services);

        using JsonDocument document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("id").GetString().ShouldBe(partyId);
        document.RootElement.GetProperty("isActive").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task DeletePartyAsync_RejectedButProjectionAlreadyInactive_ReturnsInactivePartyAsync()
    {
        using TenantScope tenantScope = TenantScope.Create("tenant-a");

        string partyId = Guid.NewGuid().ToString();
        PartyDetail currentParty = CreatePartyDetail(partyId, isActive: true);
        PartyDetail inactiveParty = currentParty with { IsActive = false };

        ICommandRouter router = Substitute.For<ICommandRouter>();
        router
            .RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CommandProcessingResult(false, "concurrent deactivation")));

        IPartyDetailProjectionActor projectionActor = Substitute.For<IPartyDetailProjectionActor>();
        projectionActor.GetDetailAsync().Returns(currentParty, inactiveParty);

        IActorProxyFactory actorProxyFactory = CreateActorProxyFactory(projectionActor);
        ServiceProvider services = BuildServices(
            router,
            actorProxyFactory,
            new InlineValidator<DeactivateParty>());

        string json = await DeletePartyMcpTool.DeletePartyAsync(partyId, services);

        using JsonDocument document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("isActive").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task DeletePartyAsync_RejectedAndStillActive_ThrowsActionableErrorAsync()
    {
        using TenantScope tenantScope = TenantScope.Create("tenant-a");

        string partyId = Guid.NewGuid().ToString();
        PartyDetail currentParty = CreatePartyDetail(partyId, isActive: true);

        ICommandRouter router = Substitute.For<ICommandRouter>();
        router
            .RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CommandProcessingResult(false, "backend timeout")));

        IPartyDetailProjectionActor projectionActor = Substitute.For<IPartyDetailProjectionActor>();
        projectionActor.GetDetailAsync().Returns(currentParty, currentParty);

        IActorProxyFactory actorProxyFactory = CreateActorProxyFactory(projectionActor);
        ServiceProvider services = BuildServices(
            router,
            actorProxyFactory,
            new InlineValidator<DeactivateParty>());

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => DeletePartyMcpTool.DeletePartyAsync(partyId, services));

        exception.Message.ShouldBe("Deactivation failed: backend timeout");
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
            .GetType("Hexalith.Parties.CommandApi.Mcp.McpSessionContext", throwOnError: true)!
            .GetField("Tenant", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;

        private readonly AsyncLocal<string?> _tenant;
        private readonly string? _previousValue;

        private TenantScope(string value)
        {
            _tenant = (AsyncLocal<string?>)_tenantField.GetValue(null)!;
            _previousValue = _tenant.Value;
            _tenant.Value = value;
        }

        public static TenantScope Create(string value) => new(value);

        public void Dispose() => _tenant.Value = _previousValue;
    }
}
