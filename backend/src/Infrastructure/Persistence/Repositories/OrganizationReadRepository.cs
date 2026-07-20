using Application.Common.Pagination;
using Application.Organizations;
using Application.Organizations.GetPublicOrganizations.v1;
using Domain.VolunteerOpportunities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

internal sealed class OrganizationReadRepository(
	ApplicationDbContext dbContext)
	: IOrganizationReadRepository
{
	public async ValueTask<PagedList<PublicOrganizationSummary>> GetPagedPublicSummariesAsync(
		OrganizationFilter filter,
		CancellationToken cancellationToken = default)
	{
		var query = dbContext.OrganizationsQuery;

		if (!string.IsNullOrWhiteSpace(filter.Search))
		{
			var search = filter.Search.ToLower();
			query = query.Where(o => o.Name.ToLower().Contains(search));
		}

		var total = await query.CountAsync(cancellationToken);

		var page = await query
			.OrderBy(o => o.Name)
			.Skip((filter.PageNumber - 1) * filter.PageSize)
			.Take(filter.PageSize)
			.Select(o => new
			{
				o.Id,
				o.Name,
				o.Description,
				City = o.Address != null ? o.Address.City : null,
				o.LogoUrl,
				o.IsVerified,
			})
			.ToListAsync(cancellationToken);

		if (page.Count == 0)
			return new PagedList<PublicOrganizationSummary>([], total, filter.PageNumber, filter.PageSize);

		var orgIds = page.Select(o => o.Id).ToList();

		var openCounts = await dbContext.VolunteerOpportunitiesQuery
			.Where(vo => orgIds.Contains(vo.OrganizationId) && vo.Status == OpportunityStatus.Published)
			.GroupBy(vo => vo.OrganizationId)
			.Select(g => new { OrganizationId = g.Key, Count = g.Count() })
			.ToDictionaryAsync(x => x.OrganizationId, x => x.Count, cancellationToken);

		var items = page
			.Select(o => new PublicOrganizationSummary(
				o.Id.Value,
				o.Name,
				o.Description,
				o.City,
				o.LogoUrl,
				o.IsVerified,
				openCounts.GetValueOrDefault(o.Id, 0)))
			.ToList();

		return new PagedList<PublicOrganizationSummary>(items, total, filter.PageNumber, filter.PageSize);
	}
}
