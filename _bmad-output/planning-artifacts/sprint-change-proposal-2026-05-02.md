# Sprint Change Proposal - Use Hexalith.Tenants for Parties Tenant Management

**Project:** Hexalith.Parties  
**Date:** 2026-05-02  
**Mode:** Batch  
**Status:** Approved
**Approved by:** Jérôme
**Approved on:** 2026-05-02

## 1. Issue Summary

### Trigger

Hexalith.Parties should use the `Hexalith.Tenants` module to manage Parties tenants instead of treating tenant handling as a local Parties concern.

This change was triggered by alignment with the `Hexalith.Tenants` module, whose PRD explicitly positions Hexalith.Parties as the first consuming service and provides tenant lifecycle management, user-role membership, tenant configuration, read models, DAPR event integration, Aspire hosting, and testing fakes.

### Current Problem

The Parties planning artifacts correctly require tenant isolation, but they currently describe the tenant boundary mostly as:

- JWT tenant extraction.
- EventStore identity scoping.
- Parties-side projection filtering.
- Parties deployment validation.

They do not define `Hexalith.Tenants` as the source of truth for:

- Tenant lifecycle: create, update, disable, enable.
- Tenant membership and role assignment.
- Tenant configuration.
- Tenant event consumption by Parties.
- Local authorization projection or test fakes from `Hexalith.Tenants.Testing`.

This creates duplicated tenant-management responsibility and risks diverging behavior between Hexalith domain services.

### Evidence

- `Hexalith.Tenants` PRD says Hexalith.Parties is the first consumer and that a consuming service can become tenant-aware through DI registration, event subscription, and access enforcement.
- `Hexalith.Tenants` MVP includes Tenant aggregate lifecycle, tenant user roles, global administrators, tenant configuration, read models, DAPR pub/sub integration, Aspire hosting, and test helpers.
- `Hexalith.Parties` PRD currently says multi-tenancy arrives "Via EventStore" and does not name `Hexalith.Tenants` as the tenant authority.
- Current Parties implementation and docs reference tenant IDs and JWT claims directly, including MCP session tenant extraction, projection actor keys, deployment validation, and getting-started tenant troubleshooting.

## 2. Impact Analysis

### Checklist Status

| Item | Status | Notes |
|---|---:|---|
| 1.1 Triggering story | [N/A] | Strategic module-alignment change, not discovered by one story. Most directly affects Stories 1.7, 5.1-5.4, 6.2, 8.1, and future Epic 10. |
| 1.2 Core problem | [x] | Existing Parties artifacts do not make `Hexalith.Tenants` the authority for tenant lifecycle, membership, role/config state. |
| 1.3 Evidence | [x] | Tenants PRD/epics and Parties PRD/architecture/code references reviewed. |
| 2.1 Current epic impact | [x] | Completed tenant-related stories remain valuable, but their tenant authority assumptions need hardening. |
| 2.2 Epic-level changes | [x] | Add a dedicated cross-cutting integration epic rather than reopening many completed stories. |
| 2.3 Remaining epic impact | [x] | Epic 10 must not implement tenant management UI inside Parties. It should link to or rely on Tenants admin surfaces. |
| 2.4 Future epic validity | [x] | No existing epic becomes obsolete. A new integration epic is needed. |
| 2.5 Priority/order | [x] | New integration epic should be completed before Epic 10 admin portal work. |
| 3.1 PRD conflicts | [x] | PRD must separate tenant authority from party data isolation. |
| 3.2 Architecture conflicts | [x] | Architecture must add Tenants integration, event subscription, local tenant-access projection, and test strategy. |
| 3.3 UX conflicts | [N/A] | No Parties UX artifact exists. Future admin UX impact is captured under Epic 10. |
| 3.4 Other artifacts | [!] | Docs, deployment validation, AppHost, tests, and current command/API authorization code need follow-up implementation changes. |
| 4.1 Direct adjustment | Viable | Add integration epic and targeted artifact edits. Effort: Medium. Risk: Medium. |
| 4.2 Rollback | Not viable | Existing tenant isolation code is still needed as local enforcement. Rollback would remove useful security work. |
| 4.3 MVP review | Not viable | MVP goals stay valid. This is an authority/integration correction, not scope reduction. |
| 4.4 Recommended path | [x] | Direct adjustment with new integration epic. |
| 5.1-5.5 Proposal components | [x] | Captured in this document. |
| 6.1-6.5 Final review/handoff | [x] | Approved by Jérôme on 2026-05-02; backlog/status update completed. |

