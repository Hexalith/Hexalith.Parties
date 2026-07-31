---
title: Sprint Change Proposal — Deliver G7/G9 Owner APIs Before Parties Migration
date: 2026-07-31
author: Administrator
workflow: bmad-correct-course
mode: batch
scope_classification: moderate
status: approved
approval: approved
approved_at: 2026-07-31T23:33:05+02:00
related_decision: sprint-change-proposal-2026-07-16-g7-g9-tenant-claims-ownership.md
trigger: >
  Deliver the approved EventStore and Commons G7/G9 APIs, then record named
  approvals, exact dependency identities, producer-consumer parity, and an
  exercised rollback in Story 8.3 before retiring Hexalith.Parties.Authentication.
---

# Sprint Change Proposal — Deliver G7/G9 Owner APIs Before Parties Migration

## 1. Issue Summary

The approved 2026-07-16 G7/G9 decision assigned the shared tenant-claims and
identifier APIs to EventStore and Commons. That decision resolved ownership, but
it did not deliver the APIs. Story 8.3 therefore still marks the tenant-claims
row `needs-additive-api`, Story 8.4 remains complete only for its safe retirement
slice, and Story 8.8 remains blocked from deleting
`Hexalith.Parties.Authentication`.

This proposal converts the approved ownership decision into an auditable delivery
and consumption gate. It does not authorize producer-repository edits, a release,
a root gitlink advance, Parties migration, or deletion. Those actions require
their normal owner review plus the evidence defined below.

### Trigger and current evidence

The 2026-07-31 revalidation found:

- The Parties root pins EventStore at
  `9b9c776791c149cab26c795a476d23d3d11f7796`; the checkout is clean, matches
  the root gitlink, and is `v3.87.0-8-g9b9c7767` by repository description.
- Parties package mode still selects EventStore `3.86.0` through the Builds
  package catalog.
- Neither the EventStore source pin nor the selected package contract supplies
  a public `EventStoreClaimTypes.Tenant`,
  `AggregateIdentity.IsValid(string)`, or a separately packable
  `Hexalith.EventStore.Authentication` project. The existing
  `EventStoreClaimsTransformation` remains in the heavy EventStore host project
  and its tenant constant is internal.
- The Parties root pins Commons at
  `f2b5f1b12b478dce902756876138a60cde4fde65`; the checkout is clean, matches
  the root gitlink, and is `v2.29.0-1-gf2b5f1b` by repository description.
- Parties package mode selects Commons `2.29.0`. Neither the source pin nor that
  selected package exposes `UniqueIdHelper.IsValidUlid(string)`.
- Parties still owns `PartiesClaimsTransformation`, the
  `PartiesClaimTypes.EventStoreTenant` literal, distinct domain-service and UI
  `IClaimsTransformation` registrations, and focused authentication tests.

The root pins above are exact baseline identities, not delivery identities. An
identity that lacks a required API cannot close the Story 8.3 row merely because
the checkout is current or clean.

### Problem classification

This is a technical API-availability and release-coordination gap discovered
during Epic 8 execution. It is not a new product requirement, strategic pivot,
or change to the completed MVP.

## 2. Impact Analysis

### Epic and story impact

- **Epic 8 remains viable.** Its post-MVP maintenance scope, acceptance intent,
  and authoritative sequence remain unchanged:
  `8.3 -> 8.4 -> 8.5 -> 8.6 -> 8.7 -> 8.8 -> 8.9 -> 8.10`.
- **Story 8.3 remains `done` as discovery/routing work.** Its prerequisite
  matrix is the live receipt ledger and must retain the G7/G9 row as
  `needs-additive-api` until the full delivery, parity, and rollback packet is
  recorded.
- **Story 8.4 remains `done`.** It is not reopened or reclassified. Its
  deliberately deferred Authentication deletion stays gated and is executed as
  an independently reviewed retirement slice only after the Story 8.3 receipt
  passes.
- **Story 8.8 remains `blocked`.** Closing G7/G9 would unblock only its
  authentication slice; G6, G8, G11, the authoritative story sequence, and all
  other independent prerequisites still govern the rest of Story 8.8.
- **Story 8.10 remains the final reconciliation gate.** It must verify the
  consumed release versions or root gitlinks and the retained rollback record.

No new epic or story number is required. No epic is obsolete, and no
resequencing is justified.

### Artifact impact

