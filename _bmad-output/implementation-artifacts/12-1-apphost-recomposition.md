# Story 12.1: AppHost Recomposition

Status: ready-for-dev

## Story

As a developer running Hexalith.Parties locally,
I want the AppHost to start the EventStore service, EventStore Admin Server, EventStore Admin UI, Parties actor host, and Tenants service all sharing the same Redis state store and pub/sub,
so that the local topology matches the canonical platform deployment shape.

## Acceptance Criteria

1. Given `dotnet aspire run --project src/Hexalith.Parties.AppHost`, when the application starts, then resources named `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `parties`, and `tenants` are visible in the Aspire dashboard.
2. Given the running topology, when the EventStore Admin UI is opened, then Parties events written by the Parties actor host are visible in the EventStore stream browser.
3. Given `src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj`, then project references include `Hexalith.EventStore`, `Hexalith.EventStore.Admin.Server.Host`, `Hexalith.EventStore.Admin.UI`, and `Hexalith.EventStore.Aspire`, with Aspire integration/library projects marked `IsAspireProjectResource="false"` where appropriate.
4. Given AppHost DAPR components, then `accesscontrol.yaml`, `accesscontrol.eventstore-admin.yaml`, `accesscontrol.tenants.yaml`, `accesscontrol.parties.yaml`, and `resiliency.yaml` exist under `src/Hexalith.Parties.AppHost/DaprComponents/`.
5. Given Keycloak is enabled, then `eventstore`, `eventstore-admin`, `parties`, and `tenants` wire OIDC consistently through the same realm URL and compatible audience/client settings.
6. Given Story 12.0 is the Epic 12 gate, then implementation does not start unless Story 12.0 has a dated `## Spike Conclusion` with a positive or explicitly partial recommendation that documents the working domain/app-id convention, required DAPR/EventStore configuration, and command/query evidence or classified blocker.
7. Given this is AppHost recomposition only, then the implementation does not remove Parties REST controllers, MCP wiring, public mappings, client/admin/picker migration paths, broad integration suites, EventStore submodule code, or production manifests beyond the DAPR component files required for this local topology.
8. Given topology and deploy-validation tests, then deterministic tests assert the five AppHost resources, exact EventStore project references, DAPR sidecar configuration files, stable `statestore` and `pubsub` component names, Parties actor-host app id, Keycloak environment variable/resource wiring, and absence of plain-text secrets in generated or loggable deployment output.

## Tasks / Subtasks

- [ ] Confirm the Story 12.0 gate before implementation. (AC: 1, 2)
  - [ ] Read `_bmad-output/implementation-artifacts/12-0-eventstore-parties-actor-invocation-feasibility-spike.md`.
  - [ ] If `## Spike Conclusion` is still pending, negative, or lacks the domain/app-id and DAPR/EventStore evidence required by AC6, stop and mark this story blocked through the normal dev workflow; do not start AppHost recomposition.
  - [ ] If the spike conclusion documents required domain/app-id configuration, tenant/auth shortcuts, EventStore limitations, or query blockers, carry that exact result into the AppHost and DAPR component changes.