### Epic Impact

**Affected completed stories:**

- Story 1.7 AppHost/local development: AppHost must include or reference `Hexalith.Tenants` topology and seed/bootstrap tenant state for local dev.
- Stories 5.1-5.4 MCP: MCP tenant extraction must be backed by Tenants membership/authorization, not just a claim.
- Story 6.2 Getting-started/README: docs must instruct users to run/provision through `Hexalith.Tenants`.
- Story 8.1 Deployment validation: validation must check Tenants subscription/configuration and disabled-tenant rejection behavior.
- Story 8.2 Health/readiness: readiness should include Tenants integration health or clearly document degraded behavior.
- Future Epic 10 Admin: Parties admin portal should not own tenant creation, tenant membership, tenant role, or tenant configuration management.

**Recommended backlog change:**

Add a new epic before Epic 10:

> Epic 11: Hexalith.Tenants Integration for Parties

This keeps completed stories intact and adds a focused integration layer rather than reopening the entire plan.

### Artifact Conflicts

**PRD conflict:** Parties currently describes multi-tenancy as an EventStore/JWT/platform concern. It should distinguish:

- `Hexalith.Tenants` owns tenant lifecycle, membership, roles, and configuration.
- EventStore/Parties enforce tenant-scoped storage and local request isolation.
- Parties consumes Tenants state/events to authorize and react to tenant changes.

**Architecture conflict:** The architecture has detailed tenant isolation but no component for:

- `Hexalith.Tenants.Contracts` or `Hexalith.Tenants.Client`.
- Tenants event subscription.
- Local tenant authorization projection/cache.
- Disabled-tenant behavior.
- Membership revocation propagation.
- Test strategy with `Hexalith.Tenants.Testing`.

**Implementation/documentation conflict:** Existing API/MCP flows use tenant claims directly. They need a validation/enforcement layer backed by Tenants state.

### Technical Impact

- Add `Hexalith.Tenants` package/project references where appropriate, likely `Hexalith.Parties`, `AppHost`, deployment validation tests, and integration tests.
- Add a tenant access service in Parties that answers: tenant exists, tenant is active, user has required role, tenant config relevant to Parties.
- Subscribe to Tenants events and maintain a local read model or cache for fast command-path checks.
- Preserve EventStore tenant scoping and actor key formats. Tenants does not replace aggregate identity isolation.
- Update JWT claims transformation to normalize identity while tenant membership is verified against Tenants state.
- Add integration tests for create party allowed/denied, disabled tenant denied, removed user denied, and projection isolation.
- Update local dev docs to provision tenants through `Hexalith.Tenants`.

## 3. Recommended Approach

### Selected Path: Direct Adjustment

Use a targeted backlog addition and artifact edits:

1. Update PRD language so `Hexalith.Tenants` is the tenant authority.
2. Update architecture with a Tenants integration component, event subscription, local projection/cache, and authorization boundary.
3. Add a new Epic 11 with stories for AppHost integration, local tenant projection, authorization enforcement, and docs/tests.
4. Update future Epic 10 acceptance criteria so tenant management remains outside Parties.

### Rationale

This avoids duplicated tenant management while preserving the existing Parties isolation work. EventStore and Parties still enforce storage/query separation, but tenant lifecycle and membership decisions come from a shared domain service.

### Effort, Risk, Timeline

- **Effort:** Medium. The change touches API/MCP authorization, AppHost, docs, tests, and deployment validation.
- **Risk:** Medium. Main risks are eventual consistency of tenant event consumption and the synchronous authorization path while Tenants events have not yet propagated.
- **Timeline impact:** Add one cross-cutting epic before Epic 10. Existing Epic 1-9 work should not be rolled back.
- **Scope classification:** Moderate. Requires backlog reorganization and developer implementation, but not a fundamental PRD replan.

## 4. Detailed Change Proposals

### PRD Changes

#### PRD - Product Scope, MVP Included

**OLD:**

