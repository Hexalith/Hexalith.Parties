---
title: Epic 8 Architecture Spine (Reconciliation)
epic: 8
date: 2026-07-07
updated: 2026-08-18
status: final
amendment: 2026-08-18 validation-driven (I16–I18 added; I1/I1a tightened; §2/§4/§5/§7 corrected — see §8)
classification: post-MVP maintenance (Class C) — zero new PRD FRs
closes-blocker: "Story 8.1 preserved 'missing Epic 8 architecture spine' blocker"
related:
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-06.md
  - _bmad-output/planning-artifacts/implementation-readiness-report-2026-07-07.md
  - _bmad-output/implementation-artifacts/epic-8-context.md
  - _bmad-output/planning-artifacts/architecture/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md
---

# Epic 8 Architecture Spine — Domain-Focus Refactoring & Platform Extraction

## 1. Purpose & Reconciliation Statement

The 2026-07-06 change proposal that created Epic 8 reserved this path for an
architecture spine and made it a prerequisite for the deletion-heavy migration
stories. The spine document was never authored; Story 8.1 correctly *preserved*
"missing Epic 8 architecture spine" as an open blocker, yet Stories 8.2–8.5
shipped against `spec-8-x` files and landed with parity evidence.

This document reconciles that deviation. It does **not** re-derive the design
from scratch — it **ratifies** the artifacts that already carry the spine's
substance and adds the missing piece the readiness assessment asked for: an
explicit invariant set and a per-story readiness gate for the remaining work.

**Authoritative spine artifact set (read together):**
- This document — invariants + readiness gate + blocker closure.
- `epic-8-context.md` — goal, requirements/constraints, technical decisions,
  UX conformance rules, cross-story dependencies.
- Epic 7 spine (`…/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md`)
  — the platform-adoption boundary Epic 8 continues from.