- [ ] Recompose `Hexalith.Parties.AppHost` around standalone EventStore resources. (AC: 1, 3)
  - [ ] Add AppHost project references for EventStore service, Admin Server Host, Admin UI, and EventStore Aspire integration without initializing or updating nested submodules.
  - [ ] Replace the current self-hosted `AddHexalithParties` topology with explicit `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `parties`, and `tenants` project resources.
  - [ ] Call `AddHexalithEventStore` once to create the shared `statestore` and `pubsub`; reuse those resources for `parties` and `tenants`.
  - [ ] Keep `parties` as the Parties actor host resource with DAPR `appId="parties"`, not as EventStore itself.
- [ ] Split DAPR access-control configuration by receiving sidecar. (AC: 4)
  - [ ] Keep `accesscontrol.yaml` as the EventStore sidecar configuration, following the EventStore AppHost pattern.
  - [ ] Add `accesscontrol.eventstore-admin.yaml` for the Admin Server sidecar.
  - [ ] Add `accesscontrol.tenants.yaml` for the Tenants sidecar, allowing the required EventStore-origin service invocation path.
  - [ ] Add `accesscontrol.parties.yaml` for the Parties actor host sidecar, allowing EventStore to invoke Parties through POST-only DAPR service invocation.
  - [ ] Ensure each service loads only its own `Configuration` CRD path; do not keep one shared access-control file for all sidecars.
  - [ ] Preserve stable shared component names `statestore` and `pubsub`; the access-control split must not introduce alternate state/pubsub component names unless a later architecture decision explicitly approves it.
  - [ ] Treat access-control files as receiving-sidecar ownership files: shared defaults stay in the EventStore pattern, while app-specific caller permissions stay in that service's dedicated file.
- [ ] Preserve shared state and pub/sub semantics. (AC: 1, 2, 4)
  - [ ] Use one shared Redis-backed state store with `actorStateStore=true` and `keyPrefix=none`.
  - [ ] Scope the state store to the services that require it in the recomposed topology; do not grant domain-service-style broad infrastructure access unless Story 12.0 proves it is required.
  - [ ] Preserve Tenants event subscription inputs: `Tenants__Enabled`, `Tenants__ServiceName`, `Tenants__PubSubName`, and `Tenants__TopicName`.
  - [ ] Re-evaluate `Tenants__CommandApiAppId`; it currently points to `parties`, but the EventStore-fronted topology may require `eventstore`.
- [ ] Wire Keycloak and auth settings consistently. (AC: 5)
  - [ ] Keep the existing `EnableKeycloak=false` local fallback.
  - [ ] Reuse one Keycloak realm import and realm URL for EventStore, Admin Server, Parties, and Tenants.
  - [ ] Use service-appropriate audiences: EventStore/Admin should follow EventStore conventions; Parties should keep only settings still needed by the actor host after the pivot.
  - [ ] Preserve the explicit `Authentication__JwtBearer__SigningKey` clearing when OIDC is active.
  - [ ] Preserve existing auth resource wiring, environment variable names, realm/client references, secret references, callback/logout assumptions, and startup ordering unless the Story 12.0 conclusion proves a required change.
- [ ] Update topology tests and deployment validation coverage. (AC: 1, 3, 4, 5)
  - [ ] Update `tests/Hexalith.Parties.Tests/FitnessTests/AppHostTenantsTopologyTests.cs` to assert all five resource names and the new EventStore project references.
  - [ ] Add or update tests that assert AppHost resolves all four access-control files and `resiliency.yaml`.
  - [ ] Extend deployment validation tests to cover the new four-service topology and ensure output does not echo sensitive operator-supplied values.
  - [ ] Add a focused verification that the EventStore Admin UI is wired to Admin Server and can be reached from the Aspire dashboard endpoints.
  - [ ] Keep deterministic topology tests static where possible; they must not require local Aspire or DAPR startup to catch resource-name, project-reference, sidecar, and component-name drift.
  - [ ] Add assertions that generated or loggable deployment output does not expose Keycloak credentials, connection strings, tokens, admin passwords, or operator-supplied secrets in plain text.
  - [ ] Assert `accesscontrol.parties.yaml` allows the Story 12.0-proven EventStore caller path to `parties`, preserves the `parties` actor-host app id, and does not grant wildcard app ids or broad caller sets without an explicit deferred decision.
- [ ] Document local operation evidence. (AC: 1, 2)
  - [ ] Record the exact `dotnet aspire run --project src/Hexalith.Parties.AppHost` command used.
  - [ ] Capture the Aspire dashboard resource names and endpoint URLs observed locally.
  - [ ] Document how to open the EventStore Admin UI and confirm Parties event visibility.
  - [ ] If local Aspire or DAPR is unavailable, document that limitation separately; static topology, deploy-validation, and build tests remain required.

## Dev Notes

### Source Context

- Epic 12 comes from `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07.md`; `_bmad-output/planning-artifacts/epics.md` has not yet been rewritten with Epic 12, so the sprint-change proposal is the authoritative source for this story.
- The pivot decision is that clients submit commands and queries to `eventstore`; EventStore routes to the `parties` actor/domain host through DAPR. This story creates the local topology that makes that shape visible and runnable.
- Story 12.0 is the gate. This story is ready as implementation context, but the dev agent must not implement it until 12.0 has a positive feasibility conclusion.

### Current Implementation to Inspect

- `src/Hexalith.Parties.AppHost/Program.cs` currently starts `parties` and `tenants`, delegates through `AddHexalithTenants`, then calls `AddHexalithParties` with Tenants shared DAPR resources. It does not create separate `eventstore`, `eventstore-admin`, or `eventstore-admin-ui` resources.
- `src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj` currently references Parties, Parties Aspire, Tenants, and Tenants Aspire. It does not reference EventStore service/admin/UI projects or `Hexalith.EventStore.Aspire`.
- `src/Hexalith.Parties.Aspire/HexalithPartiesExtensions.cs` currently wraps Parties as both the EventStore resource and the Parties resource in `HexalithPartiesResources`. Story 12.1 should avoid relying on that shortcut for the final topology.
- `Hexalith.EventStore/src/Hexalith.EventStore.AppHost/Program.cs` is the canonical pattern for separate `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `tenants`, and domain-service sidecars.
- `Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs` creates shared `statestore` and `pubsub`, sets EventStore DAPR `AppId = "eventstore"`, uses fixed EventStore DAPR HTTP port `3501`, injects admin observability links, wires Admin Server to the state store, and exposes Admin UI external HTTP endpoints.
- `src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.yaml`, `statestore.yaml`, and `pubsub.yaml` still describe the old single-service `parties` topology. They must be reconciled with the EventStore-fronted topology rather than copied forward unchanged.
- `tests/Hexalith.Parties.Tests/FitnessTests/AppHostTenantsTopologyTests.cs` currently asserts the Tenants composition only. It is the right narrow home for initial AppHost topology assertions.
- `tests/Hexalith.Parties.DeployValidation.Tests/DeploymentValidationTests.cs` and `TenantsDeploymentValidationTests.cs` already validate DAPR YAML shape and safe output behavior; extend them instead of creating a parallel validator.

