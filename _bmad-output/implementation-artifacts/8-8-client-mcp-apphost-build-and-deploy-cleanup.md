---
story_key: 8-8-client-mcp-apphost-build-and-deploy-cleanup
story_id: "8.8"
epic: "8"
created: 2026-07-16T04:07:47+02:00
revalidated: 2026-07-16T04:15:47+02:00
source_status: backlog
target_status: blocked
baseline_commit_at_story_start: 8644b1b1a50d2f1ab7f1cdad8dc00a88314080e2
baseline_commit_at_revalidation: a8428bb30dc15a278f062dde97c438ad683f0185
builds_root_pin_at_revalidation: 87d76ba79309f5ba86bd6cea7f2105100cabde09
builds_checkout_at_revalidation: 87d76ba79309f5ba86bd6cea7f2105100cabde09
commons_root_pin_at_revalidation: b03469b13408530bb757d3d02279c2d772ee4848
eventstore_root_pin_at_revalidation: a48580b1c8e43e2dd434771400c0d8008587d040
frontcomposer_root_pin_at_revalidation: 0c873c3d0b5dd1e357887b952a8a655498fbe7ac
tenants_root_pin_at_revalidation: 00a2895cdd85f239f7b330c525c2442aeeb7467e
tenants_checkout_at_revalidation: 66f5b2e7b0fe2e519ad4af200d226632a923e51f
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
3. Given the Commons HTTP row records `Hexalith.Commons.Http` `2.28.1` and root pin `b03469b...`, when Parties adopts shared registration, correlation, or bounded ProblemDetails mechanics, then the selected identity matches that row and preserves absolute HTTP/HTTPS endpoint validation, tenant validation, independent typed clients, cancellation, correlation, bounded error reads, safe public messages, and current command/query routes and contracts.
4. Given the G6 and G8-B EventStore client surfaces become consumable, when local client transport mechanics are replaced, then command envelopes, correlation and payload semantics, route-ID authority, paging and `ProjectionFreshnessMetadata`, projection actor compatibility fields, request customization, typed error codes, and fail-closed response-ID validation remain compatible. This story does not move the projection/query domain semantics owned by Story 8.6 and does not introduce a non-additive public `Client` or `Contracts` break.
5. Given the G11 MCP relay surface becomes consumable, when MCP context/result plumbing is replaced, then tenant and user context come only from the authenticated server principal, outbound identity headers are CR/LF-free single values with replace-not-append behavior, and diagnostics contain no raw identity or credentials. The MCP server validates its own token audience and never forwards the inbound MCP bearer token or reuses a FrontComposer MCP API key downstream; an approved downstream credential provider is used or the call fails closed.
6. Given MCP infrastructure moves, when the Parties MCP server is exercised, then exactly `create_party`, `get_party`, `find_parties`, `update_party`, and `delete_party` remain exposed with compatible names, schemas, bounded validation, cancellation, status/category/code/message/correlation result semantics, and domain behavior. Tool definitions remain Parties-owned, `delete_party` remains the existing soft-delete operation rather than GDPR erasure, and no temporal-name tool or new product behavior is added.
7. Given the G11 deep-link and capability surfaces become consumable, when local AdminPortal plumbing is replaced, then EventStore aggregate/stream and correlation links accept only absolute HTTP/HTTPS base URIs without user-info, preserve configured base path/query, encode each value once, and return a typed unavailable outcome. Rich-search capability probing has explicit timeout/cancellation and response-size/JSON-depth bounds, extracts the configured named health result, maps only to `Available`, `LocalOnly`, or `Degraded`, and exposes no downstream body or exception detail.
8. Given the approved G7/G9 tenant-claims APIs become consumable, when `Hexalith.Parties.Authentication` is considered for retirement, then the exact EventStore/Commons package or root-pin identities, claim constant, transformation, identifier validation, idempotence, registration ordering, host/UI parity, and rollback are proven first. Only then are project, solution, package, CI, fitness, host, and UI references removed; ownership approval without delivered APIs does not authorize deletion.
9. Given the complete G8 AppHost packet becomes consumable, when reusable AppHost helpers or canonical topology move, then EventStore.Aspire supplies an approved audience-aware JWT surface, EventStore.Client supplies granular typed-client registration, and the approved platform AppHost proves the explicit `parties-mcp` resource, standalone `parties-ui` versus `frontcomposer-ui` disposition, Dapr dependencies and deny-by-default ACL behavior, audience/security configuration, health behavior, Docker/Kubernetes/ACA publish targets, exact producer/consumer identities, and exercised rollback.
10. Given canonical integrated topology is proven, when runtime ownership is cleaned up, then runtime deployment orchestration remains external while Parties retains workload source, GitHub Actions CI, and Parties-owned container publication. `Hexalith.Parties.AppHost` remains a functional migration/rollback surface until topology, security, publish, operator-documentation, and rollback parity are green; this story creates no production manifests and does not absorb Stories 8.12 or 8.13.
11. Given the Builds row currently records `v4.18.5`/`ed75ae3...` while the revalidated root-approved gitlink and checkout are `v4.18.7`/`87d76ba...`, when build-root probing is considered for removal, then the matrix is first refreshed and revalidated against the exact selected release or root gitlink. Adoption preserves .NET 10/`.slnx`, Central Package Management, source/package modes, MinVer, sequential `-m:1` release builds, warnings-as-errors, test inventory, CI/release behavior, and no-warning-override gates.
12. Given Epic 8 is Class C post-MVP maintenance with zero new PRD functional coverage, when any 8.8 slice completes, then public packages, the five MCP tools, topology, gateway routes, self-scoped authorization, UI behavior, operator documentation, logging/telemetry privacy, and rollback remain stable or are intentionally versioned. Exact commands and results are recorded in the prerequisite matrix and `_bmad-output/implementation-artifacts/tests/test-summary.md` before corresponding local code is deleted.

