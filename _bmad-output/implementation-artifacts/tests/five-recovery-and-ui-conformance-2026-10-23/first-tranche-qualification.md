# Five recovery and UI conformance: first implementation tranche

Spec: [five recovery and UI conformance](../../spec-five-recovery-and-ui-conformance-deferred-items.md).
Baseline: `08f43497b3f916efa0cdf9b110258f763d894bd9`.
Selected Memories contract source: `b55db82c060caf917a142fdb6585fa7340b01647`.

This receipt covers committed erasure lifecycle, complete live search retry content,
and superseded workflow retention qualification. Worker containment and UI conformance
remain for sequential implementation; the spec and unrelated ledger gates are unchanged.

## Package/Release verification

Each project was built individually with `dotnet build <project> -c Release -m:1`.
Both builds exited 0 with zero warnings/errors; see the two build logs.

```text
dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Release -m:1
dotnet build tests/Hexalith.Parties.Projections.Tests/Hexalith.Parties.Projections.Tests.csproj -c Release -m:1

dotnet tests/Hexalith.Parties.Tests/bin/Release/net10.0/Hexalith.Parties.Tests.dll -class '*PartyDomainProcessorValidationTests' -class '*PartyDomainEventPublicationContractTests' -class '*PartySdkQueryHandlerTests' -class '*PartyMemoryCoordinationTests' -class '*PartyMemoryCleanupServiceTests' -class '*PartyMemoryRecoveryTests' -class '*PartyDetailSdkProjectionRecoveryTests'
# Exit 0: 1155 passed, 0 failed/errors/skipped/not-run

dotnet tests/Hexalith.Parties.Projections.Tests/bin/Release/net10.0/Hexalith.Parties.Projections.Tests.dll
# Exit 0: 447 passed, 0 failed/errors/skipped/not-run
```

## Matrix evidence

| Scenario | Production-path regression |
| --- | --- |
| Failed/canceled append | `RetryErasureAppendFailureOrCancellationCannotPromoteQueryUntilCommittedProjection`: real processor result, rejected/canceled append, serialized projection and recreated query stay KeyDestroyed; successful append alone still cannot promote the projected query |
| Successful retry/legacy status | Same connected regression observes Erased only after committed projection and preserves it on duplicate delivery; `ErasureStatusHandlerIgnoresLegacyStatusAuthorityAsync` covers optimistic and regressive legacy records |
| Privacy cleanup/older models | `CleanupTombstonePreservesCommittedLifecycleAndReplayBackfillsHistoricalRowsAsync` proves privacy fences cannot promote lifecycle and same-checkpoint complete replay persists legacy metadata; empty-v1 recovery fingerprints pass unchanged |
| First ingestion retry | `FirstSuccessfulSubmissionAfterRestartIncludesAllContactAndIdentifierContent` uses real adapters over serialized SDK state and observes complete outgoing content after rejection/restart; `SearchRetryRestoresAllIgnoredContentAndRejectsChangedOrPartialReplayAsync` rejects stale, partial and same-checkpoint altered ignored values |
| Concurrency/erasure | Projection contention tests require the newer complete replay; existing erasure interleavings pass. Digest removal is asserted for direct event erasure, SDK cleanup and rebuild-preserved cleanup fences |
| Superseded workflow | `SupersededAcknowledgedAttemptSurvivesCurrentRevisionAndRestartUntilTerminalCleanup` and `MissingExpiredOrUnavailableSupersededStatusRetainsOnlyItsUncertainObligation` retain the older uncertain obligation while newer work settles, then clean only with matching successful terminal evidence |

## Scope and limits

- Lifecycle metadata is additive in the existing aggregate-owned detail slot; null metadata is omitted to preserve canonical empty initialization bytes. Legacy status records and pre-append privacy tombstones have no lifecycle authority. Older rows without evidence remain unknown until complete committed replay backfills them.
- Complete live search replay is compared to canonical sequence, visible content, available event descriptor and optional new SHA-256 search-content digest. Historical rows lack the stronger digest witness. Raw ignored collections are never persisted in recovery manifests. Erasure removes each party's digest while preserving unrelated entries.
- Shared rebuild completion still lacks a complete ignored-content replay source. Its existing behavior is preserved; this tranche qualifies live/retry content, as approved. It does not qualify that separate existing limitation.
- Selected Memories documentation retains completed workflow history until explicit purge and adds no purge feature. Missing and hypothetical expired responses are injected consumer failures, not deployed-retention proof. No production cleanup change was necessary; no physical-erasure, authoritative inventory or owner signoff claim is made.
- The pre-code Aspire check was missed. A later current run started but security was Unhealthy, with dependent resources Waiting/NotStarted; the exact AppHost was stopped. See sanitized resource-state and qualification receipts.
- Debug/source mappings, the full service test assembly, process/UI tests, browser checks, ledger completion, commitlint, reconciliation and push remain for the parent continuation. No dependencies, gitlinks, public wire values or owner signoffs changed.
