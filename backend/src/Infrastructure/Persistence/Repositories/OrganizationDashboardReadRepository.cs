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

		// Materialize the org's opportunity ids once instead of re-running the
		// same subquery inside every count below.
		var orgOpportunityIds = await dbContext.VolunteerOpportunitiesQuery
			.Where(vo => vo.OrganizationId == orgId)
			.Select(vo => vo.Id)
			.ToListAsync(cancellationToken);

		var openOpportunities = orgOpportunityIds.Count;

		// A single grouped query covers every status breakdown (pending, cancelled, ...).
		var countsByStatus = await dbContext.EngagementsQuery
			.Where(e => orgOpportunityIds.Contains(e.OpportunityId))
			.GroupBy(e => e.Status)
			.Select(g => new { Status = g.Key, Count = g.Count() })
			.ToListAsync(cancellationToken);

		var pendingEngagements = countsByStatus
			.FirstOrDefault(c => c.Status == EngagementStatus.Pending)?.Count ?? 0;

		var cancelledEngagements = countsByStatus
			.FirstOrDefault(c => c.Status == EngagementStatus.Cancelled)?.Count ?? 0;

		// The "next 7 days" metric needs a join to time slots plus a date filter,
		// so it stays a dedicated query (still using the materialized id list).
		var confirmedEngagementsNext7Days = await dbContext.EngagementsQuery
			.Where(e => orgOpportunityIds.Contains(e.OpportunityId) && e.Status == EngagementStatus.Confirmed && e.TimeSlotId != null)
			.Join(
				dbContext.TimeSlotsQuery,
				e => e.TimeSlotId,
				ts => ts.Id,
				(e, ts) => ts.StartDateTime)
			.CountAsync(start => start >= now && start <= sevenDaysLater, cancellationToken);

		return new OrganizationDashboardResponse(
			openOpportunities,
			pendingEngagements,
			confirmedEngagementsNext7Days,
			cancelledEngagements);
	}
}
