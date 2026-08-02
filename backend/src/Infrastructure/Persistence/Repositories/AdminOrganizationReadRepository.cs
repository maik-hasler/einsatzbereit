using Application.Common.Pagination;
using Application.Organizations;
using Application.Organizations.ListOrganizations.v1;
using Infrastructure.Persistence.Extensions;

namespace Infrastructure.Persistence.Repositories;

internal sealed class AdminOrganizationReadRepository(
	ApplicationDbContext dbContext)
	: IAdminOrganizationReadRepository
{
	public async ValueTask<PagedList<AdminOrganizationSummary>> GetPagedAsync(
		int pageNumber,
		int pageSize,
		CancellationToken cancellationToken = default)
	{
		var paged = await dbContext.OrganizationsQuery
			.OrderBy(o => o.Name)
			.ThenBy(o => o.Id.Value)
			.ToPagedListAsync(pageNumber, pageSize, cancellationToken);

		return paged.Map(o => new AdminOrganizationSummary(o.Id.Value, o.Name, o.LogoUrl));
	}
}
