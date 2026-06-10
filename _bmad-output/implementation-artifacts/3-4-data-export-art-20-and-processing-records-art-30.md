---
baseline_commit: 04852f3
---

# Story 3.4: Data export (Art.20) and processing records (Art.30)

Status: done

<!-- Note: Validation completed during create-story. Run dev-story for implementation. -->

## Story

As a DPO,
I want to export a party's data and view its processing records,
so that I can fulfill portability and accountability obligations.

## Acceptance Criteria

1. Given an authorized DPO/admin is on `/admin/parties/{id}/gdpr`, when I choose Export party data, then the existing `PartyGdprOperationsPanel` calls `IPartiesAdminPortalApiClient.ExportPartyDataAsync(partyId, token)` through the AdminPortal API boundary, downloads a machine-readable JSON package, and does not add a new route, controller, direct gateway call, or direct `IAdminPortalGdprClient` injection from Razor.
2. Given a portability package is downloaded, when the UI chooses the download name, then the displayed and JS-supplied filename is re-derived from party id plus UTC timestamp only; tenant id, display name, contact value, identifier, free-text reason, raw server filename, and payload content never appear in the filename, visible copy, logs, fixture snapshots, or browser URL.
3. Given the server returns a package with status `Exported`, `RestrictedExported`, `Erased`, or `PersonalDataUnavailable`, when the export completes, then the UI reports a bounded completed status with `role="status" aria-live="polite"`, preserves previous content, does not show success-green celebration, and does not echo package payload fields into the page.
4. Given the selected party is restricted, when export is requested, then export remains available for an authorized DPO and the JSON package status remains `RestrictedExported`; restriction copy stays bounded and no restriction reason text is displayed as part of export.
5. Given the selected party is erased, gone, or only reachable by the GDPR route party id, when the GDPR surface renders, then mutable operations remain disabled or absent, but portability export and processing-record retrieval remain available when the GDPR capability permits them; the export package status is `Erased` with no `Party` payload and the UI never reintroduces erased personal fields.
6. Given personal data is unavailable in the projection at export time, when `ExportPartyDataAsync` returns a package with status `PersonalDataUnavailable`, then the JSON download is still delivered if the payload is non-empty, no partial `Party` payload is displayed, and the operation result remains bounded and retryable on transport/download failure.
7. Given the processing-records view is refreshed, when `GetProcessingRecordsAsync` returns records, then the panel displays only the bounded Art.30 metadata fields: `partyId`, `tenantId`, `sequenceNumber`, `eventType`, `operationCategory`, `timestamp`, `actorId`, `correlationId`, `outcome`, and `summary`.
8. Given processing records contain raw-looking payload text, names, identifiers, contact values, restriction reasons, parser details, destroyed-key text, tokens, claims, or long/free-text summaries, when they render, then Blazor encoding is preserved, summaries are bounded/truncated, and no prohibited value is echoed outside the stable bounded fields.
9. Given export or processing-record loading is rejected, forbidden, unauthenticated, tenant-unavailable, not found/gone, contract-unavailable, transiently failed, empty-payload, JS-download failed, or unknown, when the outcome is mapped, then the existing `AdminPortalGdprOutcome`/query-failure mapping and politeness split are preserved: completed/degraded states are polite, validation/transient/load/hard failures use `role="alert"`/assertive, and stale success correlations or filenames are cleared.
10. Given the party, tenant, capability, erasure status, or route context changes while an export or records request is in flight, when the stale result returns, then it is ignored and cannot update the current party's filename, processing records, operation message, or JS download.
11. Given keyboard, screen-reader, forced-colors, reduced-motion, 320px width, or 200% zoom usage, when export and processing-record flows are used, then controls remain reachable, labels/statuses are localized through `AdminPortalLabels`, state is never color-only, and no native `alert`/`confirm`/`prompt` is used.
12. Given Story 3.5 has not implemented the D7 EventStore erasure-verification contract, when Story 3.4 is implemented, then `GetErasureCertificateAsync` and `RetryErasureVerificationAsync` remain contract-unavailable stubs and the erasure-verification report behavior is not changed.

## Tasks / Subtasks

