using Application.Achievements.BadgeCatalog;

namespace Infrastructure.Achievements;

internal sealed class BadgeCatalogOptions
{
	public List<BadgeCatalogEntry> Badges { get; init; } = [];
}
