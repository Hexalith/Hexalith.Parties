---
baseline_commit: e796138
---

# Story 5.2: My data & privacy - export my data (FR-Consumer-4)

Status: done

<!-- Note: Ultimate context engine analysis completed - comprehensive developer guide created. -->

## Story

As a consumer,
I want to export my own data,
so that I can keep it or move it elsewhere.

## Acceptance Criteria

1. Given `/me/privacy`, when I request an export, then `ExportPartyData` is invoked only through the self-scoped Consumer path and produces a machine-readable JSON package.
2. Given the export request starts, when the UI enters its preparing state, then the visible copy makes no time promise and uses the approved text: "Preparing your export - this can take a little while. We'll show it here the moment it's ready."
3. Given the export is ready, when the current implementation receives the existing `AdminPortalExportDownload`, then the page surfaces an in-app download action for the JSON file without exposing browser-visible EventStore command/query calls.
4. Given the future slow/large async path is introduced, when a SignalR or polling readiness signal reports the export ready, then the same in-app download-ready state is used; synchronous download remains the happy path, not a promised baseline.
5. Given the export service is unreachable, the JS download helper is unavailable, the export payload is empty, the query returns a transient/server/timeout failure, or the circuit disconnects during download, when the request fails, then a `TransientFailure` message says "Your data is safe - try again.", offers retry/backoff, keeps prior content visible, and does not claim the export completed.
6. Given an erased, unavailable, restricted, stale, degraded, forbidden/unbound, or unauthenticated self, when `/me/privacy` renders or export is attempted, then the state maps to the existing safe status vocabulary, remains PII-free, never displays raw ids/problem details/payloads, and does not navigate away for routine export failures.
7. Given this is Story 5.2 only, when files are changed, then no erasure request/cancel workflow, processing-records summary UI, consent workflow changes, identity-binding redesign, Parties aggregate/event/projection change, public endpoint, DAPR ACL change, EventStore/Tenants/FrontComposer submodule edit, package upgrade, or new third-party library is introduced.

## Tasks / Subtasks

- [x] Replace the `/me/privacy` placeholder with the export-capable My data & privacy surface (AC: 1, 2, 3, 5, 6)
  - [x] Update `src/Hexalith.Parties.ConsumerPortal/Components/MyPrivacyPage.razor` so it no longer renders the generic `ConsumerRouteShell` placeholder.
  - [x] Keep `@page "/me/privacy"` and `@attribute [Authorize(Policy = "Consumer")]`.
  - [x] Render a cold-load/preparing status with `role="status" aria-live="polite"`; do not use a spinner-only screen.
  - [x] Add the export panel/action on the page with Consumer plain-language copy and machine-readable JSON expectation.
  - [x] Keep erasure and processing-record sections as non-operational previews or simple headings only if needed for layout; do not implement Stories 5.3 or 5.4.

- [x] Add a ConsumerPortal-owned privacy/export port and UI-host adapter (AC: 1, 3, 5, 6, 7)
  - [x] Add `IConsumerPrivacyExportClient` or an equivalently narrow ConsumerPortal-owned port under `src/Hexalith.Parties.ConsumerPortal/Services/`.
  - [x] The port must expose no-caller-id methods, for example `ExportMyDataAsync(CancellationToken)`; no method may accept `partyId`, return `PagedResult<>`, expose list/search, or reference UI-host/internal services.
  - [x] Add DTO/result types that carry only download-safe fields needed by the component: safe file name, content type, payload/stream, outcome, and optional freshness/status. Do not include `PartyId`, `TenantId`, `CorrelationId`, display name, email, contact values, backend detail, or raw payload preview as visible model fields.
  - [x] Add a UI-host adapter under `src/Hexalith.Parties.UI/Services/` that implements the port and delegates to `ISelfScopedPartiesClient.ExportMyDataAsync(...)`.
  - [x] Register the adapter as Scoped in `src/Hexalith.Parties.UI/Program.cs` after `AddSelfScopedPartiesClient()`, matching `ConsumerProfileDataClient`, `ConsumerProfileEditClient`, and `ConsumerConsentClient`.
  - [x] Keep `src/Hexalith.Parties.ConsumerPortal/Hexalith.Parties.ConsumerPortal.csproj` free of references to `Hexalith.Parties.UI`, AdminPortal, Server, Projections, Security, Testing, direct gateway clients, or internal host projects.

- [x] Implement download-ready behavior without leaking PII or trusting transport file names (AC: 2, 3, 5, 6)
  - [x] Use the existing backend path: `ISelfScopedPartiesClient.ExportMyDataAsync` -> `IAdminPortalGdprClient.ExportPartyDataAsync(myPartyId)` -> EventStore query `ExportPartyData`.
  - [x] Treat the returned `AdminPortalExportDownload` as the current synchronous happy path, but drive the UI through `Preparing` -> `Ready` so the UX is async-ready.
  - [x] Re-derive or validate the consumer-facing filename from non-PII components before download. Existing client filenames are `party-{sanitizedPartyId}-{yyyyMMddTHHmmssZ}.json`; AdminPortal also re-derives a non-PII name before JS download.
  - [x] Do not display the raw party id, tenant id, exported-by, correlation id, contact values, identifiers, display name, or export payload in the page, status text, alert text, logs, telemetry, URLs, or DOM attributes.
  - [x] Use `application/json` and do not attempt HTML preview of the export payload.
  - [x] Reject empty payloads as transient failure rather than showing a broken download.
  - [x] Cancel in-flight export work on component disposal and ignore stale completions after navigation/reload.

