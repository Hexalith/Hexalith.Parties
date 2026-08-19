---
title: Sprint Change Proposal — Backfill Authority for the Story 8.10 FrontComposer Shell Slice
date: 2026-08-19
author: Administrator
workflow: bmad-code-review
mode: batch
scope_classification: moderate
status: approved
approval_required: true
approval: approved
approved_by: Administrator
approved: 2026-08-19
trigger: >
  The Story 8.10 code review found that the diff adopted the FrontComposer shell
  (skip links and landmarks) in place of the Parties-owned surface while Story 8.9
  is backlog and the Story 8.3 "FrontComposer UI primitives" row is still
  needs-additive-api with an explicit keep-rollback-path clause naming skip links.
  The change was authorized in-session and recorded only in test-summary prose;
  no matrix row, spine disposition, sprint-status entry, or dependency identity
  receipt carries that authority.
baseline:
  parties_commit: 2b63ab9
  parties_branch: main
  frontcomposer_prior_gitlink: 97f44c499e83a0ffbf054febd0aab384054ea39e
  frontcomposer_selected_gitlink: 7a337a21d4ba261bf27aeb3feedde47789f0160a
  memories_prior_gitlink: 98e27534
  memories_selected_gitlink: 003fd21488d60307cd932a3139f69319a25cea66
  polymorphicserializations_prior_gitlink: 5e01ff3ab7a7393c2252ee0c2fc1247556e7c129
  polymorphicserializations_selected_gitlink: 0dca9e9d3f8b2a20ba426b84fa575ab4e7b5562b
related:
  - _bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md
  - _bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md
  - _bmad-output/implementation-artifacts/deferred-work.md
  - _bmad-output/implementation-artifacts/sprint-status.yaml
  - _bmad-output/implementation-artifacts/tests/test-summary.md
  - _bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-08-02-story-8-6-eventstore-identity-authorization-backfill.md
---

# Sprint Change Proposal — Backfill Authority for the Story 8.10 FrontComposer Shell Slice

## 1. Issue Summary

Story 8.10 is a closure-and-evidence story. Its frozen Boundaries forbid
weakening gates and forbid treating a checkout or compile as consumption proof.
During the 2026-08-18 remediation the story nevertheless executed a slice of the
deferred Story 8.9 / G4 work:

- `src/Hexalith.Parties.UI/Components/Layout/MainLayout.razor` dropped the
  Parties-owned `.parties-skip-link` anchors, `#parties-main-content`
  (`role="main"`), and `#parties-app-navigation` (`role="navigation"`), and now
  delegates to `FrontComposerShell` for `#fc-main-content`, `#fc-nav`, and both
  skip links.
- `src/Hexalith.Parties.UI/Components/Layout/MainLayout.razor.css` lost every
  `.parties-skip-link` rule and its `prefers-reduced-motion` block.
- `references/Hexalith.FrontComposer` advanced from
  `97f44c499e83a0ffbf054febd0aab384054ea39e` to
  `7a337a21d4ba261bf27aeb3feedde47789f0160a`.

The motivation was legitimate and is recorded: the Playwright lane was failing
with duplicate `Skip to content` strict-locator ambiguity between the Parties
and FrontComposer shells, and `test-summary.md` §"Authorized dependency and
shell remediation — 2026-08-18" states *"The user authorized the two
owner-boundary changes recorded above."*

The defect is not the decision. It is that the decision lives only in
`test-summary.md` prose, which is an evidence log, not a governance surface.
None of the artifacts the Epic 8 spine treats as authoritative record it:

| Artifact | State before this proposal |
| --- | --- |
| Story 8.3 matrix, "FrontComposer UI primitives" row | `needs-additive-api`, *"Keep rollback path: … skip links … remain until each Story 8.9 slice proves parity"* |
| Story 8.3 matrix, Story 8.10 reconciliation table | Four rows (EventStore package/source, Commons HTTP, Builds); no FrontComposer row |
| Spine §7, I13 / I14 | I13 named `MainLayoutAccessibilityTests` without recording that it now asserts the shell slice |
| `sprint-status.yaml` | `8-9-ui-frontcomposer-and-fluent-consolidation: backlog`, unqualified |
| `deferred-work.md`, `8.9-frontcomposer-ui-consolidation` | `rollback` field amended with a "Delivered-slice carve-out" describing work already performed |

