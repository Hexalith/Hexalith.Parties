---
baseline_commit: 721eed3
---

# Story 5.3: My data & privacy - request / cancel erasure (FR-Consumer-4)

Status: done

<!-- Note: Ultimate context engine analysis completed - comprehensive developer guide created. -->

## Story

As a consumer,
I want to delete my data with honest, reversible-until-it-starts copy,
so that I understand exactly what will happen.

## Acceptance Criteria

1. Given `/me/privacy`, when the erasure control renders, then the primary verb is "Delete my data", the right is named nearby as right to erasure / right to be forgotten, the action uses `ButtonAppearance.Outline` with danger semantics, and the acknowledgement uses neutral/info tone rather than success-green.
2. Given no erasure request is active, when I choose "Delete my data", then the UI asks for a single explicit confirmation before submitting, does not use native `alert`/`confirm`/`prompt`, does not require typed-name personal data, and invokes request only through a Consumer self-scoped path with no caller-supplied party id.
3. Given an erasure request is accepted or pending, when its state is shown, then copy states both honest halves: "You can cancel until deletion begins" while status is cancellable, and "Once it's done, it's permanent - we can't undo it" for completed deletion; any 30-day wording commits only to starting/working the request and is not presented as the cancel window.
4. Given deletion has not begun, when I cancel the request, then cancellation is invoked via a self-scoped path, the state reconciles from authoritative status, and all copy/DOM/logs remain PII-free with one visible status source.
5. Given deletion has begun or completed, when cancellation is unavailable or rejected, then the cancel action is not shown or is disabled with neutral explanation; the UI never claims cancellation succeeded, never implies completed deletion is reversible, and maps backend rejection to bounded PII-free copy.
6. Given erased, unavailable, stale/degraded, forbidden/unbound, unauthenticated, transient/server/timeout failure, or circuit-disconnect states, when `/me/privacy` renders or erasure request/cancel is attempted, then the page keeps existing export content visible where possible, uses the established safe status vocabulary, exposes no raw ids/problem details/payloads, and does not navigate away for routine failures.
7. Given Story 5.3 is the erasure request/cancel slice, when files are changed, then no processing-records summary UI, consent workflow changes, identity-binding redesign, public browser endpoint, direct EventStore browser call, DAPR ACL change, EventStore/Tenants/FrontComposer submodule edit, package upgrade, or new third-party library is introduced.

## Tasks / Subtasks

- [x] Replace the erasure preview on `/me/privacy` with the real request/cancel panel (AC: 1, 2, 3, 5, 6)
  - [x] Update `src/Hexalith.Parties.ConsumerPortal/Components/MyPrivacyPage.razor`; keep `@page "/me/privacy"`, `[Authorize(Policy = "Consumer")]`, the existing export section, `IConsumerPrivacyExportClient`, and JS download behavior from Story 5.2.
  - [x] Add an erasure section below export that uses "Delete my data" as the button label and names "right to erasure" nearby in supporting copy.
  - [x] Use outline/danger styling for reversible-until-start erasure request; do not reuse the admin danger-fill typed-name pattern for the consumer request unless a future requirement makes the action immediate and irreversible.
  - [x] Use one explicit in-app confirmation step. Do not use native browser dialogs; do not ask the user to type their display name, party id, email, or any PII.
  - [x] Add request, pending, cancellable, cancellation-in-progress, permanent/erased, transient failure, and unavailable states without hiding the existing export state.

- [x] Add a ConsumerPortal-owned erasure port and UI-host adapter (AC: 2, 4, 5, 6, 7)
  - [x] Add an RCL-owned port under `src/Hexalith.Parties.ConsumerPortal/Services/`, for example `IConsumerPrivacyErasureClient`.
  - [x] Port methods must be no-caller-id, for example `GetMyErasureStatusAsync(CancellationToken)`, `RequestMyErasureAsync(CancellationToken)`, and `CancelMyErasureAsync(CancellationToken)`.
  - [x] Add DTO/result types that carry only bounded values needed by the component: erasure UI state, cancellability, optional freshness/status, and safe outcome. Do not include `PartyId`, `TenantId`, `CorrelationId`, display name, email, contact values, raw backend details, or command payloads.
  - [x] Add a UI-host adapter under `src/Hexalith.Parties.UI/Services/` that implements the ConsumerPortal port and delegates only to `ISelfScopedPartiesClient`.
  - [x] Register the adapter as Scoped in `src/Hexalith.Parties.UI/Program.cs` after `AddSelfScopedPartiesClient()`, matching the profile/edit/consent/export adapters.
  - [x] Keep `src/Hexalith.Parties.ConsumerPortal/Hexalith.Parties.ConsumerPortal.csproj` free of references to `Hexalith.Parties.UI`, AdminPortal, Server, Projections, Security, Testing, direct gateway clients, or internal host projects.

- [x] Close the cancel-erasure backend contract gap narrowly (AC: 3, 4, 5, 7)
  - [x] Do not misuse `LiftRestrictionAsync` as erasure cancellation. It lifts processing restriction only; it does not cancel `ErasePartyRequested` or reset `ErasureStatus`.
  - [x] Add an additive `CancelPartyErasure` command in `src/Hexalith.Parties.Contracts/Commands/` and an additive `PartyErasureCancelled` event in `src/Hexalith.Parties.Contracts/Events/`, unless an equivalent approved command already exists by implementation time.
  - [x] Add `PartyAggregate.Handle(CancelPartyErasure, PartyState?)`: allow success only from `ErasureStatus.ErasurePending` (the cancellable-before-key-destroyed state); return no-op for `Active`; reject `KeyDestroyed`, `VerificationInProgress`, `Verified`, and `Erased` with bounded `PartyErasureInProgress` copy.
  - [x] Add `PartyState.Apply(PartyErasureCancelled)` to return `ErasureStatus` to `Active` and keep `ErasedAt` null. Do not attempt to restore data after key destruction.
  - [x] Add a FluentValidation validator for `CancelPartyErasure` following `ErasePartyValidator` style.
  - [x] Update `IAdminPortalGdprClient`, `HttpAdminPortalGdprClient`, and `AdminPortalGdprRoutes` with a cancel method/route that posts through `/api/v1/commands` with `Domain="party"` and the self-resolved aggregate id.
  - [x] Add `ISelfScopedPartiesClient.CancelMyErasureAsync(CancellationToken)` and implement it by resolving the current principal's bound party id and delegating to the GDPR client cancel method.
  - [x] Update contract API snapshot only as an intentional additive contract change; do not remove or rename existing public contract members.