| Artifact | Impact |
| --- | --- |
| PRD | No change. Epics 1–5 retain all product functional coverage. |
| Epics | No scope, acceptance-criteria, or sequence change. Existing 8.3, 8.4, 8.8, and 8.10 language already establishes the gate. |
| Architecture | No redesign. Epic 8 invariants I3, I4, I11, and I15 remain authoritative. |
| UX | No visual, interaction, copy, accessibility, or user-flow change. |
| Story 8.3 matrix | Add the delivery receipt and keep the row closed to consumption until every required field is evidenced. |
| Sprint status | Keep the delivery action open; refresh its evidence comment after approval without implying delivery. |
| Owner repositories | Additive APIs, package inventory, public API baselines, focused producer tests, and release or source-pin evidence. |
| Parties | Later compatibility adoption, host/UI composition and parity tests, package/source validation, rollback proof, then isolated deletion. |

### Architecture boundaries preserved

- `eventstore:tenant` and its normalization policy remain EventStore-owned.
- ASP.NET authentication dependencies do not enter EventStore.Contracts.
- Parties does not depend on EventStore Gateway or Server merely to normalize a
  tenant claim.
- EventStore aggregate-ID validity remains permissive and distinct from strict
  ULID validity.
- No dependency is considered consumable from a source path, working tree, tag
  description, or unapproved commit alone.
- The local Parties implementation remains the rollback path until parity and
  switch-back are proven.

## 3. Recommended Approach

**Selected path: Direct Adjustment.** Deliver two bounded owner work packets,
consume them through one gated Parties adoption, prove switch-back, and retire
the local project in a separate reviewed change.

- **Effort:** medium across EventStore, Commons, Parties, and test/release owners.
- **Risk:** medium while the local implementation is retained; high if adoption
  and deletion are combined or if a pin is accepted without API/package proof.
- **Timeline:** no MVP impact and no invented delivery date. The G7/G9 slice of
  Story 8.8 remains blocked until evidence exists.
- **Scope:** moderate because execution crosses two owner repositories and one
  consuming repository but does not change product behavior.

### Alternatives not selected

- **Delete Parties Authentication now and use EventStore Gateway:** rejected;
  the dependency boundary is too heavy and the approved lightweight package is
  absent.
- **Move EventStore policy to Commons:** rejected; Commons owns strict generic
  ULID recognition, not an EventStore authorization claim or parser.
- **Treat the current pins as delivery:** rejected; the required public APIs are
  absent at those identities.
- **Combine activation and deletion:** rejected; it removes the only proven
  switch-back path before consumer parity exists.
- **Reopen Story 8.4 or add another epic:** rejected; the existing deferred slice
  and Story 8.8 gate are sufficient.

## 4. Detailed Change Proposals

### 4.1 Story 8.3 G7/G9 row

**Artifact:**
`_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md`

**OLD:**

> G7/G9 ownership is approved, delivery proof is required, the row is
> `needs-additive-api`, and the local Parties rollback path is retained.

**NEW:**

Retain that text and append a structured **G7/G9 Delivery Receipt**. The row
must remain `needs-additive-api` while any required receipt field is blank,
unverified, or points to an identity that lacks an API.

The receipt must contain:

| Receipt field | Required evidence |
| --- | --- |
| EventStore API approval | Actual approver name or accountable handle, role, UTC-offset timestamp, review/decision reference, and acceptance of the exact constant, predicate, transformer, dependency boundary, and behavior contract. A role label alone is insufficient. |
| EventStore release/pin approval | Actual release or root-gitlink approver name/handle, timestamp, selected delivery mode, and proof that the identity contains all three EventStore surfaces. |
| Commons API/release approval | Actual approver name/handle, role, timestamp, review reference, and acceptance of the strict non-throwing ULID predicate at the recorded identity. |
| Parties consumer approval | Actual Parties owner name/handle, timestamp, selected dependencies, compatibility disposition, and evidence reference. |
| Test approval | Actual test owner name/handle, timestamp, producer-consumer parity result, negative/boundary coverage, and rollback result. |
| Exact EventStore identity | Either exact released versions for `Hexalith.EventStore.Contracts` and `Hexalith.EventStore.Authentication`, including feed/source and package hashes when available, or the exact 40-character root-declared EventStore gitlink plus the parent commit that selects it. |
| Exact Commons identity | Either the exact released `Hexalith.Commons.UniqueIds` version, including feed/source and package hash when available, or the exact 40-character root-declared Commons gitlink plus the parent commit that selects it. |
| Package/API inventory | Public API baseline or equivalent inspection showing every required member, and pack/release inventory showing the lightweight Authentication package when package mode is selected. |
| Producer evidence | Exact commands, results, test counts, artifact paths, and commit/release identity for both owner repositories. |
| Consumer parity | Exact source-mode and package-mode commands/results for every supported consumption mode, host/UI composition evidence, claim truth table, identifier boundaries, and compatibility checks. |
| Rollback | Trigger conditions, retained-path identity, exact switch-back procedure, execution timestamp, command/results, forward-restore procedure, and retirement-commit revert procedure. |

