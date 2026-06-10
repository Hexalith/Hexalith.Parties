---
baseline_commit: 6a7ebaf5f8bf
---

# Story 3.3: Restrict / lift restriction and record / revoke consent

Status: done

<!-- Note: Validation completed during create-story. Run dev-story for implementation. -->

## Story

As a DPO,
I want to restrict or lift processing and record or revoke consent on a party,
so that I can manage lawful processing.

## Acceptance Criteria

1. Given the Admin GDPR operations surface is loaded for an eligible party, when I choose Restrict processing or Lift restriction, then the existing `RestrictionActionsPanel` uses `ButtonAppearance.Outline`, opens a single in-app confirmation step for the reversible action, does not use native `alert`/`confirm`, and issues only `IPartiesAdminPortalApiClient.RestrictProcessingAsync(partyId, reason, token)` or `LiftRestrictionAsync(partyId, token)` after confirmation.
2. Given a restriction or lift command is accepted, when the UI updates, then the operation announcement uses the existing accepted-processing message (`Saved - updating...`) with `role="status" aria-live="polite"`, the party state badge reflects the expected `restricted`/not-restricted state optimistically, and the component reconciles by refreshing the current party detail through the existing `RefreshParty` path without stealing focus.
3. Given restriction/lift is rejected, forbidden, unauthenticated, tenant-unavailable, not found/gone, transiently failed, or unknown, when the outcome is mapped, then the existing `AdminPortalGdprOutcome` mapping and politeness split are preserved: validation/transient/load/hard failures use `role="alert"`/assertive, accepted/completed/degraded states remain polite, and stale success correlation ids are cleared for assertive failures.
4. Given the party is erased, erasure is pending/in progress, GDPR capability is unavailable, the route resolved a partial/missing/gone party, the tenant changed, or another GDPR command is busy, when restriction/lift controls render or a stale result returns, then mutation is disabled or ignored with bounded no-PII state; no stale operation can mutate a different party or tenant.
5. Given consent inputs are provided, when I submit Add consent, then `AddConsentAsync` issues `RecordConsent` using the existing channel id, purpose, and lawful-basis fields, all visible labels/help/confirmation/outcome copy come from `AdminPortalLabels` or the label override seams, and empty channel/purpose still disables submission.
6. Given an active consent record is shown, when I choose Revoke consent, then the existing `ConsentManagementPanel` opens a single in-app confirmation step for the reversible action, does not use native `alert`/`confirm`, and issues only `RevokeConsentAsync(partyId, consentId, token)` after confirmation.
7. Given consent add/revoke is accepted or rejected, when the UI updates, then accepted-processing and outcome mapping follow the same `AdminPortalGdprOutcome`/politeness contract as restriction, the panel refreshes current party detail through `RefreshParty`, and rejection preserves operator input where safe.
8. Given consent records render, when a DPO reviews the ledger, then the display is bounded and localized: no raw problem details, contact values, identifiers, tenant-scoped ids, free-text restriction reasons, command payloads, or parser/destroyed-key text are echoed; enum display goes through `AdminPortalLabels` translation seams.
9. Given keyboard, screen reader, phone viewport, forced-colors, reduced-motion, or 200% zoom usage, when restriction and consent confirmation flows are opened, cancelled, confirmed, or rejected, then focus stays logical, controls remain reachable and visible at 320px width, confirmation transitions are announced, and state is never communicated by color alone.

## Tasks / Subtasks

- [x] Add single-confirm behavior for reversible restriction actions (AC: 1, 2, 3, 4, 9)
  - [x] Keep `PartyGdprOperationsPanel` as the owner for restriction command execution; do not create a parallel GDPR page, duplicate panel, direct `IAdminPortalGdprClient` injection, or lower-level gateway call from Razor.
  - [x] Extend `RestrictionActionsPanel` or add a tightly scoped child component under `src/Hexalith.Parties.AdminPortal/Components` for one confirmation layer with Cancel and Confirm; keep `ButtonAppearance.Outline` for both reversible actions.
  - [x] Confirm restrict and lift separately. The confirm copy must not echo the selected party display name, tenant id, contact values, identifiers, or reason text.
  - [x] Preserve `CanRestrict`, `CanLiftRestriction`, `_isBusy`, `IsErasurePending`, capability checks, `TenantContextSignature`, cancellation token, and `_capabilityVersion` stale-operation guards.
  - [x] On accepted restriction, optimistically show the restricted state/badge before or while `RefreshParty` reconciles. On accepted lift, optimistically remove restricted state/badge before or while refresh reconciles.

