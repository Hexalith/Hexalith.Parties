# EXT-PARTIES-1 owner prerequisite implementation — 2026-10-06

This is an uncommitted owner implementation candidate for Story 5.4. It does not accept the dependency, an immutable launch target, an integration date or a compatibility command. The authoritative Agents register remains `Uncommitted` with the three fields `TBD`. No unavailable consuming seam or authenticated live endpoint was called.

Observed base HEADs are Parties `b3794a4dcbe2fff3e9ea5c420a3c4695e2d1a8ca`, EventStore `785d58fc99ce4c2751c0546ca6368e993c654ed9`, and Platform `711a70fd94794ef1aea1b91f5524136b0d660468`. The tested sibling source trees contain owner changes beyond those commits. These observations are not accepted targets. Existing identity work at Parties commit `37d87f5a2869b076c651a58714d60f64647848bc` was reused.

## Implemented behavior

`PartyIdentityQueryService.ResolveAtAsync` now reads only the independent `IRetainedIdentityHistoryReader` attribution source. A destroyed profile key or unavailable current profile cannot force a historical read through the full profile or substitute the current actor. The exact tenant/Party/purpose certificate must cover a disjoint complete partition from original position 1 through the sampled head; sparse positions are never renumbered. Current observation time, `AuthorityRevision` and exclusive `ValidUntil` are required. The service rechecks query authority and certificate/custody expiry after awaited work.

`RetainedHumanActorHistoryFold.Fold` accepts exactly the three established/rebound/revoked full contract names. It preserves finite non-overlapping intervals, original opening positions, actor/version/provenance and purpose custody evidence. Unknown, profile, simple-name, assembly-qualified and namespace-alias events fail closed. `HumanActorBindingResult.BindingSourcePosition` is additive and keeps the existing constructor/deconstruction signature; the narrow HTTP client requires it to be inside the source checkpoint on successful history replies.

Current identity keeps the Branch B Organization classification for the provisioned Agent Party. Inactive or restricted humans return no usable human binding, and current reads recheck authority and finite binding/custody expiry before success. The pre-existing classification enum member remains part of the wire API, but this Branch B client does not accept it as an eligible classification.

The owner host must provide `IRetainedIdentityHistoryReader`, trusted `IPartyIdentityAuthority`, and an actual `IIdentityHistoryCustody`. Missing history/custody remains unavailable. EventStore's new source reader and contracts are sibling-owner changes, not Parties implementations of a custody backend.

## Executed source verification

All three final focused Debug builds used explicit sibling project references, `--artifacts-path /tmp/hexalith-agents54-parties-artifacts`, `-m:1`, and succeeded with 0 warnings and 0 errors. Each test command followed a successful build under `set -e`.

| Final lane | Executed | Passed | Failed / skipped |
| --- | ---: | ---: | ---: |
| Parties domain/admission | 38 | 38 | 0 / 0 |
| Parties client/DI | 18 | 18 | 0 / 0 |
| Parties identity contracts/API snapshot | 3 | 3 | 0 / 0 |
| Total | 59 | 59 | 0 / 0 |

The final fixture coverage includes erased-profile/current-history separation, original positions, serialized retained-source reconstruction in a fresh service, exact interval boundaries, gaps/overlap, actor/version mismatch, malformed source/purpose and all alias substitutions, future observation, missing source authority, certificate expiry in transit, custody expiry crossing an await, read-authority revocation/substitution, missing reader and cancellation. The serialized representation and fresh domain service are local fixtures; they are not Dapr persistence, process restart, backup restore, destruction or live erasure qualification.

The exact project commands, XML/log paths, checksums and changed-source hashes are in [the separate source evidence JSON](tests/ext-parties-1-owner-source-evidence-2026-10-06.json). Main final logs have prefixes `/tmp/parties-story54-retained-domain-final3`, `/tmp/parties-story54-retained-client-final`, and `/tmp/parties-story54-retained-contracts-final2` followed by `-build-20261006.log` or `-tests-20261006.log`/`.xml`.

## Executable verifier evidence and blocker

The Local verifier now uses isolated artifacts, requires a successful fresh build before tests, validates every requested class was executed, and permits explicit optional `-MemoriesRoot`. Its default uses the packaged optional Memories dependency rather than initializing missing nested submodules. Both PowerShell scripts parse without errors.

Executed from the Parties root:

```sh
pwsh -NoProfile -File eng/verify-ext-parties-1.ps1 -Mode Local -ArtifactsDirectory /tmp/parties-story54-local-verifier-20261006/artifacts -EvidenceDirectory /tmp/parties-story54-local-verifier-20261006
```

This command exited 1. Six lanes passed: EventStore contracts 23, client 98, server 137; Platform identity 2; Parties contracts 29 and server 94. Parties domain then executed 107 with 106 passing and one failure. The failing unchanged test is `PartyDomainProcessorValidationTests.ProcessAsync_ProtectedHistoricalPayloadWithDestroyedKey_RedactsAndContinuesRehydration` at `tests/Hexalith.Parties.Tests/Domain/PartyDomainProcessorValidationTests.cs:286`. The sibling strict rehydrator rejects its `json-redacted` serialization format at `DomainProcessorStateRehydrator.RequireSupportedReplayMetadata` (`../eventstore/src/Hexalith.EventStore.Client/Handlers/DomainProcessorStateRehydrator.cs:436`). Existing expected behavior and shared replay enforcement were preserved. This is a separate source compatibility blocker. The fail-fast verifier did not execute its remaining security/client/UI lanes. This Local run preceded the final two alias-negative fixtures; those passed in the final focused domain run above.

`LiveReadiness` performs only owner-configured read probes after the real dependency register requires `Available`, the exact accepted target/date/command, a matching passing compatibility receipt and explicit policy/prerequisite proof. The child process exit is propagated. Its checks use the shared 32 MiB wire / 16 MiB decoded payload / 10,000-position bounds and reject expiry crossed during probes. It does not bypass dependency acceptance or provide full P-01–P-10 qualification. The full `Live` mode still reports its complete missing qualification gate and exits 1.

Executed boundary smoke commands:

```sh
pwsh -NoProfile -File /tmp/parties-story54-verifier-parser-20261006.ps1
python3 /tmp/parties-story54-readiness-gates-smoke-20261006.py
```

The smoke starts a local counting HTTP listener. Missing inputs and complete sentinel inputs against the real `Uncommitted` register both returned wrapper exit 1 with zero requests. The sentinel policy and receipt names exist only as rejected boundary fixtures and do not configure production. A separate `-Mode Live` missing-input run returned exit 1. Gate logs and reproducible smoke source are checksummed in the evidence JSON. `git -c core.whitespace=cr-at-eol diff --check -- eng src tests` passed; all eleven changed implementation/test/verifier files preserve CRLF.

## Required owner/Product inputs and real remaining gaps

No production retention duration or erasure decision was selected. `Parties:Identity:PolicyId`, a positive finite `Parties:Identity:Retention`, and `Parties:Identity:ExpiryTrigger` remain mandatory. The current contract supports only `binding-effective-at`; any different Product-selected trigger needs an explicit contract change. Product and owners must state the retention and the relationship between immediate profile erasure and purpose-limited attribution, including deletion timing for originals, derived copies and backup/restore material.

A real provider must independently protect retained events/snapshot history, authorize the current lifecycle on every read, enforce irreversible expiry/destruction with non-rollback evidence across restart/restore, and cover derived copies and cleanup. HMAC signing or a self-reported evidence flag cannot establish those guarantees. Production admission/custody/retention and the authenticated persisted-state P-01–P-10, restart/restore, failure-injection and cross-tenant installed matrix remain unqualified. The strict profile-replay `json-redacted` compatibility failure above also remains open.

The six pre-existing owner planning/test-summary edits were left untouched. No commits, pushes, submodule updates, owner acceptance changes or external messages were made.

## Cancellation review fix

Current and historical identity reads now reject pre-cancelled calls, cancel outstanding reads even when providers ignore the token, and check again before returning identity evidence. The normal Debug source build passed with zero warnings/errors. The focused domain/admission suite passed 52/52, including 14 new cancellation regressions. Reusing unchanged client 18/18 and contract 3/3 evidence yields 73 focused passing tests. Exact commands, hashes and XML are in the source-evidence JSON. This does not replace the historical broad Local result of 489/490; its unchanged json-redacted strict replay compatibility failure remains unresolved. Retention policy, production custody, expiry cleanup/restore and complete P-01–P-10/live qualification remain unavailable.

Final review also verified that custody cancellation returning false stops before another authority lookup; both current/historical true/false cases pass.
