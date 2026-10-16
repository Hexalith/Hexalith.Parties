---
title: 'DW-119: tighten consent command validation'
type: 'bugfix'
created: '2026-10-16'
status: 'in-review'
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
