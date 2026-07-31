---
title: Sprint Change Proposal — Revalidate Parties Crypto / Key-Management Retention
date: 2026-07-31
author: Administrator
workflow: bmad-correct-course
mode: incremental
scope_classification: moderate
status: approved
approved: 2026-07-31T22:24:02+02:00
trigger: >
  Keep Parties crypto/key-management implementation until an approved shared
  provider proves payload compatibility, typed unreadable outcomes, no-leak
  diagnostics, exports, processing records, certificates, and rollback.
supersedes: null
revalidates:
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-crypto-key-management-retention.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-16-crypto-key-management-retention-revalidation.md
related:
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md
  - _bmad-output/implementation-artifacts/spec-8-7-data-protection-extraction.md
  - _bmad-output/implementation-artifacts/8-7-data-protection-extraction.md
  - _bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md
  - _bmad-output/implementation-artifacts/sprint-status.yaml
  - references/Hexalith.EventStore/_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md
  - references/Hexalith.EventStore/_bmad-output/implementation-artifacts/sprint-status.yaml
---

# Sprint Change Proposal — Revalidate Parties Crypto / Key-Management Retention

## 1. Issue Summary

Story 8.7, **Data-protection extraction**, may remove generic crypto and
key-management mechanics from `Hexalith.Parties.Security` only after an exact,
approved shared provider proves that the migration preserves the complete
Parties data-protection contract. Removing the local path earlier could make
persisted payloads unreadable, collapse typed failure semantics, leak sensitive
diagnostics, break GDPR reads or proof artifacts, and eliminate the only usable
rollback path.

The governing course remains:

> Parties retains its crypto/key-management implementation and usable rollback
> registration until an approved shared provider proves payload compatibility,
> typed unreadable outcomes, no-leak diagnostics, exports, processing records,
> certificates/reports, and rollback.

This proposal revalidates the approved 2026-07-07 and 2026-07-16 guardrails. It
does not supersede or weaken them.

### Trigger and current evidence

- The triggering work is Story 8.7, currently `blocked` behind Story 8.6 and the
  G5 provider prerequisite.
- The Story 8.3 prerequisite matrix still classifies the EventStore payload
  protection engine package as `needs-additive-api`.
- The root-declared EventStore gitlink and checkout both resolve to
  `e4618d9114c8824fd50fdfc8d135438aa261377c` (`v3.86.0`).
- EventStore now contains a proposed owner specification, but its normative
  frontmatter remains `status: draft-not-authorized`, `decision: proposed`, and
  `story_8_2_authorized: false`; every mandatory approval is still pending.
- EventStore sprint status keeps Story 8.1 `in-progress` and the runtime engine
  and Parties G5 parity Story 8.2 in `backlog`.
- The required Story 8.2 parity proof packet does not exist.
- Checked-out EventStore source contains provider-neutral protection contracts,
  typed outcome transport, and a no-op implementation, but no approved runtime
  G5 engine, `pdenc-v2`, `IPersonalDataPolicy`, or `IErasureStateProvider`.
- All 18 rollback-only MOVE files, all 5 Parties-domain KEEP files, and the
  `EventStorePartyPayloadProtectionAdapter` seam remain present.
- Active Parties DI still registers `LocalDevKeyStorageBackend`, the concrete
  local protection/key-management services, and
  `IEventPayloadProtectionService -> EventStorePartyPayloadProtectionAdapter`.
- The focused Parties compatibility harness passed 19/19 tests on 2026-07-31.

The change trigger is therefore a stakeholder safety constraint and technical
prerequisite revalidation, not a failed feature or new MVP requirement.

## 2. Impact Analysis

### Epic impact

- **Epic 8 only.** It remains post-MVP maintenance work with zero new PRD
  functional requirements.
- No epic is added, removed, redefined, or re-sequenced.
- The existing sequence remains authoritative: Story 8.6 must complete before
  Story 8.7 can start, and Story 8.7 must satisfy G5 before downstream deletion
  or cleanup can rely on the shared provider.

### Story impact

- **Story 8.7 remains blocked.** It is not authorized to delete any MOVE-listed
  file or disable the Parties-local provider path.
- Stories 7.6 and 7.7 remain done. Their adapter-first, reversible design is
  ratified and is not reopened.
