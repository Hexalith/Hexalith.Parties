# Story 7.7: Implement GDPR Operation Panels

Status: done

<!-- 2026-05-23: Both scheduling gates resolved (primary RISK-ACCEPTED scoped to 7.7; secondary 9.8 DONE). create-story authored the Tasks/Subtasks against the accepted provisional contract; story moved blocked → ready-for-dev. See Gate Resolution + Tasks / Subtasks. -->

## Story

As a Parties administrator or DPO,
I want compact GDPR operation panels for a selected party,
so that I can trigger and monitor erasure, restriction, consent, portability, and processing-record workflows.

## Acceptance Criteria

1. Given an administrator opens the GDPR panel for a selected party, when the panel renders, then it shows operation sections for erasure, erasure status/certificate, verification retry, restrict/lift restriction, consent add/revoke/history, portability export, and processing records where supported, and each section uses the accepted Parties client/query/command contract.
2. Given an erasure request is submitted, when confirmation is required, then the confirmation displays party id only, and it returns command accepted outcome before refreshing authoritative erasure status.
3. Given erasure certificate or verification status is requested, when the operation completes, then the panel shows safe verification results, and generated filenames use party id plus timestamp only.
4. Given restriction is applied or lifted, when the administrator submits a bounded reason where required, then the operation returns command accepted outcome, and the panel refreshes current status before enabling follow-on actions.
5. Given consent is added, revoked, or viewed, when the administrator uses consent controls, then consent is scoped per channel and per purpose, and no party-wide or tenant-wide consent shortcut is offered.
6. Given portability export or processing records are requested, when the operation completes, then outputs use safe filenames, content types, bounded summaries, and safe correlation links, and exported payloads or raw processing details are not logged.
7. Given GDPR panel tests run, when they cover all supported flows, disabled unsupported flows, safe filenames, bounded reasons, stale state, redaction, and tenant switch, then GDPR operations remain usable and privacy-safe.

## Tasks / Subtasks

> **Binding constraints (from the 2026-05-23 Risk Acceptance — non-negotiable).** Build ONLY on provisional `IAdminPortalGdprClient` methods that genuinely exist; NEVER fake or stub the two unavailable operations (erasure certificate, retry verification). Preserve the Story 7.6 fail-closed capability gate (UX-DR11). Keep tenant-safety, privacy, accessibility (7.9), and privacy-encoding (7.10) guardrails in force. The actor host `src/Hexalith.Parties` must gain no REST/Swagger/MCP/DAPR-actor/projection-actor calls.

- [x] **Task 1 — Make the production GDPR capability honest about the provisional bridge (keystone).** (AC: 1, 3)
  - [x] 1.1 In `PartiesAdminPortalApiClient.GetGdprCapabilityAsync` (`src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs:178`), replace the `_gdprClient is not null → AdminPortalGdprCapability.Available()` branch. `Available()` claims all eight operations work (incl. `CanRetryVerification`), but the registered `HttpAdminPortalGdprClient` reports certificate and retry-verification as contract-unavailable (`GetErasureCertificateAsync` throws `ContractUnavailable`; `RetryErasureVerificationAsync` returns `AdminPortalGdprOutcome.ContractUnavailable` — `src/Hexalith.Parties.Client/AdminPortal/HttpAdminPortalGdprClient.cs:48-55`). Return instead a capability that enables only the genuinely-working operations: request erasure, read erasure status, restrict, lift restriction, manage consent, export data, read processing records.
  - [x] 1.2 Keep the `_gdprClient is null → AdminPortalGdprCapability.Unavailable()` branch unchanged — this is the Story 7.6 fail-closed gate and must not weaken.
  - [x] 1.3 The disabled operations (certificate + retry) MUST carry the exact bounded blocker. The provisional-bridge capability's `Reason` MUST equal `AdminPortalGdprCapability.ContractUnavailableReason` so the panel's `CapabilityReason` (`PartyGdprOperationsPanel.razor:234`) resolves to `Labels.GdprOperationContractBlocked`, which is literally that blocker string (`AdminPortalLabels.cs:174`). Do NOT reuse `Available()` (empty reason) or `Partial(...)` (reason = "temporarily unavailable"). Add a dedicated factory on `AdminPortalGdprCapability` (`src/Hexalith.Parties.AdminPortal/Services/AdminPortalGdprCapability.cs`), e.g. `ProvisionalBridge(...)`, that sets the enabled flags above with `Reason = ContractUnavailableReason`. Preserve the granular per-operation flag composition and `HasAnySupport`; do NOT collapse to a single Boolean.
  - [x] 1.4 Gate the erasure-certificate fetch so it is never attempted while unavailable. Today `RefreshErasureStatusAsync` (`PartyGdprOperationsPanel.razor:439-450`) fetches the certificate whenever status is `Verified`/`Complete`/`Erased`; against the provisional client that fetch throws `ContractUnavailable` and surfaces a misleading failure outcome when an operator refreshes an erased party. Add an additive `CanReadErasureCertificate` flag to `AdminPortalGdprCapability` (false for `Unavailable`/`Degraded`/`ProvisionalBridge`, true for `Available`, optional param defaulting false on `Partial`) and gate the certificate fetch on it. `ErasureStatusPanel.razor:3-6` already renders `Labels.GdprCertificateUnavailable` ("Certificate unavailable") when the certificate is null — rely on that fail-safe instead of attempting the unavailable query.
  - [x] 1.5 Verify the erasure-section blocker (`ErasureDisabledReason`, `PartyGdprOperationsPanel.razor:217`) shows `GdprOperationContractBlocked` while keeping Request erasure + Refresh erasure status enabled, and that the Retry verification button stays hidden (`@if (CanRetryVerification)`, `:50`). Adjust only if the certificate gate requires it; do not regress the enabled operations.

