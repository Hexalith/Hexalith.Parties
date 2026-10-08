---
title: '8.7 Shared payload-protection adoption and parity'
type: 'refactor'
created: '2026-08-22'
status: 'blocked'
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
- [x] `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md` and `references/Hexalith.EventStore/_bmad-output/implementation-artifacts/8-11-g5-evidence-and-approval-closure.md` -- verify exact identities, approvals, API inventory, backend, and `available`; otherwise halt. Halted `blocked` 2026-09-07: G5 is `needs-additive-api`; named missing receipts are in the Spec Change Log. No production or dependency changes.
- [ ] `tests/Hexalith.Parties.Security.Tests/CryptoKeyManagementCompatibilityHarnessTests.cs` -- run local/shared vectors for v1/v2, AAD mutation/transplant, typed failures, tenant isolation, persisted restart state, and no-leak telemetry.
- [ ] `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs`, `src/Hexalith.Parties.Security/EventStorePartyPayloadProtectionAdapter.cs`, and `src/Hexalith.Parties/Domain/PartyDomainProcessor.cs` -- add reversible selection and neutralize local coupling while retaining v2-capable rollback and public APIs.
- [ ] `tests/Hexalith.Parties.Tests/Gateway/PartySdkQueryHandlerTests.cs`, `tests/Hexalith.Parties.Security.Tests/ErasureVerificationServiceTests.cs`, and `tests/Hexalith.Parties.IntegrationTests/Security/EncryptionPipelineIntegrationTests.cs` -- prove real GDPR, erasure, rotation/retry, persisted state, and backward/forward switches.
- [ ] `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md`, `_bmad-output/implementation-artifacts/sprint-status.yaml`, and `_bmad-output/implementation-artifacts/tests/test-summary.md` -- record identities, totals, rollback proof, retained surfaces, and open KMS gates without crediting skips. Closed-gate identities and missing receipts are recorded; dual-provider totals and post-v2 rollback proof remain unrun.

**Acceptance Criteria:**
- Given the current checkout lacks the G5 runtime packages and 8.11 closure, when execution begins, then it halts `blocked` with no production or dependency changes.
- Given approved G5 artifacts and matching identities, when mixed histories and failures run through both providers, then state, outcomes, GDPR reads, erasure evidence, and no-leak behavior are equivalent.
- Given persisted v2 writes, when selection switches backward and forward, then v1/v2 remain readable without migration and any failure blocks adoption.
- Given every adoption gate is green, when the shared provider is selected, then the local v2-capable rollback path and published Parties security APIs remain intact for the deferred cleanup.

## Spec Change Log

- 2026-10-08 retry at Parties `9e74c2b66ea0e201c6a4f9200e875627fed3b541`: Revalidated the closed gate against matching clean EventStore `8dd7dc2ecdb2c06ecb900676042aa42b66619ee0` (`v3.117.1-7-g8dd7dc2e`) and Builds `a283481c69393dcba911db6a0edcb152167238cc` (`v4.30.1-3-ga283481`), with both package selectors still `3.117.1`. All 32 recorded G5 static checks and retained-file/DI checks passed. New owner 8.3 postreview core evidence grants no provider, package, successor, or G5 approval; 8.3 remains in-progress and 8.4-8.11 backlog. Consumable runtime/backend/release delivery, dual-provider GDPR parity, post-v2 rollback, owner 8.11 closure, and I2/I19a classification approval remain missing. Story 8.7 stays blocked. Production, dependencies, frozen intent/original baseline, matrix approvals, and sprint status are preserved. Exact commands/results are in `tests/test-summary.md`, section "Story 8.7 G5 retry at 9e74c2b6"; product suites were not run or credited.

- 2026-10-08: Closed-gate audit at Parties `998c0b649d7bbf01e0fed4cc79924662dd16cc91`. EventStore root gitlink/clean checkout `9542d3c9f48bf9ce1c57f2ef68904703eaba56cc` (`v3.117.1-5-g9542d3c9`); Builds root gitlink/clean checkout `6f07763bd955d22ace0123798add528dc933bf51` (`v4.30.0-13-g6f07763`); both catalogs select package `3.117.1`. Observations grant no G5 approval or new adoption. Owner 8.2 remains done, 8.3 in-progress, 8.4-8.11 backlog. Public hooks/internal non-packable v2 core are partial delivery; compatibility/lifecycle/persistence/backend/runtime/release delivery, dual-provider GDPR parity, post-v2 rollback, owner 8.11 closure, and I2/I19a classification approval remain missing. All 32 G5 static checks and retained-file/DI checks passed. Story 8.7 stays blocked. No production, dependency, submodule, frozen-block, original-baseline, or approval-table change; no product-test credit. Reconcile the frozen historical identity with an approved G5 identity before activation.

