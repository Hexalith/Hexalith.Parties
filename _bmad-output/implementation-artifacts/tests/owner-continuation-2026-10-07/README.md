# Parties continuation verification — 2026-10-07

The exact owner Local runner passed all ten required source lanes: **784 tests**, zero errors, failures, skips or not-run cases. All ten Debug builds report zero warnings/errors. Fresh Platform custody verification passed **115** more tests, for **899 distinct final passing tests**. Focused replay29 and retained-source36 results overlap the matrix and are not added to that total.

[Machine evidence](evidence.json) records full canonical initial/final HEADs, worktree observations, seven owned paths and their baseline/current hashes, exact commands, artifact hashes and log/XML hashes. [Source manifest](source-manifest.json) records hashes only, captured after verification. Scoped [Parties diff](owned-parties.patch) and [EventStore diff](owned-eventstore.patch) preserve the initial-baseline comparison even where another concurrent workflow committed Parties changes. External commits and other worktree edits are observations; this workflow performed no staging, commit, push, deployment or submodule action.

| Required lane | Passed | Errors / failures / skipped / not run |
| --- | ---: | --- |
| Hexalith.EventStore.Contracts.Tests | 23 | 0 / 0 / 0 / 0 |
| Hexalith.EventStore.Client.Tests | 107 | 0 / 0 / 0 / 0 |
| Hexalith.EventStore.Server.Tests | 156 | 0 / 0 / 0 / 0 |
| Hexalith.Platform.Identity.Tests | 2 | 0 / 0 / 0 / 0 |
| Hexalith.Parties.Contracts.Tests | 29 | 0 / 0 / 0 / 0 |
| Hexalith.Parties.Server.Tests | 94 | 0 / 0 / 0 / 0 |
| Hexalith.Parties.Tests | 173 | 0 / 0 / 0 / 0 |
| Hexalith.Parties.Security.Tests | 30 | 0 / 0 / 0 / 0 |
| Hexalith.Parties.Client.Tests | 98 | 0 / 0 / 0 / 0 |
| Hexalith.Parties.UI.Tests | 72 | 0 / 0 / 0 / 0 |

[Final runner log](local-matrix-run.log) and [per-lane manifest](local-matrix/local-evidence.json) link the individual build/test logs and XML. Platform [build](platform-cleanup/build.log), [test log](platform-cleanup/tests.log) and [XML](platform-cleanup/tests.xml) are fresh source evidence using synthetic custody.

The historical destroyed-profile replay case already passed on the initial current source Debug build: [initial test log](replay-repro/tests.log). Its JSON adapter existed in Parties at the captured baseline. This continuation hardens that adapter; it does not claim to have newly fixed the original historical failure. Twelve new processor boundary cases [failed before](replay-repro/red-tests.xml) and [passed afterward](replay-repro/green-tests.xml). Six shared retained-source cases [failed before](shared-security/red-tests.xml) and [passed afterward](shared-security/green-tests.xml), with all 36 retained-source tests passing. Six additional accepted-policy query cases and three shared source lifecycle cases executed in the final matrix; all **27 new cases** are included in the 784 total.

Earlier attempts remain intact. [Attempt 1](attempt-1/local-matrix-run.log) stopped on concurrent EventStore Client test CS1929; its owner corrected that unrelated source. [Attempt 2](attempt-2/local-matrix-run.log) reached UI and found five source-root discovery failures with external artifacts. [Attempt 3](attempt-3/local-matrix-run.log) showed that changing to process working-directory discovery still failed because xUnit runs from the assembly directory. The final owner runner explicitly passes its source root, restores the prior environment value in finally, and executes every original assertion and required class. Archived manifests retain the absolute locations they had at execution, before archival; their sibling logs/XML are preserved in the attempt folders.

Production custody and full owner qualification remain unavailable. The tests prove pre-expiry rebind boundary/serialized restore, immutable exclusive 365-day expiry, source preservation, and fresh lifecycle denial after reader restart. They explicitly prove that an expired/destroyed predecessor cannot be skipped to authorize a successor. No positive successor-after-destruction, real keys, all-copy destruction, production restore or Level 4 result is claimed. See the [owner packet](../../ext-parties-1-replay-and-lifecycle-2026-10-07.md) for the exact missing contracts and qualification inputs.
