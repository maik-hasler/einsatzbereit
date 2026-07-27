using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Organizations;
using Domain.Primitives;

namespace Application.Organizations.AdminRestoreOrganization.v1;

/// <summary>
/// Undoes an admin shadow delete (<see cref="AdminShadowDeleteOrganization.v1.AdminShadowDeleteOrganizationCommandHandler"/>).
/// Only restores the organization itself - its opportunities were shadow-deleted
/// independently and need their own restore call, since some may have been
/// reported and taken down separately from the organization.
/// </summary>
internal sealed class AdminRestoreOrganizationCommandHandler(
	IApplicationDbContext dbContext)
	: ICommandHandler<AdminRestoreOrganizationCommand, bool>
{
	public async ValueTask<bool> Handle(
		AdminRestoreOrganizationCommand request,
		CancellationToken cancellationToken = default)
	{
		var organizationId = OrganizationId.Create(request.OrganizationId).GetValueOrThrow();

		var organization = await dbContext.FindOrganizationIncludingDeletedAsync(organizationId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("Organization.NotFound", $"Organization '{request.OrganizationId}' not found."));

		organization.Restore().ThrowIfFailure();

		return true;
	}
}
