# EXT-PARTIES-1 minimal retention application

The user's accepted engineering recommendation is implemented locally. [The proposed policy](../planning-artifacts/actor-history-retention-policy-v1.md) selects one finite, purpose-specific actor-history lifetime on the existing aggregate/SDK seams. It has no approved production duration or provider and installs no runtime defaults.

Current human-binding and historical reads now require explicit valid policy configuration, exact custody policy/purpose and effective-at-derived expiry. They snapshot policy and recheck after source/custody awaits and final authority before releasing bindings. A missing, mismatched or withdrawn policy returns Unavailable. Organization Branch B still resolves without human policy. Actor/version/half-open interval and original source positions remain unchanged under a valid policy.

The rebind fixtures now derive successor expiry from the successor's own effective instant. The fixture's ten-day period is synthetic; it is not a production recommendation or approval. No new persistence, general policy engine, service, provider or erasure-trigger clock was added.

## Verification

Normal Debug source build: **0 warnings, 0 errors**. Focused xUnit v3 query/admission suite: **78 passed, 0 failed, 0 skipped, 0 errors, 0 not run**. The 26 new cases cover missing/unsupported configuration, wrong policy/duration/expiry/purpose/custody flags, actual suspended source/custody reads during policy change and final-authority policy withdrawal. Existing erasure, rebind/revoke, exclusive expiry, original-position and cancellation tests also ran. Both current and historical modes are exercised inside each configuration/mismatch case.

[Exact source/artifact hashes and commands](tests/actor-retention-apply-2026-10-06/source-evidence.json), [build output](tests/actor-retention-apply-2026-10-06/build.log), [test output](tests/actor-retention-apply-2026-10-06/tests.log), [xUnit results](tests/actor-retention-apply-2026-10-06/tests.xml).

The first build caught two CA2007 findings in the new private test helper; the helper was corrected with ConfigureAwait and the normal build rerun. No analyzer or assertion was suppressed. Previous evidence is preserved as earlier snapshots; this packet supersedes its current query-source coverage. The prior broader Local result remains 489/490 with the strict SDK json-redacted replay failure; no broad rerun or waiver is claimed here.

## Remaining qualification

Product/Governance must approve exact post-profile-erasure scope and finite duration. Owners must deliver/qualify independent custody, source and derived-copy cleanup, fresh nonrollback lifecycle and restore behavior. The strict history fold also needs owner proof that complete retained lifecycle remains demonstrable when older predecessors expire. Production activation, full P-01–P-10 acceptance and Story 5.4 remain blocked. One configured policy version is supported; changing configuration cannot extend old records and makes mismatched versions unavailable.

The BMad independent review is separately pending; passing local tests is not a completed review or production acceptance.
