# DW-166 validation — 2026-10-18

The versioned cleanup/compensation/probe fix builds with zero warnings or errors.
Final focused tests pass 58/58 without errors, skips or unrun tests. Before wiring
the fix, the initial 52-case focused run reproduced 27 failures.

The full affected assembly passes 1156/1157, with no errors, skips or unrun tests.
The only failure is the pre-existing
`PlatformApiPrerequisitesTests.FinalDependencyReceiptsMatchTheSelectedPackageAndSourceGraph`.
Its unchanged baseline test expects Builds
`4f53d9f37c5b86cfd43fbcc8fa5dd4df19b84c77`; baseline Parties commit
`d2ec85c9e2b85e2b0c7a50d92ffaf197590ea053` already selects
`544fd2e907d62ccdc4b8f24168b8ab5dfcc13eb9`. Both test blob identities and the
baseline gitlink are preserved in validation.json. A focused invocation of this
test also exits 1. No gate or dependency receipt was changed. DW-166 and its spec
remain open/in-review pending the required full-suite gate.

`validation.json` records exact commands, exits, test counts, failures and final
code/test hashes. The XML and diagnostic outputs remain under
`/tmp/dw-166-validation/` for this session. Commitlint 21.2.2 accepted the exact
commit message; successful output is retained in commitlint.txt. The warning
policy and whitespace guards pass.

The exact Parties Aspire AppHost was started before source edits. Parties passed
the 20-second healthy wait in 16.9 seconds; resource state was inspected. The
AppHost was stopped before builds, and the final running-AppHost list is empty.
This baseline does not qualify optional Memories or terminal physical erasure.

Independent review found one malformed-charset defect and three missing
verification cases. All four are fixed and independently reinspected: charset
failures remain blocked, accepted misses remove their mapping, mixed outcomes
retain only blocked mappings, and cancellation propagates through decoder,
cleanup, health probe and compensation. The fifth finding requested the final
evidence and ledger updates, which are retained here. No production data or
external requests were used: the compensation HTTP tests use an ephemeral local
Kestrel server, and cleanup/probe tests use in-memory handlers and mappings.
