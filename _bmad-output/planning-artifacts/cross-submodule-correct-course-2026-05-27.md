---
title: Cross-Submodule & Contract-Blocked Deferred Work — Correct Course
date: 2026-05-27
author: bmad-correct-course (facilitated by Claude), acting as PM / requirements-traceability reviewer
scope: out-of-repo remediation plan
status: PROPOSAL — routes work to the owning submodules; no Parties code changes here
source_of_truth: _bmad-output/implementation-artifacts/deferred-work.md
related:
  - _bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md
  - _bmad-output/planning-artifacts/implementation-readiness-report-2026-05-26.md
buckets:
  cross_submodule: ~60 items (EventStore / Memories / Tenants / Commons)
  contract_blocked: ~15 items (EventStore-fronted Parties client/gateway)
---

# Cross-Submodule & Contract-Blocked Deferred Work — Correct Course

## 0. Why this document exists

`Hexalith.Parties` is feature-complete (all 9 epics / 76 stories + retros `done`). The remaining
items in `deferred-work.md` that are **not** safe to implement inside this repo fall into two
buckets:

| Bucket | ~Count | Why it is out of `Hexalith.Parties` scope |
|--------|-------:|-------------------------------------------|
| **Cross-submodule** | ~60 | The correct fix lives in `Hexalith.EventStore`, `Hexalith.Memories`, `Hexalith.Tenants`, or `Hexalith.Commons`. Project rule: *"Before changing submodule-owned behavior, check whether the correct fix belongs in the submodule rather than Parties."* |
| **Blocked on the EventStore-fronted contract** | ~15 | The real Parties client/gateway contract was never globally `Satisfied`; Story 7.7 + Epic 8 shipped against a temporary bridge under 3 scoped, lead-approved risk acceptances. These unblock only when the real contract lands. |

This document converts those two buckets into **per-submodule action items** so each owning repo can
schedule them, and so the Parties-side follow-ups are explicit. Every row cites the originating
code review / story for traceability back to `deferred-work.md`.

> **How to use it:** each submodule section below is a self-contained backlog for that repo. Land
> the change in the submodule first (its own PR, tests, and review), bump the submodule pointer in
> `Hexalith.Parties`, then action any **Parties-side follow-up** noted in the row.

---

## 1. Recommended sequencing

The submodule PRs are mostly independent. Suggested priority order by risk/leverage:

1. **EventStore — land the EventStore-fronted Parties client/gateway contract** (§5). Highest
   leverage: unblocks the entire ~15-item contract bucket and lets the 3 risk acceptances close.
2. **Tenants — enum default + claim-casing** (§4, `TEN-1/2/3`). Security-adjacent: today a malformed
   tenant event can fail *open* (`Active=0`, `TenantOwner=0`), and benign claim-casing fails *closed*.
3. **EventStore — typed actor-not-found + payload hardening** (§2, `ES-1/2/3/6`). Removes
   locale-fragile string sniffing and closes the gateway DoS / silent-drop surfaces.
4. **Memories — SDK capability asks** (§3, `MEM-4/5/6`). Unblocks durable, idempotent indexing and
   graph-mode reliability; several Parties carve-outs are waiting on these.
5. **EventStore / Memories — build & topology** (`ES-7`, `MEM-1/2`). Clean-clone build + real aspirate
   emission for the Memories carve-out.
6. **Doc / cosmetic clarifications** (`ES-8/9/10`, `MEM-3/7`, `TEN-4/5`, Commons §6). Low risk.

---

## 2. Hexalith.EventStore

Repo: `Hexalith.EventStore` · solution `Hexalith.EventStore.slnx`. Note the EventStore ID-validation
rule (CLAUDE.md R2-A7): identifiers are **ULIDs**, `Guid.TryParse` is forbidden on `messageId` /
`correlationId` / `aggregateId` / `causationId`.

