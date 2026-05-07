---
stepsCompleted:
  - step-01-preflight-and-context
  - step-02-generation-mode
  - step-03-test-strategy
  - step-04-generate-tests
  - step-04c-aggregate
  - step-05-validate-and-complete
lastStep: step-05-validate-and-complete
lastSaved: 2026-05-05
storyId: '10.1'
storyKey: 10-1-admin-portal-browse-search-and-inspect
storyFile: _bmad-output/implementation-artifacts/10-1-admin-portal-browse-search-and-inspect.md
atddChecklistPath: _bmad-output/test-artifacts/atdd-checklist-10-1-admin-portal-browse-search-and-inspect.md
detectedStack: fullstack
testFramework: xUnit + Shouldly + NSubstitute (bUnit deferred to green phase)
generationMode: ai-generation
generatedTestFiles:
  - tests/Hexalith.Parties.Client.Tests/AdminPortal/AdminPortalQueryContractTests.cs
  - tests/Hexalith.Parties.Contracts.Tests/AdminPortal/AdminPortalReadOnlySurfaceTests.cs
  - tests/Hexalith.Parties.Contracts.Tests/AdminPortal/AdminPortalXssGuardrailTests.cs
  - tests/Hexalith.Parties.Contracts.Tests/AdminPortal/AdminPortalAuthorizationStateTests.cs
inputDocuments:
  - _bmad-output/implementation-artifacts/10-1-admin-portal-browse-search-and-inspect.md
  - _bmad/tea/config.yaml
  - src/Hexalith.Parties/Controllers/PartiesController.cs
  - src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs
  - src/Hexalith.Parties.Client/Abstractions/IPartiesQueryClient.cs
  - src/Hexalith.Parties.Contracts/Models/PartyDetail.cs
  - src/Hexalith.Parties.Contracts/Models/PartyIndexEntry.cs
  - src/Hexalith.Parties.Contracts/Models/PagedResult.cs
  - src/Hexalith.Parties.Contracts/Models/PartySearchResult.cs
  - tests/Hexalith.Parties.Client.Tests/HttpPartiesQueryClientTests.cs
  - tests/Hexalith.Parties.Client.Tests/HttpPartiesCommandClientTests.cs
  - Hexalith.FrontComposer/tests/Hexalith.FrontComposer.Shell.Tests/FrontComposerTestBase.cs
  - Hexalith.FrontComposer/tests/Hexalith.FrontComposer.Shell.Tests/Hexalith.FrontComposer.Shell.Tests.csproj
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/data-factories.md
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/component-tdd.md
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/test-quality.md
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/test-healing-patterns.md
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/selector-resilience.md
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/timing-debugging.md
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/test-levels-framework.md
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/test-priorities-matrix.md
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/risk-governance.md
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/probability-impact.md
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/error-handling.md
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/ci-burn-in.md
---

# ATDD Checklist — Story 10.1: Admin Portal Browse, Search & Inspect

## Step 01 — Preflight & Context

### Stack Detection

- `test_stack_type: auto` → resolved to **fullstack** (.NET 10 backend + Hexalith.FrontComposer Blazor/Fluent UI submodule).
- Indicators: `Hexalith.Parties.slnx`, ~10 `*.csproj` projects, `Hexalith.FrontComposer/tests/e2e/playwright.config.ts`, `Hexalith.FrontComposer/tests/Hexalith.FrontComposer.Shell.Tests` already on bUnit + xUnit v3.
- Decision: AI generation (no recording — story is read-only, the public REST surface is already documented in `PartiesController`).

### Prerequisites

- Story status: `ready-for-dev` with 8 acceptance criteria + Party-Mode Clarifications + UX State Matrix.
- Test projects in scope:
  - `tests/Hexalith.Parties.Client.Tests` — xUnit + Shouldly + NSubstitute, hosts the wire-level transport scaffolds.
  - `tests/Hexalith.Parties.Contracts.Tests` — xUnit + Shouldly + NSubstitute, hosts reflective architectural fitness scaffolds (no AspNetCore.App framework reference required).
