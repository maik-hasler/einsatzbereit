# Frontend & UX Review - einsatzbereit - 2026-08-24

Reviewed: https://einsatzbereit.maik-hasler.de/ · Repo commit at review time: `ee27be5`

## Executive Summary

Einsatzbereit is in far better shape than a typical volunteer-matching side project, and the gap between its best and worst moments is what this review is really about. The craft floor is high: measured against the actually painted background, not a single one of 220 text elements sampled across the landing page and the browse list falls below WCAG AA contrast, including white type over the photographic hero gradient. Every one of the first 22 keyboard stops on the landing page carries a visible focus ring, the skip link moves real focus into `<main>`, modals are `aria-modal` with a working focus trap, Escape handling and focus restoration, offline is communicated as a state with a retry action rather than an error, and the DE/EN locale files are at exact 1357-key parity with no drift. The create-opportunity wizard is the strongest single screen in the product: `aria-invalid` plus `role="alert"` plus focus moved to the first invalid field, and upload errors that name the file, its actual size and the limit.

Against that floor, the top risks are concentrated in one place: the path from "I found an opportunity" to "I am signed up". Three defects stack on that path. Signing in from an opportunity page discards the opportunity and drops the visitor on the homepage, because three call sites omit the `state.returnTo` that the header and `ProtectedRoute` both pass correctly (F1, Critical). A time slot that ended six days ago is listed under "Verfügbare Zeitslots" with 19 free places and opens a live booking confirmation (F2, High). And silent SSO is permanently broken on this deployment, so a returning visitor in a new tab looks signed out and the only recovery is the sign-in button that loses their page (F3, High). F1 and F3 compound each other: the broken renewal is what pushes users onto the button that then loses their context.

The remaining findings are quality rather than blockers: German organisation text served inside `<html lang="en">` without a `lang` marker, a badge at 100 percent progress still drawn in the locked style, no result count above a "Mehr laden" list, and a 20x20 icon target. Notably, the codebase already solves several of these correctly elsewhere - `findNextTimeSlot` filters past slots, `OpportunityCard` sets `lang` on fallback text, `Header.tsx` passes `returnTo`. Most fixes are about extending an existing pattern to the one place that missed it, which is why the effort estimates below are mostly S.

## Scope & Method

**Method.** No `/live-verify` skill exists in this environment, so the fallback in the brief was used: a scripted Playwright 1.56.1 session driving the pre-installed Chromium 1194 against the live deployment. Only one browser engine was available (Chromium); nothing was cross-checked in Gecko or WebKit. Outbound traffic runs through this session's TLS-terminating egress proxy, which required capping the browser at TLS 1.2; certificate verification stayed enabled throughout.

**Coverage.** Viewports 375 x 812, 768 x 1024 and 1440 x 900. Languages German (default) and English via the header switcher. Personas `vera` (volunteer), `olaf` (organiser) and `admin`, each through a real Keycloak login.

Routes exercised: `/`, `/opportunities` (incl. `?q=` search and the empty-result state), `/organizations`, `/volunteer-opportunities/:id`, `/help`, `/contact`, `/imprint`, `/terms-of-use`, `/privacy-policy`, a 404 route, `/my-signups`, `/profile`, `/profile/settings`, `/app/:org/dashboard` and its `opportunities`, `engagements`, `members` and `settings` tabs, `/administration/{organizations,users,reports,audit-log}`, plus the Keycloak login and password-reset pages.

Interactions driven: opportunity sign-up modal and slot selection, report-content modal (focus trap, Escape, focus restore), the four-step create-opportunity wizard including empty-form validation and banner upload with a wrong file type and a 3 MB file against a 2 MB limit, the mobile menu, the organisation switcher, and the DE/EN switch on four screens.

**Custom instrumentation.** Because the brief asks for checks that complement rather than duplicate the existing axe-core and `jsx-a11y` automation, three probes were written for this review:

- **Contrast against real painted pixels.** For each text element the glyphs were set to `transparent`, a viewport screenshot taken, the element's rectangle cropped and decoded (a small PNG decoder written for this run), and the median background luminance compared against the text colour resolved through a canvas so that Tailwind 4 `oklch()` values are measurable. This is what lets contrast be judged over gradients and photographs, which axe skips.
- **WCAG 2.2 target size (2.5.8)** with the inline-text and visually-hidden exceptions honoured, so ordinary prose links are not falsely flagged.
- **Language-of-parts detection**: German-looking runs of text on an `<html lang="en">` page, resolved against their nearest `[lang]` ancestor.

**Excluded.** Backend logic, data model, infrastructure, security, CI and performance tooling, code hygiene and dead code - these belong to the existing `einsatzbereit-review` lenses. No diff or PR review. Nothing was fixed; this report is the deliverable.

**Features named in the brief that do not exist in this build** and were therefore not reviewed: a map browse view and mini-calendar on the opportunities list (the only Leaflet map is the static single-marker map on the detail page), saved searches and alerts, and CSV export. Achievements, check-in with QR scanning, feedback, notifications, invitations, the organisation switcher and image uploads all do exist and were reviewed.

**Test data.** No test data was created. The past-slot booking (F2) was driven up to the confirmation dialog and deliberately not submitted; the create-opportunity wizard was exercised through validation, upload errors and step 2 but never saved as draft or published. Nothing was deleted.

---

## Findings

### F1 - Signing in from an opportunity page throws the visitor back to the homepage

**Kategorie:** UX
**Schweregrad:** Kritisch
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #3 Nutzerkontrolle und -freiheit; #4 Konsistenz - the same app already does this correctly in `Header.tsx`)
**Ort:** `/volunteer-opportunities/:id`, `/organizations/:id` · Persona: logged-out visitor · Viewport: all · Sprache: DE/EN

Beleg: `assets/2026-08-24-frontend-ux/f1-offer-page-before-login.png` (the offer page with its "Anmelden" call to action), `f1-landed-on-homepage-after-login.png` (where you actually end up). Reproduced twice end to end: open the offer page logged out, click the in-page "Anmelden", authenticate as `vera`, and the browser lands on `https://einsatzbereit.maik-hasler.de/` showing "Dein Ehrenamt beginnt hier." - not the offer.

Code: `main.tsx:54` resolves the post-login target as `(user?.state as { returnTo?: string })?.returnTo ?? "/"`, but `signinLocaleArgs()` (`lib/authLocale.ts`) returns only `{ ui_locales }`. Three call sites therefore never set `returnTo`:

- `pages/VolunteerOpportunityDetailPage.tsx:549` - the primary sign-up call to action on an offer
- `pages/VolunteerOpportunityDetailPage.tsx:643` - the per-slot sign-up path
- `pages/OrganizationProfilePage.tsx:152` - the organisation profile action

By contrast `Header.tsx:79` and `:86`, `ProtectedRoute.tsx:47`, `useSessionExpiryHandler.ts:38` and `HomePage.tsx:137` all pass `state: { returnTo: ... }` correctly. `App.tsx:89` (the auth-error retry button) has the same omission.

Auswirkung: This is the product's primary conversion funnel. A shared offer link is the most likely entry point for a new volunteer, and it is exactly the path that discards their intent at the moment they commit. After login they are on the homepage with no breadcrumb back, and must search for the opportunity again from memory. Signing in from the header, by contrast, returns them correctly - so the behaviour is inconsistent as well as lossy.

Verbesserungsvorschlag: Give `signinLocaleArgs()` an optional return-path argument, or add a small `signinWithReturn(location)` helper, and use it at the four call sites above so they match the header. A route-level guard would be sturdier still: any `signinRedirect` that omits `state.returnTo` could default to the current `location.pathname + location.search` rather than `/`. · Aufwand: S

---

### F2 - A time slot that already ended is listed as available and can still be booked

**Kategorie:** UX
**Schweregrad:** Hoch
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #5 Fehlervermeidung; #1 Sichtbarkeit des Systemstatus)
**Ort:** `/volunteer-opportunities/01a0254e-5025-75e6-a903-2d8c98fa6811` ("Erste-Hilfe-Kurs") · Persona: all, incl. logged-out · Viewport: all · Sprache: DE/EN

