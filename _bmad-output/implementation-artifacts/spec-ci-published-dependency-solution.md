---
title: 'Build CI and releases against published dependencies'
type: 'bugfix'
created: '2026-10-28'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="operator requested CI fixes and release without human checkpoints">

## Intent

**Problem:** Parties CI builds external diagnostic source projects from the development solution, causing concurrent EventStore output writes and mixed Debug/Release dependencies. Consumer verification also repacks external source instead of validating the published dependency graph.

**Approach:** Select the existing complete standalone solution for CI and release preparation. Restore consumer support dependencies from configured package feeds by default, retaining explicit source-support packing for diagnostics. Keep all test tiers, warnings-as-errors, exact-source release proof and publication validation active.

</frozen-after-approval>

## Implementation Notes

- Investigation: latest CI run 40586521470 failed with MSB3026 in EventStore Client and Server. StandaloneSolutionTests already proves the standalone solution includes every owned project.
- Change the CI/release solution inputs, keep the development solution intact, and make source-support packing an explicit consumer-validator option.
- Update workflow contract tests and the central-version exception test for the existing FrontComposer.Contracts.UI catalog alias. Exercise package-only Release restore/build and consumer checks, then request independent review.
- Git synchronization, owner-authorized release approval and actual publication are separate authorized operator actions performed after this code fix.

- Local standalone Release restore/build passed with zero warnings and errors; ten packages packed and validated, and two isolated consumers restored and built against public dependencies.
- Independent review identified three fixes: remove stale support-feed expectations, validate restored Commons family identities/versions, and correct active parity commands. All addressed.
- Full CI contract execution exposed a pre-existing source-only browser case inside the mandatory portal suite. Moved the unchanged cancellation scenario to an explicit source-qualification spec; absence of its capability now fails instead of skips. Required package browser suite remains unconditional.

## Review Triage Log

- medium, patched: default source test expected a removed support feed; it now requires only the Parties feed plus explicitly configured sources, and rejects source packing.
- medium, patched: published consumers did not validate Commons family versions; exact version/package checks now run before build with missing, wrong-version and project-substitution negative controls.
- low, patched: active docs parity commands still selected the diagnostic development solution; commands and policy now select the standalone package solution.

- Review follow-up: explicit source browser qualification now has its own config and is excluded from default discovery. Client optional-family negative fixtures include required Commons packages and assert the offending family so they cannot pass for the wrong reason.

- Final local verification: 464 CI contracts passed, zero failures/skips; Release standalone build had zero warnings/errors; ten package archives and both published-dependency consumers passed. Browser configuration passed 8 tests; typecheck and default/explicit source discovery passed. Remote full CI and actual release publication remain operator execution steps.

- Follow-up full service tests exposed stale current-selection receipts and EventStore package skew against the refreshed Tenants runtime graph. Select published EventStore 3.128.3, matching the catalog/runtime roots; update current source identity constants and add a dated matrix observation while preserving historical qualification receipts. Current Aspire technology tables now reflect 13.6.4.
- Public API snapshot adds only existing ProcessingActivityPage and GetProcessingRecords records from prior source commit 0455c28a8604bf68d7065d36977c1c2ccf13e2e7; the remaining 225 Contracts controls were unchanged and all 226 passed after the refresh. Independent review confirmed no API removal or hidden change.

- Clean-run follow-up: ClientPackageTests now packs only owned Contracts/Client artifacts and restores published external dependencies, removing latent dependencies on pre-existing external source output. Both package probes pass.
- Immutable source proof still checks exact committed and checked-out SHAs. Receipt descriptions now constrain git describe to the tag captured by the observation; raw short-SHA observations use rev-parse. Fetching the new Memories v3.0.1 tag reproduced the previous failure while the anchored v3.0.0 description remained unchanged.
