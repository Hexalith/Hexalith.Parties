---
title: Connect the Parties NuGet trusted publishing policy
status: done
route: dispatch
baseline_commit: a3e5ef3b92a56fb03adc0e7e3235ff5ca158ab9a
review_loop_iteration: 0
---

## User intent

The original authorized task repairs compilation/test and CI failures, runs Release with human validation bypassed, and verifies all nine NuGet packages. The latest steering is to create and use a NuGet publishing policy. The operator has now registered an active GitHub Actions policy named `Hexalith.Parties` and corrected its package owner to `Hexalith`. The provided policy binds GitHub owner `Hexalith` (80614290), repository `Hexalith.Parties` (1170072276), workflow filename `release.yml`, and environment `production`. The operator's NuGet creator account is `jpiquot`; the policy owner and login account have distinct purposes. The policy reported by the user allows new packages/versions plus unlist/relist with glob `*`. This task consumes that existing policy and publishes only the nine manifest packages; it does not edit remote policy permissions.

<frozen-after-approval>
Use NuGet Trusted Publishing to replace the expired long-lived key for Parties. Preserve the current exact main-source gate, typed bypass that selects Commitlint proof, protected production environment, immutable shared execution identity, independent nine-package inventory, package metadata and consumer validation, and three Parties-owned container destinations. Do not publish another module or change root gitlinks. Preserve concurrent source/test work. The user already authorized Release and bypassing human validation; no second approval checkpoint is required.
</frozen-after-approval>

## Evidence and architecture

Release run 37597780819 failed its first NuGet upload with HTTP 403. All nine 1.2.0 versions remained absent and the generated failed tag was removed after registry and GitHub release checks. Root Release currently calls a pinned Builds reusable workflow with a secret API key. NuGet's current server validates `job_workflow_ref` against the package repository, so login inside a Builds reusable workflow cannot authenticate the user-created Parties policy (NuGet/login issue 6, NuGetGallery issue 11000). Login must run in the Parties workflow's own job. Do not implement a custom token exchange or copy platform preparation code into Parties.

The already checked-out/published Builds commit `397c94a4e246c90b21cf408790fa0d55bf32d795` supplies `Github/prepare-domain-release`. This composite validates its own repository/ref against the supplied immutable execution SHA, checks the package inventory and checkout identity, checks out the same Builds actions, initializes only root submodules, installs/verifies npm dependencies, restores/builds Release, resolves the exact publication freeze flag, requires the NuGet creator, and repeats current-main and exact-source push proof. Its output permits the caller to gate login and Semantic Release. Existing reusable workflow callers stay untouched. `NuGet/login` v1.2.0 resolves to reviewed commit `8d196754b4036150537f80ac539e15c2f1028841`.

## Implementation

1. Change only `.github/workflows/release.yml`'s protected release job to a direct Ubuntu job, keeping `needs: verify-source`, production environment, 60-minute timeout, release concurrency, and necessary GitHub permissions. Remove the unused attestation permission. Check out the dispatched source with full history and no stored credentials. Call SHA-pinned `prepare-domain-release` with the same execution SHA, solution, manifest, declared count nine, selected source-proof workflow, repository freeze flag, and explicit `vars.NUGET_USER` creator account. Never use GitHub actor or package owner as a login default.
2. Set repository variable `NUGET_USER=jpiquot`, preserving existing secrets and publication flag. After successful shared preparation, set up arm64 emulation and use the publisher composite from that exact Builds checkout for the same three project/image mappings. Authenticate with pinned official NuGet/login in this direct job, conditioned on the enabled output. Pass its key exclusively to Semantic Release, with no secret fallback. Semantic Release must carry the same container, source, manifest, environment, execution SHA and authority-disabled contract values as before. Keep evidence upload even after failures.
3. Update the relevant tests in `tests/Hexalith.Parties.Ci.Tests/PartiesContainerPublishWorkflowTests.cs` to verify direct caller ownership, immutable action/login pins, production protection, both source-proof paths, freeze conditions, temporary-key handoff, absence of legacy secret fallback, container inventory and complete preflight environment. Preserve all existing test intent. Add only meaningful coverage for new token/identity boundaries; the shared composite's hermetic guards are already tested upstream.
4. Update `docs/ci.md` and `docs/ci-secrets-checklist.md` to explain the current shared composite path and NuGet policy creator variable, the correct package owner, and short-lived login key. Keep CI, package-mode, Zot, Pact, environment and deployment boundaries current. Update `tools/nuget-trusted-publishing-policy.json` to record user-reported registration accurately, including the policy creator and broad scopes the user selected, without implying that local JSON creates a policy.

