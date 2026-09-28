---
title: Epic 8 Architecture Spine (Reconciliation)
epic: 8
date: 2026-07-07
updated: 2026-09-08
status: final
amendment: 2026-09-08 validation-driven (I19/I19a/I20 added; I1/I7/I10/I11/I14/I16 tightened; §2/§4/§5/§7 corrected; Inherited Invariants + Deferred added — see §10); prior 2026-08-18 amendment in §8
classification: post-MVP maintenance (Class C) — zero new PRD FRs
closes-blocker: "Story 8.1 preserved 'missing Epic 8 architecture spine' blocker"
open-condition: "HEAD Builds gitlink a32cb422 is past the last I16-authorized pin 35c3d1e5 with no signoff — I16 stop in effect (see §7 I4, §10)"
related:
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-06.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-16-g7-g9-tenant-claims-ownership.md
  - _bmad-output/planning-artifacts/implementation-readiness-report-2026-07-07.md
  - _bmad-output/implementation-artifacts/epic-8-context.md
  - _bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md
  - _bmad-output/planning-artifacts/architecture/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md
---

# Epic 8 Architecture Spine — Domain-Focus Refactoring & Platform Extraction

**Approved McpCli course correction (2026-09-27):** `Hexalith.McpCli` is the target Hexalith-owned CLI/MCP surface for Parties operations that pass contract enrollment and authorization. Any proprietary module CLI, MCP host, plug-in, or planned adapter described below is an obsolete migration source or historical design, not a new target. The module retains its domain, UI, and security semantics; replacement or approved withdrawal and parity evidence precede retirement. External development CLIs are unaffected.


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

## 1a. Design Paradigm & Inherited Invariants

**Paradigm (inherited, unchanged):** adapter-first strangler migration over
event-sourced platform boundaries — the Epic 7 paradigm. Epic 8 is its
extraction-and-deletion phase: Parties stays the behavior-preserving facade,
every remaining deletion follows an adapter or compatibility surface that has
already proven parity (I3, §4), and nothing here introduces a delete-first
rule.

The Epic 7 spine is the parent of this one. Its decisions bind here **read-only,
by their original IDs** — they are not re-derived below, and a local invariant
that weakens one is a conflict to surface, not an override. The only recorded
supersession is the gated Authentication/tenant-claims slice named in §2.

| Inherited | From parent (Epic 7 spine) | Binds here |
| --- | --- | --- |
| AD-1 Adapter-First Migration | `…/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md` | Every remaining extraction introduces or consumes a Parties-compatible adapter first, proves old/new parity, then deletes local code in a later step (I3, §4 clauses 3/4/6). |
| AD-2 Projection Platform Ownership | same | EventStore owns checkpoint/rebuild/freshness primitives; Parties owns only the compatibility mapping to `ProjectionFreshnessMetadata` / UI `StatusKind` until a public-contract story (G6) changes it (I9, I10). |
| AD-3 Crypto Placement Gate | same | Generic payload-protection engine → EventStore/shared security; Parties retains GDPR policy, party commands, key semantics, and adapters until the harness approves migration (I7, I8, I19a; 8.7). |
| AD-4 Utility Destination Discipline | same | Shared utilities go to the existing destination (Commons, Memories search-only, FrontComposer UI lifecycle) — §2 names owners per capability class, never arbitrary packages. |
| AD-5 Release Sequencing Before References | same | Land additive API in the owning submodule, validate its gates, update root gitlink/CPM, then adopt in Parties (I4, I16, §4 clause 1). |
| AD-6 Compatibility And Rollback Gates | same | Each story names a rollback (adapter switch, pointer rollback, dual-read, deferred deletion) preserving data, projection state, gateway routes, and PII redaction (I1a, I3, §4 clause 3). |
| EventStore gateway boundary | parent Inherited Invariants | No public Parties host API; traffic enters via the EventStore gateway (I1, I2). |
| Consumer self-scope and GDPR privacy | parent Inherited Invariants | Platform adoption never weakens own-data checks, erasure semantics, PII rules, or regulated copy (I6, I7, I8, I14). |
| Event-sourced projection replay | parent Inherited Invariants | At-least-once replay, duplicate tolerance, last-known fallback, degraded freshness stay mandatory (I9, I10). |
| Class A shared-anchor boundary | parent Inherited Invariants; `sprint-change-proposal-2026-06-28.md` | Shared anchors are defined once in `Hexalith.Parties.Contracts` and never re-hardcoded in a second project. **Contracts half: fully binding.** Authentication half: superseded, gated (§2). |
| Submodule governance | parent Inherited Invariants | EventStore, Commons, Memories, FrontComposer, Tenants, and Builds changes require explicit story ownership, non-recursive handling, and release sequencing (I12, §4 clause 2). |
| Convention — public contracts additive only | parent Consistency Conventions | Removing/renaming contract fields, enum values, shapes, or metadata needs an approved versioning plan (I5, I20). |
| Convention — logs and telemetry | parent Consistency Conventions | Low-cardinality and PII-free across host, UI, and MCP: no event payloads, party names, identifiers, raw key aliases, destroyed-key details, decrypted values, or unbounded claim-transformation logs (I8 covers the payload half; this row covers the rest). |

