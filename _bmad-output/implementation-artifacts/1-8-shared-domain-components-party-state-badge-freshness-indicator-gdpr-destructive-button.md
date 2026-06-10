---
baseline_commit: 90f2b97
---

# Story 1.8: Shared domain components (party-state badge, freshness indicator, GDPR destructive button)

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer,
I want the three shared domain components built on FluentUI V5 with color-plus-label and token pairs,
so that both Admin and Consumer areas render party state, data freshness, and GDPR destructive actions consistently and accessibly.

This story builds the shared visual primitives that later Epic 2/4/5 screens consume. It does not add pages, routes, Fluxor slices, gateway calls, picker behavior, or AdminPortal adoption. The deliverable is the component set under `Hexalith.Parties.UI/Components/Shared` plus bUnit tests proving state labels, ARIA semantics, and token-bound styling contracts.

## Acceptance Criteria

1. **AC1 - Party lifecycle badge uses Fluent badge semantics and color-plus-label.** Given a party lifecycle value, when `PartyStateBadge` renders, then it uses `FluentBadge` with `BadgeAppearance.Tint` and a rounded/circular pill shape, renders a visible text label for `active`, `inactive`, `restricted`, and `erased`, and never communicates state by color alone. Erased state renders tombstone copy only, never party names, ids, contact values, identifiers, or stale personal data.

2. **AC2 - Party lifecycle mapping is reusable and contract-aware.** Given either a `PartyDetail` or the state booleans available from a list/detail view, when `PartyStateBadge` determines state, then `IsErased` wins over every other state, `IsRestricted` wins over active/inactive for detail payloads, and list rows that only have `PartyIndexEntry.IsActive` plus `IsErased` do not pretend to know restricted state. The component exposes a small explicit API so later screens do not duplicate `StatusLabel` / `DetailStatus` logic from AdminPortal.

3. **AC3 - Freshness indicator maps every `ProjectionFreshnessStatus` through the canonical status rules.** Given `ProjectionFreshnessMetadata`, when `DataFreshnessIndicator` renders, then `Current` shows a dot plus "Up to date"; `Stale` shows a dot plus "Showing what we last knew - refreshing" and, when a timestamp is supplied, "as of HH:mm"; `Rebuilding`, `Degraded`, `Unavailable`, and `LocalOnly` show a degraded dot plus "Showing last known". The indicator reuses `StatusPresentation.FromFreshness(...)` for degraded classification rather than inventing a second freshness map.

4. **AC4 - Freshness transitions are announced politely.** Given the freshness text changes, when `DataFreshnessIndicator` renders, then the text node is in a `role="status" aria-live="polite"` region, using the same politeness contract as `StatusLiveRegion`; no validation or failure state is announced through this component.

5. **AC5 - GDPR destructive button distinguishes irreversible from reversible actions.** Given an irreversible GDPR action, when `GdprDestructiveButton` renders, then it uses a danger visual treatment based on Fluent status danger tokens, requires a real labeled typed-confirmation input before firing, keeps the irreversible action disabled until the typed value matches the expected confirmation value with ordinal comparison, and never auto-fires on focus, blur, or typing. Given a reversible GDPR action such as restrict, lift, withdraw, or revoke, the component renders `ButtonAppearance.Outline`, uses a single explicit click confirmation path, and does not use danger fill.

6. **AC6 - Token and accessibility guardrails are test-pinned.** Given the component suite, when bUnit tests run, then they assert visible labels for all party/freshness states, live-region attributes, typed-confirm label/description wiring, disabled/enabled behavior, no automatic callback before explicit click, and absence of hard-coded hex colors in the component markup/CSS. Tests must use xUnit v3, Shouldly, and bUnit, matching the existing UI test style.

## Tasks / Subtasks

### Part A - Party state badge - AC1, AC2, AC6

- [x] **Task 1 - Add a reusable party-state value model** (`src/Hexalith.Parties.UI/Components/Shared/PartyLifecycleState.cs`) (AC1, AC2)
  - [x] Define a minimal host-owned enum/value model for `Active`, `Inactive`, `Restricted`, and `Erased`; keep it UI-host scoped, not in `Contracts`.
  - [x] Add helper factory methods from explicit booleans and from `PartyDetail`. For list rows, accept only `isActive` and `isErased`; document that `PartyIndexEntry` cannot know `Restricted`.
  - [x] Precedence must be `Erased` > `Restricted` > `Active/Inactive`.

