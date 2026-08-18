# Frontend & UX Review - einsatzbereit - 2026-08-18

Reviewed: https://einsatzbereit.maik-hasler.de - Commit (local `main` at review time): `6f3ddda`

Evidence screenshots live next to this file in [`2026-08-18-frontend-ux/`](2026-08-18-frontend-ux/).

## Executive Summary

Einsatzbereit does not look like a template. The brand has a real, defensible voice: Barlow Condensed as a dispatch-board display face against Source Sans 3, a single green (`#2d8a5e`) carried consistently through theme colour, PWA manifest, markers and chips, and an amber counterweight used sparingly. Body-text contrast passes WCAG 2.2 AA everywhere I measured (worst case 4.84:1), `prefers-reduced-motion` genuinely disables the entrance animations, the skip link works, the sign-up modal traps focus correctly, upload errors name the file and the actual size that was rejected, and the offline state reads as a state rather than a failure. That is a higher baseline than most projects of this size reach.

The problems are concentrated in five places. First, the single most-used control on the site - the homepage location search - silently throws the user's input away unless they click an autocomplete suggestion, and says nothing when it does. Second, the landing page promises "kostenlos, ohne Konto" four sections above an FAQ that correctly states an account is required. Third, the app's own Content-Security-Policy blocks the OIDC silent-renew iframe, so `automaticSilentRenew` never actually renews and sessions expire out from under people mid-flow. Fourth, the withdrawal confirmation tells users the action cannot be undone when it demonstrably can. Fifth, a whole family of secondary and destructive buttons is drawn with borders between 1.24:1 and 1.5:1 against their background, failing WCAG 1.4.11 - the one contrast rule axe-core does not check for borders.

Underneath those, the recurring theme is drift rather than defect: three names for one action (`Zeitslot auswählen` -> `Mitmachen` -> `Anmeldung`), two card designs for the same object, two date formats on the same dashboard, two phrasings of the same result-count line. Each is small; together they are what makes an otherwise well-built product feel less finished than it is.

Top five to fix, in order: **F9** (location search drops input), **F1** (account promise contradicts reality), **F13** (silent renew blocked by CSP), **F2** (false irreversibility warning), **F19** (non-text contrast on outline buttons).

## Scope & Method

**Tooling.** No browser MCP tool resolved in this session, so all live work was done with scripted Playwright 1.62 driving the sandbox Chromium against `https://einsatzbereit.maik-hasler.de`. **Only one browser engine (Chromium) was available** - no Firefox/WebKit cross-check, and no real touch device; mobile was emulated at 375x812 with `hasTouch`/`isMobile`. Contrast numbers were computed in-page from `getComputedStyle` with `oklch()` resolved through a 1x1 canvas, against the nearest opaque ancestor background; the numbers below are from that instrumentation, not eyeballed.

**Viewports.** 375 px, 768 px, 1440 px.

**Languages.** German (default) and English via the header switcher, on the landing page, browse, opportunity detail, my sign-ups, profile and help.

**Personas.** Anonymous, Vera (`user`), Olaf (`user` + `organisator`), Admin (`admin`).

**Covered.** Landing page, `/opportunities` (filters, mini calendar, location autocomplete, search, empty state), `/organizations`, opportunity detail (anonymous / volunteer / organizer), sign-up modal, full sign-up -> withdraw -> re-sign-up cycle, `/my-signups`, `/profile`, `/profile/settings`, org app (dashboard, Einsätze, Anmeldungen, Mitglieder, Einstellungen, org switcher, create-opportunity wizard incl. upload error cases), `/administration/{organizations,users,reports,audit-log}`, 404, help, contact, imprint, privacy, terms, the Keycloak login and its error state, PWA manifest / service worker / offline reload, keyboard traversal of the browse flow, `prefers-reduced-motion`.

**Test data written and cleaned up.** One sign-up on "Erste-Hilfe-Kurs" as Vera, withdrawn again in the same session; capacity verified back at 20/20 afterwards. Two invalid file uploads were rejected client-side and never left the browser. No other data was created or deleted.

**Not covered / out of scope.** Backend logic, data model, infrastructure, security, CI, code hygiene, dead code (the `lens` / `einsatzbereit-review` skill owns those). Also not reachable in this run: check-in with a PIN, post-event feedback, invitation acceptance, CSV export (no export control was reachable from the organizer surfaces I could get to), and image upload success paths (only the rejection paths were exercised, deliberately, to avoid leaving files on staging).

**Two features named in the brief do not exist in the deployed frontend.** There is no map view on the browse page (`SingleMarkerMap.tsx` is used only on the detail page) and there is no saved-search / alert feature anywhere in `frontend/src`. They are listed in the Parking Lot rather than as findings.

**Note on Keycloak.** Findings on the login screen are marked `[Keycloak/FTL]` - they live in `keycloak/` as Freemarker templates, not in the React app.

---

## Findings

### Content

---

### F1 - Landing page promises "ohne Konto"; the FAQ on the same page says an account is required

**Kategorie:** Content
**Schweregrad:** Hoch
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #2 "Übereinstimmung mit der realen Welt", plus the `/frontend-design` writing rule that copy must be specific and true rather than persuasive)
**Ort:** `/` (founder band and FAQ) - Persona: alle - Viewport: alle - Sprache: DE und EN

Beleg: `pub-home-desktop.webp` (founder paragraph at ~y=1180, FAQ at ~y=1550). Code: `frontend/src/locales/de.json` `landing.missionText` ("... Mikro-Ehrenamt, das in eine echte Woche passt - kostenlos, ohne Konto, komplett open source.") vs. `landing.faqA2` ("Zum Stöbern nicht - du kannst alle Einsätze ohne Anmeldung durchsuchen. Für die Anmeldung zu einem Einsatz brauchst du ein kostenloses Konto."). Identical contradiction in `en.json` ("free, no account required" vs. "To join one, you'll need a free account").

Auswirkung: The founder paragraph is the emotional pitch of the page, and its central claim is false for the only action that matters. A visitor who believes it hits the Keycloak login as an unannounced wall at the exact moment they decided to help - which is precisely the "Anmeldeformulare, Wartelisten, fehlende Einstiegspunkte" friction the same paragraph claims to have removed.

Verbesserungsvorschlag: Change `missionText` to claim only what is true - e.g. "kostenlos, ohne Wartelisten, komplett open source" - and let `faqA2` remain the authority on accounts. Mirror the change in `en.json`. Aufwand: S

