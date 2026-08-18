# Frontend & UX Review - einsatzbereit - 2026-08-18

Reviewed: https://einsatzbereit.maik-hasler.de (live staging)
Repository state used for code references: `main` @ `6f3ddda` (the running build is not surfaced anywhere in the UI, see Parking Lot)
Method: live browser passes with Playwright/Chromium, three personas, three viewports, both languages.

## Executive Summary

Einsatzbereit does not read as a template. The green/gold palette, the Barlow Condensed display face against Source Sans 3, the wave-cut page header bands and the blob-masked hero photography add up to a visual direction that is recognisably its own (`assets/2026-08-18/pub-home-1440.jpg`), and the engineering hygiene underneath it is unusually good: a single global focus ring on every interactive element, a working modal focus trap, `prefers-reduced-motion` honoured down to smooth scrolling, an offline state that recovers by itself when the connection returns, upload errors that name the file and its actual size, and confirmation dialogs in front of every privileged admin action. German and English chrome are complete on every screen visited, and the locale even carries across into the Keycloak login page.

The problems are concentrated in two places. First, feedback: the two actions the whole product exists for - signing up for a slot and withdrawing again - are the only significant actions in the app that produce no confirmation message at all, while every organizer and admin action gets a toast (F1). Worse, the withdraw dialog promises the action "cannot be undone", which is false in a way that is invisible until a hidden churn limit blocks re-signing up with a dead-end error (F2). Second, desktop layout: several key pages render a narrow content column with the other half of the page empty, squeezing sign-up cards down to 256 px in a 1440 px viewport (F9), and the organizer dashboard - the first screen an organization sees after logging in - spends 2613 px of scroll on four facts, opening with an empty week calendar that starts at 00:00 (F10).

The top five things to fix: sign-up/withdraw confirmation (F1), the withdraw/reactivation copy contradiction (F2), the dashboard's default layout (F10), the narrow-column desktop pages (F9), and the silent member-invite search (F3). None of them are hard; four of the five are copy or layout changes.

## Scope & Method

**Personas** (staging test accounts from the README): Vera (`vera`, volunteer), Olaf (`olaf`, volunteer + organizer of Lindenauer Nachbarschaftshilfe e.V.), Admin (`admin`, platform admin), plus an anonymous visitor.

**Viewports:** 375x812, 768x1024, 1440x900. Single engine (Chromium via Playwright) - Firefox and WebKit were not available in this environment, so rendering differences specific to Gecko or WebKit are out of this review's evidence base.

**Languages:** German (default) and English via the header switcher, checked on home, opportunity list, help, contact, imprint, privacy, terms and the Keycloak login page.

**Pages and flows covered:** home, opportunity list (incl. filters, keyword search, empty state), organization directory, public organization profile, opportunity detail, help, contact, imprint, privacy, terms, 404; sign-up -> pending state -> withdraw -> re-sign-up; check-in modal; my sign-ups (current and past tabs); profile with badges; profile settings; notification panel; organizer dashboard (view and widget edit mode), organizer opportunity list, engagements, members (incl. invite search), organization settings, create-opportunity wizard step 1 with validation and upload error cases; admin organizations, users, reports, audit log; Keycloak login, login error, forgot password; PWA service worker, offline reload and reconnect; keyboard tab order, focus rings, modal focus trap; `prefers-reduced-motion`.

**Deliberately not covered:** backend logic, data model, infrastructure, security, CI, code hygiene and dead code (those belong to the `lens` / `einsatzbereit-review` skills); no diff or PR review; no axe-core or `jsx-a11y` re-run - the accessibility findings below are limited to things those tools structurally cannot catch (heading strategy, keyboard journeys, focus behaviour, scroll affordances).

**Test data:** one sign-up was created and withdrawn again on "Erste-Hilfe-Kurs"; a second sign-up attempt was rejected by the platform, so nothing was left behind. Verified afterwards: Vera's current sign-ups are back to the two seeded ones. No other account's data was modified; no admin block/promote actions were triggered.

**Evidence:** screenshots referenced per finding live in `docs/reviews/assets/2026-08-18/`.

## Findings

### UX

#### F1 - Signing up and withdrawing produce no confirmation message

