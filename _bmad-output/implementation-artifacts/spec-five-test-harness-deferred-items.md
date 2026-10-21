---
title: 'Complete five deferred test harness items'
type: 'bugfix'
created: '2026-10-21'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Five open deferred items weaken the reliability of existing checks: DW-113 parses trailing MSBuild diagnostics as JSON, DW-117 guesses the restored asset cache, DW-118 can reuse unrelated local servers, DW-138 combines identity expiry and wrong-purpose rejection, and DW-190 does not require the shared-status structural evidence command.

**Approach:** Complete these five items in one batch, as requested. Consume only the first complete MSBuild JSON object, resolve stylesheet assets from evaluated restore metadata, give Playwright explicit server ownership with executable environment coverage, isolate the two identity rejection causes with a valid control, and require the exact FrontComposer shared-status command. Verify the affected checks, independently review, reconcile the ledger, commit and push to main.

</frozen-after-approval>

## Implementation Notes

- The user explicitly authorized five deferred items and a push on main. Initial revision is 92be80b12d57cff4351b70ea3cee47da4ba886d4 with a clean tree. These are small reversible test/configuration corrections with no intent gap, public API, data mutation or dependency upgrade; use oneshot. DW-157 duplicates DW-138 and will cross-reference its completion rather than be counted as another work item.
- Code map: tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs owns EvaluateProjectGraph, required evidence dictionaries and command-removal tests. Reuse Utf8JsonReader/JsonDocument.ParseValue and existing assertion helpers. Current live MSBuild output is clean; mixed-output fixtures establish robustness without claiming reproduction in the current toolchain.
- Code map: tests/Hexalith.Parties.UI.Tests/AccessibilityStyleGuardTests.cs reads the catalog-selected shell CSS. Add evaluated NuGetPackageRoot and HexalithFrontComposerVersion assembly metadata in its csproj; do not consult runtime environment or guess the cache. Exercise alternate restore configuration with a disposable restore probe.
- Code map: tests/e2e/playwright.config.ts retains its existing child Test environment, CI Release and local Debug commands. Set reuseExistingServer=false; PLAYWRIGHT_SKIP_WEBSERVER remains the explicit external-server choice. Test the actual config in isolated Node subprocesses across CI/local and Test/Development, including explicit external ownership. Add the configuration check to the existing CI job and update its Ci.Tests guard and the obsolete UI text-only assertion.
- Code map: tests/Hexalith.Parties.Client.Tests/HttpPartiesIdentityClientTests.cs already supplies HTTP envelopes and a completion clock seam. Given valid current human evidence, then a successful resolve is returned; given only wrong purpose or only an expired binding, then a bounded 503 is returned. Use a fixed injected clock and preserve production validation.
- Acceptance: given logs before/after a nested valid JSON object containing escaped quotes/braces, when parsing, then only that object is read; missing/incomplete JSON still fails. Given nondefault NuGet restore settings or empty NUGET_PACKAGES, then stylesheet lookup uses the actual evaluated cache. Given any managed-server environment, then Playwright cannot reuse an existing listener. Given removal of only the required shared-status command, then evidence validation fails even when other commands remain.
- Aspire start/describe/stop succeeded; dependent resources waited on security. The pinned source launcher exits 2/HXR011 because HEXALITH_DAPR_HOME is unset. Use the available selected-package Debug fallback for focused validation, preserving this limitation. No full topology/source qualification is claimed.
- Run affected Debug builds and relevant test classes/assemblies, Node configuration tests and TypeScript checking, plus the existing warning/submodule guard. Preserve commands/results in tests/five-test-harness-deferred-items-2026-10-21/. Preserve tracked file line endings; new artifacts use CRLF.

