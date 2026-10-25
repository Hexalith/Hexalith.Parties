# Preservation and future qualification

**Review-only plan.** No runtime or owner qualification runs in this task. These are future requirements if the complete amendment/targets are accepted; they confer no execution authority.

| Area | Required evidence under the candidate amendment |
| --- | --- |
| Immutable policy | Original +31,536,000 seconds; offset-equivalent instants, leap years, overflow, immediately before/at/after E; no per-action, erasure, retry, revoke, rotation, successor or restore reset. |
| Service boundary | Record current scope/authority/time at admit/decrypt/use/copy/handoff; delay across E, withdraw authority during awaits, pause between check and handoff, deliver already-sent output after E and verify receiver refusal. Distinguish authorization time from physical output time. |
| Time/authority faults | 100-ms uncertainty and 1-second observation bounds; uncertainty at boundary, rollback, stopped clocks, unavailable authority, stale signatures/fences, old incarnation and restored authority cuts all fail closed. No cached permit hides revocation. |
| Destruction | Independently inspect every online generation by E+900 s and every C01–C16 durable/key copy by E+2,592,000 s. Test backup/escrow/shared-wrap paths, locked storage and provider recovery copies. A released unresolved DEK forbids claiming retained ciphertext is permanently unreadable; require verified DEK retirement or physical durable-ciphertext eradication. Verify no live successor/tenant is lost. Unknown evidence cannot pass. |
| Failure and receipts | Outages through either bound yield retained breaches and Pending; connectivity never resets the clock. Lost acknowledgements/restarts/concurrent retries recover one operation and final original receipt; no late-copy race bypasses the frozen inventory. Evidence due actual completion+900 s, with the stated absolute ceiling. |
| Holders | Inventory both hosts and all consumer/runtime/serializer/transport holders. Exercise runnable 5-second lifetime, 1-second owned-buffer cleanup, pause/VM suspend/crash/late completion and resume-before-use gating. Show dump/swap/hibernate/snapshot controls; inspect accidental spill handling without writing actor plaintext/DEKs into test evidence. Disclose residual inaccessible memory; never convert it to a passing physical-erasure result. |
| Restore | Genuine pre-expiry data/key/lifecycle cuts restored into quarantine both within and after the destruction window; current independent authority blocks expired use. After the durable-copy bound require irreversible recovery failure, not merely service denial. Positive live controls and old-writer fences remain mandatory. |
| F1 | Expired X cannot be read even during its destruction window; live Y in the same Party and another tenant resolves with complete actor-free continuity. Reject gaps/overlaps/tampering/stale/foreign proofs and expired predecessor references/hashes. Preserve schema/issuer/erasure decisions. |
| Compatibility | Old strict consumers reject amended assurance; every provider/host/SDK/cleanup/verifier agrees on revision, states and receipt scope. No reuse of strict Boolean flags as weaker assurance. |
| Full identity/F2 | P-01 exact/concurrent/lost-ack provisioning; P-02 divergent intent conflicts; P-03 rejection-only replay; P-04 tenant/service isolation; P-05 immutable ID/type mismatch; P-06 liveness/freshness failures; P-07 human/actor matching; P-08 bind/rebind/rotation historical correctness; P-09 gaps/overlap/stale/cross-tenant history; P-10 stable-actor approver exclusions/revocation. Keep P1–P4 and all consuming commitments. |
| Complete acceptance | Clean exact source/package/image/install/profile/configuration/plan/trust guards, real B15 pass path, independent persisted assertions, no failed/skipped/unrun lanes. Local/ReadinessOnly evidence stays local. Available requires accepted exact installed Level 4 owner qualification; Level 5 remains consumer/RQ-1. |

Shortened synthetic intervals may exercise boundaries on an isolated owner-approved target; separately verify actual production 365-day configuration and destruction bounds. Finite passing tests do not establish universal physical guarantees. The independent assurance case must identify failure assumptions and evidence for the actual backend, copies, clocks and holders.

