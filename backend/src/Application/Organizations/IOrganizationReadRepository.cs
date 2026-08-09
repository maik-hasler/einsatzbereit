using Application.Common.Sitemap;

namespace Application.Organizations;

public interface IOrganizationReadRepository
{
	ValueTask<IReadOnlyList<SitemapEntry>> GetAllForSitemapAsync(
		CancellationToken cancellationToken = default);
}
