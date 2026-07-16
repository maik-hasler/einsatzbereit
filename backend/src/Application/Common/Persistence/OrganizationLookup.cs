using Domain.Organizations;

namespace Application.Common.Persistence;

public static class OrganizationLookup
{
	public static async Task<Organization?> FindByIdOrSlugAsync(
		IApplicationDbContext dbContext,
		string idOrSlug,
		CancellationToken cancellationToken) =>
		Guid.TryParse(idOrSlug, out var guid)
			? await dbContext.Organizations.FindAsync(new OrganizationId(guid), cancellationToken)
			: await dbContext.FindOrganizationBySlugAsync(idOrSlug, cancellationToken);
}