## 2. Target End-State — Domain-Module Contract

Parties conforms to the Hexalith domain-module contract: it keeps domain
substance and sheds reusable platform mechanics.

| Parties KEEPS (domain) | MOVES to platform owner |
|---|---|
| Aggregates, contracts, validators | Service defaults, correlation/ProblemDetails → Commons |
| Projection/query **semantics** (folds, tenant guardrails) | Projection/query **mechanics** (actors, rebuild, cursor codec) → EventStore SDK |
| GDPR **policy** + legal semantics | Generic crypto/key-management engine → EventStore/shared DataProtection |
| Typed domain clients, domain UI, MCP tool **definitions** | Command/query envelopes + freshness metadata → EventStore.Contracts (G6), referencing Commons paging; paging primitives → Commons (Epic 7 AD-4); MCP/CLI presentation → `Hexalith.McpCli` from decorated Parties Contracts; the prior FrontComposer/Parties MCP plumbing is obsolete migration compatibility (G11) |
| Tenant-claims **policy** (which claims Parties requires) | Tenant-claim transformation → EventStore.Authentication + Commons ULID helpers (owner decision 2026-07-16; G7/G9) |
| Domain UI **semantics** (labels, GDPR copy, flows) | Status/freshness/reconcile/grid/picker UI primitives → FrontComposer (G4) |
| Domain samples; no domain-owned AppHost in the target state | Build-root probing → Builds; reusable security/module helpers → EventStore.Aspire; canonical integrated local topology → FrontComposer.AppHost / approved platform AppHost owner; runtime deploy orchestration → platform-ops |
| Domain-specific health semantics (which projections must be current for "ready") | Degraded-response middleware and DAPR health-check plumbing → EventStore SDK / Commons ServiceDefaults (G1/G2); the Parties-local copies stay only as the `8.8-runtime-boundary-cleanup` rollback surface |

Where this table and the Epic 7 spine disagree, this table wins — provided
the divergence carries recorded SCP or owner authority, as the one on record
does. That one recorded supersession is the **Authentication / tenant-claims
slice** of the Epic 7 Class A anchor boundary — not the whole boundary. It is
approved but gated, not executed (SCP
`sprint-change-proposal-2026-07-16-g7-g9-tenant-claims-ownership.md`, G7/G9):
the tenant-claim anchors route to EventStore.Authentication, while
`Hexalith.Parties.Authentication` remains in-repo as the gated rollback
surface until `8.8-runtime-boundary-cleanup` proves parity and retires it.
The **Contracts half is not superseded**: shared anchors stay defined once in
`Hexalith.Parties.Contracts`, `PartiesClaimTypes.EventStoreTenant` remains a
compatibility alias until an intentionally versioned public-contract removal
is approved (I5, I20), and deleting the authentication implementation never
authorizes a Contracts breaking change.

