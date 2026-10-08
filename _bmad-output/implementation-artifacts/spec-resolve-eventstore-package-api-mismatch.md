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
- Resumed on 2026-10-08. External work committed the earlier handoff and receipts
  during startup as Parties revision `450b1ddb6dde38a4431f300033d2c7d3eba27830`.
  This run preserves that work and records a separate dated verification receipt.
- Rechecked all nine public NuGet indexes and downloaded DomainService 3.115.0:
  the publication prerequisite still exists and the package/assembly hashes match
  the earlier receipt. Source history confirms the required API commit follows
  the published v3.115.0 commit.
- Refreshed `docs/ci.md` and `docs/architecture.md` to the current evidence; corrected
  the stale 3.113.0 current-package claim and release-readiness wording while
  retaining the historical source approvals. No runtime or dependency edits.
- Current verification: restore passed; serialized Release solution and focused
  host builds both failed with only the two expected CS1061 errors and no warnings.
  All 101 CI-lane tests passed without skips; the warning/submodule guard passed.
  Receipt: `tests/eventstore-package-api-2026-10-08/README.md`, with sanitized
  command/test/package/dependency evidence and unchanged source/input stamps.
- External work advanced the root revision again to
  `f2886c604f642c5d730f8b615a554206a2b35300` while evidence was being collected,
  adding only this run's preliminary JSON receipts. Root submodule checkouts and
  every stamped build/host/release input remained unchanged. Source receipts
  record both identities; no exact-current-main full-CI success is claimed.