## Tasks / Subtasks

- [ ] Clear the story and slice start gates before production edits (AC: 1, 2, 12)
  - [x] Confirm Stories 8.6 and 8.7 are `blocked` and keep 8.8 blocked under the authoritative sequence.
  - [x] Inventory the G6, G7/G9, G8, G11, Commons HTTP, Builds, and G12 rows plus the current root gitlinks and live checkouts.
  - [x] Confirm Commons HTTP `2.28.1`/`b03469b...` matches its row; do not infer that this opens any other slice.
  - [x] Confirm the Builds row does not match the revalidated root/checkout `87d76ba...` identity; halt the build slice.
  - [x] Confirm current EventStore and FrontComposer pins do not contain an approved G6/G8/G11/G7-G9 closure packet; halt those slices.
  - [x] Preserve the modified Tenants checkout as unrelated user-owned work; never treat checkout drift as a root-approved pin.
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
  - [ ] Preserve the root-only submodule rule, `global.json` SDK pin, `net10.0`, C# settings, `.slnx`, MinVer, warnings-as-errors, sequential `-m:1` builds, and the complete test-project inventory.
  - [ ] Preserve Parties-owned GitHub CI and container publication. Shared workflow use does not transfer product image ownership or authorize deployment.
  - [ ] Delete only build rules proven redundant; never mask warnings or use a broad package/toolchain upgrade to make the migration pass.

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
- The draft 8.8 spec is explicitly `blocked-prerequisite` and recommends a split or hard gates. This story uses four independently closable slices: client/HTTP, MCP/Admin plumbing, AppHost/build, and runtime/auth cleanup. One slice's proof never authorizes another slice's deletion.
- G6 EventStore client envelopes/freshness/error codes, G8 client/Aspire/topology, G11 MCP/deep-link/capability, and G7/G9 claims delivery remain `needs-additive-api`. Current EventStore `a48580b...` and FrontComposer `0c873c3...` checkouts do not carry a recorded closure packet for those rows.
- Commons HTTP is the only directly matching 8.8 row at creation: released `2.28.1` and root `b03469b...`. It still requires Parties consumer parity before local deletion.
- The Builds row records `v4.18.5`/`ed75ae3...`, while revalidated root HEAD and checkout record `v4.18.7`/`87d76ba...`. The row must be refreshed against that exact selected identity before build-helper adoption.
- G12 publication selection is resolved, but its historical proof used older Commons/Tenants package versions. Before deleting rollback paths, rerun package-only restore/build and consumer parity against the versions actually selected by current Central Package Management; do not reopen the publication decision merely because versions advanced.