- [x] Make erasure status authoritative enough for the UI (AC: 3, 4, 5, 6)
  - [x] Current `GetMyErasureStatusAsync` exists, but `HttpAdminPortalGdprClient.GetErasureStatusAsync` currently derives only terminal erased status from `PartyDetail`; it does not expose `ErasurePending` or cancellation eligibility.
  - [x] Wire the existing `GetErasureStatus` route/query to return `PartyErasureStatusRecord` for pending/in-progress/verified/erased states, preferably via `IPartyErasureRecordStore.GetStatusAsync` when available, with safe fallback to `PartyDetail.IsErased`.
  - [x] Persist/update status when erasure is requested, cancelled, key deletion starts, verification completes/fails, and terminal erased state is reached. Status strings must stay bounded: `Active`, `ErasurePending`, `KeyDestroyed`, `VerificationInProgress`, `Verified`, `Erased`, or an existing bounded verification status.
  - [x] UI cancellability is true only for `ErasurePending`; it is false for `KeyDestroyed`, `VerificationInProgress`, `Verified`, and `Erased`.
  - [x] If the backend cannot prove cancellability, fail closed: do not show an enabled cancel action.

- [x] Preserve regulated copy, accessibility, and Consumer visual posture (AC: 1, 3, 5, 6)
  - [x] Add all erasure labels, descriptions, status text, confirmation copy, failure copy, and disabled copy to `Resources/ConsumerPortalResources.resx` and expose them through `ConsumerPortalLabels`.
  - [x] Required copy signals include "Delete my data", "right to erasure", "You can cancel until deletion begins", and "Once it's done, it's permanent - we can't undo it."
  - [x] Do not use "It'll be gone within 30 days" or any copy that makes 30 days sound like a guaranteed completion deadline or cancel window.
  - [x] Use exactly one visible erasure status source at a time. Accepted/pending/refresh/freshness use `role="status" aria-live="polite"`; rejection, transient failure, forbidden/unbound, and blocking failure use inline `role="alert"`.
  - [x] Do not move focus for routine request/pending/cancel transitions. Move focus only to a blocking alert or confirmation dialog when it helps recovery.
  - [x] Keep the Consumer page phone-first, single-column, roomy, and 16px body text. Use Fluent/FrontComposer tokens and CSS isolation only if needed.
  - [x] No raw hex/rgb/hsl colors, no raw `#0097A7`, no color-only state, no spinner-only screen, no success-green erasure acknowledgement, and no PII in copy, DOM attributes, logs, telemetry, URLs, or alert text.

- [x] Add focused tests and static guards (AC: 1-7)
  - [x] Add/extend `tests/Hexalith.Parties.ConsumerPortal.Tests/Components/MyPrivacyPageTests.cs` for request rendering, confirmation, pending/cancellable copy, permanent copy, cancel action, cancellation rejection after deletion begins, transient failure, PII-free DOM, no hard 30-day promise, and one visible erasure status source.
  - [x] Add ConsumerPortal service/DTO tests proving the erasure port has no caller-supplied `partyId`, no list/search/direct gateway surface, no `PagedResult<>`, and no UI-host/internal references.
  - [x] Update `ConsumerPortalPackagingTests` to allow only the new erasure port while continuing to forbid `Hexalith.Parties.UI`, `ISelfScopedPartiesClient`, `IPartiesQueryClient`, `IPartiesCommandClient`, `IAdminPortalGdprClient`, direct `GetPartyAsync(`, `ListPartiesAsync`, `SearchPartiesAsync`, and methods/DTOs with id-shaped public properties inside ConsumerPortal.
  - [x] Add UI-host adapter tests proving Scoped registration and delegation to `ISelfScopedPartiesClient.RequestMyErasureAsync`, `GetMyErasureStatusAsync`, and `CancelMyErasureAsync` without caller-supplied party ids.
  - [x] Update `SelfScopedPartiesClientTests` and surface tripwires: no list/search members, no `PagedResult<>`, no party-id parameters, and fail-closed unbound/ambiguous principals call no underlying client for every self-scoped method.
  - [x] Add aggregate tests for `CancelPartyErasure`: pending emits cancel event; active no-ops; key-destroyed/verification/erased reject with bounded status; apply resets to `Active`.
  - [x] Add client contract tests proving request and cancel post the right command types to `/api/v1/commands`, status query returns bounded `PartyErasureStatusRecord`, and raw ProblemDetails are sanitized.
  - [x] Update `tests/e2e/specs/consumer-portal-routes.spec.ts`: bound Consumer can request/cancel through the self-scoped fixture path, Admin/unauthenticated users cannot see Consumer erasure data, no browser-visible EventStore gateway calls occur, and forbidden timing/irreversibility copy regressions are absent.

