---
date: 2026-05-24
project: Hexalith.Parties
project_lead: Jérôme
trigger: Epic 8 picker stories 8.2-8.6 blocked by the EventStore-fronted Parties client/gateway scheduling gate
scope_classification: Moderate (planning-gate resolution + dependency record + story status + sprint-status)
recommended_path: Direct Adjustment
status: approved-and-applied
related_artifacts:
  - _bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-05-23-epic8-picker-gate.md
  - _bmad-output/planning-artifacts/ux-party-picker-2026-05-12.md
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/implementation-artifacts/8-2-implement-typeahead-search-and-bounded-results.md
  - _bmad-output/implementation-artifacts/8-3-emit-durable-selection-by-party-id.md
  - _bmad-output/implementation-artifacts/8-4-handle-picker-states-and-stale-responses.md
  - _bmad-output/implementation-artifacts/8-5-enforce-picker-accessibility-and-localization.md
  - _bmad-output/implementation-artifacts/8-6-enforce-picker-privacy-and-integration-boundary.md
  - _bmad-output/implementation-artifacts/sprint-status.yaml
  - docs/frontend/party-picker.md
  - src/Hexalith.Parties.Picker/Components/PartyPicker.razor
  - src/Hexalith.Parties.Picker/Services/PartyPickerApiClient.cs
  - src/Hexalith.Parties.Picker/Extensions/PartyPickerServiceCollectionExtensions.cs
  - src/Hexalith.Parties.Client/Abstractions/IPartiesQueryClient.cs
  - src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs
  - src/Hexalith.Parties.Client/PartiesClientOptions.cs
  - tests/Hexalith.Parties.Picker.Tests/Services/PartyPickerTransportGuardrailTests.cs
---

# Sprint Change Proposal - Unblock Epic 8 Picker Stories 8.2-8.6

## 1. Issue Summary

Stories 8.2-8.6 (the functional Embeddable Party Picker stories) were the only remaining
not-done work in the project and were all `blocked` / `Required` by the shared EventStore-fronted
Parties client/gateway scheduling gate.

- At trigger time, `dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md` was
  `Risk Accepted` only for Epic 7 Story 7.7 and Epic 8 Story 8.1, and explicitly kept Stories
  8.2-8.6 `Required` before implementation scheduling.
- The contract has never been formally marked `Satisfied`; both prior unblocks were scoped
  risk-acceptances against the existing temporary bridge (`Hexalith.Parties.Picker` +
  `IPartiesQueryClient` / `HttpPartiesQueryClient`).
- The blocker is therefore a planning-gate decision, not a missing draft or technical failure.
  Story 8.2 was refreshed on 2026-05-24 with a full post-unblock task plan; 8.3-8.6 exist as
  implementation-ready stubs whose Tasks/Subtasks will be authored by create-story before dev.

Evidence:

- Before application, the dependency record listed Stories 8.2-8.6 as `Required`.
- Before application, story files `8-2`..`8-6` each had `Status: blocked` with a `## Blocker` /
  `## Required To Unblock` section.
- The picker RCL and typed client bridge already exist in source and route through
  `IPartiesQueryClient`, not retired REST/admin/DAPR/projection/controller/actor-host internals.
- `tests/Hexalith.Parties.Picker.Tests/Services/PartyPickerTransportGuardrailTests.cs` enforces
  transport/source guardrails for the picker package.

## 2. Impact Analysis

| Dimension | Impact |
|---|---|
| Epic Impact | Epic 8 scope is unchanged. Only the scheduling status of Stories 8.2-8.6 changes from blocked to ready-for-dev after approval. |
| Story Impact | Stories 8.2, 8.3, 8.4, 8.5, and 8.6 become schedulable against the existing picker RCL and typed query client boundary. No other epic is affected (Epics 1-7 and 9 are done). |
| Artifact Conflicts | No PRD, UX, or architecture rewrite required. Acceptance aligns with existing FR67, the picker UX spec (UX-DR22/25/32), and the architecture's Party Picker Frontend Surface. |
| Technical Impact | No source code change is part of this correction. Implementation proceeds against the current `Hexalith.Parties.Picker` and `Hexalith.Parties.Client` surfaces with guardrails preserved. |
| Risk | Medium. Higher than the 8.1 shell acceptance because 8.2-8.6 implement real picker behavior; if the formal contract later reshapes `IPartiesQueryClient` or the selection model, these implementations may churn. Contained by scoping to the existing bridge with binding conditions. |

### Keystone Scoping Decision

Do not globally mark the dependency `Satisfied`. Extend the same scoped acceptance used for
Story 8.1 to cover Stories 8.2-8.6 against the existing picker/client surface, with binding
fail-closed / privacy / boundary conditions. The dependency's "Risk Acceptance (2026-05-24 -
Stories 8.2-8.6)" section is the authoritative scope and conditions record.

Story-specific note (8.3): the .NET `PartyPickerSelection` model carries more display metadata
than the narrow DOM `party-selected` event detail. The durable callback payload must remain
party-id-only; the richer .NET model may need reconciliation when the formal contract is frozen.

## 3. Options Considered

### Option A - Direct Adjustment (recommended, selected)

Extend the scoped risk acceptance to Stories 8.2-8.6 against the existing bridge, then move the
five stories and sprint-status to ready-for-dev.

