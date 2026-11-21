---
title: Epic 8 Architecture Spine (Reconciliation)
epic: 8
date: 2026-07-07
updated: 2026-11-21
status: final
amendment: 2026-11-21 release selection, host cutover, erasure-safe reads and delivery, custody, replay and deferral sequencing
classification: post-MVP maintenance (Class C) — zero new PRD FRs
closes-blocker: "Story 8.1 preserved 'missing Epic 8 architecture spine' blocker"
open-condition: "Stories 8.7–8.9 remain blocked. GATE-KMS and the end-to-end erasure and replica-revocation actions remain open. Round 10 closure evidence applies to its recorded source."
related:
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-11-20.md
  - docs/architecture.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-06.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-16-g7-g9-tenant-claims-ownership.md
  - _bmad-output/planning-artifacts/implementation-readiness-report-2026-07-07.md
  - _bmad-output/implementation-artifacts/epic-8-context.md
  - _bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md
  - _bmad-output/planning-artifacts/architecture/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md
---

# Epic 8 Architecture Spine — Domain-Focus Refactoring & Platform Extraction

**Approved McpCli course correction (2026-09-27):** `Hexalith.McpCli` is the target Hexalith-owned CLI/MCP surface for Parties operations that pass contract enrollment and authorization. Any proprietary module CLI, MCP host, plug-in, or planned adapter described below is an obsolete migration source or historical design, not a new target. The module retains its domain, UI, and security semantics; replacement or approved withdrawal and parity evidence precede retirement. External development CLIs are unaffected.


## Release selection and open gates · 2026-11-21

Parties revision `42b27f2e` records Platform **v2.2.4** (`a74abe17`) and the same Builds **v6.4.3** catalog (`dc225a69`) that this Platform release selects. The EventStore source gitlink is `7435254c`. The superproject tree records all eleven source identities. Evaluate package versions for the consuming project and build mode under I4. [Gitlink signoffs](../../../../.gitlink-signoff.tsv) · [Central package selection](../../../../Directory.Packages.props)

| Decision for the next story | Evidence and owner |
| --- | --- |
| Preserve the release selection when preparing a private Development catalog. | In Release package mode the Parties host and gateway test host select EventStore `4.2.0`. The attached lifecycle qualification suite selects `5.4.0`. Each selection governs its own build. See I4 and I16. |
| Keep the Parties AppHost, crypto adapters and UI rollback surfaces. | The [sprint tracker](../../../implementation-artifacts/sprint-status.yaml) records Stories 8.7, 8.8 and 8.9 as blocked. The [November 20 decision](../../sprint-change-proposal-2026-11-20.md) assigns production custody and end-to-end erasure evidence to Parties and the deployment owners. |
| Use closure receipts only for their recorded revision. | Story 8.10 and Epic 8 closed at `c7dd607b` with six passing Round 10 lanes. Subsequent release qualification uses evidence from the selected source and packages. [Closure receipts](../../../implementation-artifacts/tests/test-summary.md) · [Product qualification](../../../../docs/ci.md) |

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
- [Epic 8 context](../../../implementation-artifacts/epic-8-context.md) — original
  goal, story scope and cross-story dependencies. For current target framework,
  projection replay and last-known UI behavior, follow I12, I9 and I10/I14 here.
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
that weakens one requires parent authority. Section 2 identifies the selected
destinations and the outstanding approval for the tenant-claims and McpCli changes.

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
| Event-sourced projection replay | parent Inherited Invariants | Preserve at-least-once replay, duplicate tolerance, safe last-known fallback and degraded freshness under I9/I10. |
| Class A shared-anchor boundary | parent Inherited Invariants; `sprint-change-proposal-2026-06-28.md` | Define shared anchors once in `Hexalith.Parties.Contracts`. Retain the Authentication rollback surface pending the §2 approval and parity gates. |
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
| Parties readiness policy for Dapr and Tenants, plus diagnostic health for read models, pub/sub and Memories (I1a) | Degraded-response middleware and Dapr health-check plumbing → EventStore SDK / Commons ServiceDefaults (G1/G2). The Parties-local copies provide the `8.8-runtime-boundary-cleanup` rollback surface. |

