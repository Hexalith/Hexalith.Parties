---
title: 'Fix CI evidence drift and search latency'
type: 'bugfix'
created: '2026-08-01'
status: 'done'
review_loop_iteration: 0
baseline_commit: '3295560a1f883a0303bc85556082530a2956c8cd'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/spec-gh-30703092444-fix-ci-prerequisite-matrix.md'
  - '{project-root}/_bmad-output/planning-artifacts/sprint-change-proposal-2026-08-01.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** GitHub Actions run `30708560778` builds and passes Tier 1, but Tier 2 fails because Parties still validates the previous EventStore payload-protection status after commit `3295560` advanced the gitlink, and the local fallback search performs redundant full-phrase fuzzy work that left its 500 ms NFR gate with insufficient hosted-runner margin.

**Approach:** Atomically revalidate the living G5 matrix, retention ledger, and fitness contract against EventStore `4bcf2484`, while preserving G5 as unavailable until its runtime and closure proof ship. Align local multi-token matching with the existing semantic-provider rule: full phrases retain exact/prefix/contains matching, while Jaro-Winkler runs only for individual tokens.

## Boundaries & Constraints

**Always:** Preserve the user-owned Memories spec and submodule changes; retain the exact 500 ms performance threshold; preserve single-token fuzzy and multi-token exact behavior; record EventStore Story 8.1 as approved, authorized, and done while Stories 8.2–8.11 remain backlog; keep the G5 row `needs-additive-api`, Parties Story 8.7 blocked, and the crypto-retention action open.

**Ask First:** Any EventStore or Memories submodule content/pointer change, shared Builds workflow edit, performance-threshold change, payload-package inventory change, G5 promotion, Story 8.7 activation, or local crypto/key-management deletion requires explicit approval.

**Never:** Do not revert the approved EventStore specification, suppress/skip either failed test, relax fitness evidence, classify planning authorization as runtime delivery, remove token-level fuzzy matching, or hide the latency failure with retries or a larger timeout.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Current G5 owner state | EventStore `4bcf2484`; Story 8.1 done; Stories 8.2–8.11 backlog; packages and closure packet absent | Evidence commands reproduce the approved authorization and missing runtime while G5 remains unavailable | Any future identity, status, package, or closure drift fails the fitness gate |
| Multi-token local search | Exact or misspelled phrase such as `Jean Dupont` or `Jean Dupnt` | Full phrase uses deterministic matching; individual tokens retain fuzzy matching, result ordering, metadata, and paging | Cancellation and invalid paging retain existing fail-fast behavior |

</frozen-after-approval>

## Code Map

- `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md` -- living G5 owner/consumer evidence and reproducible validation commands.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` -- open Parties crypto-retention decision that must match the matrix.
- `tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs` -- exact gitlink, owner-status, missing-delivery, rollback, and command-reproduction contract.
- `src/Hexalith.Parties/Search/LocalFuzzyPartySearchProvider.cs` -- local display-name matching hot path.
- `tests/Hexalith.Parties.Tests/Search/LocalFuzzyPartySearchProviderTests.cs` -- search-result and match-mode regression coverage.
- `tests/Hexalith.Parties.Tests/Search/LocalFuzzySearchPerformanceBenchmarkTests.cs` -- unchanged hard 10K/500 ms NFR gate.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md` and `_bmad-output/implementation-artifacts/sprint-status.yaml` -- replace the stale `8f004ecf` draft-state narrative with exact `4bcf2484` authorization/status evidence while preserving missing runtime, rollback, and blocked-G5 truth.
- [x] `tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs` -- pin the current SHA/describe/status tokens and keep fail-closed checks for absent package projects, absent Story 8.11 closure, retained Parties code, and the open ledger action.
- [x] `src/Hexalith.Parties/Search/LocalFuzzyPartySearchProvider.cs` -- disable fuzzy evaluation only for the synthetic full multi-token candidate, mirroring `SemanticPartySearchProvider`; retain deterministic phrase and per-token fuzzy evaluation.
- [x] `tests/Hexalith.Parties.Tests/Search/LocalFuzzyPartySearchProviderTests.cs` -- prove exact multi-token and misspelled-token searches retain expected results and metadata after the optimization.

