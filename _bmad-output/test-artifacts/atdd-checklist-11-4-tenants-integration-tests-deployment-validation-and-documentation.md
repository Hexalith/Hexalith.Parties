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
storyId: '11.4'
storyKey: 11-4-tenants-integration-tests-deployment-validation-and-documentation
storyFile: _bmad-output/implementation-artifacts/11-4-tenants-integration-tests-deployment-validation-and-documentation.md
atddChecklistPath: _bmad-output/test-artifacts/atdd-checklist-11-4-tenants-integration-tests-deployment-validation-and-documentation.md
detectedStack: backend
testFramework: xUnit + Shouldly + NSubstitute
generationMode: ai-generation
generatedTestFiles:
  - tests/Hexalith.Parties.Tests/Authorization/HelperDrivenTenantAccessTests.cs
  - tests/Hexalith.Parties.Tests/Controllers/PartiesControllerTenantAuthorizationTests.cs
  - tests/Hexalith.Parties.Tests/Mcp/McpToolTenantAuthorizationTests.cs
  - tests/Hexalith.Parties.Tests/Controllers/CrossTenantIsolationTests.cs
  - tests/Hexalith.Parties.IntegrationTests/Tenants/TenantsBackedAccessE2ETests.cs
  - tests/Hexalith.Parties.DeployValidation.Tests/TenantsDeploymentValidationTests.cs
inputDocuments:
  - _bmad-output/implementation-artifacts/11-4-tenants-integration-tests-deployment-validation-and-documentation.md
  - _bmad-output/implementation-artifacts/sprint-status.yaml
  - _bmad/tea/config.yaml
  - tests/Hexalith.Parties.Tests/Authorization/TenantAccessServiceTests.cs
  - tests/Hexalith.Parties.Tests/Authorization/TestTenantAccessService.cs
  - tests/Hexalith.Parties.IntegrationTests/Events/TenantIsolationTests.cs
  - tests/Hexalith.Parties.DeployValidation.Tests/DeploymentValidationTests.cs
  - Hexalith.Tenants/src/Hexalith.Tenants.Testing/Helpers/TenantTestHelpers.cs
  - Hexalith.Tenants/src/Hexalith.Tenants.Testing/Projections/InMemoryTenantProjection.cs
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/data-factories.md
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/test-quality.md
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/test-healing-patterns.md
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/test-levels-framework.md
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/test-priorities-matrix.md
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/ci-burn-in.md
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/risk-governance.md
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/probability-impact.md
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/component-tdd.md
---

# ATDD Checklist — Story 11.4

## Step 01 — Preflight & Context

### Stack Detection

- `test_stack_type: auto` → resolved to **backend**
- Indicators: 100+ `*.csproj` files, no Playwright/Vite/React in main project root
- Decision: API/integration generation (no browser recording)

### Prerequisites

- Story status: `ready-for-dev` with 5 ACs and 8 task groups
- Test projects available:
  - `tests/Hexalith.Parties.Tests` (sidecar-free unit/integration)
  - `tests/Hexalith.Parties.IntegrationTests` (Aspire topology, gracefully skips)
  - `tests/Hexalith.Parties.DeployValidation.Tests` (PowerShell harness)
- Hexalith.Tenants.Testing helpers available (submodule already checked out)

### Existing Coverage (avoid duplication)

| Surface | File | Coverage |
|---|---|---|
| TenantAccessService | `Authorization/TenantAccessServiceTests.cs` | role matrix, fail-closed, missing inputs, throwing store |
| Test double | `Authorization/TestTenantAccessService.cs` | controllable handler |
| Tenant event wiring | `Tenants/TenantEventInfrastructureTests.cs` | event handler wiring |
| Publisher tenant scoping | `Events/TenantIsolationTests.cs` | tenant-scoped topic selection |
| Deploy validation (DAPR) | `DeployValidation.Tests/DeploymentValidationTests.cs` | access-control, pubsub, state-store, dead-letter |

### Red-Phase Gaps Targeted by Story 11.4

