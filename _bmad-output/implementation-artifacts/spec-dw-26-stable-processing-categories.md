---
title: 'DW-26 stable processing activity categories'
type: 'bugfix'
created: '2026-10-17'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** DW-26 remains open: the Article 30 processing-activity fold uses individual event names as operation categories when the payload has no explicit category mapping. The ledger's September 6 decision already requires the stable fallback `Other` and separate event-type diagnostics.

**Approach:** Apply `Other` to newly derived uncategorized processing records, keeping their short `EventType`, summary, outcome, checkpoint and retry behavior. Preserve `Consent`, `Restriction`, `Erasure`, and `PartyCommand` mappings. Cover unresolved short/qualified names, resolved uncategorized events, and corrupt/redacted payloads. Existing persisted records remain readable; a normal supported projection rebuild derives the new categories without an in-place data migration.

</frozen-after-approval>

## Implementation Notes

- Investigation: `src/Hexalith.Parties.Projections/Handlers/PartyProcessingActivityFold.cs` owns the sole category mapping; remove the unused event-name parameter from `GetOperationCategory` when changing its default. Reuse the event constructors, `Event` helper and fold assertions in `tests/Hexalith.Parties.Projections.Tests/Handlers/PartySdkProjectionHandlerTests.cs`.
- No intent gaps or irreversible actions. No new public API, contract-default change, dependency update or platform-source edit. The consumer adapter already displays arbitrary categories through its generic Activity fallback, including `Other`.
- Validation: build `Hexalith.Parties.Projections.Tests` in Debug with `UseHexalithProjectReferences=true` and `UseNuGetDeps=false`, invoke the built xUnit assembly for the processing-fold focus, then run the full project. Record a failing baseline before changing the mapper and a passing result afterward.
- Baseline topology: exact Parties AppHost started through Aspire CLI. `aspire describe --non-interactive --apphost src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj` reported security Unhealthy and application services Waiting. The exact AppHost was stopped successfully before edits. Logs are retained in `/tmp/dw-26-validation`; no healthy runtime acceptance is claimed.
- Implemented the mapper and removed its event-name parameter; retained the independent contract default and all established mappings. Added regression assertions and documentation in `docs/gdpr-processing-activity-records.md`. DW-26 is resolved in the ledger.
- Regression evidence: with the old mapper, six of ten focused cases fail on the category assertion. Initial fixed focus passes 10/10; full project passes 256/256. After review improvements, final focus passes 13/13 and full project passes 259/259; no failures/errors/skips/unrun tests. Debug source-reference builds have zero warnings/errors. No broader test lane is needed for this localized mapping.
- Review improvements cover decodable redacted events, legacy append compatibility, and serialized SDK rebuild-plan category/metadata preservation. Representative sanitized raw logs/XML and `validation.json` are retained in `tests/dw-26-stable-processing-categories-2026-10-17/` beside this spec.

- Commit validation: repository-pinned commitlint 21.2.2 accepts the exact local commit message with zero problems/warnings; evidence is retained beside `validation.json`. No push.

## Review Triage Log

- Low, patched: documentation overgeneralized redacted events. Confirmed `json-redacted` can decode `PartyDeactivated` to a recognized payload; clarified the statement and covered its preserved category/outcome/checkpoint.
- Low, patched: the legacy-read compatibility promise lacked a direct assertion. Added a checkpointed legacy record and verified append preserves the entire existing record while a new uncategorized record uses `Other`.
- Medium, patched: the documented rebuild route lacked category-specific serialized-slot proof. Added `PrepareRebuildAsync` coverage with an existing legacy slot, exact metadata/outcome/event-type equality, normalized category, and matching concurrency ETag.
- Low, patched: temporary-only output weakened durable verification evidence. Retained baseline/final XML, final build/test text and topology describe/stop output, stripping repository prefixes and machine/user metadata. No new deferred items.
