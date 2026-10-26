# Proposed assurance and lifecycle contract

All names in this companion are proposed. A-01–A-06 text approval is recorded in approval.md; operational adoption and these representation choices still require R1–R10 in readiness-decisions.md.

## Exact assurance identity

Use schemaVersion = 2 and assuranceContract = `party-actor-custody-amended-v2` for the complete A-01–A-05 guarantee. Authenticate both inside payload envelopes, current authorization observations, source/F1 proofs, lifecycle responses and receipts. A profile pins this exact token and guarantee-profile digest; it is not an opportunistic list from which a provider chooses a weaker level. Policy `party-actor-retention-v1`, purpose `party-actor-history-v1` and trigger remain unchanged.

Legacy strict means the existing SDK contract, not a newly invented v1 token. There is no implication from amended completion to legacy strict expiry: even a finally destroyed amended unit may have survived E. v2 assurance is never converted to the old three true flags, DestroyExpiredAsync true, ProviderConfirmedDestroyed or an old terminal receipt/certificate.

## Immutable original and transition mapping (Q5 proposal)

An original unit is the full tenant/domain/Party + environment/instance + policy/purpose + bindingVersion + original binding-effective instant. Domain must be party. UnitId/EvidenceId and admissionRevision are allocated once in a durable reservation; E is derived, never caller-overridden. Normalize instants to UTC without changing the instant; reject overflow for E and every required destruction/evidence deadline before admission.

A separate idempotency index binds authenticated tenant/Party + transition kind + logicalId to the canonical complete accepted intent and its original output references. Include expected source/binding revisions, actor/revision, effective time and original-unit references in that intent. A matching retry returns the same reservation/evidence/version/operation and original effective instant; changed intent conflicts. Same binding slot occupied by a divergent logical intent conflicts. Pending reservation/key allocation is reconciled after crash, never abandoned into an untracked orphan. Durable lookup is required before allocating on retry.

Establish creates one original. Rebind at T closes the predecessor interval at T using its original unit/E and creates a successor only for its own evidence. Revoke records the closing boundary under the predecessor's original unit; it creates no later-lived copy of predecessor actor/provenance/logical material. Split multi-unit transitions and snapshots into independently protected segments. Every duplicate OriginalEvidence follows the predecessor's E. Exact command retries never choose a new T or new identity. Do not infer or repair legacy Party IDs; an ambiguous existing mapping stays unavailable pending same-ID reconciliation approved by its owners.

## Orthogonal states

These are independent fields, not one linear enum. Combinations carry information a single “Destroyed” state loses.

| Field / value | Meaning and allowed transition |
| --- | --- |
| serviceState: Active | The unit is before E and currently eligible. This is necessary but not sufficient for an authorization decision; every use still needs current proofs. |
| serviceState: Closed | Before E, authorization, revocation, uncertainty, quarantine or authority failure prevents service. Recovery can reopen only before E with current authority. |
| serviceState: ServiceExpired | Derived at E even if no worker ran. Terminal for service; later time/restore cannot return it to Active. |
| destructionState: NotDue | Before E; no amended ordinary-expiry destruction claim. Separate revocation closes service and never renews E. |
| destructionState: PendingDestruction | At/after E, any required online/durable/key/in-flight/verification outcome remains unknown or incomplete. Fencing must already deny service. |
| destructionState: DurableCopiesDestroyed | Independent evidence closes every required durable/key path and registration race. This intermediate observation is not a final receipt; holder classification or final denied-read verification may remain outstanding. |
| destructionState: VerifiedCompleteWithinApprovedScope | One final receipt meets the predicate below. It never claims inaccessible memory erased, strict instant expiry or absence of historical breaches. |
| holderState: RetirementVerified | All enumerated released volatile holders are independently retired or authenticated absent. Zeroing one buffer, process exit or lack of an acknowledgement is insufficient. |
| holderState: HolderResidualOpen | Every remaining holder is identified and classified under operationally accepted A-03 residual scope. The residual remains visible even alongside VerifiedCompleteWithinApprovedScope. |
| holderState: Unresolved | Unknown holder, uncontrolled runtime copy without classification, or missing required owner acceptance. Final amended completion is prohibited. |

Track onlineCompletedAt separately. Durable completion does not prove the online deadline was met. ServiceExpired + PendingDestruction + HolderResidualOpen is valid; so is ServiceExpired + VerifiedCompleteWithinApprovedScope + HolderResidualOpen. Closed/Active cannot coexist with final expiry destruction. Missing fields and unknown enum values fail closed.

