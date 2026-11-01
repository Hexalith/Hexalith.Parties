---
title: 'Synchronize dependencies and publish a verified release'
type: 'bugfix'
created: '2026-11-01'
status: 'in-review'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '8dae3f15c2624bc3990128691c732bef73858b78'
context: []
---

<frozen-after-approval reason="user explicitly authorizes synchronization, fixes, release and bypassing human validation">

## Intent

**Problem:** Parties must ship all current work with synchronized root submodules, current stable SDK and packages, passing CI, and independently confirmed NuGet publication. Current release preparation is blocked by unsigned source selections and secret-name drift.

**Approach:** Refresh only outdated dependencies, repair demonstrated CI/release defects, preserve automated gates, synchronize and push owned changes, then dispatch and approve the production release using the authenticated operator. The user explicitly requested bypassing human validation; no additional spec or release confirmation is required.

## Boundaries & Constraints

**Always:** Work on main; preserve user work; use only root-declared submodules. Keep shared NuGet authority in Builds and package-only Release CI. Validate exact commit messages with each owning repository's pinned commitlint. Keep production environment identity, exact-source CI, root-pointer proof, trusted NuGet login, package boundary checks and publication verification. Use existing operator authorization for source-selection ledger entries and release approval.

**Never:** Initialize nested submodules, use recursive/remote submodule update, weaken automated tests or warnings, fabricate test evidence or owner parity acceptance, expose credentials, or publish an artifact before required automated gates pass. Keep approved prereleases when a stable alternative would downgrade required APIs. Leave historical receipts unchanged.

</frozen-after-approval>

## Code Map

- `global.json`: SDK 10.0.403 already matches Microsoft's latest stable metadata; leave unchanged unless metadata advances.
- `references/Hexalith.Builds/Props/Directory.Packages.props`: shared catalog selects Commons 2.30.7 and PolymorphicSerializations 1.19.6, while 2.30.8 and 1.19.7 are published. Preserve catalog audit requirements in `Tools/README.md` and `Tools/package-version-audit.json`.
- `.config/dotnet-tools.json`: module tool 4.39.0 can advance to published 4.41.0; aspirate 9.1.0 is current. README's current pin must match; historical dated preparation remains historical.
- `.github/workflows/release.yml`: keep execution SHA 11a3a79cfac4bba9548c6364a17a01ee9b561370. Both publish and verification currently reference HEXALITH_ZOT secrets while repository secrets are ZOT_REGISTRY_USERNAME/API_KEY. Support configured names without removing preferred names.
- `.gitlink-signoff.tsv`: root source selections lack current entries. Parent performs explicit upstream fast-forwards, then records all ten final exact source identities under authorized operator jpiquot.
- `tests/Hexalith.Parties.Ci.Tests/PartiesContainerPublishWorkflowTests.cs`: existing workflow contracts bind immutable execution identities and release secret names. Extend meaningful coverage for configured secret fallback; do not rewrite unrelated assertions.
- `scripts/gitlink-rc-gate.sh`, `tools/release-packages.json`, `scripts/verify-release-publication.py`: preserve admission and eleven-package verification.
- `/tmp/parties-release-20261101/dependency-audit.json`: official registry audit; remaining npm direct dependencies and .NET package families are current. Node24 typings 24.19.2 match selected Node24.

## Tasks & Acceptance

**Execution:**
- [x] Shared Builds catalog/audit -- update Commons and PolymorphicSerializations to latest published stable patches; run relevant catalog validation without nested initialization. Implementation agent makes local edits only; parent validates messages, commits and pushes inside Builds.
- [x] `.config/dotnet-tools.json`, `README.md`, `docs/getting-started.md` -- select module tool 4.41.0 and update current documentation, then restore/validate the tool.
- [x] Release workflow and existing CI tests -- map preferred Zot names with fallback to existing repository secret names for publish and verification, preserving production and automated release gates.
- [ ] Parent synchronization -- fast-forward upstream root submodule main branches, publish Builds changes, record exact selected gitlinks/signoffs, and push parent after validation. Implementation agent leaves Git operations to parent.
- [ ] Parent release -- require green CI/RC on exact main, dispatch Release, approve pending production jobs under explicit operator authorization, diagnose/retry failures, and independently check all eleven NuGet package versions and source metadata.

**Acceptance Criteria:**
- Given official current registries, when dependencies are audited, then SDK and direct dependency selections are latest compatible stable versions with deliberate prereleases preserved.
- Given root submodule updates, when source synchronization completes, then every selected commit exists upstream, working trees are clean, nested submodules remain uninitialized, and exact pointer admission passes.
- Given existing Zot secret names, when release and verification run, then credentials resolve without plaintext exposure and automated gates remain enforced.
- Given a green main source, when Release is dispatched and approved, then GitHub records its release and all eleven declared packages download from NuGet with matching version and source commit.