## Acceptance

- Given the active Parties policy and `NUGET_USER=jpiquot`, when the protected release job reaches NuGet login, then its caller workflow identity matches `Hexalith/Hexalith.Parties/.github/workflows/release.yml` and production, and login returns a masked temporary key.
- Given absent creator, stale source, missing selected source proof, malformed inventory, or mismatched Builds execution identity, when preparation runs, then authentication/publication does not begin.
- Given any publication flag other than exact lowercase `true`, when preparation completes, then login and publication are skipped.
- Given enabled publication, when Semantic Release pushes packages, then it receives only the temporary login output, uses the exact nine-package manifest and the existing metadata/consumer/collision gates, and still publishes only the three Parties-owned images.
- Given a successful Release, when independent verification downloads all nine registry packages, then each expected version exists with matching repository commit and valid nuspec metadata. Report CI failures separately; the original full CI acceptance remains open until all required jobs pass.

## Verification

Run `python3 Tools/test-prepare-domain-release.py` from the unchanged owning Builds checkout. Run actionlint when available and the focused Parties CI test class using a serialized Release build and native xUnit v3 test execution. Run whitespace/JSON checks. Review the complete auth diff in the three prescribed BMad lenses before committing. Validate exact Conventional Commit text with root pinned commitlint and preserve evidence. Stage only this task's files; concurrent application changes must remain intact. Push the auth commit, wait for exact-source Commitlint proof, dispatch Release with bypass-validation=true, approve the existing production pending deployment under standing authorization, and independently verify NuGet package bytes. If policy activation or external credentials fail, report the actual failed stage and do not claim publication.

## Review Triage Log

## Remote evidence

## CI selection reconciliation

The owner concurrently advanced main to `a3e5ef3b92a56fb03adc0e7e3235ff5ca158ab9a`, selecting EventStore `98da5a04e6df33ba026cbaae46d1777acdca7a21` (`v3.115.0-8-g98da5a04`) and Memories `14bb1c17b66ad0fa053ad0ed332c82fdd85f3887`. CI run 37601341845 failed only the two platform fitness methods against superseded diagnostic pointers. Reconcile `tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs` and the current selection in `story-8-3-platform-api-prerequisite-matrix.md` to the accepted root graph, preserving historical receipt constants, SDK 3.115.0, all G5 gates, and root gitlinks. Verify current source describes in the current graph check; verify historical G5 identity at its immutable Parties/EventStore receipt commits. Run the complete fitness class and include this small repair in the auth review.

The repository variable `NUGET_USER=jpiquot` is configured and verified. The unchanged shared preparation's nine hermetic guard tests pass. Public registry checks still show EventStore DomainService/Contracts 3.115.0, so the previously recorded Tenants runtime API blocker has not been cured by source-pointer changes.

## Review execution

Review is scoped to this task's eight files against immutable root baseline `a3e5ef3b92a56fb03adc0e7e3235ff5ca158ab9a`; concurrent owner-continuation work is excluded and will remain unstaged. Three review lenses reuse the existing threads because this runtime has reached its four-thread lifetime limit. The reviewers are not context-free, which limits independence.

## Local verification — 2026-10-07

