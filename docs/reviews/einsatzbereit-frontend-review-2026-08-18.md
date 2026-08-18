# Frontend & UX Review - einsatzbereit - 2026-08-18

Reviewed: https://einsatzbereit.maik-hasler.de - Commit (repo `main` at review time): `6f3ddda`

## Executive Summary

Einsatzbereit is in better frontend shape than most projects of its size, and the review kept turning up
things that were already done right rather than things that were broken. Colour contrast passes WCAG 2.2 AA
on every public page, every logged-in page and the Keycloak theme, with zero real failures across the
automated sweep. Keyboard support is genuinely good: a skip link leads the tab order, focus order follows
visual order, and every focusable control carries a visible 2px brand outline. `prefers-reduced-motion` is
respected the safe way (keyframes gated behind `no-preference` in `global.css:19`). The i18n layer is
strong: DE/EN key parity is exact at 1333 keys each, switching language preserves the route, the query
string, the value in the search input and the result count, and dates reformat per locale. The
create-opportunity wizard is a model of how to build a modal: real dialog semantics, focus moved into the
form, a polite live region announcing the step, `aria-required`, `aria-invalid` and `role="alert"` errors on
submit.

The problems that remain cluster in three places. First and most serious, the deployed
Content-Security-Policy silently disables the app's own session-restore machinery: `frame-src` omits
`'self'`, so the OIDC silent-renew and silent-SSO iframes are blocked on the final hop back to `/callback`.
The consequence is user-visible and was reproduced directly: a second tab shows "Anmelden/Registrieren"
despite a live Keycloak SSO session, and an expiring access token bounces the user to the login screen
instead of renewing. Second, the organizer dashboard, which is Olaf's landing screen, devotes its largest
widget to an empty calendar week scrolled to the small hours, and ships a "Einstellungen" widget whose
entire content is an unlabelled gear glyph. Third, a scattering of content and layout defects: same-day time
ranges print the date twice, the sign-ups grid squeezes cards to 256px inside an 800px container, and the
offline story is thorough on `/opportunities` but entirely absent on the homepage.

Top risks and opportunities, in order: **(1)** fix the CSP `frame-src` so sessions survive (one line, restores
a shipped feature that is currently inert); **(2)** give the organizer dashboard a defensible default layout;
**(3)** collapse same-day date ranges; **(4)** bring the homepage's offline behaviour up to the standard the
browse page already sets; **(5)** make the sign-ups grid respond to its container rather than the viewport.

## Scope & Method

**Tooling.** No MCP browser tools resolved in this session, so live verification ran through throwaway
Playwright scripts per the `/live-verify` recipe (scratch directory, deleted after use). **Single browser
engine: Chromium only** - no WebKit or Gecko pass, so engine-specific rendering issues are out of this
report's reach.

**Personas.** All three documented staging accounts were exercised: `vera` (volunteer), `olaf`
(volunteer + organizer), `admin`. Anonymous/logged-out state covered separately.

**Viewports.** 1440x900 (desktop), 768x1024 (tablet), 375x812 (mobile, `isMobile` + `hasTouch`,
`deviceScaleFactor: 2`).

**Languages.** German (default) and English, switched through the header control. EN verified on
`/`, `/opportunities`, `/organizations`, `/help`, `/contact`, `/imprint`, `/privacy-policy`.

**Screens covered.** Public: home, browse/`/opportunities` (incl. filters and query state), organizations
directory, help, contact, imprint, privacy policy, terms, 404, Keycloak login (success + wrong-password
error state). Vera: profile with badges, sign-ups (upcoming + past tabs), settings. Olaf: organizer
dashboard, org switcher, create-opportunity wizard step 1 with validation, tab navigation. Admin:
organizations, users, reports, audit log.

**Cross-cutting checks run.** Contrast sweep computing real composited sRGB (canvas-resolved colours, alpha
composited up the ancestor chain, gradient-backed text excluded as uncomputable); accessible-name audit over
every button/link/`role=button`; heading outline per page; `alt` presence; horizontal-overflow detection at
each viewport; 26-step tab walk with focus-ring measurement; `prefers-reduced-motion: reduce` emulation;
PWA manifest and service-worker inspection plus a real offline pass with `setOffline(true)`.

**Excluded by scope.** Backend logic, data model, infrastructure, security vulnerabilities, CI, performance
tooling, code hygiene and dead code - these belong to the existing `lens` / `einsatzbereit-review` lenses.
No diff or PR review. Per `AGENTS.md`, the shared staging test credentials are a documented, accepted
trade-off and are not treated as a finding.

