# Permitted milestone and build dependencies

## Decision and precedence

**No V2 product implementation can start on the available evidence. The smallest permitted milestone is M0: a source-grounded missing-evidence packet for the retained operational-security fallback.** This folder delivers M0. M1 is a subsequent reconciliation of genuinely new records, not a scheduled repeat assessment. No completed G01–G08 production gate is claimed.

The [applied direction](../../../docs/implementation/ext-parties-1-readiness-2026-11-08/applied-direction.md) and its [decision](../../../docs/implementation/ext-parties-1-readiness-2026-11-08/applied-decision.json) supersede older staffing/design-choice prompts. The [October 29 direction](../../../docs/implementation/ext-parties-1-applied-direction-2026-10-29.md) places feasibility before broad V2 work; the [no-go](../../../docs/implementation/ext-parties-1-feasibility-verdict-2026-10-29.md) remains operative. Historical proposal labels and the [readiness baseline](../../../docs/implementation/ext-parties-1-readiness-2026-11-08/README.md) are not new approval requests.

The canonical return surface is `/home/administrator/projects/hexalith/platform/docs/implementation/story-5-4-coupled-delivery-2026-10-25/owner-decisions.json`. The root reference checkout is an inspected historical copy. Do not update it, a submodule pointer or a second owner form. Documentary roles belong to the current sole Owner; they do not establish runtime independence. This spec does not fill the form or authorize operational effects.

## Source findings that determine scope

Paths in this table are relative to the Parties repository root; sibling repositories own their changes.

| Inspected file | Finding and consequence |
| --- | --- |
| `eng/verify-ext-parties-1.ps1` | Live writes `live-gate.txt` and exits 1 before an endpoint, even with all listed environment inputs. A full persisted/restart/restore/fault pass path is still missing. Do not rerun a refusal to manufacture progress. |
| `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs` | Processor/query custody is host-supplied through `GetService<IIdentityHistoryCustody>()`. No production provider is supplied by this registration. |
| `src/Hexalith.Parties/Domain/PartyDomainProcessor.cs`, `src/Hexalith.Parties/Queries/PartyIdentityQueryService.cs` | Identity paths depend on real authority/source/custody; an absent provider or inadequate strict evidence cannot be repaired by configuration claims or PartyDetail. |
| `../platform/src/Hexalith.Platform.Custody/PlatformCustodyServiceCollectionExtensions.cs` | Adds system time, unavailable prerequisites and optional strict cleanup; this is not independent currentness or production actor-history custody. |
| `../eventstore/src/Hexalith.EventStore.Contracts/Security/IdentityHistoryCustodyEvidence.cs`, `IExpiredIdentityHistoryCustody.cs` in that directory | Strict evidence requires all three flags; expired proof requires completed irreversible destruction. Neither represents V2 service expiry with destruction pending. |
| `../eventstore/src/Hexalith.EventStore.Server/Security/RetainedIdentityHistorySourceReader.cs` | Existing retained-history reader is a reusable strict seam, not an installed V2 source/proof producer. |
| `tests/Hexalith.Parties.IntegrationTests/Identity/` | Existing persisted test custody remains local evidence only. Preserve these files under the frozen owner-delivery stop. |

`source-evidence.json` pins 45 local inputs and exact bounded searches of the three owning `src` trees for `IIdentityHistoryCustodyV2`, `ServiceExpiredHistorySegmentProofV2` and `party-actor-custody-amended-v2`; all returned no matches. This is a source observation, not an assertion about every installation. The known external secrets archive is absent locally, so its private receipts/signatures were not revalidated. Dated October 29/November 2 service observations retain their historical scope; this run made no fresh service read.

## M0 — actionable evidence packet, permitted now

| Task | Concrete files/output | Dependency | Acceptance |
| --- | --- | --- | --- |
| T01 — pin current source and approval limits | This folder's `source-evidence.json`; read the three user inputs, applied-decision.json, canonical Platform form and source seams above | Existing local records; no current credential required | AC-01, AC-02 |
| T02 — route missing facts | This folder's `evidence-requests.md`, E01–E09 and preserved D3 disposition | T01 and original Platform field names | AC-03, AC-04 |
| T03 — define next build and execution stops | This folder's `SPEC.md`, this plan and `.memlog.md` | T01–T02; latest applied direction | AC-05, AC-06 |
| T04 — validate preservation and contract coherence | This folder's `validation.json`; existing read-only `docs/implementation/ext-parties-1-readiness-2026-11-08/verify-readiness.py` | T01–T03 | AC-07 |

