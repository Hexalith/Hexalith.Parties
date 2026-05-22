---
date: 2026-05-22
project: Hexalith.Parties
project_lead: Jérôme
trigger: Epic 3 retrospective action items B4, B6, B8
scope_classification: Moderate (process changes + new prep story)
status: approved-and-applied
related_artifacts:
  - _bmad-output/implementation-artifacts/epic-3-retro-2026-05-22.md
  - _bmad-output/process-notes/story-creation-lessons.md
  - .claude/skills/bmad-story-automator-review/checklist.md
  - _bmad-output/implementation-artifacts/9-8-solution-build-green-on-clean-clone.md
  - _bmad-output/implementation-artifacts/sprint-status.yaml
---

# Sprint Change Proposal — Epic 3 Retrospective Follow-Through (B4, B6, B8)

## 1. Issue Summary

Epic 3 retrospective (2026-05-22) surfaced three systemic patterns that need to be addressed before Epic 7 (Administration Console, in flight) and Epic 8 (Embeddable Party Picker, blocked) active work resumes:

- **B4 — Cross-submodule edits not atomic with downstream proofs.** Story 3.1's `PackedEventStoreContractsPackage_DoesNotLeakDaprOrInfrastructureDependencies` test depended on a `Dapr.Actors PrivateAssets="all"` edit that lived only in a `-dirty` `Hexalith.EventStore` submodule working tree at review time. A clean clone or CI build would have failed the test.
- **B6 — Architecture pivots silently rotted Epic 3 planning text.** Stories 3.3, 3.4, 3.5 each had to author an `Architecture Reconciliation` section because Story 12.5 retired `/api/v1/parties` in favor of the EventStore-fronted gateway but the epic text was never swept. The same pattern will recur for any future pivot.
- **B8 — Solution-level build broken throughout Epic 3.** `dotnet build .\Hexalith.Parties.slnx --configuration Release` failed across Stories 3.1, 3.2, 3.10 on out-of-scope blockers (CA2007, missing nested `Hexalith.Memories/Hexalith.Commons` submodule, submodule-inherited analyzer warnings promoted to errors). Each story routed around the broken build with focused project-level commands. CI cannot rely on this as a signal.

## 2. Impact Analysis

| Dimension | Impact |
|---|---|
| Epic Impact | Epic 9 (Kubernetes Deployment) grows from 4 to 5 stories; Story 9.8 added as prep gate before Epic 7/8 active work resumes |
| Story Impact | Story 3.10's `-p:TreatWarningsAsErrors=false` workaround should be removed once Story 9.8 lands |
| Artifact Conflicts | None — no PRD, architecture, or UX changes required |
| Process Impact | `bmad-story-automator-review` checklist gains submodule-cleanliness gate; story-creation lessons ledger gains L09 (submodule discipline) and L10 (pivot sweep) |
| Technical Impact | New Story 9.8 work: audit + fix solution-level build on clean clone; add CI gate |

## 3. Recommended Approach

**Direct Adjustment.** All three items are operational follow-throughs to documented retro findings. No MVP scope reduction or rollback needed. Effort scoped to:

- B4: one checklist line + one process-note entry (done).
- B6: one process-note entry (done).
- B8: one new prep story (Story 9.8) authored as `ready-for-dev`; sprint-status updated; Epic 7/8 active work gated on 9.8 reaching `done`.

Rationale: B4 and B6 are template/discipline edits that take effect on the next story-automator run. B8 is a real engineering prep gate that must complete before Epic 7/8 can use CI as a quality signal. Tracking B8 as a story (not a tracker entry) preserves the Dev Agent Record, review trail, and sprint-status integration that the rest of Epic 9 already follows.

## 4. Detailed Change Proposals

### Change 1: Submodule cleanliness gate (B4)

**Artifact:** `.claude/skills/bmad-story-automator-review/checklist.md`

**Type:** Addition (new checklist item)

**Before:**
```
- [ ] Code quality review performed on changed files
- [ ] Security review performed on changed files and dependencies
- [ ] Outcome decided (Approve/Changes Requested/Blocked)
```

**After:**
```
- [ ] Code quality review performed on changed files
- [ ] Security review performed on changed files and dependencies
- [ ] Submodule cleanliness verified (per `_bmad-output/process-notes/story-creation-lessons.md` L09): if the story's File List, Debug Log, or AC evidence references files inside a submodule, `git submodule status` shows no `-dirty` markers for that submodule; if dirty, the submodule edit is committed and the parent pointer bumped in the same change set, or the affected acceptance item is explicitly deferred to a cross-repo follow-up with a recorded blocker.
- [ ] Outcome decided (Approve/Changes Requested/Blocked)
```

