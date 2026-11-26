---
title: 'Restore default Parties AppHost startup'
type: 'bugfix'
created: '2026-11-26'
status: 'done'
baseline_commit: 'b9c19657f4616ab5da18433159754a650f7f643e'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent">

## Intent

Restore the default Parties AppHost launch after `b9c19657`. Reproduce the NU1109 restore failures and align the two Microsoft.Extensions packages with Commons.ServiceDefaults 3.0.4 in the Parties central catalog. Preserve caller-selected catalogs, NuGet audits and the DW-97 edits.

Verify isolated startup, Parties and Dapr health, and cleanup of owned resources. Rerun the focused package checks for DW-50, DW-58, DW-82, DW-100 and DW-168. Record the effective versions, commands and results. Keep the five entries open for the required production evidence and owner approvals.

</frozen-after-approval>

## Implementation Notes

- Parties selected Commons 3.0.4 before importing the pinned Builds 6.5.1 catalog (`055d7df5b49669d5b705ce163b0b2e187ecb9a50`). The catalog contributed both Microsoft.Extensions pins at 11.0.0. With central transitive pinning enabled, NuGet could not satisfy the 11.0.1 minimums declared in the published Commons.ServiceDefaults 3.0.4 nuspec. This affected both Parties.UI and Parties.Mcp.
- `Directory.Packages.props` updates the two imported items after the shared catalog loads, under the same default-catalog and non-package-mode conditions as the Commons 3.0.4 selection. The update applies to the imported 11.0.0 metadata for each item. The architecture table and existing evaluated catalog checks cover the resulting pins.
- The DW-97 story, prerequisite matrix, spec and deferred-work ledger match their pre-task SHA-256 checksums. DW-50, DW-58, DW-82, DW-100 and DW-168 remain open for applicable deployed qualification, historical reconciliation, writer drain, production composition or cutover, and owner approvals.

## Verification

Commands ran in `run-with-temp` workspaces created from the baseline and its root-declared dependency revisions. The forward workspaces overlaid the Parties catalog, architecture table and routing test. Local logs and executable command records are in [TestResults](../../TestResults/apphost-startup-after-b9c19657/).

### Restore and catalog selection

```bash
app=src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj
aspire start --non-interactive --isolated --apphost "$app"
dotnet msbuild "$app" -getProperty:HexalithSourceMappingMode,Hexalith1BuildPackageProps,_PartiesUsesDefaultCatalog,HexalithCommonsVersion,HexalithEventStoreVersion,NuGetAudit,NuGetAuditMode,CentralPackageTransitivePinningEnabled -getItem:PackageVersion
```

The baseline start exited 2 with four NU1109 errors, one per package in each of Parties.UI and Parties.Mcp. The MSBuild query exited 0 for each selection. Package mode used `-p:HexalithSourceMappingMode=package`. The alternate-catalog control used `-p:Hexalith1BuildPackageProps="$TMPDIR/caller.props"` with a temporary copy of the Builds catalog containing the two sentinel versions. The root-selection control placed that file at `"$TMPDIR/alternate-builds/Props/Directory.Packages.props"` and used `-p:HexalithBuildsRoot="$TMPDIR/alternate-builds"`.

| Selection | Commons.ServiceDefaults pin | Microsoft.Extensions.Http.Resilience pin | Microsoft.Extensions.ServiceDiscovery pin |
| --- | --- | --- | --- |
| Baseline default | 3.0.4 | 11.0.0 | 11.0.0 |
| Corrected default | 3.0.4 | 11.0.1 | 11.0.1 |
| Package mode | 3.0.1 | 11.0.0 | 11.0.0 |
| Alternate catalog via `Hexalith1BuildPackageProps` | 3.0.1 | 91.0.1 | 91.0.2 |
| Alternate Builds root | 3.0.4 | 91.0.1 | 91.0.2 |

All five evaluations used EventStore 5.4.0, enabled central transitive pinning and evaluated `NuGetAudit=true` and `NuGetAuditMode=all`.

### Isolated startup and shutdown

```bash
aspire ps --non-interactive
aspire start --non-interactive --isolated --apphost "$app"
bash scripts/check-legacy-apphost-startup.sh
for resource in parties parties-dapr-cli; do
    aspire wait "$resource" --apphost "$app" --status healthy --timeout 60 --non-interactive
done
aspire describe --include-hidden --format Json --non-interactive --apphost "$app"
aspire stop --non-interactive --apphost "$app"
aspire ps --non-interactive
```

The final default launch exited 0 with Aspire CLI `17.0.0+2d325b13709752ba6d9a943635c5e2940a2530c0`, AppHost SDK and Aspire.Hosting 17.0.0, EventStore.Aspire 5.4.0, CommunityToolkit.Aspire.Hosting.Dapr `17.0.0-preview.1.261120-0454`, Dapr 1.19.1 and Docker 29.8.2. It reused the development Dapr infrastructure, including Redis 6.2.21.

- Both resource waits exited 0. Aspire reported Parties and `parties-dapr-cli` as `Running` and `Healthy`. The repository probe exited 0 with HTTP 200 from Parties `/alive` and HTTP 204 from Dapr `/v1.0/healthz`.
- AppHost, UI and MCP assets resolved both Microsoft.Extensions packages at 11.0.1. UI and MCP resolved Commons.ServiceDefaults 3.0.4. All three `obj/project.assets.json` files recorded `restoreAuditProperties` with audit enabled, mode `all`, level `low`.
- Scoped `aspire stop` exited 0. `aspire ps` found no AppHost. All 11 executable PIDs recorded by Aspire were absent, and the Parties and Dapr HTTP ports 33599 and 45133 had closed. Comparing Docker listings before, during and after the test identified the owned `security-whumfefs` container. The exact-ID check `docker container ls --all --quiet --no-trunc --filter "id=$owned_container"` returned no rows. The pre-existing Dapr containers retained their IDs.

