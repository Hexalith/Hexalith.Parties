---
title: 'Advance five deferred data-lifecycle gates'
type: 'bugfix'
created: '2026-10-28'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '779f35adf101f65cb2a72c11bb2eca4cd351bce4'
context: []
---

<frozen-after-approval reason="user authorized five deferred items and publication on main">

## Intent

**Problem:** DW-50/82/168 retain outdated package-publication blockers despite current EventStore and Memories selections. Parties still disables revision-aware Memories refresh in package builds. DW-130/149 require qualified history-expiry evidence unavailable from synthetic custody tests.

**Approach:** Process these five items as the explicitly requested batch. Adopt the existing refresh protocol for the currently selected, API-compatible Memories package, execute its recovery/cleanup controls and the bounded processing-history controls against actual restored packages, and preserve precise external blockers. Record fresh custody readiness refusal without fabricating a provider. Review and push authorized task changes on main. This is progress on the five items; full closure requires their actual exit evidence.

## Boundaries & Constraints

**Always:** Keep fail-closed inventory completeness, terminality, tenant/source/producer authority, stable retry identities, writer-drain requirements, independent history custody and rollback. Bind receipts to actual restored packages, repository revisions and exercised assemblies. Preserve user changes. Use Debug for local verification; attempt the sanctioned source mapping separately. Honor the existing pinned commitlint.

**Never:** Update dependencies/gitlinks, modify producers, production data or owner acceptance, invent historical completeness, skip expired history on ordinary read denial, or mark externally blocked ledger entries done. No application/provider deployment is authorized. Existing tests using synthetic providers establish their bounded behavior only.

## I/O & Edge-Case Matrix

| Scenario | Input | Expected behavior |
| --- | --- | --- |
| Published refresh | Selected Memories 3.0.3; canonical revision | Actual SDK refresh sends revision and stable identity; supersession and lost acknowledgement retain obligations |
| Inventory cleanup | Empty/partial local state; pending producer; wrong scope | Bounded remote reconciliation; incomplete, running or foreign evidence never certifies deletion |
| Processing history | Large legacy lists, interruption, concurrent write, scoped cursor | Bounded buckets, persisted restart, complete authorized reads and stale/corrupt read refusal |
| History expiry | Missing installed custody prerequisites | Existing Live gate refuses before endpoint access; no continuity or expiry qualification claimed |

</frozen-after-approval>

## Code Map

- `Directory.Build.targets`: `HEXALITH_MEMORIES_SOURCE_REFRESH` currently enabled only for source; preserve its existing assembly metadata key while adding the qualified current package.
- `src/Hexalith.Parties/Search/PartyMemoryIndexingService.cs`: existing refresh/digest/revision/recovery implementation; reuse rather than inventing a new protocol.
- `tests/Hexalith.Parties.Tests/Search/PartyMemoryRestClientRecoveryTests.cs`, `PartyMemorySourceInventoryTests.cs`, `PartyMemoryIndexEntrySearchIndexerTests.cs`, `PartyMemoryCleanupServiceTests.cs`: real REST adapter plus durable synthetic store controls, with refresh-specific cases currently compiled out in package mode.
- `tests/Hexalith.Parties.Tests/Projections/PartyProcessingGenerationRecoveryTests.cs`, `Gateway/PartySdkQueryHandlerTests.cs`, projection `Handlers/PartySdkProjectionHandlerTests.cs`: existing bounded migration/rebuild, query and recovery controls.
- `docs/gdpr-processing-activity-records.md`, `docs/development-guide.md`, `tests/README.md`: current capability and qualification guidance; dated historical receipts stay historical.
- `eng/verify-ext-parties-1.ps1`, `docs/implementation/ext-parties-1-custody-prerequisites-2026-10-25.md`: installed custody/acceptance gate; do not replace with synthetic evidence.

## Tasks & Acceptance

