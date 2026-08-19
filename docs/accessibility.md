# Parties UI Accessibility Contract

`parties-ui` targets WCAG 2.2 AA for consumer-facing surfaces. Automated bUnit gates run on every CI build and catch detectable regressions in markup and styling. They are not a complete manual WCAG audit.

**Lane status (accurate as of 2026-08-19):** the Playwright accessibility suite in `tests/e2e` is **not** wired into any workflow — `.github/workflows/` has no Playwright step and `scripts/test.ps1` invokes no npm lane. It runs on demand via `npm --prefix tests/e2e run test:a11y`, and its results are recorded manually in `_bmad-output/implementation-artifacts/tests/test-summary.md`. Making it an always-on CI gate is a tracked open item (Epic 8 spine §7, invariant I12). Treat browser-observable guarantees below as verified-on-demand, not continuously enforced.

## Shell Primitives

- `MainLayout` delegates shell accessibility primitives to `FrontComposerShell`; it does not add parallel skip links or landmarks. It renders one app-owned wrapper element inside the shell's content slot so Blazor CSS isolation has something to scope the app's focus rules to — a layout that renders only a child component emits no scope attribute and its `::deep` rules silently match nothing.
- `Skip to content` and `Skip to navigation` are the shell's first two focusable descendants, in that order, targeting FrontComposer's `#fc-main-content` and `#fc-nav` focus targets. They are `<a href>` elements whose activation is handled by the shell's interactive `@onclick`, so focus movement on Enter requires interactive Blazor to have hydrated; the static-SSR fallback is plain fragment navigation.
- Caveat: after hydration the shell focuses the route heading, which advances the browser's sequential focus navigation point past the skip links. A keyboard user reaching the page through a client-side route change must Shift+Tab to get back to them. Tracked for the shell owners in `deferred-work.md` as `frontcomposer-skip-link-reachability-after-route-focus`.
- `#fc-main-content` is the single main landmark and is labelled `Main content` unless a page supplies its own name through `FcContentLabel`; `FrontComposerNavigation` owns the single `Primary navigation` landmark.
- Focus indicators come from two places. FrontComposer's skip links carry their own `--colorStrokeFocus2` outline plus a `@media (forced-colors: active)` override in `fc-shell.css`. Every other interactive control inside the content area is covered by the app-owned rules in `MainLayout.razor.css`, which declare the same token and forced-colors treatment.
- Shell skip links have no motion transition, so reduced-motion users receive the same immediate focus state. App-owned components inside the content area suppress animation and transition duration under `@media (prefers-reduced-motion: reduce)` while leaving state changes visible.

## Component Semantics

- Routine status, freshness, and processing updates are polite live regions.
- Validation, failure, and load errors are assertive live regions.
- Irreversible destructive actions require exact typed confirmation before the confirming action is enabled.
- Reversible destructive actions do not use the irreversible typed-confirmation pattern.

## Button Color Rule

Raw teal `#0097A7` is not valid for white text on filled buttons. Filled primary buttons must use Fluent primary button styling and bind through `--colorBrandBackground`; app-owned CSS must not set raw filled button background colors.
