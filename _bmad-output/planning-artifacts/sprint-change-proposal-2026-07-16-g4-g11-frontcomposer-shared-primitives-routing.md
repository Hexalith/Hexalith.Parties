---
title: Sprint Change Proposal — Route G4 and G11 to FrontComposer and Commons
date: 2026-07-16
author: Administrator
workflow: bmad-correct-course
mode: incremental
scope_classification: moderate
status: approved
approval_required: false
approved_by: Administrator
approved_at: 2026-07-16T01:00:07+02:00
applied_at: 2026-07-16T01:00:07+02:00
handoff_status: routed
trigger: >
  Route G4 reusable UI and browser-service primitives for Story 8.9, and G11
  MCP/integration primitives for Story 8.8, to Hexalith.FrontComposer with
  bounded transport and URI mechanics in Hexalith.Commons.
related:
  - _bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/implementation-artifacts/spec-8-8-client-mcp-apphost-build-and-deploy-cleanup.md
  - _bmad-output/implementation-artifacts/spec-8-9-ui-frontcomposer-and-fluent-consolidation.md
  - _bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md
  - _bmad-output/implementation-artifacts/sprint-status.yaml
  - docs/accessibility.md
  - docs/frontend/party-picker.md
  - docs/gdpr-portability-export.md
  - docs/memories-backed-party-search.md
  - docs/api-contracts.md
  - references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Components/Forms/FcDestructiveConfirmationDialog.razor
  - references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Components/EventStore/FcProjectionConnectionStatus.razor
  - references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Components/Lifecycle/FcLifecycleWrapper.razor
  - references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Components/Layout/FrontComposerShell.razor
  - references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Mcp/HttpFrontComposerMcpAgentContextAccessor.cs
  - references/Hexalith.Commons/src/libraries/Hexalith.Commons.Http/README.md
---

# Sprint Change Proposal — Route G4 and G11 to FrontComposer and Commons

## 1. Issue Summary

Stories 8.9 and 8.8 cannot safely delete their Parties-local rollback surfaces
because the Story 8.3 G4 and G11 prerequisite rows remain
`needs-additive-api`.

- **G4 blocks Story 8.9.** FrontComposer has related shell, status,
  accessibility, and destructive-dialog foundations, but it does not yet expose
  the complete reusable parity set needed by the Parties Admin, Consumer, and
  Picker packages.
- **G11 blocks Story 8.8.** FrontComposer MCP already resolves authenticated
  tenant/user context and enforces fail-closed gates, while Commons.Http already
  supplies typed-client registration and bounded ProblemDetails mechanics. The
  shared outbound relay, EventStore Admin UI link, and bounded capability-health
  surfaces are still missing.

This is a shared-platform ownership and API-closure issue, not a new product
requirement. Routing the work does not make either row `available` and does not
authorize Parties source deletion.

### Trigger and classification

- **Triggering stories:** Epic 8 Story 8.9 for G4 and Story 8.8 for G11.
- **Issue type:** technical/API limitation plus producer ownership
  clarification during sprint execution.
- **Scope:** moderate. The change coordinates FrontComposer Contracts.UI,
  Shell, MCP, Commons.Http, and Parties consumer evidence without adding an epic
  or changing delivered functionality.
- **Immediate effect:** Stories 8.8 and 8.9 remain `backlog`; each gated slice
  stays blocked until its matrix row records owner approval, an exact consumable
  identity, parity evidence, and rollback.

### Evidence verified 2026-07-16

#### G4

- `FrontComposerShell` already renders localized skip-to-content and conditional
  skip-to-navigation links targeting stable, focusable landmarks. Skip links are
  therefore an **existing parity candidate**, not an assumed missing component.
- `FcLifecycleWrapper` already applies the routine `status`/`polite` versus
  failure `alert`/`assertive` split, and `FcProjectionConnectionStatus` already
  exposes a polite connection-level status. No public general-purpose
  per-record status/freshness primitive was found.
- `FcDestructiveConfirmationDialog` preserves safe cancel focus and Escape
  cancellation, but has no typed-name mode and does not disable confirmation
  until an exact caller-supplied value matches.
- No `FcEntityPicker<T>` or shared FrontComposer file/JSON download service was
  found.
- Parties currently carries the required behavior locally: the WAI-ARIA
  combobox/custom-element picker, `FreshnessStatus`, separate polite/assertive
  status regions, exact typed-name erasure confirmation, and two browser JSON
  download paths.

