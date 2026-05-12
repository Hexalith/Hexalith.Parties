---
project_name: Hexalith.Parties
user_name: Jerome
date: 2026-05-12
change_scope: Minor
status: implemented
---

# Sprint Change Proposal: xUnit v3 Migration

## 1. Issue Summary

The test stack still used xUnit v2 package IDs and central versions even though the current course correction is to use xUnit v3. The change was identified during sprint execution as a tooling alignment issue, not a product-scope change.

## 2. Impact Analysis

- Epic impact: No functional epic scope changes.
- Story impact: Test implementation guidance should now assume xUnit v3 package IDs.
- Artifact conflicts: `Directory.Packages.props`, root test project package references, `project-context.md`, and the architecture dependency table required updates.
- Technical impact: Test projects should reference `xunit.v3` while keeping `xunit.runner.visualstudio` on its existing 3.x runner version. xUnit v3 also surfaces existing `xUnit1051` cancellation-token analyzer debt across more projects, so that debt remains explicitly suppressed for root test projects and tracked as follow-up hardening.

## 3. Recommended Approach

Direct adjustment. Update central package management and root test project references in place, then run restore/build/test verification. This is a minor dependency migration and does not require backlog restructuring.

## 4. Detailed Change Proposals

### Central Packages

OLD:

```xml
<PackageVersion Include="xunit" Version="2.9.3" />
<PackageVersion Include="xunit.assert" Version="2.9.3" />
```

NEW:

```xml
<PackageVersion Include="xunit.v3" Version="3.2.2" />
<PackageVersion Include="xunit.v3.assert" Version="3.2.2" />
```

Rationale: xUnit v3 uses `xunit.v3` package IDs, and the v3 assertion package is `xunit.v3.assert`.

### Test Projects

OLD:

```xml
<PackageReference Include="xunit" />
```

NEW:

```xml
<PackageReference Include="xunit.v3" />
```

Rationale: All root-level Parties test projects should use the v3 framework package consistently.

### Analyzer Compatibility

NEW:

```xml
<NoWarn>$(NoWarn);xUnit1051</NoWarn>
```

Rationale: The xUnit v3 migration turns pre-existing cancellation-token guidance into build-blocking analyzer errors across several test projects. Suppression keeps this dependency migration scoped while preserving the deferred-work path to plumb `TestContext.Current.CancellationToken` in future test hardening.

### Planning Context

Update live project context and architecture references from xUnit 2.9.3 to xUnit v3 3.2.2 so future implementation work follows the corrected test stack.

## 5. Implementation Handoff

Scope classification: Minor.

Handoff: Developer implementation completed in the current workspace. Success criteria are package restore, solution build, and at least one focused test project passing under xUnit v3.
