# Story 7.4: Handle Admin Empty, Error, and Degraded States

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a Parties administrator,
I want every empty, error, and degraded admin state to be bounded and safe,
so that I can recover from problems without stale or sensitive data staying visible.

## Acceptance Criteria

1. Given the admin console has no matching results, when list or search completes successfully with no records, then the UI shows a localized empty state, and it does not imply that records exist in another tenant.
2. Given token, tenant, or admin permission is missing, when the admin console evaluates access, then it clears sensitive list, detail, GDPR, and export state, and shows the appropriate missing-token, missing-tenant, or admin-required state.
3. Given a tenant switch occurs while requests are in flight, when stale responses return, then the UI suppresses stale data, and selected row, caches, detail state, GDPR state, and export state reset before loading the new tenant.
4. Given a forbidden, cross-tenant, not-found, gone/erased, timeout, transient failure, degraded, malformed response, or contract-unavailable condition occurs, when the console renders the state, then it uses bounded localized status text, and raw response bodies, raw ProblemDetails details, tokens, claims, and personal data are not rendered.
5. Given a retryable list failure occurs, when the administrator uses retry, then the console retries the current safe request context, and focus returns predictably to the initiating control or relevant status region.
6. Given empty/error/degraded UI tests run, when they cover all documented operator states, stale response suppression, retry, focus behavior, redaction, and tenant switch, then the console handles failures without data leakage or confusing stale state.

## Tasks / Subtasks

- [x] Preserve localized empty and degraded list/search states. (AC: 1, 4, 6)
  - [x] Keep `NoParties`, `NoMatches`, display-name-only, rich-search degraded, degraded data, and loaded status text routed through `AdminPortalLabels`.
  - [x] Verify empty list and empty search states remain distinct, localized, and tenant-neutral; do not imply records exist in another tenant.
  - [x] Verify degraded rich-search and degraded metadata states use bounded operator text and coordinator state instead of raw downstream messages.

- [x] Preserve fail-closed authorization and context reset behavior. (AC: 2, 3)
  - [x] Keep `AuthenticationStateProvider` plus `Hexalith.Tenants` membership/role derivation through `IAdminPortalAuthorizationService`.
  - [x] Ensure sign-out, missing tenant, non-admin user, tenant switch, and route context changes clear rows, selected party, detail metadata, GDPR/export state, validation detail, retry failure state, and in-flight list/detail operations before rendering.
  - [x] Preserve `AdminPortalPartyQueryService.ResetForTenantSwitch`, `PartiesAdminListCoordinator.ResetForTenantSwitch`, and `AdminPortalGdprStateCoordinator.ResetForTenantSwitch` wiring.

- [x] Bound list/detail failure surfaces and retry behavior. (AC: 4, 5, 6)
  - [x] Verify forbidden, cross-tenant, not-found, gone/erased, timeout/transient, malformed/unknown, validation, and contract-unavailable failures map to bounded `StatusKind`, `DetailEmptyKind`, and `AdminPortalListState` values.
  - [x] Verify retry is visible only for retryable list failures and replays the current safe request context only.
  - [x] Verify retry focus behavior remains best-effort and does not throw when the target control is not rendered.
  - [x] Do not render raw ProblemDetails, parser details, response bodies, stack traces, tokens, claims, tenant membership data, personal data, or raw validation internals.

- [x] Preserve privacy-safe rendering and non-stale state after failures. (AC: 2, 3, 4)
  - [x] Continue rendering all status, empty, validation, degraded, detail, GDPR, export, and error text through normal Razor/component text paths.
  - [x] Do not use `MarkupString`, `AddMarkupContent`, `innerHTML`, JavaScript interpolation, raw HTML fragments, or unencoded attributes for any failure or backend-provided text.
  - [x] Verify stale list and detail responses after tenant/auth changes cannot restore previous tenant rows, details, GDPR/export content, or status text.

