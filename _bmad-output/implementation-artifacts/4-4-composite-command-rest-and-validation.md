# Story 4.4: Composite Command REST & Validation

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer,
I want REST endpoints for composite commands with structural validation,
so that API consumers can create and update full parties in single HTTP requests.

## Acceptance Criteria

1. **Given** the `PartiesController`, **when** composite endpoints are added, **then** a POST endpoint exists for `CreatePartyComposite`, **and** a POST endpoint exists for `UpdatePartyComposite`.

2. **Given** a valid `CreatePartyComposite` request, **when** sent via POST, **then** the response is `202 Accepted` with `correlationId`, **and** the response body includes the composite sub-operation outcome summary (`applied`, `skipped`, `rejected`).

3. **Given** a valid `UpdatePartyComposite` request, **when** sent via POST, **then** the response is `202 Accepted`, **and** the response body includes the complete updated party state (`PartyDetail`) as required by FR69.

4. **Given** the `CreatePartyCompositeValidator` (`FluentValidation`), **when** reviewed, **then** it validates:
    - party ID is required and is a valid GUID;
    - party type is required;
    - total sub-operation count does not exceed the configurable maximum (`D17`);
    - contact-channel and identifier IDs are valid GUIDs when present;
    - required payload fields for the current contract shape are present;
    - it does **not** duplicate aggregate-domain rules such as duplicate detection or conflict detection.

5. **Given** the `UpdatePartyCompositeValidator` (`FluentValidation`), **when** reviewed, **then** it validates:
    - party ID is required and is a valid GUID;
    - total sub-operation count does not exceed the configurable maximum (`D17`);
    - contact-channel and identifier IDs in add, update, and remove collections are valid GUIDs;
    - it does **not** check conflicting operations or entity existence, because those remain aggregate-domain validation.

6. **Given** a composite command that fails `FluentValidation`, **when** sent via POST, **then** the response is `400 Bad Request` as `ProblemDetails`.

7. **Given** a composite command rejected by aggregate-domain logic (for example conflicting operations or unknown IDs), **when** the aggregate returns a rejection, **then** the response is `422 Unprocessable Entity` as `ProblemDetails` with specific error details and corrective action.

8. **Given** the existing `PartiesController` command-dispatch helper only returns `CommandProcessingResult` with `Accepted`, `ErrorMessage`, `CorrelationId`, and `EventCount`, **when** composite endpoints are implemented, **then** the implementation introduces a safe path to surface composite response payloads without relying on eventually consistent projection reads in the same request.

## Tasks / Subtasks

