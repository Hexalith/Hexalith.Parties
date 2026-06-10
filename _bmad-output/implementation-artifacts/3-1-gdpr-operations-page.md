---
baseline_commit: da94d758fea8cfb6a1c57347c0c25fe30530b310
---

# Story 3.1: GDPR operations page

Status: done

<!-- Note: Validation completed during create-story. Run dev-story for implementation. -->

## Story

As a DPO,
I want a single GDPR page on a party,
so that I can reach all data-subject operations in one place.

## Acceptance Criteria

1. Given a loaded party detail, when I activate the GDPR entry point, then the app navigates to `/admin/parties/{id}/gdpr` with the safe route party id, preserves Admin authorization, and does not place tenant-scoped ids, display names, contact values, identifiers, or reason text into the URL.
2. Given a direct request to `/admin/parties/{id}/gdpr`, when the user is authorized by the `Admin` policy and tenant context is available, then the page loads that party through the existing AdminPortal query path and renders the GDPR operations surface as the primary destination for that route.
3. Given the GDPR route renders, when party data is loaded, then it reuses the existing `PartyGdprOperationsPanel` and its child panels for operational summary, erasure status, restriction actions, consent management, portability export, processing records, and the D7-gated verification report fallback; it must not reimplement those operation controls or call lower-level clients directly from Razor.
4. Given the requested party is partial, missing, gone, erased, unauthorized, unauthenticated, or tenant context is unavailable, when the GDPR route resolves, then the UI shows the existing bounded state/tombstone/access message with no PII leak and no GDPR mutation controls enabled.
5. Given any `AdminPortalGdprOutcome` from the route's operations, when it is announced, then it maps through the canonical `StatusKind`/politeness contract: accepted/completed/status/degraded states are `role="status" aria-live="polite"` and validation/transient/load failures are `role="alert"`/assertive; raw problem details, parser details, correlation payloads, and party data are never echoed.
6. Given the D7 EventStore erasure-verification contract is still unavailable, when the GDPR route is used, then all supported provisional operations remain usable and the verification/certificate area degrades to the bounded "contract unavailable" state without faulting the page. This story must not implement the EventStore contract or cross-submodule changes.
7. Given the route is exercised by keyboard, screen reader, phone viewport, or 200% zoom, when the GDPR surface opens, then focus lands on the GDPR page/operations heading, routine operation updates announce without stealing focus, blocking failures are focusable/assertive, and there is no horizontal overflow at 320px width.

## Tasks / Subtasks

- [x] Wire the GDPR route as an intentional party-scoped destination (AC: 1, 2, 7)
  - [x] Keep `PartiesAdminPortalManifest.GdprRoute` as `/admin/parties/{partyId}/gdpr`.
  - [x] Add or adjust the GDPR entry action from the detail surface so it navigates to the manifest route using `Uri.EscapeDataString` and `AdminPortalRouteIds` safety rules.
  - [x] Ensure direct route entry loads only through `AdminPortalPartyQueryService` / `IPartiesAdminPortalApiClient.GetPartyAsync` and rejects unsafe route ids without a fetch.
  - [x] Make the route land focus on the GDPR heading/primary destination instead of the list toolbar or generic detail heading.

- [x] Reuse the existing GDPR operation surface instead of duplicating it (AC: 3, 6)
  - [x] Reuse `PartyGdprOperationsPanel` and current child panels; do not create parallel erasure/restriction/consent/export/records controls.
  - [x] Preserve `AdminPortalGdprStateCoordinator` behavior for tenant switches, party switches, sign-out, authorization failure, erased terminal state, request versioning, and stale response suppression.
  - [x] Keep `AdminPortalGdprCapability.ProvisionalBridge()` semantics: supported operations enabled, erasure certificate and retry verification blocked until Story 3.5.
  - [x] Do not reference `Hexalith.Parties.UI` from `Hexalith.Parties.AdminPortal`; if host-owned `StatusKind` primitives are needed, map locally or promote only through a neutral package in a separate architecture decision.

