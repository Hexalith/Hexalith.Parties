---
title: Sprint Change Proposal — Route EventStore Additive-API Prerequisites
date: 2026-07-11
author: Administrator
workflow: bmad-correct-course
mode: incremental
scope_classification: moderate
status: approved
approved: 2026-07-11T12:25:15+02:00
trigger: >
  Route the EventStore-owned additive API prerequisites for Parties Stories
  8.6, 8.7, and 8.8, and prohibit Parties deletion until owner approval,
  provenance, parity evidence, and rollback proof are recorded.
---

# Sprint Change Proposal — Route EventStore Additive-API Prerequisites

## 1. Issue Summary

Epic 8 removes generic platform mechanics from Hexalith.Parties, but Stories
8.6 through 8.8 cannot safely delete Parties-local projection/query, payload-
protection, client-adapter, degraded-response, or DAPR-health implementations
until Hexalith.EventStore supplies additive replacement APIs with behavioral
parity.

Story 8.3 correctly identified the missing surfaces and assigned them to
Hexalith.EventStore. The consuming specs correctly block migration, but the
owner-routing action remained `open`. This proposal turns that requirement into
four explicit owner work packages and records the routing without representing
it as delivery or approval.

### Trigger and evidence

- **Immediate trigger:** Story 8.6 halted at its prerequisite gate on 2026-07-09.
- **Story state:** 8.6 is `blocked`; 8.7 and 8.8 remain `backlog`.
- **Matrix state:** the affected Story 8.3 rows remain `needs-additive-api`.
- **Tracking state:** the exact EventStore routing action existed in
  `sprint-status.yaml` with status `open`.
- **Deletion risk:** starting Parties migration from checked-out contracts alone
  would bypass owner approval, release provenance, parity proof, and rollback.

The problem is a technical limitation and cross-repository ownership gap, not a
new product requirement.

## 2. Impact Analysis

### Epic impact

Epic 8 remains viable and retains its approved Class C, post-MVP maintenance
scope. No epic is added, removed, redefined, or resequenced. The existing
sequence remains authoritative:

`8.1 -> 8.2 -> 8.3 -> 8.4 -> 8.5 -> 8.6 -> 8.7 -> 8.8 -> 8.9 -> 8.10`

The EventStore owner packages may be delivered in parallel, but their consuming
Parties stories remain individually gated.

### Story impact

- **8.6 Projection/query SDK migration:** remains blocked by G3 read-model
  erasure hooks, G10 index batching, and G6 freshness/client-adapter parity.
- **8.7 Data-protection extraction:** remains backlog and blocked from source
  migration by G5 shared payload-protection engine delivery.
- **8.8 Client/MCP/AppHost/build cleanup:** remains backlog and blocked from the
  EventStore-owned slices by G6 client adaptation and G1/G2 degraded-response and
  DAPR-health parity. Other existing 8.8 gates remain independently binding.
- **8.9 UI consolidation:** receives no separate owner work package here;
  existing freshness and degraded-read behavior remains downstream parity.
- **8.10 Final gate:** cannot close the routed action or authorize deletion
  without recorded owner delivery and consuming-story evidence.

### Artifact conflicts

- **PRD:** no conflict or edit. Epic 8 adds zero functional requirements.
- **Epics:** no scope or sequencing edit required.
- **Architecture:** no conflict or edit. Invariants I3, I4, and I8-I10 already
  require additive approval, rollback retention, and behavior parity.
- **UX:** no new surface or copy. Existing freshness, degraded, erased, export,
  processing-record, certificate, and no-leak behavior remains binding.
- **Tracking:** the owner action changes from `open` to `in-progress`; affected
  story statuses do not advance.

### Technical impact

This proposal authorizes documentation and routing-state changes only. It does
not authorize production code changes, submodule edits, package-version changes,
gitlink changes, persisted-format changes, route or ACL changes, or deletion of
any Parties rollback implementation.

## 3. Recommended Approach

**Selected approach: Direct Adjustment.** Route four independently deliverable
work packages to Hexalith.EventStore owners and keep every consuming Parties
story blocked until its corresponding matrix row records complete exit evidence.

- **Routing effort:** Low.
- **External delivery effort:** High.
- **Risk:** Moderate while the gates remain enforced; high if Parties deletion
  begins from unapproved or contract-only evidence.
- **Timeline impact:** no MVP impact. Epic 8 schedule remains dependent on
  EventStore owner delivery; this proposal does not invent an external delivery
  date.
- **Scope classification:** Moderate because resolution requires coordinated
  owner backlog, architecture approval, developer adoption, and test evidence
  across repositories.

### Alternatives considered

- **Potential rollback:** not viable. Completed Stories 8.3-8.5 remain valid,
  and the retained Parties implementations are themselves the rollback paths.
- **MVP review:** not applicable. Epics 1-5 remain complete feature scope.
- **New Parties epic:** rejected. The missing work belongs to EventStore owners;
  representing it as Parties implementation would blur ownership and authority.

