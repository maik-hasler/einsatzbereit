using Application.Common.Authorization;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.VolunteerOpportunities;

namespace Application.VolunteerOpportunities.CreateVolunteerOpportunity.v1;

internal sealed class CreateVolunteerOpportunityCommandHandler(
	IApplicationDbContext dbContext,
	IPinGenerator pinGenerator)
	: ICommandHandler<CreateVolunteerOpportunityCommand, VolunteerOpportunity>
{
	public async ValueTask<VolunteerOpportunity> Handle(
		CreateVolunteerOpportunityCommand request,
		CancellationToken cancellationToken = default)
	{
		await OwnershipGuard.EnsureIsOrganizerAsync(
			dbContext,
			request.OrganizationId.Value,
			request.RequestingUserId,
			cancellationToken);

		// Geocoding (and the up-to-1.1s Nominatim throttle it can wait on, see
		// NominatimGeocodingService) happens out of band after this command's
		// transaction commits - VolunteerOpportunity.Create raises
		// VolunteerOpportunityGeocodingRequestedDomainEvent for a non-remote
		// address, picked up by GeocodeVolunteerOpportunityAddressHandler (#1388).
		var opportunity = VolunteerOpportunity.Create(
			request.OrganizationId,
			request.Title,
			request.Description,
			request.IsRemote,
			request.Address,
			request.Occurrence,
			request.ParticipationType,
			request.CheckInMethod,
			pinGenerator,
			request.Category,
			request.Tags,
			request.Status,
			request.CheckInPin).GetValueOrThrow();

		await dbContext.VolunteerOpportunities.AddAsync(opportunity, cancellationToken);

		return opportunity;
	}
}
