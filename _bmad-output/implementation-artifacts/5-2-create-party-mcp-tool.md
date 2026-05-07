# Story 5.2: create_party MCP Tool

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an AI agent,
I want to create a complete party from a single natural-language-extracted input,
so that I can turn "Jean Dupont at Acme Corp, email jean@acme.com" into a structured party record in one tool call.

## Acceptance Criteria

1. **Given** an AI agent calling `create_party` with full input: type "person", first name "Jean", last name "Dupont", email "jean@acme.com", **when** the tool executes, **then** the translation layer constructs a `CreatePartyComposite` command with person details and one email contact channel, **and** the complete created `PartyDetail` is returned in the response — not just the ID (FR24).

2. **Given** an AI agent calling `create_party` with partial input: type "person", last name "Bernard", email "m.bernard@newcorp.fr", **when** the tool executes, **then** the tool accepts the partial input gracefully (FR25), **and** omitted optional fields (first name, date of birth, prefix, suffix) use documented default behaviors (empty string for required `FirstName`, null for optional fields), **and** the party is created successfully with available information, **and** the complete `PartyDetail` is returned.

3. **Given** an AI agent calling `create_party` with type "organization", legal name "Acme Corp", VAT "FR12345678901", **when** the tool executes, **then** the translation layer constructs a `CreatePartyComposite` with organization details and one VAT identifier, **and** the complete created party is returned.

4. **Given** an AI agent calling `create_party` with missing required fields (e.g., no party type), **when** the tool executes, **then** a clear validation error message is returned stating what's needed (FR25), **and** the error is actionable — the AI agent understands what to fix.

5. **Given** an AI agent calling `create_party` with a contact channel but no explicit channel ID, **when** the tool executes, **then** the translation layer generates a UUID for the channel ID (forgiving input normalization).

6. **Given** the `CreatePartyMcpTool` class, **when** reviewed for architecture compliance (D11), **then** it contains input normalization (forgiving-to-strict conversion) and response assembly, **and** it does NOT contain business rules, domain validation, or state caching, **and** it references only command types and query result types — zero event type references.

## Tasks / Subtasks

