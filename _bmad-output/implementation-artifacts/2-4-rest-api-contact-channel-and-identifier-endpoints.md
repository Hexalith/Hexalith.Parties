# Story 2.4: REST API -- Contact Channel & Identifier Endpoints

Status: done

<!-- Key Context: This story extends PartiesController with 5 new POST command endpoints (AddContactChannel, UpdateContactChannel, RemoveContactChannel, AddIdentifier, RemoveIdentifier) following the exact patterns established in Story 1.6. All aggregate Handle methods already exist (Story 2.1, 2.2). All contracts already exist. No new command/event types needed. Preferred channel marking is handled via AddContactChannel (IsPreferred=true) and UpdateContactChannel (IsPreferred=true) -- no separate endpoint required. -->

## Story

As a developer,
I want REST API endpoints for all contact channel and identifier operations,
So that I can manage party contact information and identifiers from any programming language.

## Acceptance Criteria

1. **Given** the `PartiesController`, **When** contact channel and identifier endpoints are added, **Then** POST endpoints exist for: `AddContactChannel`, `UpdateContactChannel`, `RemoveContactChannel`, `AddIdentifier`, `RemoveIdentifier` **And** preferred channel marking is handled via `AddContactChannel` (IsPreferred=true) and `UpdateContactChannel` (IsPreferred=true) -- no separate endpoint required.

2. **Given** a valid `AddContactChannel` command with type `Email`, **When** sent via POST to `/api/v1/parties/{id}/add-contact-channel`, **Then** the response is `202 Accepted` with `correlationId` **And** the contact channel is added to the party aggregate.

3. **Given** an `AddContactChannel` command with empty `ContactChannelId`, **When** sent via POST, **Then** the response is `400 Bad Request` with ProblemDetails describing the validation error.

4. **Given** a `RemoveContactChannel` command referencing a non-existent channel, **When** sent via POST, **Then** the response is `422 Unprocessable Entity` with a domain rejection message.

5. **Given** a valid `AddIdentifier` command with type `VAT`, **When** sent via POST to `/api/v1/parties/{id}/add-identifier`, **Then** the response is `202 Accepted` with `correlationId`.

6. **Given** an `AddIdentifier` command with empty `Value`, **When** sent via POST, **Then** the response is `400 Bad Request` with ProblemDetails.

7. **Given** all new endpoints, **When** all endpoints follow Story 1.6 patterns, **Then**: `202 Accepted` on success with `correlationId`, ProblemDetails (RFC 9457) for errors, FluentValidation on entry (structural validation), domain rejection returns `422` with corrective action, authentication required -- no anonymous access, tenant extracted from JWT claims.

8. **Given** all tests in the solution, **When** `dotnet test` is run, **Then** all tests pass (existing 98 + new tests), zero regressions.

9. **Given** the new validators, **When** reviewed, **Then** FluentValidation uses assembly scanning (auto-discovery, no explicit registration).

10. **Given** personal data fields (`Value` on contact channel commands, `Value` on identifier commands), **When** application logs are written, **Then** `[PersonalData]`-attributed fields are masked or excluded from all log output.

## Tasks / Subtasks

