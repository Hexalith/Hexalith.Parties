---
title: '8.7 Data-protection extraction'
type: 'refactor'
created: '2026-07-07T00:00:00+02:00'
status: 'draft'
review_loop_iteration: 0
followup_review_recommended: false
context:
  - '{project-root}/_bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-8-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-8-6-projection-and-query-sdk-migration.md'
warnings:
  - oversized
  - blocked-prerequisite
---

<intent-contract>

## Intent

**Problem:** `Hexalith.Parties.Security` owns generic data-protection mechanics — AES-256-GCM payload protection, key storage/wrapping/rotation, retry scheduling, a decryption circuit breaker, key-operation audit, and typed-unreadable handling — even though Epic 7 already introduced the `EventStorePartyPayloadProtectionAdapter` seam. A domain module owning reusable crypto infrastructure violates the domain-module contract.

**Approach:** Move the generic mechanics behind shared provider contracts (a shared DataProtection package or the approved EventStore provider), leaving Parties only its party-specific commands, GDPR legal policy, erasure orchestration semantics, domain-specific certificates/reports, and UX/copy — after compatibility proves every protected-payload and erasure behavior and a rollback path.

## Boundaries & Constraints

**Always:** Preserve spine invariant I8 — `json+pdenc-v1`, `json-redacted`, legacy unprotected reads, key zeroing, typed-unreadable outcomes, no-leak diagnostics, Art.20 exports, Art.30 processing records, and erasure reports/certificates — and I7 (two-front-door erasure + D7 cross-submodule verification, consent≠lawful-basis, Art.18 guards). Keep `Parties:CryptoShredding:IsEnabled` default-true (the crypto feature) distinct from `Parties:Compliance:GdprFeaturesActive` default-false (the MVP warning). Keep `LocalDevKeyStorageBackend` as the dev-only default and never ship it to production. Never log `[PersonalData]` or event payloads.

**Block If:** No shared DataProtection package or approved EventStore payload-protection provider exists with owner approval, parity proof, and a `references/Hexalith.EventStore` submodule-pin recorded in the 8.3 matrix (the matrix "EventStore DataProtection" row is cursor-scoped for 8.6; a payload-protection-provider parity row must be added and proven before 8.7 starts). Also HALT if the shared provider cannot express typed-unreadable/deleted-key redaction, the retry/circuit-breaker semantics, or per-tenant key rotation; or if the erasure certificate/report domain contract (`IErasureVerificationService`, approval-gated) would change.

**Never:** Do not migrate projection/query (8.6), client/MCP/AppHost/deploy (8.8), or UI (8.9). Do not move party-specific erasure orchestration, GDPR legal policy, or domain-specific certificates/reports unless an ADR explicitly moves them. Do not delete any local crypto/key file before the shared provider records parity + proven rollback. Do not disable crypto-shredding to "match the README."

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|----------------------------|----------------|
| Protected read | Current state with `json+pdenc-v1` payloads | Shared provider unprotects to identical plaintext state | No payload/key in logs or exceptions |
| Deleted-key redaction | Erased party, key destroyed | `json-redacted` typed-unreadable outcome; PII-free tombstone | `PartyEncryptionKeyDestroyedException` mapped to typed unreadable, not a 500 |
| Legacy unprotected | Pre-encryption event payloads | Read as-is, unchanged | No spurious decrypt attempt |
| Export / processing records | Art.20 export / Art.30 read on erased-or-live party | Domain semantics identical to pre-migration | Missing → existing no-op/empty semantics |
| Rollback | Provider regression detected | Revert to local `PartyPayloadProtectionService` + `PartyKeyManagementService` registration | No data loss; local path still present |

</intent-contract>

## Code Map

Target: shared DataProtection package / approved EventStore provider (owner: Hexalith.EventStore — parity row must be added to 8.3 and proven first). Existing seam kept: `src/Hexalith.Parties.Security/EventStorePartyPayloadProtectionAdapter.cs`.

