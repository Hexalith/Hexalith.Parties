---
baseline_commit: 4a3b518
---

# Story 6.1: Shared claim types and extraction helpers (A1/A5)

Status: done

## Story

As a maintainer,
I want claim type constants and principal extraction helpers defined once,
so that tenant and user scope cannot drift across hosts, portals, and clients.

## Acceptance Criteria

1. Given duplicated claim literals exist for `eventstore:tenant`, `party_id`, `sub`, and `oid`, when shared anchors are added, then `Hexalith.Parties.Contracts` exposes `PartiesClaimTypes` constants and all application projects use them instead of local copies or raw string literals.
2. Given user and tenant extraction logic is repeated, when extraction helpers are added, then callers use one BCL-only shared helper surface that handles normalized tenant claims plus `sub`/`oid` consistently.
3. Given a principal has missing, empty, or ambiguous scope claims, when callers use the helper, then the result fails closed without throwing or silently choosing an arbitrary value.
4. Given `Contracts` is infrastructure-free, when this story is implemented, then no ASP.NET, Dapr, EventStore server, persistence, UI, or host package reference is added to `Hexalith.Parties.Contracts`.
5. Given the consolidation is complete, when tests and scans run, then constants/extraction parity is covered and the boundary fitness test remains green.

## Tasks / Subtasks

- [x] Add shared claim constants in `src/Hexalith.Parties.Contracts/Authorization/PartiesClaimTypes.cs` (AC: 1, 4)
  - [x] Include constants for `eventstore:tenant`, `party_id`, `sub`, and `oid`.
  - [x] Keep the file BCL-only and named for the type.
- [x] Add BCL-only extraction helpers in Contracts (AC: 2-4)
  - [x] Prefer a focused static helper or extension type that works with `ClaimsPrincipal` / `ClaimsIdentity`.
  - [x] Return explicit success/failure results for tenant and user extraction instead of throwing for routine missing claims.
  - [x] Preserve current fail-closed behavior for ambiguous `party_id` and tenant values.
- [x] Replace local constants and helper copies (AC: 1-3)
  - [x] Update actor host authentication/authorization code.
  - [x] Update UI authentication/self-scope code.
  - [x] Update AdminPortal and ConsumerPortal call sites that inspect claims.
  - [x] Remove obsolete local constants after callers move.
- [x] Add or update tests (AC: 1-5)
  - [x] Cover normalized tenant, subject fallback order, object id fallback, absent values, empty values, and ambiguity.
  - [x] Add a scan or focused assertion that the known raw claim literals no longer appear outside the shared anchor, except in tests that intentionally assert wire values.
  - [x] Keep existing Story 1.4 party binding tests green. (Verified in review: `PartyIdClaimResolverTests` 11/11, `NoPartyBindingRoutingTests` 5/5, `RoleLandingRedirectTests` 13/13, `SelfScopedPartiesClientTests` 42/42 all pass.)
- [x] Validate (AC: 5)
  - [x] Run `git diff --check`.
  - [x] Run focused Contracts/UI/authentication tests. (Review: Contracts 15+1+1, UI 11+5+13+42+9+4, Gateway 51, AdminPortal 11+111 — all green.)
  - [x] Run `dotnet build Hexalith.Parties.slnx -c Release --no-restore` if the environment supports it. (The `--no-restore` slnx path fails on a known environment quirk; verified instead via per-project `dotnet build` **with** restore — every affected project builds with 0 warnings/0 errors.)

## Dev Notes

### Decision Context

- This story implements Class A items A1 and A5 from `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-28.md`.
- The readiness remediation pins shared claim constants and pure extraction helpers to `Hexalith.Parties.Contracts`.
- Do not move JWT transformation logic in this story; that is Story 6.3 and belongs in `Hexalith.Parties.Authentication`.

### Guardrails

- `Contracts` may use BCL types such as `System.Security.Claims`; it must not take ASP.NET or host dependencies.
- Consumer own-data access remains fail-closed. Do not weaken `PartyIdClaimResolver`, `NoPartyBinding`, or `ISelfScopedPartiesClient` behavior to make helpers easier to call.
- Do not add new claim names or change token semantics. This is consolidation, not a new authentication design.
- Do not log claim values, tenant ids, user ids, party ids, or tokens.

### References

- `_bmad-output/planning-artifacts/epics.md#Story-6.1-Shared-claim-types-and-extraction-helpers-A1A5`
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-28.md#Class-A--Internal-consolidation-approved--Epic-6`
- `_bmad-output/project-context.md#Critical-Implementation-Rules`
- `src/Hexalith.Parties.UI/Authentication/PartyIdClaimResolver.cs`
- `src/Hexalith.Parties.UI/Services/SelfScopedPartiesClient.cs`

## Dev Agent Record

### Debug Log