- [x] **Task 2 - Implement `PartyStateBadge`** (`src/Hexalith.Parties.UI/Components/Shared/PartyStateBadge.razor`) (AC1, AC2)
  - [x] Render `FluentBadge` with `Appearance="BadgeAppearance.Tint"` and a rounded/circular pill shape supported by Fluent UI V5.
  - [x] Map states to Fluent status token families: active/success, inactive/subtle or neutral, restricted/warning, erased/danger. Use Fluent token variables or `BadgeColor`/`BadgeAppearance` pairs; do not hand-pick hex colors.
  - [x] Render visible label text for every state. Labels default to `Active`, `Inactive`, `Restricted`, and `Erased`, and can be overridden by parameters for later localization.
  - [x] For erased state, expose only tombstone text. Do not accept or render `DisplayName`, `SortName`, ids, contact values, identifiers, or raw payload details.

- [x] **Task 3 - Add scoped CSS only if needed** (`src/Hexalith.Parties.UI/Components/Shared/PartyStateBadge.razor.css`) (AC1, AC6)
  - [x] If shape/spacing requires CSS, use layout/shape declarations and Fluent token variables only.
  - [x] No literal `#...`, `rgb(...)`, `hsl(...)`, raw teal `#0097A7`, or redeclared Fluent custom property definitions.

### Part B - Data freshness indicator - AC3, AC4, AC6

- [x] **Task 4 - Implement `DataFreshnessIndicator`** (`src/Hexalith.Parties.UI/Components/Shared/DataFreshnessIndicator.razor`) (AC3, AC4)
  - [x] Accept `ProjectionFreshnessMetadata? Freshness` and an optional `DateTimeOffset? AsOf`.
  - [x] Treat `null` freshness conservatively as degraded/last-known; do not render a blank or throw.
  - [x] Use `StatusPresentation.FromFreshness(...)` to distinguish fresh vs degraded. `Current` renders "Up to date"; all non-current statuses render degraded copy, with `Stale` allowed to include "as of HH:mm" when `AsOf` is supplied.
  - [x] Put the changing text in `role="status" aria-live="polite"` so stale-to-fresh transitions are announced. Do not add assertive/error semantics here.
  - [x] Render a visual dot plus the word/copy. The dot is decorative (`aria-hidden="true"`) because the text carries the state.

- [x] **Task 5 - Add scoped CSS only if needed** (`src/Hexalith.Parties.UI/Components/Shared/DataFreshnessIndicator.razor.css`) (AC3, AC6)
  - [x] Use Fluent status token variables for dot foreground/background. Do not use hard-coded colors.
  - [x] Include forced-colors safe styling if custom dot borders/backgrounds are used.

### Part C - GDPR destructive button - AC5, AC6

- [x] **Task 6 - Implement `GdprDestructiveButton`** (`src/Hexalith.Parties.UI/Components/Shared/GdprDestructiveButton.razor`) (AC5)
  - [x] Parameters should cover `IsIrreversible`, `Disabled`, button text, confirmation label, confirmation description/warning, expected confirmation value, current confirmation value, value-changed callback, and confirmed action callback.
  - [x] Irreversible actions render a `FluentButton` with danger-token styling and remain disabled until the typed confirmation exactly matches the expected value using `StringComparison.Ordinal`.
  - [x] The typed confirmation must be a real `FluentTextInput` or labeled input with an associated label and `aria-describedby` pointing at the irreversibility warning. Placeholder-only instructions are not enough.
  - [x] Reversible actions render `ButtonAppearance.Outline` and require an explicit click; no typed input is shown unless the caller opts into irreversible mode.
  - [x] Do not call the action callback from `onfocus`, `onblur`, input value changes, or component lifecycle methods. The callback fires only on an explicit enabled button click.

- [x] **Task 7 - Add scoped CSS only if needed** (`src/Hexalith.Parties.UI/Components/Shared/GdprDestructiveButton.razor.css`) (AC5, AC6)
  - [x] Use `--colorStatusDangerForeground1` and paired Fluent status/danger token variables for danger treatment; do not hard-code color literals.
  - [x] Preserve visible focus rings and keyboard operability; do not suppress `outline`.

### Part D - Tests and regression checks - AC1-AC6

