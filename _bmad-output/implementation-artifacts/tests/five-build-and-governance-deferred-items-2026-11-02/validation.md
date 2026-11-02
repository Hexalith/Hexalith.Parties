# Five deferred build and governance items — validation, 2026-11-02

Starting Parties revision: `d06c316fd57cbfaa0b80cf8622ec0b8ec571c6a0` on clean `main`. The user authorized the batch, push to `main`, and EventStore alignment to latest. The committed root EventStore gitlink and checkout both read `7c93a1234d14c66f5ee85ed48caaee1d30a671f3`. The upstream `v3.132.2` tag resolves to that SHA, the NuGet flat-container feed reported `3.132.2` as the latest ServiceDefaults package, and effective Release MSBuild properties returned EventStore `3.132.2` and Tenants `6.0.0`.

| Check | Result |
| --- | --- |
| `dotnet build tests/Hexalith.Parties.EventStoreAdminUI.TestHost/Hexalith.Parties.EventStoreAdminUI.TestHost.csproj -c Release -m:1 --verbosity quiet` | Exit 0; 0 warnings, 0 errors |
| `dotnet restore Hexalith.Parties.slnx --verbosity quiet` | Exit 0 |
| `dotnet build Hexalith.Parties.slnx -c Release --no-restore -m:1 --verbosity quiet` | Exit 0; 0 warnings, 0 errors |
| `pwsh -NoProfile -File scripts/test.ps1 -Lane all -Configuration Release -ContinueOnFailure -ResultsDirectory /tmp/parties-five-deferred-latest-results` | Exit 1; 15 of 16 test projects passed. `Hexalith.Parties.Tests` had 2,832 passed, 3 failed, 4 skipped. The three failures are the existing `PlatformApiPrerequisitesTests` current-selection assertions described below. |
| Ordered all lane, `Hexalith.Parties.IntegrationTests.trx` | 207 passed, 0 failed, 21 opt-in skips after Client.Tests; no deleted fixture-cache MSB3106/CS0234 failure |
| Ordered all lane, `Hexalith.Parties.Ci.Tests.trx` | 523 passed, 0 failed |
| Post-review direct xUnit v3 `DocumentationFitnessTests`, `ArchitecturalFitnessTests`, `ProjectionPlatformAdapterTests` | 45 passed, 0 failed |
| Post-review direct xUnit v3 `ContractsPackageTests` and `ClientPackageTests` | 5 and 2 passed respectively, 0 failed |
| Post-review affected Release test project builds (main, Contracts and Client) | Each exit 0; 0 warnings, 0 errors |
| Four in-repo source `obj/project.assets.json` files after package tests | Commons.UniqueIds, EventStore.Contracts, Parties.Contracts and Parties.Client each list only `/home/administrator/.nuget/packages/` under `packageFolders`; no temporary fixture path |
| `git -c core.whitespace=cr-at-eol diff --check` | Exit 0 |

The three failed tests are `CommittedSourceSelections_RecordOverridesAndRefuseInvalidImmutableConsumption` for `Platform` and `McpCli`, and `CommittedSelectionObservations_PreservePackageGraphAndImmutableConsumptionRefusals`. They compare current root selections and package graph with the owner-approved 2026-11-01 I16 table. They failed in the baseline all lane before the EventStore alignment too; current Platform/McpCli gitlinks had advanced before this batch. DW-182 and the new flat review deferral retain the owner-signoff task. These failures cannot be changed into passing evidence by editing immutable expected identities without that signoff.

Before the EventStore package update, the focused TestHost and Release solution build failed at `AdminUIServiceExtensions.cs:118` with CS1061: selected EventStore source called `AddEventStoreBoundedInvocationResilience`, absent from the `3.130.2` ServiceDefaults package. The matching `3.132.2` package/source selection removed that compiler mismatch. The historical DW-199 rollback and the bounded DW-202 compatible fallback are recorded in [rollback.md](rollback.md). Neither exercises current topology, deployment, UI accessibility or full Epic 8 parity.
