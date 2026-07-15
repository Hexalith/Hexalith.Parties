---
title: Sprint Change Proposal — Revalidate Parties Crypto / Key-Management Retention
date: 2026-07-16
author: Administrator
workflow: bmad-correct-course
mode: batch
scope_classification: moderate
status: approved
approved: 2026-07-16T00:26:58+02:00
trigger: >
  Keep Parties crypto/key-management implementation until an approved shared
  provider proves payload compatibility, typed unreadable outcomes, no-leak
  diagnostics, exports, processing records, certificates, and rollback.
supersedes: null
revalidates:
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-crypto-key-management-retention.md
related:
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md
  - _bmad-output/implementation-artifacts/spec-8-7-data-protection-extraction.md
  - _bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md
  - _bmad-output/implementation-artifacts/sprint-status.yaml
  - _bmad-output/planning-artifacts/adr-epic-7-crypto-key-management-split-2026-06-29.md
---

# Sprint Change Proposal — Revalidate Parties Crypto / Key-Management Retention

## 1. Issue Summary

Story 8.7, **Data-protection extraction**, targets removal of generic
crypto/key-management mechanics from `Hexalith.Parties.Security` after an
approved shared provider is ready. Premature removal could make historic
payloads unreadable, collapse typed failures, leak sensitive diagnostics, break
Art.20 exports or Art.30 processing records, weaken erasure proof, or leave no
tested recovery path.

The approved course is:

> Parties retains its crypto/key-management implementation and usable rollback
> registration until an approved shared provider proves payload compatibility,
> typed unreadable outcomes, no-leak diagnostics, exports, processing records,
> certificates/reports, and rollback.

This revalidates the approved 2026-07-07 guardrail; it does not supersede or
dilute it.

### Trigger and evidence

- **Triggering story:** Story 8.7, Data-protection extraction.
- **Issue category:** stakeholder safety constraint for a deletion-heavy
  technical migration.
- `sprint-status.yaml` keeps Story 8.7 in `backlog` and the exact retention
  action item `open`.
- `spec-8-7-data-protection-extraction.md` remains `draft` with
  `blocked-prerequisite` and a halt condition requiring an approved provider,
  owner approval, parity proof, and a recorded EventStore pin.
- The Story 8.3 matrix keeps **Payload protection engine package** at
  `needs-additive-api`; owner routing explicitly states that routing is not
  delivery or approval.
- All **18/18** rollback-only MOVE files remain present in
  `src/Hexalith.Parties.Security/`.
- All **5/5** KEEP domain files and the
  `EventStorePartyPayloadProtectionAdapter` seam remain present.
- Active DI still registers `LocalDevKeyStorageBackend`, the concrete
  `PartyPayloadProtectionService`, and
  `IEventPayloadProtectionService -> EventStorePartyPayloadProtectionAdapter`;
  the adapter still wraps the Parties provider.
- Static inspection of the checked-out EventStore source found no implementation
  file containing `pdenc-v2`, `IPersonalDataPolicy`, or
  `IErasureStateProvider`, consistent with the G5 row remaining unfulfilled.
- The existing harness proves Parties local/adapter behavior, but no approved
  shared-provider path exists for the required dual-path parity run.

## 2. Impact Analysis

### Epic and story impact

- **Epic 8 only.** It remains Class C post-MVP maintenance with zero new PRD
  functional requirements.
- No epic is added, removed, redefined, or re-sequenced.
- Story 8.7 remains `backlog` and must not begin source migration while G5 is
  `needs-additive-api`.
- Stories 7.6 and 7.7 stay done; their adapter-first, locally reversible outcome
  is ratified rather than rolled back.
- Story 8.10 must not close the retention action without provider approval,
  exact release/pin, parity evidence, and tested rollback.

### Artifact impact

- **PRD:** no change; MVP scope and completed GDPR features remain stable.
- **Architecture:** no change; Epic 8 invariants I3, I7, and I8 already require
  rollback retention, GDPR-semantic preservation, format compatibility, typed
  unreadable outcomes, no-leak behavior, exports, records, and certificates.
