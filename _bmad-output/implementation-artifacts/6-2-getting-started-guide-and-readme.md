# Story 6.2: Getting-Started Guide & README

Status: done

## Story

As a developer evaluating Hexalith.Parties,
I want clear documentation that enables self-service onboarding,
So that I can deploy and send my first command without needing help from the core team.

## Acceptance Criteria

1. **README first-paragraph clarity**: A developer reads the first paragraph and can explain what Hexalith.Parties does in one sentence (one-sentence clarity test). Value proposition: "integrate, don't rebuild".

2. **README content completeness**: README includes:
   - Clear positioning (party management microservice, not auth, not CRM)
   - Key features summary (event-sourced, MCP, NuGet, multi-tenant)
   - GDPR disclaimer prominently placed (FR62)
   - Link to getting-started guide
   - Link to architecture overview

3. **Getting-started guide narrative arc** (FR32) at `/docs/getting-started.md` follows this sequence:
   - Step 1: Prerequisites (Docker, .NET 10 SDK) -- explicit list, nothing assumed
   - Step 2: Deploy: clone, `dotnet aspire run`, verify Aspire dashboard -- target < 15 minutes (NFR30)
   - Step 3: First command: POST `CreateParty` via REST (curl/Postman example) -- target < 30 minutes total
   - Step 4: First query: GET party by ID, search by name -- verify round-trip
   - Step 5: MCP server: configure AI assistant, first `create_party` tool call
   - Step 6: NuGet integration: add `Hexalith.Parties.Client`, `AddPartiesClient()`, send command from code
   - Each step includes exact commands to copy-paste

4. **Non-.NET developer path**: Docker deploy + REST API only (no NuGet). Documented alongside or branching from the main narrative.

5. **GDPR disclaimer** present in the getting-started guide (FR62 reference) and emergency manual erasure procedure referenced for pre-v1.1 erasure requests.

6. **Documentation quality**: Docs-as-code in repository. Markdown files pass linting. All code examples are syntactically valid and reference actual project types/endpoints.

## Tasks / Subtasks

