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
storyId: '10.2'
storyKey: 10-2-admin-portal-gdpr-operations
storyFile: _bmad-output/implementation-artifacts/10-2-admin-portal-gdpr-operations.md
atddChecklistPath: _bmad-output/test-artifacts/atdd-checklist-10-2-admin-portal-gdpr-operations.md
detectedStack: fullstack
testFramework: xUnit + Shouldly + reflection-based portal fitness scaffolds (bUnit deferred to green phase)
generationMode: ai-generation
generatedTestFiles:
  - tests/Hexalith.Parties.Client.Tests/AdminPortal/AdminPortalGdprOperationContractTests.cs
  - tests/Hexalith.Parties.Contracts.Tests/AdminPortal/AdminPortalGdprSurfaceTests.cs
  - tests/Hexalith.Parties.Contracts.Tests/AdminPortal/AdminPortalGdprAuthorizationStateTests.cs
  - tests/Hexalith.Parties.Contracts.Tests/AdminPortal/AdminPortalGdprPrivacyGuardrailTests.cs
inputDocuments:
  - _bmad-output/implementation-artifacts/10-2-admin-portal-gdpr-operations.md
  - _bmad-output/test-artifacts/atdd-checklist-10-1-admin-portal-browse-search-and-inspect.md
  - _bmad/tea/config.yaml
  - src/Hexalith.Parties.CommandApi/Controllers/AdminController.cs
  - src/Hexalith.Parties.CommandApi/Controllers/PartiesController.cs
  - src/Hexalith.Parties.Contracts/Models/PartyDetail.cs
  - src/Hexalith.Parties.Contracts/ValueObjects/ConsentRecord.cs
  - src/Hexalith.Parties.Contracts/Models/ProcessingActivityRecord.cs
  - tests/Hexalith.Parties.Client.Tests/AdminPortal/AdminPortalQueryContractTests.cs
  - tests/Hexalith.Parties.Contracts.Tests/AdminPortal/AdminPortalReadOnlySurfaceTests.cs
  - tests/Hexalith.Parties.Contracts.Tests/AdminPortal/AdminPortalXssGuardrailTests.cs
  - tests/Hexalith.Parties.Contracts.Tests/AdminPortal/AdminPortalAuthorizationStateTests.cs
  - tests/Hexalith.Parties.CommandApi.Tests/Controllers/ErasureEndpointTests.cs
  - tests/Hexalith.Parties.CommandApi.Tests/Controllers/ConsentEndpointTests.cs
  - tests/Hexalith.Parties.CommandApi.Tests/Controllers/RestrictionEndpointTests.cs
  - tests/Hexalith.Parties.CommandApi.Tests/Controllers/PortabilityEndpointTests.cs
  - .agents/skills/bmad-testarch-atdd/resources/knowledge/data-factories.md
  - .agents/skills/bmad-testarch-atdd/resources/knowledge/component-tdd.md
  - .agents/skills/bmad-testarch-atdd/resources/knowledge/test-quality.md
  - .agents/skills/bmad-testarch-atdd/resources/knowledge/selector-resilience.md
  - .agents/skills/bmad-testarch-atdd/resources/knowledge/test-healing-patterns.md
  - .agents/skills/bmad-testarch-atdd/resources/knowledge/timing-debugging.md
---

# ATDD Checklist — Story 10.2: Admin Portal GDPR Operations

## Step 01 — Preflight & Context

### Stack Detection

- `test_stack_type: auto` -> resolved to **fullstack**.
- Indicators: .NET 10 projects at the root, existing xUnit/Shouldly/NSubstitute suites, Story 10.1 admin-portal ATDD scaffolds, and FrontComposer Blazor/Fluent UI direction in the story.
- Decision: generate .NET xUnit red-phase scaffolds that compile before `Hexalith.Parties.AdminPortal` exists; bUnit/component test project remains a green-phase implementation task.

### Prerequisites

- Story status: `ready-for-dev` with 10 acceptance criteria and explicit endpoint/state matrices.
- Test framework: existing xUnit + Shouldly projects:
  - `tests/Hexalith.Parties.Client.Tests`
  - `tests/Hexalith.Parties.Contracts.Tests`
  - focused backend endpoint suites under `tests/Hexalith.Parties.CommandApi.Tests`
