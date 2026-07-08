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

## Story 8.3 Platform API Prerequisites - 2026-07-07

### Focused Artifacts

- Created `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md` as a no-production-migration prerequisite matrix for Stories 8.4-8.10.
- Covered all required platform surfaces: EventStore domain-service host, EventStore projection/query SDK, EventStore DataProtection, EventStore client envelopes/freshness/error codes, tenant claims transformation, Aspire publish helpers, FrontComposer UI primitives, Commons HTTP helpers, and Builds shared props/targets.
- Preserved Story 8.1 and Story 8.2 residual blocker wording, including the Release source-mode guard and the five pre-existing tenant-event failures.
- Added `tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs` to verify required rows, required fable-gap rows, status vocabulary, normalized evidence paths, no-migration wording, exact dependent-story coverage, exact per-row fable gap coverage, available-row release/submodule proof wording, proof/rollback wording for every row, validation-evidence symbols, executable `rg` evidence, duplicate matrix markers, and the current baseline-to-worktree no-production-migration diff guard.

### Commands Attempted

| Command | Result | Notes |
| --- | --- | --- |
| `dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` | Pass | Debug source-mode root test assembly builds cleanly for the new fitness tests. |
| `dotnet ./tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.PlatformApiPrerequisitesTests` | Pass | 10 passed, 0 failed. |
| `for surface in 'EventStore domain-service host' 'EventStore projection/query SDK' 'EventStore DataProtection' 'EventStore client envelopes/freshness/error codes' 'Tenant claims transformation' 'Aspire publish helpers' 'FrontComposer UI primitives' 'Commons HTTP helpers' 'Builds shared props/targets'; do rg -n -F "$surface" _bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md >/dev/null || exit 1; done` | Pass | Every required matrix surface name is checked independently. |
| `git diff --check` | Pass | No whitespace or conflict-marker issues. |

### Remaining Blockers

- No Parties source migration starts in Story 8.3. Later migration stories remain gated by the matrix row status, proof requirements, rollback wording, and owner decisions.
- Full `Hexalith.Parties.Tests` Release source-mode remains blocked by the Story 8.1 `Hexalith.Memories` Release guard.
- Full `Hexalith.Parties.Tests` Debug source-mode still has the five pre-existing tenant-event failures recorded by Story 8.1 and Story 8.2.

## Story 8.4 Leaf Project Retirement - 2026-07-07

### Focused Changes

- Moved `PartyAggregate` from the retired production `src/Hexalith.Parties.Server` shell into `src/Hexalith.Parties/Domain/PartyAggregate.cs` under `Hexalith.Parties.Domain`.
- Deleted the empty `src/Hexalith.Parties.Server` production project shell and removed it from `Hexalith.Parties.slnx`.
- Deleted `src/Hexalith.Parties.ServiceDefaults` and updated the `parties`, `parties-ui`, and `parties-mcp` hosts to consume `Hexalith.Commons.ServiceDefaults` directly.
- Preserved service-default behavior: `/health`, `/alive`, `/ready`, `RegisterDefaultSelfCheck=false`, and `ActivitySourceNames.Add("Hexalith.Parties")`.
- Updated aggregate tests, domain publication tests, service-default compatibility tests, MCP/deploy guards, docs, and project context for the retired paths.
- Added `RetiredLeafProjectFitnessTests` to guard that retired production paths stay absent from `.slnx` and production project references.
- Kept `Hexalith.Parties.Authentication` in place. The Story 8.3 tenant-claims transformation row remains `needs-additive-api`, so auth retirement stays gated.
- Review follow-up hardened the no-unapproved-migration guard so approved Story 8.4 paths must match aggregate-move or service-default-retirement diff shapes, normalized retired path checks, parsed the tenant-claims matrix row directly, and documented the ServiceDefaults migration target.

### Commands Attempted

