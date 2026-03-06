# Story 5.4: MCP Tools Tests & Architectural Fitness

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer,
I want comprehensive tests for all MCP tools and a CI-enforced architectural fitness test,
so that MCP tool behavior is verified and the translation layer boundary is machine-enforced.

## Acceptance Criteria

1. **Given** the `Hexalith.Parties.CommandApi.Tests` project, **when** MCP tool tests are implemented, **then** the following test classes exist under `Mcp/`:
   - `CreatePartyMcpToolTests` -- full input, partial input, missing required fields, generated channel IDs, complete response verification
   - `FindPartiesMcpToolTests` -- search query, empty query (list mode), match metadata presence, pagination
   - `GetPartyMcpToolTests` -- existing party, non-existent party, response shape
   - `UpdatePartyMcpToolTests` -- patch semantics (only specified fields), add channel only, remove channel only, mixed operations, generated IDs, non-existent party error
   - `DeletePartyMcpToolTests` -- active party, already deactivated (idempotent), non-existent party

2. **Given** the forgiving input normalization logic, **when** tested, **then** the following scenarios are covered:
   - Missing optional fields default to sensible values
   - Missing channel/identifier IDs are auto-generated as UUIDs
   - Partial person details (last name only, no first name) are accepted
   - Clear validation errors for missing required fields (party type)

3. **Given** the `FitnessTests/ArchitecturalFitnessTests.cs` file, **when** the MCP boundary test is implemented, **then** it verifies via reflection that the `CommandApi/Mcp/` namespace:
   - Has zero references to any type implementing `IEventPayload` or `IRejectionEvent`
  - References only approved translation-layer types from `Contracts.Commands`, `Contracts.Models`, `Contracts.ValueObjects`, `Contracts.Search`, `Projections.Abstractions`, and `Projections.Actors`
   **And** this test runs in CI and fails the build on violation (D11).

4. **Given** the architectural fitness tests, **when** all boundary tests are reviewed, **then** the following additional boundaries are verified:
   - Projection handlers (`Hexalith.Parties.Projections.Handlers` namespace) have zero DAPR references
   - Contracts project has zero runtime dependencies beyond `netstandard2.1` (verified via `.csproj` analysis or assembly reference check)
   - Client project has no references to Server, Projections, or CommandApi
   **And** all fitness tests are in `CommandApi.Tests/FitnessTests/`.

5. **And** all tests pass with `dotnet test`.

## Tasks / Subtasks

