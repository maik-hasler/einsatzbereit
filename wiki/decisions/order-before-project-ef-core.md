---
type: decision-note
title: Order on the raw entity property before projecting into a DTO, not after
description: Calling OrderBy on an already-projected DTO property fails EF Core query translation; the repo's convention is to order on the entity property first, then Select into the DTO.
tags: [backend, ef-core, query-patterns]
timestamp: 2026-07-16
---

# Schema

EF Core needs to translate an `OrderBy` into SQL against the underlying entity, not against an already-projected DTO shape. `.Where(...).Select(e => new Dto(...)).OrderBy(d => d.SomeProperty)` can fail translation where `.Where(...).OrderBy(e => e.SomeProperty).Select(e => new Dto(...))` works. Order first, project second.

# Examples

`GetOpportunityFeedback`'s query in `EngagementReadRepository.cs` called `.OrderByDescending` on the already-projected `FeedbackItemDto.SubmittedAt` instead of the raw entity's `FeedbackSubmittedAt`, which failed EF Core query translation and made every call to the endpoint return 500 regardless of the underlying data. The fix moved the ordering before the `Select`. The commit message notes this was the only read-repository query in the codebase ordering after projection - every other query already follows order-then-project.

# Citations

- commit `a4a70d9` - fix: repair GetOpportunityFeedback 500 and drop doubled arrow on manage-engagements links (#631)
- `backend/src/Infrastructure/Persistence/Repositories/EngagementReadRepository.cs`
- `backend/src/Application/VolunteerOpportunities/GetOpportunityFeedback/v1/`
