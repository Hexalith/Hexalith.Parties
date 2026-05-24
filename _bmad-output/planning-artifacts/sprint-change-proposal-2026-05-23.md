---
date: 2026-05-23
project: Hexalith.Parties
project_lead: Jérôme
trigger: dev-story 7.7 request blocked by still-Required primary scheduling gate
scope_classification: Moderate (planning-gate resolution + dependency record + sprint-status)
recommended_path: Direct Adjustment
status: approved-and-applied
related_artifacts:
  - _bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-05-17.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-05-22.md
  - _bmad-output/implementation-artifacts/7-7-implement-gdpr-operation-panels.md
  - _bmad-output/implementation-artifacts/7-6-gate-gdpr-operations-on-accepted-client-contract.md
  - _bmad-output/implementation-artifacts/sprint-status.yaml
---

# Sprint Change Proposal — Unblock Story 7.7 (GDPR Operation Panels)

## 1. Issue Summary

A `dev-story 7.7` request surfaced that Story 7.7 (Implement GDPR Operation Panels) is `blocked` by two scheduling gates, of which the **primary gate was still live**:

- **Primary gate** — `dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md` was still `status: Required`. It mandates that no Epic 7 / Epic 8 implementation story be scheduled until the accepted EventStore-fronted Parties client/gateway contract is updated to `Satisfied` or `Risk Accepted` and linked from planning.
- **Secondary gate** — Story 9.8 (`9-8-solution-build-green-on-clean-clone`), added as a second gate by `sprint-change-proposal-2026-05-22.md`. This gate is **already satisfied**: `9-8` reached `done` on 2026-05-23.

The core problem is therefore a **scheduling-gate resolution decision**, not a scope or implementation failure. A fully-built, formally accepted EventStore-fronted Parties client/gateway contract does not yet exist: `HttpAdminPortalGdprClient` has mixed maturity — some commands post through the EventStore command gateway, some queries derive from `PartyDetail`, and erasure-certificate + retry-verification still report contract-unavailable.

Evidence:

- `dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md` line 4 was `status: Required`.
- `sprint-status.yaml` shows `9-8-solution-build-green-on-clean-clone: done`.
- Story 7.7 file Current Implementation Snapshot documents the provisional, mixed-maturity client shape.

## 2. Impact Analysis

| Dimension | Impact |
|---|---|
| Epic Impact | Epic 7 completable; Story 7.7 is the only non-`done` Epic 7 story. No epic-structure change. |
| Story Impact | Story 7.7 unblocked (scoped). **Epic 8 (8.1, 8.2, 8.3, 8.6) shares the same gate and is intentionally left `Required`/`blocked`.** |
| Artifact Conflicts | None. PRD, architecture, and UX unaffected. UX-DR11 fail-closed blocker is retained as the safety net. |
| Technical Impact | No code change in this transaction. Implementation proceeds against the provisional client treated as a temporary bridge, with capability gating preserved. |

### Keystone scoping decision

The dependency's Affected Stories list covers Story 7.7 **and** Epic 8 picker stories. The risk acceptance is **deliberately scoped to Story 7.7 only**. Unblocking the embeddable party picker (Epic 8) is left as a separate, deliberate decision and is not a side effect of this proposal.

## 3. Recommended Approach

**Direct Adjustment.** Flip the shared scheduling gate to `Risk Accepted` scoped to Story 7.7; record the decision; update sprint-status. No rollback, no PRD/architecture replan.

- Effort: Low.
- Risk after correction: Low–Medium — residual churn risk on the provisional client is explicitly accepted and contained by preserving the Story 7.6 fail-closed gate (UX-DR11).

Alternatives considered:

- **Satisfied** instead of Risk Accepted — rejected as dishonest: no complete, formally accepted contract exists (cert/retry still contract-unavailable).
- **Global flip (incl. Epic 8)** — rejected: Epic 8 is a distinct deliverable warranting its own decision.
- **Rollback / MVP review** — not applicable; no failed implementation, MVP scope unchanged.

## 4. Detailed Change Proposals (applied)

### Change 1 — Dependency record → Risk Accepted (scoped)

**Artifact:** `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md`

