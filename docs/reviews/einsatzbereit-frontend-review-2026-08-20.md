# Frontend & UX Review - einsatzbereit - 2026-08-20

Reviewed: https://einsatzbereit.maik-hasler.de/ - Commit (if known): `main` at `068a2f5` (as of review start, 2026-08-20)

## Executive Summary

Einsatzbereit's live product is well above the bar of a typical AI-reviewed side project: the visual identity (the organic-blob hero, the consistent `#2d8a5e` brand green, the confident display type on section headers) reads as a deliberate design decision rather than a template default, and it holds up unchanged across German/English and across the three personas. Several details that are easy to skip went in anyway - a locale-aware PWA manifest that swaps itself live when the language toggle is used, an offline state that reads as a calm "you're offline, we'll reload when you're back" rather than a browser error, upload-validation errors that name the exact file and the exact limit it broke, and visible, high-contrast keyboard focus rings everywhere this review tabbed through. None of that is required to ship a volunteering platform; all of it is exactly what a UX review is supposed to be checking for, and it was already there.

Set against that, five things are worth the team's attention, roughly in order of user impact: (1) one live opportunity listing ("Wir suchen Helfer:innen") carries literal placeholder content - `xxx` as its description and a mechanically bad English title ("We search helper") - on the homepage, the search list, its own detail page, and the organization profile that owns it; (2) a Content-Security-Policy setting blocks the browser's own dropdown-arrow icon on at least two plain HTML `<select>` elements in the organizer app, so those controls render with no visible affordance that they are dropdowns at all; (3) the Administration section's sub-navigation overflows the 375px viewport with the last tab ("Audit-Log") clipped and no scroll indicator, hiding it from mobile admins; (4) a notification promising a pending organization invite led, when clicked, to a page with no trace of that invite and no explanation; and (5) the organizer dashboard was observed once, reproducibly on that occasion, stuck showing five skeleton placeholders for 15+ seconds after every underlying API call had already returned 200 - though two other attempts loaded normally in 2-3 seconds, so this reads as an intermittent loading-state bug rather than a hard failure.

None of the five block registration, browsing, or signing up as a volunteer - the core flow is solid - but #1 sits on the most-viewed page in the product, and #2-#4 are the kind of paper-cut a first-time organizer or admin hits in their first session.

## Scope & Method

**Personas:** anonymous visitor, Vera (`vera`/`vera123`, volunteer), Olaf (`olaf`/`olaf123`, volunteer + organizer of "Lindenauer Nachbarschaftshilfe e.V."), Admin (`admin`/`admin123`, platform admin). All three staging test accounts as documented in the project README.

**Viewports:** 375px (mobile), 768px (tablet), 1440px (desktop). Full sweeps were run at all three for the public surface, the opportunity detail page, and the Administration section; the authenticated volunteer/organizer flows were swept at 375px and 1440px, with 768px spot-checked via the same responsive components already covered elsewhere.

**Languages:** German (default) and English (via the header language toggle, which was confirmed to update `<html lang>`, the PWA manifest link, and preserve the current route/scroll position without a navigation).

**Method:** a real Chromium browser (Playwright, driven from throwaway scripts per this repo's `/live-verify` fallback recipe - no dedicated browser-automation MCP tool resolved in this session) against the live URL above, not a static HTML fetch. Console and network activity was logged on every page as a secondary signal, not a dedicated bug hunt (that's `einsatzbereit-review`'s `bugs` lens). One recurring, non-visible console error was filtered out throughout: an OIDC silent-SSO probe (`prompt=none`) that legitimately 400s and trips a CSP `frame-ancestors` warning on every anonymous page load - it has no visible effect on the page and is exactly the kind of pure-code finding the `bugs` lens, not this review, should evaluate.

**Only one browser engine was available** (Chromium via the sandboxed Playwright launch) - no Firefox/WebKit cross-check was performed.

**Accessibility method note:** this review did not drive a screen reader end-to-end; it spot-checked keyboard-only navigation (tab order, focus-ring visibility, Escape/focus-trap behavior on one modal) and reviewed the DOM/CSS for patterns automation can't catch (e.g. Finding F2 below is a CSP policy blocking a browser-native icon - axe-core doesn't evaluate that, and it's not a jsx-a11y-catchable pattern either, so it's a legitimate gap for this review to fill rather than a duplicate of the existing `AccessibilityTests.cs` suite).