- Story 10.1 ATDD is already complete and provides the portal foundation/precedent.

### Existing Coverage To Preserve

| Surface | Existing File | Coverage |
|---|---|---|
| Read-only portal query contract | `AdminPortalQueryContractTests.cs` | Story 10.1 list/search/detail transport expectations |
| Portal architecture | `AdminPortalReadOnlySurfaceTests.cs` | FrontComposer shell, no parallel SPA, no tenant-management UI |
| Portal XSS guardrails | `AdminPortalXssGuardrailTests.cs` | no `MarkupString`, no `AddMarkupContent`, no unsafe JS HTML bridge |
| Portal auth state | `AdminPortalAuthorizationStateTests.cs` | missing token/tenant/forbidden/tenant-switch reset states |
| GDPR backend endpoints | `ErasureEndpointTests.cs`, `ConsentEndpointTests.cs`, `RestrictionEndpointTests.cs`, `PortabilityEndpointTests.cs` | existing admin API behavior |

### Red-Phase Gaps Targeted

1. GDPR portal adapter/transport for existing admin endpoints.
2. Blazor/FrontComposer operation surface components for erasure, restriction, consent, export, processing records, and DPO summary.
3. Sensitive GDPR state coordinator that clears on auth failure, tenant switch, sign-out, party change, and terminal erased state.
4. Privacy/XSS guardrails specific to GDPR untrusted text, storage keys, telemetry literals, and export filenames.
5. Explicit bounded outcomes for `401`, missing tenant, `403`, `409`, `410`, `422`, and transient failures without raw ProblemDetails leakage.

## Step 02 — Generation Mode

**Mode chosen:** AI generation from story, source code, and prior ATDD patterns.

**Why not browser recording:** the Story 10.2 UI does not exist yet, and the repo’s Story 10.1 precedent uses skipped reflection/transport scaffolds to keep tests compiling before the portal assembly lands.

## Step 03 — Test Strategy

### Test Surfaces

| Level | Project | Purpose |
|---|---|---|
| Client/adapter contract | `tests/Hexalith.Parties.Client.Tests` | Pin methods, routes, outcomes, and safe export envelope for existing admin endpoints |
| Architecture/component fitness | `tests/Hexalith.Parties.Contracts.Tests` | Reflection checks for Blazor components, FrontComposer shell, route scope, no duplicate tenant UI |
| State/privacy fitness | `tests/Hexalith.Parties.Contracts.Tests` | Bounded GDPR state coordinator, stale-response guards, XSS/privacy constraints |
| Backend endpoint suites | existing CommandApi tests | Keep green; only extend in green phase if endpoint contracts change |
| bUnit component tests | future AdminPortal test project | Deferred until implementation creates the actual portal assembly/project |

### Acceptance Criteria To Scenario Map

| AC | Priority | Scenarios |
|---|---|---|
| AC1, AC7, AC10 | P0 | Required GDPR operation panels exist, extend FrontComposer, and avoid tenant-management UI |
| AC2, AC3 | P0 | Erasure request, status, certificate, retry, and terminal erased-state methods/outcomes exist |
| AC4 | P0 | Restrict/lift methods exist and are separate from consent management |
| AC5 | P0 | Consent add/revoke/read methods preserve channel id, purpose, and `LawfulBasis` enum shape |
| AC6 | P0 | Export returns a JSON download envelope with non-PII filename inputs and distinct `409`/`410` outcomes |
| AC7 | P1 | DPO summary component exists and remains tenant-scoped |
| AC8 | P0 | Missing token, missing tenant, forbidden, domain rejection, conflict, gone, and transient failures are bounded states |
| AC9 | P0 | GDPR components avoid `MarkupString`, `AddMarkupContent`, unsafe JS HTML bridges, and PII storage/telemetry literals |
| AC10 | P0 | Portal remains a Blazor/FrontComposer extension, not standalone TypeScript SPA |

### Red-Phase Confirmation

- All 21 tests are marked with `[Fact(Skip = SkipReason)]`.
- Tests assert expected behavior through reflection and contract names.
- Activated tests will fail until Story 10.2 adds the `Hexalith.Parties.AdminPortal` assembly and GDPR portal client/adapter seams.

