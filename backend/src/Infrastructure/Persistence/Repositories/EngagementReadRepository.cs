using Application.Engagements;
using Domain.Organizations;
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
				dbContext.VolunteerOpportunitiesQuery.Select(o => new { o.Id, o.Title, o.OrganizationId }),
				e => e.OpportunityId,
				o => o.Id,
				(e, o) => new
				{
					e.Id,
					e.OpportunityId,
					OpportunityTitle = o.Title,
					o.OrganizationId,
					e.VolunteerId,
					e.TimeSlotId,
					e.Message,
					e.Status,
					e.IsCheckedIn,
					e.CreatedOn,
				})
			.Join(
				dbContext.OrganizationsQuery.Select(org => new { org.Id, org.Name }),
				x => x.OrganizationId,
				org => org.Id,
				(x, org) => new
				{
					x.Id,
					x.OpportunityId,
					x.OpportunityTitle,
					OrganizationId = org.Id,
					OrganizationName = org.Name,
					x.VolunteerId,
					x.TimeSlotId,
					x.Message,
					x.Status,
					x.IsCheckedIn,
					x.CreatedOn,
				})
			.OrderByDescending(x => x.CreatedOn)
			.ToListAsync(cancellationToken);

		return raw.Select(x => new EngagementSummary(
			x.Id.Value,
			x.OpportunityId.Value,
			x.OpportunityTitle,
			x.OrganizationId.Value,
			x.OrganizationName,
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
				dbContext.VolunteerOpportunitiesQuery.Select(o => new { o.Id, o.Title, o.OrganizationId }),
				e => e.OpportunityId,
				o => o.Id,
				(e, o) => new
				{
					e.Id,
					e.OpportunityId,
					OpportunityTitle = o.Title,
					o.OrganizationId,
					e.VolunteerId,
					e.TimeSlotId,
					e.Message,
					e.Status,
					e.IsCheckedIn,
					e.CreatedOn,
				})
			.Join(
				dbContext.OrganizationsQuery.Select(org => new { org.Id, org.Name }),
				x => x.OrganizationId,
				org => org.Id,
				(x, org) => new
				{
					x.Id,
					x.OpportunityId,
					x.OpportunityTitle,
					OrganizationId = org.Id,
					OrganizationName = org.Name,
					x.VolunteerId,
					x.TimeSlotId,
					x.Message,
					x.Status,
					x.IsCheckedIn,
					x.CreatedOn,
				})
			.OrderByDescending(x => x.CreatedOn)
			.ToListAsync(cancellationToken);

		return raw.Select(x => new EngagementSummary(
			x.Id.Value,
			x.OpportunityId.Value,
			x.OpportunityTitle,
			x.OrganizationId.Value,
			x.OrganizationName,
			x.VolunteerId.Value,
			x.TimeSlotId?.Value,
			x.Message,
			x.Status.ToString(),
			x.IsCheckedIn,
			x.CreatedOn)).ToList();
	}
}
