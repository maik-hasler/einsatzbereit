using Application.Common.Email;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Engagements;
using Application.VolunteerOpportunities.Common;
using Domain.Primitives;
using Domain.VolunteerOpportunities;

namespace Application.VolunteerOpportunities.AdminShadowDeleteVolunteerOpportunity.v1;

/// <summary>
/// Admin-only takedown: unlike <see cref="DeleteVolunteerOpportunity.v1.DeleteVolunteerOpportunityCommandHandler"/>,
/// this bypasses <c>OwnershipGuard</c> entirely - the endpoint's
/// <c>EinsatzbereitAdminPolicy</c> gate is the only authorization check - and
/// shadow-deletes rather than removing the row, so a report-driven takedown is
/// restorable (see einsatzbereit#1075).
/// </summary>
internal sealed class AdminShadowDeleteVolunteerOpportunityCommandHandler(
	IApplicationDbContext dbContext,
	IEngagementReadRepository engagementReadRepository,
	IKeycloakUserService keycloakUserService,
	IEmailService emailService)
	: ICommandHandler<AdminShadowDeleteVolunteerOpportunityCommand, bool>
{
	public async ValueTask<bool> Handle(
		AdminShadowDeleteVolunteerOpportunityCommand request,
		CancellationToken cancellationToken = default)
	{
		var opportunityId = VolunteerOpportunityId.Create(request.OpportunityId).GetValueOrThrow();

		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(
			opportunityId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("VolunteerOpportunity.NotFound", $"Volunteer opportunity '{request.OpportunityId}' not found."));

		await VolunteerOpportunityDeletionHelper.ShadowDeleteAsync(
			dbContext,
			engagementReadRepository,
			keycloakUserService,
			emailService,
			opportunity,
			opportunityId,
			request.AdminUserId,
			DateTimeOffset.UtcNow,
			cancellationToken);

		return true;
	}
}
