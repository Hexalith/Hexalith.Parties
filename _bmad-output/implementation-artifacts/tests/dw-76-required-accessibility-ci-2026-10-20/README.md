# DW-76 verification receipt

The required `ui-a11y` job runs the existing six Chromium accessibility tests
inside `ci.yml`. CI uses a prebuilt Release/package UI; local Playwright uses
Debug/source assets. No browser assertions or baselines were weakened.

`validation.json` records the commands and results: six browser cases pass
with Node 24 in CI mode, six pass locally, the four new CI contracts pass, and
the complete CI assembly passes 188/188 without skips. Three targeted mutations
are rejected. UI and test-project builds have zero warnings and errors;
actionlint and the build guard pass. The pinned commitlint output is retained.

An isolated snapshot of the unchanged UI source and only the root Builds and
Commons dependencies also builds in Release/package mode. It was assembled
using `git archive HEAD` for the owning repository and each dependency, without
nested submodule initialization. This checks the new job's minimal checkout.
Local browser runs use cached Chromium; fresh browser/system installation is
performed by the hosted job. Full hosted CI and runtime topology remain
separate evidence.

Independent review identified four corrections: stale accessibility docs,
missing host-configuration coverage, missing trigger/path-filter coverage, and
artifact-step extraction that admitted a later cleanup condition. All four
are patched and independently confirmed; nothing was deferred.

A local diagnostic run left generated UI restore metadata with audit disabled.
The security-floor test caught it; a normal Release/package build refreshed
those artifacts with audit enabled before the final passing assembly run.