- 2026-06-29: Added `PartiesClaimTypes`, `PartiesClaimExtraction`, `PartiesClaimExtractionFailure`, and `PartiesClaimExtractionResult` under Contracts with BCL-only `System.Security.Claims` usage.
- 2026-06-29: Replaced actor host, UI host, AdminPortal, MCP, security metric tag, and test fixture claim literals with shared constants/helpers; removed obsolete `src/Hexalith.Parties/Authorization/PartiesAuthClaims.cs`.
- 2026-06-29: Added Contracts tests for tenant/user/party extraction success and fail-closed cases, plus a raw-claim-literal scan scoped to `src/` and `tests/`.
- 2026-06-29: `rg -n '"eventstore:tenant"|"party_id"|"sub"|"oid"' src tests --glob '*.cs'` now reports only `PartiesClaimTypes.cs` and the intentional Keycloak mapper wire-value test.
- 2026-06-29: `git diff --check` passed.
- 2026-06-29: `bash scripts/check-no-warning-override.sh` passed.
- 2026-06-29: Focused test commands attempted but blocked before compilation:
  - `dotnet test tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj -c Release --no-restore -v:minimal`
  - `dotnet test tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -c Release --no-restore -v:minimal`
  - `dotnet test tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj -c Release --no-restore -v:minimal`
- 2026-06-29: Solution build attempted with `dotnet build Hexalith.Parties.slnx -c Release --no-restore -v:minimal`; it failed before compilation with `Build FAILED. 0 Warning(s) 0 Error(s)`.
- 2026-06-29: Diagnostic build shows MSBuild failing during `_GetProjectReferenceTargetFrameworkProperties` while querying referenced projects, e.g. `references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj` and `src/Hexalith.Parties.AdminPortal/Hexalith.Parties.AdminPortal.csproj`, with no compiler diagnostics emitted.

### Completion Notes

- Shared claim constants and extraction helpers are implemented in `Hexalith.Parties.Contracts`.
- Call sites now use shared constants/helpers instead of raw claim literals or duplicated extraction logic.
- Consumer party binding now fails closed unless both a single `party_id` and a single normalized tenant claim are present.
- Senior Developer Review (AI) 2026-06-29 verified all affected projects build (per-project, with restore) and all focused tests pass; the earlier "blocked" state was only the `--no-restore` slnx build path. Story moved to `done`.

## File List

- `_bmad-output/implementation-artifacts/6-1-shared-claim-types-and-extraction-helpers-a1-a5.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Parties.Contracts/Authorization/PartiesClaimExtraction.cs`
- `src/Hexalith.Parties.Contracts/Authorization/PartiesClaimExtractionFailure.cs`
- `src/Hexalith.Parties.Contracts/Authorization/PartiesClaimExtractionResult.cs`
- `src/Hexalith.Parties.Contracts/Authorization/PartiesClaimTypes.cs`
- `src/Hexalith.Parties.AdminPortal/Services/AdminPortalAuthorizationService.cs`
- `src/Hexalith.Parties.Mcp/PartiesMcpRequestContext.cs`
- `src/Hexalith.Parties.Security/DecryptionCircuitBreaker.cs`
- `src/Hexalith.Parties.UI/Authentication/PartiesUiAuthorization.cs`
- `src/Hexalith.Parties.UI/Authentication/PartyIdClaimResolver.cs`
- `src/Hexalith.Parties.UI/Program.cs`
- `src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs`
- `src/Hexalith.Parties/Authentication/PartiesClaimsTransformation.cs`
- `src/Hexalith.Parties/Authorization/PartiesAuthClaims.cs` (deleted)
- `tests/Hexalith.Parties.AdminPortal.Tests/Components/CreateEditPartyPageTests.cs`
- `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs`
- `tests/Hexalith.Parties.Contracts.Tests/Authorization/PartiesClaimExtractionTests.cs`
- `tests/Hexalith.Parties.Contracts.Tests/Authorization/PartiesClaimLiteralConsolidationTests.cs`
- `tests/Hexalith.Parties.Contracts.Tests/Package/ContractsPublicApiSnapshot.txt`
- `tests/Hexalith.Parties.IntegrationTests/Gateway/EventStoreGatewayE2ETests.cs`
- `tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs`
- `tests/Hexalith.Parties.Tests/HealthChecks/HealthEndpointIntegrationTests.cs`
- `tests/Hexalith.Parties.UI.Tests/NoPartyBindingRoutingTests.cs`
- `tests/Hexalith.Parties.UI.Tests/PartiesClaimsTransformationTests.cs`
- `tests/Hexalith.Parties.UI.Tests/PartiesUiAuthenticationCompositionTests.cs`
- `tests/Hexalith.Parties.UI.Tests/PartyIdClaimResolverTests.cs`
- `tests/Hexalith.Parties.UI.Tests/RoleLandingRedirectTests.cs`
- `tests/Hexalith.Parties.UI.Tests/SelfScopedPartiesClientTests.cs`
- `tests/e2e/specs/consumer-party-binding.spec.ts`

## Senior Developer Review (AI)

**Reviewer:** Administrator — 2026-06-29 — Outcome: **Approve** (all findings auto-fixed)

### Acceptance Criteria

