using Application.Common.Geocoding;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Primitives;
using Domain.VolunteerOpportunities;
using Microsoft.Extensions.Logging;

namespace Application.VolunteerOpportunities.UpdateVolunteerOpportunity.v1;

internal sealed class UpdateVolunteerOpportunityCommandHandler(
	IApplicationDbContext dbContext,
	IGeocodingService geocodingService,
	ILogger<UpdateVolunteerOpportunityCommandHandler> logger)
	: ICommandHandler<UpdateVolunteerOpportunityCommand, bool>
{
	public async ValueTask<bool> Handle(
		UpdateVolunteerOpportunityCommand request,
		CancellationToken cancellationToken = default)
	{
		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(
			new VolunteerOpportunityId(request.OpportunityId), cancellationToken)
			?? throw new DomainException($"Volunteer opportunity '{request.OpportunityId}' not found.");

		var address = request.Address;

		if (!request.IsRemote && address is not null)
			address = await GeocodingHelper.EnrichAsync(address, geocodingService, logger, cancellationToken);

		opportunity.Update(request.Title, request.Description, request.IsRemote, address, request.CheckInMethod);

		return true;
	}
}
