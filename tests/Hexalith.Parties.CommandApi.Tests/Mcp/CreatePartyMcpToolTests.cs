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
using Hexalith.Parties.Projections.Abstractions;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.CommandApi.Tests.Mcp;

public sealed class CreatePartyMcpToolTests
{
    [Fact]
    public async Task CreatePartyAsync_TypeMissing_ReturnsActionableValidationErrorAsync()
    {
        using TenantScope _ = TenantScope.Create("tenant-a");

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => CreatePartyMcpTool.CreatePartyAsync(
                type: null,
                services: Substitute.For<IServiceProvider>(),
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
                services: Substitute.For<IServiceProvider>(),
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

    private sealed class TenantScope : IDisposable
    {
        private static readonly FieldInfo _tenantField = typeof(CreatePartyMcpTool)
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
