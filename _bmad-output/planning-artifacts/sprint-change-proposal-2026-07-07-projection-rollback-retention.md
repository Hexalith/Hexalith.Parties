---
title: Sprint Change Proposal — Projection Rollback-Only Path Retention (Story 8.6 Deletion Guardrail)
date: 2026-07-07
author: Administrator
workflow: bmad-correct-course
mode: batch
scope_classification: moderate
trigger: >
  Epic 7 retrospective action item (sprint-status.yaml): "Keep projection
  rollback-only paths until Epic 8 proves EventStore SDK projection/query
  parity, GDPR processing-record reads, rebuild behavior, and rollback
  replacement." Formalize that open retro action into a governing, repo-evidenced
  deletion guardrail for Story 8.6.
status: approved
approved: 2026-07-07T20:15:00+02:00
related:
  - _bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md
  - _bmad-output/implementation-artifacts/spec-8-6-projection-and-query-sdk-migration.md
  - _bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md
  - _bmad-output/implementation-artifacts/7-5-projection-checkpoint-rebuild-migration-and-local-code-removal.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-platform-prerequisite-routing.md
  - _bmad-output/implementation-artifacts/epic-8-context.md
---

# Sprint Change Proposal — Projection Rollback-Only Path Retention (Story 8.6 Deletion Guardrail)

## 1. Issue Summary

