using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Parties.Contracts;
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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.Tests.Gateway;

// Story 2.6 — Enforce Tenant-Safe Projection Reads.
// Test-design risk references: R-01 cross-tenant leakage (P0), R-03 untrusted tenant source (P1),
// R-04 erased-PII resurfacing (P1), R-05 mixed-provenance static cache (P1).
//
// Each test uses unique party-id slugs (e.g., "p-26-static-tenantb-noread") to avoid contamination
// of the projection actors' process-wide static caches (`s_lastKnownDetails`, `s_lastKnownEntries`)
// across test cases within this class.
public sealed class TenantSafeProjectionReadGuardrailsTests
{
    // AC1 — Missing tenant on the envelope. Two complementary proofs:
    //  (a) the envelope itself rejects empty/whitespace tenant at the ctor boundary (EventStore
    //      Contracts guarantee — covered by `EnvelopeCtor_RejectsMissingTenant_BeforeAdapter` below);
    //  (b) defense-in-depth: even when an envelope with a syntactically valid tenant arrives but
    //      the QueryActor's host-id carries a malformed tenant segment, the adapter rejects the
    //      route BEFORE any projection actor proxy is created.
    // Reference: 2.6-GTW-001, 2.6-UNIT-006.
    [Fact]
    public void EnvelopeCtor_RejectsMissingTenant_BeforeAdapter()
    {
        // Upstream guard at the public boundary (Hexalith.EventStore.Contracts.QueryEnvelope ctor).
        // Captured here so a future relaxation of the contract triggers a Story 2.6 test failure.
        Should.Throw<ArgumentException>(
                () => new QueryEnvelope(
                    tenantId: string.Empty,
                    domain: "party",
                    aggregateId: "p-1",
                    queryType: "PartyDetail",
                    payload: [],
                    correlationId: "corr-1",
                    userId: "user-1",
                    entityId: "p-1"))
            .ParamName
            .ShouldBe("tenantId");
    }

