# Lifecycle and qualification implementation evidence (2026-11-10)

`verification.json` is the historical implementation-handoff receipt. It records baseline revisions, source/assembly hashes, exact
observer commands, outcomes and remaining parent-workflow gates. The owner MCP
packet is in the root-declared FrontComposer repository at
`_bmad-output/implementation-artifacts/tests/lifecycle-alias-and-reader-2026-11-10/`.

The owner final baseline replay fails 23/119 lifecycle cases; corrected source
passes 119/119 lifecycle and 529/529 full MCP tests, all with zero skips. Its
packet contains exact commands and test XML.

The observer red controls use a newly extracted injectable copy of the original
suppression behavior, retained as `observer-baseline-extraction.cs.snapshot.gz`.
This seam preserves original task/children/identity/role traversal and initially
returns no qualification failures. It is not represented as an unchanged HEAD
IntegrationTests binary. Ten of eleven controls fail before hardening; the
complete-tree positive control passes. Corrected Release controls pass 11/11
with zero skips, including a real owned-process cleanup. Final builds retain
warnings as errors and finish with zero warnings/errors. The same hardened
helper is called by the ordinary live timing test, which records all capture
refusals before asserting eligibility after cleanup. The live runtime rerun is
still a separate parent-workflow gate.

Browser checks ran under installed Node 24.22.1/npm 11.19.1. Typecheck passes;
all eight configuration controls pass; both maintained discovery sets include
both formatter cases. `CI=true npm run test:ui -- --retries=0` passes 81/81
including those two cases. Each command also ran with a temporary injected
formatter exception selected by its describe name: both `test:ui` and
`test:portals` return exit 1 (one failure, one formatter pass). The original
formatter spec bytes were restored in a finally block before final green
qualification. `browser-commands.json` records every command and exit code;
final JUnit and full logs are retained. An earlier ambient Node 26 diagnostic
run also passed but supplies no additional qualification here.

Aspire was started with the exact Parties AppHost, resource state was captured,
and the owned app was stopped before implementation builds. The baseline startup
log's dashboard query token is redacted; the original is retained outside the
repository at `/tmp/five-aspire-baseline-unredacted.log`. The baseline state is
diagnostic and supplies no ordinary launcher stop-timing acceptance.

At implementation handoff, the remaining gates were source Debug/package Release consumer builds and selection
fitness, fresh runtime preparation and live ordinary timing, workflow review,
owner commit/push, exact gitlink/current-source adoption, and parent commit/push.
No versions, budgets, CI ordinary-live opt-ins, deployments or publications changed.

## Current evidence

The later `orchestrator-verification.json` and `review-verification.json` record consumer builds, focused controls, runtime preparation and live timing after the handoff. The earlier pending list and spec hash describe that historical capture stage. Final content hashes and completion status are recorded in `final-verification.json`; the original handoff hashes remain unchanged.

## Final result

All five selected deferrals are resolved. The reviewed FrontComposer source commit `7ace06c5c50406b9021fc95d8d87cf962b5180dd` is pushed to main and selected by the committed Parties gitlink; published package 4.7.6 remains unchanged. The three review layers completed, all patch groups are fixed, and two pre-existing observer follow-ups remain separately deferred.

| Current gate | Result |
| --- | --- |
| FrontComposer MCP suite | 531 passed, zero skips |
| Parties source Debug build and focused controls | warning-free; 28 passed |
| Parties package Release build and model/cleanup controls | warning-free; 103 passed, four separate mTLS opt-in skips |
| Pinned-toolchain browser gate | 81 passed, retries disabled |
| Live ordinary timing, complete and injected incomplete capture | 2 passed, zero skips |
| Final repository fitness with exact committed source selection | 159 passed, zero skips |

The final positive live run records complete capture, five exit-143 launchers, no survivors and 9324.7 ms quiescence against the 30-second budget. The injected incomplete run retains fifteen capture refusals, no survivors, exit-143 launchers and disposed-application proof before qualification is refused; its quiescence is 9653.7 ms. These have distinct JSON files beginning `final-ordinary-launcher-stop-timing`. `final-runtime-profile/` binds the preceding runtime preparation; subsequent source-selection verification does not recertify those runtime bytes.

`post-commit-verification.json` and `final-source-fitness.xml.gz` supersede the earlier dirty-owner and missing-graph-sentence refusals. Those refusals remain retained as historical diagnostics. Final source hashes are in `final-verification.json`; the historical handoff receipt preserves its original hashes under an explicit capture-stage label. The final amendment changes completion metadata and evidence only; source code, selected gitlink and the evaluated package graph remain those verified by the passing gate.
