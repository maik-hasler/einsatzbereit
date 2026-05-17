using Application.Achievements.BadgeCatalog;
using Application.Common.Messaging;

namespace Application.Achievements.GetBadgeCatalog.v1;

internal sealed class GetBadgeCatalogQueryHandler(
	IBadgeCatalogService catalogService)
	: IQueryHandler<GetBadgeCatalogQuery, List<BadgeCatalogEntry>>
{
	public ValueTask<List<BadgeCatalogEntry>> Handle(
		GetBadgeCatalogQuery request,
		CancellationToken cancellationToken = default) =>
		ValueTask.FromResult(catalogService.GetAll().ToList());
}
