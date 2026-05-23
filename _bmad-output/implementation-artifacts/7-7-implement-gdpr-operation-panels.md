# Story 7.7: Implement GDPR Operation Panels

Status: blocked

<!-- This story remains intentionally blocked. Do not move to ready-for-dev until every gate in Required To Unblock is satisfied. -->

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

## Blocking Status

Story 7.7 must not be scheduled for implementation yet. Both gates were re-verified on 2026-05-23 and remain live.

Primary gate:

- `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md` is still `status: Required` (confirmed 2026-05-23).
- That dependency says Story 7.7 must not be scheduled until the accepted EventStore-fronted Parties client/gateway contract is updated to `Satisfied` or `Risk Accepted` and linked from sprint planning.

Secondary gate:

- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-22.md` adds Story 9.8 as a second gate before Epic 7 / Epic 8 active work resumes.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` still tracks `9-8-solution-build-green-on-clean-clone: review` (not `done`) as of 2026-05-23.
- Epic 5 retrospective escalates Story 9.8 to a program-level critical-path gate before any new story work in Epics 7, 8, or 9 except Story 9.8 itself.

Story 7.6 already implemented the fail-closed capability gate for this condition. Implementing Story 7.7 now would bypass the planning gate and would require guessing the accepted typed command/query contract.

## Required To Unblock

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

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- Loaded Story 7.7 source from `_bmad-output/planning-artifacts/epics.md`.
- Loaded dependency gate `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md`; status is still `Required`.
- Confirmed readiness follow-up explicitly requires the dependency to be `Satisfied` or `Risk Accepted` before scheduling Story 7.6 or Story 7.7.
- Refreshed 2026-05-22: loaded sprint-change proposal for Story 9.8, sprint status, Story 7.6, Stories 7.8-7.10, architecture D20, admin UX, current AdminPortal GDPR components/services, and AdminPortal component/API test coverage.
- Checked Fluent UI Blazor documentation metadata; available MCP docs target `5.0.0.26098`, while this project pins `5.0.0-rc.2-26098.1`, so local patterns remain authoritative.
- Re-verified 2026-05-23: dependency record `dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md` still `status: Required` (line 4). Story `9-8-solution-build-green-on-clean-clone` still `review` (sprint-status.yaml line 262). `git log --since=2026-05-22` on `src/Hexalith.Parties.AdminPortal`, `src/Hexalith.Parties.Client/AdminPortal`, and `tests/Hexalith.Parties.AdminPortal.Tests` shows no commits after the v0.2 refresh — Story 7.6/7.8/7.9/7.10 implementations landed on 2026-05-22 and are already reflected in this artifact. Spot-checked `AdminPortalGdprCapability.cs` (the `ContractUnavailableReason` const matches the UX-mandated blocker string exactly), `GdprExportFileNameBuilder.cs` (re-derives `party-{token}-export-{yyyyMMddHHmmss}Z.json` from party id + UTC timestamp, sanitized + 64-char bounded), and `AdminPortalGdprStateCoordinator.cs` (tracks `ActiveTenantId`/`ActivePartyId`/`RequestVersion` with `TryApplyResponse` stale-response rejection); snapshot section remains accurate.

### Completion Notes List

- No production implementation started because the story is blocked by a required planning dependency.
- Story 7.6 already provides fail-closed UI behavior until the accepted contract exists.
- Story remains `blocked`; sprint status was not moved to `ready-for-dev`.
- Current code already has GDPR panel scaffolding and a provisional client shape, but the accepted client/gateway dependency still decides whether that shape is valid for implementation.

### File List

- `_bmad-output/implementation-artifacts/7-7-implement-gdpr-operation-panels.md`