#### G11

- `HttpFrontComposerMcpAgentContextAccessor` authenticates through configured
  API keys or an authenticated principal, resolves server-trusted tenant/user
  identity, and fails closed for missing or ambiguous context.
- FrontComposer MCP requires explicit tenant-tool and resource-visibility gates
  at registration. A new relay must preserve those gates and must not trust tool
  arguments or raw tenant/user headers as authority.
- Parties currently forwards `Authorization`, `X-Tenant-Id`, and `X-User-Id`
  through `McpContextForwardingHandler`. That local handler does not provide a
  shared, owner-approved single-value replacement/injection policy.
- API-key authentication cannot safely be converted into a downstream bearer
  credential by copying the FrontComposer MCP API key. The shared surface needs
  an explicit host-provided downstream credential path or a fail-closed no-token
  outcome.
- Parties locally owns `AdminPortalEventStoreAdminLinks`, which combines an
  absolute base URI, path, and encoded aggregate/correlation query value.
- Parties locally probes `health`, reads the named `memories-search` result, and
  maps enabled/reachable/degraded state to Available, LocalOnly, or Degraded.
  No equivalent bounded shared capability probe was found.
- Commons.Http currently owns generic typed-HTTP registration and bounded
  ProblemDetails reading. It does not yet own safe relay-header replacement,
  general URI query composition, or a bounded named-health-check reader.

## 2. Ownership Decision

The proposed package boundary is:

| Concern | Accountable producer | Boundary |
|---|---|---|
| UI-facing state, picker, dialog, download, and accessibility contracts | `Hexalith.FrontComposer.Contracts.UI` | Public Blazor/Fluent-facing contracts and descriptors; no domain-specific Party or EventStore freshness authority |
| Fluent/Blazor implementations and browser interop | `Hexalith.FrontComposer.Shell` | `Fc*` components, download implementation, shell skip links, localization, and accessibility behavior |
| Authenticated MCP context and outbound relay orchestration | `Hexalith.FrontComposer.Mcp` | Fail-closed tenant/user/auth semantics, DI registration, and handler composition |
| Safe HTTP-header, bounded health-response, and URI mechanics | `Hexalith.Commons.Http` | Domain-neutral transport helpers only; no MCP policy, claim precedence, search semantics, or Blazor UI |
| Parties-specific labels, domain mappings, safe filenames, tool definitions, and consumer parity | Parties packages | Remain local unless a separately approved contract moves them |

This split keeps the FrontComposer kernel package UI-clean, keeps Blazor and
Fluent dependencies in Contracts.UI/Shell, and prevents Commons from becoming
an authorization or product-semantics owner.

## 3. Impact Analysis

### Epic and story impact

- **Epic 8 remains viable.** No new epic or MVP feature is introduced.
- The sequence remains
  `8.1 -> 8.2 -> 8.3 -> 8.4 -> 8.5 -> 8.6 -> 8.7 -> 8.8 -> 8.9 -> 8.10`.
- **Story 8.8 remains `backlog`.** Its G11 integration slice stays gated.
- **Story 8.9 remains `backlog`.** Its G4 UI migration stays gated.
- Story 8.10 may close the rows only after exact producer identity, consumer
  parity, and rollback evidence are recorded.

### Artifact impact

- **PRD:** no change. No functional or non-functional product requirement
  changes.
- **UX:** no change. The approved Parties interaction, copy, privacy, and WCAG
  behavior remains the parity target.
- **Architecture spine:** no change. I3, I10, I13, and the six-part readiness
  gate already govern this correction.
- **Epics:** no change. Stories 8.8 and 8.9 already express the correct product
  intent and sequence.
- **Story 8.3 prerequisite matrix:** expand G4 and G11 into named producer work
  packages, correct their dependent-story mapping, and retain
  `needs-additive-api`.
- **Story 8.8 spec:** make the G11 producer/consumer gate and local rollback
  surfaces explicit; include the local deep-link and search-probe services in
  the code map. Correct stale G12 language if it has not already been corrected
  by the G8 proposal.
- **Story 8.9 spec:** distinguish additive G4 work from the existing skip-link
  parity candidate and make download/live-region/dialog/freshness evidence
  explicit.
- **Sprint status:** route the G4/G11 action to named FrontComposer/Commons
  package owners and set the routing action to `in-progress` after approval.
  Story statuses and matrix statuses do not change.
