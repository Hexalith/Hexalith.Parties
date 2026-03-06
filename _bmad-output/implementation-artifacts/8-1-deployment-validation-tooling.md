# Story 8.1: Deployment Validation Tooling

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an operator,
I want a deployment validation tool that verifies security configuration before production use,
So that I can be confident DAPR access controls, tenant isolation, and pub/sub policies are correctly configured.

## Acceptance Criteria

1. **DAPR security configuration verification (FR61)**: Given a deployment validation script or tool, when executed against a target deployment, then it verifies the following DAPR security configurations:
   - Access control policies are defined and restrict cross-tenant pub/sub access
   - State store access is scoped per tenant namespace
   - Pub/sub topic access control prevents unauthorized subscription
   - Secret store access is configured (preparation for v1.1 key management)

2. **Misconfiguration reporting**: Given the validation tool, when a misconfiguration is detected, then the specific misconfiguration is reported with:
   - What is wrong
   - What the correct configuration should be
   - A reference to the security config checklist

3. **Validation success confirmation**: Given the validation tool, when all checks pass, then a "deployment validated" confirmation is output, and the validation results can be logged for audit purposes.

4. **Production DAPR component configurations exist**: Given the `/deploy/` directory, when reviewed for deployment artifacts, then production DAPR component configurations exist for:
   - `pubsub-kafka.yaml`, `pubsub-rabbitmq.yaml`, `pubsub-servicebus.yaml`
   - `statestore-cosmosdb.yaml`, `statestore-postgresql.yaml`
   - `accesscontrol.yaml`, `resiliency.yaml`
   - `subscription-parties.yaml`
   And a security config checklist document exists with operator responsibilities.

5. **Deployment documentation for operators**: Given the deployment documentation, when reviewed for operator guidance, then it covers:
   - Required DAPR component configuration per deployment target
   - Minimum state store requirements (entry size limits for index actor -- D5)
   - Backup strategy guidance that accounts for crypto-shredding (v1.1 preparation)
   - Network security and infrastructure IAM responsibilities (operator scope)

## Tasks / Subtasks

