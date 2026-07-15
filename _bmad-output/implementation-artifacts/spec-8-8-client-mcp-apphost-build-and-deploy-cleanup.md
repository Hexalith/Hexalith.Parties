---
title: '8.8 Client, MCP, AppHost, build, and runtime-boundary cleanup'
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

**Problem:** Parties still owns non-domain plumbing — client command envelopes + paging/freshness adapters, MCP context-forwarding/result plumbing, AppHost security/module helpers, build-root probing, ProblemDetails scrubbing, and stale local-topology ownership — that belongs to Commons/EventStore/FrontComposer/Builds/platform-ops.

**Approach:** Adopt the replacement shared APIs from their owning modules so Parties packages and runtime-boundary documentation describe only Parties-owned behavior. Parties retains workload source, CI, and immutable image publication; FrontComposer.AppHost / an approved platform AppHost owns integrated local topology after parity; external platform operations owns runtime deployment. Public package compatibility, MCP tool contracts, AppHost topology, and operator docs stay stable or are intentionally versioned — each row is adopted only after its owner proof exists.

## Boundaries & Constraints

**Always:** Preserve spine invariants I5 (public `Client`/`Contracts` + RCL package contracts), I1 (gateway boundary, DAPR deny-by-default, only `eventstore -> POST /process`), I6 (command/query behavior, self-scoped consumer authorization), and I12 (build discipline). Keep the exactly-5 MCP tool contracts (`create_party`, `get_party`, `find_parties`, `update_party`, `delete_party`; no `get_party_name_at`). Preserve CI secret-safety and the rule that production deployment manifests remain outside this repository.

**Block If:** G12 **"Package publishing/source-mode CI" was resolved by package publication on 2026-07-11**; Story 8.8 must consume and validate those approved package identities but is no longer blocked on G12 delivery. The story remains independently gated on `needs-additive-api` rows: "EventStore client envelopes/freshness/error codes" (G6), "Aspire publish helpers" (G8), "MCP, deep-link, and search probes" (G11), and "Tenant claims transformation" (G7/G9, to allow the `Hexalith.Parties.Authentication` deletion deferred from 8.4). G8 requires (A) an EventStore.Aspire owner-approved `WithEventStoreJwtAuthentication(audience)` helper or documented/tested `WithJwtBearerSecurity(..., audience)` replacement covering local and publish/external-orchestrator settings, (B) EventStore.Client granular typed-client registration that preserves module clients, and (C) FrontComposer.AppHost / approved platform AppHost ownership and parity proof for the full integrated topology. Package C must explicitly disposition `parties-mcp`, standalone `parties-ui` versus `frontcomposer-ui`, and Docker/Kubernetes/ACA publish-target behavior; runtime deployment remains external platform-operations work. G11 requires (A) FrontComposer.Mcp outbound relay composed from server-resolved authenticated tenant/user context, with Commons.Http limited to CR/LF-free single-value replace-not-append header mechanics, approved bearer-credential sourcing, no MCP API-key reuse downstream, explicit host credentials or fail-closed behavior for API-key calls, no-secret diagnostics, and unchanged mandatory tenant-tool/resource-visibility gates; (B) a FrontComposer EventStore Admin UI aggregate/stream and correlation deep-link builder over optional Commons URI mechanics, with absolute safe base URI, path/query preservation, single encoding, and typed unavailable outcomes; and (C) Commons-bounded HTTP/JSON named-health-check extraction plus a FrontComposer UI-facing capability result, with cancellation/timeouts/size limits and fail-closed Available/LocalOnly/Degraded mapping without raw downstream detail. Each gated slice must record named owner approval, exact reusable-library release/root-submodule identity, exact FrontComposer source commit or approved host-artifact identity where applicable, public API/package validation, producer/consumer evidence, and rollback before it migrates. Delivery of one G11 sub-surface does not unblock deletion of another.