### Technical Constraints

- Keep package versions aligned with the repo: .NET SDK `10.0.103`, `net10.0`, Aspire `13.2.2`, CommunityToolkit Aspire DAPR `13.0.0`, Dapr.Client/Dapr.AspNetCore `1.17.7`, and Dapr.Actors `1.16.1`.
- Do not initialize or update nested submodules. The `Hexalith.EventStore`, `Hexalith.Tenants`, `Hexalith.FrontComposer`, and `Hexalith.Memories` folders are root-level submodules already present in the workspace.
- Prefer the EventStore AppHost pattern over inventing a new DAPR/AppHost abstraction. The external docs check on 2026-05-09 confirmed the CommunityToolkit Aspire DAPR pattern remains `WithDaprSidecar(...)` plus `WithReference(...)` to state store/pubsub resources, and DAPR service invocation remains app-id/method based.
- Do not remove Parties controllers, MCP wiring, or public surfaces in this story; Story 12.2 owns that cleanup.
- Do not move validators or tenant authorization ownership in this story; Story 12.3 owns that boundary.
- Do not rewrite broad controller/integration suites in this story; Story 12.4 owns the EventStore gateway test rewrite.
- Keep the AppHost publishing branches for `PUBLISH_TARGET=docker|k8s|aca`; do not remove deployment target support while recomposing resources.

### Architecture and Security Guidance

