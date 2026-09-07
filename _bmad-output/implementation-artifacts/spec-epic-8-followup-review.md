---
title: 'Epic 8 follow-up review'
type: 'bugfix'
created: '2026-09-07'
status: 'ready-for-dev'
review_loop_iteration: 0
followup_review_recommended: false
baseline_revision: '971eeab7c1a9f242a46cd0326db7c5dee6f67404'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-8-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-8-2-identifier-correctness-and-zero-risk-hygiene.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-8-3-platform-api-prerequisites.md'
warnings: [multiple-goals, oversized]
deferred: []
---

<intent-contract>

## Intent

**Problem:** The generic follow-up recommendations for completed Stories 8.2 and 8.3 have not been discharged against the current implementation. Independent inspection found that MCP scalar identifiers can be normalized past the Story 8.2 whitespace rejection rule, while the Story 8.3 evidence gate contains stale source receipts, skips its completed revision range, and contradicts two APIs present in the selected EventStore source.

**Approach:** Apply only the bounded identifier and prerequisite-evidence corrections proven by the fresh review, add focused regression coverage, validate current package/source identities without changing prerequisite dispositions, and retire both source-spec follow-up flags after the evidence is green.

## Boundaries & Constraints

**Always:** Preserve legacy GUID, ULID, and readable semantic-ID compatibility; reject whitespace-containing scalar IDs without echoing their values or calling clients; retain CSV delimiter-whitespace behavior; distinguish committed gitlink identities from package catalog identities and from uncommitted checkout state; validate the historical Story 8.3 `baseline_revision..final_revision` range after completion; keep every currently unresolved prerequisite row and rollback gate unresolved unless this run produces its required owner proof; preserve unrelated user/external worktree changes.

**Never:** Do not edit `_bmad-output/implementation-artifacts/deferred-work.md`, `.bmad-loop/**`, submodule content or gitlinks, package versions, `.gitlink-signoff.tsv`, sprint status, test summaries, or unrelated Epic 8 production surfaces. Do not reinterpret a newer gitlink as owner approval, validate a completed Story 8.3 range against current HEAD, trim scalar IDs into validity, or promote a matrix row merely because one previously missing API is now present.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| MCP scalar identifier | A party, contact-channel, identifier, or scalar removal ID has leading or trailing whitespace | The input remains unnormalized and is rejected before query or command client access | Return the existing bounded `validation_failed` result without the raw ID |
| MCP CSV removal identifiers | A comma-delimited removal list contains delimiter-adjacent whitespace but each trimmed item is otherwise valid | Preserve the established `TrimEntries` list parsing and dispatch the exact parsed identifiers | Invalid or empty parsed items use the bounded validation failure path |
| Completed Story 8.3 scope | Both recorded baseline and final revisions exist | Inspect `baseline_revision..final_revision` and reject forbidden production migration paths | Missing revisions or forbidden paths fail closed; later authorized HEAD changes are irrelevant |
| Selected dependency receipts | Parent gitlinks and clean selected checkouts identify the current source graph | Matrix prose and executable constants match exact full SHAs/describes while package catalog values remain unchanged | A parent/checkout/receipt mismatch fails the focused fitness gate |
| EventStore API evidence | `AddEventStoreDaprHealthChecks` and `WithEventStoreJwtAuthentication` exist in selected source | Matrix records the delivered symbols and narrows the remaining proof gap without changing row status | Retain local rollback paths and `needs-additive-api` until all row-specific proof exists |

</intent-contract>

## Code Map

- `src/Hexalith.Parties.Mcp/Tools/PartiesMcpTools.cs:202,528-531,935-950` -- scalar party/child/removal identifiers are trimmed before validation; CSV parsing at `SplitCsv` is the intentional exception to preserve.
- `tests/Hexalith.Parties.Mcp.Tests/PartiesMcpToolDispatchTests.cs:361-379,659-700` -- extend structured no-call coverage for whitespace-bearing create, update, and scalar removal IDs.
- `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md:51-108` -- refresh five committed source receipts and replace false missing-API statements while preserving package identities, row statuses, proof gaps, and rollback decisions.
- `tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs:22-34,139-215,630-868,1357-1442` -- refresh exact receipt constants/evidence tokens and make the no-production-migration gate validate completed historical revisions instead of returning early.
- `docs/architecture.md:56-57` -- align EventStore and Tenants source-pin documentation with the committed gitlinks; do not claim a new owner sign-off.
- `_bmad-output/implementation-artifacts/spec-8-2-identifier-correctness-and-zero-risk-hygiene.md` and `_bmad-output/implementation-artifacts/spec-8-3-platform-api-prerequisites.md` -- after successful focused validation, set `followup_review_recommended: false` and append the bounded follow-up outcome without changing historical revisions.
- `references/Hexalith.EventStore/**`, `references/Hexalith.Builds/**`, `references/Hexalith.FrontComposer/**`, `references/Hexalith.Memories/**`, and `references/Hexalith.Tenants/**` -- read-only symbol, gitlink, describe, and package/source evidence; never edit or move these checkouts.
- `_bmad-output/implementation-artifacts/deferred-work.md` and `.bmad-loop/**` -- prohibited write targets; orchestration owns ledger resolution.

