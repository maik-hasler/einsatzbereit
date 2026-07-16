---
type: gotcha
title: Out-of-order async fetch responses can overwrite fresher state
description: An effect-driven fetch with no request-ordering guard let a slower, earlier response overwrite state a faster, later response already set correctly.
tags: [frontend, react, race-condition]
timestamp: 2026-07-16
---

# Schema

A component that fetches data in a `useEffect` on every render/navigation, without tracking which request was issued last, is exposed to a race: if an earlier request resolves after a later one, its response overwrites state the later request already set correctly. The general fix is a per-request counter (or `AbortController`) so each response's `.then`/`.catch`/`.finally` callback checks it's still the latest request before touching state.

# Examples

`VolunteerOpportunityDetailPage.tsx` fetched opportunity details on every effect run with no such guard. An earlier-sent unauthenticated GET could resolve after a later authenticated GET and overwrite the correct `currentUserEngagement` with `null`, making the "already applied" status intermittently revert to "Express interest" on a hard navigation.

The fix added a `useRef` counter (`latestRequestRef`), incremented per request as `requestId`; each `.then`/`.catch`/`.finally` callback checks `if (requestId !== latestRequestRef.current) return;` before touching state, so a stale response is dropped instead of applied. Verified with `scripts/smoke-test-644-followup-race-condition.mjs`, which reproduces the race deterministically via route interception that delays the unauthenticated response.

# Citations

- commit `74455e9` - fix: guard opportunity detail fetch against out-of-order responses (#647)
- `frontend/src/pages/VolunteerOpportunityDetailPage.tsx`
- `scripts/smoke-test-644-followup-race-condition.mjs`
