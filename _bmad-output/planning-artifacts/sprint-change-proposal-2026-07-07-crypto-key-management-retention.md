---
title: Sprint Change Proposal — Crypto / Key-Management Implementation Retention (Story 8.7 Deletion Guardrail)
date: 2026-07-07
author: Administrator
workflow: bmad-correct-course
mode: incremental
scope_classification: moderate
trigger: >
  Epic 7 retrospective action item (sprint-status.yaml): "Keep Parties
  crypto/key-management implementation until an approved shared provider proves
  payload compatibility, typed unreadable outcomes, no-leak diagnostics,
  exports, processing records, certificates, and rollback." Formalize that open
  retro action into a governing, repo-evidenced deletion guardrail for Story 8.7
  (Data-protection extraction).
status: approved
approved: 2026-07-07T23:04:35+02:00
related:
  - _bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md
  - _bmad-output/implementation-artifacts/spec-8-7-data-protection-extraction.md
  - _bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md
  - _bmad-output/implementation-artifacts/7-6-crypto-key-management-adr-and-compatibility-harness.md
  - _bmad-output/implementation-artifacts/7-7-crypto-key-management-migration-behind-eventstore-provider-contracts.md
  - _bmad-output/planning-artifacts/adr-epic-7-crypto-key-management-split-2026-06-29.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-projection-rollback-retention.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-platform-prerequisite-routing.md
  - _bmad-output/implementation-artifacts/epic-8-context.md
---

# Sprint Change Proposal — Crypto / Key-Management Implementation Retention (Story 8.7 Deletion Guardrail)

## 1. Issue Summary

Epic 7 closed the crypto/key-management platform work as adapter-first and
reversible: Stories 7.6 (`7-6-crypto-key-management-adr-and-compatibility-harness`,
**done**) and 7.7 (`7-7-crypto-key-management-migration-behind-eventstore-provider-contracts`,
**done**) introduced the `EventStorePartyPayloadProtectionAdapter` seam and a
compatibility harness, but **deliberately retained** the Parties-local
protection engine, key-management services, retry scheduler, decryption circuit
breaker, key-operation audit, tenant key-rotation, and the dev-only key store
rather than deleting them. Actual extraction/deletion was deferred to Epic 8
Story 8.7 ("Data-protection extraction").

The Epic 7 retrospective preserved this as an **open action item** (verbatim, in
`sprint-status.yaml`):

> "Keep Parties crypto/key-management implementation until an approved shared
> provider proves payload compatibility, typed unreadable outcomes, no-leak
> diagnostics, exports, processing records, certificates, and rollback."

**This proposal formalizes that open retro action into a governing, evidenced
deletion guardrail** for Story 8.7. It is **not** a feature or MVP-scope change
and **not** a code change. It ratifies "hold the line until proven" as an
approved decision with a named proof-gate and explicit exit criteria,
consolidates the already-encoded governance layers into one home, and records
repo-verified evidence that the guardrail is intact today. It is the crypto
sibling of the approved
`sprint-change-proposal-2026-07-07-projection-rollback-retention.md`, which
explicitly named this guardrail as its out-of-scope analogue.

### Why now

Story 8.7 ("Data-protection extraction") is the deletion-heavy migration that
will move the generic crypto/key-management mechanics behind a shared provider
and then delete the local files. It is currently `backlog` (spec `draft`,
`blocked-prerequisite`). Before it starts, the retention constraint should be an
**approved decision with explicit exit criteria**, not merely an open retro
action — so that a future dev / dev-auto session cannot delete a local crypto or
key file without first recording the seven proofs the guardrail names.

### Evidence (repo-verified 2026-07-07)

**The constraint is already encoded across four layers (this proposal
consolidates, it does not invent):**