## Step 04 + 04C — Red-Phase Test Generation

### Generated Files

| File | Skipped Tests | Scope |
|---|---:|---|
| `tests/Hexalith.Parties.Client.Tests/AdminPortal/AdminPortalGdprOperationContractTests.cs` | 6 | GDPR adapter methods, routes, outcome enum, safe export envelope |
| `tests/Hexalith.Parties.Contracts.Tests/AdminPortal/AdminPortalGdprSurfaceTests.cs` | 5 | Required Blazor components, FrontComposer reference, route scope, no parallel SPA |
| `tests/Hexalith.Parties.Contracts.Tests/AdminPortal/AdminPortalGdprAuthorizationStateTests.cs` | 5 | Bounded states, cleanup hooks, stale-response guard, mutation gating |
| `tests/Hexalith.Parties.Contracts.Tests/AdminPortal/AdminPortalGdprPrivacyGuardrailTests.cs` | 5 | XSS/raw-markup scans, unsafe JS literals, safe filename/storage/telemetry rules |
| **Total** | **21** | |

### Fixture Needs For Green Phase

1. `Hexalith.Parties.AdminPortal` assembly that references FrontComposer shell.
2. GDPR operation client/adapter in `Hexalith.Parties.Client.AdminPortal`.
3. `AdminPortalGdprRoutes`, `AdminPortalGdprOutcome`, and `AdminPortalExportDownload` contract types.
4. bUnit test project for actual component rendering, focus management, accessible names/status regions, confirmation dialogs, and localized copy.
5. Fake admin transport covering erasure, status, certificate, retry, restriction, consent, export, processing records, auth failures, `409`, `410`, and `422`.

### Activation Order

1. `AdminPortalGdprOperationContractTests` for adapter/transport contracts.
2. `AdminPortalGdprSurfaceTests` for component/project structure.
3. `AdminPortalGdprAuthorizationStateTests` for state cleanup and erasure mutation gating.
4. `AdminPortalGdprPrivacyGuardrailTests` for XSS/privacy regression guardrails.
5. Add bUnit component tests in the new portal test project once components exist.

## Step 05 — Validate & Complete

### Checklist Validation

- Prerequisites satisfied: story file loaded, existing test framework present, Story 10.1 foundation present.
- Test files created correctly: 4 files, 21 skipped tests.
- Acceptance criteria coverage: AC1 through AC10 mapped.
- Red-phase compliance: every generated test uses `[Fact(Skip = SkipReason)]`.
- No placeholder assertions: tests assert expected types, methods, route constants, enum names, properties, and forbidden patterns.
- Temp artifacts: none outside `_bmad-output/test-artifacts`.
- Browser/CLI sessions: none opened.

### Risks And Assumptions

1. The exact namespace/type names are intentionally pinned for green phase: `Hexalith.Parties.Client.AdminPortal.IAdminPortalGdprClient`, `AdminPortalGdprRoutes`, `AdminPortalGdprOutcome`, `AdminPortalExportDownload`, and `Hexalith.Parties.AdminPortal`.
2. The current red-phase files are reflection/contract scaffolds, not substitute for future bUnit tests. Story implementation should add component tests once the portal project exists.
3. Backend endpoint contracts appear present. If implementation changes request/response contracts, update focused CommandApi tests in the same story rather than widening these portal fitness tests.
4. DPO tenant-wide summary depends on available backend reads; any bounded limitation should be documented in completion notes if no summary endpoint exists.

### Next Recommended Workflow

Run `bmad-dev-story` (or the preferred dev workflow) for `10-2-admin-portal-gdpr-operations`.

Suggested TDD loop:

1. Remove `Skip = SkipReason` from the next scenario being implemented.
2. Run the focused test project.
3. Confirm RED.
4. Implement the smallest matching portal/client seam.
5. Confirm GREEN.
6. Refactor, then proceed to the next scenario.

Focused commands:

```powershell
dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj --configuration Release --filter "FullyQualifiedName~AdminPortalGdprOperationContractTests"
dotnet test tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj --configuration Release --filter "FullyQualifiedName~AdminPortalGdpr"
dotnet build Hexalith.Parties.slnx --configuration Release
```