- [x] Add or strengthen focused tests for empty/error/degraded states. (AC: 1-6)
  - [x] Extend `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs` to cover empty browse, empty search, degraded search, degraded metadata, missing token, missing tenant, non-admin, forbidden, not-found, gone/erased, transient retry, contract-unavailable/malformed failure, tenant-switch stale suppression, and failure redaction.
  - [x] Preserve existing tests for authorization fail-closed paths, tenant switch, stale response suppression, retry, coordinator transitions, route hardening, detail safety, localization, and encoded rendering.
  - [x] Extend `tests/Hexalith.Parties.AdminPortal.Tests/Services/PartiesAdminPortalApiClientTests.cs` only if failure mapping or client/query contract behavior changes.

- [x] Validate the affected scope.
  - [x] Run `dotnet test tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release`.
  - [x] Run `dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj --filter "FullyQualifiedName~AdminPortal" --configuration Release` if client/query contract or source guardrails change.
  - [x] Run `dotnet build Hexalith.Parties.slnx --configuration Release` if production code changes.

## Dev Notes

### Story Source

- Current sprint tracking maps story 7.4 to `7-4-handle-admin-empty-error-and-degraded-states`.
- Epic 7 objective: administrators can browse, inspect, and process Parties records and GDPR operations through a privacy-safe FrontComposer admin surface. [Source: `_bmad-output/planning-artifacts/epics.md#Epic 7: Administration Console`]
- Story 7.4 covers FR65 and UX-DR8, UX-DR9, UX-DR19, UX-DR21. [Source: `_bmad-output/planning-artifacts/epics.md#Story 7.4: Handle Admin Empty, Error, and Degraded States`]

### Architecture Guardrails

- The Parties Admin Portal is a FrontComposer domain surface implemented with Blazor/Razor and Fluent UI Blazor. It must register Parties-domain views with the FrontComposer shell, read through EventStore query/client abstractions, route supported commands through the typed Parties client/EventStore command boundary, and delegate generic event/stream browsing to EventStore Admin UI safe deep-links. [Source: `_bmad-output/planning-artifacts/architecture.md#Frontend Architecture`]
- The portal must fail closed and clear sensitive state on sign-out, missing tenant, non-admin user, tenant switch, stale response, forbidden, not found, gone/erased, timeout, malformed response, and contract-unavailable failures. [Source: `_bmad-output/planning-artifacts/architecture.md#Frontend Architecture`]
- FrontComposer admin/picker UX stale-response rules require token, tenant, user, host configuration, selected id, or search option changes to suppress stale responses and handle loading, empty, retry, degraded, unauthorized, forbidden, not-found, gone/erased, and transient-failure states without leaking personal data. [Source: `_bmad-output/planning-artifacts/architecture.md#D20 — FrontComposer Party Picker and Admin Portal UX Contracts`]

### Current Implementation to Preserve

`src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor`

- Current state: `StatusText`, `EmptyText`, and `DetailEmptyText` map component states to localized `AdminPortalLabels` values.
- Current state: `ApplyAuthorizationContextAsync` clears state and transitions coordinators for sign-in required, tenant unavailable, and admin-required states.
- Current state: `LoadPageAsync`, `ApplyRowsAsync`, `ApplyFailure`, `_listVersion`, `_detailVersion`, `SwapListCts`, `SwapDetailCts`, and `ResetVisibleState` implement bounded list/detail state changes and stale-response suppression.
- Current state: `RetryAvailable` exposes retry only for transient/unknown list failures; `RetryAsync` calls `LoadPageAsync` with the current filters, page, tenant, and auth context.
- Preserve: Story 7.1 route hardening, Story 7.2 date-filter validation, Story 7.3 detail safety tests, encoded Razor rendering, cancellation/version guards, tenant/auth reset behavior, GDPR coordinator reset behavior, status announcements, keyboard handling, and safe EventStore link delegation.

