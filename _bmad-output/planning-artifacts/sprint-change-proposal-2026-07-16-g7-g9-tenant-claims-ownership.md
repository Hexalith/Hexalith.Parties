---
title: Sprint Change Proposal — Confirm G7/G9 Tenant-Claims Ownership
date: 2026-07-16
author: Administrator
workflow: bmad-correct-course
mode: incremental
scope_classification: moderate
status: approved
approval: approved
approved_at: 2026-07-16T00:43:36+02:00
trigger: >
  Confirm the architecture owner for the reusable tenant-claims transformation,
  the public eventstore:tenant claim constant, and the two non-throwing identifier
  predicates before Hexalith.Parties.Authentication is deleted.
---

# Sprint Change Proposal — Confirm G7/G9 Tenant-Claims Ownership

## 1. Issue Summary

Epic 8 requires Parties to delete generic platform plumbing, but the G7/G9
tenant-claims prerequisite still has only a proposed owner. Story 8.4 therefore
retired the safe leaf projects while deliberately preserving
`Hexalith.Parties.Authentication`; Story 8.8 remains blocked from the related
client/AppHost/build cleanup.

The checked-out source and built API surfaces confirm the gap:

- EventStore already implements tenant-claim normalization in
  `EventStoreClaimsTransformation`, but that type is delivered through the heavy
  `Hexalith.EventStore.Gateway` assembly and its `eventstore:tenant` constant is
  internal.
- `Hexalith.EventStore.Contracts.Identity.AggregateIdentity` validates its
  aggregate-id component only through constructor-throwing private methods; it
  exposes no non-throwing `IsValid(string)` predicate.
- `Hexalith.Commons.UniqueIds.UniqueIdHelper` contains the private ULID regex used
  by throwing conversion methods; it exposes no `IsValidUlid(string)` predicate.
- Parties consequently still owns a separate ASP.NET authentication project and
  a public Parties-local copy of the EventStore tenant claim literal.

This is a cross-repository ownership and API-availability gap, not a new product
requirement. Deleting the Parties implementation before ownership, release
provenance, consumer parity, and rollback evidence would violate Epic 8
invariants I3 and I4.

## 2. Architecture-Owner Decision

### Decision: confirm EventStore and Commons ownership

The architecture owner confirms the proposed destination. There is no redirect.

| Surface | Confirmed owner | Required additive destination |
| --- | --- | --- |
| Public tenant claim constant | Hexalith.EventStore | `Hexalith.EventStore.Contracts`, exposed as a documented public constant such as `EventStoreClaimTypes.Tenant = "eventstore:tenant"` |
| Reusable tenant claims transformation | Hexalith.EventStore | New lightweight, packable `Hexalith.EventStore.Authentication` surface exposing `EventStoreTenantClaimsTransformation`; it references Contracts and ASP.NET Core, not Gateway or Server |
| Aggregate-id grammar predicate | Hexalith.EventStore | `AggregateIdentity.IsValid(string)` in `Hexalith.EventStore.Contracts`; non-throwing and semantically identical to the constructor's aggregate-id component validation |
| Strict ULID predicate | Hexalith.Commons | `UniqueIdHelper.IsValidUlid(string)` in `Hexalith.Commons.UniqueIds`; non-throwing and based on the same Crockford Base32/parser rules as existing ULID operations |

### Boundary rationale

- `eventstore:tenant` is an EventStore authorization contract, so neither the
  constant nor its normalization policy belongs to a domain module or to generic
  Commons.
- The ASP.NET `IClaimsTransformation` implementation cannot live in the
  infrastructure-free EventStore.Contracts assembly. A small authentication
  package avoids making Parties UI or domain-service hosts depend on the Gateway
  and its Server/DAPR graph.
- `AggregateIdentity.IsValid(string)` owns the permissive EventStore aggregate-id
  grammar. It must continue to accept valid ULID-shaped identifiers, existing
  GUID-shaped identifiers, and other valid semantic aggregate IDs used for replay
  compatibility.
- `UniqueIdHelper.IsValidUlid(string)` owns strict ULID recognition and must not be
  substituted for EventStore aggregate-id validation.

