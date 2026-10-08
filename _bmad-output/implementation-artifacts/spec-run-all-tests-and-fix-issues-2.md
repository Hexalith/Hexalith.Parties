---
title: 'Revalidate all tests and fix current failures'
type: 'bugfix'
created: '2026-07-12'
status: 'done'
review_loop_iteration: 0
baseline_revision: '8d28a1bc7fe5faebb09bf9cc495fa671346140f5'
baseline_commit: '8d28a1bc7fe5faebb09bf9cc495fa671346140f5'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The last complete test evidence predates dependency, build-workflow, and package-routing changes. All current .NET and browser tests must be rerun, and root-owned regressions must be repaired so failures are not hidden by stale results, zero-test runs, or environment skips.

**Approach:** Establish fresh package-mode and source-reference baselines, run every configured .NET test project plus the full Playwright workspace, fix failures with focused reruns, and finish with broad validation and explicit evidence for any genuine environment blocker.

## Boundaries & Constraints

**Always:** Preserve warnings-as-errors and Microsoft.Testing.Platform semantics; run `.slnx` only for restore/build and test projects individually; inspect TRX and Playwright results for executed, failed, and skipped counts; keep package and source-reference evidence separate; treat the five advanced submodule checkouts as user-owned, read-only baseline state.

**Ask First:** Editing any `references/Hexalith.*` content or gitlink, changing a public package contract, changing dependency versions or source/package strategy, weakening a build/test/coverage gate, or accepting a previously unexpected skip as intentional.

**Never:** Do not initialize nested submodules, use legacy `.sln`, use project-level `--filter` under MTP, exclude failing tests, count a zero-test run as passing, suppress warnings globally, or modify tests solely to bypass Docker, DAPR, browser, network, or localhost limitations.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Green repository | All 15 .NET projects and browser prerequisites are available | Every configured test executes and passes; expected skips are enumerated | Record fresh per-project and browser evidence |
| Root-owned regression | A root source, test, script, or configuration defect fails a lane | Apply the smallest architecture-conformant fix and rerun focused then broad tests | Stop if the fix crosses an Ask First boundary |
| Dependency-mode drift | Package and source-reference modes differ | Diagnose the exact API/version/routing difference without conflating modes | Fix root compatibility only; report dependency-owned failures |
| Environment blocker | Docker, DAPR, Chromium, network, or port binding is unavailable | Continue all independent checks and preserve the exact blocked command | Do not mark the blocked lane green or weaken it |

</frozen-after-approval>

## Code Map

- `scripts/aspire-start-mtls.sh` -- persistent Docker port binding checks before restarting scoped mTLS prerequisites.
- `src/Hexalith.Parties.AppHost/Program.cs` and `tests/Hexalith.Parties.IntegrationTests/Topology/PartiesUiTopologyTests.cs` -- explicit SDK receiver identities matching DAPR app IDs and the existing model assertion.
- `tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs` -- strict current committed graph receipts, separate from historical identities.
- `tests/Hexalith.Parties.Ci.Tests/AspireMtlsBootstrapTests.cs` -- executable stopped-container bootstrap regression coverage with isolated command doubles.
- `scripts/test.ps1` -- authoritative local inventory and lane runner for all 15 .NET test projects.
- `.github/workflows/ci.yml` and `references/Hexalith.Builds/.github/workflows/domain-ci.yml` -- active CI caller and read-only shared-workflow baseline; the retired `.github/workflows/test.yml` must not be referenced.
- `tests/Hexalith.Parties.*.Tests/**` and `tests/Hexalith.Parties.Tests/**` -- focused unit, package, component, integration, topology, and CI regression surfaces.
- `tests/e2e/package.json`, `tests/e2e/playwright.config.ts`, and `tests/e2e/specs/**` -- separate full browser test workspace excluded from `scripts/test.ps1 -Lane all`.
- `_bmad-output/implementation-artifacts/tests/test-summary.md` -- append-only consolidated verification evidence.

## Tasks & Acceptance

