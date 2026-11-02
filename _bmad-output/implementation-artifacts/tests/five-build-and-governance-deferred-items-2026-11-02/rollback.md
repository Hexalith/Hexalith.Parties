# DW-199 rollback exercise — 2026-11-02

The original verification-maintenance commit is `fb07a6cf14507bcae371f957b0272f3f4bbead84`. Its parent is `80f4c8f562b3d663c94f4d0c36cccff6189257ec`. An isolated detached worktree at the parent represented the reverted tree. All ten root-declared submodules were checked out locally at the parent commit's gitlink; no nested submodule was initialized. `git status --porcelain` was empty.

The four relevant root gitlinks were EventStore `bbfd0fada14ce11e137365637238af4fec2f7f7f`, Tenants `73e4cbee3c3d429ee90a81562ceb9060f8a82ce5`, FrontComposer `099299cb5da4ec1ffa3b19cc0387f06d5bbb7634`, and Builds `2744d549270298cfe73679f76b477a7368429006`. `dotnet msbuild src/Hexalith.Parties.Client/Hexalith.Parties.Client.csproj -p:Configuration=Debug -getProperty:HexalithEventStoreVersion,HexalithTenantsVersion,HexalithFrontComposerVersion` returned `3.126.0`, `5.9.0`, and `4.7.0` respectively.

| Command run in the detached parent worktree | Result |
| --- | --- |
| `dotnet build tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj -c Debug -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity quiet` | Exit 0; 0 warnings, 0 errors |
| `dotnet tests/Hexalith.Parties.Tests/bin/Debug/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.Projections.ProjectionPlatformAdapterTests -class Hexalith.Parties.Tests.Projections.PartySdkConfigurationCompatibilityTests -class Hexalith.Parties.Tests.HealthChecks.HealthEndpointIntegrationTests -class Hexalith.Parties.Tests.Gateway.EventStoreGatewayRoutingTests -class Hexalith.Parties.Tests.FitnessTests.ArchitecturalFitnessTests -class Hexalith.Parties.Tests.FitnessTests.DocumentationFitnessTests -class Hexalith.Parties.Tests.FitnessTests.EffectivePackageVersionsTests -class Hexalith.Parties.Tests.FitnessTests.PlatformApiPrerequisitesTests -class Hexalith.Parties.Tests.FitnessTests.EpicEightClosureFitnessTests` | Exit 0; 252 passed, 0 failed/skipped |
| `dotnet build tests/Hexalith.Parties.Ci.Tests/Hexalith.Parties.Ci.Tests.csproj -c Debug -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --verbosity quiet` | Exit 0; 0 warnings, 0 errors |
| `dotnet tests/Hexalith.Parties.Ci.Tests/bin/Debug/net10.0/Hexalith.Parties.Ci.Tests.dll -class Hexalith.Parties.Ci.Tests.CommonsHttpRestoreRoutingTests -class Hexalith.Parties.Ci.Tests.PartiesContainerPublishWorkflowTests` | Exit 0; 86 passed, 0 failed/skipped |

This exercises rollback of the historical test/documentation commit at its original dependency selection. It does not qualify the current source/package graph, topology, deployment, or full Epic 8 closure.

## DW-202 compatible package/source fallback

An isolated detached worktree at the starting Parties revision `d06c316fd57cbfaa0b80cf8622ec0b8ec571c6a0` retained its root-declared dependency checkouts except that EventStore was deliberately set to tag `v3.130.2` (`28e3f260bbaf4619d1a99d656fba04c0c532387b`). The original root package property selected `3.130.2`. The worktree kept the one-line solution fix that removes the direct Admin.UI project, so the TestHost remained the single owner of that native build. `git status --short` showed only that solution edit and the EventStore source override.

| Command run in the isolated fallback tree | Result |
| --- | --- |
| `dotnet msbuild src/Hexalith.Parties.Client/Hexalith.Parties.Client.csproj -p:Configuration=Release -getProperty:HexalithEventStoreVersion` | `3.130.2` |
| `git -C references/Hexalith.EventStore describe --tags --exact-match` | `v3.130.2` |
| `dotnet restore Hexalith.Parties.slnx --verbosity quiet` | Exit 0 |
| `dotnet build Hexalith.Parties.slnx -c Release --no-restore -m:1 --verbosity quiet` | Exit 0; 0 warnings, 0 errors |
| `dotnet tests/Hexalith.Parties.Tests/bin/Release/net10.0/Hexalith.Parties.Tests.dll -class Hexalith.Parties.Tests.Projections.ProjectionPlatformAdapterTests -class Hexalith.Parties.Tests.FitnessTests.ArchitecturalFitnessTests -class Hexalith.Parties.Tests.FitnessTests.DocumentationFitnessTests -class Hexalith.Parties.Tests.Gateway.EventStoreGatewayRoutingTests -class Hexalith.Parties.Tests.HealthChecks.HealthEndpointIntegrationTests` | Exit 0; 130 passed, 0 failed/skipped |

This is a technical fallback control, not a committed or owner-approved I16 identity. It provides no current parity, topology or deployment credit. A production rollback still requires the owner to select and commit the matching source/package pair before use.