- Effort: Low.
- Risk: Medium, bounded by explicit scope, binding conditions, and existing transport/privacy guardrails.
- Benefit: Unblocks the only remaining work without pretending the full Epic 8 contract is complete.

### Option B - Mark Dependency Satisfied

Rejected. No fully-built, formally accepted EventStore-fronted Parties client/gateway contract
exists. The full dependency includes admin browse/search/detail, GDPR command methods, capability
detection, FrontComposer route support, and complete failure semantics — not honestly satisfiable today.

### Option C - Keep Stories 8.2-8.6 Blocked Until Formal Contract Freeze

Viable but stalls the only remaining work and leaves the existing picker RCL and guardrail tests
idle. Rejected by the project lead in favor of accepting bounded implementation risk now.

## 4. Applied Changes

### Change 1 - Dependency Record

Artifact: `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md`

- Frontmatter `status`, `required_before`, `risk_accepted_for`, `risk_accepted_date`, and
  `risk_acceptance_source` updated to record all Epic 8 picker stories (8.1-8.6) as scoped
  risk-accepted; no story remains `Required`; dependency still not globally `Satisfied`.
- `## Status` prose and `## Scheduling Gate` note updated to reflect that no story remains gated.
- New `## Risk Acceptance (2026-05-24 - Stories 8.2-8.6)` section added with decision, residual
  risks (incl. the 8.3 callback-model reconciliation risk), binding conditions, and the linked
  contract-of-record (same temporary picker bridge as Story 8.1).

### Change 2 - Story Files 8.2-8.6

Artifacts: `8-2-…`, `8-3-…`, `8-4-…`, `8-5-…`, `8-6-….md`

- Changed each `Status: blocked` to `Status: ready-for-dev`.
- Replaced each `## Blocker` / `## Required To Unblock` section with a `## Gate Resolution (2026-05-24)`
  section citing this proposal and the updated dependency record, restating the binding conditions.
- Added a Change Log row (v0.2) to each (created where absent).
- Story 8.2: updated the stale create-story HTML comment that asserted the dependency "remains Required".
- No Tasks/Subtasks authored here for 8.3-8.6; that is create-story's responsibility inside the build cycle.

### Change 3 - Sprint Status

Artifact: `_bmad-output/implementation-artifacts/sprint-status.yaml`

- Changed `8-2`..`8-6` from `blocked` to `ready-for-dev`.
- Added an Epic 8 section note and a top-of-file `last_updated` audit comment scoping the acceptance.

## 5. Acceptance Conditions

The correction is valid only if all of the following remain true for Stories 8.2-8.6:

- All data access stays routed through `IPartiesQueryClient` (e.g. `SearchPartiesAsync`); no retired
  Parties REST endpoints, admin endpoints, DAPR actors, projection actors, local search services,
  controllers, or actor-host internals are introduced.
- Host request/auth context is host-supplied; the picker does not persist, refresh, parse for
  authorization, or log tokens.
- Durable DOM callback payloads remain narrow and exclude tenant ids, JWTs, search text, display
  names, contact values, identifiers, consent text, backend problem details, and raw query payloads.
- Failure states remain fail-closed and PII-safe for unauthorized, forbidden, unavailable, malformed,
  timeout, degraded, not found, gone/erased, and stale responses.
- Existing picker transport/privacy guardrail tests remain binding before any of 8.2-8.6 is closed.
- When the formal contract is accepted, the dependency flips to `Satisfied` and the provisional
  bridge is reconciled or replaced across all picker stories.

## 6. Checklist Review

| Correct-Course Area | Result |
|---|---|
| Trigger/context | Confirmed. Trigger is the Epic 8 scheduling gate blocking Stories 8.2-8.6. |
| Epic impact | No epic scope change. Scheduling impact limited to Stories 8.2-8.6. |
| Artifact conflicts | No PRD, architecture, or UX contradiction found. Dependency/stories/sprint-status updated after approval. |
| Path forward | Direct Adjustment (Option A). Rollback and MVP replan not applicable. |
| Proposal completeness | Artifact edits, scope boundaries, residual risks, and binding acceptance conditions documented here. |
| Handoff | Approval received from Jérôme on 2026-05-24; changes applied. Build cycle (story-automator) to implement 8.2-8.6. |

## 7. Approval And Application

Approved by Jérôme on 2026-05-24 and applied as Option A.

Applied changes:

1. Dependency record: scoped risk acceptance extended to Stories 8.2-8.6.
2. Story files 8.2-8.6: status flip and gate-resolution update.
3. Sprint-status update.

No code changes are part of this correct-course transaction.

## 8. Implementation Handoff

- **Scope classification:** Moderate (planning-gate resolution; no replan, no code).
- **Recipient:** Build cycle / Developer agent via the story-automator, running create-story →
  dev-story → code-review for Stories 8.2 → 8.3 → 8.4 → 8.5 → 8.6 in order.
- **Binding constraint for the dev agent:** honor every condition in Section 5 and the dependency
  record's "Risk Acceptance (2026-05-24 - Stories 8.2-8.6)" section. Do not invent contract methods;
  build only on `IPartiesQueryClient` surfaces that genuinely exist.
- **Success criteria:** all five stories reach `done` with picker transport/privacy guardrail tests
  green and no retired-surface reintroduction; the dependency record remains accurate (still not
  globally `Satisfied` until the formal contract lands).