- Landed specs 8.1–8.5 (esp. 8.3's platform-API prerequisite matrix) — the
  approved, evidenced starting state.

**Ratification of 8.2–8.5:** accepted as done. The readiness report (2026-07-07)
records them as done with parity evidence; each was zero-risk hygiene (8.2),
additive platform prerequisites (8.3), leaf-project retirement (8.4), or an
SDK host cutover proven by focused + topology tests (8.5). No rollback of
completed work is warranted (Correct Course §4.2 = not viable).

## 2. Target End-State — Domain-Module Contract

Parties conforms to the Hexalith domain-module contract: it keeps domain
substance and sheds reusable platform mechanics.

| Parties KEEPS (domain) | MOVES to platform owner |
|---|---|
| Aggregates, contracts, validators | Service defaults, correlation/ProblemDetails → Commons |
| Projection/query **semantics** (folds, tenant guardrails) | Projection/query **mechanics** (actors, rebuild, cursor codec) → EventStore SDK |
| GDPR **policy** + legal semantics | Generic crypto/key-management engine → EventStore/shared DataProtection |
| Typed domain clients, domain UI, MCP tool **definitions** | Command/query envelopes + freshness metadata → EventStore.Contracts (G6), referencing Commons paging; paging primitives → Commons (Epic 7 AD-4); MCP plumbing → FrontComposer MCP host on Commons.Http (G11) |
| Tenant-claims **policy** (which claims Parties requires) | Tenant-claim transformation → EventStore.Authentication + Commons ULID helpers (owner decision 2026-07-16; G7/G9) |
| Domain UI **semantics** (labels, GDPR copy, flows) | Status/freshness/reconcile/grid/picker UI primitives → FrontComposer (G4) |
| Domain samples; no domain-owned AppHost in the target state | Build-root probing → Builds; reusable security/module helpers → EventStore.Aspire; canonical integrated local topology → FrontComposer.AppHost / approved platform AppHost owner; runtime deploy orchestration → platform-ops |

Where this table and the Epic 7 spine disagree, this table wins — provided
the divergence carries recorded SCP or owner authority, as the one on record
does. That supersession of the Epic 7 Class A anchor boundary is approved but
gated, not executed (owner decision 2026-07-16, G7/G9): the tenant-claim
anchors route to EventStore.Authentication, while
`Hexalith.Parties.Authentication` remains in-repo as the gated rollback
surface until `8.8-runtime-boundary-cleanup` proves parity and retires it.

## 3. Invariants — must hold across every remaining migration (8.6–8.10 and all later-added Epic 8 work)

**Boundary**
- I1. No public API on the domain-service host. Traffic enters via the
  EventStore gateway over DAPR; ACL stays deny-by-default and admits only the
  `eventstore` app ID to `POST /process`, `/query`,
  `/admin/operational-index-metadata`, `/project`, `/project/v2`,
  `/project/v2/reconcile`, `/replay-state`, `/project/rebuild/v1`,
  `/project/rebuild/shared/v1`, `/project/rebuild/stage/v1`,
  `/project/rebuild/commit/v1`, `/project/rebuild/abort/v1`, and
  `/project/rebuild/verify/v1`. Migration must never add public
  controllers/endpoints to that internal host. The ACL has exactly one
  authoritative owner file at any time (today:
  `src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.parties.yaml`);
  the fitness gate asserts the authoritative copy as (app ID, verb, policy,
  action) tuples, and route-list changes require an owner approval recorded in
  the Story 8.3 matrix reconciliation ledger, referencing this invariant — the
  list above is the baseline, not a hand-editable ceiling.
- I1a. A domain-owned AppHost may remain only as a migration rollback surface.
  The target integrated local topology is owned by FrontComposer.AppHost or an
  explicitly approved platform AppHost owner, and the domain AppHost is retired
  only after topology, security, publish, and rollback parity are proven.
  Retirement additionally requires that every accepted deferral whose exit
  proof or rollback names the Parties AppHost (currently
  `8.6-residual-review-debt`, `8.8-runtime-boundary-cleanup` itself, and
  `external-runtime-deployment`) has passed that proof or been re-approved by
  that deferral's recorded owner against the successor topology; deferral
  rollback clauses take precedence over parity-based retirement until then.
- I2. Host target is the EventStore SDK shape (`AddEventStoreDomainService` /
  `UseEventStoreDomainService`); Parties retains only domain registrations,
  Parties-specific policy, and payload-protection hooks the SDK cannot own.

**Deletion-safety**
- I3. Local rollback paths (projection, query, crypto, release recovery) stay
  in place until the replacement API has **parity evidence** and proven
  rollback. `catch (NotImplementedException)` remoting control flow is deleted
  only after parity.
- I4. No Parties source migration starts from an unapproved or unidentified
  dependency. Every prerequisite is either an owner-approved additive API or an
  already-available surface whose Story 8.3 row records the exact released
  package version or root-declared submodule gitlink SHA selected by the
  consumer. A checked-out source file or `available` status alone is not
  consumption evidence.

**Behavior preservation (stable or intentionally versioned)**
- I5. Public package contracts: `Client` + `Contracts` public shape and the
  three UI RCLs (`Picker`/`AdminPortal`/`ConsumerPortal`).
- I6. Command/query behavior; self-scoped consumer authorization incl.
  `aggregateId == party_id` defense in depth.
- I7. GDPR legal semantics: consent ≠ lawful basis; Art.18 restriction guards
  (consent edits allowed while restricted, rejected while erasure in progress);
  two-front-door erasure + cross-submodule verification (D7).
- I8. Protected-payload compatibility: `json+pdenc-v1`, `json-redacted`, legacy
  unprotected reads, key zeroing, typed-unreadable outcomes, no-leak
  diagnostics, Art.20 exports, Art.30 processing records, erasure
  reports/certificates.

**Projection/query (survive the SDK migration)**
- I9. Replay-from-zero on every delivery; per-read-model sequence checkpoints +
  set-based idempotency; duplicate/out-of-order tolerance.
- I10. Stale/degraded reads render last-known (never throw on staleness);
  `ProjectionFreshnessMetadata` on every read; erased parties excluded from the
  index, and Party identifiers remain permanently unavailable for reuse after
  erasure — sequence/checkpoint state must preserve that tombstone under
  delayed or same-ID events. Target abstractions: `IDomainProjectionHandler`,
  `IDomainQueryHandler`, `IReadModelStore`, `ReadModelWritePolicy`,
  `IQueryCursorCodec`. A full rebuild is executed and verified against
  aggregate replay before local code deletion.

**Identifier, build, UI, GDPR copy, scope**
- I11. Stop rejecting valid ULID-compatible aggregate IDs; retain replay compat
  for GUID-shaped IDs; use Commons unique-ID helpers where semantics require.
- I12. Build discipline unchanged: .NET 10, `.slnx` only, CPM, warnings-as-
  errors, xUnit v3 / Shouldly / NSubstitute / bUnit, Playwright a11y where UI
  is touched, root submodules only, MinVer.
- I13. UI: Fluent 2 inheritance; purge FAST/v4 tokens; teal accent non-text
  only, filled actions bind AA-safe brand background; WCAG 2.2 AA contracts
  (keyboard/pointer parity, skip links, focus rings, forced-colors,
  reduced-motion, semantic controls, typed destructive confirmation,
  polite/assertive live-region split, no focus-stealing on optimistic updates).
- I14. GDPR copy honesty: no consent dark patterns, no over-promised export
  latency, cancellation-vs-permanence distinction, stale reads show last-known.
- I15. Scope: Epic 8 adds **zero** PRD functional requirements and must never be
  reported as MVP feature delivery.

**Gate integrity (added 2026-08-18 after spine validation)**
- I16. Identity-stamped parity: parity evidence is valid only at the exact
  package version or gitlink SHA it was produced against, and must record that
  identity. Any change to a retained dependency identity (Builds catalog value,
  root gitlink, package pin) re-opens every parity claim recorded at a
  different identity of the same dependency — the changing story re-runs the
  affected named test surfaces at the new identity or records the claim as
  unvalidated in the 8.3 matrix before merging. No deletion authorized by
  parity evidence may merge into a tree whose retained identity differs from
  the evidence's stamp. An unvalidated marker is a stop, not a state: while
  any parity claim on a dependency stands unvalidated, no further deletion
  relying on that dependency may merge, and the matrix row names the owner
  who re-runs it.
- I17. Deferral executors inherit the gate: an accepted Epic 8 closure
  deferral (or any Epic 8 ledger item) may be worked only through a spec file
  declaring all six §4 clauses; on activation, the deferral entry is annotated
  with that spec's path, and a second spec may activate the same deferral only
  after the first activation closes or explicitly hands over. Labels do not
  scope this rule: any spec or ledger item — Epic 8-labeled or not — that
  deletes or weakens an I18 baseline surface or code guarded by I1–I15
  inherits the §4 gate the same way. The four deferral fields are the contract
  for *waiting*; the six §4 clauses are the contract for *working*.
- I18. Parity baseline: the parity baseline for each invariant is the set of
  named test surfaces in the §7 map as of the Epic 8 closure commit — the
  first superproject commit that lands the §7 map and the closure fitness
  tests; until it lands, the §7 map in this document as amended 2026-08-18 is
  the baseline. Parity evidence must enumerate the baseline surfaces it
  discharges (or name a successor test approved by an owner recorded in the
  spec, never by the authoring executor alone). Deleting or weakening a
  baseline surface while the deletion of the implementation it guards is
  pending or later relies on it — in the same changeset or across changesets —
  is invalid as parity evidence, and evidence at slice N does not certify
  later slices (I16 identity stamps apply per slice).

## 4. Remaining-Work Readiness Gate (mandatory for all remaining Epic 8 work)

Each `spec-8-x` (8.6–8.10) — and any later-added deletion-heavy Epic 8 spec,
and every spec that activates an accepted closure deferral (I17) — is **not**
ready for a dev session until its spec file declares all six, in the spec
itself:

1. **Prerequisites** — which 8.3 platform APIs must be landed + owner-approved,
   which prior stories must be done, and, for every `available` surface, the
   release or root gitlink recorded in the matrix that must match the consuming
   story's actual dependency mode and identity before source changes begin.
2. **Touched repos/submodules** — Parties + each of EventStore / Commons /
   FrontComposer / Builds / `deploy` that the change edits.
3. **Rollback path** — which local code stays until parity, and how to revert.
4. **Validation lanes** — the specific xUnit v3 assemblies (run directly, not
   `dotnet test --filter`), topology, deploy, and `ui-a11y` lanes, plus the
   **parity evidence** required before any deletion.
5. **Non-goals** — explicit out-of-scope and what must **not** be deleted yet.
6. **Parity-evidence checklist** — the I5–I10/I8 items relevant to that story.

Broad cross-module stories (8.6 projection/query, 8.7 data-protection,
8.8 client/MCP/AppHost/build/deploy) MUST additionally be split or hard-gated
at spec-creation time per readiness Major-issue #3.

## 5. Sequencing & Dependencies

`8.1 → 8.2 → 8.3 → 8.4 → 8.5 → 8.6 → 8.7 → 8.8 → 8.9 → 8.10`.
Stories 8.5–8.7 depend on 8.3 platform-API readiness. 8.10 runs last and closes
or explicitly defers remaining work with owners, proof, rollback, and evidence.
Correct-course additions 8.11–8.13 (validation ladder, container-publish CI,
deployment-asset retirement) executed under the 2026-07-07/08 SCP authority
before the 2026-08-18 amendment brought later-added deletion-heavy specs under
§4. The `8.6 → 8.7 → 8.8 → 8.9` order binds the accepted closure deferrals
exactly as it bound the stories; two deferrals may execute concurrently only if
their §4 clause-2 touched-repo sets (beyond Parties itself, which every set
contains) and their clause-6 parity-checklist sets are disjoint.

## 6. Blocker Closure

**Epic 8 architecture spine — APPROVED (reconciled), 2026-07-07.** Story 8.1's
preserved "missing Epic 8 architecture spine" blocker is **CLOSED for planning
purposes**. Remaining deletion-heavy migrations (8.6–8.10) are henceforth gated
by §4 (per-spec readiness gate) rather than by the absence of this document.

## 7. Story 8.10 Closure Evidence Map — 2026-08-18

The following map is the final Epic 8 disposition. “Executable” means the
retained implementation is guarded by a named automated test surface;
“deferred” means the current path remains the rollback surface and the accepted
entry in `deferred-work.md` owns the future exit proof. No deferred item is
represented as delivered. Map corrected 2026-08-18 after spine validation
(§8); the I4 identity caveat below is the open closure condition that
sprint-status tracks.

<!-- epic-8-invariant-map:start -->
| Invariant | Disposition | Executable evidence or accepted deferral |
| --- | --- | --- |
| I1 | Executable + Deferred | `DocumentationFitnessTests` and `ArchitecturalFitnessTests` verify the static deny-default EventStore-only SDK route contract and absence of retired in-repo deployment assets; `8.6-residual-review-debt` owns runtime ACL enforcement and `external-runtime-deployment` owns environment orchestration. |
| I1a | Deferred | `8.8-runtime-boundary-cleanup` retains the Parties AppHost until integrated-topology, security, publish, and rollback parity exist. |
| I2 | Executable + Deferred | `RetiredLeafProjectFitnessTests` guards the EventStore SDK host shape and retired leaf boundaries at source level; `EventStoreGatewayE2ETests` adds topology-gated coverage (runs fully only with Docker/DAPR available); `8.6-residual-review-debt` owns authenticated end-to-end handler-discovery proof. |
| I3 | Deferred | `8.6-residual-review-debt` (host/gateway/ACL switch-back seams), `8.7-data-protection-extraction`, `8.8-runtime-boundary-cleanup`, and `8.9-frontcomposer-ui-consolidation` retain every named local rollback path until parity is executable; `external-runtime-deployment` owns the release-recovery rollback path. |
| I4 | Executable | `PlatformApiPrerequisitesTests` verifies package/source selection separately and pins EventStore package `3.95.0`, EventStore source `454b4d100c8c095abf5077c6a8d408da6681e87e`, Commons HTTP source `6fbac0c5dff2b8a58e90732c51b31911421a8a65`, and Builds catalog `17b1c7aae3e1854e464f17bd88d527f8350ea203`. Resolved (2026-08-18): superproject commit `2b63ab9` landed the gitlinks and closure tests — all identities now match HEAD and the row is durable. |
| I5 | Executable | `ContractsPublicApiSnapshotTests`, `ClientPackageTests`, `PartyPickerPackagingTests`, `AdminPortalPackagingTests`, and `ConsumerPortalPackagingTests` preserve the public package surface. |
| I6 | Executable + Deferred | `EventStoreGatewayRoutingTests`, `HttpPartiesQueryClientTests`, and `SelfScopedPartiesClientTests` preserve command/query behavior and self-scope; `8.8-runtime-boundary-cleanup` owns future shared-helper adoption. |
| I7 | Executable + Deferred | `PartyAggregateConsentTests`, `PartyAggregateErasureTests`, and `ErasureVerificationServiceTests` preserve consent, restriction, and erasure behavior; `8.6-residual-review-debt` owns the residual erasure-certificate identity/status validation and Memories cleanup-race review debt. |
| I8 | Executable + Deferred | `CryptoKeyManagementCompatibilityHarnessTests`, `AdminPortalGdprPrivacyGuardrailTests`, and `ErasureVerificationServiceTests` preserve retained security and no-leak behavior; `8.7-data-protection-extraction` owns future shared-engine extraction. |
| I9 | Executable + Deferred | `PartySdkProjectionHandlerTests` guards projection replay, checkpoint, idempotency, duplicate, and out-of-order behavior; `8.6-residual-review-debt` owns residual projection quality debt (unbounded Art.30 read model, null-dictionary recovery). |
| I10 | Executable + Deferred | `PartySdkQueryHandlerTests` and `ProjectionFreshnessAndDegradationTests` guard rebuild, freshness, stale-read, erased-index, and tombstone behavior; `8.6-residual-review-debt` owns the open freshness-mapping and search-input-bounds debt. |
| I11 | Executable + Deferred | `IdentifierHygieneFitnessTests` bans GUID-parser regressions while `IdentifierValidatorTests` and `PartyAggregateCompositeTests` preserve ULID-compatible acceptance and GUID-shaped replay; `8.8-runtime-boundary-cleanup` owns future Commons-helper adoption. |
| I12 | Executable | `DocumentationFitnessTests` and `PartiesContainerPublishWorkflowTests` pin the runnable lanes and publication contract; warning, restore, Release build, package, consumer, typecheck, and Playwright receipts remain mandatory closure gates (the always-on CI Playwright a11y lane is a separate open ledger item). |
| I13 | Deferred (parity not yet discharged) | `MainLayoutAccessibilityTests` (asserting the FrontComposer shell slice — skip links and landmarks — adopted 2026-08-18 under the 2026-08-19 backfill SCP) and `PartiesAccessibilitySpecimenTests` guard the retained UI, but I13 parity is NOT discharged: the shell slice left the app-owned focus-visible and forced-colors rules unscoped and therefore dead at runtime, and the Playwright receipt focuses only the shell skip link, never a content control. `8.9-frontcomposer-ui-consolidation` owns both the remaining shared-primitive slices and this open regression. |
| I14 | Executable + Deferred | `MyConsentPageTests` and `MyPrivacyPageTests` guard GDPR copy and stale-read honesty; `8.9-frontcomposer-ui-consolidation` owns the remaining shared-copy consolidation (shell slice already adopted 2026-08-18). |
| I15 | Executable | `EpicEightClosureFitnessTests` verifies that Epic 8 changes no PRD functional-requirement artifact and cannot be reported as MVP feature delivery. |
<!-- epic-8-invariant-map:end -->

### 7a. Disposition of I16–I18 — added 2026-08-19

The map above covers I1–I15 by construction: those invariants each name a
retained implementation surface that a test can guard. I16–I18 are **gate-
integrity invariants** — they govern how evidence is produced, stamped, and
inherited, not what the software does at runtime. They are dispositioned here
rather than in the map so that their absence is a recorded decision instead of a
silent omission, and so the map's row set stays exactly the set the closure
fitness test parses.

- **I16 (identity-stamped parity) — partially executable.**
  `PlatformApiPrerequisitesTests` is I16 enforcement: it verifies package and
  source selection separately and pins each retained identity. Its residual gap
  is that identity re-opening is a review-time obligation, not something a test
  can observe. Owner: whichever story changes a retained identity.
- **I17 (deferral executors inherit the gate) — process gate, not executable.**
  Enforced at spec-authoring and review time. The `deferred-work.md` field
  vocabulary added 2026-08-19 (`authored_by_spec` / `activated_by_spec` /
  `delivered_slices`) makes the waiting-versus-working distinction legible so
  that a reviewer can check it. Owner: the spec author and the reviewer gate.
- **I18 (parity baseline) — process gate, not executable.** The baseline is the
  §7 map as of closure commit `2b63ab9`. `InvariantMapCoversI1ThroughI15WithExecutableOrDeferredEvidence`
  checks that every named surface still resolves to a class under `tests/`, which
  is a necessary but not sufficient guard: it cannot tell whether a surface was
  weakened rather than deleted. Owner: the reviewer gate.

No executable-evidence claim is made for I17 or I18, and none should be
manufactured — a test asserting that a document contains its own wording is not
evidence.

## 8. Validation & Amendment Record — 2026-08-18

The reviewer gate (deterministic lint plus rubric-walker, reality-check,
adversarial, and closure-evidence lenses) validated this spine on 2026-08-18
with a conditional pass; the consolidated report and full reviews live in
`reviews/`. Amendments applied in response: I16–I18 added, I1/I1a tightened,
§2 owners named with the Epic 7 precedence rule and the Class A supersession
stated, §4 rescoped by property, §5 sequencing extended to bind the accepted
closure deferrals, and the §7 map corrected. The formerly open closure
condition is resolved: superproject commit `2b63ab9` (2026-08-18) landed the
submodule identities, the closure fitness tests, and the §7 map as committed
state — `2b63ab9` is therefore the Epic 8 closure commit that freezes the I18
parity baseline. A post-amendment gate pass (rubric, reality-check,
adversarial) confirmed the prior critical/high findings closed and its own
fixes — the gated-not-executed Class A supersession wording, I16–I18
tightenings, and §5/§7 precision — were applied the same day. Findings
deferred with revisit conditions are recorded in this folder's `.memlog.md`.
