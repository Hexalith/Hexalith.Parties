# Test Automation Summary

## Generated Tests

### API Tests
- [x] Not applicable for Story 7.8; this story produced release/readiness evidence and did not add API endpoints.

### E2E Tests
- [x] `tests/e2e/specs/story-7-8-release-readiness.spec.ts` - Story 7.8 release readiness artifact validation.
- [x] `tests/e2e/specs/story-7-4-projection-platform-compatibility.spec.ts` - Updated stale Story 7.4 method-name assertions discovered by the artifact suite.

## Coverage

- Story 7.8 final readiness sections: 10/10 covered.
- Root repository/package state rows: 8/8 covered.
- Validation matrix commands: 11/11 covered.
- Cleanup and rollback decisions: projection, crypto, UI fixture, gitlink drift, and KMS guardrails covered.
- Existing Epic 7 artifact assertions: Story 7.4 projection compatibility spec updated to current method names.

## Validation

- [x] `npm run typecheck`
- [x] `PLAYWRIGHT_SKIP_WEBSERVER=1 npm run test -- specs/story-7-8-release-readiness.spec.ts --project=chromium` - 6 passed, 0 failed.
- [x] `PLAYWRIGHT_SKIP_WEBSERVER=1 npm run test -- specs/story-7-1-platform-planning-artifacts.spec.ts specs/story-7-4-projection-platform-compatibility.spec.ts specs/story-7-8-release-readiness.spec.ts --project=chromium` - 16 passed, 0 failed.
- [x] `git diff --check`

## Next Steps

- Run the new spec in CI with the existing Playwright lane.
- Release remains blocked by documented implementation blockers until full solution build, package compatibility, UI accessibility, deploy validation assembly completion, and drifted gitlinks are resolved.

## Story 8.1 Baseline Stabilization - 2026-07-07

### Baseline Changes

