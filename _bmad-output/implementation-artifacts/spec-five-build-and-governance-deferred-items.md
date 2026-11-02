---
title: 'Close five deferred build and governance gaps'
type: 'bugfix'
created: '2026-11-02'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** DW-199 through DW-203 leave an Epic 8 spec outside its required readiness gate, a projection-retirement guard incomplete, a stale Tenants version table, and two build lanes vulnerable to shared restore state.

**Approach:** Close these five entries as one authorized batch. Record the six required gate clauses and honest evidence limits, preserve the typed SDK registration decision while guarding retired interface reintroduction, derive the Tenants row from the effective catalog, isolate the Admin UI and package-probe builds, and align the EventStore package selection to the latest release matching its pinned source. Verify focused and canonical lanes, review the result, reconcile the ledger, then commit and push main.

</frozen-after-approval>

## Implementation Notes

- Starting clean `main` revision `d06c316fd57cbfaa0b80cf8622ec0b8ec571c6a0`; user authorized five deferred entries and push, then explicitly approved aligning EventStore to latest on 2026-11-02. The batch changes tests, docs, solution composition, test build behavior and the EventStore package selection. No public API, data migration, gitlink change or deployment is needed. The existing DW-33 owner decision favors typed SDK checks; do not restore the old string-name DI exclusions. This bounded, reversible footprint uses `oneshot`.
- Code map: `_bmad-output/implementation-artifacts/spec-five-verification-maintenance-deferred-items.md` needs a retrospective §4 record; `tests/Hexalith.Parties.Tests/FitnessTests/ArchitecturalFitnessTests.cs` guards only retired paths; `ProjectionPlatformAdapterTests.cs` keeps typed SDK registration checks. `docs/architecture.md` and `DocumentationFitnessTests.cs` govern the Tenants table. `Hexalith.Parties.slnx` lists native Admin.UI directly while `tests/Hexalith.Parties.EventStoreAdminUI.TestHost/*.csproj` builds it again. `ClientPackageTests.cs` and `ContractsPackageTests.cs` pack in-repo projects with temporary NuGet caches; `PackageTestProcess.cs` is the shared fixture helper.
- I17 prerequisites: the original five-verification spec operated against already-delivered EventStore APIs and prior Epic 8 slices; its historical closure receipts remain historical, not current parity proof. For this correction, use committed EventStore gitlink `7c93a1234d14c66f5ee85ed48caaee1d30a671f3`, which is upstream tag `v3.132.2`, and the same published package `3.132.2`; use Tenants gitlink `4b3e431745550f8466f1ce0d70e6b507c747b59a` and package `6.0.0`. I16 selection checks still govern any validation claim.
- I17 touched repositories: Parties only. Read the root-declared EventStore, Tenants and Builds checkouts; do not edit or advance any gitlink. The external runtime owner and all other root submodules remain untouched.
- I17 rollback path: retain the SDK handlers and typed DI registrations. Revert test/docs changes independently. If 3.132.2 regresses, retain the solution's single Admin UI build and restore the prior EventStore package `3.130.2` together with source tag `v3.130.2`; the old package cannot compile the newer Admin UI source. An isolated tree based on the starting Parties revision exercised this compatible fallback: Release solution restore/build passed with zero warnings/errors, and 130 named projection, architecture, documentation, gateway and health tests passed. The test-only source override was not an approved root gitlink, so this proves technical rollback viability without current I16 parity credit. Exact commands and identities are in `tests/five-build-and-governance-deferred-items-2026-11-02/rollback.md`.
- I17 validation lanes: direct xUnit v3 `DocumentationFitnessTests`, `ArchitecturalFitnessTests`, `ProjectionPlatformAdapterTests`, the Client and Contracts package test classes, focused Admin UI TestHost build, Release `Hexalith.Parties.slnx` restore/build and `scripts/test.ps1 -Lane all`. The package update changes the runtime graph; topology, deploy and `ui-a11y` were not run and receive no parity credit. Never replace a failed canonical lane with a focused pass.
- I17 non-goals: no deletion of SDK or local rollback seams, no EventStore/Tenants/Builds source edits, no other dependency or gitlink changes, no release or deployment, and no new Epic 8 closure claim.
- I17 parity checklist: Parties' I5-I10 and I8 domain/query/export/erasure source is unchanged, but the selected EventStore package changed, so behavior at the new graph needs its own parity evidence. Preserve the named I18 surfaces and the red current I16 identity checks; do not infer runtime or publication parity from test/build repairs.
- Replan result: removing the duplicate Admin.UI solution entry exposed a distinct source/package API skew. EventStore source `7c93a1234d14c66f5ee85ed48caaee1d30a671f3` calls `AddEventStoreBoundedInvocationResilience`; the prior 3.130.2 ServiceDefaults package lacks it. Both canonical Release solution and focused TestHost builds failed CS1061 at `AdminUIServiceExtensions.cs:118` with zero warnings. The user selected latest alignment; the NuGet feed reported `3.132.2` as latest and upstream `v3.132.2` resolves to the already committed source gitlink. The package pin and catalog capability allowlists now select `3.132.2`.
- Final build verification: focused TestHost and canonical Release solution restore/build pass with zero warnings/errors. After review patches, 45 focused architecture/documentation/projection tests, 5 Contracts package tests and 2 Client package tests pass. The four in-repo source projects inspected retain their normal NuGet package folder in `obj/project.assets.json`; neither package fixture path appears there. The canonical ordered all lane built and ran IntegrationTests after Client.Tests: 207 passed, zero failed, with opt-in skips.
- Full `-Lane all` result: 15 of 16 test projects passed and one failed three `PlatformApiPrerequisitesTests` assertions for the pre-existing current I16 selection mismatch (Platform, McpCli and the old package graph stamp). These tests were already red before this correction. The new matrix observation records the user-authorized EventStore package/source match without altering the dated owner-approved I16 table. DW-182 and the new flat review deferral retain the owner-signoff work. No current Epic 8 parity, runtime topology, deployment or release qualification is claimed. Exact commands and results are in `tests/five-build-and-governance-deferred-items-2026-11-02/validation.md`.

## Review Triage Log

- Medium, patch: the first rollback note omitted the package/source fallback. It now identifies the matching `v3.130.2` source and package pair, and an isolated fallback Release build plus 130 selected tests passed; formal I16 owner approval remains deferred.
- Medium, patch: the first spec called topology and deploy unaffected although the EventStore package changed. It now records the changed runtime graph and assigns no topology, deploy or accessibility parity credit.
- Medium, patch: the 3.132.2 selection lacked a matrix observation. The new dated EventStore observation records the matching package and root gitlink while preserving the historical approved selection.
- Medium, defer: current Platform/McpCli gitlinks and the package graph stamp differ from the old I16 assertions. The three failures reproduce before this correction, and DW-182 requires owner reconciliation; a new flat deferred entry keeps this review finding visible without inventing approval.
- Low, patch: the retrospective prerequisite clause omitted available G3/G6/G10 projection/query SDK and G12 build-catalog rows. It now identifies those rows and keeps G8 as needs-additive-api.
- Low, patch: a naive source regex treated ordinary comments or strings as retired declarations. The anchored declaration guard and theory cases reject those inputs.
- Low, patch: a declaration with an inline comment between `interface` and the retired name escaped the first guard. The revised guard and theory case detect it.
- Medium, patch: package fixture tests lacked a regression assertion for shared assets. Both fixtures now check every in-repo source project's normal assets after packing; all seven focused package tests pass.
