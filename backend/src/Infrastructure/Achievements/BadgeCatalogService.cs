using Application.Achievements.BadgeCatalog;
using Microsoft.Extensions.Options;

namespace Infrastructure.Achievements;

internal sealed class BadgeCatalogService(
	IOptions<BadgeCatalogOptions> options)
	: IBadgeCatalogService
{
	private readonly IReadOnlyList<BadgeCatalogEntry> _entries = options.Value.Badges;

	public IReadOnlyList<BadgeCatalogEntry> GetAll() => _entries;

	public BadgeCatalogEntry? FindByKey(string key) =>
		_entries.FirstOrDefault(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
}
