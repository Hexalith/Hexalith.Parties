---
title: 'DW-194 preserve destroyed-key errors during marker failure'
type: 'bugfix'
created: '2026-10-18'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** When protection finds no current party key, it makes a best-effort crypto-pending marker write and throws `PartyEncryptionKeyDestroyedException`. Its outer catch writes that marker again without protection, so persistent marker-storage failure replaces the typed destroyed-key outcome and prevents callers from identifying it reliably (DW-194).

**Approach:** Preserve exact destroyed-key failures across current-key acquisition, retaining the existing best-effort marker attempt for missing keys. Cover public event and snapshot protection with persistent marker faults and negative controls; preserve healthy encryption, unrelated failure types, cancellation and bounded diagnostics. Resolve only DW-194 and its corresponding retrospective action after passing validation and independent review.

</frozen-after-approval>

## Implementation Notes

- Selected by the user on 2026-10-18. No unresolved intent gap or irreversible operation. Small footprint: one private catch filter, one focused security-test class, this spec, DW-194 ledger entry, matching sprint retrospective action and sanitized validation evidence. No new public API, dependencies, gitlinks, schema or persistence mechanism.
- Code map: `src/Hexalith.Parties.Security/PartyPayloadProtectionService.cs`, `GetCurrentKeyAsync` (no-version branch and outer catch). Reuse `PartyEncryptionKeyDestroyedException.IsMatch` to bypass marker retries for exact destroyed-key errors; keep other catch behavior. Both `ProtectEventPayloadAsync` and `ProtectProfileSnapshotStateAsync` use this helper. `UnprotectNodeAsync` uses direct version retrieval and is unchanged; do not claim a new unprotect integration path.
- Tests: add `tests/Hexalith.Parties.Security.Tests/PartyPayloadProtectionMarkerFailureTests.cs`. Reuse real `PartyKeyLifecycleService`, substituted `IPartyKeyRetryScheduler`, existing `CapturingLogger<T>`, real personal-data event/state contracts and `PartiesJsonOptions.Default`. Given no key and a persistently failing marker, when public event/snapshot protection runs, then the exact destroyed-key type and tenant/party identity escape without returning plaintext. Cover no-key creation and noncreation events, marker success, typed key-fetch failure, transient/raw failure controls, caller cancellation and safe logging. Build the regression before production change to reproduce the failure.
- Validation: record required Aspire baseline and stop the owned topology before builds. Use Debug source references with `dotnet build tests/Hexalith.Parties.Security.Tests/Hexalith.Parties.Security.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -m:1`; invoke built xUnit v3 assembly with `-class Hexalith.Parties.Security.Tests.PartyPayloadProtectionMarkerFailureTests`, then the full security assembly. Run warning-policy and CRLF-aware whitespace checks. Store exact commands/results under `tests/dw-194-destroyed-key-marker-fault-2026-10-18/` beside this spec. Runtime-topology limitations remain separate from focused verification.

- Implementation: the outer current-key catch now excludes exact destroyed-key errors using the existing `IsMatch` predicate. The missing-version branch still makes one best-effort marker attempt; a typed version-read failure propagates unchanged without a marker retry. Other key failures retain the previous generic catch behavior.
- Validation before review: corrected helper CA2007 annotations and an overly strict cancellation exception identity assertion before the final red run. With original production source, 8/16 regressions fail (three duplicate-marker cases and five masked-error cases); after the filter, 16/16 pass. Full Security.Tests passes 234/234 with no failures/errors/skips/not-run. Debug/source builds have zero warnings/errors.
- Runtime baseline: initial five-second security wait expired, then the topology reached Healthy (including Parties, gateway, Tenants and sidecars). Parties healthy wait passed; sanitized resource state is retained. Owned topology stopped successfully before compilation. This is baseline observation, not an authenticated end-to-end proof.

- Independent blind review returned six verified improvements. The final catch now preserves caller-requested cancellation in both marker scopes, skips only the redundant destroyed-key marker retry and retains type-only logging. The test matrix adds pending-key retry, marker-write cancellation and a real PartyState folded from PartyCreated. Red assertion details and exact pre-review test bytes are retained; all new JSON follows CRLF policy. A test-only PartyState initializer compile error was corrected to the existing Apply API before the successful final build.

## Review Triage Log

- [medium; patch] Caller cancellation during the inner marker write was swallowed, and the initial filter bypassed the old second attempt. Both catches now let caller-requested cancellation escape; event/snapshot marker-cancellation cases require the supplied token and exactly one attempt. Key-read cancellation also forbids marker writes.
- [low; patch] The initial filter removed the pre-existing type-only diagnostic for typed version-read failures. Keep the catch and diagnostic, guard only marker retries with IsMatch; assert the diagnostic without provider-message leakage.
- [low; patch] RetryPendingKeyCreationAsync lacked fault-matrix coverage. Pending event/snapshot cases run the real lifecycle retry and require one creation, one clear and one final marker attempt while preserving the typed outcome.
- [low; patch] Snapshot cases used an event object. Replace it with a real PartyState folded from a personal-data PartyCreated event.
- [low; patch] Red classifications lacked retained assertion text. Record each actual assertion/exception type and preserve the exact red-test source; Git baseline production bytes match the recorded red hash.
- [low; patch] JSON evidence used LF and was untracked during git diff whitespace validation. Normalize all new text artifacts to CRLF and explicitly validate tracked and new artifact bytes.

Post-review Debug/source build passes with zero warnings/errors. Expanded fault matrix passes 20/20; full Security.Tests passes 238/238 without failures/errors/skips/not-run. The executable evidence verifier checks final source hashes, canonical Git baseline production bytes, exact retained red-test bytes and CRLF/whitespace in changed code/new artifacts. Tracked ledger edits use the separate CRLF-aware diff check. No unrelated whitespace is changed.

Independent targeted follow-up verified all six review corrections and found no new concrete defect. No review layer was skipped and no finding is deferred. Closed only DW-194 and its matching retrospective action. Final evidence verifier, warning-policy check and CRLF-aware diff check pass; pinned commitlint 21.2.2 validates the exact local commit candidate without problems or warnings. The one-shot workflow requires a local commit; no push is performed.