- [x] Task 1: Create missing production state store DAPR component templates (AC: #4)
  - [x] Create `deploy/dapr/statestore-cosmosdb.yaml` — CosmosDB state store with actorStateStore, scoped to commandapi, env-var connection string, entry size limit documentation (D5)
  - [x] Create `deploy/dapr/statestore-postgresql.yaml` — PostgreSQL state store with actorStateStore, scoped to commandapi, env-var connection string

- [x] Task 2: Create deployment validation tool (AC: #1, #2, #3)
  - [x] Create `deploy/validate-deployment.ps1` (PowerShell cross-platform script) that:
    - Accepts a `--config-path` parameter pointing to the DAPR component directory to validate
    - Loads and parses all YAML component files in the target directory
    - Returns structured validation results (pass/fail per check, details, recommendations)
    - Exits with code 0 on success, non-zero on failure (CI-friendly)
  - [x] Implement access control validation checks:
    - `accesscontrol.yaml` exists and has `defaultAction: deny`
    - Trust domain is NOT "public" (production requires real SPIFFE domain)
    - Policies restrict operations to known app-ids only
  - [x] Implement state store validation checks:
    - State store component exists with `actorStateStore: true`
    - Scopes list contains ONLY `commandapi` (no other app-ids)
    - Connection string uses env-var reference (not hardcoded)
  - [x] Implement pub/sub validation checks:
    - Pub/sub component exists with scopes defined
    - `publishingScopes` restricts subscribers from publishing
    - `subscriptionScopes` restricts subscribers to authorized tenant topics only
    - `enableDeadLetter` is `true`
    - Connection string uses env-var reference (not hardcoded)
  - [x] Implement subscription validation checks:
    - Subscription file exists with `v2alpha1` API version
    - Dead-letter topic is configured
    - Scopes reference subscriber app-ids (not hardcoded)
  - [x] Implement resiliency validation checks:
    - Resiliency policy exists
    - Circuit breakers are configured for pub/sub and state store
    - Retry policies use exponential backoff
  - [x] Implement secret store validation check (v1.1 preparation):
    - Warn if no secret store component found (advisory, not blocking)
  - [x] Implement validation output:
    - Structured console output with pass/fail per check category
    - Summary: total checks, passed, failed, warnings
    - Machine-readable JSON output option (`--output json`)
    - Audit-loggable format with timestamp and config path

- [x] Task 3: Create security config checklist document (AC: #4, #5)
  - [x] Create `docs/deployment-security-checklist.md` covering:
    - Pre-deployment checklist (access control, state store scoping, pub/sub scoping, secret store)
    - Per-broker deployment notes (Kafka, RabbitMQ, Azure Service Bus)
    - State store backend notes (CosmosDB entry size limits per D5, PostgreSQL)
    - Tenant provisioning checklist (subscription file per tenant, scope updates)
    - v1.1 preparation (secret store, key management, backup strategy for crypto-shredding)
    - Network security and IAM responsibilities (operator scope -- out of service boundary)
    - Reference to `deploy/validate-deployment.ps1` for automated verification

- [x] Task 4: Create deployment guide document (AC: #5)
  - [x] Create `docs/deployment-guide.md` covering:
    - Required DAPR component configuration per deployment target (Kafka, RabbitMQ, Service Bus)
    - State store selection guidance (Redis for dev, CosmosDB/PostgreSQL for production)
    - Minimum state store requirements and entry size limits (D5)
    - Multi-tenant setup (subscription file per tenant, scope customization)
    - Backup strategy guidance accounting for crypto-shredding (v1.1 preparation)
    - Running the validation tool before go-live
    - Troubleshooting common misconfigurations

- [x] Task 5: Create validation tool tests (AC: #1, #2, #3)
  - [x] Create `tests/Hexalith.Parties.DeployValidation.Tests/` project (xUnit + Shouldly)
  - [x] Test: Valid production config passes all checks
  - [x] Test: Missing `accesscontrol.yaml` fails with specific error
  - [x] Test: `defaultAction: allow` in accesscontrol fails with recommendation to set `deny`
  - [x] Test: Hardcoded connection string fails with recommendation to use env-var
  - [x] Test: Missing state store scopes fails with recommendation
  - [x] Test: Missing pub/sub dead-letter config fails with recommendation
  - [x] Test: Subscriber in publishingScopes with non-empty topics fails
  - [x] Test: Missing resiliency config fails with recommendation
  - [x] Test: JSON output mode produces valid parseable JSON
  - [x] Test: Local dev config (AppHost/DaprComponents/) passes with warnings (allow defaultAction acceptable for dev)
  - [x] Test: Secret store warning is advisory, not blocking

## Dev Notes

### Architecture & Conventions

- **Target framework**: net10.0 (pinned in `global.json` to SDK 10.0.103)
- **Build**: `TreatWarningsAsErrors=true`, nullable enabled, implicit usings, file-scoped namespaces
- **Central package management**: All package versions in `Directory.Packages.props` at solution root
- **Solution format**: `Hexalith.Parties.slnx` (modern XML format)
- **Code style**: `.editorconfig` — Allman braces, `_camelCase` private fields, `I` prefix for interfaces, `Async` suffix, 4 spaces, CRLF, UTF-8
- **Test naming**: `{Method}_{Scenario}_{ExpectedResult}`
- **Assertions**: Shouldly library
- **Test tiers**: Tier 1 (pure logic, no DAPR), Tier 2 (DAPR slim), Tier 3 (full Aspire topology)
- **DAPR SDK**: Dapr.Client 1.16.1, Dapr.AspNetCore 1.16.1, Dapr.Actors 1.16.1
- **Packages**: xUnit 2.9.3, Shouldly 4.3.0, NSubstitute 5.3.0, YamlDotNet 16.3.0, coverlet.collector 6.0.4
- **CI**: GitHub Actions — restore → build (Release) → Tier 1+2 tests → optional Tier 3

### What Already Exists (DO NOT Recreate)

**Production DAPR component configs (deploy/dapr/) — 6 files exist:**
- `accesscontrol.yaml` — Configuration kind, `defaultAction: deny`, trust domain via env-var, commandapi policy with `/**` POST allow
- `resiliency.yaml` — Resiliency kind, exponential retries (10 default, 5 outbound pubsub, 20 inbound pubsub), circuit breakers, per-component targets
- `subscription-parties.yaml` — Subscription v2alpha1, single-tenant example (`sample-tenant`), dead-letter topic, subscriber app-id via env-var
- `pubsub-kafka.yaml` — Component kind, 3-layer scoping architecture (component/publishing/subscription), env-var connection, dead-letter enabled
- `pubsub-rabbitmq.yaml` — Same 3-layer scoping pattern, RabbitMQ-specific metadata (durable, deletedWhenUnused)
- `pubsub-servicebus.yaml` — Same 3-layer scoping pattern, Azure Service Bus topics, pre-created topics note

**Local dev DAPR configs (src/Hexalith.Parties.AppHost/DaprComponents/) — 5 files exist:**
- `accesscontrol.yaml` — `defaultAction: allow` (self-hosted, no mTLS), trust domain `public`
- `statestore.yaml` — Redis, `actorStateStore: true`, scoped to commandapi
- `pubsub.yaml` — Redis Streams pubsub for local dev
- `resiliency.yaml` — Same resiliency policies as production
- `subscription-parties.yaml` — Local dev subscription

**Key patterns in existing deploy configs (FOLLOW EXACTLY):**
- All configs use `{env:VAR_NAME}` for secrets/connection strings — NEVER hardcode
- Component scopes restrict access to `commandapi` + explicit subscriber app-ids
- 3-layer pub/sub scoping: component scopes → publishingScopes → subscriptionScopes
- `publishingScopes` denies subscribers from publishing (`{env:SUBSCRIBER_APP_ID}=`)
- `subscriptionScopes` restricts subscribers to specific tenant topics
- Production accesscontrol uses `{env:DAPR_TRUST_DOMAIN|hexalith.io}` and `{env:DAPR_NAMESPACE|hexalith}`
- All files include extensive header comments explaining purpose, architecture, and customization steps

**Health check endpoints (ALREADY EXIST — Story 8.2 scope, NOT this story):**
- `src/Hexalith.Parties.ServiceDefaults/Extensions.cs` — Registers `/health`, `/alive`, `/ready` endpoints
- Default checks are basic self-checks (`HealthCheckResult.Healthy()`) with "live" and "ready" tags
- Status codes: Healthy=200, Degraded=200, Unhealthy=503
- Development mode returns detailed JSON; production returns minimal plaintext
- Extending health checks with DAPR sidecar/state store/pub/sub checks is Story 8.2 scope

**AppHost DAPR config path resolution pattern (reference):**
- `src/Hexalith.Parties.AppHost/Program.cs` resolves accesscontrol.yaml path with fallback:
  - First: `./DaprComponents/accesscontrol.yaml` (relative to CWD)
  - Fallback: `../../../../DaprComponents/accesscontrol.yaml` (relative to binary output)
  - Throws `FileNotFoundException` if neither exists

**Files that DO NOT exist yet (must create):**
- `deploy/dapr/statestore-cosmosdb.yaml` — Required by architecture
- `deploy/dapr/statestore-postgresql.yaml` — Required by architecture
- `docs/deployment-security-checklist.md` — Security checklist for operators
- `docs/deployment-guide.md` — Deployment guide for operators
- `deploy/validate-deployment.ps1` — Validation tool

**Existing documentation (EXTEND — do not rewrite):**
- `docs/event-publishing.md` — Production broker config, topic naming, dead-letter, retry/circuit breaker
- `docs/event-subscribing.md` — Wire format, event types, idempotency, ordering per broker
- `docs/event-handler-patterns.md` — Handler patterns per event type, PartyErased mandatory handler
- `docs/getting-started.md` — Getting started guide (reference, link from deployment guide)

**Existing test projects (7 projects, 371+ tests passing):**
- `tests/Hexalith.Parties.Contracts.Tests/` — Tier 1
- `tests/Hexalith.Parties.Client.Tests/` — Tier 1
- `tests/Hexalith.Parties.Server.Tests/` — Tier 1
- `tests/Hexalith.Parties.Projections.Tests/` — Tier 1
- `tests/Hexalith.Parties.CommandApi.Tests/` — Tier 2
- `tests/Hexalith.Parties.IntegrationTests/` — Tier 3
- `tests/Hexalith.Parties.Sample.Tests/` — Tier 1

### Validation Tool Design Decisions

**PowerShell over C# console app:** The validation tool is a deployment-time script, not a runtime service. PowerShell is cross-platform (PowerShell 7+), requires no compilation, and integrates naturally into CI/CD pipelines and operator workflows. It parses YAML files and reports findings — no .NET runtime dependency needed at deployment time.

**YAML parsing approach:** Use `ConvertFrom-Yaml` (from `powershell-yaml` module) or fallback to regex-based parsing for environments without the module installed. The script should work standalone with minimal dependencies.

**Test project uses YamlDotNet:** YamlDotNet 16.3.0 is already in `Directory.Packages.props`. The test project uses YamlDotNet to create known-good and known-bad DAPR config YAML files in temporary directories, invokes the PowerShell script via `Process.Start`, and asserts on exit code and structured output. This validates the tool works correctly within the existing .NET test infrastructure.

### Architecture Decisions Relevant to This Story

**D5 — State Store Entry Size Limits:** DAPR state store backends have varying per-entry size limits. The partition interface keeps the door open for scale. Document minimum state store requirements for deployment (affects `statestore-cosmosdb.yaml` — CosmosDB has 2MB limit).

**D14 — Projection Rebuild Strategy:** v1.0 manual rebuild via admin endpoint. The deployment guide should reference this for operational awareness (Story 8.3 implements it).

**D15 — Projection Health Monitoring:** Projection actors handle state corruption gracefully. Referenced in deployment documentation for operational awareness (Story 8.3 implements it).

### Security Requirements Context

**PRD Threat Model (directly relevant to validation tool):**
- Cross-tenant pub/sub subscription → DAPR access control policies + deployment validation script
- Misconfigured DAPR access policies → Security config checklist + deployment validation script
- Compromised sidecar / key extraction → Per-tenant key namespaces + infrastructure hardening (operator)

**PRD Security Boundary Model:**
- **In-scope (service):** Tenant filtering, JWT claim extraction, input validation, log sanitization
- **Operator responsibility (validate these):** DAPR secret store hardening, pub/sub access control policies, infrastructure IAM, network security, key backup procedures, deployment configuration validation

### DAPR 3-Layer Scoping Architecture (Critical for Validation Logic)

The validation tool must verify the 3-layer pub/sub scoping pattern used across all broker configs:

1. **Layer 1 — Component Scoping (`scopes`):** Controls which app-ids can USE the pub/sub component. Only `commandapi` and authorized subscriber app-ids should be listed.
2. **Layer 2 — Publishing Scoping (`publishingScopes`):** Controls which app-ids can PUBLISH to which topics. Subscribers should have empty publish access (`{env:SUBSCRIBER_APP_ID}=`). `commandapi` NOT listed = unrestricted.
3. **Layer 3 — Subscription Scoping (`subscriptionScopes`):** Controls which app-ids can SUBSCRIBE to which topics. Subscribers restricted to authorized tenant topics only. `commandapi` NOT listed = unrestricted.

**DAPR limitation:** Wildcards (`*`) are NOT supported in scoping — strict string match only.

### Critical Anti-Patterns to Avoid

- **Do NOT modify** existing `deploy/dapr/` files — they are already correct. Only ADD missing files.
- **Do NOT modify** the EventStore submodule (read-only)
- **Do NOT modify** existing docs — only ADD new documentation files and cross-reference
- **Do NOT hardcode** connection strings, passwords, or tenant IDs in any config file
- **Do NOT create** a .NET console app for the validation tool — use PowerShell for deployment tooling
- **Do NOT break** existing tests — the 371+ existing tests must continue passing
- **Do NOT add** runtime validation to the service itself — this is a deployment-time tool
- **Do NOT use** `defaultAction: allow` in production accesscontrol configs
- **Do NOT validate** DAPR runtime behavior — only validate static configuration files

### Project Structure Notes

```
deploy/
    dapr/
        accesscontrol.yaml              (EXISTS — no change)
        resiliency.yaml                 (EXISTS — no change)
        subscription-parties.yaml       (EXISTS — no change)
        pubsub-kafka.yaml               (EXISTS — no change)
        pubsub-rabbitmq.yaml            (EXISTS — no change)
        pubsub-servicebus.yaml          (EXISTS — no change)
        statestore-cosmosdb.yaml        (NEW — production CosmosDB state store)
        statestore-postgresql.yaml      (NEW — production PostgreSQL state store)
    validate-deployment.ps1             (NEW — deployment validation tool)

docs/
    deployment-security-checklist.md    (NEW — operator security checklist)
    deployment-guide.md                 (NEW — deployment guide)
    event-publishing.md                 (NO CHANGE)
    event-subscribing.md                (NO CHANGE)
    event-handler-patterns.md           (NO CHANGE)
    getting-started.md                  (NO CHANGE — link from deployment guide)

tests/
    Hexalith.Parties.DeployValidation.Tests/
        Hexalith.Parties.DeployValidation.Tests.csproj  (NEW — Tier 1 test project)
        DeploymentValidationTests.cs                     (NEW — validation tool tests)
```

### Previous Story Learnings (from Story 7.3 and earlier)

- **Commit pattern**: `feat: Implement Story X.Y - <title>` with PRs from feature branches `feat/story-X-Y-slug`
- **Documentation style**: Extensive inline comments in YAML configs explaining architecture, customization, and operational notes
- **Test pattern**: `WebApplicationFactory<Program>` with xUnit/Shouldly for service tests; for script testing, invoke process and assert on output/exit code
- **File headers**: All DAPR configs include multi-line comment headers with purpose, architecture, and adding-new-service instructions
- **Cross-references**: Link docs together (e.g., handler patterns → publishing → subscribing → deployment)
- **xUnit parallel execution**: Use `[Collection]` attribute for test classes sharing static state
- **Existing test count**: 371+ tests passing — must not regress

### Git Intelligence

Recent commits follow pattern `feat: Implement Story X.Y - <title>` with PRs merged from `feat/story-X-Y-slug` branches. Epics 1-7 (28 stories) all completed. Story 8.1 is the first story in Epic 8 (Operational Readiness & Production Hardening).

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic 8, Story 8.1]
- [Source: _bmad-output/planning-artifacts/prd.md#FR61 — Deployment validation tooling]
- [Source: _bmad-output/planning-artifacts/prd.md#Security Boundary Model — Operator responsibility]
- [Source: _bmad-output/planning-artifacts/prd.md#Threat Model — Cross-tenant pub/sub, DAPR misconfiguration]
- [Source: _bmad-output/planning-artifacts/prd.md#FR64 — Graceful degradation]
- [Source: _bmad-output/planning-artifacts/prd.md#FR71 — Health/readiness signals]
- [Source: _bmad-output/planning-artifacts/prd.md#NFR20-22 — Reliability requirements]
- [Source: _bmad-output/planning-artifacts/architecture.md#D5 — State Store Entry Size Limits]
- [Source: _bmad-output/planning-artifacts/architecture.md#D14 — Projection Rebuild Strategy]
- [Source: _bmad-output/planning-artifacts/architecture.md#D15 — Projection Health Monitoring]
- [Source: _bmad-output/planning-artifacts/architecture.md#Infrastructure & Deployment]
- [Source: _bmad-output/planning-artifacts/architecture.md#Authentication & Security]
- [Source: deploy/dapr/accesscontrol.yaml]
- [Source: deploy/dapr/resiliency.yaml]
- [Source: deploy/dapr/subscription-parties.yaml]
- [Source: deploy/dapr/pubsub-kafka.yaml]
- [Source: deploy/dapr/pubsub-rabbitmq.yaml]
- [Source: deploy/dapr/pubsub-servicebus.yaml]
- [Source: src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.yaml]
- [Source: src/Hexalith.Parties.AppHost/DaprComponents/statestore.yaml]
- [Source: _bmad-output/implementation-artifacts/7-3-handler-patterns-documentation-and-dangling-reference-guidance.md]

## Change Log

- **2026-03-06**: Implemented Story 8.1 — Deployment Validation Tooling. Created production state store configs (CosmosDB, PostgreSQL), deployment validation PowerShell script with 6 check categories (38 checks for production configs), security checklist and deployment guide documentation, and 11 xUnit tests validating the tool. Full regression suite passes (390 tests, 0 failures).
- **2026-03-06**: Addressed senior code review findings. Updated `validate-deployment.ps1` to support documented `--config-path`/`--output` CLI syntax, distinguish local self-hosted development profiles from production failures, reject wildcard access-control app-ids, and verify resiliency targets are wired for pub/sub and state store. Expanded deployment validation coverage to 14 tests. Production configs now pass 39/40 checks (1 advisory warning for missing secret store), and local AppHost DAPR configs pass with warnings.

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- Initial PowerShell script failed on Windows PowerShell 5.1 due to `.Count` on `Where-Object` results in strict mode. Fixed by wrapping in `@()` array subexpression and using `ArrayList` instead of `List<T>` with custom class.
- Senior review follow-up fixed documented GNU-style CLI argument support, local-dev warning behavior, wildcard `appId` rejection, and missing resiliency target detection.

### Completion Notes List

- **Task 1**: Created `statestore-cosmosdb.yaml` and `statestore-postgresql.yaml` following existing deploy config patterns (env-var references, actorStateStore, commandapi-only scopes, extensive header comments including D5 entry size limit documentation).
- **Task 2**: Created `validate-deployment.ps1` with 6 validation categories: Access Control (file exists, defaultAction deny, trust domain, explicit app-id policies, wildcard rejection), State Store (exists, actorStateStore, commandapi-only scopes, env-var connection), Pub/Sub (scopes, publishingScopes, subscriptionScopes, dead-letter, env-var connection, local-dev warnings), Subscription (v2alpha1, dead-letter, env-var scopes, local-dev warnings), Resiliency (exists, circuit breakers, exponential retry, pub/sub and state store target wiring), Secret Store (advisory warning). Supports console and JSON output, documented GNU-style CLI syntax, and CI-friendly exit codes. Production configs pass 39/40 (1 advisory warning for missing secret store); local AppHost DAPR configs pass with warnings.
- **Task 3**: Created `docs/deployment-security-checklist.md` with pre-deployment checklist, per-broker notes, state store backend notes (D5), tenant provisioning checklist, v1.1 preparation, network security/IAM operator scope, and validation tool reference.
- **Task 4**: Created `docs/deployment-guide.md` with broker/state store selection guidance, environment variables, multi-tenant setup, backup strategy (crypto-shredding), CI/CD integration, and troubleshooting.
- **Task 5**: Created 14 xUnit/Shouldly tests: valid config passes, missing accesscontrol fails, defaultAction allow fails, hardcoded connection string fails, missing scopes fails, missing dead-letter fails, subscriber publishing fails, missing resiliency file fails, JSON output valid, GNU-style CLI arguments work, local dev config passes with warnings, wildcard `appId` fails, missing component resiliency targets fail, and secret store warning remains advisory. All 14 pass.

### File List

- `deploy/dapr/statestore-cosmosdb.yaml` (NEW)
- `deploy/dapr/statestore-postgresql.yaml` (NEW)
- `deploy/validate-deployment.ps1` (NEW)
- `docs/deployment-security-checklist.md` (NEW)
- `docs/deployment-guide.md` (NEW)
- `tests/Hexalith.Parties.DeployValidation.Tests/Hexalith.Parties.DeployValidation.Tests.csproj` (NEW)
- `tests/Hexalith.Parties.DeployValidation.Tests/DeploymentValidationTests.cs` (NEW)
- `Hexalith.Parties.slnx` (MODIFIED — added DeployValidation.Tests project)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (MODIFIED — status updates)
- `_bmad-output/implementation-artifacts/8-1-deployment-validation-tooling.md` (MODIFIED — task checkboxes, dev record, status)

## Senior Developer Review (AI)

### Reviewer

GitHub Copilot

### Review Date

2026-03-06

### Outcome

Approved after fixes.

### Findings Resolved

- Fixed the documented CLI contract so `--config-path` and `--output` work as written in the operator docs.
- Updated validation behavior so the checked-in AppHost local development DAPR profile passes with warnings instead of failing.
- Strengthened access-control validation to reject wildcard `appId` entries and require explicit callers.
- Strengthened resiliency validation to require pub/sub and state store target wiring, not just the presence of generic policies.
- Added automated regression coverage for all resolved review findings.
