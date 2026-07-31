---
story_key: 8-8-client-mcp-apphost-build-and-deploy-cleanup
story_id: "8.8"
epic: "8"
created: 2026-07-16T04:07:47+02:00
revalidated: 2026-07-31T08:59:12+02:00
source_status: backlog
target_status: blocked
baseline_commit_at_story_start: 8644b1b1a50d2f1ab7f1cdad8dc00a88314080e2
baseline_commit_at_revalidation: b592fb5d9c78591439a166e185c799aecc10791b
builds_root_pin_at_revalidation: e85a319ecd80f82c3090c49979d4580f07697742
builds_checkout_at_revalidation: e85a319ecd80f82c3090c49979d4580f07697742
commons_root_pin_at_revalidation: f2b5f1b12b478dce902756876138a60cde4fde65
commons_checkout_at_revalidation: f2b5f1b12b478dce902756876138a60cde4fde65
eventstore_root_pin_at_revalidation: e4618d9114c8824fd50fdfc8d135438aa261377c
eventstore_checkout_at_revalidation: e4618d9114c8824fd50fdfc8d135438aa261377c
eventstore_owner_approved_source: fa2d1c9910f8976553adb33dcdb1c9ff2ea75594
eventstore_owner_approved_proof_package: 999.1.20-proof.fa2d1c9910f8
frontcomposer_root_pin_at_revalidation: b6efcad5b293017f9805e4fc7dc982b92abff678
frontcomposer_checkout_at_revalidation: b6efcad5b293017f9805e4fc7dc982b92abff678
memories_root_pin_at_revalidation: a1f64d552f843ed299cb95ef4ffa18b81516a2fb
memories_checkout_at_revalidation: a1f64d552f843ed299cb95ef4ffa18b81516a2fb
tenants_root_pin_at_revalidation: 625061bd4858d34263c2deef6a705742ac68ed37
tenants_checkout_at_revalidation: 625061bd4858d34263c2deef6a705742ac68ed37
---

# Story 8.8: Client, MCP, AppHost, build, and runtime-boundary cleanup

Status: blocked

<!-- The implementation packet is complete enough for workflow intake. Parties production migration is hard-gated by the authoritative Epic 8 sequence and by the independently governed Story 8.3 G6, G7/G9, G8, G11, Commons HTTP, and Builds rows. -->

## Story

As a maintainer,
I want remaining non-domain plumbing to move to the appropriate shared surface,
so that Parties packages and runtime-boundary assets describe only Parties-owned behavior.

## Acceptance Criteria

1. Given Stories 8.6 and 8.7 remain blocked in the authoritative `8.6 -> 8.7 -> 8.8` sequence, when Story 8.8 is prepared or owner-side prerequisite work proceeds, then no Parties production migration or deletion begins until both predecessors complete or an approved architecture/product artifact explicitly changes the sequence.
2. Given Story 8.8 contains independently owned slices, when a slice starts, then its Story 8.3 row records the named owner approval, exact selected release or root-declared gitlink, public API/package inventory, producer and Parties consumer evidence, rollback instructions, and behavior parity. An `available` label, a checked-out source file, a routing proposal, or success in a different slice does not authorize consumption or deletion.
3. Given the Commons HTTP row records `Hexalith.Commons.Http` `2.28.1` and root pin `b03469b...` while the current catalog/root select `2.29.0` and `f2b5f1b...`, when Parties adopts shared registration, correlation, or bounded ProblemDetails mechanics, then the matrix first records the exact selected identity and preserves absolute HTTP/HTTPS endpoint validation, tenant validation, independent typed clients, cancellation, correlation, genuinely bounded error reads, safe public messages, and current command/query routes and contracts.
4. Given the G6 and G8-B EventStore client surfaces become consumable, when local client transport mechanics are replaced, then command envelopes, correlation and payload semantics, route-ID authority, paging and `ProjectionFreshnessMetadata`, projection actor compatibility fields, request customization, typed error codes, and fail-closed response-ID validation remain compatible. This story does not move the projection/query domain semantics owned by Story 8.6 and does not introduce a non-additive public `Client` or `Contracts` break.
5. Given the G11 MCP relay surface becomes consumable, when MCP context/result plumbing is replaced, then tenant and user context come only from the authenticated server principal, outbound identity headers are CR/LF-free single values with replace-not-append behavior, and diagnostics contain no raw identity or credentials. The MCP server validates its own token audience and never forwards the inbound MCP bearer token or reuses a FrontComposer MCP API key downstream; an approved downstream credential provider is used or the call fails closed.
6. Given MCP infrastructure moves, when the Parties MCP server is exercised, then exactly `create_party`, `get_party`, `find_parties`, `update_party`, and `delete_party` remain exposed with compatible names, schemas, bounded validation, cancellation, status/category/code/message/correlation result semantics, and domain behavior. Tool definitions remain Parties-owned, `delete_party` remains the existing soft-delete operation rather than GDPR erasure, and no temporal-name tool or new product behavior is added.
7. Given the G11 deep-link and capability surfaces become consumable, when local AdminPortal plumbing is replaced, then EventStore aggregate/stream and correlation links accept only absolute HTTP/HTTPS base URIs without user-info, preserve configured base path/query, encode each value once, and return a typed unavailable outcome. Rich-search capability probing has explicit timeout/cancellation and response-size/JSON-depth bounds, extracts the configured named health result, maps only to `Available`, `LocalOnly`, or `Degraded`, and exposes no downstream body or exception detail.
8. Given the approved G7/G9 tenant-claims APIs become consumable, when `Hexalith.Parties.Authentication` is considered for retirement, then the exact EventStore/Commons package or root-pin identities, claim constant, transformation, identifier validation, idempotence, registration ordering, host/UI parity, and rollback are proven first. Only then are project, solution, package, CI, fitness, host, and UI references removed; ownership approval without delivered APIs does not authorize deletion.
9. Given the complete G8 AppHost packet becomes consumable, when reusable AppHost helpers or canonical topology move, then EventStore.Aspire supplies an approved audience-aware JWT surface, EventStore.Client supplies granular typed-client registration, and the approved platform AppHost proves the explicit `parties-mcp` resource, standalone `parties-ui` versus `frontcomposer-ui` disposition, Dapr dependencies and deny-by-default ACL behavior, audience/security configuration, health behavior, Docker/Kubernetes/ACA publish targets, exact producer/consumer identities, and exercised rollback.
10. Given canonical integrated topology is proven, when runtime ownership is cleaned up, then runtime deployment orchestration remains external while Parties retains workload source, GitHub Actions CI, and Parties-owned container publication. `Hexalith.Parties.AppHost` remains a functional migration/rollback surface until topology, security, publish, operator-documentation, and rollback parity are green; this story creates no production manifests and does not absorb Stories 8.12 or 8.13.
11. Given the Builds row records `v4.18.5`/`ed75ae3...` while the revalidated root gitlink and checkout are `e85a319...` (`v4.23.0-17-ge85a319`), when build-root probing is considered for removal, then the matrix is first refreshed and revalidated against the exact selected release or root gitlink. Adoption preserves .NET 10/`.slnx`, Central Package Management, source/package modes, the current semantic-release-to-`HexalithPartiesPackageVersion` version path, sequential `-m:1` release builds, warnings-as-errors, test inventory, CI/release behavior, and no-warning-override gates; it does not reintroduce the removed local MinVer setup to satisfy stale documentation.
12. Given Epic 8 is Class C post-MVP maintenance with zero new PRD functional coverage, when any 8.8 slice completes, then public packages, the five MCP tools, topology, gateway routes, self-scoped authorization, UI behavior, operator documentation, logging/telemetry privacy, and rollback remain stable or are intentionally versioned. Exact commands and results are recorded in the prerequisite matrix and `_bmad-output/implementation-artifacts/tests/test-summary.md` before corresponding local code is deleted.

