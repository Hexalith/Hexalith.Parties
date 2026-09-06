---
title: Sprint Change Proposal — Freeze Sprint-Status Keys and Ratify `blocked`
date: 2026-09-06
author: Administrator
workflow: bmad-correct-course
mode: batch
scope_classification: moderate
status: approved
approval_required: true
approval: approved
approved_by: Administrator
approved: 2026-09-06T16:34:00+02:00
trigger: >
  bmad-sprint-planning on 2026-09-06 passed readiness, then a dry-run of
  sprint_plan.py generate against epics.md showed that a real write would
  rename 19 existing story keys, drop Parties' `blocked` vocabulary, and
  strip development_status comments. Story packets 8.7/8.8 still describe
  Story 8.6 as blocked, and Story 8.9 is recorded as both backlog and blocked.
baseline:
  parties_commit: d5e5d361481be234d1fded40711f69b0a1f75cfe
  parties_branch: main
related:
  - _bmad-output/implementation-artifacts/sprint-status.yaml
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/planning-artifacts/parties-ui-prd.md
  - _bmad-output/implementation-artifacts/8-7-data-protection-extraction.md
  - _bmad-output/implementation-artifacts/8-8-client-mcp-apphost-build-and-deploy-cleanup.md
  - _bmad-output/implementation-artifacts/spec-8-9-ui-frontcomposer-and-fluent-consolidation.md
  - _bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-08-19-story-8-10-frontcomposer-shell-slice-backfill.md
  - _bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md
  - _bmad-output/project-context.md
---

# Sprint Change Proposal — Freeze Sprint-Status Keys and Ratify `blocked`

## 1. Issue Summary

Sprint planning on 2026-09-06 judged Epic 8 **implementable as recorded**
(gate PASS). The refresh path then failed safely: `sprint_plan.py generate
--dry-run` was run and **no write** was performed.

The stock generator cannot round-trip this repository's tracker.

1. **Story keys are frozen in practice but not in policy.** Keys were minted
   from titles, then titles kept evolving. The parser also truncates slugs at
   60 characters and treats backticks as word characters (`party_id` vs
   `party-id`). A regenerate therefore reports 19 `new_entries` and 19
   `dropped_orphans` that are the same 19 stories. The existing keys match
   story filenames; the generated keys would not.
2. **`blocked` is a Parties-legal story status the stock script rejects.**
   `sprint-status.yaml` already defines it: context exists, an explicit
   prerequisite prevents implementation. Stories 8.7, 8.8, and 8.9 use it.
   `STORY_RANK` in `sprint_plan.py` has no `blocked`, so generate would
   replace 8.7 with `ready-for-dev` and 8.9 with `backlog`. 8.8 would lose
   `blocked` through the key rename (`…-deploy-cleanup` →
   `…-runtime-boundary-cleanup`). `--set` cannot restore `blocked`.
3. **A real write rebuilds `development_status`.** Per-key history comments
   would be dropped even if statuses survived.
4. **Sequence and status prose drifted after Story 8.6 closed.** Story 8.7 AC2
   and Story 8.8 AC1 still treat 8.6 as blocked. The 8.10 spec frozen Intent
   and an 8.10 sprint-status comment still call 8.9 `backlog`, while the
   tracked value is `blocked`. `spec-8-9` frontmatter says `in-progress`.

This is tracking and packet hygiene. It is not a product-requirement change
and it does not reopen Epics 1–7.

### Evidence (2026-09-06)

Command:

```bash
uv run .agents/skills/bmad-sprint-planning/scripts/sprint_plan.py generate \
  --epic-file _bmad-output/planning-artifacts/epics.md \
  --status-file _bmad-output/implementation-artifacts/sprint-status.yaml \
  --stories-dir _bmad-output/implementation-artifacts \
  --project parties --date "09-06-2026 14:43" --dry-run
```

Result: `ok: true`, `in_sync: false`, `epics: 8`, `stories: 58`,
`changed: 2`, `illegal: [{8-7-data-protection-extraction: blocked},
{8-9-ui-frontcomposer-and-fluent-consolidation: blocked}]`, plus the 19/19
key-drift sets listed in the sprint-planning session. HEAD at discovery:
`d5e5d361481be234d1fded40711f69b0a1f75cfe` on `main`.

## 2. Impact Analysis

### Epic impact

Epic 8 can still complete as planned. No epic is added, removed, or
resequenced. Epics 1–7 stay `done`. The `8.6 → 8.7 → 8.8 → 8.9 → 8.10`
order in the architecture spine is unchanged: 8.6 is done, 8.7–8.9 remain
hard-gated by Story 8.3 owner rows, 8.10 stays in review.