- `Hexalith.Parties.AdminPortal` source project and its dedicated bUnit test project (`tests/Hexalith.Parties.AdminPortal.Tests`) **do not yet exist**; the dev story creates them in green phase. Reflective scaffolds are wired so they activate the moment that assembly ships.

### TEA Configuration Snapshot

- `tea_use_playwright_utils: true` → API-only profile (no `page.goto` usage in `tests/`).
- `tea_use_pactjs_utils: true` → out of scope (read-only portal does not introduce new contract boundaries).
- `tea_pact_mcp: mcp` → out of scope for this story.
- `tea_browser_automation: auto` → defers to green-phase bUnit when AdminPortal lands.
- `test_framework: auto` → resolved to xUnit v3 (matching FrontComposer.Shell.Tests + Story 11.4 precedent).

### Persistent Facts

- No `project-context.md` files exist anywhere in the repo (Glob `**/project-context.md` empty). Story dev notes call this out explicitly.

---

## Step 02 — Generation Mode

**Mode:** AI generation. No browser recording.

- Story 10.1 is read-only and reuses the documented `GET /api/v1/parties`, `GET /api/v1/parties/search`, `GET /api/v1/parties/{id}` endpoints. All assertions can be derived from `PartiesController`, `IPartiesQueryClient`, the story's UX State Matrix, and the FrontComposer testing conventions already in the repository.
- The new `Hexalith.Parties.AdminPortal` Blazor project is created during green phase; recording UI before the components exist would force speculative selectors. Reflective fitness scaffolds defer concrete render assertions to bUnit work the dev pairs with the new components.

---

## Step 03 — Test Strategy

### Acceptance Criteria → Scenario → Level → Priority

| AC | Scenario | Level | Priority | File |
|----|----------|-------|----------|------|
| AC1 | Authenticated admin browses paginated list | Wire (transport contract) | P0 | `AdminPortalQueryContractTests` |
| AC1, AC5, AC8 | `PartyBrowsePage` + `PartyDetailView` Blazor components exist and inherit `ComponentBase` | Architecture (reflection) | P0 | `AdminPortalReadOnlySurfaceTests` |
| AC2 | Empty search query falls back to baseline list | Wire | P0 | `AdminPortalQueryContractTests` |
| AC3 | Rich-search-unavailable path keeps display-name-only behavior, no client-side emulation | Wire | P0 | `AdminPortalQueryContractTests` |
| AC4 | Page-size cap of 100 enforced before request leaves the client | Wire | P1 | `AdminPortalQueryContractTests` |
| AC4, AC7 | Cross-tenant scoped id resolves to forbidden state without leaking ProblemDetails | Wire | P0 | `AdminPortalQueryContractTests` |
| AC5 | Detail hydration gone state surfaces typed exception | Wire | P0 | `AdminPortalQueryContractTests` |
| AC6 | No `MarkupString` fields/properties in portal types | Architecture (reflection) | P0 | `AdminPortalXssGuardrailTests` |
| AC6 | `BuildRenderTree` IL contains no `AddMarkupContent` invocations | Architecture (IL scan) | P0 | `AdminPortalXssGuardrailTests` |
| AC6 | No JS interop literals like `innerHTML` / `eval` / `setHtmlUnsafe` | Architecture (literal scan) | P1 | `AdminPortalXssGuardrailTests` |
| AC7 | Tenant switch cancels in-flight list request | Wire | P0 | `AdminPortalQueryContractTests` |
| AC7 | 401 fails closed with no cached rows | Wire | P0 | `AdminPortalQueryContractTests` |
| AC7 | Distinguishable list states (`MissingToken`, `MissingTenant`, `Forbidden`, …) | Architecture (enum surface) | P0 | `AdminPortalAuthorizationStateTests` |
| AC7 | `PartiesAdminListCoordinator.ResetForTenantSwitch` exists | Architecture (method surface) | P1 | `AdminPortalAuthorizationStateTests` |
| AC7 | `AdminPortalPartyQueryService` is `IDisposable`/`IAsyncDisposable` so scoped disposal drops cached state | Architecture | P1 | `AdminPortalAuthorizationStateTests` |
| AC7 | No `JwtTenantClaimParser`-style local authority resolver — Tenants is the source of truth | Architecture | P1 | `AdminPortalAuthorizationStateTests` |
| AC8 | AdminPortal references `Hexalith.FrontComposer.Shell` | Architecture | P0 | `AdminPortalReadOnlySurfaceTests` |
| AC8 | No SPA artifacts (`package.json`, `vite.config.*`, etc.) ship next to the assembly | Architecture | P1 | `AdminPortalReadOnlySurfaceTests` |
| AC8 + non-goals | No mutation/GDPR-named components (Stories 10.2/10.3 own those) | Architecture | P0 | `AdminPortalReadOnlySurfaceTests` |
| AC8 + non-goals | No tenant-management components (Hexalith.Tenants owns those) | Architecture | P0 | `AdminPortalReadOnlySurfaceTests` |
| AC1 + non-goals | All routes scoped under `/admin/parties` | Architecture (RouteAttribute reflection) | P1 | `AdminPortalReadOnlySurfaceTests` |

