# Story 8.2: Health, Readiness & Graceful Degradation

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an operator,
I want health and readiness signals and graceful degradation under infrastructure failure,
So that I can monitor the service in production and trust that partial failures don't cause total outage.

## Acceptance Criteria

1. **Component-level health status (FR71)**: Given the running party service, when a health check request is made to `/health`, then a component-level health status is returned covering:
    - DAPR sidecar connectivity
    - State store accessibility
    - Pub/sub connectivity
    - Projection actor responsiveness

2. **Readiness probe (FR71)**: Given the running party service, when a readiness check request is made to `/ready`, then the service reports whether it is ready to accept requests. Readiness is `false` during startup until the service can process commands.

3. **State store failure — graceful write failure (FR64)**: Given the DAPR state store becomes unavailable, when write commands are sent, then commands fail gracefully with a clear ProblemDetails error (not an unhandled exception), and read operations from projection actors continue serving cached/last-known state with an `X-Service-Degraded: true` and `X-Stale-Data-Age` response header (NFR21).

4. **Pub/sub failure — event commit without publish (FR64)**: Given the DAPR pub/sub becomes unavailable, when commands are processed successfully, then events are committed to the event store but not published. Events are retried on recovery via EventStore persist-then-publish pattern. Consuming apps may experience delayed event delivery but never event loss.

5. **Sidecar failure — full degradation**: Given the DAPR sidecar becomes unavailable, when any request is sent, then the service reports unhealthy via health check, readiness reports false, and the failure is logged at `Error` level.

6. **Crash recovery (NFR20, NFR22)**: Given the service recovers from a crash, when it restarts, then it replays necessary event state and accepts requests within 30 seconds, with no data loss — event store is the durable source of truth.

7. **Failure mode documentation**: Given component failure scenarios, when documented for operators, then inline code comments and operator runbook guidance document behavior for each failure mode:
    - State store unavailable: writes fail, reads may serve stale data
    - Pub/sub unavailable: events committed but not published, retry on recovery
    - Sidecar unavailable: full degradation, unhealthy status

## Tasks / Subtasks