- [x] Preserve the existing GDPR operation surface and API boundary (AC: 1, 9, 10, 12)
  - [x] Keep `PartyGdprOperationsPanel` as the owner for export and processing-record actions; do not create a duplicate GDPR page or child flow that bypasses the panel.
  - [x] Keep calls behind `IPartiesAdminPortalApiClient.ExportPartyDataAsync` and `GetProcessingRecordsAsync`; Razor must not inject `IAdminPortalGdprClient`, `IPartiesQueryClient`, `HttpClient`, EventStore gateway clients, or lower-level query services for this story.
  - [x] Preserve `_isBusy`, `TenantContextSignature`, party id, cancellation token, `_capabilityVersion`, `RunQueryAsync`, and `IsCurrentOperation` stale-result guards for both export and processing records.
  - [x] Keep D7 certificate/retry-verification out of scope; do not change `AdminPortalGdprCapability.ProvisionalBridge()` semantics except where export/records capability handling explicitly requires it.

- [x] Harden Art.20 export behavior (AC: 1-6, 9, 10, 11)
  - [x] Keep the download helper call `HexalithPartiesAdminPortal.downloadJson` server-side through Blazor JS interop; no browser-visible `/api/v1/queries` call may be introduced.
  - [x] Continue to ignore `AdminPortalExportDownload.FileName` and re-derive the download filename with `GdprExportFileNameBuilder` from party id and UTC timestamp only; verify the JS invocation receives this safe filename and `application/json`.
  - [x] Treat non-empty JSON payloads for `Exported`, `RestrictedExported`, `Erased`, and `PersonalDataUnavailable` as downloadable results; do not require a `Party` payload for erased or unavailable-data exports.
  - [x] Allow export for restricted parties and for erased/gone GDPR-route contexts when capability permits export; only destructive/reversible mutations should stay blocked for erased/pending-erasure parties.
  - [x] If the selected party becomes erased and the normal detail surface is replaced by a tombstone, keep enough bounded context from the route party id to offer export and processing records without displaying personal fields.
  - [x] On empty payload, JS disconnect, missing JS helper, transient transport failure, malformed response, or contract-unavailable result, surface the existing bounded failure outcome and clear `_exportFileName`.

- [x] Expand Art.30 processing-record rendering (AC: 7, 8, 9, 10, 11)
  - [x] Extend `ProcessingRecordsPanel` to render the complete bounded audit schema: party id, tenant id, sequence number, event type, operation category, timestamp, actor id, correlation id, outcome, and summary.
  - [x] Keep records ordered by `SequenceNumber`; preserve current Blazor encoding and summary truncation, or tighten it if needed to keep long/free-text summaries bounded.
  - [x] Add `AdminPortalLabels` entries for every visible field label rather than inline strings; preserve host label override support.
  - [x] Ensure the empty state remains localized and non-alarming, and that stale/error results do not clear previously valid records unless the party/tenant/capability context changes.
  - [x] Do not add raw payload, command/query JSON, consent ids, channel ids beyond the bounded record field if supplied by the backend, contact values, identifiers, names, restriction reasons, parser/destroyed-key text, token, or claims display.

- [x] Support erased/gone GDPR route behavior without PII leakage (AC: 5, 7, 8, 10, 11)
  - [x] Review `PartiesAdminPortal.razor` erased-detail and `Gone` handling. Today it clears `_detail`, removes the row, and resets GDPR state before the operations panel can offer export/records; adjust only as needed to render a bounded GDPR terminal surface for direct `/admin/parties/{id}/gdpr`.
  - [x] If a minimal `PartyDetail` or a panel parameter is introduced for terminal GDPR operations, populate only durable non-PII route id, lifecycle flags, and capability context; do not preserve display name, sort name, contact channels, identifiers, person details, organization details, or name history.
  - [x] Keep request erasure, restrict/lift, add/revoke consent, and retry verification disabled or absent for erased/pending-erasure contexts unless an existing capability contract says otherwise.
  - [x] Ensure processing records remain retrievable for erased parties because they are audit metadata and do not require decrypted personal data.

