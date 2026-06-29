## Run: 2026-06-10T17:25:24Z

**Epic:** parties - Epic Breakdown
**Stories:** 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7, 1.8, 1.9, 1.10, 2.1, 2.2, 2.3, 2.4, 2.5, 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 4.1, 4.2, 4.3, 4.4, 4.5, 5.1, 5.2, 5.3, 5.4

### Patterns Observed
- Source-of-truth verification from story files and sprint-status was more reliable than parser output; several Codex parser calls returned invalid JSON after successful child sessions.
- Browser Playwright execution repeatedly hit sandbox Kestrel socket permissions, but typecheck/spec discovery and focused component/unit tests still provided useful evidence.
- ConsumerPortal stories benefited from the narrow RCL-owned port plus UI-host adapter pattern; review issues mostly caught boundary, PII filtering, status-source, and File List gaps.

### Code Review Insights
- Common issues: authoritative-state reconciliation, unsafe backend metadata reaching UI summaries, incomplete story File Lists, and environment-proof wording.
- Average cycles to clean: 1 review cycle per completed story in this resumed run.

### Timing Estimates
- create-story: usually a few minutes, with longer runs when stories required contract archaeology.
- dev-story: longest for cross-layer privacy stories that touched contracts, UI, fixtures, and tests.
- code-review: usually one cycle, with auto-fixes applied in the same cycle.

### Recommendations for Future Runs
- Add a pre-review File List check that compares touched files against the story File List.
- Keep Playwright browser-runtime evidence separate from typecheck/spec-discovery evidence in every story record.
- Treat new GDPR user promises as backend-contract checks first, then UI copy and tests.

## Run: 2026-06-29T17:35:42Z

**Epic:** parties - Epic Breakdown
**Stories:** 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7, 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 7.7, 7.8

### Patterns Observed
- Direct source-of-truth checks against story files, sprint-status, and tmux pane state were more reliable than long-running monitor sessions after tmux panes exited.
- Manual staging was required because `commit-story` would have staged unrelated `references/*` gitlink drift.
- Cross-submodule Epic 7 stories needed exact blocker recording instead of broad success claims; final readiness preserved rollback paths where deletion proof was incomplete.

### Code Review Insights
- Common issues: no-leak diagnostics in retry paths, self-invalidating artifact tests that asserted pre-review status, and stale artifact assertions after method renames.
- Average cycles to clean: one review cycle per story; Claude review applied fixes in-cycle for 7.6, 7.7, and 7.8.

### Timing Estimates
- create-story: usually a few minutes once story range and agent configuration were fixed.
- dev-story: longest for migration evidence stories that touched security, projections, submodule compatibility, and validation artifacts.
- code-review: usually one cycle, but monitor cleanup often required direct tmux/status verification.

### Recommendations for Future Runs
- Keep manual, explicit `git add` lists when the worktree has known unrelated submodule drift.
- Prefer focused build/test lanes with `MinVerVersionOverride=1.0.0` when no-dependencies builds would otherwise stamp assemblies as `0.0.0.0`.
- Treat final gate stories as evidence and blocker recording work; do not delete rollback-only code without fresh parity proof and a non-deletion rollback path.