**GDPR words vs. UI mechanics.** The GDPR copy Parties KEEPS is the *legal
vocabulary* — consent vs. lawful basis, restriction, cancellation vs.
permanence, export latency. FrontComposer may own layout, live-region,
download, and grid *mechanics* only. "Shared-copy consolidation" under
`8.9-frontcomposer-ui-consolidation` means the primitives; it never moves the
legal strings. Where this table's KEEPS column and a deferral's exit proof
disagree, this table wins.

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
  list above is the baseline, not a hand-editable ceiling. The deny-default
  EventStore-only tuple set binds **every** ACL that admits traffic to a
  Parties domain-service host — the local authoritative file *and* any
  environment-specific copy `external-runtime-deployment` writes in its owner
  repository; an external copy that adds an app ID, verb, or route is an I1
  route-list change and needs the same recorded approval. Fitness asserts the
  local copy; the external owner attests tuple-equality (or an approved
  delta) in that deferral's evidence field. Ratified brownfield: the only
  other ingress is the DAPR pub/sub subscription-delivery endpoint
  `POST /tenants/events` mapped by `MapEventStoreDomainEvents()` — it is
  subscription delivery, not service invocation, stays outside the tuple
  list, is not a public API, and adding, removing, or re-exposing it is an I1
  route change. On I1a retirement the authoritative path named above is
  rewritten in this invariant and in the 8.3 ledger in the same changeset as
  the deletion, and the fitness classes named in §7 I1 must open the new path
  (or a recorded identity of the owning repository) — a changeset that leaves
  a dead path here is invalid.
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
  two-front-door erasure + cross-submodule verification (D7). The erasure
  doors are a closed set of **two**: Admin (`EraseParty` with typed-name
  confirmation, `CancelPartyErasure`) and Consumer self-scoped
  (`ISelfScopedPartiesClient.RequestMyErasureAsync` → `EraseParty`,
  `CancelMyErasureAsync` → `CancelPartyErasure`). Both doors apply the same
  two aggregate commands, the same D7 verification, and the same I10
  tombstone; UI-only controls stay on their doors. **MCP has no erasure
  door:** `delete_party` is `DeactivateParty` soft-deactivation and returns
  `gdprErasurePerformed = false` — no spec may promote it into erasure or add
  an MCP erasure tool; doing so is an I7 door change recorded in the 8.3
  ledger and approved (I20) first. `ErasureCertificate` /
  `ErasureVerificationReport` field semantics (identity, tenant, status,
  destroyed key versions, cleanup completeness) are **one owned, versioned
  shape** owned by Parties.Contracts: `8.6-residual-review-debt` may only add
  tests against the current shape; `8.7-data-protection-extraction` may not
  change it except by an I5 versioning plan recorded in the 8.3 ledger and
  approved (I20) *before* G5 producer adoption; D7 verification and Memories
  cleanup certification consume that same shape.
- I8. Protected-payload compatibility: `json+pdenc-v1`, `json-redacted`, legacy
  unprotected reads, key zeroing, typed-unreadable outcomes, no-leak
  diagnostics, Art.20 exports, Art.30 processing records, erasure
  reports/certificates.

**Projection/query (survive the SDK migration)**
- I9. Replay-from-zero on every delivery; per-read-model sequence checkpoints +
  set-based idempotency; duplicate/out-of-order tolerance.
- I10. Stale/degraded reads render last-known (never throw on staleness);
  the Parties compatibility mapping `ProjectionFreshnessMetadata` (Epic 7
  AD-2) on every read; erased parties excluded from the index, and Party
  identifiers remain permanently unavailable for reuse after erasure —
  sequence/checkpoint state must preserve that tombstone under delayed or
  same-ID events. Target SDK seams: `IDomainProjectionHandler`,
  `IDomainQueryHandler`, `IReadModelStore`, `IQueryCursorCodec`, plus the
  static write-policy helper `ReadModelWritePolicy`. A full rebuild is
  executed and verified against aggregate replay before local code deletion.
  **The freshness contract is semantic and singly owned:** state vocabulary,
  per-read-model `ProjectionVersion` grammar, and the erasure-time
  `ProjectedAt` rule are one versioned shape. Owner: the EventStore.Contracts
  G6 envelope once `available`; until then the spec-8-6 three-state production
  dialect is the only legal emitter: `ProjectionFreshnessStatus` produces only
  `Current` / `Stale` / `Unavailable` (the other enum members stay for wire
  compatibility), a store outage is `Stale` carrying the
  `projection-state-store-unavailable` warning code, and the degraded flag
  rides the EventStore `QueryResponseMetadata` that Parties maps from — and a
  UI consumer (G4-B) may bind only to that named shape version. Presence of
  `ProjectionFreshnessMetadata` without a named grammar is not I10 compliance.