- **Project docs:** no immediate edit. Current docs describe current behavior;
  Story 8.10 updates them after actual migration.
- **Owner repositories:** no submodule content is changed by this workflow.

### Risk and rollback

- **Effort:** low for the approved Parties planning edits; medium-to-high for
  producer delivery and cross-package verification; medium for later consumer
  migration.
- **Primary G4 risk:** shipping superficially similar controls that regress
  keyboard behavior, durable-ID selection, privacy-safe announcements,
  forced-colors, reduced motion, exact confirmation, or browser-download
  cleanup.
- **Primary G11 risk:** treating inbound headers or an MCP API key as safe
  downstream credentials, appending duplicate identity headers, reading
  unbounded health JSON, or generating unsafe/malformed admin links.
- **Rollback:** retain every Parties picker, freshness/status component,
  download helper, typed confirmation, MCP handler/context accessor, deep-link
  builder, and search probe until its replacement passes producer and Parties
  consumer parity. No completed product functionality is rolled back.
- **Timeline:** only the G4/G11 slices of Stories 8.9/8.8 are affected; no MVP
  impact.

## 4. Recommended Approach

**Selected: direct adjustment with six G4 work packages, three G11 work
packages, and independent consumer deletion gates.**

The symbol names below identify the requested public capabilities. Except for
the explicitly requested `FcEntityPicker<T>`, producer owners may choose the
final additive names while preserving the behavioral contracts and package
boundaries.

### G4-A — additive `FcEntityPicker<T>`

Owners: FrontComposer Contracts.UI and Shell.

Provide a generic durable-ID selection component with:

- host-supplied async search and selected-record resolution; no domain transport
  dependency;
- stable selected key/value binding, with display fields treated as preview;
- cancellation and stale-response rejection across query, authentication,
  tenant/user context, and option changes;
- labeled editable combobox, input-owned focus, listbox/options,
  `aria-activedescendant`, Arrow/Enter/Escape/Tab behavior, and accessible clear;
- disabled/read-only separation, localized safe states, paging bounds, retry
  only for retryable failures, forced-colors, reduced-motion, and no color-only
  status;
- an optional adopter event adapter so Parties can preserve its existing
  `party-selected` custom-element contract without making that domain event part
  of FrontComposer.

### G4-B — per-record freshness indicator

Owners: FrontComposer Contracts.UI and Shell.

Provide a UI-normalized per-record freshness contract and component that can
render current, stale/last-known, rebuilding, degraded, unavailable, and
local-only states after an adopter mapping. It must not redefine the
EventStore/G6 wire model. Routine freshness updates are polite, retain bounded
last-known content when allowed, never steal focus, and are not color-only.

### G4-C — reusable live-region politeness

Owners: FrontComposer Contracts.UI and Shell.

Expose a reusable status/live-region policy or component that makes the
existing FrontComposer convention public: routine loading, result-count,
freshness, processing, ready, and retry-complete changes use `status`/`polite`;
validation, failure, and load-error changes use `alert`/`assertive`. It must
support atomic bounded announcements, localization, and safe repeated updates
without exposing payloads, tokens, tenant IDs, or backend problem detail.

### G4-D — file and JSON download service

Owners: FrontComposer Contracts.UI and Shell. Commons is not the browser-interop
owner.

Provide an injectable file-download service plus a JSON convenience path that:

- accepts caller-approved safe filename, content type, and bounded stream/bytes;
- uses stream interop for non-trivial payloads and does not require a
  domain-specific global JavaScript name;
- creates and revokes object URLs and removes temporary anchors in `finally`;
- does not log, persist, cache, or echo payload content;
- propagates cancellation and maps disconnect/JS failure to a bounded caller
  outcome;
- leaves domain filename derivation and export authorization in Parties.

### G4-E — typed-name destructive dialog mode

Owners: FrontComposer Contracts.UI and Shell.

Extend `FcDestructiveConfirmationDialog` additively with an opt-in typed-name
mode. The caller supplies localized label/instruction and the expected plain
text. Confirmation stays disabled until an ordinal exact match; cancel retains
initial focus; Escape cancels; Enter cannot bypass the disabled state; copy is
rendered as text; focus and validation are accessible. The existing simple
confirm mode remains source- and behavior-compatible.

### G4-F — skip-link parity certification

Owner: FrontComposer Shell.