- [x] Apply the canonical outcome and accessibility behavior (AC: 5, 7)
  - [x] Map `AdminPortalGdprOutcome.Accepted` and `Completed` to polite status announcements.
  - [x] Map `ValidationRejected`, `TransientFailure`, `Unknown`/load failures, and hard failures to assertive alert semantics.
  - [x] Preserve in-memory-only typed confirmation and bounded operation messages; do not log, persist, route, or telemetry-send typed names, reason text, contact values, identifiers, display names, raw problem details, or parser exception details.
  - [x] Ensure routine optimistic/accepted operation messages do not steal focus; blocking failures and route load failures remain keyboard reachable and assertive.

- [x] Preserve bounded non-happy paths (AC: 4, 6)
  - [x] Unauthenticated users challenge sign-in with the full GDPR return URL preserved.
  - [x] Non-admin users and missing tenant context fail closed without fetching party data.
  - [x] Missing/gone/erased parties render the existing no-PII detail/tombstone behavior and disable mutations.
  - [x] D7-unavailable certificate/report operations render the bounded blocker while other provisional operations remain available.

- [x] Extend tests at the existing seams (AC: 1-7)
  - [x] Add bUnit coverage in `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs` for the detail GDPR action navigating to `/admin/parties/{id}/gdpr`, direct-route focus/primary destination, and no unsafe id fetch.
  - [x] Add bUnit coverage that `AdminPortalGdprOutcome` announcements have the expected role/politeness for accepted/completed vs validation/transient/failure outcomes.
  - [x] Keep/extend existing capability tests for unavailable, partial, malformed, null, tenant-switch, and provisional-bridge states.
  - [x] Add or extend Playwright coverage in `tests/e2e/specs/admin-parties-list.spec.ts` or a focused GDPR spec for direct `/admin/parties/{id}/gdpr`, auth return URL, focus, no browser-visible `/api/v1/commands` or `/api/v1/queries`, and 320px/200% zoom overflow.
  - [x] Run the focused unit/component tests and Playwright typecheck/spec discovery; record any browser runtime limitation separately from typecheck success.

## Dev Notes

### Current Implementation State

- `PartiesAdminPortal.razor` already declares `@page "/admin/parties/{RoutePartyId}/gdpr"` and currently renders `PartyGdprOperationsPanel` inside the detail aside when a full detail is loaded. Treat this as prior work to refine, not as permission to fork the GDPR UI. [Source: src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor]
- `PartyGdprOperationsPanel.razor` already owns the operation coordinator, capability probe, command/query execution, export download, processing records, erasure status/certificate fallback, and stale-operation guards. Reuse this component. [Source: src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor]
- `IPartiesAdminPortalApiClient` is the AdminPortal boundary. It wraps query/list/detail/create/edit and GDPR operations. Razor components should not inject `IAdminPortalGdprClient`, `IPartiesQueryClient`, or `IPartiesCommandClient` directly for this route. [Source: src/Hexalith.Parties.AdminPortal/Services/IPartiesAdminPortalApiClient.cs]
- `PartiesAdminPortalApiClient` maps GDPR client failures to `AdminPortalGdprOutcome` / `AdminPortalQueryFailureKind` and reports the provisional bridge honestly. Preserve that transport boundary. [Source: src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs]

### Architecture and Scope Guardrails

