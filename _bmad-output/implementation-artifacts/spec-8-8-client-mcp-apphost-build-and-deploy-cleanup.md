---
title: '8.8 Client, MCP, AppHost, build, and deploy cleanup'
type: 'refactor'
created: '2026-07-07T00:00:00+02:00'
status: 'draft'
review_loop_iteration: 0
followup_review_recommended: false
context:
  - '{project-root}/_bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-8-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md'
warnings:
  - oversized
  - multiple-goals
  - blocked-prerequisite
---

<intent-contract>

## Intent

**Problem:** Parties still owns non-domain plumbing — client command envelopes + paging/freshness adapters, MCP context-forwarding/result plumbing, AppHost security/module helpers, build-root probing, ProblemDetails scrubbing, and platform-owned deploy assets — that belongs to Commons/EventStore/FrontComposer/Builds/platform-ops.

**Approach:** Adopt the replacement shared APIs from their owning modules so Parties packages and deploy assets describe only Parties-owned behavior; `deploy/k8s` keeps only Parties-owned assets; public package compatibility, MCP tool contracts, AppHost topology, and operator docs stay stable or are intentionally versioned — each row adopted only after its owner proof exists.

## Boundaries & Constraints

**Always:** Preserve spine invariants I5 (public `Client`/`Contracts` + RCL package contracts), I1 (gateway boundary, DAPR deny-by-default, only `eventstore -> POST /process`), I6 (command/query behavior, self-scoped consumer authorization), and I12 (build discipline). Keep the exactly-5 MCP tool contracts (`create_party`, `get_party`, `find_parties`, `update_party`, `delete_party`; no `get_party_name_at`). Keep the `deploy/` credential-leak poison-sweep green.

**Block If:** The 8.3 row **"Package publishing/source-mode CI" is `blocked` (G12)** — Commons/Tenants release owners must first publish the missing packages (`Hexalith.Commons.Http`, `Hexalith.Commons.ServiceDefaults`, `Hexalith.Tenants.Client`, `Hexalith.Tenants.Testing`) or bless source-mode CI. Also gated on `needs-additive-api` rows: "EventStore client envelopes/freshness/error codes" (G6), "Aspire publish helpers" (G8 `WithEventStoreJwtAuthentication(audience)` + granular typed-client registration), "MCP, deep-link, and search probes" (G11 tenant-header relay + MCP auth + deep-link + search probe), and "Tenant claims transformation" (G7/G9, to allow the `Hexalith.Parties.Authentication` deletion deferred from 8.4). Each must record owner approval + pin proof before its slice migrates.

**Never:** Do not migrate projection/query (8.6), crypto/DataProtection (8.7), or UI (8.9). Do not change public `Client`/`Contracts` shapes or MCP tool contracts non-additively. Do not widen the DAPR ACL. Do not commit secrets/tokens into `deploy/`. Do not weaken warnings-as-errors, `.slnx`, CPM, or the build gate.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|----------------------------|----------------|
| Typed client call | `IPartiesCommandClient`/`IPartiesQueryClient` call after envelope adoption | Same result/outcome/freshness semantics via shared envelopes | Bounded ProblemDetails, no leak |
| MCP tool call | One of the 5 tools with tenant/auth headers | Same tool contract; tenant/auth relayed via shared handler | Auth/tenant failure fails closed, no PII |
| AppHost topology | `dotnet aspire run` / publish after helper adoption | Same resource topology + security env; `eventstore`/`parties`/`tenants` healthy | Topology test unchanged |
| Deploy manifests | `deploy/k8s` after platform-asset move | Only Parties-owned assets remain; poison-sweep clean | DeployValidation lane green |

</intent-contract>

## Code Map

MOVE to owning modules (after each 8.3 row proof; keep local until then — I3):
- `src/Hexalith.Parties.Client/HttpPartiesCommandClient.cs`, `HttpPartiesQueryClient.cs`, `Paging/PartiesPagedResultAdapter.cs`, `Abstractions/PartiesCommandResult.cs` -- command envelopes, paging/freshness → EventStore client envelopes / Commons HTTP helpers.
- `src/Hexalith.Parties.Mcp/McpContextForwardingHandler.cs`, `Tools/PartiesMcpTools.cs`, `Tools/PartiesMcpToolResult.cs` -- MCP context/result plumbing → FrontComposer MCP (G11); keep the 5 tool definitions.
- `src/Hexalith.Parties.AppHost/Program.cs` -- AppHost security/module helpers + build-root probing → EventStore.Aspire publish helpers (G8).
- `Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props` -- build-root probing → Builds shared props/targets.
- `deploy/k8s/*`, `deploy/dapr/*` -- move platform-owned assets to platform-ops; keep Parties-owned only.

Evidence: `story-8-3-platform-api-prerequisite-matrix.md` (rows above), `tests/.../test-summary.md`, `sprint-status.yaml`.

## Tasks & Acceptance

**Execution:** (gated — G12 blocked row first, then each needs-additive-api row)
- [ ] `story-8-3-platform-api-prerequisite-matrix.md` -- record G12 resolution (publish/source-mode) + owner proof + pins for G6/G8/G11/G7-G9 rows.
- [ ] Adopt shared client envelopes/paging; keep local until parity; update `Client.Tests`/`Package` tests.
- [ ] Adopt shared MCP plumbing behind the 5 stable tools; update `Mcp.Tests` fitness.
- [ ] Adopt AppHost publish helpers + Builds props; verify topology + deploy validation.
- [ ] Delete `Hexalith.Parties.Authentication` (deferred from 8.4) only after the tenant-claims-transformation G7/G9 row is proven.

**Acceptance Criteria:**
- Given G12 is still `blocked`, when 8.8 is attempted, then it HALTs `blocked` with the package/source-mode prerequisite.
- Given proven rows, when clients/MCP/AppHost/deploy adopt shared surfaces, then public package contracts, the 5 MCP tools, AppHost topology, and the DAPR ACL are unchanged or intentionally versioned.
- Given `deploy/` after the move, when validated, then only Parties-owned assets remain and the credential poison-sweep is clean.

## Design Notes

- **§4 gate mapping:** (1) Prereq: G12 (`blocked`) then G6/G8/G11/G7-G9 (`needs-additive-api`). (2) Repos: `Parties` + `Hexalith.EventStore` + `Hexalith.Commons` + `Hexalith.FrontComposer` + `Hexalith.Builds` + `deploy`. (3) Rollback: MOVE files stay until each row proven; revert per slice. (4) Lanes: `Client`/`Mcp`/`Package` test EXEs directly, topology, deploy-validation. (5) Non-goals: 8.6/8.7/8.9. (6) Parity checklist: I5/I1/I6/I12.
- **Broad-story handling:** hard-gated per-row; may split into 8.8a (client), 8.8b (MCP), 8.8c (AppHost/build), 8.8d (deploy) at planning time — same gate.

## Verification

**Commands:**
- `dotnet build Hexalith.Parties.slnx -c Release -m:1` -- expected: green (subject to G12 package availability).
- `pwsh scripts/test.ps1 -Lane deploy` -- expected: DeployValidation + poison-sweep green.
- `pwsh scripts/test.ps1 -Lane topology` -- expected: AppHost topology + ACL unchanged.

**Manual checks:**
- Confirm the 5 MCP tool names are unchanged and `deploy/k8s` holds only Parties-owned assets.
