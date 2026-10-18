# DW-172 validation

The initial regression matrix reproduced the disclosure against the unchanged
baseline service: 16 of 20 cases failed. Final parent verification after review
fixes passed the Debug build with zero warnings/errors, 28 cross-instance cases,
98 query-handler cases, and all 1,284 host cases without skips.

The regression uses independent host caches over the SDK's serialized in-memory
read-model store and the real coordinated eraser. Persisted detail/index/processing
tombstones and retained Article 30 records are read back after cleanup. Coverage
includes detail aliases, export, processing, list/search, ordinary outages,
defensive erased-detail redaction, timestamps, empty/absent safe fallback,
delayed candidates, unaffected tenants, saturation, cancellation, and recovery.
Privacy assertions inspect serialized response payloads as well as the envelope.

`validation.json` records exact commands, results, and final source/assembly
SHA-256 hashes. Its executable whitespace check is retained as
`check-whitespace.py`; run the exact repository-root command in the JSON evidence.
Routine logs are local at
`/tmp/parties-dw172-validation-ff_is1pq`.

The Aspire baseline started successfully but its security resource remained
unhealthy, leaving Parties waiting. The owned topology was stopped before builds.
That environment limitation is separate from the passing SDK/host evidence.

All three independent review layers completed. Review corrections include null-index
rejection, stronger privacy/isolation checks, delayed-task cleanup, maintained query
documentation and the executable whitespace check. Final parent verification passed
at the recorded source/assembly hashes; no deferred work was added.
Canonical-read consistency and other erasure races remain outside scope.

Final routine logs are local at `/tmp/dw172-parent-final-pbvq6hvf`.