- [x] Validate the focused implementation (AC: 1-7)
  - [x] Run `git diff --check`.
  - [x] Run `dotnet build src/Hexalith.Parties.Contracts/Hexalith.Parties.Contracts.csproj -c Release --no-restore -m:1 -v:minimal`.
  - [x] Run `dotnet build src/Hexalith.Parties.Client/Hexalith.Parties.Client.csproj -c Release --no-restore -m:1 -v:minimal`.
  - [x] Run `dotnet build src/Hexalith.Parties.ConsumerPortal/Hexalith.Parties.ConsumerPortal.csproj -c Release --no-restore -m:1 -v:minimal`.
  - [x] Run `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release --no-restore -m:1 -v:minimal`.
  - [x] Run `dotnet build tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj -c Release --no-restore -m:1 -v:minimal`.
  - [x] Run `dotnet build tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj -c Release --no-restore -m:1 -v:minimal`.
  - [x] Run `dotnet build tests/Hexalith.Parties.ConsumerPortal.Tests/Hexalith.Parties.ConsumerPortal.Tests.csproj -c Release --no-restore -m:1 -v:minimal`.
  - [x] Run `dotnet build tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -c Release --no-restore -m:1 -v:minimal`.
  - [x] Run focused xUnit v3 executables for Contracts, Server, Client, ConsumerPortal, and UI tests when `dotnet test`/MTP is unreliable.
  - [x] Attempt `pwsh scripts/test.ps1 -Lane unit` if the environment allows it; record exact wrapper/MTP/socket limitations instead of claiming success.

## Dev Notes

### Current Implementation State

- Sprint status has Epic 5 in progress, Stories 5.1 and 5.2 done, and Story 5.3 in backlog before this story file was created. [Source: `_bmad-output/implementation-artifacts/sprint-status.yaml#development_status`]
- Story 5.3 covers FR-Consumer-4's erasure slice: request/cancel erasure, plain "Delete my data" verb, cancellable-until-start state, permanent-once-complete state, neutral/info acknowledgement, and no 30-day cancel-window promise. [Source: `_bmad-output/planning-artifacts/epics.md#Story-5.3-My-data--privacy-request--cancel-erasure-FR-Consumer-4`]
- `/me/privacy` is now the real Story 5.2 export page. It injects `IConsumerPrivacyExportClient` and `IJSRuntime`, renders export/preparing/ready/failure states, uses `DotNetStreamReference`, and still has a static erasure preview section. [Source: `src/Hexalith.Parties.ConsumerPortal/Components/MyPrivacyPage.razor`]
- The existing export section must remain working. Do not regress `HexalithPartiesConsumerPortal.downloadJson`, empty-payload handling, JS failure mapping, or PII-free JSON download behavior from Story 5.2. [Source: `_bmad-output/implementation-artifacts/5-2-my-data-privacy-export-my-data-fr-consumer-4.md#Completion-Notes-List`]
- `ISelfScopedPartiesClient` already exposes `RequestMyErasureAsync(CancellationToken)` and `GetMyErasureStatusAsync(CancellationToken)`. It does not expose `CancelMyErasureAsync` yet. [Source: `src/Hexalith.Parties.UI/Services/ISelfScopedPartiesClient.cs`]
- `SelfScopedPartiesClient.RequestMyErasureAsync` resolves the current principal's bound party id and delegates to `IAdminPortalGdprClient.RequestErasureAsync`. `GetMyErasureStatusAsync` delegates to `IAdminPortalGdprClient.GetErasureStatusAsync`. [Source: `src/Hexalith.Parties.UI/Services/SelfScopedPartiesClient.cs`]
- `IAdminPortalGdprClient` has request/status/retry/restrict/lift/consent/export/processing methods. It has no cancel-erasure method. [Source: `src/Hexalith.Parties.Client/AdminPortal/IAdminPortalGdprClient.cs`]
- `HttpAdminPortalGdprClient.GetErasureStatusAsync` currently calls `GetPartyDetailAsync` and returns a status only when `PartyDetail.IsErased` is true; pending/cancellable states are invisible through this method today. [Source: `src/Hexalith.Parties.Client/AdminPortal/HttpAdminPortalGdprClient.cs#GetErasureStatusFromPartyDetailAsync`]
- `AdminPortalGdprRoutes` already names `ErasureStatus`, but `PartyDetailProjectionQueryActor.CanHandle` does not include `GetErasureStatus` today. [Source: `src/Hexalith.Parties.Client/AdminPortal/AdminPortalGdprRoutes.cs`] [Source: `src/Hexalith.Parties/Queries/PartyDetailProjectionQueryActor.cs#CanHandle`]
- `PartyAggregate.Handle(EraseParty)` emits `ErasePartyRequested`, no-ops if already pending or erased, and does not provide a cancel path. [Source: `src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs#Handle-EraseParty`]
- `PartyState.Apply(ErasePartyRequested)` sets `ErasureStatus.ErasurePending`; `Apply(PartyEncryptionKeyDeleted)` sets `KeyDestroyed`; `Apply(ErasureVerified)` sets `Verified`; `Apply(PartyErased)` sets terminal `Erased`. [Source: `src/Hexalith.Parties.Contracts/State/PartyState.cs`]
- `LiftRestriction` is unrelated to erasure cancellation. It only emits `RestrictionLifted` when `state.IsRestricted` is true and does not change `ErasureStatus`. [Source: `src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs#Handle-LiftRestriction`]

### Current Files Being Modified - Required Reading

- `src/Hexalith.Parties.ConsumerPortal/Components/MyPrivacyPage.razor` (UPDATE)
  - Current state: real export page plus erasure preview.
  - What this story changes: add real erasure request/status/cancel UI while preserving export.
  - Preserve: route, Consumer policy, existing export status/download/failure behavior, no raw ids/payloads.
- `src/Hexalith.Parties.ConsumerPortal/Components/MyPrivacyPage.razor.css` (UPDATE only if needed)
  - Current state: shared Consumer privacy/profile layout, token-based colors, no raw color literals except token fallbacks.
  - What this story changes: erasure section styling if necessary.
  - Preserve: phone-first layout, token colors, no success-green erasure state.
