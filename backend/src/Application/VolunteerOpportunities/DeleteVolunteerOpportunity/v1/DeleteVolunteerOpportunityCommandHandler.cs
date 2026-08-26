using Application.Common.Authorization;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Common.Storage;
using Application.Engagements;
using Application.VolunteerOpportunities.Common;
using Domain.Primitives;
using Domain.VolunteerOpportunities;
using Microsoft.Extensions.Logging;

namespace Application.VolunteerOpportunities.DeleteVolunteerOpportunity.v1;

internal sealed class DeleteVolunteerOpportunityCommandHandler(
	IApplicationDbContext dbContext,
	IEngagementReadRepository engagementReadRepository,
	IFileStorageService fileStorage,
	ILogger<DeleteVolunteerOpportunityCommandHandler> logger)
	: ICommandHandler<DeleteVolunteerOpportunityCommand, bool>
{
	public async ValueTask<bool> Handle(
		DeleteVolunteerOpportunityCommand request,
		CancellationToken cancellationToken = default)
	{
		var opportunityId = VolunteerOpportunityId.Create(request.OpportunityId).GetValueOrThrow();

		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(
			opportunityId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("VolunteerOpportunity.NotFound", $"Volunteer opportunity '{request.OpportunityId}' not found."));

		await OwnershipGuard.EnsureIsOrganizerAsync(
			dbContext,
			opportunity.OrganizationId.Value,
			request.RequestingUserId,
			cancellationToken);

		await VolunteerOpportunityDeletionHelper.DeleteAsync(
			dbContext,
			engagementReadRepository,
			fileStorage,
			opportunity,
			opportunityId,
			request.RequestingUserId,
			DateTimeOffset.UtcNow,
			logger,
			cancellationToken);

		return true;
	}
}
