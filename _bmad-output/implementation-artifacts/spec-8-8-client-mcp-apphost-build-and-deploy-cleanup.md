---
title: '8.8 Client, MCP, AppHost, build, and runtime-boundary cleanup'
type: 'refactor'
created: '2026-07-07T00:00:00+02:00'
planned: '2026-10-08'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md'
  - '{project-root}/_bmad-output/implementation-artifacts/story-8-8-readiness-2026-10-08.md'
  - '{project-root}/_bmad-output/implementation-artifacts/8-8-client-mcp-apphost-build-and-deploy-cleanup.md'
warnings:
  - blocked-prerequisite
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Shared runtime plumbing remains in Parties pending owner delivery and parity.

**Approach:** Adopt proven owner surfaces per slice, retaining compatibility and exercised rollback. Parties owns workloads, CI and immutable images; an approved platform AppHost owns integrated local topology; external operations owns deployment. G12 publication remains resolved.

## Boundaries & Constraints

**Always:** Apply the current spine, matrix and detailed implementation packet, reconciled by the readiness report. Production migration requires Story 8.7 closure or approved resequencing, plus named approvals, exact consumed identities, public/package inventory, producer/consumer parity and exercised rollback per gate. Refresh available Commons HTTP/Builds rows before consumption. Retain G1/G2 pending an explicit adoption/deferral disposition.

Preserve public Client/Contracts/RCL shapes, self-scope, domain behavior, freshness, error bounds, secret safety and the current deny-default/EventStore-only ACL tuples. Preserve the five existing MCP operations and soft-delete behavior until approved McpCli replacement/withdrawal; no get_party_name_at. Follow approved McpCli ownership rather than extending obsolete proprietary MCP plumbing. Keep .slnx, CPM and warning gates.

**Never:** Edit producer repositories/gitlinks or package versions under this consumer spec; migrate projection/query, crypto or G4 UI; add production manifests; trust tool/header identity; pass inbound MCP tokens/API keys downstream; disclose credentials, raw identity or downstream health/error details; delete unproven rollback paths. AppHost retirement also requires dependent deferrals to pass or be re-approved against its successor.

## I/O & Edge-Case Matrix

| Scenario | Required behavior | Failure outcome |
| --- | --- | --- |
| Client command/query | Compatible envelopes, paging, six freshness states including 200/304, route-ID authority and customizers | Bounded typed errors; fail closed |
| MCP outbound identity | Server-authoritative context, approved credential provider, single CR/LF-free replace-not-append headers; mandatory authorization gates | Missing/ambiguous context or credentials prevents downstream calls; no secrets |
| Admin link | Absolute HTTP/HTTPS base without user-info; preserve path/query; encode once | Blank/unsafe configuration returns typed unavailable |
| Named health probe | Timeout/cancellation/byte/depth bounds; Available/LocalOnly/Degraded mapping | Missing/wrong-type/malformed/oversized/disabled/non-success fails safely; cancellation propagates |
| Topology handoff | Healthy EventStore/Parties/Tenants; approved MCP/UI, optional-resource and Docker/Kubernetes/ACA map | Keep Parties AppHost until security/publish/continuity/rollback proof passes |

**Accepted decisions — 2026-10-08:** `do recommended` keeps 8.8 consumer-only with separate owner prerequisite planning. Require predecessor key-ring/cursor/payload compatibility, restart and switch-back proof; preserve identities/purposes/formats until 8.7 proves replacements. No invalidation is selected. Record the shared 8.7/8.8 continuity decision through I20 before activation; delivery/parity/release approval remains separate.

</frozen-after-approval>

## Code Map

- `story-8-8-readiness-2026-10-08.md` — current identities, APIs, missing proof, build failure and detailed file/test map.

## Tasks & Acceptance

**Execution:**
- [ ] `story-8-3-platform-api-prerequisite-matrix.md` — reconcile identities/modes and evidence; obtain owner/reviewer acceptance. Preserve G12; separately verify the owner's published DomainService API fix.
- [ ] `src/Hexalith.Parties.Client/{HttpPartiesCommandClient,HttpPartiesQueryClient}.cs` — adopt proven G6/Commons/G8-B mechanics.
- [ ] `src/Hexalith.Parties.Mcp/McpContextForwardingHandler.cs` — reconcile G11-A with approved McpCli transport; prove five-operation, identity/credential, authorization and no-secret parity.
- [ ] `src/Hexalith.Parties.AdminPortal/Services/{AdminPortalEventStoreAdminLinks,PartiesAdminPortalApiClient}.cs` — adopt G11-B/C independently; test all link/probe matrix edge cases before deletion.
- [ ] `src/Hexalith.Parties.AppHost/Program.cs` — adopt G8-A/B/C only after approved identity/topology/resource/publish proof; exercise rollback and continuity before retirement.
- [ ] `Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props` — adopt proven Builds rules; preserve gates and unsupported probes.
- [ ] `src/Hexalith.Parties.Authentication/PartiesClaimsTransformation.cs` — retire only after complete G7/G9 producer/consumer/identifier/host/UI/switch-back proof; update references and inventories atomically.
- [ ] `docs/architecture.md`, `docs/deployment-guide.md`, `docs/ci.md` — reconcile ownership and companion inventory; keep deployment external.

**Acceptance Criteria:**
- Given a missing sequence, identity, API, approval or parity prerequisite, when its slice is evaluated, then it remains blocked and retains rollback.
- Given a proven slice, when adopted, then existing public/domain/authorization contracts and the I/O matrix pass at the exact consumed identities before deletion.
- Given accepted topology handoff, when Parties AppHost retires, then dependent rollback deferrals and continuity are resolved, Parties keeps workload/publication ownership and external deployment manifests remain absent.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

§4: (1) Prerequisites: sequence and each named row. (2) Touched repo: Parties only. (3) Rollback: retain seams; build/test switch-back at stamped identities, then forward restore. (4) Lanes: readiness report and packet. (5) Non-goals: forbidden migrations above. (6) Parity: I1/I1a/I3-I12/I16-I20, including I8 payload/export/record/certificate continuity. Hard-gate each row; require Release/package/runtime proof.

### Block If — available-row identities

Before consuming an available prerequisite row, verify its exact selected
package version or source commit against the current prerequisite matrix.
Source receipts require a clean checkout at that immutable identity. Keep
the slice blocked if identity or build mode differs, evidence is missing or
stale, or fresh Release/package and runtime parity has not passed at the
selected identity. Availability alone does not authorize adoption; retain
rollback until the required owner/reviewer acceptance is recorded.

## Verification

- `dotnet build Hexalith.Parties.slnx -c Release -m:1` — package gate. The historical 3.115.0 CS1061 baseline is resolved; the separate [3.117.1 handoff receipt](tests/eventstore-3.117.1-consumer-2026-10-08/README.md) records a passing Release build. Runtime and migration gates remain required.
- `pwsh scripts/test.ps1 -Lane ci` and `pwsh scripts/test.ps1 -Lane topology` — publication/topology proof without skipped required checks.
- Build individual Client/Mcp/AdminPortal/Authentication/host/UI test projects; invoke xUnit v3 DLLs directly for the report's parity/package/switch-back tests. Require owner API/security/bounds and actual publish proof.
- Scoped Git whitespace and document checks — planning validation only.
