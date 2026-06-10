---
baseline_commit: ef935bbb84d6
---

# Story 3.2: Erase a party with typed-name confirmation

Status: done

<!-- Note: Validation completed during create-story. Run dev-story for implementation. -->

## Story

As a DPO,
I want to erase a party behind a typed-name confirmation,
so that I never erase accidentally and the action is honest.

## Acceptance Criteria

1. Given the DPO opens the Erase action from the existing GDPR operations surface, when the action starts, then a FluentUI modal dialog opens with `role="dialog"`, `aria-modal="true"`, an accessible title wired by `aria-labelledby`, and focus inside the dialog; it must not use native `alert`, native `confirm`, or a second modal layer.
2. Given the dialog is open, when it renders, then it contains a real labeled typed-confirm input whose accessible description is tied with `aria-describedby` to the irreversibility warning, and the typed value exists only in component/dialog memory.
3. Given the typed value does not exactly match the selected party display name, when the dialog is evaluated, then the danger-fill Erase button remains disabled and also exposes disabled semantics to assistive tech (`aria-disabled` or the Fluent-rendered equivalent); no erase command is issued.
4. Given the typed value exactly matches the selected party display name, when the match first enables the Erase button, then the enable transition is announced through a polite live region without moving focus away from the input.
5. Given the typed value matches and the DPO confirms, when the command runs, then `IPartiesAdminPortalApiClient.RequestErasureAsync(partyId, token)` is the only erase command path used; the dialog closes safely, stale-operation guards remain in force, and the typed name is never logged, persisted, sent to telemetry, placed in the URL, included in EventStore links, included in correlation displays, or passed to any client/service method.
6. Given `RequestErasureAsync` returns `Accepted`, when the UI updates optimistically, then the erasure state moves toward pending/erased using the existing GDPR panel status path, the party state/freshness messaging includes "Saved - updating..." or equivalent accepted-processing copy, the freshness/status state is degraded or pending until projection confirmation, and the acknowledgement uses neutral/info presentation, never success-green.
7. Given erasure is accepted, in-progress, rejected, forbidden, not found, erased, contract-unavailable, missing-tenant, or transiently failed, when the outcome is announced, then the existing `AdminPortalGdprOutcome` mapping and politeness split are preserved: accepted/completed/degraded states use `role="status" aria-live="polite"`, while validation/transient/load/unknown failures use `role="alert"`/assertive and remain focusable.
8. Given the party is partial, missing, gone, already erased, erasure pending, unauthorized, unauthenticated, or tenant context is unavailable, when the GDPR surface renders or changes state, then the Erase action and dialog are unavailable and the UI shows only bounded no-PII state; it must not reveal display names, contact values, identifiers, destroyed-key text, raw problem details, parser details, typed-confirm values, or reason text.
9. Given the dialog is used by keyboard, screen reader, phone viewport, forced-colors, reduced-motion, or 200% zoom, when the DPO opens, cancels, or confirms erasure, then focus is trapped/restored by the dialog behavior, Escape/Cancel takes the safe path, controls remain visible without horizontal overflow at 320px width, and the required target sizes and focus indicators are preserved.

## Tasks / Subtasks

- [x] Replace the inline erase confirmation with an accessible typed-confirm dialog (AC: 1, 2, 3, 4, 9)
  - [x] Keep `PartyGdprOperationsPanel` as the owning surface for the erasure flow; do not create a parallel GDPR page, duplicate erasure panel, or lower-level Razor client call.
  - [x] Use the pinned FluentUI Blazor dialog surface (`IDialogService`/`FluentDialogBody` pattern or a local `FluentDialog` integration) and verify the rendered semantics include modal dialog behavior, an accessible title, and one modal depth.
  - [x] Add a dialog component or tightly scoped child component only if it keeps `PartyGdprOperationsPanel.razor` readable; keep it under `src/Hexalith.Parties.AdminPortal/Components`.
  - [x] Add a real labeled typed-confirm input, an irreversibility warning description, a polite enablement live region, Cancel, and a danger/primary Erase action.
  - [x] Require an exact ordinal match against the currently loaded party display name and clear the typed value on cancel, confirm, party switch, capability close, disposal, and dialog close.

