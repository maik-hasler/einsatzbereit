# Frontend & UX Review - einsatzbereit - 2026-08-24

Reviewed: https://einsatzbereit.maik-hasler.de/ (staging) - Repo commit at review time: `ee27be5`

## Executive Summary

Einsatzbereit is in noticeably better shape than most projects at this stage. The
things that usually go wrong in a volunteer marketplace have been done right here:
colour contrast passes WCAG 2.2 AA essentially everywhere I sampled (pixel-sampled,
including text over the dark hero gradients), focus indicators are visible and
context-aware on every interactive element I tabbed through, modals trap focus
correctly and close on Escape, form errors are wired up with `aria-invalid` and
`aria-describedby`, the German and English bundles are complete with route and query
state preserved across a language switch, and the PWA has a real offline story that
reads as a state rather than a failure. The dashboard's widget grid even ships a full
keyboard placement mode with live-region instructions, which satisfies WCAG 2.2's new
SC 2.5.7 (Dragging Movements). None of that is accidental.

The problems that remain are concentrated in a handful of places, and three of them
are worth fixing this week. First, the app's own Content Security Policy blocks the
`data:` URI that draws the chevron on every styled `<select>`, so dropdowns render as
plain boxes indistinguishable from text inputs. Second, the city autocomplete only
matches complete city names, so a volunteer typing "Leip" is told no matching city
exists while Leipzig opportunities sit on the page behind the dropdown - this breaks
the primary discovery path. Third, past time slots are presented as bookable: a slot
dated six days in the past appears under "Verfügbare Zeitslots" with free spots and is
offered as a selectable, non-disabled option in the signup dialog.

Two more deserve attention: the organisation switcher truncates mid-word on mobile
("Lin... schaftshilfe e.V."), which defeats the one job it has - telling you whose
name you are acting in - and the mobile dashboard's agenda calendar is clipped and
unreachable, hiding the event title on every organiser's phone.

The remaining findings are polish: terminology drifts between "Interesse bekunden" and
"Anmeldung"/"Platz", the opportunities list omits a result count that the organisations
list has, and an earned achievement badge is styled exactly like an unearned one.

## Scope & Method

**Tooling.** Chromium 141 driven by Playwright against the live staging deployment.
Only one browser engine was available in this environment, so all rendering
observations are Chromium-only; nothing here has been cross-checked in Firefox or
WebKit. The sandbox routes outbound HTTPS through a TLS-terminating proxy, which
required capping Chromium at TLS 1.2 to establish the tunnel; certificate verification
remained enabled throughout and this does not affect page rendering.

**Personas.** `vera` (volunteer), `olaf` (organiser), `admin`, plus anonymous. Note
that the documented persona table does not match staging: `vera` is a member of
Lindenauer Nachbarschaftshilfe e.V. and can reach that organisation's dashboard, and
`olaf` belongs to two organisations, not one. The review reflects what the accounts
can actually do.

**Viewports.** 375 x 812, 768 x 1024, 1440 x 900. **Languages.** German (default) and
English via the in-app switcher, compared on the opportunities list, organisations
list, help page and header on all three.

**Areas covered.** Home, opportunities list with all six filter groups and the location
autocomplete, opportunity detail (both participation types), organisations list and
profile, help, contact, imprint, terms, privacy, unsubscribe, 404; Keycloak login,
login error, forgotten password and registration; my signups, profile with
achievements, profile settings, notifications; organiser dashboard with widget edit
mode, opportunities list, the four-step create-opportunity wizard, engagements,
members, organisation settings; admin organisations, users, reports and audit log;
empty states, offline behaviour, reduced motion, and a keyboard-only pass.

**Verification approach.** Every finding below was reproduced live and, where the cause
was in the frontend, traced to the source. Several plausible-looking issues were
checked and discarded rather than reported: Escape does close the create-opportunity
modal, its focus order does match visual order once the tab cycle is read from its
start, admin's destructive actions are behind `ConfirmDialog`, the "Admin Admin Admin"
string in the users list is a name plus a role chip rendering correctly, and a
CSS-walk contrast audit that flagged 23 items turned out to be measuring a transparent
header against the wrong background - pixel sampling reduced that to zero real
failures.

