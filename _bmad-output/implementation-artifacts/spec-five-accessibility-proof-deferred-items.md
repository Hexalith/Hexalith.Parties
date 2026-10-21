---
title: 'Close five deferred accessibility proof entries'
type: 'bugfix'
created: '2026-10-21'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** DW-111, DW-135 and DW-154 lack runtime proof of visible app-content focus, forced-colors styling and reduced-motion overrides. DW-136 and DW-155 record the same omission of navigation and skip links from the blocking axe scan.

**Approach:** Complete these five related ledger entries by strengthening the existing required accessibility browser lane. Measure real content controls against Fluent/system focus colors and nonzero motion controls, scan the complete rendered shell, and prove omitted-region defects are rejected. Record reproducible evidence, independently review, commit and push to main as requested.

</frozen-after-approval>

## Implementation Notes

- Starting clean main: 40f8574df8a1fe47e232ca70f5167f242339cce3. The user explicitly requested five deferred items and a push to main. Five entries cover two related verification gaps; no unresolved intent gaps or new production behavior/API/dependency selections. Small reversible browser-test and documentation changes qualify for oneshot.
- Investigation: `tests/e2e/specs/parties-accessibility.spec.ts` already owns the required Chromium specimen lane and keyboard traversal helpers. Its axe include restricts scanning to main; its media test measures only a truthy skip-link color. Reuse `expectNoBlockingAxeViolations` from `tests/e2e/helpers/a11y.ts`, keeping required landmarks and serious/critical failure policy.
- Production contract: `src/Hexalith.Parties.UI/Components/Layout/MainLayout.razor` renders `.parties-main-content`, which anchors isolated `::deep` styles. Its CSS supplies solid Fluent-token focus, forced-colors Highlight/Canvas/CanvasText and important reduced-motion overrides. Measure existing button and link after keyboard navigation; resolve expected tokens/system colors independently in the browser. Stimulate nonzero motion on the real content button and check ordinary values before requiring suppression under reduced motion. No production CSS change is expected.
- Given the hydrated specimen, when normal or forced-colors keyboard focus reaches content controls, then their outlines are visible, positive-width, solid, offset and match the expected theme/system color. Given nonzero animation/transition/scroll values, when reduced motion activates, then production computed overrides suppress motion and iteration count. Given a shell navigation or skip-link naming defect, when the real axe helper runs, then it rejects the defect outside main content.
- Update `docs/accessibility.md` to describe observed coverage without claiming complete manual WCAG, pixel baseline or Story 8.9/I13 parity approval. Keep historical frozen closure decisions unchanged. Close only the five selected ledger entries after actual browser proof passes.
- Verification: browser typecheck/config tests, complete `test:a11y` against the owned Debug fixture, UI test-project Debug build and complete assembly, repository guard, exact pinned commitlint. Record commands/results and bounded browser reports under `tests/five-accessibility-proof-deferred-items-2026-10-21/`. CI already runs the same browser suite against Release/package UI; no dependency source-mode or external topology parity claim follows from a local run.
- Required Aspire baseline: selected AppHost started and was described; Parties, UI, security and sidecars became healthy, while EventStore, EventStore admin and Tenants finished. Stopped the same exact AppHost successfully before builds. This is an observation, not full topology qualification.
- The first full-document axe run exposed a missing specimen document title (serious). Add the enabled specimen's `PageTitle` and await it with hydration, preserving the complete scan. The navigation mutation now targets the visible labelled navigation landmark because responsive shell markup contains more than one `#fc-nav`. Normal/forced focus and reduced-motion cases already pass against the actual Debug package assets.
- Qualification: Node 24 typecheck and 8/8 configuration cases pass. The complete browser suite passes 10 with the two pre-existing DW-91 package-adoption skips. UI Debug build has zero warnings/errors; full UI assembly passes 387/387 without skips. Both controlled mutations fail exactly as expected: removing the content scope class fails all three focus/motion cases; restoring the main-only axe include fails both omitted-region regressions. Source bytes are restored after each experiment. All five ledger entries now reference the shared evidence receipt; other ledger content is preserved.
- Review corrections strengthen the same verification scope: contrast is measured against composited ancestor fills, with a 3:1 minimum and a solid-background precondition; actual paint visibility includes opacity and controls require full viewport intersection. Each original skip link receives its own exposed positive scan. Focus JSON precedes style assertions; motion JSON is retained through assertion failure. Motion covers button and link, accepting durations from zero through 10ms and at most one repetition. The browser receipt records its actual browser/Node version, managed fixture environment and served stylesheet hashes.

## Review Triage Log

- Medium, patch: comparing with a transparent link's own fill accepted white-on-white surrounding focus. Composite ancestor backgrounds and outline alpha, then assert at least 3:1 contrast; reject gradient backdrops from this computed proof.
- Medium, patch: style/viewport assertions alone accepted zero-opacity controls. Add the browser's opacity-aware paint visibility check and require full control intersection with the viewport.
- Medium, patch: offscreen skip links did not receive positive contrast checks. Scan the unchanged document again with each original link visibly focused.
- Low, patch: failing style checks lost computed JSON. Write focus measurements before assertions and motion observations in `finally` after polling.
- Low, patch: exact scientific-notation duration strings rejected accessible zero/short alternatives. Assert bounded nonnegative durations, finite bounded repetition and automatic scrolling instead.
- Medium, patch: button-only motion stimulus missed a narrowed production selector affecting links. Exercise both existing button and link controls.
- Low, patch: the receipt lacked fixture/served asset attribution. Record actual managed/external mode, allowlisted fixture variables, runtime versions and served stylesheet hashes; retain evaluated package and built UI identity separately.
- Follow-up to the visibility finding: full control intersection excludes the outside ring. Expand the control bounds by the actual outline width plus offset and reject clipping by the viewport or overflow/paint-containing ancestors. Qualify an exact-fit overflow-hidden parent as a failing control.
- Final qualification after all patches: the unmodified Node 24 browser suite passes 10 with only the two existing DW-91 skips, and typecheck passes. The clipping control uses 2px clearance so the whole button remains visible while its 6px outside ring is clipped; both focus cases fail specifically at the new ring assertion. Independent follow-up verifies the correction and reports no remaining material finding. Earlier complete UI 387/387 and configuration 8/8 results remain applicable; no production change followed them. Exact pinned commitlint passes with zero problems/warnings. No new deferred work.