Do not create a parallel skip-link component by default. Record producer and
Parties consumer evidence that the existing localized shell links:

- are the first keyboard stops;
- target stable, unique, programmatically focusable content/navigation
  landmarks;
- remain visible with normal focus and forced-colors;
- behave correctly when navigation is absent or responsive navigation changes;
- are present in the approved package/submodule identity.

Only add a public label/target customization seam if Parties consumer evidence
shows that the current shell contract cannot satisfy the approved UX.

### G11-A — authenticated MCP context and tenant/auth relay

Owners: FrontComposer MCP, with domain-neutral header mechanics in Commons.Http.

FrontComposer MCP must expose a composable outbound relay based on the
server-resolved `FrontComposerMcpAgentContext`. Commons.Http may provide the
single-value header replacement/validation primitive, but FrontComposer owns
which values are authoritative and when relay is allowed.

The proof must establish:

- tenant and user values originate from the authenticated server context, never
  tool arguments or untrusted passthrough headers;
- relayed headers are validated, CR/LF-free, single-valued, and deterministically
  replace—not append to—existing identity headers;
- a bearer credential is relayed only from an approved authenticated credential
  source;
- FrontComposer MCP API keys are never re-used as downstream bearer/API keys;
  the API-key path requires an explicit host credential provider or fails closed;
- missing/ambiguous context and credential-provider failure do not call the
  downstream service;
- tokens and raw identity values are absent from logs, telemetry, exceptions,
  tool results, and resource output;
- existing tenant-tool and resource-visibility gates remain mandatory.

### G11-B — EventStore Admin UI deep-link builder

Owners: FrontComposer Shell/Contracts.UI for the EventStore-facing public
contract; Commons.Http for optional domain-neutral URI composition mechanics.

Provide a configured builder for stream/aggregate and correlation links that:

- requires an absolute HTTP/HTTPS base URI without user-info;
- preserves an approved base path and existing safe query parameters;
- combines relative paths without path loss or traversal;
- URI-encodes names and values exactly once;
- returns a typed unavailable/failure result for missing configuration or blank
  identifiers rather than emitting a malformed link;
- never places credentials, tenant IDs, display names, contact values, or free
  text into the link unless an explicitly approved contract requires it.

### G11-C — bounded search-capability health probe

Owners: Commons.Http for HTTP/JSON bounds and named health-check extraction;
FrontComposer Shell/Contracts.UI for the UI-facing capability result and
configuration adapter.

Provide a configurable named-health-check probe that:

- uses an absolute validated endpoint, cancellation, timeout/resilience policy,
  success-status validation, a response-size limit, and bounded JSON parsing;
- extracts a configured check key rather than hard-coding `memories-search`;
- fail-closes missing/malformed/non-boolean data;
- maps enabled + reachable + not-degraded to Available, disabled to LocalOnly,
  and unreachable/degraded/non-success/malformed to a bounded Degraded or
  LocalOnly result according to the public contract;
- does not expose exception messages, endpoint secrets, raw response JSON,
  tenant data, or downstream diagnostics to the UI;
- leaves Parties/Memories search semantics and result hydration in Parties.

### Alternatives considered

- **Keep all helpers Parties-local:** rejected because the behavior is reusable
  platform plumbing and Story 8.8/8.9 explicitly target shared consolidation.
- **Put all nine capabilities in Commons:** rejected because Commons must not
  own Blazor/Fluent UI, MCP authorization, claim precedence, EventStore UI
  semantics, or search UX.
- **Treat current FrontComposer foundations as full parity:** rejected. They are
  useful evidence, but the picker, record freshness, download, typed-name mode,
  outbound relay, deep-link builder, and bounded capability probe are absent.
- **Delete local code when producer tests pass:** rejected. Parties consumer
  parity, exact release/pin identity, and tested rollback are mandatory.

## 5. Detailed Planning Edits After Approval

No edit in this section is applied until the proposal is approved.

### 5.1 Story 8.3 G4 prerequisite row

Artifact:
`_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md`

**OLD:** one FrontComposer UI row names the parity set, lists dependent stories
`8.8, 8.9, 8.10`, and says the G4 primitives are missing without distinguishing
existing skip-link/live-region foundations.

**NEW:**

- keep status `needs-additive-api`;
- name `Hexalith.FrontComposer.Contracts.UI` and
  `Hexalith.FrontComposer.Shell` as producers;