## 4. Detailed Change Proposals

### 4.1 Sprint routing status

**Artifact:** `_bmad-output/implementation-artifacts/sprint-status.yaml`  
**Section:** Epic 8 platform-API prerequisite routing

**OLD:** EventStore routing action status `open`.

**NEW:** Status `in-progress`, with a dated comment referencing this proposal and
stating that it remains in progress until all four packages have owner acceptance,
approved provenance, and Story 8.3 parity evidence.

**Rationale:** Records routing without falsely claiming delivery.

### 4.2 Story 8.3 matrix routing record

**Artifact:**
`_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md`

**OLD:** The four affected rows named EventStore ownership and proof requirements
but had no dated routing record.

**NEW:** Add the four work packages below. All affected row statuses stay
`needs-additive-api`.

#### Package A — Projection/query parity for Story 8.6

Hexalith.EventStore owners deliver additive or approved APIs and evidence for:

- **G3:** read-model erasure hooks that remove erased parties from indexes and
  preserve PII-free detail tombstones.
- **G10:** index batching compatible with idempotent `ReadModelWritePolicy`
  transforms, duplicate/out-of-order delivery, and rebuild semantics.
- **G6:** freshness mapping preserving `Current`, `Stale`, `Rebuilding`,
  `Degraded`, `Unavailable`, and `LocalOnly` behavior plus last-known fallback.

Exit evidence includes named owner approval, exact release or root EventStore
submodule pin, producer/consumer tests, full rebuild-versus-aggregate-replay
evidence, cursor-scope compatibility, and exercised rollback.

#### Package B — Shared payload-protection engine for Story 8.7

Hexalith.EventStore owners deliver an additive shared engine package containing:

- `pdenc-v2` writes with authenticated additional-data binding.
- Read compatibility for existing `json+pdenc-v1`, `json-redacted`, and legacy
  unprotected payloads.
- `IPersonalDataPolicy` and `IErasureStateProvider` extension seams.
- Typed unreadable outcomes, key storage/wrapping/rotation, retry, audit,
  circuit-breaker, and pluggable production-backend behavior.
- A moved golden compatibility harness covering protected/redacted/legacy reads,
  key zeroing, no-leak diagnostics, Art.20 exports, Art.30 processing records,
  erasure reports/certificates, and rollback.

Exit evidence includes named security/owner approval, exact release or root
EventStore submodule pin, stable format/state-key/actor/metric compatibility,
green golden results against local and shared paths, and exercised rollback.

#### Package C — Client envelope/freshness/error adapter for Stories 8.6 and 8.8

Hexalith.EventStore owners deliver an additive adapter preserving:

- Typed command and query outcomes.
- G6 freshness states and warning codes.
- Bounded ProblemDetails reason/error-code mapping without PII or provider detail.
- Compatibility with existing Parties paging, query, and client behavior.

Exit evidence includes named owner approval, exact release or root EventStore
submodule pin, public/package compatibility tests, producer/consumer parity, and
rollback to the Parties adapter.

#### Package D — Degraded response and DAPR health parity for Story 8.8

Hexalith.EventStore owners deliver:

- **G1/G2:** additive degraded-response behavior matching Parties status and
  header semantics.
- `AddEventStoreDaprHealthChecks` with state-store/pub-sub option parity and the
  established readiness/liveness tag policy.

Exit evidence includes named owner approval, exact release or root EventStore
submodule pin, degraded-header and health-state parity tests, topology evidence,
and rollback to Parties middleware and health checks.

### 4.3 Consuming story specs

**Artifacts:** Specs 8.6, 8.7, and 8.8.

**OLD:** Each spec is already `blocked-prerequisite` and contains the applicable
Block-If rule and rollback set.

**NEW:** Unchanged.

**Rationale:** The specs remain the execution gates; duplicating the handoff text
inside them would create drift from the Story 8.3 matrix.

### 4.4 PRD, epics, architecture, and UX

**OLD:** Epic 8 is zero-FR maintenance and governed by parity-before-deletion.

**NEW:** Unchanged.

**Rationale:** This is owner routing and backlog governance, not a product,
architecture, or interaction change.

## 5. Implementation Handoff

### Scope and recipients

**Classification:** Moderate — backlog coordination across platform owners and
Parties Developer/Test roles; no fundamental product replan.

| Recipient | Responsibility |
| --- | --- |
| Hexalith.EventStore owners | Accept, split, implement, review, and release or pin Packages A-D as additive surfaces. Record named approvals and exact provenance. |
| Winston / Parties Architect | Keep Story 8.3 as the authoritative gate, validate ownership and API shape, and reject checked-out-source-only evidence. |
| Amelia / Parties Developer | Preserve local implementations and registrations until each consuming story proves parity and rollback. Adopt only approved APIs. |
| Murat / Test Architect | Require producer/consumer, golden, no-leak, topology, rebuild, health, package, and rollback evidence appropriate to each package. |
| Product Owner | Keep 8.6 blocked and 8.7-8.8 backlog until their prerequisite rows satisfy the exit criteria; prevent 8.10 from silently bypassing open work. |

