# Story 11.4: Tenants Integration Tests, Deployment Validation, and Documentation

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer and operator,
I want tests and docs proving Parties uses Hexalith.Tenants correctly,
so that tenant integration is reliable in CI and local development.

## Acceptance Criteria

1. Given fast tenant authorization test scenarios, when tests are written, then they use public `Hexalith.Tenants.Testing` helpers or the Story 11.2 tenant-access projection/service seam for tenant status, membership, and role state.
2. Given integration tests for Tenants-backed access, when the test suite runs, then it covers active tenant allowed, valid JWT tenant claim but no active Tenants membership/role denied, disabled tenant denied, removed user denied, insufficient role denied, and cross-tenant projection isolation.
3. Given deployment validation tooling, when validation runs, then it checks Tenants subscription/configuration and reports actionable errors when integration is missing or unhealthy, with each error identifying the failed dependency or setting, impact, remediation target, and no secrets, tokens, claims, membership records, or PII.
4. Given getting-started documentation, when a developer follows the guide, then tenants are provisioned through Hexalith.Tenants and Parties is described only as a consumer of Tenants state, not a tenant lifecycle, membership, role, global administrator, or configuration authority.
5. Given tenant troubleshooting documentation, when reviewed, then it distinguishes missing JWT claims from missing Tenants membership or role, disabled tenants, and removed users with observable symptoms, likely cause, fix owner, and expected REST/MCP behavior.

## Tasks / Subtasks

- [ ] Add Tenants testing helper references only where they reduce test setup (AC: 1)
  - [ ] Add a project reference to `Hexalith.Tenants.Testing` in the affected test project(s), most likely `tests/Hexalith.Parties.CommandApi.Tests` for sidecar-free authorization tests and `tests/Hexalith.Parties.IntegrationTests` only if full-topology tests need Tenants seed helpers.
  - [ ] Reuse `TenantTestHelpers`, `InMemoryTenantService`, and/or `InMemoryTenantProjection` for tenant lifecycle, membership, role, and projection setup instead of hand-rolling tenant aggregate behavior.
  - [ ] Use public Tenants testing helpers/builders only; do not assert on private Tenants event shapes or duplicate Tenants lifecycle, membership, role, or disabled/removed-user policy in Parties-local fakes.
  - [ ] Record a short implementation note naming which Tenants testing helpers are used and where thin Parties-local adapters remain intentional for request/auth composition.
  - [ ] Do not add `Hexalith.Tenants.Testing` to production projects.
  - [ ] Keep `src/Hexalith.Parties.Contracts` free of Tenants dependencies.

- [ ] Strengthen fast Tenants-backed authorization tests (AC: 1, 2, 5)
  - [ ] Extend or add tests in `tests/Hexalith.Parties.CommandApi.Tests/Authorization` for the access service and denial translation created by Stories 11.2 and 11.3.
  - [ ] Cover active tenant allowed for the required role, disabled tenant denied, removed user denied, insufficient role denied, missing user id denied, unknown tenant denied, and missing/stale local projection state denied.
  - [ ] Cover valid JWT tenant context with no active Tenants membership or role as denied; JWT claims identify requested context only and must never be the authorization source of truth.
  - [ ] Cover the role matrix explicitly: `TenantReader` can read/search only, `TenantContributor` can read/write but not admin, and `TenantOwner` can read/write/admin.
  - [ ] Assert denial reason codes remain stable for troubleshooting, for example `missing-tenant`, `missing-user`, `unknown-tenant`, `tenant-disabled`, `not-member`, `insufficient-role`, and `tenant-state-stale` when modeled.
  - [ ] Keep these tests deterministic and sidecar-free; instantiate in-process Tenants testing helpers, fakes, or projection/service seams directly with no sidecars, containers, network calls, EventStore, DAPR, or external identity provider.

