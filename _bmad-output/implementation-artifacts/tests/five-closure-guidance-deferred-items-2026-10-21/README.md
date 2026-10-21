# Five planning and closure guidance items — 2026-10-21

This packet covers DW-16, DW-42, DW-43, DW-45 and DW-114 from clean Parties
`4fbe30883eb18ec074530e49a42669b5600ba2af`.

DW-16 aligns the persistent project context with authoritative default-main
guidance. DW-43 restores the G8 run/publish authentication and integrated-topology
proof, DataProtection prerequisite identity binding, and external deployment
responsibilities to the cached Epic 8 context. These are existing constraints;
this work grants no new migration, deletion, deployment or parity approval.

DW-42 adds reproducible historical Git commands to the G5 matrix row. Parties
`75b4fa1fe2cfd2167c186e94faf52e18be4b0ff6` selects EventStore
`865cd9e49273dffbb1cdae85efeaf1aac322e09e` (`v3.113.0`) and Builds
`360a2b9c4e96809365a7de785be9a68152d5ac28`. These are the existing historical
receipt identities. Current structural inspection, missing provider gates and
historical parity receipts retain their separate scope.

DW-45 restores and attributes a specific timestamp fix. Commit
`d4e1d83d22d554345dec816c52b00b87954002af` (2026-08-04) added the
`not args.fresh` guard and YAML timestamp assertions. The BMAD update at
`4d39759a4dc72d5d679f88dc449dd54c6881885a` (2026-08-16) reverted both.
This is separate from `STORY_RANK` and `_slug()` behavior. `--fresh` now starts
both timestamps at the supplied date; incremental refresh preserves original
`generated`. Retrospective action items still survive and explicit status
overrides still apply last. All planner tests use temporary fixtures; canonical
sprint status is untouched. The restored test fails before the correction
(36 passed, 1 failed), then the whole planner file passes 37/37.

DW-114 reads each complete ledger entry and uses its first-class status, so
completed work with retained reason fields cannot satisfy an accepted wait.
Missing, unknown or duplicated ledger status also cannot establish acceptance.
Legacy accepted/open bullets remain supported. The fixed expected-wait set and
dependent invariants must be deliberately reconciled when one actually closes;
the parser no longer hides a stale expectation. `failing-before.json` records
11 real failures out of 48 closure cases against the baseline parser.

`validation.json` records exact final commands and outcomes. Debug validation
uses the repository's default published dependency graph. No runtime topology,
source-mode graph, production custody, or new shared-provider parity is claimed.

Before the review corrections, the full affected service assembly passed 2,227/2,227 without errors, failures or
skips; focused closure and prerequisite checks pass 106/106. The Debug build has
zero warnings/errors. Repository guards and pinned commitlint 21.2.2 pass.

After the review corrections, the full service assembly passes 2,237/2,237,
including all 58 closure and 58 prerequisite cases, with no errors, failures,
skips or not-run cases. Ten added review cases reproduced four failures before
the corrections (`review-failing-before.json`). Five review findings were
addressed; the unsupported fenced-example concern was rejected as a low-impact
format expansion. Independent follow-up found no remaining material introduced
defect. Final build, warning/submodule guard, whitespace and commitlint pass.
