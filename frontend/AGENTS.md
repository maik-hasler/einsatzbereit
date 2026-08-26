# Frontend - Vite + React 19 + Tailwind CSS 4

## Architecture

Vite SPA. React Router v8 for routing. Client-side OIDC via `react-oidc-context`. Static files served by nginx in the released image.

```
src/
├── client/
│   ├── api-client.ts       NSwag-generated TypeScript client - DO NOT HAND-EDIT
│   └── api-instance.ts     Creates EinsatzbereitApi with Bearer token (accepts optional token string)
├── hooks/
│   └── useApiClient.ts     React hook: returns api-client instance with token from useAuth()
├── components/
│   ├── Header/             Header with auth state (login/logout buttons, org switcher, notifications) + optional per-page breadcrumb/action bar (see `breadcrumb` prop) - split across Header.tsx (orchestrator), DesktopHeader.tsx, MobileHeader.tsx, MobileMenu.tsx, AccountControls.tsx, NotificationDropdown.tsx, NotificationItem.tsx, OrganizationSwitcher.tsx, LanguageSelector.tsx, BreadcrumbBar.tsx - all exclusive to Header, nothing here is imported outside this folder
│   ├── Footer.tsx          Footer with links and social icons
│   ├── CreateOrganizationModal.tsx     Modal form for org creation
│   ├── OpportunityCard.tsx     The one opportunity card, shared by every surface that shows one (#2054) - /opportunities, the landing page's three-card preview, the org profile's "current needs" list, and the opportunity detail page's "more from this organization" rail. Takes a `headingLevel` (2|3) prop and an `item` that either DTO (`VolunteerOpportunitySummary` or the leaner `PublicOpportunitySummaryDto`) satisfies structurally - fields absent on the leaner DTO (organization identity, tags, banner) gate the blocks that need them rather than switching components
│   ├── VolunteerOpportunitiesList/     Paginated list (size=10), filter bar - split across VolunteerOpportunitiesList.tsx (orchestrator), icons.tsx, MiniCalendar.tsx, FilterDropdown.tsx, OpportunityResultsList.tsx, useVolunteerOpportunitiesData.ts, useOpportunityDateAvailability.ts, useCitySuggestions.ts - exclusive to this folder, all consuming the shared `OpportunityCard` above
│   └── CreateVolunteerOpportunityModal.tsx  Modal form for opportunity creation
├── layouts/
│   ├── AppLayout.tsx       Header + <Outlet /> + Footer
│   └── ProtectedRoute.tsx  Redirects to Keycloak if not authenticated
├── pages/
│   ├── HomePage.tsx                    Landing page: hero search, LatestOpportunitiesSection preview, org CTA, founder band, FAQ
│   ├── OpportunitiesPage.tsx           /opportunities - the browse/search route that owns VolunteerOpportunitiesList
│   ├── app/OrgDashboardPage/           Customizable widget dashboard (calendar, to-do, quick check-in, etc. - see widgetCatalog.ts) - org app shell's landing tab
│   ├── AdministrationPage.tsx          Platform-admin only: list/verify organizations, list users, toggle admin/enabled status
│   ├── PrivacyPolicyPage.tsx           Privacy policy (static)
│   └── ImprintPage.tsx                 Legal notice (static)
├── styles/global.css       Tailwind directives + custom brand theme
├── main.tsx                Entry point: AuthProvider + BrowserRouter + App
├── App.tsx                 React Router route declarations
└── vite-env.d.ts           ImportMetaEnv types for VITE_ variables
```

## Auth Flow

```
User clicks "Anmelden"
→ auth.signinRedirect() (react-oidc-context)
→ Keycloak login (PKCE, handled by oidc-client-ts)
→ Redirect back to /callback
→ AuthProvider processes code exchange, fires onSigninCallback (strips params)
→ auth.isAuthenticated = true, auth.user.access_token available
→ ProtectedRoute renders children
```

