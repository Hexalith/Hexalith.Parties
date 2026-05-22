---
stepsCompleted:
  - step-01-preflight-and-context
  - step-02-generation-mode
  - step-03-test-strategy
  - step-04-generate-tests
  - step-04c-aggregate
lastStep: step-04c-aggregate
lastSaved: 2026-05-19
storyId: '2.7'
storyKey: 2-7-handle-projection-freshness-and-graceful-degradation
storyFile: _bmad-output/implementation-artifacts/2-7-handle-projection-freshness-and-graceful-degradation.md
atddChecklistPath: _bmad-output/test-artifacts/atdd-checklist-2-7-handle-projection-freshness-and-graceful-degradation.md
detectedStack: backend
testFramework: xUnit v3 + Shouldly + NSubstitute
generationMode: ai-generation
generatedTestFiles:
  - tests/Hexalith.Parties.Tests/HealthChecks/ProjectionFreshnessAndDegradationTests.cs
inputDocuments:
  - _bmad-output/implementation-artifacts/2-7-handle-projection-freshness-and-graceful-degradation.md
  - _bmad-output/test-artifacts/test-design/test-design-epic-2.md
  - _bmad-output/implementation-artifacts/sprint-status.yaml
  - _bmad/tea/config.yaml
  - _bmad-output/project-context.md
  - tests/Hexalith.Parties.Tests/HealthChecks/DegradedResponseMiddlewareTests.cs
  - tests/Hexalith.Parties.Tests/HealthChecks/ProjectionActorsHealthCheckTests.cs
  - src/Hexalith.Parties/Middleware/DegradedResponseMiddleware.cs
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/data-factories.md
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/test-levels-framework.md
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/test-priorities-matrix.md
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/test-quality.md
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/test-healing-patterns.md
---

# ATDD Checklist — Story 2.7

## Step 01 — Preflight & Context

- **Stack:** backend (.NET 10, xUnit v3, Shouldly, NSubstitute)
- **Story status:** ready-for-dev (party-mode review complete on 2026-05-19; predev preflight failure logged in `predev-preflight-2026-05-19T150055Z.json`)
- **Test design risk anchor:** R-05 mixed-provenance cache (P1), R-10 cross-tenant freshness leakage (P1)
- **Existing coverage avoided:** existing `DegradedResponseMiddlewareTests` healthy/degraded/POST/5xx coverage

## Step 02 — Generation Mode

- **Mode:** AI generation from source + story-pinned ACs (backend rule)
- **Recording:** N/A

## Step 03 — Test Strategy

| AC | Scenario | Tier | Priority | Risk |
|---|---|---|---|---|
| AC2 | Healthy current emits no freshness/degraded headers | Tier-2 Gateway | P1 | R-10 |
| AC3 | Rebuilding projection emits bounded vocabulary | Tier-2 Gateway | P1 | R-10 |
| AC4 | State-store unavailable + loaded actor → degraded signal | Tier-2 Gateway | P1 | R-05 |
| AC5 | Sidecar unavailable → no safe degraded signal | Tier-2 Gateway | P1 | R-05 |
| AC6 | Cross-tenant freshness probe non-enumerating | Tier-2 Gateway | P1 | R-10 |
| AC5 | 5xx response strips degraded headers | Tier-2 Gateway | P1 | — |

## Step 04 — Generated Tests (RED PHASE)

- `tests/Hexalith.Parties.Tests/HealthChecks/ProjectionFreshnessAndDegradationTests.cs` — 6 facts, all `[Fact(Skip = "...")]`. Cross-tenant freshness probe uses `Assert.Skip("Materialize once...")` because `DegradedResponseMiddleware` currently emits global health and per-tenant projection probing requires a new harness.

## Step 04C — Aggregation

- Compilation expected to pass (signatures use real types: `DegradedResponseMiddleware`, `HealthCheckService`, `HealthReport`, `HealthReportEntry`).
- Four health-service factories already wired (`CreateHealthCheckService`, `CreateRebuildingProjectionHealthCheckService`, `CreateStateStoreUnavailableButProjectionLoadedHealthCheckService`, `CreateSidecarUnavailableHealthCheckService`). Dev-story may need to align them with the production projection health-check key names.
- Bounded vocabulary assertion uses tentative header semantics; dev-story should pin the final naming and update the allowlist if the freshness header model evolves beyond `X-Service-Degraded` / `X-Stale-Data-Age`.

## Dev-Story Handoff

When activating: (1) align the synthetic `HealthReportEntry` keys with the production health-check names; (2) decide whether the freshness contract stays on headers or moves to additive `PagedResult<T>` metadata (per story's "Deferred Decisions"); (3) remove `Skip = "..."`; (4) run `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~ProjectionFreshnessAndDegradationTests`, expect RED until middleware emits bounded-vocabulary freshness signals and tenant-scoped projection probing.