### Required behavior of the shared transformation

The EventStore-owned tenant transformation preserves the proven Parties behavior:

- recognizes `tenants` as a JSON string array or space-delimited string;
- recognizes `tenant_id`, then `tid` as the singular fallback;
- emits the public EventStore tenant claim constant;
- is idempotent and does not duplicate normalized claims;
- ignores null, empty, and whitespace source values;
- handles malformed JSON through the existing bounded fallback without throwing;
- logs only bounded counts/status, never raw claims, tenant values, tokens, or PII.

The existing Gateway `EventStoreClaimsTransformation` may retain its additional
domain, permission, and name-identifier behavior, but it must consume or compose
the shared tenant normalizer rather than maintain another tenant parser.

## 3. Impact Analysis

### Epic and story impact

- **Epic 8:** remains viable, Class C post-MVP maintenance, with no sequence or
  PRD functional-coverage change.
- **Story 8.4:** remains `done` for its completed safe slice. Its explicitly
  deferred Authentication deletion becomes a follow-up retirement slice and
  stays blocked until delivery and consumer gates below pass.
- **Story 8.8:** remains backlog/blocked by G7/G9 delivery as well as its other
  independent G6/G8/G11 prerequisites.
- **Story 8.10:** cannot close tenant-claims retirement while the matrix row lacks
  exact release or root-submodule pin, parity evidence, and rollback proof.

### Artifact impact

- **PRD:** no edit; all feature scope remains complete in Epics 1–5.
- **Epics:** no scope, story, or sequencing edit.
- **Architecture/UX:** no product architecture or interaction change. This
  proposal is the durable G7/G9 owner decision under Epic 8 invariant I4.
- **Story 8.3 matrix:** record the confirmed owner and exact destination, but keep
  the row `needs-additive-api` until the APIs are released or pinned and proven.
- **Sprint status:** close the architecture-decision action and create/retain a
  separate open delivery/adoption action so ownership approval is not mistaken
  for API availability.

### Technical impact

The owner implementation spans EventStore, Commons, and later Parties adoption:

1. EventStore adds the public contract constant, aggregate-id predicate, and
   lightweight authentication package; Gateway adopts the shared normalizer.
2. Commons adds the strict ULID predicate without changing existing generation or
   conversion behavior.
3. Both owners update package inventories, public API baselines where applicable,
   tests, and release evidence.
4. Parties consumes exact released packages or approved root-submodule pins,
   migrates both host registrations and claim-constant usages, proves parity and
   rollback, then deletes `Hexalith.Parties.Authentication` and its focused test
   project.
5. `PartiesClaimTypes.EventStoreTenant` remains a compatibility alias until an
   intentionally versioned public-contract removal is approved; deleting the
   authentication implementation does not authorize an accidental Contracts
   breaking change.

## 4. Recommended Approach

**Selected path: Direct Adjustment.** Confirm ownership now, then route one
EventStore owner package and one Commons additive predicate change before Parties
adoption.

- **Decision effort:** low.
- **Owner implementation effort:** medium.
- **Parties adoption/deletion effort:** medium.
- **Risk:** low while the current implementation remains; high if the Parties
  package is deleted before released/pinned API and parity evidence exist.
- **Timeline impact:** no MVP impact. Story 8.8 and the deferred 8.4 deletion
  remain dependent on platform-owner delivery; no external delivery date is
  invented.
- **Scope classification:** moderate because execution crosses two platform
  repositories plus Parties consumption and release coordination.

### Alternatives considered

- **Redirect transformation to Commons:** rejected. The normalized claim name and
  policy are EventStore-specific; Commons may own generic parsing helpers but not
  the EventStore authorization contract.
- **Reuse `Hexalith.EventStore.Gateway` directly:** rejected. It introduces an
  inappropriate Gateway/Server dependency into domain-service and UI consumers.
- **Place the transformer in EventStore.Contracts:** rejected. Contracts must not
  acquire ASP.NET authentication dependencies.