**Rationale:** Discovered in Epic 3 retrospective (Story 3.1). Without this gate, story acceptance evidence that depends on submodule contents can pass locally and fail on clean clone / CI.

### Change 2: Lessons ledger entries L09 and L10 (B4 + B6)

**Artifact:** `_bmad-output/process-notes/story-creation-lessons.md`

**Type:** Addition (two new lesson entries after existing L08)

**Summary:**
- **L09 — Submodule Cleanliness for Cross-Submodule Acceptance Evidence.** Memorializes the B4 discipline with concrete triggers (packed-package proof, gateway behavior pinned to specific submodule commit, fitness tests locking submodule-owned surfaces) and the resolution path (commit + pointer bump in same change set, or explicit cross-repo deferral with recorded blocker).
- **L10 — Architecture Pivot Sweep for Downstream Epic Text.** Memorializes the B6 discipline: any story that retires/renames/replaces a public surface must include a sweep step (grep planning artifacts, update inline or tag `<!-- pivot-affected -->`, record sweep result in Completion Notes). Includes a one-time backfill instruction for pre-pivot Parties REST text.

**Rationale:** The lessons ledger is referenced by `bmad-create-story` story Dev Notes (existing references to L08), so new entries propagate to future story-creation flows without further skill changes.

### Change 3: New Story 9.8 — Solution build green on clean clone (B8)

**Artifact:** `_bmad-output/implementation-artifacts/9-8-solution-build-green-on-clean-clone.md`

**Type:** New story file, `Status: ready-for-dev`

**Summary:**

5 acceptance criteria covering: clean-clone build succeeds without warnings-as-errors overrides; opt-in projects do not break the default graph; submodule-inherited analyzer warnings do not promote to errors at the consumer; pre-existing analyzer debt closed; CI signal locks the new baseline. Owner field intentionally not assigned — `ready-for-dev` is the trigger for normal story-pickup flow. Non-goals explicit: do not globally disable `TreatWarningsAsErrors`, do not initialize nested submodules in the default path, do not retarget any project.

**Sprint-status update:**
- Epic 9 story count comment updated `4 stories → 5 stories`
- Epic 9 retro gating comment updated to require `9.3 done AND 9.4 done AND 9.8 done`
- New entry: `9-8-solution-build-green-on-clean-clone: ready-for-dev`
- Added comment: `Epic 7 / Epic 8 active work is gated on 9.8 done so CI can serve as a quality signal`

**Rationale:** Carving as a story (rather than a tracker entry) preserves the Dev Agent Record discipline applied to every other Epic 9 story (compare 9.4's blocked-but-tracked artifact). Placing under Epic 9 (deployment / infrastructure) matches the work category — solution build is a deployment-adjacent quality signal — and keeps the new gating relationship visible in the same epic that already houses 9.4's blocked status.

## 5. Implementation Handoff

**Scope classification:** Moderate (process changes + new prep story).

**B4 + B6 (applied):**
- Owners: Amelia (Developer) for L09 / submodule check enforcement; John (PM) + Winston (Architect) for L10 / pivot sweep when next pivot story is authored.
- Success criterion for B4: first applicable story in Epic 7 or Epic 8 work passes/fails the submodule gate deterministically.
- Success criterion for B6: next pivot story (whenever authored) carries the sweep step in Completion Notes and tags affected epic sections.

**B8 (handed off to dev pickup):**
- Owner: open (`ready-for-dev`); recommended pickup by Charlie (Senior Dev) per Epic 3 retro action assignment.
- Success criterion: `dotnet build .\Hexalith.Parties.slnx --configuration Release` exits zero on a fresh clone with `git submodule update --init` (root-level only), no `-p:TreatWarningsAsErrors=false` overrides remain in scripts/docs/stories, and a CI check gates regression.
- Gating relationship: Epic 7 Story 7.7 (currently blocked on EventStore-fronted client/gateway contract acceptance) and all Epic 8 stories (currently blocked on the same dependency) gain Story 9.8 as a *second* gate before resumption — CI signal is the prerequisite for relying on integration tests.

## 6. Status

Approved by Jérôme (project lead) via Correct Course workflow batch mode on 2026-05-22. All three changes applied in this transaction.
