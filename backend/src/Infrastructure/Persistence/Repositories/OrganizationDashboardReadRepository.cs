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

		var orgOpportunityIds = dbContext.VolunteerOpportunitiesQuery
			.Where(vo => vo.OrganizationId == orgId)
			.Select(vo => vo.Id);

		var countsByStatus = await dbContext.EngagementsQuery
			.Where(e => orgOpportunityIds.Contains(e.OpportunityId))
			.GroupBy(e => e.Status)
			.Select(g => new { Status = g.Key, Count = g.Count() })
			.ToListAsync(cancellationToken);

		var pendingEngagements = countsByStatus
			.FirstOrDefault(c => c.Status == EngagementStatus.Pending)?.Count ?? 0;

		var confirmedEngagementsTotal = countsByStatus
			.FirstOrDefault(c => c.Status == EngagementStatus.Confirmed)?.Count ?? 0;

		return new OrganizationDashboardResponse(
			pendingEngagements,
			confirmedEngagementsTotal);
	}
}
