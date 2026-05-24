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

The DOM `party-selected` detail intentionally contains only `partyId`, `partyType`, and status. It does not include tenant ids, JWTs, search text, names, contact values, identifiers, or backend problem details.

## Search Behavior

The picker normalizes the type-ahead text, caps `pageSize` at `100`, and calls `IPartiesQueryClient.SearchPartiesAsync(query, page, pageSize, cancellationToken)`. The picker package must not construct old Parties REST URLs, call DAPR actors, or reach into Parties server/projection internals.

Empty or invisible-only queries do not call the backend. The picker does not call Hexalith.Memories directly and does not emulate semantic, hybrid, graph, email, or identifier search in the browser.

Until Story 12.5 exposes/freeze rich search metadata through the typed client, metadata unavailable from `IPartiesQueryClient` is treated as bounded unavailable state. The picker must not fabricate local-only, degraded, semantic, hybrid, graph, email, or identifier matching details.

## Shell Boundary And Layout

The picker is a single, bounded embeddable search-and-selection control. It is not an admin portal, party editor, tenant selector, GDPR surface, or EventStore stream browser. Host applications embed it inside their own layouts; it renders one compact control surface (label, input row, optional selected preview, polite status region, and an optional results listbox).

`Disabled` and `ReadOnly` are independent host inputs:

- `Disabled` removes interaction entirely (the input is disabled, no search is issued, and the clear control is inert).
- `ReadOnly` keeps the input keyboard-reachable but does not mutate search or selection.
- In both states, a host-supplied current selection (`SelectedPartyId`) keeps its display present and accessible.

The compact layout contract lives in `PartyPicker.razor.css`: a bounded `max-width`, a stable input row, long display names that wrap (`overflow-wrap: anywhere`) instead of overflowing, visible `:focus-visible` outlines, and status that is conveyed by text (not color alone). The clear control is an accessible icon button: its accessible name comes from the localized `ClearSelection` label while the visible `×` glyph is decorative (`aria-hidden`).

## Privacy And State

Hosts must provide either an access-token provider, an in-memory token property for the custom element, or a request customizer. The picker does not refresh tokens and does not persist tokens.

Use `ContextKey` to represent tenant, signed-in user, and host configuration changes. Use `AuthContextKey` for a non-sensitive authentication version when the host uses a token provider whose delegate instance does not change. When either key changes, the picker clears visible results, selected preview data, and pending requests before searching again.

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