Spine I4 additionally requires that every consumed surface record the exact
root-declared gitlink SHA selected by the consumer. `7a337a21`, `003fd214`, and
`0dca9e9d` appear nowhere under `_bmad-output`.

## 2. Impact Analysis

**Invariants touched.** I3 (local rollback paths stay until the replacement has
parity evidence *and* proven rollback), I4 (no migration from an unidentified
dependency), I13 (WCAG 2.2 AA contracts), I16 (identity-stamped parity), I17
(deferral executors inherit the §4 gate), I18 (baseline surfaces may not be
deleted while the deletion relies on them).

**Parity evidence is weaker than it appears.** The 6/6 Playwright pass that
resolved the `playwright-shell-accessibility` blocker does not cover the
regression the slice introduced. Its `forced-colors and reduced-motion media are
observable` test focuses `.fc-skip-link`, an element with its own rules in
`fc-shell.css`, and never focuses a content control. Meanwhile `MainLayout.razor`
now renders no HTML element of its own, so Blazor emits no CSS-isolation scope
attribute and every `::deep` rule left in `MainLayout.razor.css` — the
`:focus-visible` outline and its `@media (forced-colors: active)` override — is
dead at runtime. The lane is green and the app-owned focus indicator is gone.

**Not viable: rollback of the slice.** Reverting restores the duplicate
skip-link ambiguity and the four Playwright failures, and discards authorized,
tested work (`Hexalith.Parties.UI.Tests` 327/327). Correct Course §4.2 rollback
is therefore not selected.

**Scope classification: moderate.** No PRD functional requirement is added or
changed. Story 8.9 is not completed; one slice of it is recorded as delivered
and the remaining G4 primitives stay deferred.

## 3. Recommended Path — Backfill and Reconcile

This proposal follows the precedent set by
`sprint-change-proposal-2026-08-02-story-8-6-eventstore-identity-authorization-backfill.md`,
which converted an in-session identity authorization for Story 8.6 into a
governed record without discarding the work.

**Authorized here:**

1. The FrontComposer shell slice (skip links + `role="main"` / `role="navigation"`
   landmarks) is recorded as a **delivered Story 8.9 / G4 slice**, consumed at
   root gitlink `7a337a21d4ba261bf27aeb3feedde47789f0160a`.
2. The Story 8.3 matrix gains a FrontComposer identity row in the Story 8.10
   reconciliation table and a delivered-slice annotation on the
   "FrontComposer UI primitives" row. That row's status stays
   `needs-additive-api`: five of six G4 work packages (A–E) remain undelivered,
   and only work package F (shell skip-link parity) is discharged.
3. `references/Hexalith.Memories` `003fd21488d60307cd932a3139f69319a25cea66` and
   `references/Hexalith.PolymorphicSerializations`
   `0dca9e9d3f8b2a20ba426b84fa575ab4e7b5562b` are recorded as retained
   identities. PolymorphicSerializations carries the StyleCop compatibility fix
   that cleared the 21-error Release build blocker.
4. Story 8.9 stays `backlog`. A delivered slice does not advance the story.
5. The `8.9-frontcomposer-ui-consolidation` deferral's `rollback` field is
   restored to a genuine rollback statement; the delivered slice is recorded in
   a separate `delivered_slices` field rather than inside the rollback clause.

**Explicitly NOT authorized:** deleting any further Parties UI primitive
(picker, freshness/status regions, download helpers, typed-name confirmation,
optimistic reconciliation, portal components), promoting the G4 row to
`available`, or marking Story 8.9 anything other than `backlog`.

## 4. Conditions

- The shell slice's parity evidence is stamped at FrontComposer
  `7a337a21d4ba261bf27aeb3feedde47789f0160a` and is invalid at any other
  identity (I16).
- The `authorized-owner-fixes-not-immutable` blocker **stays open**. The
  FrontComposer and PolymorphicSerializations fixes are now selected through
  superproject gitlinks, but no owner release or tag has been published for
  either; the blocker's exit proof still requires immutable owner receipts.
- The app-owned focus-visible and forced-colors regression introduced by the
  slice must be repaired before the slice is treated as I13 parity. A green
  Playwright receipt that never focuses a content control is not I13 evidence.

## 5. Approval

Authorized by Administrator on 2026-08-19, backfilling the in-session
authorization recorded in `_bmad-output/implementation-artifacts/tests/test-summary.md`
§"Authorized dependency and shell remediation — 2026-08-18".