- [x] Preserve the existing erase command boundary and stale-operation protections (AC: 5, 7, 8)
  - [x] Continue routing erasure through `QueryService.ApiClient.RequestErasureAsync(partyId, token)` in `PartyGdprOperationsPanel`.
  - [x] Preserve the existing `IsCurrentOperation`, `capabilityVersion`, `TenantContextSignature`, cancellation-token, and `_isBusy` guards so an old dialog/result cannot mutate a new party.
  - [x] Do not add typed-name parameters to `IPartiesAdminPortalApiClient`, `IAdminPortalGdprClient`, `HttpAdminPortalGdprClient`, `EraseParty`, EventStore command payloads, logs, or tests' fake API request captures.
  - [x] Preserve correlation-id behavior from Story 3.1: accepted command correlations may display, assertive failures must clear stale/sensitive correlations.

- [x] Add honest accepted-processing and erased/pending state behavior (AC: 6, 7, 8)
  - [x] On accepted erasure, close the dialog, clear the typed value, set the operation announcement to accepted-processing, refresh erasure status through the existing `RefreshErasureStatusAsync(allowWhileBusy: true)` path, and do not show success-green styling.
  - [x] If projection confirmation is not current yet, keep the UI in pending/degraded status rather than implying completed erasure.
  - [x] Keep `CanRequestErasure` false for erased parties and in-progress statuses (`ErasurePending`, `KeyDestroyed`, `VerificationInProgress`, `Verified`) and keep other mutations disabled where existing GDPR state rules require it.

- [x] Keep copy localized/bounded and PII-safe (AC: 2, 5, 6, 8)
  - [x] Extend `AdminPortalLabels` for dialog title, input label, warning text, typed-match help, enablement announcement, and neutral accepted-processing acknowledgement.
  - [x] Avoid echoing the display name in warning/copy unless it is strictly needed for the typed-confirm contract; the only allowed use of the display name is comparing against the input and, if shown as confirmation target, it must never appear in route, logs, telemetry, EventStore links, correlations, or command payloads.
  - [x] Do not include raw problem details, parser exceptions, destroyed-key text, typed input, contact values, identifiers, tenant-scoped ids, or reason text in visible error/copy surfaces.

- [x] Extend focused bUnit/component tests at the existing seams (AC: 1-9)
  - [x] Update `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs` so the erasure test asserts a modal dialog, accessible title, labeled input, `aria-describedby`, disabled Erase until exact typed match, polite enablement announcement, safe cancel, and one `api.ErasureRequests` entry only after confirm.
  - [x] Add tests for mismatch/no command, value cleared on cancel/party switch/capability close, partial/missing/erased/pending states with no dialog or mutation controls, and typed value never present in markup after close.
  - [x] Keep/adjust existing outcome tests for accepted and validation-rejected erasure so they type the matching value before confirm and continue asserting role/politeness/correlation behavior.
  - [x] Update `RecordingAdminPortalApiClient` only if needed for assertions, without adding any typed-name capture field.

- [x] Extend browser-level coverage where this repo already verifies Admin GDPR behavior (AC: 1, 3, 4, 5, 9)
  - [x] Add or extend `tests/e2e/specs/admin-parties-list.spec.ts` to cover direct GDPR route erase dialog semantics, disabled-until-match, no browser-visible `/api/v1/commands` or `/api/v1/queries`, safe cancel, accepted command through the UI fixture, 320px and 200% zoom overflow, and no native dialog events.
  - [x] Keep Playwright runtime evidence distinct from TypeScript typecheck/spec discovery if local socket binding remains blocked.

- [x] Validate with focused test lanes and record limitations (AC: 1-9)
  - [x] Run the focused AdminPortal test project in Release with `--no-restore -m:1` if the broader lane hits the known MTP/submodule issue.
  - [x] Run the compiled xUnit v3 executable fallback if `dotnet test`/MTP is blocked, matching Story 3.1 evidence.
  - [x] Run E2E TypeScript typecheck and Playwright spec discovery; run the focused Playwright spec if the environment permits browser/server socket binding.

## Dev Notes

### Current Implementation State