**Excluded.** Backend logic, data model, infrastructure, security, CI and performance
tooling, code hygiene and dead code, per the review brief. Two features named in the
brief do not exist in the product and so could not be reviewed: saved searches / alerts
(no locale keys, no UI) and CSV export (likewise). There is also no map-based browsing
mode; the only map is the single-marker map on the opportunity detail page.

**Test data.** One interest signup was created and withdrawn, and the four
notifications it generated on olaf's account were deleted. A withdrawal confirmation
dialog was opened for evidence and cancelled with "Behalten", leaving the signup
intact. No other data was modified. Note that another tester was active on this shared
staging environment during the session; notifications from 16:10-17:17 are not mine and
were left untouched.

## Findings

### F1 - Every styled dropdown is missing its chevron because CSP blocks the icon

**Category:** UI
**Severity:** High
**Confidence:** Confirmed
**Classification:** Best Practice (WCAG 2.2 AA SC 1.3.1 / 3.2.4 Consistent
Identification; Nielsen heuristic #6 Recognition rather than recall)
**Location:** `/app/:org/dashboard/engagements`, `/app/:org/dashboard/members`,
opportunity engagement management - Persona: Olaf - Viewport: all - Language: both

Evidence: `f01-select-no-chevron.png`. Console on both pages: `Refused to load the
image 'data:image/svg+xml;charset=utf-8,...' because it violates the following Content
Security Policy directive: "img-src 'self' blob: https://api.maik-hasler.de
https://storage.maik-hasler.de"`. Computed style on the Status select:
`appearance: none`, `padding-right: 36px`, `background-image` set but blocked. Source:
`frontend/src/lib/formClasses.ts:8` builds `selectClass` with a `data:` URI background,
used in `OrgEngagementsPage.tsx`, `OrgMembersPage.tsx` and `EngagementManagementPage.tsx`.

Impact: `appearance: none` removes the browser's native dropdown arrow, and the
replacement never loads. The result is a control that looks exactly like the text input
sitting next to it - in the screenshot, "Alle Status" and "Nach Freiwilligennamen
suchen" are visually indistinguishable. Users cannot tell there is a menu to open, and
36px of padding is reserved for an icon that is never painted.

Suggested improvement: render the chevron as an inline SVG sibling positioned over the
select (the pattern already used elsewhere in the codebase) instead of a CSS
background image. Adding `data:` to `img-src` would also work but weakens the CSP for
one icon, so the inline SVG is the better trade. - Effort: S

### F2 - Organisation switcher truncates mid-word, so you cannot tell which org you are in

**Category:** UI
**Severity:** High
**Confidence:** Confirmed
**Classification:** Best Practice (Nielsen heuristic #1 Visibility of system status)
**Location:** Header org switcher, all `/app/:org/*` routes - Persona: Olaf, Vera -
Viewport: 375 px - Language: both

Evidence: `f02-org-switcher-mid-word-truncation-375.png` shows the switcher reading
"Lin... schaftshilfe e.V."; `f02-org-switcher-open-1440.png` shows the two similarly
named organisations in the open dropdown. Source: `frontend/src/lib/middleTruncateSplit.ts` splits the
name at `Math.ceil(text.length / 2)` with no regard for word boundaries, and
`Header/OrganizationSwitcher.tsx:98-108` renders the head with `truncate` and the tail
with `shrink-0 whitespace-nowrap`. For "Lindenauer Nachbarschaftshilfe e.V." (35
characters) the split lands inside "Nachbarschaftshilfe", giving head "Lindenauer
Nachbar" and tail "schaftshilfe e.V.". At 375 px the head collapses to "Lin...".

Impact: the switcher's entire purpose is to make it unambiguous whose name you are
acting under, and Olaf belongs to two organisations whose names share a prefix
("Lindenauer Nachbarschaftshilfe e.V." and "Lindenauer Tierschutzverein e.V."). A
fragment like "schaftshilfe e.V." is not a name a user recognises, and publishing an
opportunity under the wrong organisation is not a cheap mistake. The `title` attribute
carries the full name but is unavailable on touch.

Suggested improvement: split at the last word boundary at or before the midpoint so the
tail always begins a whole word, and prefer keeping the distinguishing words. At 375 px,
dropping to the avatar plus a short label with the full name in the dropdown would be
more honest than a fragment. - Effort: S

### F3 - Time slots that are already in the past are offered for signup

**Category:** UX
**Severity:** High
**Confidence:** Confirmed
**Classification:** Best Practice (Nielsen heuristic #5 Error prevention)
**Location:** `/volunteer-opportunities/:id` (Erste-Hilfe-Kurs) - Persona: anonymous,
Vera, Admin - Viewport: all - Language: both

Evidence: `f03-past-timeslot-listed-as-available.png` and
`f03-signup-modal-slot-choice.png`. On 2026-08-24 the section headed "Verfügbare
Zeitslots" lists `18.08.2026, 09:00-17:00 - 19 Plätze frei` with identical styling to
the future slot `04.09.2026`. Signed in as a user who has not yet signed up, the past
slot is an enabled `<button>` (`disabled: false`), and the signup dialog's slot
dropdown offers it as a selectable option with `aria-disabled=null`. The header's
aggregate "38 Plätze frei" counts both slots. Source:
`VolunteerOpportunityDetailPage.tsx:825` maps `opportunity.timeSlots` with no
date filter anywhere in the file.

Impact: a volunteer can select and submit a signup for a shift that happened six days
ago. Even where the backend rejects it, the user has been walked into an error the
interface invited. The inflated free-spot count also misrepresents availability on the
card, the list and the detail header.

Suggested improvement: partition slots into upcoming and past in the frontend; render
past ones disabled and visually de-emphasised under a "Vergangen" label, or omit them
entirely, and exclude them from the aggregate capacity figure. - Effort: S

Suspected backend cause: the API returns all time slots regardless of date. That is
reasonable for an organiser view, so the filtering belongs in the volunteer-facing
frontend rather than in the endpoint. Not changed as part of this review.

### F4 - City autocomplete only matches complete city names and reports a dead end

**Category:** UX
**Severity:** High
**Confidence:** Confirmed
**Classification:** Best Practice (Nielsen heuristic #9 Help users recognise, diagnose
and recover from errors; #1 Visibility of system status)
**Location:** `/opportunities`, Standort filter - Persona: all - Viewport: all -
Language: both

Evidence: `f04-city-prefix-no-match.png` and `f04-city-full-name-match.png`. Typing
"Leip" produces "Keine passende Stadt gefunden." while a Leipzig opportunity is visible
on the page behind the panel. Network trace: `GET /v1/maps/cities?Q=Leip` returns `[]`,
`GET /v1/maps/cities?Q=Leipzig` returns
`[{"label":"Leipzig","latitude":51.34,"longitude":12.37}]`. Same for "Berlin" and
"Markkleeberg" only when spelled in full. The input is marked
`role="combobox" aria-autocomplete="list"` and labelled "Stadt eingeben...", so it
presents itself as a type-ahead.

Impact: this is the main way a volunteer narrows opportunities to their area, and it
fails for every partial entry, which is how people use a type-ahead. The message is
phrased as a definitive negative result, so a user reasonably concludes there is nothing
near them and leaves. The radius filter stays disabled until a city is chosen, so the
whole location facet is unreachable without an exact spelling.

Suggested improvement: while the query is shorter than the shortest matchable term, show
a neutral prompt ("Tippe den vollständigen Ortsnamen") rather than a no-results verdict,
and keep the empty state non-terminal by offering "In meiner Nähe" as the adjacent
action. - Effort: S for the frontend copy and state handling.

Suspected backend cause: `/v1/maps/cities` appears to do exact rather than prefix
matching. A prefix-capable geocoder query would fix this properly, but that is a backend
change and out of scope here.

### F5 - Mobile agenda calendar is clipped, hiding the event title

**Category:** UI
**Severity:** Medium
**Confidence:** Confirmed
**Classification:** Best Practice (WCAG 2.2 AA SC 1.4.10 Reflow)
**Location:** `/app/:org/dashboard`, Kalender widget - Persona: Olaf - Viewport: 375 px -
Language: both

Evidence: `f05-mobile-agenda-clipped.png` shows the TERMIN column cut to
"Erste-Hilfe-Ku". Measured chain: `.rbc-agenda-content` is 307 px wide with
`scrollWidth: 480` and `overflow-x: visible`; the `.rbc-agenda-table` inside is 480 px
and extends to x=514, past the container's right edge at 341. The document itself
reports no horizontal overflow, meaning an ancestor clips the spill, so the hidden
column cannot be scrolled to.

Impact: the calendar helpfully falls back to Agenda view on small screens, but the one
column that identifies the event is the one truncated, and there is no way to reach it.
An organiser checking their phone sees times without knowing which opportunity they
belong to.

Suggested improvement: give `.rbc-agenda-content` `overflow-x: auto` so the table can be
scrolled, or better, replace the agenda table with a stacked card list at this breakpoint
where the title leads. - Effort: S

### F6 - The opportunities list gives no result count, while the organisations list does

**Category:** UX
**Severity:** Medium
**Confidence:** Confirmed
**Classification:** Best Practice (Nielsen heuristic #1 Visibility of system status; #4
Consistency and standards - the project's own pattern one route away)
**Location:** `/opportunities` vs `/organizations` - Persona: all - Viewport: all -
Language: both

Evidence: `f06-opportunities-no-result-count.png` and
`f06-organizations-has-result-count.png`. `/organizations` renders a visible "2
Organisationen gefunden" ("2 organizations found" in EN). `/opportunities` renders only
a visually hidden `<h2>Suchergebnisse</h2>` and nine cards followed by "Mehr laden", with
no count anywhere in the page text.

Impact: after applying filters, users cannot tell whether they are looking at nine
results or the first nine of two hundred, so "Mehr laden" gives no sense of how much is
left. It also makes filters hard to evaluate - you cannot see that a filter cut the set
from 40 to 3. The organisations page already establishes the right pattern.

Suggested improvement: render the same "N Einsätze gefunden" line above the grid, and
keep it live-updating as filters change so the count doubles as filter feedback. -
Effort: S

### F7 - "Interesse bekunden" turns into "Anmeldung" and a "Platz" that does not exist

**Category:** Content
**Severity:** Medium
**Confidence:** Confirmed
**Classification:** Best Practice (Nielsen heuristic #2 Match between system and the real
world, #4 Consistency and standards; `/frontend-design` writing guidance: an action keeps
the same name through the whole flow)
**Location:** `/volunteer-opportunities/:id` for interest-based opportunities,
`/my-signups` - Persona: Vera - Viewport: all - Language: DE

Evidence: `f07-interest-cta-modal.png` and `f07-withdraw-confirm-mentions-platz.png`.
The call to action is "Interesse bekunden" and the modal is titled "Interesse bekunden".
Immediately after submitting, the status card is headed "Deine Anmeldung" with the body
"Die Organisation prüft deine Anmeldung". The withdrawal dialog for the interest-based
"Blutspendetermin begleiten" reads "Dein **Platz** für "Blutspendetermin begleiten"
wird wieder **freigegeben**" - but this opportunity has no seats at all. Its capacity is
`notApplicable` with reason `interest` (`OpportunityCard.tsx:61-68`), which is exactly
why its card shows the word "Interessenbekundung" where other cards show a spot count.

Impact: three different words describe one thing across a four-step flow, and the
confirmation promises to release a seat the system never allocated. For a user deciding
whether withdrawal is consequential, that is actively misleading. The German noun
"Anmeldung" (a signup) also collides with the verb "Anmelden" (to sign in) used in the
header and the login prompt, which compounds the ambiguity for new users.

Suggested improvement: keep the interest vocabulary end to end - "Deine
Interessenbekundung", "Die Organisation prüft deine Interessenbekundung", and a
withdrawal dialog that says the interest will be withdrawn rather than a seat released.
Branch the confirmation copy on participation type rather than sharing one string. -
Effort: S

### F8 - An earned achievement is styled exactly like an unearned one

**Category:** Visual Design
**Severity:** Medium
**Confidence:** Confirmed
**Classification:** Best Practice (Nielsen heuristic #1 Visibility of system status; #4
Consistency and standards)
**Location:** `/profile`, Abzeichen section - Persona: Vera - Viewport: 1440 px -
Language: both

Evidence: `f08-earned-badge-styled-as-unearned.png`. "Erster Schritt" is complete - its
progress bar is full and it reads "1 von 1" - yet it keeps the grey icon, grey title and
plain border of the unearned badges beside it. "Früheinsteiger", also earned, gets an
entirely different and much stronger treatment: green icon on a tinted circle, dark
title, green border, and "Freigeschaltet: 24.08.2026" in place of a progress bar.

Impact: two earned badges render in two different visual languages, and the one tied to
the user's first confirmed engagement - the moment worth celebrating - is the one that
looks unearned. The reward loses its function at exactly the point it should land.

Suggested improvement: drive the earned treatment off a single completion predicate so
any badge at 100 percent gets the highlighted styling and the "Freigeschaltet" date. -
Effort: S

Related and lower priority: showing "100 Einsätze - 1 von 100" to a user with one
engagement produces a near-empty progress bar that reads as discouraging. Consider
revealing far-off tiers only once the previous tier is earned.

### F9 - Notifications do not link to what they are about

**Category:** UX
**Severity:** Medium
**Confidence:** Confirmed
**Classification:** Best Practice (Nielsen heuristic #7 Flexibility and efficiency of use)
**Location:** Header notification panel - Persona: Olaf - Viewport: all - Language: both

Evidence: `f09-notifications-not-linked.png`. Each list item contains exactly two
controls: a toggle whose accessible name begins "Ungelesen..." and a delete button
labelled `Löschen: <notification title>`. No item contains an `<a>`; measured
`isLink: false` across all sampled entries.

Impact: "Neue Anmeldung eingegangen für Futterspenden-Sammlung" tells an organiser
something needs attention but leaves them to navigate to the right organisation, then
Anmeldungen, then find the row by hand. With eleven unread notifications spanning two
organisations, that is a lot of manual work for a panel whose job is to shorten it.

Suggested improvement: make the notification body a link to the relevant engagement or
opportunity, and mark it read on activation. - Effort: M

### F10 - The capacity chip sometimes reports capacity and sometimes reports participation type

**Category:** UI
**Severity:** Medium
**Confidence:** Confirmed
**Classification:** Best Practice (Nielsen heuristic #4 Consistency and standards)
**Location:** `/opportunities`, `/` (latest opportunities) - Persona: all - Viewport: all -
Language: both

Evidence: `f06-opportunities-no-result-count.png`. In one grid, the chip in the same
position reads "38 Plätze frei" on one card and "Interessenbekundung" on the next.
Source: `OpportunityCard.tsx:54-78` returns the participation-type label
("Interessenbekundung", "Keine Plätze") from the same `capacityChip` function that
returns spot counts. Separately, the participation-type chip in the top-right corner is
rendered only when `item.participationType === "ScheduledSlots"`
(`OpportunityCard.tsx:149`), so "Zeitslots" appears top-right while "Interessenbekundung"
appears inline lower down.

Impact: the same visual slot answers two different questions, so the grid cannot be
scanned down a column - the reader has to parse each chip's meaning individually. The
two participation types are also announced in two different places with different visual
weight, which makes them look like unrelated attributes rather than two values of one
property.

Suggested improvement: always render the participation type in the top-right chip for
both values, and reserve the capacity chip for capacity, using a neutral dash or omitting
it when capacity does not apply. - Effort: S

### F11 - The same opportunity shows three different capacity figures across dashboard widgets

**Category:** UX
**Severity:** Low
**Confidence:** Confirmed
**Classification:** Best Practice (Nielsen heuristic #2 Match between system and the real
world)
**Location:** `/app/:org/dashboard` - Persona: Olaf, Vera - Viewport: 1440 px -
Language: both

Evidence: `f11-dashboard-capacity-numbers.png`. "Bevorstehende Einsätze" shows
"Erste-Hilfe-Kurs - 04.09.2026, 09:00 - 2/40 Anmeldungen". The calendar below shows the
same course as "Erste-Hilfe-Kurs 1/20" on two separate dates. The detail page reports
"38 Plätze frei" and "2 Zeitslots".

The arithmetic reconciles - two slots of 20 with one signup each - so nothing here is
wrong. The problem is that nothing on screen says one figure is per slot and the other is
the total, so a reader comparing the two widgets sees a contradiction and has to work out
which is which.

Suggested improvement: label the calendar entries per slot (for example "1/20 pro
Zeitslot") or show the same aggregate in both places. - Effort: S

### F12 - The sticky header animation ignores prefers-reduced-motion

**Category:** Accessibility
**Severity:** Low
**Confidence:** Confirmed
**Classification:** Best Practice (project's own convention - `motion-reduce:` variants
are used throughout the codebase; WCAG 2.2 SC 2.3.3 is AAA, so this is not an AA failure)
**Location:** Global header, all routes - Persona: all - Viewport: all - Language: both

Evidence: with `prefers-reduced-motion: reduce` active, the `fade-up` keyframe animations
correctly disappear (56 animated elements drop to 40, `animationName` list empties), but
the header retains `class="sticky top-0 z-40 transition-all duration-300 ..."` with no
`motion-reduce:` variant, as does the logo image. `transition-all` covers layout and
transform properties, so the header and logo resize animation on scroll still plays.

Impact: small, and limited to users who have asked for less motion. It stands out mainly
because the rest of the codebase handles this correctly, including
`SingleMarkerMap`, the card hover transforms and the chevron rotations.

Suggested improvement: add `motion-reduce:transition-none` to the header and logo, and
prefer explicit transition properties over `transition-all`. - Effort: S

### F13 - Keyboard widget placement works but is not discoverable before you commit to it

**Category:** Accessibility
**Severity:** Low
**Confidence:** Confirmed
**Classification:** Preference (the underlying requirement, WCAG 2.2 AA SC 2.5.7 Dragging
Movements, is met)
**Location:** `/app/:org/dashboard` edit mode - Persona: Olaf - Viewport: 1440 px -
Language: both

Evidence: `f13-dashboard-edit-mode.png` and `f13-dashboard-keyboard-placing-mode.png`.
The grip is a focusable button named `"Einsatz erstellen" verschieben oder Größe
ändern`. It carries no `aria-describedby` and no visible hint. Only after pressing
Enter does a live region announce "Spalte 1, Zeile 1. Klicke auf eine Rasterzelle (oder
nutze die Pfeiltasten), um hier die erste Ecke von "Einsatz erstellen" festzulegen.
Escape zum Abbrechen." Arrow keys are ignored until that mode is entered
(`useWidgetPlacement.ts:148` returns early unless `placingKey` matches).

This is a genuinely good implementation - the drag alternative exists, it is announced,
and Escape restores the original position. The only gap is that a keyboard user has to
guess that Enter starts a placement mode, because pressing arrows first does nothing and
gives no feedback.

Suggested improvement: add an `aria-describedby` hint on the grip naming the Enter step,
and show the same hint visually in edit mode. - Effort: S

### F14 - Widget content is hard to read in dashboard edit mode

**Category:** Visual Design
**Severity:** Low
**Confidence:** Confirmed
**Classification:** Preference
**Location:** `/app/:org/dashboard` edit mode - Persona: Olaf - Viewport: 1440 px -
Language: both

Evidence: `f13-dashboard-edit-mode.png`. Widget bodies are rendered at low opacity over
the tinted grid overlay, so "Erfordert deine Aufmerksamkeit / 2 / Ausstehende
Anmeldungen" and the calendar contents wash out. Because the whole grid is tinted the
same green, tile boundaries are also harder to read than in the normal view.

Impact: rearranging widgets is exactly the moment you need to recognise them, and the
edit affordance currently obscures the content it is arranging.

Suggested improvement: keep widget content at full opacity and express edit mode through
the tile chrome instead - a dashed outline, the grip and the remove control - reserving
the tint for the empty grid cells that are valid drop targets. - Effort: S

### F15 - Notification panel header actions crowd the title

**Category:** UI
**Severity:** Low
**Confidence:** Confirmed
**Classification:** Preference
**Location:** Header notification panel - Persona: Olaf - Viewport: 1440 px -
Language: DE

Evidence: `f09-notifications-not-linked.png`. "Alle als gelesen markieren" and "Gelesene
löschen" each wrap onto two lines inside the panel header, sitting tight against the
"Benachrichtigungen" heading with no separation. German strings are longer than the
English ones this layout appears to have been sized for.

Suggested improvement: move both actions to a single row beneath the heading, or into an
overflow menu, so the header holds one line at any string length. - Effort: S

### F16 - Mobile organisation tab bar scrolls with no affordance

**Category:** UI
**Severity:** Low
**Confidence:** Confirmed
**Classification:** Preference (Nielsen heuristic #6 Recognition rather than recall)
**Location:** `/app/:org/*` tab bar - Persona: Olaf - Viewport: 375 px - Language: both

Evidence: `f16-mobile-tab-bar-cut.png`. The nav is `flex gap-1 overflow-x-auto` with
`scrollWidth: 474` against `clientWidth: 343`. "Mitglieder" sits at x=406, entirely off
screen. The bar is swipeable so nothing is unreachable, and "Einstellungen" being cut
mid-word does hint that more exists.

Suggested improvement: add a fade or shadow at the scrolling edge so the cut is
unambiguous rather than incidental. - Effort: S

### F17 - Admin tools carry the full marketing hero

**Category:** Visual Design
**Severity:** Low
**Confidence:** Value judgement
**Classification:** Preference (Nielsen heuristic #8 Aesthetic and minimalist design)
**Location:** `/administration/*` - Persona: Admin - Viewport: 1440 px - Language: both

Evidence: `f17-admin-marketing-hero.png`. "Nutzer:innen" renders in the same 72 px
display face over the same dark green gradient used on the public landing pages,
consuming roughly 280 px before any content. The page beneath it is a dense operational
list of four users with block and promote controls.

Impact: no functional harm, but it sets the wrong register. Administration is a
high-frequency utility surface, and the hero pushes the working area below the fold while
signalling "marketing page" to someone doing moderation work.

Suggested improvement: use a compact page header for `/administration/*` and the org app,
keeping the display hero for public, acquisition-facing routes. - Effort: S

### F18 - Keycloak login: error placement implies a username problem (FTL template, not React)

**Category:** Content
**Severity:** Low
**Confidence:** Confirmed
**Classification:** Preference
**Location:** Keycloak login page - Persona: anonymous - Viewport: 1440 px - Language: DE

Evidence: `f18-keycloak-login-error.png`. This finding concerns the custom Keycloak FTL
theme under `keycloak/`, not the React app.

The accessibility of this page is correct and worth recording: the error is
`<span id="input-error" class="form-error" aria-live="polite">`, both fields carry
`aria-invalid="true"` and `aria-describedby="input-error"`, and the message is the page's
only live region. Nothing needs fixing there.

The presentation is what jars. "Benutzername oder Passwort stimmt nicht." is rendered
directly beneath the username field and above the password field, so despite covering
both credentials it reads as a username-specific error. Both fields do get red borders,
which partly compensates.

Separately, the "KONTO" eyebrow above the "Anmelden" heading carries no information -
it labels the page as an account page on a page that already says "Anmelden".

Suggested improvement: move the combined error above the field group, directly under the
heading, so its scope matches its placement; drop the eyebrow. - Effort: S

### F19 - The interest message field has no character counter, unlike comparable fields

**Category:** UI
**Severity:** Low
**Confidence:** Confirmed
**Classification:** Preference (Nielsen heuristic #4 Consistency and standards)
**Location:** Signup modal for interest-based opportunities - Persona: Vera -
Viewport: all - Language: both

Evidence: `f07-interest-cta-modal.png`. The required "Nachricht" textarea shows no
counter. The create-opportunity wizard shows "0 / 150" and "0 / 2000" on its text fields,
and the report-content modal shows "0/1000" on its optional details field.

The field is otherwise well built: a persistent label, a helpful placeholder
("Beschreibe kurz, warum du dich engagieren möchtest..."), and on empty submit a
message "Bitte gib eine Nachricht ein." wired up with `aria-invalid` and
`aria-describedby="sign-up-message-error"`.

Suggested improvement: add the same counter used by the other two forms. - Effort: S

## What is working well

Worth recording explicitly, because these are the areas that usually generate findings
and did not:

- **Colour contrast.** Pixel-sampled across six anonymous and four authenticated pages
  using exact CSS foreground values against rendered background pixels, so gradients and
  imagery were measured as painted rather than as declared. Zero real failures.
- **Focus management.** Every interactive element sampled carried a visible 2 px outline
  whose colour adapts to its background. Modal focus is trapped, moves to the first
  field on open, follows visual order, and Escape closes.
- **Form validation.** `aria-invalid` and `aria-describedby` are wired correctly in the
  create-opportunity wizard, the signup modal and the Keycloak theme. Success is
  announced through `role="alert"`.
- **Destructive-action copy.** "Anmeldung zurückziehen?" explains the consequence and
  its reversibility, and offers "Behalten" / "Ja, zurückziehen" rather than Yes/No.
  Admin block, promote, shadow-delete and restore all route through `ConfirmDialog`.
  "Als Entwurf speichern" is disabled with a sentence explaining exactly what is missing
  (`context-create-opportunity-wizard.png`).
- **Upload errors.** Rejecting `bad.txt` produced "bad.txt ist kein unterstütztes Bild.
  Erlaubt sind JPEG, PNG und WebP." - names the file, the problem and the fix.
- **i18n.** Route and query string survive a language switch, `<html lang>` updates, no
  raw keys surfaced anywhere, and both bundles are complete. German strings caused no
  layout breakage: zero horizontal overflow at 375, 768 and 1440 across all pages tested.
- **PWA.** Language-specific manifest, `theme_color` matching the brand `#2d8a5e`,
  192/512 and maskable icons, screenshots, and a registered service worker. Offline on a
  visited route shows "Du bist offline / Sobald deine Verbindung zurück ist, laden wir
  die Einsätze" with a retry - a state, not an error (`context-offline-state.png`) - and
  an unvisited static route still renders fully from the shell.
- **Empty states.** "Keine Einsätze gefunden. Passe deine Filter an oder setze sie
  zurück." with a reset button (`context-empty-search-state.png`); "Keine gemeldeten
  Inhalte. Alles erledigt."; and a profile prompt that explains why completing it helps.
- **Landing page.** A coherent visual direction that reads as a decision rather than a
  template: one deep-green brand field, a single warm accent, organic blob shapes and a
  wave divider used consistently across hero sections (`context-home-desktop-de.png`).

## Parking Lot

- Silent SSO probe produces a console error on every page load: a 400 from
  `/protocol/openid-connect/auth` plus `Refused to frame 'https://login.maik-hasler.de/'
  because an ancestor violates ... frame-ancestors 'self'`. User-invisible but constant
  noise. Belongs to the `bugs` lens.
- `splitForMiddleTruncation` has unit tests but none covering a split landing inside a
  word, which is how F2 survived. Belongs to `test-gaps`.
- The documented staging persona table (README) does not match reality: `vera` has
  organisation membership and dashboard access, `olaf` belongs to two organisations.
  Belongs to `docs-drift`.
- Saved searches / alerts and CSV export are described in the review brief but do not
  exist in the product. Belongs to `docs-drift` or product backlog.
- `docs/reviews/` did not exist before this report; `docs/` is otherwise arc42 AsciiDoc.
  If Markdown review reports become routine, worth a line in `docs/AGENTS.md`.

## Prioritised Next Steps

**Quick wins - low effort, high impact**

1. **F1** - swap the `data:` URI chevron for an inline SVG. One file
   (`lib/formClasses.ts`), restores the dropdown affordance everywhere. (S)
2. **F3** - filter or disable past time slots in the detail page and exclude them from
   the aggregate capacity. Removes an error the UI currently invites. (S)
3. **F2** - split the org name at a word boundary. One function
   (`lib/middleTruncateSplit.ts`), plus a test for the mid-word case. (S)
4. **F4** - reword the location empty state so a short query reads as "keep typing"
   rather than "nothing found". Frontend-only mitigation of a backend limitation. (S)
5. **F6** - add the result count to `/opportunities`, matching `/organizations`. (S)
6. **F8** - drive the earned badge treatment off completion so "Erster Schritt" is
   celebrated. (S)

**Larger undertakings**

7. **F7** - work the interest vocabulary through the whole flow, branching confirmation
   copy on participation type. Touches copy, locale files and the confirm dialog, and
   needs a terminology decision first: settle whether "Anmeldung" covers both
   participation types or only scheduled slots, then apply it consistently. (M)
8. **F9** - make notifications navigable to their subject. Needs a target route per
   notification type. (M)
9. **F5 / F14 / F17** - a pass over the organiser dashboard's small-screen and edit-mode
   presentation: agenda view at 375 px, edit-mode legibility, and the page-header
   register for internal tools. (M)
10. **F10** - decide what the card's two chips mean and apply it uniformly. Small in
    code, but worth doing alongside the F7 terminology decision since both hinge on how
    participation types are presented. (S once the decision is made)