- [x] Add the browser download helper only at the UI-host boundary (AC: 3, 5, 7)
  - [x] Reuse the AdminPortal JS-helper pattern if possible, or add a host-level namespaced helper for Consumer export, for example `HexalithPartiesConsumerPortal.downloadJson`.
  - [x] Prefer Blazor's stream download pattern (`DotNetStreamReference` + JS interop) for the current JSON byte payload and keep the object URL revoked after click.
  - [x] Catch `JSDisconnectedException` and `JSException` and map them to transient failure copy; do not claim completion if JS interop fails.
  - [x] Do not add public HTTP download endpoints, webroot-stored files, browser calls to `/api/v1/queries`, direct EventStore URLs, or browser bearer-token flows.

- [x] Preserve regulated copy, accessibility, and Consumer visual posture (AC: 2, 5, 6)
  - [x] Add all page/export labels, status text, alert text, retry text, and download-ready text to `Resources/ConsumerPortalResources.resx` and expose them through `ConsumerPortalLabels`.
  - [x] Required copy includes: "Export my data", "Machine-readable JSON", "Preparing your export - this can take a little while. We'll show it here the moment it's ready.", and "Your data is safe - try again."
  - [x] Use exactly one visible export status source at a time. Preparing/ready/freshness use `role="status" aria-live="polite"`; transient/load/failure states use inline `role="alert"`.
  - [x] Do not move focus for routine preparing/ready transitions. Move focus only to a blocking alert when it helps recovery.
  - [x] Keep Consumer layout phone-first, single-column, roomy, and 16px body text. Use Fluent/FrontComposer tokens and CSS isolation only if styling is needed.
  - [x] No raw hex/rgb/hsl colors, no raw `#0097A7`, no color-only state, no spinner-only screen, no native `alert()`/`confirm()`/`prompt()`, and no success-green deletion/export overclaim language.

- [x] Add focused tests and static guards (AC: 1-7)
  - [x] Add `tests/Hexalith.Parties.ConsumerPortal.Tests/Components/MyPrivacyPageTests.cs` covering placeholder removal, export action rendering, preparing status, ready/download state, retry state, stale/degraded status, erased/unavailable safe state, no banned timing promises, and PII-free DOM.
  - [x] Add ConsumerPortal service/DTO tests proving the privacy export port has no caller-supplied party id, no list/search/direct gateway surface, no `PagedResult<>`, and no UI-host/internal references.
  - [x] Update `ConsumerPortalPackagingTests` to allow the new privacy/export port while continuing to forbid `Hexalith.Parties.UI`, `ISelfScopedPartiesClient`, `IPartiesQueryClient`, `IPartiesCommandClient`, `IAdminPortalGdprClient`, `ListPartiesAsync`, `SearchPartiesAsync`, direct `GetPartyAsync(`, and methods/DTOs with party-id shaped properties inside ConsumerPortal.
  - [x] Add UI-host adapter tests proving Scoped registration and delegation to `ISelfScopedPartiesClient.ExportMyDataAsync()` without caller-supplied party ids.
  - [x] Keep `SelfScopedPartiesClientSurfaceTests` passing: no list/search members, no `PagedResult<>`, no party-id parameters, and no request type with a party-id shaped property.
  - [x] Add component/JS interop tests proving the page uses one status source, treats empty payload and JS failure as transient failure, and does not expose raw ids or payload preview.
  - [x] Update e2e specs for `/me/privacy`: bound Consumer can request export through the self-scoped path, Admin/unauthenticated users cannot see Consumer export data, no browser-visible EventStore gateway calls occur, and no banned timing promise appears.

- [x] Validate the focused implementation (AC: 1-7)
  - [x] Run `git diff --check`.
  - [x] Run `dotnet build src/Hexalith.Parties.ConsumerPortal/Hexalith.Parties.ConsumerPortal.csproj -c Release --no-restore -m:1 -v:minimal`.
  - [x] Run `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release --no-restore -m:1 -v:minimal`.
  - [x] Run `dotnet build tests/Hexalith.Parties.ConsumerPortal.Tests/Hexalith.Parties.ConsumerPortal.Tests.csproj -c Release --no-restore -m:1 -v:minimal`.
  - [x] Run `dotnet build tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -c Release --no-restore -m:1 -v:minimal`.
  - [x] Run compiled focused xUnit v3 executables for ConsumerPortal and UI tests when `dotnet test`/MTP is unreliable.
  - [x] Run or update `tests/e2e/specs/consumer-portal-routes.spec.ts`; label Playwright typecheck/spec discovery separately from browser runtime proof if sandbox sockets block execution.
  - [x] Attempt `pwsh scripts/test.ps1 -Lane unit` if the environment allows it; record exact wrapper/MTP/socket limitations instead of claiming success.