| ID | Item & correct course | Location | Origin | Parties-side follow-up |
|----|-----------------------|----------|--------|------------------------|
| **ES-1** | `CommandsController.ParseOptionalResultPayload` parses unbounded `string? ResultPayload` with `JsonDocument.Parse` (no depth/length cap → gateway DoS) **and** silently swallows `JsonException`. **Course:** add `JsonReaderOptions.MaxDepth` + a max-length guard; emit `LogWarning(ex, "Malformed result payload for correlationId {CorrelationId}", correlationId)`. | `src/Hexalith.EventStore/Controllers/CommandsController.cs:128-131` | 1-9 (+ re-review) | None |
| **ES-2** | `PipelineState.ResultPayload` is briefly persisted into Dapr actor state between `EventsStored` and `EventsPublished` — enriched payloads (possibly carrying PII) land in the state store. **Course:** decide the privacy posture in an EventStore-owned story (scrub/redact the result payload from persisted pipeline state, or document the acceptance with a retention bound). | `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:422,468,503` | 1-9 | Revisit Parties PII-marking assumptions once posture is set |
| **ES-3** | `SubmitCommandHandler` silently drops the result payload when the status-store read fails transiently (fail-closed is correct, but invisible). **Course:** add a warning log (no payload content) so the drop is observable. | `src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs:81-93,167-174` | 1-9 | None |
| **ES-4** | No back-compat test for `DomainServiceWireResult` deserialization when the producer omits `ResultPayload` (STJ positional-record default handling is the implicit guarantee). **Course:** add an explicit fixture so a future serializer-config change can't break the wire silently. | `src/Hexalith.EventStore.Contracts/Results/DomainServiceWireResult.cs` | 1-9 | None |
| **ES-5** | `EventStoreAggregate.DiscoverHandleMethods` accepts any `Task<T> where T : DomainResult`, but `DispatchCommandAsync` only matches `Task<DomainResult>`; `Task<T>` invariance means an accepted async handler returning a derived result (e.g. `Task<CompositeCommandResult>`) falls through to the unexpected-type path at runtime. **Course:** either narrow discovery back to `Task<DomainResult>`, or await the reflected `Task` and cast to `DomainResult`; add sync + async derived-result coverage. | `src/Hexalith.EventStore.Client/Aggregates/EventStoreAggregate.cs:95,141` | 12-4 | Parties composite handlers may use derived async results once fixed |
| **ES-6** | `QueryRouter.IsProjectionActorNotFound` detects "actor not found" via locale-sensitive `Message.Contains(...)` over English Dapr strings — fragile across SDK/locale. The **same** pattern is copy-pasted into both Parties query actors. **Course:** introduce a typed Dapr exception / status-code check in EventStore **first**; then Parties migrates both copies. | `src/Hexalith.EventStore.Server/Queries/QueryRouter.cs:110-123` | 2-3 / 2-4 / 2-5 / 2-6 (reaffirmed ×4) | Switch `PartyDetailProjectionQueryActor` + `PartyIndexProjectionQueryActor` to the typed check |
| **ES-7** | `AddHexalithEventStore` sets `redisHost` via `WithMetadata`, bypassing `statestore.yaml`; the yaml is not the runtime source of truth, yet Parties deploy-validation asserts against the yaml → divergence. **Course:** make the yaml authoritative (or document the `WithMetadata` override as authoritative and align the validator contract). | `src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs:72-77` | 12-1 | Reconcile `deploy/validate-deployment.ps1` Redis-host assertions after the decision |
| **ES-8** | `DomainServiceResolver` sanitized key uses `_` as the separator (collides when `domain`/`version` contains `_`, e.g. `party_v2`/`v1`); the sanitized form is only the **fourth** fallback, so an operator deploying only the sanitized form alongside a legacy pipe-form ConfigStore gets the legacy entry; fallback ordering is undocumented. **Course:** pick a non-colliding separator/escaping and document the fallback ordering. | `src/Hexalith.EventStore.Server/DomainServices/DomainServiceResolver.cs` | 9-3 | Re-verify Parties wildcard `*\|party\|v1` registration after change |
| **ES-9** | `EventStore.Contracts` transitively drags `Dapr.Actors` / `Dapr.Client` / `Grpc.*` / `Google.Protobuf` into the **client** asset graph (architectural leak; accepted exception today, pinned by a Parties fitness test). **Course:** prune `EventStore.Contracts` so clients no longer reference Dapr/Grpc transitively. | `src/Hexalith.EventStore.Contracts` (package dependency graph) | 1-1 | Relax `ClientArchitecturalFitnessTests` leaked-set pin once the leak is gone |
| **ES-10** | Spike-doc clarifications: wildcard `*\|domain\|v1` case-sensitivity asymmetry (resolver lowercases version but not domain); reproduction commands labelled `powershell` use POSIX `rg`; "event persistence: unknown" omits state-store config dependencies. **Course:** doc-only fixes (low priority). | `docs/spikes/2026-05-09-eventstore-parties-actor-invocation.md` | 12-0 | None |

