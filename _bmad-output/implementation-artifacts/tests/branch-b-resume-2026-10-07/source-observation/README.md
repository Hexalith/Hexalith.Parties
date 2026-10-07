# Current-source observation verification

This packet records the 2026-10-07 Branch B continuation. It establishes local Debug/source behavior and compiled-source correspondence. The spec remains `in-progress`; its original `5388884eec84b16545fdc008b2fc04547b0ed5b6` baseline and both open production/live tasks remain unchanged.

`PartyIdentitySourceFold` now rejects missing observation identity/time, incomplete source shape, ciphertext or unsupported serialization, opaque/undefined protection states, unknown protection schema, and establishment/rebind/revocation instants later than the source observation. Readable JSON remains supported with legacy absent metadata, explicit unprotected metadata, and retained Protected provenance after successful gateway decryption. The existing suspended-custody clock tests begin with a coherent observation and still introduce rollback only while custody is awaited.

The corrected negative cases failed **11/11** against the unmodified original fold and pass after the correction. The focused final run passed **18/18**, including three supported-metadata cases and four preserved custody timing cases. The temporary MSBuild override in `reproduce-original-fold.targets` compiled the retained original source without reverting tracked files. `before-PartyIdentitySourceFold.cs.txt` preserves those exact source bytes; the final changed-source hashes are in `source-hashes.json`. Exact build and test commands are in [current-local/verification.json](current-local/verification.json). Earlier evidence that incorrectly treated decrypted Protected JSON as unreadable is superseded and excluded from this packet.

The final Local evidence totals **933 passing tests** across all ten required lanes, with zero warnings/errors/failures/skips/not-run cases. Each configured class has executed passing tests. It consists of the second complete Local run plus one focused **168/168** EventStore Server lane refresh with identical flags and class filters. The independent parent checked all four frozen matrix rows, all twelve callback regressions, and **3,393 unique workspace C# documents across 337 PDB copies**, with zero mismatches or skipped documents.

| Lane | Passed |
| --- | ---: |
| EventStore Contracts | 23 |
| EventStore Client | 107 |
| EventStore Server | 168 |
| Platform Identity | 2 |
| Parties Contracts | 29 |
| Parties Server | 94 |
| Parties domain/query | 310 |
| Parties Security | 30 |
| Parties Client | 98 |
| Parties UI | 72 |

[initial-local](initial-local/verification.json) preserves the first 933-pass run. Its later independent audit detected an external EventStore Client source change. [current-local](current-local/verification.json) records the final run and focused Server refresh after a second external sample-source change; preceding Server evidence remains alongside it. The original complete runs are retained in `/tmp/ext-parties-1-source-validation-20261007`. No unchanged-whole-worktree claim is made. Parties HEAD moved externally from `2fb9e545adbb2f974abf3791b726a2f29750bcf9` through `2c2ff2ea4ead2c4b8cbd3a96c60f04426630efd1`, including the two identity source changes. The parent later observed Parties `0d72186fe3e031d34665969255cc23ce265d793c` and Platform `60c2b0dec172261d27461f30cc0c001334a7b861`. This implementation agent did not stage, commit or push; these observations do not replace the original spec baseline or establish an accepted target.

Hexalith.Platform owns production composition. Its `docs/implementation/actor-history-lifecycle-2026-10-07.md` confirms that `AddPlatformCustody` registers cleanup but no `IIdentityHistoryCustody` backend. The production adapter, durable all-copy destruction receipts, nonrollback restore and live successor qualification remain unavailable. Complete installed P-01–P-10 persisted-state/restart/restore/failure-injection probes and targets remain required. Local tests and Platform ownership do not establish identity availability. The parent retains its sanitized Aspire, discovery and missing-input Live-gate observations separately.

[Independent root verification](root-audit/root-final-verification.json) records final class/matrix/source checks and the review scope. [Root matrix audit](root-audit/root-final-matrix-audit.json) and [regression audit](root-audit/root-regression-audit.json) preserve the execution mapping. [Sanitized Aspire baseline](root-audit/root-aspire-baseline.json) records healthy local resources and exact owned shutdown; [Platform production discovery](root-audit/root-platform-production-discovery.json) and [Live gate](root-audit/live-gate.txt) record the remaining installed-provider/probe blocker. Superseded PDB audit summaries disclose both external source changes. The complete historical baseline diff was captured separately; the parent reviewed the current identity patch and relevant SDK contracts, not every unrelated historical change.
