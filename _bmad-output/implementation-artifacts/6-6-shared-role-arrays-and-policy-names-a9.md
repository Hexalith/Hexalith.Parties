---
baseline_commit: 4a3b518
---

# Story 6.6: Shared role arrays and policy names (A9)

Status: done

## Story

As a maintainer,
I want role and policy names defined once,
so that authorization checks, navigation, and tests cannot drift through duplicated strings.

## Acceptance Criteria

1. Given role and policy names are repeated across hosts and portals, when shared anchors are added, then `Contracts` exposes `PartiesRoles` and policy-name constants used by the actor host, UI host, and portals.
2. Given the UI intentionally grants Admin access to `TenantOwner` aliases, when role arrays are centralized, then the shared base arrays are reused while the UI host still composes its documented `TenantOwner` additions.
3. Given Consumer and Admin navigation must never cross-render, when call sites adopt shared role/policy constants, then existing authorization behavior is unchanged.
4. Given policy names become shared anchors, when tests run, then Admin, TenantOwner, Consumer, forbidden, and unauthenticated paths remain covered.
5. Given this is a consolidation story, when implementation completes, then no new roles, policies, claims, or authorization semantics are introduced.

## Tasks / Subtasks

- [x] Add shared role/policy anchors in Contracts (AC: 1, 5)
  - [x] Add role names and policy names to a focused type such as `PartiesRoles`.
  - [x] Keep UI-specific additions composable outside Contracts if needed.
- [x] Replace duplicated strings (AC: 1-3)
  - [x] Update actor host authorization registration.
  - [x] Update UI host policy registration.
  - [x] Update AdminPortal/ConsumerPortal route or nav references.
  - [x] Update tests and topology fixtures.
- [x] Preserve TenantOwner behavior (AC: 2-4)
  - [x] Keep the UI host's documented `TenantOwner` and lowercase alias support if currently present.
  - [x] Add tests that prove TenantOwner still lands in/authorizes Admin.
- [x] Validate (AC: 3-5)
  - [x] Run `git diff --check`.
  - [x] Run focused UI authorization/nav tests and topology tests.
  - [x] Run solution build if available.

## Dev Notes

### Decision Context

- This story implements Class A item A9.
- The proposal explicitly says not to force identical role arrays everywhere. Shared base roles live in Contracts; host-specific additions remain host composition.

### Guardrails

- Do not change role names emitted by Keycloak/tache or topology fixtures.
- Do not weaken policy gates or replace policy checks with role checks in components.
- Do not make Admin and Consumer nav cross-render.
- Do not add new roles or silently remove `TenantOwner` access.

### References

- `_bmad-output/planning-artifacts/epics.md#Story-6.6-Shared-role-arrays-and-policy-names-A9`
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-28.md#Class-A--Internal-consolidation-approved--Epic-6`
- `_bmad-output/project-context.md#Consumer-portal-consent--GDPR-rights-Epics-4-5`
- `src/Hexalith.Parties.UI/Authentication/PartiesUiAuthorization.cs`
- `tests/Hexalith.Parties.UI.Tests/`

## Dev Agent Record

### Debug Log

