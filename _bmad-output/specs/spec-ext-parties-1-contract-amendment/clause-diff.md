# EXT-PARTIES-1 proposed clause diff — 2026-10-25

**UNAPPROVED — REVIEW ONLY.** This is a proposed amendment overlay, not an applied edit or replacement acceptance packet. All new numeric bounds and scope exclusions below require explicit owner approval. Existing acceptance, source files and evidence remain unchanged.

Each “Before” is an exact paragraph from the named local source. “After” is the complete proposed replacement obligation. Historical acceptance, proposal and research artifacts must be preserved; if adopted, record the new clauses in a versioned accepted addendum with explicit precedence and align live contracts/verifiers. Do not rewrite historical evidence to make it appear to have tested these terms.

The complete identity contract is retained, not reduced to its custody seam. The governing register and October 25 reconciliation take precedence over older “TBD/Uncommitted” snapshots. The hardware proposal supplies a precise description of the stopped-holder requirement, not an accepted hardware mechanism.

## A-01 — Service-boundary expiry and in-flight results

**Before (verbatim)**

[retention](../../../../agents/_bmad-output/implementation-artifacts/story-5-4-retention-custody-proposal-2026-10-06.md) — Required behavior and qualification:

```text
At expiry, all reads deny immediately. Cleanup is idempotent and outcome lookup resolves crashes/lost acknowledgements; unknown outcomes remain pending. `DestroyExpiredAsync` returns success only after authenticated irreversible destruction and copy-cleanup evidence. Recoverable soft deletion, a delayed scheduled purge, or changing a read threshold cannot be reported as completed destruction. A provider with incompatible timing either fails strict v1 or requires a separately accepted lifecycle-contract change; there is no hidden grace period.
```

**After (proposed, unapproved)**

```text
For each original history unit, E = original binding-effective-at + 31,536,000 seconds. The policy remains party-actor-retention-v1 and the purpose remains party-actor-history-v1. E is exclusive. No rebinding, revocation, rotation, retry, copying, erasure or restore changes E for predecessor evidence. A successor receives its own deadline only for its own evidence. This does not promise 365 days after each action or profile erasure.

Every controlled decision to admit, unwrap/decrypt, use retained attribution, commit a derived copy, or hand a result to the next controlled holder must authenticate the exact tenant/Party/original unit, current actor/source authorization, current lifecycle epoch and fence, and trusted time. Recheck after each asynchronous wait and immediately before the decision's recorded authorization point. Cover the custody service, actual EventStore persistence/replay/source-reader process, Parties query/command process, and all enrolled downstream holders, serializers, caches and transport handoffs. Dapr remains the infrastructure boundary; direct backend credentials cannot bypass these controls.

Permit only while the complete trusted-time uncertainty interval is strictly before E and the unit is Active under current authority. Proposed maximum clock uncertainty is 100 ms; proposed maximum age of the authenticated time/authority observation is 1 second. Account for elapsed time and uncertainty through the decision. An observation alone is not a lease: current revocation/fence validation remains required. A paused clock, unknown elapsed time, clock rollback, stale epoch, authority outage or uncertainty above the bound denies before decrypt/use/handoff; no local-clock, cached-key or stale-checkpoint fallback is allowed. Conservatively refusing before E is an availability cost, never a later expiry.

At or after E, no new authorized service decision succeeds, even if cleanup has not run. Work authorized earlier receives no completion grace: late decrypt results are discarded and each subsequent controlled use/handoff requires a new check. A result already committed to an external transport or recipient before E may arrive after E; receivers under this contract must revalidate before use. This is a guarantee at recorded service authorization points, not a claim that the last instruction, physical byte emission, network delivery or already-released plaintext disappears at E. A process stop between its last check and a physical handoff is within A-03's released/in-flight limitation, not proof of instant erasure. Cancellation or a timeout grants no release authority.
```

**Consequence.** Replaces universal immediate-read/physical-release wording with observable authorization-point enforcement. It explicitly exposes in-flight delivery and the check-to-handoff pause gap. Neither a 1-second observation age nor a 5-second call timeout extends E.