`src/Hexalith.Parties.AdminPortal/Services/AdminPortalLabels.cs`

- Current state: contains bounded labels for loading, loaded, sign-in required, tenant unavailable, access denied, transient failure, no data, no parties, no matches, validation, degraded search, detail unavailable, erased detail, and detail load failure.
- Story change should prefer adding/using labels here over inline operator text.

`src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs`

- Current state: maps typed client/FrontComposer query failures to `AdminPortalQueryFailureKind` and sanitizes ProblemDetails-derived failures into bounded UI outcomes.
- Preserve: typed `IPartiesQueryClient` preference, `IQueryService` fallback, contract-unavailable handling, bounded failure mapping, null-collection normalization, and redaction of raw ProblemDetails details.

`tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs`

- Current state: covers empty first viewport, unauthenticated, non-admin, rich-search degraded, tenant switch, delayed stale list/detail responses, gone/forbidden detail failures, transient list retry, localization, coordinator transitions, route hardening, privacy-safe detail rendering, and EventStore link safety.
- Story change: strengthen empty/error/degraded coverage, especially list-level contract-unavailable/unknown failures, no raw downstream text, retry context, and tenant-neutral empty copy.

### Previous Story Learnings

- Story 7.1 completed the operational first viewport and route support for `/admin/parties/{partyId}` and `/admin/parties/{partyId}/gdpr`; selected rows navigate only for bounded non-PII route tokens. Preserve route hardening. [Source: `_bmad-output/implementation-artifacts/7-1-compose-admin-console-working-view.md`]
- Story 7.2 added date filters and bounded validation; invalid filters must not dispatch list/search queries or leak raw validation internals. [Source: `_bmad-output/implementation-artifacts/7-2-browse-and-filter-party-results.md`]
- Story 7.3 strengthened detail safety coverage for state text, erased terminal payloads, bounded detail failures, and stale tenant detail suppression. Preserve those tests. [Source: `_bmad-output/implementation-artifacts/7-3-inspect-party-detail-safely.md`]
- Story 12.7 rebuilt the Admin Portal around FrontComposer/EventStore query boundaries and EventStore Admin UI deep-links. Preserve typed client preference, query-service fallback, GDPR client operations, and safe link generation. [Source: `_bmad-output/implementation-artifacts/12-7-admin-portal-rebuild-on-frontcomposer.md`]

### Anti-Patterns to Avoid

- Do not build a TypeScript SPA, standalone admin shell, duplicate tenant selector, duplicate authorization model, generic stream/event browser, or local error-state cache inside Parties.
- Do not query retired REST paths, admin endpoints, DAPR actor internals, EventStore streams, or projection actors directly from the portal component.
- Do not add client-side filtering, tenant inference, or raw downstream-message parsing to explain empty/error states.
- Do not render party data, ProblemDetails text, parser text, consent text, processing summaries, operator-entered values, tokens, claims, or backend-provided text through raw markup APIs.
- Do not put names, emails, identifiers, consent text, free text, raw errors, tenant ids, tokens, claim values, route fragments, or search text in URLs, storage keys, telemetry dimensions, filenames, logs, or EventStore Admin UI link labels.
- Do not weaken existing bUnit, privacy, accessibility, localization, route-hardening, tenant-switch, stale-response, retry, or transport guardrail tests to make error work easier.

### Testing

- Primary test file: `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs`
- API/client boundary test file: `tests/Hexalith.Parties.AdminPortal.Tests/Services/PartiesAdminPortalApiClientTests.cs`
- Existing fake API: `tests/Hexalith.Parties.AdminPortal.Tests/Services/RecordingAdminPortalApiClient.cs`
- Validation commands:
  - `dotnet test tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release`
  - `dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj --filter "FullyQualifiedName~AdminPortal" --configuration Release`
  - `dotnet build Hexalith.Parties.slnx --configuration Release`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-05-22 | 0.1 | Created ready-for-dev story context for current Epic 7 Story 7.4 and reconciled it with existing AdminPortal empty/error/degraded state coverage. | Codex |
