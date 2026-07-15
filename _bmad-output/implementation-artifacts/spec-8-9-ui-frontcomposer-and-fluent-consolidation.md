---
title: '8.9 UI FrontComposer and Fluent consolidation'
type: 'refactor'
created: '2026-07-07T00:00:00+02:00'
status: 'draft'
review_loop_iteration: 0
followup_review_recommended: false
context:
  - '{project-root}/_bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-8-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md'
warnings:
  - oversized
  - blocked-prerequisite
---

<intent-contract>

## Intent

**Problem:** Parties UI carries local parallel implementations of FrontComposer/Fluent primitives — projection stream/freshness fallback, optimistic reconcile, status, result envelope, grid/list navigation, identity binding, picker internals, and E2E fixture/specimen primitives — plus lingering FAST/v4 tokens and the picker re-skin / Admin phone-reflow debt carried from Story 2.5.

**Approach:** Replace, move, or defer-with-proof onto FrontComposer + Fluent 2 primitives; purge legacy FAST/v4 tokens from production UI styles; keep every accessibility, forced-colors, reduced-motion, focus, state, and GDPR-copy contract intact; record bUnit + Playwright/SSR a11y evidence with sandbox limitations separated from release blockers.

## Boundaries & Constraints

**Always:** Preserve spine invariant I13 — Fluent 2 inheritance, teal accent non-text only, filled actions bind the AA-safe brand background, WCAG 2.2 AA, keyboard/pointer parity, functional skip links, visible focus rings, forced-colors + reduced-motion support, semantic controls, typed-name destructive confirmation, polite/assertive live-region split, and no focus-stealing on optimistic updates — plus I14 (GDPR copy honesty: no dark patterns, no over-promised export latency, cancellation-vs-permanence, stale reads render last-known) and I10 (freshness fallback in the UI). Keep consumer self-scoping (`/me`, `party_id` resolved, never client-supplied ids). Keep the two RCL package ids (`AdminPortal`, `ConsumerPortal`, `Picker`) stable (I5).

**Block If:** The 8.3 row **"FrontComposer UI primitives" is `needs-additive-api`**. G4 requires owner-accepted work packages for: (A) additive `FcEntityPicker<T>`; (B) UI-normalized per-record freshness without redefining EventStore/G6 wire semantics; (C) a public routine `status`/`polite` versus validation/failure `alert`/`assertive` live-region contract; (D) injectable file/JSON browser download with cleanup and no payload persistence/logging; (E) source-compatible typed-name mode on `FcDestructiveConfirmationDialog`; and (F) parity certification for the skip links already present in `FrontComposerShell`, with a narrow additive customization seam only if Parties evidence proves it necessary. All six require named owner approval, exact `Hexalith.FrontComposer.Contracts.UI`/`Hexalith.FrontComposer.Shell` release versions or root-declared submodule pin, public API/package validation, producer bUnit evidence, Parties bUnit/Playwright parity, and rollback before UI migration. Also HALT if a replacement primitive regresses any WCAG 2.2 AA contract or the polite/assertive split, or if `blazor.web.js` sandbox limits (interactive gate) are conflated with real release blockers instead of deferred to the `ui-a11y` CI lane.