**Identifier, build, UI, GDPR copy, scope**
- I11. Stop rejecting valid ULID-compatible aggregate IDs; retain replay compat
  for GUID-shaped IDs; use Commons unique-ID helpers where semantics require.
  **Format acceptance is not allocation:** today's `IdentifierValidator` and,
  once the G9 additive APIs are `available`, `UniqueIdHelper.IsValidUlid` /
  `AggregateIdentity.IsValid` decide shape only; allocation of an aggregate ID
  that is tombstoned under I10 is rejected at the command handler with a typed,
  stable outcome, reading tombstone state from aggregate/event-stream state —
  never from a class-(a) read model that I10 permits to be stale — and
  helpers are never wired as the sole create-time gate. I10 wins on reuse.
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
  The legal strings that carry these distinctions have exactly one source —
  Parties (§2 "GDPR words vs. UI mechanics"); a shared FrontComposer primitive
  renders them, never defines them.
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
  who re-runs it. **Absence of the marker is not compliance:** the moment a
  retained identity moves (gitlink, catalog value, or package pin) without a
  same-changeset re-run and stamp, every parity claim recorded at the prior
  identity *is* unvalidated and the stop is in force, whether or not anyone
  wrote the marker — the marker records the state, it does not create it.
  **The stop is claim-scoped, not gitlink-scoped:** it halts
  every deletion or replacement of a rollback seam, receipt, or user-visible
  path that the unvalidated claim's original evidence covered, regardless of
  which dependency the changing spec says it relies on; "affected" is
  stamp-derived — every claim that names any identity of the moved dependency
  is affected. Art.20 export bytes (I8) and Art.20 export UX (I14) are one
  covered path.
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
  named test surfaces in the §7 map as of the Epic 8 closure commit
  `2b63ab9`, plus any surface later added to §7 through an I20-approved
  successor or an I19 class-(b) consistency test. Later edits to a §7 row's
  evidence text (such as the 2026-09-08 witness re-attributions) may correct
  *which claim* a surface witnesses; they never remove a class from the
  baseline — deleting a class name from §7 is a baseline weaken under this
  invariant. Parity evidence must enumerate the baseline surfaces it
  discharges (or name a successor test approved by an owner recorded in the
  spec, never by the authoring executor alone). Deleting or weakening a
  baseline surface while the deletion of the implementation it guards is
  pending or later relies on it — in the same changeset or across changesets —
  is invalid as parity evidence, and evidence at slice N does not certify
  later slices (I16 identity stamps apply per slice).

**Persistence class, policy ownership, approval record (added 2026-09-08 after
spine validation)**
- I19. Persistence class: every persisted store is exactly one of **(a)** an
  event-derived read model — rebuildable from zero, subject to I9/I10
  rebuild-vs-replay verification — or **(b)** an operational side-effect
  ledger — records effects outside the event stream, is never rebuilt from
  replay, is excluded from rebuild-vs-replay, is retained across rebuilds, and
  carries its own consistency tests, named in the activating spec and
  approved (I20) before they become I18 baseline surfaces. Retention across
  rebuilds is not retention across erasure: a class-(b) ledger holding
  personal data still obeys I7/I8. Every store an Epic 8 spec touches names
  its class in that spec. `PartyMemoryUnitMappingStore` is class (b): DW-81
  may place it behind an EventStore persistence seam only if that seam has no
  rebuild-from-replay semantics (so not `IReadModelStore` /
  `ReadModelWritePolicy` nor any seam a rebuild can overwrite), or the store
  is explicitly reclassified with its own parity evidence and an I20 approval.