- 2026-10-05: Closed-gate audit at Parties `b3794a4dcbe2fff3e9ea5c420a3c4695e2d1a8ca`. EventStore root gitlink/clean checkout `865cd9e49273dffbb1cdae85efeaf1aac322e09e` (`v3.113.0`); observed Builds gitlink/clean checkout `90f3836dd7482db35c2c187a50999b99215919b0` (`v4.29.1-21-g90f3836`); both catalog and Parties pin select `3.113.0`. The existing I20 EventStore identity selection is not G5 approval; the later Builds observation is not a new approval. Owner 8.2 remains done, 8.3 in-progress, 8.4-8.11 backlog. Public policy/erasure contracts and internal non-packable v2 core exist; compatibility/lifecycle/backend/runtime/release delivery, dual-provider GDPR parity, post-v2 rollback, 8.11 availability closure, and I2/I19a policy-hook classification approval remain missing. All 32 recorded G5 checks and retained-file/DI checks passed. Story 8.7 stays blocked; no production, dependency, submodule, frozen-block, or original-baseline change; no product-test credit. Activation must reconcile the frozen historical identity with the eventual approved G5 identity.

- 2026-10-03: Revalidated the closed gate at Parties `06714c166c090200ac87373b11d8243aa11b2126`. EventStore gitlink/clean checkout `2c58ffda41759e895ace4b9625c9bd931a217672` (`v3.111.0`); Builds gitlink/checkout `688eec9a4333245cc0ff7772115c769094471863` selects packages `3.110.0`. Observed identities differ from the frozen `c21bd749154d701c3b7d68e40d1008d3475e35c4` / `3.95.0`; no new identity is adopted. Public `IPersonalDataPolicy` and `IErasureStateProvider`, plus an internal `pdenc-v2` core project (`IsPackable=false`), now exist. EventStore 8.2 is done and 8.3 in-progress; 8.4-8.11 remain backlog. The AzureKeyVault project, package catalog/release enrollment, consumable runtime provider, dual-provider parity, post-v2 rollback, named G5 availability approval, and 8.11 closure remain missing. G5 stays `needs-additive-api`; this spec and Story 8.7 stay `blocked`. Parties 8.6 is done. Static inventory passed; all 24 MOVE/KEEP/adapter files and local DI remain. No production/dependency changes; no product tests run or credited. The frozen block and original baseline remain unchanged.

- 2026-09-07: Closed-gate halt. Live EventStore gitlink and checkout `d45206f7cbd80a112519c1d4687d7279a745f0c5` (`v3.103.0-2-gd45206f7`); package graph `3.103.0`. Spec frozen identity `c21bd749154d701c3b7d68e40d1008d3475e35c4` / `3.95.0` does not match live checkout; Ask First forbids adopting a new identity. G5 remains `needs-additive-api`. Missing receipts: EventStore `8-11-g5-evidence-and-approval-closure.md`; `Hexalith.EventStore.PayloadProtection` and `Hexalith.EventStore.PayloadProtection.AzureKeyVault` source projects and catalog package versions; runtime `pdenc-v2`, `IPersonalDataPolicy`, and `IErasureStateProvider`; EventStore Stories 8.2-8.11 remaining backlog; named G5 `available` approvals; dual-provider parity; post-v2 rollback; production KMS. `AddEventStoreDataProtection` and `NoOpEventPayloadProtectionService` are not the shared engine. No production, DI, or dependency changes. Local engine, adapter, DI, MOVE/KEEP files, and public APIs retained.

## Verification

**Commands:**
- `git ls-tree HEAD references/Hexalith.EventStore && git -C references/Hexalith.EventStore rev-parse HEAD` -- expected: identical source identity recorded in the matrix.
- `dotnet build tests/Hexalith.Parties.Security.Tests/Hexalith.Parties.Security.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:HexalithEventStoreFromSource=true && ./tests/Hexalith.Parties.Security.Tests/bin/Debug/net10.0/Hexalith.Parties.Security.Tests -class Hexalith.Parties.Security.Tests.CryptoKeyManagementCompatibilityHarnessTests` -- expected: dual-provider tests pass without skips.
- `dotnet build src/Hexalith.Parties.Security/Hexalith.Parties.Security.csproj -c Release -p:UseHexalithProjectReferences=false -p:HexalithEventStoreFromSource=false -m:1 && pwsh scripts/test.ps1 -Lane unit && pwsh scripts/test.ps1 -Lane topology && bash scripts/check-no-warning-override.sh && git diff --check` -- expected: available gates pass; a production-KMS gap remains blocking.