- Story 8.3 remains done as a routing/matrix story; only its living G5 evidence
  row is refreshed.
- Story 8.6 and later Epic 8 stories retain their current status and ordering.

### Artifact impact

- **PRD:** no change. The action adds no product behavior or MVP scope.
- **Architecture:** no change. Existing Epic 8 invariants I3, I4, I7, and I8
  already require exact dependency identity, domain/legal ownership, parity,
  and rollback.
- **UX:** no change. Existing honest unavailable/erased outcomes and no-leak
  language remain compatibility evidence rather than a redesign request.
- **Epics/stories:** no acceptance-criteria change. The current Story 8.7 gates
  already express the requested retention condition.
- **Implementation tracking:** refresh the Story 8.3 G5 evidence and the matching
  open sprint action comment to the 2026-07-31 repository state.

### Technical impact

The following rollback-only MOVE inventory stays in Parties until the complete
G5 proof is accepted:

- Protection/key engine: `PartyPayloadProtectionService.cs`,
  `PartyKeyManagementService.cs`, `CachedPartyKeyManagementService.cs`, and
  `PartyKeyLifecycleService.cs`.
- Retry scheduling: `IPartyKeyRetryScheduler.cs`,
  `ActorBackedPartyKeyRetryScheduler.cs`, `PartyKeyRetryActor.cs`, and
  `IPartyKeyRetryActor.cs`.
- Rotation/audit/circuit behavior: `DecryptionCircuitBreaker.cs`,
  `DecryptionCircuitOpenException.cs`, `KeyOperationAuditService.cs`,
  `TenantKeyRotationService.cs`, `TenantKeyRotationProgress.cs`,
  `TenantKeyRotationProgressConflictException.cs`, and
  `ITenantKeyRotationCacheInvalidator.cs`.
- Key store and typed outcomes: `LocalDevKeyStorageBackend.cs`,
  `PartyEncryptionKeyDestroyedException.cs`, and `CryptoPendingRecord.cs`.

The following domain-owned KEEP inventory remains in Parties unless a later
explicit ADR changes ownership:

- `PartyErasureOrchestrator.cs`
- `ErasureVerificationService.cs`
- `PartyErasureRecordStore.cs`
- `PartyPersonalDataCommandGuard.cs`
- `PersonalDataGraphInspector.cs`

`EventStorePartyPayloadProtectionAdapter.cs` remains the reversible seam.
`Parties:CryptoShredding:IsEnabled` remains separate from
`Parties:Compliance:GdprFeaturesActive`. `LocalDevKeyStorageBackend` remains
development-only and is not a production KMS.

## 3. Recommended Approach

### Decision: Direct Adjustment

Keep the current Parties implementation and refresh only the living planning
evidence. Do not start extraction, delete rollback code, switch the default
provider, migrate key material, or close the sprint action.

### Why this path

- The existing Epic 8 plan already contains the correct gated migration story.
- The proposed EventStore design is useful progress but is neither authorized
  nor implemented.
- Existing provider-neutral contracts are transport seams, not proof of the G5
  runtime engine.
- Retention preserves readable history and the only tested recovery path while
  creating no new product scope.
- Rolling back Stories 7.6/7.7 would remove the seam needed for a future safe
  migration and is therefore not viable.
- MVP review is not applicable because Epic 8 is post-MVP maintenance and this
  proposal changes no functional requirement.

### Required exit proof before any deletion

Story 8.7 may authorize MOVE-file deletion only after an exact approved shared
provider identity records all of the following in the G5 matrix and test-summary
evidence:

1. **Payload compatibility:** new `pdenc-v2` writes, readable historic
   `json+pdenc-v1` events/snapshots, legacy/unprotected reads, stable AAD identity
   binding, and explicitly bounded unsupported-format behavior.
2. **Typed unreadable outcomes:** key deleted/invalidated, provider unavailable,
   forbidden, malformed, unknown version, and consistency failures remain
   distinguishable without exception-text parsing.
3. **No-leak diagnostics:** logs, traces, metrics, errors, and provider metadata
   disclose no payload, personal data, raw keys/wrapped key bytes, aliases,
   tokens, credentials, or raw provider failures.
4. **Art.20 exports:** readable, restricted, erased, and personal-data-unavailable
   outcomes preserve the approved package and no-partial-payload semantics.
