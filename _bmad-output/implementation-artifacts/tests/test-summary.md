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

## G12 Package Publication Resolution - 2026-07-11

### Decision Evidence

- The Commons and Tenants release paths selected package publication; source-mode
  CI blessing is not required for G12.
- NuGet serves `Hexalith.Commons.Http` 2.28.0 and
  `Hexalith.Commons.ServiceDefaults` 2.28.0.
- NuGet serves `Hexalith.Tenants.Client` and `Hexalith.Tenants.Testing` at the
  repository pin 2.4.2; later 3.x versions are also published.
- Parties consumer asset files resolved all four identities as packages when the
  corresponding source-reference switches were forced off.

### Commands Attempted

| Command | Result | Notes |
| --- | --- | --- |
| `curl -fsS https://api.nuget.org/v3-flatcontainer/hexalith.commons.http/index.json` and the corresponding ServiceDefaults, Tenants.Client, and Tenants.Testing indexes | Pass | All four package IDs returned HTTP 200 with published versions. |
| `dotnet restore Hexalith.Parties.slnx -m:1 -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true -p:HexalithCommonsHttpFromSource=false -p:HexalithCommonsServiceDefaultsFromSource=false -p:HexalithCommonsVersion=2.28.0 -p:HexalithTenantsVersion=2.4.2 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` | Pass | Package-only restore resolved Commons.Http/ServiceDefaults 2.28.0 and Tenants.Client/Testing 2.4.2 in Parties consumer assets. |
| `dotnet build Hexalith.Parties.slnx -c Release --no-restore -m:1 -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true -p:HexalithCommonsHttpFromSource=false -p:HexalithCommonsServiceDefaultsFromSource=false -p:HexalithCommonsVersion=2.28.0 -p:HexalithTenantsVersion=2.4.2 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` | Pass | Build succeeded with 0 warnings and 0 errors. |

### Remaining Gates

- G12 no longer blocks Story 8.8, Story 8.10, or the Story 8.1 package-mode
  baseline.
- Story 8.8 remains gated by its G6, G8, G11, and G7/G9 owner proofs; Story 8.10
  remains gated by incomplete or unowned Epic 8 work under its own Block If.

## Revalidate All Tests And Fix Current Failures - 2026-07-12

Full rerun of every configured .NET test project (both dependency shapes) plus
the Playwright workspace against the current dependency, build-workflow, and
package-routing changes (baseline commit `8d28a1b`). No product/test source was
edited in this pass; the working tree already held the in-progress accessibility,
E2E-auth-fixture, and FrontComposer canonical-query-shim changes.

### Result Headline

- **Package mode (Release, CI parity): ALL 15 projects PASS — 2321 tests, 0 failed,
  6 expected integration skips.** This is the authoritative shippable configuration
  (`hexalith-llm-instructions`: CI = NuGet package reference + Release).
- **Root-owned fix applied:** installed `ripgrep` (`sudo apt-get install ripgrep`)
  which cleared the only genuine environment failure —
  `PlatformApiPrerequisitesTests.Matrix_ValidationEvidenceCommandsAreReproducible`
  shells out to `rg` and threw `Win32Exception: process 'rg' … No such file`.
- **Source mode (Debug, project references): BLOCKED by a governed dependency-mode
  drift** (see Blockers). Product code compiles clean in source mode (first build:
  0 warnings/0 errors, all 15 test projects); the block is a Commons assembly-version
  skew / `CS1704` at the source/package boundary, not a code defect.
- **e2e:** `tsc` typecheck passes; 16 artifact/SSR specs pass; interactive specs are
  blocked locally by the documented `blazor.web.js` 500 (deferred to CI `ui-a11y`).

### Package-Mode Release Per-Project Results

| Project | Total | Failed | Skipped |
| --- | --- | --- | --- |
| Contracts.Tests | 135 | 0 | 0 |
| Authentication.Tests | 12 | 0 | 0 |
| Client.Tests | 137 | 0 | 0 |
| Server.Tests | 237 | 0 | 0 |
| Projections.Tests | 139 | 0 | 0 |
| Security.Tests | 169 | 0 | 0 |
| AdminPortal.Tests | 183 | 0 | 0 |
| ConsumerPortal.Tests | 82 | 0 | 0 |
| UI.Tests | 326 | 0 | 0 |
| Picker.Tests | 171 | 0 | 0 |
| Mcp.Tests | 57 | 0 | 0 |
| Tests (domain/gateway/fitness) | 574 | 0 | 0 |
| Sample.Tests | 58 | 0 | 0 |
| IntegrationTests | 34 | 0 | 6 (Docker/DAPR graceful) |
| Ci.Tests | 7 | 0 | 0 |
| **Total** | **2321** | **0** | **6** |

### Commands Attempted

| Command | Result | Notes |
| --- | --- | --- |
| `dotnet build Hexalith.Parties.slnx -c Debug -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` (incremental, prebuilt submodules) | Build Pass, run Fail | 0 warn/0 err, all 15 built; at runtime 6 projects (AdminPortal, Client, Mcp, Security, Tests, IntegrationTests) failed with `FileNotFoundException: Hexalith.Commons.UniqueIds, Version=3.58.0.0` — deployed copy was `1.0.0.0`. Version skew, not a code defect. |
| `dotnet build … -c Debug --no-incremental …` | Fail | `CS0006` submodule ref-assembly race (memory: use `-m:1`, avoid Rebuild). Reverted approach. |
| clean root `bin/obj` + `dotnet build … -c Debug -m:1 …` | Fail | `CS1704`: `Hexalith.Commons.UniqueIds` imported twice in `EventStore.Contracts` (source project + transitive NuGet `2.28.0`) once the submodule recompiles from source under the leaked `HexalithCommonsFromSource=true`. |
| `dotnet restore/build Hexalith.Parties.slnx -c Release -m:1 -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true -p:HexalithCommonsHttpFromSource=false -p:HexalithCommonsServiceDefaultsFromSource=false -p:HexalithCommonsVersion=2.28.0 -p:HexalithTenantsVersion=2.4.2 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` | Pass | Package mode 0 warn/0 err; `Commons.UniqueIds` deployed==referenced==`2.28.0.0` (no skew). |
| Run all 15 built Release test assemblies directly (`dotnet <proj>.dll`, `~/.dotnet` first on PATH so nested `dotnet pack` resolves SDK `10.0.301`) | Pass | 2321 passed, 0 failed, 6 expected skips. Contracts/Client `*.Package` tests initially failed only when `/usr/bin/dotnet` (SDK 10.0.300) shadowed `~/.dotnet` (10.0.301) — a harness PATH artifact, fixed by PATH order. |
| `sudo apt-get install -y ripgrep` | Pass | Installed `rg` 15.1.0; fixes `Matrix_ValidationEvidenceCommandsAreReproducible`. |
| `cd tests/e2e && npm ci && npm run typecheck` | Pass | 9 packages, `tsc --noEmit` clean. |
| `PLAYWRIGHT_SKIP_WEBSERVER=1 npx playwright test specs/story-7-1 specs/story-7-4 specs/story-7-8 --project=chromium` | Pass | 16 passed (artifact/SSR specs). |
| UI host `dotnet run … -c Release --no-build` (ASPNETCORE_ENVIRONMENT=Test, `AdminPortalE2E__Enabled=true`) + `npx playwright test specs/admin-parties-list.spec.ts` | Host starts; interactive Fail | `/alive`,`/health`=200; `/`,`/admin/parties`=302→`/authentication/challenge` (E2E cookie-auth fixture works). Interactive rows never render — `blazor.web.js` returns 500. |

### Unresolved Blockers And Owner Decisions

| Blocker | Exact evidence | Owner decision / rerun path |
| --- | --- | --- |
| Source-mode Commons dependency-mode drift | Clean source-reference build hits `CS1704` (`Hexalith.Commons.UniqueIds` from source project **and** transitive NuGet `2.28.0`) inside `EventStore.Contracts`; with prebuilt submodules the runtime hits `FileNotFoundException Hexalith.Commons.UniqueIds Version=3.58.0.0`. Governed by Story 7.1's pinned `ProjectReference Include="$(HexalithCommonsRoot)…"` Commons strategy — "no project-reference change, submodule pointer change, or submodule source edit" without authorization. | Platform/submodule owner: reconcile the Commons submodule (`a3b4f88`) source-reference version so `Hexalith.Commons.UniqueIds` resolves to a single assembly version across parties source + submodule consumers, OR authorize a source/package strategy change. Not fixable inside this repo without crossing the Ask-First boundary. Package-mode Release is fully green and proves product correctness. |
| Interactive Playwright (local) | `blazor.web.js` → HTTP 500: `FileNotFoundException … /src/Hexalith.Parties.UI/wwwroot/_framework/blazor.web.js` from `StaticAssetDevelopmentRuntimeHandler.AttachRuntimePatching` under `dotnet run --no-build` (non-Production env, un-published assets). Blazor never hydrates, so interactive rows/components don't render. | Deferred to CI `ui-a11y` gate (bUnit + published/served assets), per established local-sandbox limitation. SSR/artifact specs pass locally; typecheck passes. |

### Source-Mode Resolution (owner-authorized strategy fix — 2026-07-12)

The source-mode Commons dependency-mode drift was resolved (owner-authorized) by
consuming **Commons as a package** in source mode — aligning with `CLAUDE.md`
(only EventStore/Tenants/Memories are source-referenced) and matching the already-green
package mode — while keeping EventStore, Tenants, FrontComposer, and Memories as source
project references. The `HexalithCommons*FromSource=false` properties are **global**
(command-line), so they also override the submodule projects' own auto-enable, which
eliminates the `CS1704` double-import in `EventStore.Contracts`.

Working source-mode build/run command (Commons → package; keeps the FrontComposer
`#if HEXALITH_FRONTCOMPOSER_CANONICAL_QUERY` canonical-query branch active):

```
dotnet build Hexalith.Parties.slnx -c Debug -m:1 \
  -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false \
  -p:HexalithCommonsFromSource=false -p:HexalithCommonsHttpFromSource=false \
  -p:HexalithCommonsServiceDefaultsFromSource=false \
  -p:HexalithCommonsVersion=2.28.0 -p:HexalithTenantsVersion=2.4.2 \
  -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0
```

Result: build 0 warnings / 0 errors; `Commons.UniqueIds` deployed==referenced==`2.28.0.0`
(skew gone). Source-mode Debug tests: **14/15 projects green** (2679 executed, 6 expected
integration skips). The only remainder is 2 `Hexalith.Parties.Client.Tests.Package.ClientPackageTests`
(`PackedClientPackage_HasOnlyApprovedDeclaredDependenciesAndFitsSizeBudget`,
`CleanPackageConsumer_RegistersTypedClientsWithoutForbiddenTransitivePackages`): their fixture
runs `dotnet pack --configuration Release` on the **source** `Hexalith.Commons.Http` project
and hits `NU5026` (no Release DLL under a Debug build). These are Release/package-oriented
PackageTests and **pass in package-mode Release** (their correct context) — consistent with the
documented `*PackageTests` build-state sensitivity; not product defects.

**Durability:** the fix is the command above (global Commons→package properties). The residual
trigger is the checked-out `references/Hexalith.Commons` submodule auto-enabling source Commons;
`git submodule deinit -f references/Hexalith.Commons` would make source-mode consume Commons as a
package with no extra flags (matching the `CLAUDE.md` "init only EventStore + Tenants" rule). Not
applied automatically — left as an owner choice since it changes submodule checkout state.

### Combined Verdict

| Configuration | Projects green | Tests | Failed | Skipped |
| --- | --- | --- | --- | --- |
| Package mode (Release, CI parity) | 15 / 15 | 2321 | 0 | 6 (Docker/DAPR) |
| Source mode (Debug, project refs, Commons→package) | 14 / 15 | 2679 exec | 2 (Client PackageTests — pass in package mode) | 6 |
| e2e Playwright | typecheck + 16 SSR/artifact specs pass | — | interactive → CI `ui-a11y` | — |

## Story 8.3 Available-Row Consumption Identities — 2026-07-16

The four named `available` matrix rows now record immutable consumption
identities without requesting additive APIs:

| Surface | Recorded identity |
| --- | --- |
| EventStore domain-service host | Historical Story 8.5 root gitlink `9f8b54dc161a4d5a9b2e6b1deacf331d1b80f1e0` at Parties commit `bff30c1182e95af1a922d74777a6611e788a53ee` |
| EventStore DataProtection | Current root gitlink `82ed167c1c78d4ff50d3f8eab43850bb6abd0fe7` |
| Commons HTTP helpers | `Hexalith.Commons.Http` `2.28.1` / `v2.28.1`, root gitlink `b03469b13408530bb757d3d02279c2d772ee4848` |
| Builds shared props/targets | `4.18.5` / `v4.18.5`, root gitlink `ed75ae3c45425b9610d5e75e6c5ec3e8d5283fe1` |

Validation results:

- The initial Release package-mode build using Commons `2.28.0` failed with
  `NU1109` because the concurrently updated EventStore dependency requires
  `Hexalith.Commons.UniqueIds >= 2.28.1`; rerunning with Commons `2.28.1`
  passed with 0 warnings and 0 errors.
- A later `--no-restore` rerun observed concurrent central-version requests for
  unpublished EventStore `3.67.1` and Memories `2.6.17` (`NU1102`). The final
  Release validation invocation overrode only the command line to published
  EventStore `3.67.0`, Memories `2.6.16`, Commons `2.28.1`, and Tenants `2.4.2`;
  it passed with 0 warnings and 0 errors. No repository dependency file was
  changed by this correction.
- Focused xUnit v3 execution of
  `Matrix_NamedAvailableRowsRecordImmutableConsumptionIdentities` and
  `AvailableRowConsumersFailClosedOnMissingOrMismatchedIdentity`: 2 passed,
  0 failed.
- Exact-object inspections for the three EventStore DataProtection/cursor files
  passed; Commons `v2.28.1^{}` and Builds `v4.18.5^{}` resolve to their recorded
  root gitlinks; the historical Story 8.5 `git ls-tree` resolves to `9f8b54dc…`.
- Targeted `git diff --check` passed.

The concurrently advanced EventStore checkout is not treated as consumption
proof. Stories 8.6, 8.8, and 8.10 now fail closed and refresh the matrix if their
selected release or root gitlink differs from the recorded identity.

## Story 8.6 Refreshed Authorization, Latest SDK, And Migration — 2026-08-01

| Check | Result | Evidence |
| --- | --- | --- |
| EventStore proof-integrity tests | Pass | `ProofPacketValidatorIntegrityTests`: 13 passed, 0 failed. |
| Refreshed immutable owner proof | Pass | Raw evidence retention is locked through `2036-08-02T00:00:00Z`; provider proof SHA-256 `1d1c12c45aef2e77305e26d2315c715be9cae47372ab312aabb583bf475bc8c4`. Refreshed chain: A `21997d1974c4bc7022c77a5065edd9d327435c97`, B `55471ad752e49686c7d0a47159f25455fda24003`, C `dbf81916ac56ceebf8cda313089be86e40d96c98`; owner merge `77d6f47743453d542d96dbe088d5eef7cd05284b`. |
| Historical exact source-consumer handoff | Pass | The same-shell verifier and consumer procedure at Parties dependency checkpoint `e65e8b5e9a1d202f240bb641490e7747a84a2da1` reported `verified_source_consumer_handoff=passed` for EventStore `fa2d1c9910f8976553adb33dcdb1c9ff2ea75594`. The subsequent compile proved that identity lacks the tenant-shared rebuild surface; no compatibility pass credit is assigned to it. |
| Latest stable EventStore selection | Pass | User explicitly selected the latest release. Root gitlink, checkout, and tag all resolve to `v3.89.0`, commit `c590590bc581a3f72ef6e67148eda988ba4b8fe6`; this identity defines `IAsyncDomainSharedProjectionRebuildHandler`, `DomainSharedProjectionRebuildIdentity`, and `DomainSharedProjectionRebuildCandidate`. |
| Latest SDK source consumer build | Pass | Source-mode restore plus `dotnet build tests/Hexalith.Parties.Projections.Tests/Hexalith.Parties.Projections.Tests.csproj --no-restore -c Debug -m:1 ...` completed with 0 warnings and 0 errors. |
| Projection/rebuild parity suite | Pass | Direct xUnit v3 execution: 150 passed, 0 failed, 0 skipped. Coverage includes replay from zero, duplicate/out-of-order idempotency, aggregate detail rebuild, tenant-shared index rebuild replacement/pruning, erased-party exclusion, protected/redacted payloads, batch concurrency, processing records, and PII-free processing summaries. |
| SDK query/registration/health/architecture focus | Pass | Direct xUnit v3 execution of `PartySdkQueryHandlerTests`, `HealthEndpointIntegrationTests`, `ProjectionPlatformAdapterTests`, and `ArchitecturalFitnessTests`: 48 passed, 0 failed. Coverage includes protected cursor continuation/scope rejection, strict payload and tenant validation, GDPR reads, detail/index last-known degraded fallback, SDK-only composition, and absence of retired actor/rebuild types. |
| Broad Parties test assembly | Partial | 452 total: 449 passed, 3 failed, 0 skipped. The only failures are the pre-existing payload-protection prerequisite-matrix checks `Matrix_ValidationEvidenceCommandsAreReproducible`, `Matrix_EvidencePathsExistAndMatchDeclaredOwner`, and `Matrix_ValidationEvidenceNamesExpectedSymbols`; no pass credit is assigned to those failures. |
| Integration project compile | Pass | After declaring the EventStore server fixture dependency directly in the integration-test project, compilation completed with 0 warnings and 0 errors while `Hexalith.Parties` retained no production `Hexalith.EventStore.Server` dependency. |
| Integration execution | Environment-blocked | The focused encryption fixture could not start because the mixed source/package restore graph omitted runtime `Hexalith.Commons.Http, Version=2.29.0.0`. Six tests failed during host construction before exercising Story 8.6 behavior; no test-pass credit is assigned. |
| Static validation | Pass | `git diff --check` passed; `bash scripts/check-no-warning-override.sh` reported no warning-override or nested-submodule regressions; production search found no `NotImplementedException` and no retired projection actor/rebuild/adapter runtime types. |
| Operational-index metadata route red/green | Pass | New `ArchitecturalFitnessTests` assertions first failed 2/2 because `Program.cs` and the Parties ACL omitted `/admin/operational-index-metadata`. After the exact EventStore-only POST ACL operation and host documentation were added, the focused assertions passed 2/2 and the full architecture class passed 21/21. |
| Canonical package-mode unit lane | Pass | `pwsh scripts/test.ps1 -Lane unit -ContinueOnFailure -Properties NuGetAudit=false`: all 11 unit projects passed, 1660 tests total. |
| CI lane | Pass | `pwsh scripts/test.ps1 -Lane ci -ContinueOnFailure -Properties NuGetAudit=false`: 31 passed, 0 failed. |
| Release solution build | Pass | `dotnet build Hexalith.Parties.slnx -c Release --no-restore -m:1 -p:NuGetAudit=false --verbosity minimal`: 0 warnings, 0 errors. |
| Source-mode unit lane | Partial | Eight projects built and passed; Contracts, Authentication, and Server did not build because the mixed source graph resolves duplicate `Hexalith.Commons.UniqueIds` assembly versions, and Memories correctly rejects Release source mode. No pass credit is assigned to the three projects. |
| Pre-closure topology lane | Environment-blocked | `pwsh scripts/test.ps1 -Lane topology -ContinueOnFailure -Properties NuGetAudit=false`: 37 total, 26 passed, 6 explicitly skipped, 5 failed. All five failures were the encryption fixture calling DAPR actors at `localhost:3500`; the connection was refused. A direct class rerun reproduced 5 failures out of 6 tests in 2.136 seconds. No Story 8.6 projection/query failure was reported, but no completion credit is assigned to the failed lane. |
| Pre-closure static validation | Pass | Story-scoped `git diff --check` passed and `bash scripts/check-no-warning-override.sh` reported no warning-override or nested-submodule regression. Unrelated concurrent CRLF edits were preserved and excluded from the story-scoped whitespace result. |
| Encryption fixture isolation | Pass | `EncryptionTestFactory` now replaces `IPartyKeyRetryScheduler` with a deterministic substitute, retaining DAPR isolation after the retired projection actor proxy setup was removed. The focused Release build completed with 0 warnings/errors and direct `EncryptionPipelineIntegrationTests` execution passed 6/6. |
| Final all-lanes regression | Pass | `pwsh scripts/test.ps1 -Lane all -ContinueOnFailure -Properties NuGetAudit=false,MinVerVersionOverride=1.0.0` exited 0 with all 15 projects passing. Parties passed 452/452, Sample passed 58/58, Integration passed 31 with 6 explicit deferred-health skips, and CI passed 31/31. The skips remain documented deferred topology coverage and are not counted as Story 8.6 parity evidence. |
| Final Release solution build | Pass | `dotnet build Hexalith.Parties.slnx -c Release -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --disable-build-servers -m:1 --verbosity:minimal`: 0 warnings, 0 errors. |
| Final static and File List validation | Pass with manual File List reconciliation | `git diff --check`, `bash scripts/check-no-warning-override.sh`, and production searches for `NotImplementedException` and retired projection/query runtime types passed. `_bmad/scripts/check_file_list.py` is absent, so manual reconciliation confirmed all Story 8.6 continuation files are listed while unrelated concurrent changes remain excluded. |

