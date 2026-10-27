---
title: 'Deliver five deferred FrontComposer UI capabilities'
type: 'refactor'
created: '2026-10-27'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '6ba02654ca390b3333183962355bd5c5857b8224'
context:
  - AGENTS.md
  - docs/development-guide.md
  - references/Hexalith.AI.Tools/hexalith-ux-instructions.md
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Parties retains generic UI behavior because FrontComposer lacks
shared freshness, live-region/reconciliation, download and typed-name APIs.
Published Shell 4.7.0 also lacks the already implemented skip-link fixes.

**Approach:** Address exactly DW-91, DW-100, DW-101, DW-102 and DW-112.
Deliver additive capabilities in FrontComposer, verify producer behavior, then
adopt each available approved capability behind stable Parties adapters.
Qualify published packages; retain open gates otherwise. Review, validate, commit and push the
task changes to owning main branches and adopt the producer gitlink separately.

## Boundaries & Constraints

**Always:** Preserve domain labels, freshness wire semantics, export bytes and
safe filenames, authorization, exact ordinal erasure confirmation, keyboard
accessibility and rollback. Record immutable producer and resolved package
identities. Require owner acceptance and producer/consumer parity before
migrating a slice. This is a new B–F maintenance batch; the blocked G4-A picker
spec and ordered story prerequisites remain authoritative for that story.

**Never:** Reimplement shared machinery in Parties; initialize nested
submodules; change public picker contracts, security/custody or deployment;
weaken warning/test gates; infer package delivery from source success; promote
the whole G4 row or Story 8.9 from these five slices. Do not publish packages
manually or alter release workflows to bypass owner publication gates.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Route navigation | Cold load or hydrated client-side route | Forward Tab reaches skip links; route remains announced | Preserve current keyboard and focus contracts |
| Compact navigation | Phone/tablet, desktop, resize | Navigation link exists exactly when its unique target mounts | Content bypass remains reachable |
| Freshness/reconcile | Current, stale, degraded; accepted, rejected, cancelled, duplicate/late confirmation | Existing labels, politeness and single completion survive shared implementation | Cancellation tears down; failures remain assertive without focus theft |
| Export | Admin/Consumer file or JSON, repeated download, cancellation/failure | Identical bytes and safe filename; streams, anchors and URLs cleaned up | Existing transient failure feedback; no payload logging/storage |
| Erasure confirmation | Empty, whitespace/case mismatch, exact name, Escape | Only ordinal exact match permits one submit; cancel stays safe | Guard callback as well as disabled button |
| Package prerequisite | Published package lacks a capability | Retained local adapter continues working | Record open publication/adoption gate |

</frozen-after-approval>

## Code Map

- Producer root: `references/Hexalith.FrontComposer`, selected revision
  `8d2b47bd206ffd5c5e77a5ac0461b9cfe52ea4fd`. Keep contracts neutral;
  update public API manifests with producer additions.
- Producer `Components/Forms/FcDestructiveConfirmationDialog.razor` and its
  `.razor.cs` already provide safe cancel/Escape behavior; extend additively.
  `Components/Layout/FrontComposerShell.razor` and `wwwroot/js/fc-keyboard.js`
  already contain source skip-link fixes; preserve them.
- Parties `UI/Components/Shared/{DataFreshnessIndicator,StatusLiveRegion}.razor`,
  `UI/Services/OptimisticReconcile.cs` and Consumer `Components/FreshnessStatus.razor`
  supply behavior to adapt, including subscription/polling disposal.
- Admin `Components/PartyGdprOperationsPanel.razor`, Consumer
  `Components/MyPrivacyPage.razor`, and `UI/wwwroot/*-privacy-export.js`
  contain typed-name and duplicated stream-download seams.
- `Directory.Packages.props`, `Directory.Build.targets` and consuming `.csproj`
  files control package/source selection. NuGet currently publishes through
  Shell/Contracts.UI 4.7.0; its Shell repository revision is
  `cf5fe8da100bd5224a0d7f597fc92e1ebb98a5f4` and lacks both skip-link fixes.

## Tasks & Acceptance

**Execution:**
- [x] Producer `src/Hexalith.FrontComposer.Contracts.UI/Freshness/` and
  `src/Hexalith.FrontComposer.Shell/Components/Feedback/` — add normalized record freshness,
  polite/assertive live regions and callback-based optimistic reconciliation;
  keep EventStore/Parties wire types outside the reusable API.
