using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.Parties.Tests.Gateway;

/// <summary>Exercises the Parties host's real workload and sidecar boundary with isolated credentials.</summary>
public sealed class PartiesDomainServiceSecurityTests
{
    /// <summary>Missing or forged channel/assertion fields never reach even an otherwise accepting processor.</summary>
    [Theory]
    [InlineData("missing", HttpStatusCode.Unauthorized)]
    [InlineData("caller-header", HttpStatusCode.Unauthorized)]
    [InlineData("channel-only", HttpStatusCode.Unauthorized)]
    [InlineData("missing-channel", HttpStatusCode.Unauthorized)]
    [InlineData("wrong-channel", HttpStatusCode.Unauthorized)]
    [InlineData("duplicate-channel", HttpStatusCode.Unauthorized)]
    [InlineData("forged-assertion", HttpStatusCode.Unauthorized)]
    [InlineData("foreign-assertion", HttpStatusCode.Unauthorized)]
    [InlineData("duplicate-assertion", HttpStatusCode.Unauthorized)]
    [InlineData("conflicting-caller", HttpStatusCode.Unauthorized)]
    [InlineData("human-bearer", HttpStatusCode.Unauthorized)]
    [InlineData("wrong-audience", HttpStatusCode.Unauthorized)]
    [InlineData("wrong-operation", HttpStatusCode.Forbidden)]
    public async Task PostProcess_InvalidCredentials_DenyBeforeDomainWork(string scenario, HttpStatusCode expected)
    {
        using var factory = new PartiesProcessTestFactory();
        using HttpClient client = factory.CreateAuthenticatedClient(
            scenario == "wrong-operation" ? EventStoreWorkloadOperations.DomainServiceQuery : EventStoreWorkloadOperations.DomainServiceProcess,
            scenario == "wrong-audience" ? "tenants" : "parties");
        string assertion = client.DefaultRequestHeaders.GetValues(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName).Single();
        switch (scenario)
        {
            case "missing":
                client.DefaultRequestHeaders.Clear();
                break;
            case "caller-header":
            case "channel-only":
                client.DefaultRequestHeaders.Remove(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName);
                client.DefaultRequestHeaders.Add(EventStoreWorkloadAuthenticationDefaults.DaprCallerHeaderName, "eventstore");
                break;
            case "missing-channel":
                client.DefaultRequestHeaders.Remove(DaprAppChannelToken.HeaderName);
                break;
            case "wrong-channel":
                client.DefaultRequestHeaders.Remove(DaprAppChannelToken.HeaderName);
                client.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, "forged-channel");
                break;
            case "duplicate-channel":
                client.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, PartiesProcessTestFactory.ChannelToken);
                break;
            case "forged-assertion":
                client.DefaultRequestHeaders.Remove(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName);
                client.DefaultRequestHeaders.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, "forged-assertion");
                break;
            case "foreign-assertion":
                using (var foreign = new PartiesProcessTestFactory())
                using (HttpClient foreignClient = foreign.CreateAuthenticatedClient())
                {
                    client.DefaultRequestHeaders.Remove(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName);
                    client.DefaultRequestHeaders.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName,
                        foreignClient.DefaultRequestHeaders.GetValues(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName).Single());
                }
                break;
            case "duplicate-assertion":
                client.DefaultRequestHeaders.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, assertion);
                break;
            case "conflicting-caller":
                client.DefaultRequestHeaders.Add(EventStoreWorkloadAuthenticationDefaults.DaprCallerHeaderName, "untrusted-service");
                break;
            case "human-bearer":
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + assertion);
                break;
        }

        using HttpResponseMessage response = await client.PostAsJsonAsync("/process", Request(), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(expected);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();
        factory.Processor.ShouldNotBeNull().ReceivedCommands.ShouldBeEmpty();
    }

    /// <summary>Sidecar discovery/configuration/health are usable with the channel token alone and deny forged channels.</summary>
    [Theory]
    [InlineData("/dapr/subscribe", "valid")]
    [InlineData("/dapr/subscribe", "missing")]
    [InlineData("/dapr/subscribe", "wrong")]
    [InlineData("/dapr/subscribe", "duplicate")]
    [InlineData("/dapr/config", "valid")]
    [InlineData("/dapr/config", "missing")]
    [InlineData("/dapr/config", "wrong")]
    [InlineData("/dapr/config", "duplicate")]
    [InlineData("/healthz", "valid")]
    [InlineData("/healthz", "missing")]
    [InlineData("/healthz", "wrong")]
    [InlineData("/healthz", "duplicate")]
    public async Task SidecarRoute_RequiresAuthenticatedChannel(string route, string channel)
    {
        using var factory = new PartiesProcessTestFactory();
        using HttpClient client = factory.CreateClient();
        if (channel != "missing")
        {
            client.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName,
                channel == "wrong" ? "forged-channel" : PartiesProcessTestFactory.ChannelToken);
            if (channel == "duplicate")
            {
                client.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, PartiesProcessTestFactory.ChannelToken);
            }
        }

        using HttpResponseMessage response = await client.GetAsync(route, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(channel == "valid" ? HttpStatusCode.OK : HttpStatusCode.Unauthorized);
        factory.Processor.ShouldNotBeNull().ReceivedCommands.ShouldBeEmpty();
    }

    /// <summary>Actor and tenant-delivery routes reject unauthenticated requests before dispatch or payload parsing.</summary>
    [Theory]
    [InlineData("/actors/PartyProjectionActor/party-1/method/Untrusted")]
    [InlineData("/tenants/events")]
    public async Task SidecarDelivery_MissingChannel_DeniesBeforeDispatch(string route)
    {
        using var factory = new PartiesProcessTestFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.PostAsync(route, JsonContent.Create(new { }), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        factory.Processor.ShouldNotBeNull().ReceivedCommands.ShouldBeEmpty();
    }

    /// <summary>The complete actual endpoint inventory keeps canonical operation policies and actor/subscription channel policies.</summary>
    [Fact]
    public void ActualEndpointInventory_PassesUnchangedSdkAudit()
    {
        using var factory = new PartiesProcessTestFactory();
        using HttpClient client = factory.CreateClient();
        EndpointDataSource source = factory.Services.GetRequiredService<EndpointDataSource>();
        AuthorizationPolicy? fallback = factory.Services.GetRequiredService<IOptions<AuthorizationOptions>>().Value.FallbackPolicy;
        EventStoreDomainServiceEndpointInventory.Validate(source.Endpoints, fallback).ShouldBeEmpty();
        RouteEndpoint[] endpoints = source.Endpoints.OfType<RouteEndpoint>().ToArray();
        RouteEndpoint[] sidecar = endpoints.Where(endpoint => EventStoreDomainServiceEndpointInventory.IsSidecarOriginated(
            endpoint, EventStoreDomainServiceRoutes.Normalize(endpoint.RoutePattern.RawText))).ToArray();
        sidecar.ShouldNotBeEmpty();
        sidecar.ShouldContain(endpoint => endpoint.RoutePattern.RawText!.StartsWith("actors/", StringComparison.Ordinal)
            || endpoint.RoutePattern.RawText.StartsWith("/actors/", StringComparison.Ordinal));
        foreach (RouteEndpoint endpoint in sidecar)
        {
            endpoint.Metadata.GetMetadata<IAllowAnonymous>().ShouldBeNull();
            endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().ShouldContain(data => data.Policy == EventStoreDomainServicePolicies.SidecarChannel);
        }
    }

    private static DomainServiceRequest Request() => new(new CommandEnvelope(
        "security-denial-command", "tenant-a", "party", "party-1", "CreatePartyComposite", JsonSerializer.SerializeToUtf8Bytes(new { }),
        "security-denial-correlation", null, "untrusted-user", null), null);
}
