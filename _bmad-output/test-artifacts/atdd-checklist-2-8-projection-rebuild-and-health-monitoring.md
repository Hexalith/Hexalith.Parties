---
stepsCompleted:
  - step-01-preflight-and-context
  - step-02-generation-mode
  - step-03-test-strategy
  - step-04-generate-tests
  - step-04c-aggregate
lastStep: step-04c-aggregate
lastSaved: 2026-05-19
storyId: '2.8'
storyKey: 2-8-projection-rebuild-and-health-monitoring
storyFile: _bmad-output/implementation-artifacts/2-8-projection-rebuild-and-health-monitoring.md
atddChecklistPath: _bmad-output/test-artifacts/atdd-checklist-2-8-projection-rebuild-and-health-monitoring.md
detectedStack: backend
testFramework: xUnit v3 + Shouldly + NSubstitute
generationMode: ai-generation
generatedTestFiles:
  - tests/Hexalith.Parties.Tests/Projections/ProjectionRebuildAndHealthHardeningTests.cs
inputDocuments:
  - _bmad-output/implementation-artifacts/2-8-projection-rebuild-and-health-monitoring.md
  - _bmad-output/test-artifacts/test-design/test-design-epic-2.md
  - _bmad-output/implementation-artifacts/sprint-status.yaml
  - _bmad/tea/config.yaml
  - _bmad-output/project-context.md
  - tests/Hexalith.Parties.Tests/Projections/ProjectionRebuildServiceTests.cs
  - tests/Hexalith.Parties.Tests/Projections/PartyDetailProjectionActorCorruptionTests.cs
  - tests/Hexalith.Parties.Tests/Projections/PartyIndexProjectionActorCorruptionTests.cs
  - tests/Hexalith.Parties.Tests/HealthChecks/ProjectionActorsHealthCheckTests.cs
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/data-factories.md
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/test-levels-framework.md
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/test-priorities-matrix.md
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/test-quality.md
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/test-healing-patterns.md
---

# ATDD Checklist — Story 2.8

## Step 01 — Preflight & Context

- **Stack:** backend (.NET 10, xUnit v3, Shouldly, NSubstitute)
- **Story status:** ready-for-dev (story created 2026-05-19; no party-mode review yet — story note L08 says party-mode + advanced elicitation are separate traces)
- **Test design risk anchor:** R-13 PERF rebuild latency (capture-only, P2), R-14 OPS rebuild stuck states (P2), R-19 OPS health-endpoint authorization (P3, lifted to confidence 7)
- **Existing coverage avoided:** existing `ProjectionRebuildServiceTests`, `PartyDetailProjectionActorCorruptionTests`, `PartyIndexProjectionActorCorruptionTests`, `ProjectionActorsHealthCheckTests`

## Step 02 — Generation Mode

- **Mode:** AI generation from source + story-pinned ACs (backend rule)
- **Recording:** N/A

## Step 03 — Test Strategy

| AC | Scenario | Tier | Priority | Risk |
|---|---|---|---|---|
| AC1/AC7 | Healthy projection actors → bounded description text | Tier-1 Unit | P3 | R-19 |
| AC2/AC4 | Routing failure → degraded without tenant/key leakage | Tier-1 Unit | P3 | R-19 |
| AC3 | Rejection events do not mutate successful state during rebuild | Tier-1 Unit | P1 | R-07 |
| AC6 | State-store write failure fails closed | Tier-1 Unit | P2 | R-14 |
| AC8 | Cancellation mid-rebuild stops after current event | Tier-1 Unit | P2 | R-14, R-15 |
| AC6 | Checkpoint delete failure leaves projection degraded | Tier-1 Unit | P2 | R-14 |
| AC4 | Cross-tenant probe during rebuild non-enumerating | Tier-1 Unit | P1 | R-04, R-05 |
| AC5 | Successful rebuild clears rebuilding state | Tier-1 Unit | P2 | — |

## Step 04 — Generated Tests (RED PHASE)

- `tests/Hexalith.Parties.Tests/Projections/ProjectionRebuildAndHealthHardeningTests.cs` — 8 facts, all `[Fact(Skip = "...")]`. Six use `Assert.Skip("Materialize once...")` placeholders because they need rebuild-service harnesses that don't yet exist (cancellable event pump, checkpoint-cleanup probe, two-tenant rebuild test seam). Dev-story Task 7 lists these as harness extensions.

## Step 04C — Aggregation

- Compilation expected to pass (signatures use real types: `ProjectionActorsHealthCheck`, `HealthCheckContext`, `HealthCheckRegistration`).
- Helper `CreateHealthCheck(bool)` throws `NotImplementedException` — dev-story must wire it to the existing `ProjectionActorsHealthCheckTests` mock pattern (mocked `IActorProxyFactory`, `IPartyDetailProjectionActor`, `IPartyIndexProjectionActor`).
- Tier-3 capture-only baselines (`2.8-INT-120/121`, R-13) and chaos rebuild kill (`2.8-INT-133`, R-14) deferred to Aspire integration test project — they belong in `tests/Hexalith.Parties.IntegrationTests/` and require Nightly/Weekly schedule, not PR-time RED.

## Dev-Story Handoff

When activating: (1) wire `CreateHealthCheck(bool)` to the existing mock pattern from `ProjectionActorsHealthCheckTests`; (2) add the rebuild-service test seams for cancellable event pump and checkpoint-cleanup probe (story Task 7); (3) remove `Skip = "..."`; (4) run `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~ProjectionRebuildAndHealthHardeningTests`, expect RED until bounded health vocabulary + rebuild-failure tenant-safety paths land.