### Levels Deferred to Green Phase

- **bUnit component tests** — render assertions for browse grid, search box, filters panel, detail layout, accessibility/focus, and localization. Defer to green phase: add a new `tests/Hexalith.Parties.AdminPortal.Tests` project alongside `src/Hexalith.Parties.AdminPortal`, mirroring `Hexalith.FrontComposer.Shell.Tests` conventions (bUnit + Fluxor + NSubstitute + Verify). Story tasks already enumerate the bUnit coverage required.
- **Aspire integration smoke** — the existing `tests/Hexalith.Parties.IntegrationTests` topology can be extended after green to assert `/admin/parties` is reachable end-to-end with admin authentication; gate behind infra prerequisites already enforced by the integration test fixture.
- **No new backend tests** — story is read-only. Existing controller coverage in `tests/Hexalith.Parties.Tests` already pins endpoint behavior.

### Red Phase Confirmation

Every generated test is gated by `[Fact(Skip = "TDD red phase — …")]`. They compile, skip cleanly under `dotnet test`, and assert real expected behavior (no placeholder `Assert.True(true)` patterns). Activation guidance is embedded in each test body.

---

## Step 04 — Red-Phase Test Generation

### Generated Files

| File | Tests | Purpose |
|------|------:|---------|
| `tests/Hexalith.Parties.Client.Tests/AdminPortal/AdminPortalQueryContractTests.cs` | 8 | Transport-level contract pinning AC1–AC5/AC7 wire behavior the FrontComposer-hosted portal must rely on. |
| `tests/Hexalith.Parties.Contracts.Tests/AdminPortal/AdminPortalReadOnlySurfaceTests.cs` | 6 | Architectural fitness for AC8 + read-only non-goals (no SPA, no tenant-management UX, no mutation/GDPR components, scoped routes). |
| `tests/Hexalith.Parties.Contracts.Tests/AdminPortal/AdminPortalXssGuardrailTests.cs` | 3 | Reflection + IL scan fitness for AC6 (no `MarkupString`, no `AddMarkupContent`, no JS interop bridges to raw HTML). |
| `tests/Hexalith.Parties.Contracts.Tests/AdminPortal/AdminPortalAuthorizationStateTests.cs` | 4 | AC4/AC7 fitness for distinguishable list states, tenant-switch reset hook, scoped query service disposability, no local JWT-claim authority resolution. |

**Total:** 21 skipped tests across two existing test projects. No build pollution, no `.slnx` edits, both target projects compile clean (`dotnet build … --configuration Release` returns 0 warnings / 0 errors).

### Verification

```
dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj \
    --configuration Release --no-build \
    --filter "FullyQualifiedName~AdminPortalQueryContractTests"
# Skipped! 8 / 8

dotnet test tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj \
    --configuration Release --no-build \
    --filter "FullyQualifiedName~AdminPortal"
# Skipped! 13 / 13
```

### Fixtures

No new fixture files needed for the red phase. Existing `HttpPartiesCommandClientTests.MockHandler` is reused for transport scaffolds; reflection-based fitness tests are self-contained.

---

## Acceptance Criteria Coverage

