# Five deferred test harness items — 2026-10-21

Completed DW-113, DW-117, DW-118, DW-138 and DW-190. DW-157 is the duplicate
of DW-138 and shares its resolution; it is not counted as a sixth work item.

The selected-package Debug builds for Client.Tests, UI.Tests, Parties.Tests and
Ci.Tests pass with zero warnings/errors. Full Client (332), UI (387), CI (188)
and focused prerequisite checks (22) pass: 929 .NET cases with no skips.
Eight executable Node checks cover the environment matrix, explicit external
ownership, a real Playwright occupied-URL refusal and managed startup/response/cleanup.
They also pass on the declared Node 24.0.0 floor. TypeScript checking passes.
The maintained `tests/Hexalith.Parties.UI.Tests/verify-restored-assets.py` is required
in CI. It restores a real local fixture nupkg, compiles the exact metadata declarations
and actual asset resolver, then runs its assembly and verifies stylesheet bytes.
Five scenarios cover NuGet configuration, RestorePackagesPath, NUGET_PACKAGES,
empty NUGET_PACKAGES and an empty primary cache with the package in a fallback.
The initial item-only probe in validation.json is historical and superseded.
A separate actual UI stylesheet guard run with runtime-empty NUGET_PACKAGES passes 4/4.

The broader prerequisite class initially passes 51/52. Its unchanged
FinalDependencyReceiptsMatchTheSelectedPackageAndSourceGraph test expects Builds
fff4a212180d8cd1c5c0759655a9bc4fb6963c45, but the committed gitlink is
2744d549270298cfe73679f76b477a7368429006. The same failure is reproduced after
building the original PlatformApiPrerequisitesTests.cs from baseline Parties
92be80b12d57cff4351b70ea3cee47da4ba886d4. Candidate source was restored byte for
byte and rebuilt before the final focused checks. No gitlink or dependency
changed, and no complete green prerequisite class or solution gate is claimed.

The pinned source launcher exits 2/HXR011 because HEXALITH_DAPR_HOME is unset.
Aspire start/describe/stop succeeds, but dependent resources wait on security.
This evidence does not qualify the complete topology, source graph or browser
accessibility. The new Playwright configuration checks need no installed browser.

Exact commands and scoped outputs are in `validation.json`. Commitlint 21.2.2
success for the exact candidate is in `commitlint.txt`. The warning/submodule
guard and CRLF-aware whitespace check pass. Review triage is kept in the spec.

Independent review identified seven issues in the initial candidate; all were patched.
The reviewer confirmed the nested-parser recovery and CI Release fixes and found
no remaining introduced defects. The compiled restore fixture passes all five
scenarios both locally and with CI=true; CI executes Release assets.