Epic 7 closed as adapter-first and reversible: Story 7.5
(`7-5-projection-checkpoint-rebuild-migration-and-local-code-removal`, **done**)
made the EventStore projection platform authoritative at runtime but
**deliberately retained** the Parties-local projection/query/rebuild code as a
**rollback-only** path rather than deleting it (its AC7 = "adapter-first and
reversible"; its tasks explicitly say *"narrow \[the local adapter] to
rollback-only and document the switch"* and *"keep a thin local adapter and
document why deletion is deferred"*). Actual deletion was deferred to Epic 8
Story 8.6.

The Epic 7 retrospective preserved this as an **open action item** (verbatim, in
`sprint-status.yaml`):

> "Keep projection rollback-only paths until Epic 8 proves EventStore SDK
> projection/query parity, GDPR processing-record reads, rebuild behavior, and
> rollback replacement."

**This proposal formalizes that open retro action into a governing, evidenced
deletion guardrail** for Story 8.6. It is **not** a feature or MVP-scope change
and **not** a code change. It ratifies "hold the line until proven" as an
approved decision with a named proof-gate and exit criteria, consolidates the
four already-encoded governance layers into one home, and records repo-verified
evidence that the guardrail is intact today.

### Why now

Story 8.6 ("Projection and query SDK migration") is the deletion-heavy migration
that will remove these paths. It is currently `backlog`. Before it starts, the
retention constraint should be an **approved decision with explicit exit
criteria**, not merely an open retro action — so that a future dev/dev-auto
session cannot delete a rollback path without first recording the four proofs.

### Evidence (repo-verified 2026-07-07)

**The constraint is already encoded in four places (this proposal consolidates,
it does not invent):**

1. **Spine invariants** (`ARCHITECTURE-SPINE.md`): **I3** (local rollback paths
   for projection/query stay until the replacement API has parity evidence +
   proven rollback; `catch (NotImplementedException)` deleted only after parity),
   **I9** (replay-from-zero, per-actor checkpoints, set-based idempotency,
   duplicate/out-of-order tolerance), **I10** (stale/degraded last-known,
   `ProjectionFreshnessMetadata` on every read, erased-party exclusion, full
   rebuild verified against aggregate replay **before local deletion**), **I8**
   (Art.30 processing-record reads, no-leak diagnostics).
2. **`spec-8-6`**: Block-If prerequisite + "Never delete … before its
   replacement has recorded parity evidence and proven rollback"; §4 readiness-
   gate mapping; acceptance criterion that any rebuild divergence blocks
   deletion.
3. **`story-8-3` matrix**, row *"EventStore projection/query SDK"* =
   `needs-additive-api`, rollback column: *"Keep local rollback path:
   projection/query actors, rebuild service, adapters, and existing freshness
   fallback stay until Story 8.6 proves parity."*
4. **`sprint-status.yaml`** Epic 7 action item (open) — the trigger text itself —
   and the Epic 8 owner-routing action item that routes projection/query parity
   (G3/G10/G6) to Hexalith.EventStore owners.

**The guardrail is holding in the tree today** (verified 2026-07-07):

| Rollback-only artifact (spec-8-6 "DELETE only after parity" set) | Present? |
|---|---|
| `Projections/Actors/PartyDetailProjectionActor.cs`, `PartyIndexProjectionActor.cs`, `PartyEventTypeResolver.cs` | ✅ present |
| `Projections/Services/ProjectionRebuildService.cs` (+ `IProjectionRebuildService.cs`), `PartyProjectionRebuildCheckpoint.cs`, `PartyProjectionRebuildScope.cs` | ✅ present |
| `Projections/Services/IPartyProjectionPlatformAdapter.cs`, `LocalPartyProjectionPlatformAdapter.cs`, `PartyProjectionPlatformFreshness.cs` | ✅ present |
| `Projections/Configuration/PartyProjectionPlatformAdapterMode.cs` | ✅ present |
| `Hexalith.Parties/Domain/EventStorePartyProjectionPlatformAdapter.cs`, `PartyProjectionUpdateOrchestrator.cs` | ✅ present |
| `Hexalith.Parties/Extensions/PartyDetailProjectionActorExtensions.cs` (**7** `catch (NotImplementedException)`) | ✅ present |
| `Hexalith.Parties/HealthChecks/ProjectionActorsHealthCheck.cs` | ✅ present |
| `Hexalith.Parties/Queries/PartyDetailProjectionQueryActor.cs`, `PartyIndexProjectionQueryActor.cs` (**3** `catch (NotImplementedException)`), `IPartyProjectionQueryActor.cs` | ✅ present |

- **10** `catch (NotImplementedException)` remoting branches remain (7 + 3) — the
  reversible SDK-vs-local control flow. Intact.
- `sprint-status.yaml`: `8-6-projection-and-query-sdk-migration: backlog` — no
  deletion has occurred; guardrail currently satisfied.

### Change-Analysis Checklist Status

| # | Item | Status |
|---|---|---|
| 1.1–1.3 | Trigger, core problem, evidence | ✅ Done (deletion-safety guardrail; repo-verified) |
| 2.1 | Epic 8 completable as planned? | ❗ Only after Story 8.6 records the four proofs in the 8.3 matrix row |
| 2.2 | Epic-level change | ✅ None — no epic added/removed; tracking annotation only |
| 2.3–2.4 | Other/future epics affected | ✅ N/A (Epics 1–7 done; Epic 8 only) |
| 2.5 | Resequencing | ✅ None — `8.1→8.10` preserved |
| 3.1 | PRD conflict | ✅ None — no FR change (Class C, zero new PRD FRs) |
| 3.2 | Architecture | ✅ None — spine I3/I9/I10/I8 already govern; no spine edit |
| 3.3 | UX conflict | ✅ N/A — Epic 8 conformance; no new UX |
| 3.4 | Other artifacts | ❗→✅ `sprint-status.yaml` retro item annotated to this proposal |
| 4.1 | Direct Adjustment (formalize-and-gate) | ✅ **Viable — selected** |
| 4.2 | Rollback | ⛔ N/A — nothing to revert (the point is to *keep* rollback paths) |
| 4.3 | MVP review | ✅ N/A — Epics 1–5 complete/unaffected |
| 4.4 | Path selected | ✅ Direct Adjustment (formalize the retro action as a governed guardrail) |

## 2. Impact Analysis

### Epic Impact
- **Epic 8 only.** Epics 1–7 done and unaffected. No epic added, removed, or
  re-sequenced; `8.1→8.10` unchanged. Class C maintenance classification (zero
  new PRD FRs) preserved.

### Story Impact
- **Story 8.6** stays `backlog`. This proposal adds **no new gate** — it makes
  the existing spine/spec/matrix retention rule an **approved, named guardrail
  with exit criteria** that Story 8.6 must satisfy before deleting any listed
  file. Stories 8.1–8.5 done, unaffected. 8.7–8.10 unaffected.
- **Story 7.5** (done) is **ratified as-is**: its intentional retention of the
  local code as rollback-only was correct and is not reopened. No rollback of
  completed work (Correct Course §4.2 = not viable).

### Artifact Conflicts (resolved by this proposal)
- **`sprint-status.yaml`:** the open Epic 7 retro action item had no governing
  home → annotate it to point at this proposal; keep `status: open` until Story
  8.6 records the four proofs, then close. `last_updated` bumped. **No story/epic
  row changes; no re-sequencing.**
- **`ARCHITECTURE-SPINE.md` / `spec-8-6` / `story-8-3` matrix:** **untouched.**
  The spine already carries I3/I9/I10/I8; `spec-8-6` already carries the Block-If
  + parity-before-delete rule; the 8.3 matrix is a done Story 8.3 artifact and is
  fitness-guarded (`PlatformApiPrerequisitesTests`). This proposal **cross-
  references** them, consistent with the precedent that Correct Course does not
  rewrite the fitness-guarded matrix or edit spec files.
- **PRD / UX / epic-8-context.md:** no conflict, no change. **`epics.md`:** this
  proposal made no change; a later 2026-07-07 Correct Course session then tightened
  **Story 8.6 AC** to name *rebuild behavior verified against aggregate replay* and a
  *proven rollback replacement for the EventStore SDK path* as explicit deletion
  preconditions — aligning the AC wording with §4.2 of this proposal (additive
  reinforcement; no semantic conflict).

### Technical Impact
- **No Parties code change. No submodule edits.** The guardrail's whole purpose
  is that the rollback-only files **stay in place** until Story 8.6 proves
  parity, GDPR processing-record reads, rebuild behavior, and rollback
  replacement — per spine I3/I9/I10/I8, `spec-8-6`, and the 8.3 matrix rollback
  column.
- Owner-side delivery (EventStore additive projection/query parity, G3/G10/G6)
  is already routed by the approved
  `sprint-change-proposal-2026-07-07-platform-prerequisite-routing.md`; this
  proposal governs the **consuming-side deletion discipline**, not the owner work.

## 3. Recommended Approach

**Selected: Option 1 — Direct Adjustment (formalize the retro action as a
governed guardrail with exit criteria).**

Ratify "keep the projection/query rollback-only paths until Story 8.6 proves the
four conditions" as an approved decision, consolidate the four existing
governance layers into one home, record today's repo-verified evidence, and give
Story 8.6 an explicit, checkable **exit criteria** (§4.2). Annotate the open
Epic 7 retro action item so it has a governing reference and stays `open` until
the proofs land.

**Rationale:** lowest-disruption, honest, and momentum-preserving. The constraint
is already encoded across spine/spec/matrix/retro — the residual risk is purely
**process drift** (a future automated session deleting a rollback path without
recording proof). Formalizing the guardrail with named exit criteria closes that
drift risk without inventing new scope. Rollback (§4.2) is the opposite of what's
wanted here. MVP review (§4.3) does not apply.

- **Effort:** Low (one governing document + a one-line-block `sprint-status.yaml`
  annotation; no code).
- **Risk:** Low. Residual delivery risk stays owned by the §4 gate + the 8.3
  matrix Review Gate; this proposal reduces process-drift risk.
- **Timeline impact:** None to MVP. Does **not** unblock or block Story 8.6 —
  8.6 remains gated on owner-delivered projection/query parity (G3/G10/G6) per
  the 8.3 matrix.

## 4. Detailed Change Proposals

### 4.1 Governing decision (this document)

**Decision (approved-on-approval):** The following Parties-local projection,
query, rebuild, and platform-adapter files, plus the `catch
(NotImplementedException)` remoting control flow, are **rollback-only** and
**MUST NOT be deleted, narrowed, or have their `NotImplementedException`
branches removed** until Story 8.6 has recorded, in the `story-8-3` matrix row
*"EventStore projection/query SDK"*, all four proofs in §4.2 below.

**Retention set (must stay until §4.2 proofs are recorded):**
- `src/Hexalith.Parties.Projections/Actors/PartyDetailProjectionActor.cs`,
  `Actors/PartyIndexProjectionActor.cs`, `Actors/PartyEventTypeResolver.cs`
- `src/Hexalith.Parties.Projections/Services/ProjectionRebuildService.cs`
  (+ `IProjectionRebuildService.cs`), `Services/PartyProjectionRebuildCheckpoint.cs`,
  `Services/PartyProjectionRebuildScope.cs`
- `src/Hexalith.Parties.Projections/Services/IPartyProjectionPlatformAdapter.cs`,
  `Services/LocalPartyProjectionPlatformAdapter.cs`,
  `Services/PartyProjectionPlatformFreshness.cs`
- `src/Hexalith.Parties.Projections/Configuration/PartyProjectionPlatformAdapterMode.cs`
- `src/Hexalith.Parties/Domain/EventStorePartyProjectionPlatformAdapter.cs`,
  `Domain/PartyProjectionUpdateOrchestrator.cs`
- `src/Hexalith.Parties/Extensions/PartyDetailProjectionActorExtensions.cs`
  (7× `catch (NotImplementedException)`)
- `src/Hexalith.Parties/HealthChecks/ProjectionActorsHealthCheck.cs`
- `src/Hexalith.Parties/Queries/PartyDetailProjectionQueryActor.cs`,
  `Queries/PartyIndexProjectionQueryActor.cs` (3× `catch (NotImplementedException)`),
  `Queries/IPartyProjectionQueryActor.cs`

**Revert semantics:** rollback = restore the local actor/adapter/rebuild
registrations in `PartiesServiceCollectionExtensions.cs` and switch
`PartyProjectionPlatformAdapterMode` back to the local path (a named DI/config
switch and/or a revertable deletion commit — per Story 7.5 AC7). No data
migration without a rollback script + approval.

### 4.2 Exit criteria — what Story 8.6 must prove before any deletion

Mapping the trigger's four conditions to spine invariants and the parity harness
Story 8.6 already prescribes. Each must be recorded in the 8.3 matrix row before
the corresponding local code is deleted:

| # | Trigger condition | Spine invariant | Proof required (recorded in 8.3 matrix + `test-summary.md`) |
|---|---|---|---|
| 1 | **EventStore SDK projection/query parity** | I9 | Replay-from-zero folds identically; duplicate/out-of-order delivery is idempotent (final state == single in-order delivery); paginated index cursor scope stable via `IQueryCursorCodec` (in-flight cursors don't break); detail + index read models behaviorally identical to pre-migration. |
| 2 | **GDPR processing-record reads** | I8 | Art.30 `GetProcessingRecords` returns `ProcessingActivityRecord[]` with identical semantics on the SDK query path; Art.20 export + erased-party exclusion preserved; **no** payload/actor-id/party-name/key-alias leakage in logs, traces, or errors. |
| 3 | **Rebuild behavior** | I10 | A **full projection rebuild is executed and verified against aggregate replay** for detail **and** index, with recorded command + diff evidence; any divergence **blocks** deletion; freshness metadata (`Current/Stale/Rebuilding/Degraded/Unavailable`) preserved. |
| 4 | **Rollback replacement** | I3 | The replacement SDK path has **both** parity evidence **and** a proven rollback (named DI/config switch or revertable commit); `catch (NotImplementedException)` branches removed **only after** parity is recorded. |

**Deletion is authorized for a given file only when** its governing proof(s)
above are recorded in the `story-8-3` matrix row **and** the parity harness (new,
per `spec-8-6`) is green **and** the full rebuild diff is clean. Absent any one,
the file stays.

### 4.3 `sprint-status.yaml` change (applied on approval)

Annotate the open Epic 7 retro action item (the trigger text) with a comment
block naming this proposal as its governing home, keep `status: open`, and bump
`last_updated`. **No story/epic rows change; no re-sequencing.** Proposed diff:

```yaml
  - epic: 7
+   # Formalized 2026-07-07 as a governed deletion guardrail with exit criteria:
+   #   sprint-change-proposal-2026-07-07-projection-rollback-retention.md.
+   # Stays `open` until Story 8.6 records parity (I9), processing-record reads (I8),
+   # rebuild-vs-replay (I10), and proven rollback (I3) in the 8.3 matrix row.
+   # Verified 2026-07-07: all rollback-only projection/query files intact; 8.6 backlog.
    action: "Keep projection rollback-only paths until Epic 8 proves EventStore SDK projection/query parity, GDPR processing-record reads, rebuild behavior, and rollback replacement."
    owner: "Winston (Architect) + Amelia (Developer)"
    status: open
```

(Plus `last_updated` bump in both the header comment and the `last_updated:`
field.)

## 5. Implementation Handoff

**Scope classification: Moderate.** Governance/tracking formalization plus a
one-block `sprint-status.yaml` annotation — no fundamental replan, no code, no
submodule edits.

| Recipient | Responsibility |
|---|---|
| PM / Architect (Winston) | Owns the guardrail. Ensure Story 8.6's spec/dev session records the four §4.2 proofs in the `story-8-3` matrix row before any deletion. Close the annotated retro action item only when all four proofs are recorded. |
| Product Owner / Developer (Alice / Amelia) | Do **not** delete, narrow, or strip `NotImplementedException` from any §4.1 file until §4.2 exit criteria are met. Build the `spec-8-6` parity harness and record the full-rebuild diff evidence. Keep the local DI/config rollback switch functional. |
| Test Architect (Murat) | Verify parity-harness lanes run the xUnit v3 assemblies directly (not `dotnet test --filter`), pin `-p:MinVerVersionOverride=1.0.0` on rebuilt projects, and confirm the rebuild-vs-replay diff is clean before signing off deletion. |

### Success Criteria
1. The retention constraint is an **approved decision** with an explicit
   retention set (§4.1) and exit criteria (§4.2) — met by this document on
   approval.
2. The open Epic 7 retro action item names this proposal as its governing home
   and stays `open` until Story 8.6 proves the four conditions — met by §4.3.
3. **No Parties code change, no submodule edits, no spec/matrix/spine rewrite** —
   met (cross-reference only).
4. Story 8.6 stays `backlog`; deletion authorized only when the four §4.2 proofs
   are recorded in the 8.3 matrix row — governance preserved (spine §4, matrix
   Review Gate).
5. No PRD FR coverage change; Epics 1–5 remain the feature-readiness baseline —
   met.

### Deferred / out of scope for this proposal
- Owner-side delivery of EventStore projection/query parity (G3/G10/G6) — already
  routed by `sprint-change-proposal-2026-07-07-platform-prerequisite-routing.md`.
- Authoring/splitting Story 8.6's spec beyond what `spec-8-6` already records
  (routed to the spec/create-story workflow, per spine §4).
- The parallel crypto/key-management retention guardrail (separate open Epic 7
  retro action item) — analogous but out of scope here.