1. **Spine invariants** (`ARCHITECTURE-SPINE.md`): **I8** (`json+pdenc-v1` /
   `json-redacted` / legacy-unprotected reads, key zeroing, typed-unreadable
   outcomes, no-leak diagnostics, Art.20 exports, Art.30 processing records, and
   erasure reports/certificates), **I7** (two-front-door erasure + D7
   cross-submodule verification, consent ≠ lawful-basis, Art.18 guards), **I3**
   (local rollback paths stay until the replacement API has parity evidence +
   proven rollback).
2. **`spec-8-7`**: Block-If prerequisite (HALT unless an approved shared
   payload-protection provider exists with owner approval, parity proof, and a
   `references/Hexalith.EventStore` submodule-pin recorded in the 8.3 matrix);
   *"Do not delete any local crypto/key file before the shared provider records
   parity + proven rollback"*; the MOVE list marked "delete locally **only
   after** parity + rollback proof"; the KEEP list retaining party-specific GDPR
   policy / erasure orchestration / certificates; and AC *"certificates, reports,
   exports, and processing records preserve domain semantics and leak no PII."*
3. **`story-8-3` matrix**, G5 row *"Payload-protection engine package"* =
   `needs-additive-api`: an additive shared payload-protection engine
   (`pdenc-v2` AAD binding + `json+pdenc-v1` read, `IPersonalDataPolicy`,
   `IErasureStateProvider`, key storage/wrapping/rotation/audit/retry/circuit-
   breaker, typed-unreadable outcomes, moved golden compatibility harnesses) must
   land with golden-harness parity + rollback before `Hexalith.Parties.Security`
   is touched.
4. **`sprint-status.yaml`** Epic 7 action item (open) — the trigger text itself —
   and the Epic 8 owner-routing action item that routes the payload-protection
   engine (G5) to Hexalith.EventStore owners
   (`sprint-change-proposal-2026-07-07-platform-prerequisite-routing.md`).

**The guardrail is holding in the tree today** (verified 2026-07-07): all 18
`spec-8-7` MOVE-listed files, all 5 KEEP domain files, and the adapter seam are
present in `src/Hexalith.Parties.Security/`.

| `spec-8-7` set | Files | Present? |
|---|---|---|
| MOVE — protection + key engine | `PartyPayloadProtectionService.cs`, `PartyKeyManagementService.cs`, `CachedPartyKeyManagementService.cs`, `PartyKeyLifecycleService.cs` | ✅ present |
| MOVE — retry scheduling | `IPartyKeyRetryScheduler.cs`, `ActorBackedPartyKeyRetryScheduler.cs`, `PartyKeyRetryActor.cs`, `IPartyKeyRetryActor.cs` | ✅ present |
| MOVE — rotation / audit / circuit | `DecryptionCircuitBreaker.cs`, `DecryptionCircuitOpenException.cs`, `KeyOperationAuditService.cs`, `TenantKeyRotationService.cs`, `TenantKeyRotationProgress.cs`, `TenantKeyRotationProgressConflictException.cs`, `ITenantKeyRotationCacheInvalidator.cs` | ✅ present |
| MOVE — key store + typed outcomes | `LocalDevKeyStorageBackend.cs`, `PartyEncryptionKeyDestroyedException.cs`, `CryptoPendingRecord.cs` | ✅ present |
| KEEP — domain (do NOT move without ADR) | `PartyErasureOrchestrator.cs`, `ErasureVerificationService.cs`, `PartyErasureRecordStore.cs`, `PartyPersonalDataCommandGuard.cs`, `PersonalDataGraphInspector.cs` | ✅ present |
| SEAM (kept) | `EventStorePartyPayloadProtectionAdapter.cs` | ✅ present |

- **18** MOVE-listed crypto/key files intact; no deletion has occurred.
- `sprint-status.yaml`: `8-7-data-protection-extraction: backlog` — guardrail
  currently satisfied.
- `Parties:CryptoShredding:IsEnabled` stays default-**true** (the AES-256-GCM
  feature, ON) and is distinct from `Parties:Compliance:GdprFeaturesActive`
  default-**false** (the MVP warning suppressor); `LocalDevKeyStorageBackend`
  remains the dev-only default and must never ship to production.

