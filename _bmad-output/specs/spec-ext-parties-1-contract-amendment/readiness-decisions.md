# Remaining owner decisions and readiness verdict

**Reviewable proposal; not implementation-ready.** User approval of A-01–A-06 text is complete. This run proposes concrete compatibility choices; no affected-owner approval of the new schemas or operational target is recorded. Decisions below route into the original five decisions without closing their Q/D/F/B obligations.

## Gates are separate

| Gate | Present state | What can establish it |
| --- | --- | --- |
| Amendment text | Approved 2026-10-25 at the exact clause hash in approval.md. | Already recorded; no repeat approval of unchanged text is needed. |
| Proposed representation | Proposed, awaiting review. | Parties/Platform/EventStore and affected consumer owners ratify api-schemas.md, predicates, Q5/F1 and migration. |
| Operational/privacy acceptance | Pending. | Actual accountable operators/privacy/recovery owners accept residual risks, bounds, RTO/RPO, copy/holder/receipt policies and failure model for the selected route. Text approval supplies no other owner's principal/sign-off. |
| Complete changed commitment | Not supplied or accepted. | All nine fields plus coordinated immutable targets/command/date and scope explicitly accepted. Changed incomplete adoption is Uncommitted. |
| Installed qualification | Not established. | Accepted exact installed owner command passes complete Level 4 with independent persisted/restore/fault evidence. Local/LiveReadiness cannot establish Available. |
| Consumer launch | Blocked. | All relevant exact dependencies Available and complete consuming Level 5/RQ-1 gates. |

While unapplied, preserve EXT-PARTIES-1 target 56cb4401c4179d431385ad3d931fefa402e4fc5a, missed 2026-10-08 and Committed/unavailable. No replacement date is set. The historical November 13/December 14 proposals remain superseded, not reactivated.

## Decision register

| ID | Accountable owner roles / original decision | Proposed choice or missing evidence to decide | Blocks |
| --- | --- | --- | --- |
| R1 | Parties + EventStore + Platform maintainers and affected Agents/Conversations consumers; decision 5 | Ratify exact party-actor-custody-amended-v2 token, schemas, closed enums/result branches, canonical bytes, public error mapping, new routes/types, strict rejection and affected current-identity versioning. Approve the complete scope together, not just a new Boolean field. | Shared public implementation; ES-1/ES-2/PA-1. Q5/Q6, F2, B03/B10/B15. |
| R2 | Parties/EventStore/custody maintainers; decision 5 | Ratify full original tuple, stable logical-intent comparator, revoke/rebind segment mapping and exact lookup. Inventory legacy IDs/units and decide which can be authenticated/migrated with unchanged ID/E; unsupported expired entries remain unavailable. | Production admission and migrations; PA-2/PA-3/PL-1. Q5, B09/B10. |
| R3 | Platform custody/key owner + project/funding owner; decisions 1/5 | Select a backend/key hierarchy only after an assessment proves independently erasable units and all generation/provider-recovery paths can meet A-02; choose crypto mode after holder/capacity evidence. Existing OpenBao is first conditional assessment candidate; ordinary key service in existing cloud is second. Neither is selected or qualified. Hardware work stays stopped. | Concrete production adapter; Q1, B07/B11. No backend API is invented here. |
| R4 | Funding/deployment/capacity owners; decisions 1/2 | Supply capped amount/currency, named engineering allocation, assessment timebox/review date and measured key/unit/traffic/copy/receipt capacity plus bounded protocol quotas. No spend, nodes or device count is inherited. | Funded assessment and production admission; Q1/Q4/Q6, D01/D04, B01–B03/B07/B13. |
| R5 | Independent authority/time/recovery owners + Platform/EventStore; decision 2 | Bind real surviving monotonic/current time authority outside every restore cut, trusted uncertainty/elapsed-time mechanism, writer/observer separation, epoch/fence/reissue and lost-witness behavior. Decide exact affected-scope fencing/reopen authority. Approved 100-ms/1-s bounds are fixed terms to meet, not an unanswered numeric choice. | All authorization/admission/recovery; Q2, D02/D03, B06/B08/B12. |
| R6 | Every copy/holder/work owner + privacy/custody; decisions 3/5 | Bind C01–C16 actual locations/generations/owners or authenticated absence; every real holder and spill controls; durable reservations/work/receipts, independent observers and receipt invalidation delivery. Approve metadata/anchor/F1 retention, minimization, erasure and exact-retry lookup window, with no 400-day default or indefinite actor linkage. | Finalization and all admission; Q3/Q7, D02, B05/B09/B10/B14. |
| R7 | EventStore authoritative-source + Parties domain + independent issuer/privacy owners; decision 5 | Ratify proposed per-position/segment F1 partition, numeric skipped-version/live-interval summary and pre-expiry source metadata production, pending-destruction distinction and legacy unsupported handling. Supply issuer/trust/revocation/rotation, validity and erasure policy. Do not retain expired actors/provenance/logical IDs or recoverable hashes as a shortcut. | F1 and successor availability; Q5/Q6/Q7, B14. |
| R8 | Operations + privacy + custody/key + recovery owners; decision 4 | Supply route-specific operational acceptance of 15-minute/30-day recovery exposure and unbounded inaccessible memory, availability/loss/RTO/RPO/maintenance envelope, monitored bounds and actual D03/D04 fault/cut/abort/disposal scope. Text-approved drill fault<=10 min and disposal<=60 min still require exact execution authority. Ordinary expiry/restore must preserve live controls. | Operational adoption and destructive tests; Q1/Q4/Q7, D03/D04, B07/B08/B10–B13. |
| R9 | Platform deployment/identity + Parties + EventStore + Tenants + independent observer; decisions 2/3/5 | Bind both real hosts, downstream holders, scoped runtime credentials/only-writers/renewal/revocation/deputies, two approved tenants, exact environments/resource UIDs/image IDs/package/bootstrap paths, value-free config/profile/plan and public trust. Confirm source-to-installed package mapping; dirty sibling files and root refs are not targets. | Installed composition; Q6, D01–D04, B01–B06/B09/B11–B13. |
| R10 | Parties Maintainer + Product/Governance + coordinated delivery owners; decision 5 | Supply complete achievable replacement: all nine register fields, immutable source/package/image/install/profile/config/plan/trust targets, real full B15 executable command and exact integration date. Then explicitly accept the complete changed commitment; execute only under its gates. | F2/Available/consumers; Q1–Q7, D01–D04, F1/F2, B01–B15. |

