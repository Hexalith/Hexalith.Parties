---
title: '8.4 Leaf-project retirement'
type: 'refactor'
created: '2026-07-07T12:14:34+02:00'
status: 'done'
review_loop_iteration: 0
followup_review_recommended: false
baseline_commit: '6a1aed3bb04664f9005ebc19ede18a294ffd71ff'
baseline_revision: '6a1aed3bb04664f9005ebc19ede18a294ffd71ff'
final_revision: 'c091a5995d8877cd32ec5d64682f24a3a87af371'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-8-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-8-3-platform-api-prerequisites.md'
  - '{project-root}/_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md'
warnings: []
---

<intent-contract>

## Intent

**Problem:** Parties still carries obsolete wrapper projects that make the domain module look larger and more platform-owned than it is. The safe Story 8.4 work is to retire the wrapper shells whose replacement path is already direct and to preserve the tenant-claims wrapper until the Story 8.3 platform gate is satisfied.

**Approach:** Move `PartyAggregate` into the main Parties domain host project, delete the separate `Hexalith.Parties.Server` shell, replace `Hexalith.Parties.ServiceDefaults` consumers with direct `Hexalith.Commons.ServiceDefaults` calls, and record `Hexalith.Parties.Authentication` as an explicitly gated non-retirement.

## Boundaries & Constraints

**Always:** Preserve aggregate behavior and public command/event contracts; keep health endpoints `/health`, `/alive`, `/ready`; keep `RegisterDefaultSelfCheck=false` and the `Hexalith.Parties` activity source; use `.slnx` only and Central Package Management; update project references, tests, docs, and sprint evidence with the structural change.

**Block If:** `Hexalith.Commons.ServiceDefaults` cannot be consumed through the existing source/package reference pattern, moving `PartyAggregate` changes domain behavior, or retiring `Hexalith.Parties.Authentication` would require unapproved tenant-claims APIs still marked `needs-additive-api`.

**Never:** Do not change EventStore host SDK wiring, projection/query actors, crypto/data-protection code, public package contracts, UI behavior, DAPR topology, or `Hexalith.Parties.Authentication` until its Story 8.3 row has owner proof and release/submodule-pin evidence.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Service defaults wrapper removal | Root host, UI host, and MCP host call shared service defaults | Each host calls `Hexalith.Commons.ServiceDefaults` directly with the existing Parties defaults | Build fails if any host still references the deleted project |
| Aggregate shell removal | Tests and runtime invoke `PartyAggregate.Handle(...)` | Aggregate behavior is unchanged under the new `Hexalith.Parties.Domain` namespace | Focused aggregate tests fail on any behavior or namespace update miss |
| Gated auth retirement | Story 8.3 matrix still marks tenant claims transformation `needs-additive-api` | `Hexalith.Parties.Authentication` remains and sprint/test evidence records the blocker | Do not delete or partially migrate the auth project |

</intent-contract>

## Code Map

