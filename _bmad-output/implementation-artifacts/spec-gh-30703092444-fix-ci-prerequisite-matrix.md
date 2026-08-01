---
title: 'Fix CI payload-protection prerequisite drift'
type: 'bugfix'
created: '2026-08-01'
status: 'done'
baseline_commit: '31a2f372285c7772a7a7f9f6cbec55a59bfae3ef'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/planning-artifacts/sprint-change-proposal-2026-08-01.md'
  - '{project-root}/references/Hexalith.EventStore/_bmad-output/planning-artifacts/story-id-migration-2026-08-01.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** GitHub Actions run `30703092444` builds successfully and passes Tier 1, but three `PlatformApiPrerequisitesTests` fail because the Parties G5 payload-protection matrix still describes EventStore's removed umbrella Story 8.2 identity, admits a test file in the wrong evidence-path category, and requires legacy tokens that are absent from its reproducible validation column. This stale governance evidence blocks the Tier 2 gate even though no production behavior failed.

**Approach:** Rebind the living G5 matrix and open retention ledger to the EventStore `8f004ecf` story decomposition, and strengthen the fitness contract around the actual present seams, future package owners, and Story 8.11 availability authority. Keep G5 `needs-additive-api`, Parties Story 8.7 blocked, and all local crypto/key-management rollback code intact.

## Boundaries & Constraints

**Always:** Preserve the user-owned edits to `8-6-projection-and-query-sdk-migration.md` and the FrontComposer gitlink; distinguish current contract/no-op seams from an implemented payload-protection engine; make every matrix `rg -n -F` command reproducible by the existing parser; retain exact blocked/backlog and no-deletion semantics.

**Ask First:** Any production source change, EventStore or FrontComposer submodule modification/pointer update, G5 status promotion, Story 8.7 unblocking, package-inventory change, or rollback-path deletion requires explicit approval.

**Never:** Do not make `CryptoShreddingWorkflowState` stand in for engine/package delivery, weaken or skip the fitness tests, claim the proposed `pdenc-v2` design is implemented, add source-mode CI workarounds, or alter unrelated CI/CD/release work.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Current owner state | EventStore `8f004ecf`, Story 8.1 in progress, Stories 8.2–8.11 backlog | Matrix proves existing interfaces/no-op provider, future package ownership, and Story 8.11 closure authority while remaining `needs-additive-api` | Any missing or changed owner evidence fails the fitness gate |
| Local rollback evidence | Parties provider and compatibility harness remain present | Source evidence stays in the allowed Parties path and the harness stays in validation evidence rather than the owner-path column | Missing rollback evidence blocks migration and keeps Story 8.7 blocked |
| Future delivery | EventStore later changes status, package inventory, or G5 authority | Fixed-string validations fail visibly until the living matrix is deliberately revalidated | Never silently promote availability or delete local protection code |

</frozen-after-approval>

## Code Map

- `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md` -- living owner/consumer evidence; its G5 row contains all three failing inputs.
- `tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs` -- parses matrix paths and commands and pins required G5 evidence tokens.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` -- open crypto-retention ledger that must match the matrix's blocked owner state.
- `references/Hexalith.EventStore/_bmad-output/planning-artifacts/story-id-migration-2026-08-01.md` -- canonical 8.2–8.11 ownership crosswalk; inspection only.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md` -- replace stale G5 identity/status/proof references with current root-gitlink and story-decomposition evidence; remove the harness from the owner/context path column but retain focused validation; preserve the blocked rollback decision.
- [x] `tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs` -- replace the misleading workflow-state token with stronger current/future package, compatibility-format, and G5-closure tokens without relaxing path ownership or command reproducibility.
- [x] `_bmad-output/implementation-artifacts/sprint-status.yaml` -- synchronize the open retention comment with EventStore `8f004ecf`, the 8.2–8.11 backlog chain, and Story 8.11's sole closure authority; leave action text, owner, and status unchanged.

**Acceptance Criteria:**
- Given the current EventStore gitlink and unchanged production source, when `PlatformApiPrerequisitesTests` run, then all twelve tests pass and each G5 command reproduces its claimed match or absence.
- Given the repaired evidence, when reviewers inspect the matrix and ledger, then they cannot mistake legacy workflow contracts or a proposal for a delivered engine, package, or G5 approval.
- Given future owner drift, when a status key, planned package, format contract, or closure authority changes, then focused fitness validation fails rather than silently accepting stale evidence.

## Spec Change Log

## Design Notes

The fitness contract should test semantic ownership, not preserve a historical symbol merely to turn CI green. Existing `IEventPayloadProtectionService` and the no-op provider prove extension seams only; planned `Hexalith.EventStore.PayloadProtection` packages and Story 8.11 describe the unavailable target. `pdenc-v2`, `json+pdenc-v1`, policy, erasure-state, and Parties provider checks preserve migration/rollback truth without claiming delivery.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --no-restore -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` -- expected: zero warnings and errors.
- `dotnet tests/Hexalith.Parties.Tests/bin/Release/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.PlatformApiPrerequisitesTests` -- expected: 12/12 pass.
- `pwsh scripts/test.ps1 -Lane integration -ContinueOnFailure -Properties NuGetAudit=false,MinVerVersionOverride=1.0.0` -- expected: both integration projects pass, including Parties 452/452 and Sample 58/58.
- `pwsh scripts/test.ps1 -Lane ci -ContinueOnFailure -Properties NuGetAudit=false,MinVerVersionOverride=1.0.0` -- expected: the CI contract project passes 31/31.
- `dotnet build tests/Hexalith.Parties.Security.Tests/Hexalith.Parties.Security.Tests.csproj --configuration Release --no-restore -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` followed by direct execution of `CryptoKeyManagementCompatibilityHarnessTests` -- expected: zero warnings/errors and 19/19 pass.
- `bash scripts/check-no-warning-override.sh` -- expected: build safeguards pass.
- `git diff --check -- _bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md _bmad-output/implementation-artifacts/sprint-status.yaml tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs` -- expected: no whitespace errors.
- `git diff --no-index --check -- /dev/null _bmad-output/implementation-artifacts/spec-gh-30703092444-fix-ci-prerequisite-matrix.md` -- expected: exit 1 because the spec is new, with no whitespace-error output.

## Suggested Review Order

**G5 owner state**

- Start with the repaired owner/consumer contract and its reproducible evidence trail.
  [`story-8-3-platform-api-prerequisite-matrix.md:43`](story-8-3-platform-api-prerequisite-matrix.md#L43)

- Confirm the retention ledger preserves blocked migration and local rollback semantics.
  [`sprint-status.yaml:248`](sprint-status.yaml#L248)

**Fail-closed verification**

- Review the closed inventories for owner statuses, absent delivery, and rollback paths.
  [`PlatformApiPrerequisitesTests.cs:25`](../../tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs#L25)

- Verify gitlink, authority, package, registration, and ledger drift all fail CI.
  [`PlatformApiPrerequisitesTests.cs:1066`](../../tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs#L1066)
