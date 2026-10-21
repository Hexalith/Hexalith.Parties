# Five deferred accessibility proof entries — 2026-10-21

DW-111, DW-135 and DW-154 share the missing app-content focus/media proof.
DW-136 and DW-155 share the missing navigation/skip-link axe scope. This
receipt closes five ledger entries covering those two verification gaps.

The actual Chromium suite passes **10 cases**, with the **two existing DW-91
forward-skip cases skipped** pending FrontComposer package adoption. The
suite runs against the real Debug UI, with published dependencies, through
its owned Test fixture. It changes no served asset or dependency selection.
The normal CI job runs this same suite against its Release/package UI;
this receipt does not claim a new successful remote CI run or local Release run.

`checks.json` records exact commands, working directories and exit codes.
Node 24.22.0 runs browser typecheck, configuration checks (8/8), and the browser
suite. The UI test-project Debug build has zero warnings/errors; its complete
assembly passes 387/387 with no skips. `ui-test-summary.json` preserves the
assembly attributes from the full xUnit XML report without its 387 success
records. The repository warning/submodule guard also passes.

`content-focus-normal.json` and `content-focus-forced-colors.json` record real
keyboard-focused content button/link styles: solid 2px outlines with 4px offset,
matching Fluent tokens or resolved system Highlight. Forced-colors backgrounds
and text match Canvas/CanvasText. Composited surrounding fills must give at least
3:1 outline contrast, and opacity-aware visibility plus full control viewport
intersection must hold. This computed contrast check requires solid ancestor
backgrounds. The full outline bounds, including width and offset, must also fit
inside the viewport and clipping ancestors. `content-motion.json` records nonzero ordinary settings on both the
button and link, followed by production reduced-motion overrides of at most
10ms, at most one repetition and automatic scrolling. No reduced-motion CSS is
supplied by the delivered test. Computed JSON survives style assertion failures.

`axe-parties-accessibility.json` has no blocking, report-only or unknown
violations across the complete specimen document. The two `axe-rejected-*.json`
controls prove the shared gate rejects a critical navigation button-name defect
and a serious exposed skip-link name defect outside main content. The complete
scan also exposed the specimen's missing document title, now supplied by its
enabled `PageTitle` and explicitly awaited by browser readiness. Two additional
positive axe reports scan the document with each original skip link exposed,
so their visible contrast is covered.

`mutation-checks.json` records controlled regression experiments:

- Removing the real content wrapper's scope class at runtime fails all three
  normal-focus, forced-colors and reduced-motion cases.
- Restoring the old main-only include in the shared axe gate fails both
  omitted-region regression cases, because the gate incorrectly accepts them.
- Zero-opacity content fails both focus cases. White-on-white focus fails the
  normal-color contrast check. An exposed low-contrast skip link fails axe.
- Moving only the link out of the scoped wrapper fails reduced-motion proof.
- An overflow-hidden parent tightly fitted around the button fails both focus
  cases because it clips the outside ring despite exposing the whole button.
- A zero-duration/repetition alternative passes, proving the motion check
  accepts accessible alternatives to the original scientific-notation value.

Each experiment restores the browser source bytes in a `finally` block. These
expected failures demonstrate sensitivity to the original gaps, not failures
of the delivered test suite. Per-check source hashes distinguish the initial
seven controls from the added clipping guard; the final unmodified suite runs
after all corrections.

`browser-identity.json` records the actual browser/Node versions, selected URL,
managed/external server mode, allowlisted fixture variables and SHA-256 hashes
of the three served accessibility stylesheets. `ui-identity.json` separately
records the built Debug UI and evaluated dependency catalog identities. These
observations attribute this local run; they do not renew owner approvals.

The required Aspire baseline started and was inspected, then stopped through
the same selected AppHost. Parties, UI, security and sidecars became healthy;
EventStore, EventStore admin and Tenants finished. Full topology qualification,
manual WCAG certification, pixel regression baselines and historical Story
8.9/I13 approvals remain outside this receipt.
