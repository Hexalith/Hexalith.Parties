---
title: 'Close five deferred package validation gaps'
type: 'bugfix'
created: '2026-10-21'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Four open DW-67/DW-69 follow-ups leave consumer validation sensitive to NuGet version spelling, unqualified restored FrontComposer/Tenants selections, inherited fallback packages and disabled-feed settings. DW-188 leaves the release registry-floor comparison untested at execution time.

**Approach:** Close these five deferred items together, as requested. Compare support and restored versions using NuGet identity normalization, qualify every selected FrontComposer/Tenants family package and require the portal's direct support dependencies, clear inherited fallback/disabled-feed settings, and execute the actual registry-floor workflow with isolated registry fixtures. Verify and independently review the changes, reconcile the five ledger entries, then commit and push to main.

</frozen-after-approval>

## Implementation Notes

- User explicitly authorized a five-item batch and push to main. Starting revision: 1bed6c139f7699bc717182763c04099ea75abc5b; clean main. Four entries are unnumbered follow-ups from completed DW-67 and DW-69; DW-188 is the fifth. DW-92 stays open because its broad effective-version lookup consolidation is separate work.
- Route assessment: small reversible script/test/documentation corrections, no unresolved intent gap, new public API, dependency upgrade, data mutation or deployment. Push is already authorized. Use oneshot.
- Code map: scripts/validate-consumer-package-references.py owns validate_support_packages, write_nuget_config, validate_event_store_assets, validate_consumer and main. Reuse evaluated MSBuild version resolvers and private caches; preserve all eight support IDs, strict artifact inventory, package-only consumers and Release build behavior. Normalize zero fourth components and metadata consistently; preserve meaningful revisions and prerelease identity.
- Generalize restored-family validation while keeping the EventStore wrapper and diagnostics. Require FrontComposer Contracts/Shell and Tenants Client/Contracts for portal consumers. Client consumers have no new required IDs but any selected family package must match its evaluated version. Reject stale transitive siblings, case variants, missing direct packages and project substitutions before build.
- Code map: tests/Hexalith.Parties.Ci.Tests/CommonsHttpRestoreRoutingTests.cs contains bounded Python probes for support artifacts, main-path wiring, asset selection and actual offline NuGet restores. Extend these with normalization cases, mismatched assets, poisoned fallback/disabled parent configuration, positive controls and separate caches. Use actual SDK pack/restore output to qualify normalization.
- Code map: .github/workflows/release.yml contains the registry-floor bash and embedded jq expression; add ReleaseRegistryFloorTests.cs to execute this exact tracked step with fake gh/curl, synthetic manifests and published-version indexes. Cover numeric ordering, equality, prerelease exclusion, four-part versions, malformed payloads, missing indexes and API/transport failures; no network or publication.
- Given equivalent NuGet spellings, support packages and restored selections pass; changed revisions/prereleases/versions, wrong filenames/IDs and duplicates still fail. Given undeclared fallback packages, restore fails for the missing package; explicitly chosen feeds still restore when parent configuration disables their generated keys. Given stable registry versions above the reachable release tag floor, the workflow fails without emitting a success output; equality/below/prerelease-only fixtures pass.
- Update docs/ci.md to describe actual source, fallback and disabled-feed isolation and restored version qualification. Record exact commands and outcomes under tests/five-package-validation-deferred-items-2026-10-21/. Run focused Debug CI build, direct xUnit v3 cases and the full affected assembly, Python syntax and repository guards. Preserve full starting Git identity and narrow baseline failure evidence. No full topology or complete Release package-consumer qualification is implied by synthetic tests.

- Implementation complete: normalized archive and restored-family identities, clear inherited fallback/disabled settings, four maintained Python regression methods exposed by three required CI theory cases, and 40 executable registry-floor fixtures. Initial consumer class passes 37/37; CI builds report zero warnings/errors. Baseline independently accepts stale FrontComposer/Tenants assets, restores from inherited fallback storage and suppresses explicit feeds.
- Required existing-build Aspire baseline started in isolation; describe showed downstream resources waiting on the external security endpoint. The selected Parties AppHost was stopped through Aspire before builds; another repository's running AppHost was left untouched. This is not fresh topology qualification.

## Review Triage Log

- Medium, patch: inherited NUGET_FALLBACK_PACKAGES bypasses config clearing. Reproduced through run_dotnet with a seeded cache and fresh primary cache; remove the inherited environment key and require offline missing-package failure plus explicit-source success. The regression fails before the correction.
- Medium, patch: identical family versions hide swapped validation arguments. Use distinct EventStore, FrontComposer and Tenants versions while retaining zero-revision and metadata normalization controls.
- Low, patch (shared tag-fixture cause): single-tag fixtures cannot test highest reachable floor selection. Add numerically ordered multi-tag fixtures with higher unreachable and lower reachable candidates.
- Low, patch (shared tag-fixture cause): missing pagination coverage permits omitted tag pages. Require --paginate and supply multiple response pages, plus a pagination-sensitive fixture.
- Medium, defer: the existing jq prerelease regex accepts malformed 3.16.0-a..b and 3.16.0-. then filters them out. Confirmed old workflow behavior; record a separate fail-closed grammar correction without changing this batch's stable comparison contract.
- Low, patch: manifest payload fixture does not cover independently checked API envelope failures. Add wrong path, encoding, missing content and invalid-base64 fixtures.
- Low, patch: fake curl ignores --write-out content. Require the real %{http_code} format and a destination before emitting a simulated status.
- Low, patch: pack-only SDK probe leaves restored normalization synthetic. Restore every generated normalized archive and qualify its actual project.assets.json family selection.
- Low, patch: baseline reproduction command/source evidence is incomplete. Retain executable probes bound to the full starting Git revision and record their exact invocations; distinguish new-signature errors from the separate baseline behavioral proof.
- Low, patch: disposable .NET fixtures fall outside the repository SDK hierarchy. Copy global.json into each new workspace and assert/print that its SDK selection matches the repository before real package generation.

- Follow-up implementation: run_dotnet removes inherited NUGET_FALLBACK_PACKAGES. The offline fixture invokes the actual runner with bounded captured subprocesses; both configuration and environment fallback cannot satisfy missing packages, while explicit sources succeed. Distinct family versions pass; real SDK pack plus restore covers all five normalized forms on SDK 10.0.402. All four Python methods pass.
- Registry fixtures now cover 47 cases, including numeric multi-tag reachability, two response pages with enforced --paginate, malformed manifest API envelopes and enforced curl status format. Four isolated author-side mutations are detected; the parent runs the compiled class with the full assembly.
- Withdrawn initial review observation, false: an empty disabled local feed would evade the fixture. The fixture already asserts the complete effective restored-source set, including that feed, so disabling it is detected. Reviewer withdrew the claim after inspecting that assertion.

- Final verification: Debug CI build has zero warnings/errors; the full affected CI assembly passes 238/238 with no errors, failures, skips or unexecuted tests. Warning/submodule and Python syntax guards pass. Follow-up independent review verifies 4/4 Python regressions, 47/47 registry fixtures, both executable baseline probes and SDK 10.0.402, reporting no remaining material introduced issue. The pre-existing malformed-prerelease grammar gap is retained as separate deferred work. Pinned commitlint 21.2.2 validates the exact full commit candidate stored with the evidence.
