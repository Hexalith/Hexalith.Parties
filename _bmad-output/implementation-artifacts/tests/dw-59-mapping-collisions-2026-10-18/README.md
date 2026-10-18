# DW-59 mapping collision verification

Recording a known Memories unit now prioritizes its eligible unit-ID mapping over
source-only matches. Distinct units remain discoverable for cleanup when the
incoming unit ID and source URI match different rows. Existing source-only
replacement for previously unseen units, case isolation, legacy fallback,
idempotency, duplicate-unit collapse, storage schema and keys remain compatible.

The original production algorithm built cleanly and failed all 11 initial
collision regressions. The final Debug/source-reference build has zero warnings
or errors. All 180 focused tests and 1251 service tests pass with no skips or
unrun tests. Tests assert serialized persisted inventories, fresh-reader results,
both row orders, legacy fallback routes, exact partial-failure retention, cleanup
retry, subsequent source replacement and real concurrent indexing/cleanup saves.

Independent review raised four findings; all were patched and follow-up inspection
confirmed the corrections with no new concrete regression. No findings were
deferred. The warning-override/nested-submodule guard and whitespace check pass.
The pinned commitlint CLI accepted the exact message with zero problems/warnings.

Aspire baseline startup and the Parties readiness wait succeeded. Resource
inspection reported Parties and its required runtime dependencies healthy; the
optional MCP and admin UI resources were not started. The AppHost was stopped and
its stopped state confirmed before builds. Full-topology and terminal physical
erasure qualification are not claimed.

This is a forward fix: mappings discarded by older writers are not recovered.
Drain those writers before relying on collision preservation. Broader source-only
replacement policy, terminal workflow reconciliation, distributed erasure fencing,
cancellation recovery and the DW-81 persistence migration remain separate work.
No dependency versions, submodule identities, deployment or real data changed.

`validation.json` records canonical baseline/dependency revisions, source hashes,
commands, test counts, review disposition and limits. Raw local logs/reports are
summarized to avoid publishing workstation paths or runtime credentials.
