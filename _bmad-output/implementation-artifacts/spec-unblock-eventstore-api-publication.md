---
title: 'Fix EventStore CI regressions blocking API publication'
type: 'bugfix'
created: '2026-10-08'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Fix the upstream EventStore CI regressions blocking publication of the API needed
by Parties. Preserve Parties' sidecar security calls, package-mode dependency
selection, and required successful exact-current-main full CI before release.
Correct the package-authority evidence classification, tracked-secret scan
findings, and provider verification authentication regression without weakening
production authentication or active-package governance. Preserve sealed evidence
except for explicitly documented credential sanitization. Verify the affected
upstream test projects and record the remaining publication prerequisite.

</frozen-after-approval>

## Implementation Notes

- Latest public package indexes still end at 3.115.0. Upstream already implements
  `RequireEventStoreSidecarChannel`, but current push CI run 37742090359 for
  `4cc77f9554395e84539e173e94b8f0b4df14d643` fails in Contracts, Server secret
  protection, and ProviderVerification tests. This work repairs those causes in
  the owning root-declared EventStore repository; it does not duplicate security
  plumbing in Parties or invent an unpublished dependency version.
- Investigation used three read-only agents and the actual CI job logs. The
  package-authority gate needs exact exclusions for two immutable historical
  package-observation inputs; active/sibling build inputs must remain checked.
  The provider harness must override its explicit authentication policy to its
  per-run credential scheme after production registration; production JWT policy
  stays intact. Secret findings require structural false-positive corrections
  and removal of an actually captured runtime OTEL header value.
- No commit, push, package publication, release dispatch, submodule update, or
  deployment is included in this local fix. The normal owner publication and
  subsequent Parties package upgrade remain required to close the compiler gap.

- Implemented exact historical exclusions for the captured central package file
  and identity probe, verified against pinned original SHA256 hashes before every
  authority-validator invocation. Added tests that tampering fails and that
  sibling `.csproj` and `.props` overrides remain rejected in Git-index and
  filesystem discovery modes.
- Restricted NuGet scanner exemptions to three known public package identities at
  valid lock/asset/dependency-capture positions, with library metadata and SHA512
  content hashes. Bare JSON null exemptions require a complete parsed document
  and the exact token offset; quoted null and malformed documents remain scanned.
  UTF-8 offset conversion now advances once through the content. Added tests for
  unicode offsets, adjacent secrets, spoofed schemas, malformed library entries,
  and NuGet version metadata/ranges.
- Preserved the sealed serialization XML capture and pinned its exact path and
  original content hash; a raw scan still reports only its known username-only
  URI example. Renamed a cancellation variable instead of adding a scanner rule.
- Sanitized 17 occurrences of the recorded OTEL header credential in one JSON
  capture. Updated only its size/hash entry in `execution-file-manifest.json`
  and added `credential-sanitization-2026-10-08.json`, recording old/new hashes and
  replacement count without retaining credential material.
- Provider verification now explicitly selects its per-run authentication scheme
  in the authorization default policy after production service registration.
  Production JWT registration and Parties' sidecar calls were not changed.
- All four affected upstream projects build in default package-mode Release with
  zero warnings and errors. Focused package-authority, seal, secret-protection,
  provider credential, and client framing checks pass. The final complete Server
  suite passes 3,927 tests with 25 existing skips. Exact commands, outputs, hashes,
  and fixture limits are in [the validation receipt](tests/eventstore-ci-unblock-2026-10-08/README.md).
- Provider Kestrel checks use an isolated temporary fixture root with copied test
  binaries and read-only links to production source and the already initialized
  umbrella FrontComposer sibling at exactly the declared nested gitlink commit
  `c561b3210f15206a90c39c82c58f2e5b1005cd60`. The normal in-repository provider
  command fails with `DirectoryNotFoundException` for the uninitialized nested
  consumer fixture. No nested submodule was initialized or updated. Three focused
  Kestrel cases pass; this does not claim a full provider suite or full CI pass.
- The remaining publication prerequisite is successful full push CI for the exact
  resulting upstream main SHA, followed by normal validated package publication.
  Only after that publication can Parties select the real compatible package
  version and verify its ordinary package-mode Release build. Local checks do not
  authorize or replace that release gate.

## Review Triage Log

1. **medium — patched:** Full UTF-8 prefix conversions made JSON token offsets
   quadratic for captures with thousands of nulls. Incremental conversion now
   counts each byte at most once; unicode-offset and complete scanner tests pass.
2. **medium — patched:** Two new historical build exclusions initially lacked an
   executable seal check. Pinned original hashes now gate every validator call;
   unchanged evidence passes and byte tampering is rejected.
3. **medium — patched:** An empty-library schema lookalike could conceal a numeric
   credential. Exact public identities, library metadata, valid hash checks, and
   rejection tests now prevent both arbitrary-name and known-name lookalikes.
4. **medium — patched:** Reusing an npm version regex rejected valid NuGet build
   metadata and dependency ranges. Separate NuGet handling and four positive and
   negative regression cases pass.
