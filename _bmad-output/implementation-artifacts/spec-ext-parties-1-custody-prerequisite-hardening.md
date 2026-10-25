---
title: 'Harden existing actor-history lifecycle prerequisites'
type: 'bugfix'
created: '2026-10-25'
status: 'done'
qualification_blocked: true
complete_owner_delivery: false
route: 'dispatch'
baseline_commit: '13a6247aff26a77c80d0308b6c69af2bc3e20279'
review_loop_iteration: 0
context:
  - /home/administrator/projects/hexalith/parties/AGENTS.md
  - /home/administrator/projects/hexalith/platform/references/Hexalith.AI.Tools/hexalith-llm-instructions.md
---

<frozen-after-approval reason="user explicitly requested all independently actionable source work and review">

## Intent

Existing Platform actor-history contracts allow authority regression and new decrypting key generations after expiry. Harden those source invariants while the complete EXT-PARTIES-1 installation remains blocked. The user separately authorized a concrete provisioning proposal; the parent session prepares that proposal and owner decision matrix.

## Boundaries & Constraints

Preserve Branch B, immutable tenant/Party identity, exact original binding-effective-at plus 31,536,000 seconds and exclusive expiry. Preserve all pre-existing work in every checkout; no Git mutation, dependency update, deployment, credential retrieval or invented owner approval. Work in the sibling Platform repository that owns these contracts. No skill may be loaded from references. Existing ordinary baseline documentation there is permitted.

This change supplies local contract validation only. It neither installs custody nor supplies an independent nonrollback authority. Constructors/deserialization cannot establish currentness without the external owner authority. Test custody remains confined to local regression tests. Do not alter production DI, expiry policy, the accepted register, previous evidence or general interaction-custody work. Do not forbid pre-expiry destruction receipts: tenant erasure boundaries remain Q7.

## I/O & Edge-Case Matrix

| State/input | Expected behavior |
| --- | --- |
| Legal forward lifecycle transition, higher revision, lower epoch | Reject without mutating original record |
| Same epoch, lower fence | Reject without mutating original record |
| Higher epoch and lower nonnegative fence | Accept as a new epoch-local fence; both coordinates remain distinct |
| Same epoch/fence, legal higher revision | Preserve existing transition behavior |
| Active unit, increasing generation | Add generation with identical original unit/deadline |
| Expired, PendingDestruction, Destroyed or ReceiptFinal, unknown generation | Reject; never mint post-expiry decrypting authority |
| Any lifecycle state, known generation retry | Return same record with no new generation/revision |

</frozen-after-approval>

## Code Map

- Sibling `../platform/src/Hexalith.Platform.Custody/IdentityHistoryLifecycleRecord.cs`: `Move` validates revision/state but not authority regression; `AddGeneration` validates unit/order but not lifecycle state. Scope all edits to these methods and their XML documentation.
- Sibling `../platform/tests/Hexalith.Platform.Custody.Tests/IdentityHistoryCustodyPolicyTests.cs`: existing immutable-unit helpers, lifecycle tests and test proving production provider remains absent. Extend this class using existing xUnit/Shouldly conventions, one type per file.
- Sibling Platform `Directory.Build.props`, `Directory.Packages.props`, `global.json`, custody project and test project define .NET 10.0.402, Debug source references and warnings as errors. No root .editorconfig or AGENTS exists there; use the loaded baseline. Consult the existing architecture/spec before implementation: sibling `_bmad-output/specs/spec-production-actor-history-custody/{SPEC,lifecycle-contract,implementation-plan,test-plan}.md`.

## Tasks & Acceptance

- [x] Guard `Move` against lower epochs and lower fences in the same epoch; document epoch-local fence comparison.
- [x] Guard `AddGeneration` against unknown generations outside Active; preserve exact known-generation retries in every state.
- [x] Add meaningful tests covering every matrix row, unchanged original facts and rejection of foreign units. Run the focused class and existing cleanup tests.
- [x] Record exact commands, full canonical checkout HEADs, scoped diff and test evidence in a new directory under sibling `docs/implementation/ext-parties-1-custody-proposal-2026-10-25/source-verification/`. Use fresh artifact paths outside the repository so concurrent build outputs are not overwritten. Report failure blockers verbatim and use the baseline fallback ladder if needed.

Given these contracts, when an invalid transition or generation is attempted, then it cannot grant renewed authority. Given missing production custody, when source tests pass, then Available and installed qualification remain false.

## Implementation Notes

User authorization to preserve existing dirty work supersedes the workflow's clean-tree checkpoint. Explicit request to implement actionable source work supplies authorization to proceed with this bounded spec; no provisioning or unresolved owner choice is approved. The Platform baseline revision must be obtained directly from Git before editing and recorded below; validate the parent baseline directly as well and correct a transcription error before any implementation if necessary.

Observed Platform planning HEAD from Git: `f84977132777decec2f45a199d9392de73b6cc84`. Concurrent external commits advanced both repositories during discovery; these observations are source provenance, not accepted or installed targets.