## Dev Notes

### Current Implementation State

- Sprint status has Epic 5 in progress, Story 5.1 done, and Story 5.2 in backlog before this story file was created. [Source: `_bmad-output/implementation-artifacts/sprint-status.yaml#development_status`]
- Story 5.2 covers FR-Consumer-4's export slice only: `/me/privacy`, own-data export, async-looking preparing/ready UX, machine-readable JSON, and no timing promise. [Source: `_bmad-output/planning-artifacts/epics.md#Story-5.2-My-data--privacy-export-my-data-FR-Consumer-4`]
- `/me/privacy` currently renders only `ConsumerRouteShell` with two next-step bullets (`PrivacyNextExport`, `PrivacyNextErasure`). It does not load data, call export, render a download action, or handle failure states. [Source: `src/Hexalith.Parties.ConsumerPortal/Components/MyPrivacyPage.razor`]
- Story 5.1 completed the real `/me/consent` surface and established the current Epic 5 pattern: ConsumerPortal-owned port plus UI-host adapter plus self-scoped accessor. Reuse that pattern for export. [Source: `_bmad-output/implementation-artifacts/5-1-my-consent-grant-withdraw-with-honest-lawful-basis-split-fr-consumer-3.md#Completion-Notes-List`]
- `ISelfScopedPartiesClient` already exposes `ExportMyDataAsync(CancellationToken)` and the implementation already resolves the current principal's bound `party_id` fail-closed before delegating to `IAdminPortalGdprClient.ExportPartyDataAsync(...)`. [Source: `src/Hexalith.Parties.UI/Services/ISelfScopedPartiesClient.cs`] [Source: `src/Hexalith.Parties.UI/Services/SelfScopedPartiesClient.cs`]
- `IAdminPortalGdprClient.ExportPartyDataAsync(partyId, ct)` posts EventStore query `ExportPartyData` to `/api/v1/queries`, receives `PartyDataPortabilityPackage`, serializes it as JSON bytes, and returns `AdminPortalExportDownload`. No new public endpoint is needed. [Source: `src/Hexalith.Parties.Client/AdminPortal/HttpAdminPortalGdprClient.cs`]
- `PartyDetailProjectionQueryActor.BuildPortabilityPackageAsync` sets export status to `Exported`, `RestrictedExported`, `Erased`, or `PersonalDataUnavailable`; erased/unavailable packages have `Party = null`; freshness is included. [Source: `src/Hexalith.Parties/Queries/PartyDetailProjectionQueryActor.cs#BuildPortabilityPackageAsync`]
- AdminPortal already has a portability export panel and JS download pattern, including empty-payload and JS failure handling. Reuse the approach, not the AdminPortal component or service boundary. [Source: `src/Hexalith.Parties.AdminPortal/Components/PortabilityExportPanel.razor`] [Source: `src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor#ExportAsync`]

### Current Files Being Modified - Required Reading

- `src/Hexalith.Parties.ConsumerPortal/Components/MyPrivacyPage.razor` (UPDATE)
  - Current state: protected `/me/privacy` placeholder shell with static export/erasure next steps.
  - What this story changes: real export action, preparing/ready/retry states, download-ready UI, safe erased/unavailable/degraded handling.
  - Preserve: route, Consumer policy, Consumer copy register, no browser-visible gateway calls.
- `src/Hexalith.Parties.ConsumerPortal/Components/ConsumerRouteShell.razor` and `.razor.css` (READ/possibly UPDATE)
  - Current state: generic placeholder still useful for surfaces that remain unimplemented.
  - What this story may change: ideally no change; if adjusted, do not regress any remaining placeholder usage.
  - Preserve: polite status, Consumer area styling, no hard-coded colors.
- `src/Hexalith.Parties.ConsumerPortal/Components/FreshnessStatus.razor`, `MyProfilePage.razor`, and `MyConsentPage.razor` (READ/reuse patterns)
  - Current state: established ConsumerPortal local patterns for freshness, loading, failure, erased self, single status source, and no PII display.
  - What this story changes: copy those patterns into `/me/privacy`; do not import UI-host shared components directly into the RCL.
  - Preserve: ConsumerPortal boundary and resource-backed labels.
- `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerPortalLabels.cs` and `Resources/ConsumerPortalResources.resx` (UPDATE)
  - Current state: resource-backed profile, edit, consent, and placeholder privacy copy.
  - What this story changes: export labels/statuses/alerts/download-ready copy.
  - Preserve: no regulated/user-facing inline copy in feature components.
- `src/Hexalith.Parties.ConsumerPortal/Services/*Privacy*` or `*Export*` (NEW)
  - Current state: consent/profile ports exist; no privacy/export port exists.
  - What this story changes: add the narrow no-caller-id export port and DTOs.
  - Preserve: no `partyId`, list/search, direct gateway, UI host, AdminPortal, or internal host references.