- The target topology should make EventStore the gateway and state owner: `eventstore` owns command/query ingress and EventStore actor state, `parties` is a domain actor host, `tenants` is the authority service, and `eventstore-admin`/`eventstore-admin-ui` provide stream/admin inspection.
- DAPR access control is evaluated by each receiving sidecar. Separate config files matter because one shared `Configuration` would apply the same inbound policy to every sidecar.
- Local self-hosted DAPR can remain `defaultAction: allow` as documented by EventStore, but production guidance and deploy validation should still describe deny-by-default with mTLS/Sentry.
- The Parties sidecar must allow EventStore as a caller for the operation path proven by Story 12.0. Do not grant broad caller sets or wildcard app IDs.
- The state store must continue to use `keyPrefix=none` so EventStore, Admin Server, and any projection/admin readers see the same keys. Previous story hardening called this out as a regression risk.
- Redis remains the backing infrastructure for the DAPR `statestore` and `pubsub` components. Do not describe EventStore itself as the DAPR state or pub/sub component.
- The EventStore Admin UI should communicate with Admin Server, not directly with Parties.
- If Story 12.0 proves that Parties must keep state/pubsub access for actor hosting, record that as a deliberate exception in comments and tests. Otherwise preserve the domain-service zero-infrastructure-access posture from the EventStore sample pattern.
- `parties` remains the Parties actor-host app id. Do not move actor hosting to `tenants` or make Tenants an actor service in this story.
- `eventstore-admin` and `eventstore-admin-ui` are required local AppHost resources for this story. Whether they are dev-only or part of a deployable production topology is a deferred architecture/deployment decision and must not expand this story's scope.
- Keycloak/OIDC recomposition must keep one configuration path. Do not create a second local auth model or duplicate realm/client configuration while wiring the new resources.

### Testing Guidance

