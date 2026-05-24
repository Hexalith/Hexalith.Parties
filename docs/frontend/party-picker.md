# Embeddable Party Picker

`Hexalith.Parties.Picker` is a Parties-owned Razor class library for host applications that need party search and selection without building a custom selector. It consumes party reads through `Hexalith.Parties.Client` so host applications can point the client at the EventStore gateway configuration.

## Blazor Usage

Register the picker services and provide the Parties client configuration. `Parties:BaseUrl` is the EventStore gateway base URL, not a Parties actor-host REST endpoint.

```csharp
builder.Services.AddHexalithPartyPicker(builder.Configuration);
```

Render the component with host-owned authentication:

```razor
<PartyPicker ContextKey="@($"{TenantContextVersion}:{UserContextVersion}")"
             AuthContextKey="@AccessTokenVersion"
             AccessTokenProvider="GetAccessTokenAsync"
             SearchMode="hybrid"
             PageSize="10"
             SelectedPartyChanged="OnPartySelected" />
```

The selected value contract is the stable party id. Display names and status labels are preview data only and must not become route keys, storage keys, telemetry dimensions, log values, or durable foreign keys.

For Blazor hosts, bind durable storage to `SelectedPartyId`/`SelectedPartyIdChanged`. The source-compatible `SelectedPartyChanged` callback may include preview display fields for rendering convenience, but `PartyId` is the only stable value in that model and preview fields are not required for host persistence.

## JavaScript Host Usage

Register the custom element in the Blazor host:

```csharp
builder.RootComponents.RegisterHexalithPartyPickerCustomElement();
```

Then configure it from the JavaScript host:

```html
<hexalith-party-picker id="party-picker"
                       search-mode="hybrid"
                       page-size="10"></hexalith-party-picker>
```

```js
const picker = document.getElementById("party-picker");
picker.accessToken = await getTokenFromHost();
picker.contextKey = `${tenantContextVersion}:${userContextVersion}`;
picker.authContextKey = accessTokenVersion;
picker.addEventListener("party-selected", event => {
  const selectedPartyId = event.detail.partyId;
});
```

The DOM `party-selected` detail intentionally contains only `partyId`, `partyType`, and bounded status. It does not include tenant ids, JWTs, search text, names, contact values, identifiers, consent text, degraded reasons, raw query payloads, or backend problem details.

## Search Behavior

The picker normalizes the type-ahead text by trimming whitespace and removing control characters. The minimum query is one visible non-control character; empty, whitespace-only, or control-character-only queries do not call the backend and render the localized idle state.

For visible queries, the picker caps `pageSize` at `100` and calls `IPartiesQueryClient.SearchPartiesAsync(query, page, pageSize, cancellationToken, mode, caseId, requestCustomizer)`. The picker package must not construct old Parties REST URLs, call DAPR actors, or reach into Parties server/projection internals.

Search results are displayed from the bounded visible page only. When the typed client provides consistent total-count metadata, the status text can say `Showing {visible} of {total} matching parties`. If total-count metadata is absent, negative, or inconsistent with the visible page, the picker falls back to `Showing {visible} matching parties` and does not render raw backend count metadata.

No-result searches render `No matching parties in the current authorized context`. The wording is intentionally tenant-safe: it does not imply records exist outside the host-supplied authorization context.

While a search request is in flight, the picker clears the previous result list and renders the localized loading status instead of presenting old results as current. Delayed responses are accepted only for the current query, host context, authentication context, search options, and page-size state; responses from superseded contexts are ignored.

Unauthorized, forbidden, not-found, gone/erased, transient, unavailable, malformed, local-only, and degraded search states render bounded localized status text through the component status region. Backend problem details, correlation ids, tenant ids, access tokens, query payloads, and stale party display data are not rendered. Local-only and degraded results remain usable only as bounded current-context results and are identified by text, not color alone.

Retry is shown only for retryable search failures. Activating it reissues the current safe request context using the latest host-supplied token provider/request customizer and returns focus to the initiating input when the retry completes.

The picker does not call Hexalith.Memories directly and does not emulate semantic, hybrid, graph, email, or identifier search in the browser.

Until Story 12.5 exposes/freeze rich search metadata through the typed client, metadata unavailable from `IPartiesQueryClient` is treated as bounded unavailable state. The picker must not fabricate local-only, degraded, semantic, hybrid, graph, email, or identifier matching details.