The July 16 G7/G9 decision routes tenant-claim transformation to
EventStore.Authentication and identifier helpers to Commons. The September 27
course correction routes MCP/CLI presentation to McpCli. The I20 approval row
for these divergences from Epic 7 is pending. Keep `Hexalith.Parties.Authentication`
as the rollback surface until the row records parent authority and
`8.8-runtime-boundary-cleanup` proves consumer parity and exercises rollback.
Any MCP/CLI retirement also requires the approved replacement or withdrawal
for the affected surface. Shared anchors remain in `Hexalith.Parties.Contracts`.
Removal of the `PartiesClaimTypes.EventStoreTenant` compatibility alias
requires its own I5/I20 versioning approval.
[G7/G9 routing decision](../../sprint-change-proposal-2026-07-16-g7-g9-tenant-claims-ownership.md) · [Story 8.3 approval table](../../../implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md#i20-approval-table--reconciled-2026-10-05)

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
  the [declaration check](../../../../scripts/render-platform-declaration.py) verifies its
  global and caller-policy deny defaults, trust domains, caller app ID and
  namespace, and each operation’s exact path, HTTP verb and action. The
  external deployment owner must compare the effective ACL one grant at a time
  with that canonical policy at the selected revision. Record both identities,
  defaults and grants in the deployment deferral’s evidence field. Any
  difference requires an I1 owner decision in the Story 8.3 I20 ledger before
  use. In the isolated mTLS test profile,
  record the canonical caller and the exercised `eventstore-<suffix>` caller,
  session namespace and `public` trust domain. Check the same deny-default
  verbs and routes under that mapping. Deployment ACL changes require their
  own I1 approval. [Test identity mapping](../../../../src/Hexalith.Parties.AppHost/DaprMtlsBootstrap.cs)
  The other ingress is the DAPR pub/sub subscription-delivery endpoint
  `POST /tenants/events` mapped by `MapEventStoreDomainEvents()` — it is
  subscription delivery, stays outside the service-invocation grant
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
  On the successor receiver, exercise authenticated Dapr application-channel
  admission for discovery, actor callbacks and Tenants delivery. Canonical
  gateway operations additionally require a workload assertion for the receiving
  service and exact operation. Record acceptance and refusal for each channel,
  including absent or invalid channel credentials and assertions with the wrong
  caller, service or operation. [Host registration](../../../../src/Hexalith.Parties/Program.cs)
  Exercise `/ready` with Dapr sidecar and state storage healthy and with each
  unavailable. Disabled Tenants integration contributes a healthy result.
  Enabled Tenants must be configured and ready for readiness to pass.
  With those dependencies healthy, read-model, pub/sub or Memories degradation
  must appear on `/health` while `/ready` stays healthy.
  [Health registrations](../../../../src/Hexalith.Parties/HealthChecks/PartiesHealthCheckExtensions.cs) · [Tenants readiness](../../../../src/Hexalith.Parties/HealthChecks/TenantsIntegrationHealthCheck.cs)
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
  consumption evidence. For a source claim, compare the actual checkout with the
  superproject gitlink and record the revision exercised. Only a clean, matching
  checkout can support evidence for the pinned source. For each EventStore
  package consumer, record the evaluated `HexalithEventStoreVersion`.
  A caller-supplied version takes precedence over catalog defaults. When
  preparing the private Development catalog, pass the gateway test host's
  evaluated version to the catalog reader and build it with package references.
  Bind the reader to the prepared owner and adapter assemblies and their app IDs.
  Reject differing binaries with the same filename or restored EventStore packages
  outside the evaluated version. Release builds check resolved EventStore
  NuGet references against the selected version.
  [Package guards](../../../../Directory.Build.targets) · [Catalog reader](../../../../src/Hexalith.Parties.AppHost/PartiesDevelopmentCatalogTool.cs)

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
  The first committed request for an active party starts a pending erasure.
  Repeated requests preserve its committed state. Both doors derive terminal
  status from committed lifecycle events and the persisted verification record.
  Parties and deployment owners must identify the durable, idempotent consumer,
  name the retry owner and monitor pending requests against the owner’s operating
  threshold. Qualification covers cancellation before destruction, failed event
  append, restart and redelivery through cleanup and redacted reads.
  A subscriber may acknowledge completed work, a verified completed duplicate,
  an event explicitly outside its obligations, or a durable handoff that records
  the operation identity and retry owner. A transient failure before completion
  or handoff must request redelivery. The owner reconciles exhausted deliveries.
  This applies to the initial erasure request and downstream `PartyErased`
  cleanup. Only the committed lifecycle and verification record establish terminal status.
  [Erasure qualification and ownership](../../sprint-change-proposal-2026-11-20.md)
  Profile erasure ends current human-actor eligibility immediately. A past
  action may be attributed only through a separately authorized, tenant- and
  party-scoped historical query under I8. It conveys no current authority.
- I8. Protected-payload compatibility: `json+pdenc-v1`, `json-redacted`, legacy
  unprotected reads, key zeroing, typed-unreadable outcomes, no-leak
  diagnostics, Art.20 exports, Art.30 processing records, erasure
  reports/certificates.
  Before handling real regulated EU personal data, the host must refuse
  ephemeral key storage outside Development. The deployment owner must prove
  that a synthetic protected record decrypts and replays after host replacement
  and on an independent replica using the deployed per-party `IKeyStorageBackend`. After
  certified destruction, replicas must refuse retired keys, including versioned
  reads and cache fills overlapping destruction. The provider and deployed
  worker still require deployment-owner evidence. [GATE-KMS](../../parties-ui-prd.md#deployment-gates) · [Revocation proof](../../sprint-change-proposal-2026-11-20.md)
  Human-actor attribution history requires custody independent of the profile.
  Under `party-actor-retention-v1`, access expires at `ValidFrom + 365 fixed days`,
  measured from binding effectiveness. Deny release at and after that instant,
  even during cleanup. Restore must check fresh, durable lifecycle evidence
  before decryption and honor the original expiry and destruction. Production
  use remains disabled pending qualified custody, all-copy cleanup and restore
  evidence, and owner compatibility acceptance. [Actor-history retention policy v1](../../actor-history-retention-policy-v1.md)

**Projection/query (survive the SDK migration)**
- I9. Live delivery resumes from each read model’s persisted checkpoint.
  Order the delivered events by sequence, tolerate equivalent duplicates and
  fold the contiguous tail once. Conflicts or uncovered gaps cannot advance
  the checkpoint. Recover missing historical state only from verified history
  or require a rebuild. A full rebuild folds the complete history into an empty
  candidate. For the same history, require equivalent domain state and per-party
  sequence and erasure checkpoints. Reject a candidate that would roll back
  newer state or lose an erasure tombstone. The shared index version is a
  diagnostic hint and can restart on rebuild.
  [Detail fold and rebuild](../../../../src/Hexalith.Parties.Projections/Handlers/PartyDetailSdkProjectionHandler.cs) · [Index checkpoints](../../../../src/Hexalith.Parties.Projections/Handlers/PartyIndexSdkProjectionHandler.cs)
- I10. Query handlers apply the Parties `ProjectionFreshnessMetadata` mapping
  (Epic 7 AD-2) to admitted reads. If canonical storage is unavailable, evict
  cached active detail and nonempty or unknown indexes and fail the read.
  Cached erased detail must be redacted again. Absent detail retains the
  query’s absence outcome. Only a confirmed empty index with intact tracking
  state can be served from cache. Export and Article 30 queries must admit the
  party detail before using any processing-record fallback. Clients must honor
  a denied read even when they hold an older copy. For list and search, an
  admitted empty page replaces the prior page. The sole reuse case is explicit
  server confirmation that the same authorized representation is unchanged.
  Match identity, tenant, operation, query, filters, sort, page and representation
  validator. A degradation reason or stale age alone cannot authorize reuse.
  Erased parties stay out of the index, and their IDs
  stay unavailable for reuse under delayed or same-ID events.
  [Query admission](../../../../src/Hexalith.Parties/Queries/PartySdkQueryService.cs) · [Cross-replica outage cases](../../../../tests/Hexalith.Parties.Tests/Gateway/PartySdkCrossInstanceErasureTests.cs)
  Target SDK seams: `IDomainProjectionHandler`,
  `IDomainQueryHandler`, `IReadModelStore`, `IQueryCursorCodec`, plus the
  static write-policy helper `ReadModelWritePolicy`. A full rebuild is
  executed and verified against aggregate replay before local code deletion.
  **The freshness contract is semantic and singly owned:** state vocabulary,
  per-read-model `ProjectionVersion` grammar, and the erasure-time
  `ProjectedAt` rule are one versioned shape. Owner: the EventStore.Contracts
  G6 envelope once `available`; until then the spec-8-6 three-state production
  dialect is the only legal emitter: `ProjectionFreshnessStatus` produces only
  `Current` / `Stale` / `Unavailable` (the other enum members stay for wire
  compatibility), an admitted degraded result is `Stale` carrying the
  `projection-state-store-unavailable` warning code, and the degraded flag
  rides the EventStore `QueryResponseMetadata` that Parties maps from — and a
  UI consumer (G4-B) may bind only to that named shape version. Presence of
  `ProjectionFreshnessMetadata` without a named grammar is not I10 compliance.

**Identifier, build, UI, GDPR copy, scope**
- I11. Preserve ULID-compatible IDs and GUID-shaped legacy replay.
  `PartyIdentifier.IsValid` checks the format. Allocation uses aggregate
  creation evidence (`HasBeenCreated` or the legacy party type). After
  format and provisioning checks, an ordinary create retry against an
  already-created party emits no events, including after erasure. The simple
  handler returns `NoOp` and the composite handler returns an empty result.
  G9 helper adoption must preserve this behavior and I10’s permanent ban on
  reuse. A distinct tombstone rejection requires an explicit compatibility
  decision and tests for both commands. [Create handlers](../../../../src/Hexalith.Parties/Domain/PartyAggregate.cs)
- I12. Build Parties for `net11.0` using the SDK selected by `global.json`
  (`11.0.100`, latest patch roll-forward). Use `.slnx`, CPM, warnings-as-errors,
  xUnit v3 / Shouldly / NSubstitute / bUnit, Playwright a11y for UI changes,
  direct submodules only, and MinVer.
- I13. UI: Fluent 2 inheritance; purge FAST/v4 tokens; teal accent non-text
  only, filled actions bind AA-safe brand background; WCAG 2.2 AA contracts
  (keyboard/pointer parity, skip links, focus rings, forced-colors,
  reduced-motion, semantic controls, typed destructive confirmation,
  polite/assertive live-region split, no focus-stealing on optimistic updates).
- I14. GDPR copy honesty: no consent dark patterns, no over-promised export
  latency, cancellation-vs-permanence distinction. For a denied read, clear
  previously displayed personal data and show the query’s unavailable or
  absent outcome. For an admitted empty page, replace prior rows, counts and
  paging with the admitted result and clear detail from the superseded result.
  Preserve rows only on I10’s explicit confirmation of an unchanged representation.
  Story 8.9 must cover denial, an authoritative empty result (including a changed
  query with degradation metadata), and confirmed unchanged results in bUnit
  for both list and search. The existing degraded-empty search test expects the
  old row to remain. UI parity stays open until the component and tests implement
  the rule and any I18 successor approval is recorded.
  [Component](../../../../src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor) · [Component tests](../../../../tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs)
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
- I19. Each store of projection or operational records is either **(a)** an
  event-derived read model — rebuildable from zero, subject to I9/I10
  rebuild-vs-replay verification — or **(b)** an operational side-effect
  ledger — records effects outside the event stream, is never rebuilt from
  replay, is excluded from rebuild-vs-replay, is retained across rebuilds, and
  carries its own consistency tests, named in the activating spec and
  approved (I20) before they become I18 baseline surfaces. Retention across
  rebuilds is not retention across erasure: a class-(b) ledger holding
  personal data still obeys I7/I8. Each activating spec names the class of
  every such store it touches. Treat `PartyMemoryUnitMappingStore` as class (b).
  DW-81 shipped conditional reads, writes and deletes through the EventStore
  SDK’s `IReadModelStore`. Projection rebuilds must never replace or purge
  its mapping keys. Cleanup must preserve outstanding obligations across
  conflicts and retries. Before a migration relies on this interface, the
  Parties and EventStore owners must record in I20 whether it satisfies the
  ledger contract or requires a dedicated interface, and approve tests for
  rebuild exclusion, concurrent updates and exact cleanup. The row is pending.
  [Mapping store](../../../../src/Hexalith.Parties/Search/PartyMemoryUnitMappingStore.cs) · [DW-81 evidence](../../../implementation-artifacts/deferred-work-archive.md#dw-81-move-the-memories-mapping-ledger-behind-eventstore-persistence)
  The per-party key provider is authoritative for key versions, wrapping
  metadata and destruction. Projection replay must never recreate, replace or
  delete custody state. Before provider migration, Parties security/privacy
  and the deployment/key-storage owner must record which I19 rules apply and
  which tests qualify the provider in the I20 ledger. I8 and GATE-KMS govern
  restart, independent replica and revocation proof. That decision remains open.
  [Custody contract](../../../../src/Hexalith.Parties.Contracts/Security/IKeyStorageBackend.cs)
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
  reclassification, class-(b) test set and custody disposition, I19a G5 reclassification, and any
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
3. **Rollback path** — name the retained code, describe how to revert and
   exercise the revert once. Build and run the named I18 checks on the
   reverted tree. For an adapter switch at fixed dependencies, use the same
   I16 dependency identities for forward and rollback runs. For a package or
   gitlink rollback, record both the forward and restored identities and stamp
   each run with the versions or SHAs it exercised. The forward deletion gate
   still requires parity at the identity selected for merge (I16).
   A later spec that edits a prior slice's Parties paths
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
deferral's rollback clause, exit proof or a §7 row names** — the last test is by touched
file. Assign each bound item a slot before work starts.

- A child inherits its declared parent’s slot. For a parent in 8.10–8.13 or
  `external-runtime-deployment`, use the earliest accepted 8.6–8.9 deferral
  whose rollback, exit proof or §7 evidence names a touched surface. With no
  match, use the slot after 8.9. For multiple declared parents, take the latest
  of their individually resolved slots.
- For a child with no declared parent, list **all** accepted 8.6–8.9 deferrals
  whose rollback, exit proof or §7 evidence names a touched surface. Take the
  latest matching slot so every affected predecessor has closed. If none
  matches, run after 8.9. Record the surfaces, matches and slot in the activating
  spec. The recorded deferral owners resolve unclear mappings before work starts.

No child may start before its slot’s predecessors have closed. Two items may execute concurrently only if their §4
clause-2 touched-repo sets (beyond Parties itself, which every set contains)
and their clause-6 parity-checklist sets are disjoint, where clause-6
disjointness is computed from the §7 invariants the *touched files* actually
guard — not from the invariants the spec author lists as relevant.

## 6. Blocker Closure

**Epic 8 architecture spine — APPROVED (reconciled), 2026-07-07.** Story 8.1's
preserved "missing Epic 8 architecture spine" blocker is **CLOSED for planning
purposes**. Remaining deletion-heavy migrations (8.6–8.10) are henceforth gated
by §4 (per-spec readiness gate) rather than by the absence of this document.

## 7. Story 8.10 Closure Evidence Map — 2026-10-05 (baseline 2026-08-18)

**Map reconciliation at 2026-10-26:** DW-96 is complete; the four
remaining accepted open umbrellas stay unchanged. I2/I7/I9/I10 now identify
executable guards for retained behavior, with the passing package and configured
runtime receipts linked below. Migration parity remains separately gated.
DW-50 writer drain and historical reconciliation, DW-82, DW-168 and independent
migration, publication and deployment obligations remain open; every retained
rollback seam remains in place. Dated identity and approval receipts are unchanged.

Read the following map at the closure revision. “Executable” means a named
automated test guarded the retained implementation at that revision;
“deferred” means the current path remains the rollback surface and the listed deferral owned the exit proof at closure. Commit `0f261ccd`
retired the story-status and receipt tests, including `EpicEightClosureFitnessTests`
and `PlatformApiPrerequisitesTests`. Their names preserve the I18 baseline.
CAP-1 records the committed August baseline at `2b63ab9` and the six final
Round 10 receipts at `c7dd607b`. Before a new deletion
relies on a retired witness, the story owner must identify an executable
product check covering the same behavior, obtain I20 approval for the I18
successor and run it at the selected identity. [Epic 8 SPEC](../../../specs/spec-epic-8-domain-focus/SPEC.md)

<!-- epic-8-invariant-map:start -->
| Invariant | Disposition | Executable evidence or accepted deferral |
| --- | --- | --- |
| I1 | Executable + Deferred | `DocumentationFitnessTests` and `ArchitecturalFitnessTests` verify the static deny-default EventStore-only SDK route contract and absence of retired in-repo deployment assets; `PartiesMtlsTopologyTests` executes actual permitted calls and Dapr caller/verb/path denials in the configured isolated topology. DW-96 runtime ACL proof is complete; `external-runtime-deployment` retains environment orchestration and deployment qualification. |
| I1a | Deferred | `8.8-runtime-boundary-cleanup` retains the Parties AppHost until integrated-topology, security, publish, and rollback parity exist. Retirement additionally requires requalifying completed DW-96 (`8.6-residual-review-debt`) host/gateway/ACL proof against the successor topology under I20 and the open `8.8-runtime-boundary-cleanup` / `external-runtime-deployment` owners. Every deferral whose exit proof or rollback names the Parties AppHost must pass or be re-approved (I20) against that successor; successor-topology parity remains required. |
| I2 | Executable | `RetiredLeafProjectFitnessTests` guards the retained EventStore SDK host shape and retired leaf boundaries; `EventStoreGatewayE2ETests` executes authenticated handler discovery, exact projected detail and persisted event-envelope checks in the explicitly configured isolated topology. DW-96 closure receipts are in `_bmad-output/implementation-artifacts/tests/five-published-sdk-and-runtime-closure-2026-10-26/`; original-name coexistence and independent deployment gates remain separate. |
| I3 | Deferred | `8.8-runtime-boundary-cleanup` retains the host/gateway/ACL switch-back seams after DW-96 proof completion; together with `8.7-data-protection-extraction` and `8.9-frontcomposer-ui-consolidation`, it retains every named local rollback path until parity is executable. `external-runtime-deployment` retains the release-recovery rollback path and successor-topology requalification. All existing retirement, I20 approval and parity gates remain required. |
| I4 | Executable | `PlatformApiPrerequisitesTests` proves committed root gitlinks via `git ls-tree HEAD` plus clean matching checkouts and package/source graphs separately. The then-selected source/package set was approved by Administrator / jpiquot in the 2026-11-02 DW-182 I20 rows (Tenants successor explicitly confirmed at the spec 8.10 Round 9 review); the 2026-11-01 I20 row is dated history, and selection does not revalidate parity. The dated 2026-10-05 set (8.3 I20 rows for decisions 1 and 2) recorded separately: EventStore package `3.113.0` (Parties pre-import CPM pin) and source `865cd9e49273dffbb1cdae85efeaf1aac322e09e` (tag `v3.113.0`, the same release); Builds `360a2b9c4e96809365a7de785be9a68152d5ac28`; Tenants `72b8e4f508176b69826549e87b7b2a286f607fd1` / package `5.7.0`; Commons HTTP `116d26815eb81e35b3c161e1799e5ee12805fc0a` / package fallback `2.30.1`; FrontComposer `2cc8dd3a3ac76c03f5ea6f6f92e65829306db470` / package `4.5.0`; Memories `5b43fe2f8a0f04dc021921a077dff1a573c2ce5e` / package `2.27.1`; PolymorphicSerializations `98de6e013840ece9f0fa7c68ab7dcdf2bba3b375`; AI.Tools `3f194e17174994d308ec84af9ee2b5aa68674d0d`. Every root gitlink in the final set is committed: EventStore `865cd9e49273dffbb1cdae85efeaf1aac322e09e` in Parties `75b4fa1fe2cfd2167c186e94faf52e18be4b0ff6`, the other seven were already committed at Parties `47e2da3244fd7d6d14e39bc30513b23504e9bc27`, so `git ls-tree HEAD references/` records all eight and the committed-tree guard is green. Dated history: never-approved committed EventStore `547c938d52e4fd31b47783b9dd032528a5100549` / `0dc44e46ccb7f56c3182b855c21f337173a1307f` and Tenants `b55f96d89b839fd443ac589835880102add46d64`; superseded, never-committed 2026-10-04 approvals of EventStore `2242ad55a1b678828df8aa093fd92399c29af5bf` and Tenants `04e655cf070b17eced9daefb9eaa87a09ec60e81`; the superseded 2026-10-04 Builds `145ae921d9032f110b7371614939559e0aee202a`, three-pin, and Round 6 selections. Source presence/compile and selection signoff do not certify parity. |
| I5 | Executable | `ContractsPublicApiSnapshotTests`, `ClientPackageTests`, `PartyPickerPackagingTests`, `AdminPortalPackagingTests`, and `ConsumerPortalPackagingTests` preserve the public package surface. |
| I6 | Executable + Deferred | `EventStoreGatewayRoutingTests`, `HttpPartiesQueryClientTests`, and `SelfScopedPartiesClientTests` preserve command/query behavior and self-scope; `8.8-runtime-boundary-cleanup` owns future shared-helper adoption. |
| I7 | Executable | `PartyAggregateConsentTests`, `PartyAggregateErasureTests`, and `ErasureVerificationServiceTests` guard retained consent, restriction, certificate and erasure behavior. Story 8.6 residual review is resolved under DW-96; DW-82 authoritative inventory and DW-168 live physical-erasure qualification remain open, alongside shared-provider migration, publication and deployment gates. |
| I8 | Executable + Deferred | `CryptoKeyManagementCompatibilityHarnessTests`, `AdminPortalGdprPrivacyGuardrailTests`, and `ErasureVerificationServiceTests` preserve retained security and no-leak behavior; `8.7-data-protection-extraction` owns future shared-engine extraction. |
| I9 | Executable | `PartySdkProjectionHandlerTests` and `PartyDetailSdkProjectionRecoveryTests` guard retained replay, checkpoints, idempotency, duplicates, ordering and actual published-SDK recovery of historical bytes. DW-96 residual review closure supersedes its scalability follow-up to still-open DW-50: deployed writer drain, historical private recovery reconciliation and independent migration/publication/deployment gates remain required. |
| I10 | Executable | `PartySdkQueryHandlerTests` and `ProjectionFreshnessAndDegradationTests` guard retained rebuild, freshness mapping, search bounds, stale reads, erased indexes, tombstones and historical wire compatibility. DW-96 residual review is resolved; DW-82, DW-168 and independent migration/publication/deployment qualification remain open. |
| I11 | Executable + Deferred | `IdentifierHygieneFitnessTests` bans GUID-parser regressions; `IdentifierValidatorTests` preserves both ULID-compatible acceptance and GUID-shaped replay acceptance; `PartyAggregateCompositeTests` exercises ULID party IDs through the aggregate; `8.8-runtime-boundary-cleanup` owns future Commons-helper adoption and the command-handler tombstone-allocation refusal. |
| I12 | Executable | `DocumentationFitnessTests` and `PartiesContainerPublishWorkflowTests` pin the runnable lanes and publication contract; warning, restore, Release build, package, consumer, typecheck, and Playwright receipts remain mandatory closure gates (the always-on CI Playwright a11y lane is a separate open ledger item). |
| I13 | Deferred (parity not yet discharged) | `MainLayoutAccessibilityTests` and `PartiesAccessibilitySpecimenTests` guard the retained shell and UI. The October 5 I20 decision accepted DW-111 as a closure deferral. Its October 21 browser evidence covers content focus in normal and forced colors and reduced motion. Story 8.9 still owns shared-primitive and token-purge parity for the remaining UI. [DW-111 resolution](../../../implementation-artifacts/deferred-work-archive.md#dw-111-observe-app-owned-content-focus-and-reduced-motion-css-at-runtime-not-as-source-greps) |
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
  At closure, `PlatformApiPrerequisitesTests` verified package and source
  selection separately and pinned each retained identity. Its residual gap
  is that identity re-opening is a review-time obligation, not something a test
  can observe. Owner: whichever story changes a retained identity. On
  2026-10-05 Administrator / jpiquot approved I16 identity re-validation for the
  final set (8.3 I20 `I16 identity parity re-validation` row): AI.Tools
  `3f194e17`, Builds `360a2b9c`, Commons `116d2681`, EventStore `865cd9e4`
  (`v3.113.0`) / package `3.113.0`, FrontComposer `2cc8dd3a`, Memories
  `5b43fe2f`, PolymorphicSerializations `98de6e01`, and Tenants `72b8e4f5`. The
  approval does not turn a receipt into Pass: a receipt is Pass only when it was
  rerun at the stamped identity, and every receipt not rerun stays
  **unvalidated** in the canonical `tests/test-summary.md` table. The
  accessibility gate now requires the selected
  `references/Hexalith.FrontComposer` gitlink bound by `parties_snapshot`,
  rather than the historical `PlatformApiPrerequisitesTests.FrontComposerSha`.
  Administrator / jpiquot approved the then-current set at Parties
  `b6443b2475d3495b58358f84cdb161b05c62d0f9` on 2026-11-01 (Round 7 decision 3,
  recorded in the 8.3 I20 table); its FrontComposer selection is
  `c092e8f91ebb670fad4ab959f2d3c8e13d809e0f`, separate from package `4.7.4`.
  That selection is dated history. On 2026-11-02 Administrator / jpiquot
  approved the ten committed root gitlinks at Parties
  `a92d15866bbf4f49cefa6c03f4a9eeec442af1de` and the independently
  evaluated Release packages EventStore `3.132.2`, Commons `2.30.10`,
  PolymorphicSerializations `1.19.8`, FrontComposer `4.7.4`, Memories
  `3.0.13`, Tenants `6.0.0`, and McpCli `1.1.0` for current-selection
  assertions only (8.3 I20 DW-182 row). Later the same day, Parties
  `18579b62ef9ac3c8780fc5e8ba26112d2665f566` selected Tenants
  `4d0d599879767196928e1c265622483a62b457ae`; the owner approved that
  exact successor for current-selection assertions and gitlink signoff.
  The initial Tenants `4b3e431745550f8466f1ce0d70e6b507c747b59a`
  selection is dated history. The other nine gitlinks and effective package
  graph stayed at their then-selected identities. The six lanes passed at Parties `56fc3b73` with
  the pinned v2 tokens above; that qualification is dated history. The
  final Round 10 table in `tests/test-summary.md` records the six Pass results
  and stamps committed at closure.
  Every current clean Pass must bind the live v2 snapshot and dependency set.
  Once Story 8.10 is committed `done`, closure evidence, both digests and the
  FrontComposer gitlink are verified against the Git objects of the commit
  that recorded that done transition. Later work does not recertify or erase
  that closed evidence; a reopening returns qualification to the live tree.
  A shallow history that cannot establish the closure commit skips loudly,
  granting no verification credit. Earlier receipts keep their original
  identities and confer no parity at this set. No deletion is authorized by
  selection or by deriving a stamp. Owner: the changing story plus the named
  Parties/EventStore/FrontComposer test owners.
- **I17 (deferral executors inherit the gate) — process gate, not executable.**
  Enforced at spec-authoring and review time. The `deferred-work.md` field
  vocabulary added 2026-08-19 (`authored_by_spec` / `activated_by_spec` /
  `delivered_slices`) makes the waiting-versus-working distinction legible so
  that a reviewer can check it. Owner: the spec author and the reviewer gate.
- **I18 (parity baseline) — review gate.** The §7 baseline is frozen at
  `2b63ab9`. At closure, `InvariantMapCoversI1ThroughI15WithExecutableOrDeferredEvidence`
  checked that the named classes existed under `tests/`. That test retired
  with its suite at `0f261ccd`. The reviewer checks behavior coverage against
  the baseline and the I20 row for any proposed successor.

No executable-evidence claim is made for I17 or I18, and none should be
manufactured — a test asserting that a document contains its own wording is not
evidence.

### 7b. Disposition of I19–I20 — added 2026-09-08

- **I19 (persistence class) — owner approval pending.** DW-81 completed on
  October 23 with SDK/Dapr compatibility and two-client conflict evidence.
  The ledger remains operational across rebuilds. The activating spec must
  identify the affected storage operations and secure the I20 classification
  and test-set decision before citing its tests as I18 migration evidence.
- **I19a (policy oracle) — blocked on the 8.3 G5 row.** G5 is still
  `needs-additive-api`; the row must be amended (drop or reclassify
  `IPersonalDataPolicy` / `IErasureStateProvider`) with an I20 row before 8.7
  may adopt the engine. Existing `PartyAggregateConsentTests` and
  `PartyAggregateErasureTests` already pin the aggregate as writer. Owner:
  8.7 spec author + EventStore G5 owner.
- **I20 (approval record) — process gate, not executable.** The 8.3 matrix now
  contains the approval table: the existing Round 6 identity-selection decision,
  the explicit 2026-10-04 three-pin approval, the EventStore `3.112.0` package
  selection, the later 2026-10-04 working-tree five-pin approval, and the four
  2026-10-05 decisions (Builds/Tenants selection, EventStore `3.113.0` package
  and source, I16 identity re-validation for the final set, and the I13/DW-111
  accepted deferral), plus the 2026-11-01 Round 7 approval of the then-current
  set at Parties `b6443b2475d3495b58358f84cdb161b05c62d0f9` and the
  selected-gitlink accessibility rule, the 2026-11-02 DW-182 approval of
  the then-current committed selection at Parties
  `a92d15866bbf4f49cefa6c03f4a9eeec442af1de`, and the Tenants successor
  approval at Parties `18579b62ef9ac3c8780fc5e8ba26112d2665f566`,
  are recorded with exact scope and provenance. Other
  applicable decision kinds remain pending. A populated selection row does not
  certify parity; under the approved I16 row only receipts rerun at the stamped
  identities count. Each applicable pending row needs its
  decision, artifact, named human, date, and human/reviewer-gate authority before
  its action proceeds. Owner: the reviewer gate.

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
| Provider for per-party encryption keys (`IKeyStorageBackend`) | `8.7-data-protection-extraction` spec, `external-runtime-deployment` | Record the I19 custody disposition and test scope under I20 before provider migration. Qualify the deployed backend under GATE-KMS and I8 before regulated-data go-live. Before any crypto deletion, the 8.7 spec must declare the `pdenc` readability window. |
| Infra / provider strategy and environment topology beyond local Aspire | `external-runtime-deployment` | Its activation spec names the provider and topology. They must preserve the I1 ACL, I1a rollback and the I7/I8 erasure, custody and revocation guarantees. |
| ASP.NET Core Data Protection key-ring, cursor and payload continuity across the AppHost cutover (I1a parity list) | **One decision** recorded once in the 8.3 ledger (I20) and binding both `8.7-data-protection-extraction` (key backend) and `8.8-runtime-boundary-cleanup` (successor topology) | Whichever of the two activation specs is authored first: either prove the successor unprotects predecessor payloads (cursor purposes included) or declare the invalidation window and the user-visible `InvalidCursor` / typed-unreadable behavior; the second spec inherits that row. |
| FAST/v4 token purge and a token-purity guard for retained RCL CSS | `8.9-frontcomposer-ui-consolidation` | Its activation spec; I13 stays Deferred until the purge is executable. |
| Remaining Fluent UI V5 accessibility parity | Story 8.9, I13 | Recheck affected controls at the selected package and source identities when adopting a shared primitive. |
| Epic 7 parent stack snapshot | Epic 7 spine (parent) | Refresh on the next parent revision. For current Parties builds, use I12 and the project’s evaluated package graph. |
| 8.3 historical G5 retained-pair wording | Story 8.3 matrix owners | Resolved 2026-10-04: the G5 cell labels both old pairs as dated inspection receipts; the current 8.10 graph is recorded separately. G5 itself remains needs-additive-api. |

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

## 11. Identity and Receipt Reconciliation — 2026-10-04

The Round 6 selection decision and the Administrator's explicit 2026-10-04
approval of the three later HEAD pins are recorded in the 8.3 I20 table and
signoff. This supersedes the old Builds V27 snapshot above. Identity selection
does not certify parity re-validation. The mandatory
Release/package/consumer/browser receipts and moved I9/I10/I13 parity claims are
unvalidated; source Debug checks are diagnostics at the approved source identities.
Story 8.10 and Epic 8 remain open; Stories 8.7–8.9 remain blocked. Module-landing
routing is source-only adoption recorded under G4/DW-99 and awaits the DW-124
FrontComposer release before package-mode UI proof.

## 12. EventStore Package Selection — 2026-10-04

Administrator / jpiquot directly requested EventStore `3.112.0`. Parties now
pins `HexalithEventStoreVersion=3.112.0` before importing the shared catalog;
package consumers remain separate from diagnostic source references. The 8.3
I20 table records package selection only. Later committed/working source
identities at Parties `7c720c7f9879ecb469993b1d2dff2bdb72b96e1b` are
observations, not inferred source-pin or I16 parity approvals. Existing user
checkout advances are preserved, and identity guards still require exact
committed gitlinks plus clean matching checkouts. Current verification and
remaining package/UI blockers are recorded in `tests/test-summary.md`.
Later the same day the Administrator / jpiquot approved exactly the five
working checkouts (8.3 I20 five-pin row, §7 I4); this section's "observations"
wording is dated history. That approval grants no I16 parity, and the committed
gitlinks remain pending until the human commits them.

## 13. Final Identity and Approval Reconciliation — 2026-10-05

After the 2026-10-04 approvals, Parties commit `2f6157d3` recorded the approved
Builds `145ae921`, FrontComposer `2cc8dd3a`, and Memories `5b43fe2f` but the
never-approved EventStore `547c938d` and Tenants `b55f96d8`; `2bdb303b` then
moved Builds, EventStore (`0dc44e46`), and Tenants again, and `47e2da32` gated
the source-only FrontComposer route options behind source mode (Round 6 D4
option (b)), which cleared the DW-124 package build failure. On 2026-10-05
Administrator / jpiquot decided, in the 8.3 I20 table: (1) Builds `360a2b9c`
and Tenants `72b8e4f5` are approved; (2) EventStore moves to `3.113.0` as both
package and source (root checkout tag `v3.113.0` = `865cd9e4`, gitlink commit
pending); (3) I16 identity re-validation is approved for the final set, but a
receipt is Pass only when rerun at the stamped identity; (4) the missing I13
runtime proof is the accepted DW-111 deferral, and I13 stays `Deferred`. §12's
`3.112.0` selection is dated history. Lane receipts are in
`tests/test-summary.md`; Story 8.10 stays in progress until its closure gate
passes.

**Closure-blocker resolution, 2026-10-05.** Parties `75b4fa1f` committed the
EventStore `865cd9e4` gitlink, so all eight final-set identities are committed.
The first full Release lane at the final set exposed three failures from commits
`37d87f5a` and `c069af07` (DW-140, DW-141, DW-142). Administrator / jpiquot
decided, in spec 8.10 "Closure-blocker resolution: DW-140, DW-141, DW-142 —
2026-10-05": (1) DW-140 (I7): `ProvisionAgentParty` and the three human
actor-binding commands are exempt from the `RejectIfRestricted` inventory, with
production handlers unchanged; provisioning is creation-only, and the binding
handlers refuse a restricted party through `CanBind` with `binding-unavailable`,
now proven by focused `HumanActorBindingTests`; (2) DW-142 (I8):
`PartyCreated.CreatedAt`, `PartyState.HasBeenCreated`, and `HumanBindingVersion`
are non-personal metadata, `AgentProvisioning` is personal data because it is
already marked `[PersonalData]` (decision amended in review loop 3, below), while
`HumanActorBindings` and `HumanActorTransitions` are a deferred privacy design
bound to open DW-129, with no `[PersonalData]` or contract change; (3) DW-141:
`PartiesOverview` passes the
source-only `FcPageTabs` `ModuleRoute`/`DefaultTabId` parameters only under
`HFC_ROUTE_OPTIONS`, matching `47e2da32`, and DW-139 keeps the package-mode
follow-up. No dependency, submodule, owner-repository, rollback-deletion, or PRD
change was made. I13 stays `Deferred` (DW-111). Lane receipts are in
`tests/test-summary.md`; Story 8.10 stays in progress until the review step
owns the status transition.

**Review loop 3, 2026-10-05.** Review of the closure-blocker resolution found
that decision 2 had classified `PartyState.AgentProvisioning` as non-personal
metadata although the property is marked `[PersonalData]` (Edge 17). Administrator
/ jpiquot amended decision 2: `AgentProvisioning` is `PersonalData`, and a new
inventory guard fails any `NonPersonalMetadata` row that carries `[PersonalData]`.
The reverted closure-blocker code was re-derived with that correction, and the
surviving review patches were applied (exact-`Pass` receipt gate, FrontComposer
stamp only for a clean `Pass`, stale-SDK documentation guard, restriction
exemption wording, EventStore pin removal condition). The E2E fixture-scope gap
stays the open DW-143 deferral. No dependency, submodule, owner-repository,
rollback-deletion, `[PersonalData]`-attribute, production-handler, or PRD change
was made; I13 stays `Deferred` (DW-111). Rerun receipts are in
`tests/test-summary.md`.

**Closure, 2026-10-05.** The loop-3 review found no spec defect. Its eight
patches were applied and every Verification lane was rerun green at Parties
`75b4fa1f` plus the closing diff. Every canonical receipt row in
`tests/test-summary.md` reads `Pass`, and the closure fitness gate passes with
Story 8.10 `done`. As the Administrator directed ("fix all three, then close"),
Story 8.10 and Epic 8 are `done` under the deferral-based closure this spine
defines. Stories 8.7–8.9 stay `blocked` behind their accepted deferrals, and every
rollback path is retained. I13 stays `Deferred` (DW-111). Open follow-ups:
DW-139, DW-143, DW-144, DW-145.
