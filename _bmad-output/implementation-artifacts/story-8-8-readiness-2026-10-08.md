# Story 8.8 readiness investigation — 2026-10-08

This is planning evidence, not owner approval, API adoption, parity certification,
or activation of DW-98. Story 8.8 remains `blocked`; its spec remains `draft`.
The existing user edit to `spec-epic-8-followup-review.md` was preserved.
No production, dependency, root-gitlink, approval-ledger, or sprint-status changes
were made. The bmad-build draft route performs investigation before approval.

The current authoritative Epic 8 spine supersedes historical stack and MCP
statements in the cached project context and July implementation packet. Preserve
the packet's detailed per-gate execution inventory, but reconcile identities and
MCP ownership before executing it.

## Observed dependency identities

Parties parent: `c095134f6ec648560954d6f0fc76858fd1d7cd0e`, branch `main`.
Each source identity below was obtained from the committed root tree and matched
a clean checkout at initial inspection. These are observations, not approvals.
Concurrent production/test/submodule edits appeared later and were preserved;
this receipt does not certify the final working tree or those external edits.

| Root dependency | Committed gitlink / checkout | Selected package or mode |
| --- | --- | --- |
| EventStore | `4cc77f9554395e84539e173e94b8f0b4df14d643` | Runtime packages `3.115.0`; orchestration/diagnostic source separate |
| Commons | `116d26815eb81e35b3c161e1799e5ee12805fc0a` | HTTP source fallback selected; package fallback `2.30.1` |
| FrontComposer | `c561b3210f15206a90c39c82c58f2e5b1005cd60` | Packages `4.5.0`; source topology separate |
| Builds | `af20682ac8fc420068a731ecb87cff84727a3d53` | Shared package-catalog import |
| Platform | `332a7d8104e3f6c9aaa57cbc7f07afb5fa0859de` | Source inspection only; no AppHost found |

Reproduce with `git ls-tree c095134f6ec648560954d6f0fc76858fd1d7cd0e references/Hexalith.EventStore references/Hexalith.Commons references/Hexalith.FrontComposer references/Hexalith.Builds references/Hexalith.Platform`,
then run `git rev-parse HEAD` and `git status --porcelain` from each owning
root-declared submodule. Do not update or initialize nested submodules.

## Gates and reusable surfaces