---

## 3. Hexalith.Memories

Repo: `Hexalith.Memories`. Several Parties search items are *consumption* concerns that can only be
resolved once the Memories SDK / service exposes the capability — those are listed here as upstream
asks with the Parties-side follow-up noted.

| ID | Item & correct course | Location | Origin | Parties-side follow-up |
|----|-----------------------|----------|--------|------------------------|
| **MEM-1** | The Parties AppHost cannot resolve `Projects.Hexalith_Memories_Server` at compile time without the submodule initialised; Memories submodule drift (`AddHexalithEventStore` redis param, missing `Projects.Hexalith_Memories_Server`) has repeatedly blocked the full `.slnx` Release build. **Course:** stabilise `Hexalith.Memories.Server` + its Aspire wiring so a clean clone (submodules initialised) builds; keep the public project/type names stable. | `src/Hexalith.Memories.Server` + Aspire extensions | 7-7 (known-unrelated), 9-3 | Add an AppHost compile assertion that `Projects.Hexalith_Memories_Server` resolves |
| **MEM-2** | Parties' `deploy/k8s/memories/kustomization.yaml` env literals are placeholder-shaped (no OTLP exporter endpoint, no Dapr port hints) pending the first successful `aspirate` emission against an initialised Memories submodule. **Course (Memories):** ensure `Memories.Server` emits real config via aspirate; **then (Parties):** replace the placeholders. | `deploy/k8s/memories/kustomization.yaml` (Parties, blocked on Memories) | 9-3 | Replace placeholders post-emission |
| **MEM-3** | `accesscontrol.memories.yaml` operation path `/process` is not asserted against the Memories controller surface (cross-submodule verification deferred to first end-to-end run). **Course:** Memories publishes/documents its invocable route surface so the ACL can be verified end-to-end. | Memories controller surface ↔ `deploy/dapr/accesscontrol.memories.yaml` | 9-3 | Add an end-to-end ACL assertion once routes are documented |
| **MEM-4** | Memories indexing uses a **deprecated** API (`HXL001` silenced at the call site) and has no idempotency key beyond `sourceUri`, so concurrent same-party indexing from two near-simultaneous projection events races on Memories storage. **Course:** expose a non-deprecated indexing path with an explicit idempotency token. | Memories SDK (`IngestAsync` surface) | 9-6 (chunk A / 3rd pass) | Drop the `HXL001` suppression in `PartyMemoryIndexingService`; pass the idempotency token |
| **MEM-5** | `ResolveGraphStartNodeIdAsync` uses a free-text URN syntactic-axis search to find the memory unit for a source URI; the canonical match may not be in the top-5 hits → graph mode silently degrades to local. **Course:** add a URI-keyed lookup endpoint to the Memories SDK. | Memories SDK (lookup endpoint) | 9-6 (chunk A) | Switch `MemoriesPartySearchService` to the keyed lookup |
| **MEM-6** | `MemoryUnitId` vs `workflowInstanceId` semantic mismatch (decision D1): the per-party mapping list dedups by `MemoryUnitId`, but if the stable-id property doesn't hold (Memories restart / contract change) the list grows with ghost ids and can exceed the Dapr state-store value-size limit. **Course:** Memories guarantees/clarifies stable `MemoryUnitId` semantics. | Memories contract (`MemoryUnitId` stability) | 9-6 (5th pass) | Then revisit cap / TTL / dedup-by-`SourceUri` in `PartyMemoryUnitMappingStore` |
| **MEM-7** | The `ProbingMemoriesClient` test fixture inherits the real `MemoriesClient`; if the SDK becomes `sealed` upstream every test breaks. **Course:** Memories SDK provides a mockable interface abstraction. | Memories SDK (mockable abstraction) | 9-6 (2nd pass) | Switch the Parties test fixture to the abstraction once available |