## Tasks / Subtasks

- [ ] Clear the story and slice start gates before production edits (AC: 1, 2, 12)
  - [x] Confirm Stories 8.6 and 8.7 are `blocked` and keep 8.8 blocked under the authoritative sequence.
  - [x] Inventory the G6, G7/G9, G8, G11, Commons HTTP, Builds, and G12 rows plus the current root gitlinks and live checkouts.
  - [x] Confirm the Commons HTTP row's `2.28.1`/`b03469b...` identity does not match the current `2.29.0`/`f2b5f1b...` selection; halt that slice until the row is refreshed or the older exact package is deliberately selected.
  - [x] Confirm the Builds row does not match the revalidated root/checkout `e85a319...` identity; halt the build slice.
  - [x] Confirm EventStore Story 1.20 now authorizes only exact source `fa2d1c9...` or its proof artifacts; the current `v3.86.0`/`e4618d9...` identity is not that approved identity and the packet does not close G5, G7/G9, G8, or G11.
  - [x] Confirm all six relevant checkouts match their root gitlinks and the root worktree is clean; do not treat synchronization alone as owner approval or consumer parity.
  - [ ] Before resuming, re-read the matrix and root pins because concurrent repository synchronization can invalidate this snapshot.
  - [ ] Complete Stories 8.6 and 8.7 or record an approved sequence change.

- [ ] Adopt Commons HTTP and owner-delivered EventStore client mechanics without duplicating them (AC: 2-4, 12)
  - [ ] Validate the selected `Hexalith.Commons.Http` package/root identity through restore, public API inventory, producer tests, and Parties consumer tests before changing local helpers.
  - [ ] Replace only proven registration, correlation, endpoint-validation, and bounded ProblemDetails mechanics; keep Parties command/query route construction, domain request selection, and compatibility adapters.
  - [ ] Consume the approved G6 command/query envelope, paging/freshness, and typed-error APIs only after their exact EventStore.Client identity is recorded; do not guess owner API names or recreate them locally.
  - [ ] Preserve `api/v1/commands` and `api/v1/queries` behavior, correlation IDs, enriched command payloads, server-authoritative IDs, query actor fields, request customizers, cancellation, and bounded error mapping.
  - [ ] Preserve fail-fast absolute HTTP/HTTPS `BaseUrl` and tenant validation at registration and deterministic composition with granular EventStore client registrations.
  - [ ] Keep public `PartiesCommandResult<T>` and other published Client/Contracts shapes behind compatibility adapters unless an ADR and intentional version plan approve a break.
  - [ ] Delete `PartiesPagedResultAdapter`, local envelopes, scrubbers, or registration validation only one-for-one after their individual parity and rollback evidence is green.

- [ ] Adopt the G11 MCP relay/result infrastructure while keeping Parties tool semantics (AC: 2, 5, 6, 12)
  - [ ] Require the approved FrontComposer.Mcp/Commons HTTP relay to derive tenant/user from the authenticated server context, reject CR/LF and multi-value identity input, and replace rather than append outbound headers.
  - [ ] Remove trust in client-supplied tenant/user headers and raw `Authorization` only after the replacement is wired and its negative security tests pass.
  - [ ] Use a separately issued, audience-correct downstream credential or explicit host credential provider. Never transit the inbound MCP token, reuse the MCP API key, or fall back to an unvalidated raw header.
  - [ ] Preserve the exact five tool names, schemas, size/page limits, correlation/result envelope, soft-delete meaning, authorization gates, cancellation, and typed client calls.
  - [ ] Keep `PartiesMcpTools` and all domain mapping/validation in Parties. Move only generic context forwarding and result plumbing that the approved owner surface actually replaces.
  - [ ] Add adversarial tests for forged tool arguments/headers, duplicate headers, CR/LF, wrong audience, missing downstream credentials, API-key calls, token/identity log leakage, cancellation, and bounded downstream errors.

- [ ] Adopt G11 AdminPortal link and health-capability plumbing independently (AC: 2, 7, 12)
  - [ ] Consume the approved EventStore Admin link builder only after exact FrontComposer/Commons identities and public APIs are recorded.
  - [ ] Prove scheme/user-info rejection, base-path/query preservation, fragment handling, aggregate/stream/correlation single encoding, and typed unavailable outcomes.
  - [ ] Consume the approved bounded named-health/capability result independently; delivery of the link builder does not authorize deletion of the health probe or vice versa.
  - [ ] Preserve Parties/Memories rich-search behavior and `Available`/`LocalOnly`/`Degraded` UI semantics while adding explicit timeout, cancellation, response-size, JSON-depth, and named-result bounds.
  - [ ] Ensure downstream response bodies, exception types/messages, tokens, headers, tenant IDs, and user IDs never enter user-facing text, logs, traces, or metrics.