1. **REST/MCP authorization-before-projection** (AC 2) — observable proof auth runs before reads
2. **Cross-tenant projection isolation via REST/MCP** (AC 2) — tenant A cannot list/search/read/MCP-resolve tenant B data
3. **Tenants subscription deployment validation** (AC 3) — `system.tenants.events`, parties subscriptionScopes, distinct failure categories
4. **JWT-claim-without-membership negative case** (AC 1, 2) — claims identify context only
5. **Eventual-consistency / stale-projection denial** (AC 1, 5) — `tenant-state-stale` reason

## Step 02 — Generation Mode

**Mode chosen:** **AI generation from source code & API documentation**

**Rationale:**

- Backend stack: no browser to record
- ACs are explicit; denial codes, role enum, helper APIs are concrete and source-available
- Existing test patterns (xUnit + Shouldly + NSubstitute, WebApplicationFactory, `DistributedApplicationTestingBuilder`, PowerShell harness) provide template to mirror
- Hexalith.Tenants.Testing helpers (`TenantTestHelpers`, `InMemoryTenantService`, `InMemoryTenantProjection`) are public APIs we can compose

**Recording mode:** N/A (skipped per step-02 backend rule)

## Step 03 — Test Strategy

### 1. Test Surfaces & Levels (Backend Tier Map)

| Tier | Project | Purpose |
|---|---|---|
| **T1 unit** | `tests/Hexalith.Parties.Tests/Authorization` | Fast access decisions w/ helper APIs |
| **T1 controller** | `tests/Hexalith.Parties.Tests/Controllers` | WebApplicationFactory; ProblemDetails + auth-before-projection |
| **T1 MCP** | `tests/Hexalith.Parties.Tests/Mcp` | MCP tool denial paths |
| **T2 integration** | `tests/Hexalith.Parties.IntegrationTests/Tenants` (new) | Full Aspire topology Tenants-backed scenarios; gracefully skip when Docker/Aspire unavailable |
| **Deploy validation** | `tests/Hexalith.Parties.DeployValidation.Tests` | PowerShell script harness w/ temp YAML/JSON fixtures |

### 2. Acceptance-Criteria → Scenario Map (P0/P1)

#### AC1 + AC2 — Fast Authorization w/ Public Tenants Helpers (T1)

> Note: existing `TenantAccessServiceTests.cs` covers role matrix + fail-closed via direct projection store mutation. New tests **must** drive state through `InMemoryTenantService` + `InMemoryTenantProjection` (the public helpers AC1 mandates), NOT by mutating `InMemoryTenantProjectionStore` directly.

| # | Priority | Test Name | Reason Code |
|---|---|---|---|
| 1 | **P0** | `CheckAccessAsync_GivenTenantSeededViaHelpers_AllowsOwnerWrite` | `None` |
| 2 | **P0** | `CheckAccessAsync_GivenJwtTenantClaimButNoMembershipApplied_DeniesAsMissingMember` | `MissingMember` |
| 3 | **P0** | `CheckAccessAsync_AfterUserRemovedFromTenantEventApplied_DeniesAsMissingMember` | `MissingMember` |
| 4 | **P0** | `CheckAccessAsync_AfterTenantDisabledEventApplied_DeniesAsDisabledTenant` | `DisabledTenant` |
| 5 | **P0** | `CheckAccessAsync_AfterUserRoleChangedToReader_DeniesWriteAsInsufficientRole` | `InsufficientRole` |
| 6 | **P1** | `CheckAccessAsync_GivenStaleProjectionStore_DeniesAsTenantStateStale` | `TenantStateStale` |

#### AC2 — REST Auth-Before-Projection (T1, WebApplicationFactory)

> Each test asserts denial happens **before** projection actor / read-model is touched. Use a tracking double on `IPartyProjectionService` (or equivalent read seam) to assert it was never invoked.

| # | Priority | Test Name | Expected |
|---|---|---|---|
| 7 | **P0** | `GetParty_GivenDisabledTenant_Returns403_BeforeReadingProjection` | 403, `reasonCode: tenant-disabled`, projection.Received(0) |
| 8 | **P0** | `GetParty_GivenContributorOnReadEndpoint_Returns200` *(positive control)* | 200 |
| 9 | **P0** | `CreateParty_GivenReader_Returns403_InsufficientRole_BeforeRoutingCommand` | 403, `reasonCode: insufficient-role`, command-bus.Received(0) |
| 10 | **P0** | `GetParty_GivenStaleProjection_Returns403_TenantStateStale` | 403, `reasonCode: tenant-state-stale` |
| 11 | **P1** | `AdminEndpoint_GivenContributor_Returns403_InsufficientRole` | 403, `reasonCode: insufficient-role` |
| 12 | **P1** | `GetParty_DenialResponse_HasProblemJsonAndCorrelationId` | content-type `application/problem+json`, `correlationId` extension |

