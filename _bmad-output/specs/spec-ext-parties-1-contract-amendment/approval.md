---
status: text-approved
approved_on: 2026-10-25
recorded_at_utc: '2026-10-25T21:06:02Z'
approval_scope: amendment-text-only
reviewed_revision: 5b9f80d0155807edbb4af9599b7d97e2f3f3ac6d
effective_contract_changed: false
replacement_commitment_accepted: false
---

# Approval of EXT-PARTIES-1 amendment text

The user replied **“approve”** after delivery of the completed review artifact
and the explicit statement that the amendment remained unapproved. This records
the user's approval of the complete A-01–A-06 text, including its numeric bounds,
released-holder limitations, compatibility requirements and acceptance gates.
The user text-approval step is complete.

The approved [clause text](clause-diff.md) is the version in Parties commit
`5b9f80d0155807edbb4af9599b7d97e2f3f3ac6d`, with SHA-256
`bd43978bdba457642c05b48b6bf56a3a38c044ff189a797d4ceba1c5a465aa46`.
Its bytes are preserved. Its “unapproved” labels describe the reviewed draft;
this dated approval record supplies the subsequent user text-approval status.
Any later change to those clause bytes needs a new approval record.

## Remaining decisions

The original [decision table](owner-decisions.md) is retained as the reviewed
baseline. This overlay records the subsequent approval without supplying
missing operational decisions or changing the original Q/D/F/B ledger.

| Decision | Current disposition |
| --- | --- |
| 1 — Funding | No funding amount, engineering allocation, assessment timebox or review date was supplied. |
| 2 — Resources and authority | The A-01 bounds are included in the approved text; actual resources, authority/time mechanisms and recovery bindings remain unresolved. |
| 3 — Operators and copy accountability | Principal, credential-reference, copy-owner and receipt-retention bindings remain unresolved. |
| 4 — Availability and exposure | The A-01–A-05 bounds and residual-holder limitations are included in the approved text. Remaining operational/privacy sign-offs, actual outage/data-loss envelope and authorized drills are still required. |
| 5 — Approval scope and delivery | Complete A-01–A-06 user text approval is recorded. Versioned compatibility representation, original-unit/holder/F1 decisions and complete nine-field replacement acceptance, target, command and delivery date remain open. |

## Effect and evidence

A-06 separates text approval from a complete changed commitment and installed
Level 4 qualification; Level 5 remains the consumer gate. The governing
EXT-PARTIES-1 record therefore retains its accepted target, historical date and
Committed-but-unavailable status. The replacement date and installed bindings
remain unresolved. Existing fail-closed behavior remains in effect.

This approval records the user's decision; it supplies no separate principal or
signature for another accountable owner. Backend selection, funding, resource
allocation, deployment and fault/destruction execution remain distinct scopes.

The earlier validation reports and archived checker are historical evidence
for the pre-approval revision. Their `proposed-unapproved` and sixteen-companion
checks describe that snapshot. This record and the updated SPEC.md intentionally
change its approval metadata; earlier reports are not regenerated or relabelled
as validation of the updated packet. Governing source files, original ledger
nulls and historical evidence remain unchanged.
