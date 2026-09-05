---
title: '8.7 Shared payload-protection adoption and parity'
type: 'refactor'
created: '2026-08-22'
status: 'in-progress'
baseline_commit: '3d3abef4279e41cf0025870152e3fc597e26f872'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md'
  - '{project-root}/references/Hexalith.EventStore/_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Parties owns the working payload engine; EventStore supplies only contracts and a no-op. An unproven replacement risks unreadable histories, wrong erasure outcomes, and failed rollback.

**Approach:** After EventStore closes G5, adopt its provider behind reversible DI and prove parity across real Parties workflows and post-v2 switch-back. Retain the local engine and public APIs; delete them only in the deferred cleanup.

## Boundaries & Constraints

**Always:** Gate entry on G5 `available`, the EventStore 8.11 closure, approvals, and exact package/source identities. The current source is `c21bd749154d701c3b7d68e40d1008d3475e35c4`; the package graph uses `3.95.0`. Preserve plaintext/v1/v2 reads, typed outcomes, tenant isolation, key zeroing, no-leak diagnostics, GDPR semantics, erasure evidence, and default-on crypto-shredding.

**Ask First:** Any EventStore contract or dependency identity change; any breaking change to published Parties security APIs; any change to `IErasureVerificationService`, certificates/reports, persisted formats/names, or approved G5 evidence.

**Never:** Change production code while G5 is closed. Do not mistake `AddEventStoreDataProtection` or the no-op service for the shared engine, weaken provenance tests, log PII/key/payload material, disable crypto-shredding, delete the local path, or absorb Stories 8.8/8.9.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Closed gate | Missing package, closure, approval, identity, or parity receipt | Halt `blocked` without production/dependency changes | Name every missing receipt |
| Mixed history | Plaintext, v1, and v2 events/snapshots | Both providers reconstruct identical domain state | Malformed/mismatched metadata yields typed unreadable outcomes |
| Rollback | Persisted v2 data followed by local selection | Local path reads v1/v2; forward selection succeeds again | Any mismatch blocks adoption |

</frozen-after-approval>

## Code Map

- `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md:97` -- G5 is `needs-additive-api`; its source receipt trails the live gitlink.
- `references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Security/IEventPayloadProtectionService.cs:10` -- provider-neutral event/snapshot and typed-outcome seam.
- `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs:130` -- reversible provider-selection boundary; currently local-only.
- `src/Hexalith.Parties.Security/EventStorePartyPayloadProtectionAdapter.cs:15` and `src/Hexalith.Parties/Domain/PartyDomainProcessor.cs:569` -- adapter and direct coupling to neutralize without deleting.
- `tests/Hexalith.Parties.Security.Tests/CryptoKeyManagementCompatibilityHarnessTests.cs:24` -- 19-case local baseline; `CreateHarness` at line 699 needs dual-provider parameterization.
- `tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs:29` -- immutable identity and retention guard.

## Tasks & Acceptance

**Execution:**
- [ ] `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md` and `references/Hexalith.EventStore/_bmad-output/implementation-artifacts/8-11-g5-evidence-and-approval-closure.md` -- verify exact identities, approvals, API inventory, backend, and `available`; otherwise halt.
- [ ] `tests/Hexalith.Parties.Security.Tests/CryptoKeyManagementCompatibilityHarnessTests.cs` -- run local/shared vectors for v1/v2, AAD mutation/transplant, typed failures, tenant isolation, persisted restart state, and no-leak telemetry.
- [ ] `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs`, `src/Hexalith.Parties.Security/EventStorePartyPayloadProtectionAdapter.cs`, and `src/Hexalith.Parties/Domain/PartyDomainProcessor.cs` -- add reversible selection and neutralize local coupling while retaining v2-capable rollback and public APIs.
- [ ] `tests/Hexalith.Parties.Tests/Gateway/PartySdkQueryHandlerTests.cs`, `tests/Hexalith.Parties.Security.Tests/ErasureVerificationServiceTests.cs`, and `tests/Hexalith.Parties.IntegrationTests/Security/EncryptionPipelineIntegrationTests.cs` -- prove real GDPR, erasure, rotation/retry, persisted state, and backward/forward switches.
- [ ] `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md`, `_bmad-output/implementation-artifacts/sprint-status.yaml`, and `_bmad-output/implementation-artifacts/tests/test-summary.md` -- record identities, totals, rollback proof, retained surfaces, and open KMS gates without crediting skips.

**Acceptance Criteria:**
- Given the current checkout lacks the G5 runtime packages and 8.11 closure, when execution begins, then it halts `blocked` with no production or dependency changes.
- Given approved G5 artifacts and matching identities, when mixed histories and failures run through both providers, then state, outcomes, GDPR reads, erasure evidence, and no-leak behavior are equivalent.
- Given persisted v2 writes, when selection switches backward and forward, then v1/v2 remain readable without migration and any failure blocks adoption.
- Given every adoption gate is green, when the shared provider is selected, then the local v2-capable rollback path and published Parties security APIs remain intact for the deferred cleanup.

## Spec Change Log

## Verification

**Commands:**
- `git ls-tree HEAD references/Hexalith.EventStore && git -C references/Hexalith.EventStore rev-parse HEAD` -- expected: identical source identity recorded in the matrix.
- `dotnet build tests/Hexalith.Parties.Security.Tests/Hexalith.Parties.Security.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:HexalithEventStoreFromSource=true && ./tests/Hexalith.Parties.Security.Tests/bin/Debug/net10.0/Hexalith.Parties.Security.Tests -class Hexalith.Parties.Security.Tests.CryptoKeyManagementCompatibilityHarnessTests` -- expected: dual-provider tests pass without skips.
- `dotnet build src/Hexalith.Parties.Security/Hexalith.Parties.Security.csproj -c Release -p:UseHexalithProjectReferences=false -p:HexalithEventStoreFromSource=false -m:1 && pwsh scripts/test.ps1 -Lane unit && pwsh scripts/test.ps1 -Lane topology && bash scripts/check-no-warning-override.sh && git diff --check` -- expected: available gates pass; a production-KMS gap remains blocking.