#### AC2 — MCP Auth-Before-Projection (T1)

| # | Priority | Test Name | Expected |
|---|---|---|---|
| 13 | **P0** | `FindPartiesMcpTool_GivenDisabledTenant_ThrowsWithTenantDisabledCode` | throws InvalidOperationException w/ `tenant-disabled` text |
| 14 | **P0** | `CreatePartyMcpTool_GivenReader_ThrowsInsufficientRole_BeforeIssuingCommand` | throws + command-bus.Received(0) |
| 15 | **P1** | `GetPartyMcpTool_GivenStaleProjection_ThrowsTenantStateStale` | throws w/ `tenant-state-stale` text |

#### AC2 — Cross-Tenant Projection Isolation via REST/MCP (T1/T2)

| # | Priority | Test Name | Expected |
|---|---|---|---|
| 16 | **P0** | `ListParties_TenantAUserContext_DoesNotReturnTenantBRows_EvenWhenSeededInProjection` | result excludes tenant-B parties |
| 17 | **P0** | `GetParty_TenantAUser_FetchingTenantBPartyById_Returns403_NotEnumerable` | 403 (not 404 — both mask existence equivalently in test) |
| 18 | **P0** | `FindPartiesMcpTool_TenantAUser_DoesNotIncludeTenantBHits` | only tenant-A hits |
| 19 | **P1** | `GetPartyMcpTool_TenantAUser_OnTenantBPartyId_ThrowsAccessDenied` | throws w/ tenant denial text |

#### AC2 — Tier 3 Tenants-Backed Topology (skippable)

| # | Priority | Test Name | Expected |
|---|---|---|---|
| 20 | **P1** | `Aspire_GivenTenantsSeededActiveTenant_RestPartiesAccessIsAuthorized` | 200 path through real CommandApi |
| 21 | **P1** | `Aspire_GivenTenantDisabledViaTenantsApi_RestPartiesReturns403` | 403 |
| 22 | **P2** | `Aspire_GivenUserRemovedViaTenantsApi_McpToolThrowsForbidden` | throws |

#### AC3 — Deployment Validation Tenants Checks

| # | Priority | Test Name | Expected |
|---|---|---|---|
| 23 | **P0** | `TenantsSubscription_Missing_FailsWithSpecificError` | exit 1, contains `system.tenants.events` |
| 24 | **P0** | `TenantsSubscriptionScopes_MissingCommandApi_FailsWithRecommendation` | exit 1, contains `parties`, contains `subscriptionScopes` |
| 25 | **P0** | `TenantsConfiguration_Missing_FailsWithRecommendation` | exit 1, contains `Tenants` config marker |
| 26 | **P1** | `TenantsConfiguration_Malformed_FailsWithDistinctCategory` | exit 1, distinct check name vs missing |
| 27 | **P0** | `TenantsValidation_JsonOutput_IncludesTenantsChecksInChecksArray` | valid JSON, `checks[]` includes Tenants entries |
| 28 | **P1** | `TenantsValidation_LocalDev_PassesWithWarnings` | exit 0, output contains `WARN` and Tenants topic name |
| 29 | **P1** | `TenantsValidation_Output_DoesNotLeakSecretsOrPii` | output excludes any token/claim/membership/PII fixtures |

#### AC4 + AC5 — Documentation (deferred from automated ATDD)

> Documentation ACs (getting-started + troubleshooting decision table) are best validated by manual review during PR. Optional automation: a single smoke test asserting required headings exist in `docs/getting-started.md` and `docs/deployment-guide.md` (deferred unless requested).

### 3. Coverage / Duplication Decisions

