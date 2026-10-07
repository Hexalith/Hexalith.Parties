---
title: 'Align Parties CI/CD with EventStore and Tenants'
type: 'bugfix'
created: '2026-08-01'
status: 'in-review'
baseline_commit: '92ee9c1b23a444db5c0ea44ec99ad9ffeef16e83'
review_loop_iteration: 0
context:
  - '{project-root}/references/Hexalith.Builds/.github/workflows/ci-cd-standards.md'
  - '{project-root}/docs/ci.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Parties still uses an obsolete push-triggered release caller and commitlint contract. Release run `30690869631` cannot start because the caller omits the required Builds identity and `actions: read`; later stages also lack the reviewed package inventory and fail-closed publication preflight. CI run `30690869648` separately exposes package/API skew: Parties uses EventStore rebuild APIs not present in published package `3.88.0`.

**Approach:** Align Parties with the hardened EventStore/Tenants model: CI remains package-mode, releases become manual and exact-source gated, shared tooling is immutable, secrets are explicit, publication identity is frozen, and tests/docs enforce the contract. Extend the platform-owned Builds publisher first so Parties' three-container set receives the same atomic identity guarantees as the siblings' single-container releases.

## Boundaries & Constraints

**Always:** Preserve all nine Parties NuGet packages and exactly `parties`, `parties-mcp`, and `parties-ui`; require current `main` plus successful exact-SHA push CI before protected `production` approval; pin the release workflow and nested tooling to one reviewed 40-character Builds SHA; keep ordinary CI on Release NuGet dependencies; fail before any publication when source, inventory, destination, secret, or identity proof is incomplete.

**Ask First:** Committing or pushing the Builds submodule change, updating the parent gitlink to its resulting immutable SHA, configuring GitHub environments/secrets, dispatching a release, or changing the nine-package/three-container inventory.

**Never:** Force source references in CI, revert or conditionally compile away the projection-rebuild work, publish automatically on push, use `secrets: inherit`, use `--skip-duplicate`, weaken collision checks, duplicate shared publisher logic in Parties, publish a partial container set, or claim the EventStore package blocker is resolved before a compatible package exists.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Pull request | Open, synchronize, reopen, or retitle | Shared commitlint validates commits and current PR title | Reject empty/non-conventional title |
| Main push | Normal source change | CI/CodeQL run; Release does not auto-run | Package/API incompatibility remains visible |
| Invalid release dispatch | Non-main, stale main, or no exact green push CI | Stop before protected environment and secrets | Emit the failed source invariant |
| Valid release | Green current main, nine packages, three absent container tags | One frozen identity covers all destinations; approval precedes writes | Reject any package/container collision or identity drift before first write |

</frozen-after-approval>

## Code Map

- `references/Hexalith.Builds/Github/publish-containers/` -- existing canonical multi-container publisher, reviewed at release identity `397c94a4e246c90b21cf408790fa0d55bf32d795`; preserve this completed implementation and all gitlinks.
- `.github/workflows/release.yml` -- current caller-owned protected publication job with immutable shared preparation and NuGet trusted publishing. Remove the typed Commitlint bypass and require literal `ci.yml` proof in the unprotected gate, shared preparation, and publication environment; preserve trusted authentication, registry-floor and gitlink checks, inventory, and immutable execution identity.
- `.github/workflows/commitlint.yml`, `.github/dependabot.yml` -- PR-title/main-push enforcement and Conventional Commit dependency prefixes.
- `tools/release-packages.json`, `scripts/validate-publication-preflight.sh`, `release.config.cjs` -- caller-owned inventory and semantic-release verify/publish sequence.
- `tests/Hexalith.Parties.Ci.Tests/{PartiesContainerPublishWorkflowTests,ReleaseSourcePreflightTests}.cs` -- current structural and executable gate coverage; enforce fixed full-CI proof and reject Commitlint in the publication wrapper.
- `docs/ci.md`, `docs/architecture.md`, `docs/ci-secrets-checklist.md` -- operational contract and external prerequisites.

## Tasks & Acceptance

**Execution:**
- [x] `references/Hexalith.Builds/Github/publish-containers/{publication_preflight.py,publish-containers.sh,tests/,README.md}` -- make publication identity and destination evidence canonical for one-or-more container repositories while retaining single-container compatibility; work from the Builds repository and stop before commit/push without approval.
- [x] `.github/workflows/release.yml` -- mirror the hardened EventStore/Tenants caller: `workflow_dispatch`, non-cancelling release concurrency, unprotected exact-green-main preflight, job-scoped permissions, protected environment, immutable Builds SHA/input equality, count `9`, explicit secrets, and post-publication source verification.
- [x] `tools/release-packages.json`, `scripts/validate-publication-preflight.sh`, `release.config.cjs` -- declare nine packages and three containers, freeze/revalidate the shared identity, and remove duplicate-skipping publication behavior.
- [x] `.github/workflows/commitlint.yml`, `.github/dependabot.yml` -- validate edited PR titles plus direct `main` pushes and replace forbidden `chore(deps)` prefixes with `build(deps)`.
- [x] `tests/Hexalith.Parties.Ci.Tests/PartiesContainerPublishWorkflowTests.cs` -- replace obsolete string checks with fail-closed caller, inventory, multi-container, and semantic-release contract coverage.
- [x] `docs/ci.md`, `docs/architecture.md`, `docs/ci-secrets-checklist.md` -- document manual release operation, protected-environment prerequisites, immutable Builds identity, and the unresolved EventStore package prerequisite.