- I19a. Policy oracle: there is exactly one writer of erasure-in-progress,
  Art.18 restriction, personal-data classification, and lawful basis — the
  Parties aggregate and GDPR policy types. `IPersonalDataPolicy` and
  `IErasureStateProvider` (8.3 G5) are read-only adapters over that writer: an
  adapter is read-only when it holds no state of its own, performs no writes,
  and answers every call from aggregate/event-stream state — never from a
  class-(a) read model that I10 permits to be stale. A shared engine that
  *decides* those facts is out of bounds; before G5 adoption the 8.3 G5 row
  must either drop those two types from the engine package or reclassify
  them as Parties-owned hooks under I2, by recorded owner decision (I20) —
  never by executor inference.
- I20. Approval record: every approval this spine names — at minimum I1
  route-list change, I1a successor AppHost owner and deferral re-approval, I2
  hook-vs-engine classification, I5 intentional versioning, I7 certificate
  versioning and erasure-door changes, I10 freshness-dialect change, I16
  identity re-validation, I17 activation handover, I18 successor test, I19
  reclassification and class-(b) test set, I19a G5 reclassification, and any
  §2 divergence from the Epic 7 spine — is decidable only as a row in the
  Story 8.3 matrix reconciliation ledger's **approval table**: decision,
  artifact, named human, date, written by that human or the reviewer gate,
  never by the executor requesting it. "Recorded in the spec" is a pointer to
  that row, never the approval itself. Until the table exists, no approval
  this spine names can be claimed and every approval-gated action is blocked.

## 4. Remaining-Work Readiness Gate (mandatory for all remaining Epic 8 work)

Each `spec-8-x` (8.6–8.10) — and any later-added deletion-heavy Epic 8 spec,
and every spec that activates an accepted closure deferral (I17) — is **not**
ready for a dev session until its spec file declares all six, in the spec
itself:

1. **Prerequisites** — which 8.3 platform APIs must be landed + owner-approved,
   which prior stories must be done, and, for every `available` surface, the
   release or root gitlink recorded in the matrix that must match the consuming
   story's actual dependency mode and identity before source changes begin.
2. **Touched repos/submodules** — Parties + each root-declared submodule the
   change edits (EventStore / Commons / FrontComposer / Memories / Tenants /
   PolymorphicSerializations / Builds) + any external owner repository (e.g.
   the `external-runtime-deployment` deployment repo) it edits. An omitted
   edited repository makes the spec incomplete.
