# Story 3.9: Add Deployment Security Validation

## Status

Done.

## Summary

Extended `deploy/validate-deployment.ps1` with blocking deployment security checks for production authentication, tenant identity, and transport posture:

- Authentication validation now requires JWT issuer, audience, signing-key secret reference metadata, and `failClosed: true`.
- Tenants integration validation now requires tenant identity to come from authenticated credentials and authoritative metadata, not request payloads.
- Transport validation now requires production HTTPS/TLS, DAPR mTLS, and a disabled local HTTP exception.
- Output remains bounded and safe for logs by reporting field names and unsafe categories instead of operator-supplied token, signing-key, claims, tenant membership, or personal-data values.

## Files Changed

- `deploy/validate-deployment.ps1`
- `deploy/dapr/topology.yaml`
- `deploy/dapr/tenants-integration.yaml`
- `docs/deployment-guide.md`
- `docs/deployment-security-checklist.md`
- `tests/Hexalith.Parties.DeployValidation.Tests/DeploymentValidationTests.cs`
- `tests/Hexalith.Parties.DeployValidation.Tests/TenantsDeploymentValidationTests.cs`
- `tests/Hexalith.Parties.DeployValidation.Tests/K8sStory93LintTests.cs`

## Validation

- Passed: `pwsh -NoProfile -ExecutionPolicy Bypass -File deploy\validate-deployment.ps1 -ConfigPath deploy\dapr -Output json`
- Passed: `dotnet test tests\Hexalith.Parties.DeployValidation.Tests\Hexalith.Parties.DeployValidation.Tests.csproj --filter "FullyQualifiedName~DeploymentValidationTests"` (36/36)
- Attempted: `dotnet test tests\Hexalith.Parties.DeployValidation.Tests\Hexalith.Parties.DeployValidation.Tests.csproj` timed out after 10 minutes in this environment.

## Notes

The full project test timeout happened after the focused deployment validation coverage passed and after resolving the compile-time `CA2007` analyzer issue in `K8sStory93LintTests`.