- [x] **Task 2 — Confirm every supported GDPR flow routes through the accepted provisional contract and stays privacy/tenant-safe (no fakes).** (AC: 1, 2, 4, 5, 6)
  - [x] 2.1 Erasure (AC2): `BeginErasureConfirmation` shows party id only (no name/PII) before confirming; `ConfirmErasureAsync` posts via `RequestErasureAsync` (→ `EraseParty`) and returns the Accepted outcome before `RefreshErasureStatusAsync` refreshes authoritative status. Correlation id is shown and an EventStore correlation link is offered only when available.
  - [x] 2.2 Restriction (AC4): Restrict/Lift route via `RestrictProcessingAsync`/`LiftRestrictionAsync` (→ `RestrictProcessing`/`LiftRestriction`), accept a bounded reason where required, return Accepted, then `RefreshParty` refreshes current status before re-enabling follow-on actions.
  - [x] 2.3 Consent (AC5): Add/Revoke route via `AddConsentAsync`/`RevokeConsentAsync` (→ `RecordConsent`/`RevokeConsent`) scoped per channel AND per purpose; history is sourced from `PartyDetail.ConsentRecords`. Confirm `ConsentManagementPanel` offers NO party-wide or tenant-wide consent shortcut.
  - [x] 2.4 Portability + processing (AC6): Export re-derives the filename via `GdprExportFileNameBuilder.Build` (party id + UTC timestamp, sanitized, 64-char bounded — `GdprExportFileNameBuilder.cs`), ignoring the transport-supplied `FileName`; content type `application/json`; processing summaries render bounded + HTML-encoded. Confirm no exported payload or raw processing detail is logged (the AdminPortal source scan test already forbids `ILogger`/`TelemetryClient`/`ActivitySource`).
  - [x] 2.5 Confirm certificate + retry stay capability-disabled with the exact blocker and are never faked or stubbed as working, and that no actor-host/REST/MCP/projection-actor calls were introduced in `src/Hexalith.Parties`.

- [x] **Task 3 — Extend automated tests for the honest capability and every accepted operation path.** (AC: 7)
  - [x] 3.1 `tests/Hexalith.Parties.AdminPortal.Tests/Services/PartiesAdminPortalApiClientTests.cs` (no `GetGdprCapabilityAsync` coverage exists today): add (a) no `IAdminPortalGdprClient` registered → `Unavailable()` (all flags false, `Reason == ContractUnavailableReason`); (b) provisional client registered → the 7 supported flags true, `CanRetryVerification` + `CanReadErasureCertificate` false, `Reason == ContractUnavailableReason`.
  - [x] 3.2 `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs`: add a component test using a capability mirroring the provisional bridge (supported ops enabled; retry + certificate disabled). Assert Request erasure / Refresh erasure status / Restrict / Add consent (with inputs) / Export party data / Processing records enabled; Retry verification button absent; erasure section shows `AdminPortalGdprCapability.ContractUnavailableReason`; refreshing an erased party shows "Certificate unavailable" and NOT a contract-unavailable failure message.
  - [x] 3.3 Cover remaining accepted-flow gaps if not already exercised: consent add + revoke happy paths and history render; restriction lift happy path; bounded restriction reason; stale-response / tenant-switch invalidation mid-operation; redaction (no PII in safe filename, correlation link, DOM ids); failure mapping (sign-out → SignInRequired, missing tenant → TenantUnavailable, forbidden → AccessDenied, gone/erased → terminal, timeout/transient → bounded) to localized labels.
  - [x] 3.4 Do NOT regress existing GDPR tests — they exercise the panel against a fully-capable double (a future "contract Satisfied" state) and must stay green. Adding an optional `canReadErasureCertificate` param to `Partial(...)` keeps `PartiesAdminPortal_GdprOperations_EnableOnlySupportedPartialContractActions` compiling; `Available()` sets it true so the recording double still fetches certificates where tests expect.
  - [x] 3.5 Run the focused suite: `dotnet test tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release`.