**Not reached.** A browse-level map view does not exist in the product (`SingleMarkerMap` is used only on the
opportunity detail page, `VolunteerOpportunityDetailPage.tsx:58`), so the "Kartenansicht" area of the brief
had no surface to review beyond the detail-page map. Image upload error paths (wrong type, oversized),
CSV export, invitation acceptance and the check-in/rating flows were not driven to completion, to avoid
leaving debris on shared staging.

**Data hygiene.** No test data was created and nothing was deleted or modified on staging. The
privilege-change flow in the admin area was reviewed from source rather than clicked, specifically to avoid
mutating another account.

## Findings

### UX

#### F1 - CSP `frame-src` omits `'self'`, so silent SSO and silent token renewal never complete

**Kategorie:** UX (Auswirkung), Ursache in der Frontend-Auslieferungskonfiguration
**Schweregrad:** Kritisch
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #1 Sichtbarkeit des Systemstatus und #3 Nutzerkontrolle; zusätzlich projekteigene Konvention: `useSilentSsoProbe.ts` dokumentiert dieses Verhalten als das Ziel von #1929)
**Ort:** alle Routen (jeder Seitenaufruf) - Persona: alle - Viewport: alle - Sprache: DE/EN

Beleg: `docs/reviews/assets/2026-08-18/f1-silent-sso-second-tab-logged-out.jpg` - a second tab in the same
browser, with all four Keycloak SSO cookies present (`AUTH_SESSION_ID`, `KC_AUTH_SESSION_HASH`,
`KEYCLOAK_SESSION`, `KEYCLOAK_IDENTITY`), renders "Anmelden / Registrieren" while the first tab is logged in.

The console error on that load is decisive, because it carries an authorization code:

```
Refused to frame 'https://einsatzbereit.maik-hasler.de/callback?state=...&session_state=...&code=9b3c0717-...'
because it violates the following Content Security Policy directive: "frame-src https://login.maik-hasler.de".
```

Keycloak authenticated the silent request and issued the code; the CSP killed the last hop that delivers it
back to the app.

Code:
- `frontend/nginx.conf.template:15` - `frame-src ${CSP_KEYCLOAK_ORIGIN};` with no `'self'`.
- `frontend/src/main.tsx:33` - `redirect_uri: window.location.origin + "/callback"`. oidc-client-ts defaults
  `silent_redirect_uri` to `redirect_uri`, so the silent iframe navigates to a **same-origin** URL.
- `frontend/src/main.tsx:36` - `automaticSilentRenew: true`.
- Live response header confirms the deployed value: `frame-src https://login.maik-hasler.de`.

Auswirkung: Two shipped behaviours are inert in production. Opening the app in a new tab shows a logged-out
header even though the SSO session is live - exactly the gap `useSilentSsoProbe` was written to close.
More damaging, `automaticSilentRenew` cannot renew an expiring access token, so an ordinary working session
ends by throwing the user back to the login screen. This was hit accidentally during the review: a page
opened with a token minutes old navigated away mid-load rather than refreshing. Any unsaved form state, for
example a half-filled create-opportunity wizard, is lost with it.

Verbesserungsvorschlag: Add `'self'` to `frame-src` in `frontend/nginx.conf.template:15`
(`frame-src 'self' ${CSP_KEYCLOAK_ORIGIN};`). `frame-ancestors 'none'` already prevents the app from being
framed by third parties, so this does not widen clickjacking exposure. Worth pairing with an assertion in
`frontend/scripts/check-nginx-csp.js` and extending the `frontend-checks.yml` CSP job (which today only
greps for the Keycloak origin, `frontend-checks.yml:209`) so the same-origin token cannot regress out again.
- Aufwand: S

#### F5 - Organizer dashboard opens on an empty calendar week scrolled to the small hours

**Kategorie:** UX
**Schweregrad:** Hoch
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #1 Sichtbarkeit des Systemstatus, #8 Ästhetisches und minimalistisches Design)
**Ort:** `/app/:organizationId/dashboard` - Persona: Olaf - Viewport: 1440px (auch 375px) - Sprache: DE/EN

Beleg: `docs/reviews/assets/2026-08-18/f2-org-dashboard-empty-calendar.jpg`

