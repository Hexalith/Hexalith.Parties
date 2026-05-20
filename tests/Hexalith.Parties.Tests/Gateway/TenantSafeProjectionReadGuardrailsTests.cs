using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Search;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Abstractions;
using Hexalith.Parties.Projections.Actors;
using Hexalith.Parties.Projections.Configuration;
using Hexalith.Parties.Projections.Services;
using Hexalith.Parties.Projections.Strategies;
using Hexalith.Parties.Queries;
using Hexalith.Parties.Search;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.Tests.Gateway;

// ATDD red-phase scaffold for Story 2.6 — Enforce Tenant-Safe Projection Reads.
// Test-design risk references: R-01 cross-tenant leakage (P0), R-03 untrusted tenant source (P1),
// R-04 erased-PII resurfacing (P1), R-05 mixed-provenance static cache (P1).
public sealed class TenantSafeProjectionReadGuardrailsTests
{
    // AC1 — Missing tenant on the envelope must fail before any IPartyDetailProjectionActor
    // construction. Reference: 2.6-GTW-001, 2.6-UNIT-006.
    [Fact]
    public async Task QueryAsync_MissingTenantInEnvelope_FailsBeforeActorConstructionAsync()
    {
        IActorProxyFactory proxyFactory = Substitute.For<IActorProxyFactory>();
        PartyDetailProjectionQueryActor actor = CreateDetailActor("party-detail::p-1", proxyFactory);

        ArgumentException exception = await Should.ThrowAsync<ArgumentException>(
            () => Task.FromResult(CreateEnvelope(tenantId: "", entityId: "p-1", queryType: "PartyDetail")));

        exception.ParamName.ShouldBe("tenantId");
        proxyFactory.DidNotReceiveWithAnyArgs().CreateActorProxy<IPartyDetailProjectionActor>(
            default!, default!, default);
        _ = actor;
    }

    // AC2/AC3 — A caller probing with payload-tenant-like overrides cannot force the adapter to
    // read another tenant's projection state; the authenticated envelope tenant is authoritative.
    // Reference: 2.6-GTW-020, 2.6-FIT-023, 2.6-FIT-143.
    [Fact]
    public async Task QueryAsync_PayloadTenantLikeOverride_IgnoredInFavorOfAuthenticatedTenantAsync()
    {
        IActorProxyFactory proxyFactory = Substitute.For<IActorProxyFactory>();
        IPartyDetailProjectionActor detailActor = Substitute.For<IPartyDetailProjectionActor>();
        detailActor.GetDetailAsync().Returns(Task.FromResult<PartyDetail?>(null));
        proxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
                Arg.Any<ActorId>(),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
            .Returns(detailActor);
        PartyDetailProjectionQueryActor actor = CreateDetailActor("party-detail:tenant-b:p-1", proxyFactory);

        // Envelope authenticated as tenant-b, but a malicious payload field/header carries "tenant-a".
        // The adapter must derive the actor id from tenant-b (envelope) and never construct a
        // tenant-a actor proxy.
        QueryResult result = await actor.QueryAsync(CreateEnvelopeWithPayloadTenantOverride(
            envelopeTenant: "tenant-b",
            payloadTenantOverride: "tenant-a",
            entityId: "p-1",
            queryType: "PartyDetail"));

        // Whether the result is success-with-null or bounded not-found, what matters is the actor id.
        proxyFactory.Received(1).CreateActorProxy<IPartyDetailProjectionActor>(
            Arg.Is<ActorId>(id => id.GetId() == "tenant-b:party-detail:p-1"),
            nameof(PartyDetailProjectionActor),
            Arg.Any<ActorProxyOptions?>());
        proxyFactory.DidNotReceive().CreateActorProxy<IPartyDetailProjectionActor>(
            Arg.Is<ActorId>(id => id.GetId().StartsWith("tenant-a:", StringComparison.Ordinal)),
            Arg.Any<string>(),
            Arg.Any<ActorProxyOptions?>());
    }