### Change-Analysis Checklist Status

| # | Item | Status |
|---|---|---|
| 1.1–1.3 | Trigger, core problem, evidence | ✅ Done (deletion-safety guardrail; repo-verified) |
| 2.1 | Epic 8 completable as planned? | ❗ Only after Story 8.7 records the seven proofs in the 8.3 matrix G5 row |
| 2.2 | Epic-level change | ✅ None — no epic added/removed; tracking annotation only |
| 2.3–2.4 | Other/future epics affected | ✅ N/A (Epics 1–7 done; Epic 8 only) |
| 2.5 | Resequencing | ✅ None — `8.1→8.10` preserved |
| 3.1 | PRD conflict | ✅ None — no FR change (Class C, zero new PRD FRs) |
| 3.2 | Architecture | ✅ None — spine I8/I7/I3 already govern; no spine edit |
| 3.3 | UX conflict | ✅ N/A — Epic 8 conformance; no new UX |
| 3.4 | Other artifacts | ❗→✅ `sprint-status.yaml` retro item annotated to this proposal |
| 4.1 | Direct Adjustment (formalize-and-gate) | ✅ **Viable — selected** |
| 4.2 | Rollback | ⛔ N/A — nothing to revert (the point is to *keep* the local crypto path) |
| 4.3 | MVP review | ✅ N/A — Epics 1–5 complete/unaffected |
| 4.4 | Path selected | ✅ Direct Adjustment (formalize the retro action as a governed guardrail) |

## 2. Impact Analysis

### Epic Impact
- **Epic 8 only.** Epics 1–7 done and unaffected. No epic added, removed, or
  re-sequenced; `8.1→8.10` unchanged. Class C maintenance classification (zero
  new PRD FRs) preserved.

### Story Impact
- **Story 8.7** stays `backlog`. This proposal adds **no new gate** — it makes
  the existing spine/spec/matrix retention rule an **approved, named guardrail
  with exit criteria** that Story 8.7 must satisfy before deleting any MOVE-listed
  file. Stories 8.1–8.6 unaffected; 8.8–8.10 unaffected.
- **Stories 7.6 / 7.7** (done) are **ratified as-is**: their intentional
  retention of the local crypto/key engine behind the adapter seam was correct
  and is not reopened. No rollback of completed work (Correct Course §4.2 = not
  viable).

### Artifact Conflicts (resolved by this proposal)
- **`sprint-status.yaml`:** the open Epic 7 retro action item had no governing
  home → annotate it to point at this proposal; keep `status: open` until Story
  8.7 records the seven proofs, then close. `last_updated` bumped. **No
  story/epic row changes; no re-sequencing.**
- **`ARCHITECTURE-SPINE.md` / `spec-8-7` / `story-8-3` matrix:** **untouched.**
  The spine already carries I8/I7/I3; `spec-8-7` already carries the Block-If +
  parity-before-delete rule and the AC that certificates/reports/exports/
  processing-records preserve domain semantics; the 8.3 matrix is a done Story
  8.3 artifact and is fitness-guarded (`PlatformApiPrerequisitesTests`). This
  proposal **cross-references** them, consistent with the precedent (projection
  sibling §4.3) that Correct Course does not rewrite the fitness-guarded matrix
  or edit the auto-generated draft spec files.
- **PRD / UX / epics.md / epic-8-context.md:** no conflict, no change.

### Technical Impact
- **No Parties code change. No submodule edits.** The guardrail's whole purpose
  is that the MOVE-listed crypto/key files **stay in place** until Story 8.7
  proves the seven conditions — per spine I8/I7/I3, `spec-8-7`, and the 8.3
  matrix G5 rollback column.
- Owner-side delivery (Hexalith.EventStore additive payload-protection engine,
  G5) is already routed by the approved
  `sprint-change-proposal-2026-07-07-platform-prerequisite-routing.md`; this
  proposal governs the **consuming-side deletion discipline**, not the owner work.