- [ ] Retire local tenant-claims plumbing only after G7/G9 delivery and parity (AC: 2, 8, 12)
  - [ ] Record exact packages/pins for the public EventStore tenant claim constant, `AggregateIdentity.IsValid(string)`, reusable EventStore claims transformation, and Commons `UniqueIdHelper.IsValidUlid(string)`.
  - [ ] Prove `tenants`, `tenant_id`, and `tid` normalization, malformed/multiple claims, idempotence, existing canonical claims, registration lifetimes/order, and host/UI authorization parity.
  - [ ] Keep `Hexalith.Parties.Authentication` and all registrations/tests as the rollback path until both host and UI run green on the shared path and a switch-back is exercised.
  - [ ] After proof, remove the project from `Hexalith.Parties.slnx`, project/package references, `scripts/test.ps1`, CI inventory, fitness rules, and obsolete tests in the same reviewed change.

- [ ] Move reusable AppHost mechanics and prove the canonical platform topology (AC: 2, 9, 10, 12)
  - [ ] Record the exact G8-A audience-aware EventStore.Aspire security API and prove issuer, audience, development-secret handling, Keycloak/local behavior, and no secret disclosure.
  - [ ] Record the exact G8-B granular EventStore.Client registration API and prove module clients coexist without duplicate or order-dependent registrations.
  - [ ] Record the G8-C platform AppHost owner, reusable-library identity, FrontComposer host identity, producer tests, Parties consumer tests, operator evidence, and rollback.
  - [ ] Prove parity for EventStore/admin resources, Parties and Tenants Dapr resources/dependencies, `parties-mcp` explicit startup, standalone `parties-ui` versus `frontcomposer-ui`, optional Memories/sample branches, health, audiences, ACLs, Docker, Kubernetes, and ACA publish behavior.
  - [ ] Retain `src/Hexalith.Parties.AppHost` until the approved integrated host passes topology/security/publish proof and a rollback to the Parties AppHost is exercised.
  - [ ] Keep runtime deployment application outside Parties. Do not create or retire production deployment manifests here; Stories 8.12 and 8.13 remain separate.
  - [ ] After parity, update `README.md`, `docs/deployment-guide.md`, `docs/getting-started.md`, `docs/development-guide.md`, `docs/ci.md`, `docs/component-inventory.md`, `docs/source-tree-analysis.md`, and `docs/api-contracts.md` to name the canonical AppHost, retained rollback path, five-tool boundary, external deployment owner, and supported publish targets without claiming unproven runtime delivery.

- [ ] Adopt the exact approved Builds surface without weakening repository gates (AC: 2, 11, 12)
  - [ ] Refresh the Story 8.3 Builds row against the exact selected release or root gitlink; a dirty/newer checkout is not consumable identity proof.
  - [ ] Compare shared props/targets with `Directory.Build.props`, `Directory.Build.targets`, and `Directory.Packages.props` line by line for SDK/framework, CPM, source/package mode, versioning, analyzers, warnings, and repository-root behavior.
  - [ ] Stage imports/adapters first and retain local probes until source-mode and package-mode restore/build/test/pack/release parity plus rollback pass.
  - [ ] Preserve the root-only submodule rule, `global.json` SDK pin, `net10.0`, C# settings, `.slnx`, semantic-release version injection through `HexalithPartiesPackageVersion`, warnings-as-errors, sequential `-m:1` builds, and the complete test-project inventory.
  - [ ] Reconcile stale MinVer references in scripts and documentation with the already-removed local MinVer configuration; do not re-add local MinVer ownership or change release semantics as an incidental cleanup.
  - [ ] Preserve Parties-owned GitHub CI and container publication. Shared workflow use does not transfer product image ownership or authorize deployment.
  - [ ] Delete only build rules proven redundant; never mask warnings or use a broad package/toolchain upgrade to make the migration pass.

- [ ] Disposition the G1/G2 degraded-response and DAPR-health planning overlap without broadening scope (AC: 2, 12)
  - [ ] Record whether an approved planning artifact assigns G1/G2 consumption to 8.8; until then keep `DegradedResponseMiddleware` and all Parties DAPR health-check registrations local.
  - [ ] If assigned here, select an exact EventStore identity and prove full middleware/header, check-name/tag, state-store/pub-sub/config-store, failure-status, timeout, and rollback parity. The visible `AddEventStoreDaprHealthChecks` API alone is insufficient.
  - [ ] If assigned elsewhere, record the owner/story in the matrix and explicitly exclude G1/G2 deletion from this story.

- [ ] Validate, document, and close each deletion proof independently (AC: 3-12)
  - [ ] Run the exact focused direct xUnit v3 assemblies and source/package builds in Testing and Validation Guidance; do not rely on a filtered command that can execute zero tests.
  - [ ] Run `unit`, `integration`, `topology`, and `ci` lanes plus package/public API, no-warning-override, topology/security, and container-publication fitness.
  - [ ] Manually inspect the five MCP tools, forged-context failures, credential separation, links, capability degradation, topology resource names, publish output, and rollback.
  - [ ] Treat Docker, registry, credentials, network, or external-host limitations as unproven gates rather than passes.
  - [ ] Update the corresponding matrix row and `_bmad-output/implementation-artifacts/tests/test-summary.md` with exact owner, release/root identity, commands, results, environment limits, deletion list, and exercised rollback before deleting local code.
  - [ ] Run `git diff --check` and update sprint status only to the state the evidence supports.

## Dev Notes

### Story Classification and Current Blockers