---

## 4. Hexalith.Tenants

Repo: `Hexalith.Tenants`. `TEN-1`/`TEN-2` are **fail-open** defaults and should be treated as
security-adjacent.

| ID | Item & correct course | Location | Origin | Parties-side follow-up |
|----|-----------------------|----------|--------|------------------------|
| **TEN-1** | `TenantRole.TenantOwner = 0` is the default enum value → a malformed `UserAddedToTenant` payload with a missing `Role` field silently grants `TenantOwner`. **Course:** make `0` a non-privileged sentinel (e.g. `Unknown = 0`) or mark `Role` explicit-required in serialization. | `src/Hexalith.Tenants.Contracts/Enums/TenantRole.cs` | 11-2 | Re-verify Parties authorization assumes a non-zero owner role |
| **TEN-2** | `TenantStatus.Active = 0` is the default enum value → a malformed event without `Status` defaults to `Active` (**fail-open**). **Course:** make `0` a non-active sentinel (e.g. `Unknown`/`Suspended = 0`) or mark `Status` explicit-required. | `src/Hexalith.Tenants.Contracts/Enums/TenantStatus.cs` | 11-2 | Re-verify Parties tenant-status gating after the default flips |
| **TEN-3** | `TenantLocalState.Members` uses `StringComparer.Ordinal`; JWT `sub` and `eventstore:tenant` claim casing varies by IdP, so benign casing differences fail closed with `UnknownTenant` / `MissingMember`. **Course:** align the userId/tenantId comparison convention (case-insensitive lookup or a documented claims-normalization contract) in the Tenants client. | `src/Hexalith.Tenants.Client/Projections/TenantLocalState.cs:26-27` | 11-2 / 11-3 | Parties claims-transformation can stop compensating once the convention is set |
| **TEN-4** | `InMemoryTenantProjection.ApplyEvents` silently drops unknown event types in `ProjectFromTenantsAsync`. **Course:** add a projection-conformance test in the Tenants test suite (Parties is the wrong home for it). | Tenants test suite | 11-4 | None |
| **TEN-5** | `Hexalith.Tenants.Testing` helper APIs return `EventStore.Contracts.Results.DomainResult`, coupling that type into Parties tests. **Course:** decide whether this remains the public Tenants testing surface (accept) or is narrowed; revisit with an architecture-fitness pass. | `Hexalith.Tenants.Testing` public surface | 11-4 | If narrowed, add a Parties fitness test forbidding `Hexalith.Tenants.Testing` under `src/` |

---

## 5. EventStore-fronted Parties client/gateway contract (the ~15 contract-blocked items)

This is the single largest unblock. It is **EventStore-owned** for the contract itself, then
**Parties-owned** for the closure.

### 5.1 The umbrella (tracking item, not a defect)

The full EventStore-fronted Parties client/gateway contract was **never globally `Satisfied`**.
Story 7.7 and all Epic 8 picker stories (8.1–8.6) shipped against a **temporary bridge**
(`IAdminPortalGdprClient` / `HttpAdminPortalGdprClient` / `IPartiesQueryClient` / `Hexalith.Parties.Picker`)
under three scoped, project-lead-approved risk acceptances (SCPs 2026-05-23, 2026-05-23-epic8-picker-gate,
2026-05-24-epic8-picker-gate-remaining). The Story 7.6 fail-closed gate correctly keeps two GDPR
operations — **erasure-certificate** and **retry-verification** — disabled with the exact bounded
blocker `Blocked on accepted EventStore-fronted Parties client/gateway contract`.

### 5.2 Correct course

1. **(EventStore submodule)** Land the **real** EventStore-fronted Parties client/gateway contract —
   the genuine query gateway and GDPR client surface that the temporary bridge stands in for.
2. **(Parties)** Execute the consolidated **"Closure Procedure"** in
   `dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md` (frontmatter `closure_status: OPEN`):
   - Enable (or keep bounded with a *real*, non-placeholder reason) the two disabled GDPR ops:
     **erasure-certificate** + **retry-verification** (`PartyGdprOperationsPanel.razor`,
     `HttpAdminPortalGdprClient.cs`, `AdminPortalGdprCapability` — today `ProvisionalBridge()`).
   - Reconcile / replace the GDPR bridge and the picker/query bridge (`IPartiesQueryClient`,
     `Hexalith.Parties.Picker/*`).
   - Flip the dependency record to **`Satisfied`**; remove the umbrella tracking entry from
     `deferred-work.md`.