3. **Rollback path** — which local code stays until parity, how to revert, and
   evidence the revert was **exercised once**: build + the named I18 surfaces
   green on the reverted tree *at the I16 identity the spec's parity evidence
   is stamped with*. A later spec that edits a prior slice's Parties paths
   must re-exercise or explicitly supersede every still-live rollback path it
   disturbs, recorded in both specs.
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
exactly as it bound the stories, **and binds every `deferred-work.md` ledger
item whose `source_spec`, `origin`, `authored_by_spec`, or `reason` names an
accepted closure deferral, a `spec-8-x` file, or a Story 8.x story file
(`8-x-…md`), and every item that deletes or replaces a surface that a
deferral's rollback clause or a §7 row names** — the last test is by touched
file, so a child with no parent spelled out is still bound (today DW-81/82/86,
DW-100, DW-101, DW-104, DW-111 among others). Children are not independently
schedulable: a child inherits the slot of its parent deferral; a child with
two parents takes the later slot; a child whose parent is 8.10–8.13 or
`external-runtime-deployment` (neither in the four-slot order) takes the slot
of the earliest accepted deferral whose rollback, exit proof, or §7 row names
a surface it touches, and if none does it runs after 8.9. No child may start
before its slot's predecessors have closed. Two items may execute concurrently only if their §4
clause-2 touched-repo sets (beyond Parties itself, which every set contains)
and their clause-6 parity-checklist sets are disjoint, where clause-6
disjointness is computed from the §7 invariants the *touched files* actually
guard — not from the invariants the spec author lists as relevant.

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
| I1a | Deferred | `8.8-runtime-boundary-cleanup` retains the Parties AppHost until integrated-topology, security, publish, and rollback parity exist; retirement additionally waits for `8.6-residual-review-debt` and `external-runtime-deployment` — every deferral whose exit proof or rollback names the Parties AppHost — to pass or be re-approved (I20) against the successor topology. |
| I2 | Executable + Deferred | `RetiredLeafProjectFitnessTests` guards the EventStore SDK host shape and retired leaf boundaries at source level; `EventStoreGatewayE2ETests` adds topology-gated coverage (runs fully only with Docker/DAPR available); `8.6-residual-review-debt` owns authenticated end-to-end handler-discovery proof. |
| I3 | Deferred | `8.6-residual-review-debt` (host/gateway/ACL switch-back seams), `8.7-data-protection-extraction`, `8.8-runtime-boundary-cleanup`, and `8.9-frontcomposer-ui-consolidation` retain every named local rollback path until parity is executable; `external-runtime-deployment` owns the release-recovery rollback path. |
| I4 | Executable | `PlatformApiPrerequisitesTests` verifies package/source selection separately and pins the last **authorized** identity set (signoff 2026-09-08): EventStore package `3.103.0` / source `c6efdbba6439370a5c674c12ed866c959624106a`, Commons HTTP source `6da79aed2daa4e199689331ee3196f7872c0988a`, and Builds catalog `35c3d1e5b8a55a74a440b9c2cad4c5e18747b241`. Originally resolved (2026-08-18): superproject commit `2b63ab9` landed the gitlinks and closure tests; re-reconciled 2026-09-05, 2026-09-06, 2026-09-08. **Open condition (2026-09-08):** HEAD gitlink `references/Hexalith.Builds` is `a32cb422749352cce8dec948aa3e78c8f00eb4cf` (superproject commit `bf8daf98`), past the authorized pin with no signoff, test-constant, or matrix `unvalidated` marker — under I16 this is a stop on every Builds-relying deletion until `a32cb422` is re-validated and stamped in the test constant, matrix, and signoff. Restoring `35c3d1e5` is not a remedy: the only diff is `HexalithFrontComposerVersion` `4.3.0 → 4.4.0`, and the test, matrix, and signoff already assert `4.4.0`, so the authorized Builds pin and the authorized FrontComposer catalog value are mutually inconsistent until the re-stamp lands. The 8.3 matrix and `.gitlink-signoff.tsv` remain the durable record; this row is a snapshot of the authorized set, never of an unstamped HEAD. |
| I5 | Executable | `ContractsPublicApiSnapshotTests`, `ClientPackageTests`, `PartyPickerPackagingTests`, `AdminPortalPackagingTests`, and `ConsumerPortalPackagingTests` preserve the public package surface. |
| I6 | Executable + Deferred | `EventStoreGatewayRoutingTests`, `HttpPartiesQueryClientTests`, and `SelfScopedPartiesClientTests` preserve command/query behavior and self-scope; `8.8-runtime-boundary-cleanup` owns future shared-helper adoption. |
| I7 | Executable + Deferred | `PartyAggregateConsentTests`, `PartyAggregateErasureTests`, and `ErasureVerificationServiceTests` preserve consent, restriction, and erasure behavior; `8.6-residual-review-debt` owns the residual erasure-certificate identity/status validation and Memories cleanup-race review debt. |
| I8 | Executable + Deferred | `CryptoKeyManagementCompatibilityHarnessTests`, `AdminPortalGdprPrivacyGuardrailTests`, and `ErasureVerificationServiceTests` preserve retained security and no-leak behavior; `8.7-data-protection-extraction` owns future shared-engine extraction. |
| I9 | Executable + Deferred | `PartySdkProjectionHandlerTests` guards projection replay, checkpoint, idempotency, duplicate, and out-of-order behavior; `8.6-residual-review-debt` owns residual projection quality debt (unbounded Art.30 read model, null-dictionary recovery). |
| I10 | Executable + Deferred | `PartySdkQueryHandlerTests` and `ProjectionFreshnessAndDegradationTests` guard rebuild, freshness, stale-read, erased-index, and tombstone behavior; `8.6-residual-review-debt` owns the open freshness-mapping and search-input-bounds debt. |
| I11 | Executable + Deferred | `IdentifierHygieneFitnessTests` bans GUID-parser regressions; `IdentifierValidatorTests` preserves both ULID-compatible acceptance and GUID-shaped replay acceptance; `PartyAggregateCompositeTests` exercises ULID party IDs through the aggregate; `8.8-runtime-boundary-cleanup` owns future Commons-helper adoption and the command-handler tombstone-allocation refusal. |
| I12 | Executable | `DocumentationFitnessTests` and `PartiesContainerPublishWorkflowTests` pin the runnable lanes and publication contract; warning, restore, Release build, package, consumer, typecheck, and Playwright receipts remain mandatory closure gates (the always-on CI Playwright a11y lane is a separate open ledger item). |
| I13 | Deferred (parity not yet discharged) | `MainLayoutAccessibilityTests` (asserting the FrontComposer shell slice — skip links and landmarks — adopted 2026-08-18 under the 2026-08-19 backfill SCP) and `PartiesAccessibilitySpecimenTests` guard the retained UI, but I13 parity is NOT discharged: the app-owned focus-visible, forced-colors, and reduced-motion rules are now scoped under `.parties-main-content` (isolation repair) but are still verified only by source greps, not observed at runtime (DW-111), and the Playwright receipt focuses only the shell skip link, never a content control; residual FAST/v4 tokens remain in retained RCL CSS. `8.9-frontcomposer-ui-consolidation` owns the remaining shared-primitive slices, the token purge, and this open runtime-observation gap. |
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
  can observe. Owner: whichever story changes a retained identity. Open I16
  items at 2026-09-08: the Builds gitlink condition recorded in the I4 row, and
  a FrontComposer stamp split — `PlatformApiPrerequisitesTests` and the HEAD
  gitlink stand at `a0acb78f…` while `EpicEightClosureFitnessTests` and the
  Playwright a11y receipt are stamped `f0c3b6fd…`; the I12/I13 receipts are
  therefore at a superseded identity until the lane is re-run at `a0acb78f…`
  or the receipt is marked unvalidated in the 8.3 matrix.
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

