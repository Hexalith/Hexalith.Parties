---
title: 'Complete five runtime isolation and authorization deferrals'
type: 'bugfix'
created: '2026-10-22'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'd2ad697dcf679dd8e174d83e6d2b4f0721174142'
context: ['AGENTS.md', 'docs/development-guide.md', 'tests/README.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Gateway tests lack authenticated projection and persisted-stream proof; concurrent topologies share resources; loaded admin forms can send values after authorization changes.

**Approach:** Complete DW-30, DW-31, DW-160 and two unnumbered ledger entries: concurrent topology isolation from `spec-fix-ci-and-publish-release.md`, and form authorization invalidation from `spec-five-review-runtime-convergence-deferred-items.md`. Independently review, verify, commit and push to main. The requested five-item batch overrides single-goal scope.

## Boundaries & Constraints

**Always:** Preserve production identity/ACL tuples, private fixture state, cancellation, accepted-edit convergence and exact execution receipts. Use existing authenticated tenant seeding and ULID helpers. Test sessions own their resources.

**Never:** Change dependencies, gitlinks, signoffs, public contracts, deployment, privacy mechanics or upstream repositories. Skips and mocks cannot close runtime items. DW-116 and Memories persistence stay open.

## I/O & Edge-Case Matrix

| Input/state | Expected behavior | Failure handling |
|---|---|---|
| Seeded authenticated create | Projected query and stored events match party/command | Bounded nonconvergence fails |
| Wrong caller/verb/path | Dapr denial; permitted control succeeds | Application rejection alone fails proof |
| Two sessions; stop one | Independent identities/state; survivor works | Cross-session calls fail; retain failed-cleanup ownership |
| Changed form context, including A/B/A | Clear old values; prevent stale sends/completions | Current pre-send authority required |
| Revoked role, inactive tenant, failed/delayed lookup | No command | Existing bounded denied/unavailable UI |

</frozen-after-approval>

## Code Map

- `tests/Hexalith.Parties.IntegrationTests/HealthChecks/PartiesAspireTopologyFixture.cs`: complete mTLS profile already seeds the real callback; ordinary health profile remains unseeded. Redis is private.
- `src/Hexalith.Parties.AppHost/DaprMtlsBootstrap.cs`: fixed namespace, control-plane addresses and shared generated directory. `Program.cs` composes fixed sidecar IDs; EventStore helper accepts a configurable HTTP port.
- `src/Hexalith.Parties.AdminPortal/Components/CreateEditPartyPage.razor`: authorization refresh occurs after sending. Reuse `_formVersion`, query-service scope tokens and the portal authentication-notification pattern.

## Tasks & Acceptance

**Execution:**

- [x] Mapped fixture/AppHost files, new `tests/Hexalith.Parties.IntegrationTests/HealthChecks/PartiesTopologySession.cs` and `scripts/aspire-start-mtls.sh`: immutable session configuration supplies unique control-plane names/loopback ports, trust/configuration directories, namespace and suffixed literal app IDs. Remap generated ACL identities, component scope and routing consistently. Preserve logical resource names and production defaults. Replace fixture process-global environment mutation with builder configuration.
- [x] New `tests/Hexalith.Parties.IntegrationTests/Topology/PartiesTopologyIsolationTests.cs`, existing fixture/bootstrap tests and `tests/Hexalith.Parties.Ci.Tests/AspireMtlsBootstrapTests.cs`: prove concurrent sessions, independent stored markers, own-peer routing, cross-session rejection and survivor operation after peer disposal. Exercise cancellation, conflicts and ownership-safe cleanup failures.
- [x] `tests/Hexalith.Parties.IntegrationTests/Gateway/EventStoreGatewayE2ETests.cs`: use seeded tenant/member, authenticated create/query and canonical query constants. Assert projected identity/detail/freshness. Add a fixture reader for exact Redis actor metadata/event keys derived from `AggregateIdentity`, without scans; deserialize Server envelopes and verify sequence, identity, correlation, event type and payload independently of command status.
- [x] `tests/Hexalith.Parties.IntegrationTests/Topology/PartiesMtlsTopologyTests.cs`: run original production identities/ACLs serially; test permitted controls and forbidden caller/verb/path combinations requiring Dapr denial evidence. Authoritative ACL bytes remain unchanged.
- [x] Mapped form: synchronously invalidate generation and cancel work on authentication notification or captured scope cancellation. Clear values and reload even on unchanged route/signature. Fence loads/disposal; snapshot create/update before awaiting fresh pre-send authorization. Require unchanged generation/context/scope and current admin authority before sending. Preserve accepted-result and busy-state ownership.
- [x] `tests/Hexalith.Parties.AdminPortal.Tests/Components/CreateEditPartyPageTests.cs`: assert zero stale API calls and no restored old values for tenant/user/sign-out/A-B-A changes, role revocation, delayed/faulted checks, old loads, scope reset and disposal. Unchanged create/update each sends once.
- [x] `tests/README.md`, `docs/development-guide.md`, batch evidence/spec and `deferred-work.md`: document qualified profiles and exact results; close only these five after acceptance passes.

**Acceptance Criteria:**

- Given seeded runtime, when authorized creation completes, then projection query and independent persisted bytes prove the same party/command.
- Given original ACLs, when prohibited combinations execute, then Dapr denies them while allowed controls pass.
- Given concurrent sessions, when one stops, then isolated routing/state and survivor operation remain correct.
- Given a loaded form, when authority changes or becomes unavailable before sending, then stale commands never leave; unchanged authorized submissions succeed once.

## Implementation Notes

Approved by the user on 2026-10-22 with "approve and stop". Intent is frozen; implementation, verification, commit and push remain pending for a resumed build session.

Planning only; no runtime qualification claimed. Tenants now evaluates EventStore 3.126.1. Verify launched builds before counting runtime proof; historical DW-158 failures do not establish current results.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Route | Evidence |
|---|---|---|---|
| BH-1 | high | patch | QueueReload renders only after awaited reload, leaving previous values visible while new authority is pending; render the cleared/loading state before awaiting. |
| BH-2 | high | patch | Notification callbacks exchange/dispose _workCts off-renderer while reload/submit capture its Token; nullable/disposed access can escape before try. Shares lifecycle-ownership fix with EH-1. |
| BH-3 | medium | patch | Scope Register can invoke QueueReload synchronously before assignment, allowing the superseded reload to overwrite registration/status. Preserve generation ownership across registration. |
| BH-4 | medium | defer | Referenced Admin.UI and Sample.BlazorUI hardcode eventstore-admin/eventstore in AddEventStoreDaprServiceInvocation. Their upstream clients predate this change and expose no target setting; frozen intent explicitly forbids upstream edits. Qualify Parties gateway/Tenants routing and disclose this reference-UI concurrency limitation. |
| BH-5 | medium | patch | The script accepts a changed Sentry port without a session ID but exports its address only for sessions. Reject the unsupported combination before Docker; EH-4 duplicates the same outcome. |
| BH-6 | medium | patch | A custom namespace with original IDs is accepted but generated ACLs retain original namespaces. Reject unsupported namespace-without-suffix in script/bootstrap; EH-3 duplicates this outcome. |
| BH-7 | medium | patch | Session IDs without a certificate-directory override share the default credentials/config path. Derive the default private path from the session ID while preserving the non-session default. |
| BH-8 | low | reject | The process-lifetime reservation set can exhaust after roughly thousands of sessions, but ordinary suite runs create only a small number. Lifetime-release machinery would add state for a non-everyday case; no current test/session exhausts it. |
| BH-9 | medium | patch | Appending an ID suffix after a whole YAML line mishandles valid quoted/commented identities. Preserve scalar quoting/comments or fail unsupported syntax explicitly without touching authoritative bytes. |
| BH-10 | medium | patch | Evidence contains workstation/temp absolute prefixes despite tests/README evidence rules. Normalize local prefixes to explicit stable placeholders while retaining commands, revision IDs and hashes. |
| EH-1 | high | patch | Verified the same off-renderer CTS publication/capture/disposal race as BH-2; group under work lifecycle ownership. |
| EH-2 | high | patch | Accepted outputDirectory can equal the source parent, causing generatedPath to overwrite authoritative YAML. Reject identical full source/output paths before writing. |
| EH-3 | medium | patch | Verified the same accepted namespace-without-suffix mismatch as BH-6; group with that configuration guard. |
| EH-4 | medium | patch | Verified the same custom-Sentry-without-session mismatch as BH-5; group with that configuration guard. |
| EH-5 | low | reject | Same permanent reservation exhaustion as BH-8; thousands of fixture constructions in one process are outside everyday execution, and release ownership adds lifecycle complexity. |
| VG-1 | medium | patch | Pre-verified gap: existing tenant/user tests notify before send, so they do not independently exercise ContextSignature mismatch. Add create/edit no-notification authorized-context-change regression. |
| VG-2 | medium | patch | Pre-verified gap: real fixtures pass builder arguments independently of script exports, while script tests assert only the old fields. Exercise the script launch boundary and capture every session export; pair with the existing real model/runtime consumers. |

## Design Notes

Namespace-only discovery is insufficient: the [upstream mDNS resolver](https://github.com/dapr/components-contrib/blob/v1.18.0/nameresolution/mdns/mdns.go) uses literal app IDs. Apply suffixes only in the concurrent test profile; retain original identities for production ACL proof.

## Verification

Build affected projects individually in Debug through supported source mapping; qualify Release/package consumers separately. Invoke fresh xUnit v3 assemblies with `-class`/`-method`; require real complete-mTLS gateway/ACL and dual-session cases, full AdminPortal suite, and bootstrap/cleanup CI tests. Record source-launch blockers without selector bypasses or qualifying skips.

Run `bash -n scripts/aspire-start-mtls.sh`, `bash scripts/check-no-warning-override.sh` and CRLF-aware diff checks. Resolve independent review findings. Validate exact full messages with pinned commitlint and retain evidence; fetch/inspect divergence before the authorized non-force main push.


## Implementation Handoff — 2026-10-22

Implemented all five scoped items. Sessions now own control-plane names, external and internal ports, trust/configuration directories, namespaces and optional literal app-ID suffixes. Fixture builder arguments carry configuration before the AppHost model executes; generated ACL, component scope, routing and workload identities follow the session. A private launcher passes the owned Sentry address directly to daprd because the Dapr CLI hardcodes its default Sentry endpoint. Production identity/ACL defaults remain covered by the serial original-identity profile.

The authenticated gateway now proves both the projected Party result and independent exact-key persisted actor metadata/envelopes. Real original-identity ACL tests require Dapr policy-denial evidence, while concurrent sessions prove independent markers/streams, cross-call rejection and survivor operation. Loaded forms synchronously invalidate their generation on authentication/scope changes, clear/reload values, fence old work and require fresh bounded authority against a captured submission snapshot before sending. Accepted-result convergence and busy-state ownership remain covered.

Verification receipts: `tests/five-runtime-isolation-and-form-authorization-2026-10-22/validation.json`. Final Release/package results: AdminPortal 315/315; CI bootstrap/cleanup 26/26; fixture/model/Redis/cleanup 27/27; complete-mTLS gateway/ACL 3/3; concurrent topology 2/2. Every final runtime case executed with zero skips. Bash syntax, warning-override guard and CRLF-aware diff checks passed.

The pinned source tool also launched seven healthy actual Debug projects from contained local clones at the recorded exact revisions, then completed cleanup by exact run ID. Mapped Debug CI tests passed 26/26. Focused mapped Debug AdminPortal and Integration builds remain blocked by MSB4044: SourceMappingValidationTask requires AssetsFile in FrontComposer outer multi-target contexts. No source selector/guard bypass was applied; successful package runtime acceptance is distinct from that incomplete Debug test-build qualification. DW-116 and Memories persistence remain open.

Independent review, final acceptance audit and authorized commit/push remain parent workflow steps. The frozen intent and task/acceptance block were preserved by this implementation handoff.

## Parent Acceptance Audit — 2026-10-22

Read the staged baseline diff, verified all seven execution tasks against code and receipts, and mapped every frozen matrix row to named passing tests in `tests/five-runtime-isolation-and-form-authorization-2026-10-22/matrix-audit.json`. The independent parent AdminPortal rerun passed 315/315 with zero skips and retained named XML evidence. Production ACL files, dependencies and gitlinks are unchanged. Debug source consumer-build blockers remain explicit; no blocked build is counted as passing.

## Final Review and Verification — 2026-10-22

All three independent layers completed. Every finding has a triage row; in-scope patches cover renderer clearing, cancellation/registration ownership, independent fresh-context rejection, bootstrap option guards, private default trust paths, source/output protection, scalar quoting/comments, script-to-model handoff and receipt normalization. The unchanged upstream admin/sample UI target literals are recorded as a separate deferral. The process-lifetime port-allocation exhaustion reports were rejected as negligible for ordinary test runs.

Fresh final results: AdminPortal 322/322, Release CI 30/30, model/fixture/cleanup 33/33, live gateway/ACL/isolation 5/5, mapped Debug CI 30/30; zero failures/skips. The final Debug refresh again records the FrontComposer MSB4044 consumer-build blocker separately. Tool-reported source launch readiness is time-specific; reported launcher PIDs were absent at post-build inspection and no new loaded-assembly claim is made. Exact-run cleanup succeeded. Final evidence is `tests/five-runtime-isolation-and-form-authorization-2026-10-22/final-verification.json`.
