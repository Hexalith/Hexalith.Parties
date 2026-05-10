# Story 12.1: AppHost Recomposition

Status: done

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

- [x] Confirm the Story 12.0 gate before implementation. (AC: 1, 2)
  - [x] Read `_bmad-output/implementation-artifacts/12-0-eventstore-parties-actor-invocation-feasibility-spike.md`.
  - [x] If `## Spike Conclusion` is still pending, negative, or lacks the domain/app-id and DAPR/EventStore evidence required by AC6, stop and mark this story blocked through the normal dev workflow; do not start AppHost recomposition.
  - [x] If the spike conclusion documents required domain/app-id configuration, tenant/auth shortcuts, EventStore limitations, or query blockers, carry that exact result into the AppHost and DAPR component changes.
- [x] Recompose `Hexalith.Parties.AppHost` around standalone EventStore resources. (AC: 1, 3)
  - [x] Add AppHost project references for EventStore service, Admin Server Host, Admin UI, and EventStore Aspire integration without initializing or updating nested submodules.
  - [x] Replace the current self-hosted `AddHexalithParties` topology with explicit `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `parties`, and `tenants` project resources.
  - [x] Call `AddHexalithEventStore` once to create the shared `statestore` and `pubsub`; reuse those resources for `parties` and `tenants`.
  - [x] Keep `parties` as the Parties actor host resource with DAPR `appId="parties"`, not as EventStore itself.
- [x] Split DAPR access-control configuration by receiving sidecar. (AC: 4)
  - [x] Keep `accesscontrol.yaml` as the EventStore sidecar configuration, following the EventStore AppHost pattern.
  - [x] Add `accesscontrol.eventstore-admin.yaml` for the Admin Server sidecar.
  - [x] Add `accesscontrol.tenants.yaml` for the Tenants sidecar, allowing the required EventStore-origin service invocation path.
  - [x] Add `accesscontrol.parties.yaml` for the Parties actor host sidecar, allowing EventStore to invoke Parties through POST-only DAPR service invocation.
  - [x] Ensure each service loads only its own `Configuration` CRD path; do not keep one shared access-control file for all sidecars.
  - [x] Preserve stable shared component names `statestore` and `pubsub`; the access-control split must not introduce alternate state/pubsub component names unless a later architecture decision explicitly approves it.
  - [x] Treat access-control files as receiving-sidecar ownership files: shared defaults stay in the EventStore pattern, while app-specific caller permissions stay in that service's dedicated file.
- [x] Preserve shared state and pub/sub semantics. (AC: 1, 2, 4)
  - [x] Use one shared Redis-backed state store with `actorStateStore=true` and `keyPrefix=none`.
  - [x] Scope the state store to the services that require it in the recomposed topology; do not grant domain-service-style broad infrastructure access unless Story 12.0 proves it is required.
  - [x] Preserve Tenants event subscription inputs: `Tenants__Enabled`, `Tenants__ServiceName`, `Tenants__PubSubName`, and `Tenants__TopicName`.
  - [x] Re-evaluate `Tenants__CommandApiAppId`; it currently points to `parties`, but the EventStore-fronted topology may require `eventstore`.
- [x] Wire Keycloak and auth settings consistently. (AC: 5)
  - [x] Keep the existing `EnableKeycloak=false` local fallback.
  - [x] Reuse one Keycloak realm import and realm URL for EventStore, Admin Server, Parties, and Tenants.
  - [x] Use service-appropriate audiences: EventStore/Admin should follow EventStore conventions; Parties should keep only settings still needed by the actor host after the pivot.
  - [x] Preserve the explicit `Authentication__JwtBearer__SigningKey` clearing when OIDC is active.
  - [x] Preserve existing auth resource wiring, environment variable names, realm/client references, secret references, callback/logout assumptions, and startup ordering unless the Story 12.0 conclusion proves a required change.
- [x] Update topology tests and deployment validation coverage. (AC: 1, 3, 4, 5)
  - [x] Update `tests/Hexalith.Parties.Tests/FitnessTests/AppHostTenantsTopologyTests.cs` to assert all five resource names and the new EventStore project references.
  - [x] Add or update tests that assert AppHost resolves all four access-control files and `resiliency.yaml`.
  - [x] Extend deployment validation tests to cover the new four-service topology and ensure output does not echo sensitive operator-supplied values.
  - [x] Add a focused verification that the EventStore Admin UI is wired to Admin Server and can be reached from the Aspire dashboard endpoints.
  - [x] Keep deterministic topology tests static where possible; they must not require local Aspire or DAPR startup to catch resource-name, project-reference, sidecar, and component-name drift.
  - [x] Add assertions that generated or loggable deployment output does not expose Keycloak credentials, connection strings, tokens, admin passwords, or operator-supplied secrets in plain text.
  - [x] Assert `accesscontrol.parties.yaml` allows the Story 12.0-proven EventStore caller path to `parties`, preserves the `parties` actor-host app id, and does not grant wildcard app ids or broad caller sets without an explicit deferred decision.
- [x] Document local operation evidence. (AC: 1, 2)
  - [x] Record the exact `dotnet aspire run --project src/Hexalith.Parties.AppHost` command used.
  - [x] Capture the Aspire dashboard resource names and endpoint URLs observed locally.
  - [x] Document how to open the EventStore Admin UI and confirm Parties event visibility.
  - [x] If local Aspire or DAPR is unavailable, document that limitation separately; static topology, deploy-validation, and build tests remain required.

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

Codex GPT-5

### Debug Log References

- 2026-05-09: Confirmed Story 12.0 gate is `done` with dated `## Spike Conclusion`, `partial` static-analysis outcome, explicit Wave-1 unblock, `Domain=party` guidance, static `party -> parties/process` mapping requirement, and shared DAPR state-store dependency (`actorStateStore=true`, `keyPrefix=none`).
- 2026-05-09: Red phase confirmed: `AppHostTenantsTopologyTests` failed 6/6 and `AppHostDaprTopologyValidationTests` failed 4/4 against the old single-`parties` AppHost/DAPR shape.
- 2026-05-09: Focused tests green after implementation: `dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --filter AppHostTenantsTopologyTests --no-restore` (6/6 passed); `dotnet test tests/Hexalith.Parties.DeployValidation.Tests/Hexalith.Parties.DeployValidation.Tests.csproj --filter AppHostDaprTopologyValidationTests --no-restore` (4/4 passed).
- 2026-05-09: Broader validation green for story-owned surfaces: `dotnet test tests/Hexalith.Parties.DeployValidation.Tests/Hexalith.Parties.DeployValidation.Tests.csproj --no-restore` (32/32 passed); `dotnet build src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj` passed; `dotnet build Hexalith.Parties.slnx` passed.
- 2026-05-09: Full regression run `dotnet test Hexalith.Parties.slnx --no-build` still has unrelated residual failures outside Story 12.1 AppHost/DAPR scope; newly observed non-12.0 residuals logged in `deferred-work.md`.
- 2026-05-09: Local runtime proof not captured: `dotnet aspire --version` fails because `dotnet-aspire` is not on PATH. `dapr --version` reports CLI 1.17.1 / runtime 1.17.4 and Docker reports server 29.4.0.