R1/R2/R7 are unresolved design decisions with concrete proposed answers. R3–R6/R8–R9 require actual feasibility, ownership and operational bindings, not further invented DTO fields. R10 is a later complete delivery commitment; a date should be supplied when supportable, not guessed to declare this spec ready. Owner review can approve contract-only work while production adapters, admission and installed qualification remain gated.

## Obligation coverage

The unchanged owner-decisions.md retains the exhaustive original crosswalk. This proposal does not replace the machine ledger or fill any of its original 661 decision/165 command-binding null paths.

| Original obligation | Current proposal / remaining gate |
| --- | --- |
| Q1 / Q2 / Q3 / Q4 | Backend, independent authority, copy/operator/receipt mechanism and operational failure envelope: R3/R4/R5/R6/R8. |
| Q5 / Q6 / Q7 | Original mapping, actual both-host/holder composition, privacy/erasure: R1/R2/R6/R7/R9. |
| D01 / D02 | Actual resources/tenants and scoped principals/renewal: R4/R5/R6/R9. |
| D03 / D04 | Exact isolated recovery/fault permission, cut, observer, abort and verified disposal: R8/R9. |
| F1 / F2 | Proposed complete actor-free successor proof and complete installed identity acceptance: R7/R10. |
| B01–B06 | Rendered release, environment/app execution, enrollment, credential and Dapr/public-trust isolation: R4/R5/R9. |
| B07–B10 | Backend capabilities, nonrollback time/authority, C01–C16 and original cleanup/receipt proof: R3/R5/R6/R8. |
| B11–B13 | Genuine immutable backups, isolated restore/live controls and authorized faults: R3/R8/R9. Historical hardware assumptions must be explicitly replaced, never marked passed. |
| B14 / B15 | F1 proof/negative cases and a real complete exact-target Live pass path: R7/R10. |

Preserve unrelated accepted gates: OQ-5.4-1, 192.168.1.30, trusted-envelope bounds 300/30/600/86400/604800, independent approval authority, Dapr-only custody, profile/history separation and the protection → secrets without host references → host composition → mutual cases order. Amendment actor-history bounds replace none of those terms. No prior attempt/journal/evidence is reset.

## Review disposition

The proposed contract and tests can be reviewed now. Full implementation readiness requires at least R1–R9 at the granularity needed by each owned change; complete replacement/availability additionally requires R10 and installed evidence. Approving this proposal alone would not supply a qualified backend, real principal, installation/trust hash or integration date. No question in this document is treated as an authorization received.
