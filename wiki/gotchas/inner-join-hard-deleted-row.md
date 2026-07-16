---
type: gotcha
title: Inner-joining a hard-deleted row silently drops the other side's history
description: A query that inner-joins across an entity that can be hard-deleted loses rows on the non-deleted side instead of showing them as cancelled or removed.
tags: [backend, ef-core, data-loss]
timestamp: 2026-07-16
---

# Schema

When one entity is hard-deleted but a related entity is only soft-cancelled, an inner join between them silently excludes every row on the soft-cancelled side once its counterpart is gone - no error, no log, just missing data. The fix is to query the surviving side alone first, then look up the related entity separately by id (e.g. into a dictionary) and merge in memory, falling back to null or a placeholder when the id is missing.

# Examples

Opportunity deletion hard-deletes the `VolunteerOpportunity` row; its `Engagement` rows are only cancelled, never deleted. `EngagementReadRepository.GetByVolunteerAsync` chained `.Join()` from Engagements to VolunteerOpportunitiesQuery to OrganizationsQuery, so any engagement whose opportunity was gone was silently excluded from the result - not shown as cancelled, just gone, with no way for the volunteer to know they had ever applied.

The fix queries engagements alone first, then looks up opportunities and organizations separately by id into dictionaries and merges in memory. `EngagementSummary.OpportunityTitle`/`OrganizationId`/`OrganizationName` became nullable; the frontend (`ProfileOverviewPage.tsx`) renders a "This opportunity has been removed" fallback title in that case.

A follow-up, commit `cadb08a` (#704), found the same non-terminal engagements then got stuck in "Current & Upcoming" forever (no date field left to re-evaluate them against) and added an `opportunityExists` subquery check to bucket them into Past instead. See [deleted-opportunity edge cases](../persona-notes/deleted-opportunity-edge-cases.md) for the same entity-lifecycle mismatch showing up on the frontend.

# Citations

- commit `211e2fe` - fix: preserve a volunteer's own engagement history after opportunity deletion (#669)
- commit `cadb08a` - fix: bucket non-terminal engagements for deleted opportunities into Past (#704)
- `backend/src/Infrastructure/Persistence/Repositories/EngagementReadRepository.cs`
