---
title: 'EXT-PARTIES-1 Branch B authoritative identity'
type: 'feature'
created: '2026-10-03'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '5388884eec84b16545fdc008b2fc04547b0ed5b6'
context:
  - '_bmad-output/implementation-artifacts/ext-parties-1-implementation-notes.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Agents cannot prove its immutable Party or a human approver's current and historical identity. Generic creation retries ignore divergent intent, client scope is ambient, and identity reads lack authoritative evidence.

**Approach:** Deliver the complete Parties-owned Branch B provisioning, identity and actor-attribution contract through EventStore, with explicit client scope, pure durable history, fail-closed authorization and an executable compatibility verifier.

**Approved decisions (2026-10-03):** Platform owns a small durable actor registry using existing authentication/hosting. Only its trusted identity service writes new actor bindings, with verified operator provenance. Parties owns minimal finite attribution history. Policy ID, duration and expiry trigger are required configuration with no production default; missing policy or enforceable custody blocks binding writes and live qualification. Profile erasure blocks current eligibility immediately; retained evidence uses an independently approved protection/expiry lifecycle.

## Boundaries & Constraints

**Always:** Preserve Organization Party IDs and Consumer onboarding. Keep issuer/subject mappings outside Party streams; store only opaque actor attribution/version/interval/provenance. Verify authenticated tenant, operation and exact target before protected reads or mutation. Use EventStore SDK persistence and shared verified-source reads; reject unsupported authority/currentness. Preserve exact provisioning identity and first result on retry; inactive/restricted/erasing/erased Parties cannot become eligible. Person is the positive Human classification; Organization flags do not confer it.

**Never:** Create replacement identities, infer legacy ownership, trust JWT roles/unsigned extensions/projection age, use PartyDetail as an identity result, put tokens/raw subjects/names/emails in Parties identity history, add direct Parties APIs/actors/state stores, change unrelated GDPR behavior, or promote availability from fixtures. Opaque references remain protected data; existing consumer/launch gates apply.

## I/O & Edge-Case Matrix