- `actionlint .github/workflows/release.yml` passes.
- The unchanged Builds `python3 Tools/test-prepare-domain-release.py` guard suite passes 9/9.
- The isolated CI project Release build reports 0 warnings/errors; native `PartiesContainerPublishWorkflowTests` passes 35/35 with zero skips. Logs: `/tmp/parties-trusted-publishing-ci-build.log`, `/tmp/parties-trusted-publishing-ci-tests.log`.
- A serialized package-mode Parties service test project build with isolated artifacts reports 0 warnings/errors (5.17 s). The platform fitness class passes 16/16 with zero skips (9.761 s), including both failed remote methods. Logs: `/tmp/parties-nuget-fitness-build.log`, `/tmp/parties-nuget-fitness-tests.log`; XML: `/tmp/parties-nuget-fitness-tests.xml`. Initial execution outside the repository failed root discovery; copying the same built output to an ignored directory beneath the repository resolved that test harness path requirement without changing implementation.
- JSON matches all nine authoritative IDs and operator-supplied policy identity; whitespace checks pass. Current-selection checks still enforce the accepted committed gitlinks, checkout identities and describe value, while historical G5 identity checks remain bound to their dated receipt.
- Actual OIDC login, package pushes, container publication, and full push CI remain remote verification steps.

## Review triage — all three lenses collected

| Finding | Verdict | Evidence and route |
| --- | --- | --- |
| Blind 1: current pointers absent from RC signoff ledger | medium | The release-base diff rejects both already-committed current pointers; current source fitness alone does not authorize that ledger. Patch by recording the selected graph under the user's standing release authorization, without changing gitlinks or historical gates. |
| Blind 2: RC gate absent before authentication/tagging | medium | The supported Commitlint bypass does not run the separate root gitlink gate. Patch by passing the already-proved release floor to the unchanged RC gate before login. |
| Blind 3: frozen run fails downstream verification | medium | Preparation deliberately returns successful false, but verify-publication unconditionally requires a new exact-source release. Patch with the existing verdict as an internal job output/condition. |
| Blind 4: unrestricted describe can change at identical SHA | low | Extra upstream tags or abbreviation settings can change describe output without pointer drift. Patch with the selected published base tag and fixed eight-character abbreviation; bind historical exact-tag lookup to its dated version too. |
| Blind 5: comment decoys satisfy structural tests | medium | Required conditions and action references are checked with unanchored substring assertions. Patch the structural view/field checks so comments cannot stand in for executable YAML, with a targeted regression. No parser dependency is required. |
| Blind 6: no permanent literal tests for policy receipt | low | The JSON is a non-executable record of operator settings. One-time validation already checked creator, owner, immutable GitHub IDs, workflow/environment and the nine-package inventory. Reject permanent tests mirroring this low-impact record; real authentication is independently verified by the remote exchange. |
| Blind 7: downstream tag check alone does not prove NuGet bytes | medium | The existing verification job checks GitHub release identity; this code predates the patch. Defer persistent registry-byte verification in that job. This task independently downloads all nine registry packages and checks identity/version/repository commit after Release, so a successful tag is never reported as NuGet publication proof. |
| Edge 1: frozen verdict discarded by downstream job | medium | Same demonstrated outcome as Blind 3; patch the shared root cause. |

Verification-gap review reported no findings. All review threads were reused because of the runtime's lifetime thread limit; that limits independence. The parent stages only the ten task-owned files; concurrent owner-continuation work remains untouched.

## Verification after review fixes