- [x] Harden consent add/revoke UX without changing the transport contract (AC: 5, 6, 7, 8, 9)
  - [x] Keep `ConsentManagementPanel` as the visual child and `PartyGdprOperationsPanel.AddConsentAsync` / `RevokeConsentAsync` as the only command path.
  - [x] Add a single in-app confirm for revoke consent. Do not require typed confirmation; revoke is reversible-action UX, not erasure UX.
  - [x] Route every new visible string through `AdminPortalLabels`; use existing virtual seams for enum/purpose/lawful-basis labels instead of inline string formatting in components.
  - [x] Preserve empty-input guards for `Channel id` and `Purpose`; keep `LawfulBasis.Consent` as the safe parse fallback only if the current value is not a valid enum.
  - [x] Do not add typed-name, display-name, reason, contact-value, identifier, or raw payload capture fields to `RecordingAdminPortalApiClient`, fixture snapshots, URLs, links, correlations, or command payloads.

- [x] Preserve operation outcome, privacy, and eventual-consistency behavior (AC: 2, 3, 4, 7, 8)
  - [x] Keep `RunCommandAsync` as the shared outcome path so accepted/completed stay polite and assertive outcomes clear stale correlations.
  - [x] Keep `RefreshParty.InvokeAsync()` as the authoritative reconciliation path after accepted restriction/consent commands.
  - [x] If adding local optimistic state, reset it on party switch, tenant switch, capability loss, erasure-pending refresh, disposal, rejected command, and authoritative detail refresh.
  - [x] Ensure erased/pending-erasure states disable lift/revoke/export as Story 3.2 fixed; do not regress those guards.
  - [x] Keep D7 certificate/retry-verification contract work out of scope. Story 3.5 remains approval-gated and Story 3.6 consumes it later.

- [x] Extend bUnit/component tests at the existing seams (AC: 1-9)
  - [x] Update `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs` so `RestrictThenLiftRouteThroughAcceptedContractWithReason` covers confirm-before-command, no native browser dialog dependency, accepted polite status, optimistic restricted badge behavior, refresh reconciliation, and lift confirmation.
  - [x] Update `GdprOperations_AddAndRevokeConsentRouteThroughAcceptedContractPerChannelPurpose` so add uses localized labels, revoke requires confirm-before-command, accepted outcomes are polite, and rejected revoke preserves bounded UI.
  - [x] Add regression tests for cancelled restrict/lift/revoke producing no API request and clearing any confirmation state.
  - [x] Add tests for erased/pending-erasure/partial/missing/capability-unavailable states with no mutation command and no stale confirm UI.
  - [x] Add tests that assertive failures clear previous success correlations and never echo raw details, reason text, contact values, identifiers, tenant ids, or rejected correlation ids.

- [x] Extend browser-level coverage where this repo already verifies Admin GDPR behavior (AC: 1, 2, 5, 6, 9)
  - [x] Extend `tests/e2e/specs/admin-parties-list.spec.ts` for direct GDPR route restriction and consent flows: single confirm, cancel no-op, confirm sends one fixture request, accepted `Saved - updating...`, no native dialogs, no browser-visible `/api/v1/commands` or `/api/v1/queries`, and no 320px/200% zoom overflow.
  - [x] Extend `src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs` only if fixture capture needs restriction/consent request evidence; keep captured fields bounded and avoid PII-like free text beyond the explicit command inputs already under test.

- [x] Validate with focused lanes and record limitations (AC: 1-9)
  - [x] Run `dotnet build tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj -c Release --no-restore -m:1 -v:minimal`.
  - [x] Run the compiled xUnit v3 AdminPortal test assembly directly if `dotnet test`/MTP is blocked by local IPC/socket limits.
  - [x] Run `npm run typecheck` and `npx playwright test --list` in `tests/e2e`; run the focused Playwright spec if local browser/server socket binding permits it.
  - [x] Run `bash scripts/check-no-warning-override.sh`.