## A-02 — Bounded destruction across copies, versions and backups

**Before (verbatim)**

[lifecycle](../../../../platform/_bmad-output/specs/spec-production-actor-history-custody/lifecycle-contract.md) — Durable authority, copies and receipts:

```text
No derived copy gets a later deadline. Copy registration and release must participate in current lifecycle fencing. At expiry, prevent new copies/writes/releases, reconcile or invalidate in-flight registrations, freeze the covered inventory generation and irreversibly destroy all decrypting paths. An in-flight write cannot commit after the final receipt unless it is already provably unrecoverable and covered by the same receipt. Missing/unacknowledged copies keep destruction pending.
```

[lifecycle](../../../../platform/_bmad-output/specs/spec-production-actor-history-custody/lifecycle-contract.md) — Durable authority, copies and receipts:

```text
Hard expiry includes cryptographic unrecoverability of ciphertext retained in immutable backups. A tombstone, application read denial, recoverable key deletion or a promise to purge later is insufficient. Q1/Q4 must establish the backend mechanism and its behavior during outages before admission claims `SourceExpiryEnforced`, `RestoreSafe` and `DerivedCopiesCovered`.
```

**After (proposed, unapproved)**

```text
At E, block authorized reads, new decrypting generations and new copy authority under A-01; fence and reconcile in-flight registrations against the original unit. Keep one durable original destruction operation with its original E and inventory generation. No failed attempt, restored connectivity or restart resets a destruction deadline.

By E + 900 seconds (15 minutes), irreversibly destroy every decrypting generation and recoverable plaintext/key copy in the online serving tier, subject only to A-03's explicitly disclosed inaccessible-memory limitation. By E + 2,592,000 seconds (30 fixed 24-hour days), irreversibly destroy or render cryptographically unrecoverable every recoverable durable copy and decrypting path in the full C01–C16 inventory, including offline, replica, native/versioned/off-site/immutable backup, escrow, seal/recovery dependency, drill and provider-operated recovery copies. The second deadline is original binding-effective-at + 395 fixed days; it is a destruction bound, not a readable retention window. Both are proposed hard upper bounds, not percentiles or worker-availability targets.

All-copy in this amendment means every inventoried durable copy and key path plus every controlled volatile holder for which retirement is independently evidenced. A-03 explicitly excludes a finite physical-erasure guarantee for already-released material in inaccessible stopped/crashed memory and uncontrolled recipients. An excluded or unresolved holder must remain identified as residual exposure; it must not be represented as erased or as authenticated absence. No unqualified statement that every physical copy is gone is permitted.

Cryptographic destruction requires proof that no retained generation, wrapping key, snapshot, export, backup, escrow, root reconstruction or alternative decryption route can recover the retained ciphertext. A shared surviving wrapping key plus a backed-up wrapped DEK fails this test. An unresolved released DEK/root also defeats a claim that ciphertext is permanently unreadable: either independently prove that key's retirement or physically eradicate the affected durable ciphertext copies by the same bound; otherwise remain Pending and record a breach when due. The holder exclusion cannot be used to certify readable ciphertext as cryptographically destroyed. Retaining permanently unreadable ciphertext is allowed; a tombstone, ACL denial, reversible version threshold, scheduled deletion or expiry timestamp is not destruction. Provider internal recovery copies remain in scope; undocumented deletion timing is a qualification gap, not an implicit exclusion.

Ordinary expiry, maintenance and restore must preserve unrelated live units, including a successor and another tenant. A shared key cannot be destroyed to satisfy one expired unit while destroying live units. Close every C01–C16 class with a real owner/location/generation and verified outcome or authenticated absence; include embedded OriginalEvidence, historical key generations and copy-registration races. No late copy may commit outside the closed inventory unless already unrecoverable under the same verified outcome.

Retry pending cleanup at intervals no greater than 30 seconds while the worker is operational; retain the existing 5-second bound on each provider wait. An independent monitor must alert within 120 seconds of detected cleanup/authority failure or a missed deadline. Outages do not extend either destruction bound. Unknown completion remains Pending; a missed bound additionally records a breach, fences the affected scope and blocks new admissions there. Recovery completes the same operation and retains the breach. It cannot backdate success or silently widen the bound.
```