**Block If — available-row identities:** HALT the relevant 8.8 slice unless the `available` Commons HTTP helpers and Builds shared props/targets rows each record a package release or root-declared submodule gitlink matching the dependency identity selected by 8.8. A different selected identity must be written to and revalidated in the row before Parties-local HTTP/build helpers are deleted. These rows need identity validation, not additive APIs.

**Never:** Do not migrate projection/query (8.6), crypto/DataProtection (8.7), or G4 UI components/presentation (8.9); Story 8.8 may adopt the G11 deep-link and capability-probe service plumbing without performing UI consolidation. Do not change public `Client`/`Contracts` shapes or MCP tool contracts non-additively. Do not widen the DAPR ACL. Do not trust tenant/user tool arguments or raw passthrough headers, reuse a FrontComposer MCP API key downstream, log tokens/raw identity/health payloads, commit secrets/tokens, or reintroduce production deployment manifests. Do not weaken warnings-as-errors, `.slnx`, CPM, or the build gate.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|----------------------------|----------------|
| Typed client call | `IPartiesCommandClient`/`IPartiesQueryClient` call after envelope adoption | Same result/outcome/freshness semantics via shared envelopes | Bounded ProblemDetails, no leak |
| MCP tool call | One of the 5 tools with tenant/auth headers | Same tool contract; tenant/auth relayed via shared handler | Auth/tenant failure fails closed, no PII |
| MCP API-key call | FrontComposer MCP API-key identity plus downstream EventStore call | Server-resolved tenant/user is used; the MCP key is never reused downstream; an explicit host credential provider supplies any downstream credential | Missing/ambiguous context or credential-provider failure prevents the downstream call and returns a bounded failure |
| Existing outbound identity headers | Request already contains tenant/user/authorization values before the shared relay | Approved server context deterministically replaces identity headers as single CR/LF-free values | Never append duplicates; unsafe values fail closed and are not logged |
| EventStore Admin deep link | Configured base URI/path plus aggregate or correlation id | Absolute HTTP/HTTPS link preserves safe base path/query and encodes the value once | Blank id, unsafe scheme/user-info, or invalid configuration yields typed unavailable; no malformed link |
| Search capability probe | Named health result is healthy, disabled, degraded, missing, malformed, oversized, slow, or non-success | Bounded Available/LocalOnly/Degraded result through the shared probe | Cancellation propagates; timeout/size/JSON failures expose no raw response or exception detail |
| AppHost topology | `dotnet aspire run` / publish after helper adoption | Same resource topology + security env; `eventstore`/`parties`/`tenants` healthy | Topology test unchanged |
| Runtime boundary | Parties workload images after topology-owner migration | Parties retains source, CI, and immutable image publication; the platform AppHost owns integrated local topology and the external orchestrator owns runtime deployment | CI publication contract and topology lanes green; no production manifests reintroduced |

</intent-contract>

## Code Map

MOVE to owning modules (after each 8.3 row proof; keep local until then — I3):
- `src/Hexalith.Parties.Client/HttpPartiesCommandClient.cs`, `HttpPartiesQueryClient.cs`, `Paging/PartiesPagedResultAdapter.cs`, `Abstractions/PartiesCommandResult.cs` -- command envelopes, paging/freshness → EventStore client envelopes / Commons HTTP helpers.
- `src/Hexalith.Parties.Mcp/McpContextForwardingHandler.cs`, `PartiesMcpRequestContext.cs`, `Tools/PartiesMcpTools.cs`, `Tools/PartiesMcpToolResult.cs` -- authenticated MCP context/relay plumbing → FrontComposer.Mcp plus domain-neutral Commons.Http header mechanics (G11-A); keep the 5 Parties tool definitions.
- `src/Hexalith.Parties.AdminPortal/Services/AdminPortalEventStoreAdminLinks.cs` -- aggregate/correlation link composition → FrontComposer EventStore Admin UI builder plus optional Commons.Http URI mechanics (G11-B); keep local until link parity.
- `src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs` (`GetRichSearchCapabilityAsync` and its parser) -- bounded named health probing → Commons.Http transport/parser bounds plus FrontComposer capability result (G11-C); keep Parties/Memories search semantics local.
- `src/Hexalith.Parties.AppHost/Program.cs` -- temporary compatibility/rollback surface; reusable security/module helpers → EventStore.Aspire and canonical integrated topology → FrontComposer.AppHost / approved platform AppHost after G8 parity.
- `Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props` -- build-root probing → Builds shared props/targets.
- Runtime deploy manifests/apply remain outside this repository; preserve Parties-owned workload source, CI, and container publication only.

