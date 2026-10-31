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
using Hexalith.EventStore.ServiceDefaults.Authentication;
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
    public void SessionConfiguration_ReservesListenersOutsideTheLinuxEphemeralClientRange()
    {
        if (!OperatingSystem.IsLinux()) { Assert.Skip("Linux ephemeral client port controls."); }
        int[] ephemeral = File.ReadAllText("/proc/sys/net/ipv4/ip_local_port_range")
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(value => int.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        PartiesTopologySession session = PartiesTopologySession.Create(isolatedIdentities: true);
        var bootstrap = new Dictionary<string, string?>();
        session.ConfigureBootstrapEnvironment(bootstrap);
        int[] ports = bootstrap.Where(pair => pair.Key.EndsWith("PORT", StringComparison.Ordinal))
            .Select(pair => int.Parse(pair.Value!, System.Globalization.CultureInfo.InvariantCulture))
            .Concat(session.InternalGrpcPorts.Values).Append(session.EventStoreHttpPort)
            .Append(session.MemoriesDomainInternalGrpcPort).ToArray();

        ports.Distinct().Count().ShouldBe(16);
        ports.Where(port => port >= ephemeral[0] && port <= ephemeral[1]).ShouldBeEmpty(
            "Released listener reservations must not be claimed by outbound client connections.");
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
        await AssertSampleReceiverRefusesInvalidCredentialsAsync(first, TestContext.Current.CancellationToken);
        await VerifyGreetingAndCounterRejectionAsync(first, TestContext.Current.CancellationToken);
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
        using HttpClient client = CreateSampleGatewayClient(fixture);
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


    /// <summary>Real receiver policies deny app-channel and workload credential failures before dispatch.</summary>
    private static async Task AssertSampleReceiverRefusesInvalidCredentialsAsync(PartiesAspireTopologyFixture fixture, CancellationToken cancellationToken)
    {
        using HttpClient direct = fixture.App.CreateHttpClient("sample", "http");
        using HttpClient gatewaySidecar = fixture.CreateSidecarClient("eventstore");
        string humanAssertion = CreateSampleBearerToken(fixture.Session.AppId("sample"));
        foreach (string route in new[] { "/process", "/project", "/replay-state", "/admin/operational-index-metadata" })
        {
            foreach (bool invalidChannel in new[] { false, true })
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, route) { Content = JsonContent.Create(new { }) };
                if (invalidChannel)
                {
                    request.Headers.Add("dapr-api-token", "incorrect-channel-token");
                    request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, "invalid-workload-assertion");
                }
                using HttpResponseMessage denied = await direct.SendAsync(request, cancellationToken).ConfigureAwait(false);
                denied.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, "The real sample receiver must reject absent/invalid direct credentials: " + route);
            }
            // The permitted, mTLS-authenticated gateway sidecar supplies the genuine app-channel proof.
            // Missing, malformed and correctly signed human credentials still cannot impersonate its workload.
            foreach (string? assertion in new[] { null, "invalid-workload-assertion", humanAssertion })
            {
                using var request = new HttpRequestMessage(HttpMethod.Post,
                    $"/v1.0/invoke/{fixture.Session.AppId("sample")}/method{route}") { Content = JsonContent.Create(new { }) };
                if (assertion is not null) { request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, assertion); }
                using HttpResponseMessage denied = await gatewaySidecar.SendAsync(request, cancellationToken).ConfigureAwait(false);
                denied.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, "The real sample workload policy must challenge, not admit or defer to body binding: " + route);
            }
        }
        var unchanged = await fixture.ReadStoredStreamAsync(new AggregateIdentity(PartiesTenantBootstrapHost.TenantId, "counter", "counter-1"), cancellationToken).ConfigureAwait(false);
        unchanged.Metadata.CurrentSequence.ShouldBe(2);
        unchanged.Events.Length.ShouldBe(2);
        unchanged.Events.ShouldAllBe(item => item.EventTypeName.EndsWith(".CounterIncremented", StringComparison.Ordinal));
    }

    /// <summary>Both sample domains and Counter's rejection serializer execute through the real package gateway.</summary>
    private static async Task VerifyGreetingAndCounterRejectionAsync(PartiesAspireTopologyFixture fixture, CancellationToken cancellationToken)
    {
        using HttpClient client = CreateSampleGatewayClient(fixture);
        var greeting = new AggregateIdentity(PartiesTenantBootstrapHost.TenantId, "greeting", UniqueIdHelper.GenerateSortableUniqueStringId());
        string greetingId = UniqueIdHelper.GenerateSortableUniqueStringId();
        using HttpResponseMessage accepted = await client.PostAsJsonAsync("/api/v1/commands", new
        {
            messageId = greetingId, correlationId = greetingId, tenant = greeting.TenantId, domain = greeting.Domain,
            aggregateId = greeting.AggregateId, commandType = "SendGreeting", payload = new { },
        }, cancellationToken).ConfigureAwait(false);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await AssertSampleCommandStatusAsync(client, greetingId, "Completed", cancellationToken).ConfigureAwait(false);
        var sent = await fixture.ReadStoredStreamAsync(greeting, cancellationToken).ConfigureAwait(false);
        sent.Metadata.CurrentSequence.ShouldBe(1);
        var greetingEvent = sent.Events.ShouldHaveSingleItem();
        greetingEvent.Identity.ShouldBe(greeting);
        greetingEvent.EventTypeName.ShouldBe("Hexalith.EventStore.Sample.Greeting.Events.GreetingSent");
        greetingEvent.CorrelationId.ShouldBe(greetingId);
        greetingEvent.CausationId.ShouldBe(greetingId);
        greetingEvent.UserId.ShouldBe(PartiesTenantBootstrapHost.UserId);
        greetingEvent.SerializationFormat.ShouldBe("json");
        Encoding.UTF8.GetString(greetingEvent.Payload).ShouldBe("{}");

        var counter = new AggregateIdentity(PartiesTenantBootstrapHost.TenantId, "counter", UniqueIdHelper.GenerateSortableUniqueStringId());
        string rejectionId = UniqueIdHelper.GenerateSortableUniqueStringId();
        using HttpResponseMessage rejected = await client.PostAsJsonAsync("/api/v1/commands", new
        {
            messageId = rejectionId, correlationId = rejectionId, tenant = counter.TenantId, domain = counter.Domain,
            aggregateId = counter.AggregateId, commandType = "decrement-counter", payload = new { counterId = counter.AggregateId },
        }, cancellationToken).ConfigureAwait(false);
        rejected.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problem = await rejected.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Counter rejection problem was empty.");
        problem.RootElement.GetProperty("type").GetString().ShouldNotBeNull().ShouldContain("counter-cannot-go-negative");
        await AssertSampleCommandStatusAsync(client, rejectionId, "Rejected", cancellationToken).ConfigureAwait(false);
        var rejection = await fixture.ReadStoredStreamAsync(counter, cancellationToken).ConfigureAwait(false);
        rejection.Metadata.CurrentSequence.ShouldBe(1);
        var rejectionEvent = rejection.Events.ShouldHaveSingleItem();
        rejectionEvent.Identity.ShouldBe(counter);
        rejectionEvent.EventTypeName.ShouldBe("Hexalith.EventStore.Sample.Counter.Events.CounterCannotGoNegative");
        rejectionEvent.CorrelationId.ShouldBe(rejectionId);
        rejectionEvent.SerializationFormat.ShouldBe("json");
        Encoding.UTF8.GetString(rejectionEvent.Payload).ShouldBe("{}");
    }

    private static async Task AssertSampleCommandStatusAsync(HttpClient client, string id, string expected, CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(60));
        while (true)
        {
            using HttpResponseMessage response = await client.GetAsync($"/api/v1/commands/status/{id}", budget.Token).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                using JsonDocument status = await response.Content.ReadFromJsonAsync<JsonDocument>(budget.Token).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("Sample command status was empty.");
                string? actual = status.RootElement.GetProperty("status").GetString();
                if (actual is "Completed" or "Rejected" or "PublishFailed" or "TimedOut") { actual.ShouldBe(expected); return; }
            }
            await Task.Delay(TimeSpan.FromMilliseconds(250), budget.Token).ConfigureAwait(false);
        }
    }

    private static HttpClient CreateSampleGatewayClient(PartiesAspireTopologyFixture fixture)
    {
        var client = new HttpClient { BaseAddress = fixture.EventStoreClient.BaseAddress };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateSampleBearerToken());
        return client;
    }

    private static string CreateSampleBearerToken(string audience = "hexalith-eventstore") =>
        new JwtSecurityTokenHandler().CreateEncodedJwt(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim(PartiesClaimTypes.Subject, PartiesTenantBootstrapHost.UserId),
                new Claim(PartiesClaimTypes.EventStoreTenant, PartiesTenantBootstrapHost.TenantId),
                new Claim("tenants", JsonSerializer.Serialize(new[] { PartiesTenantBootstrapHost.TenantId })),
                new Claim("domains", "[\"counter\",\"greeting\"]"), new Claim("permissions", "[\"commands:*\",\"command:submit\",\"query:read\"]")]),
            Issuer = PartiesAspireTopologyFixture.JwtIssuer, Audience = audience, Expires = DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(PartiesAspireTopologyFixture.JwtSigningKey)), SecurityAlgorithms.HmacSha256),
        });

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
