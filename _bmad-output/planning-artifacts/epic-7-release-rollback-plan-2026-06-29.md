---
date: 2026-06-29
status: approved
epic: epic-7-platform-alignment
story: 7.1
plan: release-rollback
---

# Epic 7 Release and Rollback Plan

## Scope

This plan makes the approved Epic 7 sequence executable for implementation
stories. It does not authorize production code migration by itself. It preserves
EventStore gateway routing, projection idempotency, stale/degraded fallback, GDPR
erasure guarantees, crypto-shredding guarantees, PII-free logs/telemetry, and
public Parties contract compatibility unless a separate breaking-change plan
approves otherwise.

## Release Sequence

The release order is:

`7.1 -> 7.2/7.3 -> 7.4 -> 7.5 -> 7.6 -> 7.7 -> 7.8`

Story 7.2 and Story 7.3 may run after Story 7.1 in parallel if they do not
require the same Commons or FrontComposer submodule release. Story 7.5 requires
Story 7.4. Story 7.7 requires Story 7.6. Story 7.8 runs last and records the
final readiness gate.

## Dependency Gates

| Gate | Required before release advances |
| --- | --- |
| ADR gate | Story 7.1 ADR accepted and every target destination has owner, reference path, rollback, and evidence. |
| Owner API gate | Any missing shared API lands additively in the owner submodule before Parties references it. |
| Reference gate | Root submodule pointer or Central Package Management package version is updated only in the adopting story. |
| Adapter gate | Parties-compatible adapter exists before local infrastructure is removed. |
| Parity gate | Old/new behavior parity evidence passes for the adoption cluster. |
| Rollback gate | A named switch, pointer rollback, or deferred-deletion path exists and is tested or inspection-validated. |
| Privacy gate | No story weakens PII redaction, erasure guarantees, key-destruction semantics, or regulated copy. |

## Adoption Clusters

| Cluster | Release unit | Prerequisite owner-submodule story | Parties adoption story | Validation lane | Rollback switch or pointer | Data/contract compatibility condition |
| --- | --- | --- | --- | --- | --- | --- |
| Commons service defaults, correlation, ProblemDetails, and paging | Commons root property plus Commons ServiceDefaults/Metadatas/Http/core references | Story 7.2 lands `HexalithCommonsRoot`; if Commons lacks diagnostics, HTTP error, or paging APIs, Story 7.2 lands additive Commons changes before adoption | Story 7.2 | Parties focused unit tests for service defaults, typed-client error mapping, correlation propagation, paging serialization; Commons owner tests when touched; `git diff --check` | Revert Commons reference or wrapper DI registration; restore local `Hexalith.Parties.ServiceDefaults`, `CorrelationIdMiddleware`, and current error/paging mapping | Public `PagedResult<T>`, typed client outcomes, health endpoints, OpenTelemetry, and PII-free ProblemDetails behavior remain compatible |
| Search normalization and FrontComposer UI orchestration | Commons pure text helpers and FrontComposer lifecycle/orchestration consumption | Story 7.3 lands additive Commons text helpers if absent; FrontComposer APIs must already exist or be added in owner story before use | Story 7.3 | Local fallback search parity, Memories-enabled search parity, bUnit/Playwright lanes when UI orchestration changes, `git diff --check` | Revert adapter to local Jaro-Winkler/diacritic code and local optimistic/freshness orchestration; roll back Commons/FrontComposer pointer if needed | Admin search results, Memories-optional behavior, `StatusKind`, aria-live split, and optimistic reconciliation remain unchanged |
| Projection compatibility adapter | EventStore projection checkpoint/rebuild/freshness adapter surface | Story 7.4 adds EventStore gaps only if existing projection contracts cannot preserve Parties semantics | Story 7.4 | Projection unit tests for duplicate events, out-of-order delivery, replay from sequence zero, state-store unavailability, stale/degraded fallback, erased-party exclusion; build lane; `git diff --check` | DI switch returns to Parties-local checkpoint/rebuild/freshness path; EventStore pointer rollback remains possible | `ProjectionFreshnessMetadata`, read models, query results, and EventStore gateway delivery stay compatible |
| Projection checkpoint/rebuild migration and local removal | EventStore projection primitives become active path; duplicate Parties code deleted only after evidence | Story 7.4 complete and parity evidence approved | Story 7.5 | Full projection regression lane, integration/topology evidence for command -> publish -> projection -> query when environment permits, rebuild resume/cancel tests, `git diff --check` | Revert deletion commit or submodule pointer; re-enable local projection services through DI | Projection state remains idempotent and replayable; no data migration is required without a rollback script and approval |
| Crypto/key-management decision and harness | ADR plus compatibility harness, no production crypto migration | Story 7.6 may require EventStore/shared-security additive APIs for provider mechanics, workflow vocabulary, or circuit-breaker classification | Story 7.6 | Harness covers readable, unreadable, missing-key, provider-unavailable, erased, restricted, legacy unprotected, no-PII/no-key/raw-payload leakage; `git diff --check` | No production path changes to roll back; if harness scaffolding changes registration, keep it test-only or revert | Party-specific legal policy, erasure commands, certificates, export/processing records, and no-PII telemetry remain unchanged |
| Crypto/key-management migration | Approved provider contracts and mechanics from EventStore/shared security | Story 7.6 approved split and green harness; any provider implementation is released/pinned before use | Story 7.7 | Crypto harness plus focused security tests, erasure export/certificate regression, provider-unavailable/readability tests, build lane, `git diff --check` | Restore previous `PartyPayloadProtectionService`/key provider registration; roll back provider package/submodule pointer | Existing protected payloads remain readable or correctly classified; crypto-shredded payloads remain irreversibly unreadable |
| Final release, rollback cleanup, and readiness gate | Final submodule commits or package versions plus removal of only evidence-backed local infrastructure | All adoption stories complete and reviewed | Story 7.8 | Full agreed Parties lanes, owner submodule lanes for touched repositories, package/API compatibility checks, docs/project-context updates when durable rules changed, `git diff --check` | Pin rollback commit/package set; defer deletion lacking evidence; restore previous pointers if gate fails | Public Parties packages/contracts/UI behavior remain compatible; Epic 7 remains post-MVP maintenance and not PRD feature evidence |

