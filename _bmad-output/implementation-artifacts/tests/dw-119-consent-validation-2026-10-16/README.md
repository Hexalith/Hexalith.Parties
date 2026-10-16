# DW-119 consent validation evidence

The implementation rejects undefined lawful bases and revocation reasons longer than
256 characters. Null, empty, and short whitespace reasons remain valid. Diagnostics
are fixed; processor tests prove rejection precedes protected-state rehydration.
The original review remained **in review** because the repository requires all existing
and new tests to pass before closure. The gate-restoration results below supersede
that earlier blocker status. No source-selection approval or release
qualification is asserted by this evidence.

## Historical commands and results

```bash
dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Debug -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false
dotnet tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.Validation.RecordConsentValidatorTests -class Hexalith.Parties.Tests.Validation.RevokeConsentValidatorTests -class Hexalith.Parties.Tests.Domain.PartyDomainProcessorValidationTests
dotnet tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll
```

| Check | Result |
| --- | --- |
| Debug source-reference build | Pass, 0 warnings, 0 errors |
| Final focused classes | 86 passed, 0 failed |
| Final full project | 978 total, 925 passed, 53 failed |
| Original-validator comparison before two review cases | 976 total, 914 passed, 62 failed |

The original-validator comparison retained the new regression tests, replaced only
the two production validators with their contents at the recorded pre-work revision,
then used the identical build and full-project commands. Edited validators were
restored in a finally block. The original validators failed the same 53 existing
cases plus all nine new negative consent cases. The final run has no additional
failing cases. Two passing whitespace/padding regression cases were added during
review, explaining its final total of 978 rather than 976.

[validation.json](validation.json) records the exact parent revision, commands,
summary counts, and compared failing-case display names as emitted by the runner.
Long theory arguments in those names are truncated by the runner; they are not
stable test identifiers or complete argument values. The initial focused run
was red (9 failures out of 84); all final focused tests pass.

## Historical baseline blockers

- Fifty-two `EventStoreGatewayRoutingTests` cases fail during fixture setup: the
  current EventStore source registers `FoldedSnapshotQualificationHostedService`,
  absent from the fixture's explicit hosted-service exclusion snapshot. Review the
  service's dependencies before deliberately reconciling that snapshot.
- `PlatformApiPrerequisitesTests.FinalDependencyReceiptsMatchTheSelectedPackageAndSourceGraph`
  fails because its constants and current-source receipts still describe the
  submodule revisions preceding the earlier sync commit. Refresh current observations
  with exact evidence, retaining dated historical receipts and separate owner gates.

The original baseline failures were not hidden or weakened. Before gate restoration,
the two consent validators, their tests, and the processor tests were the only
C# files changed in DW-119.

## Runtime baseline

The explicitly targeted Parties AppHost started and its `parties` resource reported
healthy. Gateway, admin, Tenants, and UI projects were finished when described;
therefore this is not a passing full-topology qualification. The Parties AppHost
was stopped before compilation. No unrelated AppHost was stopped.

## Resumed verification

At source revision `95f21bbcd09dfbfa8d1c71a4b382c12c8eef8a0f`, the same commands
again produced a clean Debug build (0 warnings/errors), 86 passing focused tests,
and 978 full-project cases with 925 passing and 53 failing (exit 1). The failing
display-name set is unchanged. No production or test code was changed in this
resumed review.

[resume-validation.json](resume-validation.json) records the full root revision,
the committed and checked-out identities and clean working trees of all ten
root-declared dependencies, commands, results, and representative failure details.
The gateway fixture and dependency-receipt diagnostics match both the earlier
original-validator comparison and the previous final implementation run. The
original comparison did not capture dependency worktree state; the new observations
apply to this resumed run only. These two representative matches establish the
reported blocker causes, rather than treating matching display names as proof of
every individual failure cause. The spec remains in review and DW-119 remains open.


## Full test-project gate restored

The user requested both baseline repairs together. The Debug source-reference
build passes with **0 warnings and 0 errors**, the focused consent classes pass
**86/86**, and the full `Hexalith.Parties.Tests` assembly passes **978/978**,
with **0 errors, failures, skips, or tests not run**. Independent review is complete and the required gates passed again after its
documentation/evidence corrections. DW-119 is **done**; no new work was deferred.

The gateway fixture retains `FoldedSnapshotQualificationHostedService` because
source inspection shows it only checks the DI qualification registry and logs
a fixed startup warning. Its dependencies have no Dapr calls or state writes.
The existing excluded-service snapshot and all gateway assertions remain intact;
the authorization startup validator still runs.

Current-source fitness constants and the prerequisite matrix now match the
three gitlinks committed in `86d03a63d31b337cbcde8fc998dcde4396d48df4`:

| Dependency | Current committed source | Describe |
| --- | --- | --- |
| EventStore | `7edc260d8c0bd904f1215f801bad02b99c6694cf` | `v3.122.0-13-g7edc260d` |
| FrontComposer | `5fac9ab1df26da19ceadf626f9e557884a189e7c` | `v4.6.0-40-g5fac9ab1` |
| Tenants | `6ad5e502fa5d6e453e24610ef418007e1bd53298` | `v5.8.0-89-g6ad5e502` |

All ten root checkouts match their committed gitlinks and are clean. The prior
current-source observation is preserved under a dated historical heading;
older matrix content, `validation.json`, `resume-validation.json`, and owner
signoffs are unchanged. The consent implementation, focused test assertions,
command/event contracts, package versions, and gitlinks are unchanged.

[full-gate-validation.json](full-gate-validation.json) records exact commands,
UTC execution times, exit codes, counts, dependency observations, and preservation
checks. Portable command/runner summaries are retained alongside it as
`full-gate-build-summary.txt`, `full-gate-focused-summary.txt`, and
`full-gate-full-summary.txt`. The JSON includes verifiable SHA-256 digests of the retained summaries, which
omit application logs and local build output paths. The optional `--no-build`
Aspire observation used existing binaries whose source provenance was not
verified; the subsequent Debug build and assembly runs supply the requested
source-reference proof.

This passes the requested local test-project gate. Historical approvals remain
bound to their original identities; no new owner signoff, G5 parity, snapshot
enrollment, release, publication, or full-topology qualification is claimed.
No push was made.


Three independent review layers completed. The edge-case and verification-gap
layers found no issues; six documentation/evidence findings were corrected,
and three unsupported or outside-scope claims were rejected with evidence in
the spec triage log. Final results remain 0 build warnings/errors, 86/86 focused
passes, and 978/978 full-assembly passes. The JSON binds these results to hashes
of the tested source, contract, test, documentation, and matrix files. Historical
receipt bodies and owner approvals remain unchanged; no additional qualification
is implied. DW-119 and its two baseline repair follow-ups are complete.

The local completion message passed repository-pinned commitlint 21.2.2 with
zero problems/warnings; `full-gate-commitlint.txt` preserves its exact text and
validation output.
