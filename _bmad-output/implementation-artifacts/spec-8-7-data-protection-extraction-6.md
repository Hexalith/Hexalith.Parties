---
title: '8.7 Data-protection extraction prerequisite revalidation'
type: 'refactor'
created: '2026-11-03'
status: 'blocked'
route: 'oneshot'
review_loop_iteration: 0
baseline_commit: 'fcf7cbc86076ce43f0982e4959da9691feaf2d3a'
story_key: '8-7-data-protection-extraction'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/8-7-data-protection-extraction.md'
  - '{project-root}/_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 8.7 must move generic payload protection to an approved shared provider while preserving Parties GDPR policy and compatibility. Its current G5 prerequisite remains `needs-additive-api`: EventStore 8.5 is in progress and 8.6–8.11 are backlog; a consumable runtime/backend, release enrollment, dual-provider parity, post-v2 rollback, named availability approval, and I2/I19a classification are absent.

**Approach:** Revalidate the prerequisite against current committed source and effective package identities, run the narrow structural evidence checks, and record the exact result in the story, G5 ledger, and test summary. Keep Story 8.7 blocked and retain all local implementation, DI, public APIs, dependencies, and rollback files. No production extraction or deletion begins without owner 8.11 closure and the existing adoption/deletion proofs. Record only checks actually run; source presence and package selection grant no G5 approval. This run's executable footprint is a reversible documentation update because the existing prerequisite forbids production work; it does not complete the extraction story.

</frozen-after-approval>

## Implementation Notes

- The original extraction goal remains blocked by its explicit G5 prerequisite. This run completes only prerequisite revalidation; it does not mark the story implemented or advance sprint status to in-progress/review/done. The earlier approved extraction specifications remain unchanged.
- Regenerated stale Epic 8 context, refreshed the story's current gate receipt, and appended a dated G5 ledger observation and test summary. No production, dependency, gitlink, API, DI, or rollback file changes.
- `tests/story-8-7-g5-2026-11-03/audit.py` records all 32 passing structural commands, hashes for the 24 retained files, four local registrations, exact clean source identities, and package selectors in `audit.json`. I2/I19a remains pending.
- The pinned 4.41.1 tool prepared source mapping with advisories HXW009/HXW010/HXW011; the Debug test-project build then failed with 15 Tenants CS0234/CS0246 errors for missing EventStore checkpointed projection types. No fresh test assembly was produced or executed. Exact invocation and raw output are retained in the evidence directory. Static evidence grants no product, dual-provider, GDPR, rollback, KMS, or G5 approval credit.
- Source inspection still shows EventStore 8.5 in progress and 8.6–8.11 backlog, a non-packable core and Server no-op, absent AzureKeyVault project and engine catalog/release enrollment, and no 8.11 closure. These are missing owner deliverables, not an unresolved implementation preference. Halt extraction without asking to waive them.

- Independent Blind Hunter review returned nine findings. All were verified and patched; none deferred. Restored concrete context constraints, parsed the policy-hook decision cell, compared retained sources against the baseline, documented JSON capture, and retained the original source mapping with the full ten-dependency identity/cleanliness receipt. The strengthened audit passed again. The extraction remains blocked; no completion, review-state promotion, or commit was performed.

- 2026-11-03 — User approval (`approve`) accepts the reviewed prerequisite evidence update and its local commit. Story 8.7 remains blocked; owner G5 delivery, policy-hook classification, parity, and rollback requirements retain their existing gates.

## Review Triage Log

- R1 — medium, patched: regeneration removed the distinct cursor/key-ring consumption gate. Restored exact DataProtection ledger identity, cursor purpose/scope, persisted key rings, and rollback requirements; G5 remains separate.
- R2 — medium, patched: regeneration compressed G8 authentication acceptance into an ambiguous label. Restored authority, issuer, workload/EventStore audiences, HTTPS metadata, signing-key clearing, secret-free publication, granular registration/client coexistence, workload disposition and rollback criteria.
- R3 — medium, patched: approved routes alone do not restrict callers. Restored EventStore-only invocation and retained separately governed subscription ingress.
- R4 — medium, patched: generic own-data wording omitted the explicit defense-in-depth comparison. Restored `aggregateId == party_id` from architecture I6.
- R5 — medium, patched: generic restriction wording omitted an approved legal behavior. Restored consent edits allowed during restriction and rejected during erasure from I7; also retained honest freshness and open accessibility-deferral constraints.
- R6 — medium, patched: owner/date placeholders could satisfy a whole-row `Pending` search. The audit now parses and validates the decision column and records its actual text.
- R7 — low, patched: presence/hashes alone did not verify unchanged retention. The audit now compares all 24 retained sources plus the compatibility harness and DI source byte-for-byte against the exact recorded baseline before emitting success.
- R8 — low, patched: the documented invocation emitted stdout without reproducing the named file. The replay command now redirects to `audit.json`.
- R9 — medium, patched: the failed build's Tenants/Platform provenance was not retained. Added the original tool `source-mapping.json`, recorded its content and byte hashes, and verified all ten mapped root gitlinks/checkouts and cleanliness, including Tenants and Platform.