### Focused package checks

Each build used .NET SDK 11.0.100 and the following flags. `project` is one of `Hexalith.Parties.Tests`, `Hexalith.Parties.ConsumerPortal.Tests`, `Hexalith.Parties.Runtime.IntegrationTests` and `Hexalith.Parties.Ci.Tests`.

```bash
flags=(-c Release -m:1 --disable-build-servers -p:UseHexalithProjectReferences=false
       -p:HexalithSourceMappingMode=package -p:MinVerVersionOverride=1.0.0)
dotnet build "tests/$project/$project.csproj" "${flags[@]}"
dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj "${flags[@]}"
```

The focused xUnit commands invoked the `bin/Release/net11.0` DLLs directly with the filters below, plus `-failSkips -result-xml "$results/$lane.xml"`. The Consumer Portal command ran the entire DLL. The root catalog and architecture tests ran as separate invocations since xUnit intersects class filters with method filters.

```bash
dotnet "$core" -class '*PartySdkQueryHandlerTests' -class '*PartyLegacyReadModelBackfillTests' \
    -class '*PartyProcessingGenerationRecoveryTests' -class '*PartyMemoryRecoveryRegressionTests' \
    -class '*PartyMemoryRecoveryTests' -class '*PartyMemoryRestClientRecoveryTests' \
    -class '*PartyMemorySourceInventoryTests'
dotnet "$core" -class '*PartyMemoryCleanupServiceTests'
dotnet "$consumer"
dotnet "$core" -class '*PackageCatalogFallbackTests'
dotnet "$core" -method '*DocumentationFitnessTests.MaintainedTechnologyTablesMatchCentralPackageCatalog'
dotnet "$ci" -method '*CommonsHttpRestoreRoutingTests.DirectoryBuildPropsLeavesSourceAndPackageSelectionToThePinnedTool' \
    -method '*CommonsHttpRestoreRoutingTests.MsbuildPropertyResolverReturnsCurrentCentralVersion'
python3 -m unittest discover -s scripts -p test_backfill_and_memories_integration.py
PARTIES_BACKFILL_WORKLOAD_EVIDENCE="$results/provider/workloads" \
    python3 _bmad-output/implementation-artifacts/tests/five-snapshot-history-and-publish-2026-11-11/run-owned-backfill.py \
    --assembly "$runtime" --output "$results/provider"
python3 -c 'import json, pathlib, runpy, sys; verify=runpy.run_path("scripts/verify-backfill-and-memories-deferrals.py")["verify_workloads"]; print(json.dumps(verify(pathlib.Path(sys.argv[1])), indent=2))' \
    "$results/provider/workloads"
(cd tests/e2e && node "$npm_cli" ci && \
    CI=true node node_modules/@playwright/test/cli.js test specs/consumer-portal-routes.spec.ts \
    --project=chromium --grep 'bound Consumer saves editable profile details' --retries=0)
```

The Release builds exited 0 with zero warnings and errors. The restored package graph used EventStore.DomainService 5.4.0, Memories.Client.Rest 4.1.0, FrontComposer.Shell 6.0.1 and Commons/Commons.ServiceDefaults 3.0.1 where referenced. Both Microsoft.Extensions packages resolved at 11.0.0 in the package-mode host graph. The captured restore graphs recorded audit enabled, mode `all`, level `low`.

| Check | Result |
| --- | --- |
| Gateway, backfill, processing and Memories regressions | 1,211 passed |
| Memories cleanup service | 34 passed |
| Consumer Portal | 130 passed |
| Catalog fallback, architecture and routing | 3, 1 and 3 passed |
| Backfill and Memories verifier, Python 3.14.4 | 15 passed |
| Owned provider | 18 passed |
| Chromium profile save, Playwright 1.65.1, Node 26.13.1, npm 11.20.0 | 1 passed |

Each check exited 0 and reported zero skips where applicable. The owned provider used Dapr 1.19.1 and Redis 7.4.7 from `redis/redis-stack:latest@sha256:880df9c228597cb0d15b585f39a4327d6ee2d8b0d0f155e3f75dba9a761d4ec3`. The independent snapshot reader verified six workload receipts for each of the empty and existing targets, including interruption. The provider receipt confirmed both owned processes exited and the owned Redis container was removed. Playwright closed its UI server on port 5072.

`bash scripts/check-no-warning-override.sh`, `git diff --check` and the DW-97 checksum comparison passed.

## Review Triage Log

- **Medium, patched.** `HexalithBuildsRoot` can select another Builds catalog while keeping the default Commons selection. The metadata guard preserves both sentinel versions under this selector and under `Hexalith1BuildPackageProps`.
- **Medium, patched.** An unconditional version update could downgrade a newer pin at the normal catalog path. The metadata guard applies only to the observed 11.0.0 pins.
- **Medium, verified.** An evaluated client catalog alone leaves the host restore graph unverified. The default AppHost build restored both UI and MCP, with resolved versions and audit settings captured from their assets files.
- **Low, patched.** The architecture table now identifies the catalog scope for the HTTP resilience and service discovery versions.
