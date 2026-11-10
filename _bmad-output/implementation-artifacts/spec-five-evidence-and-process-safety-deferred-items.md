---
title: Close five evidence and process safety deferrals
type: bugfix
created: 2026-11-10
status: done
route: oneshot
review_loop_iteration: 1
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Resolve five existing deferred entries affecting trustworthy integration
diagnostics and ownership of test processes: bind persisted public/private
G4 aggregate tenants to the issued synthetic tenant; compare the loaded
assembly MVID with disk PE metadata before reporting its file hash; signal
lifecycle tool processes through stable kernel handles; terminate and reap
canceled native asset-staging children; and reconcile the already delivered
automatic lifecycle safety-test gate with the ledger.

Keep source selection, native reporter case identities, authorization,
qualification limits and normal failure gates intact. Add focused controls
using coherently resealed foreign-tenant evidence, real loaded assembly
replacement, actual process exit between identity verification and signal,
and cancellation of an owned staging subprocess. Preserve existing asset
staging behavior and withdrawal-before-AppHost-stop ordering. Mark only
these five entries resolved after verification and independent review.
Commit and push the completed changes on main as requested.

</frozen-after-approval>

## Implementation Notes

- Investigation confirmed four defects. Full lifecycle discovery already
  runs on ordinary CI main pushes/PRs and before diagnostic services start;
  its ledger entry is stale and requires reconciliation, not workflow edits.
- Small reversible changes to private test/evidence helpers, their controls,
  this spec and the ledger. There are no user-visible intent gaps, new
  public APIs, package changes, deployments or irreversible data actions.
- Reuse the shared current-owner reader in the public verifier. Validate
  `bindings.environment.isolation.syntheticTenantId` before exact persisted
  identity comparison. Preserve both readers' integrity and native timing
  checks; a synthetic test fixture grants no runtime qualification.
- Read PE metadata and SHA-256 through one held file stream. Extend existing
  native controls so the exact reporter inventory remains stable. Open a
  Linux pidfd before rechecking birth and send only through that handle;
  never fall back to numeric PID signalling.
- Asset staging must finish child cleanup before fixture deletion even if
  the caller's cancellation token has fired. Keep cleanup cancellation
  independent from the caller and retain successful staging assertions.
- Verify Python reader/lifecycle discovery and focused Debug C# controls;
  use the pinned module tool for local source mappings. Independent blind
  review and pinned commitlint validation precede the authorized push.
- Prerequisites: committed dependency selections are unchanged; no Epic 8
  producer availability or parity status is promoted by this maintenance.
- Touched repos: Parties only. Rollback path: revert the maintenance commit;
  there is no persisted domain-state or configuration migration.
- Validation lanes: local Python and focused C# process/file controls.
  Non-goals: deployment, source/catalog approval and runtime qualification.
- Parity checklist: preserve reader roles, exact native case inventory,
  asset bytes, owner isolation and lifecycle withdrawal ordering.

- Final verification: 85 script controls, 111 lifecycle controls, three
  staging controls and four native assembly controls pass (203 total,
  zero failures/skips). Both focused source Debug builds have zero warnings
  and errors. Exact commands, source hashes and logs are retained in
  `tests/five-evidence-and-process-safety-2026-11-10/`.
- The unchanged genuine prior hosted artifact still passes the stricter
  reader with its original source/context and all 28 native cases. This
  is compatibility evidence, not a fresh runtime qualification.
- A no-restore build encountered HXW005 from mixed shared restore state;
  the normal pinned source restore/build corrected it without relaxing a gate.
- All five selected ledger entries are resolved. The CI-gate entry was
  reconciled against existing delivery; the other four received fixes.
- Pinned commitlint 21.2.4 validates the exact commit message with zero
  problems/warnings. No dependency or root gitlink changed.

## Review Triage Log

- R1 — medium, patch: the loaded-copy control could pollute concurrent
  native inventory. Load only control assemblies excluded by that inventory.
- R2 — medium, patch: an early-exiting staging parent could leave a live
  descendant. Reuse the existing exclusive subreaper and add a detached-child
  control that verifies reaping before return.
- R3 — medium, patch: cleanup timeout could pass as expected cancellation.
  Report a distinct TimeoutException and assert the exact caller token.
- R4 — low, patch: process metadata could disappear between existence
  check and read. Accept disappearance during the direct read.

All four findings were fixed. The independent follow-up static review found
no remaining confirmed defects; the rebuilt C# controls pass. No findings
were deferred. See `tests/five-evidence-and-process-safety-2026-11-10/review.json`.