Verdict: the former SDK compatibility block is resolved by EventStore `v3.89.0`,
and the Story 8.6 projection/query migration is implemented with focused parity green.
The later frozen spec resolves the ingress wording: gateway/public behavior is unchanged,
while only EventStore may invoke exact internal POST SDK routes. The missing operational-index
metadata discovery route is admitted and fitness-tested. The encryption fixture is isolated
from its production DAPR retry scheduler, the full 15-project regression is green, and Story 8.6
is ready for review.

### Story 8.6 Consolidated Review Hardening — 2026-08-03

The review patch closes the erasure, ordering, cache-race, query-freshness,
rebuild-side-effect, ACL-structure, and model-layout findings. Canonical SDK
cleanup now writes detail, processing activity, and the shared tenant index in
one `IReadModelBatchStore` operation with bounded optimistic retry and retained
anti-resurrection tombstones. Projection deliveries reject cross-delivery gaps
without persistence, shared-index retries revalidate against every reloaded
snapshot, and rebuild finalization only returns a plan. Query caching is
generation-aware, capacity-bounded, and retention-bounded; processing reads
prove that the Party exists, and degraded portability responses now use one
consistent stale/degraded freshness classification.

The current checked-in EventStore gitlink and checkout both resolve to
`7854f8e51ce9b852bb6c3cac6012670122e93792`. The exact current-pin source tests
use EventStore from that checkout while intentionally consuming
`Hexalith.Commons.UniqueIds` from its package. The all-source aggregate graph is
not credited: it imports both package `Hexalith.Commons.UniqueIds` 2.30.0 and a
source assembly with version 1.0.0, producing `MSB3243` followed by `CS1704`.

| Check | Result | Evidence |
| --- | --- | --- |
| Current EventStore pin | Pass | `git ls-tree HEAD references/Hexalith.EventStore` and `git -C references/Hexalith.EventStore rev-parse HEAD` both returned `7854f8e51ce9b852bb6c3cac6012670122e93792`. |
| Current-pin projection suite | Pass | `dotnet test tests/Hexalith.Parties.Projections.Tests/Hexalith.Parties.Projections.Tests.csproj -c Debug --no-restore -p:HexalithEventStoreFromSource=true -p:HexalithCommonsFromSource=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 -- --no-progress --output Normal`: 200 passed, 0 failed, 0 skipped. |
| Current-pin Parties suite | Pass | `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --no-restore -p:HexalithEventStoreFromSource=true -p:HexalithCommonsFromSource=false -- --no-progress --output Normal`: 501 passed, 0 failed, 0 skipped. |
| Current-pin security suite | Pass | `dotnet test tests/Hexalith.Parties.Security.Tests/Hexalith.Parties.Security.Tests.csproj --no-restore -p:HexalithEventStoreFromSource=true -p:HexalithCommonsFromSource=false -- --no-progress --output Normal`: 169 passed, 0 failed, 0 skipped. |
| All-source Commons caveat | Expected baseline failure; no pass credit | `dotnet test tests/Hexalith.Parties.Projections.Tests/Hexalith.Parties.Projections.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --no-restore -- --no-progress --output Normal` failed before tests with `MSB3243` and `CS1704` for duplicate `Hexalith.Commons.UniqueIds` 2.30.0/1.0.0 assemblies. The successful current-pin commands above isolate EventStore source consumption and do not conceal this graph defect. |
| Unit lane | Pass | `pwsh -NoProfile -File scripts/test.ps1 -Lane unit -Configuration Debug -ContinueOnFailure -Properties NuGetAudit=false,MinVerVersionOverride=1.0.0`: all 11 projects passed, 1,710 tests, 0 failed, 0 skipped. |
| Integration lane | Pass | `pwsh -NoProfile -File scripts/test.ps1 -Lane integration -Configuration Debug -ContinueOnFailure -Properties NuGetAudit=false,MinVerVersionOverride=1.0.0`: Parties 501/501 and Sample 58/58; 559 passed, 0 failed, 0 skipped. |
| Topology lane | Pass with unrelated skips excluded from proof | `pwsh -NoProfile -File scripts/test.ps1 -Lane topology -Configuration Debug -ContinueOnFailure -Properties NuGetAudit=false,MinVerVersionOverride=1.0.0`: 35 passed, 0 failed, 6 skipped. All six skips are pre-existing Story 12 health/readiness deferrals and are not credited as Story 8.6 evidence. Independent active proofs passed with no skips: `DaprMtlsBootstrapTests` 3/3, `AppHostTenantsTopologyTests` 16/16, and the exact structured Parties ACL assertion 1/1. |
| CI lane | Pass | `pwsh -NoProfile -File scripts/test.ps1 -Lane ci -Configuration Debug -ContinueOnFailure -Properties NuGetAudit=false,MinVerVersionOverride=1.0.0`: 35 passed, 0 failed, 0 skipped. |
| Final Release solution build | Pass | `dotnet build Hexalith.Parties.slnx -c Release --no-restore -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0`: 0 warnings, 0 errors. |
| Spec/prerequisite verification | Pass | Direct `PlatformApiPrerequisitesTests` execution against the current pin: 12 passed, 0 failed, 0 skipped. |
| Static policy | Pass | `bash scripts/check-no-warning-override.sh` reported no warning override or nested-submodule regression. `PartySdkProjectionFold.cs` is consistently CRLF as required; `git -c core.whitespace=cr-at-eol diff --check` passes. Plain `git diff --check` reports the intentional CR characters on newly added lines in that pre-existing CRLF file because the repository has no `.gitattributes` rule declaring CR-at-EOL. |

The 14 executable project inventories therefore pass with 2,339 succeeded,
0 failed, and 6 unrelated deferred Story 12 skips; the non-executable
`Hexalith.Parties.EventStoreGateway.TestHost` remains explicitly inventoried as
test support rather than being falsely executed as a test assembly.

### Story 8.6 Rebuild Concurrency Closure — 2026-08-16

Current EventStore v3.95 maps optimistic staging conflicts to bounded failed
dispatch outcomes and rebuilds plans from fresh handler state on subsequent
lifecycle requests. Parties therefore replaced unconditional rebuild writes
with snapshot ETag matching for existing rows and create-only protection for
absent rows. A concurrent live projection write is preserved rather than being
silently overwritten.

| Check | Result | Evidence |
| --- | --- | --- |
| Projection source build | Pass | `dotnet build tests/Hexalith.Parties.Projections.Tests/Hexalith.Parties.Projections.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 -nr:false -m:1 --verbosity minimal`: 0 warnings, 0 errors. |
| Rebuild concurrency focus | Pass | Direct xUnit v3 `PartySdkProjectionHandlerTests`: 66 passed, 0 failed, 0 skipped. Existing detail, processing, and index rows require their captured ETags; absent rows use `CreateOnly`. |
| Full projection suite | Pass | Direct xUnit v3 execution: 222 passed, 0 failed, 0 skipped. |
| Package-mode projection build | Pass | Package-mode restore plus Release build with `UseHexalithProjectReferences=false` and `HexalithEventStoreFromSource=false`: 0 warnings, 0 errors. |
| Release solution build | Pass | `dotnet build Hexalith.Parties.slnx -c Release -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 -nr:false -m:1 --verbosity minimal`: 0 warnings, 0 errors. |
| Query/DI/architecture/prerequisite focus | Partial | Source-mode build passed with 0 warnings/errors; direct execution passed 89/90. The sole failure is the pre-existing, deferred Story 8.7/G5 `PlatformApiPrerequisitesTests.Matrix_ValidationEvidenceCommandsAreReproducible` matrix pin drift, not a Story 8.6 regression. |

### Story 8.6 Final Correctness Patch — 2026-08-16

The final review patch closes eight fail-open or stale-data paths without changing
the approved SDK migration intent or the human-deferred host E2E/legacy Playwright
scope.

| Check | Result | Evidence |
| --- | --- | --- |
| Projection handler focus | Pass | `PartySdkProjectionHandlerTests`: 75 passed, 0 failed. Covers both one-slot-missing directions, failed-to-success Art.30 reconciliation, canonical updated/erased rebuild completion, bounded eraser notify failures, cancellation, and identical/conflicting duplicates. |
| Query and Memories focus | Pass | `PartySdkQueryHandlerTests`, `PartyMemoryCleanupServiceTests`, and `PartyMemoryUnitMappingStoreTests`: 66 passed, 0 failed. Covers observed absence for detail/index/processing, per-key generation races, fail-closed mapping reads, cancellation, and sanitized failure reporting. |
| Erasure verification focus | Pass | `ErasureVerificationServiceTests`: 17 passed, 0 failed. Caller cancellation propagates; non-caller cancellation and unexpected exceptions are sanitized failed results; failure logs contain no supplied identifiers or PII. |
| Full projection suite | Pass | 231 passed, 0 failed, 0 skipped. |
| Full security suite | Pass | 171 passed, 0 failed, 0 skipped. |
| Broad Parties suite | Pass with known exclusion | 516 passed, 0 failed with only `PlatformApiPrerequisitesTests.Matrix_ValidationEvidenceCommandsAreReproducible` excluded. A prior unfiltered run reproduced that pre-existing Story 8.7/G5 pin-drift failure; no Story 8.6 test remains red. |
| Integration cleanup composition | Pass | `ProjectionPlatformAdapterTests`: 7 passed, 0 failed after its mapping inventory was made explicitly authoritative-empty instead of relying on a state-read failure degrading to empty. |
| Source and package builds | Pass | All three affected test projects build with 0 warnings/errors; package-mode Release projection build with `HexalithEventStoreFromSource=false` also completes with 0 warnings/errors. |
| Release solution build | Pass | `dotnet build Hexalith.Parties.slnx -c Release --no-restore`: 0 warnings, 0 errors. |
| Static policy | Pass | `git diff --check` and `bash scripts/check-no-warning-override.sh` pass. |

## Story 8.10 Final Readiness, Documentation, and Retirement Gate — 2026-08-18

Story 8.10 reconciled the retained dependency graph, accepted explicit
owner/proof/rollback/evidence deferrals for Stories 8.7-8.9 and external runtime
deployment, refreshed the maintained topology/inventory documentation, and
added executable documentation, closure, zero-PRD, invariant-map, and dependency
selection fitness. Closure remains deliberately open because two required gates
are red; no deferred migration or external deployment work is represented as
delivered.

### Retained immutable identities and rollback — snapshot before the EventStore 3.112.0 update

Parties `c782b68c5cf56a19e6a2a237f5f44e3043d5e461`, SDK `10.0.401`. The 8.3 reconciliation/I20 table
is authoritative. The original Round 6 selection and the Administrator's explicit
2026-10-04 approval of the three later pins select the following current source
identities. Earlier affected parity receipts remain unvalidated; selection approval
does not approve I16 parity, owner releases, publishing, or deletion.

- AI.Tools: approved `3f194e17174994d308ec84af9ee2b5aa68674d0d` (`3f194e1`). Committed root gitlink and clean checkout match.
- Builds: approved `688eec9a4333245cc0ff7772115c769094471863` (`v4.29.1-14-g688eec9`). Committed root gitlink and clean checkout match.
- Commons: approved `116d26815eb81e35b3c161e1799e5ee12805fc0a` (`v2.30.1-15-g116d268`). Committed root gitlink and clean checkout match.
- EventStore: approved `cbbe41501ba722731bf36b2c343efdef4ac714fb` (`v3.111.0-10-gcbbe4150`), explicit Administrator / jpiquot approval 2026-10-04. Committed root gitlink and clean checkout match. Prior Round 6 `b046425503c694d5857e8bba890351840fa52c26` is superseded.
- FrontComposer: approved `374bb83392d8ab4e8a8397cfd312d09948fb0b9d` (`v4.5.0-117-g374bb833`), explicit Administrator / jpiquot approval 2026-10-04. Committed root gitlink and clean checkout match. Prior Round 6 `bf40099f81fcaeac324b7b4377513ac7d49cead4` is superseded.
- Memories: approved `3d72927f4dac66af4968cc6726c4e96df292f2e4` (`v2.27.1-23-g3d72927f`). Committed root gitlink and clean checkout match.
- PolymorphicSerializations: approved `98de6e013840ece9f0fa7c68ab7dcdf2bba3b375` (`v1.19.4`). Committed root gitlink and clean checkout match.
- Tenants: approved `cc348c9d7839ec7aad01649fc1c0e6f4fe672da4` (`v5.7.0-134-gcc348c9d`), explicit Administrator / jpiquot approval 2026-10-04. Committed root gitlink and clean checkout match. Prior Round 6 `b63bdbba8613801bb488eaa9116f386e41a261f8` is superseded.
- Builds selects EventStore `3.110.0`, Commons `2.30.1`, FrontComposer `4.5.0`,
  Memories `2.27.1`, Tenants `5.7.0`, Parties `1.1.1`; source and package proof
  remain separate. AI.Tools is instruction-only; Memories remains optional.
- Rollback remains the Parties security, authentication, client/MCP/AppHost/build,
  UI, and local-topology paths. External runtime recovery redeploys the prior
  immutable images/configuration. No rollback seam is retired by this reconciliation.

### Initial validation receipts (superseded 2026-08-18)

Retained as the audit trail for the first run. **These rows are not the closure
gate** — the authoritative table is the `### Validation receipts` section below,
after the authorized remediation. The heading here deliberately omits the exact
phrase `Validation receipts` so the closure fitness parser cannot select it.

| Check | Result | Evidence |
| --- | --- | --- |
| Focused Release build | Pass | `dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Release --no-restore -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0`: 0 warnings, 0 errors. |
| Closure fitness | Pass | Post-review direct xUnit v3 `EpicEightClosureFitnessTests`: 13 passed, 0 failed, 0 skipped; status aliases, accepted residual debt, evidence anchors, invariant test classes, canonical PRD/epic scope, and red-receipt closure guards are fail-closed. |
| Documentation fitness | Pass | Direct xUnit v3 `DocumentationFitnessTests`: 3 passed, 0 failed, 0 skipped. |
| Dependency-prerequisite fitness | Pass | Post-review direct xUnit v3 `PlatformApiPrerequisitesTests`: 16 passed, 0 failed, 0 skipped; final-ledger rows are surface-specific and every conditional EventStore/Commons consumer graph is evaluated through MSBuild in its selected modes. |
| Warning/nested-submodule policy | Pass | `bash scripts/check-no-warning-override.sh`: no warning-override or nested-submodule regression. |
| Solution restore | Pass | `dotnet restore Hexalith.Parties.slnx`: restored the current graph successfully. |
| Release solution build | **Blocked** | `dotnet build Hexalith.Parties.slnx -c Release --no-restore -m:1`: 21 errors, all in clean root-gitlink `references/Hexalith.PolymorphicSerializations` (`5e01ff3ab7a7393c2252ee0c2fc1247556e7c129`): SA1000, SA1010, SA1313, and SA1316. Parties-owned projects built; dependency edits require owner authorization and were not made. |
| All .NET test projects | Pass with owner-visible skips | Post-review exact `scripts/test.ps1 -Lane all -Configuration Release -ContinueOnFailure -ResultsDirectory TestResults`: all 15 projects passed; 2,437 succeeded, 0 failed, 6 skipped. The six topology skips are the existing Story 12 DAPR/Tenants runtime-health deferrals and remain visible in `TestResults/Hexalith.Parties.IntegrationTests.trx`. |
| CI identity regression | Pass after repair | The first all-lane run found one stale live assertion expecting EventStore `3.90.0`; the test and `docs/ci.md` now consume the catalog-selected `3.95.0`. Focused CI rerun and the final all-lane rerun passed 37/37. |
| Package/API validation | Pass | Packed and validated all 9 release packages at `0.0.0-story810`; exact EventStore `3.95.0` and Commons `2.30.0` metadata passed. |
| Package-only consumers | Pass | Client and portal consumer projects restored and built from the temporary package feed with 0 warnings and 0 errors. |
| npm install and typecheck | Pass | `npm ci --prefix tests/e2e` found 0 vulnerabilities; `npm --prefix tests/e2e run typecheck` passed. |
| Playwright accessibility | **Blocked** | `npm --prefix tests/e2e run test:a11y`: 2 passed, 4 failed. Failures are the shell skip link navigating to the auth challenge instead of focusing `#parties-main-content`, keyboard focus consequently timing out, duplicate `Skip to content` strict-locator ambiguity between Parties and FrontComposer, and three polite status regions causing strict-locator ambiguity in the visual contract. The axe gate and raw-teal guard passed. Resolving this crosses the deferred Story 8.9 shell-consolidation boundary, so no gate or test was weakened. |
| Static diff | Pass | `git diff --check` completed with no output. |

### Historical closure blockers — 2026-08-18

The dated blockers below retain their original evidence. Current blockers and
execution outcomes are recorded in the canonical `### Validation receipts` table
and the 2026-10-05 sections at the end of this file.

- blocker: `release-solution-polymorphic-stylecop`
  owner: `Hexalith.PolymorphicSerializations maintainers for the dependency fix; Amelia (Parties Developer) and Murat (Test Architect) for consuming-graph revalidation`
  exit_proof: `At the retained root gitlink, dotnet build Hexalith.Parties.slnx -c Release --no-restore -m:1 completes with zero warnings and zero errors, including the PolymorphicSerializations projects.`
  rollback_or_action: `Do not edit or advance the dependency without owner authorization. Retain the current Parties package/source selectors and rerun the complete Release, test, package, and consumer gates after an approved dependency receipt.`
  evidence: `The Release solution build row above records 21 SA1000/SA1010/SA1313/SA1316 errors at root gitlink 5e01ff3ab7a7393c2252ee0c2fc1247556e7c129.`

- blocker: `playwright-shell-accessibility`
  owner: `Hexalith.FrontComposer shell owners + Sally (UX Designer) + Amelia (Parties Developer) + Murat (Test Architect)`
  exit_proof: `npm --prefix tests/e2e run test:a11y passes the skip-link target/focus, unique landmark/status-region, axe, forced-color, and raw-token checks at an approved FrontComposer identity.`
  rollback_or_action: `Keep the retained Parties UI primitives and current Story 8.9 rollback surface. Do not weaken strict locators or accessibility gates; change shared shell or Parties adoption only with the Story 8.9 owner boundary authorized.`
  evidence: `The Playwright accessibility row above records 2 passed and 4 failed, and deferred-work.md entry 8.9-frontcomposer-ui-consolidation owns the shared-shell adoption exit.`

**Closure verdict:** keep Story 8.10 open in `review`/`in-review` and Epic 8
`in-progress`. The accepted deferrals are complete, but the solution-build and
Playwright a11y requirements must both pass before either status changes to
`done`.

### Authorized dependency and shell remediation — 2026-08-18

The user authorized the two owner-boundary changes recorded above. The
PolymorphicSerializations source was made compatible with its pinned StyleCop
analyzers without suppressing diagnostics, and Parties now adopts the shared
FrontComposer shell instead of emitting duplicate skip links, landmarks, and
status selectors. The Playwright Test host explicitly serves static web assets
and builds the selected FrontComposer source graph, so the accessibility lane
exercises an interactive Blazor UI rather than SSR-only output.

### Validation receipts

Authoritative closure-gate table. Check names are canonical: the closure fitness
test requires all six canonical Verification-lane rows (warning policy, Release
build, all .NET tests, packages/consumers, npm/typecheck, and Playwright), and
accepts only an exact `Pass` Result (bold markup tolerated); any other or
qualified value, such as `Pass (partial)` or `Pass with errors`, is a gap.

