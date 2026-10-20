---
title: 'DW-71 pin reusable CI and CodeQL workflows'
type: 'bugfix'
created: '2026-10-20'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** CI and CodeQL delegate to shared Builds workflows through mutable `@main` references, allowing upstream edits to change validation without a Parties change.

**Approach:** Pin both reusable workflow calls to the existing root-declared Builds revision. Preserve caller triggers, inputs and permissions, align existing workflow assertions and current documentation, and verify the selected revision and caller compatibility before pushing.

</frozen-after-approval>

## Implementation Notes

- Selected DW-71 after completed DW-70; this is a freeform deferred item without a sprint story-key transition. Baseline: 4d4be476687cff4210774ec62558131ff8659558 on clean main, synchronized with origin/main after fetch. Read-only investigation delegated as directed by bmad-build.
- Route assessment: no intent gaps, no irreversible implementation action, and a small configuration/documentation footprint. Commit and push are explicitly authorized by the user.
- `.github/workflows/ci.yml` and `.github/workflows/codeql.yml`: replace only the two reusable `@main` references with `332b2c144defba546fe0a09626a4a72f2340e22f`, the current root Builds gitlink. GitHub confirms that exact full commit exists; both workflow blobs expose all existing caller inputs. Release retains its separate execution pin.
- `tests/Hexalith.Parties.Ci.Tests/PartiesContainerPublishWorkflowTests.cs`: update the two existing CI/docs assertions for the selected immutable revision; reuse existing test coverage instead of introducing tests for this mechanical configuration change.
- `docs/ci.md` and `docs/architecture.md`: describe the pinned delegation and explicit revision-update procedure. Keep transitive action versioning outside the claim: this pins reusable workflow definitions only.
- Validate the changed YAML with installed actionlint, focused Debug/source CI project build and the affected CI test assembly. Preserve exact selected revision, commands, results and pinned commitlint evidence under `tests/dw-71-immutable-ci-workflows-2026-10-20/`. Mark DW-71 complete only after successful checks and independent workflow review.

- Initial implementation passed actionlint, a Debug/source build with zero warnings/errors, 48 focused workflow tests and 148 complete CI tests. Existing-build Aspire baseline reached healthy Parties state in 3.4 seconds; all started resources were stopped. Two sibling projects remained NotStarted, so this is not fresh full-topology qualification.
- GitHub API confirms the exact selected commit and both workflow blob identities match the local Git objects. A byte comparison confirms the callers retain all other inputs, triggers and permissions.
- Review corrections bind the pin assertion to each active root job, document companion SHA/test/document updates and focused commands, and read the compatibility workflow through bounded `git show` at the selected immutable revision. Tests fail with an actionable diagnostic if that object is unavailable; they perform no fetch or checkout.

- Final verification after review: Debug/source CI build has zero warnings/errors; 48/48 focused workflow and 148/148 complete CI tests pass without failures/errors/skips/not-run. Actionlint and whitespace checks pass. Isolated disposable-workspace probes reject commented pins for both callers, accept divergent checked-out workflow contents when the selected Git object is present, and reject a missing pinned object. No production submodule files were modified. Commands, results and source hashes are retained in `tests/dw-71-immutable-ci-workflows-2026-10-20/validation.json`. Pinned commitlint 21.2.2 validates the exact full commit message.

## Review Triage Log

- Medium, patch: the initial pin assertion matched commented text. It now binds the immutable reference to the active root job block; independent review confirmed the correction, and both commented-pin mutations fail the owned assertion.
- Low, patch: the update procedure omitted the companion test constant and documentation references. The procedure now names every coordinated edit and provides actionlint/build/focused-test commands; independent review confirmed completion.
- Medium, patch: the existing compatibility check read the current Builds checkout and could validate another revision after pinning. It now reads the selected immutable workflow Git object through a bounded process with concurrent output draining. A divergent-checkout probe passes and a missing-object probe fails with the expected diagnostic; independent review confirmed the correction.

- Review complete: all three findings patched and independently verified; no remaining introduced issue or new deferred work. Hosted GitHub Actions execution remains post-push validation; local evidence does not claim fresh full-topology or release qualification.