- [x] **Task 4 — Verify guardrails, build, and finalize.** (AC: 1-7)
  - [x] 4.1 Confirm no new user-facing string bypasses `AdminPortalLabels` (localization — Story 7.8); accessibility guardrails hold (polite `role="status"` regions, keyboard-reachable controls, non-color-only state — Story 7.9); privacy-encoding scans still pass (no `MarkupString`/raw HTML, no browser storage, no PII in DOM ids/filenames/URLs — Story 7.10).
  - [x] 4.2 Run the Release solution build (Story 9.8 clean-clone gate is `done`); record any known-unrelated failure explicitly rather than silencing it.
  - [x] 4.3 Update the File List in the Dev Agent Record with every touched file and add a Change Log entry.

### Review Findings

- [x] [Review][Patch] Certificate capability is missing from support/state-clearing composition [src/Hexalith.Parties.AdminPortal/Services/AdminPortalGdprCapability.cs:54]
- [x] [Review][Patch] Erasure disabled reason ignores certificate capability [src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor:217]
- [x] [Review][Patch] Erasure confirmation does not display the party id [src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor:21]
- [x] [Review][Patch] Export download content type still trusts the transport [src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor:627]
- [x] [Review][Patch] Processing record summaries are rendered unbounded [src/Hexalith.Parties.AdminPortal/Components/ProcessingRecordsPanel.razor:20]

## Gate Resolution (2026-05-23)

Both scheduling gates for Story 7.7 are now resolved.

Primary gate — RESOLVED (Risk Accepted, scoped):

- `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md` is now `status: Risk Accepted`, scoped to Story 7.7 only (Epic 8 remains `Required`), per `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-23.md`, decided by Jérôme (project lead).
- The existing `IAdminPortalGdprClient` / `HttpAdminPortalGdprClient` / `AdminPortalGdprRoutes` are accepted as a **temporary bridge** (provisional, may be reshaped when the formal contract lands). Story 7.7 builds only on provisional methods that genuinely exist and MUST NOT fake unavailable methods.

Secondary gate — RESOLVED (done):

- `9-8-solution-build-green-on-clean-clone` is now `done` in `sprint-status.yaml`, so the clean-clone / CI build is a reliable signal.

Binding conditions carried into implementation (from the Risk Acceptance):

- Preserve the Story 7.6 fail-closed capability gate (UX-DR11). Operations whose provisional methods are unavailable (currently erasure certificate and retry verification) MUST stay disabled with the exact bounded blocker `Blocked on accepted EventStore-fronted Parties client/gateway contract`.
- Keep tenant-safety, privacy, accessibility (Story 7.9), and privacy-encoding (Story 7.10) guardrails in force.
- Honor the AdminPortal/GDPR implementation snapshot below; do not recreate it from scratch.

Remaining step before `dev-story`: run `create-story 7.7` to author the Tasks/Subtasks breakdown against the accepted (provisional) contract and move the story `blocked → ready-for-dev`. The story file currently has no task breakdown, so it is not yet dev-ready.

## Required To Unblock

> 2026-05-23: RESOLVED via Risk Acceptance (see Gate Resolution). The decision items below are satisfied or carried as binding conditions; retained for history.