- [x] Task 1: Add contact channel POST endpoints to PartiesController (AC: #1, #2, #4, #7)
  - [x] 1.1: `POST /{id}/add-contact-channel` --> AddContactChannel -- set `command.PartyId` from URL `{id}`, dispatch via `DispatchCommandAsync`, return 202
  - [x] 1.2: `POST /{id}/update-contact-channel` --> UpdateContactChannel -- set `command.PartyId` from URL `{id}`
  - [x] 1.3: `POST /{id}/remove-contact-channel` --> RemoveContactChannel -- set `command.PartyId` from URL `{id}`

- [x] Task 2: Add identifier POST endpoints to PartiesController (AC: #1, #5, #7)
  - [x] 2.1: `POST /{id}/add-identifier` --> AddIdentifier -- set `command.PartyId` from URL `{id}`
  - [x] 2.2: `POST /{id}/remove-identifier` --> RemoveIdentifier -- set `command.PartyId` from URL `{id}`

- [x] Task 3: Create FluentValidation validators (AC: #3, #6, #9)
  - [x] 3.1: `AddContactChannelValidator` -- PartyId (GUID), ContactChannelId (not empty), Type (valid enum, not default), Value (not empty)
  - [x] 3.2: `UpdateContactChannelValidator` -- PartyId (GUID), ContactChannelId (not empty)
  - [x] 3.3: `RemoveContactChannelValidator` -- PartyId (GUID), ContactChannelId (not empty)
  - [x] 3.4: `AddIdentifierValidator` -- PartyId (GUID), IdentifierId (not empty), Type (valid enum), Value (not empty)
  - [x] 3.5: `RemoveIdentifierValidator` -- PartyId (GUID), IdentifierId (not empty)
  - [x] 3.6: All validators auto-discovered via assembly scanning (no explicit registration needed -- existing `AddValidatorsFromAssemblyContaining<CreatePartyValidator>()` covers them)

- [x] Task 4: Build and regression verification (AC: #8)
  - [x] 4.1: `dotnet build Hexalith.Parties.slnx` -- zero errors, zero new warnings
  - [x] 4.2: `dotnet test Hexalith.Parties.slnx` -- all tests pass (98 existing + any new), zero regressions

#### Review Follow-ups (AI)

- [x] [AI-Review][HIGH] Add CommandApi tests that exercise all 5 new REST endpoints (happy path + validation + domain rejection) to validate AC #2, #3, #4, #5, #6, and #7. Implemented in `tests/Hexalith.Parties.CommandApi.Tests/Controllers/PartiesControllerProblemDetailsTests.cs` with theory-based coverage for all five routes.
- [x] [AI-Review][MEDIUM] Align endpoint method names with controller convention. Originally added `Async` suffix; second review corrected to drop `Async` suffix to match the dominant pre-existing convention (6/7 POST methods have no suffix). Fixed in `PartiesController.cs`.
- [x] [AI-Review][MEDIUM] Keep Dev Agent Record `File List` in sync with actual git modifications; file list updated below.

## Dev Notes

### What This Story Does

This story extends the existing `PartiesController` with 5 new POST endpoints for contact channel and identifier operations. All command types, events, aggregate Handle methods, and state Apply methods already exist from Stories 2.1 and 2.2. This story is **CommandApi only** -- no modifications to Contracts, Server, Testing, or Projections projects.

### Command Dispatch -- Follow Existing PartiesController Pattern Exactly

The controller already has a generic `DispatchCommandAsync<TCommand>` method. All new endpoints MUST use this exact same method. Study the existing endpoints in `PartiesController.cs`.

**Dispatch flow (already implemented):**
```
POST request -> Validate via ValidateCommandAsync -> Extract tenant from JWT
  -> Create SubmitCommand envelope -> ICommandRouter.RouteCommandAsync
  -> Return 202 Accepted with correlationId (success) or 422 ProblemDetails (rejection)
```

**Existing endpoint pattern to follow:**
```csharp
[HttpPost("{id}/update-person-details")]
[ProducesResponseType(StatusCodes.Status202Accepted)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
public async Task<IActionResult> UpdatePersonDetailsAsync(
    [FromRoute] string id,
    [FromBody] UpdatePersonDetails command,
    CancellationToken cancellationToken)
{
    command = command with { PartyId = id };
    return await DispatchCommandAsync(
        id,
        nameof(UpdatePersonDetails),
        command,
        cancellationToken);
}
```

**New endpoints must follow this exact same pattern:**
- Route: `{id}/{kebab-case-action}`
- Set `command.PartyId` from URL `{id}` using `command = command with { PartyId = id }`
- Call `DispatchCommandAsync(id, nameof(CommandType), command, cancellationToken)`
- Same `ProducesResponseType` attributes (202, 400, 401, 422)
- Method naming: `{Action}Async` (e.g., `AddContactChannelAsync`)

### URL Routing for New Endpoints

| HTTP Method | Route | Command | PartyId Source |
|---|---|---|---|
| POST | `/api/v1/parties/{id}/add-contact-channel` | AddContactChannel | URL path `{id}` |
| POST | `/api/v1/parties/{id}/update-contact-channel` | UpdateContactChannel | URL path `{id}` |
| POST | `/api/v1/parties/{id}/remove-contact-channel` | RemoveContactChannel | URL path `{id}` |
| POST | `/api/v1/parties/{id}/add-identifier` | AddIdentifier | URL path `{id}` |
| POST | `/api/v1/parties/{id}/remove-identifier` | RemoveIdentifier | URL path `{id}` |

### Preferred Channel Marking

The epics mention "preferred channel marking" as a feature. This is NOT a separate endpoint. The `AddContactChannel` command has an `IsPreferred` property -- when set to `true`, the aggregate emits both `ContactChannelAdded` and `PreferredContactChannelChanged` events. Similarly, `UpdateContactChannel` has an `IsPreferred?` property. No additional endpoint needed.

### Command Contracts (DO NOT MODIFY -- already exist)

**AddContactChannel** (`src/Hexalith.Parties.Contracts/Commands/AddContactChannel.cs`):
```csharp
public sealed record AddContactChannel
{
    public required string PartyId { get; init; }
    public required string ContactChannelId { get; init; }
    public required ContactChannelType Type { get; init; }
    [PersonalData]
    public required string Value { get; init; }
    public bool IsPreferred { get; init; }
}
```

**UpdateContactChannel** (`src/Hexalith.Parties.Contracts/Commands/UpdateContactChannel.cs`):
```csharp
public sealed record UpdateContactChannel
{
    public required string PartyId { get; init; }
    public required string ContactChannelId { get; init; }
    public ContactChannelType? Type { get; init; }
    [PersonalData]
    public string? Value { get; init; }
    public bool? IsPreferred { get; init; }
}
```

**RemoveContactChannel** (`src/Hexalith.Parties.Contracts/Commands/RemoveContactChannel.cs`):
```csharp
public sealed record RemoveContactChannel
{
    public required string PartyId { get; init; }
    public required string ContactChannelId { get; init; }
}
```

**AddIdentifier** (`src/Hexalith.Parties.Contracts/Commands/AddIdentifier.cs`):
```csharp
public sealed record AddIdentifier
{
    public required string PartyId { get; init; }
    public required string IdentifierId { get; init; }
    public required IdentifierType Type { get; init; }
    [PersonalData]
    public required string Value { get; init; }
}
```

**RemoveIdentifier** (`src/Hexalith.Parties.Contracts/Commands/RemoveIdentifier.cs`):
```csharp
public sealed record RemoveIdentifier
{
    public required string PartyId { get; init; }
    public required string IdentifierId { get; init; }
}
```

### FluentValidation -- Structural Only (DO NOT Duplicate Domain Logic)

Two validation layers -- NEVER overlap:
1. **FluentValidation** at API entry: structural checks (required fields, format)
2. **Domain validation** in aggregate Handle: business rules (party existence, duplicate channels, etc.)

**Validator pattern to follow (from existing `CreatePartyValidator.cs`):**
```csharp
public sealed class CreatePartyValidator : AbstractValidator<CreateParty>
{
    public CreatePartyValidator()
    {
        RuleFor(x => x.PartyId)
            .NotEmpty()
            .Must(id => Guid.TryParse(id, out _))
            .WithMessage("PartyId must be a valid GUID");
        // ...
    }
}
```

**Validators to create:**

1. **AddContactChannelValidator:**
```csharp
public sealed class AddContactChannelValidator : AbstractValidator<AddContactChannel>
{
    public AddContactChannelValidator()
    {
        RuleFor(x => x.PartyId)
            .NotEmpty()
            .Must(id => Guid.TryParse(id, out _))
            .WithMessage("PartyId must be a valid GUID");

        RuleFor(x => x.ContactChannelId)
            .NotEmpty()
            .WithMessage("ContactChannelId is required");

        RuleFor(x => x.Type)
            .IsInEnum()
            .WithMessage("Type must be a valid ContactChannelType");

        RuleFor(x => x.Value)
            .NotEmpty()
            .WithMessage("Value is required");
    }
}
```

2. **UpdateContactChannelValidator:** PartyId (GUID), ContactChannelId (not empty). No validation on optional fields (Type, Value, IsPreferred) -- they are all nullable/optional by design.

3. **RemoveContactChannelValidator:** PartyId (GUID), ContactChannelId (not empty).

4. **AddIdentifierValidator:** PartyId (GUID), IdentifierId (not empty), Type (valid enum), Value (not empty).

5. **RemoveIdentifierValidator:** PartyId (GUID), IdentifierId (not empty).

**Assembly scanning:** No explicit registration needed. The existing `AddValidatorsFromAssemblyContaining<CreatePartyValidator>()` in `PartiesServiceCollectionExtensions.cs` auto-discovers all validators in the same assembly.

### Rejection Events (for ProblemDetails messages)

The aggregate Handle methods for contact channels and identifiers return these rejection events:
- `PartyNotFound` -- party does not exist (null state)
- `ContactChannelNotFound` -- channel ID not found in party state (update/remove)
- `IdentifierNotFound` -- identifier ID not found in party state (remove)
- `DomainResult.NoOp()` -- duplicate add (idempotent, returns 202 not rejection)

The existing `DispatchCommandAsync` already maps rejection to 422 ProblemDetails and NoOp to 202 -- no additional error handling code needed.

### Project Structure Notes

**New files (this story):**
```
src/Hexalith.Parties.CommandApi/
├── Controllers/
│   └── PartiesController.cs                         <- MODIFY: add 5 POST endpoints
└── Validation/
    ├── AddContactChannelValidator.cs                <- NEW
    ├── UpdateContactChannelValidator.cs              <- NEW
    ├── RemoveContactChannelValidator.cs              <- NEW
    ├── AddIdentifierValidator.cs                    <- NEW
    └── RemoveIdentifierValidator.cs                 <- NEW
```

**No modifications to:**
- `src/Hexalith.Parties.Contracts/` -- all command types already exist
- `src/Hexalith.Parties.Server/` -- all Handle methods already exist
- `src/Hexalith.Parties.Testing/` -- no test data changes needed
- `src/Hexalith.Parties.Projections/` -- not in scope
- `src/Hexalith.Parties.CommandApi/Extensions/` -- no DI changes needed (assembly scanning auto-discovers validators)

### Architecture Compliance

- **Thin controller:** Controllers dispatch commands only -- zero domain logic in the controller.
- **FluentValidation structural only:** Validate presence, format, enum ranges. Do NOT duplicate aggregate business rules.
- **No PII in logs:** `Value` on AddContactChannel, UpdateContactChannel, and AddIdentifier is `[PersonalData]`. Never log these values.
- **202 Accepted for commands:** Never return 200 OK for successful commands.
- **Tenant from JWT only:** PartyId from URL path, tenant from JWT claims. Never trust tenant from request body.
- **Fail-closed auth:** All endpoints require `[Authorize]` (already on the controller class).
- **Content types:** `application/json` for responses, `application/problem+json` for errors.
- **Idempotency (D10):** Duplicate add commands return `DomainResult.NoOp()` which the existing dispatch logic maps to 202 -- NOT a rejection.

### Code Style Requirements

- File-scoped namespaces (`namespace X;`)
- `sealed` classes for validators
- Allman brace style, 4-space indentation
- CRLF line endings, UTF-8 encoding
- One public type per file, file name = type name
- `TreatWarningsAsErrors = true` -- zero warnings allowed
- No positional record parameters
- `[GeneratedRegex]` for any compiled regex patterns (unlikely needed here)

### Library & Framework Versions

| Package | Version |
|---|---|
| .NET SDK | 10.0.103 (net10.0) |
| FluentValidation | 12.1.1 |
| xUnit | 2.9.3 |
| Shouldly | 4.3.0 |

No new packages needed for this story.

### Anti-Patterns to Avoid

- **DO NOT** create new command or event types -- all contracts already exist
- **DO NOT** modify Contracts, Server, Testing, or Projections projects
- **DO NOT** modify `PartiesServiceCollectionExtensions.cs` -- assembly scanning already covers new validators
- **DO NOT** add domain logic to the controller -- use `DispatchCommandAsync` only
- **DO NOT** duplicate aggregate Handle validation in FluentValidation (e.g., don't check if party exists in validator)
- **DO NOT** log `[PersonalData]` fields (Value on contact channel/identifier commands)
- **DO NOT** return 200 OK for commands -- use 202 Accepted
- **DO NOT** create a separate "mark preferred" endpoint -- preferred marking is via AddContactChannel/UpdateContactChannel
- **DO NOT** use positional record parameters on validators
- **DO NOT** explicitly register validators -- assembly scanning handles it
- **DO NOT** add `using` statements for `Hexalith.Parties.Contracts.Events` in the controller -- controller only deals with commands, not events

### Previous Story Intelligence (Story 2.3)

- 98 tests pass (21 contracts + 68 server + 2 integration + 7 CommandApi), zero regressions
- All 11 Handle methods are complete in `PartyAggregate.cs`: CreateParty, UpdatePersonDetails, UpdateOrganizationDetails, SetIsNaturalPerson, DeactivateParty, ReactivateParty, AddContactChannel, UpdateContactChannel, RemoveContactChannel, AddIdentifier, RemoveIdentifier
- Build succeeds with `TreatWarningsAsErrors = true`
- Pre-existing ASPIRE004 build warning is unrelated -- ignore
- `PartyAggregateContactChannelTests.cs` has 15 tests, `PartyAggregateIdentifierTests.cs` has 8 tests -- aggregate behavior is fully verified

**Key learnings from Story 1.6 (REST API foundation):**
- Validators must be explicitly invoked via `ValidateCommandAsync` (not just registered) -- this is already implemented in the controller
- Route/body PartyId consistency: set `command.PartyId` from URL `{id}` using `command = command with { PartyId = id }`
- The `DispatchCommandAsync` method handles all success/rejection/NoOp mapping -- new endpoints just call it
- `PartiesControllerProblemDetailsTests.cs` exists for API integration tests -- may need extension for new endpoints
- Missing tenant claim returns 401 (not 403) -- already handled by existing controller logic
- Domain rejection returns 422 with corrective action guidance -- already handled by existing error handlers
- Cross-tenant protection is built into the actor ID derivation -- no additional code needed

### Git Intelligence

**Recent commits:**
```
7542827 Merge pull request #11 -- Story 2.3: Contact Channel & Identifier Unit Tests
468d6c8 Implement Story 2.3: Contact Channel & Identifier Unit Tests
4992d09 Merge pull request #10 -- Story 2.2
75b7c58 Implement Story 2.2: Party Aggregate Identifier Management
```

**Branch naming pattern:** `implement-story-2-4-rest-api-contact-channel-and-identifier-endpoints`
**Commit message pattern:** `Implement Story 2.4: REST API Contact Channel & Identifier Endpoints`

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-2.4 -- Acceptance criteria and requirements]
- [Source: _bmad-output/planning-artifacts/architecture.md -- REST API patterns (D8-D12), validation conventions, JSON conventions, implementation patterns]
- [Source: _bmad-output/implementation-artifacts/1-6-rest-api-error-handling-and-party-retrieval.md -- REST API foundation patterns, controller dispatch, error handling, validators]
- [Source: src/Hexalith.Parties.CommandApi/Controllers/PartiesController.cs -- Existing 7 endpoints (1 GET + 6 POST) and DispatchCommandAsync method]
- [Source: src/Hexalith.Parties.CommandApi/Validation/ -- Existing 6 validators as pattern reference]
- [Source: src/Hexalith.Parties.CommandApi/Extensions/PartiesServiceCollectionExtensions.cs -- Assembly scanning registration]
- [Source: src/Hexalith.Parties.Contracts/Commands/ -- AddContactChannel, UpdateContactChannel, RemoveContactChannel, AddIdentifier, RemoveIdentifier]
- [Source: src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs -- Handle methods for all 5 commands (already implemented)]
- [Source: _bmad-output/implementation-artifacts/2-3-contact-channel-and-identifier-unit-tests.md -- Previous story patterns and learnings]

## Change Log

- 2026-03-05: Implemented Story 2.4 -- Added 5 POST endpoints (AddContactChannel, UpdateContactChannel, RemoveContactChannel, AddIdentifier, RemoveIdentifier) to PartiesController and created 5 FluentValidation validators. Build: 0 errors, 0 warnings. Tests: 98/98 pass, zero regressions.
- 2026-03-05: Senior Developer Review (AI) completed. Outcome: Changes Requested. Story status set to `in-progress`; 1 HIGH and 2 MEDIUM follow-ups added.
- 2026-03-05: Addressed all AI review follow-ups: added endpoint-level tests for all 5 new routes (202/400/422 coverage), renamed endpoint methods to `{Action}Async`, and re-ran full suite: 113/113 tests passed. Story returned to `review`.
- 2026-03-05: Second code review (AI). Fixed 1 MEDIUM: reverted `Async` suffix on 5 new methods to match dominant controller convention (6/7 pre-existing POST methods have no suffix). 3 LOW informational findings noted (narrow per-endpoint validation test coverage, AC #10 verified by inspection only, pre-existing serializer options mismatch). Build: 0 errors, 0 warnings. Tests: 113/113 pass. Story status set to `done`.

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

No issues encountered. Straightforward implementation following established patterns from Story 1.6.

### Completion Notes List

- Added 5 new POST endpoints to PartiesController following the exact existing pattern (ArgumentNullException.ThrowIfNull, EnsureRouteMatchesBodyPartyId, DispatchCommandAsync with `command with { PartyId = id }`)
- Created 5 FluentValidation validators: AddContactChannelValidator, UpdateContactChannelValidator, RemoveContactChannelValidator, AddIdentifierValidator, RemoveIdentifierValidator
- All validators follow the sealed class + AbstractValidator pattern with PartyId GUID validation
- Validators auto-discovered via existing assembly scanning -- no DI registration changes needed
- No contracts, server, testing, or projection files modified (as specified)
- Build: 0 errors, 0 warnings with TreatWarningsAsErrors=true
- Tests: 98/98 pass (21 contracts + 68 server + 2 integration + 7 CommandApi), zero regressions

### File List

- `src/Hexalith.Parties.CommandApi/Controllers/PartiesController.cs` -- MODIFIED: added 5 POST endpoints
- `src/Hexalith.Parties.CommandApi/Validation/AddContactChannelValidator.cs` -- NEW
- `src/Hexalith.Parties.CommandApi/Validation/UpdateContactChannelValidator.cs` -- NEW
- `src/Hexalith.Parties.CommandApi/Validation/RemoveContactChannelValidator.cs` -- NEW
- `src/Hexalith.Parties.CommandApi/Validation/AddIdentifierValidator.cs` -- NEW
- `src/Hexalith.Parties.CommandApi/Validation/RemoveIdentifierValidator.cs` -- NEW
- `tests/Hexalith.Parties.CommandApi.Tests/Controllers/PartiesControllerProblemDetailsTests.cs` -- MODIFIED: added endpoint-level tests for all 5 Story 2.4 routes (Accepted/BadRequest/UnprocessableEntity)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` -- MODIFIED: story status sync updates

## Senior Developer Review (AI)

### Reviewer

Jérôme (AI-assisted adversarial review)

### Date

2026-03-05

### Outcome

Approved

### Summary

- Second review pass after all first-review follow-ups were addressed.
- Git vs Story File List: 0 discrepancies.
- All 10 ACs validated as implemented (AC #10 verified by code inspection).
- All 16 task subtasks verified as done.
- 1 MEDIUM issue found and fixed during review: removed `Async` suffix from 5 new methods to match dominant controller convention.
- 3 LOW informational findings noted (not blocking).
- Full test run: 113/113 pass (21 contracts + 68 server + 2 integration + 22 CommandApi), 0 errors, 0 warnings.

### Findings (Second Review)

1. **[MEDIUM] Controller method naming inconsistency (FIXED)**
    - New endpoints used `{Action}Async` suffix while 6 pre-existing POST methods do not. Removed `Async` suffix from all 5 new methods to match dominant convention.
    - `PartiesController.cs:214,226,238,250,262`

2. **[LOW] Single-scenario validation test coverage per endpoint**
    - Each endpoint has one invalid payload in `CommandEndpointValidationCases`. No tests for invalid enum values or multi-field failures. Matches pre-existing test pattern from Story 1.6 -- adequate but not comprehensive.

3. **[LOW] AC #10 (PersonalData logging exclusion) verified by inspection only**
    - Controller `DispatchCommandAsync` logs only `CommandType`, `AggregateId`, `CorrelationId`, `Tenant` -- never command payloads. No automated regression test for this.

4. **[LOW] `DispatchCommandAsync` uses default `JsonSerializerOptions` (PascalCase, numeric enums) vs MVC's camelCase + `JsonStringEnumConverter`**
    - Pre-existing from Story 1.6, not introduced by this story. Noted for awareness.

### AC Validation Snapshot

- AC #1: **Implemented** (all 5 POST routes present).
- AC #2: **Implemented** (tested via `CommandEndpointSuccessCases` theory -- 202 Accepted with correlationId).
- AC #3: **Implemented** (tested via `CommandEndpointValidationCases` theory -- 400 Bad Request with ProblemDetails).
- AC #4: **Implemented** (tested via `CommandEndpointDomainRejectionCases` theory -- 422 Unprocessable Entity).
- AC #5: **Implemented** (tested via `CommandEndpointSuccessCases` theory -- 202 Accepted with correlationId).
- AC #6: **Implemented** (tested via `CommandEndpointValidationCases` theory -- 400 Bad Request with ProblemDetails).
- AC #7: **Implemented** (all endpoints follow Story 1.6 dispatch pattern, ProblemDetails, FluentValidation, auth, tenant extraction).
- AC #8: **Implemented** (113/113 tests pass -- 98 existing + 15 new CommandApi tests).
- AC #9: **Implemented** (assembly scanning via `AddValidatorsFromAssemblyContaining<CreatePartyValidator>()` -- no explicit registration).
- AC #10: **Implemented** (verified by code inspection -- controller never logs command payloads or `[PersonalData]` fields).
