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

public sealed class CreatePartyMcpToolTests
{
    [Fact]
    public async Task CreatePartyAsync_TypeMissing_ReturnsActionableValidationErrorAsync()
    {
        using TenantScope _ = TenantScope.Create("tenant-a");

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => CreatePartyMcpTool.CreatePartyAsync(
                type: null,
                services: new ServiceCollection().AddSingleton<Hexalith.Parties.Authorization.ITenantAccessService, Hexalith.Parties.Tests.Authorization.TestTenantAccessService>().BuildServiceProvider(),
                lastName: "Dupont"));

        exception.Message.ShouldBe("Party type is required. Must be 'Person' or 'Organization'.");
    }

    [Fact]
    public async Task CreatePartyAsync_InvalidDateOfBirth_ReturnsIso8601ValidationErrorAsync()
    {
        using TenantScope _ = TenantScope.Create("tenant-a");

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => CreatePartyMcpTool.CreatePartyAsync(
                type: "person",
                services: new ServiceCollection().AddSingleton<Hexalith.Parties.Authorization.ITenantAccessService, Hexalith.Parties.Tests.Authorization.TestTenantAccessService>().BuildServiceProvider(),
                lastName: "Dupont",
                dateOfBirth: "03/06/2026"));

        exception.Message.ShouldBe("Date of birth must be a valid ISO 8601 date or date-time (for example '1990-01-15').");
    }

    [Fact]
    public async Task CreatePartyAsync_FallbackResponse_UsesAggregateDisplayNameRulesAsync()
    {
        using TenantScope _ = TenantScope.Create("tenant-a");

        ICommandRouter router = Substitute.For<ICommandRouter>();
        router
            .RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CommandProcessingResult(true)));

        IPartyDetailProjectionActor projectionActor = Substitute.For<IPartyDetailProjectionActor>();
        projectionActor.GetDetailAsync().Returns((Hexalith.Parties.Contracts.Models.PartyDetail?)null);

        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        actorProxyFactory
            .CreateActorProxy<IPartyDetailProjectionActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(projectionActor);

        ServiceProvider services = new ServiceCollection()
            .AddSingleton(router)
            .AddSingleton(actorProxyFactory)
            .AddSingleton<IValidator<CreatePartyComposite>>(new InlineValidator<CreatePartyComposite>())
            .AddSingleton<Hexalith.Parties.Authorization.ITenantAccessService, Hexalith.Parties.Tests.Authorization.TestTenantAccessService>()
            .BuildServiceProvider();

        string json = await CreatePartyMcpTool.CreatePartyAsync(
            type: "person",
            services: services,
            firstName: null,
            lastName: "Dupont");

        using JsonDocument document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("displayName").GetString().ShouldBe(" Dupont");
        document.RootElement.GetProperty("sortName").GetString().ShouldBe("Dupont, ");
    }

    [Fact]
    public async Task CreatePartyAsync_FullPersonInput_DispatchesCorrectCompositeCommandAsync()
    {
        using TenantScope tenantScope = TenantScope.Create("tenant-a");

        SubmitCommand? routedCommand = null;
        ICommandRouter router = Substitute.For<ICommandRouter>();
        router
            .RouteCommandAsync(Arg.Do<SubmitCommand>(cmd => routedCommand = cmd), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CommandProcessingResult(true)));

        IPartyDetailProjectionActor projectionActor = Substitute.For<IPartyDetailProjectionActor>();
        projectionActor.GetDetailAsync().Returns((PartyDetail?)null);

        IActorProxyFactory actorProxyFactory = CreateActorProxyFactory(projectionActor);
        ServiceProvider services = BuildServices(router, actorProxyFactory, new InlineValidator<CreatePartyComposite>());

        await CreatePartyMcpTool.CreatePartyAsync(
            type: "person",
            services: services,
            firstName: "Jean",
            lastName: "Dupont",
            dateOfBirth: "1990-01-15",
            email: "jean@example.com",
            phone: "+33612345678",
            vatNumber: "FR12345678901");

        routedCommand.ShouldNotBeNull();
        routedCommand!.CommandType.ShouldBe(nameof(CreatePartyComposite));
        routedCommand.Domain.ShouldBe("party");

        CreatePartyComposite? command = JsonSerializer.Deserialize<CreatePartyComposite>(routedCommand.Payload);
        command.ShouldNotBeNull();
        command!.PartyId.ShouldNotBeNullOrWhiteSpace();
        Guid.TryParse(command.PartyId, out _).ShouldBeTrue();
        command.Type.ShouldBe(PartyType.Person);
        command.PersonDetails.ShouldNotBeNull();
        command.PersonDetails!.FirstName.ShouldBe("Jean");
        command.PersonDetails.LastName.ShouldBe("Dupont");
        command.PersonDetails.DateOfBirth.ShouldNotBeNull();
        command.ContactChannels.Count.ShouldBe(2);
        command.ContactChannels.ShouldContain(c => c.Type == ContactChannelType.Email && c.Value == "jean@example.com");
        command.ContactChannels.ShouldContain(c => c.Type == ContactChannelType.Phone && c.Value == "+33612345678");
        command.Identifiers.Count.ShouldBe(1);
        command.Identifiers[0].Type.ShouldBe(IdentifierType.VAT);
        command.Identifiers[0].Value.ShouldBe("FR12345678901");
    }

    [Fact]
    public async Task CreatePartyAsync_PartialInput_LastNameOnly_AcceptedSuccessfullyAsync()
    {
        using TenantScope _ = TenantScope.Create("tenant-a");

        SubmitCommand? routedCommand = null;
        ICommandRouter router = Substitute.For<ICommandRouter>();
        router
            .RouteCommandAsync(Arg.Do<SubmitCommand>(cmd => routedCommand = cmd), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CommandProcessingResult(true)));

        IPartyDetailProjectionActor projectionActor = Substitute.For<IPartyDetailProjectionActor>();
        projectionActor.GetDetailAsync().Returns((PartyDetail?)null);

        IActorProxyFactory actorProxyFactory = CreateActorProxyFactory(projectionActor);
        ServiceProvider services = BuildServices(router, actorProxyFactory, new InlineValidator<CreatePartyComposite>());

        await CreatePartyMcpTool.CreatePartyAsync(
            type: "person",
            services: services,
            lastName: "Dupont");

        routedCommand.ShouldNotBeNull();

        CreatePartyComposite? command = JsonSerializer.Deserialize<CreatePartyComposite>(routedCommand!.Payload);
        command.ShouldNotBeNull();
        command!.PersonDetails.ShouldNotBeNull();
        command.PersonDetails!.FirstName.ShouldBe(string.Empty);
        command.PersonDetails.LastName.ShouldBe("Dupont");
    }

    [Fact]
    public async Task CreatePartyAsync_OrganizationWithVat_ConstructsCorrectCompositeAsync()
    {
        using TenantScope _ = TenantScope.Create("tenant-a");

        SubmitCommand? routedCommand = null;
        ICommandRouter router = Substitute.For<ICommandRouter>();
        router
            .RouteCommandAsync(Arg.Do<SubmitCommand>(cmd => routedCommand = cmd), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CommandProcessingResult(true)));

        IPartyDetailProjectionActor projectionActor = Substitute.For<IPartyDetailProjectionActor>();
        projectionActor.GetDetailAsync().Returns((PartyDetail?)null);

        IActorProxyFactory actorProxyFactory = CreateActorProxyFactory(projectionActor);
        ServiceProvider services = BuildServices(router, actorProxyFactory, new InlineValidator<CreatePartyComposite>());

        await CreatePartyMcpTool.CreatePartyAsync(
            type: "organization",
            services: services,
            legalName: "Acme SAS",
            vatNumber: "FR98765432101");

        routedCommand.ShouldNotBeNull();

        CreatePartyComposite? command = JsonSerializer.Deserialize<CreatePartyComposite>(routedCommand!.Payload);
        command.ShouldNotBeNull();
        command!.Type.ShouldBe(PartyType.Organization);
        command.OrganizationDetails.ShouldNotBeNull();
        command.OrganizationDetails!.LegalName.ShouldBe("Acme SAS");
        command.PersonDetails.ShouldBeNull();
        command.Identifiers.Count.ShouldBe(1);
        command.Identifiers[0].Type.ShouldBe(IdentifierType.VAT);
        command.Identifiers[0].Value.ShouldBe("FR98765432101");
    }

    [Fact]
    public async Task CreatePartyAsync_GeneratesUuidsForChannelAndIdentifierAsync()
    {
        using TenantScope tenantScope = TenantScope.Create("tenant-a");

        SubmitCommand? routedCommand = null;
        ICommandRouter router = Substitute.For<ICommandRouter>();
        router
            .RouteCommandAsync(Arg.Do<SubmitCommand>(cmd => routedCommand = cmd), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CommandProcessingResult(true)));

        IPartyDetailProjectionActor projectionActor = Substitute.For<IPartyDetailProjectionActor>();
        projectionActor.GetDetailAsync().Returns((PartyDetail?)null);

        IActorProxyFactory actorProxyFactory = CreateActorProxyFactory(projectionActor);
        ServiceProvider services = BuildServices(router, actorProxyFactory, new InlineValidator<CreatePartyComposite>());

        await CreatePartyMcpTool.CreatePartyAsync(
            type: "person",
            services: services,
            lastName: "Dupont",
            email: "jean@example.com",
            vatNumber: "FR12345678901");

        routedCommand.ShouldNotBeNull();

        CreatePartyComposite? command = JsonSerializer.Deserialize<CreatePartyComposite>(routedCommand!.Payload);
        command.ShouldNotBeNull();
        command!.ContactChannels.Count.ShouldBe(1);
        Guid.TryParse(command.ContactChannels[0].ContactChannelId, out _).ShouldBeTrue();
        command.Identifiers.Count.ShouldBe(1);
        Guid.TryParse(command.Identifiers[0].IdentifierId, out _).ShouldBeTrue();
    }

    [Fact]
    public async Task CreatePartyAsync_SuccessfulCreate_ReturnsCompletePartyDetailJsonAsync()
    {
        using TenantScope tenantScope = TenantScope.Create("tenant-a");

        ICommandRouter router = Substitute.For<ICommandRouter>();
        router
            .RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CommandProcessingResult(true)));

        IPartyDetailProjectionActor projectionActor = Substitute.For<IPartyDetailProjectionActor>();
        projectionActor.GetDetailAsync().Returns((PartyDetail?)null);

        IActorProxyFactory actorProxyFactory = CreateActorProxyFactory(projectionActor);
        ServiceProvider services = BuildServices(router, actorProxyFactory, new InlineValidator<CreatePartyComposite>());

        string json = await CreatePartyMcpTool.CreatePartyAsync(
            type: "person",
            services: services,
            firstName: "Jean",
            lastName: "Dupont",
            email: "jean@example.com");

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        root.TryGetProperty("id", out _).ShouldBeTrue();
        root.GetProperty("type").GetString().ShouldBe("Person");
        root.GetProperty("isActive").GetBoolean().ShouldBeTrue();
        root.GetProperty("displayName").GetString().ShouldNotBeNullOrWhiteSpace();
        root.GetProperty("sortName").GetString().ShouldNotBeNullOrWhiteSpace();
        root.TryGetProperty("personDetails", out _).ShouldBeTrue();
        root.TryGetProperty("contactChannels", out _).ShouldBeTrue();
        root.TryGetProperty("identifiers", out _).ShouldBeTrue();
        root.TryGetProperty("createdAt", out _).ShouldBeTrue();
        root.TryGetProperty("lastModifiedAt", out _).ShouldBeTrue();
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

    // P8 — MCP missing-user denial: a session with tenant context but no
    // authenticated user id must deny the write tool with reason missing-user
    // BEFORE any payload validation, projection read, or command dispatch.
    // No `mcp-agent` fallback.
    [Fact]
    public async Task CreatePartyAsync_MissingUserId_ThrowsMissingUserAuthorizationErrorAsync()
    {
        using TenantOnlyScope _ = TenantOnlyScope.Create("tenant-a");

        ICommandRouter router = Substitute.For<ICommandRouter>();

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => CreatePartyMcpTool.CreatePartyAsync(
                type: "person",
                services: new ServiceCollection()
                    .AddSingleton(router)
                    .AddSingleton<Hexalith.Parties.Authorization.ITenantAccessService, Hexalith.Parties.Tests.Authorization.TestTenantAccessService>()
                    .BuildServiceProvider(),
                lastName: "Dupont"));

        exception.Message.ShouldContain("missing-user");
        await router.DidNotReceive().RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>());
    }

    private sealed class TenantScope : IDisposable
    {
        private static readonly FieldInfo _tenantField = typeof(CreatePartyMcpTool)
            .Assembly
            .GetType("Hexalith.Parties.Mcp.McpSessionContext", throwOnError: true)!
            .GetField("Tenant", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;

        private static readonly FieldInfo _userIdField = typeof(CreatePartyMcpTool)
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

    // Sets Tenant but leaves UserId null — simulates a session where the
    // authenticated principal lacks a usable subject identifier.
    private sealed class TenantOnlyScope : IDisposable
    {
        private static readonly FieldInfo _tenantField = typeof(CreatePartyMcpTool)
            .Assembly
            .GetType("Hexalith.Parties.Mcp.McpSessionContext", throwOnError: true)!
            .GetField("Tenant", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;

        private static readonly FieldInfo _userIdField = typeof(CreatePartyMcpTool)
            .Assembly
            .GetType("Hexalith.Parties.Mcp.McpSessionContext", throwOnError: true)!
            .GetField("UserId", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;

        private readonly AsyncLocal<string?> _tenant;
        private readonly AsyncLocal<string?> _userId;
        private readonly string? _previousTenant;
        private readonly string? _previousUserId;

        private TenantOnlyScope(string tenant)
        {
            _tenant = (AsyncLocal<string?>)_tenantField.GetValue(null)!;
            _userId = (AsyncLocal<string?>)_userIdField.GetValue(null)!;
            _previousTenant = _tenant.Value;
            _previousUserId = _userId.Value;
            _tenant.Value = tenant;
            _userId.Value = null;
        }

        public static TenantOnlyScope Create(string tenant) => new(tenant);

        public void Dispose()
        {
            _tenant.Value = _previousTenant;
            _userId.Value = _previousUserId;
        }
    }
}