| Scenario | Input/state | Result | Failure |
| --- | --- | --- | --- |
| Provision | Verified service; exact tenant/Agent/Party intent | One immutable creation/marker; retry preserves result | Changed intent/occupied unmarked ID conflicts |
| Current identity | Exact scope; complete current source and actor evidence | Safe classification/liveness/binding basis | Missing/gapped/stale/ambiguous/unavailable blocks |
| History | Binding X before T, Y from T | Before T resolves X; at T resolves Y after replay | Gaps/overlaps/version or actor mismatch block |
| Isolation | Wrong service/tenant/target; concurrent scoped calls | Deny before protected lookup/effect | No foreign identity/existence/PII disclosure |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Parties/Domain/PartyAggregate.cs` and `src/Hexalith.Parties.Contracts/State/PartyState.cs` — reuse pure folds; non-null rejection state is not creation; Apply's wall clock is not history.
- `src/Hexalith.Parties/Domain/PartyDomainProcessor.cs` — add verified identity admission before unprotection; preserve gateway RBAC ownership and erasure processing.
- `src/Hexalith.Parties/Queries/PartySdkQueryService.cs` — preserve ordinary stale-cache reads; separate authority queries from timestamp-based freshness.
- `src/Hexalith.Parties.Client/HttpPartiesCommandClient.cs` — preserve existing clients; add a narrow identity client with explicit immutable per-call tenant and retry identity.

## Tasks & Acceptance

**Execution:**
- [x] `../eventstore/src/Hexalith.EventStore.Client/Streams/IAuthoritativeEventStreamReader.cs` — add bounded authorized complete-prefix/head reads, server event-scope validation and tests.
- [x] `../eventstore/src/Hexalith.EventStore.Server/Identity/` and `Actors/` — implement a private durable actor registry, verified capabilities and exact-command admission proofs using shared gateway hooks.
- [x] `../platform/src/Hexalith.Platform.Identity/` and `Hexalith.Platform.EventStoreHost/` — configure trusted enrollment/alias administration and compose the existing eventstore deployment; preserve existing authentication.
- [x] `../eventstore/src/Hexalith.EventStore.Contracts/Security/` — define the enforceable purpose/history lifecycle seam; missing custody/restore guarantees deny authority. Never equate projection TTL with source expiry.
- [x] `src/Hexalith.Parties.Contracts/Commands/ProvisionAgentParty.cs`, `Events/AgentPartyProvisioned.cs`, `Models/PartyIdentityEvidence.cs` — add safe provision/current/history contracts and pure history; full file map in companion.
- [x] `src/Hexalith.Parties.Contracts/State/PartyState.cs`, `src/Hexalith.Parties/Domain/PartyAggregate.cs` — enforce exact retry, conflicts, rejection-only recovery, immutable intervals and generic/composite bypass denial.
- [ ] `src/Hexalith.Parties/Domain/PartyDomainProcessor.cs`, `Authorization/`, `Queries/` — verify source/target before unprotection, enforce retention capability and authoritative current/history reads.
- [x] `src/Hexalith.Parties.Client/Abstractions/IPartiesIdentityClient.cs`, `HttpPartiesIdentityClient.cs` — implement scoped gateway calls with stable logical retry IDs and returned-evidence validation.
- [x] `_bmad-output/planning-artifacts/adr-consumer-party-id-binding.md`, `tests/Hexalith.Parties.UI.Tests/IdentityBindingBoundaryTests.cs` — document opaque attribution extension preserving Consumer behavior.
- [ ] `eng/verify-ext-parties-1.ps1` and companion test files — cover P-01–P-10; missing policy/custody/targets or skipped lanes fail live qualification.

**Acceptance Criteria:**
- Given lost acknowledgement/concurrent retries, when provisioning recovers, then one original Party/result survives and no alternate generic create can replace it.
- Given revoked, non-human or uncertain evidence, when current identity is evaluated, then eligibility blocks before effects while ordinary Consumer/UI behavior stays intact.
- Given binding transitions and restart/restore, when action-time identity is resolved, then the unique immutable actor/version/interval is reproduced without current-binding substitution.
- Given tenant collisions and forged admission, when every identity surface runs, then foreign persisted state and public existence/count/error/log outputs remain unchanged.
- Given complete installed owner targets, when the live verifier runs, then P-01–P-10 and persisted end states pass; mocks or partial runs never establish record availability.

## Implementation Notes

Implemented the shared complete-prefix/head reader, private global actor registry, purpose-separated asymmetric admission, Platform enrollment/bootstrap/operator verification, deterministic Agent provisioning, pure finite Human attribution, strict identity queries, explicit scoped client and Consumer compatibility extension. Checked execution items represent implemented source with passing local owning tests, not installed availability.

The processor/query execution item remains open for production retained-history and lifecycle qualification. The shared SDK now has a purpose-scoped retained-history source/checkpoint API, and Parties folds its authenticated sparse attribution partition independently of erased profile payloads. Independent event/snapshot custody remains mandatory; missing custody denies binding writes and authoritative history reads. An expired/destroyed predecessor cannot be relabeled as a profile exclusion or replaced with today's binding. Approved actor-free continuation, production custody and nonrollback restore guarantees remain unavailable. The explicitly approved host policy is `party-actor-retention-v1`, 365 fixed days from `binding-effective-at`; library retention defaults remain unset.

The verifier execution item remains open. Local owning suites and each required class filter have recorded xUnit XML evidence; the script checks every filter and rejects skipped/not-run lanes, including required retention/timeout configuration coverage. LiveReadiness implements authenticated retained-source/query read probes behind the accepted dependency gate. Live mode remains an executable fail-closed gate: complete P-01–P-10 persisted-state/restart/restore/failure-injection interfaces and targets are unavailable. No live availability is established.

## Spec Change Log

2026-10-03: Recorded implemented local execution items and explicit incomplete owner capabilities. Intent, frozen boundaries, acceptance criteria and original baseline remain unchanged; status remains `in-progress`.

2026-10-07: Resumed source verification, fixed provider cancellation callbacks blocking query deadline/caller completion, and required the host retention/timeout configuration lane. Recorded the existing retained-history seam and approved 365-day policy, plus independent current-source verification. Both remaining execution tasks, frozen intent, original baseline and `in-progress` status remain unchanged.

## Review Triage Log

2026-10-03 parent implementation audit: read the baseline source diffs including untracked files, then reviewed every change since the initial source snapshot. Corrected findings cover public-key-only admission, global alias continuity, independent history/snapshot protection, returned/source scope validation, future-action denial, finite intervals and invalid revocation replay. No frozen intent, matrix or acceptance expectation was changed to satisfy implementation.

The parent independently checked all ten xUnit XML files against the final manifest: **615 passing tests**, matching XML digests, and at least one actual passing test for every required class filter. Every frozen matrix row has executed passing local coverage: provisioning original retry/conflict, exact current source/actor, X-before-T/Y-at-T replay, and concurrent tenant-scoped client isolation. Additional stream-ingress tests deny all three foreign raw-envelope scopes before payload unprotection. Local coverage does not satisfy persisted live acceptance.

Eight execution tasks are implemented and locally verified. The post-erasure history-source and live persisted-probe tasks remain unchecked. Production policy/trust/custody, real restart/restore and installed P-01–P-10 acceptance remain unresolved; the build stays `in-progress` and the next workflow review/completion stage has not been reached.

## Design Notes

One goal spans domain and shared infrastructure. No production mutation is proposed. Legacy unmarked IDs fail closed. Production custody/retention and other launch dependencies remain gates; local tests do not deliver them.

## Verification

- Executed individual Debug/source owner builds and filtered xUnit suites; retained logs/XML and per-class manifest are in `/tmp/ext-parties-1-verification`. The companion records commands, counts and acceptance mapping. The full final Local script was not rerun over already-passing unchanged owner lanes; all configured lanes were executed individually and audited from their XML.
- Executed `pwsh -NoProfile -File eng/verify-ext-parties-1.ps1 -Mode Live` twice in isolated subprocess environments: required inputs absent, and sentinel strings supplied solely to test the capability gate. Both exit 1; no installed targets or persisted end states were exercised.
- Checked verifier PowerShell syntax and repository-scoped whitespace with `git -c core.whitespace=cr-at-eol diff --check`; source files use UTF-8/CRLF. Production custody/history source and live acceptance remain incomplete.

### Resumed verification — 2026-10-07

- Latest complete Local command passed **919/919** across ten required owner lanes, with zero warnings/errors/failures/skips/not-run cases: `pwsh -NoProfile -File eng/verify-ext-parties-1.ps1 -Mode Local -EventStoreRoot /home/administrator/projects/hexalith/eventstore -PlatformRoot /home/administrator/projects/hexalith/platform -EvidenceDirectory /tmp/ext-parties-1-cancellation-20261007/full-local-current -ArtifactsDirectory /tmp/ext-parties-1-cancellation-20261007/artifacts-current -MemoriesRoot /tmp/ext-parties-1-cancellation-20261007/optional-memories-package-mode`. [Retained evidence](tests/branch-b-resume-2026-10-07/cancellation/current-source/README.md) records exact commands, XML, source/artifact hashes and scope limits.
- Twelve pre-fix cases failed deterministically for blocked provider cancellation across current/history, source/custody and timer/elapsed/caller cancellation. Final private query cancellation, shared asynchronous provider cancellation, fault observation and deferred cleanup pass those cases. Concurrent query-entry/remaining-budget and additional deadline tests were preserved; their authorship is separate from this callback fix.
- [Root independent audit](tests/branch-b-resume-2026-10-07/root-audit/root-verification.json) checked every required class, all four frozen matrix rows and all twelve callback regressions. Its portable-PDB audit matched **3,391 unique workspace C# source documents across 337 PDB copies**, with zero mismatches and zero skipped documents. Configuration and production qualification are separate from compiled-source evidence.
- The earlier **905-pass** attempt is retained separately. Its subsequent source drift was detected by the independent PDB audit. The fresh run captured source inventories before/after; an external query-test change during that run is disclosed, and the final independent compiled-source audit binds its executed tests to current bytes. No unchanged-whole-worktree assertion is made.
- Parties source HEAD was observed as `f4c5e83dfb884ef086bbdbad479955564ea16d9c`, EventStore as `40c92e085d8a6463d469c1b340c410fec84a690f`, and Platform as `5f945450d4140fd190594a30b60a603521ec4e30`; these observations do not replace the original spec baseline or establish an accepted delivery target. Unrelated CI/docs/replay changes and earlier query-deadline evidence were preserved.
- The isolated Parties Aspire baseline started and its resource state was inspected; resources were waiting behind security. The owned AppHost was stopped through its explicit path. A healthy Parties runtime or installed identity availability is not claimed.
- Live exited **1** for missing installed targets/credentials/custody inputs before contacting an endpoint. [Root live-input discovery](tests/branch-b-resume-2026-10-07/root-live-input-discovery.json) found no approved installed production custody provider, qualified restore/failure-injection target, actor-free continuation contract or exact accepted complete verification target/date/command. The approved 365-day policy is preserved. Complete live acceptance and both remaining tasks stay open.

### Current-source observation verification — 2026-10-07

- Current identity rejects missing observation metadata, unsupported serialization/protection evidence, and establishment/rebind/revocation times after the authoritative source observation. Readable JSON with decrypted Protected provenance and legacy absent metadata remains compatible. [Retained evidence](tests/branch-b-resume-2026-10-07/source-observation/README.md) records eleven failing-before cases and eighteen focused passing cases.
- Final local evidence is **933/933** across all ten owner lanes: a complete current-source rerun followed by one identical 168-test EventStore Server refresh after external sample-source drift. Builds have zero warnings/errors, and every required class executed passing tests with no failures/skips/not-run cases. Root independently checked all four frozen matrix rows, all twelve callback regressions and 3,393 unique workspace C# documents across 337 PDB copies, with zero mismatches/skipped documents. Superseded runs and external HEAD/source movement are disclosed; no unchanged-whole-worktree claim is made.
- Hexalith.Platform owns production. Its lifecycle documentation confirms no production IIdentityHistoryCustody/backend registration; qualified all-copy destruction, nonrollback restore, still-live successor continuation and complete installed persisted-state/restart/restore/failure-injection probes remain unavailable. Platform ownership and local evidence do not establish installed availability. Both open execution tasks, frozen intent, original baseline and in-progress status remain unchanged.
