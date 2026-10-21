# Parties UI Accessibility Contract

`parties-ui` targets WCAG 2.2 AA for consumer-facing surfaces. Automated bUnit gates run on every CI build and catch detectable regressions in markup and styling. They are not a complete manual WCAG audit.

**Lane status (2026-10-21):** the blocking `ui-a11y` job in `.github/workflows/ci.yml` runs `npm run test:ui` from `tests/e2e` on every CI trigger against the Release/package-mode UI in the explicit Test environment. This single invocation includes the accessibility specimen, admin list/forms/GDPR, consumer routes and picker, including contact dropdown selection and the destructive dialog's shadow-root warning/description. Reports and available failure diagnostics are retained in the `ui-a11y-results` artifact for 14 days, including after failures. A failed lane fails CI and prevents successful exact-source CI proof for release. See [CI details](ci.md#ci) and [local reproduction](development-guide.md#4-test); `scripts/test.ps1` continues to run only .NET lanes.

The browser suite scans the complete rendered specimen document, including shell navigation and skip links, for blocking axe violations. Deliberate inaccessible navigation and skip-link controls must fail the same gate. Keyboard-focused content buttons and links must have a visible solid outline matching Fluent tokens in normal colors and system Highlight in forced colors. Reduced-motion checks first establish nonzero motion settings on a real content control, then require the production stylesheet to suppress animation/transition durations, repetition and smooth scrolling; media emulation alone cannot pass. Computed styles are attached to the browser report.

Seeded shell skip-link focus, representative keyboard traversal, button color, semantic JSON and an asserted Chromium/Linux PNG baseline remain covered. Two forward-skip tests remain opt-in pending DW-91 package adoption; see [test instructions](../tests/README.md#forward-skip-link-source-qualification-dw-91). The automation does not establish every browser guarantee below or replace a manual WCAG audit. The [five-entry evidence receipt](../_bmad-output/implementation-artifacts/tests/five-accessibility-proof-deferred-items-2026-10-21/README.md) records earlier local qualification; historical Story 8.9/I13 approval decisions retain their original scope.

Run `npm run test:visual` in `tests/e2e` to compare the deterministic 1280×900 specimen with `specs/parties-accessibility.spec.ts-snapshots/parties-accessibility-shell-chromium-linux.png`. Fonts finish loading, the server-generated copyright year is normalized to 2000, and animations/carets are suppressed before comparison. Run `npm run test:visual:update` on Ubuntu 24.04 with the locked Chromium installation to regenerate that asserted image, then inspect the PNG diff before accepting it. Playwright writes expected/actual/diff images on failure. The separate `baselines/parties-accessibility-shell.visual.json` remains a semantic contract edited deliberately; the image update command does not rewrite it.

The semantic baseline runs on every platform. The separate pixel comparison is skipped on Windows/macOS; use Ubuntu 24.04 (including a suitable Linux environment) for pixel qualification. Regenerate the CI baseline with the same Release/package-mode host, from `tests/e2e`:

```bash
dotnet build ../../src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj --configuration Release -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true
CI=true BASE_URL=http://127.0.0.1:5072 PLAYWRIGHT_SKIP_WEBSERVER= npm run test:visual:update
```

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
