# Story 6.1: Client Package — Command & Query Abstractions

Status: done

## Story

As a .NET developer,
I want to integrate party management via a single NuGet package with one-line DI registration,
So that I can send commands and query parties without knowing anything about DAPR, MediatR, or the service's infrastructure.

## Acceptance Criteria

1. `IPartiesCommandClient` interface exists with methods for all party commands:
   - `CreatePartyAsync(CreateParty command, CancellationToken ct)` returning a correlation ID
   - `UpdatePersonDetailsAsync(string partyId, UpdatePersonDetails command, CancellationToken ct)`
   - `UpdateOrganizationDetailsAsync(string partyId, UpdateOrganizationDetails command, CancellationToken ct)`
   - `AddContactChannelAsync(string partyId, AddContactChannel command, CancellationToken ct)`
   - `UpdateContactChannelAsync(string partyId, UpdateContactChannel command, CancellationToken ct)`
   - `RemoveContactChannelAsync(string partyId, RemoveContactChannel command, CancellationToken ct)`
   - `AddIdentifierAsync(string partyId, AddIdentifier command, CancellationToken ct)`
   - `RemoveIdentifierAsync(string partyId, RemoveIdentifier command, CancellationToken ct)`
   - `DeactivatePartyAsync(string partyId, CancellationToken ct)`
   - `ReactivatePartyAsync(string partyId, CancellationToken ct)`
   - `CreatePartyCompositeAsync(CreatePartyComposite command, CancellationToken ct)`
   - `UpdatePartyCompositeAsync(string partyId, UpdatePartyComposite command, CancellationToken ct)`
   - `SetIsNaturalPersonAsync(string partyId, SetIsNaturalPerson command, CancellationToken ct)`

2. `IPartiesQueryClient` interface exists with methods:
   - `GetPartyAsync(string partyId, CancellationToken ct)` returning `PartyDetail`
   - `ListPartiesAsync(int page, int pageSize, PartyType? type, bool? active, DateTimeOffset? createdAfter, DateTimeOffset? createdBefore, DateTimeOffset? modifiedAfter, DateTimeOffset? modifiedBefore, CancellationToken ct)` returning `PagedResult<PartyIndexEntry>`
   - `SearchPartiesAsync(string query, int page, int pageSize, CancellationToken ct)` returning `PagedResult<PartySearchResult>`

3. HTTP-based implementations (`HttpPartiesCommandClient`, `HttpPartiesQueryClient`) communicate with the REST API at `api/v1/parties` via standard `HttpClient`:
   - JSON serialization: camelCase, ISO 8601 dates, string enums, omit nulls
   - Command endpoints return `202 Accepted` with correlation ID — client extracts and returns it
   - Query endpoints return `200 OK` with typed response body
   - ProblemDetails error responses (RFC 9457) translated into typed `PartiesClientException`

4. `AddPartiesClient(this IServiceCollection services, IConfiguration configuration)` extension method:
   - Registers `IPartiesCommandClient` and `IPartiesQueryClient` in DI as `HttpPartiesCommandClient` / `HttpPartiesQueryClient`
   - Configures `HttpClient` via `IHttpClientFactory` with base address from configuration (`Parties:BaseUrl`)
   - Single line is all that's needed for basic usage
   - Returns `IServiceCollection` for fluent chaining

5. Client project `.csproj` references only `Hexalith.Parties.Contracts` and HTTP abstractions (`Microsoft.Extensions.Http`, `Microsoft.Extensions.Options`):
   - Zero references to DAPR, MediatR, FluentValidation, or any server-side infrastructure
   - No references to Server, Projections, or CommandApi projects
   - Total transitive dependencies < 10, package size < 5MB (NFR28, NFR31)

6. Tier 1 unit tests in `Hexalith.Parties.Client.Tests`:
   - DI registration validation (all interfaces resolved)
   - Interface contract completeness (all command/query methods present)
   - HTTP client serialization round-trip tests
   - Error handling tests (ProblemDetails → PartiesClientException)
   - Architectural fitness: Client assembly has zero forbidden references