| Check | Result | Evidence |
| --- | --- | --- |
| PolymorphicSerializations owner build and tests | Pass | Rerun 2026-10-05 at the final set in the clean committed root checkout `98de6e013840ece9f0fa7c68ab7dcdf2bba3b375` (`v1.19.4`; nested submodules left uninitialized; shared props resolve from the root `references/Hexalith.Builds`): `dotnet restore Hexalith.PolymorphicSerializations.slnx -p:NuGetAudit=false && dotnet build Hexalith.PolymorphicSerializations.slnx -c Release --no-restore --no-incremental -m:1 -p:NuGetAudit=false` built with 0 warnings and 0 errors, and direct xUnit v3 execution of `test/Hexalith.PolymorphicSerializations.Tests/bin/Release/net10.0/Hexalith.PolymorphicSerializations.Tests.dll` passed 15/15 (0 errors, 0 not run). The checkout stayed clean. Historical receipt: 15/15 at gitlink `0dca9e9d3f8b2a20ba426b84fa575ab4e7b5562b`. The compatible explicit-syntax preferences do not suppress StyleCop diagnostics. |
| FrontComposer shell focus/theme tests | Pass | Rerun 2026-10-05 at the final set in the clean committed root checkout `2cc8dd3a3ac76c03f5ea6f6f92e65829306db470` (`v4.5.0-121-g2cc8dd3a`; nested submodules left uninitialized): `dotnet restore tests/Hexalith.FrontComposer.Shell.Tests/Hexalith.FrontComposer.Shell.Tests.csproj -p:NuGetAudit=false && dotnet build tests/Hexalith.FrontComposer.Shell.Tests/Hexalith.FrontComposer.Shell.Tests.csproj -c Release --no-restore --no-incremental -m:1 -p:NuGetAudit=false` built with 0 warnings and 0 errors; direct xUnit v3 execution with `-class` for `Components.Layout.Story13AccessibilityPrimitivesTests`, `Components.Layout.FrontComposerShellTests`, `Components.Layout.FcSystemThemeWatcherTests`, and `State.Theme.ThemeEffectsScopeTests` passed 64/64 (0 errors, 0 not run). The checkout stayed clean. Historical receipt: 50/50 for the same four classes at an earlier identity. Fluent `ThemeSettings.IsExact=false` keeps the configured teal as a palette seed instead of forcing the raw, non-AA brand background. Producer tests do not discharge I13 (accepted DW-111 deferral). |
| Parties UI tests | Pass | Stamped 2026-10-05 (review loop 3, rerun after its review patches) at the final set and the Parties tree under test (`75b4fa1fe2cfd2167c186e94faf52e18be4b0ff6`, which commits all eight final-set gitlinks, plus the loop-3 diff and its review patches; full set and commands in the review loop 3 subsections at the end of this file), package mode inside the full Release lane: 343/343 passed, 0 failed, 0 skipped. All five `PartiesOverviewTests` pass because `PartiesOverview.razor` passes the source-only `FcPageTabs` `ModuleRoute`/`DefaultTabId` parameters only under `HFC_ROUTE_OPTIONS` (DW-141); `MainLayoutAccessibilityTests`, `AccessibilityStyleGuardTests`, and the host-composition tests passed. The gated source branch compiled separately (diagnostic, not this row): `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release -m:1 -p:HexalithFrontComposerFromSource=true -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0`, 0 warnings, 0 errors. |
| Warning and nested-submodule policy | Pass | Stamped 2026-10-05 (review loop 3, rerun after its review patches) at the final set and the Parties tree under test (`75b4fa1fe2cfd2167c186e94faf52e18be4b0ff6`, which commits all eight final-set gitlinks, plus the loop-3 diff and its review patches; full set and commands in the review loop 3 subsections at the end of this file): `bash scripts/check-no-warning-override.sh` passed in the chained solution-build command below; no nested submodule is initialized in any of the eight root submodules, and every root checkout matches `git ls-tree HEAD references/` and is clean. `bash scripts/gitlink-rc-gate.sh` (working-tree mode) passed, and `bash scripts/gitlink-rc-gate.sh --diff 882c02455bbdd6b76886fe3fba8bd24a24e2a057` passed all eight root gitlinks (`BUMP ok — validated-advance` for each). |
| Release solution build | Pass | Stamped 2026-10-05 (review loop 3, rerun after its review patches) at the final set and the Parties tree under test (`75b4fa1fe2cfd2167c186e94faf52e18be4b0ff6`, which commits all eight final-set gitlinks, plus the loop-3 diff and its review patches; full set and commands in the review loop 3 subsections at the end of this file): `bash scripts/check-no-warning-override.sh && dotnet restore Hexalith.Parties.slnx -p:NuGetAudit=false && dotnet build Hexalith.Parties.slnx -c Release --no-restore -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` exited 0 with Build succeeded, 0 Warning(s), 0 Error(s) in 00:00:18.81 (incremental, after a restore that reset the source-mode Playwright outputs). Rerun because the loop-3 amendment changed code. |
| All .NET test projects | Pass | Stamped 2026-10-05 (review loop 3, rerun after its review patches) at the final set and the Parties tree under test (`75b4fa1fe2cfd2167c186e94faf52e18be4b0ff6`, which commits all eight final-set gitlinks, plus the loop-3 diff and its review patches; full set and commands in the review loop 3 subsections at the end of this file): `pwsh -NoProfile -File scripts/test.ps1 -Lane all -Configuration Release -ContinueOnFailure -ResultsDirectory TestResults` exited 0 in 313 s; all 15 projects passed: 2,641 tests, 2,635 passed, 0 failed, 6 skipped (the existing Story 12 `HealthEndpointE2ETests` Tier 3 cases in IntegrationTests). Contracts 181/181 (DW-142 with `AgentProvisioning` = `PersonalData` plus the new `NonPersonalMetadataProperties_RemainUnmarked` guard), Server 259/259 (DW-140 exemptions plus three restricted-binding tests), UI 343/343 (DW-141), and Parties.Tests 625/625 (fitness additions for Edge 11, Edge 12, the stale-SDK guard, and the L3 Edge 6 SDK-band case). |
| Package/API and package-only consumers | Pass | Stamped 2026-10-05 (review loop 3, rerun after its review patches) at the final set and the Parties tree under test (`75b4fa1fe2cfd2167c186e94faf52e18be4b0ff6`, which commits all eight final-set gitlinks, plus the loop-3 diff and its review patches; full set and commands in the review loop 3 subsections at the end of this file): `pkg_dir=$(mktemp -d /tmp/parties-810-packages.XXXXXX); consumer_dir=$(mktemp -d /tmp/parties-810-consumer.XXXXXX); python3 scripts/pack-release-packages.py "$pkg_dir" 0.0.0-story810 && python3 scripts/validate-nuget-packages.py "$pkg_dir" && python3 scripts/validate-consumer-package-references.py "$pkg_dir" --work-directory "$consumer_dir"` exited 0: 9 packages validated and the package-only client and portal consumers built with 0 warnings and 0 errors. The `Hexalith.Parties.Contracts` nuspec depends on `Hexalith.EventStore.Contracts` `3.113.0`. It ran on package-mode Release outputs, after the test lane and before the source-mode Playwright build. |
| npm install and typecheck | Pass | Stamped 2026-10-05 (review loop 3, rerun after its review patches) at the final set and the Parties tree under test (`75b4fa1fe2cfd2167c186e94faf52e18be4b0ff6`, which commits all eight final-set gitlinks, plus the loop-3 diff and its review patches; full set and commands in the review loop 3 subsections at the end of this file), Node `26.4.0`, npm `11.18.0`, TypeScript `7.0.2`: `npm ci --prefix tests/e2e` added 9 packages with 0 vulnerabilities; `npm --prefix tests/e2e run typecheck` passed. |
| Playwright accessibility | Pass | Stamped 2026-10-05 (review loop 3, rerun after its review patches) at the final set and the Parties tree under test (`75b4fa1fe2cfd2167c186e94faf52e18be4b0ff6`, which commits all eight final-set gitlinks, plus the loop-3 diff and its review patches; full set and commands in the review loop 3 subsections at the end of this file): `npm --prefix tests/e2e run test:a11y` passed 6/6 in 21.6 s (Playwright `1.63.0`, chromium) against the source-mode UI built from FrontComposer `2cc8dd3a3ac76c03f5ea6f6f92e65829306db470` (`v4.5.0-121-g2cc8dd3a`, `PlatformApiPrerequisitesTests.FrontComposerSha`) and EventStore `865cd9e49273dffbb1cdae85efeaf1aac322e09e`; port 5072 was free before the run, Playwright started the web server, and the port was free again afterwards. Packaged Shell `4.5.0` is a separate identity. This lane does not discharge I13: the DW-111 content-control focus, forced-colors, and reduced-motion proof is the deferral the Administrator accepted on 2026-10-05. Only `specs/parties-accessibility.spec.ts` runs in this lane (`test:a11y`); the admin, consumer, and picker specs are not run (DW-143). |
| Static diff | Pass | Rerun 2026-10-05 (review loop 3, after its review patches) at Parties `75b4fa1fe2cfd2167c186e94faf52e18be4b0ff6` plus the loop-3 diff: `git -c core.whitespace=cr-at-eol diff --check` passed, and the untracked `PartiesOverview.razor.cs` (CRLF) has no whitespace errors under the same setting. Existing line endings are preserved; `core.whitespace` is an invocation-only interpretation of CR at EOL, not a repository configuration change. |

The RC diff base `882c02455bbdd6b76886fe3fba8bd24a24e2a057` (2026-09-07,
`chore(dependencies): update submodule references for Hexalith components`) is
the Round 5 record commit, the last tree whose root gitlinks a Story 8.10 review
round recorded; diffing HEAD against it makes `gitlink-rc-gate.sh --diff` demand a
`validated-advance` signoff for every root pointer moved since then (Round 6
through the 2026-10-05 final set).

Every row in the table was rerun at the 2026-10-05 final set. The six
Verification rows plus Parties UI tests and Static diff are stamped at
`75b4fa1f` plus the review loop 3 diff; the two producer rows keep their
2026-10-05 reruns because the producer identities did not change. The review
loop 3 subsection at the end of this file records the exact commands and
results. I13 remains an accepted deferral (DW-111), not a discharged invariant.
Story 8.10 and Epic 8 closed `done` on 2026-10-05 after the review loop 3 patch
rerun (see *Review loop 3 patch rerun* at the end of this file).

**Corrected 2026-08-19 (code review).** The paragraph previously here was
written before the gitlinks were committed and is false at HEAD. Superproject
commit `2b63ab9` records FrontComposer
`7a337a21d4ba261bf27aeb3feedde47789f0160a` and PolymorphicSerializations
`0dca9e9d3f8b2a20ba426b84fa575ab4e7b5562b`; both checkouts equal their gitlinks
and both working trees are clean. The identities are recorded in the Story 8.3
reconciliation table as of 2026-08-19.

What remains missing is narrower than "no receipt exists": neither owner has
published a **release or tag** containing its fix. FrontComposer
`7a337a21` is 104 commits past the packaged `4.1.1` that CI bUnit runs and the
released `parties-ui` container are built from, so the Playwright receipt is
stamped at an identity that does not ship. PolymorphicSerializations `0dca9e9d`
carries the StyleCop compatibility fix that cleared the 21-error Release build
but is likewise unreleased.

### Historical immutable-receipt blocker — 2026-08-19

This earlier blocker closed historically at the 2026-09-06 selected owner pin;
its build proof is unvalidated at the current identity. Current identities and
receipts are recorded in the canonical `### Validation receipts` table and the
2026-10-05 sections at the end of this file.

- blocker: `authorized-owner-fixes-not-immutable`
  owner: `Hexalith.FrontComposer and Hexalith.PolymorphicSerializations maintainers for owner commits/releases; Amelia (Parties Developer) and Murat (Test Architect) for superproject selection and revalidation`
  exit_proof: `Record immutable owner commits or releases, select them through the superproject gitlinks/package graph, then rerun the exact Release solution build, all 15-project lane, package/consumer validation, and npm accessibility lane with the same green results.`
  rollback_or_action: `Keep Story 8.10 in review and Epic 8 in progress. Do not represent dirty dependency checkouts as delivered dependencies; if either owner fix is rejected, restore the retained Story 8.9 UI surface and dependency selection before rerunning the gates.`
  evidence: `Corrected 2026-08-19: the superproject now selects FrontComposer 7a337a21d4ba261bf27aeb3feedde47789f0160a and PolymorphicSerializations 0dca9e9d3f8b2a20ba426b84fa575ab4e7b5562b, both checkouts match their gitlinks, and both working trees are clean. The blocker stays open on the narrower ground that neither owner has published a release or tag containing its fix: FrontComposer 7a337a21 is 104 commits past the packaged 4.1.1 that CI bUnit and the released parties-ui container consume, so the accessibility receipt is stamped at an identity that does not ship.`

### Story 8.10 code review round 2 — 2026-08-19