- Epic 8 is Class C post-MVP maintenance with zero new PRD functional coverage. Story 8.8 changes ownership and plumbing, not product features. [Source: `_bmad-output/planning-artifacts/epics.md#Story-8.8-Client-MCP-AppHost-build-and-runtime-boundary-cleanup`]
- The approved sequence remains `8.6 -> 8.7 -> 8.8`. Both predecessor stories are blocked, so owner work may proceed but Parties source consumption remains sequence-gated.
- The draft 8.8 spec is explicitly `blocked-prerequisite` and recommends a split or hard gates. Treat Commons HTTP, G6 EventStore client, G11 MCP relay, G11 Admin links, G11 named-health capability, G7/G9 authentication, G8-A security, G8-B client registration, G8-C topology, Builds, G1/G2 runtime health, and G12 package-only parity as independent evidence gates. Proof for one gate never authorizes another gate's activation or deletion.
- EventStore Story 1.20 is now `done` with an owner-approved `available` packet dated 2026-07-26. It authorizes migration only to exact source `fa2d1c9...`, proof package `999.1.20-proof.fa2d1c9910f8`, or the digest-pinned container recorded there. The current Parties EventStore root/checkout `e4618d9...` (`v3.86.0`) is a descendant, not an approved exact identity; Story 8.6 and the `8.6 -> 8.7 -> 8.8` sequence therefore remain blocked.
- G6 owner code and lifecycle evidence have advanced materially, but the Parties matrix still needs an exact consumable identity plus command/query outcome parity and rollback. G7/G9 delivery remains absent: the tenant claim constant is not in the approved public Contracts surface, and `AggregateIdentity.IsValid(string)` and `UniqueIdHelper.IsValidUlid(string)` do not exist at the current pins.
- G8 is partial but unclosed: EventStore has audience-aware `WithJwtBearerSecurity`, `AddEventStoreGatewayClient`, and DAPR service-invocation wiring; the current FrontComposer AppHost composes EventStore, Tenants, Parties, and `frontcomposer-ui`. No owner-approved packet yet proves the required independently selectable module clients, explicit `parties-mcp`, standalone `parties-ui` disposition, optional Memories/sample parity, supported publish targets, or rollback.
- G11 remains unclosed. Current FrontComposer MCP resolves authenticated server context, but no approved outbound relay/downstream-credential surface, EventStore Admin deep-link builder, or bounded named-health capability primitive replaces the Parties-local paths.
- Commons HTTP and Builds no longer match the historical `available` identities in the matrix: current selections are Commons `2.29.0`/`f2b5f1b...` and Builds `e85a319...` (`v4.23.0-17-ge85a319`). Each row must be refreshed or its older exact identity deliberately selected before consumption.
- G12 publication selection is resolved, but its package-only proof used Commons `2.28.0` and Tenants `2.4.2`; current Central Package Management selects Commons `2.29.0` and Tenants `5.3.0`. Rerun package-only restore/build and consumer parity before deletion without reopening the publication decision merely because versions advanced.

### Architecture and Scope Guardrails

- I1/I1a: keep the EventStore gateway-to-`/process` boundary and deny-by-default Dapr access control. The Parties AppHost is temporary rollback infrastructure until a platform AppHost proves parity.
- I3/I4: retain rollback code until parity and switch-back are exercised; consume only an owner-approved release or root-declared gitlink.
- I5/I6: preserve public Client, Contracts, Picker, AdminPortal, and ConsumerPortal compatibility plus `aggregateId == party_id` self-scope.
- I7/I8: preserve GDPR policy, protected-payload behavior, exports, processing records, certificates, and no-leak diagnostics even though Stories 8.7 and 8.9 own adjacent implementation.
- I12: preserve .NET 10, `.slnx`, Central Package Management, warnings-as-errors, xUnit v3/Microsoft Testing Platform, Shouldly, NSubstitute, bUnit, the current semantic-release/version-injection path, and root-declared submodules only.
- Do not absorb Story 8.6 projection/query mechanics, Story 8.7 crypto, Story 8.9 visible UI consolidation, Story 8.12 Zot publication, or Story 8.13 deployment-artifact retirement.
- The Story 8.3 G1/G2 degraded-response/Dapr-health row names 8.8, but the approved 8.8 spec does not include that row in its start gates or file map. Keep `DegradedResponseMiddleware` and Parties Dapr health checks local; route the planning inconsistency explicitly rather than silently deleting them under “platform runtime concerns.”

### Current Implementation Facts to Preserve

- `HttpPartiesCommandClient` posts to `api/v1/commands`, generates Commons ULIDs, uses the server route ID as authority, validates enriched payload IDs fail-closed, propagates correlation, and reads bounded ProblemDetails. Shared envelopes must preserve those semantics.
- `HttpPartiesQueryClient` posts EventStore `SubmitQueryRequest` to `api/v1/queries`, preserves projection actor compatibility fields and request customization, and returns paging plus `ProjectionFreshnessMetadata`. Transport cleanup must not become Story 8.6 query-domain migration.
- Current EventStore `IEventStoreGatewayClient`, `EventStoreQueryResult<T>.Metadata`, `EventStoreGatewayException`, `AddEventStoreGatewayClient`, and `AddEventStoreDaprServiceInvocation` are candidate owner surfaces, not automatic replacements. Prove Parties' six freshness states across both `200` and `304`, typed outcomes, handler order, and fail-closed DAPR routing before adoption.
- `PartiesClientServiceCollectionExtensions` validates an absolute HTTP/HTTPS base URL and tenant at registration and creates independent typed clients. Granular owner registration must compose deterministically with these module clients.
- `McpContextForwardingHandler` currently uses `TryAddWithoutValidation` and can append tenant/user values; `PartiesMcpRequestContext` can read raw headers and authorization. These are retained rollback files, not the target security model.
- `PartiesMcpTools` owns exactly five domain tools and extensive validation/mapping. Only generic context/result infrastructure may move; tool definitions and domain behavior stay.
- `AdminPortalEventStoreAdminLinks` preserves configured base paths and queries, but the replacement must add explicit scheme/user-info safety and typed unavailable outcomes.
- `PartiesAdminPortalApiClient.GetRichSearchCapabilityAsync` directly parses a `/health` response and currently can disclose exception type in degraded text. The replacement must bound transport/JSON, extract the named result, and return safe typed states without changing search behavior.
- `Hexalith.Parties.AppHost/Program.cs` currently models EventStore/admin, Parties/Tenants Dapr resources, explicit `parties-mcp`, standalone `parties-ui`, optional Memories/sample resources, local security, and Docker/Kubernetes/ACA publish targets. Its project, DAPR/Keycloak assets, launch settings, `aspire.config.json`, topology tests, and release-script coupling remain the rollback oracle until approved integrated-host parity.
- `Hexalith.Parties.Authentication` normalizes `tenants`, `tenant_id`, and `tid` to the EventStore tenant claim and is referenced by host, UI, solution, test, and CI inventory. Delete it only as one coherent, reversible G7/G9 migration.
- Root package authority has already moved to Builds and local MinVer/package-version duplication has been removed. `Directory.Build.props` still owns root discovery, source/package switches, warnings/package metadata, and analyzer filtering; `Directory.Build.targets` still carries narrow FrontComposer warning exceptions; `Directory.Packages.props` is the required import shim. A shared Builds import is not proof that every local rule is redundant.

### File Ownership and Change Boundaries

KEEP as Parties-owned behavior:

