---
title: '8.7 G5 revalidation and data-protection extraction'
type: 'refactor'
created: '2026-10-10'
status: 'ready-for-dev'
route: 'dispatch'
review_loop_iteration: 0
story_key: '8-7-data-protection-extraction'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-8-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/8-7-data-protection-extraction.md'
  - '{project-root}/_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Parties owns generic payload protection and key mechanics. Premature extraction risks unreadable histories, false erasure claims, and failed v2 rollback.

**Approach:** Revalidate G5, retaining the local implementation while closed. After approved owner closure, adopt through reversible selection, prove parity and post-v2 switch-back, then remove individually proven generic files.

## Boundaries & Constraints

**Always:** Before adoption require G5 `available`, named owner/security approval, EventStore 8.11 closure, approved source/package identity, I2/I19a classification, runtime/backend/release enrollment, golden vectors, and rollback proof. Preserve plaintext, v1/v2, redacted/snapshot reads; typed failures, tenant isolation, key zeroing, PII-free diagnostics, exports, processing records, certificates, and default-on crypto-shredding. A missing key alone never proves erasure. Local v2 reads must work before shared-provider writes; production KMS is a separate release gate.

**Never:** Change production, dependencies, or gitlinks while G5 is closed; mistake the internal core, reader, or cursor Data Protection for a provider; delete rollback before dual-provider parity, seven deletion proofs, and post-v2 switch-back. Public contract, format, or erasure changes need separate approval. Exclude 8.8/8.9.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Closed gate | Missing G5 receipt | Keep blocked, local provider and DI intact | Record gap; no unrun-test credit |
| Mixed history | Plaintext, v1/v2, redacted, malformed event/snapshot | Equivalent state or bounded typed outcome | Distinguish missing, denied, outage, tamper, destroyed |
| Switch-back | Shared v2 writes | Local reads v1/v2, shared path resumes without rewrite | Mismatch blocks switch/deletion |

</frozen-after-approval>

## Code Map

- `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md:220` -- G5 `needs-additive-api`; :249 I2/I19a pending.
- `references/Hexalith.EventStore/_bmad-output/implementation-artifacts/sprint-status.yaml:306` -- owner 8.4 done; 8.5-8.11 backlog. Its reader remains non-packable.
- `references/Hexalith.EventStore/src/Hexalith.EventStore.PayloadProtection/Hexalith.EventStore.PayloadProtection.csproj:5` and `references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs:95` -- non-packable core; server no-op. No AzureKeyVault or release enrollment.
- `Directory.Packages.props:5` and `references/Hexalith.Builds/Props/Directory.Packages.props:9` -- Parties selects EventStore `3.117.1`; Builds defaults `3.119.0`. Current clean gitlinks: EventStore `b9651dbf15fa435744db744809bfaecaaef11bf6`, Builds `0d5f45c7612a0f9582690d5eb143948e7798946e`; neither is G5 approval.
- `src/Hexalith.Parties.Security/{PartyPayloadProtectionService.cs,EventStorePartyPayloadProtectionAdapter.cs}` and `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs:137` -- local v1 engine, adapter, active DI; retain for rollback.
- `tests/Hexalith.Parties.Security.Tests/CryptoKeyManagementCompatibilityHarnessTests.cs:699` -- local-only harness. `tests/Hexalith.Parties.IntegrationTests/Security/EncryptionPipelineIntegrationTests.cs` -- persisted workflow tests.
- `_bmad-output/implementation-artifacts/8-7-data-protection-extraction.md:228` -- 18 MOVE, five KEEP, adapter KEEP; :272 rollout order. Preserve public Contracts security types.

## Tasks & Acceptance

**Execution:**
- [ ] `_bmad-output/implementation-artifacts/{8-7-data-protection-extraction.md,story-8-3-platform-api-prerequisite-matrix.md,tests/test-summary.md}` -- record current gate, identities, missing receipts, and stale fitness assertions; keep 8.7 blocked.
- [ ] `references/Hexalith.EventStore/_bmad-output/implementation-artifacts/{sprint-status.yaml,8-11-g5-evidence-and-approval-closure.md}` and Parties G5 matrix -- verify owner approval, policy classification, enrollment, and consumed identity before adoption.
- [ ] `tests/Hexalith.Parties.Security.Tests/CryptoKeyManagementCompatibilityHarnessTests.cs` -- run legacy, v1/v2, AAD transplant, snapshot, key-state, no-leak vectors through both providers.
- [ ] `src/Hexalith.Parties.Security/EventStorePartyPayloadProtectionAdapter.cs`, `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs`, and `src/Hexalith.Parties/Domain/PartyDomainProcessor.cs` -- add reversible selection and v2-safe mapping; retain local default until parity.
- [ ] `tests/Hexalith.Parties.IntegrationTests/Security/EncryptionPipelineIntegrationTests.cs` and GDPR query/erasure tests -- prove persisted state, export, processing, certificate outcomes, post-v2 switch-back.
- [ ] `_bmad-output/implementation-artifacts/{story-8-3-platform-api-prerequisite-matrix.md,tests/test-summary.md}` -- record seven deletion proofs and identities before removing proven MOVE files; retain KEEP files and adapter.

**Acceptance Criteria:**
- Given a missing G5 receipt, when 8.7 runs, then it remains blocked with production, dependencies, and rollback intact.
- Given an approved provider, when both paths process mixed histories and GDPR workflows, then persisted state, typed outcomes, erasure evidence, and diagnostics match.
- Given persisted v2 writes, when selection switches local and back, then v1/v2 remain readable without rewrite; mismatch prevents adoption/deletion.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

**Commands:**
- `git ls-tree HEAD references/Hexalith.EventStore references/Hexalith.Builds` and clean checkout/status checks -- record exact selected identities.
- Build and directly run focused Security, Parties, and Integration xUnit v3 assemblies in Debug source mode after G5 opens -- no skipped parity proof; assert persisted end state.
- `pwsh scripts/test.ps1 -Lane unit`, `pwsh scripts/test.ps1 -Lane topology`, `bash scripts/check-no-warning-override.sh`, and `git diff --check` -- report each result and any environment blocker separately.
