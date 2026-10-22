# Five query and release deferrals — 2026-10-22

Implementation baseline: `12a071cf7dfec52b335f52e7d162fb835f49af9c`.
Scope: DW-41, DW-109, DW-161 and the two identity options/selection entries
from [the approved spec](../../spec-five-query-release-deferred-items.md).

Before independent review, fresh Release/package builds of the service and CI test projects succeeded with
zero warnings and errors, using the normal enabled NuGet audit setting. The
focused identity class passed **292/292**; the complete service assembly passed
**2,446/2,446**. Focused Gitlink/tag/artifact classes passed **98/98**; the complete
CI assembly passed **314/314**. Those runs report zero errors, failures,
skips and not-run cases. Exact commands, outputs and file hashes are indexed in
[validation.json](validation.json).

The identity cases gate initial options and every policy revalidation for both
queries, race caller cancellation, retain saturated capacity through late faults,
and verify recovery. Existing cumulative-budget tests also assert the actual
timer is shortened to ten seconds, or to three seconds after seven seconds of
setup. Gated real interval scanning remains bounded by its query deadline and
retains the work lease; semantic cases preserve gaps, ambiguity, exclusive ends
and original source positions. Terminal policy tests move expiry during the
await and require release-time denial.

Git fixtures create real isolated histories without checking out submodules.
They exercise unsigned/signed/stale/placeholder/reset ledger rows, intermediate
and multi-commit pointer changes, unusable/nonancestor bases, removed root
declarations and malformed `.gitmodules`. RC-style `--diff` controls remain.
The fixed fixture commit message has its own successful pinned commitlint result
in [fixture-commitlint.txt](fixture-commitlint.txt); this is separate from the
parent's pending main-repository commit validation.

Publication fixtures execute the actual CLI with local HTTP archives and the
validator extracted from Builds `397c94a4e246c90b21cf408790fa0d55bf32d795`.
Successful cases require all nine package IDs and all three images with both
architectures, and recompute every retained package/index/child/config receipt
hash from its raw bytes. Rejections cover missing artifacts, wrong nuspec
identity/repository/commit metadata, malformed and duplicate archives, ambiguous
inventories, OCI digest/platform/provenance errors, authentication failures,
response byte limits, timeout and exhausted retries. Missing/transient/transport
recovery must finish within the configured finite attempts and total budget.
Fixture credentials are checked absent from textual receipts and diagnostics.

Actionlint, Bash syntax, the warning/nested-submodule guard and whitespace checks
pass. Service output is retained as a runner summary to omit unrelated fixture
application logs. Workspace/home prefixes in build and baseline output are
normalized; commands and observed exit codes are preserved.

Debug/source qualification remains blocked. The first launcher attempt reports
HXR011 because no Dapr home is configured. Reusing the existing prepared home
reaches **HXP020: mapped Parties source project unavailable**, explicitly without
starting resources. Both results are retained. Initial complete CI attempts also
record generated restore assets with `NuGetAudit=false`; normal UI.Tests and
Contracts.Tests restores and normal service/CI builds repair those generated
inputs. The final complete CI run passes with audit enabled, without changing
tracked build policy.

Final parent verification after review passes **2,450/2,450 service tests** and
**360/360 CI tests**, with zero failures, errors, skips or not-run cases. The
reviewed identity class passes **296/296**. The initial post-review CI subset
passes 140/140; four further connection cases are included in the final complete
360-case run. All twelve direct connection/download controls also pass.

Review corrections protect main-source concurrency, delayed declarations and
merge histories; exercise actual workflow/query callers; reject nested nuspec
metadata, missing license files and incorrect required dependencies; and bound
DNS/TCP/TLS, headers, bodies and redirects. Four shared connection slots retain
abandoned work until faults and late sockets settle. Referenced license files,
catalog versions, credential isolation and partial-byte receipts are covered.
Independent follow-up review confirms no material findings remain. One existing
publication-policy follow-up is recorded separately in the ledger, and the
optional detailed metadata error taxonomy was rejected as disproportionate.

No dependencies, gitlinks, signoffs, contracts, retention, persistence or
deployments changed. No registry artifact was published for testing. Observed
registry hashes do not prove equality with unsigned build archives. Independent review and exact main-commit validation pass. This evidence records
pre-commit checks; the parent workflow owns the authorized commit and push.

Output whitespace is normalized for repository formatting; commands, observed exits and diagnostics are retained.
