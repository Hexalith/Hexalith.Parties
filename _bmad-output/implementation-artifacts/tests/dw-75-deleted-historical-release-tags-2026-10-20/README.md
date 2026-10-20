# DW-75 validation — 2026-10-20

The unchanged publication verifier fails eight new regression cases when a
published release has a deleted tag. The patched verifier skips only explicit
HTTP 404 responses from tag-ref lookup and still requires a valid tag resolving
to the exact dispatched source. Both lightweight and annotated tags, mixed
CRLF/LF response framing, pagination, draft exclusion and encoded names are
exercised. Non-404 errors, transport failure, misleading 404 text, missing-only
releases and malformed ref/tag evidence remain failures.

The Debug/source CI project build has zero warnings/errors. All 36 focused
cases and all 184 tests in the complete affected assembly pass without skips,
errors, failures or not-run cases. Workflow lint and repository guards pass.
The API is represented by a temporary local gh executable; no release is
published by these checks. See validation.json for exact commands, counts,
source hashes and pinned commitlint evidence.

All five independent review findings were patched and follow-up verified.
No new deferred work. The spec records each verdict and correction.