- ✅ **Reuse**, don't duplicate: `TenantAccessServiceTests` role matrix and fail-closed on direct projection-store mutation.
- ✅ **Add new** value: helper-driven event-sourced setup, REST/MCP denial *before* projection/command, cross-tenant non-enumeration via observable surfaces, deployment validation Tenants checks.
- ⛔ **Out of scope** for ATDD here: re-asserting publisher tenant-scoped topic selection (already in `TenantIsolationTests`), MCP fitness rules (already covered).

### 4. Red-Phase Confirmation

Each test is designed to fail before implementation lands:

- Tests #1–6 fail because no helper-bridging factory exists yet between `InMemoryTenantProjection` events and `ITenantProjectionStore` (Story 11.4 must add this in test fixtures).
- Tests #7–15 fail because the WebApplicationFactory wiring for tracking-double projection/command seams does not exist yet for assertion of `Received(0)`.
- Tests #16–19 fail because no test fixture currently seeds two tenants then issues REST/MCP calls under tenant A's identity.
- Tests #20–22 fail because the IntegrationTests project has no `Tenants/` folder driving Tenants-backed scenarios end-to-end.
- Tests #23–29 fail because `validate-deployment.ps1` has no Tenants-subscription / Tenants-configuration checks; `checks[]` JSON does not yet include them.

### 5. Priority Summary

- **P0:** 16 tests (must pass before merge)
- **P1:** 11 tests (recommended before merge; T3 may be skipped if infrastructure unavailable)
- **P2:** 1 test
- **Deferred:** documentation smoke tests (manual review by default)

## Step 04 + 04C — Red-Phase Test Generation & Aggregation

**Execution mode:** `sequential` (single-agent, in-conversation; subagent JS/TS path skipped because the project is .NET xUnit)

### TDD Red-Phase Compliance

✅ All 29 tests use `[Fact(Skip = ...)]` or `[Theory(Skip = ...)]` — the xUnit equivalent of `test.skip()`. None contains placeholder assertions; each test asserts the *expected* behavior so it fails until the implementation lands.

| File | # Skipped Tests | Scope |
|---|---|---|
| `tests/Hexalith.Parties.Tests/Authorization/HelperDrivenTenantAccessTests.cs` | 6 | AC1, AC2 — fast access decisions via Tenants helpers |
| `tests/Hexalith.Parties.Tests/Controllers/PartiesControllerTenantAuthorizationTests.cs` | 6 | AC2, AC5 — REST auth-before-projection |
| `tests/Hexalith.Parties.Tests/Mcp/McpToolTenantAuthorizationTests.cs` | 3 | AC2 — MCP auth-before-projection |
| `tests/Hexalith.Parties.Tests/Controllers/CrossTenantIsolationTests.cs` | 4 | AC2 — cross-tenant non-enumeration |
| `tests/Hexalith.Parties.IntegrationTests/Tenants/TenantsBackedAccessE2ETests.cs` | 3 | AC2 — Tier 3 Aspire topology (auto-skips when infra unavailable) |
| `tests/Hexalith.Parties.DeployValidation.Tests/TenantsDeploymentValidationTests.cs` | 7 | AC3 — deployment validation Tenants checks |
| **Total** | **29** | |

### Acceptance-Criteria Coverage

- **AC1** Use public Tenants testing helpers / 11.2 seam → covered by `HelperDrivenTenantAccessTests` (drives all setup through `InMemoryTenantService` + `TenantTestHelpers`).
- **AC2** Integration coverage of allowed/disabled/removed/insufficient/cross-tenant → covered by `HelperDrivenTenantAccessTests`, `PartiesControllerTenantAuthorizationTests`, `McpToolTenantAuthorizationTests`, `CrossTenantIsolationTests`, `TenantsBackedAccessE2ETests`.
- **AC3** Deployment validation actionable + secret-safe errors → covered by `TenantsDeploymentValidationTests` (7 tests inc. JSON-output and PII-redaction checks).
- **AC4** Getting-started docs → **deferred to manual PR review** (per Step 03 strategy).
- **AC5** Troubleshooting docs → **deferred to manual PR review**, with REST denial reason-code stability covered by `PartiesControllerTenantAuthorizationTests`.

### Fixture Needs (to be created during green-phase implementation)

Each red-phase test references a `NotImplementedException` helper. Story 11.4 implementation must add:

1. **Tenants→Parties projection bridge** in test helpers — replays `InMemoryTenantProjection` events into an `ITenantProjectionStore` for the access service to consume.
2. **Multi-tenant projection seeder** in `PartiesApiTestFactory` — preloads two tenants' worth of `PartyDetail`/`PartyIndexEntry` rows under separate projection actor proxies.
3. **MCP tool invocation harness** — sidecar-free wrapper that invokes `FindPartiesMcpTool` / `CreatePartyMcpTool` / `GetPartyMcpTool` with `ITenantAccessService` and an `ICommandRouter` tracking double.
4. **Aspire topology Tenants seeder** — uses Hexalith.Tenants client APIs to provision tenant + membership + role through the running CommandApi/Tenants topology, polls until the local projection converges.
5. **Deployment-validation YAML/JSON fixtures** for: missing Tenants subscription, missing parties in subscriptionScopes, missing/malformed Tenants config, valid local-dev config, sensitive-value redaction probe.

### Next Steps (Task-by-Task Activation)

When implementing Story 11.4:

1. Pick the next task from the story; remove the `Skip = ...` argument from the matching test(s).
2. Run the test → verify **RED** (fails as expected because helper bridge / fixture is not yet implemented).
3. Implement the helper or production code.
4. Run again → verify **GREEN**.
5. Refactor; commit.
6. Repeat until all 29 tests pass.

### Implementation Guidance Snapshot

REST endpoints exercised: `/api/v1/parties` (POST/GET/list), `/api/v1/admin/keys/status`. MCP tools: `FindParties`, `CreateParty`, `GetParty`. Deployment script: `deploy/validate-deployment.ps1` (extend with Tenants checks; preserve exit codes 0/1/2 and JSON `checks[]` shape).

### Performance Report

- Mode: sequential
- Generation duration: ~single-agent, in-conversation (no parallel speedup expected with .NET xUnit subagent absent)

## Step 05 — Validate & Complete

### Checklist Validation

The default `checklist.md` is JS/Playwright-oriented; the items below are mapped to the actual .NET xUnit deliverables for this story.

**Prerequisites**

- ✅ Story has clear acceptance criteria (5 ACs, all testable)
- ✅ Test framework available (xUnit + Shouldly + NSubstitute via Hexalith.Parties.slnx)
- ⏭️ N/A: Playwright/Cypress configs (backend stack)

**Step 1 — Story Context**

- ✅ Story file loaded; ACs extracted
- ✅ Affected systems identified: CommandApi, IntegrationTests, DeployValidation script
- ✅ Knowledge fragments loaded (data-factories, test-quality, test-levels-framework, test-priorities-matrix, ci-burn-in, risk-governance, probability-impact, component-tdd, test-healing-patterns)
- ⏭️ N/A: fixture-architecture / network-first (Playwright-specific)

**Step 2 — Test Level Selection**

- ✅ Each AC analyzed for level
- ✅ Backend levels applied: Unit (T1), Integration via WebApplicationFactory (T1), Aspire topology (T2/T3), Deploy validation
- ⏭️ N/A: E2E browser, Component-mounting tests
- ✅ Duplicate coverage avoided (existing `TenantAccessServiceTests`, `TenantIsolationTests`, `DeploymentValidationTests` not re-asserted)
- ✅ P0–P3 priorities assigned (16 P0 / 11 P1 / 1 P2 / docs deferred)

**Step 3 — Red-Phase Scaffolds**

- ✅ All 29 tests use `[Fact(Skip = …)]` / `[Theory(Skip = …)]` (xUnit equivalent of `test.skip()`)
- ✅ Tests asserts EXPECTED behavior (no `expect(true).toBe(true)` placeholders); each helper that lacks an implementation throws `NotImplementedException` with a story-tagged message
- ✅ Given/When/Then structure via `// Arrange / Act / Assert` xUnit convention
- ✅ Activation guidance documented (remove `Skip = …` argument, run, verify RED, implement, verify GREEN)
- ✅ One observable assertion per test (multi-assert reserved for ProblemDetails contract verification, which represents a single observable contract)

**Step 4 — Data Infrastructure**