| Command | Result | Notes |
| --- | --- | --- |
| `dotnet build tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` | Pass | Aggregate test project builds against moved aggregate; 0 warnings, 0 errors. |
| `dotnet ./tests/Hexalith.Parties.Server.Tests/bin/Debug/net10.0/Hexalith.Parties.Server.Tests.dll` | Pass | 237 passed. |
| `dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` | Pass | Root test assembly builds cleanly; 0 warnings, 0 errors. |
| `dotnet ./tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.HealthChecks.ServiceDefaultsCompatibilityTests` | Pass | 8 passed; validates Commons direct defaults preserve Parties health and telemetry options. |
| `dotnet ./tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.RetiredLeafProjectFitnessTests` | Pass | 3 passed; validates retired production paths are absent and Authentication remains gated. |
| `dotnet build tests/Hexalith.Parties.DeployValidation.Tests/Hexalith.Parties.DeployValidation.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` | Pass | Deploy validation test project builds cleanly; 0 warnings, 0 errors. |
| `dotnet ./tests/Hexalith.Parties.DeployValidation.Tests/bin/Debug/net10.0/Hexalith.Parties.DeployValidation.Tests.dll -class Hexalith.Parties.DeployValidation.Tests.K8sManifestPublishTests` | Pass | 5 passed. |
| `dotnet build tests/Hexalith.Parties.Mcp.Tests/Hexalith.Parties.Mcp.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` | Pass | MCP test project builds cleanly; 0 warnings, 0 errors. |
| `dotnet ./tests/Hexalith.Parties.Mcp.Tests/bin/Debug/net10.0/Hexalith.Parties.Mcp.Tests.dll -class Hexalith.Parties.Mcp.Tests.PartiesMcpProjectFitnessTests` | Pass | 5 passed. |
| `dotnet ./tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.PlatformApiPrerequisitesTests` | Pass | 10 passed after narrowing the no-unapproved-migration guard to allow only the approved Story 8.4 leaf-retirement paths. |
| `dotnet build tests/Hexalith.Parties.Sample.Tests/Hexalith.Parties.Sample.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` | Pass | Sample test project builds cleanly after retired project removal; 0 warnings, 0 errors. |
| `dotnet ./tests/Hexalith.Parties.Sample.Tests/bin/Debug/net10.0/Hexalith.Parties.Sample.Tests.dll -class Hexalith.Parties.Sample.Tests.SampleOnboardingGuardrailTests` | Pass | 7 passed; sample production project stays within approved consumer references. |
| `dotnet ./tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.PlatformApiPrerequisitesTests` | Pass | 10 passed after review hardening changed the approved path list from a broad bypass into narrow diff-shape checks. |
| `git diff --check` | Pass | No whitespace or conflict-marker issues. |

### Remaining Blockers

- `Hexalith.Parties.Authentication` remains intentionally unretired because the Story 8.3 tenant-claims transformation row is still `needs-additive-api`.
- Existing Epic 8 residual blockers from Stories 8.1-8.3 remain unchanged unless explicitly closed by later stories.

## Story 8.5 EventStore Domain-Service SDK Host Cutover - 2026-07-07

### Focused Changes

- Moved the production Parties host to the EventStore DomainService SDK shape with `builder.AddEventStoreDomainService(typeof(PartyAggregate).Assembly)` and `app.UseEventStoreDomainService()`.
- Removed the hand-written production `MapPost("/process")` route and retired the production `PartyDomainServiceInvoker` registration; EventStore's `DaprDomainServiceInvoker` remains only inside the projection/rebuild compatibility set needed by the retained `AggregateActor`.
- Replaced `PartyDomainServiceInvoker` with keyed `PartyDomainProcessor : IDomainProcessor, IAggregateReplay` for domain `party`.
- Registered every casing variant of the `party` keyed processor because the SDK keyed lookup is exact-match and the retired invoker accepted case-insensitive domains.
- Restored the narrow EventStore Server projection/rebuild compatibility registrations still required by local projection actors before Story 8.6: projection checkpoint stores, projection discovery, rebuild cleanup, projection polling, `AggregateActor`, and its activation dependencies.
- Preserved Parties-specific validation rejection, protected current-state unprotection/redaction, erasure retry verification, and erasure-status persistence.
- Kept local degraded-response middleware and DAPR health checks because the Story 8.3 platform row remains `needs-additive-api`.
- Kept projection/query actors, AppHost publish helpers, DataProtection/cursor codecs, MCP/client/UI, payload protection engine, and `Hexalith.Parties.Authentication` out of scope.
- Kept DAPR ACLs `/process`-only; SDK `/query`, `/project`, `/replay-state`, and metadata endpoints are not allowed through service invocation in Story 8.5.
- Recorded the EventStore submodule pin proof: `references/Hexalith.EventStore` at `9f8b54dc161a4d5a9b2e6b1deacf331d1b80f1e0`.

### Commands Attempted

