using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Aspire.Hosting.Testing;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.DomainService;
using Hexalith.Parties.IntegrationTests.HealthChecks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Checks real gateway stream reads, independent actor bytes and authenticated domain replay.</summary>
internal static class MemoriesAuthoritativePersistence
{
    internal static async Task AssertAsync(PartiesAspireTopologyFixture fixture, Uri sidecar, string apiToken,
        string privateKey, string privateRoot, CancellationToken cancellationToken)
    {
        using JsonDocument authority = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(privateRoot, "runtime.authority.json"), cancellationToken).ConfigureAwait(false));
        string tenant = authority.RootElement.GetProperty("TenantId").GetString()!;
        string caseId = authority.RootElement.GetProperty("CaseId").GetString()!;
        string unit = authority.RootElement.GetProperty("UnitIds").EnumerateArray().Last().GetString()!;
        using HttpClient admin = fixture.App.CreateHttpClient("eventstore");
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PartiesAspireTopologyFixture.CreateAdministratorToken());
        var observations = new List<object>();
        foreach ((string domain, string aggregate, string expectedEvent) in new[]
        {
            ("memories-tenants", tenant, "TenantRegisteredEvent"),
            ("memories-cases", caseId, "CaseCreatedEvent"),
            ("memories-memory-units", unit, "MemoryUnitDeletionRequestedEvent"),
        })
        {
            var builder = new HostApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:JwtBearer:Issuer"] = PartiesAspireTopologyFixture.JwtIssuer,
                ["Authentication:JwtBearer:SigningKey"] = PartiesAspireTopologyFixture.JwtSigningKey,
                ["Authentication:JwtBearer:Audience"] = fixture.Session.AppId("eventstore"),
                ["Authentication:JwtBearer:AllowedAlgorithms:0"] = "HS256",
                ["Authentication:WorkloadIssuer:Workload"] = fixture.Session.AppId("memories"),
            });
            builder.Services.AddSingleton<IEventStoreGatewayStreamReadProofSource>(new OwnedProof(fixture.Session.AppId("memories"), privateKey));
            builder.Services.AddEventStoreGatewayClient(options => options.BaseAddress = sidecar)
                .AddEventStoreGatewayWorkloadTransport(domain, fixture.Session.AppId("eventstore"))
                .AddEventStoreDaprServiceInvocation(fixture.Session.AppId("eventstore"), apiToken);
            using IHost host = builder.Build();
            StreamReadPage page = await host.Services.GetRequiredService<IEventStoreGatewayClient>().ReadWorkloadStreamAsync(
                new StreamReadRequest(tenant, domain, aggregate, PageSize: 100) { RequireNativeProof = true }, cancellationToken).ConfigureAwait(false);
            page.Tenant.ShouldBe(tenant); page.Domain.ShouldBe(domain); page.AggregateId.ShouldBe(aggregate);
            page.Metadata.IsTruncated.ShouldBeFalse(); page.Metadata.NativeProof.ShouldNotBeNull().Erased.ShouldBeFalse();
            page.Events.ShouldNotBeEmpty();
            page.Events.ShouldContain(item => item.EventTypeName.EndsWith("." + expectedEvent, StringComparison.Ordinal));
            var stored = await fixture.ReadStoredStreamAsync(new AggregateIdentity(tenant, domain, aggregate), cancellationToken).ConfigureAwait(false);
            page.Metadata.LatestSequence.ShouldBe(stored.Metadata.CurrentSequence);
            page.Events.Count.ShouldBe(stored.Events.Length);
            for (int index = 0; index < stored.Events.Length; index++)
            {
                var actual = stored.Events[index];
                actual.TenantId.ShouldBe(tenant); actual.Domain.ShouldBe(domain); actual.AggregateId.ShouldBe(aggregate);
                actual.SequenceNumber.ShouldBe(index + 1L);
                actual.EventTypeName.ShouldStartWith("Hexalith.Memories.EventStore.Domain.Events.");
                page.Events[index].SequenceNumber.ShouldBe(actual.SequenceNumber);
                page.Events[index].Payload.ShouldBe(actual.Payload);
                page.Events[index].NativeEventDigest.ShouldNotBeNullOrWhiteSpace();
                using JsonDocument payload = JsonDocument.Parse(actual.Payload);
                payload.RootElement.GetProperty("TenantId").GetString().ShouldBe(tenant);
            }
            foreach (long sequence in new[] { 1L, stored.Metadata.CurrentSequence }.Distinct())
            {
                string route = $"api/v1/admin/streams/{Uri.EscapeDataString(tenant)}/{domain}/{Uri.EscapeDataString(aggregate)}/state?at={sequence}";
                using HttpResponseMessage response = await admin.GetAsync(route, cancellationToken).ConfigureAwait(false);
                string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                response.IsSuccessStatusCode.ShouldBeTrue("The actual authenticated gateway must reconstruct through the real domain host: " + body);
                using JsonDocument replay = JsonDocument.Parse(body);
                replay.RootElement.GetProperty("sequenceNumber").GetInt64().ShouldBe(sequence);
                using JsonDocument state = JsonDocument.Parse(replay.RootElement.GetProperty("stateJson").GetString()!);
                state.RootElement.GetProperty("tenantId").GetString().ShouldBe(tenant);
                if (domain != "memories-tenants") { state.RootElement.GetProperty("caseId").GetString().ShouldBe(caseId); }
                if (domain == "memories-memory-units")
                {
                    state.RootElement.GetProperty("memoryUnitId").GetString().ShouldBe(unit);
                    state.RootElement.GetProperty("deletionRequested").GetBoolean().ShouldBeTrue();
                }
                if (domain == "memories-cases") { state.RootElement.GetProperty("deletionRequested").GetBoolean().ShouldBe(sequence > 1); }
                observations.Add(new { Domain = domain, Aggregate = aggregate, Sequence = sequence, EventCount = page.Events.Count, NativeProof = true, Replay = true });
            }
            var reread = await fixture.ReadStoredStreamAsync(new AggregateIdentity(tenant, domain, aggregate), cancellationToken).ConfigureAwait(false);
            reread.Metadata.CurrentSequence.ShouldBe(stored.Metadata.CurrentSequence);
            reread.Events.Select(item => item.MessageId).ShouldBe(stored.Events.Select(item => item.MessageId));
        }
        await File.WriteAllTextAsync(Path.Combine(privateRoot, "authoritative-persistence.json"), JsonSerializer.Serialize(observations), cancellationToken).ConfigureAwait(false);
    }

    private sealed class OwnedProof(string caller, string privateKey) : IEventStoreGatewayStreamReadProofSource
    {
        public ValueTask<string?> IssueAsync(StreamReadRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<string?>(BoundStreamReadProof.Issue(request, caller, privateKey, DateTimeOffset.UtcNow));
        }
    }
}
