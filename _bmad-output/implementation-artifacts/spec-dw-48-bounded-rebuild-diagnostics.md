---
title: 'Bound Party index rebuild drop diagnostics'
type: 'bugfix'
created: '2026-10-18'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Close DW-48 by replacing per-event drop logging during full Party index rebuild accumulation with bounded counts by the existing seven drop reasons. Carry counts in the serialized, operation-owned rebuild candidate and emit one privacy-safe structured summary after successful rebuild completion, regardless of the number of aggregates or dropped events. A failed accumulation must retain a bounded warning without exposing event names, formats, identifiers, payloads or exceptions. Preserve live-delivery diagnostics, event validation, checkpoint advancement, canonical index contents, erasure markers and search reconciliation. Empty or clean rebuilds need no drop summary; incomplete or canceled search reconciliation must not announce successful completion. Repeated successful completion invocations may repeat the single summary under the SDK's at-least-once completion contract. Retain compatibility with legacy raw-index candidates, whose historical diagnostic counts are unavailable. Keep unrelated logging categories and other deferred goals separate.

</frozen-after-approval>

## Implementation Notes

- No unresolved intent gaps or irreversible actions. Small footprint: the shared index handler and fold, two internal rebuild-state/count types, logging-generator dependency declaration if required, and focused tests in the existing projection test project. No public Contracts or canonical read-model schema changes.
- Baseline revision: `ad097dea4f98b4709d1f9e1e98e7d2993f2687f0`. Working tree was clean on `main` when scope was selected.
- Reuse `PartySdkProjectionFold.DeserializeNew` as the sole drop classifier. Its validation preflight stays silent; count only the actual fold, or one diagnostic walk on an unresolved failure, to avoid double counting. Use fixed counters without retaining source values or cross-operation handler state.
- Rebuild helpers in `PartyIndexSdkProjectionHandler` currently serialize the raw index. Introduce an internal envelope with compatibility reading; `FinalizeAsync` still writes only the canonical model. Completion logs only after all search reconciliation succeeds.
- Extend `PartySdkProjectionHandlerTests` using its request/event/identity helpers, `RecordingLogger<T>` and serialized `InMemoryReadModelBatchStore`. Cover large mixed histories, duplicate/checkpointed events, independent operations and recreated handlers, empty histories, legacy candidates, failed accumulation, reconciliation retry/cancellation, privacy, and unchanged persisted state/live diagnostics.
- Validation: Debug source-reference build of the projection test project, direct xUnit class selection followed by its full assembly, warning-override guard and whitespace checks. Record the required Aspire baseline separately from focused test acceptance.

- Implemented seven saturating counters and an internal candidate envelope. Legacy raw-index candidates resume with zero historical counts. The canonical write remains the unwrapped index. Successful completion emits a single structured event (4801), warning for unexpected corruption and information for expected redaction; failed accumulation emits one warning with `Completed=false`.
- The telemetry package sets `DisableMicrosoftExtensionsLoggingSourceGenerator=true`, and the root build removes its replacement. The projection project now explicitly retains the in-box generator and the centrally pinned logging package; no dependency versions changed. Existing tracked source line endings are retained to avoid unrelated full-file rewrites.
- Initial Debug/source build and focused SDK tests pass (135/135); the full projection assembly passes 325/325 without failures, errors or skips. Regressions compare persisted live/rebuilt canonical bytes and inspect exact structured field names/types for privacy.
- Aspire baseline started successfully; `aspire wait parties --status healthy --timeout 20 --non-interactive --apphost src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj` exited 17 after the timeout. Resource inspection showed services Waiting while security was Starting. The app was stopped successfully before compiling. No healthy full-topology qualification is claimed.

- Review corrections put optional diagnostics in the existing completion manifest, eliminating candidate/index deserialization at completion. Candidate format detection streams only root property names and uses case-insensitive envelope detection without a DOM; explicit null/partial envelopes reject instead of falling back to an empty legacy index. Legacy empty completion state and manifests without diagnostics retain their prior behavior.
- A final cancellation check suppresses successful completion logging even when a search provider returns true after canceling the caller. Sequence-gap and conflicting-duplicate failures now emit one bounded warning even with zero observed drop counts. Unresolved failure summaries describe the inspected prefix through the first unresolved event; the existing short-circuit and unchanged candidate bytes are preserved.
- Final independent follow-up verified all eight findings resolved, with no unresolved defect and no new deferred work. Expanded focused SDK tests pass 145/145 and the full projection assembly passes 335/335 without failures, errors or skips.

## Review Triage Log

- **Medium — patched:** Case-sensitive envelope detection could treat PascalCase envelope members as an empty legacy index. Streaming root-property detection and the configured case-insensitive deserializer now preserve the model; both casing variants have persisted-state coverage.
- **Medium — patched:** A search provider returning success after canceling the token could trigger a completed summary. A final cancellation check and a noncooperative-provider regression now suppress that report until a successful retry.
- **Medium — patched:** Completion recreated the full candidate index solely to read counters, including when no logger existed. Finalization now carries the seven counters in the optional completion-manifest field, and completion never reads the candidate index.
- **Low — patched:** Clean candidates failing sequence validation emitted no bounded warning. Sequence-gap and conflicting-duplicate regressions require one warning, unchanged candidate bytes and no persisted index.
- **Low — patched:** The ambiguous-type counter lacked a nonzero assertion. The existing resolver-cache injection helper now drives failed rebuild accumulation and checks exactly one privacy-safe warning.
- **Low — patched:** Operation isolation used different handlers. The regression now interleaves two candidates through one handler and verifies separate completed summaries.
- **Low — patched:** Saturation lacked serialized-boundary coverage. Both casing variants resume a maximum counter, record another drop, retain long.MaxValue and verify the emitted warning and persisted entries.
- **Medium — patched:** Follow-up found explicitly null envelope members still fell back to an empty index. Four serialized malformed shapes now reject in both accumulation and finalization, preserve original bytes and never persist an empty replacement. Independent final follow-up found no unresolved defect.

- Final post-review Debug/source builds have zero warnings/errors. Focused projection tests pass 145/145, full projection assembly 335/335, and full service assembly 1195/1195, without failures/errors/skips. The warning-override and whitespace gates pass. Exact commands, output summaries, runtime limitation and validated commit-message evidence are retained in `tests/dw-48-bounded-rebuild-diagnostics-2026-10-18/validation.json`.
