# Story 7.10: Enforce Admin Console Privacy and Encoding Rules

Status: done

## Story

As a privacy-conscious operator,
I want the admin console to prevent personal data leakage through UI, logs, routes, storage, and telemetry,
so that administration workflows do not create secondary privacy exposure.

## Acceptance Criteria

1. Given user, backend, AI-created, or operator-entered content is rendered, when the admin console displays it, then content is rendered through encoded Razor/component text paths, and raw markup, raw HTML fragments, JavaScript interpolation, and unsafe rendering APIs are not used.
2. Given routes, storage keys, telemetry dimensions, filenames, logs, link labels, DOM event names, or JavaScript event payloads are generated, when party data is available, then names, emails, identifiers, consent text, free text, tenant membership payloads, JWTs, claims dictionaries, sidecar names, and DAPR ports are excluded, and safe identifiers are used only where explicitly allowed.
3. Given backend failures, malformed responses, or ProblemDetails responses occur, when the console handles them, then raw bodies and raw details are not rendered or logged, and bounded localized summaries are used instead.
4. Given operator actions such as erasure, export, retry, and link navigation are logged, when logs or telemetry are emitted, then they include operation category, non-PII id, correlation/status id, bounded outcome code, and retry category where useful, and they exclude personal data and secrets.
5. Given privacy and encoding tests run, when they cover malicious party data, AI-created content, ProblemDetails text, filenames, routes, telemetry, logs, storage, and DOM event payloads, then no unsafe rendering or PII leakage is detected.

## Tasks / Subtasks

- [x] Add privacy and unsafe-rendering guardrails. (AC: 1, 2, 4, 5)
  - [x] Assert AdminPortal source does not use `MarkupString`, `AddMarkupContent`, `RenderTreeBuilder`, browser storage APIs, logging APIs, or telemetry APIs.
  - [x] Assert component-generated accessibility attributes do not duplicate raw party names or party ids.
  - [x] Preserve safe EventStore link labels and export filename behavior.

- [x] Preserve bounded failure and encoded rendering behavior. (AC: 1, 3, 5)
  - [x] Preserve malicious party data encoding tests.
  - [x] Preserve malformed/backend failure tests that reject raw ProblemDetails/parser text.
  - [x] Preserve GDPR disabled reason and export filename tests.

- [x] Validate the affected scope.
  - [x] Run `dotnet test tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release`.
  - [x] Run `dotnet build Hexalith.Parties.slnx --configuration Release` if production code changes.

## Dev Notes

### Story Source

- Current sprint tracking maps story 7.10 to `7-10-enforce-admin-console-privacy-and-encoding-rules`.
- Story source: `_bmad-output/planning-artifacts/epics.md`, Story 7.10.

### Current Implementation to Preserve

- Razor/component text rendering is already used for party data, processing summaries, ProblemDetails-safe statuses, and GDPR operation messages.
- EventStore Admin UI links use generic labels and encoded query parameters.
- GDPR exports re-derive filenames from bounded identifiers and timestamps.
- Story 7.9 changed row state descriptions to use non-PII generated DOM ids.

### Anti-Patterns to Avoid

- Do not use raw HTML rendering APIs or `MarkupString`.
- Do not store tokens, claims, tenant membership payloads, names, emails, identifiers, consent text, free text, DAPR ports, or sidecar names in browser storage, logs, telemetry dimensions, filenames, routes, or DOM event payloads.
- Do not add logging/telemetry from the AdminPortal component until a bounded privacy design exists.
- Do not weaken route hardening, EventStore link safety, GDPR capability gating, stale-response, accessibility, or localization tests.

### Testing

- Primary test file: `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs`
- Validation commands:
  - `dotnet test tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release`
  - `dotnet build Hexalith.Parties.slnx --configuration Release`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-05-22 | 0.1 | Created in-progress story context for Epic 7 Story 7.10 privacy and encoding guardrails. | Codex |
| 2026-05-22 | 0.2 | Added AdminPortal privacy source scan and ARIA attribute leakage tests; moved story to done. | Codex |

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- Loaded Story 7.10 source from `_bmad-output/planning-artifacts/epics.md`.
- Scanned AdminPortal components, services, and existing privacy/encoding tests.
- Confirmed existing coverage for encoded malicious fields, bounded ProblemDetails/parser failures, safe EventStore link labels, and safe GDPR export filenames.
- Added component coverage proving row accessibility attributes do not leak raw party ids or raw display names.
- Added source-level guardrail coverage for unsafe rendering, browser storage, logging, and telemetry APIs in AdminPortal source.
- Validation: `dotnet test tests\Hexalith.Parties.AdminPortal.Tests\Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release --no-restore` passed with 100/100 tests.
- Validation: Release solution build was not rerun for this story because no production code changed; the solution build passed after Story 7.9 production changes.
- Review: no critical/high/medium issues found.

### Completion Notes List

- Added privacy and encoding regression coverage without production code changes.
- Story moved to `done` after AdminPortal tests and review passed.

## Senior Developer Review

Reviewer: Codex

Date: 2026-05-22

### Findings

- No critical, high, or medium issues remain.

### Verification

- `dotnet test tests\Hexalith.Parties.AdminPortal.Tests\Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release --no-restore` passed with 100/100 tests.
- Release solution build was not rerun for this story because no production code changed; the previous 7.9 build passed with 0 warnings and 0 errors.

### File List

- `_bmad-output/implementation-artifacts/7-10-enforce-admin-console-privacy-and-encoding-rules.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/story-automator/orchestration-1-20260521-062818.md`
- `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs`