- The KEEP domain files (erasure orchestration, GDPR policy, certificates/
  reports) remain in Parties regardless; the guardrail additionally requires
  proving they behave identically once the crypto engine beneath them is the
  shared provider.

## 3. Recommended Approach

**Selected: Option 1 — Direct Adjustment (formalize the retro action as a
governed guardrail with exit criteria).**

Ratify "keep the Parties crypto/key-management implementation until Story 8.7
proves the seven conditions" as an approved decision, consolidate the existing
governance layers into one home, record today's repo-verified evidence, and give
Story 8.7 an explicit, checkable **exit criteria** (§4.2). Annotate the open
Epic 7 retro action item so it has a governing reference and stays `open` until
the proofs land.

**Rationale:** lowest-disruption, honest, and momentum-preserving. The
constraint is already encoded across spine/spec/matrix/retro — the residual risk
is purely **process drift** (a future automated session deleting a crypto/key
file without recording proof). Formalizing the guardrail with named exit criteria
closes that drift risk without inventing new scope. Rollback (§4.2) is the
opposite of what's wanted here. MVP review (§4.3) does not apply.

- **Effort:** Low (one governing document + a one-block `sprint-status.yaml`
  annotation; no code).
- **Risk:** Low. Residual delivery risk stays owned by the §4 gate + the 8.3
  matrix Review Gate; this proposal reduces process-drift risk.
- **Timeline impact:** None to MVP. Does **not** unblock or block Story 8.7 —
  8.7 remains gated on the owner-delivered payload-protection engine (G5) per the
  8.3 matrix Block-If.

## 4. Detailed Change Proposals

### 4.1 Governing decision (this document)

**Decision (approved-on-approval):** The following Parties-local
protection/key-management files are **rollback-only** for Story 8.7 and **MUST
NOT be deleted** until Story 8.7 has recorded, in the `story-8-3` matrix G5 row,
all seven proofs in §4.2 below. The 5 KEEP domain files and the adapter seam
**MUST NOT be moved out of Parties without an explicit ADR** (they are domain,
not generic mechanics).

**Retention set — MOVE-listed (must stay until §4.2 proofs are recorded):**
- `src/Hexalith.Parties.Security/PartyPayloadProtectionService.cs`,
  `PartyKeyManagementService.cs`, `CachedPartyKeyManagementService.cs`,
  `PartyKeyLifecycleService.cs`
- `src/Hexalith.Parties.Security/IPartyKeyRetryScheduler.cs`,
  `ActorBackedPartyKeyRetryScheduler.cs`, `PartyKeyRetryActor.cs`,
  `IPartyKeyRetryActor.cs`
- `src/Hexalith.Parties.Security/DecryptionCircuitBreaker.cs`,
  `DecryptionCircuitOpenException.cs`, `KeyOperationAuditService.cs`,
  `TenantKeyRotationService.cs`, `TenantKeyRotationProgress.cs`,
  `TenantKeyRotationProgressConflictException.cs`,
  `ITenantKeyRotationCacheInvalidator.cs`
- `src/Hexalith.Parties.Security/LocalDevKeyStorageBackend.cs`,
  `PartyEncryptionKeyDestroyedException.cs`, `CryptoPendingRecord.cs`

**KEEP — domain (do NOT move without ADR; stay in Parties):**
- `src/Hexalith.Parties.Security/PartyErasureOrchestrator.cs`,
  `ErasureVerificationService.cs`, `PartyErasureRecordStore.cs`,
  `PartyPersonalDataCommandGuard.cs`, `PersonalDataGraphInspector.cs`
- Seam kept: `EventStorePartyPayloadProtectionAdapter.cs`

**Revert semantics:** rollback = restore the local
`PartyPayloadProtectionService` + `PartyKeyManagementService` (and retry /
rotation / audit / circuit-breaker / key-store) registrations in
`PartiesServiceCollectionExtensions`, re-pointing the
`EventStorePartyPayloadProtectionAdapter` seam at the local path. Keep the local
registration live until parity is proven; no key-material migration without a
rollback script + approval. `Parties:CryptoShredding:IsEnabled` stays
default-true; `LocalDevKeyStorageBackend` never ships to production.