| Gate | Current finding | Required before consumption or retirement |
| --- | --- | --- |
| Sequence / I17 | Story 8.6 is done; 8.7 remains blocked. DW-98 is open without a new activation. | 8.7 completes or an approved architecture/product artifact changes the order; record activation/handover through the established ledger process. Planning does not activate production work. |
| G12 | Original package publication decision was resolved on 2026-07-11. Current selected runtime family is `3.115.0`. | Retain that resolution; separately validate actual selected packages and the later missing-API build prerequisite below. |
| G6 | `QueryResponseMetadata.Lifecycle`/`Provenance`, `ProjectionLifecycleState` (six Parties states plus Unknown), `QueryResponseProvenance`, and `ProjectionLifecyclePolicy` exist in current source and cached Contracts/Client `3.115.0` XML. Gateway carries the metadata. | Refresh the matrix's gap to exact-identity/dialect acceptance, warning/reason/outcome mapping, paging, 200/304 parity, consumer tests and exercised rollback. Do not infer availability at current identity from historical Story 1.20 approval. |
| G8-A | Audience-aware `WithEventStoreJwtAuthentication` and `HexalithEventStoreJwtAuthenticationOptions` exist in current EventStore.Aspire source. The historical producer receipt tested 26 cases at `e7cf91fa714b780d60eb129722f4ab82fc7b0b26`. | Current named approval, public/package inventory, local and external authority/issuer/audience/algorithm/token proof, actual secret-free publish artifacts, consumer parity and exercised rollback. Historical producer tests are not current consumer proof. |
| G8-B | Generic `AddEventStoreGatewayClient`, DAPR invocation registration, and domain-processor `AddEventStoreClient<TProcessor>` exist. | Independently selectable command/query/GDPR transports, module-client coexistence, repeat registration and handler-order delivery/proof. Processor registration is not that API. |
| G8-C | FrontComposer AppHost composes EventStore/Tenants/Parties and frontcomposer-ui, but lacks parties-mcp, standalone parties-ui and Parties-equivalent PUBLISH_TARGET selection. | Exact host-artifact/source and EventStore.Aspire identities; approved MCP/UI preserve-or-replace map, optional Memories/sample parity, Docker/Kubernetes/ACA disposition, topology/security/publish tests and exercised rollback. Re-approve every dependent AppHost rollback deferral before retirement. |
| G7/G9 | Public EventStore tenant constant, AggregateIdentity.IsValid, lightweight Authentication package/transformer and Commons UniqueIdHelper.IsValidUlid remain absent. Gateway's internal transformer is not the approved package boundary. | Complete named owner/API/release/consumer/test ledgers, host transient versus UI scoped lifetime/order, claims/identifier truth tables, compatibility alias and exercised switch-back before atomic Authentication retirement. |
| G11-A | FrontComposer MCP has authenticated context accessors and mandatory tenant/resource gates. Shell token helpers serve UI infrastructure. Neither supplies the approved MCP relay/credential contract. | McpCli-targeted operation inventory and approved transport/credential decision, plus authoritative context, no inbound MCP token/API-key reuse, explicit downstream provider or fail closed, single-value replace-not-append headers, CR/LF rejection, cancellation and no-secret evidence. Keep the five existing operations until replacement/withdrawal passes. |
| G11-B | No shared configured EventStore Admin aggregate/stream/correlation builder or typed unavailable outcome found in FrontComposer; no Commons URI composer. | Safe absolute HTTP/HTTPS base without user-info, path/query preservation, single encoding, blank/unsafe configuration outcomes and consumer parity. Keep local links. |
| G11-C | No shared Commons bounded named-health extractor or FrontComposer capability contract found. | Explicit timeout/cancellation/byte/depth bounds; named-check and wrong-type parsing; Available/LocalOnly/Degraded parity; missing, disabled, malformed, oversized, slow and non-success outcomes without downstream detail. Keep local probe. |
| G1/G2 | Parties still registers AddPartiesDaprHealthChecks and DegradedResponseMiddleware. EventStore has domain state-store readiness, but no complete current shared replacement was identified. | Reconcile adoption versus continued deferral in the G1/G2 matrix row; preserve names/tags/failure/readiness and GET degraded/stale headers until parity. Never delete these through generic runtime cleanup. |
| Commons HTTP available row | Main row still records `2.28.1` / `b03469b13408530bb757d3d02279c2d772ee4848`; later reconciliation records current Commons `116d268...`. | Reconcile the marked row with selected mode/identity and validate individual behavior facets before further adoption. Identity mismatch alone requires no additive API. |
| Builds available row | Main row records `4.18.5` / `ed75ae3c45425b9610d5e75e6c5ec3e8d5283fe1`; current gitlink is `af20682...`. Latest observed signoff instead names `397c94a4e246c90b21cf408790fa0d55bf32d795`. | Exact row/signoff acceptance as required by repository policy, effective-property and source/package proof, CI publication compatibility and exercised rollback before deleting local selectors. |

The matrix, spine and DW-98 remain authoritative. This report neither promotes
rows nor fills pending I20 approval fields. Producer work belongs in its owning
repository and needs separate scope authorization; this consumer spec does not
permit editing submodule content or pointers.

## HTTP and build compatibility cautions

A technically feasible narrow Parties change is replacing four typed-client
registrations in `PartiesClientServiceCollectionExtensions.cs` with Commons
`HttpClientRegistration.AddTypedHttpClient<TClient,TImplementation,TOptions>`.
Its options callback, OnRegistration, web-scheme validation and chainable builder
can preserve eager endpoint/tenant validation and handler chaining. Sequence,
identity and parity gates still apply. Preserve exact public validation messages.

Command error reading and server exception handling already consume shared
ProblemDetails helpers. Keep `HttpPartiesCommandClient.SanitizeDetail`: the shared
reader does not provide equivalent sensitive-detail suppression or explicit byte
and field-length limits. Commons `HttpCorrelation.ResolveCorrelationId` currently
uses Guid.TryParse and creates GUIDs; its availability does not justify further
identifier adoption under the required ULID baseline.

Parties imports only the shared package catalog, not Hexalith.Build.props or
Hexalith.Package.props. Shared build props add analyzers and change
CodeAnalysisTreatWarningsAsErrors / NuGet exemptions; blind imports can weaken the
build gate. No exported shared replacement was identified for the local generic
source-root and dependency-mode probes. Preserve effective warning gates, package
metadata/inventory, container provenance and publication contracts.

The local capability probe also has observed gaps (unbounded response buffering,
health description disclosure and wrong-shaped results exceptions). These are
planning findings, not permission to implement unrelated safety fixes or to
claim the retained implementation already meets the future shared contract.

## Implementation and validation map

Use the detailed UPDATE/KEEP table in
`8-8-client-mcp-apphost-build-and-deploy-cleanup.md`, with current identities and
McpCli ownership reconciled. Atomic retirement must include solution/project
references, host/UI composition, CI/scripts, package inventories and documentation.
The deleted production deploy tree stays absent.

- Client: command/query, dependency injection, self-scope and Package tests in
  `tests/Hexalith.Parties.Client.Tests`.
