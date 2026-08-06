using Application.Common.Authorization;
using Application.Common.Email;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Engagements;
using Application.Notifications;
using Domain.Common;
using Domain.Notifications;
using Domain.Primitives;
using Domain.VolunteerOpportunities;

namespace Application.VolunteerOpportunities.UpdateVolunteerOpportunity.v1;

internal sealed class UpdateVolunteerOpportunityCommandHandler(
	IApplicationDbContext dbContext,
	IEngagementReadRepository engagementReadRepository,
	IPinGenerator pinGenerator,
	IKeycloakUserService keycloakUserService,
	IEmailService emailService,
	IEmailTemplateRenderer emailTemplateRenderer)
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

			// Widened from "Pending or Confirmed" to any engagement at all (#1145):
			// switching away from ScheduledSlots clears every time slot
			// (VolunteerOpportunity.SwitchParticipationType), which cascade-deletes
			// the slot rows and sets Withdrawn/Cancelled/checked-in-and-completed
			// engagements' TimeSlotId to null too, silently erasing their date -
			// not just the active ones the old guard checked.
			if (engagements.Count > 0)
				throw new ResultFailureException(Error.Conflict(
					"VolunteerOpportunity.ParticipationTypeLocked",
					"ParticipationType cannot be changed while any engagement exists for this opportunity."));
		}

		var prevIsRemote = opportunity.IsRemote;
		var prevAddress = opportunity.Address;
		var prevOccurrence = opportunity.Occurrence;

		opportunity.Rename(request.Title).ThrowIfFailure();
		opportunity.ChangeDescription(request.Description).ThrowIfFailure();

		// Relocate raises VolunteerOpportunityGeocodingRequestedDomainEvent itself
		// when the address text actually changed (or is newly added after
		// switching away from remote), and skips re-resolving an unchanged
		// address (see GeocodeVolunteerOpportunityAddressHandler for the
		// out-of-band geocoding attempt this triggers - #1388).
		opportunity.Relocate(request.IsRemote, request.Address).ThrowIfFailure();
		opportunity.Reschedule(request.Occurrence);
		opportunity.Recategorize(request.Category, request.Tags).ThrowIfFailure();
		opportunity.ChangeCheckInMethod(request.CheckInMethod, pinGenerator, request.CheckInPin).ThrowIfFailure();
		opportunity.SwitchParticipationType(request.ParticipationType);
		opportunity.SetValidUntil(request.ValidUntil, DateTimeOffset.UtcNow).ThrowIfFailure();

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
