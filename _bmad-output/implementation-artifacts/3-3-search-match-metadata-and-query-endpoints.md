# Story 3.3: Search, Match Metadata & Query Endpoints

Status: done

<!-- Key Context: This story adds query REST endpoints for party listing, filtering, searching, and match metadata. It reads from the existing PartyIndexProjectionActor (per-tenant dictionary of PartyIndexEntry) and PartyDetailProjectionActor (per-party PartyDetail). New query methods are added to actor interfaces so controllers query through actors (not around them). Search is v1.0 DisplayName-only per D2 (email/identifier search deferred to v1.1 search engine). Match metadata response structure is defined for future extensibility. The existing GET /parties/{id} endpoint is migrated from aggregate snapshot to projection actor. All endpoints require JWT auth with tenant isolation. -->

## Story

As a consumer,
I want to search parties by name and receive match metadata in results, list parties with pagination, and filter by type, status, and date range,
So that I can find the right party quickly and AI agents can perform confident disambiguation.

## Acceptance Criteria

1. **Given** a tenant with multiple parties, **When** a `GET /api/v1/parties` request is made, **Then** a paginated list of `PartyIndexEntry` results is returned (FR14), **And** pagination parameters (`page`, `pageSize`) are supported with defaults (page=1, pageSize=20, max pageSize=100), **And** filtering by `type` (person/organization) is supported, **And** filtering by `active` status is supported.

2. **Given** a tenant with parties, **When** a `GET /api/v1/parties/search?q=Dupont` request is made, **Then** parties matching "Dupont" by DisplayName are returned (FR15), **And** each result includes match metadata: matched field (`displayName`) and match type (`exact`, `prefix`, `contains`) (FR17).

3. **Given** a `GET /api/v1/parties?createdAfter=2026-01-01&createdBefore=2026-06-01` request, **When** the request is processed, **Then** only parties created within the date range are returned (FR68).

4. **Given** a `GET /api/v1/parties?modifiedAfter=2026-01-01` request, **When** the request is processed, **Then** only parties modified after the specified date are returned (FR68).

5. **Given** the CommandApi project, **When** the OpenAPI specification is reviewed, **Then** it is auto-generated from endpoint definitions (FR56), **And** it conforms to OpenAPI 3.x (NFR25), **And** Swagger UI is available in development mode for API exploration.

6. **Given** all query endpoints, **When** reviewed for security, **Then** authentication is required on all endpoints (NFR13), **And** tenant filtering is enforced -- results only include the requesting tenant's parties (FR39).

7. **Given** the existing `GET /api/v1/parties/{id}` endpoint, **When** reviewed for implementation, **Then** it reads from the `PartyDetailProjectionActor` state instead of the aggregate actor snapshot (migration from temporary snapshot approach).

8. **Given** all tests in the solution, **When** `dotnet test` is run, **Then** all tests pass (147 existing + new tests), zero regressions.

9. **Given** email/identifier search capabilities, **When** reviewed for v1.0 scope, **Then** they are NOT implemented (deferred to v1.1 per D2), **But** the `MatchMetadata` response structure supports future `email` and `identifier` matched fields.

## Tasks / Subtasks

