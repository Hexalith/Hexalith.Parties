---
title: 'DW-166 align Memories cleanup routes and qualify missing-unit responses'
type: 'bugfix'
created: '2026-10-18'
status: 'in-review'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** DW-166 records that Parties cleanup, indexing compensation and the cleanup health probe call the obsolete unversioned Memories route and accept any HTTP 404 as success. A routing failure can therefore discard cleanup mappings or hide a broken cleanup capability.

**Approach:** Use the existing Memories versioned route builder in all three paths. Recognize an already-missing unit only from a JSON 404 envelope carrying the exact MEMORY_UNIT_NOT_FOUND code; block generic, malformed, case-missing, tenant-missing and other error responses. Preserve the mapping's captured CaseId, authentication, endpoint prefixes, cancellation and success-status behavior. Use a validator-compatible synthetic health ID. Test the actual cleanup/health/compensation behavior, including retained mappings and bounded diagnostics. Update DW-166 with verified evidence.

</frozen-after-approval>

## Implementation Notes

- User selected the recommended DW-166 scope. No human-only intent gap remains; all edits are small and reversible. No real data, dependency, migration, external publication or deployment changes are needed. Separate terminal workflow completion, duplicate canonical IDs and mapping concurrency remain under their existing deferred work (DW-167/DW-168/DW-169).
- Baseline revision: d2ec85c9e2b85e2b0c7a50d92ffaf197590ea053. Tree clean on main. Authoritative Hexalith Git policy takes precedence over stale BMAD project-context branch advice.
- Ordinary Memories source confirms DELETE MemoriesRoutes.CaseMemoryUnit uses /api/v1, a missing case returns CASE_NOT_FOUND, and a scoped unit miss returns MEMORY_UNIT_NOT_FOUND. Reuse MemoriesRoutes.CaseMemoryUnitPath (escaped relative path), ErrorResponse and web JSON decoding. A qualified miss is scoped to the stored case; it is not proof of global or terminal physical erasure.
- Change src/Hexalith.Parties/Search/PartyMemoryCleanupService.cs and PartyMemoryIndexingService.cs; share only response classification in a focused internal helper. Keep each production type in its own file, moving the existing result records from these touched service files if needed. Revise the stale route comment in PartyMemoryUnitMappingStore.cs and document the scoped response contract in docs/memories-backed-party-search.md.
- Extend tests/Hexalith.Parties.Tests/Search/PartyMemoryCleanupServiceTests.cs and HealthChecks/MemoriesSearchHealthCheckTests.cs; add focused compensation/response regression coverage. Use fake handlers for cleanup and a local HTTP server for the internally constructed compensation client; test the requested verb/path/auth and bounded diagnostics without sending real requests.
- Validate the focused service test project in Debug/source mode with warnings-as-errors, direct xUnit class selection, then the full affected assembly. Preserve commands, summaries and hashes under tests/dw-166-versioned-memories-cleanup-2026-10-18/ alongside this spec. Check warning policy and whitespace. Complete the one-shot independent review and patch verified in-scope findings.
- Aspire baseline: exact Parties AppHost start succeeded, aspire wait parties --timeout 20 succeeded in 16.9 seconds, and resource state was inspected. Stop succeeded before edits. This is the Parties readiness baseline; optional Memories runtime/terminal-erasure qualification is not claimed.
- dotnet-inspect verified CaseMemoryUnitPath in the pinned Memories.Contracts 2.27.1 package. The installed inspector does not support --oneline, so inspection used its supported member output. An initial test build caught CA2007 in the local HTTP handler; corrected ConfigureAwait without weakening analyzers. Existing LF files retain their line endings to keep the diff focused; new production files use CRLF.
- Implemented the shared versioned route builder in cleanup, compensation and probing; the probe now uses a legal hyphenated synthetic ID. Shared response decoding accepts only success or a structured exact scoped-unit-missing response. Malformed JSON and invalid charset metadata block without exposing provider body text. Existing result records were moved intact into their own focused files.
- Initial regression run reproduced 27/52 failures before the call-path fix. After independent-review patches, focused tests pass 58/58 and the final Debug/source build has zero warnings/errors. Full assembly passes 1156/1157; the only failure is the unchanged, pre-existing Builds gitlink receipt assertion. Exact commands and baseline proof are retained in tests/dw-166-versioned-memories-cleanup-2026-10-18/validation.json.
- Qualification blocker: FinalDependencyReceiptsMatchTheSelectedPackageAndSourceGraph expects Builds 4f53d9f37c5b86cfd43fbcc8fa5dd4df19b84c77, while the starting commit already selects 544fd2e907d62ccdc4b8f24168b8ab5dfcc13eb9. The implementation does not change that test, source selection or historical receipts. Required Hexalith baseline prohibits story completion before all tests pass, so status remains in-review and DW-166 remains open despite the one-shot workflow's default finalization instruction. The code and verified evidence are committed for review; no push.

## Review Triage Log

- Medium, patched: invalid charset metadata throws InvalidOperationException during JSON decoding. Classify it as unaccepted and verify retained cleanup mappings and a failed cleanup probe. Cancellation is not caught.
- Low, patched: the qualified-404 success test did not assert persisted inventory removal. Added a direct empty-inventory assertion.
- Low, patched: single-mapping rejection tests did not verify partial progress. Added mixed successful/qualified-missing/unqualified-missing responses and assert only the blocked mapping remains with 2-of-3 progress.
- Low, patched: the new body-reading boundary lacked cancellation coverage. Added a direct asynchronous decoder cancellation test, cleanup/probe body-read cancellation with retained mappings, and compensation cancellation through an actual local HTTP response body.
- Low, completed during finalization: evidence and ledger updates were pending when review began. Final validation.json, README, commitlint evidence and ledger progress now record the actual outcome without claiming the full gate passed.
- Independent follow-up confirmed all four code/test findings are addressed and found no concrete regressions in those patches. The unrelated baseline dependency-receipt failure is recorded separately.