- `src/Hexalith.Parties.Client/HttpPartiesCommandClient.cs` and `HttpPartiesQueryClient.cs` domain routing/mapping portions
- `src/Hexalith.Parties.Mcp/Tools/PartiesMcpTools.cs` and the five tool contracts
- Parties `Contracts`, typed domain clients, domain UI, samples, workload source, GitHub CI, and container publication
- `src/Hexalith.Parties.AppHost` until integrated topology and rollback parity are proven

CONDITIONAL UPDATE/DELETE only after the named slice gate:

- Client: `Paging/PartiesPagedResultAdapter.cs`, `Abstractions/PartiesCommandResult.cs`, `PartiesClientException.cs`, local command/query envelope and error plumbing, `PartiesClientOptions.cs`, `Extensions/PartiesClientServiceCollectionExtensions.cs`, and the Client project dependency graph
- MCP: `McpContextForwardingHandler.cs`, `PartiesMcpRequestContext.cs`, `Program.cs`, `PartiesMcpHttpClientNames.cs`, `PartiesMcpOptions.cs`, the MCP project dependency graph, and generic portions of `Tools/PartiesMcpToolResult.cs`
- AdminPortal: `Services/AdminPortalEventStoreAdminLinks.cs`, only the capability-probe plumbing in `Services/PartiesAdminPortalApiClient.cs`, the public capability result/interface compatibility adapters, options/validation/registration, and the AdminPortal project dependency graph
- Authentication: `src/Hexalith.Parties.Authentication/**` plus Parties host/UI registrations, project/solution references, CI/package inventories, fitness rules, and replacement tests
- AppHost: `src/Hexalith.Parties.AppHost/**`, `aspire.config.json`, topology tests, and AppHost-dependent release scripting; retire them only after complete G8-C proof
- Build: `Directory.Build.props`, `Directory.Build.targets`, and `Directory.Packages.props` rules proven redundant against the exact Builds identity

Additional update/retirement inventory that must move atomically with its slice includes `Hexalith.Parties.slnx`, per-project `.csproj` references, `.github/workflows/ci.yml`, `.github/workflows/release.yml`, `scripts/test.ps1`, package validation/packing scripts, `scripts/publish-parties-containers.ps1`, `tests/README.md`, `RELEASE-CHECKLIST.md`, and the affected architecture/build/deployment documentation. The removed root `deploy/` tree belongs to completed Story 8.13 and is not recreated here.

Use this exact execution map when a gate becomes consumable. Every listed target is conditional; re-run `rg` for references immediately before editing and add any newly discovered companion to the same reviewed change.

