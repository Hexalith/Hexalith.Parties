using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Parties.Contracts.Authorization;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

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
    [InlineData("human-bearer-only", HttpStatusCode.Unauthorized)]
    [InlineData("disallowed-workload", HttpStatusCode.Unauthorized)]
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
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add(EventStoreWorkloadAuthenticationDefaults.DaprCallerHeaderName, "eventstore");
                break;
            case "channel-only":
                client.DefaultRequestHeaders.Remove(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName);
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
            case "human-bearer-only":
                string human = SignedToken(factory.Services, "hexalith-eventstore");
                TokenValidationResult humanValidation = await new JsonWebTokenHandler().ValidateTokenAsync(human,
                    factory.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
                        .Get(JwtBearerDefaults.AuthenticationScheme).TokenValidationParameters);
                humanValidation.IsValid.ShouldBeTrue();
                client.DefaultRequestHeaders.Remove(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName);
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + human);
                break;
            case "disallowed-workload":
                string disallowed = SignedToken(factory.Services, "parties", "untrusted-service");
                TokenValidationResult workloadValidation = await new JsonWebTokenHandler().ValidateTokenAsync(disallowed,
                    factory.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
                        .Get(EventStoreWorkloadAuthenticationDefaults.WorkloadScheme).TokenValidationParameters);
                workloadValidation.IsValid.ShouldBeTrue();
                client.DefaultRequestHeaders.Remove(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName);
                client.DefaultRequestHeaders.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, disallowed);
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
    [InlineData("/healthz/", "valid")]
    [InlineData("/healthz/", "missing")]
    [InlineData("/healthz/", "wrong")]
    [InlineData("/healthz/", "duplicate")]
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
        RouteEndpoint[] endpoints = source.Endpoints.OfType<RouteEndpoint>().ToArray();
        foreach (EventStoreDomainServiceRoute route in EventStoreDomainServiceRoutes.Operational)
        {
            endpoints.ShouldContain(endpoint => EventStoreDomainServiceRoutes.Normalize(endpoint.RoutePattern.RawText) == route.Route
                && endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Contains(HttpMethods.Post));
        }

        EventStoreDomainServiceEndpointInventory.Validate(source.Endpoints, fallback).ShouldBeEmpty();
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

    /// <summary>Inherited OIDC, collection and workload configuration cannot weaken or disable the isolated real boundary.</summary>
    [Fact]
    public async Task InheritedAuthenticationSettings_CannotAlterFixtureContracts()
    {
        using var factory = new PartiesProcessTestFactory();
        using var inherited = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Authentication:JwtBearer:Authority", "https://unavailable.example.test");
            builder.UseSetting("Authentication:JwtBearer:ValidAudiences:0", "untrusted-audience");
            builder.UseSetting("Authentication:JwtBearer:AllowedAlgorithms:1", SecurityAlgorithms.RsaSha256);
            builder.UseSetting("Authentication:Workload:Audience", "untrusted-audience");
            builder.UseSetting("Authentication:Workload:AllowedCallers:0", "untrusted-service");
            builder.UseSetting("Authentication:Workload:AllowedCallers:1", "other-untrusted-service");
            builder.UseSetting("Authentication:Workload:MaximumLifetimeSeconds", "0");
            builder.UseSetting("Authentication:WorkloadIssuer:Workload", "untrusted-service");
            builder.UseSetting("Authentication:WorkloadIssuer:LifetimeSeconds", "0");
            builder.UseSetting("Authentication:WorkloadIssuer:TokenEndpoint", "https://unavailable.example.test/token");
        });
        using HttpClient client = inherited.CreateClient();
        JwtBearerAuthenticationOptions contract = inherited.Services.GetRequiredService<IOptionsMonitor<JwtBearerAuthenticationOptions>>()
            .Get(EventStoreWorkloadAuthenticationDefaults.JwtContractOptionsName);
        contract.Authority.ShouldBeNull();
        contract.ValidAudiences.ShouldBeEmpty();
        contract.AllowedAlgorithms.ShouldBe([SecurityAlgorithms.HmacSha256]);
        WorkloadAuthenticationOptions workload = inherited.Services.GetRequiredService<IOptionsMonitor<WorkloadAuthenticationOptions>>()
            .Get(EventStoreWorkloadAuthenticationDefaults.WorkloadScheme);
        workload.Audience.ShouldBe("parties");
        workload.AllowedCallers.ShouldBe(["eventstore"]);
        workload.MaximumLifetimeSeconds.ShouldBe(WorkloadAuthenticationOptions.DefaultMaximumLifetimeSeconds);
        WorkloadAssertionIssuerOptions issuer = inherited.Services.GetRequiredService<IOptions<WorkloadAssertionIssuerOptions>>().Value;
        issuer.Workload.ShouldBe("eventstore");
        issuer.LifetimeSeconds.ShouldBe(WorkloadAssertionIssuerOptions.DefaultLifetimeSeconds);
        issuer.TokenEndpoint.ShouldBeNull();
        string assertion = (await inherited.Services.GetRequiredService<IWorkloadAssertionIssuer>().IssueAsync(
            new("parties", EventStoreWorkloadOperations.DomainServiceProcess), TestContext.Current.CancellationToken)).ShouldNotBeNull();
        client.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, PartiesProcessTestFactory.ChannelToken);
        client.DefaultRequestHeaders.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, assertion);

        using HttpResponseMessage response = await client.PostAsJsonAsync("/process", Request(), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        factory.Processor.ShouldNotBeNull().ReceivedCommands.ShouldHaveSingleItem();
    }

    private static string SignedToken(IServiceProvider services, string audience, string? caller = null)
    {
        JwtBearerAuthenticationOptions contract = services.GetRequiredService<IOptionsMonitor<JwtBearerAuthenticationOptions>>()
            .Get(EventStoreWorkloadAuthenticationDefaults.JwtContractOptionsName);
        var claims = new Dictionary<string, object> { [PartiesClaimTypes.Subject] = "isolated-human" };
        if (caller is not null)
        {
            claims[EventStoreWorkloadAuthenticationDefaults.CallerClaimType] = caller;
            claims[EventStoreWorkloadAuthenticationDefaults.OperationClaimType] = EventStoreWorkloadOperations.DomainServiceProcess;
        }

        DateTime now = DateTime.UtcNow;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = contract.Issuer, Audience = audience, IssuedAt = now, NotBefore = now, Expires = now.AddMinutes(2), Claims = claims,
            SigningCredentials = new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(contract.SigningKey!)), SecurityAlgorithms.HmacSha256),
        });
    }

    private static DomainServiceRequest Request() => new(new CommandEnvelope(
        "security-denial-command", "tenant-a", "party", "party-1", "CreatePartyComposite", JsonSerializer.SerializeToUtf8Bytes(new { }),
        "security-denial-correlation", null, "untrusted-user", null), null);
}