Record sticky append-only breach facts `OnlineDestructionDeadlineMissed`, `DurableDestructionDeadlineMissed`, `FinalEvidenceDeadlineMissed`, `HolderCleanupBoundMissed` and `PersistentSpillDetected`, each with dueAt/detectedAt/observedAt and incident reference. A previously unresolved outcome whose completion is later proven on time may be annotated as such; the recorded missed-verification/failure observation is retained. An actual late destruction remains a breach after eventual completion. Alert within 120 seconds of detected cleanup/authority failure or a missed deadline. Fence the affected scope and block new admissions there when a destruction bound is missed; scope definition and recovery authority require R5/R8, not an automatic whole-tenant interpretation.

## Authorization predicate and holders (A-01/A-03)

For admit, unwrap/decrypt, attribution use, derived-copy commit and each controlled handoff: authenticate exact original unit, caller/recipient scope, current actor and source authorization, epoch/incarnation/fence and independent trusted time. Revalidate after every await, on resume/uncertain scheduling and immediately before the recorded authorization point. A valid observation is not a lease: the effect/release participant must validate the current fence/revocation state.

The trusted-time observation supplies an interval and uncertainty no greater than 100 ms; its authenticated observation age must be at most 1 second, accounting for measured elapsed monotonic time and uncertainty through the decision. Permit only if the entire resulting interval is strictly before E, the unit remains Active and every independent authorization is current. Upper bound equal to E denies. Unknown elapsed time, stopped/rollback clock, stale authority/epoch or missing authority denies; do not fall back to local wall time, cached keys or checkpoints. Near-E conservative refusal is allowed. Current time/authority mechanism is R5, not selected here.

Record authorization-point time/proof separately from physical output. A provider result completed after E is discarded; cancellation/timeouts grant no permission. An externally committed pre-E result may arrive later; every enrolled recipient revalidates before use. A process pause between check and physical handoff is the disclosed A-03 limit, not evidence that physical emission was stopped at E.

Enroll both actual owning hosts and all controlled holders before admission, including SDK, serializer, cache, transport, downstream and DEK holders. Bind each to original unit/E, owner and process/transport incarnation. While runnable with healthy independent controls, authorized lifetime is at most 5 seconds from acquisition, capped at E; owned-buffer cleanup completes within 1 second after use ends/cancellation and grants no extra use. Reacquisition does not reset E. Resume requires revalidation before use/serialization/handoff; expired/uncertain work is discarded and accessible buffers purged before serving again. Crash fences the old incarnation; restart never restores readable holder state. A stopped holder/controller has no promised cleanup clock and remains explicit residual/unresolved exposure.

Persistent plaintext/DEK caches, telemetry, swap, dumps, hibernation, debugger exports and holder VM images are forbidden in the approved deployment, with independently verified absence. A discovered spill is an incident and an A-02 durable copy with the original deadlines, even if discovered after them. Restored spill images stay quarantined. Recipient scope never expands beyond the complete identity contract.

## Destruction and finalization (A-02/A-05)

At E block service and new decrypting/copy authority, fence in-flight registrations, and preserve the original destruction operation and inventory lineage. Freeze the inventory generation only with an authenticated closure of every registered/in-flight obligation. Late writes must be rejected, cancelled with proven absence, or already irrecoverable under the same receipt. Discovery of an unclosed copy invalidates the scope claim; preserve the old receipt and publish an incident/invalidity fact rather than rewriting history.

Online tier: irreversible retirement of every online decrypting generation and recoverable plaintext/key copy by E+900 seconds, subject only to the disclosed inaccessible-memory limitation. Full durable/key scope: destruction or cryptographic unrecoverability by E+2,592,000 seconds. These are hard upper bounds; outages do not pause them. Retry pending cleanup at intervals no greater than 30 seconds while workers operate; each provider wait is at most 5 seconds. Unknown completion remains Pending. Existing owner work/scheduling mechanisms own retries; the one-unit cleanup class does not become a scheduler.

Cryptographic unrecoverability requires closing all generations, wrapping/reconstruction, backup, escrow, provider-internal recovery and seal paths. A backed-up wrapped DEK plus surviving wrapping key fails. A residual released DEK/root also defeats a cryptographic claim for retained ciphertext: prove its retirement or eradicate the affected durable ciphertext copies by the same deadline; otherwise remain Pending and breach when due. HolderResidualOpen is never a shortcut around that test. Preserve successors and other tenants during normal expiry, maintenance and restore; broad shared-key destruction is invalid.

