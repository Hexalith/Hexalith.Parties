---
title: 'Fix current CI evidence and publish the Parties release'
type: 'bugfix'
created: '2026-10-13'
status: 'done'
route: 'dispatch'
baseline_commit: '1e64c3e1bc37bb1cdfb0c4f1d7e2f174222984c9'
review_loop_iteration: 0
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-git-instructions.md'
  - '{project-root}/docs/ci.md'
  - '{project-root}/docs/build-gate.md'
---

<frozen-after-approval reason="user explicitly authorizes fixing CI, release publication, and bypass of human authorization">

## Intent

**Problem:** CI run 38399980224 builds successfully but two platform fitness checks fail after already committed root dependency updates. Current-source receipt constants still select October 11 gitlinks, and the FrontComposer evidence command searches for a literal polite live region superseded by conditional speech suppression. Release also requires source-selection ledger entries for six already committed pointers.

**Approach:** Refresh the current source observation and its matching exact fitness expectations, preserve dated historical receipts, and make the structural accessibility command reproduce the selected source. Record the existing root selections under the user's release authorization. Produce a locally verified, independently reviewed patch; the parent then commits, pushes, waits for successful full CI for the exact main SHA, dispatches Release, approves the existing production review, and verifies all nine exact package versions on nuget.org.

## Boundaries & Constraints

**Always:** Keep exact committed-gitlink, matching clean checkout, no nested submodule, package graph, warning, publication collision, and historical migration gates active. Keep current observation distinct from earlier approvals and parity evidence. Preserve package versions and current gitlinks. Maintain CRLF in edited C# and ledger files. Human approval checkpoints are already waived by the user, while automated checks remain required.

**Never:** Update dependencies, edit source under references/, modify production behavior, delete rollback paths, weaken test expectations to conceal runtime defects, change release protection, or claim new accessibility or G5 parity from static source inspection. The implementation subagent must not invoke bmad-build, commit, push, dispatch, or change remote state. External publication belongs to the parent after local implementation and review.

</frozen-after-approval>

## Code Map

- `tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs`: `CurrentReleaseGitlinks` and `CurrentSourceDescribes` hold current observation expectations, distinct from historical constants. `FinalDependencyReceiptsMatchTheSelectedPackageAndSourceGraph` checks the dated current section plus live gitlinks, clean checkouts, catalog, and evaluated project graphs. `Matrix_ValidationEvidenceCommandsAreReproducible` extracts fixed-string commands from the marked matrix and searches exact source files.
- `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md`: insert a new October 13 current section, demote October 11 post-sync to historical without changing its contents, and refresh only the live FcSurfaceStatus command and its explanation. Keep migration row statuses and historical acceptance unchanged.
- Selected graph at the baseline: Builds `b2f59c59d16fc2234af6a0f850297958c6143e0d` (`v4.30.1-35-gb2f59c59`); EventStore `2a7b80a4ec639bdba7ff8acc74cacb4f8880b24c` (`v3.120.0-75-g2a7b80a4`); FrontComposer `c3174dc917b094f3736b098ac78a531f197dbe56` (`v4.6.0-25-gc3174dc9`); Memories `24b1fa7408eabc1db24b7536fd35f575c7a572a7` (`v2.28.1-88-g24b1fa74`); Tenants `ed7cef6e327156628c0479071f3bf419fd64612c` (`v5.8.0-63-ged7cef6e`); Platform `be294641fe06490b7f1d9788f383512088842e7d`. Other four root pointers match the earlier table. Catalog versions remain unchanged.
- `references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Components/Rendering/FcSurfaceStatus.razor`: shared status markup uses `aria-live="@(_suppressSpeech ? "off" : "polite")"`; inspect this ordinary source, do not edit it. The full expression makes a stronger structural check than searching only the word polite.
- `.gitlink-signoff.tsv` and `scripts/gitlink-rc-gate.sh`: six selected committed pointers above lack release selection entries versus v1.2.1. Append rows owned by authenticated release account jpiquot dated October 13, grounded in this session's explicit release instruction; this certifies selection only.
- `tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj` is the service integration test project; its built xUnit v3 executable supports `-class` and `-method`. Repository test instructions prohibit solution-level dotnet test.
- `.github/workflows/release.yml` requires successful exact-source push CI, registry floor, existing production approval, and automated package/container validation. `tools/release-packages.json` declares nine packages. Parent operates this unchanged workflow.

## Tasks & Acceptance

**Execution:**
- [x] `PlatformApiPrerequisitesTests.cs` -- refresh only current release SHA/describe expectations and current/historical heading boundaries for the new October 13 observation.
- [x] `story-8-3-platform-api-prerequisite-matrix.md` -- add accurate current table, retain former current table as dated history, update shared-status structural evidence and explain conditional silent/polite rendering without claiming runtime parity.
- [x] `.gitlink-signoff.tsv` -- append the six exact existing committed selections, documenting the user's release authorization without broad parity certification.
- [x] Affected fitness test class and release gitlink scripts -- run focused checks and record exact results in Implementation Notes. Full test-project verification may be completed by the parent after handoff.

**Acceptance Criteria:**
- Given the unchanged baseline root graph, when final dependency receipt validation runs, then committed gitlinks, clean checkouts, catalog versions and evaluated source/package graphs all match the new current observation.
- Given the selected FrontComposer source, when matrix structural commands run, then every positive and negative command produces its declared result and G5/rollback gates retain their prior expectations.
- Given user-authorized source selections, when both root gitlink gate modes run against v1.2.1, then they pass without moving dependencies or changing historical signoffs.
- Given the implementation diff, when independent reviewers inspect it, then no unresolved regression remains before the parent publishes it.