- [x] **Task 8 - Add bUnit tests for `PartyStateBadge`** (`tests/Hexalith.Parties.UI.Tests/PartyStateBadgeTests.cs`) (AC1, AC2, AC6)
  - [x] Assert active/inactive/restricted/erased labels are present as visible text.
  - [x] Assert erased output contains tombstone copy and does not render supplied PII-like values if any helper overload receives a `PartyDetail`.
  - [x] Assert precedence: erased beats restricted/active; restricted beats active/inactive; list-row inputs do not infer restricted.
  - [x] Assert `FluentBadge` is present and configured with tinted appearance/expected color family where bUnit can observe component parameters.

- [x] **Task 9 - Add bUnit tests for `DataFreshnessIndicator`** (`tests/Hexalith.Parties.UI.Tests/DataFreshnessIndicatorTests.cs`) (AC3, AC4, AC6)
  - [x] Assert `Current`, `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, and `LocalOnly` render the expected dot plus text.
  - [x] Assert the text node has `role="status"` and `aria-live="polite"`.
  - [x] Assert null freshness renders a last-known/degraded message rather than blank output or an exception.
  - [x] Assert the component uses `StatusPresentation.FromFreshness` behavior by covering every `ProjectionFreshnessStatus`.

- [x] **Task 10 - Add bUnit tests for `GdprDestructiveButton`** (`tests/Hexalith.Parties.UI.Tests/GdprDestructiveButtonTests.cs`) (AC5, AC6)
  - [x] Assert irreversible mode renders a labeled input and description/warning, keeps the button disabled until the exact typed confirmation matches, and fires only on explicit click.
  - [x] Assert focus/blur/input changes alone do not call the callback.
  - [x] Assert reversible mode renders `ButtonAppearance.Outline`, no typed input by default, and no danger-fill class.

- [x] **Task 11 - Add style/token guard tests** (`tests/Hexalith.Parties.UI.Tests/SharedDomainComponentStyleTests.cs`) (AC6)
  - [x] Scan the new `.razor` / `.razor.css` files for forbidden color literals (`#`, `rgb(`, `rgba(`, `hsl(`, `hsla(`) except comments in the story are irrelevant.
  - [x] Assert raw `#0097A7` never appears in the new component files.

- [x] **Task 12 - Verify targeted gates** (AC6)
  - [x] Build `src/Hexalith.Parties.UI` in Release with `-m:1`.
  - [x] Build `tests/Hexalith.Parties.UI.Tests` in Release with `-m:1`.
  - [x] Run the UI test executable directly for the new test classes if `dotnet test --filter` returns zero tests under xUnit v3 MTP.
  - [x] Run `bash scripts/check-no-warning-override.sh`.

## Dev Notes

### Source Discovery Summary

- Loaded `epics_content` from `_bmad-output/planning-artifacts/epics.md`.
- Loaded `architecture_content` from `_bmad-output/planning-artifacts/architecture.md`.
- Loaded UX source documents selectively from `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/`: `DESIGN.md`, `EXPERIENCE.md`, `review-accessibility.md`, and `review-regulated-language.md`.
- No formal PRD exists for this brownfield initiative; `epics.md` and `architecture.md` state the requirements basis is brownfield docs plus UX design.
- Loaded project persistent facts from `_bmad-output/project-context.md`.
- Loaded previous story intelligence from `_bmad-output/implementation-artifacts/1-7-live-freshness-via-signalr-shared-optimistic-reconcile-effect.md`.

### Existing Code to Reuse

- Reuse `Hexalith.Parties.UI.Status.StatusKind`, `StatusPresentation`, and `LiveRegionPoliteness`; do not create a second status/freshness/politeness map.
- Reuse `StatusPresentation.FromFreshness(ProjectionFreshnessMetadata)` / `FromFreshness(ProjectionFreshnessStatus)` for degraded classification. Only `ProjectionFreshnessStatus.Current` is fresh; all other values are degraded.
- Reuse `StatusLiveRegion` semantics where useful, but `DataFreshnessIndicator` may render its own `role="status" aria-live="polite"` text node because the UX specifically requires the freshness indicator itself to announce transitions.
- Use `ProjectionFreshnessMetadata` and `ProjectionFreshnessStatus` from `Hexalith.Parties.Contracts.Models`; do not create new freshness contracts.
- Use `PartyDetail.IsActive`, `PartyDetail.IsRestricted`, `PartyDetail.IsErased`, and `PartyDetail.Freshness`. `PartyIndexEntry` exposes `IsActive` and `IsErased` only; it intentionally cannot identify restricted list rows.
- The current AdminPortal has local `StatusLabel` and `DetailStatus` logic in `PartiesAdminPortal.razor`; this story should extract a reusable host component for later screens, not refactor AdminPortal adoption now.
- The current AdminPortal erasure flow is not a real typed-confirm field. Story 1.8 should provide the correct reusable primitive; later Admin/Consumer stories can adopt it.