    // AC3 — Wrong-tenant detail probe is non-enumerating: identical bounded outcome whether the
    // party exists in another tenant or does not exist anywhere. Reference: 2.6-GTW-002.
    [Fact]
    public async Task QueryAsync_WrongTenantProbe_HasSameBoundedOutcomeAsAbsentPartyAsync()
    {
        IActorProxyFactory proxyFactory = Substitute.For<IActorProxyFactory>();
        IPartyDetailProjectionActor empty = Substitute.For<IPartyDetailProjectionActor>();
        empty.GetDetailAsync().Returns(Task.FromResult<PartyDetail?>(null));
        proxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
                Arg.Any<ActorId>(),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
            .Returns(empty);

        PartyDetailProjectionQueryActor wrongTenantActor = CreateDetailActor("party-detail:tenant-b:p-existing-in-a", proxyFactory);
        PartyDetailProjectionQueryActor absentActor = CreateDetailActor("party-detail:tenant-b:p-missing", proxyFactory);

        QueryResult wrongTenantResult = await wrongTenantActor.QueryAsync(
            CreateEnvelope("tenant-b", "p-existing-in-a", "PartyDetail"));
        QueryResult absentResult = await absentActor.QueryAsync(
            CreateEnvelope("tenant-b", "p-missing", "PartyDetail"));

        // The two outcomes must be observationally identical: same Success, same ErrorMessage,
        // same payload shape. No part of the response may reveal that p-existing-in-a exists
        // in another tenant.
        wrongTenantResult.Success.ShouldBe(absentResult.Success);
        wrongTenantResult.ErrorMessage.ShouldBe(absentResult.ErrorMessage);
        wrongTenantResult.ProjectionType.ShouldBe(absentResult.ProjectionType);
    }

    // AC6 — Static last-known-detail cache (s_lastKnownDetails) must not return tenant-A entries
    // to tenant-B even when the in-memory dictionary survives actor reactivation across tenants.
    // Reference: 2.6-UNIT-007, closes Story 2.3 D2 carry-forward.
    [Fact]
    public async Task StaticLastKnownDetailCache_LookupKey_IncludesTenantSegmentToPreventCrossTenantReuseAsync()
    {
        (PartyDetailProjectionActor tenantAActor, IActorStateManager tenantAState) =
            CreateDetailProjectionActor("tenant-a:party-detail:p-shared");
        tenantAState.TryGetStateAsync<PartyDetail>(
                "tenant-a:party-detail:p-shared",
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PartyDetail>(true, CreateDetail("p-shared", "Ada Tenant A")));

        PartyDetail? tenantADetail = await tenantAActor.GetDetailAsync();
        tenantADetail.ShouldNotBeNull();
        tenantADetail.DisplayName.ShouldBe("Ada Tenant A");

        (PartyDetailProjectionActor tenantBActor, IActorStateManager tenantBState) =
            CreateDetailProjectionActor("tenant-b:party-detail:p-shared");
        tenantBActor.SetRebuilding(true);

        PartyDetail? tenantBDetail = await tenantBActor.GetDetailAsync();

        tenantBDetail.ShouldBeNull();
        await tenantBState.DidNotReceive().TryGetStateAsync<PartyDetail>(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    // AC2/AC6 — Erase + static cache coordination: after EraseAsync clears the actor's PII, a
    // subsequent activation must not resurface PII from the static cache. Reference: 2.6-UNIT-031,
    // closes Story 2.3 D2.
    [Fact]
    public async Task EraseAsync_ThenReactivation_DoesNotResurrectPiiFromStaticCacheAsync()
    {
        (PartyDetailProjectionActor actor, IActorStateManager state) =
            CreateDetailProjectionActor("tenant-a:party-detail:p-erased");
        PartyDetail seed = CreateDetail("p-erased", "Ada Lovelace");
        state.TryGetStateAsync<PartyDetail>(
                "tenant-a:party-detail:p-erased",
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PartyDetail>(true, seed));

        await actor.GetDetailAsync();
        await actor.EraseAsync("p-erased");

        (PartyDetailProjectionActor reactivated, _) =
            CreateDetailProjectionActor("tenant-a:party-detail:p-erased");
        reactivated.SetRebuilding(true);

        PartyDetail? detail = await reactivated.GetDetailAsync();

        detail.ShouldNotBeNull();
        detail.IsErased.ShouldBeTrue();
        detail.DisplayName.ShouldNotContain("Ada", Case.Insensitive);
    }

    // AC4 — Index list query must derive partition state key from authenticated tenant and never
    // from a caller-supplied partition key. Reference: 2.6-GTW-021.
    [Fact]
    public async Task QueryAsync_IndexQueryWithCallerSuppliedPartitionKey_FailsBeforeActorConstructionAsync()
    {
        IActorProxyFactory proxyFactory = Substitute.For<IActorProxyFactory>();
        IPartyIndexProjectionActor indexActor = Substitute.For<IPartyIndexProjectionActor>();
        indexActor.GetEntriesAsync().Returns(Task.FromResult<IReadOnlyDictionary<string, PartyIndexEntry>>(
            new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal)));
        proxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
                Arg.Any<ActorId>(),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
            .Returns(indexActor);
        PartyIndexProjectionQueryActor actor = CreateIndexActor("party-index:tenant-b:parties", proxyFactory);

        // Caller tries to inject "tenant-a:party-index" through payload metadata.
        QueryResult result = await actor.QueryAsync(CreateEnvelopeWithPayloadPartitionOverride(
            envelopeTenant: "tenant-b",
            payloadActorId: "tenant-a:party-index",
            queryType: "PartyIndex"));

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.InvalidEnvelope);
        proxyFactory.DidNotReceive().CreateActorProxy<IPartyIndexProjectionActor>(
            Arg.Any<ActorId>(),
            Arg.Any<string>(),
            Arg.Any<ActorProxyOptions?>());
        proxyFactory.DidNotReceive().CreateActorProxy<IPartyIndexProjectionActor>(
            Arg.Is<ActorId>(id => id.GetId().StartsWith("tenant-a:", StringComparison.Ordinal)),
            Arg.Any<string>(),
            Arg.Any<ActorProxyOptions?>());
    }

