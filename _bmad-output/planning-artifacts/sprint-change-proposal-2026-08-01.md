---
title: Sprint Change Proposal — Revalidate Parties Crypto / Key-Management Retention
date: 2026-08-01
author: Administrator
workflow: bmad-correct-course
mode: batch
scope_classification: moderate
status: approved
approved: 2026-08-01T07:48:37+02:00
trigger: >
  Keep Parties crypto/key-management implementation until an approved shared
  provider proves payload compatibility, typed unreadable outcomes, no-leak
  diagnostics, exports, processing records, certificates, and rollback.
supersedes: null
revalidates:
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-crypto-key-management-retention.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-16-crypto-key-management-retention-revalidation.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-31-crypto-key-management-retention-revalidation.md
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

This proposal revalidates the approved 2026-07-07, 2026-07-16, and 2026-07-31
guardrails. It does not supersede or weaken them.

### Trigger and current evidence

- The triggering work is Story 8.7, which remains `blocked` behind the G5
  provider prerequisite and the authoritative `8.6 -> 8.7` sequence. Story 8.6
  is now `in-progress`, not complete.
- The Story 8.3 prerequisite matrix still classifies the EventStore payload
  protection engine package as `needs-additive-api`.
- The Parties root now pins EventStore commit
  `fa2d1c9910f8976553adb33dcdb1c9ff2ea75594`; the checkout matches that clean
  gitlink and describes as `v3.82.0-24-gfa2d1c99`.
- The selected EventStore identity closes projection/query Story 1.20 evidence.
  It does not close G5: the payload-protection security specification remains
  `status: draft-not-authorized`, `decision: proposed`, and
  `story_8_2_authorized: false`, with all named approvals still pending.
- EventStore sprint status keeps Story 8.1 `in-progress` and Story 8.2, the
  runtime engine and Parties G5 parity story, in `backlog`.
- The required Story 8.2 G5 proof packet is absent.
- Checked-out EventStore source contains provider-neutral protection contracts,
  typed outcome transport, and a no-op provider, but no approved runtime G5
  engine, `pdenc-v2`, `IPersonalDataPolicy`, or `IErasureStateProvider`.
- All 18 rollback-only MOVE files, all 5 Parties-domain KEEP files, and the
  `EventStorePartyPayloadProtectionAdapter` seam remain present.
- Active Parties DI still registers `LocalDevKeyStorageBackend`, the concrete
  local protection/key-management services, and
  `IEventPayloadProtectionService -> EventStorePartyPayloadProtectionAdapter`.
- The focused Parties compatibility harness passed 19/19 tests on 2026-08-01.

The change trigger is therefore a stakeholder safety constraint and technical
prerequisite revalidation, not a failed feature or new MVP requirement.

## 2. Impact Analysis

### Epic impact

- **Epic 8 only.** It remains post-MVP maintenance work with zero new PRD
  functional requirements.
- No epic is added, removed, redefined, or re-sequenced.
- The sequence remains authoritative: Story 8.6 must complete before Story 8.7
  starts Parties production migration unless an approved architecture/product
  artifact explicitly changes that sequence.

### Story impact

- **Story 8.7 remains blocked.** It is not authorized to delete any MOVE-listed
  file, disable the Parties-local provider path, or write a new shared-provider
  format.
- **Story 8.6 remains in progress.** Its projection/query work does not satisfy
  the independent G5 payload-protection gate.
- Stories 7.6 and 7.7 remain done. Their adapter-first, reversible design is
  ratified and is not reopened.
- Story 8.3 remains done as a routing/matrix story; only its living G5 evidence
  row needs refreshing.

### Artifact impact

- **PRD:** no change. The action adds no product behavior or MVP scope.
- **Architecture:** no change. Existing Epic 8 invariants I3, I4, I7, and I8
  already require exact dependency identity, domain/legal ownership, parity,
  and rollback.
- **UX:** no change. Existing honest unavailable/erased outcomes and no-leak
  language remain compatibility evidence rather than a redesign request.
- **Epics and Story 8.7 acceptance criteria:** no change. The current gates
  already express the requested retention condition.
- **Implementation tracking:** refresh the Story 8.3 G5 evidence and the
  matching open sprint-action comment to the 2026-08-01 repository state.
  Preserve the concurrent Story 8.6 `in-progress` change and other unrelated
  user-owned edits.

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
evidence. Do not start Story 8.7 production migration, delete rollback code,
switch the default provider, migrate key material, write `pdenc-v2`, or close
the sprint action.

### Why this path

- The existing Epic 8 plan already contains the correct gated migration story.
- The exact EventStore pin is approved for projection/query Story 1.20, not for
  G5 payload protection.
- The proposed EventStore G5 design is useful progress but remains neither
  authorized nor implemented.