- Authentication: claims tests plus host and UI authentication-composition tests.
- MCP: tool-contract, dispatch and project-fitness tests in
  `tests/Hexalith.Parties.Mcp.Tests`; replacement owner authorization/behavior tests.
- Admin: link/probe service tests and affected component/package tests in
  `tests/Hexalith.Parties.AdminPortal.Tests`.
- Runtime/build: PlatformApiPrerequisitesTests, AppHostTenantsTopologyTests,
  correlation/degraded/health tests, CommonsHttpRestoreRoutingTests and the CI
  container-publication suite; actual topology and publish-artifact checks.
- AppHost retirement: predecessor key-ring, cursor-purpose and protected-payload
  continuity, or a separately recorded approved invalidation window; dependency
  deferrals must pass or be re-approved against the successor.

Producer/consumer tests must name exact dependency identities and run before
switch-back, on the reverted path, and after forward restore. Invoke individual
xUnit v3 assemblies directly for focused filters. Source Debug diagnostics cannot
substitute for Release/package-mode or real runtime proof. The future broad gate
remains `dotnet build Hexalith.Parties.slnx -c Release -m:1`, followed by applicable
CI/topology and owner validation; no gate is weakened to hide a blocker.

## Focused baseline validation in this run

Command: `dotnet build src/Hexalith.Parties/Hexalith.Parties.csproj --configuration Release --no-restore -m:1`.

Result: exit 1; 0 warnings and 2 CS1061 errors, at Program.cs lines 64 and 66:
IEndpointConventionBuilder lacks RequireEventStoreSidecarChannel in the selected
published DomainService `3.115.0`. Elapsed build time: 3.50 seconds.

This independently reproduces the existing 2026-10-08 publication handoff.
No new NuGet availability claim is made and no live feed query was required to
reproduce the installed consumer failure. Upstream source API presence does not
repair the published consumer. See
`tests/eventstore-package-api-2026-10-08/README.md` and `docs/ci.md` for the separate
owner publication and runtime-credential follow-up. G12 is not reopened.

No product tests or topology were run for these planning-only edits. No runtime
parity, no-skips topology success, full CI or release readiness is claimed.

## Planning-document validation

The draft YAML, dispatch route, context paths, frozen-block structure, CRLF and
retained blocked sprint state passed structural checks. Git's default whitespace
check treats CRLF as trailing whitespace in this repository; the scoped check uses
`git -c core.whitespace=cr-at-eol diff --check -- _bmad-output/implementation-artifacts/spec-8-8-client-mcp-apphost-build-and-deploy-cleanup.md`.
A direct whitespace scan also covers this new report. This accommodates the
tracked EditorConfig CRLF rule without changing repository configuration or gates.
Concurrent edits outside these two planning documents are outside this run's
validation scope.

## Accepted path and owner-task planning — 2026-10-08

The user's `do recommended` selected consumer-only Story 8.8 with separate owner
prerequisite tasks, predecessor key/cursor/payload compatibility and exercised
rollback. Open intent questions are resolved in the draft spec. Technical sequence,
API, owner approval, exact-identity and parity gates remain closed; neither spec nor
sprint state is promoted to ready/done. DW-98 is not activated. The continuity choice
must enter the I20 ledger through the human/reviewer gate before either 8.7 or 8.8
activation relies on it. No invalidation or resequencing is selected.

The [owner-task plan](story-8-8-owner-prerequisite-plan-2026-10-08.md) contains ten
separate delivery/validation items in dependency order, with owner repository,
target paths, outputs, validation and retention conditions. Its first item continues
the existing completed local EventStore CI repair rather than duplicating it.
Full exact-current-main CI and coordinated owner publication remain required.

A fresh read-only package probe was run with:

`python3 _bmad-output/implementation-artifacts/tests/eventstore-package-api-2026-10-08/probe-package.py`

Result: exit 0. The retained
[probe receipt](tests/story-8-8-planning-2026-10-08/package-probe.json) and raw index
responses record observation at 2026-10-08T08:05:46.883937+00:00. All nine consumer
indexes still end at 3.115.0, without newer or prerelease versions; the downloaded
DomainService assembly still lacks the required API metadata name. The package
and DLL hashes match the earlier observation. G12 remains resolved; this is the
later installed API/publication gap.

Parties advanced externally to `71d5f036ea87cf360af4de09f8fcb616faef2e1a` before this
planning update. The relevant root gitlinks and central package selection remain
as previously observed, with McpCli additionally at
`e159f82b7528797fc245045625ff387d65294ba9`. EventStore and other production/test
paths have concurrent user-owned changes; they are preserved. No fresh product or
full-CI validation is claimed for that working tree. This run writes only the
consumer spec, this readiness supplement, the owner-task plan and fresh probe
receipt. No owner patch, package selection, root pointer, approval ledger or
production migration is changed by these planning decisions.