- [ ] Add integration coverage for REST, MCP, and projection isolation (AC: 2)
  - [ ] Add or extend WebApplicationFactory/CommandApi tests that prove authorized active tenant access succeeds while disabled tenant, removed user, and insufficient role requests fail before projection reads or command routing.
  - [ ] Cover REST read/query, REST write, admin REST, MCP read/search, and MCP write paths through representative endpoints/tools rather than duplicating every existing endpoint assertion.
  - [ ] Preserve existing tenant isolation tests that verify tenant-scoped actor ids and query isolation; add Tenants-backed access scenarios without weakening cross-tenant non-enumeration behavior.
  - [ ] Define projection isolation through externally observable behavior: seed or create similar data for at least two tenants, then assert tenant A cannot list, read, search, or MCP-resolve tenant B data and cannot address tenant B records by direct id/path.
  - [ ] Keep these tests representative proof of the Story 11.1-11.3 integration contract; do not reopen topology, event handling, endpoint authorization design, command payload shape, MCP tool schemas, EventStore actor identity, or projection key strategy.
  - [ ] If full Aspire topology tests are used, seed tenants and memberships through Hexalith.Tenants command/test helpers or documented Tenants APIs, not Parties-local tenant setup.
  - [ ] Skip or quarantine Tier 3 tests gracefully when Docker, DAPR, or Aspire infrastructure is unavailable, matching the existing `PartiesAspireTopologyFixture` pattern.

- [ ] Extend deployment validation for Tenants integration (AC: 3)
  - [ ] Update `deploy/validate-deployment.ps1` so it detects Tenants event subscription/configuration requirements in addition to existing Parties DAPR checks.
  - [ ] Validate that the configured pub/sub component scopes include the app id that hosts the Parties Tenants event subscription, normally `commandapi`, and that `subscriptionScopes` explicitly allow it to subscribe to `system.tenants.events` when production scoping is enabled.
  - [ ] Validate that Tenants subscription configuration is present when Tenants integration is enabled, either through a declarative subscription file or documented programmatic subscription expectations from `MapTenantEventSubscription()`.
  - [ ] Report actionable failures for missing `Tenants` configuration, wrong pub/sub name/topic, missing `commandapi` subscription permission, missing dead-letter/resiliency coverage, or unhealthy/missing Tenants integration configuration.
  - [ ] Emit distinct validation categories for missing Tenants subscription, missing or malformed Tenants configuration, invalid or unreachable Tenants dependency where detectable, and Parties configuration that bypasses Tenants authorization.
  - [ ] Ensure console output remains useful without color by using stable check names and clear `PASS`, `WARN`, and `FAIL` prefixes suitable for CI logs and screen readers.
  - [ ] Preserve local development warnings where Redis/self-hosted DAPR intentionally omits production scoping metadata.

- [ ] Add deployment-validation tests for Tenants requirements (AC: 3)
  - [ ] Extend `tests/Hexalith.Parties.DeployValidation.Tests/DeploymentValidationTests.cs` with known-good and known-bad Tenants config fixtures.
  - [ ] Assert missing Tenants subscription/configuration fails with a specific message and recommendation.
  - [ ] Assert malformed or empty Tenants config values, invalid/unreachable dependency signals where detectable, and missing authorization-required subscription scope each produce distinct, secret-safe failures.
  - [ ] Assert missing `commandapi` access to `system.tenants.events` fails when production `subscriptionScopes` are present.
  - [ ] Assert local development config can pass with warnings when the Tenants integration is explicitly documented as local-only or disabled.
  - [ ] Keep JSON output valid and include Tenants validation checks in the structured `checks` array.

- [ ] Update local getting-started and tenant provisioning docs (AC: 4, 5)
  - [ ] Update `docs/getting-started.md` so the local walkthrough provisions or references an active tenant through Hexalith.Tenants before calling Parties REST or MCP.
  - [ ] Present the setup order explicitly: provision tenant in Hexalith.Tenants, assign user membership/role, configure Parties Tenants subscription, run deployment validation, then run a minimal authorized Parties request.
  - [ ] Distinguish the JWT tenant claim from Tenants membership: the claim selects requested tenant context, while active Tenants membership/role authorizes the operation.
  - [ ] Document the minimum local role needed for the walkthrough, normally `TenantContributor` for create/update flows and `TenantReader` for read/search-only flows.
  - [ ] Keep tenant lifecycle and membership management instructions pointed at Hexalith.Tenants. Do not add Parties-local tenant management screens, commands, or seed state as the authority.
  - [ ] State that tenant ids are not command payload fields or MCP tool parameters; tenant context comes from authenticated request/session context and Tenants membership.
  - [ ] Update README links or summary text if the high-level onboarding promise changes because Tenants provisioning is now required.

