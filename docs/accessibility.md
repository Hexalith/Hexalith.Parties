# Parties UI Accessibility Contract

`parties-ui` targets WCAG 2.2 AA for consumer-facing surfaces. Automated bUnit and Playwright gates catch detectable regressions, but they are not a complete manual WCAG audit.

## Shell Primitives

- `MainLayout` delegates shell accessibility primitives to `FrontComposerShell`; it does not add parallel skip links or landmarks.
- The first two keyboard tab stops are `Skip to content` and `Skip to navigation`, targeting FrontComposer's `#fc-main-content` and `#fc-nav` focus targets.
- `#fc-main-content` is the single main landmark and is labelled `Main content`; `FrontComposerNavigation` owns the single `Primary navigation` landmark.
- FrontComposer's focus indicators use `--colorStrokeFocus2` in normal mode and system colors under `@media (forced-colors: active)`.
- Shell skip links have no motion transition, so reduced-motion users receive the same immediate focus state.

## Component Semantics

- Routine status, freshness, and processing updates are polite live regions.
- Validation, failure, and load errors are assertive live regions.
- Irreversible destructive actions require exact typed confirmation before the confirming action is enabled.
- Reversible destructive actions do not use the irreversible typed-confirmation pattern.

## Button Color Rule

Raw teal `#0097A7` is not valid for white text on filled buttons. Filled primary buttons must use Fluent primary button styling and bind through `--colorBrandBackground`; app-owned CSS must not set raw filled button background colors.
