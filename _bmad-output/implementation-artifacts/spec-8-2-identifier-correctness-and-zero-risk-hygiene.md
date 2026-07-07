---
title: '8.2 Identifier correctness and zero-risk hygiene'
type: 'bugfix'
created: '2026-07-07T08:39:58+02:00'
status: 'done'
review_loop_iteration: 0
followup_review_recommended: true
baseline_revision: 'd6d3bbb26d04e2aeebe6326726bc084163f40bd3'
final_revision: '6725d50e58746766801236596f26c5e6d986bad3'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-8-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-8-1-baseline-and-release-blocker-stabilization.md'
warnings: []
---

<intent-contract>

## Intent

**Problem:** Parties still treats several semantic identifiers as GUIDs, so valid ULID-compatible aggregate IDs and readable child IDs can be rejected while new command and correlation identifiers are minted as GUIDs. This violates the Hexalith identifier rule and blocks safe Epic 8 refactoring.

**Approach:** Centralize Parties-owned identifier hygiene, remove GUID parsing from aggregate/validator identity checks, and mint new EventStore message/correlation/semantic IDs with `UniqueIdHelper.GenerateSortableUniqueStringId()` while preserving existing GUID-shaped IDs.

## Boundaries & Constraints

**Always:** Keep existing GUID-shaped IDs valid; preserve command/event JSON contract shape; keep validation failures support-safe; use Central Package Management and source/package reference patterns; keep tests focused and per-project.

**Block If:** A fix requires changing submodule source, changing EventStore or Commons public APIs, migrating persisted IDs, or weakening existing validation unrelated to identifier shape.

**Never:** Do not rewrite stored event/projection data, broaden this into Epic 8 structural migrations, ban legitimate `Guid.NewGuid()` usages for UI DOM IDs/temp paths/health probes, or alter tenant/user identity semantics.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| ULID party id | Create/update/GDPR command uses a 26-character ULID party id | Validator and aggregate accept it and preserve the exact string in aggregate/payload fields | No error expected |
| Legacy GUID party id | Existing GUID-shaped party id is replayed or resubmitted | Validator and aggregate still accept it for compatibility | No migration or rewrite |
| Readable child ids | Composite command uses `ch-email-1` / `id-vat-1` child ids | Composite validation matches standalone validation and accepts non-empty readable ids | Blank child ids still fail required validation |
| New command ids | Client/Admin/MCP creates commands without caller-supplied ids | `messageId`, `correlationId`, generated party/contact/identifier ids are ULID-shaped | Caller-supplied valid IDs are preserved |
| Unsafe semantic id | ID contains whitespace/path-like unsafe characters or is blank | Validation fails before EventStore submission | Error messages do not echo raw PII or unsafe input |

</intent-contract>

## Code Map