- **Potential rollback:** not applicable to the decision. The existing Parties
  package remains the rollback until adoption passes.
- **MVP review:** not applicable; Epic 8 carries zero product FRs.

## 5. Detailed Change Proposals

### 5.1 Story 8.3 prerequisite matrix

**Artifact:**
`_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md`

**OLD:**

> Proposed EventStore/Commons ownership; architecture-owner confirmation or
> redirect required. Row status: `needs-additive-api`.

**NEW:**

> Ownership confirmed on 2026-07-16 by this proposal. EventStore owns
> `EventStoreClaimTypes.Tenant`, the lightweight
> `EventStoreTenantClaimsTransformation` package surface, and
> `AggregateIdentity.IsValid(string)`. Commons owns
> `UniqueIdHelper.IsValidUlid(string)`. Row status remains
> `needs-additive-api` until named owner approval of the delivered APIs, exact
> release or root-submodule pins, producer/consumer parity, and rollback evidence
> are recorded.

**Rationale:** closes ownership ambiguity without falsely marking missing APIs
available.

### 5.2 Sprint routing action

**Artifact:** `_bmad-output/implementation-artifacts/sprint-status.yaml`

**OLD:** one `open` action combines architecture ownership confirmation with the
delivery gate.

**NEW:**

1. Mark the ownership-decision action `done` only after user approval of this
   proposal, citing the decision record.
2. Add or retain an `open` delivery/adoption action assigned to EventStore,
   Commons, Parties Developer, and Test Architect. It remains blocking for the
   deferred 8.4 deletion and the G7/G9 slice of 8.8.

**Rationale:** separates a completed architecture decision from uncompleted API
delivery and consumer proof.

### 5.3 Owner backlog and consuming stories

**EventStore owner deliverables:**

- `Hexalith.EventStore.Contracts`: public tenant claim constant and
  `AggregateIdentity.IsValid(string)` with XML documentation and tests.
- `Hexalith.EventStore.Authentication`: reusable tenant transformation package,
  DI registration helper if approved, package/release inventory, and migrated
  Gateway tenant-normalization tests.

**Commons owner deliverable:**

- `Hexalith.Commons.UniqueIds`: `UniqueIdHelper.IsValidUlid(string)` with null,
  whitespace, lowercase/uppercase, invalid Crockford character, length, and parse
  coverage.

**Parties consuming deliverables:**

- Replace transformation registrations in the Parties domain-service host and
  UI host.
- Replace local claim literal usage with the EventStore contract while preserving
  the Parties public compatibility alias.
- Move relevant normalization tests to the producer and keep Parties consumer
  composition/parity tests.
- Record exact package versions or root-submodule commits, then delete
  `Hexalith.Parties.Authentication` only after rollback is exercised.

## 6. Implementation Handoff

**Classification:** Moderate.

| Recipient | Responsibility |
| --- | --- |
| Winston / Architecture owner | Approve this ownership decision and reject redirects that place EventStore policy in a domain module or generic Commons |
| Hexalith.EventStore owners | Design, implement, review, package, and release/pin the public constant, aggregate-id predicate, and lightweight authentication surface; migrate Gateway to shared tenant normalization |
| Hexalith.Commons owners | Implement and release/pin the strict non-throwing ULID predicate |
| Amelia / Parties Developer | Preserve the local package until producer delivery is recorded; adopt both approved surfaces and delete only after parity/rollback |
| Murat / Test Architect | Require producer tests, Parties consumer tests, package/API compatibility, host DI composition, malformed/idempotent claim cases, identifier boundary cases, and exercised rollback |
| Product Owner | Keep the deferred 8.4 deletion and G7/G9 slice of 8.8 blocked until the matrix exit gate passes |

### Success criteria before Parties deletion

1. Named EventStore and Commons owners accept the exact API shapes.
2. The APIs are available through exact released package versions or approved
   root-submodule commits, and release inventories contain every new package.
3. The Story 8.3 row is updated before Parties migration begins.
4. Producer tests prove the tenant normalization and both predicates, including
   non-throwing invalid-input behavior.