MOVE behind provider (delete locally only after parity + rollback proof — I3 set):
- `PartyPayloadProtectionService.cs`, `PartyKeyManagementService.cs`, `CachedPartyKeyManagementService.cs`, `PartyKeyLifecycleService.cs` -- protection + key management engine.
- `IPartyKeyRetryScheduler.cs`, `ActorBackedPartyKeyRetryScheduler.cs`, `PartyKeyRetryActor.cs`, `IPartyKeyRetryActor.cs` -- retry scheduling.
- `DecryptionCircuitBreaker.cs`, `DecryptionCircuitOpenException.cs`, `KeyOperationAuditService.cs`, `TenantKeyRotationService.cs`, `TenantKeyRotationProgress.cs`, `TenantKeyRotationProgressConflictException.cs`, `ITenantKeyRotationCacheInvalidator.cs` -- rotation/audit/circuit mechanics.
- `LocalDevKeyStorageBackend.cs`, `PartyEncryptionKeyDestroyedException.cs`, `CryptoPendingRecord.cs` -- key store + typed outcomes (keep dev-only default semantics until provider proves the equivalent).

KEEP (domain — do NOT move without ADR):
- `PartyErasureOrchestrator.cs`, `ErasureVerificationService.cs`, `PartyErasureRecordStore.cs`, `PartyPersonalDataCommandGuard.cs`, `PersonalDataGraphInspector.cs` -- party-specific GDPR policy, erasure orchestration, certificates/reports.

Evidence: `story-8-3-platform-api-prerequisite-matrix.md` (add payload-protection-provider parity + pin), `tests/.../test-summary.md`, `sprint-status.yaml`.

## Tasks & Acceptance

**Execution:** (gated by the Block-If prerequisite)
- [ ] `story-8-3-platform-api-prerequisite-matrix.md` -- add + prove the shared payload-protection-provider parity row (formats, key zeroing, typed-unreadable, rotation, audit, no-leak, exports, processing records, rollback) + pin proof.
- [ ] `tests/Hexalith.Parties.Security.Tests/` (parity harness) -- prove `json+pdenc-v1`, `json-redacted`, legacy unprotected, key zeroing, typed unreadable, no-leak diagnostics, export/processing-record semantics against BOTH local and provider paths.
- [ ] Rebind `EventStorePartyPayloadProtectionAdapter` (and registrations in `PartiesServiceCollectionExtensions`) to the shared provider; keep local registration until parity.
- [ ] Delete the MOVE-listed files only after parity harness is green and rollback is verified.

**Acceptance Criteria:**
- Given the payload-protection-provider parity row is unproven, when 8.7 is attempted, then it HALTs `blocked` with that prerequisite as the blocking condition.
- Given proven parity, when protected/redacted/legacy payloads are read, then plaintext state, typed-unreadable outcomes, and PII-free tombstones are behaviorally identical to pre-migration.
- Given erasure and export flows, when run on the provider, then certificates, reports, exports, and processing records preserve domain semantics and leak no PII.
- Given the migration completes, when inspected, then Parties.Security retains only domain GDPR policy/orchestration and no generic key-management engine.

## Design Notes

- **§4 gate mapping:** (1) Prereq: shared payload-protection provider parity + pin (predecessors 8.3, 8.5 done; independent of 8.6). (2) Repos: `Parties` + `Hexalith.EventStore`. (3) Rollback: MOVE files stay until parity; revert = restore local registration. (4) Lanes: `Hexalith.Parties.Security.Tests` EXE directly, no-leak diagnostics check, topology. (5) Non-goals: 8.6/8.8/8.9. (6) Parity checklist: I8 + I7.

## Verification

**Commands:**
- `dotnet build src/Hexalith.Parties.Security/Hexalith.Parties.Security.csproj -c Release -m:1` -- expected: green.
- `Hexalith.Parties.Security.Tests` EXE run directly with `-class` for the parity harness -- expected: format/key/typed-unreadable/no-leak/export/processing tests pass.

**Manual checks:**
- Confirm no generic key-management/circuit-breaker/rotation engine remains in `Hexalith.Parties.Security` after deletion, and the dev-only key-store warning is preserved.
