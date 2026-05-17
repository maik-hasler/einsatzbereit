using Application.Common.Geocoding;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.VolunteerOpportunities;
using Microsoft.Extensions.Logging;

namespace Application.VolunteerOpportunities.CreateVolunteerOpportunity.v1;

internal sealed class CreateVolunteerOpportunityCommandHandler(
	IApplicationDbContext dbContext,
	IGeocodingService geocodingService,
	ILogger<CreateVolunteerOpportunityCommandHandler> logger)
	: ICommandHandler<CreateVolunteerOpportunityCommand, VolunteerOpportunity>
{
	public async ValueTask<VolunteerOpportunity> Handle(
		CreateVolunteerOpportunityCommand request,
		CancellationToken cancellationToken = default)
	{
		var address = request.Address;

		if (!request.IsRemote && address is not null)
			address = await GeocodingHelper.EnrichAsync(address, geocodingService, logger, cancellationToken);

		var opportunity = VolunteerOpportunity.Create(
			request.OrganizationId,
			request.Title,
			request.Description,
			request.IsRemote,
			address,
			request.Occurrence,
			request.ParticipationType,
			request.CheckInMethod);

		await dbContext.VolunteerOpportunities.AddAsync(opportunity, cancellationToken);

		return opportunity;
	}
}
