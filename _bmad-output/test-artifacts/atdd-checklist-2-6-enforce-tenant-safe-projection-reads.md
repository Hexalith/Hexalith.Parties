---
stepsCompleted:
  - step-01-preflight-and-context
  - step-02-generation-mode
  - step-03-test-strategy
  - step-04-generate-tests
  - step-04c-aggregate
lastStep: step-04c-aggregate
lastSaved: 2026-05-19
storyId: '2.6'
storyKey: 2-6-enforce-tenant-safe-projection-reads
storyFile: _bmad-output/implementation-artifacts/2-6-enforce-tenant-safe-projection-reads.md
atddChecklistPath: _bmad-output/test-artifacts/atdd-checklist-2-6-enforce-tenant-safe-projection-reads.md
detectedStack: backend
testFramework: xUnit v3 + Shouldly + NSubstitute
generationMode: ai-generation
generatedTestFiles:
  - tests/Hexalith.Parties.Tests/Gateway/TenantSafeProjectionReadGuardrailsTests.cs
inputDocuments:
  - _bmad-output/implementation-artifacts/2-6-enforce-tenant-safe-projection-reads.md
  - _bmad-output/test-artifacts/test-design/test-design-epic-2.md
  - _bmad-output/implementation-artifacts/sprint-status.yaml
  - _bmad/tea/config.yaml
  - _bmad-output/project-context.md
  - tests/Hexalith.Parties.Tests/Gateway/PartyDetailProjectionQueryActorTests.cs
  - tests/Hexalith.Parties.Tests/Gateway/PartyIndexProjectionQueryActorTests.cs
  - tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs
  - tests/Hexalith.Parties.Tests/Projections/PartyDetailProjectionActorCorruptionTests.cs
  - tests/Hexalith.Parties.Tests/Projections/PartyIndexProjectionActorCorruptionTests.cs
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/data-factories.md
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/test-levels-framework.md
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/test-priorities-matrix.md
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/test-quality.md
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/test-healing-patterns.md
---

# ATDD Checklist — Story 2.6

## Step 01 — Preflight & Context

- **Stack:** backend (.NET 10, xUnit v3, Shouldly, NSubstitute)
- **Story status:** ready-for-dev (party-mode review complete on 2026-05-19)
- **Test design risk anchor:** R-01 cross-tenant leakage (P0), R-02 diagnostics PII (P0), R-03 untrusted tenant source (P1), R-04 erased-PII resurfacing (P1), R-05 mixed-provenance cache (P1)
- **Existing coverage avoided:** existing cross-tenant + log-scrub tests in `PartyDetailProjectionQueryActorTests` (lines 89-285) and `PartyIndexProjectionQueryActorTests`

## Step 02 — Generation Mode

- **Mode:** AI generation from source + story-pinned ACs (backend rule)
- **Recording:** N/A

## Step 03 — Test Strategy

| AC | Scenario | Tier | Priority | Risk |
|---|---|---|---|---|
| AC1 | Missing tenant fails before actor construction | Tier-2 Gateway | P0 | R-01 |
| AC2/AC3 | Payload-tenant override ignored | Tier-2 Gateway | P1 | R-03 |
| AC3 | Wrong-tenant probe non-enumerating | Tier-2 Gateway | P0 | R-01 |
| AC6 | Static last-known-detail cache scoped per tenant | Tier-1 Unit | P1 | R-05 |
| AC2/AC6 | Erase + reactivation doesn't resurface PII | Tier-1 Unit | P1 | R-04 |
| AC4 | Index query ignores caller-supplied partition key | Tier-2 Gateway | P1 | R-03 |
| AC4 | Cross-tenant list `TotalCount` tenant-scoped | Tier-2 Gateway | P1 | R-09 |
| AC7 | Diagnostics never include actor/state/partition keys | Tier-2 Gateway | P0 | R-02 |

## Step 04 — Generated Tests (RED PHASE)

- `tests/Hexalith.Parties.Tests/Gateway/TenantSafeProjectionReadGuardrailsTests.cs` — 8 facts, all `[Fact(Skip = "...")]`. Two scaffolds use `Assert.Skip("Materialize once...")` placeholders because they need test seams that don't yet exist (`PartyDetailProjectionActor` static cache lookup; `PartyIndexProjectionQueryActor` deterministic `TotalCount` harness). The dev-story should add the seams and convert these to active failing tests first.

## Step 04C — Aggregation

- Compilation expected to pass (signatures use real types: `IActorProxyFactory`, `ActorId`, `PartyDetailProjectionQueryActor`, `PartyIndexProjectionQueryActor`).
- Three private helpers (`CreateEnvelope`, `CreateEnvelopeWithPayloadTenantOverride`, `CreateEnvelopeWithPayloadPartitionOverride`) throw `NotImplementedException` — dev-story must wire them to the production `QueryEnvelope` builder used by the existing `PartyDetailProjectionQueryActorTests` harness.
- Topology Fitness scans (`2.6-FIT-009/014/015/110/111/112/143`) deferred — those are Roslyn-based fitness tests that need their own project surface and belong to a follow-up `bmad-testarch-test-design` execute or in-story Task 7.

## Dev-Story Handoff

When activating: (1) wire the three `QueryEnvelope` builders to the production contract; (2) add the test seams for static-cache lookup in `PartyDetailProjectionActor`; (3) remove `Skip = "..."`; (4) run `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~TenantSafeProjectionReadGuardrailsTests`, expect RED until tenant-source authority + static cache provenance are implemented per Tasks 2–4 of the story.