- [x] Task 1: Create `CreatePartyMcpTool` class (AC: #1, #2, #3, #5, #6)
  - [x] 1.1: Create `src/Hexalith.Parties/Mcp/CreatePartyMcpTool.cs` as a `static class` with `[McpServerToolType]` attribute
  - [x] 1.2: Implement `create_party` method with `[McpServerTool(Name = "create_party")]` and `[Description("Creates a new party (person or organization) with optional contact channels and identifiers. Accepts forgiving input — missing IDs are auto-generated, partial details are accepted.")]`
  - [x] 1.3: Define forgiving input parameters with `[Description]` attributes:
    - `type` (string, required): `"Person"` or `"Organization"` — the party type
    - `firstName` (string, optional): Person's first name
    - `lastName` (string, optional): Person's last name (required for Person type)
    - `dateOfBirth` (string, optional): ISO 8601 date format
    - `prefix` (string, optional): Name prefix (e.g., "Mr.", "Dr.")
    - `suffix` (string, optional): Name suffix (e.g., "Jr.", "III")
    - `legalName` (string, optional): Organization legal name (required for Organization type)
    - `tradingName` (string, optional): Organization trading/brand name
    - `legalForm` (string, optional): Legal form (e.g., "SAS", "SARL")
    - `registrationNumber` (string, optional): Company registration number
    - `email` (string, optional): Email address — creates an Email contact channel
    - `phone` (string, optional): Phone number — creates a Phone contact channel
    - `vatNumber` (string, optional): VAT number — creates a VAT identifier
    - `IServiceProvider services`: auto-injected, not exposed in schema
    - `CancellationToken cancellationToken`: auto-injected, not exposed in schema
  - [x] 1.4: Implement input normalization (forgiving-to-strict conversion):
    - Generate `PartyId` as `Guid.NewGuid().ToString()`
    - Parse and validate `type` (case-insensitive) — throw clear error if missing or invalid
    - For Person: require at least `lastName`, default `firstName` to `""` if omitted
    - For Organization: require `legalName` — throw clear error if missing
    - Build `List<AddContactChannel>` from email/phone parameters — auto-generate `ContactChannelId` as UUID, set `PartyId` to generated party ID
    - Build `List<AddIdentifier>` from vatNumber parameter — auto-generate `IdentifierId` as UUID, set `PartyId` to generated party ID
  - [x] 1.5: Construct `CreatePartyComposite` command with normalized inputs
  - [x] 1.6: Validate command using `IValidator<CreatePartyComposite>` from DI — translate `ValidationException` into AI-friendly error message listing all validation failures
  - [x] 1.7: Dispatch command via `ICommandRouter.RouteCommandAsync()` with `SubmitCommand`:
    - `Tenant`: from `McpSessionContext.Tenant.Value`
    - `Domain`: `"party"`
    - `AggregateId`: generated PartyId
    - `CommandType`: `nameof(CreatePartyComposite)`
    - `Payload`: `JsonSerializer.SerializeToUtf8Bytes(command)`
    - `CorrelationId`: `Guid.NewGuid().ToString()`
    - `UserId`: `"mcp-agent"` (MCP context does not have a JWT `sub` claim accessible in tool methods)
  - [x] 1.8: Handle `CommandProcessingResult`:
    - If `!result.Accepted`: throw `InvalidOperationException` with AI-friendly error from `result.ErrorMessage`
    - If `result.Accepted`: query `IPartyDetailProjectionActor` to get the complete `PartyDetail` and return it
  - [x] 1.9: Return complete `PartyDetail` as JSON using `McpSessionContext.JsonOptions` (FR24)
    - Query `IPartyDetailProjectionActor` via `IActorProxyFactory` using actor ID `{tenant}:party-detail:{partyId}` — same pattern as `GetPartyMcpTool`
    - **Eventual consistency note:** The projection actor may not have processed the creation events immediately after command acceptance. If the projection returns null, construct a minimal `PartyDetail` from the input data as a fallback response (the projection will catch up asynchronously)

- [x] Task 2: Validate input normalization edge cases (AC: #2, #4, #5)
  - [x] 2.1: Verify missing `type` returns: `"Party type is required. Must be 'Person' or 'Organization'."`
  - [x] 2.2: Verify invalid `type` (e.g., "company") returns: `"Invalid party type 'company'. Must be 'Person' or 'Organization'."`
  - [x] 2.3: Verify Person without `lastName` returns: `"Last name is required for Person party type."`
  - [x] 2.4: Verify Organization without `legalName` returns: `"Legal name is required for Organization party type."`
  - [x] 2.5: Verify Person with only `lastName` succeeds — `firstName` defaults to `""`
  - [x] 2.6: Verify all sub-entity IDs (ContactChannelId, IdentifierId) are auto-generated UUIDs

- [x] Task 3: Build and regression verification (AC: #1-#6)
  - [x] 3.1: `dotnet build Hexalith.Parties.slnx` — zero errors, zero warnings
  - [x] 3.2: `dotnet test Hexalith.Parties.slnx` — all existing tests pass (zero regressions)

## Dev Notes

### What this story does

This story adds the `create_party` MCP tool — a **write tool** that enables AI agents to create parties (persons or organizations) with optional contact channels and identifiers in a single tool call. The tool is a **translation layer** (D11) that normalizes forgiving AI-agent input into strict `CreatePartyComposite` commands, dispatches them via the existing command pipeline, and returns the complete created `PartyDetail` (FR24).

### What already exists (do not recreate)

- **MCP server infrastructure** — fully set up in Story 5.1: `AddMcpServer().WithHttpTransport().WithToolsFromAssembly()` in `PartiesServiceCollectionExtensions.cs`, `app.MapMcp().RequireAuthorization()` in `Program.cs`
- **McpSessionContext** — `src/Hexalith.Parties/Mcp/McpSessionContext.cs` with `AsyncLocal<string?> Tenant` and shared `JsonSerializerOptions`
- **GetPartyMcpTool** — `src/Hexalith.Parties/Mcp/GetPartyMcpTool.cs` — reference for MCP tool patterns (static class, `[McpServerToolType]`, `IServiceProvider` injection, tenant extraction, error handling, JSON return)
- **FindPartiesMcpTool** — `src/Hexalith.Parties/Mcp/FindPartiesMcpTool.cs` — reference for parameter patterns
- **CreatePartyComposite command** — `src/Hexalith.Parties.Contracts/Commands/CreatePartyComposite.cs` — the target command type
- **CreatePartyCompositeValidator** — `src/Hexalith.Parties/Validation/CreatePartyCompositeValidator.cs` — FluentValidation validator with full input validation rules
- **PartiesController.DispatchCompositeCommandAsync** — `src/Hexalith.Parties/Controllers/PartiesController.cs` — reference implementation for command dispatch via `ICommandRouter`
- **ICommandRouter** — registered by `AddEventStoreServer(configuration)`, accepts `SubmitCommand` and returns `CommandProcessingResult`
- **SubmitCommand** — record with `Tenant`, `Domain`, `AggregateId`, `CommandType`, `Payload` (byte[]), `CorrelationId`, `UserId`
- **CommandProcessingResult** — record with `Accepted`, `ErrorMessage`, `CorrelationId`, `EventCount`
- **PartyDetail model** — `src/Hexalith.Parties.Contracts/Models/PartyDetail.cs` — complete party entity view
- **IPartyDetailProjectionActor** — `src/Hexalith.Parties.Contracts/Projections/IPartyDetailProjectionActor.cs` — for retrieving created party details
- **All value objects and enums** — `PersonDetails`, `OrganizationDetails`, `AddContactChannel`, `AddIdentifier`, `PartyType`, `ContactChannelType`, `IdentifierType`
- **JSON serialization options** — camelCase, omit nulls, string enums (shared via `McpSessionContext.JsonOptions`)

### MCP tool implementation pattern (from Story 5.1)

```csharp
[McpServerToolType]
public static class CreatePartyMcpTool
{
    [McpServerTool(Name = "create_party")]
    [Description("Creates a new party...")]
    public static async Task<string> CreatePartyAsync(
        [Description("...")] string type,
        IServiceProvider services,
        // ... optional parameters with defaults ...
        CancellationToken cancellationToken = default)
    {
        // 1. Extract tenant from McpSessionContext.Tenant.Value
        // 2. Validate and normalize input (forgiving -> strict)
        // 3. Construct CreatePartyComposite command
        // 4. Validate via IValidator<CreatePartyComposite>
        // 5. Dispatch via ICommandRouter.RouteCommandAsync()
        // 6. Query PartyDetail projection and return complete entity
    }
}
```

**Key SDK behaviors (from Story 5.1 learnings):**
- `[McpServerToolType]` on class + `[McpServerTool]` on method = tool registration via assembly scanning
- Parameters with `[Description]` become the tool's JSON schema for AI agents
- Special parameter types (`CancellationToken`, `IServiceProvider`) are auto-resolved from DI, not exposed in schema
- Errors: throw `InvalidOperationException` for client-visible errors — MCP SDK converts to `CallToolResult` with `IsError = true`
- Return type `string` → TextContent in MCP protocol

### Command dispatch pattern (from PartiesController)

```csharp
// Construct SubmitCommand
var submitCommand = new SubmitCommand(
    Tenant: tenant,
    Domain: "party",
    AggregateId: partyId,
    CommandType: nameof(CreatePartyComposite),
    Payload: JsonSerializer.SerializeToUtf8Bytes(command),
    CorrelationId: Guid.NewGuid().ToString(),
    UserId: "mcp-agent");

// Dispatch and handle result
ICommandRouter commandRouter = services.GetRequiredService<ICommandRouter>();
CommandProcessingResult result = await commandRouter
    .RouteCommandAsync(submitCommand, cancellationToken)
    .ConfigureAwait(false);

if (!result.Accepted)
    throw new InvalidOperationException($"Party creation failed: {result.ErrorMessage}");
```

**Important:** The REST controller gets `userId` from `User.FindFirst("sub")?.Value`. In MCP tool context, the JWT sub claim is not directly accessible (the `RunSessionHandler` only captures the tenant claim into `McpSessionContext`). Use `"mcp-agent"` as the userId, or extend `McpSessionContext` to also capture the sub claim if needed.

### Input normalization rules (D11 — forgiving-to-strict)

The MCP tool accepts **flat, AI-friendly parameters** and normalizes them into the strict `CreatePartyComposite` structure:

| MCP Input | → | CreatePartyComposite Field |
|-----------|---|---------------------------|
| `type` (string) | → | `Type` (parsed to `PartyType` enum) |
| `firstName`, `lastName`, `dateOfBirth`, `prefix`, `suffix` | → | `PersonDetails` (when Type=Person) |
| `legalName`, `tradingName`, `legalForm`, `registrationNumber` | → | `OrganizationDetails` (when Type=Organization) |
| `email` | → | `ContactChannels[{auto-id, Email, value, IsPreferred=true}]` |
| `phone` | → | `ContactChannels[{auto-id, Phone, value, IsPreferred=false}]` |
| `vatNumber` | → | `Identifiers[{auto-id, VAT, value}]` |

**Auto-generated values:**
- `PartyId` = `Guid.NewGuid().ToString()`
- `ContactChannelId` = `Guid.NewGuid().ToString()` (for each contact channel)
- `IdentifierId` = `Guid.NewGuid().ToString()` (for each identifier)
- `AddContactChannel.PartyId` = generated PartyId
- `AddIdentifier.PartyId` = generated PartyId
- `PersonDetails.FirstName` = `""` if omitted (required field, empty is valid default)

### Complete response assembly (FR24)

After successful command dispatch, query the `IPartyDetailProjectionActor` to get the complete `PartyDetail`:

```csharp
IActorProxyFactory actorProxyFactory = services.GetRequiredService<IActorProxyFactory>();
var actorId = new ActorId($"{tenant}:party-detail:{partyId}");
IPartyDetailProjectionActor proxy = actorProxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
    actorId, nameof(PartyDetailProjectionActor));
PartyDetail? detail = await proxy.GetDetailAsync().ConfigureAwait(false);
```

**Eventual consistency consideration:** The projection actor processes events asynchronously after command acceptance. If `detail` is null immediately after creation, construct a fallback `PartyDetail` from the input data:

```csharp
if (detail is null)
{
    // Fallback: construct from input (projection hasn't caught up yet)
    detail = new PartyDetail
    {
        Id = partyId,
        Type = partyType,
        IsActive = true,
        DisplayName = /* computed from person/org details */,
        SortName = /* computed from person/org details */,
        PersonDetails = personDetails,
        OrganizationDetails = orgDetails,
        ContactChannels = /* map from AddContactChannel */,
        Identifiers = /* map from AddIdentifier */,
        CreatedAt = DateTimeOffset.UtcNow,
        LastModifiedAt = DateTimeOffset.UtcNow
    };
}
```

### Error handling pattern (consistent with Story 5.1)

| Scenario | MCP Response |
|----------|-------------|
| Missing tenant | `InvalidOperationException`: "Authentication required. No tenant context found in the request." |
| Missing party type | `InvalidOperationException`: "Party type is required. Must be 'Person' or 'Organization'." |
| Invalid party type | `InvalidOperationException`: "Invalid party type '{value}'. Must be 'Person' or 'Organization'." |
| Person without lastName | `InvalidOperationException`: "Last name is required for Person party type." |
| Org without legalName | `InvalidOperationException`: "Legal name is required for Organization party type." |
| Validation failures | `InvalidOperationException`: "Validation failed: {list of FluentValidation errors}" |
| Command rejected | `InvalidOperationException`: "Party creation failed: {domain error message}" |

### Critical architectural constraints (D11 — Translation Layer)

**ALLOWED in CreatePartyMcpTool:**
- Input normalization (flat parameters → CreatePartyComposite record)
- UUID generation for PartyId, ContactChannelId, IdentifierId
- Type string parsing (case-insensitive "person"/"organization" → PartyType enum)
- Default value assignment for optional fields
- FluentValidation execution
- Command dispatch via ICommandRouter
- Response assembly (querying PartyDetail projection actor)
- Error message translation for AI-friendly responses

**FORBIDDEN in CreatePartyMcpTool:**
- Business rules or domain validation logic (beyond input format normalization)
- Direct state store access (must go through command router and projection actors)
- Importing or using domain event types (`IEventPayload`, `IRejectionEvent`)
- Importing or using Server project types (`PartyAggregate`, `PartyState`, etc.)
- State caching or retry logic with domain awareness
- Duplicate validation that the aggregate already performs

### Validator details (CreatePartyCompositeValidator)

The existing validator enforces:
- `PartyId` must be a non-empty valid GUID
- `Type` must be `Person` or `Organization` (not `Unknown`)
- `PersonDetails` required when `Type == Person`
- `OrganizationDetails` required when `Type == Organization`
- Total sub-operations ≤ max (1 + ContactChannels.Count + Identifiers.Count)
- Each `ContactChannel`: `PartyId` (valid GUID), `ContactChannelId` (valid GUID), `Type` (valid enum), `Value` (non-empty)
- Each `Identifier`: `PartyId` (valid GUID), `IdentifierId` (valid GUID), `Type` (valid enum), `Value` (non-empty)

The MCP tool's input normalization must produce data that passes all these rules. Since the tool auto-generates all IDs as valid GUIDs, the main validation failure scenarios are: missing type, wrong type, missing lastName (Person), missing legalName (Organization).

### Project Structure Notes

New file to create:
```
src/Hexalith.Parties/
├── Mcp/
│   ├── GetPartyMcpTool.cs        (exists)
│   ├── FindPartiesMcpTool.cs     (exists)
│   ├── McpSessionContext.cs      (exists)
│   └── CreatePartyMcpTool.cs     (NEW)
```

No other files need modification — the MCP server already uses assembly scanning (`WithToolsFromAssembly()`) so the new tool is auto-discovered.

### Testing requirements

This story does **not** include MCP tool unit tests — those are covered in Story 5.4. However, the developer must ensure:
1. `dotnet build Hexalith.Parties.slnx` succeeds with zero errors and zero warnings
2. `dotnet test Hexalith.Parties.slnx` passes all existing tests (zero regressions)

### Anti-patterns to avoid

- **Do NOT create a separate project** — the tool goes in `CommandApi/Mcp/`
- **Do NOT reference event types** from MCP tool code — only command types and model types
- **Do NOT reference Server namespace types** (`PartyAggregate`, `PartyState`) in MCP tool code
- **Do NOT duplicate the CreatePartyCompositeValidator logic** — use the existing validator via DI
- **Do NOT return just a correlation ID** — FR24 requires the complete `PartyDetail` in the response
- **Do NOT create new DTO types** for MCP responses — use existing `PartyDetail`
- **Do NOT add explicit tool registration** — assembly scanning handles it
- **Do NOT bypass authentication** — tenant must come from `McpSessionContext.Tenant.Value`
- **Do NOT add update/delete tools** — those are Story 5.3

### Previous story intelligence (Story 5.1)

Key learnings from the previous story implementation:
- `IHttpContextAccessor` does NOT work in MCP tool methods — use `McpSessionContext.Tenant` (AsyncLocal) instead
- `#pragma warning disable MCPEXP002` is needed for `RunSessionHandler` (experimental API)
- `ConfigureAwait(false)` required on all async calls to avoid CA2007
- The `IPartyDetailProjectionActor` proxy uses actor name `nameof(PartyDetailProjectionActor)` — this is a string constant, NOT the actual type reference
- Error handling pattern: throw `InvalidOperationException` — MCP SDK auto-converts to `CallToolResult` with `IsError = true`
- JSON serialization uses `McpSessionContext.JsonOptions` (camelCase, omit nulls, string enums)
- Build verification: `dotnet build Hexalith.Parties.slnx /warnaserror` must pass
- The `PartySearchResultsBuilder` was extracted as a shared utility between REST and MCP — follow same sharing principle if normalization logic proves reusable

### Git intelligence

Recent commits show Story 5.1 and its review follow-ups:
```
2b05f24 Merge pull request #19 from Hexalith/refactor/search-results-builder-and-projection-improvements
63288eb refactor: Extract PartySearchResultsBuilder and improve projection handler
082d3d1 Merge pull request #18 from Hexalith/implement-story-5-1-mcp-server-get-party-find-parties
ded476d feat: Implement Story 5.1 - MCP server setup with get_party and find_parties tools
```

Pattern: focused, additive changes. Story 5.2 adds a single new file (`CreatePartyMcpTool.cs`) with no modifications to existing files.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-5.2`] — Story requirements and acceptance criteria
- [Source: `_bmad-output/planning-artifacts/architecture.md#D11`] — Translation layer boundary
- [Source: `_bmad-output/planning-artifacts/architecture.md#D12`] — All-or-nothing composite operations
- [Source: `_bmad-output/planning-artifacts/architecture.md#FR24`] — Complete party returned on create
- [Source: `_bmad-output/planning-artifacts/architecture.md#FR25`] — Forgiving input schemas
- [Source: `_bmad-output/implementation-artifacts/5-1-mcp-server-setup-and-get-party-find-parties-tools.md`] — Previous story (MCP infrastructure + read tools)
- [Source: `src/Hexalith.Parties/Mcp/GetPartyMcpTool.cs`] — Reference MCP tool implementation
- [Source: `src/Hexalith.Parties/Mcp/FindPartiesMcpTool.cs`] — Reference MCP tool with parameters
- [Source: `src/Hexalith.Parties/Mcp/McpSessionContext.cs`] — Tenant and JSON options
- [Source: `src/Hexalith.Parties.Contracts/Commands/CreatePartyComposite.cs`] — Target command type
- [Source: `src/Hexalith.Parties/Validation/CreatePartyCompositeValidator.cs`] — Existing validator
- [Source: `src/Hexalith.Parties/Controllers/PartiesController.cs`] — Command dispatch reference
- [Source: `src/Hexalith.Parties.Contracts/Models/PartyDetail.cs`] — Response model

## Senior Developer Review (AI)

### Review Date

- 2026-03-06

### Findings Addressed

- Updated `CreatePartyMcpTool` so missing `type` is handled inside the tool and returns the documented actionable validation message instead of relying on MCP parameter binding.
- Tightened `dateOfBirth` parsing to accept ISO 8601 formats only and return a clear validation error when the input is invalid.
- Aligned fallback `PartyDetail` name derivation with the aggregate's `DeriveDisplayName` logic so immediate MCP responses match eventual projection state.
- Added focused MCP tests covering missing type validation, strict date parsing, and fallback display/sort name behavior.
- Synced story metadata and sprint tracking after review fixes.

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- Build: zero errors, zero warnings
- Tests: 231 total (21 contracts + 36 projections + 118 server + 2 integration + 54 parties), all pass

### Completion Notes List

- Created `CreatePartyMcpTool.cs` implementing the `create_party` MCP tool as a static class with `[McpServerToolType]` attribute
- Implemented forgiving-to-strict input normalization: flat AI-friendly parameters → `CreatePartyComposite` command
- All IDs auto-generated (PartyId, ContactChannelId, IdentifierId) as UUIDs
- Input validation: missing/invalid type, Person without lastName, Organization without legalName — all produce actionable error messages
- FluentValidation integration via DI (`IValidator<CreatePartyComposite>`)
- Command dispatch via `ICommandRouter.RouteCommandAsync()` with `SubmitCommand`
- Complete `PartyDetail` returned (FR24) via `IPartyDetailProjectionActor` query with eventual consistency fallback
- Architecture compliance (D11): tool is purely a translation layer — no business rules, no event types, no Server types
- Error handling follows Story 5.1 pattern: `InvalidOperationException` for all client-visible errors
- Review fixes: missing `type` now returns the documented MCP validation error instead of binder-level failure
- Review fixes: `dateOfBirth` now enforces ISO 8601 input with actionable error messaging
- Review fixes: fallback response now uses aggregate-aligned display name and sort name derivation
- Added MCP tool regression tests for the review fixes

### Change Log

- 2026-03-06: Implemented Story 5.2 — created `CreatePartyMcpTool.cs` with create_party MCP tool
- 2026-03-06: Applied code review fixes for validation, fallback response parity, MCP tests, and sprint tracking sync

### File List

- `src/Hexalith.Parties/Mcp/CreatePartyMcpTool.cs` (NEW, updated after review)
- `tests/Hexalith.Parties.Tests/Mcp/CreatePartyMcpToolTests.cs` (NEW)
- `_bmad-output/implementation-artifacts/5-2-create-party-mcp-tool.md` (review notes and status updated)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (story status synced to done)