- `src/Hexalith.Parties.Contracts/ValueObjects/PartyIdentifier.cs` -- shared contract-level semantic-ID helper on the existing value-object type, avoiding root-namespace shadowing in contract DTO namespaces.
- `src/Hexalith.Parties/Validation/*.cs` -- command validators currently contain repeated `Guid.TryParse` checks for `PartyId` and some child IDs.
- `src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs` -- create handlers contain guard-cascade ID checks that reject non-GUID party IDs.
- `src/Hexalith.Parties.Client/HttpPartiesCommandClient.cs` -- typed command client currently mints GUID `messageId` / `correlationId`.
- `src/Hexalith.Parties.Client/AdminPortal/HttpAdminPortalGdprClient.cs` -- admin GDPR command client mints GUID `messageId` / `correlationId`.
- `src/Hexalith.Parties.Mcp/Tools/PartiesMcpTools.cs` -- MCP tool helper mints generated party/contact/identifier IDs.
- `src/Hexalith.Parties.AdminPortal/Components/CreateEditPartyPage.razor` -- Admin create/edit UI mints party/contact/identifier IDs.
- `src/Hexalith.Parties.Security/PartyKeyManagementService.cs` and `src/Hexalith.Parties.Security/TenantKeyRotationService.cs` -- security services mint fallback correlation IDs.
- `src/Hexalith.Parties.Client/Hexalith.Parties.Client.csproj`, `src/Hexalith.Parties.AdminPortal/Hexalith.Parties.AdminPortal.csproj`, `src/Hexalith.Parties.Mcp/Hexalith.Parties.Mcp.csproj`, `src/Hexalith.Parties.Security/Hexalith.Parties.Security.csproj`, and any other touched production project -- add `Hexalith.Commons.UniqueIds` references only where needed, with source/package conditions matching existing Commons references.
- `tests/Hexalith.Parties.Tests/Validation/IdentifierValidatorTests.cs`, `tests/Hexalith.Parties.Tests/Validation/ContactChannelValidatorTests.cs`, `tests/Hexalith.Parties.Server.Tests/Aggregates/*.cs`, `tests/Hexalith.Parties.Client.Tests/**/*.cs`, and `tests/Hexalith.Parties.Mcp.Tests/*.cs` -- focused behavior coverage.
- `tests/Hexalith.Parties.Tests/FitnessTests/IdentifierHygieneFitnessTests.cs` -- source guard for semantic identifier hygiene and tracked cache artifacts.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.Parties.Contracts/ValueObjects/PartyIdentifier.cs` -- add a small dependency-free semantic-ID helper for non-empty, bounded, support-safe IDs -- gives validators and server aggregates one shared rule without reversing dependencies.
- [x] `src/Hexalith.Parties/Validation/*.cs` -- replace `Guid.TryParse` rules and GUID-specific messages with `PartyIdentifier` while preserving required-field cascade behavior -- allows ULID/GUID/readable IDs consistently.
- [x] `src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs` -- replace create-handler GUID checks with the same semantic ID helper -- keeps aggregate guard cascade aligned with validation.
- [x] `src/Hexalith.Parties.Client/HttpPartiesCommandClient.cs`, `src/Hexalith.Parties.Client/AdminPortal/HttpAdminPortalGdprClient.cs`, `src/Hexalith.Parties.Mcp/Tools/PartiesMcpTools.cs`, `src/Hexalith.Parties.AdminPortal/Components/CreateEditPartyPage.razor`, and security fallback correlation services -- replace semantic ID minting with `UniqueIdHelper.GenerateSortableUniqueStringId()` and preserve caller-supplied IDs -- new IDs become ULID-shaped without changing contracts.
- [x] Touched `.csproj` files -- add `Hexalith.Commons.UniqueIds` package/project references using existing `HexalithCommonsFromSource` conditions and no inline versions -- keeps package/source modes valid.
- [x] Focused tests and fitness tests listed in Code Map -- update GUID-specific expectations, add ULID/GUID/readable/unsafe cases, and guard against `Guid.TryParse` in semantic validation plus tracked `.lscache` artifacts -- prevents regression.
- [x] `_bmad-output/implementation-artifacts/tests/test-summary.md` and `_bmad-output/implementation-artifacts/sprint-status.yaml` -- append Story 8.2 evidence/status and preserve Story 8.1 residual blocker wording -- keeps BMAD continuity current.

**Acceptance Criteria:**
- Given every Parties command validator that validates `PartyId`, when the party id is a valid ULID or legacy GUID string, then validation succeeds unless another business rule fails.
- Given composite contact channel and identifier child IDs, when the ID is a non-empty readable string such as `ch-email-1` or `id-vat-1`, then composite validation accepts it consistently with standalone validators.
- Given `CreateParty` or `CreatePartyComposite`, when the aggregate receives a ULID party id, then it emits the same success events as the legacy GUID path.
- Given the command client, admin GDPR client, MCP generated IDs, admin create/edit generated IDs, and security fallback correlations, when a new ID is minted, then it is accepted by `UniqueIdHelper.ExtractTimestamp` and is not GUID-shaped.
- Given semantic identifier validation source, when repository guard tests scan the targeted files, then `Guid.TryParse` is absent from validators and create aggregate ID guards.
- Given tracked repository files, when the cache-artifact guard runs, then no `*.csproj.lscache` or `*.lscache` files are tracked.

## Spec Change Log

- 2026-07-07: Implemented semantic identifier hygiene and sortable ID generation. The helper was placed on the existing `Hexalith.Parties.Contracts.ValueObjects.PartyIdentifier` type instead of a root contract type to avoid shadowing contract value-object references in nested namespaces.

## Review Triage Log

### 2026-07-07 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 14: (high 3, medium 8, low 3)
- defer: 0
- reject: 1: (high 0, medium 0, low 1)
- addressed_findings:
  - `[high]` `[patch]` Added the missing `PartyIdentifier.IsValid` public API snapshot entry and included `Contracts.Tests` in final evidence.
  - `[high]` `[patch]` Hardened `UpdatePartyComposite` aggregate validation so unsafe add/update/remove child IDs fail before conflict or not-found paths can echo raw IDs.
  - `[high]` `[patch]` Added client and admin GDPR gateway aggregate-ID validation so unsafe party IDs are rejected before EventStore submission.
  - `[medium]` `[patch]` Tightened `PartyIdentifier.IsValid` to reject punctuation-only IDs by requiring alphanumeric first and last characters.
  - `[medium]` `[patch]` Aligned MCP safe party-ID validation with the shared `PartyIdentifier` helper.
  - `[medium]` `[patch]` Aligned AdminPortal route-ID safety checks with the shared `PartyIdentifier` helper.
  - `[medium]` `[patch]` Added create/update composite child `PartyId` equality checks in validators and aggregate paths.
  - `[medium]` `[patch]` Added aggregate create/update null child-operation handling for composite contact channel and identifier operations.
  - `[medium]` `[patch]` Reworked domain-service validation tests so invalid command payload IDs are not coerced into EventStore envelope aggregate IDs.
  - `[medium]` `[patch]` Expanded identifier hygiene fitness tests to cover GUID parsers, GUID construction, targeted GUID generation, and cache artifacts.
  - `[medium]` `[patch]` Replaced blank security correlation fallbacks with sortable IDs in party key management and tenant key rotation.
  - `[low]` `[patch]` Added XML documentation for the new public `PartyIdentifier` helper members.
  - `[low]` `[patch]` Tightened generated-ID tests to verify timestamps fall within the command/test execution window.
  - `[low]` `[patch]` Updated final test-summary evidence and counts after review-driven changes.

### 2026-07-07 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 3: (high 0, medium 3, low 0)
- defer: 0
- reject: 1: (high 0, medium 0, low 1)
- addressed_findings:
  - `[medium]` `[patch]` Added semantic-ID no-send guards to party query and admin GDPR query paths so unsafe party IDs do not reach the EventStore gateway.
  - `[medium]` `[patch]` Normalized nested child `PartyId` values in routed `UpdatePartyComposite` client calls so route-authoritative updates do not regress into child-party mismatch rejections.
  - `[medium]` `[patch]` Preserved bracketed legacy GUID formats in `PartyIdentifier.IsValid` without reintroducing `Guid.TryParse`.

### 2026-07-07 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 3: (high 0, medium 3, low 0)
- defer: 1: (high 0, medium 1, low 0)
- reject: 1: (high 0, medium 0, low 1)
- addressed_findings:
  - `[medium]` `[patch]` Preserved .NET legacy `X`-format GUID strings in `PartyIdentifier.IsValid` without reintroducing `Guid.TryParse`.
  - `[medium]` `[patch]` Added typed command-client no-send guards for child contact-channel and identifier IDs, including composite add/update/remove lists and nested child `PartyId` mismatches.
  - `[medium]` `[patch]` Added MCP `update_party` validation for unsafe update and removal child IDs so agent calls fail with structured validation before client access.

### 2026-07-07 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 4: (high 0, medium 4, low 0)
- defer: 0
- reject: 1: (high 0, medium 0, low 1)
- addressed_findings:
  - `[medium]` `[patch]` Added standalone aggregate semantic-ID guards for contact-channel and identifier handlers so unsafe child IDs fail before not-found paths or persisted success events.
  - `[medium]` `[patch]` Mapped typed-client `ArgumentException` validation failures to bounded AdminPortal query, command, and GDPR outcomes.
  - `[medium]` `[patch]` Preserved legacy .NET `X`-format GUID values for MCP single child-ID fields by splitting only explicit CSV inputs.
  - `[medium]` `[patch]` Added typed command-client composite-list null guards so explicit null operation lists fail as validation errors without sending gateway requests.

## Design Notes

Use a bounded semantic-ID helper instead of raw `NotEmpty()` everywhere because the goal is not to accept arbitrary strings. Keep the allowed shape conservative enough for existing readable IDs already used in tests: letters, digits, `.`, `_`, and `-`, with no whitespace, slash, colon, control characters, or raw payload echoing in messages.

## Verification

**Commands:**
- `git ls-files '*.csproj.lscache' '*.lscache'` -- expected: no tracked cache artifacts.
- `rg -n 'Guid\.TryParse' src/Hexalith.Parties/Validation src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs` -- expected: no semantic validation matches.
- `dotnet test tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj -c Release -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` -- expected: passes or only a recorded Story 8.1 baseline blocker appears.
- `dotnet test tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj -c Release -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` -- expected: passes.
- `dotnet test tests/Hexalith.Parties.Mcp.Tests/Hexalith.Parties.Mcp.Tests.csproj -c Release -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity minimal` -- expected: passes.

**Observed 2026-07-07:**
- `git ls-files '*.csproj.lscache' '*.lscache'` -- pass; no output.
- `rg -n 'Guid\.TryParse|Guid\.Parse|new Guid\(' src/Hexalith.Parties/Validation src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs src/Hexalith.Parties.Contracts/ValueObjects/PartyIdentifier.cs` -- pass; no matches.
- `rg -n 'Guid\.NewGuid' src/Hexalith.Parties.Client/HttpPartiesCommandClient.cs src/Hexalith.Parties.Client/AdminPortal/HttpAdminPortalGdprClient.cs src/Hexalith.Parties.Mcp/Tools/PartiesMcpTools.cs src/Hexalith.Parties.Security/PartyKeyManagementService.cs src/Hexalith.Parties.Security/TenantKeyRotationService.cs` -- pass; no matches.
- `git diff --check` -- pass.
- `git diff --cached --check` -- pass.
- `Hexalith.Parties.Contracts.Tests` Release source-mode -- pass; 135 passed.
- `Hexalith.Parties.Client.Tests` Release source-mode -- pass; 132 passed.
- `Hexalith.Parties.Mcp.Tests` Release source-mode -- pass; 54 passed.
- `Hexalith.Parties.Server.Tests` Release source-mode -- pass; 232 passed.
- `Hexalith.Parties.AdminPortal.Tests` Release source-mode -- pass; 179 passed.
- `Hexalith.Parties.Security.Tests` Release source-mode -- pass; 169 passed.
- `Hexalith.Parties.Tests` Debug source-mode build -- pass.
- Direct xUnit v3 run for `IdentifierValidatorTests` after follow-up review fixes -- pass; 16 passed.
- Direct xUnit v3 run for `IdentifierValidatorTests`, `ContactChannelValidatorTests`, `IdentifierHygieneFitnessTests`, and `PartyDomainServiceInvokerValidationTests` -- pass; 44 passed.
- Follow-up review patch validation: `Hexalith.Parties.Contracts.Tests` Release source-mode -- pass; 135 passed.
- Follow-up review patch validation: `Hexalith.Parties.Client.Tests` Release source-mode -- pass; 135 passed.
- Follow-up review patch validation: `Hexalith.Parties.Mcp.Tests` Release source-mode -- pass; 56 passed.
- Follow-up review patch validation: `Hexalith.Parties.Tests` Debug source-mode build -- pass.
- Follow-up review patch validation: direct xUnit v3 run for `IdentifierValidatorTests` and `IdentifierHygieneFitnessTests` -- pass; 20 passed.
- Final review patch validation: `Hexalith.Parties.Server.Tests` Release source-mode -- pass; 237 passed.
- Final review patch validation: `Hexalith.Parties.Client.Tests` Release source-mode -- pass; 137 passed.
- Final review patch validation: `Hexalith.Parties.Mcp.Tests` Release source-mode -- pass; 57 passed.
- Final review patch validation: `Hexalith.Parties.AdminPortal.Tests` Release source-mode -- pass; 183 passed.
- Final review patch validation: `git diff --check`, cache-artifact guard, targeted GUID parser scan, and targeted GUID generation scan -- pass.
- `Hexalith.Parties.Tests` Release source-mode remains blocked by the Story 8.1 `Hexalith.Memories` Release guard: `Release builds must use NuGet package references for external Hexalith libraries. Remove -p:UseHexalithProjectReferences=true or build Debug for source-debugging.`
- `Hexalith.Parties.Tests` Debug source-mode compiled and ran; identifier-focused classes pass directly, while the full project still has the five pre-existing tenant-event failures recorded by Story 8.1.

## Auto Run Result

Status: done

Summary:
- Implemented shared support-safe semantic-ID validation for Parties IDs while preserving GUID-shaped, bracketed legacy GUID, and ULID-compatible IDs.
- Replaced Parties-owned command, correlation, admin-generated, MCP-generated, and security fallback IDs with `UniqueIdHelper.GenerateSortableUniqueStringId()`.
- Removed tracked language-server cache artifacts and added focused regression coverage.
- Hardened query clients, command clients, MCP child-ID validation, and route-authoritative composite updates during follow-up review passes.
- Added final review hardening for standalone aggregate child IDs, AdminPortal validation mapping, MCP legacy `X`-format single IDs, and null composite operation lists.

Files changed:
- `.gitignore` -- ignores `*.lscache` artifacts.
- `src/Hexalith.Parties.Contracts/ValueObjects/PartyIdentifier.cs` -- adds documented semantic-ID helper and bounded safe-shape rule.
- `src/Hexalith.Parties/Validation/*.cs` -- replaces GUID parsing with `PartyIdentifier` and adds composite child party-ID checks.
- `src/Hexalith.Parties.Server/Aggregates/PartyAggregate.cs` -- aligns create/update composite and standalone child-ID aggregate guards with semantic-ID rules.
- `src/Hexalith.Parties.Client/**/*.cs`, `src/Hexalith.Parties.Mcp/**/*.cs`, `src/Hexalith.Parties.AdminPortal/**/*.cs`, and `src/Hexalith.Parties.Security/**/*.cs` -- use sortable ID generation, shared route/query/aggregate/child-ID safety checks, MCP validation, route-authoritative composite normalization, and bounded validation-outcome mapping.
- `tests/**/*.cs` and contract snapshot files -- add/adjust focused validator, aggregate, client, MCP, admin portal, security, public API, and hygiene tests.
- `_bmad-output/implementation-artifacts/**/*.md`, `deferred-work.md`, and `sprint-status.yaml` -- records Story 8.2 evidence/status and appends the new consent-ID follow-up item.

Review findings breakdown:
- Patches applied: 24 total: high 3, medium 18, low 3.
- Deferred: 1, appended as a new `deferred-work.md` entry for the consent/channel identifier contract because existing consent IDs intentionally use `channel:purpose` and need a separate compatibility decision.
- Rejected: 4, because accepting readable non-GUID IDs such as `not-a-guid` is intentional under this story's semantic-ID contract, the temporary `in-review`/`done` status mismatch was required by the review workflow before finalization, and broad validator-inventory/status findings covered internal/out-of-scope or workflow-temporary states without evidence of a current public-path regression.

Follow-up review recommendation: true.

Verification performed:
- Static scans and diff whitespace checks passed.
- Release source-mode project tests passed for Contracts, Client, MCP, Server, AdminPortal, and Security; Client, MCP, Server, and AdminPortal passed again after the final review fixes.
- Debug source-mode root test project built, and focused identifier/hygiene xUnit v3 coverage passed directly after follow-up review fixes.

Residual risks:
- Full root `Hexalith.Parties.Tests` Release source-mode remains blocked by the Story 8.1 `Hexalith.Memories` Release guard.
- Full root `Hexalith.Parties.Tests` Debug source-mode still has the five pre-existing Story 8.1 tenant-event failures.
