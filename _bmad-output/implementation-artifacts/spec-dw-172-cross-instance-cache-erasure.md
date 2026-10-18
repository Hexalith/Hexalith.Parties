---
title: 'DW-172 prevent cached personal data after cross-instance erasure'
type: 'bugfix'
created: '2026-10-18'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '749b26d303e448bddafb1e56402bfe676bc92d7f'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Erasure evicts only the handling host's cache. Another Parties instance can disclose pre-erasure detail or index entries when its canonical SDK read fails after shared cleanup (DW-172).

**Approach:** Reject personal-data-bearing detail and every nonempty index as degraded fallback. Preserve already-erased detail after defensive redaction, absent detail, empty indexes, and existing Article 30 processing retention. Approval accepts reduced outage availability: cached personal data is withheld even without known erasure because another instance may have erased it.

## Boundaries & Constraints

**Always:** Preserve healthy reads, response shapes, tenancy, cancellation, bounded diagnostics, freshness, cache bounds and read-token cleanup. Evict unsafe candidates. Preserve redaction timestamps. Never falsely report an active party erased or an unknown index empty.

**Never:** Add persistence, distributed fencing, APIs, raw Dapr calls, dependencies, gitlinks, deployments or pushes. Status/cross-slot probes cannot authorize cached personal data. Complete only DW-172; canonical-read consistency and other erasure races remain outside scope.

## I/O & Edge-Case Matrix