- [x] Producer `src/Hexalith.FrontComposer.Contracts.UI/Downloads/IFcBrowserDownloadService.cs`
  and Shell `Services/Downloads/` — add injectable stream/file/JSON download
  with safe filenames, cancellation, cleanup and no content persistence.
- [x] Producer `FcDestructiveConfirmationDialog.razor` and `.razor.cs` — add optional typed-name mode with ordinal
  match and guarded confirmation; retain existing callers and safe focus.
- [x] Producer component/browser tests and API manifests — cover every matrix
  row, including both existing skip-link fixes and default dialog compatibility.
- [x] Parties adapters, registration and consuming projects — adopt only
  accepted capabilities available in the selected dependency mode; retain local
  rollback where package delivery remains absent.
- [x] Parties UI/Admin/Consumer component tests and
  `tests/e2e/specs/parties-accessibility.spec.ts` — verify mapped source and
  package behavior; remove opt-ins only after positive package proof.
- [x] `deferred-work.md`, prerequisite matrix and a new evidence directory —
  record exact scope, commands, executed assets, review and remaining gates.
- [x] Owning Git metadata and parent gitlink — validate exact messages with
  pinned commitlint, inspect divergence, push without force, verify remote SHAs.

**Acceptance Criteria:**
- Given each shared replacement, when producer and Parties controls execute,
  then its matrix behavior passes without targeted skips or public regression.
- Given missing publication or owner acceptance, when reconciling the ledger,
  then affected entries remain open and the retained consumer path works.
- Given passing review and checks, when task commits are pushed, then owning
  main branches and the parent gitlink identify the verified changes.


## Implementation Notes

- 2026-10-27: User approved the unchanged frozen spec and requested continuation.
  Implement sequentially. Prepare producer/consumer changes and validation first;
  the parent owns final review, staging, commits, producer gitlink adoption and
  all remote operations after implementation verification. Do not push during
  the implementation handoff.


- Parent audit: all six frozen matrix rows map to named executed passing tests;
  107 current source/configuration hashes and the executed binary/host hashes
  match. The staged diff includes new files and reviewed producer changes from
  the original baselines; CRLF-only differences are normalized for readability.
  Git publication remains queued for the final workflow stage because step 03
  prohibits remote operations. No frozen intent or broader G4 gate was changed.

- Review outcome: all 13 patch findings are resolved in 12 root-cause groups;
  BH7/VG1 share the missing actual-host qualification control. Two pre-existing
  transport issues are deferred; BH6 is rejected as low-impact extension-only
  complexity. Producer targeted regressions (44), actual-host startup cases (3),
  and focused browser cases (2) pass. Parent reran the full affected source and
  retained-package controls; the final archive supplies exact results. Frozen
  intent SHA256 remains 519b088b583d66c259d938874088204b8a680a939b0c0a30f676ad7f8762e420.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| BH1 | medium | defer | The retained Parties ReconcileAsync also subscribes only after IssueCommand; the extracted producer preserves this pre-existing missed-confirmation window. Immediate reads or subscribe-before-command semantics need a separate transport decision. |
| BH2 | medium | defer | The retained Parties path checks subscription.IsConnected once and RunFallback stops on reconnect; later disconnection is a pre-existing transport-lifecycle gap. |
| BH3 | medium | patch | IssueCommand can cancel its token and then throw an ordinary exception; the generic catch currently emits rollback and failure after abandonment. Suppress that catch when caller cancellation is requested. |
| BH4 | medium | patch | ConfirmErasureAsync leaves the mounted dialog open on AdminPortalQueryException and resets Disabled after busy state. The permanent completion latch then prevents retry. Reset that latch only on the demonstrated caller busy-to-ready transition, retaining the in-flight and terminal guards. |
| BH5 | medium | patch | The typed field observer filters changes to its own root; Body is an external sibling description. Refresh the proxy for changes to that referenced description. |
| BH6 | low | reject | Only the explicit opt-in AddFrontComposerFeedback chain registers this marker in the actual host; no independent production registration exists. A hypothetical custom download-only registration would enable other adapters, but introducing a new capability marker/option surface for this uncommon extension is more than a direct correction. |
| BH7 | medium | patch | The source browser erasure test accepts either shared or retained implementation, so registration removal can leave the qualification green. Require the shared marker for qualified source execution. |
| BH8 | medium | patch | The evidence verifier skips final receipt checks when final-verification.json is absent, yet prints success. Make that receipt mandatory. |
| BH9 | medium | patch | The verifier skips expected zero browser counts. Parse totals and compare all declared outcomes including zero. |
| BH10 | medium | patch | run-check.py overwrites its deterministic raw log path on repeated IDs before preserving the prior receipt. Use immutable per-execution logs and explicitly report unavailable historical logs rather than imply their presence. |
| ECH1 | medium | patch | Rejected runs within the IssueCommand catch scope. A throwing rejection callback triggers Revert a second time; move rejected-result processing outside that try/catch. |
| ECH2 | medium | patch | A throwing reconciled announcement faults the detached read before resolving completion. Propagate that exception through completion so teardown runs and ExecuteAsync terminates. |
| ECH3 | medium | patch | A cancelDownload timeout can skip module disposal. Nest cleanup in try/finally so disposal runs even when cleanup throws. |
| VG1 | medium | patch | Pre-verified: component fixtures register capabilities themselves and browser assertions accept fallback. Add actual host option-on/off composition controls and a strict source browser marker. |
| VG2 | medium | patch | Pre-verified: immediate Consumer downloads and independent producer cancellation tests do not exercise page disposal during a pending transfer. Add a pending transfer disposal control through the actual Consumer adapter. |
| P1 | medium | patch | Parent inspection found assert-based validation disappears under Python optimization. Replace assertions with explicit checks and verify normal and optimized poisoned-evidence controls. |

