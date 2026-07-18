---
type: "gotcha"
title: Deleting an opportunity orphans its engagements
description: Hard-deleting a VolunteerOpportunity only cancels its engagements; reads that assume a live opportunity silently drop or strand a volunteer's history.
tags: [engagement, ef-core, data-integrity, opportunity-deletion]
timestamp: 2026-07-18
---

# Deleting an opportunity orphans its engagements

Deleting a `VolunteerOpportunity` is not symmetric with what happens to its
`Engagement` rows, and nothing at a single call site tells you so.
`DeleteVolunteerOpportunityCommandHandler` cancels every active engagement
(`engagement.Cancel("Opportunity was deleted.")`) and then hard-deletes the
opportunity row. The engagements survive as cancelled records pointing at an
`OpportunityId` that no longer resolves. That surviving-but-orphaned state is an
invariant the rest of the read side has to honor, and three separate reads got
it wrong by assuming the opportunity is still there.

# Reads must not inner-join engagements to opportunities

`EngagementReadRepository.GetByVolunteerAsync` powers a volunteer's own
history. An inner join from engagements to `VolunteerOpportunitiesQuery` drops
every engagement whose opportunity was deleted, so a volunteer loses all record
that they ever applied or were confirmed (#669). The join direction hides it:
the SQL looks correct, it just quietly filters.

The fix fetches the engagements first, then looks opportunities and
organizations up separately (dictionary by id) and merges them in.
`OpportunityTitle`, `OrganizationId`, and `OrganizationName` become nullable and
fall back to null when the lookup misses. The UI renders a "This opportunity has
been removed" fallback instead of dropping the card. Contrast this with sibling
methods in the same repository (`GetByOpportunityAsync`, `GetCalendarInfoAsync`)
that still inner-join or short-circuit on a missing opportunity - correct for
their use, because they are scoped to one opportunity that is known to exist.

# Bucketing must also check the opportunity still exists

The same read splits engagements into "Current & Upcoming" and "Past". A pure
status-based split puts `Pending` and not-yet-checked-in `Confirmed`
engagements into the upcoming bucket. A deleted-opportunity engagement stays
non-terminal (it was cancelled, but the older stale rows may predate that, and
in any case there is no date to advance against), so status-only bucketing
leaves it in "Current & Upcoming" forever (#704).

The bucket predicate therefore also tests `opportunityExists.Contains(e.OpportunityId)`:
a non-terminal engagement whose opportunity is gone is pushed to Past, since it
can never be confirmed, checked into, or acted on again. This is read-time
reclassification, no migration - existing stale rows correct themselves on the
next query. The existence check only moves the row between buckets; the
opportunity data is still merged in separately per the no-inner-join rule above,
so the engagement still appears rather than vanishing.

# A fetch with no .catch strands the check-in modal

`CheckInModal` loads opportunity details on mount. If the organizer deletes the
opportunity in another session after the engagements list has already rendered,
that fetch 404s. Without a `.catch`, the promise rejects unhandled and the modal
sits on its "Loading..." state with no way out but Close (#687).

The fix adds `.catch(() => setLoadError(true))` and renders a translated
`checkIn.loadError` ("This opportunity is no longer available.") with
`role="alert"`. `ProfileOverviewPage` also gates the "Check in" action on
`opportunityTitle` being present, so once the title cannot resolve the button is
hidden rather than opening a modal that can only fail. The lesson generalizes:
any client fetch keyed on an opportunity id can 404 mid-session because deletion
is a hard delete, so every such fetch needs an error path.

# Related
- [domain-events-noop](/gotchas/domain-events-noop.md) - both concern engagement lifecycle state that changes outside the obvious call site
- [ef-migrations](/process/ef-migrations.md) - the singular snake_case table names bite the same read-repository/raw-SQL code

# Citations
- #669 - preserve a volunteer's own engagement history after opportunity deletion (no-inner-join read, nullable title/org fields, "This opportunity has been removed" fallback)
- #704 - bucket non-terminal engagements for deleted opportunities into Past (existence check in the read-time bucketing predicate)
- #687 - handle deleted-opportunity 404 in CheckInModal instead of hanging on Loading (add .catch, gate the check-in action on opportunityTitle)