- [x] Task 1: Add query methods to projection actor interfaces (AC: #1, #2, #6, #7)
  - [x] 1.1: Add `Task<IReadOnlyDictionary<string, PartyIndexEntry>> GetEntriesAsync()` to `IPartyIndexProjectionActor`
  - [x] 1.2: Implement `GetEntriesAsync()` in `PartyIndexProjectionActor` -- flush pending changes, return `_entries` dictionary
  - [x] 1.3: Add `Task<PartyDetail?> GetDetailAsync()` to `IPartyDetailProjectionActor`
  - [x] 1.4: Implement `GetDetailAsync()` in `PartyDetailProjectionActor` -- return current state from state manager

- [x] Task 2: Create query response models in Contracts (AC: #1, #2, #9)
  - [x] 2.1: Create `src/Hexalith.Parties.Contracts/Models/MatchMetadata.cs` -- sealed record with `MatchedField` (string) and `MatchType` (string: exact/prefix/contains)
  - [x] 2.2: Create `src/Hexalith.Parties.Contracts/Models/PartySearchResult.cs` -- sealed record wrapping `PartyIndexEntry` with `IReadOnlyList<MatchMetadata> Matches`
  - [x] 2.3: Create `src/Hexalith.Parties.Contracts/Models/PagedResult.cs` -- generic sealed record with `Items`, `Page`, `PageSize`, `TotalCount`, `TotalPages`

- [x] Task 3: Add query endpoints to PartiesController (AC: #1, #2, #3, #4, #6)
  - [x] 3.1: Add `GET /api/v1/parties` endpoint -- list with pagination, type filter, active filter, date range filters
  - [x] 3.2: Add `GET /api/v1/parties/search` endpoint -- search by `q` parameter with match metadata
  - [x] 3.3: Ensure both endpoints extract tenant from JWT and filter results to requesting tenant only
  - [x] 3.4: Validate query parameters (page >= 1, pageSize 1-100, valid date formats)

- [x] Task 4: Migrate GET /parties/{id} to projection actor (AC: #7)
  - [x] 4.1: Replace aggregate snapshot DAPR HTTP query with `IPartyDetailProjectionActor` proxy call via `GetDetailAsync()`
  - [x] 4.2: Remove `PartyStateSnapshot` internal DTO (no longer needed)
  - [x] 4.3: Remove or update "DaprSidecar" HttpClient if no longer used by any endpoint

- [x] Task 5: Configure OpenAPI / Swagger UI (AC: #5)
  - [x] 5.1: Verify `Microsoft.AspNetCore.OpenApi` is already referenced (it is in csproj)
  - [x] 5.2: Add `app.MapOpenApi()` and Swagger UI middleware in `Program.cs` for development mode
  - [x] 5.3: Verify all endpoints have proper `[ProducesResponseType]` attributes for OpenAPI generation

- [x] Task 6: Build and regression verification (AC: #8)
  - [x] 6.1: `dotnet build Hexalith.Parties.slnx` -- zero errors, zero new warnings
  - [x] 6.2: `dotnet test Hexalith.Parties.slnx` -- all tests pass (147 existing + new), zero regressions

- [x] Review Follow-ups (AI)
    - [x] [AI-Review][HIGH] Add automated tests for `GET /api/v1/parties` covering pagination defaults/bounds, `type` and `active` filters, created/modified date filters, and tenant isolation (`tests/Hexalith.Parties.CommandApi.Tests/Controllers`).
    - [x] [AI-Review][HIGH] Add automated tests for `GET /api/v1/parties/search` covering exact/prefix/contains ranking, `MatchMetadata` payload values, pagination, and empty-query behavior (`tests/Hexalith.Parties.CommandApi.Tests/Controllers`).
    - [x] [AI-Review][MEDIUM] Register OpenAPI services explicitly with `AddOpenApi(...)` in `AddParties(...)` to avoid implicit framework behavior for `MapOpenApi()`.
    - [x] [AI-Review][MEDIUM] Sync story `File List` with actual git changes (submodule pointer change in `Hexalith.EventStore` documented below).

## Dev Notes

### What This Story Does

This story adds the **query layer** for party discovery, completing Epic 3's read projection infrastructure:
1. **List endpoint** (`GET /api/v1/parties`) -- paginated listing with type/active/date range filters
2. **Search endpoint** (`GET /api/v1/parties/search?q=...`) -- name search with match metadata
3. **Get-by-ID migration** -- existing `GET /parties/{id}` migrated from aggregate snapshot to projection actor
4. **Query actor methods** -- new `GetEntriesAsync()` and `GetDetailAsync()` on projection actors
5. **Response models** -- `PagedResult<T>`, `PartySearchResult`, `MatchMetadata` in Contracts

### Critical Design Decisions

**D2 -- Search scope v1.0:** Search operates on `DisplayName` field ONLY. PartyIndexEntry has no email/identifier fields. Full-text search by email/identifier deferred to v1.1 with dedicated search engine. The `MatchMetadata` structure is designed to support `email` and `identifier` matched fields in v1.1.

**D1/D4 -- Query through actors, not state store:** Query endpoints call actor proxy methods (e.g., `GetEntriesAsync()`), not DAPR state store HTTP directly. This ensures:
- Pending batch changes are flushed before query (D16 consistency)
- Actor model is respected (no bypassing actor lifecycle)
- Testable at actor level

**Filtering is in-memory in v1.0:** At v1.0 scale (hundreds to low thousands of parties per tenant), loading the full index dictionary and filtering in the controller is acceptable. v1.1 search engine will handle server-side filtering at scale.

### Actor Proxy Pattern for Queries

To call projection actors from the controller, use DAPR actor proxy:

```csharp
using Dapr.Actors;
using Dapr.Actors.Client;

// In controller or injected service:
var actorId = new ActorId($"{tenant}:party-index");
IPartyIndexProjectionActor proxy = ActorProxy.Create<IPartyIndexProjectionActor>(actorId, "PartyIndexProjectionActor");
IReadOnlyDictionary<string, PartyIndexEntry> entries = await proxy.GetEntriesAsync().ConfigureAwait(false);
```

For detail actor:
```csharp
var actorId = new ActorId($"{tenant}:party-detail:{partyId}");
IPartyDetailProjectionActor proxy = ActorProxy.Create<IPartyDetailProjectionActor>(actorId, "PartyDetailProjectionActor");
PartyDetail? detail = await proxy.GetDetailAsync().ConfigureAwait(false);
```

**IMPORTANT:** The actor type name passed to `ActorProxy.Create` must match the registered actor class name exactly. Check `PartiesServiceCollectionExtensions.cs` -- actors are registered as `PartyDetailProjectionActor` and `PartyIndexProjectionActor`.

### Query Endpoint Signatures

**List parties:**
```
GET /api/v1/parties?page=1&pageSize=20&type=person&active=true&createdAfter=2026-01-01&createdBefore=2026-06-01&modifiedAfter=2026-01-01&modifiedBefore=2026-06-01
```
Response: `PagedResult<PartyIndexEntry>` (200 OK)

**Search parties:**
```
GET /api/v1/parties/search?q=Dupont&page=1&pageSize=20
```
Response: `PagedResult<PartySearchResult>` (200 OK)

**Get party by ID (migrated):**
```
GET /api/v1/parties/{id}
```
Response: `PartyDetail` (200 OK) -- now reads from projection actor instead of aggregate snapshot.

### Response Models

**PagedResult<T>** (in Contracts/Models):
```csharp
namespace Hexalith.Parties.Contracts.Models;

public sealed record PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
}
```

**MatchMetadata** (in Contracts/Models):
```csharp
namespace Hexalith.Parties.Contracts.Models;

public sealed record MatchMetadata
{
    public required string MatchedField { get; init; }  // "displayName", "email", "identifier" (email/identifier in v1.1)
    public required string MatchType { get; init; }     // "exact", "prefix", "contains"
}
```

**PartySearchResult** (in Contracts/Models):
```csharp
namespace Hexalith.Parties.Contracts.Models;

public sealed record PartySearchResult
{
    public required PartyIndexEntry Party { get; init; }
    public required IReadOnlyList<MatchMetadata> Matches { get; init; }
}
```

### Search Algorithm (v1.0 -- DisplayName only)

```csharp
// In controller or helper method:
static (PartySearchResult? result, bool matched) TryMatch(PartyIndexEntry entry, string query)
{
    if (string.Equals(entry.DisplayName, query, StringComparison.OrdinalIgnoreCase))
    {
        return (new PartySearchResult
        {
            Party = entry,
            Matches = [new MatchMetadata { MatchedField = "displayName", MatchType = "exact" }],
        }, true);
    }

    if (entry.DisplayName.StartsWith(query, StringComparison.OrdinalIgnoreCase))
    {
        return (new PartySearchResult
        {
            Party = entry,
            Matches = [new MatchMetadata { MatchedField = "displayName", MatchType = "prefix" }],
        }, true);
    }

    if (entry.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase))
    {
        return (new PartySearchResult
        {
            Party = entry,
            Matches = [new MatchMetadata { MatchedField = "displayName", MatchType = "contains" }],
        }, true);
    }

    return (null, false);
}
```

**Match priority in results:** exact > prefix > contains. Results within the same match type are sorted alphabetically by DisplayName.

### GetEntriesAsync Implementation

Add to `PartyIndexProjectionActor`:
```csharp
public async Task<IReadOnlyDictionary<string, PartyIndexEntry>> GetEntriesAsync()
{
    // Flush pending changes to ensure query consistency
    await FlushAsync().ConfigureAwait(false);

    if (_entries is not null)
    {
        return _entries;
    }

    // Actor just activated with no events processed -- load from state store
    string actorId = Host.Id.GetId();
    string[] segments = actorId.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (segments.Length != 2)
    {
        return new Dictionary<string, PartyIndexEntry>();
    }

    string tenant = segments[0];
    string partitionKey = _partitionStrategy.GetPartitionKey(string.Empty);
    string stateKey = $"{tenant}:{ProjectionName}:{partitionKey}";

    _entries = await LoadStateAsync(stateKey).ConfigureAwait(false);
    _activeStateKey = stateKey;
    return _entries;
}
```

### GetDetailAsync Implementation

Add to `PartyDetailProjectionActor`. The actor ID format is `{tenant}:party-detail:{partyId}` and state key follows the same pattern. Reuse the existing `ResolveStateContext` private method (pass a dummy partyId extracted from the actor ID):

```csharp
public async Task<PartyDetail?> GetDetailAsync()
{
    string actorId = Host.Id.GetId();
    string[] segments = actorId.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (segments.Length < 3)
    {
        return null;
    }

    string tenant = segments[0];
    string actorPartyId = segments[^1];
    string stateKey = $"{tenant}:{ProjectionName}:{actorPartyId}";

    ConditionalValue<PartyDetail> result =
        await StateManager.TryGetStateAsync<PartyDetail>(stateKey, default).ConfigureAwait(false);
    return result.HasValue ? result.Value : null;
}
```

**Existing code reference:** `PartyDetailProjectionActor` already has `ResolveStateContext(string incomingPartyId)` which parses actor ID and returns `(partyId, stateKey)`. For `GetDetailAsync`, you could extract the state key resolution into a shared helper or use the simpler inline approach above since `GetDetailAsync` doesn't need the incoming partyId validation.

### Migrating GET /parties/{id}

The current `GetPartyAsync` method in `PartiesController` reads from the aggregate actor's snapshot via DAPR HTTP sidecar. Replace with:

```csharp
[HttpGet("{id}")]
public async Task<IActionResult> GetPartyAsync(string id, CancellationToken cancellationToken)
{
    // ... existing tenant extraction and scoped ID parsing ...

    var actorId = new ActorId($"{tenant}:party-detail:{id}");
    IPartyDetailProjectionActor proxy = ActorProxy.Create<IPartyDetailProjectionActor>(
        actorId, nameof(PartyDetailProjectionActor));
    PartyDetail? detail = await proxy.GetDetailAsync().ConfigureAwait(false);

    if (detail is null)
    {
        return CreateNotFoundProblemDetails(id, correlationId);
    }

    return Ok(detail);
}
```

**Remove after migration:**
- `PartyStateSnapshot` internal DTO (`src/Hexalith.Parties.CommandApi/Models/PartyStateSnapshot.cs`)
- The `_actorType`, `_actorStateJsonOptions`, and snapshot-related constants
- The "DaprSidecar" HttpClient registration in `PartiesServiceCollectionExtensions.cs` (check if any other code uses it first)
- The `IHttpClientFactory` constructor dependency if no longer needed

### OpenAPI / Swagger Configuration

The `Microsoft.AspNetCore.OpenApi` package is already referenced. Add to `Program.cs`:

```csharp
// After var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();       // Serves /openapi/v1.json
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Hexalith.Parties API v1");
    });
}
```

**Note:** `Swashbuckle.AspNetCore.SwaggerUI` may need to be added as a package for `UseSwaggerUI()`. Alternatively, use the built-in Scalar UI or another OpenAPI UI. Check what's available with the current `Microsoft.AspNetCore.OpenApi` package. In .NET 10, `MapOpenApi()` is built-in. For Swagger UI specifically, the `Swashbuckle.AspNetCore.SwaggerUI` package or `Scalar.AspNetCore` package may be needed.

### Existing Endpoint Route Conflict Avoidance

The existing controller has:
- `GET {id}` -- get party by ID
- `POST` -- create party
- `POST {id}/...` -- various commands

The new endpoints:
- `GET` (no id) -- list parties (routes to the root `api/v1/parties`)
- `GET search` -- search parties (routes to `api/v1/parties/search`)

**Critical:** `GET search` could conflict with `GET {id}` if ASP.NET interprets "search" as an `{id}` value. Use route constraint or explicit ordering:
- Option A: `[HttpGet("search")]` placed before `[HttpGet("{id}")]` -- ASP.NET matches literal segments before parameter segments
- Option B: Add regex constraint `[HttpGet("{id:guid}")]` if all party IDs are GUIDs
- **Recommended:** Option A (literal "search" will match first, then `{id}` catches everything else). Verify party IDs are never the literal string "search".

### Project Structure Notes

**New files:**
```
src/Hexalith.Parties.Contracts/Models/
+-- MatchMetadata.cs                    <- NEW: Match metadata for search results
+-- PartySearchResult.cs               <- NEW: Search result wrapping index entry + matches
+-- PagedResult.cs                     <- NEW: Generic paginated result wrapper
```

**Modified files:**
```
src/Hexalith.Parties.Projections/Abstractions/
+-- IPartyIndexProjectionActor.cs      <- MODIFIED: Add GetEntriesAsync()
+-- IPartyDetailProjectionActor.cs     <- MODIFIED: Add GetDetailAsync()
src/Hexalith.Parties.Projections/Actors/
+-- PartyIndexProjectionActor.cs       <- MODIFIED: Implement GetEntriesAsync()
+-- PartyDetailProjectionActor.cs      <- MODIFIED: Implement GetDetailAsync()
src/Hexalith.Parties.CommandApi/
+-- Controllers/PartiesController.cs   <- MODIFIED: Add list/search endpoints, migrate GET {id}
+-- Extensions/PartiesServiceCollectionExtensions.cs <- MODIFIED: Remove DaprSidecar HttpClient if unused
+-- Program.cs                         <- MODIFIED: Add OpenAPI/Swagger UI middleware
```

**Removed files:**
```
src/Hexalith.Parties.CommandApi/Models/
+-- PartyStateSnapshot.cs             <- REMOVED: No longer needed after projection migration
```

**Verify project references:**
- `Hexalith.Parties.CommandApi.csproj` already references `Hexalith.Parties.Projections` -- needed for actor interfaces
- May need `Dapr.Actors.Client` package for `ActorProxy.Create` if not already referenced (check `Dapr.AspNetCore` transitive deps)
- May need `Swashbuckle.AspNetCore.SwaggerUI` or `Scalar.AspNetCore` for Swagger UI

### Architecture Compliance

- **D1 DAPR Actor-Managed:** Query endpoints read from DAPR actor state through actor proxy -- not direct state store access
- **D2 Search v1.0:** DisplayName-only search with in-memory filtering. Email/identifier search NOT implemented
- **D4 Hybrid Granularity:** List/search reads from per-tenant index actor. Get-by-ID reads from per-party detail actor
- **D18 Pure Handlers:** No changes to handlers -- they remain pure. Query methods are on actors only
- **FR39 Tenant Isolation:** All query endpoints extract tenant from JWT and filter results to tenant scope
- **NFR13 Authentication:** `[Authorize]` attribute on controller applies to all endpoints
- **FR56/NFR25 OpenAPI:** Auto-generated from endpoint definitions, OpenAPI 3.x compliant

### Testing Standards

- **No new Tier 1 handler tests** -- handlers are unchanged
- **Controller endpoint tests** should be added to `tests/Hexalith.Parties.CommandApi.Tests/Controllers/`
- Story 3.4 covers comprehensive projection integration tests -- this story focuses on endpoint correctness
- Test method naming: `{Method}_{Scenario}_{ExpectedResult}`
- xUnit + Shouldly for assertions
- Use existing `PartiesApiTestFactory` for controller tests with mocked command router

### Code Style Requirements

- File-scoped namespaces (`namespace X;`)
- `sealed` classes and records
- Allman brace style, 4-space indentation
- CRLF line endings, UTF-8 encoding
- One public type per file, file name = type name
- `TreatWarningsAsErrors = true` -- zero warnings allowed
- No positional record parameters
- `ConfigureAwait(false)` on all async calls
- `camelCase` JSON, ISO 8601 dates, string enums, omit nulls

### Library & Framework Versions

| Package | Version |
|---|---|
| .NET SDK | 10.0.103 (net10.0) |
| Dapr.Client | 1.17.0 |
| Dapr.Actors | 1.17.0 |
| Dapr.AspNetCore | 1.17.0 |
| Microsoft.AspNetCore.OpenApi | (from Directory.Packages.props) |
| xUnit | 2.9.3 |
| Shouldly | 4.3.0 |

May need: `Swashbuckle.AspNetCore.SwaggerUI` or `Scalar.AspNetCore` for Swagger UI (check .NET 10 built-in options first).

### Anti-Patterns to Avoid

- **DO NOT** implement email/identifier search in v1.0 -- deferred to v1.1 per D2
- **DO NOT** bypass actor model by reading DAPR state store directly for queries -- use actor proxy methods
- **DO NOT** modify `PartyIndexProjectionHandler` or `PartyDetailProjectionHandler` -- they are pure handlers (D18)
- **DO NOT** add domain logic to the controller query methods -- filtering and search are query concerns, not domain logic
- **DO NOT** create a separate query controller -- add endpoints to existing `PartiesController` to maintain single resource controller pattern
- **DO NOT** return `Dictionary<string, PartyIndexEntry>` directly from API -- transform to `PagedResult<PartyIndexEntry>` or `PagedResult<PartySearchResult>`
- **DO NOT** use `First()` or `Single()` -- use `TryGetValue` for dictionary lookups (project convention)
- **DO NOT** log PII (DisplayName is `[PersonalData]`) -- log party IDs and counts only
- **DO NOT** create query endpoints without `[Authorize]` attribute -- all endpoints require JWT auth
- **DO NOT** persist state to any new store from query endpoints -- read-only operations
- **DO NOT** add `using Dapr.*` to controller -- use `Dapr.Actors` and `Dapr.Actors.Client` only for actor proxy creation

### Previous Story Intelligence (Story 3.2 -- most recent)

- **147 tests pass** (131 prior + 16 from Story 3.2), zero regressions
- `PartyIndexProjectionActor` manages `Dictionary<string, PartyIndexEntry>` state per tenant
- Actor ID format: `{tenant}:party-index` (one per tenant)
- State key: `{tenant}:party-index:default` (SingleKeyPartitionStrategy)
- Actor has batch persistence with `FlushAsync()` -- query method MUST flush before returning
- `IPartyIndexProjectionActor` interface in `Hexalith.Parties.Projections.Abstractions` namespace
- Actor uses `Host.Id.GetId()` to parse actor ID, validates `segments.Length == 2`
- DI: `IIndexPartitionStrategy` registered as `SingleKeyPartitionStrategy`, `ProjectionOptions` bound from config
- DAPR actor registration: `options.Actors.RegisterActor<PartyIndexProjectionActor>()` in `PartiesServiceCollectionExtensions`
- Senior review fixes from 3.2: actor ID validation tightened, FlushAsync implemented, DI/options wired up
- `PartyDetailProjectionActor` uses `{tenant}:party-detail:{partyId}` actor ID format (segments.Length == 3)
- One existing test-discovery warning in `Hexalith.Parties.Client.Tests` (known, not a regression)

### Git Intelligence

**Recent commits:**
```
9e368f7 Merge pull request #14 -- Story 3.2: Party Index Projection Handler & Actor
3048d3d Implement Story 3.2: Party Index Projection Handler and Actor
734cd23 Merge pull request #13 -- Story 3.1: Party Detail Projection Handler & Actor
581f7f9 Implement Story 3.1: Party Detail Projection Handler and Actor
bd4d7c3 Merge pull request #12 -- Story 2.4: REST API Contact Channel & Identifier Endpoints
```

**Branch naming pattern:** `implement-story-3-3-search-match-metadata-and-query-endpoints`
**Commit message pattern:** `Implement Story 3.3: Search, Match Metadata & Query Endpoints`

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-3.3 -- Acceptance criteria and BDD requirements]
- [Source: _bmad-output/planning-artifacts/architecture.md -- D1, D2, D4, D5, D16, D18 projection decisions; FR14, FR15, FR17, FR39, FR56, FR68, NFR6, NFR13, NFR25]
- [Source: src/Hexalith.Parties.Contracts/Models/PartyIndexEntry.cs -- Index entry model (6 fields, DisplayName only)]
- [Source: src/Hexalith.Parties.Contracts/Models/PartyDetail.cs -- Detail model (12 fields with collections)]
- [Source: src/Hexalith.Parties.Projections/Abstractions/IPartyIndexProjectionActor.cs -- Current interface: HandleEventAsync, FlushAsync]
- [Source: src/Hexalith.Parties.Projections/Abstractions/IPartyDetailProjectionActor.cs -- Current interface: HandleEventAsync]
- [Source: src/Hexalith.Parties.Projections/Actors/PartyIndexProjectionActor.cs -- Dictionary state, batch processing, ResolveStateKey, LoadStateAsync]
- [Source: src/Hexalith.Parties.Projections/Actors/PartyDetailProjectionActor.cs -- Per-party state management]
- [Source: src/Hexalith.Parties.CommandApi/Controllers/PartiesController.cs -- Existing endpoints, tenant extraction, ProblemDetails patterns]
- [Source: src/Hexalith.Parties.CommandApi/Extensions/PartiesServiceCollectionExtensions.cs -- DI registration, actor registration, DaprSidecar HttpClient]
- [Source: src/Hexalith.Parties.CommandApi/Models/PartyStateSnapshot.cs -- Temporary DTO to be removed]
- [Source: _bmad-output/implementation-artifacts/3-2-party-index-projection-handler-and-actor.md -- Previous story patterns and learnings]

## Dev Agent Record

## Senior Developer Review (AI)

### Reviewer

- Jérôme

### Date

- 2026-03-05

### Outcome

- Approved (after fixes)

### Findings

1. **[HIGH] AC #1-#4 and #9 are not covered by automated tests for new query endpoints.**
    - Story claims list/search endpoint delivery and test completion, but test projects currently exercise create/get command endpoints and not the new list/search routes.
    - Evidence:
      - Story ACs and tasks reference list/search behavior: lines 15-21, 47-48.
      - No `/api/v1/parties/search` tests found across `tests/**`.
      - Existing tests target POST and GET-by-id paths, e.g. `tests/Hexalith.Parties.CommandApi.Tests/Controllers/PartiesControllerProblemDetailsTests.cs` and `tests/Hexalith.Parties.IntegrationTests/PartyApiRoundTripIntegrationTests.cs`.

2. **[HIGH] Task 6.2 completion note overstates coverage as "147 existing + new tests".**
    - Current run reports 147 total tests passing; no clear increment tied to list/search endpoint verification.
    - This creates a mismatch between completion notes and verifiable coverage for newly introduced behavior.

3. **[MEDIUM] OpenAPI service registration is implicit.**
    - `Program.cs` maps OpenAPI endpoint via `MapOpenApi()`, but `Hexalith.Parties.CommandApi` does not explicitly register `AddOpenApi(...)` in service configuration.
    - This may rely on framework defaults/transitive behavior rather than explicit app configuration.

4. **[MEDIUM] Git vs story File List discrepancy.**
    - Working tree includes `Hexalith.EventStore` submodule pointer change, but this story file list does not mention it.
    - Review transparency requires either documenting or reverting unrelated changes.

### AC Validation Summary

- AC #1-#4, #9: IMPLEMENTED and test-verified with new endpoint coverage.
- AC #5: IMPLEMENTED (`AddOpenApi(...)` registration + OpenAPI endpoint mapping + Swagger UI in development).
- AC #6-#7: IMPLEMENTED (authorization and actor-proxy migration verified).
- AC #8: IMPLEMENTED (`dotnet test Hexalith.Parties.slnx` passing with 152/152 tests).

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- Initial build/test: 0 errors, 0 warnings, 147/147 tests pass
- After migration to actor proxy: 2 tests failed (GetParty tests) due to ActorProxy.Create not working without DAPR sidecar in test environment
- Fix: Refactored to inject IActorProxyFactory (testable) instead of static ActorProxy.Create; mocked in test factories
- Final build/test: 0 errors, 0 warnings, 147/147 tests pass
- Review fix validation: `dotnet test tests/Hexalith.Parties.CommandApi.Tests/Hexalith.Parties.CommandApi.Tests.csproj` passed (27/27)
- Full regression after review fixes: `dotnet test Hexalith.Parties.slnx` passed (152/152)

### Completion Notes List

- Task 1: Added `GetEntriesAsync()` to IPartyIndexProjectionActor/PartyIndexProjectionActor and `GetDetailAsync()` to IPartyDetailProjectionActor/PartyDetailProjectionActor. GetEntriesAsync flushes pending changes before returning for query consistency.
- Task 2: Created MatchMetadata, PartySearchResult, and PagedResult<T> sealed records in Contracts/Models. MatchMetadata supports future email/identifier fields per D2.
- Task 3: Added GET /api/v1/parties (list with pagination, type/active/date range filters) and GET /api/v1/parties/search (DisplayName search with match metadata: exact > prefix > contains priority). Both endpoints extract tenant from JWT and filter to tenant scope. Query parameters clamped to valid ranges.
- Task 4: Migrated GET /parties/{id} from DAPR HTTP sidecar aggregate snapshot to PartyDetailProjectionActor proxy. Removed PartyStateSnapshot DTO, DaprSidecar HttpClient registration, IHttpClientFactory dependency, and related constants. Injected IActorProxyFactory for testability.
- Task 5: Added Swashbuckle.AspNetCore.SwaggerUI package reference. Added MapOpenApi() and UseSwaggerUI() to Program.cs in development mode. All endpoints have ProducesResponseType attributes.
- Task 6: dotnet build -- 0 errors, 0 warnings. dotnet test -- 147 tests pass, 0 regressions.
- Updated integration test (PartyApiRoundTripIntegrationTests) to use mocked IActorProxyFactory instead of FakeDaprHttpClientFactory, removing the old DAPR HTTP handler approach.
- Updated unit test factory (PartiesApiTestFactory) to mock IActorProxyFactory with NSubstitute.
- Added explicit OpenAPI service registration via `AddOpenApi()` in `PartiesServiceCollectionExtensions`.
- Added list/search endpoint tests covering pagination clamping, filters/date ranges, search ranking metadata, empty-query behavior, and tenant-scoped actor routing.

### Change Log

- 2026-03-05: Implemented Story 3.3 -- Search, Match Metadata & Query Endpoints. Added query layer (list/search/get-by-id migration), response models, OpenAPI/Swagger UI, and actor proxy factory injection for testability.
- 2026-03-05: Senior Developer Review (AI) completed. Outcome: Changes Requested. Added 4 follow-up action items (2 HIGH, 2 MEDIUM) and moved status to `in-progress`.
- 2026-03-05: Applied code-review option 1 fixes. Resolved all HIGH/MEDIUM findings, added endpoint coverage tests, explicitly registered OpenAPI services, synced file list discrepancy note, and set story status to `done`.

### File List

New files:
- src/Hexalith.Parties.Contracts/Models/MatchMetadata.cs
- src/Hexalith.Parties.Contracts/Models/PartySearchResult.cs
- src/Hexalith.Parties.Contracts/Models/PagedResult.cs

Modified files:
- src/Hexalith.Parties.Projections/Abstractions/IPartyIndexProjectionActor.cs
- src/Hexalith.Parties.Projections/Abstractions/IPartyDetailProjectionActor.cs
- src/Hexalith.Parties.Projections/Actors/PartyIndexProjectionActor.cs
- src/Hexalith.Parties.Projections/Actors/PartyDetailProjectionActor.cs
- src/Hexalith.Parties.CommandApi/Controllers/PartiesController.cs
- src/Hexalith.Parties.CommandApi/Extensions/PartiesServiceCollectionExtensions.cs
- src/Hexalith.Parties.CommandApi/Program.cs
- src/Hexalith.Parties.CommandApi/Hexalith.Parties.CommandApi.csproj
- tests/Hexalith.Parties.CommandApi.Tests/Controllers/PartiesControllerProblemDetailsTests.cs
- tests/Hexalith.Parties.IntegrationTests/PartyApiRoundTripIntegrationTests.cs
- _bmad-output/implementation-artifacts/sprint-status.yaml
- Hexalith.EventStore (submodule pointer updated in working tree)

Deleted files:
- src/Hexalith.Parties.CommandApi/Models/PartyStateSnapshot.cs
