# Story 6.3: Sample Integration Project

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer,
I want a runnable sample project demonstrating all integration patterns,
So that I have working reference code for commands, queries, event subscriptions, and MCP usage.

## Acceptance Criteria

1. **Sample project location and structure**: The project at `samples/Hexalith.Parties.Sample/` contains a `Program.cs` that demonstrates all integration patterns. The `.csproj` already exists (empty, `IsPackable=false`). The project is already listed in `Hexalith.Parties.slnx` under the `/samples/` folder.

2. **One-line DI registration**: The sample uses `builder.Services.AddPartiesClient(builder.Configuration)` from `Hexalith.Parties.Client.Extensions` to register `IPartiesCommandClient` and `IPartiesQueryClient` via DI. Configuration uses `Parties:BaseUrl` in `appsettings.json`.

3. **Send commands**: The sample demonstrates:
   - Create a person party via `IPartiesCommandClient.CreatePartyAsync`
   - Add an email contact channel via `AddContactChannelAsync`
   - Add a VAT identifier via `AddIdentifierAsync`
   - Each command returns a `correlationId` string

4. **Query parties**: The sample demonstrates:
   - Get party by ID via `IPartiesQueryClient.GetPartyAsync` returning `PartyDetail`
   - Search by name via `SearchPartiesAsync` returning `PagedResult<PartySearchResult>`
   - List with pagination via `ListPartiesAsync` returning `PagedResult<PartyIndexEntry>`

5. **Event subscription via DAPR pub/sub**: The sample subscribes to party events on topic `{tenant}.parties.events` via DAPR pub/sub and builds a simple in-memory `CustomerSummary` read model from `PartyCreated` and `ContactChannelAdded` events.

6. **Handle PartyDeactivated**: When a `PartyDeactivated` event is received, the local `CustomerSummary` read model marks the party as inactive. The handler demonstrates idempotent event handling (re-processing same event is safe).

7. **MCP server configuration example**: The sample includes a commented/documented section showing how to configure an AI assistant (Claude Desktop, VS Code) to use the Parties MCP server endpoint.

8. **End-to-end execution**: Running `dotnet run` (with the Parties service already running via `dotnet aspire run`) executes successfully. Console output shows each step: party created, contact channel added, identifier added, party queried, search results, event received.

9. **CI compatibility**: The sample builds as part of `dotnet build Hexalith.Parties.slnx`. It does not break CI (the DAPR event subscription path is optional/graceful when infrastructure is unavailable).

10. **Forward-compatibility comments**: The event subscription section references dangling reference guidance for `PartyErased` (future v1.1) and mentions `PartyMerged` as a future subscription target.

## Tasks / Subtasks

