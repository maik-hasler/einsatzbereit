using Application.Common.Authorization;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Primitives;
using Domain.VolunteerOpportunities;

namespace Application.VolunteerOpportunities.UnpublishVolunteerOpportunity.v1;

internal sealed class UnpublishVolunteerOpportunityCommandHandler(
	IApplicationDbContext dbContext)
	: ICommandHandler<UnpublishVolunteerOpportunityCommand, bool>
{
	public async ValueTask<bool> Handle(
		UnpublishVolunteerOpportunityCommand request,
		CancellationToken cancellationToken = default)
	{
		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(
			VolunteerOpportunityId.Create(request.OpportunityId).GetValueOrThrow(), cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("VolunteerOpportunity.NotFound", $"Volunteer opportunity '{request.OpportunityId}' not found."));

		await OwnershipGuard.EnsureIsOrganizerAsync(
			dbContext,
			opportunity.OrganizationId.Value,
			request.RequestingUserId,
			cancellationToken);

		// Cascade-cancelling active engagements + notifying volunteers happens
		// asynchronously via the outbox (VolunteerOpportunityUnpublishedDomainEventHandler),
		// not inline here - see Unpublish()'s doc comment on OpportunityStatus.
		opportunity.Unpublish().ThrowIfFailure();

		return true;
	}
}