Story 8.9's *product* scope in `epics.md` (full UI consolidation) is
unchanged. The executable next slice remains G4-A (`spec-8-9`, picker
adapter). G4-B–E stay on the `8.9-frontcomposer-ui-consolidation`
deferral. G4-F (shell) stays a delivered slice, not story completion.

### Artifact conflicts

| Artifact | Impact |
| --- | --- |
| PRD `parties-ui-prd.md` | None. Zero new FRs (I15). MVP already delivered by Epics 1–5. |
| Architecture / UX spines | None. No component, API, or experience change. |
| `epics.md` | Add a key-freeze note and a Story 8.9 slice pointer. Do not retitle stories to chase the 60-character slugger. |
| `sprint-status.yaml` | Keep every existing key and the three `blocked` values. Fix the stale “8.9 remains backlog” comment. Do **not** run stock `generate`. |
| Story 8.7 / 8.8 packets | Reconcile AC/task text that still says 8.6 is blocked. Remaining gates: G5 for 8.7; G5 plus 8.7 plus G6/G7/G8/G11 for 8.8. |
| `spec-8-9` | Frontmatter `in-progress` is not the workflow status. Canonical status is `blocked` in sprint-status. |
| `spec-8-10` frozen Intent | One sentence still says “8.9 is backlog”. Changing it needs human renegotiation of that frozen block. |
| `.agents/skills/bmad-sprint-planning/scripts/sprint_plan.py` | **Do not patch.** Skill files are overwritten on install. |
| `project-context.md` | Add a Development Workflow rule so agents do not regenerate keys or treat `blocked` as illegal. |

### Technical impact

No production code, package, submodule, CI, or deploy change. Risk of *not*
doing this: the next sprint-planning or fix-sprint-status run silently
unblocks 8.7/8.9 and detaches 19 story files from their tracker keys.

## 3. Recommended Approach

**Selected: Direct Adjustment (checklist §4.1).**
Rollback (§4.2) is not viable: it would discard years of status comments and
the `blocked` holds. MVP review (§4.3) is not applicable.

Effort: **Low**. Risk: **Low**. Timeline: documentation/tracker only; Story
8.10 review continues.

**Rationale.** Parties already uses two tracker extensions the stock BMad
generator does not know: long frozen keys and `blocked`. The durable fix is
to record those extensions as policy and align the drifted packets, not to
force the file back onto stock vocabulary (which would call 8.7
ready-for-dev while G5 is still `needs-additive-api`).

This proposal **supersedes one clause** of
`sprint-change-proposal-2026-08-19-story-8-10-frontcomposer-shell-slice-backfill.md`:
“Story 8.9 stays `backlog`” / “must stay backlog”. A delivered G4-F slice
does not complete 8.9, but the story is no longer “epic-only”: it has a spec,
a halt gate, and undelivered G4-A–E work. That is `blocked`, not `backlog`.
Every other 2026-08-19 authorization (slice ≠ completion, G4 row stays
`needs-additive-api`, retain remaining UI primitives) still holds.

**Out of scope unless requested later:** a Parties-owned `_bmad/scripts`
wrapper that adds `blocked` to the rank table and merges by story-id instead
of regenerated slugs. Policy first; a wrapper is optional follow-up, not
required to keep the tracker honest.

## 4. Detailed Change Proposals

### 4.1 Tracker — freeze keys; keep `blocked`

**File:** `_bmad-output/implementation-artifacts/sprint-status.yaml`

**OLD:** Informal practice plus a header that already lists `blocked`, with a
stale 8.10 comment:

```yaml
# Stories 8.7/8.8 remain blocked and Story 8.9 remains backlog.
8-10-final-readiness-documentation-and-retirement-gate: review
```

**NEW:**

- Treat every existing `development_status` key as frozen. Canonical key =
  current kebab-case id = story filename stem (`8-8-client-mcp-apphost-build-and-deploy-cleanup`,
  not a newly slugged title).
- Keep `blocked` on:
  - `8-7-data-protection-extraction`
  - `8-8-client-mcp-apphost-build-and-deploy-cleanup`
  - `8-9-ui-frontcomposer-and-fluent-consolidation`
- Replace the stale comment with: Stories 8.7/8.8/8.9 remain `blocked`;
  Story 8.10 remains `review`.
- After approval, set `last_updated` to the approval stamp. Do not run
  `sprint_plan.py generate` (with or without `--fresh`) against this file.

**Rationale:** Dry-run proved generate cannot preserve this file. Manual
hygiene is the only safe refresh until a Parties-owned wrapper exists.

### 4.2 Epics — key-freeze note and Story 8.9 slice pointer

**File:** `_bmad-output/planning-artifacts/epics.md`