**Never:** Do not migrate projection/query (8.6), crypto/DataProtection (8.7), or client/MCP/AppHost/deploy (8.8). Do not regress accessibility or GDPR copy. Do not change consumer self-scoping or admin policy gating. Do not hard-code raw accent colors for text-bearing controls or redeclare Fluent tokens in product CSS.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|----------------------------|----------------|
| Freshness render | Stale/degraded/rebuilding read in a portal | FrontComposer freshness indicator shows last-known + quiet cue; never blanks/throws | Polite live region; no focus steal |
| Optimistic reconcile | Accepted command echoed then projection-confirmed | Optimistic echo → reconcile via shared effect; assertive only on validation/failure | Rollback of echo on rejection |
| Party picker | Admin create/edit uses `FcEntityPicker<T>` | WAI-ARIA combobox parity with the current re-skinned picker (2.5); `party-selected` event preserved | Keyboard + forced-colors intact |
| Destructive erase | Typed-name confirmation dialog | FrontComposer typed-name destructive mode; permanence copy honest | Confirmation required; no accidental submit |
| Routine versus failure announcement | Loading/count/freshness/ready update versus validation/load failure | Routine changes use `status`/`polite`; failures use `alert`/`assertive`; atomic bounded localized copy | No duplicate source, payload, tenant id, token, or backend detail |
| Browser export download | Authorized export supplies a caller-approved safe filename, content type, and bounded stream/bytes | Shared download service creates the file without domain-specific JS globals and cleans temporary browser resources | Cancellation/disconnect/JS failure maps to bounded UI state; no payload log, storage, or echo |
| Skip navigation | Keyboard user enters the FrontComposer shell with or without navigation | Existing localized skip links are first stops and target unique focusable content/navigation landmarks | Navigation-absent/responsive behavior remains valid; forced-colors focus visible |
| Forced-colors / reduced-motion | User OS settings | Contracts honored on migrated components | No color-only cues |

</intent-contract>

## Code Map

Target: FrontComposer/Fluent primitives (owners: `Hexalith.FrontComposer.Contracts.UI` and `Hexalith.FrontComposer.Shell` — 8.3 row `needs-additive-api`, prove G4 first). Existing foundations include `FcAggregateListPage`, `FcAggregateDetailPage`, `FcDestructiveConfirmationDialog`, `FcProjectionConnectionStatus`, `FcLifecycleWrapper`, `FrontComposerShell` skip links, `ProjectionSubscriptionService`, and `CommandResult`/`QueryResult`/`ProblemDetailsPayload`. Existing foundations are not full G4 delivery.

REPLACE/MOVE/DEFER (keep local until parity — I3):
- `src/Hexalith.Parties.ConsumerPortal/Components/FreshnessStatus.razor`, `ConsumerRouteShell.razor`, `MyProfilePage.razor`, `EditMyProfilePage.razor`, `MyConsentPage.razor`, `MyPrivacyPage.razor`, `ProfileField.razor` -- freshness/status/optimistic + consumer surfaces.
- `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor`, `CreateEditPartyPage.razor`, `PartyGdprOperationsPanel.razor`, `ErasureVerificationReportPanel.razor`, `ConsentManagementPanel.razor`, `ProcessingRecordsPanel.razor`, `RestrictionActionsPanel.razor`, `PortabilityExportPanel.razor`, `ErasureStatusPanel.razor` -- grid/list nav, destructive dialog, GDPR panels, download.
- `src/Hexalith.Parties.Picker/Components/PartyPicker.razor` + `Extensions/PartyPickerCustomElementExtensions.cs` + `Services/PartyPicker*` -- picker internals (the 2.5 re-skin/ARIA debt) → `FcEntityPicker<T>` parity.
- `src/Hexalith.Parties.UI/IdentityBinding/*` -- identity binding primitives to move/keep per FrontComposer parity.
- `tests/e2e/specs/*`, `tests/e2e/helpers/*`, `tests/e2e/baselines/*` -- fixture/specimen primitives + a11y specs.

Evidence: `story-8-3-platform-api-prerequisite-matrix.md` (G4-A through G4-F + exact FrontComposer identities), `sprint-change-proposal-2026-07-16-g4-g11-frontcomposer-shared-primitives-routing.md`, `tests/.../test-summary.md`, `sprint-status.yaml`.

## Tasks & Acceptance