### 4.2 Exit criteria — what Story 8.7 must prove before any deletion

Mapping the trigger's seven conditions to spine invariants and the parity harness
`spec-8-7` prescribes. Each must be recorded in the 8.3 matrix G5 row **and**
proven by the `Hexalith.Parties.Security.Tests` parity harness against **BOTH**
the local and provider paths before the corresponding MOVE-listed code is deleted:

| # | Trigger condition | Spine invariant | Proof required (recorded in 8.3 matrix G5 row + `test-summary.md`) |
|---|---|---|---|
| 1 | **Payload compatibility** | I8 | Shared provider unprotects `json+pdenc-v1` to byte-identical plaintext state; `json-redacted` and legacy-unprotected payloads read with identical semantics; AES-256-GCM AAD binding preserved; **no** spurious decrypt attempt on legacy payloads. |
| 2 | **Typed unreadable outcomes** | I8 | Deleted-key read yields the typed `json-redacted` unreadable outcome; `PartyEncryptionKeyDestroyedException` maps to a typed-unreadable result (**not** a 500); retry-scheduler + `DecryptionCircuitBreaker` open/half-open/closed semantics behaviorally identical. |
| 3 | **No-leak diagnostics** | I8 | No payload, key material, key alias, party/tenant name, or actor id in logs, traces, exceptions, or ProblemDetails on any protect / unprotect / erase / rotate / audit path. |
| 4 | **Exports (Art.20)** | I8 | `ExportPartyData` → `PartyDataPortabilityPackage` behaviorally identical on live **and** erased parties; erased-party redaction preserved; missing → existing no-op/empty semantics. |
| 5 | **Processing records (Art.30)** | I8 | `GetProcessingRecords` → `ProcessingActivityRecord[]` identical semantics on the provider path; missing → existing no-op/empty semantics. |
| 6 | **Certificates** | I8 + I7 | Deleted-key redaction → PII-free tombstone → identical `ErasureVerificationReport` / `ErasureCertificate` domain semantics; the approval-gated `IErasureVerificationService` D7 cross-submodule contract **unchanged**; two-front-door erasure + Art.18 / consent-≠-lawful-basis guards intact. |
| 7 | **Rollback** | I3 | The replacement provider path has **both** parity evidence **and** a proven rollback (restore local registration in `PartiesServiceCollectionExtensions`); MOVE-listed files remain until parity is recorded; the dev-only key-store production warning preserved. |

**Deletion is authorized for a given MOVE-listed file only when** its governing
proof(s) above are recorded in the `story-8-3` matrix G5 row **and** the parity
harness (both local and provider paths) is green **and** rollback is verified.
Absent any one, the file stays. The KEEP domain files are not deleted at all;
they are only proven (rows 4–6) to behave identically once the engine beneath
them is the shared provider.

### 4.3 `sprint-status.yaml` change (applied on approval)

Annotate the open Epic 7 retro action item (the trigger text) with a comment
block naming this proposal as its governing home, keep `status: open`, and bump
`last_updated`. **No story/epic rows change; no re-sequencing.** Applied diff:

```yaml
  - epic: 7
+   # Formalized 2026-07-07 as a governed deletion guardrail with exit criteria:
+   #   sprint-change-proposal-2026-07-07-crypto-key-management-retention.md.
+   # Stays `open` until the Story 8.7 parity harness records payload/format parity,
+   # typed-unreadable outcomes + no-leak diagnostics, Art.20 export + Art.30
+   # processing-record semantics, erasure certificate/report parity, and proven
+   # rollback (spine I8/I3) in the 8.3 matrix G5 row; routed to Hexalith.EventStore
+   # owners as G5. Verified 2026-07-07: all 18 spec-8-7 MOVE-listed
+   # src/Hexalith.Parties.Security crypto/key files (+ 5 KEEP domain files + the
+   # EventStorePartyPayloadProtectionAdapter seam) intact; spec-8-7 backlog + blocked-prerequisite.
    action: "Keep Parties crypto/key-management implementation until an approved shared provider proves payload compatibility, typed unreadable outcomes, no-leak diagnostics, exports, processing records, certificates, and rollback."
    owner: "Winston (Architect) + Amelia (Developer)"
    status: open
```

