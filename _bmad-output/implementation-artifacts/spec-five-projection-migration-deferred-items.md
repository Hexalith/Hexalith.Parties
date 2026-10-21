---
title: 'Complete five deferred projection migration items'
type: 'refactor'
created: '2026-10-21'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Five open projection-migration deferrals leave actor-era source names and configuration (DW-32), weaker query and host fixtures (DW-34), sequential live reads and validation coupling (DW-51), resolver placement under retired Actors (DW-57), and unqualified compatibility-spec drift (DW-61).

**Approach:** Complete the five related items as requested. Introduce canonical SDK query names while preserving obsolete aliases and wire discriminators; prefer Parties projection configuration with legacy fallback. Use the production protected cursor codec and identify excluded hosted services precisely. Parallelize paired live reads and share address/store validation. Move event resolution under serialization. Run and repair the projection compatibility spec against the existing SDK boundary, record evidence, independently review, then commit and push main.

</frozen-after-approval>

## Implementation Notes

- User authorized five items and push on main; initial clean revision f3e06a078d1d9bbb1be84df8fae67666829339a1. These are bounded reversible changes; no dependency updates, stored-data migration or deployment. The compatibility names are additive and retain existing values. No unresolved intent gap; oneshot route.
- Code map: src/Hexalith.Parties/Queries/Party{Detail,Index}ProjectionQueryActor.cs owns discriminator constants and index payload parsing. Introduce PartyDetailQueryTypes/PartyIndexQueryTypes; old public constants forward to those types and remain obsolete aliases. SDK handlers/services use canonical source names; client wire strings remain historical. Configuration/PartySdkReadModelOptions.cs and AddParties options binding gain Parties:Projections with per-property legacy fallback, preserving defaults and validation.
- Code map: PartySdkQueryHandlerTests uses a private TestCursorCodec; replace it with the real AddEventStoreQueryCursorCodec registration and ephemeral data protection. Its invalid-payload theory already has five distinct cases and no-read assertions. EventStoreGatewayRoutingTests currently snapshots six identical factory placeholders; pin concrete factory return identities without constructing or starting services.
- Code map: live Detail ProjectAsync reads and post-initialization reloads are sequential; reuse the existing Task.WhenAll rebuild pattern while retaining recovery-before-read ordering, coordinated slots, ETags and cancellation. PartySdkReadModelAddresses owns segment validation; reuse it directly for index requests rather than building a detail key. Centralize store-name validation on existing read-model options. Erasure timestamp normalization is already a separate branch and needs regression evidence only.
- Code map: move Projections/Actors/PartyEventTypeResolver.cs and resolver tests to Serialization, update imports, extract its nested result into one file per type. Preserve the contracts-only allowlist and ambiguity behavior. ArchitecturalFitnessTests contains a parser source path requiring canonical relocation.
- Code map: tests/e2e/specs/story-7-4-projection-platform-compatibility.spec.ts already targets the SDK; execute and correct stale expectations, retaining retirement and parity assertions. This is a repository contract spec without browser fixtures; explicit PLAYWRIGHT_SKIP_WEBSERVER is suitable.
- Acceptance: Given legacy configuration only, options retain its values; given both sections, canonical values override only configured properties, including invalid values that must fail validation. Given old and canonical discriminator names, public constant values remain equal. Given pending paired live reads, both start before either completes; read faults/cancellation cannot write projections. Existing handler tests continue to assert persisted contents, erasure and retry semantics. Given changed hosted-service factories, identity checks detect substitution even at unchanged counts. Real protected cursors continue pages and reject scope mismatch/tampering before store reads. Resolver tests retain safe dispatch, and the compatibility spec passes on current SDK source.
- Verification: focused Debug builds and direct xUnit assemblies; full projection assembly, selected query/gateway/DI/architecture classes, Playwright repository-contract spec, TypeScript and warning/submodule guard. Record exact commands/results under tests/five-projection-migration-deferred-items-2026-10-21/. No full runtime qualification is claimed: Aspire started and describe showed security Unhealthy with dependent services Waiting, then stopped successfully.

- Production-codec adoption exposed a real Unicode continuation failure: a 512-unit supplementary-character query expands in the protected JSON cursor past QueryPolicyLimits.MaxCursorLength. Bound the complete canonical search scope with SHA-256 before using the existing SDK codec, and fall back to the original raw scope only on wrong-scope rejection. Preserve existing tokens and tenant/user/filter binding; cover legacy acceptance, tampering and caller/filter changes. No codec or persistence abstraction is reimplemented.
- The six SDK factories return IHostedService and some construct workers conditionally, so implementation-type inspection cannot name them safely. Snapshot distinct factory declaring-type/method identities without constructing services; implementation registrations retain their existing names. The regression requires deliberate review when any factory identity changes.

