---
title: 'Recover Story 8.6 status/evidence and refresh the Story 8.7 G5 receipt (SCP 2026-08-04)'
type: 'chore'
created: '2026-08-04'
status: 'done'
route: 'one-shot'
review_loop_iteration: 0
context: []
---

## Intent

**Problem:** Story 8.6's status was incorrectly flipped to `done` despite unresolved review findings still recorded in its own body, and Story 8.7's G5 payload-protection prerequisite receipt in the Story 8.3 matrix was still pinned to a stale EventStore identity (`7854f8e5` / `v3.90.0`), while the regenerated `epic-8-context.md` left `git diff --check` red on CRLF line endings.

**Approach:** Implement exactly what the approved `sprint-change-proposal-2026-08-04.md` §4 authorizes: restore Story 8.6 to `in-progress` with dated resolution evidence, refresh the Story 8.3 matrix's two Story 8.6 rows and the G5 row to the current EventStore `v3.91.0` / Builds `824d7ef` identity, normalize `epic-8-context.md` to LF, and route the blind-hunter review's real pre-existing findings to `deferred-work.md`. Story 8.7 itself stays `blocked` and G5 stays `needs-additive-api` throughout — no crypto/key-management deletion, no Story 8.7 status promotion.

## Suggested Review Order

**Story 8.6 status correction**

- Story-level `Status:` flipped back to `in-progress`, correcting the contradiction the SCP flagged.
  [`8-6-projection-and-query-sdk-migration.md:14`](8-6-projection-and-query-sdk-migration.md#L14)

- Dated Debug Log entry records the resolved package-mode build at EventStore `v3.91.0` without closing the remaining review findings.
  [`8-6-projection-and-query-sdk-migration.md:457`](8-6-projection-and-query-sdk-migration.md#L457)

- Completion Notes correction removes the stale "package handoff still pending" reasoning, keeping the real open findings as the reason status stays `in-progress`.
  [`8-6-projection-and-query-sdk-migration.md:467`](8-6-projection-and-query-sdk-migration.md#L467)

- Change Log 0.10 entry attributes the correction and reiterates no Story 8.7/G5 change is implied.
  [`8-6-projection-and-query-sdk-migration.md:589`](8-6-projection-and-query-sdk-migration.md#L589)

- `sprint-status.yaml` mirrors the correction and carries the rationale as a comment.
  [`sprint-status.yaml:160`](sprint-status.yaml#L160)

**Story 8.3 / G5 matrix identity refresh**

- New dated override note explains the `v3.91.0` identity refresh and its scope boundary (two Story 8.6 rows + G5 current-identity receipt only).
  [`story-8-3-platform-api-prerequisite-matrix.md:36`](story-8-3-platform-api-prerequisite-matrix.md#L36)

- G5 "Payload protection engine package" row: current-identity receipt moves to `1d6e9321` / `v3.91.0`; status stays `needs-additive-api`, Story 8.7 stays `blocked`, retention action stays open.
  [`story-8-3-platform-api-prerequisite-matrix.md:58`](story-8-3-platform-api-prerequisite-matrix.md#L58)

- EventStore projection/query SDK row: records the Administrator-authorized `v3.91.0` identity and the green package-mode build, plus a caveat that this narrow evidence does not mean Story 8.6 overall is complete.
  [`story-8-3-platform-api-prerequisite-matrix.md:55`](story-8-3-platform-api-prerequisite-matrix.md#L55)

- EventStore DataProtection row: same identity refresh and completeness caveat.
  [`story-8-3-platform-api-prerequisite-matrix.md:56`](story-8-3-platform-api-prerequisite-matrix.md#L56)

- Superseded historical `v3.90.0` note updated so it doesn't contradict the new current-identity receipt above it.
  [`story-8-3-platform-api-prerequisite-matrix.md:296`](story-8-3-platform-api-prerequisite-matrix.md#L296)

- `sprint-status.yaml` G5/Story 8.7 comment mirrors the same refreshed identity and unchanged blocked status.
  [`sprint-status.yaml:169`](sprint-status.yaml#L169)

**Working-tree cleanliness**

- CRLF line endings normalized to LF so `git diff --check` passes; no content change.
  [`epic-8-context.md:1`](epic-8-context.md#L1)

**Review findings routed to the ledger**

- Four pre-existing issues found by the blind-hunter review (non-reproducible G5 evidence command, dropped `epic-8-context.md` guidance, an untracked projection concurrency defect, an unattributed `sprint_plan.py` fix) logged for later triage rather than fixed here, since none were caused by this change.
  [`deferred-work.md:123`](deferred-work.md#L123)
