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
		CancellationToken cancellationToken = default)
	{
		var raw = await dbContext.EngagementsQuery
			.Where(e => e.OpportunityId == opportunityId)
			.Join(
				dbContext.VolunteerOpportunitiesQuery.Select(o => new { o.Id, o.Title }),
				e => e.OpportunityId,
				o => o.Id,
				(e, o) => new
				{
					e.Id,
					e.OpportunityId,
					OpportunityTitle = o.Title,
					e.VolunteerId,
					e.TimeSlotId,
					e.Message,
					e.Status,
					e.IsCheckedIn,
					e.CreatedOn,
				})
			.OrderByDescending(x => x.CreatedOn)
			.ToListAsync(cancellationToken);

		return raw.Select(x => new EngagementSummary(
			x.Id.Value,
			x.OpportunityId.Value,
			x.OpportunityTitle,
			x.VolunteerId.Value,
			x.TimeSlotId?.Value,
			x.Message,
			x.Status.ToString(),
			x.IsCheckedIn,
			x.CreatedOn)).ToList();
	}

	public async ValueTask<List<EngagementSummary>> GetByVolunteerAsync(
		UserId volunteerId,
		CancellationToken cancellationToken = default)
	{
		var raw = await dbContext.EngagementsQuery
			.Where(e => e.VolunteerId == volunteerId)
			.Join(
				dbContext.VolunteerOpportunitiesQuery.Select(o => new { o.Id, o.Title }),
				e => e.OpportunityId,
				o => o.Id,
				(e, o) => new
				{
					e.Id,
					e.OpportunityId,
					OpportunityTitle = o.Title,
					e.VolunteerId,
					e.TimeSlotId,
					e.Message,
					e.Status,
					e.IsCheckedIn,
					e.CreatedOn,
				})
			.OrderByDescending(x => x.CreatedOn)
			.ToListAsync(cancellationToken);

		return raw.Select(x => new EngagementSummary(
			x.Id.Value,
			x.OpportunityId.Value,
			x.OpportunityTitle,
			x.VolunteerId.Value,
			x.TimeSlotId?.Value,
			x.Message,
			x.Status.ToString(),
			x.IsCheckedIn,
			x.CreatedOn)).ToList();
	}
}
