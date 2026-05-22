# Story 7.5: Add Safe EventStore Admin UI Links

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a Parties administrator,
I want safe links from party records to EventStore Admin UI diagnostics,
so that I can inspect streams, commands, and correlations without duplicating generic EventStore browsing in Parties.

## Acceptance Criteria

1. Given EventStore Admin UI is configured and available, when the admin console renders diagnostic links, then it generates links only from safe identifiers such as stream id, aggregate id, command id, correlation id, or timestamp, and link labels use generic text such as `Open stream`, `Open command status`, or `Open correlation`.
2. Given EventStore Admin UI URL or availability is missing, when diagnostic links are rendered, then the controls are disabled with a bounded localized reason, and no fallback embedded browser or generic event stream browser is shown inside Parties.
3. Given party data includes names, emails, identifiers, consent text, or free text, when EventStore links are generated, then none of those values appear in URLs, link labels, storage keys, telemetry dimensions, or logs.
4. Given an administrator activates a safe EventStore link, when the navigation occurs, then focus and navigation behavior remain predictable in the FrontComposer shell, and no raw payload or token is appended to the link.
5. Given EventStore link tests run, when they cover configured, unavailable, disabled, safe identifier, PII redaction, and navigation cases, then links remain useful without becoming a privacy or boundary leak.

## Tasks / Subtasks

- [x] Preserve safe EventStore Admin UI delegation. (AC: 1, 2)
  - [x] Keep generic stream/correlation URL generation inside `AdminPortalEventStoreAdminLinks`; do not build EventStore URLs inline in the component.
  - [x] Preserve configured base URL, stream path, correlation path, existing query string, URL encoding, target `_blank`, and `rel="noopener noreferrer"` behavior.
  - [x] Render disabled bounded controls when EventStore Admin UI configuration is absent; do not embed or recreate EventStore stream/event browsing inside Parties.

- [x] Keep links free of party PII and raw payloads. (AC: 1, 3, 4)
  - [x] Use only safe identifiers such as aggregate id and correlation id in generated URLs.
  - [x] Keep link labels generic; do not include display names, emails, identifiers, consent text, free text, raw errors, tenant membership data, tokens, claims, or raw payload snippets.
  - [x] Verify URL query construction encodes values and never appends raw payload, token, or operator-entered text.

- [x] Preserve admin console integration. (AC: 1, 2, 4)
  - [x] Keep detail-region stream links available when a selected party detail is loaded and the Admin UI base URL is configured.
  - [x] Keep unavailable state localized through `AdminPortalLabels.EventStoreAdminUnavailable`.
  - [x] Preserve dense working-region layout and avoid adding a diagnostic card shell, modal browser, or explanatory feature text.

- [x] Add or strengthen focused tests. (AC: 1-5)
  - [x] Extend `tests/Hexalith.Parties.AdminPortal.Tests/Services/PartiesAdminPortalServiceCollectionTests.cs` to cover custom paths, existing base query strings, blank identifiers, safe encoding, and PII-free URLs.
  - [x] Extend `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs` to cover configured detail links, unavailable disabled state, generic labels, safe navigation attributes, and PII-free href/label behavior.
  - [x] Preserve existing EventStore Admin UI link tests from Stories 7.1-7.4 and Story 12.7.

- [x] Validate the affected scope.
  - [x] Run `dotnet test tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release`.
  - [x] Run `dotnet build Hexalith.Parties.slnx --configuration Release` if production code changes.

## Dev Notes

### Story Source

- Current sprint tracking maps story 7.5 to `7-5-add-safe-eventstore-admin-ui-links`.
- Epic 7 objective: administrators can browse, inspect, and process Parties records and GDPR operations through a privacy-safe FrontComposer admin surface. [Source: `_bmad-output/planning-artifacts/epics.md#Epic 7: Administration Console`]
- Story 7.5 covers FR65 and UX-DR3, UX-DR21. [Source: `_bmad-output/planning-artifacts/epics.md#Story 7.5: Add Safe EventStore Admin UI Links`]

### Architecture Guardrails

- The Parties Admin Portal must delegate generic event/stream browsing to EventStore Admin UI safe deep-links rather than duplicating stream browsing in Parties. [Source: `_bmad-output/planning-artifacts/architecture.md#Frontend Architecture`]
- EventStore Admin UI links must use only safe identifiers and generic labels; if the Admin UI base address is unavailable, render a disabled bounded state instead of embedding a generic stream browser. [Source: `_bmad-output/planning-artifacts/ux-admin-portal-2026-05-10.md#EventStore Admin UI Boundary`]

### Current Implementation to Preserve

`src/Hexalith.Parties.AdminPortal/Services/AdminPortalEventStoreAdminLinks.cs`

- Current state: builds stream links with `aggregateId` and correlation links with `correlationId`, appends to existing query strings, combines configured base/path safely, URL-encodes parameters, and returns `null` when the base address or identifier is unavailable.
- Story change should prefer strengthening this helper and tests over spreading URL logic into the component.

`src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor`

- Current state: detail panel calls `EventStoreLinks.BuildStreamLink(_detail.Id)`, renders a generic `Open EventStore stream` link with `_blank` and `noopener noreferrer`, or a disabled Fluent button using `EventStoreAdminUnavailable`.
- Preserve: sibling detail working region, route hardening, encoded rendering, GDPR panel, and no scoped id display in user-facing metadata.

