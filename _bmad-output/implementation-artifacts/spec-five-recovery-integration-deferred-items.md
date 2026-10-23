---
title: 'Five recovery integration deferred items'
type: 'bugfix'
created: '2026-10-23'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '3f33e57432b301198770dc77bc0c240d22306fba'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Deferred integration gaps affect source assets, MCP authentication, interrupted projections, legacy erasure evidence and retained-history snapshots.

**Approach:** Implement the requested five-item batch: DW-116 and the unnumbered entries for MCP algorithm replacement, legacy normal-batch compaction, legacy erasure-certificate recovery and SDK retained-history snapshot capture. Review, verify and push the changes on main. The explicit five-item request retains these independently testable goals together.

## Boundaries & Constraints

**Always:** Keep shared persistence fixes in EventStore and MCP binding in Memories. Preserve authorization, cancellation, conditional writes, newer erasure state and default retry behavior. Recover certificates only from fresh authoritative key deletion/read-back and audit. Qualify source, package and runtime evidence separately.

**Never:** Promote ambiguous certificates, retain payload-bearing recovery manifests, silently discard snapshot fields, bypass source/warning gates, change package versions, deploy or fabricate owner approval. Synthetic custody does not qualify production retention/expiry. Existing publication, custody and deployment gates remain explicit.

## I/O & Edge-Case Matrix

| Given | When | Then |
|---|---|---|
| Selected FrontComposer source | Host serves its RCL script | Actual selected bytes and JavaScript content type match; framework-script success alone is insufficient. |
| Explicit asymmetric MCP algorithms | Registered bearer policy validates tokens | Only configured algorithms pass; absent configuration retains defaults; empty/invalid configuration fails. |
| Committed unguarded v1 batch without manifest | Recovery/redelivery precedes next tail | Original envelopes compact safely; next tail obtains writable ETags; newer/foreign values survive. |
| Ambiguous legacy certificate | Authorized explicit recovery retry | Fresh scoped verified evidence persists before cleanup; uncertainty cannot certify completion. |
| Complete supported Party snapshot JSON | SDK captures, persists and restores | Every modeled field/collection survives independent protection; malformed or lossy inputs fail closed. |

</frozen-after-approval>

## Code Map

Paths below are relative to the owning repository; EventStore and Memories are direct root references.

- Parties `tests/Hexalith.Parties.UI.Tests/PartiesUiHostCompositionTests.cs` and its csproj: framework-only asset test and existing build metadata.
- FrontComposer `src/Hexalith.FrontComposer.Contracts/Hexalith.FrontComposer.Contracts.csproj` and `src/Hexalith.FrontComposer.Schema/Hexalith.FrontComposer.Schema.csproj`: conditional canonical SDK assets-file path for outer multi-target source validation before restore.
- EventStore `src/Hexalith.EventStore.Client/Projections/ReadModelPendingBatchRecovery.cs`: missing-manifest branch; reuse content-free recovery anchors. Parties `src/Hexalith.Parties.Projections/Handlers/PartyDetailSdkProjectionHandler.cs` already invokes recovery before reading slots.
- Memories `src/Hexalith.Memories.Mcp/McpCompositionRoot.cs` and `Authentication/MemoriesMcpAuthenticationOptions.cs`: initialized-list binding. Reuse Server's explicit-list replacement pattern.
- Parties `src/Hexalith.Parties/Domain/PartyDomainProcessor.cs`: retry dispatch; Security `PartyKeyManagementService.cs` already deletes, reads back and audits; `PartyErasureOrchestrator.cs` and record store coordinate evidence.
- Parties Security `EventStorePartyPayloadProtectionAdapter.cs` and `PartyPayloadProtectionService.cs`: JSON/typed mismatch. Contracts `State/PartyState.cs` has 18 private-set properties and three getter collections. EventStore Server `Events/SnapshotManager.cs` supplies Web JSON.

## Tasks & Acceptance

**Execution:**

