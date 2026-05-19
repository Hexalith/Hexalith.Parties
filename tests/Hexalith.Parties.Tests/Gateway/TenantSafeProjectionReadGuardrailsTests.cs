using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Projections.Abstractions;
using Hexalith.Parties.Projections.Actors;
using Hexalith.Parties.Queries;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.Tests.Gateway;

// ATDD red-phase scaffold for Story 2.6 — Enforce Tenant-Safe Projection Reads.
// Test-design risk references: R-01 cross-tenant leakage (P0), R-03 untrusted tenant source (P1),
// R-04 erased-PII resurfacing (P1), R-05 mixed-provenance static cache (P1).
// Each [Fact(Skip = "...")] is a red-phase scaffold — activate during dev-story.
public sealed class TenantSafeProjectionReadGuardrailsTests
{
    // AC1 — Missing tenant on the envelope must fail before any IPartyDetailProjectionActor
    // construction. Reference: 2.6-GTW-001, 2.6-UNIT-006.
    [Fact(Skip = "ATDD red-phase scaffold for Story 2.6 / R-01 — activate in dev-story")]
    public async Task QueryAsync_MissingTenantInEnvelope_FailsBeforeActorConstructionAsync()
    {
        IActorProxyFactory proxyFactory = Substitute.For<IActorProxyFactory>();
        PartyDetailProjectionQueryActor actor = CreateDetailActor("party-detail::p-1", proxyFactory);

        QueryResult result = await actor.QueryAsync(CreateEnvelope(tenantId: "", entityId: "p-1", queryType: "PartyDetail"));

        result.Success.ShouldBeFalse();
        proxyFactory.DidNotReceiveWithAnyArgs().CreateActorProxy<IPartyDetailProjectionActor>(
            default!, default!, default);
    }

    // AC2/AC3 — A caller probing with payload-tenant-like overrides cannot force the adapter to
    // read another tenant's projection state; the authenticated envelope tenant is authoritative.
    // Reference: 2.6-GTW-020, 2.6-FIT-023, 2.6-FIT-143.
    [Fact(Skip = "ATDD red-phase scaffold for Story 2.6 / R-03 — activate in dev-story")]
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
    [Fact(Skip = "ATDD red-phase scaffold for Story 2.6 / R-01 — activate in dev-story")]
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
    [Fact(Skip = "ATDD red-phase scaffold for Story 2.6 / R-05 — activate in dev-story")]
    public void StaticLastKnownDetailCache_LookupKey_IncludesTenantSegmentToPreventCrossTenantReuse()
    {
        // Red-phase shape: when this test is activated, it should construct two PartyDetailProjectionActor
        // hosts with actor ids "tenant-a:party-detail:p-shared" and "tenant-b:party-detail:p-shared",
        // populate the static cache from tenant A, then assert tenant B activation cannot read the
        // tenant A cached entry. Until the cache is provably keyed by full tenant-scoped actor id,
        // this is a contract guard.
        Assert.Skip("Materialize once PartyDetailProjectionActor exposes a test seam for static cache lookup.");
    }

    // AC2/AC6 — Erase + static cache coordination: after EraseAsync clears the actor's PII, a
    // subsequent activation must not resurface PII from the static cache. Reference: 2.6-UNIT-031,
    // closes Story 2.3 D2.
    [Fact(Skip = "ATDD red-phase scaffold for Story 2.6 / R-04 — activate in dev-story")]
    public void EraseAsync_ThenReactivation_DoesNotResurrectPiiFromStaticCache()
    {
        // Red-phase shape: build PartyDetailProjectionActor under a test ActorHost, seed projection
        // state with PII, call EraseAsync, evict the in-memory actor, reactivate, and verify the
        // static cache cannot return the pre-erase detail. Failing until provenance gate is added.
        Assert.Skip("Materialize once PartyDetailProjectionActor exposes a test seam for static cache lookup.");
    }

    // AC4 — Index list query must derive partition state key from authenticated tenant and never
    // from a caller-supplied partition key. Reference: 2.6-GTW-021.
    [Fact(Skip = "ATDD red-phase scaffold for Story 2.6 / R-03 — activate in dev-story")]
    public async Task QueryAsync_IndexQueryWithCallerSuppliedPartitionKey_IgnoredInActorIdConstructionAsync()
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
        PartyIndexProjectionQueryActor actor = CreateIndexActor("party-index:tenant-b", proxyFactory);

        // Caller tries to inject "tenant-a:party-index" through payload metadata.
        QueryResult _ = await actor.QueryAsync(CreateEnvelopeWithPayloadPartitionOverride(
            envelopeTenant: "tenant-b",
            payloadActorId: "tenant-a:party-index",
            queryType: "PartyIndex"));