`tests/Hexalith.Parties.AdminPortal.Tests/Services/PartiesAdminPortalServiceCollectionTests.cs`

- Current state: covers basic stream/correlation URL generation and null when base address is unavailable.

`tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs`

- Current state: covers configured detail stream link attributes and PII-free href, plus unavailable disabled state.

### Previous Story Learnings

- Story 7.1 preserved privacy-safe EventStore Admin UI delegation while adding route hardening. [Source: `_bmad-output/implementation-artifacts/7-1-compose-admin-console-working-view.md`]
- Story 7.3 and 7.4 strengthened detail and failure-state privacy; do not weaken those tests while improving link coverage. [Source: `_bmad-output/implementation-artifacts/7-3-inspect-party-detail-safely.md`; `_bmad-output/implementation-artifacts/7-4-handle-admin-empty-error-and-degraded-states.md`]
- Story 12.7 rebuilt the Admin Portal around FrontComposer/EventStore query boundaries and EventStore Admin UI deep-links; preserve the helper, options, and safe link behavior. [Source: `_bmad-output/implementation-artifacts/12-7-admin-portal-rebuild-on-frontcomposer.md`]

### Anti-Patterns to Avoid

- Do not implement a stream browser, event grid, payload viewer, command-status viewer, or diagnostic modal inside Parties.
- Do not include display names, emails, identifiers, consent text, free text, raw errors, tenant ids, tokens, claims, raw payloads, search text, or route fragments in EventStore link labels or URLs.
- Do not build URLs with string concatenation in the component when `AdminPortalEventStoreAdminLinks` exists.
- Do not weaken privacy, route-hardening, stale-response, localization, accessibility, or failure-state tests to make link work easier.

### Testing

- Primary component test file: `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs`
- Link helper/service test file: `tests/Hexalith.Parties.AdminPortal.Tests/Services/PartiesAdminPortalServiceCollectionTests.cs`
- Validation commands:
  - `dotnet test tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release`
  - `dotnet build Hexalith.Parties.slnx --configuration Release`

## Senior Developer Review (AI)

Reviewer: Jérôme on 2026-05-22

Outcome: Approved.

Findings:

- No critical, high, or medium issues found. The implementation keeps EventStore Admin UI URL construction in `AdminPortalEventStoreAdminLinks`, renders only the generated absolute URI, preserves generic link text and safe navigation attributes, and does not add embedded EventStore browsing to Parties.
- Story file list matches the application/source files changed for Story 7.5. Sprint/status files and orchestration logs are workflow tracking artifacts.
- Microsoft Learn Blazor navigation guidance confirms ordinary anchor/navigation attributes are rendered through component markup; this supports the direct `<a href=... target="_blank" rel="noopener noreferrer">` usage for the external Admin UI link.

Validation:

- `dotnet test tests\Hexalith.Parties.AdminPortal.Tests\Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release` passed: 90/90.
- First parallel solution build attempt failed on a transient file lock while tests were compiling dependencies.
- `dotnet build Hexalith.Parties.slnx --configuration Release` passed on serial retry: 0 warnings, 0 errors.

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-05-22 | 0.1 | Created ready-for-dev story context for current Epic 7 Story 7.5 and reconciled it with existing EventStore Admin UI link implementation. | Codex |
| 2026-05-22 | 0.2 | Added safe link encoding and PII-redaction coverage, rendered EventStore hrefs with `AbsoluteUri`, and moved story to review. | Codex |
| 2026-05-22 | 1.0 | Senior developer review approved the story and synced status to done. | Codex |

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- Resolved current sprint status and confirmed story key `7-5-add-safe-eventstore-admin-ui-links`.
- Loaded current Epic 7 Story 7.5 source, current EventStore Admin UI link helper, component link rendering, existing link tests, Story 7.1/7.3/7.4 notes, and Story 12.7 admin portal rebuild history.
- Skipped web research; local project versions and existing Fluent UI component usage are authoritative for this story.
- Changed the detail link href to render `streamLink.AbsoluteUri` so Blazor emits the encoded URI form for values such as spaces in safe identifiers.
- Added link-helper tests for custom paths, existing base query strings, encoded identifiers, and blank identifiers.
- Added component coverage proving generic labels, encoded hrefs, safe navigation attributes, and no party PII/raw payload terms in EventStore Admin UI links.
- Validation: `dotnet test tests\Hexalith.Parties.AdminPortal.Tests\Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release` passed with 90/90 tests.
- Validation: `dotnet build Hexalith.Parties.slnx --configuration Release` passed with 0 warnings and 0 errors.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Story status set to `ready-for-dev`.
- Sprint status updated for current story key `7-5-add-safe-eventstore-admin-ui-links`.
- Checklist review applied: story includes specific acceptance criteria, current files to inspect, preserve/change guidance, anti-patterns, validation commands, and source references.
- Strengthened EventStore Admin UI link safety by pinning encoded `AbsoluteUri` href output and PII-free label/href behavior.
- Story moved to `review` after AdminPortal tests and Release solution build passed.

### File List

- `_bmad-output/implementation-artifacts/7-5-add-safe-eventstore-admin-ui-links.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor`
- `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs`
- `tests/Hexalith.Parties.AdminPortal.Tests/Services/PartiesAdminPortalServiceCollectionTests.cs`
