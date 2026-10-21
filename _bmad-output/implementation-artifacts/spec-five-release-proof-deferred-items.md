---
title: 'Close five deferred release proof entries'
type: 'bugfix'
created: '2026-10-21'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** DW-106, DW-132, DW-151, DW-162 and DW-191 remain open despite committed release-source checks, removal of the Commitlint bypass, the downstream available-row gate correction, and coverage of all ten root source selections. Their exit evidence and regression protection need explicit qualification.

**Approach:** Verify the five exit conditions against current tracked source, strengthen executable release-source and non-consumed source-row rejection coverage, record reproducible evidence, and close the five ledger entries. Independently review, commit and push to main as requested. DW-132 and DW-151 are duplicate records of the same bypass gap; qualify both with the same evidence.

</frozen-after-approval>

## Implementation Notes

- Starting clean main: 8e9ddec689c6e144a527738e921e3161310179ec. The user requested a five-entry batch and push. This is small, reversible test/helper and ledger work, with no public API, production behavior, dependency selection, deployed data or release-policy change. No unresolved intent gaps; use oneshot.
- `tests/Hexalith.Parties.Ci.Tests/ReleaseSourcePreflightTests.cs` already executes the actual tracked release Bash with fake gh and GITHUB_OUTPUT. Add mixed-run cases proving a successful matching run suffices, but independently wrong runs cannot combine into exact-source proof. Keep missing-CI plus successful Commitlint rejection and the retired bypass cases.
- `tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs` already includes McpCli and Platform in CurrentReleaseGitlinks and CurrentSourceDescribes. Extract the current table identity check into a narrow test helper, retain real Git tree/checkout validation, and add adversarial missing, duplicated, wrong-identity and wrong-description rows for each non-consumed dependency. Bind describes to the identity cell rather than unrelated prose.
- `_bmad-output/implementation-artifacts/spec-8-8-client-mcp-apphost-build-and-deploy-cleanup.md` already retains the available-row gate. Verify its committing revision and run AvailableRowConsumersFailClosedOnMissingOrMismatchedIdentity with the identity-gap cases; preserve frozen story content and canonical sprint status.
- Existing implementation provenance: release harness last changed at 5d1701371273eb0d1ed9a3360c54e62254d11419; the current McpCli/Platform selections were refreshed at 4fbe30883eb18ec074530e49a42669b5600ba2af; available-row heading correction is committed at 890119f1b4ced37ccec34787c812f064199fb3ef. These are historical implementation facts, not fresh owner approval or parity evidence.
- Verify affected test projects individually in Debug, focused executable cases followed by complete affected assemblies. Record exact commands/results, repository guard and pinned commitlint under `tests/five-release-proof-deferred-items-2026-10-21/`. Keep every root gitlink and historical receipt unchanged.

- Implemented 14 source-row boundary cases for McpCli and Platform, using the same helper as the complete ten-root receipt check. Exact identity-cell comparison also rejects an expected describe placed only in unrelated prose. Three added release cases cover mixed valid runs, split proof across invalid runs, and the explicit false legacy bypass value. Both Debug builds pass with zero warnings/errors; the focused release source gate passes 31/31.
- Required Aspire baseline: `aspire start --apphost src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj --non-interactive --format Json` succeeded; `aspire describe` showed the security container unhealthy after an HTTPS handshake EOF, with services waiting on it. Stopped successfully through the same exact AppHost CLI target before builds. No topology-health or runtime qualification is claimed by this test-only work.

- Review corrections: the four new spacing cases first failed 4/18 against the pre-correction helper. Parse trimmed dependency cells so compact valid rows pass and differently spaced duplicates reject. Added two standalone real McpCli/Platform checks against the current matrix and actual Git receipts, reusing current-section selection with the combined gate. The mixed-run release fixture now places a completed failed exact-source run before the matching success.
- Existing policy authority for DW-132/DW-151 is the human selection recorded in `spec-align-parties-ci-cd-with-eventstore-tenants.md`, “Release-gate decision — 2026-10-07”: remove bypass and require full CI. This reconciliation records that prior decision rather than making a new policy decision.

## Review Triage Log

- Medium, patch (finding 1): exact-prefix matching missed differently spaced duplicate Markdown rows. Reproduced with two invalid duplicate cases and two valid compact controls; parse/trim dependency cells and retain exact identity-cell checking.
- Low, patch (finding 2): synthetic cases covered parsing, but real non-consumed checks could only run within the combined gate. Add standalone McpCli and Platform cases exercising actual current rows, committed Git identities, clean checkouts, no initialized nested submodules and Git describes.
- Low, patch (finding 3): the positive mixed-run fixture omitted a completed failed run for the same source before success. Add it ahead of the matching successful run to protect success selection from first-match shortcuts.
- Low, patch (finding 4): the spec named bypass-removal provenance without citing its existing human policy authority. Record the dated decision above and in the closure evidence; frozen intent is unchanged.

- Final verification after review corrections: complete service assembly 2,257/2,257 and CI assembly 249/249, zero failures/errors/skips/not-run. Both final Debug test-project builds have zero warnings/errors. Independent follow-up confirms all four findings addressed and no material introduced issue. All five selected ledger entries are done; no new deferred work. Pinned commitlint 21.2.2 passes the exact candidate with zero problems/warnings.