## Shell Boundary And Layout

The picker is a single, bounded embeddable search-and-selection control. It is not an admin portal, party editor, tenant selector, GDPR surface, or EventStore stream browser. Host applications embed it inside their own layouts; it renders one compact control surface (label, input row, optional selected preview, polite status region, and an optional results listbox).

`Disabled` and `ReadOnly` are independent host inputs:

- `Disabled` removes interaction entirely (the input is disabled, no search is issued, and the clear control is inert).
- `ReadOnly` keeps the input keyboard-reachable but does not mutate search or selection.
- In both states, a host-supplied current selection (`SelectedPartyId`) keeps its display present and accessible.

The compact layout contract lives in `PartyPicker.razor.css`: a bounded `max-width`, a stable input row, long display names that wrap (`overflow-wrap: anywhere`) instead of overflowing, visible `:focus-visible` outlines, and status that is conveyed by text (not color alone). The clear control is an accessible icon button: its accessible name comes from the localized `ClearSelection` label while the visible `×` glyph is decorative (`aria-hidden`).

## Accessibility And Localization

The picker exposes a labeled search input, localized result-list name, polite atomic status region, localized retry and clear controls, native keyboard-operable result buttons, and a named selected-party region. Search results expose selected state through `aria-selected`; each option includes localized party-type and state text so selected, inactive, erased, local-only, degraded, unavailable, retryable, and loading states are not color-only.

Every user-facing picker string is supplied by `PartyPickerLabels` or by the host-provided `Labels` parameter. This includes labels, placeholders, result-list names, status messages, count summaries, retry and clear text, selected-display state text, and party-type display text. Hosts can replace these labels with FrontComposer/localized values without changing the durable selection contract.

The status region is intentionally bounded and privacy-safe. It announces loading, empty, error, retry, degraded/local-only, selected-display, and result-count changes without rendering tokens, tenant ids, query payloads, backend problem details, contact data, identifiers, correlation ids, or stale display data.

The stylesheet includes visible `:focus-visible` outlines, forced-colors focus/border handling, reduced-motion guards, and no CSS-generated state labels. State indicators are rendered as text from labels instead of relying on color, pseudo-content, animation, or host-specific visual tokens.

## Privacy And State

Hosts must provide either an access-token provider, an in-memory token property for the custom element, or a request customizer. The picker does not refresh tokens and does not persist tokens.

Use `ContextKey` to represent tenant, signed-in user, and host configuration changes. Use `AuthContextKey` for a non-sensitive authentication version when the host uses a token provider whose delegate instance does not change. When either key changes, the picker clears visible results, selected preview data, and pending requests before searching again.

When a host supplies `SelectedPartyId`, the picker resolves display preview state through `IPartiesQueryClient.GetPartyAsync` using the same host-supplied access token provider or request customizer pattern as search. If the selected party is unavailable, unauthorized, forbidden, not found, gone, erased, or transiently unavailable, the component keeps the durable party id and renders a bounded localized selected-state label instead of replacing the id with display text or backend details.

Selected-party display lookup also has a localized loading label. If a selected-display response arrives after `SelectedPartyId`, `ContextKey`, `AuthContextKey`, token identity, request customizer identity, disabled/read-only state, or picker options change, it is ignored. Retry for selected-party transient/unavailable failures re-resolves only the current selected id and current host request context, then returns focus to the selected display region.

`ApiBaseUrl` remains on the component for source compatibility with existing hosts, but request routing is owned by the configured `Hexalith.Parties.Client` service. Do not point `ApiBaseUrl` at a Parties actor-host REST endpoint or rely on it for transport selection.

The component renders all party data, labels, degraded states, and problem summaries through normal Razor text rendering. Do not pass raw HTML labels or templates that render untrusted values with `MarkupString`, `AddMarkupContent`, `innerHTML`, or unsafe markdown.

## Theming

The picker uses CSS custom properties that can be mapped to FrontComposer or Fluent UI tokens:

```css
hexalith-party-picker {
  --neutral-stroke-rest: var(--neutral-stroke-rest);
  --neutral-layer-1: var(--neutral-layer-1);
  --neutral-foreground-rest: var(--neutral-foreground-rest);
  --accent-fill-rest: var(--accent-fill-rest);
}
```

No changes to the `Hexalith.FrontComposer` submodule are required.
