using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Engagements;
using Application.VolunteerOpportunities.Common;
using Domain.Primitives;
using Domain.VolunteerOpportunities;

namespace Application.VolunteerOpportunities.AdminDeleteVolunteerOpportunity.v1;

/// <summary>
/// Admin-only takedown: unlike <see cref="DeleteVolunteerOpportunity.v1.DeleteVolunteerOpportunityCommandHandler"/>,
/// this bypasses <c>OwnershipGuard</c> entirely - the endpoint's
/// <c>EinsatzbereitAdminPolicy</c> gate is the only authorization check
/// (see einsatzbereit#1075: admins previously had no delete lever at all).
/// </summary>
internal sealed class AdminDeleteVolunteerOpportunityCommandHandler(
	IApplicationDbContext dbContext,
	IEngagementReadRepository engagementReadRepository)
	: ICommandHandler<AdminDeleteVolunteerOpportunityCommand, bool>
{
	public async ValueTask<bool> Handle(
		AdminDeleteVolunteerOpportunityCommand request,
		CancellationToken cancellationToken = default)
	{
		var opportunityId = VolunteerOpportunityId.Create(request.OpportunityId).GetValueOrThrow();

		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(
			opportunityId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("VolunteerOpportunity.NotFound", $"Volunteer opportunity '{request.OpportunityId}' not found."));

		await VolunteerOpportunityDeletionHelper.DeleteAsync(
			dbContext,
			engagementReadRepository,
			opportunity,
			opportunityId,
			request.AdminUserId,
			DateTimeOffset.UtcNow,
			cancellationToken);

		return true;
	}
}