- `src/Hexalith.Parties.UI/Services/ISelfScopedPartiesClient.cs` and `SelfScopedPartiesClient.cs` (READ; UPDATE only if required)
  - Current state: already has `ExportMyDataAsync`, `RequestMyErasureAsync`, `GetMyProcessingRecordsAsync`, and related self-scoped GDPR methods.
  - What this story likely changes: no change unless a missing result shape forces a narrow additive method; avoid broadening the self-scope surface.
  - Preserve: no list/search, no `PagedResult<>`, no party-id parameters, fail-closed generic errors, Scoped lifetime.
- `src/Hexalith.Parties.UI/Services/*Privacy*` or `*Export*` adapter (NEW)
  - Current state: `ConsumerConsentClient` shows the adapter pattern from Story 5.1.
  - What this story changes: implement the ConsumerPortal export port by delegating to `ISelfScopedPartiesClient.ExportMyDataAsync`.
  - Preserve: no business rules beyond mapping/result translation, no PII logging, no raw backend details.
- `src/Hexalith.Parties.UI/Program.cs` (UPDATE)
  - Current state: registers self-scoped accessor then profile/edit/consent ConsumerPortal adapters.
  - What this story changes: register privacy/export adapter as Scoped after `AddSelfScopedPartiesClient()`.
  - Preserve: `ValidateScopes=true`, no browser tokens, no DAPR sidecar, no eager gateway resolution in degraded/test boot.
- UI host JS asset/location (UPDATE/possibly NEW)
  - Current state: AdminPortal download helper exists for admin GDPR export.
  - What this story changes: add or reuse a namespaced host-side JSON download helper for Consumer export.
  - Preserve: helper revokes object URLs, never logs payload/name, and maps JS failure back to transient UI state.
- `tests/Hexalith.Parties.ConsumerPortal.Tests/Packaging/ConsumerPortalPackagingTests.cs` (UPDATE)
  - Current state: static boundary guard allows profile/edit/consent ports and forbids UI/gateway/internal dependencies.
  - What this story changes: allow the new privacy/export port and keep direct host/gateway calls forbidden.
  - Preserve: source scans exclude `bin/` and `obj/`.
- `tests/Hexalith.Parties.UI.Tests/SelfScopedPartiesClientSurfaceTests.cs` and `SelfScopedPartiesClientTests.cs` (READ/possibly UPDATE)
  - Current state: tripwire tests already include export self-scope injection.
  - What this story changes: likely no surface-test change; add adapter tests instead.
  - Preserve: reflection tests remain strict.

### Architecture Guardrails

- ConsumerPortal is an independent RCL. It must not reference `Hexalith.Parties.UI`, AdminPortal, `ISelfScopedPartiesClient`, gateway clients, `StatusKind` from UI internals, Server, Projections, Security, or Testing. Use a ConsumerPortal-owned port with a UI-host adapter. [Source: `_bmad-output/planning-artifacts/architecture.md#Project-Structure--Boundaries`]
- Consumer data access is own-data-only. The UI BFF resolves the Consumer's `party_id` and injects it; ConsumerPortal must never accept route ids, query ids, hidden form party ids, arbitrary ids, list/search, or direct `GetPartyAsync(partyId)`. [Source: `_bmad-output/planning-artifacts/architecture.md#Authentication--Security`] [Source: `src/Hexalith.Parties.UI/Services/ISelfScopedPartiesClient.cs`]
- Browser traffic talks only to the UI host. Do not add public ConsumerPortal APIs, direct browser calls to EventStore, actor-host calls, DAPR ACLs, CORS-dependent download URLs, or browser bearer-token flows. [Source: `_bmad-output/planning-artifacts/architecture.md#API--Communication-Patterns`]
- Use the existing EventStore gateway typed client path for export. `ExportPartyData` already exists and returns `PartyDataPortabilityPackage`; this story should not add commands, events, projections, validators, controllers, gateway routes, or actor-host endpoints. [Source: `src/Hexalith.Parties.Client/AdminPortal/HttpAdminPortalGdprClient.cs`] [Source: `src/Hexalith.Parties/Queries/PartyDetailProjectionQueryActor.cs`]
- The architecture's slow/large async-export path is a UX and extension requirement, but the current codebase has only the synchronous `AdminPortalExportDownload` contract. Do not fake a backend job id/status contract. Implement the Consumer UI state machine so the current synchronous path and a future readiness signal can converge on the same download-ready state. [Source: `_bmad-output/planning-artifacts/architecture.md#API--Communication-Patterns`]
- Use Central Package Management. Do not add package versions to csproj files, bump packages, or add a third-party download/state library. [Source: `_bmad-output/project-context.md#Technology-Stack--Versions`]

### Domain and Contract Guidance

