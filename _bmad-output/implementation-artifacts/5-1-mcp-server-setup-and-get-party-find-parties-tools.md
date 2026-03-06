# Story 5.1: MCP Server Setup & get_party / find_parties Tools

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an AI agent,
I want to search for and retrieve party details through dedicated MCP tools,
so that I can perform identity resolution and access structured party information.

## Acceptance Criteria

1. **Given** the CommandApi project, **when** the MCP server is configured, **then** MCP tools are registered via assembly scanning with `[McpServerToolType]` and `[McpServerTool]` attributes, **and** the MCP server implements the MCP protocol specification (NFR26) using `ModelContextProtocol.AspNetCore` HTTP transport, **and** the MCP server shares the same authentication pipeline as the REST API, **and** tenant context is extracted identically to REST (FR39).

2. **Given** an AI agent calling `get_party` with a valid party ID, **when** the tool executes, **then** the complete `PartyDetail` is returned (all details, contact channels, identifiers, active status), **and** the response shape matches what REST `GET /api/v1/parties/{id}` returns.

3. **Given** an AI agent calling `get_party` with a non-existent party ID, **when** the tool executes, **then** a clear error message is returned: "party not found".

4. **Given** an AI agent calling `find_parties` with query "Dupont", **when** the tool executes, **then** matching `PartySearchResult[]` results are returned (FR20), **and** each result includes match metadata: matched fields and match type (FR17), **and** results are sufficient for the AI agent to rank candidates and make confident autonomous matches.

5. **Given** an AI agent calling `find_parties` with query "Dupont Acme", **when** the tool executes, **then** results include parties matching on name and/or organization, with match metadata indicating which fields matched.

6. **Given** an AI agent calling `find_parties` with no query (list mode), **when** the tool executes, **then** a paginated list of parties is returned (FR23), **and** optional filters (type, active status) are supported.

7. **Given** the `GetPartyMcpTool` and `FindPartiesMcpTool` classes, **when** reviewed for naming conventions, **then** class names follow `{ToolName}McpTool` pattern, **and** MCP protocol names are snake_case: `get_party`, `find_parties`.

8. **Given** all MCP tool calls, **when** measured for latency, **then** each completes in < 1 second end-to-end including transport (NFR1).

## Tasks / Subtasks