**Two features named in the review brief do not exist in the current build** and were therefore not testable: volunteer-side "saved searches / alerts", and an admin-side organization-verification action (the Administration → Organizations page lists and can hide/unhide organizations, but there is no verify/unverify control, and no `verif*` concept anywhere in the frontend source). Both are noted here for transparency rather than reported as defects - they are unbuilt, not broken.

**Test-data caveat:** staging's `vera`/`olaf`/`admin` accounts are shared, public, and regularly exercised by other reviewers, CI, and the repo's own automated `lens` routine (see root `AGENTS.md`). Several sign-up records under Vera's "Past" tab carry raw QA notes ("Testnachricht fuer Review - bitte ignorieren"). That's pre-existing account debris the review inherited, not something this pass created, and it made a couple of otherwise-interesting data states (e.g. why an org-invite notification pointed at nothing, F4) impossible to attribute with full certainty - flagged as such below rather than silently dropped.

**Pages covered:** Home, Opportunities list (+ filters, mini-calendar), Opportunity detail (+ map), Organizations directory, Organization profile, Login/Register (Keycloak), 404, Contact/Help/Imprint/Privacy/Terms, Vera's My-Sign-ups (current + past), Profile + Achievements, Profile Settings, Notifications, sign-up modal, Olaf's Org Dashboard, Create-Opportunity wizard (incl. upload validation), Org Settings, Org Members (+ invite), Org Opportunities, Org Engagements, Admin Organizations/Users/Reports/Audit-Log, PWA offline behavior (visited + unvisited routes).

**Excluded per brief:** backend/infrastructure/data-model/CI/performance/security/dead-code topics (covered by `einsatzbereit-review`'s other lenses) and diff/PR review (covered by `.claude/skills/self-review`).

## Findings

### Content

#### F1 - Live placeholder content on a real, published opportunity
**Kategorie:** Content
**Schweregrad:** Hoch
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (explicitly named in this review's own brief: "Platzhaltertext" is in scope; matches CONTRIBUTING.md's expectation of production-ready copy)
**Ort:** Home page "Gerade veröffentlicht" section, `/opportunities`, `/volunteer-opportunities/{id}`, `/organizations/{id}` (org profile), Olaf's `/app/{orgId}/dashboard/opportunities` · Persona: anonymous, Vera, Olaf · Viewport: 375/768/1440 · Sprache: DE and EN

Beleg: `home-desktop-de-DE.png`, `opportunities-list-desktop-de-DE.png`, `opp-detail-desktop-de-DE.png`, `opp-detail-desktop-en-US.png`, `org-profile-desktop-de-DE.png`, `olaf-org-opportunities.png`. Code: the opportunity's title/description are organizer-authored fields (`CreateVolunteerOpportunityModal`'s per-locale "Deutsch"/"English" tabs), not platform strings.

Auswirkung: The opportunity titled "Wir suchen Helfer:innen" shows the literal description `xxx` in German and English, and its English title is the mechanically-translated "We search helper" rather than idiomatic copy. This is organization-authored content (the org itself typed it into the create-opportunity form), not a platform bug - but it is live, published, and currently the very first card a first-time visitor sees on the homepage in both languages. It reads as broken or abandoned software regardless of who typed it, which is exactly the kind of thing a professional platform should catch before or shortly after publish.