5. **Art.30 processing records:** bounded audit metadata remains available,
   including after erasure, without decrypting or leaking erased personal data.
6. **Certificates/reports:** erasure certificates, verification reports, and
   Parties-owned legal lifecycle semantics preserve their typed, bounded
   contract.
7. **Rollback:** a tested provider switch restores the Parties-local path for
   historic and post-`pdenc-v2` data, with an approved key-material recovery or
   dual-read strategy rather than a DI-only rollback claim.

The provider must also expose a pluggable production key backend seam and be
approved for an exact release or root-submodule identity. Mock-only, planning,
or status evidence cannot satisfy the gate.

### Effort, risk, and schedule

- **Scope classification:** Moderate — planning/backlog evidence coordination
  across Parties and EventStore owners; no code change in this proposal.
- **Immediate effort:** small; two tracking artifacts and this proposal are
  refreshed.
- **Migration effort:** unchanged and provider-dependent; it remains blocked
  until EventStore Stories 8.1 and 8.2 deliver authorization and proof.
- **Schedule impact:** no new delay beyond the existing Story 8.7 prerequisite.
- **Risk if followed:** low; local behavior and rollback remain available.
- **Risk if bypassed:** high; irreversible unreadability, privacy leakage,
  broken GDPR evidence, or an untestable rollback could result.

## 4. Detailed Change Proposals

### 4.1 Story 8.3 prerequisite matrix — G5 row

**Old evidence:** the row was last revalidated on 2026-07-16 at EventStore SHA
`82ed167c` and stated that no G5 engine or approved packet existed.

**Approved edit:** record the current `e4618d91` / `v3.86.0` identity, the new but
still unauthorized EventStore specification, Story 8.2 backlog status, absent
proof packet, missing runtime surfaces, intact Parties retention inventory, and
the focused 19/19 compatibility-harness result. Keep status
`needs-additive-api` and explicitly state that no deletion is authorized.

**Rationale:** remove stale producer evidence without confusing proposed design
with approved, delivered, and parity-proven implementation.

### 4.2 Sprint-status open action

**Old evidence:** the action comment referenced the 2026-07-16 EventStore SHA
and pre-specification state.

**Approved edit:** update the comment to the 2026-07-31 identity and authorization
state, absent proof packet, missing runtime surfaces, intact rollback inventory,
and passing focused harness. Preserve the exact action text, owners, and
`status: open`.

**Rationale:** keep the execution ledger synchronized with the G5 matrix while
retaining the stakeholder guardrail verbatim.

### 4.3 Explicit non-changes

- No PRD, architecture, UX, epic, story acceptance-criteria, source-code,
  configuration, package, submodule, or dependency edit.
- No migration, deletion, provider switch, key-material operation, status
  unblocking, or action closure.
- The unrelated approved
  `sprint-change-proposal-2026-07-31.md` remains untouched; this proposal uses a
  descriptive collision-safe filename.

## 5. Implementation Handoff

### Classification and recipients

**Moderate** — backlog/evidence coordination is required, but no fundamental
product or architecture replan is needed.

- **Winston / Parties Architect:** maintain G5 as `needs-additive-api`, validate
  the exact provider identity and approval chain, and prevent premature Story
  8.7 activation.
- **Amelia / Parties Developer:** retain the MOVE/KEEP/seam inventory and local
  DI rollback path; run the dual-provider harness only when an approved provider
  candidate exists.
- **EventStore owner and Security Reviewer:** finish and authorize Story 8.1,
  implement Story 8.2, publish the exact provider identity, and produce the
  required proof packet without leaking protected material.
- **Test Architect / independent reviewer:** reproduce all seven proof groups
  against real persisted data and both provider paths.
- **Release and Operations owners:** approve the production key-backend identity,
  operational recovery, and post-`pdenc-v2` rollback procedure.

### Success criteria

This proposal is successfully implemented when:

1. The G5 matrix and sprint action contain the approved 2026-07-31 evidence.
2. Story 8.7 and the retention action remain blocked/open respectively.
3. No MOVE, KEEP, or seam file and no local rollback registration is removed.
4. The focused Parties compatibility harness remains green.
5. Future extraction begins only after an exact approved provider and complete
   proof packet satisfy all seven exit-proof groups.

Until then, the operational instruction is unchanged: **keep the Parties
crypto/key-management implementation.**
