---
title: '8.7 G5 revalidation and data-protection extraction'
type: 'refactor'
created: '2026-10-10'
status: 'blocked'
baseline_commit: '56cb4401c4179d431385ad3d931fefa402e4fc5a'
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
- `references/Hexalith.EventStore/_bmad-output/implementation-artifacts/sprint-status.yaml:312` -- owner 8.4 done; 8.5-8.11 backlog. Its reader remains non-packable.
- `references/Hexalith.EventStore/src/Hexalith.EventStore.PayloadProtection/Hexalith.EventStore.PayloadProtection.csproj:5` and `references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs:95` -- non-packable core; server no-op. No AzureKeyVault or release enrollment.
- `Directory.Packages.props:5` and `references/Hexalith.Builds/Props/Directory.Packages.props:9` -- Parties selects EventStore `3.117.1`; Builds defaults `3.119.0`. Current clean gitlinks: EventStore `785260fa233880e403bf80bd1bc2f1d53b1f4836`, Builds `6a002df5fa60f2f880f09ffd90b3bfaf819bcbf5`; neither is G5 approval.
- `src/Hexalith.Parties.Security/{PartyPayloadProtectionService.cs,EventStorePartyPayloadProtectionAdapter.cs}` and `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs:137` -- local v1 engine, adapter, active DI; retain for rollback.
- `tests/Hexalith.Parties.Security.Tests/CryptoKeyManagementCompatibilityHarnessTests.cs:699` -- local-only harness. `tests/Hexalith.Parties.IntegrationTests/Security/EncryptionPipelineIntegrationTests.cs` -- persisted workflow tests.
- `_bmad-output/implementation-artifacts/8-7-data-protection-extraction.md:252` -- 18 MOVE, five KEEP, adapter KEEP; :298 rollout order. Preserve public Contracts security types.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/implementation-artifacts/{8-7-data-protection-extraction.md,story-8-3-platform-api-prerequisite-matrix.md,tests/test-summary.md}` -- record current gate, identities, missing receipts, and stale fitness assertions; keep 8.7 blocked.
- [x] `references/Hexalith.EventStore/_bmad-output/implementation-artifacts/{sprint-status.yaml,8-11-g5-evidence-and-approval-closure.md}` and Parties G5 matrix -- verify owner approval, policy classification, enrollment, and consumed identity before adoption.
- [ ] `tests/Hexalith.Parties.Security.Tests/CryptoKeyManagementCompatibilityHarnessTests.cs` -- run legacy, v1/v2, AAD transplant, snapshot, key-state, no-leak vectors through both providers.
- [ ] `src/Hexalith.Parties.Security/EventStorePartyPayloadProtectionAdapter.cs`, `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs`, and `src/Hexalith.Parties/Domain/PartyDomainProcessor.cs` -- add reversible selection and v2-safe mapping; retain local default until parity.
- [ ] `tests/Hexalith.Parties.IntegrationTests/Security/EncryptionPipelineIntegrationTests.cs` and GDPR query/erasure tests -- prove persisted state, export, processing, certificate outcomes, post-v2 switch-back.
- [ ] `_bmad-output/implementation-artifacts/{story-8-3-platform-api-prerequisite-matrix.md,tests/test-summary.md}` -- record seven deletion proofs and identities before removing proven MOVE files; retain KEEP files and adapter.

**Acceptance Criteria:**
- Given a missing G5 receipt, when 8.7 runs, then it remains blocked with production, dependencies, and rollback intact.
- Given an approved provider, when both paths process mixed histories and GDPR workflows, then persisted state, typed outcomes, erasure evidence, and diagnostics match.
- Given persisted v2 writes, when selection switches local and back, then v1/v2 remain readable without rewrite; mismatch prevents adoption/deletion.

## Implementation Notes

- 2026-10-10: G5 remains closed. EventStore 8.4 now has an internal mixed-history
  compatibility router, but the core is non-packable and has no approved
  runtime/backend/release enrollment. Stories 8.5-8.11 and the named G5 and
  I2/I19a approvals remain open. No shared-provider adoption, v2 write mode,
  production code, dependency, gitlink, or rollback-file change was made.
- The pre-existing owner 8.4 `backlog` fitness assertion was updated to `done`.
  The current root-gitlink fitness guard still fails on an observed, unapproved
  Builds identity; its expected approval identity was preserved. The historical
  32-check reproduction procedure also assumes both package selectors are
  `3.117.1`; Builds now defaults to `3.119.0`.
- Remaining execution tasks are gated by EventStore 8.11 closure, G5
  `available`, approved identity and policy classification, producer-consumer
  parity, and post-v2 switch-back. No unrun dual-provider or GDPR test is
  credited as proof.

## Spec Change Log

- 2026-10-10: Revalidated the closed G5 gate at the current clean EventStore
  and Builds gitlinks, recorded owner 8.4 completion and remaining receipts,
  repaired the one stale owner-status fitness assertion, and kept adoption
  blocked. Frozen intent and acceptance text are unchanged.

## Review Triage Log

## Verification

**Commands:**
- `git ls-tree HEAD references/Hexalith.EventStore references/Hexalith.Builds` and clean checkout/status checks -- record exact selected identities.
- Build and directly run focused Security, Parties, and Integration xUnit v3 assemblies in Debug source mode after G5 opens -- no skipped parity proof; assert persisted end state.
- `pwsh scripts/test.ps1 -Lane unit`, `pwsh scripts/test.ps1 -Lane topology`, `bash scripts/check-no-warning-override.sh`, and `git diff --check` -- report each result and any environment blocker separately.

**Results (2026-10-10):**
- G5 static audit: 32/32 matrix commands passed; 24 retained files, local
  harness, four DI registrations, clean matching EventStore/Builds checkouts,
  and package selectors `3.117.1`/`3.119.0` verified. The older procedure
  fails only its dated both-`3.117.1` selector assertion.
- Debug source-mode Parties fitness build: 0 warnings/errors. Focused G5
  matrix method: 1/1 passed. Full prerequisite class: 32/33 passed; the
  remaining final-set identity test expects Builds `fef03180` while HEAD
  selects `6a002df5`. Exact identities and commands are in
  `tests/test-summary.md`.
- `pwsh scripts/test.ps1 -Lane unit`: exit 1. Contracts 181/181,
  Authentication 12/12, Client 171/171 passed; Server compilation stopped on
  CS0246/CS1061 for EventStore identity-history types absent from the
  selected Release package graph. Remaining unit projects did not run.
- `pwsh scripts/test.ps1 -Lane topology`: exit 1 before tests on the same
  CS0246/CS1061 compilation errors.
- `bash scripts/check-no-warning-override.sh`: exit 0. `git diff --check`:
  exit 2 on required CRLF endings; `git -c core.whitespace=cr-at-eol diff
  --check`: exit 0.
- Dual-provider mixed-history, GDPR, and post-v2 switch-back matrix rows are
  not run and remain unproven because G5 is closed.
