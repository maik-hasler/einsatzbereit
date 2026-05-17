namespace Application.Achievements.BadgeCatalog;

public interface IBadgeCatalogService
{
	IReadOnlyList<BadgeCatalogEntry> GetAll();

	BadgeCatalogEntry? FindByKey(string key);
}