- record G4-A through G4-E as additive work and G4-F as existing-surface parity
  certification unless evidence requires a narrow additive customization seam;
- replace the evidence paths with the current FrontComposer foundations plus
  the Parties-local picker, freshness, download, typed-confirmation, and shell
  rollback surfaces;
- set dependent stories to `8.9, 8.10`; G4 is not the Story 8.8 gate;
- keep all Parties-local surfaces until the exact FrontComposer identity and
  Story 8.9 consumer parity are accepted;
- change to `available` only when all six packages have named owner approval,
  producer tests, exact package version/root gitlink, Parties bUnit/Playwright
  parity, and rollback evidence.

Routing is not delivery and does not change the row status.

### 5.2 Story 8.3 G11 prerequisite row

Artifact:
`_bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md`

**OLD:** one `Hexalith.FrontComposer and Hexalith.Commons` row requests MCP auth
and tenant relay, deep links, and a search probe, but leaves package ownership,
trust boundaries, and proof criteria unresolved.

**NEW:**

- keep status `needs-additive-api`;
- name `Hexalith.FrontComposer.Mcp`, FrontComposer Shell/Contracts.UI, and
  `Hexalith.Commons.Http` with the boundaries in Section 2;
- record G11-A through G11-C and their security/bounds requirements;
- replace the evidence paths with the FrontComposer MCP context/accessor and DI
  gates, Commons.Http foundations, and the Parties-local relay, deep-link, and
  search-probe rollback surfaces;
- keep dependent stories `8.8, 8.10`;
- change to `available` only after named producer approvals, exact package
  versions/root gitlinks, public API/package validation, producer tests, Parties
  MCP/AdminPortal consumer parity, and tested rollback are recorded.

Routing is not delivery and does not change the row status.

### 5.3 Story 8.9 spec

Artifact:
`_bmad-output/implementation-artifacts/spec-8-9-ui-frontcomposer-and-fluent-consolidation.md`

- Replace the undifferentiated G4 Block-If text with G4-A through G4-F and the
  exact producer identity/evidence gate.
- State that skip links already exist in FrontComposer Shell and require parity
  certification, not an assumed duplicate implementation.
- Add file/JSON download and public live-region policy scenarios to the I/O
  matrix.
- Expand tasks and ACs to cover record freshness, polite/assertive mapping,
  typed-name exact match, object-URL/anchor cleanup, payload no-log/no-storage,
  skip-link focus targets, and picker durable-ID/custom-event parity.
- Keep the current Parties controls and JavaScript helpers as rollback until
  each migrated slice passes bUnit and `ui-a11y`.

### 5.4 Story 8.8 spec

Artifact:
`_bmad-output/implementation-artifacts/spec-8-8-client-mcp-apphost-build-and-deploy-cleanup.md`

- If not already applied through the G8 proposal, replace the stale “G12 is
  blocked” statement and AC with the factual package-publication resolution from
  2026-07-11. Apply that factual correction once if both proposals are approved.
- Replace the generic G11 Block-If with G11-A through G11-C, producer ownership,
  exact identity, and Parties consumer proof.
- Add `McpContextForwardingHandler.cs`, `PartiesMcpRequestContext.cs`,
  `AdminPortalEventStoreAdminLinks.cs`, and
  `PartiesAdminPortalApiClient.GetRichSearchCapabilityAsync` to the explicit
  local rollback/code map.
- Expand the 8.8b MCP/integration slice to adopt the shared relay, link builder,
  and capability probe independently; a delivered sub-surface does not unblock
  deletion of another.
- Add ACs for server-authoritative tenant/user context, API-key credential
  separation, replace-not-append headers, no-secret diagnostics, safe links,
  bounded health responses, cancellation, malformed/non-success fallbacks, and
  the stable five MCP tool contracts.

### 5.5 Sprint action routing

Artifact: `_bmad-output/implementation-artifacts/sprint-status.yaml`

**OLD:**

```text
Route G4 + G11 to Hexalith.FrontComposer (+ Commons): additive
FcEntityPicker<T>, per-record freshness indicator, live-region politeness,
file/JSON download service, typed-name destructive dialog, skip links (G4,
blocks 8.9); MCP auth + tenant header relay, EventStore Admin UI deep-link
builder, search-capability health probe (G11, blocks 8.8).
```

Owner is `Sally (UX Designer) + Winston (Architect) +
Hexalith.FrontComposer owners`; status is `open`.

