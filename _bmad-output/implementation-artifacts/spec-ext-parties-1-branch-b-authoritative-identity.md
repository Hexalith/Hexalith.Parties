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

## Planning classification (2026-10-09)

EXT-PARTIES-1 Branch B is a spec-only external-consumer extension for Agents,
outside the Parties UI PRD and Parties sprint epic/story tracking. Its `feature`
type and `in-progress` status describe owner execution, not Epic 7 or 8 scope,
MVP coverage, or a completed consumer dependency. The
[course correction](../planning-artifacts/sprint-change-proposal-2026-10-09.md)
and [readiness addendum](../planning-artifacts/implementation-readiness-scope-addendum-2026-10-09.md)
record this classification. The acceptance criteria and live qualification
gates in this spec remain authoritative.

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

2026-10-08: Inspected current Platform host/source and operational recovery evidence, required the Platform custody Local lane, and recorded a complete normal Local pass plus refreshed external-source receipts. Both production tasks remain open.

2026-10-09: Recorded the spec-only external-consumer planning classification without changing frozen intent, acceptance criteria, original baseline, open production tasks, or `in-progress` status.

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

### Command dispatch admission verification — 2026-10-08

- [Retained verification packet](tests/branch-b-resume-2026-10-08/command-admission/README.md) preserves compact raw receipts. Its manifest maps retained files to their original `/tmp` execution locations.
- Identity commands now reverify the exact original admission after protected-state unwrapping and again after replay/custody, then observe caller cancellation immediately before dispatch. Binding writes capture their original policy and use `IOptionsMonitor<PartyIdentityOptions>` to reject policy withdrawal or change during provider waits. Generic commands, immutable retry results and existing Consumer behavior are preserved.
- Actual signed-proof expiry/trust withdrawal, changed actor evidence and noncooperative-provider cancellation reproduced **27 failures in 37 executed pre-fix cases**. Final coverage is **50/50** admission cases, including actual configuration reload, final-authority-callback cancellation and unchanged-admission controls. The actual configuration-reload and final-verification cases were added after that 37-case red run. Both changed C# files retain UTF-8/CRLF and repository-scoped whitespace checks pass.
- The normal complete Local command exited **1** after its first six passing owner lanes: external `PlatformApiPrerequisitesTests.cs:681` produces two CS1503 errors. Subsequent source/dependency movement also produces NU1109 for EventStore's `Swashbuckle.AspNetCore.SwaggerUI` 10.3.0 versus Parties' central 10.2.3. Those files/dependencies were preserved; neither blocker was suppressed or overridden.
- Nine normally built owner lanes executed **623 passing cases**. A separately disclosed supplemental domain build reused previously restored assets with `--no-restore` and a temporary import excluding only the unrelated `PlatformApiPrerequisitesTests.cs` and its dependent `EpicEightClosureFitnessTests.cs`; its required filters executed **357/357**. Together these are **980 selected passing cases across ten owner XML files**, not a successful complete Local gate. [Verification manifest](tests/branch-b-resume-2026-10-08/command-admission/final-verification-manifest.json) retains exact commands, XML digests, required-class counts, failed gates and fallback scope. [Independent root audit](tests/branch-b-resume-2026-10-08/command-admission/root-verification.json) confirms all 38 required classes, all four frozen matrix rows locally covered and exact compiled/current source hashes for the processor and regression tests. External source movement is disclosed; no unchanged-whole-worktree claim is made.
- The isolated owned Parties AppHost started and `aspire wait parties --status healthy --timeout 30` succeeded; the instance was stopped through its explicit AppHost path. No identity endpoint or production mutation was exercised. Live missing-input and non-authoritative sentinel capability gates both exited **1** without contacting an endpoint.
- Production independent custody, qualified all-copy destruction/nonrollback restore, approved successor continuation and complete installed P-01–P-10 targets/probes remain unavailable. Both original open execution tasks, frozen intent, original baseline and `in-progress` status remain unchanged; installed identity availability is not established.

### Platform custody verifier continuation — 2026-10-08

- [Retained verification packet](tests/branch-b-resume-2026-10-08/platform-custody/README.md) records current Platform source/host/configuration and operational-artifact inspection. Sibling Platform `f043a2f242762233091abdaa5bbe1ab777bd0f12` and root reference `332a7d8104e3f6c9aaa57cbc7f07afb5fa0859de` have no production `IIdentityHistoryCustody` implementation or registration; only a synthetic fixture implements it. Existing OpenBao and October 1 generic isolated restore evidence were found, but do not provide actor-history all-copy destruction, nonrollback restore or successor continuation qualification.
- The Local verifier now requires `Hexalith.Platform.Custody.Tests` with prerequisite and cleanup filters. The normal complete Local command exited **0**: **1,095/1,095**, eleven lanes, all 40 required classes, zero build warnings/errors/failures/skips/not-run. The custody lane passed **115** cases (68 prerequisite and 47 cleanup). [Root independent audit](tests/branch-b-resume-2026-10-08/platform-custody/root-local-audit.json) confirms all required classes and all four frozen matrix rows locally covered.
- Six external EventStore test documents changed during the run. Three affected lanes were normally rebuilt and reran **23/107/168**; original receipts and source mismatch discovery remain retained. Final compiled-source checks match all 40 required test-class files and eight relevant production custody/domain/query files. Refreshed counts replace originals; no double counting or unchanged-whole-worktree claim is made. Exact commands, hashes and external HEAD/UI/docs/test movement are retained.
- The explicit isolated Parties AppHost started, Parties reached Healthy, resources were inspected and the owned instance stopped. Complete Live missing-input, non-authoritative sentinel capability and LiveReadiness missing-input gates exited **1** before endpoint access. No identity endpoint, production mutation, dependency update, staging, commit or push was performed by this continuation.
- Both original open execution tasks, frozen intent, original baseline and `in-progress` status remain unchanged. Production independent custody/all-copy receipts/nonrollback restore, approved successor continuation and complete installed P-01–P-10 targets/probes remain unavailable. The approved 365-day policy and existing Consumer behavior are preserved; local verification does not establish installed identity availability.