- [ ] Task 1: Add MCP server infrastructure to CommandApi (AC: #1)
  - [ ] 1.1: Add `ModelContextProtocol.AspNetCore` package reference to `Directory.Packages.props` and to `src/Hexalith.Parties.CommandApi/Hexalith.Parties.CommandApi.csproj`
  - [ ] 1.2: Add MCP server registration in `PartiesServiceCollectionExtensions.cs`: `.AddMcpServer().WithHttpTransport().WithToolsFromAssembly()`
  - [ ] 1.3: Add `app.MapMcp()` endpoint mapping in the application pipeline (must share the same authentication/authorization middleware as the REST API)
  - [ ] 1.4: Verify MCP endpoints share JWT Bearer authentication and tenant extraction from the `eventstore:tenant` claim — same as `PartiesController`
  - [ ] 1.5: Create `src/Hexalith.Parties.CommandApi/Mcp/` folder for MCP tool classes

- [ ] Task 2: Implement `GetPartyMcpTool` (AC: #2, #3, #7, #8)
  - [ ] 2.1: Create `src/Hexalith.Parties.CommandApi/Mcp/GetPartyMcpTool.cs` with `[McpServerToolType]` attribute on the class
  - [ ] 2.2: Implement `get_party` method with `[McpServerTool]` and `[Description("Retrieves the complete details of a party by its ID, including person/organization details, contact channels, identifiers, and active status.")]`
  - [ ] 2.3: Accept `partyId` parameter (string, required) with `[Description("The unique identifier (UUID) of the party to retrieve")]`
  - [ ] 2.4: Validate `partyId` is a valid GUID; return clear error if not
  - [ ] 2.5: Extract tenant from the MCP request context using the same JWT claim extraction as `PartiesController` (`eventstore:tenant`)
  - [ ] 2.6: Query `IPartyDetailProjectionActor` via `IActorProxyFactory` using actor ID `{tenant}:party-detail:{partyId}` — same pattern as the REST `GET /api/v1/parties/{id}` endpoint
  - [ ] 2.7: Return `PartyDetail` as JSON-serialized content on success
  - [ ] 2.8: Return clear error message "Party not found" when the projection actor returns null or empty state
  - [ ] 2.9: Return 403-equivalent error for missing/invalid tenant claim

- [ ] Task 3: Implement `FindPartiesMcpTool` (AC: #4, #5, #6, #7, #8)
  - [ ] 3.1: Create `src/Hexalith.Parties.CommandApi/Mcp/FindPartiesMcpTool.cs` with `[McpServerToolType]` attribute
  - [ ] 3.2: Implement `find_parties` method with `[McpServerTool]` and `[Description("Searches for parties by name, organization, or other criteria. Returns matching parties with match metadata for disambiguation. When called with no query, returns a paginated list of all parties.")]`
  - [ ] 3.3: Accept parameters: `query` (string, optional) with `[Description("Search text to match against party names, organization names, and identifiers. Leave empty to list all parties.")]`, `type` (string, optional) with `[Description("Filter by party type: 'Person' or 'Organization'")]`, `activeOnly` (bool, optional, default true) with `[Description("When true, only returns active parties")]`, `page` (int, optional, default 1) with `[Description("Page number for pagination (starts at 1)")]`, `pageSize` (int, optional, default 20) with `[Description("Number of results per page (max 100)")]`
  - [ ] 3.4: Extract tenant from MCP request context
  - [ ] 3.5: Query `IPartyIndexProjectionActor` via `IActorProxyFactory` using actor ID `{tenant}:party-index` — same pattern as the REST `GET /api/v1/parties` and `GET /api/v1/parties/search` endpoints
  - [ ] 3.6: When `query` is provided: perform search matching (exact > prefix > contains on `DisplayName`), include `MatchMetadata` in results (matched fields, match type)
  - [ ] 3.7: When `query` is empty: return paginated list with optional `type` and `activeOnly` filters
  - [ ] 3.8: Apply `pageSize` clamping (min 1, max 100) and return `PagedResult<PartySearchResult>` as JSON
  - [ ] 3.9: Return clear error for missing/invalid tenant claim

- [ ] Task 4: Build and regression verification (AC: #1-#8)
  - [ ] 4.1: `dotnet build Hexalith.Parties.slnx` — zero errors, zero warnings
  - [ ] 4.2: `dotnet test Hexalith.Parties.slnx` — all existing tests pass (zero regressions)

## Dev Notes

### What this story does

This story sets up the MCP server infrastructure within the existing `CommandApi` project and implements the first two read-only MCP tools: `get_party` and `find_parties`. The MCP server is a **translation layer** (D11) — it accepts AI-agent-friendly inputs, delegates to existing projection actors for data retrieval, and returns complete entity responses. No domain logic, business rules, or state mutations are introduced.

### What already exists (do not recreate)

The following pieces are already implemented and should be reused directly:

- **PartyDetail model:** `src/Hexalith.Parties.Contracts/Models/PartyDetail.cs` — complete party entity view with person/org details, contact channels, identifiers, timestamps
- **PartyIndexEntry model:** `src/Hexalith.Parties.Contracts/Models/PartyIndexEntry.cs` — lightweight party summary for listing/search
- **PartySearchResult model:** `src/Hexalith.Parties.Contracts/Models/PartySearchResult.cs` — search result with match metadata
- **MatchMetadata model:** `src/Hexalith.Parties.Contracts/Models/MatchMetadata.cs` — matched fields and match type info
- **PagedResult<T> model:** used for paginated responses with Items, Page, PageSize, TotalCount, TotalPages
- **IPartyDetailProjectionActor:** `src/Hexalith.Parties.Contracts/Projections/IPartyDetailProjectionActor.cs` — DAPR actor interface for retrieving full party details
- **IPartyIndexProjectionActor:** `src/Hexalith.Parties.Contracts/Projections/IPartyIndexProjectionActor.cs` — DAPR actor interface for party search and listing
- **PartiesController query endpoints:** `src/Hexalith.Parties.CommandApi/Controllers/PartiesController.cs` — the `GetPartyAsync`, `ListPartiesAsync`, and `SearchPartiesAsync` methods show exactly how to query projection actors, extract tenant context, and handle errors. **These are the reference implementation for MCP tools.**
- **JWT tenant extraction pattern:** `User.FindAll("eventstore:tenant")` — used in `PartiesController` for multi-tenancy
- **ModelContextProtocol NuGet package:** Already declared in `Directory.Packages.props` at version 1.0.0
- **JSON serialization options:** camelCase property naming, WhenWritingNull ignore, string enum converter — configured in `PartiesServiceCollectionExtensions.cs`
- **Existing service registration:** `src/Hexalith.Parties.CommandApi/Extensions/PartiesServiceCollectionExtensions.cs`

### MCP Server technology: ModelContextProtocol C# SDK 1.0.0

**Package:** `ModelContextProtocol.AspNetCore` (for HTTP transport in ASP.NET Core apps)

**Registration pattern:**
```csharp
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();
// ...
app.MapMcp();
```

**Tool declaration pattern:**
```csharp
[McpServerToolType]
public class GetPartyMcpTool
{
    [McpServerTool(Name = "get_party"), Description("Retrieves complete party details by ID.")]
    public async Task<string> GetPartyAsync(
        [Description("The party ID (UUID)")] string partyId,
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        // Implementation
    }
}
```

**Key SDK behaviors:**
- `[McpServerToolType]` on class + `[McpServerTool]` on method = tool registration via assembly scanning
- `McpServerTool(Name = "get_party")` sets the snake_case protocol tool name
- Parameters with `[Description]` become the tool's JSON schema for AI agents
- Special parameter types (`CancellationToken`, `IServiceProvider`, `McpServer`) are auto-resolved from DI, not exposed in schema
- Return types: `string` → TextContent, `CallToolResult` → direct, other → JSON serialized as text
- Errors: throw `McpException` for client-visible errors, or return `CallToolResult` with `IsError = true`

### Critical architectural constraints (D11 — Translation Layer)

**ALLOWED in MCP tool classes:**
- Input validation (GUID format, parameter clamping)
- Tenant extraction from request context
- Projection actor querying via `IActorProxyFactory`
- Response formatting (JSON serialization of `PartyDetail`/`PartySearchResult`)
- Error message translation for AI-friendly responses

**FORBIDDEN in MCP tool classes:**
- Business rules or domain validation logic
- Direct state store access (must go through projection actors)
- References to domain event types (`IEventPayload`, `IRejectionEvent`)
- References to `PartyAggregate`, `PartyState`, or Server project types
- State caching or retry logic with domain awareness
- Command dispatching (this story is read-only; write tools are Stories 5.2 and 5.3)

### Tenant context in MCP requests

MCP tools must extract tenant context identically to REST endpoints. The `PartiesController` pattern:

```csharp
string? tenant = User.FindAll("eventstore:tenant")
    .Select(c => c.Value)
    .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
```

For MCP tools, tenant must be extracted from the authenticated request context. Since MCP tools in the `ModelContextProtocol.AspNetCore` SDK run within the ASP.NET Core pipeline, the `HttpContext` should be accessible via DI or via the MCP server's request context. The developer must verify how to access the authenticated user's claims from within an MCP tool method.

**If `HttpContext` is not directly available in MCP tools:** The developer should implement a scoped service (e.g., `ITenantAccessor`) that is populated by middleware and injected into MCP tools. This service extracts the tenant from the JWT and makes it available to tool implementations.

**Fail-closed:** If no valid tenant claim is found, the tool must return an error — never default to a fallback tenant.

### Actor ID conventions

| Actor | ID Format | Example |
|-------|-----------|---------|
| PartyDetail | `{tenant}:party-detail:{partyId}` | `acme:party-detail:550e8400-...` |
| PartyIndex | `{tenant}:party-index` | `acme:party-index` |

These are the same actor IDs used by `PartiesController` — reuse the exact same construction logic.

### Response formatting for AI agents

MCP tool responses should be JSON-serialized using the same `JsonSerializerOptions` as the REST API (camelCase, omit nulls, string enums). The response should be human-readable JSON that AI agents can parse and present to users.

**get_party response shape:**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "type": "Person",
  "isActive": true,
  "displayName": "Jean Dupont",
  "sortName": "Dupont, Jean",
  "personDetails": {
    "firstName": "Jean",
    "lastName": "Dupont"
  },
  "organizationDetails": null,
  "contactChannels": [
    {
      "id": "...",
      "type": "Email",
      "value": "jean@acme.com",
      "isPreferred": true
    }
  ],
  "identifiers": [],
  "createdAt": "2026-03-01T12:30:45Z",
  "lastModifiedAt": "2026-03-01T12:30:45Z"
}
```

**find_parties response shape (search mode):**
```json
{
  "items": [
    {
      "party": {
        "id": "...",
        "type": "Person",
        "isActive": true,
        "displayName": "Jean Dupont",
        "createdAt": "...",
        "lastModifiedAt": "..."
      },
      "matches": [
        {
          "field": "DisplayName",
          "matchType": "Prefix",
          "matchedValue": "Dupont"
        }
      ]
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 3,
  "totalPages": 1
}
```

### Error handling pattern

MCP tools should translate infrastructure/domain errors into AI-friendly messages:

| Scenario | MCP Response |
|----------|-------------|
| Invalid GUID format | `CallToolResult` with `IsError = true`, message: "Invalid party ID format. Expected a UUID like '550e8400-e29b-41d4-a716-446655440000'." |
| Party not found | `CallToolResult` with `IsError = true`, message: "Party not found. No party exists with ID '{partyId}'." |
| Missing tenant | `CallToolResult` with `IsError = true`, message: "Authentication required. No tenant context found in the request." |
| Actor failure | `CallToolResult` with `IsError = true`, message: "Unable to retrieve party details. Please try again." |

### Package dependency guidance

- `ModelContextProtocol.AspNetCore` must be added to `Directory.Packages.props` (the base `ModelContextProtocol` package is already declared at 1.0.0 — the AspNetCore package should use the same version)
- The CommandApi csproj should reference `ModelContextProtocol.AspNetCore` (not the base package) for HTTP transport support
- Verify the `ModelContextProtocol.AspNetCore` version compatibility with the existing `ModelContextProtocol` 1.0.0 declaration

### Project Structure Notes

New files to create:
```
src/Hexalith.Parties.CommandApi/
├── Mcp/
│   ├── GetPartyMcpTool.cs      (new)
│   └── FindPartiesMcpTool.cs   (new)
```

Files to modify:
```
Directory.Packages.props                                      (add ModelContextProtocol.AspNetCore)
src/Hexalith.Parties.CommandApi/Hexalith.Parties.CommandApi.csproj  (add package reference)
src/Hexalith.Parties.CommandApi/Extensions/PartiesServiceCollectionExtensions.cs  (add MCP server registration)
```

Application pipeline (wherever `app.MapMcp()` needs to be added — likely `Program.cs` or equivalent startup):
- The MCP endpoint mapping must be placed after authentication/authorization middleware
- `MapMcp()` adds `/sse` and `/messages` endpoints for MCP communication

### Testing requirements

This story does **not** include MCP tool unit tests — those are covered in Story 5.4 (MCP Tools Tests & Architectural Fitness). However, the developer must ensure:

1. `dotnet build Hexalith.Parties.slnx` succeeds with zero errors and zero warnings
2. `dotnet test Hexalith.Parties.slnx` passes all existing tests (zero regressions)
3. The MCP server starts and the tools appear in the tool listing (manual verification)

### Anti-patterns to avoid

- **Do NOT create a separate MCP project** — MCP tools live in `CommandApi/Mcp/` within the existing CommandApi project
- **Do NOT reference the Server project** from MCP tools — only Contracts types are allowed (PartyDetail, PartyIndexEntry, etc.)
- **Do NOT reference event types** (IEventPayload, IRejectionEvent) from MCP tool code
- **Do NOT duplicate query logic** — reuse the same projection actor query patterns from PartiesController
- **Do NOT create new DTO types** for MCP responses — use existing `PartyDetail`, `PartySearchResult`, `PagedResult<T>`
- **Do NOT bypass authentication** — MCP tools must require the same JWT authentication as REST endpoints
- **Do NOT add command dispatching** — this story is read-only (get + find); write tools come in Stories 5.2 and 5.3
- **Do NOT add explicit tool registration** — use `WithToolsFromAssembly()` assembly scanning

### Previous story intelligence

Story 4.4 (most recent) completed the composite command REST endpoints with validation. Key learnings:

- Extended EventStore pipeline with `ResultPayload` pass-through for composite command responses
- `PartiesController` now has both simple and composite command dispatch patterns
- FluentValidation validators use assembly scanning — no explicit registration needed
- The controller uses `IActorProxyFactory` to create actor proxies for both command routing and projection queries
- Test infrastructure uses `WebApplicationFactory<Program>` with mock `ICommandRouter` and `IActorProxyFactory`

The REST query endpoints in `PartiesController` (GetPartyAsync, ListPartiesAsync, SearchPartiesAsync) are the **exact reference implementation** for MCP tools — same projection actor queries, same tenant extraction, same error handling patterns.

### Git intelligence

Recent commits show Epic 4 completing with composite commands:

```
799ae99 Merge branch 'implement-story-4-3-composite-command-unit-tests'
0365dcc Add validators for CreatePartyComposite and UpdatePartyComposite commands
796e162 feat: Implement Composite Command REST endpoints with validation
39e713f Merge pull request #17 - Story 4.1: Create Party Composite Aggregate Handler
85b67cf Implement Story 4.1: Create Party Composite Aggregate Handler
```

Pattern: focused, additive changes with test coverage in the same slice. Story 5.1 should follow the same pattern — add MCP infrastructure and two read tools, verify build and tests pass.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-5.1`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#D11`] — Translation layer boundary
- [Source: `_bmad-output/planning-artifacts/architecture.md#D8`] — Composite aggregate command pattern
- [Source: `_bmad-output/planning-artifacts/architecture.md#NFR1`] — < 1 second latency requirement
- [Source: `_bmad-output/planning-artifacts/architecture.md#NFR26`] — MCP protocol specification
- [Source: `_bmad-output/planning-artifacts/architecture.md#FR39`] — Multi-tenancy and tenant extraction
- [Source: `_bmad-output/planning-artifacts/architecture.md#FR17`] — Match metadata for search results
- [Source: `_bmad-output/planning-artifacts/architecture.md#FR20`] — Search and discovery
- [Source: `_bmad-output/planning-artifacts/architecture.md#FR23`] — Pagination support
- [Source: `src/Hexalith.Parties.CommandApi/Controllers/PartiesController.cs`] — Reference implementation for query patterns
- [Source: `src/Hexalith.Parties.CommandApi/Extensions/PartiesServiceCollectionExtensions.cs`] — Service registration patterns
- [Source: `src/Hexalith.Parties.Contracts/Models/PartyDetail.cs`] — Complete party entity model
- [Source: `src/Hexalith.Parties.Contracts/Models/PartyIndexEntry.cs`] — Search result entry model
- [Source: `src/Hexalith.Parties.Contracts/Models/PartySearchResult.cs`] — Search result with match metadata
- [Source: `src/Hexalith.Parties.Contracts/Models/MatchMetadata.cs`] — Match metadata model
- [Source: `Directory.Packages.props`] — Central package versioning (ModelContextProtocol 1.0.0)
- [Source: `_bmad-output/implementation-artifacts/4-4-composite-command-rest-and-validation.md`] — Previous story learnings
- [Source: https://learn.microsoft.com/en-us/dotnet/ai/quickstarts/build-mcp-server] — Microsoft MCP server quickstart
- [Source: https://csharp.sdk.modelcontextprotocol.io/concepts/getting-started.html] — MCP C# SDK getting started
- [Source: https://csharp.sdk.modelcontextprotocol.io/api/ModelContextProtocol.Server.McpServerTool.html] — McpServerTool API docs

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
