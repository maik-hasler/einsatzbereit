using Application.Common.Exceptions;
using Domain.Organizations;

namespace Application.Common.Persistence;

public static class OrganizationLookup
{
	public static async Task<Organization?> FindByIdOrSlugAsync(
		IApplicationDbContext dbContext,
		string idOrSlug,
		CancellationToken cancellationToken) =>
		Guid.TryParse(idOrSlug, out var guid)
			? await dbContext.Organizations.FindAsync(OrganizationId.Create(guid).GetValueOrThrow(), cancellationToken)
			: await dbContext.FindOrganizationBySlugAsync(idOrSlug, cancellationToken);
}
