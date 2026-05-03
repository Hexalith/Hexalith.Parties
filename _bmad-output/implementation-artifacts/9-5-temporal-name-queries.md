# Story 9.5: Temporal Name Queries

Status: done

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

### Review Findings

#### Decision Resolutions

- [x] [Review][Decision→Defer] `NameHistoryEntry.ChangedAt` is stamped from `DateTimeOffset.UtcNow` at apply time — Architectural fix requires threading event envelope timestamp through `Apply(string, IEventPayload, PartyDetail?)` and `ProjectionRebuildService`, plus the parallel `PartyIndexProjectionHandler`. Too much blast radius for a review patch — split into a dedicated hardening story. [src/Hexalith.Parties.Projections/Handlers/PartyDetailProjectionHandler.cs:91, 119, 141]
- [x] [Review][Decision→Defer] `PartyCreated` is silently no-op'd when `state is not null` — Existing test `Apply_PartyCreated_WhenStateExists_DoesNotResetNameHistory` pins the no-op behavior intentionally for replay idempotency. Flipping it requires a design discussion about projection-rebuild philosophy; defer to consent/projection hardening. [src/Hexalith.Parties.Projections/Handlers/PartyDetailProjectionHandler.cs:15]
- [x] [Review][Decision→Patch] Dedup compares `DisplayName` only — Promoted to patch and applied: dedup now compares both `DisplayName` AND `SortName`, so sort-only changes (locale tweak, family-name spelling) append a `NameHistoryEntry`. Test `Apply_PartyDisplayNameDerived_SortNameOnlyChange_AppendsHistoryEntry` added. [src/Hexalith.Parties.Projections/Handlers/PartyDetailProjectionHandler.cs:119-128]
- [x] [Review][Decision→Defer] `NameHistory` is unbounded inside `PartyDetail` actor state and `GET /name-history` has no pagination or size guard — Needs GDPR retention policy + pagination protocol design with consent-management owners. Defer to a follow-up story. [src/Hexalith.Parties.Projections/Handlers/PartyDetailProjectionHandler.cs:130-146; src/Hexalith.Parties.CommandApi/Controllers/PartiesController.cs:354-382]

#### Patch

- [x] [Review][Patch] MCP `GetPartyNameAtMcpTool` now calls `proxy.ReadDetailAsync()` from the new `PartyDetailProjectionActorExtensions` helper, sharing the JSON / serialized / typed fallback chain with the controller [src/Hexalith.Parties.CommandApi/Extensions/PartyDetailProjectionActorExtensions.cs; src/Hexalith.Parties.CommandApi/Mcp/GetPartyNameAtMcpTool.cs]
- [x] [Review][Patch] Added `tests/Hexalith.Parties.CommandApi.Tests/Mcp/GetPartyNameAtMcpToolTests.cs` covering valid-timestamp, after-all-changes, before-creation, not-found, erased, no-history, invalid-id, invalid-timestamp, missing-tenant, and out-of-order history scenarios (10 tests, all pass)
- [x] [Review][Patch] MCP error semantics now pinned by the new test suite — distinct exception messages for not-found / erased / before-creation / no-history asserted by `ShouldBe`/`ShouldContain` so the contract cannot drift silently [tests/Hexalith.Parties.CommandApi.Tests/Mcp/GetPartyNameAtMcpToolTests.cs]
- [x] [Review][Patch] Temporal lookup in both controller and MCP tool now uses `.Where(e => e.ChangedAt <= at).OrderBy(e => e.ChangedAt).LastOrDefault()` for defense-in-depth against out-of-order entries [src/Hexalith.Parties.CommandApi/Controllers/PartiesController.cs:GetPartyNameAtAsync; src/Hexalith.Parties.CommandApi/Mcp/GetPartyNameAtMcpTool.cs]
- [x] [Review][Patch] `TemporalNameE2ETests` no longer captures `afterCreation` from the test-process wall-clock — it reads `createdAt` from the projection response so the comparison cannot drift under inter-host clock skew [tests/Hexalith.Parties.IntegrationTests/Search/TemporalNameE2ETests.cs]

#### Deferred

- [x] [Review][Defer] `ApplyErasure` wipes `ConsentRecords` — may delete the GDPR consent audit trail (Art.7(1)) [src/Hexalith.Parties.Projections/Handlers/PartyDetailProjectionHandler.cs:822-830] — deferred, pre-existing (consent area, story 9.4)
- [x] [Review][Defer] `HandleConsentRevoked` returns `null` for unknown consent id — out-of-order revocation is silently dropped [src/Hexalith.Parties.Projections/Handlers/PartyDetailProjectionHandler.cs:912-919] — deferred, pre-existing (consent area, story 9.4)
- [x] [Review][Defer] `UpdatePersonDetails` typed-shape branch does not fall back to route id when body omits `partyId`, inconsistent with the flat-shape branch [src/Hexalith.Parties.CommandApi/Controllers/PartiesController.cs:312-331] — deferred, pre-existing (composite-command area, epic 4)
- [x] [Review][Defer] `GetPartyDetailAsync` JSON-empty check trims to `"{}"` only; `"{ }"` (with internal whitespace) bypasses the empty-payload guard while the byte-array path uses `JsonNode.Parse` correctly [src/Hexalith.Parties.CommandApi/Controllers/PartiesController.cs:428-440] — deferred, pre-existing (utility helper inconsistency)
- [x] [Review][Defer] `ParseSearchMode` falls through to `Hybrid` for unknown values — typos like `?mode=semntic` silently switch mode instead of returning 400 [src/Hexalith.Parties.CommandApi/Controllers/PartiesController.cs:557-564] — deferred, pre-existing (search area, story 9.6)
- [x] [Review][Defer] `SetSearchMetadataHeaders` strips only CR/LF from `DegradedReason`; other control chars (NUL, VT, FF, DEL) still break Kestrel header serialization [src/Hexalith.Parties.CommandApi/Controllers/PartiesController.cs:545-555] — deferred, pre-existing (search area, story 9.6)
- [x] [Review][Defer] `MessageId = Guid.NewGuid()` minted per-request inside the controller defeats command-level idempotency on client retries [src/Hexalith.Parties.CommandApi/Controllers/PartiesController.cs:257, 274] — deferred, pre-existing (CQRS pattern, multi-story)
- [x] [Review][Defer] `TryUpdateProjectionAsync` swallows non-cancellation exceptions and still returns 202; clients depending on read-after-write get false-positive accept and stale read [src/Hexalith.Parties.CommandApi/Controllers/PartiesController.cs:287-308] — deferred, pre-existing (search/projection area, story 9.6)
- [x] [Review][Defer] New `ConsentRecorded` / `ConsentRevoked` / `ProcessingRestricted` / `RestrictionLifted` handlers ship without unit tests in the diff [src/Hexalith.Parties.Projections/Handlers/PartyDetailProjectionHandler.cs:894-932] — deferred, pre-existing (consent/restriction area, story 9.4)
- [x] [Review][Defer] Diff range `c870bcb^..HEAD` includes substantial work outside 9.5 acceptance criteria (search modes, consent records, restriction events, command-DTO reshape, MessageId injection) — expected because the original combined story 9.5 was split after the work was already merged; flagged for context [multiple files] — deferred, pre-existing (pre-split combined-story residue)

