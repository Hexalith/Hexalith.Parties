# Five FrontComposer UI deferred items — 2026-10-27

Scope: DW-91, DW-100, DW-101, DW-102 and DW-112 (G4 B–F only).

The producer supplies neutral freshness/live-region/reconciliation contracts,
an injectable ephemeral stream/file/JSON download service and optional ordinal
name confirmation. Parties keeps stable adapters and the local rollback.
Shared registration requires the explicit source qualification option
`Parties:FrontComposer:UseSharedUiPrimitives=true`; its production default is
false. All five ledger entries remain open for named owner acceptance and/or
matching upstream publication and adoption. G4-A and Story 8.9 are unchanged.

| Matrix row | Final qualification |
| --- | --- |
| Route navigation (DW-91) | Producer cold/client route and Parties forward-Tab controls pass |
| Compact navigation (DW-112) | Mounted-target/resize controls and deterministic focus-ownership regression pass |
| Freshness/reconcile (DW-100) | Current/stale/degraded, acceptance/rejection, cancellation and duplicate/late confirmations pass |
| Export (DW-101) | Exact Admin/Consumer bytes and filenames, repeats, cancellation and cleanup pass |
| Typed erasure (DW-102) | Empty/whitespace/case mismatch rejected; exact name submits once; Cancel/Escape stay safe |
| Package prerequisite | Retained behavior passes; three forced 4.7.0 route/compact negatives fail as expected |

Final counts: producer components 103/103, contract/API controls 3/3, mapped source
UI/Admin/Consumer 419/325/83, producer browser 20/20 and Parties source browser
9/9. Release/package suites pass 418/323/82; UI has one named existing compact
prerequisite skip, independently exercised by the forced browser negative.
Retained package browser controls pass 5/5. Builds report zero warnings/errors.
Pinned TypeScript/config checks and the warning/whitespace guards pass.

`final-verification.json` records final commands/results, named matrix controls,
actual source/package identities and binary/asset hashes. `raw-evidence.zip`
retains exact detailed JSON/XML/trace receipts and available raw logs, including
historical failures. `archive-entry-hashes.json` binds each archive entry by
SHA-256. Run `python3 verify-evidence.py` from this directory to verify the archive.

`run-check.py` validates retained pinned-tool mapping hashes before each source
check and records the actual executable assembly/PDB copies. `verify-binaries.py`
checks portable PDB document hashes against owning source bytes. Mapping
regeneration/replay uses the already tracked `build-source.py` helper from the
five-legacy-recovery batch; no handwritten source selectors or validator bypass
is used. The initial missing-object and stale-map failures remain historical; final source
checks use a freshly regenerated current mapping, with all exact generated
selector bytes archived under `provenance/current-map`.

The browser fixture uses verified Node 24.22.1 / npm 11.19.1 and locked Playwright
1.63.0. Producer packages labeled 4.7.1-preview.0 are local API-compatibility
artifacts against baseline 4.7.0; they were not published or adopted as released
packages. Package-mode controls continue to qualify the actually resolved 4.7.0
assemblies and retained behavior.

Rollback: leave the runtime option false. Source APIs remain additive, and
package consumers keep their existing freshness/live regions, reconciliation,
export helpers and typed erasure dialog. Final review, exact commit-message
validation, owning-main pushes and final gitlink adoption belong to the parent
workflow. Concurrent external commits are preserved and recorded, not treated
as owner acceptance.

Review corrected the evidence runner to give every execution an immutable log.
The archive retains 27 older superseded receipts whose full logs had already been
overwritten; their summaries and expected hashes remain, and the missing full
logs cannot be reconstructed. Every selected final check has its full matching
archived log. The verifier rejects missing final receipts, omitted matrix rows,
count mismatches (including zero), and corruption in ordinary and optimized
Python execution.

Independent review resolved callback-failure teardown, cleanup-timeout disposal,
typed-confirmation retry, external warning-description refresh and strict startup
and active-transfer cancellation coverage. Two existing transport gaps (early
confirmation delivery and later disconnection polling) were recorded separately
in the deferred ledger. The generic custom-download registration concern was
triaged as negligible for the actual opt-in host and left unchanged.

Published source: FrontComposer `c2b925bae1c9b3bfa8c3a4698ad0104de02ed5a4` and Parties
implementation `03fecb86e742055e87d8b7ff6ac40a12b73372ef`, both verified on owning main.
`publication.json` records exact readbacks and gitlink proof. All task execution
steps are complete; the named acceptance and package-delivery gates stay open.