| Independent gate | Exact Parties UPDATE candidates | KEEP / replacement boundary | Minimum isolated proof before deletion |
|---|---|---|---|
| Commons HTTP | `src/Hexalith.Parties.Client/Extensions/PartiesClientServiceCollectionExtensions.cs`, `PartiesClientOptions.cs`, `PartiesClientException.cs`, `Hexalith.Parties.Client.csproj`; the matching dependency-injection, exception, package, and fitness tests under `tests/Hexalith.Parties.Client.Tests` | Keep Parties routes, domain mapping, public interfaces, and typed command/query clients; replace only selected registration, endpoint validation, correlation, and bounded error mechanics | Exact Commons identity/API inventory, producer tests, source/package consumer builds, invalid URI/tenant cases, independent typed-client composition, cancellation, correlation, bounded error, and rollback |
| G6 EventStore client | `src/Hexalith.Parties.Client/HttpPartiesCommandClient.cs`, `HttpPartiesQueryClient.cs`, `Paging/PartiesPagedResultAdapter.cs`, `Abstractions/PartiesCommandResult.cs`, client project references; `HttpPartiesCommandClientTests.cs`, `HttpPartiesQueryClientTests.cs`, `DependencyInjectionTests.cs`, `ClientArchitecturalFitnessTests.cs`, and `ClientPackageTests.cs` | Keep `api/v1/commands`, `api/v1/queries`, domain request selection, public compatibility shapes, route-ID authority, and projection/query semantics owned by Story 8.6 | Exact EventStore identity/API inventory, six freshness states over `200` and `304`, paging, compatibility fields, request customizers, typed errors, handler order, source/package builds, real-call switch-back |
| G11 MCP relay | `src/Hexalith.Parties.Mcp/McpContextForwardingHandler.cs`, `PartiesMcpRequestContext.cs`, `PartiesMcpHttpClientNames.cs`, `PartiesMcpOptions.cs`, `Program.cs`, `Hexalith.Parties.Mcp.csproj`, and generic result plumbing in `Tools/PartiesMcpToolResult.cs`; matching MCP contract, dispatch, fitness, and project tests | Keep `Tools/PartiesMcpTools.cs`, `Tools/PartiesMcpToolNames.cs`, exactly five tool contracts, and all Parties validation/domain mapping | Exact relay and downstream-credential APIs, authenticated-principal sourcing, audience rejection, no token/API-key passthrough, replace-not-append headers, CR/LF/duplicate defenses, safe bounded failures/logs, cancellation, rollback |
| G11 Admin links | `src/Hexalith.Parties.AdminPortal/Services/AdminPortalEventStoreAdminLinks.cs`, related options/validator/registration in `Services/PartiesAdminPortalOptions.cs`, `PartiesAdminPortalOptionsValidator.cs`, and `Extensions/PartiesAdminPortalServiceCollectionExtensions.cs`; matching AdminPortal service and packaging tests | Keep Parties navigation semantics and typed unavailable compatibility; do not couple this deletion to health-capability delivery | Exact link-builder identity/API, scheme/user-info rejection, base-path/query and fragment behavior, single encoding for aggregate/stream/correlation values, typed unavailable outcome, rollback |
| G11 named-health capability | Capability-probe portions of `src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs`, `IPartiesAdminPortalApiClient.cs`, `AdminPortalRichSearchCapability.cs`, options/registration/project references; `PartiesAdminPortalApiClientTests.cs`, service-collection tests, and affected component tests | Keep Parties/Memories rich-search decisions and the three product states `Available`, `LocalOnly`, and `Degraded`; do not expose raw health payloads | Exact capability API, timeout/cancellation, response byte and JSON-depth limits, configured named-result extraction, malformed/oversized/failure cases, safe diagnostics, rollback |
| G7/G9 authentication | `src/Hexalith.Parties.Authentication/Hexalith.Parties.Authentication.csproj` and `PartiesClaimsTransformation.cs`; `src/Hexalith.Parties/Hexalith.Parties.csproj`, host registration in `Extensions/PartiesServiceCollectionExtensions.cs`, `src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj`, `Authentication/PartiesUiAuthorization.cs`, solution/CI/test inventory; authentication, host-composition, UI-composition, resolver, package, and fitness tests | Keep the complete local project and registrations as rollback until the public claim constant, transformation, aggregate-ID validation, and ULID validation are all delivered and host/UI parity is green | Exact EventStore/Commons identities and public APIs, all legacy/canonical claim combinations, malformed/multiple/idempotent cases, DI lifetime/order, host/UI authorization parity, public/package fitness, exercised switch-back |
| G8-A security | Security configuration in `src/Hexalith.Parties.AppHost/Program.cs`, `KeycloakRealms/hexalith-realm.json`, `Properties/launchSettings.json`, and affected host/UI authentication topology tests | Keep local audience, issuer, development-secret, Keycloak, ACL, and rollback configuration until the approved EventStore.Aspire surface proves parity | Exact owner identity/API, issuer/audience success and failure, local/Keycloak behavior, secret non-disclosure, deny-by-default ACLs, topology test, rollback |
| G8-B granular client registration | EventStore-client registration portions of `src/Hexalith.Parties.AppHost/Program.cs`; Parties Client/AdminPortal/MCP `.csproj` and registration files already named above; their DI and project-fitness tests | Keep independently selectable command, query, GDPR/admin, and MCP client registrations; never accept an all-or-nothing helper that creates duplicate or order-dependent registrations | Exact EventStore.Client API, every subset and combined-module registration, handler-chain order, duplicate/order tests, source/package builds, rollback |
| G8-C canonical topology | `src/Hexalith.Parties.AppHost/Program.cs`, `Hexalith.Parties.AppHost.csproj`, `DaprComponents/**`, `KeycloakRealms/hexalith-realm.json`, `Properties/launchSettings.json`, root `aspire.config.json`; `tests/Hexalith.Parties.IntegrationTests/HealthChecks/PartiesAspireTopologyCollection.cs`, `PartiesAspireTopologyFixture.cs`, `HealthEndpointE2ETests.cs`, all `tests/Hexalith.Parties.IntegrationTests/Topology/**`, and `tests/Hexalith.Parties.Tests/FitnessTests/AppHostTenantsTopologyTests.cs` | Keep the whole local AppHost as the rollback oracle until explicit `parties-mcp`, UI disposition, optional Memories/sample branches, dependencies, health, security, and publish targets are proven in the approved platform host | Exact owner/host identities, producer and Parties topology/security tests, Docker/Kubernetes/ACA publish output, operator evidence, real startup, switch-back; no production manifest creation or runtime deployment application |
| Builds | `Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props`, `global.json`, `Hexalith.Parties.slnx`; `.github/workflows/ci.yml`, `.github/workflows/release.yml`, `.github/workflows/rc-gate.yml`; `scripts/test.ps1`, `check-no-warning-override.sh`, `gitlink-rc-gate.sh`, `msbuild_properties.py`, `pack-release-packages.py`, `validate-consumer-package-references.py`, `validate-nuget-packages.py`; `tests/Directory.Build.props`, `tests/README.md`, `RELEASE-CHECKLIST.md` | Keep every local rule that is not proven identical to the exact Builds selection; preserve CPM, .NET 10/`.slnx`, source/package modes, warnings, analyzer policy, test inventory, `-m:1`, semantic-release injection, CI, and product-image ownership | Line-by-line props/targets comparison, source/package restore/build/test/pack, public/package and warning gates, CI/release dry proof, exact version propagation, rollback without warning suppression |
| G1/G2 runtime health | `src/Hexalith.Parties/Middleware/DegradedResponseMiddleware.cs`; `src/Hexalith.Parties/HealthChecks/DaprSidecarHealthCheck.cs`, `DaprStateStoreHealthCheck.cs`, `DaprPubSubHealthCheck.cs`, `PartiesHealthCheckExtensions.cs`; corresponding middleware/health tests and `tests/Hexalith.Parties.IntegrationTests/HealthChecks/HealthEndpointE2ETests.cs` | Keep all local middleware/checks unless the matrix explicitly assigns this consumption to 8.8 and an exact owner surface proves the complete contract | Owner/story disposition plus exact identity; degraded header/middleware, names/tags, state/pub-sub/config coverage, statuses, timeouts, end-to-end health, privacy, rollback |
| G12 package-only parity | `scripts/validate-consumer-package-references.py`, `scripts/validate-nuget-packages.py`, `scripts/pack-release-packages.py`, affected package project files, package tests, CI/release inventory | Keep the resolved publication decision and all current validation until the present Commons/Tenants selection passes; this gate does not authorize another slice's deletion | Current-version package-only restore/build/test/pack, consumer dependency closure, package contents/public API, exact command/results in the matrix and test summary |
| Documentation and metadata companions | `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md`, `_bmad-output/implementation-artifacts/tests/test-summary.md`, `_bmad-output/project-context.md`, `README.md`, `docs/index.md`, `docs/project-overview.md`, `docs/architecture.md`, `docs/project-scan-report.json`, `docs/source-tree-analysis.md`, `docs/development-guide.md`, `docs/ci.md`, `docs/build-gate.md`, `docs/getting-started.md`, `docs/deployment-guide.md`, `docs/component-inventory.md`, `docs/api-contracts.md` | Preserve historical evidence while correcting stale MinVer/AppHost ownership claims; document only proven canonical surfaces and rollback paths | Same-change review with each activated gate, link/reference validation, no unsupported deployment claim, exact owner/version/commands/results/rollback recorded |

Never delete a whole file merely because it contains some generic code. Split or retain a compatibility adapter when the same file also owns Parties routing, public contracts, domain mapping, or rollback.

### Rollout and Rollback Design

For every slice, use the same order:

1. Owner approval, additive API, exact release/root pin, public API inventory, and producer tests land.
2. Parties consumes the shared path behind a compatibility adapter while the local path remains registered and testable.
3. Run local-versus-shared parity, negative security/bounds tests, package/public API tests, and the relevant topology or CI lane.
4. Switch the default, exercise real calls, then switch back to the retained path and prove rollback.
5. Restore forward, record evidence in the matrix and test summary, and delete only the individually proven local mechanics.

Do not combine first activation and deletion. Do not edit submodules in this story without explicit approval; producer changes belong in their owner repository and need their own reviewed delivery.