- [ ] Task 1: Add composite endpoint response plumbing for payload-bearing `202 Accepted` responses (AC: #2, #3, #8)
    - [ ] 1.1: Decide and implement the response path for composite commands so the API can return more than `correlationId`; the current `DispatchCommandAsync` helper and `CommandProcessingResult` are insufficient.
    - [ ] 1.2: Keep existing simple-command endpoints behavior unchanged while introducing a composite-specific path.
    - [ ] 1.3: Return a create-composite success payload containing `correlationId` plus composite outcome details (`applied`, `skipped`, `rejected`, and optionally `eventCount`).
    - [ ] 1.4: Return an update-composite success payload containing `correlationId` plus the updated `PartyDetail`.
    - [ ] 1.5: Avoid building the update response via projection read-after-write in the same request; use write-side data or a deterministic in-process mapping path.

- [ ] Task 2: Add composite REST endpoints to `PartiesController` (AC: #1, #2, #3)
    - [ ] 2.1: Add an explicit create-composite route that does not conflict with the existing root `POST /api/v1/parties` create endpoint.
    - [ ] 2.2: Add an explicit update-composite route under the party resource, following the controller's existing verb-based route style.
    - [ ] 2.3: Enforce route/body `PartyId` consistency for the update endpoint using the same validation approach as existing command endpoints.
    - [ ] 2.4: Reuse existing unauthorized / forbidden / domain-rejection `ProblemDetails` conventions.

- [ ] Task 3: Add `FluentValidation` validators for composite commands (AC: #4, #5, #6)
    - [ ] 3.1: Create `src/Hexalith.Parties.CommandApi/Validation/CreatePartyCompositeValidator.cs`.
    - [ ] 3.2: Create `src/Hexalith.Parties.CommandApi/Validation/UpdatePartyCompositeValidator.cs`.
    - [ ] 3.3: Reuse existing validator patterns and child-command rules where practical; keep registration via assembly scanning only.
    - [ ] 3.4: Introduce a single configurable max-sub-operations option for API validation that aligns with aggregate `MaxSubOperations` behavior.

- [ ] Task 4: Add focused controller and validation tests (AC: #2, #3, #6, #7, #8)
    - [ ] 4.1: Extend `PartiesControllerProblemDetailsTests` with valid composite create/update request cases.
    - [ ] 4.2: Add invalid composite payload cases verifying `400 Bad Request` and no router dispatch.
    - [ ] 4.3: Add domain rejection cases verifying `422 Unprocessable Entity` for composite endpoints.
    - [ ] 4.4: Add assertions for the accepted response body shape: create returns composite summary; update returns full `PartyDetail`.
    - [ ] 4.5: Update or extend test doubles for the composite payload-return path introduced in Task 1.

- [ ] Task 5: Build and regression verification (AC: #1-#8)
    - [ ] 5.1: `dotnet build Hexalith.Parties.slnx`
    - [ ] 5.2: `dotnet test Hexalith.Parties.slnx`

## Dev Notes

### What this story does

This story exposes the already-implemented aggregate composite commands through the REST API and adds entry-point validation. The goal is **not** to change `PartyAggregate` composite logic; it is to wire the HTTP surface cleanly, validate payload structure, and return useful success payloads for composite operations.

### What already exists (do not recreate)

The following pieces are already implemented and should be reused:

- `CreatePartyComposite` in `src/Hexalith.Parties.Contracts/Commands/CreatePartyComposite.cs`
- `UpdatePartyComposite` in `src/Hexalith.Parties.Contracts/Commands/UpdatePartyComposite.cs`
- `CompositeCommandResult` in `src/Hexalith.Parties.Contracts/Results/CompositeCommandResult.cs`
- `PartyAggregate.Handle(CreatePartyComposite, PartyState?)` in `src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs`
- `PartyAggregate.Handle(UpdatePartyComposite, PartyState?)` in `src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs`
- Existing validation registration via assembly scanning in `src/Hexalith.Parties.CommandApi/Extensions/PartiesServiceCollectionExtensions.cs`
- Existing controller patterns in `src/Hexalith.Parties.CommandApi/Controllers/PartiesController.cs`
- Existing API test harness in `tests/Hexalith.Parties.CommandApi.Tests/Controllers/PartiesControllerProblemDetailsTests.cs`

### Critical implementation seam: current command dispatch cannot return composite payloads

The current helper path in `PartiesController` is optimized for fire-and-forget command submission:

- `DispatchCommandAsync(...)` serializes the command into `SubmitCommand`
- `ICommandRouter.RouteCommandAsync(...)` returns `CommandProcessingResult`
- `CommandProcessingResult` only contains `Accepted`, `ErrorMessage`, `CorrelationId`, and `EventCount`

That means the current path cannot satisfy AC #2 or AC #3 by itself.

**Important:** do **not** fake FR69 by reading from the projection actor immediately after command dispatch. The read side is eventually consistent (`NFR6`), so a same-request projection read can return stale data and produce flaky behavior.

Instead, implement one of these safe approaches:

- extend the write-side result pipeline so composite command processing can surface a serialized success payload; or
- introduce a composite-specific application service path that invokes the same aggregate logic and deterministically maps the resulting state to API response DTOs while preserving the EventStore command flow guarantees.

Whatever path is chosen, keep the behavior explicit and covered by tests.

### Suggested endpoint shape

Follow the controller's existing explicit action-route style and avoid overloading the existing simple create route.

Use routes equivalent to:

- `POST /api/v1/parties/create-composite`
- `POST /api/v1/parties/{id}/update-composite`

If a different route name is chosen, keep it explicit, verb-based, and consistent with the rest of `PartiesController`.

### Response-shaping guidance

For create-composite success, return a body shaped like:

```json
{
    "correlationId": "...",
    "result": {
        "applied": ["..."],
        "skipped": ["..."],
        "rejected": []
    }
}
```

For update-composite success, return a body shaped like:

```json
{
    "correlationId": "...",
    "party": {
        "id": "...",
        "type": "Person",
        "isActive": true,
        "displayName": "...",
        "sortName": "...",
        "personDetails": {},
        "organizationDetails": null,
        "contactChannels": [],
        "identifiers": [],
        "createdAt": "...",
        "lastModifiedAt": "..."
    }
}
```

The success payload should remain PII-safe in operation summaries (`applied`, `skipped`, `rejected`) and reuse the existing `PartyDetail` contract for the updated-state response.

### Validation guidance

`FluentValidation` should only enforce structural correctness.

Validate:

- `PartyId` is present and a GUID;
- `Type` is present and not `Unknown` for create-composite;
- max sub-operation count is within the configured threshold;
- add/update/remove IDs are GUIDs where applicable;
- nested add/update items have the required current-contract fields.

Do **not** validate:

- duplicate detection;
- add/remove or update/remove conflicts;
- entity existence;
- party-type mismatch rules.

Those remain aggregate-domain rules already implemented in `PartyAggregate`.

### Current contract caveat

Although the broader product language refers to type-specific contact payloads, the current API contract still models contact-channel content as a flattened `Value` string on `AddContactChannel` and `UpdateContactChannel`.

For this story:

- validate the current contract as it exists today;
- do **not** invent new structured composite DTOs;
- do **not** change command contracts in `Hexalith.Parties.Contracts` unless the payload-return plumbing in Task 1 explicitly requires an additive response contract.

### Reuse opportunities

Do not reinvent mapping logic unnecessarily.

Useful existing patterns:

- `src/Hexalith.Parties.Projections/Handlers/PartyDetailProjectionHandler.cs` already contains the event-to-`PartyDetail` shape the system expects.
- `src/Hexalith.Parties.Contracts/State/PartyState.cs` contains the canonical write-side state transitions for contact channels, identifiers, preferred-channel updates, and display-name derivation.
- Existing validators such as `CreatePartyValidator`, `AddContactChannelValidator`, `UpdateContactChannelValidator`, and `AddIdentifierValidator` show the repository's validation style.

If you need to construct a `PartyDetail` response from write-side data, prefer reusing the established shape and transformation rules instead of creating a second interpretation of what a party looks like.

### Files likely to change

Primary files:

- `src/Hexalith.Parties.CommandApi/Controllers/PartiesController.cs`
- `src/Hexalith.Parties.CommandApi/Validation/CreatePartyCompositeValidator.cs` (new)
- `src/Hexalith.Parties.CommandApi/Validation/UpdatePartyCompositeValidator.cs` (new)
- `src/Hexalith.Parties.CommandApi/Extensions/PartiesServiceCollectionExtensions.cs` (only if the new plumbing requires additional service registration)
- `tests/Hexalith.Parties.CommandApi.Tests/Controllers/PartiesControllerProblemDetailsTests.cs`

Potential EventStore dependency surface if Task 1 is implemented at the shared pipeline level:

- `Hexalith.EventStore/src/Hexalith.EventStore.Server/Actors/CommandProcessingResult.cs`
- `Hexalith.EventStore/src/Hexalith.EventStore.Server/Commands/ICommandRouter.cs`
- the corresponding command-processing implementation behind the router/actor path

### Testing requirements

Add or extend tests for:

- valid composite create returns `202` with `correlationId` plus composite summary payload;
- valid composite update returns `202` with `correlationId` plus `PartyDetail` payload;
- invalid GUIDs / missing required fields return `400` and do not dispatch to the router;
- aggregate-domain rejections return `422` with the existing `ProblemDetails` structure;
- route/body `PartyId` mismatches return validation failures for update-composite;
- test doubles reflect the new payload-return path rather than hard-coding correlation-only success.

### Anti-patterns to avoid

- **Do not** modify `PartyAggregate` composite domain behavior in this story unless a bug is uncovered during controller integration.
- **Do not** duplicate domain conflict detection in `FluentValidation`.
- **Do not** query the projection actor immediately after write dispatch to build the update response.
- **Do not** bypass the existing authentication, tenant extraction, or `ProblemDetails` conventions.
- **Do not** add explicit validator registration; keep assembly scanning.
- **Do not** create alternate response models for `PartyDetail` if the existing contract already fits.

### Previous story intelligence

Story `4.2` completed the aggregate-side `UpdatePartyComposite` behavior, including:

- payload-size enforcement via `PartyAggregate.MaxSubOperations`;
- all-or-nothing validation for unknown IDs and conflicting operations;
- duplicate tracking surfaced through `CompositeCommandResult.Skipped`;
- deterministic ordering for deduplicated remove operations.

Story `4.3` already prepared the Tier 1 test matrix around those behaviors. This story should assume the composite domain handlers are correct and focus on getting them safely through the HTTP boundary.

### Git intelligence

Recent repository history shows Epic 4 work landing in this order:

- `39e713f` Merge pull request #17 -- Story 4.1: Create Party Composite Aggregate Handler
- `85b67cf` Implement Story 4.1: Create Party Composite Aggregate Handler
- `579aa3d` Merge pull request #16 -- Story 3.4: Projection Unit and Integration Tests
- `610959b` Implement Story 3.4: Projection Unit and Integration Tests
- `38684de` Merge pull request #15 -- Story 3.3: Search, Match Metadata & Query Endpoints

That pattern suggests keeping changes focused, additive, and accompanied by test coverage in the same slice.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-4.4`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#D8`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#D9`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#D10`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#D11`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#D12`]
- [Source: `_bmad-output/planning-artifacts/architecture.md#D17`]
- [Source: `src/Hexalith.Parties.CommandApi/Controllers/PartiesController.cs`]
- [Source: `src/Hexalith.Parties.CommandApi/Extensions/PartiesServiceCollectionExtensions.cs`]
- [Source: `src/Hexalith.Parties.CommandApi/Validation/CreatePartyValidator.cs`]
- [Source: `src/Hexalith.Parties.CommandApi/Validation/AddContactChannelValidator.cs`]
- [Source: `src/Hexalith.Parties.CommandApi/Validation/UpdateContactChannelValidator.cs`]
- [Source: `src/Hexalith.Parties.CommandApi/Validation/AddIdentifierValidator.cs`]
- [Source: `src/Hexalith.Parties.Contracts/Commands/CreatePartyComposite.cs`]
- [Source: `src/Hexalith.Parties.Contracts/Commands/UpdatePartyComposite.cs`]
- [Source: `src/Hexalith.Parties.Contracts/Results/CompositeCommandResult.cs`]
- [Source: `src/Hexalith.Parties.Contracts/Models/PartyDetail.cs`]
- [Source: `src/Hexalith.Parties.Contracts/State/PartyState.cs`]
- [Source: `src/Hexalith.Parties.Projections/Handlers/PartyDetailProjectionHandler.cs`]
- [Source: `tests/Hexalith.Parties.CommandApi.Tests/Controllers/PartiesControllerProblemDetailsTests.cs`]
- [Source: `Hexalith.EventStore/src/Hexalith.EventStore.Server/Actors/CommandProcessingResult.cs`]
- [Source: `Hexalith.EventStore/src/Hexalith.EventStore.Server/Commands/ICommandRouter.cs`]

## Dev Agent Record

### Agent Model Used

GPT-5.4

### Debug Log References

### Completion Notes List

### File List
