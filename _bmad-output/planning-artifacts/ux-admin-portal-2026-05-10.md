# UX Specification: Parties Admin Portal

Date: 2026-05-10  
Story: 12.7 Admin Portal Rebuild on FrontComposer

## Scope

The Parties Admin Portal is a FrontComposer domain surface for Parties-specific administration. It does not include a landing page, marketing hero, duplicated tenant management, or a generic EventStore stream browser. Generic event and stream inspection is delegated to EventStore Admin UI through safe deep-links.

Production readiness is blocked until Wave 1 behavior is landed or formally frozen and Story 12.5 exposes the typed Parties client/query/command contract. Until then the portal may show operational shell, specification-backed layouts, and fail-closed contract-unavailable states.

## Route Map

| Route | Owner | Purpose |
|---|---|---|
| `/admin/parties` | Parties Admin Portal | Search, browse, and inspect Parties records. |
| `/admin/parties/{partyId}` | Parties Admin Portal | Deep-linkable party detail when FrontComposer route support is available. |
| `/admin/parties/{partyId}/gdpr` | Parties Admin Portal | Compact GDPR operation panel for one party. |
| EventStore Admin UI stream/correlation links | EventStore Admin UI | Generic stream, event, correlation, and command-status inspection. |

## Information Architecture

The first viewport is the working console: global search, compact filters, tenant/auth state, result grid, and detail panel are visible without an introductory page. The list and detail regions are siblings, not nested card shells. Repeated records may use compact rows; detail sections remain dense, titled bands.

Primary regions:

| Region | Contents |
|---|---|
| Toolbar | Search input, party type filter, active filter, retry, EventStore Admin UI availability indicator. |
| Results | Server-side paged or virtualized grid with display name, type, active/erased/restricted state, created/modified dates, and non-PII status indicators. |
| Detail | Selected party summary, contacts, identifiers, consent, restriction, erasure, processing records, and safe EventStore Admin UI links. |
| GDPR drawer/panel | Action-specific confirmations and outcomes for erasure, restriction, consent, portability, and processing records. |
| Status region | Screen-reader announced bounded states for loading, empty, blocked, forbidden, timeout, degraded, malformed response, and stale response. |

## Navigation And Deep-Links

Parties-domain navigation stays inside FrontComposer. Selecting a row updates detail state and, when the shell supports it, updates the route with a non-PII party id. The portal never places names, emails, identifiers, consent purposes, free text, or raw ProblemDetails details in URLs, storage keys, telemetry dimensions, or link labels.

EventStore Admin UI links are generated only from safe identifiers: stream id, aggregate id, command id, correlation id, or timestamp. Labels use generic text such as `Open stream`, `Open command status`, and `Open correlation`. If the EventStore Admin UI URL is unavailable, controls are disabled with a bounded reason and no fallback browser is embedded.

## List And Detail Layout

The list supports browse, display-name search, rich-search capability gating, type and active filters where the accepted query contract supports them, server-side paging, stale response suppression, and tenant-switch cancellation. Search mode disables unsupported filters rather than silently sending ignored filters.

The detail panel clears on sign-out, missing tenant, non-admin, tenant switch, stale in-flight response, forbidden, not found, gone/erased, timeout, malformed response, or contract-unavailable failures. Erased parties show terminal privacy-preserving state only; stale personal detail is removed before the state is rendered.

## GDPR Operation Flows

GDPR actions are shown only when the accepted EventStore command/client contract is available. Until Story 12.5 exposes those methods, each action is disabled with the dated blocker: `Blocked on Story 12.5 EventStore Parties client contract`.

Required flows after the contract exists:

| Flow | UX Contract |
|---|---|
| Erasure request | Confirmation with party id only, command accepted outcome, refresh authoritative erasure status through EventStore query. |
| Erasure status and certificate | Poll/refresh action, safe verification result, certificate download filename from party id plus timestamp only. |
| Verification retry | Enabled only for supported partial or failed verification states. |
| Restrict/lift restriction | Bounded reason input, command accepted outcome, refresh before follow-on actions. |
| Consent add/revoke/history | Per channel and per purpose, no party-wide or tenant-wide consent shortcut. |
| Portability export | Generates safe filename, content type, and payload through accepted query/client path. |
| Processing records | Read-only list with bounded summaries and safe correlation links. |

## Empty And Error States

| State | Operator State |
|---|---|
| Missing token | Clear all sensitive state and route to sign-in affordance. |
| Missing tenant | Clear list/detail/GDPR/export state and ask for tenant context. |
| Non-admin | Clear sensitive state and show admin-required state. |
| Tenant switch | Cancel in-flight work, reset selected row, clear caches, then reload for the new tenant. |
| Forbidden or cross-tenant | Clear current data and avoid confirming whether a party exists. |
| Not found | Clear detail and show unavailable state. |
| Gone/erased | Clear personal detail and show erased/no-longer-inspectable state. |
| Degraded | Preserve only data proven to belong to the current tenant and show non-color-only degraded indicator. |
| Timeout or transient failure | Clear stale detail for detail loads; allow retry for list loads. |
| Malformed/non-JSON response | Treat as load failure or auth redirect depending on classifier; never render raw body. |
| Contract unavailable | Disable unsupported reads/actions and show the exact Story 12.4/12.5 blocker. |

## Localization

All labels, status messages, dates, booleans, counts, warning copy, validation messages, lawful-basis labels, and operation outcomes must come from localized resource strings or the FrontComposer localization seam. ProblemDetails title/detail text may be displayed only as bounded encoded text and must not be used as a localization key.

## Accessibility

All controls are keyboard reachable. Search returns focus to the input after filter changes, grid rows expose names and state through accessible labels, detail panel headings define landmarks, retry and deep-link controls restore focus after completion, dialogs trap focus, and status changes are announced through a polite status region. Status is never color-only; icons or text labels accompany degraded, erased, restricted, and blocked states.

## Privacy Rules

The portal renders user, backend, AI-created, and operator-entered content only through normal Razor/component text paths. It must not use raw markup, JavaScript interpolation, raw HTML fragments, personal data in filenames, PII cache discriminators, PII telemetry dimensions, raw ProblemDetails details in logs, raw command/query payloads, JWTs, claims dictionaries, tenant membership payloads, sidecar names, or DAPR ports.

Safe logs may include operation category, non-PII id, correlation/status id, bounded outcome code, and retry category.
