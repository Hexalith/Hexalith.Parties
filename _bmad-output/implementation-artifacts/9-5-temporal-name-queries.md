# Story 9.5: Temporal Name Queries

Status: review

<!-- Split from the former Story 9.5 "Semantic Search & Temporal Name Queries". Advanced search moved to Story 9.6. -->

## Story

As a consumer,
I want to query a party's historical name at a specific point in time,
so that legal, audit, and DPO workflows can answer "what was this party called then?" without replaying the event stream on the request path.

## Acceptance Criteria

1. **Historical Name at a Point in Time (FR72)**
   - Given a party whose name has changed over time through `UpdatePersonDetails` or `UpdateOrganizationDetails`
   - When a temporal name query is made with a specific timestamp
   - Then the party's `DisplayName` and `SortName` as they were at that timestamp are returned
   - And the query uses pre-computed name history tracked in the party detail projection
   - And the request does not replay the event stream at query time

2. **Timeline Availability**
   - Given a party with one or more name changes
   - When the full name history endpoint is called
   - Then the response returns name history entries in chronological order
   - And each entry includes display name, sort name, change timestamp, and triggering event/source where available

3. **Error and Erasure Behavior**
   - Given a timestamp before the party existed
   - When the temporal name query is made
   - Then the API returns `404 Not Found`
   - Given a party has been erased
   - When the temporal name query is made after erasure
   - Then the API returns `410 Gone`
   - And erased name history is not returned

4. **REST and MCP Access**
   - Given an authorized REST caller
   - When `GET /api/v1/parties/{id}/name?at={timestamp}` is called
   - Then a `TemporalNameResult` is returned
   - Given an authorized REST caller
   - When `GET /api/v1/parties/{id}/name-history` is called
   - Then the full `NameHistoryEntry[]` is returned
   - Given an MCP caller
   - When `get_party_name_at` is called
   - Then it returns the same temporal name result and error semantics as REST

5. **Projection Rebuild Compatibility**
   - Given party events are replayed through projection rebuild
   - When `PartyCreated` and `PartyDisplayNameDerived` events are processed
   - Then `NameHistory` is rebuilt deterministically
   - And duplicate entries are not appended when the display name did not change

## Tasks / Subtasks

- [x] Task 1: Define temporal name query contracts
  - [x] 1.1 Create `NameHistoryEntry` in `Contracts/ValueObjects/`
  - [x] 1.2 Add `NameHistory` to `PartyDetail` with safe default `[]`
  - [x] 1.3 Create `TemporalNameResult` in `Contracts/Models/`

- [x] Task 2: Implement temporal name projection
  - [x] 2.1 Initialize `NameHistory` from `PartyCreated`
  - [x] 2.2 Append `NameHistoryEntry` from `PartyDisplayNameDerived`
  - [x] 2.3 Deduplicate unchanged display-name derivations
  - [x] 2.4 Clear `NameHistory` during erasure
  - [x] 2.5 Keep `NameHistory` out of aggregate state

- [x] Task 3: Add REST and MCP endpoints
  - [x] 3.1 Add `GET /api/v1/parties/{id}/name?at={timestamp}`
  - [x] 3.2 Add `GET /api/v1/parties/{id}/name-history`
  - [x] 3.3 Add MCP `get_party_name_at`
  - [x] 3.4 Preserve authentication and tenant behavior from existing party read endpoints

- [x] Task 4: Add tests
  - [x] 4.1 Projection handler tests for creation, name change, deduplication, chronological order, and erasure
  - [x] 4.2 Controller tests for valid timestamp, before creation, full history, erased party, and auth
  - [x] 4.3 MCP tests for temporal name behavior
  - [x] 4.4 E2E coverage for create, update name, and query original timestamp

## Dev Notes

Name history is a Parties read-model concern, not a search concern. It stays in the party detail projection because legal/audit lookups require authoritative party state semantics and GDPR erasure handling.

Advanced party discovery has moved to Story 9.6 and must use `Hexalith.Memories` for lexical, semantic, hybrid, and graph-assisted retrieval. Do not reintroduce local "semantic" search into this story.

### Key Files

- `src/Hexalith.Parties.Contracts/ValueObjects/NameHistoryEntry.cs`
- `src/Hexalith.Parties.Contracts/Models/TemporalNameResult.cs`
- `src/Hexalith.Parties.Contracts/Models/PartyDetail.cs`
- `src/Hexalith.Parties.Projections/Handlers/PartyDetailProjectionHandler.cs`
- `src/Hexalith.Parties.CommandApi/Controllers/PartiesController.cs`
- `src/Hexalith.Parties.CommandApi/Mcp/GetPartyNameAtMcpTool.cs`
- `tests/Hexalith.Parties.Projections.Tests/Handlers/PartyDetailProjectionHandlerNameHistoryTests.cs`
- `tests/Hexalith.Parties.CommandApi.Tests/Controllers/TemporalNameEndpointTests.cs`
- `tests/Hexalith.Parties.IntegrationTests/Search/TemporalNameE2ETests.cs`

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Completion Notes List

- Split from the original mixed Story 9.5.
- Temporal name functionality remains in review.
- Advanced search tasks moved to Story 9.6.

### File List

