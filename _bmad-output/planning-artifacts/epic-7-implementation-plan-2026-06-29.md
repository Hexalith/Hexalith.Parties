---
project_name: parties
date: 2026-06-29
workflow: bmad-architecture
epic: epic-7-platform-alignment
status: approved-for-story-creation
approved_by: Administrator
architecture_spine: _bmad-output/planning-artifacts/architecture/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md
memlog: _bmad-output/planning-artifacts/architecture/epic-7-platform-alignment-2026-06-29/.memlog.md
---

# Epic 7 PM/Architect Implementation Plan

Epic 7 is approved as post-MVP platform maintenance: adopt existing or newly
added Hexalith shared platform primitives where Parties currently carries generic
technical infrastructure. It covers **no new PRD functional requirements** and must
not be used as product-feature readiness evidence.

This plan approves the Epic 7 story backlog and sprint-status tracking. It does
not authorize source-code changes by itself. Developer execution still starts with
dedicated `7-*` story files created from this plan.

## Approved Approach

Use an adapter-first strangler migration:

1. Prove target ownership, package graph, compatibility, and rollback before code
   migration.
2. Introduce Parties-compatible adapters over shared primitives.
3. Run parity tests against old and new paths.
4. Remove local infrastructure only after a story proves behavior and rollback.

Non-goals:

- No product-scope change to Epics 1-5.
- No change to EventStore gateway routing or a new public Parties host API.
- No weakening of GDPR erasure, crypto-shredding, projection idempotency, or
  own-data authorization.
- No recursive submodule update and no unowned submodule edits.

## Target Destination Matrix

| ID | Scope | Target destination | Plan decision |
| --- | --- | --- | --- |
| B1 | Projection sequence checkpoint and replay dedupe | `Hexalith.EventStore.Server.Projections` | Consume `IProjectionCheckpointTracker` through a Parties adapter; keep Parties read contracts stable. |
| B2 | Resumable projection rebuild/replay-from-zero | `Hexalith.EventStore.Server.Projections` | Consume `IProjectionRebuildOrchestrator` and rebuild checkpoint contracts after parity tests. |
| B3 | AES-GCM payload protection | `Hexalith.EventStore.Contracts/Security` plus provider package | Treat EventStore payload-protection contracts as the generic seam; migrate only after 7.6 proof harness. |
| B4 | Party key-management subsystem | Shared security only for generic key-provider mechanics; Parties for policy | Do not move party-specific legal semantics or commands into EventStore. |
| B5 | ServiceDefaults | `Hexalith.Commons.ServiceDefaults` | Replace local defaults with Commons or keep a thin wrapper for Parties-specific health hooks. |
| B6 | Correlation accessor/middleware | Commons metadata/diagnostics or additive Commons middleware | Centralize correlation shape without leaking PII or changing command IDs. |
| B7 | ProblemDetails/global exception mapping | Commons/ServiceDefaults where available | Share bounded HTTP error mapping; keep domain rejection semantics in Parties. |
| B8 | Jaro-Winkler and diacritic normalization | Commons for pure text helpers; Memories for search-specific scoring | Preserve local fallback search when Memories is absent. |
| B9 | Projection freshness vocabulary | EventStore query/ETag/freshness primitives plus Parties compatibility mapping | Keep `ProjectionFreshnessMetadata` until a separate public contract migration is approved. |
| B10 | `PagedResult<T>` | Commons generic paging package | Adopt behind an adapter so public Parties client/UI shape remains compatible. |
| B11 | Mixed primitives | Split by owner | `DecryptionCircuitBreaker` follows payload protection; `PartyEventTypeResolver` follows EventStore type-resolution; typed-client ProblemDetails follows Commons; UI orchestration follows FrontComposer; `IIndexPartitionStrategy` follows EventStore projections. |

## Story Backlog

### Story 7.1 - Platform Target-Destination ADR And Release/Rollback Plan

**Status:** backlog.
**Purpose:** close ownership, dependency, and rollout decisions before code.

Acceptance criteria:

- Map every B1-B11 item to owner repo/project, API surface, package/reference path,
  release order, rollback path, and test evidence.
- Decide whether `Hexalith.CommonsRoot` is added to `Directory.Build.props` for
  project references or whether released package versions are used.
- Identify any additive submodule API stories needed before Parties adoption.
- Preserve Central Package Management, `.slnx`, no `Version=` in `.csproj`, and
  root-only submodule rules.

### Story 7.2 - Commons Service Defaults, Correlation, ProblemDetails, And Paging

**Status:** backlog.
**Purpose:** migrate low-risk shared utilities first.

Acceptance criteria:

- Replace or wrap `Hexalith.Parties.ServiceDefaults` with
  `Hexalith.Commons.ServiceDefaults` while preserving health endpoint behavior,
  OpenTelemetry sources, JSON console logging expectations, and DAPR health hooks.
