using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Organizations;
using Domain.Primitives;

namespace Application.Organizations.VerifyOrganization.v1;

internal sealed class VerifyOrganizationCommandHandler(
	IApplicationDbContext dbContext)
	: ICommandHandler<VerifyOrganizationCommand, bool>
{
	public async ValueTask<bool> Handle(
		VerifyOrganizationCommand request,
		CancellationToken cancellationToken = default)
	{
		var organization = await dbContext.Organizations.FindAsync(
			OrganizationId.Create(request.OrganizationId).GetValueOrThrow(), cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("Organization.NotFound", $"Organization '{request.OrganizationId}' not found."));

		if (request.IsVerified)
			organization.Verify().ThrowIfFailure();
		else
			organization.RevokeVerification().ThrowIfFailure();

		return true;
	}
}
