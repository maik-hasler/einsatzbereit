using Application.Common.Authorization;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Organizations;
using Domain.Primitives;

namespace Application.Organizations.DeleteOrganization.v1;

internal sealed class DeleteOrganizationCommandHandler(
	IApplicationDbContext dbContext,
	IKeycloakOrganizationService keycloakOrganizationService)
	: ICommandHandler<DeleteOrganizationCommand, bool>
{
	public async ValueTask<bool> Handle(
		DeleteOrganizationCommand request,
		CancellationToken cancellationToken = default)
	{
		var organizationId = new OrganizationId(request.OrganizationId);

		var organization = await dbContext.Organizations.FindAsync(organizationId, cancellationToken)
			?? throw new DomainException($"Organization '{request.OrganizationId}' not found.");

		await OwnershipGuard.EnsureIsOrgMemberAsync(
			keycloakOrganizationService,
			request.OrganizationId,
			request.RequestingUserId,
			cancellationToken);

		var members = await keycloakOrganizationService.GetMembersAsync(
			request.OrganizationId, cancellationToken);

		if (members.Count > 1)
			throw new DomainException(
				"Conflict: only the organization's sole remaining member can delete it. Remove the other members first.");

		var blockingOpportunities = await dbContext.GetBlockingOpportunitiesForOrganizationAsync(
			organizationId, cancellationToken);

		if (blockingOpportunities.Count > 0)
		{
			var titles = string.Join(", ", blockingOpportunities.Select(o => $"'{o.Title}'"));
			throw new DomainException(
				$"Conflict: cannot delete organization while it has opportunities with future time slots or active engagements: {titles}. Resolve or cancel these first.");
		}

		await keycloakOrganizationService.DeleteOrganizationAsync(
			request.OrganizationId, cancellationToken);

		dbContext.Organizations.Delete(organization);

		return true;
	}
}