- Epic 3 covers Admin GDPR/DPO operations via the existing AdminPortal and `IAdminPortalGdprClient`; Story 3.1 is the route/page wrapper, not the typed-name erase dialog, export implementation, processing records detail, or D7 backend contract. [Source: _bmad-output/planning-artifacts/epics.md#Story-3.1-GDPR-operations-page]
- The EventStore erasure-verification contract remains Story 3.5 and is approval-gated. In Story 3.1, contract-unavailable must degrade cleanly; do not edit `Hexalith.EventStore` or add cross-submodule contract work. [Source: _bmad-output/planning-artifacts/epics.md#Story-3.5-EventStore-erasure-verification-contract]
- AdminPortal is an adopter-facing RCL and must remain host-agnostic. Do not add a project reference from `Hexalith.Parties.AdminPortal` to `Hexalith.Parties.UI`; current tests explicitly guard this. [Source: tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalAuthorizationTests.cs]
- The architecture's old file tree mentioned separate Admin pages, but Epic 2 implementation deliberately reused `PartiesAdminPortal.razor`. Continue that pattern unless this story makes a small extraction that preserves behavior and avoids duplicate state. [Source: _bmad-output/implementation-artifacts/epic-2-retro-2026-06-10.md#Significant-Discoveries-Affecting-Epic-3]
- The browser must talk only to the UI host; no public browser command/query calls to the actor host or EventStore gateway. [Source: _bmad-output/planning-artifacts/architecture.md#Architectural-Boundaries]

### Status and Accessibility Requirements

- Canonical status vocabulary and politeness live in `Hexalith.Parties.UI.Status`, but AdminPortal currently cannot reference the UI host. If local mapping is needed in AdminPortal, keep it behaviorally aligned and covered by tests rather than adding an RCL-to-host dependency. [Source: src/Hexalith.Parties.UI/Status/StatusPresentation.cs]
- Required mapping for this story: `Accepted`/`Completed`/degraded route states use polite status; `ValidationRejected`, transient failure, load failure, and unknown hard failures use alert/assertive. `AuthenticationRequired` should route/challenge, not render an in-place live region. [Source: _bmad-output/planning-artifacts/architecture.md#Communication-Patterns]
- Existing GDPR panel currently focuses the operations heading after any outcome. Adjust only if needed to honor the split: routine success should be announced without disruptive focus movement; blocking failures should remain reachable/assertive. [Source: src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor]
- Preserve forced-colors/reduced-motion and 320px/200% zoom behavior from Epic 2; Story 3.1 adds the focused GDPR route to that matrix. [Source: _bmad-output/planning-artifacts/epics.md#NFR1-Accessibility]

### Privacy and Security Requirements

- Never expose PII in logs, telemetry, route values, links, tombstones, operation copy, raw exception messages, or export filenames. [Source: _bmad-output/project-context.md#Critical-Dont-Miss-Rules-anti-patterns--gotchas]
- Route ids must be safe party ids only. Existing tests reject tenant-scoped ids and path traversal; extend those tests for the GDPR route if the implementation changes routing. [Source: tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs]
- Erased parties must show tombstone/bounded state and never display stale names, contact values, identifiers, destroyed-key text, crypto exception text, or raw payloads. [Source: _bmad-output/planning-artifacts/epics.md#GDPR-backend-behaviors]

### Files Likely to Change

- `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor` - existing route/detail shell, route selection, navigation, focus behavior.
- `src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalManifest.cs` - route constants if the entry/action needs helper methods.
- `src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor` - only for outcome announcement/focus semantics or route-specific polish; do not duplicate operations.
- `src/Hexalith.Parties.AdminPortal/Services/AdminPortalLabels.cs` - localized/bounded labels only if new route-entry text is required.
- `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs` - primary bUnit coverage.
- `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalAuthorizationTests.cs` - route/auth/host-reference guardrails.
- `tests/e2e/specs/admin-area-authorization.spec.ts` and `tests/e2e/specs/admin-parties-list.spec.ts` or a new focused GDPR spec - browser-level route/auth/focus/overflow coverage.

### Testing Standards

- Use xUnit v3, Shouldly, NSubstitute, and bUnit. Do not introduce Moq, FluentAssertions, or raw `Assert.*`. [Source: _bmad-output/project-context.md#Testing-Rules]
- Use `scripts/test.ps1 -Lane unit` for the normal focused lane where possible. If the sandbox blocks MTP or browser runtime, run the built test executable/typecheck that matches the established Epic 2 workaround and document the limitation. [Source: _bmad-output/project-context.md#Development-Workflow-Rules]
- Keep Playwright runtime evidence distinct from Playwright typecheck/spec discovery. Epic 2 found local browser execution can be blocked by socket permissions. [Source: _bmad-output/implementation-artifacts/epic-2-retro-2026-06-10.md#Action-Items]

### Recent Work Intelligence

- Last commits delivered Epic 2 docs sync and Story 2.5 picker modernization. The AdminPortal route/component pattern, safe routing, stale/degraded rendering, Playwright fixtures, and extensive GDPR panel tests are already present; build on them. [Source: git log -5]
- Epic 2 explicitly committed to not creating duplicate Admin list/detail/GDPR pages unless a refactor story deliberately replaces current behavior. Story 3.1 should be a focused route/page completion, not a broad AdminPortal restructure. [Source: _bmad-output/implementation-artifacts/epic-2-retro-2026-06-10.md#Commitments]

### Project Structure Notes

- Align with existing `src/Hexalith.Parties.AdminPortal/Components` and `Services` layout.
- Keep generated output under `obj/**/generated` untouched and uncommitted.
- Preserve Central Package Management: no `Version=` attributes in project files.
- No new NuGet package is expected for this story; the relevant stack is already pinned locally.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-3.1-GDPR-operations-page]
- [Source: _bmad-output/planning-artifacts/architecture.md#API--Communication-Patterns]
- [Source: _bmad-output/planning-artifacts/architecture.md#Project-Structure--Boundaries]
- [Source: _bmad-output/project-context.md#Technology-Stack--Versions]
- [Source: _bmad-output/implementation-artifacts/epic-2-retro-2026-06-10.md#Next-Epic-Preview-Epic-3-Admin---GDPR--DPO-Operations]

## Validation Summary

- Source discovery loaded project context, sprint status, `epics.md`, `architecture.md`, and existing Epic 2 retrospective.
- Relevant existing code and tests were inspected: `PartiesAdminPortal.razor`, `PartyGdprOperationsPanel.razor`, AdminPortal GDPR services/client boundary, AdminPortal bUnit tests, UI status primitives, and Playwright admin specs.
- Checklist fixes applied before finalizing: clarified no duplicate GDPR operation controls, no EventStore D7 work, no AdminPortal -> UI host reference, explicit outcome politeness requirements, and explicit focus/phone/zoom test expectations.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-06-10: Added GDPR route/action tests first; initial direct `dotnet test`/`scripts/test.ps1 -Lane unit` hit an MTP/project-reference graph failure with zero emitted errors under parallel MSBuild.
- 2026-06-10: Re-ran builds with `-m:1`; AdminPortal test project built successfully. Full solution build with `-m:1` failed on unrelated `Hexalith.PolymorphicSerializations` package warnings-as-errors (`NU5118`, `NU5128`).
- 2026-06-10: Ran compiled xUnit v3 AdminPortal test assembly directly to bypass the MTP build target.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Added a safe GDPR operations action on the party detail surface that navigates to `PartiesAdminPortalManifest.GdprRoute` only when the selected party id satisfies `AdminPortalRouteIds`.
- Direct `/admin/parties/{id}/gdpr` requests now keep the existing AdminPortal query path, reject unsafe route ids before fetch, and focus the reused GDPR operations heading as the route's primary destination.
- Reused `PartyGdprOperationsPanel` and child panels; no EventStore D7 contract or cross-submodule work was added.
- Split GDPR operation announcements into polite `role="status"` for accepted/completed/degraded states and assertive focusable `role="alert"` for validation/transient/hard failures; validation-rejected command correlations are not echoed into the page.
- Review fix: direct GDPR routes that resolve to partial party details now render the bounded unavailable state instead of the partial party display name and never render GDPR mutation controls.
- Review fix: GDPR route heading autofocus resets when the party changes or when the panel leaves and re-enters route-primary mode.
- Review fix: the GDPR operation live region starts empty instead of announcing a false generic failure before any operation has run.
- Review fix: assertive GDPR command failures now clear any prior successful command correlation so stale operation metadata is not shown beside validation/transient/hard failures.
- Added bUnit coverage for GDPR action routing, unsafe GDPR route ids, direct route primary destination, and outcome role/politeness semantics.
- Added bUnit coverage for bounded partial GDPR route rendering with no requested-party display name or GDPR mutation controls.
- Added bUnit regression coverage that a validation-rejected GDPR command clears the previous command correlation and never echoes the rejected correlation id.
- Added Playwright coverage for direct GDPR route focus/no browser-visible command-query URLs/320px plus 200% zoom overflow and for detail action navigation.
- Validation passed for `Hexalith.Parties.AdminPortal.Tests` (150 tests), E2E TypeScript typecheck, and Playwright spec discovery. Focused Playwright runtime remains blocked by sandbox socket permissions (`System.Net.Sockets.SocketException (13): Permission denied`). Full solution build remains blocked by unrelated submodule packaging warnings in `Hexalith.PolymorphicSerializations`.

### File List

- src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor
- src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor
- tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs
- tests/Hexalith.Parties.AdminPortal.Tests/Services/RecordingAdminPortalApiClient.cs
- tests/e2e/specs/admin-parties-list.spec.ts
- _bmad-output/implementation-artifacts/3-1-gdpr-operations-page.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- _bmad-output/implementation-artifacts/tests/test-summary.md

### Change Log

- 2026-06-10: Implemented Story 3.1 GDPR route/page completion, primary destination focus, canonical outcome announcements, and route/action tests.
- 2026-06-10: Senior developer review auto-fixed bounded partial GDPR route rendering, reusable panel autofocus reset, and empty initial GDPR live-region behavior; validation now passes 149 AdminPortal component tests plus E2E typecheck/spec discovery.
- 2026-06-10: Story automator review auto-fixed stale GDPR command correlation cleanup for assertive failures; validation now passes 150 AdminPortal component tests plus E2E typecheck/spec discovery.

## Senior Developer Review (AI)

Reviewer: Administrator on 2026-06-10

Outcome: Approved after automatic fixes. No critical issues remain.

### Findings Fixed

- HIGH: Direct `/admin/parties/{id}/gdpr` could render a partial party detail header using the partial display name, violating AC4's bounded/no-PII route behavior for partial parties. Fixed in `PartiesAdminPortal.razor` by rendering the bounded unavailable state for GDPR-route partial details, and added a bUnit regression test.
- MEDIUM: `PartyGdprOperationsPanel` only focused the route-primary operations heading once per component lifetime. A later party switch or detail-to-GDPR route toggle could miss the AC7 focus contract. Fixed by resetting route autofocus state when the party changes or the panel exits route-primary mode.
- MEDIUM: The GDPR operation live region rendered "Operation failed" before any operation had run because null outcome fell through to the failure label. Fixed by rendering an empty initial live-region message while preserving `Unknown` as the bounded failure outcome.
- MEDIUM: A validation/transient/hard GDPR command failure suppressed the new failure correlation id but could leave a previous successful command correlation visible beside the assertive failure. Fixed in `PartyGdprOperationsPanel.razor` by clearing correlation state for assertive/no-correlation command outcomes and `AdminPortalQueryException` failures, with a bUnit regression test.

### Review Validation

- `dotnet build tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj -c Release --no-restore -m:1` passed.
- `DiffEngine_Disabled=true tests/Hexalith.Parties.AdminPortal.Tests/bin/Release/net10.0/Hexalith.Parties.AdminPortal.Tests` passed: 150 tests.
- `cd tests/e2e && npm run typecheck` passed.
- `cd tests/e2e && npx playwright test specs/admin-parties-list.spec.ts --list` passed: 18 specs discovered.
- `cd tests/e2e && npx playwright test specs/admin-parties-list.spec.ts --project=chromium --grep "direct GDPR|detail GDPR action"` remains blocked before test execution by sandbox Kestrel socket binding permission.
- Web fallback documentation check used official Microsoft Blazor lifecycle/`FocusAsync` docs and WAI-ARIA 1.2 alert/status role definitions to validate the focus/live-region behavior.
