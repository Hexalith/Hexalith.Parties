---
title: '8.7 Data-protection extraction at the G5 gate'
type: 'refactor'
created: '2026-10-11'
status: 'ready-for-dev'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '4a80e6a8e9c2e0a9ee0eb38735dd405ecc3d4e2e'
story_key: '8-7-data-protection-extraction'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/8-7-data-protection-extraction.md'
  - '{project-root}/_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Parties owns generic payload protection and keys. Premature extraction risks unreadable history, false erasure claims, and failed v2 rollback.

**Approach:** Revalidate G5 at consumed identities. While closed, record evidence and retain local protection. After approved owner closure, add reversible selection, prove parity and post-v2 switch-back, then remove proven generic files.

## Boundaries & Constraints

**Always:** Gate adoption on G5 `available`, EventStore 8.11 closure, owner/security approval, source/package identity, I2/I19a classification, and runtime/backend/release enrollment. Gate deletion on dual-provider parity, seven proofs, and post-v2 switch-back. Preserve plaintext, v1/v2, redacted and snapshot reads; typed failures, tenant isolation, key zeroing, no-leak diagnostics, GDPR behavior, certificates, and default-on crypto-shredding. Missing keys alone do not prove erasure. Prove local v2 reads before shared v2 writes; production KMS is a separate release gate.

**Never:** Change production, dependencies, or gitlinks while G5 is closed; mistake the internal core, reader, or cursor Data Protection for a provider; delete local rollback before proof. Public contract, format, and erasure changes need separate approval. Exclude 8.8/8.9.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Closed gate | Any missing G5 receipt | Keep Story 8.7 blocked and local DI/files intact | Name missing proof; credit no unrun test |
| Mixed history | Plaintext, v1/v2, redacted or malformed event/snapshot | Both providers give equivalent state or typed outcome | Distinguish missing, denied, outage, tamper, and destroyed |
| Switch-back | Persisted shared-provider v2 writes | Local reads v1/v2; switching forward works without rewrite | Any mismatch blocks adoption and deletion |

</frozen-after-approval>

## Code Map

- `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md:220` -- G5 `needs-additive-api`; I2/I19a pending at :249. Append dated evidence, preserve gates.
- `references/Hexalith.EventStore/_bmad-output/implementation-artifacts/sprint-status.yaml:261` -- owner 8.4 done; 8.5–8.11 backlog; 8.11 approval packet absent.
- `references/Hexalith.EventStore/src/Hexalith.EventStore.PayloadProtection/Hexalith.EventStore.PayloadProtection.csproj:5` and `references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs:95` -- non-packable core; Server no-op. Azure and release enrollment absent.
- `Directory.Packages.props:5` and `references/Hexalith.Builds/Props/Directory.Packages.props:9` -- EventStore package `3.117.1` versus Builds `3.120.0`; clean gitlinks EventStore `25f063e9fee2da1f4e00acb512083c2ca223d12a`, Builds `db20fea8b616dc0b0c57a72d65b0c785ff460f18`.
- `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs:187` and `src/Hexalith.Parties.Security/{PartyPayloadProtectionService.cs,EventStorePartyPayloadProtectionAdapter.cs}` -- active local provider and adapter; retain for rollback.

## Tasks & Acceptance

**Execution:**
- [ ] `_bmad-output/implementation-artifacts/8-7-data-protection-extraction.md`, `story-8-3-platform-api-prerequisite-matrix.md`, and `tests/test-summary.md` -- record current gate, identities, and missing receipts; retain blocked status.
- [ ] `references/Hexalith.EventStore/_bmad-output/implementation-artifacts/sprint-status.yaml` and `8-11-g5-evidence-and-approval-closure.md` -- verify owner closure, approvals, and consumed identity before production work; absence halts adoption.
- [ ] `tests/Hexalith.Parties.Security.Tests/CryptoKeyManagementCompatibilityHarnessTests.cs` -- after G5, run legacy, v1/v2, snapshot, AAD, key-state, and no-leak vectors through both providers.
- [ ] `src/Hexalith.Parties.Security/EventStorePartyPayloadProtectionAdapter.cs`, `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs`, and `src/Hexalith.Parties/Domain/PartyDomainProcessor.cs` -- after G5 approval, add reversible selection and v2 mapping; retain local default until parity and preserve APIs.
- [ ] `tests/Hexalith.Parties.IntegrationTests/Security/EncryptionPipelineIntegrationTests.cs` and GDPR query/erasure tests -- prove persisted state, exports, records, certificates, and post-v2 switch-back.
- [ ] `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md`, `tests/test-summary.md`, and `sprint-status.yaml` -- record seven deletion proofs before removing MOVE files; retain KEEP files and adapter.

**Acceptance Criteria:**
- Given G5 lacks any required receipt, when Story 8.7 runs, then status remains blocked, local DI and rollback files remain, and no unrun parity test is credited.
- Given an approved shared provider, when both paths process mixed histories and GDPR workflows, then persisted state, typed outcomes, erasure evidence, and diagnostics match.
- Given persisted v2 writes, when selection switches to local and back, then v1/v2 remain readable without rewrite; any mismatch prevents adoption and deletion.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

**Commands:**
- `git ls-tree HEAD references/Hexalith.EventStore references/Hexalith.Builds` plus clean checkout checks -- exact consumed source identities.
- Run the 32 G5 matrix commands and the focused `PlatformApiPrerequisitesTests.Matrix_ValidationEvidenceCommandsAreReproducible` direct xUnit v3 method -- structural gate receipt; report identity-guard failures separately.
- After G5 opens, build and directly run Security, Parties, and Integration test assemblies in Debug source mode -- real dual-provider and persisted GDPR parity with zero skipped proof rows.
- `bash scripts/check-no-warning-override.sh` and `git -c core.whitespace=cr-at-eol diff --check` -- no gate weakening or whitespace errors.
