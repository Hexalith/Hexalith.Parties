---
title: Sprint Change Proposal — Root Gitlink Release-Candidate Gate
date: 2026-07-07
author: Administrator
workflow: bmad-correct-course
mode: incremental
scope_classification: minor-to-moderate
trigger: 5 uncommitted root gitlink drifts with no release-candidate gate to catch them before tagging
status: approved
related:
  - scripts/gitlink-rc-gate.sh
  - .gitlink-signoff.tsv
  - RELEASE-CHECKLIST.md
  - .github/workflows/rc-gate.yml
  - _bmad-output/planning-artifacts/architecture.md
---

# Sprint Change Proposal — Root Gitlink Release-Candidate Gate

## 1. Issue Summary

**Problem.** The repository has **no gate that forces each root submodule pointer
(gitlink) to be a conscious choice before a release tag is cut.** Submodule
pointers drift routinely during development (a local build or `git submodule
update` moves a pointer), and nothing stops an accidental — or simply
un-reviewed — pointer from being carried into a tagged release.

**Discovery.** Observed directly in the working tree: `git submodule status`
shows **5 drifted (`+`) root gitlinks**, all uncommitted, alongside **zero git
tags** in the repository — i.e. tagging discipline is being established now, which
is the correct moment to install the gate.

**Evidence (all 5 drifts are clean FORWARD moves — checked-out is a descendant of
the recorded commit; nothing diverged or behind):**

| Submodule | Recorded (root index) | → Checked-out | Ahead | Lands on |
| --- | --- | --- | --- | --- |
| Hexalith.Commons | v2.26.0-3 (`275edc0`) | `20048e9` | +2 | ✅ clean tag **v2.27.0** |
| Hexalith.PolymorphicSerializations | v1.16.1-1 (`89c8409`) | `ca108649` | +2 | ✅ clean tag **v1.16.2** |
| Hexalith.Builds | v4.16.3-26 (`6fcd894`) | `94989b0` | +2 | ⚠️ untagged (v4.16.3-**28**) |
| Hexalith.Tenants | v2.3.0-6 (`d923393`) | `8d1a5bd` | +3 | ⚠️ untagged (v2.3.0-**9**) |
| Hexalith.EventStore | v3.43.0-9 (`c0fe028`) | `963402c5` | +7 | ⚠️ untagged (v3.43.0-**16**) |

3 submodules are in sync (AI.Tools, FrontComposer, Memories). All 8 are
Hexalith-org owned.

## 2. Impact Analysis

- **Epic impact:** None. No product epic is affected; this is additive
  release-governance tooling. Epics 1–8 scope untouched.
- **Story impact:** None. No story is created, modified, or reordered.
- **PRD / MVP:** No FR or MVP-scope impact.
- **UI/UX:** No surface.
- **Architecture:** One additive **Process Patterns** invariant registering the
  gate as the release-time source of truth (does not alter any existing
  UI-tier rule).
- **Technical / CI-CD:** New gate script, sign-off ledger, RC checklist, and a
  dedicated release-scoped CI workflow. Mirrors the existing
  `scripts/check-no-warning-override.sh` build-gate pattern already wired into
  `test.yml`.

**Change Analysis Checklist result:** Sections 2 (Epic), 3.1 (PRD), 3.3 (UI/UX)
→ **N/A**. Sections 3.2 (Architecture), 3.4 (CI/CD, scripts, docs) →
**Action-needed** (addressed by P1–P5 below). No halt conditions triggered.

## 3. Recommended Approach

**Selected path: Option 1 — Direct Adjustment.** Effort **Low**, Risk **Low**.

- Option 2 (Rollback) — **not viable**: nothing to roll back.
- Option 3 (MVP review) — **not viable**: no scope change.

**Rationale.** The gap is a missing process control, not a defect in delivered
work. It is closed by adding tooling + one governance invariant, with no rework
and no timeline impact. The gate is **fail-closed**: an unratified or drifted
pointer blocks the tag by default.

**Gate invariant.** Before a release tag, every root gitlink that has drifted
(working tree) or been bumped (vs the release base) must be **either**
owner-validated in the ledger **or** deliberately reset to the recorded commit.
Enforced on two surfaces:

| Surface | Command | Catches |
| --- | --- | --- |
| Local, pre-tag (release engineer) | `scripts/gitlink-rc-gate.sh --worktree` | uncommitted drift (`+`/`U`), uninitialised (`-`) |
| CI, on release candidate | `scripts/gitlink-rc-gate.sh --diff <base>` | a gitlink bump committed into the RC without a signed-off ledger entry |

## 4. Detailed Change Proposals

### P1 — `scripts/gitlink-rc-gate.sh` (NEW)