- Update `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md` to `Satisfied` or `Risk Accepted`.
- Link the accepted contract reference from sprint planning or story metadata.
- Move Story 9.8 (`9-8-solution-build-green-on-clean-clone`) to `done` so the solution build / CI signal is reliable before resuming Epic 7 work.
- Confirm the accepted contract includes typed GDPR command methods, query/status/certificate methods, capability detection semantics, and bounded failure behavior for unauthorized, forbidden, gone/erased, degraded, timeout, malformed, and contract-unavailable states.
- Explicitly decide whether the current local `IAdminPortalGdprClient`, `HttpAdminPortalGdprClient`, and `AdminPortalGdprRoutes` are the accepted contract, a temporary bridge, or implementation scaffolding that must be replaced. Until the dependency record says so, treat them as provisional and do not expand behavior around assumptions.
- Confirm FrontComposer route support for `/admin/parties`, `/admin/parties/{partyId}`, and `/admin/parties/{partyId}/gdpr` remains accepted.
- Confirm the contract forbids retired Parties REST endpoints, admin-only endpoints, DAPR actors, projection actors, local search services, controllers, and actor-host internals from UI components.
- Confirm privacy rules for tokens, tenant context, party data, logs, telemetry, storage keys, URLs, filenames, callbacks, and rendered failure summaries.

## Current Implementation Snapshot

Use this snapshot after the gates are satisfied. Do not recreate these pieces from scratch.

- `src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor` already renders the compact GDPR surface: erasure actions, erasure status/certificate display, retry verification, restriction actions, consent management, portability export, and processing records.
- `PartyGdprOperationsPanel` already composes `AdminPortalGdprCapability` with existing busy, erased, erasure-pending, restriction, and validation guards. A future implementation must preserve that composition rather than replacing it with a single enabled/disabled Boolean.
- `src/Hexalith.Parties.AdminPortal/Services/AdminPortalGdprCapability.cs` already models unavailable, degraded, available, and partial operation support. The exact unavailable reason is `Blocked on accepted EventStore-fronted Parties client/gateway contract`.
- `src/Hexalith.Parties.AdminPortal/Services/AdminPortalGdprStateCoordinator.cs` tracks active tenant/party context and request version. Continue using it for stale response and tenant-switch safety.
- `src/Hexalith.Parties.AdminPortal/Services/IPartiesAdminPortalApiClient.cs` and `PartiesAdminPortalApiClient.cs` expose the AdminPortal-facing GDPR operation methods and fail closed when the GDPR client is missing or transport/query failures occur.
- `src/Hexalith.Parties.Client/AdminPortal/IAdminPortalGdprClient.cs` and `HttpAdminPortalGdprClient.cs` exist, but the planning dependency has not accepted them as the contract of record. `HttpAdminPortalGdprClient` currently has mixed maturity: some commands post through the EventStore command gateway, some queries derive from `PartyDetail`, and erasure-certificate / retry-verification support still reports contract unavailable.
- `src/Hexalith.Parties.AdminPortal/Services/GdprExportFileNameBuilder.cs` re-derives export filenames from party id plus UTC timestamp. Keep this defense-in-depth even if the accepted contract returns a filename.
- `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs` already covers GDPR capability gating, partial support, malformed/null capability fail-closed behavior, tenant-switch capability invalidation, state clearing, erasure confirmation, retry status refresh, processing-record rendering, safe export filename behavior, localization, accessibility, and privacy guardrails.

## Dev Notes

### Keystone Implementation Gap (read first)

The GDPR panels, services, client, and most tests already exist (see Current Implementation Snapshot). The one substantive code gap is that the **production capability is dishonest**: `PartiesAdminPortalApiClient.GetGdprCapabilityAsync` returns `AdminPortalGdprCapability.Available()` whenever the provisional client is registered — and it always is (`src/Hexalith.Parties.Client/Extensions/PartiesClientServiceCollectionExtensions.cs:44` wires `HttpAdminPortalGdprClient`). `Available()` claims all eight operations work, but `HttpAdminPortalGdprClient` reports erasure-certificate and retry-verification as contract-unavailable. Story 7.7's core work is to make the capability reflect the provisional bridge's true surface so those two operations stay disabled with the exact bounded blocker, while the seven genuinely-working operations stay enabled. Everything else is verification plus test coverage. Do NOT add or fake the two unavailable operations. See Task 1.

### Architecture Guardrails