## Dev Notes

### Current Implementation State

- `PartyGdprOperationsPanel.razor` already owns the GDPR operation surface, capability probing, command execution, outcome live region, correlation display, stale-operation guards, erasure status refresh, export, and processing-record loading. Restrict/lift and consent commands already route through this component; build on it instead of adding another operation surface. [Source: src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor]
- `RestrictionActionsPanel.razor` currently renders a labeled reason input plus two `ButtonAppearance.Outline` buttons that invoke `Restrict`/`Lift` immediately. Story 3.3 must add a single confirmation step while preserving outline styling and the existing callback boundary. [Source: src/Hexalith.Parties.AdminPortal/Components/RestrictionActionsPanel.razor]
- `ConsentManagementPanel.razor` currently renders channel id, purpose, lawful basis, Add consent, and active-record Revoke consent. It already accepts `AdminPortalLabels`, `CanAdd`, `CanRevoke`, and event callbacks; improve it rather than replacing it. [Source: src/Hexalith.Parties.AdminPortal/Components/ConsentManagementPanel.razor]
- `IPartiesAdminPortalApiClient` is the AdminPortal component boundary. Razor must not inject `IAdminPortalGdprClient`, `IPartiesCommandClient`, gateway clients, or EventStore query services directly for these operations. [Source: src/Hexalith.Parties.AdminPortal/Services/IPartiesAdminPortalApiClient.cs]
- `PartiesAdminPortalApiClient` delegates restriction and consent methods to `IAdminPortalGdprClient` and maps client exceptions to `AdminPortalGdprOutcome`; keep this adapter boundary intact. [Source: src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs]
- `HttpAdminPortalGdprClient` already posts `RestrictProcessing`, `LiftRestriction`, `RecordConsent`, and `RevokeConsent` through the EventStore command gateway (`POST api/v1/commands`, `domain="party"`). Do not add new endpoints or browser-visible gateway calls. [Source: src/Hexalith.Parties.Client/AdminPortal/HttpAdminPortalGdprClient.cs]

### Architecture and Scope Guardrails

