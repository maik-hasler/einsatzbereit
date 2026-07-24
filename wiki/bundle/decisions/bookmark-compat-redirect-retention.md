---
type: "decision-note"
title: "Bookmark-compat redirects get a 6-month retention window, not indefinite life"
description: "The three redirect-only routes in App.tsx with no in-app link (/account, /achievements, /opportunities) each get an explicit remove-after date instead of living forever as unlabeled debris."
tags:
  - routing
  - redirects
  - App.tsx
  - repo-hygiene
  - retention
timestamp: 2026-07-24
---

# What changed

`App.tsx` has three routes that exist only to catch old bookmarks and shared
links, with nothing in the current UI linking to them: `/account` (redirects
to `/profile`, added by the profile consolidation in #508, 2026-06-22),
`/achievements` (redirects to `/profile?tab=achievements`, same commit), and
`/opportunities` (redirects to `/#opportunities`, added by #271/#297,
2026-05-24). #843 found all three had no in-app link pointing at them and
asked for a maintainer call on how long to keep them, rather than treating
"bookmark-compat is intentional" as license to keep them forever unexamined.

The decision: each gets a 6-month retention window measured from the
restructure that orphaned it, recorded as a comment directly above the
`<Route>` in `App.tsx` with the source PR, the date, and an explicit
remove-after date. `/opportunities` is eligible for removal after
2026-11-24; `/account` and `/achievements` after 2026-12-22. A fourth
redirect-only route, `/my-engagements`, is excluded - `Header.tsx` still
navigates to it directly (`components/Header/Header.tsx:104`), so it is a
live in-app link, not debris.

# Why

A redirect with no comment and no expiry looks identical whether it is one
week old or three years old - the same trap the root `scripts/` folder fell
into before [scripts-folder-removed](/decisions/scripts-folder-removed.md):
debris that nobody ever goes back and removes because nothing marks it as
safe to remove. Deleting these three outright was not an option either -
they still work, and a real bookmark or shared link out there would 404 the
moment they went away. A time-boxed retention window is the middle path:
old links keep working for a defined period, and after that period the
route is fair game for a future cleanup pass without needing a fresh
maintainer decision each time - the decision was made once, here, with the
dates already committed to code.

Six months was picked as long enough that any realistically-bookmarked link
from before a restructure has had a fair chance to be used again, without
leaving the redirect around indefinitely. The date is anchored to each
route's own restructure commit rather than a single shared date, since
`/opportunities` and the `/account`/`/achievements` pair were orphaned a
month apart.

# What this does not change

The routes still work exactly as before; only a comment was added above
each. Removal itself is not automatic and is not done by this change - when
a route's remove-after date passes, removing the `<Route>` (and its
now-dead `element` reference, if nothing else uses it) is a normal follow-up
change, not a decision that needs re-litigating.

# Related

- [scripts-folder-removed](/decisions/scripts-folder-removed.md) - the same
  shape of problem (unmarked debris nobody goes back to remove) resolved
  for the root `scripts/` folder instead of a route
- [frontend-conventions](/reference/frontend-conventions.md) - routes are
  declared centrally in `src/App.tsx`, where these three redirects live

# Citations

- frontend/src/App.tsx@e761b84 (introduced `/account` and `/achievements`
  redirects, #508, 2026-06-22)
- frontend/src/App.tsx@2ac41e3 (introduced the `/opportunities` redirect,
  #271/#297, 2026-05-24)
- frontend/src/components/Header/Header.tsx:104 (`/my-engagements` is still
  linked in-app, unlike the three routes this decision covers)
- #843