**Acceptance Criteria:**
- Given the resulting workflows and support files, when static CI contract tests and workflow lint run, then Parties matches the EventStore/Tenants release invariants without losing any package or container destination.
- Given a mocked three-container release, when verify, publish, and container phases execute, then all three repositories share one unchanged source/package/container-set identity and collisions fail before publication.
- Given EventStore `3.88.0` remains latest, when package-mode CI runs, then no workflow workaround hides the CS0246 dependency blocker; green CI requires an owner-published compatible EventStore package and subsequent approved dependency update.

## Spec Change Log

### Implementation evidence — 2026-10-07

- Loaded both frontmatter context files and audited the present checkout. Preserved `baseline_commit` and the frozen intent. Most requested safeguards already exist: manual dispatch, non-cancelling concurrency, protected `production`, job permissions, explicit credentials, nine-package inventory, complete three-container preflight, immutable Builds identity, collision rejection, and publication-source verification. Commitlint already covers edited PR titles and main pushes; dependency prefixes already use `build(deps)` or `ci(deps)`.
- The shared publisher at the existing reviewed release identity `397c94a4e246c90b21cf408790fa0d55bf32d795` already implements canonical multi-container identities and complete destination checks while preserving singular compatibility. Its publisher and reusable-workflow bytes are unchanged in the current Builds checkout. No Builds edit, commit, push, or gitlink change was needed.
- The October trusted-publishing implementation supersedes the task's reusable-workflow-only wiring: Parties has a caller-owned protected job, immutable shared preparation, and official NuGet login supplying a short-lived key. Preserved that authentication mechanism. The current `bypass-validation=true` path selects exact-source Commitlint proof, conflicting with this frozen spec's unconditional green CI requirement; the gate decision awaits explicit clarification, and release behavior remains unchanged. Strict-green-CI acceptance is therefore incomplete.
- Added `tests/Hexalith.Parties.Ci.Tests/ReleaseSourcePreflightTests.cs` to execute the actual caller shell against mocked GitHub responses. Twelve rejection cases cover non-main/invalid/stale source, absent/wrong-SHA/wrong-branch/non-push/incomplete/failed proof, malformed API data, and API failure. Three invalid typed-input fixtures (empty, `TRUE`, and unknown) fail before any API request. Success fixtures verify both current proof selections and exact SHA/branch/event request arguments. A structural check verifies the source gate has no protected environment or publication secrets and precedes the release dependency.
- Updated `docs/ci.md`, `docs/architecture.md`, and `docs/ci-secrets-checklist.md` with the current package-mode API prerequisite without altering dependencies, host security calls, projection-rebuild code, GitHub configuration, or dispatching a release.

Verification:

- `actionlint -no-color .github/workflows/*.yml`: passed.
- `python3 -m unittest discover -s Github/publish-containers/tests -p 'test_*.py'` from `references/Hexalith.Builds`: passed, 135 tests, including mocked three-container verify/publish/container identity and collision contracts.
- `bash -n scripts/*.sh && bash scripts/check-no-warning-override.sh`: passed.
- `pwsh -NoProfile -File scripts/test.ps1 -Lane ci -Configuration Release`: passed, 84 tests, no failures or skips.
- Initial `dotnet build Hexalith.Parties.slnx --configuration Release --no-restore -m:1` encountered stale source-mode assets (278 CS0234/CS0246 errors). Default Release restore of the focused Contracts project followed by its package-mode build passed. `dotnet restore Hexalith.Parties.slnx -p:Configuration=Release` then passed; rerunning the exact full-build command failed with two CS1061 errors at `src/Hexalith.Parties/Program.cs:64` and `:66`, because published EventStore 3.115.0 lacks `RequireEventStoreSidecarChannel`. The host assets identify all EventStore dependencies as packages. Today's blocker is distinct from the spec's historical EventStore 3.88.0 projection-rebuild CS0246 failure. Full-CI acceptance remains incomplete until a compatible package is published and an approved dependency update succeeds.


### Release-gate decision — 2026-10-07

The user explicitly selected "Yes — remove bypass and require full CI". This resolves the earlier ambiguity and restores the frozen strict-CI invariant. Remove `bypass-validation` and dynamic source-proof selection from the Parties caller, require literal `ci.yml` through preparation and publication, and reject `commitlint.yml` in the Parties publication-preflight wrapper. Update executable and static CI guard coverage and active docs. Preserve the newer caller-owned NuGet OIDC flow, immutable shared prepare action, complete nine-package/three-container set, registry-floor and gitlink gates, and the original baseline. No dependency update, upstream edit, Git mutation, external configuration, or release dispatch is authorized or needed. Record the package-mode build blocker separately without claiming green full CI.

