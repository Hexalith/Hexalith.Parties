# Story 5.3: update_party & delete_party MCP Tools

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an AI agent,
I want to update party details and deactivate parties through MCP tools with patch semantics,
so that I can make targeted modifications without sending full party state.

## Acceptance Criteria

1. **Given** an AI agent calling `update_party` with party ID and only a new email address to add, **when** the tool executes, **then** the translation layer constructs an `UpdatePartyComposite` command with only `AddContactChannels` populated, **and** all other fields (PersonDetails, RemoveContactChannelIds, etc.) remain absent — patch semantics (FR74), **and** the complete updated `PartyDetail` is returned (FR69).

2. **Given** an AI agent calling `update_party` with party ID, updated first name, and a channel to remove, **when** the tool executes, **then** the translation layer constructs an `UpdatePartyComposite` with `PersonDetails` present and `RemoveContactChannelIds` populated, **and** only specified fields are modified; unspecified fields remain unchanged.

3. **Given** an AI agent calling `update_party` with a channel to add but no explicit channel ID, **when** the tool executes, **then** the translation layer generates a UUID for the channel ID (forgiving input).

4. **Given** an AI agent calling `update_party` targeting a non-existent party, **when** the tool executes, **then** a clear error message is returned: "party not found".

5. **Given** an AI agent calling `update_party` with an invalid channel ID in the remove list, **when** the tool executes, **then** the error from the aggregate rejection is translated into a clear MCP error message.

6. **Given** an AI agent calling `delete_party` with a party ID, **when** the tool executes, **then** the translation layer maps this to a `DeactivateParty` command (soft delete — FR4), **and** a confirmation response is returned with the updated `PartyDetail` showing `IsActive = false`.

7. **Given** an AI agent calling `delete_party` for an already deactivated party, **when** the tool executes, **then** the operation is handled idempotently — no error.

8. **Given** the `UpdatePartyMcpTool` and `DeletePartyMcpTool` classes, **when** reviewed for architecture compliance (D11), **then** they contain input normalization and command construction only, **and** zero references to domain event types, **and** zero business rules or domain validation logic.

## Tasks / Subtasks

