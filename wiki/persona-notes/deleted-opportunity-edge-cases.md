---
type: persona-note
title: persona-simulation keeps finding deleted-opportunity edge cases via unhandled fetch rejections
description: A recurring bug family where deleting an opportunity leaves stale UI state elsewhere - a Check-in modal stuck on Loading, engagements stuck in Current & Upcoming, cards missing their time slot.
resource: https://github.com/maik-hasler/einsatzbereit/issues/686
tags: [persona-simulation, frontend, deleted-opportunity]
timestamp: 2026-07-16
---

# Schema

Deleting a parent entity (here, a volunteer opportunity) tends to leave dependent UI in an inconsistent state wherever the code assumed the parent still exists - a missing `.catch()` on a fetch that now 404s, a status derived only from the child record without checking the parent, a display field that silently becomes null. This is the specific bug shape `persona-simulation` keeps surfacing in this codebase, walking realistic flows an isolated unit test wouldn't cover.

# Examples

Issue #686, found by `persona-simulation` playing Volunteer Vera (Olaf creates an opportunity, Vera applies and is confirmed, Olaf deletes the opportunity, Vera clicks Check in): the Check-in modal spins on Loading forever. Two causes stacked: `ProfileOverviewPage.tsx` shows the Check-in button based on engagement status alone, without checking that `opportunityTitle` still exists (even though the title display elsewhere already has deleted-opportunity fallback text), and `CheckInModal.tsx`'s fetch effect has no `.catch()`, so a 404 leaves an unhandled rejection and the Loading state never clears.

The same recurring family shows up in #703 (engagements for a deleted opportunity never leave "Current & Upcoming" - see [inner-join hard-deleted row](../gotchas/inner-join-hard-deleted-row.md) for the backend side of that one) and #705 (engagement cards show only the sign-up date, never the opportunity's actual time slot). All three are labeled `persona-sim`.

# Citations

- `#686` https://github.com/maik-hasler/einsatzbereit/issues/686
- `#703` https://github.com/maik-hasler/einsatzbereit/issues/703
- `#705` https://github.com/maik-hasler/einsatzbereit/issues/705
