# Frontend & UX Review - einsatzbereit - 2026-08-18

Reviewed: https://einsatzbereit.maik-hasler.de · Commit (main at review time): `6f3ddda`

Evidence screenshots live in [`assets/2026-08-18-frontend/`](assets/2026-08-18-frontend/); every finding names the file that backs it.

## Executive Summary

Einsatzbereit reads as a real product, not a demo. The type system is disciplined and deliberate - Barlow Condensed for h1 and major section headings, Source Sans 3 for everything else, on a tight 12/14/16/18/20 body scale and a 36/48/72/96 display scale - and the brand green is applied consistently across surfaces. The volunteer path (browse, open, sign up, check in, withdraw) works end to end, keyboard focus order matches visual order with a visible ring on every stop, the skip link works, reduced motion is correctly gated at the CSS level, and the form error copy in the create-opportunity wizard is genuinely excellent ("„big.png" ist 4,1 MB groß - erlaubt sind maximal 2 MB." names the file, its real size, and the limit in one line). Translation coverage is complete: 1333 keys in both locales, no drift, no raw keys on screen, and the language switcher preserves route and state.

The problems cluster in five places. **First, the city autocomplete actively misleads.** Typing "Lei" returns Köln, Dresden, Regensburg and Halle (Saale); "Leip", "Leipz" and "Leipzi" all return "Keine passende Stadt gefunden." - and only the fully typed "Leipzig" works. Location is the core filter of a product whose promise is "in deiner Nähe", and it fails exactly during normal typing. **Second, the organizer dashboard is the weakest screen in the product**: fixed-height widget tiles leave a 688x712 px card holding ~110 px of content, the calendar opens on an empty week starting at 00:00, and the whole grid uses roughly the left half of a 1440 px viewport. **Third, several functional icons fall below the WCAG 2.2 non-text contrast floor** - the filter chips' leading icons sit at 2.26:1 against white where 3:1 is required, in a place axe-core's text-only contrast rule cannot see. **Fourth, layout width is inconsistent within single pages**: the opportunity detail page renders its slots and organisation sections at 672 px but the related-opportunities grid below them at 1152 px, so the right edges disagree by 480 px. **Fifth, offline is a designed state on the browse list but a generic "Ein unerwarteter Fehler ist aufgetreten" on the detail page** - the app already knows how to say this well and simply does not, on the route users are most likely to deep-link into.

Nothing here blocks a core flow outright, so no finding is rated Critical. The city search comes closest.

## Scope & Method

**How.** Live browser sessions against `https://einsatzbereit.maik-hasler.de` driven by Playwright (headless Chromium 1194 via the sandbox launch recipe in `.claude/skills/live-verify/`). **Only one browser engine was available** - all rendering, contrast and layout observations are Chromium-only; no WebKit or Gecko verification. No Playwright MCP browser tools were available in this session, so everything ran through throwaway scripts.

**Viewports.** 375x812, 768x1024, 1440x900. Layout probes read `getBoundingClientRect()` and `scrollWidth/clientWidth` in-page rather than being eyeballed from screenshots.

**Languages.** German (default) and English via the in-app switcher, on home, browse, organizations, help, profile, my sign-ups, the organizer dashboard and org members. Locale files were diffed for key parity.

**Personas.** Vera (`vera`), Olaf (`olaf`), Admin (`admin`) - the documented staging accounts.

**Pages covered.** Public: `/`, `/opportunities`, `/organizations`, `/help`, `/contact`, `/imprint`, `/privacy-policy`, `/terms-of-use`, a 404 route. Keycloak: login (empty, filled, error), the language menu, forgot-password entry. Vera: opportunity detail (all nine seeded opportunities), the slot-selection and interest-expression dialogs, check-in, `/profile`, `/profile/settings`, `/my-signups`, notifications, the user menu. Olaf: org dashboard, opportunities, engagements, members, settings, the org-edit modal, the 4-step create-opportunity wizard, banner upload with an over-size and a wrong-type file. Admin: organizations, users, reports, audit log. Cross-cutting: 404, no-result empty states on opportunities and organizations, PWA manifest and service worker, offline behaviour on a cached route, a precached static route and an uncached data route.

**Complementary to, not duplicating, the automated suite.** ESLint `jsx-a11y`, `tsc --noEmit` and Playwright + axe-core already run in CI. Contrast work here therefore targets what axe cannot evaluate: SVG icon (non-text) contrast, and text over gradients and photographs. Keyboard work targets order and trap behaviour rather than attribute presence.

**Deliberately excluded.** Backend logic, data model, infrastructure, security, CI, performance, code hygiene, dead code - all owned by the `lens` / `einsatzbereit-review` skills. No diff or PR review.

**Test data.** No records were created on staging. The one sign-up attempt returned HTTP 409 from a re-application rate limit and the one interest submission was stopped by client-side required-field validation, so nothing needed to be withdrawn or deleted. Pre-existing debris from earlier review runs ("Testeintrag fuer Review - bitte ignorieren") is visible in Olaf's engagements list and is noted in the Parking Lot, not treated as a finding.

**Features named in the brief that do not exist in the product.** There is no map/list toggle on the browse page (Leaflet is used only for the single-marker map on the detail page), and there are no saved searches or search alerts. Those areas could not be reviewed.

---

## Findings

### Content

#### F1 - City autocomplete returns unrelated cities, then declares no match, while you are still typing

**Kategorie:** Content
**Schweregrad:** Hoch
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #1 Sichtbarkeit des Systemstatus, #9 Fehler erkennen und beheben)
**Ort:** `/opportunities` -> Standort-Filter · Persona: nicht eingeloggt (also as Vera) · Viewport: 1440 · Sprache: DE

Beleg: `f01-city-autocomplete-irrelevant.jpg`, `f01-city-autocomplete-no-match.jpg`. Measured by typing one character at a time into the Stadt field and reading the rendered suggestion list after each keystroke:

| Eingabe | Vorschläge |
|---|---|
| `L` | (none - below the 2-character threshold) |
| `Le` | Leipzig |
| `Lei` | **Köln, Dresden, Regensburg, Halle (Saale)** |
| `Leip` | **Keine passende Stadt gefunden.** |
| `Leipz` | **Keine passende Stadt gefunden.** |
| `Leipzi` | **Keine passende Stadt gefunden.** |
| `Leipzig` | Leipzig · Exakte Namensübereinstimmung |

Code-Fundstelle: `frontend/src/components/VolunteerOpportunitiesList/useCitySuggestions.ts` renders whatever the endpoint returns; `frontend/src/lib/citySuggestionSort.ts` only *sorts* by prefix match (`sortByLabelPrefixMatch`) and never filters, so a response containing no label matching the query is displayed verbatim.

Auswirkung: Location is the primary filter of a product built around "Einsätze in deiner Nähe". A user typing their own city sees four wrong cities, then a flat "no such city" for three consecutive keystrokes, immediately before the correct answer appears. Most people stop at the first "not found" and conclude the platform has nothing near them.

Verbesserungsvorschlag: Two frontend-only changes, independent of the upstream geocoder. (a) Filter the response before rendering - drop suggestions whose label does not contain the query case-insensitively; that alone removes the Köln/Dresden/Regensburg case. (b) Replace the hard "Keine passende Stadt gefunden." with a softer mid-typing state ("Weiter tippen ...") while the query is still short, or keep the last non-empty suggestion list visible instead of collapsing to a dead end. · Aufwand: S

Vermutliche Backend-Ursache: the ranking itself comes from the backend's `/v1/maps/cities` proxy over Nominatim free-text search, which does not do prefix matching. Not addressed here - the two frontend mitigations above stand on their own.

#### F2 - Same-day time ranges print the date twice

**Kategorie:** Content
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (projekteigene Konvention - `lib/format.ts` centralises every other date format; `frontend-design` writing guidance: let each element do exactly one job)
**Ort:** six locations, all `${formatDateTime(start)} - ${formatDateTime(end)}` · Persona: Vera + Olaf · Viewport: alle · Sprache: DE und EN

Beleg: `f04-same-day-range-my-signups.jpg` ("Termin: 20.08.2026, 10:00 - 20.08.2026, 14:00", wrapped over three lines in a 256 px card), `f05-detail-column-widths.jpg` ("27.08.2026, 09:00 - 27.08.2026, 17:00" in Verfügbare Zeitslots).

Code-Fundstellen - one root cause, six call sites:
- `frontend/src/components/SignUpModal.tsx:131`
- `frontend/src/components/CreateVolunteerOpportunityModal/DetailsStep.tsx:359`
- `frontend/src/pages/app/OrgDashboardPage/CalendarWidget.tsx:96`
- `frontend/src/pages/EngagementManagementPage.tsx:803`
- `frontend/src/pages/MyEngagementsPage/ActivitySection.tsx:479`
- `frontend/src/pages/VolunteerOpportunityDetailPage.tsx:437`

Auswirkung: Almost every shift on this platform starts and ends on the same day, so almost every range on screen repeats its own date. It doubles the string length in exactly the places where space is tightest (the my-sign-ups cards, the slot `<select>`), forces three-line wraps, and makes the reader parse a date twice to extract two times.

Verbesserungsvorschlag: Add `formatDateTimeRange(start, end, lng)` to `lib/format.ts` that collapses same-day ranges to `27.08.2026, 09:00 - 17:00` and keeps the full form across midnight, then replace all six call sites. · Aufwand: M

#### F3 - Slot picker jams the end time and the capacity together

**Kategorie:** Content
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #4 Konsistenz und Standards)
**Ort:** `/volunteer-opportunities/:id` -> "Zeitslot auswählen"-Dialog · Persona: Vera · Viewport: 1440 · Sprache: DE

Beleg: `f10-slot-picker-concatenation.jpg` - the option reads `27.08.2026, 09:00 - 27.08.2026, 17:00 20 Plätze frei`. Code-Fundstelle: `frontend/src/components/SignUpModal.tsx:131-143` builds the label as `` `${start} - ${end} ${spotsLabel}` `` with a bare space between the two values.

Auswirkung: "17:00 20 Plätze frei" parses as a single run of digits. In the list view the same two facts are separated by layout and read cleanly; in the dialog - the one place where the user is committing - they collide. On an opportunity with several slots this is the entire basis for the choice.

Verbesserungsvorschlag: Separate the two values with a middle dot (` · `), the separator the cards already use for meta lines, or render capacity as a right-aligned secondary element in a custom listbox rather than concatenating into one string. · Aufwand: S

#### F4 - Sign-up vocabulary changes at every step of the same flow

**Kategorie:** Content
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #4 Konsistenz und Standards; `frontend-design`: "An action keeps the same name through the whole flow")
**Ort:** `/opportunities`, `/volunteer-opportunities/:id`, `/my-signups`, Organizer-Anmeldungen · Persona: Vera + Olaf · Sprache: DE

Beleg: `f05-detail-column-widths.jpg`, `f10-slot-picker-concatenation.jpg`, `f04-same-day-range-my-signups.jpg`, `overview-org-engagements.jpg`. The same act is named, in order:

1. card badge: **Interessenbekundung** / list CTA context: **20 Plätze frei**
2. detail CTA: **Zeitslot auswählen** (or **Interesse bekunden**)
3. dialog title: **Zeitslot auswählen**, confirm button: **Mitmachen**
4. volunteer's own list: **Meine Anmeldungen**, item state **Bestätigt**, undo **Zurückziehen**
5. organizer's list: **Anmeldungen**, states **Zurückgezogen / Abgesagt / Bestätigt**
6. detail summary band: **ABLAUF: Zeitslots** / **ABLAUF: Interessenbekundung**

The noun for the thing being joined also varies: *Einsatz* (dominant), *Termin* (my-sign-ups), *Engagement* (route `/app/.../engagements`, `EngagementManagementPage`).

Auswirkung: A first-time volunteer clicks a button called "Zeitslot auswählen", confirms with a button called "Mitmachen", and then finds the result under a heading called "Anmeldungen". Each hop costs a small re-orientation. "ABLAUF: Zeitslots" in particular means nothing before you have been through the flow once.

Verbesserungsvorschlag: Pick one verb pair for the two participation modes (e.g. *anmelden* for slot-based, *Interesse bekunden* for open-ended) and use it unchanged in the CTA, the dialog title, the confirm button and the resulting state label. Rename "ABLAUF" to something a newcomer can act on ("Anmeldung: feste Zeitslots" / "Anmeldung: Interesse bekunden"). Keep *Einsatz* as the single product noun in UI copy; *Engagement* can stay in routes and code. · Aufwand: M

#### F5 - Check-in shows a bare UUID as the fallback code

**Kategorie:** Content
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (`frontend-design`: "Name things by what people control and recognize, never by how the system is built"; Nielsen-Norman-Heuristik #2 Übereinstimmung mit der realen Welt)
**Ort:** `/my-signups` -> "Einchecken" · Persona: Vera · Viewport: 1440 · Sprache: DE

Beleg: `f14-checkin-uuid.jpg`. The dialog reads "Einchecken / Zeige diesen QR-Code dem:der Organisator:in zum Einchecken." then a QR code, then the unlabelled string `019ff9d4-9930-70d0-972d-dc4aadcdfd98`, then "Fertig".

Auswirkung: The check-in happens on site, in a field, on a phone, probably with poor light and a queue behind you. When the scan fails - the fallback this string exists for - the volunteer has to read out 36 hexadecimal characters, or the organizer has to type them. Nothing on screen even says that is what the string is for.

Verbesserungsvorschlag: Label it ("Falls der Scan nicht klappt, nenne diesen Code") and shorten what is displayed to a human-transferable prefix (the first 8 characters are already unique across staging), or replace the display with a short numeric code. Also say what happens after check-in ("Der:die Organisator:in bestätigt deine Teilnahme vor Ort"). · Aufwand: M

Vermutliche Backend-Ursache: the short code would need to come from, or be validated by, the backend. The labelling and truncation are frontend-only.

#### F6 - Thirteen API error strings show internal field names to users

**Kategorie:** Content
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (`frontend-design`: errors are written in the interface's voice, never in the system's)
**Ort:** `frontend/src/locales/de.json` / `en.json`, `apiError.*` · beide Sprachen

Beleg: 13 of 26 `apiError` groups render a raw backend property name into user-facing text, identically in both locales:

```
VolunteerOpportunityId darf nicht leer sein.
OrganizationInvitationId darf nicht leer sein.
OrganizationDashboardLayoutId darf nicht leer sein.
UserStreakId darf nicht leer sein.        (+9 more)
```

`apiError.User.AlreadyDeleted` similarly reads "Nutzer:in ist bereits **schattengelöscht**" - an implementation term ("shadow delete") surfaced to admins.

Auswirkung: These are "should never happen" validation guards, so the blast radius is small - but when one does fire, the user is shown a developer identifier and given no action. The rest of the error copy in this product is unusually good (see F1's counter-example in the upload validator), which makes these stand out.

Verbesserungsvorschlag: Collapse the thirteen `*Id.Empty` variants into one human string ("Diese Anfrage war unvollständig. Lade die Seite neu und versuche es erneut.") and replace "schattengelöscht" with "deaktiviert" or "ausgeblendet". · Aufwand: S

#### F7 - Help page points at itself instead of linking, and the email address is a link on one page and plain text on two others

**Kategorie:** Content
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #10 Hilfe und Dokumentation; WCAG 2.2 AA 3.2.4 Consistent Identification)
**Ort:** `/help`, `/contact`, `/imprint` · nicht eingeloggt · 1440 · DE

Beleg: `f16-help-page.jpg`. Three separate inconsistencies on the same address:
- `/help` hero: "Nicht gefunden, was du suchst? Siehe „Noch Hilfe nötig?" unten." - a scroll instruction where an anchor link belongs, in a page short enough that the section is one screen away.
- `/help` body: "... oder schreibe uns direkt an maikhslr@gmail.com" - **plain text**, in a sentence where "Einsatz-", "Organisationsseite" and "Kontaktseite" immediately next to it *are* links.
- `/imprint`: the same address and the site URL are both plain text (`PLAIN P`), while `/contact` renders the address as a proper `mailto:` link.

Auswirkung: On mobile, a plain-text address means select-copy-switch-app instead of one tap. The inconsistency across three legal/support pages is the kind of detail that quietly signals "unfinished" on exactly the pages users visit when they need to trust the operator.

Verbesserungsvorschlag: Make the hero sentence an in-page anchor to `#noch-hilfe-noetig`; wrap both address occurrences in `mailto:` links using the same link class as their neighbours; link the site URL on `/imprint`. · Aufwand: S

#### F8 - Homepage FAQ and the Help Center answer different questions

**Kategorie:** Content
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #10)
**Ort:** `/` (Noch Fragen?) -> `/help` · nicht eingeloggt · 1440 · DE

Beleg: `overview-home-1440.jpg` vs `f16-help-page.jpg`. Home asks: *Kostet die Nutzung von Einsatzbereit etwas? / Brauche ich ein Konto, um mitzumachen? / Wie schnell kann meine Organisation starten? / Ist der Code wirklich offen einsehbar?* The link below it says "Weitere Fragen im Hilfe-Center". `/help` then answers six entirely different questions (*Wie melde ich mich für einen Einsatz an?* etc.) and none of the four.

Auswirkung: "Weitere Fragen" promises a superset and delivers a disjoint set. A visitor who followed the link to re-read the pricing answer will not find it.

Verbesserungsvorschlag: Make `/help` the single source and have the homepage render a subset of it (four entries, same strings), so the link genuinely leads to "more of the same". · Aufwand: S

#### F9 - Offline empty state repeats its own heading verbatim

**Kategorie:** Content
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (`frontend-design`: "Let each element do exactly one job. A label labels, an example demonstrates, and nothing quietly does double duty")
**Ort:** `/opportunities` im Offline-Zustand · 375 · DE

Beleg: `f06-offline-list-state.jpg` - heading "Du bist offline", body "Du bist offline. Sobald deine Verbindung zurück ist, laden wir die Einsätze."

Auswirkung: Minor, but the body line's first sentence is pure repetition; the only new information ("Sobald deine Verbindung zurück ist ...") is buried behind it.

Verbesserungsvorschlag: Body becomes "Sobald deine Verbindung zurück ist, laden wir die Einsätze automatisch." · Aufwand: S

#### F10 - `/organizations` eyebrow repeats the h1, and the subline undersells the page

**Kategorie:** Content
**Schweregrad:** Niedrig
**Konfidenz:** Werturteil
**Einordnung:** Präferenz
**Ort:** `/organizations` · nicht eingeloggt · 1440 · DE

Beleg: `overview-home-1440.jpg` for the pattern; on `/organizations` the eyebrow reads `ORGANISATIONEN` directly above the h1 `Organisationen`. Every other page uses the eyebrow as a *category* for the title (`SUPPORT` / Kontakt, `RECHTLICHES` / Impressum, `EHRENAMT` / Einsätze finden); here the device collapses into an echo. The subline - "Finde Organisationen auf Einsatzbereit, die du schon kennst." - frames a directory as a lookup tool for names you already have, which is the narrower of the two things people come here to do.

Auswirkung: A small break in an otherwise consistent header system, plus a headline promise smaller than what the page actually offers.

Verbesserungsvorschlag: Eyebrow `PLATTFORM` or `VERZEICHNIS`; subline along the lines of "Entdecke die Vereine und Initiativen, die auf Einsatzbereit Einsätze veröffentlichen." · Aufwand: S

#### F11 - A personal Gmail address is the platform's only support channel, with a 24-hour response promise

**Kategorie:** Content
**Schweregrad:** Niedrig
**Konfidenz:** Werturteil
**Einordnung:** Präferenz
**Ort:** `/contact`, `/help`, `/imprint` · nicht eingeloggt · DE und EN

Beleg: `f16-help-page.jpg` - "schreibe uns direkt an maikhslr@gmail.com - wir antworten in der Regel innerhalb von 24 Stunden."; the same address appears on `/contact` and twice on `/imprint`.

Auswirkung: The Impressum entry is legally required and correct. The issue is the *support* framing: a platform that asks volunteers and Vereine to hand over personal data routes every non-abuse question to a free consumer mailbox, uses "wir" for a single maintainer, and commits publicly to a 24-hour SLA that one person cannot hold during a holiday. That combination reads as less trustworthy than a plain, honest statement would.

Verbesserungsvorschlag: Use a domain address (`hallo@einsatzbereit.maik-hasler.de`) forwarding to the same mailbox, and soften the promise ("Ich antworte meist innerhalb weniger Tage") - honest and smaller beats confident and unbacked. · Aufwand: S

---

### Visuelles Design

#### F12 - Organizer dashboard: fixed-height tiles, an empty default calendar week, and half a viewport of unused space

**Kategorie:** Visuelles Design
**Schweregrad:** Hoch
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #8 Ästhetisches und minimalistisches Design, #1 Sichtbarkeit des Systemstatus)
**Ort:** `/app/:orgId/dashboard` · Persona: Olaf · Viewport: 1440 und 375 · Sprache: DE

Beleg: `f02-org-dashboard-empty-widgets.jpg` (1440), `f02-org-dashboard-375.jpg` (375). Measured card boxes at 1440x900:

| Widget | Kartengröße | Inhalt |
|---|---|---|
| Bevorstehende Einsätze | 688 x **712** px | one row (~110 px) plus a link |
| Freiwillige | 688 x **348** px | the numeral `1` and a caption |
| Kalender | 681 x **816** px | a week grid showing no events |
| Einsatz erstellen | 333 x 192 px | one button |
| Einstellungen | 158 x 192 px | a gear icon, no label, no link text |
| Organisation | 1376 x 166 px | avatar, name, two meta links |

Three compounding problems: (a) tiles stretch to a fixed row height regardless of content, producing 600 px of white space inside a single card; (b) the calendar defaults to **Woche** scrolled to **00:00**, so ten rows of empty night hours fill the largest tile while the org's only event (27.08.) sits outside the visible week - the mobile view defaults to **Agenda** and immediately shows it, which is the better default; (c) only the Organisation strip uses the full 1376 px, everything else stops at ~690 px, leaving the right half of the screen empty below the two small top tiles.

Auswirkung: This is the first screen an organizer sees after signing up, and it looks broken rather than empty - there is no empty-state message explaining what the blank space is for. The one number an organizer actually wants (upcoming shifts and how many people signed up) is present but drowned. "Dashboard anpassen" - the control that would fix the layout - is a dashed strip at the very bottom, below all the white space, where nobody will look.

Verbesserungsvorschlag: (1) Let tiles size to content (`align-items: start` on the grid rows) or give each an empty state that fills the reserved space with something useful. (2) Default the calendar to **Agenda** at both breakpoints, or to **Monat** - never to a week grid anchored at midnight. (3) Give the Einstellungen tile a label and a description; a lone gear icon in a 158 px box is not a control anyone will click deliberately. (4) Move "Dashboard anpassen" next to the h1 as a secondary action. · Aufwand: L

#### F13 - Content width changes inside a single page

**Kategorie:** Visuelles Design
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #4 Konsistenz und Standards)
**Ort:** `/volunteer-opportunities/:id`, `/my-signups`, `/app/:orgId/dashboard/settings` · Vera + Olaf · 1440 · DE

Beleg: `f05-detail-column-widths.jpg`. Measured on the detail page at 1440x900:

```
Verfügbare Zeitslots            x = 144 .. 816    (w = 672)
Über diese Organisation         x = 144 .. 816    (w = 672)
Weitere Einsätze dieser Org.    x = 144 .. 1296   (w = 1152)
```

All three are left-aligned at 144, so the right edges disagree by 480 px inside one scroll. On `/my-signups` the engagement cards render at **256 px wide in a 1440 px viewport**, which is what forces "Termin: 20.08.2026, 10:00 - 20.08.2026, 14:00" onto three lines (`f04-same-day-range-my-signups.jpg`). `/app/:orgId/dashboard/settings` similarly stops at ~700 px with the right half of the screen unused.

Auswirkung: The eye reads a hard right edge as the page's boundary; when a later section crosses it, the page looks like two pages stacked. On my-sign-ups the narrow column also causes real content damage - a 256 px card cannot hold an organisation name or a time range on one line.

Verbesserungsvorschlag: Pick one container width per page and hold it - either widen the detail page's main column to match the related-opportunities grid, or constrain that grid to 672 px. On `/my-signups`, let the engagement grid use the same `sm:grid-cols-2 xl:grid-cols-3` rhythm as the browse list so cards get ~440 px. · Aufwand: M

#### F14 - The footer marketing CTA is the second-largest type on every page, including the admin console

**Kategorie:** Visuelles Design
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Präferenz (die Typo-Skala selbst ist konsistent - hier geht es um die Hierarchie)
**Ort:** alle Seiten mit dem öffentlichen Footer · alle Personas · 1440 · DE

Beleg: computed styles sampled across `/` and `/opportunities`:

```
H1 "Dein Ehrenamt beginnt hier."   Barlow Condensed 96px/96px w700
H1 "Einsätze finden"               Barlow Condensed 72px/72px w700
H2 "Bereit, wenn du es bist."      Barlow Condensed 48px/48px w700   <- footer CTA
H2 "Diese Einsätze suchen Leute"   Barlow Condensed 36px/40px w700   <- real section heading
```

The repeated footer promo outranks every genuine section heading on the site by 12 px, and it appears unchanged on `/administration/audit-log` (`f08-admin-marketing-chrome.jpg`).

Auswirkung: Every page ends with a recruitment shout louder than its own content. On the admin console an operator reviewing abuse reports is told "Bereit, wenn du es bist." at 48 px.

Verbesserungsvorschlag: Set the footer CTA at 36 px so it sits at section-heading level rather than above it, and suppress the CTA block entirely on `/administration/*` (see F19). · Aufwand: S

#### F15 - Mobile hero reserves ~130 px of empty green below the search box

**Kategorie:** Visuelles Design
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Präferenz
**Ort:** `/` · nicht eingeloggt · 375 · DE

Beleg: `f27-mobile-hero-dead-space.jpg`. The hero card runs to y≈700; the "Suchen" button ends at y≈565. The desktop hero fills that region with cropped photographs and colour blobs; at 375 those are dropped but the card keeps its height.

Auswirkung: On the first screen of the product, roughly a fifth of the hero is empty dark green. It is the difference between a composition and a container.

Verbesserungsvorschlag: Either let the card height follow its content at `<sm`, or keep one of the photo shapes as a bottom-anchored accent so the space is doing work. · Aufwand: S

---

### UX

#### F16 - Offline is a designed state on the list and an "unexpected error" on the detail page

**Kategorie:** UX
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #9 Fehler erkennen, diagnostizieren, beheben)
**Ort:** `/opportunities` vs `/volunteer-opportunities/:id` · nicht eingeloggt · 375 · DE

Beleg: `f06-offline-list-state.jpg` vs `f06-offline-detail-error.jpg`. With the service worker registered and the network cut:

- `/opportunities` -> icon, heading "Du bist offline", body "Sobald deine Verbindung zurück ist, laden wir die Einsätze." A state, exactly as it should be.
- `/imprint` (precached static route) -> renders completely. Correct.
- `/volunteer-opportunities/019ff9d4-...` -> "**Fehler: Ein unerwarteter Fehler ist aufgetreten. Bitte versuche es später erneut.**" with a "Erneut versuchen" button.

Auswirkung: The detail route is the one people deep-link into - from a notification, a shared link, a home-screen shortcut. Being told the app hit an unexpected error, when the only thing that happened is a lost connection, sends users to reload, to reinstall, or to conclude the site is broken. The app already knows how to phrase this correctly one route away.

Verbesserungsvorschlag: Route the detail page's fetch failure through the same `navigator.onLine` check the list uses (`lib/onlineStatus.ts` already exists) and render the offline state instead of the generic error. Add the list's missing "Erneut versuchen" button while you are there, so both states offer the same recovery. · Aufwand: S

#### F17 - Failed geocoding leaves a large empty box on the detail page

**Kategorie:** UX
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #8)
**Ort:** `/volunteer-opportunities/019ff9d4-9823-7d6f-...` (Gassi-Dienst, Tierparkweg 5, 04177 Leipzig) · Vera · 1440 · DE

Beleg: `f12-interest-modal.jpg` - a ~250 px dashed placeholder containing a small pin icon and "Keine Karte für diese Adresse verfügbar.", occupying the position the map would fill.

Auswirkung: The message is honest and correctly written, which is good. But it reserves map-sized real estate to say nothing, on a page whose "WO" value is already printed in text a few hundred pixels above. Two of nine seeded opportunities hit this, so it is not an edge case in practice.

Verbesserungsvorschlag: Collapse the block entirely when there are no coordinates and promote the address instead (larger type, with a link out to a maps app), rather than holding the space for an absent map. · Aufwand: S

Vermutliche Backend-Ursache: the geocode itself comes from the backend's Nominatim proxy. Not addressed here.

#### F18 - The map cannot be zoomed and offers no way out to a real map

**Kategorie:** UX
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Präferenz (die statische Karte ist eine bewusste Entscheidung - siehe Code-Kommentar; das Finding betrifft die fehlende Eskalation)
**Ort:** `/volunteer-opportunities/:id` · Vera · 1440 und 375 · DE

Beleg: `f05-detail-column-widths.jpg`; measured container 339x254 px at 375. Code-Fundstelle: `frontend/src/components/SingleMarkerMap.tsx:47-58` disables `dragging`, `scrollWheelZoom`, `doubleClickZoom`, `touchZoom`, `boxZoom`, `keyboard` and `zoomControl`, fixed at `zoom={14}`. The marker does carry an accessible name via `title` (`:66`), so the a11y side is handled - there is no keyboard trap and no scroll conflict on touch, both of which I checked.

Auswirkung: Zoom 14 in a 254 px box shows roughly a district. For an address in a city the user knows, that is enough. For anything unfamiliar or rural it is not, and there is no zoom out, no pan, and no "in Karten öffnen" escape - so the user has to copy the address by hand into another app to answer "can I actually get there?", which is the single most decisive question before signing up.

Verbesserungsvorschlag: Keep the map static, and add a link below it that hands the address to the device's map app (`https://www.openstreetmap.org/?mlat=..&mlon=..#map=16/..` or a `geo:` URI on mobile). One link, no new interaction model. · Aufwand: S

#### F19 - The admin console wears the public marketing chrome

**Kategorie:** UX
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #4 Konsistenz und Standards)
**Ort:** `/administration/*` · Persona: Admin · 1440 · DE

Beleg: `f08-admin-marketing-chrome.jpg`, `f07-admin-audit-log.jpg`. The four admin sections render inside `AppLayout`: a 280 px decorative green hero band, then the content, then the full public footer with the "Bereit, wenn du es bist. / Einsätze finden" recruitment card. The organizer console at `/app/:orgId/*` - a comparable operational surface - correctly uses a compact chrome with a slim link-row footer (`overview-org-members.jpg`).

Auswirkung: A moderator working through abuse reports gets a volunteer-recruitment pitch under the queue, and 280 px of every screen goes to a decorative band above a dense list. Two different answers to the same question inside one product.

Verbesserungsvorschlag: Move `/administration/*` onto the same compact layout the org console already uses, or at minimum suppress the footer CTA block there. · Aufwand: M

#### F20 - All four admin sections share one page title and one h1

**Kategorie:** UX
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #1 Sichtbarkeit des Systemstatus; WCAG 2.2 AA 2.4.2 Page Titled)
**Ort:** `/administration/organizations|users|reports|audit-log` · Admin · 1440 · DE

Beleg: `f07-admin-audit-log.jpg`, `f08-admin-marketing-chrome.jpg`. Measured:

| Route | `document.title` | `h1` |
|---|---|---|
| `/administration/organizations` | Administration \| Einsatzbereit | Administration |
| `/administration/users` | Administration \| Einsatzbereit | Administration |
| `/administration/reports` | Administration \| Einsatzbereit | Administration |
| `/administration/audit-log` | Administration \| Einsatzbereit | Administration |

The section name exists only as an `h2` in the content column. The org console does this correctly - `Mitglieder - Lindenauer Nachbarschaftshilfe e.V. | Einsatzbereit` - so the pattern is established elsewhere in the same codebase.

Auswirkung: Four browser tabs, four bookmarks and four history entries that cannot be told apart. A screen-reader user navigating by h1 hears "Administration" on every section.

Verbesserungsvorschlag: Set the title per child route via the existing `usePageTitle` hook, and make the section name the `h1` with "Administration" demoted to the eyebrow - which is exactly what the band above it already renders (`PLATTFORM`). · Aufwand: S

#### F21 - "Bitte wende dich direkt an die Organisation" names a recovery step it does not provide

**Kategorie:** UX
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #9)
**Ort:** `/volunteer-opportunities/:id` -> Zeitslot-Dialog, bei HTTP 409 · Vera · 1440 · DE

Beleg: encountered live on Erste-Hilfe-Kurs. `POST /v1/volunteer-opportunities/.../engagements` returned 409 and the dialog rendered, correctly inline and in red: "Diese Anmeldung wurde bereits zu oft zurückgezogen und erneut angemeldet. Bitte wende dich direkt an die Organisation."

Auswirkung: The placement and wording are right - it is inside the dialog, it explains what happened, it names a way out. But the way out is not clickable: the organisation's email and phone are further down the same page, behind a dialog the user must first dismiss. A message that names an action should carry it.

Verbesserungsvorschlag: Append a link to the organisation's contact block (`#ueber-diese-organisation`) or surface the email directly in the error. · Aufwand: S

#### F22 - Notification panel clips its next item mid-line

**Kategorie:** UX
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Präferenz
**Ort:** Header-Glocke, alle eingeloggten Seiten · Vera · 1440 · DE

Beleg: `f11-notification-dropdown-clip.jpg`. Measured: the list container is `clientHeight 320 / scrollHeight 529`, `overflow-y: auto`, six items. It does scroll - but the boundary falls across the fourth item's timestamp line, and with Chromium's overlay scrollbars there is no bar, no fade and no chevron at that edge.

Auswirkung: A half-sliced line of text reads as a rendering bug, not as "there is more below". Users who do not try to scroll conclude they have seen everything.

Verbesserungsvorschlag: Set the panel's max-height to a whole number of rows, or add a bottom fade mask - the same device `OrgPageHeader.tsx:176-183` already uses for its scrollable tab rail. · Aufwand: S

#### F23 - Mobile dashboard drops meta the desktop version shows

**Kategorie:** UX
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #4)
**Ort:** `/app/:orgId/dashboard`, Widget "Bevorstehende Einsätze" · Olaf · 375 vs 1440 · DE

Beleg: `f02-org-dashboard-empty-widgets.jpg` shows "Erste-Hilfe-Kurs / 27.08.2026, 09:00 · 0/20 Anmeldungen"; `f02-org-dashboard-375.jpg` shows only "Erste-Hilfe-Kurs".

Auswirkung: The date and the sign-up count are the entire point of an "upcoming" widget, and they are dropped on the viewport an organizer is most likely to check between other things.

Verbesserungsvorschlag: Keep the meta line at all breakpoints - it fits on two lines at 375 - and drop something else if space is needed. · Aufwand: S

---

### UI

#### F24 - Functional icons fall below the WCAG non-text contrast floor

**Kategorie:** UI
**Schweregrad:** Hoch
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (WCAG 2.2 AA, SC 1.4.11 Non-text Contrast - 3:1 für bedeutungstragende grafische Objekte)
**Ort:** `/opportunities` Filter-Chips (6x), plus `Spinner.tsx` und `FilterDropdown.tsx` · alle Personas · alle Viewports · beide Sprachen

Beleg: `f03-filter-chip-icons.jpg`; colours read from `getComputedStyle` on the rendered SVGs and converted to contrast ratios against their actual backgrounds:

| Element | Farbe | Hintergrund | Ratio | Erforderlich |
|---|---|---|---|---|
| Filter-Chip Leading-Icon (Pin, Tag, Personen, Globus, Uhr, Kalender) | `--color-brand-400` `#5bbf8c` | white | **2.26:1** | 3:1 |
| Filter-Chip Chevron | gray-400 `#99a1af` | white | **2.50:1** | 3:1 |
| `FilterDropdown.tsx:143` inactive icon | `text-brand-400` `#5bbf8c` | white | **2.26:1** | 3:1 |
| `FilterDropdown.tsx:143` active icon | `text-brand-500` `#3eaf78` | white | **2.76:1** | 3:1 |
| `Spinner.tsx:19` | `text-brand-500` `#3eaf78` | white / brand-50 | **2.76 / 2.59:1** | 3:1 |

For reference, the palette values that *do* pass: `brand-600 #2d8a5e` = 4.28:1, `brand-700 #226947` = 6.60:1. All text on these surfaces passes comfortably (gray-600 7.56:1, gray-500 4.84:1, the orange deadline `#bb4d00` 5.03:1) - which is why axe-core, whose `color-contrast` rule evaluates text only, does not flag any of this.

Auswirkung: The chevron is the only cue that a filter chip opens a menu, and the leading pin/tag/clock icons are the only cue to what each chip filters - the six chip labels are otherwise identical grey pills. At 2.26:1 they wash out for users with low vision, on a sunlit phone, or on a low-quality display. The spinner is a status indicator - the one moment the user needs to see that something is happening.

Verbesserungsvorschlag: Move all functional icons to `brand-600` (4.28:1) or `brand-700` (6.60:1) and the chevrons to `gray-500` (4.84:1); keep `brand-400`/`brand-500` for decorative fills only (the 404 illustration is legitimately exempt). Since this is a palette-token decision rather than six one-off classes, it is worth adding to `frontend/AGENTS.md` alongside the existing a11y conventions so `a11y-check` can enforce it. · Aufwand: S

#### F25 - Related-opportunity cards are a second card design for the same object

**Kategorie:** UI
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #4 Konsistenz und Standards)
**Ort:** `/volunteer-opportunities/:id` -> "Weitere Einsätze dieser Organisation" vs `/opportunities` · Vera · 1440 · DE

Beleg: `f05-detail-column-widths.jpg` vs `f03-filter-chip-icons.jpg`. The browse card carries category chip · frequency chip · participation badge on one row, then title, then a dated meta line, then a footer with the organisation avatar and location. The related card carries category chip · participation badge on row one, frequency chip *with its own calendar icon* on row two, title, description, and a location line - **no date, no organisation footer, different chip order, different row structure**.

Auswirkung: Two visual grammars for the same entity, one scroll apart. The related card also drops the date, which is the field a volunteer scans first.

Verbesserungsvorschlag: Reuse `PublicOpportunityCard` / `OpportunityListItem` for the related grid, with a `compact` variant if the density needs to differ. · Aufwand: M

#### F26 - Org tab rail hides two of five tabs at 375 px behind a fade that reads as a rendering edge

**Kategorie:** UI
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Präferenz
**Ort:** `/app/:orgId/dashboard/*` · Olaf · 375 · DE

Beleg: `f23-org-tabs-375.jpg` (scrolled - the left fade is visible), `f02-org-dashboard-375.jpg` (unscrolled - "Einstell…" is cut at the viewport edge). Measured: `scrollWidth 474 / clientWidth 343`, `overflow-x: auto`; tab offsets Dashboard@16, Einsätze@109, Anmeldungen@187, Einstellungen@298, Mitglieder@406.

Auswirkung: It genuinely scrolls and the gradient masks exist (`OrgPageHeader.tsx:176-183`), so this is not broken - but at rest, "Mitglieder" is entirely off-screen and "Einstellungen" is half-cut with a fade subtle enough to read as clipping. Member management is a primary organizer task.

Verbesserungsvorschlag: Strengthen the right-edge gradient, or scroll the active tab into view on mount so the rail visibly starts mid-list and its scrollability is self-evident. · Aufwand: S

---

### Barrierefreiheit

> Note: keyboard order, focus visibility, the skip link, dialog labelling and reduced motion were all checked and are in good shape - see Strengths below. The findings here are the gaps that remain.

#### F27 - Keycloak: the login error is not programmatically tied to the fields it invalidates

**Kategorie:** Barrierefreiheit
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (WCAG 2.2 AA - 3.3.1 Error Identification, 1.3.1 Info and Relationships)
**Ort:** Keycloak-Login (FTL-Template, nicht React) · nicht eingeloggt · 1440 · DE und EN

Beleg: `f09-keycloak-error-state.jpg`. After submitting wrong credentials, the DOM reads:

```html
<input id="username" ... aria-invalid="true" aria-describedby=null>
<input id="password" ... aria-invalid="true" aria-describedby=null>
<span id="input-error" class="form-error" aria-live="polite">
  Benutzername oder Passwort stimmt nicht.
</span>
```

Both inputs are marked invalid but neither points at `#input-error`. And `aria-live="polite"` cannot help here: this is a **server-rendered full page load**, so the region exists at parse time and there is no mutation for the live region to announce.

Auswirkung: A screen-reader user tabbing back into the username field hears "invalid entry" and nothing else - not why, not that the password was the likely culprit, not that the field was cleared. They can find the message by reading the page linearly, but the association that would deliver it at the point of correction is missing. This surface is outside the React app, so neither `jsx-a11y` nor the axe-core E2E suite covers it.

Verbesserungsvorschlag: In `keycloak/.../login.ftl`, add `aria-describedby="input-error"` to both inputs when `messagesPerField` has an error, and give the span `role="alert"` (harmless on load, correct if the theme ever moves to client-side validation). · Aufwand: S

Second, smaller point on the same screen: the error text sits under the *username* field although the failure is about the credential pair, which nudges the user to re-check the wrong field first. Moving it above the submit button - where it applies to the form, not to one input - would match what actually failed.

#### F28 - German content under `lang="en"` is never marked as German

**Kategorie:** Barrierefreiheit
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (WCAG 2.2 AA - 3.1.2 Language of Parts)
**Ort:** alle Seiten mit nutzergenerierten Inhalten, EN-Modus · alle Personas · 1440 · EN

Beleg: `overview-home-en.jpg`. With the switcher on English, `document.documentElement.lang === "en"` and the chrome is fully translated - but the opportunity titles and descriptions stay German: "Patenschaft für Pflegetiere", "Übernimm eine Patenschaft für ein Pflegetier und begleite es bis zur Vermittlung.", "Lindenauer Nachbarschaftshilfe e.V.". A DOM scan for `[lang]` returns only `HTML=de|en`; no content node carries a `lang` override.

Auswirkung: A screen reader reads German text with English phonemes - typically unintelligible rather than merely accented. This is the one AA criterion in this review that the existing automated suite genuinely cannot catch, because axe has no way to detect the language of a text run.

Verbesserungsvorschlag: The create-opportunity wizard already collects per-language title and description (`Deutsch` / `English` tabs with a completeness dot). `pickLocalizedText` in `lib/format.ts` therefore already knows which language it fell back to - have it return that alongside the string, and render `lang={resolvedLang}` on the element. Where content exists in only one language this is a small change with a real payoff. · Aufwand: M

#### F29 - Card titles and footer column headings share one heading level with no results-region heading

**Kategorie:** Barrierefreiheit
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (WCAG 2.2 AA 1.3.1 Info and Relationships; nicht von axe' `heading-order` erfasst, da die Reihenfolge selbst gültig ist)
**Ort:** `/opportunities` · nicht eingeloggt · 1440 · DE

Beleg: measured heading outline:

```
H1 Einsätze finden
  H2 Wir suchen Helfer:innen        <- sr-only status/count region
  H2 Erste-Hilfe-Kurs               ┐
  H2 Patenschaft für Pflegetiere    ├ 9 result cards
  ... (7 more)                      ┘
  H2 Bereit, wenn du es bist.       <- marketing CTA
  H2 PLATTFORM / SUPPORT / RECHTLICHES  <- footer columns
```

Auswirkung: A screen-reader user navigating by heading gets thirteen level-2 headings in a flat row, with the nine results, the marketing CTA and the three footer column labels indistinguishable by level. There is no heading that says "these are your results", so there is no landmark to jump the list from or to.

Verbesserungsvorschlag: Give the results region a real (optionally `sr-only`) `h2` such as "Suchergebnisse", demote the card titles to `h3` beneath it, and drop the footer column labels to `h3` (or replace them with `<nav aria-label>` groupings) so page chrome does not compete with page content in the outline. · Aufwand: S

#### F30 - Card image hover scale is the one motion effect not gated on `prefers-reduced-motion`

**Kategorie:** Barrierefreiheit
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (WCAG 2.2 AAA 2.3.3 Animation from Interactions - unterhalb AA, daher Best Practice statt Pflicht; projekteigene Konvention: der Rest des Motion-Systems ist korrekt gegated)
**Ort:** `/opportunities`, `/` -> Karten mit Bannerbild · alle Personas · 1440 · DE

Beleg: `frontend/src/components/VolunteerOpportunitiesList/OpportunityListItem.tsx:184` -

```
transition-transform duration-300 group-hover:scale-105
```

Every keyframe animation in `styles/global.css:19-49` (`fade-up`, `scroll-behavior: smooth`) is correctly wrapped in `@media (prefers-reduced-motion: no-preference)`, and `Spinner.tsx` carries `motion-reduce:animate-none`. This one transform is the only unguarded motion in the codebase - a grep for `hover:scale|hover:-translate|group-hover:scale` without a `motion-reduce` sibling returns exactly one hit.

Auswirkung: Small - a 5% scale over 300 ms - but it is a transform, and for vestibular-sensitive users a grid of cards that each lurch on pointer entry is the exact pattern the preference exists to suppress. The rest of the system is so consistent that this reads as an oversight rather than a decision.

Verbesserungsvorschlag: Add `motion-reduce:transform-none` (or `motion-reduce:transition-none`) to that class list. · Aufwand: S

---

### i18n

#### F31 - English bell label stutters

**Kategorie:** i18n
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (`frontend-design`: no filler, each element does one job)
**Ort:** Header-Glocke, alle eingeloggten Seiten · Olaf · 1440 · EN

Beleg: measured accessible name `"Notifications, 10 unread notifications"`. `locales/en.json`:

```
notifications.bellLabelWithCount_one   = "Notifications, {{count}} unread notification"
notifications.bellLabelWithCount_other = "Notifications, {{count}} unread notifications"
```

The German equivalent is clean: `"Benachrichtigungen, {{count}} ungelesen"`.

Auswirkung: Screen-reader users on English hear the noun twice on every focus of the bell. Cosmetic, trivially fixable.

Verbesserungsvorschlag: `"Notifications, {{count}} unread"` in both plural forms, matching the German construction. · Aufwand: S

#### F32 - PWA manifest is German-only regardless of the chosen language

**Kategorie:** i18n
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (W3C Web App Manifest - `lang` beschreibt die Sprache der Manifest-Strings)
**Ort:** `/manifest.webmanifest` · alle Personas · 375 · EN

Beleg: fetched live. `"lang": "de"`, and every human-readable string is German only:

```json
"description": "Einsatzbereit verbindet engagierte Freiwillige mit regionalen Hilfsangeboten ...",
"shortcuts": [
  { "name": "Einsätze finden", "short_name": "Einsätze", "description": "Freiwilligeneinsätze in deiner Nähe durchsuchen" },
  { "name": "Meine Anmeldungen", "short_name": "Anmeldungen", ... }
],
"screenshots": [ { "label": "Startseite mit Suche nach Einsätzen in deiner Nähe" }, ... ]
```

Auswirkung: An English-locale user who switches the UI to English and then installs the PWA gets German long-press shortcuts on their home screen and a German description in the install sheet. The manifest is otherwise unusually complete - maskable icon, five screenshots with `form_factor`, two shortcuts, matching `theme_color` - which makes this the one loose end.

Verbesserungsvorschlag: Serve the manifest from a small route that reads the persisted language preference and emits the matching strings, or ship `manifest.de.webmanifest` / `manifest.en.webmanifest` and swap the `<link rel="manifest">` href when the language changes. · Aufwand: M

#### F33 - Keycloak language menu mixes two naming conventions

**Kategorie:** i18n
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Präferenz
**Ort:** Keycloak-Login -> Sprachumschalter (FTL-Template) · nicht eingeloggt · 1440 · DE

Beleg: the open `<details>` menu lists **Deutsch** and **Englisch (English)** - the first in its own language, the second in German with the endonym in brackets.

Auswirkung: Tiny, but language pickers are read by people who cannot read the current language, so endonyms (`Deutsch` / `English`) are the convention that works for everyone. The in-app switcher already gets this right.

Verbesserungsvorschlag: Use `Deutsch` and `English` in the Keycloak theme, matching the React app. · Aufwand: S

#### F34 - Keycloak: "Passwort vergessen?" does not look like a link

**Kategorie:** UI (Keycloak/FTL)
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (WCAG 2.2 AA 1.4.1 Use of Color; Nielsen-Norman-Heuristik #6 Wiedererkennen statt Erinnern)
**Ort:** Keycloak-Login · nicht eingeloggt · 1440 und 375 · DE und EN

Beleg: `f09-keycloak-error-state.jpg`. On one card there are two links styled in opposite ways: **Registrieren** is brand green and bold; **Passwort vergessen?** is muted grey, regular weight, no underline - visually identical to the static label "Angemeldet bleiben" sitting on the same row. Nothing but hue separates it from body text.

Auswirkung: Password recovery is the highest-stakes link on the page and the one people reach for while already frustrated. Rendering it as the quietest element on the card, next to a static label, buries it.

Verbesserungsvorschlag: Give it the same treatment as the other links (brand colour, or an underline), or at minimum raise it above the "Angemeldet bleiben" label's weight so the row reads as label + link rather than two labels. · Aufwand: S

---

## Strengths worth protecting

Not findings - things this review found done well, which are worth naming so they do not get refactored away.

- **Upload validation copy.** `„big.png" ist 4,1 MB groß - erlaubt sind maximal 2 MB.` and `„bad.gif" ist kein unterstütztes Bild. Erlaubt sind JPEG, PNG und WebP.` name the file, the actual value and the limit, and the constraint is stated up front under the drop zone before anyone tries. `strength-upload-validation.jpg`
- **Keyboard order and focus.** Tab order on `/opportunities` follows visual order exactly through 40 stops (header → hero → six filter chips → nine cards, title then organisation, left to right by row → Mehr laden → footer), with a consistent 2 px solid outline plus a white halo on every stop. No traps found. The skip link ("Zum Inhalt springen") appears on first Tab and works. `strength-skip-link.jpg`
- **Dialog semantics.** The sign-up dialog carries `role="dialog" aria-modal="true" aria-labelledby="sign-up-dialog-title"` and moves focus to the first control inside on open.
- **Destructive-action confirmations.** "Mitglied entfernen? / Möchtest du Vera Volunteer wirklich aus dieser Organisation entfernen? / Behalten · Ja, entfernen" - names the person, and the safe option is the one that says what it does.
- **The create-opportunity wizard.** Four labelled steps, an `sr-only` "Schritt 1 von 4" announcement, per-language tabs with a completeness dot, inline `Bitte ausfüllen.` with red field borders, live character counters, and "Als Entwurf speichern" as a real escape hatch. `strength-create-wizard.jpg`
- **Empty states.** "Keine Einsätze gefunden. / Passe deine Filter an oder setze sie zurück." with a reset button - and the accompanying count line is deliberately `sr-only` while the list is empty specifically so sighted users do not read the same sentence twice (`OpportunityResultsList.tsx:70-77`). That is a considered decision, documented in the code. `strength-empty-state.jpg`
- **Reduced motion.** Every keyframe animation and `scroll-behavior: smooth` lives inside `@media (prefers-reduced-motion: no-preference)` - the correct direction of the guard, not an override. One exception, F30.
- **Translation discipline.** 1333 keys in each locale, zero drift in either direction, no raw keys observed anywhere, and language switching preserves both route and state (`/profile` stayed `/profile`, `html lang` flipped to `en`).
- **Typography.** Barlow Condensed reserved for h1 and major section h2s, Source Sans 3 for everything else, on a tight and consistently applied scale. A real pairing decision, not a default.
- **404 page.** The illustrated dog is the one piece of personality on the site that costs nothing and earns its place.

---

## Parking Lot

- **Silent-SSO probe blocked by CSP on every navigation.** `GET https://login.maik-hasler.de/.../auth?...` fails with `net::ERR_BLOCKED_BY_CSP`, then `Refused to frame 'https://einsatzbereit.maik-hasler.de/callback?error=login_required...' because it violates ... "frame-src https://login.maik-hasler.de"` - the callback redirects back to the app origin inside the iframe, which `frame-src` does not allow. One pair of console errors per route change, on every page, logged out. → `bugs` / `security` lens.
- **Staging debris.** Olaf's engagement list carries sign-ups from earlier review runs ("Testeintrag fuer Review - bitte ignorieren", "UX-Review Testeintrag - wird wieder zurueckgezogen"), and Vera has notifications from cancelled test engagements. → `.github/workflows/reset-staging.yml`, at the repo owner's discretion.
- **Engagement re-application rate limit.** `POST .../engagements` → 409 "This engagement has been withdrawn and re-applied for too many times." The policy itself is a backend decision; only its presentation is covered here (F21).
- **`/opportunities` reports "9 Einsätze geladen, weitere verfügbar." rather than a total match count.** Users cannot tell whether a filter narrowed anything. This is a deliberate, documented trade-off (`OpportunityResultsList.tsx:59-67`: "N found" would overclaim a total the user has not seen), so it is not filed as a finding - but if the backend ever returns a total count, this is where it should land.
- **Fixed-height dashboard tiles are configurable** via "Dashboard anpassen"; F12 assumes the shipped default layout, which is what every new organizer sees.

---

## Prioritized Next Steps

### Quick wins - low effort, high impact

1. **F24 - raise functional icons to `brand-600`/`gray-500`.** A palette-token change that clears a WCAG 2.2 AA criterion the automated suite structurally cannot see. Aufwand S.
2. **F1(a) - filter city suggestions whose label does not contain the query.** One predicate in `useCitySuggestions.ts`; removes the worst symptom (Köln for "Lei") of the most damaging finding in this review. Aufwand S.
3. **F16 - route the detail page's fetch failure through the existing `onlineStatus` check.** The correct offline state already exists one route away. Aufwand S.
4. **F20 - per-section titles and h1s in the admin console.** `usePageTitle` is already used correctly in the org console. Aufwand S.
5. **F3 - one separator in the slot picker label.** Aufwand S.
6. **F27 - `aria-describedby` on the two Keycloak login inputs.** One attribute, closes a 3.3.1 gap on a surface nothing else in CI checks. Aufwand S.
7. **F30 - `motion-reduce:transform-none` on the card image.** Closes the single gap in an otherwise complete motion system. Aufwand S.
8. **F7, F9, F31 - copy fixes:** `mailto:` links on `/help` and `/imprint`, drop the repeated sentence in the offline body, drop the stutter in the English bell label. Aufwand S each.

### Larger undertakings

1. **F12 - rework the organizer dashboard.** Content-sized tiles, an Agenda or Monat calendar default, a labelled Einstellungen tile, and "Dashboard anpassen" promoted out of the basement. This is the screen that decides whether an organizer stays, and it is currently the weakest surface in the product. Load `.claude/skills/frontend-design/` before touching it. Aufwand L.
2. **F13 - settle one container width per page** and fix `/my-signups` to the browse list's grid rhythm. Aufwand M.
3. **F2 - `formatDateTimeRange` in `lib/format.ts`,** applied to all six call sites. Aufwand M.
4. **F4 - one vocabulary for the sign-up flow,** carried through CTA, dialog title, confirm button and state label, with `Einsatz` fixed as the single product noun in UI copy. Aufwand M.
5. **F28 - `lang` on unresolved-translation content.** `pickLocalizedText` already knows which language it fell back to; returning that is the whole change. Aufwand M.
6. **F19 - move `/administration/*` onto the org console's compact chrome.** Aufwand M.
7. **F25 - collapse the two opportunity-card designs into one component with a compact variant.** Aufwand M.
8. **F32 - localised PWA manifest.** Aufwand M.
