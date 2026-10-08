---
title: 'Resolve the EventStore package API mismatch'
type: 'bugfix'
created: '2026-10-07'
status: 'done'
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
- Blind Hunter review returned eight findings. Each was checked against the
  EventStore release workflow, package manifest, SDK authentication helpers,
  route catalog, and retained receipts; all were resolved with documentation or
  evidence corrections. No findings were deferred and no runtime code was added.
- The handoff now names upstream exact-source full CI, non-bypass publication,
  the full coordinated package inventory, published API/provenance/dependency
  inspection, topology-owner credential wiring, and successful persisted-state
  and callback/credential acceptance evidence. Retained raw NuGet index snapshots
  match the initial probe hashes; `probe-package.py` reproduced the observation,
  and `upstream-history.json` retains the three source-history check results.
- The final focused CI documentation-contract check rejected the legacy input
  literal in `docs/ci.md`, including the upstream-only mention. Moved the exact
  EventStore dispatch input to the handoff receipt and linked it from CI docs;
  preserved the existing test and all release-gate enforcement.

- Final focused documentation check and complete CI lane passed after the
  correction: 101 CI tests, zero failures/skips. Final source/input stamps confirm
  no runtime, dependency, release-gate, or root checkout changes by this workflow.
  The spec is complete for the no-compatible-package handoff branch; the Release
  compiler blocker remains documented. No commit or push was made by this run.

## Review Triage Log

- F1 — medium, patch: `docs/ci.md` ambiguously called domain-service invocations
  gateway operational routes. Upstream `security-model.md` distinguishes public
  JwtBearer APIs from internal workload assertions; corrected the boundary.
- F2 — medium, patch: the handoff did not identify the upstream manual release
  or its Commitlint-only bypass. Verified `release.yml`; now require successful
  exact-source push full CI, `bypass-validation=false`, and returned release evidence.
- F3 — low, patch: the nine inspected consumer indexes did not identify the full
  publisher inventory. Verified `tools/release-packages.json` has 14 entries;
  linked the authoritative manifest and required one coordinated release version.
- F4 — medium, patch: "verified release" lacked artifact acceptance criteria.
  The SDK declares the public generic extension constrained to
  `IEndpointConventionBuilder`; the handoff now requires that API, descendant
  provenance, hashes, and matching resolved ServiceDefaults/family versions.
- F5 — medium, patch: the acknowledged absent credential wiring lacked a concrete
  owner/action handoff. Verified the two app-channel helpers, workload-client
  helper, and scoped issuer contract; documented the integrated-topology owner,
  receiving app/sidecar pairs, and optional audience/operation grants. Runtime
  implementation stays a post-publication follow-up within the stated scope.
- F6 — medium, patch: runtime verification had no explicit success criteria.
  Documented persisted state after a gateway command, accepted sidecar callbacks,
  denied missing/forged credentials without writes, and no skipped required checks.
- F7 — low, patch: index hashes and derived results alone did not retain the full
  observed content or replay procedure. Saved nine matching raw index snapshots
  and a standalone probe; execution reproduced all original index/package hashes.
- F8 — low, patch: source-history command results were only described in prose.
  Re-ran the tag, introducing-symbol history, and ancestry checks; recorded exact
  argument arrays, UTC times, outputs, and successful exit codes in JSON.