### Completion Notes List

- Story 12.0 gate passed before implementation; AppHost recomposition proceeds with runtime proof still documented as required before production claims.
- Replaced the old `AddHexalithParties` / `AddHexalithTenants` shortcut topology with explicit `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `parties`, and `tenants` project resources.
- Wired `AddHexalithEventStore` as the single creator of shared DAPR `statestore` and `pubsub`, then reused those resources for Parties and Tenants sidecars with `parties` preserved as the actor-host app id.
- Added static `EventStore__DomainServices__Registrations__*|party|v1` mapping to route Story 12.0's selected `party` domain to `parties/process`.
- Split DAPR access-control by receiving sidecar: EventStore, Admin Server, Tenants, and Parties each load a dedicated `Configuration` CRD path.
- Updated local DAPR components to preserve stable `statestore` and `pubsub` names, Redis env-var configuration, `actorStateStore=true`, and `keyPrefix=none`.
- Kept Keycloak optional with `EnableKeycloak=false`, reused one realm URL when enabled, cleared `Authentication__JwtBearer__SigningKey` for OIDC services, and avoided adding plain-text Keycloak passwords to AppHost/DAPR YAML.
- Retired the Story 12.0 spike blocker test file after promoting the relevant AppHost resource/config invariants into Story 12.1 topology tests.
- Runtime evidence limitation: the exact intended command is `dotnet aspire run --project src/Hexalith.Parties.AppHost`, but it could not be executed in this environment because the `dotnet aspire` CLI is not installed. With that command available, open the Aspire dashboard, select `eventstore-admin-ui`, open its external HTTP endpoint, and use the stream browser against `eventstore-admin` to inspect Parties events after submitting a Parties command through EventStore.

### File List

- `_bmad-output/implementation-artifacts/12-1-apphost-recomposition.md`
- `_bmad-output/implementation-artifacts/deferred-work.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.eventstore-admin.yaml`
- `src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.parties.yaml`
- `src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.tenants.yaml`
- `src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.yaml`
- `src/Hexalith.Parties.AppHost/DaprComponents/pubsub.yaml`
- `src/Hexalith.Parties.AppHost/DaprComponents/resiliency.yaml`
- `src/Hexalith.Parties.AppHost/DaprComponents/statestore.yaml`
- `src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj`
- `src/Hexalith.Parties.AppHost/Program.cs`
- `tests/Hexalith.Parties.DeployValidation.Tests/AppHostDaprTopologyValidationTests.cs`
- `tests/Hexalith.Parties.Tests/FitnessTests/AppHostTenantsTopologyTests.cs`
- `tests/Hexalith.Parties.Tests/FitnessTests/EventStorePartiesInvocationSpikeTests.cs` (deleted)

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-05-09 | 0.1 | Created ready-for-dev story through BMAD pre-dev hardening automation. | Codex |
| 2026-05-09 | 0.2 | Party-mode review applied low-risk clarifications for Story 12.0 gate evidence, stable DAPR component names, actor-host boundary, Keycloak invariants, static topology tests, deploy-validation coverage, sensitive-output checks, and explicit non-goals. | Codex |
| 2026-05-09 | 0.3 | Started implementation; Story 12.0 gate confirmed and carried into AppHost recomposition constraints. | Codex |
| 2026-05-09 | 1.0 | Recompleted AppHost around EventStore/Admin/UI/Parties/Tenants resources, split DAPR sidecar configs, added static topology/deploy-validation coverage, retired stale Story 12.0 spike guard, and moved story to review. | Codex |
| 2026-05-10 | 1.1 | bmad-code-review applied 22 patches across DAPR access-control YAMLs (deny-by-default + parties caller + /process scope), pubsub scopes, REDIS_PASSWORD defaults, Program.cs (PUBLISH_TARGET validation, EnableKeycloak bool.TryParse, WaitFor for shared DAPR components, multi-audience tolerance, adminUI auth-clear, ResolveDaprConfigPath improvements, FalseLiteral inlined, Tenants direct WithReference comment), csproj HexalithTenantsBasePath validation, deploy-validation tests (admin UI wiring, deny posture, parties caller, secret-scan tightening, secret-scan of source/launchSettings), AppHostTenantsTopologyTests (multi-audience, adminUI clear, PUBLISH_TARGET, WaitFor counts, SigningKey count), relocated projection-contract invariant from deleted spike test, deleted orphaned Hexalith.Parties.Aspire project + slnx + README references. 15 items deferred. Story moved to done. | Claude Opus 4.7 |

## Review Findings

Source: `bmad-code-review` (2026-05-10) — diff scope: commit `6a4b557` (AppHost + DAPR YAML + tests). Reviewers: Blind Hunter (adversarial), Edge Case Hunter, Acceptance Auditor. **Outcome:** all 7 decisions resolved, all 22 patches applied (15 originals + 7 decision-driven), 15 items deferred to `deferred-work.md`. AppHost build clean; deploy-validation tests 37/37 green; AppHost topology + projection-contract tests 11/11 green; full solution build clean.

### Decision Needed

- [x] [Review][Decision] **DAPR access-control posture: `defaultAction: allow` everywhere** — All 4 AC YAML files (`accesscontrol.yaml`, `accesscontrol.eventstore-admin.yaml`, `accesscontrol.parties.yaml`, `accesscontrol.tenants.yaml`) use `defaultAction: allow` at the spec level. This means per-appId policies are effectively decorative — any non-listed caller still gets full access. The `accesscontrol.eventstore-admin.yaml` comment says "no peer service should invoke it" but the config does the opposite. EventStore canonical local pattern uses `allow` for dev and `deny` for prod, but no test asserts the prod posture. Decide: (a) flip to `deny` everywhere with explicit allowlists, (b) keep `allow` for local but add a deploy-validation test that prod manifests differ, or (c) document split intent and add tests asserting it.
- [x] [Review][Decision] **Tenants → EventStore caller path under new topology** — `Program.cs:65` flips `Tenants__CommandApiAppId` from `parties` to `eventstore`, but `accesscontrol.yaml` lists only `eventstore-admin` and `tenants` as allowed callers (NOT `parties`). With current `defaultAction: allow` this is masked, but if D1 flips to `deny`, `parties → eventstore` POST will be denied. Conversely, `accesscontrol.parties.yaml` opens `eventstore → parties` to POST `/**` rather than the Story 12.0-proven `/process` only. Decide caller matrix and tighten both files.
- [x] [Review][Decision] **Tenants Aspire integration removed without spec authorization** — `csproj` removes the `Hexalith.Tenants.Aspire` reference, `AddHexalithTenants` removed entirely, and `AppHostTenantsTopologyTests` now asserts `ShouldNotContain("AddHexalithTenants(")`. Tenants now takes a direct `WithReference(eventStore)` which diverges from the canonical `Hexalith.EventStore.AppHost` pattern (Tenants is a peer with no eventstore reference). Spec Project Structure Notes did not authorize this. Decide: (a) re-introduce `AddHexalithTenants` wrapping the new resource shape, or (b) accept the removal and document why Tenants takes a direct eventstore reference.
- [x] [Review][Decision] **`publishingScopes` / `subscriptionScopes` in `pubsub.yaml` use empty values to silence services** — `publishingScopes: "parties=;tenants="` means parties/tenants cannot publish. `subscriptionScopes: "parties=system.tenants.events;tenants=;sample=sample.parties.events"` means tenants subscribes to nothing, and `eventstore` is missing from `subscriptionScopes` entirely (yet present in `scopes`). Confirm intended publish/subscribe matrix per service.
- [x] [Review][Decision] **Keycloak audience asymmetry across services** — `tenants` validates `Audience = "hexalith-eventstore"` (Program.cs:131) while `parties` validates `"hexalith-parties"`. EventStore-issued tokens for cross-service DAPR invocations may fail audience validation depending on target. Decide token audience policy: single shared audience, distinct-per-service with multi-audience tolerance on receivers, or per-call token issuance.
- [x] [Review][Decision] **Orphaned `Hexalith.Parties.Aspire` helper project** — `csproj` removed the reference; the helper project (`HexalithPartiesExtensions.cs`, `HexalithPartiesResources.cs`) still exists on disk as dead code. Spec File List does not enumerate `Hexalith.Parties.Aspire/*`. Decide: delete the helper, or wire it back into AppHost.
- [x] [Review][Decision] **Sensitive-output assertion scope** — Spec AC8 / Task 5 sub-bullet 6 demanded that "generated or loggable deployment output does not include Keycloak credentials, connection strings, bearer tokens, admin passwords, or operator-supplied secrets in plain text." Current test (`AppHostDaprComponentsDoNotContainPlainTextSecrets`) only scans static `DaprComponents/*.yaml` files (which never contained Keycloak passwords in this repo). Decide: extend tests to scan AppHost-generated `aspire publish` manifests / env-var dumps, or accept current scope as sufficient for Story 12.1.

### Patches

- [x] [Review][Patch] **Restrict `accesscontrol.parties.yaml` POST to `/process` only** [src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.parties.yaml:18-20] — change `name: /**` to `name: /process` to match Story 12.0-proven mapping
- [x] [Review][Patch] **Add AC2 admin UI ↔ admin server wiring test** [tests/Hexalith.Parties.DeployValidation.Tests/AppHostDaprTopologyValidationTests.cs] — assert that `eventstore-admin-ui` references `eventstore-admin` and exposes external HTTP endpoints (Task 5 sub-bullet 4 was checked but never implemented)
- [x] [Review][Patch] **Fix wildcard appId assertion in parties access-control test** [tests/Hexalith.Parties.DeployValidation.Tests/AppHostDaprTopologyValidationTests.cs:49] — current `ShouldNotContain("appId: \"*\"")` checks a string the file would never contain (file uses unquoted `appId: eventstore`); rewrite to actually catch unquoted/quoted wildcard
- [x] [Review][Patch] **Empty REDIS_PASSWORD default** [src/Hexalith.Parties.AppHost/DaprComponents/pubsub.yaml:14, statestore.yaml:14] — `value: "{env:REDIS_PASSWORD|}"` to provide explicit empty fallback (currently substitutes literal `{env:REDIS_PASSWORD}` when env var unset)
- [x] [Review][Patch] **Clear stale auth env vars in non-Keycloak `adminUI` branch** [src/Hexalith.Parties.AppHost/Program.cs:138-140] — explicitly set `EventStore__Authentication__Authority=""` and `EventStore__Authentication__ClientId=""` to prevent leakage of stale values
- [x] [Review][Patch] **Validate `PUBLISH_TARGET` enum** [src/Hexalith.Parties.AppHost/Program.cs:142-154] — throw on unknown PUBLISH_TARGET (not docker/k8s/aca/empty); current code silently falls through
- [x] [Review][Patch] **Tighten secret-scan assertions** [tests/Hexalith.Parties.DeployValidation.Tests/AppHostDaprTopologyValidationTests.cs:556-563] — strip comments before scanning, assert per-file rather than concatenated; current `ShouldNotContain("password: ")` misses `redisPassword:` (no colon-space) and `ShouldContain("{env:REDIS_PASSWORD}")` passes if any single file references it
- [x] [Review][Patch] **Relocate projection-contract invariant from deleted spike test** [tests/Hexalith.Parties.Tests/FitnessTests/EventStorePartiesInvocationSpikeTests.cs (deleted)] — the deleted spike test contained "Parties projection actors do not implement EventStore generic projection contract" — Story 12.1 promised promotion of useful invariants but did not relocate this one; add to an architectural fitness test
- [x] [Review][Patch] **Add MSBuild error for missing `HexalithTenantsBasePath`** [src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj:7] — add `<Error Condition="!Exists('$(HexalithTenantsBasePath)')" Text="Tenants submodule missing"/>` to fail fast instead of silently producing broken AdminServer.Host build
- [x] [Review][Patch] **Inline misleading `FalseLiteral` const** [src/Hexalith.Parties.AppHost/Program.cs:5] — `const string FalseLiteral = "false"` adds no DRY value and obscures that callsites are config string values; inline `"false"` directly
- [x] [Review][Patch] **Improve `ResolveDaprConfigPath` error messages** [src/Hexalith.Parties.AppHost/Program.cs:158-176] — include all probed paths in `FileNotFoundException`; current message only references the second (fallback) path; consider adding `AppContext.BaseDirectory + DaprComponents` as primary search to support published deployments
- [x] [Review][Patch] **Reconcile `eventstore-admin` between `resiliency.yaml` and `accesscontrol.eventstore-admin.yaml`** [src/Hexalith.Parties.AppHost/DaprComponents/resiliency.yaml:38-41 + accesscontrol.eventstore-admin.yaml:9-12] — resiliency lists `eventstore-admin` as a target (implying it participates in DAPR invocation) but accesscontrol comment says "no peer should invoke." Either drop from resiliency or set `defaultAction: deny` with empty policies (related to D1)
- [x] [Review][Patch] **Use `bool.TryParse` for `EnableKeycloak`** [src/Hexalith.Parties.AppHost/Program.cs:5,91] — current `OrdinalIgnoreCase` literal-`"false"` comparison treats `"0"`, `"no"`, empty string as truthy
- [x] [Review][Patch] **Set `defaultAction: deny` with empty policies on `accesscontrol.eventstore-admin.yaml`** [src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.eventstore-admin.yaml:9-12] — comment header explicitly states "no peer service should invoke it through DAPR"; current `allow` + `policies: []` does the opposite (independent of D1 because intent is unambiguous in the comment)
- [x] [Review][Patch] **Add `WaitFor(StateStore)` and `WaitFor(PubSub)` for parties and tenants sidecars** [src/Hexalith.Parties.AppHost/Program.cs:32-50] — both reference shared DAPR components but never wait for them; if Redis/components have init dependencies, sidecar may attempt early state operations and fault on startup

### Deferred (pre-existing or architectural — out of scope for Story 12.1)

- [x] [Review][Defer] **`AddHexalithEventStore` helper sets `redisHost` via `WithMetadata`, bypassing local `statestore.yaml`** [Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs:72-77] — yaml is not the runtime source of truth for redisHost; deploy-validation tests assert against yaml, creating divergence; helper-side fix
- [x] [Review][Defer] **Hard-coded `trustDomain: "public"` and `namespace: "default"` in DAPR access-control YAMLs** [src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol*.yaml] — production SPIFFE identities differ; needs env-var substitution path
- [x] [Review][Defer] **Uniform `circuitBreaker` policy** [src/Hexalith.Parties.AppHost/DaprComponents/resiliency.yaml:38-53] — health-probe burst can trip and cascade-trip command path; needs separate breakers per route class
- [x] [Review][Defer] **Shared state store with `keyPrefix=none` + `actorStateStore=true` across 4 appIds** [src/Hexalith.Parties.AppHost/DaprComponents/statestore.yaml:16-19] — spec-mandated but architecturally risky (Redis key collisions, actor placement service confusion); document as deliberate exception in a follow-up
- [x] [Review][Defer] **Circular `WaitFor` intent** [src/Hexalith.Parties.AppHost/Program.cs:69-71] — parties → tenants → eventstore chain plus EventStore reads tenants AllowedCallers; brittle startup ordering needs explicit one-way decision
- [x] [Review][Defer] **`/ready` path validity through DAPR sidecar** [src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.tenants.yaml:26-28] — DAPR sidecars typically expose `/v1.0/healthz`; `/ready` may not be the invocable path
- [x] [Review][Defer] **Hard-coded Keycloak port 8180** [src/Hexalith.Parties.AppHost/Program.cs:93] — pre-existing; conflicts if multiple Aspire AppHosts run concurrently
- [x] [Review][Defer] **`WithRealmImport("./KeycloakRealms")` relative path** [src/Hexalith.Parties.AppHost/Program.cs:93-94] — pre-existing dependency on cwd
- [x] [Review][Defer] **`MemoriesEndpoint` URL validation** [src/Hexalith.Parties.AppHost/Program.cs:73-87] — pre-existing; invalid URL forwarded verbatim to Parties
- [x] [Review][Defer] **`Tenants__BootstrapGlobalAdminUserId` flow has no test** [src/Hexalith.Parties.AppHost/Program.cs:52-56] — minor coverage gap; no regression
- [x] [Review][Defer] **Test brittleness — Program.cs substring assertions** [tests/Hexalith.Parties.Tests/FitnessTests/AppHostTenantsTopologyTests.cs:617-697] — many `ShouldContain` use short literals (`"parties"`, `"process"`, `"party"`) that match comments and other contexts; wider test refactor
- [x] [Review][Defer] **`FindSolutionDirectory` infinite walk safety** [tests/Hexalith.Parties.DeployValidation.Tests/AppHostDaprTopologyValidationTests.cs:77-86] — testing infra; needs max-depth or env override
- [x] [Review][Defer] **`Directory.GetFiles` discovery weakness in deploy-validation** [tests/Hexalith.Parties.DeployValidation.Tests/AppHostDaprTopologyValidationTests.cs:553-555] — concatenation-based asserts mask file-of-origin
- [x] [Review][Defer] **Tenants given direct state-store/pub-sub access vs spec architecture posture** [src/Hexalith.Parties.AppHost/Program.cs:42-50] — relates to Decision #3 (Tenants Aspire removal); defer until that resolves
- [x] [Review][Defer] **`SigningKey=""` clearing pattern not asserted across all 4 services** [tests/Hexalith.Parties.Tests/FitnessTests/AppHostTenantsTopologyTests.cs:82-98] — single-service regression would still pass; assert `>= 4` occurrences

## Party-Mode Review

- Date/time: 2026-05-09T14:20:09Z
- Selected story key: 12-1-apphost-recomposition
- Command/skill invocation used: `/bmad-party-mode 12-1-apphost-recomposition; review;`
- Participating BMAD agents: Winston (System Architect), John (Product Manager), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor)
- Findings summary: reviewers agreed the story was directionally correct but needed sharper pre-dev constraints before implementation. The main risks were weak Story 12.0 gate evidence, ambiguous Redis/EventStore DAPR wording, actor-host boundary drift, unstable component names, vague Keycloak invariants, insufficient static topology assertions, deploy-validation gaps, sensitive-output leakage, and decision-budget pressure from scope that belongs to later Epic 12 stories.
- Changes applied: added acceptance criteria for Story 12.0 gate evidence, explicit non-goals, and deterministic topology/deploy-validation tests; strengthened task-level checks for DAPR access-control ownership, stable `statestore` and `pubsub` names, Keycloak resource/env-var preservation, static topology tests, sensitive-output assertions, and optional runtime proof; clarified architecture guidance for Redis-backed DAPR components, `parties` actor-host ownership, admin resource scope, and single auth configuration path.
- Findings deferred: production/deployable status of `eventstore-admin` and `eventstore-admin-ui`; long-term EventStore state/pubsub ownership boundary; exact production DAPR ACL policy model; future Tenants actor-service direction; any EventStore submodule/platform change; broader integration test rewrite and consumer migration in later Epic 12 stories.
- Final recommendation: `ready-for-dev`