- Existing provider-neutral contracts are transport seams, not proof of the G5
  runtime engine or the Parties legal-domain reads.
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
   `json+pdenc-v1` events/snapshots, `json-redacted`, legacy/unprotected reads,
   stable AAD identity binding, and bounded unsupported-format behavior.
2. **Typed unreadable outcomes:** key deleted/invalidated, missing key, provider
   unavailable, provider denied, malformed marker, unknown version, integrity
   failure, and consistency failure remain distinguishable without parsing
   exception text.
3. **No-leak diagnostics:** logs, traces, metrics, errors, ProblemDetails, and
   provider metadata disclose no payload, personal data, raw keys/wrapped key
   bytes, aliases, tokens, credentials, actor identifiers, or raw provider
   failures.
4. **Art.20 exports:** readable, restricted, erased, and personal-data-unavailable
   outcomes preserve the approved package and no-partial-payload semantics.
5. **Art.30 processing records:** bounded audit metadata remains available,
   including after erasure, without decrypting or leaking erased personal data.
6. **Certificates/reports:** erasure certificates, verification reports, and
   Parties-owned legal lifecycle semantics preserve their typed, bounded
   contract.
7. **Rollback:** after real `pdenc-v2` writes, a tested provider switch restores
   the retained Parties-local path for historic and new data through an approved
   v2 reader or reversible dual-read/write strategy. A DI-only switch before v2
   writes is insufficient.

The provider must also expose a pluggable production key-backend seam and be
approved for an exact release or root-submodule identity. Mock-only, planning,
interface-only, or status evidence cannot satisfy the gate.

### Effort, risk, and schedule

- **Scope classification:** Moderate — planning/backlog evidence coordination
  across Parties and EventStore owners; no code change in this proposal.
- **Immediate effort:** small; two living tracking passages require refresh.
- **Migration effort:** unchanged and provider-dependent; it remains blocked
  until EventStore Stories 8.1 and 8.2 deliver authorization and proof.
- **Schedule impact:** no new delay beyond the existing Story 8.7 prerequisites.
- **Risk if followed:** low; local behavior and rollback remain available.
- **Risk if bypassed:** high; irreversible unreadability, privacy leakage,
  broken GDPR evidence, or an untestable rollback could result.

## 4. Detailed Change Proposals

### 4.1 Story 8.3 prerequisite matrix — G5 row

**OLD evidence passage:**

> Revalidated for Story 8.7 on 2026-07-31 at root gitlink and checkout
> `e4618d9114c8824fd50fdfc8d135438aa261377c` (`v3.86.0`).

**NEW evidence passage:**

> Revalidated for Story 8.7 on 2026-08-01 at matching root gitlink and clean
> checkout `fa2d1c9910f8976553adb33dcdb1c9ff2ea75594`
> (`v3.82.0-24-gfa2d1c99`). This identity is the approved projection/query Story
> 1.20 source, not a G5 payload-engine approval. The EventStore G5 specification
> remains `draft-not-authorized`, `decision: proposed`, and
> `story_8_2_authorized: false`; Story 8.1 is in progress, Story 8.2 is backlog,
> and the required Story 8.2 G5 proof packet is absent. The checked-out source
> still has provider-neutral contracts and typed outcomes but no approved
> runtime G5 engine, `pdenc-v2`, `IPersonalDataPolicy`, or
> `IErasureStateProvider`. Parties retains all 18 MOVE files, all 5 KEEP files,
> and the adapter seam; active DI remains locally reversible; the focused
> compatibility harness passes 19/19 tests. The row remains
> `needs-additive-api`, and no Parties crypto/key-management deletion is
> authorized.

Update only the identity-sensitive validation-command outputs in that row.
Retain the required proof text, dependent stories, rollback decision, and
`needs-additive-api` status.

**Rationale:** remove stale producer identity without confusing an approved
projection/query pin with an approved, delivered, and parity-proven payload
engine.

### 4.2 Sprint-status open retention action

**OLD comment passage:**

> Revalidated 2026-07-31 at root gitlink/checkout `e4618d91` (`v3.86.0`).

**NEW comment passage:**

> Revalidated 2026-08-01 at matching root gitlink/clean checkout `fa2d1c99`
> (`v3.82.0-24-gfa2d1c99`). The pin satisfies projection/query Story 1.20
> provenance but does not deliver G5. EventStore Story 8.1 remains
> draft/not-authorized and in progress; Story 8.2 remains backlog; its G5 proof
> packet is absent; and no approved runtime G5 engine, `pdenc-v2`,
> `IPersonalDataPolicy`, or `IErasureStateProvider` exists. All 18 MOVE files, 5
> KEEP files, and the adapter seam remain intact; active local DI is reversible;
> the focused compatibility harness passes 19/19. Story 8.6 is in progress,
> Story 8.7 remains blocked, and the retention action stays open.

