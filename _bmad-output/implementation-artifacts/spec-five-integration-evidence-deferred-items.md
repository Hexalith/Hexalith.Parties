---
title: Close five integration evidence reader deferrals
type: bugfix
created: 2026-11-10
status: done
route: oneshot
review_loop_iteration: 1
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Complete the five related unnumbered BH3–BH7 deferrals from
`spec-qualify-eventstore-4.0.4-for-mcpcli.md`: bind native TRX timing and
execution identities to the current owner observation, require private
selector compatibility, enforce integer counts/sequences, constrain public
observation times to the issued execution interval, and enforce private
loaded/copied assembly scopes. Preserve exact source, hash, native case,
cleanup and profile checks. Use existing released execution deadline and
terminal fields; do not introduce a producer contract or grant runtime,
deployment, schema-completeness or qualification credit.

Add regression controls to the existing public/private Python fixtures,
including resealed malformed evidence and valid real-reporter shapes. Mark
only these five ledger entries completed after verification and review.
Commit the result and push to main as requested.

</frozen-after-approval>

## Implementation Notes

- Investigation confirmed all five defects in the two verifier scripts.
  Baseline integration-reader discovery passes 52 tests. Boolean/float
  sequence values, private false selector proof, mislabeled private scopes,
  and public observations in 1900/2999 are currently accepted.
- This is a small, reversible four-code-file change, with no intent gaps,
  dependency updates or external mutations beyond the authorized Git push.
- Reuse `verify-current-owner-integration.py` readers from the public script.
  The released Builds writer records `bindings.deadlines.execution.originAt`
  and `terminal.recordedAt`; actual MTP TRX supplies Times, case start/end,
  test/execution IDs and matching definitions. TRX IDs identify reporter
  executions, not domain identifiers or the module owner run ID.
- Verification: Python integration-reader discovery, adversarial mutations,
  retained actual TRX compatibility, independent blind review and exact
  pinned commitlint validation before committing.

- Implemented all five reader corrections and ten additional regression tests.
  All 80 Python script tests pass (62 integration-reader controls).
- The unchanged `module-integration-diagnostics` artifact downloaded from hosted
  CI run 42690523916 passes the updated public CLI with all 28 native cases,
  exact original source/context and qualification credit still false.
  `tests/five-integration-evidence-2026-11-10/` retains commands and hashes.

- Blind review found five corrections; all were patched and independently
  rechecked. Comparisons retain nine fractional digits, malformed timing
  objects fail deliberately, deadline durations match expiry, native finish
  observes expiry while terminal recording may follow, and measurements
  occur inside the persistence case. The follow-up reviewer passed 65
  integration-reader controls and found no remaining defects in the fixes.

## Review Triage Log

- R1 — low, patch: Python datetime truncated the seventh fractional digit.
  Integer nanosecond comparisons now retain all supported precision;
  100 ns observation and 1 ns image-order violations are refused.
- R2 — medium, patch: new timing reads called `.get()` on malformed objects.
  Explicit dictionary checks now raise ValueError before field access;
  both full readers exercise null/list/string/boolean/number controls.
- R3 — medium, patch: contradictory duration/expiry and undeclared deadline
  fields were accepted. Exact positive finite duration/expiry agreement and
  mutually consistent deadline states now pass targeted refusal controls.
- R4 — medium, patch: terminal recording was incorrectly bounded by child
  expiry. The pinned executor separately processes reports after child exit;
  the deadline now bounds native finish and later successful recording passes.
- R5 — medium, patch: measurement could precede the persistence case if the
  image had started earlier. The returned lower bound is now the actual
  persistence case start; an earlier running image no longer masks the error.

All five findings are resolved. No review findings were deferred.

Final verification: all 83 Python script tests pass, including 65 reader
controls; the unchanged real hosted artifact still passes after review fixes.
The pinned commitlint 21.2.4 validates the exact commit message with zero
problems or warnings.