### Technical and Security Guidance

- Repository pins win. Use SDK `10.0.302`, `net10.0`, C# 14, current Central Package Management, warnings-as-errors, `-m:1`, and `HexalithPartiesPackageVersion` where a deterministic package version is required. Do not bundle framework, Aspire, MCP SDK, EventStore, or test-library upgrades into this cleanup.
- `TryAddWithoutValidation` explicitly bypasses header-value validation. The adopted relay must validate and replace identity headers rather than copy arbitrary input. [Microsoft `HttpHeaders.TryAddWithoutValidation`](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.headers.httpheaders.tryaddwithoutvalidation?view=net-10.0)
- The MCP 2025-11-25 authorization specification requires resource indicators and server-side intended-audience validation; the current 2026-07-28 security guidance explicitly forbids token passthrough. Use a distinct downstream token/credential path; never pass through the inbound MCP token. [MCP authorization specification](https://modelcontextprotocol.io/specification/2025-11-25/basic/authorization), [MCP security best practices](https://modelcontextprotocol.io/docs/2026-07-28/tutorials/security/security_best_practices)
- ASP.NET Core health checks distinguish `Healthy`, `Degraded`, and `Unhealthy` and are designed for bounded external monitoring. Preserve the product's typed capability mapping, but do not expose raw health payloads or assume HTTP 200 means the named dependency is available. [ASP.NET Core health checks](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-10.0)
- Current Aspire publishing is driven by AppHost model resources and registered pipeline steps; `aspire publish` emits target artifacts as a one-way handoff, while `aspire deploy` can remain a separate owner concern. Prove Docker/Kubernetes/ACA model parity in the approved platform AppHost rather than recreating the retired production manifests in Parties. [Aspire deployment model](https://aspire.dev/deployment/), [`aspire publish` reference](https://aspire.dev/reference/cli/commands/aspire-publish/)
- xUnit v3 projects are standalone Microsoft Testing Platform executables. Keep direct assembly execution for focused proof because a filtered solution command can silently provide incomplete evidence. [xUnit v3/MTP guidance](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform)

### Previous Story and Git Intelligence

- Story 8.7 established the correct deletion discipline: routing or visible owner code is not approval; record exact provenance, halt at the gate, and keep the entire local rollback path.
- Recent root history centralized package authority in Builds, removed local MinVer/package-version duplication, expanded the Client package dependency allowlist, and synchronized submodules. Preserve those accepted changes; none independently closes G6/G8/G11/G7-G9.
- At final revalidation the root worktree was clean and all six relevant submodule checkouts matched their root gitlinks. Revalidate immediately before implementation because synchronization alone neither selects the EventStore proof identity nor supplies owner/consumer parity.
- EventStore review history exposes two extra migration hazards: inbound bearer forwarding can append/conflict rather than replace, and DAPR routing is a separate opt-in handler. Require real handler-chain tests for conflicting existing headers, duplicates, CR/LF/whitespace credentials, missing registration, credential rotation/reload, and no sensitive logs.
- Only root-declared submodules may be initialized or updated. Never recurse into nested submodules.

### Testing and Validation Guidance

Build sequentially. Run the relevant direct xUnit v3 executables after each build, then the repository lanes:

```bash
git ls-tree HEAD references/Hexalith.Builds references/Hexalith.Commons references/Hexalith.EventStore references/Hexalith.FrontComposer references/Hexalith.Memories references/Hexalith.Tenants
git submodule status -- references/Hexalith.Builds references/Hexalith.Commons references/Hexalith.EventStore references/Hexalith.FrontComposer references/Hexalith.Memories references/Hexalith.Tenants
rg -n "Client command/query|Tenant claims|Aspire|Commons HTTP|MCP, deep-link|Builds shared|Package publishing" _bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md

dotnet build Hexalith.Parties.slnx -c Release -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:HexalithPartiesPackageVersion=1.0.0 --verbosity minimal
dotnet ./tests/Hexalith.Parties.Client.Tests/bin/Release/net10.0/Hexalith.Parties.Client.Tests.dll
dotnet ./tests/Hexalith.Parties.Mcp.Tests/bin/Release/net10.0/Hexalith.Parties.Mcp.Tests.dll
dotnet ./tests/Hexalith.Parties.AdminPortal.Tests/bin/Release/net10.0/Hexalith.Parties.AdminPortal.Tests.dll
dotnet ./tests/Hexalith.Parties.Authentication.Tests/bin/Release/net10.0/Hexalith.Parties.Authentication.Tests.dll
dotnet ./tests/Hexalith.Parties.Tests/bin/Release/net10.0/Hexalith.Parties.Tests.dll
dotnet ./tests/Hexalith.Parties.Ci.Tests/bin/Release/net10.0/Hexalith.Parties.Ci.Tests.dll

dotnet build Hexalith.Parties.slnx -c Release -m:1 -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true -p:NuGetAudit=false -p:HexalithPartiesPackageVersion=1.0.0 --verbosity minimal
pwsh scripts/test.ps1 -Lane unit
pwsh scripts/test.ps1 -Lane integration
pwsh scripts/test.ps1 -Lane topology
pwsh scripts/test.ps1 -Lane ci
bash scripts/check-no-warning-override.sh
git diff --check
```

Add focused class runs for `HttpPartiesCommandClientTests`, `HttpPartiesQueryClientTests`, `DependencyInjectionTests`, MCP tool contract/dispatch/fitness, AdminPortal link/capability tests, claims transformation/composition, AppHost topology, platform prerequisite fitness, package/public API snapshots, and container-publication workflow. Run the full executable whenever class names or runner syntax change.

Manual/runtime evidence must cover all five MCP tools, malicious/duplicate identity headers, wrong token audience, missing downstream credential, no credential/identity logging, deep links, rich-search degradation, all six freshness/lifecycle states under both `200` and `304`, explicit `parties-mcp` and UI resource dispositions, security audiences, Dapr ACLs, and each supported publish target. Environment-limited lanes stay open.

### Project Structure Notes

- The only authorized consuming-repository edits are in Parties. Required producer APIs belong to Hexalith.EventStore, Hexalith.Commons, Hexalith.FrontComposer, or Hexalith.Builds and must arrive through separately reviewed owner work plus a root-declared gitlink or released package; this story authorizes no submodule source edits.
- The root architecture's older Parties-owned AppHost/deployment tree is a historical implementation baseline. The later Epic 8 spine governs 8.8: keep the Parties AppHost as rollback, move canonical orchestration to the approved platform owner, and keep production deployment application external.
- No UX artifact adds behavior for this story. Preserve the browser-to-BFF-to-EventStore boundary and existing UI states; visible FrontComposer/Fluent work belongs to 8.9.
- Planning currently contains Stories 8.12 and 8.13 despite the older `8.1-8.10` sequence statement. Do not broaden 8.8; leave sequence reconciliation to planning/8.10.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-8.8-Client-MCP-AppHost-build-and-runtime-boundary-cleanup`]
- [Source: `_bmad-output/implementation-artifacts/spec-8-8-client-mcp-apphost-build-and-deploy-cleanup.md`]
- [Source: `_bmad-output/implementation-artifacts/epic-8-context.md`]
- [Source: `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md`]
- [Source: `_bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md`]
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-16-g4-g11-frontcomposer-shared-primitives-routing.md`]
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-16-g7-g9-tenant-claims-ownership.md`]
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-16-g8-aspire-publish-helper-routing.md`]
- [Source: `_bmad-output/implementation-artifacts/8-7-data-protection-extraction.md`]
- [Source: `references/Hexalith.EventStore/_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md`]
- [Source: `references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Registration/EventStoreServiceCollectionExtensions.cs`]
- [Source: `references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreSecurityExtensions.cs`]
- [Source: `references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.AppHost/Program.cs`]
- [Source: `_bmad-output/project-context.md`]
- [Source: `src/Hexalith.Parties.Client/HttpPartiesCommandClient.cs`]
- [Source: `src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs`]
- [Source: `src/Hexalith.Parties.Client/Extensions/PartiesClientServiceCollectionExtensions.cs`]
- [Source: `src/Hexalith.Parties.Mcp/McpContextForwardingHandler.cs`]
- [Source: `src/Hexalith.Parties.Mcp/PartiesMcpRequestContext.cs`]
- [Source: `src/Hexalith.Parties.Mcp/Tools/PartiesMcpTools.cs`]
- [Source: `src/Hexalith.Parties.AdminPortal/Services/AdminPortalEventStoreAdminLinks.cs`]
- [Source: `src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs`]
- [Source: `src/Hexalith.Parties.AppHost/Program.cs`]
- [Source: `src/Hexalith.Parties.Authentication/PartiesClaimsTransformation.cs`]
- [Source: `Directory.Build.props`; `Directory.Build.targets`; `Directory.Packages.props`]