- Frontmatter `status: Required` → `status: Risk Accepted (scoped — Epic 7 Story 7.7 only; Epic 8 remains Required)`; `required_before` reduced to Epic 8; added `risk_accepted_for`, `risk_accepted_date`, `risk_accepted_by`, `risk_acceptance_source`.
- `## Status` body rewritten to state the scoped acceptance and that Epic 8 stays `Required`.
- New `## Risk Acceptance (2026-05-23)` section: decision (existing client = temporary bridge), residual risks accepted, BINDING conditions (preserve Story 7.6 fail-closed gate; build only on existing provisional methods; keep tenant/privacy/accessibility/encoding guardrails; flip to Satisfied when formal contract lands), and the linked provisional contract-of-record files.

### Change 2 — Story 7.7 file gate resolution

**Artifact:** `_bmad-output/implementation-artifacts/7-7-implement-gdpr-operation-panels.md`

- Line-5 HTML comment updated from "intentionally blocked" to gate-resolution pointer.
- `## Blocking Status` replaced by `## Gate Resolution (2026-05-23)` documenting both gates resolved and the binding conditions.
- `## Required To Unblock` prepended with a RESOLVED note (bullets retained for history).
- Change Log v0.4 entry added.
- Dev Agent Record → Debug Log entry added.

### Change 3 — sprint-status.yaml

**Artifact:** `_bmad-output/implementation-artifacts/sprint-status.yaml`

- `last_updated` bumped to `2026-05-23 17:00:00 +02:00`.
- correct-course touched-by comment added recording the scoped risk acceptance and that Epic 8 stays blocked.
- Inline comment added above the `7-7` entry. **`7-7` left as `blocked`** pending `create-story` task authoring (the story has no Tasks/Subtasks yet; flipping to `ready-for-dev` prematurely would let a dev-story run auto-complete an empty story).

## 5. Checklist Status

| Item | Status | Notes |
| --- | --- | --- |
| 1.1 Triggering story | Done | dev-story 7.7 request. |
| 1.2 Core problem | Done | Scheduling-gate resolution; primary gate still Required. |
| 1.3 Evidence | Done | Dependency line 4 Required; 9-8 done; client mixed maturity. |
| 2.1 Current epic impact | Done | Epic 7 completable; 7.7 only non-done story. |
| 2.2 Epic-level changes | Done | No epic-structure change. |
| 2.3 Future epic impact | Action-needed→Done | Epic 8 shares gate; intentionally left Required. |
| 2.4 Invalidated/new epics | N/A | None. |
| 2.5 Priority/order | Done | 7.7 cleared; Epic 8 deferred decision. |
| 3.1 PRD | N/A | No conflict; MVP unaffected. |
| 3.2 Architecture | N/A | EventStore-fronted UI boundary already supported. |
| 3.3 UX | Done | UX-DR11 fail-closed blocker retained. |
| 3.4 Secondary artifacts | Done | epics.md unchanged by design; dependency record + sprint-status updated. |
| 4.1 Direct adjustment | Viable | Low effort. |
| 4.2 Rollback | Not viable | No failed implementation. |
| 4.3 MVP review | Not needed | MVP unchanged. |
| 4.4 Recommended path | Done | Direct Adjustment. |
| 5.1–5.5 Proposal components | Done | Included above. |
| 6.3 User approval | Pending | See Section 7. |
| 6.4 Sprint-status update | Done | Applied. |

## 6. Implementation Handoff

**Scope classification:** Moderate.

**Handoff recipients:**

- **Product Owner / Architect (Jérôme):** Risk Acceptance decision recorded. If/when the formal EventStore-fronted contract is accepted, flip the dependency record to `Satisfied` and reconcile the provisional bridge.
- **Scrum Master / create-story:** Run `create-story 7.7` to author the Tasks/Subtasks breakdown against the accepted (provisional) contract and move the story `blocked → ready-for-dev`.
- **Developer / dev-story:** After tasks are authored, run `dev-story 7.7`. Implementation must honor the binding conditions (preserve Story 7.6 fail-closed gate; never fake unavailable methods; keep privacy/accessibility/tenant guardrails).

**Success criteria:**

- Dependency record is `Risk Accepted` (scoped) with linked provisional contract — done.
- Story 7.7 records gate resolution — done.
- sprint-status reflects the resolution; Epic 8 remains blocked — done.
- Story 7.7 gains a real Tasks/Subtasks breakdown via create-story before any dev-story run.

## 7. Approval

Approved by Jérôme (project lead) on 2026-05-23 via Correct Course workflow (incremental mode). All three changes were reviewed and applied in this transaction. Finalized as `approved-and-applied`.