### 7b. Disposition of I19–I20 — added 2026-09-08

- **I19 (persistence class) — process gate until first activation.** No store
  is reclassified today; `PartyMemoryUnitMappingStore` remains the DAPR-backed
  operational ledger it is. The first spec that touches any store (DW-81 in
  `8.6-residual-review-debt`, or 8.7) names each store's class and, for class
  (b), adds the consistency tests that become its I18 baseline surface. Owner:
  the activating spec author and the reviewer gate.
- **I19a (policy oracle) — blocked on the 8.3 G5 row.** G5 is still
  `needs-additive-api`; the row must be amended (drop or reclassify
  `IPersonalDataPolicy` / `IErasureStateProvider`) with an I20 row before 8.7
  may adopt the engine. Existing `PartyAggregateConsentTests` and
  `PartyAggregateErasureTests` already pin the aggregate as writer. Owner:
  8.7 spec author + EventStore G5 owner.
- **I20 (approval record) — process gate, not executable.** The approval
  table does not yet exist in the 8.3 matrix reconciliation ledger; creating
  it (empty, with the eight decision rows named in I20) is the first
  companion follow-up of this amendment. Until it exists, no approval those
  invariants name can be claimed. Owner: the reviewer gate.

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

## 9. Deferred — intentionally not decided here (2026-09-08)

Each row names the artifact that owns the decision and the condition that
re-opens it. A row here is a scheduled decision, not permission to diverge.

