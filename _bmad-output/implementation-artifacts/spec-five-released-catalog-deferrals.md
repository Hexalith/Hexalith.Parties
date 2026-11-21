---
title: 'Qualify five deferred boundaries on the released catalog'
type: 'bugfix'
created: '2026-11-21'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
baseline_commit: '9b192aabf1a12982a3432ba827bf86b6a978fc5c'
context: ['docs/build-gate.md', 'docs/read-model-migration.md', 'tests/README.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

The five selected deferrals carry local evidence for earlier dependency cohorts. Qualify the equivalent boundaries on the repository's current released catalog and correct the backfill instructions to target the executable that owns the live tests.

- DW-50 — Verify processing generation recovery, bounded paging, and authorized export.
- DW-58 — Verify actor rollback data and canonical SDK state through the disposable provider rehearsal.
- DW-82 — Verify authoritative inventory reconciliation with missing or partial consumer mappings.
- DW-100 — Verify shared freshness and reconciliation with registered and fallback paths.
- DW-168 — Verify terminal-ingestion and deletion obligations through durable recovery.

Record executed coverage and any runtime limitations in the deferred-work ledger. Production cutover, writer drain, historical inventory, and named owner acceptance require their own evidence.

</frozen-after-approval>

## Implementation Notes

### Released package checks

The standalone Release build under .NET SDK 11.0.100 restored EventStore 5.4.0,
Memories 4.1.0, and FrontComposer 6.0.1 from the pinned catalog. The build
completed with zero warnings and errors.

| Executed test lane | Passed | Skipped |
| --- | ---: | ---: |
| Processing recovery, query, in-memory backfill, and Memories recovery | 1,070 | 0 |
| Current SDK projections | 466 | 0 |
| Parties UI | 465 | 0 |
| Disposable Redis 7.4.7 and Dapr 1.19.1 backfill | 9 | 0 |
| Backfill evidence-retention failure controls | 2 | 0 |
| Backfill workload children, nested inside the parent test | 12 | 0 |
| Owned Memories source-host wrapper | 1 | 0 |
| Authenticated Memories runtime child, nested inside the wrapper | 1 | 0 |

The focused core selections covered 39 generation recovery, 941 query, six
in-memory backfill, 18 Memories inventory, 27 REST recovery, 25 cleanup, and
14 cleanup regression cases. The UI run included 39 optimistic-reconciliation
cases, 22 live-region cases, 17 freshness-indicator cases and three real
SignalR transport cases.

The initial UI run used artifacts outside the repository, so 24 source-reading
controls could not locate the checkout. The successful rerun placed its
disposable job under the ignored `artifacts/j` directory. The current projection
run excludes the legacy inline SDK control, whose capability guard skips SDKs
that publish staged operations. The separate owned provider run confirms the
test processes exited and the Redis container was removed.

### Owned Memories runtime

The packaged gateway profile executed
`ActualCurrentSourceMemoriesRunsAuthorityAndPhysicalErasureControl` with
`PARTIES_MEMORIES_OWNED_RUNTIME=1` and the same prepared cohort manifest. The
Memories host came from the recorded source revision
`e413124b3cafe437c3e512d4b65114c09b0df0f3`. The wrapper and actual child passed.
Seventeen unit-authority observations and six native persistence/replay
observations covered scoped refusals, terminal ingestion, physical deletion,
and unrelated survivors. Owned topology disposal completed, the session
directory was removed, and no primary or cleanup failure was recorded.

The published Memories 4.1.0 client and contracts were selected for the
consumer. The producer execution above qualifies the source host. The built
producer at the 4.1.0 package's source revision
`0856785c7c764a0dc305af058227d6485b65c697` still needs a separate run for
package-matched producer qualification.

### Reproduction

Build `Hexalith.Parties.Standalone.slnx` in Release with
`--artifacts-path "$TMPDIR/artifacts"` through `run-with-temp`. Execute
`Hexalith.Parties.Tests.dll` with `-failSkips` and one `-class` selector for each
of `*PartyProcessingGenerationRecoveryTests`, `*PartySdkQueryHandlerTests`,
`*PartyLegacyReadModelBackfillTests`, `*PartyMemorySourceInventoryTests`,
`*PartyMemoryRestClientRecoveryTests`, `*PartyMemoryRecoveryTests` and
`*PartyMemoryRecoveryRegressionTests`. Build and run the UI assembly inside a
disposable job under the checkout. Create the ignored `artifacts/j` directory and
set `TMPDIR="$PWD/artifacts/j"` before invoking `run-with-temp`.
Run `Hexalith.Parties.Projections.Tests.dll` with
`-nomethod '*PublishedSdkPreservesLargeLegacyLiveAndRebuildCompatibilityAsync'`
for the staged SDK. Use the live command and provider requirements in
[the migration guide](../../docs/read-model-migration.md) for backfill.

Detailed XML, package provenance, assembly hashes and teardown evidence are
retained locally under `.git/agent-evidence/five-released-boundaries-20261121/`.

## Review Triage Log

- Fixed the backfill runbook's configuration path and parent XML output. The
  child runs from its own scratch directory. The command uses the workstation's
  `run-with-temp` helper and preserves evidence in the supplied results path.
- Split the nine provider cases, two local evidence-retention cases and twelve
  nested child executions in the results so their counts are independently clear.
- Documented where to build the UI DLL so its source-reading guards can locate
  the checkout.
- Retained separate dispositions for package consumer behavior, the owned
  source-hosted runtime, and the published producer.
