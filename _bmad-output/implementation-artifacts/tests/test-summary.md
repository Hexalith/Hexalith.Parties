# Test Automation Summary

Story: 1.10 - Deploy parties-ui container/K8s with production-KMS prerequisite gate

## Generated Tests

### API / Deploy Validation Tests
- [x] `tests/Hexalith.Parties.DeployValidation.Tests/ValidateDeploymentLintToolingTests.cs` - added `WorkloadChecksRejectDaprAnnotationsOnPartiesUi`, proving static deploy validation rejects `parties-ui` when Dapr annotations are present.
- [x] `tests/Hexalith.Parties.DeployValidation.Tests/ValidateDeploymentLintFitnessTests.cs` - pinned the forbidden non-Dapr workload contract in `deploy/validate-deployment.ps1`.

### E2E Tests
- [x] Existing Playwright a11y/E2E workspace typechecked successfully.
- [ ] Browser execution was attempted, but this sandbox cannot bind the local Kestrel socket required by Playwright webServer startup.

## Coverage

- Deploy validator workload categories: 9/9 covered.
- Story 1.10 `parties-ui` deploy contracts covered: container metadata, ServiceDefaults wiring, generated folder/image, ingress route, OIDC `secretKeyRef`, image pull secret, health probes, forbidden Dapr annotations.
- New negative coverage: `parties-ui` with any `dapr.io/*` pod-template annotation fails static deploy validation.

## Validation

- [x] `dotnet build src/Hexalith.Parties.UI -c Release -m:1`
- [x] `dotnet build tests/Hexalith.Parties.UI.Tests -c Release -m:1`
- [x] `dotnet build tests/Hexalith.Parties.DeployValidation.Tests -c Release -m:1`
- [x] Direct xUnit: `Hexalith.Parties.DeployValidation.Tests` - 83 total, 0 failed, 3 skipped live-cluster tests.
- [x] Direct xUnit affected UI classes - 6 total, 0 failed.
- [x] `pwsh deploy/validate-deployment.ps1 -ConfigPath deploy/dapr -K8sPath deploy/k8s/`
- [x] `pwsh deploy/validate-deployment.ps1 -ConfigPath deploy/dapr -K8sPath deploy/k8s/ -Format json`
- [x] `bash scripts/check-no-warning-override.sh`
- [x] `cd tests/e2e && npm run typecheck`
- [ ] `cd tests/e2e && npm run test:a11y` - blocked by sandbox socket permission: Kestrel fails with `System.Net.Sockets.SocketException (13): Permission denied`.

## Checklist Result

- API tests generated: yes.
- E2E tests generated: existing UI E2E gate retained; no new browser workflow was required for this deploy-only story.
- Tests use standard framework APIs: yes, xUnit v3, Shouldly, Playwright.
- Happy path covered: yes, valid deploy tree and real `deploy/k8s` validation pass.
- Critical error cases covered: yes, including forbidden `parties-ui` Dapr annotations and invalid ingress/secret/probe/image cases.
- Tests independent: yes.
- No hardcoded waits or sleeps added.