## Story-Level Release Rules

### Story 7.1

- Release unit: planning artifacts only.
- No production code, project references, package versions, submodule pointers, or
  submodule source changes.
- Validation: required markdown artifacts exist, every target item is mapped,
  missing APIs are routed to owner stories, and `git diff --check` passes.
- Rollback: remove the two planning artifacts and revert story/sprint bookkeeping
  if the ADR is rejected before later stories consume it.

### Story 7.2

- Adds `HexalithCommonsRoot` only after confirming root `references/Hexalith.Commons`
  checkout rules.
- Adds Commons project references for service defaults, metadata/diagnostics,
  HTTP error mapping, and paging only after owner APIs exist.
- Uses adapters/wrappers so local behavior remains restorable.

### Story 7.3

- Keeps Memories optional and configuration gated.
- Uses Commons only for pure text helpers.
- Uses FrontComposer lifecycle/orchestration only where existing `StatusKind`,
  aria-live, and optimistic reconciliation behavior are preserved.

### Story 7.4

- Introduces EventStore projection compatibility adapters.
- Does not delete local projection infrastructure.
- Establishes parity evidence required by Story 7.5.

### Story 7.5

- Replaces local checkpoint/rebuild implementation only after Story 7.4 proves
  compatibility.
- Deletes duplicate code only where rollback and replay evidence exist.
- Defers any unproven deletion to Story 7.8 or a follow-up story.

### Story 7.6

- Produces crypto/key-management ADR and harness before migration.
- Classifies generic provider mechanics versus party-specific legal policy.
- Blocks Story 7.7 until the harness is green.

### Story 7.7

- Migrates only the provider and generic mechanics approved by Story 7.6.
- Keeps party-specific commands, legal copy, key semantics, and erasure
  orchestration local unless the Story 7.6 ADR explicitly routes an additive API.
- Requires rollback proof before cleanup.

### Story 7.8

- Pins final submodule commits or package versions.
- Records final readiness evidence and rollback set.
- Removes local infrastructure only where previous stories provide passing parity
  and rollback evidence.
- Updates durable docs and project context only if the platform rules changed.

## Rollback Sets

Each adoption story records an explicit rollback set:

- Code switch: DI registration, adapter flag, or wrapper selection that restores
  the local implementation.
- Pointer switch: root submodule commit rollback or package version rollback.
- Data condition: whether any state migration occurred and how replay or fallback
  preserves existing reads.
- Contract condition: whether public package/API behavior is unchanged or
  separately versioned.

No story may rely on deleting local code as its rollback mechanism. Local code is
kept until the replacement path is proven and Story 7.8 records readiness.

## Final Readiness Evidence for Story 7.8

Story 7.8 must record:

- Final submodule commit hashes or package versions for every touched shared
  repository/package.
- The exact Parties build and test lanes run.
- The owning submodule build/test lanes run.
- Projection parity evidence and any deferred deletion list.
- Crypto/key-management harness evidence and provider rollback notes.
- Public contract/package compatibility notes.
- Confirmation that EventStore gateway routing, GDPR erasure, crypto-shredding,
  PII-free logs/telemetry, stale/degraded fallback, and public Parties behavior
  remain compatible.
- Confirmation that Epic 7 remains post-MVP platform maintenance and does not
  change PRD functional coverage.

## Scope Guard

This plan intentionally does not change code or references. Any future story that
needs a package upgrade, submodule pointer update, source migration, local
infrastructure deletion, or cross-submodule source edit must name that change in
its own tasks and validation evidence before implementation.