**Execution:**
- [x] `Hexalith.Parties.slnx`, `scripts/test.ps1`, and all 15 test projects -- fresh Release package-mode and current-catalog Debug source-reference restore/build pass with zero warnings/errors. Every project executes; fresh post-review complete sweeps in each mode are **3,038 total, 3,032 executed, 3,032 passed, zero failed/errors, six predeclared skips**. Seven new bootstrap regression cases raise the CI project to 108 tests. Complete repaired service and topology projects pass in each mode; the permitted direct xUnit v3 fallback executes every project after the standard CLI stalls. No unexpected skip is accepted.
- [x] Root-owned source/test/documentation defects -- repaired stopped-container mTLS port inspection with seven executable regression cases and a mutation check, explicit SDK workload receiver identities matching DAPR app IDs, strict current owner-graph receipts, and centralized claim/encryption assertions, Test-only UI context and picker composition, Fluent control semantics, search/empty recovery, mobile/focus behavior, accepted-command state, export/download cleanup, erasure modal/warning and scoped interop lifecycle. Current catalog/documentation receipts align with the existing graph; historical receipts remain dated. Focused checks and corresponding full projects pass in both modes.
- [x] `tests/e2e/**` -- locked dependencies and typecheck pass; the automatic source-mode Release host builds with `BuildInParallel=false`; **90/90 Playwright tests execute/pass, zero failed/skipped**, including configured specimen axe and authorization/privacy gates. Awaited refreshed-row focus, export cleanup, picker disposal and Escape diagnostics pass.
- [x] `_bmad-output/implementation-artifacts/tests/test-summary.md` -- October commands, per-project counts, source identities, working-tree observations, fixes, declared skips, failed baseline/recovery attempts and remaining execution limitations appended without changing earlier evidence.

**Acceptance Criteria:**
- Given the current workspace and all available prerequisites, when both 15-project .NET baselines and the full Playwright workspace run, then every executed test passes and every non-executed test has an exact, verified reason.
- Given a root-owned failure, when its focused test passes after repair, then the corresponding broad lane also passes without warning, inventory, runner, or architecture-gate suppression.
- Given package/source or environment divergence, when completion is reported, then runnable checks remain green and each unresolved external blocker identifies the exact command, dependency or prerequisite, and observed failure.

**Current acceptance — 2026-10-08 post-review verification:** Both complete 15-project sweeps pass after the review patch: **3,038 total, 3,032 executed/passed, zero failed/errors, six predeclared skips** per mode. All service tests pass **907/907**, live topology passes **36/36 executed**, and CI passes **108/108**, including seven new bootstrap regression cases. Browser passes **90/90**, with no skipped cases. The parent independently verifies all 40 final commands exit zero at Parties `ccaf77399d92be696e44ecb8ab17acf77d97646a`; build warnings/errors are zero and tracked source hashes remain unchanged during execution. The fixed shell behavior is mutation-verified. Eleven pre-existing review root causes are logged in deferred work; no current patch remains open. The standard CLI delay is documented and the permitted direct xUnit assembly fallback provides actual per-project evidence. Implementation, verification and independent review are complete; status is `done`.

## Spec Change Log

- 2026-07-12 — Full rerun executed (baseline `8d28a1b`). **Package-mode Release
  (CI parity): all 15 projects green — 2321 tests, 0 failed, 6 expected integration
  skips.** Root-owned environment fix: installed `ripgrep` (cleared the `rg`-dependent
  `Matrix_ValidationEvidenceCommandsAreReproducible` fitness test). **Source-mode Debug
  baseline is BLOCKED** by a governed Commons dependency-mode drift (`CS1704`
  double-import of `Hexalith.Commons.UniqueIds` on clean source rebuild;
  `FileNotFoundException Version=3.58.0.0` version skew with prebuilt submodules) —
  pinned by Story 7.1's `ProjectReference Include="$(HexalithCommonsRoot)…"` strategy,
  an **Ask First** boundary; not resolved without owner authorization. e2e: typecheck +
  16 artifact/SSR specs pass; interactive specs deferred to CI `ui-a11y`
  (`blazor.web.js` 500 under `dotnet run --no-build`). Full evidence in
  `tests/test-summary.md`. No product/test source edited in this pass.
- 2026-07-12 (owner-authorized strategy fix) — Source-mode Commons drift **resolved** by
  consuming Commons as a package in source mode (global `HexalithCommons*FromSource=false`
  overrides the submodule auto-enable → no `CS1704`, no version skew), keeping
  EventStore/Tenants/FrontComposer/Memories from source. Source-mode Debug now **14/15 green**
  (was 6 projects failing); only the 2 Release-oriented `ClientPackageTests` remain, and they
  pass in package mode. No product/test source edited; the fix is the documented build
  properties (see updated Verification command). Optional flag-free durability:
  `git submodule deinit references/Hexalith.Commons` (left as owner choice).

