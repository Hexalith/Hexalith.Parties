---
project: parties
date: 2026-07-16
workflow: bmad-correct-course
mode: incremental
status: proposed
change_scope: minor
approval: pending
---

# Sprint Change Proposal — FrontComposer Navigation Accessibility Closure

## 1. Issue Summary

Epic 7 final readiness recorded one failing UI test:
`MainLayout_exposes_named_navigation_and_content_landmarks`. The test expected the
obsolete FrontComposer selectors `fc-navigation-full` or `fc-collapsed-rail`, while
the selected FrontComposer surface exposes a single `fc-navigation-rail` landmark.
The Epic 7 retrospective therefore carried an open action to resolve the
accessibility blocker or validate/reset the FrontComposer pointer.

Current evidence separates the accessibility contract from the pointer hygiene
problem:

- The Parties test now asserts `fc-navigation-rail` and passes against the current
  FrontComposer checkout: 1 test run, 1 passed, 0 failed.
- The navigation root still exposes `role="navigation"` and
  `aria-label="Application navigation"`.
- The root repository pins FrontComposer at
  `4e2e1af5225143aeac2e6ff6cf7517276cb1c242`, but the working checkout is at
  `32db5c3460a4aa0ae6382ae9db36fa42e512ffd3`.
- The relevant `FrontComposerNavigation.razor` markup is identical at those two
  commits.
- The later checked-out pointer has no owner validation in `.gitlink-signoff.tsv`.
  The established release-candidate rule therefore requires either an owner-
  validated advance or a deliberate reset to the recorded root pointer.

The remaining blocker is not an accessibility design defect. It is an unvalidated
FrontComposer checkout drift that prevents release evidence from being tied to the
root-selected dependency graph.

## 2. Impact Analysis

### Epic and Story Impact

- Epic 7 remains completed maintenance scope.
- No story, acceptance criterion, dependency, sequence, priority, or product scope
  changes.
- Epic 8 sequencing is unchanged; it receives a clean, root-reproducible UI
  accessibility baseline.
- No new epic or story is required.

### Artifact Conflicts

- `sprint-status.yaml` still reports the navigation action as `open`; it should be
  closed only after the exact test passes at the root-pinned pointer.
- The dated Epic 7 final-readiness report and retrospective accurately record the
  state observed at their assessment dates and must remain unchanged.
- The PRD, epics, architecture, UX specifications, and accessibility review already
  express the intended navigation semantics and require no edit.
- `.gitlink-signoff.tsv` requires no edit because the proposal does not advance the
  root gitlink.

### Technical Impact

No FrontComposer source, Parties source, public contract, deployment artifact, or
runtime behavior is changed. The adjustment changes only the checked-out commit of
the root-declared `references/Hexalith.FrontComposer` submodule, aligning it with the
gitlink already recorded by the root repository.

Nested FrontComposer submodules must remain uninitialized.

## 3. Recommended Approach

Use **Direct Adjustment**:

1. Reset only `references/Hexalith.FrontComposer` to the root-recorded gitlink
   `4e2e1af5225143aeac2e6ff6cf7517276cb1c242`.
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

Advancing the root gitlink to the later checkout is not recommended. No required
navigation behavior differs, the later pointer contains unrelated change, and no
owner sign-off supports that advance.

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
root gitlink: 4e2e1af5225143aeac2e6ff6cf7517276cb1c242
checkout:     4e2e1af5225143aeac2e6ff6cf7517276cb1c242
status:       aligned with the root-selected dependency graph
```

Rationale: select the already-reviewed root dependency rather than silently
advancing to an unrelated, unsigned pointer.

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

NEW, contingent on passing validation:

```yaml
  - epic: 7
    # Closed 2026-07-16: reset the FrontComposer checkout to root-pinned
    # 4e2e1af5225143aeac2e6ff6cf7517276cb1c242; the targeted navigation
    # landmark test passed against the root-selected dependency graph.
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
  `4e2e1af5225143aeac2e6ff6cf7517276cb1c242` without a leading drift marker.
- The UI test project builds in project-reference mode.
- `MainLayout_exposes_named_navigation_and_content_landmarks` passes at the
  root-pinned pointer.
- The root gitlink gate no longer reports FrontComposer drift or missing
  FrontComposer validation. Unrelated gate findings are recorded but not modified.
- The sprint action is changed from `open` to `done` only after all targeted
  FrontComposer criteria pass.

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
- [x] 4.4 Deliberate reset selected because it restores the approved dependency
  graph without introducing unrelated FrontComposer changes.

### 5. Sprint Change Proposal Components

- [x] 5.1 Issue summary complete.
- [x] 5.2 Epic, artifact, and technical impacts documented.
- [x] 5.3 Recommended path, fallback, and non-selected pointer advance documented.
- [x] 5.4 Detailed old-to-new changes and success criteria documented.
- [x] 5.5 Minor-scope handoff assigned.

### 6. Final Review and Approval

- [x] 6.1 Checklist completeness reviewed.
- [x] 6.2 Proposal internally consistent and implementation-ready.
- [ ] 6.3 Explicit user approval pending.
- [ ] 6.4 Implementation and post-change validation pending approval.