- `PartyDataPortabilityPackage` fields are `PartyId`, `TenantId`, `Status`, `ExportedAt`, `ExportedBy`, `CorrelationId`, optional `Party`, `ProcessingRecords`, and optional `Freshness`. Treat ids/operator/correlation fields as backend/audit data, not user-facing copy. [Source: `src/Hexalith.Parties.Contracts/Models/PartyDataPortabilityPackage.cs`]
- Export statuses currently produced by `PartyDetailProjectionQueryActor` are string values: `Exported`, `RestrictedExported`, `Erased`, and `PersonalDataUnavailable`. UI should map them to safe copy and not assume `Party` is non-null. [Source: `src/Hexalith.Parties/Queries/PartyDetailProjectionQueryActor.cs#BuildPortabilityPackageAsync`]
- Erased parties export `Status = Erased` with no `Party` payload. Unavailable personal data exports `Status = PersonalDataUnavailable` with no partial `Party` payload. Restricted parties may still export as `RestrictedExported`. [Source: `docs/gdpr-portability-export.md`]
- Export filenames must derive from party id plus UTC timestamp only; tenant ids, display names, contact values, identifiers, and free-text reasons are forbidden in filenames. Consumer UI should not render the raw id-bearing filename as proof of identity; a generic "Download JSON" action is enough. [Source: `docs/gdpr-portability-export.md`] [Source: `src/Hexalith.Parties.Client/AdminPortal/HttpAdminPortalGdprClient.cs#BuildSafeExportFileName`]
- Logs and telemetry for export operations must use bounded metadata only and must not include exported payload. ConsumerPortal pages should not use `ILogger`, `Console`, `Debug`, `ActivitySource`, or `Meter`. [Source: `docs/gdpr-portability-export.md`] [Source: `tests/Hexalith.Parties.ConsumerPortal.Tests/Packaging/ConsumerPortalPackagingTests.cs`]
- `PartiesClientException` and `AdminPortalGdprOutcome`/query failures must map to safe user states. Do not display raw ProblemDetails, stack traces, query payloads, correlation ids, or exception messages. [Source: `src/Hexalith.Parties.Client/AdminPortal/HttpAdminPortalGdprClient.cs#OutcomeFromErrorAsync`]

### UX and Accessibility Guardrails

- Export copy must not promise "ready in a moment", "under a minute", or any fixed completion time. Required status: "Preparing your export - this can take a little while. We'll show it here the moment it's ready." [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Voice-and-Tone`]
- State the export format as machine-readable JSON so the GDPR Art. 20 portability expectation is clear. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Voice-and-Tone`]
- Failure copy must preserve trust: "Your data is safe - try again." It should offer retry/backoff and keep prior content visible. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#State-Patterns`]
- Accepted/preparing/ready/freshness states use `role="status" aria-live="polite"` with no focus steal. Transient/load/failure states use inline `role="alert"`. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Accessibility-Floor`]
- Use exactly one visible status source per export action. Do not show "Preparing", "Saved", and freshness as competing simultaneous status messages. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Voice-and-Tone`]
- Consumer pages are phone-first, single-column, roomy, and 16px body text. This is a working product surface, not a landing page. [Source: `_bmad-output/planning-artifacts/architecture.md#Frontend-Architecture`]
- Do not use raw teal `#0097A7`, raw hex/rgb/hsl colors, color-only state, spinner-only screens, native `alert()`/`confirm()`/`prompt()`, or success-green deletion/export overclaim language. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Accessibility-Floor`]

### Previous Story Intelligence

- Story 5.1 established the Epic 5 ConsumerPortal pattern: RCL-owned port/DTOs, UI-host adapter, resource-backed regulated copy, strict packaging guard, one status source, and no raw consent/channel/operator ids in DOM/copy. Mirror this for export. [Source: `_bmad-output/implementation-artifacts/5-1-my-consent-grant-withdraw-with-honest-lawful-basis-split-fr-consumer-3.md#Dev-Notes`]
- Story 5.1 also showed `Program.cs` adapter registration order matters: `AddSelfScopedPartiesClient()` before ConsumerPortal adapters. Extend the existing composition test rather than weakening it. [Source: `tests/Hexalith.Parties.UI.Tests/PartiesUiHostCompositionTests.cs#Program_RegistersConsumerPortalAdaptersAfterSelfScopedClient`]
- Story 4.5 added `IConsumerProfileEditClient`, a UI-host adapter, no-caller-id self-scoped writes, and one-status-source tests. Reuse its port/result shape style for privacy export. [Source: `_bmad-output/implementation-artifacts/4-5-edit-my-profile-fr-consumer-2.md#Completion-Notes-List`]
- Story 4.4 added the real `/me` profile rendering and `FreshnessStatus`. Reuse local RCL display conventions instead of introducing a shared UI dependency. [Source: `_bmad-output/implementation-artifacts/4-4-my-profile-fr-consumer-1.md#Previous-Story-Intelligence`]
- Story 3.4/Admin GDPR export already hardened export behavior: JSON download envelope, safe filename, no PII filenames, empty-payload failure, JS failure handling, and bounded metadata. Apply those lessons to Consumer export without importing AdminPortal internals. [Source: `src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor#ExportAsync`] [Source: `tests/Hexalith.Parties.Client.Tests/AdminPortal/AdminPortalGdprOperationContractTests.cs#ExportPartyDataAsync_UsesAuthoritativeExportQueryAndSafeDownloadNameAsync`]
- Recent commits are sequential and scoped: Story 4.3 ConsumerPortal stand-up, 4.4 read-only profile, 4.5 edit profile, and 5.1 consent. Keep Story 5.2 focused on export only. [Source: `git log -5`]

### Testing and Validation Guidance