- `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerPortalLabels.cs` and `Resources/ConsumerPortalResources.resx` (UPDATE)
  - Current state: resource-backed export and erasure-preview copy.
  - What this story changes: erasure request/cancel/status/failure/confirmation copy.
  - Preserve: no regulated/user-facing inline copy in components.
- `src/Hexalith.Parties.ConsumerPortal/Services/*Privacy*Erasure*` (NEW)
  - Current state: export port exists; erasure port does not.
  - What this story changes: add a no-caller-id ConsumerPortal erasure port and bounded DTOs.
  - Preserve: no UI-host/internal references, no party/tenant/correlation id surface.
- `src/Hexalith.Parties.UI/Services/ISelfScopedPartiesClient.cs` and `SelfScopedPartiesClient.cs` (UPDATE)
  - Current state: request/status exist; cancel missing.
  - What this story changes: add `CancelMyErasureAsync` only if the backend cancel command is added.
  - Preserve: no list/search, no `PagedResult<>`, no party-id parameters, fail-closed generic errors, Scoped lifetime.
- `src/Hexalith.Parties.UI/Services/*Privacy*Erasure*` (NEW)
  - Current state: export adapter shows the Story 5.2 pattern.
  - What this story changes: implement ConsumerPortal erasure port by delegating to self-scoped request/status/cancel.
  - Preserve: mapping only, no PII logging, no raw backend details.
- `src/Hexalith.Parties.UI/Program.cs` (UPDATE)
  - Current state: registers self-scoped accessor, profile/edit/consent/export adapters.
  - What this story changes: register erasure adapter as Scoped after `AddSelfScopedPartiesClient()`.
  - Preserve: `ValidateScopes=true`, no eager gateway resolution in degraded/test boot.
- `src/Hexalith.Parties.Contracts/Commands/`, `Events/`, `State/PartyState.cs` (UPDATE/NEW)
  - Current state: erasure request and lifecycle events exist; cancel does not.
  - What this story changes: additive cancel command/event/state apply.
  - Preserve: immutable sealed records, infrastructure-free Contracts, public API snapshot discipline.
- `src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs` and `src/Hexalith.Parties/Validation/` (UPDATE)
  - Current state: request/lifecycle handlers; validator for `EraseParty`.
  - What this story changes: cancel handler plus validator.
  - Preserve: guard cascade, idempotency/no-op behavior, bounded rejection events.
- `src/Hexalith.Parties.Client/AdminPortal/` (UPDATE)
  - Current state: EventStore gateway GDPR client has request/status but no cancel, and status only sees terminal erased.
  - What this story changes: add cancel method/route and authoritative status query support.
  - Preserve: `/api/v1/commands` and `/api/v1/queries` only, sanitized details, no public UI endpoints.
- `src/Hexalith.Parties/Queries/PartyDetailProjectionQueryActor.cs` (UPDATE)
  - Current state: supports GetParty, PartyDetail, ExportPartyData, GetProcessingRecords, GetErasureCertificate.
  - What this story changes: support `GetErasureStatus` if needed for pending/cancellable state.
  - Preserve: tenant id allowlist, PII-free logs, actor routing validation.
- Tests listed in Tasks (UPDATE/NEW)
  - Current state: 5.2 export component/adapter/static tests exist.
  - What this story changes: add erasure UI, adapter, self-scope, aggregate, client, contract, and e2e guards.
  - Preserve: source scans exclude `bin/` and `obj/`.

### Architecture Guardrails

- ConsumerPortal is an independent RCL. It must not reference `Hexalith.Parties.UI`, AdminPortal, `ISelfScopedPartiesClient`, gateway clients, `StatusKind` from UI internals, Server, Projections, Security, or Testing. Use a ConsumerPortal-owned port with a UI-host adapter. [Source: `_bmad-output/planning-artifacts/architecture.md#Project-Structure--Boundaries`]
- Consumer data access is own-data-only. The UI BFF resolves the Consumer's `party_id` and injects it. ConsumerPortal must never accept route ids, query ids, hidden form party ids, arbitrary ids, list/search, or direct `GetPartyAsync(partyId)`. [Source: `_bmad-output/planning-artifacts/architecture.md#Authentication--Security`] [Source: `src/Hexalith.Parties.UI/Services/ISelfScopedPartiesClient.cs`]
- Browser traffic talks only to the UI host. Do not add public ConsumerPortal APIs, direct browser calls to EventStore, actor-host calls, DAPR ACLs, CORS-dependent cancel/request URLs, or browser bearer-token flows. [Source: `_bmad-output/planning-artifacts/architecture.md#API--Communication-Patterns`]
- Use the EventStore gateway typed client path. Erasure request uses `EraseParty`; cancel must be an additive command if implemented. Do not add actor-host endpoints or direct DAPR calls from the UI. [Source: `_bmad-output/planning-artifacts/architecture.md#API--Communication-Patterns`]
- Event contracts evolve additively only. New command/event types are acceptable when required; removing/renaming existing fields or meanings is not. [Source: `_bmad-output/project-context.md#Critical-Dont-Miss-Rules-anti-patterns--gotchas`]
- Central Package Management is on. Do not add package versions to csproj files, bump packages, or add a third-party dialog/state library. [Source: `_bmad-output/project-context.md#Technology-Stack--Versions`]
- The GDPR copy contract is architectural: erasure copy commits to the start of the obligation, states completed erasure is permanent, and uses neutral/info acknowledgement. [Source: `_bmad-output/planning-artifacts/architecture.md#Project-Context-Analysis`]

### Domain and Contract Guidance