**Execution:** (gated by the G4 Block-If; each slice retains its local rollback until accepted)
- [ ] `story-8-3-platform-api-prerequisite-matrix.md` -- record named owner approval, public API/package validation, exact Contracts.UI/Shell release versions or root gitlink, producer tests, Parties consumer parity, and rollback for G4-A through G4-F.
- [ ] Migrate freshness/status/optimistic-reconcile onto FrontComposer per-record freshness and live-region primitives; bUnit-cover routine polite versus validation/failure assertive behavior without focus steal or unsafe detail.
- [ ] Migrate picker to `FcEntityPicker<T>` parity, preserving durable party-id selection, stale-response rejection, the adopter `party-selected` custom event, and current WAI-ARIA combobox behavior.
- [ ] Migrate Admin/Consumer JSON export to the shared download service; retain safe domain filename derivation; prove stream/object-URL/anchor cleanup, cancellation/error mapping, and no payload echo/log/storage.
- [ ] Migrate irreversible erasure to the additive typed-name dialog mode; prove ordinal exact matching, disabled confirmation, safe initial cancel focus, Escape cancellation, and no Enter bypass. Preserve the existing simple-confirm mode for other callers.
- [ ] Certify the existing FrontComposer shell skip links against Parties content/navigation landmarks; add only an owner-approved narrow customization seam if certification cannot satisfy the UX.
- [ ] Purge FAST/v4 tokens from production UI styles; keep Fluent 2 inheritance discipline.
- [ ] Record Playwright/SSR a11y evidence via `ui-a11y`; run SSR specs with `PLAYWRIGHT_SKIP_WEBSERVER=1` locally and defer the interactive gate to CI.

**Acceptance Criteria:**
- Given the G4 row is `needs-additive-api`, when 8.9 is attempted, then it HALTs `blocked` with that prerequisite.
- Given proven parity, when portals render on FrontComposer/Fluent primitives, then all WCAG 2.2 AA, forced-colors, reduced-motion, focus, live-region, and GDPR-copy contracts hold (bUnit + `ui-a11y` green).
- Given the picker migration, when used in Admin create/edit, then durable party-id binding, stale-response rejection, combobox ARIA, keyboard/focus behavior, and the adopter `party-selected` event match the current re-skinned picker.
- Given routine status/freshness/processing changes and validation/load failures, when announced, then there is one bounded source using polite status for routine updates and assertive alert for failures, with no focus steal or sensitive detail.
- Given an authorized portability export, when downloaded, then the shared service uses the caller-approved safe filename/content type, releases browser resources in failure and success paths, and does not log, store, or echo the payload.
- Given irreversible erasure, when the typed name differs by any ordinal character, then the destructive action remains disabled; Cancel is initially safe, Escape cancels, and Enter cannot bypass the gate.
- Given the FrontComposer shell with or without navigation, when traversed by keyboard, then existing localized skip links are the first stops, target unique programmatically focusable landmarks, and retain visible forced-colors focus.
- Given production CSS after migration, when inspected, then no FAST/v4 tokens remain and no raw accent is used for text-bearing controls.

## Design Notes

- **§4 gate mapping:** (1) Prereq: G4-A through G4-F + exact FrontComposer Contracts.UI/Shell identities (predecessors 8.3 done; independent of 8.6/8.7/8.8). (2) Repos: `Parties` + `Hexalith.FrontComposer`; producer work remains owner-repository work and no submodule edit is authorized by this spec. (3) Rollback: local picker, freshness/status, downloads, typed confirmation, and skip-link paths stay until each slice proves parity; revert per component. (4) Lanes: producer bUnit/public-API/package validation, Parties bUnit (`projections-ui` shard), `ui-a11y` (WCAG gate), SSR Playwright with sandbox note. (5) Non-goals: 8.6/8.7/8.8, domain filename policy, EventStore/G6 wire freshness semantics. (6) Parity checklist: I13/I14/I10/I5.
- **Local a11y-gate limitation:** the interactive Blazor gate cannot fully pass in the WSL sandbox (`blazor.web.js` 0 bytes; webServer probe 500s without OIDC) — run SSR specs locally, defer the interactive gate to `ui-a11y` CI; a sandbox skip is not a release blocker.

## Verification

**Commands:**
- `pwsh scripts/test.ps1 -Lane unit` then the UI/bUnit test EXEs directly -- expected: migrated-component bUnit tests pass.
- `PLAYWRIGHT_SKIP_WEBSERVER=1 npm run test:a11y` (in `tests/e2e`) -- expected: SSR a11y specs pass; interactive gate deferred to CI `ui-a11y`.

**Manual checks:**
- Confirm no FAST/v4 tokens in production UI CSS; the picker's durable-id/WAI-ARIA/`party-selected` behavior; routine-polite versus failure-assertive announcements; typed-name exact matching; file-download cleanup/no-payload handling; and skip-link focus targets are intact.