- [x] Task 1: Create `UpdatePartyMcpTool` class (AC: #1, #2, #3, #4, #5, #8)
  - [x] 1.1: Create `src/Hexalith.Parties/Mcp/UpdatePartyMcpTool.cs` as a `static class` with `[McpServerToolType]` attribute
  - [x] 1.2: Implement `update_party` method with `[McpServerTool(Name = "update_party")]` and `[Description("Updates an existing party using patch semantics — only specified fields are modified. Supports updating person/organization details, adding/updating/removing contact channels, and adding/removing identifiers. Missing IDs for new items are auto-generated.")]`
  - [x] 1.3: Define forgiving input parameters with `[Description]` attributes:
    - `partyId` (string, required): The party ID to update
    - `firstName` (string, optional): Updated person first name
    - `lastName` (string, optional): Updated person last name
    - `dateOfBirth` (string, optional): Updated person date of birth (ISO 8601)
    - `prefix` (string, optional): Updated name prefix
    - `suffix` (string, optional): Updated name suffix
    - `legalName` (string, optional): Updated organization legal name
    - `tradingName` (string, optional): Updated organization trading name
    - `legalForm` (string, optional): Updated legal form
    - `registrationNumber` (string, optional): Updated registration number
    - `addEmail` (string, optional): New email address to add as contact channel
    - `addPhone` (string, optional): New phone number to add as contact channel
    - `updateContactChannelId` (string, optional): Existing contact channel ID to update
    - `updateContactChannelType` (string, optional): Updated contact channel type
    - `updateContactChannelValue` (string, optional): Updated contact channel value
    - `updateContactChannelIsPreferred` (bool, optional): Updated preferred flag
    - `removeContactChannelIds` (string, optional): Comma-separated list of contact channel IDs to remove
    - `addVatNumber` (string, optional): New VAT number to add as identifier
    - `removeIdentifierIds` (string, optional): Comma-separated list of identifier IDs to remove
    - `IServiceProvider services`: auto-injected, not exposed in schema
    - `CancellationToken cancellationToken`: auto-injected, not exposed in schema
  - [x] 1.4: Implement input normalization (forgiving-to-strict conversion):
    - Validate `partyId` is a valid GUID — throw clear error if missing or invalid
    - Build `PersonDetails` only if any person field is provided (firstName, lastName, dateOfBirth, prefix, suffix) — fetch current party state from projection to merge with provided fields (patch semantics: AI only sends what changed)
    - Build `OrganizationDetails` only if any org field is provided (legalName, tradingName, legalForm, registrationNumber) — fetch current state to merge
    - Build `List<AddContactChannel>` from addEmail/addPhone — auto-generate `ContactChannelId` as UUID, set `PartyId` to the target party ID
    - Build `List<UpdateContactChannel>` from `updateContactChannelId` + optional update fields — validate the ID exists and populate only specified fields
    - Parse `removeContactChannelIds` comma-separated string into `List<string>` — validate each is a valid GUID
    - Build `List<AddIdentifier>` from addVatNumber — auto-generate `IdentifierId` as UUID, set `PartyId` to the target party ID
    - Parse `removeIdentifierIds` comma-separated string into `List<string>` — validate each is a valid GUID
  - [x] 1.5: Construct `UpdatePartyComposite` command with only populated fields (patch semantics — null means "don't change")
  - [x] 1.6: Validate command using `IValidator<UpdatePartyComposite>` from DI — translate `ValidationException` into AI-friendly error message
  - [x] 1.7: Dispatch command via `ICommandRouter.RouteCommandAsync()` with `SubmitCommand`:
    - `Tenant`: from `McpSessionContext.Tenant.Value`
    - `Domain`: `"party"`
    - `AggregateId`: partyId
    - `CommandType`: `nameof(UpdatePartyComposite)`
    - `Payload`: `JsonSerializer.SerializeToUtf8Bytes(command)`
    - `CorrelationId`: `Guid.NewGuid().ToString()`
    - `UserId`: `"mcp-agent"`
  - [x] 1.8: Handle `CommandProcessingResult`:
    - If `!result.Accepted`: throw `InvalidOperationException` with AI-friendly error from `result.ErrorMessage`
    - If `result.Accepted`: query `IPartyDetailProjectionActor` to get the complete updated `PartyDetail` and return it (FR69)
  - [x] 1.9: Return complete updated `PartyDetail` as JSON using `McpSessionContext.JsonOptions`
    - Query `IPartyDetailProjectionActor` via `IActorProxyFactory` using actor ID `{tenant}:party-detail:{partyId}` — same pattern as other tools
    - **Eventual consistency note:** If the projection returns the old state (pre-update), return it anyway — the projection will catch up. The important thing is returning a `PartyDetail`, not just a correlation ID

- [x] Task 2: Create `DeletePartyMcpTool` class (AC: #6, #7, #8)
  - [x] 2.1: Create `src/Hexalith.Parties/Mcp/DeletePartyMcpTool.cs` as a `static class` with `[McpServerToolType]` attribute
  - [x] 2.2: Implement `delete_party` method with `[McpServerTool(Name = "delete_party")]` and `[Description("Deactivates a party (soft delete). The party record is preserved but marked as inactive. This operation is idempotent — deleting an already deactivated party succeeds without error.")]`
  - [x] 2.3: Define parameters:
    - `partyId` (string, required): The party ID to deactivate
    - `IServiceProvider services`: auto-injected, not exposed in schema
    - `CancellationToken cancellationToken`: auto-injected, not exposed in schema
  - [x] 2.4: Implement:
    - Validate `partyId` is a valid GUID — throw clear error if missing or invalid
    - Extract tenant from `McpSessionContext.Tenant.Value` — throw if missing
    - Check party exists by querying `IPartyDetailProjectionActor` — if null, throw "Party not found"
    - If party is already deactivated (`IsActive == false`), return current `PartyDetail` immediately (idempotent — AC #7)
    - Construct `new DeactivateParty { PartyId = partyId }`
  - [x] 2.5: Validate via `IValidator<DeactivateParty>` from DI
  - [x] 2.6: Dispatch command via `ICommandRouter.RouteCommandAsync()` with `SubmitCommand`:
    - `Tenant`: from `McpSessionContext.Tenant.Value`
    - `Domain`: `"party"`
    - `AggregateId`: partyId
    - `CommandType`: `nameof(DeactivateParty)`
    - `Payload`: `JsonSerializer.SerializeToUtf8Bytes(command)`
    - `CorrelationId`: `Guid.NewGuid().ToString()`
    - `UserId`: `"mcp-agent"`
  - [x] 2.7: Handle `CommandProcessingResult`:
    - If `!result.Accepted`: re-query the projection and treat an already-inactive party as idempotent success
    - If `result.Accepted`: query `IPartyDetailProjectionActor` and return updated `PartyDetail`
  - [x] 2.8: Return `PartyDetail` as JSON using `McpSessionContext.JsonOptions`

- [x] Task 3: Build and regression verification (AC: #1-#8)
  - [x] 3.1: `dotnet build Hexalith.Parties.slnx` — zero errors, zero warnings
  - [x] 3.2: `dotnet test Hexalith.Parties.slnx` — all existing tests pass (zero regressions)

## Dev Notes

### What this story does

This story adds two MCP tools — `update_party` and `delete_party` — completing the CRUD set of MCP tools for AI agent party management. Both tools are **translation layers** (D11) that normalize forgiving AI-agent input into strict domain commands, dispatch them via the existing command pipeline, and return the complete `PartyDetail` (FR69).

- **`update_party`** implements **patch semantics** (FR74): the AI agent sends only the fields it wants to change. The tool constructs an `UpdatePartyComposite` command with explicit add/update/remove lists (D9).
- **`delete_party`** maps to **soft delete** via `DeactivateParty` command (FR4). The operation is idempotent — deactivating an already-deactivated party succeeds without error.

### What already exists (do not recreate)

- **MCP server infrastructure** — fully set up in Story 5.1: `AddMcpServer().WithHttpTransport().WithToolsFromAssembly()` in `PartiesServiceCollectionExtensions.cs`
- **McpSessionContext** — `src/Hexalith.Parties/Mcp/McpSessionContext.cs` with `AsyncLocal<string?> Tenant` and shared `JsonSerializerOptions`
- **CreatePartyMcpTool** — `src/Hexalith.Parties/Mcp/CreatePartyMcpTool.cs` — reference for write MCP tool patterns (input normalization, command dispatch, projection query, error handling)
- **GetPartyMcpTool** — `src/Hexalith.Parties/Mcp/GetPartyMcpTool.cs` — reference for projection query pattern
- **FindPartiesMcpTool** — `src/Hexalith.Parties/Mcp/FindPartiesMcpTool.cs` — reference for parameter patterns
- **UpdatePartyComposite command** — `src/Hexalith.Parties.Contracts/Commands/UpdatePartyComposite.cs` — the target update command with explicit add/update/remove lists
- **DeactivateParty command** — `src/Hexalith.Parties.Contracts/Commands/DeactivateParty.cs` — the target deactivation command (just PartyId)
- **UpdatePartyCompositeValidator** — `src/Hexalith.Parties/Validation/UpdatePartyCompositeValidator.cs` — FluentValidation validator for update operations
- **DeactivatePartyValidator** — `src/Hexalith.Parties/Validation/DeactivatePartyValidator.cs` — FluentValidation validator for deactivation
- **ICommandRouter** — registered by `AddEventStoreServer(configuration)`, accepts `SubmitCommand` and returns `CommandProcessingResult`
- **SubmitCommand** — record with `Tenant`, `Domain`, `AggregateId`, `CommandType`, `Payload` (byte[]), `CorrelationId`, `UserId`
- **CommandProcessingResult** — record with `Accepted`, `ErrorMessage`, `CorrelationId`, `EventCount`
- **PartyDetail model** — `src/Hexalith.Parties.Contracts/Models/PartyDetail.cs` — complete party entity view
- **IPartyDetailProjectionActor** — `src/Hexalith.Parties.Contracts/Projections/IPartyDetailProjectionActor.cs` — for retrieving party details
- **All value objects and enums** — `PersonDetails`, `OrganizationDetails`, `AddContactChannel`, `UpdateContactChannel`, `AddIdentifier`, `ContactChannel`, `PartyIdentifier`, `ContactChannelType`, `IdentifierType`, `PartyType`
- **JSON serialization options** — camelCase, omit nulls, string enums (shared via `McpSessionContext.JsonOptions`)

### MCP tool implementation pattern (from Stories 5.1/5.2)

```csharp
[McpServerToolType]
public static class UpdatePartyMcpTool
{
    [McpServerTool(Name = "update_party")]
    [Description("Updates an existing party using patch semantics...")]
    public static async Task<string> UpdatePartyAsync(
        [Description("...")] string partyId,
        IServiceProvider services,
        // ... optional parameters with defaults ...
        CancellationToken cancellationToken = default)
    {
        // 1. Extract tenant from McpSessionContext.Tenant.Value
        // 2. Validate partyId is valid GUID
        // 3. Query current party state for patch merge (person/org details)
        // 4. Build UpdatePartyComposite with only populated fields
        // 5. Validate via IValidator<UpdatePartyComposite>
        // 6. Dispatch via ICommandRouter.RouteCommandAsync()
        // 7. Query PartyDetail projection and return complete entity (FR69)
    }
}
```

**Key SDK behaviors (from Story 5.1/5.2 learnings):**
- `[McpServerToolType]` on class + `[McpServerTool]` on method = tool registration via assembly scanning
- Parameters with `[Description]` become the tool's JSON schema for AI agents
- Special parameter types (`CancellationToken`, `IServiceProvider`) are auto-resolved from DI, not exposed in schema
- Errors: throw `InvalidOperationException` for client-visible errors — MCP SDK converts to `CallToolResult` with `IsError = true`
- Return type `string` → TextContent in MCP protocol

### UpdatePartyComposite command structure (D9 — explicit add/update/remove lists)

```csharp
public sealed record UpdatePartyComposite
{
    public required string PartyId { get; init; }
    public PersonDetails? PersonDetails { get; init; }              // null = don't change
    public OrganizationDetails? OrganizationDetails { get; init; }  // null = don't change
    public IReadOnlyList<AddContactChannel> AddContactChannels { get; init; } = [];
    public IReadOnlyList<UpdateContactChannel> UpdateContactChannels { get; init; } = [];
    public IReadOnlyList<string> RemoveContactChannelIds { get; init; } = [];
    public IReadOnlyList<AddIdentifier> AddIdentifiers { get; init; } = [];
    public IReadOnlyList<string> RemoveIdentifierIds { get; init; } = [];
}
```

**Patch semantics (FR74):** The MCP tool only populates fields the AI agent explicitly provides. Null/empty means "don't change". This maps directly to the `UpdatePartyComposite` design where absent collections and null objects signal "no change".

### Patch merge strategy for PersonDetails / OrganizationDetails

When the AI agent provides partial person updates (e.g., only `firstName`), the tool must:
1. Query current `PartyDetail` from projection actor
2. Merge provided fields with current values
3. Send the complete `PersonDetails` to the command (the aggregate replaces the whole `PersonDetails`)

Example: AI sends `firstName = "Marie"` only → tool queries current party, gets `PersonDetails { FirstName: "Jean", LastName: "Dupont", ... }`, constructs `PersonDetails { FirstName: "Marie", LastName: "Dupont", ... }` (preserving all unspecified fields).

Same logic applies to `OrganizationDetails`.

**Edge case:** If the party hasn't been projected yet (projection returns null), and the AI tries to update, the tool should throw "Party not found" — can't update what doesn't exist.

### Command dispatch pattern (from PartiesController/CreatePartyMcpTool)

```csharp
// Construct SubmitCommand
var submitCommand = new SubmitCommand(
    Tenant: tenant,
    Domain: "party",
    AggregateId: partyId,
    CommandType: nameof(UpdatePartyComposite),  // or nameof(DeactivateParty)
    Payload: JsonSerializer.SerializeToUtf8Bytes(command),
    CorrelationId: Guid.NewGuid().ToString(),
    UserId: "mcp-agent");

// Dispatch and handle result
ICommandRouter commandRouter = services.GetRequiredService<ICommandRouter>();
CommandProcessingResult result = await commandRouter
    .RouteCommandAsync(submitCommand, cancellationToken)
    .ConfigureAwait(false);

if (!result.Accepted)
    throw new InvalidOperationException($"Update failed: {result.ErrorMessage}");
```

### Error handling pattern (consistent with Stories 5.1/5.2)

| Scenario | MCP Response |
|----------|-------------|
| Missing tenant | `InvalidOperationException`: "Authentication required. No tenant context found in the request." |
| Missing/invalid partyId | `InvalidOperationException`: "Party ID is required and must be a valid UUID." |
| Party not found (update) | `InvalidOperationException`: "Party not found. No party exists with ID '{partyId}'." |
| Party not found (delete) | `InvalidOperationException`: "Party not found. No party exists with ID '{partyId}'." |
| Invalid remove channel ID | `InvalidOperationException`: "Invalid contact channel ID '{id}'. Must be a valid UUID." |
| Invalid remove identifier ID | `InvalidOperationException`: "Invalid identifier ID '{id}'. Must be a valid UUID." |
| Validation failures | `InvalidOperationException`: "Validation failed: {list of FluentValidation errors}" |
| Command rejected | `InvalidOperationException`: "Update failed: {domain error message}" |
| Already deactivated (delete) | Return current `PartyDetail` — idempotent, no error |
| No changes specified (update) | `InvalidOperationException`: "No changes specified. Provide at least one field to update." |

### Critical architectural constraints (D11 — Translation Layer)

**ALLOWED in UpdatePartyMcpTool and DeletePartyMcpTool:**
- Input normalization (flat parameters → `UpdatePartyComposite`/`DeactivateParty` records)
- UUID generation for new ContactChannelId, IdentifierId
- Querying current party state for patch merge (via projection actor)
- Default value assignment for optional fields
- FluentValidation execution
- Command dispatch via ICommandRouter
- Response assembly (querying PartyDetail projection actor)
- Error message translation for AI-friendly responses

**FORBIDDEN in UpdatePartyMcpTool and DeletePartyMcpTool:**
- Business rules or domain validation logic (beyond input format normalization)
- Direct state store access (must go through command router and projection actors)
- Importing or using domain event types (`IEventPayload`, `IRejectionEvent`)
- Importing or using Server project types (`PartyAggregate`, `PartyState`, etc.)
- State caching or retry logic with domain awareness
- Duplicate validation that the aggregate already performs

### Validator details

**UpdatePartyCompositeValidator** enforces:
- `PartyId` must be a non-empty valid GUID
- Total sub-operations ≤ max (PersonDetails? + OrgDetails? + AddChannels + UpdateChannels + RemoveChannelIds + AddIdentifiers + RemoveIdentifierIds)
- Each `AddContactChannel`: `PartyId` (valid GUID), `ContactChannelId` (valid GUID), `Type` (valid enum), `Value` (non-empty)
- Each `UpdateContactChannel`: `PartyId` (valid GUID), `ContactChannelId` (valid GUID)
- Each remove ID: valid GUID
- Each `AddIdentifier`: `PartyId` (valid GUID), `IdentifierId` (valid GUID), `Type` (valid enum), `Value` (non-empty)

**DeactivatePartyValidator** enforces:
- `PartyId` must be a non-empty valid GUID

The MCP tools' input normalization must produce data that passes all these rules. Since the tools auto-generate all IDs as valid GUIDs, the main validation scenarios are: invalid partyId, invalid remove IDs, missing required fields for new channels/identifiers.

### Project Structure Notes

New files to create:
```
src/Hexalith.Parties/
├── Mcp/
│   ├── GetPartyMcpTool.cs          (exists)
│   ├── FindPartiesMcpTool.cs       (exists)
│   ├── McpSessionContext.cs        (exists)
│   ├── CreatePartyMcpTool.cs       (exists)
│   ├── UpdatePartyMcpTool.cs       (NEW)
│   └── DeletePartyMcpTool.cs       (NEW)
```

No other files need modification — the MCP server already uses assembly scanning (`WithToolsFromAssembly()`) so new tools are auto-discovered.

### Testing requirements

This story does **not** include MCP tool unit tests — those are covered in Story 5.4. However, the developer must ensure:
1. `dotnet build Hexalith.Parties.slnx` succeeds with zero errors and zero warnings
2. `dotnet test Hexalith.Parties.slnx` passes all existing tests (zero regressions)

### Anti-patterns to avoid

- **Do NOT create separate projects** — the tools go in `CommandApi/Mcp/`
- **Do NOT reference event types** from MCP tool code — only command types and model types
- **Do NOT reference Server namespace types** (`PartyAggregate`, `PartyState`) in MCP tool code
- **Do NOT duplicate validator logic** — use the existing validators via DI
- **Do NOT return just a correlation ID** — FR69 requires the complete `PartyDetail` in the response
- **Do NOT create new DTO types** for MCP responses — use existing `PartyDetail`
- **Do NOT add explicit tool registration** — assembly scanning handles it
- **Do NOT bypass authentication** — tenant must come from `McpSessionContext.Tenant.Value`
- **Do NOT implement business rules** for idempotent deactivation — check party state via projection and short-circuit if already deactivated
- **Do NOT send full PersonDetails/OrganizationDetails when only some fields changed** without first merging with current state — the aggregate replaces the whole object, so unmerged = data loss
- **Do NOT add MCP tool tests** — those are Story 5.4's scope

### Previous story intelligence (Story 5.2)

Key learnings from the previous story implementation:
- `McpSessionContext.Tenant.Value` for tenant extraction — throw `InvalidOperationException` if missing
- `ConfigureAwait(false)` required on all async calls to avoid CA2007
- The `IPartyDetailProjectionActor` proxy uses actor name `nameof(PartyDetailProjectionActor)` — this is a string constant, NOT the actual type reference
- Error handling pattern: throw `InvalidOperationException` — MCP SDK auto-converts to `CallToolResult` with `IsError = true`
- JSON serialization uses `McpSessionContext.JsonOptions` (camelCase, omit nulls, string enums)
- Build verification: `dotnet build Hexalith.Parties.slnx /warnaserror` must pass
- The `CreatePartyMcpTool` is 279 lines — `UpdatePartyMcpTool` will be similar size due to patch merge logic
- `dateOfBirth` parsing must use ISO 8601 and return clear validation error when invalid — established in Story 5.2 review
- Fallback `PartyDetail` construction pattern from Story 5.2: if projection hasn't caught up, construct from input data. For update_party, just return what projection gives (even if stale) — the AI agent can re-query later
- `#pragma warning disable MCPEXP002` is needed if referencing experimental MCP API — check if needed for new tool classes

### Git intelligence

Recent commits show the established pattern:
```
05d02f3 Merge pull request #20 from Hexalith/feat/story-5-2-create-party-mcp-tool
046705f feat: Implement Story 5.2 - create_party MCP tool
2b05f24 Merge pull request #19 from Hexalith/refactor/search-results-builder-and-projection-improvements
63288eb refactor: Extract PartySearchResultsBuilder and improve projection handler
082d3d1 Merge pull request #18 from Hexalith/implement-story-5-1-mcp-server-get-party-find-parties
```

Pattern: focused, additive changes. Story 5.3 adds two new files (`UpdatePartyMcpTool.cs` and `DeletePartyMcpTool.cs`) with no modifications to existing files.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-5.3`] — Story requirements and acceptance criteria
- [Source: `_bmad-output/planning-artifacts/architecture.md#D11`] — Translation layer boundary
- [Source: `_bmad-output/planning-artifacts/architecture.md#D9`] — Composite command with aggregate-side diff
- [Source: `_bmad-output/planning-artifacts/architecture.md#D12`] — All-or-nothing composite operations
- [Source: `_bmad-output/planning-artifacts/architecture.md#FR4`] — Soft delete (deactivation)
- [Source: `_bmad-output/planning-artifacts/architecture.md#FR69`] — Complete updated party returned
- [Source: `_bmad-output/planning-artifacts/architecture.md#FR74`] — Patch semantics for MCP update
- [Source: `_bmad-output/implementation-artifacts/5-2-create-party-mcp-tool.md`] — Previous story (create_party MCP tool)
- [Source: `src/Hexalith.Parties/Mcp/CreatePartyMcpTool.cs`] — Reference write MCP tool implementation
- [Source: `src/Hexalith.Parties/Mcp/GetPartyMcpTool.cs`] — Reference read MCP tool implementation
- [Source: `src/Hexalith.Parties/Mcp/McpSessionContext.cs`] — Tenant and JSON options
- [Source: `src/Hexalith.Parties.Contracts/Commands/UpdatePartyComposite.cs`] — Target update command type
- [Source: `src/Hexalith.Parties.Contracts/Commands/DeactivateParty.cs`] — Target deactivation command type
- [Source: `src/Hexalith.Parties/Validation/UpdatePartyCompositeValidator.cs`] — Update validator
- [Source: `src/Hexalith.Parties/Validation/DeactivatePartyValidator.cs`] — Deactivation validator
- [Source: `src/Hexalith.Parties/Controllers/PartiesController.cs`] — Command dispatch reference
- [Source: `src/Hexalith.Parties.Contracts/Models/PartyDetail.cs`] — Response model

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

Code review follow-up: added missing existing-contact-channel update support and hardened `delete_party` stale/idempotent response handling.

### Completion Notes List

- **Task 1 (UpdatePartyMcpTool):** Implemented `update_party` MCP tool with patch semantics (FR74). The tool queries current party state from projection, merges provided fields with existing values for `PersonDetails`/`OrganizationDetails`, constructs `UpdatePartyComposite` with add, update, and remove contact-channel operations plus identifier add/remove operations, validates via FluentValidation, dispatches via `ICommandRouter`, and returns complete `PartyDetail` (FR69). Includes input normalization for comma-separated remove IDs with GUID validation, explicit support for updating an existing contact channel, auto-generated UUIDs for new contact channels and identifiers, and ISO 8601 date parsing for `dateOfBirth`.
- **Task 2 (DeletePartyMcpTool):** Implemented `delete_party` MCP tool mapping to `DeactivateParty` command (soft delete — FR4). Idempotent behavior: checks projection for party existence and active status before dispatching, treats a post-command inactive projection as idempotent success, and guarantees the returned `PartyDetail` reports `IsActive = false` even if the projection is briefly stale.
- **Task 3 (Build & Regression):** `dotnet build Hexalith.Parties.slnx -warnaserror` passes. MCP regression tests were added for contact-channel updates and deactivation stale/idempotent behavior.
- **Architecture compliance (D11):** Both tools contain only input normalization and command construction. Zero references to domain event types, zero business rules or domain validation logic, zero Server namespace imports.

### Change Log

- 2026-03-06: Implemented Story 5.3 — Created `UpdatePartyMcpTool.cs` and `DeletePartyMcpTool.cs` completing CRUD MCP tool set
- 2026-03-06: Addressed AI code review findings — added existing-contact-channel update support, hardened `delete_party` stale/idempotent handling, and added MCP regression tests
- 2026-03-06: Synced story status and sprint tracking after successful review fixes

### File List

- `src/Hexalith.Parties/Mcp/UpdatePartyMcpTool.cs` (NEW)
- `src/Hexalith.Parties/Mcp/DeletePartyMcpTool.cs` (NEW)
- `tests/Hexalith.Parties.Tests/Mcp/UpdateAndDeletePartyMcpToolTests.cs` (NEW)
- `_bmad-output/implementation-artifacts/5-3-update-party-and-delete-party-mcp-tools.md` (UPDATED)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (UPDATED)

## Senior Developer Review (AI)

### Findings Resolved

- Added missing translation of existing contact-channel updates into `UpdatePartyComposite.UpdateContactChannels`.
- Hardened `delete_party` so successful responses always return an inactive `PartyDetail`, even during projection lag.
- Replaced overly broad idempotent rejection matching with projection-state verification.
- Added regression tests covering MCP update translation and deactivation stale/idempotent behavior.
