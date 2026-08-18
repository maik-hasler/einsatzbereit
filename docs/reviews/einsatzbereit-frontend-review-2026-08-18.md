# Frontend & UX Review - einsatzbereit - 2026-08-18

Reviewed: https://einsatzbereit.maik-hasler.de - Commit (staging baseline): `6f3ddda`

## Executive Summary

Einsatzbereit does not look or feel like a template. The visual direction is a deliberate one - a deep forest-green brand field, a heavy condensed display face against a humanist body face, organic blob shapes and a wave divider that repeats consistently across every hero - and it holds together across public, volunteer, organizer and admin surfaces. The engineering floor underneath it is genuinely high: measured contrast on the public pages produced zero WCAG AA failures, the sign-up dialog is a textbook modal (`aria-modal`, labelled, focus-trapped, Escape closes, focus returns to trigger), the create-opportunity wizard wires `role="alert"` errors to fields via `aria-describedby`, search state round-trips through the URL and survives the back button, the language switcher preserves route and query while swapping `lang`, `<title>` and even the web app manifest, and the offline state is presented as a state rather than an error. Most of what follows is polish on top of a solid base, not rescue work.

The concentration of real problems is in three places. First, **feedback**: the single most important action in the product - signing up for a slot - completes with no confirmation of any kind. No toast, no success text, and the only status message written to a live region during the whole flow is "Wird geladen...". Screen reader users are told nothing happened; sighted users get a silent card swap in the far corner of the page. The same silence follows "Als Entwurf speichern". Second, **desktop composition**: the organizer dashboard and "Meine Anmeldungen" both allocate space by grid rule rather than by content, producing widgets that are mostly empty air and sign-up cards squeezed into ~256px tracks that wrap a same-day date across three lines and truncate the volunteer's own message. Third, **one incorrect promise**: the withdraw confirmation tells users the action "cannot be undone" when the backend explicitly supports reactivation and even ships a "withdrawn and re-applied too many times" error.

The highest-value fixes are small. Announcing success, correcting one sentence of withdraw copy, and letting two grids size to their content would address the three most user-visible problems in the product for well under a day of work.

## Scope & Method

**Tool.** Playwright driving the pre-installed Chromium build (the `/live-verify` recipe: proxy-aware launch args, TLS 1.2 cap). No Playwright MCP browser tooling was available in this session. **Only one browser engine was exercised** - all rendering, focus and layout observations below are Chromium-only, and WebKit/Gecko differences were not checked.

**Viewports.** 375px, 768px, 1440px. No horizontal overflow was found at any width on any page tested; the decorative blobs that extend past the viewport are `pointer-events-none` and correctly clipped.

**Languages.** German (default) and English via the header switcher, checked on the landing page, opportunity search, organizations, help, contact and 404.

**Personas.** Anonymous, Vera (`user`), Olaf (`user`, `organisator`), Admin (`admin`).

**Pages covered.** Public: landing, opportunity search, organizations, help, contact, imprint, privacy, terms, 404. Keycloak: login, login error state, forgot-password entry. Vera: search with filters and empty state, opportunity detail, slot dialog, sign-up, withdraw, notifications, profile and badges, my sign-ups, account settings. Olaf: dashboard, opportunities list with drafts, engagements, members, org settings, org switcher, create-opportunity wizard step 1 and its validation. Admin: organizations, users, reports, audit log. Cross-cutting: PWA manifest, service worker, offline reload, `prefers-reduced-motion`, keyboard tab order, focus-ring contrast.

**Methods beyond clicking.** A canvas-normalised contrast pass (handles the `oklch()` colours Tailwind 4 emits, which naive parsers get wrong) over every text node on the public pages; tab-order walks recording the accessible name, position and focus-ring colour of each stop; a `MutationObserver` recording every live-region write and fixed-position node insertion during sign-up; pixel-histogram comparison of focused vs unfocused controls.

**Test data.** All data created during the review was removed: two sign-ups on "Erste-Hilfe-Kurs" were withdrawn (slot capacity verified back at 20/20) and one accidental empty draft was deleted. No other account's data was touched.

**Explicitly out of scope** (owned by the `lens` / `einsatzbereit-review` skills): backend logic, data model, infrastructure, security, CI, code hygiene, dead code, diff review.

