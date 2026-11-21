---
title: 'Reproduce five released-catalog deferred-work checks'
type: 'feature'
created: '2026-11-21'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
baseline_commit: '1ee5b49a416c2325bac3e6050bc088abaa74cab4'
context: ['docs/build-gate.md', 'docs/read-model-migration.md', 'tests/README.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Make the released-package checks for DW-50 processing pagination, DW-58 actor backfill, DW-82 Memories inventory, DW-100 UI reconciliation, and DW-168 terminal cleanup repeatable from one command. Reuse the current package catalog and existing .NET suites. Keep logs, per-test reports, and the tested package and assembly identities after scratch cleanup. Fail if a required test class has no passing tests. Exercise the remaining producer qualification against the source revision identified by the released Memories 4.1.0 packages in a disposable checkout. Record what the executed checks establish and the evidence still required to close each entry.

</frozen-after-approval>

## Implementation Notes

### Released package checks

`scripts/verify-released-catalog-deferrals.sh` builds in a disposable directory
beneath the checkout so UI tests can find their source fixtures. It retains
reports, build logs, the SDK version, assembly hashes and the resolved runtime
dependency manifests in a new results directory.

| Lane | Passed | Skipped |
| --- | ---: | ---: |
| Processing, backfill and Memories consumer checks | 1,070 | 0 |
| Current staged SDK projections | 466 | 0 |
| UI freshness, reconciliation and composition | 465 | 0 |

The .NET 11.0.100 standalone Release build passed with zero warnings and
errors. The runner used the released EventStore 5.4.0, Memories 4.1.0 and
FrontComposer 6.0.1 dependencies. A simulated test runner that omitted the
inventory class produced a failing exit and no success summary. A reused
results directory was refused and its summary remained intact. A rerun
with `HexalithFrontComposerVersion=6.0.0` in the caller environment still
resolved the checkout's default FrontComposer 6.0.1 and passed the same tests.

### Owned runtime producer

The two Memories 4.1.0 package archives name source revision
`0856785c7c764a0dc305af058227d6485b65c697`. A disposable Parties checkout
selected that source for the Memories server and domain service. Its other
root-declared dependencies retained the Parties baseline revisions. The first
attempt built the runtime but failed before startup because its assembly output
was outside the checkout. The documented command builds within the disposable
repository and binds the test repository root explicitly.

With the checkout corrected, the package-matched producer started. Its
non-generation fixture reached `source-publication-legacy-owner-reconciliation-required`
during refresh and case deletion. The child failed and the fixture disposed
the owned topology. The release source supports the existing ownership-generation
profile, which binds synthetic writes to the disposable producer's own generation.

The generation-scoped wrapper and authenticated child each passed one test
with zero skips. The run observed two canonical outputs, 17 unit-authority
checks and six persisted authoritative-stream and replay checks. Delayed
ingestion and terminal deletion were exercised with synthetic records.
Unrelated records survived. The owned topology and session directory were
removed, and the before/after container inventories matched.

The five entries retain their production gates in the ledger. Detailed local
evidence is under `.git/agent-evidence/released-catalog-followup-20261121/`.

## Review Triage Log

- Medium. An inherited family version could select a different cohort.
  The runner clears the catalog selectors, and the older-version rerun
  resolved FrontComposer 6.0.1.
- Medium. The initial runbook lacked the matched producer invocation.
  The guide now names the release revision, preparation profile, test
  selector, and evidence to retain.
- Low. The initial draft lacked executed results. This spec and the ledger
  record the verification outcomes and remaining owner evidence.