### Architecture and Scope Guardrails

- I1/I1a: keep the EventStore gateway-to-`/process` boundary and deny-by-default Dapr access control. The Parties AppHost is temporary rollback infrastructure until a platform AppHost proves parity.
- I3/I4: retain rollback code until parity and switch-back are exercised; consume only an owner-approved release or root-declared gitlink.
- I5/I6: preserve public Client, Contracts, Picker, AdminPortal, and ConsumerPortal compatibility plus `aggregateId == party_id` self-scope.
- I7/I8: preserve GDPR policy, protected-payload behavior, exports, processing records, certificates, and no-leak diagnostics even though Stories 8.7 and 8.9 own adjacent implementation.
- I12: preserve .NET 10, `.slnx`, Central Package Management, warnings-as-errors, xUnit v3/Microsoft Testing Platform, Shouldly, NSubstitute, bUnit, MinVer, and root-declared submodules only.
- Do not absorb Story 8.6 projection/query mechanics, Story 8.7 crypto, Story 8.9 visible UI consolidation, Story 8.12 Zot publication, or Story 8.13 deployment-artifact retirement.
- The Story 8.3 G1/G2 degraded-response/Dapr-health row names 8.8, but the approved 8.8 spec does not include that row in its start gates or file map. Keep `DegradedResponseMiddleware` and Parties Dapr health checks local; route the planning inconsistency explicitly rather than silently deleting them under “platform runtime concerns.”

### Current Implementation Facts to Preserve

- `HttpPartiesCommandClient` posts to `api/v1/commands`, generates Commons ULIDs, uses the server route ID as authority, validates enriched payload IDs fail-closed, propagates correlation, and reads bounded ProblemDetails. Shared envelopes must preserve those semantics.
- `HttpPartiesQueryClient` posts EventStore `SubmitQueryRequest` to `api/v1/queries`, preserves projection actor compatibility fields and request customization, and returns paging plus `ProjectionFreshnessMetadata`. Transport cleanup must not become Story 8.6 query-domain migration.
- `PartiesClientServiceCollectionExtensions` validates an absolute HTTP/HTTPS base URL and tenant at registration and creates independent typed clients. Granular owner registration must compose deterministically with these module clients.
- `McpContextForwardingHandler` currently uses `TryAddWithoutValidation` and can append tenant/user values; `PartiesMcpRequestContext` can read raw headers and authorization. These are retained rollback files, not the target security model.
- `PartiesMcpTools` owns exactly five domain tools and extensive validation/mapping. Only generic context/result infrastructure may move; tool definitions and domain behavior stay.
- `AdminPortalEventStoreAdminLinks` preserves configured base paths and queries, but the replacement must add explicit scheme/user-info safety and typed unavailable outcomes.
- `PartiesAdminPortalApiClient.GetRichSearchCapabilityAsync` directly parses a `/health` response and currently can disclose exception type in degraded text. The replacement must bound transport/JSON, extract the named result, and return safe typed states without changing search behavior.
- `Hexalith.Parties.AppHost/Program.cs` currently models EventStore/admin, Parties/Tenants Dapr resources, explicit `parties-mcp`, standalone `parties-ui`, optional Memories/sample resources, local security, and Docker/Kubernetes/ACA publish targets. It remains the rollback oracle until approved integrated-host parity.
- `Hexalith.Parties.Authentication` normalizes `tenants`, `tenant_id`, and `tid` to the EventStore tenant claim and is referenced by host, UI, solution, test, and CI inventory. Delete it only as one coherent, reversible G7/G9 migration.
- Root build files contain source/package selection and repository probing beyond simple imports. A shared Builds import is not proof that every local rule is redundant.

### File Ownership and Change Boundaries

KEEP as Parties-owned behavior:

- `src/Hexalith.Parties.Client/HttpPartiesCommandClient.cs` and `HttpPartiesQueryClient.cs` domain routing/mapping portions
- `src/Hexalith.Parties.Mcp/Tools/PartiesMcpTools.cs` and the five tool contracts
- Parties `Contracts`, typed domain clients, domain UI, samples, workload source, GitHub CI, and container publication
- `src/Hexalith.Parties.AppHost` until integrated topology and rollback parity are proven

CONDITIONAL UPDATE/DELETE only after the named slice gate:

- Client: `Paging/PartiesPagedResultAdapter.cs`, `Abstractions/PartiesCommandResult.cs`, local command/query envelope and error plumbing, `PartiesClientServiceCollectionExtensions.cs`
- MCP: `McpContextForwardingHandler.cs`, `PartiesMcpRequestContext.cs`, and generic portions of `Tools/PartiesMcpToolResult.cs`
- AdminPortal: `Services/AdminPortalEventStoreAdminLinks.cs` and only the capability-probe plumbing in `Services/PartiesAdminPortalApiClient.cs`
- Authentication: `src/Hexalith.Parties.Authentication/**` plus host/UI/project/solution/test/CI references
- AppHost: reusable helpers in `src/Hexalith.Parties.AppHost/Program.cs`; retire the project only after complete G8-C proof
- Build: `Directory.Build.props`, `Directory.Build.targets`, and `Directory.Packages.props` rules proven redundant against the exact Builds identity

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

