---
title: '8.9 Adopt the FrontComposer entity picker'
type: 'refactor'
created: '2026-08-19'
status: 'in-progress'
baseline_commit: 'eeda4bacb2b79b2173d77c4fed6c3fdd1f5edaa5'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/specs/spec-epic-8-domain-focus/SPEC.md'
  - '{project-root}/_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md'
  - '{project-root}/_bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The adopter-facing Parties picker reimplements generic WAI-ARIA combobox and asynchronous-selection mechanics. Migration is blocked because G4-A `FcEntityPicker<T>` is absent from FrontComposer 4.1.1 and source gitlink `7a337a21d4ba261bf27aeb3feedde47789f0160a`.

**Approach:** Fail closed until the ordered Epic 8 predecessors and complete G4 availability receipt are accepted at the exact identity Parties consumes. Then keep `Hexalith.Parties.Picker` as a stable adopter adapter over `FcEntityPicker<T>`, replacing its internals while preserving every public parameter, durable party-ID behavior, and `party-selected` custom event.

## Boundaries & Constraints

**Always:** Preserve package ID `Hexalith.Parties.Picker`, `<hexalith-party-picker>`, its bounded event, durable `SelectedPartyId`, stale-response rejection, keyboard/pointer and WAI-ARIA behavior, forced colors/reduced motion, and Admin integration. Use Fluent UI V5 and Fluent 2 inheritance.

**Ask First:** HALT without production changes unless CAP-2 → CAP-3 → CAP-4 is discharged or re-approved and G4 records owner approval, immutable identity, API/package validation, producer tests, Parties parity, and rollback. Also halt if the selected mode lacks `FcEntityPicker<T>` or requires an adopter-contract change.

**Never:** Edit the FrontComposer submodule under this Parties story; treat routing approval or an unshipped source checkout as delivery; change the custom-element name, public package identity, callback/event shape, or durable-ID semantics; delete local behavior before parity passes; or implement deferred G4-B through G4-E and styling-conformance work.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Prerequisite gate | Any ordered predecessor or G4 receipt is incomplete/mismatched | No picker migration; report the exact blocker | Keep the current picker and Admin bridge |
| Async ordering | Older search/selection work completes after newer input | Results, display text, callback, and active descendant reflect only the newest durable ID | Discard stale work without clearing valid state |
| Interaction/event | Keyboard, pointer, clear, or custom-element selection | Current ARIA/focus behavior and one bounded `party-selected` event remain intact | No duplicate event, focus steal, or transient-label persistence |

</frozen-after-approval>

## Code Map

- `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md:101` -- authoritative G4 gate; A is undelivered.
- `references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Contracts.UI/PublicAPI.Shipped.txt:3` -- public baseline without an entity picker.
- `src/Hexalith.Parties.Picker/Components/PartyPicker.razor:5` -- markup; parameters near 112, event near 399, ordering guards near 265/479, keyboard logic near 610.
- `src/Hexalith.Parties.Picker/wwwroot/hexalith-parties-picker.js:7` and `Hexalith.Parties.Picker.csproj:8` -- adopter bridge and package boundary.
- `src/Hexalith.Parties.AdminPortal/Components/CreateEditPartyPage.razor:118` and `wwwroot/party-form-picker.js:1` -- Admin consumer bridge.
- `tests/Hexalith.Parties.Picker.Tests/Components/PartyPickerComponentTests.cs:23` and `tests/e2e/specs/party-picker.spec.ts:20` -- component/browser parity baseline.

## Tasks & Acceptance

**Execution:**
- [ ] Prerequisite matrix and `sprint-status.yaml` -- verify ordered G4 receipts/identity; on failure set Story 8.9 `blocked` and stop.
- [ ] `src/Hexalith.Parties.Picker/Hexalith.Parties.Picker.csproj` -- consume the approved entity-picker surface in both dependency modes.
- [ ] `src/Hexalith.Parties.Picker/Components/PartyPicker.razor` plus services/JS -- adopt the shared primitive behind the stable adapter.
- [ ] Admin create/edit component/bridge -- remove only redundant behavior while preserving binding and event compatibility.
- [ ] Picker/Admin bUnit and `tests/e2e/specs/party-picker.spec.ts` -- port parity coverage and prove both dependency modes.

**Acceptance Criteria:**
- Given an incomplete or mismatched prerequisite receipt, when Story 8.9 runs, then it stops blocked and changes no production picker or rollback path.
- Given an approved FrontComposer identity, when consumers use the migrated picker, then every matrix scenario and existing adopter contract passes in both supported dependency modes.
- Given interaction, accessibility, zoom, and stale-response tests, when the picker is exercised, then WAI-ARIA state, focus, durable ID, and event behavior match the baseline.
- Given a parity failure, when rollback is exercised, then the prior local picker can be restored without changing the public package or custom-element contract.

## Spec Change Log

## Design Notes

The Parties component remains the domain adapter and owns party result mapping plus the adopter event name; FrontComposer owns generic entity-picker mechanics. G4-B through G4-E adoption and cross-RCL Fluent styling were split into the deferred-work ledger.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.Parties.Picker.Tests/Hexalith.Parties.Picker.Tests.csproj -c Release -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` -- expected: zero warnings/errors before direct xUnit v3 execution.
- `pwsh scripts/test.ps1 -Lane unit` -- expected: picker and Admin consumer regressions pass.
- `dotnet build Hexalith.Parties.slnx -c Release --no-restore -m:1` -- expected: zero warnings/errors in the selected dependency mode.
- `PLAYWRIGHT_SKIP_WEBSERVER=1 npm --prefix tests/e2e run test:a11y` -- expected: SSR accessibility checks pass; interactive picker parity runs in delegated CI.