- Use xUnit v3, bUnit, Shouldly, and NSubstitute. Do not introduce Moq, FluentAssertions, raw `Assert.*`, Verify snapshots unless needed, package versions in csproj files, or new third-party libraries. [Source: `_bmad-output/project-context.md#Testing-Rules`]
- Required ConsumerPortal component tests:
  - `/me/privacy` renders the real page, not `ConsumerRouteShell`.
  - Cold/preparing state uses one polite status region and no spinner-only UI.
  - Export action invokes the ConsumerPortal-owned privacy/export port, not any gateway/self-scoped/internal type.
  - Ready state presents an in-app JSON download action and no raw filename/id/payload preview.
  - Empty payload, JS failure, transient query failure, and load failure show inline `role="alert"` with "Your data is safe - try again." and expose no raw details.
  - Erased/unavailable/restricted/degraded statuses map to safe copy and never assume `Party` is present.
  - No text contains hard timing promises such as "within 30 days", "under one minute", or "ready in a moment".
- Required boundary/static tests:
  - ConsumerPortal has no reference to `Hexalith.Parties.UI` or AdminPortal.
  - ConsumerPortal contains no `IPartiesQueryClient`, `IPartiesCommandClient`, `IAdminPortalGdprClient`, `ISelfScopedPartiesClient`, direct `GetPartyAsync(`, `ListPartiesAsync`, `SearchPartiesAsync`, public endpoint code, or methods/DTOs with party-id shaped properties.
  - UI host registers the privacy/export adapter as Scoped and delegates only to `ISelfScopedPartiesClient.ExportMyDataAsync`.
  - Self-scope surface tripwires remain green.
  - Source scans exclude `bin/` and `obj/`.
- Required E2E updates:
  - `/me/privacy` route is reachable for bound Consumers and not shown to Admin-only/unauthenticated users.
  - Bound Consumer export request reaches the server-side fixture/self-scoped path for `party-bound-001`; the browser sees no `/api/v1/commands` or `/api/v1/queries` calls.
  - Export copy states machine-readable JSON and contains no forbidden timing promise.
  - Download-ready behavior can be asserted through the JS helper invocation if the fixture supports it; otherwise typecheck/spec discovery must still cover selectors and route behavior.

### Latest Technical Information

- Microsoft Learn's current Blazor file-download guidance says small files, typically under 250 MB, can be downloaded by streaming to the client with JS interop, while large files should use a URL-based approach. Story 5.2 should use the stream/JS interop pattern for the current in-memory JSON bytes and avoid adding a public URL endpoint. [Source: `https://learn.microsoft.com/en-us/aspnet/core/blazor/file-downloads?view=aspnetcore-10.0`]
- The same guidance warns that the stream approach reads content into a JS `ArrayBuffer`, so the UI must not use it for arbitrarily large future exports; the architecture's future slow/large async path should converge on a URL/download-ready contract only when an authoritative backend contract exists. [Source: `https://learn.microsoft.com/en-us/aspnet/core/blazor/file-downloads?view=aspnetcore-10.0`]
- Microsoft Learn's current file-download example revokes the object URL after triggering the anchor click. The Consumer helper must do the same to avoid client memory leaks. [Source: `https://learn.microsoft.com/en-us/aspnet/core/blazor/file-downloads?view=aspnetcore-10.0`]
- No package upgrade is required. Use the pinned local stack: .NET SDK `10.0.300`, FluentUI Blazor `5.0.0-rc.3`, xUnit v3, bUnit, Shouldly, and NSubstitute. [Source: `_bmad-output/project-context.md#Technology-Stack--Versions`]

### Project Structure Notes

- Likely new files:
  - `src/Hexalith.Parties.ConsumerPortal/Services/IConsumerPrivacyExportClient.cs`
  - `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerPrivacyExportResult.cs`
  - `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerPrivacyExportOutcome.cs`
  - `src/Hexalith.Parties.UI/Services/ConsumerPrivacyExportClient.cs`
  - `tests/Hexalith.Parties.ConsumerPortal.Tests/Components/MyPrivacyPageTests.cs`
  - `tests/Hexalith.Parties.UI.Tests/ConsumerPrivacyExportClientTests.cs`
- Likely updated files:
  - `src/Hexalith.Parties.ConsumerPortal/Components/MyPrivacyPage.razor`
  - `src/Hexalith.Parties.ConsumerPortal/Components/MyPrivacyPage.razor.css` if styling is needed
  - `src/Hexalith.Parties.ConsumerPortal/Resources/ConsumerPortalResources.resx`
  - `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerPortalLabels.cs`
  - `src/Hexalith.Parties.UI/Program.cs`
  - UI host JS asset/helper location used by existing AdminPortal download wiring
  - `tests/Hexalith.Parties.ConsumerPortal.Tests/Components/ConsumerPortalPageTests.cs`
  - `tests/Hexalith.Parties.ConsumerPortal.Tests/Packaging/ConsumerPortalPackagingTests.cs`
  - `tests/Hexalith.Parties.UI.Tests/PartiesUiHostCompositionTests.cs`
  - `tests/Hexalith.Parties.UI.Tests/SelfScopedPartiesClientTests.cs` only if additional export edge cases are needed
  - `tests/e2e/specs/consumer-portal-routes.spec.ts`