| Deferred decision | Owned by | Revisit when |
| --- | --- | --- |
| Production key-backend / KMS choice and key-ring topology (Parties `DaprXmlRepository` vs G5 KMS vs successor AppHost statestore) | `8.7-data-protection-extraction` spec, `external-runtime-deployment` | First 8.7 activation spec is authored; must declare the backend and the I8 `pdenc` readability window before any crypto deletion. |
| Infra / provider strategy and environment topology beyond local Aspire | `external-runtime-deployment` | Its activation spec; the spine binds only I1 tuples and I1a parity, not the provider. |
| Key-ring, cursor, and DataProtection payload continuity across the AppHost cutover (I1a parity list) | **One decision** recorded once in the 8.3 ledger (I20) and binding both `8.7-data-protection-extraction` (key backend) and `8.8-runtime-boundary-cleanup` (successor topology) | Whichever of the two activation specs is authored first: either prove the successor unprotects predecessor payloads (cursor purposes included) or declare the invalidation window and the user-visible `InvalidCursor` / typed-unreadable behavior; the second spec inherits that row. |
| FAST/v4 token purge and a token-purity guard for retained RCL CSS | `8.9-frontcomposer-ui-consolidation` | Its activation spec; I13 stays Deferred until the purge is executable. |
| Fluent UI Blazor V5 is a release candidate (`5.0.0-rc.5-26219.1`) | Builds catalog, I13 | V5 GA: re-pin through the catalog; I13 rules do not change. |
| `EventStoreGatewayE2ETests` is a silent no-op without Docker/DAPR | `8.6-residual-review-debt` | Its topology proof: convert to an explicit skip or run in the topology lane. |
| Epic 7 parent Stack table (SDK `10.0.302`, Dapr `1.18.4`, Aspire `13.4.6`, Fluent RC3) is behind live pins (`10.0.400` / `1.18.5` / `13.5.3` / RC5) | Epic 7 spine (parent) | An Epic 7 spine update; this child does not override the parent's seed, and I12 deliberately names no patch pins. |
| 8.3 matrix G5 cell still names a superseded "retained" EventStore/Builds pair | Story 8.3 matrix owners | Next matrix reconciliation; the 8.10 table on the same page is authoritative meanwhile. |

## 10. Validation & Amendment Record — 2026-09-08

The reviewer gate (deterministic lint plus rubric-walker, reality-check,
adversarial, closure-evidence, and an ad-hoc parent-inheritance lens) validated
the 2026-08-18 spine on 2026-09-08 with a **conditional fail** — 37 deduped
findings, consolidated in `reviews/validation-report-2026-09-08.md`. The
2026-08-18 criticals stayed closed; the fail rested on a tree condition (V27)
and a §5 hole (V28). Amendments applied the same day: I19, I19a, I20 added;
I1 (external ACL copies, pub/sub door ratified), I7 (enumerated doors,
certificate shape), I10 (single freshness grammar), I11 (format ≠
allocation), I14 (single string source), I16 (claim-scoped stop) tightened;
§2 Class A label narrowed to the Authentication slice with the Contracts
carve-out restated and the GDPR words-vs-mechanics rule added; §4 clause 2
completed (Memories, Tenants) and clause 3 requires an exercised rollback; §5
binds ledger children to the order; §7 I1a/I4/I11/I13 rows corrected; §1a
Design Paradigm + Inherited Invariants and §9 Deferred added. A post-amendment
gate pass (rubric, reality-check, adversarial) on the new text caught and
corrected the same day: an I7 door enumeration that had wrongly promoted MCP
`delete_party` (soft-deactivation, `gdprErasurePerformed = false`) into an
erasure door — I7 now fixes the door count at two and forbids an MCP erasure
door; I11 naming G9 helpers as if present; the I10 degraded-flag carrier; I16
firing only on a written marker (now: identity movement itself is the
unvalidated state); I18 silent on post-closure §7 edits; §5 slot rules for
children of 8.10–8.13 / `external-runtime-deployment` / two parents; §4
clause 2 missing PolymorphicSerializations; I19 class-(b) tests and erasure
retention; I19a "read-only" made decidable; I20 row set widened and
executor-writing forbidden; a §2 owner row for G1/G2 health plumbing. The V27
tree condition is **not** spine-fixable and stays open in the I4 row and
frontmatter; the gate also established that restoring `35c3d1e5` is not a
remedy (tests/matrix/signoff already assert FrontComposer `4.4.0`), so the
only coherent exit is re-validating and stamping `a32cb422`. Findings deferred
with revisit conditions are in §9 and this folder's `.memlog.md`; known
residuals accepted with reasons (I10 `ProjectionVersion` grammar waits on
G6/DW-25; I20 table creation is a companion follow-up) are in the memlog.