- **AC-01:** Every named source hash is recomputed from existing bytes and labelled source-only. Missing external records are scoped as locally unavailable, never authenticated absence of copies or universal absence of evidence.
- **AC-02:** Original owner delivery/frozen Branch B bytes, all 48 readiness-protected inputs (including the exact prior Platform-form reconstruction), original amendment packet and all prior receipts remain unchanged. Conditional direction matches its four actual proposal hashes; installation/qualification/availability guards remain false.
- **AC-03:** Every `#/...` pointer in evidence-requests.md resolves in the canonical form. Every one of the 17 selected still-null readiness fields is covered, either by a factual request or the explicit D3 deferral. No null is replaced by an inferred value.
- **AC-04:** Each E request identifies the source content, provenance required, insufficient substitutes and exact work it could unlock. It asks neither for another contributor nor reapproval of settled text/cap/fallback/design direction. No repeated denied probe or expired-scope execution is a task.
- **AC-05:** The dependency sequence distinguishes evidence packaging, fallback reconciliation, feasibility, effective ratification, source build, production composition, complete commitment and qualification. No date/installed target/authority is synthesized; both production tasks remain unchecked.
- **AC-06:** ES-1 is explicitly future and conditional; local contract tests cannot close P-01–P-10/F1/F2, Level 4 or Level 5. Production recovery remains deferred under the single-contributor model even if a source-only build later becomes possible.
- **AC-07:** Both skill self-validation passes are logged; all new links/pointers and protected hashes validate; the existing readiness command exits 0 with its 21 checks. validation.json records actual commands/results and limits. No Local, LiveReadiness or Live lane is invoked for M0.

## M1 — reconcile a new evidence return, only when one exists

Codex remains the collection/reconciliation assignee. The Owner supplies only factual declarations or acceptance that existing records cannot establish, not file discovery or evidence administration. Use [E01–E09](evidence-requests.md); do not resend obsolete staffing/design prompts.

1. Require a genuinely new record, newly accessible authentic archive or actual already-operated service evidence relevant to E01–E08. A reworded no-go, unchanged configuration, unsigned template, new timestamp on an old receipt or generic healthy pod is not that trigger.
2. Inspect available public records without requesting Secret values or decrypting actor/snapshot content. Record original path, actual hash, issuer/authentication, observation/coverage interval, target identity and exact field mapping; revalidate required signatures against existing accepted trust when the source is accessible. Documentary sole-owner acceptance needs no invented personal certificate or new signing key.
3. Write a fresh receipt at `_bmad-output/implementation-artifacts/tests/ext-parties-1-continuation-evidence/review.json`, with source references and per-request `supported`, `partial`, `unavailable` or `conflicting` findings. Reserve this output for the first new-evidence run; refuse overwrite and preserve later attempts separately. This is an evidence receipt, not a second mutable owner form. Return supported references through the existing Platform form under its owning workflow; preserve nulls and all earlier assertions until their exact evidence and acceptance exist.
4. Retain fallback-only progress for E01–E04. To reconsider full custody, require **E01–E07 together** to substantiate current profile/access/preservation, every C01–C16 recovery path and its E-relative disposal construction, independent lifecycle/time authority across every restore cut, intended workload/capacity and route-specific operating policies. Unknown copies, currentness or deadlines retain the no-go. Evaluate actual existing evidence before any further assessment; no new load test, denied probe or service is authorized here.
5. After a credible feasibility finding, reconcile E08 against the already approved exact design hashes. Record only newly supported policy/legacy facts and scoped effective ratification through the existing form bundles. If those facts require a schema/predicate change, record the exact delta for decision; do not silently extend the conditional approval or ask to reapprove unchanged direction.

**M1 acceptance:** Each conclusion cites the exact new authenticated record and coverage; contrary records remain visible; each still-missing field stays pending. A finding states one of fallback-only progress, retained no-go, or evidenced feasibility plus remaining ratification facts. It supplies no resource mutation, restored profile success, new access grant, production-deputy waiver or installed acceptance. If no new source is available, the valid output remains E01–E09 with the stop retained; do not run M1 merely to create another empty receipt.

## First build that the missing evidence could unlock

**S1 — EventStore ES-1 contract-only slice; blocked now.** Dependencies are a credible E01–E07 operating-route feasibility finding and E08 facts making exact V2 ratification effective. Neither a second contributor nor installed hashes/date is a prerequisite to pure contract code; their absence still blocks production recovery and installed acceptance. The independent authority facts and trust policies must be real, even though S1 installs none of them.

