# Review corrections — 2026-10-07

The original owner continuation packet remains the pre-review 784-test Local / 115-test Platform snapshot. This separate folder records bounded review corrections only. No full Local verifier was rerun here; the parent owns the final full verification.

## Changes

- Normalize every validated command's SDK snapshot-aware JSON wrapper before protection; preserve identity rejection behavior and cancellation during identity-source validation.
- Reject a snapshot at checkpoint zero and any absent required positive-checkpoint snapshot, including JSON null/undefined after unprotection. Snapshot-provider errors propagate; no positive tail falls back to empty reconstruction.
- Require current-version `Unprotected` protection metadata with application JSON output. Validate and detach the entire tail before invoking a provider, with cancellation on each event and after byte capture. Give providers a separate copy so mutation cannot corrupt local destroyed-profile redaction or caller state.
- Correct the missing-snapshot test to contain only the valid sequence-2 tail at checkpoint 1/current 2; cover full serialized request binding, valid snapshot reconstruction, cancellation, provider output metadata and both failed/successful provider mutation. Add actual `/process` endpoint regressions and the three excluded-position SDK metadata variants.

## Focused verification

- Processor validation: 44 passing cases (15 additions). `final-processor-tests.xml` is the final focused result; `final-build-2.log` has zero warnings/errors.
- Retained-source reader: 39 passing cases (3 additions); `sdk-tests.xml`, `sdk-build.log` (zero warnings/errors).
- Endpoint class: 13 cases (4 additions) could not reach requests because the SDK startup route-inventory audit rejects the host. `final-tests.xml` / `final-tests.log` record all 44 processor cases passing plus those 13 endpoint startup failures. The audit was not disabled and production route metadata was not changed.
- Pre-fix `red-tests.xml`: 12 processor regression failures plus the same 13 endpoint startup failures. Three of the 15 new processor cases already passed (valid reconstruction, successful provider mutation and rejection of invalid serialized metadata).
- `red-build.log` and `red-build-2.log` preserve test-authoring compile failures (immutable metadata construction, then provider nullability); `red-build-3.log` is the successful regression build.

The startup blocker is exact: `/dapr/subscribe`, `/dapr/config` and the actor route templates lack `EventStoreSidecarChannel`; `/healthz` carries anonymous metadata outside the allowed probes. This fixture has no configuration seam to repair pre-mapped production route metadata. Actual endpoint behavior remains unqualified. The complete `DomainServiceRequest` wire round-trip is proved through the production processor in `ProcessAsync_SerializedLifecycleTail_UsesValidatedProtectionBoundary` instead.

Only the four paths in `review-source-hashes.json` are review-fix source ownership. Concurrent CI, FitnessTests, EventStore loader/planning and Platform work are excluded. The processor's CRLF and the Parties test files' LF endings are preserved.

## Commands

```sh
dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Debug --artifacts-path /tmp/parties54-review-fixes-artifacts -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:HexalithEventStoreRoot=/home/administrator/projects/hexalith/eventstore -p:HexalithCommonsRoot=/home/administrator/projects/hexalith/parties/references/Hexalith.Commons -p:HexalithMemoriesRoot=/tmp/parties54-optional-memories -p:NuGetAudit=false -m:1
dotnet /tmp/parties54-review-fixes-artifacts/bin/Hexalith.Parties.Tests/debug/Hexalith.Parties.Tests.dll -class '*PartyDomainProcessorValidationTests' -result-xml /tmp/parties54-review-final-processor-tests.xml
dotnet /tmp/parties54-review-fixes-artifacts/bin/Hexalith.Parties.Tests/debug/Hexalith.Parties.Tests.dll -class '*PartyDomainProcessorValidationTests' -class '*PartiesProcessEndpointTests' -result-xml /tmp/parties54-review-final-tests.xml
dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -c Debug --artifacts-path /tmp/parties54-review-fixes-security-artifacts -p:NuGetAudit=false -m:1
dotnet /tmp/parties54-review-fixes-security-artifacts/bin/Hexalith.EventStore.Server.Tests/debug/Hexalith.EventStore.Server.Tests.dll -class '*RetainedIdentityHistorySourceReaderTests' -result-xml /tmp/parties54-review-sdk-tests.xml
```