**OLD:** Story headings are the implied key source. Story 8.9 has no pointer
to the G4-A spec vs G4-F delivered slice vs G4-B–E deferral.

**NEW:** After the Overview (or Epic 8 sequencing), add a short Tracking
rule:

- Story keys in `sprint-status.yaml` are frozen. Title edits in this file
  must not regenerate keys. New stories get a key at creation time from the
  title, then that key is frozen.
- Under Story 8.9, add: next executable slice is G4-A
  (`spec-8-9-ui-frontcomposer-and-fluent-consolidation.md`); G4-F was
  delivered under the 2026-08-19 SCP; G4-B–E remain deferred; workflow
  status is `blocked` until the Story 8.3 G4 row is consumable.

**Rationale:** Stops the next generate from treating title punctuation as a
rename. Clarifies 8.9 without shrinking the epic-level Then clause.

### 4.3 Story 8.7 — 8.6 is done; G5 remains the halt

**File:** `_bmad-output/implementation-artifacts/8-7-data-protection-extraction.md`

**OLD (AC2):**

> Given the approved Epic 8 sequence remains `8.6 -> 8.7` while Story 8.6
> is blocked, when owner-side G5 work or Story 8.7 preparation proceeds in
> parallel, then no Parties production migration or deletion starts until
> 8.6 is completed or the sequence is explicitly changed…

**NEW (AC2):**

> Given the approved Epic 8 sequence remains `8.6 -> 8.7` and Story 8.6 is
> `done`, when owner-side G5 work or Story 8.7 preparation proceeds, then
> no Parties production migration or deletion starts until the Story 8.3
> G5 row is consumable (`available` with named approval, exact identity,
> producer/consumer parity, and rollback). Completing 8.6 does not waive
> G5.

Also mark the task “Confirm Story 8.6 is `blocked`” as historical (done at
creation) and the task “Resolve the `8.6 -> 8.7` sequence by completing
8.6…” as **done** (8.6 closed 2026-08-17). Leave the G5 start-gate tasks
open. Story status stays `blocked`.

Do **not** edit `spec-8-7` frozen Intent. That spec already gates on G5
`available` and forbids production changes while G5 is closed. Its
frontmatter `in-progress` is a spec-session marker, not a workflow unblock.

**Rationale:** A developer reading AC2 today would halt on a predecessor
that is already done and miss that G5 is the live gate.

### 4.4 Story 8.8 — 8.6 is done; 8.7 plus owner rows remain

**File:** `_bmad-output/implementation-artifacts/8-8-client-mcp-apphost-build-and-deploy-cleanup.md`

**OLD (AC1):**

> Given Stories 8.6 and 8.7 remain blocked in the authoritative
> `8.6 -> 8.7 -> 8.8` sequence…

**NEW (AC1):**

> Given Story 8.6 is `done` and Story 8.7 remains `blocked` in the
> authoritative `8.6 -> 8.7 -> 8.8` sequence, when Story 8.8 is prepared
> or owner-side prerequisite work proceeds, then no Parties production
> migration or deletion begins until 8.7 completes (or an approved
> artifact changes the sequence) **and** each 8.8 slice’s Story 8.3 row is
> consumable.

Reconcile the “Current Blockers” / debug-log lines that still say both
predecessors are blocked. Keep `story_key`:
`8-8-client-mcp-apphost-build-and-deploy-cleanup` even though the epic
title now says “runtime-boundary cleanup”.

**Rationale:** Same as 4.3. Key freeze prevents the generate rename.

### 4.5 Story 8.9 — canonical status is `blocked`

**File:** `_bmad-output/implementation-artifacts/spec-8-9-ui-frontcomposer-and-fluent-consolidation.md`

**OLD:** YAML `status: 'in-progress'`.

**NEW:** YAML `status: 'blocked'`. Add a one-line note outside the frozen
block: workflow status lives in `sprint-status.yaml`; this spec is the
G4-A slice, not a license to start production edits.

Do not edit the frozen Intent/Boundaries. They already fail closed until
G4 is consumable.

**Rationale:** Aligns the spec session marker with the halt. Does not
mark 8.9 done. Does not expand G4-A into G4-B–E.

### 4.6 Story 8.10 frozen Intent — 8.9 wording (needs renegotiation)

**File:** `_bmad-output/implementation-artifacts/spec-8-10-final-readiness-documentation-and-retirement-gate.md`

The frozen Problem sentence still says “8.9 is backlog”. Proposed replacement
inside the frozen block, **only if Administrator renegotiates that
sentence**:

**OLD:** `Epic 8 cannot close while 8.7/8.8 are blocked, 8.9 is backlog, …`

**NEW:** `Epic 8 cannot close while 8.7/8.8/8.9 are blocked, …`

