using Application.Engagements;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

internal sealed class EngagementReadRepository(
    ApplicationDbContext dbContext)
    : IEngagementReadRepository
{
    public async ValueTask<List<EngagementSummary>> GetByOpportunityAsync(
        VolunteerOpportunityId opportunityId,
        CancellationToken cancellationToken = default) =>
        await dbContext.EngagementsQuery
            .Where(e => e.OpportunityId == opportunityId)
            .OrderByDescending(e => e.CreatedOn)
            .Select(e => new EngagementSummary(
                e.Id.Value,
                e.OpportunityId.Value,
                e.VolunteerId.Value,
                e.TimeSlotId != null ? e.TimeSlotId.Value.Value : (Guid?)null,
                e.Message,
                e.Status.ToString(),
                e.CreatedOn))
            .ToListAsync(cancellationToken);

    public async ValueTask<List<EngagementSummary>> GetByVolunteerAsync(
        UserId volunteerId,
        CancellationToken cancellationToken = default) =>
        await dbContext.EngagementsQuery
            .Where(e => e.VolunteerId == volunteerId)
            .OrderByDescending(e => e.CreatedOn)
            .Select(e => new EngagementSummary(
                e.Id.Value,
                e.OpportunityId.Value,
                e.VolunteerId.Value,
                e.TimeSlotId != null ? e.TimeSlotId.Value.Value : (Guid?)null,
                e.Message,
                e.Status.ToString(),
                e.CreatedOn))
            .ToListAsync(cancellationToken);
}