### Technical Constraints

- Put new shared components in `src/Hexalith.Parties.UI/Components/Shared/`. This host already references FluentUI V5, Parties.Client, Parties.Contracts, FrontComposer Shell, and EventStore SignalR.
- Do not add package versions or `Version=` attributes. Central Package Management is enabled in `Directory.Packages.props`; FluentUI Blazor is already pinned as `Microsoft.FluentUI.AspNetCore.Components` `5.0.0-rc.3-26138.1`.
- Fluent UI V5 local package metadata confirms `BadgeAppearance.Tint`, `BadgeColor`, `BadgeShape`, and `ButtonAppearance.Outline` / `Primary` are available in the installed package XML. Prefer component parameters and token variables over custom raw color CSS.
- Keep UI host brace style Allman/next-line in C# blocks where surrounding files use it; Razor markup follows existing component style.
- File-scoped namespaces for `.cs` files, `using` directives outside namespaces, no unused usings, nullable-safe code, and warnings-as-errors all apply.
- Do not edit generated output under `obj/**/generated`.
- Do not introduce Fluxor slices, `[Command]`, `[Projection]`, pages, routes, gateway calls, AppHost changes, SignalR wiring, Picker changes, AdminPortal changes, or ConsumerPortal scaffolding in this story.
- Ownership caveat: `Hexalith.Parties.AdminPortal` is an RCL referenced by the UI host, and the future `ConsumerPortal` is also planned as an RCL. Those RCLs must not reference `Hexalith.Parties.UI` directly because that creates a dependency cycle. This story follows the current architecture by creating the canonical host-owned shared components; if a later RCL needs direct compile-time reuse, move/extract the component into an adopter-facing shared RCL in a separate story instead of copying it.

### UX and Accessibility Guardrails

- State is never color-only: party badges always include text; freshness always includes a word/copy next to the dot.
- Use Fluent status token pairs (`--colorStatus*Foreground1` on `--colorStatus*Background1`) or component color/appearance pairs. Do not hand-mix arbitrary foreground/background colors.
- Do not use raw teal `#0097A7`; filled primary brand fill is `--colorBrandBackground`, but this story's state components should use status token families.
- Freshness is polite only: `role="status" aria-live="polite"`. Validation and failure alerts are handled by status/command surfaces, not `DataFreshnessIndicator`.
- Irreversible GDPR actions require a real labeled typed-confirmation input with `aria-describedby` tied to warning text. Placeholder text or a styled `div` is not acceptable.
- Typed confirmation values are personal data risk. Compare in memory only, do not log, persist, emit in telemetry, or include in error text.
- Do not interpolate party names, ids, contact values, identifiers, or raw payload values into tombstone, freshness, or destructive-action status copy.

### Previous Story Intelligence

- Story 1.7 completed the shared live-freshness and optimistic-reconcile services. Page consumption is still deferred. Story 1.8 should only provide the visual/domain components that later screens plug into those services.
- The UI test suite is xUnit v3 MTP; direct test executable runs are reliable when `dotnet test --filter` reports zero filtered tests.
- TreatWarningsAsErrors gotchas from 1.7: avoid analyzer noise, unused usings, and async disposal mistakes in tests. If a test scope contains `IAsyncDisposable` services, use async scope disposal.
- Status and politeness are already test-pinned in `StatusPresentationTests` and `StatusLiveRegionTests`. Add component tests instead of weakening or duplicating those contracts.

### File Structure Requirements