### Current-source continuation — 2026-10-08

- [Retained verification packet](tests/branch-b-resume-2026-10-08/current-source/README.md) records the exact current complete Local command, which exited **1** at EventStore Client with two CS1503 errors in `EventLocalImplementationBindingTests.cs:24` and `:26`. The preceding Contracts lane passed 23 cases; the remaining nine normal owner lanes were built and executed individually without exclusions or restore overrides.
- **988 selected cases passed across ten projects**, with zero warnings/errors in those builds and zero failed/skipped/not-run executions. The independent root audit checks all ten XMLs, 36 passing required classes and all four frozen matrix rows. Four required EventStore Client class filters remain unexecuted; this is not a successful complete Local gate.
- The selected source version is 3.117.1. One Parties fitness document and ten EventStore source/test documents moved externally during execution. Eight selected source files match run-start bytes; no unchanged-worktree or Portable-PDB claim is made. The existing verifier edit, root dependency changes and unrelated owner work were preserved.
- Live missing-input, non-authoritative sentinel capability and LiveReadiness missing-input gates all exited **1** before endpoint access. Current register bytes confirm the user as Parties Maintainer and integration date 2026-10-08; immutable target/complete command remain TBD and AcceptedStatus remains Uncommitted.
- Platform still registers cleanup without a production `IIdentityHistoryCustody` backend. Qualified independent custody/all-copy destruction/nonrollback restore, approved actor-free successor continuation and complete installed P-01–P-10 persisted probes/targets remain unavailable. The approved 365-day policy, both open execution tasks, frozen intent, original baseline and `in-progress` status stay unchanged. No production mutation, dependency update or Git write was performed by this continuation.

### Policy/lifecycle verification continuation — 2026-10-08

- [Retained packet](tests/branch-b-resume-2026-10-08/policy-lifecycle/README.md) records the newly required Platform policy/lifecycle filter, three passing expired-suffix ordering cases, normal owner receipts and a narrow Platform test callback-return repair. External expired-prefix continuation source/tests were preserved; no continuation bug-fix or production qualification is claimed.
- The [canonical selected manifest](tests/branch-b-resume-2026-10-08/policy-lifecycle/verification-manifest.json) records **1,205 required passes plus 10 supplemental passes**, eleven owner XMLs and all 41 required classes. The repaired custody build passed **181/181**: 68 prerequisite, 47 cleanup, 56 policy and 10 supplemental revocation-subscriber cases. [Independent root audit](tests/branch-b-resume-2026-10-08/policy-lifecycle/root-independent-audit.json) checks XML/artifact digests, class/source hashes, five selected production files, all four frozen matrix rows and three suffix cases. Its timestamped byte recheck found no additional selected-file change; policy-aware continuation was already captured. No Portable-PDB document, whole-worktree, final-current compiled-source or installed claim is made.
- Original complete Local **1,195/1,195** remains separate. Fresh Local exited **1** on external test CS0121 errors, repaired narrowly without changing lost-ack assertions. The latest complete Local also exited **1** on external `Fr34ProtectionGate.cs:37` CS0117 (`SchemaVersion` unavailable). That source and a new FR-34 test moved afterward and were preserved; remaining Parties lanes were normally built without exclusions or restore overrides. Selected results do not establish a complete current Local pass.
- The normal pinned-package Debug host build exited **1**, with three CS0246/CS1061 errors at `RetainedHumanActorHistoryFold.cs:29/:30/:31`: EventStore **3.117.1** lacks `ExpiredIdentityHistoryCertificate`/`ExpiredEvents` used by new external continuation source. No dependency update or compatibility bypass was performed.
- [Platform inspection](tests/branch-b-resume-2026-10-08/policy-lifecycle/platform-inspection.json) found the approved 365-day policy and scoped cleanup, with no production identity-history backend, expired-certificate custody implementation or complete installed P-01–P-10 targets in the inspected checkouts. Qualified independent custody/all-copy destruction/nonrollback restore and successor continuation remain unavailable. Both original production tasks, frozen intent, original baseline and `in-progress` status remain unchanged.
- Live missing-input, non-authoritative sentinel capability and LiveReadiness missing-input gates exited **1** before endpoint access. Isolated Parties reached Healthy and stopped; the default isolated Platform host had no opt-in resources and stopped. This continuation performed no Git write, dependency update, deployment or production identity mutation; external source/Git movement is disclosed.