- 2026-10-08 (earlier attempt) — All configured current package/source projects and all 90 browser tests rerun. Root repairs and affected full projects pass; .NET builds are zero-warning/error. Current broad source and reconciled package totals are 3,031 total / 3,025 executed / 3,024 passed / one gateway failure / six existing skips. Existing mTLS recovery preserves ACLs and still fails live gateway readiness because the independently built Tenants child consumes DomainService 3.115.0 and cannot compile the required administrator contracts. Preserve failed attempts, original baseline fields and frozen intent; see the appended October evidence. Concurrent authors supplied the current dependency selections/commits; no reference, dependency strategy or Git mutation was performed by this repair pass.

- 2026-10-08 (follow-up at `ccaf77399d92be696e44ecb8ab17acf77d97646a`) — All 15 projects execute per mode via the permitted direct-runner fallback after standard CLI startup stalls. Current root receipts match the external author's committed Builds `f717a87c26a8266bdde95d18f998ef2ab366d43a` and Tenants `fcdcb4205a3f6e46f736cdd3e6f2b20ca2f241df` selections; earlier `998c0b649d7bbf01e0fed4cc79924662dd16cc91` receipts remain dated. Persistent stopped-container port inspection repairs bootstrap. Standalone comparisons isolate inherited `BuildInParallel=false` as the effective local Tenants startup pin; audit alone does not resolve it. Explicit `EventStore:DomainService:AppId` values matching `parties`/`tenants` repair the newly reachable workload audience failure. Complete repaired service and topology projects pass in both modes; latest reconciled totals are **3,025 passed, zero failed, six declared skips** per mode, and final browser evidence is **90/90** at the final graph. Failed attempts and receipt limitations remain recorded; parent review is pending.

- 2026-10-08 (independent review and final verification) — All three required review layers completed. Thirteen findings were triaged individually: one current missing-regression-coverage patch is fixed, and eleven distinct pre-existing causes are appended to the deferred-work ledger (one cause was reported twice). Seven isolated script-execution cases pass, and restoring the old runtime-only Docker lookup in a temporary copy makes the restart case fail. Fresh parent restore/build and all 15 projects pass in both modes: **3,038 total / 3,032 executed and passed / zero failures/errors / six unchanged skips**; browser **90/90**. This final evidence replaces the earlier reconciled aggregate as the current acceptance receipt, while preserving all earlier attempts.

## Design Notes

Run broad lanes with continue-on-failure first to capture the complete failure set. Triage CI/package routing before UI and application changes, then validate FrontComposer, EventStore, and Memories compatibility independently because the checked-out submodules are newer than the root gitlinks. Coverage remains explicitly unsupported by the local MTP runner and is reported as such rather than simulated.

## Verification

**Earlier October commands and results (retained as dated evidence; full argv/environment and identity receipts in `TestResults/bmad-revalidation-20261008/*-commands.json`):**
- `dotnet restore Hexalith.Parties.slnx -m:1` then `dotnet build Hexalith.Parties.slnx -c Release --no-restore -m:1` -- **0/0, zero warnings/errors**.
- `GITHUB_ACTIONS=true pwsh -NoProfile -File scripts/test.ps1 -Lane all -Configuration Release -ContinueOnFailure -ResultsDirectory TestResults/bmad-revalidation-20261008/package-latest -Properties UseHexalithProjectReferences=false,UseNuGetDeps=true,NuGetAudit=false,MinVerVersionOverride=1.0.0` -- all 15 projects execute; original exit 1 includes the repaired stale receipt case and gateway failure. Complete repaired Release service rerun **907/907**; latest reconciled lane **3,024 passed, one failed, six declared skips**.
- Current-catalog source solution restore/build with `UseHexalithProjectReferences=true,UseNuGetDeps=false,HexalithCommonsFromSource=false,HexalithCommonsHttpFromSource=false,HexalithCommonsServiceDefaultsFromSource=false,NuGetAudit=false,MinVerVersionOverride=1.0.0,GeneratePackageOnBuild=false,BuildInParallel=false` -- **0/0, zero warnings/errors**.
- `GITHUB_ACTIONS=true pwsh -NoProfile -File scripts/test.ps1 -Lane all -Configuration Debug -ContinueOnFailure -ResultsDirectory TestResults/bmad-revalidation-20261008/source-latest -Properties UseHexalithProjectReferences=true,UseNuGetDeps=false,HexalithCommonsFromSource=false,HexalithCommonsHttpFromSource=false,HexalithCommonsServiceDefaultsFromSource=false,NuGetAudit=false,MinVerVersionOverride=1.0.0,GeneratePackageOnBuild=false,BuildInParallel=false` -- exit 1; **3,031 total, 3,025 executed, 3,024 passed, one gateway failure, six declared skips**.
- Full topology reruns in Debug/source then Release/package with the same respective properties and `GITHUB_ACTIONS=true,Dapr__Mtls__Enabled=true,Dapr__Mtls__CertificateDirectory=/home/administrator/.local/state/hexalith-parties/dapr-certs` -- each exit 1, **35 passed, one gateway failed, six existing skips**. Exact child build/CS0246 and readiness traces are retained; no ACL or gate weakened.
- `cd tests/e2e` then `npm ci`, `npm run typecheck`, `DEBUG=pw:webserver npm test` -- **pass; 90/90 executed/passed**, automatic source host, configured axe gates, no exclusions.
- `bash scripts/check-no-warning-override.sh` and `git -c core.whitespace=cr-at-eol diff --check` -- **pass**. Plain `git diff --check` reports existing CRLF line terminators; no Git/build whitespace policy was changed.