- [ ] Update deployment and troubleshooting documentation (AC: 3, 5)
  - [ ] Update `docs/deployment-guide.md` and `docs/deployment-security-checklist.md` with Tenants pub/sub topic `system.tenants.events`, required app ids, subscription permissions, validation command examples, and operator remediation steps.
  - [ ] Add troubleshooting guidance that separates `401` missing/invalid JWT issues from `403` Tenants projection or membership/role denial.
  - [ ] Include a troubleshooting decision table for missing/invalid tenant JWT claim, valid claim with no active Tenants membership, insufficient Tenants role, disabled tenant, and removed user, with symptom, likely cause, fix owner, and remediation.
  - [ ] Document eventual-consistency behavior from Story 11.2: local Tenants projection lag can temporarily affect access decisions, and missing/unknown local state fails closed.
  - [ ] Document how to inspect health/readiness and validation output when Tenants subscription/configuration is missing or unhealthy.
  - [ ] Avoid exposing full claim sets, tokens, membership dictionaries, or personal party data in examples or logs.

- [ ] Validate affected build and tests
  - [ ] Run `dotnet test tests/Hexalith.Parties.CommandApi.Tests/Hexalith.Parties.CommandApi.Tests.csproj --configuration Release`.
  - [ ] Run `dotnet test tests/Hexalith.Parties.DeployValidation.Tests/Hexalith.Parties.DeployValidation.Tests.csproj --configuration Release`.
  - [ ] Run `dotnet test tests/Hexalith.Parties.IntegrationTests/Hexalith.Parties.IntegrationTests.csproj --configuration Release` when Docker/Aspire prerequisites are available, or record the infrastructure skip reason.
  - [ ] Run `dotnet build Hexalith.Parties.slnx --configuration Release`.

## Dev Notes

### Epic Context