Beleg: `assets/2026-08-24-frontend-ux/f2-past-slot-listed-as-available.png` - under the heading "VERFÜGBARE ZEITSLOTS", the first row reads `18.08.2026, 09:00-17:00` with `19 Plätze frei`. The review date is 2026-08-24, so that slot ended six days earlier. `f2-past-slot-en.png` shows the same on the English page. `f2-past-slot-booking-confirm.png` shows the consequence: clicking that row (as `admin`, who has not signed up for this opportunity) opens a confirmation dialog reading "Du meldest dich für 18.08.2026, 09:00-17:00 an." with an enabled "Anmeldung abschicken" button. The submission was deliberately not sent.

The same stale slot also appears in the organiser dashboard calendar for August 2026, styled identically to the future one.

Code: `pages/VolunteerOpportunityDetailPage.tsx:825` renders `opportunity.timeSlots.map(...)` with no date filter, and clickability is decided by `clickable = showSignUpCta && !isSlotFull(ts.maxParticipants, ts.bookedCount)` - capacity only, never time. The concept already exists in the codebase: `lib/format.ts` `findNextTimeSlot` selects `endDateTime >= now`, which is why the "WANN" summary on the very same page correctly reads `04.09.2026, 09:00` while the list below still offers 18.08.

Auswirkung: The page contradicts itself - the summary names one date, the list offers an earlier one. A volunteer can complete a sign-up for an event that has already happened, which produces a dead registration for them and junk in the organiser's pending queue. "19 Plätze frei" on an expired slot also inflates perceived availability across the site.

Verbesserungsvorschlag: Filter the slot list by the predicate `findNextTimeSlot` already uses, or keep past slots visible for context but render them non-interactive with an explicit "Abgelaufen" state and no capacity figure. Either way, derive `clickable` from time as well as capacity so the confirmation dialog is unreachable for a past slot. · Aufwand: S

---

### F3 - Silent SSO and token renewal are broken on this deployment, so returning visitors look signed out

**Kategorie:** UX
**Schweregrad:** Hoch
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #1 Sichtbarkeit des Systemstatus)
**Ort:** every route · Persona: any previously authenticated user · Viewport: all · Sprache: DE/EN

Beleg: `assets/2026-08-24-frontend-ux/f8-authstate-1-flash-loggedout.png` and `f8-authstate-2-restored.png` - a fresh browser context carrying the Keycloak session cookies opens the offer page and the header still shows "Anmelden / Registrieren" at 0.9 s and unchanged at 6 s.

The cause is a redirect URI the deployed Keycloak realm does not accept. Requesting the authorize endpoint directly, same client, only the redirect URI differing:

```
https://einsatzbereit.maik-hasler.de/callback            -> HTTP 200
https://einsatzbereit.maik-hasler.de/silent-renew.html   -> HTTP 400  (Invalid parameter: redirect_uri)
```

Every page load consequently emits two console errors: a `frame-ancestors 'self'` CSP violation for the hidden iframe, and the 400 itself. `keycloak/realms/einsatzbereit-realm.json:87-90` does declare both `${KC_FRONTEND_URL}/callback` and `${KC_FRONTEND_URL}/silent-renew.html`, so the deployed realm has drifted from the template in the repository - most likely imported before that entry was added, since Keycloak imports a realm only once.

Auswirkung: Two user-visible effects. First, `useSilentSsoProbe` can never succeed, so opening Einsatzbereit in a new tab presents the full logged-out interface - including the logged-out call to action on an offer - even though the Keycloak session is alive. This is deliberate in part: `main.tsx:33` stores tokens in `sessionStorage` on purpose, with a documented rationale about shared and kiosk machines, and the silent probe is what is supposed to bridge that gap. Right now the bridge is out. Second, `automaticSilentRenew: true` (`main.tsx:26`) uses the same iframe, so access tokens can never be refreshed and an active session ends at token expiry instead of renewing. The two together funnel users onto the sign-in button that then loses their page (F1).

Verbesserungsvorschlag: The fix itself is a deployment change, not a frontend one - add `https://einsatzbereit.maik-hasler.de/silent-renew.html` to the `frontend` client's valid redirect URIs in the deployed realm so it matches the checked-in template. Frontend-side, the state deserves to be visible rather than silent: while the probe is in flight, render the account area as a neutral pending state instead of the full logged-out affordances, and surface a "Sitzung abgelaufen - bitte erneut anmelden" state when renewal fails, rather than reverting to the anonymous interface.

