using System.Net;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using System.Net.Http.Json;
using System.Text.Json;

using Aspire.Hosting.Testing;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Contracts.Identity;
using Microsoft.IdentityModel.Tokens;
using Hexalith.Parties.Contracts.Authorization;
using Hexalith.Parties.IntegrationTests.Gateway;
using Hexalith.Parties.IntegrationTests.HealthChecks;

using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Proves two real mTLS sessions own disjoint discovery, routing, persisted state and cleanup.</summary>
[Collection("Non-parallel")]
public sealed class PartiesTopologyIsolationTests
{
    [Fact]
    public void SessionConfiguration_SeparatesEveryOwnedIdentityPathAndLoopbackListener()
    {
        PartiesTopologySession first = PartiesTopologySession.Create(isolatedIdentities: true);
        PartiesTopologySession second = PartiesTopologySession.Create(isolatedIdentities: true);
        first.Id.ShouldNotBe(second.Id);
        first.Namespace.ShouldNotBe(second.Namespace);
        first.CertificateDirectory.ShouldNotBe(second.CertificateDirectory);
        first.ConfigurationDirectory.ShouldNotBe(second.ConfigurationDirectory);
        first.AppId("parties").ShouldNotBe(second.AppId("parties"));
        var firstEnvironment = new Dictionary<string, string?>();
        var secondEnvironment = new Dictionary<string, string?>();
        first.ConfigureBootstrapEnvironment(firstEnvironment);
        second.ConfigureBootstrapEnvironment(secondEnvironment);
        firstEnvironment["DAPR_MTLS_SESSION_ID"].ShouldNotBe(secondEnvironment["DAPR_MTLS_SESSION_ID"]);
        firstEnvironment.Where(pair => pair.Key.EndsWith("PORT", StringComparison.Ordinal)).Select(pair => pair.Value)
            .Distinct().Count().ShouldBe(6);
        firstEnvironment.Where(pair => pair.Key.EndsWith("PORT", StringComparison.Ordinal)).Select(pair => pair.Value)
            .Intersect(secondEnvironment.Where(pair => pair.Key.EndsWith("PORT", StringComparison.Ordinal)).Select(pair => pair.Value))
            .ShouldBeEmpty();
        // The production ACL profile retains its original literal identities and namespace.
        PartiesTopologySession production = PartiesTopologySession.Create();
        production.AppId("parties").ShouldBe("parties");
        production.Namespace.ShouldBe("default");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Tier", "3")]
    public async Task ConcurrentSessions_IsolateStreamsRejectCrossCallsAndSurvivePeerDisposalAsync()
    {
        EventStoreGatewayE2ETests.RequireRuntimeProfile();
        var first = new PartiesAspireTopologyFixture(completeMtlsProfile: true, isolatedIdentities: true);
        var second = new PartiesAspireTopologyFixture(completeMtlsProfile: true, isolatedIdentities: true);
        await using var firstLifetime = first.ConfigureAwait(true);
        await using var secondLifetime = second.ConfigureAwait(true);
        // Qualify each cold startup within its existing readiness budget. Both sessions
        // remain live for every routing, state, cross-call and peer-disposal assertion.
        await first.InitializeAsync(TestContext.Current.CancellationToken);
        first.IsAvailable.ShouldBeTrue(first.UnavailableReason);
        await second.InitializeAsync(TestContext.Current.CancellationToken);
        second.IsAvailable.ShouldBeTrue(second.UnavailableReason);
        await SeedPersistedCounterAsync(first, 2, TestContext.Current.CancellationToken);
        await SeedPersistedCounterAsync(second, 5, TestContext.Current.CancellationToken);
        await AssertAuthenticatedUiRoutingAsync(first, 2, TestContext.Current.CancellationToken);
        await AssertAuthenticatedUiRoutingAsync(second, 5, TestContext.Current.CancellationToken);
        const string markerKey = "fixture-runtime-isolation-marker";
        foreach (PartiesAspireTopologyFixture fixture in new[] { first, second })
        {
            using HttpClient sidecar = fixture.CreateSidecarClient("parties");
            using JsonDocument metadata = (await sidecar.GetFromJsonAsync<JsonDocument>("/v1.0/metadata", TestContext.Current.CancellationToken))!;
            metadata.RootElement.GetProperty("id").GetString().ShouldBe(fixture.Session.AppId("parties"));
            using HttpResponseMessage ready = await sidecar.GetAsync(
                $"/v1.0/invoke/{fixture.Session.AppId("tenants")}/method/ready", TestContext.Current.CancellationToken);
            ready.StatusCode.ShouldBe(HttpStatusCode.OK);
            using HttpResponseMessage write = await sidecar.PostAsJsonAsync("/v1.0/state/statestore",
                new[] { new { key = markerKey, value = fixture.Session.Id } }, TestContext.Current.CancellationToken);
            write.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            (await fixture.ReadRedisDataAsync(markerKey, TestContext.Current.CancellationToken)).ShouldBe(JsonSerializer.Serialize(fixture.Session.Id));
        }
        foreach ((PartiesAspireTopologyFixture caller, PartiesAspireTopologyFixture target) in new[] { (first, second), (second, first) })
        {
            using HttpClient sidecar = caller.CreateSidecarClient("parties");
            using HttpResponseMessage cross = await sidecar.GetAsync(
                $"/v1.0/invoke/{target.Session.AppId("tenants")}/method/ready", TestContext.Current.CancellationToken);
            cross.IsSuccessStatusCode.ShouldBeFalse();
            string denial = await cross.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            denial.ShouldContain("ERR_DIRECT_INVOKE");
            foreach ((string ui, string destination) in new[] { ("eventstore-admin-ui", "eventstore-admin"), ("sample-blazor-ui", "eventstore") })
            {
                using HttpClient uiSidecar = caller.CreateSidecarClient(ui);
                using HttpResponseMessage uiCross = await uiSidecar.GetAsync(
                    $"/v1.0/invoke/{target.Session.AppId(destination)}/method/ready", TestContext.Current.CancellationToken);
                uiCross.IsSuccessStatusCode.ShouldBeFalse();
                (await uiCross.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("ERR_DIRECT_INVOKE");
            }
        }
        string samePartyId = UniqueIdHelper.GenerateSortableUniqueStringId();
        await EventStoreGatewayE2ETests.CreateAndVerifyAsync(first, samePartyId, "First session", TestContext.Current.CancellationToken);
        await EventStoreGatewayE2ETests.CreateAndVerifyAsync(second, samePartyId, "Second session", TestContext.Current.CancellationToken);
        await first.DisposeAsync();
        Directory.Exists(first.Session.Root).ShouldBeFalse();
        (await second.ReadRedisDataAsync(markerKey, TestContext.Current.CancellationToken)).ShouldBe(JsonSerializer.Serialize(second.Session.Id));
        using HttpClient survivor = second.CreateSidecarClient("parties");
        using HttpResponseMessage survivorReady = await survivor.GetAsync(
            $"/v1.0/invoke/{second.Session.AppId("tenants")}/method/ready", TestContext.Current.CancellationToken);
        survivorReady.StatusCode.ShouldBe(HttpStatusCode.OK);
        await EventStoreGatewayE2ETests.CreateAndVerifyAsync(second, UniqueIdHelper.GenerateSortableUniqueStringId(), "Survivor session", TestContext.Current.CancellationToken);
        await AssertAuthenticatedUiRoutingAsync(second, 5, TestContext.Current.CancellationToken);
        await second.DisposeAsync();
        Directory.Exists(second.Session.Root).ShouldBeFalse();
    }

    /// <summary>Seeds real counter commands and independently verifies the persisted increment stream.</summary>
    private static async Task SeedPersistedCounterAsync(PartiesAspireTopologyFixture fixture, int count, CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(60));
        var identity = new AggregateIdentity(PartiesTenantBootstrapHost.TenantId, "counter", "counter-1");
        var token = new JwtSecurityTokenHandler().CreateEncodedJwt(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim(PartiesClaimTypes.Subject, PartiesTenantBootstrapHost.UserId),
                new Claim(PartiesClaimTypes.EventStoreTenant, identity.TenantId), new Claim("tenants", JsonSerializer.Serialize(new[] { identity.TenantId })),
                new Claim("domains", "[\"counter\"]"), new Claim("permissions", "[\"commands:*\",\"command:submit\",\"query:read\"]")]),
            Issuer = PartiesAspireTopologyFixture.JwtIssuer, Audience = "hexalith-eventstore", Expires = DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(PartiesAspireTopologyFixture.JwtSigningKey)), SecurityAlgorithms.HmacSha256),
        });
        using var client = new HttpClient { BaseAddress = fixture.EventStoreClient.BaseAddress };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        for (int increment = 0; increment < count; increment++)
        {
            string id = UniqueIdHelper.GenerateSortableUniqueStringId();
            using HttpResponseMessage submitted = await client.PostAsJsonAsync("/api/v1/commands", new
            {
                messageId = id, correlationId = id, tenant = identity.TenantId, domain = identity.Domain,
                aggregateId = identity.AggregateId, commandType = "increment-counter", payload = new { counterId = identity.AggregateId },
            }, budget.Token).ConfigureAwait(false);
            submitted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            while (true)
            {
                using HttpResponseMessage response = await client.GetAsync($"/api/v1/commands/status/{id}", budget.Token).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    using JsonDocument status = await response.Content.ReadFromJsonAsync<JsonDocument>(budget.Token).ConfigureAwait(false)
                        ?? throw new InvalidOperationException("Counter status was empty.");
                    string? outcome = status.RootElement.GetProperty("status").GetString();
                    if (outcome is "Completed" or "Rejected" or "PublishFailed" or "TimedOut")
                    {
                        outcome.ShouldBe("Completed");
                        break;
                    }
                }
                await Task.Delay(TimeSpan.FromMilliseconds(250), budget.Token).ConfigureAwait(false);
            }
        }
        var stored = await fixture.ReadStoredStreamAsync(identity, budget.Token).ConfigureAwait(false);
        stored.Metadata.CurrentSequence.ShouldBe(count);
        stored.Events.Length.ShouldBe(count);
        stored.Events.ShouldAllBe(item => item.Identity == identity && item.EventTypeName.EndsWith(".CounterIncremented", StringComparison.Ordinal));
    }

    /// <summary>Prerendering executes the real UI clients, bearer handlers and configured SDK Dapr routing.</summary>
    private static async Task AssertAuthenticatedUiRoutingAsync(PartiesAspireTopologyFixture fixture, int expectedCount, CancellationToken cancellationToken)
    {
        using HttpClient admin = fixture.App.CreateHttpClient("eventstore-admin-ui", "http");
        string dashboard = await admin.GetStringAsync("/dapr", cancellationToken).ConfigureAwait(false);
        dashboard.ShouldContain(fixture.Session.AppId("eventstore-admin"));
        dashboard.ShouldNotContain("Unable to load DAPR infrastructure");
        using HttpClient sample = fixture.App.CreateHttpClient("sample-blazor-ui", "http");
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(60));
        while (true)
        {
            string counter = await sample.GetStringAsync("/pattern-silent-reload", budget.Token).ConfigureAwait(false);
            Match displayed = Regex.Match(counter, @"<p\b[^>]*style=""[^""]*font-size:\s*48px[^""]*""[^>]*>\s*(\d+)\s*</p>");
            if (displayed.Success && displayed.Groups[1].Value == expectedCount.ToString(System.Globalization.CultureInfo.InvariantCulture))
            {
                counter.ShouldNotContain("Failed to load counter:");
                break;
            }
            await Task.Delay(TimeSpan.FromMilliseconds(250), budget.Token).ConfigureAwait(false);
        }
    }
}