        proxyFactory.Received(1).CreateActorProxy<IPartyIndexProjectionActor>(
            Arg.Is<ActorId>(id => id.GetId() == "tenant-b:party-index"),
            nameof(PartyIndexProjectionActor),
            Arg.Any<ActorProxyOptions?>());
        proxyFactory.DidNotReceive().CreateActorProxy<IPartyIndexProjectionActor>(
            Arg.Is<ActorId>(id => id.GetId().StartsWith("tenant-a:", StringComparison.Ordinal)),
            Arg.Any<string>(),
            Arg.Any<ActorProxyOptions?>());
    }

    // AC4 — Cross-tenant list overlap: tenant A and tenant B both have entries with the same
    // display names/dates. Tenant B's list must compute TotalCount/TotalPages strictly from
    // tenant-B-authorized entries. Reference: 2.6-INT-083 (Tier-3 integration variant).
    [Fact(Skip = "ATDD red-phase scaffold for Story 2.6 / R-09 — activate in dev-story (Tier-2 surrogate)")]
    public void ListQuery_OverlappingDisplayNamesAcrossTenants_TotalCountIsTenantScoped()
    {
        // Red-phase shape: arrange a fake IPartyIndexProjectionActor returning a curated entries
        // list for tenant-b, with a tenant-a equivalent in static state. Assert TotalCount equals
        // the tenant-b-only count.
        Assert.Skip("Materialize once PartyIndexProjectionQueryActor exposes a deterministic harness for tenant-scoped TotalCount calculation.");
    }

    // AC7 — Diagnostics must not include raw actor/state/partition keys, payload JSON, stack traces,
    // or tenant membership data on any of: success / not-found / corrupt / degraded / cancellation.
    // Reference: 2.6-GTW-010, 2.6-GTW-011 — extends existing log-scrub coverage in
    // PartyDetailProjectionQueryActorTests with adapter actor focus.
    [Fact(Skip = "ATDD red-phase scaffold for Story 2.6 / R-02 — activate in dev-story")]
    public void TenantSafeReadDiagnostics_NeverIncludeActorOrStateKeyText()
    {
        // Red-phase shape: capture log output through a RecordingLogger across every public read
        // path on PartyDetailProjectionQueryActor + PartyIndexProjectionQueryActor and assert no
        // log message contains the substrings "tenant-a:", "party-detail:", "party-index:",
        // ":last-sequence", "stateKey", "streamName", "Bearer ".
        Assert.Skip("Materialize once both adapter actors are pinned through a shared RecordingLogger harness.");
    }

    private static PartyDetailProjectionQueryActor CreateDetailActor(string actorId, IActorProxyFactory proxyFactory)
    {
        ActorTimerManager timerManager = Substitute.For<ActorTimerManager>();
        var host = ActorHost.CreateForTest<PartyDetailProjectionQueryActor>(
            new ActorTestOptions { ActorId = new ActorId(actorId), TimerManager = timerManager });
        return new PartyDetailProjectionQueryActor(host, proxyFactory, NullLogger<PartyDetailProjectionQueryActor>.Instance);
    }

    private static PartyIndexProjectionQueryActor CreateIndexActor(string actorId, IActorProxyFactory proxyFactory)
    {
        ActorTimerManager timerManager = Substitute.For<ActorTimerManager>();
        var host = ActorHost.CreateForTest<PartyIndexProjectionQueryActor>(
            new ActorTestOptions { ActorId = new ActorId(actorId), TimerManager = timerManager });
        return new PartyIndexProjectionQueryActor(host, proxyFactory, NullLogger<PartyIndexProjectionQueryActor>.Instance);
    }

    private static QueryEnvelope CreateEnvelope(string tenantId, string entityId, string queryType)
    {
        // Red-phase shape: build a QueryEnvelope mirroring the production payload contract used by
        // PartyDetailProjectionQueryActorTests. The exact builder call is intentionally left to the
        // dev-story so the scaffold compiles only after the activated test pulls in the existing
        // helper that builds Domain=party / AggregateId=parties / EntityId envelopes.
        throw new NotImplementedException("Reuse the QueryEnvelope helper from PartyDetailProjectionQueryActorTests when activating this scaffold.");
    }

    private static QueryEnvelope CreateEnvelopeWithPayloadTenantOverride(
        string envelopeTenant,
        string payloadTenantOverride,
        string entityId,
        string queryType)
    {
        // Red-phase shape: build an envelope authenticated as envelopeTenant, but with a payload
        // field carrying payloadTenantOverride. Forces the adapter to choose between authenticated
        // and payload tenant — must always pick the authenticated tenant.
        throw new NotImplementedException("Build payload-override envelope via the production QueryEnvelope contract when activating.");
    }

    private static QueryEnvelope CreateEnvelopeWithPayloadPartitionOverride(
        string envelopeTenant,
        string payloadActorId,
        string queryType)
    {
        // Red-phase shape: build an envelope authenticated as envelopeTenant with a payload field
        // carrying an alternate actor id / partition key. Must never reach the proxy factory.
        throw new NotImplementedException("Build payload-partition-override envelope via the production QueryEnvelope contract when activating.");
    }
}