- Verification passed: 906 selected main tests and the full 440-case projection assembly, zero skips; five Playwright repository-contract checks; TypeScript; warning/submodule guard; CRLF-aware diff check. Both affected Debug project builds have zero warnings/errors. Broader Debug solution build fails in unchanged references/Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Queries/TenantReadAuthorityResult.cs(12,5): CS0246 NativeSourceProof is missing. Preserve this broad-gate limit separately from the passing Parties evidence.
- Exact commands, runner summaries, focused and broad build output, initial corrections and pinned commitlint success are preserved in tests/five-projection-migration-deferred-items-2026-10-21/validation.json and sibling evidence files. No dependency or gitlink was changed. Supported source launcher exits 2/HXR011 because HEXALITH_DAPR_HOME is unset; no source runtime qualification is claimed.

- Independent Blind Hunter review returned ten findings; all were verified and patched. The final cursor behavior supersedes the initial all-search digest approach: emit the original protected raw scope whenever it fits the existing SDK bound; compact only oversized tokens and accept either scope through the same SDK codec. Bounded tokens remain readable by the pre-change service during rolling upgrade. Independent DI providers/handlers share an ephemeral key source in continuation tests; this is codec interoperability evidence, not a production key-ring qualification.
- Configuration now layers the two sections before one bind, so valid canonical values can override malformed legacy numbers without legacy conversion errors. Hosted-service snapshots now include factory IL plus resolved member/type/string/signature metadata fingerprints, detecting body replacement even when a compiler-generated method name stays unchanged. No factory is constructed or started.

## Review Triage Log

- Medium, patch: two sequential configuration binds convert overridden malformed legacy values. Layer sections first and bind once; new invalid-legacy/valid-canonical numeric cases prove precedence before conversion.
- Medium, patch: a factory body can change without changing its generated method name. Pin factory IL and resolved metadata in addition to the call-site identity; actual six factory body fingerprints are reviewed and exercised by all isolated gateway tests.
- Medium, patch: hashing every search scope prevents an older service instance from reading a new token during rolling upgrade. Preserve raw output whenever bounded and compact only the previously broken oversized case; tests decode bounded emitted tokens with the exact legacy raw scope through the SDK codec.
- Medium, patch: duplicate-only completion did not prove advancing coordinated persistence. Add both read-completion orders; hold one pending, verify both slots absent, then assert persisted detail and processing sequence 2 and domain end-state.
- Medium, patch: pending-read errors covered only Detail and synthetic cancellation. Cover Processing failure too, cancel the real request token, and inspect both persisted slots at initial and reload boundaries.
- Medium, patch: the new scope fallback lacked legacy-token rejection cases. Test tampering and every caller/filter change against both raw and compact tokens, with no store access after rejection.
- Medium, patch: changing several filters together could mask a lost binding. Mutate query, type, active, mode and page size individually, as well as tenant and user.
- Low, patch: single-handler ephemeral tests omitted independent composition. Mint and continue through independently built service providers/codecs and separate query handlers with the same purpose and shared ephemeral keys.
- Medium, patch: alias/target equality alone cannot pin wire compatibility. Assert the historical literal for every public discriminator and preserve obsolete alias metadata.
- Low, patch: resolver documentation still claimed Actors ownership. Correct the relocated serialization-registry description.

- Follow-up review corrected an intermediate fingerprint issue: raw IL and assembly-qualified names drift between source/package Debug and Release. Final factory identities use sorted concrete IHostedService Type.FullNames referenced by metadata, including constructor, field, return and generic argument types. They exclude compiler-generated method names, IL offsets/token values and assembly versions, and never construct services. Unsupported factories with no concrete type evidence fail closed. This supersedes the earlier call-site/fingerprint notes.
- Final independent re-review: all ten original findings addressed; no unresolved introduced defects. Final verification is 921 selected main tests + 444 full projection tests = 1,365 .NET tests, zero skips; five Playwright repository-contract cases, TypeScript and repository guards pass. The unchanged Tenants broad-build and unavailable runtime/source qualification limits remain explicit in validation.json. DW-32, DW-34, DW-51, DW-57 and DW-61 are reconciled as done.
- Follow-up triage, medium/patch: compiler-generated factory names and raw IL fingerprints differ across supported compilation modes. Compare concrete referenced hosted-service names instead; reviewer inspected the final helper and accepted the correction.