**Consequence.** Permits recoverable online material for up to 15 minutes and recoverable durable backups for up to 30 days after E, while service decisions deny at E. A compromise/bypass during that interval may recover data. The 30-day bound deliberately does not certify inaccessible RAM; A-03 is a material scope reduction. A provider or immutable retention policy exceeding 30 days is incompatible unless separately amended.

## A-03 — Plaintext and DEKs already released to paused or crashed holders

**Before (verbatim)**

[direction](../../../../platform/docs/implementation/ext-parties-1-custody-direction-2026-10-25.md) — Contract retained (context; only the holder assertion is replaced here):

```text
Preserve Branch B/Organization, immutable tenant and Party identity, `party-actor-retention-v1` / `party-actor-history-v1`, and `ExpiresAt = original binding-effective-at + 31,536,000 seconds`: exactly 365 fixed days, with release only while `now < ExpiresAt`. Rebinding, rotation, retry, erasure, copying and restore cannot renew a predecessor. All-copy/historical-key destruction, stopped/crashed plaintext-holder expiry, unrelated-live-unit preservation, P-01–P-10 and F1 remain required. Dapr remains the infrastructure boundary. Local tests cannot establish Available; EXT-PARTIES-1 remains Committed and unavailable.
```

[proposal](../../../../platform/docs/implementation/ext-parties-1-custody-proposal-2026-10-25/PROPOSAL.md) — All-copy expiry and nonrollback acceptance requirements:

```text
Root destruction alone cannot erase plaintext already copied into a paused application's memory. Q1/Q3/Q6 feasibility therefore also requires a closed plaintext-holder boundary: hardware-contained AEAD, independently inventoried transient output holders, nonpageable/non-dumpable buffers with enforced deadline retirement, cancellation of in-flight decryption/release, and no ordinary application cache, log, immutable managed-string copy or export retaining actor evidence past its original deadline. Pause/suspend the owning processes before expiry, cross expiry with workloads/network unavailable, resume them and independently inspect their retained memory/copy outcomes. Prove no late release or recoverable plaintext/DEK through device or host crash dumps, snapshots or debugger paths. **The current .NET host paths do not establish those memory guarantees; no attested transient-holder implementation is delivered.** If the deployed plaintext path cannot meet the accepted all-copy scope, this design fails feasibility even if the HSM can zeroize roots. An owner-approved scope interpretation cannot be invented by the adapter.
```

**After (proposed, unapproved)**

