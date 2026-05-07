# Sprint Change Proposal - Memories Search Verification Addendum

**Project:** Hexalith.Parties  
**Date:** 2026-05-03  
**Mode:** Batch  
**Status:** Approved - implementation handoff ready  
**Change Trigger:** Three verification gaps remain after Story 9.6 review: the dedicated Memories topology smoke test, the `X-Parties-Search-Status: LocalOnly` HTTP-boundary test, and co-located unit tests for `PartyProjectionUpdateOrchestrator`.

## 1. Issue Summary

Story 9.6 is in `review` and the code review notes acknowledge that most Memories-backed party search patches have landed. However, three test gaps remain recorded as carry-over:

- `MemoriesPartySearchIntegrationTests.cs` smoke path against an Aspire topology, proving index -> Memories search -> Parties hydration -> authorization -> response metadata.
- An HTTP-boundary test proving default local search returns `X-Parties-Search-Status: LocalOnly`.
- Co-located unit tests for `PartyProjectionUpdateOrchestrator`, covering full-stream replay correctness, key-destroyed redaction fallback, event ordering by sequence number, and cancellation behavior.

The trigger is not a product-scope change. It is an acceptance and review-integrity issue: Task 7 in Story 9.6 is marked complete, while the review section still lists test work that directly validates the highest-risk behavior introduced by the story.

## 2. Impact Analysis

### Checklist Status

| Item | Status | Notes |
|---|---:|---|
| 1.1 Triggering story | [x] | Story 9.6: Hexalith.Memories-Backed Party Search. |
| 1.2 Core problem | [x] | Verification work is incomplete while Task 7 is marked complete. |
| 1.3 Evidence | [x] | Story 9.6 review lines list the three missing tests; `rg` found no dedicated `MemoriesPartySearchIntegrationTests.cs`, no HTTP-boundary `LocalOnly` header test, and no co-located orchestrator tests. |
| 2.1 Current epic impact | [x] | Epic 9 remains valid, but Story 9.6 should not move to `done` until the test addendum is implemented or explicitly accepted as a separate follow-up. |
| 2.2 Epic-level changes | [N/A] | No new epic needed. |
| 2.3 Remaining epic impact | [x] | Epic 10 depends on trustworthy search status semantics and rich/local search behavior. |
| 2.4 Future epic validity | [x] | No future epic is invalidated. |
| 2.5 Priority/order | [x] | Complete before Epic 9 retrospective and before relying on Memories search in admin UX work. |
| 3.1 PRD conflicts | [N/A] | PRD requirements remain correct. |
| 3.2 Architecture conflicts | [!] | Architecture test strategy requires Tier 3 full-topology confidence and Tier 1/Tier 2 focused coverage for projection orchestration. |
| 3.3 UI/UX conflicts | [N/A] | No UI artifact change. |
| 3.4 Other artifacts | [!] | Story 9.6 and sprint status need clear handling so carry-over is not lost. |
| 4.1 Direct adjustment | Viable | Add a test-only review addendum to Story 9.6. Effort: Low to Medium. Risk: Low. |
| 4.2 Potential rollback | Not viable | Rolling back Memories search would not solve the missing verification. |
| 4.3 MVP review | Not viable | MVP/v1.1 scope does not need reduction. |
| 4.4 Recommended path | [x] | Direct adjustment: keep Story 9.6 in review with blocking verification tasks. |
| 5.1-5.5 Proposal components | [x] | Captured below. |
| 6.1-6.5 Final review/handoff | [!] | Pending user approval. |

### Epic Impact

**Epic 9: GDPR Compliance (v1.1)** remains in progress. Story 9.6 should remain `review` and must not move to `done` until the verification addendum passes. This protects the v1.1 search and erasure boundary, because Memories-backed search stores searchable party data and must be covered by both full-topology and boundary tests.

**Epic 10: Administration & Frontend (v1.2)** is indirectly affected. Admin search and party picker work will depend on the `LocalOnly` / `Degraded` / rich-search status contract. The missing HTTP-boundary test should land before UI work treats these states as stable.

### Technical Impact

- Add one Tier 3 integration smoke test file under `tests/Hexalith.Parties.IntegrationTests/Search/`.
- Add HTTP-boundary coverage under `tests/Hexalith.Parties.Tests/Controllers/` or the existing search/controller test location.
- Add co-located orchestrator unit tests under `tests/Hexalith.Parties.Tests/Domain/`.
- No PRD, architecture decision, production code, or package-boundary change is required unless tests expose a defect.
- Do not initialize nested submodules recursively while setting up test topology; use existing root-level project composition and configured fixtures.

## 3. Recommended Approach

### Selected Path: Direct Adjustment

Add a test-only verification addendum to Story 9.6 and keep the story in `review` until the addendum is complete. This is better than opening a new epic or accepting the carry-over silently because all three gaps validate Story 9.6 acceptance criteria and review patches.

### Rationale

The missing work is narrow but high-signal:

- The Memories topology smoke test validates that the feature works beyond mocked service-level behavior.
- The `LocalOnly` HTTP-boundary test locks the public REST contract for disabled Memories integration.
- `PartyProjectionUpdateOrchestrator` unit tests protect the riskiest part of the implementation: replay, projection delivery, indexing side effects, cancellation, and crypto-shredding fallback behavior.

### Effort, Risk, Timeline

