using Application.Common.Pagination;
using Application.Organizations;
using Application.Organizations.GetPublicOrganizations.v1;
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

		var items = await query
			.OrderBy(o => o.Name)
			.Skip((filter.PageNumber - 1) * filter.PageSize)
			.Take(filter.PageSize)
			.Select(o => new PublicOrganizationSummary(
				o.Id.Value,
				o.Name,
				o.Description,
				o.Address != null ? o.Address.City : null,
				o.LogoUrl,
				o.IsVerified))
			.ToListAsync(cancellationToken);

		return new PagedList<PublicOrganizationSummary>(items, total, filter.PageNumber, filter.PageSize);
	}
}