- Keep generated/build artifacts out of the repo. Do not edit or commit `bin/`, `obj/`, or `obj/**/generated/**`.

### Out of Scope

- No erasure request/cancel UX; Story 5.3 owns it.
- No processing-records summary UX or "Manage all consent" privacy-card deep link; Story 5.4 owns it.
- No consent toggle, consent catalogue, or lawful-basis behavior changes; Story 5.1 owns them.
- No identity binding redesign, self-registration, IdP federation, binding-store persistence, or Keycloak mapper changes.
- No Parties aggregate/event/projection/actor/validator changes.
- No EventStore, Tenants, FrontComposer, Memories, DAPR ACL, deployment, or production KMS changes.
- No public UI API endpoint, direct EventStore browser call, actor-host endpoint, browser token flow, webroot export file store, or new app host sidecar.
- No package upgrades and no new third-party libraries.
- No broad shared UI package refactor unless a build-breaking dependency issue proves it is strictly required.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-5.2-My-data--privacy-export-my-data-FR-Consumer-4`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#API--Communication-Patterns`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Frontend-Architecture`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#Project-Structure--Boundaries`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md`]
- [Source: `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/review-regulated-language.md`]
- [Source: `_bmad-output/project-context.md`]
- [Source: `docs/gdpr-portability-export.md`]
- [Source: `src/Hexalith.Parties.ConsumerPortal/Components/MyPrivacyPage.razor`]
- [Source: `src/Hexalith.Parties.UI/Services/ISelfScopedPartiesClient.cs`]
- [Source: `src/Hexalith.Parties.UI/Services/SelfScopedPartiesClient.cs`]
- [Source: `src/Hexalith.Parties.Client/AdminPortal/HttpAdminPortalGdprClient.cs`]
- [Source: `src/Hexalith.Parties.Contracts/Models/PartyDataPortabilityPackage.cs`]
- [Source: `src/Hexalith.Parties/Queries/PartyDetailProjectionQueryActor.cs`]
- [Source: `src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor`]
- [Source: `src/Hexalith.Parties.AdminPortal/Services/GdprExportFileNameBuilder.cs`]
- [Source: `_bmad-output/implementation-artifacts/5-1-my-consent-grant-withdraw-with-honest-lawful-basis-split-fr-consumer-3.md`]
- [Source: `https://learn.microsoft.com/en-us/aspnet/core/blazor/file-downloads?view=aspnetcore-10.0`]

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-06-10: Preserved existing `baseline_commit: e796138`; marked story and sprint status in-progress before implementation, then review after focused validation.
- 2026-06-10: `dotnet build src/Hexalith.Parties.ConsumerPortal/Hexalith.Parties.ConsumerPortal.csproj -c Release --no-restore -m:1 -v:minimal` passed with 0 warnings/errors.
- 2026-06-10: `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release --no-restore -m:1 -v:minimal` passed with 0 warnings/errors.
- 2026-06-10: `dotnet build tests/Hexalith.Parties.ConsumerPortal.Tests/Hexalith.Parties.ConsumerPortal.Tests.csproj -c Release --no-restore -m:1 -v:minimal` passed with 0 warnings/errors.
- 2026-06-10: `dotnet build tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -c Release --no-restore -m:1 -v:minimal` passed with 0 warnings/errors.
- 2026-06-10: Compiled xUnit executable `tests/Hexalith.Parties.ConsumerPortal.Tests/bin/Release/net10.0/Hexalith.Parties.ConsumerPortal.Tests` passed: 66 total, 0 failed.
- 2026-06-10: Compiled xUnit executable `tests/Hexalith.Parties.UI.Tests/bin/Release/net10.0/Hexalith.Parties.UI.Tests` passed: 304 total, 0 failed.
- 2026-06-10: `npm run typecheck` in `tests/e2e` passed.
- 2026-06-10: `npx playwright test specs/consumer-portal-routes.spec.ts --list` discovered 15 Chromium tests, including the new `/me/privacy` export flow.
- 2026-06-10: Browser runtime for `npx playwright test specs/consumer-portal-routes.spec.ts --project=chromium` was blocked by sandbox socket permissions: Kestrel failed to bind `127.0.0.1:5072` with `System.Net.Sockets.SocketException (13): Permission denied`.
- 2026-06-10: `git diff --check` passed.
- 2026-06-10: `pwsh scripts/test.ps1 -Lane unit` returned exit code 0 but each `dotnet test --project ...` invocation printed `Build failed with exit code: 1` immediately after restore discovery; direct `dotnet test <project>` showed the same opaque failure before test execution.
- 2026-06-10: Additional `dotnet build Hexalith.Parties.slnx -c Release --no-restore -m:1 -v:minimal` failed in unrelated `Hexalith.PolymorphicSerializations` package projects with NuGet pack warnings-as-errors `NU5118` and `NU5128`; Story 5.2 scope forbids submodule/package edits.
- 2026-06-10: Senior review found and fixed an e2e fixture payload defect: the Consumer export fixture returned JSON missing required portability package fields, so the UI adapter would map the e2e export path to transient failure instead of ready.
- 2026-06-10: Senior review hardened `consumer-privacy-export.js` so object URLs are revoked in a `finally` block after the synthetic download click path.
- 2026-06-10: Senior review validation passed: `dotnet build tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -c Release --no-restore -m:1 -v:minimal`; compiled UI xUnit executable passed 306 total, 0 failed.
- 2026-06-10: Senior review validation passed: `dotnet build tests/Hexalith.Parties.ConsumerPortal.Tests/Hexalith.Parties.ConsumerPortal.Tests.csproj -c Release --no-restore -m:1 -v:minimal`; compiled ConsumerPortal xUnit executable passed 67 total, 0 failed.
- 2026-06-10: Senior review validation passed: `npm run typecheck` in `tests/e2e`; `npx playwright test specs/consumer-portal-routes.spec.ts --list` discovered 16 Chromium tests; `git diff --check` passed.
- 2026-06-10: Senior review attempted `npx playwright test specs/consumer-portal-routes.spec.ts --project=chromium`; browser runtime remained blocked by sandbox socket permissions when Kestrel tried to bind (`System.Net.Sockets.SocketException (13): Permission denied`).