Parent implementation also removes `-Force` from the evidence-directory creation in `eng/verify-ext-parties-1.ps1`. Each attempt must reserve a new directory before writing any evidence. Probe fresh Live refusal, repeat attempt and pre-existing sentinel preservation. This directly implements the user's evidence-preservation requirement and does not implement Live qualification. The parent prepares the additive sibling Platform provisioning proposal, exhaustive unresolved decision/command ledger and discovery/provenance evidence; include these files in the full review.

Independent review demonstrated that directory creation alone races. Patch both verifier entry points with exclusive permanent file reservation, let direct readiness own its reservation, and add repeatable bounded external-process tests in the existing Ci.Tests project. No protected call may precede reservation. Retain original pre-review evidence separately from post-review tests. Review also requires actual Pod runtime image observations, device isolation/maintenance/capacity and transient-plaintext feasibility criteria in the proposal. These elaborate existing preservation/all-copy requirements, without approving a backend or resource.

## Spec Change Log

## Review Triage Log

All three required layers reviewed `/tmp/ext-parties-custody-review-ce2p637m/authorized-work.diff` (507,076 bytes). Every finding is recorded individually; no finding was silently merged or deferred into concurrent work.

| ID | Verdict / route | Evidence and disposition |
| --- | --- | --- |
| BH-01 | medium / patch | Deployment templates do not prove runtime images. Add Pod and ReplicaSet observations and required exact ownership/rollout/imageID comparison; actual comparator stays B03/B09-blocked. |
| BH-02 | medium / patch | Root erasure cannot remove an exported DEK or paused plaintext holder. Require hardware-contained AEAD, no DEK export, explicit transient holders and pause/crash/expiry feasibility; current .NET hosts do not provide this guarantee. |
| BH-03 | medium / patch | Volatile roots cannot survive ordinary reset/update maintenance. Propose side-by-side pair drain through every last original deadline; unschedulable security updates remain an explicit feasibility rejection condition. |
| BH-04 | medium / patch | Pair count proves neither memory nor throughput. Add bounded engineering cohort, measured memory/timer/throughput limits and refusal-before-allocation on exhaustion; production capacity remains unapproved. |
| BH-05 | medium / patch | Shared staging/production devices expose production to staging reset/fault/capacity. Propose isolated pairs and replacement pairs per environment, with distinct signing and reset scopes. |
| BH-06 | medium / patch | Retained runner opened logs with wb, allowing repeated labels to overwrite original evidence. Preserve original runner/manifest, use xb and verify unchanged log/journal on repeat. |
| BH-07 | low / patch | Initial probe JSON did not name the script digest input. Reconstruct and hash-match exact reviewed script bytes, preserve them and name the meaning in an additive identification record. |
| BH-08 | medium / patch | Historical verifier commands omit their synthetic env setup and repeatable assertions. Preserve explicit nonsecret assignments and add repeatable process tests. |
| BH-09 | low / patch | A preservation baseline needs an after-comparison receipt. Retain a final comparison of the exact 281 original files; report any concurrent drift independently. |
| BH-10 | medium / patch | Direct readiness had its own final Force/write path. Reserve at helper startup and remove the late directory creation; parent delegates reservation exactly once. |
| EC-01 | medium / patch | Synchronized runspaces demonstrated multiple directory-creation successes on one target. Exclusive CreateNew marker must precede every write, with a deterministic competing-reservation test. |
| EC-02 | medium / patch | Repeated runner label replaces a previously hashed failed log. Same verified defect as BH-06; preserve and repair without rewriting original raw logs. |
| VG-01 | medium / patch | No repeatable CI process test would fail if Force returned. Add tests asserting exact file bytes/contents across changed-input retries and both readiness routes, rather than merely checking nonzero exit. |
| R2-01 | medium / patch | Follow-up review found the new test harness failed to propagate nested script exit 1. Explicit LASTEXITCODE propagation repairs it; original seven-failure XML/fixtures remain preserved and all nine final cases pass. |

Final verification: 136 lifecycle/policy/cleanup cases and 9 verifier process cases passed, zero skips/unrun cases; both Debug builds had zero warnings/errors. The edge reviewer inspected final guards, synchronized fixtures and original-runner/log hashes and reported both initial defects repaired with no remaining source defect. This review proves local source behavior only. All backend/authority/operator/installation and full acceptance gates remain open. No commit, push or other Git mutation was made: the user's preservation requirement controls the workflow's optional completion action.

## Verification

Build the Platform custody test project in Debug with `UseHexalithProjectReferences=true`, `HexalithEventStoreRoot=/home/administrator/projects/hexalith/eventstore`, `NuGetAudit=false`, `-m:1` and a fresh `--artifacts-path`. Invoke its built xUnit v3 assembly with `-class '*IdentityHistoryCustodyPolicyTests' -class '*IdentityHistoryCleanupTests' -result-xml <fresh-path>`. All selected tests must execute without failure or skip. Broader tests are needed only if the focused evidence exposes a relevant regression.
