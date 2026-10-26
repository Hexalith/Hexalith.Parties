---
id: SPEC-ext-parties-1-contract-amendment
status: proposed-compatibility-contract
amendment_text_status: text-approved
implementation_ready: false
updated: 2026-10-26
created: 2026-10-25
approval_scope: amendment-text-only
approval_record: approval.md
companions:
  - approval.md
  - clause-diff.md
  - owner-decisions.md
  - verification-plan.md
  - ../../../../platform/docs/implementation/ext-parties-1-custody-direction-2026-10-25.md
  - ../../planning-artifacts/research/technical-custody-backend-feasibility-2026-10-25/research.md
  - ../../planning-artifacts/research/technical-custody-backend-feasibility-2026-10-25/deepening-pragmatic-options/research.md
  - ../../../../agents/_bmad-output/planning-artifacts/external-dependency-register.md
  - ../../../../agents/_bmad-output/specs/spec-story-5-4-dependency-unblock/parties-identity-contract.md
  - ../../../../agents/_bmad-output/implementation-artifacts/story-5-4-retention-custody-proposal-2026-10-06.md
  - ../../../../agents/_bmad-output/implementation-artifacts/story-5-4-dependency-reconciliation-2026-10-25.md
  - ../../../../platform/_bmad-output/specs/spec-production-actor-history-custody/lifecycle-contract.md
  - ../../../../platform/_bmad-output/specs/spec-production-actor-history-custody/SPEC.md
  - ../../../../platform/_bmad-output/specs/spec-production-actor-history-custody/implementation-plan.md
  - ../../../../platform/_bmad-output/specs/spec-production-actor-history-custody/test-plan.md
  - ../../../../platform/docs/implementation/ext-parties-1-custody-proposal-2026-10-25/COMMANDS.md
  - ../../../../platform/docs/implementation/ext-parties-1-custody-proposal-2026-10-25/PROPOSAL.md
  - brownfield.md
  - compatibility-contract.md
  - api-schemas.md
  - implementation-plan.md
  - acceptance-tests.md
  - readiness-decisions.md
sources: []
---

# EXT-PARTIES-1 compatibility and implementation proposal

**PROPOSED FOR REVIEW; implementation readiness is blocked.** A-01–A-06 have [user text approval](approval.md); the schemas, API names, F1 representation and migration choices below are new proposals. This update preserves the approved clause bytes. It does not adopt a replacement commitment, select a backend, supply operational acceptance or qualify an installation. Historical draft labels in the retained companions describe their original revisions.

This kernel and its companions define the proposed work. Read [brownfield.md](brownfield.md) for inspected source boundaries, [compatibility-contract.md](compatibility-contract.md) for assurance semantics, [api-schemas.md](api-schemas.md) for wire/API proposals, [implementation-plan.md](implementation-plan.md) for repository ownership and migration, [acceptance-tests.md](acceptance-tests.md) for future tests, and [readiness-decisions.md](readiness-decisions.md) for blocking decisions. The approved clauses and complete adopted identity contract remain load-bearing; historical hardware mechanisms and dates are superseded by the recorded direction.

## Why

Approved amendment text separates service authorization expiry from bounded durable destruction and inaccessible-holder exposure. Existing Parties, Platform and EventStore contracts encode strict expiry through Boolean capabilities and terminal destruction proofs. Owners need an explicit incompatible assurance representation and coordinated implementation proposal before these weaker guarantees can be implemented or accepted.

## Capabilities

- **CAP-1**
  - **intent:** Owners can review exact changes to expiry, destruction and released-holder guarantees.
  - **success:** The preserved A-01–A-06 clauses and proposed compatibility predicates identify all changed guarantees and bounds.
- **CAP-2**
  - **intent:** Owners can resolve remaining delivery decisions without losing obligations.
  - **success:** The five original decisions and readiness crosswalk retain Q1–Q7, D01–D04, F1/F2 and B01–B15, distinguishing proposed resolutions from missing owner choices.
- **CAP-3**
  - **intent:** Reviewers can check preservation and future qualification requirements.
  - **success:** Source observations and static validation identify protected inputs; future tests retain complete identity, F1/F2 and Level 4/5 gates without claiming runtime qualification.