```text
Deleting a backend key does not revoke plaintext or a DEK previously released to a process. This amendment does not guarantee physical erasure by E, or by a finite later instant, from an inaccessible paused/crashed process, stopped VM, physical memory remanence or an uncontrolled recipient. That residual exposure can last as long as the copy remains recoverable. A pre-expiry authorization and a later process-kill/cleanup attempt are not erasure evidence.

Every controlled holder must be enumerated before admission, with an owner, unit/deadline metadata, cleanup path and boundary of control. Use payload crypto without exporting DEKs where the eventually selected backend supports the accepted design; where DEKs are released, inventory them as separate holders under the same rules. This is not backend selection. Prefer bounded mutable buffers; neither zeroing one buffer nor avoiding DEK export proves removal of all plaintext/runtime/serializer/transport copies.

While a holder is scheduled and its independent control plane is healthy, the proposed maximum authorized holder lifetime is 5 seconds from acquisition, capped at E, and cleanup of owned buffers must finish within 1 second of authorized use ending or cancellation. The cleanup interval grants no extra authorized use. Reacquisition cannot extend the original E. These are operational cleanup bounds, not physical-memory guarantees. Any uncontrolled runtime copy prevents claiming complete volatile retirement.

On resume after any suspension or uncertain scheduling/time interval, perform no use, serialization or onward handoff until current authority, epoch, trusted time and original E are revalidated. If expired, denied or uncertain, discard the operation and purge accessible owned buffers before returning the holder to service. On a crash, fence the old incarnation; restart with a new validated incarnation and no restored plaintext/DEK state. A late provider completion is discarded. A process halted together with its controller has no promised cleanup clock: report the holder as unresolved, and do not count it as a successful bounded cleanup.

Persistent plaintext or plaintext-DEK caches, telemetry, swap, core/crash dumps, hibernation images, debugger exports and VM snapshots containing holder state are forbidden in the approved deployment. Their absence requires evidence. An accidental or discovered persistent spill is an incident and a durable copy under A-02, with the original E-based deadlines; detection does not restart them. A spill discovered after its deadline is already a breach. Restored spill images remain quarantined, and ordinary service cannot consume their contents. Retirement/eradication evidence must cover the actual image/media and decrypting paths, not only the restarted process.

The changed contract explicitly accepts the remaining inaccessible-memory and prior-recipient limitation only if owners approve this clause. If owners require a finite all-physical-copy deadline for those holders, this candidate amendment is insufficient: retain the strict contract and keep the feature unavailable until an independently enforceable holder mechanism is demonstrated.
```

**Consequence.** Removes the strict stopped/crashed-holder expiry guarantee; it does not replace it with an invented supervisor or TTL guarantee. The before quote is the retained unapproved HSM proposal’s articulation of the holder obligation, corroborated by the governing custody direction; its mechanism was never accepted or delivered.

## A-04 — Restore must deny service before the destruction deadline too

**Before (verbatim)**

[retention](../../../../agents/_bmad-output/implementation-artifacts/story-5-4-retention-custody-proposal-2026-10-06.md) — Required behavior and qualification:

```text
Restore quarantines source/snapshot/key copies until it can consult fresh independent lifecycle state. A backup of the application cannot roll that authority back. Missing/stale authority denies before decryption. Expired/destroyed units cannot be revived by recovering a key, resetting a clock or restoring an older ledger. Unexpired retained bindings must still reproduce their original actor/version/interval/positions without profile decryption.
```

**After (proposed, unapproved)**

```text
Restore quarantines source, snapshot, key and holder-image copies with ingress, effects and ordinary retention workers disabled. The current monotonic lifecycle/time authority and independent observer must survive outside every application, data, key and authority restore cut. Replication, a signature on an old checkpoint or restoring the authority alongside its data does not prove currentness. Fence the prior incarnation and reconcile current epoch, lineage, original E, inventory and terminal receipts before any service decryption, replay, rebuild or reopening.

At and after E, restored material must never authorize a service read or use. Before A-02 destruction completes, a retained backup may still be technically decryptable outside the controlled service: this is the explicitly accepted residual-recovery window, not strict cryptographic expiry. Quarantine is not destruction. By the applicable A-02 deadline every in-scope recovery path must be irrecoverable, with independent evidence. Lost authority keeps the service closed and does not restart destruction deadlines.

Restore must still reproduce each live unit’s original actor, binding version, interval and positions without profile decryption; preserve unrelated tenants and live successors. F1 must prove complete actor-free successor continuity after predecessor E, even while predecessor ciphertext remains pending destruction. Neither access to an expired predecessor during the destruction window, a current binding substitution nor retained actor/provenance/logical references or recoverable hashes may serve as F1 proof.
```

**Consequence.** Distinguishes service non-revival at E from cryptographic non-revival by the later bound. Keeps independent authority, positive live controls and F1; a restore denial alone cannot certify destruction.

## A-05 — Receipts and compatibility cannot silently retain the strict meaning

**Before (verbatim)**

[lifecycle](../../../../platform/_bmad-output/specs/spec-production-actor-history-custody/lifecycle-contract.md) — Durable authority, copies and receipts:

```text
A content-free authenticated receipt records exact original identity/evidence/policy/effective-at/expiry, stable operation ID, terminal authority revision/epoch, covered inventory generation/digest, per-copy-class irreversible outcomes and confirmation time. Minimize and protect references; Q3/Q7 determine owner retention without creating an indefinite actor history. One final receipt survives concurrent retries, restart and lost acknowledgements. No new evidence, unit, key or deadline is allocated during cleanup retry.
```

[lifecycle](../../../../platform/_bmad-output/specs/spec-production-actor-history-custody/lifecycle-contract.md) — Durable authority, copies and receipts:

```text
The SDK returns a Boolean, not that receipt. The provider must expose the original durable receipt to the authorized owner through its existing lifecycle/receipt mechanism; the caller persists the receipt reference and pending-work outcome. `IdentityHistoryCleanup.ProcessAsync` still requires destruction followed by a fresh denied read. A timeout may leave the provider completing work; retry the same original tuple and recover its result. `ProviderConfirmedDestroyed` alone is not an independent all-copy audit.
```

[custody_spec](../../../../platform/_bmad-output/specs/spec-production-actor-history-custody/SPEC.md) — Constraints:

```text
- Admission capability flags must describe proven backend guarantees. A fixture, TTL, tombstone, recoverable deletion or queued future purge cannot establish irreversible expiry.
```

**After (proposed, unapproved)**

```text
The revised compatibility contract must distinguish ServiceExpired, PendingDestruction, DurableCopiesDestroyed, HolderResidualOpen and VerifiedCompleteWithinApprovedScope. These are proposed semantic states, not published API names or installed fields. Preserve one original operation and authenticate its immutable unit/deadline, contract revision, authority epoch/revision, frozen inventory, all generations, per-class outcomes, actual destruction times, observer evidence and any residual-holder limitations. Record deadline breaches separately from eventual completion.

A final amended-scope receipt is permitted only after independently verified destruction of every required durable/key copy, reconciliation of all in-flight registrations, explicit holder retirement or owner-approved A-03 residual classification, and a fresh denied-read check. It must say exactly which guarantee it certifies and must never say that inaccessible memory was destroyed. It must remain recoverable on exact retry, restart and lost acknowledgement without allocating new evidence, a new unit or a later E. Unknown required-copy outcomes stay Pending. Final evidence is due no later than 900 seconds after actual completion, with an absolute ceiling of E + 2,592,900 seconds when destruction completes on time; a later receipt is an evidence breach, not a shifted deletion deadline.

Do not reinterpret the existing SourceExpiryEnforced, RestoreSafe or DerivedCopiesCovered flags, DestroyExpiredAsync true, ProviderConfirmedDestroyed or old receipts as proof of this weaker contract. Before implementation, owners must accept a versioned compatibility representation for A-01–A-05 and migrate every writer, reader, host, cleanup caller and verifier together. Legacy strict consumers must reject the amended assurance representation. The policy/purpose identifiers and E remain unchanged; the assurance/compatibility revision must make the changed guarantees distinguishable. Actual public schema/API naming remains owner work under decision 5.

Receipt and authority metadata remain protected and purpose-limited; content-free does not mean unlinkable. Q3/Q7 must approve retention, minimization and erasure, including F1 certificates, before admission. The historical proposed 400-day receipt retention is not accepted by this amendment.
```

**Consequence.** Prevents a weaker service/deletion promise from being labelled as the existing strict capability. A completed durable-copy receipt can disclose unresolved excluded memory, but cannot be an unqualified all-physical-copy certificate.

## A-06 — Complete acceptance and delivery gates

**Before (verbatim)**

[register](../../../../agents/_bmad-output/planning-artifacts/external-dependency-register.md) — Accepted Status Semantics:

```text
Changing an owner, artifact, target, compatibility behavior, verification command, or required evidence level invalidates prior acceptance and returns the record to `Uncommitted` until the changed commitment is accepted. A failed compatibility command makes the dependency unavailable and blocks consumers even if the record was previously `Available`.
```

**After (proposed, unapproved)**