- **AC1** — `AdminPortalQueryContractTests.ListPartiesAsync_WhenTenantSwitchCancelsToken_PropagatesCancellationAsync`, `AdminPortalReadOnlySurfaceTests.AdminPortal_DefinesRequiredBrowseAndDetailComponents`, `AdminPortalReadOnlySurfaceTests.AdminPortal_RoutesAreScopedToAdminPartiesPath`
- **AC2** — `AdminPortalQueryContractTests.SearchPartiesAsync_EmptyQuery_FallsBackToBaselineListAsync`
- **AC3** — `AdminPortalQueryContractTests.SearchPartiesAsync_RichSearchUnavailable_KeepsDisplayNameOnlyAsync`, `AdminPortalQueryContractTests.ListPartiesAsync_WhenResponseHasDegradedHeaders_SurfacesThemToCallerAsync`
- **AC4** — `AdminPortalQueryContractTests.ListPartiesAsync_PageSizeAboveServerCap_ClientClampsTo100Async`, `AdminPortalQueryContractTests.GetPartyAsync_OnForbidden_DoesNotLeakRawProblemDetailsToCallerAsync`
- **AC5** — `AdminPortalQueryContractTests.GetPartyAsync_OnGoneResponse_RaisesGoneOutcomeAsync`, `AdminPortalReadOnlySurfaceTests.AdminPortal_DefinesRequiredBrowseAndDetailComponents`
- **AC6** — `AdminPortalXssGuardrailTests.AdminPortal_DoesNotDeclareMarkupStringFields`, `AdminPortalXssGuardrailTests.AdminPortal_DoesNotInvokeAddMarkupContentInBuildRenderTree`, `AdminPortalXssGuardrailTests.AdminPortal_HasNoJsInteropBridgeForRawPartyHtml`
- **AC7** — `AdminPortalQueryContractTests.ListPartiesAsync_OnUnauthorized_FailsClosedWithoutCachedRowsAsync`, `AdminPortalQueryContractTests.GetPartyAsync_OnForbidden_DoesNotLeakRawProblemDetailsToCallerAsync`, `AdminPortalAuthorizationStateTests.AdminPortal_DefinesDistinguishableAuthorizationStates`, `AdminPortalAuthorizationStateTests.AdminPortal_DefinesTenantSwitchResetHook`, `AdminPortalAuthorizationStateTests.AdminPortal_DefinesScopedQueryServiceFailingClosed`, `AdminPortalAuthorizationStateTests.AdminPortal_DoesNotInferAuthorizationFromJwtTenantClaim`
- **AC8** — `AdminPortalReadOnlySurfaceTests.AdminPortal_AssemblyExists_AndReferencesFrontComposerShell`, `AdminPortalReadOnlySurfaceTests.AdminPortal_DoesNotShipParallelSpaArtifacts`, `AdminPortalReadOnlySurfaceTests.AdminPortal_DoesNotExposeMutationOrGdprComponents`, `AdminPortalReadOnlySurfaceTests.AdminPortal_DoesNotDuplicateTenantManagementSurface`

---

## Activation Guidance (Green Phase)

1. **Create the AdminPortal source project** — `src/Hexalith.Parties.AdminPortal` (Razor Class Library, `Microsoft.NET.Sdk.Razor`, `net10.0`), reference `Hexalith.FrontComposer.Shell` + `Hexalith.Parties.Client` + `Hexalith.Parties.Contracts`. Add to `Hexalith.Parties.slnx`.
2. **Wire AC8 fitness** — once the project compiles, drop `Skip = SkipReason` on `AdminPortalReadOnlySurfaceTests` and iterate component scaffolding (`PartyBrowsePage`, `PartyDetailView`, …) until the reflective checks turn green.
3. **Wire AC6 guardrails** — keep all party rendering on plain `@value` Razor binding. Drop `Skip` on `AdminPortalXssGuardrailTests` after the first round of components ship.
4. **Wire AC4/AC7 coordinator** — implement `AdminPortalListState` enum + `PartiesAdminListCoordinator.ResetForTenantSwitch` + scoped `AdminPortalPartyQueryService`. Drop `Skip` on `AdminPortalAuthorizationStateTests`.
5. **Wire AC1–AC5/AC7 transport** — extend `IPartiesQueryClient` (or layer `IAdminPortalPartyQueryService` over it) so degraded headers/disposable scope/cancellation propagation are observable. Drop `Skip` on `AdminPortalQueryContractTests` test by test as each behavior lands.
6. **Add bUnit project** — `tests/Hexalith.Parties.AdminPortal.Tests` (mirror `Hexalith.FrontComposer.Shell.Tests` package set: `bunit` + `xunit.v3` + `Shouldly` + `NSubstitute` + `Verify`). Author component-level tests for browse grid, filters, detail rendering, focus/accessibility, and localization per the story's task list.
7. **Run affected tests on every activation:**
   ```
   dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj --configuration Release
   dotnet test tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj --configuration Release
   ```