5. Parties host and UI composition tests prove the shared transformation is
   registered with the intended lifetimes and behavior.
6. Existing GUID-shaped aggregate IDs, valid ULIDs, and valid semantic aggregate
   IDs remain accepted; strict ULID checks reject non-ULID semantic IDs.
7. Public Parties compatibility is preserved or deliberately versioned.
8. Rollback to `Hexalith.Parties.Authentication` is exercised before deletion.
9. Only then may the matrix row advance and the package/project be retired.

## 7. Change-Analysis Checklist

| Checklist item | Status | Finding |
| --- | --- | --- |
| 1.1 Trigger story | [x] Done | Story 8.4 preserved the Authentication project; the open G7/G9 action blocks deferred deletion and 8.8. |
| 1.2 Core problem | [x] Done | Platform ownership was proposed but not confirmed; public APIs are absent. |
| 1.3 Evidence | [x] Done | Source and built API inspection confirm the internal/heavy Gateway surface and missing predicates. |
| 2.1 Current epic viability | [x] Done | Epic 8 remains viable with gated owner delivery. |
| 2.2 Epic-level changes | [N/A] Skip | No epic scope change. |
| 2.3 Remaining epics/stories | [x] Done | Deferred 8.4 deletion, 8.8, and 8.10 assessed. |
| 2.4 New/obsolete epics | [N/A] Skip | None. |
| 2.5 Order/priority | [x] Done | Owner APIs precede Parties adoption/deletion; Epic sequence stays fixed. |
| 3.1 PRD | [N/A] Skip | No product requirement change. |
| 3.2 Architecture | [x] Done | Exact owner/package/API boundaries are decided; I3/I4 remain binding. |
| 3.3 UI/UX | [N/A] Skip | No UI or copy change. |
| 3.4 Other artifacts | [x] Done | Matrix, sprint routing, package inventories, API baselines, and tests are affected. |
| 4.1 Direct adjustment | [x] Viable | Selected; low decision effort, medium coordinated delivery. |
| 4.2 Potential rollback | [N/A] Skip | Current Parties implementation is retained as rollback. |
| 4.3 MVP review | [N/A] Skip | Epic 8 is post-MVP maintenance. |
| 4.4 Recommended path | [x] Done | Confirm EventStore/Commons ownership and separate decision closure from delivery. |
| 5.1 Issue summary | [x] Done | Trigger, evidence, and deletion risk recorded. |
| 5.2 Impact analysis | [x] Done | Epic, story, artifact, and technical impacts recorded. |
| 5.3 Recommended path | [x] Done | Direct adjustment and rejected redirects documented. |
| 5.4 MVP/action plan | [x] Done | No MVP effect; producer-to-consumer sequence defined. |
| 5.5 Handoff plan | [x] Done | Architecture, platform owners, Developer, Test Architect, and PO responsibilities defined. |
| 6.1 Checklist review | [x] Done | All applicable analysis items addressed. |
| 6.2 Proposal accuracy | [x] Done | Cross-checked against PRD, epics, architecture, Story 8.3, 8.4, 8.8, source, and built APIs. |
| 6.3 Explicit approval | [x] Done | Administrator approved the proposal on 2026-07-16 at 00:43:36+02:00. |
| 6.4 Sprint-status update | [x] Done | Decision completion and the still-open additive delivery gate are tracked separately. |
| 6.5 Next steps/handoff | [x] Done | Additive owner work is routed to the EventStore and Commons owners, Amelia, and Murat. |

## 8. Approval Record

Approved by Administrator on 2026-07-16 at 00:43:36+02:00.

The architecture-owner decision is final: EventStore owns the public tenant
claim constant, reusable lightweight tenant-claims transformation, and
`AggregateIdentity.IsValid(string)`; Commons owns
`UniqueIdHelper.IsValidUlid(string)`. No redirect was selected.

This approval closes only the ownership decision. Additive API delivery,
release-or-pin evidence, producer/consumer parity, and exercised rollback remain
open and are routed through Story 8.3 before Parties may delete
`Hexalith.Parties.Authentication` or proceed with the dependent 8.8 work.