## Implementation Notes

Implemented the current October 13 source observation at unchanged baseline
Parties `1e64c3e1bc37bb1cdfb0c4f1d7e2f174222984c9`. Updated only current
SHA/describe expectations and the current/historical section boundary in the
fitness class. Added the new current matrix table and the full conditional
`aria-live="@(_suppressSpeech ? "off" : "polite")"` structural command, with an
explicit source-inspection limitation. Appended the six existing committed
selections under jpiquot's session release authorization; selection does not
certify parity or discharge migration/rollback gates.

Focused verification completed successfully on 2026-10-13:

- `dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release -m:1` -- exit 0; 0 warnings, 0 errors; package graph unchanged.
- `dotnet tests/Hexalith.Parties.Tests/bin/Release/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.PlatformApiPrerequisitesTests` -- exit 0; 33 total, 33 passed, 0 errors, 0 failed, 0 skipped, 0 not run.
- `bash scripts/gitlink-rc-gate.sh --worktree` -- exit 0; all root gitlinks clean or validated.
- `bash scripts/gitlink-rc-gate.sh --diff v1.2.1` -- exit 0; all eight changed root pointers have matching selection entries.
- `bash scripts/check-no-warning-override.sh` -- exit 0; no warning-override or nested-submodule regressions.
- `git -c core.whitespace=cr-at-eol diff --check` -- exit 0; no whitespace errors.
- A byte comparison against `git show HEAD:<path>` confirmed that the former current matrix and all historical content are unchanged except its demoted heading and the one live structural command; previous ledger bytes remain an exact prefix. C# and ledger files retain CRLF.

All ten root committed gitlinks have matching clean checkouts. No dependency,
package version, production source, release workflow, or remote state was
changed. Independent review, full affected-project validation, exact-source
push CI, release dispatch/production approval, and public package verification
remain with the parent.


Parent verification: `dotnet test --project tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --no-build --results-directory /tmp/parties-ci-38399980224-local-tests --report-xunit-trx --report-xunit-trx-filename parties-tests.trx` completed with exit 0: 924 total, 924 passed, zero failed/skipped. The TRX and summary remain in the system temporary directory.

After that validation and the review diff snapshot, a concurrent workspace session advanced the Builds checkout to `8f014f180a0ba99615cbd8942118bc66d2de2b7b`. This task preserves that external checkout and excludes its gitlink drift from staging and publication; the root-recorded release graph remains `b2f59c59d16fc2234af6a0f850297958c6143e0d`. Exact-source GitHub CI will validate the committed graph in a clean checkout.


Review patch verification: the current observation now explicitly separates the imported Builds catalog from release executor `397c94a4e246c90b21cf408790fa0d55bf32d795`. `Matrix_ValidationEvidenceCommandsAreReproducible` passed again (one passed, zero failed/skipped), and whitespace passed. Local implementation/review is complete; the parent continues the authorized exact-source CI and external publication work.

The full commit message passed the repository-pinned commitlint CLI with zero problems/warnings; validation evidence is retained in the system temporary directory.

## Spec Change Log

## Review Triage Log

| Reviewer / finding | Verdict / route | Evidence |
| --- | --- | --- |
| Blind 1: require the shared-status command against deletion | low / defer | The command extractor already permitted removal of any individual FrontComposer command before this patch; the new expression is present and runs successfully. Exact command/path inventory coverage is existing test-hardening debt. |
| Blind 2: validate McpCli and Platform table rows in fitness | low / defer | The current graph test already excludes these non-consumed root selections. Both rows match the committed pointers and the root release ledger gate checks changed gitlinks; direct documentation-table validation is existing coverage debt. |
| Blind 3: distinguish release executor from root catalog | low / patch | The new Builds role repeats an ambiguous description. Add the exact pinned executor SHA separately from the root catalog identity; no historical row changes are needed. |
| Blind 4: add release completion acceptance criteria | low / reject | This proposes editing the build spec. Its frozen approach already assigns exact-source CI, Release, and package verification to the parent after the local reviewed patch; the user task remains open until these occur. |
| Blind 5: specify downloaded package provenance | low / reject | This proposes editing the spec. The parent publication verifier already downloads all nine public packages, validates IDs/versions and records nuspec repository metadata and SHA-256, beyond mere tag existence. |
| Blind 6: add container results to spec receipt | low / reject | This proposes editing the spec. The unchanged Release workflow runs publication and verification for all three containers and is required to succeed before task completion. |
| Blind 7: retain exact historical comparison command | low / reject | This proposes editing the spec's notes. Parent independently compared the historical matrix tail after the permitted heading/command substitutions against the full baseline revision and the ledger as an exact byte prefix; both passed with a temporary receipt. |

Edge-case reviewer reported no findings. Verification-gap reviewer reported no gaps. The two deferred coverage improvements predate this change; no high or medium regression was identified.


## Verification

- `dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release -m:1` -- zero warnings/errors using the CI package graph; this local Release run mirrors the failing CI under the user-authorized task.
- `dotnet tests/Hexalith.Parties.Tests/bin/Release/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.PlatformApiPrerequisitesTests` -- all prerequisite checks pass without skips.
- `bash scripts/gitlink-rc-gate.sh --worktree` and `bash scripts/gitlink-rc-gate.sh --diff v1.2.1` -- both pass.
- `bash scripts/check-no-warning-override.sh` and `git -c core.whitespace=cr-at-eol diff --check` -- regression/whitespace guards pass.