Verbesserungsvorschlag: Frontend-side, two independent, non-destructive options are available without touching backend logic: (a) a lightweight publish-time nudge/warning when a description is under some sane length or matches a placeholder-like pattern (`xxx`, `lorem`, `test`, `asdf`, ...), surfaced in the same inline-validation style already used elsewhere in this exact modal (see F1's sibling findings on upload validation below, which show this pattern already exists and reads well); (b) since this happens to be the review's own seed/demo organization's content, simply editing or unpublishing it removes the live evidence immediately. · Aufwand: S (for the demo-data fix) / M (for a publish-time content-quality nudge)

<em>Not a frontend defect - the content itself was authored by the organizing account, not generated by the platform. Included here per the review's own scope note that placeholder text is explicitly in scope regardless of source.</em>

---

### UI

#### F2 - Content-Security-Policy blocks the native `<select>` dropdown icon, leaving some dropdowns with zero visual affordance
**Kategorie:** UI
**Schweregrad:** Hoch
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (WCAG 2.2 SC 1.4.11 Non-text Contrast / component recognizability; Nielsen-Norman-Heuristik #4 Konsistenz und Standards - every other dropdown in the product uses a custom, visible chevron via `ChevronDownIcon`, see `LanguageSelector.tsx`, `FilterDropdown.tsx`)
**Ort:** Olaf's Org Members page ("Einladen als" role select) and Org Engagements page ("Alle Status" filter select) · Persona: Olaf · Viewport: 1440 (confirmed; layout suggests the same on 375/768) · Sprache: DE

Beleg: `olaf-org-members.png`, `olaf-org-engagements.png`. Console evidence on both pages: `Refused to load the image 'data:image/svg+xml;charset=utf-8,...' because it violates the following Content Security Policy directive: "img-src 'self' blob: https://api.maik-hasler.de https://storage.maik-hasler.de"`. The blocked data-URI is a plain chevron-down glyph, almost certainly the Tailwind Forms plugin's default `<select>` background-image icon.

Auswirkung: These two controls are native HTML `<select>` elements, not the app's own `FilterDropdown`/`Dropdown` component (which render their chevron as an inline SVG and are unaffected). Because the CSP's `img-src` directive has no `data:` source, the browser silently drops the select's background-image chevron and the control renders as a plain bordered box with text and nothing else - visually indistinguishable from a static label/tag. A user has no visual cue that "Einladen als: Mitglied" or "Alle Status" are clickable, only that they happen to still work if clicked. This is exactly the class of bug that neither axe-core (no missing-icon check) nor `eslint-plugin-jsx-a11y` (a static-analysis tool, blind to a runtime CSP policy) would catch - which is also why it slipped past the existing automated suites this review is explicitly told not to duplicate.

Verbesserungsvorschlag: Either (a) add `data:` to the CSP's `img-src` (the smallest, but broadest, fix - re-check whether that's an acceptable relaxation for the rest of the policy), or (b) the more targeted and more consistent fix: replace these two native `<select>` elements with the project's own `Dropdown.tsx` component, which frontend/AGENTS.md already documents as "the one real listbox in this repo" for exactly this use case - that would also resolve the inconsistency noted under Konsistenz above. · Aufwand: S

#### F3 - Admin sub-navigation overflows on mobile with the last tab clipped and no scroll indicator
**Kategorie:** UI
**Schweregrad:** Hoch
**Konfidenz:** Bestätigt
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #6 Wiedererkennen statt Erinnern, and #4 Konsistenz - this exact "content extends past the visible area with no affordance" problem was already identified and fixed elsewhere in this codebase via `useScrollFade`, see `NotificationDropdown.tsx`'s own code comment referencing issue #2062, but the same treatment was not applied here)
**Ort:** `/administration/*` sub-nav tab strip (Organisationen / Nutzer:innen / Meldungen / Audit-Log) · Persona: Admin · Viewport: 375 · Sprache: DE

Beleg: `admin-orgs-mobile.png` - the tab strip is cut off mid-word at "Au" with the viewport edge, no fade, shadow, or arrow signalling there's a fourth tab. The row is very likely `overflow-x-auto` and swipeable, but nothing on screen says so.

Auswirkung: A platform admin on a phone has no visible way to discover that an "Audit-Log" section exists at all unless they happen to swipe the tab strip sideways. Given the Audit-Log is explicitly the record of "privileged administration actions" (per its own on-page description), making it effectively invisible on mobile undercuts exactly the oversight function it exists for.

Verbesserungsvorschlag: Apply the same `useScrollFade` treatment (or equivalent - a trailing gradient fade, or simply wrapping the tabs so they don't require horizontal scroll at 375px) already built and used by `NotificationDropdown.tsx` in this same codebase. · Aufwand: S

---

### UX

#### F4 - Organization-invite notification leads to a page with no trace of the invite, no explanation
**Kategorie:** UX
**Schweregrad:** Mittel
**Konfidenz:** Wahrscheinlich
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #1 Sichtbarkeit des Systemstatus and #9 Fehler erkennen, diagnostizieren, beheben - the destination gives no indication of what happened to the thing the notification promised)
**Ort:** Notification bell → "Du wurdest eingeladen, Lindenauer Tierschutzverein e.V. beizutreten" → `/my-signups` · Persona: Vera · Viewport: 1440 · Sprache: DE

Beleg: `vera-notifications-desktop.png` (notification present, dated 14.08.2026), `vera-invitation-landing-v2.png` (destination page after clicking it - only shows Vera's one confirmed sign-up, no invitations section, no error, no empty-state explanation for invitations specifically). Code check: `MyEngagementsPage/ActivitySection.tsx` does implement a full accept/decline invitations UI (`getMyInvitations`, `acceptInvitation`, `declineInvitation`) that renders above the sign-ups list whenever the invitations array is non-empty - so the destination and the UI both exist and work when there's live data; what's missing is any feedback when the notification is stale.

Auswirkung: Clicking a notification that explicitly names a pending action ("you were invited to join...") and landing on a page with zero acknowledgment of that invite reads as broken, even if the underlying cause is that the invite was already resolved (accepted/declined/expired) by the time it was clicked and the notification itself just wasn't cleaned up. Either way, the user is left to guess why nothing happened.

Verbesserungsvorschlag: When `acceptInvitation`/`declineInvitation`'s underlying list no longer contains the invitation a notification points to (404/already-resolved), route the click to a small inline confirmation ("This invitation is no longer available") rather than a silent landing on an unrelated list - and/or mark stale invitation notifications read-only or remove them once the invitation resolves, so they can't be clicked into a dead end. · Aufwand: S-M

<em>Konfidenz "Wahrscheinlich" rather than "Bestätigt": this could not be fully isolated from the shared-staging test-data caveat noted in Scope & Method above - it's possible another reviewer/session resolved this specific invitation between it being created and this session clicking it, which would be a one-off data-timing artifact rather than a reproducible gap. The missing-feedback behavior itself, however, is real and observable regardless of cause.</em>

#### F5 - Organizer dashboard observed stuck on loading skeletons for 15+ seconds after all data had already loaded
**Kategorie:** UX
**Schweregrad:** Hoch (when it occurs)
**Konfidenz:** Wahrscheinlich
**Einordnung:** Best Practice (Nielsen-Norman-Heuristik #1 Sichtbarkeit des Systemstatus - a loading skeleton that never resolves and never explains itself is indistinguishable, to the user, from a crash)
**Ort:** `/app/{orgId}/dashboard` (all five widgets: "Erfordert deine Aufmerksamkeit", "Freiwillige", "Bevorstehende Einsätze", "Kalender", "Schnell-Check-in") · Persona: Olaf · Viewport: 1440 · Sprache: DE

Beleg: `olaf-org-dashboard-15s.png`, taken 15 seconds after navigating to the dashboard, with every widget still showing its skeleton/placeholder state. Network logging over that same window shows all of the dashboard's own API calls (`/dashboard`, `/dashboard/layout`, `/calendar-events`, `/opportunities`) returning `200` - the data was available server-side well before the UI reflected it. Two subsequent attempts (one 10s poll, one second-by-second poll) loaded the same dashboard normally in 2-2.5 seconds, and a third attempt on mobile also loaded normally.

Auswirkung: When it happens, an organizer's primary landing page - the page every organizer sees first after picking their org - shows nothing but grey placeholder bars for well over ten seconds with no progress indication, no partial content, and no error message, even though the platform already had everything it needed to render real content within a second or two (as later runs confirmed). A first-time organizer hitting this on their first login has no way to tell "still loading" from "broken."

Verbesserungsvorschlag: Since this could not be reproduced on demand (2 of 3 attempts loaded fine), this needs the `bugs`/`ci` lens's tooling to catch it under controlled, repeatable conditions (e.g. a component-level race in one of the five widgets' independent data-fetch hooks, or transient backend/staging latency) rather than a frontend-only fix proposed from a single observation. Frontend-side regardless of root cause: consider a maximum skeleton duration after which a widget shows a retry/error affordance instead of an indefinite skeleton, so a genuine hang is never visually indistinguishable from a fast load in progress. · Aufwand: investigation first (see above), then S if it turns out to be missing a loading-timeout affordance

<em>Vermutete Ursache: konnte in diesem Review nicht zuverlässig reproduziert werden (2 von 3 Versuchen luden normal in 2-3s) - möglich sind ein Frontend-seitiger Re-Fetch-Loop in einem der fünf unabhängigen Widget-Hooks oder eine transiente Backend-/Staging-Latenz; nicht umgesetzt, da außerhalb des Beleg-Standards für eine sichere Ursachenzuordnung.</em>

## Parking Lot

- Silent-SSO probe (`prompt=none` iframe) 400s + trips a CSP `frame-ancestors` console warning on every anonymous page load, with no visible UI effect - candidate for the `bugs` lens.
- Vera's "Past" sign-ups tab surfaces raw internal QA note text ("Testnachricht fuer Review - bitte ignorieren") as if it were the volunteer's own sign-up message - this is inherited shared-staging test debris (see Scope & Method), not a product defect, but worth a `reset-staging` pass or a `repo-hygiene`/`bugs` lens look if it recurs.
- F5's dashboard loading behavior needs reproduction under controlled conditions - candidate for the `bugs` or `ci` lens rather than further frontend-only investigation.
- Two review-brief-described features do not exist in the current build and were therefore untestable: volunteer "saved searches/alerts", and an admin organization-verification action. Worth confirming with the product owner whether these are still on the roadmap or the brief should be updated - not a defect either way.
- Notification bell's unread-count red badge was not visible on Vera's account at the point of testing despite unread-looking items in the panel - plausibly because those items were already marked read from a prior session (shared staging), not confirmed as a bug.

## Prioritized Next Steps

**Quick wins (low effort, high impact):**
- F1: unpublish or edit the one placeholder opportunity ("Wir suchen Helfer:innen" / `xxx`) - it's the first thing on the homepage in both languages.
- F2: either add `data:` to the CSP `img-src`, or swap the two affected native `<select>` elements for the existing `Dropdown.tsx` component - either fix is small and self-contained.
- F3: apply the existing `useScrollFade` pattern to the Administration sub-nav tab strip.

**Larger undertakings:**
- F4: design the "notification points at something that's already gone" state consistently (this likely recurs for other notification types beyond invitations - opportunity-cancelled, sign-up-withdrawn-by-organizer, etc. - worth a systematic pass rather than a one-off fix).
- F5: needs reproduction and root-causing before any fix is safe to scope - route to the `bugs`/`ci` lens with this review's evidence (API-200-but-UI-never-updates, 1 of 3 runs) as the starting point.
