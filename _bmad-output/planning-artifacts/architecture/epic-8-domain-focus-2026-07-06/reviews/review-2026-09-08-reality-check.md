# Reviewer Gate — Reality-Check Review
- reviewer: REALITY CHECK
- date: 2026-09-08
- target: `_bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md`
- mode: VALIDATE-only
- web sources consulted:
  - https://dotnet.microsoft.com/en-us/download/dotnet/10.0
  - https://github.com/dotnet/core/blob/main/release-notes/10.0/10.0.11/10.0.11.md
  - https://devblogs.microsoft.com/dotnet/dotnet-and-dotnet-framework-august-2026-servicing-updates/
  - https://devblogs.microsoft.com/dotnet/introducing-csharp-14/
  - https://github.com/dotnet/csharplang/blob/main/Language-Version-History.md
  - https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/extension-methods
  - https://github.com/dapr/dapr/releases
  - https://blog.dapr.io/posts/2026/06/10/dapr-v1.18-is-now-available/
  - https://docs.dapr.io/operations/support/support-release-policy
  - https://api.nuget.org/v3-flatcontainer/dapr.client/index.json
  - https://www.nuget.org/packages/Dapr.Client/
  - https://github.com/dapr/dotnet-sdk/releases
  - https://github.com/microsoft/aspire/releases
  - https://github.com/microsoft/aspire/releases/tag/v13.5.0
  - https://aspire.dev/whats-new/aspire-13/
  - https://api.nuget.org/v3-flatcontainer/aspire.hosting/index.json
  - https://www.nuget.org/profiles/fluentui-blazor
  - https://api.nuget.org/v3-flatcontainer/microsoft.fluentui.aspnetcore.components/index.json
  - https://xunit.net/releases/v3/4.0.0
  - https://www.nuget.org/packages/xunit.v3
  - https://api.nuget.org/v3-flatcontainer/xunit.v3/index.json
  - https://api.nuget.org/v3-flatcontainer/hexalith.eventstore.domainservice/index.json
  - https://api.nuget.org/v3-flatcontainer/hexalith.commons.http/index.json
  - https://github.com/Hexalith/Hexalith.EventStore/

## Verdict

**CONDITIONAL PASS — do not treat the 2026-08-18 reality-check pass as still current.**

