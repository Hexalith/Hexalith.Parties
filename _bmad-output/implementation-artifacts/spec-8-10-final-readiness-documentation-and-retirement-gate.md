---
title: '8.10 Final readiness, documentation, and retirement gate'
type: 'chore'
created: '2026-07-07T00:00:00+02:00'
status: 'draft'
review_loop_iteration: 0
followup_review_recommended: false
context:
  - '{project-root}/_bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-8-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md'
warnings:
  - blocked-prerequisite
---

<intent-contract>

## Intent

**Problem:** Epic 8 needs a closing gate so the domain-focus refactor is complete, releasable, and not open-ended — with exact pins, validation commands, public compatibility, rollback paths, and deferred-work owners recorded, docs regenerated to the new project inventory, new invariants pinned by fitness tests, and release confirmed or blocked.

**Approach:** Run last; close or explicitly defer the remaining 8.1–8.9 work with owners/proof/rollback/evidence; regenerate/update docs to the new inventory; update fitness tests + `sprint-status.yaml`; run final verification across build, focused-test, package/API, topology, deploy, and UI-accessibility lanes — changing no PRD FR coverage.

## Boundaries & Constraints

**Always:** Preserve spine invariant I15 — Epic 8 adds zero PRD functional requirements and is never reported as MVP feature delivery — and keep Epics 1–5 as the feature-readiness baseline. Every deferred item carries an owner, proof requirement, rollback path, and validation evidence. Fitness tests pin the new project inventory + invariants (I1–I15). Keep the build gate (`check-no-warning-override.sh`), `.slnx`, and CPM intact.

**Block If:** Any of Stories 8.6–8.9 is incomplete AND lacks an explicit deferral record (owner + proof + rollback + evidence); a fitness test cannot pin a claimed new invariant; or final release verification fails without a recorded, owner-assigned blocker. HALT and record the blocker rather than marking Epic 8 done.

**Block If — available-row identities:** HALT final closure if any of the four named `available` rows — EventStore domain-service host, EventStore DataProtection, Commons HTTP helpers, or Builds shared props/targets — lacks a recorded release/root-gitlink identity for an actually consumed surface, if that identity differs from the dependency used by Parties, or if an unconsumed surface lacks an explicit owner/proof/rollback deferral.

**Never:** Do not change PRD FR coverage or Epics 1–5. Do not mark Epic 8 `done` while blockers lack an owner/deferral. Do not relax the build gate, warnings-as-errors, or the deploy poison-sweep to force a green. Do not delete rollback paths whose parity was never proven — record them as deferred instead.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|----------------------------|----------------|
| Closure with all done | 8.6–8.9 complete + evidence | Final readiness records pins/commands/compatibility/rollback; Epic 8 → done | — |
| Closure with deferrals | Some rows deferred | Each deferral has owner/proof/rollback/evidence; Epic 8 closes with an explicit deferred set | Missing owner → Block If |
| Docs regen | New project inventory | `docs/` + component inventory reflect removed platform mechanics; DocumentationFitnessTest green | Stale doc → fitness fail |
| Release verify | All lanes run | Build/focused-test/package-API/topology/deploy/ui-a11y pass or block with recorded reason | Fail without owner → HALT |

</intent-contract>

## Code Map

- `docs/architecture.md`, `docs/component-inventory.md`, `docs/development-guide.md`, `docs/deployment-guide.md`, `docs/deployment-security-checklist.md`, `docs/gdpr-*.md` -- regenerate/update to the post-Epic-8 inventory.
- `tests/Hexalith.Parties.DeployValidation.Tests/DocumentationFitnessTest.cs`, `CarveOutPreservationFitnessTest.cs`, `DaprAccessControlFitnessTests.cs`, `tests/Hexalith.Parties.Tests/FitnessTests/RetiredLeafProjectFitnessTests.cs` -- pin the new invariants/inventory.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` -- set Epic 8 + story rows; `epic-8-retrospective`.
- `_bmad-output/implementation-artifacts/deferred-work.md`, `story-8-3-platform-api-prerequisite-matrix.md`, `tests/.../test-summary.md` -- record pins, deferrals, owners, validation evidence.
- `README.md` -- update project inventory + GDPR/KMS notice if changed.

## Tasks & Acceptance

**Execution:** (gated — run only after 8.6–8.9 are done or explicitly deferred)
- [ ] Reconcile each 8.6–8.9 outcome: done-with-evidence OR deferred-with-owner/proof/rollback in `deferred-work.md` + the 8.3 matrix.
- [ ] Reconcile the Story 8.5 historical host pin, the Story 8.6 DataProtection identity, and the Story 8.8 Commons HTTP/Builds identities against the final package/root-gitlink dependency graph; record an explicit deferral instead for any unconsumed surface.
- [ ] Regenerate/update `docs/` + component inventory; make `DocumentationFitnessTest` green.
- [ ] Update fitness tests to pin the new inventory + invariants; update `sprint-status.yaml` (Epic 8 + retrospective).
- [ ] Run the full lane set; record exact commands/results + pins in `test-summary.md`; confirm or block release.

**Acceptance Criteria:**
- Given any 8.6–8.9 item is incomplete without a deferral record, when 8.10 runs, then it HALTs `blocked` naming the item + owner gap.
- Given any of the four named `available` rows, when final readiness runs, then its release/root gitlink matches the dependency actually consumed or its explicit deferral records owner, proof, rollback, and evidence; otherwise 8.10 HALTS.
- Given closure, when Epic 8 is marked done, then final readiness records pins, validation commands, public compatibility, rollback paths, and deferred-work owners, and PRD FR coverage is unchanged.
- Given the docs + fitness tests, when validated, then they reflect the new project inventory and pin the I1–I15 invariants.
- Given final verification, when the lanes run, then each either passes or blocks with a recorded, owner-assigned reason.

## Design Notes

- **§4 gate mapping:** (1) Prereq: 8.6–8.9 done or explicitly deferred, with the four named available-row identities reconciled to actual consumption or explicit deferral. (2) Repos: `Parties` (+ pins recorded for all touched submodules). (3) Rollback: unproven-parity paths recorded as deferred, not deleted. (4) Lanes: build, focused xUnit v3 EXEs, package/API tests, topology, deploy, `ui-a11y`. (5) Non-goals: no PRD/feature change; no new migration. (6) Parity checklist: confirms I1–I15 pinned by fitness tests.

## Verification

**Commands:**
- `bash scripts/check-no-warning-override.sh` -- expected: build gate intact.
- `pwsh scripts/test.ps1 -Lane all` -- expected: pass or block-with-reason across lanes (Docker-dependent lanes skip gracefully; a skip is not a pass).
- `dotnet build Hexalith.Parties.slnx -c Release -m:1` -- expected: green, warnings-as-errors clean.

**Manual checks:**
- Confirm `sprint-status.yaml` Epic 8 rows are current, every deferral has an owner, and `docs/` reflects the post-refactor inventory.