Evidence: `story-8-3-platform-api-prerequisite-matrix.md` (rows above), `sprint-change-proposal-2026-07-16-g4-g11-frontcomposer-shared-primitives-routing.md`, `tests/.../test-summary.md`, `sprint-status.yaml`.

## Tasks & Acceptance

**Execution:** (gated — consume the resolved G12 packages, then require proof for each `needs-additive-api` row)
- [ ] `story-8-3-platform-api-prerequisite-matrix.md` -- retain the recorded G12 package resolution and record owner proof + exact identities for G6/G8/G11/G7-G9 rows.
- [ ] Before the HTTP/build slices, reconcile the matrix with the selected `Hexalith.Commons.Http` release/root gitlink and `Hexalith.Builds` release/root gitlink, prove the cited files and symbols exist at those identities, and refresh either row before consumption if its recorded identity differs.
- [ ] Adopt shared client envelopes/paging; keep local until parity; update `Client.Tests`/`Package` tests.
- [ ] For G11 Package A, adopt the FrontComposer.Mcp server-authoritative context/relay path plus approved Commons.Http header mechanics behind the 5 stable tools; prove replace-not-append headers, bearer credential provenance, API-key separation, explicit credential-provider/fail-closed behavior, mandatory gates, cancellation, and no-secret diagnostics in producer and `Mcp.Tests` fitness.
- [ ] For G11 Package B, adopt the FrontComposer EventStore Admin UI link builder; prove base path/query preservation, single encoding, blank/unsafe configuration outcomes, and current aggregate/correlation link parity in `AdminPortal.Tests`.
- [ ] For G11 Package C, adopt the Commons-bounded named health probe and FrontComposer capability result; prove success/disabled/degraded/non-success/timeout/cancellation/oversized/malformed/missing/wrong-type outcomes and current rich-search capability parity in `AdminPortal.Tests`.
- [ ] For G8 Package A, adopt the owner-approved audience-aware EventStore.Aspire security surface and prove local plus publish/external-orchestrator settings without secrets.
- [ ] For G8 Package B, adopt granular EventStore.Client transport registration and prove Parties/FrontComposer module-typed client coexistence and handler ordering.
- [ ] For G8 Package C, validate the integrated Parties topology in FrontComposer.AppHost / approved platform AppHost, including an explicit map for `parties-mcp`, standalone `parties-ui` versus `frontcomposer-ui`, and Docker/Kubernetes/ACA publish targets. Record an exact FrontComposer source commit or approved host-artifact identity plus its exact EventStore.Aspire identity, retain the Parties AppHost as rollback until parity, then retire it; keep runtime deployment external.
- [ ] Adopt Builds props and verify package/source expectations, topology, and Parties-owned CI publication contracts.
- [ ] Delete `Hexalith.Parties.Authentication` (deferred from 8.4) only after the tenant-claims-transformation G7/G9 row is proven.

