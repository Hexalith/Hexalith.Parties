# Story 7.8: Localize Admin Console Text and Status

Status: done

## Story

As a Parties administrator,
I want all admin console labels and statuses localized,
so that the admin surface is consistent with FrontComposer localization expectations.

## Acceptance Criteria

1. Given the admin console renders toolbar, grid, detail, GDPR, empty, error, and status text, when labels or messages are displayed, then they come from localized resource strings or the FrontComposer localization mechanism, and hard-coded user-facing strings are avoided.
2. Given dates, booleans, counts, lawful-basis labels, validation messages, warning copy, and operation outcomes are displayed, when locale changes or localized resources are inspected, then formatting and text follow the active localization context, and fallback behavior is bounded.
3. Given ProblemDetails title or detail text is displayed, when the UI renders it, then the text is encoded and bounded, and it is not used as a localization key.
4. Given localized resources are missing, when the console attempts to render affected text, then fallback text is safe and discoverable in tests, and missing resource coverage can be detected before release.
5. Given localization tests run, when they cover core labels, status messages, validation, operation outcomes, date/number formatting, fallback, and ProblemDetails handling, then the console is localization-ready.

## Tasks / Subtasks

- [x] Strengthen the AdminPortal label seam. (AC: 1, 2, 4)
  - [x] Allow hosts to override enum label mapping methods.
  - [x] Add a localizable lawful-basis label hook for GDPR consent records and choices.
  - [x] Preserve safe default fallback labels.

- [x] Keep rendered text flowing through labels and current culture. (AC: 1, 2, 3)
  - [x] Verify toolbar, detail, GDPR, status, validation, and operation outcome text use `AdminPortalLabels`.
  - [x] Preserve current-culture date formatting for visible dates.
  - [x] Keep backend failure details bounded and out of localization keys.

- [x] Add focused localization regression tests. (AC: 1-5)
  - [x] Cover custom shell/detail/GDPR labels and operation outcomes.
  - [x] Cover validation messages with custom label values.
  - [x] Cover enum label overrides including lawful basis.
  - [x] Preserve existing privacy, route, stale-response, and GDPR gating tests.

- [x] Validate the affected scope.
  - [x] Run `dotnet test tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release`.
  - [x] Run `dotnet build Hexalith.Parties.slnx --configuration Release` if production code changes.

## Dev Notes

### Story Source

- Current sprint tracking maps story 7.8 to `7-8-localize-admin-console-text-and-status`.
- Story source: `_bmad-output/planning-artifacts/epics.md`, Story 7.8.
- Microsoft Learn confirms Blazor renders values using `CultureInfo.CurrentCulture` and supports localized content through .NET localization mechanisms such as `IStringLocalizer`; the current AdminPortal uses an injected `AdminPortalLabels` object as its host-facing localization seam.

### Current Implementation to Preserve

- `src/Hexalith.Parties.AdminPortal/Services/AdminPortalLabels.cs` centralizes fallback text for the Admin Portal.
- `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor` accepts `AdminPortalLabels? Labels` and uses `EffectiveLabels` throughout the shell, list, detail, status, and GDPR child panel parameters.
- Visible date formatting uses `CultureInfo.CurrentCulture` in AdminPortal and GDPR child panels.
- Existing tests already cover localized shell labels, French date formatting, booleans, counts, and bounded validation/error states.

### Anti-Patterns to Avoid

- Do not add a second standalone localization system inside AdminPortal components.
- Do not use raw ProblemDetails titles/details, backend exception names, tenant ids, claims, or party data as localization keys.
- Do not hard-code new user-facing text in Razor components when it belongs in `AdminPortalLabels`.
- Do not weaken privacy, encoding, GDPR gating, stale-response, or tenant-switch behavior.

### Testing

- Primary test file: `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs`
- Validation commands:
  - `dotnet test tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release`
  - `dotnet build Hexalith.Parties.slnx --configuration Release`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-05-22 | 0.1 | Created in-progress story context for Epic 7 Story 7.8 localization hardening. | Codex |
| 2026-05-22 | 0.2 | Added extensible enum/lawful-basis labels, GDPR localization tests, and validation; moved story to review. | Codex |
| 2026-05-22 | 0.3 | Review found no blocking issues; moved story to done. | Codex |

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- Loaded Story 7.8 source from `_bmad-output/planning-artifacts/epics.md`.
- Loaded AdminPortal label seam, current localization tests, and GDPR child panel label usage.
- Queried Microsoft Learn for Blazor globalization/localization guidance; relevant guidance confirms current-culture formatting and .NET localization support.
- Made `AdminPortalLabels` extensible and added virtual enum-label hooks including `LawfulBasisLabel`.
- Routed GDPR consent lawful-basis rendering and select option text through `AdminPortalLabels`.
- Added AdminPortal component tests for custom validation text, GDPR outcome labels, and enum/lawful-basis label overrides.
- Validation: `dotnet test tests\Hexalith.Parties.AdminPortal.Tests\Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release --no-restore` passed with 98/98 tests.
- Validation: `dotnet build Hexalith.Parties.slnx --configuration Release` passed with 0 warnings and 0 errors.
- Review: no critical/high/medium issues found.

### Completion Notes List

- Strengthened the host-facing localization seam without adding a second localization system.
- Added focused localization coverage for validation text, GDPR operation outcomes, and enum/lawful-basis labels.
- Story moved to `done` after AdminPortal tests, Release solution build, and review passed.

## Senior Developer Review

Reviewer: Codex

Date: 2026-05-22

### Findings

- No critical, high, or medium issues remain.

### Verification

- `dotnet test tests\Hexalith.Parties.AdminPortal.Tests\Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release --no-restore` passed with 98/98 tests.
- `dotnet build Hexalith.Parties.slnx --configuration Release` passed with 0 warnings and 0 errors.

### File List

- `_bmad-output/implementation-artifacts/7-8-localize-admin-console-text-and-status.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Parties.AdminPortal/Services/AdminPortalLabels.cs`
- `src/Hexalith.Parties.AdminPortal/Components/ConsentManagementPanel.razor`
- `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs`
