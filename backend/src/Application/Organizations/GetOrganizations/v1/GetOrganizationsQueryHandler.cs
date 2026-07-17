using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;

namespace Application.Organizations.GetOrganizations.v1;

internal sealed class GetOrganizationsQueryHandler(
	IKeycloakOrganizationService keycloakOrganizationService,
	IApplicationDbContext dbContext)
	: IQueryHandler<GetOrganizationsQuery, IReadOnlyList<OrganizationSummaryDto>>
{
	public async ValueTask<IReadOnlyList<OrganizationSummaryDto>> Handle(
		GetOrganizationsQuery request,
		CancellationToken cancellationToken = default)
	{
		var organizations = await keycloakOrganizationService.GetUserOrganizationsAsync(
			request.UserId, cancellationToken);

		var slugs = await dbContext.GetOrganizationSlugsAsync(
			organizations.Select(o => o.Id).ToList(), cancellationToken);

		return organizations
			.Select(o => new OrganizationSummaryDto(o.Id, o.Name, slugs.GetValueOrDefault(o.Id)))
			.ToList();
	}
}
