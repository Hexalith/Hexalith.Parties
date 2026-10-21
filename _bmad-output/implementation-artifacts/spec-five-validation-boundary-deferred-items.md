---
title: 'Close five deferred validation boundary gaps'
type: 'bugfix'
created: '2026-10-21'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Five deferred gaps leave commit validation dependent on a floating workflow, browser tests typed against a newer Node major than their supported runtime, malformed registry prereleases accepted as evidence, and identity admission/provisioning boundaries without direct production-path assertions.

**Approach:** Resolve DW-40, DW-125, DW-133, DW-137 and the unnumbered malformed-prerelease follow-up from spec-five-package-validation-deferred-items together. Pin the existing reviewed Builds validation workflow, retain the Node 24 floor with matching declarations, reject empty prerelease identifiers, and exercise real admission scope construction plus registered provisioning dispatch and enriched results. Verify, independently review, reconcile the ledger, commit and push to main as requested.

</frozen-after-approval>

## Implementation Notes

- User authorized five deferred items and push to main. Starting clean main: 63cd1279a82df47af3d16dd0ff9f002f3e4c86eb. The explicit batch sets scope. Small reversible workflow/declaration/test corrections; no unresolved intent gaps, public API changes, persisted data mutation or deployment. Use oneshot.
- Code map: .github/workflows/commitlint.yml currently selects @main. Reuse the existing CI validation revision 332b2c144defba546fe0a09626a4a72f2340e22f, whose reusable workflow supports the existing pull-request-title input. Add its assertion to the existing CI workflow test class.
- Code map: tests/e2e/package.json and package-lock.json select Node 26 declarations despite engines >=24 and required CI Node 24. Select the registry-verified 24.19.2 declarations and update the locked-version theory. Run actual browser workspace typecheck/config checks under Node 24 where available; preserve runtime floor and other dependency selections.
- Code map: .github/workflows/release.yml registry-floor jq validates before excluding prereleases. Require nonempty dot-separated prerelease identifiers, retaining alphanumeric/hyphen semantics and the existing numerical stable-floor comparison. Extend ReleaseRegistryFloorTests with invalid leading/trailing/repeated dots and valid dotted/hyphenated controls; execute the actual workflow shell offline.
- Code map: src/Hexalith.Parties/Authorization/PartyIdentityAuthority.cs constructs command and query scopes; existing PartyIdentityAdmissionTests primarily exercise provider races. Add focused tests using real RSA-backed IdentityAdmissionProof and PartyIdentityAuthority with exact independently constructed expected scopes, malformed payloads, absent/forged proofs and each cross-scope mismatch. Do not replace the authority under test.
- Code map: AddParties registers the party-keyed IAsyncDomainProcessor. Resolve that production registration with real admission proof/authority, send serialized ProvisionAgentParty without Authorization, and assert ordered creation/display/marker events plus exact immutable result identity/source/revision. Fold returned events and retry through the same registered processor to assert original result without new events; reject missing/foreign evidence. External persistence/protection seams may be isolated; this proves domain dispatch, not event-store persistence.
- Given valid signed scope, real admission succeeds; changed tenant/domain/target/operation/request/logical identity/digest is denied. Given unsigned serialized provisioning, dispatch rejects; signed valid provisioning emits three ordered events and result revision 3, and exact replay retry preserves that result. Given malformed prerelease identifiers, registry-floor validation fails before success output; valid prereleases retain existing exclusion behavior.
- Record exact commands/results under tests/five-validation-boundary-deferred-items-2026-10-21/. Run Debug project builds with local source references, focused xUnit v3 classes and complete affected assemblies where feasible, browser typecheck/config checks and repository guards. Preserve broad-gate blockers separately from passing focused evidence.
- Required Aspire baseline: started the selected Parties AppHost in isolation using its existing build, described resources (security unhealthy; dependent resources waiting), then stopped that AppHost successfully before building. No topology qualification claimed.

- Implementation completed all five distinct items; DW-152/DW-156 are duplicate aliases reconciled with DW-133/DW-137. Only Node declarations and their undici-types dependency changed in the browser lock; runtime floor is preserved.
- Build observation correction: the initial full service run passed 2207/2208 but exposed pre-existing current gitlink/catalog drift. Verified all ten committed roots/checkouts/describes, added a new dated current observation, updated only current fitness constants and Parties catalog expectation, and preserved the old observation bytes apart from its historical heading. No dependency pointer/version or approval changed. Final complete service suite passes 2214/2214; CI passes 246/246; both without skips. The extra six cases come from review improvements.
- Actual validation graph: bare legacy source switches are inert without module-tool mapping. Debug builds and tests qualify the repository default published graph; no local source-mode or full topology qualification is claimed.
- Final evidence: tests/five-validation-boundary-deferred-items-2026-10-21/validation.json records exact commands and results. Node 24 typecheck and 8/8 config checks pass; both Debug builds have zero warnings/errors, repository guard passes, and pinned commitlint 21.2.2 validates the full exact commit candidate. Independent follow-up review passes 33/33 identity cases and the receipt method, with no remaining material introduced issue.

## Review Triage Log

- Low, patch: malformed proof failed before cryptographic verification. Added correctly scoped evidence signed by a foreign RSA key at both admission boundaries.
- Low, patch: static valid proofs omitted query expiry/future-issue denial. Added validly signed expired and future-issued command/query cases against the fixed clock.
- Low, patch: null-state rejection could not demonstrate admission before protected-state access. Supply an opaque protected snapshot and assert neither unprotect API is invoked.
- Low, patch: identical signed source and transport user masked provenance substitution. Give them distinct values and retain exact expected trusted source assertions.
- Low, patch: retry at original source/position masked recomputed provenance/revision. Retry with a new transport user, new signed source and a later valid snapshot containing a renamed display name; preserve original identity result and later state.
- Low, patch: malformed payloads also lacked proof. Attach valid signed evidence scoped to each delivered payload digest to isolate parser failure.
- Low, patch: initial matrix rewrite normalized pre-existing mixed line endings. Restore original bytes outside the inserted current observation and historical heading rename; independent comparison confirms preservation.
- Low, patch: correcting gitlinks exposes stale current Parties catalog expectation. Update 1.5.0 to the evaluated/selected 1.5.1 without altering historical catalog expectations.