### Completion Notes List

- Replaced the `/me/privacy` placeholder with a Consumer export surface that keeps the `/me/privacy` route and Consumer policy, uses the approved preparing copy, exposes machine-readable JSON format copy, and keeps erasure controls as a non-operational preview.
- Added a ConsumerPortal-owned `IConsumerPrivacyExportClient` port and safe result/status DTOs with no caller-supplied identity, list/search, gateway, or UI-host surface.
- Added a UI-host scoped `ConsumerPrivacyExportClient` adapter that delegates to `ISelfScopedPartiesClient.ExportMyDataAsync`, maps package statuses to safe Consumer outcomes, rejects empty payloads, and uses a generic non-PII consumer-facing filename.
- Added host-level `HexalithPartiesConsumerPortal.downloadJson` JS interop using `DotNetStreamReference`, object URL revocation, and component-side `JSException`/`JSDisconnectedException` transient failure handling.
- Added focused bUnit/static/UI adapter/e2e coverage for placeholder removal, preparing/ready/retry states, empty payload, JS failure, terminal statuses, boundary guards, adapter registration/delegation, and no browser-visible EventStore calls.
- Senior review fixed the e2e export fixture contract so it returns a complete portability package and pinned helper cleanup behavior for the download object URL.

### File List

- `_bmad-output/implementation-artifacts/5-2-my-data-privacy-export-my-data-fr-consumer-4.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Parties.ConsumerPortal/Components/MyPrivacyPage.razor`
- `src/Hexalith.Parties.ConsumerPortal/Components/MyPrivacyPage.razor.css`
- `src/Hexalith.Parties.ConsumerPortal/Resources/ConsumerPortalResources.resx`
- `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerPortalLabels.cs`
- `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerPrivacyExportOutcome.cs`
- `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerPrivacyExportResult.cs`
- `src/Hexalith.Parties.ConsumerPortal/Services/ConsumerPrivacyExportStatus.cs`
- `src/Hexalith.Parties.ConsumerPortal/Services/IConsumerPrivacyExportClient.cs`
- `src/Hexalith.Parties.UI/Components/App.razor`
- `src/Hexalith.Parties.UI/Program.cs`
- `src/Hexalith.Parties.UI/Services/ConsumerPrivacyExportClient.cs`
- `src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs`
- `src/Hexalith.Parties.UI/wwwroot/consumer-privacy-export.js`
- `tests/Hexalith.Parties.ConsumerPortal.Tests/Components/ConsumerPortalPageTests.cs`
- `tests/Hexalith.Parties.ConsumerPortal.Tests/Components/MyPrivacyPageTests.cs`
- `tests/Hexalith.Parties.ConsumerPortal.Tests/Packaging/ConsumerPortalPackagingTests.cs`
- `tests/Hexalith.Parties.UI.Tests/ConsumerPrivacyExportClientTests.cs`
- `tests/Hexalith.Parties.UI.Tests/PartiesAdminPortalE2eFixtureTests.cs`
- `tests/Hexalith.Parties.UI.Tests/PartiesUiHostCompositionTests.cs`
- `tests/e2e/specs/consumer-portal-routes.spec.ts`

### Senior Developer Review (AI)

- Outcome: Approved after automatic fixes.
- Fixed HIGH: The e2e export fixture emitted an incomplete `PartyDataPortabilityPackage` JSON payload, causing the UI adapter to treat the bound Consumer export path as `TransientFailure` instead of ready during real browser execution.
- Fixed MEDIUM: The host download helper revoked the object URL only after a successful click path; it now removes the temporary anchor and revokes the object URL in `finally`.
- Remaining critical issues: none.

### Change Log

- 2026-06-10: Implemented Story 5.2 Consumer own-data JSON export path for `/me/privacy`, including RCL port, UI adapter, host JS download helper, focused tests, e2e spec updates, and BMAD status tracking.
- 2026-06-10: Senior review fixed the Consumer export e2e fixture payload, hardened download-helper cleanup, added regression guards, and marked Story 5.2 done.