| Action | File |
|---|---|
| NEW | `src/Hexalith.Parties.UI/Components/Shared/PartyLifecycleState.cs` |
| NEW | `src/Hexalith.Parties.UI/Components/Shared/PartyStateBadge.razor` |
| NEW optional | `src/Hexalith.Parties.UI/Components/Shared/PartyStateBadge.razor.css` |
| NEW | `src/Hexalith.Parties.UI/Components/Shared/DataFreshnessIndicator.razor` |
| NEW optional | `src/Hexalith.Parties.UI/Components/Shared/DataFreshnessIndicator.razor.css` |
| NEW | `src/Hexalith.Parties.UI/Components/Shared/GdprDestructiveButton.razor` |
| NEW optional | `src/Hexalith.Parties.UI/Components/Shared/GdprDestructiveButton.razor.css` |
| NEW | `tests/Hexalith.Parties.UI.Tests/PartyStateBadgeTests.cs` |
| NEW | `tests/Hexalith.Parties.UI.Tests/DataFreshnessIndicatorTests.cs` |
| NEW | `tests/Hexalith.Parties.UI.Tests/GdprDestructiveButtonTests.cs` |
| NEW | `tests/Hexalith.Parties.UI.Tests/SharedDomainComponentStyleTests.cs` |

Do not modify `src/Hexalith.Parties.AdminPortal/**`, `src/Hexalith.Parties.Picker/**`, `src/Hexalith.Parties.ConsumerPortal/**`, `src/Hexalith.Parties.UI/Program.cs`, `src/Hexalith.Parties.AppHost/**`, or `Directory.Packages.props` for this story unless a real compile error proves a narrow update is required.

### Testing Requirements

- Use bUnit for Razor component assertions, Shouldly for assertions, xUnit v3 for tests, and existing UI test naming style.
- Assert DOM semantics, visible text, component parameters where possible, and callback firing behavior. Avoid placeholder tests that only render without assertions.
- Include a token guard that scans only this story's new component files. Keep it precise so it does not fail unrelated legacy code.
- Verification commands:
  - `dotnet build src/Hexalith.Parties.UI -c Release -m:1`
  - `dotnet build tests/Hexalith.Parties.UI.Tests -c Release -m:1`
  - Run the UI test executable for new test classes if needed.
  - `bash scripts/check-no-warning-override.sh`

### Project Structure Notes