- **UX:** no new surface or interaction. Existing PII-free states, honest
  erasure copy, export behavior, and bounded diagnostics remain acceptance
  evidence.
- **Epics and sprint tracking:** no change. Story 8.7 and the open action already
  reflect the requested course.
- **Source, tests, packages, CI, deployment, and submodules:** no change.

## 3. Recommended Approach

### Selected: Direct Adjustment — preserve and revalidate the existing gate

Keep the approved 2026-07-07 deletion guardrail in force. Do not start Story 8.7
source migration or delete a rollback-only Parties crypto/key file until the G5
row contains the complete accepted proof packet.

- **Governance effort:** Low.
- **Change risk:** Low.
- **Risk if ignored:** Critical—data unreadability, GDPR-evidence regression,
  sensitive leakage, or failed migration without recovery.
- **Timeline:** no completed-MVP impact; Story 8.7 remains provider-gated.

Alternatives rejected:

- Rolling back Stories 7.6/7.7 is counterproductive because their retained local
  provider and adapter are the desired rollback-safe state.
- MVP/PRD review is unnecessary because functional scope is unchanged.
- Provider-neutral contracts alone are insufficient; they do not constitute an
  approved engine or executable proof of domain reads and rollback.

## 4. Detailed Change Proposals

### 4.1 Story 8.7 execution state — reaffirm, no edit

**CURRENT:**

```text
8-7-data-protection-extraction: backlog
spec status: draft; warning: blocked-prerequisite
G5 payload-protection engine: needs-additive-api
```

**APPROVED STATE:** keep these values until G5 records named owner approval,
exact released package or root-submodule pin, green parity evidence, and tested
rollback.

### 4.2 Retention and ownership — reaffirm, no edit

**CURRENT:** 18/18 rollback-only MOVE files, 5/5 KEEP domain files, and the
adapter seam are present.

**APPROVED STATE:**

- Retain the MOVE files needed to switch back to the local provider until every
  governing proof passes.
- Keep Parties erasure orchestration, legal policy, record store, command guard,
  graph inspection, certificates/reports, and the compatibility seam in Parties
  unless a separate approved ADR changes ownership.
- Treat rollback as a live, tested registration path—not a promise to recreate
  deleted code after failure.

### 4.3 Shared-provider exit gate — reaffirm, no edit

| Proof | Required outcome |
|---|---|
| Provider approval | Named owner approval and exact released package or approved root EventStore pin. |
| Payload compatibility | `json+pdenc-v1`, `json-redacted`, and legacy-unprotected reads retain their semantics; new format/AAD behavior stays backward-readable. |
| Typed unreadable | Destroyed, missing, tampered, denied, opaque, and unavailable cases remain bounded typed outcomes, never generic 500s. |
| No-leak diagnostics | Logs, traces, metrics, exceptions, ProblemDetails, evidence, and copy reveal no PII, payload, key material/alias, provider text/blob, identifiers, or state-store/connection detail. |
| Art.20 export | Live, restricted, unavailable, and erased exports preserve status/redaction and PII-free filename/diagnostic behavior. |
| Art.30 records | Bounded audit metadata remains available without raw payloads, personal values, free-text reasons, or decrypted data. |
| Certificates/reports | Erasure proof remains PII-free and preserves the approval-gated D7 contract and Parties legal semantics. |
| Rollback | Switching back to the retained local provider is executed successfully without data or metadata incompatibility. |

Executable evidence must exercise both the local and approved shared-provider
paths. Interface availability and owner routing are necessary but insufficient.

### 4.4 Other planning artifacts — no edit

The PRD, epics, architecture, UX, Story 8.7 spec, Story 8.3 matrix, and sprint
status already align. The 2026-07-07 approved proposal remains the governing
decision; this document records current revalidation and approval.

## 5. Implementation Handoff