Named platform technologies still exist and still fit the Hexalith family claims (.NET 10 / C# 14 / Dapr 1.18+ / Aspire 13.x / Fluent UI Blazor V5 RC / xUnit v3). I1’s ACL route list still matches the authoritative YAML one-for-one. I2/I10 named EventStore types still exist at the checked-out EventStore SHA. Class A Authentication remains gated-not-executed. I1a does not claim the Parties AppHost is already retired.

The spine’s **I4 identity claim is stale as of 2026-09-08**, and the staleness is layered: the I4 row still quotes 2026-09-06 pins; the 8.3 matrix / fitness constants / signoff advanced on 2026-09-08 to a later set; **HEAD Builds has already moved past that later set without a signoff or test-constant update**. Frontmatter and `.memlog.md` still say `updated: 2026-08-18`. The I4 row’s own disclaimer (matrix + `.gitlink-signoff.tsv` are source of truth) is correct — and those sources already contradict the spine snapshot. The 2026-08-18 review’s Builds-gitlink finding has recurred in a new generation; this is not a rubber stamp of that pass.

## Per-claim table

| # | Spine claim | Status | Evidence |
|---|---|---|---|
| 1 | I1: deny-default EventStore-only ACL admits exactly the listed POST routes | **Verified** | Spine `ARCHITECTURE-SPINE.md:73-80` lists 13 routes. `src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.parties.yaml:30-73` has the same 13 names, `appId: eventstore`, `httpVerb: ['POST']`, two `defaultAction: deny` lines, no extras. `DocumentationFitnessTests.cs:26-41,152-155` asserts the identical `ExpectedSdkRoutes` list against that YAML. |
| 2 | I1a: domain AppHost is a rollback surface, not the target topology owner | **Verified** | Spine `:88-97`. `Hexalith.Parties.AppHost` still exists and still composes EventStore/Tenants/Parties (`src/Hexalith.Parties.AppHost/Program.cs:59-115`). `DocumentationFitnessTests.cs:12` still inventories the AppHost. `deferred-work.md` `8.8-runtime-boundary-cleanup` and `external-runtime-deployment` still name the Parties AppHost as rollback. No claim that the domain already owns the target topology. |
| 3 | I2: host shape is `AddEventStoreDomainService` / `UseEventStoreDomainService` | **Verified** | Declared at `references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs:66-121`. `RetiredLeafProjectFitnessTests.cs:91-94` asserts both calls in `src/Hexalith.Parties/Program.cs`. |
| 4 | I4 snapshot: EventStore package `3.102.0` / source `acf5c4e4…` / Commons `6da79aed…` / Builds `8db7459d…`; re-reconciled 2026-09-05/06; matrix+signoff are SoT | **Refuted as current identity; Commons pin still matches** | Spine `:250`. Live 8.10 table (`story-8-3-platform-api-prerequisite-matrix.md:69-72`) and `PlatformApiPrerequisitesTests.cs:23-34,178` pin EventStore **package `3.103.0` / source `c6efdbba6439370a5c674c12ed866c959624106a`** and Builds **`35c3d1e5b8a55a74a440b9c2cad4c5e18747b241`**. Commons `6da79aed…` still matches HEAD, tests, matrix, and signoff. HEAD EventStore gitlink matches the 8.10/test pin (`git ls-tree HEAD` → `c6efdbba…`). HEAD Builds gitlink is **`a32cb422749352cce8dec948aa3e78c8f00eb4cf`**, which is in none of those SoT records. NuGet still serves both `3.102.0` and `3.103.0` (`hexalith.eventstore.domainservice` index). See F1, F2. |
| 5 | I5–I15 §7 named test classes exist and still resolve under `tests/` | **Verified** | All 28 backtick-named `*Tests` classes in the §7 map were found as `public` types under `tests/` (see §7 class audit below). `EpicEightClosureFitnessTests.InvariantMapCoversI1ThroughI15WithExecutableOrDeferredEvidence` (`EpicEightClosureFitnessTests.cs:118-145`) only checks that named classes still exist — it does **not** re-read I4 SHAs. |
| 6 | Class A / Authentication supersession is gated, not executed | **Verified** | Spine `:64-68`. `src/Hexalith.Parties.Authentication/Hexalith.Parties.Authentication.csproj` exists. `RetiredLeafProjectFitnessTests.cs:104-122` requires the project, the `.slnx` entry, and the 8.3 tenant-claims row to stay `needs-additive-api` with the Authentication rollback path. `deferred-work.md` `8.8-runtime-boundary-cleanup` still lists the Authentication project in rollback. |
| 7 | I8 codecs `json+pdenc-v1` / `json-redacted` | **Verified in Parties; not EventStore runtime source** | Parties constants: `src/Hexalith.Parties.Security/PartyPayloadProtectionService.cs:28-29`. No matches in `references/Hexalith.EventStore/src/**/*.cs`. EventStore planning/spec text still names both codecs (e.g. `references/Hexalith.EventStore/_bmad-output/planning-artifacts/epics.md` FR37). Compatible with I8 (Parties-owned compatibility) and with G5 still `needs-additive-api`. |
| 8 | I10 abstractions: `IDomainProjectionHandler`, `IDomainQueryHandler`, `IReadModelStore`, `ReadModelWritePolicy`, `IQueryCursorCodec`, `ProjectionFreshnessMetadata` | **Verified with two type-kind caveats** | EventStore files exist at the checked-out `c6efdbba…` tree (see type audit). `ProjectionFreshnessMetadata` is a Parties contract (`src/Hexalith.Parties.Contracts/Models/ProjectionFreshnessMetadata.cs:3`), not an EventStore type. `ReadModelWritePolicy` is a `static partial class` (`ReadModelWritePolicy.cs:28`). See F8, F9. |
| 9 | I12: .NET 10, `.slnx` only, CPM, warnings-as-errors, xUnit v3 / Shouldly / NSubstitute / bUnit, Playwright a11y, root submodules, MinVer | **Family claims verified; patch pins live elsewhere** | `global.json:3` SDK `10.0.400`; `Directory.Build.props:35,38` `net10.0` + `TreatWarningsAsErrors`; `Directory.Packages.props:3,11` CPM import of Builds catalog. Catalog at checkout: `xunit.v3` `4.0.0` (`Directory.Packages.props:319`). Web: .NET SDK 10.0.400 is current (10.0.11, 2026-08-11); xUnit v3 current is `4.0.0` (2026-08-14). Parties does not set `<LangVersion>` (C# 14 is the .NET 10 default, confirmed on learn.microsoft.com / csharplang). See F5, F10, F11. |
| 10 | I13: Fluent 2 inheritance; purge FAST/v4 tokens | **Partially refuted** | Catalog pins `Microsoft.FluentUI.AspNetCore.Components` `5.0.0-rc.5-26219.1` — current NuGet latest, still prerelease (no 5.0.0 GA). Residual FAST/v4 custom properties remain in AdminPortal/ConsumerPortal CSS. Same class of finding as 2026-08-18 F3. See F4, F11. |
| 11 | Hexalith LLM baseline stack: .NET 10+, C# 14+, DAPR 1.18+, Aspire 13.x, Fluent UI Blazor V5 | **Verified as current families** | Web 2026-09-08: .NET 10 LTS (SDK 10.0.400); C# 14 ships with .NET 10; Dapr runtime latest `v1.18.2` (1.18.3-rc.2 exists); Dapr.Client latest stable `1.18.5`; Aspire latest `13.5.3`; Fluent UI Blazor latest `5.0.0-rc.5-26219.1`. Live Builds catalog matches Dapr.Client `1.18.5` and Aspire.Hosting `13.5.3`. Parent Epic 7 stack table is behind those pins. See F5. |
| 12 | Parent Epic 7 stack table is the inherited version table | **Stale vs live pins** | Parent `…/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md:140-144` still lists SDK `10.0.302`, Dapr `1.18.4`, Aspire `13.4.6`, FluentUI `5.0.0-rc.3-26138.1`. Epic 8 I12 does not repeat those patch pins; `DocumentationFitnessTests.cs:124-138` now pins `10.0.400` and forbids `10.0.302`. See F5. |

## §7 class audit (2026-09-08)

Every named executable surface in the I1–I15 map still exists:

| Named class | Path |
|---|---|
| `DocumentationFitnessTests` | `tests/Hexalith.Parties.Tests/FitnessTests/DocumentationFitnessTests.cs:7` |
| `ArchitecturalFitnessTests` | `tests/Hexalith.Parties.Tests/FitnessTests/ArchitecturalFitnessTests.cs:15` |
| `RetiredLeafProjectFitnessTests` | `tests/Hexalith.Parties.Tests/FitnessTests/RetiredLeafProjectFitnessTests.cs:8` |
| `EventStoreGatewayE2ETests` | `tests/Hexalith.Parties.IntegrationTests/Gateway/EventStoreGatewayE2ETests.cs:23` |
| `PlatformApiPrerequisitesTests` | `tests/Hexalith.Parties.Tests/FitnessTests/PlatformApiPrerequisitesTests.cs:11` |
| `ContractsPublicApiSnapshotTests` | `tests/Hexalith.Parties.Contracts.Tests/Package/ContractsPublicApiSnapshotTests.cs:10` |
| `ClientPackageTests` | `tests/Hexalith.Parties.Client.Tests/Package/ClientPackageTests.cs:10` |
| `PartyPickerPackagingTests` | `tests/Hexalith.Parties.Picker.Tests/Services/PartyPickerPackagingTests.cs:10` |
| `AdminPortalPackagingTests` | `tests/Hexalith.Parties.AdminPortal.Tests/Packaging/AdminPortalPackagingTests.cs:7` |
| `ConsumerPortalPackagingTests` | `tests/Hexalith.Parties.ConsumerPortal.Tests/Packaging/ConsumerPortalPackagingTests.cs:7` |
| `EventStoreGatewayRoutingTests` | `tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs:54` |
| `HttpPartiesQueryClientTests` | `tests/Hexalith.Parties.Client.Tests/HttpPartiesQueryClientTests.cs:14` |
| `SelfScopedPartiesClientTests` | `tests/Hexalith.Parties.UI.Tests/SelfScopedPartiesClientTests.cs:26` |
| `PartyAggregateConsentTests` | `tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateConsentTests.cs:15` |
| `PartyAggregateErasureTests` | `tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateErasureTests.cs:16` |
| `ErasureVerificationServiceTests` | `tests/Hexalith.Parties.Security.Tests/ErasureVerificationServiceTests.cs:9` |
| `CryptoKeyManagementCompatibilityHarnessTests` | `tests/Hexalith.Parties.Security.Tests/CryptoKeyManagementCompatibilityHarnessTests.cs:24` |
| `AdminPortalGdprPrivacyGuardrailTests` | `tests/Hexalith.Parties.Contracts.Tests/AdminPortal/AdminPortalGdprPrivacyGuardrailTests.cs:20` |
| `PartySdkProjectionHandlerTests` | `tests/Hexalith.Parties.Projections.Tests/Handlers/PartySdkProjectionHandlerTests.cs:29` |
| `PartySdkQueryHandlerTests` | `tests/Hexalith.Parties.Tests/Gateway/PartySdkQueryHandlerTests.cs:28` |
| `ProjectionFreshnessAndDegradationTests` | `tests/Hexalith.Parties.Tests/HealthChecks/ProjectionFreshnessAndDegradationTests.cs:20` |
| `IdentifierHygieneFitnessTests` | `tests/Hexalith.Parties.Tests/FitnessTests/IdentifierHygieneFitnessTests.cs:7` |
| `IdentifierValidatorTests` | `tests/Hexalith.Parties.Tests/Validation/IdentifierValidatorTests.cs:12` |
| `PartyAggregateCompositeTests` | `tests/Hexalith.Parties.Server.Tests/Aggregates/PartyAggregateCompositeTests.cs:14` |
| `PartiesContainerPublishWorkflowTests` | `tests/Hexalith.Parties.Ci.Tests/PartiesContainerPublishWorkflowTests.cs:6` |
| `MainLayoutAccessibilityTests` | `tests/Hexalith.Parties.UI.Tests/MainLayoutAccessibilityTests.cs:21` |
| `PartiesAccessibilitySpecimenTests` | `tests/Hexalith.Parties.UI.Tests/PartiesAccessibilitySpecimenTests.cs:17` |
| `MyConsentPageTests` / `MyPrivacyPageTests` | `tests/Hexalith.Parties.ConsumerPortal.Tests/Components/MyConsentPageTests.cs:16`, `…/MyPrivacyPageTests.cs:16` |
| `EpicEightClosureFitnessTests` | `tests/Hexalith.Parties.Tests/FitnessTests/EpicEightClosureFitnessTests.cs:8` |

I13/I14 rows still honestly say I13 parity is not discharged and I14 remaining copy is deferred. Those dispositions match the named tests’ limited scope (shell landmarks / GDPR page copy), not a claim that the tests now cover more than they did on 2026-08-18.

## I4 identity matrix (2026-09-08)

| Record | EventStore package | EventStore source | Commons HTTP | Builds catalog |
|---|---|---|---|---|
| Spine I4 row (`ARCHITECTURE-SPINE.md:250`) | `3.102.0` | `acf5c4e403699d4f9290fd6636e4d6b1872a3bd6` | `6da79aed2daa4e199689331ee3196f7872c0988a` | `8db7459d065926501ee045b3aaf7b816780905e5` |
| 8.10 reconciliation table (`story-8-3-…matrix.md:69-72`) | `3.103.0` | `c6efdbba6439370a5c674c12ed866c959624106a` | `6da79aed…` | `35c3d1e5b8a55a74a440b9c2cad4c5e18747b241` |
| `PlatformApiPrerequisitesTests.cs:23-34` | `3.103.0` | `c6efdbba…` | `6da79aed…` | `35c3d1e5…` |
| `.gitlink-signoff.tsv:87-88` (latest Builds/EventStore lines) | (package via catalog) | `c6efdbba…` `2026-09-08` | `6da79aed…` `2026-09-05` | `35c3d1e5…` `2026-09-08` |
| `git ls-tree HEAD` / index / checkout | catalog at checkout = `3.103.0` | `c6efdbba…` | `6da79aed…` | **`a32cb422749352cce8dec948aa3e78c8f00eb4cf`** |

`35c3d1e5` is an ancestor of Builds HEAD `a32cb422` (`git -C references/Hexalith.Builds merge-base --is-ancestor` succeeded). Catalog versions at `35c3d1e5` and the live checkout both select EventStore `3.103.0`, Aspire `13.5.3`, Dapr.Client `1.18.5`, FluentUI `5.0.0-rc.5-26219.1`, xunit.v3 `4.0.0`. The Builds move is a real gitlink identity change (`a32cb42 fix: update HexalithFrontComposerVersion to 4.4.0`), not a no-op.

`AssertGitlinkAndCheckout` (`PlatformApiPrerequisitesTests.cs:1548-1562`) now requires the **index** gitlink **and** checkout to equal `BuildsSha`. On this tree both are `a32cb422`, so `FinalDependencyReceiptsMatchTheSelectedPackageAndSourceGraph` is expected to fail until the constant, 8.10 table, and signoff catch up. That is the opposite of the 2026-08-18 OR-shaped mask; the test is stricter, and HEAD has outrun it.

## Type audit (checked-out EventStore / Commons / FrontComposer / Parties)

| Name | Exists? | Location |
|---|---|---|
| `AddEventStoreDomainService` | Yes | `references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs:66+` |
| `UseEventStoreDomainService` | Yes | same file `:121` |
| `IDomainProjectionHandler` | Yes | `…/IDomainProjectionHandler.cs:20` |
| `IDomainQueryHandler` | Yes | `…/IDomainQueryHandler.cs:18` |
| `IReadModelStore` | Yes | `…/Client/Projections/IReadModelStore.cs:23` |
| `ReadModelWritePolicy` | Yes (static class) | `…/Client/Projections/ReadModelWritePolicy.cs:28` |
| `IQueryCursorCodec` | Yes | `…/Client/Queries/IQueryCursorCodec.cs:21` |
| `ProjectionFreshnessMetadata` | Yes (Parties) | `src/Hexalith.Parties.Contracts/Models/ProjectionFreshnessMetadata.cs:3` |
| `json+pdenc-v1` / `json-redacted` | Yes (Parties runtime) | `src/Hexalith.Parties.Security/PartyPayloadProtectionService.cs:28-29` |
| Commons HTTP helpers | Yes | `references/Hexalith.Commons/src/libraries/Hexalith.Commons.Http/{HttpClientRegistration,BoundedProblemDetailsReader,HttpCorrelation}.cs` |
| `FrontComposerShell` | Yes | `references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Components/Layout/FrontComposerShell.razor.cs:83` |

## Findings

### F1 — I4 snapshot pins are stale versus the spine’s own source of truth
- Severity: high
- Disposition if updating: autofix
- Evidence: `ARCHITECTURE-SPINE.md:250` still quotes EventStore package `3.102.0`, source `acf5c4e403699d4f9290fd6636e4d6b1872a3bd6`, Builds `8db7459d065926501ee045b3aaf7b816780905e5`, and a 2026-09-05 / 2026-09-06 re-reconcile. The same sentence says the 8.3 matrix and `.gitlink-signoff.tsv` are the durable record. Those records (matrix `:69-72`; tests `:23-34,178`; signoff `:47,67,87-88`) have already moved to package `3.103.0` / EventStore `c6efdbba…` / Builds `35c3d1e5…`. NuGet still lists `3.102.0` **and** `3.103.0` (https://api.nuget.org/v3-flatcontainer/hexalith.eventstore.domainservice/index.json), so `3.102.0` is a real superseded release, not a hallucinated version. Commons `6da79aed…` is the only I4 SHA that is still current. The 2026-08-18 review verified a different generation of pins (`3.95.0` / `454b4d10…` / `17b1c7aa…`); that pass does not cover today’s row.

### F2 — HEAD Builds gitlink is ahead of matrix, tests, and signoff
- Severity: high
- Disposition if updating: discuss
- Evidence: `git ls-tree HEAD references/Hexalith.Builds` and `git ls-files --stage` both record `a32cb422749352cce8dec948aa3e78c8f00eb4cf` (`v4.27.2-10-ga32cb42`). Latest signoff Builds line is `35c3d1e5…` dated 2026-09-08 (`.gitlink-signoff.tsv:87`). `PlatformApiPrerequisitesTests.BuildsSha` is still `35c3d1e5…` (`:23,639`). I16 (`ARCHITECTURE-SPINE.md:156-167`) says a retained-identity change re-opens parity claims and that an unvalidated marker is a stop. Superproject commit `bf8daf98` moved the gitlink; no matching signoff line and no I4/matrix/test refresh exist for `a32cb422`. This is the same *class* of I4 bookkeeping gap the 2026-08-18 review called F1 (`17b1c7aa` checkout vs `6b78075` HEAD), now inverted: the committed gitlink advanced and the stamps did not.

### F3 — Document metadata and memlog are frozen at 2026-08-18
- Severity: medium
- Disposition if updating: autofix
- Evidence: spine frontmatter `updated: 2026-08-18` (`ARCHITECTURE-SPINE.md:5`). I4 body claims 2026-09-05 / 2026-09-06 re-reconcile (`:250`). `.memlog.md` frontmatter `updated: 2026-08-18T23:36` and its last events stop at commit `2b63ab9` / 2026-08-18. The memlog still narrates the earlier I4 caveat (Builds `6b78075` / `17b1c7aa`, `:16-17,27`) and does not record the 2026-09-05 catalog adopt, 2026-09-06 EventStore authorization, or 2026-09-08 refresh that the I4 sentence and signoff now describe. A reader using `updated:` as “last reality-checked” is looking at a date three weeks behind the identity text.

### F4 — I13 “purge FAST/v4 tokens” is still not true in retained UI
- Severity: medium
- Disposition if updating: defer
- Evidence: I13 (`ARCHITECTURE-SPINE.md:146`) requires purge of FAST/v4 tokens. Residual FAST-convention properties remain: `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor.css:75` `var(--neutral-fill-stealth-rest)` with **no fallback**; `:80,85` `var(--warning-fill-rest)` / `var(--error-fill-rest)`; `CreateEditPartyPage.razor.css:51`; `PartyGdprOperationsPanel.razor.css:18-30`; `src/Hexalith.Parties.ConsumerPortal/Components/MyProfilePage.razor.css:143`. Catalog FluentUI is v5 RC (`5.0.0-rc.5-26219.1`, https://api.nuget.org/v3-flatcontainer/microsoft.fluentui.aspnetcore.components/index.json). Same residual set the 2026-08-18 review filed as F3; I13 remains correctly **Deferred** in §7, so this is a lingering implementation gap, not a new false “executable” claim.

### F5 — Parent Epic 7 stack table is behind the live Parties pins
- Severity: medium
- Disposition if updating: defer
- Evidence: parent spine `…/epic-7-platform-alignment-2026-06-29/ARCHITECTURE-SPINE.md:140-144` lists SDK `10.0.302`, Dapr `1.18.4`, Aspire `13.4.6`, FluentUI `5.0.0-rc.3-26138.1`. Live: `global.json:3` `10.0.400`; catalog Dapr.Client `1.18.5`; Aspire.Hosting `13.5.3`; FluentUI `5.0.0-rc.5-26219.1`. Web current: SDK 10.0.400 (https://dotnet.microsoft.com/en-us/download/dotnet/10.0), Dapr.Client 1.18.5 (https://api.nuget.org/v3-flatcontainer/dapr.client/index.json), Aspire 13.5.3 (https://github.com/microsoft/aspire/releases), FluentUI RC5 (NuGet profile fluentui-blazor). Epic 8 I12 only says “.NET 10” and “xUnit v3”; it does not copy the stale parent patches. `DocumentationFitnessTests.cs:124-138` now treats `10.0.302` as forbidden. The inherited table is a trap if a later story treats Epic 7 as the version authority.

### F6 — 8.3 G5 cell still names a superseded “retained” EventStore/Builds pair
- Severity: medium
- Disposition if updating: autofix
- Evidence: payload-protection row (`story-8-3-platform-api-prerequisite-matrix.md:104`) still says retained identities are EventStore `d45206f7cbd80a112519c1d4687d7279a745f0c5` / package `3.103.0` and Builds `7b0b1837ce368e314b5e11b011b73603637a17e1`, then later says revalidated 2026-09-07 at that same EventStore SHA. The 8.10 table on the same page (`:70-72`) and `PlatformApiPrerequisitesTests.PayloadProtectionEventStoreSha` (`:34`) already record `c6efdbba…` / Builds `35c3d1e5…`. Spine I4 tells readers to treat the matrix as SoT; the matrix currently disagrees with itself on the G5 identity sentence. G5 status `needs-additive-api` is still consistent with EventStore source (no `IPersonalDataPolicy` / `pdenc-v2` / PayloadProtection projects in `references/Hexalith.EventStore/src`).

### F7 — I4 “Executable” evidence does not assert the SHAs printed in the I4 row
- Severity: low
- Disposition if updating: autofix
- Evidence: `EpicEightClosureFitnessTests.cs:135-145` only requires that `PlatformApiPrerequisitesTests` still exists. `PlatformApiPrerequisitesTests` asserts `3.103.0` / `c6efdbba…` / `35c3d1e5…`, not the I4-printed `3.102.0` / `acf5c4e…` / `8db7459d…`. The I4 disclaimer makes this survivable, but a reader who treats the printed SHAs as “what the test pins” is wrong as of 2026-09-08.

### F8 — `ReadModelWritePolicy` is a static helper, not an abstraction seam
- Severity: low
- Disposition if updating: ignore
- Evidence: I10 (`ARCHITECTURE-SPINE.md:134-135`) groups it with four interfaces. Implementation is `public static partial class ReadModelWritePolicy` (`ReadModelWritePolicy.cs:28`). Name and consumption are real; the “abstraction” label is still overstated (2026-08-18 F5, unchanged).

### F9 — `ProjectionFreshnessMetadata` is a Parties contract, not an EventStore platform type
- Severity: low
- Disposition if updating: ignore
- Evidence: I10 (`ARCHITECTURE-SPINE.md:131`) says “`ProjectionFreshnessMetadata` on every read” in the same sentence as EventStore target abstractions. The type lives at `src/Hexalith.Parties.Contracts/Models/ProjectionFreshnessMetadata.cs:3`. EventStore’s live freshness surface is `QueryResponseMetadata` / `IReadModelFreshness` (e.g. `references/Hexalith.EventStore/src/Hexalith.EventStore/Controllers/QueriesController.cs:108`). The Parties type exists and is used; the ownership implication is easy to misread.

### F10 — C# 14+ is the .NET 10 default, not an explicit Parties pin
- Severity: low
- Disposition if updating: ignore
- Evidence: baseline and I12 imply C# 14+. Web: C# 14 ships with .NET 10 (https://devblogs.microsoft.com/dotnet/introducing-csharp-14/; https://github.com/dotnet/csharplang/blob/main/Language-Version-History.md). Parties `Directory.Build.props` has no `<LangVersion>`. Matrix `:72` states Parties does not import `Hexalith.Build.props` (which sets `LangVersion=latest` at `references/Hexalith.Builds/Hexalith.Build.props:22`). Claim is still true via SDK default for `net10.0`, not via a repo-local pin.

### F11 — Fluent UI Blazor V5 is still an RC, not GA
- Severity: low
- Disposition if updating: ignore
- Evidence: I13 “Fluent 2 inheritance” plus baseline “Fluent UI Blazor V5” are still the correct product family. Latest NuGet version is `5.0.0-rc.5-26219.1` (https://www.nuget.org/profiles/fluentui-blazor; last updated 2026-08-09). No `5.0.0` stable appears in the flat container. Catalog matches the latest RC. Not stale — still pre-GA, which the 2026-08-18 review already treated as a recorded pin.

## What still holds (not findings)

- I1 route list = ACL YAML = `DocumentationFitnessTests.ExpectedSdkRoutes` (13 POST routes, `eventstore` only).
- I1a / §2 target-vs-rollback AppHost wording does not contradict the still-present Parties AppHost.
- `Hexalith.Parties.Authentication` remains in-repo; Class A supersession is still gated.
- Named EventStore host/projection/query types exist at checkout `c6efdbba…`.
- All §7 named test classes exist; I13 is still correctly marked Deferred for undischarged a11y parity.
- Family stack claims (.NET 10, C# 14, Dapr 1.18+, Aspire 13.x, Fluent UI Blazor V5, xUnit v3) match current public releases and the live Builds catalog.
- EventStore package `3.102.0` still exists on nuget.org; it is simply no longer the consumed identity.

## Finding count

critical: 0 · high: 2 · medium: 4 · low: 5 — total 11.