| Command | Result | Notes |
| --- | --- | --- |
| `git -C references/Hexalith.EventStore rev-parse HEAD` | Pass | Returned `9f8b54dc161a4d5a9b2e6b1deacf331d1b80f1e0`. |
| `dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` | Pass | 0 warnings, 0 errors. |
| `dotnet ./tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.Domain.PartyDomainProcessorValidationTests` | Pass | 13 passed; covers validation rejection, protected-payload redaction, retry verification, and erasure-status paths. |
| `dotnet ./tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.Gateway.PartiesProcessEndpointTests` | Fail before fix, pass after review fixes | Pre-fix DI validation failed because projection checkpoint services were no longer registered after removing `AddEventStoreServer`; final rerun passed with 8 passed after adding projection/rebuild compatibility registrations, SDK replay coverage, and all-case `party` keyed registrations. |
| `dotnet ./tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.Gateway.EventStoreGatewayRoutingTests` | Pass | 52 passed; output includes expected DAPR-sidecar connection warnings from EventStore gateway tests running without a sidecar. |
| `dotnet ./tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.ArchitecturalFitnessTests` | Pass | 21 passed; validates SDK host shape, request-path boundaries, and architectural guardrails. |
| `dotnet ./tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.PlatformApiPrerequisitesTests` | Pass | 10 passed; validates the Story 8.5 diff shape, projection/rebuild compatibility registration guard, and prerequisite matrix proof. |
| `dotnet ./tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.RetiredLeafProjectFitnessTests` | Pass | 4 passed; validates retired leaf project guardrails remain intact. |
| `dotnet build tests/Hexalith.Parties.DeployValidation.Tests/Hexalith.Parties.DeployValidation.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` | Pass | 0 warnings, 0 errors. |
| `dotnet ./tests/Hexalith.Parties.DeployValidation.Tests/bin/Debug/net10.0/Hexalith.Parties.DeployValidation.Tests.dll -class Hexalith.Parties.DeployValidation.Tests.DaprAccessControlFitnessTests` | Pass | 5 passed; ACL remains deny-by-default with only `eventstore -> POST /process`. |
| `git diff --check` | Pass | No whitespace or conflict-marker issues. |

### Remaining Blockers

- EventStore degraded-response and DAPR-health owner parity remains `needs-additive-api`; Parties keeps local degraded-response middleware and DAPR health checks.
- EventStore projection/query SDK migration remains deferred to Story 8.6; Parties keeps projection/query actors, rebuild services, local adapters, and freshness fallback.
- Aspire/AppHost publish helper cleanup remains deferred to Story 8.8; AppHost topology and publish helpers were not migrated in Story 8.5.
- Existing Epic 8 residual release blockers from Stories 8.1-8.4 remain unchanged unless explicitly closed by later stories.

## Run All Tests And Fix Issues - 2026-07-08

### Focused Changes

- EventStore and Tenants source builds now evaluate against the same central package version values used by CPVM; regenerated outputs contain no retired EventStore version references.
- Package-mode tests can consume the source-only `Hexalith.Commons.ServiceDefaults` project when it exists locally, without switching all Commons dependencies to source mode.
- Client dependency fitness now treats `Hexalith.Commons.Http` and `Hexalith.EventStore.Contracts` as direct client package references instead of transitive violations.

### Commands Attempted

| Command | Result | Notes |
| --- | --- | --- |
| `pwsh -NoProfile -File scripts/test.ps1 -Lane all -Configuration Debug -ContinueOnFailure -ResultsDirectory TestResults/bmad-source-debug-final -Properties UseHexalithProjectReferences=true,UseNuGetDeps=false,NuGetAudit=false,MinVerVersionOverride=1.0.0,GeneratePackageOnBuild=false,BuildInParallel=false` | Pass | All 15 test projects passed in source-reference mode. Integration tests: 34 total, 28 succeeded, 6 expected skips. |
| `pwsh -NoProfile -File scripts/test.ps1 -Lane all -Configuration Release -ContinueOnFailure -ResultsDirectory TestResults/bmad-package-final-2 -Properties UseHexalithProjectReferences=false,UseNuGetDeps=true,NuGetAudit=false,MinVerVersionOverride=1.0.0` | Pass | All 15 test projects passed in package mode. Integration tests: 34 total, 28 succeeded, 6 expected skips. |
| Full working-tree search for the retired EventStore version literal | Pass | No remaining working-tree references, including ignored generated outputs. |
| `bash scripts/check-no-warning-override.sh` | Pass | `OK: no warning-override or nested-submodule regressions detected in active CI/build scripts.` |
| `git diff --check && git -C references/Hexalith.Builds diff --check && git -C references/Hexalith.EventStore diff --check && git -C references/Hexalith.Tenants diff --check` | Pass | No whitespace or conflict-marker issues in root or checked submodule diffs. |