- 2026-06-29T12:05:33+02:00 - Workflow activation resolved with no prepend/append steps; story 6.6 marked `in-progress` in sprint status while preserving existing `baseline_commit`.
- 2026-06-29T12:08:00+02:00 - Added red-phase `PartiesRoles` contract tests and shared-anchor assertions in host/UI/topology tests.
- 2026-06-29T12:09:00+02:00 - Implemented `PartiesRoles` in Contracts; rewired actor host Admin/Consumer policies, UI host policy arrays, portal route attributes, E2E fixture role claims, and topology assertions to shared anchors.
- 2026-06-29T12:11:00+02:00 - `git diff --check` passed.
- 2026-06-29T12:13:00+02:00 - Focused UI tests, topology tests, and solution build were attempted but blocked before test execution by MSBuild target-framework discovery failures in referenced submodule projects. `references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Contracts/Hexalith.FrontComposer.Contracts.csproj` fails `GetTargetFrameworks` with a zero-diagnostic MSBuild failure; `UseNuGetDeps=true` exposes a missing `references/Hexalith.FrontComposer/deps.nuget.props`. Per repo instructions, nested FrontComposer submodules were not initialized.
- 2026-06-29T12:20:00+02:00 - Senior Developer Review (AI): the FrontComposer block did not reproduce. Built and ran the full affected surface in Release with `-m:1`: Contracts.Tests `PartiesRolesTests` 3/3 + `ContractsPublicApiSnapshotTests` 1/1; Parties.Tests `PartiesConsumerPolicyTests` 5/5; UI.Tests `PartiesUiAuthorizationPolicyTests`/`RoleLandingRedirectTests`/`NoPartyBindingRoutingTests` all green; AdminPortal.Tests `PartiesAdminPortalAuthorizationTests` 5/5; ConsumerPortal.Tests `ConsumerPortalAuthorizationTests` 4/4; IntegrationTests `PartiesUiRealmRolesTests` 3/3 + `ConsumerPartyIdBindingRealmTests` 3/3. All affected projects compile clean (0 warnings/errors).

### Completion Notes

- Added shared `PartiesRoles` constants and base role arrays in `Hexalith.Parties.Contracts.Authorization`.
- Preserved existing authorization semantics: actor host Admin roles remain Admin/admin/Administrator/administrator; Consumer roles remain Consumer/consumer; UI Admin roles compose shared Admin base roles plus existing TenantOwner/tenantowner aliases.
- Replaced policy-name literals in AdminPortal and ConsumerPortal route attributes with Contracts policy constants.
- Updated focused tests and topology tests to assert shared anchors and TenantOwner composition.
- Story moved to `done` after the Senior Developer Review (AI) re-ran the focused authorization/nav/topology suites and affected-project builds successfully (see Debug Log 2026-06-29T12:20:00).

## Senior Developer Review (AI)

**Reviewer:** Jérôme Piquot — 2026-06-29
**Outcome:** Approve (no CRITICAL findings; all ACs implemented, all affected tests green)

### Scope validated

- **AC1** — `PartiesRoles` exists in `Hexalith.Parties.Contracts.Authorization` and is consumed by the actor host (`PartiesServiceCollectionExtensions`, `ConsumerPolicy`), the UI host (`PartiesUiAuthorization`), the E2E fixture, and both portals' route `[Authorize]` attributes. IMPLEMENTED.
- **AC2** — UI Admin role array is `[.. PartiesRoles.AdminRoleNames, .. PartiesRoles.TenantOwnerRoleNames]`: shared base reused, `TenantOwner` composed at the host. IMPLEMENTED.
- **AC3** — Behavior unchanged: actor host Admin set is still `{Admin, admin, Administrator, administrator}`; UI Admin adds `{TenantOwner, tenantowner}`; Consumer stays `{Consumer, consumer}`. No policy gate weakened, no role/policy substitution. IMPLEMENTED.
- **AC4** — Admin, TenantOwner (both casings), Consumer, forbidden, and unauthenticated paths covered by `PartiesUiAuthorizationPolicyTests` and the new `shared-role-policy-authorization.spec.ts`. IMPLEMENTED.
- **AC5** — No new roles/policies/claims/semantics; the E2E fixture's new `tenant-owner`/`tenantowner` cookie values map to the pre-existing `TenantOwner` role only. IMPLEMENTED.

### Findings and resolution (all auto-fixed)