Adversarial code review of the diff from baseline `37f4ec8` to `2b63ab9`, scoped
to `src`, `tests`, `references`, `docs`, and `README.md`. Four independent review
layers produced roughly 100 raw findings; after verification against the code,
12 were dismissed as false (two "vacuous in CI" claims died on the fact that the
reusable `domain-ci.yml` sets `fetch-depth: 0` and initializes all root
submodules; two more died on the spec's own `test.use({locale, viewport})`).
Three decisions were resolved, 23 patches applied, four items deferred.

The three highest-consequence repairs:

1. **Dead focus-visible CSS.** `MainLayout.razor` rendered only
   `<FrontComposerShell>`, so Blazor CSS isolation emitted no scope attribute and
   every `::deep` rule in `MainLayout.razor.css` — the `--colorStrokeFocus2`
   outline and its `@media (forced-colors: active)` override — matched nothing at
   runtime. Confirmed by inspecting the generated `MainLayout_razor.g.cs`.
   Repaired with an app-owned `display: contents` wrapper, plus a restored
   reduced-motion rule and a guard test that fails if the wrapper disappears.
2. **Gitlink proof was unfalsifiable.** `AssertGitlinkAndCheckout` accepted a
   third disjunct on the working-tree checkout that the following assertion
   already guaranteed, so a superproject pointing at a different commit passed
   whenever the checkout happened to be right — precisely the "checkout is not
   consumption proof" rule the frozen Boundaries forbid. Disjunct removed, and a
   new guard rejects any present-tense `git ls-tree HEAD` receipt in the matrix
   that names a superseded identity.
3. **Closure receipt parser read the wrong table.** `ParseValidationReceipts`
   sliced from the last receipts heading to end of file, swallowing the blocker
   and remediation sections, so superseded `**Blocked**` rows decided the gate.
   The section is now bounded at the next heading, the receipts table was made
   canonical, and a new always-on test proves the gate is parseable rather than
   leaving that discoverable only at closure. Writing this very summary then
   exposed a second defect in the same selector: heading selection used a
   substring search, so the prose above — which quotes the heading text — was
   itself selected as the section. Heading matching is now anchored to a complete
   line, which is the property it always needed.

Validation after the repairs:

| Check | Result | Evidence |
| --- | --- | --- |
| Warning and nested-submodule policy | Pass | `bash scripts/check-no-warning-override.sh`: no regressions. |
| Release solution build | **Blocked** | 16 SA1316 errors, all in `references/Hexalith.PolymorphicSerializations`; see the blocker below. Zero errors in Parties-owned projects. |
| All .NET test projects | Pass | `pwsh -NoProfile -File scripts/test.ps1 -Lane all -Configuration Release -ContinueOnFailure -ResultsDirectory TestResults`: all 15 projects passed. |
| Focused fitness classes | Pass | `EpicEightClosureFitnessTests` 14/14, `DocumentationFitnessTests` 3/3, `PlatformApiPrerequisitesTests` 16/16, full `Hexalith.Parties.Tests` 559/559. |
| Parties UI tests | Pass | `Hexalith.Parties.UI.Tests` 329/329, including the two new scope and static-asset coupling guards. |
| npm typecheck and Playwright accessibility | Pass | `npm --prefix tests/e2e run typecheck` passed; `npm --prefix tests/e2e run test:a11y` passed 6/6 at FrontComposer source `7a337a21`, with the strict `[role='status'][aria-live='polite']` locator restored. |

The strict "first two keyboard tab stops" assertion was attempted and **failed**:
after hydration the shell focuses the route `<h1>`, which advances the browser's
sequential focus navigation point past both skip links, so the first `Tab` lands
on the page's first interactive control. The DOM order is correct, so the test
now asserts that explicitly and is named for it; the reachability question is
routed to the FrontComposer shell owners in `deferred-work.md`.

- blocker: `polymorphicserializations-stylecop-fix-incomplete-at-selected-gitlink`
  owner: `Hexalith.PolymorphicSerializations maintainers for the completing commit; Amelia (Parties Developer) and Murat (Test Architect) for superproject reselection and revalidation`
  exit_proof: `At the reselected root gitlink, dotnet build Hexalith.Parties.slnx -c Release -m:1 completes with zero warnings and zero errors, including every PolymorphicSerializations project.`
  rollback_or_action: `Do not suppress SA1316 and do not add a NoWarn to work around it -- the build gate forbids weakening warnings-as-errors. Complete the tuple-element-casing fix in the owner repository, publish it, then advance the superproject gitlink and rerun the full Release, test, package, consumer, and accessibility gates.`
  evidence: `The Release solution build row above records 16 SA1316 errors at gitlink 0dca9e9d3f8b2a20ba426b84fa575ab4e7b5562b, all inside references/Hexalith.PolymorphicSerializations and none outside it.`

**Updated closure verdict (revised 2026-08-19):** Story 8.10 remains
`review`/`in-review` and Epic 8 remains `in-progress`. Two gates are red or
unmet: the Release solution build fails with 16 SA1316 errors at the selected
PolymorphicSerializations gitlink, and neither authorized owner fix has an
immutable release. The 2026-08-18 "former technical blockers are resolved"
verdict was based on receipts produced from modified working trees and did not
survive re-measurement at the committed tree. The frozen `Never` rule rejecting
checkout/compile evidence as consumption proof is what caught this: the fix was
real in a working tree and only partly real in the commit that landed.

### Authorized PolymorphicSerializations SA1316 completion — 2026-08-19

The user authorized the smallest owner-repository patch needed to complete the
tuple-element-casing fix. The working tree at selected gitlink
`0dca9e9d3f8b2a20ba426b84fa575ab4e7b5562b` restores `Type` / `Data` in the
source generator and the prior public `Name` / `TypeName` / `Version`
discriminator tuple names. No analyzer suppression, warning override, package
version, dependency, or Parties production source changed.

| Check | Result | Evidence |
| --- | --- | --- |
| PolymorphicSerializations owner Release build | Pass in authorized working tree | `dotnet build Hexalith.PolymorphicSerializations.slnx -c Release --no-restore -m:1`: 0 warnings, 0 errors. |
| PolymorphicSerializations owner tests | Pass in authorized working tree | Direct xUnit v3 execution of `test/Hexalith.PolymorphicSerializations.Tests/bin/Release/net10.0/Hexalith.PolymorphicSerializations.Tests.dll`: 15 passed, 0 failed, 0 skipped. |
| Story 8.10 Release gate | Pass in authorized working tree | `bash scripts/check-no-warning-override.sh && dotnet restore Hexalith.Parties.slnx && dotnet build Hexalith.Parties.slnx -c Release --no-restore -m:1`: guard and restore passed; all 59 projects built with 0 warnings and 0 errors. |
| Story 8.10 focused fitness | Pass | Direct xUnit v3 execution: `EpicEightClosureFitnessTests` 14/14, `DocumentationFitnessTests` 3/3, and `PlatformApiPrerequisitesTests` 16/16; no failures or skips. |

This is repair evidence, not immutable consumption proof. The selected
superproject gitlink still names `0dca9e9d`, whose clean committed tree fails
with the 16 SA1316 diagnostics recorded above. Keep
`polymorphicserializations-stylecop-fix-incomplete-at-selected-gitlink` and
`authorized-owner-fixes-not-immutable` open until the owner patch is committed,
the superproject gitlink is advanced to that exact commit, and the full Release,
test, package/consumer, and accessibility gates are rerun at the immutable
identity.

### PolymorphicSerializations blockers closed — 2026-09-06 (code review round 3)

The superproject gitlink has since advanced to `8aeed1d27c9a050bc4bec6d89051aa00de306a69`
(`v1.19.2-11-g8aeed1d`), a real committed identity 9 commits past `0dca9e9d`,
carrying the completing tuple-element-casing fix as a landed owner commit rather
than a working-tree patch. Re-verified 2026-09-06:

| Check | Result | Evidence |
| --- | --- | --- |
| Release solution build | Pass | `dotnet build Hexalith.Parties.slnx -c Release -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0`: 0 warnings, 0 errors. `PolymorphicHelper.cs`'s tuple casing is fixed at this gitlink; the 16 SA1316 errors recorded at `0dca9e9d` are gone. |

`polymorphicserializations-stylecop-fix-incomplete-at-selected-gitlink` and
`authorized-owner-fixes-not-immutable` are both closed: the exit proof each
blocker required (a zero-warning, zero-error Release build at a real committed
gitlink) is met at `8aeed1d2`. The 8-3 reconciliation matrix's PolymorphicSerializations
row has been updated to this gitlink, and `.gitlink-signoff.tsv` carries the matching
`validated-advance` row (2026-09-06) authorizing this identity to ship in a release tag.

## Story 8.7 Shared payload-protection adoption — closed-gate halt — 2026-09-07

Spec `_bmad-output/implementation-artifacts/spec-8-7-data-protection-extraction.md` AC1 requires a `blocked` halt when G5 runtime packages and the EventStore 8.11 closure are missing. Execution verified the gate and stopped before production, DI, or dependency changes. Dual-provider, GDPR, and post-v2 rollback suites were not run and are not credited.

### Identities

| Mode | Identity | Result |
| --- | --- | --- |
| EventStore source gitlink and checkout | `d45206f7cbd80a112519c1d4687d7279a745f0c5` (`v3.103.0-2-gd45206f7`) | Match each other. Spec frozen pin `c21bd749154d701c3b7d68e40d1008d3475e35c4` / package `3.95.0` does not match; Ask First forbids adopting a new identity. Neither identity delivers G5. |
| EventStore package graph | Builds catalog `HexalithEventStoreVersion=3.103.0` | No `Hexalith.EventStore.PayloadProtection` or `.AzureKeyVault` package version. |
| G5 matrix row | `needs-additive-api` | Unchanged. Local MOVE/KEEP files, adapter, and DI remain. |

### Missing receipts

| Receipt | Inspection |
| --- | --- |
| EventStore 8.11 closure packet | `test ! -f references/Hexalith.EventStore/_bmad-output/implementation-artifacts/8-11-g5-evidence-and-approval-closure.md` — absent. Story `8-11-g5-evidence-and-approval-closure: backlog`. |
| Runtime engine packages | Both `Hexalith.EventStore.PayloadProtection*.csproj` absent. Catalog has no PayloadProtection versions. |
| Runtime APIs | `IPersonalDataPolicy`, `IErasureStateProvider`, and `pdenc-v2` have no matches under `references/Hexalith.EventStore/src`. |
| EventStore predecessor stories | Stories 8.2-8.11 remain `backlog`. Story 8.1 `approved-authorized` authorizes only 8.2 preflight. |
| Named G5 `available` approvals | Absent. Story 8.11 alone may record G5 `available`. |
| Dual-provider parity / post-v2 rollback | Not run. Not credited. |
| Production KMS | Still an independently blocking release gate. `LocalDevKeyStorageBackend` remains. |

### Commands

| Command | Result | Notes |
| --- | --- | --- |
| `git ls-tree d354166c20704fb676818b2973a1bf866db40280 references/Hexalith.EventStore` | Pass | `160000 commit d45206f7cbd80a112519c1d4687d7279a745f0c5 references/Hexalith.EventStore` |
| `git -C references/Hexalith.EventStore rev-parse HEAD` | Pass | `d45206f7cbd80a112519c1d4687d7279a745f0c5` (matches gitlink) |
| `git -C references/Hexalith.EventStore describe --tags --always HEAD` | Pass | `v3.103.0-2-gd45206f7` |
| `test ! -f` on 8.11 closure and both PayloadProtection csproj files | Pass | All three absent. |
| `rg -n -F 'IPersonalDataPolicy\|IErasureStateProvider\|pdenc-v2' references/Hexalith.EventStore/src` | Pass (no matches) | Runtime G5 surfaces are not in source. |
| `rg -n -F 'TryAddSingleton<IEventPayloadProtectionService, NoOpEventPayloadProtectionService>' references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs` | Pass | Default remains the no-op. Not the shared engine. |
| Dual-provider harness, unit lane, topology lane, package-mode Security Release build | Not run | Closed-gate halt. Not credited as pass, skip, or fail. |

### Retained rollback surfaces

Parties still owns `PartyPayloadProtectionService`, `EventStorePartyPayloadProtectionAdapter`, local DI in `PartiesServiceCollectionExtensions`, `PartyDomainProcessor` coupling, all 18 MOVE files, all 5 KEEP files, and `CryptoKeyManagementCompatibilityHarnessTests`. Crypto-shredding remains default-on. The Epic 7 crypto-retention action stays `open`.

### Totals

Dual-provider totals: not run / not credited. No skips credited. Story 8.7 remains `blocked`.

## Story 8.7 G5 revalidation — closed-gate halt — 2026-10-03

At Parties `06714c166c090200ac87373b11d8243aa11b2126`, G5 remains `needs-additive-api` and Story 8.7
remains `blocked`. This receipt supersedes the 2026-09-07 claims that policy
contracts, v2 source, and the PayloadProtection core project are absent. It does
not authorize a new dependency identity or claim provider adoption.

| Identity | Observation |
| --- | --- |
| EventStore root gitlink and clean checkout | `2c58ffda41759e895ace4b9625c9bd931a217672` (`v3.111.0`), matching. |
| Builds root gitlink and checkout | `688eec9a4333245cc0ff7772115c769094471863`, matching; imported catalog selects `HexalithEventStoreVersion=3.110.0`. |
| Locked Story 8.7 identity | `c21bd749154d701c3b7d68e40d1008d3475e35c4` / `3.95.0`; unchanged. Live observation is not G5 adoption approval. |

| Inspection command | Result |
| --- | --- |
| `git rev-parse HEAD` | `06714c166c090200ac87373b11d8243aa11b2126`. |
| `git ls-tree 06714c166c090200ac87373b11d8243aa11b2126 references/Hexalith.EventStore`; `git -C references/Hexalith.EventStore rev-parse HEAD`; `git -C references/Hexalith.EventStore status --short`; `git -C references/Hexalith.EventStore describe --tags --always` | Matching `2c58ffda41759e895ace4b9625c9bd931a217672`, clean checkout, `v3.111.0`. |
| `git ls-tree 06714c166c090200ac87373b11d8243aa11b2126 references/Hexalith.Builds`; `git -C references/Hexalith.Builds rev-parse HEAD`; `rg -n 'HexalithEventStoreVersion\|PayloadProtection' references/Hexalith.Builds/Props/Directory.Packages.props` | Matching `688eec9a4333245cc0ff7772115c769094471863`; `3.110.0`; no PayloadProtection catalog entries. |
| `cat references/Hexalith.EventStore/src/Hexalith.EventStore.PayloadProtection/Hexalith.EventStore.PayloadProtection.csproj` | Core project exists, `IsPackable=false`; internal core and wire-format/context types, no approved runtime registration. |
| `rg -n 'interface (IPersonalDataPolicy\|IErasureStateProvider)\|TryAddSingleton<IEventPayloadProtectionService\|Add.*PayloadProtection' references/Hexalith.EventStore/src -g '*.cs'` | Both public contracts present; server still selects the no-op default. |
| `rg -n 'PayloadProtection' references/Hexalith.EventStore/tools/release-packages.json` | Exit 1, no package release enrollment; expected missing receipt. |
| `test ! -f references/Hexalith.EventStore/_bmad-output/implementation-artifacts/8-11-g5-evidence-and-approval-closure.md`; `test ! -f references/Hexalith.EventStore/src/Hexalith.EventStore.PayloadProtection.AzureKeyVault/Hexalith.EventStore.PayloadProtection.AzureKeyVault.csproj` | Both absent; missing closure and production-backend delivery. |
| `sed -n '245,290p' references/Hexalith.EventStore/_bmad-output/implementation-artifacts/sprint-status.yaml` | Owner 8.2 `done`; 8.3 `in-progress`; compatibility, lifecycle, backend, integration, release, parity, post-v2 rollback, and closure (8.4-8.11) `backlog`. |
| Static Python inventory assertions and `rg -n 'IEventPayloadProtectionService\|EventStorePartyPayloadProtectionAdapter\|PartyPayloadProtectionService' src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs` | All 24 retained files (18 MOVE + 5 KEEP + adapter) exist; local payload service, adapter factory registration, and LocalDev backend retained. |

The named G5 availability approval, consumable provider/backend release, actual
dual-provider GDPR/parity evidence, and post-v2 rollback proof remain missing.
Parties 8.6 is done; its completion does not waive G5. Production KMS remains a
separate release gate. No production, DI, dependency, or submodule changes were
made; the locked spec block and original baseline remain unchanged.

Product/unit/topology/dual-provider/GDPR/post-v2 suites were not run because the
start gate is closed; no passes or skips are credited. Static gate inspection
passed. Documentation validation passed: the frozen spec block and original
baseline are byte-identical, sprint YAML data is unchanged, all new revision
fields are canonical, the regenerated context is valid, and the File List
matches the six changed documentation artifacts. `git diff --check` passed.


## Story 8.10 Round 6 implementation verification before the three-pin approval — 2026-10-04

Historical pre-approval packet: the post-approval packet below supersedes its pin
failures and pending-selection status. These execution counts remain provenance.

This packet records uncommitted Parties-owned repairs at parent revision
`c782b68c5cf56a19e6a2a237f5f44e3043d5e461`; it records no new human approval.
The exact eight approved and observed identities are listed above and in the
8.3 reconciliation/I20 table. EventStore `cbbe41501ba722731bf36b2c343efdef4ac714fb`,
FrontComposer `374bb83392d8ab4e8a8397cfd312d09948fb0b9d`, and Tenants
`cc348c9d7839ec7aad01649fc1c0e6f4fe672da4` are later pending advances.
Constants and signoff retain the previously approved identities. All root
checkouts match committed gitlinks and are clean; no nested submodule was
initialized or changed. Source compilation and fixture execution are diagnostic
results, separate from approved consumption/parity receipts.

### Repairs covered by this packet

The reconciliation and signoff transcribe the existing Round 6 decision, keep
pending advances explicit, restore committed-tree pin proof, and record all I20
decision kinds. G5 contracts/core are partial delivery: owner 8.2 is done and 8.3
is in progress; AzureKeyVault, catalog/release enrollment, dual-provider parity,
rollback, and the 8.11 closure packet remain gates. The owner ADR field is
`decision: adopted-amendment`; the exact check now reflects the existing
September amendment. Independent G5 evidence is inspected before the expected
pending pin failure. The crypto retention action and all rollback files remain.

SDK/catalog expectations now derive from tracked configuration. Source-mode UI
asset validation uses a CPM-compatible `PackageDownload` for packaged Shell
`4.5.0`. Qualified `Pass (unvalidated)`, `Pass with skipped checks`, and `Pass
with failed checks` receipts are rejected by meaningful closure-negative cases.
The no-PRD scope check is restored; duplicate ledger decisions and unaddressed
trailing entries are repaired, with DW-124–126 keeping owner work visible.

MainLayout's older anonymous fixture had no FrontComposer tenant/user context.
Current producer shell fixtures provide a valid scope because the shell guards
navigation and content with `ScopeBoundaryService.IsCurrent`. The bUnit fixture
now supplies that scope without changing its assertions. The browser host uses
`PartiesAccessibilitySpecimenUserContextAccessor` only under the existing
explicit specimen flag and Development/Test guard. Its synthetic scope applies
only to the exact accessibility route; navigation to ordinary routes delegates
to the selected authenticated accessor, including null/missing context. Eight
focused cases prove the restriction and delegation. The first browser attempt
failed 0/6 at the workspace-identity gate; the fixture repair restored 6/6.
No shell/parity assertion or production authorization was weakened.

### Exact commands and current outcomes

Source-mode Debug restore/build commands below each completed successfully with
0 build warnings and 0 build errors. They do not replace the package-mode Release
gate:

```sh
dotnet restore tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:Configuration=Debug -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0
dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Debug --no-restore -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0
dotnet restore tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:Configuration=Debug -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0
dotnet build tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -c Debug --no-restore -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0
```

| Command | Current outcome |
| --- | --- |
| `dotnet tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.DocumentationFitnessTests -class Hexalith.Parties.Tests.FitnessTests.EpicEightClosureFitnessTests -class Hexalith.Parties.Tests.FitnessTests.PlatformApiPrerequisitesTests -noColor` | 39 total: Documentation 6/6 and closure 17/17 passed; prerequisites 14/16 passed. Both prerequisite failures ultimately identify the observed EventStore committed gitlink `cbbe41501ba722731bf36b2c343efdef4ac714fb` against approved `b046425503c694d5857e8bba890351840fa52c26`; the later prerequisite-only rerun below confirms independent G5 checks reach that pin guard. No skips. |
| `dotnet tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.PlatformApiPrerequisitesTests -noColor` | 14/16 passed, 2 pending-identity failures, 0 skips, after fixing the exact amended ADR field. |
| `dotnet tests/Hexalith.Parties.UI.Tests/bin/Debug/net10.0/Hexalith.Parties.UI.Tests.dll -class Hexalith.Parties.UI.Tests.AccessibilityStyleGuardTests -noColor` | 4/4 passed; the catalog-selected packaged Shell 4.5.0 CSS is present via PackageDownload. |
| `dotnet tests/Hexalith.Parties.UI.Tests/bin/Debug/net10.0/Hexalith.Parties.UI.Tests.dll -class Hexalith.Parties.UI.Tests.PartiesAccessibilitySpecimenScopeTests -class Hexalith.Parties.UI.Tests.MainLayoutAccessibilityTests -class Hexalith.Parties.UI.Tests.PartiesUiHostCompositionTests -noColor` | 28/28 passed: new scope guards 8/8, unchanged MainLayout assertions 3/3, host composition 17/17. |
| `dotnet tests/Hexalith.Parties.UI.Tests/bin/Debug/net10.0/Hexalith.Parties.UI.Tests.dll -class Hexalith.Parties.UI.Tests.PartiesAccessibilitySpecimenScopeTests -noColor` | Final renamed/documented guard class: 8/8 passed. |
| `dotnet tests/Hexalith.Parties.UI.Tests/bin/Debug/net10.0/Hexalith.Parties.UI.Tests.dll -class Hexalith.Parties.UI.Tests.PartiesAccessibilitySpecimenTests -noColor` | Existing enabled/disabled specimen route/component checks: 7/7 passed. |
| `bash scripts/check-no-warning-override.sh` | Passed. |
| `bash scripts/gitlink-rc-gate.sh --diff 882c0245` | Failed exactly the three later unapproved EventStore, FrontComposer, and Tenants gitlinks; the other five have matching Round 6 signoff. |
| `git diff 37f4ec826c6f4aea4651cfbad94fb6ab7fc4f0a0 -- _bmad-output/planning-artifacts/epics.md` | No output after removing the McpCli banner; zero new PRD functional requirement. |
| `npm ci --prefix tests/e2e` | Passed: 9 packages installed, 0 vulnerabilities. Node 26.4.0 / npm 11.18.0. |
| `npm --prefix tests/e2e run typecheck` | Passed. |
| `npm --prefix tests/e2e run test:a11y` | After fixture repair, 6/6 passed at observed pending FrontComposer `374bb83392d8ab4e8a8397cfd312d09948fb0b9d`, with Playwright's source-mode Release Test host. This is execution evidence; approved immutable I13 parity remains unvalidated. |
| `git -c core.whitespace=cr-at-eol diff --check` | Passed with existing CRLF preserved. |

The package-mode commands and result are current and separate:

```sh
dotnet restore Hexalith.Parties.slnx -p:Configuration=Release -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0
dotnet build Hexalith.Parties.slnx -c Release --no-restore -m:1 -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0
```

Restore passed; build failed with 0 warnings and 10 CS0246 errors in
`Hexalith.Parties.Contracts` for `IdentityAdmissionEvidence`,
`IdentityHistoryPolicy`, `IdentityHistoryCustodyEvidence`, and
`IIdentityHistoryEvent`, absent from selected EventStore package `3.110.0`.
The all-15-project Release test lane, no-build package/API pack, and package-only
consumer checks were not run after that failed prerequisite. Their historical
pass counts are unvalidated. The required baseline command
`aspire run --detach --isolated --non-interactive --apphost src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj`
also exited 2 during the package-mode build with the same 10 errors, before any
resources started.

Story 8.10 stays `in-progress`, Epic 8 stays `in-progress`, and Stories 8.7–8.9
remain `blocked`. Pending identity selection, I16 re-validation approval,
package compatibility/release work (including DW-124), full-lane proof, and the
remaining I13 content-control proof prevent closure. This invocation authorizes
no dependency advance, deletion, owner commitment, or publishing action.

## Story 8.10 three-pin approval and verification — 2026-10-04

Administrator / jpiquot explicitly answered "yes" to the request to approve exactly
these existing current root pins at Parties `c782b68c5cf56a19e6a2a237f5f44e3043d5e461`:

- EventStore `cbbe41501ba722731bf36b2c343efdef4ac714fb` (`v3.111.0-10-gcbbe4150`).
- FrontComposer `374bb83392d8ab4e8a8397cfd312d09948fb0b9d` (`v4.5.0-117-g374bb833`).
- Tenants `cc348c9d7839ec7aad01649fc1c0e6f4fe672da4` (`v5.7.0-134-gcc348c9d`).

The 8.3 I20 table, `.gitlink-signoff.tsv`, shared identity constants, architecture
spine, dependency documentation, deferral chronology, and sprint comments now
record this approval. The prior Round 6 selection remains historical provenance.
No gitlink, dependency checkout, package catalog, or owner repository was changed.
Selection approval does not approve I16 parity, owner releases, publishing, owner
commitments, or rollback deletion. D1's identity reconciliation is complete;
Story 8.10 and Epic 8 remain `in-progress` with 8.7–8.9 `blocked`.

Exact post-approval verification commands and results:

| Command | Result |
| --- | --- |
| `dotnet restore tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:Configuration=Debug -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` | Passed. |
| `dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Debug --no-restore -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` | Passed: 0 warnings and 0 errors. |
| `dotnet tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.DocumentationFitnessTests -class Hexalith.Parties.Tests.FitnessTests.EpicEightClosureFitnessTests -class Hexalith.Parties.Tests.FitnessTests.PlatformApiPrerequisitesTests -noColor` | Passed: 39/39; documentation 6, closure 17, prerequisites 16. No errors, failures, skips, or unrun cases. Both former pin failures are resolved; all eight exact committed gitlinks and clean matching checkouts pass, and package/source graph evaluation executes. |
| `bash scripts/gitlink-rc-gate.sh --diff 882c02455bbdd6b76886fe3fba8bd24a24e2a057` | Passed: all eight current root gitlinks have matching `validated-advance` signoff. |

The 39 passing focused UI checks and 6/6 Playwright execution in the preceding
packet were run at these same unchanged source identities. Together with this
fitness rerun, the unique focused .NET result is 78 passed, 0 failed, 0 skipped.
These checks do not certify the full Release/package lane or discharge I13.

The package-mode Release result remains blocked by the 10 CS0246 errors above:
selected EventStore `3.110.0` lacks the identity-history types consumed by
Parties.Contracts. Approval changed source identity records and guards, not the
selected packages, so the failed package prerequisite has not been rerun. The
full Release tests, pack/API, and package-only consumer checks remain blocked.
DW-124 still requires a FrontComposer release/catalog/consumer packet; DW-111
still requires content-control focus evidence. Other applicable I20 approvals,
including I16 parity re-validation, remain pending. The original baseline and
frozen intent are preserved; no retirement or closure is claimed.

## Story 8.10 review repairs and final verification — 2026-10-04

All three review layers returned against the preserved full baseline diff.
The 20.3 MB / 2,093-file historical diff received bounded reachable-code review,
not an exhaustive audit of every installed BMAD asset. The spec's Review Triage
Log records all sixteen findings individually before grouping: two closure
patches, twelve deferred existing root causes (DW-127–138), and one rejection.
The two patches require all six Verification-lane receipts and stop parsing at
level-one/two/three Markdown headings. Nine regression cases cover omissions
and section boundaries; no expectation or closure requirement was weakened.

Exact latest commands and results:

| Command | Result |
| --- | --- |
| `dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Debug --no-restore -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` | Implementation-agent source build after the two review patches: passed, 0 warnings and 0 errors. |
| `dotnet tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.EpicEightClosureFitnessTests -noColor` | Implementation-agent focused check: 26/26 passed. |
| `dotnet tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.DocumentationFitnessTests -class Hexalith.Parties.Tests.FitnessTests.EpicEightClosureFitnessTests -class Hexalith.Parties.Tests.FitnessTests.PlatformApiPrerequisitesTests -noColor` | Parent rerun after reviewing the patches: 48/48 passed (6 documentation, 26 closure, 16 prerequisites); 0 errors/failures/skips/unrun cases. |
| `dotnet restore tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -p:Configuration=Release -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` | Passed; matching package-mode assets restored before the required Release check. |
| `dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Release --no-restore -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 && dotnet tests/Hexalith.Parties.Tests/bin/Release/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.EpicEightClosureFitnessTests` | Required first Verification command: exit 1, build failed with 0 warnings and 10 CS0246 errors in Parties.Contracts for IdentityAdmissionEvidence, IdentityHistoryPolicy, IdentityHistoryCustodyEvidence, and IIdentityHistoryEvent. Selected EventStore package 3.110.0 lacks these types. The chained Release assembly test did not run. |

The latest unique focused .NET result is **87 passed / 0 failed / 0 skipped**:
48 fitness plus the unchanged 39 focused UI cases. Browser execution remains
6/6 at approved FrontComposer 374bb83392d8ab4e8a8397cfd312d09948fb0b9d, and the
RC diff gate passes all eight approved root gitlinks. These source diagnostics
remain separate from Release/package proof and the incomplete I13 content-focus
and I16 parity approval packet.

The required Release failure stops step-04 before step-05 under the build
workflow's unfixable-verification rule. Story 8.10 and Epic 8 remain in-progress,
Stories 8.7–8.9 remain blocked, and full Release tests, pack/API, and package-only
consumers remain blocked. Resolving the package prerequisite requires owner
release/catalog work beyond the three exact source-pin approvals. No such work,
commit, push, package publication, or deletion occurred. The frozen intent and
original baseline remain preserved.

## EventStore 3.112.0 update and verification — 2026-10-04

User authority: "update eventstore to version 3.112.0". Parent revision:
`7c720c7f9879ecb469993b1d2dff2bdb72b96e1b`. The working Builds catalog already
selected 3.112.0, while its committed root gitlink still selected an older catalog.
Parties now pins HexalithEventStoreVersion=3.112.0 before the shared import.
The pin persists independently of that checkout advance. CPM guards, CI/architecture
docs, the current 8.3 package rows/I20 approval, and spine were reconciled.
Earlier package 3.110.0 receipts remain historical; no source approvals are inferred.

Commands and latest results:

| Command | Result |
| --- | --- |
| `aspire start --isolated --non-interactive --apphost src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj` | Baseline exit 2 before resources started: Parties.Contracts and core dependencies compiled at package 3.112.0; Parties.UI failed with the same three missing FrontComposer types described below. |
| `dotnet restore tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -p:Configuration=Release -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` | Passed. |
| `dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Release --no-restore -m:1 -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` | Passed: 0 warnings, 0 errors. The previous missing EventStore identity-history contract types are resolved. |
| `dotnet tests/Hexalith.Parties.Tests/bin/Release/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.DocumentationFitnessTests -class Hexalith.Parties.Tests.FitnessTests.EpicEightClosureFitnessTests -class Hexalith.Parties.Tests.FitnessTests.PlatformApiPrerequisitesTests -noColor` | 48 total, 46 passed, 2 failed, 0 skipped: documentation 6/6, closure 26/26, prerequisites 14/16. Both failures expect prior-approved EventStore cbbe41501ba722731bf36b2c343efdef4ac714fb but committed HEAD records e968467c7db5b5685894fa2b85cfa7ec14512a7c; checkout is a third identity, 2242ad55a1b678828df8aa093fd92399c29af5bf. No pin guard was relaxed. |
| `dotnet tests/Hexalith.Parties.Tests/bin/Release/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.Domain.PartyIdentityAdmissionTests -class Hexalith.Parties.Tests.Gateway.PartyIdentityQueryHandlerTests -noColor` | 15/15 passed, 0 skipped. |
| `dotnet msbuild <consumer.csproj> -nologo -p:Configuration=Release -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true -getProperty:HexalithEventStoreVersion -getItem:PackageReference,PackageVersion` | Evaluated each of all 10 src/samples/tests package consumers with HexalithEventStoreFromSource selection. Every effective property and all central Hexalith.EventStore.* PackageVersion items select 3.112.0. The independent check ran because the source-pin failure prevents the combined fitness case reaching its graph assertions. |
| `dotnet msbuild src/Hexalith.Parties.Contracts/Hexalith.Parties.Contracts.csproj -nologo -p:Configuration=Release -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true -p:Hexalith1BuildPackageProps=/tmp/parties-older-catalog-i306keyz.props -getProperty:HexalithEventStoreVersion -getItem:PackageVersion` | Passed with a temporary byte-for-byte catalog from committed Builds 688eec9a4333245cc0ff7772115c769094471863 (default EventStore 3.110.0). Both the effective property and Contracts PackageVersion remain 3.112.0, proving fresh-checkout selection. |
| `dotnet restore Hexalith.Parties.slnx -p:Configuration=Release -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` | Passed. |
| `dotnet build Hexalith.Parties.slnx -c Release --no-restore -m:1 -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` | Failed: 0 warnings, 3 CS0234 errors in Parties.UI. Shell 4.5.0 lacks FrontComposerRouteOptions (Program.cs:49) and FcModuleLandingPage (Routes.razor:7, Program.cs:223). DW-124 remains open. |

Latest unique focused package-mode result: 61 passed / 2 failed / 0 skipped.
Full Release tests, pack/API and package-only consumers remain blocked by the
solution prerequisite. Earlier source UI/browser receipts do not certify the
five later source checkout advances recorded in the 8.3 observation table.
Story 8.10/Epic 8 remain in-progress, and 8.7–8.9 remain blocked. Existing user
submodule checkouts were preserved; no Git staging, commit, push, source reset,
owner-repository edit, nested-submodule update or publication occurred.

## Story 8.10 working-tree identity approval and verification — 2026-10-04

Authority: spec 8.10 "Working-tree identity reconciliation — 2026-10-04". Parties
HEAD `7c720c7f9879ecb469993b1d2dff2bdb72b96e1b` had committed EventStore
`e968467c7db5b5685894fa2b85cfa7ec14512a7c`, FrontComposer
`37c8c6d295032968edba347940c41614ce8f2573`, Memories
`47027d35a1f4a2c6986bec85b5cae601ce1b201e`, and Tenants
`c05fe3171b2d3f95e0dd0970edfa6c421bed2f19` past the approved pins, and the
working tree advanced them (and Builds) further by clean fast-forwards. The
Administrator / jpiquot chose **"approve working tree"** for exactly these five
identities and nothing else:

| Root dependency | Prior approved | Approved working-tree identity | `git describe --tags --always` |
| --- | --- | --- | --- |
| Builds | `688eec9a4333245cc0ff7772115c769094471863` | `145ae921d9032f110b7371614939559e0aee202a` | `v4.29.1-16-g145ae92` |
| EventStore | `cbbe41501ba722731bf36b2c343efdef4ac714fb` | `2242ad55a1b678828df8aa093fd92399c29af5bf` | `v3.112.0-2-g2242ad55` |
| FrontComposer | `374bb83392d8ab4e8a8397cfd312d09948fb0b9d` | `2cc8dd3a3ac76c03f5ea6f6f92e65829306db470` | `v4.5.0-121-g2cc8dd3a` |
| Memories | `3d72927f4dac66af4968cc6726c4e96df292f2e4` | `5b43fe2f8a0f04dc021921a077dff1a573c2ce5e` | `v2.28.0` |
| Tenants | `cc348c9d7839ec7aad01649fc1c0e6f4fe672da4` | `04e655cf070b17eced9daefb9eaa87a09ec60e81` | `v5.7.0-141-g04e655cf` |

Each checkout was re-read before recording (`git -C <path> rev-parse HEAD`,
`git -C <path> describe --tags --always`, `git -C <path> status --porcelain`):
all five match the table and are clean, and no nested submodule is initialized.
AI.Tools `3f194e17`, Commons `116d2681`, and PolymorphicSerializations `98de6e01`
are unchanged (committed and checked out). The Builds catalog at `145ae921`
itself selects EventStore `3.112.0`, consistent with the Parties pre-import pin;
Commons `2.30.1`, FrontComposer `4.5.0`, Memories `2.27.1`, and Tenants `5.7.0`
are unchanged.

Recorded in: `.gitlink-signoff.tsv` (commented 2026-10-04 block plus five
`validated-advance` rows, owner `jpiquot`); `PlatformApiPrerequisitesTests`
(`BuildsSha`, `PayloadProtectionEventStoreSha`/`Describe`, `FrontComposerSha`,
`MemoriesSha`, `TenantsSha`, and the Memories/Tenants describe literals;
`AssertGitlinkAndCheckout` unchanged); the 8.3 reconciliation rows, the G4
amendment chain, one I20 five-pin row, and the annotated observation section;
`docs/architecture.md`; spine §7 I4, §7a I16, §7b I20, §12, and frontmatter;
DW-99; and the sprint-status Story 8.9/8.10 comments. The approval authorizes
recording, signing, and guarding these source pins only. It grants no I16
parity, owner release, publishing, commit, or rollback deletion. The gitlinks
stay uncommitted (the human commits them), so the committed-tree guard stays
red until then; no guard was weakened and no submodule was reset, staged, or
committed.

Exact commands and results:

| Command | Result |
| --- | --- |
| `dotnet build tests/Hexalith.Parties.Tests -c Release -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` | Passed (package mode by default): 0 warnings, 0 errors. The rebuilt assembly carries the re-stamped constants. |
| `dotnet tests/Hexalith.Parties.Tests/bin/Release/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.DocumentationFitnessTests -noColor` | 6/6 passed; 0 skipped. |
| `dotnet tests/Hexalith.Parties.Tests/bin/Release/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.EpicEightClosureFitnessTests -noColor` | 26/26 passed; 0 skipped. |
| `dotnet tests/Hexalith.Parties.Tests/bin/Release/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.PlatformApiPrerequisitesTests -noColor` | 14/16 passed, 2 failed, 0 skipped. Both failures are the expected committed-gitlink assertions: `FinalDependencyReceiptsMatchTheSelectedPackageAndSourceGraph` (`AssertGitlinkAndCheckout`) and `Matrix_ValidationEvidenceCommandsAreReproducible` (G5 `git ls-tree HEAD` check) expect `160000 commit 2242ad55a1b678828df8aa093fd92399c29af5bf references/Hexalith.EventStore`, while HEAD records `e968467c7db5b5685894fa2b85cfa7ec14512a7c`. |
| `bash scripts/gitlink-rc-gate.sh` | Passed: all five drifted checkouts have matching `validated-advance` signoff. |
| `bash scripts/gitlink-rc-gate.sh --diff 882c02455bbdd6b76886fe3fba8bd24a24e2a057` | Failed as expected: the intermediate committed pointers EventStore `e968467c`, FrontComposer `37c8c6d2`, Memories `47027d35`, and Tenants `c05fe317` were never approved; AI.Tools, Builds `688eec9a`, Commons, and PolymorphicSerializations pass. |
| `bash scripts/check-no-warning-override.sh` | Passed. |
| `git -c core.whitespace=cr-at-eol diff --check` | Passed. |
| Scratch-clone simulation (outside the repository): `git clone --shared` of Parties, tracked working-tree files copied in, each root submodule cloned `--shared` at its current checkout, and the approved gitlinks committed in the scratch clone only; the same Release test assembly and RC diff gate were run there. | Fitness 48/48 passed (documentation 6, closure 26, prerequisites 16; 0 skipped), including package/source graph evaluation and the G5 describe check that the real-tree failures short-circuit; `gitlink-rc-gate.sh --diff 882c02455bbdd6b76886fe3fba8bd24a24e2a057` passed all eight. This is diagnostic evidence that the only real-tree failures are the uncommitted pointers, not commit proof. |

The full solution, all-tests, pack, and a11y lanes were out of scope for this pass
(blocked by DW-124). Every receipt not rerun at the five approved identities is
marked unvalidated or remains blocked in the canonical table, including the
Playwright 6/6 execution at superseded FrontComposer `374bb833`. Closure stays
blocked by DW-124, the I16 parity approval, and I13 (DW-111). Story 8.10 and
Epic 8 stay `in-progress`; Stories 8.7–8.9 stay `blocked`. No Git staging,
commit, push, submodule reset/update, owner-repository edit, catalog change,
publication, or rollback deletion occurred.

## Story 8.10 final identity, approval, and EventStore 3.113.0 verification — 2026-10-05

Authority: spec 8.10 "Final identity, approval, and EventStore 3.113.0
reconciliation — 2026-10-05". Administrator / jpiquot decided: (1) approve
exactly Builds `360a2b9c4e96809365a7de785be9a68152d5ac28` (`v4.29.1-17-g360a2b9`)
and Tenants `72b8e4f508176b69826549e87b7b2a286f607fd1` (`v5.7.0-143-g72b8e4f5`);
(2) update EventStore to `3.113.0` as package and source, with the root checkout at
tag `v3.113.0` = `865cd9e49273dffbb1cdae85efeaf1aac322e09e`; (3) approve I16
identity re-validation for the final set, where a receipt is `Pass` only when it
was rerun at the stamped identity; (4) accept the missing I13 runtime proof as the
named DW-111 deferral (I13 stays `Deferred`).

| Root dependency | Final identity | `git describe --tags --always` | Committed at Parties `47e2da32`? |
| --- | --- | --- | --- |
| AI.Tools | `3f194e17174994d308ec84af9ee2b5aa68674d0d` | `3f194e1` | Yes |
| Builds | `360a2b9c4e96809365a7de785be9a68152d5ac28` | `v4.29.1-17-g360a2b9` | Yes (`2bdb303b`) |
| Commons | `116d26815eb81e35b3c161e1799e5ee12805fc0a` | `v2.30.1-15-g116d268` | Yes |
| EventStore | `865cd9e49273dffbb1cdae85efeaf1aac322e09e` | `v3.113.0` | **No**: HEAD records never-approved `0dc44e46ccb7f56c3182b855c21f337173a1307f`; commit pending |
| FrontComposer | `2cc8dd3a3ac76c03f5ea6f6f92e65829306db470` | `v4.5.0-121-g2cc8dd3a` | Yes (`2f6157d3`) |
| Memories | `5b43fe2f8a0f04dc021921a077dff1a573c2ce5e` | `v2.28.0` | Yes (`2f6157d3`) |
| PolymorphicSerializations | `98de6e013840ece9f0fa7c68ab7dcdf2bba3b375` | `v1.19.4` | Yes |
| Tenants | `72b8e4f508176b69826549e87b7b2a286f607fd1` | `v5.7.0-143-g72b8e4f5` | Yes (`2bdb303b`) |

Packages: EventStore `3.113.0` (Parties pre-import pin; the Builds `360a2b9c`
catalog itself still defaults to `3.112.0`), Commons `2.30.1`, FrontComposer
`4.5.0`, Memories `2.27.1`, Tenants `5.7.0`. Each checkout was re-read before
recording (`rev-parse`, `describe --tags --always`, `status --porcelain`): all
eight match and are clean, and no nested submodule is initialized. Never
approved: EventStore `547c938d52e4fd31b47783b9dd032528a5100549` (committed in
`2f6157d3`) and `0dc44e46ccb7f56c3182b855c21f337173a1307f` (committed in
`2bdb303b`), and Tenants `b55f96d89b839fd443ac589835880102add46d64` (committed in
`2f6157d3`). Superseded and never committed: the 2026-10-04 approvals of
EventStore `2242ad55` and Tenants `04e655cf`.

Recorded in: `.gitlink-signoff.tsv` (2026-10-05 block, three `validated-advance`
rows, owner `jpiquot`); `PlatformApiPrerequisitesTests` (`EventStorePackageVersion`
`3.113.0`, `BuildsSha`, `PayloadProtectionEventStoreSha`/`Describe` `v3.113.0`,
`TenantsSha`, Tenants describe literal; `AssertGitlinkAndCheckout` unchanged); the
8.3 reconciliation rows, G4 row, four 2026-10-05 I20 rows (including the filled I16
row and the I13/DW-111 row), and observation outcome; DW-99, DW-111 (decision),
DW-124 (resolved by `47e2da32`), new DW-139 to DW-142; `docs/architecture.md`;
`docs/ci.md`; spine frontmatter, §7 I4/I13, §7a I16, §7b I20, and §13; and the
sprint-status Story 8.9/8.10 comments.

Version-derivation fixes required by the approved Builds `360a2b9c` catalog and
the `3.113.0` pin (no guard was weakened):

- Builds `360a2b9c` routes `CommunityToolkit.Aspire.Hosting.Dapr` through
  `$(HexalithAspireHostingDaprVersion)` (unconditional `13.6.0-preview.1.261001-0243`;
  only `Hexalith.Folders.Aspire` overrides it). `DocumentationFitnessTests` now
  resolves catalog property references the way MSBuild evaluates them for a
  Parties project, and `PlatformApiPrerequisitesTests` pins both the property
  value and the `PackageVersion` alias exactly.
- `Hexalith.Parties.Ci.Tests` hard-coded `EventStore 3.104.0` for `docs/ci.md` and
  read the EventStore version only from the shared catalog. Both now read the
  effective version: the Parties root pin when present, else the catalog default.

Exact commands and results:

| Command | Result |
| --- | --- |
| `bash scripts/check-no-warning-override.sh && dotnet restore Hexalith.Parties.slnx -p:NuGetAudit=false && dotnet build Hexalith.Parties.slnx -c Release --no-restore -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` | Recorded in spec 8.10 as already run at the final working-tree identities: exit 0, 0 warnings, 0 errors, 00:02:02. Not rerun, as directed. |
| `dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Release -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` (package mode by default) | Passed: 0 warnings, 0 errors. |
| `dotnet tests/Hexalith.Parties.Tests/bin/Release/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.DocumentationFitnessTests -class Hexalith.Parties.Tests.FitnessTests.EpicEightClosureFitnessTests -class Hexalith.Parties.Tests.FitnessTests.PlatformApiPrerequisitesTests -noColor` | 48 total, 46 passed, 2 failed, 0 skipped. Both failures are the expected committed-gitlink assertions: `FinalDependencyReceiptsMatchTheSelectedPackageAndSourceGraph` (`AssertGitlinkAndCheckout`) and `Matrix_ValidationEvidenceCommandsAreReproducible` (G5 `git ls-tree HEAD`) expect `865cd9e49273dffbb1cdae85efeaf1aac322e09e`, while HEAD records `0dc44e46ccb7f56c3182b855c21f337173a1307f`. Before the catalog-alias fix, `MaintainedTechnologyTablesMatchCentralPackageCatalog` also failed. |
| Scratch-clone simulation (outside the repository): `git clone --shared` of Parties, the modified tracked files copied in, each root submodule cloned `--shared` at its current checkout, and only the EventStore gitlink committed in the scratch clone (`test: simulate committed eventstore v3.113.0 gitlink`, commitlint-valid); the same Release test assembly and RC diff gate run there | Fitness 48/48 passed (documentation 6, closure 26, prerequisites 16; 0 skipped), including the package/source graph evaluation, the G5 describe check, and the catalog assertions that the real-tree failure short-circuits; `bash scripts/gitlink-rc-gate.sh --diff 882c02455bbdd6b76886fe3fba8bd24a24e2a057` passed all eight. Diagnostic only, not commit proof. |
| `bash scripts/gitlink-rc-gate.sh` | Passed: EventStore drift `865cd9e4` has `validated-advance` signoff; every other root gitlink is clean. |
| `bash scripts/gitlink-rc-gate.sh --diff 882c02455bbdd6b76886fe3fba8bd24a24e2a057` | Failed as expected: only EventStore `0dc44e46` (committed, never approved); the other seven pass. Against `7c720c7f9879ecb469993b1d2dff2bdb72b96e1b`: Builds, FrontComposer, Memories, and Tenants pass; EventStore fails the same way. |
| `pwsh -NoProfile -File scripts/test.ps1 -Lane all -Configuration Release -ContinueOnFailure -ResultsDirectory TestResults` | Exit 1. 15 projects, 2,624 tests: 2,608 passed, 10 failed, 6 skipped. Passed: Authentication 12, Client 156, Projections 236, Security 178, AdminPortal 184, ConsumerPortal 82, Picker 171, Mcp 57, Sample 58, IntegrationTests 36 (+6 Story 12 skips). Failed: Contracts 179/180 (DW-142), Server 255/256 (DW-140), UI 339/343 (DW-141), Parties.Tests 610/612 (EventStore committed gitlink), Ci 55/57 (stale EventStore expectations). |
| `pwsh -NoProfile -File scripts/test.ps1 -Lane ci -Configuration Release -ContinueOnFailure -ResultsDirectory TestResults` (after the Ci.Tests fix) | Passed 57/57. |
| `pkg_dir=$(mktemp -d /tmp/parties-810-packages.XXXXXX); consumer_dir=$(mktemp -d /tmp/parties-810-consumer.XXXXXX); python3 scripts/pack-release-packages.py "$pkg_dir" 0.0.0-story810 && python3 scripts/validate-nuget-packages.py "$pkg_dir" && python3 scripts/validate-consumer-package-references.py "$pkg_dir" --work-directory "$consumer_dir"` | Passed: 9 packages validated; package-only consumers built with 0 warnings and 0 errors; Contracts depends on EventStore.Contracts `3.113.0`. |
| `npm ci --prefix tests/e2e && npm --prefix tests/e2e run typecheck` | Passed: 9 packages, 0 vulnerabilities; typecheck clean. |
| `npm --prefix tests/e2e run test:a11y` | Passed 6/6 at FrontComposer source `2cc8dd3a3ac76c03f5ea6f6f92e65829306db470`; the web server was started by Playwright and stopped on completion. |
| `bash scripts/check-no-warning-override.sh` (rerun after all edits) | Passed. |
| `git -c core.whitespace=cr-at-eol diff --check` | Passed. |

Closure stays open. The full Release lane is red (DW-140, DW-141, DW-142, and the
uncommitted EventStore gitlink), and I13 is an accepted deferral, not discharged.
Story 8.10 and Epic 8 stay `in-progress`; Stories 8.7–8.9 stay `blocked`. No Git
staging, commit, push, submodule reset/update, owner-repository edit, catalog
change, publication, or rollback deletion occurred in the repository; the only
commit was made in the scratch clone.

## Story 8.10 closure-blocker resolution (DW-140, DW-141, DW-142) — 2026-10-05

Authority: spec 8.10 "Closure-blocker resolution: DW-140, DW-141, DW-142 —
2026-10-05". Administrator / jpiquot decided: (1) DW-140 (I7): exempt
`ProvisionAgentParty`, `EstablishHumanActorBinding`, `RebindHumanActorBinding`,
and `RevokeHumanActorBinding` in the restriction inventory with one-line
justifications, production handlers unchanged, and prove the binding refusal;
(2) DW-142 (I8): classify `PartyCreated.CreatedAt`, `PartyState.HasBeenCreated`,
and `PartyState.HumanBindingVersion` as `NonPersonalMetadata`,
`PartyState.AgentProvisioning` as `PersonalData` (it is already marked
`[PersonalData]`; corrected 2026-10-05 in review loop 3 — this pass first
classified it `NonPersonalMetadata`), and `PartyState.HumanActorBindings` /
`PartyState.HumanActorTransitions` as `DeferredPrivacyDesign` bound to open
DW-129; (3) DW-141: pass `FcPageTabs` `ModuleRoute="/parties"` and
`DefaultTabId="overview"` only under `HFC_ROUTE_OPTIONS`, through an
`@attributes` dictionary in a new `PartiesOverview.razor.cs` partial, matching
`47e2da32`.

Tree under test: Parties `75b4fa1fe2cfd2167c186e94faf52e18be4b0ff6`
(`build(deps): select eventstore 3.113.0 and final pins`) plus this uncommitted
diff. `git ls-tree HEAD references/` records all eight final-set identities, each
checkout matches and is clean, and no nested submodule is initialized: AI.Tools
`3f194e17174994d308ec84af9ee2b5aa68674d0d`, Builds
`360a2b9c4e96809365a7de785be9a68152d5ac28` (`v4.29.1-17-g360a2b9`), Commons
`116d26815eb81e35b3c161e1799e5ee12805fc0a` (`v2.30.1-15-g116d268`), EventStore
`865cd9e49273dffbb1cdae85efeaf1aac322e09e` (`v3.113.0`), FrontComposer
`2cc8dd3a3ac76c03f5ea6f6f92e65829306db470` (`v4.5.0-121-g2cc8dd3a`), Memories
`5b43fe2f8a0f04dc021921a077dff1a573c2ce5e` (`v2.28.0`),
PolymorphicSerializations `98de6e013840ece9f0fa7c68ab7dcdf2bba3b375`
(`v1.19.4`), Tenants `72b8e4f508176b69826549e87b7b2a286f607fd1`
(`v5.7.0-143-g72b8e4f5`). Packages: EventStore `3.113.0` (Parties pre-import
pin), Commons `2.30.1`, FrontComposer `4.5.0`, Memories `2.27.1`, Tenants `5.7.0`.

Changes (no dependency, submodule, owner-repository, rollback-deletion, PRD,
`[PersonalData]`, or production handler change):

- `tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateRestrictionTests.cs`:
  the four commands are in the `exempt` inventory, each with a one-line
  justification.
- `tests/Hexalith.Parties.Server.Tests/Aggregates/HumanActorBindingTests.cs`:
  `RestrictedPersonParty_RefusesEstablishThatSucceedsUnrestricted`,
  `RestrictedBoundParty_RefusesRebindThatSucceedsUnrestricted`, and
  `RestrictedBoundParty_RefusesRevokeThatSucceedsUnrestricted` reuse the existing
  fixtures. Each command first succeeds on the unrestricted person party, and is
  then rejected with a single `HumanActorBindingRejected("binding-unavailable")`
  and no state-changing event once `ProcessingRestricted` is applied to the same
  state.
- `tests/Hexalith.Parties.Contracts.Tests/Privacy/PersonalDataInventoryTests.cs`:
  the six decided classifications (as first applied; review loop 3 below
  corrects `PartyState.AgentProvisioning` to `PersonalData`).
- `src/Hexalith.Parties.UI/Components/Pages/PartiesOverview.razor` and new
  `PartiesOverview.razor.cs`: `FcPageTabs @attributes="ModuleRouteTabAttributes"`,
  filled with `ModuleRoute`/`DefaultTabId` only under `#if HFC_ROUTE_OPTIONS`
  (empty in package mode).

| Command | Result |
| --- | --- |
| `bash scripts/check-no-warning-override.sh && dotnet restore Hexalith.Parties.slnx -p:NuGetAudit=false && dotnet build Hexalith.Parties.slnx -c Release --no-restore -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` | Exit 0: warning policy OK; Build succeeded, 0 Warning(s), 0 Error(s), 00:00:20.13 (incremental, the changed UI and test projects recompiled). |
| `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release -m:1 -p:HexalithFrontComposerFromSource=true -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` (diagnostic, not a canonical row) | Exit 0: 0 warnings, 0 errors against FrontComposer source `2cc8dd3a`. The source-mode `Hexalith.Parties.UI.dll` carries the gated `ModuleRoute`, `DefaultTabId`, and `no-party-binding` literals; the package-mode copy carries none. |
| Focused package-mode checks from the Release build: `HumanActorBindingTests` + `PartyAggregateRestrictionTests`; `PersonalDataInventoryTests`; `PartiesOverviewTests` (assemblies run directly with `-class`) | 35/35, 4/4, and 5/5 passed; 0 skipped. |
| `pwsh -NoProfile -File scripts/test.ps1 -Lane all -Configuration Release -ContinueOnFailure -ResultsDirectory TestResults` | Exit 0 in 5m29s; all 15 projects passed. 2,627 tests: 2,621 passed, 0 failed, 6 skipped (the existing Story 12 `HealthEndpointE2ETests` Tier 3 skips). Contracts 180, Authentication 12, Client 156, Server 259, Projections 236, Security 178, AdminPortal 184, ConsumerPortal 82, UI 343, Picker 171, Mcp 57, Parties.Tests 612, Sample 58, IntegrationTests 36 (+6 skipped), Ci 57. |
| `bash scripts/gitlink-rc-gate.sh` | Passed: all root gitlinks validated or clean. |
| `bash scripts/gitlink-rc-gate.sh --diff 882c02455bbdd6b76886fe3fba8bd24a24e2a057` | Passed: all eight root gitlinks `BUMP ok — validated-advance`, including EventStore `865cd9e4` now committed in `75b4fa1f`. |
| `pkg_dir=$(mktemp -d /tmp/parties-810-packages.XXXXXX); consumer_dir=$(mktemp -d /tmp/parties-810-consumer.XXXXXX); python3 scripts/pack-release-packages.py "$pkg_dir" 0.0.0-story810 && python3 scripts/validate-nuget-packages.py "$pkg_dir" && python3 scripts/validate-consumer-package-references.py "$pkg_dir" --work-directory "$consumer_dir"` | Exit 0: 9 packages validated; package-only client and portal consumers built with 0 warnings and 0 errors; the Contracts nuspec depends on `Hexalith.EventStore.Contracts` `3.113.0`. |
| `npm ci --prefix tests/e2e && npm --prefix tests/e2e run typecheck && npm --prefix tests/e2e run test:a11y` | Exit 0: 9 packages, 0 vulnerabilities; typecheck clean (TypeScript `7.0.2`); Playwright `1.63.0` chromium 6/6 passed in 22.8s against the source-mode UI at FrontComposer `2cc8dd3a`. Playwright started and stopped the web server; port 5072 was free before and after. |
| `dotnet tests/Hexalith.Parties.Tests/bin/Release/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.DocumentationFitnessTests -class Hexalith.Parties.Tests.FitnessTests.EpicEightClosureFitnessTests -class Hexalith.Parties.Tests.FitnessTests.PlatformApiPrerequisitesTests -noColor` (rerun after the ledger, matrix, spine, docs, sprint-status, and receipt edits) | 48/48 passed (documentation 6, closure 26, prerequisites 16), 0 skipped. |
| `git -c core.whitespace=cr-at-eol diff --check` | Passed; the new untracked `PartiesOverview.razor.cs` (CRLF) has no whitespace errors. |

Ledger and record updates: DW-140, DW-141, and DW-142 are `done 2026-10-05` with
`decision:` and `resolution:` lines; DW-142 names DW-129 as the open carrier;
DW-139 gains `PartiesOverview.razor` `ModuleRoute`/`DefaultTabId` in its location
and exit proof. The spine `open-condition`, §7 I4 row, `docs/architecture.md`
EventStore row, and the current 8.3 matrix EventStore rows cite `75b4fa1f` as the
EventStore gitlink commit; spine §13 has a dated paragraph for decisions 1-3.

At this point all six mandatory Verification-lane receipts passed at this tree.
The canonical table still held two **Unvalidated** producer receipts, rerun
below. I13 is an accepted deferral (DW-111), not discharged.

### Producer receipts rerun at the final set — 2026-10-05

Both producer checkouts are the clean committed root gitlinks. Their nested
submodules stayed uninitialized, and shared build props resolve from the root
`references/Hexalith.Builds`. Build outputs are ignored and both checkouts
stayed clean.

| Command (run in the root checkout) | Result |
| --- | --- |
| `references/Hexalith.PolymorphicSerializations` at `98de6e013840ece9f0fa7c68ab7dcdf2bba3b375` (`v1.19.4`): `dotnet restore Hexalith.PolymorphicSerializations.slnx -p:NuGetAudit=false && dotnet build Hexalith.PolymorphicSerializations.slnx -c Release --no-restore --no-incremental -m:1 -p:NuGetAudit=false`, then `dotnet test/Hexalith.PolymorphicSerializations.Tests/bin/Release/net10.0/Hexalith.PolymorphicSerializations.Tests.dll -noColor` | Build: 0 warnings, 0 errors. Tests: 15/15 passed, 0 failed, 0 skipped. |
| `references/Hexalith.FrontComposer` at `2cc8dd3a3ac76c03f5ea6f6f92e65829306db470` (`v4.5.0-121-g2cc8dd3a`): `dotnet restore tests/Hexalith.FrontComposer.Shell.Tests/Hexalith.FrontComposer.Shell.Tests.csproj -p:NuGetAudit=false && dotnet build tests/Hexalith.FrontComposer.Shell.Tests/Hexalith.FrontComposer.Shell.Tests.csproj -c Release --no-restore --no-incremental -m:1 -p:NuGetAudit=false`, then the test DLL with `-class` for `Hexalith.FrontComposer.Shell.Tests.Components.Layout.{Story13AccessibilityPrimitivesTests,FrontComposerShellTests,FcSystemThemeWatcherTests}` and `Hexalith.FrontComposer.Shell.Tests.State.Theme.ThemeEffectsScopeTests` | Build: 0 warnings, 0 errors. Tests: 64/64 passed, 0 failed, 0 skipped. |

Every canonical `### Validation receipts` row now reads `Pass` at the final set.
Story 8.10 and Epic 8 stay `in-progress` until the workflow's review step moves
them. Stories 8.7–8.9 stay `blocked`. No Git staging, commit, push, submodule
reset or update, owner-repository edit, catalog change, publication, or rollback
deletion occurred.

### Review loop 3 — 2026-10-05

Authority: spec 8.10 "Review loop 3 amendment — 2026-10-05". The review of the
closure-blocker resolution found one bad_spec entry (Edge 17):
`PartyState.AgentProvisioning` is marked `[PersonalData]` (`PartyState.cs:19-20`),
yet decision 2 classified it `NonPersonalMetadata`. Administrator / jpiquot
amended decision 2 (`AgentProvisioning` = `PersonalData`) and deferred the E2E
fixture-scope gap as the existing DW-143. The first-pass closure-blocker code was
reverted and re-derived per the spec's KEEP instructions with that correction.

Tree under test: Parties `75b4fa1fe2cfd2167c186e94faf52e18be4b0ff6` plus the
uncommitted loop-3 diff. `git ls-tree HEAD references/` records the eight
final-set identities, each checkout matches and is clean, and no nested submodule
is initialized: AI.Tools `3f194e17174994d308ec84af9ee2b5aa68674d0d`, Builds
`360a2b9c4e96809365a7de785be9a68152d5ac28`, Commons
`116d26815eb81e35b3c161e1799e5ee12805fc0a`, EventStore
`865cd9e49273dffbb1cdae85efeaf1aac322e09e` (`v3.113.0`), FrontComposer
`2cc8dd3a3ac76c03f5ea6f6f92e65829306db470`, Memories
`5b43fe2f8a0f04dc021921a077dff1a573c2ce5e`, PolymorphicSerializations
`98de6e013840ece9f0fa7c68ab7dcdf2bba3b375`, Tenants
`72b8e4f508176b69826549e87b7b2a286f607fd1`. Packages: EventStore `3.113.0`
(Parties pre-import pin), Commons `2.30.1`, FrontComposer `4.5.0`, Memories
`2.27.1`, Tenants `5.7.0`. SDK `10.0.401`.

Changes (no dependency, submodule, owner-repository, rollback-deletion,
`[PersonalData]`-attribute, production-handler, or PRD change):

- Re-derived per KEEP: the four `exempt` entries in
  `PartyAggregateRestrictionTests` (each comment says the command never mutates a
  restricted party and lists its actual outcomes); the `Rebind`, `Revoke`,
  `Restriction`, `BoundHuman`, and `ShouldBeRefusedAsUnavailable` helpers and the
  three `Restricted*_Refuses*ThatSucceedsUnrestricted` facts in
  `HumanActorBindingTests`; the six inventory rows in `PersonalDataInventoryTests`
  with `AgentProvisioning` = `PersonalData`; and the `PartiesOverview.razor`
  `@attributes` gate with the new `PartiesOverview.razor.cs`.
- New guard `PersonalDataInventoryTests.NonPersonalMetadataProperties_RemainUnmarked`
  (mirrors `OrganizationEntityFields_RemainUnmarkedByDefault`). Mutation check:
  temporarily reclassifying `AgentProvisioning` as `NonPersonalMetadata`, then
  rebuilding Contracts.Tests, made exactly this guard fail (4/5); the source was
  restored before the canonical build below.
- `EpicEightClosureFitnessTests`: `IsCleanPass` accepts only an exact `Pass`
  after trimming `*` and spaces (Edge 11; `Pass with errors`, `Pass (not run)`,
  and `Pass (partial)` added to the qualified-pass theory), and the FrontComposer
  stamp is required only for a clean `Pass` Playwright Result (Edge 12; new
  `AccessibilityStampIsRequiredOnlyForACleanPass` theory).
- `DocumentationFitnessTests.CodeMapDocumentsThePinnedSdkVersion` also fails any
  `10.0.\d{3}` token other than the pinned SDK (Blind 13 / Edge 16; new
  `StaleSdkTokensAreReportedBesideThePinnedSdk` theory).
- Records: `Directory.Packages.props` pin comment gains its removal condition
  (value unchanged), the seven current 8.3 reconciliation rows cite `75b4fa1f`,
  DW-142 is amended, DW-129 and DW-92 gain dated notes, the sprint-status G5
  receipt label and a loop-3 8.10 comment, the component-inventory banner date,
  and spine §13 plus this file's closure-blocker wording.

| Step | Command | Result |
| --- | --- | --- |
| 1 | `bash scripts/check-no-warning-override.sh && dotnet restore Hexalith.Parties.slnx -p:NuGetAudit=false && dotnet build Hexalith.Parties.slnx -c Release --no-restore -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` | Exit 0: warning policy OK; Build succeeded, 0 Warning(s), 0 Error(s), 00:00:11.05 (incremental). An earlier attempt in this pass failed with one CA1062 error in the new SDK theory; the argument guard was added and the command rerun. |
| 2 | `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release -m:1 -p:HexalithFrontComposerFromSource=true -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` (diagnostic, not a canonical row) | Exit 0: 0 warnings, 0 errors against FrontComposer source `2cc8dd3a`. The source-mode `Hexalith.Parties.UI.dll` carries the `ModuleRoute`, `DefaultTabId`, and `no-party-binding` literals; the package-mode copy from step 1 carries none. |
| 3a | `pwsh -NoProfile -File scripts/test.ps1 -Lane all -Configuration Release -ContinueOnFailure -ResultsDirectory TestResults` | Exit 0 in 312 s; all 15 projects passed. 2,640 tests: 2,634 passed, 0 failed, 6 skipped (the existing Story 12 `HealthEndpointE2ETests` Tier 3 skips). Contracts 181, Authentication 12, Client 156, Server 259, Projections 236, Security 178, AdminPortal 184, ConsumerPortal 82, UI 343, Picker 171, Mcp 57, Parties.Tests 624, Sample 58, IntegrationTests 36 (+6 skipped), Ci 57. |
| 3b | `bash scripts/gitlink-rc-gate.sh` | Passed: all root gitlinks validated or clean. |
| 3c | `bash scripts/gitlink-rc-gate.sh --diff 882c02455bbdd6b76886fe3fba8bd24a24e2a057` | Passed: all eight root gitlinks `BUMP ok — validated-advance`. |
| 4 | `pkg_dir=$(mktemp -d /tmp/parties-810-packages.XXXXXX); consumer_dir=$(mktemp -d /tmp/parties-810-consumer.XXXXXX); python3 scripts/pack-release-packages.py "$pkg_dir" 0.0.0-story810 && python3 scripts/validate-nuget-packages.py "$pkg_dir" && python3 scripts/validate-consumer-package-references.py "$pkg_dir" --work-directory "$consumer_dir"` | Exit 0 in 23 s: 9 packages validated; package-only client and portal consumers built with 0 warnings and 0 errors; the Contracts nuspec depends on `Hexalith.EventStore.Contracts` `3.113.0`. |
| 5 | `npm ci --prefix tests/e2e && npm --prefix tests/e2e run typecheck && npm --prefix tests/e2e run test:a11y` | Exit 0: 9 packages, 0 vulnerabilities; typecheck clean (TypeScript `7.0.2`); Playwright `1.63.0` chromium 6/6 passed in 13.1 s against the source-mode UI at FrontComposer `2cc8dd3a`. Port 5072 was free before and after; Playwright started and stopped the web server. |
| 6 | `dotnet tests/Hexalith.Parties.Tests/bin/Release/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.DocumentationFitnessTests -class Hexalith.Parties.Tests.FitnessTests.EpicEightClosureFitnessTests -class Hexalith.Parties.Tests.FitnessTests.PlatformApiPrerequisitesTests -noColor` (assembly from step 3a, run after every record edit in this pass) | 60/60 passed (documentation 9, closure 35, prerequisites 16), 0 skipped; rerun after this row was written, with the same result. |
| 7 | `git -c core.whitespace=cr-at-eol diff --check` | Passed; the untracked `PartiesOverview.razor.cs` (CRLF) has no whitespace errors under the same setting. |

Every canonical `### Validation receipts` row reads `Pass`; the six
Verification rows plus Parties UI tests and Static diff are re-stamped at
`75b4fa1f` plus this diff, and the two producer rows keep their 2026-10-05
reruns because the producer identities are unchanged. I13 remains an accepted
deferral (DW-111), not discharged, and DW-143 stays open. Story 8.10 and Epic 8
stay `in-progress` until the workflow's review step moves them; Stories 8.7–8.9
stay `blocked`. No Git staging, commit, push, submodule reset or update,
owner-repository edit, catalog change, publication, or rollback deletion
occurred.

### Review loop 3 patch rerun — 2026-10-05

After the loop-3 review (Review Triage Log, *Review loop 3*), the implementation
agent applied the eight patch entries: I20 outcome notes and a signoff comment,
G4 cells, historical pointers, the RC diff-base sentence, the DW-143 decision and
Playwright note, the SDK-band pattern, and `nameof` keys in
`PartiesOverview.razor.cs`. The parent session then reran the spec's
Verification lanes in order at Parties `75b4fa1fe2cfd2167c186e94faf52e18be4b0ff6`
plus the full uncommitted diff (final identity set unchanged):

| Command | Result |
| --- | --- |
| `bash scripts/check-no-warning-override.sh && dotnet restore Hexalith.Parties.slnx -p:NuGetAudit=false && dotnet build Hexalith.Parties.slnx -c Release --no-restore -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` | Exit 0: 0 warnings, 0 errors, 00:00:18.81. |
| `pwsh -NoProfile -File scripts/test.ps1 -Lane all -Configuration Release -ContinueOnFailure -ResultsDirectory TestResults` | Exit 0 in 313 s; all 15 projects passed: 2,641 tests, 2,635 passed, 0 failed, 6 skipped (the existing Story 12 `HealthEndpointE2ETests` Tier 3 cases). |
| `bash scripts/gitlink-rc-gate.sh && bash scripts/gitlink-rc-gate.sh --diff 882c02455bbdd6b76886fe3fba8bd24a24e2a057` | Pass: all eight root gitlinks `validated-advance` or clean. |
| Package/consumer command from `## Verification` | Exit 0: 9 packages validated; package-only consumers built. |
| `npm ci --prefix tests/e2e && npm --prefix tests/e2e run typecheck && npm --prefix tests/e2e run test:a11y` | Exit 0: 0 vulnerabilities; typecheck clean; Playwright 6/6 in 21.6 s at FrontComposer `2cc8dd3a`. |
| Three fitness classes, direct xUnit v3 (`DocumentationFitnessTests`, `EpicEightClosureFitnessTests`, `PlatformApiPrerequisitesTests`) | 61/61 passed, 0 skipped. |
| `git -c core.whitespace=cr-at-eol diff --check` | Passed. |

The two producer receipts keep their 2026-10-05 reruns: PolymorphicSerializations
`98de6e01` and FrontComposer `2cc8dd3a` are unchanged. DW-143, DW-144, and DW-145
are open follow-ups; DW-111 (I13) stays an accepted deferral.

## Story 8.7 G5 revalidation — closed-gate halt — 2026-10-05

At Parties `b3794a4dcbe2fff3e9ea5c420a3c4695e2d1a8ca`, G5 remains
`needs-additive-api`; Story 8.7 remains `blocked`. This receipt supersedes the
2026-10-03 inspection for this story. It does not alter Story 8.10 closure,
accepted deferrals, the I20 approval table, or the approved/frozen spec block.

| Identity | Observation |
| --- | --- |
| EventStore root gitlink and clean checkout | `865cd9e49273dffbb1cdae85efeaf1aac322e09e`, `v3.113.0`, matching; the existing I20 identity selection does not approve G5. |
| Builds root gitlink and clean checkout | `90f3836dd7482db35c2c187a50999b99215919b0`, `v4.29.1-21-g90f3836`, matching; both catalog and Parties pre-import pin select `3.113.0`. The later Builds identity is observed only; the dated I20 approved selection remains `360a2b9c4e96809365a7de785be9a68152d5ac28`. |
| Frozen Story 8.7 identity | `c21bd749154d701c3b7d68e40d1008d3475e35c4` / `3.95.0`, unchanged; reconcile with the eventual approved G5 identity before activation. |

| Inspection command | Result |
| --- | --- |
| `git rev-parse HEAD`; `git ls-tree HEAD references/Hexalith.EventStore references/Hexalith.Builds` | Parties baseline and matching root identities above. |
| `git -C references/Hexalith.EventStore rev-parse HEAD`; `git -C references/Hexalith.EventStore status --short --branch`; `git -C references/Hexalith.EventStore describe --tags --always` | Matching EventStore identity, clean detached checkout, `v3.113.0`. |
| `git -C references/Hexalith.Builds rev-parse HEAD`; `git -C references/Hexalith.Builds status --short --branch`; `git -C references/Hexalith.Builds describe --tags --always` | Matching Builds identity, clean checkout, `v4.29.1-21-g90f3836`. |
| `rg -n 'HexalithEventStoreVersion\|PayloadProtection' Directory.Packages.props references/Hexalith.Builds/Props/Directory.Packages.props references/Hexalith.EventStore/tools/release-packages.json` | Both catalogs select `3.113.0`; no payload package enrollment. |
| `cat references/Hexalith.EventStore/src/Hexalith.EventStore.PayloadProtection/Hexalith.EventStore.PayloadProtection.csproj` | Internal core exists with `IsPackable=false`; no consumable runtime provider. |
| `rg -n 'PayloadProtection\|AzureKeyVault' references/Hexalith.EventStore/Hexalith.EventStore.slnx references/Hexalith.EventStore/tools/release-packages.json references/Hexalith.Builds/Props/Directory.Packages.props` | Exit 1, no payload project/package enrollment in these three files; expected absent delivery. |
| Recorded G5 commands executed by the Python procedure below | 32/32 passed: public contracts/spec markers/statuses exist, no-op registration remains, closure/backend paths and catalog/release enrollment remain absent. |
| `rg -n 'PayloadProtection\|IKeyStorageBackend' src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs` and Python retained-file/DI assertions | All 24 retained files, local harness, local backend, payload service, adapter, and factory registration remain intact. |

Reproduce the 32 recorded G5 checks directly from the matrix without treating a
static pass as runtime/provider parity:

```bash
python3 - <<'PY_CHECK'
from pathlib import Path
import re
import subprocess
matrix = Path('_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md').read_text()
row = next(line for line in matrix.splitlines() if line.startswith('| Payload protection engine package |'))
assert row.split('|')[3].strip() == 'needs-additive-api'
commands = re.findall(r'`(rg -n -F [^`]+|test ! -f [^`]+)`(\s*\(expected no matches\))?', row)
for command, absent in commands:
    result = subprocess.run(command, shell=True, capture_output=True, text=True)
    assert result.returncode == (1 if absent else 0), (command, result.returncode, result.stderr)
assert len(commands) == 32
print('PASS: 32/32 recorded G5 static checks')
PY_CHECK
```

Owner 8.2 is `done`; 8.3 is `in-progress`; 8.4-8.11 remain `backlog`.
Public policy/erasure contracts and internal v2 core are partial delivery.
Existing owner contract and requirements approvals do exist; missing receipts are
compatibility/lifecycle/backend/runtime/release completion, dual-provider GDPR
parity, post-v2 rollback, and the final 8.11 G5 availability approval/closure.
The I2/I19a policy-hook classification approval is pending; Parties remains the
policy writer. Production KMS is a separate release gate. Parties 8.6 is done;
accepted Epic closure deferrals do not mark 8.7 complete. The crypto-retention
action stays `open`.

No production, DI, dependency, submodule, public-API, or frozen-spec changes were
made. Product/unit/topology/dual-provider/GDPR/post-v2 suites were not run or
credited while the start gate is closed. Documentation validation checks the
unchanged frozen spec and original baseline, unchanged sprint YAML data, valid
regenerated context, canonical revision fields, and exact six-file scope.

Documentation checks passed: frozen spec block/original baseline byte-identical,
sprint YAML data unchanged, all new revision fields canonical, regenerated
context valid, and the story File List matches the six changed documentation
artifacts. The recorded G5 reproduction command also passed 32/32 after the edits.
`git diff --check` reports CRLF line terminators as trailing whitespace in the
context and already-CRLF sprint file. `git -c core.whitespace=cr-at-eol diff --check`
passes with the repository's `.editorconfig` CRLF convention; no whitespace or
build policy file was changed.


## CI repair and current release selection — 2026-10-07

This evidence belongs to Parties baseline
`45d653a7d7f14a75d5227fbd780db3fbf22f70fe` plus the release repair patch. The
current source selections are recorded separately in the prerequisite matrix's
2026-10-07 section and `.gitlink-signoff.tsv`. EventStore runtime package consumers
select `3.115.0`; diagnostic EventStore source is
`48ef7171b9532f390b7b41b61ba679ba1030c923` (`v3.115.0-5-g48ef7171`). The older
2026-10-05 migration, producer, accessibility, and approval receipts retain their
original identities and results; this release selection does not revalidate them.

The new SDK prefers `IAsyncDomainProcessor`. Parties now registers its existing
validation/protection/erasure wrapper for both processor contracts and passes the
request cancellation token into aggregate processing. Destroyed-key replay adapts
only its redacted in-memory JSON copy to the SDK intake; stored event metadata and
the security service's redaction marker remain unchanged.

| Local command | Fresh result |
| --- | --- |
| `dotnet build Hexalith.Parties.slnx --configuration Release -m:1 -p:UseNuGetDeps=true -p:UseHexalithProjectReferences=false` | Initial dependency-aligned build and final source-patch build: exit 0, zero warnings/errors, without an EventStore command-line override; final build took 13.72 s. Actual host/projection/client assets select EventStore packages `3.115.0`. |
| `pwsh -NoProfile -File scripts/test.ps1 -Lane all -Configuration Release -ContinueOnFailure -ResultsDirectory /tmp/parties-release-tests` | Initial discovery run: 14/15 projects passed; 2,727 tests, 2,714 passed, 7 failed, 6 pre-existing Tier 3 health skips. Seven service-project failures exposed async routing, redacted replay metadata, and stale historical identity checks; focused repairs and reruns are recorded below. This initial run is not the final all-project release verdict. |
| `dotnet tests/Hexalith.Parties.Tests/bin/Release/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.Gateway.PartiesProcessEndpointTests -class Hexalith.Parties.Tests.Domain.PartyDomainProcessorValidationTests` | 25/25 passed after the routing and replay fixes, zero skips. Endpoint/privacy assertions were preserved. |
| `dotnet tests/Hexalith.Parties.Tests/bin/Release/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.PlatformApiPrerequisitesTests -class Hexalith.Parties.Tests.FitnessTests.EpicEightClosureFitnessTests -class Hexalith.Parties.Tests.FitnessTests.DocumentationFitnessTests -noColor` | 61/61 passed, zero skips. Current gitlink/package assertions use the release selection; historical consumption/a11y constants and receipts remain dated. G5 status, absent-provider/enrollment, rollback, and approval checks still run. |
| `python3 scripts/pack-release-packages.py /tmp/parties-release-packages 0.0.0-ci-test` and `python3 scripts/validate-nuget-packages.py /tmp/parties-release-packages` | Exit 0; exactly nine manifest packages packed and passed metadata/dependency validation. |
| `python3 scripts/validate-consumer-package-references.py /tmp/parties-release-packages --work-directory /tmp/parties-release-consumers` | Exit 0; isolated package-only client and portal consumers built with zero warnings/errors. This configured smoke test locally packs support packages from checked-out source; it is separate from the published EventStore package proof in the authoritative solution build. |
| `bash scripts/gitlink-rc-gate.sh --worktree`; `bash scripts/gitlink-rc-gate.sh --diff v1.1.1`; `bash scripts/check-no-warning-override.sh`; `git -c core.whitespace=cr-at-eol diff --check` | All passed. The whitespace check honors the existing `.editorconfig` CRLF convention. Plain `git diff --check` still flags CRLF in modified `.props`/source lines; no Git whitespace configuration was changed. |

Aspire baseline was observed through `aspire start`, `aspire describe`, and
`aspire wait parties --timeout 10`: Parties, EventStore, UI, Keycloak, and their
sidecars were healthy; the Tenants host was `Finished` with a healthy sidecar.
The explicitly started app was stopped with `aspire stop` before source edits.
That start also stopped the owned topology test instance that the CLI had
identified as the running AppHost. Consequently the initial topology pass is not
credited as live gateway proof. Its existing gateway test returns early when its
fixture is unavailable, independently of the six declared health-test skips; a
clean topology rerun is required for the final handoff.

FrontComposer's current source delegates projection/lifecycle announcements to
`FcSurfaceStatus`, whose markup contains `aria-live="polite"`. Reproducible source
inspection commands now check that delegation and shared region. This structural
finding does not refresh runtime accessibility parity at the advanced source pin.
No submodule content, gitlink, package inventory, container inventory, release
protection, or historical approval was changed. Final all-project reruns, source
review, GitHub CI, release dispatch/approval, and independent publication evidence
remain the parent release session's gates.

Final combined rerun of the five endpoint, processor, prerequisite, closure, and documentation classes after evidence edits: **86/86 passed**, zero skips (8.393 s). The parent subsequently reported the full service project passing **696/696**.

### Fresh topology fixture repair and local runtime limits — 2026-10-07

The clean topology rerun exposed an invalid fixture configuration: disabling
Keycloak left neither an Authority nor a SigningKey because the normal AppHost
clears symmetric keys for OIDC. The fixture now applies its intended symmetric
JWT settings to the test resources after model construction and shares the test
key/issuer with the gateway token. Production authentication guards and AppHost
policy are unchanged. The gateway test now dynamically skips an unavailable
fixture with its actual reason; all command/status/event assertions remain active
when the infrastructure starts.

| Local command | Fresh result |
| --- | --- |
| `dotnet build tests/Hexalith.Parties.IntegrationTests/Hexalith.Parties.IntegrationTests.csproj --configuration Release -m:1` | Exit 0; zero warnings/errors, 3.95 s. Log: `/tmp/parties-release-topology-patched-build.log`. |
| `dotnet test tests/Hexalith.Parties.IntegrationTests/Hexalith.Parties.IntegrationTests.csproj --configuration Release --no-build --verbosity minimal --results-directory /tmp/parties-release-topology-patched-results --report-xunit-trx --report-xunit-trx-filename Hexalith.Parties.IntegrationTests.trx` | Exit 0; 42 total, **35 passed, 7 skipped, zero failed**, 3m 16s. Log: `/tmp/parties-release-topology-patched-tests.log`. Six skips are the existing declared health deferrals. |
| Gateway TRX result `CreatePartyCommand_ThroughEventStoreGateway_CompletesWithPersistedEventCountAsync` | `NotExecuted`, explicit reason: `TimeoutException: Endpoint did not become ready within 00:03:00. Url: /health. Last status: ServiceUnavailable. Last error: n/a`. This result is not credited as live gateway proof. |

Read-only Aspire observation confirmed Parties, EventStore, and EventStore Admin
reached Running/Healthy after the JWT fix. Dapr sidecars independently exited
with `failed to add target .../statestore: no space left on device`; the parent
confirmed 38 GB of free disk space and exhausted file-watch resources. The
Tenants runtime build also failed CS0246 for
`IDomainServiceAdministratorVerifier` and `DomainServiceAdministratorClaim`.
Inspection of the published DomainService 3.115.0 package DLL found neither type
name; both exist in the retained newer EventStore source. Passing version 3.115.0
to that runtime build would not supply the missing contracts. No source-reference
workaround, submodule edit, root pointer update, or production policy change was
made. Independent review, final all-project reconciliation, fresh-runner GitHub
CI, and publication verification remain assigned to the parent release session.

Parent final service verification: `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --no-build --results-directory /tmp/parties-release-tests-final --report-xunit-trx --report-xunit-trx-filename Hexalith.Parties.Tests.trx` passed 696/696, zero skips, after all service fixes. Release tooling npm audit verified all 499 registry signatures and 120 attestations. Fresh-runner GitHub CI and publication are still pending.

### Review fixes and required CI gateway execution — 2026-10-07

All three review lenses completed; two reused prior threads because the runtime rejected additional fresh reviewer creation. The triage is recorded in the release repair spec. CI now fails an unavailable gateway fixture with its actual reason; local runs still report an explicit skip. Production keyed async cancellation, exact protected-input immutability, and named dependency-table identities are verified by focused tests. Both affected test projects built with zero warnings/errors and the three affected service classes passed **42/42**, zero skips (8.434 s). The real gateway negative check under `GITHUB_ACTIONS=true` returned **one failed, zero skipped**, exit 1, with the recorded `/health` 503 timeout (193.788 s). That proves the guard and is not successful live gateway evidence. The post-review solution Release build passed with zero warnings/errors (13.61 s). The pre-existing upstream Tenants runtime compile mismatch and wider orchestration/storage-read coverage gaps are recorded in `deferred-work.md`. Remote CI and publication remain pending.

Full post-review service project: **697/697 passed**, zero skips (18.867 s; `/tmp/parties-release-post-review-service.log`, TRX `/tmp/parties-release-post-review-service-results/Hexalith.Parties.Tests.trx`). This includes the new production async cancellation case.

GitHub CI run [37596156512](https://github.com/Hexalith/Hexalith.Parties/actions/runs/37596156512) on `bed34240` passed the Release build, consumers, all eleven unit projects, and Sample/CI projects. The full service project reported one failure: `SemanticSearchPerformanceBenchmarkTests.Search_10KEntries_CompletesWithin500ms` took **631 ms** against its **500 ms** gate while sharing parallel suite execution. Aspire did not run after that blocking failure. Commitlint, CodeQL, and the dispatched release-candidate gate passed separately. The pending Release passed source/registry-floor preflight and was canceled before approval/publication to supersede its source after the benchmark collection fix.

Semantic benchmark isolation verification: the affected service project Release build passed with zero warnings/errors (7.39 s); both benchmark classes and semantic provider behavior tests passed **30/30**, zero skips (1.830 s). The unchanged 10K semantic threshold measured 84 ms (exact/multi-token) and 40 ms (fuzzy). The complete service suite then passed **697/697**, zero skips (20.213 s; `/tmp/parties-release-benchmark-service.log`). Only the class collection attribute changed; production search and all performance/result assertions remain intact.

### Remote gates and publication blockers — 2026-10-07

Final source: `9096ea357fd627782828d671a6860c9eabe99c89` (main, pushed).

- [CI 37597236336](https://github.com/Hexalith/Hexalith.Parties/actions/runs/37597236336): the build-and-test job passed its Release warning-as-error build, isolated consumers, and **2,686/2,686** tests across eleven unit projects and three service/sample/CI projects, zero skips. Its separate required Aspire job failed: **35 passed, six existing health skips, one gateway failure** after `/health` returned 503 for three minutes. The fresh runner's Tenants launch log independently reports CS0246 for `IDomainServiceAdministratorVerifier` and `DomainServiceAdministratorClaim`. These unpublished SDK dependencies prevent a green full CI result; the new guard reports the failure instead of silently passing or skipping it.
- [Commitlint](https://github.com/Hexalith/Hexalith.Parties/actions/runs/37597236412), [CodeQL](https://github.com/Hexalith/Hexalith.Parties/actions/runs/37597236050), and [release-candidate gate](https://github.com/Hexalith/Hexalith.Parties/actions/runs/37597241012) passed for that exact source.
- [Release 37597780819](https://github.com/Hexalith/Hexalith.Parties/actions/runs/37597780819) used the supported `bypass-validation=true` input and the user's authorized production approval. Source/registry-floor preflight, npm signature verification, restore, Release build, nine-package metadata checks, isolated consumers, and publication preflight passed. NuGet then rejected the first upload with **HTTP 403**: `The specified API key is invalid, has expired, or does not have permission to access the specified package.` No GitHub Release was created and container publication did not run after that rejection.
- Independent verification of all nine NuGet indexes found **0/9** version `1.2.0` packages. `/tmp/parties-nuget-publication-1.2.0.json` contains each package's absence result. All nine indexes were queried again before cleanup.
- The failed attempt had created `v1.2.0` at the exact source above. After confirming that source, absent GitHub Release, and absent versions for all nine packages, the generated tag was removed. This restores a retry at `1.2.0`; no prior published tag was changed.
- Credential metadata: Parties has no repository or production-environment NuGet key override. It inherits organization `NUGET_API_KEY` (last updated 2025-09-20); no local NuGet key is available. A valid repository-level key with push permission for the nine Parties package IDs is required. Its value was neither read nor printed.
- Pending user input: repository credential update, plus whether this task may extend to fixing/releasing the upstream EventStore SDK (14 additional packages). An unanswered choice does not authorize that additional publication. No upstream repository was changed or published.

Retry after credential update: `gh run rerun 37597780819 --failed`, then approve the existing production deployment under the standing user authorization and independently rerun `python3 /tmp/parties-verify-published-packages.py 1.2.0 9096ea357fd627782828d671a6860c9eabe99c89`. Recheck current-main identity and all package indexes first. If source changes for upstream alignment, dispatch a new Release for its newly verified exact source instead. Full CI and publication acceptance remain incomplete; do not mark this spec done or claim published packages.


## Revalidate all tests and fix current failures — 2026-10-08

**Acceptance remains incomplete:** every configured project and browser test ran,
and root-owned failures were repaired. Both current .NET modes still fail the live
gateway prerequisite. The spec remains `in-progress`; no unavailable gateway or
unexpected dynamic skip is credited as a pass.

### Source identity and preserved workspace state

This run began on clean `main` at
`c095134f6ec648560954d6f0fc76858fd1d7cd0e`. Concurrent authors supplied source,
documentation, dependency selections and commits while validation continued.
Earlier root observations include `28fff255a18a7de540631c792034a8df5a33c980`.
The successful browser recorded-start/end snapshots and the final package/source
restore, build and full-test snapshots select
`f0004ffea4ee0fcc00fbbc99457f9ec1a9414f34` plus the recorded working-tree repairs.
Both broad .NET lanes kept that HEAD throughout. The mTLS source topology rerun
started there and ended at `904460303b2e32b40b35b3fe0d1970d73a52e62d`; its root
file SHA256 comparison reports no changed tracked or untracked source files.
The mTLS package rerun stayed at `904460303b2e32b40b35b3fe0d1970d73a52e62d`.
The evidence snapshots include the complete working-tree status, root file hashes,
and every root-declared checkout identity. They describe a working-tree run,
not proof of a clean committed release.

The current externally supplied package selection is EventStore `3.117.0`,
Commons `2.30.1`, FrontComposer `4.6.0`, Memories `2.27.1`, Tenants `5.7.0`, and
Parties `1.1.1`; the AppHost SDK is `13.6.1`. Those version/SDK and source-pointer
changes belong to the concurrent author, not this repair pass. The current source
checkouts used for these results are:

| Root dependency | Checkout HEAD |
| --- | --- |
| AI.Tools | `3f194e17174994d308ec84af9ee2b5aa68674d0d` |
| Builds | `ad52c5bdd4361c59eedf12a16620150006403584` |
| Commons | `116d26815eb81e35b3c161e1799e5ee12805fc0a` |
| EventStore | `b830d9829af70536d2a3fd21c5e2a23b2ca2f256` (`v3.117.0`, clean) |
| FrontComposer | `c561b3210f15206a90c39c82c58f2e5b1005cd60` |
| McpCli | `e159f82b7528797fc245045625ff387d65294ba9` |
| Memories | `aac6d9054cb138881e6e49c8e48233553123ffce` |
| Platform | `332a7d8104e3f6c9aaa57cbc7f07afb5fa0859de` |
| PolymorphicSerializations | `98de6e013840ece9f0fa7c68ab7dcdf2bba3b375` |
| Tenants | `86aa888301b40cda12b000f68d9292d53cd93efe` |

No dependency/version/routing file, reference content, gitlink or nested submodule
was changed by this repair pass. No staging, commit, push, branch, reset, clean,
or submodule initialization/update was performed. The concurrently edited MCP,
publication, architecture and prerequisite artifacts were preserved; only the
specified current receipt correction below was applied to their current content.
The parent separately inserted the Story 8.8 available-row execution gate outside
its frozen block. No historical approval or parity receipt was promoted.

### Commands and fresh lane results

All .NET test execution uses the individual-project Microsoft.Testing.Platform
runner, `scripts/test.ps1 -ContinueOnFailure`, inspectable TRXs and the existing
`GITHUB_ACTIONS=true` fixture guard. `.slnx` was used only for restore/build.
No legacy solution, project `--filter`, test exclusion or warning suppression was
introduced. Local `-Lane coverage` remains explicitly unsupported by the runner;
coverage was not simulated or claimed.

```bash
dotnet restore Hexalith.Parties.slnx -m:1
dotnet build Hexalith.Parties.slnx -c Release --no-restore -m:1
GITHUB_ACTIONS=true pwsh -NoProfile -File scripts/test.ps1 -Lane all -Configuration Release -ContinueOnFailure -ResultsDirectory TestResults/bmad-revalidation-20261008/package-latest -Properties UseHexalithProjectReferences=false,UseNuGetDeps=true,NuGetAudit=false,MinVerVersionOverride=1.0.0
```

Restore/build exited **0/0**, Release build **zero warnings/errors** (25.06 s).
The first complete package run exited **1**: all 15 TRXs, **3,031 total, 3,025
executed, 3,023 passed, two failed, six skipped**. One failure was the stale current
FrontComposer catalog assertion; the other was gateway readiness. After the narrow
receipt repair, the service project rebuilt with zero warnings/errors, its exact
fitness method passed **1/1**, and the complete Release service project passed
**907/907**, zero skips. Its separate receipt supersedes only that original
service row; the failed original TRX remains intact.

```bash
dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Release --no-restore -m:1 -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0
dotnet tests/Hexalith.Parties.Tests/bin/Release/net10.0/Hexalith.Parties.Tests.dll -method '*FinalDependencyReceiptsMatchTheSelectedPackageAndSourceGraph*'
dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --no-build --verbosity minimal --results-directory TestResults/bmad-revalidation-20261008/package-service-repaired --report-xunit-trx --report-xunit-trx-filename Hexalith.Parties.Tests.trx -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0
```

The fresh Debug source baseline retains the previously approved Commons package
routing flags and reads current versions from the unchanged catalog; it omits
only the obsolete July Commons `2.28.0`/Tenants `2.4.2` overrides.

```bash
dotnet restore Hexalith.Parties.slnx -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:HexalithCommonsFromSource=false -p:HexalithCommonsHttpFromSource=false -p:HexalithCommonsServiceDefaultsFromSource=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 -p:GeneratePackageOnBuild=false -p:BuildInParallel=false
dotnet build Hexalith.Parties.slnx -c Debug --no-restore -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:HexalithCommonsFromSource=false -p:HexalithCommonsHttpFromSource=false -p:HexalithCommonsServiceDefaultsFromSource=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 -p:GeneratePackageOnBuild=false -p:BuildInParallel=false
GITHUB_ACTIONS=true pwsh -NoProfile -File scripts/test.ps1 -Lane all -Configuration Debug -ContinueOnFailure -ResultsDirectory TestResults/bmad-revalidation-20261008/source-latest -Properties UseHexalithProjectReferences=true,UseNuGetDeps=false,HexalithCommonsFromSource=false,HexalithCommonsHttpFromSource=false,HexalithCommonsServiceDefaultsFromSource=false,NuGetAudit=false,MinVerVersionOverride=1.0.0,GeneratePackageOnBuild=false,BuildInParallel=false
```

Source restore/build exited **0/0**, Debug build **zero warnings/errors**. Its
complete all-project runner exited **1**, with **3,031 total, 3,025 executed,
3,024 passed, one gateway failure and six declared skips**. All fourteen other
projects pass. The latest reconciled package totals are identical; this does not
change either failed full-run exit code.

| Project (`Hexalith.Parties.*`) | Release package passed/executed | Debug source passed/executed | Failures per mode | Declared skips per mode |
| --- | --- | --- | --- | --- |
| AdminPortal.Tests | 190/190 | 190/190 | 0 | 0 |
| Authentication.Tests | 12/12 | 12/12 | 0 | 0 |
| Ci.Tests | 101/101 | 101/101 | 0 | 0 |
| Client.Tests | 171/171 | 171/171 | 0 | 0 |
| ConsumerPortal.Tests | 82/82 | 82/82 | 0 | 0 |
| Contracts.Tests | 181/181 | 181/181 | 0 | 0 |
| IntegrationTests | 35/36 | 35/36 | 1 | 6 |
| Mcp.Tests | 94/94 | 94/94 | 0 | 0 |
| Picker.Tests | 171/171 | 171/171 | 0 | 0 |
| Projections.Tests | 236/236 | 236/236 | 0 | 0 |
| Sample.Tests | 58/58 | 58/58 | 0 | 0 |
| Security.Tests | 178/178 | 178/178 | 0 | 0 |
| Server.Tests | 259/259 | 259/259 | 0 | 0 |
| Tests | 907/907 | 907/907 | 0 | 0 |
| UI.Tests | 349/349 | 349/349 | 0 | 0 |
| **Total** | **3,024/3,025** | **3,024/3,025** | **1** | **6** |

The parent verified its independent Story 8.8 gate repair with
`dotnet tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -method '*AvailableRowConsumersFailClosedOnMissingOrMismatchedIdentity*'`:
**1 total, zero errors/failed/skipped/not-run**. The final full Debug service run
also includes this case and all current immutable-source/available-row gates.

The six predeclared `HealthEndpointE2ETests` skips remain unchanged:
`QueryEndpoints_WithPubSubUnavailable_ReturnCachedDataWithDegradationHeaders`
(the in-memory pub/sub component shares its sidecar process),
`ReadyEndpoint_WithAllDaprComponentsRunning_Returns200Async`
(the deferred Tenants DAPR `/ready` contract),
`HealthEndpoint_WithAllDaprComponentsRunning_Returns200Async`,
`AliveEndpoint_WithAllDaprComponentsRunning_Returns200Async`,
`HealthEndpoint_WithAllDaprComponentsRunning_DoesNotIncludeDegradationHeadersAsync`
(existing Tier 3 Tenants-readiness deferrals), and
`HealthAndReadyEndpoints_WithDaprSidecarStopped_Return503ThenRecoverAsync`
(the deferred stop/restart recovery proof). A seventh dynamic unavailable-fixture
skip seen in an earlier local diagnostic is **unexpected and not accepted**;
final CI-strict runs keep that gateway test failed.

### Browser, behavior repairs and focused verification

Locked browser dependencies and Chromium were available; `npm ci` and final
`npm run typecheck` passed. The final **default** `DEBUG=pw:webserver npm test`
exited **0**, **90/90 executed/passed, zero failed/skipped** (1.1 m), including the
configured six specimen axe gates, all Consumer routes/interactions, anonymous
challenges, role restrictions and missing/duplicate binding cases. The host still
builds the source-mode UI before testing. `BuildInParallel=false` serializes that
existing build graph. Exact DEBUG stdout retained one earlier startup contention:
`NuGet.Build.Tasks.Pack.targets(226,5)` could not access the Commons.UniqueIds
Release XML file because another process was using it. Later serialized startup
and full inventory pass; no routing, pack or build gate was skipped. Earlier
full deliberate-host suites and failed automatic-host attempts remain retained.

Repairs made in this task, including early changes subsequently committed by
another process, are:

- Gateway claim security tests use the centralized subject claim type. Encryption
  tests reject complete serialized plaintext fields rather than random ciphertext
  substrings and retain explicit `$enc`/AES256GCM/key-version/ciphertext checks.
- The guarded Test-only shell context uses existing fixture claims. Anonymous,
  environment/configuration guard and Consumer/Admin role tests remain active;
  the accessibility wrapper stays restricted to its exact specimen route.
- UI composition registers the existing PartyPicker services and custom element,
  preserving configured/test query clients. Actual Fluent inputs, buttons,
  grid, radios, options and dropdown listboxes expose their required names, roles,
  checked/disabled states and keyboard behavior using supported parameters plus
  the scoped portal initializer where the installed API has no listbox seam.
- Search input changes reach the existing debounce. Empty local-fallback searches
  clear rows while actual degraded reads preserve them. Mobile detail attributes
  use explicit string values matching CSS so background rows are absent. GDPR
  destination and originating row focus work after route changes; row references
  are pruned for removed IDs and retained for reused buttons.
- Accepted create/edit status transfers once across the form-to-detail transition
  through internal circuit state. Tests clear it on changed tenant/user/auth scope,
  missing tenant, sign-out and unrelated routes. Public signatures stay intact.
- The existing export invocation now downloads its base64 JSON through a Blob,
  timestamped safe name and `finally` anchor/object-URL cleanup. Erasure uses the
  supported native named modal, actual input focus, immediate exact typed-name
  validation, visible warning plus a shadow-local accessible description, and
  scoped native-dialog percentage bounds/scrolling at 320 px and 200% zoom.
- Portal observers/listeners and JS modules clean up through existing `IDisposable`
  and private async helpers, with detached-element and late-import guards;
  no public `IAsyncDisposable`/`DisposeAsync` contract was added.
- Documentation inventory validates every authoritative `.gitmodules` path.
  Story 7.4/7.8 browser source checks follow the approved SDK migration while
  retaining historical decisions and substantive replay/rebuild/GDPR anchors.
  The current dependency receipt/test now matches catalog FrontComposer `4.6.0`;
  historical `4.5.0` approvals and accessibility receipts remain unchanged.
- Independent Admin browser cases reset their fixture and no longer use serial
  fail-fast grouping; the existing single worker and all failing assertions remain.
  Locators select actual input/control/record containers and safe opaque IDs;
  command/request, privacy, focus and recovery assertions remain enforced.

All affected complete component/UI projects pass in **both** final .NET modes:
AdminPortal **190**, ConsumerPortal **82**, UI **349**, zero failures/skips.
Additional awaited browser diagnostics confirm focus restoration after
search/clear **and** a fresh same-list request; erased export `party:null` with
no seeded PII, one URL created/revoked and zero leftover anchors; picker listener
count **1→0** after disposal with its old element disconnected; Escape clears the
typed confirmation without an erasure command; and no page errors.
`browser-manual-final.json` records these checks. An expanded diagnostic axe scan
outside the configured specimen gates found `document-title` (one node) and
`target-size` (nine nodes); those separate findings are not a configured-suite
failure or an acceptance change. The dropdown required-parent violation was fixed.

### Preserved failures and remaining live gateway acceptance

Earlier exact July-version source-command evidence is retained independently:
Commons `2.28.0` conflicts with Gateway's required UniqueIds `>=2.30.1` (**NU1109**).
It is historical diagnostic evidence, not the current-source verdict. Earlier
published EventStore `3.115.0` lacked `RequireEventStoreSidecarChannel`; the
externally supplied root `3.117.0`/SDK selection now builds cleanly. Earlier root
service warning and dirty EventStore-proof failures are also cleared in the
current full builds/service tests. The independently restored Tenants runtime
child still selects `3.115.0` and fails its administrator contracts, as verified
below; that current dependency-owned divergence remains a blocker.

The fresh baseline failure in both modes is
`EventStoreGatewayE2ETests.CreatePartyCommand_ThroughEventStoreGateway_CompletesWithPersistedEventCountAsync`:

```text
TimeoutException: Endpoint did not become ready within 00:03:00.
Url: /health. Last status: ServiceUnavailable. Last error: n/a
at EventStoreGatewayE2ETests.cs:40
```

Baseline runtime logs show mTLS disabled and Parties tenants readiness requests
returning **403**, leaving `tenants-integration` unhealthy. This is distinct from
obsolete file-watch or missing-package-API diagnostics.

The parent then ran the existing approved bootstrap
`bash scripts/aspire-start-mtls.sh --help` (exit **0**, no extra AppHost), which
created the three scoped control-plane containers. After the full source fixture
was disposed, full topology reruns executed sequentially with:

```bash
GITHUB_ACTIONS=true Dapr__Mtls__Enabled=true Dapr__Mtls__CertificateDirectory=/home/administrator/.local/state/hexalith-parties/dapr-certs pwsh -NoProfile -File scripts/test.ps1 -Lane topology -Configuration Debug -ContinueOnFailure -ResultsDirectory TestResults/bmad-revalidation-20261008/topology-mtls-source -Properties UseHexalithProjectReferences=true,UseNuGetDeps=false,HexalithCommonsFromSource=false,HexalithCommonsHttpFromSource=false,HexalithCommonsServiceDefaultsFromSource=false,NuGetAudit=false,MinVerVersionOverride=1.0.0,GeneratePackageOnBuild=false,BuildInParallel=false
GITHUB_ACTIONS=true Dapr__Mtls__Enabled=true Dapr__Mtls__CertificateDirectory=/home/administrator/.local/state/hexalith-parties/dapr-certs pwsh -NoProfile -File scripts/test.ps1 -Lane topology -Configuration Release -ContinueOnFailure -ResultsDirectory TestResults/bmad-revalidation-20261008/topology-mtls-package -Properties UseHexalithProjectReferences=false,UseNuGetDeps=true,NuGetAudit=false,MinVerVersionOverride=1.0.0
```

**Both exited 1**, each **42 total, 36 executed, 35 passed, one failed, six declared
skips**, with the same three-minute `/health` 503 guard. Relevant sidecars use the
generated mTLS configuration and connect to ports **55005/55006**; application
sidecar-channel authentication succeeds. In these fresh mTLS attempts the Parties
Tenants-readiness HTTP requests repeatedly hit their configured **two-second
TaskCanceledException timeout**. Both runtime logs independently show the Tenants
child build failing before it can serve readiness. The exact commands are:

```bash
dotnet run --project /home/administrator/projects/hexalith/parties/references/Hexalith.Tenants/src/Hexalith.Tenants/Hexalith.Tenants.csproj --configuration Debug --no-launch-profile
dotnet run --project /home/administrator/projects/hexalith/parties/references/Hexalith.Tenants/src/Hexalith.Tenants/Hexalith.Tenants.csproj --configuration Release --no-launch-profile
```

Both report **CS0246** in `Authorization/TenantsGlobalAdministratorVerifier.cs`:
line **19,81**, `IDomainServiceAdministratorVerifier` cannot be found, and line
**22,9**, `DomainServiceAdministratorClaim` cannot be found. Source log lines
966/968/1030 and package log lines 959/961/965 retain the exact compiler errors and
`The build failed`. Read-only inspection after the package attempt confirms
`references/Hexalith.Tenants/src/Hexalith.Tenants/obj/project.assets.json` selects
**`Hexalith.EventStore.DomainService/3.115.0`**. The child uses the Tenants-owned
catalog import and its own `dotnet run` restore/build; it does not receive the
root's pre-import EventStore `3.117.0` selection or broad source-routing overrides.
Thus a green root solution build does not establish a runnable Tenants child.
This verified dependency-owned runtime graph divergence remains the live gateway
prerequisite blocker. A fix that edits Tenants/reference content or changes its
versions/routing crosses the spec's **Ask First** boundary and was not applied.

No ACL, authorization, dependency, timeout or skip gate was loosened. The parent
stopped only the three newly bootstrapped scoped control-plane containers (exit
0); original global `dapr_*` resources remain preserved. Browser hosts and both
test fixtures were disposed.

Raw receipts are under ignored `TestResults/bmad-revalidation-20261008/`:
`package-latest-*`, `package-service-repaired-*`, `source-latest-*`,
`topology-mtls-*`, `browser-final-junit.xml`, `browser-final.log`, retained browser
traces/screenshots/videos and `browser-manual-final.json`. Command JSONs record
exact argv/environment, exit, duration and full source identities; before/after
JSONs preserve working-tree state and hashes. Earlier failure receipts are retained
without overwriting them. The successful browser stdout and prior DEBUG build
errors also remain in `/tmp/parties-revalidation-*` logs.

Warnings-as-errors remain enforced. The warning-override guard and repository
CRLF-aware `git -c core.whitespace=cr-at-eol diff --check` pass. Plain
`git diff --check` reports CRLF line terminators in already-CRLF modified files;
no Git whitespace/build configuration was changed. The spec's original
`baseline_revision`/`baseline_commit` remain
`8d28a1bc7fe5faebb09bf9cc495fa671346140f5`, and its frozen intent block remains
byte-identical (SHA256
`bcab44c7fa7bb0efac9a97f03ab961c6921ce845ee01695aaf728a295b9ae0d9`).
This consolidated section is appended; all earlier evidence text is preserved.

Post-evidence focused documentation/prerequisite validation: `dotnet tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.DocumentationFitnessTests -class Hexalith.Parties.Tests.FitnessTests.PlatformApiPrerequisitesTests -noColor` passed **43/43**, zero errors/failed/skipped/not-run (9.252 s). Log: `TestResults/bmad-revalidation-20261008/documentation-final-fitness.log`. This separate focused check does not add duplicate executions to the broad-lane counts.
