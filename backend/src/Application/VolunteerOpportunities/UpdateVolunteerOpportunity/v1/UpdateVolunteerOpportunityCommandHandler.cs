using Application.Common.Authorization;
using Application.Common.Email;
using Application.Common.Exceptions;
using Application.Common.Geocoding;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Engagements;
using Application.Notifications;
using Domain.Common;
using Domain.Notifications;
using Domain.Primitives;
using Domain.VolunteerOpportunities;
using Microsoft.Extensions.Logging;

namespace Application.VolunteerOpportunities.UpdateVolunteerOpportunity.v1;

internal sealed class UpdateVolunteerOpportunityCommandHandler(
	IApplicationDbContext dbContext,
	IEngagementReadRepository engagementReadRepository,
	IGeocodingService geocodingService,
	IPinGenerator pinGenerator,
	IKeycloakUserService keycloakUserService,
	IEmailService emailService,
	IEmailTemplateRenderer emailTemplateRenderer,
	ILogger<UpdateVolunteerOpportunityCommandHandler> logger)
	: ICommandHandler<UpdateVolunteerOpportunityCommand, bool>
{
	public async ValueTask<bool> Handle(
		UpdateVolunteerOpportunityCommand request,
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

		if (request.ParticipationType != opportunity.ParticipationType)
		{
			var engagements = await engagementReadRepository.GetByOpportunityAsync(
				opportunityId, cancellationToken);

			var hasActiveEngagements = engagements.Any(e =>
				e.Status is "Pending" or "Confirmed");

			if (hasActiveEngagements)
				throw new ResultFailureException(Error.Conflict(
					"VolunteerOpportunity.ParticipationTypeLocked",
					"ParticipationType cannot be changed while active engagements exist."));
		}

		var address = request.Address;

		// Only re-geocode when the address text actually changed (or is newly added
		// after switching away from remote) - re-running it on every unrelated edit
		// would re-block a save on a legacy address that was already accepted before
		// NotFound became a hard validation error.
		if (!request.IsRemote && address is not null)
		{
			address = AddressTextChanged(opportunity.Address, address)
				? (await GeocodingHelper.EnrichAsync(address, geocodingService, logger, cancellationToken)).GetValueOrThrow()
				: opportunity.Address;
		}

		// Snapshot material fields before mutation to detect meaningful changes.
		var prevIsRemote = opportunity.IsRemote;
		var prevAddress = opportunity.Address;
		var prevOccurrence = opportunity.Occurrence;

		opportunity.Rename(request.Title).ThrowIfFailure();
		opportunity.ChangeDescription(request.Description).ThrowIfFailure();
		opportunity.Relocate(request.IsRemote, address).ThrowIfFailure();
		opportunity.Reschedule(request.Occurrence);
		opportunity.Recategorize(request.Category, request.Tags);
		opportunity.ChangeCheckInMethod(request.CheckInMethod, pinGenerator, request.CheckInPin).ThrowIfFailure();
		opportunity.SwitchParticipationType(request.ParticipationType);

		// Only notify on material changes (location or schedule); cosmetic edits
		// (title, description, tags) must not spam engaged volunteers.
		var materialChanged =
			prevIsRemote != request.IsRemote ||
			prevOccurrence != request.Occurrence ||
			AddressTextChanged(prevAddress, request.Address);

		if (materialChanged)
			await OpportunityNotificationHelper.NotifyActiveVolunteersAsync(
				dbContext,
				engagementReadRepository,
				opportunityId,
				NotificationKind.OpportunityUpdated,
				cancellationToken,
				keycloakUserService: keycloakUserService,
				emailService: emailService,
				emailTemplateRenderer: emailTemplateRenderer,
				opportunityTitle: opportunity.Title);

		return true;
	}

	private static bool AddressTextChanged(Address? prev, Address? next) =>
		prev?.Street != next?.Street ||
		prev?.HouseNumber != next?.HouseNumber ||
		prev?.ZipCode != next?.ZipCode ||
		prev?.City != next?.City;
}
