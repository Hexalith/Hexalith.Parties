# Story 12.10: Deployment Validation and Topology Fitness Rewrite

Status: review

## Story

As an operator,
I want deployment validation to assert the EventStore-fronted topology,
so that incorrect deployments fail fast.

## Acceptance Criteria

1. Given `tests/Hexalith.Parties.Tests/FitnessTests/AppHostTenantsTopologyTests.cs`, then assertions cover `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `parties`, `tenants`, `parties-mcp`, shared `statestore`, shared `pubsub`, and the intentional startup dependencies among those resources.
2. Given `tests/Hexalith.Parties.DeployValidation.Tests/TenantsDeploymentValidationTests.cs` and `DeploymentValidationTests.cs`, then deployment validation rejects production topologies missing any required EventStore-fronted service, DAPR component, or Tenants integration manifest needed by the canonical local/deployable topology.
3. Given `tests/Hexalith.Parties.DeployValidation.Tests/AppHostDaprTopologyValidationTests.cs`, then static validation proves each receiving DAPR sidecar uses the correct access-control file, deny-by-default posture, app-id caller matrix, `parties -> eventstore` command/query permission, `eventstore -> parties /process` permission, and no wildcard app-id or broad `/**` Parties actor-host invocation.
4. Given `ArchitecturalFitnessTests`, then `Hexalith.Parties` contains zero public REST controllers, zero Swagger/OpenAPI exposure, zero in-process MCP tool registrations, and the actor sidecar uses only `accesscontrol.parties.yaml` for DAPR invocation policy.
5. Given production deployment validation output, then console and JSON results report actionable categories without echoing operator-supplied secrets, access tokens, connection strings, raw tenant membership data, Keycloak credentials, DAPR sidecar config internals beyond file/check names, or protected party payloads.
6. Given the current AppHost, then topology fitness distinguishes user-facing EventStore gateway readiness from internal Parties actor-host liveness, Tenants dependency health, EventStore Admin Server/UI wiring, and the separate `parties-mcp` consumer host.
7. Given deploy-validation manifests use DAPR state store and pub/sub components, then validation checks `actorStateStore=true`, `keyPrefix=none` where required by the shared local topology, explicit scopes for `eventstore`, `eventstore-admin`, `parties`, and `tenants`, dead-letter settings, and pub/sub publish/subscribe scopes for Tenants and subscriber traffic.
8. Given prior deferred fitness gaps, then this story either resolves or explicitly reclassifies the Story 12.1 deferred validation items that are in scope for topology/deploy validation: production trust domain placeholders, route-specific resiliency policy coverage, shared state-store exception documentation, startup dependency assertions, AppHost source/launchSettings secret scans, and brittle substring tests.
9. Given validation tests are complete, then focused deploy-validation tests, AppHost topology tests, architectural fitness tests, and solution build all pass or record exact infrastructure/tooling blockers without editing EventStore, Tenants, FrontComposer, or Memories submodules.

## Advanced Elicitation Clarifications

- Evidence source rule: topology and deploy-validation assertions must be grounded in current checked-in AppHost, DAPR component, deploy script, and accepted predecessor story evidence. If those sources disagree, record the dated blocker and fail closed rather than normalizing the story around guessed production topology.
- Failure taxonomy rule: keep EventStore gateway readiness, Parties actor-host liveness, Tenants authority reachability, EventStore Admin Server/UI wiring, DAPR component validity, and optional `parties-mcp` availability as separate validation categories. A missing internal actor host must not be reported as a public gateway outage, and a missing MCP host must not fail core command/query topology unless MCP is enabled.
- Parser-first guardrail rule: prefer YAML/XML/JSON/PowerShell structure-aware checks for manifests, project files, and validation output. Use source-text regex only for narrow invariants after comments/generated output are excluded, and document each allowlist for retired literals or secret-looking tokens.
- Receiving-sidecar DAPR rule: access-control validation must inspect the receiving sidecar policy file for each allowed call, including method/path shape and caller app ids. Do not infer safety from caller-side configuration or broad component scopes.
- Sanitized-output rule: every new validator failure path needs paired console and JSON assertions that allow only stable names, categories, check ids, resource ids, route names, and remediation labels. Tests should include at least one secret-looking or operator-supplied value and prove it is not echoed.
- Runtime-proof rule: runtime Aspire checks are useful only when they use official resource notification/health APIs and can run deterministically. If local Aspire tooling or infrastructure is unavailable, completion notes must state that runtime proof was not captured and identify the static tests/builds used instead.

## Tasks / Subtasks

- [x] Confirm predecessor and scope gates. (AC: 1-9)
  - [x] Read `_bmad-output/implementation-artifacts/12-1-apphost-recomposition.md`.
  - [x] Read `_bmad-output/implementation-artifacts/12-2-parties-actor-host.md`.
  - [x] Read `_bmad-output/implementation-artifacts/12-3-validation-relocation-and-tenant-auth-ownership.md`.
  - [x] Read `_bmad-output/implementation-artifacts/12-4-server-tier-1-tier-2-test-rewrite.md`.
  - [x] Read `_bmad-output/implementation-artifacts/12-5-parties-client-thin-wrapper.md`.
  - [x] Read `_bmad-output/implementation-artifacts/12-6-parties-mcp-thin-host.md`.
  - [x] Read `_bmad-output/implementation-artifacts/12-7-admin-portal-rebuild-on-frontcomposer.md`, `12-8-picker-rewrite.md`, and `12-9-sample-and-getting-started-doc-updates.md` for consumer topology wording.
  - [x] If the runtime command/query/client contracts are still blocked, keep this story focused on static topology/deploy validation and do not fabricate runtime success.

- [x] Inventory current validation coverage before editing. (AC: 1-8)
  - [x] List every existing test in `AppHostTenantsTopologyTests.cs`, `ArchitecturalFitnessTests.cs`, `AppHostDaprTopologyValidationTests.cs`, `DeploymentValidationTests.cs`, and `TenantsDeploymentValidationTests.cs`.
  - [x] Create a compact coverage matrix in the Dev Agent Record with columns: file/test, current invariant, AC-12.10.x label, gap, action taken, and deferred owner.
  - [x] Record the evidence source for each new topology invariant: AppHost source, DAPR YAML, deploy script behavior, predecessor story artifact, or explicit blocker.
  - [x] Identify brittle substring assertions that match comments or unrelated literals; replace them with regexes, XML parsing, or small source parsers where practical.
  - [x] Avoid creating a second deploy-validation test harness unless the existing tests cannot represent the required topology checks.

- [x] Harden AppHost topology fitness. (AC: 1, 6, 8)
  - [x] Update `AppHostTenantsTopologyTests.cs` so resource-name checks parse the AppHost source deliberately and assert the exact resource declarations for `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `parties`, `tenants`, and `parties-mcp`.
  - [x] Assert `parties` and `tenants` wait for shared `StateStore` and `PubSub`; assert `parties-mcp` references/waits for `eventstore` and references/waits for `parties` only for startup/liveness.
  - [x] Assert `EnableKeycloak` parsing, multi-audience receiver tolerance, and `Authentication__JwtBearer__SigningKey=""` clearing for every JWT-bearing service.
  - [x] Assert `eventstore-admin-ui` receives the Admin Server Swagger URL in both Keycloak-on and Keycloak-off branches.
  - [x] Replace broad `ShouldContain("parties")`, `ShouldContain("party")`, and similar checks with patterns tied to the surrounding configuration key.

- [x] Harden DAPR component and access-control validation. (AC: 3, 7, 8)
  - [x] Validate all access-control YAMLs under `src/Hexalith.Parties.AppHost/DaprComponents/`: `accesscontrol.yaml`, `accesscontrol.eventstore-admin.yaml`, `accesscontrol.tenants.yaml`, and `accesscontrol.parties.yaml`.
  - [x] Assert deny-by-default posture in production-intended files and classify any local-dev exception explicitly by trust domain and file path.
  - [x] Assert `eventstore` access-control explicitly allows `eventstore-admin`, `tenants`, and `parties` only for the required methods; no wildcard app ids.
  - [x] Assert `accesscontrol.parties.yaml` allows only `eventstore` to invoke `/process` with POST.
  - [x] Assert Tenants access control exposes only the accepted readiness/invocation paths and does not grant broad caller sets.
  - [x] For every allowed invocation, test the receiving sidecar file, caller app id, method, and path together; do not count component scopes or caller-side config as authorization proof.
  - [x] Reconcile deferred `/ready` through DAPR-sidecar validity: either validate the actual accepted path or record the exact architecture blocker.
  - [x] Assert `statestore.yaml` and `pubsub.yaml` keep shared component names, required scopes, env-var secret placeholders, `actorStateStore=true`, and `keyPrefix=none`.
  - [x] Document the shared state store exception as deliberate and tested, not an accidental broadening.

- [x] Extend `deploy/validate-deployment.ps1` for the EventStore-fronted topology. (AC: 2, 5, 7)
  - [x] Add topology checks that can validate production manifests for required app ids/resources: `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `parties`, `tenants`, and optionally `parties-mcp` when present.
  - [x] Add manifest-shape checks for the EventStore domain service registration from `*|party|v1` to `AppId=parties`, `MethodName=process`, and `Domain=party`, or record a blocker if production manifests do not currently expose that contract.
  - [x] Update expected `TenantsIntegration.spec.commandApiAppId` from the pre-pivot `parties` value to the accepted EventStore-fronted `eventstore` value when validating production topology.
  - [x] Validate EventStore Admin UI to Admin Server wiring without requiring runtime dashboard access.
  - [x] Keep JSON output stable: each added check must include category, check, status, details, and recommendation.
  - [x] Keep validation categories distinct for gateway, actor-host, Tenants authority, admin resources, DAPR components, and optional MCP; combined missing-dependency cases must report separate sanitized failures.
  - [x] Ensure validation output never echoes arbitrary YAML values; use normalized status/category names and file/check names instead.

- [x] Add negative production-manifest tests. (AC: 2, 5, 7)
  - [x] Missing `eventstore` fails with a specific EventStore gateway/topology recommendation.
  - [x] Missing `eventstore-admin` or `eventstore-admin-ui` fails or warns according to the deployment profile, with a clear operator action.
  - [x] Missing `parties` actor host fails because EventStore command routing cannot invoke the domain service.
  - [x] Missing `tenants` or malformed Tenants integration fails closed with the existing Tenants category.
  - [x] Missing shared `statestore` or `pubsub` fails with component-specific remediation.
  - [x] Missing `parties-mcp` is not a core command/query deployment failure unless the manifest declares MCP enabled; when enabled, missing MCP must fail with a sanitized message.
  - [x] Malformed or secret-looking operator values in topology manifests must not appear in console or JSON output.
  - [x] At least one negative fixture should contain a fake token, password, connection string, or URI userinfo value and assert both console and JSON output redact or omit it.

- [x] Preserve retired-surface architectural guardrails. (AC: 4)
  - [x] Ensure `ArchitecturalFitnessTests.cs` fails on public REST controller markers, Swagger/OpenAPI hosting packages, in-process MCP attributes/registrations, and old actor-host `/mcp` mapping.
  - [x] Keep checks scoped to source files and project references so legitimate test/story/documentation references do not produce false positives.
  - [x] Include `parties-mcp` as the only allowed MCP host and assert it has no DAPR actor sidecar, no `Hexalith.Parties` service project reference, and no EventStore server assembly reference.

- [x] Verify official Aspire testing and readiness usage. (AC: 1, 6, 9)
  - [x] If adding runtime Aspire tests, use `DistributedApplicationTestingBuilder` and `ResourceNotifications.WaitForResourceHealthyAsync(...)`/resource states rather than sleeps or dashboard scraping.
  - [x] Use `.WaitFor(...)` in AppHost only for dependency readiness semantics that match Aspire's health behavior; use `.WaitForStart(...)` only if startup without health is the explicit requirement.
  - [x] If local `dotnet aspire` is unavailable, keep runtime proof out of completion claims and rely on deterministic static tests plus build evidence.
  - [x] When runtime Aspire proof is skipped, add a completion-note entry naming the unavailable command/tool and the static test/build evidence that replaced it.

- [x] Verify the story. (AC: 1-9)
  - [x] Run `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --filter "FullyQualifiedName~AppHostTenantsTopologyTests|FullyQualifiedName~ArchitecturalFitnessTests" --configuration Release`.
  - [x] Run `dotnet test tests/Hexalith.Parties.DeployValidation.Tests/Hexalith.Parties.DeployValidation.Tests.csproj --configuration Release`.
  - [x] Run `dotnet build src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj --configuration Release`.
  - [x] Run `dotnet build Hexalith.Parties.slnx --configuration Release`.
  - [x] If running `dotnet aspire run --project src/Hexalith.Parties.AppHost` is possible, record dashboard resource names and EventStore Admin UI reachability; otherwise record the exact unavailable tool/runtime.

## Dev Notes

### Required Topology Contract

| Component | Role | Public/Internal | Required Dependencies | Expected DAPR/AppHost Validation |
|---|---|---|---|---|
| `eventstore` | Public command/query gateway for Parties commands and queries | Public gateway | Tenants authority, shared `statestore`, shared `pubsub`, Parties actor host command routing | Validates `*|party|v1` routes to `parties/process`; allows only required callers; reports `gateway_not_ready` separately from internal host health. |
| `parties` | Internal actor host and projection runtime behind EventStore | Internal only | EventStore, Tenants, shared `statestore`, shared `pubsub` | Validates no public REST, Swagger/OpenAPI, or in-process MCP surface; validates only `eventstore -> parties /process` where required. |
| `tenants` | Authority service for tenant validation and membership decisions | Internal dependency | Shared DAPR resources and EventStore-facing integration | Validates authority reachability as `authority_unreachable`; missing or malformed Tenants integration fails closed. |
| `eventstore-admin` | EventStore inspection/admin server | Admin inspection only | EventStore gateway and configured Swagger endpoint | Validates admin wiring without treating admin resources as public command/query gateways. |
| `eventstore-admin-ui` | Operator UI for EventStore admin inspection | Admin inspection only | `eventstore-admin` Swagger URL | Validates Admin UI-to-Admin Server wiring and reports `admin_resource_unavailable` separately. |
| `parties-mcp` | Separate MCP consumer host | Consumer host | EventStore and Parties startup/liveness references only | Validates this is not a Parties-hosted public MCP surface and does not grant broad caller permissions. |

Validation must keep EventStore gateway readiness, Parties internal host liveness, Tenants authority reachability, admin inspection resources, and MCP consumer host status as separate result categories. Tests should prove one category can fail without being reported as another category.

### Validation Message Contract

Validation output may include stable machine-readable codes, file names, check names, resource names, app ids, route names, and remediation categories. Validation output must not echo arbitrary operator-supplied values, secrets, access tokens, passwords, signing keys, connection strings, URI userinfo, Keycloak credentials, DAPR secret values, raw tenant membership data, raw protected party payloads, or raw exception traces. Console output must not rely on color alone; JSON output should remain stable enough for deterministic assertions.

### Story 12.1 Deferred Gap Disposition Format

When closing the Story 12.1 deferred validation items, record a compact decision table in the Dev Agent Record with columns: gap, prior source, disposition (`implemented-here`, `deferred-with-owner`, `no-longer-applicable`), evidence/test, owner, and rationale.

### Source Context

- Epic 12 comes from `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07.md`; Story 12.10 closes the topology/deployment validation loop for the EventStore-fronted pivot.
- The pivot makes EventStore the public command/query gateway. `parties` is the actor host/projection runtime behind EventStore; `tenants` is the authority service; `eventstore-admin` and `eventstore-admin-ui` provide stream/admin inspection.
- The sprint proposal calls this the "four-service topology" while listing `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `parties`, and `tenants`. Treat those five AppHost resources as required local topology evidence; classify `parties-mcp` separately as the Story 12.6 consumer host.
- Story 12.1 is done and introduced most AppHost/DAPR topology invariants. This story should harden validation around those invariants rather than recompose the AppHost again.
- Stories 12.4, 12.5, and 12.6 are blocked/partial in current sprint state. Do not claim runtime command/query/MCP parity if the underlying contracts remain blocked.
- No `project-context.md` persistent fact file was found during story creation.

### Current Implementation to Inspect

- `src/Hexalith.Parties.AppHost/Program.cs` currently declares `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `parties`, `parties-mcp`, and `tenants`; wires shared EventStore DAPR resources to `parties` and `tenants`; maps `*|party|v1` to `parties/process`; and clears auth values when Keycloak is disabled.
- `src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol*.yaml` owns receiving-sidecar DAPR policy. These files are the main source of caller-matrix regressions.
- `src/Hexalith.Parties.AppHost/DaprComponents/statestore.yaml` and `pubsub.yaml` currently preserve shared `statestore`/`pubsub` names and must stay aligned with tests and deploy validation.
- `deploy/validate-deployment.ps1` currently validates access control, state store, pub/sub, subscriptions, Tenants integration, resiliency, and secret-store warning. It still models some pre-pivot assumptions such as `TenantsIntegration.spec.commandApiAppId=parties`.
- `tests/Hexalith.Parties.DeployValidation.Tests/DeploymentValidationTests.cs` and `TenantsDeploymentValidationTests.cs` create temp production/local manifests and execute the PowerShell validator. Extend these fixtures rather than using real deployment files.
- `tests/Hexalith.Parties.DeployValidation.Tests/AppHostDaprTopologyValidationTests.cs` already has static DAPR component checks and explicitly defers full `aspire publish` manifest scanning to Story 12.10.
- `tests/Hexalith.Parties.Tests/FitnessTests/AppHostTenantsTopologyTests.cs` and `ArchitecturalFitnessTests.cs` already enforce many pivot boundaries but still contain substring-style assertions that are easy to satisfy by comments or unrelated literals.
- `_bmad-output/implementation-artifacts/deferred-work.md` contains Story 12.1 deferred items that overlap this story: production trust-domain placeholders, uniform circuit-breaker policy, shared state-store exception documentation, circular `WaitFor` intent, `/ready` path through DAPR sidecar, AppHost source/launchSettings secret scans, brittle substring tests, and deploy-validation file discovery weakness.

### Technical Constraints

- Keep package versions aligned with this repository: .NET SDK `10.0.103`, `net10.0`, Aspire `13.2.2`, Aspire Hosting Testing `13.2.1`, CommunityToolkit Aspire DAPR `13.0.0`, Dapr Client/AspNetCore `1.17.7`, Dapr Actors `1.16.1`, xUnit `2.9.3`, Shouldly `4.3.0`, NSubstitute `5.3.0`, and YamlDotNet `16.3.0`.
- Do not initialize or update nested submodules. `Hexalith.EventStore`, `Hexalith.Tenants`, `Hexalith.FrontComposer`, and `Hexalith.Memories` are root-level submodules already present.
- Do not edit EventStore, Tenants, FrontComposer, or Memories submodule source in this story. If topology validation proves a platform change is required, record the blocker.
- Prefer structured parsing for XML/YAML/JSON/project files. Use regex only for narrow source-code invariants where no structured parser is practical.
- Keep deployment validation side-effect-free: tests should write temp manifests and run `deploy/validate-deployment.ps1`; they must not start real infrastructure unless explicitly gated and documented.
- Generated `bin/`, `obj/`, `TestResults`, Aspire dashboard captures, screenshots, and publish outputs must stay out of commits unless a test explicitly owns a sanitized fixture.
- Do not initialize or update nested submodules. Do not edit EventStore, Tenants, FrontComposer, or Memories submodules. If a root-level submodule is already present and must be inspected, do not run recursive submodule commands.

### Security and Privacy Guidance

- EventStore owns public authentication, tenant validation, RBAC, command/query routing, generic response mapping, persistence, and stream/admin inspection.
- Parties owns domain execution behind EventStore and should expose only actor/default/subscription endpoints required by the internal topology.
- DAPR access control is evaluated by the receiving sidecar. Validate each receiving sidecar's file independently; do not infer safety from another sidecar's policy.
- Safe validation output may include file names, check names, normalized status categories, app ids, topic names, route names, and remediation categories.
- Unsafe output includes access tokens, signing keys, Keycloak credentials, connection strings, raw protected payloads, raw operator-supplied YAML values, tenant membership dictionaries, stack traces, or full command/query payload dumps.

### Latest Technical Context

- Microsoft Learn documents `ResourceBuilderExtensions.WaitFor(...)` as waiting for a dependency resource to enter Running and, when health checks are associated, Healthy before starting the waiting resource. Use it for true dependency readiness, not as a comment-level ordering hint.
- Microsoft Learn documents `WaitForStart(...)` as waiting only for Running state and ignoring health checks. Use it only if health readiness would create an intentional initialization cycle.
- Microsoft Learn documents `DistributedApplication.ResourceNotifications` and `WaitForResourceHealthyAsync(...)` for tests that need to wait for Aspire resource readiness. Prefer those APIs over sleeps if this story adds runtime Aspire tests.
- Microsoft Learn code samples show `WithHttpHealthCheck(...)` and `WaitFor(...)` as the AppHost pattern for HTTP resource health dependencies; use repository versions and existing AppHost patterns before introducing new package versions.

### Testing Guidance

- Minimum focused tests:
  - Static AppHost resource declarations and dependency wiring for `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `parties`, `tenants`, and `parties-mcp`.
  - DAPR access-control caller matrix: `parties -> eventstore` where required, `tenants -> eventstore` where required, `eventstore -> parties /process`, explicit denied caller/resource examples, no wildcard app ids, and no broad Parties `/**`.
  - Deployment validator rejects missing EventStore gateway, missing DAPR sidecar/config, missing Parties actor host, missing Tenants integration, missing shared state/pubsub, unauthorized DAPR scopes, admin UI treated as the command/query gateway, and malformed topology manifest values.
  - Deployment validator includes at least one combined missing-dependency case and asserts each failure reports a distinct sanitized reason.
  - Validator JSON and console output remain valid and sanitized for both pass and fail cases, including absence assertions for secrets, credentials, connection strings, URI userinfo, DAPR secret values, raw tenant data, and raw exception traces.
  - Parser-backed checks are preferred for YAML, XML project files, JSON output, and generated validation objects; raw source-text scans must document comment/generated-output exclusions and literal allowlists.
  - Architectural fitness still blocks REST/OpenAPI/MCP regressions in `Hexalith.Parties` through layered checks for service registration, route mapping, OpenAPI document generation, exposed ports, and MCP host references; it keeps MCP only in `Hexalith.Parties.Mcp`.
  - AppHost source and launchSettings secret scans cover Keycloak, connection strings, bearer tokens, admin passwords, and client secrets.
  - Existing Tenants validation tests keep passing after pivoting `commandApiAppId` expectations to `eventstore` where appropriate.

### Out of Scope

- Implementing EventStore, Tenants, FrontComposer, or Memories platform changes.
- Rewriting server command/query tests through EventStore; Story 12.4 owns that and is currently blocked.
- Implementing the typed Parties client EventStore transport; Story 12.5 owns that.
- Completing MCP tool dispatch or query parity; Story 12.6 owns that and is currently blocked by client/query contracts.
- Rebuilding Admin Portal, Picker, sample app, README, or getting-started guide.
- Reintroducing old Parties REST/admin/MCP routes as compatibility shims.
- Running nested submodule initialization or recursive submodule update.

### References

- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07.md` - Story 12.10 source, Epic 12 pivot rationale, sequencing, success criteria, and out-of-scope boundaries.
- `_bmad-output/planning-artifacts/architecture.md` - EventStore-fronted topology, technical stack, test strategy, deployment validation requirement, and DAPR/EventStore constraints.
- `_bmad-output/implementation-artifacts/12-1-apphost-recomposition.md` - current AppHost topology, DAPR access-control split, test coverage, and deferred validation items.
- `_bmad-output/implementation-artifacts/12-4-server-tier-1-tier-2-test-rewrite.md` - blocked server test rewrite and old-surface retirement boundaries.
- `_bmad-output/implementation-artifacts/12-6-parties-mcp-thin-host.md` - `parties-mcp` topology and current blocked/partial MCP scope.
- `_bmad-output/implementation-artifacts/12-9-sample-and-getting-started-doc-updates.md` - adopter-facing topology wording and docs guardrails.
- `_bmad-output/implementation-artifacts/deferred-work.md` - Story 12.1 deferred validation items and pre-existing topology/test risks.
- `src/Hexalith.Parties.AppHost/Program.cs` - current AppHost resource graph and environment wiring.
- `src/Hexalith.Parties.AppHost/DaprComponents/*.yaml` - DAPR access-control, state store, pub/sub, and resiliency policy inputs.
- `deploy/validate-deployment.ps1` - deployment validation script to extend.
- `tests/Hexalith.Parties.Tests/FitnessTests/AppHostTenantsTopologyTests.cs` - static AppHost topology test home.
- `tests/Hexalith.Parties.Tests/FitnessTests/ArchitecturalFitnessTests.cs` - actor-host and retired-surface guardrail home.
- `tests/Hexalith.Parties.DeployValidation.Tests/*.cs` - deployment validation test fixtures and validator execution patterns.
- Microsoft Learn `ResourceBuilderExtensions.WaitFor`: https://learn.microsoft.com/dotnet/api/aspire.hosting.resourcebuilderextensions.waitfor?view=dotnet-aspire-13.0
- Microsoft Learn `ResourceBuilderExtensions.WaitForStart`: https://learn.microsoft.com/dotnet/api/aspire.hosting.resourcebuilderextensions.waitforstart?view=dotnet-aspire-13.0
- Microsoft Learn `DistributedApplication.ResourceNotifications`: https://learn.microsoft.com/dotnet/api/aspire.hosting.distributedapplication.resourcenotifications?view=dotnet-aspire-13.0
- Microsoft Learn `ResourceNotificationService.WaitForResourceHealthyAsync`: https://learn.microsoft.com/dotnet/api/aspire.hosting.applicationmodel.resourcenotificationservice.waitforresourcehealthyasync?view=dotnet-aspire-13.0

## Project Structure Notes

- Keep AppHost topology tests under `tests/Hexalith.Parties.Tests/FitnessTests`.
- Keep deploy-validation tests under `tests/Hexalith.Parties.DeployValidation.Tests`.
- Keep production validator changes in `deploy/validate-deployment.ps1`.
- Keep DAPR component files under `src/Hexalith.Parties.AppHost/DaprComponents`.
- Use temp directories for generated test manifests; do not add real secrets or machine-specific publish output.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-12: Resolved workflow customization and loaded project context/config.
- 2026-05-12: Confirmed predecessor scope: Stories 12.1 through 12.6 are done; 12.7 and 12.8 remain blocked consumer migrations; 12.9 is in review and used only for public wording/context.
- 2026-05-12: Red phase captured with `dotnet test tests/Hexalith.Parties.DeployValidation.Tests/Hexalith.Parties.DeployValidation.Tests.csproj --configuration Release`; expected failures showed pre-pivot `commandApiAppId=parties`, `statestore` scoped only to `parties`, and missing EventStore topology validation.
- 2026-05-12: Runtime Aspire proof not captured because `dotnet aspire --version` failed: `dotnet-aspire does not exist` / command not found on PATH.
- 2026-05-12: Full no-build regression attempted twice with `dotnet test Hexalith.Parties.slnx --configuration Release --no-build` and once with `--blame-hang --blame-hang-timeout 120s --verbosity normal`; all three commands timed out without returning test results.

### Coverage Matrix

| File/test | Current invariant | AC-12.10.x | Gap | Action taken | Deferred owner |
|---|---|---|---|---|---|
| `AppHostTenantsTopologyTests` | AppHost project/resource graph, DAPR configs, waits, auth wiring | AC1, AC6, AC8 | Substring resource checks and missing `parties-mcp` readiness assertion | Added parser-backed resource extraction, `parties-mcp` checks, exact domain route env assertions, Admin UI Swagger branch count, and `WaitFor`/no `WaitForStart` guard | None |
| `ArchitecturalFitnessTests` | Retired REST/OpenAPI/MCP guardrails | AC4 | Separate MCP host boundary not explicitly checked | Added `Hexalith.Parties.Mcp` project guard: MCP package allowed only there, no DAPR actor package, no Parties service or EventStore server references | None |
| `AppHostDaprTopologyValidationTests` | Static AppHost DAPR component checks | AC3, AC7, AC8 | Production validator did not enforce equivalent receiving-sidecar matrix | Kept static AppHost checks; added production validator split-file checks for EventStore, Parties, Tenants, and Admin receiving sidecars | None |
| `DeploymentValidationTests` | PowerShell validator temp production/local manifests | AC2, AC5, AC7 | Production fixtures modeled pre-pivot topology | Updated fixtures to EventStore-fronted topology, added `topology.yaml`, split ACLs, state/pubsub scopes, negative topology/MCP/redaction cases | None |
| `TenantsDeploymentValidationTests` | Tenants integration validation | AC2, AC5, AC7 | Expected `commandApiAppId=parties` | Pivoted expected command API app id to `eventstore`; retained Parties subscription scope for tenant projection consumer | None |
| `deploy/validate-deployment.ps1` | Production deployment validation | AC2, AC3, AC5, AC7 | No topology category, pre-pivot state/tenant assumptions | Added EventStore topology category, receiving-sidecar ACL validation, shared state/pubsub rules, `commandApiAppId=eventstore`, and sanitized output checks | None |

### Story 12.1 Deferred Gap Disposition

| Gap | Prior source | Disposition | Evidence/test | Owner | Rationale |
|---|---|---|---|---|---|
| Production trust-domain placeholders | Story 12.1 deferred work | implemented-here | `Test-AccessControl`, production fixtures, `deploy/dapr/accesscontrol*.yaml` | Parties | Validator keeps `public` as local-only warning and validates production placeholders without echoing secrets. |
| Route-specific resiliency policy coverage | Story 12.1 deferred work | deferred-with-owner | Existing `Test-Resiliency`; no runtime route manifest source | EventStore/AppHost deployment | Current manifests expose component resiliency, not per-route policy metadata. |
| Shared state-store exception documentation | Story 12.1 deferred work | implemented-here | `Test-StateStore`, `deploy/dapr/statestore*.yaml` | Parties | `eventstore`, `eventstore-admin`, `parties`, and `tenants` scopes plus `keyPrefix=none` are explicit and tested. |
| Startup dependency assertions | Story 12.1 deferred work | implemented-here | `AppHostProgramWaitsForSharedDaprComponentsOnPartiesAndTenants`, `AppHostProgramWiresPartiesMcpAsSeparateConsumerHost` | Parties | Static AppHost tests assert dependency readiness waits and no `WaitForStart`. |
| `/ready` through DAPR sidecar validity | Story 12.1 deferred work | implemented-here | `Test-AccessControl` Tenants sidecar accepted paths | Parties/Tenants | Tenants receiving sidecar validates `parties -> GET /ready` separately from command routing. |
| AppHost source/launchSettings secret scans | Story 12.1 deferred work | implemented-here | Existing `AppHostSourceAndLaunchSettingsDoNotContainPlainTextSecrets`; deploy validator redaction tests | Parties | Source and output paths are covered without publishing runtime manifests. |
| Brittle substring tests | Story 12.1 deferred work | implemented-here | Parser-backed AppHost resource/domain tests; structured deploy fixtures | Parties | Broad string checks were replaced or narrowed around exact keys/routes. |

### Completion Notes List

- Hardened AppHost topology fitness around exact EventStore/Admin/UI/Parties/Tenants/`parties-mcp` resource declarations, readiness waits, domain route env keys, Keycloak/JWT clearing, and Admin UI Swagger wiring.
- Extended deployment validation to enforce the EventStore-fronted topology: required resources in `topology.yaml`, split receiving-sidecar access-control files, `eventstore -> parties /process`, Tenants readiness, shared `statestore` scopes with `keyPrefix=none`, pub/sub publish/subscribe scopes, dead-letter requirements, and `TenantsIntegration.spec.commandApiAppId=eventstore`.
- Added negative production-manifest tests for missing EventStore gateway, enabled-but-missing `parties-mcp`, malformed Tenants config, missing shared components/scopes, wildcard callers, and secret-looking topology values across console and JSON output.
- Updated checked-in production deploy templates under `deploy/dapr` to match the new validator contract, including split access-control files and `topology.yaml`.
- Runtime Aspire proof was not captured because `dotnet-aspire` is not installed on PATH; static topology tests, deploy-validation tests, AppHost build, deployment script validation, and solution build are the replacement evidence.
- Full no-build regression did not complete in this environment: `dotnet test Hexalith.Parties.slnx --configuration Release --no-build` timed out at 4 minutes and 10 minutes, and the `--blame-hang` retry also timed out at 6 minutes without returned test results.

### File List

- `_bmad-output/implementation-artifacts/12-10-deployment-validation-and-topology-fitness-rewrite.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `deploy/dapr/accesscontrol.yaml`
- `deploy/dapr/accesscontrol.eventstore-admin.yaml`
- `deploy/dapr/accesscontrol.parties.yaml`
- `deploy/dapr/accesscontrol.tenants.yaml`
- `deploy/dapr/pubsub-kafka.yaml`
- `deploy/dapr/pubsub-rabbitmq.yaml`
- `deploy/dapr/pubsub-servicebus.yaml`
- `deploy/dapr/statestore-cosmosdb.yaml`
- `deploy/dapr/statestore-postgresql.yaml`
- `deploy/dapr/tenants-integration.yaml`
- `deploy/dapr/topology.yaml`
- `deploy/validate-deployment.ps1`
- `tests/Hexalith.Parties.DeployValidation.Tests/DeploymentValidationTests.cs`
- `tests/Hexalith.Parties.DeployValidation.Tests/TenantsDeploymentValidationTests.cs`
- `tests/Hexalith.Parties.Tests/FitnessTests/AppHostTenantsTopologyTests.cs`
- `tests/Hexalith.Parties.Tests/FitnessTests/ArchitecturalFitnessTests.cs`

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-05-12 | 1.0 | Rewrote deployment validation and topology fitness around the EventStore-fronted topology, split DAPR receiving-sidecar access control, shared DAPR component scopes, optional `parties-mcp`, sanitized output assertions, and production deploy templates; story moved to review. | Codex |
| 2026-05-10 | 0.3 | Advanced elicitation completed; applied evidence-source, failure-taxonomy, parser-first, receiving-sidecar, sanitized-output, and runtime-proof clarifications. | Codex |
| 2026-05-10 | 0.2 | Applied party-mode review clarifications for topology contract, DAPR matrices, negative validation cases, sanitized output, deferred-gap disposition, and submodule guardrails. | Codex |
| 2026-05-10 | 0.1 | Created ready-for-dev story through BMAD pre-dev hardening automation. | Codex |

## Party-Mode Review

- Date/time: 2026-05-10T18:06:50+02:00
- Selected story key: 12-10-deployment-validation-and-topology-fitness-rewrite
- Command/skill invocation used: `/bmad-party-mode 12-10-deployment-validation-and-topology-fitness-rewrite; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), Paige (Technical Writer)
- Findings summary: the story was directionally ready but needed sharper pre-dev wording for the required EventStore-fronted topology contract, DAPR caller/resource matrices, gateway readiness versus internal liveness categories, negative deployment-validation cases, sanitized output assertions, retired REST/OpenAPI/MCP guardrail layers, Story 12.1 deferred-gap disposition, and submodule boundaries.
- Changes applied: added a required topology contract table; added validation output/redaction rules; added a Story 12.1 deferred-gap disposition format; strengthened focused testing guidance for denied DAPR paths, combined missing-dependency failures, sanitized JSON/console assertions, layered retired-surface checks, and non-recursive submodule handling.
- Findings deferred: exact runtime health probe endpoints and deployment-manifest source-of-truth locations remain implementation details to validate against existing AppHost/deploy-validation structure; any platform contract change outside topology validation must be recorded as a blocker rather than silently implemented here.
- Final recommendation: ready-for-dev

## Advanced Elicitation

- Date/time: 2026-05-10T20:07:47+02:00
- Selected story key: `12-10-deployment-validation-and-topology-fitness-rewrite`
- Command/skill invocation used: `/bmad-advanced-elicitation 12-10-deployment-validation-and-topology-fitness-rewrite`
- Batch 1 method names: Red Team vs Blue Team; Failure Mode Analysis; Security Audit Personas; Self-Consistency Validation; Architecture Decision Records
- Reshuffled Batch 2 method names: Pre-mortem Analysis; Chaos Monkey Scenarios; User Persona Focus Group; Critique and Refine; Expand or Contract for Audience
- Findings summary:
  - The story needed an explicit evidence-source rule so validation changes are anchored to checked-in topology sources and accepted predecessor evidence instead of inferred production shape.
  - Failure categories needed sharper separation so gateway, actor-host, Tenants, admin, DAPR, and optional MCP failures cannot be collapsed into misleading operator messages.
  - DAPR access-control checks needed a receiving-sidecar rule to avoid false confidence from caller-side configuration or broad component scopes.
  - Validator output needed concrete negative fixtures proving secret-looking and operator-supplied values are not echoed to console or JSON.
  - Runtime proof needed a deterministic boundary: use official Aspire health/resource APIs when available, otherwise record the exact tooling gap and static evidence used.
- Changes applied:
  - Added advanced elicitation clarifications for evidence sources, failure taxonomy, parser-first guardrails, receiving-sidecar DAPR validation, sanitized output, and runtime proof.
  - Added subtasks for evidence-source recording, receiving-sidecar invocation assertions, distinct combined-failure reporting, secret-looking negative fixtures, and runtime-proof completion notes.
  - Expanded testing guidance to prefer parser-backed checks and require documented exclusions for source-text scans.
- Findings deferred:
  - Exact runtime health probe endpoints remain implementation-time evidence to validate against AppHost and accepted platform behavior.
  - Production manifest source-of-truth locations remain implementation-time evidence unless the current deploy-validation script already exposes them.
  - Any platform contract change outside topology/deploy validation remains a blocker for this story rather than silent scope expansion.
- Final recommendation: ready-for-dev