**Execution:**
- [x] `Directory.Build.targets` and affected existing tests: enable and verify the current package refresh capability; keep unsupported selections fail closed and source support intact.
- [x] Existing service/projection test projects: build and execute the relevant refresh, inventory, cleanup, generation and paging controls; record failures and fix task regressions.
- [x] `docs/gdpr-processing-activity-records.md`, development/testing guidance and deferred ledger: correct current package blockers and preserve each item's exact outstanding external requirement.
- [x] `tests/five-data-lifecycle-qualification-2026-10-28/` under implementation artifacts: retain commands, provenance, results, matrix coverage, custody refusal and the source-build disposition.
- [x] Task diff and Git publication: independently review, remediate, run relevant final checks, validate the exact commit message, inspect divergence and push task changes to main.

**Acceptance Criteria:**
- Given restored current packages, when refresh/recovery and processing controls execute, then each matrix behavior passes with no targeted skip and receipts identify the actual implementation assemblies.
- Given incomplete owner evidence, when reporting the five items, then all missing deployed/custody gates remain explicit and no ledger status implies unsupported closure.
- Given successful task verification, when publication completes, then remote main contains the reviewed task commit without unrelated changes.

## Implementation Notes

- User explicitly requested five items together and push on main; that authorization supplies the batch scope and final publication decision. No dependency change or external provider selection is inferred.
- Baseline isolated Aspire startup succeeded and `aspire wait parties --timeout 15` reported healthy. The owned AppHost was stopped before test builds.
- Read-only investigation found no production `IIdentityHistoryCustody` or `IExpiredIdentityHistoryCustody` implementation at the selected Platform revision. Installed owner evidence was requested asynchronously; dependent qualification remains blocked until supplied.

## Spec Change Log

## Review Triage Log

| Finding | Verdict / route | Evidence |
| --- | --- | --- |
| BH-01 | medium / patch | Maintained Memories search guidance still requires source mode and pending publication although the selected package is now enabled; correct that consumer upgrade guidance. |
| BH-02 | medium / patch | Refresh assertions share the implementation compilation guard and disappear with a selector regression; add an unconditional actual-request capability test. |
| BH-03 | false | Negative receipts explicitly claim selector/property probes without restore, not alternate-package compiled runtime qualification. The same target condition covers production and tests; current production assembly metadata and actual SDK requests are captured. No divergence is demonstrated. BH-02 independently addresses positive activation coverage. |
| BH-04 | medium / patch | Existing RegisterAsync rejects retained legacy attempts lacking authority/protocol/digest proof. Document the preserved retry obligation and qualified reconciliation/rollback requirement rather than implying an automatic intent migration. |
| BH-05 | medium / patch | Live always exits 1, including after all inputs are supplied: complete authenticated persisted-state/expiry lanes are not installed. Missing inputs are the observed first refusal, not the complete blocker. |
| BH-06 | medium / patch | Projection receipt hashes its test assembly but not its own restored graph or exercised SDK/implementation copies; bind that separate execution output. |
| BH-07 | low / patch | Aligned source rebuild replaced the initial failure assemblies before hashes were captured. Preserve this historical evidence limitation explicitly; do not invent original hashes or claim source qualification. |
| BH-08 | medium / patch | Zero-warning build claims depend on logs in temporary storage; retain compact sanitized compressed logs with hashes. |
| BH-09 | low / patch | Sanitization has hashes but no retained implementation. Record the exact path-only transformation and deterministic compression procedure for reproducibility. |
| BH-10 | medium / patch | Static result manifests lack an executable consistency check against retained XML, unique case IDs, counts and required matrix methods; retain and run a bounded receipt validator. |
| VG-01 | medium / patch | Pre-verified: the only independent capability assertion belongs to an excluded opt-in runtime; normal package verification needs an unconditional actual request assertion. Same root cause as BH-02. |

| VG-02 | medium / patch | Follow-up reviewer independently deleted required matrix rows, emptied controls and removed a processing control from both manifests; all incorrectly passed. Pin mandatory control identities independently and add deletion/empty-list rejection controls. |

