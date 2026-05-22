# Story Creation Lessons

This ledger was bootstrapped automatically by `jobs/preflight-predev-hardening.py`
because this repository had no existing story-creation lessons file.

Use this file to record durable lessons for recurring BMAD story creation,
party-mode review, advanced elicitation, and code-review automation.

## L08 - Party Review vs. Elicitation

- Party-mode review is the cross-role critique and triage pass before
  development; it should produce dated trace evidence when completed.
- Advanced elicitation is a separate hardening pass after a completed
  party-mode trace exists; a recommendation to run elicitation is not itself
  completed elicitation evidence.

## L09 - Submodule Cleanliness for Cross-Submodule Acceptance Evidence

Recorded by Epic 3 retrospective (action B4, 2026-05-22) after Story 3.1's
`PackedEventStoreContractsPackage_DoesNotLeakDaprOrInfrastructureDependencies`
test depended on a `Dapr.Actors PrivateAssets="all"` edit that lived only in a
`-dirty` `Hexalith.EventStore` submodule working tree at review time. A clean
clone or CI build would have reproduced a different `.nuspec` and failed the
test.

- When a story's File List, Debug Log, or AC evidence references files inside
  a submodule, the story's `Senior Developer Review (AI)` must verify
  `git submodule status` reports no `-dirty` markers for the referenced
  submodule before approving.
- If the submodule is dirty, the review must require committing inside the
  submodule and bumping the parent pointer in the same change set, or
  explicitly defer the affected acceptance item to a follow-up cross-repo
  story with a recorded blocker.
- Packed-package proof, gateway behavior pinned to a specific EventStore
  commit, and architectural fitness tests that lock submodule-owned surfaces
  are the common triggers for this check.

## L10 - Architecture Pivot Sweep for Downstream Epic Text

Recorded by Epic 3 retrospective (action B6, 2026-05-22) after three Epic 3
stories (3.3, 3.4, 3.5) each had to author an `Architecture Reconciliation`
section because Story 12.5 retired `/api/v1/parties` in favor of the
EventStore-fronted gateway but the epic text was never swept. Future
story-creation runs against un-swept epic text repeat the tax.

- Any story that retires, renames, or replaces a public surface (REST path,
  client API, event shape, package boundary) is a "pivot story." Pivot
  stories must include a sweep step in their Completion Notes:
  1. Grep all `_bmad-output/planning-artifacts/*epic*.md` and
     `_bmad-output/planning-artifacts/*prd*.md` for the retired surface.
  2. For each hit, either update the inline text to the new surface, or tag
     the affected section with `<!-- pivot-affected: Story {{pivot_id}} on
     {{date}}; reconcile before authoring stories from this section -->`.
  3. Record the sweep result (files touched, sections tagged) in the pivot
     story's Completion Notes.
- Story-creation flows reading a `pivot-affected` tag must treat the section
  as authoritative-but-stale and add an `## Architecture Reconciliation`
  section to the generated story.
- One-time backfill: the next time anyone touches an Epic 3-adjacent or
  Epic 12-adjacent epic section, sweep for the pre-pivot Parties REST text
  (`api/v1/parties`, direct Parties REST routes, `PartiesController`) and
  reconcile.