- [x] Extend component and service tests at existing seams (AC: 1-12)
  - [x] Update `PartiesAdminPortal_PortabilityExport_UsesSafeDownloadEnvelopeCue` or add focused bUnit tests for `Exported`, `RestrictedExported`, `Erased`, and `PersonalDataUnavailable` packages with non-empty payloads, safe filename derivation, no server filename leakage, no payload echo, and polite completion.
  - [x] Add bUnit coverage that an erased/gone direct GDPR route can retrieve export and processing records while mutation controls stay disabled/absent and personal fields do not render.
  - [x] Add bUnit tests for empty export payload, JS helper failure, contract unavailable, transient failure, malformed response, and stale party/tenant/capability result cleanup.
  - [x] Update `PartiesAdminPortal_ProcessingRecords_RenderEncodedAuditSummaries` so it asserts all bounded fields render, records sort by sequence number, summaries are encoded/truncated, and prohibited PII-like values do not appear.
  - [x] Extend `RecordingAdminPortalApiClient` only with bounded export/package/records helpers needed by tests; do not capture payloads, raw problem details, display names, tenant-scoped secrets, or free-text reasons.
  - [x] Add/adjust `PartiesAdminPortalApiClientTests` or `HttpAdminPortalGdprClient` tests if client-side mapping of `ExportPartyData`/`GetProcessingRecords` changes.

- [x] Extend browser-level evidence where existing E2E coverage already exercises the Admin GDPR route (AC: 1, 2, 7, 8, 11)
  - [x] Update `PartiesAdminPortalE2eFixture` so export and processing-record fixtures contain bounded Art.20/Art.30 data without PII-like values.
  - [x] Extend `tests/e2e/specs/admin-parties-list.spec.ts` for direct GDPR route export and records: one fixture request, no browser-visible EventStore `/api/v1/queries`, no native dialogs, safe filename/status visible, bounded processing fields visible, and no 320px/200% zoom overflow.
  - [x] Keep Playwright runtime optional if local Kestrel/socket binding is blocked; typecheck and spec discovery still provide useful evidence.

- [x] Validate with focused lanes and record limitations (AC: 1-12)
  - [x] Run `dotnet build tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj -c Release --no-restore -m:1 -v:minimal`.
  - [x] Run the compiled xUnit v3 AdminPortal test assembly directly if `dotnet test`/MTP is blocked by local IPC/socket limits.
  - [x] Run `npm run typecheck` and `npx playwright test --list` in `tests/e2e`; run the focused Playwright spec if local browser/server socket binding permits it.
  - [x] Run `bash scripts/check-no-warning-override.sh`.

## Dev Notes

### Current Implementation State