The selected identity must be coherent. Do not combine a source API observed at
one commit with a package version built from another commit. If a root-gitlink
delivery supports only source mode, package-mode adoption and deletion remain
blocked unless source-only CI/release consumption is separately approved and
recorded.

The row may transition to `available` only after the entire receipt passes.
`available` means the G7/G9 dependency is consumable; it does not by itself mark
Story 8.8 done or close any other prerequisite row.

**Rationale:** the current prose names the evidence classes but does not prevent
partial receipts, role-only approvals, mixed identities, or an unexercised
rollback from being interpreted as closure.

### 4.2 EventStore owner work packet

**OLD:**

> EventStore ownership is approved, but the checked-out source contains only an
> internal claim constant, constructor-throwing private aggregate-ID validation,
> and a host-bound transformation.

**NEW:**

Deliver one coherent EventStore identity containing all of the following:

1. `Hexalith.EventStore.Contracts` exposes the documented public constant
   `EventStoreClaimTypes.Tenant = "eventstore:tenant"` without ASP.NET
   dependencies.
2. `AggregateIdentity.IsValid(string)` is public, deterministic, and
   non-throwing. It validates the aggregate-ID component only and remains
   semantically identical to the constructor guard: nonblank, at most 256
   characters, printable ASCII, alphanumeric first/last characters, and only
   alphanumeric, dot, underscore, or hyphen internally.
3. The AggregateIdentity constructor and predicate share one validation rule so
   they cannot drift while existing exception types and parameter behavior stay
   compatible.
4. A lightweight, packable `Hexalith.EventStore.Authentication` exposes
   `EventStoreTenantClaimsTransformation`, references Contracts plus the minimum
   ASP.NET abstractions, and does not depend on Gateway, Server, Dapr, storage,
   or an AppHost.
5. The shared transformation recognizes an existing canonical claim,
   `tenants` JSON arrays and space-delimited values, `tenant_id`, and `tid`;
   ignores blank values; produces no duplicate canonical values; is idempotent;
   and handles malformed JSON through the approved bounded non-throwing fallback.
6. Logs contain bounded status/count data only—no tokens, raw source claims,
   tenant values, subjects, or other PII.
7. Existing EventStore Gateway normalization composes or consumes the shared
   tenant normalizer while retaining separately owned domain, permission, and
   name-identifier behavior.
8. Project/solution inventory, package manifest, central version catalog,
   public API baselines, dependency fitness tests, XML documentation, pack
   validation, and release notes are updated together.

Minimum producer tests cover null input, no source, existing canonical claims,
each legacy source, JSON and space-delimited multiple values, duplicate and
overlapping values, blank values, malformed JSON, repeated transformation,
bounded safe logging, Gateway composition, valid one-character/semantic/ULID/GUID
aggregate IDs, and every invalid aggregate-ID boundary.

### 4.3 Commons owner work packet

**OLD:**

> `UniqueIdHelper` has private Crockford validation used by throwing operations
> but no public non-throwing predicate.

**NEW:**

Add `UniqueIdHelper.IsValidUlid(string)` to
`Hexalith.Commons.UniqueIds` with these requirements:

- returns `false` for null, empty, whitespace, wrong length, forbidden Crockford
  characters, non-ASCII input, and parser-invalid or out-of-range values;
- accepts the same lowercase/uppercase forms accepted by existing ULID parsing;
- performs the existing Crockford check and an actual non-throwing parse so a
  regex-shaped but invalid 128-bit value is not accepted;
- does not change generation, timestamp extraction, GUID conversion, or their
  existing exception contracts;
- updates XML documentation, public API baselines, focused tests, package
  inventory, pack validation, and release evidence at one exact identity.

Minimum producer tests cover valid upper/lowercase ULIDs, first-character range,
all excluded Crockford characters, length boundaries, null/blank input,
non-ASCII input, parse-invalid values, and unchanged conversion/generation
behavior.

### 4.4 Parties adoption, parity, and deletion gate

**OLD:**

> Parties retains its local Authentication project and tests; Story 8.8 names
> parity and rollback generally, but no executable G7/G9 adoption sequence is
> recorded in Story 8.3.

**NEW:**

Use this mandatory two-change adoption sequence:

1. **Adopt without deletion.** Select the approved EventStore and Commons
   identities, update source/package references and catalogs coherently, replace
   host and UI registrations through a reversible selector or compatibility
   adapter, and keep `Hexalith.Parties.Authentication` buildable and testable.
2. **Prove producer-consumer parity.** Record the exact shared-versus-local
   truth table and commands/results in Story 8.3 and the test summary.
3. **Exercise switch-back.** Run both hosts on the shared path, switch both back
   to the retained local path, rerun the focused authentication/authorization
   checks, then restore the shared path and rerun them. Do not count a code review
   or theoretical revert as exercised rollback.
4. **Retire separately.** Only after the receipt is complete, remove the local
   implementation, test project, registrations, project/solution references,
   CI/release/test-script inventory, and package assumptions in one isolated,
   reviewed retirement change.

Consumer parity must cover:

- canonical `eventstore:tenant`, `tenants` JSON and space-delimited forms,
  `tenant_id`, `tid`, mixed sources, multiple values, duplicate values, blank
  values, malformed JSON, no source, and repeated transformation;
- the intended `IClaimsTransformation` lifetime and order in both the Parties
  domain-service host and UI host, with no duplicate or order-dependent result;
- authorization outcomes for allowed, missing, mismatched, and multiple tenant
  claims, with no raw token/claim/tenant logging;
- `AggregateIdentity.IsValid(string)` parity with constructor acceptance for
  existing semantic, ULID-shaped, and GUID-shaped IDs and all invalid boundaries;
- `UniqueIdHelper.IsValidUlid(string)` only where strict ULID semantics are
  required; it must not replace permissive aggregate-ID or Party semantic-ID
  validation;
- Debug source-mode and Release package-mode restore/build/focused-test/package
  validation for every supported consumption mode;
- public compatibility: `PartiesClaimTypes.EventStoreTenant` remains a
  compatibility alias to `EventStoreClaimTypes.Tenant` until a separately
  approved versioned removal.

Rollback triggers include changed authorization results, lost or duplicated
tenant claims, DI resolution/order failures, identifier acceptance regressions,
unsafe diagnostics, missing package contents, and source/package divergence.
The retirement receipt must identify the exact deletion commit and prove the
documented non-destructive `git revert <retirement-commit>` path plus dependency
re-pin/restore commands. No rollback instruction may depend on an unrecorded
working tree.

### 4.5 Sprint routing

**Artifact:** `_bmad-output/implementation-artifacts/sprint-status.yaml`

**OLD:**

> One open G7/G9 delivery action requires the four APIs, named approvals, exact
> releases or pins, parity, and rollback before deletion.

**NEW:**

Keep that action `open`. After this proposal is approved, add an evidence comment
that cites this delivery proposal, the revalidated baseline identities, and the
Story 8.3 receipt gate. Do not mark the action `in-progress` until an owner work
packet has an accountable assignee and review reference. Mark it `done` only
after the complete Story 8.3 receipt is recorded and independently checked.

**Rationale:** proposal approval defines execution; it is not owner delivery or
consumer proof.

## 5. Implementation Handoff

**Classification:** Moderate. Product Owner and architecture coordination can
route the work; owner-repository implementation, release, root gitlink changes,
and Parties migration need their own reviewed changes.

| Recipient | Responsibility and exit evidence |
| --- | --- |
| Administrator / Product Owner | Approve, revise, or reject this course correction; keep the action and Story 8.8 authentication slice blocked until the receipt passes. |
| Architecture owner | Check the delivered dependency boundaries and exact API semantics against the approved 2026-07-16 ownership decision; record a named approval. |
| Hexalith.EventStore API owner | Deliver the Contracts constant/predicate and lightweight Authentication package, Gateway composition, producer tests, API/package inventory, and exact review identity. |
| Hexalith.EventStore release or root-pin owner | Publish coherent packages or approve the exact root-declared gitlink and record the name/handle, timestamp, and provenance. |
| Hexalith.Commons API/release owner | Deliver and publish/pin the strict ULID predicate with producer evidence and a named approval. |
| Amelia / Parties Developer | Adopt without deletion, preserve the compatibility alias and rollback path, record exact dependency identities and source/package results, then submit isolated retirement. |
| Murat / Test Architect | Independently verify producer-consumer parity, DI composition, identifier boundaries, safe diagnostics, package/public API evidence, switch-back, and forward restore; record named approval. |
| Story 8.8 reviewer | Verify G7/G9 closure unblocks only the authentication slice and does not waive other Story 8.8 prerequisites. |