## Validation Summary

- Loaded the complete Epic 8 requirements, whole PRD and architecture, Epic 8 spine/context, draft 8.8 spec, Story 8.3 matrix, predecessor Story 8.7, EventStore Story 1.20 closure packet, persistent project contexts, current implementation/test surfaces, recent Git history, and current official MCP, ASP.NET Core, .NET HTTP, Aspire, and xUnit guidance.
- Checklist corrections applied and revalidated on 2026-07-31: kept the story honestly blocked; corrected all root/pin/package evidence; distinguished the exact approved EventStore identity from its current descendant; split the work into twelve independent evidence gates; added an exact per-gate change/retention/proof map; prevented synchronized gitlinks or visible APIs from authorizing deletion; preserved tool/domain and full AppHost rollback ownership; added MCP token-passthrough/header-chain defenses and `200`/`304` lifecycle parity; reconciled the current semantic-release version path and Story 8.6 status metadata; expanded atomic retirement inventory; and required post-switch rollback plus exact evidence per deletion.
- No dependency upgrade, product feature, UI consolidation, production manifest, or submodule edit is authorized by this story.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-07-16T04:07:47+02:00 - Selected requested story `8-8-client-mcp-apphost-build-and-deploy-cleanup` from sprint status (`backlog`).
- 2026-07-16T04:07:47+02:00 - Recorded root baseline `8644b1b1...` and exact root gitlinks for Builds, Commons, EventStore, FrontComposer, Memories, and Tenants.
- 2026-07-16T04:07:47+02:00 - Observed unrelated modified Builds, Memories, and Tenants checkouts; preserved them and used root gitlinks as approval provenance.
- 2026-07-16T04:07:47+02:00 - Confirmed Commons HTTP matches its available row, Builds does not, and G6/G8/G11/G7-G9 remain without consumable closure packets.
- 2026-07-16T04:07:47+02:00 - Confirmed Stories 8.6 and 8.7 remain blocked and halted before production, dependency, build-rule, or submodule edits.
- 2026-07-16T04:15:47+02:00 - Revalidated after concurrent root synchronization at `a8428bb3...`: Builds now root-pins `v4.18.7`/`87d76ba...` but still mismatches its matrix row; Tenants checkout drift remains user-owned.
- 2026-07-31T08:59:12+02:00 - Revalidated clean root `b592fb5d...`; Builds, Commons, EventStore, FrontComposer, Memories, and Tenants checkouts all exactly match their root gitlinks.
- 2026-07-31T08:59:12+02:00 - Confirmed EventStore Story 1.20 is owner-approved `available` only for exact source `fa2d1c9...` or its recorded proof artifacts; current Parties selection `v3.86.0`/`e4618d9...` does not match that identity.
- 2026-07-31T08:59:12+02:00 - Confirmed G5, G7/G9, G8, and G11 remain unclosed, and the Commons/Builds/G12 evidence rows are stale against current selections. No production, dependency, submodule, or build-rule edit was authorized.
- 2026-07-31T08:59:12+02:00 - Reconciled Story 8.6 automation metadata with its blocked body/sprint state and added the exact per-gate file, replacement, proof, and companion-document execution map requested by final quality review.

### Completion Notes List

- Story 8.8 is context-complete as a guarded packet but remains blocked for production work by the predecessor sequence and slice-specific owner/identity/parity prerequisites.
- EventStore owner evidence has advanced, but its exact approved identity is not selected by Parties; Commons HTTP and Builds also require matrix refresh against current identities.
- G5, G7/G9, G8, and G11 still require delivered/approved surfaces plus exact identities, Parties parity, and exercised rollback. The old Tenants checkout-drift blocker no longer exists.
- No product tests were run during story creation because no production code changed. Markdown integrity and repository diff checks are the applicable validation.

### File List

**Modified**

- `_bmad-output/implementation-artifacts/8-8-client-mcp-apphost-build-and-deploy-cleanup.md`
- `_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