Finalization requires all of: exact original identity/E/revision/operation; frozen closed inventory and all generations; independently authenticated C01–C16 outcomes or absences; reconciled registrations; every holder retired or covered by actual operational A-03 acceptance; fresh authoritative denial specifically because of ServiceExpired; and a verified final receipt with its exact guarantee scope and residual limitations. A timeout, false CanRead, missing credentials or service outage cannot substitute for the authoritative denial or destruction evidence.

Final evidence is due within 900 seconds of independently established actual scoped completion, with absolute ceiling E+2,592,900 seconds when destruction completes on time. Preserve per-copy actual times and compute actual completion as the latest required destruction/retirement/residual-classification/race-reconciliation completion, excluding receipt publication and its final verification round. Later fresh verification must not reset that clock. Record actual destruction lateness and evidence lateness separately. If actual completion cannot be established, remain Pending; do not start a convenient clock at lookup or restored connectivity.

## Inventory closure

| Class | Scope that requires owner/location/generation and outcome or authenticated absence |
| --- | --- |
| C01 | Original source events and all sealed segments. |
| C02 | Current snapshots, including embedded OriginalEvidence. |
| C03 | Older snapshot/state versions. |
| C04 | Projections and read models. |
| C05 | Caches and controlled volatile holders, with residuals separately identified. |
| C06 | Broker, outbox, DLQ and replay copies. |
| C07 | Spools and authorized exports. |
| C08 | Telemetry/log artifacts; plaintext absence must be proved. |
| C09 | Replicas. |
| C10 | Native backups. |
| C11 | Off-site, versioned and immutable copies. |
| C12 | Raw, wrapped, escrowed and rotated key generations. |
| C13 | Key backups and seal/recovery dependencies, including provider internal recovery. |
| C14 | Drill/quarantine copies and accidental persistent holder images. |
| C15 | Work, receipt and authority metadata under its separately approved minimized retention policy. |
| C16 | F1 continuation material under its approved expiry/erasure/issuer policy. |

For C15/C16, close attribution-bearing payload/key copies under A-02; do not claim required minimized receipt/authority metadata was deleted when it remains for its separately approved audit purpose. A receipt inventories its own reference/metadata retention obligation without recursively requiring its own destruction before it can exist. Retention/minimization/erasure of that metadata is a mandatory pre-admission Q3/Q7 decision; no 400-day default is adopted.

## Restore and compatibility

Restore disables user ingress, effects and ordinary retention workers until independent current authority outside every application/data/key/authority restore cut validates epoch, lineage, original deadlines, inventory and receipts. Fence old incarnations. A signature on a restored checkpoint is insufficient. Authorized recovery cleanup can run under separate current hooks; ordinary workers stay closed. Before the destruction deadline, denial of expired service coexists with possible technical backup recovery outside the service; record that exposure. After the deadline require irreversible path closure, not just denied API access. Lost authority does not extend deadlines. Prove live controls in the same Party and another tenant, and F1 continuity without profile decryption or expired predecessor use.

| Producer / consumer | Required behavior |
| --- | --- |
| Amended V2 → legacy strict API/client/cleanup/verifier | Reject before decrypt/use/effect. Provider never implements a success-mapping shim. No v2 DTO serialized as the old evidence with true flags. |
| Legacy v1 artifact → amended-only V2 profile | Reject as AssuranceContractMismatch; only an explicit owner-approved migration may create a separate v2 attestation for an authenticated original. No inference from policy ID. |
| Unknown/missing token/version; conflicting outer/header/payload/receipt revisions | Reject as UnsupportedAssurance or AssuranceContractMismatch; do not probe an older endpoint or retry under v1. |
| V2 → accepted V2 profile, incomplete required proof | Unavailable/Pending as applicable; no partial history result, current-binding fallback or authoritative empty approver roster. |
| V2 completion with residuals or breaches → V2 verifier | Check exact approved scope and expose residual/breach facts. Completion does not clear the incident or imply on-time destruction. |

Genuinely unchanged strict contracts can continue serving isolated strict data if they are independently qualified. A mixed deployment cannot serve amended data through an old path. Endpoint/version routing, incompatible event names, unknown protected-format rejection and fencing of old writers are required even where old JSON deserializers ignore unknown fields. Assertions in a new field alone cannot force already-installed old code to reject.