    // AC4 — Cross-tenant list overlap: tenant A and tenant B both have entries with the same
    // display names/dates. Tenant B's list must compute TotalCount/TotalPages strictly from
    // tenant-B-authorized entries. Reference: 2.6-INT-083 (Tier-3 integration variant).
    [Fact]
    public async Task ListQuery_OverlappingDisplayNamesAcrossTenants_TotalCountIsTenantScopedAsync()
    {
        IActorProxyFactory proxyFactory = Substitute.For<IActorProxyFactory>();
        IPartyIndexProjectionActor indexActor = Substitute.For<IPartyIndexProjectionActor>();
        indexActor.GetEntriesAsync().Returns(Task.FromResult<IReadOnlyDictionary<string, PartyIndexEntry>>(
            new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal)
            {
                ["tenant-b-party"] = CreateIndexEntry("tenant-b-party", "Shared Display Name"),
            }));
        proxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
                Arg.Any<ActorId>(),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
            .Returns(indexActor);
        PartyIndexProjectionQueryActor actor = CreateIndexActor("party-index:tenant-b:parties", proxyFactory);

        QueryResult result = await actor.QueryAsync(CreateIndexEnvelope(
            tenantId: "tenant-b",
            queryType: "PartyIndex",
            payload: JsonSerializer.SerializeToUtf8Bytes(new { page = 1, pageSize = 20 })));

