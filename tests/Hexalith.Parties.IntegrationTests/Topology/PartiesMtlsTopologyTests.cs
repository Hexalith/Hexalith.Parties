using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.Parties.Authorization;
using Hexalith.Parties.IntegrationTests.HealthChecks;
using Hexalith.Tenants.Client.Projections;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Qualifies real mTLS transport and the registered tenant bootstrap trust boundary serially.</summary>
[Collection("Non-parallel")]
[Trait("Category", "E2E")]
[Trait("Tier", "3")]
public sealed class PartiesMtlsTopologyTests
{
    [Fact]
    public async Task CompleteProfile_AllEightSidecarsRespondAndPartiesInvokesTenantsReadiness()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("PARTIES_MTLS_TOPOLOGY_TESTS"), "true", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Skip("Set PARTIES_MTLS_TOPOLOGY_TESTS=true to require the complete disposable mTLS topology.");
        }

        var fixture = new PartiesAspireTopologyFixture(completeMtlsProfile: true,
            isolatedIdentities: string.Equals(Environment.GetEnvironmentVariable("PARTIES_MTLS_ISOLATED_IDENTITIES"), "true", StringComparison.OrdinalIgnoreCase));
        await using var lifetime = fixture.ConfigureAwait(true);
        await fixture.InitializeAsync(TestContext.Current.CancellationToken);
        fixture.IsAvailable.ShouldBeTrue(fixture.UnavailableReason);
        Console.WriteLine($"Runtime identity profile: namespace={fixture.Session.Namespace}; suffix={fixture.Session.AppIdSuffix}");
        foreach (string appId in PartiesAspireTopologyFixture.CompleteSidecarIds)
        {
            using HttpClient sidecar = fixture.CreateSidecarClient(appId);
            using JsonDocument metadata = (await sidecar.GetFromJsonAsync<JsonDocument>("/v1.0/metadata", TestContext.Current.CancellationToken))!;
            metadata.RootElement.GetProperty("id").GetString().ShouldBe(fixture.Session.AppId(appId));
            Console.WriteLine($"Runtime metadata verified: {fixture.Session.AppId(appId)}");
        }

        using HttpClient partiesSidecar = fixture.CreateSidecarClient("parties");
        using HttpResponseMessage readiness = await partiesSidecar.GetAsync(
            $"/v1.0/invoke/{fixture.Session.AppId("tenants")}/method/ready", TestContext.Current.CancellationToken);
        readiness.StatusCode.ShouldBe(HttpStatusCode.OK,
            await readiness.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Console.WriteLine("Runtime Parties-to-Tenants GET /ready: 200 through existing ACL");

        using HttpClient adminUiSidecar = fixture.CreateSidecarClient("eventstore-admin-ui");
        using HttpResponseMessage adminHealth = await adminUiSidecar.GetAsync(
            $"/v1.0/invoke/{fixture.Session.AppId("eventstore-admin")}/method/health", TestContext.Current.CancellationToken);
        adminHealth.StatusCode.ShouldBe(HttpStatusCode.OK,
            await adminHealth.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Console.WriteLine("Runtime admin UI-to-admin server GET /health: 200 through existing ACL");

        // Require the sidecar's policy diagnostic: an application 401/403 is insufficient.
        foreach ((string caller, string target, HttpMethod verb, string path) in new[]
        {
            ("sample", "tenants", HttpMethod.Get, "ready"),
            ("parties", "tenants", HttpMethod.Post, "ready"),
            ("parties", "tenants", HttpMethod.Get, "health"),
            ("eventstore", "parties", HttpMethod.Get, "process"),
            ("eventstore", "parties", HttpMethod.Post, "health"),
            ("sample", "parties", HttpMethod.Post, "process"),
        })
        {
            using HttpClient callerSidecar = fixture.CreateSidecarClient(caller);
            using var request = new HttpRequestMessage(verb, $"/v1.0/invoke/{fixture.Session.AppId(target)}/method/{path}");
            using HttpResponseMessage denied = await callerSidecar.SendAsync(request, TestContext.Current.CancellationToken);
            denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden,
                $"{fixture.Session.AppId(caller)} {verb.Method} {fixture.Session.AppId(target)}/{path}: "
                + await denied.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            using JsonDocument evidence = JsonDocument.Parse(await denied.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            evidence.RootElement.GetProperty("errorCode").GetString().ShouldBe("ERR_DIRECT_INVOKE");
            string diagnostic = evidence.RootElement.GetProperty("message").GetString()!;
            diagnostic.ShouldContain("PermissionDenied");
            diagnostic.ShouldContain("access control policy has denied access");
            Console.WriteLine($"Dapr denied {fixture.Session.AppId(caller)} {verb.Method} {fixture.Session.AppId(target)}/{path}: {diagnostic}");
        }
        Console.WriteLine("Configured identity ACLs rejected forbidden caller, verb and path combinations at Dapr");

        foreach (string? token in new string?[] { null, "invalid-test-channel-token" })
        {
            foreach (var envelope in PartiesTenantBootstrapHost.Envelopes("rejected-runtime-tenant", "rejected-runtime-member"))
            {
                using HttpResponseMessage rejected = await PartiesTenantBootstrapHost.DeliverAsync(
                    fixture.PartiesClient, envelope, token, TestContext.Current.CancellationToken);
                rejected.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            }
        }
        Console.WriteLine("Runtime tenant callback: authenticated bootstrap accepted; missing/wrong token rejected");

        // The process-external runtime callback has no public projection-read endpoint. Inspect the
        // production subscription, security and access-service registrations in a controlled host.
        PartiesTenantBootstrapHost host = fixture.TenantBootstrapHost!;
        TenantLocalState? state = await host.Services.GetRequiredService<ITenantProjectionStore>()
            .GetAsync(PartiesTenantBootstrapHost.TenantId, TestContext.Current.CancellationToken);
        state.ShouldNotBeNull();
        state.Members.ShouldContainKey(PartiesTenantBootstrapHost.UserId);
        TenantAccessDecision access = await host.Services.GetRequiredService<ITenantAccessService>().CheckAccessAsync(
            PartiesTenantBootstrapHost.TenantId, PartiesTenantBootstrapHost.UserId, TenantAccessRequirement.Write,
            TestContext.Current.CancellationToken);
        access.IsAllowed.ShouldBeTrue();
        Console.WriteLine("In-process production subscription handler: projected member has write access");
    }

    [Fact]
    public async Task RegisteredTenantHandler_RejectsUnauthenticatedDeliveryAndEstablishesAccessOnlyWithTestToken()
    {
        string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var host = await PartiesTenantBootstrapHost.StartAsync(token, TestContext.Current.CancellationToken);
        await using var lifetime = host.ConfigureAwait(true);
        using HttpClient client = host.CreateClient();
        ITenantProjectionStore store = host.Services.GetRequiredService<ITenantProjectionStore>();
        ITenantAccessService authorization = host.Services.GetRequiredService<ITenantAccessService>();
        var envelopes = PartiesTenantBootstrapHost.Envelopes(PartiesTenantBootstrapHost.TenantId, PartiesTenantBootstrapHost.UserId);
        foreach (string? presented in new string?[] { null, "wrong-test-channel-token" })
        {
            // Reuse the same delivery IDs on the admitted retry: denial must not poison deduplication.
            foreach (var envelope in envelopes)
            {
                using HttpResponseMessage rejected = await PartiesTenantBootstrapHost.DeliverAsync(
                    client, envelope, presented, TestContext.Current.CancellationToken);
                rejected.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            }
            (await store.GetAsync(PartiesTenantBootstrapHost.TenantId, TestContext.Current.CancellationToken)).ShouldBeNull();
            (await authorization.CheckAccessAsync(PartiesTenantBootstrapHost.TenantId, PartiesTenantBootstrapHost.UserId,
                TenantAccessRequirement.Read, TestContext.Current.CancellationToken)).IsAllowed.ShouldBeFalse();
        }
        foreach (var envelope in envelopes)
        {
            using HttpResponseMessage admitted = await PartiesTenantBootstrapHost.DeliverAsync(
                client, envelope, token, TestContext.Current.CancellationToken);
            admitted.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        TenantLocalState? state = await store.GetAsync(PartiesTenantBootstrapHost.TenantId, TestContext.Current.CancellationToken);
        state.ShouldNotBeNull();
        state.Members[PartiesTenantBootstrapHost.UserId].ShouldBe(Hexalith.Tenants.Contracts.Enums.TenantRole.TenantContributor);
        (await authorization.CheckAccessAsync(PartiesTenantBootstrapHost.TenantId, PartiesTenantBootstrapHost.UserId,
            TenantAccessRequirement.Write, TestContext.Current.CancellationToken)).IsAllowed.ShouldBeTrue();
        (await authorization.CheckAccessAsync(PartiesTenantBootstrapHost.TenantId, PartiesTenantBootstrapHost.UserId,
            TenantAccessRequirement.Admin, TestContext.Current.CancellationToken)).IsAllowed.ShouldBeFalse();
        (await authorization.CheckAccessAsync(PartiesTenantBootstrapHost.TenantId, "unregistered-member",
            TenantAccessRequirement.Read, TestContext.Current.CancellationToken)).IsAllowed.ShouldBeFalse();
    }
}
