# Test Automation Summary

## Generated Tests

### API Tests
- [x] `tests/Hexalith.Parties.Contracts.Tests/PartyExportFileNameTests.cs` - Covers canonical `party-{partyId}-{yyyyMMddTHHmmssZ}.json` export filenames, UTC conversion, unsafe character replacement, token bounding, and fallback behavior.
- [x] `tests/Hexalith.Parties.Contracts.Tests/PartiesTextHeuristicsTests.cs` - Covers centralized tenant-text heuristic positives and negatives.
- [x] `tests/Hexalith.Parties.Contracts.Tests/AdminPortal/AdminPortalGdprPrivacyGuardrailTests.cs` - Covers privacy guardrails for helper surfaces and filename metadata.
- [x] `tests/Hexalith.Parties.Client.Tests/AdminPortal/AdminPortalGdprOperationContractTests.cs` - Covers shared admin GDPR HTTP outcome mapping and export filename behavior.
- [x] `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs` - Covers AdminPortal export UI behavior, hostile transport filenames, and the approved removal of the old `-export-` shape.

### E2E Tests
- [x] `tests/e2e/specs/admin-parties-list.spec.ts` - Updated the erased-party GDPR export workflow to require the canonical timestamped filename and explicitly reject the old `party-{party}-export-{timestamp}Z.json` shape.

## Coverage
- GDPR outcome mapping: representative status mappings covered in Client tests.
- Tenant heuristic: positive/negative cases covered in Contracts tests.
- Export filename format: Contracts helper, Client export, AdminPortal component, and Playwright workflow assertions covered.
- Privacy guardrails: filename and rendered/export metadata assertions cover no PII/raw detail leakage.

## Validation
- `npm run typecheck` in `tests/e2e` passed.
- `git diff --check` passed.
- Direct xUnit v3 runner passed `PartyExportFileNameTests`, `PartiesTextHeuristicsTests`, and `AdminPortalGdprPrivacyGuardrailTests`: 16 total, 0 failed.
- Direct xUnit v3 runner passed `AdminPortalGdprOperationContractTests`: 26 total, 0 failed.
- Direct xUnit v3 runner passed AdminPortal `*PortabilityExport*` tests: 5 total, 0 failed.
- Focused Playwright execution was attempted but blocked by sandbox socket restrictions while starting Kestrel: `System.Net.Sockets.SocketException (13): Permission denied`.
- `dotnet test` was also blocked by the same sandbox class while creating the .NET 10 test named pipe.

## Checklist
- [x] API tests generated where applicable.
- [x] E2E tests generated where UI exists.
- [x] Tests use standard framework APIs.
- [x] Tests cover happy paths.
- [x] Tests cover critical error/privacy cases.
- [x] Tests use semantic locators in Playwright.
- [x] Tests have clear descriptions.
- [x] No hardcoded waits or sleeps were added.
- [x] Tests are independent.
- [x] Test summary created.
- [x] Tests saved to appropriate directories.
- [x] Summary includes coverage metrics.
