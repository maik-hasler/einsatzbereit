using Application.Common.Sitemap;
using Application.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

internal sealed class OrganizationReadRepository(
	ApplicationDbContext dbContext)
	: IOrganizationReadRepository
{
	public async ValueTask<IReadOnlyList<SitemapEntry>> GetAllForSitemapAsync(
		CancellationToken cancellationToken = default) =>
		await dbContext.OrganizationsQuery
			.Select(o => new SitemapEntry(o.Id.Value, o.ModifiedOn ?? o.CreatedOn))
			.ToListAsync(cancellationToken);
}