The October 25 reconciliation remains effective: zero complete changed commitments ready; four Story 5.4 dependencies Committed and unavailable, protection Uncommitted. Preserve OQ-5.4-1, 192.168.1.30, trusted-envelope bounds 300/30/600/86400/604800, independent approval authority and Dapr-only custody. This amendment’s proposed numeric bounds are actor-history-specific and do not replace those envelope bounds. Keep the retained qualification order and all historical failures. No original attempt/journal may be reset.

Static validation for this draft checks exact Before quotations, local companion links, unique CAP IDs with intent/success, full Q1–Q7/D01–D04/F1/F2/B01–B15 mapping, unapproved labels and protected file/index bytes. Results and precise scope are in validation-final.json; validation.json preserves the initial drift-detecting result. All named governing sources match their captured hashes. Concurrent Git activity changed the index and five pre-existing files (including removal of three probe files); it also committed draft files while this task was in progress. This task issued no Git mutation and did not restore or overwrite that concurrent work. Global workspace preservation is therefore not claimed. This is document validation, not backend or installed qualification.

## Build review — 2026-10-25

The preceding account and original validation reports describe the drafting
session. The separate [build receipt](validation-build.json) checks this review
packet from Parties HEAD `9ee857b11ebcd13d44644be7c04c4ea6da5fdc44`.
The blank line separating the owner-decision table header from its rows was
removed; the five decisions now render as table rows. Their text is unchanged.

The build checks rendered table structure with `markdown-it-py` in CommonMark
mode with tables enabled, exact Before excerpts with line endings normalized,
all sixteen source SHA-256 values, local links and all twenty-eight crosswalk
IDs. It also records the unchanged proposal status, ledger fields and original
receipts. The receipt identifies the checked packet bytes and preservation
scope. Independent document review supplements these mechanical checks; neither
establishes backend feasibility, owner acceptance or installed qualification.

The [archived checker and baseline](validation-build-support.zip) retain the
full validation code and captured preservation hashes. Run its `validate.py`
from the Parties repository root after extracting both files into a temporary
directory. It emits a new JSON report to stdout and writes no files. Revision
and index preservation checks compare the original pre-commit baseline and
deliberately fail after that baseline changes; they are historical observations,
not a reusable CI gate. Source hashes and recorded sibling repository revisions
identify the exact governing inputs, including whether they match committed
bytes. Unrelated sibling worktree changes are outside that source claim.

The checker requires Python 3.9+, `markdown-it-py==3.0.0` and `PyYAML==6.0.3`.
For example, run it in an isolated environment with
`uv run --with markdown-it-py==3.0.0 --with PyYAML==6.0.3 python /path/to/extracted/validate.py`.
The table-only command below needs `markdown-it-py`; the same `uv run` prefix
can replace `python3` when that package is absent. Run it from the repository
root:

```bash
python3 - <<'PY'
from pathlib import Path
from markdown_it import MarkdownIt

path = Path('_bmad-output/specs/spec-ext-parties-1-contract-amendment/owner-decisions.md')
tokens = MarkdownIt('commonmark').enable('table').parse(path.read_text(encoding='utf-8'))
table = 0
rows = []
for token in tokens:
    if token.type == 'table_open':
        table += 1
    elif token.type == 'table_close' and table == 1:
        break
    elif table == 1 and token.type == 'tr_open':
        rows.append([])
    elif table == 1 and token.type == 'inline':
        rows[-1].append(token.content)
assert [row[0] for row in rows] == ['No.', '1', '2', '3', '4', '5'], rows
assert rows[0] == ['No.', 'Owner decision', 'Accountable roles', 'Exact unresolved decision and consequence', 'Original mapping'], rows[0]
assert all(len(row) == 5 and all(cell.strip() for cell in row) for row in rows), rows
print('PASS: header and five owner decisions, each with five cells')
PY
```

Expected: `PASS: header and five owner decisions, each with five cells` and
exit 0. Missing rows, wrong IDs or empty cells raise an assertion failure. This focused
command does not regenerate the build receipt or repeat its other checks.
All five decisions remain unresolved and the amendment remains
`proposed-unapproved`.