- `EraseParty` requires `PartyId` and `TenantId`; the self-scoped accessor supplies party id through the underlying client, never through ConsumerPortal. [Source: `src/Hexalith.Parties.Contracts/Commands/EraseParty.cs`] [Source: `src/Hexalith.Parties.UI/Services/SelfScopedPartiesClient.cs`]
- `ErasePartyRequested` carries `PartyId`, `TenantId`, `RequestedAt`, and `RequestedBy`. Treat all ids/operator fields as backend/audit metadata; do not display them in Consumer copy. [Source: `src/Hexalith.Parties.Contracts/Events/ErasePartyRequested.cs`]
- Cancellable means `ErasureStatus.ErasurePending` only. Once `PartyEncryptionKeyDeleted` moves state to `KeyDestroyed`, crypto-shredding has begun and cancellation must be unavailable. [Source: `src/Hexalith.Parties.Contracts/State/PartyState.cs`] [Source: `docs/gdpr-key-rotation-and-shredding.md`]
- Terminal erased status is privacy preserving: UI may show stable erased/permanent state but must not expose destroyed-key messages, cryptographic exception text, stale display names, contact values, identifiers, or raw command/query payloads. [Source: `docs/gdpr-erased-party-status.md`]
- `PartyErasureStatusRecord` currently carries `PartyId`, `TenantId`, `Status`, `UpdatedAt`, optional `ErasedAt`, and optional `ErrorMessage`. Consumer DTOs must not expose the ids/error text; map them to bounded UI states. [Source: `src/Hexalith.Parties.Contracts/Security/PartyErasureStatusRecord.cs`]
- `AdminPortalGdprOutcome` already distinguishes accepted, validation rejected, forbidden, missing tenant, erasure in progress, erased, not found, authentication required, transient failure, contract unavailable, and unknown. Reuse bounded outcomes instead of showing raw ProblemDetails. [Source: `src/Hexalith.Parties.Client/AdminPortal/AdminPortalGdprOutcome.cs`]

### UX and Accessibility Guardrails

- Required consumer erasure copy: "Delete my data" / "You can cancel until deletion begins. Once it's done, it's permanent - we can't undo it." [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Voice-and-Tone`]
- Erasure requested/in-progress has two honest states: cancellable until deletion begins, and permanent once complete. Do not present 30 days as the cancel window. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#State-Patterns`]
- Review findings explicitly fixed legal-copy risks: no hard 30-day SLA, state completed erasure is irreversible, and lead with plain "Delete my data" rather than legalese "Erasure." [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/review-regulated-language.md#Findings`]
- Reversible GDPR actions use outline and single confirmation; irreversible erase uses danger fill with typed confirmation in admin contexts. Consumer request is reversible until start, so use outline/danger semantics rather than success-green or filled destructive overstatement. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Component-Patterns`]
- Accepted/pending/freshness states use `role="status" aria-live="polite"` with no focus steal. Rejection/failure states use inline `role="alert"`. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Accessibility-Floor`]
- Consumer pages are phone-first, single-column, roomy, and 16px body text. This is a working product surface, not a landing page. [Source: `_bmad-output/planning-artifacts/architecture.md#Frontend-Architecture`]
- Do not use raw teal `#0097A7`, raw hex/rgb/hsl colors, color-only state, spinner-only screens, native dialogs, or success-green deletion language. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Accessibility-Floor`]

### Previous Story Intelligence

- Story 5.2 implemented the real `/me/privacy` export surface. Keep the export port, UI-host adapter, JS download helper, PII-free DOM assertions, and one export status source intact while adding erasure. [Source: `_bmad-output/implementation-artifacts/5-2-my-data-privacy-export-my-data-fr-consumer-4.md#Dev-Notes`]
- Story 5.2 established `IConsumerPrivacyExportClient` in ConsumerPortal and `ConsumerPrivacyExportClient` in the UI host. Mirror this pattern for erasure instead of exposing self-scoped or AdminPortal clients to the RCL. [Source: `src/Hexalith.Parties.ConsumerPortal/Services/IConsumerPrivacyExportClient.cs`] [Source: `src/Hexalith.Parties.UI/Services/ConsumerPrivacyExportClient.cs`]
- Story 5.1 established the Epic 5 regulated-copy pattern: resource-backed labels, strict packaging guard, one status source, and no raw consent/channel/operator ids in DOM/copy. Mirror this for erasure. [Source: `_bmad-output/implementation-artifacts/5-1-my-consent-grant-withdraw-with-honest-lawful-basis-split-fr-consumer-3.md#Dev-Notes`]
- Story 4.5 and 5.1 showed adapter registration order matters: `AddSelfScopedPartiesClient()` before ConsumerPortal adapters. Extend the existing composition test; do not weaken it. [Source: `tests/Hexalith.Parties.UI.Tests/PartiesUiHostCompositionTests.cs#Program_RegistersConsumerPortalAdaptersAfterSelfScopedClient`]
- Existing self-scope tests already cover request/status erasure path and fail-closed unbound/ambiguous principals. Add cancel to the same tripwire set. [Source: `tests/Hexalith.Parties.UI.Tests/SelfScopedPartiesClientTests.cs`]
- Recent commits are sequential and scoped: Story 4.3 ConsumerPortal stand-up, 4.4 read-only profile, 4.5 edit profile, 5.1 consent, and 5.2 export. Keep Story 5.3 focused on erasure request/cancel only. [Source: `git log -5`]

### Testing and Validation Guidance

