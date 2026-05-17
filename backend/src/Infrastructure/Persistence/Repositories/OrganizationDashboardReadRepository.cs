using Application.Organizations;
using Application.Organizations.GetOrganizationDashboard.v1;
using Domain.Engagements;
using Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

internal sealed class OrganizationDashboardReadRepository(
	ApplicationDbContext dbContext)
	: IOrganizationDashboardReadRepository
{
	public async ValueTask<OrganizationDashboardResponse> GetKpisAsync(
		Guid organizationId,
		CancellationToken cancellationToken = default)
	{
		var orgId = new OrganizationId(organizationId);
		var now = DateTimeOffset.UtcNow;
		var sevenDaysLater = now.AddDays(7);

		var orgOpportunityIds = dbContext.VolunteerOpportunitiesQuery
			.Where(vo => vo.OrganizationId == orgId)
			.Select(vo => vo.Id);

		var openOpportunities = await orgOpportunityIds.CountAsync(cancellationToken);

		var pendingEngagements = await dbContext.EngagementsQuery
			.CountAsync(
				e => orgOpportunityIds.Contains(e.OpportunityId) && e.Status == EngagementStatus.Pending,
				cancellationToken);

		var confirmedEngagementsNext7Days = await dbContext.EngagementsQuery
			.Where(e => orgOpportunityIds.Contains(e.OpportunityId) && e.Status == EngagementStatus.Confirmed && e.TimeSlotId != null)
			.Join(
				dbContext.TimeSlotsQuery,
				e => e.TimeSlotId,
				ts => ts.Id,
				(e, ts) => ts.StartDateTime)
			.CountAsync(start => start >= now && start <= sevenDaysLater, cancellationToken);

		var cancelledEngagements = await dbContext.EngagementsQuery
			.CountAsync(
				e => orgOpportunityIds.Contains(e.OpportunityId) && e.Status == EngagementStatus.Cancelled,
				cancellationToken);

		return new OrganizationDashboardResponse(
			openOpportunities,
			pendingEngagements,
			confirmedEngagementsNext7Days,
			cancelledEngagements);
	}
}
