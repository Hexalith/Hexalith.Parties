---
title: 'EXT-PARTIES-1 owner delivery'
type: 'feature'
created: '2026-10-25'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 1
baseline_commit: 'e523b06551ebdc69657ee78c045123dac19de6c8'
context:
  - '_bmad-output/planning-artifacts/actor-history-retention-policy-v1.md'
  - '_bmad-output/implementation-artifacts/spec-ext-parties-1-branch-b-authoritative-identity.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Agents Story 5.4 still cannot accept a replacement for `EXT-PARTIES-1`. Live always refuses, and the authenticated persisted P-01–P-10 proof, including ambiguity, cross-tenant denial, destruction, successor continuity, and recovery, is not an executed owner lane.

**Approach (user-directed resume, 2026-10-25):** Finish the Parties-owned delivery and replace refusal-only Live with complete authenticated persisted P-01–P-10 qualification. Bind real actor authority, retained-history custody, all-copy expiry, nonrollback restore and successor continuity. Return reviewed canonical source containing the delivery, a coordinated dependency target set, explicit owner integration date, exact executable command and actual installed hashes. Local test custody cannot substitute for those bindings. Execution requires acceptance of the exact target first.

**Decisions (owner answers, 2026-10-25):**
1. Installation: none — stop before Live.
2. Integration date: unresolved — do not invent one.

Both answers are absent. Stop before Live. Do not correct the reviewed local-proof limitations. Keep Live pending until the accepted target, installed hashes, and a new owner date exist. The local P-01–P-10 lane and the historical Local exit 1 are not Available.

## Boundaries & Constraints

**Always:** Preserve Branch B: the provisioned Party stays Organization-typed and is verified by immutable tenant and Party id. Retain human-binding history for 365 fixed days from original `binding-effective-at` under `party-actor-retention-v1` / `party-actor-history-v1`, with exclusive expiry (`now < ExpiresAt`). Reuse the existing processor, query, fold, and custody seams. Preserve historical failures and all recovery evidence. Distinguish observed source commits, reviewed delivery commits, accepted dependency targets and actual installed hashes.

**Never:** Change the Agents register, invent an owner integration date, trust anchor, credential, or qualified production custody backend, or register a test provider in production DI. Do not qualify an unaccepted target or execute a consuming Agents path. Do not treat Local or persisted evidence as Available. Do not alter Consumer onboarding, GDPR erasure, or the unrelated staged Parties files and dirty EventStore and Memories submodule worktrees.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Recovery | Lost acknowledgement, then restart | One original Party and binding survive | A second create or today's binding is rejected |
| Ambiguity | Gap, overlap, or conflicting revision | Typed fail-closed history result | No current-binding fallback |
| Cross-tenant denial | Other tenant, forged admission, or foreign id | Denial before protected lookup or effect | Foreign persisted state is unchanged |
| Destruction | Exclusive expiry reached | Reads deny; decryption capability is destroyed for every copy | Unknown acknowledgement stays pending |
| Successor continuity | Predecessor expired; approved actor-free successor | Later interval resolves only the successor | Expired predecessor cannot be relabeled or revived |
| Live qualification | Accepted exact source/dependency targets and complete authenticated installed bindings | Execute every P-01–P-10 persisted, restart, restore and fault obligation | Missing, mismatched or unaccepted bindings deny effects; incomplete/skipped probes fail |

</frozen-after-approval>

## Code Map

- `PartyDomainProcessor`, `PartyIdentityQueryService`, and `RetainedHumanActorHistoryFold` — reuse. `PartyDetail` is not authority.
- `PartiesServiceCollectionExtensions` — custody and retained-history reads stay host-supplied.
- `eng/verify-ext-parties-1.ps1` — Live stays refused on this stop.
- `tests/Hexalith.Parties.IntegrationTests/Identity/` — do not edit. Test custody stays out of production DI.
- `tests/ext-parties-1-owner-delivery-2026-10-25/resume-recovery/` — retained review and verification evidence.

## Tasks & Acceptance

**Prior local work (retained; does not satisfy delivery):**
- [x] Unregistered custody and retained-history reads fail closed. `AddParties` registers no custody provider.
- [x] The persisted P-01–P-10 lane uses a test-only double. Live still refuses. The packet stays `proposed` with a null date.

**Acceptance Criteria:**
- Given accepted installed bindings and exact targets, when Live executes, then P-01–P-10 pass through real authority, persisted history, all-copy expiry, nonrollback restore, and approved successor continuity.
- Given missing or unaccepted bindings, when qualification is requested, then no target effect occurs and the missing inputs are retained.
- Given reviewed canonical source, when the packet is submitted, then it names the target set, new owner date, exact command, and installed hashes. Local evidence leaves the register unchanged.

**Remaining execution:**
- [ ] Stopped. Installation is `none — stop before Live`. Integration date is `unresolved — do not invent one`. Live and the Identity lane stay untouched until an approved B01–B15 path, an accepted target, installed hashes, and a new owner date exist.

## Implementation Notes

Recorded the absent owner answers and stopped. No Identity, Live, register, custody, trust, credential, or production-DI change.

## Spec Change Log

- 2026-10-25 token split: review and verification history moved to deferred work. Evidence stays in `resume-recovery/`. KEEP Branch B, immutable identity, and 365-day exclusive expiry. Status stays `draft`.

## Review Triage Log

BH-01–BH-10, EC-01, VG-01, and VG-02 stay in `resume-recovery/review-triage.json`.

## Verification

No new command. Prior results stay in `resume-recovery/`.
