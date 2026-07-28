using Application.Common.Exceptions;
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
		var orgId = OrganizationId.Create(organizationId).GetValueOrThrow();
		var now = DateTimeOffset.UtcNow;
		var sevenDaysLater = now.AddDays(7);

		// Kept as an IQueryable (not materialized) so every use below compiles to a
		// correlated "IN (SELECT ...)" subquery instead of shipping the id list to
		// and from Postgres as a literal array.
		var orgOpportunityIds = dbContext.VolunteerOpportunitiesQuery
			.Where(vo => vo.OrganizationId == orgId)
			.Select(vo => vo.Id);

		var openOpportunities = await dbContext.VolunteerOpportunitiesQuery
			.CountAsync(vo => vo.OrganizationId == orgId, cancellationToken);

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

		var confirmedEngagementsTotal = countsByStatus
			.FirstOrDefault(c => c.Status == EngagementStatus.Confirmed)?.Count ?? 0;

		// The "next 7 days" metric needs a join to time slots plus a date filter,
		// so it stays a dedicated query (still reusing the same subquery).
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
			confirmedEngagementsTotal,
			cancelledEngagements);
	}
}
