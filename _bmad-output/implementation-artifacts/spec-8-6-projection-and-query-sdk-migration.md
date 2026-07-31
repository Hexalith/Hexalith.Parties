---
title: '8.6 Projection and query SDK migration'
type: 'refactor'
created: '2026-07-31'
status: 'draft'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-8-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md'
  - '{project-root}/references/Hexalith.EventStore/_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** EventStore Story 1.20 is `available`, but Parties consumes source SHA `9b9c776791c149cab26c795a476d23d3d11f7796` and package default `3.86.0`, neither of which is the approved identity `fa2d1c9910f8976553adb33dcdb1c9ff2ea75594`. SDK transport, erasure, parity, rebuild, and rollback are also unproven, so every local compatibility path must remain.

**Approach:** Pin the approved source identity in a dependency-only root checkpoint; add SDK projection/query consumers beside retained local paths; narrowly admit EventStore-only internal routes; prove topology, parity, full rebuild, and rollback; only then remove rollback-only code.

## Boundaries & Constraints

**Always:** Preserve deny-by-default/no-public-API topology; query names, page/page-size clients, freshness states, read-model identities, tenant isolation, cancellation, and GDPR Art.20/Art.30/no-leak behavior. Use the approved async projection/rebuild, read-model store/batch/conditional-erasure/freshness, query-handler, write-policy, and cursor-codec seams. Retain all 18 documented rollback artifacts, both actor interfaces, registrations, and ten `NotImplementedException` fallbacks until rebuild and rollback gates pass. Keep the Story 8.3 owner status `available` while recording consumer gates.

**Ask First:** Any other identity or consumption mode; any public/external ACL expansion or replacement transport; any dependency commit with unrelated changes; any cursor contract change beyond an additive opaque option preserving page/page-size clients.

**Never:** Infer approval for descendants, tags, or packages; rehost EventStore lifecycle actors in Parties; use `Type.GetType`; expose tenant/read-model/key material; pull Story 8.7 crypto extraction into scope; delete or disable rollback early.

## I/O & Edge-Case Matrix

| Scenario | State | Required behavior | Failure behavior |
|----------|-------|-------------------|------------------|
| Delivery | replay zero, duplicate, out of order | SDK detail/index equal local end state; atomic checkpoint | Bounded degradation; no key leak |
| Query | detail/list/search/GDPR; fresh or stale | Same validation, paging, freshness, erasure exclusion | Same bounded rejection or last-known result |
| Cursor | absent, valid, expired, tampered, wrong scope | Existing paging works; any cursor is opaque/key-ring compatible | Deterministic rejection |
| Erasure | detail plus shared index | Remove target only, under coordination | Retry conflict; preserve unrelated entries |
| Rebuild/rollback | empty SDK store, aggregate history | Normalized rebuild equals replay; both cutover directions preserve reads | Block deletion on any mismatch |

</frozen-after-approval>

## Code Map

- `references/Hexalith.EventStore` -- exact approved root gitlink.
- `src/Hexalith.Parties.Projections/Handlers/` and `src/Hexalith.Parties/Queries/` -- reusable folds and new SDK consumers.
- `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs` -- dual-path registration/cutover boundary.
- `src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.parties.yaml` -- internal route allowlist.
- `tests/Hexalith.Parties.Projections.Tests/` and `tests/Hexalith.Parties.Tests/` -- parity evidence.

## Tasks & Acceptance

**Execution:**
- [ ] `references/Hexalith.EventStore` and evidence artifacts -- create an otherwise-clean dependency checkpoint at the approved SHA; run the packet's exact A/B/C source receipt; refresh stale matrix/sprint identities.
- [ ] Test projects -- add a dual-path harness covering the matrix before registration changes.
- [ ] Projection handlers -- adapt pure folds to approved async persistence, batching, coordinated erasure, freshness, and staged full rebuild; explicitly register their assembly.
- [ ] Query handlers -- implement one SDK handler per existing query and approved cursor/key-ring use without breaking clients.
- [ ] ACL and topology tests -- allow only EventStore and exact POST query/projection/rebuild routes; retain default deny.
- [ ] Stores and registrations -- full rebuild/diff, SDK cutover, and rollback rehearsal while locals remain.
- [ ] Rollback set -- after an evidence manifest marks every gate green, delete it and all ten fallbacks; rerun all gates.

**Acceptance Criteria:**
- The packet receipt proves the root gitlink and checkout exactly equal the approved SHA and the dependency checkpoint is otherwise clean.
- Only EventStore can invoke approved internal SDK routes; gateway/public behavior is unchanged.
- Dual-path tests agree for delivery, queries, freshness, erasure, diagnostics, GDPR, and normalized read models without leakage.
- Full rebuild matches aggregate replay and rollback restores equivalent reads. Any failed gate preserves all rollback code; all-green cleanup leaves no Parties-hosted platform lifecycle mechanics.

## Spec Change Log

## Design Notes

The internal EventStore allowlist is not a public API expansion. Source mode is the only executable approved identity because proof-package bytes are unavailable and the approved SHA has no exact tag. Normalize fold-generated timestamps during rebuild comparison.

## Verification

**Commands:**
- Story 1.20 A/B/C source procedure -- exact identity, authorization, and cleanliness pass.
- Build and directly execute `Hexalith.Parties.Projections.Tests` and focused `Hexalith.Parties.Tests` in Debug source mode -- parity/rebuild/rollback pass.
- `pwsh scripts/test.ps1 -Lane unit` and `pwsh scripts/test.ps1 -Lane topology` -- pass; a topology skip is not credited.
- `bash scripts/check-no-warning-override.sh && git diff --check` -- pass.