- `auth.user?.profile` - decoded id_token claims (sub, email, name, preferred_username, roles)
- `auth.user?.access_token` - Bearer token for API calls
- `active-org` cookie - last-opened organization id (set by `OrgAppLayout` on every successful org load; read by `HomePage`'s org-app resolution to skip straight to the right dashboard, see `lib/activeOrg.ts`)
- Roles: `auth.user?.profile?.roles` - flat string array from Keycloak custom mapper

## API Client

`src/client/api-client.ts` is auto-generated from `backend/src/Api/wwwroot/openapi-v1.json` by NSwag on every backend build. Never edit it manually - changes will be overwritten.

Use the `useApiClient()` hook in components to get an authenticated `EinsatzbereitApi` client instance, then call its generated methods directly (e.g. `api.getVolunteerOpportunities(...)`, `api.createOrganization(...)`).

For one-off calls outside React (e.g., scripts), use `createApiClient(token)` directly.

For endpoints with many optional query params (e.g. `getVolunteerOpportunities`, 17 positional params), don't call the generated client method directly - write a named-options wrapper instead, see `lib/volunteerOpportunities.ts`'s `fetchVolunteerOpportunities`.

## Environment Variables

Dev values are defined in `.env.development` - see that file for the current list. Exposed client-side via Vite (must use `VITE_` prefix) and accessed via `import.meta.env.VITE_*`.

## Role Checks

Roles come from `auth.user?.profile?.roles` (flat string array, custom Keycloak mapper).

```tsx
const roles = (
	Array.isArray(auth.user?.profile?.roles) ? auth.user!.profile.roles : []
) as string[];
const isOrganisator = roles.includes("organisator");
```

Known roles: `user`, `organisator`, `admin`.

## Routing

Routes declared in `src/App.tsx`. Add new routes there, following the file's existing pattern: lazy-load the page component (`const MyPage = lazy(() => import("./pages/MyPage"))`) and declare the route (`<Route path="/my-page" element={<MyPage />} />`, wrapped in `<ProtectedRoute>` if it requires login) - see the file's own top-of-file comments for why pages are lazy-loaded (per-route build chunks, PWA precache size, Vite's `INEFFECTIVE_DYNAMIC_IMPORT` warning) and which pages are the deliberate exceptions.

**Note:** During development, new page code may use `(api as any)` until the backend rebuilds and `api-client.ts` regenerates with the new method (see API Client above).

## Scripts

```bash
pnpm dev             # dev server on :4321
pnpm build           # build to dist/ (static files)
pnpm preview         # preview production build
pnpm check           # tsc --noEmit
pnpm test            # vitest run - CI hard gate (frontend.yml's test job)
pnpm test:watch      # vitest in watch mode, for local development
pnpm test:coverage   # vitest run --coverage
pnpm mutation        # StrykerJS over src/lib - report-only, minutes, never a gate
pnpm mutation:since  # only files changed since origin/main
pnpm lint            # eslint, zero warnings allowed
pnpm format:write    # apply Prettier formatting - run before every commit
pnpm format:check    # check Prettier formatting (used by CI)
pnpm i18n:check      # verify en.json/de.json key parity - CI hard gate, run before committing locale changes
```

## Unit Tests

Vitest (`vitest.config.ts`, jsdom environment) covers three things, colocated next to what they test:

- **Pure logic in `src/lib/`**, as `*.test.ts` (e.g. `src/lib/activeOrg.test.ts`).
- **Component and page behaviour**, as `*.test.tsx` next to the component or page (e.g. `src/pages/app/OrgSettingsPage.test.tsx`) - rendered copy, form state, which branch a component takes for a given prop or a given rejected promise.
- **Component accessibility**, as `*.a11y.test.tsx` (e.g. `src/components/ConfirmDialog.a11y.test.tsx`) - see "Accessibility (a11y)" below.

What stays in the Playwright suite in `backend/tests/VisualTests/` is what a browser is genuinely needed for: rendered layout (jsdom has no layout engine, so anything measuring a width, a computed colour or a bounding box cannot move), real keyboard and pointer input, real focus movement, a real Keycloak round trip, real offline, page-level axe scans, and multi-page journeys that are not any one component's output. #2148 moved everything else down - 310 cases, leaving 232; see root `AGENTS.md` and `docs/TDRs/2_slow_ci_pipeline.adoc` for the per-category breakdown of what is left and why.

Two porting hazards are worth knowing before moving anything else down, both of which produced green-but-vacuous tests during #2148: **Playwright matches accessible names by substring, RTL matches them exactly** (so a `getByRole("button", { name: "Remove" })` that worked end-to-end matches nothing here when the real name is an `aria-label` reading "Remove Vera Volunteer"), and **an `alt=""` image has no `img` role** (query it by tag, and say why in a comment).

`src/test/` holds the shared harness for the component suites and is not itself a test target:

| File | What it gives you |
| ---- | ----------------- |
| `render.tsx` | `renderWithProviders(ui, { lng, route, auth })` - wraps the tree in the three providers every non-trivial component assumes (i18n, router, auth). Defaults to English, `/`, signed out |
| `i18n.ts` | A synchronous i18n instance preloaded from the shipped `en.json`/`de.json`. The app's own `src/i18n.ts` loads locales through a *dynamic import*, so a component rendered in the same tick still shows raw keys - and an accessible name of "opportunities.signUp" is not the name a user gets |
| `a11y.ts` | `expectNoA11yViolations(target?)` - see below |
| `apiMock.ts` | `createApiMock()` - a Proxy standing in for the generated `EinsatzbereitApi`, where every endpoint resolves to a `vi.fn()` on first access. A page pulls from five or more endpoints across its own effects and its children's; listing each by hand makes a test fail on a call it never cared about every time an unrelated fetch is added |
| `setup.ts` | Registers jest-dom matchers and Testing Library cleanup (this config has `globals: false`, so RTL's own auto-cleanup never registers), plus the jsdom stubs axe needs |

Conventions used across the existing suite:

- Mock a module's own dependencies with `vi.mock(...)`, not the module under test itself. Values referenced inside a `vi.mock` factory must go through `vi.hoisted(...)` (mock factories are hoisted above imports, so a plain top-level `const` isn't initialized yet when the factory runs).
- Prefer computing the expected value with the same underlying call (e.g. `new Date(iso).toLocaleString(...)`) over hardcoding a formatted string, when the result depends on the host timezone or locale data.
- For module-level singletons/config computed at import time (e.g. `lib/runtimeConfig.ts`, `lib/keycloakRegistration.ts`), call `vi.resetModules()` in `beforeEach` and re-`import()` the module inside each test to get a fresh instance.
- `vi.spyOn` on an already-spied method (e.g. `console.error` spied in a previous test) returns the *same* mock and keeps its call history - restore with `vi.restoreAllMocks()` in `afterEach` rather than only resetting the fake in `beforeEach`.

## Mutation Testing

`pnpm mutation` runs StrykerJS over `src/lib/**` and reports a mutation score - how many deliberate defects the suite actually catches, which is the question line coverage cannot answer. Report-only, never a gate (`thresholds.break: 0`), same posture as the backend's Stryker.NET (#2147). CI equivalent: `mutation-tests.yml`'s `mutation-tests-frontend` job, `workflow_dispatch` only.

- **Baseline: 80.95 %** (897 generated / 840 scored / 680 killed / 126 survived / 34 no-coverage / 57 ignored). Recorded on #2148's head, and confirmed by CI [run `32479000847`](https://github.com/maik-hasler/einsatzbereit/actions/runs/32479000847) at the same commit. Comparable only against another StrykerJS 10.0.0 run of the same `mutate` scope - a version bump or a scope change makes the number a different number.
- **Runtime: 28 min 59 s on a GitHub runner**, 36 min 24 s on a contended 4-core sandbox. Either way it is the slow leg - the two .NET legs are 4 min 23 s and 7 min 34 s - which is why the component tier stays on demand.
- **`concurrency: 2` is reproducible here, unlike on the .NET side.** Two runs of this config on very different hardware (a 4-core sandbox and a clean `ubuntu-latest`) produced 80.95 % with a bit-for-bit identical per-file table. That matters because `backend/tests/Application.UnitTests/stryker-config.json` pins `concurrency: 1` precisely *because* Stryker.NET's preview MTP runner invents kills at 2 (#2147). Do not copy that constraint over here by analogy - it is a different runner with a different bug, and n=2 agreement is the evidence for leaving this at 2.
- **Scoped to `src/lib/**` deliberately.** All of `src/**` is 182 files / 14 851 mutants against a suite that renders components in jsdom - a full pass is hours, where the backend's 2 690 mutants over pure functions take ~12 minutes. `src/lib/**` (37 files, 897 mutants) is the tier that is cheap to mutate and comparable to the backend's.
- **Score one component on demand:** `pnpm mutation --mutate "src/components/OpportunityCard.tsx"`. That is the useful granularity - a single component's survivors point at specific unasserted branches, where a whole-app average points at nothing. Measured: `OpportunityCard.tsx` 39.68 %, `NotificationItem.tsx` 57.69 %, `Footer.tsx` 4.76 %.
- **Read the survivors, not the number.** Only 5 of `OpportunityCard`'s 76 survivors sat on a `className` line - the rest were real branches the tests never exercise (`capacity.isFull`, the `unlimited` case, the `FEW_SPOTS_THRESHOLD` tone). The score is a pointer to those, not a target to raise.
- **`plugins` in `stryker.config.json` must name `@stryker-mutator/vitest-runner`.** pnpm's strict `node_modules` stops Stryker discovering it by convention; without it the error reads "no TestRunner plugins were loaded", which looks like a missing install.
- **`ignoreStatic: true` changes the denominator, and buys less than Stryker's warning implies.** A static mutant sits in module-level code, so killing it needs a full test-file reload rather than a re-run of the covering tests; Stryker warns that 111 of `src/lib`'s 897 mutants (12 %) are static and estimates them at 69 % of the run. Two things measured against that estimate: the flag actually excluded **57** mutants, not 111, and the run still took 36 minutes. The pass without the flag was killed at 35 minutes before finishing, so the saving is real but unquantified - what is certain is that it does not make the run cheap. Keep the flag (module-scope initialisation is excluded from the score as a result), and do not read the 69 % as headroom anyone can reclaim.
- `reports/` and `.stryker-tmp/` are gitignored.

## Key Dependencies

| Package              | Purpose                              |
| -------------------- | ------------------------------------ |
| `react-router` v8    | Client-side routing                  |
| `react-oidc-context` | Keycloak OIDC (wraps oidc-client-ts) |
| `oidc-client-ts`     | PKCE flow, token management          |
| `@tailwindcss/vite`  | Tailwind CSS 4 via Vite              |
| `leaflet`            | Map renderer (OpenStreetMap tiles)   |
| `react-leaflet`      | React bindings for Leaflet           |

## Linting

The CI `lint` job runs `format:check` and will fail if any Prettier violations exist - causing `build` to be skipped and a follow-up fix commit.

```bash
pnpm lint
```

Rules enabled: `@typescript-eslint/strict`, `react-hooks/rules-of-hooks`, `react-hooks/exhaustive-deps`, `jsx-a11y/recommended`, `tailwindcss/classnames-order`, `tailwindcss/no-contradicting-classname`, `tailwindcss/no-unnecessary-arbitrary-value`, `tailwindcss/enforces-negative-arbitrary-values`.

- No non-null assertions (`!`). Use `as Type` or type narrowing (`if (!x) return`).
- If `api` is intentionally excluded from `exhaustive-deps`, suppress with `// eslint-disable-next-line react-hooks/exhaustive-deps` and keep it consistent with the existing pattern.
- `prettier-plugin-tailwindcss` sorts `className` values automatically as part of `pnpm format:write` - don't hand-order classes. `eslint-plugin-tailwindcss` (`tailwindcss/*` rules above) is the CI-enforced backstop for anything that slips through unformatted, plus `no-contradicting-classname` (e.g. `mt-2 mt-4` on the same element) and `no-unnecessary-arbitrary-value`/`enforces-negative-arbitrary-values` (flags e.g. `w-[480px]` where the equivalent scale utility `w-120` exists). Both plugins resolve the theme from `src/styles/global.css` (`.prettierrc.json`'s `tailwindStylesheet`, `eslint.config.js`'s `settings.tailwindcss.cssConfigPath`), so custom tokens like `brand-700` are recognized.

## Design System

Shared UI primitives live in `src/components/` and `src/lib/` (no separate design-system package). Reuse these instead of re-deriving their markup/classes - each exists because the same visual pattern had drifted into 2-4 slightly different hand-rolled versions across pages (see the rationale comments in `Button.tsx` and `ErrorBanner.tsx` for two examples); the Tailwind lint rules above only catch class-order and arbitrary-value drift, not this kind of duplication.

| Primitive     | File                          | Use for                                                                                                                               |
| ------------- | ------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------- |
| `Button`      | `components/Button.tsx`      | Every clickable action, button or link - `variant` (`primary`/`secondary`), `size` (`sm`/`md`/`lg`), `fullWidth`                     |
| `formClasses` | `lib/formClasses.ts`         | `inputClass`, `textareaClass`, `labelClass` for every form control                                                                   |
| `RequiredMark` | `components/RequiredMark.tsx` | The product's only required-field marker - an aria-hidden `*` appended by the component, never written into a translation string, plus the `RequiredFieldsLegend` that explains it (one per form). The control still needs its own `required`/`aria-required`: that is the accessible half |
| `ErrorBanner` | `components/ErrorBanner.tsx` | Inline "action failed" message - the one boxed style every page's error state should share                                          |
| `SuccessBanner` | `components/SuccessBanner.tsx` | Inline "action succeeded" message - the `ErrorBanner` twin, same box style in green                                                |
| `WarningBanner` | `components/WarningBanner.tsx` | Inline advisory that isn't a failure (e.g. approaching a churn/rate limit) - the `ErrorBanner` twin, same box style in amber, `role="status"` instead of `"alert"` |
| `EmptyState`  | `components/EmptyState.tsx`  | "Nothing here yet" placeholder with an optional CTA button                                                                           |
| `RouteState`  | `components/RouteState.tsx`  | The four ways a route can fail to show what was asked for - `variant` (`notFound`/`forbidden`/`offline`/`error`), each with its own glyph, tone and allowed actions. `inline` for a state replacing one section of a page rather than the whole route. Retry is honoured for `error` only, by design (see #1774) |
| `Skeleton`    | `components/Skeleton.tsx`    | Loading placeholders (`animate-pulse` block)                                                                                         |
| `Modal`       | `components/Modal.tsx`       | Every dialog - backdrop-button a11y pattern (see Accessibility below), focus trap, Escape-to-close, portals out of `inert` ancestors |
| `OrgAvatar`   | `components/OrgAvatar.tsx`   | Every organization avatar (logo image, or an initials fallback) - the one avatar treatment for orgs, used by the header nav, `/organizations`, Administration, an org's profile view and `OpportunityCard`. `size` (`sm`/`md`/`lg`/`xl`/`2xl`/`3xl`) covers every pixel size those surfaces need (#2244) |
| `lockScroll`  | `lib/scrollLock.ts`          | Locking the page behind any blocking overlay (dialog, mobile menu) for as long as it is open - reference-counted, so nested dialogs work. Targets the **root** element, not `body`: `global.css`'s `html { overflow-x: clip }` stops a body-level lock from ever reaching the viewport (#1787) |
| `surfaceClasses` | `lib/surfaceClasses.ts`   | `cardClass`, `cardSubtleClass` for every bordered content panel (list items, result cards, widget panels, info panels) - not media cards whose padding lives on an inner wrapper around an edge-to-edge image/map |
| `opportunityCapacity` | `lib/opportunityCapacity.ts` | Every surface stating how full an opportunity is - `getOpportunityCapacity` resolves `VolunteerOpportunitySummary.totalMaxParticipants`'s tri-state (`null` unlimited / `0` no time slots / `> 0` capped) into an explicit state, `getCapacityFromTimeSlots` does the same from the detail page's per-slot rows, `FEW_SPOTS_THRESHOLD` is the shared scarcity cut-off. Wording for the organizer-facing count is `formatSignUpCount` in `lib/format.ts`. Don't re-derive the mapping inline: three surfaces each did, each handled only two of the three cases, and all three silently rendered nothing for the third (#1777) |

**Tokens:** the brand color scale (`brand-50`...`brand-900`) and `accent-400` are defined once in `src/styles/global.css`'s `@theme` block - use them instead of ad hoc hex values or arbitrary colors. `brand-700` is the single rendered "brand green" - it's also what `public/logo.svg`, `public/favicon.svg` and `index.html`/`vite.config.ts`'s `theme_color` are pinned to, so a control's rendered color, the installed app's chrome color and the logo agree (#2244). `accent-400` has exactly one job: decoration (glow blobs, background texture) - never a call-to-action surface or anything semantically "warning"/"pending" (that's Tailwind's built-in `amber-*` scale, e.g. `WarningBanner`, `Chip`'s `warning` variant). `rounded-xl` is the default corner radius for interactive surfaces (buttons, inputs); cards, panels and modals use the separate `rounded-card` (16px) token instead - see `surfaceClasses.ts` and `Modal.tsx`. `rounded-md` is reserved for `Skeleton` loading blocks.

## Date Formatting

One numeric convention (`27.08.2026, 09:00`), plus one documented relative
exception - no other date register (#2047). All helpers live in
`lib/format.ts` and take `lng` as `i18n.language` ("de"/"en"), not an Intl
locale (see `resolveDateLocale`):

| Helper                | Use for                                                                                                             |
| --------------------- | -------------------------------------------------------------------------------------------------------------------- |
| `formatDateTime`      | Any single point-in-time timestamp ("27.08.2026, 09:00") - the default                                             |
| `formatDateTimeRange` | Any start/end range (time slots, calendar events) - collapses a same-day range to one date with a hyphen-joined time range ("27.08.2026, 09:00-17:00") instead of repeating the date on both ends; falls back to two full `formatDateTime`s joined by " - " once the range crosses a calendar day. Always use this for ranges instead of hand-joining two `formatDateTime` calls - that's the exact duplication #2047 was filed about |
| `formatDate`          | Date-only, no time-of-day (deadlines like `ValidUntil`, "created on"/"sent on" timestamps)                          |
| `formatPostedAgo`     | The one relative-date exception ("Vor 5 Tagen veroeffentlicht") - pair it with an `aria-label` (not just `title`, which browse-mode screen reader users and touch/mobile never see) on the wrapping element combining the relative text with the absolute `formatDateTime`, so the exact timestamp is still available to everyone; `title` alongside it is a harmless bonus for mouse users (see `VolunteerOpportunityDetailPage.tsx`'s posted-on span) |
| `formatFullDate`      | Screen-reader-only accessible names for calendar day cells whose visible label is a bare number (`MiniCalendar`, `CalendarWidget`) - not a visible register, so it doesn't compete with the above |

There is no long-form date helper (spelled-out month, e.g. "13. August
2026") - it was retired as a third competing register; use `formatDate`
even for lower-frequency "created on" timestamps.

## Copy Conventions

- **"Zeitslot" vs "Termin"** (opportunity-detail page, `/my-signups`): both
  words describe the same `TimeSlot` entity, but the split is a deliberate
  register shift, not drift (#1920). "Zeitslot" (`opportunities.availableTimeSlots`,
  `opportunities.joinWaitlist`, etc.) is used while a slot is still one of
  several open options to choose from - an inventory listing. Once the
  volunteer has committed to one, both the opportunity-detail page and
  `/my-signups` switch to "Termin" (`myEngagements.scheduledFor`) - the
  volunteer's own confirmed appointment. Keep this pairing when touching
  either surface; don't rename one into the other to "fix" the apparent
  inconsistency.
- **"Anmelden" vs "eintragen"** (German copy, #2237): "Anmelden"/"Anmeldung"
  is reserved for authentication - signing in, signing out, the Keycloak
  login flow - and nothing else. Signing up to participate in a
  `ScheduledSlots` Einsatz uses "eintragen" instead (`opportunities.joinWaitlist`
  "Für Zeitslot eintragen", `signUp.submitWaitlist` "Verbindlich eintragen",
  `opportunities.loginPrompt` "Trag dich ein, um mitzumachen."), and the
  resulting record is an "Eintragung", not an "Anmeldung", through the
  confirmation dialog, toasts, notifications and transactional emails
  (`backend/src/Infrastructure/Email/Templates/de.json`). This does not
  apply to `IndividualContact` opportunities, which already use their own
  unambiguous "Interesse bekunden" / "Interessenbekundung" wording (#2228) -
  don't fold that into "eintragen". Adding a new German string that says
  "sich anmelden" for anything other than logging in reintroduces the exact
  ambiguity #2237 fixed.

## Accessibility (a11y)

`eslint-plugin-jsx-a11y` is enabled with the recommended ruleset. All violations are errors - CI will fail on any a11y lint issue.

Key conventions:

- **Modal dialogs**: Use the backdrop-button pattern. Separate the clickable backdrop (`<button aria-hidden="true" tabIndex={-1}>`) from the dialog container (`<div role="dialog" aria-modal="true" aria-labelledby="...">`) inside a neutral wrapper div. Handle Escape via `useEffect` on `document`.
- **Clickable cards**: Use a stretched `<Link className="absolute inset-0">` inside a `relative` `<li>` rather than putting `onClick` on the `<li>` directly. Any secondary links inside the card get `relative z-10` to sit above the stretched link.
- **Dropdown panels are disclosures, not menus or listboxes** - a trigger with `aria-expanded` opening a `<ul aria-label>` of plain `<button>`s, marking the active one with `aria-current`. That is what `RowActionsMenu.tsx`, `OrganizationSwitcher.tsx` and `Header/LanguageSelector.tsx` all do. Don't reach for `role="menu"`/`"menuitem"` or `role="listbox"`/`"option"` (or the matching `aria-haspopup`) unless you actually implement that pattern's keyboard model - arrow keys, Home/End, and either roving tabindex or `aria-activedescendant`. Claiming the role without the keys tells a screen-reader user to press arrows and gives them nothing, and putting `role="option"` on an `<li>` that *wraps* a `<button>` is an axe `nested-interactive` violation (serious) on top (#1772). `Dropdown.tsx` is the one real listbox in this repo - use it when you need to pick a form value; `LocationSearchInput.tsx` is the one real combobox.
- **Images**: All `<img>` tags need an `alt` attribute. Purely decorative images use `alt=""`.
- **SVG icons**: Decorative SVGs get `aria-hidden="true"`. Meaningful standalone SVGs need a `<title>` or `aria-label`.
- **Form labels**: Every form control must have an associated `<label htmlFor="...">` or `aria-label`.
- **Color contrast (text)**: `text-gray-400` (2.6:1 on white) fails the WCAG AA 4.5:1 floor for text - reserve it for decorative icons and input placeholders only, never for body copy, labels, timestamps, or other real content. Use `text-gray-500` (4.9:1) or darker for content; an interactive control's resting label needs to clear at least the 3:1 UI-component floor too.
- **Color contrast (non-text)**: WCAG 1.4.11 sets the same 3:1 floor for meaningful icons and interactive-control borders (outline-button/chip borders, checkbox/radio boundaries) - axe-core's `color-contrast` check doesn't evaluate this, so it slips past CI silently (#2048). `-200`/`-300` border tokens and `brand-400`/`brand-500` icon fills all measure under 3:1 against a white or brand-50 background - use `border-gray-500`+, `border-red-500`+, `border-brand-600`+ for borders, and `text-brand-600`/`text-brand-700` for functional icons instead. See `Button.tsx`'s `outline`/`dangerOutline` variants and `FilterDropdown.tsx`'s active/inactive icon colors for the reference values.

Automated axe-core checks run at two altitudes (#2148). Both apply the same gate - fail on any "serious" or "critical" violation, plus a short list of escalated "moderate" rules - so the split is about scope, not about standard:

| | Where | Covers | Run by |
| - | ----- | ------ | ------ |
| **Component** | `src/**/*.a11y.test.tsx`, via `expectNoA11yViolations` from `src/test/a11y.ts` | Roles, accessible names, labelling, ARIA validity, interactive nesting - per component and per component *state* (open/closed, loading, empty, error, validation-rejected) | `pnpm test`, seconds, no browser |
| **Page** | `backend/tests/VisualTests/AccessibilityTests.cs` | Landmark structure, heading order, **colour contrast**, real focus movement, tab order - one scan per distinct layout and palette | `dotnet.yml`'s `visual-tests` shards, minutes, real Chromium + Aspire |

Two consequences worth knowing before adding a test:

- **jsdom has no layout engine and no canvas**, so axe can only ever report `color-contrast` as *inconclusive* there. It is disabled outright in `src/test/a11y.ts` rather than left to look like a passing check. The page scans are the only place contrast is genuinely evaluated - which is why the contrast rules above are stated as conventions here rather than left to CI to discover.
- **Page-scope rules cannot be judged from a fragment** (a component has no `<main>`, no page `<h1>`, no `lang`), so they are disabled in the component harness too and stay the page scans' job.

So: a new component or component state gets a colocated `*.a11y.test.tsx`; a new page/route additionally gets a scan in `AccessibilityTests.cs`. `a11y-check` flags a missing one of either. `src/test/a11y.test.tsx` is the harness's own negative control - it asserts the scanner can still *fail*, and is the first place to look if the a11y suite goes green through a change that should have broken it.

## Released image

Static files in `dist/` served by nginx. `nginx.conf.template` handles SPA routing via `try_files $uri /index.html`; `docker-entrypoint.d/99-runtime-config.sh` renders it (and `config.js`) into their final form at container start via `envsubst`, filling in the Content-Security-Policy's `connect-src`/`frame-src` origins from the same `VITE_API_URL`/`VITE_KEYCLOAK_AUTHORITY_URL` env vars used for runtime config, plus `img-src`'s MinIO storage origin from a separate `STORAGE_PUBLIC_URL` env var (matching the backend's `Storage__PublicEndpoint` - uploaded org logos/opportunity banners/avatars are served from there, not the API origin), so one image works across environments. `img-src` also allows the `blob:` scheme unconditionally (no env var needed) since avatar/org-logo/opportunity-banner pickers preview the selected file via `URL.createObjectURL()` before it's ever uploaded. The CSP string itself is defined once via an nginx `map` (`$csp_header`) and referenced from all four location blocks - `frontend/scripts/check-nginx-csp.js` (`pnpm check:nginx-csp`, wired into CI) guards against a location block silently falling out of sync with that definition.

**Important:** CORS must be configured on the backend to allow the frontend origin, since API calls are now cross-origin (no server-side proxy).