(Plus `last_updated` bump. This annotation is already applied in the same session
as this proposal.)

## 5. Implementation Handoff

**Scope classification: Moderate.** Governance/tracking formalization plus a
one-block `sprint-status.yaml` annotation — no fundamental replan, no code, no
submodule edits.

| Recipient | Responsibility |
|---|---|
| PM / Architect (Winston) | Owns the guardrail. Ensure Story 8.7's spec/dev session records the seven §4.2 proofs in the `story-8-3` matrix G5 row before any deletion. Confirm the shared payload-protection engine (G5) is owner-approved with a recorded `references/Hexalith.EventStore` submodule-pin. Close the annotated retro action item only when all seven proofs are recorded. |
| Product Owner / Developer (Alice / Amelia) | Do **not** delete any §4.1 MOVE-listed file, and do **not** move any KEEP domain file without an ADR, until §4.2 exit criteria are met. Build/extend the `Hexalith.Parties.Security.Tests` parity harness to prove all seven conditions against BOTH local and provider paths. Keep the local DI rollback registration functional and `CryptoShredding:IsEnabled` default-true. |
| Platform owner (Hexalith.EventStore) | Deliver the additive shared payload-protection engine (G5): `pdenc-v2` AAD binding + `json+pdenc-v1` read, `IPersonalDataPolicy`, `IErasureStateProvider`, key storage/wrapping/rotation/audit/retry/circuit-breaker, typed-unreadable outcomes, and moved golden compatibility harnesses — additive/approved before any Parties deletion. |
| Test Architect (Murat) | Verify the parity-harness lane runs the `Hexalith.Parties.Security.Tests` xUnit v3 assembly directly (not `dotnet test --filter`), pin `-p:MinVerVersionOverride=1.0.0` on rebuilt projects, run the no-leak diagnostics check, and confirm typed-unreadable + certificate/report parity before signing off deletion. |

### Success Criteria
1. The retention constraint is an **approved decision** with an explicit
   retention set (§4.1) and exit criteria (§4.2) — met by this document on
   approval.
2. The open Epic 7 retro action item names this proposal as its governing home
   and stays `open` until Story 8.7 proves the seven conditions — met by §4.3.
3. **No Parties code change, no submodule edits, no spec/matrix/spine rewrite** —
   met (cross-reference only; the spec-8-7 harness task already has its AC, and
   the certificate/report obligation is recorded here in §4.2).
4. Story 8.7 stays `backlog`; deletion authorized only when the seven §4.2 proofs
   are recorded in the 8.3 matrix G5 row — governance preserved (spine §4, matrix
   Review Gate).
5. No PRD FR coverage change; Epics 1–5 remain the feature-readiness baseline —
   met. `CryptoShredding:IsEnabled` stays default-true and distinct from
   `Compliance:GdprFeaturesActive` default-false.

### Deferred / out of scope for this proposal
- Owner-side delivery of the Hexalith.EventStore additive payload-protection
  engine (G5) — already routed by
  `sprint-change-proposal-2026-07-07-platform-prerequisite-routing.md`.
- Authoring/splitting Story 8.7's spec beyond what `spec-8-7` already records
  (routed to the spec/create-story workflow, per spine §4; `spec-8-7` carries the
  `oversized` warning and may be split at spec-creation time).
- Production KMS provisioning (operational prerequisite before real regulated EU
  personal data — unchanged; not a Story 8.7 blocker).
- The parallel projection rollback-only retention guardrail (separate open Epic 7
  retro action item) — governed by
  `sprint-change-proposal-2026-07-07-projection-rollback-retention.md`.