Epic 11 makes `Hexalith.Tenants` the source of truth for tenant lifecycle, membership, roles, and configuration while Parties continues to own party aggregates, projections, REST/MCP surfaces, and tenant-scoped party data isolation. Stories 11.1, 11.2, and 11.3 establish topology, local projection/access service, and REST/MCP enforcement. This story hardens the integration through tests, deployment validation, and operator/developer documentation. [Source: _bmad-output/planning-artifacts/epics.md#Epic 11: Hexalith.Tenants Integration for Parties]

### Approved Change Context

The approved May 2, 2026 sprint change proposal says local development should provision tenant state through Hexalith.Tenants, Parties should use Tenants testing helpers where useful, and Parties must not duplicate tenant lifecycle, membership, role, or configuration ownership. [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-05-02.md#Implementation Handoff]

The same proposal keeps tenant-scoped aggregate identity, actor keys, projections, pub/sub, REST, MCP, and log safety as Parties/EventStore enforcement responsibilities. Tests and docs must prove that Tenants authority data is consumed without changing those storage and routing boundaries. [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-05-02.md#Architecture - Cross-Cutting Multi-Tenancy]

### Architecture Constraints

- Target framework is `net10.0`; warnings are treated as errors. [Source: Directory.Build.props]
- Current Parties package versions are centrally managed in `Directory.Packages.props`; do not opportunistically upgrade DAPR, Aspire, MCP, ASP.NET, or Tenants package versions as part of this story.
- The architecture defines a three-tier testing strategy: pure domain/unit tests, EventStore integration tests, and full-stack Parties integration tests. Use fast sidecar-free tests for access logic and reserve full Aspire/DAPR tests for end-to-end topology confidence. [Source: _bmad-output/planning-artifacts/architecture.md#Requirements Overview]
- Architectural fitness rules remain in force: Contracts has no runtime dependencies beyond netstandard2.1, MCP must not reference domain event types, projection handlers must avoid DAPR references, and test tiers must not collapse into infrastructure-heavy unit tests. [Source: _bmad-output/planning-artifacts/architecture.md#Architectural Fitness Tests]
- Multi-tenancy and security requirements include fail-closed JWT/tenant handling, zero cross-tenant leakage, safe logs, and deployment validation. [Source: _bmad-output/planning-artifacts/architecture.md#Requirements Coverage Validation]

### Current Code State and Files Likely Touched

`tests/Hexalith.Parties.CommandApi.Tests`

- Current state: contains controller ProblemDetails tests, MCP tool tests, health-check tests, search tests, and architectural fitness tests.
- Story change: add focused sidecar-free tests for Tenants-backed access decisions and REST/MCP denial translation, reusing `Hexalith.Tenants.Testing` where it shortens setup.
- Preserve: existing tenant isolation and non-enumeration coverage, MCP fitness rules, and fast Tier 1 behavior.

`tests/Hexalith.Parties.IntegrationTests`

- Current state: contains full-topology health tests using `DistributedApplicationTestingBuilder`, event publishing verification, tenant isolation tests, and GDPR/security E2E tests. The topology fixture disables Keycloak and skips gracefully when Aspire/DAPR infrastructure is unavailable.
- Story change: add full-topology Tenants integration coverage only where it adds confidence beyond CommandApi tests, such as seeding a Tenants-backed active tenant and proving disabled/removed/insufficient-role access fails through the real host.
- Preserve: infrastructure skip behavior and existing tenant-scoped event topic assertions.

`deploy/validate-deployment.ps1`

- Current state: validates DAPR access control, state store, pub/sub scopes, subscriptions, resiliency, and secret-store advisory checks. It supports PowerShell-style and GNU-style arguments plus JSON output for CI.
- Story change: add Tenants integration validation without replacing the existing minimal YAML parser. Keep output categories/checks stable and include Tenants checks in JSON.
- Preserve: exit codes `0`, `1`, and `2`, local-development warnings, and existing Parties pub/sub/security checks.

`tests/Hexalith.Parties.DeployValidation.Tests/DeploymentValidationTests.cs`

- Current state: builds temporary good/bad DAPR configs, invokes the PowerShell validation script, and asserts console/JSON output.
- Story change: add fixtures for Tenants subscription/configuration success and failure cases.
- Preserve: valid JSON output assertions and platform-tolerant PowerShell discovery.

`docs/getting-started.md`, `README.md`, `docs/deployment-guide.md`, `docs/deployment-security-checklist.md`

- Current state: docs explain local Aspire startup, Keycloak/JWT basics, multi-tenant party topics, deployment validation, and troubleshooting for DAPR, auth, cross-tenant access, and projection delay. They still largely describe tenant isolation as JWT/EventStore/Parties behavior.
- Story change: document that Tenants is now the tenant authority, local setup must provision tenants and membership through Tenants, deployment validation must include Tenants pub/sub/configuration, and troubleshooting must distinguish missing JWT tenant claims from Tenants membership/role denials.
- Preserve: existing GDPR warning, DAPR deployment guidance, party event subscription docs, and no-PII examples.

### Hexalith.Tenants APIs to Reuse

- `Hexalith.Tenants.Testing.Helpers.TenantTestHelpers` can bootstrap tenant test scenarios through the Tenants aggregate/test service instead of duplicating tenant lifecycle logic. [Source: Hexalith.Tenants/src/Hexalith.Tenants.Testing/Helpers/TenantTestHelpers.cs]
- `Hexalith.Tenants.Testing.Fakes.InMemoryTenantService` delegates to the production Tenants aggregate Handle/Apply methods and stores event history for fast, infrastructure-free tests. [Source: Hexalith.Tenants/src/Hexalith.Tenants.Testing/Fakes/InMemoryTenantService.cs]
- `Hexalith.Tenants.Testing.Projections.InMemoryTenantProjection` applies Tenants events into read models without DAPR state, useful for projection and conformance tests. [Source: Hexalith.Tenants/src/Hexalith.Tenants.Testing/Projections/InMemoryTenantProjection.cs]
- `TenantLocalState` records tenant id, status, members mapped by user id to `TenantRole`, and configuration. [Source: Hexalith.Tenants/src/Hexalith.Tenants.Client/Projections/TenantLocalState.cs]
- Tenants roles are exactly `TenantOwner`, `TenantContributor`, and `TenantReader`; do not invent unqualified `Owner`, `Contributor`, or `Reader` enum values in Parties tests or docs. [Source: Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Enums/TenantRole.cs]
- Tenants client defaults from previous stories remain `PubSubName = "pubsub"`, `TopicName = "system.tenants.events"`, and `CommandApiAppId = "commandapi"` unless implementation deliberately changes configuration. [Source: _bmad-output/implementation-artifacts/11-2-tenants-event-consumption-and-local-access-projection.md#Hexalith.Tenants APIs to Reuse]

### Implementation Guardrails

- Do not implement tenant lifecycle, membership, role, global administrator, or configuration management in Parties.
- Do not create Parties-local seed data that becomes the tenant authority. Local setup must use Hexalith.Tenants command/test APIs or explicitly documented Tenants workflow.
- Do not treat `eventstore:tenant`, `tenant_id`, `tid`, or `tenants` claims as sufficient authorization. Claims identify the requested tenant context; Tenants projection membership and role authorize it.
- Do not derive test authorization outcomes from JWT claims alone. Tenant status, membership, removed-user state, and role must come from Hexalith.Tenants-backed state, approved Tenants testing helpers, or the Story 11.2 tenant-access projection/service seam.
- Do not add tenant id parameters to MCP tool schemas or party command payloads.
- Do not change EventStore actor identity formats or Parties projection keys.
- Do not require DAPR/Aspire for fast unit-level authorization tests.
- Do not log full tokens, full claim sets, membership dictionaries, contact channel values, identifier values, names, or other personal party data in tests, docs, validation output, or troubleshooting examples.
- Do not run recursive submodule initialization/update. Use the checked-out root-level `Hexalith.Tenants` submodule content.

### Testing Requirements

- Fast tests must cover allowed/denied access decisions with active tenant, disabled tenant, removed user, insufficient role, unknown tenant, missing user id, and missing/stale local projection state.
- Fast tests must include the negative contract case where a valid JWT tenant claim is present but no active Tenants membership/role exists.
- Fast authorization tests belong in `tests/Hexalith.Parties.CommandApi.Tests` and must use in-process Tenants helpers/fakes or the tenant-access seam; REST/MCP/projection isolation belongs in CommandApi or integration suites; deployment validation belongs in `tests/Hexalith.Parties.DeployValidation.Tests`.
- REST and MCP tests must prove authorization occurs before projection reads or command routing for representative read/write/admin paths.
- Cross-tenant projection isolation must remain covered: tenant A cannot list/search/read tenant B data, and opaque ids for another tenant must not enable enumeration.
- Deployment validation tests must cover both console and JSON output for Tenants failures so CI can consume the results.
- Full Aspire tests should follow the existing fixture pattern and report infrastructure unavailability instead of producing misleading product failures.

### Latest Technical Information

- DAPR pub/sub topic access can be constrained with component scopes plus `publishingScopes` and `subscriptionScopes`; sensitive topics should explicitly list allowed applications instead of relying on omitted scopes. Source: https://docs.dapr.io/developing-applications/building-blocks/pubsub/pubsub-scopes/
- DAPR declarative subscriptions use `apiVersion: dapr.io/v2alpha1`; the subscription spec supports `deadLetterTopic` for messages that cannot be delivered successfully. Source: https://docs.dapr.io/reference/resource-specs/subscription-schema/
- DAPR dead-letter topics can be configured with declarative subscriptions and should be paired with retry/resiliency policy for production message handling. Source: https://docs.dapr.io/developing-applications/building-blocks/pubsub/pubsub-deadletter/
- .NET Aspire integration tests can use `DistributedApplicationTestingBuilder` to create and start the AppHost for end-to-end scenarios; keep those tests separate from sidecar-free unit tests. Source: https://learn.microsoft.com/en-us/dotnet/aspire/testing/manage-app-host
- Aspire health checks and `WaitFor()` support startup orchestration and resource health visibility; use the existing topology fixture style when adding Tenants-aware full-stack tests. Source: https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/health-checks

### Previous Story Intelligence

Story 11.3 owns broad REST/MCP authorization enforcement through `ITenantAccessService`. This story should test and document that behavior; it should not re-open endpoint authorization design unless the implementation lacks testable seams. [Source: _bmad-output/implementation-artifacts/11-3-rest-and-mcp-tenant-authorization-enforcement.md]

Story 11.2 owns Tenants event consumption and local access projection. This story should verify and document the local projection, eventual-consistency, and fail-closed behavior; it should not create another Tenants event processor or projection store in Parties. [Source: _bmad-output/implementation-artifacts/11-2-tenants-event-consumption-and-local-access-projection.md]

Story 11.1 owns AppHost/package integration and local Tenants topology. This story may add tests and docs around that topology, but should not change resource names or command gateway boundaries without an explicit architecture decision. [Source: _bmad-output/implementation-artifacts/11-1-apphost-and-package-integration.md]

### Git Intelligence

Recent commits show Story 11.3 was just created and submodule pointers were updated for EventStore, FrontComposer, and Memories. Keep this story narrow: improve validation, tests, and docs around Tenants integration without changing unrelated submodule pointers or reopening completed GDPR/search work.

### Project Structure Notes

- Tenants helper references belong in test projects, not production projects.
- Deployment validation logic stays in `deploy/validate-deployment.ps1`; validation tests stay in `tests/Hexalith.Parties.DeployValidation.Tests`.
- Fast authorization tests should stay under `tests/Hexalith.Parties.CommandApi.Tests`, using subfolders such as `Authorization/`, `Controllers/`, and `Mcp/`.
- Full-topology tests should stay under `tests/Hexalith.Parties.IntegrationTests` and reuse the existing Aspire fixture pattern.
- Documentation updates should prefer existing `README.md`, `docs/getting-started.md`, `docs/deployment-guide.md`, and `docs/deployment-security-checklist.md`.
- No separate UX artifact was found for this story.
- No `project-context.md` persistent fact file was found in the repository during story creation.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 11.4: Tenants Integration Tests, Deployment Validation, and Documentation]
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-05-02.md#Implementation Handoff]
- [Source: _bmad-output/planning-artifacts/architecture.md#Architectural Fitness Tests]
- [Source: _bmad-output/implementation-artifacts/11-3-rest-and-mcp-tenant-authorization-enforcement.md]
- [Source: _bmad-output/implementation-artifacts/11-2-tenants-event-consumption-and-local-access-projection.md]
- [Source: _bmad-output/implementation-artifacts/11-1-apphost-and-package-integration.md]
- [Source: deploy/validate-deployment.ps1]
- [Source: tests/Hexalith.Parties.DeployValidation.Tests/DeploymentValidationTests.cs]
- [Source: tests/Hexalith.Parties.IntegrationTests/HealthChecks/PartiesAspireTopologyFixture.cs]
- [Source: tests/Hexalith.Parties.IntegrationTests/Events/TenantIsolationTests.cs]
- [Source: docs/getting-started.md]
- [Source: docs/deployment-guide.md]
- [Source: docs/deployment-security-checklist.md]
- [Source: Hexalith.Tenants/src/Hexalith.Tenants.Testing/Helpers/TenantTestHelpers.cs]
- [Source: Hexalith.Tenants/src/Hexalith.Tenants.Testing/Fakes/InMemoryTenantService.cs]
- [Source: Hexalith.Tenants/src/Hexalith.Tenants.Testing/Projections/InMemoryTenantProjection.cs]
- [Source: Hexalith.Tenants/src/Hexalith.Tenants.Client/Projections/TenantLocalState.cs]
- [Source: Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Enums/TenantRole.cs]

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Party-mode review completed on 2026-05-04; story clarified before development.

### File List

### Party-Mode Review

- Date: 2026-05-04T09:06:10.6004188+02:00
- Selected story key: 11-4-tenants-integration-tests-deployment-validation-and-documentation
- Command/skill invocation used: `/bmad-party-mode 11-4-tenants-integration-tests-deployment-validation-and-documentation; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect), Paige (Technical Writer)
- Findings summary:
  - Authorization tests needed an explicit source-of-truth rule so JWT tenant claims identify context only while Hexalith.Tenants-backed membership and role authorize access.
  - Tenants testing helper usage needed clearer fixture ownership to avoid Parties-local tenant lifecycle, membership, role, disabled-tenant, or removed-user policy fakes.
  - Cross-tenant projection isolation needed observable REST/MCP/read-model assertions without changing EventStore actor identities, command payloads, MCP schemas, or projection keys.
  - Deployment validation needed distinct, secret-safe failure categories and CI-readable output for missing subscription, missing or malformed configuration, unreachable dependency signals, and bypassed authorization wiring.
  - Operator and developer docs needed scenario-based setup and troubleshooting guidance that separates missing JWT context from missing membership, insufficient role, disabled tenant, and removed user cases.
- Changes applied:
  - Tightened acceptance criteria around Tenants-backed authorization truth, actionable validation errors, Hexalith.Tenants provisioning ownership, and troubleshooting observables.
  - Added task bullets for public Tenants testing helpers, valid-JWT-without-membership negative coverage, sidecar-free fast tests, projection isolation surfaces, representative REST/MCP integration proof, and validation error categories.
  - Added documentation requirements for setup order, no tenant ids in command/MCP payloads, and a troubleshooting decision table.
  - Added guardrail and testing requirement notes clarifying fixture ownership and test placement.
- Findings deferred:
  - Exact deployment validator implementation details remain for development, provided the required failure categories and secret-safe output contract are met.
  - Exact REST/MCP representative endpoint selection remains implementation-specific, provided it proves Tenants-backed authorization and projection isolation without reopening Story 11.3 design.
- Final recommendation: ready-for-dev
