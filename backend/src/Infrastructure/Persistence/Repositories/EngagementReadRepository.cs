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
			.Join(
				dbContext.VolunteerOpportunitiesQuery,
				e => e.OpportunityId,
				o => o.Id,
				(e, o) => new EngagementSummary(
					e.Id.Value,
					e.OpportunityId.Value,
					o.Title,
					e.VolunteerId.Value,
					e.TimeSlotId != null ? e.TimeSlotId.Value.Value : (Guid?)null,
					e.Message,
					e.Status.ToString(),
					e.IsCheckedIn,
					e.CreatedOn))
			.OrderByDescending(e => e.CreatedOn)
			.ToListAsync(cancellationToken);

	public async ValueTask<List<EngagementSummary>> GetByVolunteerAsync(
		UserId volunteerId,
		CancellationToken cancellationToken = default) =>
		await dbContext.EngagementsQuery
			.Where(e => e.VolunteerId == volunteerId)
			.Join(
				dbContext.VolunteerOpportunitiesQuery,
				e => e.OpportunityId,
				o => o.Id,
				(e, o) => new EngagementSummary(
					e.Id.Value,
					e.OpportunityId.Value,
					o.Title,
					e.VolunteerId.Value,
					e.TimeSlotId != null ? e.TimeSlotId.Value.Value : (Guid?)null,
					e.Message,
					e.Status.ToString(),
					e.IsCheckedIn,
					e.CreatedOn))
			.OrderByDescending(e => e.CreatedOn)
			.ToListAsync(cancellationToken);
}