- Alignment: new shared domain components belong in `Hexalith.Parties.UI/Components/Shared`, exactly as architecture reserves for cross-area components reused by Admin and Consumer.
- Intentional variance: `PartyLifecycleState` is host-owned UI presentation state, not a domain contract. Domain contracts already expose booleans (`IsActive`, `IsRestricted`, `IsErased`); adding a contract enum for UI labels would be scope creep.
- Existing legacy AdminPortal status rendering remains in place until later screens adopt the shared component. This story creates reusable primitives without changing existing AdminPortal behavior.
- `GdprDestructiveButton` supplies a typed-confirm primitive, but it does not execute GDPR commands itself. Command handling stays in client/effect/caller code.
- RCL dependency risk: do not make `AdminPortal` or `ConsumerPortal` reference the UI host to consume these components. Later adoption must be handled by host-level composition, a shared RCL extraction, or another architect-approved ownership change.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 1.8] - story statement and ACs for `PartyStateBadge`, `DataFreshnessIndicator`, and `GdprDestructiveButton`.
- [Source: _bmad-output/planning-artifacts/epics.md#UX-DR4/UX-DR5/UX-DR6] - party badge, freshness indicator, and GDPR destructive-button visual rules.
- [Source: _bmad-output/planning-artifacts/architecture.md#Communication Patterns] - canonical `StatusKind` table, politeness split, optimistic/reconcile, and no bespoke freshness/status mapping.
- [Source: _bmad-output/planning-artifacts/architecture.md#Project Structure] - shared domain components live under `UI/Components/Shared`.
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/DESIGN.md#Brand-layer / domain components] - FluentBadge, token pairs, danger button, and no hard-coded hex.
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Component Patterns] - color-plus-label, freshness dot plus word, typed-confirmation behavior, no blocking browser confirm.
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/review-accessibility.md#typed-confirmation field] - typed confirmation must be a real labeled input with warning description and keyboard confirmation.
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/review-accessibility.md#freshness stale-to-fresh transition] - freshness indicator text must be a polite live region.
- [Source: src/Hexalith.Parties.UI/Status/StatusPresentation.cs] - single source for status/freshness mapping and live-region attributes.
- [Source: src/Hexalith.Parties.UI/Components/Shared/StatusLiveRegion.razor] - existing live-region primitive and semantics.
- [Source: src/Hexalith.Parties.Contracts/Models/ProjectionFreshnessStatus.cs] and [Source: src/Hexalith.Parties.Contracts/Models/ProjectionFreshnessMetadata.cs] - freshness enum and metadata shape.
- [Source: src/Hexalith.Parties.Contracts/Models/PartyDetail.cs] and [Source: src/Hexalith.Parties.Contracts/Models/PartyIndexEntry.cs] - party state fields available to detail and index/list surfaces.
- [Source: src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor#StatusLabel/DetailStatus] - existing local mapping to replace later, preserving list/detail restricted-state difference.
- [Source: _bmad-output/implementation-artifacts/1-7-live-freshness-via-signalr-shared-optimistic-reconcile-effect.md#Previous Story Intelligence] - Story 1.7 shipped shared freshness/reconcile services, UI test patterns, and warnings-as-errors gotchas.
- [Source: _bmad-output/project-context.md] - .NET 10, Central Package Management, FluentUI V5, bUnit/xUnit v3/Shouldly, PII hygiene, and build-gate rules.
- [Source: /home/administrator/.nuget/packages/microsoft.fluentui.aspnetcore.components/5.0.0-rc.3-26138.1/lib/net10.0/Microsoft.FluentUI.AspNetCore.Components.xml#BadgeAppearance] - installed Fluent UI V5 package metadata for `BadgeAppearance.Tint`, `BadgeColor`, and `ButtonAppearance.Outline`.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex - BMAD create-story workflow.
GPT-5 Codex - BMAD dev-story workflow.
GPT-5 Codex - BMAD story-automator-review workflow.

### Debug Log References

- 2026-06-10: Red phase confirmed with `dotnet build tests/Hexalith.Parties.UI.Tests -c Release -m:1` failing on missing Story 1.8 component/value-model types before implementation.
- 2026-06-10: `dotnet build src/Hexalith.Parties.UI -c Release -m:1` passed.
- 2026-06-10: `dotnet build tests/Hexalith.Parties.UI.Tests -c Release -m:1` passed.
- 2026-06-10: `dotnet test tests/Hexalith.Parties.UI.Tests -c Release --no-build --filter ...` could not run in the sandbox because the .NET test runner failed to create its named-pipe IPC server with `SocketException (13): Permission denied`.
- 2026-06-10: Direct xUnit executable run for new Story 1.8 classes passed: `tests/Hexalith.Parties.UI.Tests/bin/Release/net10.0/Hexalith.Parties.UI.Tests -class ...` reported 27 total, 0 failed.
- 2026-06-10: Direct xUnit executable run for the full affected UI test project passed: 235 total, 0 failed.
- 2026-06-10: `bash scripts/check-no-warning-override.sh` passed.
- 2026-06-10: Broader `pwsh scripts/test.ps1 -Lane unit -Configuration Release` could not provide a useful regression signal because restore/test runner execution failed in this restricted environment; direct unit executable follow-up stopped in `Contracts.Tests` package fixtures when `dotnet pack` attempted to reach `https://api.nuget.org/v3/index.json` and failed with NU1900 vulnerability-data access under warnings-as-errors.
- 2026-06-10: Additional direct unit follow-up passed `Contracts.Tests` with package fixtures excluded (80 total, 0 failed), then stopped in `Client.Tests` package fixtures for the same restricted-network `dotnet pack` / NU1900 NuGet vulnerability-data access failure.
- 2026-06-10: Review fix verification passed `dotnet build src/Hexalith.Parties.UI -c Release -m:1`.
- 2026-06-10: Review fix verification passed `dotnet build tests/Hexalith.Parties.UI.Tests -c Release -m:1`.
- 2026-06-10: Review fix verification passed direct Story 1.8 xUnit executable run: 28 total, 0 failed.
- 2026-06-10: Review fix verification passed direct full UI test executable run: 236 total, 0 failed.
- 2026-06-10: Review fix verification passed `bash scripts/check-no-warning-override.sh`.

### Completion Notes List

- Create-story context analysis completed for Story 1.8.
- Checklist self-review applied before finalization.
- Added the host-owned `PartyLifecycleState` value model with detail/list/boolean factories and the required erased/restricted/active precedence.
- Added `PartyStateBadge`, `DataFreshnessIndicator`, and `GdprDestructiveButton` shared components under `Hexalith.Parties.UI/Components/Shared`.
- Added bUnit coverage for party-state labels and precedence, freshness copy/live-region semantics, GDPR typed confirmation behavior, and token/color guardrails.
- Implemented token-based CSS for freshness dots and irreversible GDPR danger styling without hard-coded color literals.
- Code review fixed freshness live-region binding to reuse the canonical `StatusPresentation.LiveRegionAttributes(...)` contract.
- Code review fixed irreversible GDPR confirmation so an empty expected confirmation value cannot enable the destructive action.
- Code review changed filled danger-button foreground styling to the Fluent danger inverted token and added regression coverage.

### Senior Developer Review (AI)

**Outcome:** Approved after automatic fixes. No critical issues remain.

**Issues fixed:**

- [HIGH] `GdprDestructiveButton` could enable an irreversible action when a caller supplied both `ExpectedConfirmationValue` and `CurrentConfirmationValue` as empty strings. Fixed by requiring a non-blank expected confirmation value before ordinal matching can enable the button, with a regression test.
- [MEDIUM] `DataFreshnessIndicator` hard-coded `role="status"` / `aria-live="polite"` instead of using the same canonical live-region contract as `StatusLiveRegion`. Fixed by binding through `StatusPresentation.LiveRegionAttributes(LiveRegionPoliteness.Polite)` and updating the test to assert that contract.
- [LOW] The irreversible danger button used a neutral foreground token on a danger background. Fixed to use the Fluent danger inverted foreground token.

**Validation checklist:**

- [x] Story file loaded from `_bmad-output/implementation-artifacts/1-8-shared-domain-components-party-state-badge-freshness-indicator-gdpr-destructive-button.md`
- [x] Story Status verified as reviewable (`review`) before review and updated to `done`
- [x] Epic and Story IDs resolved (`1.8`)
- [x] Story Context located or warning recorded: story contains source discovery and previous-story context; no separate context file was present
- [x] Epic Tech Spec located or warning recorded: `_bmad-output/planning-artifacts/epics.md` and `_bmad-output/planning-artifacts/architecture.md`
- [x] Architecture/standards docs loaded: `_bmad-output/project-context.md`, architecture references, and story references
- [x] Tech stack detected and documented: .NET 10, Blazor, FluentUI V5, bUnit, xUnit v3, Shouldly
- [x] MCP doc search performed or web fallback: not needed for local code review; installed FluentUI package metadata/token usage was inspected locally
- [x] Acceptance Criteria cross-checked against implementation
- [x] File List reviewed and validated for completeness
- [x] Tests identified and mapped to ACs; gaps fixed
- [x] Code quality review performed on changed files
- [x] Security review performed on changed files and dependencies
- [x] Outcome decided: Approved after fixes
- [x] Review notes appended under "Senior Developer Review (AI)"
- [x] Change Log updated with review entry
- [x] Status updated according to settings
- [x] Sprint status synced
- [x] Story saved successfully

### File List

- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/implementation-artifacts/1-8-shared-domain-components-party-state-badge-freshness-indicator-gdpr-destructive-button.md`
- `src/Hexalith.Parties.UI/Components/Shared/PartyLifecycleState.cs`
- `src/Hexalith.Parties.UI/Components/Shared/PartyStateBadge.razor`
- `src/Hexalith.Parties.UI/Components/Shared/DataFreshnessIndicator.razor`
- `src/Hexalith.Parties.UI/Components/Shared/DataFreshnessIndicator.razor.css`
- `src/Hexalith.Parties.UI/Components/Shared/GdprDestructiveButton.razor`
- `src/Hexalith.Parties.UI/Components/Shared/GdprDestructiveButton.razor.css`
- `tests/Hexalith.Parties.UI.Tests/PartyStateBadgeTests.cs`
- `tests/Hexalith.Parties.UI.Tests/DataFreshnessIndicatorTests.cs`
- `tests/Hexalith.Parties.UI.Tests/GdprDestructiveButtonTests.cs`
- `tests/Hexalith.Parties.UI.Tests/SharedDomainComponentStyleTests.cs`

### Change Log

- 2026-06-10: Implemented shared domain UI primitives for party lifecycle state, projection freshness, and GDPR destructive confirmation with bUnit/token guard coverage; marked Story 1.8 ready for review.
- 2026-06-10: Story-automator review auto-fixed canonical live-region binding, empty typed-confirmation enablement, and danger foreground token usage; marked Story 1.8 done.
