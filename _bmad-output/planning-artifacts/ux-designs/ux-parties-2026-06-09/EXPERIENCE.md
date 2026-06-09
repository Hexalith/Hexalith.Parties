---
name: Parties UI
status: final
sources:
  - {planning_artifacts}/../project-context.md
updated: 2026-06-09
---

# Parties UI — Experience Spine

> Behavioral contract for `parties-ui`: a single Blazor app, two role-gated areas
> (**Admin** records management + **Consumer** GDPR self-service), on the
> FrontComposer shell + FluentUI Blazor V5. Visual identity lives in `DESIGN.md`;
> this spine owns *how it works*. **Spine wins on conflict with any mock.**

## Foundation

`parties-ui` is a **single responsive web app** (Blazor) hosting two role-gated
areas under one FrontComposer shell — **Admin** (manage Person/Organization party
records within tenant scope) and **Consumer** (self-service over one's own
personal data). One sign-in; **role decides the landing area** (`Admin` /
`TenantOwner` → Admin; `Consumer` → Consumer). The UI system does most of the
work: the shell supplies layout, navigation, theme (Light/Dark/System), density,
command palette, and skip links; FluentUI V5 supplies the components. Brand
discipline is "respect the defaults except where `DESIGN.md` overrides them."
Visual identity (color, type, spacing, components) is the `DESIGN.md` reference;
this spine specifies only the behavioral delta. **Defaults:** Admin density
*comfortable*, Consumer density *roomy*; theme follows the OS until the user
chooses.

This product sits in front of an **event-sourced, CQRS, eventually-consistent**
backend (commands route through the EventStore gateway; reads come from replayed
projections carrying `ProjectionFreshnessMetadata`). That single fact shapes more
of this spine than anything visual — see **State Patterns**.

## Information Architecture

| Surface | Area | Reached from | Purpose |
|---|---|---|---|
| **Sign in** | Shell | App entry (unauthenticated) | Authenticate; role routing to landing |
| **Parties list** | Admin | Nav → Parties (`/admin/parties`) | Search/filter Person + Organization records |
| **Party detail** | Admin | List row (`/admin/parties/{id}`) | View one record; entry to edit + GDPR |
| **Create / Edit party** | Admin | List toolbar / detail action | Author a party (validated → command) |
| **GDPR operations** | Admin | Detail → GDPR (`/admin/parties/{id}/gdpr`) | DPO: erase · restrict · consent · export · processing records · verify |
| **My profile** | Consumer | Consumer landing | View own personal data |
| **Edit my profile** | Consumer | My profile → Edit | Correct/update own data |
| **My consent** | Consumer | Nav → Consent | Grant / withdraw consent, plain-language |
| **My data & privacy** | Consumer | Nav → Privacy | Export my data · request erasure · see what's processed about me |

**Navigation model:** the shell's `<FluentNav>` auto-populates from registered
domain manifests, gated by `<AuthorizeView Policy=…>` — Admin nav entries never
render for a Consumer, and vice-versa. Sidebar (220px) on desktop; hamburger
drawer on tablet/phone. Modal depth ≤ 1 (a confirm dialog never stacks on a
dialog). The `<hexalith-party-picker>` is **not a top-level surface** — it is an
inline control reused inside Admin Create/Edit to link a related party.

→ Composition reference:
[`mockups/signin.html`](mockups/signin.html) (sign-in + role routing) ·
[`mockups/admin-parties.html`](mockups/admin-parties.html) (Admin master-detail + GDPR) ·
[`mockups/create-edit-party.html`](mockups/create-edit-party.html) (Create/Edit + in-form picker) ·
[`mockups/consumer-profile.html`](mockups/consumer-profile.html) (My profile view/edit) ·
[`mockups/consumer-privacy.html`](mockups/consumer-privacy.html) (My data & privacy).
**Spine wins on conflict.**

## Voice and Tone

Microcopy only — brand voice lives in `DESIGN.md.Brand & Style`. Two registers,
one product: **Admin is terse and precise** (operator language); **Consumer is
plain and reassuring** (no legalese, no jargon, no blame). The hard rule for
Consumer GDPR copy: **say what will happen in human words, then name the right.**

