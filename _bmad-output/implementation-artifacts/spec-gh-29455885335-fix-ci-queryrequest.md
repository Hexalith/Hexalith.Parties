---
title: 'Fix package-mode HFC0001 CI failure'
type: 'bugfix'
created: '2026-07-16'
status: 'in-review'
review_loop_iteration: 0
baseline_commit: 'a88f458e42975bc705343d5db8bf962334c9cfda'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/references/Hexalith.FrontComposer/docs/diagnostics/HFC0001.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** GitHub Actions run `29455885335` restores successfully but fails its Release build because three Admin Portal queries use FrontComposer's deprecated flattened `QueryRequest` constructor in package mode. HFC0001 is promoted to an error, so tests and later CI stages never run.

**Approach:** Remove the obsolete package/source compatibility split and always compose list, search, and detail requests through `QueryRequest.Create` with `ProjectionQuery`. Make the focused tests use `request.Criteria` unconditionally and verify the canonical request retains the existing flat wire representation.

## Boundaries & Constraints

**Always:** Preserve list/search/detail projection types, bounded paging, filters, search text, EventStore routing metadata, cache discriminators, and the flat JSON contract. Keep warnings-as-errors enabled and preserve all unrelated worktree changes.

**Ask First:** Changing FrontComposer package versions, submodule pointers, dependency-routing policy, shared CI workflows, or public query contracts requires approval.

**Never:** Do not suppress HFC0001, weaken the build gate, modify a submodule, retain a legacy constructor fallback, or broaden this into the Epic 8 projection/query migration.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Release package mode | FrontComposer Contracts `3.1.1` with source-mode constant absent | List, search, and detail requests compile through `QueryRequest.Create` without HFC0001 | Build remains failed for any unrelated warning/error |
| Source mode | FrontComposer project references enabled | The same canonical code and assertions compile; no conditional behavior remains | API incompatibility fails compilation visibly |
| Query serialization | Canonical request with criteria and routing/cache metadata | JSON remains flat, contains the mapped criteria, and omits a nested `criteria` member | Test fails on wire-shape drift |

</frozen-after-approval>

## Code Map

- `Directory.Build.props` -- Owns the now-obsolete `HEXALITH_FRONTCOMPOSER_CANONICAL_QUERY` definition.
- `src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs` -- Contains the three package/source query construction branches that failed CI.
- `tests/Hexalith.Parties.AdminPortal.Tests/Services/PartiesAdminPortalApiClientTests.cs` -- Covers list, search, paging, detail routing, and captured `QueryRequest` values.
- `tests/Hexalith.Parties.Client.Tests/AdminPortal/AdminPortalQueryContractTests.cs` -- Covers the consumer-facing paging/search query contract.

## Tasks & Acceptance

**Execution:**
- [x] `Directory.Build.props` -- remove the canonical-query compilation symbol because supported package and source versions now share one API.
- [x] `src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs` -- delete the three legacy branches and keep their existing canonical `ProjectionQuery` mappings unchanged.
- [x] `tests/Hexalith.Parties.AdminPortal.Tests/Services/PartiesAdminPortalApiClientTests.cs` -- make `Criteria` assertions unconditional and add representative flat-JSON regression coverage.
- [x] `tests/Hexalith.Parties.Client.Tests/AdminPortal/AdminPortalQueryContractTests.cs` -- remove legacy flattened-property assertions so package-mode test compilation also stays HFC0001-free.

**Acceptance Criteria:**
- Given the dependency mode is package or source, when the Admin Portal and Client tests compile, then no production or test code uses the deprecated flattened `QueryRequest` surface.
- Given CI's exact Release build command, when the solution builds with warnings as errors, then the three HFC0001 failures from run `29455885335` are absent.
- Given list, search, and detail requests, when existing focused tests inspect them, then criteria and routing/cache metadata match their pre-fix semantics.

## Spec Change Log

## Design Notes

FrontComposer `3.1.1` adds `ProjectionQuery` and `QueryRequest.Create` while retaining a converter that serializes canonical criteria into the v1.12-compatible flat JSON shape. The compatibility constant was tied to source-reference selection rather than API availability, so Release selected deprecated code even though its package supported the replacement.

## Verification

**Commands:**
- `dotnet restore Hexalith.Parties.slnx` -- expected: package-mode restore succeeds.
- `dotnet build tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj --no-restore --configuration Release -warnaserror -m:1` followed by its built xUnit v3 assembly filtered to `PartiesAdminPortalApiClientTests` -- expected: focused tests pass.
- `dotnet build tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj --no-restore --configuration Release -warnaserror -m:1` followed by its built xUnit v3 assembly filtered to `AdminPortalQueryContractTests` -- expected: focused tests pass.
- `dotnet build Hexalith.Parties.slnx --no-restore --configuration Release -warnaserror -m:1` -- expected: exact failed CI gate passes without HFC0001.
