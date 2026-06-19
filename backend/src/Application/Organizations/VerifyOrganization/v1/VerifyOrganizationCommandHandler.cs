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
			new OrganizationId(request.OrganizationId), cancellationToken)
			?? throw new DomainException($"Organization '{request.OrganizationId}' not found.");

		organization.SetVerified(request.IsVerified);

		return true;
	}
}