---

### F2 - The withdrawal confirmation says the action cannot be undone; it can

**Kategorie:** Content
**Schweregrad:** Hoch
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #5 "Fehlervermeidung" and #9 "Fehler erkennen, diagnostizieren, beheben"; the `/frontend-design` rule that confirmations must be accurate about consequences)
**Ort:** `/my-signups` and `/volunteer-opportunities/:id` - Persona: Vera - Viewport: alle - Sprache: DE/EN

Beleg: `signup-withdraw-confirm.webp` - "Möchtest du deine Anmeldung wirklich zurückziehen? Diese Aktion kann nicht rückgängig gemacht werden." I then withdrew from "Erste-Hilfe-Kurs" and the detail page immediately offered "Zeitslot auswählen" again with capacity restored to 20/20 (`withdraw-after.webp`). The backend also carries a dedicated reactivation path (`Engagement.NotTerminated`: "Only withdrawn or cancelled sign-ups can be reactivated", `de.json:1546`). Code: `de.json:961` (`confirmDialog...message`).

Auswirkung: The warning is strictly false, and it is false in the direction that costs the platform volunteers: someone who cannot make a slot after all will hesitate to withdraw, or will withdraw and then wrongly believe the door is closed. Warnings that overstate consequences also erode trust in the ones that are accurate (account deletion, org deletion - both correctly irreversible today).

Verbesserungsvorschlag: Replace the second sentence with what actually happens: "Dein Platz wird wieder frei. Du kannst dich später erneut anmelden, solange noch Plätze verfügbar sind." Keep the irreversibility wording only on the dialogs where it is true. Aufwand: S

---

### F3 - One action, three names, across one flow

**Kategorie:** Content
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #4 "Konsistenz und Standards"; the `/frontend-design` rule "an action keeps the same name through the whole flow")
**Ort:** `/volunteer-opportunities/:id`, sign-up modal, `/my-signups`, `/app/:id/dashboard` - Persona: Vera, Olaf - Sprache: DE (EN has the same drift)

Beleg: `verali-detail-notsignedup.webp` (trigger: **Zeitslot auswählen**), `signup-modal-1.webp` (dialog title: **Zeitslot auswählen**, confirm: **Mitmachen**), `signup-after-detail.webp` (resulting state: **Deine Anmeldung / Ausstehend**), `vera-mysignups.webp` (page: **Meine Anmeldungen**, action: **Zurückziehen**). EN repeats the pattern: "Select a slot" -> "Sign up" -> "Sign-ups" -> "Withdraw" (`i18n-detail-en.webp`, `i18n-mysignups-en.webp`).
The same drift affects the object noun: **Einsatz** on the public site, **Termin** in the organizer calendar's agenda column header (`m-orgdash.webp`), **Interessenbekundung** as a card chip, **Anmeldung** in the account area.

Auswirkung: Volunteers cannot build a vocabulary for the product. Someone told "melde dich für einen Zeitslot an" looks for a button labelled "Anmelden" and finds "Zeitslot auswählen" - while "Anmelden" in the header means sign in. The interface's vocabulary is its signposting; four words for one object is four chances to take a wrong turn.

Verbesserungsvorschlag: Pick one verb pair and enforce it end to end. Suggested: trigger **Für Zeitslot anmelden**, confirm **Anmeldung abschicken**, state **Angemeldet**, reverse **Zurückziehen**. Rename the header's sign-in control to **Einloggen** so the two senses of "anmelden" stop colliding. Standardise on **Einsatz** as the object noun and drop "Termin" from the calendar header. Aufwand: M

---

### F4 - Date ranges repeat the same date twice

**Kategorie:** Content
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #8 "Ästhetisches und minimalistisches Design" - no redundant information)
**Ort:** at least four places - Persona: Vera, Olaf - Viewport: alle - Sprache: DE und EN

Beleg (all the same single-day slot):
- detail page, "VERFÜGBARE ZEITSLOTS": `27.08.2026, 09:00 - 27.08.2026, 17:00` (`vera-detail-erstehilfe.webp`)
- sign-up modal combobox: same string (`signup-modal-1.webp`)
- detail page after sign-up, "Termin:": `27.08.2026, 09:00 - 27.08.2026, 17:00` (`signup-after-detail.webp`)
- `/my-signups`: `Termin: 20.08.2026, 10:00 - 20.08.2026, 14:00` (`vera-mysignups.webp`)
- English is identical: `27 Aug 2026, 09:00 - 27 Aug 2026, 17:00` (`i18n-detail-en.webp`)

Auswirkung: The most important fact on the card - when do I have to be there - is the hardest to scan, because half the characters are a repeat. In the narrow `/my-signups` card it forces the range onto three wrapped lines.

Verbesserungsvorschlag: Collapse same-day ranges to `27.08.2026, 09:00 - 17:00` (EN: `27 Aug 2026, 09:00 - 17:00`) and keep the full form only when the end date differs. One shared formatter, used by all four call sites. Aufwand: S

---

### F5 - The city autocomplete exposes internal match jargon

**Kategorie:** Content
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (the `/frontend-design` rule "name things by what people control and recognize, never by how the system is built")
**Ort:** `/` hero location field - Persona: alle - Viewport: alle - Sprache: DE

Beleg: `home-city-leipzig.webp` - the single suggestion reads "Leipzig" with the subtitle "Exakte Namensübereinstimmung".

Auswirkung: The subtitle describes the matcher's confidence, not the place. For a user picking a city it carries no information and reads like debug output that escaped into production.

Verbesserungsvorschlag: Show something about the place instead (federal state, or postcode range), or drop the second line entirely when there is one exact match. Aufwand: S

---

### F6 - Support contact is a personal Gmail address, and is a link on one page but plain text on another