- `PartyGdprOperationsPanel.razor` already renders `PortabilityExportPanel` and `ProcessingRecordsPanel`, probes `AdminPortalGdprCapability`, runs export/records through `RunQueryAsync`, and uses party/tenant/capability/cancellation stale-result guards. Build on this panel instead of introducing a new GDPR operation surface. [Source: src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor]
- Current export calls `QueryService.ApiClient.ExportPartyDataAsync(partyId, token)`, ignores the server-supplied filename, re-derives a safe filename through `GdprExportFileNameBuilder.Build(partyId, DateTimeOffset.UtcNow)`, base64-encodes the payload, and invokes `HexalithPartiesAdminPortal.downloadJson`. Preserve that defense-in-depth and add missing status/edge coverage. [Source: src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor] [Source: src/Hexalith.Parties.AdminPortal/Services/GdprExportFileNameBuilder.cs]
- Current `CanExport` blocks `Party.IsErased` and `IsErasurePending`. Story 3.4 requires an erased export to produce status `Erased` with no `Party` payload, so terminal erased/gone GDPR route handling needs a bounded export/records path while mutation controls remain blocked. [Source: src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor#CanExport]
- `PartiesAdminPortal.razor` currently treats a 200 payload with `IsErased` like `Gone`: it clears `_detail`, removes the row, sets `DetailErased`, and resets GDPR state. That prevents the existing operations panel from offering export/processing records for erased parties; adjust carefully without restoring personal fields. [Source: src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor]
- `ProcessingRecordsPanel.razor` currently renders only event type, changed-at timestamp, and bounded summary. Story 3.4 must render the complete bounded Art.30 schema, with labels routed through `AdminPortalLabels`. [Source: src/Hexalith.Parties.AdminPortal/Components/ProcessingRecordsPanel.razor]
- `PortabilityExportPanel.razor` currently has a single outline export button, capability disabled reason, and a polite prepared-file cue. Keep the simple surface; avoid adding payload previews or package field dumps. [Source: src/Hexalith.Parties.AdminPortal/Components/PortabilityExportPanel.razor]
- `IPartiesAdminPortalApiClient` is the AdminPortal component boundary, and `PartiesAdminPortalApiClient` delegates export/records to `IAdminPortalGdprClient` through `ExecuteGdprQueryAsync`. Razor should stay on this boundary. [Source: src/Hexalith.Parties.AdminPortal/Services/IPartiesAdminPortalApiClient.cs] [Source: src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs]
- `HttpAdminPortalGdprClient.ExportPartyDataAsync` posts the `ExportPartyData` query to the EventStore gateway and serializes `PartyDataPortabilityPackage` to JSON. `GetProcessingRecordsAsync` posts `GetProcessingRecords` and returns `ProcessingActivityRecord[]`. Do not add public actor-host endpoints. [Source: src/Hexalith.Parties.Client/AdminPortal/HttpAdminPortalGdprClient.cs]

### Backend Contract Requirements

- `PartyDataPortabilityPackage` contains `PartyId`, `TenantId`, `Status`, `ExportedAt`, `ExportedBy`, `CorrelationId`, optional `Party`, `ProcessingRecords`, and `Freshness`. The UI should download the JSON package and should not render its payload into the page. [Source: src/Hexalith.Parties.Contracts/Models/PartyDataPortabilityPackage.cs]
- `PartyDetailProjectionQueryActor.BuildPortabilityPackageAsync` already computes status values: `Erased` when `detail.IsErased`, `PersonalDataUnavailable` when display/sort data is unavailable, `RestrictedExported` when the party is restricted, otherwise `Exported`. For `Erased` and `PersonalDataUnavailable`, `Party` is `null`. [Source: src/Hexalith.Parties/Queries/PartyDetailProjectionQueryActor.cs]
- `ProcessingActivityRecord` is the bounded Art.30 model: sequence number, party id, tenant id, actor id, correlation id, operation category, outcome, event type, timestamp, and summary. Do not extend this UI story to add payload fields. [Source: src/Hexalith.Parties.Contracts/Models/ProcessingActivityRecord.cs]
- `ProjectionRebuildService.GetProcessingRecordsAsync` derives records from persisted party events and creates stable summaries such as "Consent recorded.", "Processing restricted.", and "Party erased."; its tests assert no payload text, names, email-like contact values, or free-text reasons leak into serialized records. [Source: src/Hexalith.Parties.Projections/Services/ProjectionRebuildService.cs] [Source: tests/Hexalith.Parties.Tests/Projections/ProjectionRebuildServiceTests.cs]
- GDPR docs require restricted parties to remain exportable for an authorized administrator/DPO with status `RestrictedExported`; erased parties return status `Erased` with no `Party` payload; unavailable personal data returns `PersonalDataUnavailable` with no partial payload; filenames use party id + UTC timestamp only. [Source: docs/gdpr-portability-export.md]
- Processing-record docs require only bounded audit metadata and stable summaries. Raw command/query payloads, exported package content, tokens, claims dictionaries, contact values, identifiers, names, and restriction reason text are excluded; erased parties keep records. [Source: docs/gdpr-processing-activity-records.md]

### Architecture and Scope Guardrails

- Epic 3 wraps existing AdminPortal GDPR panels and uses `IAdminPortalGdprClient`. Story 3.4 is export and processing records only; erasure verification remains Story 3.5/3.6. [Source: _bmad-output/planning-artifacts/epics.md#Story-3.4-Data-export-Art20-and-processing-records-Art30]
- The browser talks only to the UI host/BFF; the UI host talks to the EventStore gateway through typed clients. Do not add public actor-host APIs or browser-visible EventStore gateway calls. [Source: _bmad-output/planning-artifacts/architecture.md#Architectural-Boundaries]
- Public command/query traffic enters EventStore through `POST /api/v1/commands` and `POST /api/v1/queries` with `Domain="party"`; the `parties` actor host has no public command/query API. [Source: docs/api-contracts.md#The-boundary-in-one-picture]
- `GetErasureCertificateAsync` and `RetryErasureVerificationAsync` intentionally remain non-functional pending the D7 EventStore contract. Story 3.4 must not "fix" them. [Source: docs/api-contracts.md#IAdminPortalGdprClient]

### UX and Accessibility Requirements

- GDPR operations are part of the Admin detail-to-GDPR route: erasure status, processing records, consent, portability, and verification. Story 3.4 covers the portability and processing-record portions. [Source: _bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Information-Architecture]
- Routine completed/accepted work uses `role="status" aria-live="polite"`; validation, transient, load, and hard failures use `role="alert"`/assertive. Do not move focus for routine export completion. [Source: _bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Accessibility-Floor]
- Export copy must not promise "ready in a moment" or "under a minute"; the package is machine-readable JSON, and transient failures should keep prior content visible with retry. [Source: _bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Voice-and-Tone]
- Admin copy is terse/operator-focused. Every new visible label must flow through `AdminPortalLabels` or an existing label override seam, not inline strings. [Source: _bmad-output/planning-artifacts/architecture.md#Process-Patterns]

### Privacy and Security Requirements

- Never log or display event payloads, raw problem details, parser exceptions, destroyed-key text, contact values, identifiers, tenant-scoped secrets, free-text restriction reasons, consent revocation reasons, or command payloads. Projection and operation logs must stay bounded. [Source: _bmad-output/project-context.md#Critical-Dont-Miss-Rules-anti-patterns--gotchas]
- Tenant access fails closed and is eventually consistent. Missing tenant, forbidden, unauthenticated, gone, contract-unavailable, and transient states must not expose party data or enable mutation controls. [Source: _bmad-output/project-context.md#Critical-Dont-Miss-Rules-anti-patterns--gotchas]
- Contracts evolve additively only. Do not rename/remove `PartyDataPortabilityPackage` or `ProcessingActivityRecord` fields to make the UI easier. [Source: _bmad-output/project-context.md#Critical-Dont-Miss-Rules-anti-patterns--gotchas]

### Previous Story Intelligence

- Story 3.3 kept restriction/consent commands on `PartyGdprOperationsPanel`, preserved the shared `RunCommandAsync` outcome mapping, cleared stale correlations on assertive failures, reset confirmation state on party/tenant/capability/erasure-context switches, and bounded consent-ledger display. Reuse those stale-state and bounded-display patterns. [Source: _bmad-output/implementation-artifacts/3-3-restrict-lift-restriction-and-record-revoke-consent.md#Senior-Developer-Review-AI]
- Story 3.2 established typed-name erasure confirmation, pending-erasure guards, and the evidence split where `dotnet test`/Playwright runtime may be blocked by local socket permissions; direct xUnit executable plus Playwright typecheck/spec discovery are acceptable local evidence when runtime binding is blocked. [Source: _bmad-output/implementation-artifacts/3-2-erase-a-party-with-typed-name-confirmation.md#Review-Validation]
- Story 3.1 established that direct GDPR routes must reuse `PartyGdprOperationsPanel`, keep operation live regions initially empty, clear stale correlations on assertive failures, and render partial/missing states with bounded no-PII content. Do not regress these behaviors. [Source: _bmad-output/implementation-artifacts/3-1-gdpr-operations-page.md#Senior-Developer-Review-AI]
- Recent commits for Stories 3.1-3.3 touched `PartyGdprOperationsPanel.razor`, `PartiesAdminPortal.razor`, `AdminPortalLabels.cs`, `RecordingAdminPortalApiClient.cs`, `PartiesAdminPortalComponentTests.cs`, `PartiesAdminPortalE2eFixture.cs`, and `admin-parties-list.spec.ts`. Continue this focused seam rather than broad AdminPortal restructuring. [Source: git log -5]

### Files Likely to Change

- `src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor` - export/records availability, erased/gone terminal context, stale-operation cleanup, export filename/result handling, and query outcome mapping.
- `src/Hexalith.Parties.AdminPortal/Components/PortabilityExportPanel.razor` - only for bounded status/label/accessibility refinements; do not add payload previews.
- `src/Hexalith.Parties.AdminPortal/Components/ProcessingRecordsPanel.razor` - render complete bounded Art.30 fields with localized labels, ordering, encoding, and truncation.
- `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor` - direct GDPR route behavior for erased/gone parties so export/records remain possible without personal fields.
- `src/Hexalith.Parties.AdminPortal/Services/AdminPortalLabels.cs` - new labels for bounded processing-record fields and any export status copy.
- `src/Hexalith.Parties.AdminPortal/Services/GdprExportFileNameBuilder.cs` - only if filename format needs tightening; preserve party-id + UTC timestamp only.
- `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs` - primary bUnit coverage for export statuses, erased route, bounded records display, stale-result cleanup, and failure mapping.
- `tests/Hexalith.Parties.AdminPortal.Tests/Services/RecordingAdminPortalApiClient.cs` - bounded fixture helpers only.
- `tests/Hexalith.Parties.AdminPortal.Tests/Services/PartiesAdminPortalApiClientTests.cs` - only if service/client mapping changes.
- `src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs` and `tests/e2e/specs/admin-parties-list.spec.ts` - browser-level fixture/spec coverage for export and processing records.

### Testing Standards

- Use xUnit v3, Shouldly, NSubstitute, and bUnit. Do not introduce Moq, FluentAssertions, or raw `Assert.*`. [Source: _bmad-output/project-context.md#Testing-Rules]
- Use Central Package Management; do not add `Version=` attributes to project files or bump packages for this story. [Source: _bmad-output/project-context.md#Technology-Stack--Versions]
- Preferred focused verification is the AdminPortal test project/build, direct xUnit v3 executable when MTP is blocked, E2E typecheck/spec discovery, focused Playwright runtime where available, and `scripts/check-no-warning-override.sh`. [Source: _bmad-output/implementation-artifacts/3-3-restrict-lift-restriction-and-record-revoke-consent.md#Testing-Standards]

### Project Structure Notes

- Keep work inside the existing AdminPortal RCL, typed client boundary, UI fixture, and tests/e2e seams.
- Keep generated output under `obj/**/generated` untouched and uncommitted.
- No new NuGet package is expected.
- Preserve CRLF/final-newline/editorconfig conventions when editing C#, Razor, CSS, and TypeScript.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-3.4-Data-export-Art20-and-processing-records-Art30]
- [Source: _bmad-output/planning-artifacts/architecture.md#GDPR-backend-behaviors-from-docsgdpr-md-constrain-AdminConsumer-GDPR-stories]
- [Source: _bmad-output/planning-artifacts/architecture.md#Project-Structure--Boundaries]
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Component-Patterns]
- [Source: docs/gdpr-portability-export.md]
- [Source: docs/gdpr-processing-activity-records.md]
- [Source: docs/api-contracts.md#IAdminPortalGdprClient]
- [Source: docs/data-models.md#Supporting-model-types]
- [Source: src/Hexalith.Parties/Queries/PartyDetailProjectionQueryActor.cs]
- [Source: src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor]
- [Source: _bmad-output/implementation-artifacts/3-3-restrict-lift-restriction-and-record-revoke-consent.md#Previous-Story-Intelligence]

## Validation Summary

- Source discovery loaded project context, sprint status, `epics.md`, `architecture.md`, UX spine/design/review docs, prior Stories 3.1-3.3, GDPR portability/processing docs, API/data-model docs, current AdminPortal GDPR components/services/tests, backend export/records query code, projection rebuild code, E2E fixture/spec seams, and recent git history.
- Checklist fixes applied before finalizing: called out the current erased-detail conflict, required export/records availability for erased/gone GDPR routes without personal fields, expanded Art.30 bounded field rendering, preserved safe filename derivation, prohibited payload previews, kept D7 verification out of scope, and tied tests to the existing bUnit/E2E seams.
- Latest-technology review did not identify a dependency change needed for this story. The relevant stack is pinned locally (.NET 10, FluentUI Blazor `5.0.0-rc.3`, xUnit v3/bUnit, Playwright), and no new packages or external APIs are required.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Preserved the existing `PartyGdprOperationsPanel` ownership and `IPartiesAdminPortalApiClient` boundary for export and processing-record actions; no Razor lower-level GDPR/gateway client injection or D7 verification changes were added.
- Kept export downloads behind `HexalithPartiesAdminPortal.downloadJson`, continued to ignore server filenames, re-derived safe filenames from party id plus UTC timestamp, and cleared stale filenames on export failures.
- Allowed Art.20 export and Art.30 processing-record retrieval for restricted, erased, gone, and direct GDPR route terminal contexts while keeping mutation controls disabled for erased/pending-erasure states.
- Added a bounded terminal GDPR shell for direct erased/gone routes so export and processing records remain available without restoring display names, contacts, identifiers, consent details, or name history.
- Expanded `ProcessingRecordsPanel` to render the bounded Art.30 schema through `AdminPortalLabels`: party id, tenant id, sequence number, event type, operation category, timestamp, actor id, correlation id, outcome, and truncated summary.
- Extended bUnit coverage for export statuses, empty payload, JS failure, contract failure, stale export suppression, direct erased/gone GDPR route behavior, full bounded processing-record rendering, ordering, encoding, and truncation.
- Extended the E2E fixture/spec seam for direct erased GDPR route export and records, bounded request capture, no browser-visible EventStore command/query calls, no native dialogs, and narrow/zoom overflow evidence.
- Validation: focused AdminPortal build passed with 0 warnings; direct AdminPortal xUnit executable passed 163/163; E2E typecheck passed; Playwright discovery found 32 tests; warning-override gate passed.
- Residual limitations: `dotnet test` is blocked by local IPC permissions; focused Playwright runtime is blocked by Kestrel socket permissions; full solution build fails in sibling `Hexalith.PolymorphicSerializations` packaging warnings-as-errors; broader direct Contracts tests also have unrelated/package-feed failures.
- Definition of Done: PASS for Story 3.4 scoped implementation and focused validation gates. Broader repo/environment failures are documented above.

### File List

- _bmad-output/implementation-artifacts/3-4-data-export-art-20-and-processing-records-art-30.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor
- src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor
- src/Hexalith.Parties.AdminPortal/Components/ProcessingRecordsPanel.razor
- src/Hexalith.Parties.AdminPortal/Services/AdminPortalLabels.cs
- src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs
- tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs
- tests/Hexalith.Parties.AdminPortal.Tests/Services/RecordingAdminPortalApiClient.cs
- tests/e2e/specs/admin-parties-list.spec.ts

### Change Log

- 2026-06-10 - Implemented Story 3.4 Art.20 export and Art.30 processing-record UI behavior, terminal erased/gone GDPR route support, bounded labels/fixtures, bUnit coverage, E2E coverage, and validation evidence.
- 2026-06-10 - Senior developer review fixed AC9 stale GDPR query state cleanup, added regression coverage, and marked story done after focused validation.

## Senior Developer Review (AI)

Reviewer: Administrator on 2026-06-10

### Outcome

Approved after auto-fix. Story status moved to `done`; sprint status synced.

### Findings Fixed

- [HIGH] AC9 cleanup gap: failed processing-record queries preserved a stale successful export filename and prior command correlation in the GDPR panel. Fixed `RunQueryAsync` to clear command correlations on query completion and clear both correlations and export filename on query failure. Added bUnit regression coverage for a successful export plus command correlation followed by a transient processing-record failure.
- [MEDIUM] Story validation evidence was stale after review changes because the AdminPortal test count increased from 162 to 163. Updated the review record with current validation evidence.

### Validation

- PASS: `dotnet build tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj -c Release --no-restore -m:1 -v:minimal`
- PASS: `./tests/Hexalith.Parties.AdminPortal.Tests/bin/Release/net10.0/Hexalith.Parties.AdminPortal.Tests` (`163/163`)
- PASS: `npm run typecheck` in `tests/e2e`
- PASS: `npx playwright test --list` in `tests/e2e` (`32` tests discovered)
- PASS: `bash scripts/check-no-warning-override.sh`
- PASS: `git diff --check`
- BLOCKED BY ENVIRONMENT: `dotnet test ... --no-build` cannot create the local test IPC pipe (`SocketException 13`).
- BLOCKED BY ENVIRONMENT: focused Playwright runtime cannot bind Kestrel (`SocketException 13`).