| 2026-05-22 | 0.2 | Added focused empty search, degraded metadata, bounded list failure, retry, and missing-tenant clearing tests; moved story to review. | Codex |
| 2026-05-22 | 0.3 | Senior Developer Review (AI) completed; outcome Approve; story moved to done. | Claude Opus 4.7 |

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- Resolved current sprint status and confirmed story key `7-4-handle-admin-empty-error-and-degraded-states`.
- Loaded current Epic 7 Story 7.4 source, frontend architecture D20, Story 7.1/7.2/7.3 completion notes, Story 12.7 admin portal rebuild history, and current AdminPortal component/API-client/test references.
- Skipped web research; local project versions and existing Fluent UI component usage are authoritative for this story.
- Added focused component tests for tenant-neutral empty search, degraded metadata redaction, contract-unavailable list clearing, unknown-failure retry, and missing-tenant clearing after detail hydration.
- Validation: `dotnet test tests\Hexalith.Parties.AdminPortal.Tests\Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release` passed with 87/87 tests.
- Validation: `dotnet build Hexalith.Parties.slnx --configuration Release` passed with 0 warnings and 0 errors.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Story status set to `ready-for-dev`.
- Sprint status updated for current story key `7-4-handle-admin-empty-error-and-degraded-states`.
- Checklist review applied: story includes specific acceptance criteria, current files to inspect, preserve/change guidance, anti-patterns, validation commands, and source references.
- Strengthened existing empty/error/degraded state coverage without changing production component behavior; current implementation already satisfied the newly pinned safety paths.
- Story moved to `review` after AdminPortal tests and Release solution build passed.

### File List

- `_bmad-output/implementation-artifacts/7-4-handle-admin-empty-error-and-degraded-states.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs`

## Senior Developer Review (AI)

**Reviewer:** Jérôme Piquot (automated review by Claude Opus 4.7)
**Date:** 2026-05-22
**Outcome:** Approve

### Summary

Story 7.4 strengthens empty/error/degraded coverage for the Parties Admin Portal without changing production behavior. Five new bUnit tests pin tenant-neutral empty search copy, bounded degraded-metadata redaction, contract-unavailable list clearing with detail reset, unknown-failure retry, and missing-tenant clearing after detail hydration. All ACs 1–6 are exercised. Validation passes: `dotnet test` reports 87/87 passing in Release. File List matches git reality; no production source changed.

### Findings

- **AC validation:** All six acceptance criteria are exercised by the new and pre-existing tests. Empty search (AC1), fail-closed authorization & context reset (AC2), tenant switch suppression (AC3), bounded failure surfaces with no raw leakage (AC4), retry on retryable list failures (AC5), and aggregate test coverage (AC6) are present.
- **Code quality:** Tests follow existing conventions in the file (Arrange/Act/Assert, `RenderAuthorized` helper, `RecordingAdminPortalApiClient`, Shouldly assertions, `WaitForAssertion` for async UI). Naming follows `{Component}_{Scenario}_{Expected}` convention.
- **Security / privacy:** Tests explicitly assert non-leakage of sensitive substrings (`"sensitive@example.test"`, `"before-failure@example.test"`, raw degraded-metadata text, internal failure kind names, parser internals like `"System.Text.Json"` / `"ProblemDetails"`). No `MarkupString` usage introduced.
- **Documentation:** File List matches git diff exactly (one source file modified plus story + sprint status). Change Log updated.

### Action Items

- None blocking. The story is approved as-is.

### Notes

- Production source under `src/Hexalith.Parties.AdminPortal/` was not modified — story explicitly aimed at strengthening test coverage of already-satisfied behavior, and that intent is honored.
- Sprint-status.yaml entry updated to `done` after review.
