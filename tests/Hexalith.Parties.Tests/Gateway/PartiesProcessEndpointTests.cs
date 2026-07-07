using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Replay;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.Parties.Contracts.Commands;
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

using Shouldly;

namespace Hexalith.Parties.Tests.Gateway;

public sealed class PartiesProcessEndpointTests
{
    [Fact]
    public async Task PostProcess_InvokesRegisteredPartiesDomainProcessorAsync()
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
        factory.Processor.ShouldNotBeNull().ReceivedCommands.ShouldHaveSingleItem().Domain.ShouldBe("party");
    }

    [Fact]
    public async Task PostProcess_PreservesResultPayloadAcrossPartiesHostWireSerializationAsync()
    {
        // P12: prove the enriched result payload survives the Parties /process endpoint
        // round-trip (DomainResult.ResultPayload → DomainServiceWireResult.ResultPayload).
        var payloadProcessor = new PayloadProducingDomainProcessor();
        using var factory = new PartiesProcessTestFactory(payloadProcessor);
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

    [Fact]
    public async Task PostProcess_InvalidPayload_UsesProductionProcessorValidationRejectionAsync()
    {
        using var factory = new PartiesProcessTestFactory(useProductionProcessor: true);
        using HttpClient client = factory.CreateClient();
        var command = new CommandEnvelope(
            MessageId: "cmd-8-5-invalid-payload",
            TenantId: "tenant-a",
            Domain: "party",
            AggregateId: "party-invalid-payload",
            CommandType: typeof(Hexalith.Parties.Contracts.Commands.CreatePartyComposite).FullName!,
            Payload: JsonSerializer.SerializeToUtf8Bytes(new
            {
                PartyId = "party-invalid-payload",
                Type = "Person",
            }),
            CorrelationId: "cmd-8-5-invalid-payload",
            CausationId: null,
            UserId: "user-a",
            Extensions: null);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/process",
            new DomainServiceRequest(command, CurrentState: null));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        DomainServiceWireResult result = (await response.Content.ReadFromJsonAsync<DomainServiceWireResult>())
            .ShouldNotBeNull();
        result.IsRejection.ShouldBeTrue();
        DomainServiceWireEvent rejection = result.Events.ShouldHaveSingleItem();
        rejection.EventTypeName.ShouldBe(typeof(PartyCommandValidationRejected).FullName);
        string rejectionPayload = Encoding.UTF8.GetString(rejection.Payload.ShouldNotBeNull());
        rejectionPayload.ShouldContain(nameof(Hexalith.Parties.Contracts.Commands.CreatePartyComposite.PersonDetails));
        rejectionPayload.ShouldNotContain("tenant-a");
        rejectionPayload.ShouldNotContain("user-a");
        rejectionPayload.ShouldNotContain("party-invalid-payload");
    }

    [Theory]
    [InlineData("Party")]
    [InlineData("PARTY")]
    [InlineData("pArTy")]
    public async Task PostProcess_CommonPartyDomainCaseVariants_ResolveProductionProcessorAsync(string domain)
    {
        using var factory = new PartiesProcessTestFactory(useProductionProcessor: true);
        using HttpClient client = factory.CreateClient();
        var command = new CommandEnvelope(
            MessageId: $"cmd-8-5-domain-{domain}",
            TenantId: "tenant-a",
            Domain: domain,
            AggregateId: "party-case-variant",
            CommandType: typeof(CreatePartyComposite).FullName!,
            Payload: JsonSerializer.SerializeToUtf8Bytes(new CreatePartyComposite
            {
                PartyId = "party-case-variant",
                Type = PartyType.Person,
                PersonDetails = new PersonDetails
                {
                    FirstName = "Ada",
                    LastName = "Lovelace",
                },
            }),
            CorrelationId: $"cmd-8-5-domain-{domain}",
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
        result.Events.ShouldContain(e => e.EventTypeName == typeof(PartyCreated).FullName);
    }

    [Fact]
    public async Task PostReplayState_UsesProductionProcessorReplayCapabilityAsync()
    {
        using var factory = new PartiesProcessTestFactory(useProductionProcessor: true);
        using HttpClient client = factory.CreateClient();
        var request = new AggregateReconstructionRequest(
            TenantId: "tenant-a",
            Domain: "party",
            AggregateType: "Party",
            AggregateId: "party-replay",
            UpToSequence: 1,
            Events:
            [
                new ReplayEventEnvelope(
                    SequenceNumber: 1,
                    EventTypeName: typeof(PartyCreated).FullName!,
                    Payload: JsonSerializer.SerializeToUtf8Bytes(new PartyCreated { Type = PartyType.Person }),
                    SerializationFormat: "json",
                    MetadataVersion: 1,
                    MessageId: "evt-8-5-party-created",
                    CorrelationId: "replay-8-5",
                    CausationId: "cmd-8-5-party-created"),
            ],
            IncludeTimeline: false,
            RequestId: "replay-8-5");

        HttpResponseMessage response = await client.PostAsJsonAsync("/replay-state", request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        string content = await response.Content.ReadAsStringAsync();
        using JsonDocument document = JsonDocument.Parse(content);
        JsonElement result = document.RootElement;
        result.GetProperty("status").GetString().ShouldBe(nameof(AggregateReconstructionStatus.Succeeded));
        string? stateJson = result.GetProperty("stateJson").GetString();
        stateJson.ShouldNotBeNullOrWhiteSpace();
        stateJson!.ShouldContain(nameof(PartyType.Person));
    }

    private static string RetiredGdprWarningHeader() => "X-" + "GDPR-Warning";

    private sealed class PartiesProcessTestFactory : WebApplicationFactory<Program>
    {
        private readonly IDomainProcessor? _registeredProcessor;
        private readonly bool _gdprFeaturesActive;
        private readonly bool _useProductionProcessor;

        public PartiesProcessTestFactory(bool gdprFeaturesActive = false)
        {
            Processor = new CapturingDomainProcessor();
            _registeredProcessor = Processor;
            _gdprFeaturesActive = gdprFeaturesActive;
        }

        public PartiesProcessTestFactory(bool useProductionProcessor, bool gdprFeaturesActive = false)
        {
            _useProductionProcessor = useProductionProcessor;
            _gdprFeaturesActive = gdprFeaturesActive;
        }

        public PartiesProcessTestFactory(IDomainProcessor registeredProcessor, bool gdprFeaturesActive = false)
        {
            ArgumentNullException.ThrowIfNull(registeredProcessor);
            Processor = new CapturingDomainProcessor();
            _registeredProcessor = registeredProcessor;
            _gdprFeaturesActive = gdprFeaturesActive;
        }

        public CapturingDomainProcessor? Processor { get; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.UseEnvironment("Development");
            builder.UseSetting(MvpComplianceWarning.ActivationConfigurationKey, _gdprFeaturesActive.ToString());
            builder.ConfigureTestServices(services =>
            {
                if (!_useProductionProcessor)
                {
                    services.AddKeyedSingleton<IDomainProcessor>("party", (_, _) => _registeredProcessor!);
                }
            });
        }
    }

    private sealed class CapturingDomainProcessor : IDomainProcessor
    {
        private readonly List<CommandEnvelope> _receivedCommands = [];

        public IReadOnlyList<CommandEnvelope> ReceivedCommands => _receivedCommands;

        public Task<DomainResult> ProcessAsync(CommandEnvelope command, object? currentState)
        {
            ArgumentNullException.ThrowIfNull(command);
            _receivedCommands.Add(command);
            return Task.FromResult(DomainResult.Success(
                [new PartyCreated { Type = PartyType.Person }]));
        }
    }

    private sealed class PayloadProducingDomainProcessor : IDomainProcessor
    {
        public Task<DomainResult> ProcessAsync(CommandEnvelope command, object? currentState)
        {
            ArgumentNullException.ThrowIfNull(command);
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
