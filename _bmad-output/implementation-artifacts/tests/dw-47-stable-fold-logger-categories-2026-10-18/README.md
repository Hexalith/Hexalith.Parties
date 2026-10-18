# DW-47 stable projection-fold logger categories

Detail routes per-event diagnostics through the processing-activity fold category;
Index uses the SDK fold category. The bounded rebuild summary retains its Index
handler category. Existing diagnostic metadata, privacy, checkpoint and retry
behavior are preserved. Manual constructor and operator filter migration are
in docs/development-guide.md.

The initial validation.json records the 27-case implementation snapshot. After
independent review, final-validation.json records the final Debug/source build
with zero warnings/errors, 31 category cases and all 379 projection cases passing
without failures, errors, skips or not-run tests. XML output lists executed cases.
The original manifest's hashes describe its earlier snapshot; final hashes are
in final-validation.json. Captured build/test outputs normalize local paths to
repository-relative paths.

All three review layers completed. Edge Case Hunter and Verification Gap Reviewer
found no defects/gaps; Blind Hunter produced eight suggestions. Four small
filter/rebuild/configuration/evidence improvements were patched; four low-value
coverage/spec-history suggestions were rejected with evidence in the spec.
No new deferred work. DW-47 is closed.

The planning baseline is e55e9aaff1bdd67f13486193e651c8283f04ef73. Implementation
started at 5efaae7f0120439bc8d942bbc227dabdeefa56d8, preserving the separate
build-catalog commit. No dependencies, gitlinks, persisted models or SDK
registration changed for DW-47. The Aspire baseline only observed startup and
waiting services and was stopped; no healthy-topology qualification is claimed.

Commit message and pinned commitlint validation are retained beside this file.
The workflow creates a local commit and does not push.
