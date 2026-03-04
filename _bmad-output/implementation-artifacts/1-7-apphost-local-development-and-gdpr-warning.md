# Story 1.7: AppHost, Local Development & GDPR Warning

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer,
I want to run the full party service locally with a single command and see a clear GDPR compliance warning,
So that I can develop and evaluate the service easily while being aware of compliance limitations.

## Acceptance Criteria

1. **Given** a developer with .NET 10 SDK and Docker installed, **When** they run `dotnet aspire run` on the AppHost project, **Then** the following components start: Hexalith.Parties.CommandApi with DAPR sidecar, Redis (state store + pub/sub) for local development, and Aspire dashboard for observability. **And** the service accepts requests within 30 seconds of container launch (NFR5).

2. **Given** the AppHost project, **When** reviewed for DAPR configuration, **Then** the following DAPR component files exist under `DaprComponents/`: `statestore.yaml` (Redis for local dev), `pubsub.yaml` (Redis Streams for local dev), `subscription-parties.yaml` (event subscription configuration). **And** `launchSettings.json` exists with appropriate profiles.

3. **Given** the ServiceDefaults project, **When** reviewed for configuration, **Then** OpenTelemetry tracing and structured logging are configured. **And** health check endpoints (`/health`, `/ready`) are registered. **And** resilience patterns are configured.

4. **Given** a running instance of the party service, **When** any API response is returned, **Then** a non-dismissable GDPR compliance warning header is included (FR62). **And** the warning states that MVP does not include GDPR compliance features and regulated EU personal data should not be stored.

5. **Given** service startup, **When** the service initializes, **Then** the GDPR compliance warning is logged at `Warning` level in startup logs (FR62).

6. **Given** the full local development setup, **When** a developer follows the sequence: start service -> POST CreateParty -> GET party by ID, **Then** the round-trip works end-to-end (FR60). **And** the party is created via the aggregate actor and retrievable via the GET endpoint.

## Tasks / Subtasks