### Completion sequence

1. Proposal approved by Administrator on 2026-07-31.
2. Route bounded EventStore and Commons owner work with accountable assignees.
3. Deliver and review producer APIs at coherent exact identities.
4. Record named owner approvals, API/package inventories, producer results, and
   selected release versions or root gitlinks in Story 8.3.
5. Adopt in Parties while retaining the local project.
6. Record consumer parity, exercise switch-back and forward restore, and obtain
   named test/consumer approvals.
7. Transition the G7/G9 row to `available` and close the sprint action.
8. Retire `Hexalith.Parties.Authentication` in a separate reviewed change.
9. Keep Story 8.8 blocked on every remaining independent prerequisite.

## 6. Change-Analysis Checklist

| Checklist item | Status | Finding |
| --- | --- | --- |
| 1.1 Trigger story | Done | Story 8.3 exposes an undelivered prerequisite; deferred 8.4 Authentication deletion and the 8.8 slice remain blocked. |
| 1.2 Core problem | Done | Technical API-availability and release-coordination gap after ownership approval. |
| 1.3 Evidence | Done | Exact current pins, selected package versions, owner source APIs, local implementation, registrations, tests, and matrix row were inspected. |
| 2.1 Current epic viability | Done | Epic 8 remains viable through a gated direct adjustment. |
| 2.2 Epic-level changes | N/A | No epic scope or acceptance change. |
| 2.3 Remaining stories | Done | 8.4 stays done, 8.8 stays blocked, and 8.10 retains final identity/rollback reconciliation. |
| 2.4 New/obsolete epics | N/A | None. |
| 2.5 Order/priority | Done | Owner delivery precedes Parties adoption and deletion; authoritative story order remains unchanged. |
| 3.1 PRD | N/A | No product goal, FR, NFR, or MVP coverage change. |
| 3.2 Architecture | Done | Existing I3/I4/I11/I15 boundaries remain binding; no architecture document edit is required. |
| 3.3 UI/UX | N/A | No user-visible or interaction change. |
| 3.4 Other artifacts | Done | Matrix receipt, sprint evidence, owner API/package inventories, tests, CI/package validation, and later Parties inventory are affected. |
| 4.1 Direct adjustment | Viable | Selected; medium coordinated effort with bounded owner packets. |
| 4.2 Potential rollback | Not selected | Retaining and exercising the current local implementation is part of the direct path, not an alternative scope rollback. |
| 4.3 MVP review | N/A | Epic 8 is maintenance scope with zero new product FRs. |
| 4.4 Recommended path | Done | Deliver, pin, adopt without deletion, prove parity/switch-back, then retire separately. |
| 5.1 Issue summary | Done | Trigger, baseline, gap, and deletion risk are explicit. |
| 5.2 Impact analysis | Done | Epic, story, artifact, architecture, technical, and sequencing effects are covered. |
| 5.3 Recommended path | Done | Direct adjustment and rejected alternatives are documented. |
| 5.4 Action plan | Done | Owner-to-consumer-to-retirement sequence and receipt fields are defined. |
| 5.5 Handoff plan | Done | Accountable roles and required exit evidence are defined without fabricating owner approval. |
| 6.1 Checklist review | Done | Every checklist item is addressed. |
| 6.2 Proposal accuracy | Done | Cross-checked against PRD, epics, architecture spine, UX, Story 8.3, 8.4, 8.8, sprint status, current pins, and source APIs. |
| 6.3 Explicit approval | Done | Administrator approved the proposal on 2026-07-31 at 23:33:05+02:00. |
| 6.4 Sprint-status update | Done | The approved proposal and current baseline are cited while the delivery action remains open. |
| 6.5 Next steps/handoff | Done | Moderate-scope owner delivery and Parties adoption are routed to the Product Owner, platform owners, Developer, and Test Architect; implementation remains separately reviewed work. |

## 7. Approval and Handoff Record

Approved by Administrator on 2026-07-31 at 23:33:05+02:00.

The moderate-scope handoff is recorded for the Product Owner, architecture owner,
Hexalith.EventStore API/release or root-pin owners, Hexalith.Commons API/release
or root-pin owners, Amelia as Parties Developer, and Murat as Test Architect.
The approved deliverables are this proposal, the Story 8.3 G7/G9 delivery receipt,
and the open sprint routing action.

Approval authorizes the course correction and routing-artifact updates. It does
not by itself authorize producer code changes, package releases, root gitlink
advances, Parties migration, or deletion; each remains subject to its normal
owner review and the recorded Story 8.3 gate.
