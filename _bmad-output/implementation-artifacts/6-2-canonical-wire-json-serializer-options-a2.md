---
baseline_commit: 4a3b518
---

# Story 6.2: Canonical wire JSON serializer options (A2)

Status: ready-for-dev

## Story

As a maintainer,
I want one canonical Parties wire JSON options object,
so that command, query, projection, and protection paths serialize the same contract shape.

## Acceptance Criteria

1. Given serializer options are hand-copied in multiple projects, when `PartiesJsonOptions.Default` is introduced in `Contracts`, then all wire serialization callers use camelCase, `WhenWritingNull`, and `JsonStringEnumConverter` from the shared source.
2. Given `PartyPayloadProtectionService` currently omits the enum converter, when it adopts the shared options, then encrypted/protected payload serialization uses the canonical enum behavior.
3. Given projection rebuild/replay reader paths may need permissive read behavior, when they remain separate, then they use clearly named local reader options and tests document why they differ from canonical wire serialization.
4. Given JSON options are shared from `Contracts`, when the project builds, then no infrastructure dependency is added to `Contracts`.
5. Given the consolidation is complete, when focused serialization tests run, then representative commands/events/read models preserve their previous wire shape except for fixing the known enum-converter drift.

## Tasks / Subtasks

- [ ] Add `PartiesJsonOptions` in Contracts (AC: 1, 4)
  - [ ] Expose a reusable read-only `JsonSerializerOptions` instance or factory that avoids accidental mutation.
  - [ ] Include camelCase naming, `DefaultIgnoreCondition = WhenWritingNull`, and `JsonStringEnumConverter`.
- [ ] Replace duplicated wire serializer options (AC: 1, 2)
  - [ ] Update command/query client serialization call sites.
  - [ ] Update payload protection serialization.
  - [ ] Update projection/event serialization call sites where they represent the wire contract.
  - [ ] Remove obsolete local `JsonSerializerOptions` copies.
- [ ] Preserve intentional reader options (AC: 3)
  - [ ] Rename projection rebuild or replay options to make read-only/permissive intent explicit.
  - [ ] Keep case-insensitive reader behavior only where compatibility requires it.
- [ ] Add tests (AC: 1-5)
  - [ ] Assert enum values serialize consistently through the shared options.
  - [ ] Assert null values are omitted where expected.
  - [ ] Assert projection reader options are intentionally separate.
- [ ] Validate (AC: 5)
  - [ ] Run `git diff --check`.
  - [ ] Run focused Contracts, Client, Security, and Projections tests touched by JSON serialization.
  - [ ] Run solution build if available.

## Dev Notes

### Decision Context

- This story implements Class A item A2.
- The approved change fixes real drift: `PartyPayloadProtectionService` must regain `JsonStringEnumConverter`; projection rebuild reader behavior must be separated by name instead of looking like another canonical wire serializer.

### Guardrails

- Do not change event contract names, remove fields, rename fields, or introduce non-additive contract changes.
- Do not add Newtonsoft.Json or any new serialization package.
- Do not make `JsonSerializerOptions` mutable global state that later callers can alter.
- Keep generated `obj/**/generated` output untouched.

### References

- `_bmad-output/planning-artifacts/epics.md#Story-6.2-Canonical-wire-JSON-serializer-options-A2`
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-28.md#Class-A--Internal-consolidation-approved--Epic-6`
- `_bmad-output/project-context.md#Technology-Stack--Versions`
- `src/Hexalith.Parties.Security/`
- `src/Hexalith.Parties.Projections/`

