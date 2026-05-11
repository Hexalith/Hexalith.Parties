using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.ValueObjects;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Shouldly;

namespace Hexalith.Parties.Tests.Gateway;

public sealed class PartiesProcessEndpointTests
{
    [Fact]
    public async Task PostProcess_InvokesRegisteredPartiesDomainServiceAsync()
    {
        using var factory = new PartiesProcessTestFactory();
        using HttpClient client = factory.CreateClient();
        var command = new CommandEnvelope(
            MessageId: "cmd-12-4-process",
            TenantId: "tenant-a",
            Domain: "party",
            AggregateId: "party-process",
            CommandType: "Hexalith.Parties.Contracts.Commands.CreatePartyComposite",
            Payload: JsonSerializer.SerializeToUtf8Bytes(new { PartyId = "party-process" }),
            CorrelationId: "cmd-12-4-process",
            CausationId: null,
            UserId: "user-a",
            Extensions: null);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/process",
            new DomainServiceRequest(command, CurrentState: null));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        DomainServiceWireResult result = (await response.Content.ReadFromJsonAsync<DomainServiceWireResult>())
            .ShouldNotBeNull();
        result.IsRejection.ShouldBeFalse();
        result.Events.ShouldHaveSingleItem().EventTypeName.ShouldBe(typeof(PartyCreated).FullName);
        factory.Invoker.ReceivedCommands.ShouldHaveSingleItem().Domain.ShouldBe("party");
    }

    private sealed class PartiesProcessTestFactory : WebApplicationFactory<Program>
    {
        public CapturingDomainServiceInvoker Invoker { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.UseEnvironment("Development");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDomainServiceInvoker>();
                services.AddSingleton<IDomainServiceInvoker>(Invoker);
            });
        }
    }

    private sealed class CapturingDomainServiceInvoker : IDomainServiceInvoker
    {
        private readonly List<CommandEnvelope> _receivedCommands = [];

        public IReadOnlyList<CommandEnvelope> ReceivedCommands => _receivedCommands;

        public Task<DomainResult> InvokeAsync(
            CommandEnvelope command,
            object? currentState,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);
            cancellationToken.ThrowIfCancellationRequested();
            _receivedCommands.Add(command);
            return Task.FromResult(DomainResult.Success(
                [new PartyCreated { Type = PartyType.Person }]));
        }
    }
}
