# Parties UI Accessibility Contract

`parties-ui` targets WCAG 2.2 AA for consumer-facing surfaces. Automated bUnit and Playwright gates catch detectable regressions, but they are not a complete manual WCAG audit.

## Shell Primitives

- `MainLayout` exposes the first two keyboard tab stops as `Skip to content` and `Skip to navigation`.
- `Skip to content` targets `#parties-main-content`; `Skip to navigation` targets `#parties-app-navigation`.
- Both targets are stable, programmatically focusable with `tabindex="-1"`, and named with app-owned landmark semantics.
- Focus indicators use `--colorStrokeFocus2` in normal mode and system colors under `@media (forced-colors: active)`.
- App-owned transitions must honor `@media (prefers-reduced-motion: reduce)` without hiding state changes.

## Component Semantics

- Routine status, freshness, and processing updates are polite live regions.
- Validation, failure, and load errors are assertive live regions.
- Irreversible destructive actions require exact typed confirmation before the confirming action is enabled.
- Reversible destructive actions do not use the irreversible typed-confirmation pattern.

## Button Color Rule

Raw teal `#0097A7` is not valid for white text on filled buttons. Filled primary buttons must use Fluent primary button styling and bind through `--colorBrandBackground`; app-owned CSS must not set raw filled button background colors.