- [x] Task 1: Implement ServiceDefaults `Extensions.cs` (AC: #3)
  - [x] 1.1: Create `src/Hexalith.Parties.ServiceDefaults/Extensions.cs` — follow EventStore pattern exactly
  - [x] 1.2: `AddServiceDefaults<TBuilder>()` — OpenTelemetry, health checks, service discovery, resilience
  - [x] 1.3: `ConfigureOpenTelemetry<TBuilder>()` — logging (JSON console + OTLP), metrics, tracing with health endpoint exclusion
  - [x] 1.4: `AddDefaultHealthChecks<TBuilder>()` — "self" liveness check
  - [x] 1.5: `MapDefaultEndpoints()` — `/health`, `/alive`, `/ready` with JSON response in dev
  - [x] 1.6: Add tracing sources: `Hexalith.Parties.CommandApi`, `Hexalith.Parties`
  - [x] 1.7: Add `ServiceDefaults` project reference to CommandApi `.csproj`

- [x] Task 2: Create Aspire hosting extensions (AC: #1)
  - [x] 2.1: Create `src/Hexalith.Parties.Aspire/HexalithPartiesExtensions.cs` — `AddHexalithParties()` extension method
  - [x] 2.2: Create `src/Hexalith.Parties.Aspire/HexalithPartiesResources.cs` — resource record
  - [x] 2.3: Extension must: call `builder.AddHexalithEventStore()` for EventStore topology, then wire Parties CommandApi with DAPR sidecar
  - [x] 2.4: Add Aspire project reference: `Hexalith.EventStore.Aspire` NuGet package reference

- [x] Task 3: Create AppHost `Program.cs` and DAPR config (AC: #1, #2)
  - [x] 3.1: Create `src/Hexalith.Parties.AppHost/Program.cs` — orchestrates full local topology
  - [x] 3.2: Resolve DAPR access control config path (current dir + BaseDirectory fallback)
  - [x] 3.3: Add Parties CommandApi project via `builder.AddProject<>()`
  - [x] 3.4: Call `builder.AddHexalithParties()` to wire EventStore + DAPR components
  - [x] 3.5: Optional Keycloak OIDC integration (follow EventStore pattern with `EnableKeycloak` config)
  - [x] 3.6: Publisher environment targets (docker, k8s, aca)
  - [x] 3.7: Create `DaprComponents/statestore.yaml` — Redis state store with `actorStateStore: "true"`
  - [x] 3.8: Create `DaprComponents/pubsub.yaml` — Redis Streams pub/sub with scoping
  - [x] 3.9: Create `DaprComponents/accesscontrol.yaml` — service-to-service invocation policies
  - [x] 3.10: Create `DaprComponents/resiliency.yaml` — retry, timeout, circuit breaker policies
  - [x] 3.11: Create `Properties/launchSettings.json` with HTTP/HTTPS profiles

- [x] Task 4: Integrate ServiceDefaults into CommandApi (AC: #3, #6)
  - [x] 4.1: Update `Program.cs` — add `builder.AddServiceDefaults()` call before `AddParties()`
  - [x] 4.2: Add `app.MapDefaultEndpoints()` after `MapActorsHandlers()`
  - [x] 4.3: Verify health endpoints respond: `/health` (all), `/alive` (live), `/ready` (ready-tagged)

- [x] Task 5: Implement GDPR compliance warning (AC: #4, #5)
  - [x] 5.1: Create `src/Hexalith.Parties.CommandApi/Middleware/GdprWarningMiddleware.cs` — adds `X-GDPR-Warning` header to every response
  - [x] 5.2: Register middleware in `Program.cs` — place FIRST in pipeline (before exception handler)
  - [x] 5.3: Add GDPR startup warning log at `Warning` level in `Program.cs` during app initialization
  - [x] 5.4: Warning text: "GDPR Notice: This MVP does not include GDPR compliance features (crypto-shredding, consent, erasure). Do not store regulated EU personal data. See v1.1 roadmap."

- [x] Task 6: Build and regression verification (AC: all)
  - [x] 6.1: `dotnet build Hexalith.Parties.slnx` — zero errors, zero new warnings
  - [x] 6.2: `dotnet test` — all existing tests pass (66+ tests), zero regressions
  - [x] 6.3: No modifications to Contracts, Server, or Testing projects

## Dev Notes

### ServiceDefaults — Follow EventStore Pattern Exactly

The `Extensions.cs` must mirror `Hexalith.EventStore.ServiceDefaults/Extensions.cs` structure. Key adaptations for Parties:

```csharp
// Tracing sources — use Parties names, not EventStore
.WithTracing(tracing => tracing
    .AddSource(builder.Environment.ApplicationName)
    .AddSource("Hexalith.Parties.CommandApi")
    .AddSource("Hexalith.Parties")
    // ... rest identical to EventStore
```

**Reference file:** `Hexalith.EventStore/src/Hexalith.EventStore.ServiceDefaults/Extensions.cs` (185 lines)

**Key methods to implement:**
1. `AddServiceDefaults<TBuilder>()` — main entry point (OpenTelemetry, health checks, service discovery, resilience)
2. `ConfigureOpenTelemetry<TBuilder>()` — logging (JSON console + OTLP), metrics, tracing
3. `AddOpenTelemetryExporters<TBuilder>()` — OTLP exporter when `OTEL_EXPORTER_OTLP_ENDPOINT` configured
4. `AddDefaultHealthChecks<TBuilder>()` — "self" liveness check with `["live"]` tag
5. `WriteHealthCheckJsonResponse()` — detailed JSON for dev environments
6. `MapDefaultEndpoints()` — `/health` (all checks), `/alive` (live-tagged), `/ready` (ready-tagged)

**Health check status code mapping:** Healthy=200, Degraded=200, Unhealthy=503

### Aspire Extensions — Parties Layered on EventStore

The Parties Aspire extension MUST use the EventStore Aspire extension as its foundation. It does NOT recreate DAPR components from scratch — it extends the EventStore topology.

```csharp
// HexalithPartiesExtensions.cs pattern:
public static HexalithPartiesResources AddHexalithParties(
    this IDistributedApplicationBuilder builder,
    IResourceBuilder<ProjectResource> commandApi,
    string? daprConfigPath = null)
{
    // 1. Wire EventStore topology (creates statestore, pubsub, sidecar)
    HexalithEventStoreResources eventStoreResources = builder.AddHexalithEventStore(commandApi, daprConfigPath);

    // 2. Return Parties resources wrapping EventStore resources
    return new HexalithPartiesResources(eventStoreResources, commandApi);
}
```

**Reference files:**
- `Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs`
- `Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreResources.cs`

**Critical:** The EventStore `AddHexalithEventStore()` already creates the DAPR state store (in-memory with `actorStateStore: "true"`) and pub/sub, and wires the CommandApi with a DAPR sidecar. Parties extension layers on top — it does NOT duplicate this infrastructure.

**Aspire.csproj needs:** Add `ProjectReference` to `Hexalith.EventStore.Aspire` NuGet or project reference. Current `Hexalith.Parties.Aspire.csproj` only has `Aspire.Hosting` — it needs access to `HexalithEventStoreExtensions`.

### AppHost Program.cs — Full Topology Orchestrator

Follow `Hexalith.EventStore/src/Hexalith.EventStore.AppHost/Program.cs` (86 lines) as the pattern:

```csharp
// 1. Resolve DAPR access control config path
string accessControlConfigPath = Path.Combine(
    Directory.GetCurrentDirectory(), "DaprComponents", "accesscontrol.yaml");
if (!File.Exists(accessControlConfigPath))
{
    accessControlConfigPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "DaprComponents", "accesscontrol.yaml"));
}
// Throw FileNotFoundException if not found

// 2. Add Parties CommandApi project
IResourceBuilder<ProjectResource> commandApi = builder.AddProject<Projects.Hexalith_Parties_CommandApi>("commandapi");

// 3. Wire Parties topology (delegates to EventStore + Parties Aspire extensions)
HexalithPartiesResources partiesResources = builder.AddHexalithParties(commandApi, accessControlConfigPath);

// 4. Optional Keycloak (follow EventStore pattern)
// 5. Publisher environments (docker, k8s, aca)
```

**Key differences from EventStore AppHost:**
- References `Projects.Hexalith_Parties_CommandApi` (not EventStore CommandApi)
- Calls `builder.AddHexalithParties()` (not `AddHexalithEventStore()`)
- No sample domain service (Parties IS the domain service)
- Keycloak realm: `hexalith` (same realm, different audience: `hexalith-parties`)

### DAPR Component YAML Files

**CRITICAL:** These files go under `src/Hexalith.Parties.AppHost/DaprComponents/`. Follow EventStore patterns from `Hexalith.EventStore/src/Hexalith.EventStore.AppHost/DaprComponents/`.

**statestore.yaml:**
```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: statestore
spec:
  type: state.redis
  version: v1
  metadata:
  - name: redisHost
    value: "localhost:6379"
  - name: actorStateStore
    value: "true"
scopes:
  - commandapi
```

**pubsub.yaml:**
```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: pubsub
spec:
  type: pubsub.redis
  version: v1
  metadata:
  - name: redisHost
    value: "localhost:6379"
scopes:
  - commandapi
```

**accesscontrol.yaml** — Defines service-to-service policies. Follow EventStore pattern. Scope `commandapi` as trusted with all infrastructure access.

**resiliency.yaml** — Retry, timeout, circuit breaker policies. Follow EventStore pattern.

**Note:** The `subscription-parties.yaml` from the AC is for event subscription configuration. At this point (Epic 1), there are no projections yet — subscriptions will be added in Epic 3. Create a minimal placeholder or skip if not needed for the round-trip in AC #6. The round-trip (CreateParty -> GET by ID) works via the aggregate actor directly (no projections needed).

### GDPR Warning Implementation

**Middleware approach (FR62):**

```csharp
// GdprWarningMiddleware.cs
namespace Hexalith.Parties.CommandApi.Middleware;

public sealed class GdprWarningMiddleware(RequestDelegate next)
{
    private const string GdprWarningHeader = "X-GDPR-Warning";
    private const string GdprWarningMessage =
        "GDPR Notice: This MVP does not include GDPR compliance features "
        + "(crypto-shredding, consent, erasure). Do not store regulated EU personal data. "
        + "See v1.1 roadmap.";

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.Append(GdprWarningHeader, GdprWarningMessage);
            return Task.CompletedTask;
        });

        await next(context);
    }
}
```

**Startup log warning in Program.cs:**
```csharp
WebApplication app = builder.Build();

// GDPR compliance warning (FR62) — non-dismissable, logged at startup
ILogger startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Hexalith.Parties");
startupLogger.LogWarning(
    "GDPR Notice: This MVP does not include GDPR compliance features "
    + "(crypto-shredding, consent, erasure). Do not store regulated EU personal data. "
    + "See v1.1 roadmap.");
```

### CommandApi Program.cs — Updated Pipeline

After all changes, `Program.cs` should look like:

```csharp
using Hexalith.Parties.CommandApi.Extensions;
using Hexalith.Parties.CommandApi.Middleware;
using Hexalith.Parties.ServiceDefaults;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Service defaults (OpenTelemetry, health checks, resilience, service discovery)
builder.AddServiceDefaults();

builder.Services.AddDaprClient();
builder.Services.AddParties(builder.Configuration);

WebApplication app = builder.Build();

// GDPR compliance warning (FR62) — non-dismissable
ILogger startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Hexalith.Parties");
startupLogger.LogWarning(
    "GDPR Notice: This MVP does not include GDPR compliance features "
    + "(crypto-shredding, consent, erasure). Do not store regulated EU personal data. "
    + "See v1.1 roadmap.");

// Middleware pipeline (order matters)
app.UseMiddleware<GdprWarningMiddleware>();  // FIRST — every response gets GDPR header
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapActorsHandlers();
app.MapDefaultEndpoints();                    // Health checks: /health, /alive, /ready

app.Run();

public partial class Program;
```

### launchSettings.json for AppHost

```json
{
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "applicationUrl": "http://localhost:15100",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL": "http://localhost:21100",
        "ASPIRE_DASHBOARD_FRONTEND_BROWSERTOKEN": "dev-token"
      }
    },
    "https": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "applicationUrl": "https://localhost:17100;http://localhost:15100",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL": "http://localhost:21100",
        "ASPIRE_DASHBOARD_FRONTEND_BROWSERTOKEN": "dev-token"
      }
    }
  }
}
```

### End-to-End Round Trip Verification (AC #6)

After starting with `dotnet aspire run`:
1. Get a JWT token (dev symmetric key: `DevOnlySigningKey-AtLeast32Chars-MustBeSecure!`)
2. POST to `http://localhost:{port}/api/v1/parties` with CreateParty command
3. Should return `202 Accepted` with `correlationId`
4. GET `http://localhost:{port}/api/v1/parties/{id}` with the same JWT
5. Should return `200 OK` with the party details
6. Both responses include `X-GDPR-Warning` header

The CommandApi port is dynamically assigned by Aspire — check the Aspire dashboard for the actual URL.

### Project Structure Notes

**New files (this story):**
```
src/Hexalith.Parties.ServiceDefaults/
└── Extensions.cs                                ← NEW

src/Hexalith.Parties.Aspire/
├── HexalithPartiesExtensions.cs                 ← NEW
└── HexalithPartiesResources.cs                  ← NEW

src/Hexalith.Parties.AppHost/
├── Program.cs                                   ← NEW
├── Properties/
│   └── launchSettings.json                      ← NEW
└── DaprComponents/
    ├── statestore.yaml                          ← NEW
    ├── pubsub.yaml                              ← NEW
    ├── accesscontrol.yaml                       ← NEW
    └── resiliency.yaml                          ← NEW

src/Hexalith.Parties.CommandApi/
└── Middleware/
    └── GdprWarningMiddleware.cs                 ← NEW
```

**Modified files:**
```
src/Hexalith.Parties.CommandApi/
├── Program.cs                                   ← MODIFIED (add ServiceDefaults, GDPR log, MapDefaultEndpoints)
└── Hexalith.Parties.CommandApi.csproj           ← MODIFIED (add ServiceDefaults project reference)

src/Hexalith.Parties.Aspire/
└── Hexalith.Parties.Aspire.csproj              ← MODIFIED (add EventStore.Aspire reference)
```

**No modifications to Contracts, Server, or Testing projects.**

### Existing .csproj Files (Already Exist — Key Details)

**AppHost.csproj** — already has correct SDK and references:
```xml
<Project Sdk="Aspire.AppHost.Sdk/13.1.2">
  <ItemGroup>
    <ProjectReference Include="..\Hexalith.Parties.CommandApi\..." />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Aspire.Hosting.Redis" />
    <PackageReference Include="CommunityToolkit.Aspire.Hosting.Dapr" />
  </ItemGroup>
</Project>
```

**ServiceDefaults.csproj** — already has correct packages:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Http.Resilience" />
    <PackageReference Include="Microsoft.Extensions.ServiceDiscovery" />
    <PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" />
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" />
    <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Http" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Runtime" />
  </ItemGroup>
</Project>
```

**Note:** ServiceDefaults.csproj has a typo: `<PackagePackageReference` instead of `<PackageReference` for `OpenTelemetry.Instrumentation.Http`. This MUST be fixed.

**Aspire.csproj** — currently only has `Aspire.Hosting`. Needs EventStore.Aspire reference.

### Dependency Versions (from Directory.Packages.props)

| Package | Version |
|---|---|
| .NET SDK | 10.0.103 (net10.0) |
| DAPR SDK | 1.16.1 |
| Aspire | 13.1.2 |
| Aspire.Hosting.Redis | 13.1.1 |
| CommunityToolkit.Aspire.Hosting.Dapr | 13.0.0 |
| OpenTelemetry.* | 1.15.0 |
| Microsoft.Extensions.Http.Resilience | 10.3.0 |
| Microsoft.Extensions.ServiceDiscovery | 10.3.0 |

### Previous Story Intelligence (Story 1.6)

- 66+ tests currently pass (21 contracts + 45 server + CommandApi integration tests), zero regressions expected
- Pre-existing ASPIRE004 build warning is unrelated — ignore it
- CommandApi already has: `Program.cs`, controllers, validation, error handling, authentication, middleware pipeline
- CommandApi uses `AddDaprClient()` and `MapActorsHandlers()` — DAPR integration exists
- GET endpoint uses DAPR actor state HTTP API (`/v1.0/actors/{actorType}/{actorId}/state/{key}`) — no projections yet
- All code conventions confirmed: Allman braces, 4-space indent, CRLF, UTF-8, file-scoped namespaces, `sealed` classes
- JWT authentication configured with dev symmetric key in `appsettings.Development.json`

### Git Intelligence

**Recent commits:**
```
4400522 Merge pull request #7 — Story 1.6: REST API Error Handling & Party Retrieval
67cdf88 Implement Story 1.6: REST API Error Handling & Party Retrieval
f3159b9 Merge pull request #6 — Story 1.5: Party Aggregate Tier 1 Unit Tests
f54056b Implement Story 1.5: Party Aggregate — Tier 1 Unit Tests
a3caccb Merge pull request #5 — Story 1.4: Party Aggregate Update & Lifecycle
```

**Branch naming pattern:** `implement-story-1-7-apphost-local-development-and-gdpr-warning`
**Commit message pattern:** `Implement Story 1.7: AppHost, Local Development & GDPR Warning`

### Critical Implementation Rules

1. **Follow EventStore patterns** — ServiceDefaults, Aspire extensions, AppHost all mirror EventStore equivalents
2. **DO NOT create health check classes in CommandApi** — EventStore already provides DAPR health checks via `AddEventStoreDaprHealthChecks()`. If needed, call that from `PartiesServiceCollectionExtensions` or `Program.cs`. Do NOT duplicate the health check classes.
3. `sealed` classes, file-scoped namespaces, Allman braces, 4-space indentation, CRLF, UTF-8
4. One public type per file, file name = type name
5. No PII in log messages — GDPR warning text is generic, no personal data
6. Build must succeed with `TreatWarningsAsErrors = true`
7. Fix the ServiceDefaults.csproj typo (`PackagePackageReference` -> `PackageReference`)
8. DAPR YAML files must have correct `scopes` restricting access to `commandapi` only
9. The `subscription-parties.yaml` from AC #2 is for event subscriptions. Since no projections exist yet (Epic 3), either create a minimal placeholder or skip it. The end-to-end round trip works via the aggregate actor — no subscriptions needed.

### Anti-Patterns to Avoid

- **DO NOT** duplicate EventStore DAPR health check classes — reuse them
- **DO NOT** hardcode DAPR AppPort in Aspire config — let CommunityToolkit auto-detect
- **DO NOT** create Redis infrastructure in AppHost — EventStore's `AddHexalithEventStore()` handles DAPR component creation via Aspire
- **DO NOT** put GDPR logic in a filter or controller — use middleware for universal coverage
- **DO NOT** make the GDPR header dismissable or configurable — it's non-dismissable per FR62
- **DO NOT** modify Contracts, Server, or Testing projects
- **DO NOT** create Keycloak configuration files unless following EventStore's exact pattern

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-1.7 — Acceptance criteria, FR62 GDPR warning]
- [Source: _bmad-output/planning-artifacts/architecture.md — Aspire hosting, DAPR topology, ServiceDefaults, health checks, OpenTelemetry]
- [Source: _bmad-output/planning-artifacts/prd.md#FR62 — Non-dismissable GDPR compliance warning]
- [Source: _bmad-output/implementation-artifacts/1-6-rest-api-error-handling-and-party-retrieval.md — Previous story intelligence, Program.cs patterns, middleware pipeline]
- [Source: Hexalith.EventStore/src/Hexalith.EventStore.ServiceDefaults/Extensions.cs — ServiceDefaults reference (185 lines)]
- [Source: Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs — Aspire extension reference (57 lines)]
- [Source: Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreResources.cs — Resources record reference]
- [Source: Hexalith.EventStore/src/Hexalith.EventStore.AppHost/Program.cs — AppHost reference (86 lines)]
- [Source: Hexalith.EventStore/src/Hexalith.EventStore.CommandApi/HealthChecks/ — DAPR health check classes]
- [Source: src/Hexalith.Parties.CommandApi/Program.cs — Current CommandApi entry point]
- [Source: src/Hexalith.Parties.CommandApi/Extensions/PartiesServiceCollectionExtensions.cs — Current DI registration]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- Build failure 1: AppHost.csproj needed `IsAspireProjectResource="false"` on Aspire project reference (ASPIRE004 warning treated as error). Fixed by adding attribute and matching EventStore AppHost pattern (OutputType, Keycloak/publisher packages, Content items).

### Completion Notes List

- **Task 1:** Created `Extensions.cs` in ServiceDefaults mirroring EventStore pattern with Parties-specific tracing sources (`Hexalith.Parties.CommandApi`, `Hexalith.Parties`). Allman brace style. All 6 methods implemented: `AddServiceDefaults`, `ConfigureOpenTelemetry`, `AddOpenTelemetryExporters`, `AddDefaultHealthChecks`, `WriteHealthCheckJsonResponse`, `MapDefaultEndpoints`.
- **Task 2:** Created `HexalithPartiesExtensions.cs` with `AddHexalithParties()` delegating to EventStore's `AddHexalithEventStore()`. Created `HexalithPartiesResources` record. Added EventStore.Aspire project reference to Aspire.csproj.
- **Task 3:** Created AppHost `Program.cs` following EventStore pattern — DAPR config path resolution, CommandApi project wiring via `AddHexalithParties()`, optional Keycloak OIDC (audience: `hexalith-parties`), publisher environments (docker/k8s/aca). Created 4 DAPR component YAML files (statestore, pubsub, accesscontrol, resiliency) and launchSettings.json.
- **Task 4:** Updated CommandApi `Program.cs` — added `builder.AddServiceDefaults()` before `AddParties()`, added `app.MapDefaultEndpoints()` after `MapActorsHandlers()`. Added ServiceDefaults project reference to CommandApi.csproj.
- **Task 5:** Created `GdprWarningMiddleware.cs` adding `X-GDPR-Warning` header via `OnStarting` callback. Registered as FIRST middleware in pipeline. Added startup `LogWarning` with GDPR notice text.
- **Task 6:** Build succeeded with zero errors, zero warnings. All 73 tests pass (21 contracts + 45 server + 7 CommandApi). No modifications to Contracts, Server, or Testing projects.

### Change Log

- 2026-03-04: Story 1.7 implementation — AppHost, ServiceDefaults, Aspire extensions, DAPR configuration, GDPR warning middleware and startup log
- 2026-03-04: Senior Developer Review (AI) performed — changes requested; follow-up action items added
- 2026-03-04: AI review fixes applied — added subscription configuration, readiness check tagging fix, integration tests for GDPR header and create/get round-trip

### File List

**New files:**
- `src/Hexalith.Parties.ServiceDefaults/Extensions.cs`
- `src/Hexalith.Parties.Aspire/HexalithPartiesExtensions.cs`
- `src/Hexalith.Parties.Aspire/HexalithPartiesResources.cs`
- `src/Hexalith.Parties.AppHost/Program.cs`
- `src/Hexalith.Parties.AppHost/Properties/launchSettings.json`
- `src/Hexalith.Parties.AppHost/DaprComponents/statestore.yaml`
- `src/Hexalith.Parties.AppHost/DaprComponents/pubsub.yaml`
- `src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.yaml`
- `src/Hexalith.Parties.AppHost/DaprComponents/resiliency.yaml`
- `src/Hexalith.Parties.AppHost/DaprComponents/subscription-parties.yaml`
- `src/Hexalith.Parties.CommandApi/Middleware/GdprWarningMiddleware.cs`
- `tests/Hexalith.Parties.IntegrationTests/PartyApiRoundTripIntegrationTests.cs`

**Modified files:**
- `src/Hexalith.Parties.CommandApi/Program.cs` — added ServiceDefaults, GDPR warning log, GdprWarningMiddleware, MapDefaultEndpoints
- `src/Hexalith.Parties.CommandApi/Hexalith.Parties.CommandApi.csproj` — added ServiceDefaults project reference
- `src/Hexalith.Parties.Aspire/Hexalith.Parties.Aspire.csproj` — added EventStore.Aspire project reference
- `src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj` — added Aspire project reference, Keycloak/publisher packages, Content items, OutputType
- `tests/Hexalith.Parties.IntegrationTests/Hexalith.Parties.IntegrationTests.csproj` — added `Microsoft.AspNetCore.Mvc.Testing`
- `src/Hexalith.Parties.ServiceDefaults/Extensions.cs` — added default `ready` health check tag for `/ready`
- `src/Hexalith.Parties.CommandApi/Middleware/GdprWarningMiddleware.cs` — switched header assignment to single-value set
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — synced story status transitions (`review` → `in-progress` → `done`)

### Review Follow-ups (AI)

- [x] [AI-Review][HIGH] AC #2 not fully implemented: `subscription-parties.yaml` added under AppHost DAPR components.
- [x] [AI-Review][HIGH] Readiness endpoint non-functional due to missing `ready` tags: fixed by registering a default `ready` health check.
- [x] [AI-Review][MEDIUM] AC #6 verification gap: added integration tests covering `POST CreateParty -> GET party by ID` and GDPR warning header behavior.
- [x] [AI-Review][MEDIUM] Git/story discrepancy for sprint tracking file list: File List updated to include `_bmad-output/implementation-artifacts/sprint-status.yaml`.
- [x] [AI-Review][LOW] Potential duplicate GDPR warning header values: fixed by switching to single-value header assignment.

## Senior Developer Review (AI)

**Reviewer:** Jérôme (AI)
**Date:** 2026-03-04
**Outcome:** Approved

### Summary

- Story claims were validated against implementation and git changes.
- Review findings were fixed in code and tests.
- Story status moved to `done` after re-validation.

### Findings

#### High

1. **Missing required DAPR subscription file (AC #2).**
  - Evidence: Story AC explicitly requires `subscription-parties.yaml`, but no such file exists under `src/Hexalith.Parties.AppHost/DaprComponents/`.
2. **Readiness signal does not represent readiness state.**
  - Evidence: readiness endpoint filters `Tags.Contains("ready")`, while registered default check is tagged only `"live"`.

#### Medium

1. **End-to-end acceptance path not evidenced in repository tests/artifacts.**
  - Evidence: no Parties test coverage found for GDPR header injection or AppHost startup round-trip verification.
2. **File List mismatch with git reality.**
  - Evidence: sprint status file changed but missing from story File List.

#### Low

1. **Potential duplicate GDPR warning header values.**
  - Evidence: middleware uses `Headers.Append` rather than setting a single value.