- The Admin Portal is a FrontComposer domain surface implemented with Blazor/Razor and Fluent UI Blazor. Keep UI code in AdminPortal / FrontComposer-facing projects.
- Do not add REST controllers, Swagger/OpenAPI endpoints, in-process MCP tools, DAPR actor calls, projection-actor calls, or EventStore stream browsing to `src/Hexalith.Parties` for this story. The actor host remains an actor-hosted domain service.
- Route supported commands through the accepted typed Parties client / EventStore command boundary only after the dependency record accepts that boundary.
- GDPR operation behavior must stay tenant-safe, privacy-safe, and fail-closed. Stale responses, tenant switches, auth changes, capability changes, and erased terminal states must clear or suppress sensitive state before rendering a status.
- All displayed text must flow through `AdminPortalLabels` or the FrontComposer localization mechanism. Do not hard-code new user-facing text in components.
- Accessibility guardrails from Story 7.9 apply to this story: controls must be keyboard reachable, status changes must be announced through polite regions, state must not be color-only, and focus must remain predictable.
- Privacy guardrails from Story 7.10 apply to this story: no raw HTML rendering, no raw ProblemDetails/parser details, no party PII in URLs/storage/logs/telemetry/filenames/DOM ids, and no browser storage coupling for sensitive operation state.

### Story Intelligence From Prior Epic 7 Work

- Story 7.6 delivered the fail-closed GDPR capability gate. Future work must build on that gate instead of weakening it.
- Story 7.8 moved labels and lawful-basis text through `AdminPortalLabels`; GDPR operation outcomes must remain localizable.
- Story 7.9 added accessibility coverage around focus, row state descriptions, and polite status regions; GDPR operation panels inherit those expectations.
- Story 7.10 added privacy source scans and ARIA leakage tests; operation panels must not reintroduce raw party ids/names into generated attributes or unsafe rendering APIs.

### Technical Version Notes

- Project package pin: `Microsoft.FluentUI.AspNetCore.Components` `5.0.0-rc.2-26098.1` via `Directory.Packages.props`.
- The available Fluent UI Blazor MCP documentation is for `5.0.0.26098`, which is not an exact match. Use existing local component patterns as authoritative and do not upgrade Fluent UI as part of Story 7.7 unless a separate dependency decision explicitly approves it.
- Current `FluentButton` usage relies on `Disabled`, `Appearance`, and existing autofocus/status-region patterns. Preserve these patterns unless the accepted component version requires a focused migration.

### Required Tests After Unblock

- Extend `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs` for every accepted GDPR operation path: erasure, status/certificate, verification retry, restriction/lift, consent add/revoke/history, export, and processing records.
- Extend `tests/Hexalith.Parties.AdminPortal.Tests/Services/PartiesAdminPortalApiClientTests.cs` when mapping accepted client/gateway responses, capability data, or failure semantics.
- Cover unsupported partial capabilities, malformed capability payloads, stale responses, tenant switch, sign-out, missing tenant, forbidden, gone/erased, timeout, transient failure, bounded validation, redaction, safe filenames, and no raw exported payload logging.
- Run focused AdminPortal tests first: `dotnet test tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release`.
- Run the Release solution build only after Story 9.8 has closed the clean-clone build gate or explicitly record any known unrelated build blocker.

### Source References

