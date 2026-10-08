---
title: 'Resolve the EventStore package API mismatch'
type: 'bugfix'
created: '2026-10-07'
status: 'in-progress'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Find a published EventStore version that provides `RequireEventStoreSidecarChannel`
and update Parties to it. If no compatible published version exists, identify the
required upstream EventStore change and document a concrete publication handoff.
Verify the package-mode Release solution build and the CI test lane. Preserve the
host security calls, package-mode dependency selection, and the mandatory successful
full-CI proof for the exact current main commit before release.

</frozen-after-approval>

## Implementation Notes

- Investigation found the NuGet v3 indexes for DomainService, Server, Client,
  Contracts, Testing, and Aspire end at 3.115.0. The downloaded DomainService
  3.115.0 assembly does not contain `RequireEventStoreSidecarChannel`; its
  repository metadata identifies `283b07a52c9c70e1c940164a7011ee8c3ad98b2d`.
- The API first appears in upstream EventStore commit
  `c4d5455a3b79ca1ba0113a286cf2432b2cace7fb`, after that release. No dependency
  update is valid until an owner-published compatible version exists. Scope is
  a publication handoff and current verification evidence in `docs/ci.md` and
  `docs/architecture.md`, plus this workflow artifact.
- Preserve `Directory.Packages.props`, `Directory.Build.props`, all project
  references, `src/Hexalith.Parties/Program.cs`, release workflows, and release
  preflight wrappers. Existing CI tests already check package selection and
  reject missing full CI or legacy bypass values; no new tests are needed for
  this documentation-only outcome.
- Verification: default solution restore; serialized Release solution build;
  focused host build for first-failure evidence; `scripts/test.ps1 -Lane ci`;
  existing warning/submodule guard. Record broad build failure separately from
  successful CI-lane checks. No publication, push, commit, or submodule update
  is part of the requested work.
- Default restore succeeded. The Release solution build failed with exactly two
  CS1061 errors and no warnings; a subsequent focused host build reproduced both.
  The CI lane passed all 101 tests without skips; the warning/submodule guard
  passed. No compatible package exists, so the 3.115.0 pin remains in place.
- Updated `docs/ci.md` and `docs/architecture.md` with the exact upstream API
  commit, coordinated publication handoff, security dependencies, and required
  post-publication verification. Added the dated verification receipt with NuGet
  probes and effective package-mode assets. Raw logs remain ignored local files.
- Concurrent external work advanced EventStore and Platform submodule pointers.
  These changes were not made or reverted by this workflow; the focused host
  build confirms the package blocker independently of those source checkouts.