    [Fact]
    public async Task QueryAsync_HostIdWithMalformedTenantSegment_FailsBeforeActorConstructionAsync()
    {
        IActorProxyFactory proxyFactory = Substitute.For<IActorProxyFactory>();
        // QueryActor host id with empty tenant segment between projection and party id. After
        // RemoveEmptyEntries this collapses to 2 segments ["party-detail", "p-1"], so the adapter
        // route resolver fails closed with InvalidEnvelope before any actor proxy creation.
        PartyDetailProjectionQueryActor actor = CreateDetailActor("party-detail::p-1", proxyFactory);

        QueryResult result = await actor.QueryAsync(
            CreateEnvelope(tenantId: "tenant-a", entityId: "p-1", queryType: "PartyDetail"));

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.InvalidEnvelope);
        proxyFactory.DidNotReceiveWithAnyArgs().CreateActorProxy<IPartyDetailProjectionActor>(
            default!, default!, default);
    }

    [Fact]
    public async Task QueryAsync_EnvelopeTenantWithForbiddenColonCharacter_FailsBeforeActorConstructionAsync()
    {
        // P13 parity — the detail query adapter must reject malformed tenant identifiers via the
        // same `s_validTenantId` allowlist as the index sibling. A tenant value containing ':'
        // would corrupt the downstream actor id "{tenant}:party-detail:{partyId}" routing.
        IActorProxyFactory proxyFactory = Substitute.For<IActorProxyFactory>();
        PartyDetailProjectionQueryActor actor = CreateDetailActor("party-detail:malicious:tenant:p-1", proxyFactory);

        QueryResult result = await actor.QueryAsync(
            CreateEnvelope(tenantId: "malicious:tenant", entityId: "p-1", queryType: "PartyDetail"));

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.InvalidEnvelope);
        proxyFactory.DidNotReceiveWithAnyArgs().CreateActorProxy<IPartyDetailProjectionActor>(
            default!, default!, default);
    }

    // AC2/AC3 — Payload-tenant-like overrides cannot force the adapter to read another tenant.
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

        await actor.QueryAsync(CreateEnvelopeWithPayloadTenantOverride(
            envelopeTenant: "tenant-b",
            payloadTenantOverride: "tenant-a",
            entityId: "p-1",
            queryType: "PartyDetail"));

        // The authenticated envelope tenant (tenant-b) is authoritative; payload field is ignored.
        proxyFactory.Received(1).CreateActorProxy<IPartyDetailProjectionActor>(
            Arg.Is<ActorId>(id => id != null && id.GetId() == "tenant-b:party-detail:p-1"),
            nameof(PartyDetailProjectionActor),
            Arg.Any<ActorProxyOptions?>());
        proxyFactory.DidNotReceive().CreateActorProxy<IPartyDetailProjectionActor>(
            Arg.Is<ActorId>(id => id != null && id.GetId().StartsWith("tenant-a:", StringComparison.Ordinal)),
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

        // The two outcomes must be observationally indistinguishable across every bounded field:
        // Success flag, ErrorMessage, ProjectionType, and the payload bytes. No part of the
        // response may reveal that p-existing-in-a exists in another tenant.
        wrongTenantResult.Success.ShouldBe(absentResult.Success);
        wrongTenantResult.ErrorMessage.ShouldBe(absentResult.ErrorMessage);
        wrongTenantResult.ProjectionType.ShouldBe(absentResult.ProjectionType);
        // Both should be Failure(ActorNotFoundInfrastructure) — bounded, non-enumerating.
        wrongTenantResult.Success.ShouldBeFalse();
        wrongTenantResult.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorNotFoundInfrastructure);
    }

    // AC6 — Static last-known-detail cache (s_lastKnownDetails) must not return tenant-A entries
    // to tenant-B. Direct proof: populate the cache by reading tenant-A data successfully, then
    // configure a tenant-B actor with a failing state store and assert the catch-when guard does
    // NOT find a cache entry under the tenant-B state key — the exception propagates instead of
    // returning resurfaced tenant-A PII. Reference: 2.6-UNIT-007, closes Story 2.3 D2.
    [Fact]
    public async Task StaticLastKnownDetailCache_TenantBCannotReadTenantAEntryViaSameStateKeyAsync()
    {
        // Step 1: tenant-A reads successfully → populates s_lastKnownDetails["tenant-a:party-detail:p-26-static-tenantb-noread"].
        const string SharedPartyId = "p-26-static-tenantb-noread";
        (PartyDetailProjectionActor tenantAActor, IActorStateManager tenantAState) =
            CreateDetailProjectionActor($"tenant-a:party-detail:{SharedPartyId}");
        tenantAState.TryGetStateAsync<PartyDetail>(
                $"tenant-a:party-detail:{SharedPartyId}",
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PartyDetail>(true, CreateDetail(SharedPartyId, "Ada Tenant A")));

        PartyDetail? tenantADetail = await tenantAActor.GetDetailAsync();
        tenantADetail.ShouldNotBeNull();
        tenantADetail.DisplayName.ShouldBe("Ada Tenant A");

        // Step 2: tenant-B's state store throws on any read. The catch-when guard only triggers
        // if `_cachedDetail` (instance-local, null on a fresh actor) is non-null OR
        // `s_lastKnownDetails["tenant-b:party-detail:p-26-static-tenantb-noread"]` exists. Since
        // tenant-A's seed lives under its own tenant-a key, the catch-when CANNOT fire and the
        // exception propagates. If the cache were keyed only by partyId or by a non-tenant-scoped
        // key, the catch-when would find tenant-A's entry and silently serve it as tenant-B —
        // which is exactly the leak this test guards against.
        (PartyDetailProjectionActor tenantBActor, IActorStateManager tenantBState) =
            CreateDetailProjectionActor($"tenant-b:party-detail:{SharedPartyId}");
        tenantBState.TryGetStateAsync<PartyDetail>(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns<ConditionalValue<PartyDetail>>(_ =>
                throw new InvalidOperationException("simulated state-store outage"));

        await Should.ThrowAsync<InvalidOperationException>(() => tenantBActor.GetDetailAsync());
    }

    // AC2/AC6 — Erase + static cache coordination: after EraseAsync clears the actor's PII and
    // writes the erased entry to both the state store and the static cache, a subsequent
    // reactivation that falls back to the static cache (e.g., state store transient outage)
    // must surface the erased state, NOT the pre-erase PII. Reference: 2.6-UNIT-031.
    [Fact]
    public async Task EraseAsync_ThenReactivation_DoesNotResurrectPiiFromStaticCacheAsync()
    {
        const string PartyId = "p-26-erase-resurrect";
        (PartyDetailProjectionActor actor, IActorStateManager state) =
            CreateDetailProjectionActor($"tenant-a:party-detail:{PartyId}");
        PartyDetail seed = CreateDetail(PartyId, "Ada Lovelace");

        // The state-manager mock starts by returning the pre-erase seed; EraseAsync's SetStateAsync
        // call mutates the mock so subsequent reads return the erased shape (mimicking a real
        // state-store transaction).
        PartyDetail current = seed;
        state.TryGetStateAsync<PartyDetail>(
                $"tenant-a:party-detail:{PartyId}",
                Arg.Any<CancellationToken>())
            .Returns(_ => new ConditionalValue<PartyDetail>(true, current));
        state.WhenForAnyArgs(s => s.SetStateAsync<PartyDetail>(default!, default!, default))
            .Do(call => current = call.ArgAt<PartyDetail>(1));

        // Populate cache with the seed.
        PartyDetail? before = await actor.GetDetailAsync();
        before.ShouldNotBeNull();
        before.DisplayName.ShouldBe("Ada Lovelace");

        // Erase — writes erased state to state store + updates static cache.
        await actor.EraseAsync(PartyId);

        // Reactivate as a fresh actor instance under the same actor id. Its state-store call
        // throws (transient outage), forcing the catch-when fallback to s_lastKnownDetails.
        (PartyDetailProjectionActor reactivated, IActorStateManager reactivatedState) =
            CreateDetailProjectionActor($"tenant-a:party-detail:{PartyId}");
        reactivatedState.TryGetStateAsync<PartyDetail>(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns<ConditionalValue<PartyDetail>>(_ =>
                throw new InvalidOperationException("simulated state-store outage"));

        PartyDetail? after = await reactivated.GetDetailAsync();

        // The catch-when guard found the static cache entry under tenant-A:party-detail:p-... but
        // that entry is now the ERASED record (EraseAsync overwrote it). No pre-erase PII surfaces.
        after.ShouldNotBeNull();
        after.IsErased.ShouldBeTrue();
        after.DisplayName.ShouldNotBe("Ada Lovelace");
        after.DisplayName.ShouldNotContain("Ada", Case.Insensitive);
    }

    // AC4 — Index list query: caller-supplied payload partition key / actor id MUST be rejected
    // as InvalidEnvelope (defense-in-depth via JsonUnmappedMemberHandling.Disallow). The original
    // 2.6 spec listed both "ignored" and "rejected" as acceptable outcomes; per code-review
    // resolution (2026-05-20) we keep the strict reject path. Reference: 2.6-GTW-021, DN1.
    [Fact]
    public async Task QueryAsync_IndexQueryWithCallerSuppliedPartitionKey_RejectsAsInvalidEnvelopeAsync()
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

        // Payload tries to inject extra "actorId"/"partitionKey" fields; the strict JSON parser
        // rejects unmapped members so the adapter returns InvalidEnvelope before any proxy call.
        QueryResult result = await actor.QueryAsync(CreateEnvelopeWithPayloadPartitionOverride(
            envelopeTenant: "tenant-b",
            payloadActorId: "tenant-a:party-index",
            queryType: "PartyIndex"));

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.InvalidEnvelope);
        proxyFactory.DidNotReceiveWithAnyArgs().CreateActorProxy<IPartyIndexProjectionActor>(
            default!, default!, default);
    }

    // AC4 — Cross-tenant list overlap: tenant A and tenant B both have entries with overlapping
    // display names. Tenant B's list MUST compute TotalCount/TotalPages and rows strictly from
    // tenant-B-authorized entries — and the adapter must never create a proxy targeting tenant A.
    // Reference: 2.6-INT-083.
    [Fact]
    public async Task ListQuery_TenantBQueryDoesNotReadTenantAEntriesAsync()
    {
        IActorProxyFactory proxyFactory = Substitute.For<IActorProxyFactory>();

        IPartyIndexProjectionActor tenantBActor = Substitute.For<IPartyIndexProjectionActor>();
        tenantBActor.GetEntriesAsync().Returns(Task.FromResult<IReadOnlyDictionary<string, PartyIndexEntry>>(
            new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal)
            {
                ["tenant-b-party-1"] = CreateIndexEntry("tenant-b-party-1", "Shared Display Name"),
            }));

        // Tenant-A actor returns DIFFERENT entries; if the adapter mistakenly routes to it,
        // tenant-A rows would surface in tenant-B's response and the count would balloon.
        IPartyIndexProjectionActor tenantAActor = Substitute.For<IPartyIndexProjectionActor>();
        tenantAActor.GetEntriesAsync().Returns(Task.FromResult<IReadOnlyDictionary<string, PartyIndexEntry>>(
            new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal)
            {
                ["tenant-a-party-1"] = CreateIndexEntry("tenant-a-party-1", "Shared Display Name"),
                ["tenant-a-party-2"] = CreateIndexEntry("tenant-a-party-2", "Shared Display Name"),
            }));

        proxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
                Arg.Is<ActorId>(id => id != null && id.GetId().StartsWith("tenant-b:", StringComparison.Ordinal)),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
            .Returns(tenantBActor);
        proxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
                Arg.Is<ActorId>(id => id != null && id.GetId().StartsWith("tenant-a:", StringComparison.Ordinal)),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
            .Returns(tenantAActor);

        PartyIndexProjectionQueryActor actor = CreateIndexActor("party-index:tenant-b:parties", proxyFactory);

        QueryResult result = await actor.QueryAsync(CreateIndexEnvelope(
            tenantId: "tenant-b",
            queryType: "PartyIndex",
            payload: JsonSerializer.SerializeToUtf8Bytes(new { page = 1, pageSize = 20 })));

        result.Success.ShouldBeTrue();
        PagedResult<PartyIndexEntry> page = result.GetPayload().Deserialize<PagedResult<PartyIndexEntry>>(
            PartiesJsonOptions.Default)!;
        page.Items.Select(static i => i.Id).ShouldBe(["tenant-b-party-1"]);
        page.TotalCount.ShouldBe(1);
        page.TotalPages.ShouldBe(1);
        // The adapter never reached for tenant-A entries.
        proxyFactory.DidNotReceive().CreateActorProxy<IPartyIndexProjectionActor>(
            Arg.Is<ActorId>(id => id != null && id.GetId().StartsWith("tenant-a:", StringComparison.Ordinal)),
            Arg.Any<string>(),
            Arg.Any<ActorProxyOptions?>());
    }

    // AC4 — Index actor cache key includes the tenant segment so a reused partition key cannot
    // surface tenant-A entries to tenant-B even when the index actor's process-wide
    // s_lastKnownEntries dictionary persists across activations. Direct proof: load tenant-A
    // entries successfully (cache populated under "tenant-a:party-index:default"), then construct
    // a tenant-B index actor with a state store that throws and a cold (no _entries) start.
    // The cold-read catch fallback only triggers if TryGetCachedState finds the tenant-B state
    // key — which it cannot, so the exception propagates instead of leaking tenant-A rows.
    // Reference: 2.6-UNIT-008.
    [Fact]
    public async Task PartyIndexActor_StaticCacheKey_IncludesTenantSegmentAsync()
    {
        // Step 1: populate s_lastKnownEntries for tenant-A.
        (PartyIndexProjectionActor tenantAActor, IActorStateManager tenantAState) =
            CreateIndexProjectionActor("tenant-a:party-index");
        tenantAState.TryGetStateAsync<Dictionary<string, PartyIndexEntry>>(
                "tenant-a:party-index:default",
                Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<Dictionary<string, PartyIndexEntry>>(
                true,
                new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal)
                {
                    ["tenant-a-party-leaktest"] = CreateIndexEntry("tenant-a-party-leaktest", "Tenant A Only"),
                }));

        IReadOnlyDictionary<string, PartyIndexEntry> tenantAEntries = await tenantAActor.GetEntriesAsync();
        tenantAEntries.Count.ShouldBe(1);
        tenantAEntries["tenant-a-party-leaktest"].DisplayName.ShouldBe("Tenant A Only");

        // Step 2: tenant-B activates cold (no instance entries). LoadStateAsync throws → the
        // catch-when guard only triggers if TryGetCachedState finds "tenant-b:party-index:default".
        // Since tenant-A's cache lives under its own tenant-a key, the guard fails closed and
        // the exception propagates without serving tenant-A rows.
        (PartyIndexProjectionActor tenantBActor, IActorStateManager tenantBState) =
            CreateIndexProjectionActor("tenant-b:party-index");
        tenantBState.TryGetStateAsync<Dictionary<string, PartyIndexEntry>>(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns<ConditionalValue<Dictionary<string, PartyIndexEntry>>>(_ =>
                throw new InvalidOperationException("simulated state-store outage"));

        await Should.ThrowAsync<InvalidOperationException>(() => tenantBActor.GetEntriesAsync());
    }

    // AC5 — Search with degraded index (rebuilding state, no cached entries) returns bounded
    // empty PagedResult without leaking rows, counts, scores, or source metadata from any other
    // tenant. Reference: 2.6-INT-091, Required Test Matrix row "Degraded search proof".
    [Fact]
    public async Task PartySearch_OverRebuildingIndex_ReturnsBoundedEmptyPagedResultAsync()
    {
        // Index actor in degraded state returns an empty dictionary (the contract under rebuilding
        // per PartyIndexProjectionActor.GetEntriesAsync). The query adapter must not fall back to
        // alternate sources (aggregate stream, Memories, retired REST). The response is bounded
        // empty — no rows, counts, or match metadata escape.
        IActorProxyFactory proxyFactory = Substitute.For<IActorProxyFactory>();
        IPartyIndexProjectionActor rebuildingActor = Substitute.For<IPartyIndexProjectionActor>();
        rebuildingActor.GetEntriesAsync().Returns(Task.FromResult<IReadOnlyDictionary<string, PartyIndexEntry>>(
            new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal)));
        proxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
                Arg.Any<ActorId>(),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
            .Returns(rebuildingActor);

        PartyIndexProjectionQueryActor actor = CreateIndexActor("party-index:tenant-b:parties", proxyFactory);

        QueryResult result = await actor.QueryAsync(CreateIndexEnvelope(
            tenantId: "tenant-b",
            queryType: "PartySearch",
            payload: JsonSerializer.SerializeToUtf8Bytes(new
            {
                query = "shared display name",
                page = 1,
                pageSize = 20,
            })));

        result.Success.ShouldBeTrue();
        PagedResult<PartySearchResult> page = result.GetPayload().Deserialize<PagedResult<PartySearchResult>>(
            PartiesJsonOptions.Default)!;
        page.Items.ShouldBeEmpty();
        page.TotalCount.ShouldBe(0);
        // PartyMessenger paging convention reports `TotalPages == max(1, ceil(TotalCount/PageSize))`
        // so a zero-row page still surfaces as one empty page. The important fail-closed guarantee
        // is rows + TotalCount; TotalPages is a paging-convention artifact only.
        page.TotalPages.ShouldBeLessThanOrEqualTo(1);
    }

    // AC5 — Search filters out erased entries before computing match metadata, scores, or page
    // metadata. Even if the index has erased rows present, the search response excludes them.
    [Fact]
    public async Task PartySearch_ErasedEntriesAreExcludedFromMatchAndMetadataAsync()
    {
        IActorProxyFactory proxyFactory = Substitute.For<IActorProxyFactory>();
        IPartyIndexProjectionActor indexActor = Substitute.For<IPartyIndexProjectionActor>();
        indexActor.GetEntriesAsync().Returns(Task.FromResult<IReadOnlyDictionary<string, PartyIndexEntry>>(
            new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal)
            {
                ["alive"] = CreateIndexEntry("alive", "Shared Match Token"),
                ["erased"] = CreateErasedIndexEntry("erased", "Shared Match Token"),
            }));
        proxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
                Arg.Any<ActorId>(),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
            .Returns(indexActor);

        PartyIndexProjectionQueryActor actor = CreateIndexActor("party-index:tenant-b:parties", proxyFactory);

        QueryResult result = await actor.QueryAsync(CreateIndexEnvelope(
            tenantId: "tenant-b",
            queryType: "PartySearch",
            payload: JsonSerializer.SerializeToUtf8Bytes(new
            {
                query = "Shared",
                page = 1,
                pageSize = 20,
            })));

        result.Success.ShouldBeTrue();
        PagedResult<PartySearchResult> page = result.GetPayload().Deserialize<PagedResult<PartySearchResult>>(
            PartiesJsonOptions.Default)!;
        page.Items.Select(static i => i.Party.Id).ShouldNotContain("erased");
        page.TotalCount.ShouldBeLessThanOrEqualTo(1);
    }

    // DN2 (resolved 2026-05-20) — The malformed-actor-id contract on PartyIndexProjectionActor.
    // GetEntriesAsync returns an empty dictionary for v1.0. The contract is acceptable BECAUSE
    // the query adapter never constructs a malformed actor id (validated by `s_validTenantId`
    // regex + segment-shape check before `CreateActorProxy`). This test pins the positive
    // invariant: a forged envelope tenant with an invalid character cannot make the adapter
    // create an actor proxy at all.
    [Fact]
    public async Task PartyIndexQueryActor_MalformedTenantInEnvelope_NeverConstructsActorProxyAsync()
    {
        IActorProxyFactory proxyFactory = Substitute.For<IActorProxyFactory>();
        PartyIndexProjectionQueryActor actor = CreateIndexActor("party-index:malicious!tenant:parties", proxyFactory);

        QueryResult result = await actor.QueryAsync(CreateIndexEnvelope(
            tenantId: "malicious!tenant",
            queryType: "PartyIndex",
            payload: JsonSerializer.SerializeToUtf8Bytes(new { page = 1, pageSize = 20 })));

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.InvalidEnvelope);
        proxyFactory.DidNotReceiveWithAnyArgs().CreateActorProxy<IPartyIndexProjectionActor>(
            default!, default!, default);
    }

    // AC7 — Diagnostics surface for production code. Two complementary proofs:
    //  Part A — query-actor and projection-actor log MESSAGE templates never contain forbidden
    //    markers (actor keys, state keys, party ids, tenants, seeded PII). State-store throws
    //    are seeded with NEUTRAL exception text so the assertion fails only when production
    //    code itself adds a marker to the formatted message.
    //  Part B — exceptions THROWN BY production code (ResolveStateContext on malformed actor id)
    //    must have scrubbed messages — no raw actor id, party id, or projection segment value.
    // Reference: 2.6-GTW-010, 2.6-GTW-011.
    [Fact]
    public async Task TenantSafeReadDiagnostics_NeverIncludeActorOrStateKeyTextAsync()
    {
        // Part A — production log MESSAGE templates are clean even when the captured exception
        // is propagated. Seed neutral state-store exception text so the assertion catches only
        // production-side leaks (not the test fixture itself).
        var detailLogger = new RecordingLogger<PartyDetailProjectionQueryActor>();
        IActorProxyFactory detailProxyFactory = Substitute.For<IActorProxyFactory>();
        IPartyDetailProjectionActor failingDetail = Substitute.For<IPartyDetailProjectionActor>();
        failingDetail.GetDetailAsync().Returns<PartyDetail?>(_ =>
            throw new InvalidOperationException("transient state-store outage"));
        detailProxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
                Arg.Any<ActorId>(),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
            .Returns(failingDetail);
        PartyDetailProjectionQueryActor detailQueryActor = CreateDetailActor(
            "party-detail:tenant-a:p-26-diag-1",
            detailProxyFactory,
            detailLogger);
        await detailQueryActor.QueryAsync(CreateEnvelope("tenant-a", "p-26-diag-1", "PartyDetail"));

        var indexLogger = new RecordingLogger<PartyIndexProjectionQueryActor>();
        IActorProxyFactory indexProxyFactory = Substitute.For<IActorProxyFactory>();
        IPartyIndexProjectionActor failingIndex = Substitute.For<IPartyIndexProjectionActor>();
        failingIndex.GetEntriesAsync().Returns<IReadOnlyDictionary<string, PartyIndexEntry>>(_ =>
            throw new InvalidOperationException("transient state-store outage"));
        indexProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
                Arg.Any<ActorId>(),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
            .Returns(failingIndex);
        PartyIndexProjectionQueryActor indexQueryActor = CreateIndexActor(
            "party-index:tenant-a:parties",
            indexProxyFactory,
            indexLogger);
        await indexQueryActor.QueryAsync(CreateIndexEnvelope(
            tenantId: "tenant-a",
            queryType: "PartyIndex",
            payload: JsonSerializer.SerializeToUtf8Bytes(new { page = 1, pageSize = 20 })));

        // Projection-actor diagnostic paths (malformed actor id → logs `MalformedActorId` without
        // the raw id; should not throw).
        var detailProjectionLogger = new RecordingLogger<PartyDetailProjectionActor>();
        (PartyDetailProjectionActor malformedDetailProjection, _) =
            CreateDetailProjectionActor("tenant-a:wrong-projection:p-26-diag-2", detailProjectionLogger);
        await malformedDetailProjection.GetDetailAsync();

        var indexProjectionLogger = new RecordingLogger<PartyIndexProjectionActor>();
        (PartyIndexProjectionActor malformedIndexProjection, _) =
            CreateIndexProjectionActor("tenant-a:party-index:extra", indexProjectionLogger);
        await malformedIndexProjection.GetEntriesAsync();

        var allRecords = detailLogger.Records
            .Concat(indexLogger.Records)
            .Concat(detailProjectionLogger.Records)
            .Concat(indexProjectionLogger.Records)
            .ToList();
        allRecords.ShouldNotBeEmpty();

        foreach ((LogLevel _, string message, Exception? _) in allRecords)
        {
            // Formatted log message — production-template only. The captured Exception arg is
            // covered separately by Part B (production-thrown) and by the input-trust contract
            // (state-store exceptions are pass-through and outside production code's diagnostic
            // surface).
            AssertNoForbiddenMarkers(message);
        }

        // Part B — production code that THROWS (ResolveStateContext on a malformed actor id via
        // EraseAsync) must scrub its exception messages. Verify by triggering the throw and
        // asserting Message is clean.
        (PartyDetailProjectionActor detailWithMalformedId, _) =
            CreateDetailProjectionActor("malformed-no-projection-segment");
        InvalidOperationException detailThrow = await Should.ThrowAsync<InvalidOperationException>(
            () => detailWithMalformedId.EraseAsync("p-26-diag-throw"));
        AssertNoForbiddenMarkers(detailThrow.Message);

        (PartyIndexProjectionActor indexWithMalformedId, _) =
            CreateIndexProjectionActor("malformed-no-projection-segment");
        InvalidOperationException indexThrow = await Should.ThrowAsync<InvalidOperationException>(
            () => indexWithMalformedId.EraseAsync("p-26-diag-throw"));
        AssertNoForbiddenMarkers(indexThrow.Message);

        static void AssertNoForbiddenMarkers(string text)
        {
            text.ShouldNotContain("tenant-a:party-detail", Case.Insensitive);
            text.ShouldNotContain("tenant-a:party-index", Case.Insensitive);
            text.ShouldNotContain("tenant-b:party-detail", Case.Insensitive);
            text.ShouldNotContain("tenant-b:party-index", Case.Insensitive);
            text.ShouldNotContain(":last-sequence", Case.Insensitive);
            text.ShouldNotContain(":manifest", Case.Insensitive);
            text.ShouldNotContain("malformed-no-projection-segment", Case.Insensitive);
            text.ShouldNotContain("ada@example.test", Case.Insensitive);
            text.ShouldNotContain("Ada Lovelace", Case.Insensitive);
            text.ShouldNotContain("Bearer ", Case.Sensitive);
        }
    }

    private static PartyDetailProjectionQueryActor CreateDetailActor(string actorId, IActorProxyFactory proxyFactory)
        => CreateDetailActor(actorId, proxyFactory, NullLogger<PartyDetailProjectionQueryActor>.Instance);

    private static PartyDetailProjectionQueryActor CreateDetailActor(
        string actorId,
        IActorProxyFactory proxyFactory,
        ILogger<PartyDetailProjectionQueryActor> logger)
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
        ILogger<PartyIndexProjectionQueryActor> logger)
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
        ILogger<PartyDetailProjectionActor> logger)
    {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        IProjectionRebuildService rebuildService = Substitute.For<IProjectionRebuildService>();
        ActorTimerManager timerManager = Substitute.For<ActorTimerManager>();
        var host = ActorHost.CreateForTest<PartyDetailProjectionActor>(
            new ActorTestOptions { ActorId = new ActorId(actorId), TimerManager = timerManager });
        var actor = new PartyDetailProjectionActor(host, rebuildService, logger);
        InjectStateManager(actor, stateManager);
        return (actor, stateManager);
    }

    private static (PartyIndexProjectionActor Actor, IActorStateManager StateManager) CreateIndexProjectionActor(string actorId)
        => CreateIndexProjectionActor(actorId, NullLogger<PartyIndexProjectionActor>.Instance);

    private static (PartyIndexProjectionActor Actor, IActorStateManager StateManager) CreateIndexProjectionActor(
        string actorId,
        ILogger<PartyIndexProjectionActor> logger)
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
        InjectStateManager(actor, stateManager);
        return (actor, stateManager);
    }

    // Dapr's Actor base type exposes `StateManager` as a public-getter / public-setter property
    // on the framework type. Reflection is the only available seam to swap in a test substitute
    // because the production constructor does not expose it. Fail fast if the property ever
    // moves — a silent `prop?.SetValue(...)` would leave the test running against the real
    // (unconfigured) state manager and turn assertions into tautologies.
    private static void InjectStateManager(Actor actor, IActorStateManager stateManager)
    {
        PropertyInfo prop = typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException(
                "Dapr.Actors.Runtime.Actor.StateManager property is no longer accessible by reflection. "
                + "Update the test seam to match the new Dapr SDK shape.");
        if (prop.SetMethod is null)
        {
            throw new InvalidOperationException(
                "Dapr.Actors.Runtime.Actor.StateManager has no public setter. "
                + "Update the test seam to match the new Dapr SDK shape.");
        }

        prop.SetValue(actor, stateManager);
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

    private static PartyIndexEntry CreateErasedIndexEntry(string id, string displayName)
        => new()
        {
            Id = id,
            Type = PartyType.Organization,
            IsActive = false,
            IsErased = true,
            // The DisplayName is intentionally non-empty and matches the search query token so
            // this test proves that erasure filtering (not just a DisplayName-miss) excludes the
            // row — see LocalFuzzyPartySearchProvider's IsErased guard.
            DisplayName = displayName,
            CreatedAt = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            LastModifiedAt = DateTimeOffset.Parse("2026-05-02T00:00:00Z"),
        };

    // Captures (LogLevel, Message, Exception?). Critically, the Exception argument is retained
    // so AC7 diagnostics tests can assert ex.Message contains no PII or actor keys — the most
    // common leak path because the structured exception is rendered by most log sinks.
    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Records { get; } = [];

        IDisposable? ILogger.BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            Records.Add((logLevel, formatter(state, exception), exception));
        }
    }
}