**Scope: Moderate.** No fundamental replan or implementation change is required,
but release of retained security code requires cross-owner evidence and sign-off.

| Recipient | Responsibility |
|---|---|
| PM / Architect | Keep the guardrail open; accept the provider identity/pin and full proof packet before authorizing deletion. |
| Product Owner / Developer | Do not start 8.7 migration or delete retention files while G5 is unproven; keep local DI rollback functional. |
| Hexalith.EventStore owner | Deliver and approve the additive shared engine with an exact release/pin and provider-path evidence. |
| Test Architect | Require dual-path parity, no-leak scanning, domain-read/certificate evidence, and an executed rollback rehearsal. |
| Story 8.10 owner | Close or explicitly defer the gate with owner, evidence, pin, and rollback; never infer completion from contracts alone. |

### Success criteria

1. Story 8.7 remains migration-blocked while G5 is `needs-additive-api`.
2. The local provider and rollback-only files remain usable until all proofs pass.
3. No domain KEEP file moves without an explicit approved ADR.
4. G5 records approval, exact provider identity, parity, and rollback before
   deletion begins.
5. Payloads, typed outcomes, diagnostics, exports, records, certificates/reports,
   and rollback pass against both provider paths.
6. PRD coverage, UX, public contracts, and GDPR legal semantics remain unchanged.

## 6. Change-Analysis Checklist

| Item | Status | Finding |
|---|---|---|
| 1.1–1.3 Trigger/problem/evidence | [x] Done | Story 8.7; premature-deletion risk; repo and planning evidence recorded. |
| 2.1 Current epic viable | [x] Done | Yes, after—not before—the provider gate. |
| 2.2–2.5 Epic changes/order | [N/A] Skip | No epic change, addition, removal, or resequencing. |
| 3.1 PRD | [x] Done | No conflict or MVP impact. |
| 3.2 Architecture | [x] Done | I3/I7/I8 and the accepted ADR already govern. |
| 3.3 UI/UX | [N/A] Skip | No new surface; existing no-leak/honesty obligations remain evidence. |
| 3.4 Other artifacts | [x] Done | Spec, matrix, sprint status, source inventory, and DI are aligned. |
| 4.1 Direct adjustment | [x] Viable | Selected: preserve/revalidate the existing gate. |
| 4.2 Rollback option | [N/A] Skip | Retained implementation is the desired rollback path. |
| 4.3 MVP review | [N/A] Skip | Completed feature scope is unaffected. |
| 4.4 Recommended path | [x] Done | Retention until every proof passes. |
| 5.1–5.5 Proposal components | [x] Done | Issue, impact, recommendation, changes, and handoff recorded. |
| 6.1–6.2 Final review | [x] Done | Proposal cross-checked against current artifacts and source inventory. |
| 6.3 Explicit approval | [x] Done | Administrator approved on 2026-07-16T00:26:58+02:00. |
| 6.4 Sprint-status update | [N/A] Skip | No epic/story/status mutation proposed. |
| 6.5 Handoff | [x] Done | Moderate-scope responsibilities and gate condition confirmed. |

## 7. Approval and Workflow Execution Log

- **Decision:** approved by Administrator on 2026-07-16T00:26:58+02:00.
- **Issue addressed:** continued retention of Parties crypto/key-management until
  the complete shared-provider proof gate passes.
- **Change scope:** Moderate.
- **Artifacts modified:** this Sprint Change Proposal only.
- **Artifacts deliberately unchanged:** PRD, epics, architecture, UX, sprint
  status, Story 8.7 spec, Story 8.3 matrix, source, tests, packages, CI,
  deployment, and submodules.
- **Routed to:** PM/Architect, Product Owner/Developer, Hexalith.EventStore owner,
  Test Architect, and Story 8.10 owner.
- **Handoff condition:** no Story 8.7 source migration or retention-set deletion
  until G5 records owner approval, exact provider release/pin, green dual-path
  parity evidence, and tested rollback.