Resumed checkout before this gate change: `2fb9e545adbb2f974abf3791b726a2f29750bcf9`. An external session committed the independent tests/docs and query-deadline work between turns; preserve that history and scope the new review to this gate change while retaining the original full-baseline artifact.

### Strict full-CI gate implementation — 2026-10-07

- Reloaded the full spec and both frontmatter context files. Removed the dispatch input, retired bypass branch, and dynamic source-workflow output. The unprotected source query, immutable shared preparation input, and semantic-release environment now require literal `ci.yml`. The Parties publication-preflight wrapper rejects `commitlint.yml` before invoking the shared tool in both verify and publish phases.
- Preserved the caller-owned NuGet trusted-publishing job, immutable Builds identity `397c94a4e246c90b21cf408790fa0d55bf32d795`, protected approval, release freeze, registry-floor and gitlink gates, nine packages, three containers, collision checks, and publication-source verification. No dependencies, Builds files, gitlinks, GitHub configuration, or runtime code changed; no release was dispatched.
- Updated executable gate fixtures to require successful exact-current-main push CI. Four legacy bypass-environment values cannot substitute successful Commitlint for missing full CI. Static contracts require fixed CI at every publication boundary, reject alternate wrapper workflows, and verify main pushes trigger both CI and CodeQL while Release remains manual. Updated all three active operational documents to describe mandatory full CI.
- `actionlint -no-color .github/workflows/*.yml`: passed.
- `bash -n scripts/*.sh && bash scripts/check-no-warning-override.sh`: passed.
- `pwsh -NoProfile -File scripts/test.ps1 -Lane ci -Configuration Release`: passed, 85 tests, zero failures or skips.
- `python3 -m unittest discover -s Github/publish-containers/tests -p 'test_*.py'` from `references/Hexalith.Builds`: passed, 135 tests, including single-/multi-container identity and collision contracts.
- `dotnet build Hexalith.Parties.slnx --configuration Release --no-restore -m:1`: failed with the same two CS1061 errors at `src/Hexalith.Parties/Program.cs:64` and `:66` for `RequireEventStoreSidecarChannel`; zero warnings. Restored host assets still identify EventStore 3.115.0 dependencies as NuGet packages. Strict release-gate acceptance is implemented and locally verified; full package-mode CI remains blocked by the separately owned compatible-package prerequisite.
- Verified the original `baseline_commit` and frozen intent are unchanged. The source/package delivery blocker remains visible through ordinary CI and can no longer be bypassed with Commitlint proof.

## Design Notes

At the August specification baseline, the shared publisher froze a singular `container_repository` and reused its evidence for each mapping, changing the identity for Parties' second image. That platform-owned issue is now resolved in the existing immutable publisher: its canonical container set supports all three Parties images while preserving EventStore/Tenants single-image callers. The EventStore package mismatch is an independent upstream delivery prerequisite, not a reason to violate package-mode CI.

## Verification

**Commands:**
- `actionlint -no-color .github/workflows/*.yml` -- expected: all callers parse and validate locally.
- `python3 -m unittest discover -s Github/publish-containers/tests -p 'test_*.py'` from `references/Hexalith.Builds` -- expected: single- and multi-container publication contracts pass.
- `bash -n scripts/*.sh && bash scripts/check-no-warning-override.sh` -- expected: scripts parse and CI safeguards pass.
- `pwsh -NoProfile -File scripts/test.ps1 -Lane ci -Configuration Release` -- expected: Parties CI contract tests pass.
- `dotnet build Hexalith.Parties.slnx --configuration Release --no-restore -m:1` -- expected after a compatible EventStore package is published and approved; until then, report the exact current package/API blocker separately (currently CS1061 with EventStore 3.115.0; the original CS0246 with 3.88.0 is historical).


## Matrix Verification — 2026-10-07

| Approved matrix row | Passing coverage |
| --- | --- |
| Pull request | `CommitlintAndDependabotUseReleaseCompatibleCommitContracts` verifies edited PR events, direct main pushes, and current title forwarding. |
| Main push | `MainPushRunsCiAndCodeQlWhileReleaseRequiresManualDispatch` verifies both main-push triggers and the manual-only release trigger. |
| Invalid release dispatch | `SourceGateRejectsInvalidDispatchOrIncompletePushEvidence` executes twelve rejected dispatch/proof cases; legacy-bypass fixtures also fail when only Commitlint is green. `SourceGateRunsOutsideTheProtectedReleaseEnvironment` verifies ordering and credential isolation. |
| Valid release | CI inventory/wrapper tests preserve nine packages and three container repositories. Shared publisher tests `test_main_freezes_and_revalidates_three_container_repositories`, `test_package_and_multi_container_destinations_are_checked_as_one_set`, and `test_multi_container_sequence_rejects_set_drift_before_publish` exercise all phases, collision rejection, and identity drift. |

All covering checks ran in the 85-test Parties CI lane and the 135-test shared publisher suite with no skips. The original full-baseline diff is retained separately; cold review targets the seven source/test/documentation files changed by the user-confirmed strict gate, excluding the spec account and unrelated concurrent query work. No claim of a green package-mode application build or remote publication is made.