- Repository pins win. Use SDK `10.0.301`, `net10.0`, C# 14, current Central Package Management, warnings-as-errors, and `-m:1`. Do not bundle framework, Aspire, MCP SDK, EventStore, or test-library upgrades into this cleanup.
- `TryAddWithoutValidation` explicitly bypasses header-value validation. The adopted relay must validate and replace identity headers rather than copy arbitrary input. [Microsoft `HttpHeaders.TryAddWithoutValidation`](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.headers.httpheaders.tryaddwithoutvalidation?view=net-10.0)
- The current MCP 2025-11-25 authorization specification requires MCP servers to validate tokens for their own audience and not accept or transit other tokens. Use a distinct downstream token/credential path; never pass through the inbound MCP token. [MCP authorization specification](https://modelcontextprotocol.io/specification/2025-11-25/basic/authorization), [MCP security best practices](https://modelcontextprotocol.io/docs/tutorials/security/security_best_practices)
- ASP.NET Core health checks distinguish `Healthy`, `Degraded`, and `Unhealthy` and are designed for bounded external monitoring. Preserve the product's typed capability mapping, but do not expose raw health payloads or assume HTTP 200 means the named dependency is available. [ASP.NET Core health checks](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-10.0)
- Current Aspire publishing is driven by AppHost model resources and registered pipeline steps; `aspire publish` emits target artifacts as a handoff, while deployment can remain a separate owner concern. Prove Docker/Kubernetes/ACA model parity in the approved platform AppHost rather than copying production manifests into Parties. [Aspire deployment model](https://aspire.dev/deployment/overview/), [`aspire publish` reference](https://aspire.dev/reference/cli/commands/aspire-publish/)
- xUnit v3 projects are standalone Microsoft Testing Platform executables. Keep direct assembly execution for focused proof because a filtered solution command can silently provide incomplete evidence. [xUnit v3/MTP guidance](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform)

### Previous Story and Git Intelligence

- Story 8.7 established the correct deletion discipline: routing or visible owner code is not approval; record exact provenance, halt at the gate, and keep the entire local rollback path.
- Recent root history is primarily prerequisite/routing reconciliation, package-version repair, and submodule synchronization. It does not by itself close G6/G8/G11/G7-G9.
- The root advanced twice during analysis. At final revalidation Builds and Memories matched their new root pins, while the live Tenants checkout `66f5b2e...` remained ahead of root `00a2895...`. Revalidate immediately before implementation and preserve concurrent user-owned changes.
- Only root-declared submodules may be initialized or updated. Never recurse into nested submodules.

### Testing and Validation Guidance

Build sequentially. Run the relevant direct xUnit v3 executables after each build, then the repository lanes:

```bash
git ls-tree HEAD references/Hexalith.Builds references/Hexalith.Commons references/Hexalith.EventStore references/Hexalith.FrontComposer references/Hexalith.Tenants
git submodule status -- references/Hexalith.Builds references/Hexalith.Commons references/Hexalith.EventStore references/Hexalith.FrontComposer references/Hexalith.Tenants
rg -n "Client command/query|Tenant claims|Aspire|Commons HTTP|MCP, deep-link|Builds shared|Package publishing" _bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md

dotnet build Hexalith.Parties.slnx -c Release -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal
dotnet ./tests/Hexalith.Parties.Client.Tests/bin/Release/net10.0/Hexalith.Parties.Client.Tests.dll
dotnet ./tests/Hexalith.Parties.Mcp.Tests/bin/Release/net10.0/Hexalith.Parties.Mcp.Tests.dll
dotnet ./tests/Hexalith.Parties.AdminPortal.Tests/bin/Release/net10.0/Hexalith.Parties.AdminPortal.Tests.dll
dotnet ./tests/Hexalith.Parties.Authentication.Tests/bin/Release/net10.0/Hexalith.Parties.Authentication.Tests.dll
dotnet ./tests/Hexalith.Parties.Tests/bin/Release/net10.0/Hexalith.Parties.Tests.dll
dotnet ./tests/Hexalith.Parties.Ci.Tests/bin/Release/net10.0/Hexalith.Parties.Ci.Tests.dll

dotnet build Hexalith.Parties.slnx -c Release -m:1 -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal
pwsh scripts/test.ps1 -Lane unit
pwsh scripts/test.ps1 -Lane integration
pwsh scripts/test.ps1 -Lane topology
pwsh scripts/test.ps1 -Lane ci
bash scripts/check-no-warning-override.sh
git diff --check
```

Add focused class runs for `HttpPartiesCommandClientTests`, `HttpPartiesQueryClientTests`, `DependencyInjectionTests`, MCP tool contract/dispatch/fitness, AdminPortal link/capability tests, claims transformation/composition, AppHost topology, platform prerequisite fitness, package/public API snapshots, and container-publication workflow. Run the full executable whenever class names or runner syntax change.

Manual/runtime evidence must cover all five MCP tools, malicious/duplicate identity headers, wrong token audience, missing downstream credential, no credential/identity logging, deep links, rich-search degradation, explicit `parties-mcp` and UI resource dispositions, security audiences, Dapr ACLs, and each supported publish target. Environment-limited lanes stay open.

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
- [Source: `_bmad-output/implementation-artifacts/8-7-data-protection-extraction.md`]
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

- Loaded the complete Epic 8 requirements, whole PRD and architecture, Epic 8 spine/context, draft 8.8 spec, Story 8.3 matrix, predecessor Story 8.7, persistent project contexts, current implementation/test surfaces, recent Git history, and current official MCP, ASP.NET Core, .NET HTTP, Aspire, and xUnit guidance.
- Checklist corrections applied: marked the story honestly blocked; made the authoritative predecessor sequence explicit; split gates by owner surface; distinguished root gitlinks from changing user-owned checkouts; prevented Commons availability from authorizing unrelated deletion; preserved tool/domain and AppHost rollback ownership; added MCP token-passthrough and header-injection defenses; bounded deep-link/health behavior; added claims-project retirement coverage; and required post-switch rollback plus exact evidence per deletion.
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

### Completion Notes List

- Story 8.8 is implementation-ready as a guarded packet but remains blocked for production work by predecessor sequence and producer-specific prerequisites.
- Commons HTTP is the sole matching available slice at creation; it does not waive consumer parity or open the other slices.
- Builds provenance must be refreshed, and EventStore/FrontComposer/claims capabilities need delivered additive APIs plus exact identities and parity packets.
- No product tests were run during story creation because no production code changed. Markdown integrity and repository diff checks are the applicable validation.

### File List

**Added**

- `_bmad-output/implementation-artifacts/8-8-client-mcp-apphost-build-and-deploy-cleanup.md`

**Modified**

- `_bmad-output/implementation-artifacts/sprint-status.yaml`