## Implementation Notes

- Initial CI finished with three fitness failures: current McpCli/Platform constants lag committed pointers, and the current source observation still says EventStore 3.130.1 instead of effective 3.130.2. Reconcile only current selection constants and the current observation in `tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs` and `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md`; preserve historical identities and independent parity refusals. Final source identities follow parent synchronization.

- Initial root and all ten submodules were clean. Root main equals origin/main. Microsoft release metadata reports stable SDK 10.0.403; .NET11 remains prerelease.
- User's explicit instruction to bypass human validation supersedes the skill's spec checkpoint; proceed without a redundant confirmation. Public pushes and publication are explicitly authorized.
- Latest initial CI has owner-publisher, declaration, UI, BMAD and unit/build lanes passing; Tier2 remains in progress. Older missing Builds ref ef40e875f346f47903f8112ae2d30af0d9e48198 was already replaced by an upstream commit.

## Spec Change Log

## Review Triage Log

| ID | Verdict | Evidence and route |
| --- | --- | --- |
| E1 | medium / patch | Independent credential fallbacks can mix a partial preferred pair with configured fallback. Select a complete pair consistently at publish and verification boundaries. |
| B1 | medium / patch | Same demonstrated partial-pair defect as E1; preserve preferred precedence only when both preferred secrets exist. |
| B2 | medium / patch | Existing secret validation proves presence only, while NuGet publishes before containers. Add registry authentication before trusted NuGet login/publication so invalid selected credentials fail before writes. |
| B3 | false | This is declarative GitHub expression wiring; the existing boundary theory checks actual publication and verification values. Extend it to require the complete-pair predicate and preflight ordering; hosted release provides the real configured fallback execution. A separate local reimplementation of GitHub's evaluator would duplicate the platform rather than test production code. |
| B4 | medium / patch | Current module instructions in CI/development/test docs still prescribe 4.39.0. Update current instructions to 4.41.0 and preserve dated preparation receipts. |
| B5 | low / patch | Registry discovery exists only in temporary files. Retain the source-linked dependency audit with an observation timestamp in the repository receipt directory. |
| B6 | false | Final exact-source checks and hosted release remain explicitly pending; no final validation claim has been made. Preserve commands/results in the durable receipt as work finishes. |
| B7 | false | Shared feed audit intentionally preserves ungranted cross-module compatibility acceptance; it does not claim consumer qualification. Parties validates its actual updated package graph independently, without inventing owner parity acceptance. |
| B8 | low / patch | Add direct effective-graph assertions for Commons and PolymorphicSerializations to the existing current-selection fitness test, preventing consumer overrides from silently retaining old patches. |
| B9 | false | Documentation explicitly dates successful source preparation to 2026-10-31/tool4.39.0, while new evidence claims declaration validation only. No new source preparation success is claimed; record a fresh prepare result separately if available. |
| B10 | false | Source synchronization/push is still pending, not asserted complete. Upstream advanced during validation; reconciled it with merge426df281f7728ca971c21d1cf9b205f3a636910d retaining the exact newer upstream tree/audit, and all current pins will point to the published result before parent push. |
| V1 | false | Verification-gap reviewer found no gaps. |

## Verification

- Restore and build `Hexalith.Parties.Standalone.slnx` in Release package mode, then run affected CI test project individually; preserve warnings as errors.
- Run Builds catalog validators, dotnet tool restore/manifest validation, npm locked installs/audit as needed, root gitlink gates and no-warning-override guard.
- Inspect exact-source GitHub CI/RC and release workflow jobs, then use publication verification and NuGet flatcontainer downloads for final proof.

- Local implementation verified: standalone Release package build passed with zero warnings/errors, module4.41.0 declaration validation passed, workflow tests67/67 passed, catalog audit and115generator scenarios passed. Complete exact-HEAD tests and hosted CI/release remain pending. npm clean install passed; latest bundled npm still reports37moderate/1high advisories and its forced fix would downgrade current semantic-release. No unsupported downgrade or override was applied.

- Concurrent upstream Builds catalog refresh was reconciled without losing either history. The final selected catalog/audit exactly matches upstream3f269068be3f7425b19c399bf9be3cc90ffbf5cd and binds source88776c37e0b3a5572c4c6af7a7a9e1de57d44cde; local merge426df281f7728ca971c21d1cf9b205f3a636910d preserves both validated local commits and upstream changes. Strict merged audit validation passed.

- Final Builds synchronization also preserved upstream evidence-pin corrections7fd5fd3bb3efae762e9cfce97c80d5dd04c6f30c. Published merge df13d9d31f7ee1a1b0fb7eeac7e8b27b9a9f9fb0 is the selected root gitlink and CI execution revision. Workflow review patches passed69/69 cases; tool4.41.0 source preparation returned prepared with observational diagnostics, recorded separately from a source build.