## Tasks & Acceptance

**Execution:**
- `src/Hexalith.Parties.Mcp/Tools/PartiesMcpTools.cs` and `tests/Hexalith.Parties.Mcp.Tests/PartiesMcpToolDispatchTests.cs` -- stop trimming scalar semantic IDs before shared validation, preserve CSV parsing semantics, and cover bounded no-client failures -- restores Story 8.2's exact-input safety contract.
- `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md`, `tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs`, and `docs/architecture.md` -- refresh current committed source receipts, record the two delivered EventStore symbols, narrow remaining gaps, and enforce completed historical scope -- makes Story 8.3 evidence reproducible without granting approval or widening migration scope.
- `_bmad-output/implementation-artifacts/spec-8-2-identifier-correctness-and-zero-risk-hygiene.md` and `_bmad-output/implementation-artifacts/spec-8-3-platform-api-prerequisites.md` -- retire their generic follow-up flags only after focused verification passes and record this run's concrete disposition -- prevents an unverified administrative closure.
- `_bmad-output/implementation-artifacts/spec-epic-8-followup-review.md` -- record any newly discovered valid but out-of-bounds finding in `deferred` with exact evidence; otherwise leave `deferred` empty -- keeps any new deferral specific without touching the orchestrator-owned ledger.

**Acceptance Criteria:**
- Given any scalar MCP semantic identifier containing leading or trailing whitespace, when the tool validates it, then it returns the existing bounded validation failure and neither query nor command client is accessed.
- Given a comma-delimited MCP removal list with delimiter-adjacent whitespace, when it is parsed, then valid trimmed list items continue through the established dispatch path.
- Given the completed Story 8.3 spec, when the no-production-migration fitness test runs, then it validates the recorded baseline-to-final range and fails closed for missing revisions or forbidden paths rather than returning because status is `done`.
- Given current parent gitlinks, selected checkout revisions, and central package values, when the prerequisite fitness suite runs in a clean selected-source graph, then exact source receipts match and package identities remain EventStore `3.102.0`, Commons `2.30.0`, Memories `2.26.1`, Tenants `5.7.0`, and Parties `1.1.1`.
- Given the selected EventStore source exposes DAPR health registration and audience-aware JWT configuration, when the matrix is inspected, then it cites reproducible evidence for those symbols while retaining the remaining degraded-response, granular-client, integrated-topology, and rollback proof gates.
- Given all bounded patches and focused checks pass, when the two completed source specs are inspected, then both follow-up flags are false and neither the deferred-work ledger nor `.bmad-loop` files changed.

## Spec Change Log

## Review Triage Log

## Design Notes

The recorded `final_revision` values are review commits on historical branches rather than ancestors of current HEAD. Story 8.3's completion guard must therefore use its recorded `baseline_revision..final_revision` pair; applying its baseline to current HEAD would reject later authorized Epic 8 migrations. Source receipt updates represent the immutable graph selected by the parent repository only, not renewed owner approval.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.Parties.Mcp.Tests/Hexalith.Parties.Mcp.Tests.csproj -c Debug --no-restore -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` -- expected: succeeds without moving any submodule checkout.
- `dotnet ./tests/Hexalith.Parties.Mcp.Tests/bin/Debug/net10.0/Hexalith.Parties.Mcp.Tests.dll -class Hexalith.Parties.Mcp.Tests.PartiesMcpToolDispatchTests` -- expected: all MCP dispatch tests pass, including scalar-whitespace no-call and CSV compatibility cases.
- `dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Debug --no-restore -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` -- expected: succeeds without moving any submodule checkout.
- `dotnet ./tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.FitnessTests.PlatformApiPrerequisitesTests -class Hexalith.Parties.Tests.FitnessTests.IdentifierHygieneFitnessTests` -- expected: both focused fitness classes pass.
- `git ls-files '*.csproj.lscache' '*.lscache'` and targeted `rg` scans for semantic GUID parsing/generation -- expected: no tracked caches or forbidden semantic GUID usage.
- `git diff --check` plus explicit status/diff inspection limited to this spec's Code Map and prohibited paths -- expected: no whitespace errors, ledger edits, submodule/gitlink changes, or overwritten concurrent work.
