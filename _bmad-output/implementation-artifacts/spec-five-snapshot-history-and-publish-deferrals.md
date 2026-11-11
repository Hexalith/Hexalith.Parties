---
title: 'Qualify five snapshot, history and publish deferrals'
type: 'bugfix'
created: '2026-11-11'
status: 'done'
route: 'dispatch'
review_loop_iteration: 1
baseline_commit: '6bba88dd85362555edf9e4672deb68811e73895f'
context: ['AGENTS.md', 'tests/README.md', 'docs/read-model-migration.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Five unresolved ledger boundaries lack sufficient runtime evidence: blocked snapshot I/O, purged Memories history, concurrent historical scheduling, backfill workload restart, and sidecar secret publication.

**Approach:** Reproduce each boundary, correct demonstrated defects in the owning technical module, verify retained behavior, and publish reviewed changes on main. Retain external blockers.

## Boundaries & Constraints

**Always:** Use owned disposable files, processes, tenants and services. Inspect actual persisted artifacts and target output. Preserve cancellation, cleanup, tenant isolation, existing lost-ack recovery and unrelated survivors. Distinguish source, package and deployed evidence.

**Never:** Claim production completeness or writer drain from synthetic fixtures, scan complete databases, alter production data, publish packages, change dependency versions, remove rollback implementations, or weaken existing validation. Any new contract or dependency decision outside this scope returns for human resolution.

## I/O & Edge-Case Matrix

| Deferred boundary | Actual input/control | Required observation |
|---|---|---|
| Snapshot deadline | Owned regular-file lease blocks eligible source read | Record elapsed deadline and eventual result; a correction must bound the caller and own worker cleanup |
| Purged workflow identity | Historical chunk/NL artifacts survive actual workflow purge | Absent workflow metadata must not falsely certify their erasure; preserve fresh and tracked retry controls |
| Concurrent historical producer | Older producer creates history after modern absent-history lookup | Retain real scheduling order and post-cleanup artifact contents before introducing exclusion |
| DW-58 process restart | Provider state survives separate workload processes | Preserve operation/tenant identity, canonical detail/processing/index, erasure and foreign bytes |
| Sidecar publication | Actual selected SDK emits token-bearing app and sidecar | Require paired secret references in supported target artifacts; unsupported output remains an explicit blocker |

</frozen-after-approval>

## Epic 8 Section 4 Gate

1. **Prerequisites:** Reuse selected contracts. Owner baselines: Platform `5adb00b2f30d2985ee0b1fb1abd713b10660dc48`, Memories `c4addf98c6a4017d54e0c3aaeb3b57a52f82bffa`, EventStore `d35a4febffbfcead9c0ff096d052b4698d1242db`, Builds `539b10ab0f884888ae4aeba0edb829dd59f707d6`. Record final exact revisions and effective package identities. Preserve I16 gates.
2. **Repositories:** Parties, Platform for snapshot enforcement, Memories for ingestion provenance, and EventStore for any demonstrated supported generic publication correction. External publisher delivery is a hard gate, not authority to invent domain-local plumbing.
3. **Rollback:** Keep existing runtime selectors and compatibility paths. Exercise reversed changed code in isolated owner checkouts at final dependencies, preserving passing baseline controls and expected regression failures. Preserve the current immutable Builds/Platform package-consumption cohort; do not adopt incompatible source pointers.
4. **Validation:** Platform Composition source inventory/seal classes; Memories IntegrationTests actual Dapr producer controls plus Server determinism guards; Parties service backfill controls and IntegrationTests publish controls. Build individual projects, invoke xUnit v3 assemblies directly, require nonzero discovery and no targeted skips. Retain source Debug separately from package Release.
5. **Non-goals:** No production migration, crypto extraction, UI consolidation, AppHost retirement or deployment. DW-58 remains subject to source-history completeness, deployed writer drain, authorization and cutover acceptance.
6. **Parity:** Preserve I5–I10/I8 erasure, replay, tenancy and retained history; treat inventory as I19 reconciliation state. Preserve all I18 baseline surfaces.

## Code Map

- Platform `src/Hexalith.Platform.Composition/PlatformCurrentOwnerSourceState.cs`: synchronous `Capture`, `RequireUnchanged`, and `Hash`; inventory deadline checks do not bound `File.ReadAllBytes`.
- Memories `src/Hexalith.Memories.Server/Ingestion/{DaprIngestionWorkflowScheduler,SourceInventoryStore}.cs` and `Activities/Cases/ScheduleAnnotationIngestionActivity.cs`: existing-history checks, matching durable admission and scheduling. Reuse IntegrationTests `Ingestion/HistoricalCandidateArtifactWorkflow.cs` and `RetryWorkflowErasureIntegrationTests.cs` for real historical artifacts/Dapr hosting.
- Parties `tests/Hexalith.Parties.Tests/Projections/PartyLegacy{BackfillRuntimeFixture,ReadModelBackfillRuntimeTests}.cs`: current eight provider controls recreate clients, not workloads. Reuse `tests/PackageTests.Shared/PackageTestProcess.cs` for owned bounded children.
- Parties `src/Hexalith.Parties.AppHost/Program.cs` and IntegrationTests `Topology/PartiesPublish{Environment,Oidc}Tests.cs`; EventStore `src/Hexalith.EventStore.Aspire/HexalithEventStoreAppChannelExtensions.cs`: app/sidecar token annotations exist; selected Toolkit callback omits environment serialization.

## Tasks & Acceptance

**Execution:**

- [x] Platform source-state and Composition tests: reproduce real lease-blocked capture/recheck, then correct demonstrated deadline behavior using bounded ownership rather than abandoning a blocked thread.
- [x] Memories scheduler/inventory and integration tests: retain actual historical artifact/purge/reuse evidence, correct the independently demonstrated legacy-NL collision, preserve ordinary production cleanup, and record the missing per-unit authority as an external gate without qualification credit.
- [x] Memories scheduler integration evidence: retain the actual absent-read/older-completion interleaving and independent cross-source false-success proof; reject source-wide freshness and preserve tracked acknowledgement recovery. Full history safety stays unqualified pending a new owner-approved protocol.
- [x] Parties backfill fixture/runtime tests: carry immutable synthetic inputs through separate seed/rebuild/stage/commit/verify processes; inspect durable values and unchanged survivors. Preserve supported aggregate/shared protocols.
- [x] EventStore generic publisher and Parties publish tests: establish supported target capability before changing publication; qualify actual emitted secret pairing and startup negatives after a correction. If upstream delivery is unavailable, retain its precise owner blocker without a model-only closure.
- [x] This spec, `deferred-work.md`, and dated evidence: activate exactly these entries, retain commands/revisions/red-green results and review inputs, and preserve unresolved statuses for parent finalization.

**Acceptance Criteria:**

- Given a reproduced defect, when its fix and focused controls run, then observed runtime/persisted outcomes satisfy the matrix without weakening existing controls.
- Given an unavailable external prerequisite, when evidence is recorded, then its entry stays open with the exact blocker and no source-to-deployment equivalence claim.
- Given reviewed changes, when pinned commitlint and divergence checks pass, then authorized main pushes contain only verified owner changes and compatible parent selections.

## Publication Follow-through

After independent review, complete exact owning identities, `.gitlink-signoff.tsv`, current-source observations, commitlint, divergence checks and owner-before-parent main pushes. This remains required under the approved intent and is tracked through workflow finalization rather than as a pre-review implementation task.

## Implementation Notes

Approved by the user on 2026-11-11 and re-read unchanged before implementation. Execute changes sequentially. This is the implementation handoff of the active workflow: do not restart bmad-build. Implementation performs no remote Git operations; the parent owns staging, independent review, commits and the authorized main pushes after review. Retain exact command/results and cleanup evidence in a dated batch directory.

## Historical pre-review implementation progress (superseded)

- Platform uses an owned internal .NET measurement worker, preserving SHA-256,
  path/link/size checks and a cumulative caller budget. The nonpackable Composition
  assembly retains its library role and adds only a private worker entry point.
- Memories reuses existing independently reconciled source coverage before granting
  first-candidate modern completeness. Unknown/purged sources stay incomplete;
  the legacy NL namespace joins the exact atomic candidate collision check.
  Real purge and two-producer controls inspect retained artifacts and cleanup refusal.
- Parties adds immutable manifests and separate seed/rebuild/stage/commit/verify
  workload processes for supported aggregate one-shot/shared staged protocols.
- Actual selected SDK sidecar conformance passes by refusing unsupported output.
  Positive paired secret emission remains blocked by the selected upstream
  publisher; no generic publisher, dependency or deployment change was introduced.
- Parent owns independent review, final commit identities/signoff, commitlint,
  divergence checks, owner pushes and compatible parent adoption. No remote Git
  operation, staging or commit was performed by implementation.

## Spec Change Log

- H1: source-wide attestation is not per-unit authority and breaks ordinary cleanup. Removed that prerequisite and its baseline fixture attestations. KEEP the actual purge/concurrency/cross-source counterexamples, bounded snapshot worker, ordinary fresh/lost-ack behavior, separate-workload backfill and honest publisher refusal. The approved external-gate clause governs the unresolved authority requirement; no frozen intent or production contract changes.

- Parent audit clarified implementation versus post-review publication sequencing; all approved publication work remains required. Frozen intent is unchanged.

## Review Triage Log

| Finding | Verdict | Route | Verified evidence |
|---|---|---|---|
| BH1 | medium | patch P1 | Worker cleanup creates a new five-second wait after the original deadline; transfer unfinished cleanup to retained ownership without renewing caller time. |
| BH2 | medium | patch P1 | A supported Kill exception exits finally before disposal or retained reaping; cleanup ownership must survive the exception. |
| BH3 | medium | patch P2 | Final diagnostics report recheck worker count zero: Git diff consumes the deadline before the final hashing boundary is reached. |
| BH4 | medium | patch P3 | Copied-worker test has no termination finally; its timeout/assertion path can leave the started child alive. |
| BH5 | medium | patch P4, pending history scope | Candidate completion is signaled from finally, and unknown coverage hides removal of the legacy-NL collision check; durable candidate/refusal outcomes need direct assertions. |
| BH6 | medium | patch P4, pending history scope | Historical controls compare selected text fields and a same-tenant survivor, whereas the claimed preserved-byte/foreign-tenant outcomes lack complete values. |
| BH7 | medium | patch P5 | A process stop/wait exception skips subsequent sidecar/container cleanup and receipt creation in the driver's single finally. |
| BH8 | medium | patch P5 | Any failed docker inspect is recorded as absent, including daemon/permission failures; only a successful independent lookup or exact missing-object result proves absence. |
| BH9 | medium | patch P6 | Matrix success uses failed==0 without discovery/skip/required-case or exact reversal validation; an empty or skipped report qualifies incorrectly. |
| BH10 | medium | patch P6 | Sidecar conformance is hardcoded true without validating actual receipt records, target identities or publisher outcomes. |
| E1 | high | defer D1 | Unit keys/provenance are tenant/unit scoped, but current.CoverageComplete belongs to tenant/source. Reconciled empty source B can grant modern completeness to source A's reused unit while unregistered chunks survive; SemanticKeyDiscovery returns early for modern markers. |
| E2 | high | patch P7 | SourceInventoryStore is always registered; absent source coverage now withholds provenance for ordinary fresh ingestion, SemanticKeyDiscovery refuses it, and the only source qualification endpoint is explicitly non-Production. Added fixture attestations do not preserve baseline fresh cleanup. |
| E3 | medium | patch P5 | Exit between poll and killpg raises ProcessLookupError and aborts remaining cleanup in the owned provider runner. |
| E4 | medium | patch P6 | Zero-discovery/skipped XML with zero failures is accepted as qualified by the receipt writer. |
| E5 | medium | patch P3 | Copied worker has no termination finally around its ten-second wait; Process.Dispose does not terminate a live child. |
| V1 | medium | patch P4 | Pre-verified regression gap: current collision theory omits legacy NL, and new unknown-source controls pass after removal of the eighth key. Add a direct candidate-recording refusal and unchanged-artifact assertion. |

Final H1 routing: E2 is fixed by removing the invalid prerequisite and fixture assumptions; its smallest correction is deletion with no new contract. E1 is a pre-existing false-completeness defect, independently reproduced and deferred as D1. H1 corrects the non-frozen plan, which incorrectly treated source-wide coverage as per-unit authority. Re-reading the approved frozen intent confirms that external blockers must remain explicit and no new contract is authorized. The original scope therefore preserves production cleanup, rejects the invalid prerequisite, retains historical identity qualification as open, and proceeds with verified safe work. The asynchronous question is an optional request to expand beyond that approved scope; no response is assumed. Frozen intent remains unchanged.

Implementation qualification found positive fixture assumptions that previously
inferred completeness from modern admission. Genuinely owned virgin sources now
use the existing fenced reconciliation before any producer. The legacy refresh
fixture keeps source coverage unknown and explicitly reconciles only its exact
owned output keys. Separate unknown-source purge/concurrent controls require
cleanup refusal. Independent review remains parent-owned.

## Historical proposal verification (superseded)

Dated commands, per-test XML, source/binary hashes, isolated reversals, cleanup and
five-row coverage are retained in
`tests/five-snapshot-history-and-publish-2026-11-11/verification.json`.
The snapshot green lane passes 11/11 versus reversed 9/11 (two lease regressions);
The final expanded Memories lane passes 102/102 versus reversed 98/102 (four
historical regressions), including related adapter/lifecycle qualification. An
unchanged worker-upgrade intermittent failure from an earlier overlapping run is
retained; no cause is established, and the final serialized run passes it. Server determinism
passes 25/25; package provider 9/9 includes ten exiting workload children; publish
model 42/42 and three actual SDK refusal/conformance controls pass without skips.


Build owner test projects with `dotnet build <project.csproj> -c Debug -m:1`. Run their built assemblies with `-class`/`-method`, `-failSkips`, and fresh `-result-xml` paths. Use prepared package Release hosts for provider/publish qualification. Record blockers, follow the validation ladder, independently review, and inspect every root submodule before committing.

### Parent implementation audit

The parent read the staged Parties, Platform and Memories unified diff, verified every retained source/binary/XML hash and test count, and compared executed isolated Platform sources byte-for-byte with the owner checkout. All five matrix rows have executed passing controls; unsupported paired sidecar emission remains explicitly unqualified. Implementation tasks are complete. Independent review and authorized publication follow.

### H1 correction while scope is pending

The invalid source-wide prerequisite and its additional baseline fixture attestations were removed from the owning checkout. A narrow legacy-NL base-key collision guard and direct durable-state regression remain for verification. The rejected proposal is preserved byte-for-byte in the evidence packet. Independent runtime evidence demonstrates source-A purge, source-B reconciliation, modern provenance, deletion returning true and the historical chunk surviving. Current history qualification is not claimed; the two history implementation tasks remain open pending the requested human choice.

### Final local scope after H1

The approved external-blocker allowance applies to the missing per-unit identity authority. Ordinary production cleanup is preserved. Historical false-completeness safety is not qualified: the rejected proposal and independent false-success counterexample are retained as blockers, not passing production acceptance. All 204 selected safe-tree verification checks pass. The Platform ordinary restore separately reports NU1903 at unchanged pinned dependencies; its documented diagnostic build and focused controls pass. Final review disposition, compatible source adoption and authorized publication remain.

### Review correction verification

P1–P3 and P5–P6 are verified by the strengthened14-control Platform lane,12Python refusal/cleanup controls and3actual publisher checks. P4/P7 remove the invalid source-wide prerequisite and artificial baseline attestations;99real Memories controls pass with ordinary fresh/lost-ack behavior preserved. The legacy-NL case asserts exact refusal, no durable candidate, no provenance and unchanged complete hash bytes. Historical proposal preservation claims are withdrawn; full counterexample hashes and the rejected patch remain evidence of the unresolved D1 gate. Final reversals retain12/14Platform baseline passes with exactly two deadline failures and98/99Memories baseline passes with exactly the legacy-NL failure. No finding is omitted.

### Final implementation and owner publication

The workflow completes within the approved external-gate allowance. Pushed owner commits are Platform `3ad2f29ae50858ba665499568623b557725b9d93` and Memories `13fdb88a3267626b14f75e16cc2de58dcfbbb2bb`. The final tree bounds source measurement and its worker lifetime, rejects an existing legacy-NL candidate without durable mutation, preserves normal fresh cleanup, verifies backfill through ten workload processes, and refuses unsupported sidecar publication qualification. All204selected checks pass, with exact source/binary/test identities retained in `tests/five-snapshot-history-and-publish-2026-11-11/final-verification.json`. Historical identity safety, older-writer exclusion, positive publisher delivery and deployed backfill acceptance stay open. Parent current-selection validation and main publication follow this local completion record.

### Committed-source validation and publication handoff

At code revision `e522f6fe35f5d061fcf57b3c27523e7e64387353`, all 196 current
source-selection/closure checks and 31 reader tests pass, and the push-range
signoff gate accepts both exact owner gitlinks. Together with 204 implementation
controls, 431 distinct selected checks pass. An initial unchanged closure fixture
MSBuild query failure is retained alongside its focused pass and the full
serialized pass; no cause is established and no assertion was changed or skipped.
The committed code message and final evidence message pass pinned commitlint.
The completion evidence commit and authorized parent main push complete the
publication follow-through; all named external gates remain open.