- [x] UI tests above: request `/_content/Hexalith.FrontComposer.Shell/js/fc-focus.js`; bind exact bytes to selected source and capture source-mode/root metadata. Qualify with pinned-tool mappings; package execution cannot certify source delivery.
- [x] MCP files above: replace explicit algorithm lists without breaking public init semantics or absent-setting defaults. Extend `tests/Hexalith.Memories.Mcp.Tests/ConfigureJwtBearerOptionsTests.cs` and actual registered integration/token tests, including invalid lists and HS256 rejection.
- [x] EventStore recovery above: validate committed unguarded v1 scope, marker and original envelope linkage; CAS-compact only owned envelopes and reuse content-free final anchors for restart safety. Reject prepared, aborting, guarded, retained, malformed and expiring unsupported cases. Extend Client.Tests `Projections/DaprReadModelBatchTests.PendingRecovery.cs` and Parties `tests/Hexalith.Parties.Tests/Projections/PartyDetailSdkProjectionRecoveryTests.cs` for interrupted recovery, next tail and superseding erasure bytes.
- [x] Parties Contracts `Commands/RetryErasureVerification.cs`, its validator and processor: add explicit recovery opt-in with default refusal preserved. Reuse key destruction/read-back/audit; validate and persist a fresh matching certificate before verification. Extend processor/security tests for remaining keys, outages, cancellation, foreign evidence, persistence failure and serialized restart. Refresh additive API snapshots as necessary.
- [x] Add Security `PartyStateSnapshotCodec.cs`: strictly restore only modeled PartyState fields and collections; reject recursive duplicates, unknown/lost/partial shapes and exceeded byte/depth limits. Support exact existing Web and Parties writer shapes through round-trip equality. Bridge adapter capture/restore to typed state while preserving the raw serialized-history guard. Extend `tests/Hexalith.Parties.Security.Tests/IdentityHistoryProtectionTests.cs` and add actual SDK SnapshotManager persisted capture/read-back plus post-restore command coverage in service tests, using synthetic custody and recreated storage.
- [x] Update `docs/architecture.md`, `tests/README.md`, deferred ledger and bounded batch receipts. Record exact results and unresolved external gates.

**Acceptance Criteria:**

- Given every matrix row, when its actual integration path runs, then focused tests prove the outcome and persisted end-state where applicable.
- Given completed fixes, when full affected suites and relevant build checks run, then regressions are resolved; unavailable prerequisites are recorded exactly and cannot justify a closure claim.

## Implementation Notes

Parent read the complete staged combined diff, verified all 27 code/config hashes and audited all five matrix rows against executed zero-failure, zero-skip covering lanes. Final publication operations are separate from the implementation checklist: after independent review, commit/publish owners before immutable root adoption and the authorized main push. Two clean-owner provenance tests are deliberately pending that final Git phase; all other affected service cases pass and no gate is weakened.

Parent captured the required isolated Aspire baseline before code edits and stopped the exact AppHost successfully. Parties exceeded its 30-second healthy wait; security subsequently passed its own wait. Sanitized evidence: `tests/five-recovery-integration-2026-10-23/aspire-baseline.json`. This does not qualify full-topology health.

User approved this unchanged frozen spec on 2026-10-23 with "approve and contiinue". This is the implementation handoff of an already-rendered workflow; do not rerun the skill renderer or execute planning/review phases. Implement the five tasks sequentially. The parent owns independent review and all final Git commits, source adoption and pushes; no remote operations during implementation.

Planning found no remaining product-choice gaps. Changes are reversible code/test changes; executing a deployed migration or certificate recovery is outside this implementation. Remote commits/pushes are explicitly requested. Keep the selected package catalog unchanged. Source qualification currently has recorded Platform HXR020 and focused mapping MSB4044 prerequisites; resolve through owning tooling or retain their exact failure evidence without weakening validation.