If renegotiation is declined, add an amendment **below** the frozen block
instead of touching it.

**Rationale:** Frozen blocks are human-owned. The SCP records the desired
alignment and does not silently edit them.

### 4.7 Project context — agent workflow rule

**File:** `_bmad-output/project-context.md`  
Section: Development Workflow Rules

**NEW bullet:**

- **Sprint tracking keys and `blocked` are frozen.** Canonical story ids
  are the existing keys in
  `_bmad-output/implementation-artifacts/sprint-status.yaml` (they match
  story filename stems). Do not run
  `.agents/skills/bmad-sprint-planning/scripts/sprint_plan.py generate`
  against that file: stock BMad rejects `blocked` and re-slugs titles,
  which would detach story files and unblock G5/G4-gated work. Refresh
  status by editing the YAML in place (or a future Parties-owned wrapper).
  `blocked` means context exists and an explicit prerequisite prevents
  implementation; it is not `backlog` and not `ready-for-dev`.

**Rationale:** This is how agents actually destroy the tracker. PRD/UX
need no change.

### 4.8 Explicit non-changes

- No PRD FR/NFR edits.
- No architecture spine invariant edits.
- No UX spine or mockup edits.
- No production source, gitlink, or package-pin edits.
- No patch to `sprint_plan.py` (overwritten on skill update).
- No new epic or story (8.14 wrapper deferred unless requested).
- Story 8.10 stays `review`. Stories 8.7–8.9 stay `blocked`.

## 5. Implementation Handoff

**Scope: Moderate** — backlog/tracker reorganization, not a product replan.
No PM/Architect epic rewrite.

| Role | Responsibility |
| --- | --- |
| Developer | Apply 4.1–4.5 and 4.7 after approval. Touch 4.6 only after an explicit frozen-block renegotiation. |
| Product Owner | Confirm 8.9 `blocked` supersedes the 2026-08-19 “stay backlog” clause; confirm no PRD/MVP impact. |
| Architect | No spine change. Sequence `8.6 → 8.7 → 8.8 → 8.9 → 8.10` unchanged. |

**Success criteria**

1. `sprint-status.yaml` still has the pre-proposal keys; 8.7/8.8/8.9 are
   `blocked`; 8.10 is `review`; the “8.9 remains backlog” comment is gone.
2. Story 8.7 AC2 and Story 8.8 AC1 no longer claim 8.6 is blocked.
3. `epics.md` and `project-context.md` state the key-freeze / no-generate
   rule.
4. A later `sprint_plan.py generate --dry-run` may still report drift; that
   is expected and **must not** be “fixed” by writing the generator output.
5. I15: no PRD functional-requirement artifact changes.

**Next after implementation:** resume Story 8.10 review. Do not start 8.7–8.9.

## Checklist record (Correct Course)

### §1 Trigger and context

- [x] 1.1 Trigger: Story 8.10 in review plus 2026-09-06 sprint-planning refresh
- [x] 1.2 Type: planning-tool limitation + tracker/packet drift (not a new FR)
- [x] 1.3 Evidence: generate `--dry-run` JSON as quoted above

### §2 Epic impact

- [x] 2.1 Epic 8 still completable as planned
- [x] 2.2 No epic add/remove/redefine
- [x] 2.3 Epics 1–7 unaffected except key freeze
- [x] 2.4 No obsolete or new epics
- [x] 2.5 No resequence

### §3 Artifact conflicts

- [x] 3.1 PRD: no conflict; MVP unchanged
- [x] 3.2 Architecture: no component/pattern change
- [x] 3.3 UX: N/A
- [x] 3.4 Tracker, story packets, project-context; do not patch the skill script

### §4 Path forward

- [x] 4.1 Direct Adjustment: Viable — Low effort, Low risk
- [x] 4.2 Rollback: Not viable
- [x] 4.3 MVP Review: Not viable / N/A
- [x] 4.4 Selected: Option 1 Direct Adjustment

### §5 Proposal components

- [x] 5.1–5.5 captured in sections 1–5 of this document

### §6 Final review

- [x] 6.1–6.3 Administrator approved 2026-09-06T16:34:00+02:00
- [x] 6.4 sprint-status comment + `last_updated` updated; keys and `blocked` values unchanged
- [x] 6.5 handoff: Developer applied 4.1, 4.3–4.7; 4.6 frozen sentence renegotiated as approved

**Implementation note (4.2):** `epics.md` was **not** edited. `EpicEightClosureFitnessTests.EpicEightAddsNoPrdFunctionalRequirement` fails closed on any `git diff` of that file versus baseline `37f4ec82`. The key-freeze rule and Story 8.9 slice pointer live in `sprint-status.yaml` and `project-context.md` instead.