- `src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs` -- only production type in the separate Server shell; move into `src/Hexalith.Parties/Domain/PartyAggregate.cs`.
- `src/Hexalith.Parties/Domain/PartyDomainServiceInvoker.cs`, `src/Hexalith.Parties/Validation/*CompositeValidator.cs`, and aggregate tests -- update `PartyAggregate` namespace imports.
- `src/Hexalith.Parties.ServiceDefaults/*` -- wrapper project to delete after replacing callers with Commons service-default APIs.
- `src/Hexalith.Parties/Program.cs`, `src/Hexalith.Parties.UI/Program.cs`, `src/Hexalith.Parties.Mcp/Program.cs` -- replace wrapper extension calls with direct Commons configuration.
- `src/Hexalith.Parties/Hexalith.Parties.csproj`, `src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj`, `src/Hexalith.Parties.Mcp/Hexalith.Parties.Mcp.csproj`, and `Hexalith.Parties.slnx` -- remove retired project references and add direct Commons ServiceDefaults references.
- `tests/Hexalith.Parties.Tests/HealthChecks/ServiceDefaultsCompatibilityTests.cs`, MCP/deploy/package/fitness tests, and `tests/Hexalith.Parties.Server.Tests` -- update assertions and project references for retired shells.
- `_bmad-output/implementation-artifacts/tests/test-summary.md` and `_bmad-output/implementation-artifacts/sprint-status.yaml` -- append Story 8.4 evidence and keep the auth-retirement blocker visible.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.Parties/Domain/PartyAggregate.cs` and `src/Hexalith.Parties.Server/*` -- move the aggregate, update namespace/usings, and delete the Server project -- removes a leaf project shell without changing domain behavior.
- [x] Host project files and `Hexalith.Parties.slnx` -- remove `Hexalith.Parties.Server` and `Hexalith.Parties.ServiceDefaults` entries, add direct `Hexalith.Commons.ServiceDefaults` references where used, and keep no inline package versions -- keeps source/package modes explicit.
- [x] `src/Hexalith.Parties/Program.cs`, `src/Hexalith.Parties.UI/Program.cs`, and `src/Hexalith.Parties.Mcp/Program.cs` -- call Commons service-default APIs directly with Parties endpoint/activity-source defaults -- preserves runtime health and telemetry behavior after wrapper deletion.
- [x] Tests that reference Server or ServiceDefaults -- update namespaces, project references, and assertions; add or adjust a guard that the retired project paths are absent from `.slnx` and production project references -- prevents the shells from returning.
- [x] `_bmad-output/implementation-artifacts/tests/test-summary.md` and `_bmad-output/implementation-artifacts/sprint-status.yaml` -- record Story 8.4 verification and the gated `Hexalith.Parties.Authentication` non-retirement -- keeps BMAD continuity accurate.

**Acceptance Criteria:**
- Given the solution file and production projects, when inspected after implementation, then `Hexalith.Parties.Server` and `Hexalith.Parties.ServiceDefaults` are absent and no project references their deleted paths.
- Given aggregate tests, when the moved `PartyAggregate` is invoked through the new namespace, then existing create/update/consent/restriction/erasure behavior remains unchanged.
- Given the three Web hosts, when service defaults are configured, then `/health`, `/alive`, and `/ready` remain mapped, the shared self-check remains disabled, and `Hexalith.Parties` remains a traced activity source.
- Given the Story 8.3 tenant-claims row is still `needs-additive-api`, when Story 8.4 completes, then `Hexalith.Parties.Authentication` remains in place and the blocker is recorded rather than hidden.

## Spec Change Log

## Review Triage Log

### 2026-07-07 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 6: (high 0, medium 4, low 2)
- defer: 0
- reject: 2: (high 0, medium 0, low 2)
- addressed_findings:
  - `[medium]` `[patch]` Strengthened `PlatformApiPrerequisitesTests` so approved Story 8.4 paths must match aggregate-move or service-default-retirement diff shapes instead of bypassing the no-unapproved-migration guard by filename alone.
  - `[medium]` `[patch]` Hardened retired leaf fitness checks to inspect comment-stripped host source so service-default calls cannot be satisfied by comments.
  - `[medium]` `[patch]` Hardened the authentication-retirement gate check to parse the tenant-claims matrix row and assert its status, evidence, and decision cells directly.
  - `[medium]` `[patch]` Updated project context and project overview to document that the retired `Hexalith.Parties.ServiceDefaults` wrapper migrates consumers to `Hexalith.Commons.ServiceDefaults`.
  - `[low]` `[patch]` Normalized `.slnx` path separators in retired project checks so backslash paths cannot reintroduce retired project references undetected.
  - `[low]` `[patch]` Added sample-project build and sample onboarding guard verification to prove the sample was not left with deleted project references.

### 2026-07-07 — Follow-up review pass
- intent_gap: 0
- bad_spec: 0
- patch: 6: (high 0, medium 1, low 5)
- defer: 0
- reject: 5: (high 0, medium 0, low 5)
- addressed_findings:
  - `[medium]` `[patch]` Replaced broad substring matching in `PlatformApiPrerequisitesTests` with exact per-path Story 8.4 changed-line allowlists and CR normalization so approved paths cannot hide unrelated production edits.
  - `[low]` `[patch]` Extended retired project reference checks to scan owned `src` and `tests` projects so stale references cannot survive outside production project files.
  - `[low]` `[patch]` Replaced regex comment stripping in retired-leaf host source checks with a scanner that preserves string and character literals.
  - `[low]` `[patch]` Hardened retired-leaf matrix row parsing to skip only actual separator/header rows rather than data rows containing dash text.
  - `[low]` `[patch]` Hardened retired-leaf Markdown table splitting for escaped pipes, code-span pipes, and trailing escapes.
  - `[low]` `[patch]` Clarified generated documentation provenance for Story 8.4 structural path edits without claiming a new full rescan.

## Design Notes

`Hexalith.Parties.Authentication` is intentionally not part of the implementation scope even though the original epic summary lists it. The latest prerequisite matrix is stricter than the summary: it requires owner approval and public predicate proof before deleting that project.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` -- expected: aggregate test project builds against moved aggregate.
- `dotnet ./tests/Hexalith.Parties.Server.Tests/bin/Debug/net10.0/Hexalith.Parties.Server.Tests.dll` -- expected: aggregate tests pass.
- `dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` -- expected: fitness and service-default tests build.
- `dotnet ./tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.HealthChecks.ServiceDefaultsCompatibilityTests` -- expected: service-default compatibility tests pass.
- `dotnet build tests/Hexalith.Parties.DeployValidation.Tests/Hexalith.Parties.DeployValidation.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` -- expected: deploy validation tests build after project-reference updates.
- `git diff --check` -- expected: no whitespace or conflict-marker issues.

**Observed 2026-07-07:**
- `dotnet build tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` -- pass; 0 warnings, 0 errors.
- `dotnet ./tests/Hexalith.Parties.Server.Tests/bin/Debug/net10.0/Hexalith.Parties.Server.Tests.dll` -- pass; 237 passed.
- `dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` -- pass; 0 warnings, 0 errors.
- `dotnet ./tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.HealthChecks.ServiceDefaultsCompatibilityTests` -- pass; 8 passed.
- `dotnet ./tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.RetiredLeafProjectFitnessTests` -- pass; 3 passed.
- `dotnet build tests/Hexalith.Parties.DeployValidation.Tests/Hexalith.Parties.DeployValidation.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` -- pass; 0 warnings, 0 errors.
- `dotnet ./tests/Hexalith.Parties.DeployValidation.Tests/bin/Debug/net10.0/Hexalith.Parties.DeployValidation.Tests.dll -class Hexalith.Parties.DeployValidation.Tests.K8sManifestPublishTests` -- pass; 5 passed.
- `dotnet build tests/Hexalith.Parties.Mcp.Tests/Hexalith.Parties.Mcp.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` -- pass; 0 warnings, 0 errors.
- `dotnet ./tests/Hexalith.Parties.Mcp.Tests/bin/Debug/net10.0/Hexalith.Parties.Mcp.Tests.dll -class Hexalith.Parties.Mcp.Tests.PartiesMcpProjectFitnessTests` -- pass; 5 passed.
- `dotnet ./tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.PlatformApiPrerequisitesTests` -- pass; 10 passed after allowing only the approved Story 8.4 leaf-retirement paths in the Story 8.3 no-unapproved-migration guard.
- `dotnet build tests/Hexalith.Parties.Sample.Tests/Hexalith.Parties.Sample.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` -- pass; 0 warnings, 0 errors.
- `dotnet ./tests/Hexalith.Parties.Sample.Tests/bin/Debug/net10.0/Hexalith.Parties.Sample.Tests.dll -class Hexalith.Parties.Sample.Tests.SampleOnboardingGuardrailTests` -- pass; 7 passed.
- `dotnet ./tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.PlatformApiPrerequisitesTests` -- pass; 10 passed after review hardening changed approved path filtering into narrow diff-shape checks.
- `dotnet ./tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.RetiredLeafProjectFitnessTests` -- pass; 3 passed after path normalization, comment stripping, and row-specific matrix parsing.
- `dotnet ./tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.HealthChecks.ServiceDefaultsCompatibilityTests` -- pass; 8 passed after review patch validation.
- `git diff --check` -- pass.
- `dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` -- pass after follow-up review hardening; 0 warnings, 0 errors.
- `dotnet ./tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.PlatformApiPrerequisitesTests` -- pass after exact Story 8.4 changed-line allowlists; 10 passed.
- `dotnet ./tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.RetiredLeafProjectFitnessTests` -- pass after parser/comment-strip hardening; 3 passed.
- `dotnet ./tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.HealthChecks.ServiceDefaultsCompatibilityTests` -- pass after follow-up review hardening; 8 passed.
- `git diff --check` -- pass after follow-up review hardening.

## Auto Run Result

Status: done

Summary:
- Moved `PartyAggregate` into `src/Hexalith.Parties/Domain/PartyAggregate.cs` under `Hexalith.Parties.Domain` and deleted the retired `src/Hexalith.Parties.Server` production project shell.
- Deleted `src/Hexalith.Parties.ServiceDefaults` and updated the `parties`, `parties-ui`, and `parties-mcp` hosts to call `Hexalith.Commons.ServiceDefaults` directly with `/health`, `/alive`, `/ready`, `RegisterDefaultSelfCheck=false`, and `ActivitySourceNames.Add("Hexalith.Parties")`.
- Removed retired production project entries from `Hexalith.Parties.slnx`, updated source/package references, aggregate tests, service-default compatibility tests, MCP/deploy guards, docs, and the root project context.
- Follow-up review hardened the Story 8.4 diff guard, retired-project reference guard, host-source comment stripping, Markdown matrix parsing, and generated-doc provenance notes.
- Preserved `Hexalith.Parties.Authentication`; the Story 8.3 tenant-claims prerequisite row remains `needs-additive-api` and now records that the auth package remains after Story 8.4 until owner proof exists.

Files changed:
- Retired leaf projects: `src/Hexalith.Parties.Server/*`, `src/Hexalith.Parties.ServiceDefaults/*`, `Hexalith.Parties.slnx`, and host project references.
- Runtime migration: `src/Hexalith.Parties/Domain/PartyAggregate.cs`, host `Program.cs` service-default calls, and related domain/validation imports.
- Tests and evidence: aggregate tests, service-default compatibility tests, retired leaf fitness tests, platform prerequisite guard hardening, MCP/deploy/sample guards, Story 8.3 matrix, sprint status, and test summary.
- Documentation/context: `README.md`, `docs/*`, and `_bmad-output/project-context.md`, including Story 8.4 provenance notes in generated documentation.

Review findings breakdown:
- Patches applied in the follow-up pass: 6 (high 0, medium 1, low 5).
- Deferred: 0.
- Rejected in the follow-up pass: 5 low-severity context mismatches or intentional tradeoffs.
- Follow-up review recommendation: false; the final pass changed only localized tests/docs and introduced no runtime, API, security, or data behavior changes.

Verification performed:
- `dotnet build tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` -- pass.
- `dotnet ./tests/Hexalith.Parties.Server.Tests/bin/Debug/net10.0/Hexalith.Parties.Server.Tests.dll` -- pass; 237 passed.
- `dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` -- pass.
- `dotnet ./tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.PlatformApiPrerequisitesTests` -- pass; 10 passed after review hardening.
- `dotnet ./tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.RetiredLeafProjectFitnessTests` -- pass; 3 passed after review hardening.
- `dotnet ./tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.HealthChecks.ServiceDefaultsCompatibilityTests` -- pass; 8 passed.
- `dotnet build tests/Hexalith.Parties.DeployValidation.Tests/Hexalith.Parties.DeployValidation.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` -- pass.
- `dotnet ./tests/Hexalith.Parties.DeployValidation.Tests/bin/Debug/net10.0/Hexalith.Parties.DeployValidation.Tests.dll -class Hexalith.Parties.DeployValidation.Tests.K8sManifestPublishTests` -- pass; 5 passed.
- `dotnet build tests/Hexalith.Parties.Mcp.Tests/Hexalith.Parties.Mcp.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` -- pass.
- `dotnet ./tests/Hexalith.Parties.Mcp.Tests/bin/Debug/net10.0/Hexalith.Parties.Mcp.Tests.dll -class Hexalith.Parties.Mcp.Tests.PartiesMcpProjectFitnessTests` -- pass; 5 passed.
- `dotnet build tests/Hexalith.Parties.Sample.Tests/Hexalith.Parties.Sample.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` -- pass.
- `dotnet ./tests/Hexalith.Parties.Sample.Tests/bin/Debug/net10.0/Hexalith.Parties.Sample.Tests.dll -class Hexalith.Parties.Sample.Tests.SampleOnboardingGuardrailTests` -- pass; 7 passed.
- `dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` -- pass after follow-up review hardening.
- `dotnet ./tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.PlatformApiPrerequisitesTests` -- pass; 10 passed after exact changed-line hardening.
- `dotnet ./tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.RetiredLeafProjectFitnessTests` -- pass; 3 passed after parser/comment-strip hardening.
- `dotnet ./tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.HealthChecks.ServiceDefaultsCompatibilityTests` -- pass; 8 passed.
- `git diff --check` -- pass.

Residual risks:
- `Hexalith.Parties.Authentication` remains intentionally gated on the Story 8.3 tenant-claims prerequisite row.
- Existing Story 8.1 and 8.3 platform package/source readiness blockers remain outside this Story 8.4 retirement scope.
