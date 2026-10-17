---
title: 'DW-49 projection converter failure handling'
type: 'bugfix'
created: '2026-10-17'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `PartySdkProjectionFold.DeserializeNew` handles malformed JSON but lets `FormatException` and `OverflowException` from a JSON converter escape and abort projection dispatch. DW-49 requests consistent handling of those conversion failures.

**Approach:** Include these two exception types in the existing corrupt-payload catch filter. Preserve skip-and-advance behavior, distinct live/redacted log levels, exception-type-only diagnostics, and all other exceptions' existing propagation. Exercise actual System.Text.Json converter calls, continuation to valid events, checkpoint filtering, processing audit outcomes, and non-echoing diagnostics. Complete only DW-49; DW-163 remains open with its independently verified SDK recovery blocker.

</frozen-after-approval>

## Implementation Notes

Planning classification: no unresolved intent gaps, irreversible operations, migrations, dependency updates or public production API changes. Small footprint: two exception-filter alternatives in the existing fold and isolated converter test types in the existing projection test project, plus this spec and the ledger. Use the oneshot route.

The contract assembly currently has no custom event converters throwing these exceptions; this implements the explicit deferred resilience requirement. The tests must use the real JSON deserializer and converter. Reuse the existing test-only reflection pattern for resolver-cache fault injection under a unique synthetic event name, avoiding changes to the allowlist or shared canonical serializer options. Keep one type per file. No runtime test seam or new production API is needed.

Build the individual Projections.Tests project in Debug/source mode, demonstrate failing-before conversion cases, then run its complete assembly and the service assembly after rebuilding it against the final source. Preserve privacy by logging only exception type names, with no exception objects or converter messages. Keep changes uncommitted under the user's repository safeguards.

Implemented the two catch-filter alternatives without changing serializer configuration or type resolution. Eight focused cases use a synthetic converter through the actual serializer: six conversion/audit cases failed before the change; checkpoint filtering and unrelated-exception controls already passed. The complete Projections.Tests assembly now passes 246/246 with zero failures/errors/skips, and its Debug/source build has zero warnings/errors. Final service verification and independent review are pending.

Review follow-up adds handler-level persisted read-back for both exception types with and without a valid tail, and redacted processing-audit outcomes. Synthetic resolver registrations are removed by fixture disposal, including failed test assertions and the explicit using scope in handler tests. The final projection suite passes 252/252; the rebuilt final service suite passes 1027/1027. Both Debug/source builds have zero warnings/errors. No tests are skipped. Warning-override and whitespace checks pass. No dependency, gitlink, runtime configuration, commit or push changed.

## Review Triage Log

- Low, patch: fold-only audit tests did not establish completed handler dispatch and persisted checkpoints. Added four real-handler/stateful-store cases covering each exception alone and before a valid event; read back both serialized slots.
- Low, patch: redacted converter failures lacked audit assertions. Both exceptions now produce Redacted processing records, advance the checkpoint and continue to a successful event.
- Low, patch: synthetic resolver entries survived each test. The fixture now disposes only its own unique registrations, including failure cleanup; handler tests use an explicit using scope.
- Low, patch: the DW-163 SDK blocker depended on temporary evidence. A sanitized portable reproduction patch and instructions now preserve the exact source identities, deterministic failure phase and observed/expected persisted state; patch applicability is checked.
- Low, patch: historical DW-163 notes read as a current implementation. A prominent withdrawn/current-outcome notice and explicit design correction distinguish historical attempts from the remaining requirements. Its frozen intent is unchanged and its status remains draft.

Independent follow-up verified all five review corrections; 14/14 focused cases and patch applicability pass, with no concrete remaining defect in scope. DW-49 is complete. The separate DW-163 investigation remains draft/open and its unsafe candidate is not retained.
