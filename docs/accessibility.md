# Parties UI Accessibility Contract

`parties-ui` targets WCAG 2.2 AA for consumer-facing surfaces. Automated bUnit gates run on every CI build and catch detectable regressions in markup and styling. They are not a complete manual WCAG audit.

**Lane status (2026-10-20):** the blocking `ui-a11y` job in `.github/workflows/ci.yml` runs `npm run test:a11y` from `tests/e2e` on every CI trigger. It runs all six existing Chromium accessibility cases against the Release/package-mode UI in the explicit Test environment. Reports and available failure diagnostics are retained in the `ui-a11y-results` artifact for 14 days, including after failures. A failed lane fails CI and prevents successful exact-source CI proof for release. See [CI details](ci.md#ci) and [local reproduction](development-guide.md#4-test); `scripts/test.ps1` continues to run only .NET lanes.

The browser suite continuously checks its existing specimen scope: blocking axe violations in main content, seeded shell skip-link focus, representative keyboard traversal, media settings, button color, and the semantic visual baseline. Whole-shell axe scans, app-content focus/media behavior and pixel-diff baselines remain separate deferred work. The automation does not establish every browser guarantee below or replace a manual WCAG audit.

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