Preserve the exact action text, owners, and `status: open`. Preserve the
concurrent Story 8.6 status change and unrelated sprint-status edits.

**Rationale:** synchronize the execution ledger with the G5 matrix while
retaining the stakeholder guardrail verbatim.

### 4.3 Explicit non-changes

- No PRD, architecture, UX, epic, Story 8.7 acceptance-criteria, source-code,
  configuration, package, submodule, or dependency edit.
- No migration, deletion, provider switch, key-material operation, G5 status
  promotion, Story 8.7 unblocking, or action closure.
- No overwrite of concurrent user-owned changes in `sprint-status.yaml` or
  unrelated planning/implementation artifacts.

### 4.4 Change-analysis checklist

| Item | Status | Finding |
| --- | --- | --- |
| 1.1–1.3 Trigger, problem, evidence | [x] Done | Story 8.7; premature-deletion risk; current pin, authorization, source, inventory, DI, and focused-test evidence recorded. |
| 2.1 Current epic viable | [x] Done | Yes, after—not before—the sequence and G5 provider gates. |
| 2.2–2.5 Epic changes/order | [N/A] Skip | No epic change, addition, removal, or resequencing. |
| 3.1 PRD | [x] Done | No conflict or MVP impact. |
| 3.2 Architecture | [x] Done | I3/I4/I7/I8 and the accepted ADR already govern. |
| 3.3 UI/UX | [N/A] Skip | No new surface; existing no-leak and honest-outcome obligations remain parity evidence. |
| 3.4 Other artifacts | [x] Done | Refreshed the Story 8.3 G5 evidence and matching sprint-action comment. |
| 4.1 Direct adjustment | [x] Viable | Selected: retain implementation and refresh evidence. |
| 4.2 Potential rollback | [N/A] Skip | The retained implementation is the required rollback path. |
| 4.3 MVP review | [N/A] Skip | Completed feature scope is unaffected. |
| 4.4 Recommended path | [x] Done | Keep Story 8.7 blocked until every exit proof passes. |
| 5.1–5.5 Proposal components | [x] Done | Issue, impact, recommendation, edits, and handoff are present. |
| 6.1–6.2 Final review | [x] Done | Draft cross-checked against current artifacts and source evidence. |
| 6.3 Explicit approval | [x] Done | Administrator approved on 2026-08-01T07:48:37+02:00. |
| 6.4 Sprint-status update | [x] Done | Approved retention-comment refresh merged without overwriting concurrent user changes. |
| 6.5 Handoff | [x] Done | Moderate-scope recipients, responsibilities, and success criteria are defined. |

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
- **EventStore owner and Security Reviewer:** approve Story 8.1, implement Story
  8.2, publish the exact provider identity, and produce the required proof packet
  without leaking protected material.
- **Test Architect / independent reviewer:** reproduce all seven proof groups
  against real persisted data and both provider paths.
- **Release and Operations owners:** approve the production key-backend identity,
  operational recovery, and post-`pdenc-v2` rollback procedure.

### Success criteria

This proposal is successfully implemented when:

1. The G5 matrix and sprint-action comment contain the approved 2026-08-01
   evidence without overwriting concurrent user changes.
2. Story 8.7 and the retention action remain `blocked` and `open`, respectively.
3. No MOVE, KEEP, or seam file and no local rollback registration is removed.
4. The focused Parties compatibility harness remains green.
5. Future extraction begins only after Story 8.6 sequence completion or approved
   resequencing, plus an exact approved G5 provider and complete proof packet
   satisfying all seven exit-proof groups.

Until then, the operational instruction is unchanged: **keep the Parties
crypto/key-management implementation.**

## 6. Approval and Workflow Execution Log

- **Decision:** Approved by Administrator on 2026-08-01T07:48:37+02:00.
- **Issue addressed:** Parties crypto/key-management deletion remains unsafe
  because the pinned EventStore source has no authorized, proven G5 replacement.
- **Change scope:** Moderate.
- **Artifacts modified:** this proposal, the Story 8.3 matrix G5 evidence row,
  and the sprint-status crypto-retention comment.
- **Artifacts deliberately unchanged:** PRD, epics, architecture, UX, Story 8.7
  specification and acceptance criteria, source, tests, packages, CI, deployment,
  and submodules. Concurrent user changes were preserved.
- **Routed to:** Parties Architecture and Development, EventStore ownership and
  Security Review, Test Architecture, and Release/Operations.
- **Handoff condition:** Story 8.7 stays blocked and the retention action stays
  open until an authorized exact provider identity and complete seven-group proof
  packet satisfy the exit criteria above.
