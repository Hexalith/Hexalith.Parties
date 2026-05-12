---
project: Hexalith.Parties
date: 2026-05-12
scope: FR67 party picker
status: approved
---

# Party Picker UX Addendum

The party picker is an embeddable FrontComposer/Blazor component for party search and selection. It is not an admin portal, party editor, tenant selector, GDPR surface, or EventStore stream browser.

## Required Behavior

- Debounced type-ahead display-name search.
- Bounded result count and stable compact layout.
- Selected party id callback to the host application.
- Disabled and read-only states.
- Loading, empty, retry, degraded/local-only, unauthorized, forbidden, not-found, gone/erased, and transient-failure states.
- Stale-response clearing when token, tenant, user, host configuration, selected id, or search options change.
- Keyboard operation, visible focus, screen-reader naming, localized labels/status text, and non-color-only status.
- Encoded rendering only for party data, host labels, backend messages, degraded reasons, and localized values.

## Durable Selection Contract

The durable selection contract is the party id. Display names, contact values, identifiers, consent text, degraded reasons, search text, tenant ids, tokens, raw ProblemDetails, and raw query payloads must not become durable host keys or event payloads.

## Privacy Rules

Names, contacts, identifiers, consent text, search text, tenant ids, tokens, raw ProblemDetails, and raw query payloads must not be placed in storage keys, telemetry dimensions, URLs, logs, filenames, DOM event names, or JavaScript event payloads.

## Integration Boundary

The picker queries through the EventStore-fronted Parties client boundary. It must not call retired Parties REST endpoints, admin endpoints, DAPR actors, projection actors, local search services, controllers, or actor-host internals.

Host applications provide request/auth context through the accepted Parties client/EventStore gateway configuration. The picker does not persist, refresh, parse for authorization, or log tokens.
