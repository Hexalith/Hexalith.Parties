---
id: SPEC-epic-8-domain-focus
companions:
  - ../../planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md
  - ../../implementation-artifacts/deferred-work.md
  - ../../implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md
sources: []
---

> **Canonical contract.** This SPEC and the files in `companions:` are the complete, preservation-validated contract for what to build, test, and validate. Source documents listed in frontmatter are for traceability — consult them only if you need narrative rationale or prose color this contract intentionally omits.

# Epic 8 Remaining Work & Deferral Contract

## Why

A mandate: Epic 8 (post-MVP maintenance, Class C) conforms Parties to the
Hexalith domain-module contract — keep domain substance, shed reusable platform
mechanics. Stories 8.1–8.10 landed or closed with five accepted deferrals; the
remaining force is finishing the extraction without eroding preserved GDPR,
projection, and contract behavior, and without deleting a rollback surface
before identity-stamped parity exists. The architecture spine (companion,
status final, amended 2026-08-18) is the binding invariant set; this spec is
the work contract that cites it.

## Capabilities

- **CAP-1** — Closure durability
  - **intent:** The Epic 8 closure evidence — submodule identities, closure
    fitness tests, the spine's §7 map — exists as committed superproject state,
    not working-tree-only.
  - **success:** A fresh clone of HEAD passes `PlatformApiPrerequisitesTests`
    and `EpicEightClosureFitnessTests` with the pinned identities.
- **CAP-2** — Residual review-debt discharge (`8.6-residual-review-debt`)
  - **intent:** The 8.6 umbrella debt is discharged: deny-default
    EventStore-only ACL enforced in a runnable topology, authenticated
    end-to-end handler discovery, erasure-certificate identity/status
    validation, Memories cleanup races closed, Art.30/search inputs bounded.
  - **success:** The deferral's exit proof passes and every `[Review][Defer]`
    item in the 8.6 story file is checked.
- **CAP-3** — Data-protection extraction (`8.7-data-protection-extraction`)
  - **intent:** Parties consumes the shared EventStore data-protection and
    key-management engine while GDPR policy and legal semantics stay local.
  - **success:** The full I8 compatibility packet is green at the recorded
    consumption identity, with an exercised switch-back.
- **CAP-4** — Runtime boundary cleanup (`8.8-runtime-boundary-cleanup`)
  - **intent:** Client envelopes/freshness, MCP plumbing, identifier helpers,
    and the integrated topology move to their platform owners; the Parties
    AppHost retires.
  - **success:** Topology, security, publish, and rollback parity are proven
    and every I1a condition holds (all AppHost-naming deferrals passed or
    owner-re-approved) before the retirement merges.
- **CAP-5** — UI consolidation (`8.9-frontcomposer-ui-consolidation`)
  - **intent:** The remaining G4 FrontComposer primitive slices — picker,
    freshness/status, safe downloads, typed confirmation, GDPR copy — replace
    the Parties-local implementations (shell slice delivered 2026-08-18).
  - **success:** Producer bUnit plus Parties bUnit/Playwright parity per
    slice, per the deferral's exit proof.
- **CAP-6** — External runtime deployment (`external-runtime-deployment`)
  - **intent:** An external platform-ops owner runs Parties from immutable
    image tags with environment-specific DAPR configuration and promotion
    evidence.
  - **success:** Owner-repository consumption evidence plus a proven rollback
    to the prior immutable image set.

## Constraints

- Spine invariants I1–I18 bind every slice; a conflict with one is surfaced,
  never locally overridden.
- I17: activating any deferral — or any deletion-heavy spec, whatever its
  label — requires a spec declaring all six §4 gate clauses first; the spine's
  §5 order binds the deferrals (CAP-2 → CAP-3 → CAP-4 → CAP-5).
- I16/I18: parity evidence is identity-stamped and measured against the §7
  baseline; no rollback surface is deleted without it.
- I15: zero new PRD functional requirements; this work is never reported as
  MVP feature delivery (machine-enforced by `EpicEightClosureFitnessTests`).
- Validation runs xUnit v3 assemblies directly (never `dotnet test --filter`);
  Release-build and Playwright-a11y receipts must start with Pass before any
  closure status flips to done.

## Non-goals

- No re-derivation or rollback of the ratified stories 8.1–8.10.
- No in-repo runtime deployment orchestration — the assets Story 8.13 retired
  stay retired.
- No deletion of `Hexalith.Parties.Authentication` outside CAP-4's gated
  supersession.
- No domain-feature expansion; maintenance only.

## Success signal

Epic 8 reports done only when CAP-1 is durable and each accepted deferral has
passed its exit proof or been re-approved by its recorded owner, with
`EpicEightClosureFitnessTests` green throughout — a fresh clone shows the whole
contract holding with no dependence on anyone's working tree.

## Assumptions

- The G-row platform APIs (G4/G5/G6) land under their EventStore/FrontComposer
  owners as recorded in the 8.3 matrix; capability order follows spine §5.

## Open Questions

- OQ-1: Who owns the semantic freshness grammar (`ProjectionVersion`
  vocabulary) once the G6 envelope lands?
- OQ-2: Is the Memories mapping ledger formally a rebuild-surviving
  operational ledger (the spine's I19 candidate)?
- OQ-3: Is a production key-backend/KMS a prerequisite for CAP-3 before
  regulated data?
- OQ-4: How is key-ring/cursor continuity handled across CAP-4's topology
  cutover?
- OQ-5: What are the decidable approval mechanisms for I1a owner approval,
  I5 intentional versioning, and I2 SDK-hook determinations?