- [x] Task 1: Update `samples/Hexalith.Parties.Sample/Hexalith.Parties.Sample.csproj` (AC: #1, #2)
  - [x] Add project reference to `Hexalith.Parties.Client`
  - [x] Add `Dapr.AspNetCore` package reference for event subscription endpoint
  - [x] Add `Microsoft.Extensions.Hosting` for generic host (skipped: redundant with Microsoft.NET.Sdk.Web)
  - [x] Set `<OutputType>Exe</OutputType>`
- [x] Task 2: Create `samples/Hexalith.Parties.Sample/appsettings.json` (AC: #2)
  - [x] Configure `Parties:BaseUrl` pointing to `https://localhost:5001` (default CommandApi port)
  - [x] Add logging configuration
- [x] Task 3: Create `samples/Hexalith.Parties.Sample/Program.cs` (AC: #2, #3, #4, #5, #6, #7, #8, #10)
  - [x] Set up generic host with `AddPartiesClient(configuration)` DI registration
  - [x] Map DAPR pub/sub event endpoint at `/events/parties`
  - [x] Implement command demo: CreateParty (person), AddContactChannel (email), AddIdentifier (VAT)
  - [x] Implement query demo: GetPartyAsync, SearchPartiesAsync, ListPartiesAsync
  - [x] Add 1-2 second delays between command and query to allow eventual consistency
  - [x] Console output for each step
- [x] Task 4: Create `samples/Hexalith.Parties.Sample/CustomerSummary.cs` (AC: #5, #6)
  - [x] Simple record/class: `Id`, `DisplayName`, `Email`, `IsActive`
  - [x] Thread-safe in-memory store (ConcurrentDictionary)
- [x] Task 5: Create `samples/Hexalith.Parties.Sample/PartyEventHandler.cs` (AC: #5, #6, #10)
  - [x] DAPR pub/sub endpoint handler for CloudEvents containing EventStore event envelopes
  - [x] Deserialize flat Server envelope format (all metadata fields at top level, base64 payload)
  - [x] Handle `PartyCreated` -> create CustomerSummary entry
  - [x] Handle `ContactChannelAdded` -> update email on CustomerSummary
  - [x] Handle `PartyDeactivated` -> mark inactive (idempotent)
  - [x] Log unhandled event types (forward-compat: `PartyMerged`, `PartyErased` comments)
  - [x] Idempotent: track processed `cloudevent.id` values
- [x] Task 6: Create DAPR subscription config for the sample (AC: #5, #9)
  - [x] Create `samples/Hexalith.Parties.Sample/DaprComponents/subscription-sample.yaml`
  - [x] Subscribe to topic `tenant-a.parties.events` (matches local dev setup)
  - [x] Route to `/events/parties`
  - [x] Document that this subscription requires the sample app-id in `pubsub.yaml` scopes to be active
- [x] Task 7: Add MCP configuration documentation (AC: #7)
  - [x] Add commented section in Program.cs or separate `mcp-config.md` showing Claude Desktop / VS Code MCP config
  - [x] Reference MCP endpoint at `/mcp` on the CommandApi
- [x] Task 8: Verify build and CI compatibility (AC: #9)
  - [x] Run `dotnet build Hexalith.Parties.slnx` -- sample must compile
  - [x] Ensure no warnings with `TreatWarningsAsErrors`
  - [x] DAPR subscription path must not crash when DAPR sidecar is absent

## Dev Notes

### Architecture & Conventions

- **Target framework**: net10.0 (pinned in `global.json` to SDK 10.0.103)
- **Build**: `TreatWarningsAsErrors=true`, nullable enabled, implicit usings, file-scoped namespaces
- **Central package management**: All package versions in `Directory.Packages.props` at solution root
- **Solution format**: `Hexalith.Parties.slnx` (modern XML format). The sample is already listed under `/samples/`
- **Code style**: `.editorconfig` -- Allman braces, `_camelCase` private fields, `I` prefix for interfaces, `Async` suffix, 4 spaces, CRLF, UTF-8

### Client Package (from Story 6.1)

The client project at `src/Hexalith.Parties.Client/` provides:

```csharp
// DI registration (one line)
builder.Services.AddPartiesClient(builder.Configuration);
// Reads Parties:BaseUrl from IConfiguration

// Interfaces resolved via DI:
IPartiesCommandClient  // 13 command methods, each returns Task<string> (correlationId)
IPartiesQueryClient    // 3 query methods: GetPartyAsync, ListPartiesAsync, SearchPartiesAsync
```

**Client .csproj references**: `Hexalith.Parties.Contracts` + `Microsoft.Extensions.Http` + `Microsoft.Extensions.Options`

### Actual Contract Types to Use

**Commands** (namespace `Hexalith.Parties.Contracts.Commands`):
```csharp
// All are sealed records with required init properties
CreateParty { PartyId, Type (PartyType enum), PersonDetails?, OrganizationDetails? }
AddContactChannel { PartyId, ContactChannelId, Type (ContactChannelType enum), Value, IsPreferred }
AddIdentifier { PartyId, IdentifierId, Type (IdentifierType enum), Value }
```

**Value Objects** (namespace `Hexalith.Parties.Contracts.ValueObjects`):
```csharp
PersonDetails { FirstName (required), LastName (required), DateOfBirth?, Prefix?, Suffix? }
enum PartyType { Unknown, Person, Organization }
enum ContactChannelType { Email, Phone, PostalAddress, SocialMedia }
enum IdentifierType { VAT, SIRET, NationalId, CompanyRegistration, TaxId, Other }
```

**Models** (namespace `Hexalith.Parties.Contracts.Models`):
```csharp
PartyDetail { Id, Type, IsActive, DisplayName, SortName, PersonDetails?, OrganizationDetails?, ContactChannels, Identifiers, CreatedAt, LastModifiedAt }
PartyIndexEntry { Id, Type, IsActive, DisplayName, CreatedAt, LastModifiedAt }
PartySearchResult // extends PartyIndexEntry with MatchMetadata
PagedResult<T> { Items (IReadOnlyList<T>), Page, PageSize, TotalCount, TotalPages }
```

**Events** (namespace `Hexalith.Parties.Contracts.Events`, all implement `IEventPayload`):
```csharp
PartyCreated { Type (PartyType), PersonDetails?, OrganizationDetails? }
ContactChannelAdded { ContactChannelId, Type (ContactChannelType), Value, IsPreferred }
IdentifierAdded { IdentifierId, Type (IdentifierType), Value }
PartyDeactivated // empty record (marker event)
PartyMerged // forward-compat placeholder
```

### Event Subscription Architecture

**DAPR Pub/Sub topic pattern**: `{tenant}.{domain}.events` (e.g., `tenant-a.parties.events`)
**Dead letter topic**: `deadletter.{tenant}.{domain}.events`
**Pub/sub component**: `pubsub` (Redis-backed in local dev)

**Event wire format**: Events arrive as CloudEvents 1.0 wrapping a flat EventStore Server envelope:
```json
{
  "aggregateId": "tenant-a:parties:550e8400-...",
  "tenantId": "tenant-a",
  "domain": "parties",
  "sequenceNumber": 1,
  "timestamp": "2026-03-06T10:30:00+00:00",
  "correlationId": "...",
  "causationId": "...",
  "userId": "user@example.com",
  "domainServiceVersion": "1.0.0",
  "eventTypeName": "PartyCreated",
  "serializationFormat": "json",
  "payload": "<base64-encoded JSON>",
  "extensions": {}
}
```

The `payload` field is base64-encoded UTF-8 JSON of the event record (e.g., `{"type":"Person","personDetails":{"firstName":"Jean","lastName":"Dupont"}}`).

**CloudEvents attributes added by EventStore**:
- `cloudevent.type` = event type name (e.g., `PartyCreated`)
- `cloudevent.source` = `hexalith-eventstore/{tenantId}/{domain}`
- `cloudevent.id` = `{correlationId}:{sequenceNumber}` (use for idempotency)

**Subscription YAML** (declarative, v2alpha1):
```yaml
apiVersion: dapr.io/v2alpha1
kind: Subscription
metadata:
  name: sample-parties-events
spec:
  pubsubname: pubsub
  topic: tenant-a.parties.events
  routes:
    default: /events/parties
  deadLetterTopic: deadletter.tenant-a.parties.events
scopes:
  - sample
```

**Important**: For the sample subscription to be active at runtime, the `pubsub.yaml` component scopes must include the sample's DAPR app-id. The existing `pubsub.yaml` only scopes to `parties`. Document this requirement clearly.

### JSON Serialization Convention

- Property naming: camelCase
- Null handling: omit when writing null
- Enum serialization: string (not numeric)
- Date format: ISO 8601
- Reference: `System.Text.Json` defaults used by DAPR

### MCP Configuration (for documentation section)

MCP endpoint: `/mcp` on CommandApi (mapped via `app.MapMcp().RequireAuthorization()`)
5 tools: `search_parties` (actually `find_parties`), `get_party`, `create_party`, `update_party`, `delete_party`

Claude Desktop config example:
```json
{
  "mcpServers": {
    "hexalith-parties": {
      "url": "https://localhost:5001/mcp",
      "headers": { "Authorization": "Bearer <token>" }
    }
  }
}
```

### Previous Story Learnings (from Stories 6.1 & 6.2)

- **Verified REST routes**: All 13 endpoints confirmed in `PartiesController.cs`. Base: `api/v1/parties`
- **JSON property names**: Use `type` (not `partyType`), `personDetails` (camelCase), etc.
- **MCP tool name correction**: The actual tool name is `find_parties` (not `search_parties` as in some docs)
- **PersonDetails uses init-only properties** (not positional constructors)
- **Auth setup**: Keycloak OIDC on port 8180, realm `hexalith`, audience `hexalith-parties`. Disable with `EnableKeycloak=false`
- **GDPR warning**: `GdprWarningMiddleware` adds `X-GDPR-Warning` header to every response
- **322 tests passing** across the solution (as of story 6.2 completion)
- **Client DI**: `AddPartiesClient(IConfiguration)` reads `Parties:BaseUrl`; registers `HttpPartiesCommandClient` and `HttpPartiesQueryClient` via `AddHttpClient`

### File Structure

```
samples/Hexalith.Parties.Sample/
  Hexalith.Parties.Sample.csproj    (exists - update)
  appsettings.json                  (new)
  Program.cs                        (new)
  CustomerSummary.cs                (new)
  PartyEventHandler.cs              (new)
  DaprComponents/
    subscription-sample.yaml        (new)
```

### Critical Anti-Patterns to Avoid

- **Do NOT reference** Server, Projections, or CommandApi projects from the sample. The sample is a *consumer* of the Client package only.
- **Do NOT add** DAPR, MediatR, FluentValidation, or EventStore server packages. The sample uses only `Hexalith.Parties.Client` + `Dapr.AspNetCore` (for subscription endpoint).
- **Do NOT use** `Hexalith.EventStore.Contracts` directly for the event envelope type. Define a simple local record matching the flat Server envelope JSON shape (the sample should demonstrate how an external consumer deserializes events without coupling to EventStore internals).
- **Do NOT hardcode** the Parties service URL. Use `IConfiguration` with `Parties:BaseUrl`.
- **Do NOT block** on event subscription in CI. The DAPR subscription handler must gracefully handle the case where no DAPR sidecar is present (the app should still compile and run the command/query demo portion).

### Project Structure Notes

- The sample `.csproj` already exists with `<IsPackable>false</IsPackable>` -- add to it, don't recreate
- The project is already in `Hexalith.Parties.slnx` under `/samples/` folder -- no solution changes needed
- Follow the same `Directory.Build.props` conventions (net10.0, nullable, TreatWarningsAsErrors)
- The sample inherits central package management from `Directory.Packages.props`

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 6.3]
- [Source: _bmad-output/planning-artifacts/architecture.md#Project Structure & Boundaries]
- [Source: _bmad-output/planning-artifacts/architecture.md#Event/Pub-Sub Conventions]
- [Source: _bmad-output/planning-artifacts/architecture.md#Implementation Patterns & Consistency Rules]
- [Source: Hexalith.EventStore/docs/concepts/event-envelope.md]
- [Source: src/Hexalith.Parties.Client/Abstractions/IPartiesCommandClient.cs]
- [Source: src/Hexalith.Parties.Client/Abstractions/IPartiesQueryClient.cs]
- [Source: src/Hexalith.Parties.Client/Extensions/PartiesClientServiceCollectionExtensions.cs]
- [Source: src/Hexalith.Parties.Contracts/Commands/CreateParty.cs]
- [Source: src/Hexalith.Parties.Contracts/Events/PartyCreated.cs]
- [Source: src/Hexalith.Parties.Contracts/Events/PartyDeactivated.cs]
- [Source: src/Hexalith.Parties.AppHost/DaprComponents/pubsub.yaml]
- [Source: src/Hexalith.Parties.AppHost/DaprComponents/subscription-parties.yaml]
- [Source: _bmad-output/implementation-artifacts/6-2-getting-started-guide-and-readme.md]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- Task 1: `Microsoft.Extensions.Hosting` package removed from .csproj because `Microsoft.NET.Sdk.Web` already includes it (NU1510 error with TreatWarningsAsErrors)
- Task 8: CA2007 `.ConfigureAwait(false)` added to all await calls in Program.cs; xUnit1030 required `.ConfigureAwait(true)` in test helper methods

### Completion Notes List

- Implemented full sample integration project demonstrating all Hexalith.Parties integration patterns
- Sample uses `Microsoft.NET.Sdk.Web` with `WebApplication.CreateBuilder` for minimal API host
- One-line DI registration via `AddPartiesClient(builder.Configuration)` reading from `appsettings.json`
- Command demo: CreateParty (person), AddContactChannel (email), AddIdentifier (VAT) with correlationId output
- Query demo: GetPartyAsync, SearchPartiesAsync, ListPartiesAsync with full pagination parameters
- Event handler: CloudEvent-aware subscriber using local `EventEnvelope` record (decoupled from EventStore internals), base64 payload deserialization
- CustomerSummary read model: ConcurrentDictionary-based in-memory store with PartyCreated, ContactChannelAdded, PartyDeactivated handlers
- Idempotent event processing via `cloudevent.id` tracking with fallback for direct local posts
- Forward-compatibility comments for PartyMerged (v2) and PartyErased (v1.1 GDPR)
- MCP configuration documented as comments in Program.cs for Claude Desktop and VS Code
- DAPR subscription YAML aligned with local AppHost pubsub scopes so the sample app-id is authorized as a subscriber
- 14 new tests (9 event handler + 5 store) via WebApplicationFactory and xUnit/Shouldly
- Solution builds with 0 warnings, 0 errors; 336 total tests passing (322 existing + 14 new)

### Change Log

- 2026-03-06: Story 6.3 implemented - Sample integration project with commands, queries, DAPR event subscription, MCP config, and tests
- 2026-03-06: Code review fixes applied - CloudEvent wrapper handling, `cloudevent.id` idempotency, local subscriber scope, and CloudEvent transport tests

### File List

- samples/Hexalith.Parties.Sample/Hexalith.Parties.Sample.csproj (modified)
- samples/Hexalith.Parties.Sample/appsettings.json (new)
- samples/Hexalith.Parties.Sample/Program.cs (new)
- samples/Hexalith.Parties.Sample/CustomerSummary.cs (new)
- samples/Hexalith.Parties.Sample/PartyEventHandler.cs (new)
- samples/Hexalith.Parties.Sample/DaprComponents/subscription-sample.yaml (new)
- src/Hexalith.Parties.AppHost/DaprComponents/pubsub.yaml (modified)
- tests/Hexalith.Parties.Sample.Tests/Hexalith.Parties.Sample.Tests.csproj (new)
- tests/Hexalith.Parties.Sample.Tests/CustomerSummaryStoreTests.cs (new)
- tests/Hexalith.Parties.Sample.Tests/PartyEventHandlerTests.cs (new)
- Hexalith.Parties.slnx (modified - added test project)
- _bmad-output/implementation-artifacts/sprint-status.yaml (modified)

## Senior Developer Review (AI)

### Review Date

- 2026-03-06

### Outcome

- Approved after fixes.

### Findings Resolved

- Updated the sample subscriber to deserialize the real DAPR CloudEvent wrapper and process the flat EventStore envelope from `data`.
- Switched subscriber deduplication to `cloudevent.id` with a compatibility fallback for direct local posts.
- Authorized the `sample` app-id in the local pub/sub component so the reference subscriber can receive events in the default AppHost topology.
- Reworked event handler tests to exercise the CloudEvent transport shape used by DAPR.
