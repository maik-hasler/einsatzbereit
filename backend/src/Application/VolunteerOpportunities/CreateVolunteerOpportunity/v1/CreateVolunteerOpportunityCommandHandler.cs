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

		// request.Address (built by CreateVolunteerOpportunityEndpoint) already
		// carries resolved coordinates on the happy path - it geocodes
		// synchronously, before this command ever dispatches, precisely so that
		// call can hold the up-to-1.1s Nominatim throttle (see
		// NominatimGeocodingService) without holding this command's transaction
		// open too (#1388, #1963). Only a TransientFailure there leaves address
		// uncoordinated; VolunteerOpportunity.Create then raises
		// VolunteerOpportunityGeocodingRequestedDomainEvent so
		// GeocodeVolunteerOpportunityAddressHandler retries it out of band.
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
			request.CheckInPin,
			request.ValidUntil).GetValueOrThrow();

		await dbContext.VolunteerOpportunities.AddAsync(opportunity, cancellationToken);

		return opportunity;
	}
}