- Edge-case review reported no additional finding. All three layers completed before triage.

## Verification

- Individual Debug service/projection builds; invoke built xUnit v3 assemblies with `-class`/`-method` and fresh XML.
- Inspect current nupkg repository metadata, package/assembly hashes and effective build properties; attempt sanctioned mapped-source verification independently.
- Execute the existing custody Live gate only with available inputs; absent prerequisites must refuse before transport.
- Run warning-override and whitespace checks, pinned commitlint, then non-force push and remote SHA verification.

## Implementation Handoff — 2026-10-28

- Current package builds pass with zero warnings/errors. Service controls pass
  1,197/1,197 and projection controls 218/218, both with zero targeted skips.
- Exact Memories 3.0.3 enables the existing refresh protocol; unqualified older
  and future package selectors remain disabled. Source support and the existing
  assembly metadata key are preserved.
- Fresh sanctioned source mapping builds, but original execution fails assembly
  resolution (root 3.128.4 versus source 3.128.3). The explicitly diagnostic
  source-only `HexalithEventStoreVersion=3.128.3` override passes all 1,197
  controls; it does not qualify the unchanged 3.128.4 mapped-source graph. No
  tracked dependency or gitlink changed.
- Fresh custody Live refuses before endpoint access on all twelve absent inputs.
  No continuity, expiry or installed-provider qualification is claimed.
- All five ledger entries remain open with their exact external requirements.
  Compact commands/provenance/matrix summaries and complete sanitized compressed
  native XML are retained in `tests/five-data-lifecycle-qualification-2026-10-28/`.
- Independent review, final verification and authorized Git publication remain
  with the parent workflow.

## Parent Verification Findings

- Focused native XML hashes, unique case IDs, all matrix methods/counts and SDK/implementation assembly hashes independently match the retained receipts.
- Full package service suite: 2,800 total, 2,790 pass, six failures and four optional skips. The six unchanged fitness checks concern external EventStore 3.128.3 versus root 3.128.4, stale package/source receipts for McpCli/Platform and the architecture Dapr catalog table. Their exact failures are retained separately; the task did not change those pins, assertions or historical owner acceptance.
- Full projection suite: 467 total, 466 pass, zero failures and one old-package compatibility skip.
- User requested independent discovery of missing evidence. Wider owner records and read-only installation discovery are being retained; no owner decision or installed workload is inferred from ordinary service presence.

- Review-1 resolves BH-01/02/04/05/06/07/08/09/10 and VG-01 through corrected operational guidance, unconditional REST assertions, separate projection provenance, historical hash limitations, retained logs and executable receipt checks. Follow-up verification confirms these fixes; VG-02 remains in remediation.
- Wider read-only discovery is complete in `tests/five-data-lifecycle-qualification-2026-10-28/owner-discovery.json`: named Kubernetes workloads exist, while authoritative EXT-PARTIES-1 remains Committed and Platform installed custody hashes remain null. Azure resource listing is separately blocked by a missing MSAL cache entry; no login, secret retrieval, domain-store read or deployment was performed.

## Final disposition

- VG-02 is resolved and independently rechecked: mandatory matrix rows/control identities are fixed independently of input receipts, and all fifteen negative checks reject in normal and optimized Python. No unresolved task review finding remains.
- Final primary package acceptance is 1,198 service controls plus the retained 218 projection controls, all passing without targeted skips. The six pre-existing broad service fitness failures and original selected-source version mismatch remain explicit.
- This bounded workflow advances the five items and fixes package refresh adoption. DW-50/82/168/130/149 remain open; missing deployed reconciliation/writer-drain and complete accepted custody/expiry execution prevent full closure. Wider independent owner/installation discovery supplied no such evidence.
- Exact final breaking commit message passed pinned commitlint21.2.2 on Node24.22.1. Main and origin/main were identical after fetch; task-only publication is authorized and final remote identity is checked by the parent Git operation.
