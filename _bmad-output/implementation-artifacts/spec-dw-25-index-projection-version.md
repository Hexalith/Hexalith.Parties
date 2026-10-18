---
title: 'DW-25 Party index projection version semantics'
type: 'refactor'
created: '2026-10-18'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** DW-25 records that the shared Party index's `ProjectionVersion` formats and limits are undocumented. Projection and query consumers can mistake its source-position hint for tenant-wide completeness, a content revision, or a freshness guarantee.

**Approach:** Document the existing `global:N`, `{aggregateId}:{sequence}`, and retained/unknown cases at the SDK handler/fold and read-model property, with consumer guidance in the existing data-model documentation. Explain duplicate delivery, rebuild, cleanup, and timestamp-based freshness implications from current source. Complete DW-25 only.

</frozen-after-approval>

## Implementation Notes

Planning classification: no unresolved intent gaps or irreversible actions. Small documentation-only footprint; no executable behavior, public API, persistence, dependency, or gitlink changes. Use the oneshot route. This deferred-work invocation selects one independently actionable ledger item.

Code map: `src/Hexalith.Parties.Projections/Handlers/PartyIndexSdkProjectionHandler.cs` owns `FoldCore`, `ParseGlobalPosition`, duplicate no-op detection and rebuild accumulation. Add class/fold remarks without changing statements. `Models/PartyIndexSdkReadModel.cs` exposes the version; document its nullable diagnostic meaning. `docs/data-models.md` already describes the shared index and is the consumer-facing destination. `PartySdkReadModelEraser.RemoveParty` preserves the version during cleanup. `PartySdkQueryService.Success` forwards it through SDK query metadata; freshness classification uses `ProjectedAt`, and degraded fallback overrides status. The SDK freshness extension forwards the stored version without interpreting its numeric text.

Document selection precedence precisely: greatest positive delivered global position or retained parseable global position; otherwise this request aggregate's retained/advanced sequence; otherwise prior version, including null. The global maximum includes supplied events regardless of entry mutation; it does not certify a contiguous prefix. Duplicate-only live deliveries can skip candidate persistence even when global metadata differs. Fallback sequence values belong to one aggregate and can switch aggregate identities. Rebuild starts empty and uses the same fold, so comparisons across rebuilds are unsafe. Cleanup can alter entries and time while retaining the version.

Verification: build the affected projections test project in Debug with local source references and warnings as errors, then run its existing SDK projection handler tests directly through xUnit v3. Inspect prose against the fold, cleanup and query source; validate XML remarks and local Markdown links, plus whitespace and the repository warning-override/nested-submodule gate. Do not add documentation-mirroring tests or start the distributed topology for comment-only changes. Record exact results in `tests/dw-25-index-projection-version-2026-10-18/validation.json`. Independent blind review precedes completion; update only DW-25 in the ledger.

Implemented class/fold XML remarks and read-model property documentation, plus a consumer section in `docs/data-models.md`. Comparison with the baseline confirms that all C# changes are XML comments.

Final verification: Debug/source build passes with zero warnings/errors; existing focused SDK projection handler tests pass 89/89 with zero failures/errors/skips. All eight XML comment blocks parse, the new local Markdown link resolves, whitespace validation and the warning-override/nested-submodule gate pass. The exact Conventional Commit candidate passes repository-pinned commitlint 21.2.2. Evidence is in `tests/dw-25-index-projection-version-2026-10-18/validation.json`. This documentation change does not qualify runtime/topology behavior or claim a full-solution test gate.

## Review Triage Log

- Low, patch: the initial selection table omitted early delivery-validation failure. `FoldCore` returns the current/empty model for sequence gaps, conflicting duplicates and unresolved events before calculating a version. Qualified the table and XML remarks with successful validation.
- Low, patch: timestamp freshness needed a convergence limit. `ProjectedAt` is the maximum incorporated event/cleanup time for the shared index, so one party's activity can classify the index as current while another party is behind. Added this limit to prose and XML remarks.
- Low, patch: address validation accepts the aggregate identifier `global`, whose fallback text is parsed as a global token on a later fold. Documented the existing ambiguity and the resulting inability to infer authoritative global-position provenance from its prefix. No runtime format or identifier policy changed.
- Low, patch: define parseable retained global tokens. `ParseGlobalPosition` uses an ordinal lowercase prefix and `long.TryParse` with `NumberStyles.None` and invariant culture. Documented decimal Int64 bounds, rejection of signs/whitespace/overflow, and zero's lack of positive precedence.

Independent follow-up verified all four corrections against executable source and found no remaining factual discrepancy. All findings are resolved through documentation corrections; no new deferred work. DW-25 is complete.