**Acceptance Criteria:**
- Given G12 was resolved on 2026-07-11, when 8.8 begins package consumption, then it uses the recorded package identities and does not reopen the delivery decision without contrary evidence.
- Given the Commons HTTP or Builds row is `available`, when its recorded release/root gitlink is missing or differs from the identity selected by 8.8, then the affected HTTP/build slice HALTS before local helper deletion; no additive API is requested for that identity mismatch.
- Given G8 remains `needs-additive-api`, when the AppHost/client slice is attempted, then it HALTs until Packages A-C have named owner approval, exact consumption identities, producer and Parties consumer evidence, explicit MCP/UI/publish-target disposition, and rollback.
- Given G11 remains `needs-additive-api`, when the MCP/integration slice is attempted, then it HALTs until G11-A through G11-C have named FrontComposer/Commons owner approval, exact package/root-gitlink identities, public API/package validation, producer security/bounds evidence, Parties MCP/AdminPortal consumer parity, and rollback.
- Given an MCP outbound call, when context or credential provenance is missing/ambiguous, an API-key call lacks an explicit downstream credential provider, or a relay value is unsafe, then no downstream call occurs; identity headers are never duplicated and tokens/raw identities are never emitted in diagnostics or tool results.
- Given an Admin deep-link or capability probe failure, when configuration, HTTP status, response size, JSON shape, or named check is invalid, then the shared service returns a typed bounded unavailable/local-only/degraded outcome without malformed links or raw downstream details.
- Given proven rows, when clients, MCP, AppHost, and runtime-boundary documentation adopt shared surfaces, then public package contracts, the 5 MCP tools, AppHost topology, and the DAPR ACL are unchanged or intentionally versioned.
- Given topology ownership moves, when validated, then Parties retains only workload source, CI, and owned container publication; FrontComposer.AppHost / the approved platform AppHost owns integrated local topology; external platform operations owns runtime deployment; and no production manifests are reintroduced.

## Design Notes

- **§4 gate mapping:** (1) Prereq: consume resolved G12 package identities; G6/G8/G11/G7-G9 remain `needs-additive-api`. G8 is split into EventStore.Aspire security, EventStore.Client granular registration, and FrontComposer/platform AppHost topology packages. G11 is split into FrontComposer.Mcp authenticated relay, FrontComposer EventStore Admin links, and Commons-bounded/FrontComposer-facing named capability probing. (2) Repos: `Parties` + `Hexalith.EventStore` + `Hexalith.Commons` + `Hexalith.FrontComposer` + `Hexalith.Builds`; producer work remains owner-repository work and no submodule edit is authorized by this spec; runtime deployment is external platform-operations scope. (3) Rollback: MOVE files, Parties MCP relay/context, deep-link builder, rich-search probe, the current Parties AppHost, and typed-client registrations stay until each row and consumer parity are proven; revert per slice. (4) Lanes: producer public-API/package/security/bounds tests, Parties `Client`/`Mcp`/`AdminPortal`/`Package` test EXEs directly, topology, and CI publication-contract validation. (5) Non-goals: 8.6/8.7/G4 UI migration in 8.9, Parties/Memories search semantics, and production deployment manifests/apply. (6) Parity checklist: I5/I1/I6/I12.
- **Available-row identity mapping:** Commons HTTP helpers and Builds shared props/targets require no additive API, but their matrix release/root-gitlink identities must match the exact dependencies selected by the affected 8.8 slice before consumption.
- **Broad-story handling:** hard-gated per-row; may split into 8.8a (client), 8.8b (MCP), 8.8c (AppHost/build), and 8.8d (runtime-boundary documentation) at planning time — same gate.

## Verification

**Commands:**
- `dotnet build Hexalith.Parties.slnx -c Release -m:1` -- expected: green using the recorded G12 package identities.
- `pwsh scripts/test.ps1 -Lane ci` -- expected: Parties-owned publication contract and secret-safety checks green.
- `pwsh scripts/test.ps1 -Lane topology` -- expected: AppHost topology + ACL unchanged.

**Manual checks:**
- Confirm the 5 MCP tool names are unchanged; tenant/user values come only from authenticated server context; MCP API keys are not reused downstream; identity headers replace rather than append; tokens/raw identity/raw health JSON are absent from output and diagnostics; admin links are safely encoded; capability failures are bounded; `parties-mcp` and standalone `parties-ui` have an approved preserve-or-replace map; Docker/Kubernetes/ACA publish behavior is preserved or intentionally reduced by owner approval; no production deployment manifests are reintroduced; and the current Parties AppHost is deleted only after the platform-owned topology passes parity.