**Pre-review follow-up verification (retained as dated evidence; full argv, environment, exits, source identities and TRX/JUnit verdicts in `TestResults/bmad-revalidation-20261008-resume/`):**

- Initial package/source restore/build and every configured project run are retained under `*-direct-*`; explicit property sets remain the current-catalog package/source sets above. Standard `scripts/test.ps1 -Lane all` and serialized individual CLI stalls are retained with their termination evidence; no zero-test command is credited.
- `source-identity-repaired-commands.json` and `package-identity-repaired-commands.json` record solution restore/build, strict focused receipt **1/1**, and complete service project **907/907** in each mode; all exit **0**, zero compiler warnings/errors.
- A subsequent owner tag marks the same Builds SHA as `v4.30.1`; current describe receipts are refreshed. `source-tag-repaired-commands.json` and `package-tag-repaired-commands.json` repeat strict focus **1/1** and whole service **907/907**, with restore/build **exit 0** and zero warnings/errors. Final reconciled service rows use these latest TRXs.
- `GITHUB_ACTIONS=true Dapr__Mtls__Enabled=true Dapr__Mtls__CertificateDirectory=/home/administrator/.local/state/hexalith-parties/dapr-certs DOTNET_CLI_USE_MSBUILD_SERVER=0 BuildInParallel=false dotnet tests/Hexalith.Parties.IntegrationTests/bin/<Debug|Release>/net10.0/Hexalith.Parties.IntegrationTests.dll -noColor -result-trx <mode-specific-path>` -- both complete topology commands exit **0**, **42 total, 36 executed/passed, zero failed/errors, six existing skips**. The existing model-construction test verifies the explicit receiver identity; inventory remains unchanged.
- `package-final-reconciled-verdict.json` and `source-final-reconciled-verdict.json` replace only complete repaired service/topology rows: **15 projects, 3,031 total, 3,025 executed/passed, zero failures/errors, six skips**, no zero-execution project. This is reconciled evidence across dated runs, not a single atomic run at one revision.
- `cd tests/e2e && DEBUG=pw:webserver npm test` -- **exit 0, 90/90 passed, zero failed/errors/skipped** at Parties `ccaf77399d92be696e44ecb8ab17acf77d97646a`; exact before/after identities and JUnit retained under `browser-current-final-*`.
- `bash -n scripts/aspire-start-mtls.sh`, `bash scripts/check-no-warning-override.sh`, and `git -c core.whitespace=cr-at-eol diff --check` -- **pass**. Scoped control-plane containers return to their original stopped state; global DAPR resources and all tracked reference content remain preserved.

| Frozen scenario / acceptance requirement | Final verification mapping |
| --- | --- |
| All runnable .NET and browser checks execute | 15 per-project TRXs per mode, nonzero executions, current full service/topology reruns, final 90-test JUnit; six exact tracked skips enumerated in the summary. |
| Root-owned regression repaired with focused and broad checks | Strict current-identity focus plus full 907-test service projects; stopped-container bootstrap plus full topology; focused live gateway plus full topology and extended model assertion. |
| Dependency-mode drift diagnosed without conflating identities | Separate Debug/source and Release/package properties, reference/hash snapshots, initial failures and complete repaired projects; committed owner selection refreshed without reference/dependency changes. |
| Environment blockers retain exact failed commands | CLI restore/discovery stalls, mTLS failures, prebuilt/normal Tenants comparisons, serialized startup and removed public-audience attempt retained; no hidden skip or simulated coverage. |

