---
title: 'Qualify five deferred runtime and browser boundaries'
type: 'bugfix'
created: '2026-11-15'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '5dacf507003d861e6ed935a4298cafb34b5f6e9a'
context: ['AGENTS.md', 'docs/read-model-migration.md', 'tests/README.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Five deferred entries retain locally actionable qualification gaps after the latest package run. Large processing recovery has not crossed actual workload-process boundaries on the durable provider; the current Memories source host lacks its required private-session authority configuration; the browser asset verifier rejects FrontComposer 6.0.0 before download assertions execute.

**Approach:** Complete the following local portions, review and verify changes, then commit and push to main as requested. The five-entry batch contains three independently reviewable slices, each with its own evidence; no slice can certify another.

| Deferred entry | Concrete deliverable |
|---|---|
| DW-50: bounded processing history | Interrupt a 4,097-record generation, resume in another workload process, and verify bounded persisted buckets and complete history |
| DW-58: actor-to-SDK migration | Exercise that recovery in the owned real-provider backfill rehearsal, preserving actor rollback bytes, erasure and unrelated survivors |
| DW-82: authoritative Memories inventory | Bind the current-source runtime to private-session generation/exclusion custody; inspect exact-unit authority and inventory through recreation |
| DW-168: terminal Memories cleanup | Qualify delayed ingestion/deletion and physical erasure under that source authority, including retained receipts and unrelated survivors |
| Unnumbered FrontComposer browser deferral, source spec `spec-five-current-package-capability-deferrals.md`, VG1 | Measure served/restored 6.0.0 asset parity and execute real download/navigation-cancellation controls |

## Boundaries & Constraints

**Always:** Keep historical package receipts separate from current-source evidence. Bind synthetic authority to demonstrably exclusive disposable resources and retain sanitized custody evidence. Preserve strict runtime/compiler/cohort guards, unknown-version refusal, package rollback and SDK-owned persistence. Use run-with-temp for disposable jobs; retain results outside TMPDIR and await owned cleanup.

**Never:** Upgrade dependencies, weaken gates, invent production writer exclusion or inventory completeness, deploy, delete retained implementations, or close outer entries whose external acceptance remains unmet. Source-host qualification must not be presented as published-package proof. Aggregate one-shot recovery must not be presented as owner-authorized aggregate staged resume.

## I/O & Edge-Case Matrix

| Given | When | Then |
|---|---|---|
| Durable legacy history and an incomplete bucket generation | A separately exiting workload interrupts before canonical cutover | Original canonical bytes survive; persisted early buckets and operation identity remain recoverable |
| The same immutable input and operation in a new process | Recovery completes | Prior bucket bytes remain; all records, erasure fences and survivor hashes match |
| Exclusively owned current-source Memories resources | Authority is issued and cleanup crosses client recreation | Exact tenant/unit/generation provenance persists; terminal absence and unrelated survivors are inspected |
| Missing or mismatched authority | Refresh or cleanup attempts admission | Refuse certification without retiring obligations or mutating foreign state |
| Exact FrontComposer 6.0.0 assets | Export completes or navigation cancels it | Served bytes match restored assets; successful JSON downloads work and cancellation leaves no late download or feedback |

</frozen-after-approval>

## Code Map

- `tests/Hexalith.Parties.Tests/Projections/PartyProcessingGenerationRecoveryTests.cs`: existing 4,097-record interruption assertions use an in-memory byte store; reuse expected business outcomes.
- Same directory, `PartyLegacyBackfillRuntimeFixture.cs`, `PartyLegacyBackfillProcessRuntimeTests.cs`, `PartyLegacyBackfillWorkloadManifest.cs`: real SDK/Dapr provider, immutable histories and separate workload phases; currently small aggregate one-shot/shared staged proof.
- `_bmad-output/implementation-artifacts/tests/five-snapshot-history-and-publish-2026-11-11/run-owned-backfill.py`: reusable exclusive Redis/Dapr provisioning and exact cleanup.
- `tests/Hexalith.Parties.IntegrationTests/Topology/PartyMemoryOwnedRuntimeTests.cs`: private source topology and child identity guards; omits OwnershipGeneration/OwnershipWriterExclusionEvidenceId. Current source refused refresh; older package-matched host passed separately.
- `references/Hexalith.Memories/src/Hexalith.Memories.Server/Ingestion/UnitOwnershipAuthority.cs`: existing producer authority contract; consume it without changing the producer.
- `tests/Hexalith.Parties.Tests/Search/PartyMemorySourceInventoryTests.cs` and existing runtime cleanup controls: persisted obligations and authority negatives.
- `tests/e2e/helpers/frontcomposer-assets.ts`: exact 5.0.0 admission and four served-asset SHA-256 comparisons. `specs/consumer-portal-routes.spec.ts` and `helpers/consumer-download-cancellation.ts`: real export/cancellation behavior.

## Tasks & Acceptance

**Execution:**

- [x] Backfill fixture/process tests/manifest: add bounded large-history interruption and separate-process resume using the existing SDK batch observer; assert canonical preservation, generation identity, bucket bounds, complete results and survivor bytes.
- [x] Owned Memories runtime and associated inventory controls: add a distinct current-source authority profile with session-bound custody, persisted provenance observations and missing/mismatch refusal. Preserve the older package-matched profile independently.
- [x] Browser asset helper and existing export/cancellation specs: qualify exact 6.0.0 alongside retained 5.0.0, preserving unknown-package and byte-mismatch refusal; execute actual browser behavior.
- [x] `docs/read-model-migration.md`, `tests/README.md`, deferred ledger and a new dated receipt: record five bounded dispositions, exact source/package identities, commands, assembly/asset hashes, test XML and cleanup.
- [x] Complete independent review, relevant checks, pinned commitlint validation and divergence inspection; prepare the reviewed changes for the explicitly requested terminal commit/push and remote verification.

**Acceptance Criteria:**

- Given each selected entry, when its targeted lane runs, then retain nonzero executed passing coverage; skips or prerequisite refusal never count as qualification.
- Given independent slices, when a lane fails, then record its exact blocker separately and repair within scope before claiming that deliverable complete.
- Given local-only proof, when updating the ledger, then retain production custody, publication, writer-drain, migration approval, owner acceptance and deployment gates.

## Implementation Notes

User approved the complete scope on 2026-11-15. Implementation executes sequentially without remote operations, staging or commits; independent review and requested Git publication are parent follow-through after implementation. Do not reinvoke bmad-build; this is its implementation handoff.

Root main and declared submodules are clean. Behind-remote submodules stay unchanged. Approval confirms keeping three independent slices in the requested five-entry batch. Requested Git publication is the terminal external action. Footprint: approximately ten code/test files plus documentation and receipts; no new public API.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Route | Evidence |
|---|---|---|---|
| BH1 | medium | patch | Authority replacement precedes try/finally and WaitAsync can abandon its acknowledgement; cancellation can bypass restoration after a real mutation. Protect submission and reconcile original/altered state before returning. |
| BH2 | medium | patch | Standalone current-source child validation checks only three custody entries; it does not bind the direct Redis mutation endpoint to the named running search resource. Require that concrete binding. |
| BH3 | medium | patch | The refusal helper checks HTTP500/current bytes/local intents only. Those checks cannot distinguish a post-admission failure; inspect producer inventory and admission identities around the request. |
| BH4 | medium | patch | Producer IsComplete checks equality before tenant/unit identity; current foreign control leaves provenance unchanged, so it does not independently test the identity check. Mutate/restore a matching foreign pair. |
| BH5 | medium | patch | Existing consumer cleanup negatives do not execute the current-source exact-unit authority faults. Add cleanup refusal under those demonstrated missing/foreign faults while retaining obligations and remote values. |
| BH6 | medium | patch | Large detail is seeded with the final fold, so an early identical detail write is invisible. Seed distinguishable previous detail and retain its exact bytes until cutover. |
| BH7 | low | patch | A failed evidence copy skips Directory.Delete in the new finally block. Nest unconditional deletion and preserve evidence/qualification errors. |
| BH8 | medium | patch | New runner copies PLAYWRIGHT_SKIP_WEBSERVER/BASE_URL; config explicitly honors those overrides, allowing another host to supply behavior while local assemblies are hashed. Reject or clear the overrides and test invocation. |
| BH9 | medium | patch | subprocess.run timeout kills only the direct npm process; run-with-temp waits its foreground job and removes scratch without independently supervising descendants. Own and await command descendants on timeout. |
| BH10 | low | patch, parent finalization | Parent added matrix-audit.json after implementation sealing. Regenerate checksums after all parent review/publication evidence additions. |
| E1 | medium | patch | Independently confirms BH9: a reachable timeout can retain Playwright/dotnet/Chromium beyond scratch removal. Same command-ownership defect. |
| E2 | medium | patch | Independently confirms BH1: uncertain Redis mutation completion can bypass the restoration block. Same mutation-lifetime defect. |
| VG1 | medium | patch | Pre-verified runner invocation gap: actual config under inherited overrides selects external baseURL and no webServer. Same defect as BH8; existing config tests do not exercise the runner's environment. |

All thirteen findings were classified individually before grouping. BH1/E2, BH8/VG1 and BH9/E1 share roots; other corrections are independent. Small private harness/assertion fixes preserve the frozen intent and add no public surface. Parent owns final packet checksums.

## Verification

Inspect the Aspire baseline before code changes. Build individual projects with the pinned module workflow in disposable workspaces; use Debug for mapped source and Release for explicit package qualification. Execute built xUnit assemblies with focused single-dash selectors, -failSkips and fresh XML. Run the owned provider/current-source runtime with bound immutable inputs and completed cleanup. Run `npm --prefix tests/e2e run typecheck`, `run test:config`, then maintained Chromium export/cancellation controls with retries disabled and asset attachments. Finish with `bash scripts/check-no-warning-override.sh` and `git diff --check`. Record environmental blockers and follow the repository's narrow fallback ladder without bypasses.

Parent diff/matrix audit: all five rows have nonzero executed passing coverage in the retained XML, all 12 nested workload results pass, all captured source bytes match, and current-source/package scopes remain distinct. See the dated receipt matrix-audit.json. Independent review and authorized Git publication remain terminal follow-through.

Final review corrections resolve all thirteen classified findings. Parent final verification passes provider11/11 plus12 workload children, consumer/custody42/42, source wrapper1/1 and child1/1, Chromium3/3, config20/20 and runner2/2, with zero skips and successful owned cleanup. All five matrix rows and1,101 final code inputs verify. The initial parent bootstrap-path refusal is retained separately. Browser VG1 is done; DW-50/58/82/168 retain their external gates. User explicitly requested pushing main; this authorizes publication after the workflow's local completion and is not an automatic unrequested push.
