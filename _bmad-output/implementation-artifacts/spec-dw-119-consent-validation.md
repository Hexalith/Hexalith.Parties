---
title: 'DW-119: tighten consent command validation'
type: 'bugfix'
created: '2026-10-16'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `RecordConsentValidator` accepts undefined `LawfulBasis` values, and `RevokeConsentValidator` accepts unbounded reasons. DW-119 explicitly approves tightening these rules without changing command or event wire shapes.

**Approach:** Add enum membership validation for `RecordConsent.LawfulBasis` and a 256-character maximum for `RevokeConsent.Reason`, following existing sibling validators. Keep the reason optional: null, empty, whitespace, and any other value of at most 256 characters retain their current validity. Preserve every defined lawful basis and existing consent identifier, purpose, lookup, and event behavior. Use fixed, bounded diagnostics that do not echo submitted data. Verify the boundaries in the two existing validator test classes and prove invalid commands produce only validation rejection before protected state rehydration in the existing domain-processor tests. Close only DW-119 after verification; leave adjacent deferred items unchanged.

</frozen-after-approval>

## Implementation Notes

- Planning facts: no unresolved intent gaps; no migrations, data mutations, deployment, or other irreversible actions; footprint limited to two validators, their existing tests, processor-boundary tests, this spec, and the DW-119 ledger entry. No new public API is needed.
- Reuse the existing `.IsInEnum()` convention and `.MaximumLength(256)`; do not add `NotEmpty()` or trim reasons before validation. The command contract keeps `Reason` nullable.
- The user selected DW-119 on 2026-10-16. Its recorded tightening decision supersedes the older identifier spec's prohibition on changing lawful-basis validation for this follow-up only; the earlier spec remains unchanged.
- Aspire baseline: started the explicitly targeted Parties AppHost, and `aspire wait parties --timeout 30 --non-interactive` reported healthy. `aspire describe` showed Parties and sidecars healthy but gateway, admin, Tenants, and UI projects finished, so this is not a passing full-topology baseline. Stopped only this AppHost before compilation; the unrelated Works AppHost was left running.
- Added boundary tests in `Validation/RecordConsentValidatorTests.cs` and `Validation/RevokeConsentValidatorTests.cs`, and rejection-before-rehydration tests in `Domain/PartyDomainProcessorValidationTests.cs`, under `tests/Hexalith.Parties.Tests`. New async processor calls use the xUnit cancellation token.
- Red evidence: Debug source-reference project build passed with 0 warnings/errors; the three focused classes ran 84 cases with exactly 9 failures (three undefined enum validator cases, two oversized-reason validator cases, and four processor cases). Existing cases passed. Logs: `/tmp/parties-dw119-20261016/red-build.log` and `red-tests.log`.
- Added `.IsInEnum()` and a fixed message to `RecordConsentValidator`; added `.MaximumLength(256)` and a fixed message to `RevokeConsentValidator`. No constants or public API were added; documented the touched validator types and constructors and normalized touched C# files to CRLF.
- Preserved the existing tracked LF endings in the touched C# files: wholesale CRLF conversion produced full-file diffs and Git trailing-whitespace errors. No repository formatting policy or configuration was changed.
- Green focused run: `dotnet tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.Validation.RecordConsentValidatorTests -class Hexalith.Parties.Tests.Validation.RevokeConsentValidatorTests -class Hexalith.Parties.Tests.Domain.PartyDomainProcessorValidationTests` passed 84/84 after a Debug source-reference build with 0 warnings/errors.
- Broad run: `dotnet tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll` ran 976 tests, with 923 passing and 53 failing. Fifty-two gateway cases fail during fixture setup because the synced EventStore registers `FoldedSnapshotQualificationHostedService`, absent from the fixture's explicit hosted-service snapshot. `FinalDependencyReceiptsMatchTheSelectedPackageAndSourceGraph` fails because its approved-source constants and recorded receipts predate the three gitlinks committed in `86d03a63d31b337cbcde8fc998dcde4396d48df4`. These are separate baseline-validation failures, not consent behavior failures.
- Baseline comparison: temporarily restored only the two validators from HEAD, rebuilt using the identical Debug source-reference command, and ran the same 976 tests. Exactly 62 failed: the same 53 baseline cases plus the nine DW-119 regression cases. The comparison found zero failures introduced by DW-119. Restored the edited validators in a `finally` block. Logs and exact failing-case comparison are retained in `/tmp/parties-dw119-20261016/`.
- Completion gate: the repository baseline requires all existing and new tests to pass before closure. Keep the spec in review and DW-119 open while these broader baseline failures remain; do not weaken the fixture or source-identity assertions to claim green validation.
- Review corrections: added raw-length regression cases for 257 spaces and a 256-letter reason plus trailing space; processor tests now assert exact `EnumValidator` / `MaximumLengthValidator` codes, alongside sole-rejection and no-unprotection assertions.
- Final formatting state: the five touched C# files preserve their original tracked LF endings; the earlier CRLF attempt was undone before review. No line-ending configuration changed.
- Exact local build command: `dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false`. No warning suppression or validation bypass was added.

## Review Triage Log