**Historical July command retained separately:** `pwsh -NoProfile -File scripts/test.ps1 -Lane all -Configuration Debug -ContinueOnFailure -ResultsDirectory TestResults/bmad-source-20260712 -Properties UseHexalithProjectReferences=true,UseNuGetDeps=false,HexalithCommonsFromSource=false,HexalithCommonsHttpFromSource=false,HexalithCommonsServiceDefaultsFromSource=false,HexalithCommonsVersion=2.28.0,HexalithTenantsVersion=2.4.2,NuGetAudit=false,MinVerVersionOverride=1.0.0,GeneratePackageOnBuild=false,BuildInParallel=false` -- the exact obsolete version overrides now produce **NU1109** against required Commons.UniqueIds >=2.30.1. This diagnostic does not replace the current-catalog source verdict above. Original July evidence and decisions remain dated in the change log and append-only summary.


## Post-review Final Verification

All exact argv, explicit environments, exits, timing, before/after HEAD/gitlink/working-tree snapshots, logs, per-project TRXs and browser JUnit are retained in `TestResults/bmad-revalidation-20261008-review-final-61ed9809/`; the orchestration source is retained there too. This is a fresh complete sweep of every project in each mode after P1, rather than replacement rows from older baselines.

- The inventory matches `scripts/test.ps1` and every test project under `tests/`; the one support host remains explicitly classified as a non-test project. All 15 test projects execute in each mode.
- Source Debug and package Release solution restore/build use the respective unchanged property sets above and `-m:1`: all four commands exit **0**, each build reports **0 warnings / 0 errors**. The `.slnx` is used only for restore/build.
- Every project runs individually as `GITHUB_ACTIONS=true dotnet <project>/bin/<configuration>/net10.0/<project>.dll -noColor -result-trx <unique-project-path>`. The topology project additionally inherits the existing mTLS certificate directory, `DOTNET_CLI_USE_MSBUILD_SERVER=0` and `BuildInParallel=false`; no filter, exclusion, timeout or skip policy changes.
- `source-verdict.json` and `package-verdict.json`: each **15 projects, 3,038 total, 3,032 executed/passed, zero failures/errors, six exact existing skips**. Service is **907/907**, CI **108/108**, topology **36/36 executed** plus those six skips. Focused and mutation checks are not counted twice.
- `cd tests/e2e` then `DEBUG=pw:webserver npm test`: exit **0**, **90/90 passed**, zero failures/errors/skips; automatic source Release host, existing authorization/privacy and axe configuration.
- `bash -n scripts/aspire-start-mtls.sh`, `bash scripts/check-no-warning-override.sh`, and `git -c core.whitespace=cr-at-eol diff --check`: exit **0**. Scoped mTLS containers return from running to their original stopped state; no real resources are changed by the new regression tests.
- `review-mtls-script-regression.trx` in the earlier resume evidence directory: **7/7** new cases pass. Its uniquely named temporary-copy mutation receipt intentionally exits **1** and fails the one restart case after replacing persistent inspection with `docker port`; the tracked bootstrap hash is preserved. This demonstrates detection of the original defect.
- All 40 parent commands exit **0** and before/after snapshots record the same Parties HEAD `ccaf77399d92be696e44ecb8ab17acf77d97646a`, committed gitlinks and tracked source hashes. Frozen intent and original baseline fields remain preserved. Earlier CLI stalls and intermediate receipt limitations stay recorded; coverage remains unsupported and is not simulated.

## Review Triage Log

Review round 1 — all three layers completed before triage. The mandatory archive is the complete diff from the preserved historical baseline; task-entry `890119f1b4ced37ccec34787c812f064199fb3ef` distinguishes existing committed defects from this follow-up repair. Each finding is recorded before grouping. No intent-gap or bad-spec route was found.

