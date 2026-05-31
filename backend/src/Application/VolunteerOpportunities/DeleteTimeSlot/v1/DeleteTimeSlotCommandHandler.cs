using Application.Common.Authorization;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Primitives;
using Domain.VolunteerOpportunities;

namespace Application.VolunteerOpportunities.DeleteTimeSlot.v1;

internal sealed class DeleteTimeSlotCommandHandler(
	IApplicationDbContext dbContext,
	IKeycloakOrganizationService keycloakOrgService)
	: ICommandHandler<DeleteTimeSlotCommand, bool>
{
	public async ValueTask<bool> Handle(
		DeleteTimeSlotCommand request,
		CancellationToken cancellationToken = default)
	{
		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(
			new VolunteerOpportunityId(request.OpportunityId), cancellationToken)
			?? throw new DomainException($"Volunteer opportunity '{request.OpportunityId}' not found.");

		await OwnershipGuard.EnsureIsOrgMemberAsync(
			keycloakOrgService,
			opportunity.OrganizationId.Value,
			request.RequestingUserId,
			cancellationToken);

		opportunity.RemoveTimeSlot(new TimeSlotId(request.TimeSlotId));

		return true;
	}
}