- **Effort:** Low to Medium, likely one focused developer pass.
- **Risk:** Low for test-only changes; Medium if tests reveal production defects in replay or search status handling.
- **Timeline impact:** Should be completed before Story 9.6 moves from `review` to `done`.
- **Scope classification:** Minor.

## 4. Detailed Change Proposals

### Story 9.6 - Task List

**OLD:**

```text
- [x] Task 7: Tests and docs
  - [x] 7.2 Integration test Memories-backed search with a fake or test fixture
  ...
  - [x] 7.8 Add one smoke path proving index -> Memories search -> Parties hydration -> authorization -> response metadata
```

**NEW:**

```text
- [x] Task 7: Tests and docs
  - [x] 7.1 Unit test mapper, hydration, fallback, and score metadata mapping
  - [x] 7.2 Service-level Memories-backed search coverage with a fake or test fixture
  - [x] 7.3 Test degraded fallback when Memories is unavailable
  - [x] 7.4 Test erasure cleanup and blocked cleanup reporting
  - [x] 7.5 Update getting-started, operations, and admin search documentation
  - [x] 7.6 Add REST and MCP parity tests for default hybrid, explicit syntactic, explicit semantic, graph-assisted, and fallback modes
  - [x] 7.7 Add hydration edge-case tests for stale IDs, deleted parties, unauthorized parties, wrong tenant/case, and duplicate hits

- [ ] Task 8: Review verification addendum
  - [ ] 8.1 Add `tests/Hexalith.Parties.IntegrationTests/Search/MemoriesPartySearchIntegrationTests.cs` with a smoke path proving index -> Memories search -> Parties hydration -> authorization -> response metadata.
  - [ ] 8.2 Add an HTTP-boundary test asserting the default local search response includes `X-Parties-Search-Status: LocalOnly` when Memories search is disabled.
  - [ ] 8.3 Add `PartyProjectionUpdateOrchestratorTests` co-located under Parties service domain tests for full-stream replay correctness, key-destroyed redaction fallback, sequence-number ordering, and cancellation.
```

**Rationale:** Keeps completed service-level tests truthful while making the remaining review obligations explicit and blocking.

### Story 9.6 - Review Remaining Items

**OLD:**

```text
##### Remaining patch items (deferred to follow-up)

- [ ] `MemoriesPartySearchIntegrationTests.cs` integration test scaffold against an Aspire topology ...
- [ ] `PartyProjectionUpdateOrchestratorTests` co-located unit tests ...
- [ ] Test asserting default REST response includes `X-Parties-Search-Status: LocalOnly` ...
```

**NEW:**

```text
##### Review verification addendum (blocking before done)

- [ ] `MemoriesPartySearchIntegrationTests.cs` integration smoke test against the available Aspire topology or an approved test fixture. Must prove index -> Memories search -> Parties hydration -> authorization -> response metadata.
- [ ] `PartyProjectionUpdateOrchestratorTests` co-located unit tests for full-stream replay correctness, key-destroyed redaction fallback, sequence-number ordering, and cancellation.
- [ ] HTTP-boundary test asserting default REST response includes `X-Parties-Search-Status: LocalOnly` when Memories search is disabled.
```

**Rationale:** These are not deferred product ideas; they are review verification gaps for the current story.

### Sprint Status

**OLD:**

```yaml
9-6-hexalith-memories-backed-party-search: review
```

**NEW:**

```yaml
9-6-hexalith-memories-backed-party-search: review
```

Add a note in the story file, not necessarily in `sprint-status.yaml`, that Story 9.6 is `review` with blocking verification addendum items. If the team wants file-based status to reflect active implementation instead, temporarily move it to `in-progress` while the tests are added, then return it to `review`.

**Rationale:** The status can remain `review` because these are review findings. The important control is that the story must not move to `done` until the addendum passes.

### Expected Test Files

```text
tests/Hexalith.Parties.IntegrationTests/Search/MemoriesPartySearchIntegrationTests.cs
tests/Hexalith.Parties.Tests/Controllers/PartiesControllerSearchHeaderTests.cs
tests/Hexalith.Parties.Tests/Domain/PartyProjectionUpdateOrchestratorTests.cs
```

Exact filenames may follow existing local conventions, but the three test responsibilities should remain separate enough that failures point to the right boundary.

## 5. Implementation Handoff

### Scope Classification

**Minor**

This can be handled directly by the Developer agent as a focused test hardening pass. No Product Manager or Architect replan is required unless the tests expose behavior that contradicts the approved Story 9.6 design.

### Route To

- **Developer agent:** implement the three test gaps and any production fixes they expose.
- **Test Architect / reviewer:** verify the smoke test is a real topology boundary test, not another unit-test duplicate.
- **Product Owner:** keep Story 9.6 out of `done` until the addendum is complete or explicitly waivered.

### Success Criteria

- Dedicated Memories topology smoke test exists and passes.
- REST local-only boundary test asserts `X-Parties-Search-Status: LocalOnly`.
- `PartyProjectionUpdateOrchestrator` has focused tests for replay, key-destroyed fallback, ordering, and cancellation.
- Full relevant test commands pass, including the new tests.
- Story 9.6 review section no longer lists these three items as missing.

## Approval

Approved by Jérôme on 2026-05-03.

## Workflow Handoff

**Scope classification:** Minor  
**Routed to:** Developer agent  
**Implementation responsibility:** Add the Story 9.6 review verification addendum tests, address any production defects exposed by those tests, and keep Story 9.6 out of `done` until the addendum passes.