- [low] [patch] Raw-length whitespace/padding regression coverage was missing. Added 257-space and trailing-space cases; these detect trimming or a whitespace exemption before the maximum-length check.
- [low] [patch] Processor assertions allowed arbitrary bounded error codes. Assert exact standard codes to pin the fixed public rejection diagnostics and exclude truncated caller content.
- [false] [reject] Formatting notes describe successive implementation states rather than simultaneous claims. The later note already records preserved LF; appended an explicit final-state note while respecting append-only implementation history.
- [low] [patch] Build-command details and portable comparison evidence were incomplete. Added the exact build command above; repository evidence under `tests/dw-119-consent-validation-2026-10-16/` records counts, commands, and all compared failing cases. No frozen intent was changed.

- Final verification after review fixes: Debug source build passed with 0 warnings/errors; focused classes passed 86/86. The final full project ran 978 cases, 925 passing and the same 53 baseline failures. Portable evidence is in `tests/dw-119-consent-validation-2026-10-16/README.md` and `validation.json`. Updated only DW-119 with implementation/blocker details and left it open; appended the two independent baseline-validation follow-ups.

### Resumed review on 2026-10-16

- Review range: best-effort reconstruction from parent `86d03a63d31b337cbcde8fc998dcde4396d48df4` through committed implementation `95f21bbcd09dfbfa8d1c71a4b382c12c8eef8a0f`; no baseline frontmatter was recorded in the original spec. Three independent review layers ran: blind hunter, edge-case hunter, and verification-gap reviewer. The latter two returned no findings.
- [low] [patch] The original comparison retained failing display names without diagnostic details. Preserved representative gateway-fixture and dependency-receipt diagnostics from the retained original-validator, prior final, and resumed logs in `resume-validation.json`; each diagnostic matches across all three runs. This verifies the two reported blocker causes without claiming to compare every exception.
- [low] [patch] The earlier evidence omitted actual dependency checkouts and cleanliness. Recorded every root-declared gitlink, checkout revision, and clean dependency worktree for this resumed run; the root worktree was also clean before evidence edits. Historical dependency cleanliness cannot be reconstructed and is explicitly left uncertified.
- [low] [patch] The evidence README called runner-truncated theory display names complete case names. Corrected that description and recorded the display-name limitation explicitly; no stable-ID comparison is asserted.
- [false] [reject] Additional positive processor tests were proposed without a demonstrated regression. The new rules are covered by passing defined-enum and optional/boundary-reason validator cases; existing `PartyAggregateConsentTests` verify consent success events and copied lawful-basis/reason values. The unchanged processor invokes the registered validators before rehydration, so this review found no failing positive processing path requiring a patch.
- [low] [reject] Exactly 256 spaces is not a separate test case. Existing tests cover accepted whitespace, accepted raw length 256, and rejected 257 spaces/trailing padding; `MaximumLength(256)` applies uniformly to the raw string. No behavioral defect was demonstrated, and additional parameterization for this uncommon combination would add test complexity without changing the established boundary evidence.
- [false] [reject] ASCII-only boundary inputs do not expose a different Unicode counting rule: the reused sibling convention is FluentValidation string length, measured in UTF-16 code units. Documented that existing meaning in the maintained API contract; no scalar-value or grapheme-count requirement was introduced and no production rule needs changing.
- [false] [reject] Generating positive enum cases dynamically was suggested for possible future enum growth. The current contract has exactly the four members already covered by explicit cases, and `IsInEnum()` will accept newly defined members. No present missing member or caller failure was found.
- [low] [patch] Caller-facing documentation omitted the tightened rules. Added the allowed lawful bases, optional raw reason length and whitespace behavior, and fixed rejection codes to `docs/api-contracts.md` without changing command/event wire shapes.
- Resumed verification: the exact recorded Debug source-reference build passed with 0 warnings/errors; the focused command passed 86/86; the full-project command exited 1 with 978 total, 925 passing and the identical 53 failing display names. `tests/dw-119-consent-validation-2026-10-16/resume-validation.json` preserves current-source observations and diagnostics. No production or test code changed; the all-tests-pass completion gate still prevents closure, so the spec stays `in-review` and DW-119 stays open.


### Full test-project gate restoration — 2026-10-16

- User-authorized scope extension: keep both baseline repairs together, retain the consent implementation and all existing assertions, command/event shapes, dependency versions, and historical receipts/owner approvals. No push is authorized.
- Gateway decision: inspection of EventStore `7edc260d8c0bd904f1215f801bad02b99c6694cf` shows `FoldedSnapshotQualificationHostedService` only queries `IServiceProviderIsService` and emits a fixed warning; it neither calls Dapr nor writes state. Retain it alongside the authorization startup validator. Keep `KnownExcludedHostedServiceNames` and its equality assertion intact.
- Source-receipt decision: record current gitlink/checkouts/describes separately, preserve the previous 2026-10-16 observation as dated history, and change only the three current-source identities/describes in fitness constants. Package versions and `.gitlink-signoff.tsv` stay unchanged. This is no new G5, snapshot, release, or owner qualification.
- Runtime baseline: the explicitly targeted Parties AppHost started with `--no-build`; `aspire wait parties --timeout 30 --non-interactive` passed. Parties, EventStore gateway/admin, UI, Tenants and their sidecars were healthy; admin-ui and parties-mcp had not started. Stopped the owned AppHost before compilation. This observation is not a full-topology qualification.

