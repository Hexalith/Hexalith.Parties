## Run: 2026-05-22T11:28:11Z

**Epic:** Hexalith.Parties - Epic Breakdown
**Stories:** 2.7 through 8.6 active range

### Patterns Observed
- Parent-managed recovery was required because tmux-backed child sessions repeatedly failed to spawn or ran with read-only child workspaces.
- The accepted EventStore-fronted Parties client/gateway contract is the central blocker for remaining Epic 7 and Epic 8 UI implementation work.
- Sprint-status is the reliable source of truth when historical orchestration progress rows drift.

### Code Review Insights
- Common issues: accessibility details, privacy-safe ARIA/DOM identifiers, fail-closed capability handling, and localization seams.
- Average cycles to clean: one direct review cycle after focused tests and source scans were in place.

### Timing Estimates
- create-story: fast for blocked stories once the dependency evidence is loaded.
- dev-story: bounded by focused AdminPortal tests and Release solution builds for implemented stories.
- code-review: one focused cycle per implemented story in the recovery path.

### Recommendations for Future Runs
- Resolve or risk-accept `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md` before rescheduling Stories 7.7 or 8.1 through 8.6.
- Keep parent-managed recovery available until the tmux runner and child workspace permissions are fixed on Windows.
- Reconcile orchestration progress rows against `sprint-status.yaml` during resume and wrap-up.

## Run: 2026-05-24T15:44:30Z

**Epic:** Embeddable Party Picker (Epic 8)
**Stories:** 8.2 through 8.6

### Patterns Observed
- Windows tmux execution required the patched legacy runtime: `-c "."`, sourced command files, LF command scripts, direct `node.exe` Codex invocation, and Python subprocess argv handoff for multiline prompts.
- Existing story artifacts for 8.3-8.6 needed task/dev-note repair after scoped risk acceptance; dev-story stopped early when those sections were missing or incomplete.
- Focused picker tests were the reliable closure signal. Full-solution regression still exposed unrelated AppHost, sample, deploy-validation, benchmark, and file-lock failures.
- Privacy and boundary hardening worked best as executable guardrails: source scans, JavaScript payload whitelists, event-detail reflection, encoded rendering tests, and token-handling tests.

### Code Review Insights
- Common issues: stale-response edge cases, ARIA relationship completion, event payload whitelist precision, and tests proving token-value privacy invariants.
- Average cycles to clean: one review cycle for Stories 8.4, 8.5, and 8.6; Story 8.3 needed a second cycle after a stuck first review session.

### Timing Estimates
- create-story repair: short once scoped risk acceptance and predecessor story state were verified.
- dev-story: several minutes for focused picker stories; retry may be needed when Codex stops after status/context updates.
- automate: useful for guardrail expansion when the dev implementation is already green.
- code-review: 8-12 minutes for Claude high-effort review on picker stories.

### Recommendations for Future Runs
- Keep the patched story-automator runtime changes available or upstream them before relying on autonomous tmux sessions on Windows.
- For risk-accepted stories, repair Tasks/Subtasks and Dev Notes before spawning dev-story.
- Preserve the focused picker suite as the required quality gate for future picker changes, and treat full-solution failures separately unless they touch changed files.
- Add a reusable source-verification checklist for public docs and samples, as called out by the Epic 8 retrospective.