**Acceptance Criteria:**
- Given EventStore `4bcf2484` and unchanged payload runtime/package inventory, when the prerequisite fitness class runs, then all twelve tests pass and no evidence claims G5 runtime availability.
- Given the optimized local provider, when its functional and isolated performance classes run repeatedly, then matching behavior remains green and every 10K hard-gate sample stays below 500 ms without changing the threshold.
- Given the CI-equivalent MTP Tier 2 project run, when all 455 Parties tests and both remaining integration projects execute, then both run `30708560778` failure signatures are absent and every Tier 2 project passes.

## Spec Change Log

## Design Notes

`SemanticPartySearchProvider` already distinguishes its synthetic full query from individual tokens. Reusing that rule removes redundant Jaro-Winkler and word-splitting work for each nonmatching entry without adding a new search policy. The phrase still participates in exact, prefix, and contains scoring; spelling tolerance remains on each real query token.

## Verification

**Commands:**
- `dotnet restore Hexalith.Parties.slnx -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` -- expected: solution dependencies restore successfully.
- `dotnet build Hexalith.Parties.slnx --configuration Release --no-restore -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` -- expected: zero warnings and errors.
- `dotnet tests/Hexalith.Parties.Tests/bin/Release/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.PlatformApiPrerequisitesTests` -- expected: 12/12 pass.
- `dotnet tests/Hexalith.Parties.Tests/bin/Release/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.Search.LocalFuzzyPartySearchProviderTests` -- expected: all functional search tests pass.
- `for iteration in 1 2 3 4 5; do dotnet tests/Hexalith.Parties.Tests/bin/Release/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.Search.LocalFuzzySearchPerformanceBenchmarkTests || exit 1; done` -- expected: 4/4 pass each run and every hard 10K measurement remains below 500 ms.
- `dotnet test --project tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --no-restore -- --minimum-expected-tests 455` -- expected: 455/455 pass at the root-recorded EventStore gitlink.
- `dotnet test --project tests/Hexalith.Parties.Sample.Tests/Hexalith.Parties.Sample.Tests.csproj --configuration Release --no-restore -- --minimum-expected-tests 58` -- expected: 58/58 pass.
- `dotnet test --project tests/Hexalith.Parties.Ci.Tests/Hexalith.Parties.Ci.Tests.csproj --configuration Release --no-restore -- --minimum-expected-tests 31` -- expected: 31/31 pass.
- `bash scripts/check-no-warning-override.sh` and `git diff --check` -- expected: safeguards and whitespace checks pass.

**Observed:** Release build completed with zero warnings and errors. Functional search passed 30/30, the isolated performance class passed 4/4 in five consecutive post-review runs, and the root-gitlink-aligned Tier 2 run passed Parties 455/455, Sample 58/58, and CI 31/31. Across implementation and review runs, the changed 10K case measured 37-79 ms against the unchanged 500 ms gate.

## Suggested Review Order

**Search hot path**

- Gate only synthetic full-phrase fuzzy work while retaining deterministic phrase matching.
  [`LocalFuzzyPartySearchProvider.cs:192`](../../src/Hexalith.Parties/Search/LocalFuzzyPartySearchProvider.cs#L192)

- Prove real token fuzziness survives the full-phrase optimization.
  [`LocalFuzzyPartySearchProviderTests.cs:89`](../../tests/Hexalith.Parties.Tests/Search/LocalFuzzyPartySearchProviderTests.cs#L89)

- Lock phrase prefix/contains ordering across actual page boundaries.
  [`LocalFuzzyPartySearchProviderTests.cs:107`](../../tests/Hexalith.Parties.Tests/Search/LocalFuzzyPartySearchProviderTests.cs#L107)

**G5 evidence alignment**

- Start with the living matrix's exact owner state and fail-closed commands.
  [`story-8-3-platform-api-prerequisite-matrix.md:43`](story-8-3-platform-api-prerequisite-matrix.md#L43)

- Confirm retention stays open despite approved planning authorization.
  [`sprint-status.yaml:256`](sprint-status.yaml#L256)

- Pin the repository-recorded EventStore identity used by CI.
  [`PlatformApiPrerequisitesTests.cs:17`](../../tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs#L17)

- Parse authoritative frontmatter instead of accepting historical literal matches.
  [`PlatformApiPrerequisitesTests.cs:1106`](../../tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs#L1106)

**Review follow-ups**

- Preserve non-blocking allocation and relevance concerns for focused follow-up.
  [`deferred-work.md:71`](deferred-work.md#L71)