### Global success criteria before any Parties deletion

1. The corresponding EventStore work package has a named accepting owner.
2. The additive API is available through an exact released package or recorded
   root EventStore submodule pin.
3. Story 8.3 contains the owner approval, provenance, validation commands, and
   green results before the consuming source migration begins.
4. Producer and Parties consumer parity covers every behavior named in the
   affected matrix row and spec.
5. The Parties-local path remains present until a rollback exercise succeeds.
6. No public package, gateway, DAPR ACL, GDPR, freshness, degraded-read,
   health-check, PII-safety, or UI behavior regresses.
7. Only after the consuming story completes may its local implementation be
   deleted and the corresponding routing package be marked complete.

### Deferred and out of scope

- Implementing or editing Hexalith.EventStore source in this workflow.
- Advancing any prerequisite row to `available`.
- Starting Stories 8.6-8.8 implementation.
- Deleting Parties production or rollback code.
- Changing packages, root gitlinks, persisted data, routes, DAPR ACLs, or UI.
- Provisioning a production KMS.

## 6. Change-Analysis Checklist

| Checklist item | Status | Finding |
| --- | --- | --- |
| 1.1 Trigger story | [x] Done | Story 8.6 exposed the immediate prerequisite block; 8.7 and 8.8 share EventStore gaps. |
| 1.2 Core problem | [x] Done | Missing owner-approved additive platform APIs make Parties deletion unsafe. |
| 1.3 Evidence | [x] Done | Matrix rows remain `needs-additive-api`; 8.6 blocked; 8.7-8.8 backlog; routing action was open. |
| 2.1 Current epic viability | [x] Done | Epic 8 remains viable with gated external prerequisites. |
| 2.2 Epic-level changes | [x] Done | Strengthen routing and handoff; no new epic. |
| 2.3 Remaining epics/stories | [x] Done | 8.6, 8.7, 8.8, 8.9 downstream parity, and 8.10 closure assessed. |
| 2.4 New/obsolete epics | [N/A] Skip | None. |
| 2.5 Order/priority | [x] Done | Parties sequence unchanged; EventStore packages may proceed in parallel. |
| 3.1 PRD | [x] Done | No conflict or FR change. |
| 3.2 Architecture | [x] Done | Existing I3/I4/I8-I10 govern approval, rollback, and parity. |
| 3.3 UI/UX | [N/A] Skip | No UX change; existing safe states remain parity evidence. |
| 3.4 Other artifacts | [x] Done | Matrix and sprint tracking require routing records; specs remain unchanged. |
| 4.1 Direct adjustment | [x] Viable | Selected. Routing effort low; external delivery high. |
| 4.2 Potential rollback | [N/A] Skip | Completed work remains valid; retained Parties code is the rollback. |
| 4.3 MVP review | [N/A] Skip | Epic 8 is post-MVP maintenance. |
| 4.4 Recommended path | [x] Done | Four EventStore owner packages with independent exit gates. |
| 5.1 Issue summary | [x] Done | Trigger, context, evidence, and deletion risk recorded. |
| 5.2 Impact analysis | [x] Done | Story and artifact impacts recorded. |
| 5.3 Recommended path | [x] Done | Direct adjustment and alternatives documented. |
| 5.4 MVP/action plan | [x] Done | No MVP impact; routing and evidence sequence defined. |
| 5.5 Handoff plan | [x] Done | Owner, Architect, Developer, Test Architect, and Product Owner responsibilities defined. |
| 6.1 Checklist review | [x] Done | All applicable analysis items addressed. |
| 6.2 Proposal accuracy | [x] Done | Proposal matches current matrix, specs, architecture, and sprint status. |
| 6.3 Explicit approval | [x] Done | Administrator approved the complete proposal on 2026-07-11 at 12:25:15+02:00. |
| 6.4 Sprint-status update | [x] Done | Routing changed from `open` to `in-progress`; story statuses unchanged. |
| 6.5 Next steps/handoff | [x] Done | Four owner packages routed to Hexalith.EventStore owners; delivery and evidence remain governed by the matrix gate. |

## 7. Approval and Routing Record

Administrator approved this proposal on 2026-07-11 at 12:25:15+02:00.

The moderate-scope handoff is complete for planning purposes:

- Hexalith.EventStore owners receive Packages A-D as additive owner work.
- Winston retains architecture and Story 8.3 gate ownership.
- Amelia retains all Parties rollback implementations until consuming-story parity.
- Murat owns parity, no-leak, rebuild, topology, health, package, and rollback evidence.
- Product ownership keeps Story 8.6 blocked and Stories 8.7-8.8 in backlog until
  their corresponding prerequisite rows satisfy the approved exit criteria.

Routing is not delivery. The sprint action remains `in-progress`, all affected
Story 8.3 rows remain `needs-additive-api`, and no Parties deletion is approved.