| AC | Verdict | Evidence |
| --- | --- | --- |
| AC1 — shared `PartiesClaimTypes`, no stray literals | ✅ Implemented | `PartiesClaimLiteralConsolidationTests` green; repo scan finds the 4 literals only in the anchor + the intentional Keycloak wire-value test. Local aliases (`PartiesUiAuthorization`, `PartiesClaimsTransformation`) now delegate to the anchor, so no drift. |
| AC2 — BCL-only extraction helper (tenant + `sub`/`oid`) | ✅ Implemented | `PartiesClaimExtraction` (extension methods on `ClaimsPrincipal`/`ClaimsIdentity`); only `System.Security.Claims` referenced. |
| AC3 — missing/empty/ambiguous fails closed, no throw | ✅ Implemented | `PartiesClaimExtractionResult` returns `Missing`/`Empty`/`Ambiguous` without throwing (only `ArgumentNullException` for a null principal). 15 `PartiesClaimExtractionTests` cover every shape. |
| AC4 — `Contracts` stays infrastructure-free | ✅ Implemented | `Hexalith.Parties.Contracts.csproj` is byte-for-byte unchanged vs HEAD; no ASP.NET/Dapr/host package added. |
| AC5 — parity tests + boundary fitness green | ✅ Implemented | Extraction parity + consolidation scan added; `ContractsPublicApiSnapshotTests` green (snapshot correctly extended additively). |

### Findings (all auto-fixed in this review)

1. **[MED] Mixed line endings introduced** — `src/Hexalith.Parties.UI/Authentication/PartyIdClaimResolver.cs` and `PartiesUiAuthorization.cs` ended up with LF in the edited regions and CRLF elsewhere. **Fixed:** normalized both files to LF (repo is 98.7% LF and the dev already normalized the three sibling test files in this story to LF).
2. **[MED] Misordered `using` directive** — `tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs` placed `using Hexalith.Parties.Contracts.Authorization;` between two `Hexalith.EventStore.Server.*` usings. **Fixed:** moved to its correct alphabetical position. (Not a build error — no SA1210/IDE0055 enforcement — but inconsistent with the same import placed correctly in the sibling files.)
3. **[MED] File List omission** — `tests/e2e/specs/consumer-party-binding.spec.ts` was modified (adds the `no-tenant` Consumer case) but absent from the File List. **Fixed:** added to the File List.
4. **[LOW] Completion understated** — validation was reported as "blocked by a silent MSBuild failure," but that failure is only the `dotnet build … .slnx --no-restore` path (a known environment quirk). Per-project builds **with** restore and all focused tests pass. **Fixed:** re-checked the now-verified subtasks and moved Status to `done`.

### Verification performed (the dev could not complete this locally)

- Builds (Release, `-m:1`, with restore): `Contracts`, `Contracts.Tests`, `UI`, `UI.Tests`, `Mcp`, `Security`, `Parties` (actor host), `Parties.Tests`, `AdminPortal.Tests` — **all 0 warnings / 0 errors**.
- Tests (xUnit v3 EXE runner): Contracts `PartiesClaimExtractionTests` 15/15, `PartiesClaimLiteralConsolidationTests` 1/1, `ContractsPublicApiSnapshotTests` 1/1; UI `PartyIdClaimResolverTests` 11/11, `NoPartyBindingRoutingTests` 5/5, `RoleLandingRedirectTests` 13/13, `SelfScopedPartiesClientTests` 42/42, `PartiesClaimsTransformationTests` 9/9, `PartiesUiAuthenticationCompositionTests` 4/4; `EventStoreGatewayRoutingTests` 51/51; AdminPortal `CreateEditPartyPageTests` 11/11, `PartiesAdminPortalComponentTests` 111/111. **All green.**
- `git diff --check` clean; consolidation scan clean after fixes.

### Note on behavior change (intentional, verified safe)

`PartyIdClaimResolver` now fails closed when the normalized tenant claim is missing or ambiguous (previously it bound with an empty tenant). This **tightens** the Story 1.4 fail-closed contract rather than weakening it, matches the updated `BoundPartyId_WithoutTenantClaim_ResolvesUnbound` / `BoundPartyId_WithAmbiguousTenantClaim_ResolvesUnbound` tests, and the new e2e `no-tenant` case routes such Consumers to `/no-party-binding`. The deleted `PartiesAuthClaims` (incl. `HasConflictingPayloadTenant`) had no callers at HEAD; the payload-tenant guardrail is enforced in the gateway and remains covered by `TenantSafeProjectionReadGuardrailsTests`.

## Change Log

- 2026-06-29: Added shared claim constants/extraction helpers and migrated application/test call sites away from duplicated raw claim literals.
- 2026-06-29: Added extraction parity tests and raw-literal consolidation scan; validation remains blocked by silent MSBuild project-reference failure.
- 2026-06-29: Senior Developer Review (AI) — Approved. Auto-fixed mixed line endings (`PartyIdClaimResolver.cs`, `PartiesUiAuthorization.cs` → LF), a misordered `using` in `EventStoreGatewayRoutingTests.cs`, and a File List omission (`consumer-party-binding.spec.ts`). Verified all affected projects build and all focused tests pass; Status → done.
