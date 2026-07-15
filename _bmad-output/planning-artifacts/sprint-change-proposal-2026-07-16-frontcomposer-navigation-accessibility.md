---
project: parties
date: 2026-07-16
workflow: bmad-correct-course
mode: incremental
status: implemented
change_scope: minor
approval: approved
---

# Sprint Change Proposal — FrontComposer Navigation Accessibility Closure

## 1. Issue Summary

Epic 7 final readiness recorded one failing UI test:
`MainLayout_exposes_named_navigation_and_content_landmarks`. The test expected the
obsolete FrontComposer selectors `fc-navigation-full` or `fc-collapsed-rail`, while
the selected FrontComposer surface exposes a single `fc-navigation-rail` landmark.
The Epic 7 retrospective therefore carried an open action to resolve the
accessibility blocker or validate/reset the FrontComposer pointer.

Assessment evidence separated the accessibility contract from the pointer hygiene
problem:

- The Parties test now asserts `fc-navigation-rail` and passes against the current
  FrontComposer checkout: 1 test run, 1 passed, 0 failed.
- The navigation root still exposes `role="navigation"` and
  `aria-label="Application navigation"`.
- At assessment time, the root repository pinned FrontComposer at
  `4e2e1af5225143aeac2e6ff6cf7517276cb1c242`, but the working checkout is at
  `32db5c3460a4aa0ae6382ae9db36fa42e512ffd3`.
- The relevant `FrontComposerNavigation.razor` markup is identical at those two
  commits.
- The later checked-out pointer has no owner validation in `.gitlink-signoff.tsv`.
  The established release-candidate rule therefore requires either an owner-
  validated advance or a deliberate reset to the recorded root pointer.

The remaining blocker was not an accessibility design defect. It was an
unvalidated FrontComposer checkout drift that prevented release evidence from
being tied to the root-selected dependency graph.

During the approval-to-execution interval, concurrent root commit
`cab674b971cbf9552dc3811d3253fdbf4c91333e` advanced the recorded FrontComposer
gitlink to `32db5c3460a4aa0ae6382ae9db36fa42e512ffd3`. The checkout was therefore
already aligned with the new root-selected dependency graph when implementation
started. The approved validate-or-reset outcome proceeded through validation; no
submodule reset was necessary.

## 2. Impact Analysis

### Epic and Story Impact

- Epic 7 remains completed maintenance scope.
- No story, acceptance criterion, dependency, sequence, priority, or product scope
  changes.
- Epic 8 sequencing is unchanged; it receives a clean, root-reproducible UI
  accessibility baseline.
- No new epic or story is required.

### Artifact Conflicts

- `sprint-status.yaml` reported the navigation action as `open`; it is closed only
  after the exact test passed at the root-pinned pointer.
- The dated Epic 7 final-readiness report and retrospective accurately record the
  state observed at their assessment dates and must remain unchanged.
- The PRD, epics, architecture, UX specifications, and accessibility review already
  express the intended navigation semantics and require no edit.
- `.gitlink-signoff.tsv` requires no edit because the working-tree gate classifies
  the concurrently committed pointer as clean rather than drifted.

### Technical Impact

No FrontComposer source, Parties source, public contract, deployment artifact, or
runtime behavior was changed by this workflow. The concurrent root commit aligned
the recorded gitlink with the existing checkout; this workflow validated that
root-selected surface.

Nested FrontComposer submodules must remain uninitialized.

## 3. Recommended Approach

The approved **Direct Adjustment** was:

1. Align only `references/Hexalith.FrontComposer` with the root-recorded gitlink,
   initially `4e2e1af5225143aeac2e6ff6cf7517276cb1c242`.
2. Build the Parties UI test project in source/project-reference mode.
3. Run the exact navigation landmark test.
4. Confirm FrontComposer no longer appears as root gitlink drift. The broader gate
   may remain red for unrelated, pre-existing submodule drifts; those are outside
   this proposal.
5. If and only if the test passes, mark the Epic 7 carry-forward action `done` with
   the pointer and test evidence.

- Effort: Low
- Risk: Low
- Timeline impact: Negligible
- Rollback: Restore the prior FrontComposer working checkout only if validation at
  the root-pinned commit exposes a regression; leave the action open and escalate
  for owner validation instead.
- MVP impact: None

At assessment time, advancing the root gitlink to the later checkout was not
recommended because no required navigation behavior differed and no owner sign-off
supported that advance. The advance subsequently occurred outside this workflow in
concurrent root commit `cab674b9`; the implementation therefore used the proposal's
validation branch rather than overwriting the newly selected root graph.

## 4. Detailed Change Proposals

### FrontComposer Checkout

**Artifact:** root-declared submodule working-tree state

OLD:

```text
root gitlink: 4e2e1af5225143aeac2e6ff6cf7517276cb1c242
checkout:     32db5c3460a4aa0ae6382ae9db36fa42e512ffd3
status:       drifted and not owner-validated
```

NEW:

```text
root gitlink: 32db5c3460a4aa0ae6382ae9db36fa42e512ffd3
checkout:     32db5c3460a4aa0ae6382ae9db36fa42e512ffd3
status:       aligned, targeted test passed, working-tree gitlink gate passed
```

Rationale: the root graph changed concurrently after approval, so validate the
newly recorded dependency rather than silently replacing it with the stale proposal
baseline.

### Sprint Status

**Artifact:** `_bmad-output/implementation-artifacts/sprint-status.yaml`
**Section:** Epic 7 retrospective carry-forward actions

OLD:

```yaml
  - epic: 7
    action: "Resolve the FrontComposer navigation accessibility blocker or validate/reset the FrontComposer pointer that caused the mismatch."
    owner: "Sally (UX Designer) + Amelia (Developer)"
    status: open
```

NEW:

```yaml
  - epic: 7
    # Closed 2026-07-16: root commit cab674b9 aligned FrontComposer at
    # 32db5c3460a4aa0ae6382ae9db36fa42e512ffd3; the targeted navigation
    # landmark test and working-tree gitlink gate passed.
    action: "Resolve the FrontComposer navigation accessibility blocker or validate/reset the FrontComposer pointer that caused the mismatch."
    owner: "Sally (UX Designer) + Amelia (Developer)"
    status: done
```

Rationale: preserve the retrospective action and add auditable closure evidence
instead of rewriting historical reports.

### Unchanged Artifacts

No edits are proposed to:

- `_bmad-output/planning-artifacts/parties-ui-prd.md`
- `_bmad-output/planning-artifacts/epics.md`
- `_bmad-output/planning-artifacts/architecture.md`
- `_bmad-output/planning-artifacts/epic-7-final-readiness-2026-06-29.md`
- `_bmad-output/implementation-artifacts/epic-7-retro-2026-07-07.md`
- FrontComposer or Parties source/test files
- `.gitlink-signoff.tsv`

## 5. Implementation Handoff

**Classification:** Minor
**Route:** Developer, with UX/test evidence review

Implementation commands are intentionally scoped to the root-declared
FrontComposer submodule and must not recurse into nested submodules.

Validation success criteria:

- `git submodule status -- references/Hexalith.FrontComposer` reports
  `32db5c3460a4aa0ae6382ae9db36fa42e512ffd3` without a leading drift marker.
- The UI test project builds in project-reference mode with 0 warnings and 0
  errors.
- `MainLayout_exposes_named_navigation_and_content_landmarks` passes at the
  root-pinned pointer: 1 total, 0 errors, 0 failed, 0 skipped.
- `scripts/gitlink-rc-gate.sh --worktree` passes with all root gitlinks validated or
  clean.
- The sprint action is changed from `open` to `done` after all targeted
  FrontComposer criteria passed.

## Change Navigation Checklist Record

### 1. Understand the Trigger and Context

- [x] 1.1 Trigger tied to Epic 7 final readiness and retrospective carry-forward.
- [x] 1.2 Core problem identified as a stale selector mismatch followed by an
  unvalidated FrontComposer checkout drift.
- [x] 1.3 Historical failure, current source, pointer state, markup, targeted test,
  and gitlink governance evidence collected.

### 2. Epic Impact Assessment

- [x] 2.1 Epic 7 remains completed; only its carry-forward action needs closure.
- [N/A] 2.2 No epic scope or acceptance-criteria change.
- [x] 2.3 Epic 8 receives a clean baseline with no dependency or sequencing change.
- [N/A] 2.4 No epic is invalidated and no new epic is needed.
- [N/A] 2.5 No priority change.

### 3. Artifact Conflict and Impact Analysis

- [N/A] 3.1 No PRD conflict or MVP-scope change.
- [x] 3.2 Existing architecture and gitlink governance require deliberate reset or
  owner validation; no architecture edit is needed.
- [x] 3.3 Existing UX and accessibility semantics remain satisfied; no UX edit is
  needed.
- [x] 3.4 `sprint-status.yaml` is stale relative to current test evidence, while
  historical readiness artifacts remain valid records.

### 4. Path Forward Evaluation

- [x] 4.1 Direct Adjustment is viable with low effort, low risk, and negligible
  timeline impact.
- [N/A] 4.2 Rollback of completed product work is unnecessary.
- [N/A] 4.3 PRD MVP review is unnecessary.
- [x] 4.4 Validation selected after the concurrent root commit aligned the pointer;
  no stale-baseline reset was performed.

### 5. Sprint Change Proposal Components

- [x] 5.1 Issue summary complete.
- [x] 5.2 Epic, artifact, and technical impacts documented.
- [x] 5.3 Recommended path, fallback, and non-selected pointer advance documented.
- [x] 5.4 Detailed old-to-new changes and success criteria documented.
- [x] 5.5 Minor-scope handoff assigned.

### 6. Final Review and Approval

- [x] 6.1 Checklist completeness reviewed.
- [x] 6.2 Proposal internally consistent and implementation-ready.
- [x] 6.3 Explicit user approval received.
- [x] 6.4 Implementation and post-change validation complete.