The gate. Two modes (`--worktree`, `--diff <base-ref>`). Reads the **root**
`.gitmodules` only — never recurses into nested submodules (per repo submodule
policy). No external dependencies beyond `git`/`awk`. A `validated-advance`
ledger entry is honoured only when its `owner` field is a real handle (empty or
`<PLACEHOLDER>` owners are rejected — fail-closed).

**Validated behaviour (dry-run against the live tree):**

- `--worktree`, seeded ledger with placeholder owners → **FAIL**, all 5 drifts
  reported UNVALIDATED (placeholder owners correctly rejected).
- `--worktree`, ledger owners ratified → the 2 clean-tag pointers report
  `DRIFT ok — validated-advance`; the 3 ahead-of-tag pointers still FAIL.
- `--diff HEAD~1` → correctly detected the real gitlink bumps committed in
  `24e9f41` (Memories, Tenants), proving CI-mode bump detection.

### P2 — `.gitlink-signoff.tsv` (NEW) — sign-off ledger

Pipe-delimited, one authorised pointer per line:
`<path>|<sha>|<disposition>|<ref>|<owner>|<date>` where `disposition ∈
{validated-advance, reset}`. Seeded with the recommended dispositions (see §P6):
the 2 clean-tag drifts active with `<OWNER-PENDING>` (fail-closed until a real
owner handle replaces the placeholder), the 3 ahead-of-tag drifts commented out
as explicit DECIDE items.

### P3 — `RELEASE-CHECKLIST.md` (NEW) — manual gate item

Blocking release-checklist section: run `--worktree` clean; every `+` gitlink is
owner-validated (committed) or reset; no unexplained `+`/`U`/`-`; no placeholder
owner/date; CI `Release Candidate Gate` green.

### P4 — `.github/workflows/rc-gate.yml` (NEW) — CI enforcement

Dedicated release-scoped workflow (kept out of the per-PR `test.yml`). Triggers:
`rc/**` + `release/**` branch pushes, `v*` tag pushes, `release-candidate`-labelled
PRs to `main`, and `workflow_dispatch`. Resolves the diff base (PR base → dispatch
input → previous tag → `origin/main` fallback) and runs
`scripts/gitlink-rc-gate.sh --diff <base>`. Reads gitlink SHAs from the tree, so it
runs with `submodules: false` (fast, no submodule token needed).

### P5 — `architecture.md` Process Patterns (EDIT)

Added a pinned, release-gated **Root gitlink governance** bullet registering the
gate + ledger as the release-time source of truth. No existing rule altered.

### P6 — Per-pointer disposition for the 5 current drifts (owner ratifies)

| # | Pointer | Drift | Recommendation |
| --- | --- | --- | --- |
| 1 | Commons → **v2.27.0** | +2, clean tag | **validated-advance** — forward to a clean release tag; low risk |
| 2 | PolymorphicSerializations → **v1.16.2** | +2, clean tag | **validated-advance** — clean release tag; low risk |
| 3 | Builds → v4.16.3-**28** | +2, untagged | **DECIDE** — advance only if the unreleased Builds commits ship (ideally tag first), else **reset** |
| 4 | Tenants → v2.3.0-**9** | +3, untagged | **DECIDE** — advance if unreleased Tenants commits ship, else **reset** |
| 5 | EventStore → v3.43.0-**16** | +7, untagged | **DECIDE** — advance if unreleased EventStore commits ship (largest drift), else **reset** |

*Context: the root already tracks untagged submodule commits (e.g. recorded
EventStore is itself v3.43.0-9), so advancing is consistent with current
practice — but the gate deliberately forces a conscious sign-off regardless.*

## 5. Implementation Handoff

**Scope classification: Minor→Moderate.** Additive infra + one governance-doc
edit; no backlog reorganisation, no replan.

**Status — implemented in this session (uncommitted, pending review):**
P1–P5 created/edited and verified. The following remain as owner/human actions:

- **Release owner (Hexalith):** ratify §P6 — replace `<OWNER-PENDING>` in
  `.gitlink-signoff.tsv` for Commons + PolymorphicSerializations with a real
  handle + date; decide validate-advance-vs-reset for Builds, Tenants,
  EventStore and fill (or reset) accordingly.
- **Release engineer / DEV:** when validating, commit the corresponding gitlink
  bump into the release branch (`git add references/<Submodule>`); when
  resetting, `git submodule update --checkout references/<Submodule>`.
- **DEV / repo admin:** add a `release-candidate` PR label and (optionally) make
  the `Root gitlink RC gate` a required status check on the release path.

**Success criteria.** `bash scripts/gitlink-rc-gate.sh --worktree` exits 0 and
the CI gate is green on a release candidate with no placeholder owners remaining
— i.e. every drifted pointer is owner-validated or deliberately reset before the
tag.

**Not done (by design):** no commit/push, no submodule pointers advanced/reset,
no release tag cut — these are owner decisions per §P6.