*Vermutliche Ursache: Deployment- bzw. Keycloak-Konfiguration (deployed realm out of sync with `keycloak/realms/einsatzbereit-realm.json`). Nicht im Rahmen dieses Reviews umgesetzt.*
· Aufwand: S (config) + S (frontend pending/expired state)

---

### F4 - German organisation text is served inside an English page with no language marker

**Kategorie:** i18n / Barrierefreiheit
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (WCAG 2.2 AA, 3.1.2 Language of Parts; plus the project's own convention in `OpportunityCard.tsx:157`)
**Ort:** `/volunteer-opportunities/:id` ("About this organization"), `/organizations`, `/organizations/:id` · Persona: all · Viewport: all · Sprache: EN

Beleg: `assets/2026-08-24-frontend-ux/f3-german-org-text-on-en-page.png` - with the interface switched to English and `document.documentElement.lang === "en"`, the section reads "Wir unterstützen Menschen in Leipzig und Umgebung - von der Nachbarschaftshilfe bis zum Sanitätsdienst." Probing each German run of text against its nearest `[lang]` ancestor returns `effectiveLang: "en"`, tagged only by `<html>`. The organisations directory shows the same for two organisations.

Two contributing causes. Opportunities carry `titleEn` / `descriptionEn` and are resolved through `pickLocalizedText`, which deliberately returns `{ text, lang }` so callers can mark fallbacks - and callers do: `OpportunityCard.tsx:157,198,229`, `PageHeaderBand.tsx:80,91`, `OrgOpportunitiesPage.tsx:344,362`, `CalendarWidget.tsx:100`. Organisations have no English fields at all (no `descriptionEn` in `CreateOrganizationModal.tsx` or the organisation settings page), so their text is always German and never passes through that helper, and therefore never gets a `lang` attribute.

Auswirkung: A screen reader announces German prose with English pronunciation rules, which ranges from hard to follow to unintelligible. It also undercuts an otherwise excellent bilingual story: an English-speaking volunteer reads a fully translated opportunity and then hits untranslated German the moment they look at who is behind it.

Verbesserungsvorschlag: Two steps, independently useful. Short term and purely frontend, wrap organisation-supplied text in `<span lang="de">` wherever it is rendered on a page that may be English - this alone closes the WCAG 3.1.2 gap. Longer term, give organisations the same `nameEn` / `descriptionEn` treatment opportunities already have, including the wizard's "Deutsch / English" tab pattern, which is already built and well explained. · Aufwand: S for the `lang` marker, M for English organisation fields

---

### F5 - A badge at 100 percent progress is still drawn in the locked style

**Kategorie:** UX / Visuelles Design
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #1 Sichtbarkeit des Systemstatus; #4 Konsistenz und Standards)
**Ort:** `/profile` · Persona: Vera · Viewport: all · Sprache: DE/EN

Beleg: `assets/2026-08-24-frontend-ux/f4-badge-complete-but-locked.png` - "Erster Schritt" shows a completely filled green progress bar and the label "1 von 1", yet keeps the unearned treatment: grey icon, grey title, dashed grey border, transparent background. "Früheinsteiger" in the same grid shows the earned treatment: green icon, solid `rgb(168, 223, 195)` border, white background, "Freigeschaltet: 24.08.2026". Computed styles confirm `borderStyle: "dashed"` for the completed badge against `borderStyle: "solid"` for the earned one. The profile header simultaneously reads "1 Bestätigter Einsatz".

Code: `components/BadgeGrid.tsx` derives `isEarned` purely from the backend's earned list (`earnedByKey.get(entry.key)`), while the progress bar renders only in the `!isEarned` branch. Nothing reconciles the two, so `currentProgress === progressTarget.target` with `isEarned === false` is a reachable state and renders as "requirement met, nothing awarded".

Auswirkung: Achievements exist to reward; a full bar in the locked style reads as the system withholding something the user earned, which is worse than showing no progress at all. It also makes the profile self-contradictory, since the header already counts the confirmed engagement that the badge requires.

Verbesserungsvorschlag: Treat "progress complete but not yet granted" as its own state rather than letting it fall through to "locked" - for example keep the filled bar but switch to the earned colour treatment with wording such as "Wird in Kürze freigeschaltet". Worth confirming separately whether the award is genuinely pending on the backend or simply never granted; the frontend should not imply the latter either way.

*Vermutliche Mit-Ursache: Backend (Achievement wurde trotz erfuellter Bedingung nicht vergeben). Frontend-seitig laesst sich der Zustand dennoch sauber darstellen.*
· Aufwand: S

---

### F6 - The opportunities list never says how many results there are

**Kategorie:** UX
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #1 Sichtbarkeit des Systemstatus)
**Ort:** `/opportunities` · Persona: all · Viewport: all · Sprache: DE/EN

Beleg: `assets/2026-08-24-frontend-ux/f5-no-result-count.png`. Scanning the rendered page for any count pattern (`N Einsätze`, `Ergebnis`, `Treffer`, `gefunden`) returns nothing, while a "Mehr laden" button is present. The heading above the grid is the bare "Suchergebnisse".

Auswirkung: With incremental loading and no total, a volunteer cannot tell whether "Mehr laden" will add three more opportunities or three hundred, nor whether narrowing a filter actually helped. Filters are the main tool on this page, and their effect is currently invisible except by counting cards by hand. The empty state, by contrast, is handled well - "Keine Einsätze gefunden. Passe deine Filter an oder setze sie zurück." with a reset action - so only the populated case lacks feedback.

Verbesserungsvorschlag: Put the total next to the "Suchergebnisse" heading ("24 Einsätze gefunden"), and once loading is under way show the loaded-of-total ratio near "Mehr laden" ("9 von 24 geladen"). Announce the count in the existing results live region so filter changes are conveyed non-visually too. · Aufwand: S, assuming the list endpoint already returns a total; otherwise it needs one.

---

### F7 - Icon-only targets fall below the 24 x 24 minimum

**Kategorie:** Barrierefreiheit
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (WCAG 2.2 AA, 2.5.8 Target Size (Minimum))
**Ort:** footer on every route; organiser dashboard calendar · Persona: all · Viewport: all · Sprache: DE/EN

Beleg: `assets/2026-08-24-frontend-ux/f6-github-icon-20px-target.png`. Measured hit areas, with the inline-text and visually-hidden exceptions of 2.5.8 already excluded:

- Footer GitHub link: 20 x 20 CSS px at 1440 and 768, and 343 x 20 at 375 - the height fails at every breakpoint. `components/Footer.tsx:203-210` gives the anchor `inline-flex` with an `h-5 w-5` SVG and no padding, so the anchor collapses to the icon.
- Organiser dashboard month calendar: the day-number buttons (`rbc-button-link`, from `react-big-calendar`) measure 13 x 20.

Auswirkung: Both are small targets for touch and for anyone with reduced motor precision. The footer instance is the more visible failure because it ships on every single route; the calendar one comes from a third-party component and is the harder of the two to change.

Verbesserungsvorschlag: For the footer link add padding so the tap area reaches at least 24 x 24 while the icon stays 20 x 20 - `inline-flex p-1.5` with a negative margin preserves the current layout. For the calendar, either give `.rbc-button-link` a minimum size in the project stylesheet or accept it and record the exception; it is worth noting that 2.5.8 has no third-party exemption. · Aufwand: S

---

### F8 - In German, "Anmelden" means both "sign in" and "sign up for this slot"

**Kategorie:** Content
**Schweregrad:** Mittel
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #2 Übereinstimmung mit der realen Welt, #4 Konsistenz und Standards; and the writing guidance in `.claude/skills/frontend-design` - an action keeps the same name through the whole flow)
**Ort:** `/volunteer-opportunities/:id`, header, `/my-signups` · Persona: all · Viewport: all · Sprache: DE only

Beleg: `assets/2026-08-24-frontend-ux/f7-de-anmelden-ambiguity.png` against `f7-en-signin-vs-signup.png`, same page, same state, languages swapped:

- German: body copy "Melde dich an, um mitzumachen." above a button labelled "Anmelden" - while the header's "Anmelden" on the same screen means log in.
- English: "Sign in to sign up for this opportunity." above a button labelled "Sign in" - the two actions are lexically distinct and unambiguous.

The collision continues through the product: the header action is "Anmelden" (log in), the volunteer's list of registrations is "Meine Anmeldungen" (sign-ups), and the organiser's queue of incoming registrations is also "Anmeldungen".

Auswirkung: German is the default language, so the ambiguity lands on the majority of users at the exact decision point. On an offer page a logged-out visitor sees two "Anmelden" controls a few hundred pixels apart that do different things, and a logged-in volunteer reads "Melde dich an" as though they were signed out. English is clear, which shows this is a German wording problem rather than an information-architecture one.

Verbesserungsvorschlag: Reserve "Anmelden" for authentication and give the participation action its own verb. "Für diesen Zeitslot eintragen" or "Verbindlich eintragen" for the button, "Trag dich ein, um mitzumachen." for the lead, and "Meine Einsätze" for the volunteer list, which also reads better against the existing "Einsatz" vocabulary. Keep the chosen verb identical through the confirmation dialog and the resulting toast. · Aufwand: S for the strings, M once every downstream label and notification is aligned

---

### F9 - Badge tooltips cannot be dismissed and cannot be hovered

**Kategorie:** Barrierefreiheit
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (WCAG 2.1/2.2 AA, 1.4.13 Content on Hover or Focus)
**Ort:** `/profile`, `/users/:id` · Persona: all · Viewport: all · Sprache: DE/EN

Beleg: focusing a badge card renders `[role="tooltip"]` with `display: block`; pressing Escape leaves it at `display: block` (not dismissible), and its computed `pointer-events` is `none`, so the pointer cannot travel onto it (not hoverable). `components/BadgeGrid.tsx` drives it purely with `group-hover:block group-focus:block` and `pointer-events-none`, with no key handler.

Auswirkung: 1.4.13 requires hover/focus content to be dismissible, hoverable and persistent. For an earned badge the description exists *only* in the tooltip - the card itself shows just the name and the unlock date - so a magnifier user who cannot keep the trigger and the tooltip in view at once, or anyone who needs the tooltip out of the way, has no route to that text. Screen reader users are covered, since `aria-describedby` links the tooltip regardless of visibility.

Verbesserungsvorschlag: Add an Escape handler that hides the tooltip while focus stays on the card, and drop `pointer-events-none` so the pointer can move onto the tooltip without it closing. Alternatively, render the description inline on earned cards as it already is on unearned ones, which removes the dependency on hover entirely. · Aufwand: S

---

### F10 - The wizard's language-tab error indicator is colour-only and hidden from assistive tech

**Kategorie:** Barrierefreiheit
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (WCAG 2.2 A, 1.4.1 Use of Color; 1.3.1 Info and Relationships)
**Ort:** create-opportunity wizard, step 1 · Persona: Olaf · Viewport: all · Sprache: DE/EN

Beleg: `components/CreateVolunteerOpportunityModal/BasicsStep.tsx:78-83` - when the inactive language tab holds a validation error, the only signal is `<span aria-hidden="true" class="h-1.5 w-1.5 rounded-full bg-red-500" />`, a 6 px red dot with no text and no accessible equivalent. The tabs themselves are correctly marked with `aria-current="true"`, so active state is conveyed; only the error state is not.

Auswirkung: A screen reader user gets no indication that the other language tab contains an error and can only discover it by switching tabs. For a user who cannot distinguish the red dot from the surrounding grey, the same is true visually.

Verbesserungsvorschlag: Drop `aria-hidden` and give the indicator a visually hidden label such as "enthält Fehler", or extend the tab's accessible name to "English (enthält Fehler)". Adding a non-colour cue - an exclamation glyph rather than a plain dot - covers the visual side. · Aufwand: S

---

### F11 - Required-field errors are generic where upload errors are exemplary

**Kategorie:** Content
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Präferenz (informed by the writing guidance in `.claude/skills/frontend-design`: errors say what happened and how to fix it)
**Ort:** create-opportunity wizard · Persona: Olaf · Viewport: all · Sprache: DE

Beleg: submitting step 1 empty marks both fields with "Bitte ausfüllen." The upload errors on the very same step, by contrast, read "„notanimage.txt" ist kein unterstütztes Bild. Erlaubt sind JPEG, PNG und WebP." and "„huge.png" ist 3 MB groß - erlaubt sind maximal 2 MB." Both name the object, the problem and the rule.

Auswirkung: No user is blocked - the field is highlighted and focus moves there correctly - but the quality gap is noticeable within a single screen, and "Bitte ausfüllen." carries none of the guidance the upload messages do.

Verbesserungsvorschlag: Name the field and what good looks like: "Gib einen Titel ein - er erscheint in der Suche." and "Beschreibe kurz, was Freiwillige erwartet." · Aufwand: S

---

### F12 - A profile statistic packs two different concepts into one label

**Kategorie:** Content
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Präferenz
**Ort:** `/profile` · Persona: Vera · Viewport: all · Sprache: DE

Beleg: the second statistic card reads `1` above "Tag in Folge angemeldet · Anmeldeserie" - a unit ("Tag in Folge angemeldet") and a badge name ("Anmeldeserie") joined by a middot. The adjacent card is the clean "1 / Bestätigter Einsatz".

Auswirkung: The label reads as though two strings were concatenated, and it is the only card in the pair that needs a second look to parse.

Verbesserungsvorschlag: Let the label do one job: "Tage in Folge" as the unit, with "Anmeldeserie" surfaced as the badge it belongs to rather than appended. · Aufwand: S

---

### F13 - The sticky header still animates under prefers-reduced-motion

**Kategorie:** Visuelles Design
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Präferenz (WCAG 2.3.3 Animation from Interactions is Level AAA, so this is a nudge rather than an AA gap)
**Ort:** all routes · Persona: all · Viewport: all · Sprache: DE/EN

Beleg: with `prefers-reduced-motion: reduce` active, all 16 `fade-up` entrance animations are correctly suppressed (0 animated elements, down from 16) - the handling here is genuinely good. Two elements keep a movement-bearing transition: the sticky `header` and the logo `img`, both `transition: all 0.3s`, which drive the shrink-on-scroll effect.

Auswirkung: Small, and the header is the one element a motion-sensitive user cannot scroll away from. Since `transition: all` also animates size and position, the effect is movement rather than a colour fade.

Verbesserungsvorschlag: Narrow `transition: all` to the properties actually needed (`background-color`, `box-shadow`), or add the header to whatever media query already zeroes the `fade-up` animations. Narrowing the property list is worth doing regardless, since `transition: all` animates more than intended. · Aufwand: S

---

### F14 - The 404 page offers no route back into the core task

**Kategorie:** Content / UX
**Schweregrad:** Niedrig
**Konfidenz:** Bestätigt
**Einordnung:** Präferenz (adjacent to Nielsen-Norman-Heuristik #9 Fehler erkennen, diagnostizieren, beheben)
**Ort:** any unmatched route · Persona: all · Viewport: all · Sprache: DE/EN

Beleg: `/gibt-es-nicht-12345` renders "Seite nicht gefunden - Die Seite existiert nicht oder wurde verschoben. Vielleicht hat der Hund sie gefressen…" with a single action, "Zur Startseite". Page title and status messaging are otherwise correct.

Auswirkung: A 404 is usually reached from a stale or mistyped opportunity link, so the visitor almost always wanted an opportunity. Sending them to the homepage makes them restart the search from scratch.

Verbesserungsvorschlag: Add "Einsätze finden" beside the existing action, or drop the search field in directly. The joke can stay; it is on-brand and the page is otherwise clear. · Aufwand: S

---

## What is already good

Recording these so they do not get refactored away by accident:

- **Contrast holds up over photographs and gradients.** 97 text elements on `/` and 123 on `/opportunities` were measured against their real painted background, `oklch()` colours resolved through a canvas. Zero below AA, including white type over the hero.
- **Keyboard access is complete.** All 22 landing-page tab stops carry a visible focus ring (2 px `rgb(34,105,71)` outline plus a white halo, so it reads on light and dark alike). The skip link moves real focus to `<main>`.
- **Modals are done properly.** `aria-modal="true"`, `aria-labelledby`, focus moved in on open, focus trap holds across 16 tabs, Escape closes, focus returns to the opener.
- **Offline is a state, not an error.** The service worker controls the origin, a visited page reloads offline, and an offline navigation renders "Du bist offline. Sobald deine Verbindung zurück ist, laden wir die Einsätze." with a retry action, announced through a live region.
- **i18n infrastructure is disciplined.** 1357 keys in each locale with zero drift and no empty values; the five identical strings are format templates and an email address. Switching language preserves route, query string and typed input; `<html lang>`, the page title and even the web manifest (`manifest.de` / `manifest.en`) follow.
- **The create-opportunity wizard sets the standard.** `aria-invalid` plus `role="alert"` plus focus to the first invalid field; upload errors that name file, size and limit; "Adresse der Organisation übernehmen", which is exactly what WCAG 2.2's 3.3.7 Redundant Entry asks for.
- **The detail-page map avoids the usual mobile trap by construction.** `dragging`, `scrollWheelZoom`, `touchZoom` and `keyboard` are all disabled, so there is no gesture conflict with page scroll, and the container carries `role="group"` with an `aria-label` naming the address.
- **No horizontal overflow at 375 px** on any route tested, and no raw i18next keys leaked anywhere.
- **The Keycloak theme is not an afterthought** - brand-consistent, floating labels, autofocus with a visible ring, and clear reset-password copy.

## Parking Lot

- Two console errors on every page load (CSP `frame-ancestors` violation plus a 400) stem from F3's redirect URI mismatch; the noise disappears when that is fixed. Belongs to the `bugs` lens.
- The staging engagements list and admin user list expose a real personal email address (`maikhasler@proton.me`) to anyone who signs in as `olaf` or `admin` on a deliberately public test instance. Owner's own data, but worth a decision. Belongs to the `security` lens.
- The seeded admin renders as "Admin AdminAdmin", which looks like duplicated name parts in the seed data rather than a rendering fault. Belongs to the `bugs` lens.
- The language switcher button carries `aria-expanded` but no `aria-haspopup`. Valid as-is, not worth a finding.
- Every badge card is a tab stop (`tabIndex={0}` on a `role="group"` div) purely so tooltips are keyboard reachable - six extra stops on the profile page. Defensible, and it becomes moot if F9 is fixed by inlining the descriptions.
- `docs/AGENTS.md` mandates AsciiDoc for architecture documentation; this report is Markdown per the review brief and sits in a new `docs/reviews/` directory outside the arc42 tree. Worth confirming that is where such reports should live.

## Prioritized Next Steps

**Quick wins - low effort, high impact.** These four are all S and together they repair the sign-up funnel:

1. **F1** - pass `state.returnTo` at the four call sites that omit it, matching `Header.tsx`. Single highest-value change in this report.
2. **F3 (config half)** - add `/silent-renew.html` to the deployed Keycloak client's redirect URIs so it matches the checked-in realm template. Restores cross-tab SSO and token renewal, and removes the console noise.
3. **F2** - filter past slots out of "Verfügbare Zeitslots", or render them as expired and non-clickable, reusing the `findNextTimeSlot` predicate.
4. **F7** - add padding to the footer GitHub link so its target reaches 24 x 24.

**Next, still small.** F4's `lang="de"` wrapper on organisation text, F6's result count (given a total from the list endpoint), F5's "complete but not yet awarded" badge state, F10's accessible error indicator, F9's Escape handler, and the copy fixes in F11, F12 and F14.

**Larger undertakings.**

- **F8, the German sign-in vocabulary.** The string changes are trivial; the work is choosing one verb and carrying it through every button, dialog, toast, notification and email so the flow stays consistent. Worth doing deliberately rather than piecemeal.
- **F4's second half, English content for organisations.** Adding `nameEn` / `descriptionEn` touches the create and settings forms, and reaches beyond the frontend. The wizard's existing "Deutsch / English" tab, with its clear explanation of what an empty field means, is the pattern to copy.
- **A shared "is this in the past" rule.** F2 is one symptom; the organiser dashboard calendar renders the same stale slot with no past-event styling. A single predicate applied wherever slots are listed would close the class of defect rather than the instance.