| Finding | Layer | Verdict | Route | Verified evidence |
| --- | --- | --- | --- | --- |
| B1 | blind-hunter | medium | defer — D1 | PartyDetailSdkProjectionHandler.cs:69–87 reads both slots before the mismatch guard; RecordUnresolvedProcessingAsync writes only Processing at 245–265. An unresolved first delivery therefore makes a later retry request rebuild. The exact code already existed at the task-entry 890119f1 commit. |
| B2 | blind-hunter | medium | defer — D2 | AppHost Program.cs:237 composes sample without an explicit receiver identity; its SDK host calls AddEventStoreDomainService and falls back to DAPR_APP_ID or ApplicationName. The observed AppHost child path lacks that variable, so sample has the same audience mismatch when enabled. The optional sample branch is unchanged from task-entry 890119f1. |
| B3 | blind-hunter | medium | defer — D3 | AppHost Program.cs:217 supplies http://memories:8080/ for both publish and EnableMemoriesSearch local-run composition; the local project has an Aspire endpoint rather than that container DNS address. This optional local-search defect is unchanged from task-entry 890119f1. |
| B4 | blind-hunter | high | defer — D4 | PartyMemoryCleanupService.cs:52 and its probe, plus compensation in PartyMemoryIndexingService, use api/tenants while the selected Memories server maps MemoriesRoutes.CaseMemoryUnit under /api/v1. A routing 404 is accepted as cleanup success. These root paths are unchanged from task-entry 890119f1; no reference/API change is made by this pass. |
| B5 | blind-hunter | high | defer — D5 | PartyMemoryIndexingService.cs:117 stores the accepted workflow ID without its terminal result. The Memories duplicate branch returns ExistingMemoryUnitId; preflight reservation only covers the TTL/in-flight window and fails open, so a later duplicate can replace the source mapping with a nonexistent unit ID. The root code predates this pass. |
| B6 | blind-hunter | high | defer — D6 | PartyMemoryCleanupService.cs:57 treats HTTP success/404 as durable cleanup and removes mappings. Memories CaseService.DeleteMemoryUnitAsync schedules MemoryUnitDeletionProjectionWorkflow before returning, and a not-yet-materialized ingest returns 404. This can certify cleanup before convergence; root code predates this pass. |
| B7 | blind-hunter | high | defer — D7 | PartyMemoryUnitMappingStore.cs:234 replaces the inventory with unconditioned SaveStateAsync, or unconditioned DeleteStateAsync for empty lists. A RecordMappingAsync ETag update after the cleanup snapshot can be lost by this later overwrite. These implementations are unchanged from task-entry 890119f1. |
| B8 | blind-hunter | high | defer — D8 | PartyMemoryIndexingService.cs:127 rethrows caller cancellation during mapping persistence without compensation; the accepted server workflow runs independently of that cancellation. The resulting unit can lack a cleanup mapping. This cancellation branch predates this pass. |
| B9 | blind-hunter | medium | defer — D9 | PartySdkLastKnownReadModelCache.cs:138 adds a generation on every eviction; retention/capacity cleanup removes only _entries. PartySdkQueryService.cs:430 evicts missing IDs, so distinct valid nonexistent IDs grow the singleton bookkeeping without limit. The code is unchanged from task-entry 890119f1. |
| B10 | blind-hunter | high | defer — D10 | PartiesServiceCollectionExtensions.cs:242 evicts only its singleton cache. Another host retains its own pre-erasure cache, and PartySdkQueryService.cs:443 serves it on a canonical-store exception without reading the erasure record. This multi-instance personal-data risk predates this pass. |
| E1 | edge-case-hunter | medium | defer — D1 | Independently confirms B1: the actual current helper is RecordUnresolvedProcessingAsync, not the suggested coordinated-write guard snippet. It creates only Processing before the next delivery checks for missing Detail; the condition and harm are real and pre-existing. |
| E2 | edge-case-hunter | high | defer — D11 | PartyIndexSdkProjectionHandler.cs:282–321 takes one canonical snapshot before awaited removal notifications and reuses it for later ingestion. The current code has no proposed canonical-version/erasure fence, and PartyMemoryIndexEntrySearchIndexer forwards the supplied entry without rechecking canonical state; an intervening erasure can therefore be republished. The code predates this pass. |
| V1 | verification-gap | medium | patch — P1 | Pre-verified regression gap: no tracked automated test executes the changed persistent Docker binding validation. Manual stopped-container and live topology evidence proves the fix works now but would not detect reintroducing docker port. Add deterministic script-execution coverage for valid stopped and incompatible bindings, retaining fail-closed behavior without real-container mutation. |

B1 and E1 share the same root cause and form D1. All other findings have distinct causes. D1–D11 are pre-existing issues recorded separately in the deferred-work ledger; P1 is the current repair's missing automated regression coverage.

P1 closure — `AspireMtlsBootstrapTests.cs` executes the tracked script through private command doubles. The compatible stopped case and all six incompatible binding cases pass; a temporary regression mutation fails as expected. Both final complete CI projects pass 108/108, and all parent verification above passes.
