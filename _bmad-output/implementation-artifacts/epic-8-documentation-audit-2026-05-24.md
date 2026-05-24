# Epic 8 Documentation Audit

Project: Hexalith.Parties
Epic: 8 - Embeddable Party Picker
Audit date: 2026-05-24

## Method

Reviewed Epic 8 story implementation notes, current picker source, and candidate documentation that could have drifted during implementation. Proposed updates were applied only where current documentation disagreed with code or encouraged the wrong integration contract.

## Documents Reviewed

| Document | Verification Result | Action |
|---|---|---|
| `docs/frontend/party-picker.md` | Blazor usage example used the source-compatible preview callback as the primary example, while code and story closure establish `SelectedPartyId` / `SelectedPartyIdChanged` as the durable host binding. | Updated example and prose to lead with durable party-id binding. |
| `README.md` | Project structure listed adopter-facing packages but omitted `Hexalith.Parties.Picker`, which now exists as an adopter-facing RCL package. | Updated key features and project structure. |
| `_bmad-output/planning-artifacts/architecture.md` | Party Picker Frontend Surface matches implementation: durable party id, typed client boundary, no retired/internal transports, host-supplied auth, stale-response handling, bounded states, and privacy exclusions. | No update. |
| `_bmad-output/planning-artifacts/prd.md` | FR67 remains accurate for embeddable party picker capability. No implementation-level divergence found. | No update. |
| `_bmad-output/planning-artifacts/epics.md` | Epic 8 stories and UX-DR22 through UX-DR32 match the completed implementation and story records. | No update. |
| `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md` | Correctly records scoped risk acceptance for Epic 8 stories while keeping the full dependency not globally satisfied. | No update. |

## Code Evidence Checked

- `src/Hexalith.Parties.Picker/Components/PartyPicker.razor`
- `src/Hexalith.Parties.Picker/Services/PartyPickerApiClient.cs`
- `src/Hexalith.Parties.Picker/Services/PartyPickerEventDetail.cs`
- `src/Hexalith.Parties.Picker/Services/PartyPickerLabels.cs`
- `src/Hexalith.Parties.Picker/Services/PartyPickerDefaults.cs`
- `src/Hexalith.Parties.Picker/wwwroot/hexalith-parties-picker.js`
- `src/Hexalith.Parties.Picker/Hexalith.Parties.Picker.csproj`
- `src/Hexalith.Parties.Client/Abstractions/IPartiesQueryClient.cs`
- `src/Hexalith.Parties.Client/HttpPartiesQueryClient.cs`

## Verified Implementation Facts

- Picker package exists at `src/Hexalith.Parties.Picker` and references only `Hexalith.Parties.Client`, `Hexalith.Parties.Contracts`, and Blazor custom elements.
- `PartyPicker` exposes `SelectedPartyId` and `SelectedPartyIdChanged` for durable host storage.
- `SelectedPartyChanged` remains available for preview display data.
- DOM `party-selected` dispatch is whitelisted to `partyId`, `partyType`, and `status`.
- Search and selected-party display lookup route through `IPartiesQueryClient`.
- Host authentication remains supplied by token provider, token property, or request customizer; the picker does not persist or refresh tokens.
- Empty/invisible queries avoid backend calls, page size is bounded to `1..100`, and failure/state text is localized and bounded.

## Discarded Proposed Updates

- Architecture decision update: discarded because the Party Picker Frontend Surface already matches the code and dependency status.
- PRD update: discarded because FR67 is still correct and does not over-specify stale implementation details.
- Epics update: discarded because the current Epic 8 story definitions align with completed story records.
- Dependency record update: discarded because scoped risk acceptance and residual risk are already accurately recorded.
