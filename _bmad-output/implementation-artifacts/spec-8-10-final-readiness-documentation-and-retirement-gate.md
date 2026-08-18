---
title: '8.10 Final readiness, documentation, and retirement gate'
type: 'refactor'
created: '2026-08-17'
status: 'in-review'
baseline_commit: '37f4ec826c6f4aea4651cfbad94fb6ab7fc4f0a0'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md'
  - '{project-root}/_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Epic 8 cannot close while 8.7/8.8 are blocked, 8.9 is backlog, deferrals are incomplete, four dependency receipts are stale, and docs describe retired surfaces.

**Approach:** Use a preflight-first, deferral-based closure: reconcile immutable identities and explicit deferrals, refresh docs and executable fitness coverage, then record green validation or leave Epic 8 open. Add no PRD functional requirement.

## Boundaries & Constraints

**Always:** Preserve I1-I15, public compatibility, `.slnx`, CPM, warnings-as-errors, and root-only submodules. Keep current 8.7-8.9 statuses unless their own evidence supports another existing status. Each deferral names an owner, exit proof, rollback, and evidence; record package and source identities separately.

**Ask First:** Dependency, submodule, owner-repository, rollback-deletion, owner-commitment, or PRD changes.

**Never:** Treat a matrix label, checkout, compile, skip, or historical pin as consumption proof. Do not restore retired deploy assets, invent a deploy lane, weaken gates, mark 8.7-8.9 done, or close 8.10/Epic 8 with missing evidence.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Deferral closure | 8.7-8.9 incomplete | Accepted deferrals preserve rollback and permit closure | Missing field leaves Epic 8 open |
| Identity check | Package/source graph consumed | Matrix matches exact releases/gitlinks and consumers | Mismatch blocks closure |

</frozen-after-approval>

## Code Map

- `_bmad-output/implementation-artifacts/{sprint-status.yaml,story-8-3-platform-api-prerequisite-matrix.md,deferred-work.md}` -- statuses, stale receipts, open G4-G11 gates, and deferral ledger.
- `Directory.Build.props:19`, `Directory.Packages.props:5`, `src/Hexalith.Parties/Program.cs:18`, `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs:159`, `src/Hexalith.Parties.Client/HttpPartiesCommandClient.cs:401` -- selected dependency modes and consumers.
- `_bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md:50`, `docs/{architecture,source-tree-analysis,index,data-models,getting-started,api-contracts,component-inventory}.md`, `README.md` -- stale ACL, actor, inventory, and deployment claims.
- `tests/Hexalith.Parties.Tests/FitnessTests/` -- reuse current boundaries and add documentation, identity, deferral, zero-PRD, and I1-I15 gates; retired DeployValidation tests remain deleted.
- `scripts/test.ps1`, `_bmad-output/implementation-artifacts/tests/test-summary.md` -- lane inventory and final evidence.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/implementation-artifacts/{story-8-3-platform-api-prerequisite-matrix.md,deferred-work.md,sprint-status.yaml}` -- reconcile 8.6 and append its accepted residual-review-debt umbrella; append accepted 8.7-8.9 and external-runtime deferrals; record EventStore package `3.95.0` and source `454b4d100c8c095abf5077c6a8d408da6681e87e`, Commons HTTP source `6fbac0c5dff2b8a58e90732c51b31911421a8a65`, and Builds catalog `17b1c7aae3e1854e464f17bd88d527f8350ea203`. Halt if incomplete.
- [x] `docs/*.md`, `README.md`, and the Epic 8 spine -- document 13 source projects, 15 runnable tests plus one support host, SDK projection/query routes under the deny-default EventStore-only ACL, and external runtime deployment ownership.
- [x] `tests/Hexalith.Parties.Tests/FitnessTests/{DocumentationFitnessTests,EpicEightClosureFitnessTests,PlatformApiPrerequisitesTests}.cs` -- pin maintained paths/inventory, actual dependency selection, deferral completeness, I15, and an executable-or-deferred I1-I15 map.
- [x] `_bmad-output/implementation-artifacts/tests/test-summary.md` and `sprint-status.yaml` -- append exact pins/results/rollback/blockers; close 8.10/Epic 8 only after every assertion and required lane passes.

### Review Findings

- [x] [Review][Patch] Advance and pin references/Hexalith.Builds SHA 17b1c7aae3e1854e464f17bd88d527f8350ea203 and reconcile submodule gitlinks in matrix, tests, and documentation [tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs:1468]
- [x] [Review][Patch] Deletion of MainLayout.razor.css removes deep focus-visible and forced-colors rules on body controls [src/Hexalith.Parties.UI/Components/Layout/MainLayout.razor.css:1-44]
- [x] [Review][Patch] AccessibilityStyleGuardTests brittle static file read on external FrontComposer stylesheet [tests/Hexalith.Parties.UI.Tests/AccessibilityStyleGuardTests.cs:58-67]
- [x] [Review][Patch] EpicEightAddsNoPrdFunctionalRequirement fragile git diff on shallow CI checkout [tests/Hexalith.Parties.Tests/FitnessTests/EpicEightClosureFitnessTests.cs:170]
- [x] [Review][Patch] DocumentationFitnessTests omits docs/project-overview.md from project inventory verification [tests/Hexalith.Parties.Tests/FitnessTests/DocumentationFitnessTests.cs:11-24]
- [x] [Review][Patch] Deferrals table in story-8-3... not verified against deferred-work.md in fitness tests [tests/Hexalith.Parties.Tests/FitnessTests/EpicEightClosureFitnessTests.cs:150]

**Acceptance Criteria:**
- Given an incomplete 8.6-8.9 disposition, when closure fitness runs, then any missing owner, proof, rollback, or evidence names the gap and prevents closure.
- Given package/source modes, when identity fitness runs, then receipts equal selected dependencies and unconsumed surfaces have explicit deferrals.
- Given maintained docs and I1-I15, when fitness runs, then current topology/inventory and zero-PRD scope map to executable evidence or a named external deferral; failures and skips remain owner-visible.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Release --no-restore -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 && dotnet tests/Hexalith.Parties.Tests/bin/Release/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.EpicEightClosureFitnessTests` -- expected: build and closure fitness pass; repeat the assembly command for `DocumentationFitnessTests` and `PlatformApiPrerequisitesTests`.
- `bash scripts/check-no-warning-override.sh && dotnet restore Hexalith.Parties.slnx && dotnet build Hexalith.Parties.slnx -c Release --no-restore -m:1` -- expected: pass.
- `pwsh -NoProfile -File scripts/test.ps1 -Lane all -Configuration Release -ContinueOnFailure -ResultsDirectory TestResults` -- expected: 15 projects pass; record topology skips.
- `pkg_dir=$(mktemp -d /tmp/parties-810-packages.XXXXXX); consumer_dir=$(mktemp -d /tmp/parties-810-consumer.XXXXXX); python3 scripts/pack-release-packages.py "$pkg_dir" 0.0.0-story810 && python3 scripts/validate-nuget-packages.py "$pkg_dir" && python3 scripts/validate-consumer-package-references.py "$pkg_dir" --work-directory "$consumer_dir"` -- expected: package/API compatibility passes.
- `npm ci --prefix tests/e2e && npm --prefix tests/e2e run typecheck && npm --prefix tests/e2e run test:a11y` -- expected: accessibility passes.