**NEW:** retain the concise action summary, link this proposal, name the
FrontComposer Contracts.UI/Shell/MCP and Commons.Http owners, and set the
routing action to `in-progress`. Stories 8.8/8.9 remain `backlog`; the action
becomes `done` only after both matrix rows record approved producer delivery and
Parties consumer proof.

## 6. Validation and Exit Criteria

### G4 becomes `available` only when

1. G4-A through G4-E have named FrontComposer owner approval and G4-F has an
   owner-approved existing-parity or narrow-additive decision.
2. Public API snapshots/package validation identify the exact Contracts.UI and
   Shell release versions or root-declared submodule pin.
3. Producer bUnit tests cover picker keyboard/focus/stale-response behavior,
   record freshness, live-region mapping, typed confirmation, download cleanup,
   localization, forced-colors, reduced motion, and skip-link targets.
4. Parties consumer tests prove durable party-ID selection,
   `party-selected` compatibility, safe filenames, no payload echo/log/storage,
   exact erasure confirmation, current accessibility copy, and no focus steal.
5. Parties `ui-a11y` evidence is green under the existing SSR/CI split.
6. The local G4 rollback surfaces remain until the migrated slices are accepted.

### G11 becomes `available` only when

1. G11-A through G11-C have named FrontComposer/Commons owner approval.
2. Exact FrontComposer.Mcp, FrontComposer Shell/Contracts.UI, and Commons.Http
   release versions or root-declared submodule pins are recorded.
3. Public API/package validation and producer tests prove single-value safe
   header replacement, server-authoritative context, API-key separation,
   credential-provider failure, missing/ambiguous context, cancellation, and
   no-secret diagnostics.
4. URI tests cover base paths/queries, encoding, blank values, unsafe schemes,
   user-info, and malformed configuration.
5. Capability-probe tests cover success, disabled, degraded, non-success,
   timeout/cancellation, oversized JSON, malformed JSON, missing check, and
   wrong-typed fields without exposing raw downstream data.
6. Parties MCP tests preserve exactly five tool names and prove tenant/auth
   relay through the shared path; AdminPortal tests prove current link and rich
   search capability behavior.
7. The local G11 rollback surfaces remain until Story 8.8 consumer parity is
   accepted.

Expected Parties verification after actual producer delivery includes:

```text
dotnet build Hexalith.Parties.slnx -c Release -m:1
pwsh scripts/test.ps1 -Lane unit
pwsh scripts/test.ps1 -Lane topology
PLAYWRIGHT_SKIP_WEBSERVER=1 npm run test:a11y
```

Relevant test executables must be run directly according to the repository
lane rules; exact producer commands and final consumption identities belong in
the owner proof and Story 8.8/8.9 implementation records.

## 7. Correct-Course Checklist Result

| Checklist area | Result |
|---|---|
| Triggering stories and issue | Complete — Stories 8.8/8.9, shared API and ownership gap |
| Epic/story impact | Complete — Epic 8 viable; no resequencing or new epic |
| PRD/UX impact | No change; approved UX is the parity target |
| Architecture impact | No change; current invariants/readiness gate are sufficient |
| Technical path | Viable — direct adjustment with nine producer work packages |
| Scope classification | Moderate |
| Rollback | All Parties-local G4/G11 surfaces retained until consumer parity |
| Handoff | Architect/UX route; FrontComposer/Commons deliver; Parties validates |
| Approval | Approved by Administrator on 2026-07-16 |

## 8. Handoff

- **Winston / architecture owner:** approve the package boundaries, route all
  nine work packages, and keep both matrix rows fail-closed.
- **Sally / UX owner:** approve G4 interaction/accessibility parity and the G11
  UI-facing capability/deep-link behavior.
- **FrontComposer Contracts.UI/Shell owners:** deliver G4-A through G4-E and
  certify G4-F; publish the exact consumable identity.
- **FrontComposer MCP owner:** deliver G11-A without weakening current fail-closed
  admission and visibility gates.
- **Commons.Http owner:** deliver only the approved domain-neutral portions of
  G11-A through G11-C; do not absorb MCP or UI policy.
- **Parties owner/developer:** apply the planning edits only after approval;
  consume only after the applicable row is `available`; preserve local rollback
  until consumer evidence is green.

Approval was recorded on 2026-07-16. The authorized Parties planning-artifact
edits are applied by this correct-course workflow; production code and submodule
content remain unchanged.