Implementation prerequisite: FrontComposer Contracts/Schema outer multi-target evaluations supplied no `ProjectAssetsFile` to pinned source validation. Both owner projects now conditionally supply the SDK's ordinary `$(MSBuildProjectExtensionsPath)project.assets.json` path when empty. The original negative MSB4044 and the successful fully validated source UI build are retained separately; generated mapping files and all source/warning gates remain unchanged.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Route | Evidence |
|---|---|---|---|
| B1 | medium | patch | Unsupported serialized profile format is returned unchanged by UnprotectProfileSnapshotStateAsync and then labeled Readable; reject the invalid wrapper instead. |
| B2 | high | patch | UnprotectNodeAsync returns unmarked primitive leaves unchanged, so newly admitted profile JSON can expose plaintext personal state without a key read. Reuse encrypted-payload shape validation before protected acceptance. |
| B3 | medium | patch | Codec deserialization permits null history collections and normalized round-trip equality preserves them; downstream history consumers dereference these collections. |
| B4 | medium | patch | Null history entries and binding records with null mandatory evidence survive normalization and reach history consumers. Enforce the demonstrated modeled requirements. |
| B5 | medium | patch | Unknown numeric enums deserialize and round-trip unchanged, admitting invalid modeled state. Check defined top-level and nested enum values. |
| B6 | medium | patch | The explicit recovery predicate checks only status; a matching Verified certificate with null/nonpositive versions remains permanently unusable. Include the existing evidence-validity requirements. |
| B7 | medium | patch | Fresh recovery depends on awaited audit success, but the new coordinator test never faults that prerequisite. Add persisted-state/no-cleanup coverage for audit failure. |
| B8 | medium | patch | The new fault matrix omits missing initial evidence and stale/future/nonpositive replacement evidence. Null replacement versions are also refused upstream by orchestrator logging; preserve that safe outcome in the contract test. |
| B9 | medium | patch | The new cancellation check after durable certificate save has no corresponding restart control; cover cancellation after the write and ordinary retry using saved evidence. |
| B10 | medium | patch | Legacy recovery contains direct and final-anchor delete branches; current new tests only write. Add mixed batches and interrupted delete-anchor persistence assertions. |
| B11 | medium | patch | Current erasure/foreign replacements occur before the participant scan; no new test forces a replacement between read and conditional compaction, including the final anchor. |
| E1 | medium | patch | Same verified unsupported-format acceptance as B1; the unchanged wrapper can become readable folded state. |
| E2 | medium | patch | Same null collection/entry admission as B3/B4; strict round-trip equality alone does not enforce modeled non-null history. |
| V1 | medium | patch | Pre-verified gap: the conditional compaction CAS race can be broken by refreshing the ETag without failing current pre-seeded erasure tests. Add the exact mid-recovery interleaving. |
| V2 | medium | patch | Pre-verified gap: neither stale nor future matching Verified replacements are tested; removing the new timestamp comparisons leaves current cases green. |
| B2R | medium | patch | Independent follow-up reproduced valid encrypted displayName plus plaintext person.firstName still Readable. The marker-presence patch fixes the original no-key probe but does not enforce the writer's complete personal-data policy; validate all sensitive leaves before unprotection. |

## Verification

Before code edits, establish and stop an exact owned Aspire baseline. Build individual affected projects using pinned-tool Debug mappings; run focused xUnit assemblies with single-dash class filters, then full affected suites. Check Release/package behavior independently. Exercise real SDK snapshot and batch persistence with serialized restart, and real key-service read-back. Run warning policy, CRLF-aware diff checks, pinned commitlint and staged-diff inspection; fetch and inspect divergence before owner-first main pushes.


Final acceptance: all 16 review findings are resolved, including independent confirmation of the mixed-profile fix. Source Security passes 334/334; final package service passes 2,671 with 12 explicit prerequisite skips and zero failures. Rebased source UI passes with its required asset case executed. Owner commits are published and adopted at a9f4edb5a8a769e36499063b5b88413af01a7bd0. Both clean-owner provenance checks and task gitlink gates pass. All owned runtime records were cleaned. Matching SDK package adoption, whole-topology Platform HXR020 and production custody/expiry remain explicit external gates. The user-authorized final Parties code commit/push is the last publication operation.
