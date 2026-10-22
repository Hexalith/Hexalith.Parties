# Five runtime and release deferrals — 2026-10-22

This batch qualifies DW-105 and the four selected follow-ups from
`spec-five-query-release-deferred-items.md`. Commands and exit codes are paired
in each check's `.json` and `.txt` files.
Tracked output is labeled as complete, path-redacted or excerpted; full raw
logs remain in `/tmp/parties-five-runtime-release-raw`, with hashes and paths
in `raw-log-index.json`. `validation.json` indexes all checks.
`source-inputs.json` records source/binary hashes.
The implementation started at `9a46682ad170ba70e395aa00012f86f09ef6b315`.

## Current evidence

- Fresh Debug builds of the CI, service and UI projects pass. Source development
  remains blocked: the pinned module tool reports `HXP020` for the unavailable
  mapped Parties project. It explicitly starts no resources. These Debug builds
  and the authoritative Release fallback use published packages; they do not
  establish source-mode qualification.
- Release builds pass with warnings as errors and the normal NuGet audit policy.
  Focused historical-query coverage passes 310/310; the full service assembly
  passes 2464/2464, without skips.
- The tracked release shell executes against isolated fake GitHub API responses.
  Focused CI coverage passes 170/170. Source admission needs independent active
  workflow identities and completed successful exact-SHA main-push runs for both
  CI and RC range signoff. Publication considers every unique canonical stable
  version and rejects ambiguity in either release list order.
- Each credential-bearing job is extracted independently. Removing only the
  publication environment fails the fitness assertion while release remains
  protected; the reciprocal mutation fails too. This tests local declarations,
  not GitHub approval or branch-policy enforcement.
- The CI assembly passes 427/427 with no skips; its full final result is
  retained in `ci-final-tests.txt`. Its first full run exposed missing restore/build inputs in the environment; the
  normal solution restore and non-RID Release UI build supply those prerequisites.
- Actionlint, warning-override guard and CRLF-aware whitespace checks pass.

## Fresh local UI startup

`ui-container-publish.json` records an SDK `Rebuild,PublishContainer`, Release,
package mode, `linux-x64`, framework-dependent archive publication. No registry
push occurs. Existing UI sources and package selections are unchanged from the
checkout SHA; the image source label names that checkout. `0.0.0-local` and the
supplied timestamp are local provenance fixture values, not a published release.

The archive SHA-256 is
`d478e22e1cdf206054ae747d25048995884825ce0984fea05420343ea8e93119`.
The loaded image ID is
`sha256:e36065ef6aeb2f8d8fa1f8244f1d6eebc06f04f9c837dc9b4eb077b14b7cbf80`.
The full image/base identities and labels are in
`ui-container-qualification.json`; the archive remains local under `/tmp`.

`qualify-local-ui.py` is a one-off batch receipt driver. It imports the existing
Builds bounded startup, polling, HTTP and cleanup helpers, records every actual
Docker/curl command and result, and never creates a second general smoke runner.
The exact command is in `review-ui-optimized-final.json`.

The shared Development environment starts the image successfully, then `/alive`,
`/health`, `/ready` and
`/_content/Hexalith.Parties.AdminPortal/Hexalith.Parties.AdminPortal.bundle.scp.css`
all return HTTP 200. Final process state is `running|0`. Only the newly loaded
local image and unique batch-owned container are removed; both cleanup checks
pass. Other images and containers are untouched.

This proves current local `linux/amd64` startup and static assets. It does not
repair historical v1.1.1 assets, qualify arm64, execute production authentication,
publish a release or replace the shared release smoke/artifact verification.

## Retained initial failures

`identity-debug-tests.txt` retains the first query run. Its 26 failures were
metadata assertions that assumed parsing had completed before setup/capacity
rejection. The revised assertions require empty metadata for unparsed requests;
later failures keep the original request metadata checks. Fresh Debug and
Release runs pass.

`ui-container-runtime.txt` retains an initial receipt-driver import error. Adding
the shared helper directory to Python's module search path resolves it; the
subsequent and final startup/HTTP/cleanup checks pass.

`ci-release-focused-tests.txt` retains one fake-gh interpreter error. Shouldly
truncated its suffix, so the original untruncated OS diagnostic is unavailable.
The focused retry, full initial CI run and final focused run do not reproduce
it. Source-fixture assertion failures now include complete stderr. No fixture
retry or permissive result handling was added.

DW-107, DW-126, DW-116 source qualification, externally evaluated workflow
scheduling and unrelated/upstream-dependent ledger entries remain open. Parent
workflow review and authorized commit/push evidence are recorded separately.

## Evidence-driver audit repair

The duplicate CI receipt was repaired after preserving its wrapped raw output,
nested tracked receipt and prior index in the directory recorded under
`raw-log-index.json` → `repairs`. The recovered xUnit output exactly matches
its original recorded SHA-256
`16d007f86831b2669b7ec79c42f47f19cac3763cd93e62786f764927e2a4868c`.
There is now one index row and one receipt header per log. All indexed raw hashes
and tracked header hashes pass the audit in `evidence-repair-check.json`.

The UI driver now marks the startup attempt before invoking the shared start
helper and removes its unique, previously absent container name in `finally`,
including when startup fails or times out. The narrow rerun in
`ui-container-runtime-cleanup.txt` passes all four HTTP requests, final running
state and owned container/image cleanup. Earlier qualification bytes remain
preserved with the audit snapshots. No runtime, workflow, test or spec changes
are part of this repair.

## Focused review fixes

The driver now uses explicit acceptance checks under normal Python and `-O`.
It atomically installs this attempt's failure/phase receipt before setup, accepts
only explicit Docker missing-resource responses during preflight, and tracks
load/start attempts for reconciliation. This one-off qualification pins the
archive and image identities recorded above. Startup and unforced, non-pruning
image removal use the immutable image ID. Cleanup verifies the only tag/digest
bindings belong to this batch and refuses rebound or uncertain resources.

`review-fix-check.json` records 17 isolated driver cases, including optimized
404/exited rejection, pre-existing/uncertain resources, failed/timed-out import,
failed startup, stale receipt replacement and foreign tag/digest refusal. The
actual `python3 -O` startup/HTTP/cleanup rerun passes. Its first real attempt
refused the daemon-added batch digest; that failure and guarded cleanup are
retained, and the final check admits only the exact owned alias.

The new historical recovery loop cancels itself after three seconds and preserves
test cancellation; its focused parsing cases pass 10/10. Pending RC evidence now
uses `queued` while running remains `in_progress`; the source-preflight class
passes 63/63. XML docs describe empty/default metadata before parsing without
changing the contract. These are focused checks; broader verification belongs
to the parent workflow.

Final staged whitespace audit removes trailing spaces and terminal blank lines from tracked log views. Their headers identify whitespace normalization; raw logs and their recorded hashes remain unchanged.
