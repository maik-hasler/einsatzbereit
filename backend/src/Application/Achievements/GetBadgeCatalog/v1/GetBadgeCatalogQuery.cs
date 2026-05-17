using Application.Achievements.BadgeCatalog;
using Application.Common.Messaging;

namespace Application.Achievements.GetBadgeCatalog.v1;

public sealed record GetBadgeCatalogQuery
	: IQuery<List<BadgeCatalogEntry>>;