Work in `/home/administrator/projects/hexalith/eventstore`, after its own repository guidance/build/source-mapping checks. Start the reader/refusal-first sequence from the adopted implementation plan, without a provider or host change:

| Task | Concrete proposed paths | Testable acceptance |
| --- | --- | --- |
| S1.1 — original and assurance contracts | `src/Hexalith.EventStore.Contracts/Security/AssuranceV2.cs`, `HistoryScopeV2.cs`, `OriginalUnitKeyV2.cs`, `OriginalUnitV2.cs` in the same directory | Exact amended assurance/profile; tenant/domain/Party/environment/instance bound; positive revisions/version; E from the original instant only; invalid policy/purpose/domain/overflow refused. One C# type per file. |
| S1.2 — separate closed custody surface | `src/Hexalith.EventStore.Contracts/Security/IIdentityHistoryCustodyV2.cs` and one file per required named request/result/segment/lifecycle dependency from adopted api-schemas.md, including `HistoryAdmissionRequestV2.cs`, `HistoryAdmissionResultV2.cs`, `HistoryUseRequestV2.cs`, `HistoryUseDecisionV2.cs` | Complete compilable dependency set, closed union branches and bounded ownership semantics. No Boolean/strict-success shim, no production provider, endpoint, registration, migration or backend call. Existing strict public contracts remain byte/behavior compatible. |
| S1.3 — deterministic contract vectors | New `tests/Hexalith.EventStore.Contracts.Tests/Security/IdentityHistoryV2ContractTests.cs`; existing `tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj` | Supported AT-01/AT-04/AT-05 contract vectors: exact-original expiry and overflow; offset-equivalent instants; closed parsing rejects missing/duplicate/case-alias/unknown fields and numeric/unknown enums; canonical Unicode/null/order/integer vectors; tenant/profile/type mutation rejects; amended evidence cannot become strict success. Record remaining installed/dispatch/idempotency vectors as pending. |

With the actual project-reference graph selected using EventStore's pinned tooling, the narrow build is `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj -c Debug -m:1` from that owning repository. Run the produced xUnit v3 assembly with single-dash class/method filters, using its actual output path; assert nonzero tests and retain raw results. This is a conditional local source check, not the missing Live command. No dependency version, output hash or execution result is promised here.

S1 completion means compilation and deterministic compatibility/refusal coverage for its dependency set. It does not claim an authenticated current authority, actual exact-once reservation/key creation, complete F1 producer, previous-binary dispatch isolation or installed destruction. ES-2/source parsers and F1, ES-3 persistence/SDK, Platform PL-1–PL-6 and Parties PA-1–PA-6 remain subsequent owned slices with their adopted acceptance gates. Generic boilerplate stays in EventStore/Platform, never a new Parties store, AppHost or CLI.

## Later delivery boundary

After owned code is concrete, E09 binds both real Parties command/query and EventStore persistence/replay hosts, every holder and two tenants, exact source/package/image/bootstrap/config/profile/plan/trust and current scope. Preserve strict rejection, old-writer fencing, unsupported legacy unavailability, original E, snapshots/OriginalEvidence and actor-free F1 continuity. Unknown destruction stays Pending/breach. Retain every AT-01–AT-20 and P-01–P-10 obligation in the adopted test plan; S1's local subset retires none of them.

A complete changed commitment contains Owner, Repository, RequiredArtifact, TargetVersionOrCommit, TargetIntegrationDate, CompatibilityContractAndVerificationCommand, RequiredEvidenceLevel, AcceptedStatus and ConsumingStories. Set a supportable date and exact command only for actual completed coordinated code. Accept the complete target before guarded installed execution; actual D03/D04 cut/fault/operator/observer/abort/disposal scope and the unmet D3 separation remain prerequisites. Drill fault ≤10 minutes and verified disposal ≤60 minutes never extend original data deadlines. This continuation stops before Live even if its documents validate.

A future real B15 pass path must refuse missing/unaccepted/mismatched bindings before protected lookup/effect, retain failed attempts and reject skips/zero tests/unrun lanes. Full installed Level 4 is required for Available at the exact accepted target. Consuming Level 5/RQ-1 follows its own launch gates. Keep the old accepted Committed/unavailable record unchanged; an incompletely applied changed commitment would be Uncommitted.
