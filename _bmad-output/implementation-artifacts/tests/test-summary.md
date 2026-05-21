# Test Automation Summary

## Generated Tests

### API / Query Tests
- [x] `tests/Hexalith.Parties.Tests/Search/PartySearchServiceBoundaryTests.cs` - Added display-name MVP search metadata inertness coverage for future score/source fields.
- [x] Existing `PartySearchServiceBoundaryTests` - Reserved `Hybrid`, `Semantic`, and `Graph` local search modes return bounded unsupported results without enumerating projections or calling providers.
- [x] Existing `PartyIndexProjectionQueryActorTests` - `Hybrid`, `Semantic`, `Graph`, `Email`, `Identifier`, and `TemporalName` wire modes fail before projection reads, while omitted/`Lexical`/`DisplayName` modes keep display-name search behavior.
- [x] Existing `PartySearchContractCompatibilityTests` - Additive search contract metadata remains default/null-safe and source-compatible.
- [x] Existing `PartyDetailProjectionHandlerNameHistoryTests` and `PersonalDataInventoryTests` - Name history is retained for preparation, erased safely, and classified as personal data.

### E2E / UI Surface Tests
- [x] `tests/Hexalith.Parties.Picker.Tests/Components/PartyPickerComponentTests.cs` - Added picker guardrails proving the default rendered workflow exposes no advanced search or temporal controls and default search forwards no advanced mode/case id.
- [x] `tests/Hexalith.Parties.Mcp.Tests/PartiesMcpToolContractTests.cs` - Added MCP `find_parties` contract guardrail proving no semantic, hybrid, graph, temporal, `asOf`, or case arguments are exposed on the search tool.
- [x] Existing `PartiesAdminPortalApiClientTests` and `PartiesAdminPortalComponentTests` - Admin search remains typed-client/query-service based, hides unsupported filters in search mode, and keeps rich-search capability local-only unless explicitly probed healthy.
- [x] Existing EventStore gateway and actor-boundary tests cover the accepted query workflow through `POST api/v1/queries` without reintroducing retired Parties REST routes.

## Coverage
- API/query search modes: MVP accepted modes 3/3 (`omitted`, `Lexical`, `DisplayName`); reserved/future modes 6/6 (`Hybrid`, `Semantic`, `Graph`, `Email`, `Identifier`, `TemporalName`).
- MVP match metadata: active `displayName` field covered; future email, identifier, contact-channel, semantic, graph, hybrid, temporal, provider, vector, type, and duplicate metadata guarded as absent/inert.
- Temporal preparation: name-history retention, erasure suppression, personal-data marking, and no typed client temporal method coverage are present.
- Public surface guardrails: actor host REST/OpenAPI/MCP absence, client no temporal/advanced methods, picker default UI, admin search surface, and MCP `find_parties` arguments covered.
- Dependency guardrails: `Hexalith.Parties.Contracts` package closure checked; no semantic/vector/graph/temporal backend dependency found.

## Validation
- [x] `dotnet test tests\Hexalith.Parties.Tests\Hexalith.Parties.Tests.csproj --configuration Release --filter FullyQualifiedName~PartySearchServiceBoundaryTests` - Passed 11/11.
- [x] `dotnet test tests\Hexalith.Parties.Picker.Tests\Hexalith.Parties.Picker.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyPickerComponentTests` - Passed 17/17.
- [x] `dotnet test tests\Hexalith.Parties.Mcp.Tests\Hexalith.Parties.Mcp.Tests.csproj --configuration Release --filter "FullyQualifiedName~PartiesMcpToolContractTests|FullyQualifiedName~PartiesMcpToolDispatchTests"` - Passed 21/21.
- [x] `dotnet test tests\Hexalith.Parties.Tests\Hexalith.Parties.Tests.csproj --configuration Release --filter "FullyQualifiedName~MvpDisplayNameSearchContractTests|FullyQualifiedName~PartyIndexProjectionQueryActorTests|FullyQualifiedName~ArchitecturalFitnessTests"` - Passed 65/65.
- [x] `dotnet test tests\Hexalith.Parties.Contracts.Tests\Hexalith.Parties.Contracts.Tests.csproj --configuration Release --filter "FullyQualifiedName~PartySearchContractCompatibilityTests|FullyQualifiedName~PersonalDataInventoryTests"` - Passed 6/6.
- [x] `dotnet test tests\Hexalith.Parties.Projections.Tests\Hexalith.Parties.Projections.Tests.csproj --configuration Release --filter FullyQualifiedName~PartyDetailProjectionHandlerNameHistoryTests` - Passed 10/10.
- [x] `dotnet test tests\Hexalith.Parties.Client.Tests\Hexalith.Parties.Client.Tests.csproj --configuration Release --filter "FullyQualifiedName~HttpPartiesQueryClientTests|FullyQualifiedName~ClientArchitecturalFitnessTests"` - Passed 32/32.
- [x] `dotnet test tests\Hexalith.Parties.AdminPortal.Tests\Hexalith.Parties.AdminPortal.Tests.csproj --configuration Release --filter "FullyQualifiedName~PartiesAdminPortalApiClientTests|FullyQualifiedName~PartiesAdminPortalComponentTests"` - Passed 63/63.
- [x] `dotnet package list --project src\Hexalith.Parties.Contracts\Hexalith.Parties.Contracts.csproj --include-transitive` - Completed; no semantic/vector/graph/temporal backend dependency present.

## Notes
- `python3` was not available, so the BMAD workflow customization was resolved manually from `customize.toml`; no prepend/append steps and no `on_complete` command were configured.
- `dotnet` was invoked through `C:\Program Files\dotnet\dotnet.exe` because the shell PATH does not expose `dotnet`.
- One parallel validation attempt hit a transient `Hexalith.Parties.Tests.dll` file lock from compiling the same project concurrently; the affected command was rerun serially and passed.