- `PartyGdprOperationsPanel.razor` currently owns GDPR operations and already routes erasure through `QueryService.ApiClient.RequestErasureAsync(partyId, token)`, followed by `RefreshErasureStatusAsync(allowWhileBusy: true)`. Keep this path; replace only the inline `_confirmErasure` two-button confirmation with the typed-confirm dialog behavior. [Source: src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor]
- The current erase confirmation is inline, shows the party id, has no typed input, and enables `Confirm erasure` immediately once confirmation state is open. This is the specific gap Story 3.2 closes. [Source: src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor#erasure-actions-heading]
- `IPartiesAdminPortalApiClient` is the AdminPortal boundary for erasure. Razor must not inject `IAdminPortalGdprClient`, `IPartiesCommandClient`, or gateway/query clients directly. [Source: src/Hexalith.Parties.AdminPortal/Services/IPartiesAdminPortalApiClient.cs]
- `PartiesAdminPortalApiClient.RequestErasureAsync` delegates to `IAdminPortalGdprClient.RequestErasureAsync`; `HttpAdminPortalGdprClient.RequestErasureAsync` posts an `EraseParty` command containing only `PartyId` and tenant. Do not add display-name or typed-confirm fields to this transport. [Source: src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs] [Source: src/Hexalith.Parties.Client/AdminPortal/HttpAdminPortalGdprClient.cs]
- The aggregate handler for `EraseParty` is already idempotent for erased and in-progress erasure states and emits `ErasePartyRequested` for a normal accepted request. Story 3.2 is UI safety/honesty around issuing that existing command, not a backend erasure-state rewrite. [Source: src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs]

### Architecture and Scope Guardrails

- Epic 3 wraps existing AdminPortal GDPR panels and uses `IAdminPortalGdprClient`; Story 3.2 implements the typed-name erase confirmation only. Restrict/lift, consent, export, processing records, and verification report completion remain Stories 3.3-3.6. [Source: _bmad-output/planning-artifacts/epics.md#Story-3.2-Erase-a-party-with-typed-name-confirmation]
- Do not start Story 3.5's EventStore erasure-verification contract or edit `Hexalith.EventStore`. The D7 certificate/retry-verification gap remains approval-gated and must continue to degrade cleanly. [Source: _bmad-output/planning-artifacts/epics.md#Story-3.5-EventStore-erasure-verification-contract]
- AdminPortal is an adopter-facing RCL and must remain host-agnostic. Do not reference `Hexalith.Parties.UI` to reuse host-owned status/freshness primitives; keep local behavior aligned with tests or promote through a neutral package only in a separate architecture decision. [Source: _bmad-output/implementation-artifacts/epic-2-retro-2026-06-10.md#Commitments]
- The browser talks only to the UI host/BFF. Do not add browser-visible EventStore gateway calls or public actor-host endpoints. [Source: _bmad-output/planning-artifacts/architecture.md#API--Communication-Patterns]

### Dialog and Accessibility Requirements

- The repo pins `Microsoft.FluentUI.AspNetCore.Components` to `5.0.0-rc.3-26138.1`; do not bump package versions and do not add `Version=` attributes to project files. [Source: Directory.Packages.props]
- FrontComposer already uses the FluentUI dialog service pattern with `IDialogService.ShowDialogAsync<T>()` and `FluentDialogBody`; use that as a local implementation reference, but this story's typed-name input and dialog semantics are stricter than the generic destructive confirmation. [Source: Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Components/Forms/FcDestructiveConfirmationDialog.razor] [Source: Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Components/Forms/FcDestructiveConfirmationDialog.razor.cs]
- FluentUI's local package docs state modal dialogs are modal by default and block keyboard/mouse input outside the dialog until hidden/closed; verify the rendered result rather than assuming semantics from component names alone. [Source: /home/administrator/.nuget/packages/microsoft.fluentui.aspnetcore.components/5.0.0-rc.3-26138.1/lib/net10.0/Microsoft.FluentUI.AspNetCore.Components.xml#FluentDialog.Modal]
- Accessibility contract: irreversible destructive actions require exact typed confirmation before the confirming action is enabled; routine status/freshness/processing updates are polite; validation/failure/load errors are assertive. [Source: docs/accessibility.md#Component-Semantics]
- Required UX detail: typed-erase confirm must be a real labeled input, tied to the irreversibility warning with `aria-describedby`; erase is disabled until the name matches and the enable transition is announced. [Source: _bmad-output/planning-artifacts/epics.md#UX-DR9-Real-semantics-no-interactive-divs]

### Privacy and GDPR Honesty Requirements

- The typed confirmation is compared in memory only. Never log event payloads or PII, and never put typed names, display names, contact values, identifiers, tenant-scoped ids, reason text, raw problem details, parser details, destroyed-key text, or crypto exception text into logs, traces, telemetry, route values, links, tombstones, or user-facing failure copy. [Source: _bmad-output/project-context.md#Critical-Dont-Miss-Rules-anti-patterns--gotchas]
- Erasure copy must commit to the start of the obligation, not promise completion; completed erasure is permanent, and the acknowledgement must use neutral/info tone rather than success-green. [Source: _bmad-output/planning-artifacts/epics.md#UX-DR13-Erasure-copy]
- Erased parties remain distinguishable from missing/inactive/restricted/key-failure states, and erased responses must not expose destroyed-key messages, cryptographic exception text, stale names, contact values, identifiers, or raw command/query payloads. [Source: docs/gdpr-erased-party-status.md]
- Crypto-shredding is terminal and distinct from tenant/party key rotation. Do not describe erasure as reversible after it has completed. [Source: docs/gdpr-key-rotation-and-shredding.md]

### Previous Story Intelligence

- Story 3.1 completed the GDPR route and reviewed/fixed several guardrails that must remain intact: direct GDPR routes must not render partial party display names, GDPR operation live region starts empty, assertive failures clear stale command correlations, and route-primary focus resets across party changes. [Source: _bmad-output/implementation-artifacts/3-1-gdpr-operations-page.md#Senior-Developer-Review-AI]
- Story 3.1 validation passed 150 AdminPortal tests, E2E TypeScript typecheck, and Playwright spec discovery. Focused Playwright runtime was blocked locally by sandbox Kestrel socket permissions; keep this distinction in Story 3.2 records. [Source: _bmad-output/implementation-artifacts/3-1-gdpr-operations-page.md#Review-Validation]
- Epic 2 established the implementation reality: reuse `PartiesAdminPortal.razor` and existing AdminPortal panels rather than creating parallel Admin pages. [Source: _bmad-output/implementation-artifacts/epic-2-retro-2026-06-10.md#Significant-Discoveries-Affecting-Epic-3]

### Files Likely to Change

- `src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor` - primary erasure dialog trigger, typed-confirm state cleanup, accepted-processing announcement, command execution, status refresh, and stale-operation guard preservation.
- `src/Hexalith.Parties.AdminPortal/Components/*Erasure*Dialog*.razor` or similar - optional new focused dialog component if the typed-confirm UI should not live inline in `PartyGdprOperationsPanel`.
- `src/Hexalith.Parties.AdminPortal/Services/AdminPortalLabels.cs` - bounded/localizable dialog strings and accepted-processing text.
- `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor.css` or component CSS - only if needed for dialog content layout, danger affordance, forced-colors, and 320px/200% zoom behavior.
- `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs` - primary bUnit coverage for typed-confirm dialog, disabled-until-match, outcome mapping, privacy, and state cleanup.
- `tests/Hexalith.Parties.AdminPortal.Tests/Services/RecordingAdminPortalApiClient.cs` - only for non-sensitive test helpers; do not capture typed names.
- `tests/e2e/specs/admin-parties-list.spec.ts` - browser-level GDPR erasure dialog/focus/overflow/native-dialog/no-browser-gateway coverage.

### Testing Standards

- Use xUnit v3, Shouldly, NSubstitute, and bUnit. Do not introduce Moq, FluentAssertions, or raw `Assert.*`. [Source: _bmad-output/project-context.md#Testing-Rules]
- Use `scripts/test.ps1 -Lane unit` where possible. If sandbox/MTP/project-reference issues recur, run the focused AdminPortal test project build and the compiled xUnit v3 executable as Story 3.1 did, and record the limitation. [Source: _bmad-output/implementation-artifacts/3-1-gdpr-operations-page.md#Debug-Log-References]
- Keep Playwright runtime evidence distinct from typecheck/spec discovery; local socket binding may be blocked while CI/browser environments can still run the spec. [Source: _bmad-output/implementation-artifacts/epic-2-retro-2026-06-10.md#Action-Items]

### Project Structure Notes

- Keep work inside the existing `Hexalith.Parties.AdminPortal` RCL and its tests. Do not reference the UI host from AdminPortal.
- Keep generated output under `obj/**/generated` untouched and uncommitted.
- No new NuGet package is expected. Central Package Management remains authoritative.
- Preserve CRLF/final-newline/editorconfig conventions when editing existing C# and Razor files.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-3.2-Erase-a-party-with-typed-name-confirmation]
- [Source: _bmad-output/planning-artifacts/architecture.md#Communication-Patterns]
- [Source: _bmad-output/planning-artifacts/architecture.md#Process-Patterns]
- [Source: _bmad-output/project-context.md#Technology-Stack--Versions]
- [Source: docs/accessibility.md#Component-Semantics]
- [Source: docs/gdpr-erased-party-status.md]
- [Source: _bmad-output/implementation-artifacts/3-1-gdpr-operations-page.md#Previous-Story-Intelligence]

## Validation Summary

- Source discovery loaded project context, sprint status, `epics.md`, `architecture.md`, prior Story 3.1, Epic 2 retrospective, GDPR docs, accessibility docs, existing AdminPortal components/services/tests, FrontComposer dialog examples, pinned FluentUI package metadata, and recent git history.
- Checklist fixes applied before finalizing: clarified no duplicate erasure workflow, no EventStore D7 work, no AdminPortal-to-UI host reference, exact typed-name match, typed value memory-only handling, accepted-processing neutral tone, existing outcome politeness preservation, previous Story 3.1 regression guardrails, and concrete bUnit/Playwright seams.
- Web research for official FluentUI dialog docs did not return useful official results in this environment; local pinned package XML and in-repo FrontComposer dialog patterns were used instead.

## Senior Developer Review (AI)

### Review Findings

- HIGH: An already-open erase dialog stayed rendered when an authoritative erasure-status refresh moved the party to `ErasurePending`/terminal erasure, leaving stale typed-confirm UI available even though erasure had become unavailable. Fixed by treating pending/complete/erased status values as erasure-unavailable states and clearing dialog state after status refresh.
- HIGH: Lift restriction and revoke consent remained enabled after erasure entered an in-progress state because they did not check `IsErasurePending`. Fixed both mutation guards and added regression coverage.
- MEDIUM: Erasure dialog layout and danger affordance styles were added to `PartiesAdminPortal.razor.css`, but the elements are rendered inside `PartyGdprOperationsPanel`; scoped CSS did not apply there. Moved the styles to `PartyGdprOperationsPanel.razor.css` and added explicit danger-fill styling with forced-colors handling.
- MEDIUM: The story File List was missing the new component-scoped CSS file and BMAD automation artifacts present in the working tree after review fixes. Updated the File List below.

### Review Validation

- `dotnet build tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj -c Release --no-restore -m:1 -v:minimal`: passed.
- `tests/Hexalith.Parties.AdminPortal.Tests/bin/Release/net10.0/Hexalith.Parties.AdminPortal.Tests -method "*RequestErasure*" -method "*GdprOutcome*" -method "*GdprOperations_AddAndRevoke*" -method "*RestrictThenLift*"`: 10 tests passed.
- `tests/Hexalith.Parties.AdminPortal.Tests/bin/Release/net10.0/Hexalith.Parties.AdminPortal.Tests`: 152 tests passed.
- `npm run typecheck` in `tests/e2e`: passed.
- `npx playwright test --list` in `tests/e2e`: discovered 31 tests.
- `bash scripts/check-no-warning-override.sh`: passed.

### Outcome

Approved after auto-fixes. No critical issues remain.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet test tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj -c Release --no-restore --no-build --filter "FullyQualifiedName~RequestErasure|FullyQualifiedName~GdprOutcome" -m:1` blocked by local .NET test IPC/socket permission (`SocketException (13): Permission denied`).
- `tests/Hexalith.Parties.AdminPortal.Tests/bin/Release/net10.0/Hexalith.Parties.AdminPortal.Tests -method "*RequestErasure*" -method "*GdprOutcome*"`: 7 tests passed.
- `tests/Hexalith.Parties.AdminPortal.Tests/bin/Release/net10.0/Hexalith.Parties.AdminPortal.Tests`: 151 tests passed.
- `dotnet build tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -c Release --no-restore -m:1 -v:minimal`: passed.
- `tests/Hexalith.Parties.UI.Tests/bin/Release/net10.0/Hexalith.Parties.UI.Tests`: 258 tests passed.
- `npm run typecheck` in `tests/e2e`: passed.
- `npx playwright test --list` in `tests/e2e`: discovered 31 tests including the updated Admin GDPR dialog scenario.
- `npx playwright test specs/admin-parties-list.spec.ts --project=chromium --grep "direct GDPR route opens operations"` blocked by local Kestrel socket binding permission (`SocketException (13): Permission denied`).
- `bash scripts/check-no-warning-override.sh`: passed.
- `dotnet build Hexalith.Parties.slnx -c Release --no-restore -m:1 -v:minimal` reached Parties/AdminPortal/UI projects but failed in unrelated `Hexalith.PolymorphicSerializations` submodule packaging warnings-as-errors (`NU5118`, `NU5128`).
- Review: `dotnet build tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj -c Release --no-restore -m:1 -v:minimal`: passed.
- Review: `tests/Hexalith.Parties.AdminPortal.Tests/bin/Release/net10.0/Hexalith.Parties.AdminPortal.Tests -method "*RequestErasure*" -method "*GdprOutcome*" -method "*GdprOperations_AddAndRevoke*" -method "*RestrictThenLift*"`: 10 tests passed.
- Review: `tests/Hexalith.Parties.AdminPortal.Tests/bin/Release/net10.0/Hexalith.Parties.AdminPortal.Tests`: 152 tests passed.
- Review: `npm run typecheck` in `tests/e2e`: passed.
- Review: `npx playwright test --list` in `tests/e2e`: discovered 31 tests.
- Review: `bash scripts/check-no-warning-override.sh`: passed.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Replaced the inline erasure confirmation with a local FluentUI modal dialog in `PartyGdprOperationsPanel`, including accessible title wiring, labeled typed-confirm input, warning description, disabled Erase button, and polite enablement announcement.
- Kept erasure on the existing `QueryService.ApiClient.RequestErasureAsync(partyId, token)` path and preserved operation/capability/tenant/cancellation guards and correlation behavior.
- Cleared typed confirmation state on cancel, confirm, party switch, capability close, disposal, and dialog close paths; no typed-name field was added to API clients, command payloads, fake captures, URLs, links, or correlations.
- Updated accepted erasure acknowledgement to neutral "Saved - updating..." copy and continued refreshing erasure status through the existing `RefreshErasureStatusAsync(allowWhileBusy: true)` path.
- Extended bUnit coverage for dialog semantics, exact-match enablement, safe cancel, cleanup, outcome politeness, and no-command mismatch behavior; extended Playwright spec and fixture capture for browser-level erasure coverage without typed-name capture.
- Review auto-fixes close the typed-confirm dialog when refreshed erasure status becomes pending/complete/erased, disable lift/revoke mutations during erasure states, and move dialog/danger styles into the owning component-scoped CSS file.

### File List

- src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor
- src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor.css
- src/Hexalith.Parties.AdminPortal/Services/AdminPortalLabels.cs
- src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs
- tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs
- tests/e2e/specs/admin-parties-list.spec.ts
- _bmad-output/implementation-artifacts/3-2-erase-a-party-with-typed-name-confirmation.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- _bmad-output/implementation-artifacts/tests/test-summary.md
- _bmad-output/story-automator/orchestration-1-20260609-205725.md

### Change Log

- 2026-06-10: Created Story 3.2 context for the Admin GDPR typed-name erasure dialog.
- 2026-06-10: Implemented Story 3.2 typed-name erasure dialog, privacy-safe command boundary, accepted-processing copy, component/browser coverage, and validation evidence.
- 2026-06-10: Senior developer review auto-fixed stale dialog closure, pending-erasure mutation guards, component-scoped danger styling, and regression coverage.