## Tasks / Subtasks

- [x] Task 1: Create `IPartiesCommandClient` interface (AC: #1)
  - [x] Define interface in `src/Hexalith.Parties.Client/Abstractions/IPartiesCommandClient.cs`
  - [x] All command methods match REST API endpoints exactly
  - [x] All methods return `Task<string>` (correlation ID) for command operations
- [x] Task 2: Create `IPartiesQueryClient` interface (AC: #2)
  - [x] Define interface in `src/Hexalith.Parties.Client/Abstractions/IPartiesQueryClient.cs`
  - [x] Query methods return typed results (`PartyDetail`, `PagedResult<PartyIndexEntry>`, `PagedResult<PartySearchResult>`)
- [x] Task 3: Create `PartiesClientException` (AC: #3)
  - [x] Define in `src/Hexalith.Parties.Client/PartiesClientException.cs`
  - [x] Carry ProblemDetails properties: Status, Title, Type, Detail, CorrelationId
- [x] Task 4: Create `PartiesClientOptions` record (AC: #4)
  - [x] Define in `src/Hexalith.Parties.Client/PartiesClientOptions.cs`
  - [x] Property: `BaseUrl` (string, required) bound to `Parties:BaseUrl` configuration section
- [x] Task 5: Implement `HttpPartiesCommandClient` (AC: #3)
  - [x] Define in `src/Hexalith.Parties.Client/HttpPartiesCommandClient.cs`
  - [x] Inject `HttpClient` via constructor (from IHttpClientFactory)
  - [x] Map each command method to the correct REST endpoint and HTTP verb
  - [x] Serialize requests with camelCase JSON, ISO 8601 dates, string enums
  - [x] Extract correlation ID from 202 Accepted responses
  - [x] Translate ProblemDetails error responses into `PartiesClientException`
- [x] Task 6: Implement `HttpPartiesQueryClient` (AC: #3)
  - [x] Define in `src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs`
  - [x] Map each query method to the correct REST endpoint with query parameters
  - [x] Deserialize typed responses (`PartyDetail`, `PagedResult<T>`)
  - [x] Translate ProblemDetails error responses into `PartiesClientException`
- [x] Task 7: Create `AddPartiesClient` DI extension (AC: #4)
  - [x] Define in `src/Hexalith.Parties.Client/Extensions/PartiesClientServiceCollectionExtensions.cs`
  - [x] Register IPartiesCommandClient → HttpPartiesCommandClient
  - [x] Register IPartiesQueryClient → HttpPartiesQueryClient
  - [x] Use `IHttpClientFactory` with named clients and base address from options
  - [x] Bind `PartiesClientOptions` from configuration section `Parties`
- [x] Task 8: Update Client `.csproj` with required package references (AC: #5)
  - [x] Add `Microsoft.Extensions.Http` package reference
  - [x] Add `Microsoft.Extensions.Options` package reference
  - [x] Verify no forbidden references
- [x] Task 9: Write Tier 1 unit tests (AC: #6)
  - [x] DI registration test: verify all interfaces resolve
  - [x] Interface completeness tests
  - [x] HTTP command client tests with mocked HttpMessageHandler
  - [x] HTTP query client tests with mocked HttpMessageHandler
  - [x] ProblemDetails error translation tests
  - [x] Architectural fitness test: Client assembly has no references to Server, Projections, CommandApi

## Dev Notes

### REST API Endpoint Mapping (Client Must Match Exactly)

**Command Endpoints (all return 202 Accepted with `{ "correlationId": "..." }`):**

| Client Method | HTTP Method | Route | Body |
|---|---|---|---|
| `CreatePartyAsync` | POST | `api/v1/parties` | `CreateParty` command |
| `UpdatePersonDetailsAsync` | POST | `api/v1/parties/{id}/update-person-details` | `UpdatePersonDetails` |
| `UpdateOrganizationDetailsAsync` | POST | `api/v1/parties/{id}/update-organization-details` | `UpdateOrganizationDetails` |
| `SetIsNaturalPersonAsync` | POST | `api/v1/parties/{id}/set-natural-person` | `SetIsNaturalPerson` |
| `DeactivatePartyAsync` | POST | `api/v1/parties/{id}/deactivate` | empty |
| `ReactivatePartyAsync` | POST | `api/v1/parties/{id}/reactivate` | empty |
| `AddContactChannelAsync` | POST | `api/v1/parties/{id}/add-contact-channel` | `AddContactChannel` |
| `UpdateContactChannelAsync` | POST | `api/v1/parties/{id}/update-contact-channel` | `UpdateContactChannel` |
| `RemoveContactChannelAsync` | POST | `api/v1/parties/{id}/remove-contact-channel` | `RemoveContactChannel` |
| `AddIdentifierAsync` | POST | `api/v1/parties/{id}/add-identifier` | `AddIdentifier` |
| `RemoveIdentifierAsync` | POST | `api/v1/parties/{id}/remove-identifier` | `RemoveIdentifier` |
| `CreatePartyCompositeAsync` | POST | `api/v1/parties/create-composite` | `CreatePartyComposite` |
| `UpdatePartyCompositeAsync` | POST | `api/v1/parties/{id}/update-composite` | `UpdatePartyComposite` |

**Query Endpoints:**

| Client Method | HTTP Method | Route | Query Parameters | Returns |
|---|---|---|---|---|
| `GetPartyAsync` | GET | `api/v1/parties/{id}` | none | `PartyDetail` |
| `ListPartiesAsync` | GET | `api/v1/parties` | `page`, `pageSize`, `type`, `active`, `createdAfter`, `createdBefore`, `modifiedAfter`, `modifiedBefore` | `PagedResult<PartyIndexEntry>` |
| `SearchPartiesAsync` | GET | `api/v1/parties/search` | `q`, `page`, `pageSize` | `PagedResult<PartySearchResult>` |

### JSON Serialization Configuration

Use the same conventions as the server:

```csharp
var options = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Converters = { new JsonStringEnumConverter() },
};
```

### Error Handling Pattern

The REST API returns `ProblemDetails` (RFC 9457) for errors with these patterns:
- **400 Bad Request**: Validation failures (FluentValidation)
- **401 Unauthorized**: Missing/invalid tenant claim
- **403 Forbidden**: Cross-tenant access attempt
- **404 Not Found**: Party ID not found
- **422 Unprocessable Entity**: Domain rejection (business rule violation)

All errors include `correlationId` extension, and 422 errors include `tenantId` and `correctiveAction`.

The client should:
1. Check `response.IsSuccessStatusCode`
2. If not successful, read `ProblemDetails` from response body (`application/problem+json`)
3. Throw `PartiesClientException` with status, title, detail, correlationId

### Architectural Constraints

- **Target framework**: `net10.0` for Client project
- **Namespace**: `Hexalith.Parties.Client` (+ subfolders match namespace)
- **File-scoped namespaces**, **nullable enabled**, **implicit usings**
- **One public type per file**, file name = type name
- **sealed class** for implementations, **interface** for abstractions
- **sealed record** for options types
- Command types are `sealed record` from `Hexalith.Parties.Contracts.Commands`
- Value objects and models from `Hexalith.Parties.Contracts.ValueObjects` and `Hexalith.Parties.Contracts.Models`
- **NO** references to `Hexalith.Parties.Server`, `Hexalith.Parties.Projections`, `Hexalith.Parties.CommandApi`
- **NO** event type references — only commands, models, and value objects

### File Structure

```
src/Hexalith.Parties.Client/
├── Hexalith.Parties.Client.csproj
├── Abstractions/
│   ├── IPartiesCommandClient.cs
│   └── IPartiesQueryClient.cs
├── Extensions/
│   └── PartiesClientServiceCollectionExtensions.cs
├── HttpPartiesCommandClient.cs
├── HttpPartiesQueryClient.cs
├── PartiesClientException.cs
└── PartiesClientOptions.cs

tests/Hexalith.Parties.Client.Tests/
├── Hexalith.Parties.Client.Tests.csproj
├── DependencyInjectionTests.cs
├── HttpPartiesCommandClientTests.cs
├── HttpPartiesQueryClientTests.cs
├── PartiesClientExceptionTests.cs
└── FitnessTests/
    └── ClientArchitecturalFitnessTests.cs
```

### Testing Patterns

- **Framework**: xUnit 2.9.3 + Shouldly 4.3.0 + NSubstitute 5.3.0
- **Test naming**: `{Method}_{Scenario}_{ExpectedResult}Async`
- **Tier 1 only**: no DAPR, no HTTP, no database — mock `HttpMessageHandler` for HTTP tests
- **DI test pattern**: build `ServiceCollection`, add `AddPartiesClient()`, build provider, resolve interfaces
- **HTTP mock pattern**: custom `DelegatingHandler` or `NSubstitute` on `HttpMessageHandler` to return canned responses
- Follow established pattern from `Hexalith.Parties.CommandApi.Tests`

### Existing Types to Reuse (DO NOT Recreate)

From `Hexalith.Parties.Contracts.Commands`:
- `CreateParty`, `CreatePartyComposite`, `UpdatePartyComposite`
- `UpdatePersonDetails`, `UpdateOrganizationDetails`, `SetIsNaturalPerson`
- `AddContactChannel`, `UpdateContactChannel`, `RemoveContactChannel`
- `AddIdentifier`, `RemoveIdentifier`
- `DeactivateParty`, `ReactivateParty`

From `Hexalith.Parties.Contracts.Models`:
- `PartyDetail`, `PartyIndexEntry`, `PartySearchResult`, `PagedResult<T>`, `MatchMetadata`

From `Hexalith.Parties.Contracts.ValueObjects`:
- `PersonDetails`, `OrganizationDetails`, `ContactChannel`, `PartyIdentifier`, `PartyType`, `ContactChannelType`, `IdentifierType`

### Previous Epic Learnings

- All MCP tools and REST API endpoints are complete and tested (271 tests passing)
- Architectural fitness tests enforce assembly boundaries via reflection — extend this pattern for Client
- `McpSessionContext.JsonOptions` defines the canonical JSON serialization — Client must match
- `SubmitCommand` dispatch pattern and `CommandProcessingResult` are server internals; Client should NOT reference them
- ProblemDetails error handling in `PartiesController` uses specific URN types (`urn:hexalith:parties:error:*` and `urn:hexalith:parties:rejection:*`)

### NuGet Package Dependencies (for `.csproj`)

```xml
<PackageReference Include="Microsoft.Extensions.Http" />
<PackageReference Include="Microsoft.Extensions.Options.ConfigurationExtensions" />
```

These are managed via `Directory.Packages.props` (central package management). If versions are not yet listed there, add them. No other packages should be needed.

### Project Structure Notes

- Client project already exists at `src/Hexalith.Parties.Client/` with empty .csproj referencing Contracts
- Client.Tests project already exists at `tests/Hexalith.Parties.Client.Tests/` referencing Client and Testing
- Solution file `Hexalith.Parties.slnx` already includes both projects
- Build properties inherited from `Directory.Build.props` (net10.0, nullable, TreatWarningsAsErrors, NuGet metadata, MinVer)

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 6.1]
- [Source: _bmad-output/planning-artifacts/architecture.md#Client Package]
- [Source: src/Hexalith.Parties.CommandApi/Controllers/PartiesController.cs]
- [Source: src/Hexalith.Parties.Contracts/Models/PartyDetail.cs]
- [Source: src/Hexalith.Parties.Contracts/Models/PagedResult.cs]
- [Source: src/Hexalith.Parties.Contracts/Models/PartySearchResult.cs]
- [Source: src/Hexalith.Parties.Contracts/Models/PartyIndexEntry.cs]
- [Source: tests/Hexalith.Parties.CommandApi.Tests/FitnessTests/ArchitecturalFitnessTests.cs]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- CA1062 build error: Added ArgumentNullException.ThrowIfNull for configuration parameter in AddPartiesClient extension method
- CA2007 build error: Added ConfigureAwait(false) to MockHandler.SendAsync in test code
- Code review follow-up: Route-based command methods now normalize `PartyId` in request bodies to match the route and prevent API rejection on mismatched IDs
- Code review follow-up: DI registration now binds `PartiesClientOptions` through the options pipeline and resolves base addresses from registered `IOptions<PartiesClientOptions>`
- Code review follow-up: Query tests now verify ISO 8601 filter serialization and typed `PartySearchResult` deserialization
- Remaining review gap: `Microsoft.Extensions.Http` still resolves to 11 recursive Microsoft.Extensions packages in `dotnet list package --include-transitive`, so NFR28 needs either acceptance as a platform constraint or a design change beyond the current implementation
- NFR28 resolution: Accepted as platform constraint — all 11 transitive dependencies are Microsoft.Extensions.* shared framework packages that add zero real footprint to consuming .NET applications. Added fitness test to enforce only shared framework dependencies are allowed.
- Final code review fix: `AddPartiesClient()` now rejects missing or relative `Parties:BaseUrl` during registration instead of failing later during client resolution.
- Final code review fix: NFR28 fitness now inspects the restored NuGet graph in `project.assets.json` instead of relying only on loaded assembly references.
- Final code review fix: route/body `PartyId` normalization coverage now spans all route-based command methods, and malformed `202 Accepted` bodies now raise typed `PartiesClientException` errors.

### Completion Notes List

- Implemented IPartiesCommandClient with all 13 command methods returning Task<string> (correlation ID)
- Implemented IPartiesQueryClient with 3 query methods (GetParty, ListParties, SearchParties)
- Created PartiesClientException carrying ProblemDetails properties (Status, Title, Type, Detail, CorrelationId)
- Created PartiesClientOptions sealed record with required BaseUrl property
- Implemented HttpPartiesCommandClient with JSON serialization (camelCase, string enums, null omission), correlation ID extraction from 202 responses, and ProblemDetails error translation
- Implemented HttpPartiesQueryClient with typed response deserialization and ProblemDetails error translation
- Created AddPartiesClient DI extension registering both clients via IHttpClientFactory with options-bound base address from Parties:BaseUrl configuration
- Updated Client .csproj with Microsoft.Extensions.Http and Microsoft.Extensions.Options package references
- Added both packages to Directory.Packages.props central version management (10.0.0)
- Added Microsoft.Extensions.Configuration.Binder to test project for DI testing
- Wrote 39 client unit tests covering: DI registration (4), command client HTTP interactions (16), query client HTTP interactions (7), exception construction (5), architectural fitness (7)
- All 39 client tests pass, zero regressions in the validated client test project
- Code review fixes applied: route/body `PartyId` mismatches are normalized in HTTP command client methods, DI uses the options binding pipeline, and query tests now cover typed search payloads plus ISO 8601 filter serialization
- NFR28 accepted as platform constraint: added fitness test `ClientCsproj_TransitiveDependenciesAreOnlySharedFrameworkPackages` validating that only .NET shared framework and Contracts assemblies are referenced — 40 tests pass, 94 full suite
- Final review fixes applied: registration now validates `Parties:BaseUrl` immediately, dependency fitness validates the actual restored package graph, route/body normalization coverage spans every route-based command, and malformed success payloads are translated into typed client exceptions.

### Change Log

- 2026-03-06: Implemented Story 6.1 - Client Package Command & Query Abstractions
- 2026-03-06: Applied code review fixes for route/body ID normalization, options-based DI configuration, and client package dependency alignment
- 2026-03-06: Story moved to review after implementation validation. The client test suite passed with 37 tests at that checkpoint.
- 2026-03-06: Senior developer review fixes applied for options binding and query coverage; story moved back to in-progress because the transitive dependency-count NFR remains unmet.
- 2026-03-06: NFR28 accepted as platform constraint (user decision). Added fitness test enforcing only shared framework transitive deps. Story moved to review — 40 client tests, 94 full suite, 0 warnings, 0 errors.
- 2026-03-06: Final code review issues fixed — registration now validates `Parties:BaseUrl` eagerly, dependency fitness inspects `project.assets.json`, route/body normalization coverage spans all route-based commands, malformed success payloads now throw typed exceptions, and the client test suite passed with 51 tests.

### File List

New files:
- src/Hexalith.Parties.Client/Abstractions/IPartiesCommandClient.cs
- src/Hexalith.Parties.Client/Abstractions/IPartiesQueryClient.cs
- src/Hexalith.Parties.Client/PartiesClientException.cs
- src/Hexalith.Parties.Client/PartiesClientOptions.cs
- src/Hexalith.Parties.Client/HttpPartiesCommandClient.cs
- src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs
- src/Hexalith.Parties.Client/Extensions/PartiesClientServiceCollectionExtensions.cs
- tests/Hexalith.Parties.Client.Tests/DependencyInjectionTests.cs
- tests/Hexalith.Parties.Client.Tests/HttpPartiesCommandClientTests.cs
- tests/Hexalith.Parties.Client.Tests/HttpPartiesQueryClientTests.cs
- tests/Hexalith.Parties.Client.Tests/PartiesClientExceptionTests.cs
- tests/Hexalith.Parties.Client.Tests/FitnessTests/ClientArchitecturalFitnessTests.cs

Modified files:
- src/Hexalith.Parties.Client/Hexalith.Parties.Client.csproj (added package references)
- src/Hexalith.Parties.Client/HttpPartiesCommandClient.cs (normalized route/body party IDs for route-based commands)
- src/Hexalith.Parties.Client/Extensions/PartiesClientServiceCollectionExtensions.cs (retained options binding and now validates `Parties:BaseUrl` eagerly during registration)
- tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj (added Configuration.Binder)
- tests/Hexalith.Parties.Client.Tests/DependencyInjectionTests.cs (added options registration coverage)
- tests/Hexalith.Parties.Client.Tests/HttpPartiesCommandClientTests.cs (added full route/body party ID normalization coverage and malformed success-payload handling coverage)
- tests/Hexalith.Parties.Client.Tests/HttpPartiesQueryClientTests.cs (added typed search-result coverage and ISO 8601 date filter serialization checks)
- tests/Hexalith.Parties.Client.Tests/FitnessTests/ClientArchitecturalFitnessTests.cs (asserted expected abstraction package references and validated restored transitive package graph)
- Directory.Packages.props (tracks Microsoft.Extensions.Http and Microsoft.Extensions.Options versions)
- _bmad-output/implementation-artifacts/sprint-status.yaml (status updated)

## Senior Developer Review (AI)

### Outcome

Approved

### Findings Summary

- [x] Fixed: `AddPartiesClient()` now binds `PartiesClientOptions` via the options pipeline instead of manually constructing `IOptions`.
- [x] Fixed: query tests now verify ISO 8601 serialization for date filters.
- [x] Fixed: search tests now verify typed `PartySearchResult` and `MatchMetadata` deserialization.
- [x] Resolved: NFR28 transitive dependency count accepted as platform constraint — all 11 transitive deps are Microsoft.Extensions.* shared framework packages. Fitness test added to enforce no third-party infrastructure leaks.
- [x] Fixed: `AddPartiesClient()` now rejects missing or non-absolute `Parties:BaseUrl` during registration.
- [x] Fixed: NFR28 fitness now validates the restored NuGet dependency graph from `project.assets.json`.
- [x] Fixed: route/body `PartyId` normalization coverage now spans all route-based command methods.
- [x] Fixed: malformed `202 Accepted` success bodies now raise typed `PartiesClientException` errors.