5. **medium — patched:** Malformed JSON metadata could throw for a nonobject
   library or a null ancestor name. Kind/name guards now reject those shapes;
   both cases are exercised in asset and nested-capture tests.
6. **low — patched:** Investigation notes lacked replayable commands, results,
   and fixture limitations. The implementation notes and validation receipt now
   distinguish focused checks, complete suites, and the outstanding full CI gate.

- Upstream live main advanced independently to `d87c969b6518751fafec9fbd01733d885b2efbfe`
  during validation, changing command API documentation and an unrelated Server
  integration test. These local patches remain on the recorded base; publication
  requires incorporating current main and passing its exact-source full CI.

## Approved publication follow-through

The user answered `yes` to committing/pushing the verified fixes, waiting for
successful full CI, publishing through the normal release workflow, and updating
Parties to the actually published package. Concurrent workspace work had already
committed and pushed the exact reviewed patch as
`3600d196799b7bbc5398a6d126e918171f07a2d3`. This session created no duplicate
commit. The selected full commit passes the owning pinned commitlint, and push CI
run `37747659600` subsequently passed. The initial release then stopped because
main advanced; the completed normal publication is recorded below.


### Publication and consumer result — 2026-10-08

- The initial normal release stopped before package publication because live main
  advanced. The selected source `b830d9829af70536d2a3fd21c5e2a23b2ca2f256`
  passed full push CI `37749252540`, including all required sidecar-security
  cases. Normal Release `37750172086` used `bypass-validation=false`, passed all
  source/publication gates, and published `v3.117.0`. No validation gate was
  weakened.
- All 14 actual public NuGet packages were downloaded and matched the release
  commit/version. The DomainService assembly exposes the required public generic
  endpoint-builder extension; its Client and ServiceDefaults dependencies both
  select `3.117.0`. Parties now pins that published version before the shared
  catalog import. Security calls and package-mode defaults are preserved.
- Normal solution restore and Release build pass with zero warnings and errors.
  All 11 unit-test projects, 101 CI contract tests, and 35 sidecar-security cases
  pass. A documentation fitness failure exposed AppHost SDK `13.6.0` against the
  already-selected Builds catalog's Aspire `13.6.1`; the SDK and maintained version
  tables were aligned, and all 10 documentation checks now pass.
- The current source identity receipt and fitness constants now reflect the
  existing Builds, Memories, and Tenants pointers plus the released EventStore
  pointer. Historical source approvals, parity receipts, and rollback obligations
  retain their scope. The immutable current-root identity fitness check runs
  after the upgrade commit and before push. No nested submodules were initialized.
- Exact public package, dependency graph, build, and consumer test evidence is in
  [the upgrade receipt](tests/eventstore-package-upgrade-2026-10-08/README.md).
  Integrated topology runtime acceptance and Parties exact-source full CI remain
  distinct from resolving this package API mismatch.


### Consumer upgrade review triage

1. **medium — patched:** Python optimization removed assert-based provenance and
   dependency checks. Explicit exceptions now retain these checks under `-O`.
2. **medium — patched:** A failed replay could leave stale success evidence in a
   reused directory. The probe invalidates prior success/failure outputs first.
3. **medium — patched:** Temporary-only test outputs limited auditability. The
   receipt now retains sanitized per-case outcomes/counters and exact build logs.
4. **medium — patched:** Selected-input hashes did not bind concurrent source/test
   changes. Local observations retain that limitation; the committed upgrade is
   verified in an isolated checkout with a complete source manifest before push.
5. **low — patched:** OCI proof referenced five omitted raw files. All five public
   manifest/config artifacts are now retained and their recorded hashes match.
6. **low — patched:** Graph replay instructions omitted extraction commands. The
   receipt now includes a replayable graph/input observation script and command.
7. **low — patched:** An earlier journal paragraph still described CI as running.
   It now records the completed first CI and the subsequent source-drift stop.
8. **medium — patched during review:** Release inventory was read from mutable
   checkout content. The probe now reads/hashes the manifest from the exact SHA.
9. **medium — patched during review:** Metadata proof omitted declaring-type and
   receiver/return/extension checks. The strengthened verifier and public package
   probe were replayed successfully.
10. **low — patched during review:** Architecture still cited the previous current
    Tenants pointer. It now matches the committed selection and prerequisite matrix.


### Committed-check follow-up

The normal fresh Release build passes in physical detached root-dependency
checkouts. A preliminary shared-link layout produced path/analyzer failures and
was discarded as unsuitable for build proof. The committed domain host output
contains the exact downloaded DomainService DLL hash.

The committed integration run found a stale current FrontComposer catalog
assertion/receipt (`4.5.0` versus the already-selected catalog's `4.6.0`); those
current values were corrected while historical `4.5.0` parity remains intact.
It also exposed the pre-existing Story 8.8 missing `Block If — available-row
identities` heading at committed HEAD. The user already has the correction in
uncommitted work; it remains preserved and unstaged by this task. This separate
documentation gate blocks a claim that the whole committed integration lane is
green. The package API fix, ordinary fresh Release build, unit lane, CI contract
lane, and sidecar-security checks are verified independently.
