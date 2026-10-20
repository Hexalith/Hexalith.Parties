---
title: 'DW-73 reject publish models without an OIDC client secret'
type: 'bugfix'
created: '2026-10-20'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The retained Parties AppHost substitutes an empty OIDC client secret during publishing, producing artifacts whose Parties UI cannot authenticate.

**Approach:** Fail publish model construction when PartiesUi:OidcClientSecret is missing, empty, or whitespace. Name the configuration key and environment variable in the error without including its value. Preserve valid secret bytes and local Keycloak behavior. Verify actual publish and run models, then commit and push as requested.

</frozen-after-approval>

## Implementation Notes

- Selected DW-73 after completed DW-72. This is a small continuity fix, with no unresolved intent gap, irreversible implementation action, public API, dependency update or sprint story transition; route is oneshot.
- Parent: e1db1b61de4d47336e474d41eed18b68d43d7377 on clean main. Earlier user edits were committed and pushed independently before this task resumed; preserve that commit unchanged.
- Read-only investigation delegated through bmad-build. Program.cs validates only publish mode immediately after CreateBuilder, before resolving dependencies or registering resources. Reuse the validated value in the existing UI environment assignment.
- Add model-only integration coverage under tests/Hexalith.Parties.IntegrationTests/Topology/, using the actual operation=publish and pinned testing-builder API. Reject absent/empty/whitespace values with actionable errors across default/docker/k8s/aca; verify exact nonblank secret forwarding and local behavior. Keep existing publish environment and UI topology checks intact.
- docs/deployment-guide.md documents the required key/environment variable and early failure; retain external runtime ownership. Mark DW-73 complete only after validation and independent review.
- Baseline isolated Aspire start used existing builds. Parties became healthy in 13.2 seconds; EventStore and UI were healthy. Admin and Tenants exited 134; explicit-start workloads were NotStarted. The owned AppHost stopped successfully before code edits. This is baseline evidence, not complete topology qualification.
- Validate the Debug/source integration test project, new model cases and adjacent model regressions; run warning and whitespace guards. Preserve exact commands/results and pinned commitlint evidence under tests/dw-73-publish-oidc-secret-2026-10-20/. Complete independent blind review before commit and push.

- Implemented early publish validation and reused the validated value in the existing UI assignment. Existing source-file newline styles were preserved; new C# and spec files use CRLF.
- Final Debug/source integration and service builds pass with zero warnings/errors. The full service suite passes 2004/2004; focused model checks pass 40/40, including 29 new cases. All 29 new cases also pass with a conflicting ambient secret and invalid ambient publish target. No failures, errors, skips or not-run cases remain in these checks.
- The initial build caught CA2007 on asynchronous test-builder disposal. Configured disposal fixed the errors without suppressions. Hostile environment testing then exposed default providers overwriting fixture values; explicit provider ordering corrected the fault. Added normal-defaults environment-input coverage and enabled/disabled Keycloak cases.
- Post-change isolated local run with an explicitly empty publish secret has healthy Parties and UI. Other participants exited 134, so full topology qualification is not claimed. The owned AppHost stopped successfully. No release artifact was deployed, and no dependencies or gitlinks changed.
- Exact commands/results, source identities and pinned commitlint 21.2.2 evidence are in tests/dw-73-publish-oidc-secret-2026-10-20/. Independent follow-up review found no introduced defect.

## Review Triage Log

- Medium, patch: default configuration providers could override fixture secrets and expose an ambient value in assertion output. Confirmed with a placeholder environment secret (24 initial failures). Disabled automatic defaults and explicitly ordered environment/CLI before fixture overrides; the final 29 cases pass under the conflicting environment.
- Low, reject: add separate automated tests of the fixture's ambient isolation. The observed defect is fixed and its exact repeatable hostile-environment validation command is retained. Additional fixture/process scaffolding would exceed a simple correction and adds little normal-use value.
- Low, patch: in-memory cases did not verify the documented environment-only input. Added an actual publish-model case using normal providers and a saved/restored environment variable in the non-parallel collection.
- Low, patch: local preservation cases forced Keycloak on. Expanded run coverage to include Keycloak disabled, with absent, empty, whitespace and populated production secrets.
- Low, patch: deployment wording could confuse the AppHost input and the external orchestrator's UI runtime setting. Named retained-AppHost publishing and documented the runtime Authentication__OpenIdConnect__ClientSecret mapping.
- Follow-up: the reviewer verified all corrections and found no introduced defect. No new deferred item is required.