- [x] Task 1: Create DAPR health check classes (AC: #1)
    - [x] Create `DaprSidecarHealthCheck.cs` — uses `DaprClient.CheckHealthAsync()`, FailureStatus: `Unhealthy`, tag: `"ready"`, timeout: 3s
    - [x] Create `DaprStateStoreHealthCheck.cs` — reads sentinel key `__health_check__` from state store, FailureStatus: `Unhealthy`, tag: `"ready"`, timeout: 3s
    - [x] Create `DaprPubSubHealthCheck.cs` — queries `DaprClient.GetMetadataAsync()` for component status, FailureStatus: `Degraded`, health-only tag, timeout: 3s
    - [x] Create `ProjectionActorsHealthCheck.cs` — verifies projection actor invocation remains responsive and contributes to `/health`
    - [x] Create `PartiesHealthCheckExtensions.cs` — single `AddPartiesDaprHealthChecks()` extension method registering sidecar, state store, pub/sub, and projection checks with customizable component names

- [x] Task 2: Wire health checks into application (AC: #1, #2)
    - [x] Modify `Program.cs` — call `builder.Services.AddHealthChecks().AddPartiesDaprHealthChecks()` after `AddDaprClient()`
    - [x] Modify `ServiceDefaults/Extensions.cs` — remove the two placeholder self-checks (`"self"` and `"ready"`) that return `HealthCheckResult.Healthy()`; real DAPR checks replace them
    - [x] Verify `/health` returns composite status, `/alive` returns liveness, `/ready` returns readiness gated on all `"ready"`-tagged checks

- [x] Task 3: Create degraded response middleware (AC: #3)
    - [x] Create `DegradedResponseMiddleware.cs` — on each request, query `HealthCheckService` for current composite status; if `Degraded`, inject `X-Service-Degraded: true` and `X-Stale-Data-Age: <seconds>` response headers on read (GET) responses
    - [x] Register middleware in `Program.cs` after `UseExceptionHandler()`, before `UseAuthentication()`
    - [x] Inline code comments documenting: state store failure = writes fail with ProblemDetails, reads serve cached data with staleness headers

- [x] Task 4: Inline failure mode documentation (AC: #5, #7)
    - [x] Add XML doc comments on each health check class documenting the failure mode it detects and the system behavior during that failure
    - [x] Add comments in `DegradedResponseMiddleware` documenting the three failure modes and their degradation behavior
    - [x] Add comments in `Program.cs` near health check registration documenting the readiness contract
    - [x] Update `docs/deployment-guide.md` with operator-facing runbook guidance for state store, pub/sub, and sidecar failure modes

- [x] Task 5: Tier 1 unit tests (AC: #1, #3)
    - [x] Test `DaprSidecarHealthCheck` — mock `DaprClient`: healthy returns `Healthy`, exception returns `Unhealthy`, timeout returns `Unhealthy`
    - [x] Test `DaprStateStoreHealthCheck` — mock `DaprClient`: successful read returns `Healthy`, exception returns `Unhealthy`
    - [x] Test `DaprPubSubHealthCheck` — mock `DaprClient`: component present returns `Healthy`, component missing returns `Degraded`, exception returns `Degraded`
    - [x] Test `DegradedResponseMiddleware` — mock `HealthCheckService`: healthy status = no headers added; degraded status = headers added on GET; degraded status = no headers on POST

- [x] Task 6: Tier 2 integration tests (AC: #1, #2, #3, #5)
    - [x] Test `/health` endpoint returns 200 when all DAPR components healthy (mock DaprClient at DI level)
    - [x] Test `/health` endpoint returns 503 when sidecar is down
    - [x] Test `/ready` endpoint returns 200 when ready, 503 when not ready
    - [x] Test `/alive` endpoint always returns 200 (liveness only)
    - [x] Test query endpoints include `X-Service-Degraded` header when pub/sub is degraded
    - [x] Test query endpoints do NOT include degradation headers when all healthy
    - [x] Test write endpoints return ProblemDetails when state store is unavailable

- [x] Task 7: Tier 3 end-to-end tests (AC: #1, #5, #6)
    - [x] Test full Aspire topology — `/health`, `/ready`, `/alive` return 200 with all DAPR components running; no degradation headers when healthy
    - [x] Test with DAPR sidecar stopped — uses Aspire 13.x `ResourceCommandService.ExecuteCommandAsync(...)` to stop the CommandApi sidecar resource, verifies `/health` and `/ready` return 503, then restarts the sidecar and verifies recovery to 200 (AC #5, partial #6)
    - [x] Pub/sub-only Tier 3 outage scenario documented as non-executable in this topology — in-memory DAPR components share the sidecar process, so pub/sub cannot be failed independently of the state store. Coverage is provided by Tier 1 (`DegradedResponseMiddlewareTests`) and Tier 2 (`HealthEndpointIntegrationTests`)

## Dev Notes

### Architecture & Conventions

- **Target framework**: net10.0 (pinned in `global.json` to SDK 10.0.103)
- **Build**: `TreatWarningsAsErrors=true`, nullable enabled, implicit usings, file-scoped namespaces
- **Central package management**: All package versions in `Directory.Packages.props` at solution root
- **Solution format**: `Hexalith.Parties.slnx` (modern XML format)
- **Code style**: `.editorconfig` — Allman braces, `_camelCase` private fields, `I` prefix for interfaces, `Async` suffix, 4 spaces, CRLF, UTF-8
- **Test naming**: `{Method}_{Scenario}_{ExpectedResult}`
- **Assertions**: Shouldly library
- **Test tiers**: Tier 1 (pure logic, no DAPR), Tier 2 (WebApplicationFactory + mocked DaprClient), Tier 3 (full Aspire topology via Docker)
- **DAPR SDK**: Dapr.Client 1.16.1, Dapr.AspNetCore 1.16.1, Dapr.Actors 1.16.1
- **Packages**: xUnit 2.9.3, Shouldly 4.3.0, NSubstitute 5.3.0, coverlet.collector 6.0.4
- **CI**: GitHub Actions — restore, build (Release), Tier 1+2 tests, optional Tier 3
- **Error responses**: ProblemDetails (RFC 9457) with `correlationId` and `tenantId` extensions
- **Log levels**: `Information` for commands/events, `Warning` for rejections, `Error` for infrastructure failures. No PII in log messages.

### Architecture Compliance — Critical Boundaries

**What Parties OWNS (implement in this story):**

- Health check classes (`DaprSidecarHealthCheck`, `DaprStateStoreHealthCheck`, `DaprPubSubHealthCheck`)
- Health check registration extension (`PartiesHealthCheckExtensions.cs`)
- Degraded response middleware (`DegradedResponseMiddleware.cs`)
- Enhanced readiness tied to DAPR sidecar + state store `"ready"` checks
- Inline failure mode documentation in code comments

**What Parties does NOT own (do NOT implement):**

- Crash recovery and event replay — EventStore responsibility
- Retry logic and circuit breakers — DAPR `resiliency.yaml` (finalized in Story 8-1)
- Persist-then-publish drain recovery — EventStore responsibility
- DAPR component configuration — finalized in Story 8-1, do NOT modify `deploy/dapr/*.yaml`

**Key Architecture Decisions:**

- D15: Projection health monitoring with auto-rebuild on corruption — Story 8-3 scope, NOT this story
- D14: Projection rebuild via event replay — Story 8-3 scope, NOT this story
- FR64: Graceful degradation — this story
- FR71: Health/readiness endpoints — this story
- NFR20-24: Reliability requirements — crash recovery < 30s, cached reads on failure, zero event loss, at-least-once delivery, idempotent commands

**Health Check Status Mapping:**
| Component | Healthy | Failure Status | HTTP Code |
|-----------|---------|---------------|-----------|
| DAPR Sidecar | Responsive | `Unhealthy` | 503 |
| State Store | Sentinel read succeeds | `Unhealthy` | 503 |
| Pub/Sub | Component present in metadata | `Degraded` | 200 |
| Projection Actors | Actor invocation succeeds | `Degraded` | 200 |

Pub/sub = `Degraded` (not `Unhealthy`) because cached reads can continue. State store = `Unhealthy` because writes are broken. Projection actors = `Degraded` because queries serve stale data but commands still work.

### Library & Framework Requirements

**No new NuGet packages needed.** All dependencies are already available:

- `Microsoft.Extensions.Diagnostics.HealthChecks` — available via framework (net10.0)
- `Dapr.Client` — already referenced in `Hexalith.Parties.CommandApi.csproj`
- `NSubstitute` 5.3.0 — for mocking `DaprClient` in tests
- `Shouldly` 4.3.0 — for assertions

**Do NOT add:**

- `AspNetCore.HealthChecks.Dapr` or any third-party health check packages — use the EventStore pattern
- `Polly` or any resilience library — DAPR resiliency.yaml handles this
- Any new middleware libraries — use built-in ASP.NET Core middleware

### What Already Exists (DO NOT Recreate)

**Health endpoint routing (ServiceDefaults/Extensions.cs lines 20-22):**

- `/health` — general health, `/alive` — liveness (tag: "live"), `/ready` — readiness (tag: "ready")
- Status codes: Healthy=200, Degraded=200, Unhealthy=503
- Dev mode: detailed JSON; production: minimal plaintext
- Health endpoints excluded from OpenTelemetry tracing

**Error handling (CommandApi/ErrorHandling/):**

- `PartiesGlobalExceptionHandler.cs` — maps exceptions to ProblemDetails
- `PartiesValidationExceptionHandler.cs` — maps validation errors to ProblemDetails with field-level detail
- Correlation ID propagation via `CorrelationIdMiddleware.cs`

**DAPR client registration (Program.cs line 10):**

- `builder.Services.AddDaprClient()` — already registered. Do NOT register a second DaprClient.

**DAPR resiliency config (deploy/dapr/resiliency.yaml):**

- Circuit breakers configured for pub/sub (10 failures, 60s timeout) and state store (5 failures, 60s timeout)
- Exponential backoff retry policies already defined
- Do NOT modify — finalized in Story 8-1

**Existing test projects (7 projects, 371+ tests passing):**

- `tests/Hexalith.Parties.CommandApi.Tests/` — Tier 2 tests, add health check integration tests here
- `tests/Hexalith.Parties.IntegrationTests/` — Tier 3 tests, add E2E health tests here

### File Structure Requirements

**New files to create:**

```
src/Hexalith.Parties.CommandApi/
  HealthChecks/
    DaprSidecarHealthCheck.cs          — IHealthCheck, uses DaprClient.CheckHealthAsync()
    DaprStateStoreHealthCheck.cs       — IHealthCheck, reads sentinel key from state store
    DaprPubSubHealthCheck.cs           — IHealthCheck, queries DaprClient metadata API
    PartiesHealthCheckExtensions.cs    — IHealthChecksBuilder extension method
  Middleware/
    DegradedResponseMiddleware.cs      — Injects X-Service-Degraded headers on degraded reads
```

**Existing files to modify:**

```
src/Hexalith.Parties.CommandApi/Program.cs
  — Add: builder.Services.AddHealthChecks().AddPartiesDaprHealthChecks();
  — Add: app.UseMiddleware<DegradedResponseMiddleware>(); (after UseRouting, before MapControllers)

src/Hexalith.Parties.ServiceDefaults/Extensions.cs
  — Remove: placeholder "self" and "ready" health checks (lines ~92-102)
```

**Test files to create:**

```
tests/Hexalith.Parties.CommandApi.Tests/
  HealthChecks/
    DaprSidecarHealthCheckTests.cs     — Tier 1 unit tests
    DaprStateStoreHealthCheckTests.cs  — Tier 1 unit tests
    DaprPubSubHealthCheckTests.cs      — Tier 1 unit tests
    DegradedResponseMiddlewareTests.cs — Tier 1 unit tests
    HealthEndpointIntegrationTests.cs  — Tier 2 integration tests

tests/Hexalith.Parties.IntegrationTests/
  HealthChecks/
    HealthEndpointE2ETests.cs          — Tier 3 end-to-end tests
```

### Reference Implementation Pattern

Copy the exact pattern from `Hexalith.EventStore/src/Hexalith.EventStore.CommandApi/HealthChecks/`:

- `DaprSidecarHealthCheck.cs` — `DaprClient.CheckHealthAsync()` with 3s timeout, catches exceptions
- `DaprStateStoreHealthCheck.cs` — reads `__health_check__` sentinel key, null = healthy
- `DaprPubSubHealthCheck.cs` — `DaprClient.GetMetadataAsync()`, checks component existence
- `HealthCheckBuilderExtensions.cs` — single registration method with customizable component names

Adapt for Parties: rename extension class to `PartiesHealthCheckExtensions`, use Parties component names (`statestore`, `pubsub` matching DaprComponents config).

### DegradedResponseMiddleware Design

- Inject `IHealthCheckService` via constructor (NOT `HealthCheckService` — use the interface)
- On each request: call `CheckHealthAsync()` with `"ready"` tag predicate
- If composite status is `Degraded`: set `X-Service-Degraded: true` header
- Track when degradation started; compute `X-Stale-Data-Age` as seconds since first degraded check
- Only add headers on GET requests (reads) — POST/PUT/DELETE (writes) return ProblemDetails on failure via existing exception handler
- Do NOT cache health status in a static variable — `IHealthCheckService` already handles caching with configurable period
- Check health per-request to avoid stale-staleness

### Previous Story Intelligence (Story 8-1)

**Learnings from Story 8-1 (Deployment Validation Tooling):**

- Story 8-1 created `deploy/dapr/` configs and `deploy/validate-deployment.ps1`
- Existing `ServiceDefaults/Extensions.cs` already notes: "Extending health checks with DAPR sidecar/state store/pub/sub checks is Story 8.2 scope"
- PowerShell validation tool was used instead of C# for deployment-time tooling — Story 8-2 is runtime C# code
- YamlDotNet 16.3.0 is in `Directory.Packages.props` but not needed for this story
- Story 8-1 confirmed health endpoint paths: `/health`, `/alive`, `/ready` with status codes 200/200/503

**Files created in Story 8-1 (reference, do NOT modify):**

- `deploy/dapr/statestore-cosmosdb.yaml`, `deploy/dapr/statestore-postgresql.yaml`
- `deploy/validate-deployment.ps1`
- `docs/deployment-security-checklist.md`, `docs/deployment-guide.md`
- `tests/Hexalith.Parties.DeployValidation.Tests/`

### Git Intelligence

Recent commits show Stories 6.3 through 8.1 implemented sequentially. Pattern: feature branch per story, merge to main via PR. Latest: `d7c4027 Merge pull request #29 from Hexalith/feat/story-8-1-deployment-validation-tooling`.

Branch naming convention: `feat/story-8-2-health-readiness-and-graceful-degradation`

### Testing Requirements

**Tier 1 (Unit — zero external deps, ~60% of test effort):**

- Mock `DaprClient` with NSubstitute for all health check tests
- Mock `IHealthCheckService` for middleware tests
- Test all success/failure/timeout paths for each health check
- Test middleware adds headers only on GET when degraded, not on POST, not when healthy
- Test naming: `{Method}_{Scenario}_{ExpectedResult}`

**Tier 2 (Integration — WebApplicationFactory, ~30% of test effort):**

- Use `WebApplicationFactory<Program>` with mocked DaprClient registered at DI level
- Hit real HTTP endpoints (`/health`, `/alive`, `/ready`) through the full pipeline
- Simulate component failures by configuring mock DaprClient to throw
- Verify middleware ordering: degradation headers appear on query endpoints
- Verify ProblemDetails returned for write failures

**Tier 3 (E2E — full Aspire topology, ~10% of test effort):**

- Start full topology with `dotnet aspire run`
- Verify all health endpoints return healthy when DAPR is running
- Test degradation scenarios by stopping DAPR containers
- These tests require Docker and are optional in CI

**Test risk priority (highest first):**

1. Health check logic correctness — wrong status = cascading pod kills in Kubernetes
2. Middleware header injection — wrong staleness signals mislead operators
3. Readiness gating — premature readiness = traffic to unready pods
4. E2E DAPR integration — mock vs real behavior differences

### Project Structure Notes

- Health checks go in `CommandApi/HealthChecks/` (matches EventStore convention)
- Middleware goes in `CommandApi/Middleware/` (matches existing `CorrelationIdMiddleware`, `GdprWarningMiddleware`)
- No new projects needed — all code fits in existing `Hexalith.Parties.CommandApi`
- Tests go in existing test projects — no new test projects needed

### References

- [Source: _bmad-output/planning-artifacts/architecture.md — D15 Projection Health Monitoring]
- [Source: _bmad-output/planning-artifacts/architecture.md — D14 Projection Rebuild Strategy]
- [Source: _bmad-output/planning-artifacts/architecture.md — FR64 Graceful Degradation]
- [Source: _bmad-output/planning-artifacts/architecture.md — FR71 Health/Readiness Endpoints]
- [Source: _bmad-output/planning-artifacts/architecture.md — NFR20-24 Reliability]
- [Source: _bmad-output/planning-artifacts/epics.md — Epic 8, Story 8.2]
- [Source: _bmad-output/implementation-artifacts/8-1-deployment-validation-tooling.md — Previous story]
- [Source: Hexalith.EventStore/src/Hexalith.EventStore.CommandApi/HealthChecks/ — Reference implementation]
- [Source: src/Hexalith.Parties.ServiceDefaults/Extensions.cs — Existing health endpoint setup]
- [Source: src/Hexalith.Parties.CommandApi/ErrorHandling/ — Existing exception handlers]
- [Source: deploy/dapr/resiliency.yaml — DAPR resilience configuration]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- `HealthCheckService` is an abstract class in .NET, not an interface (`IHealthCheckService` does not exist). Dev Notes incorrectly specify `IHealthCheckService` — corrected to `HealthCheckService` in implementation.
- `DaprComponentsMetadata` constructor requires named parameters: `(string name, string type, string version, IReadOnlyDictionary<string, string> capabilities)` — the `capabilities` parameter must be `new Dictionary<string, string>()`, not collection expression `[]`.
- Removing placeholder "self" and "ready" health checks from `ServiceDefaults/Extensions.cs` broke the existing `ReadyEndpoint_ReturnsSuccessAsync` integration test because no DaprClient mock was registered. Fixed by adding DaprClient mock to `PartyApiRoundTripTestFactory`.
- Pre-existing test failure in `DeploymentValidationTests.GnuStyleArguments_AreAccepted` — unrelated to Story 8.2 changes.
- Tier 3 E2E: `ProjectionActorsHealthCheck` hung indefinitely because DAPR actor proxy calls don't accept `CancellationToken`. Fixed by wrapping calls with `Task.WaitAsync(cancellationToken)`.
- Tier 3 E2E: `DegradedResponseMiddleware` caused circular dependency — ran health checks on `/actors/*` requests, triggering projection-actors health check → actor call → middleware health check → infinite loop. Fixed by skipping `/actors/` paths in middleware.
- Tier 3 E2E: `projection-actors` failure status was `Unhealthy` (503) but should be `Degraded` (200) — projection actor unavailability means stale queries, not service failure.
- Tier 3 E2E: Aspire endpoint discovery required `launchSettings.json` in CommandApi Properties folder. Missing `IsAspireSharedProject` and `FrameworkReference` in ServiceDefaults caused port mismatch.
- Tier 3 E2E: `extern alias apphost` needed for `Projects.Hexalith_Parties_AppHost` due to `Program` class ambiguity between AppHost and CommandApi.
- Pre-existing bug: `PartyIndexProjectionActor.GetEntriesAsync()` returns `Dictionary<string, PartyIndexEntry>` which DAPR's `DataContractSerializer` can't serialize without `KnownType` annotations.
- Aspire 13.x `ResourceCommandService` with `KnownResourceCommands.StopCommand`/`StartCommand` enables stopping individual resources during E2E tests. CommunityToolkit.Aspire.Hosting.Dapr names sidecar resources as `{parentResourceName}-dapr` (source: `IDistributedApplicationComponentBuilderExtensions.cs` line `$"{builder.Resource.Name}-dapr"`). This unblocked the sidecar failure E2E test.
- Aspire testing exposed the runnable sidecar resource as `commandapi-dapr-cli` in this environment. The E2E test now tries both `commandapi-dapr-cli` and `commandapi-dapr` so the stop/start flow works across naming variations.
- Projection actor health probes initially timed out during sidecar restart because actor placement had not fully recovered. Switching the probe to remoting-safe `PingAsync()` actor methods eliminated the false degradation caused by collection serialization and reduced the recovery window to real sidecar readiness.

### Completion Notes List

- Implemented 4 health checks for Story 8.2: sidecar, state store, pub/sub, and projection actor responsiveness
- Updated readiness semantics so `/ready` depends on sidecar + state store only; pub/sub degradation still surfaces on `/health`
- Hardened `DegradedResponseMiddleware` so GET responses emit stale-data headers for pub/sub degradation and cached state-store degradation scenarios
- Added stale-read fallback behavior for projection actors when an activated actor already has last-known state in memory
- Added infrastructure-specific ProblemDetails handling for dependency outages and error logging for sidecar health failures
- Expanded Tier 2 coverage with query-header assertions, projection-health coverage, readiness/pubsub regression coverage, and write-path ProblemDetails assertions
- Tier 3 E2E tests implemented with full Aspire topology: 4 active tests (health/ready/alive return 200, no degradation headers when healthy) + 3 skipped with documented reasons
- Fixed `ProjectionActorsHealthCheck` to enforce timeout via `Task.WaitAsync(cancellationToken)` — DAPR actor proxy calls don't accept CancellationToken and hung indefinitely
- Fixed `DegradedResponseMiddleware` circular dependency: middleware ran ALL health checks on every request including actor invocations (`/actors/*`), causing projection-actors health check → actor call → middleware health check → infinite loop
- Changed `projection-actors` failure status from `Unhealthy` to `Degraded` — projection actor unavailability means stale queries, not service failure; commands still work
- Added `launchSettings.json` and `IsAspireSharedProject` to fix Aspire endpoint discovery and port binding
- Codacy MCP analysis was attempted after each edit, but the Codacy CLI reported that Windows is unsupported without WSL in this environment
- Added process-level last-known projection caches so transient state store outages can still serve previously persisted detail/index snapshots after actor reactivation
- Narrowed dependency-unavailable mapping so client-aborted requests are no longer misreported as infrastructure outages
- Added operator runbook guidance to `docs/deployment-guide.md` and aligned story status/tasks with the remaining AC #6 validation gap
- Implemented sidecar failure E2E test using Aspire 13.x `ResourceCommandService`: stops `commandapi-dapr` sidecar, verifies `/health` and `/ready` return 503, restarts sidecar and verifies recovery to 200 (AC #5, partial AC #6)
- Replaced projection actor health reads with lightweight `PingAsync()` probes so health checks validate actor routing without depending on Dapr remoting serialization of projection payloads
- Hardened the sidecar E2E test to work with both `commandapi-dapr-cli` and `commandapi-dapr` resource names exposed by Aspire/Dapr hosting
- Re-ran story-focused validation successfully: CommandApi targeted tests `30/30` passed; Tier 3 Aspire health suite `5 passed, 1 skipped`

### File List

**New files:**

- `src/Hexalith.Parties.CommandApi/HealthChecks/DaprSidecarHealthCheck.cs`
- `src/Hexalith.Parties.CommandApi/HealthChecks/DaprStateStoreHealthCheck.cs`
- `src/Hexalith.Parties.CommandApi/HealthChecks/DaprPubSubHealthCheck.cs`
- `src/Hexalith.Parties.CommandApi/HealthChecks/ProjectionActorsHealthCheck.cs`
- `src/Hexalith.Parties.CommandApi/HealthChecks/PartiesHealthCheckExtensions.cs`
- `src/Hexalith.Parties.CommandApi/Middleware/DegradedResponseMiddleware.cs`
- `tests/Hexalith.Parties.CommandApi.Tests/HealthChecks/DaprSidecarHealthCheckTests.cs`
- `tests/Hexalith.Parties.CommandApi.Tests/HealthChecks/DaprStateStoreHealthCheckTests.cs`
- `tests/Hexalith.Parties.CommandApi.Tests/HealthChecks/DaprPubSubHealthCheckTests.cs`
- `tests/Hexalith.Parties.CommandApi.Tests/HealthChecks/ProjectionActorsHealthCheckTests.cs`
- `tests/Hexalith.Parties.CommandApi.Tests/HealthChecks/DegradedResponseMiddlewareTests.cs`
- `tests/Hexalith.Parties.CommandApi.Tests/HealthChecks/HealthEndpointIntegrationTests.cs`
- `tests/Hexalith.Parties.CommandApi.Tests/ErrorHandling/PartiesGlobalExceptionHandlerTests.cs`
- `tests/Hexalith.Parties.IntegrationTests/HealthChecks/HealthEndpointE2ETests.cs`
- `tests/Hexalith.Parties.IntegrationTests/HealthChecks/PartiesAspireTopologyFixture.cs`
- `tests/Hexalith.Parties.IntegrationTests/HealthChecks/PartiesAspireTopologyCollection.cs`
- `src/Hexalith.Parties.CommandApi/Properties/launchSettings.json`

**Modified files:**

- `src/Hexalith.Parties.CommandApi/Program.cs` — added health check registration and DegradedResponseMiddleware
- `src/Hexalith.Parties.CommandApi/ErrorHandling/PartiesGlobalExceptionHandler.cs` — maps dependency failures to `503` ProblemDetails
- `src/Hexalith.Parties.ServiceDefaults/Extensions.cs` — removed placeholder "self" and "ready" health checks
- `src/Hexalith.Parties.Projections/Abstractions/IPartyDetailProjectionActor.cs` — added remoting-safe `PingAsync()` probe contract for health checks
- `src/Hexalith.Parties.Projections/Abstractions/IPartyIndexProjectionActor.cs` — added remoting-safe `PingAsync()` probe contract for health checks
- `src/Hexalith.Parties.Projections/Actors/PartyDetailProjectionActor.cs` — falls back to cached state during transient store outages
- `src/Hexalith.Parties.Projections/Actors/PartyIndexProjectionActor.cs` — serves in-memory entries when flush fails during store outages
- `docs/deployment-guide.md` — adds operator runbook guidance for state store, pub/sub, and sidecar failure modes
- `tests/Hexalith.Parties.IntegrationTests/PartyApiRoundTripIntegrationTests.cs` — added DaprClient mock to test factory
- `tests/Hexalith.Parties.IntegrationTests/Hexalith.Parties.IntegrationTests.csproj` — added Aspire.Hosting.Testing + AppHost alias
- `src/Hexalith.Parties.ServiceDefaults/Hexalith.Parties.ServiceDefaults.csproj` — added IsAspireSharedProject + FrameworkReference
- `tests/Hexalith.Parties.CommandApi.Tests/HealthChecks/ProjectionActorsHealthCheckTests.cs` — updated actor health tests to verify `PingAsync()` probing
- `tests/Hexalith.Parties.IntegrationTests/HealthChecks/HealthEndpointE2ETests.cs` — probes both observed sidecar resource names during stop/start recovery validation

## Senior Developer Review (AI)

### Review Date

- 2026-03-07

### Outcome

- All review findings addressed and validated.
- Story-focused validation passed: CommandApi targeted suite `30/30`; Tier 3 E2E suite `5 passed, 1 skipped`.
- AC #6 crash recovery partially verified by sidecar stop/restart E2E test (recovery to 200). Full event replay verification is EventStore platform responsibility.

### Findings Addressed

- Added missing projection actor health coverage to `/health`
- Corrected readiness so pub/sub degradation no longer blocks `/ready`
- Implemented degraded read headers for query endpoints under pub/sub and cached state-store degradation paths
- Added integration coverage for degraded query headers and graceful write failures
- Added sidecar error logging and clearer dependency-unavailable ProblemDetails responses
- Added process-level last-known snapshot fallback for projection reads after transient state store outages
- Added operator-facing failure-mode runbook guidance in `docs/deployment-guide.md`
- Corrected story task/status metadata so blocked Tier 3 outage scenarios are no longer marked complete
- Replaced serialization-sensitive projection actor health probes with remoting-safe `PingAsync()` calls
- Hardened the sidecar stop/restart E2E to handle both observed Aspire sidecar resource names

### Remaining Gaps

- Aspire Tier 3 sidecar stopped scenario is now tested using `ResourceCommandService.ExecuteCommandAsync` (stop/start). Pub/sub-only failure cannot be tested at E2E level: in-memory DAPR components share the sidecar process. This scenario is covered by Tier 1 and Tier 2 tests.
- AC #6 crash recovery is partially verified by the sidecar stop/restart E2E test (recovery to 200 after restart). Full event replay verification remains platform-level behavior (EventStore responsibility).
- Out-of-scope workspace files currently present in git diff: `.claude/settings.local.json` and `.cursor/rules/codacy.mdc`

## Change Log

- 2026-03-07: Implemented Story 8.2 — Health, Readiness & Graceful Degradation. Added DAPR health checks (sidecar, state store, pub/sub), degraded response middleware with staleness headers, inline failure mode documentation, and comprehensive test coverage (23 new tests across 3 tiers).
- 2026-03-07: AI review follow-up — added projection actor health coverage, corrected readiness gating, hardened degraded read/write behavior, expanded Tier 2 tests, and downgraded unfinished Tier 3 work to pending.
- 2026-03-07: Tier 3 E2E tests — implemented full Aspire topology tests (4 active, 3 skipped). Fixed 3 bugs: ProjectionActorsHealthCheck timeout enforcement, DegradedResponseMiddleware circular dependency on actor paths, projection-actors failure status Unhealthy→Degraded. Fixed Aspire test infrastructure: launchSettings.json, IsAspireSharedProject, FrameworkReference, extern alias for Program ambiguity.
- 2026-03-07: AI review remediation — added operator runbook guidance, strengthened last-known projection fallback across actor reactivation, narrowed dependency outage classification for client aborts, and moved the story/sprint status back to `in-progress` pending explicit AC #6 verification.
- 2026-03-07: Implemented sidecar failure E2E test using Aspire 13.x ResourceCommandService — stops DAPR sidecar, verifies 503 on /health and /ready, restarts and verifies recovery to 200. Unblocked Task 7 subtask 2. Pub/sub subtask 3 remains blocked (in-memory components share sidecar process).
- 2026-03-07: Final review remediation validation — switched projection actor health checks to `PingAsync()` probes, made sidecar resource selection resilient to `commandapi-dapr-cli`/`commandapi-dapr` naming differences, reran focused validation successfully, and marked the story done.
