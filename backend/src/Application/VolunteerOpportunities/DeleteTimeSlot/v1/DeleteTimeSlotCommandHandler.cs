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

		var timeSlotId = new TimeSlotId(request.TimeSlotId);

		var activeCount = await dbContext.CountActiveEngagementsForTimeSlotAsync(timeSlotId, cancellationToken);
		if (activeCount > 0)
			throw new DomainException(
				$"Cannot delete a time slot that has {activeCount} active sign-up(s). Cancel the affected engagements first.");

		opportunity.RemoveTimeSlot(timeSlotId);

		return true;
	}
}