- Centralize bounded correlation and ProblemDetails handling without logging PII.
- Introduce Commons paging compatibility for `PagedResult<T>` with no public client
  or UI behavior change.
- Build and unit tests prove old and new outputs match.

### Story 7.3 - Search Normalization And FrontComposer UI Orchestration Convergence

**Status:** backlog.
**Purpose:** remove utility duplication that is not projection or crypto sensitive.

Acceptance criteria:

- Move pure diacritic normalization and similarity helpers to Commons or consume an
  approved shared helper.
- Keep search operational when Memories is absent; Memories-specific scoring remains
  optional and configuration gated.
- Adopt FrontComposer lifecycle/orchestration primitives only where they preserve the
  existing `StatusKind`, aria-live, and optimistic reconciliation behavior.
- Tests cover local fallback, Memories-enabled path, and no search-result behavior
  drift for existing admin list/search scenarios.

### Story 7.4 - Projection Platform Compatibility Adapter

**Status:** backlog.
**Purpose:** introduce the EventStore projection platform behind Parties contracts.

Acceptance criteria:

- Add a compatibility adapter from Parties projection actors/services to EventStore
  checkpoint, rebuild, and query/freshness primitives.
- Existing `ProjectionFreshnessMetadata` and `StatusKind` UI mapping remain stable.
- Tests cover duplicate events, out-of-order delivery, replay from sequence zero,
  state-store unavailability, stale/degraded fallback, and erased-party exclusion.
- Rollback is a single adapter registration or package/submodule pointer reversal.

### Story 7.5 - Projection Checkpoint/Rebuild Migration And Local Code Removal

**Status:** backlog.
**Purpose:** remove the parallel Parties projection platform after adapter parity.

Acceptance criteria:

- Replace Parties-local checkpoint/rebuild implementation with EventStore primitives.
- Remove duplicate Parties projection infrastructure only after parity tests pass.
- Preserve idempotent replay, at-least-once tolerance, rebuild resume/cancel behavior,
  and last-known read fallback.
- Integration or topology evidence verifies command -> publish -> projection -> query
  flow through the EventStore gateway.

### Story 7.6 - Crypto/Key-Management ADR And Compatibility Harness

**Status:** backlog.
**Purpose:** decide the generic-vs-party-specific split before moving security code.

Acceptance criteria:

- Produce an ADR that classifies payload protection, key storage, wrapping,
  rotation, crypto-shredding workflow, audit, circuit breaker, and event-type
  resolution into EventStore/shared security vs Parties policy.
- Build a compatibility harness proving readable, unreadable, missing-key,
  provider-unavailable, erased, restricted, and legacy unprotected cases.
- Prove no PII, key alias, destroyed-key detail, or raw payload appears in logs,
  ProblemDetails, telemetry, processing records, or erasure reports.
- Developer migration stories remain blocked until this harness is green.

### Story 7.7 - Crypto/Key-Management Migration Behind EventStore Provider Contracts

**Status:** backlog.
**Purpose:** migrate approved generic crypto/provider pieces without changing GDPR behavior.

Acceptance criteria:

- Register the approved provider through EventStore payload-protection contracts.
- Keep Parties-specific commands, legal policy, user copy, tenant/party key semantics,
  and erasure orchestration in Parties unless the 7.6 ADR explicitly moves them.
- Preserve crypto-shredding irreversible completion, erasure certificate behavior,
  export/processing-record redaction, and key-unavailable UX states.
- Rollback restores the previous Parties provider without data loss or metadata
  incompatibility.

### Story 7.8 - Release, Rollback, Cleanup, And Readiness Gate

**Status:** backlog.
**Purpose:** finish the cross-submodule release safely.

Acceptance criteria:

- Submodule commits or package versions are pinned and documented.
- Deprecated Parties-local infrastructure is removed only where replacement evidence
  exists; otherwise a removal story is deferred explicitly.
- Run the agreed build/test lanes for Parties and each touched submodule.
- Update planning docs, project context if durable rules changed, and sprint status.
- Final readiness notes state Epic 7 remains post-MVP platform maintenance and does
  not change PRD FR coverage.

## Sequencing

`7.1 -> 7.2 -> 7.3 -> 7.4 -> 7.5 -> 7.6 -> 7.7 -> 7.8`

Stories 7.2 and 7.3 may run after 7.1 in parallel if they do not require the same
submodule release. Story 7.5 requires 7.4. Story 7.7 requires 7.6. Story 7.8 runs
last.

## Required Gates

- `dotnet build Hexalith.Parties.slnx -c Release` after any adoption story.
- Focused unit tests for modified Parties projects.
- Owning submodule build/test lane for each touched submodule.
- Projection migration stories must include duplicate, out-of-order, replay, stale,
  degraded, and erased-party tests.
- Crypto migration stories must include readable/unreadable and no-PII evidence.
- `git diff --check` before handoff.