        result.Success.ShouldBeTrue();
        PagedResult<PartyIndexEntry> page = result.GetPayload().Deserialize<PagedResult<PartyIndexEntry>>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        page.Items.Select(static i => i.Id).ShouldBe(["tenant-b-party"]);
        page.TotalCount.ShouldBe(1);
        page.TotalPages.ShouldBe(1);
    }

    // AC7 — Diagnostics must not include raw actor/state/partition keys, payload JSON, stack traces,
    // or tenant membership data on any of: success / not-found / corrupt / degraded / cancellation.
    // Reference: 2.6-GTW-010, 2.6-GTW-011 — extends existing log-scrub coverage in
    // PartyDetailProjectionQueryActorTests with adapter actor focus.
    [Fact]
    public async Task TenantSafeReadDiagnostics_NeverIncludeActorOrStateKeyTextAsync()
    {
        var detailLogger = new RecordingLogger<PartyDetailProjectionQueryActor>();
        IActorProxyFactory detailProxyFactory = Substitute.For<IActorProxyFactory>();
        IPartyDetailProjectionActor failingDetail = Substitute.For<IPartyDetailProjectionActor>();
        failingDetail.GetDetailAsync().Returns<PartyDetail?>(_ => throw new InvalidOperationException("state key tenant-a:party-detail:p-1"));
        detailProxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
                Arg.Any<ActorId>(),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
            .Returns(failingDetail);
        PartyDetailProjectionQueryActor detailActor = CreateDetailActor(
            "party-detail:tenant-a:p-1",
            detailProxyFactory,
            detailLogger);

        await detailActor.QueryAsync(CreateEnvelope("tenant-a", "p-1", "PartyDetail"));

        var indexLogger = new RecordingLogger<PartyIndexProjectionQueryActor>();
        IActorProxyFactory indexProxyFactory = Substitute.For<IActorProxyFactory>();
        IPartyIndexProjectionActor failingIndex = Substitute.For<IPartyIndexProjectionActor>();
        failingIndex.GetEntriesAsync().Returns<IReadOnlyDictionary<string, PartyIndexEntry>>(
            _ => throw new InvalidOperationException("state key tenant-a:party-index:default"));
        indexProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
                Arg.Any<ActorId>(),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
            .Returns(failingIndex);
        PartyIndexProjectionQueryActor indexActor = CreateIndexActor(
            "party-index:tenant-a:parties",
            indexProxyFactory,
            indexLogger);

        await indexActor.QueryAsync(CreateIndexEnvelope(
            tenantId: "tenant-a",
            queryType: "PartyIndex",
            payload: JsonSerializer.SerializeToUtf8Bytes(new { page = 1, pageSize = 20 })));

        var detailProjectionLogger = new RecordingLogger<PartyDetailProjectionActor>();
        (PartyDetailProjectionActor malformedDetailProjection, _) =
            CreateDetailProjectionActor("tenant-a:wrong-projection:p-1", detailProjectionLogger);
        await malformedDetailProjection.GetDetailAsync();

        var indexProjectionLogger = new RecordingLogger<PartyIndexProjectionActor>();
        (PartyIndexProjectionActor malformedIndexProjection, _) =
            CreateIndexProjectionActor("tenant-a:party-index:extra", indexProjectionLogger);
        await malformedIndexProjection.GetEntriesAsync();

        IEnumerable<string> messages = detailLogger.Records.Select(static r => r.Message)
            .Concat(indexLogger.Records.Select(static r => r.Message))
            .Concat(detailProjectionLogger.Records.Select(static r => r.Message))
            .Concat(indexProjectionLogger.Records.Select(static r => r.Message));
        messages.ShouldNotBeEmpty();
        foreach (string message in messages)
        {
            message.ShouldNotContain("tenant-a:", Case.Insensitive);
            message.ShouldNotContain("party-detail:", Case.Insensitive);
            message.ShouldNotContain("party-index:", Case.Insensitive);
            message.ShouldNotContain(":last-sequence", Case.Insensitive);
            message.ShouldNotContain("stateKey", Case.Insensitive);
            message.ShouldNotContain("streamName", Case.Insensitive);
            message.ShouldNotContain("Bearer ", Case.Sensitive);
        }
    }

    private static PartyDetailProjectionQueryActor CreateDetailActor(string actorId, IActorProxyFactory proxyFactory)
        => CreateDetailActor(actorId, proxyFactory, NullLogger<PartyDetailProjectionQueryActor>.Instance);

    private static PartyDetailProjectionQueryActor CreateDetailActor(
        string actorId,
        IActorProxyFactory proxyFactory,
        Microsoft.Extensions.Logging.ILogger<PartyDetailProjectionQueryActor> logger)
    {
        ActorTimerManager timerManager = Substitute.For<ActorTimerManager>();
        var host = ActorHost.CreateForTest<PartyDetailProjectionQueryActor>(
            new ActorTestOptions { ActorId = new ActorId(actorId), TimerManager = timerManager });
        return new PartyDetailProjectionQueryActor(host, proxyFactory, logger);
    }

    private static PartyIndexProjectionQueryActor CreateIndexActor(string actorId, IActorProxyFactory proxyFactory)
        => CreateIndexActor(actorId, proxyFactory, NullLogger<PartyIndexProjectionQueryActor>.Instance);

    private static PartyIndexProjectionQueryActor CreateIndexActor(
        string actorId,
        IActorProxyFactory proxyFactory,
        Microsoft.Extensions.Logging.ILogger<PartyIndexProjectionQueryActor> logger)
    {
        ActorTimerManager timerManager = Substitute.For<ActorTimerManager>();
        var host = ActorHost.CreateForTest<PartyIndexProjectionQueryActor>(
            new ActorTestOptions { ActorId = new ActorId(actorId), TimerManager = timerManager });
        IPartySearchProvider searchProvider = new LocalFuzzyPartySearchProvider();
        IHostApplicationLifetime hostLifetime = new StubHostApplicationLifetime();
        return new PartyIndexProjectionQueryActor(host, proxyFactory, searchProvider, hostLifetime, logger);
    }

    private sealed class StubHostApplicationLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;

        public CancellationToken ApplicationStopping => CancellationToken.None;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication()
        {
        }
    }

    private static QueryEnvelope CreateEnvelope(string tenantId, string entityId, string queryType)
        => new(
            tenantId: tenantId,
            domain: "party",
            aggregateId: entityId,
            queryType: queryType,
            payload: [],
            correlationId: $"corr-{entityId}",
            userId: "user-1",
            entityId: entityId);

    private static QueryEnvelope CreateEnvelopeWithPayloadTenantOverride(
        string envelopeTenant,
        string payloadTenantOverride,
        string entityId,
        string queryType)
        => new(
            tenantId: envelopeTenant,
            domain: "party",
            aggregateId: entityId,
            queryType: queryType,
            payload: JsonSerializer.SerializeToUtf8Bytes(new { tenantId = payloadTenantOverride }),
            correlationId: $"corr-{entityId}",
            userId: "user-1",
            entityId: entityId);

    private static QueryEnvelope CreateEnvelopeWithPayloadPartitionOverride(
        string envelopeTenant,
        string payloadActorId,
        string queryType)
        => CreateIndexEnvelope(
            tenantId: envelopeTenant,
            queryType: queryType,
            payload: JsonSerializer.SerializeToUtf8Bytes(new
            {
                page = 1,
                pageSize = 20,
                actorId = payloadActorId,
                partitionKey = payloadActorId,
            }));

    private static QueryEnvelope CreateIndexEnvelope(string tenantId, string queryType, byte[] payload)
        => new(
            tenantId: tenantId,
            domain: "party",
            aggregateId: "parties",
            queryType: queryType,
            payload: payload,
            correlationId: $"corr-{queryType}",
            userId: "user-1",
            entityId: "parties");

    private static (PartyDetailProjectionActor Actor, IActorStateManager StateManager) CreateDetailProjectionActor(string actorId)
        => CreateDetailProjectionActor(actorId, NullLogger<PartyDetailProjectionActor>.Instance);

    private static (PartyDetailProjectionActor Actor, IActorStateManager StateManager) CreateDetailProjectionActor(
        string actorId,
        Microsoft.Extensions.Logging.ILogger<PartyDetailProjectionActor> logger)
    {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        IProjectionRebuildService rebuildService = Substitute.For<IProjectionRebuildService>();
        ActorTimerManager timerManager = Substitute.For<ActorTimerManager>();
        var host = ActorHost.CreateForTest<PartyDetailProjectionActor>(
            new ActorTestOptions { ActorId = new ActorId(actorId), TimerManager = timerManager });
        var actor = new PartyDetailProjectionActor(host, rebuildService, logger);

        PropertyInfo? prop = typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance);
        prop?.SetValue(actor, stateManager);
        return (actor, stateManager);
    }

    private static (PartyIndexProjectionActor Actor, IActorStateManager StateManager) CreateIndexProjectionActor(
        string actorId,
        Microsoft.Extensions.Logging.ILogger<PartyIndexProjectionActor> logger)
    {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        IProjectionRebuildService rebuildService = Substitute.For<IProjectionRebuildService>();
        ActorTimerManager timerManager = Substitute.For<ActorTimerManager>();
        var host = ActorHost.CreateForTest<PartyIndexProjectionActor>(
            new ActorTestOptions { ActorId = new ActorId(actorId), TimerManager = timerManager });
        var actor = new PartyIndexProjectionActor(
            host,
            new SingleKeyPartitionStrategy(),
            Options.Create(new ProjectionOptions()),
            rebuildService,
            logger);

        PropertyInfo? prop = typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance);
        prop?.SetValue(actor, stateManager);
        return (actor, stateManager);
    }

    private static PartyDetail CreateDetail(string id, string displayName)
        => new()
        {
            Id = id,
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = displayName,
            SortName = displayName,
            CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            LastModifiedAt = DateTimeOffset.Parse("2026-05-02T00:00:00Z"),
        };

    private static PartyIndexEntry CreateIndexEntry(string id, string displayName)
        => new()
        {
            Id = id,
            Type = PartyType.Organization,
            IsActive = true,
            DisplayName = displayName,
            CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            LastModifiedAt = DateTimeOffset.Parse("2026-05-02T00:00:00Z"),
        };

    private sealed class RecordingLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public List<(Microsoft.Extensions.Logging.LogLevel Level, string Message)> Records { get; } = [];

        IDisposable? Microsoft.Extensions.Logging.ILogger.BeginScope<TState>(TState state) => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            Records.Add((logLevel, formatter(state, exception)));
        }
    }
}