### 5.3 Parties-side items that unblock when the contract lands

These stay deferred **until** §5.2 step 1 ships — implementing them against the bridge would lock in a
premature shape.

| Area | Item | Location |
|------|------|----------|
| AdminPortal metadata | Metadata fidelity loss (degraded-search headers + ETag) collapses to a single `StaleDataAge` bit; typed path returns `AdminPortalQueryMetadata.Empty`. Defer until 12.4/12.5 freeze the metadata contract, then wire ETag + degraded-banner UX. | `PartiesAdminPortalApiClient.cs:430-433` |
| AdminPortal caching | `IsNotModified=true` on List/Search returns empty `Items=[]` (wipes grid); Detail cold-cache `IsNotModified` throws `NotFound`. Needs a cross-layer caching contract with FrontComposer. | `PartiesAdminPortalApiClient.cs:155-160,430-433` |
| AdminPortal DI | `IPartiesQueryClient` tenant-scope token captured at construction may stale across tenant switches; half-configured options surface `ContractUnavailable` mid-request; two-arg ctor bypasses typed-client preference. | `PartiesAdminPortalApiClient.cs:22-53,408-418` |
| Picker | `HttpRequestException.StatusCode` 401/403/410 distinctions collapsed to `TransientFailure`; `CreateRequestCustomizer` can send a silent unauthenticated request on token rotation. Owned by the 12.5 typed-client contract guarantees. | `PartyPickerApiClient.cs:63,100-104` |
| Client contract | Command gateway request DTO duplicated locally (EventStore keeps it in the service assembly, not the contracts package) — move to contracts once exposed. Query paging + blank-search validation is an API behaviour policy, not a wrapper guess. | `HttpPartiesQueryClient.cs:53`, `12-5-parties-client-thin-wrapper.md:200` |
| Query wire | `ProjectionActorType`/`ProjectionType` wire-contract addition has no external-consumer compat test; `Type`/`Active` fields on `SearchPartiesQueryPayloadWire` are unreachable from the typed client. Add wire-version + synchronization tests when the SDK/consumer surface ships. | `HttpPartiesQueryClient.cs:16-36,93-114` |
| Server-side bounds | "server-side `Skip`/`Take`" enforcement (clamp is client-side today via `AdminPortalQueryBounds`) waits on the 12.4/12.5 gateway. | `PartiesAdminPortalApiClient.cs` |

---

## 6. Hexalith.Commons

No concrete deferred **code** items target `Hexalith.Commons`. The only references are incidental
submodule-pointer bumps bundled into unrelated Parties commits (e.g. `Hexalith.Commons 92d04f6→1379767`
inside the Story 1.9 commit range), flagged as scope-creep smell, already merged.

**Course:** none required. Acknowledge the bundling convention in the next retrospective so future
submodule-pointer bumps land in their own commit with a change-log entry. No `Hexalith.Commons` PR is
needed for deferred work.

---

## 7. Handoff

| Submodule | Items | Owner | Gate |
|-----------|-------|-------|------|
| `Hexalith.EventStore` | `ES-1..ES-10` + §5.2 contract | EventStore maintainers | §5.2 contract gates the ~15 Parties items in §5.3 |
| `Hexalith.Memories` | `MEM-1..MEM-7` | Memories maintainers | `MEM-4/5/6` gate the Parties search carve-outs |
| `Hexalith.Tenants` | `TEN-1..TEN-5` | Tenants maintainers | `TEN-1/2` are fail-open — treat as security-adjacent |
| `Hexalith.Commons` | — | — | No action |

**Process:** land each item in its owning submodule (its own PR + tests + review), bump the submodule
pointer in `Hexalith.Parties`, then action the **Parties-side follow-up** column and remove the
corresponding entry from `deferred-work.md`. The EventStore-fronted contract (§5) should be sequenced
first because it unblocks the most downstream work and lets the three scoped risk acceptances close.