```text
Via EventStore (zero additional effort): JWT auth, multi-tenancy, event publishing, idempotent commands, convention-based discovery, snapshot support
```

**NEW:**

```text
Via EventStore and Hexalith.Tenants: EventStore provides tenant-scoped aggregate identity, event publishing, idempotent commands, convention-based discovery, snapshot support, and fail-closed request infrastructure. Hexalith.Tenants is the source of truth for tenant lifecycle, membership, roles, and tenant configuration. Hexalith.Parties consumes Tenants state/events and enforces active-tenant and role-based access consistently across REST, MCP, projections, and operational tooling.
```

**Rationale:** Separates storage isolation from tenant authority and prevents Parties from owning tenant lifecycle.

#### PRD - Multi-Tenancy & Security Requirements

**OLD:**

```text
FR39: System isolates party data by tenant at all layers - no cross-tenant data access is possible. All API surfaces (REST and MCP) carry tenant context and receive identical tenant filtering
FR40: System identifies tenant from authenticated credentials, never from request payloads
FR41: System rejects requests without valid tenant identity (fail-closed)
```

**NEW:**

```text
FR39: System isolates party data by tenant at all layers - no cross-tenant data access is possible. All API surfaces (REST and MCP) carry tenant context, validate it through the Hexalith.Tenants authority model, and receive identical tenant filtering.
FR40: System identifies requested tenant context from authenticated credentials and normalized platform claims, never from command payloads. Membership, role, active/disabled status, and tenant configuration are resolved from Hexalith.Tenants.
FR41: System rejects requests without valid tenant identity, without an active tenant in Hexalith.Tenants, or without the required tenant role for the requested operation (fail-closed).
FR75: System consumes Hexalith.Tenants lifecycle, membership, role, and configuration events so tenant changes are reflected in Parties authorization and local projections without polling or custom synchronization jobs.
```

**Rationale:** Makes Tenants the authority and adds event-driven propagation as an explicit requirement.

#### PRD - Scalability Requirements

**OLD:**

```text
NFR15: Tenant metadata operations (routing, key lookup) complete in < 50ms regardless of total tenant count
```

**NEW:**

```text
NFR15: Tenant metadata operations needed on the Parties request path, including active-tenant and role checks backed by Hexalith.Tenants local projection/cache, complete in < 50ms regardless of total tenant count.
```

**Rationale:** Keeps the performance target but assigns the data source.

### Architecture Changes

#### Architecture - Technical Constraints & Dependencies

**OLD:**

```text
Parties-specific constraints:
- Single Party aggregate
- [PersonalData] attribute infrastructure at MVP
- Contracts package: zero runtime dependencies beyond netstandard2.1
- Client package: HTTP abstractions only
- MCP server: 5 composite tools
- REST API versioning
```

**NEW:**

```text
Parties-specific constraints:
- Single Party aggregate
- [PersonalData] attribute infrastructure at MVP
- Contracts package: zero runtime dependencies beyond netstandard2.1
- Client package: HTTP abstractions only
- MCP server: composite tools
- REST API versioning
- Hexalith.Tenants is the tenant authority. Parties must not create, update, disable, enable, or manage tenant membership directly.
- Parties consumes Hexalith.Tenants contracts/client/testing packages where needed for authorization, event projection, AppHost composition, deployment validation, and tests.
```

**Rationale:** Adds a hard architectural boundary.

#### Architecture - Cross-Cutting Multi-Tenancy

**OLD:**

```text
Multi-tenancy - Enforced at every layer: aggregate, event store, projections, API, MCP, pub/sub topics. Two distinct isolation mechanisms: write-side isolation and read-side isolation.
```

**NEW:**

```text
Multi-tenancy - Split into authority and enforcement. Hexalith.Tenants is the authority for tenant lifecycle, active/disabled state, user membership, roles, global administrators, and tenant configuration. EventStore and Parties enforce tenant-scoped aggregate identity, actor keys, projection filtering, pub/sub topic naming, API/MCP authorization, and log safety. Parties maintains a local Tenants-backed authorization projection/cache for fast request-path decisions and consumes Tenants events to react to membership, role, and tenant lifecycle changes.
```

**Rationale:** Clarifies that Parties enforces, but does not own, tenant truth.

#### Architecture - Solution Structure

**ADD:**

