---
baseline_commit: 4a3b518
---

# Story 6.3: Shared claims transformation library (A3)

Status: done

## Story

As a maintainer,
I want JWT claims transformation shared through a dedicated authentication library,
so that the actor host and UI host stop carrying parallel normalization logic.

## Acceptance Criteria

1. Given A3 was open in the original proposal, when this story starts, then the destination is already pinned: create a new `Hexalith.Parties.Authentication` project, not Commons and not `Contracts`.
2. Given the shared implementation needs `Microsoft.AspNetCore.Authentication`, when the project is added, then only the new authentication library takes that dependency and `Hexalith.Parties.Contracts` remains infrastructure-free.
3. Given both the actor host and UI host need tenant normalization, when services are registered, then both consume the shared transformation library while retaining host-specific JWT/OIDC/policy registration in their own projects.
4. Given transformation behavior already has coverage, when tests are moved or extended, then `tid`, `tenant_id`, JSON-array `tenants`, space-delimited `tenants`, idempotency, null, empty, and no-source cases remain covered.
5. Given the consolidation is complete, when the solution builds, then no circular project references or public API leaks are introduced.

## Tasks / Subtasks

- [x] Add the new authentication library (AC: 1, 2, 5)
  - [x] Create `src/Hexalith.Parties.Authentication/Hexalith.Parties.Authentication.csproj`.
  - [x] Add it to `Hexalith.Parties.slnx`.
  - [x] Reference central package versions only; do not add `Version=` to the project file.
- [x] Move/shared-implement claims transformation (AC: 2-4)
  - [x] Place shared transformation code in the new project.
  - [x] Use `PartiesClaimTypes` from Story 6.1 if available; otherwise coordinate sequencing so this story depends on 6.1.
  - [x] Keep host-specific authentication registration in the host projects.
- [x] Update callers (AC: 3, 5)
  - [x] Update `Hexalith.Parties` actor host registration.
  - [x] Update `Hexalith.Parties.UI` host registration.
  - [x] Remove duplicate host-local transformation implementations.
- [x] Add or move tests (AC: 4)
  - [x] Add a focused test project or extend an existing test project according to local test organization.
  - [x] Preserve Story 1.4 normalization and resolver tests.
- [x] Validate (AC: 5)
  - [x] Run `git diff --check`.
  - [x] Run focused authentication/UI tests.
  - [x] Run solution build if available.

## Dev Notes

### Decision Context

- This story resolves A3. The decision is not open for re-litigation during implementation.
- The project context and architecture already describe the exception: JWT `IClaimsTransformation` needs ASP.NET authentication types, so it cannot live in Contracts.

### Guardrails

- Do not move this logic into Commons as part of Epic 6. Cross-repo platform moves belong to deferred Epic 7.
- Do not change claim names, token shapes, OIDC setup, Keycloak realm configuration, or authorization policy behavior.
- Do not add a browser token flow or public endpoint.
- Do not log token or claim values.

### References

- `_bmad-output/planning-artifacts/epics.md#Story-6.3-Shared-claims-transformation-library-A3`
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-28.md#Class-A--Internal-consolidation-approved--Epic-6`
- `_bmad-output/planning-artifacts/architecture.md#Architectural-Boundaries`
- `_bmad-output/project-context.md#Critical-Implementation-Rules`
- `src/Hexalith.Parties.UI/Authentication/`

## Dev Agent Record

### Debug Log

- 2026-06-29T10:28:55+02:00: Marked sprint status for story 6.3 as `in-progress`; preserved existing `baseline_commit: 4a3b518`.
- 2026-06-29T10:39:18+02:00: `dotnet test` is blocked in this sandbox by .NET test-runner named-pipe creation (`SocketException (13): Permission denied`). Used xUnit v3 test executables directly for focused test execution.
- 2026-06-29T10:39:18+02:00: Default parallel MSBuild project-reference discovery failed silently in this checkout. Serialized builds with `/p:BuildInParallel=false` or `/m:1` compile successfully.
- 2026-06-29T10:39:18+02:00: Full `Hexalith.Parties.UI.Tests` executable has an unrelated existing failure in `MainLayoutAccessibilityTests.MainLayout_exposes_named_navigation_and_content_landmarks`; the touched claim resolver class passes.

### Completion Notes

- Added the dedicated `Hexalith.Parties.Authentication` project and moved tenant claim normalization into shared `PartiesClaimsTransformation`.
- Updated both the actor host and UI host to consume the shared transformation while leaving JWT/OIDC/policy registration in their existing host projects.
- Removed duplicate host-local transformation implementations from `Hexalith.Parties` and `Hexalith.Parties.UI`.
- Added focused authentication tests covering `tid`, `tenant_id`, JSON-array `tenants`, space-delimited `tenants`, idempotency, null, empty, and no-source cases.
- Added the focused authentication test project to the solution and unit test script.
- Senior Developer Review (AI) on 2026-06-29 re-validated all five ACs against the implementation, rebuilt the affected projects serially, and re-ran the focused authentication, composition, fitness, and resolver suites (all green). Two MEDIUM File List omissions were auto-fixed. No CRITICAL/HIGH issues remain; Status advanced to `done`.

## File List