- ⏭️ N/A: `@faker-js/faker` factories (xUnit project does not require — uses `Guid.NewGuid()` and Hexalith.Tenants.Testing helpers)
- ⏭️ N/A: `test.extend()` Playwright fixtures (xUnit `IClassFixture` already in use via `PartiesApiTestFactory` and `PartiesAspireTopologyFixture`)
- ✅ Fixture needs documented in this checklist (Tenants→Parties projection bridge, multi-tenant projection seeder, MCP invocation harness, Aspire topology Tenants seeder, deploy-validation YAML/JSON fixtures)

**Step 5 — Implementation Checklist**

- ✅ Each test mapped to a concrete production/test surface (production code + helper bridge to be added by story 11.4)
- ✅ Red-Green-Refactor workflow documented above
- ✅ RED phase complete (TEA responsibility — this skill)
- ✅ GREEN phase tasks listed (implementation must remove `Skip` per task and add helpers)
- ✅ Execution commands provided in story 11.4 task list (the four `dotnet test` / `dotnet build` commands)

**Step 6 — Deliverables**

- ✅ ATDD checklist saved to `_bmad-output/test-artifacts/atdd-checklist-11-4-tenants-integration-tests-deployment-validation-and-documentation.md`
- ✅ Frontmatter populated (`storyId`, `storyKey`, `storyFile`, `atddChecklistPath`, `generatedTestFiles`)
- ✅ Story file linked back: `_bmad-output/implementation-artifacts/11-4-…md` now contains an `### ATDD Artifacts` block listing the checklist + 6 generated test files

**Quality Checks**

- ✅ Tests are readable (xUnit Arrange/Act/Assert + descriptive names)
- ✅ Tests are isolated (each instantiates its own fixtures; no shared state)
- ✅ Tests are deterministic (sidecar-free at T1, infra-skip at T3)
- ✅ Code follows Hexalith style (file-scoped namespaces, sealed test classes, consistent imports)
- ✅ No CLI sessions or temp browsers opened (backend stack)
- ✅ Temp artifacts only in `_bmad-output/test-artifacts/` (no random locations)

### Risks & Assumptions

1. **Helper bridge is the single largest unknown.** AC1 mandates use of `Hexalith.Tenants.Testing` helpers; the bridge from `InMemoryTenantProjection`/`InMemoryTenantService` events to the Parties `ITenantProjectionStore` (the seam consumed by `TenantAccessService`) does not yet exist. Implementation must add a sidecar-free fixture for this — the red-phase tests will surface that gap on first activation.
2. **`PartiesApiTestFactory` extension required for cross-tenant seeding.** Today the factory mocks a single set of projection actors; the cross-tenant tests assume a multi-tenant projection seeder. Likely a small extension method that scopes proxies per tenant.
3. **MCP test harness pattern is new.** No prior MCP tool test currently asserts auth-before-routing with NSubstitute tracking doubles. The `ICommandRouterDouble` placeholder interface in `McpToolTenantAuthorizationTests.cs` should be replaced with the real `ICommandRouter` substitute once the wiring is decided.
4. **PowerShell script changes will need careful exit-code preservation.** Existing tests assert exit codes 0/1/2; adding new failure categories must not collapse them into a single bucket.
5. **AC4 + AC5 documentation tests deferred.** No automated assertion exists for getting-started + troubleshooting docs. PR review must catch deviations.

### Next Recommended Workflow

→ **`bmad-bmm-dev-story` (or your preferred dev workflow) on story `11-4-tenants-integration-tests-deployment-validation-and-documentation`**

Activate one test family at a time:

1. Strip `Skip = SkipReason` from test #N (start with `HelperDrivenTenantAccessTests` — easiest helper bridge).
2. Run `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --filter "FullyQualifiedName~HelperDrivenTenantAccessTests"`.
3. Confirm RED (NotImplementedException from helper or assertion failure).
4. Implement the helper / production change.
5. Confirm GREEN.
6. Repeat for the next file in this order: `PartiesControllerTenantAuthorizationTests` → `McpToolTenantAuthorizationTests` → `CrossTenantIsolationTests` → `TenantsDeploymentValidationTests` → `TenantsBackedAccessE2ETests` (last; needs Aspire topology).

After GREEN: run `automate` workflow to expand coverage, then code-review.
