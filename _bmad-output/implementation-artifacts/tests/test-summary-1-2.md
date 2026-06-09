# Test Automation Summary — Story 1.2 (Host-owned OIDC sign-in)

**Workflow:** `bmad-qa-generate-e2e-tests` · **Date:** 2026-06-10 · **Engineer:** QA automation (Administrator)
**Story:** `_bmad-output/implementation-artifacts/1-2-host-owned-oidc-sign-in-server-side-tokens-never-reach-the-browser.md`
**Feature:** AR-D5 host-owned OIDC sign-in — server-side cookie session, tokens never reach the browser.
**Framework (existing, reused):** xUnit v3 (3.2.2) · Shouldly · Aspire.Hosting.Testing — DI-composition / model-inspection / config-validation lanes.
**Mode:** Auto-apply all discovered gaps.

## Context

The `parties-ui` host has **no public API** (gateway boundary), so the "end-to-end" mechanism is
exercised at the composition + topology + realm-config seams, not via Playwright/running-host. The
live browser flow itself was already manually verified in the story's Dev Agent Record
(`/authentication/challenge` → Keycloak → `/signin-oidc` → HttpOnly cookie, no JS-readable token).
This run added **automated regression guards** for the parts that were unit-testable but uncovered.

## Gaps Discovered → Closed

The dev shipped 3 tests (positive auth composition, negative degrade-gracefully, topology OIDC-env).
Gaps filled this run — all auto-applied, all green:

| # | AC / Task | Gap in existing tests | Fix |
|---|---|---|---|
| 1 | AC1 | Authorization-code flow shape (ResponseType / callbacks / scopes) never asserted — only `SaveTokens`/`SignInScheme` were. | New test asserts `ResponseType=code`, `CallbackPath=/signin-oidc`, `SignedOutCallbackPath=/signout-callback-oidc`, scopes `openid`+`profile`. |
| 2 | AC1 / AC2 | **Cookie `HttpOnly`** — the Dev Notes name it an explicit AC1 guard ("SaveTokens + cookie HttpOnly") but it was only verified live; AC2 sign-out path also untested. | New test asserts cookie `HttpOnly`, `SameSite=Lax`, `SecurePolicy=SameAsRequest`, no sliding-expiry, `LoginPath=/authentication/challenge`, `LogoutPath=/authentication/sign-out`. |
| 3 | AC3 | No automated secret-hygiene guard on committed `appsettings*.json`. | New tests: empty OIDC scaffold (env-overridable shape) + dev-secret leak sweep across `appsettings*.json`. |
| 4 | AC3 | OIDC `Audience` env on `parties-ui` not asserted. | Added `Authentication__OpenIdConnect__Audience == hexalith-eventstore` to the topology test. |
| 5 | Task 4 | The confidential `hexalith-parties-ui` Keycloak client (load-bearing for sign-in) had **zero** tests. | New realm-validation tests: confidential client shape + redirectUri `https://localhost:7210/signin-oidc` + eventstore audience mapper. |

## Generated / Modified Tests

### Composition / unit — `tests/Hexalith.Parties.UI.Tests/`
- [x] `PartiesUiAuthenticationCompositionTests.cs` (edit) — +2 tests:
  - `AuthBridge_WhenOidcConfigured_UsesAuthorizationCodeFlowWithStandardCallbacksAndScopes`
  - `AuthBridge_WhenOidcConfigured_IssuesHttpOnlyServerSideCookieWithChallengeAndSignOutPaths`
  - (+ shared `BuildConfiguredAuthBridgeProvider` helper.)
- [x] `PartiesUiOidcConfigurationTests.cs` (new) — `AppSettings_DeclaresEmptyOidcScaffold_SoTheOverrideShapeIsDiscoverable`, `NoCommittedAppSettingsFile_ContainsTheDevClientSecret`.

### Topology / config — `tests/Hexalith.Parties.IntegrationTests/Topology/`
- [x] `PartiesUiTopologyTests.cs` (edit) — OIDC `Audience` env assertion added to `PartiesUiResource_CarriesOidcEnv_AndNoJwtBearer` (model-inspection, no Docker).
- [x] `PartiesUiKeycloakRealmTests.cs` (new) — `Realm_DeclaresConfidentialPartiesUiClient_ForAuthorizationCodeFlow`, `PartiesUiClient_MapsTheEventStoreAudience_ForDownstreamClaims`.

## Results

| Suite | Total | Passed | Skipped | Failed |
|---|---|---|---|---|
| `Hexalith.Parties.UI.Tests` | 10 | 10 | 0 | 0 |
| `Hexalith.Parties.IntegrationTests` | 28 | 22 | 6 (Docker-dependent, graceful-skip by design) | 0 |

- Build: per-project Release `-m:1` → **0 warnings, 0 errors** under solution-wide `TreatWarningsAsErrors`.
- Build gate: `scripts/check-no-warning-override.sh` → **OK**. No `Version=` added (CPM; no new `PackageReference`).

## Coverage

- **AC1** (code flow / server-side cookie / tokens-never-reach-browser / return-URL path): `SaveTokens` (pre-existing) **+ ResponseType=code, callbacks, scopes, cookie HttpOnly + secure posture + challenge path** (new). ✅
- **AC2** (sign-out + endpoint surface): no-JwtBearer (pre-existing) **+ cookie LogoutPath + OIDC SignedOutCallbackPath** (new). "No public command/query API" remains live-verified (manual flow). ✅
- **AC3** (no committed secret; env-overridable): OIDC env ClientId/Authority (pre-existing) **+ Audience env + empty-scaffold + secret-leak sweep** (new). ✅
- **Task 4** (Keycloak confidential client): previously untested **→ now guarded**. ✅
- **API tests:** N/A — the BFF host exposes no public API (public surface is the EventStore gateway, other suites).
- **Browser/page E2E:** not added — host renders no routable page yet (`GET /` → 404 per Story 1.1); the live sign-in flow is manually verified in the Dev Agent Record.

## Next Steps

- Run in CI: `scripts/test.ps1 -Lane unit` and `-Lane topology` (integration health tests skip gracefully without Docker).
- A full host-up endpoint-surface assertion (`/api/v1/commands` → 404 via `WebApplicationFactory`) is deferred — covered today by the manual live verification + the topology no-JwtBearer guard. Add it when a routable page lands (Story 1.3).
