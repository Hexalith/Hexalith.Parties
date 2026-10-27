---
title: 'Deliver five deferred FrontComposer UI capabilities'
type: 'refactor'
created: '2026-10-27'
status: 'in-progress'
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
- [ ] Producer `src/Hexalith.FrontComposer.Contracts.UI/Freshness/` and
  `src/Hexalith.FrontComposer.Shell/Components/Feedback/` — add normalized record freshness,
  polite/assertive live regions and callback-based optimistic reconciliation;
  keep EventStore/Parties wire types outside the reusable API.
- [ ] Producer `src/Hexalith.FrontComposer.Contracts.UI/Downloads/IFcBrowserDownloadService.cs`
  and Shell `Services/Downloads/` — add injectable stream/file/JSON download
  with safe filenames, cancellation, cleanup and no content persistence.
- [ ] Producer `FcDestructiveConfirmationDialog.razor` and `.razor.cs` — add optional typed-name mode with ordinal
  match and guarded confirmation; retain existing callers and safe focus.
- [ ] Producer component/browser tests and API manifests — cover every matrix
  row, including both existing skip-link fixes and default dialog compatibility.
- [ ] Parties adapters, registration and consuming projects — adopt only
  accepted capabilities available in the selected dependency mode; retain local
  rollback where package delivery remains absent.
- [ ] Parties UI/Admin/Consumer component tests and
  `tests/e2e/specs/parties-accessibility.spec.ts` — verify mapped source and
  package behavior; remove opt-ins only after positive package proof.
- [ ] `deferred-work.md`, prerequisite matrix and a new evidence directory —
  record exact scope, commands, executed assets, review and remaining gates.
- [ ] Owning Git metadata and parent gitlink — validate exact messages with
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

## Spec Change Log

## Review Triage Log

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