- The updated CI project Release build reports 0 warnings/errors. Native workflow tests pass 41/41 with zero skips; logs are `/tmp/parties-trusted-publishing-followup-ci-build.log` and `/tmp/parties-trusted-publishing-followup-ci-tests.log`.
- A fresh isolated checkout of the committed baseline plus the reviewed fitness/matrix/ledger patch builds the service test project in package-mode Release with 0 warnings/errors (4.90 s). The complete updated platform fitness class passes 16/16 with zero skips (9.252 s). Logs: `/tmp/parties-nuget-isolated-fitness-build.log`, `/tmp/parties-nuget-isolated-fitness-tests.log`; XML: `/tmp/parties-nuget-isolated-fitness-tests.xml`.
- The isolated checkout avoids concurrent uncommitted application/test edits. An attempted build of the shared working tree encountered CS8619 in the concurrently edited `PartyDomainProcessorValidationTests.cs:439`; that other task's file was preserved. An old copied DLL run is not evidence for the final fitness patch. The successful fresh build above is the final evidence.
- The root gitlink gate passes both `--worktree` and `--diff v1.1.1`. Actionlint and whitespace checks pass. The exact commit candidate passes the pinned commitlint CLI with zero problems/warnings (`/tmp/parties-nuget-commitlint-evidence.log`).

## First authenticated remote Release

Source `cdd72b1d4a73013b04e909b5e3d82b59e61386d1` passed Commitlint 37607041104 and CodeQL 37607041095. CI 37607041148 passed its build, nine-package consumer validation, and all 2,713 build-and-test cases with zero skips. Its required Aspire job failed again (35 passed, six existing health skips, one gateway timeout). The launched Tenants source reports CS0246 for `IDomainServiceAdministratorVerifier` and `DomainServiceAdministratorClaim`; fresh NuGet indexes still select EventStore Contracts/DomainService 3.115.0.

Release 37607197353 passed exact-main/source proof, registry-floor, shared preparation, root gitlink gate, and the caller-owned NuGet OIDC login. All nine 1.2.0 packages were pushed and independently downloaded; package identity/version/source commit and EventStore dependency versions match. Receipt: `tests/nuget-trusted-publishing-2026-10-07/registry-publication-1.2.0.json`. All three OCI indexes were published and passed metadata validation. Domain/MCP liveness passed both architectures; UI startup failed on invariant globalization because localization needs culture `en`. Semantic Release therefore did not create the GitHub Release; publication acceptance stays open pending `spec-fix-ui-container-globalization.md` and a fresh 1.2.1 release. Retain the published 1.2.0 tag/packages/images. No duplicate-push bypass or tag deletion is permitted.

## Verified completion — 2026-10-07

Protected Release [37609885094](https://github.com/Hexalith/Hexalith.Parties/actions/runs/37609885094) succeeded with the user-authorized validation bypass at exact source `34f57baccdf3e9fc2ec615f07499c6d274105851`. Caller-owned NuGet login, preparation, root gitlink validation, publication and downstream exact-source verification all pass. [GitHub Release v1.2.1](https://github.com/Hexalith/Hexalith.Parties/releases/tag/v1.2.1) was published at 2026-10-07T10:59:03Z.

Independent NuGet index requests and package downloads verified all nine 1.2.1 identities/versions, exact repository commit and EventStore 3.115.0 dependencies. The three container indexes pass OCI provenance validation; all six digest-pinned platform startup/liveness checks and their cleanup pass. Receipts: `tests/nuget-trusted-publishing-2026-10-07/registry-publication-1.2.1.json` and `release-1.2.1-summary.json`. Existing 1.2.0 packages/images/tag remain untouched.

Commitlint 37609806697 and CodeQL 37609806812 pass. CI 37609806709 passes Release build, isolated package consumers and 2,713/2,713 build-and-test cases with zero skips. Its required Aspire job remains failed: 35 passed, six existing health skips, one gateway timeout. The launched upstream Tenants source still reports CS0246 for `IDomainServiceAdministratorVerifier` and `DomainServiceAdministratorClaim`, absent from published EventStore Contracts/DomainService 3.115.0. That unresolved upstream publication boundary belongs to the original full-CI spec, which stays in-review. These scoped trusted-publishing/UI-container acceptance criteria are complete; no claim of green full CI or refreshed platform parity is made.
