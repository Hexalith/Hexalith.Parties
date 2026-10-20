---
title: 'DW-76 restore required accessibility CI'
type: 'bugfix'
created: '2026-10-20'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The existing Playwright accessibility lane has no CI caller, so browser accessibility regressions do not fail CI or the exact-source CI proof required for release.

**Approach:** Restore the existing Chromium accessibility suite as an unconditional, blocking job in ci.yml. Install its locked dependencies and browser prerequisites, run the deterministic Test-environment UI fixture using Release/package mode in CI, and retain browser reports even on failure. Verify the workflow contract and run the browser lane. Commit and push to main as requested.

</frozen-after-approval>

## Implementation Notes

- Selected open DW-76 after completed DW-75. Not an epic story; no sprint-status mutation. Main is clean at 836c315269b196a49455aacf7534c48fba3cb834 after the user committed their existing edit. Read the Hexalith baseline/Git guidance, editor/git attributes, build/package configuration, tests/README.md, docs/architecture.md, docs/ci.md, docs/development-guide.md, and the architecture accessibility/build constraints.
- No unresolved intent gaps, new APIs, dependency upgrades, data changes, or irreversible implementation steps. The small footprint is ci.yml, Playwright server configuration, focused CI contract coverage, CI/development docs, this spec, ledger completion and verification evidence. The push is explicitly authorized; use oneshot.
- Delegated read-only investigation confirmed six existing accessibility tests and the explicit Test fixture need no distributed topology. Preserve the ready/interactive checks, all test assertions, axe severity rules, and existing baseline. New work does not implement deferred pixel-baseline, whole-shell axe, content-focus, or browser-server ownership enhancements.
- Add a sibling ui-a11y job using Ubuntu 24.04 and immutable action revisions already selected in the repository's Builds checkout. Initialize only root Builds/Commons dependencies needed by the package-mode UI graph. Install global.json SDK and Node 24; npm ci, typecheck, Chromium/system dependency installation, build the UI in Release/package mode with audit/warnings intact, then npm run test:a11y. Keep the job unconditional with bounded execution and upload reports on failure. Existing exact-source release proof then includes this job.
- Playwright CI runs the prebuilt Release UI; local runs use Debug/source mode. Keep the explicit Test environment, fixture flags, isolated loopback URL, and Playwright-owned child lifetime. Retain current general server-override behavior outside the CI job.
- Baseline package-mode Release UI build succeeds with zero warnings/errors. npm ci installs the unchanged lock with zero vulnerabilities. Verify npm typecheck, all six browser cases without skips, focused and complete CI test assembly, actionlint, build guard and whitespace. Obtain independent blind review, address actionable findings, retain exact verification results and pinned commitlint evidence before committing.

- Implemented the unconditional ui-a11y job, prebuilt CI Release host, local Debug host, browser reports, and CI/development/accessibility documentation. Existing browser checks and their scope remain intact.
- Final verification: package-mode UI and minimal Builds/Commons-only snapshot builds have zero warnings/errors; Node 24 npm ci/typecheck and six browser cases pass; local Debug/source six browser cases pass; four new CI contracts and the complete 188-test CI assembly pass without skips/errors/not-run cases. actionlint and warning/submodule guards pass. A local diagnostic restore artifact with audit disabled was detected by the security-floor test, then refreshed by the normal package build before the final passing sweep.
- Independent review's four findings are patched and independently confirmed. Trigger, host-mode, and artifact-condition mutations are rejected. No new deferred work. Evidence is in tests/dw-76-required-accessibility-ci-2026-10-20/validation.json; commitlint 21.2.2 validates the exact full commit message with zero problems/warnings.

## Review Triage Log

- Low, patch: docs/accessibility.md still said no workflow invoked Playwright. Updated the lane contract, report artifact, release-proof relationship and remaining coverage limits.
- Low, patch: workflow-only tests did not guard the CI host command or Test fixture. Added focused guards for prebuilt Release execution, isolation and both fixture opt-ins, plus a Debug-host mutation that fails as expected.
- Low, patch: the unconditional-job assertion did not check top-level path filters. Added current event/branch declarations and path-filter rejection; a push paths mutation fails as expected.
- Low, patch: artifact-step extraction extended through later steps. Bound extraction to the next step and assert its own always() condition, artifact name and retention. A success() upload followed by always() cleanup fails as expected.