**Kategorie:** Content
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Präferenz (professionalism) + Best Practice for the inconsistency (Nielsen-Norman-Heuristik #4)
**Ort:** `/help`, `/contact` - Persona: alle - Viewport: alle - Sprache: DE/EN

Beleg: `pub-help-desktop.webp` - "... oder schreibe uns direkt an maikhslr@gmail.com" rendered as plain text inside a paragraph. `pub-contact-desktop.webp` - the same address rendered as an underlined `mailto:` link with an envelope icon.

Auswirkung: A platform that asks organisations to trust it with volunteer data lists a private mailbox as its support channel; that reads as pre-launch. Separately, the same address behaving as a link on one page and as unclickable text on the other is a small but visible inconsistency in a two-page support section.

Verbesserungsvorschlag: Route support through a role address (`support@`/`kontakt@` on the project domain) and make it a `mailto:` link in both places. Aufwand: S (frontend), the mailbox itself is out of scope here.

---

## Visual design

---

### F7 - Organizer dashboard: widgets stretched into large empty cards, and a week calendar that defaults to an empty night grid

**Kategorie:** Visuelles Design
**Schweregrad:** Hoch
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #8 "Ästhetisches und minimalistisches Design" and #1 "Sichtbarkeit des Systemstatus")
**Ort:** `/app/:organizationId/dashboard` - Persona: Olaf - Viewport: 1440 (mobile is fine, see below) - Sprache: DE

Beleg: `olaf-dashboard.webp`. Measured on the full-page capture:
- "Bevorstehende Einsätze" holds one 40 px row and one link inside a ~550 px tall card; roughly 470 px is empty.
- "Freiwillige" shows the number `1` inside a ~250 px card.
- The whole right-hand column below "Einsatz erstellen" / "Einstellungen" - about 1000 px of it - is empty.
- The calendar opens in **Woche** view scrolled to 00:00-15:00 on a week that contains no events at all; the organisation's only upcoming Einsatz is on 27.08.
- The "Einstellungen" widget's entire content is a gear icon with no label, link or value.

Contrast with `m-orgdash.webp`: at 375 px the same dashboard defaults to **Agenda**, immediately shows "Do. Aug. 27 / 09:00 - 17:00 / Erste-Hilfe-K...", and the widgets size to their content. The mobile layout is the one that works.

Auswirkung: This is the organizer's home screen and the first thing a new organisation sees after creating an account. In its default state it communicates "there is nothing here" while there is in fact an upcoming Einsatz with 0/20 sign-ups - the single most actionable fact the dashboard has.

Verbesserungsvorschlag: (a) default the calendar to **Agenda** at every width, or at minimum scroll the week view to 07:00-19:00; (b) let widgets size to content instead of stretching to the row height (`items-start` on the grid), or give the sparse widgets real content - "Bevorstehende Einsätze" could list the next five with their fill rate; (c) give the "Einstellungen" widget a text label and target, or drop it since the tab bar and the "Einstellungen bearbeiten" link already reach the same page. Aufwand: M

---

### F8 - Account and detail pages leave 30-45 % of the desktop width empty while wrapping text in a narrow column

**Kategorie:** Visuelles Design
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Präferenz
**Ort:** `/my-signups`, `/profile`, `/volunteer-opportunities/:id`, `/app/:id/dashboard/settings` - Persona: Vera, Olaf - Viewport: 1440 - Sprache: DE/EN

Beleg: `vera-mysignups.webp` - two sign-up cards occupy x=430..960 of a 1440 px viewport, with ~270 px empty to their right; inside them "Helfer:innen für das Tierheim" and "Lindenauer Tierschutzverein e.V." both wrap onto two lines. `vera-detail-erstehilfe.webp` - the sign-up card sits alone at the top of a right column that is otherwise empty for ~1500 px. `olaf-settings.webp` - the contact card is ~480 px wide on a 1440 px page.

Auswirkung: Not broken, but the page reads as unfinished: text is wrapping for lack of room next to a large area with nothing in it. On `/my-signups` it costs the user two extra lines per card on exactly the information they came for.

Verbesserungsvorschlag: Let the card grid use the container's full width (3 columns from `lg`) on `/my-signups`, and either make the detail page's action card sticky through the scroll or move the "Über diese Organisation" block into the right column so both columns end together. Aufwand: M

---

## UX

---

### F9 - The homepage location search silently discards a typed city

**Kategorie:** UX
**Schweregrad:** Kritisch
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #1 "Sichtbarkeit des Systemstatus", #5 "Fehlervermeidung", #9 "Fehler erkennen, diagnostizieren, beheben")
**Ort:** `/` hero search -> `/opportunities` - Persona: alle (anonymous included) - Viewport: alle - Sprache: DE/EN

Beleg, three runs:
1. Typed `Berlin` (a valid German city) into "Stadt oder Postleitzahl", did **not** click the suggestion, pressed "Suchen" -> landed on `https://einsatzbereit.maik-hasler.de/opportunities` with **no query parameters at all**, all 9 results, no location chip, no message.
2. Typed `Leipzig` and typed `Tierheim` in the keyword field, pressed "Suchen" -> `/opportunities?q=Tierheim`. The keyword survived, the city was dropped (`search-from-home.webp` shows only a `"Tierheim"` chip).
3. Typed `Leipzig` and **clicked the suggestion**, pressed "Suchen" -> `/opportunities?city=Leipzig&lat=51.3406321&lng=12.3747329&radius=10`, filter chip "Leipzig - 10 km", "5 Einsätze gefunden." (`home-city-leipzig-result.webp`). The feature works; it just requires a click most people will not make.

Also observed: typing `Leip` produced no suggestions at all after 6 s of waiting, so partial input gets no help either.

Auswirkung: "Finde in wenigen Klicks einen Einsatz, der zu dir passt" is the product's promise and this field is where it is made. A user who types their city, presses Suchen and gets a nationwide list has been told, wordlessly, that the platform has nothing near them - which for a regional volunteering platform is the worst possible false negative. Nothing anywhere in the UI hints that the suggestion had to be clicked.

Verbesserungsvorschlag: On submit, if the location field has text but no resolved coordinates, resolve it first (call `/v1/maps/cities` with the typed value and take the top match) and only then navigate. If nothing resolves, navigate **and** show an inline message on the results page - "Für 'Berlin' haben wir keinen Ort gefunden. Wir zeigen alle Einsätze." - so the dropped input is never silent. Aufwand: M

---

### F10 - Signing in from any page drops the user on the homepage

**Kategorie:** UX
**Schweregrad:** Hoch
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #3 "Nutzerkontrolle und -freiheit")
**Ort:** every page, via the header's "Anmelden" - Persona: anonymous -> Vera/Olaf/Admin - Viewport: alle - Sprache: DE/EN

Beleg: from `/opportunities` I clicked "Anmelden", completed the Keycloak form, and landed on `https://einsatzbereit.maik-hasler.de/` (`vera-returnto-after-login.webp`). Code: `frontend/src/components/Header/Header.tsx:103` calls `auth.signinRedirect(signinLocaleArgs())` with no `state.returnTo`, while `layouts/ProtectedRoute.tsx:64` and `hooks/useSessionExpiryHandler.ts:60` both pass `{ returnTo: location.pathname + location.search }` and `main.tsx:83-84` already honours it.

Auswirkung: The most likely moment to press "Anmelden" is on an opportunity detail page, after deciding to join. The user is then thrown back to the start and has to find that opportunity again - and if they arrived through a location search, F9 makes finding it again harder than it should be. The mechanism to fix this already exists and is used by two other call sites; only the header's own button skips it.

Verbesserungsvorschlag: Pass `{ state: { returnTo: location.pathname + location.search } }` from `Header.tsx`'s sign-in and registration handlers, exactly as `ProtectedRoute` does. Aufwand: S

---

### F11 - The withdrawal dialog does not say which sign-up it is about

**Kategorie:** UX
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #5 "Fehlervermeidung")
**Ort:** `/my-signups` - Persona: Vera - Viewport: alle - Sprache: DE/EN

Beleg: `signup-withdraw-confirm.webp` - three cards each with their own "Zurückziehen" button; the dialog that opens reads only "Anmeldung zurückziehen? / Möchtest du deine Anmeldung wirklich zurückziehen? ..." and, being centred, physically covers the card it refers to.

Auswirkung: A destructive, unlabelled confirmation on a list of near-identical rows is exactly the shape that produces wrong deletions. The pattern is already solved elsewhere in this codebase - the admin area interpolates the target name into its confirmations, and even names it in the button's `aria-label` (`administration.users.blockNamed`).

Verbesserungsvorschlag: Interpolate the opportunity title into the message, as `confirmDialog.orgDelete.message` already does with `"{{name}}"`. Aufwand: S

---

### F12 - The browse list gives no sign that you are already signed up

**Kategorie:** UX
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #6 "Wiedererkennen statt Erinnern")
**Ort:** `/opportunities`, `/` (LatestOpportunitiesSection) - Persona: Vera - Viewport: alle - Sprache: DE/EN

Beleg: `signup-browse-state.webp`, taken immediately after signing Vera up for "Erste-Hilfe-Kurs". The card is byte-for-byte the same as before apart from the capacity dropping from 20 to 19 - no badge, no chip, no change of state. The same holds for "Helfer:innen für das Tierheim", which Vera had a confirmed sign-up for throughout the session.

Auswirkung: The volunteer has to hold their own sign-ups in memory while browsing, or open each card to find out. It also makes the list actively misleading for regulars: an opportunity you have already committed to looks exactly like a fresh one.

Verbesserungsvorschlag: Add a state chip to `OpportunityListItem` when the current user has a non-terminated engagement - "Angemeldet" / "Ausstehend" in the same slot the format chip uses. The data is already fetched for the detail page. Aufwand: M
(Vermutlich Backend: the list endpoint may not currently return the caller's engagement state; if so, that is a backend change and is not proposed here.)

---

### F13 - Silent token renewal is blocked by the app's own CSP, so sessions expire instead of renewing

**Kategorie:** UX
**Schweregrad:** Hoch
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #5 "Fehlervermeidung"; WCAG 2.2 AA 2.2.6 "Timeouts" is adjacent - unsaved data must not be lost to an unannounced session end)
**Ort:** every authenticated page - Persona: Vera, Olaf, Admin - Viewport: alle - Sprache: alle

Beleg: on every page load the console logs
`Refused to frame 'https://einsatzbereit.maik-hasler.de/callback?state=...&code=...' because it violates the following Content Security Policy directive: "frame-src https://login.maik-hasler.de".`
and the initial iframe request fails with `net::ERR_BLOCKED_BY_CSP`. Reproduced on `/`, `/opportunities`, detail pages, and the org app, both signed in and signed out. Code: `frontend/src/main.tsx:36` sets `automaticSilentRenew: true` with no `silent_redirect_uri`, so oidc-client-ts falls back to `redirect_uri` = `window.location.origin + "/callback"`; `frontend/nginx.conf.template:15` declares `frame-src ${CSP_KEYCLOAK_ORIGIN}` without `'self'`, so the iframe's return leg to the app's own origin is refused.

Auswirkung: The renew iframe reaches Keycloak, gets a valid code, and is then blocked on the way back - so the token is never actually exchanged. Silent renewal is configured but non-functional in production: users get dropped to the login screen when the access token expires, mid-form in the create-opportunity wizard as easily as anywhere else. `useSessionExpiryHandler` then does its job and redirects, which is why this looks like "normal" session expiry rather than a bug.

Verbesserungsvorschlag: Add `'self'` to `frame-src` in `frontend/nginx.conf.template:15` (`frame-src 'self' ${CSP_KEYCLOAK_ORIGIN}`), and set an explicit `silent_redirect_uri` (a minimal `/silent-callback.html`) in `main.tsx` so the renew iframe does not boot the whole SPA. Aufwand: S

---

### F14 - The org switcher truncates the one label that says who you are acting as

**Kategorie:** UX
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #1 "Sichtbarkeit des Systemstatus")
**Ort:** `/app/:organizationId/*` header - Persona: Olaf - Viewport: 1440 and below - Sprache: DE/EN

Beleg: `olaf-opportunities.webp` and `org-switcher-open.webp` - the closed switcher reads `LN Lindenauer Nachbarschaftshilf...`; the open dropdown wraps the full name onto two lines and shows the second org as `LT Lindenauer Tierschutzverein e.V.`. On this instance both organisations share the prefix "Lindenauer ", so the persistent indicator is truncated at almost exactly the point where the two names diverge.

Auswirkung: Everything an organizer does in this shell - publishing, cancelling, inviting, removing members - is attributed to the org named in that pill. If the pill cannot be read at a glance, the answer to "in whose name am I acting?" requires opening a dropdown. The avatar initials (`LN` / `LT`) carry the distinction, but they are two grey letters and nothing labels them as the differentiator.

Verbesserungsvorschlag: Let the switcher grow to fit up to ~340 px before truncating, truncate from the middle rather than the end (`Lindenauer Nachbar...hilfe e.V.`), and add a `title` so the full name is available on hover. Aufwand: S

---

### F15 - Organizers see the opportunity detail page with the entire action rail removed and no explanation

**Kategorie:** UX
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #1 "Sichtbarkeit des Systemstatus")
**Ort:** `/volunteer-opportunities/:id` for an opportunity belonging to the viewer's own org - Persona: Olaf - Viewport: 375, 768, 1440 - Sprache: DE

Beleg: matrix run across both personas and all three viewports. Vera sees the "Zeitslot auswählen" CTA and a "Melden" button at every width; Olaf sees **zero** buttons in `<main>` at every width (`cta-olaf-mobile.webp`, `m-detail.webp`). Nothing in the page says why.

Auswirkung: The organizer looking at their own listing gets a page whose entire right-hand column has vanished, with no statement that this is intentional. First reaction is "is this broken?", and there is no way to tell it apart from a rendering failure. There is also no shortcut from the public page back to the management view of the same Einsatz.

Verbesserungsvorschlag: Replace the removed card with an explanatory one: "Das ist ein Einsatz deiner Organisation. Du kannst dich nicht selbst anmelden." plus a primary link to `/app/:orgId/dashboard/opportunities/:id/engagements`. Aufwand: S

---

### F16 - The result-count line changes wording and meaning between unfiltered and filtered states

**Kategorie:** UX
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #1 and #4)
**Ort:** `/opportunities` - Persona: alle - Viewport: alle - Sprache: DE/EN

Beleg: unfiltered - "9 Einsätze geladen, weitere verfügbar." (`pub-opportunities-desktop.webp`); filtered - "2 Einsätze gefunden." (`search-from-home.webp`); no results - "0 Einsätze gefunden." plus a proper empty state (`empty-search-real.webp`).

Auswirkung: The default state reports what the client has downloaded ("geladen") rather than what exists, so the user never learns how many opportunities the platform actually has - the one number that would tell them whether it is worth coming back. The wording also shifts between two states of the same line.

Verbesserungsvorschlag: Use one phrasing driven by the total, e.g. "9 von 34 Einsätzen" / "2 von 34 Einsätzen (gefiltert)". Aufwand: S
(Vermutlich Backend: this needs a total count in the list response if one is not returned today.)

---

### F17 - Radius chips are selectable before a city has been chosen

**Kategorie:** UX
**Schweregrad:** Niedrig
**Konfidenz:** Wahrscheinlich
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #5 "Fehlervermeidung")
**Ort:** `/opportunities`, "Standort" filter - Persona: alle - Viewport: alle - Sprache: DE

Beleg: `vera-filter-standort.webp` - the panel offers "Stadt eingeben...", "In meiner Nähe", and then "Umkreis: 5 / 10 / 25 / 50 / 100 km" all fully enabled with no city set. A radius without a centre has no effect.

Auswirkung: A user can set "25 km", see the result list not change, and reasonably conclude the filter is broken.

Verbesserungsvorschlag: Disable the radius row until a city (or "In meiner Nähe") has resolved, with a one-line hint - "Wähle zuerst einen Ort." Aufwand: S

---

### F18 - The static map offers no route to directions

**Kategorie:** UX
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Präferenz
**Ort:** `/volunteer-opportunities/:id` - Persona: alle - Viewport: alle - Sprache: DE/EN

Beleg: `vera-detail-erstehilfe.webp`; `components/SingleMarkerMap.tsx` deliberately disables dragging, all zoom modes, keyboard control and the zoom control (a good decision - it removes the pinch-versus-scroll conflict on mobile entirely, and the marker carries an accessible name via `title`). But there is no "Route planen" or "In Karten öffnen" affordance anywhere on the page.

Auswirkung: The whole point of a regional volunteering platform is that you physically go there. Today the address has to be copied out of the contact block by hand.

Verbesserungsvorschlag: Add a link under the map - `https://www.openstreetmap.org/directions?to=<lat>,<lng>` or a `geo:` URI on mobile - labelled "Route planen". Aufwand: S

---

## UI

---

### F19 - Secondary and destructive buttons fail WCAG 1.4.11 non-text contrast

**Kategorie:** UI (accessibility-relevant)
**Schweregrad:** Hoch
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (WCAG 2.2 AA, 1.4.11 Non-text Contrast - 3:1 for the visual boundary that identifies a UI component)
**Ort:** the whole outline-button family, app-wide - Persona: alle - Viewport: alle - Sprache: alle

Beleg: measured in-page against the resolved opaque background:

| Control | Border colour | Border vs. background | Fill vs. background | Required |
|---|---|---|---|---|
| "Mehr laden" (`/opportunities`) | `rgb(168,223,195)` | **1.50:1** | 1.07:1 | 3:1 |
| "Zurückziehen" (`/my-signups`) | `oklch(0.885 0.062 18.334)` | **1.45:1** | 1.00:1 | 3:1 |
| "Zum Kalender hinzufügen" | `oklch(0.928 0.006 264.531)` | **1.24:1** | 1.00:1 | 3:1 |

Screenshots: `pub-opportunities-desktop.webp` ("Mehr laden"), `vera-mysignups.webp` (both others). The pattern repeats on "Verlauf anzeigen"/"Verbergen" (admin), "Sperren"/"Zum Admin machen" (admin), the filter chips, and the card borders.

Auswirkung: For a user with low vision or on a low-quality screen, these controls have no perceivable boundary - "Zurückziehen" in particular is a destructive action whose only non-text identification is a 1.45:1 outline. axe-core does not evaluate 1.4.11 for borders, so the existing automated a11y suite will never surface this.

Verbesserungsvorschlag: Darken the outline-button border tokens to at least 3:1 against white - roughly `#7aa88f` for the neutral-green variant and `#c88a8a` for the destructive one - or add a light fill so the fill-versus-background delta carries the 3:1 instead. Fix it once in the shared button variants; the card borders can stay decorative since cards are not interactive components. Aufwand: S

---

### F20 - Every `<select>` in the app renders without a dropdown chevron and reads as a text input

**Kategorie:** UI
**Schweregrad:** Hoch
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #6 "Wiedererkennen statt Erinnern" - a control must look like what it is)
**Ort:** `/app/:id/dashboard/members` ("Einladen als"), `/app/:id/dashboard/engagements` ("Status"), `/EngagementManagementPage` - Persona: Olaf - Viewport: alle - Sprache: alle

Beleg: `olaf-select-zoom.webp` - the "Einladen als" control is a plain white rounded box reading "Mitglied", with no chevron. Computed style on `#invite-role`: `appearance: none` **and** `background-image: none`. Root cause: `frontend/src/lib/formClasses.ts:28` builds the replacement chevron as a Tailwind arbitrary value `bg-[url('data:image/svg+xml;charset=utf-8,%3Csvg xmlns=%22http://...%22 ...%3E')]` containing raw spaces; Tailwind v4 requires `_` for spaces inside arbitrary values, so the utility is never generated and only the `appearance-none` half of the pair takes effect. Affects all 7 usages of `selectClass` across 3 files.

Auswirkung: An organizer inviting their first member sees what looks like a filled-in text field labelled "Einladen als" and has no reason to click it, so the role choice is invisible - and role assignment is the one decision in that form with lasting consequences. The same applies to the "Status" filter on the sign-ups page.

Verbesserungsvorschlag: Replace the arbitrary-value background with an absolutely positioned inline chevron icon next to the select (as the rest of the codebase does for comboboxes), or fix the class by encoding spaces as `_`. Add a visual regression assertion so it cannot silently regress again. Aufwand: S

---

### F21 - The "Abgesagt" status chip and the "Absagen" action button are visually and lexically almost identical

**Kategorie:** UI
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #4 "Konsistenz und Standards", #5 "Fehlervermeidung")
**Ort:** `/app/:id/dashboard/engagements` - Persona: Olaf - Viewport: alle - Sprache: DE

Beleg: `olaf-engagements.webp` - row 4 carries a red-outlined pill reading **Abgesagt** (a status), row 6 carries a red-outlined pill reading **Absagen** (a button that cancels a volunteer's confirmed sign-up). Same shape, same size, same colour, same position at the right edge of the row, one letter apart.

Auswirkung: Cancelling a confirmed sign-up notifies the volunteer and cannot be taken back from this screen. Presenting it in the same visual token used for a passive status label invites exactly the misclick that costs an organisation a volunteer.

Verbesserungsvorschlag: Keep the pill shape for statuses only. Render "Absagen" as a real button (filled or solid-bordered, with the destructive treatment used on "Mein Konto löschen"), and put a confirmation dialog behind it that names the volunteer and the Einsatz. Aufwand: S

---

### F22 - Two different card designs for the same object on the same page

**Kategorie:** UI
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #4 "Konsistenz und Standards")
**Ort:** `/opportunities` vs. the "Weitere Einsätze dieser Organisation" rail on `/volunteer-opportunities/:id` - Persona: alle - Viewport: alle - Sprache: DE/EN

Beleg: `pub-opportunities-desktop.webp` (browse card: category chip + frequency chip / capacity or format chip / **title** / date line / description / org avatar + name + location) vs. `vera-detail-erstehilfe.webp` (rail card: category chip / format chip / **frequency line with icon** / title / description / location - no date, no organisation, no capacity).

Auswirkung: The two cards answer different questions about the same kind of thing, sitting one scroll apart. The rail card omits the date, which on the browse card is the most prominent line - so the same Einsatz looks like a different class of object depending on where it is seen.

Verbesserungsvorschlag: Render the rail with the existing `OpportunityListItem` (already shared between `/opportunities` and the landing page's preview, per `frontend/AGENTS.md`), dropping only the org row since the organisation is implied by the section heading. Aufwand: S

---

### F23 - One chip slot carries three unrelated kinds of information

**Kategorie:** UI
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #4 "Konsistenz und Standards")
**Ort:** `/opportunities`, `/` - Persona: alle - Viewport: alle - Sprache: DE/EN

Beleg: `pub-opportunities-desktop.webp` - the top-right chip of a card is variously "20 Plätze frei" (capacity, grey), "Unbegrenzt viele Plätze" (capacity, green-tinted), or "Interessenbekundung" (sign-up mechanism, grey). Three semantic categories in one visual slot, with the colour distinction tracking none of them.

Auswirkung: The slot cannot be scanned. A reader comparing cards has to read each chip to find out which question it is answering, which defeats the purpose of a chip.

Verbesserungsvorschlag: Reserve the top-right slot for the sign-up mechanism (Zeitslot / Interessenbekundung) and move capacity next to the date line where "Beginnt am ..." already sits. Use colour only to signal scarcity ("Nur noch 2 Plätze"). Aufwand: S

---

### F24 - The sign-up combobox runs the time range and the capacity together

**Kategorie:** UI
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #8)
**Ort:** sign-up modal - Persona: Vera - Viewport: alle - Sprache: DE/EN

Beleg: `signup-modal-1.webp` - the closed combobox reads `27.08.2026, 09:00 - 27.08.2026, 17:00 20 Plätze frei`, i.e. "...17:00 20 Plätze frei" with no separator. EN identical: "27 Aug 2026, 09:00 - 27 Aug 2026, 17:00 20 spots left".

Auswirkung: "17:00 20" briefly parses as one number. In the open list the two parts are laid out on opposite sides of the row and read fine; only the collapsed state loses the separation.

Verbesserungsvorschlag: Join with a middot in the collapsed label: `27.08.2026, 09:00 - 17:00 - 20 Plätze frei`. Combined with F4 this becomes a single formatter change. Aufwand: S

---

### F25 - Members page: role chip only for organizers, destructive action as a plain text link, disabled control explained by a footnote

**Kategorie:** UI
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #4 and #9)
**Ort:** `/app/:id/dashboard/members` - Persona: Olaf - Viewport: alle - Sprache: DE

Beleg: `olaf-members.webp`. Three separate issues in one card:
1. Olaf's row shows a green "Organisator:in" chip; Vera's row shows no chip at all, so "Mitglied" is expressed by absence.
2. "Zu Organisator:in befördern" and the destructive "Entfernen" are plain text links, not buttons - inconsistent with every other action surface in the app, and "Entfernen" gets no destructive affordance beyond red text.
3. Olaf's "Verlassen" is rendered in very light grey (disabled), and the reason - "Du bist der:die einzige Organisator:in dieser Organisation ..." - sits in a footnote at the bottom of the card, two rows away from the control it explains.

Auswirkung: (1) makes the members list unreadable at a glance for anything larger than two people. (2) means the highest-consequence action on the page (removing a member) has the weakest affordance on the page. (3) is the classic disabled-control failure: the user sees a dead control and has to hunt for the reason.

Verbesserungsvorschlag: Give every row an explicit role chip. Render both row actions as buttons, with the destructive variant for "Entfernen". Attach the "only organizer" explanation to the "Verlassen" control itself (a `title` plus a `<p>` directly under it, or an inline info icon). Aufwand: S

---

### F26 - The mini calendar signals "day has opportunities" with a single small dot

**Kategorie:** UI
**Schweregrad:** Mittel
**Konfidenz:** Wahrscheinlich
**Einordnung:** Best Practice (WCAG 2.2 AA, 1.4.11 Non-text Contrast for the meaningful graphic; Nielsen-Norman-Heuristik #6)
**Ort:** `/opportunities`, "Datum" filter - Persona: alle - Viewport: alle - Sprache: DE/EN

Beleg: `vera-filter-datum.webp` - days 20 and 27 carry a ~3-4 px green dot under the number; the legend reads "Tage mit Einsätzen". Every other enabled day is visually identical to them apart from the dot's presence. The dot is the only encoding of the calendar's entire purpose.

Auswirkung: At the rendered size the marker is easy to miss outright, and there is no non-visual equivalent - a screen-reader user tabbing the grid gets no indication which dates are worth picking, so the filter degrades to trial and error.

Verbesserungsvorschlag: Give days with opportunities a filled or outlined background treatment in addition to the dot, raise the dot to ~6 px, and append an `aria-label` such as "20. August, 2 Einsätze" to those day buttons. Aufwand: S

---

### F27 - "Als Entwurf speichern" is a real action rendered as unstyled text

**Kategorie:** UI
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Präferenz
**Ort:** create-opportunity wizard footer - Persona: Olaf - Viewport: alle - Sprache: DE/EN

Beleg: `olaf-create-step1.webp` - the footer holds "Abbrechen" (plain text, left), "Als Entwurf speichern" (plain text, centre-right) and "Weiter" (filled primary, right). Two of the three are typographically identical, but one cancels and one saves.

Auswirkung: Saving a draft is the escape hatch that makes a four-step wizard safe to abandon. Giving it the same weight as "Abbrechen" hides it, and puts a save and a discard next to each other in the same visual token.

Verbesserungsvorschlag: Promote "Als Entwurf speichern" to a secondary (outlined) button and keep "Abbrechen" as the only text-only action, well separated from it. Aufwand: S

---

## Accessibility

(complementary to the existing axe-core / jsx-a11y coverage; nothing here duplicates a rule those tools run)

**Verified good and worth protecting:** the skip link (`Zum Inhalt springen`) is the first tab stop and becomes visible on focus; focus order matched visual order on every flow I traversed; the sign-up modal moves focus to its first control on open and cycles correctly with no escape (12 consecutive Tab presses stayed inside); `prefers-reduced-motion: reduce` drops all 16 entrance animations to zero; the create-opportunity wizard exposes a screen-reader-only "Schritt 1 von 4: Grunddaten"; the map marker carries an accessible name via Leaflet's `title`; the admin "Sperren" button's `aria-label` names the user it acts on. Body-text contrast passes AA everywhere I sampled (25 probes, worst case 4.84:1).

---

### F28 - German user content is served under `lang="en"` with no language marking

**Kategorie:** Barrierefreiheit
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (WCAG 2.2 AA, 3.1.2 Language of Parts)
**Ort:** every page showing user-generated content while the UI is English - Persona: alle - Viewport: alle - Sprache: EN

Beleg: with the UI switched to English, `document.documentElement.lang === "en"` and the only element in the entire document carrying a `lang` attribute is `<html>` itself (enumerated in-page: `["HTML=en"]`). The `<h1>` reads "Erste-Hilfe-Kurs", the lede "Lerne lebensrettende Sofortmaßnahmen in unserem eintägigen Praxiskurs.", the org description "Wir unterstützen Menschen in Leipzig und Umgebung ..." - all German, all unmarked (`i18n-detail-en.webp`).

Auswirkung: A screen reader announces German titles, descriptions and org profiles with English phonetics, which for longer descriptions is close to unintelligible. This is structural, not a data gap: the create-opportunity wizard already has a Deutsch/English content toggle, so the app knows which language a given translation is in - it simply does not put that on the element.

Verbesserungsvorschlag: Emit `lang="de"` / `lang="en"` on the elements rendering translated user content, driven by which content translation was actually selected for display. Aufwand: M
(Vermutlich Backend: the list/detail responses need to say which language the returned strings are in if they do not already.)

---

### F29 - All four administration sub-pages share one document title

**Kategorie:** Barrierefreiheit
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (WCAG 2.2 A, 2.4.2 Page Titled - a title must describe topic or purpose)
**Ort:** `/administration/{organizations,users,reports,audit-log}` - Persona: Admin - Viewport: alle - Sprache: DE/EN

Beleg: all four routes return `document.title === "Administration | Einsatzbereit"`. The org app, by contrast, does this correctly: "Dashboard - Lindenauer Nachbarschaftshilfe e.V. | Einsatzbereit", "Mitglieder - ...", "Einstellungen - ...".

Auswirkung: Browser tabs, history and bookmarks collapse into four identical entries; a screen-reader user navigating between admin sections gets the same announcement each time and no confirmation that the navigation happened. The pattern to copy already exists two routes away.

Verbesserungsvorschlag: Set a per-tab title in `AdministrationPage`, matching the org app's `"<Tab> - <Context> | Einsatzbereit"` shape. Aufwand: S

---

### F30 - "Einsatz-" is used as a link label

**Kategorie:** Barrierefreiheit
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (WCAG 2.2 A, 2.4.4 Link Purpose (In Context))
**Ort:** `/help` ("Noch Hilfe nötig?" card), `/contact` ("Missbräuchliche Inhalte melden" card) - Persona: alle - Viewport: alle - Sprache: DE

Beleg: `pub-help-desktop.webp` - "... nutze die Schaltfläche 'Melden' auf der jeweiligen <u>Einsatz-</u> oder <u>Organisationsseite</u>". The German compound ellipsis splits the word, so the first link's entire accessible name is the fragment "Einsatz-".

Auswirkung: In a screen reader's link list this appears as "Einsatz-", which names nothing. The construction is grammatically correct German prose but makes a bad link.

Verbesserungsvorschlag: Restructure so each link is a whole noun: "... auf der jeweiligen <u>Einsatzseite</u> oder <u>Organisationsseite</u>". Aufwand: S

---

### F31 - The error state on the Keycloak login is masked while the field has focus `[Keycloak/FTL]`

**Kategorie:** Barrierefreiheit
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Präferenz (the message itself remains visible, so 3.3.1 Error Identification is met)
**Ort:** Keycloak login, failed credentials - Persona: alle - Viewport: alle - Sprache: DE/EN

Beleg: `kc-error.webp` - after a failed sign-in both fields get a red border, but the focused username field's green focus outline is drawn over its red error border, so the field that has focus is the one that looks least like it has a problem. (The rest of this screen is well done: the username is preserved, the message "Benutzername oder Passwort stimmt nicht." leaks nothing about which was wrong, "Passwort vergessen?" is right there, and the password reveal toggle works and is labelled "Passwort einblenden".)

Auswirkung: Cosmetic, but it undercuts the error styling exactly where the user is looking.

Verbesserungsvorschlag: In the error state, draw the focus ring in the error colour, or offset it so both remain visible. Aufwand: S

---

## i18n

**Verified good:** switching language preserves route **and** query string (`/opportunities?q=...` survived intact), `<html lang>` updates, the choice persists across a full reload, per-language web manifests are served (`/manifest.de.webmanifest`), no raw i18next keys and no German leakage appeared in the English UI on any of the six screens checked, and date formats localise correctly (`27.08.2026, 09:00` -> `27 Aug 2026, 09:00`). The longer German strings did not break any layout at 375/768/1440; no page had horizontal document overflow in either language.

---

### F32 - Two date formats on the same screen

**Kategorie:** i18n
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #4 "Konsistenz und Standards")
**Ort:** `/app/:id/dashboard`, `/app/:id/dashboard/settings` - Persona: Olaf - Viewport: alle - Sprache: DE

Beleg: `olaf-dashboard.webp` shows `17.08.2026 - 23.08.2026` (calendar range), `27.08.2026, 09:00` (upcoming Einsatz) and `Erstellt am 13. August 2026` (organisation widget) within one viewport height. `olaf-settings.webp` repeats the long form.

Auswirkung: Minor, but it makes the long-form dates read as coming from a different system than everything around them.

Verbesserungsvorschlag: Use the numeric locale format everywhere, or reserve the long form for a documented class of dates (e.g. "since" dates) and apply it consistently. Aufwand: S

---

## PWA

**Verified good, no findings.** `/manifest.de.webmanifest` is complete and per-language: `display: standalone`, `theme_color: #2d8a5e` matching the brand token exactly, 192/512 icons plus a `maskable` 512, `id`/`scope`/`start_url` all set, and labelled wide-form screenshots for the install prompt. One service worker is registered at `/sw.js`. Reloading `/opportunities` with the network cut serves the full shell from cache and renders a proper empty state - icon, "Du bist offline", "Du bist offline. Sobald deine Verbindung zurück ist, laden wir die Einsätze." - which reads as a state, not a failure (`pwa-offline-reload.webp`). The only nit, too small to number: the offline card offers no "Erneut versuchen" and the filter chips stay enabled while nothing can load.

---

## Parking Lot

- **No map view on the browse page.** `SingleMarkerMap.tsx` is used only on the detail page; `/opportunities` has no geographic view despite location being the product's core axis. Feature gap, not a defect - belongs to product scoping, or the `lens` `dead-features` pass if it was ever intended.
- **No saved searches or alerts.** Nothing in `frontend/src` implements them. Same treatment as above.
- **No CSV export reachable.** Named in the brief; I found no export control on any organizer surface I could reach. Either unbuilt, or hidden behind the per-opportunity engagement view I could not exercise.
- **Staging content debris.** A public opportunity titled "Wir suchen Helfer:innen" with the description `xxx` is live and visible to anonymous visitors (`m-detail.webp`); `/app/.../engagements` is dominated by withdrawn test entries. Data hygiene - `reset-staging.yml` exists for this; not triggered.
- **Seed contact data uses `.example` TLDs** (`info@nachbarschaftshilfe-lindenau.example`) rendered as live `mailto:` links. Staging data quality.
- **Console noise from the blocked renew iframe** (see F13) fires on every page load for every visitor. The user-visible consequence is covered by F13; the noise itself belongs to the `bugs` lens.
- **`password-toggle.js` logs `net::ERR_ABORTED`** on the Keycloak page after navigation. The toggle itself works (verified: `type` flips `password` -> `text`), so this is almost certainly a benign unload race - flagging it for the `bugs` lens rather than claiming a defect.
- **Two edit affordances for the same thing on `/profile`** ("Bearbeiten" in the section header and "Profil vervollständigen" in the empty state). Too minor to number; fold into any future pass on that page.

---

## Prioritized Next Steps

### Quick wins - low effort, high impact

1. **F13** - add `'self'` to `frame-src` in `frontend/nginx.conf.template:15`. One token; restores silent session renewal for every logged-in user.
2. **F10** - pass `state.returnTo` from `Header.tsx:103`. One line; the mechanism already exists and is used by two other call sites.
3. **F2** - rewrite the withdrawal confirmation to describe what actually happens (`de.json:961` + `en.json`). Removes a false statement from a destructive dialog.
4. **F1** - drop "ohne Konto" from `landing.missionText` in both locales. Removes the product's most visible untrue claim.
5. **F20** - fix the chevron in `lib/formClasses.ts:28`. One class; makes every select in the organizer area look like a select again.
6. **F19** - darken two border tokens on the outline-button variants. One change, clears a WCAG 1.4.11 failure across the whole app.
7. **F11** - interpolate the opportunity name into the withdrawal dialog, copying the admin area's existing pattern.
8. **F29** - per-tab document titles in `AdministrationPage`, copying the org app's shape.

### Larger undertakings

9. **F9** - resolve or report the typed location on submit. The highest-value fix in this report and the only one that needs real interaction design (what to do when nothing resolves), which is why it is not in the quick-win list despite being first in priority.
10. **F3** - settle the vocabulary (one verb pair, one object noun) and sweep both locale files. Cheap per string, but it needs a decision before it needs an edit.
11. **F7** - rework the organizer dashboard: Agenda as the default calendar view, content-sized widgets, and real content in the sparse ones. This is the screen an organisation lands on after signing up.
12. **F4 + F24** - one shared date-range formatter that collapses same-day ranges, adopted by all four call sites.
13. **F12** - surface the viewer's own sign-up state on browse cards (needs the list endpoint to carry it).
14. **F28** - mark the language of user-generated content (needs the API to say which language it returned).
15. **F22 + F23** - consolidate onto one opportunity card and give the chip slot a single meaning.
