# Five deferred release-proof entries — 2026-10-21

Closes DW-106, DW-132, DW-151, DW-162 and DW-191 by qualifying their already-committed exit conditions. DW-132 and DW-151 are duplicate records of one release-bypass issue. This batch adds three executable release-source cases and eighteen McpCli/Platform source-row cases plus two standalone real Git-receipt cases, and binds the Git describe to the current identity cell.

The release source harness executes the actual tracked Bash with fake GitHub responses and GITHUB_OUTPUT. A matching successful run among unrelated runs succeeds; independently invalid runs cannot combine into proof. Successful Commitlint and retired bypass values cannot replace full CI. Existing publication-wrapper cases require ci.yml at every boundary.

The prerequisite suite checks all ten root selections against committed gitlinks, clean checkouts and exact Git describes, separately from the eight consumed dependency rows. Current-source descriptions cannot borrow a valid describe from historical prose. The existing Story 8.8 gate correction is already committed and its retention test passes.

See [validation.json](validation.json) for exact commands, execution times, suite counts and historical implementation revisions. Both Debug test-project builds pass without warnings or errors; the complete affected assemblies pass 2,257 service and 249 CI cases without skips. These are local test results; remote push CI remains a separate release requirement.

The required Aspire baseline started, but security was unhealthy after an HTTPS handshake EOF and dependent resources waited. The exact AppHost was stopped cleanly before builds. This work claims no runtime topology qualification. Package selections, root pointers, owner approvals, historical receipts, existing frozen stories and canonical sprint status are unchanged.

DW-132/DW-151 policy authority is the existing human decision in [spec-align-parties-ci-cd-with-eventstore-tenants.md](../../spec-align-parties-ci-cd-with-eventstore-tenants.md), “Release-gate decision — 2026-10-07”. It explicitly selects removal of the bypass and mandatory full CI; this work reconciles that already-implemented decision.

Independent review raised four findings, all addressed. Four added spacing regressions fail against the pre-correction helper, then pass with dependency-cell parsing. Final verification reran both complete assemblies after all four corrections.

Final verification: 2,257 service tests and 249 CI tests pass (2,506 total), with zero failures/errors/skips/not-run. Both final Debug builds have zero warnings/errors. Independent follow-up found no material introduced issue and no new work was deferred. Pinned commitlint 21.2.2 validates the exact [commit message](commit-message.txt) with [zero problems or warnings](commitlint.txt).
