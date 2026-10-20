---
title: 'DW-67 resolve central consumer support versions'
type: 'bugfix'
created: '2026-10-20'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The consumer-package validator still packs FrontComposer at 1.7.0 and Tenants at 2.4.2 rather than the evaluated dependency versions. The obsolete support artifacts do not represent the current package graph and may be bypassed during consumer restore.

**Approach:** Resolve both versions through the existing evaluated MSBuild property seam, pass them through support packing, and require exact generated package identities and versions. Preserve the existing Commons/EventStore checks and package-only consumer behavior.

</frozen-after-approval>

## Implementation Notes

- DW-67 is the next bounded packaging item after DW-65; DW-66 is already done. Independent investigation confirms four obsolete constants. The evaluated Release Client graph selects Commons 2.30.1, EventStore 3.125.1, FrontComposer 4.6.0 and Tenants 5.8.0. No dependency selection is changed.
- Route assessment: no unresolved intent gap, irreversible implementation action, new public API, or runtime behavior change. Small footprint: two Python scripts and existing CI behavior probes, plus this spec, the ledger resolution and validation evidence. Use the oneshot route; the user separately authorized commit/push.
- `scripts/msbuild_properties.py`: add FrontComposer and Tenants wrappers matching existing central-version resolvers. Reuse validation, failure translation and timeout behavior without fallback constants or XML parsing.
- `scripts/validate-consumer-package-references.py`: replace hard-coded version tuples with separate project inventories. Require all four resolved family versions in support selection/packing, resolve before modifying the disposable workspace, and reuse exact package filename/nuspec validation for the two newly resolved families.
- `tests/Hexalith.Parties.Ci.Tests/CommonsHttpRestoreRoutingTests.cs`: extend existing executable Python probes to observe evaluated resolver wiring, all eight support projects, main-path propagation and actual pack arguments. Reject missing and stale generated FrontComposer/Tenants packages. Exercise changed central values and resolution failures before workspace deletion. Preserve existing Commons/EventStore assertions.
- Architecture constraints: retain root-only dependencies, package-only isolated consumers, Release packaging, private consumer cache and existing feed policy. DW-69 feed isolation is separate work. No AppHost, UI, domain persistence, public contract, package version, gitlink, or historical qualification changes.
- Validation: required Aspire start/wait/describe and stop; focused Debug CI project build with warnings as errors, direct xUnit v3 class and complete affected assembly; Python syntax and warning/whitespace guards. Record commands, full baseline identifier and outcomes in `tests/dw-67-central-consumer-support-versions-2026-10-20/validation.json`. Synthetic packaging tests avoid publishing or altering real feeds. Package-consumer Release execution remains a distinct CI gate.

- Review corrections: documented that the eight-project local inventory covers direct support dependencies, while configured feeds supply transitive packages such as FrontComposer.Contracts.UI. The imported-property fixture now has a 245-second enclosing budget for eight individually bounded MSBuild evaluations. Exact-artifact probes additionally reject wrong filenames, wrong nuspec IDs and duplicate archives for all four newly resolved package identities.

## Review Triage Log

- Medium, defer: NuGet normalizes versions such as 6.5.4.0 and 6.5.4+build.9 when naming packages. The unchanged shared exact-version helper already rejects these forms for Commons/EventStore; the reviewer reproduced all six identity/version combinations. Current canonical 4.6.0/5.8.0 selections are unaffected. Shared NuGet identity normalization needs a separate fix covering every family consistently.
- Medium, defer: restored consumer asset validation checks EventStore only. It can accept FrontComposer/Tenants selections that differ from their packed support artifacts. This gate was absent in the baseline and DW-67 preserves existing consumer restore behavior; add family-wide restored-graph qualification as separate hardening.
- Low, patch: the inventory's transitive boundary was unclear. The source now explicitly documents that direct support dependencies are packed locally and remaining dependencies, including FrontComposer.Contracts.UI, resolve from configured feeds. No full transitive closure is claimed.
- Low, patch: eight sequential MSBuild evaluations shared the generic 30-second Python budget. The fixture now permits eight per-evaluation budgets plus output capture; production resolution timeouts remain unchanged.
- Low, patch: exact-artifact rejection probes lacked independent filename, nuspec-ID and duplicate-archive cases for the newly resolved families. All four identities now exercise all three cases alongside existing missing/stale metadata cases.

- Final verification: Debug CI build has zero warnings/errors. Focused tests pass 14/14 and the complete affected CI assembly passes 128/128 without failures/errors/skips/not-run. The first full run's five missing-restored-assets prerequisites were satisfied with the normal solution restore; no test or gate was weakened. Three review corrections are independently verified. Two pre-existing shared-normalization/restore-graph hardening items are recorded in the ledger. Exact commands, baseline reproduction, hashes, sanitized logs and pinned commitlint success are retained in tests/dw-67-central-consumer-support-versions-2026-10-20/validation.json. Full Release package-consumer execution remains an unchanged CI gate.