- **CAP-4**
  - **intent:** Consumers can distinguish amended service, destruction and holder assurance from legacy strict assurance.
  - **success:** Closed versioned states and a rejection matrix forbid every amended-to-strict downgrade, including old Boolean success and terminal certificates.
- **CAP-5**
  - **intent:** Owners can exchange exact original-unit custody and identity evidence without renewing identity or expiry.
  - **success:** Proposed typed schemas cover original admission/lookup, current authorization, protection, copies, holders and identity results with immutable per-call tenant scope.
- **CAP-6**
  - **intent:** Operators can recover truthful destruction outcomes across failure.
  - **success:** One original operation and final scoped receipt survive retry, restart and lost acknowledgement; online, durable and evidence breaches remain independently recorded.
- **CAP-7**
  - **intent:** Live successors can remain resolvable after predecessor service expiry.
  - **success:** Actor-free F1 source partitions support exact live actor/version/interval continuity while predecessor destruction is pending, with no expired actor reconstruction.
- **CAP-8**
  - **intent:** Repository owners can coordinate adoption and qualification without partial compatibility cutover.
  - **success:** Owned changes, migration/rollback gates and acceptance vectors retain P1–P4/P-01–P-10, F2 and Levels 4/5; unresolved implementation prerequisites are explicit.

## Constraints

- Preserve Branch B/Organization and every existing tenant/Agent/Party link. Organization grants no human approver authority; retain complete P1–P4 and P-01–P-10.
- Preserve policy `party-actor-retention-v1`, purpose `party-actor-history-v1`, trigger `binding-effective-at`, and exclusive E = original effective instant + exactly 31,536,000 seconds. Rebind, revoke, retry, rotation, erasure, copying and restore never renew predecessor evidence.
- A-01–A-05 are one guarantee change: authorization at E; online destruction by E+900 s; durable/key-copy destruction by E+2,592,000 s; evidence within actual completion+900 s, capped at E+2,592,900 s for on-time destruction. Inaccessible released memory has no finite physical-erasure promise.
- Keep independent current authority, all C01–C16 copy obligations, both real owning hosts and every holder. Unknown key/durable-copy outcomes prohibit final completion; unresolved released keys defeat crypto-erasure of retained ciphertext.
- Use separate versioned assurance and proof types; legacy strict flags, receipts, APIs and certificates retain their strict meaning. Dapr and EventStore remain their respective infrastructure and domain-persistence boundaries.
- Preserve accepted target `56cb4401c4179d431385ad3d931fefa402e4fc5a`, missed date `2026-10-08`, Committed/unavailable status, original attempts, ledger nulls and OQ-5.4-1 while this proposal is unapplied. An applied changed commitment requires renewed complete acceptance.

## Non-goals

- Implementing or deploying code, selecting or qualifying a backend, assigning real operators/resources/credentials, approving spending or faults, or promising a delivery date.
- Replacing immutable identities, weakening other custody/protection/secrets/host/Conversations contracts, retroactively converting strict evidence, or treating text approval or local tests as availability.

## Success signal

A reviewer can accept or revise each proposed schema, state predicate, receipt rule, F1 proof and repository change against A-01–A-06, identify every remaining owner prerequisite, and derive the future acceptance suite without inventing runtime bindings. The proposal itself leaves all consuming execution gates closed.

## Assumptions

- The `V2` names, exact assurance token, tuple mapping, segmented F1 representation, canonical bytes and closed cutover are recommendations awaiting affected-owner ratification.
- Checked local sources establish the brownfield observation only. Source HEADs, dirty sibling files and root reference revisions are not accepted delivery targets.

## Open Questions

- Which affected owners ratify the proposed compatibility/API/Q5/F1 and legacy-data migration choices (R1, R2, R7)?
- Which backend, funding/capacity and independent time/nonrollback mechanism can meet the approved scope (R3–R5)?
- Who owns actual copy/holder/work/receipt and erasure/privacy policies and the operational failure envelope (R6, R8)?
- What exact host/resource/operator/trust/fault bindings and complete target, executable command and accepted date will be supplied (R9, R10)?