- Epic 3 wraps existing AdminPortal GDPR panels and uses `IAdminPortalGdprClient`. Story 3.3 covers restriction and consent only; export/processing records are Story 3.4 and EventStore erasure verification remains Stories 3.5/3.6. [Source: _bmad-output/planning-artifacts/epics.md#Story-3.3-Restrict-lift-restriction-and-record-revoke-consent]
- AdminPortal is an adopter-facing RCL and must remain host-agnostic. Do not add a reference from `Hexalith.Parties.AdminPortal` to `Hexalith.Parties.UI`; keep any status behavior local and test-aligned. [Source: _bmad-output/implementation-artifacts/3-2-erase-a-party-with-typed-name-confirmation.md#Architecture-and-Scope-Guardrails]
- The browser talks only to the UI host/BFF. The UI host talks to the EventStore gateway through typed clients. Do not add public actor-host APIs or browser-visible EventStore gateway calls. [Source: _bmad-output/planning-artifacts/architecture.md#Architectural-Boundaries]
- Provisional GDPR bridge intentionally enables supported operations while certificate/retry-verification stay unavailable. Do not "fix" `GetErasureCertificateAsync` or `RetryErasureVerificationAsync` in this story. [Source: src/Hexalith.Parties.AdminPortal/Services/AdminPortalGdprCapability.cs]

### Domain and Contract Requirements

- The implemented GDPR commands are `RecordConsent`, `RevokeConsent`, `RestrictProcessing`, and `LiftRestriction`. `RecordConsent` derives deterministic `ConsentId = "{channelId}:{purpose}"` and no-ops on an already-active consent; `RestrictProcessing` no-ops when already restricted; `LiftRestriction` rejects/not-restricted when appropriate. [Source: docs/data-models.md#GDPR-commands-consent-restriction-erasure]
- `PartyDetail` carries `ConsentRecords`, `IsRestricted`, `RestrictedAt`, and `RestrictionReason`. Treat `RestrictionReason` as sensitive operational text for this UI story; do not surface it in confirmation copy, operation announcements, URLs, links, telemetry, or test fixture snapshots unless an existing detail field already renders it in a bounded admin context. [Source: docs/data-models.md#PartyState-fields]
- `ConsentRecord` includes channel id, purpose, lawful basis, grant/revoke metadata, and `IsActive`. Consent display must remain bounded and localizable; enum/purpose display should go through `AdminPortalLabels` seams. [Source: docs/data-models.md#Value-objects]
- Validation failures surface as `PartyCommandValidationRejected` without raw values/messages. UI rejection copy must be bounded and assertive; do not show raw problem details or command payloads. [Source: docs/data-models.md#Rejection-events-IRejectionEvent]

### UX and Accessibility Requirements

- Reversible GDPR actions, including restrict and withdraw/revoke, use outline buttons and a single confirm. Irreversible typed confirmation is only for erasure. Native `alert()`/`confirm()` are not allowed. [Source: _bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Component-Patterns]
- Routine accepted-processing updates use `role="status" aria-live="polite"`; validation, transient, load, and hard failures use `role="alert"`/assertive. Do not move focus for routine optimistic saves. [Source: docs/accessibility.md#Component-Semantics]
- `PartyStateBadge` must communicate `active/inactive/restricted/erased` with color plus text, never color alone. On accepted restriction/lift, the detail surface must make the expected restricted state legible while projection refresh catches up. [Source: _bmad-output/planning-artifacts/epics.md#Story-1.8-Shared-domain-components]
- Confirmation flows must work by keyboard and screen reader, preserve logical focus, remain usable at 320px and 200% zoom, and support forced-colors/reduced-motion behavior. [Source: _bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Accessibility-Floor]

### Privacy and Security Requirements

- Never log or display event payloads, raw problem details, parser exceptions, destroyed-key text, contact values, identifiers, tenant-scoped ids, free-text restriction reasons, consent revocation reasons, or command payloads. Projection and operation logs must stay bounded. [Source: _bmad-output/project-context.md#Critical-Dont-Miss-Rules-anti-patterns--gotchas]
- Tenant access is fail-closed and eventually consistent. Missing tenant context, forbidden, unauthenticated, erased, gone, and unavailable states must not expose party data or enable mutation controls. [Source: _bmad-output/project-context.md#Critical-Dont-Miss-Rules-anti-patterns--gotchas]
- The EventStore command gateway is the public boundary. The `parties` actor host has no public command/query API; do not add controllers or minimal endpoints for these operations. [Source: docs/api-contracts.md#The-boundary-in-one-picture]

### Previous Story Intelligence

- Story 3.2 added the typed-name erasure dialog, kept erasure on `QueryService.ApiClient.RequestErasureAsync(partyId, token)`, and fixed stale erasure-dialog closure plus pending-erasure guards for lift/revoke. Preserve those exact guards while adding Story 3.3 confirms. [Source: _bmad-output/implementation-artifacts/3-2-erase-a-party-with-typed-name-confirmation.md#Senior-Developer-Review-AI]
- Story 3.2 validation passed focused AdminPortal build/tests, full AdminPortal test executable, E2E typecheck, Playwright spec discovery, and `scripts/check-no-warning-override.sh`; local `dotnet test`/Playwright runtime may be blocked by sandbox IPC/socket permissions. Use the same evidence split. [Source: _bmad-output/implementation-artifacts/3-2-erase-a-party-with-typed-name-confirmation.md#Review-Validation]
- Story 3.1 established that direct GDPR routes must reuse `PartyGdprOperationsPanel`, keep operation live regions initially empty, clear stale correlations on assertive failures, and render partial/missing states with bounded no-PII content. Do not regress these behaviors. [Source: _bmad-output/implementation-artifacts/3-1-gdpr-operations-page.md#Senior-Developer-Review-AI]
- Recent commits for Stories 3.1 and 3.2 touched `PartyGdprOperationsPanel.razor`, `PartiesAdminPortal.razor`, `AdminPortalLabels.cs`, `RecordingAdminPortalApiClient.cs`, `PartiesAdminPortalComponentTests.cs`, `PartiesAdminPortalE2eFixture.cs`, and `admin-parties-list.spec.ts`. Continue this focused seam rather than broad AdminPortal restructuring. [Source: git log -5]

### Files Likely to Change

- `src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor` - shared command path, optimistic restriction state, confirmation state cleanup, stale-operation/capability guards, outcome handling, and refresh reconciliation.
- `src/Hexalith.Parties.AdminPortal/Components/RestrictionActionsPanel.razor` - restrict/lift single-confirm UI while preserving outline buttons and callback boundary.
- `src/Hexalith.Parties.AdminPortal/Components/ConsentManagementPanel.razor` - revoke single-confirm UI, localized/bounded consent display, and no inline raw strings.
- `src/Hexalith.Parties.AdminPortal/Services/AdminPortalLabels.cs` - localizable confirmation/copy additions and enum/purpose label seams.
- `src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor.css` or `PartiesAdminPortal.razor.css` - only if confirmation layout/focus/forced-colors/zoom styling is needed; keep styles component-scoped to the component that renders the markup.
- `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs` - primary bUnit coverage for confirm-before-command, optimistic/reconcile behavior, outcome politeness, privacy, and stale-state guards.
- `tests/Hexalith.Parties.AdminPortal.Tests/Services/RecordingAdminPortalApiClient.cs` - only if tests need queued results or bounded request assertions; do not capture new PII.
- `src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs` and `tests/e2e/specs/admin-parties-list.spec.ts` - browser-level fixture/spec coverage for restriction/consent confirmation flows.

### Testing Standards

- Use xUnit v3, Shouldly, NSubstitute, and bUnit. Do not introduce Moq, FluentAssertions, or raw `Assert.*`. [Source: _bmad-output/project-context.md#Testing-Rules]
- Use Central Package Management; do not add `Version=` attributes to project files or bump packages for this story. [Source: _bmad-output/project-context.md#Technology-Stack--Versions]
- Preferred focused verification is the AdminPortal test project/build, direct xUnit v3 executable when MTP is blocked, E2E typecheck/spec discovery, focused Playwright runtime where available, and `scripts/check-no-warning-override.sh`. [Source: _bmad-output/implementation-artifacts/3-2-erase-a-party-with-typed-name-confirmation.md#Testing-Standards]

### Project Structure Notes

- Keep work inside the existing AdminPortal RCL and its test/e2e seams.
- Keep generated output under `obj/**/generated` untouched and uncommitted.
- No new NuGet package is expected.
- Preserve CRLF/final-newline/editorconfig conventions when editing C#, Razor, CSS, and TypeScript.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-3.3-Restrict-lift-restriction-and-record-revoke-consent]
- [Source: _bmad-output/planning-artifacts/architecture.md#API--Communication-Patterns]
- [Source: _bmad-output/planning-artifacts/architecture.md#Project-Structure--Boundaries]
- [Source: _bmad-output/project-context.md#Technology-Stack--Versions]
- [Source: docs/data-models.md#GDPR-commands-consent-restriction-erasure]
- [Source: docs/api-contracts.md#IAdminPortalGdprClient]
- [Source: docs/accessibility.md#Component-Semantics]
- [Source: _bmad-output/implementation-artifacts/3-2-erase-a-party-with-typed-name-confirmation.md#Previous-Story-Intelligence]

## Validation Summary

- Source discovery loaded project context, sprint status, `epics.md`, `architecture.md`, nested UX artifacts, prior Stories 3.1 and 3.2, GDPR/data/API/accessibility docs, existing AdminPortal components/services/tests, recent git history, and current implementation files likely to be updated.
- Checklist fixes applied before finalizing: clarified no duplicate GDPR operation surface, no EventStore D7 work, no AdminPortal-to-UI host dependency, single-confirm reversible action behavior, optimistic restricted-state/reconcile requirements, localized/bounded consent copy, pending-erasure mutation guard preservation, stale-correlation cleanup, and concrete bUnit/Playwright seams.
- Latest-technology review did not identify a dependency change needed for this story. The relevant stack is pinned locally (.NET 10, FluentUI Blazor `5.0.0-rc.3`, xUnit v3/bUnit), and no new packages or external APIs are required.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet build tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj -c Release --no-restore -m:1 -v:minimal` - passed, 0 warnings, 0 errors.
- `dotnet test tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj -c Release --no-build -m:1 -v:minimal` - blocked by local IPC/socket permission (`System.Net.Sockets.SocketException (13): Permission denied`).
- `./tests/Hexalith.Parties.AdminPortal.Tests/bin/Release/net10.0/Hexalith.Parties.AdminPortal.Tests -class Hexalith.Parties.AdminPortal.Tests.Components.PartiesAdminPortalComponentTests -parallel none -noLogo` - passed 99/99 after review fixes.
- `./tests/Hexalith.Parties.AdminPortal.Tests/bin/Release/net10.0/Hexalith.Parties.AdminPortal.Tests -parallel none -noLogo` - passed 156/156 after review fixes.
- `npm run typecheck` in `tests/e2e` - passed.
- `npx playwright test --list` in `tests/e2e` - passed, discovered 31 tests.
- `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release --no-restore -m:1 -v:minimal` - passed, 0 warnings, 0 errors.
- `npx playwright test specs/admin-parties-list.spec.ts -g "direct GDPR route opens operations destination" --project chromium` - blocked by local Kestrel socket binding permission (`System.Net.Sockets.SocketException (13): Permission denied`).
- `bash scripts/check-no-warning-override.sh` - passed.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Added single in-app confirmation flows for restrict, lift, and revoke consent without changing the existing AdminPortal command boundary.
- Kept restriction/consent commands on `PartyGdprOperationsPanel` and the shared `RunCommandAsync` outcome mapping; accepted states remain polite and assertive failures clear stale correlations.
- Added optimistic restricted-state badge updates through a parent callback, followed by the existing `RefreshParty` reconciliation path.
- Routed new confirmation copy through `AdminPortalLabels` and kept confirmation text bounded: no party display name, tenant id, contact values, identifiers, reason text, raw payloads, or problem details.
- Extended bUnit and E2E seams for confirmation-before-command, cancel no-op, accepted status, stale confirmation cleanup, bounded request capture, and direct GDPR route coverage.
- Senior review auto-fixed stale child confirmation state so pending restriction/revoke confirmations reset across party, tenant, capability, and erasure-context changes before any mutation can execute.
- Senior review auto-fixed consent ledger rendering so raw consent ids, channel ids, and operator ids are not echoed while preserving bounded purpose/lawful-basis/status/date display.
- Definition of Done: PASS. Completion score: 27/27 checklist items passed. Quality gates passed except focused Playwright runtime, which is environment-blocked by socket permissions after static discovery/typecheck passed.

### Senior Developer Review (AI)

- Review outcome: Approve after auto-fixes.
- Issues found and fixed: HIGH stale confirmation state could survive a context switch and execute against the current party; HIGH consent ledger echoed raw channel/operator identifiers despite the bounded-display AC.
- Validation: focused AdminPortal build passed with 0 warnings; component class passed 99/99; full AdminPortal executable passed 156/156; E2E typecheck passed; Playwright discovery found 31 tests; UI project build passed with 0 warnings; warning-override gate passed.
- Residual limitation: focused Playwright runtime remains blocked by local Kestrel socket binding permission (`System.Net.Sockets.SocketException (13): Permission denied`), matching prior story evidence.

### File List

- _bmad-output/implementation-artifacts/3-3-restrict-lift-restriction-and-record-revoke-consent.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- src/Hexalith.Parties.AdminPortal/Components/ConsentManagementPanel.razor
- src/Hexalith.Parties.AdminPortal/Components/ConsentManagementPanel.razor.css
- src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor
- src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor
- src/Hexalith.Parties.AdminPortal/Components/RestrictionActionsPanel.razor
- src/Hexalith.Parties.AdminPortal/Components/RestrictionActionsPanel.razor.css
- src/Hexalith.Parties.AdminPortal/Services/AdminPortalLabels.cs
- src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs
- tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs
- tests/Hexalith.Parties.AdminPortal.Tests/Services/RecordingAdminPortalApiClient.cs
- tests/e2e/specs/admin-parties-list.spec.ts

### Change Log

- 2026-06-10 - Implemented Story 3.3 restriction and consent reversible confirmation flows, optimistic restricted-state reconciliation, bounded labels/fixtures, bUnit coverage, E2E coverage, and validation evidence.
- 2026-06-10 - Senior review auto-fixed stale confirmation reset and consent-ledger identifier leakage, added regression coverage, and marked story done.
