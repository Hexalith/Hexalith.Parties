---
title: 'DW-72 publish Aspire services with Production defaults'
type: 'bugfix'
created: '2026-10-20'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The retained Parties AppHost explicitly assigns Development environments to published JWT and UI services, making deployment artifacts select development configuration.

**Approach:** Set both ASPNETCORE_ENVIRONMENT and DOTNET_ENVIRONMENT to Production for every published project resource, including optional services. Preserve local run configuration and existing authentication wiring. Verify actual run and publish model construction, complete independent review, then commit and push as requested.

</frozen-after-approval>

## Implementation Notes

- Selected DW-72 after completed DW-71. This is deferred continuity work in the retained AppHost, without a sprint story-key transition. Baseline: 836b2ba4aa41cdf8a7812b97a5da7c08896c0c7c on clean main tracking origin/main.
- Route assessment: no unresolved intent gaps, no irreversible implementation action, and a small AppHost/test/documentation footprint. The user explicitly authorized push. Read-only investigation delegated as directed by bmad-build.
- `src/Hexalith.Parties.AppHost/Program.cs`: remove Development assignments from the JWT publish helper and two UI publish branches. Before building the application, add a publish-only rule over all ProjectResource resources that sets both environment variables. Keep run-mode launch-profile and custom environment values intact.
- `tests/Hexalith.Parties.Tests/FitnessTests/AppHostTenantsTopologyTests.cs`: remove obsolete assertions requiring Development settings.
- `tests/Hexalith.Parties.IntegrationTests/Topology/PartiesPublishEnvironmentTests.cs`: use existing Aspire model-only construction and environment callback evaluation patterns. Construct the actual builder in publish mode; a publish callback context alone does not select publish branches. Require the ten published project names and Production settings on each; exercise local run behavior without launching infrastructure.
- `docs/deployment-guide.md`: document publish defaults and local launch-profile behavior without changing the external deployment ownership boundary. Adjacent DW-73 OIDC secret validation remains separate.
- Baseline Aspire start used existing builds; Parties reached healthy state in five seconds, and the owned AppHost stopped successfully before editing. This is baseline resource evidence, not fresh full-topology qualification.
- Validate Debug/source builds of the two affected test projects, focused AppHost suites, full service assembly and relevant model-only integration tests. Retain commands/results and pinned commitlint validation under `tests/dw-72-production-publish-environments-2026-10-20/`. Broader runtime and Release publication gates remain separate.

- Implementation sets the publish-only environment policy after all project registration. Existing JWT/OIDC assignments are preserved. Local model callbacks contain launch-profile defaults, so the initial run test was corrected to assert those values.
- Initial isolated Debug/source integration and service builds passed with zero warnings/errors. New model checks passed 5/5, existing UI model checks 3/3, AppHost fitness 16/16 and the complete service assembly 1933/1933, without failures/errors/skips/not-run. Rebuilding the original AppHost against the new tests produced exactly four publish failures and one passing local-run case; restoring the change passed again.
- Concurrent work subsequently advanced shared dependency checkouts during final verification. DW-72 files were moved into the isolated checkout, preserving all unrelated main-worktree edits. Root-declared dependency revisions are frozen in detached checkouts at the original committed gitlinks; no dependency source, dependency selection or nested submodule is changed. Final checks will use those private paths.
- Independent review corrected four verification gaps: actual operation during callback evaluation, explicit Staging preservation, default local topology coverage plus optional prerequisite documentation, and ambient publish-target isolation. The follow-up reviewer verified all four corrections and found no remaining introduced issue.

## Review Triage Log

- Medium, patch: run callbacks were evaluated as Publish, hiding run-only overrides. Evaluation now uses the actual builder execution context.
- Medium, patch: run checks covered Development only. Cases now preconfigure both variables as Staging before callbacks and verify preservation, with optional resources both enabled and disabled.
- Low, patch: complete-model checks require optional Memories while the standard setup omits it. tests/README.md documents the additional root-only initialization, and default local cases leave optional resources disabled.
- Medium, patch: null publish target could inherit ambient PUBLISH_TARGET. Every case now supplies an explicit target or empty value and asserts the effective configuration.
- Pre-existing, defer: Memories production authentication is not supplied by the retained publish branch. Pinned Aspire copies launch-profile environment variables only in run mode, so the baseline already defaults Memories to Production. Its production configuration lacks required JWT authority/signing key, issuer and audience; this is separate publish qualification work, not a DW-72 environment regression.

- Final frozen-graph verification passes: Debug/source builds have zero warnings/errors; updated model checks pass 8/8, existing UI model checks 3/3, fitness checks 16/16, and the complete service assembly 1933/1933, with no failures/errors/skips/not-run. All eight new cases also pass with an invalid ambient PUBLISH_TARGET. Healthy local Parties and UI retain Development configuration; the owned isolated AppHost stopped successfully. Other runtime participants were Finished or explicitly NotStarted, so no full-topology qualification is claimed. Exact commands, results, selected dependency identities, source hashes, superseded environment failures and pinned commitlint 21.2.2 evidence are retained under tests/dw-72-production-publish-environments-2026-10-20/.
- DW-72 is complete; one pre-existing Memories production-authentication qualification item is recorded separately. No dependency versions, gitlinks, public contracts or domain code are changed by this work.

- Remote main advanced during finalization. Rebased the DW-72 commit onto bec09f01aa00bd9ef83554fd099ed36fb59ab2c6 and aligned only the private dependency checkouts to that parent's committed selections. Both updated Debug/source builds pass with zero warnings/errors; 8 publish/run cases, 3 existing UI model cases and 16 AppHost fitness checks pass. The updated complete service suite passes 2001/2003 with two inherited documentation/receipt failures. Both fail identically on the exact parent checkout using unchanged test methods with identical source blobs; exact commands/results are retained. The metadata drift is recorded separately without editing its gates or historical approvals. The DW-72 source change is unchanged by the rebase.
