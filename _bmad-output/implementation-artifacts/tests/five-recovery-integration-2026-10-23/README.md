# Five recovery integration items — 2026-10-23

All five code changes are implemented and independently reviewed. Four ledger items close; legacy batch compaction retains its matching-package gate because EventStore 3.127.0 remains selected.

| Item | Disposition |
|---|---|
| DW-116 source RCL delivery | Exact selected script bytes and JavaScript type pass through the source host; owner multi-target restore prerequisite fixed. |
| MCP algorithms | Registered explicit lists replace defaults; the full 120-case owner suite passes. |
| Legacy batch compaction | Source recovery, mixed deletes and eight conditional-write races pass; matching package adoption remains open. |
| Legacy certificate recovery | Fresh read-back/audit and durable evidence are required; malformed records, cancellation and fault controls pass. |
| SDK history snapshots | Complete state restoration and writer-policy validation pass actual SDK persisted capture/read-back and command tests. |

The final Release service suite passes 2,671 cases with 12 explicit source/runtime prerequisite skips and zero failures. Security passes 334/334 in source and package modes. Contracts passes 226/226, Projections 458/458, MCP 120/120, and EventStore Client 2,139 with two existing live-runtime skips. Rebased source UI passes 414 with one existing compact-navigation opt-in skip; the required asset assertion runs and passes. Builds report zero warnings/errors.

All 16 review findings, including the mixed encrypted/plaintext residual, are fixed. Its independent probe now reports unreadable state. Both clean-owner provenance failures are resolved; task-range/worktree gitlink gates pass. Exact owned source runs were cleaned up.

[Final verification](final-verification.json) records published owner identities, incoming ancestry, commands, results and final code hashes. [Initial validation](validation.json) is historical. [Matrix audit](matrix-audit.json) maps every required row to executed passing coverage.

Whole-topology Platform HXR020, matching SDK package adoption, production custody/expiry, deployment and unrelated release-floor approvals remain separate gates. No package pin, deployment or regulated data was changed.