- Minimum focused tests:
  - AppHost project references include EventStore service, Admin Server Host, Admin UI, and EventStore Aspire.
  - AppHost `Program.cs` contains stable resource names: `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `parties`, `tenants`.
  - AppHost resolves and passes separate DAPR config files: `accesscontrol.yaml`, `accesscontrol.eventstore-admin.yaml`, `accesscontrol.tenants.yaml`, `accesscontrol.parties.yaml`, and `resiliency.yaml`.
  - AppHost and deploy-validation assertions preserve stable `statestore` and `pubsub` component names and the `parties` actor-host app id.
  - Parties sidecar config allows caller `eventstore` and POST-only invocation.
  - EventStore/Admin/Tenants/Parties Keycloak wiring remains consistent when Keycloak is enabled.
  - Generated deployment or validation output does not include Keycloak credentials, connection strings, bearer tokens, admin passwords, or operator-supplied secrets in plain text.
- Run at least:
  - `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --filter AppHostTenantsTopologyTests`
  - `dotnet test tests/Hexalith.Parties.DeployValidation.Tests/Hexalith.Parties.DeployValidation.Tests.csproj`
  - `dotnet build Hexalith.Parties.slnx`
- If local DAPR/Aspire is available, run `dotnet aspire run --project src/Hexalith.Parties.AppHost` and capture dashboard resources. If local DAPR is unavailable, document that limitation and rely on static topology tests plus build verification.

### Out of Scope

- Removing Parties REST controllers, admin controllers, MCP tools, or public mappings.
- Migrating clients, Admin Portal, Picker, sample apps, or docs to EventStore endpoints.
- Rewriting server integration tests from Parties REST to EventStore gateway.
- Changing EventStore submodule code as part of this story. If the topology needs a platform change, document it and stop.
- Changing production deployment manifests beyond the DAPR component files directly required for this AppHost recomposition.

### References

- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07.md` - Epic 12 source, Story 12.1 ACs, pivot rationale, impacted artifacts, and out-of-scope boundaries.
- `_bmad-output/implementation-artifacts/12-0-eventstore-parties-actor-invocation-feasibility-spike.md` - required gate and exact domain/app-id conclusions for this story.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` - Epic 12 sequence and ready/backlog state.
- `src/Hexalith.Parties.AppHost/Program.cs` - current Parties/Tenants AppHost shape to replace.
- `src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj` - current AppHost project/package references.
- `src/Hexalith.Parties.AppHost/DaprComponents/*.yaml` - current single-service local DAPR component files.
- `src/Hexalith.Parties.Aspire/HexalithPartiesExtensions.cs` - current shortcut topology and shared DAPR component overload.
- `Hexalith.EventStore/src/Hexalith.EventStore.AppHost/Program.cs` - canonical EventStore plus Admin plus Tenants plus domain-service AppHost shape.
- `Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs` - `AddHexalithEventStore` helper, shared state/pubsub setup, Admin Server wiring, and DAPR HTTP port behavior.
- `Hexalith.EventStore/src/Hexalith.EventStore.AppHost/DaprComponents/*.yaml` - reference access-control, resiliency, state store, and pub/sub files.
- `tests/Hexalith.Parties.Tests/FitnessTests/AppHostTenantsTopologyTests.cs` - narrow static topology test home.
- `tests/Hexalith.Parties.DeployValidation.Tests/*.cs` - DAPR/deployment validation test patterns.

## Project Structure Notes

- Keep AppHost recomposition in `src/Hexalith.Parties.AppHost` and narrow Aspire helper changes in `src/Hexalith.Parties.Aspire` only if the AppHost cannot express the topology cleanly without them.
- Keep DAPR component files under `src/Hexalith.Parties.AppHost/DaprComponents/`; do not move them to `deploy/` in this story.
- Use existing xUnit/Shouldly fitness tests for static topology checks.
- Generated `bin/` and `obj/` outputs must stay out of commits.

## Dev Agent Record

### Agent Model Used

TBD

### Debug Log References

TBD

### Completion Notes List

TBD

### File List

TBD

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-05-09 | 0.1 | Created ready-for-dev story through BMAD pre-dev hardening automation. | Codex |
| 2026-05-09 | 0.2 | Party-mode review applied low-risk clarifications for Story 12.0 gate evidence, stable DAPR component names, actor-host boundary, Keycloak invariants, static topology tests, deploy-validation coverage, sensitive-output checks, and explicit non-goals. | Codex |

## Party-Mode Review

- Date/time: 2026-05-09T14:20:09Z
- Selected story key: 12-1-apphost-recomposition
- Command/skill invocation used: `/bmad-party-mode 12-1-apphost-recomposition; review;`
- Participating BMAD agents: Winston (System Architect), John (Product Manager), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor)
- Findings summary: reviewers agreed the story was directionally correct but needed sharper pre-dev constraints before implementation. The main risks were weak Story 12.0 gate evidence, ambiguous Redis/EventStore DAPR wording, actor-host boundary drift, unstable component names, vague Keycloak invariants, insufficient static topology assertions, deploy-validation gaps, sensitive-output leakage, and decision-budget pressure from scope that belongs to later Epic 12 stories.
- Changes applied: added acceptance criteria for Story 12.0 gate evidence, explicit non-goals, and deterministic topology/deploy-validation tests; strengthened task-level checks for DAPR access-control ownership, stable `statestore` and `pubsub` names, Keycloak resource/env-var preservation, static topology tests, sensitive-output assertions, and optional runtime proof; clarified architecture guidance for Redis-backed DAPR components, `parties` actor-host ownership, admin resource scope, and single auth configuration path.
- Findings deferred: production/deployable status of `eventstore-admin` and `eventstore-admin-ui`; long-term EventStore state/pubsub ownership boundary; exact production DAPR ACL policy model; future Tenants actor-service direction; any EventStore submodule/platform change; broader integration test rewrite and consumer migration in later Epic 12 stories.
- Final recommendation: `ready-for-dev`