- Initial verification: Client identity class 176/176, full UI assembly 387/387, full CI assembly 188/188, seven executable Playwright configuration cases and TypeScript pass. Four disposable NuGet restores verify configuration, RestorePackagesPath, NUGET_PACKAGES and empty NUGET_PACKAGES using the actual project metadata declarations.
- Full PlatformApiPrerequisitesTests initially yields 51/52: unchanged FinalDependencyReceiptsMatchTheSelectedPackageAndSourceGraph expects Builds fff4a212180d8cd1c5c0759655a9bc4fb6963c45 while the committed gitlink is 2744d549270298cfe73679f76b477a7368429006. No gitlink is changed by this work. Baseline reproduction and focused final checks are recorded separately.

- Independent review found seven gaps. All are patched: resolve NuGet fallback folders through NuGetPackageFolders; replace the initial property-only probe with a maintained real-package/compiled-assembly/actual-resolver fixture; wire that fixture into CI; skip braces and unrelated JSON in MSBuild diagnostics; restore bounded project-specific parse diagnostics; add successful managed-server startup/response/cleanup proof; update the primary development guide.
- New test-only RestoredFrontComposerAssets.cs owns the shared asset lookup used by the actual UI guard and compiled fixture. The fixture uses a temporary local nupkg and no external feed. Its five scenarios cover primary-cache selection and an empty primary cache with a populated fallback. The initial property-only evidence is historical and superseded by tests/Hexalith.Parties.UI.Tests/verify-restored-assets.py.
- Final focused prerequisite checks pass 20/20; full Client 332/332, UI 387/387 and CI 188/188 total 927 .NET cases. The eight Node configuration checks also pass on the declared minimum Node 24.0.0. A separate runtime-empty-NUGET_PACKAGES guard run passes 4/4. These counts exclude the pre-existing broad prerequisite mismatch, and do not claim full solution/source/topology qualification.

## Review Triage Log

- Medium, patch: primary NuGetPackageRoot could miss a restored fallback package. Search the evaluated NuGetPackageFolders; an empty-primary fallback fixture proves actual asset lookup.
- Medium, patch: item evaluation alone did not prove generated metadata or lookup. The maintained fixture restores actual package bytes, compiles the project's exact metadata and production-used test resolver, then runs the assembly and reads those bytes.
- Medium, patch: recorded manual cache checks lacked continuing enforcement. The existing required accessibility CI job now invokes the maintained compiled restore fixture, and Ci.Tests protects that command.
- Medium, patch: diagnostic braces could precede the graph payload. The parser skips malformed brace candidates and unrelated JSON until it finds a complete Properties/Items object; regression prefixes cover both.
- Low, patch: parse errors lost project context. Include the relative project and at most 1000 characters of diagnostic text; a regression bounds the message and asserts its useful context.
- Low, patch: occupied-port refusal had no managed success control. The real Playwright runner now starts a lightweight fixture, asserts its response and releases its port; the test binds that port again to verify cleanup.
- Low, patch: the primary development guide omitted configuration checks. Its browser sequence now includes test:config and the compiled restore fixture.

- Follow-up review found that recovery could accept a nested graph from malformed JSON. Candidate roots are now independent line-start objects; malformed roots stop parsing, and unrelated valid objects are skipped whole. The exact reported incomplete nested payload is rejected. The maintained fixture also selects Release under CI, retaining Debug locally, and CI=true verification passes all five scenarios.
- Reviewer confirmed both follow-up fixes and found no remaining introduced defects. Final selected checks are 22/22 prerequisite cases, full Client 332/332, UI 387/387 and CI 188/188: 929 .NET cases, no skips. Eight Node checks pass on Node 24.0.0; all five compiled restore scenarios pass locally and under CI. The pre-existing baseline Builds receipt mismatch and HXR011 source-launcher limit remain explicitly recorded.
- Follow-up triage, medium/patch: malformed graph recovery could accept a nested object. Stop at a malformed independent root and skip complete unrelated objects atomically; the regression rejects the reviewer's exact payload.
- Follow-up triage, low/patch: the new compiled restore probe initially emitted Debug assets in CI. Select Release when CI is set and execute that matching assembly; all five scenarios pass with CI=true.
