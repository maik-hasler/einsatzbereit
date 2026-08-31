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
	// The dashboard's sign-up trend compares this many days against the same
	// number of days before them. A month is the window a volunteer coordinator
	// plans in, and it is long enough that a single quiet week does not read as
	// a collapse.
	private const int SignUpWindowDays = 30;

	public async ValueTask<OrganizationDashboardResponse> GetKpisAsync(
		Guid organizationId,
		CancellationToken cancellationToken = default)
	{
		var orgId = OrganizationId.Create(organizationId).GetValueOrThrow();

		var orgOpportunityIds = dbContext.VolunteerOpportunitiesQuery
			.Where(vo => vo.OrganizationId == orgId)
			.Select(vo => vo.Id);

		var engagements = dbContext.EngagementsQuery
			.Where(e => orgOpportunityIds.Contains(e.OpportunityId));

		var countsByStatus = await engagements
			.GroupBy(e => e.Status)
			.Select(g => new { Status = g.Key, Count = g.Count() })
			.ToListAsync(cancellationToken);

		// "Volunteers" has to mean people. Counting confirmed engagements told an
		// organization with one very willing helper across twelve slots that it
		// had twelve volunteers. An anonymized engagement has no VolunteerId left
		// to attribute, so it is left out rather than counted as another person.
		var distinctVolunteersTotal = await engagements
			.Where(e => e.Status == EngagementStatus.Confirmed && e.VolunteerId != null)
			.Select(e => e.VolunteerId)
			.Distinct()
			.CountAsync(cancellationToken);

		// A running total says nothing about whether an organization is picking up
		// or going quiet, which is the only reason a number on a dashboard is worth
		// its space. Counted by when the sign-up arrived and across every status:
		// this is interest received, and filtering by what happened to each sign-up
		// afterwards would make the two windows measure different things.
		var now = DateTimeOffset.UtcNow;
		var windowStart = now.AddDays(-SignUpWindowDays);
		var previousWindowStart = now.AddDays(-2 * SignUpWindowDays);

		var signUpsByWindow = await engagements
			.Where(e => e.CreatedOn >= previousWindowStart)
			.GroupBy(e => e.CreatedOn >= windowStart)
			.Select(g => new { IsCurrentWindow = g.Key, Count = g.Count() })
			.ToListAsync(cancellationToken);

		return new OrganizationDashboardResponse(
			countsByStatus.FirstOrDefault(c => c.Status == EngagementStatus.Pending)?.Count ?? 0,
			countsByStatus.FirstOrDefault(c => c.Status == EngagementStatus.Confirmed)?.Count ?? 0,
			distinctVolunteersTotal,
			signUpsByWindow.FirstOrDefault(w => w.IsCurrentWindow)?.Count ?? 0,
			signUpsByWindow.FirstOrDefault(w => !w.IsCurrentWindow)?.Count ?? 0);
	}
}