| Do | Don't |
|---|---|
| "We've started deleting your data. We'll confirm when it's done — usually within 30 days." | "It'll be gone within 30 days." (a hard SLA GDPR Art. 12(3) lets you extend — don't commit a finish time) |
| "You can cancel until deletion begins. Once it's done, it's permanent — we can't undo it." | State only the cancel window and hide that completed deletion is irreversible |
| "Delete my data" / "Withdraw consent" / "Grant consent" (plain verbs) | "Erasure" / "Toggle data-processing authorization flag" |
| Split "Things you control" (consent) from "Things we keep to run your account" (contract / legal) | One list with a toggle on every row, implying you can switch off a contract/legal basis |
| For a legitimate-interest basis, offer **Object** (Art. 21), not a withdraw toggle | Show a withdraw toggle on a basis the user can't actually withdraw |
| "Preparing your export — we'll have a download ready here. We'll show it when it's done." (machine-readable) | "Ready in a moment / under a minute" (over-promises an async job; no format stated) |
| Admin: "Erase party — irreversible. Type the name to confirm." | Admin: a bare "Are you sure?" |
| "Showing what we last knew — refreshing…" (stale read) | "Stale projection / cache miss" |
| "We couldn't reach the service. Your data is safe; try again." | "500 Internal Server Error" |

## Component Patterns

Behavioral rules. Visual specs live in `DESIGN.md.Components` (or FluentUI V5
defaults, when inherited).

| Component | Use | Behavioral rules |
|---|---|---|
| **Parties data grid** (`FluentDataGrid`) | Admin list | Server-driven search (debounced) + type/active filters via `FluentSelect`; row → detail; never block render on staleness (show freshness, render last-known). |
| **Party picker** (`<hexalith-party-picker>`) | Admin link-a-party | Implements the full **WAI-ARIA combobox pattern**: input `role=combobox` + `aria-expanded` + `aria-controls`→listbox; listbox `role=listbox`, options `role=option` with `aria-selected`, active tracked by `aria-activedescendant` (not color alone). `aria-autocomplete=list`, 300ms debounce; selecting fires `party-selected` `{partyId, partyType, status}` (`composed:true`); honor its state machine (Idle/Loading/Ready/Empty/LocalOnly/Degraded/Unauthorized/Forbidden/TransientFailure/NotFound/Gone/Error). **Design debt:** re-skin to Fluent 2 tokens + add the ARIA wiring above (see DESIGN.md). |
| **Party-state badge** | Both | Lifecycle (`active/inactive/restricted/erased`) — color **and** text label, never color alone. An `erased` party shows tombstone copy, not data. |
| **Consent control** | Consumer | Real `role=switch` + `aria-checked` + visible label tied to its purpose/lawful-basis via `aria-describedby` — never a styled `<div>`. **Defaults Off; never pre-checked** (GDPR Art. 7). Optimistic: flip + announce "Saving…" via `aria-live=polite` — **do not move focus to a toast on save**; reconcile on projection confirm; on rejection revert + inline reason. Only **consent-based** items get a toggle; contract/legitimate-interest data is read-only (with an **Object** action where applicable). |
| **GDPR action button** | Both | Irreversible actions (erase) use the danger fill + **typed confirmation** in a real labeled input; reversible (restrict, withdraw) use outline + single confirm. Never auto-fire on focus/blur. |
| **Freshness indicator** | Both | Surfaces `ProjectionFreshnessMetadata`: fresh / stale ("as of HH:MM") / degraded ("showing last known"). Dot + word (never color alone). Pairs with an `aria-live=polite` announcement on transition. |
| **Command result toast** | Both | Success/processing → `role=status aria-live=polite` "Saved — updating…". **Validation rejection / failure → `role=alert` (assertive)** with the reason — not polite. An *erasure* acknowledgement uses a neutral/info tone, **never success-green** (deleting data isn't a "success" celebration). Never a blocking `alert()`/`confirm()`. |

## State Patterns

The defining section. The backend is **eventually consistent and at-least-once**;
the UI must make that legible without alarming anyone. States map to the real
`StatusKind` / `PartyPickerSearchState` enums already in the codebase.

| State | Surface | Treatment |
|---|---|---|
| **Cold load** | All | `FluentDataGrid`/panel skeleton; no spinner-only screens. |
| **Empty** (`NoData`) | List, search | "No parties match." + clear-filters action; never a dead end. |
| **Display-name-only** (`DisplayNameOnly`) | Detail | Partial projection: show the name, mark the rest "still loading," don't imply the record is empty. |
| **Accepted-but-processing** | After any command | Optimistic echo + freshness `degraded`/`info` dot + toast "Saved — updating…". The view the user just acted on reflects their change immediately; the projection reconciles silently. **This is the core eventual-consistency UX.** |
| **Stale read** (`Degraded`) | Any read | Banner/dot "Showing what we last knew — refreshing" using `freshness.stale`; **render the last-known cache, never throw, never blank the screen.** Auto-refresh; `aria-live` announces when fresh. |
| **Validation rejected** (`Validation`) | Create/Edit, consent | Inline field error from the `PartyCommandValidationRejected` event (not an exception); **announced via `role=alert` (assertive)**, error tied to field via `aria-describedby`; preserve the user's input; offer retry. |
| **Erasure requested / in progress** | Consumer privacy, Admin GDPR | Two honest states: **(a) cancellable** until deletion begins — "You can cancel until deletion begins"; **(b) permanent** once complete — "Done — permanently deleted, can't be undone." Neutral/info tone, never success-green. Don't present the 30-day figure as the cancel window. |
| **Transient failure** (`TransientFailure`) | Any | "We couldn't reach the service. Your data is safe." + Retry; exponential backoff; keep prior content visible. |
| **Load failure** (`LoadFailure`) | Any | Non-transient: explain + Retry + support path; never a raw stack/500. |
| **Sign-in required** (`SignInRequired`) | Any | Route to sign-in, preserve return URL. |
| **Tenant unavailable** (`TenantUnavailable`) | Admin | Tenancy is **fail-closed + eventually consistent**: after a restart it denies `UnknownTenant` until tenant events replay. Copy: "Your workspace is still warming up — try again shortly," **not** "access denied." |
| **Admin required / Forbidden** (`AdminRequired`/`Forbidden`) | Admin | Explain the role needed; never expose record data; offer the Consumer area if applicable. |
| **Erased / Gone** (`Gone`/`NotFound`) | Detail, picker | Tombstone: "This party was erased." No personal fields, no PII in the message; links resolve gracefully (subscribers cleaned up dangling refs). |

## Interaction Primitives

- **Pointer + keyboard parity** everywhere; **touch** on tablet/phone (≥44px targets).
- **Inherited global keys** (shell): `Ctrl+K` command palette, `Ctrl+,` settings,
  skip links (to content, to nav) as first tab stops.
- **Grid:** arrow-key row navigation, `Enter` opens detail, type-ahead into search.
- **Picker (combobox):** `↓/↑` move options (drives `aria-activedescendant`),
  `Enter` selects, `Esc` closes, `Backspace` on empty clears selection.
- **Forms:** `Enter` submits single-field steps; explicit Save button for
  multi-field; **destructive actions require typed confirmation** in a real input,
  never `Enter`.
- **Focus management:** on **dialog** open trap focus, on close restore to the
  trigger; on **blocking** errors move focus to the alert. For **non-blocking,
  optimistic** results (consent save, "Saved — updating…") **announce via
  `aria-live` only — do not steal focus to a toast** (it's disruptive when it fires
  on every quiet save). Pattern already in AdminPortal — extend it, don't over-apply it.
- **Banned:** native `alert()`/`confirm()`/`prompt()` dialogs (they block the
  Blazor event loop — use `FluentDialog`/toasts); color-only state; auto-submit of
  destructive actions on blur; spinner-only screens; throwing on stale reads.

## Accessibility Floor

Behavioral. Visual contrast lives in `DESIGN.md`. **Note (1.4.3):** Fluent 2 status
*token pairs* and the brand **background** token (`--colorBrandBackground`) meet AA,
but the **raw teal accent base `#0097A7` is 3.51:1 on white — non-text use only**;
filled primary buttons with white text bind to `--colorBrandBackground` (see
DESIGN.md). Target: **WCAG 2.2 AA** (consumer-facing).

- **Announcement politeness is split (4.1.3):** status + freshness + "Saved —
  updating…" use `role="status" aria-live="polite"`; **validation rejections,
  failures, and other errors use `role="alert"` (assertive)** — never silently
  polite. Extend the existing AdminPortal `aria-live` pattern to the Consumer area
  and the freshness indicator.
- **Real semantics, not styled divs (4.1.2):** the consent control is `role=switch`
  + `aria-checked`; the Person/Organization chooser is a `radiogroup`; the picker
  uses the combobox roles (see Component Patterns); the typed-erase confirm is a
  labeled `<input>`. No interactive `<div>`s.
- Full keyboard operability; visible `--colorStrokeFocus2` ring never suppressed;
  logical focus order; skip links functional; focus moved only where it helps (see
  Interaction Primitives — not on every optimistic save).
- Support **forced-colors / high-contrast** (`@media (forced-colors: active)`) and
  **`prefers-reduced-motion`** **product-wide** (today only the picker honors them).
- State is never communicated by color alone (party-state badge always carries a
  text label; freshness dot always carries a word).
- GDPR consent and erasure controls are reachable, labeled (`aria-describedby` ties
  the purpose/lawful-basis text to the control), and confirmable by keyboard.
- Interactive targets meet **2.5.8** (≥24px; aim ≥44px on touch); consumer body
  type at 16px (`DESIGN.md` `body-consumer`).

## Responsive & Platform

| Breakpoint | Behavior |
|---|---|
| **Desktop (≥1024px)** | Admin master–detail side-by-side; sidebar nav 220px; Consumer single centered column. |
| **Tablet (640–1023px)** | Nav collapses to hamburger drawer; Admin detail becomes an overlay/sheet over the list; density may step down. |
| **Phone (<640px)** | Single column; Admin list → tap → full-screen detail (back returns to list); Consumer is the primary mobile experience (privacy/consent designed phone-first). |

Admin is **desktop-first** (data-dense, used at a desk) but degrades cleanly;
Consumer is **mobile-friendly first** (people check their privacy on a phone,
often after a prompting email). One responsive codebase, two density postures.

## Inspiration & Anti-patterns

- **Lifted from FrontComposer + Fluent 2:** the entire surface vocabulary — layout,
  nav, theme/density, command palette, skip links, components. Parties UI's
  contribution is *what it adds for the party + GDPR domain*, not a new design
  system. Deliberate posture, not a shortcut.
- **Lifted from the existing AdminPortal:** the `StatusKind` state machine,
  `aria-live` status regions, and explicit focus management — these are good; make
  them product-wide (esp. into the new Consumer area).
- **Rejected — legalese to consumers:** GDPR rights are stated in plain language;
  policy detail is one click away, never an inline wall of text.
- **Rejected — consent dark patterns:** no pre-ticked consent (Art. 7), no
  confirm-shaming, no asymmetry making "keep my data / stay opted-in" easier than
  "withdraw / delete." Withdraw is as easy as grant; delete is as findable as keep.
- **Rejected — blocking/alarming on eventual consistency:** stale reads render the
  last-known value with a quiet "refreshing" cue; we never blank the screen, throw,
  or shout "stale."
- **Rejected — a separate consumer brand:** one Fluent family, differentiated by
  density and copy register, not by a second visual identity.
- **Rejected — native modal dialogs** (`alert`/`confirm`): they freeze the Blazor
  event loop; all confirmation is in-app (`FluentDialog` + typed confirm).

## Key Flows

_Visual reference: Flows 1–2 → [`mockups/consumer-privacy.html`](mockups/consumer-privacy.html) + [`mockups/consumer-profile.html`](mockups/consumer-profile.html); Flow 3 → [`mockups/admin-parties.html`](mockups/admin-parties.html); Flow 4 → [`mockups/create-edit-party.html`](mockups/create-edit-party.html). Entry: [`mockups/signin.html`](mockups/signin.html)._

### Flow 1 — Marc checks what's held about him (Marc, customer, Sunday 9pm, on his phone)

1. Marc gets an email: "Review your data." He taps the link and signs in on his phone.
2. Role routes him to the **Consumer** landing. App opens **My profile** at roomy
   density, 16px body — calm, readable. His name and details render; a small green
   freshness dot reads "Up to date."
3. He taps **My data & privacy**. Two honest groups: **Things you control**
   (consent, off by default) and **Things we keep to run your account** (contract /
   legitimate interest, read-only with an *Object* action), plus two actions —
   *Export my data*, *Delete my data*.
4. He taps **Export my data**. Toast: "Preparing your export — this can take a
   little while. We'll show it here the moment it's ready." The portability job runs
   server-side, producing a machine-readable (JSON) file.
5. **Climax:** the export readies and a download appears — *his own data, in his
   hands, in a portable file he can keep or move elsewhere, without a ticket or a
   call to support*. The right stopped being abstract.

Failure: export service unreachable → `TransientFailure` toast "We couldn't build
your export just now. Your data is safe — try again." Retry with backoff; the
request is not lost.

### Flow 2 — Marc withdraws a consent (Marc, customer, two minutes later)

1. From privacy, Marc taps **My consent**. Each consent shows its purpose in plain
   words and a toggle.
2. He flips "Marketing emails" **off**. The toggle flips immediately and reads
   "Saving…" (optimistic) — the command is on its way through the gateway.
3. The projection confirms; the toggle settles to "Off — you won't get marketing
   emails," and the freshness dot returns to green. An `aria-live` region announces
   the change for his screen reader.
4. **Climax:** Marc didn't wait, didn't reload, didn't doubt it took. The system
   *looked* done the instant he acted, and *was* done a breath later — eventual
   consistency made invisible. He closes the tab.

Failure: command rejected (`Validation`) → toggle reverts to On, inline reason
"We couldn't change this — please try again," input/intent preserved.

### Flow 3 — Priya fulfills an erasure request (Priya, data steward / DPO, Tuesday 10:40am)

1. A subject erasure request lands. Priya opens **Parties** in the Admin area at a
   desk, desktop master-detail.
2. She searches the name; the grid filters as she types. The matching person row
   shows an `active` state badge. She opens the detail.
3. She clicks **GDPR**, landing on `/admin/parties/{id}/gdpr` — erasure status,
   processing records, consent, portability, verification.
4. She clicks **Erase party** (danger fill). A `FluentDialog` requires her to
   **type the person's name** to confirm — no accidental erasure.
5. The command is accepted; the detail shows the party-state badge flip toward
   `erased` with "Saved — updating…", freshness `degraded` while the crypto-shred
   propagates.
6. **Climax:** minutes later Priya opens the **erasure verification report** and
   sees the record confirmed shredded across projections — *she can prove the
   right was honored*, not just assert it. Audit closed.

Failure: tenant projection still warming after a restart → `TenantUnavailable`,
copy "Your workspace is still warming up — try again shortly," never a hard denial
that looks like a permissions bug.

### Flow 4 — Priya links a contact to an organization (Priya, onboarding a new org, Wednesday)

1. Priya creates an Organization party, then needs to attach its primary contact
   Person.
2. In the Edit form she focuses the **party picker**; she types "acme cfo." After
   300ms it queries and shows matching people (`Ready`).
3. She arrows down, presses `Enter`. The picker emits `party-selected`
   `{partyId, partyType:"Person", status:"active"}`; the form binds the link.
4. **Climax:** the relationship is captured inline without leaving the form or
   memorizing an id — the picker turned "find the right person among thousands"
   into three keystrokes and an Enter.

Failure: search backend degraded → picker enters `Degraded`/`LocalOnly`, shows
last-known matches with a quiet "limited results" note; selection still works,
nothing blocks.