## Implementation Notes

- **No `.slnx` edits performed** — both target projects already exist; only `.cs` files were added. The dev introduces the AdminPortal source/test projects to `Hexalith.Parties.slnx` during green phase.
- **No backend test additions** — story is read-only and reuses `PartiesController.ListPartiesAsync`, `SearchPartiesAsync`, `GetPartyAsync` exactly as authored. Existing CommandApi.Tests + IntegrationTests cover the controller surface; do not duplicate.
- **Reflection-only architecture tests** — `Hexalith.Parties.Contracts.Tests.csproj` deliberately stays free of `Microsoft.AspNetCore.App` framework references. AdminPortal architecture assertions resolve `RouteAttribute` and `ComponentBase` by full type name through the loaded portal assembly's transitive references.
- **MockHandler reuse** — transport scaffolds reuse `HttpPartiesCommandClientTests.MockHandler` (internal in the same assembly). Activation will likely need to extend the handler with response headers; the relevant test body marks that hook explicitly.
- **PartiesClientException property naming** — fixed during scaffold authoring: the property is `Status` (int), not `StatusCode`. Activation can rely on this without further edits.

---

## Step 05 — Validate & Complete

### Validation Results

- Prerequisites remain satisfied: story `10.1` is `ready-for-dev` and contains eight testable acceptance criteria.
- Generated files are present and match the checklist frontmatter:
  - `tests/Hexalith.Parties.Client.Tests/AdminPortal/AdminPortalQueryContractTests.cs`
  - `tests/Hexalith.Parties.Contracts.Tests/AdminPortal/AdminPortalReadOnlySurfaceTests.cs`
  - `tests/Hexalith.Parties.Contracts.Tests/AdminPortal/AdminPortalXssGuardrailTests.cs`
  - `tests/Hexalith.Parties.Contracts.Tests/AdminPortal/AdminPortalAuthorizationStateTests.cs`
- Red-phase compliance confirmed: all 21 generated tests use `[Fact(Skip = SkipReason)]`; no placeholder `Assert.True(true)` scaffolds were found.
- Acceptance criteria coverage remains mapped for AC1 through AC8 in the checklist.
- Story metadata and downstream handoff paths are captured in frontmatter and in the story's `### ATDD Artifacts` section.
- CLI/browser cleanup: N/A. No browser or Playwright recording session was started for this validation pass.
- Temp artifacts: N/A for this validation pass. Existing ATDD outputs are stored under `_bmad-output/test-artifacts/`.

### Verification Commands

```powershell
dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj --configuration Release --filter "FullyQualifiedName~AdminPortalQueryContractTests"
# Skipped: 8 / 8, Failed: 0

dotnet test tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj --configuration Release --filter "FullyQualifiedName~AdminPortal"
# Skipped: 13 / 13, Failed: 0
```

Note: the contracts verification was rerun in isolation after a parallel build attempt hit a transient compiler file lock on `Hexalith.Parties.Contracts.dll`.

### Completion Summary

- Total red-phase scaffolds: 21 skipped tests.
- Primary handoff: activate tests incrementally during `dev-story` for Story 10.1.
- Key assumption: the green-phase implementation creates `src/Hexalith.Parties.AdminPortal` as a FrontComposer-hosted Blazor/Razor Class Library rather than a separate SPA.
- Next recommended workflow: `bmad-dev-story 10-1-admin-portal-browse-search-and-inspect`.