- `_bmad-output/planning-artifacts/epics.md`, Epic 7 Story 7.7.
- `_bmad-output/planning-artifacts/ux-admin-portal-2026-05-10.md`, GDPR Operation Flows, Empty And Error States, Localization, Accessibility, Privacy Rules.
- `_bmad-output/planning-artifacts/architecture.md`, D20 Administration Frontend: FrontComposer Domain Surface.
- `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md`.
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-22.md`.
- `_bmad-output/implementation-artifacts/7-6-gate-gdpr-operations-on-accepted-client-contract.md`.
- `_bmad-output/implementation-artifacts/7-8-localize-admin-console-text-and-status.md`.
- `_bmad-output/implementation-artifacts/7-9-enforce-admin-console-accessibility.md`.
- `_bmad-output/implementation-artifacts/7-10-enforce-admin-console-privacy-and-encoding-rules.md`.

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-05-22 | 0.1 | Created blocked story artifact because the accepted EventStore-fronted Parties client/gateway contract remains unsatisfied. | Codex |
| 2026-05-22 | 0.2 | Refreshed blocked story context with Story 9.8 second gate, current AdminPortal/GDPR implementation snapshot, prior Epic 7 guardrails, and post-unblock test requirements. | Codex |
| 2026-05-23 | 0.3 | Re-verified both gates remain live: primary dependency record still `Required`; Story 9.8 still `review` (not `done`). Confirmed zero source drift since v0.2 across the eight cited AdminPortal/Client/Tests files. Story remains `blocked` — no sprint-status flip. | Claude Opus 4.7 |
| 2026-05-23 | 0.4 | Primary scheduling gate RISK-ACCEPTED (scoped to Story 7.7) via `sprint-change-proposal-2026-05-23.md`; secondary gate (Story 9.8) now `done`. Existing AdminPortal GDPR client accepted as a temporary bridge. Blocking Status superseded by Gate Resolution. Next step: `create-story` to author tasks and flip `blocked → ready-for-dev`. | Claude Opus 4.7 |
| 2026-05-23 | 0.5 | Authored Tasks/Subtasks against the accepted provisional contract after exhaustive source analysis of the AdminPortal panel, capability model, API client, provisional GDPR client, labels, sub-panels, and existing tests. Identified the keystone gap (production `GetGdprCapabilityAsync` returns `Available()` while the provisional client reports certificate + retry as contract-unavailable) and a `Keystone Implementation Gap` note. Status moved `blocked → ready-for-dev`; sprint-status updated. | Claude Opus 4.7 |
| 2026-05-23 | 0.6 | Implemented keystone Task 1: added `AdminPortalGdprCapability.ProvisionalBridge()` factory + additive `CanReadErasureCertificate` flag; `PartiesAdminPortalApiClient.GetGdprCapabilityAsync` now returns `ProvisionalBridge()` (7 working ops enabled; erasure-certificate + retry-verification disabled with the exact bounded blocker) instead of the dishonest `Available()`; gated the panel certificate fetch on `CanReadErasureCertificate`. Added 6 tests (2 API-client capability, 4 component: provisional-bridge enable/blocker, erased-refresh certificate-skip, consent add/revoke, restrict/lift). Focused AdminPortal suite green (106 passed). Status `in-progress → review`. | Claude Opus 4.7 (1M context) |
| 2026-05-23 | 0.7 | Code review applied 5 patches: certificate capability now participates in support/state clearing; erasure blocker accounts for certificate support; erasure confirmation renders party id only; export download forces `application/json`; processing record summaries are bounded. Added focused tests for each patch. AdminPortal suite green (108 passed). Status `review → done`. | Codex GPT-5 |

## Dev Agent Record

### Agent Model Used

Codex GPT-5 (create-story); Claude Opus 4.7 (1M context) (dev-story)

### Debug Log References

- Loaded Story 7.7 source from `_bmad-output/planning-artifacts/epics.md`.
- Loaded dependency gate `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md`; status is still `Required`.
- Confirmed readiness follow-up explicitly requires the dependency to be `Satisfied` or `Risk Accepted` before scheduling Story 7.6 or Story 7.7.
- Refreshed 2026-05-22: loaded sprint-change proposal for Story 9.8, sprint status, Story 7.6, Stories 7.8-7.10, architecture D20, admin UX, current AdminPortal GDPR components/services, and AdminPortal component/API test coverage.
- Checked Fluent UI Blazor documentation metadata; available MCP docs target `5.0.0.26098`, while this project pins `5.0.0-rc.2-26098.1`, so local patterns remain authoritative.
- Re-verified 2026-05-23: dependency record `dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md` still `status: Required` (line 4). Story `9-8-solution-build-green-on-clean-clone` still `review` (sprint-status.yaml line 262). `git log --since=2026-05-22` on `src/Hexalith.Parties.AdminPortal`, `src/Hexalith.Parties.Client/AdminPortal`, and `tests/Hexalith.Parties.AdminPortal.Tests` shows no commits after the v0.2 refresh — Story 7.6/7.8/7.9/7.10 implementations landed on 2026-05-22 and are already reflected in this artifact. Spot-checked `AdminPortalGdprCapability.cs` (the `ContractUnavailableReason` const matches the UX-mandated blocker string exactly), `GdprExportFileNameBuilder.cs` (re-derives `party-{token}-export-{yyyyMMddHHmmss}Z.json` from party id + UTC timestamp, sanitized + 64-char bounded), and `AdminPortalGdprStateCoordinator.cs` (tracks `ActiveTenantId`/`ActivePartyId`/`RequestVersion` with `TryApplyResponse` stale-response rejection); snapshot section remains accurate.
- 2026-05-23 (correct-course): Primary scheduling gate RISK-ACCEPTED (scoped to Story 7.7) via `sprint-change-proposal-2026-05-23.md`; dependency record flipped `Required → Risk Accepted` with the existing AdminPortal GDPR client accepted as a temporary bridge. Secondary gate confirmed resolved — `9-8-solution-build-green-on-clean-clone` is now `done` (sprint-status line 264). Story now awaits `create-story 7.7` task authoring before `dev-story`; no production code changed in this transaction.
- 2026-05-23 (create-story): Authored Tasks/Subtasks. Read in full: `PartyGdprOperationsPanel.razor`, `ErasureStatusPanel.razor`, `AdminPortalGdprCapability.cs`, `AdminPortalGdprStateCoordinator.cs`, `GdprExportFileNameBuilder.cs`, `IPartiesAdminPortalApiClient.cs`, `PartiesAdminPortalApiClient.cs`, `IAdminPortalGdprClient.cs`, `HttpAdminPortalGdprClient.cs`, `AdminPortalLabels.cs` (GDPR block), `PartiesAdminPortalComponentTests.cs` (2378 lines), `PartiesAdminPortalApiClientTests.cs`, `RecordingAdminPortalApiClient.cs`, plus the risk-acceptance proposal and dependency record. Verified: `HttpAdminPortalGdprClient` is DI-registered (`PartiesClientServiceCollectionExtensions.cs:44`), so production `GetGdprCapabilityAsync` returns `Available()`; `GetErasureCertificateAsync` throws `ContractUnavailable` and `RetryErasureVerificationAsync` returns `ContractUnavailable` (`HttpAdminPortalGdprClient.cs:48-55`); `GdprOperationContractBlocked` == `AdminPortalGdprCapability.ContractUnavailableReason` (`AdminPortalLabels.cs:174`); `PartiesAdminPortalApiClientTests` has zero `GetGdprCapabilityAsync` coverage. Tasks scoped accordingly; Status `blocked → ready-for-dev`.
- 2026-05-23 (dev-story): Implemented Task 1. `AdminPortalGdprCapability` gained an additive `CanReadErasureCertificate` flag (true only for `Available()`; false for `Unavailable`/`Degraded`/`ProvisionalBridge`; optional trailing param on `Partial`, default false) and a new `ProvisionalBridge()` factory (request-erasure, read-erasure-status, restrict, lift, manage-consent, export, processing-records enabled; retry-verification + read-erasure-certificate disabled; `Reason = ContractUnavailableReason`). `PartiesAdminPortalApiClient.GetGdprCapabilityAsync` now returns `ProvisionalBridge()` when the GDPR client is registered (previously `Available()`); the `_gdprClient is null → Unavailable()` fail-closed branch is unchanged. `PartyGdprOperationsPanel.RefreshErasureStatusAsync` now gates the certificate fetch on `_gdprCapability.CanReadErasureCertificate`, so an erased/verified party refresh against the provisional bridge no longer attempts the contract-unavailable certificate query (the report panel renders the existing "Certificate unavailable" fallback). `ErasureDisabledReason` already surfaced the bounded blocker via `CapabilityReason` because `ProvisionalBridge.CanRetryVerification` is false and `Reason == ContractUnavailableReason` — no panel logic change needed (Task 1.5 verified).
- 2026-05-23 (dev-story tests): Added 2 API-client tests (`GetGdprCapabilityAsync_WithoutGdprClient_FailsClosedToUnavailableAsync`; `GetGdprCapabilityAsync_WithProvisionalGdprClient_ReportsHonestProvisionalBridgeAsync`) and 4 component tests (provisional-bridge enables 7 ops + retry button absent + bounded blocker shown; erased-party refresh skips the certificate fetch and shows no failure; consent add+revoke per-channel/per-purpose happy path with history render; restrict-then-lift happy path with bounded reason forwarded). Added optional `canReadErasureCertificate` param to `Partial(...)` so the existing `EnableOnlySupportedPartialContractActions` test still compiles; `Available()` keeps certificate fetch true so the recording double still serves certificates where existing tests expect.
- 2026-05-23 (dev-story validation): Focused suite `dotnet test tests/Hexalith.Parties.AdminPortal.Tests --configuration Release` → 106 passed / 0 failed / 0 skipped (clean compile under `TreatWarningsAsErrors=true`). Full `dotnet build Hexalith.Parties.slnx --configuration Release` compiled every `Hexalith.Parties.*` library and test project AND failed only in `src/Hexalith.Parties.AppHost/Program.cs` (5 errors: `AddHexalithEventStore` has no `redis` param; `Projects.Hexalith_Memories_Server` missing; undefined `memoriesAccessControlConfigPath`/`ResolveOptionalSiblingProjectPath`; `memories` local-scope collision). This failure is KNOWN-UNRELATED to Story 7.7: it is EventStore/Memories submodule-drift in the actor-host topology wiring (explicitly out of scope per the story's Architecture Guardrails), `AdminPortalGdprCapability` is consumed only by AdminPortal (+ its tests), and `git status` confirms zero AppHost/EventStore/Memories source files were modified in this story. Recorded per Task 4.2 rather than silenced; not fixed (cross-submodule, out of scope).
- 2026-05-23 (code-review): Ran three review layers (blind, edge-case, acceptance). Applied 5 patches: added `CanReadErasureCertificate` to `HasAnySupport`; clear stale certificates when certificate capability is absent; include certificate support in erasure disabled-reason logic; render party id in erasure confirmation without party name/PII; force export JS download content type to `application/json`; bound processing record summaries to 160 characters. Added focused component/API-client tests. Verification: `dotnet test tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release` → 108 passed / 0 failed / 0 skipped.

### Completion Notes List

- Both scheduling gates are resolved (primary RISK-ACCEPTED scoped to 7.7; secondary 9.8 `done`); Tasks/Subtasks authored; story is `ready-for-dev`. No production code changed during create-story.
- Story 7.6 provides the fail-closed capability gate; this story builds on it and must not weaken it.
- The accepted contract is the provisional bridge (`IAdminPortalGdprClient` / `HttpAdminPortalGdprClient` / `AdminPortalGdprRoutes`); build only on methods that genuinely exist. Certificate + retry verification stay disabled with the exact blocker — do not fake them.
- Keystone work is Task 1: make `GetGdprCapabilityAsync` honest. The panel scaffolding and most tests already exist; the rest is verification + added coverage.

#### Dev-story outcome (2026-05-23)

- DONE: keystone delivered. `GetGdprCapabilityAsync` now reports the honest provisional-bridge surface via `AdminPortalGdprCapability.ProvisionalBridge()`. The seven genuinely-working operations stay enabled; erasure-certificate and verification-retry stay disabled with the exact bounded blocker `Blocked on accepted EventStore-fronted Parties client/gateway contract`. Neither unavailable operation was faked or stubbed as working.
- The Story 7.6 fail-closed gate (`_gdprClient is null → Unavailable()`) is preserved unchanged. No REST/Swagger/MCP/DAPR-actor/projection-actor calls were added to `src/Hexalith.Parties` (no actor-host files touched).
- Certificate fetch is gated on the additive `CanReadErasureCertificate` flag, eliminating the misleading contract-unavailable failure when refreshing an erased party against the provisional bridge.
- Focused AdminPortal suite: 106 passed / 0 failed. KNOWN-UNRELATED build blocker: `src/Hexalith.Parties.AppHost/Program.cs` fails to compile against the current EventStore/Memories submodule pins (topology wiring drift, out of scope for 7.7) — see Debug Log References.

#### Code-review outcome (2026-05-23)

- DONE: all 5 review patch findings resolved. Certificate capability now composes correctly with aggregate support and stale certificate clearing; erasure confirmation shows the party id only; export content type is forced to `application/json`; processing record summaries are bounded before rendering.
- Focused AdminPortal suite: 108 passed / 0 failed.

### File List

Production:

- `src/Hexalith.Parties.AdminPortal/Services/AdminPortalGdprCapability.cs` — added `CanReadErasureCertificate` flag + `ProvisionalBridge()` factory; added optional `canReadErasureCertificate` param to `Partial(...)`; set the flag in every factory.
- `src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs` — `GetGdprCapabilityAsync` returns `ProvisionalBridge()` instead of `Available()` when the GDPR client is registered.
- `src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor` — gated the erasure-certificate fetch in `RefreshErasureStatusAsync` on `_gdprCapability.CanReadErasureCertificate`.
- `src/Hexalith.Parties.AdminPortal/Components/ProcessingRecordsPanel.razor` — bounds processing record summaries before rendering.

Tests:

- `tests/Hexalith.Parties.AdminPortal.Tests/Services/PartiesAdminPortalApiClientTests.cs` — added 2 `GetGdprCapabilityAsync` tests (Unavailable fail-closed; provisional-bridge honest surface).
- `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs` — added 4 component tests (provisional-bridge enable/blocker; erased-refresh certificate-skip; consent add/revoke; restrict/lift).

Story artifact:

- `_bmad-output/implementation-artifacts/7-7-implement-gdpr-operation-panels.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