- `scripts/test.ps1` now runs every lane through the same per-project helper using `dotnet test <projectPath>`, not `--project` or solution-level test execution.
- The unit lane includes `Hexalith.Parties.Authentication.Tests` and `Hexalith.Parties.ConsumerPortal.Tests`.
- The `all` and `coverage` lanes iterate the explicit 15-project test inventory; coverage passes `--collect "XPlat Code Coverage"` through the shared helper.
- `scripts/test.ps1` now fails fast before running tests if its explicit inventory drifts from `tests/**/*.csproj` or contains duplicate project entries.
- The CI lint job now verifies both `scripts/test.ps1` and `.github/workflows/test.yml` against the real `tests/**/*.csproj` inventory.
- The CI lint guard reads `scripts/test.ps1` inventory from the four executable lane arrays only, so unrelated project-path references cannot mask skipped local lane projects.
- `.github/workflows/test.yml` now installs .NET SDK `10.0.301` in every setup-dotnet step and assigns Authentication and ConsumerPortal tests to CI shards while preserving per-project execution.
- `README.md`, `docs/development-guide.md`, `docs/ci.md`, `docs/index.md`, `docs/getting-started.md`, and generated inventory docs now document lane/per-project tests, direct xUnit v3 executable filtering, sequential `-m:1` build triage, `MinVerVersionOverride=1.0.0`, baseline root submodules, and network-enabled package-test requirements.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` marks `epic-8` as `in-progress`, marks `8-1-baseline-and-release-blocker-stabilization` as `done`, and preserves the Epic 8 architecture-spine blocker for deletion-heavy migrations.

### Commands Attempted

| Command | Result | Notes |
| --- | --- | --- |
| `pwsh -NoProfile -Command "$tokens = $errors = $null; [System.Management.Automation.Language.Parser]::ParseFile('scripts/test.ps1', [ref] $tokens, [ref] $errors) > $null; if ($errors.Count) { $errors \| ForEach-Object { $_.Message }; exit 1 }"` | Failed invocation | Bash expanded the PowerShell variables before `pwsh` ran, producing a parser error in the command string rather than evidence about `scripts/test.ps1`. |
| `pwsh -NoProfile -Command '$tokens = $errors = $null; [System.Management.Automation.Language.Parser]::ParseFile("scripts/test.ps1", [ref] $tokens, [ref] $errors) > $null; if ($errors.Count) { $errors \| ForEach-Object { $_.Message }; exit 1 }'` | Pass | `scripts/test.ps1` parses cleanly. |
| `rg -n "dotnet test --solution\|dotnet test --project\|Hexalith\.Parties\.slnx.*dotnet test\|dotnet test .*Hexalith\.Parties\.slnx" scripts/test.ps1 docs/development-guide.md docs/ci.md docs/index.md` | Pass after wording cleanup | No stale solution-level/project-option test guidance remains in the corrected surfaces. The first run matched a negative warning line in `docs/index.md`; that wording was split so the check is clean. |
| `rg -n "10\.0\.300\|dotnet-version:\|Hexalith.Parties.Authentication.Tests\|Hexalith.Parties.ConsumerPortal.Tests\|dotnet test --solution\|dotnet test --project\|--project \$fullPath\|--solution" scripts/test.ps1 .github/workflows/test.yml docs/development-guide.md docs/ci.md docs/index.md` | Pass | Shows the three `10.0.301` setup-dotnet steps and Authentication/ConsumerPortal inventory; no `10.0.300`, `--project`, or `--solution` test execution remains in these files. |
| `pwsh -NoProfile -File scripts/test.ps1 -Lane unit -Configuration Release` | Fail | The corrected lane fails visibly on the first project, `tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj`, during restore because `Hexalith.Tenants.Client` is not available from `nuget.org`. This confirms the package-mode/default build blocker instead of silently skipping ConsumerPortal or using a solution-level false green. |
| `dotnet build Hexalith.Parties.slnx -c Release --no-restore -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` | Fail | Sequential build reaches several projects but fails with 18 `NU1101` errors for unpublished or unavailable `Hexalith.Tenants.Client`, `Hexalith.Tenants.Testing`, and `Hexalith.Commons.ServiceDefaults` packages. Source/package-mode ownership remains a release blocker. |
| `python3` inventory check comparing the `scripts/test.ps1` lane arrays and `.github/workflows/test.yml` test matrix to `tests/**/*.csproj` | Pass | Both explicit inventories match all 15 .NET test projects with no duplicates. |
| `python3` YAML parse of `.github/workflows/test.yml` | Pass | Workflow parsed successfully and contains `contract-test`, `lint`, `report`, `test`, and `ui-a11y` jobs. |
| `rg -n "14 source projects\|14 src projects\|15 test/e2e\|Quality Gate.*lint/build and test shards\|CI: lint → test \\(4 shards\\) → contract-test\|EventStore/Tenants submodule refs\|351 source C# files\|201 test C# files" docs README.md tests/README.md` | Pass with historical exception | No active docs matched; only `docs/project-scan-report.json` retains the old generated scan summary. |
| `bash scripts/check-no-warning-override.sh` | Pass | `OK: no warning-override or nested-submodule regressions detected in active CI/build scripts.` |
| `rg -n "git submodule update --init references/Hexalith.EventStore references/Hexalith.Tenants\|10\.0\.300\|dotnet test --solution\|dotnet test --project\|Hexalith.Parties.slnx.*dotnet test\|dotnet test .*Hexalith.Parties.slnx" README.md docs src tests scripts .github/workflows/test.yml -g '!docs/project-scan-report.json'` | Pass | No stale two-submodule command, SDK pin, solution-level test execution, or `--project` guidance remains in active source/docs/test guidance. |
| `dotnet test tests/Hexalith.Parties.Sample.Tests/Hexalith.Parties.Sample.Tests.csproj --configuration Release -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` | Pass | 58 passed; verifies the updated getting-started guardrail assertions in source-mode diagnostic settings. |
| `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` | Fail | Source-mode Release build is blocked by the `Hexalith.Memories` submodule guard requiring NuGet package references for external Hexalith libraries in Release. |
| `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` | Fail | Debug source-mode build and execution succeeded far enough to run 537 tests; 532 passed and 5 pre-existing tenant-event tests failed. |
| `dotnet tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.AppHostTenantsTopologyTests` | Pass | 16 passed; verifies the updated AppHost/submodule topology fitness assertions directly with xUnit v3 filtering. |

### Unresolved Release Blockers And Owner Decisions

| Blocker | Current State | Owner Decision / Rerun Path |
| --- | --- | --- |
| Gitlink drift | Builds, EventStore, FrontComposer, Memories, PolymorphicSerializations, and Tenants pointers remain drifted from the recorded Epic 7 readiness baseline. | Release manager and submodule owners must validate each drifted pointer or reset it before release tagging. Do not edit submodule contents from this story. |
| Package validation | Client and Contracts package compatibility tests can fail when NuGet repository signature metadata at `api.nuget.org:443` is blocked. | Package/release owner must rerun package validation in a network-enabled environment and record pass/fail evidence. Sandbox network denial is a blocker, not a pass. |
| Deploy validation | Static deploy validation previously passed, but direct deploy validation test assembly execution did not produce a final summary before interruption. | Deploy/release owner must rerun deploy validation with the required environment, including cluster credentials such as `KUBECONFIG_TEST_PATH` when live checks are expected. |
| UI accessibility | Direct UI tests previously had a failing navigation/landmark assertion against the current UI/FrontComposer surface. | UI and FrontComposer owners must choose whether to fix the surface, update validated expectations, or reset/advance the FrontComposer pointer with evidence. |
| Production KMS | `LocalDevKeyStorageBackend` remains dev-only and is not acceptable for regulated production personal data. | Security/platform/deployment owners must provide a production KMS or secret-store-backed key provider and deployment controls before regulated EU personal data is allowed. |
| Epic 8 architecture spine | Sprint status still records that Epic 8 story files should be created only after the architecture spine is approved; no approved architecture spine was found in this implementation pass. | PM/architect owner must approve or publish the Epic 8 architecture spine before deletion-heavy Story 8 migrations proceed. |

## Story 8.2 Identifier Correctness And Zero-Risk Hygiene - 2026-07-07

### Focused Changes

- Semantic identifier validation now accepts existing GUID-shaped IDs, ULID-compatible IDs, and bounded readable IDs while rejecting blank, whitespace, path-like, colon-containing, and control-character IDs with support-safe messages.
- Generated command IDs, correlation IDs, admin/MCP semantic IDs, and security fallback correlation IDs now use `UniqueIdHelper.GenerateSortableUniqueStringId()` where caller-supplied IDs are not present.
- The semantic-ID helper lives on the existing `Hexalith.Parties.Contracts.ValueObjects.PartyIdentifier` type to avoid root contract namespace shadowing.
- Client/admin gateway paths now reject unsafe aggregate IDs before EventStore submission.
- Typed command-client paths now reject unsafe child contact-channel and identifier IDs before EventStore submission.
- MCP `update_party` now rejects unsafe update/removal child IDs before client access.
- Legacy .NET `X`-format GUID strings remain accepted without reintroducing `Guid.TryParse`.
- Composite aggregate validation now checks child party-ID equality and unsafe child IDs before conflict/not-found handling.
- Tracked `*.csproj.lscache` / `*.lscache` artifacts were removed from the index, and `.gitignore` now excludes them.

### Commands Attempted

| Command | Result | Notes |
| --- | --- | --- |
| `git ls-files '*.csproj.lscache' '*.lscache'` | Pass | No tracked cache artifacts remain. |
| `rg -n 'Guid\.TryParse\|Guid\.Parse\|new Guid\(' src/Hexalith.Parties/Validation src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs src/Hexalith.Parties.Contracts/ValueObjects/PartyIdentifier.cs` | Pass | No semantic validation, aggregate, or helper GUID parsing remains. |
| `rg -n 'Guid\.NewGuid' src/Hexalith.Parties.Client/HttpPartiesCommandClient.cs src/Hexalith.Parties.Client/AdminPortal/HttpAdminPortalGdprClient.cs src/Hexalith.Parties.Mcp/Tools/PartiesMcpTools.cs src/Hexalith.Parties.Security/PartyKeyManagementService.cs src/Hexalith.Parties.Security/TenantKeyRotationService.cs` | Pass | No GUID generation remains in targeted new-ID sources. |
| `git diff --check` | Pass | No whitespace/conflict-marker issues. |
| `git diff --cached --check` | Pass | No staged whitespace/conflict-marker issues. |
| `dotnet test tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj -c Release -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` | Pass | 135 passed. |
| `dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj -c Release -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` | Pass | 135 passed after follow-up child-ID guard tests. |
| `dotnet test tests/Hexalith.Parties.Mcp.Tests/Hexalith.Parties.Mcp.Tests.csproj -c Release -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` | Pass | 56 passed after follow-up MCP child-ID guard tests. |
| `dotnet test tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj -c Release -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` | Pass | 232 passed. |
| `dotnet test tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj -c Release -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` | Pass | 179 passed. |
| `dotnet test tests/Hexalith.Parties.Security.Tests/Hexalith.Parties.Security.Tests.csproj -c Release -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` | Pass | 169 passed. |
| `dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` | Pass | Debug source-mode root test assembly builds cleanly. |
| `tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests -class Hexalith.Parties.Tests.Validation.IdentifierValidatorTests -class Hexalith.Parties.Tests.FitnessTests.IdentifierHygieneFitnessTests` | Pass | 20 passed after the follow-up `X`-format GUID compatibility patch. |
| `dotnet tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.Validation.IdentifierValidatorTests -class Hexalith.Parties.Tests.Validation.ContactChannelValidatorTests -class Hexalith.Parties.Tests.FitnessTests.IdentifierHygieneFitnessTests -class Hexalith.Parties.Tests.Domain.PartyDomainServiceInvokerValidationTests` | Pass | 44 passed. |

### Remaining Blockers

- The full `Hexalith.Parties.Tests` Release source-mode run is still blocked by the Story 8.1 `Hexalith.Memories` Release guard.
- The full `Hexalith.Parties.Tests` Debug source-mode run still has the Story 8.1 tenant-event failures:
  - `Hexalith.Parties.Tests.Authorization.TenantAccessServiceTests.CheckAccessAsyncDeniesAfterTenantDisabledEventIsProcessed`
  - `Hexalith.Parties.Tests.Tenants.TenantEventInfrastructureTests.TenantEventProcessorAppliesSupportedEventsAndDeduplicatesByMessageId`
  - `Hexalith.Parties.Tests.Authorization.TenantAccessServiceTests.CheckAccessAsyncDeniesAfterUserRemovedFromTenantEventIsProcessed`
  - `Hexalith.Parties.Tests.Tenants.TenantEventInfrastructureTests.ProcessorRestartReprocessesSameMessageIdAgainstSharedStore`
  - `Hexalith.Parties.Tests.Tenants.TenantEventInfrastructureTests.TenantEventProcessorRemovesUsersAndFailsInvalidPayloadWithoutPoisoningMessageId`