- Use xUnit v3, bUnit, Shouldly, and NSubstitute. Do not introduce Moq, FluentAssertions, raw `Assert.*`, Verify snapshots unless needed, package versions in csproj files, or new third-party libraries. [Source: `_bmad-output/project-context.md#Testing-Rules`]
- Required ConsumerPortal component tests:
  - `/me/privacy` renders both existing export and new erasure controls.
  - Initial erasure state uses "Delete my data", names right to erasure nearby, uses outline action, and contains no "Erasure" primary button text.
  - Request confirmation is in-app and accessible; no native dialogs, no typed-name PII.
  - Accepted/pending state shows "You can cancel until deletion begins" and exactly one polite status.
  - Permanent/erased state shows "Once it's done, it's permanent - we can't undo it" and no enabled cancel.
  - Cancel action delegates through the erasure port and reconciles status.
  - Key-destroyed/verification/erased cancellation rejection shows bounded alert and never claims success.
  - Transient failure, forbidden/unbound, unavailable status, and stale/degraded status expose no raw ids/details/payloads.
  - Page markup contains no hard timing promise such as "It'll be gone within 30 days", "within 30 days" as a cancel window, or "successfully deleted" success-green copy.
- Required backend/client tests:
  - `CancelPartyErasure` command/event are additive and included in contract snapshot.
  - Aggregate cancel succeeds only from `ErasurePending`; active no-ops; key-destroyed/verification/erased reject.
  - `PartyState.Apply(PartyErasureCancelled)` resets to `Active`.
  - `HttpAdminPortalGdprClient.CancelErasureAsync` posts command type `CancelPartyErasure` to `/api/v1/commands`.
  - `GetErasureStatusAsync` can return pending/cancellable status, not only terminal erased.
- Required boundary/static tests:
  - ConsumerPortal has no reference to `Hexalith.Parties.UI` or AdminPortal.
  - ConsumerPortal contains no `IPartiesQueryClient`, `IPartiesCommandClient`, `IAdminPortalGdprClient`, `ISelfScopedPartiesClient`, direct `GetPartyAsync(`, `ListPartiesAsync`, `SearchPartiesAsync`, public endpoint code, or methods/DTOs with party-id shaped properties.
  - UI host registers the erasure adapter as Scoped and delegates only to self-scoped erasure methods.
  - Self-scope surface tripwires remain green.
  - Source scans exclude `bin/` and `obj/`.
- Required E2E updates:
  - `/me/privacy` route is reachable for bound Consumers and not shown to Admin-only/unauthenticated users.
  - Bound Consumer erasure request/cancel reaches the server-side fixture/self-scoped path for `party-bound-001`; the browser sees no `/api/v1/commands` or `/api/v1/queries` calls.
  - Erasure copy states cancellable-until-start and permanent-once-done; forbidden timing and success-green wording are absent.
  - Cancellation unavailable after deletion begins can be asserted through fixture status if supported; otherwise add selector/typecheck coverage and label runtime fixture limits precisely.

### Latest Technical Information

- Microsoft Learn's current Blazor lifecycle guidance says a component must be left in a valid render state before awaiting incomplete lifecycle work. Use this for erasure status load/request/cancel flows so `/me/privacy` never renders a broken intermediate state. [Source: `https://learn.microsoft.com/en-us/aspnet/core/blazor/components/lifecycle?view=aspnetcore-10.0`]
- Microsoft Learn's Blazor DI guidance keeps server-side Scoped services circuit-scoped. Keep erasure adapters Scoped and compatible with `ValidateScopes=true`; never capture self-scoped services from singletons. [Source: `https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/dependency-injection?view=aspnetcore-10.0`]
- Microsoft Learn's current Blazor download guidance from Story 5.2 remains relevant because `/me/privacy` keeps export behavior: stream downloads are for relatively small files and object URLs must be revoked. Do not change export download architecture in this erasure story. [Source: `https://learn.microsoft.com/en-us/aspnet/core/blazor/file-downloads?view=aspnetcore-10.0`]
- No package upgrade is required. Use the pinned local stack: .NET SDK `10.0.302`, FluentUI Blazor `5.0.0-rc.3`, xUnit v3, bUnit, Shouldly, and NSubstitute. [Source: `_bmad-output/project-context.md#Technology-Stack--Versions`]

### Project Structure Notes

- Likely new files:
  - `src/Hexalith.Parties.Contracts/Commands/CancelPartyErasure.cs`
  - `src/Hexalith.Parties.Contracts/Events/PartyErasureCancelled.cs`
  - `src/Hexalith.Parties/Validation/CancelPartyErasureValidator.cs`
  - `src/Hexalith.Parties.ConsumerPortal/Services/IConsumerPrivacyErasureClient.cs`
  - `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerPrivacyErasureResult.cs`
  - `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerPrivacyErasureState.cs`
  - `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerPrivacyErasureOutcome.cs`
  - `src/Hexalith.Parties.UI/Services/ConsumerPrivacyErasureClient.cs`
  - `tests/Hexalith.Parties.UI.Tests/ConsumerPrivacyErasureClientTests.cs`
- Likely updated files:
  - `src/Hexalith.Parties.ConsumerPortal/Components/MyPrivacyPage.razor`
  - `src/Hexalith.Parties.ConsumerPortal/Components/MyPrivacyPage.razor.css` if styling is needed
  - `src/Hexalith.Parties.ConsumerPortal/Resources/ConsumerPortalResources.resx`
  - `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerPortalLabels.cs`
  - `src/Hexalith.Parties.Contracts/State/PartyState.cs`
  - `src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs`
  - `src/Hexalith.Parties.Client/AdminPortal/IAdminPortalGdprClient.cs`
  - `src/Hexalith.Parties.Client/AdminPortal/HttpAdminPortalGdprClient.cs`
  - `src/Hexalith.Parties.Client/AdminPortal/AdminPortalGdprRoutes.cs`
  - `src/Hexalith.Parties/Queries/PartyDetailProjectionQueryActor.cs`
  - `src/Hexalith.Parties.UI/Services/ISelfScopedPartiesClient.cs`
  - `src/Hexalith.Parties.UI/Services/SelfScopedPartiesClient.cs`
  - `src/Hexalith.Parties.UI/Program.cs`
  - `tests/Hexalith.Parties.ConsumerPortal.Tests/Components/MyPrivacyPageTests.cs`
  - `tests/Hexalith.Parties.ConsumerPortal.Tests/Packaging/ConsumerPortalPackagingTests.cs`
  - `tests/Hexalith.Parties.UI.Tests/PartiesUiHostCompositionTests.cs`
  - `tests/Hexalith.Parties.UI.Tests/SelfScopedPartiesClientTests.cs`
  - `tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateErasureTests.cs`
  - `tests/Hexalith.Parties.Client.Tests/AdminPortal/AdminPortalGdprOperationContractTests.cs`
  - `tests/Hexalith.Parties.Contracts.Tests/Package/ContractsPublicApiSnapshot.txt`
  - `tests/e2e/specs/consumer-portal-routes.spec.ts`
