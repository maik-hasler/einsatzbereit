using Application.Common.Authorization;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Organizations;
using Domain.Primitives;

namespace Application.Organizations.ResetDashboardLayout.v1;

internal sealed class ResetDashboardLayoutCommandHandler(
	IApplicationDbContext dbContext)
	: ICommandHandler<ResetDashboardLayoutCommand, bool>
{
	public async ValueTask<bool> Handle(
		ResetDashboardLayoutCommand request,
		CancellationToken cancellationToken = default)
	{
		var organizationId = OrganizationId.Create(request.OrganizationId).GetValueOrThrow();

		_ = await dbContext.Organizations.FindAsync(organizationId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("Organization.NotFound", $"Organization '{request.OrganizationId}' not found."));

		await OwnershipGuard.EnsureIsOrganizerAsync(
			dbContext,
			request.OrganizationId,
			request.RequestingUserId,
			cancellationToken);

		var layout = await dbContext.GetDashboardLayoutAsync(
			organizationId, request.RequestingUserId, cancellationToken);

		// Deliberately idempotent: with no saved layout the caller is already
		// on the default one, which is exactly the state they asked for.
		if (layout is not null)
			dbContext.OrganizationDashboardLayouts.Delete(layout);

		return true;
	}
}