```text
src/Hexalith.Parties
  Authorization/
    ITenantAccessService.cs
    TenantsBackedTenantAccessService.cs
    TenantAccessRequirement.cs

src/Hexalith.Parties.Projections
  Tenants/
    TenantAccessProjectionHandler.cs
    TenantAccessReadModel.cs

tests/Hexalith.Parties.Tests
  Authorization/
    TenantAccessAuthorizationTests.cs

tests/Hexalith.Parties.IntegrationTests
  Tenants/
    TenantsIntegrationFlowTests.cs
```

**Rationale:** Gives implementers concrete file/module ownership without moving party domain logic into tenant code.

### Epic Changes

#### Add New Epic 11

**NEW:**

```text
## Epic 11: Hexalith.Tenants Integration for Parties

Hexalith.Parties uses Hexalith.Tenants as the source of truth for tenant lifecycle, membership, roles, and tenant configuration while preserving Parties-owned tenant isolation for party aggregates, projections, REST, MCP, and event publication.

### Story 11.1: AppHost and Package Integration

As a developer running Hexalith.Parties locally,
I want the Parties AppHost to compose with Hexalith.Tenants,
So that tenant lifecycle and membership are available through the same local topology as party management.

Acceptance Criteria:
- Parties AppHost references the Tenants service/topology using the Tenants Aspire integration or equivalent local composition.
- Local development seeds or documents a default active tenant through Hexalith.Tenants, not by hardcoded Parties state.
- Parties startup validates that Tenants integration configuration is present when tenant authorization is enabled.
- Failure to reach Tenants is surfaced in health/readiness according to documented degraded behavior.

### Story 11.2: Tenants Event Consumption and Local Access Projection

As Hexalith.Parties,
I want to consume Hexalith.Tenants lifecycle, membership, role, and configuration events,
So that authorization decisions can be made locally without polling.

Acceptance Criteria:
- Parties subscribes to relevant Tenants events through DAPR pub/sub.
- A local tenant access projection/cache records active tenant state, user membership, roles, and relevant tenant configuration.
- Tenant disable and user removal events cause subsequent Parties commands and MCP tools to fail closed.
- Eventual consistency behavior is documented, including the synchronous enforcement path if the Tenants authorization plugin is enabled.

### Story 11.3: REST and MCP Tenant Authorization Enforcement

As a platform operator,
I want all Parties REST and MCP operations to enforce Tenants-backed access rules,
So that users cannot manage party data for inactive or unauthorized tenants.

Acceptance Criteria:
- REST command and query endpoints validate tenant access through `ITenantAccessService`.
- MCP tools validate tenant access through the same service and do not rely only on `McpSessionContext.Tenant`.
- Required roles are explicit: Reader for read/search, Contributor for create/update/deactivate/reactivate, Owner or configured elevated role for administrative party operations.
- Requests with missing tenant, inactive tenant, missing membership, insufficient role, or stale/unknown tenant state are rejected with standardized ProblemDetails or MCP errors.
- Tenant ID is never accepted from command payloads.

### Story 11.4: Tenants Integration Tests, Deployment Validation, and Documentation

As a developer and operator,
I want tests and docs proving Parties uses Hexalith.Tenants correctly,
So that tenant integration is reliable in CI and local development.

Acceptance Criteria:
- Tests use `Hexalith.Tenants.Testing` where appropriate for fast authorization scenarios.
- Integration tests cover active tenant allowed, disabled tenant denied, removed user denied, insufficient role denied, and cross-tenant projection isolation.
- Deployment validation checks Tenants subscription/configuration and reports actionable errors.
- Getting-started docs provision tenants through Hexalith.Tenants.
- Existing tenant troubleshooting is updated to distinguish missing JWT claims from missing Tenants membership/role.
```

**Rationale:** Adds a focused integration epic without destabilizing completed Party domain stories.

#### Epic 10 Admin Portal Adjustment

**OLD:**

```text
Administrators can browse, search, and inspect party records and process GDPR requests via a TypeScript admin portal.
```

**NEW:**

```text
Administrators can browse, search, and inspect party records and process party-level GDPR requests via a TypeScript admin portal. Tenant lifecycle, tenant membership, tenant roles, and tenant configuration management remain owned by Hexalith.Tenants admin capabilities; the Parties admin portal consumes active tenant context and must not duplicate Tenants management screens.
```