## Gate restoration tasks

- [x] Inspect the EventStore hosted service and reconcile `tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs` without weakening the exclusion snapshot.
- [x] Refresh `_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md` and `tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs` against committed gitlinks, preserving history and approvals.
- [x] Run the Debug source-reference build, focused consent classes, and full `Hexalith.Parties.Tests` assembly; investigate every remaining failure.
- [x] Complete independent review, record results in the evidence bundle, and close only DW-119 once its completion gates pass.

## Verification

Given the committed dependency graph and the preserved consent implementation, when the following commands run against Debug source references, then the build must succeed with zero warnings/errors, all 86 focused consent cases must pass, and every full-assembly case must pass without skips or reduced assertions:

```bash
dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false
dotnet tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.Validation.RecordConsentValidatorTests -class Hexalith.Parties.Tests.Validation.RevokeConsentValidatorTests -class Hexalith.Parties.Tests.Domain.PartyDomainProcessorValidationTests
dotnet tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll
```

Given the dated source receipts and owner decisions, when the current observation is refreshed, then the old receipt bodies and `.gitlink-signoff.tsv` remain byte-for-byte intact and no approval or broader qualification is inferred.

- Gate-restoration verification: Debug source-reference build exited 0 with 0 warnings/errors; focused consent classes passed 86/86; full assembly passed 978/978 with no errors, failures, skips, or tests not run. No remaining test failures need investigation. Portable commands, UTC times, summaries, source identities, and history-preservation checks are recorded in `tests/dw-119-consent-validation-2026-10-16/full-gate-validation.json`. Independent review remains pending before closure.


### Gate-restoration independent review — 2026-10-16

Review range: committed HEAD `95f21bbcd09dfbfa8d1c71a4b382c12c8eef8a0f` through the current workspace, including the pre-existing resumed-review documentation/evidence. All three layers ran at the session's model capability; no layers were skipped. Edge-case and verification-gap reviewers reported no findings. Blind-hunter findings were triaged individually:

- [low] [patch] Public API documentation could be read as exposing internal property/error-code fields. `DomainCommandRejectedExceptionHandler` serializes Problem Details with `reasonCode`/`rejectionType`, not event failures. Clarified the transport boundary without changing code or wire shape.
- [low] [patch] The unqualified undefined-enum statement also covered unknown names. `JsonStringEnumConverter` rejects unknown names before validation, and `TryRejectInvalidPayloadAsync` maps deserialization exceptions to `Payload`/`InvalidJson`. Qualified the numeric `EnumValidator` path and documented the existing string path.
- [low] [patch] DW-119's current ledger fields still described 53 failures after repair. Retained those observations under historical field names and recorded the passing current gate; final status will change after review and verification complete.
- [low] [patch] Optional runtime evidence included local absolute AppHost paths contrary to `tests/README.md`. Rendered these arguments relative to the repository root and explicitly recorded that normalization. The three required gate commands remain exact.
- [low] [patch] HEAD alone identifies the failing committed baseline, not the tested uncommitted repair. Added tested-file SHA-256 binding for both changed test files, the matrix, and preserved consent implementation/tests/contracts in the final evidence.
- [low] [patch] Full-log digests lacked retained portable artifacts. Replaced them with SHA-256 digests of the committed command/runner summary artifacts, so each digest is directly verifiable without retaining application logs.
- [false] [reject] A same-simple-name hosted-service collision was hypothetical. The actual pinned registration is `AddHostedService<FoldedSnapshotQualificationHostedService>()`, and source searches show one such implementation/registration with no colliding type in this host. The existing fixture uses trusted registrations and the same name-based boundary for its authorization validator. No reachable bypass or missing exclusion was demonstrated.
- [false] [reject] Package-mode verification was proposed without a defect in the requested gate. This run explicitly proves Debug source references; the unchanged package host references EventStore Gateway `3.120.0`, and no package-consumer/CI qualification is claimed. The new retention condition has no effect when that service is absent. No verification gap was identified by the dedicated reviewer.
- [false] [reject] The optional `--no-build` runtime observation does not claim source provenance or full-topology qualification. Recorded its unverified binary provenance explicitly; the subsequent source-reference build and test runs provide the requested completion proof.


### Completion — 2026-10-16

All required gates passed again after review corrections: Debug source-reference build 0 warnings/errors, focused consent classes 86/86, full `Hexalith.Parties.Tests` assembly 978/978 with no errors, failures, skips, or tests not run. Three independent review layers completed; six documentation/evidence findings were patched and three unsupported/outside-scope claims were rejected. No new work was deferred. DW-119 and the two specifically authorized baseline follow-ups are complete. Consent code/assertions, command/event shapes, versions/gitlinks, historical evidence, and owner approvals are preserved. The bmad-build completion step requires a local commit; no push is authorized or performed.
