---
title: 'DW-91: Restore forward skip access after route focus'
type: 'bugfix'
created: '2026-10-21'
status: 'in-progress'
route: 'oneshot'
review_loop_iteration: 0
baseline_commit: '457031c91fea0857321399722b5a89aeb7ce64b3'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** DW-91 records that automatic route-heading focus advances sequential keyboard navigation past FrontComposer's skip links. Parties' existing browser test hides that behavior by manually focusing the shell root.

**Approach:** Keep heading focus for the route announcement and make the next unmodified forward Tab reach the shell's Skip to content link. Implement this in FrontComposer's existing shell keyboard integration so it supports both its own router and Parties' Blazor FocusOnNavigate. Preserve normal reverse navigation, control and dialog focus, and explicit pointer interaction. Add producer and Parties browser regressions covering cold load, client navigation, skip targets and onward keyboard flow. Close DW-91 only after independent review and verification, then commit and push both owning repositories on main.

</frozen-after-approval>

## Implementation Notes

- Selected DW-91 after completed DW-90. Its recorded 2026-09-06 decision authorizes the producer fix and Parties coverage. Freeform work has no sprint story key.
- Small change to the existing JavaScript keyboard handler and browser tests; no public API, dependency-version change, intent gap or irreversible data operation. The oneshot route applies.
- Code map: FrontComposer `src/Hexalith.FrontComposer.Shell/wwwroot/js/fc-keyboard.js`, `registerShellKeyFilter` already owns key/focus listeners with symmetric teardown. Keep route-heading focus in `fc-focus.js` and Parties `Components/Routes.razor` intact. Add producer browser coverage under `tests/e2e/specs`; replace the root-focus workaround in Parties `tests/e2e/specs/parties-accessibility.spec.ts`.
- Verify actual keyboard behavior with Playwright, producer regression checks, TypeScript checking and affected builds. Record exact commands/results under `tests/dw-91-forward-skip-links-2026-10-21/` without credentials or historical evidence recertification.
- Startup baseline: isolated no-build Aspire start succeeded; `aspire wait parties` reported healthy, `aspire describe` showed healthy Parties/UI/EventStore resources, and the exact AppHost stopped successfully before edits/builds. This is startup evidence only.
- Replanning trigger: the source fix and browser regressions pass, but Parties CI builds Release against published FrontComposer 4.7.0. That immutable package lacks this new fix. Pushing the new unconditional consumer assertions would fail the required package-mode gate. FrontComposer publication is operator-controlled (`.github/workflows/release.yml`, workflow_dispatch), and adoption would also change the Builds package catalog. This exceeds the planned source/coverage footprint; no release has been dispatched and no package versions have changed.
- Parent main advanced concurrently to `8c88c27acd5ed3ee2d2f2b6f51f3432d536f2021`, restoring approved dependency selections. Fast-forwarded to it, preserving source edits. The producer patch is on `fix/dw-91-forward-skip-links` based on the selected `913c4132c71cd7f77fcdf6befa9e396d284dd4bc`; this avoids importing unrelated producer history into the Parties gitlink. No other dependency selection is included in this work.
- Independent Blind Hunter review found one pending-state bug and three coverage gaps, all patched: modifier/non-focus keys preserve pending forward access; reverse traversal compares against the browser's native destination; onward traversal reaches the expected content control; teardown/re-registration and non-content heading isolation have browser regressions.
- Verification so far: 90 producer route/browser cases pass across Chromium, Firefox and WebKit; all 7 Parties accessibility cases pass against the source asset; both TypeScript checks pass. Both running hosts serve byte-identical candidate keyboard assets. Release/package adoption is not qualified by this source evidence.

## Code Map

- FrontComposer `src/Hexalith.FrontComposer.Shell/wwwroot/js/fc-keyboard.js`: existing registration/teardown owns the small focus/Tab fix; no public API changes.
- FrontComposer `tests/e2e/specs/route-skip-links.spec.ts`: ten producer regressions; existing `route-contract.spec.ts` supplies twenty broader route regressions per engine.
- Parties `tests/e2e/specs/parties-accessibility.spec.ts`: implemented source consumer regressions currently require the unpublished fix; retain the existing package-mode coverage unless coordinated publication is selected.
- Parties `.github/workflows/ci.yml`, `ui-a11y`: mandatory Release build with published packages, no FrontComposer source checkout.
- FrontComposer `.github/workflows/release.yml`: operator-dispatched production release with exact-source CI and owner gates. Do not trigger as an incidental Git push action.
- Builds `Props/Directory.Packages.props`: current FrontComposer version 4.7.0 governs Parties package consumers; any adoption needs a real published successor and its own verified catalog change.

## Review Triage Log

- Medium, patched: pending heading was consumed by unrelated keys. Shift, ArrowDown and Escape now retain forward access while heading focus remains unchanged; regressions pass in all three engines.
- Medium, patched: reverse traversal assertions were too weak. The test now compares with the same browser's native destination and checks the event is not prevented.
- Medium, patched: onward traversal allowed any non-skip destination. Producer asserts the Overview tab, and Parties asserts its primary control after both cold-load and client-route skip activation.
- Medium, patched: lifecycle and shell scoping lacked regression evidence. Two unregister/re-register cycles and an unrelated banner heading are covered in all three engines.

## Resume Implementation Notes

- User confirmed resuming existing DW-91 on 2026-10-21. The requested main push authorizes delivering the producer fix and source regressions; it does not dispatch the protected owner-controlled package release. Publication/adoption remains a ledger follow-up, and DW-91 must stay open until package CI can enforce the forward-Tab assertions unconditionally.
- During investigation the user's concurrent `0c9572bc2cc7a476343f11a488b9311ebbbcb8c9` committed the starting changes. Preserve that commit and all dependency selections. The next commit repairs package-mode coverage with the original always-on seeded-root test and two explicit `PARTIES_E2E_FORWARD_SKIP_LINKS=1` source regressions; no behavior detection or automatic skip on failure.
- Footprint stays small: existing producer keyboard module, producer browser regression file, Parties accessibility tests and test instructions/evidence. No public API, release dispatch, catalog edit, data mutation or unrelated gitlink update.
- Producer remote main advanced to `6a6f06fd02f2acb72d00eb4971a639b0e366ee1e`; neither touched source path changed. Keep the fix commit directly based on selected `913c4132c71cd7f77fcdf6befa9e396d284dd4bc`, merge it into producer main, then select only the focused source commit in Parties.