- [x] Task 1: Rewrite `README.md` (AC: #1, #2)
  - [x] One-sentence first paragraph passing clarity test
  - [x] Positioning section: party management microservice (not auth, not CRM)
  - [x] Key features: event-sourced CQRS, MCP AI tools, NuGet client package, multi-tenant, REST API
  - [x] GDPR disclaimer prominently placed with visual emphasis
  - [x] Quick start snippet (3-4 lines: clone, run, verify)
  - [x] Links: getting-started guide, architecture overview, API reference (Swagger)
  - [x] Project structure overview (solution layout)
  - [x] License / contributing section (if applicable)
- [x] Task 2: Create `docs/getting-started.md` (AC: #3, #4, #5)
  - [x] Prerequisites section: .NET 10 SDK (10.0.103+), Docker Desktop, Git
  - [x] Step 1 - Deploy: `git clone`, `dotnet aspire run` from `src/Hexalith.Parties.AppHost`, verify Aspire dashboard
  - [x] Step 2 - First command: curl/Postman POST to `api/v1/parties` with `CreateParty` JSON body (include auth token setup)
  - [x] Step 3 - First query: GET `api/v1/parties/{id}`, GET `api/v1/parties/search?q=...`
  - [x] Step 4 - MCP server: configure Claude Desktop / VS Code with MCP endpoint, first `create_party` tool call
  - [x] Step 5 - NuGet integration: add `Hexalith.Parties.Client`, `AddPartiesClient(configuration)`, code example
  - [x] Non-.NET developer path: Docker deploy + REST-only usage (skip NuGet steps)
  - [x] GDPR disclaimer with emergency manual erasure procedure reference
  - [x] Troubleshooting section for common issues (DAPR not running, port conflicts, auth errors)
- [x] Task 3: Verify all code examples compile / run (AC: #6)
  - [x] curl commands match actual REST API routes in `PartiesController`
  - [x] JSON payloads match actual command record types
  - [x] NuGet code example uses actual `IPartiesCommandClient` / `IPartiesQueryClient` interfaces
  - [x] MCP configuration matches actual server setup in `Program.cs`

## Dev Notes

### Current State

- `README.md` is a one-line stub: "# Hexalith.Parties\nHexalith Parties micro service"
- No `docs/` folder exists -- must be created
- No getting-started guide exists

### REST API Routes (from PartiesController)

Base route: `api/v1/parties`

**Query endpoints:**
| Method | Route | Returns |
|--------|-------|---------|
| GET | `api/v1/parties` | `PagedResult<PartyIndexEntry>` (params: page, pageSize, type, active, createdAfter/Before, modifiedAfter/Before) |
| GET | `api/v1/parties/search?q=...` | `PagedResult<PartySearchResult>` (params: q, page, pageSize) |
| GET | `api/v1/parties/{id}` | `PartyDetail` |

**Command endpoints (all return 202 Accepted with `{ "correlationId": "..." }`):**
| Method | Route | Body |
|--------|-------|------|
| POST | `api/v1/parties` | `CreateParty` |
| POST | `api/v1/parties/{id}/update-person-details` | `UpdatePersonDetails` |
| POST | `api/v1/parties/{id}/update-organization-details` | `UpdateOrganizationDetails` |
| POST | `api/v1/parties/{id}/set-natural-person` | `SetIsNaturalPerson` |
| POST | `api/v1/parties/{id}/deactivate` | empty |
| POST | `api/v1/parties/{id}/reactivate` | empty |
| POST | `api/v1/parties/{id}/add-contact-channel` | `AddContactChannel` |
| POST | `api/v1/parties/{id}/update-contact-channel` | `UpdateContactChannel` |
| POST | `api/v1/parties/{id}/remove-contact-channel` | `RemoveContactChannel` |
| POST | `api/v1/parties/{id}/add-identifier` | `AddIdentifier` |
| POST | `api/v1/parties/{id}/remove-identifier` | `RemoveIdentifier` |
| POST | `api/v1/parties/create-composite` | `CreatePartyComposite` |
| POST | `api/v1/parties/{id}/update-composite` | `UpdatePartyComposite` |

### MCP Server Configuration

- MCP is mapped at `app.MapMcp().RequireAuthorization()` in `Program.cs`
- 5 MCP tools: `search_parties`, `get_party`, `create_party`, `update_party`, `delete_party` (in `src/Hexalith.Parties.CommandApi/Mcp/`)
- Tools are designed for AI ergonomics (forgiving schemas, composite operations)

### Authentication Setup for Examples

- Keycloak OIDC enabled by default (port 8180, realm `hexalith`, audience `hexalith-parties`)
- Can disable Keycloak with `EnableKeycloak=false` -- falls back to symmetric key JWT via `Authentication:JwtBearer:SigningKey`
- Tenant claim extracted from JWT for multi-tenancy
- The getting-started guide must document how to obtain a valid JWT token for API calls

### GDPR Warning

- `GdprWarningMiddleware` adds `X-GDPR-Warning` header to every response
- Warning message: "GDPR Notice: This MVP does not include GDPR compliance features (crypto-shredding, consent, erasure). Do not store regulated EU personal data. See v1.1 roadmap."
- This header will be visible in curl responses -- document and explain it

### Aspire AppHost Topology

- Entry point: `src/Hexalith.Parties.AppHost/Program.cs`
- Launches `commandapi` project with DAPR sidecar
- DAPR access control config at `DaprComponents/accesscontrol.yaml`
- Optional Keycloak integration (default on)
- Run command: `dotnet aspire run --project src/Hexalith.Parties.AppHost`

### .NET SDK Version

- `global.json` pins to `10.0.103` with `latestPatch` roll-forward

### JSON Serialization Convention (for curl examples)

```json
{
  "propertyNamingPolicy": "camelCase",
  "nullHandling": "omitWhenWritingNull",
  "enumSerialization": "string",
  "dateFormat": "ISO 8601"
}
```

Example `CreateParty` body:
```json
{
  "partyId": "550e8400-e29b-41d4-a716-446655440000",
  "partyType": "person",
  "personDetails": {
    "firstName": "Jean",
    "lastName": "Dupont",
    "prefix": null,
    "suffix": null,
    "dateOfBirth": null
  }
}
```

### Client Package Integration Example (for NuGet section)

```csharp
// In Program.cs or Startup.cs
builder.Services.AddPartiesClient(builder.Configuration);
```

```json
// In appsettings.json
{
  "Parties": {
    "BaseUrl": "https://localhost:5001"
  }
}
```

```csharp
// Usage via DI
public class MyService(IPartiesCommandClient commands, IPartiesQueryClient queries)
{
    public async Task CreateAndRetrievePartyAsync()
    {
        var correlationId = await commands.CreatePartyAsync(
            new CreateParty("my-party-id", PartyType.Person,
                new PersonDetails("Jean", "Dupont")),
            CancellationToken.None);

        var party = await queries.GetPartyAsync("my-party-id", CancellationToken.None);
    }
}
```

### File Structure to Create

```
README.md                          (rewrite existing stub)
docs/
  getting-started.md               (new)
```

### Previous Story Learnings (from Story 6.1)

- **JSON serialization**: camelCase, string enums, null omission, ISO 8601 dates. Use `McpSessionContext.JsonOptions` as the canonical reference
- **ProblemDetails error responses**: RFC 9457 format with `correlationId` extension. Status codes: 400 (validation), 401 (missing tenant), 403 (cross-tenant), 404 (not found), 422 (domain rejection)
- **Command types are sealed records** from `Hexalith.Parties.Contracts.Commands`
- **Client project** at `src/Hexalith.Parties.Client/` with interfaces `IPartiesCommandClient` and `IPartiesQueryClient`
- **DI extension**: `AddPartiesClient(this IServiceCollection services, IConfiguration configuration)` binds `Parties:BaseUrl` from config
- **Testing**: xUnit 2.9.3 + Shouldly 4.3.0 + NSubstitute 5.3.0
- **Build properties**: net10.0, nullable enabled, TreatWarningsAsErrors, file-scoped namespaces
- **271+ tests passing across the solution** (51 in client tests alone)

### Architectural Constraints

- **Docs-as-code**: documentation lives in the repository (`/docs` folder + `README.md`)
- **No DocFx or documentation site** at MVP -- plain markdown
- **Target framework**: .NET 10.0
- **File format**: UTF-8, CRLF line endings
- **Markdown style**: consistent with the rest of the repository

### Key Requirements References

- **FR32**: Getting-started documentation enables self-service onboarding
- **FR62**: Non-dismissable GDPR compliance warning until GDPR features activated
- **NFR30**: Deploy < 15 minutes on first attempt using getting-started guide
- **NFR31**: Client package < 5MB, < 10 transitive deps (achieved in 6.1)

### Project Structure Notes

- Solution uses modern XML format: `Hexalith.Parties.slnx`
- 9 source projects under `src/`, 6 test projects under `tests/`
- Central package management via `Directory.Packages.props`
- Shared build properties via `Directory.Build.props`
- MinVer for git tag-based SemVer versioning

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 6.2]
- [Source: _bmad-output/planning-artifacts/prd.md#Documentation Strategy]
- [Source: _bmad-output/planning-artifacts/prd.md#FR32]
- [Source: _bmad-output/planning-artifacts/prd.md#FR62]
- [Source: _bmad-output/planning-artifacts/prd.md#NFR30]
- [Source: _bmad-output/planning-artifacts/architecture.md#Starter Template Evaluation]
- [Source: src/Hexalith.Parties.CommandApi/Controllers/PartiesController.cs]
- [Source: src/Hexalith.Parties.CommandApi/Program.cs]
- [Source: src/Hexalith.Parties.CommandApi/Middleware/GdprWarningMiddleware.cs]
- [Source: src/Hexalith.Parties.AppHost/Program.cs]
- [Source: src/Hexalith.Parties.Client/Extensions/PartiesClientServiceCollectionExtensions.cs]
- [Source: _bmad-output/implementation-artifacts/6-1-client-package-command-and-query-abstractions.md]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- Verified all REST API routes against PartiesController.cs -- all 13 endpoints confirmed
- Verified CreateParty/PersonDetails use init-only properties (not positional constructors) -- fixed NuGet code example accordingly
- Verified MCP tool name is `find_parties` (not `search_parties` as listed in dev notes) -- used correct name in docs
- Verified JSON property `type` (not `partyType` as in dev notes example) -- used correct name in curl examples
- Verified AddPartiesClient extension method signature and `Parties:BaseUrl` config key
- Verified MCP endpoint at `/mcp` via MapMcp().RequireAuthorization()
- Full test suite: 322 tests pass, 0 failures, 0 skipped

### Completion Notes List

- **Task 1**: Rewrote README.md from one-line stub to comprehensive project documentation. Includes one-sentence clarity paragraph, positioning section, key features list, GDPR disclaimer with visual emphasis, 3-line quick start snippet, links to getting-started guide and API reference, full project structure overview, and MIT license section.
- **Task 2**: Created docs/getting-started.md with complete narrative arc: prerequisites table, local deployment with Aspire, Keycloak auth token setup, first CreateParty command via curl, query/search examples, MCP configuration for Claude Desktop and VS Code, NuGet client package integration with typed C# example, non-.NET developer path (Docker + REST only), GDPR disclaimer with emergency manual erasure section, and troubleshooting for 5 common issues.
- **Task 3**: Verified all code examples against actual source code. Found and corrected: PersonDetails uses init-only properties (not positional constructor), MCP tool is `find_parties` (not `search_parties`), JSON property is `type` (not `partyType`). All curl routes, JSON payloads, client interface methods, and MCP configuration verified accurate.
- **Code review fixes**: Added the missing architecture overview link to README, replaced placeholder Keycloak instructions with imported development realm credentials, added PowerShell equivalents for Windows copy-paste flows, and packaged AppHost `KeycloakRealms` content so the documented token flow is available in local runs.

### Change Log

- 2026-03-06: Story 6.2 implementation complete -- README.md rewritten, docs/getting-started.md created, all code examples verified against source
- 2026-03-06: Code review fixes applied -- architecture link added, local Keycloak realm wired into AppHost, Bash/PowerShell command paths documented, story metadata synced with git changes

### File List

- README.md (modified -- complete rewrite from stub)
- docs/getting-started.md (new -- getting-started guide)
- src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj (modified -- includes Keycloak realm content in local AppHost output)
- src/Hexalith.Parties.AppHost/KeycloakRealms/hexalith-realm.json (new -- local development realm for documented quickstart authentication)
- _bmad-output/implementation-artifacts/6-2-getting-started-guide-and-readme.md (modified -- review notes, status, and file list synced)
- _bmad-output/implementation-artifacts/sprint-status.yaml (modified -- story moved to done)

## Senior Developer Review (AI)

### Outcome

Approved

### Review Notes

- Fixed the missing README architecture overview link required by AC #2.
- Replaced the placeholder token acquisition flow with a real local-development Keycloak flow backed by an imported realm and known test credentials.
- Added PowerShell equivalents for token creation, command submission, query examples, Keycloak disabling, and port diagnostics so the guide is copy-pasteable on Windows.
- Synced the story file list with actual git-tracked review changes.