```text
Changing an owner, artifact, target, compatibility behavior, verification command, or required evidence level invalidates prior acceptance and returns the record to Uncommitted until the changed commitment is accepted. A failed compatibility command makes the dependency unavailable and blocks consumers even if the record was previously Available.

If and only if explicitly adopted, A-01–A-05 replace the identified strict expiry/destruction/holder assertions for EXT-PARTIES-1 as one complete compatibility amendment. Everything else in the complete register artifact, P1–P4 and P-01–P-10 remains required: Branch B/Organization; immutable tenant/Party identity; create-only service provisioning and exact retries/conflicts; tenant authorization and non-disclosure; stable actor identity; current source/liveness/authorization; historical interval/version correctness; separate profile/history protection; unrelated-live-unit preservation; F1 and F2. No protection, secrets, host or Conversations guarantee is silently amended.

Approval of this proposed text alone supplies no qualified backend, installed target, operator, resource allocation, exact command or integration date. The changed complete commitment must be expressly accepted with all nine register fields and the coordinated source/package/image/install/profile/configuration/plan/trust targets, then pass its exact installed owner command with complete Level 4 evidence. Level 5 remains the consuming launch/RQ-1 gate. Missing or skipped required lanes fail; Local and LiveReadiness do not establish Available.

Until an amendment is adopted, preserve the accepted EXT-PARTIES-1 target 56cb4401c4179d431385ad3d931fefa402e4fc5a, historical missed date 2026-10-08 and Committed-but-unavailable status. Leave replacement target/date/command and installed bindings unresolved. The superseded November 13 feasibility and December 14 integration proposals are not commitments. Preserve October 18 acceptance/OQ-5.4-1 and the October 25 reconciliation, all attempts and failed/refused evidence. Any later changed accepted byte requires renewed acceptance; protection → secrets without host references → host composition → mutual cases remains the retained joint qualification order under its own accepted prerequisites.
```

**Consequence.** This draft changes no status. If later applied without a complete accepted replacement, compatibility acceptance is invalidated and the changed record is Uncommitted; approving prose alone cannot carry forward the old Committed assurance.

## Proposed bounds and consequences

| Bound | Candidate value, unapproved | Consequence |
| --- | --- | --- |
| Read/use authorization expiry | E exactly; no positive grace | E remains original + 365 fixed days; physical arrival or inaccessible RAM is a different claim. |
| Trusted time/currentness | Uncertainty ≤100 ms; observation age ≤1 s; current fence validation | May refuse early near E or during uncertainty/outage; never authorizes stale revocation evidence. |
| Online destruction | No later than E + 900 s | Up to 15 minutes of residual recoverability; online destruction does not prove backup destruction. |
| All durable/key-copy destruction | No later than E + 2,592,000 s | Up to 30 extra fixed days of residual recoverability; inaccessible-memory exclusion remains explicit. |
| Final evidence | Actual completion +900 s; on-time absolute ceiling E +2,592,900 s | Unknown receipt stays Pending; evidence delay is separately recorded and cannot extend destruction. |
| Cleanup and monitoring | Provider wait ≤5 s; operational retry interval ≤30 s; alert ≤120 s | Existing wait is retained; none is an expiry grace. Loss of monitoring is itself a qualification/operational failure. |
| Runnable holder | Authorized lifetime ≤5 s, capped at E; owned-buffer cleanup ≤1 s after use/cancellation | No authorized use after E; inaccessible stopped/crashed physical memory has no finite erasure guarantee. |
| Isolated drills | Fault ≤10 min; disposal ≤60 min after completion | Needs exact D03/D04 authorization; no production fault or extension of data deadlines is implied. |

The 15-minute/30-day limits are review choices, not derived vendor capabilities. They separate online cleanup from backup eradication and expose a finite risk window without claiming that a conventional backend is qualified. Finite tests can support or falsify an assurance case; they cannot prove a universal physical-erasure guarantee. A route that cannot meet these limits and scope is rejected, not silently grandfathered.
