using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Users;

namespace Application.Organizations.GetOrganizations.v1;

internal sealed class GetOrganizationsQueryHandler(
	IApplicationDbContext dbContext)
	: IQueryHandler<GetOrganizationsQuery, IReadOnlyList<OrganizationSummaryDto>>
{
	public async ValueTask<IReadOnlyList<OrganizationSummaryDto>> Handle(
		GetOrganizationsQuery request,
		CancellationToken cancellationToken = default)
	{
		var organizations = await dbContext.GetOrganizerOrganizationsAsync(
			UserId.Create(request.UserId).GetValueOrThrow(), cancellationToken);

		return organizations
			.Select(o => new OrganizationSummaryDto(o.Id.Value, o.Name, o.LogoUrl))
			.ToList();
	}
}