**Rationale:** Prevents future UI duplication and keeps tenant management centralized.

### Story-Level Changes

#### Story 1.7 - AppHost Local Development and GDPR Warning

**OLD:**

```text
Given the AppHost is running, the service can process party requests with a development tenant claim.
```

**NEW:**

```text
Given the AppHost is running, a default development tenant is provisioned through Hexalith.Tenants and the sample/test user is assigned a role that permits party commands.
```

**Rationale:** Local dev should exercise the same tenant authority path as production.

#### Story 5.1-5.4 - MCP Server and Tools

**OLD:**

```text
MCP tools obtain tenant context from the authenticated request/session and use it for actor IDs and projection access.
```

**NEW:**

```text
MCP tools obtain normalized tenant context from the authenticated request/session, then validate active tenant state and required user role through the Tenants-backed `ITenantAccessService` before issuing commands or reading projections.
```

**Rationale:** Tenant claim presence is not sufficient authorization.

#### Story 6.2 - Getting-Started Guide and README

**OLD:**

```text
Ensure the token contains the required tenant claim (`eventstore:tenant`).
```

**NEW:**

```text
Provision the tenant and assign the test user through Hexalith.Tenants, then ensure the token carries the normalized tenant context expected by EventStore/Parties. A valid claim without matching active Tenants membership is rejected.
```

**Rationale:** Guides users through the actual source of tenant truth.

#### Story 8.1 - Deployment Validation Tooling

**OLD:**

```text
Validation verifies DAPR access controls, state store tenant namespace scoping, pub/sub topic access, and secret store access.
```

**NEW:**

```text
Validation verifies DAPR access controls, state store tenant namespace scoping, pub/sub topic access, secret store access, and Hexalith.Tenants integration: Tenants service configured, relevant event subscriptions active, local tenant access projection healthy, and disabled/unauthorized tenant checks fail closed.
```

**Rationale:** Deployment is not secure unless the tenant authority integration is working.

## 5. Implementation Handoff

### Scope Classification

**Moderate**

The change requires backlog updates and implementation across multiple already-completed areas, but it does not invalidate the core Parties domain model or require PRD scope reduction.

### Route To

- Product Owner / Developer: approve and add Epic 11 to backlog and `sprint-status.yaml`.
- Developer agent: implement Epic 11 after approval.
- Architect: review the Tenants integration boundary if the synchronous EventStore authorization plugin is available or changes the design.

### Developer Responsibilities

- Inspect current public APIs/packages in `Hexalith.Tenants` before coding against assumptions.
- Add Tenants package or project references only where needed.
- Keep `Hexalith.Parties.Contracts` free of Tenants dependencies.
- Implement `ITenantAccessService` as the boundary inside Parties.
- Preserve existing EventStore aggregate identity format and projection tenant filtering.
- Use `Hexalith.Tenants.Testing` for fast unit/integration-style authorization tests.
- Add deployment validation coverage for missing Tenants configuration and fail-closed behavior.

### Success Criteria

- Parties can create/read/update/search party data only for an active tenant managed by Hexalith.Tenants.
- Removed users and disabled tenants are denied consistently across REST and MCP.
- Tenant lifecycle and membership are not duplicated in Parties.
- Local development provisions tenant state through Tenants.
- Docs clearly explain the relationship: Tenants owns tenant management; Parties owns party data.
- Cross-tenant isolation tests still pass after Tenants integration.

### Sprint Status Update After Approval

Approved update applied to `_bmad-output/implementation-artifacts/sprint-status.yaml`:

```yaml
  # Epic 11: Hexalith.Tenants Integration for Parties (4 stories)
  epic-11: backlog
  11-1-apphost-and-package-integration: backlog
  11-2-tenants-event-consumption-and-local-access-projection: backlog
  11-3-rest-and-mcp-tenant-authorization-enforcement: backlog
  11-4-tenants-integration-tests-deployment-validation-and-documentation: backlog
  epic-11-retrospective: optional
```

Epic 11 should be scheduled before Epic 10.

## Approval

Approved by Jérôme on 2026-05-02.

Epic 11 has been added to `sprint-status.yaml` before Epic 10.