## Verification

- Inspect the retained AppHost baseline with the Aspire skill before code edits.
- Build affected producer and consumer projects with supported mapped Debug
  source selection; run their built xUnit v3 assemblies with `-class` filters.
- Run UI/Admin/Consumer suites and the Playwright route, compact/resize,
  confirmation and export controls; compare actually served owner assets.
- Run corresponding Release/package checks separately and retain negative
  package controls until matching publication is positively qualified.
- Run the warning-override gate, whitespace checks, source/binary provenance
  verification and exact pinned commitlint before staging and publication.


### Implementation handoff observations — 2026-10-27

- The retained AppHost started in isolated mode; `parties-ui` reached healthy.
  EventStore/Admin/Tenants resources also showed Finished, so this is a UI
  baseline observation, not full topology qualification. The exact AppHost was
  stopped before code edits.
- Pinned tool 4.34.0 fresh mapping generation was blocked by an unavailable
  historical Builds object. Retained producer-generated mapping was regenerated
  and its exact inputs/hashes preserved. It first rejected legacy edge metadata
  with HXW006. Parent-authorized integration conditions in FrontComposer
  `Directory.Build.props` and Tenants `Directory.Build.targets` defer to mapper
  authority and preserve the legacy paths outside mapped mode. After concurrent
  dependency updates, the stale retained map rejected ExcludeAssets metadata; a
  fresh pinned-tool regeneration then passed without further Tenants edits.
  Final checks use mapping hash
  `ec9d8eaa4595039b03a8f2b016065788232196320450e9afaa67b1c2328c35c8`.
- Browser qualification found missing V5 button roles, a shadow-boundary warning
  relationship and a 150 ms landmark-focus retry that stole later focus. The
  producer fixes are narrow; existing keyboard assertions and generated-field
  behavior stay enforced. The deterministic producer clock regression captures
  the focus ownership requirement.
- Concurrent external `/pushall` work committed parts of the in-progress tree
  and advanced reference revisions. The original review baselines remain
  unchanged. Check-specific full before/after revisions and source/binary
  hashes in the final evidence remain authoritative; no external work was
  reverted.
- The new shared-path warning initially failed with CS8795 because framework
  package pruning removed the source generator. The narrow existing host
  analyzer-restore pattern supplies Microsoft.Extensions.Logging.Abstractions
  10.0.13 / roslyn4.4; its exact generator bytes and generated-warning execution
  are recorded. The original three-argument adapter constructor is preserved.
- Shared service registration defaults off; named owner acceptance, upstream
  package publication/adoption, final review and parent-owned Git completion
  remain separate gates. The evidence archive retains failed attempts separately
  from final passing checks, including ENOSPC and browser setup failures.

- Git completion: producer main c2b925bae1c9b3bfa8c3a4698ad0104de02ed5a4 and Parties integration main
  03fecb86e742055e87d8b7ff6ac40a12b73372ef were pushed without force and read back exactly. The parent
  gitlink matches; pinned commitlint and the main-push gitlink range gate pass.
  See the batch publication.json. Named owner/package gates remain open.
