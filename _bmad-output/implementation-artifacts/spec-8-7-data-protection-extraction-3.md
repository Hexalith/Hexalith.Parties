---
title: '8.7 Data-protection extraction at the G5 gate'
type: 'refactor'
created: '2026-10-09'
status: 'ready-for-dev'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'b0fca072d54082d94467fc316ec24a809daea84e'
story_key: '8-7-data-protection-extraction'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-8-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/8-7-data-protection-extraction.md'
  - '{project-root}/_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Parties owns generic payload encryption and key mechanics. A premature provider switch could break historical reads, erasure claims, or v2 rollback.

**Approach:** Recheck G5 and retain production while closed. After approval, adopt through reversible DI, prove persisted-history and GDPR parity, exercise post-v2 rollback, then remove proven generic code.

## Boundaries & Constraints

**Always:** Require G5 `available`, named security/owner approval, EventStore 8.11 closure, exact source/package identity, I2/I19a classification, runtime/backend/release enrollment, golden vectors, and rollback before adoption. Preserve plaintext, v1/v2, redacted and snapshot reads; typed failures, tenant isolation, key zeroing, PII-free diagnostics, exports, processing records, and erasure evidence. Only certificate or erasure-record proof establishes key destruction as erasure. Keep crypto-shredding default true and local v2 reads before switching writes. Production KMS is a separate release gate.

**Never:** Change production, dependencies, or gitlinks while G5 is closed; mistake the internal v2 core or cursor Data Protection for the provider; delete local rollback before dual-provider parity, seven proofs, and post-v2 switch-back. Contract, format, or erasure-semantic changes require separate approval. Exclude 8.8/8.9.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Closed G5 | Missing required receipt | Remain blocked; retain local DI/files | Name gaps; credit no unrun tests |
| Mixed history | Plaintext, v1/v2, redacted, malformed event or snapshot | Both providers reconstruct equivalent state or bounded typed outcomes | Missing, denied, outage, tamper, and destroyed-key outcomes stay distinct |
| Rollback | Shared provider has persisted v2 writes | Local path reads v1/v2, then shared path resumes without rewriting history | Any mismatch blocks default switch and deletion |

</frozen-after-approval>

## Code Map

- `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md:220` -- G5 is closed; I2/I19a approval is pending at :249.
- `references/Hexalith.EventStore/_bmad-output/implementation-artifacts/sprint-status.yaml:310` -- owner 8.3 done, 8.4-8.11 backlog; `references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs:95` registers no-op.
- `src/Hexalith.Parties.Security/PartyPayloadProtectionService.cs:21` -- local v1 engine; no v2 reads. `EventStorePartyPayloadProtectionAdapter.cs:15` maps metadata and typed outcomes.
- `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs:137` -- local key/crypto registrations; :187 binds the active adapter. `src/Hexalith.Parties/Domain/PartyDomainProcessor.cs:694` has format coupling.
- `tests/Hexalith.Parties.Security.Tests/CryptoKeyManagementCompatibilityHarnessTests.cs:699` -- local-only harness; extend for both providers.
- `_bmad-output/implementation-artifacts/8-7-data-protection-extraction.md:228` -- authoritative 18 MOVE, five KEEP, and adapter KEEP inventory; :272 defines rollout and rollback order.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md` and owner status -- recheck G5 at pinned EventStore `75a08f0069d8c2495d9dff20a0deb84edb6cc638` / package `3.117.1`; still closed.
- [ ] `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md` -- record approval, owner closure, identity, policy classification, producer proof, and rollback before changing G5 status.
- [ ] `tests/Hexalith.Parties.Security.Tests/CryptoKeyManagementCompatibilityHarnessTests.cs` -- run fixed v1/v2, AAD transplant, legacy, key-state, snapshot, restart, and no-leak vectors through both providers.
- [ ] `src/Hexalith.Parties.Security/EventStorePartyPayloadProtectionAdapter.cs`, `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs`, and `src/Hexalith.Parties/Domain/PartyDomainProcessor.cs` -- add reversible provider selection and v2-safe mapping, retaining local default and public APIs until parity passes.
- [ ] `tests/Hexalith.Parties.Tests/Gateway/PartySdkQueryHandlerTests.cs`, `tests/Hexalith.Parties.Security.Tests/ErasureVerificationServiceTests.cs`, and `tests/Hexalith.Parties.IntegrationTests/Security/EncryptionPipelineIntegrationTests.cs` -- prove export, processing, erasure, persisted state, and post-v2 switch-back.
- [ ] `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md` and `_bmad-output/implementation-artifacts/tests/test-summary.md` -- record seven deletion proofs and exact identities before removing any MOVE file; keep domain files and adapter.

**Acceptance Criteria:**
- Given any missing G5 receipt, when Story 8.7 is attempted, then it remains blocked without production, dependency, or rollback-path changes.
- Given an approved provider, when both paths process mixed histories and GDPR workflows, then typed outcomes, persisted state, erasure evidence, and diagnostics match.
- Given persisted v2 writes, when selection switches to local and back, then v1/v2 remain readable without migration; any mismatch prevents adoption or deletion.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

Local AES-GCM v1 has no AAD and cannot read v2. Freeze key paths, metrics, retry actor state/reminder names, and metadata against both providers before changing defaults.

## Verification

**Commands:**
- `git ls-tree HEAD references/Hexalith.EventStore references/Hexalith.Builds` plus clean matching submodule checkout/status checks -- exact selected identities.
- Build and directly run focused xUnit v3 Security, Parties, and Integration assemblies in Debug source mode after G5 opens -- dual-provider and GDPR parity with no skipped proof.
- `pwsh scripts/test.ps1 -Lane unit`, `pwsh scripts/test.ps1 -Lane topology`, `bash scripts/check-no-warning-override.sh`, and `git diff --check` -- passes; report any environment-limited lane separately.