**Category:** UX
**Severity:** High
**Confidence:** Confirmed
**Classification:** Best Practice (Nielsen-Norman heuristic #1, visibility of system status; also the project's own convention - `dispatchToast` is used for reporting, publishing, organization actions and every admin action)
**Location:** `/volunteer-opportunities/:id` and `/my-signups` - Persona: Vera - Viewport: all - Language: DE/EN

Evidence: `assets/2026-08-18/vera-detail-slots.jpg` (before), the sidebar state after sign-up in the same flow; code: `frontend/src/pages/VolunteerOpportunityDetailPage.tsx` dispatches toasts only at lines 256 (report) and 264 (publish); `components/SignUpModal.tsx` and `pages/MyEngagementsPage/*` never import `dispatchToast`. Live probe polled for a toast element at 250/550/850/1150/1450 ms after confirming a sign-up - none appeared.

Impact: The single most important moment in the volunteer journey passes silently. The only signal is that a card in the right-hand sidebar swaps from "Zeitslot auswählen" to "Deine Anmeldung / Ausstehend" - roughly 900 px away from the "Mitmachen" button the user just pressed, and below the fold on shorter screens. Withdrawing is the same: the dialog closes and nothing says it worked. Users who are unsure re-submit or go looking in "Meine Anmeldungen" to check.

Recommendation: Dispatch the existing success toast on sign-up ("Du bist angemeldet - wir haben deiner Organisation Bescheid gegeben") and on withdrawal ("Anmeldung zurückgezogen"), the same way reporting and publishing already do. Keep the wording in the same voice as the action label. Effort: S

#### F2 - The withdraw dialog says the action cannot be undone; re-signing up is possible, until an invisible limit blocks it

**Category:** UX
**Severity:** High
**Confidence:** Confirmed
**Classification:** Best Practice (NN #9, help users recognize/diagnose/recover from errors; NN #5, error prevention)
**Location:** `/volunteer-opportunities/:id` - Persona: Vera - Viewport: 1440 - Language: DE

Evidence: `assets/2026-08-18/vera-withdraw-confirm.jpg` - "Möchtest du deine Anmeldung wirklich zurückziehen? Diese Aktion kann nicht rückgängig gemacht werden." Then `assets/2026-08-18/vera-resignup-state.jpg` - a later sign-up attempt on the same slot returns "Diese Anmeldung wurde bereits zu oft zurückgezogen und erneut angemeldet. Bitte wende dich direkt an die Organisation." Copy source: `frontend/src/locales/de.json:961`; the reactivation concept is confirmed by the API error catalogue (`locales/en.json:1546`, "Only withdrawn or cancelled sign-ups can be reactivated").

Impact: Three problems in one flow. The dialog overstates the consequence, which makes people hesitate before an action that is in fact reversible. When they do come back, the reversal works - silently - until a churn limit that was never mentioned refuses it. And the refusal names the only remedy ("contact the organization") without giving a way to do it: the organization's email and phone sit further down the same page, unlinked from the error. A volunteer whose plans changed twice is now stuck on a slot with 19 free places.

Recommendation: Make the withdraw dialog truthful ("Du kannst dich spaeter wieder anmelden, solange Plätze frei sind."), state the limit when the user is close to it, and turn the blocking message into a recoverable one by linking straight to the organization's contact details. Effort: M

Suspected backend cause: the churn limit itself is enforced server-side; the frontend can still surface it honestly before the user hits it.

#### F3 - Member invite search gives no feedback at all for queries shorter than four characters

**Category:** UX
**Severity:** Medium
**Confidence:** Confirmed
**Classification:** Best Practice (NN #1, visibility of system status)
**Location:** `/app/:orgId/dashboard/members` - Persona: Olaf - Viewport: 1440 - Language: DE

Evidence: `assets/2026-08-18/olaf-invite-search.jpg` - the field contains "ver", nothing is rendered below it: no result, no message, no hint. Typing "zzzz-nonexistent" does render "Keine Nutzer:innen gefunden." Code: `frontend/src/pages/app/OrgMembersPage.tsx:363` gates the empty-state message behind `memberSearch.length >= 4`.

Impact: Three-letter first names and prefixes ("ver", "max", "tom", "jan") are exactly what an organizer types first. They get a dead field and no way to tell whether the search is broken, still loading, or genuinely empty - and the one search box that grows an organization's team is the worst place to lose someone's confidence.

Recommendation: Render a hint below the field whenever a query is present but shorter than the threshold ("Mindestens 4 Zeichen eingeben"), and keep the loading state visible while the request is in flight. Effort: S

#### F4 - The "Vergangen" tab lists sign-ups whose deadlines are months in the future

**Category:** UX
**Severity:** Medium
**Confidence:** Confirmed
**Classification:** Best Practice (NN #2, match between system and the real world)
**Location:** `/my-signups` (tab "Vergangen") - Persona: Vera - Viewport: 1440 - Language: DE

Evidence: the past tab lists five withdrawn expressions of interest, four of them carrying "Interesse bekunden bis 11.11.2026" / "12.10.2026" / "12.09.2026" - all after the review date of 18.08.2026.

Impact: The tab is really "beendet" (terminated: withdrawn, cancelled, or over), but it is labelled "past". A volunteer looking for something they withdrew from yesterday will not look under "Vergangen", and an entry dated three months into the future sitting under that label reads like a bug.

Recommendation: Either rename the tab to what it holds ("Abgeschlossen & zurückgezogen") or split terminated sign-ups from genuinely past ones, and show the terminal status as the primary chip rather than the date. Effort: S

#### F5 - Help and Contact promise a "Melden" button that only exists for signed-in users

**Category:** UX
**Severity:** Medium
**Confidence:** Confirmed
**Classification:** Best Practice (NN #10, help and documentation; the instruction must match what the user can actually see)
**Location:** `/help`, `/contact`, `/organizations/:id`, `/volunteer-opportunities/:id` - Persona: anonymous - Viewport: all - Language: DE/EN

Evidence: `/help` and `/contact` both say "nutze die Schaltfläche 'Melden' auf der jeweiligen Einsatz- oder Organisationsseite". The anonymous opportunity detail page renders no report control (see the mobile dump: the report button is replaced by "Melde dich an, um mitzumachen"), and `frontend/src/pages/OrganizationProfilePage.tsx:152` wraps the report button in `auth.isAuthenticated &&`.

Impact: Reporting is the platform's only abuse channel, and the two pages that explain it send anonymous visitors - the people most likely to stumble across spam - to a button that is not there. They are left with the general support mailbox, which the same page frames as the wrong channel for this.

Recommendation: Either show the report control to anonymous users and route them through sign-in on submit, or state the precondition in the help text ("melde dich an und nutze ..."), and give both pages a direct fallback for people who cannot sign in. Effort: S

#### F6 - No way to get directions to an opportunity

**Category:** UX
**Severity:** Medium
**Confidence:** Confirmed
**Classification:** Best Practice (NN #7, flexibility and efficiency of use)
**Location:** `/volunteer-opportunities/:id` - Persona: all - Viewport: 375 and 1440 - Language: DE/EN

Evidence: `assets/2026-08-18/vera-detail-slots.jpg`. The map is intentionally inert - `frontend/src/components/SingleMarkerMap.tsx:48-58` disables dragging, all zoom, keyboard and box zoom - and the address is plain text. A repo-wide search finds no `maps.google`, `geo:` or OpenStreetMap deep link anywhere in `frontend/src`.

Impact: For a product whose core promise is "show up somewhere nearby for a few hours", the last mile is manual: users copy the address into another app. On mobile - where most people plan the trip - a static 254 px map that cannot be panned or zoomed cannot answer "is that near the tram stop?".

Recommendation: Add a "Route planen" link next to the address that opens the platform's map handler (a `geo:` URI on mobile with an OpenStreetMap `https` fallback). This keeps the static map (which is a defensible performance and privacy choice) and adds the missing exit. Effort: S

#### F7 - The "WANN" fact answers with a frequency instead of a date

**Category:** UX
**Severity:** Low
**Confidence:** Confirmed
**Classification:** Best Practice (NN #2, match between system and the real world)
**Location:** `/volunteer-opportunities/:id` - Persona: all - Viewport: all - Language: DE/EN

Evidence: `assets/2026-08-18/vera-detail-slots.jpg` - the fact strip reads "WANN: Einmalig / ABLAUF: Zeitslots / WO: Karl-Heine-Straße 12". The actual date (27.08.2026, 09:00) only appears further down in the slot list.

Impact: "When?" is the first question a volunteer has, and the box that promises to answer it answers a different one. The two most decision-relevant facts (date and free places) are the two that are not in the summary.

Recommendation: Put the next date or the application deadline in the "WANN" slot and move the frequency into a chip next to the category, where "Einmalig"/"Regelmäßig" already appear on the cards. Effort: S

#### F8 - All administration sub-pages share a single document title

**Category:** UX
**Severity:** Low
**Confidence:** Confirmed
**Classification:** Best Practice (WCAG 2.4.2 Page Titled, Level A - each page needs a title that describes its topic; the rest of the app already does this)
**Location:** `/administration/organizations`, `/users`, `/reports`, `/audit-log` - Persona: Admin - Viewport: all - Language: DE

Evidence: all four routes report `Administration | Einsatzbereit`, while comparable sub-pages elsewhere are specific ("Mitglieder - Lindenauer Nachbarschaftshilfe e.V. | Einsatzbereit", "Meine Anmeldungen | Einsatzbereit").

Impact: Browser tabs, history entries and bookmarks for the four admin areas are indistinguishable, and screen-reader users get the same announcement on every route change inside administration.

Recommendation: Set the section name in the title, matching the pattern already used by the organizer area. Effort: S

### Visual design

#### F9 - Desktop pages leave half the width empty while their content is squeezed

**Category:** Visual design
**Severity:** High
**Confidence:** Confirmed
**Classification:** Best Practice (the project's own convention: `--container-page: 90rem` exists precisely so surfaces line up edge to edge, `frontend/src/styles/global.css`)
**Location:** `/my-signups`, `/volunteer-opportunities/:id`, `/app/:orgId/dashboard/members`, `/administration/*` - Persona: Vera / Olaf / Admin - Viewport: 1440 - Language: DE/EN

Evidence (measured live at 1440):
- `assets/2026-08-18/vera-signups-current.jpg` - `main` is 1440 px, the card grid 800 px, each sign-up card **256 px**. Titles break mid-phrase ("Helfer:innen für das / Tierheim"), the organization name wraps to two lines, the date line wraps to three ("Termin: 20.08.2026, / 10:00 - 20.08.2026, / 14:00"), and "Deine Nachricht" is cut off after six words. With two sign-ups, a third of the grid stays empty on top of the empty page margin.
- `assets/2026-08-18/vera-detail-slots.jpg` - a 320 px sidebar holds one 200 px card, then roughly 1000 px of white space runs down the rest of the page while the main column stays at ~640 px.
- `assets/2026-08-18/olaf-members.jpg` - the member card is 896 px inside a 1440 px viewport, with the tab bar above it spanning the full width, so the page looks cut in half.
- `assets/2026-08-18/admin-users.jpg` - same shape again.

Impact: The app looks unfinished on the exact screens where organizers and volunteers do their work, and it costs real legibility, not just polish: a three-line date and a truncated message in a 256 px card are harder to scan than the same data in one line.

Recommendation: Let the card grids fill the available column (`auto-fill, minmax(320px, 1fr)` rather than a fixed column count), and give the sticky sidebar on the detail page something to hold - move "Verfügbare Zeitslots" and the organization contact panel up next to the action card, which also puts the slots beside the button that consumes them. Effort: M

#### F10 - The organizer dashboard is mostly empty chrome

**Category:** Visual design
**Severity:** High
**Confidence:** Confirmed
**Classification:** Best Practice (NN #8, aesthetic and minimalist design - "every extra unit of information competes with the relevant units")
**Location:** `/app/:orgId/dashboard` - Persona: Olaf - Viewport: 1440 and 375 - Language: DE

Evidence: `assets/2026-08-18/olaf-dash.jpg` (2613 px of document height at a 900 px viewport - 2.9 screens - for four facts: one upcoming opportunity, the organization name, two members, one confirmed volunteer). Specifically:
- The calendar widget opens on "Woche" and renders the full 00:00-23:00 grid. The organization's only upcoming opportunity is on 27.08 and therefore not in the displayed week, so the largest element on the page is an empty grid of night hours.
- The "Einstellungen" widget is a card containing a gear icon and nothing else - no label, no target, no state.
- "Bevorstehende Einsätze" holds one row and roughly 450 px of blank card; "Freiwillige" holds the number 1 and roughly 250 px of blank card.
- `assets/2026-08-18/olaf-dash-edit.jpg` shows why: the underlying grid is eight columns wide, and every widget defaults to a fraction of it, so the right half of every row is empty.
- At 375 px (`assets/2026-08-18/m375-orgdash.jpg`) the same page becomes six stacked cards where the agenda row is clipped mid-title ("Erste-Hilfe-K...").

Impact: This is the first screen an organization sees after signing in, and it communicates emptiness rather than the state of their work. The one genuinely useful widget (upcoming opportunities) is pushed below the fold by an empty calendar.

Recommendation: Default the calendar to "Agenda" (which is already the mobile default and shows the 27.08 entry immediately), or keep the week view but scroll it to working hours and collapse empty nights. Give widgets a sensible default span so the grid fills, drop the icon-only settings widget in favour of a labelled link, and let "Bevorstehende Einsätze" and "Freiwillige" size to their content. Effort: M

#### F11 - Every application deadline is styled as urgent, whatever the date

**Category:** Visual design
**Severity:** Medium
**Confidence:** Confirmed
**Classification:** Preference (with a best-practice basis: colour carries meaning only while it stays discriminating)
**Location:** `/opportunities`, `/`, organization pages - Persona: all - Viewport: all - Language: DE/EN

Evidence: `assets/2026-08-18/pub-opps-1440.jpg` - seven of nine cards render their deadline in amber with a clock icon, including "Interesse bekunden bis 11.11.2026", nearly three months out. Code: `frontend/src/components/VolunteerOpportunitiesList/OpportunityListItem.tsx:104-116` returns `tone: "text-amber-700"` for every `validUntil`, with no comparison against the current date.

Impact: Amber is the loudest colour on an otherwise calm green-and-grey card, and it is on almost every card, so it stops meaning "hurry" and just means "this opportunity has a deadline". When a deadline really is 48 hours away, nothing distinguishes it.

Recommendation: Keep the amber tone for deadlines inside a real window (say seven days), use the neutral grey the start-date line already uses beyond that, and consider "Noch 3 Tage" instead of an absolute date when it is close. Effort: S

#### F12 - The widget drag handle sits on top of the widget title in dashboard edit mode

**Category:** Visual design
**Severity:** Medium
**Confidence:** Confirmed
**Classification:** Best Practice (WCAG 2.2 SC 2.4.11 Focus Not Obscured is about focus, but the same principle applies to content: controls must not cover the labels they belong to)
**Location:** `/app/:orgId/dashboard` in edit mode - Persona: Olaf - Viewport: 1440 - Language: DE

Evidence: `assets/2026-08-18/olaf-dash-edit.jpg` - the narrow "Einstellungen" widget renders as "Einstel[handle]ngen", with the drag handle overlapping the middle of its own title.

Impact: In the one mode where users need to tell widgets apart to rearrange them, the narrow widgets' names become unreadable.

Recommendation: Reserve space for the handle and delete button in the widget header row instead of overlaying them, and truncate the title with an ellipsis when the widget is too narrow. Effort: S

#### F13 - Profile header: an orphaned streak line and two controls for the same job

**Category:** Visual design
**Severity:** Low
**Confidence:** Confirmed
**Classification:** Preference
**Location:** `/profile` - Persona: Vera - Viewport: 1440 - Language: DE

Evidence: `assets/2026-08-18/vera-profile.jpg` - the stat row shows two tiles ("2 Bestätigte Einsätze", "1 Woche in Serie - Wochenheld") and then a third line, "2 Tage in Folge angemeldet - Anmeldeserie", hanging below the second tile outside the tile grid. Right below, the empty profile panel offers "Profil vervollständigen" while a "Bearbeiten" button for the same panel sits in its header.

Impact: The stat block reads as a layout accident rather than a hierarchy, and the two edit affordances make the user pick between identical options - low stakes, but it is the first thing a new volunteer sees on their own profile.

Recommendation: Give both streaks equal treatment as tiles (or fold them into one "Serien" tile), and drop the header "Bearbeiten" while the panel is empty - the empty state's own call to action is the better one. Effort: S

### UI

#### F14 - Horizontally scrollable regions give no sign that they scroll

**Category:** UI
**Severity:** Medium
**Confidence:** Confirmed
**Classification:** Best Practice (NN #6, recognition rather than recall)
**Location:** `/app/:orgId/dashboard*` tab bar and agenda table at 375 px; the notification panel at 1440 px - Persona: Olaf, Vera - Language: DE

Evidence (measured live):
- Organizer tab bar at 375 px: `scrollWidth` 474 px vs `clientWidth` 343 px, `overflow-x: auto`. "Einstellungen" is cut mid-word at the viewport edge and "Mitglieder" is entirely off-screen (`assets/2026-08-18/m375-orgdash.jpg`).
- Dashboard agenda table at 375 px: 480 px of table in a 307 px container, so the event title clips to "Erste-Hilfe-K...".
- Notification panel at 1440 px: list `scrollHeight` 529 px vs `clientHeight` 320 px, with the fourth item cut through its timestamp and no fade, shadow or scrollbar cue (`assets/2026-08-18/vera-notifications.jpg`).

Impact: The content is reachable, but nothing says so. On mobile the members area of an organization is effectively hidden behind a swipe nobody prompts, and a cut-off notification reads as a rendering fault rather than "there is more below".

Recommendation: Add the usual affordances - an edge fade or scroll-shadow on all three containers, and on the tab bar auto-scroll the active tab into view plus a chevron when content overflows. Effort: S

#### F15 - Slot option text runs two facts together

**Category:** UI
**Severity:** Low
**Confidence:** Confirmed
**Classification:** Best Practice (project convention: every other card puts capacity in its own chip)
**Location:** sign-up dialog on `/volunteer-opportunities/:id` - Persona: Vera - Viewport: 1440 - Language: DE

Evidence: `assets/2026-08-18/vera-resignup-state.jpg` - the option reads "27.08.2026, 09:00 - 27.08.2026, 17:00 20 Plätze frei", so the end time and the capacity collide into "17:00 20".

Impact: In the one dialog where a volunteer picks between slots, the two numbers that matter run together and have to be parsed apart.

Recommendation: Separate the capacity with a middot or right-align it as its own column, matching the slot list on the page behind the dialog. Effort: S

### Content

#### F16 - Organization pages drop the dates, and the same visual slot means different things

**Category:** Content
**Severity:** Medium
**Confidence:** Confirmed
**Classification:** Best Practice (NN #4, consistency and standards)
**Location:** `/organizations/:id`, "Weitere Einsätze dieser Organisation" on `/volunteer-opportunities/:id` - Persona: all - Viewport: all - Language: DE/EN

Evidence: `assets/2026-08-18/pub-org-profile.jpg` - all six opportunity cards show a calendar icon followed by "Einmalig" or "Regelmäßig" and no date anywhere. On `/opportunities` (`assets/2026-08-18/pub-opps-1440.jpg`) the identical calendar-icon slot carries a real date ("Beginnt am 27.08.2026, 09:00") or a deadline. Code: `components/PublicOpportunityCard.tsx:86` renders `formatOccurrence(...)`, while `components/VolunteerOpportunitiesList/OpportunityListItem.tsx:95-116` renders the date line.

Impact: An organization's own page - the page a volunteer lands on from a flyer or a link - is the one place where nothing says when anything happens. And because the same icon and position mean "date" on one page and "frequency" on the other, users who learned the pattern on the list page misread it here.

Recommendation: Use one card component for one object. If the compact variant must stay, give it the date line and move the frequency into the chip row, where "Einmalig"/"Regelmäßig" already lives on the same cards. Effort: M

#### F17 - Same-day time ranges print the date twice

**Category:** Content
**Severity:** Low
**Confidence:** Confirmed
**Classification:** Preference
**Location:** slot list and sign-up dialog on `/volunteer-opportunities/:id`, `/my-signups`, `/app/:orgId/dashboard/opportunities/:id/engagements` - Persona: Vera, Olaf - Language: DE

Evidence: "27.08.2026, 09:00 - 27.08.2026, 17:00" in the slot list and dialog; "Termin: 20.08.2026, 10:00 - 20.08.2026, 14:00" in "Meine Anmeldungen" (`assets/2026-08-18/vera-signups-current.jpg`), where it wraps to three lines in the 256 px card of F9.

Impact: Double the characters for the same fact, and it is the string most likely to wrap badly in narrow cards.

Recommendation: Collapse same-day ranges to "27.08.2026, 09:00 - 17:00" in the shared date formatter, keeping the full form only when the range crosses midnight. Effort: S

#### F18 - The check-in dialog shows a raw UUID with no explanation

**Category:** Content
**Severity:** Low
**Confidence:** Confirmed
**Classification:** Best Practice (name things by what people recognise, not by how the system is built)
**Location:** check-in dialog on `/my-signups` - Persona: Vera - Viewport: 1440 - Language: DE

Evidence: `assets/2026-08-18/vera-checkin-modal.jpg` - below the QR code sits `019ff9d4-9930-70d0-972d-dc4aadcdfd98` in monospace, with no label and no instruction.

Impact: It is presumably the manual fallback when a scan fails, but nothing says so, and a 36-character hexadecimal string is not something a volunteer can read out to an organizer with any confidence.

Recommendation: Label it ("Falls der Scan nicht klappt, nenne diesen Code") and format it in readable groups, or replace it with a short human-readable check-in code. Effort: S

#### F19 - Keycloak (FTL, not React): English strings use Title Case while the app uses sentence case

**Category:** Content
**Severity:** Low
**Confidence:** Confirmed
**Classification:** Best Practice (NN #4, consistency and standards)
**Location:** Keycloak login and forgot-password pages - Persona: anonymous - Viewport: 1440 - Language: EN

Evidence: after switching the app to English, the login page renders "Sign In", "Forgot Password?", "Don't have an account yet? Register", while the app header immediately behind it says "Sign in" and the app's own buttons are sentence case throughout.

Impact: The login page is the seam between two systems, and capitalisation is exactly the tell that gives that seam away. Everything else about the theme (logo, eyebrow, display face, green button, "Zurück zu Einsatzbereit") successfully hides it.

Recommendation: Align the English theme messages to sentence case in the Keycloak message bundle. Note the German side is already consistent. Effort: S

Positive note for the same area (`assets/2026-08-18/kc-login-error.jpg`): the app's language choice carries into Keycloak (`html lang="en"` on the login page after switching), the password visibility toggle works, and the error state is field-level rather than a banner.

### Accessibility

#### F20 - The organization directory has no headings at all

**Category:** Accessibility
**Severity:** Medium
**Confidence:** Confirmed
**Classification:** Best Practice (WCAG 2.2 SC 1.3.1 Info and Relationships / SC 2.4.6 Headings and Labels; and the project's own pattern on `/opportunities`)
**Location:** `/organizations` - Persona: all - Viewport: all - Language: DE/EN

Evidence: a live heading dump of `main` returns an empty list on `/organizations`, while `/opportunities` returns one `h2` per card ("Erste-Hilfe-Kurs", "Patenschaft für Pflegetiere", ...) plus a screen-reader-only `h2` for the list itself. The organization cards render their names as plain text.

Impact: Heading navigation is how screen-reader users skim a list. On the opportunity list that works; on the organization directory the entire result set is invisible to it, and the two sibling directories behave differently, which is worse than either behaviour alone. This is a gap `jsx-a11y` and axe-core do not flag - neither checks whether a list of results uses headings.

Recommendation: Render the organization name as the card heading at the same level the opportunity cards use, and add the same visually hidden list heading. Effort: S

### i18n

#### F21 - Opportunity and organization content is never translated, and nothing says so

**Category:** i18n
**Severity:** Medium
**Confidence:** Confirmed
**Classification:** Preference (the fallback itself is a reasonable design; the missing signal is the finding)
**Location:** `/`, `/opportunities`, `/organizations`, `/volunteer-opportunities/:id` in English - Persona: all - Viewport: all - Language: EN

Evidence: `assets/2026-08-18/en-opps.jpg` - fully English chrome ("Find opportunities", "Express interest by 12 Oct 2026", "Unlimited spots") wrapped around German content ("Helfer:innen für das Tierheim", "Hilf uns dabei, die Tiere in unserem Tierheim zu versorgen"). `pickLocalizedText(titleDe, titleEn, language)` falls back silently when the English variant is empty.

Impact: An English-speaking user gets a half-translated page with no explanation, and cannot tell whether the English fields exist but are empty, or whether the platform simply has no English content. The create-opportunity wizard does have DE/EN tabs with a completeness dot, so organizations are being asked for both languages - the reading side just never reflects the result.

Recommendation: Mark fallen-back content ("Nur auf Deutsch verfügbar" / "Only available in German") on cards and detail pages, and set `lang="de"` on those text nodes so screen readers switch voice. Effort: M

Note: staging's seed data has no English variants at all, so the effect is more total here than it would be with real content - the missing signal is the durable part.

### PWA

#### F22 - The offline state offers no way to retry

**Category:** PWA
**Severity:** Low
**Confidence:** Confirmed
**Classification:** Preference
**Location:** `/opportunities` while offline - Persona: anonymous - Viewport: 1440 - Language: DE

Evidence: `assets/2026-08-18/offline-reload-opps.jpg` - a proper empty state ("Du bist offline. Sobald deine Verbindung zurück ist, laden wir die Einsätze.") with icon and heading, but no action. Verified separately: the promise is kept - restoring the connection reloads the list on its own, without a reload.

Impact: Minimal, and the auto-recovery is genuinely good. The gap is only for the user whose connection came back without the browser noticing (captive portals, flaky mobile data), who has no button to press and no reason to believe pressing anything would help.

Recommendation: Add a secondary "Erneut versuchen" button to the offline state. Keep the auto-retry as the primary path. Effort: S

### What is working well

Worth stating explicitly, because these are the parts a review usually finds broken:

- **Focus ring**: one global `:focus-visible` treatment (brand outline plus white halo, `styles/global.css:425`) applies to every control checked - header links, buttons, card links, FAQ summaries, dialog buttons - and stays visible on both light and dark-green surfaces.
- **Keyboard journeys**: skip link first in tab order; tab order matches visual order on the home page through 22 stops; the sign-up dialog traps focus (three controls cycling, verified over eight tabs) and closes on Escape.
- **Reduced motion**: with `prefers-reduced-motion: reduce`, animated elements drop from 35 to 2 and `scroll-behavior` falls back to `auto`.
- **Upload errors**: "'big.png' ist 3,1 MB groß - erlaubt sind maximal 2 MB." and "'notanimage.txt' ist kein unterstütztes Bild. Erlaubt sind JPEG, PNG und WebP." - the file, the actual value, the limit (`assets/2026-08-18/olaf-upload-toolarge.jpg`).
- **Form validation** in the create wizard: a four-step progress bar, per-language completeness dots, required markers, character counters and inline errors (`assets/2026-08-18/olaf-create-step1-errors.jpg`).
- **Destructive admin actions** all pass through `ConfirmDialog`, including blocking and promoting a user (`pages/AdministrationPage.tsx:552-560` documents why).
- **Notifications** carry per-item accessible names ("Als ungelesen markieren: Deine Anmeldung für Erste-Hilfe-Kurs wurde abgesagt").
- **Language switching** preserves route and query state (`/opportunities?q=Tierheim&occurrence=Recurring` survived DE -> EN intact) and carries into Keycloak.
- **Empty and offline states** are real designed states with icon, heading, explanation and - where applicable - a reset action.

## Parking Lot

- Anonymous page loads trigger a silent-renew iframe to Keycloak that is blocked by the page's own CSP (`frame-src https://login.maik-hasler.de` does not cover the callback back to `einsatzbereit.maik-hasler.de`), producing a repeating console error on every public page. Frontend-adjacent but a configuration issue - `lens` "bugs" or a CSP/config lens.
- The admin organization list shows "1 Mitglied" for Lindenauer Nachbarschaftshilfe e.V. while that organization's own members page lists two. Likely a backend counting question - `lens` "bugs".
- Live staging carries visible debris: an opportunity titled "Wir suchen Helfer:innen" with the description "xxx" is published on an organization's public profile, and Vera's past sign-ups are full of "Testeintrag für Review" messages from earlier runs. Data hygiene, see `reset-staging.yml`.
- The running build is not identifiable from the UI (no version in the footer, no meta tag, no `/version`), so a live review cannot state which commit it examined. Worth a small footer or meta build stamp - repo hygiene lens.
- Features named in the review brief that do not exist in the shipped frontend: saved searches / alerts, a map browse view for the opportunity list, CSV export, organization verification. Not defects - noted so the gap between brief and product is on record.

## Prioritized Next Steps

**Quick wins (low effort, high impact)**

1. F1 - dispatch success toasts on sign-up and withdraw. One-line changes in two places; removes the biggest hole in the volunteer flow.
2. F2 (copy half) - fix the withdraw dialog wording and link the org contact from the blocking error.
3. F3 - show a "mindestens 4 Zeichen" hint in the invite search.
4. F10 (calendar half) - default the dashboard calendar to Agenda; the single biggest visual improvement per line changed.
5. F11 - make the amber deadline tone conditional on proximity.
6. F14 - scroll affordances on the mobile tab bar, agenda table and notification list.
7. F5, F7, F8, F17, F18 - small copy and title fixes that can travel in one PR.

**Larger undertakings**

1. F9 - rework the desktop grids and the detail-page sidebar so pages use the 90rem container they already declare. Touches four page layouts; the highest-value visual change in the review.
2. F10 (layout half) - default widget spans and per-widget minimum content so the dashboard fills its own eight-column grid.
3. F16 - consolidate the two opportunity card variants into one component with a consistent date line.
4. F21 - a fallback indicator for untranslated user content, with `lang` attributes.
5. F6 - a "Route planen" affordance, and a decision on whether the map should stay fully inert.