| State | Expected behavior |
|-------|-------------------|
| A erases; B retains personal cache; B canonical read fails | Detail aliases, export, list and search disclose no cached personal data; bounded failure and eviction |
| Ordinary outage with active detail/nonempty index | Same rejection; no false erased/empty success |
| Cached erased detail, absent detail or empty index | Safe established response with stale/degraded metadata; defensively strip personal fields from malformed erased detail |
| Canonical detail readable; processing unavailable | Existing retained-record/empty fallback; export and processing cannot bypass failed detail admission |
| Cancellation, saturation or recovery | Cancellation propagates; tokens release; healthy reads recover independently by tenant/party |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Parties/Queries/PartySdkQueryService.cs`: detail/index exception filters admit all cached values; detail aliases/export and list/search converge here. Processing follows detail admission.
- `src/Hexalith.Parties.Projections/Handlers/PartyDetailProjectionHandler.cs`: `ApplyErasure` defines removed personal fields; it replaces timestamps, so preserve stored timestamps when reusing its policy.
- `src/Hexalith.Parties.Projections/Services/PartySdkReadModelEraser.cs`: use unchanged in tests; actual coordinated cleanup retains permanent tombstones and processing records.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.Parties/Queries/PartySdkQueryService.cs` — restrict fallback, defensively redact already-erased detail, evict unsafe candidates and preserve cleanup/cancellation.
- [x] `tests/Hexalith.Parties.Tests/Gateway/PartySdkQueryHandlerTests.cs` — revise personal fallback expectations; retain safe fallback, processing, saturation, cancellation and recovery coverage.
- [x] `tests/Hexalith.Parties.Tests/Gateway/PartySdkCrossInstanceErasureTests.cs` — two independent caches over SDK `InMemoryReadModelStore`; real erasure and tombstone read-back; B outages without manual eviction. Cover query discriminators, serialized privacy canaries, delayed candidates and unaffected tenants. One helper type per file.
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` — resolve only DW-172 after validation. Finalize this spec and record sanitized commands/results under `tests/dw-172-cross-instance-cache-erasure-2026-10-18/` beside it.

**Acceptance Criteria:**
- Given two populated caches and completed shared cleanup, when B's canonical detail/index read fails, then no affected response contains pre-erasure personal data.
- Given an ordinary outage or unsafe delayed cache candidate, when fallback runs, then the same rejection applies without manufacturing erasure or empty-index success.
- Given safe cached content, when canonical reads fail, then its established safe semantics and stale/degraded metadata remain available.
- Given cancelled, saturated or failed reads, when a query ends, then cancellation and token-release guarantees remain intact.

## Implementation Notes

Approved and continued on 2026-10-18. Implementation changes only the SDK query service's detail/index fallback admission; rejected candidates are evicted, and already-erased fallback is redacted with original timestamps restored. Processing retention, canonical reads and cache tracking remain intact. Added the two-cache regression class and a separate fault/delay store helper; updated affected query/cache controls.

Matrix audit: remote-erasure and ordinary-outage tests run all six affected discriminators; erased-detail redaction covers both aliases and export; empty index covers list/search; absent detail preserves not-found; processing tests cover warm/empty retained records after real cleanup. Delayed-candidate, saturation, cached-cancellation, tenant-isolation, recovery and read-token release tests all ran. Original service failed 16/20 initial cases. Final Debug/source build has zero warnings/errors; cross-instance 26/26, query/cache 98/98 and full host 1282/1282 pass with no skips. Parent audit verified retained source and assembly hashes against disk. Exact commands/results are in `tests/dw-172-cross-instance-cache-erasure-2026-10-18/validation.json`.

Runtime baseline was environment-blocked: Aspire started but security remained Running/Unhealthy, leaving Parties waiting; `aspire wait security --timeout 5 --apphost src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj --non-interactive` did not reach Healthy. Owned topology was stopped before builds; a subsequent describe confirmed none running. Independent review follows; no dependency or gitlink changed.

Review corrections are complete: fallback now requires positively empty index Entries (null rejects); privacy assertions inspect raw payload bytes and require payload-free failures; warmed unrelated tenant/same-tenant detail caches are checked before/after rejection; delayed reads release and observe their task in finally. Maintained architecture/API guidance documents the approved availability restriction. The executable CRLF check is retained with exact commands.

Final parent verification after these corrections: Debug/source build has zero warnings/errors; cross-instance tests 28/28, query/cache tests 98/98 and full host tests 1284/1284 pass with no errors, failures, skips or not-run tests. Warning-policy and CRLF-aware whitespace checks pass; retained source hashes match disk and the parent rebuild's test-assembly hash is recorded separately from earlier runs. All three configured review layers completed; every finding is triaged below, all patches verified, no new deferred work. DW-172 is resolved; runtime health remains the separately recorded baseline limitation. No remote operation occurred.

## Spec Change Log

## Review Triage Log

- Blind 1 — false, rejected: saturation did not introduce canonical-read rejection. Baseline already throws when Store*IfCurrent rejects an untracked/invalidated token; only its unsafe personal fallback is now withheld as approved. Returning untracked canonical values would bypass existing local-erasure invalidation. Existing empty/redacted saturation controls still pass and admitted reads recover after release.
- Blind 2 — low, patch: null index Entries passes the positive-count guard and becomes an empty response. Require a positively empty collection for fallback and cover malformed null indexes for list/search.
- Blind 3 — low, rejected: candidate lookup and eviction can race a safe same-key replacement, causing a transient conservative failure. Conditional eviction would add synchronized cache API/state complexity for a rare availability-only race; unconditional same-key invalidation preserves the privacy guarantee and recovery. No unrelated tenant/key is evicted.
- Blind 4 — low, patch: failure payload bytes would serialize as base64 and escape the plaintext canary check. Assert bounded failures have no payload and decode nonempty payload bytes independently of Success.
- Blind 5 — low, patch: the tenant control uses healthy store reads without proving previously cached unrelated content survives eviction. Warm and inspect unrelated tenant/party cache entries and assert expected returned content.
- Blind 6 — low, patch: an assertion failure before delayed-read release can leave the pending task unobserved. Release and observe it in finally.
- Blind 7 — low, patch: the approved outage availability change lacks maintained query documentation. Correct the architecture/query guidance beside the existing freshness contract.
- Blind 8 — low, patch: the recorded Python whitespace check describes an operation without executable command text. Retain and execute a reproducible exact command.
- Edge Case Hunter — no findings.
- Verification Gap Reviewer — no verification gaps.

## Design Notes

Dispatch: changed outage behavior requires approval. No intent gaps, irreversible actions, schema changes or new callable surfaces. I10 last-known behavior survives for safe content; privacy takes precedence over unconditional personal-data fallback.

Baseline: `749b26d303e448bddafb1e56402bfe676bc92d7f`. Status writes precede append (DW-192) and omit cleanup/replay. Detail/index deliveries are independent; SDK reads lack a strong-consistency guarantee. These probes cannot safely authorize personal cache.

## Verification

Record Aspire baseline before code changes; stop owned topology before builds. Reproduce disclosure against original code. Use:

- `dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -m:1` — zero warnings/errors.
- `dotnet tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.Gateway.PartySdkCrossInstanceErasureTests` — regression matrix passes; repeat for `Hexalith.Parties.Tests.Gateway.PartySdkQueryHandlerTests`.
- Run the same assembly without filters — full host suite passes without skips.
- `bash scripts/check-no-warning-override.sh` and CRLF-aware whitespace validation — pass.

Complete independent review. Record blockers separately from focused success; keep DW-172 open until implementation and validation complete.