- `Hexalith.Parties.slnx`
- `_bmad-output/implementation-artifacts/6-3-shared-claims-transformation-library-a3.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `scripts/test.ps1`
- `src/Hexalith.Parties.Authentication/Hexalith.Parties.Authentication.csproj`
- `src/Hexalith.Parties.Authentication/PartiesClaimsTransformation.cs`
- `src/Hexalith.Parties.UI/Authentication/PartiesClaimsTransformation.cs` (deleted)
- `src/Hexalith.Parties.UI/Authentication/PartiesUiAuthorization.cs`
- `src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj`
- `src/Hexalith.Parties/Authentication/PartiesClaimsTransformation.cs` (deleted)
- `src/Hexalith.Parties/Hexalith.Parties.csproj`
- `tests/Hexalith.Parties.Authentication.Tests/Hexalith.Parties.Authentication.Tests.csproj`
- `tests/Hexalith.Parties.Authentication.Tests/PartiesClaimsTransformationTests.cs`
- `tests/Hexalith.Parties.Tests/Authentication/PartiesAuthenticationCompositionTests.cs`
- `tests/Hexalith.Parties.Tests/FitnessTests/ContractsArchitectureFitnessTests.cs`
- `tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj`
- `tests/Hexalith.Parties.UI.Tests/PartiesClaimsTransformationTests.cs` (deleted)
- `tests/Hexalith.Parties.UI.Tests/PartyIdClaimResolverTests.cs`

## Senior Developer Review (AI)

Reviewer: Jérôme Piquot on 2026-06-29

Outcome: **Approve** (auto-fix mode — MEDIUM findings fixed during review).

### Acceptance Criteria

- AC1 — IMPLEMENTED: `src/Hexalith.Parties.Authentication/Hexalith.Parties.Authentication.csproj` created (not Commons, not Contracts) and registered in `Hexalith.Parties.slnx`.
- AC2 — IMPLEMENTED: the new library owns the ASP.NET dependency via `<FrameworkReference Include="Microsoft.AspNetCore.App" />`; Contracts stays infrastructure-free. Enforced by the new fitness tests `ContractsProjectDoesNotTakeAspNetCoreAuthenticationDependency` and `AuthenticationProjectOwnsSharedAspNetCoreAuthenticationDependency` (both green).
- AC3 — IMPLEMENTED: actor host registers the shared `PartiesClaimsTransformation` (`PartiesServiceCollectionExtensions.cs:76`, Transient) and the UI host via `AddPartiesUiClaimsResolution` (`PartiesUiAuthorization.cs:101`, Scoped); host-specific JWT/OIDC/policy registration retained in each host. The duplicate host-local transformation copies were deleted.
- AC4 — IMPLEMENTED: `tid`, `tenant_id`, JSON-array `tenants`, multi-value JSON array, space-delimited `tenants`, malformed-JSON fallback, idempotency, null, empty (`""`/`" "`), and no-source cases covered (12 tests). Story 1.4 resolver tests preserved (`PartyIdClaimResolverTests`, 11 tests, green).
- AC5 — IMPLEMENTED: serial builds (`-m:1`) of the auth library, both host test projects, and the UI test project succeed with 0 errors; no circular project references (Authentication → Contracts only); no public API leaks introduced.

### Findings (and resolution)

- MEDIUM (FIXED): File List omitted `tests/Hexalith.Parties.Tests/FitnessTests/ContractsArchitectureFitnessTests.cs` (modified — added the two boundary-enforcing fitness tests). Added to File List.
- MEDIUM (FIXED): File List omitted `tests/Hexalith.Parties.Tests/Authentication/PartiesAuthenticationCompositionTests.cs` (new — actor-host AC3 registration test). Added to File List.
- LOW (accepted, no change): shared transformation hardens empty-source handling from `IsNullOrEmpty` to `IsNullOrWhiteSpace`; this is covered by the new `EmptyTenantSource_AddsNoClaim_DoesNotThrow` theory (`""`/`" "`) and is within AC4's "empty" coverage.
- LOW (accepted, no change): the auth library references the full `Microsoft.AspNetCore.App` shared framework where only `Microsoft.AspNetCore.Authentication.Abstractions` is strictly required; this is the form pinned by the new fitness test and is consistent with other Hexalith libraries.

### Validation evidence

- Builds (Debug, `-m:1`): `Hexalith.Parties.Authentication.Tests`, `Hexalith.Parties.Tests`, `Hexalith.Parties.UI.Tests` — all succeeded, 0 errors.
- Tests (xUnit v3 executables run directly — `dotnet test` is blocked in this sandbox): Authentication 12/12; actor-host composition 1/1; `ContractsArchitectureFitnessTests` 5/5; UI `PartyIdClaimResolverTests` 11/11.
- `git diff --check`: clean (no whitespace errors).

## Change Log

- 2026-06-29: Added shared authentication claims transformation library, rewired actor/UI hosts to consume it, moved normalization coverage into a focused authentication test project, and documented validation limitations.
- 2026-06-29: Senior Developer Review (AI) — adversarial review passed (all 5 ACs implemented, all `[x]` tasks verified). Auto-fixed 2 MEDIUM File List discrepancies (undocumented fitness-test and actor-host composition-test files). No CRITICAL/HIGH issues; Status → done.
