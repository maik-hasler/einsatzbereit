using Application.Common.Authorization;
using Application.Common.Geocoding;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Engagements;
using Application.Notifications;
using Domain.Notifications;
using Domain.Primitives;
using Domain.VolunteerOpportunities;
using Microsoft.Extensions.Logging;

namespace Application.VolunteerOpportunities.UpdateVolunteerOpportunity.v1;

internal sealed class UpdateVolunteerOpportunityCommandHandler(
	IApplicationDbContext dbContext,
	IEngagementReadRepository engagementReadRepository,
	IGeocodingService geocodingService,
	IKeycloakOrganizationService keycloakOrgService,
	ILogger<UpdateVolunteerOpportunityCommandHandler> logger)
	: ICommandHandler<UpdateVolunteerOpportunityCommand, bool>
{
	public async ValueTask<bool> Handle(
		UpdateVolunteerOpportunityCommand request,
		CancellationToken cancellationToken = default)
	{
		var opportunityId = new VolunteerOpportunityId(request.OpportunityId);

		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(
			opportunityId, cancellationToken)
			?? throw new DomainException($"Volunteer opportunity '{request.OpportunityId}' not found.");

		await OwnershipGuard.EnsureIsOrgMemberAsync(
			keycloakOrgService,
			opportunity.OrganizationId.Value,
			request.RequestingUserId,
			cancellationToken);

		if (request.ParticipationType != opportunity.ParticipationType)
		{
			var engagements = await engagementReadRepository.GetByOpportunityAsync(
				opportunityId, cancellationToken);

			var hasActiveEngagements = engagements.Any(e =>
				e.Status is "Pending" or "Confirmed");

			if (hasActiveEngagements)
				throw new DomainException(
					"ParticipationType cannot be changed while active engagements exist.");
		}

		var title = opportunity.Status == OpportunityStatus.Draft
			&& string.IsNullOrWhiteSpace(request.Title)
				? "Unbenannt"
				: request.Title;

		var address = request.Address;

		if (!request.IsRemote && address is not null)
			address = await GeocodingHelper.EnrichAsync(address, geocodingService, logger, cancellationToken);

		opportunity.Update(
			title,
			request.Description,
			request.IsRemote,
			address,
			request.Occurrence,
			request.ParticipationType,
			request.CheckInMethod,
			request.Category,
			request.Tags);

		// Notify volunteers with an active engagement that details changed (#406).
		await OpportunityNotificationHelper.NotifyActiveVolunteersAsync(
			dbContext,
			engagementReadRepository,
			opportunityId,
			NotificationKind.OpportunityUpdated,
			cancellationToken);

		return true;
	}
}