- Keep generated/build artifacts out of the repo. Do not edit or commit `bin/`, `obj/`, or `obj/**/generated/**`.

### Out of Scope

- No processing-records summary UX or "Manage all consent" privacy-card deep link; Story 5.4 owns it.
- No export architecture changes beyond preserving Story 5.2 behavior.
- No consent toggle, consent catalogue, or lawful-basis behavior changes; Story 5.1 owns them.
- No identity binding redesign, self-registration, IdP federation, binding-store persistence, or Keycloak mapper changes.
- No broad Parties projection rebuild, search, picker, AdminPortal redesign, production KMS, or key-rotation feature work beyond what is strictly required to expose authoritative erasure status and cancellation.
- No EventStore, Tenants, FrontComposer, Memories, DAPR ACL, deployment, or package upgrade changes.
- No public UI API endpoint, direct EventStore browser call, actor-host endpoint, browser token flow, webroot export file store, or new app host sidecar.
- No broad shared UI package refactor unless a build-breaking dependency issue proves it is strictly required.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-5.3-My-data--privacy-request--cancel-erasure-FR-Consumer-4`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Authentication--Security`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#API--Communication-Patterns`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Frontend-Architecture`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Project-Structure--Boundaries`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/review-regulated-language.md`]
- [Source: `_bmad-output/project-context.md`]
- [Source: `_bmad-output/implementation-artifacts/5-2-my-data-privacy-export-my-data-fr-consumer-4.md`]
- [Source: `_bmad-output/implementation-artifacts/5-1-my-consent-grant-withdraw-with-honest-lawful-basis-split-fr-consumer-3.md`]
- [Source: `src/Hexalith.Parties.ConsumerPortal/Components/MyPrivacyPage.razor`]
- [Source: `src/Hexalith.Parties.UI/Services/ISelfScopedPartiesClient.cs`]
- [Source: `src/Hexalith.Parties.UI/Services/SelfScopedPartiesClient.cs`]
- [Source: `src/Hexalith.Parties.Client/AdminPortal/IAdminPortalGdprClient.cs`]
- [Source: `src/Hexalith.Parties.Client/AdminPortal/HttpAdminPortalGdprClient.cs`]
- [Source: `src/Hexalith.Parties.Client/AdminPortal/AdminPortalGdprRoutes.cs`]
- [Source: `src/Hexalith.Parties.Contracts/Commands/EraseParty.cs`]
- [Source: `src/Hexalith.Parties.Contracts/Events/ErasePartyRequested.cs`]
- [Source: `src/Hexalith.Parties.Contracts/State/PartyState.cs`]
- [Source: `src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs`]
- [Source: `docs/gdpr-erased-party-status.md`]
- [Source: `docs/gdpr-key-rotation-and-shredding.md`]

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet test tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj -c Release --no-build` failed before test execution with MTP named-pipe socket permission error.
- `UPDATE_CONTRACTS_API_SNAPSHOT=1 tests/Hexalith.Parties.Contracts.Tests/bin/Release/net10.0/Hexalith.Parties.Contracts.Tests` updated the public API snapshot; full assembly run then exposed unrelated existing admin-readonly and NuGet vulnerability-feed failures.
- `pwsh scripts/test.ps1 -Lane unit` was attempted; wrapper exited 0 but printed repeated `Build failed with exit code: 1` restore/build messages without detailed failing project output.

### Completion Notes List

- Replaced the `/me/privacy` erasure preview with a resource-backed, self-scoped request/cancel panel using in-app confirmation, outline/danger styling, cancellable-until-start copy, permanent erased copy, bounded alerts, and no typed PII confirmation.
- Added ConsumerPortal erasure port/DTOs plus UI-host adapter registered after `AddSelfScopedPartiesClient()`, delegating request/status/cancel only through `ISelfScopedPartiesClient`.
- Added additive `CancelPartyErasure` command and `PartyErasureCancelled` event, aggregate/state/validator support, typed GDPR client cancel route, and self-scoped cancel method.
- Wired authoritative erasure status through `GetErasureStatus` and `IPartyErasureRecordStore` with safe terminal fallback, plus status-store updates for erasure request/cancel/lifecycle events.
- Expanded component, packaging, UI adapter/self-scope, aggregate, client, gateway, contract snapshot, and E2E fixture/spec coverage.

### File List

- `_bmad-output/implementation-artifacts/5-3-my-data-privacy-request-cancel-erasure-fr-consumer-4.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/implementation-artifacts/tests/test-summary.md`
- `_bmad-output/story-automator/orchestration-1-20260609-205725.md`
- `src/Hexalith.Parties.Client/AdminPortal/AdminPortalGdprRoutes.cs`
- `src/Hexalith.Parties.Client/AdminPortal/HttpAdminPortalGdprClient.cs`
- `src/Hexalith.Parties.Client/AdminPortal/IAdminPortalGdprClient.cs`
- `src/Hexalith.Parties.ConsumerPortal/Components/MyPrivacyPage.razor`
- `src/Hexalith.Parties.ConsumerPortal/Components/MyPrivacyPage.razor.css`
- `src/Hexalith.Parties.ConsumerPortal/Resources/ConsumerPortalResources.resx`
- `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerPortalLabels.cs`
- `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerPrivacyErasureOutcome.cs`
- `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerPrivacyErasureResult.cs`
- `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerPrivacyErasureState.cs`
- `src/Hexalith.Parties.ConsumerPortal/Services/IConsumerPrivacyErasureClient.cs`
- `src/Hexalith.Parties.Contracts/Commands/CancelPartyErasure.cs`
- `src/Hexalith.Parties.Contracts/Events/PartyErasureCancelled.cs`
- `src/Hexalith.Parties.Contracts/State/PartyState.cs`
- `src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs`
- `src/Hexalith.Parties.UI/Program.cs`
- `src/Hexalith.Parties.UI/Services/ConsumerPrivacyErasureClient.cs`
- `src/Hexalith.Parties.UI/Services/ISelfScopedPartiesClient.cs`
- `src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs`
- `src/Hexalith.Parties.UI/Services/SelfScopedPartiesClient.cs`
- `src/Hexalith.Parties/Domain/PartyDomainServiceInvoker.cs`
- `src/Hexalith.Parties/Queries/PartyDetailProjectionQueryActor.cs`
- `src/Hexalith.Parties/Validation/CancelPartyErasureValidator.cs`
- `tests/Hexalith.Parties.AdminPortal.Tests/Services/PartiesAdminPortalApiClientTests.cs`
- `tests/Hexalith.Parties.AdminPortal.Tests/Services/RecordingAdminPortalApiClient.cs`
- `tests/Hexalith.Parties.Client.Tests/AdminPortal/AdminPortalGdprOperationContractTests.cs`
- `tests/Hexalith.Parties.ConsumerPortal.Tests/Components/MyPrivacyPageTests.cs`
- `tests/Hexalith.Parties.ConsumerPortal.Tests/Packaging/ConsumerPortalPackagingTests.cs`
- `tests/Hexalith.Parties.Contracts.Tests/Package/ContractsPublicApiSnapshot.txt`
- `tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateErasureTests.cs`
- `tests/Hexalith.Parties.Tests/Domain/PartyDomainServiceInvokerValidationTests.cs`
- `tests/Hexalith.Parties.Tests/Gateway/PartyDetailProjectionQueryActorTests.cs`
- `tests/Hexalith.Parties.UI.Tests/ConsumerPrivacyErasureClientTests.cs`
- `tests/Hexalith.Parties.UI.Tests/PartiesUiHostCompositionTests.cs`
- `tests/Hexalith.Parties.UI.Tests/SelfScopedPartiesClientTests.cs`
- `tests/e2e/specs/consumer-portal-routes.spec.ts`

### Senior Developer Review (AI)

Reviewer: Administrator on 2026-06-10

Outcome: Approved after automatic fixes. No critical issues remain.

Findings fixed:

- [HIGH] `ConsumerPrivacyErasureClient.CancelMyErasureAsync` forced the UI back to active/cancelled when the refreshed authoritative status still reported `ErasurePending`, contradicting AC4's reconcile-from-authoritative-status requirement. Fixed by preserving the refreshed pending state unless the authoritative status is active.
- [HIGH] `PartyDomainServiceInvoker.InvokeRetryErasureVerificationAsync` could leave the persisted erasure status at `Verified` after a complete retry emitted `PartyErased`, so status reads could fail to show the terminal permanent state required by AC3/AC5. Fixed by persisting lifecycle status updates from emitted retry events, ending at `Erased`.
- [MEDIUM] The story File List did not include the domain invoker validation test touched by the review fix, nor existing story-automation artifact changes. Updated File List for traceability.

Validation:

- `git diff --check` passed.
- `dotnet build src/Hexalith.Parties.Contracts/Hexalith.Parties.Contracts.csproj -c Release --no-restore -m:1 -v:minimal` passed.
- `dotnet build src/Hexalith.Parties.Client/Hexalith.Parties.Client.csproj -c Release --no-restore -m:1 -v:minimal` passed.
- `dotnet build src/Hexalith.Parties.ConsumerPortal/Hexalith.Parties.ConsumerPortal.csproj -c Release --no-restore -m:1 -v:minimal` passed.
- `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release --no-restore -m:1 -v:minimal` passed.
- `dotnet build tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj -c Release --no-restore -m:1 -v:minimal` passed.
- `dotnet build tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj -c Release --no-restore -m:1 -v:minimal` passed.
- `dotnet build tests/Hexalith.Parties.ConsumerPortal.Tests/Hexalith.Parties.ConsumerPortal.Tests.csproj -c Release --no-restore -m:1 -v:minimal` passed.
- `dotnet build tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -c Release --no-restore -m:1 -v:minimal` passed.
- `dotnet build tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj -c Release --no-restore -m:1 -v:minimal` passed.
- Focused xUnit executable classes passed: server aggregate erasure tests (26), client GDPR contract tests (17), ConsumerPortal privacy/packaging tests (35), UI erasure/composition/self-scope tests (61), gateway/domain query and invoker tests (33), and contracts public API snapshot test (1).
- `dotnet test` for focused projects still failed at the local test wrapper build handoff with only `Build failed with exit code: 1`; direct builds and xUnit v3 executables were used for actionable validation.
- `pwsh scripts/test.ps1 -Lane unit` exited 0 but printed repeated `Build failed with exit code: 1` restore/build messages without detailed failing project output, matching the existing environment limitation.

### Change Log

- 2026-06-10: Implemented Story 5.3 consumer erasure request/cancel UI, self-scoped adapter path, additive backend cancel contract, authoritative erasure status, and focused validation coverage. Status set to review.
- 2026-06-10: Senior review auto-fixed cancellation authoritative-status reconciliation and retry-verification terminal erasure status persistence. Status set to done.
