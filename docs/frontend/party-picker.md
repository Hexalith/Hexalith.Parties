# Embeddable Party Picker

`Hexalith.Parties.Picker` is a Parties-owned Razor class library for host applications that need party search and selection without building a custom selector. It consumes the existing Parties REST read API only.

## Blazor Usage

Register the picker services and provide an `HttpClient` owned by the host application:

```csharp
builder.Services.AddHttpClient();
builder.Services.AddHexalithPartyPicker();
```

Render the component with host-owned authentication:

```razor
<PartyPicker ApiBaseUrl="https://parties.example"
             ContextKey="@($"{TenantContextVersion}:{UserContextVersion}")"
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
                       api-base-url="https://parties.example"
                       search-mode="hybrid"
                       page-size="10"></hexalith-party-picker>
```

```js
const picker = document.getElementById("party-picker");
picker.accessToken = await getTokenFromHost();
picker.contextKey = `${tenantContextVersion}:${userContextVersion}`;
picker.addEventListener("party-selected", event => {
  const selectedPartyId = event.detail.partyId;
});
```

The DOM `party-selected` detail intentionally contains only `partyId`, `partyType`, and status. It does not include tenant ids, JWTs, search text, names, contact values, identifiers, or backend problem details.

## Search Behavior

The picker calls `GET /api/v1/parties/search` with bounded `q`, `page`, `pageSize`, and only explicitly configured `mode` or `caseId` parameters. `pageSize` is capped at the backend maximum of `100`; the default type-ahead page size is `10`.

Empty or invisible-only queries do not call the backend. The picker does not call Hexalith.Memories directly and does not emulate semantic, hybrid, graph, email, or identifier search in the browser.

When the API returns `X-Parties-Search-Status`, `X-Parties-Search-Degraded-Reason`, `X-Service-Degraded`, or `X-Stale-Data-Age`, the picker preserves the metadata internally and renders bounded local-only/degraded states without printing raw backend exception text.

## Privacy And State

Hosts must provide either an access-token provider, an in-memory token property for the custom element, or a request customizer. The picker does not refresh tokens and does not persist tokens.

Use `ContextKey` to represent tenant, signed-in user, and host configuration changes. When it changes, the picker clears visible results, selected preview data, and pending requests before searching again.

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
