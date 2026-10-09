---
title: '8.7 Data-protection extraction at the G5 gate'
type: 'refactor'
created: '2026-10-09'
status: 'ready-for-dev'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-8-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/8-7-data-protection-extraction.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-8-7-data-protection-extraction.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Parties owns generic payload encryption and key mechanics. An unproven shared replacement could make historical data unreadable, misstate erasure, or strand v2 writes after rollback.

**Approach:** Recheck G5. While closed, record evidence and retain production. Once approved and consumable, select the shared provider reversibly, prove parity and post-v2 switch-back, then remove proven generic code.

## Boundaries & Constraints

**Always:** Before integration, require G5 `available`, named owner/security approval, EventStore 8.11 closure, approved source/package identity, approved I2/I19a policy classification, and runtime/backend/release enrollment. Before switching defaults or deleting code, require dual-provider parity and exercised rollback. Preserve plaintext/v1/v2 and redacted reads, typed failures, tenant isolation, key zeroing, PII-free diagnostics, GDPR semantics, default-on crypto-shredding, public security APIs, and a v2-capable local rollback path. Production KMS is a separate release gate.

**Never:** Change production or dependencies while G5 is closed; treat owner 8.3 completion, its nonpackable core, source presence, or cursor Data Protection as the payload engine; delete local code before all seven deletion proofs and post-v2 rollback. Format or `IErasureVerificationService` changes require approval. Exclude 8.8/8.9.

## I/O & Edge-Case Matrix

| State | Expected behavior | Failure handling |
|-------|-------------------|------------------|
| Missing G5 receipt | Remain blocked; retain local DI/files | Name missing proof; credit no unrun tests |
| Plaintext, v1/v2, redacted, malformed event/snapshot | Both providers yield equivalent reads or typed outcomes | Distinguish deleted, missing, denied, outage, and tamper; only proven erasure yields redaction |
| Persisted v2, local/shared switch | Local reads v1/v2; forward switch succeeds without rewrite | Mismatch blocks adoption/deletion |

</frozen-after-approval>

## Code Map

- `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md:196` -- G5 remains `needs-additive-api`; I2/I19a approval is pending at line 225. Its owner-8.3-status check is stale; preserve approval cells.
- `references/Hexalith.EventStore/_bmad-output/implementation-artifacts/8-3-pdenc-v2-core-cryptographic-engine.md` -- 8.3 is done but disclaims G5; 8.4–8.11 are backlog; Server defaults to no-op.
- `src/Hexalith.Parties.Security/PartyPayloadProtectionService.cs` and `EventStorePartyPayloadProtectionAdapter.cs` -- local engine and active seam; retain for rollback.
- `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs:137` and `src/Hexalith.Parties/Domain/PartyDomainProcessor.cs:694` -- local DI and v1 coupling; wait for G5.
- `tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs:110` -- expects owner 8.3 `in-progress`; reconcile with `done` without promoting G5.

## Tasks & Acceptance

**Execution:**
- [ ] `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md` -- check owner 8.3/8.11 proof, approvals, pins, parity, and rollback; halt if any G5 receipt is missing.
- [ ] `tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs`, `_bmad-output/implementation-artifacts/8-7-data-protection-extraction.md`, `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md`, and `_bmad-output/implementation-artifacts/tests/test-summary.md` -- reconcile owner 8.3 `done`; record the gate without promoting G5 or crediting unrun tests.
- [ ] `tests/Hexalith.Parties.Security.Tests/CryptoKeyManagementCompatibilityHarnessTests.cs` -- after G5 opens, run identical v1/v2, AAD-transplant, legacy, key-failure, restart, and no-leak vectors through both providers.
- [ ] `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs`, `src/Hexalith.Parties.Security/EventStorePartyPayloadProtectionAdapter.cs`, and `src/Hexalith.Parties/Domain/PartyDomainProcessor.cs` -- add reversible selection and v2-safe mapping; retain local fallback and public APIs.
- [ ] `tests/Hexalith.Parties.Tests/Gateway/PartySdkQueryHandlerTests.cs`, `tests/Hexalith.Parties.Security.Tests/ErasureVerificationServiceTests.cs`, and `tests/Hexalith.Parties.IntegrationTests/Security/EncryptionPipelineIntegrationTests.cs` -- prove real export, processing, erasure, persisted state, and post-v2 switch-back.
- [ ] `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md`, `_bmad-output/implementation-artifacts/tests/test-summary.md`, and `_bmad-output/implementation-artifacts/sprint-status.yaml` -- record seven proofs and exact identities before deletion; preserve KEEP files and adapter.

**Acceptance Criteria:**
- Given any missing G5 receipt, when 8.7 runs, then it remains blocked without production, dependency, or rollback-path changes.
- Given an approved provider, when both paths process mixed histories and GDPR workflows, then outcomes, persisted state, erasure evidence, and diagnostics match.
- Given persisted v2 writes, when selection switches to local and back, then v1/v2 remain readable without migration; failure prevents adoption or deletion.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

**Commands:**
- `git ls-tree HEAD references/Hexalith.EventStore references/Hexalith.Builds`; compare each checkout's `git rev-parse HEAD` and `git status --porcelain` -- matching, clean identities.
- Build `tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj` in Debug source mode and invoke its xUnit v3 assembly with `-class Hexalith.Parties.Tests.FitnessTests.PlatformApiPrerequisitesTests` -- current owner-state assertion passes without changing G5.
- When G5 opens, run direct Security, Parties, and Integration assemblies plus unit/topology, no-warning-override, and `git diff --check` -- parity and rollback pass; record skips.
