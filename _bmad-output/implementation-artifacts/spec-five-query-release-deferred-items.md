---
title: 'Complete five query and release verification deferrals'
type: 'bugfix'
created: '2026-10-22'
status: 'in-review'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 12a071cf7dfec52b335f52e7d162fb835f49af9c
context: ['AGENTS.md', 'docs/development-guide.md', 'tests/README.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Identity queries still call options providers and select binding intervals outside their bounded work boundary. Ordinary main pushes omit gitlink signoff enforcement, and post-release verification proves a GitHub tag without checking the complete published artifact set.

**Approach:** Complete five ledger entries together: DW-109, DW-161, DW-41, and the two unnumbered entries from `spec-five-identity-routing-deferred-items.md` concerning options acquisition/policy revalidation and post-fold interval selection. Implement, independently review, verify, commit and push the completed batch to main as requested.

## Boundaries & Constraints

**Always:** Preserve caller-token cancellation precedence, exact interval/outcome semantics, source positions, terminal authority/policy/custody checks, and finite shared work capacity. Preserve exact-source CI and protected manual publication. Reuse existing package validation and the immutable Builds OCI validator.

**Never:** Change dependencies, gitlinks, signoffs, contracts, retention, persistence or deployments. Never publish for testing or claim downloaded registry bytes equal unsigned build archives. Leave unrelated deferrals open.

## I/O & Edge-Case Matrix

| Scenario | Input/state | Expected behavior | Error handling |
|----------|-------------|-------------------|----------------|
| Options setup | Stalled or faulting CurrentValue | Bounded unavailable without evidence | Caller cancellation wins; retain occupied capacity |
| Configured budget | Options arrive with shorter timeout | Measure total elapsed time from query entry | Invalid/exhausted budget denies |
| Selection | Long/stalled interval scan | Same bounded work lease and exact interval semantics | Deadline denies late results |
| Main push | Multiple commits since event.before | Check every root gitlink change over that range | Missing/invalid base or unsigned bump fails |
| Published set | Nine declared packages and three images | Validate every artifact against release version/source | Any missing or mismatched artifact fails |
| Registry delay | Temporary missing/transient response | Finite retry and retained evidence | Exhaustion/malformed bytes fail |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Parties/Queries/PartyIdentityQueryService.cs`: both methods read CurrentValue before deadline construction; PolicyIsCurrent and both LINQ interval scans are synchronous. `PartyIdentityQueryDeadline.cs` and `PartyIdentityQueryWorkLimiter.cs` already bound workers and retain leases through abandoned work/callback completion.
- `tests/Hexalith.Parties.Tests/Gateway/PartyIdentityQueryHandlerTests.cs`: reuse options substitutes, controlled time, gates and saturation coverage.
- `.github/workflows/rc-gate.yml`: lacks main trigger; current fallback can compare against the new origin/main tip. `scripts/gitlink-rc-gate.sh` owns ledger checks. Existing fitness constants already pin Tenants/Memories.
- `.github/workflows/release.yml`: verify-publication resolves matching_tag but performs no registry verification. `ReleasePublicationVerificationTests.cs` executes its named shell with fake gh.
- `tools/release-packages.json` declares nine IDs; `scripts/validate-nuget-packages.py` owns archive metadata/boundary validation. Existing release publisher selects immutable Builds 397c94a4e246c90b21cf408790fa0d55bf32d795; its `Github/publish-containers/oci_registry_validator.py` validates registry manifests, platform configurations, digests and provenance.

## Tasks & Acceptance

**Execution:**

- [x] Mapped identity service/deadline -- create the bounded boundary before options acquisition, cap setup at the existing thirty-second maximum, then apply the configured budget from original entry time without releasing its lease; bound every policy revalidation and interval selection. Preserve checks at the actual evidence-release instant after awaits.
- [x] Mapped identity tests -- gate initial options/revalidation for both queries; cover cancellation, saturation/recovery, late faults and elapsed budgets. Test bounded selection and preserve gap/ambiguity, exclusive-end, custody and source positions.
- [x] `.github/workflows/rc-gate.yml`, `scripts/gitlink-rc-gate.sh`, `tests/Hexalith.Parties.Ci.Tests/GitlinkGateWorkflowTests.cs` -- enforce main pushes against validated event.before; fail closed on unusable bases. Execute real temporary-Git fixtures for unsigned/signed/stale/placeholder/reset entries and multi-commit ranges. Preserve existing RC/tag/manual behavior.
- [x] `scripts/validate-nuget-packages.py`, new `scripts/verify-release-publication.py` -- reuse archive validation for Parties' exact inventory, bounded registry downloads, nuspec ID/version/repository/type/commit checks and byte-derived receipts. Reject malformed archives, duplicate nuspecs and ambiguous inventories. Orchestrate the existing pinned Builds OCI validator for parties, parties-mcp and parties-ui, including both published architectures and exact provenance.
- [x] `.github/workflows/release.yml` -- expose matched tag; check out dispatched source and immutable Builds without submodules; wire verification with read access and always-upload evidence. Keep secrets out of commands/receipts.
- [x] `tests/Hexalith.Parties.Ci.Tests/ReleasePublicationVerificationTests.cs`, new `ReleaseArtifactVerificationTests.cs` -- preserve tag cases; execute verification against isolated HTTP/archive fixtures and the real shared OCI validator. Reject missing artifacts, incorrect metadata/digests/platforms, partial inventories, authentication failures and exhausted retries; demonstrate bounded propagation recovery and byte-derived receipts.
- [x] `docs/ci.md`, `RELEASE-CHECKLIST.md` -- document main-push enforcement and complete publication proof, including the limits of observed registry hashes.
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- close only the five verified entries. Retain exact commands/results under `tests/five-query-release-deferred-items-2026-10-22/`.

**Acceptance Criteria:**

- Given either identity query, when options or selection outlast the budget, then the caller completes without evidence and outstanding work remains bounded until cleanup settles.
- Given valid identity evidence, when work completes in budget, then all existing authorization, temporal and cancellation outcomes remain unchanged.
- Given an ordinary main push, when an unsigned root pointer changes anywhere in the push range, then the gate rejects it; unchanged or correctly signed pointers pass.
- Given a matching GitHub release, when any declared registry artifact fails validation, then verification fails and retains diagnostics; a complete valid set passes.

## Implementation Notes

Final parent verification after review: service 2,450/2,450 and CI 360/360 pass without failures, errors or skips. Focused identity 296/296 and all twelve connection/download controls pass. Independent follow-up confirms the caller-path, redirect and connection deadline findings are resolved. Exact main message passes pinned commitlint; remote main matched the baseline at the pre-push fetch.

Parent audit: staged and read the complete 2,484-line diff, including new files and evidence. All eight tasks and six matrix rows have matching executed passing checks. Fresh service/CI assemblies pass 2,446/2,446 and 314/314; focused identity 292/292 and CI 98/98 pass without skips. Independent review is next.

Approved by the user on 2026-10-22. The implementation handoff performs implementation and verification; the parent handles independent review, staging, validated commit and the authorized push.

Five items and the main push are explicitly requested. Runtime/workflow changes merit dispatch; no product choices remain.

## Spec Change Log

## Review Triage Log

Final disposition: all patch findings below are resolved and verified. Follow-up reviewers confirm no material remaining findings; B2 is the single pre-existing policy deferral and B9 is rejected.

- E4 — medium, patch: follow-up review proves synchronous DNS inside socket.create_connection can outlast the request and total budgets (201 ms against 50 ms). Bound connection establishment, including resolver waits and address attempts, while ensuring late sockets are closed and outstanding abandoned work stays finite.

- B1 — medium, patch: per-ref cancel-in-progress also cancels main pushes; a later push checks only its own event.before. Use a unique concurrency group per main source so running and pending ranges survive later pushes.
- B2 — medium, defer: release already required successful ci.yml and tip-only release-floor signoff before this change. Coupling the independent RC workflow to publication is a pre-existing governance gap; preserve the approved CI/publication contract and record separate follow-up.
- B3 — medium, patch: the new range loop compares merge results against every parent, demanding a signoff for an inherited first-parent pointer when the other parent is older. Compare each reachable commit's actual first-parent advance and add merge controls.
- B4 — medium, patch: probes show a continuously arriving body succeeds after the configured request timeout; urllib enforces inactivity timeouts. Enforce one absolute per-request deadline across headers, body and redirects, bounded by the total deadline.
- B5 — medium, patch: redirect origin tuples treat implicit HTTPS and explicit :443 as different, stripping same-origin credentials. Normalize effective ports and cover preservation and cross-origin removal.
- B6 — medium, patch: descendant metadata searches accept package/wrapper/metadata as a valid nuspec. The new published-byte validator must require direct package metadata and direct fields.
- B7 — medium, patch: a file license naming ABSENT.txt is accepted because only nonempty text is checked. Validate referenced license files while preserving valid expression/file controls; this is exposed by the new published-archive validation.
- B8 — medium, patch: the new published validator omits the existing required dependency/version checks. Reuse those checks against exact dispatched-source catalog values and give successful fixtures valid dependency metadata.
- B9 — low, rejected: malformed metadata errors share a support-safe diagnostic, while the final download observation already identifies the artifact and retained bytes allow inspection. A new fine-grained error taxonomy adds complexity for a failure normally rejected before publication; no ordinary-use need warrants it.
- B10 — low, patch: failed body attempts omit status/byte counts/hash and discard bounded observed bytes, weakening promised failure evidence. Record bounded partial captures and attempt facts without exposing credentials.
- E1 — medium, patch, grouped with B1: a later main push can cancel the only run checking an earlier unsigned advance; unique main-source concurrency must cover pending runs as well as running ones.
- E2 — medium, patch: inherited HTTPRedirectHandler drains redirect bodies with unbounded fp.read() outside the downloader. Close/bound redirect responses rather than inheriting that drain, and cover byte and deadline behavior.
- E3 — medium, patch, grouped with B4: a 50 ms request probe succeeds after 178 ms of continuous body arrivals; enforce wall-clock request duration rather than socket inactivity alone.
- V1 — medium, patch, pre-verified: workflow base/mode selection is only text-asserted; direct CLI fixtures cannot detect replacing event.before with HEAD. Execute the actual shell and invoke the gate using its outputs.
- V2 — medium, patch, pre-verified: stalled scanner coverage constructs its own deadline and cannot detect either service caller reverting to synchronous selection. Cover actual current/historical service results, cancellation precedence and capacity retention/recovery.
- V3 — medium, patch, pre-verified, grouped with B5/E2: no redirect fixtures protect credential isolation or downgrade rejection. Add same-origin, two-origin and HTTPS-downgrade cases with actual handler coverage.
- P1 — medium, patch: parent reproduction adds an unsigned gitlink before declaring it in a later commit; the gate incorrectly passes because declarations are collected incrementally. Collect all root paths across the range before inspecting advances; add signed/unsigned delayed-declaration controls.

- carried B2 — medium, defer: carried: `.github/workflows/release.yml` still proves the release-floor tip and does not read the main-push range verdict. The open ledger entry already records that gap. No second deferral and no patch.
- R1 — medium, patch: `scripts/gitlink-rc-gate.sh` push-range skips a path when `git ls-tree` is not mode `160000`. Deleting a root gitlink or replacing it with a blob passes. A parent `160000` entry that is missing or no longer a gitlink must fail, with removal and mode-change fixtures.
- R2 — low, rejected: `ledger_has` reads the worktree ledger. A GitHub checkout of the pushed commit is that committed file, and a missing file fails closed. A dirty local ledger is outside the main-push job.
- R3 — low, patch: an empty `--push-range` base prints `--diff requires <base-ref>`, and a range failure still says to resolve pointers before tagging. Say which mode failed.
- R4 — maybe-false, defer: the first non-draft tag whose peeled commit equals the dispatched SHA supplies `--version`. Whether GitHub lists a non-version tag first when two tags share that SHA is unverified. If it does, publication can prove the wrong version. Severity if true: medium.
- R5 — false: image proof stops at the pinned validator's index, child manifests, configs and provenance. The accepted contract does not fetch layer blobs.
- R6 — false: cross-origin HTTPS redirects are the reviewed credential design. Authorization is removed off-origin, and the downloaded bytes are the required evidence.
- R7 — medium, patch: `ElementTree.fromstring` expands internal entities in a registry nuspec. Reject a nuspec that contains a doctype or entity declaration before parsing. `zlib.error` from `testzip` does not escape `verify-release-publication.py`; its broad handler already fails closed.
- R8 — false: on the current query, `SameAuthority` uses `completedAt` sampled after `PolicyIsCurrentAsync` and checks revision and `ExpiresAt`. The policy flag is the result of the await that just finished.
- R9 — defer: `ResolveAtAsync` still deserializes the envelope before the deadline. That placement predates this batch. The new boundary covers options and selection.
- R10 — false: a 20-minute job timeout or a second production approval fails or holds the job. Neither accepts incomplete proof. The production environment is what supplies the read credentials.
- R11 — false: author-supplied nuspec repository fields, and hashes that do not equal unsigned build archives, are the accepted publication limit.
- R12 — false: `in-review` is this review's status. The ledger closures and the temporary commitlint file are the pre-commit record, not a product defect.
- R13 — high, patch: `Downloader.get` treats only `scheme == "https"` as TLS, while origin comparison lowercases the scheme. A redirect to `HTTPS://` keeps `Authorization` and then uses `HTTPConnection` on port 80.
- R14 — medium, patch: after the historical post-fold policy await, `SameAuthority` is not sampled again before gap, ambiguity or mismatch. A stall in that await can return those outcomes after `ExpiresAt`. Recheck at a fresh instant before those returns.
- R15 — false: the current-query policy await sits before `SameAuthority`, and that check uses the later instant. Expired or revised admission does not resolve.
- R16 — medium, patch: the only nuspec may live below the archive root. Require the sole nuspec path to contain no slash or backslash.
- R17 — low, patch: license `type="File"` skips the file-exists check. Compare that type case-insensitively.
- R18 — medium, patch, pre-verified: no executed test reads `matching-tag` or checks that artifact verification strips one leading `v`.
- R19 — medium, patch, pre-verified: the 10000-member, 256 MiB and 1 MiB nuspec bounds have no failing fixture.
- R20 — medium, patch, pre-verified: a second repository element and a non-package nuspec root have no failing fixture.
- R21 — medium, patch, pre-verified: redirects with userinfo or a non-HTTP scheme have no fixture. Cover them with the uppercase-scheme case.
- R22 — medium, patch, pre-verified: failing receipts do not assert the fixed non-`PublicationError` sentence, so a raw parser message could be uploaded unnoticed.
- R23 — defer, pre-verified: GitHub evaluates `on: push` to `main` and the per-SHA concurrency group. The local suite runs the selected step scripts. Another wording assertion would not execute scheduling.
- R24 — defer, pre-verified: deleting `environment: production` from `verify-publication` still satisfies the release job's existing environment assertion. Environment protection runs only on GitHub.

## Verification

- Build affected service/CI projects individually; run focused xUnit v3 classes directly, then affected assemblies.
- Use supported Debug/source checks first; record exact blockers before serialized Release/package fallback. Use fresh binaries.
- Run local registry fixtures and actionlint; never publish for validation.
- Run `bash scripts/check-no-warning-override.sh` and `git diff --check`; independently review the diff. Validate the exact temporary commit-message file using pinned commitlint and retain its successful output.
- Fetch and inspect divergence before the authorized main push; preserve unrelated work and do not force push.
