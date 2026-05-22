using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Results;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Compliance;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
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
        response.Headers.TryGetValues(MvpComplianceWarning.HeaderName, out IEnumerable<string>? warningValues).ShouldBeTrue();
        string warning = warningValues.ShouldHaveSingleItem();
        warning.ShouldBe(MvpComplianceWarning.Message);
        warning.ShouldContain("not for regulated EU personal data");
        warning.ShouldContain("v1.1");
        warning.ShouldNotContain("tenant-a");
        warning.ShouldNotContain("user-a");
        response.Headers.Contains(RetiredGdprWarningHeader()).ShouldBeFalse();

        DomainServiceWireResult result = (await response.Content.ReadFromJsonAsync<DomainServiceWireResult>())
            .ShouldNotBeNull();
        result.IsRejection.ShouldBeFalse();
        result.Events.ShouldHaveSingleItem().EventTypeName.ShouldBe(typeof(PartyCreated).FullName);
        factory.Invoker.ReceivedCommands.ShouldHaveSingleItem().Domain.ShouldBe("party");
    }

    [Fact]
    public async Task PostProcess_PreservesResultPayloadAcrossPartiesHostWireSerializationAsync()
    {
        // P12: prove the enriched result payload survives the Parties /process endpoint
        // round-trip (DomainResult.ResultPayload → DomainServiceWireResult.ResultPayload).
        var payloadInvoker = new PayloadProducingDomainServiceInvoker();
        using var factory = new PartiesProcessTestFactory(payloadInvoker);
        using HttpClient client = factory.CreateClient();
        var command = new CommandEnvelope(
            MessageId: "cmd-1-9-process-payload",
            TenantId: "tenant-a",
            Domain: "party",
            AggregateId: "party-process-payload",
            CommandType: "Hexalith.Parties.Contracts.Commands.CreatePartyComposite",
            Payload: JsonSerializer.SerializeToUtf8Bytes(new { PartyId = "party-process-payload" }),
            CorrelationId: "cmd-1-9-process-payload",
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
        result.ResultPayload.ShouldNotBeNullOrWhiteSpace();
        result.ResultPayload.ShouldContain("\"id\":\"party-process-payload\"");
        result.ResultPayload.ShouldContain("\"type\":\"Person\"");
        result.ResultPayload.ShouldContain("\"displayName\":\"Ada Lovelace\"");
    }

    [Fact]
    public async Task PostProcess_WhenGdprFeaturesActive_DoesNotEmitMvpComplianceWarningHeaderAsync()
    {
        using var factory = new PartiesProcessTestFactory(gdprFeaturesActive: true);
        using HttpClient client = factory.CreateClient();
        var command = new CommandEnvelope(
            MessageId: "cmd-3-10-active",
            TenantId: "tenant-a",
            Domain: "party",
            AggregateId: "party-process-active",
            CommandType: "Hexalith.Parties.Contracts.Commands.CreatePartyComposite",
            Payload: JsonSerializer.SerializeToUtf8Bytes(new { PartyId = "party-process-active" }),
            CorrelationId: "cmd-3-10-active",
            CausationId: null,
            UserId: "user-a",
            Extensions: null);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/process",
            new DomainServiceRequest(command, CurrentState: null));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.Contains(MvpComplianceWarning.HeaderName).ShouldBeFalse();
        response.Headers.Contains(RetiredGdprWarningHeader()).ShouldBeFalse();
    }

    private static string RetiredGdprWarningHeader() => "X-" + "GDPR-Warning";

    private sealed class PartiesProcessTestFactory : WebApplicationFactory<Program>
    {
        private readonly IDomainServiceInvoker _registeredInvoker;
        private readonly bool _gdprFeaturesActive;

        public PartiesProcessTestFactory(bool gdprFeaturesActive = false)
        {
            Invoker = new CapturingDomainServiceInvoker();
            _registeredInvoker = Invoker;
            _gdprFeaturesActive = gdprFeaturesActive;
        }

        public PartiesProcessTestFactory(IDomainServiceInvoker registeredInvoker, bool gdprFeaturesActive = false)
        {
            ArgumentNullException.ThrowIfNull(registeredInvoker);
            Invoker = new CapturingDomainServiceInvoker();
            _registeredInvoker = registeredInvoker;
            _gdprFeaturesActive = gdprFeaturesActive;
        }

        public CapturingDomainServiceInvoker Invoker { get; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration(configuration =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [MvpComplianceWarning.ActivationConfigurationKey] = _gdprFeaturesActive.ToString(),
                });
            });
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDomainServiceInvoker>();
                services.AddSingleton(_registeredInvoker);
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

    private sealed class PayloadProducingDomainServiceInvoker : IDomainServiceInvoker
    {
        public Task<DomainResult> InvokeAsync(
            CommandEnvelope command,
            object? currentState,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);
            cancellationToken.ThrowIfCancellationRequested();
            var detail = new PartyDetail
            {
                Id = command.AggregateId,
                Type = PartyType.Person,
                IsActive = true,
                DisplayName = "Ada Lovelace",
                SortName = "Lovelace, Ada",
            };
            IEventPayload[] events = [new PartyCreated { Type = PartyType.Person }];
            return Task.FromResult<DomainResult>(new PartyCommandResult(events, detail));
        }
    }
}