- [x] Task 1: Create `GetPartyMcpToolTests` (AC: #1, #2)
  - [x] 1.1: Create `tests/Hexalith.Parties.CommandApi.Tests/Mcp/GetPartyMcpToolTests.cs`
  - [x] 1.2: Test `GetPartyAsync_ValidPartyId_ReturnsCompletePartyDetailJson` -- mock `IPartyDetailProjectionActor.GetDetailAsync()` returning a full `PartyDetail`, verify JSON shape has all properties (id, type, isActive, displayName, sortName, personDetails, contactChannels, identifiers, createdAt, lastModifiedAt)
  - [x] 1.3: Test `GetPartyAsync_NonExistentParty_ThrowsNotFoundError` -- projection returns null, verify `InvalidOperationException` with message "Party not found. No party exists with ID '{partyId}'."
  - [x] 1.4: Test `GetPartyAsync_InvalidPartyIdFormat_ThrowsValidationError` -- pass "not-a-guid", verify `InvalidOperationException` about UUID format
  - [x] 1.5: Test `GetPartyAsync_MissingTenant_ThrowsAuthenticationError` -- no tenant set, verify "Authentication required" error

- [x] Task 2: Create `FindPartiesMcpToolTests` (AC: #1, #2)
  - [x] 2.1: Create `tests/Hexalith.Parties.CommandApi.Tests/Mcp/FindPartiesMcpToolTests.cs`
  - [x] 2.2: Test `FindPartiesAsync_WithQuery_ReturnsMatchingResultsWithMetadata` -- mock `IPartyIndexProjectionActor.GetEntriesAsync()` returning entries, query "Dupont", verify search results contain match metadata (matchedField, matchType)
  - [x] 2.3: Test `FindPartiesAsync_EmptyQuery_ReturnsPaginatedList` -- no query, verify `PagedResult` with items, page, pageSize, totalCount, totalPages
  - [x] 2.4: Test `FindPartiesAsync_Pagination_RespectsPageAndPageSize` -- multiple entries, page=2, pageSize=1, verify correct page returned
  - [x] 2.5: Test `FindPartiesAsync_PageSizeClamped_MaxIs100` -- pageSize=200, verify clamped to 100
  - [x] 2.6: Test `FindPartiesAsync_MissingTenant_ThrowsAuthenticationError`

- [x] Task 3: Expand `CreatePartyMcpToolTests` (AC: #1, #2)
  - [x] 3.1: Add `CreatePartyAsync_FullPersonInput_DispatchesCorrectCompositeCommand` -- provide all fields (type, firstName, lastName, dateOfBirth, email, phone, vatNumber), capture routed `SubmitCommand`, deserialize payload as `CreatePartyComposite`, verify all fields mapped correctly
  - [x] 3.2: Add `CreatePartyAsync_PartialInput_LastNameOnly_AcceptedSuccessfully` -- type "person", only lastName, verify command dispatched with null firstName
  - [x] 3.3: Add `CreatePartyAsync_OrganizationWithVat_ConstructsCorrectComposite` -- type "organization", legalName, vatNumber, verify `OrganizationDetails` populated and identifier added
  - [x] 3.4: Add `CreatePartyAsync_GeneratesUuidsForChannelAndIdentifier` -- provide email and vatNumber without explicit IDs, verify deserialized command has valid GUID ContactChannelId and IdentifierId
  - [x] 3.5: Add `CreatePartyAsync_SuccessfulCreate_ReturnsCompletePartyDetailJson` -- verify returned JSON has all expected `PartyDetail` properties

- [x] Task 4: Expand `UpdatePartyMcpToolTests` (AC: #1, #2)
  - [x] 4.1: Rename existing file: `UpdateAndDeletePartyMcpToolTests.cs` stays as-is (existing tests preserved). Create new `UpdatePartyMcpToolTests.cs` for expanded coverage
  - [x] 4.2: Test `UpdatePartyAsync_AddEmailOnly_ConstructsPatchWithOnlyAddChannels` -- only addEmail provided, verify command has only `AddContactChannels` populated, all else null/empty
  - [x] 4.3: Test `UpdatePartyAsync_RemoveChannelOnly_ConstructsPatchWithOnlyRemoveIds` -- only removeContactChannelIds provided, verify only `RemoveContactChannelIds` populated
  - [x] 4.4: Test `UpdatePartyAsync_MixedOperations_AllListsPopulated` -- addEmail + removeContactChannelIds + firstName update, verify all three operations present in command
  - [x] 4.5: Test `UpdatePartyAsync_PersonDetailsPatch_MergesWithCurrentState` -- provide only firstName, verify command `PersonDetails` has merged firstName with existing lastName from projection
  - [x] 4.6: Test `UpdatePartyAsync_GeneratesUuidForNewChannel` -- addEmail without explicit ID, verify auto-generated GUID
  - [x] 4.7: Test `UpdatePartyAsync_NonExistentParty_ThrowsNotFoundError` -- projection returns null, verify "Party not found" error
  - [x] 4.8: Test `UpdatePartyAsync_NoChangesSpecified_ThrowsValidationError` -- no fields provided, verify "No changes specified" error
  - [x] 4.9: Test `UpdatePartyAsync_MissingTenant_ThrowsAuthenticationError`
  - [x] 4.10: Test `UpdatePartyAsync_InvalidRemoveChannelId_ThrowsValidationError` -- removeContactChannelIds "not-a-guid", verify clear error

- [x] Task 5: Expand `DeletePartyMcpToolTests` (AC: #1)
  - [x] 5.1: Create `tests/Hexalith.Parties.CommandApi.Tests/Mcp/DeletePartyMcpToolTests.cs` with new tests (existing tests in `UpdateAndDeletePartyMcpToolTests.cs` are preserved)
  - [x] 5.2: Test `DeletePartyAsync_ActiveParty_DispatchesDeactivateCommand` -- verify `SubmitCommand` with `CommandType = nameof(DeactivateParty)` is dispatched
  - [x] 5.3: Test `DeletePartyAsync_AlreadyDeactivated_ReturnsImmediatelyWithoutCommand` -- projection returns `IsActive = false`, verify router is NOT called, verify returned JSON has `isActive = false`
  - [x] 5.4: Test `DeletePartyAsync_NonExistentParty_ThrowsNotFoundError` -- projection returns null
  - [x] 5.5: Test `DeletePartyAsync_InvalidPartyId_ThrowsValidationError` -- pass "not-a-guid"
  - [x] 5.6: Test `DeletePartyAsync_MissingTenant_ThrowsAuthenticationError`

- [x] Task 6: Create `ArchitecturalFitnessTests` (AC: #3, #4)
  - [x] 6.1: Create `tests/Hexalith.Parties.CommandApi.Tests/FitnessTests/ArchitecturalFitnessTests.cs`
  - [x] 6.2: Test `McpNamespace_HasZeroReferencesToEventTypes` -- use reflection to scan all types in `Hexalith.Parties.CommandApi.Mcp` namespace, inspect method parameters, return types, field types, and local variables/generic arguments for any type implementing `IEventPayload` or `IRejectionEvent`. Assert zero references found.
  - [x] 6.3: Test `McpNamespace_ReferencesOnlyCommandAndModelTypes` -- verify referenced types from Contracts are only from `Contracts.Commands`, `Contracts.Models`, `Contracts.ValueObjects`, `Contracts.Search`, or `Projections.Abstractions` namespaces
  - [x] 6.4: Test `ProjectionHandlers_HaveZeroDaprReferences` -- scan types in `Hexalith.Parties.Projections.Handlers` namespace, verify zero references to any `Dapr.*` assembly or namespace
  - [x] 6.5: Test `ContractsProject_HasNoRuntimeDependenciesBeyondNetstandard` -- load `Hexalith.Parties.Contracts` assembly, check `GetReferencedAssemblies()`, verify only `netstandard`, `System.*`, and `Hexalith.EventStore.Contracts` references
  - [x] 6.6: Test `ClientProject_HasNoReferencesToServerProjectionsOrCommandApi` -- verified via .csproj file analysis (Client project is currently empty, Epic 6), checking for forbidden project references to Server, Projections, or CommandApi

- [x] Task 7: Build and regression verification (AC: #5)
  - [x] 7.1: `dotnet build Hexalith.Parties.slnx` -- zero errors, zero warnings
  - [x] 7.2: `dotnet test Hexalith.Parties.slnx` -- all 271 tests pass (94 CommandApi, 118 Server, 36 Projections, 21 Contracts, 2 Integration)

## Dev Notes

### What this story does

This story adds comprehensive test coverage for all 5 MCP tools and creates CI-enforced architectural fitness tests that machine-verify project boundaries defined in D11. The MCP tool tests cover the full behavior surface: input validation, forgiving normalization, command construction correctness, projection querying, error handling, and response shape verification. The architectural fitness tests ensure boundaries are never violated by future changes.

### What already exists (do not recreate)

- **3 existing MCP test files** -- tests already exist for specific scenarios:
  - `tests/Hexalith.Parties.CommandApi.Tests/Mcp/CreatePartyMcpToolTests.cs` -- 3 tests: type missing validation, invalid dateOfBirth, fallback response display name
  - `tests/Hexalith.Parties.CommandApi.Tests/Mcp/UpdateAndDeletePartyMcpToolTests.cs` -- 4 tests: existing channel update, delete with stale projection, rejected+inactive idempotent, rejected+still-active error
  - **DO NOT delete or modify these files** -- new tests go in NEW test class files alongside them

- **MCP tool implementations** (the code being tested):
  - `src/Hexalith.Parties.CommandApi/Mcp/GetPartyMcpTool.cs` -- 50 lines, static class with `get_party` tool
  - `src/Hexalith.Parties.CommandApi/Mcp/FindPartiesMcpTool.cs` -- 77 lines, static class with `find_parties` tool
  - `src/Hexalith.Parties.CommandApi/Mcp/CreatePartyMcpTool.cs` -- static class with `create_party` tool
  - `src/Hexalith.Parties.CommandApi/Mcp/UpdatePartyMcpTool.cs` -- static class with `update_party` tool
  - `src/Hexalith.Parties.CommandApi/Mcp/DeletePartyMcpTool.cs` -- static class with `delete_party` tool
  - `src/Hexalith.Parties.CommandApi/Mcp/McpSessionContext.cs` -- `internal static class` with `AsyncLocal<string?> Tenant` and `JsonSerializerOptions JsonOptions`

- **Test infrastructure already in use** (follow these patterns exactly):
  - xUnit + Shouldly + NSubstitute (DO NOT add Moq or any other library)
  - `TenantScope` helper class using reflection to set `McpSessionContext.Tenant` -- duplicate in each test class as `private sealed class TenantScope : IDisposable` (this is the established pattern, not shared)
  - `BuildServices<TCommand>()` helper to construct `ServiceProvider` with mocked `ICommandRouter`, `IActorProxyFactory`, `IValidator<T>`
  - `CreateActorProxyFactory()` helper to set up `IActorProxyFactory` mock returning a mock projection actor
  - `CreatePartyDetail()` helper to build test `PartyDetail` instances
  - `InlineValidator<T>` from FluentValidation for pass-through validation in tests

- **Existing validators** (used by tools, may need mocking in tests):
  - `CreatePartyCompositeValidator`, `UpdatePartyCompositeValidator`, `DeactivatePartyValidator`
  - `AddContactChannelValidator`, `AddIdentifierValidator`, `UpdateContactChannelValidator`
  - Use `InlineValidator<T>` (always passes) in tests to isolate tool logic from validator logic

- **PartyTestData** -- `src/Hexalith.Parties.Testing/PartyTestData.cs` provides factory methods for valid test data

- **Project references in CommandApi.Tests.csproj** -- already references `CommandApi` and `Testing` projects. No new project references needed for Tasks 1-5. For Task 6 fitness tests, the test project already transitively references all needed assemblies (Contracts, Client is separate -- need to verify Client assembly is loadable).

### MCP tool test pattern (established in existing tests)

```csharp
using System.Reflection;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using FluentValidation;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.Parties.CommandApi.Mcp;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Abstractions;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.CommandApi.Tests.Mcp;

public sealed class ExampleMcpToolTests
{
    [Fact]
    public async Task ToolMethod_Scenario_ExpectedResultAsync()
    {
        // Arrange - tenant context
        using TenantScope _ = TenantScope.Create("tenant-a");

        // Arrange - mocks
        ICommandRouter router = Substitute.For<ICommandRouter>();
        router
            .RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CommandProcessingResult(true)));

        IPartyDetailProjectionActor projectionActor = Substitute.For<IPartyDetailProjectionActor>();
        projectionActor.GetDetailAsync().Returns(CreatePartyDetail("id", true));

        IActorProxyFactory actorProxyFactory = CreateActorProxyFactory(projectionActor);
        ServiceProvider services = BuildServices(router, actorProxyFactory, new InlineValidator<CommandType>());

        // Act
        string json = await ToolClass.ToolMethodAsync(/* params */, services: services);

        // Assert
        using JsonDocument document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("id").GetString().ShouldBe("id");
    }

    // Helper: TenantScope (copy from existing tests)
    private sealed class TenantScope : IDisposable { /* ... */ }

    // Helper: BuildServices
    private static ServiceProvider BuildServices<TCommand>(
        ICommandRouter router,
        IActorProxyFactory actorProxyFactory,
        IValidator<TCommand> validator)
        where TCommand : class
        => new ServiceCollection()
            .AddSingleton(router)
            .AddSingleton(actorProxyFactory)
            .AddSingleton(validator)
            .BuildServiceProvider();

    // Helper: CreateActorProxyFactory
    private static IActorProxyFactory CreateActorProxyFactory(
        IPartyDetailProjectionActor projectionActor)
    {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        factory
            .CreateActorProxy<IPartyDetailProjectionActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(projectionActor);
        return factory;
    }

    // Helper: CreatePartyDetail
    private static PartyDetail CreatePartyDetail(string partyId, bool isActive, string? channelId = null)
        => new()
        {
            Id = partyId,
            Type = PartyType.Person,
            IsActive = isActive,
            DisplayName = "Jean Dupont",
            SortName = "Dupont, Jean",
            PersonDetails = new PersonDetails { FirstName = "Jean", LastName = "Dupont" },
            ContactChannels = channelId is null ? [] : [new ContactChannel { Id = channelId, Type = ContactChannelType.Email, Value = "old@example.com", IsPreferred = false }],
            Identifiers = [],
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            LastModifiedAt = DateTimeOffset.UtcNow,
        };
}
```

### FindPartiesMcpTool test specifics

`FindPartiesMcpTool` uses `IPartyIndexProjectionActor` (not detail). The mock setup differs:

```csharp
IPartyIndexProjectionActor indexActor = Substitute.For<IPartyIndexProjectionActor>();
indexActor.GetEntriesAsync().Returns(new Dictionary<string, PartyIndexEntry>
{
    ["id-1"] = new PartyIndexEntry
    {
        Id = "id-1",
        Type = PartyType.Person,
        IsActive = true,
        DisplayName = "Jean Dupont",
        SearchableContactChannels = [new ContactChannel { Id = "ch1", Type = ContactChannelType.Email, Value = "jean@example.com", IsPreferred = true }],
        SearchableIdentifiers = [],
        CreatedAt = DateTimeOffset.UtcNow,
        LastModifiedAt = DateTimeOffset.UtcNow,
    },
});

IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
factory
    .CreateActorProxy<IPartyIndexProjectionActor>(Arg.Any<ActorId>(), Arg.Any<string>())
    .Returns(indexActor);
```

The `find_parties` tool returns different JSON shapes:
- **With query**: `PartySearchResult[]` with `party` and `matches` properties
- **Without query**: `PagedResult<PartyIndexEntry>` with `items`, `page`, `pageSize`, `totalCount`, `totalPages`

### Architectural fitness test approach

Use **reflection-based assembly scanning** in the CommandApi.Tests project. The test project already references all needed assemblies transitively.

**MCP boundary test (D11):**
```csharp
// Get the CommandApi assembly
Assembly mcpAssembly = typeof(GetPartyMcpTool).Assembly;

// Get all types in Mcp namespace
Type[] mcpTypes = mcpAssembly.GetTypes()
    .Where(t => t.Namespace == "Hexalith.Parties.CommandApi.Mcp")
    .ToArray();

// Get all types implementing IEventPayload (from EventStore.Contracts)
Type eventPayloadInterface = typeof(IEventPayload);

// For each MCP type, scan all methods, parameters, fields, properties
// Verify none reference types assignable to IEventPayload or IRejectionEvent
```

**Projection handler DAPR test:**
```csharp
Assembly projectionsAssembly = typeof(PartyDetailProjectionHandler).Assembly;
Type[] handlerTypes = projectionsAssembly.GetTypes()
    .Where(t => t.Namespace == "Hexalith.Parties.Projections.Handlers")
    .ToArray();

// Scan method parameters, return types, field types
// Verify none are from Dapr.* namespaces
```

**Key types for imports in fitness tests:**
- `Hexalith.EventStore.Contracts.Events.IEventPayload` -- the interface to check against
- `Hexalith.EventStore.Contracts.Events.IRejectionEvent` -- extends IEventPayload
- `Hexalith.Parties.CommandApi.Mcp.GetPartyMcpTool` -- anchor type for CommandApi assembly
- `Hexalith.Parties.Projections.Handlers.PartyDetailProjectionHandler` -- anchor type for Projections assembly
- `Hexalith.Parties.Contracts.Commands.CreatePartyComposite` -- anchor type for Contracts assembly

**Client project boundary test note:** The CommandApi.Tests project does NOT reference `Hexalith.Parties.Client`. To test Client boundaries, either:
- Add a `<ProjectReference>` to Client in the test .csproj, OR
- Use `Assembly.Load("Hexalith.Parties.Client")` if it's in the output (it won't be unless referenced)
- **Recommended:** Add a project reference to Client in the test .csproj for this fitness test

### Testing conventions (MUST follow)

- **Test class naming:** `{ClassUnderTest}Tests` (e.g., `GetPartyMcpToolTests`)
- **Test method naming:** `{Method}_{Scenario}_{ExpectedResult}Async` for async tests
- **One test class per production class** (except existing `UpdateAndDeletePartyMcpToolTests` which stays)
- **File name = class name**
- **File-scoped namespaces**
- **`public sealed class`** for test classes
- **`[Fact]`** attribute for each test
- **Shouldly assertions:** `value.ShouldBe(expected)`, `Should.ThrowAsync<T>()`, `value.ShouldNotBeNull()`
- **NSubstitute mocking:** `Substitute.For<T>()`, `Arg.Any<T>()`, `Arg.Do<T>()`, `.Returns()`
- **ConfigureAwait(false)** is not used in test code (only in production code)
- Global using for `Xunit` is already configured in the .csproj

### Command dispatch verification pattern

To capture and inspect the routed command:

```csharp
SubmitCommand? routedCommand = null;
ICommandRouter router = Substitute.For<ICommandRouter>();
router
    .RouteCommandAsync(Arg.Do<SubmitCommand>(cmd => routedCommand = cmd), Arg.Any<CancellationToken>())
    .Returns(Task.FromResult(new CommandProcessingResult(true)));

// ... invoke tool ...

routedCommand.ShouldNotBeNull();
routedCommand!.CommandType.ShouldBe(nameof(CreatePartyComposite));
routedCommand.Domain.ShouldBe("party");

// Deserialize and inspect payload
var command = JsonSerializer.Deserialize<CreatePartyComposite>(routedCommand.Payload);
command.ShouldNotBeNull();
command!.PartyId.ShouldNotBeNullOrWhiteSpace();
Guid.TryParse(command.PartyId, out _).ShouldBeTrue(); // auto-generated UUID
```

### Error handling patterns to test

| Scenario | Expected Exception | Message Pattern |
|----------|-------------------|----------------|
| Missing tenant (all tools) | `InvalidOperationException` | "Authentication required. No tenant context found in the request." |
| Invalid partyId format | `InvalidOperationException` | "Invalid party ID format. Expected a UUID..." |
| Party not found (get/update) | `InvalidOperationException` | "Party not found. No party exists with ID '{id}'." |
| Missing party type (create) | `InvalidOperationException` | "Party type is required. Must be 'Person' or 'Organization'." |
| Invalid dateOfBirth (create) | `InvalidOperationException` | "Date of birth must be a valid ISO 8601 date..." |
| No changes specified (update) | `InvalidOperationException` | "No changes specified. Provide at least one field to update." |
| Command rejected | `InvalidOperationException` | Error message from `CommandProcessingResult.ErrorMessage` |

### Critical architectural constraints (D11 -- must be enforced by fitness tests)

**ALLOWED in MCP namespace (`Hexalith.Parties.CommandApi.Mcp`):**
- Types from `Hexalith.Parties.Contracts.Commands` (CreatePartyComposite, UpdatePartyComposite, DeactivateParty, etc.)
- Types from `Hexalith.Parties.Contracts.Models` (PartyDetail, PartyIndexEntry, PartySearchResult, etc.)
- Types from `Hexalith.Parties.Contracts.ValueObjects` (PersonDetails, OrganizationDetails, ContactChannel, etc.)
- Types from `Hexalith.Parties.Contracts.Search` (PagedResult, MatchMetadata)
- Types from `Hexalith.Parties.Projections.Abstractions` (IPartyDetailProjectionActor, IPartyIndexProjectionActor)
- Types from `Hexalith.Parties.Projections.Actors` (nameof reference only for actor proxy creation)

**FORBIDDEN in MCP namespace:**
- Any type implementing `IEventPayload` (e.g., `PartyCreated`, `PersonDetailsUpdated`, etc.)
- Any type implementing `IRejectionEvent` (e.g., `PartyNotFound`, `PartyTypeMismatch`, etc.)
- Any type from `Hexalith.Parties.Server` namespace (e.g., `PartyAggregate`, `PartyState`)
- Direct DAPR state store access types

### Five architectural fitness tests (from architecture.md)

1. **MCP layer: zero event type references** -- `CommandApi/Mcp/` namespace types must not reference `IEventPayload`/`IRejectionEvent` implementors
2. **Projection handlers: zero DAPR references** -- `Projections.Handlers` namespace types must not reference `Dapr.*`
3. **Contracts project: minimal dependencies** -- only `netstandard2.1` and `Hexalith.EventStore.Contracts`
4. **Client project: no server-side references** -- no `Server`, `Projections`, or `CommandApi` assembly references
5. **Test tier compliance** -- Tier 1 tests have zero infrastructure dependencies (covered by existing test structure)

### Project Structure Notes

New files to create:
```
tests/Hexalith.Parties.CommandApi.Tests/
+-- Mcp/
|   +-- CreatePartyMcpToolTests.cs       (EXISTS - expand with new tests)
|   +-- FindPartiesMcpToolTests.cs       (NEW)
|   +-- GetPartyMcpToolTests.cs          (NEW)
|   +-- UpdatePartyMcpToolTests.cs       (NEW)
|   +-- DeletePartyMcpToolTests.cs       (NEW)
|   +-- UpdateAndDeletePartyMcpToolTests.cs (EXISTS - do NOT modify)
+-- FitnessTests/
    +-- ArchitecturalFitnessTests.cs     (NEW)
```

Potential .csproj modification:
- Add `<ProjectReference>` to `Hexalith.Parties.Client.csproj` in `CommandApi.Tests.csproj` if needed for Client boundary fitness test (alternatively, test via `.csproj` file parsing)

### Anti-patterns to avoid

- **Do NOT delete or modify** existing test files (`CreatePartyMcpToolTests.cs`, `UpdateAndDeletePartyMcpToolTests.cs`) -- add NEW test classes alongside them
- **Do NOT use Moq** -- project uses NSubstitute exclusively
- **Do NOT share TenantScope** across test files -- each test class has its own private nested `TenantScope` class (established pattern)
- **Do NOT test validator logic** in MCP tool tests -- use `InlineValidator<T>` (pass-through) to isolate tool translation logic from validator rules
- **Do NOT reference Server types** in fitness test assertions -- use reflection and assembly scanning
- **Do NOT use string-based type checking** in fitness tests -- use `typeof(IEventPayload).IsAssignableFrom(type)` for reliable type hierarchy checks
- **Do NOT create integration tests** -- all Story 5.4 tests are Tier 1/2 unit tests in CommandApi.Tests
- **Do NOT add test framework packages** -- xUnit, Shouldly, NSubstitute are already in the .csproj

### Previous story intelligence (Story 5.3)

Key learnings from the previous story:
- `McpSessionContext.Tenant.Value` accessed via reflection in tests using `TenantScope` pattern
- `ConfigureAwait(false)` on all async calls in production code -- not needed in tests
- `IPartyDetailProjectionActor` proxy uses actor name `nameof(PartyDetailProjectionActor)` -- a string constant, not a type reference
- `CommandProcessingResult(true)` for accepted, `CommandProcessingResult(false, "error message")` for rejected
- `SubmitCommand` record has: `Tenant`, `Domain`, `AggregateId`, `CommandType`, `Payload` (byte[]), `CorrelationId`, `UserId`
- JSON serialization uses `McpSessionContext.JsonOptions` (camelCase, omit nulls, string enums)
- The update tool queries current party state for patch merge before constructing the command
- The delete tool checks `IsActive` flag and short-circuits for already-deactivated parties
- `#pragma warning disable MCPEXP002` may be needed for experimental MCP API references

### Git intelligence

Recent commits show consistent pattern:
```
4281ca4 Merge pull request #21 from Hexalith/feat/story-5-3-update-party-and-delete-party-mcp-tools
b6f5dea feat: Implement Story 5.3 - update_party and delete_party MCP tools
05d02f3 Merge pull request #20 from Hexalith/feat/story-5-2-create-party-mcp-tool
046705f feat: Implement Story 5.2 - create_party MCP tool
2b05f24 Merge pull request #19 from Hexalith/refactor/search-results-builder-and-projection-improvements
```

Stories 5.1-5.3 each added MCP tool code. Story 5.4 adds only test files -- no production code changes expected. Branch naming pattern: `feat/story-5-4-mcp-tools-tests-and-architectural-fitness`.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-5.4`] -- Story requirements and acceptance criteria
- [Source: `_bmad-output/planning-artifacts/architecture.md#D11`] -- MCP translation layer boundary
- [Source: `_bmad-output/planning-artifacts/architecture.md#D18`] -- Projection testability via pure handlers
- [Source: `_bmad-output/planning-artifacts/architecture.md#D19`] -- Composite command test matrix
- [Source: `_bmad-output/planning-artifacts/architecture.md#Architectural-Fitness-Tests`] -- 5 fitness tests enforced in CI
- [Source: `_bmad-output/implementation-artifacts/5-3-update-party-and-delete-party-mcp-tools.md`] -- Previous story
- [Source: `tests/Hexalith.Parties.CommandApi.Tests/Mcp/CreatePartyMcpToolTests.cs`] -- Existing create tool tests (pattern reference)
- [Source: `tests/Hexalith.Parties.CommandApi.Tests/Mcp/UpdateAndDeletePartyMcpToolTests.cs`] -- Existing update/delete tests (pattern reference)
- [Source: `src/Hexalith.Parties.CommandApi/Mcp/GetPartyMcpTool.cs`] -- Get tool implementation
- [Source: `src/Hexalith.Parties.CommandApi/Mcp/FindPartiesMcpTool.cs`] -- Find tool implementation
- [Source: `src/Hexalith.Parties.CommandApi/Mcp/CreatePartyMcpTool.cs`] -- Create tool implementation
- [Source: `src/Hexalith.Parties.CommandApi/Mcp/UpdatePartyMcpTool.cs`] -- Update tool implementation
- [Source: `src/Hexalith.Parties.CommandApi/Mcp/DeletePartyMcpTool.cs`] -- Delete tool implementation
- [Source: `src/Hexalith.Parties.CommandApi/Mcp/McpSessionContext.cs`] -- Tenant and JSON context
- [Source: `Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Events/IEventPayload.cs`] -- Event interface for fitness test
- [Source: `Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Events/IRejectionEvent.cs`] -- Rejection event interface
- [Source: `src/Hexalith.Parties.Contracts/Hexalith.Parties.Contracts.csproj`] -- Contracts project references
- [Source: `src/Hexalith.Parties.Client/Hexalith.Parties.Client.csproj`] -- Client project references
- [Source: `src/Hexalith.Parties.Projections/Hexalith.Parties.Projections.csproj`] -- Projections project references

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- Build error CS1657: `using TenantScope _ =` conflicts with `out _` discards in same scope. Fixed by renaming to `using TenantScope tenantScope =` in affected tests.
- Strengthened architectural fitness tests to inspect method-body local variable types in addition to signatures, fields, and properties.
- Strengthened the Client boundary fitness test to analyze declared project references from `.csproj` XML instead of using raw substring matching.

### Completion Notes List

- Created 4 new test files and expanded 1 existing test file with comprehensive MCP tool test coverage
- Created architectural fitness tests verifying 5 boundary constraints via reflection and file analysis
- All 271 tests pass across the solution with zero regressions
- No production code changes -- this story is test-only
- Existing test files (CreatePartyMcpToolTests.cs, UpdateAndDeletePartyMcpToolTests.cs) preserved intact; new tests added alongside
- Senior review fixes applied: fitness tests now inspect local variable types, and story boundary documentation now matches the architecture decision for MCP translation-layer allowances

### Implementation Plan

1. Task 1: Created GetPartyMcpToolTests with 4 tests covering valid retrieval (full JSON shape verification), non-existent party, invalid ID format, and missing tenant
2. Task 2: Created FindPartiesMcpToolTests with 5 tests covering search with match metadata, paginated list mode, pagination mechanics, page size clamping to 100, and missing tenant
3. Task 3: Expanded CreatePartyMcpToolTests with 5 new tests (total 8) covering full person input command dispatch, partial input (lastName only), organization with VAT, auto-generated UUIDs for channels/identifiers, and complete response JSON shape
4. Task 4: Created UpdatePartyMcpToolTests with 10 tests covering add-only, remove-only, mixed operations, patch merge with current state, auto-generated UUID, not-found, no-changes, missing tenant, and invalid remove channel ID
5. Task 5: Created DeletePartyMcpToolTests with 5 tests covering active party dispatch, already-deactivated idempotent return, non-existent party, invalid ID, and missing tenant
6. Task 6: Created ArchitecturalFitnessTests with 5 reflection-based boundary tests: MCP event-type exclusion, MCP allowed-namespace validation, projection handler DAPR exclusion, Contracts minimal dependencies, Client server-side exclusion
7. Task 7: Verified zero build errors and all 271 tests pass

### File List

- tests/Hexalith.Parties.CommandApi.Tests/Mcp/GetPartyMcpToolTests.cs (NEW)
- tests/Hexalith.Parties.CommandApi.Tests/Mcp/FindPartiesMcpToolTests.cs (NEW)
- tests/Hexalith.Parties.CommandApi.Tests/Mcp/CreatePartyMcpToolTests.cs (MODIFIED - added 5 tests, 2 helpers)
- tests/Hexalith.Parties.CommandApi.Tests/Mcp/UpdatePartyMcpToolTests.cs (NEW)
- tests/Hexalith.Parties.CommandApi.Tests/Mcp/DeletePartyMcpToolTests.cs (NEW)
- tests/Hexalith.Parties.CommandApi.Tests/FitnessTests/ArchitecturalFitnessTests.cs (NEW, later strengthened during senior review)
- _bmad-output/implementation-artifacts/5-4-mcp-tools-tests-and-architectural-fitness.md (MODIFIED - senior review corrections and closure)
- _bmad-output/implementation-artifacts/sprint-status.yaml (MODIFIED - story status sync)

## Senior Developer Review (AI)

### Reviewer

GitHub Copilot

### Review Date

2026-03-06

### Outcome

Approved after fixes.

### Findings Addressed

- Strengthened the MCP event-boundary fitness test to inspect method-body local variable types in addition to method signatures, fields, and properties.
- Strengthened the projection-handler DAPR boundary fitness test to inspect method-body local variable types.
- Strengthened the client boundary test to inspect declared project references from XML instead of using raw text matching.
- Corrected the story boundary wording to match the approved architecture for MCP translation-layer dependencies.
- Corrected implementation notes and file tracking to reflect the actual review fixes and lifecycle status.

## Change Log

- 2026-03-06: Implemented Story 5.4 - Added comprehensive MCP tool tests (30 new tests across 5 tool classes) and 5 architectural fitness tests enforcing D11 boundary constraints. Total test count in CommandApi.Tests: 94.
- 2026-03-06: Senior review fixes applied - strengthened architectural fitness tests to inspect local variable types and improved client boundary verification; story advanced to done.