**Features named in the brief that do not exist in the product.** Verified against the codebase, not just the UI: there is no map view for browsing (`SingleMarkerMap` is the detail page's single-marker map only), no mini-calendar on the volunteer search (`CalendarWidget` is an organizer dashboard widget), and no saved searches or search alerts (`grep -rn "reactivat\|savedSearch" src/**/*.tsx` finds no components). These are absent features, not defects, and are not reported as findings.

## Findings

### Content

---

### F1 - Withdraw dialog tells users the action cannot be undone, but it can

**Kategorie:** Content
**Schweregrad:** Hoch
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #5 Fehlervermeidung, #3 Nutzerkontrolle und -freiheit; the project's own backend contract)
**Ort:** Opportunity detail + `/my-signups`, withdraw confirmation - Persona: Vera - Viewport: all - Sprache: DE/EN

Beleg: `docs/reviews/assets/vera-withdraw-confirm.jpg`. Code: `frontend/src/locales/de.json:961` - "Möchtest du deine Anmeldung wirklich zurückziehen? Diese Aktion kann nicht rückgängig gemacht werden." and `en.json:961` - "This cannot be undone."
Counter-evidence in the same bundle: `de.json:1546` "Nur zurückgezogene oder abgesagte Anmeldungen können reaktiviert werden." and `de.json:1567` "Diese Anmeldung wurde bereits zu oft zurückgezogen und erneut angemeldet." I verified the behaviour live: after withdrawing, the detail page's rail returns to "Zeitslot auswählen" and the slot can be taken again (capacity 20 -> 19 -> 20 across the round trip).

Auswirkung: This is a warning that misstates the stakes of a reversible action, at exactly the moment a volunteer is deciding. It will make people keep sign-ups they cannot honour rather than free the slot for someone else - the opposite of what the platform wants. It also erodes trust in every other "cannot be undone" warning in the product, several of which (account deletion, opportunity deletion) are genuinely irreversible.

Verbesserungsvorschlag: Replace the sentence with what is actually true and useful, e.g. "Der Platz wird wieder freigegeben. Du kannst dich später erneut anmelden, solange noch Plätze frei sind." Keep "cannot be undone" reserved for the deletions where it is accurate. - Aufwand: S

---

### F2 - The one notification that matters most does not say which opportunity it is about

**Kategorie:** Content
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #1 Sichtbarkeit des Systemstatus; projekteigene Konvention - every sibling string in the same block interpolates `{{title}}`)
**Ort:** Notification panel - Persona: Vera - Viewport: all - Sprache: DE/EN

Beleg: `docs/reviews/assets/notif-panel-detail.jpg` - "Ein Einsatz, für den du dich angemeldet hast, wurde entfernt". Code: `frontend/src/locales/de.json:896` / `en.json:896`:

```
"OpportunityUpdated":     "{{title}} wurde aktualisiert",
"OpportunityDeleted":     "Ein Einsatz, für den du dich angemeldet hast, wurde entfernt",   <- no {{title}}
"OpportunityUnpublished": "{{title}} wurde zurückgezogen und deine Anmeldung wurde abgesagt",
```

Auswirkung: A volunteer signed up for several things learns that one of them is gone but not which one, and the notification is not a link, so there is no way to find out from the panel. This is the most consequential of the three messages and it carries the least information. A volunteer could show up to a cancelled shift.

Verbesserungsvorschlag: Interpolate the title like its neighbours: "{{title}} wurde entfernt". If the title is genuinely unavailable after a hard delete, capture it into the notification payload at creation time (backend-side change) - do not leave the string subject-less. - Aufwand: S (frontend string) / M (if the title must be persisted)

Vermutlich Backend: only if the deleted opportunity's title is not retained on the notification record.

---

### F3 - "Wann" never answers when, and "Ablauf" answers with a tautology

**Kategorie:** Content
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #2 Übereinstimmung mit der realen Welt)
**Ort:** `/volunteer-opportunities/:id` fact strip - Persona: alle - Viewport: alle - Sprache: DE/EN

Beleg: `docs/reviews/assets/vera-detail.jpg`. The three-up fact strip immediately under the title reads **WANN: Einmalig** - **ABLAUF: Zeitslots** - **WO: Karl-Heine-Straße 12, 04177 Leipzig**. The actual date (27.08.2026, 09:00-17:00) appears only in "Verfügbare Zeitslots", below the map, roughly 500px further down. Code: `frontend/src/locales/de.json:236-238` (`factWhen`, `factFormat`, `factWhere`).

Auswirkung: The field a scanning reader trusts to carry the date carries a recurrence type instead. "Wann: Einmalig" is a category, not a time. And "Ablauf: Zeitslots" tells the reader that a page section named "Zeitslots" exists - it consumes a third of the most valuable strip on the page to say nothing. Deciding whether an opportunity fits your week is the entire job of this page, and the strip built for that job does not do it. Note the EN label is better ("How it works") while the DE "Ablauf" is the weaker of the two.

Verbesserungsvorschlag: Put the real answer in "Wann": the next slot's date and time for single-date opportunities, the deadline for expression-of-interest ones, with the recurrence type demoted to a chip. Either give "Ablauf" a value that adds information (slot count, duration, "3 Termine zur Auswahl") or drop the field and let the strip breathe at two columns. - Aufwand: M

---

### F4 - Same-day time ranges repeat the full date on both sides

**Kategorie:** Content
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #8 Ästhetisches und minimalistisches Design)
**Ort:** Opportunity detail (slot list, sign-up rail), slot dialog, `/my-signups` - Persona: Vera/Olaf - Viewport: alle - Sprache: DE/EN

Beleg: `docs/reviews/assets/vera-detail.jpg` ("27.08.2026, 09:00 - 27.08.2026, 17:00"), `docs/reviews/assets/vera-signups.jpg` ("Termin: 20.08.2026, 10:00 - 20.08.2026, 14:00"), `docs/reviews/assets/vera-slot-modal.jpg`, `docs/reviews/assets/vera-after-signup.jpg`. Four locations, one cause: the range is composed as `formatDateTime(start) + " - " + formatDateTime(end)` with no same-day collapse.

Auswirkung: Every shift on this platform is described as "most of them only take a few hours", so nearly every range is same-day and nearly every range repeats itself. It doubles the length of the most-repeated string in the product, and in the narrow tracks of F6 it is what pushes the date onto a third line.

Verbesserungsvorschlag: Add a `formatDateTimeRange(start, end, locale)` helper in `frontend/src/lib/format.ts` that collapses to "27.08.2026, 09:00-17:00" when the dates match and keeps the long form when they differ. `lib/format.test.ts` already has the fixtures pattern to extend. - Aufwand: S

---

### F5 - Four different date formats coexist

**Kategorie:** Content
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #4 Konsistenz und Standards)
**Ort:** produktweit - Persona: alle - Viewport: alle - Sprache: DE

Beleg: `27.08.2026, 09:00` (slot lists), `13.08.2026` (badge unlock, "Angemeldet:"), `13. August 2026` (org settings and dashboard "Erstellt am" - see `docs/reviews/assets/olaf-dash.jpg`), `Vor 5 Tagen veröffentlicht` (detail page relative date).

Auswirkung: Four registers for the same data type make the interface feel assembled rather than designed, and the long form ("13. August 2026") is the odd one out in a product that is otherwise numeric. Relative dates additionally hide the actual date from anyone planning ahead.

Verbesserungsvorschlag: Pick two and document them: numeric `DD.MM.YYYY` (plus `, HH:mm` where a time matters) as the default, relative only for "published X ago" style recency signals - and give relative dates a `title` with the absolute date. Retire the long form. - Aufwand: S

---

### F6 - Slot picker concatenates the end time and the capacity

**Kategorie:** Content
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (projekteigene Konvention - the expanded list separates the two values; only the collapsed trigger does not)
**Ort:** Slot selection dialog - Persona: Vera - Viewport: alle - Sprache: DE/EN

Beleg: `docs/reviews/assets/vera-slot-modal.jpg` - the collapsed combobox reads `27.08.2026, 09:00 - 27.08.2026, 17:00 20 Plätze frei`, so the end time and the capacity run together as "17:00 20".

Auswirkung: The one control that carries the commitment is momentarily unreadable - "17:00 20" reads as a malformed number before it resolves. Combined with F4 the trigger is a 45-character run-on.

Verbesserungsvorschlag: Separate the two values in the collapsed label with a middot, the way the product already does elsewhere ("2 Mitglieder - Erstellt am ..."): `27.08.2026, 09:00-17:00 - 20 Plätze frei`. - Aufwand: S

---

### F7 - "Zeitslot auswählen" then "Zeitslot wählen"

**Kategorie:** Content
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Präferenz
**Ort:** Slot selection dialog - Persona: Vera - Viewport: alle - Sprache: DE

Beleg: `docs/reviews/assets/vera-slot-modal.jpg`. Dialog title "Zeitslot auswählen", field label directly beneath it "Zeitslot wählen".

Auswirkung: Two near-identical phrases stacked 30px apart, in a dialog with exactly one field. The label is doing no work the title has not already done.

Verbesserungsvorschlag: Drop the visible label and keep it as the combobox's accessible name, or make it earn its place ("Welcher Termin passt dir?"). - Aufwand: S

---

### F8 - Leaving an organization sends you to a page that sends you back

**Kategorie:** Content
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #10 Hilfe und Dokumentation)
**Ort:** `/app/:id/dashboard/members` and `/dashboard/settings` - Persona: Olaf - Viewport: alle - Sprache: DE

Beleg: Members page hint (`aria-describedby="leave-organization-hint"`): "Du bist der:die einzige Organisator:in dieser Organisation - lösche stattdessen die Organisation, wenn du sie schließen möchtest." Settings page: "Nur das letzte verbleibende Mitglied kann die Organisation löschen. Entferne zuerst die anderen Mitglieder."

Auswirkung: Page A says "delete the organization instead", page B says "you cannot delete it until you remove the members". Neither states the full path (remove all members, then delete), so the user bounces between two screens to assemble it themselves. The button itself is correctly disabled with a `title` and `cursor: not-allowed` - the mechanics are right, only the guidance dead-ends.

Verbesserungsvorschlag: State the whole path once, on the members page where the user hits the wall: "Entferne zuerst die anderen Mitglieder, danach kannst du die Organisation in den Einstellungen löschen." Link "Einstellungen" directly. - Aufwand: S

---

### Visuelles Design & UI

---

### F9 - Organizer dashboard widgets are mostly empty air, and the calendar opens on the emptiest hours of the week

**Kategorie:** UI
**Schweregrad:** Hoch
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #8 Ästhetisches und minimalistisches Design, #1 Sichtbarkeit des Systemstatus)
**Ort:** `/app/:id/dashboard` - Persona: Olaf - Viewport: 1440px - Sprache: DE

Beleg: `docs/reviews/assets/olaf-dash.jpg`. Measured on the live page: "Bevorstehende Einsätze" holds one 40px item and then ~450px of empty white; "Freiwillige" holds the number 1 and then ~250px of empty white; "Organisation" is a full-width card holding one line. The calendar - the largest and highest widget - renders the week 17.-23.08.2026 scrolled to **00:00-15:00** and is completely empty, because the org's only upcoming opportunity is on 27.08. The right column holds two small cards and is then blank for ~2200px. "Dashboard anpassen" sits at the very bottom, below all of it.

Auswirkung: This is the organizer's home screen and the first thing it communicates is that nothing is happening. The widgets are sized by grid rule rather than by content, so an organization with real activity and one with none produce nearly the same amount of white space. Worst is the calendar defaulting to midnight: fifteen rows of night-time are the least informative hours it could have chosen, and the one event that exists is in a week the user has to page forward to find. The control that would fix the layout is hidden below roughly two screens of emptiness.

Verbesserungsvorschlag: Three independent fixes, in impact order. (1) Scroll the calendar to the first hour with content, falling back to ~07:00, and default the range to the week containing the next opportunity rather than today. (2) Let widget cards size to their content (`h-fit` / `items-start` on the grid) instead of stretching to the row. (3) Move "Dashboard anpassen" next to the existing "Bearbeiten" control in the header. - Aufwand: M

---

### F10 - "Meine Anmeldungen" squeezes cards into a three-column grid that its container cannot afford

**Kategorie:** UI
**Schweregrad:** Hoch
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (WCAG 2.2 AA 1.4.10 Reflow is not violated, but content is truncated at the widest viewport - Nielsen-Norman-Heuristik #6 Wiedererkennen statt Erinnern)
**Ort:** `/my-signups` - Persona: Vera - Viewport: 1440px (the `xl` breakpoint, >=1280px) - Sprache: DE

Beleg: `docs/reviews/assets/vera-signups.jpg`. Code: `frontend/src/pages/MyEngagementsPage/index.tsx:25` sets the page container to `max-w-5xl` with a `[11rem_minmax(0,1fr)]` split, leaving the content track at ~800px; `ActivitySection.tsx:271` (and `:384`, `:424`) then applies `xl:grid-cols-3` to that 800px track, giving each card ~256px. Measured consequences at 1440px: the title wraps to two lines, the organization name wraps to two lines, "Termin: 20.08.2026, 10:00 - 20.08.2026, 14:00" wraps to **three** lines, and the volunteer's own message is truncated to `"Ich würde beim nächsten..."`.

Auswirkung: On the widest screen the product supports, the page is at its least readable - a card row using 527px of an 800px track while ~480px sits empty to its right, with content clipped inside the cards it did fit. The truncated message is the sharpest edge: it is text the volunteer wrote themselves, it cannot be expanded anywhere in the volunteer UI, and the organizer sees it in full on their engagements screen. The person who wrote it is the only one who cannot read it back.

Verbesserungsvorschlag: Drop the `xl:grid-cols-3` step so the grid tops out at two columns inside this container (or raise the container to `max-w-6xl` if three columns are wanted). Independently, make the message expandable - a "Mehr anzeigen" toggle or a `title` at minimum. F4 removes the three-line date as a side effect. - Aufwand: S

---

### F11 - The marketing footer CTA follows users into the admin console

**Kategorie:** UI
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #8 Ästhetisches und minimalistisches Design)
**Ort:** every page rendered by `AppLayout` - Persona: alle, auch eingeloggt - Viewport: alle - Sprache: DE/EN

Beleg: The "Bereit, wenn du es bist. / Einsatzbereit bringt engagierte Helfer:innen mit regionalen Einsätzen zusammen. / [Einsätze finden]" block appears on: `/administration/users` (`docs/reviews/assets/admin-users.jpg`), `/profile` (`docs/reviews/assets/vera-profile.jpg`), `/my-signups` (`docs/reviews/assets/vera-signups.jpg`), `/opportunities` itself (`docs/reviews/assets/pub-desktop-de-opportunities.jpg`), and the 404 page. The organizer dashboard, notably, uses a compact single-line footer instead and is the right model.

Auswirkung: An acquisition pitch aimed at anonymous visitors is shown to a platform administrator mid-way through suspending user accounts, and to a volunteer reviewing sign-ups they already made. On `/opportunities` the CTA button links to the page it is already on. It is the single largest element on several authenticated screens and it is addressed to someone who is not there.

Verbesserungsvorschlag: The dashboard's compact footer already exists - use it for all authenticated routes and keep the marketing CTA on public marketing pages only. At minimum, suppress the CTA when its target is the current route. - Aufwand: S

---

### F12 - The opportunity card has two different anatomies

**Kategorie:** UI
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #4 Konsistenz und Standards)
**Ort:** `/opportunities` and landing vs. "Weitere Einsätze dieser Organisation" on the detail page - Persona: alle - Viewport: alle - Sprache: DE/EN

Beleg: Search/landing card (`docs/reviews/assets/pub-desktop-de-opportunities.jpg`): category chip + frequency chip on the left, capacity badge right-aligned, then title, then the **date or deadline**, then description, then org and location. Related-opportunities card (`docs/reviews/assets/vera-detail.jpg`, bottom): category chip + sign-up-type badge on row one, frequency with a calendar icon on row two, then title, description, location - **and no date or deadline at all**.

Auswirkung: The same object gets two visual grammars in one session, and the second one drops the field that decides whether a volunteer can attend. A user browsing an organization's other opportunities has to open each one to learn when it is.

Verbesserungsvorschlag: Extract one card component with a `compact` variant that changes density but not field order or field set, and make sure the date/deadline row survives in both. - Aufwand: M

---

### F13 - Section eyebrows sometimes just repeat the heading below them

**Kategorie:** Visuelles Design
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Präferenz
**Ort:** `/organizations`, Keycloak login, `/administration`, `/profile` - Persona: alle - Viewport: alle - Sprache: DE/EN

Beleg: `/organizations` renders eyebrow "ORGANISATIONEN" above H1 "Organisationen". The Keycloak card renders "ANMELDUNG" above "Anmelden" (`docs/reviews/assets/kc-desktop-login-error.jpg`). Elsewhere the device works well and carries real information: "EHRENAMT" above "Einsätze finden", "KONTO" above "Mein Profil", "PLATTFORM" above "Administration", the organization name above an opportunity title.

Auswirkung: A structural device that classifies content everywhere else degenerates into an echo on a few screens. The eyebrow is a good idea in this design system - where it repeats the heading it costs a line of vertical rhythm and teaches the reader to stop looking at it.

Verbesserungsvorschlag: Where the eyebrow would restate the heading, either drop it or use it for the category the heading does not carry ("VERZEICHNIS" above "Organisationen", "KONTO" above "Anmelden"). - Aufwand: S

---

### F14 - The map's attribution links are the only blue in the product

**Kategorie:** Visuelles Design
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Präferenz (contrast itself passes: measured 7.05:1)
**Ort:** `/volunteer-opportunities/:id` map attribution - Persona: alle - Viewport: alle - Sprache: DE/EN

Beleg: `docs/reviews/assets/vera-detail.jpg`, bottom-right of the map. Measured computed colour of `.leaflet-control-attribution a`: `rgb(0, 96, 134)` - Leaflet's default teal-blue, untouched. Everything else in the product uses the brand green (`#2d8a5e` / `rgb(34,105,71)`), which the review confirmed is applied consistently including the manifest `theme_color` and the `<meta name="theme-color">`.

Auswirkung: Two small links in a otherwise thoroughly green interface read as imported furniture. This is the only place where the theme colour does not hold.

Verbesserungsvorschlag: One rule in `SingleMarkerMap.css`: `.leaflet-control-attribution a { color: var(--color-brand-700); }`. - Aufwand: S

---

### F15 - The profile stat pair does not balance

**Kategorie:** UI
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Präferenz
**Ort:** `/profile` header card - Persona: Vera - Viewport: 1440px - Sprache: DE

Beleg: `docs/reviews/assets/vera-profile.jpg`. The left card is a clean pair ("2" / "Bestätigte Einsätze"). The right card carries "1" / "Woche in Serie - Wochenheld" - a value, a unit and a badge name compressed into one label - and a second chip, "2 Tage in Folge angemeldet - Anmeldeserie", hangs below the card outline rather than inside it, so the two cards no longer share a baseline.

Auswirkung: A small composition that reads as unfinished in the most personal part of the product. The label overloading also makes the streak stat harder to parse than the plain count next to it.

Verbesserungsvorschlag: Make the streaks a third stat card of the same shape (value / unit) and move the badge names off the labels - the badge grid below already names them. - Aufwand: S

---

### UX

---

### F16 - Signing up produces no confirmation of any kind

**Kategorie:** UX
**Schweregrad:** Hoch
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (WCAG 2.2 AA 4.1.3 Status Messages; Nielsen-Norman-Heuristik #1 Sichtbarkeit des Systemstatus)
**Ort:** `/volunteer-opportunities/:id` sign-up, withdraw, and the create-opportunity wizard's "Als Entwurf speichern" - Persona: Vera, Olaf - Viewport: alle - Sprache: DE/EN

Beleg: `docs/reviews/assets/vera-after-signup.jpg` (the entire visible change after a successful sign-up). Instrumented with a `MutationObserver` watching every live-region write and every fixed-position node inserted from the moment "Mitmachen" was clicked. Complete log for a successful sign-up:

```
  77ms  ADDED: [the slot dialog]                    pos=fixed
1282ms  ADDED: "Wird geladen..."                    role=status
```

There is no third line. No toast is created, nothing is written to a live region on success, and the same holds for withdrawal. Sampling the live regions at +1.6s and +6s returned `SPAN[polite] ""` in every case. Saving an empty draft in the wizard likewise closes the dialog with no confirmation.

Auswirkung: For a screen reader user, the flow ends on "Wird geladen..." and then silence - there is no way to know whether the sign-up succeeded without navigating away to check. For a sighted user the only feedback is a small card in the top-right rail quietly swapping from "Zeitslot auswählen" to "Deine Anmeldung / Ausstehend", roughly 700px from the centred dialog they just clicked in, plus a capacity number changing from 20 to 19. This is the product's core conversion event and it is the least acknowledged action in the interface. The live-region infrastructure is already in place and already used correctly for the loading state, which makes this a wiring gap rather than an architectural one.

Verbesserungsvorschlag: Write a success message into the existing polite live region and surface it visually - a toast, or a confirmation state in the rail with the same words. Keep the vocabulary consistent with the trigger ("Angemeldet" after "Mitmachen"). Apply the same to withdrawal and to draft save. - Aufwand: S

---

### F17 - Locked badges never show how close you are

**Kategorie:** UX
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #1 Sichtbarkeit des Systemstatus)
**Ort:** `/profile` badge grid - Persona: Vera - Viewport: alle - Sprache: DE/EN

Beleg: `docs/reviews/assets/vera-profile.jpg`. The header states "2 Bestätigte Einsätze". Directly below, "Engagiert" reads "Verdient nach 5 bestätigten Einsätzen." with no indication that the user is at 2 of 5. Same for "100 Einsätze", "Anmeldeserie" (7 days) and "Wochenheld" (4 weeks) - even though the streak values (1 week, 2 days) are displayed in the header card immediately above, disconnected from the badges they belong to. "Geheimes Abzeichen" shows only a "?" with no text at all.

Auswirkung: A progression system that never shows progress is just a list of things you do not have. The data needed to show "2 / 5" is already on screen; it is simply in the wrong component. The secret badge with zero hint gives a curious user nothing to act on.

Verbesserungsvorschlag: Add a progress line or bar to each locked badge ("2 von 5 bestätigten Einsätzen") by moving the header card's counters into the badge cards. Give the secret badge a teaser rather than a bare "?". - Aufwand: M

---

### F18 - The slot list looks clickable, is not, and the button that is sits far away

**Kategorie:** UX
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #4 Konsistenz und Standards, #7 Flexibilität und Effizienz)
**Ort:** `/volunteer-opportunities/:id` - Persona: Vera - Viewport: 1440px - Sprache: DE/EN

Beleg: `docs/reviews/assets/vera-detail.jpg`. "Verfügbare Zeitslots" renders each slot as a bordered, padded, full-width row carrying the date and "20 Plätze frei" - the visual signature of a selectable list item - but the rows are inert. The only way forward is "Zeitslot auswählen" in the right rail, ~700px away horizontally and ~500px above, which opens a dialog whose only content is a dropdown listing the same rows again. For a single-slot opportunity this is three interactions to choose the only option there is.

Auswirkung: The affordance the page offers and the affordance it honours are different objects, and the correct one is the furthest thing on screen from where the user is reading. The mobile layout does not have this problem - it puts a full-width CTA directly under the fact strip and reads well (`docs/reviews/assets/mob-vera-detail.jpg`).

Verbesserungsvorschlag: Make the slot rows the primary control - clicking a row selects it and opens the confirmation directly, with the rail button as the secondary entry point. When only one slot exists, skip the picker and let the rail button confirm that slot. - Aufwand: M

---

### F19 - "Ausstehend" is never explained

**Kategorie:** UX
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #1 Sichtbarkeit des Systemstatus, #10 Hilfe und Dokumentation)
**Ort:** `/volunteer-opportunities/:id` rail, `/my-signups` - Persona: Vera - Viewport: alle - Sprache: DE/EN

Beleg: `docs/reviews/assets/vera-after-signup.jpg` - after signing up the rail shows an amber "Ausstehend" chip and nothing else. Vera's older sign-ups show green "Bestätigt". No text anywhere on either screen explains what pending means, who resolves it, how long it takes, or whether the volunteer should show up if it is still pending on the day.

Auswirkung: The volunteer has committed and immediately lands in a state they cannot interpret. The difference between "Ausstehend" and "Bestätigt" determines whether they should turn up to a shift, and the product never says so. Given F16 leaves the sign-up silent, "Ausstehend" is also the only signal that anything happened at all.

Verbesserungsvorschlag: One sentence next to the chip: "Die Organisation prüft deine Anmeldung. Du bekommst eine Nachricht, sobald sie bestätigt ist." Reuse it in `/my-signups` and, if useful, as the chip's `title`. - Aufwand: S

---

### F20 - An entirely empty draft can be saved, and there is no way to tell two of them apart

**Kategorie:** UX
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #5 Fehlervermeidung)
**Ort:** Create-opportunity wizard, step 1 - Persona: Olaf - Viewport: alle - Sprache: DE

Beleg: `docs/reviews/assets/olaf-draft-menu.jpg`. Opening the wizard and pressing "Als Entwurf speichern" without typing anything creates a record listed as **"Unbenannter Entwurf"** with "0 Anmeldungen (noch keine Zeitslots)" and a "Veröffentlichen" button. I did this accidentally while testing and it succeeded silently; the draft was then deleted. Note the contrast with "Weiter", which correctly blocks on the same empty form (see F28 - validation there is well built).

Auswirkung: Drafts are meant to be incomplete, so accepting a partial one is right - but accepting a completely empty one produces a row with no identifying information whatsoever. Two of them are indistinguishable in the list, and the most prominent action offered on the empty draft is "Veröffentlichen". Combined with F16 the save is also unacknowledged, so it is easy not to realise a record was created.

Verbesserungsvorschlag: Require at least a title before "Als Entwurf speichern" enables (the title field is already the natural identifier), or auto-name the draft from its creation timestamp so the list stays navigable. - Aufwand: S

---

### F21 - The result counter never reveals the total

**Kategorie:** UX
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #1 Sichtbarkeit des Systemstatus)
**Ort:** `/opportunities`, `/organizations` - Persona: alle - Viewport: alle - Sprache: DE/EN

Beleg: `docs/reviews/assets/filter-category-panel.jpg` (filtered: "6 Einsätze gefunden.") and `docs/reviews/assets/opps-empty-state.jpg` (no results: "0 Einsätze gefunden."). Unfiltered, the counter instead reads "9 Einsätze geladen, weitere verfügbar." (`de.json:135` `resultCountPartial` vs `de.json:132` `resultCount`). The distinction is deliberate and honest - the partial variant does not claim to be a total - but the total is never available anywhere.

Auswirkung: A searcher cannot tell whether their filters narrowed 200 results to 6 or 8 to 6, so they cannot judge whether to loosen them. "Weitere verfügbar" also gives no sense of scale, which makes "Mehr laden" a blind action.

Verbesserungsvorschlag: Surface the total the paged endpoint already knows: "9 von 34 Einsätzen geladen." - one string, one value, and the partial/complete distinction disappears. - Aufwand: S (frontend) if the total is in the response; M if it must be added.

Vermutlich Backend: only if the list endpoint does not already return a total count.

---

### Barrierefreiheit

These complement the existing axe-core and `jsx-a11y` coverage rather than repeating it. The automated tooling is clearly working - a full contrast pass over the public pages produced zero AA failures, and every icon-only control I probed had a sensible accessible name, including per-item ones like "Löschen: Deine Anmeldung für Erste-Hilfe-Kurs wurde abgesagt". What follows is what those tools structurally cannot see.

---

### F22 - The detail page's primary action is the eighth tab stop, after the organization's phone number

**Kategorie:** Barrierefreiheit
**Schweregrad:** Hoch
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (WCAG 2.2 Level A, 2.4.3 Focus Order; 1.3.2 Meaningful Sequence)
**Ort:** `/volunteer-opportunities/:id` - Persona: alle - Viewport: >=1024px (`lg`) - Sprache: DE/EN

Beleg: recorded tab walk from the top of the page:

```
 1. BUTTON "Einsatz melden"            y=413
 2. DIV    ""                          y=732    <- the map pane, no accessible name (F23)
 3. A      "Leaflet"                   y=879
 4. A      "OpenStreetMap"             y=879
 5. A      "info@nachbarschaftshilfe-lindenau.example"
 6. A      "+49 341 1234560"
 7. A      "https://www.nachbarschaftshilfe-lindenau.example"
 8. BUTTON "Zeitslot auswählen"        y=434    <- visually the topmost control on the page
 9. A      "Blutspendetermin begleiten"
```

Code: `frontend/src/pages/VolunteerOpportunityDetailPage.tsx:927` places `<aside className="hidden lg:sticky lg:top-24 lg:block">` last in the layout, with a `lg:hidden` mobile duplicate earlier at `:748`.

Auswirkung: A keyboard or screen reader user tabs through a report button, an unlabelled map, two attribution links and three organization contact details before reaching the button the page exists for - which is sitting at the top right of their screen the whole time. Visual order and focus order disagree by seven stops. The comment at `:566` shows the sticky rail was a deliberate fix for a real problem (the CTA used to sit below the fold with ~500px of empty space beside it, issue #1755), and visually it worked; the DOM-order consequence is the part that was not carried over. On mobile, where the rail is replaced by the earlier duplicate, the order is correct - so this affects desktop only.

Verbesserungsvorschlag: Render the desktop rail early in the DOM and position it with grid placement (`lg:col-start-2 lg:row-start-1`) rather than source order, so the CTA is reached right after the fact strip in both reading and focus order. That keeps the visual result identical. - Aufwand: S

---

### F23 - The map is a focusable element with no name and no role

**Kategorie:** Barrierefreiheit
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (WCAG 2.2 Level A, 4.1.2 Name, Role, Value)
**Ort:** `/volunteer-opportunities/:id` map - Persona: alle - Viewport: alle - Sprache: DE/EN

Beleg: tab stop 2 above is `DIV ""`. Probing `.leaflet-container` returns `tabindex: null, role: null, aria-label: null`, yet the pane receives focus (Leaflet makes it keyboard-operable for panning). The only focusable children are the two attribution links; there are no zoom controls.

Auswirkung: A screen reader announces a focusable element with no name and no role - the user has no way to know they are on a map, what it shows, or that arrow keys pan it. The location is available as text directly above ("Karl-Heine-Straße 12, 04177 Leipzig"), so the map is decorative-plus, but it still takes a tab stop and says nothing.

Verbesserungsvorschlag: Give the container `role="img"` with an `aria-label` naming the location ("Karte: Karl-Heine-Straße 12, 04177 Leipzig") if panning is not needed, or `role="application"` with a label and a visible hint if it is. Adding zoom controls would also help sighted mobile users, who currently have pinch-zoom only. - Aufwand: S

---

### F24 - The language switcher cannot be activated by its visible name

**Kategorie:** Barrierefreiheit
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (WCAG 2.2 Level A, 2.5.3 Label in Name)
**Ort:** header, produktweit - Persona: alle - Viewport: alle - Sprache: DE/EN

Beleg: the trigger's visible text is `DE`; its markup is

```html
<button aria-expanded="false" aria-label="Sprache wechseln, aktuell Deutsch"
        data-testid="language-selector-trigger">
  <span class="font-semibold tracking-wide">DE</span>...
```

The `aria-label` replaces rather than extends the visible label, and does not contain the string "DE".

Auswirkung: Speech-input users say what they see. "Klick DE" will not match this control, because its accessible name contains no "DE". This is exactly the mismatch 2.5.3 exists to prevent, and it is a rule axe-core only reports as "needs review" (`label-content-name-mismatch` is flagged experimental), so it will not fail the existing pipeline.

Verbesserungsvorschlag: Include the visible text at the start of the accessible name: `aria-label="DE - Sprache wechseln, aktuell Deutsch"`. Add `aria-haspopup="menu"` while there (see F27). - Aufwand: S

---

### F25 - Keycloak: the login error is not linked to the fields it describes

**Kategorie:** Barrierefreiheit
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (WCAG 2.2 Level A, 1.3.1 Info and Relationships, 3.3.1 Error Identification)
**Ort:** Keycloak login (FTL template, not React) - Persona: alle - Viewport: alle - Sprache: DE

Beleg: `docs/reviews/assets/kc-desktop-login-error.jpg`. After a failed login, probing the DOM returns:

```
aria-invalid:      username=true, password=true
live region:       SPAN "Benutzername oder Passwort stimmt nicht."
aria-describedby:  []            <- nothing
focus after submit: username
```

Both fields are marked invalid, the message is announced once via a live region, but no `aria-describedby` connects either field to the message. Visually the message sits under the username field although it concerns the credential pair.

Auswirkung: A screen reader user who hears the error once and then navigates back through the form finds two fields announced as "invalid" with no associated explanation. This is the entry point to the entire product, and it is the one place where the otherwise-good error pattern (the React wizard does this correctly - see F26) is not applied. **This finding is in the FTL theme under `keycloak/`, not in the React app.**

Verbesserungsvorschlag: Give the error span an `id` and reference it from both inputs' `aria-describedby`, mirroring what `CreateVolunteerOpportunityModal` already does. - Aufwand: S

---

### F26 - Keycloak: the password recovery link is not visually distinguishable from plain text

**Kategorie:** Barrierefreiheit
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (WCAG 2.2 Level A, 1.4.1 Use of Color - here not even colour distinguishes it)
**Ort:** Keycloak login (FTL template) - Persona: alle - Viewport: alle - Sprache: DE

Beleg: `docs/reviews/assets/kc-desktop-login-error.jpg`. Measured computed styles on the same card:

| Element | Colour | Underline |
|---|---|---|
| "Passwort vergessen?" (link) | `rgb(107, 114, 128)` | none |
| "Angemeldet bleiben" (label, not a link) | grey body text | none |
| "Registrieren" (link) | `rgb(34, 105, 71)` brand green | none |

Auswirkung: The account-recovery link renders in the same grey as the checkbox label sitting immediately to its left, with no underline and no icon - nothing marks it as interactive. Meanwhile the less critical "Registrieren" link on the same card does get brand colour. A user who cannot get in has to guess that the grey text is clickable. Contrast itself is fine (4.83:1); the problem is affordance, and the inconsistency between two links in one card.

Verbesserungsvorschlag: Style "Passwort vergessen?" like "Registrieren" - brand green, or grey with an underline. Pick one link treatment for the card and apply it to both. - Aufwand: S

---

### F27 - Popover triggers omit `aria-haspopup`, and menu items are plain buttons

**Kategorie:** Barrierefreiheit
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (WAI-ARIA Authoring Practices, Menu Button pattern)
**Ort:** language switcher, notification bell, org switcher, opportunity row "..." menu - Persona: alle - Viewport: alle - Sprache: DE/EN

Beleg: one pattern, four locations. All four triggers correctly set `aria-expanded` and (where applicable) `aria-controls`, and all four omit `aria-haspopup`. The opportunity row menu's items are plain `<button>` elements with `role: null` inside a container with `role: null` (`docs/reviews/assets/olaf-draft-menu.jpg`):

```
[{"tag":"BUTTON","role":null,"text":"Bearbeiten","parentRole":null},
 {"tag":"BUTTON","role":null,"text":"Löschen","parentRole":null}]
```

Auswirkung: Screen reader users are not told in advance that activating the control opens a menu, and inside the row menu they get two loose buttons rather than a menu with a known item count and arrow-key navigation. This is a mild degradation, not a blocker - the controls are all reachable and named.

Verbesserungsvorschlag: Add `aria-haspopup="menu"` (or `"listbox"` for the language and org switchers) to the four triggers, and give the row menu `role="menu"` / `role="menuitem"` with arrow-key handling. - Aufwand: M

---

### F28 - Required fields are marked with an asterisk but not programmatically required

**Kategorie:** Barrierefreiheit
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (WAI-ARIA Authoring Practices; WCAG 3.3.2 is met by the visible marker)
**Ort:** Create-opportunity wizard - Persona: Olaf - Viewport: alle - Sprache: DE/EN

Beleg: step 1 declares "* Pflichtfeld" and labels the fields "Titel*" / "Beschreibung*", but probing returns `required: false` on both inputs and no `aria-required`. Pressing "Weiter" then behaves correctly and well: `role="alert"` errors appear, `aria-invalid="true"` is set, and `aria-describedby` links `opportunity-title -> opportunity-title-error`. So the requirement is communicated on failure but not in advance (`docs/reviews/assets/olaf-create-validation.jpg`).

Auswirkung: A screen reader user learns a field was required only after submitting and failing. The recovery experience afterwards is genuinely good, which makes the missing up-front signal the only gap.

Verbesserungsvorschlag: Add `aria-required="true"` to the fields carrying an asterisk. Separately, move focus to the first invalid field after a blocked "Weiter" - focus currently stays on the button. - Aufwand: S

---

### F29 - Admin sub-pages all share one document title

**Kategorie:** Barrierefreiheit
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (WCAG 2.2 Level A, 2.4.2 Page Titled)
**Ort:** `/administration/*` - Persona: Admin - Viewport: alle - Sprache: DE/EN

Beleg: `/administration/organizations`, `/administration/users`, `/administration/reports` and `/administration/audit-log` all return `<title>Administration | Einsatzbereit</title>`. Every other area of the product titles correctly - "Mitglieder - Lindenauer Nachbarschaftshilfe e.V. | Einsatzbereit", "Mein Profil | Einsatzbereit", "Erste-Hilfe-Kurs | Einsatzbereit".

Auswirkung: Four distinct pages are indistinguishable in browser tabs, history and screen reader page announcements. The convention that makes the rest of the app navigable simply was not applied to this route group.

Verbesserungsvorschlag: Add the section to the title as the org dashboard already does: "Nutzer:innen - Administration | Einsatzbereit". - Aufwand: S

---

### F30 - `prefers-reduced-motion` stops animations but not transform transitions

**Kategorie:** Barrierefreiheit
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (MDN / WebKit guidance on reduced motion; WCAG 2.3.3 is Level AAA and not claimed)
**Ort:** produktweit - Persona: alle - Viewport: alle - Sprache: DE/EN

Beleg: measured with the media feature emulated both ways on the landing page:

| | `no-preference` | `reduce` |
|---|---|---|
| elements with a keyframe animation | 16 | **0** |
| elements with a transition | 40 | 40 |

Exactly one CSS rule in the bundle references `prefers-reduced-motion`. Under `reduce`, the transitioned properties still include `transform`, `translate`, `scale` and `rotate`.

Auswirkung: The heavy lifting is already done - all keyframe animation is suppressed, which is the main thing the preference is for. What remains is motion-producing transitions (card lifts, chevron rotations). Colour, opacity and shadow transitions are fine to keep and should be kept; only the transform ones are worth neutralising.

Verbesserungsvorschlag: Extend the existing reduced-motion rule to zero out transform-family transitions specifically, leaving colour and opacity alone. - Aufwand: S

---

## What works well

Worth recording so it does not get refactored away:

- **Contrast.** A canvas-normalised pass over every text node on all public pages found zero WCAG AA failures, including text over the gradient heroes and the yellow footer card.
- **The sign-up dialog.** `role="dialog"`, `aria-modal="true"`, `aria-labelledby`, a working focus trap (verified over 10 tabs), Escape closes it, and focus returns to the trigger. Textbook.
- **Form validation.** `role="alert"` errors, `aria-invalid`, and `aria-describedby` wiring each error to its field, with the step correctly blocked.
- **Disabled-state explanations.** "Verlassen" is `disabled` with `aria-describedby`, a `title` and `cursor: not-allowed`; the admin page says "Du kannst dein eigenes Konto hier nicht ändern" instead of showing a mystery greyed button; the audit log opens by stating precisely what it does and does not record.
- **i18n.** Switching language preserves route and query string, and updates `lang`, `<title>`, and the linked manifest (`manifest.de.webmanifest` -> `manifest.en.webmanifest`). No raw keys and no mixed-language remnants were found on any page in either language. German strings did not break any layout at 375/768/1440.
- **URL state.** Search and filters round-trip through `?q=`; the back button restored both the URL and the input value.
- **Upload constraints stated up front.** "JPEG, PNG oder WebP, max. 2 MB." is shown before the file picker, not after a rejection.
- **PWA and offline** (`docs/reviews/assets/offline-reload.jpg`). Locale-specific manifests, `display: standalone`, matching `theme_color` `#2d8a5e`, a maskable icon, 5 screenshots, 2 shortcuts, an active service worker - and an offline state presented as a state with a recovery expectation ("Sobald deine Verbindung zurück ist, laden wir die Einsätze"), not as an error.
- **Empty states** (`docs/reviews/assets/opps-empty-state.jpg`). The no-results state names the query, offers "Zurücksetzen" twice where it is useful, and explains what to do next.
- **Icon-button naming.** Per-item accessible names carry context, e.g. "Löschen: Deine Anmeldung für Erste-Hilfe-Kurs wurde abgesagt".
- **Mobile detail layout.** The mobile opportunity page orders content better than the desktop one - fact strip, then a full-width CTA, then map and slots.
- **Uppercase is CSS, not content.** Section headings are `text-transform: uppercase` over sentence-case source text, so screen readers are not spelling them out.

## Parking Lot

Out of scope for this review; noted for whichever lens owns them.

- CSP blocks the silent OIDC token renewal on every public page load (`frame-src` excludes the app's own `/callback`), producing a console error on every route. -> `lens: security` / infrastructure.
- CSP `img-src` omits `data:`, so Leaflet's transparent placeholder GIF is refused on every map render. -> `lens: security` / infrastructure.
- `GET /v1/volunteer-opportunities?PageNumber=1&PageSize=5` is fired and aborted on `/my-signups`. -> `lens: bugs`.
- Staging carries visible test debris: a published opportunity titled "Wir suchen Helfer:innen" whose description is `xxx` is currently the first card on the public `/opportunities` page, plus withdrawn sign-ups labelled "Testeintrag fuer Review" from earlier runs. -> staging hygiene; `reset-staging.yml` exists for this (owner's call).
- Seed organizations use `.example` domains rendered as live links, so the website and email links on every organization profile resolve to nothing. -> seed data, not product.
- `/contact` publishes the operator's personal mailbox as the platform's only support address, and directs other questions to "die Organisation, bei der du dich engagierst" without an in-product way to contact one. -> product/policy decision rather than a frontend defect.

## Prioritized Next Steps

**Quick wins - low effort, high impact.** These five are all S and address the three biggest user-visible problems:

1. **F1** - rewrite one sentence of withdraw copy so it stops misstating a reversible action. One string, two locales.
2. **F16** - write a success message into the polite live region already used for the loading state, and show it. Fixes the product's core conversion event for screen reader and sighted users at once.
3. **F10** - drop `xl:grid-cols-3` in `ActivitySection.tsx`, and **F4** - add a same-day range collapse in `lib/format.ts`. Together these fix the truncation and the three-line dates.
4. **F22** - move the desktop rail earlier in the DOM and place it with grid placement. Identical visual result, correct focus order.
5. **F2**, **F19**, **F29**, **F24** - four small string/attribute fixes: interpolate the deleted-opportunity title, explain "Ausstehend", give admin sub-pages real titles, put "DE" back in the switcher's accessible name.

**Larger undertakings.**

- **F9** - rework the organizer dashboard's space allocation: content-sized widgets, a calendar that opens where the content is, and "Dashboard anpassen" moved up. The highest-impact single piece of work in the report, and the one most likely to change how the product feels to an organizer.
- **F18** - make slot rows the primary selection control and skip the picker for single-slot opportunities.
- **F12** - unify the two opportunity card anatomies behind one component with a density variant.
- **F17** - move progress data into the badge cards so the achievement system shows progress.
- **F3** - redesign the fact strip so "Wann" answers when.
- **F25**, **F26** - two fixes in the Keycloak FTL theme, worth batching into one pass over `keycloak/`.