- **[Medium] File List incomplete** — `tests/e2e/specs/shared-role-policy-authorization.spec.ts` (the AC4 E2E proof) was untracked and undocumented. Added to the File List.
- **[Medium] Mixed line endings introduced** — `ConsumerPartyIdBindingRealmTests.cs` (155/155 CRLF at HEAD → mixed) and `PartiesUiAuthorizationPolicyTests.cs` (129/129 → mixed) had LF lines spliced into otherwise-CRLF files, producing phantom diff churn. Normalized both to uniform LF — the repo's enforced standard (398/400 `.cs` files are LF and `git diff --check` rejects CR-at-eol), restoring internal consistency. `NoPartyBindingRoutingTests.cs` and `RoleLandingRedirectTests.cs` were already mixed at HEAD (pre-existing; left untouched).
- **[Medium] Validation overstated as blocked** — the dev record claimed FrontComposer MSBuild failures blocked all tests. The review reproduced none; the full affected surface builds and tests green. Validation tasks marked complete and the Debug Log corrected.

### Notes (no action required)

- `PartiesRoles` intentionally keeps `AdminPolicy`/`ConsumerPolicy` (policy names) distinct from `Admin`/`Consumer` (role names) even though the strings are equal today — this is the anchor separation the story asks for and is asserted by `PartiesRolesTests`.

## File List

- `_bmad-output/implementation-artifacts/6-6-shared-role-arrays-and-policy-names-a9.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Parties.Contracts/Authorization/PartiesRoles.cs`
- `src/Hexalith.Parties/Authorization/ConsumerPolicy.cs`
- `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs`
- `src/Hexalith.Parties.AdminPortal/Components/CreateEditPartyPage.razor`
- `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor`
- `src/Hexalith.Parties.ConsumerPortal/Components/EditMyProfilePage.razor`
- `src/Hexalith.Parties.ConsumerPortal/Components/MyConsentPage.razor`
- `src/Hexalith.Parties.ConsumerPortal/Components/MyPrivacyPage.razor`
- `src/Hexalith.Parties.ConsumerPortal/Components/MyProfilePage.razor`
- `src/Hexalith.Parties.UI/Authentication/PartiesUiAuthorization.cs`
- `src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs`
- `tests/Hexalith.Parties.Contracts.Tests/Authorization/PartiesRolesTests.cs`
- `tests/Hexalith.Parties.Contracts.Tests/Package/ContractsPublicApiSnapshot.txt`
- `tests/Hexalith.Parties.Tests/Authorization/PartiesConsumerPolicyTests.cs`
- `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalAuthorizationTests.cs`
- `tests/Hexalith.Parties.ConsumerPortal.Tests/Components/ConsumerPortalAuthorizationTests.cs`
- `tests/Hexalith.Parties.IntegrationTests/Topology/ConsumerPartyIdBindingRealmTests.cs`
- `tests/Hexalith.Parties.IntegrationTests/Topology/PartiesUiRealmRolesTests.cs`
- `tests/Hexalith.Parties.UI.Tests/NoPartyBindingRoutingTests.cs`
- `tests/Hexalith.Parties.UI.Tests/PartiesUiAuthorizationPolicyTests.cs`
- `tests/Hexalith.Parties.UI.Tests/RoleLandingRedirectTests.cs`
- `tests/e2e/specs/shared-role-policy-authorization.spec.ts`

## Change Log

- 2026-06-29 - Added shared role and policy anchors in Contracts and rewired host, portal, E2E fixture, and tests to use them.
- 2026-06-29 - Preserved UI-specific TenantOwner aliases through UI host composition over shared base Admin roles.
- 2026-06-29 - Validation partially complete: `git diff --check` passed; focused tests and solution build blocked before execution by referenced FrontComposer MSBuild target-framework discovery failure.
- 2026-06-29 - Senior Developer Review (AI): all five ACs verified IMPLEMENTED; affected-project builds and focused authorization/nav/topology suites re-run green. Auto-fixed three findings (documented the new E2E spec in the File List, normalized two CRLF→mixed test files to uniform LF, corrected the overstated "blocked" validation record). Status → done; sprint status synced.