The "Kalender" widget is the largest element on the organizer's landing screen (measured 816px tall, 681px
wide). It opens in week view on 17.08.2026 - 23.08.2026, renders all 24 hour rows starting at 00:00, and is
scrolled such that 00:00 to 15:00 fills the visible area. The organisation's only upcoming Einsatz is on
27.08.2026, outside this week. The result is a large, completely blank grid with no "no events this week"
message, occupying the most valuable space on the page.

Auswirkung: Olaf's first impression of his own dashboard is an empty grid of night-time hours. Nothing tells
him whether the calendar is empty because there is nothing scheduled, because it is still loading, or
because something failed. The one piece of information he most likely wants, the next Einsatz, is present on
the page but relegated to a smaller widget below the fold.

Verbesserungsvorschlag: Three changes, cheapest first. (1) Scroll the week/day view to business hours on
mount (react-big-calendar's `scrollToTime`, e.g. 07:00) so the visible band can actually contain events.
(2) Render an explicit empty state inside the calendar when the visible range has no events, using the same
`EmptyState` component the browse page already uses. (3) Consider defaulting to month view, or opening on
the week containing the next upcoming Einsatz rather than always on today. - Aufwand: M

#### F6 - "Einstellungen" dashboard widget is a heading and a bare gear glyph

**Kategorie:** UX
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #6 Wiedererkennen statt Erinnern)
**Ort:** `/app/:organizationId/dashboard` - Persona: Olaf - Viewport: 1440px und 375px - Sprache: DE/EN

Beleg: `docs/reviews/assets/2026-08-18/f2-org-dashboard-empty-calendar.jpg` (desktop, 158px wide card) and
`docs/reviews/assets/2026-08-18/f3-settings-widget-bare-gear-375.jpg` (mobile, full-width card whose entire
content is one centred gear icon).

Auswirkung: The widget states a category and then shows a decorative glyph. It does not say which settings,
does not read as a control, and gives no hint whether clicking does anything. On mobile it is worse, because
a full-width card is spent on a single icon. The neighbouring "Einsatz erstellen" widget shows how this
should look: a heading plus a labelled primary button.

Related and cheap to fix in the same pass: that neighbouring widget has the heading "Einsatz erstellen"
directly above a button also labelled "Einsatz erstellen", which is redundant (NN #8).

Verbesserungsvorschlag: Give the settings widget the same shape as the create widget: a labelled control
("Einstellungen öffnen" or a set of direct links to the settings sections most used). For the create
widget, drop either the heading or the button label so the pair is not stated twice. - Aufwand: S

#### F7 - Offline: the homepage silently drops a whole section, while the browse page handles it properly

**Kategorie:** UX
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #1 Sichtbarkeit des Systemstatus, #4 Konsistenz und Standards)
**Ort:** `/` gegenüber `/opportunities` - Persona: alle - Viewport: alle - Sprache: DE/EN

Beleg: compare `docs/reviews/assets/2026-08-18/f6-offline-home-section-silently-missing.jpg` with
`docs/reviews/assets/2026-08-18/f7-offline-opportunities-good-state.jpg`. Both were captured with the
service worker active and the network forced offline after warming the cache.

On `/opportunities` the app does this well: a dedicated offline empty state with an icon, the heading
"Du bist offline" and an explanation of what happens next. On `/` the entire "Diese Einsätze suchen Leute"
section, including its heading and its "Alle Einsätze ansehen" link, simply vanishes, leaving a gap between
the hero and the "Für Organisationen" band.

Code: `frontend/src/pages/HomePage.tsx` contains no offline or error handling at all (a grep for
`offline`/`isOffline`/`error` returns nothing), whereas
`frontend/src/components/VolunteerOpportunitiesList/OpportunityResultsList.tsx:126-129` renders an explicit
offline `RouteState`.

Auswirkung: The homepage is the most likely entry point for a returning PWA user on a poor connection. A
section disappearing without explanation reads as the app being broken or the platform having no
opportunities, rather than as a connectivity state. It also contradicts the standard the browse page sets
two clicks away.

Verbesserungsvorschlag: Reuse the existing offline `RouteState` in the homepage's opportunities section, so
the heading stays and the body is replaced by the same offline message the browse page shows. - Aufwand: S

#### F8 - Logged-in volunteers land on the anonymous marketing homepage

**Kategorie:** UX
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Präferenz (verwandt mit Nielsen-Norman-Heuristik #7 Flexibilität und Effizienz)
**Ort:** `/` - Persona: Vera (mit zwei bestätigten Anmeldungen) - Viewport: alle - Sprache: DE/EN

Beleg: `docs/reviews/assets/2026-08-18/f1-silent-sso-second-tab-logged-out.jpg` (tab 1 header shows the
signed-in avatar) against the identical page body captured for the anonymous visit. Apart from the header
swapping "Anmelden/Registrieren" for the avatar and bell, the page is byte-for-byte the same, including the
"Sucht deine Organisation Freiwillige?" band, the "Organisation erstellen" call to action and the founder
story.

Auswirkung: Vera has a confirmed Einsatz on 20.08.2026 with a check-in available, and the landing page tells
her none of it. Every visit costs an extra navigation to `/my-signups` to see her own state.

Verbesserungsvorschlag: For authenticated volunteers, lead with a compact "your next Einsatz" block above
the recently-published list, keeping the acquisition sections below or dropping them. This is a product
decision rather than a defect, hence the low severity. - Aufwand: M


### Barrierefreiheit

#### F3 - The upcoming/past switcher is styled as tabs but has no tab semantics

**Kategorie:** Barrierefreiheit
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (WAI-ARIA Authoring Practices, Tabs pattern; ergänzt axe-core/jsx-a11y, die
  ein Button-Paar nicht als Tabs erkennen und daher nichts melden)
**Ort:** `/my-signups` - Persona: Vera, Olaf - Viewport: alle - Sprache: DE/EN

Beleg: `docs/reviews/assets/2026-08-18/f4-signup-cards-cramped-and-date-repeated.jpg` shows the segmented
"Aktuell & Bevorstehend / Vergangen" control. A DOM query for `[role="tablist"]`, `[role="tab"]` and
`[role="tabpanel"]` returns zero matches on this page (and on every other page audited). The controls are
plain `<button>` elements inside `inline-grid grid-cols-2`
(`frontend/src/pages/MyEngagementsPage/ActivitySection.tsx:349`).

Auswirkung: The control looks and behaves like a tab set for sighted mouse users, but a screen reader
announces two ordinary buttons with no grouping, no "selected" state and no position ("1 of 2"). Keyboard
users get no arrow-key navigation between them, and nothing associates the buttons with the list they
control. Which view is currently active is conveyed by colour and background alone in the accessibility
tree.

Verbesserungsvorschlag: Either adopt the full ARIA tabs pattern (`role="tablist"` on the wrapper,
`role="tab"` + `aria-selected` + roving `tabindex` on the buttons, `role="tabpanel"` +
`aria-labelledby` on the list) or, if arrow-key navigation is not wanted, keep plain buttons and add
`aria-pressed` to each so the active state is at least exposed. The repo already has the honest-semantics
precedent for the second option in `frontend/src/components/Header/LanguageSelector.tsx:30-37`. - Aufwand: M

#### F4 - Duplicate page title as both `h1` and `h2`

**Kategorie:** Barrierefreiheit
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (WCAG 2.2 AA, 2.4.6 Headings and Labels - Überschriften sollen den Abschnitt beschreiben)
**Ort:** `/my-signups` - Persona: Vera, Olaf - Viewport: alle - Sprache: DE/EN

Beleg: heading outline captured on the page is
`["H1: Meine Anmeldungen", "H2: Meine Anmeldungen", "H2: Bereit, wenn du es bist.", ...]`.

Auswirkung: A screen-reader user navigating by heading hears the same label twice in a row and cannot tell
the hero from the content section. `/profile` gets this right in the same layout ("Mein Profil" as `h1`,
then "Profil-Details" and "Abzeichen" as `h2`), so the inconsistency is internal.

Verbesserungsvorschlag: Make the content-section heading describe its content, or render it visually hidden
only if it exists purely to label the list. - Aufwand: S


### Content & i18n

#### F9 - Same-day time ranges print the date twice

**Kategorie:** Content
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #8 Ästhetisches und minimalistisches Design; ISO 8601 / gängige Konvention für Zeiträume am selben Tag)
**Ort:** `/my-signups` und `/volunteer-opportunities/:id` - Persona: alle - Viewport: alle - Sprache: DE/EN

Beleg: `docs/reviews/assets/2026-08-18/f4-signup-cards-cramped-and-date-repeated.jpg`
("Termin: 20.08.2026, 10:00 - 20.08.2026, 14:00") and
`docs/reviews/assets/2026-08-18/f5-detail-page-date-repeated-and-card-variant.jpg`
("27.08.2026, 09:00 - 27.08.2026, 17:00").

Two fundorte, one cause - both concatenate two full `formatDateTime` calls unconditionally:
- `frontend/src/pages/MyEngagementsPage/ActivitySection.tsx:479`
- `frontend/src/pages/VolunteerOpportunityDetailPage.tsx:436`

The shared string is `myEngagements.scheduledFor` = `"Termin: {{range}}"`, with the range assembled at the
call site rather than by a range formatter.

Auswirkung: The single most important fact on a volunteering card, when do I need to be there, is stated in
the most verbose form available. On the sign-ups cards this is also what pushes the date onto three wrapped
lines (see F2), so the two defects compound. A volunteer scanning several commitments has to read the same
date twice per row to extract "10:00 to 14:00".

Verbesserungsvorschlag: Introduce a `formatDateTimeRange(start, end, locale)` helper in
`frontend/src/lib/format.ts` that omits the second date when both fall on the same calendar day
("20.08.2026, 10:00 - 14:00" / "20 Aug 2026, 10:00 - 14:00") and keeps both when they do not. Use it at both
call sites. `Intl.DateTimeFormat.formatRange()` does exactly this natively and is locale-correct for free.
- Aufwand: S

#### F10 - Offline message restates its own heading

**Kategorie:** Content
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (`/frontend-design` Schreibregeln: jedes Element erfüllt genau eine Aufgabe, kein Element macht heimlich doppelte Arbeit)
**Ort:** `/opportunities`, `/organizations` (Offline-Zustand) - Persona: alle - Viewport: alle - Sprache: DE/EN

Beleg: `docs/reviews/assets/2026-08-18/f7-offline-opportunities-good-state.jpg` - heading "Du bist offline",
body "Du bist offline. Sobald deine Verbindung zurück ist, laden wir die Einsätze."

Code: `frontend/src/locales/de.json:1219` (`routeState.offline.title` = "Du bist offline") is rendered
together with `de.json:239` (`opportunities.offline`) and `de.json:107` (`organizations.offline`), each of
which opens by repeating the title. The English strings have the same shape. The team is clearly aware of
the pattern: a comment at
`frontend/src/components/VolunteerOpportunitiesList/OpportunityResultsList.tsx:82` refers to avoiding "body
copy above the offline notice that already says it".

Auswirkung: Minor, but it is the one weak spot in an otherwise exemplary empty state, and it costs the
reader a line before reaching the part that actually helps.

Verbesserungsvorschlag: Trim the body strings to the informative half - "Sobald deine Verbindung zurück
ist, laden wir die Einsätze." / "We will load the opportunities as soon as your connection is back." - and
let the title carry the status. - Aufwand: S

#### F11 - Browse result count switches vocabulary between filtered and unfiltered states

**Kategorie:** Content
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #4 Konsistenz und Standards; `/frontend-design`: eine Aktion behält ihren Namen durch den ganzen Flow)
**Ort:** `/opportunities` - Persona: alle - Viewport: alle - Sprache: DE/EN

Beleg: `docs/reviews/assets/2026-08-18/f8-browse-result-count-copy.jpg` shows the unfiltered state,
"9 Einsätze geladen, weitere verfügbar."; applying the query `?q=Tier` changes the same line to
"6 Einsätze gefunden."

Code: `frontend/src/locales/de.json` - `opportunities.resultCountPartial` versus
`opportunities.resultCount` (and the English equivalents).

Auswirkung: The same line alternates between the user's mental model ("gefunden" / found, a result count)
and the system's ("geladen" / loaded, a pagination detail). "Weitere verfügbar" also withholds the one
number that would help, the total, so the user cannot tell whether "Mehr laden" yields three more or three
hundred.

Verbesserungsvorschlag: Use one verb throughout, and give the total when it is known:
"9 von 24 Einsätzen" / "9 of 24 opportunities". If the total is genuinely unavailable from the endpoint,
keep "gefunden" in both variants so at least the vocabulary is stable. - Aufwand: S

#### F12 - German label "Ablauf" for the participation type is ambiguous

**Kategorie:** i18n / Content
**Schweregrad:** Niedrig
**Konfidenz:** Wahrscheinlich
**Einordnung:** Präferenz (gestützt auf Nielsen-Norman-Heuristik #2 Übereinstimmung mit der realen Welt)
**Ort:** `/volunteer-opportunities/:id` - Persona: alle - Viewport: alle - Sprache: DE

Beleg: `docs/reviews/assets/2026-08-18/f5-detail-page-date-repeated-and-card-variant.jpg` - the "at a
glance" strip reads "WANN / Einmalig", "ABLAUF / Zeitslots", "WO / Karl-Heine-Straße 12".

Code: `frontend/src/locales/de.json` `opportunities.factFormat` = "Ablauf"; the English counterpart is
"How it works". Rendered at `frontend/src/pages/VolunteerOpportunityDetailPage.tsx:674`.

Auswirkung: In German "Ablauf" most commonly means either a sequence of events or an expiry. Sitting beside
"WANN" and above a value like "Zeitslots", and on a platform whose cards elsewhere say "Interesse bekunden
bis <Datum>", the expiry reading is easy to reach and wrong. The English label "How it works" is
noticeably clearer than the German original, which is unusual for this codebase and suggests the German
string simply predates the better English one.

Verbesserungsvorschlag: Use "So läuft es ab" or "Teilnahme" instead of "Ablauf". - Aufwand: S


### UI

#### F2 - Sign-ups grid breaks at the viewport, not at its container, squeezing cards to 256px

**Kategorie:** UI
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #8 Ästhetisches und minimalistisches Design)
**Ort:** `/my-signups` - Persona: Vera, Olaf - Viewport: 1280px und breiter - Sprache: DE/EN

Beleg: `docs/reviews/assets/2026-08-18/f4-signup-cards-cramped-and-date-repeated.jpg`

Code: `frontend/src/pages/MyEngagementsPage/ActivitySection.tsx:271`, `:384` and `:424` all use
`grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3`, while the page container at
`frontend/src/pages/MyEngagementsPage/index.tsx:25` is `max-w-5xl` with an `11rem` side rail and a `3rem`
gap. At a 1440px viewport the `xl:` breakpoint fires on the viewport, but the track it divides is only
about 800px wide, giving roughly 256px per card.

Auswirkung: At the widest layout the cards are at their most cramped. Titles wrap to two lines, the
organisation name wraps, and the date line breaks across three lines, while roughly 40 percent of the row
sits empty. The volunteer's own commitments are the hardest thing on the page to scan, and the "Deine
Nachricht" quote is line-clamped to a fragment with no way to expand it.

Verbesserungsvorschlag: Switch these three grids to container queries (`@container` with
`@lg:grid-cols-2` etc., Tailwind 4 supports this natively) so the columns respond to the 800px track rather
than the viewport, or simply drop the `xl:grid-cols-3` step so the container never divides below a readable
card width. - Aufwand: S

#### F13 - Two different card compositions for the same entity

**Kategorie:** UI
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #4 Konsistenz und Standards)
**Ort:** `/opportunities` gegenüber dem Abschnitt "Weitere Einsätze dieser Organisation" auf `/volunteer-opportunities/:id` - Persona: alle - Viewport: alle - Sprache: DE/EN

Beleg: `docs/reviews/assets/2026-08-18/f8-browse-result-count-copy.jpg` (browse cards) against
`docs/reviews/assets/2026-08-18/f5-detail-page-date-repeated-and-card-variant.jpg` (related cards, lower
third).

The browse card orders: category chip + frequency chip, participation badge right-aligned, title, date line,
description, then an organisation footer with avatar and location. The related card orders: category chip +
participation badge, frequency on its own row with a calendar icon, title, description, then a bare address
line with no organisation footer and no date. Card titles are also `h2` on the browse page but `h3` on the
homepage for the same component.

Auswirkung: A user who has learned to scan the browse cards, where the date sits directly under the title,
has to re-learn the layout on the detail page, where that position holds the description and no date appears
at all. The inconsistent heading level additionally makes the document outline differ between two pages
showing the same content.

Verbesserungsvorschlag: Render both through one card component with a density or variant prop, and pass the
heading level in so the outline stays correct per page. - Aufwand: M

#### F14 - Horizontally scrollable regions on mobile give no scroll affordance

**Kategorie:** UI
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Präferenz (gestützt auf Nielsen-Norman-Heuristik #6 Wiedererkennen statt Erinnern)
**Ort:** `/app/:organizationId/dashboard` - Persona: Olaf - Viewport: 375px - Sprache: DE/EN

Beleg: `docs/reviews/assets/2026-08-18/f3-settings-widget-bare-gear-375.jpg`

Measured: the organizer tab strip is `clientWidth` 343 against `scrollWidth` 474, and the calendar's agenda
table is `clientWidth` 307 against `scrollWidth` 480. Both are correctly wrapped in `overflow-x: auto`
containers and both scroll properly when swiped - the page itself never overflows
(`documentElement.scrollWidth === clientWidth === 375` on every mobile page tested). The issue is only that
nothing signals the hidden content: "Einstellungen" is clipped mid-word at the right edge and "Mitglieder"
is fully off-screen, with no gradient, chevron or partial-item peek.

Auswirkung: Two organizer sections are reachable but not obviously discoverable on a phone. A clipped word
is a weak affordance, and the agenda table clips the event title ("Erste-Hilfe-K") in the same way.

Verbesserungsvorschlag: Add a fade mask or a small chevron on the overflowing edge while the container has
unscrolled content. Note this was initially suspected to be a hard clipping bug and is not - the regions do
scroll; only the signalling is missing. - Aufwand: S

#### F15 - Dashboard skeletons do not resemble the content they stand in for

**Kategorie:** UI
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (WCAG 2.2 AA verwandt: 2.2.x Stabilität; primär Layout-Shift-Best-Practice)
**Ort:** `/app/:organizationId/dashboard` - Persona: Olaf - Viewport: 1440px - Sprache: DE/EN

Beleg: captured during load, the "Kalender" widget shows four stacked grey bars in a card whose final
content is a 24-row time grid; "Freiwillige" shows a small chip skeleton for what resolves to a single large
number and caption. Widget heights measured once loaded (816px calendar, 712px upcoming, 348px volunteers)
against skeleton content occupying roughly the top 130px.

Auswirkung: The loading state promises a short list and delivers a tall grid, so the page jumps
substantially as widgets resolve. It is a brief effect but affects the organizer's landing screen on every
visit.

Verbesserungsvorschlag: Shape each skeleton to its widget - a grid-shaped placeholder for the calendar, a
single large block for the stat widget - so the reserved space matches what arrives. - Aufwand: S

#### F16 - Admin role badge uses a warning colour for a neutral fact

**Kategorie:** UI
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Präferenz
**Ort:** `/administration/users` - Persona: Admin - Viewport: alle - Sprache: DE/EN

Beleg: `docs/reviews/assets/2026-08-18/f10-admin-role-badge-colour.jpg` - the "Admin" role badge renders
amber while every status badge on the same rows ("Aktiv") renders green.

Auswirkung: Amber reads as a warning or a degraded state. Here it marks a role, which is neither. In a list
whose other badges use colour to encode account health, the mixed semantics make the row harder to parse at
a glance.

Verbesserungsvorschlag: Give role badges a neutral or brand-tinted treatment distinct from the
status-colour scale, so colour consistently means "status" on this screen. - Aufwand: S

## What was checked and found sound

Recorded so future passes do not re-litigate them, and because several are notably better than typical.

- **Colour contrast (WCAG 2.2 AA).** Zero genuine failures across `/`, `/opportunities`, `/organizations`,
  `/help`, `/contact`, `/imprint`, the opportunity detail page, `/profile`, `/my-signups`,
  `/profile/settings`, the organizer dashboard, all four admin sections, and the Keycloak login theme
  (including its error state). The only entries the sweep flagged were an artefact of the transparent header
  measured against the page background instead of the dark hero behind it.
- **Keyboard operability.** 26-step tab walk over the browse flow: skip link first, order matches visual
  order, and every single focusable control carried a visible `solid 2px rgb(34,105,71)` outline. Zero
  controls without a focus indicator.
- **Accessible names.** No unnamed buttons, links or `role="button"` elements on any page audited; the
  notification bell, avatar menu, language control and card overlay links all carry proper `aria-label`s
  (for example "Benachrichtigungen, 4 ungelesen"). No `<img>` missing `alt`.
- **`prefers-reduced-motion`.** Correctly handled: `frontend/src/styles/global.css:19` gates the `fade-up`
  keyframes behind `@media (prefers-reduced-motion: no-preference)`, and three pages additionally branch on
  `(prefers-reduced-motion: reduce)` in JS. Remaining 0.15s/0.3s transitions are interaction-driven colour
  changes and are not a concern.
- **i18n.** DE/EN key parity exact (1333 keys each). Switching language preserves route, query string,
  search input value and result count; updates `<html lang>` and `<title>`; persists across reload via
  `i18nextLng`. Dates reformat per locale ("11.11.2026" to "11 Nov 2026"). No raw i18next keys and no
  mixed-language UI leaked anywhere; every apparent German string in EN mode was seed content or a German
  postal address in the Impressum, which is correct.
- **Responsive containment.** No horizontal page overflow at 375px, 768px or 1440px on any page tested.
- **Create-opportunity wizard.** `role="dialog"`, `aria-modal="true"`, `aria-labelledby`, focus moved into
  the first field on open, a polite live region announcing "Schritt 1 von 4", character counters, a required
  field legend, and per-language content tabs. Submitting empty correctly sets `aria-invalid` on both
  required fields and surfaces `role="alert"` messages. `aria-required` is set via
  `CreateVolunteerOpportunityModal/shared.tsx:134`.
- **Privilege changes are confirmed.** "Zum Admin machen" and "Sperren" route through `ConfirmDialog`
  (`AdministrationPage.tsx:468`, `:520`) with per-row accessible names, and the admin's own row is disabled
  with an inline explanation. NN #5 satisfied.
- **PWA.** Per-language manifest (`manifest.de.webmanifest`), full name/description/theme, 192 and 512
  icons plus a maskable variant, 5 screenshots, 2 shortcuts, `display: standalone`. Service worker active;
  previously visited routes load offline.
- **Opportunity detail sign-up CTA is sticky** (`lg:sticky lg:top-24`) and verified to remain in the
  viewport while scrolling - initially suspected otherwise, disproved by measurement.
- **Header language selector's non-listbox semantics are deliberate**, documented at
  `Header/LanguageSelector.tsx:30-37` as an intentional retreat from a fake listbox that axe flagged as
  nested-interactive. Not a defect.
- **Keycloak theme.** Consistent with the app shell, contrast-clean, language switcher present, `ui_locales`
  forwarded so the login page follows the app language, and a clear error state.
  (`docs/reviews/assets/2026-08-18/f11-keycloak-login-error-state.jpg`.)

## Parking Lot

- Staging's user list contains a real, non-seed account with a personal email address alongside the
  `@example.com` test users, on an environment whose admin credentials are public. Not a frontend defect and
  outside this review's scope - belongs to the `security` lens, and is distinct from the documented,
  accepted test-credential trade-off.
- The homepage hero drops all of its photographic collage at 375px, leaving a plain green panel. Visually
  much weaker than the desktop treatment, but a deliberate-looking responsive decision rather than a defect
  - noting it as a design opportunity, not a finding.
- English short month rendering gives "12 Sept 2026" beside "11 Nov 2026". This is correct CLDR behaviour
  for `en-GB` and not a bug; flagging only in case a uniform three-letter abbreviation is preferred.
- No browse-level map view exists despite Leaflet being a listed dependency; `SingleMarkerMap` is used on the
  detail page only. A product gap rather than a frontend defect - belongs to `dead-features` if the
  dependency is broader than the usage.
- The `/profile` route was observed navigating away mid-load during testing. Root cause turned out to be F1
  (token expiry with no silent renew), so it is not a separate bug, but it is a good end-to-end symptom to
  regression-test once F1 is fixed.

## Prioritized Next Steps

**Quick wins (low effort, high impact)**

1. **F1** - add `'self'` to `frame-src` in `frontend/nginx.conf.template:15`. One line; restores silent SSO
   and silent token renewal, both currently inert in production. Guard it in the existing CI CSP job.
2. **F9** - add a same-day-aware `formatDateTimeRange` helper and use it at the two call sites.
   `Intl.DateTimeFormat.formatRange()` does the work.
3. **F7** - reuse the existing offline `RouteState` in the homepage opportunities section.
4. **F5 (partial)** - set the dashboard calendar's `scrollToTime` to business hours. Smallest change with
   the largest visible effect on the organizer's landing screen.
5. **F10, F11** - trim the duplicated offline copy and unify the result-count vocabulary. Pure locale edits.
6. **F2** - drop or container-scope the `xl:grid-cols-3` step on the sign-ups grid.

**Larger undertakings**

7. **F5 (remainder) and F6** - rework the organizer dashboard's default widget layout: an empty state inside
   the calendar, a real control in the settings widget, and default widget sizes proportioned to the content
   they hold.
8. **F3** - decide the tab-versus-buttons question for the sign-ups switcher and apply it consistently to
   every segmented control in the app.
9. **F13** - consolidate the two opportunity-card compositions into one component with a variant prop and a
   configurable heading level.
10. **F8** - decide whether the homepage should differ for authenticated volunteers, and if so what it leads
    with.
