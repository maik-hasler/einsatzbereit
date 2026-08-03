using Application.Common.Authorization;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Organizations;

namespace Application.Organizations.GetOrganizationDashboard.v1;

internal sealed class GetOrganizationDashboardQueryHandler(
	IApplicationDbContext dbContext,
	IOrganizationDashboardReadRepository readRepository)
	: IQueryHandler<GetOrganizationDashboardQuery, OrganizationDashboardResponse?>
{
	public async ValueTask<OrganizationDashboardResponse?> Handle(
		GetOrganizationDashboardQuery request,
		CancellationToken cancellationToken = default)
	{
		var organization = await dbContext.Organizations.FindAsync(
			OrganizationId.Create(request.OrganizationId).GetValueOrThrow(), cancellationToken);

		if (organization is null)
			return null;

		await OwnershipGuard.EnsureIsMemberAsync(
			dbContext,
			request.OrganizationId,
			request.RequestingUserId,
			cancellationToken);

		return await readRepository.GetKpisAsync(request.OrganizationId, cancellationToken);
	}
}
