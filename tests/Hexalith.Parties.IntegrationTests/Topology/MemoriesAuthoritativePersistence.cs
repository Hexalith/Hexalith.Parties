using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Aspire.Hosting.Testing;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.Parties.IntegrationTests.HealthChecks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

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
        string[] units = CanonicalUnits(authority.RootElement);
        using HttpClient admin = fixture.App.CreateHttpClient("eventstore");
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PartiesAspireTopologyFixture.CreateAdministratorToken(tenant));
        var observations = new List<object>();
        var generationObservations = new List<object>();
        string otherTenant = authority.RootElement.GetProperty("OtherTenantId").GetString()!;
        using (HttpResponseMessage denied = await admin.GetAsync($"api/v1/admin/streams/{Uri.EscapeDataString(otherTenant)}/memories-tenants/{Uri.EscapeDataString(otherTenant)}/state?at=1", cancellationToken).ConfigureAwait(false))
        {
            denied.StatusCode.ShouldBe(System.Net.HttpStatusCode.Forbidden);
        }
        fixture.App.ResourceNotifications.TryGetCurrentState("memories", out ResourceEvent? memoriesState).ShouldBeTrue();
        string Coordinate(string name) => memoriesState!.Snapshot.EnvironmentVariables.Single(variable => variable.Name == "EventStore__RouteCatalog__" + name).Value
            ?? throw new InvalidDataException("The actual Memories authority coordinate is missing: " + name);
        string stateStore = Coordinate("StateStore"), stateKey = Coordinate("StateKey");
        bool negativeControlsComplete = false;
        foreach ((string domain, string aggregate, string expectedEvent) in new[]
        {
            ("memories-tenants", tenant, "TenantRegisteredEvent"),
            ("memories-cases", caseId, "CaseCreatedEvent"),
        }.Concat(units.Select(unit => ("memories-memory-units", unit, "MemoryUnitDeletionRequestedEvent"))))
        {
            var builder = new HostApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:JwtBearer:Issuer"] = PartiesAspireTopologyFixture.JwtIssuer,
                ["Authentication:JwtBearer:SigningKey"] = PartiesAspireTopologyFixture.JwtSigningKey,
                ["Authentication:JwtBearer:Audience"] = fixture.Session.AppId("eventstore"),
                ["Authentication:JwtBearer:AllowedAlgorithms:0"] = "HS256",
                ["Authentication:WorkloadIssuer:Workload"] = fixture.Session.AppId("memories"),
                ["DAPR_HTTP_ENDPOINT"] = sidecar.ToString(),
                ["DAPR_API_TOKEN"] = apiToken,
            });
            builder.Services.AddHttpClient("route-catalog-state");
            var stateOptions = Options.Create(new RouteCatalogHostOptions { StateStore = stateStore, StateKey = stateKey });
            IHost? boundHost = null;
            var bindingHandler = new OwnedRouteCatalogBindingHandler(async token =>
            {
                var store = new DaprRouteCatalogStateStore(boundHost!.Services.GetRequiredService<IHttpClientFactory>(), builder.Configuration, stateOptions);
                return (await store.ReadAsync(token).ConfigureAwait(false)).State;
            }, (mode, observedEpoch, sentEpoch, root, binding) => generationObservations.Add(new { Mode = mode, ObservedEpoch = observedEpoch, SentEpoch = sentEpoch, Root = root, Binding = binding, StateStore = stateStore, StateKey = stateKey }));
            builder.Services.AddSingleton<IEventStoreGatewayStreamReadProofSource>(new OwnedProof(fixture.Session.AppId("memories"), privateKey));
            builder.Services.AddEventStoreGatewayClient(options => options.BaseAddress = sidecar)
                .AddEventStoreGatewayWorkloadTransport(domain, fixture.Session.AppId("eventstore"))
                .AddHttpMessageHandler(() => bindingHandler)
                .AddEventStoreDaprServiceInvocation(fixture.Session.AppId("eventstore"), apiToken);
            using IHost host = builder.Build();
            boundHost = host;
            if (!negativeControlsComplete)
            {
                try
                {
                    foreach (string mode in new[] { "missing", "stale" })
                    {
                        bindingHandler.Mode = mode;
                        EventStoreGatewayException refusal = await Should.ThrowAsync<EventStoreGatewayException>(() => host.Services.GetRequiredService<IEventStoreGatewayClient>()
                            .ReadWorkloadStreamAsync(new StreamReadRequest(tenant, domain, aggregate, PageSize: 100) { RequireNativeProof = true }, cancellationToken)).ConfigureAwait(false);
                        refusal.StatusCode.ShouldBe(503);
                    }
                    negativeControlsComplete = true;
                }
                finally { bindingHandler.Mode = "current"; }
            }
            StreamReadPage page;
            try
            {
                page = await host.Services.GetRequiredService<IEventStoreGatewayClient>().ReadWorkloadStreamAsync(
                    new StreamReadRequest(tenant, domain, aggregate, PageSize: 100) { RequireNativeProof = true }, cancellationToken).ConfigureAwait(false);
            }
            catch (EventStoreGatewayException exception)
            {
                var diagnostics = new List<object> { new { Tenant = tenant, Domain = domain, Aggregate = aggregate }, new { exception.StatusCode, exception.Title, exception.Detail, exception.Code,
                    exception.Reason, exception.ReasonCode, exception.Category, exception.ClientAction, exception.Extensions } };
                diagnostics.Add(new { CatalogGenerations = generationObservations.TakeLast(3).ToArray() });
                await CaptureNativeRefusalAsync(exception, async diagnosticToken =>
                {
                    AggregateIdentity identity = new(tenant, domain, aggregate);
                    var persisted = await fixture.ReadStoredStreamAsync(identity, diagnosticToken).ConfigureAwait(false);
                    diagnostics.Add(new { PersistedMetadata = persisted.Metadata, EventCount = persisted.Events.Length });
                    using HttpClient actor = fixture.CreateSidecarClient("eventstore");
                    using var inspection = new HttpRequestMessage(HttpMethod.Put, "/v1.0/actors/AggregateActor/" + Uri.EscapeDataString(identity.ActorId)
                        + "/method/InspectSourceProofAsync");
                    using HttpResponseMessage inspected = await actor.SendAsync(inspection, HttpCompletionOption.ResponseHeadersRead, diagnosticToken).ConfigureAwait(false);
                    using Stream inspectionBody = await inspected.Content.ReadAsStreamAsync(diagnosticToken).ConfigureAwait(false);
                    diagnostics.Add(new { InspectionStatus = (int)inspected.StatusCode, InspectionBody = await ReadDiagnosticBodyAsync(inspectionBody, diagnosticToken).ConfigureAwait(false) });
                    IAggregateActor proxy = ActorProxy.Create<IAggregateActor>(new ActorId(identity.ActorId), "AggregateActor", new ActorProxyOptions
                    {
                        HttpEndpoint = actor.BaseAddress!.ToString(),
                        DaprApiToken = actor.DefaultRequestHeaders.GetValues("dapr-api-token").Single(),
                        RequestTimeout = TimeSpan.FromSeconds(4),
                    });
                    foreach ((string method, Func<Task<object>> observe) in new (string, Func<Task<object>>)[]
                    {
                        ("TypedInspectSourceProofAsync", async () => await proxy.InspectSourceProofAsync().ConfigureAwait(false)),
                        ("ReadNativeSourceBatchAsync", async () =>
                        {
                            SourceNativeReadBatch batch = await proxy.ReadNativeSourceBatchAsync(0, null, 101, 2_097_152, false).ConfigureAwait(false);
                            return new { batch.Before, batch.After, Range = RangeDiagnostic(batch.Range) };
                        }),
                        ("ReadNativeEventsBoundedRangeAsync", async () => RangeDiagnostic(await proxy.ReadNativeEventsBoundedRangeAsync(0,
                            persisted.Metadata.CurrentSequence, 101, 2_097_152).ConfigureAwait(false))!),
                        ("TypedInspectSourceProofAfterAsync", async () => await proxy.InspectSourceProofAsync().ConfigureAwait(false)),
                    })
                    {
                        diagnosticToken.ThrowIfCancellationRequested();
                        try { diagnostics.Add(new { Method = method, Observation = await observe().ConfigureAwait(false) }); }
                        catch (Exception failure) { diagnostics.Add(new { Method = method, Failure = failure.ToString() }); }
                    }

                }, async (captureFailure, diagnosticToken) =>
                {
                    if (captureFailure is not null) { diagnostics.Add(new { DiagnosticFailure = captureFailure.ToString() }); }
                    await File.WriteAllTextAsync(Path.Combine(privateRoot, "native-proof-refusal.json"), JsonSerializer.Serialize(diagnostics), diagnosticToken).ConfigureAwait(false);
                }).ConfigureAwait(false);
                throw;
            }
            page.Tenant.ShouldBe(tenant); page.Domain.ShouldBe(domain); page.AggregateId.ShouldBe(aggregate);
            page.Metadata.IsTruncated.ShouldBeFalse(); page.Metadata.NativeProof.ShouldNotBeNull().Erased.ShouldBeFalse();
            page.Events.ShouldNotBeEmpty();
            page.Events.ShouldContain(item => item.EventTypeName.EndsWith("." + expectedEvent, StringComparison.Ordinal));
            var stored = await fixture.ReadStoredStreamAsync(new AggregateIdentity(tenant, domain, aggregate), cancellationToken).ConfigureAwait(false);
            AuthoritativeEventStream verified = await ReadVerifiedNativeStreamAsync(host.Services.GetRequiredService<IEventStoreGatewayClient>(),
                new AggregateIdentity(tenant, domain, aggregate), stored.Metadata.SourceProof
                    ?? throw new InvalidDataException("The fresh owned stream has no independent stored source proof."), cancellationToken).ConfigureAwait(false);
            verified.Head.ShouldBe(page.Metadata.LatestSequence);
            verified.NativeProof.ShouldBe(page.Metadata.NativeProof);
            JsonSerializer.Serialize(verified.Events).ShouldBe(JsonSerializer.Serialize(page.Events));
            page.Metadata.LatestSequence.ShouldBe(stored.Metadata.CurrentSequence);
            page.Events.Count.ShouldBe(stored.Events.Length);
            for (int index = 0; index < stored.Events.Length; index++)
            {
                var actual = stored.Events[index];
                actual.TenantId.ShouldBe(tenant); actual.Domain.ShouldBe(domain); actual.AggregateId.ShouldBe(aggregate);
                actual.SequenceNumber.ShouldBe(index + 1L);
                actual.EventTypeName.ShouldStartWith("Hexalith.Memories.EventStore.Domain.Events.");
                page.Events[index].MessageId.ShouldBe(actual.MessageId);
                page.Events[index].EventTypeName.ShouldBe(actual.EventTypeName);
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
                    state.RootElement.GetProperty("memoryUnitId").GetString().ShouldBe(aggregate);
                }
                if (domain is "memories-cases" or "memories-memory-units")
                {
                    state.RootElement.GetProperty("deletionRequested").GetBoolean().ShouldBe(
                        DeletionRequestedAt(stored.Events.Select(item => item.EventTypeName), sequence));
                }
                observations.Add(new { Domain = domain, Aggregate = aggregate, Sequence = sequence, EventCount = page.Events.Count, NativeProof = true, Replay = true });
            }
            var reread = await fixture.ReadStoredStreamAsync(new AggregateIdentity(tenant, domain, aggregate), cancellationToken).ConfigureAwait(false);
            reread.Metadata.CurrentSequence.ShouldBe(stored.Metadata.CurrentSequence);
            JsonSerializer.Serialize(reread.Events).ShouldBe(JsonSerializer.Serialize(stored.Events));
            StreamReadPage stable = await host.Services.GetRequiredService<IEventStoreGatewayClient>().ReadWorkloadStreamAsync(
                new StreamReadRequest(tenant, domain, aggregate, PageSize: 100) { RequireNativeProof = true }, cancellationToken).ConfigureAwait(false);
            JsonSerializer.Serialize(stable).ShouldBe(JsonSerializer.Serialize(page));
        }
        await File.WriteAllTextAsync(Path.Combine(privateRoot, "catalog-generation-controls.json"), JsonSerializer.Serialize(generationObservations), cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(privateRoot, "authoritative-persistence.json"), JsonSerializer.Serialize(observations), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Uses the workload client's native reader and binds its verified chain to independent actor metadata.</summary>
    internal static async Task<AuthoritativeEventStream> ReadVerifiedNativeStreamAsync(IEventStoreGatewayClient gateway,
        AggregateIdentity identity, SourceProof storedProof, CancellationToken token)
    {
        AuthoritativeStreamReadResult result = await new AuthoritativeEventStreamReader(gateway, TimeProvider.System, requireNativeProof: true)
            .ReadAsync(identity, token).ConfigureAwait(false);
        if (!result.IsAuthoritative) { throw new InvalidDataException("Native stream verification refused: " + result.FailureReason); }
        AuthoritativeEventStream stream = result.Stream!;
        var independent = new NativeSourceProof(storedProof.CodecVersion, storedProof.TenantId, storedProof.Domain, storedProof.AggregateId,
            storedProof.SourceIncarnation, storedProof.HeadSequence, storedProof.HeadMessageId, storedProof.CumulativeDigest, storedProof.RetainedFloor, storedProof.Erased);
        if (independent.Erased || !NativeSourceProofCodec.IsValid(independent, identity, stream.Head, 1) || stream.NativeProof != independent)
        { throw new InvalidDataException("Verified native proof differs from independent stored actor metadata."); }
        return stream;
    }

    private static object? RangeDiagnostic(SourceEventRange? range) => range is null ? null : new
    {
        range.ReasonCode, range.EnvelopeArrayBytes, EventCount = range.Events?.Length,
        Events = range.Events?.Take(4).Select(item => new { item.TenantId, item.Domain, item.AggregateId, item.SequenceNumber, item.MessageId, item.EventTypeName }).ToArray(),
    };

    /// <summary>Requires both independently recorded revision outputs and exact agreement with the unit inventory.</summary>
    internal static string[] CanonicalUnits(JsonElement authority)
    {
        string[] units = authority.GetProperty("UnitIds").EnumerateArray().Select(item => item.GetString()!).ToArray();
        JsonElement[] outputs = authority.GetProperty("CanonicalOutputs").EnumerateArray().ToArray();
        if (units.Length != 2 || units.Any(string.IsNullOrWhiteSpace) || units.Distinct(StringComparer.Ordinal).Count() != 2
            || outputs.Length != 2 || !outputs.Select(item => item.GetProperty("Revision").GetInt32()).Order().SequenceEqual(new[] { 1, 2 })
            || !outputs.OrderBy(item => item.GetProperty("Revision").GetInt32()).Select(item => item.GetProperty("MemoryUnitId").GetString()).SequenceEqual(units))
        { throw new InvalidDataException("Both canonical revision outputs must have exact independent authority."); }
        return units;
    }

    /// <summary>Derives deletion state from the actual event prefix, including deletion as the first event.</summary>
    internal static bool DeletionRequestedAt(IEnumerable<string> eventTypes, long sequence)
        => eventTypes.Take(checked((int)sequence)).Any(type => type.EndsWith("DeletionRequestedEvent", StringComparison.Ordinal));

    /// <summary>Bounds each diagnostic response body independently of actor response size.</summary>
    internal static async Task<string> ReadDiagnosticBodyAsync(Stream body, CancellationToken token)
    {
        byte[] bytes = new byte[32769];
        int read = await body.ReadAtLeastAsync(bytes, bytes.Length, throwOnEndOfStream: false, token).ConfigureAwait(false);
        return System.Text.Encoding.UTF8.GetString(bytes, 0, Math.Min(read, 32768)) + (read > 32768 ? " [truncated]" : string.Empty);
    }

    /// <summary>Requests bounded cooperative capture cancellation, awaits its cleanup, then gives persistence a fresh lifetime while preserving the refusal.</summary>
    internal static async Task CaptureNativeRefusalAsync(Exception primaryFailure, Func<CancellationToken, Task> capture,
        Func<Exception?, CancellationToken, Task> persist, TimeSpan? captureBudget = null)
    {
        using var captureDeadline = new CancellationTokenSource(captureBudget ?? TimeSpan.FromSeconds(15));
        Exception? captureFailure = null;
        // The I/O delegates must cooperate with cancellation. Await their actual completion,
        // including finally cleanup, instead of detaching them when the deadline expires.
        try { await capture(captureDeadline.Token).ConfigureAwait(false); }
        catch (Exception exception) { captureFailure = exception; }
        using var persistenceDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try { await persist(captureFailure, persistenceDeadline.Token).ConfigureAwait(false); }
        catch (Exception receiptFailure)
        {
            List<Exception> failures = [primaryFailure];
            if (captureFailure is not null) { failures.Add(captureFailure); }
            failures.Add(receiptFailure);
            throw new AggregateException("Native proof refused and diagnostic capture failed.", failures);
        }
        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(primaryFailure).Throw();
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
